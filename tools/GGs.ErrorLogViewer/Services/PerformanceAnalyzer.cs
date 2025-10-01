#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace GGs.ErrorLogViewer.Services
{
    public interface IPerformanceAnalyzer
    {
        void StartOperation(string operationName);
        void EndOperation(string operationName);
        PerformanceAnalyzerMetrics GetMetrics();
        void RecordMemoryUsage();
        void Clear();
    }

    public class PerformanceAnalyzer : IPerformanceAnalyzer
    {
        private readonly Dictionary<string, Stopwatch> _activeOperations = new();
        private readonly Dictionary<string, List<long>> _operationTimes = new();
        private readonly List<long> _memorySnapshots = new();
        private readonly ILogger<PerformanceAnalyzer> _logger;
        private readonly object _lock = new();

        public PerformanceAnalyzer(ILogger<PerformanceAnalyzer> logger)
        {
            _logger = logger;
        }

        public void StartOperation(string operationName)
        {
            lock (_lock)
            {
                if (!_activeOperations.ContainsKey(operationName))
                {
                    _activeOperations[operationName] = Stopwatch.StartNew();
                }
            }
        }

        public void EndOperation(string operationName)
        {
            lock (_lock)
            {
                if (_activeOperations.TryGetValue(operationName, out var stopwatch))
                {
                    stopwatch.Stop();
                    
                    if (!_operationTimes.ContainsKey(operationName))
                    {
                        _operationTimes[operationName] = new List<long>();
                    }
                    
                    _operationTimes[operationName].Add(stopwatch.ElapsedMilliseconds);
                    _activeOperations.Remove(operationName);

                    if (stopwatch.ElapsedMilliseconds > 1000)
                    {
                        _logger.LogWarning("Operation '{Operation}' took {Ms}ms", 
                            operationName, stopwatch.ElapsedMilliseconds);
                    }
                }
            }
        }

        public PerformanceAnalyzerMetrics GetMetrics()
        {
            lock (_lock)
            {
                var metrics = new PerformanceAnalyzerMetrics
                {
                    Operations = _operationTimes.ToDictionary(
                        kvp => kvp.Key,
                        kvp => new OperationMetrics
                        {
                            Count = kvp.Value.Count,
                            AverageMs = kvp.Value.Average(),
                            MinMs = kvp.Value.Min(),
                            MaxMs = kvp.Value.Max(),
                            TotalMs = kvp.Value.Sum()
                        }),
                    CurrentMemoryMB = GC.GetTotalMemory(false) / 1024.0 / 1024.0,
                    PeakMemoryMB = _memorySnapshots.Any() ? _memorySnapshots.Max() / 1024.0 / 1024.0 : 0
                };

                return metrics;
            }
        }

        public void RecordMemoryUsage()
        {
            lock (_lock)
            {
                _memorySnapshots.Add(GC.GetTotalMemory(false));
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                _activeOperations.Clear();
                _operationTimes.Clear();
                _memorySnapshots.Clear();
                _logger.LogInformation("Performance metrics cleared");
            }
        }
    }

    public class PerformanceAnalyzerMetrics
    {
        public Dictionary<string, OperationMetrics> Operations { get; set; } = new();
        public double CurrentMemoryMB { get; set; }
        public double PeakMemoryMB { get; set; }
    }

    public class OperationMetrics
    {
        public int Count { get; set; }
        public double AverageMs { get; set; }
        public double MinMs { get; set; }
        public double MaxMs { get; set; }
        public double TotalMs { get; set; }
    }
}
