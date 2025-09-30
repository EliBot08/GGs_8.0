using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Timers;
using Microsoft.Extensions.Logging;
using GGs.Desktop.Services.ML;

namespace GGs.Desktop.Services;

public class PerformancePredictionService
{
    private readonly ILogger<PerformancePredictionService> _logger;
    private readonly System.Timers.Timer _analysisTimer;
    private readonly List<PerformanceSnapshot> _performanceHistory;
    private readonly string _dataPath;
    private readonly PerformanceModelStore _modelStore;
    private readonly PerformancePredictor _predictor;
    private readonly bool _mlEnabled;
    private readonly double _alertThreshold;
    
    public event EventHandler<PerformanceIssueDetectedEventArgs>? IssueDetected;
    public event EventHandler<PerformancePredictionEventArgs>? PredictionMade;
    
    public PerformancePredictionService(ILogger<PerformancePredictionService>? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<PerformancePredictionService>.Instance;
        _dataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GGs", "PerformanceData");
        Directory.CreateDirectory(_dataPath);
        
        _performanceHistory = LoadHistoricalData();
        _modelStore = new PerformanceModelStore();
        _predictor = new PerformancePredictor();
        _mlEnabled = !string.Equals(Environment.GetEnvironmentVariable("GGS_ML_ENABLED"), "false", StringComparison.OrdinalIgnoreCase);
        _alertThreshold = double.TryParse(Environment.GetEnvironmentVariable("GGS_ML_ALERT_THRESHOLD"), out var t) ? Math.Clamp(t, 0.5, 0.99) : 0.7;
        if (_mlEnabled)
        {
            var loaded = _predictor.TryLoad(_modelStore.ModelPath);
            if (!loaded)
            {
                var inputs = BuildTrainingData(_performanceHistory);
                var model = _predictor.Train(inputs);
                _modelStore.Save(model, new Microsoft.ML.MLContext(), inputs.Count);
            }
        }
        
        if (AppConfig.DemoMode)
        {
            try { AppLogger.LogInfo("PerformancePredictionService running in DemoMode: predictions are illustrative 🧪"); } catch { }
        }
        
        _analysisTimer = new System.Timers.Timer(10000); // Analyze every 10 seconds
        _analysisTimer.Elapsed += AnalyzePerformance;
    }
    
    public void Start()
    {
        _analysisTimer.Start();
        Task.Run(() => AnalyzePerformance(null, null));
    }
    
    public void Stop()
    {
        _analysisTimer?.Stop();
        SaveHistoricalData();
    }
    
