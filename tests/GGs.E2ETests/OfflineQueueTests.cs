using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Xunit;
using GGs.Desktop.Services;
using GGs.Shared.Tweaks;
using GGs.Shared.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GGs.E2ETests;

/// <summary>
/// Enterprise-grade E2E tests for offline queue functionality
/// Critical for production readiness - ensures data persistence during network outages
/// </summary>
public class OfflineQueueTests : IDisposable
{
    private readonly string _testQueuePath;
    private readonly OfflineQueueService _queueService;
    private readonly ILogger<OfflineQueueService> _logger;

    public OfflineQueueTests()
    {
        _testQueuePath = Path.Combine(Path.GetTempPath(), "GGsTests", $"offline_queue_{Guid.NewGuid():N}.db");
        Directory.CreateDirectory(Path.GetDirectoryName(_testQueuePath)!);
        
        _logger = NullLogger<OfflineQueueService>.Instance;
        _queueService = new OfflineQueueService(_testQueuePath, _logger);
    }

    [Fact]
    public async Task OfflineQueue_EnqueueAndDequeue_ShouldMaintainDataIntegrity()
    {
        // Arrange - Create test tweak application log
        var testLog = new TweakApplicationLog
        {
            Id = Guid.NewGuid(),
            TweakId = Guid.NewGuid(),
            DeviceId = "TEST-DEVICE-001",
            UserId = "test-user@enterprise.com",
            AppliedUtc = DateTime.UtcNow,
            Success = true,
            BeforeState = "HKEY_CURRENT_USER\\Test\\Value=0",
            AfterState = "HKEY_CURRENT_USER\\Test\\Value=1",
            ExecutionTimeMs = 150,
            Error = null
        };

        // Act - Enqueue the log
        await _queueService.EnqueueAsync("audit_log", testLog);
        
        // Assert - Verify queue contains the item
        var queuedItems = await _queueService.GetPendingAsync<TweakApplicationLog>("audit_log");
        Assert.Single(queuedItems);
        
        var queuedItem = queuedItems[0];
        Assert.Equal(testLog.Id, queuedItem.Id);
        Assert.Equal(testLog.DeviceId, queuedItem.DeviceId);
        Assert.Equal(testLog.Success, queuedItem.Success);
        
        // Act - Dequeue the item
        var dequeuedItems = await _queueService.DequeueAsync<TweakApplicationLog>("audit_log", 1);
        
        // Assert - Verify dequeue worked
        Assert.Single(dequeuedItems);
        Assert.Equal(testLog.Id, dequeuedItems[0].Id);
        
        // Verify queue is now empty
        var remainingItems = await _queueService.GetPendingAsync<TweakApplicationLog>("audit_log");
        Assert.Empty(remainingItems);
    }

    [Fact]
    public async Task OfflineQueue_MultipleItems_ShouldProcessFIFO()
    {
        // Arrange - Create multiple test items
        var testLogs = new List<TweakApplicationLog>();
        for (int i = 0; i < 5; i++)
        {
            testLogs.Add(new TweakApplicationLog
            {
                Id = Guid.NewGuid(),
                TweakId = Guid.NewGuid(),
                DeviceId = $"DEVICE-{i:D3}",
                UserId = "test-user@enterprise.com",
                AppliedUtc = DateTime.UtcNow.AddSeconds(i),
                Success = true,
                BeforeState = $"TestValue=0",
                AfterState = $"TestValue={i}",
                ExecutionTimeMs = 100 + i * 10
            });
        }

        // Act - Enqueue all items
        foreach (var log in testLogs)
        {
            await _queueService.EnqueueAsync("audit_log", log);
            await Task.Delay(10); // Ensure different timestamps
        }

        // Act - Dequeue items one by one
        var dequeuedItems = new List<TweakApplicationLog>();
        for (int i = 0; i < 5; i++)
        {
            var items = await _queueService.DequeueAsync<TweakApplicationLog>("audit_log", 1);
            Assert.Single(items);
            dequeuedItems.Add(items[0]);
        }

        // Assert - Verify FIFO order
        for (int i = 0; i < 5; i++)
        {
            Assert.Equal(testLogs[i].Id, dequeuedItems[i].Id);
            Assert.Equal($"DEVICE-{i:D3}", dequeuedItems[i].DeviceId);
        }
    }

