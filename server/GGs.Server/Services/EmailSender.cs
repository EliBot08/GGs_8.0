namespace GGs.Server;

public interface IEmailSender
{
    Task SendWelcomeAsync(string email, string subject, string body, CancellationToken ct = default);
}

public sealed class DevEmailSender : IEmailSender
{
    private readonly ILogger<DevEmailSender> _logger;
    public DevEmailSender(ILogger<DevEmailSender> logger) { _logger = logger; }
    public Task SendWelcomeAsync(string email, string subject, string body, CancellationToken ct = default)
    {
        _logger.LogInformation("[DEV EMAIL] To={Email} Subject={Subject} Body={Body}", email, subject, body);
        return Task.CompletedTask;
    }
}

