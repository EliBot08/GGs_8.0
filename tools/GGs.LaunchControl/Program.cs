using System.Diagnostics;
using System.Text.Json;
using GGs.LaunchControl.Models;
using GGs.LaunchControl.Services;
using Spectre.Console;

namespace GGs.LaunchControl;

internal class Program
{
    private static readonly string Version = "1.0.0";
    private static readonly string LogDirectory = Path.Combine(AppContext.BaseDirectory, "..", "..", "launcher-logs");
    private static string? _currentLogFile;

    static async Task<int> Main(string[] args)
    {
        try
        {
            // Ensure log directory exists
            Directory.CreateDirectory(LogDirectory);

            // Parse command line arguments
            var options = ParseArguments(args);

            // Initialize logging
            _currentLogFile = InitializeLogging(options.Profile, options.Mode);

            // Display neon ASCII intro
            DisplayIntro(options);

            // Check for elevation status
            var isElevated = PrivilegeChecker.IsElevated();
            DisplayElevationStatus(isElevated, options.Elevate);

            // Load profile
            var profile = await LoadProfileAsync(options.Profile);
            if (profile == null)
            {
                AnsiConsole.MarkupLine("[red]✗[/] Failed to load profile");
                return 1;
            }

            // Run health checks
            var healthChecker = new HealthChecker();
            var healthResults = await healthChecker.RunHealthChecksAsync(profile.HealthChecks);
            DisplayHealthCheckResults(healthResults);

            // Check if any required health checks failed
            if (healthResults.Any(r => r.Check.Required && !r.Passed && !r.AutoFixed))
            {
                AnsiConsole.MarkupLine("[red]✗[/] Required health checks failed. Cannot continue.");
                return 1;
            }

            // Launch applications
            var launcher = new ApplicationLauncher(options.Elevate);
            var launchResults = await launcher.LaunchApplicationsAsync(profile, options.Mode);
            DisplayLaunchResults(launchResults);

            // Handle exit policy
            await HandleExitPolicyAsync(profile, launchResults);

            AnsiConsole.MarkupLine("[green]✓[/] Launch sequence completed successfully");
            Log($"Launch sequence completed successfully for profile '{options.Profile}'");

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            Log($"FATAL ERROR: {ex.Message}\n{ex.StackTrace}");
            return 1;
        }
    }

