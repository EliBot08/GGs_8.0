using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Net.Sockets;

namespace GGs.Desktop.Services;

public interface IErrorHandlingService
{
    Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3, TimeSpan? delay = null);
    Task ExecuteWithRetryAsync(Func<Task> operation, int maxRetries = 3, TimeSpan? delay = null);
    void HandleException(Exception exception, string context = "", bool showToUser = true);
    Task<bool> HandleHttpErrorAsync(HttpResponseMessage response, string context = "");
    void ShowUserFriendlyError(string message, string title = "Error");
    void LogAndReport(Exception exception, string context, Dictionary<string, object>? additionalData = null);
}

public sealed class ErrorHandlingService : IErrorHandlingService
{
    private readonly ILogger<ErrorHandlingService> _logger;
    private readonly NotificationService _notificationService;
    private readonly TelemetryService _telemetryService;

    public ErrorHandlingService(
        ILogger<ErrorHandlingService> logger,
        NotificationService notificationService,
        TelemetryService telemetryService)
    {
        _logger = logger;
        _notificationService = notificationService;
        _telemetryService = telemetryService;
    }

    public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3, TimeSpan? delay = null)
    {
        var retryDelay = delay ?? TimeSpan.FromSeconds(1);
        Exception lastException = null;

        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsRetryableException(ex) && attempt < maxRetries)
            {
                lastException = ex;
                _logger.LogWarning(ex, "Operation failed on attempt {Attempt}/{MaxRetries}. Retrying in {Delay}ms...", 
                    attempt + 1, maxRetries + 1, retryDelay.TotalMilliseconds);

                await Task.Delay(retryDelay);
                retryDelay = TimeSpan.FromMilliseconds(retryDelay.TotalMilliseconds * 1.5); // Exponential backoff
            }
        }

        if (lastException != null)
        {
            _logger.LogError(lastException, "Operation failed after {MaxRetries} retries", maxRetries);
            throw lastException;
        }

        throw new InvalidOperationException("Unexpected retry logic failure");
    }

    public async Task ExecuteWithRetryAsync(Func<Task> operation, int maxRetries = 3, TimeSpan? delay = null)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await operation();
            return true;
        }, maxRetries, delay);
    }

    public void HandleException(Exception exception, string context = "", bool showToUser = true)
    {
        _logger.LogError(exception, "Exception in context: {Context}", context);
        
        // Report to telemetry
        LogAndReport(exception, context);

        if (showToUser)
        {
            var userMessage = GetUserFriendlyMessage(exception);
            ShowUserFriendlyError(userMessage);
        }
    }

    public async Task<bool> HandleHttpErrorAsync(HttpResponseMessage response, string context = "")
    {
        if (response.IsSuccessStatusCode)
            return true;

        var statusCode = response.StatusCode;
        var content = string.Empty;

        try
        {
            content = await response.Content.ReadAsStringAsync();
        }
        catch
        {
            // Ignore content read errors
        }

        _logger.LogError("HTTP {StatusCode} error in {Context}. Content: {Content}", 
            statusCode, context, content);

        var userMessage = statusCode switch
        {
            HttpStatusCode.Unauthorized => "Your session has expired. Please log in again.",
            HttpStatusCode.Forbidden => "You don't have permission to perform this action.",
            HttpStatusCode.NotFound => "The requested resource was not found.",
            HttpStatusCode.TooManyRequests => "Too many requests. Please wait a moment and try again.",
            HttpStatusCode.InternalServerError => "Server error occurred. Our team has been notified.",
            HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout 
                => "Service is temporarily unavailable. Please try again later.",
            _ => $"Network error occurred ({statusCode}). Please check your connection and try again."
        };

        ShowUserFriendlyError(userMessage);

        // Report to telemetry
        LogAndReport(new HttpRequestException($"HTTP {statusCode}"), context, new Dictionary<string, object>
        {
            ["StatusCode"] = statusCode,
            ["Content"] = content,
            ["Url"] = response.RequestMessage?.RequestUri?.ToString() ?? "Unknown"
        });

        return false;
    }

    public void ShowUserFriendlyError(string message, string title = "Error")
    {
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _notificationService.ShowError(message, title);
        });
    }

    public void LogAndReport(Exception exception, string context, Dictionary<string, object>? additionalData = null)
    {
        try
        {
            var telemetryData = new Dictionary<string, object>
            {
                ["Context"] = context,
                ["ExceptionType"] = exception.GetType().Name,
                ["ExceptionMessage"] = exception.Message,
                ["StackTrace"] = exception.StackTrace ?? "",
                ["MachineName"] = Environment.MachineName,
                ["OSVersion"] = Environment.OSVersion.ToString(),
                ["UserDomainName"] = Environment.UserDomainName,
                ["Timestamp"] = DateTime.UtcNow
            };

            if (additionalData != null)
            {
                foreach (var kvp in additionalData)
                {
                    telemetryData[kvp.Key] = kvp.Value;
                }
            }

            _telemetryService.TrackException(exception, telemetryData);
        }
        catch (Exception telemetryEx)
        {
            _logger.LogError(telemetryEx, "Failed to report exception to telemetry");
        }
    }

    private bool IsRetryableException(Exception exception)
    {
        return exception switch
        {
            HttpRequestException => true,
            TaskCanceledException => true,
            TimeoutException => true,
            SocketException => true,
            JsonException => false, // Don't retry JSON parsing errors
            ArgumentException => false, // Don't retry argument errors
            UnauthorizedAccessException => false, // Don't retry auth errors
            _ => false
        };
    }

    private string GetUserFriendlyMessage(Exception exception)
    {
        return exception switch
        {
            HttpRequestException => "Network error occurred. Please check your internet connection and try again.",
            TaskCanceledException => "The operation timed out. Please try again.",
            TimeoutException => "The operation timed out. Please try again.",
            JsonException => "Invalid data received from server. Please try again.",
            UnauthorizedAccessException => "Access denied. Please check your permissions.",
            ArgumentException => "Invalid input provided. Please check your data and try again.",
            NotSupportedException => "This operation is not supported on your system.",
            OutOfMemoryException => "Insufficient memory to complete the operation. Please close other applications and try again.",
            _ => "An unexpected error occurred. Please try again or contact support if the problem persists."
        };
    }
}

