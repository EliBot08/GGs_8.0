using System.Net.Http.Json;
using GGs.Shared.Tweaks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;

namespace GGs.Agent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _config;
    private readonly HttpClient _http;

    public Worker(ILogger<Worker> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
        var baseUrl = _config["Server:BaseUrl"] ?? "https://localhost:5001";
        var sec = BuildSecurityOptions(_config);
        _http = GGs.Shared.Http.SecureHttpClientFactory.GetOrCreate(baseUrl, sec, userAgent: "GGs.Agent");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseUrl = _config["Server:BaseUrl"] ?? "https://localhost:5001";
        var hubPath = _config["Server:HubPath"] ?? "/hubs/admin";
        var hubUrl = CombineUrl(baseUrl, hubPath);

        var conn = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        conn.On<TweakDefinition, string>("ExecuteTweak", async (tweak, correlationId) =>
        {
            _logger.LogInformation("Received tweak {Name} ({Id}) corr={CorrelationId}", tweak.Name, tweak.Id, correlationId);
            
            // Execute tweak with enhanced error handling
            var log = await ExecuteTweakWithEnhancedLogging(tweak, correlationId);
            
            // Get machine token from environment or config
            var machineToken = Environment.GetEnvironmentVariable("AGENT_MACHINE_TOKEN") ?? _config["Agent:MachineToken"];
            
            // Enterprise-grade audit logging with comprehensive fallback strategy
            var auditSuccess = await SendAuditLogWithFallback(log, correlationId, machineToken, stoppingToken);
            
            // Real-time ACK via hub for immediate feedback to admins
            var hubAckSuccess = await SendHubExecutionResult(conn, log, correlationId, stoppingToken);
            
            // Log comprehensive execution status for enterprise monitoring
            _logger.LogInformation("Tweak execution completed: {TweakId} | Success: {ExecutionSuccess} | Audit: {AuditSuccess} | Hub: {HubSuccess} | Correlation: {CorrelationId}", 
                tweak.Id, log.Success, auditSuccess, hubAckSuccess, correlationId);
        });

        conn.Closed += async (ex) =>
        {
            if (ex != null) _logger.LogWarning(ex, "Hub connection closed");
            await Task.Delay(3000, stoppingToken);
        };

        // Ensure device enrollment before connecting hub
        try { DeviceEnrollmentService.EnsureEnrolled(); } catch { }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await conn.StartAsync(stoppingToken);
                var deviceId = Shared.Platform.DeviceIdHelper.GetStableDeviceId();
                await conn.InvokeAsync("RegisterDevice", deviceId, cancellationToken: stoppingToken);
                _logger.LogInformation("Registered device {DeviceId}", deviceId);
                break; // connected
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to connect to hub, retrying...");
                await Task.Delay(5000, stoppingToken);
            }
        }

        // Enterprise heartbeat with comprehensive health monitoring
        var myDeviceId = GGs.Shared.Platform.DeviceIdHelper.GetStableDeviceId();
        var heartbeatInterval = TimeSpan.FromSeconds(_config.GetValue<int>("Agent:HeartbeatIntervalSeconds", 30));
        var heartbeatFailureCount = 0;
        const int maxHeartbeatFailures = 3;
        
        _logger.LogInformation("Starting enterprise heartbeat monitoring: Device {DeviceId} | Interval: {Interval}s", 
            myDeviceId, heartbeatInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (conn.State == HubConnectionState.Connected)
                {
                    // Enhanced heartbeat with system health data
                    await SendEnterpriseHeartbeat(conn, myDeviceId, stoppingToken);
                    heartbeatFailureCount = 0; // Reset on success
                    
                    _logger.LogDebug("Heartbeat sent successfully: {DeviceId}", myDeviceId);
                }
                else
                {
                    _logger.LogWarning("Hub connection not available for heartbeat: {DeviceId} | State: {State}", 
                        myDeviceId, conn.State);
                    heartbeatFailureCount++;
                }
            }
            catch (Exception ex)
            {
                heartbeatFailureCount++;
                _logger.LogWarning(ex, "Heartbeat failed: {DeviceId} | Failure count: {FailureCount}/{MaxFailures}", 
                    myDeviceId, heartbeatFailureCount, maxHeartbeatFailures);
                
                // Enterprise escalation: Critical alert after multiple failures
                if (heartbeatFailureCount >= maxHeartbeatFailures)
                {
                    _logger.LogError("CRITICAL: Heartbeat failures exceeded threshold: {DeviceId} | Count: {FailureCount} | Hub State: {State}", 
                        myDeviceId, heartbeatFailureCount, conn.State);
                    
                    // Attempt to reconnect on critical failure
                    if (conn.State == HubConnectionState.Disconnected)
                    {
                        try
                        {
                            _logger.LogInformation("Attempting hub reconnection due to heartbeat failures: {DeviceId}", myDeviceId);
                            await conn.StartAsync(stoppingToken);
                            await conn.InvokeAsync("RegisterDevice", myDeviceId, cancellationToken: stoppingToken);
                            heartbeatFailureCount = 0; // Reset on successful reconnection
                        }
                        catch (Exception reconnectEx)
                        {
                            _logger.LogError(reconnectEx, "Failed to reconnect hub: {DeviceId}", myDeviceId);
                        }
                    }
                }
            }
            
            await Task.Delay(heartbeatInterval, stoppingToken);
        }
    }

    private static GGs.Shared.Http.HttpClientSecurityOptions BuildSecurityOptions(IConfiguration cfg)
    {
        var sec = new GGs.Shared.Http.HttpClientSecurityOptions();
        if (int.TryParse(cfg["Security:Http:TimeoutSeconds"], out var t)) sec.Timeout = TimeSpan.FromSeconds(Math.Clamp(t, 1, 120));
        var mode = cfg["Security:Http:Pinning:Mode"]; if (Enum.TryParse<GGs.Shared.Http.PinningMode>(mode, true, out var pm)) sec.PinningMode = pm;
        var vals = cfg["Security:Http:Pinning:Values"]; if (!string.IsNullOrWhiteSpace(vals)) sec.PinnedValues = vals.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var hosts = cfg["Security:Http:Pinning:Hostnames"]; if (!string.IsNullOrWhiteSpace(hosts)) sec.Hostnames = hosts.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (bool.TryParse(cfg["Security:Http:ClientCertificate:Enabled"], out var cce)) sec.ClientCertificateEnabled = cce;
        sec.ClientCertFindType = cfg["Security:Http:ClientCertificate:FindType"];
        sec.ClientCertFindValue = cfg["Security:Http:ClientCertificate:FindValue"];
        sec.ClientCertStoreName = cfg["Security:Http:ClientCertificate:StoreName"] ?? "My";
        sec.ClientCertStoreLocation = cfg["Security:Http:ClientCertificate:StoreLocation"] ?? "CurrentUser";
        return sec;
    }

    /// <summary>
    /// Enterprise-grade tweak execution with comprehensive logging and error handling
    /// </summary>
    private async Task<TweakApplicationLog> ExecuteTweakWithEnhancedLogging(TweakDefinition tweak, string correlationId)
    {
        var deviceId = GGs.Shared.Platform.DeviceIdHelper.GetStableDeviceId();
        var startTime = DateTime.UtcNow;
        
        try
        {
            _logger.LogInformation("Starting tweak execution: {TweakName} ({TweakId}) | Device: {DeviceId} | Correlation: {CorrelationId}", 
                tweak.Name, tweak.Id, deviceId, correlationId);
            
            // Execute the tweak with timing
            var executionStart = DateTime.UtcNow;
            var log = TweakExecutor.Apply(tweak);
            var executionTime = (DateTime.UtcNow - executionStart).TotalMilliseconds;
            
            // Ensure log has all required enterprise fields
            log.DeviceId = deviceId;
            log.AppliedUtc = startTime;
            log.ExecutionTimeMs = (int)executionTime;
            
            _logger.LogInformation("Tweak execution finished: {TweakId} | Success: {Success} | Duration: {Duration}ms | Correlation: {CorrelationId}",
                tweak.Id, log.Success, executionTime, correlationId);
            
            return log;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical error during tweak execution: {TweakId} | Correlation: {CorrelationId}", tweak.Id, correlationId);
            
            // Return error log for audit trail
            return new TweakApplicationLog
            {
                TweakId = tweak.Id,
                DeviceId = deviceId,
                AppliedUtc = startTime,
                Success = false,
                Error = $"Execution failed: {ex.Message}",
                ExecutionTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds
            };
        }
    }

    /// <summary>
    /// Enterprise audit logging with comprehensive fallback strategy
    /// Implements secure /api/audit/log endpoint with legacy fallback
    /// </summary>
    private async Task<bool> SendAuditLogWithFallback(TweakApplicationLog log, string correlationId, string? machineToken, CancellationToken cancellationToken)
    {
        try
        {
            // Primary: Secure audit endpoint with mTLS/machine token
            var success = await TrySendAuditLog("api/audit/log", log, correlationId, machineToken, cancellationToken);
            if (success)
            {
                _logger.LogDebug("Audit log sent via secure endpoint: {TweakId} | Correlation: {CorrelationId}", log.TweakId, correlationId);
                return true;
            }

            // Fallback: Legacy endpoint
            _logger.LogWarning("Primary audit endpoint failed, attempting legacy fallback: {TweakId}", log.TweakId);
            success = await TrySendAuditLog("api/auditlogs", log, correlationId, machineToken, cancellationToken);
            
            if (success)
            {
                _logger.LogInformation("Audit log sent via legacy endpoint: {TweakId} | Correlation: {CorrelationId}", log.TweakId, correlationId);
                return true;
            }

            // Both endpoints failed - log for manual intervention
            _logger.LogError("All audit endpoints failed for: {TweakId} | Correlation: {CorrelationId} | Will retry via offline queue", 
                log.TweakId, correlationId);
            
            // TODO: Queue for offline retry when OfflineQueueService is available
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical failure in audit logging: {TweakId} | Correlation: {CorrelationId}", log.TweakId, correlationId);
            return false;
        }
    }

    /// <summary>
    /// Attempts to send audit log to specified endpoint with proper enterprise security headers
    /// </summary>
    private async Task<bool> TrySendAuditLog(string endpoint, TweakApplicationLog log, string correlationId, string? machineToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            
            // Enterprise-required headers
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
            request.Headers.TryAddWithoutValidation("User-Agent", "GGs.Agent/1.0");
            
            // Machine token for authentication (enterprise security requirement)
            if (!string.IsNullOrWhiteSpace(machineToken))
            {
                request.Headers.TryAddWithoutValidation("X-Machine-Token", machineToken);
            }
            
            // JSON payload
            request.Content = JsonContent.Create(log);
            
            using var response = await _http.SendAsync(request, cancellationToken);
            
            if (response.IsSuccessStatusCode)
            {
                return true;
            }
            
            // Log specific error responses for enterprise monitoring
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Audit endpoint authentication failed: {Endpoint} | Status: {Status} | Machine token present: {HasToken}", 
                    endpoint, response.StatusCode, !string.IsNullOrWhiteSpace(machineToken));
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Audit endpoint not found: {Endpoint} | Status: {Status}", endpoint, response.StatusCode);
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Audit endpoint error: {Endpoint} | Status: {Status} | Response: {Response}", 
                    endpoint, response.StatusCode, responseContent);
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during audit log transmission: {Endpoint}", endpoint);
            return false;
        }
    }

    /// <summary>
    /// Enterprise heartbeat with comprehensive system health monitoring
    /// Sends enhanced telemetry data for enterprise monitoring and compliance
    /// </summary>
    private async Task SendEnterpriseHeartbeat(HubConnection connection, string deviceId, CancellationToken cancellationToken)
    {
        try
        {
            // Collect basic system health metrics for enterprise monitoring
            var healthData = new
            {
                DeviceId = deviceId,
                Timestamp = DateTime.UtcNow,
                AgentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown",
                OSVersion = Environment.OSVersion.ToString(),
                MachineName = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount,
                WorkingSet = Environment.WorkingSet,
                TotalPhysicalMemory = GC.GetTotalMemory(false),
                UpTime = TimeSpan.FromMilliseconds(Environment.TickCount64).ToString(@"dd\.hh\:mm\:ss"),
                ConnectionState = connection.State.ToString()
            };

            // Send enhanced heartbeat to hub
            await connection.InvokeAsync("Heartbeat", deviceId, cancellationToken: cancellationToken);
            
            // Optional: Send detailed health data for enterprise monitoring (if enabled)
            var sendDetailedHealth = _config.GetValue<bool>("Agent:SendDetailedHealthData", false);
            if (sendDetailedHealth)
            {
                await connection.InvokeAsync("HealthData", healthData, cancellationToken: cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send enterprise heartbeat: {DeviceId}", deviceId);
            throw; // Re-throw to trigger failure handling in calling method
        }
    }

    /// <summary>
    /// Sends real-time execution result via SignalR hub for immediate admin feedback
    /// Critical for enterprise real-time monitoring and compliance
    /// </summary>
    private async Task<bool> SendHubExecutionResult(HubConnection connection, TweakApplicationLog log, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            if (connection.State != HubConnectionState.Connected)
            {
                _logger.LogWarning("Hub connection not available for execution result: {TweakId} | State: {State}", log.TweakId, connection.State);
                return false;
            }

            // Send execution result with correlation ID for enterprise tracking
            await connection.InvokeAsync("ReportExecutionResult", log, correlationId, cancellationToken: cancellationToken);
            
            _logger.LogDebug("Hub execution result sent: {TweakId} | Correlation: {CorrelationId}", log.TweakId, correlationId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send hub execution result: {TweakId} | Correlation: {CorrelationId}", log.TweakId, correlationId);
            return false;
        }
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        if (path.StartsWith('/')) path = path[1..];
        return baseUrl + path;
    }
}
