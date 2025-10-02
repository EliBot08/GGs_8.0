param(
    [switch]$ForceBuild,
    [string]$DesktopPath,
    [switch]$ForceKill,
    [switch]$Test,
    [int]$TestDurationSeconds = 5
)

Import-Module (Join-Path $PSScriptRoot 'LauncherCore.psm1') -Force

$root = Resolve-Path -Path (Join-Path $PSScriptRoot '..' '..')
$context = New-LauncherContext -ComponentName 'Launch-Desktop-New' -BaseDirectory $root -TestMode:$Test

function Ensure-DesktopDependencies {
    param([psobject]$Context)

    $configPath = Join-Path $Context.BaseDirectory 'clients/GGs.Desktop/appsettings.json'
    if (-not (Test-Path -LiteralPath $configPath)) {
        Write-LauncherLog -Context $Context -Message "Desktop configuration missing at '$configPath'." -Level 'WARN'
    }
}

try {
    Write-LauncherLog -Context $context -Message 'Initializing GGs Desktop launcher.'
    Ensure-DesktopDependencies -Context $context

    $projectPath = 'clients/GGs.Desktop/GGs.Desktop.csproj'
    $buildOutput = 'out/desktop'

    if (-not $Test) {
        Invoke-ProjectBuild -Context $context -ProjectPath $projectPath -OutputDirectory $buildOutput -FriendlyName 'GGs Desktop client' -TargetFramework 'net9.0-windows' -Force:$ForceBuild | Out-Null
    }
    else {
        Write-LauncherLog -Context $context -Message 'Skipping desktop build in test mode.' -Level 'DEBUG'
    }

    $candidatePaths = @()
    if ($DesktopPath) { $candidatePaths += $DesktopPath }
    $candidatePaths += @(
        'out/desktop/GGs.Desktop.exe',
        'clients/GGs.Desktop/bin/Release/net9.0-windows/GGs.Desktop.exe'
    )

    $exePath = if ($Test) { 'powershell' } else { Resolve-ExecutablePath -Context $context -CandidatePaths $candidatePaths -FriendlyName 'GGs Desktop client' }

    Stop-ProcessTree -Context $context -ProcessNames @('GGs.Desktop')

    if ($Test) {
        $process = New-TestProcess -Context $context -DurationSeconds $TestDurationSeconds -DisplayName 'Desktop client simulation'
    }
    else {
        $process = Start-ManagedProcess -Context $context -FilePath $exePath
        if (-not (Wait-ForProcessStart -Process $process -TimeoutSeconds 20)) {
            throw 'GGs.Desktop did not become responsive in time.'
        }
    }

    Write-LauncherLog -Context $context -Message 'Desktop client running. Monitoring window state and runtime.'
    $exitCode = Monitor-Process -Context $context -Process $process -StatusLabel 'Desktop uptime'

    $runtime = (Get-Date) - $context.StartedAt
    Write-LauncherLog -Context $context -Message "Desktop application closed after $($runtime.ToString('hh\:mm\:ss')). Exit code: $exitCode" -Level 'INFO'
    exit $exitCode
}
catch {
    Write-LauncherLog -Context $context -Message $_.Exception.Message -Level 'ERROR'
    Write-LauncherLog -Context $context -Message 'Troubleshooting tips: ensure display drivers are updated, verify configuration files, and review the log for diagnostics.' -Level 'ERROR'
    exit 1
}