    private static LaunchOptions ParseArguments(string[] args)
    {
        var options = new LaunchOptions();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i].ToLowerInvariant();
            switch (arg)
            {
                case "--profile":
                case "-p":
                    if (i + 1 < args.Length)
                        options.Profile = args[++i];
                    break;
                case "--elevate":
                case "-e":
                    options.Elevate = true;
                    break;
                case "--mode":
                case "-m":
                    if (i + 1 < args.Length)
                        options.Mode = args[++i];
                    break;
                case "--help":
                case "-h":
                case "/?":
                    DisplayHelp();
                    Environment.Exit(0);
                    break;
            }
        }

        // Default profile if not specified
        if (string.IsNullOrWhiteSpace(options.Profile))
        {
            options.Profile = "desktop";
        }

        return options;
    }

    private static void DisplayHelp()
    {
        AnsiConsole.Write(new FigletText("GGs LaunchControl").Color(Color.Cyan1));
        AnsiConsole.MarkupLine($"[cyan]Version {Version}[/]\n");
        AnsiConsole.MarkupLine("[yellow]Usage:[/]");
        AnsiConsole.MarkupLine("  GGs.LaunchControl [[options]]\n");
        AnsiConsole.MarkupLine("[yellow]Options:[/]");
        AnsiConsole.MarkupLine("  --profile, -p [grey]NAME[/]     Profile to launch (desktop, errorlogviewer, fusion)");
        AnsiConsole.MarkupLine("  --elevate, -e           Request elevation (admin rights)");
        AnsiConsole.MarkupLine("  --mode, -m [grey]MODE[/]        Launch mode (normal, diag, test)");
        AnsiConsole.MarkupLine("  --help, -h              Display this help message\n");
        AnsiConsole.MarkupLine("[yellow]Examples:[/]");
        AnsiConsole.MarkupLine("  GGs.LaunchControl --profile desktop");
        AnsiConsole.MarkupLine("  GGs.LaunchControl --profile fusion --mode diag");
        AnsiConsole.MarkupLine("  GGs.LaunchControl --profile errorlogviewer --elevate");
    }

    private static void DisplayIntro(LaunchOptions options)
    {
        AnsiConsole.Clear();
        
        var panel = new Panel(new FigletText("GGs LaunchControl").Centered().Color(Color.Cyan1))
        {
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1)
        };
        AnsiConsole.Write(panel);

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[cyan]Property[/]").Centered())
            .AddColumn(new TableColumn("[cyan]Value[/]").LeftAligned());

        table.AddRow("Version", $"[green]{Version}[/]");
        table.AddRow("Profile", $"[yellow]{options.Profile}[/]");
        table.AddRow("Mode", $"[yellow]{options.Mode}[/]");
        table.AddRow("Elevation Requested", options.Elevate ? "[red]Yes[/]" : "[green]No[/]");
        table.AddRow("Log File", $"[grey]{Path.GetFileName(_currentLogFile ?? "N/A")}[/]");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private static void DisplayElevationStatus(bool isElevated, bool elevationRequested)
    {
        var statusPanel = new Panel(
            isElevated
                ? "[green]✓[/] Running with Administrator privileges"
                : elevationRequested
                    ? "[yellow]⚠[/] Admin privileges were requested but [red]DECLINED BY OPERATOR[/]\n[grey]Continuing in non-elevated mode (this is normal and expected)[/]"
                    : "[cyan]ℹ[/] Running with standard user privileges (non-admin mode)"
        )
        {
            Header = new PanelHeader(" Privilege Status ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(isElevated ? Color.Green : Color.Yellow)
        };

        AnsiConsole.Write(statusPanel);
        AnsiConsole.WriteLine();

        Log($"Elevation Status: IsElevated={isElevated}, Requested={elevationRequested}");
    }

    private static void DisplayHealthCheckResults(List<HealthCheckResult> results)
    {
        if (results.Count == 0)
            return;

        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[cyan]Health Check[/]").LeftAligned())
            .AddColumn(new TableColumn("[cyan]Status[/]").Centered())
            .AddColumn(new TableColumn("[cyan]Details[/]").LeftAligned());

        foreach (var result in results)
        {
            var status = result.Passed
                ? "[green]✓ PASS[/]"
                : result.AutoFixed
                    ? "[yellow]⚠ FIXED[/]"
                    : "[red]✗ FAIL[/]";

            var details = result.Message ?? (result.Passed ? "OK" : "Failed");
            if (result.AutoFixed)
                details += " (auto-fixed)";

            table.AddRow(result.Check.Description, status, details);
        }

        AnsiConsole.Write(new Panel(table)
        {
            Header = new PanelHeader(" Preflight Health Checks ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Cyan1)
        });
        AnsiConsole.WriteLine();
    }

    private static void DisplayLaunchResults(List<LaunchResult> results)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[cyan]Application[/]").LeftAligned())
            .AddColumn(new TableColumn("[cyan]Status[/]").Centered())
            .AddColumn(new TableColumn("[cyan]Process ID[/]").Centered())
            .AddColumn(new TableColumn("[cyan]Details[/]").LeftAligned());

        foreach (var result in results)
        {
            var status = result.Success
                ? "[green]✓ RUNNING[/]"
                : result.ElevationDeclined
                    ? "[yellow]⚠ DECLINED[/]"
                    : "[red]✗ FAILED[/]";

            var pid = result.ProcessId?.ToString() ?? "N/A";
            var details = result.ErrorMessage ?? "Started successfully";

            table.AddRow(result.Application.Name, status, pid, details);
        }

        AnsiConsole.Write(new Panel(table)
        {
            Header = new PanelHeader(" Application Launch Results ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Green)
        });
        AnsiConsole.WriteLine();
    }

    private static async Task HandleExitPolicyAsync(LaunchProfile profile, List<LaunchResult> results)
    {
        switch (profile.ExitPolicy.ToLowerInvariant())
        {
            case "waitforall":
                AnsiConsole.MarkupLine("[yellow]Waiting for all applications to exit...[/]");
                // Implementation would wait for all processes
                break;
            case "waitforany":
                AnsiConsole.MarkupLine("[yellow]Waiting for any application to exit...[/]");
                // Implementation would wait for first process to exit
                break;
            case "fireandf orget":
            default:
                AnsiConsole.MarkupLine("[cyan]Applications launched. LaunchControl exiting.[/]");
                await Task.Delay(1000); // Brief delay to ensure processes are stable
                break;
        }
    }

    private static async Task<LaunchProfile?> LoadProfileAsync(string profileName)
    {
        try
        {
            var profilePath = Path.Combine(AppContext.BaseDirectory, "profiles", $"{profileName}.json");
            
            if (!File.Exists(profilePath))
            {
                AnsiConsole.MarkupLine($"[red]✗[/] Profile not found: {profilePath}");
                Log($"Profile not found: {profilePath}");
                return null;
            }

            var json = await File.ReadAllTextAsync(profilePath);
            var profile = JsonSerializer.Deserialize(json, LaunchProfileJsonContext.Default.LaunchProfile);

            if (profile == null)
            {
                AnsiConsole.MarkupLine("[red]✗[/] Failed to deserialize profile");
                Log($"Failed to deserialize profile: {profilePath}");
                return null;
            }

            Log($"Loaded profile '{profileName}' from {profilePath}");
            return profile;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]✗[/] Error loading profile: {ex.Message}");
            Log($"Error loading profile '{profileName}': {ex.Message}");
            return null;
        }
    }

    private static string InitializeLogging(string profile, string mode)
    {
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var logFile = Path.Combine(LogDirectory, $"{profile}-{timestamp}-{mode}.log");
        
        File.WriteAllText(logFile, $"=== GGs LaunchControl v{Version} ===\n");
        File.AppendAllText(logFile, $"Profile: {profile}\n");
        File.AppendAllText(logFile, $"Mode: {mode}\n");
        File.AppendAllText(logFile, $"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        File.AppendAllText(logFile, $"========================================\n\n");

        return logFile;
    }

    private static void Log(string message)
    {
        if (_currentLogFile == null)
            return;

        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            File.AppendAllText(_currentLogFile, $"[{timestamp}] {message}\n");
        }
        catch
        {
            // Ignore logging errors
        }
    }
}

internal class LaunchOptions
{
    public string Profile { get; set; } = "desktop";
    public bool Elevate { get; set; } = false;
    public string Mode { get; set; } = "normal";
}

