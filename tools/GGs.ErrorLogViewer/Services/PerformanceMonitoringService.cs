using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GGs.ErrorLogViewer.Models;

namespace GGs.ErrorLogViewer.Services
{
    public interface IPerformanceMonitoringService
    {
        PerformanceMetrics CurrentMetrics { get; }
        IReadOnlyList<PerformanceMetrics> MetricsHistory { get; }
        event EventHandler<PerformanceMetrics>? MetricsUpdated;
        void StartMonitoring();
        void StopMonitoring();
        bool IsMonitoring { get; }
    }

    public class PerformanceMonitoringService : IPerformanceMonitoringService, IDisposable
    {
        private readonly Process _currentProcess;
        private readonly List<PerformanceMetrics> _metricsHistory;
        private readonly DateTime _startTime;
        private Timer? _monitoringTimer;
        private PerformanceMetrics _currentMetrics;
        private bool _isMonitoring;
        private long _previousDiskReads;
        private long _previousDiskWrites;
        private int _logsProcessedSinceLastCheck;
        private readonly object _lockObject = new object();

        public PerformanceMetrics CurrentMetrics => _currentMetrics;
        public IReadOnlyList<PerformanceMetrics> MetricsHistory => _metricsHistory.AsReadOnly();
        public event EventHandler<PerformanceMetrics>? MetricsUpdated;
        public bool IsMonitoring => _isMonitoring;

        public PerformanceMonitoringService()
        {
            _currentProcess = Process.GetCurrentProcess();
            _metricsHistory = new List<PerformanceMetrics>();
            _startTime = DateTime.Now;
            _currentMetrics = new PerformanceMetrics
            {
                Timestamp = DateTime.Now,
                Uptime = TimeSpan.Zero
            };
        }

        public void StartMonitoring()
        {
            if (_isMonitoring) return;

            _isMonitoring = true;
            _monitoringTimer = new Timer(CollectMetrics, null, TimeSpan.Zero, TimeSpan.FromSeconds(2));
        }

        public void StopMonitoring()
        {
            if (!_isMonitoring) return;

            _isMonitoring = false;
            _monitoringTimer?.Dispose();
            _monitoringTimer = null;
        }

        private void CollectMetrics(object? state)
        {
            try
            {
                lock (_lockObject)
                {
                    _currentProcess.Refresh();

                    var metrics = new PerformanceMetrics
                    {
                        Timestamp = DateTime.Now,
                        CpuUsage = GetCpuUsage(),
                        MemoryUsageMB = _currentProcess.WorkingSet64 / (1024.0 * 1024.0),
                        DiskReadKBps = 0, // Simplified - would need performance counters
                        DiskWriteKBps = 0, // Simplified - would need performance counters
                        LogsPerSecond = _logsProcessedSinceLastCheck / 2, // Collected every 2 seconds
                        TotalLogsProcessed = _metricsHistory.Sum(m => m.LogsPerSecond * 2),
                        Uptime = DateTime.Now - _startTime
                    };

                    _currentMetrics = metrics;
                    _metricsHistory.Add(metrics);
                    
                    // Keep only last 5 minutes of data
                    while (_metricsHistory.Count > 150)
                    {
                        _metricsHistory.RemoveAt(0);
                    }

                    _logsProcessedSinceLastCheck = 0;

                    MetricsUpdated?.Invoke(this, metrics);
                }
            }
            catch (Exception)
            {
                // Silent fail to avoid disrupting the application
            }
        }

        private double GetCpuUsage()
        {
            try
            {
                // Simplified CPU usage calculation
                var totalProcessorTime = _currentProcess.TotalProcessorTime.TotalMilliseconds;
                var uptime = (DateTime.Now - _startTime).TotalMilliseconds;
                
                if (uptime > 0)
                {
                    var cpuUsage = (totalProcessorTime / (Environment.ProcessorCount * uptime)) * 100;
                    return Math.Min(100, Math.Max(0, cpuUsage));
                }
            }
            catch
            {
                // Fallback
            }
            
            return 0;
        }

        public void RecordLogProcessed()
        {
            Interlocked.Increment(ref _logsProcessedSinceLastCheck);
        }

        public void Dispose()
        {
            StopMonitoring();
            _monitoringTimer?.Dispose();
        }
    }
}