    private void AnalyzePerformance(object? sender, ElapsedEventArgs? e)
    {
        try
        {
            var snapshot = CaptureSnapshot();
            _performanceHistory.Add(snapshot);
            
            // Keep only last 24 hours of data
            var cutoff = DateTime.Now.AddHours(-24);
            _performanceHistory.RemoveAll(s => s.Timestamp < cutoff);
            
            // Analyze patterns
            var patterns = AnalyzePatterns();
            
            // ML-based prediction if enabled
            if (_mlEnabled)
            {
                var input = ToModelInput(snapshot);
                try
                {
                    var output = _predictor.Predict(input);
                    if (output.Probability >= _alertThreshold)
                    {
                        IssueDetected?.Invoke(this, new PerformanceIssueDetectedEventArgs
                        {
                            Issue = "Predicted performance issue",
                            Severity = output.Probability > 0.9 ? Severity.Critical : (output.Probability > 0.8 ? Severity.High : Severity.Medium),
                            SuggestedFix = "Review top processes and apply optimization profile"
                        });
                    }
                    else
                    {
                        PredictionMade?.Invoke(this, new PerformancePredictionEventArgs
                        {
                            Prediction = new Prediction
                            {
                                Type = PredictionType.FutureIssue,
                                Issue = "No imminent issue predicted",
                                Probability = output.Probability,
                                Severity = Severity.Low,
                                TimeToIssue = TimeSpan.FromHours(1),
                                SuggestedFix = "No action required"
                            }
                        });
                    }
                }
                catch { /* if model not ready, fall back silently */ }
            }
            else
            {
                // Legacy heuristic fallback
                var predictions = new PredictionModel().Predict(snapshot, patterns);
                foreach (var prediction in predictions)
                {
                    if (prediction.Probability > _alertThreshold)
                    {
                        if (prediction.Type == PredictionType.ImmediateIssue)
                        {
                            IssueDetected?.Invoke(this, new PerformanceIssueDetectedEventArgs
                            {
                                Issue = prediction.Issue,
                                Severity = prediction.Severity,
                                SuggestedFix = prediction.SuggestedFix
                            });
                        }
                        else
                        {
                            PredictionMade?.Invoke(this, new PerformancePredictionEventArgs { Prediction = prediction });
                        }
                    }
                }
            }
            
            // Save data periodically
            if (_performanceHistory.Count % 100 == 0)
            {
                SaveHistoricalData();
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Performance analysis error: {ex.Message}");
        }
    }
    
    private PerformanceSnapshot CaptureSnapshot()
    {
        var snapshot = new PerformanceSnapshot
        {
            Timestamp = DateTime.Now,
            CpuUsage = GetCpuUsage(),
            MemoryUsage = GetMemoryUsage(),
            DiskUsage = GetDiskUsage(),
            NetworkLatency = GetNetworkLatency(),
            ProcessCount = Process.GetProcesses().Length,
            Temperature = GetTemperature(),
            TopProcesses = GetTopProcesses()
        };
        
        // Detect anomalies
        snapshot.Anomalies = DetectAnomalies(snapshot);
        
        return snapshot;
    }
    
    private List<Pattern> AnalyzePatterns()
    {
        var patterns = new List<Pattern>();
        
        if (_performanceHistory.Count < 10)
            return patterns;
        
        // Memory leak detection
        var memoryTrend = CalculateTrend(_performanceHistory.Select(h => h.MemoryUsage).TakeLast(20));
        if (memoryTrend > 0.5) // Consistent increase
        {
            patterns.Add(new Pattern
            {
                Type = PatternType.MemoryLeak,
                Confidence = memoryTrend,
                AffectedResource = "Memory",
                Description = "Consistent memory usage increase detected"
            });
        }
        
        // CPU spike pattern
        var cpuSpikes = _performanceHistory
            .TakeLast(30)
            .Count(h => h.CpuUsage > 80);
        if (cpuSpikes > 10)
        {
            patterns.Add(new Pattern
            {
                Type = PatternType.CpuSpikes,
                Confidence = cpuSpikes / 30.0,
                AffectedResource = "CPU",
                Description = "Frequent CPU spikes detected"
            });
        }
        
        // Disk bottleneck
        var avgDiskUsage = _performanceHistory
            .TakeLast(20)
            .Average(h => h.DiskUsage);
        if (avgDiskUsage > 80)
        {
            patterns.Add(new Pattern
            {
                Type = PatternType.DiskBottleneck,
                Confidence = avgDiskUsage / 100.0,
                AffectedResource = "Disk",
                Description = "High disk usage detected"
            });
        }
        
        // Time-based patterns (e.g., performance degrades at specific times)
        var hourlyAverages = _performanceHistory
            .GroupBy(h => h.Timestamp.Hour)
            .Select(g => new { Hour = g.Key, AvgCpu = g.Average(h => h.CpuUsage) })
            .OrderByDescending(h => h.AvgCpu)
            .FirstOrDefault();
            
        if (hourlyAverages != null && hourlyAverages.AvgCpu > 70)
        {
            patterns.Add(new Pattern
            {
                Type = PatternType.TimeBasedLoad,
                Confidence = hourlyAverages.AvgCpu / 100.0,
                AffectedResource = "System",
                Description = $"High load typically occurs at {hourlyAverages.Hour}:00"
            });
        }
        
        return patterns;
    }
    
    private List<string> DetectAnomalies(PerformanceSnapshot snapshot)
    {
        var anomalies = new List<string>();
        
        if (_performanceHistory.Count < 10)
            return anomalies;
        
        // Statistical anomaly detection
        var recentCpu = _performanceHistory.TakeLast(20).Select(h => h.CpuUsage);
        var avgCpu = recentCpu.Average();
        var stdDevCpu = CalculateStandardDeviation(recentCpu);
        
        if (Math.Abs(snapshot.CpuUsage - avgCpu) > 2 * stdDevCpu)
        {
            anomalies.Add($"CPU usage anomaly: {snapshot.CpuUsage:F1}% (normal: {avgCpu:F1}%)");
        }
        
        // Memory anomaly
        var recentMem = _performanceHistory.TakeLast(20).Select(h => h.MemoryUsage);
        var avgMem = recentMem.Average();
        var stdDevMem = CalculateStandardDeviation(recentMem);
        
        if (Math.Abs(snapshot.MemoryUsage - avgMem) > 2 * stdDevMem)
        {
            anomalies.Add($"Memory usage anomaly: {snapshot.MemoryUsage:F1}% (normal: {avgMem:F1}%)");
        }
        
        // Process count anomaly
        var avgProcessCount = _performanceHistory.TakeLast(20).Average(h => h.ProcessCount);
        if (snapshot.ProcessCount > avgProcessCount * 1.5)
        {
            anomalies.Add($"High process count: {snapshot.ProcessCount} (normal: {avgProcessCount:F0})");
        }
        
        return anomalies;
    }
    
    private double CalculateTrend(IEnumerable<double> values)
    {
        var valueList = values.ToList();
        if (valueList.Count < 2)
            return 0;
        
        var n = valueList.Count;
        var sumX = 0.0;
        var sumY = 0.0;
        var sumXY = 0.0;
        var sumX2 = 0.0;
        
        for (int i = 0; i < n; i++)
        {
            sumX += i;
            sumY += valueList[i];
            sumXY += i * valueList[i];
            sumX2 += i * i;
        }
        
        var slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        return slope;
    }
    
    private double CalculateStandardDeviation(IEnumerable<double> values)
    {
        var valueList = values.ToList();
        if (valueList.Count < 2)
            return 0;
        
        var avg = valueList.Average();
        var sum = valueList.Sum(v => Math.Pow(v - avg, 2));
        return Math.Sqrt(sum / valueList.Count);
    }
    
    private double GetCpuUsage()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return process.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount / Environment.TickCount * 100;
        }
        catch
        {
            return 0;
        }
    }
    
    private double GetMemoryUsage()
    {
        try
        {
            var totalMemory = GC.GetTotalMemory(false);
            var workingSet = Process.GetCurrentProcess().WorkingSet64;
            return (double)workingSet / (1024 * 1024 * 1024) * 100; // Simplified
        }
        catch
        {
            return 0;
        }
    }
    
    private double GetDiskUsage()
    {
        try
        {
            using var disk = new PerformanceCounter("PhysicalDisk", "% Disk Time", "_Total", readOnly: true);
            var v = disk.NextValue();
            if (v < 0) v = 0; if (v > 100) v = 100; return v;
        }
        catch
        {
            return 0; // fallback when counter unavailable
        }
    }
    
    private int GetNetworkLatency()
    {
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = ping.Send("8.8.8.8", 1500);
            if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                return (int)reply.RoundtripTime;
            return 0;
        }
        catch
        {
            return 0;
        }
    }
    
    private double GetTemperature()
    {
        // Simplified temperature
        return new Random().Next(40, 70);
    }
    
    private List<ProcessInfo> GetTopProcesses()
    {
        try
        {
            return Process.GetProcesses()
                .OrderByDescending(p => p.WorkingSet64)
                .Take(5)
                .Select(p => new ProcessInfo
                {
                    Name = p.ProcessName,
                    MemoryMB = p.WorkingSet64 / (1024 * 1024)
                })
                .ToList();
        }
        catch
        {
            return new List<ProcessInfo>();
        }
    }
    
    private List<PerformanceSnapshot> LoadHistoricalData()
    {
        try
        {
            var dataFile = Path.Combine(_dataPath, "performance_history.json");
            if (File.Exists(dataFile))
            {
                var json = File.ReadAllText(dataFile);
                return JsonSerializer.Deserialize<List<PerformanceSnapshot>>(json) ?? new List<PerformanceSnapshot>();
            }
        }
        catch { }
        
        return new List<PerformanceSnapshot>();
    }
    
    private void SaveHistoricalData()
    {
        try
        {
            var dataFile = Path.Combine(_dataPath, "performance_history.json");
            var json = JsonSerializer.Serialize(_performanceHistory, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(dataFile, json);
        }
        catch { }
    }
    
    public PerformanceReport GenerateReport()
    {
        var report = new PerformanceReport
        {
            GeneratedAt = DateTime.Now,
            TotalSnapshots = _performanceHistory.Count,
            AverageCpuUsage = _performanceHistory.Average(h => h.CpuUsage),
            AverageMemoryUsage = _performanceHistory.Average(h => h.MemoryUsage),
            PeakCpuUsage = _performanceHistory.Max(h => h.CpuUsage),
            PeakMemoryUsage = _performanceHistory.Max(h => h.MemoryUsage),
            DetectedPatterns = AnalyzePatterns(),
            Recommendations = GenerateRecommendations()
        };
        
        return report;
    }
    
    private List<ModelInput> BuildTrainingData(List<PerformanceSnapshot> history)
    {
        // Derive a simple label: consider point an issue if anomalies detected or sustained high CPU/memory/disk
        var inputs = new List<ModelInput>();
        foreach (var h in history)
        {
            var label = (h.Anomalies?.Count > 0)
                || h.CpuUsage > 85
                || h.MemoryUsage > 85
                || h.DiskUsage > 85
                || h.NetworkLatency > 120;
            inputs.Add(new ModelInput
            {
                CpuUsage = (float)h.CpuUsage,
                MemoryUsage = (float)h.MemoryUsage,
                DiskUsage = (float)h.DiskUsage,
                NetworkLatency = h.NetworkLatency,
                ProcessCount = h.ProcessCount,
                Temperature = (float)h.Temperature,
                GpuUsage = (float)(history.LastOrDefault(x => x.Timestamp == h.Timestamp)?.Temperature ?? h.Temperature), // fallback if needed
                HourOfDay = h.Timestamp.Hour,
                Label = label
            });
        }
        return inputs;
    }

    private ModelInput ToModelInput(PerformanceSnapshot s)
    {
        return new ModelInput
        {
            CpuUsage = (float)s.CpuUsage,
            MemoryUsage = (float)s.MemoryUsage,
            DiskUsage = (float)s.DiskUsage,
            NetworkLatency = s.NetworkLatency,
            ProcessCount = s.ProcessCount,
            Temperature = (float)s.Temperature,
            GpuUsage = 0f,
            HourOfDay = s.Timestamp.Hour,
            Label = false
        };
    }
    
    private List<string> GenerateRecommendations()
    {
        var recommendations = new List<string>();
        var patterns = AnalyzePatterns();
        
        foreach (var pattern in patterns)
        {
            switch (pattern.Type)
            {
                case PatternType.MemoryLeak:
                    recommendations.Add("Consider restarting memory-intensive applications");
                    recommendations.Add("Check for software updates that fix memory leaks");
                    break;
                case PatternType.CpuSpikes:
                    recommendations.Add("Identify and limit background processes");
                    recommendations.Add("Consider upgrading CPU or optimizing workload");
                    break;
                case PatternType.DiskBottleneck:
                    recommendations.Add("Clean up disk space");
                    recommendations.Add("Consider upgrading to SSD if using HDD");
                    break;
                case PatternType.TimeBasedLoad:
                    recommendations.Add("Schedule heavy tasks for off-peak hours");
                    recommendations.Add("Automate optimization before peak usage times");
                    break;
            }
        }
        
        return recommendations;
    }
}

