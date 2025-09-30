using GGs.Agent;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;

// If invoked as elevated helper, run actions and exit
if (args != null && args.Length > 0 && Array.Exists(args, s => string.Equals(s, "--elevated", StringComparison.OrdinalIgnoreCase)))
{
    Environment.ExitCode = ElevatedEntry.Run(args);
    return;
}

var builder = Host.CreateApplicationBuilder(args);

// Enable Windows Service integration. When installed as a service, this configures the service name and lifecycle.
builder.Services.AddWindowsService(options =>
{
options.ServiceName = "GGsAgent";
});

// Write logs to Windows Event Log when running as service.
builder.Logging.AddEventLog(new EventLogSettings
{
    SourceName = "GGs.Agent",
    LogName = "Application",
    MachineName = "."
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
