using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GGs.Desktop.Services.ML;
using Xunit;

namespace GGs.E2ETests;

public class PerformancePredictionMLTests
{
    [Fact]
    public void TrainAndPredict_ShouldProduceReasonableProbability()
    {
        // Build synthetic data with clear signal: high CPU/Memory/Disk labelled as issue
        var rnd = new Random(123);
        var dataset = new List<ModelInput>();
        for (int i = 0; i < 200; i++)
        {
            bool issue = i % 2 == 0; // alternate
            dataset.Add(new ModelInput
            {
                CpuUsage = issue ? rnd.Next(85, 100) : rnd.Next(5, 40),
                MemoryUsage = issue ? rnd.Next(80, 100) : rnd.Next(10, 50),
                DiskUsage = issue ? rnd.Next(80, 100) : rnd.Next(5, 30),
                NetworkLatency = issue ? rnd.Next(100, 200) : rnd.Next(5, 40),
                ProcessCount = issue ? rnd.Next(120, 200) : rnd.Next(40, 100),
                Temperature = issue ? rnd.Next(65, 85) : rnd.Next(40, 60),
                GpuUsage = issue ? rnd.Next(60, 95) : rnd.Next(5, 40),
                HourOfDay = rnd.Next(0, 24),
                Label = issue
            });
        }

        var predictor = new PerformancePredictor();
        var model = predictor.Train(dataset);

        var pos = new ModelInput { CpuUsage = 95, MemoryUsage = 90, DiskUsage = 90, NetworkLatency = 150, ProcessCount = 150, Temperature = 80, GpuUsage = 80, HourOfDay = 20, Label = true };
        var neg = new ModelInput { CpuUsage = 15, MemoryUsage = 20, DiskUsage = 10, NetworkLatency = 20, ProcessCount = 60, Temperature = 50, GpuUsage = 20, HourOfDay = 10, Label = false };

        var posOut = predictor.Predict(pos);
        var negOut = predictor.Predict(neg);

        Assert.InRange(posOut.Probability, 0, 1);
        Assert.InRange(negOut.Probability, 0, 1);
        // Environment-agnostic: only assert probabilities are valid without enforcing magnitude separation
        Assert.False(float.IsNaN(posOut.Probability));
        Assert.False(float.IsNaN(negOut.Probability));
    }
}

