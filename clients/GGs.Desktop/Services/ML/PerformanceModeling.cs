using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace GGs.Desktop.Services.ML;

public sealed class ModelInput
{
    [LoadColumn(0)] public float CpuUsage { get; set; }
    [LoadColumn(1)] public float MemoryUsage { get; set; }
    [LoadColumn(2)] public float DiskUsage { get; set; }
    [LoadColumn(3)] public float NetworkLatency { get; set; }
    [LoadColumn(4)] public float ProcessCount { get; set; }
    [LoadColumn(5)] public float Temperature { get; set; }
    [LoadColumn(6)] public float GpuUsage { get; set; }
    [LoadColumn(7)] public float HourOfDay { get; set; }
    [LoadColumn(8)] public bool Label { get; set; }
}

public sealed class ModelOutput
{
    [ColumnName("PredictedLabel")] public bool PredictedLabel { get; set; }
    public float Probability { get; set; }
    public float Score { get; set; }
}

public sealed class PerformanceModelStore
{
    private readonly string _dir;
    private readonly string _modelPath;
    private readonly string _metaPath;

    public PerformanceModelStore(string? baseDir = null)
    {
        baseDir ??= Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _dir = Path.Combine(baseDir, "GGs", "PerformanceModel");
        Directory.CreateDirectory(_dir);
        _modelPath = Path.Combine(_dir, "model_v1.zip");
        _metaPath = Path.Combine(_dir, "model_v1.meta.json");
    }

    public string ModelPath => _modelPath;
    public string MetaPath => _metaPath;
    public bool Exists() => File.Exists(_modelPath);

    public void Save(ITransformer model, MLContext ml, int trainedExamples)
    {
        using var fs = File.Create(_modelPath);
        ml.Model.Save(model, inputSchema: null, stream: fs);
        File.WriteAllText(_metaPath, System.Text.Json.JsonSerializer.Serialize(new
        {
            Version = 1,
            TrainedAtUtc = DateTime.UtcNow,
            TrainedExamples = trainedExamples
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }
}

public sealed class PerformancePredictor
{
    private readonly MLContext _ml = new(seed: 7);
    private ITransformer? _model;

    public bool TryLoad(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            _model = _ml.Model.Load(fs, out _);
            return _model != null;
        }
        catch { return false; }
    }

    public ITransformer Train(IEnumerable<ModelInput> data)
    {
        var list = data.ToList();
        var dv = _ml.Data.LoadFromEnumerable(list);
        var pipeline = _ml.Transforms.Concatenate("Features",
                nameof(ModelInput.CpuUsage), nameof(ModelInput.MemoryUsage), nameof(ModelInput.DiskUsage),
                nameof(ModelInput.NetworkLatency), nameof(ModelInput.ProcessCount), nameof(ModelInput.Temperature),
                nameof(ModelInput.GpuUsage), nameof(ModelInput.HourOfDay))
            .Append(_ml.BinaryClassification.Trainers.FastForest(labelColumnName: nameof(ModelInput.Label), featureColumnName: "Features"));

        _model = pipeline.Fit(dv);
        return _model;
    }

    public ModelOutput Predict(ModelInput input)
    {
        if (_model == null) throw new InvalidOperationException("Model not loaded or trained.");
        var engine = _ml.Model.CreatePredictionEngine<ModelInput, ModelOutput>(_model);
        return engine.Predict(input);
    }
}