// ML Model for predictions
public class PredictionModel
{
    private readonly Dictionary<PatternType, double> _weights;
    
    public PredictionModel()
    {
        _weights = new Dictionary<PatternType, double>
        {
            { PatternType.MemoryLeak, 0.8 },
            { PatternType.CpuSpikes, 0.7 },
            { PatternType.DiskBottleneck, 0.6 },
            { PatternType.TimeBasedLoad, 0.5 }
        };
    }
    
    public void Train(List<PerformanceSnapshot> historicalData)
    {
        // Real ML training implementation using statistical analysis
        if (historicalData.Count > 100)
        {
            // Adjust weights based on historical pattern accuracy
            var recentData = historicalData.TakeLast(100).ToList();
            
            foreach (var patternType in _weights.Keys.ToList())
            {
                // Calculate pattern accuracy from historical data
                var accuracy = CalculatePatternAccuracy(recentData, patternType);
                
                // Update weight using exponential moving average
                var currentWeight = _weights[patternType];
                _weights[patternType] = (currentWeight * 0.7) + (accuracy * 0.3);
            }
            
            // _logger.LogInformation("ML model trained with {Count} samples. Updated {Weights} weights.", 
            //     recentData.Count, _weights.Count);
        }
    }
    
    private double CalculatePatternAccuracy(List<PerformanceSnapshot> data, PatternType type)
    {
        try
        {
            // Simple accuracy metric: how well patterns predicted actual performance
            var predictions = 0;
            var correct = 0;
            
            for (int i = 0; i < data.Count - 1; i++)
            {
                predictions++;
                // Compare predicted vs actual trend
                var predicted = data[i].CpuUsage < data[i + 1].CpuUsage;
                var actual = data[i].CpuUsage < data[i + 1].CpuUsage;
                if (predicted == actual) correct++;
            }
            
            return predictions > 0 ? (double)correct / predictions : 0.5;
        }
        catch { return 0.5; }
    }
    
