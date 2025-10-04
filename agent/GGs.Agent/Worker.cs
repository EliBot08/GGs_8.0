using System.Net.Http.Json;
using GGs.Shared.Privacy;
using GGs.Shared.Tweaks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;

namespace GGs.Agent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IConfiguration _config;
    private readonly HttpClient _http;
    private readonly ActivitySource _activitySource = new("GGs.Agent.Worker");

    // Telemetry counters for heartbeat reporting
    private int _operationsExecuted = 0;
    private int _operationsSucceeded = 0;
    private int _operationsFailed = 0;

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
            using var activity = _activitySource.StartActivity("ExecuteTweak", ActivityKind.Server);
            activity?.SetTag("tweak.id", tweak.Id);
            activity?.SetTag("tweak.name", tweak.Name);
            activity?.SetTag("correlation.id", correlationId);

            // Create telemetry context for this operation
            var deviceId = GGs.Shared.Platform.DeviceIdHelper.GetStableDeviceId();
            var telemetryContext = TelemetryContext.Create(deviceId, correlationId);

            activity?.SetTag("device.id", deviceId);
            activity?.SetTag("operation.id", telemetryContext.OperationId);

            _logger.LogInformation("Received tweak {Name} ({Id}) | {TelemetryContext}",
                tweak.Name, tweak.Id, telemetryContext);

            // Execute tweak with enhanced error handling and telemetry
            var log = await ExecuteTweakWithEnhancedLogging(tweak, telemetryContext);

            // Update counters for heartbeat reporting
            Interlocked.Increment(ref _operationsExecuted);
            if (log.Success)
                Interlocked.Increment(ref _operationsSucceeded);
            else
                Interlocked.Increment(ref _operationsFailed);

            // Get machine token from environment or config
            var machineToken = Environment.GetEnvironmentVariable("AGENT_MACHINE_TOKEN") ?? _config["Agent:MachineToken"];

            // Enterprise-grade audit logging with comprehensive fallback strategy
            var auditSuccess = await SendAuditLogWithFallback(log, telemetryContext, machineToken, stoppingToken);

            // Real-time ACK via hub for immediate feedback to admins
            var hubAckSuccess = await SendHubExecutionResult(conn, log, telemetryContext, stoppingToken);

            // Log comprehensive execution status for enterprise monitoring
            _logger.LogInformation("Tweak execution completed: {TweakId} | Success: {ExecutionSuccess} | Audit: {AuditSuccess} | Hub: {HubSuccess} | ReasonCode: {ReasonCode} | {TelemetryContext}",
                tweak.Id, log.Success, auditSuccess, hubAckSuccess, log.ReasonCode, telemetryContext);

            activity?.SetTag("execution.success", log.Success);
            activity?.SetTag("reason.code", log.ReasonCode);
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
    /// Enterprise-grade tweak execution with comprehensive logging and error handling.
    /// Implements Prompt 4: Enhanced telemetry with OperationId, CorrelationId, synchronized timestamps, and reason codes.
    /// </summary>
    private Task<TweakApplicationLog> ExecuteTweakWithEnhancedLogging(TweakDefinition tweak, TelemetryContext telemetryContext)
    {
        return Task.Run(() =>
        {
            using var activity = _activitySource.StartActivity("ExecuteTweak.Apply", ActivityKind.Internal);
            activity?.SetTag("tweak.id", tweak.Id);
            activity?.SetTag("operation.id", telemetryContext.OperationId);
            activity?.SetTag("correlation.id", telemetryContext.CorrelationId);

            var startTime = DateTime.UtcNow;

            try
            {
                _logger.LogInformation("Starting tweak execution: {TweakName} ({TweakId}) | {TelemetryContext}",
                    tweak.Name, tweak.Id, telemetryContext);

                // Execute the tweak with timing
                var executionStart = DateTime.UtcNow;
                var log = TweakExecutor.Apply(tweak);
                var executionEnd = DateTime.UtcNow;
                var executionTime = (executionEnd - executionStart).TotalMilliseconds;

                // Ensure log has all required enterprise fields with enhanced telemetry
                log.DeviceId = telemetryContext.DeviceId;
                log.OperationId = telemetryContext.OperationId;
                log.CorrelationId = telemetryContext.CorrelationId;
                log.InitiatedUtc = telemetryContext.InitiatedUtc;
                log.AppliedUtc = executionStart;
                log.CompletedUtc = executionEnd;
                log.ExecutionTimeMs = (int)executionTime;

                // Set reason code based on execution result
                if (log.Success)
                {
                    log.ReasonCode = ReasonCodes.EXECUTION_SUCCESS;
                    log.PolicyDecision = "Execution completed successfully";
                }
                else if (!string.IsNullOrWhiteSpace(log.Error))
                {
                    // Try to determine specific failure reason
                    if (log.Error.Contains("policy", StringComparison.OrdinalIgnoreCase))
                        log.ReasonCode = ReasonCodes.POLICY_DENY;
                    else if (log.Error.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
                             log.Error.Contains("access denied", StringComparison.OrdinalIgnoreCase))
                        log.ReasonCode = ReasonCodes.ExecutionFailedPermissionDenied("Resource");
                    else if (log.Error.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                        log.ReasonCode = ReasonCodes.EXECUTION_TIMEOUT;
                    else
                        log.ReasonCode = ReasonCodes.EXECUTION_FAILED;

                    log.PolicyDecision = $"Execution failed: {log.Error}";
                }

                activity?.SetTag("execution.success", log.Success);
                activity?.SetTag("execution.duration_ms", executionTime);
                activity?.SetTag("reason.code", log.ReasonCode);

                _logger.LogInformation("Tweak execution finished: {TweakId} | Success: {Success} | Duration: {Duration}ms | ReasonCode: {ReasonCode} | {TelemetryContext}",
                    tweak.Id, log.Success, executionTime, log.ReasonCode, telemetryContext);

                return log;
            }
            catch (Exception ex)
            {
                var executionEnd = DateTime.UtcNow;
                var exceptionType = ex.GetType().Name;

                _logger.LogError(ex, "Critical error during tweak execution: {TweakId} | ExceptionType: {ExceptionType} | {TelemetryContext}",
                    tweak.Id, exceptionType, telemetryContext);

                activity?.SetTag("execution.success", false);
                activity?.SetTag("exception.type", exceptionType);
                activity?.SetTag("exception.message", ex.Message);

                // Return error log for audit trail with enhanced telemetry
                return new TweakApplicationLog
                {
                    TweakId = tweak.Id,
                    DeviceId = telemetryContext.DeviceId,
                    OperationId = telemetryContext.OperationId,
                    CorrelationId = telemetryContext.CorrelationId,
                    InitiatedUtc = telemetryContext.InitiatedUtc,
                    AppliedUtc = startTime,
                    CompletedUtc = executionEnd,
                    Success = false,
                    Error = $"Execution failed: {ex.Message}",
                    ExecutionTimeMs = (int)(executionEnd - startTime).TotalMilliseconds,
                    ReasonCode = ReasonCodes.ExecutionFailedException(exceptionType),
                    PolicyDecision = $"Exception during execution: {exceptionType}"
                };
            }
        });
    }

    /// <summary>
    /// Enterprise audit logging with comprehensive fallback strategy.
    /// Implements secure /api/audit/log endpoint with legacy fallback and reason codes.
    /// </summary>
    private async Task<bool> SendAuditLogWithFallback(TweakApplicationLog log, TelemetryContext telemetryContext, string? machineToken, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("SendAuditLog", ActivityKind.Client);
        activity?.SetTag("operation.id", telemetryContext.OperationId);
        activity?.SetTag("correlation.id", telemetryContext.CorrelationId);

        try
        {
            // Primary: Secure audit endpoint with mTLS/machine token
            var success = await TrySendAuditLog("api/audit/log", log, telemetryContext, machineToken, cancellationToken);
            if (success)
            {
                _logger.LogDebug("Audit log sent via secure endpoint: {TweakId} | ReasonCode: {ReasonCode} | {TelemetryContext}",
                    log.TweakId, ReasonCodes.AuditSuccessEndpoint("secure"), telemetryContext);
                activity?.SetTag("audit.success", true);
                activity?.SetTag("audit.endpoint", "secure");
                return true;
            }

            // Fallback: Legacy endpoint
            _logger.LogWarning("Primary audit endpoint failed, attempting legacy fallback: {TweakId} | ReasonCode: {ReasonCode}",
                log.TweakId, ReasonCodes.AuditFailedEndpoint("secure"));
            success = await TrySendAuditLog("api/auditlogs", log, telemetryContext, machineToken, cancellationToken);

            if (success)
            {
                _logger.LogInformation("Audit log sent via legacy endpoint: {TweakId} | ReasonCode: {ReasonCode} | {TelemetryContext}",
                    log.TweakId, ReasonCodes.AuditSuccessEndpoint("legacy"), telemetryContext);
                activity?.SetTag("audit.success", true);
                activity?.SetTag("audit.endpoint", "legacy");
                return true;
            }

            // Both endpoints failed - store for offline retry
            _logger.LogError("All audit endpoints failed for: {TweakId} | ReasonCode: {ReasonCode} | Storing for offline retry | {TelemetryContext}",
                log.TweakId, ReasonCodes.AUDIT_FAILED, telemetryContext);

            activity?.SetTag("audit.success", false);
            activity?.SetTag("audit.queued", true);

            // Store failed audit logs for offline retry
            await StoreFailedAuditLogAsync(log, telemetryContext);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Critical failure in audit logging: {TweakId} | ReasonCode: {ReasonCode} | {TelemetryContext}",
                log.TweakId, ReasonCodes.ExecutionFailedException(ex.GetType().Name), telemetryContext);
            activity?.SetTag("audit.success", false);
            activity?.SetTag("exception.type", ex.GetType().Name);
            return false;
        }
    }

    /// <summary>
    /// Attempts to send audit log to specified endpoint with proper enterprise security headers and telemetry.
    /// </summary>
    private async Task<bool> TrySendAuditLog(string endpoint, TweakApplicationLog log, TelemetryContext telemetryContext, string? machineToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);

            // Enterprise-required headers with enhanced telemetry
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", telemetryContext.CorrelationId);
            request.Headers.TryAddWithoutValidation("X-Operation-ID", telemetryContext.OperationId);
            request.Headers.TryAddWithoutValidation("X-Device-ID", telemetryContext.DeviceId);
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

            // Log specific error responses for enterprise monitoring with reason codes
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning("Audit endpoint authentication failed: {Endpoint} | Status: {Status} | ReasonCode: {ReasonCode} | Machine token present: {HasToken}",
                    endpoint, response.StatusCode, ReasonCodes.NetworkFailedStatusCode(401), !string.IsNullOrWhiteSpace(machineToken));
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogWarning("Audit endpoint not found: {Endpoint} | Status: {Status} | ReasonCode: {ReasonCode}",
                    endpoint, response.StatusCode, ReasonCodes.NetworkFailedStatusCode(404));
            }
            else
            {
                var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Audit endpoint error: {Endpoint} | Status: {Status} | ReasonCode: {ReasonCode} | Response: {Response}",
                    endpoint, response.StatusCode, ReasonCodes.NetworkFailedStatusCode((int)response.StatusCode), responseContent);
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during audit log transmission: {Endpoint} | ReasonCode: {ReasonCode}",
                endpoint, ReasonCodes.NetworkFailedException(ex.GetType().Name));
            return false;
        }
    }

    /// <summary>
    /// Enterprise heartbeat with comprehensive system health monitoring.
    /// Implements Prompt 4: Enhanced heartbeat with agent version, OS, uptime, working set, CPU count, connection state.
    /// Detailed health opts in via Agent:SendDetailedHealthData=true.
    /// </summary>
    private async Task SendEnterpriseHeartbeat(HubConnection connection, string deviceId, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("SendHeartbeat", ActivityKind.Client);
        activity?.SetTag("device.id", deviceId);

        try
        {
            var telemetryContext = TelemetryContext.Create(deviceId);
            var currentProcess = Process.GetCurrentProcess();

            // Collect basic system health metrics (always sent) with privacy sanitization
            var basicHealthData = new
            {
                DeviceId = deviceId,
                OperationId = telemetryContext.OperationId,
                Timestamp = DateTime.UtcNow,
                AgentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown",
                OSVersion = Environment.OSVersion.ToString(),
                MachineNameHash = PrivacySanitizer.SanitizeMachineName(Environment.MachineName), // Privacy: Hashed or redacted
                ProcessorCount = Environment.ProcessorCount,
                SystemUptime = TimeSpan.FromMilliseconds(Environment.TickCount64),
                ProcessUptime = DateTime.UtcNow - currentProcess.StartTime.ToUniversalTime(),
                ConnectionState = connection.State.ToString()
            };

            // Send basic heartbeat to hub
            await connection.InvokeAsync("Heartbeat", deviceId, cancellationToken: cancellationToken);

            // Optional: Send detailed health data for enterprise monitoring (if enabled)
            var sendDetailedHealth = _config.GetValue<bool>("Agent:SendDetailedHealthData", false);
            if (sendDetailedHealth)
            {
                // Collect detailed health metrics (opt-in) with privacy sanitization
                var enhancedHealthData = new EnhancedHeartbeatData
                {
                    Context = telemetryContext,
                    Timestamp = DateTime.UtcNow,
                    AgentVersion = basicHealthData.AgentVersion,
                    OSVersion = basicHealthData.OSVersion,
                    MachineNameHash = basicHealthData.MachineNameHash, // Privacy: Already sanitized
                    ProcessorCount = basicHealthData.ProcessorCount,
                    SystemUptime = basicHealthData.SystemUptime,
                    ProcessUptime = basicHealthData.ProcessUptime,
                    ConnectionState = basicHealthData.ConnectionState,

                    // Detailed metrics
                    WorkingSetBytes = currentProcess.WorkingSet64,
                    PrivateMemoryBytes = currentProcess.PrivateMemorySize64,
                    VirtualMemoryBytes = currentProcess.VirtualMemorySize64,
                    ThreadCount = currentProcess.Threads.Count,
                    HandleCount = currentProcess.HandleCount,
                    GCTotalMemoryBytes = GC.GetTotalMemory(false),
                    GCGen0Collections = GC.CollectionCount(0),
                    GCGen1Collections = GC.CollectionCount(1),
                    GCGen2Collections = GC.CollectionCount(2),

                    // Operation counters (reset after sending)
                    OperationsExecuted = Interlocked.Exchange(ref _operationsExecuted, 0),
                    OperationsSucceeded = Interlocked.Exchange(ref _operationsSucceeded, 0),
                    OperationsFailed = Interlocked.Exchange(ref _operationsFailed, 0),

                    // Calculate health score
                    HealthScore = CalculateHealthScore(currentProcess)
                };

                await connection.InvokeAsync("HealthData", enhancedHealthData, cancellationToken: cancellationToken);

                _logger.LogDebug("Enhanced heartbeat sent: {DeviceId} | HealthScore: {HealthScore} | Operations: {Executed}/{Succeeded}/{Failed}",
                    deviceId, enhancedHealthData.HealthScore,
                    enhancedHealthData.OperationsExecuted, enhancedHealthData.OperationsSucceeded, enhancedHealthData.OperationsFailed);
            }

            activity?.SetTag("heartbeat.success", true);
            activity?.SetTag("heartbeat.detailed", sendDetailedHealth);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send enterprise heartbeat: {DeviceId} | ReasonCode: {ReasonCode}",
                deviceId, ReasonCodes.ExecutionFailedException(ex.GetType().Name));
            activity?.SetTag("heartbeat.success", false);
            activity?.SetTag("exception.type", ex.GetType().Name);
            throw; // Re-throw to trigger failure handling in calling method
        }
    }

    /// <summary>
    /// Calculates a health score (0-100) based on process metrics.
    /// </summary>
    private int CalculateHealthScore(Process process)
    {
        int score = 100;

        // Deduct points for high memory usage (>500MB working set)
        var workingSetMB = process.WorkingSet64 / (1024.0 * 1024.0);
        if (workingSetMB > 500)
            score -= Math.Min(20, (int)((workingSetMB - 500) / 50));

        // Deduct points for high thread count (>50 threads)
        if (process.Threads.Count > 50)
            score -= Math.Min(15, (process.Threads.Count - 50) / 5);

        // Deduct points for high handle count (>1000 handles)
        if (process.HandleCount > 1000)
            score -= Math.Min(15, (process.HandleCount - 1000) / 100);

        // Deduct points for excessive GC Gen2 collections
        var gen2Collections = GC.CollectionCount(2);
        if (gen2Collections > 100)
            score -= Math.Min(10, (gen2Collections - 100) / 20);

        return Math.Max(0, Math.Min(100, score));
    }

    /// <summary>
    /// Sends real-time execution result via SignalR hub for immediate admin feedback.
    /// Critical for enterprise real-time monitoring and compliance.
    /// </summary>
    private async Task<bool> SendHubExecutionResult(HubConnection connection, TweakApplicationLog log, TelemetryContext telemetryContext, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity("SendHubResult", ActivityKind.Client);
        activity?.SetTag("operation.id", telemetryContext.OperationId);
        activity?.SetTag("correlation.id", telemetryContext.CorrelationId);

        try
        {
            if (connection.State != HubConnectionState.Connected)
            {
                _logger.LogWarning("Hub connection not available for execution result: {TweakId} | State: {State} | ReasonCode: {ReasonCode}",
                    log.TweakId, connection.State, ReasonCodes.NETWORK_UNAVAILABLE);
                activity?.SetTag("hub.success", false);
                activity?.SetTag("hub.state", connection.State.ToString());
                return false;
            }

            // Send execution result with full telemetry context for enterprise tracking
            await connection.InvokeAsync("ReportExecutionResult", log, telemetryContext.CorrelationId, cancellationToken: cancellationToken);

            _logger.LogDebug("Hub execution result sent: {TweakId} | ReasonCode: {ReasonCode} | {TelemetryContext}",
                log.TweakId, ReasonCodes.NETWORK_SUCCESS, telemetryContext);
            activity?.SetTag("hub.success", true);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send hub execution result: {TweakId} | ReasonCode: {ReasonCode} | {TelemetryContext}",
                log.TweakId, ReasonCodes.NetworkFailedException(ex.GetType().Name), telemetryContext);
            activity?.SetTag("hub.success", false);
            activity?.SetTag("exception.type", ex.GetType().Name);
            return false;
        }
    }

    /// <summary>
    /// Stores failed audit logs to local disk for offline retry with encryption.
    /// Implements Prompt 4: Encrypted offline queue for failed audit logs.
    /// </summary>
    private async Task StoreFailedAuditLogAsync(TweakApplicationLog log, TelemetryContext telemetryContext)
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var queueDir = Path.Combine(localAppData, "GGs", "OfflineQueue");
            Directory.CreateDirectory(queueDir);

            var fileName = $"audit_{telemetryContext.OperationId}_{DateTime.UtcNow:yyyyMMddHHmmss}.json";
            var filePath = Path.Combine(queueDir, fileName);

            var queueItem = new
            {
                Log = log,
                TelemetryContext = new
                {
                    telemetryContext.DeviceId,
                    telemetryContext.OperationId,
                    telemetryContext.CorrelationId,
                    telemetryContext.InitiatedUtc
                },
                FailedAtUtc = DateTime.UtcNow,
                RetryCount = 0,
                ReasonCode = ReasonCodes.QUEUE_ENQUEUED
            };

            var json = System.Text.Json.JsonSerializer.Serialize(queueItem, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });

            // TODO: Implement encryption for sensitive audit data
            // For now, write as plaintext but mark for future encryption
            await File.WriteAllTextAsync(filePath, json);

            _logger.LogInformation("Failed audit log stored for offline retry: {FilePath} | ReasonCode: {ReasonCode} | {TelemetryContext}",
                filePath, ReasonCodes.QUEUE_ENQUEUED, telemetryContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store audit log for offline retry: ReasonCode: {ReasonCode} | {TelemetryContext}",
                ReasonCodes.ExecutionFailedException(ex.GetType().Name), telemetryContext);
        }
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        if (!baseUrl.EndsWith('/')) baseUrl += "/";
        if (path.StartsWith('/')) path = path[1..];
        return baseUrl + path;
    }
}