    [Fact]
    public async Task OfflineQueue_ConcurrentOperations_ShouldHandleThreadSafety()
    {
        // Arrange - Create concurrent tasks
        var tasks = new List<Task>();
        var testLogs = new List<TweakApplicationLog>();
        
        // Create test data
        for (int i = 0; i < 10; i++)
        {
            testLogs.Add(new TweakApplicationLog
            {
                Id = Guid.NewGuid(),
                TweakId = Guid.NewGuid(),
                DeviceId = $"CONCURRENT-DEVICE-{i:D3}",
                UserId = "concurrent-user@enterprise.com",
                AppliedUtc = DateTime.UtcNow,
                Success = true,
                BeforeState = "ConcurrentTest=0",
                AfterState = $"ConcurrentTest={i}",
                ExecutionTimeMs = 50
            });
        }

        // Act - Enqueue concurrently
        foreach (var log in testLogs)
        {
            var capturedLog = log; // Capture for closure
            tasks.Add(Task.Run(async () => await _queueService.EnqueueAsync("concurrent_test", capturedLog)));
        }

        await Task.WhenAll(tasks);
        tasks.Clear();

        // Act - Dequeue concurrently
        var results = new List<TweakApplicationLog>();
        var resultsLock = new object();
        
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                var items = await _queueService.DequeueAsync<TweakApplicationLog>("concurrent_test", 2);
                lock (resultsLock)
                {
                    results.AddRange(items);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Verify all items were processed
        Assert.Equal(10, results.Count);
        
        // Verify no duplicates
        var ids = results.Select(r => r.Id).ToHashSet();
        Assert.Equal(10, ids.Count);
    }

    [Fact]
    public async Task OfflineQueue_LargePayload_ShouldHandleCorrectly()
    {
        // Arrange - Create large payload (simulating complex tweak with extensive metadata)
        var largeBeforeState = string.Join("\n", Enumerable.Repeat("HKEY_CURRENT_USER\\Large\\Path\\With\\Many\\Subkeys\\Value", 100));
        var largeAfterState = string.Join("\n", Enumerable.Repeat("HKEY_CURRENT_USER\\Large\\Path\\With\\Many\\Subkeys\\ModifiedValue", 100));
        
        var largeLog = new TweakApplicationLog
        {
            Id = Guid.NewGuid(),
            TweakId = Guid.NewGuid(),
            DeviceId = "LARGE-PAYLOAD-DEVICE",
            UserId = "enterprise-admin@company.com",
            AppliedUtc = DateTime.UtcNow,
            Success = true,
            BeforeState = largeBeforeState,
            AfterState = largeAfterState,
            ExecutionTimeMs = 5000,
            Error = null
        };

        // Act - Enqueue large payload
        await _queueService.EnqueueAsync("large_payload", largeLog);
        
        // Act - Dequeue large payload
        var dequeuedItems = await _queueService.DequeueAsync<TweakApplicationLog>("large_payload", 1);
        
        // Assert - Verify data integrity
        Assert.Single(dequeuedItems);
        var retrieved = dequeuedItems[0];
        Assert.Equal(largeLog.Id, retrieved.Id);
        Assert.Equal(largeLog.BeforeState, retrieved.BeforeState);
        Assert.Equal(largeLog.AfterState, retrieved.AfterState);
        Assert.Equal(largeLog.ExecutionTimeMs, retrieved.ExecutionTimeMs);
    }

    [Fact]
    public async Task OfflineQueue_FailedServerSync_ShouldRetainForRetry()
    {
        // Arrange - Create multiple items for retry scenarios
        var failedLogs = new List<TweakApplicationLog>();
        for (int i = 0; i < 3; i++)
        {
            failedLogs.Add(new TweakApplicationLog
            {
                Id = Guid.NewGuid(),
                TweakId = Guid.NewGuid(),
                DeviceId = "RETRY-DEVICE",
                UserId = "retry-user@enterprise.com",
                AppliedUtc = DateTime.UtcNow.AddMinutes(-i),
                Success = false,
                BeforeState = "RetryTest=original",
                AfterState = "RetryTest=failed",
                ExecutionTimeMs = 1000,
                Error = $"Network timeout on attempt {i + 1}"
            });
        }

        // Act - Enqueue failed items
        foreach (var log in failedLogs)
        {
            await _queueService.EnqueueAsync("failed_sync", log);
        }

        // Simulate partial processing (some succeed, some fail)
        var processedItems = await _queueService.DequeueAsync<TweakApplicationLog>("failed_sync", 2);
        Assert.Equal(2, processedItems.Count);

        // Re-enqueue one item (simulating failed server sync)
        await _queueService.EnqueueAsync("failed_sync", processedItems[1]);

        // Assert - Verify items available for retry
        var remainingItems = await _queueService.GetPendingAsync<TweakApplicationLog>("failed_sync");
        Assert.Equal(2, remainingItems.Count); // 1 never dequeued + 1 re-enqueued
    }

    [Fact]
    public async Task OfflineQueue_DatabaseRecovery_ShouldSurviveRestart()
    {
        // Arrange - Create test data
        var persistentLog = new TweakApplicationLog
        {
            Id = Guid.NewGuid(),
            TweakId = Guid.NewGuid(),
            DeviceId = "PERSISTENT-DEVICE",
            UserId = "recovery-user@enterprise.com",
            AppliedUtc = DateTime.UtcNow,
            Success = true,
            BeforeState = "RecoveryTest=0",
            AfterState = "RecoveryTest=1",
            ExecutionTimeMs = 200
        };

        // Act - Enqueue and dispose service (simulating app restart)
        await _queueService.EnqueueAsync("recovery_test", persistentLog);
        _queueService.Dispose();

        // Create new service instance (simulating restart)
        using var newQueueService = new OfflineQueueService(_testQueuePath, _logger);
        
        // Act - Verify data survived restart
        var recoveredItems = await newQueueService.GetPendingAsync<TweakApplicationLog>("recovery_test");
        
        // Assert - Data should be intact
        Assert.Single(recoveredItems);
        var recovered = recoveredItems[0];
        Assert.Equal(persistentLog.Id, recovered.Id);
        Assert.Equal(persistentLog.DeviceId, recovered.DeviceId);
        Assert.Equal(persistentLog.Success, recovered.Success);
    }

    [Fact]
    public async Task OfflineQueue_QueueTypes_ShouldIsolateCorrectly()
    {
        // Arrange - Create different types of queued items
        var auditLog = new TweakApplicationLog
        {
            Id = Guid.NewGuid(),
            DeviceId = "AUDIT-DEVICE",
            AppliedUtc = DateTime.UtcNow,
            Success = true
        };

        var crashReport = new 
        {
            Id = Guid.NewGuid(),
            DeviceId = "CRASH-DEVICE",
            Exception = "System.NullReferenceException",
            Timestamp = DateTime.UtcNow
        };

        // Act - Enqueue to different queue types
        await _queueService.EnqueueAsync("audit_logs", auditLog);
        await _queueService.EnqueueAsync("crash_reports", crashReport);

        // Assert - Verify isolation
        var auditItems = await _queueService.GetPendingAsync<TweakApplicationLog>("audit_logs");
        var crashItems = await _queueService.GetPendingAsync<object>("crash_reports");

        Assert.Single(auditItems);
        Assert.Single(crashItems);
        
        // Verify correct data in each queue
        Assert.Equal("AUDIT-DEVICE", auditItems[0].DeviceId);
        // Note: crashItems would be dynamic objects, so we verify by count only
    }

    [Fact]
    public async Task OfflineQueue_PerformanceStress_ShouldHandleVolume()
    {
        // Arrange - Create large volume of items
        const int itemCount = 1000;
        var items = new List<TweakApplicationLog>();
        
        for (int i = 0; i < itemCount; i++)
        {
            items.Add(new TweakApplicationLog
            {
                Id = Guid.NewGuid(),
                TweakId = Guid.NewGuid(),
                DeviceId = $"STRESS-DEVICE-{i:D4}",
                UserId = "stress-test@enterprise.com",
                AppliedUtc = DateTime.UtcNow,
                Success = true,
                BeforeState = $"StressTest{i}=0",
                AfterState = $"StressTest{i}=1",
                ExecutionTimeMs = 10
            });
        }

        // Act - Time the enqueue operation
        var enqueueStart = DateTime.UtcNow;
        
        var enqueueTasks = items.Select(item => _queueService.EnqueueAsync("stress_test", item));
        await Task.WhenAll(enqueueTasks);
        
        var enqueueTime = DateTime.UtcNow - enqueueStart;

        // Act - Time the dequeue operation
        var dequeueStart = DateTime.UtcNow;
        var allDequeued = new List<TweakApplicationLog>();
        
        while (allDequeued.Count < itemCount)
        {
            var batch = await _queueService.DequeueAsync<TweakApplicationLog>("stress_test", 100);
            allDequeued.AddRange(batch);
            if (batch.Count == 0) break; // Safety check
        }
        
        var dequeueTime = DateTime.UtcNow - dequeueStart;

        // Assert - Performance expectations for enterprise use
        Assert.Equal(itemCount, allDequeued.Count);
        Assert.True(enqueueTime.TotalSeconds < 30, $"Enqueue took {enqueueTime.TotalSeconds:F2}s, expected < 30s");
        Assert.True(dequeueTime.TotalSeconds < 30, $"Dequeue took {dequeueTime.TotalSeconds:F2}s, expected < 30s");
        
        // Verify data integrity on random samples
        var random = new Random();
        for (int i = 0; i < 10; i++)
        {
            var randomIndex = random.Next(allDequeued.Count);
            var item = allDequeued[randomIndex];
            Assert.Contains("STRESS-DEVICE-", item.DeviceId);
            Assert.Equal("stress-test@enterprise.com", item.UserId);
        }
    }

    public void Dispose()
    {
        _queueService?.Dispose();
        
        try
        {
            if (File.Exists(_testQueuePath))
            {
                File.Delete(_testQueuePath);
            }
            
            var testDir = Path.GetDirectoryName(_testQueuePath);
            if (Directory.Exists(testDir) && !Directory.EnumerateFileSystemEntries(testDir).Any())
            {
                Directory.Delete(testDir);
            }
        }
        catch
        {
            // Ignore cleanup errors in tests
        }
    }
}
