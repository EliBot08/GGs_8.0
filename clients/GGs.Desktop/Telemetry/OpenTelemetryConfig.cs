using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;

namespace GGs.Desktop.Telemetry;

public static class OpenTelemetryConfig
{
    private static TracerProvider? _tracerProvider;
    private static MeterProvider? _meterProvider;
    private static ILoggerFactory? _loggerFactory;

    public static bool Initialize()
    {
        try
        {
            var disabled = Environment.GetEnvironmentVariable("GGS_OTEL_ENABLED");
            if (!string.IsNullOrWhiteSpace(disabled) && disabled.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var endpoint = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT") ?? "http://localhost:4317";
            var protocolEnv = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_PROTOCOL") ?? "grpc"; // grpc | http/protobuf
            var serviceName = "GGs.Desktop";
            var serviceVersion = typeof(OpenTelemetryConfig).Assembly.GetName().Version?.ToString() ?? "0.0.0";
            var environment = Environment.GetEnvironmentVariable("GGS_ENV") ?? "dev";

            var resource = ResourceBuilder.CreateDefault()
                .AddService(serviceName: serviceName, serviceVersion: serviceVersion)
                .AddAttributes(new List<KeyValuePair<string, object>>
                {
                    new("deployment.environment", environment)
                });

            // Merge any extra OTEL resource attributes if provided (simple parser: key=value,key2=value2)
            var resAttr = Environment.GetEnvironmentVariable("OTEL_RESOURCE_ATTRIBUTES");
            if (!string.IsNullOrWhiteSpace(resAttr))
            {
                foreach (var pair in resAttr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var idx = pair.IndexOf('=');
                    if (idx > 0)
                    {
                        var k = pair.Substring(0, idx).Trim();
                        var v = pair.Substring(idx + 1).Trim();
                        resource = resource.AddAttributes(new[] { new KeyValuePair<string, object>(k, v) });
                    }
                }
            }

            _tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resource)
                .AddSource("GGs.Desktop", "GGs.Desktop.Startup", "GGs.Desktop.License", "GGs.Agent.Tweak", "GGs.Desktop.Log")
                .AddHttpClientInstrumentation()
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(endpoint);
                    o.Protocol = protocolEnv.Equals("http/protobuf", StringComparison.OrdinalIgnoreCase)
                        ? OtlpExportProtocol.HttpProtobuf
                        : OtlpExportProtocol.Grpc;
                })
                .Build();

            _meterProvider = Sdk.CreateMeterProviderBuilder()
                .SetResourceBuilder(resource)
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddOtlpExporter(o =>
                {
                    o.Endpoint = new Uri(endpoint);
                    o.Protocol = protocolEnv.Equals("http/protobuf", StringComparison.OrdinalIgnoreCase)
                        ? OtlpExportProtocol.HttpProtobuf
                        : OtlpExportProtocol.Grpc;
                })
                .Build();

            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddOpenTelemetry(o =>
                {
                    o.SetResourceBuilder(resource);
                    o.IncludeScopes = false;
                    o.IncludeFormattedMessage = true;
                    o.ParseStateValues = true;
                    o.AddOtlpExporter(exp =>
                    {
                        exp.Endpoint = new Uri(endpoint);
                        exp.Protocol = protocolEnv.Equals("http/protobuf", StringComparison.OrdinalIgnoreCase)
                            ? OtlpExportProtocol.HttpProtobuf
                            : OtlpExportProtocol.Grpc;
                    });
                });
            });

            TelemetryLogBridge.Initialize(_loggerFactory);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static void Shutdown()
    {
        try { _tracerProvider?.Dispose(); } catch { }
        try { _meterProvider?.Dispose(); } catch { }
        try { _loggerFactory?.Dispose(); } catch { }
    }
}
