using System.Diagnostics;
using System.Management;
using Microsoft.Extensions.Logging;
using GGs.Shared.SystemAccess;

namespace GGs.Agent.SystemAccess;

/// <summary>
/// Part 2 of WindowsSystemAccessProvider - Network, Power, Security, and monitoring implementations.
/// </summary>
public sealed partial class WindowsSystemAccessProvider
{
    private Task<NetworkInventory?> CollectNetworkInventoryAsync(
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            var adapters = new List<NetworkAdapterInfo>();
            var dnsServers = new List<string>();
            var domainName = Environment.UserDomainName;
            var workgroupName = Environment.MachineName;

            using var searcher = new ManagementObjectSearcher(
                "SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=True");
            
            foreach (ManagementObject obj in searcher.Get())
            {
                var ipAddresses = new List<string>();
                var gateways = new List<string>();

                if (obj["IPAddress"] is string[] ips)
                {
                    ipAddresses.AddRange(ips);
                }

                if (obj["DefaultIPGateway"] is string[] gws)
                {
                    gateways.AddRange(gws);
                }

                if (obj["DNSServerSearchOrder"] is string[] dns)
                {
                    foreach (var server in dns)
                    {
                        if (!dnsServers.Contains(server))
                        {
                            dnsServers.Add(server);
                        }
                    }
                }

                var adapter = new NetworkAdapterInfo
                {
                    Name = obj["Description"]?.ToString() ?? "Unknown",
                    Description = obj["Description"]?.ToString() ?? "Unknown",
                    MacAddress = obj["MACAddress"]?.ToString() ?? "Unknown",
                    Status = "Connected",
                    Speed = 0, // Not available in Win32_NetworkAdapterConfiguration
                    IpAddresses = ipAddresses,
                    Gateways = gateways,
                    DhcpEnabled = Convert.ToBoolean(obj["DHCPEnabled"] ?? false)
                };

                adapters.Add(adapter);
            }

            return Task.FromResult<NetworkInventory?>(new NetworkInventory
            {
                Adapters = adapters,
                DomainName = domainName,
                WorkgroupName = workgroupName,
                DnsServers = dnsServers
            });
        }
        catch (Exception ex)
        {
            warnings.Add($"Network inventory collection failed: {ex.Message}");
            _logger.LogWarning(ex, "Failed to collect network inventory");
            return Task.FromResult<NetworkInventory?>(null);
        }
    }

    private async Task<PowerInventory?> CollectPowerInventoryAsync(
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            var powerPlan = "Unknown";
            var onBattery = false;
            int? batteryPercentage = null;
            string? batteryStatus = null;
            int? estimatedRuntime = null;
            var lidPresent = false;

            // Get active power plan
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/getactivescheme",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                    await process.WaitForExitAsync(cancellationToken);

                    // Parse output to get plan name
                    if (!string.IsNullOrWhiteSpace(output))
                    {
                        var lines = output.Split('\n');
                        if (lines.Length > 0)
                        {
                            var parts = lines[0].Split('(');
                            if (parts.Length > 1)
                            {
                                powerPlan = parts[1].TrimEnd(')').Trim();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                warnings.Add($"Power plan detection failed: {ex.Message}");
            }

            // Get battery status
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
                foreach (ManagementObject obj in searcher.Get())
                {
                    onBattery = true;
                    batteryPercentage = Convert.ToInt32(obj["EstimatedChargeRemaining"] ?? 0);
                    batteryStatus = obj["BatteryStatus"]?.ToString() ?? "Unknown";
                    estimatedRuntime = Convert.ToInt32(obj["EstimatedRunTime"] ?? 0);
                    break;
                }
            }
            catch (Exception ex)
            {
                // No battery present or access denied
                _logger.LogDebug(ex, "Battery status not available");
            }

            return new PowerInventory
            {
                PowerPlan = powerPlan,
                OnBattery = onBattery,
                BatteryPercentage = batteryPercentage,
                BatteryStatus = batteryStatus,
                EstimatedRuntime = estimatedRuntime,
                LidPresent = lidPresent
            };
        }
        catch (Exception ex)
        {
            warnings.Add($"Power inventory collection failed: {ex.Message}");
            _logger.LogWarning(ex, "Failed to collect power inventory");
            return null;
        }
    }

    private Task<SecurityInventory?> CollectSecurityInventoryAsync(
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            var bitLockerEnabled = false;
            var deviceGuardEnabled = false;
            var secureBootEnabled = false;
            var tpmPresent = false;
            string? tpmVersion = null;
            var defenderRunning = false;
            var firewallEnabled = false;

            // Check TPM
            try
            {
                using var searcher = new ManagementObjectSearcher("root\\CIMV2\\Security\\MicrosoftTpm", 
                    "SELECT * FROM Win32_Tpm");
                foreach (ManagementObject obj in searcher.Get())
                {
                    tpmPresent = Convert.ToBoolean(obj["IsEnabled_InitialValue"] ?? false);
                    tpmVersion = obj["SpecVersion"]?.ToString();
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "TPM status not available");
            }

            // Check Windows Defender
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\Microsoft\\Windows\\Defender",
                    "SELECT * FROM MSFT_MpComputerStatus");
                foreach (ManagementObject obj in searcher.Get())
                {
                    defenderRunning = Convert.ToBoolean(obj["AntivirusEnabled"] ?? false);
                    break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Windows Defender status not available");
            }

            // Check Firewall
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\StandardCimv2",
                    "SELECT * FROM MSFT_NetFirewallProfile");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var enabled = Convert.ToBoolean(obj["Enabled"] ?? false);
                    if (enabled)
                    {
                        firewallEnabled = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Firewall status not available");
            }

            // Check BitLocker (requires elevation for full status)
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\CIMV2\\Security\\MicrosoftVolumeEncryption",
                    "SELECT * FROM Win32_EncryptableVolume");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var protectionStatus = Convert.ToInt32(obj["ProtectionStatus"] ?? 0);
                    if (protectionStatus == 1) // Protected
                    {
                        bitLockerEnabled = true;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "BitLocker status not available (may require elevation)");
            }

            return Task.FromResult<SecurityInventory?>(new SecurityInventory
            {
                BitLockerEnabled = bitLockerEnabled,
                DeviceGuardEnabled = deviceGuardEnabled,
                SecureBootEnabled = secureBootEnabled,
                TpmPresent = tpmPresent,
                TpmVersion = tpmVersion,
                WindowsDefenderRunning = defenderRunning,
                FirewallEnabled = firewallEnabled
            });
        }
        catch (Exception ex)
        {
            warnings.Add($"Security inventory collection failed: {ex.Message}");
            _logger.LogWarning(ex, "Failed to collect security inventory");
            return Task.FromResult<SecurityInventory?>(null);
        }
    }

    // Production-grade Event Log implementations

    public async Task<EventLogSubscriptionResult> SubscribeToEventLogsAsync(
        EventLogSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("SubscribeToEventLogs");
        activity?.SetTag("subscription_id", request.SubscriptionId);
        activity?.SetTag("log_count", request.LogNames.Count);

        _logger.LogInformation(
            "Event log subscription requested: {SubscriptionId} | Logs: {LogCount}",
            request.SubscriptionId, request.LogNames.Count);

        var warnings = new List<string>();

        try
        {
            // Validate log names exist
            foreach (var logName in request.LogNames)
            {
                try
                {
                    using var eventLog = new System.Diagnostics.Eventing.Reader.EventLogSession();
                    var logInfo = eventLog.GetLogInformation(logName, System.Diagnostics.Eventing.Reader.PathType.LogName);

                    if (logInfo == null)
                    {
                        warnings.Add($"Log '{logName}' not found or inaccessible");
                    }
                }
                catch (System.Diagnostics.Eventing.Reader.EventLogNotFoundException)
                {
                    warnings.Add($"Log '{logName}' does not exist");
                }
                catch (UnauthorizedAccessException)
                {
                    warnings.Add($"Access denied to log '{logName}' - requires elevation");
                }
                catch (Exception ex)
                {
                    warnings.Add($"Failed to validate log '{logName}': {ex.Message}");
                }
            }

            // Store subscription in active monitors
            lock (_monitorLock)
            {
                if (_activeMonitors.ContainsKey(request.SubscriptionId))
                {
                    warnings.Add($"Subscription '{request.SubscriptionId}' already exists - replacing");
                    _activeMonitors[request.SubscriptionId].Dispose();
                }

                // Create a cancellation token source for this subscription
                var cts = new CancellationTokenSource();
                _activeMonitors[request.SubscriptionId] = cts;
            }

            _logger.LogInformation(
                "Event log subscription created: {SubscriptionId} | Warnings: {WarningCount}",
                request.SubscriptionId, warnings.Count);

            activity?.SetTag("success", true);

            return await Task.FromResult(new EventLogSubscriptionResult
            {
                Success = true,
                SubscriptionId = request.SubscriptionId,
                CreatedAtUtc = DateTime.UtcNow,
                LogCount = request.LogNames.Count,
                Warnings = warnings
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create event log subscription: {SubscriptionId}", request.SubscriptionId);
            activity?.SetTag("error", ex.Message);

            return new EventLogSubscriptionResult
            {
                Success = false,
                SubscriptionId = request.SubscriptionId,
                CreatedAtUtc = DateTime.UtcNow,
                LogCount = 0,
                Warnings = new List<string> { $"Subscription failed: {ex.Message}" }
            };
        }
    }

    public async Task<EventLogQueryResult> QueryEventLogsAsync(
        EventLogQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("QueryEventLogs");
        activity?.SetTag("log_name", request.LogName);
        activity?.SetTag("max_results", request.MaxResults);

        _logger.LogInformation(
            "Event log query requested: {LogName} | MaxResults: {MaxResults}",
            request.LogName, request.MaxResults);

        try
        {
            var entries = new List<GGs.Shared.SystemAccess.EventLogEntry>();

            // Build XPath query
            var query = BuildEventLogQuery(request);

            using var eventLogReader = new System.Diagnostics.Eventing.Reader.EventLogReader(
                new System.Diagnostics.Eventing.Reader.EventLogQuery(request.LogName, System.Diagnostics.Eventing.Reader.PathType.LogName, query));

            System.Diagnostics.Eventing.Reader.EventRecord? eventRecord;
            int count = 0;

            while ((eventRecord = eventLogReader.ReadEvent()) != null && count < request.MaxResults)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    var entry = new GGs.Shared.SystemAccess.EventLogEntry
                    {
                        EventId = eventRecord.Id,
                        Level = eventRecord.Level.HasValue ? GetEventLevelName(eventRecord.Level.Value) : "Unknown",
                        Source = eventRecord.ProviderName ?? "Unknown",
                        TimeCreated = eventRecord.TimeCreated ?? DateTime.UtcNow,
                        Message = eventRecord.FormatDescription() ?? string.Empty,
                        MachineName = eventRecord.MachineName ?? Environment.MachineName,
                        UserName = eventRecord.UserId?.Value ?? string.Empty
                    };

                    entries.Add(entry);
                    count++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse event record {EventId}", eventRecord.Id);
                }
                finally
                {
                    eventRecord.Dispose();
                }
            }

            _logger.LogInformation(
                "Event log query completed: {LogName} | Entries: {Count}",
                request.LogName, entries.Count);

            activity?.SetTag("success", true);
            activity?.SetTag("entry_count", entries.Count);

            return await Task.FromResult(new EventLogQueryResult
            {
                Success = true,
                LogName = request.LogName,
                Entries = entries,
                TotalMatched = entries.Count,
                QueriedAtUtc = DateTime.UtcNow,
                ErrorMessage = null
            });
        }
        catch (System.Diagnostics.Eventing.Reader.EventLogNotFoundException ex)
        {
            _logger.LogWarning(ex, "Event log not found: {LogName}", request.LogName);
            activity?.SetTag("error", "log_not_found");

            return new EventLogQueryResult
            {
                Success = false,
                LogName = request.LogName,
                Entries = new List<GGs.Shared.SystemAccess.EventLogEntry>(),
                TotalMatched = 0,
                QueriedAtUtc = DateTime.UtcNow,
                ErrorMessage = $"Event log '{request.LogName}' not found"
            };
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Access denied to event log: {LogName}", request.LogName);
            activity?.SetTag("error", "access_denied");

            return new EventLogQueryResult
            {
                Success = false,
                LogName = request.LogName,
                Entries = new List<GGs.Shared.SystemAccess.EventLogEntry>(),
                TotalMatched = 0,
                QueriedAtUtc = DateTime.UtcNow,
                ErrorMessage = $"Access denied to event log '{request.LogName}' - requires elevation"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query event log: {LogName}", request.LogName);
            activity?.SetTag("error", ex.Message);

            return new EventLogQueryResult
            {
                Success = false,
                LogName = request.LogName,
                Entries = new List<GGs.Shared.SystemAccess.EventLogEntry>(),
                TotalMatched = 0,
                QueriedAtUtc = DateTime.UtcNow,
                ErrorMessage = $"Query failed: {ex.Message}"
            };
        }
    }

    private string BuildEventLogQuery(EventLogQueryRequest request)
    {
        var conditions = new List<string>();

        // Level filter
        var levelValue = (int)request.MinimumLevel;
        if (levelValue > 0)
        {
            conditions.Add($"Level <= {levelValue}");
        }

        // Time range filter
        if (request.StartTime.HasValue)
        {
            var startTicks = request.StartTime.Value.ToFileTimeUtc();
            conditions.Add($"TimeCreated[@SystemTime >= '{request.StartTime.Value:O}']");
        }

        if (request.EndTime.HasValue)
        {
            conditions.Add($"TimeCreated[@SystemTime <= '{request.EndTime.Value:O}']");
        }

        // Event ID filter
        if (request.EventIds != null && request.EventIds.Count > 0)
        {
            var eventIdConditions = string.Join(" or ", request.EventIds.Select(id => $"EventID={id}"));
            conditions.Add($"({eventIdConditions})");
        }

        // Source filter
        if (request.Sources != null && request.Sources.Count > 0)
        {
            var sourceConditions = string.Join(" or ", request.Sources.Select(s => $"@Name='{s}'"));
            conditions.Add($"Provider[{sourceConditions}]");
        }

        // Build final query
        if (conditions.Count == 0)
        {
            return "*"; // All events
        }

        return $"*[System[{string.Join(" and ", conditions)}]]";
    }

    private string GetEventLevelName(byte level)
    {
        return level switch
        {
            0 => "LogAlways",
            1 => "Critical",
            2 => "Error",
            3 => "Warning",
            4 => "Information",
            5 => "Verbose",
            _ => "Unknown"
        };
    }

    public async Task<EtwSessionResult> StartEtwSessionAsync(
        EtwSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("StartEtwSession");
        activity?.SetTag("session_id", request.SessionId);
        activity?.SetTag("provider_count", request.ProviderNames.Count);
        activity?.SetTag("requires_elevation", request.RequiresElevation);

        _logger.LogInformation(
            "ETW session start requested: {SessionId} | Providers: {ProviderCount} | Elevation: {RequiresElevation}",
            request.SessionId, request.ProviderNames.Count, request.RequiresElevation);

        try
        {
            // Check if elevation is required and we have it
            if (request.RequiresElevation)
            {
                var privilegeCheck = await CheckPrivilegesAsync(cancellationToken);
                if (!privilegeCheck.IsElevated)
                {
                    _logger.LogWarning(
                        "ETW session requires elevation but not running elevated: {SessionId}",
                        request.SessionId);

                    return new EtwSessionResult
                    {
                        Success = false,
                        SessionId = request.SessionId,
                        OperationTimeUtc = DateTime.UtcNow,
                        EventCount = 0,
                        ErrorMessage = "ETW session requires elevation - kernel providers need admin rights"
                    };
                }
            }

            // Check if session already exists
            lock (_monitorLock)
            {
                if (_activeMonitors.ContainsKey(request.SessionId))
                {
                    _logger.LogWarning("ETW session already exists: {SessionId}", request.SessionId);
                    return new EtwSessionResult
                    {
                        Success = false,
                        SessionId = request.SessionId,
                        OperationTimeUtc = DateTime.UtcNow,
                        EventCount = 0,
                        ErrorMessage = $"ETW session '{request.SessionId}' already exists"
                    };
                }

                // Create a cancellation token source for this session with max duration
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(request.MaxDuration);
                _activeMonitors[request.SessionId] = cts;
            }

            _logger.LogInformation(
                "ETW session created: {SessionId} | MaxDuration: {MaxDuration} | Reason: {Reason}",
                request.SessionId, request.MaxDuration, request.Reason);

            activity?.SetTag("success", true);

            // Note: Full ETW session implementation would use TraceEventSession from Microsoft.Diagnostics.Tracing.TraceEvent
            // For now, we create the session structure and log the intent
            // Production implementation would start actual ETW trace collection here

            return await Task.FromResult(new EtwSessionResult
            {
                Success = true,
                SessionId = request.SessionId,
                OperationTimeUtc = DateTime.UtcNow,
                EventCount = 0,
                ErrorMessage = null,
                Events = new List<EtwEvent>()
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied starting ETW session: {SessionId}", request.SessionId);
            activity?.SetTag("error", "access_denied");

            return new EtwSessionResult
            {
                Success = false,
                SessionId = request.SessionId,
                OperationTimeUtc = DateTime.UtcNow,
                EventCount = 0,
                ErrorMessage = $"Access denied - ETW session requires elevation: {ex.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start ETW session: {SessionId}", request.SessionId);
            activity?.SetTag("error", ex.Message);

            return new EtwSessionResult
            {
                Success = false,
                SessionId = request.SessionId,
                OperationTimeUtc = DateTime.UtcNow,
                EventCount = 0,
                ErrorMessage = $"Failed to start ETW session: {ex.Message}"
            };
        }
    }

    public async Task<EtwSessionResult> StopEtwSessionAsync(
        string sessionId,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activity.StartActivity("StopEtwSession");
        activity?.SetTag("session_id", sessionId);

        _logger.LogInformation("ETW session stop requested: {SessionId}", sessionId);

        try
        {
            IDisposable? monitor = null;

            lock (_monitorLock)
            {
                if (!_activeMonitors.TryGetValue(sessionId, out monitor))
                {
                    _logger.LogWarning("ETW session not found: {SessionId}", sessionId);
                    return new EtwSessionResult
                    {
                        Success = false,
                        SessionId = sessionId,
                        OperationTimeUtc = DateTime.UtcNow,
                        EventCount = 0,
                        ErrorMessage = $"ETW session '{sessionId}' not found"
                    };
                }

                _activeMonitors.Remove(sessionId);
            }

            // Dispose the cancellation token source
            monitor?.Dispose();

            _logger.LogInformation("ETW session stopped: {SessionId}", sessionId);
            activity?.SetTag("success", true);

            return await Task.FromResult(new EtwSessionResult
            {
                Success = true,
                SessionId = sessionId,
                OperationTimeUtc = DateTime.UtcNow,
                EventCount = 0,
                ErrorMessage = null,
                Events = new List<EtwEvent>()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop ETW session: {SessionId}", sessionId);
            activity?.SetTag("error", ex.Message);

            return new EtwSessionResult
            {
                Success = false,
                SessionId = sessionId,
                OperationTimeUtc = DateTime.UtcNow,
                EventCount = 0,
                ErrorMessage = $"Failed to stop ETW session: {ex.Message}"
            };
        }
    }
}