// Network connectivity service
public interface INetworkConnectivityService
{
    bool IsConnected { get; }
    event EventHandler<bool> ConnectivityChanged;
    Task<bool> CheckConnectivityAsync();
    Task WaitForConnectivityAsync(CancellationToken cancellationToken = default);
}

public sealed class NetworkConnectivityService : INetworkConnectivityService, IDisposable
{
    private readonly ILogger<NetworkConnectivityService> _logger;
    private readonly Timer _connectivityTimer;
    private bool _isConnected = true;
    private bool _disposed = false;

    public bool IsConnected => _isConnected;
    public event EventHandler<bool>? ConnectivityChanged;

    public NetworkConnectivityService(ILogger<NetworkConnectivityService> logger)
    {
        _logger = logger;
        _connectivityTimer = new Timer(CheckConnectivityCallback, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    public async Task<bool> CheckConnectivityAsync()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var response = await client.GetAsync("https://www.google.com/generate_204");
            var isConnected = response.IsSuccessStatusCode;
            
            if (isConnected != _isConnected)
            {
                _isConnected = isConnected;
                _logger.LogInformation("Network connectivity changed: {IsConnected}", isConnected);
                ConnectivityChanged?.Invoke(this, isConnected);
            }

            return isConnected;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check network connectivity");
            
            if (_isConnected)
            {
                _isConnected = false;
                ConnectivityChanged?.Invoke(this, false);
            }
            
            return false;
        }
    }

    public async Task WaitForConnectivityAsync(CancellationToken cancellationToken = default)
    {
        while (!_isConnected && !cancellationToken.IsCancellationRequested)
        {
            await CheckConnectivityAsync();
            if (!_isConnected)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
        }
    }

    private async void CheckConnectivityCallback(object? state)
    {
        if (_disposed) return;
        
        try
        {
            await CheckConnectivityAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in connectivity check timer");
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        _disposed = true;
        _connectivityTimer?.Dispose();
    }
}

// Telemetry service for error reporting
public interface ITelemetryService
{
    void TrackException(Exception exception, Dictionary<string, object>? properties = null);
    void TrackEvent(string eventName, Dictionary<string, object>? properties = null);
    void TrackMetric(string metricName, double value, Dictionary<string, object>? properties = null);
}

public sealed class TelemetryService : ITelemetryService
{
    private readonly ILogger<TelemetryService> _logger;

    public TelemetryService(ILogger<TelemetryService> logger)
    {
        _logger = logger;
    }

    public void TrackException(Exception exception, Dictionary<string, object>? properties = null)
    {
        try
        {
            // In a real implementation, this would send to Application Insights or similar
            _logger.LogError(exception, "Telemetry Exception: {Properties}", 
                properties != null ? JsonSerializer.Serialize(properties) : "None");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track exception telemetry");
        }
    }

    public void TrackEvent(string eventName, Dictionary<string, object>? properties = null)
    {
        try
        {
            _logger.LogInformation("Telemetry Event: {EventName} Properties: {Properties}", 
                eventName, properties != null ? JsonSerializer.Serialize(properties) : "None");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track event telemetry");
        }
    }

    public void TrackMetric(string metricName, double value, Dictionary<string, object>? properties = null)
    {
        try
        {
            _logger.LogInformation("Telemetry Metric: {MetricName} = {Value} Properties: {Properties}", 
                metricName, value, properties != null ? JsonSerializer.Serialize(properties) : "None");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to track metric telemetry");
        }
    }
}
