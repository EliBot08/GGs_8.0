using Microsoft.Extensions.Hosting;

namespace GGs.Server.Services;

public sealed class OnlinePresenceService : BackgroundService
{
    private readonly ILogger<OnlinePresenceService> _logger;
    private readonly DeviceRegistry _registry;

    public OnlinePresenceService(ILogger<OnlinePresenceService> logger, DeviceRegistry registry)
    {
        _logger = logger;
        _registry = registry;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var removed = _registry.ExpireStale(TimeSpan.FromSeconds(120));
                if (removed > 0)
                {
                    _logger.LogInformation("Expired {Count} stale device connections", removed);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error expiring stale device connections");
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}

