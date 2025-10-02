param(
    [switch]$ForceBuild,
    [string]$ViewerPath,
    [switch]$ForceKill,
    [switch]$Test,
    [int]$TestDurationSeconds = 5
)

Import-Module (Join-Path $PSScriptRoot 'LauncherCore.psm1') -Force

$root = Resolve-Path -Path (Join-Path $PSScriptRoot '..' '..')
$context = New-LauncherContext -ComponentName 'Launch-Viewer-New' -BaseDirectory $root -TestMode:$Test

try {
    Write-LauncherLog -Context $context -Message 'Initializing GGs Error Log Viewer launcher.'

    $projectPath = 'tools/GGs.ErrorLogViewer/GGs.ErrorLogViewer.csproj'
    $buildOutput = 'out/viewer'

    if (-not $Test) {
        Invoke-ProjectBuild -Context $context -ProjectPath $projectPath -OutputDirectory $buildOutput -FriendlyName 'Error Log Viewer' -TargetFramework 'net9.0-windows' -Force:$ForceBuild | Out-Null
    }
    else {
        Write-LauncherLog -Context $context -Message 'Skipping build in test mode.' -Level 'DEBUG'
    }

    $candidatePaths = @()
    if ($ViewerPath) {
        $candidatePaths += $ViewerPath
    }
    $candidatePaths += @(
        'out/viewer/GGs.ErrorLogViewer.exe',
        'tools/GGs.ErrorLogViewer/bin/Release/net9.0-windows/GGs.ErrorLogViewer.exe'
    )

    $exePath = if ($Test) {
        'powershell'
    }
    else {
        Resolve-ExecutablePath -Context $context -CandidatePaths $candidatePaths -FriendlyName 'GGs Error Log Viewer'
    }

    Stop-ProcessTree -Context $context -ProcessNames @('GGs.ErrorLogViewer')

    if ($Test) {
        $process = New-TestProcess -Context $context -DurationSeconds $TestDurationSeconds -DisplayName 'Error Log Viewer simulation'
    }
    else {
        $process = Start-ManagedProcess -Context $context -FilePath $exePath
        if (-not (Wait-ForProcessStart -Process $process -TimeoutSeconds 15)) {
            throw 'GGs.ErrorLogViewer did not start successfully within the expected time window.'
        }
    }

    Write-LauncherLog -Context $context -Message 'Viewer started successfully. Monitoring runtime...'
    $exitCode = Monitor-Process -Context $context -Process $process -StatusLabel 'Viewer uptime'

    $runtime = (Get-Date) - $context.StartedAt
    Write-LauncherLog -Context $context -Message "Viewer closed after $($runtime.ToString('hh\:mm\:ss')). Exit code: $exitCode" -Level 'INFO'
    exit $exitCode
}
catch {
    Write-LauncherLog -Context $context -Message $_.Exception.Message -Level 'ERROR'
    Write-LauncherLog -Context $context -Message 'Troubleshooting tips: verify the executable path, ensure dependencies are installed, and review the log for details.' -Level 'ERROR'
    exit 1
}