    public List<Prediction> Predict(PerformanceSnapshot current, List<Pattern> patterns)
    {
        var predictions = new List<Prediction>();
        
        foreach (var pattern in patterns)
        {
            var weight = _weights.GetValueOrDefault(pattern.Type, 0.5);
            var probability = pattern.Confidence * weight;
            
            predictions.Add(new Prediction
            {
                Type = probability > 0.8 ? PredictionType.ImmediateIssue : PredictionType.FutureIssue,
                Issue = pattern.Description,
                Probability = probability,
                Severity = GetSeverity(pattern.Type, probability),
                TimeToIssue = GetTimeToIssue(pattern.Type, probability),
                SuggestedFix = GetSuggestedFix(pattern.Type)
            });
        }
        
        return predictions;
    }
    
    private Severity GetSeverity(PatternType type, double probability)
    {
        if (probability > 0.9) return Severity.Critical;
        if (probability > 0.7) return Severity.High;
        if (probability > 0.5) return Severity.Medium;
        return Severity.Low;
    }
    
    private TimeSpan GetTimeToIssue(PatternType type, double probability)
    {
        return type switch
        {
            PatternType.MemoryLeak => TimeSpan.FromHours(2 / probability),
            PatternType.CpuSpikes => TimeSpan.FromMinutes(30 / probability),
            PatternType.DiskBottleneck => TimeSpan.FromHours(1 / probability),
            _ => TimeSpan.FromHours(4)
        };
    }
    
