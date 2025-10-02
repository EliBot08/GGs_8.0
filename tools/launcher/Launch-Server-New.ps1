param(
    [switch]$ForceBuild,
    [switch]$ForcePort,
    [switch]$Test,
    [int]$TestDurationSeconds = 5,
    [int]$Port = 5000
)

Import-Module (Join-Path $PSScriptRoot 'LauncherCore.psm1') -Force

$root = Resolve-Path -Path (Join-Path $PSScriptRoot '..' '..')
$context = New-LauncherContext -ComponentName 'Launch-Server-New' -BaseDirectory $root -TestMode:$Test

function Monitor-Server {
    param(
        [psobject]$Context,
        [System.Diagnostics.Process]$Process,
        [int]$Port
    )

    $start = Get-Date
    while (-not $Process.HasExited) {
        $uptime = (Get-Date) - $start
        $portOpen = $false
        try {
            $portOpen = Test-NetConnection -ComputerName 'localhost' -Port $Port -WarningAction SilentlyContinue -InformationLevel Quiet
        }
        catch {
            $portOpen = $false
        }
        $status = if ($portOpen) { 'Listening' } else { 'Probing' }
        $msg = "Uptime {0:hh\:mm\:ss} | Port {1}: {2}" -f $uptime, $Port, $status
        Write-Progress -Activity 'GGs Server' -Status $msg -PercentComplete -1
        Start-Sleep -Seconds 1
    }
    Write-Progress -Activity 'GGs Server' -Completed -Status 'Server stopped'
    return $Process.ExitCode
}

try {
    Write-LauncherLog -Context $context -Message 'Initializing GGs Server launcher.'

    Ensure-DotNetSdk -Context $context | Out-Null

    if (-not $Test) {
        Ensure-PortAvailable -Context $context -Port $Port -Force:$ForcePort
    }
    else {
        Write-LauncherLog -Context $context -Message 'Skipping port management in test mode.' -Level 'DEBUG'
    }

    $projectPath = 'server/GGs.Server/GGs.Server.csproj'
    $buildOutput = 'out/server'

    if (-not $Test) {
        Invoke-ProjectBuild -Context $context -ProjectPath $projectPath -OutputDirectory $buildOutput -FriendlyName 'GGs Server' -Force:$ForceBuild | Out-Null
    }
    else {
        Write-LauncherLog -Context $context -Message 'Skipping server build in test mode.' -Level 'DEBUG'
    }

    if ($Test) {
        $process = New-TestProcess -Context $context -DurationSeconds $TestDurationSeconds -DisplayName 'Server simulation'
    }
    else {
        $exePath = Resolve-ExecutablePath -Context $context -CandidatePaths @('out/server/GGs.Server.exe') -FriendlyName 'GGs Server service'
        $arguments = @('--urls', "http://localhost:$Port")
        $process = Start-ManagedProcess -Context $context -FilePath $exePath -Arguments $arguments -NoWindow
        Write-LauncherLog -Context $context -Message 'Server launched in background mode (/B equivalent).' -Level 'INFO'
        if (-not (Wait-ForProcessStart -Process $process -TimeoutSeconds 25)) {
            throw 'GGs.Server failed to start within the expected timeframe.'
        }
    }

    Write-LauncherLog -Context $context -Message 'Server started successfully. Monitoring health...'
    $exitCode = Monitor-Server -Context $context -Process $process -Port $Port
    $runtime = (Get-Date) - $context.StartedAt
    Write-LauncherLog -Context $context -Message "Server process exited after $($runtime.ToString('hh\:mm\:ss')). Exit code: $exitCode" -Level 'INFO'
    exit $exitCode
}
catch {
    Write-LauncherLog -Context $context -Message $_.Exception.Message -Level 'ERROR'
    Write-LauncherLog -Context $context -Message 'Troubleshooting: confirm .NET SDK 9.0+, inspect port usage, and review build output.' -Level 'ERROR'
    exit 1
}