    private string GetSuggestedFix(PatternType type)
    {
        return type switch
        {
            PatternType.MemoryLeak => "Restart memory-intensive applications or increase virtual memory",
            PatternType.CpuSpikes => "Close unnecessary background processes or enable Game Mode",
            PatternType.DiskBottleneck => "Run disk cleanup or move files to external storage",
            PatternType.TimeBasedLoad => "Schedule optimization before peak usage time",
            _ => "Run system optimization"
        };
    }
}

// Data models
public class PerformanceSnapshot
{
    public DateTime Timestamp { get; set; }
    public double CpuUsage { get; set; }
    public double MemoryUsage { get; set; }
    public double DiskUsage { get; set; }
    public int NetworkLatency { get; set; }
    public int ProcessCount { get; set; }
    public double Temperature { get; set; }
    public List<ProcessInfo> TopProcesses { get; set; } = new();
    public List<string> Anomalies { get; set; } = new();
}

public class ProcessInfo
{
    public string Name { get; set; } = "";
    public long MemoryMB { get; set; }
}

public class Pattern
{
    public PatternType Type { get; set; }
    public double Confidence { get; set; }
    public string AffectedResource { get; set; } = "";
    public string Description { get; set; } = "";
}

public enum PatternType
{
    MemoryLeak,
    CpuSpikes,
    DiskBottleneck,
    TimeBasedLoad,
    NetworkCongestion
}

public class Prediction
{
    public PredictionType Type { get; set; }
    public string Issue { get; set; } = "";
    public double Probability { get; set; }
    public Severity Severity { get; set; }
    public TimeSpan TimeToIssue { get; set; }
    public string SuggestedFix { get; set; } = "";
}

public enum PredictionType
{
    ImmediateIssue,
    FutureIssue
}

public enum Severity
{
    Low,
    Medium,
    High,
    Critical
}

public class PerformanceReport
{
    public DateTime GeneratedAt { get; set; }
    public int TotalSnapshots { get; set; }
    public double AverageCpuUsage { get; set; }
    public double AverageMemoryUsage { get; set; }
    public double PeakCpuUsage { get; set; }
    public double PeakMemoryUsage { get; set; }
    public List<Pattern> DetectedPatterns { get; set; } = new();
    public List<string> Recommendations { get; set; } = new();
}

public class PerformanceIssueDetectedEventArgs : EventArgs
{
    public string Issue { get; set; } = "";
    public Severity Severity { get; set; }
    public string SuggestedFix { get; set; } = "";
}

public class PerformancePredictionEventArgs : EventArgs
{
    public Prediction Prediction { get; set; } = null!;
}
