param(
    [switch]$ForceBuild,
    [switch]$ForcePort,
    [switch]$Test,
    [int]$TestDurationSeconds = 5,
    [int]$ServerPort = 5000
)

Import-Module (Join-Path $PSScriptRoot 'LauncherCore.psm1') -Force

$root = Resolve-Path -Path (Join-Path $PSScriptRoot '..' '..')
$context = New-LauncherContext -ComponentName 'Launch-All-New' -BaseDirectory $root -TestMode:$Test

Write-LauncherLog -Context $context -Message 'GGs Launcher Suite orchestrator initializing.'

$componentSpecs = @(
    [pscustomobject]@{
        Name = 'Server'
        ProcessNames = @('GGs.Server')
        Project = 'server/GGs.Server/GGs.Server.csproj'
        Output = 'out/server'
        Executable = 'out/server/GGs.Server.exe'
        Arguments = { param($port) @('--urls', "http://localhost:$port") }
        Start = {
            param($component,$ForceBuild,$ForcePort,$ServerPort,$Test,$TestDurationSeconds)
            $ctx = $component.Context
            $spec = $component.Spec
            if (-not $Test) {
                Ensure-DotNetSdk -Context $ctx | Out-Null
                Ensure-PortAvailable -Context $ctx -Port $ServerPort -Force:$ForcePort
                Invoke-ProjectBuild -Context $ctx -ProjectPath $spec.Project -OutputDirectory $spec.Output -FriendlyName 'GGs Server' -Force:$ForceBuild | Out-Null
                $exePath = Resolve-ExecutablePath -Context $ctx -CandidatePaths @($spec.Executable) -FriendlyName 'GGs Server'
                $args = & $spec.Arguments $ServerPort
                $proc = Start-ManagedProcess -Context $ctx -FilePath $exePath -Arguments $args -NoWindow
                if (-not (Wait-ForProcessStart -Process $proc -TimeoutSeconds 25)) {
                    throw 'GGs Server failed to report ready state in time.'
                }
                Write-LauncherLog -Context $ctx -Message "Server running on http://localhost:$ServerPort" -Level 'INFO'
                return $proc
            }
            else {
                return New-TestProcess -Context $ctx -DurationSeconds $TestDurationSeconds -DisplayName 'Server simulation'
            }
        }
        Monitor = {
            param($component,$ServerPort)
            $portOpen = $false
            try {
                $portOpen = Test-NetConnection -ComputerName 'localhost' -Port $ServerPort -InformationLevel Quiet -WarningAction SilentlyContinue
            }
            catch { $portOpen = $false }
            if ($portOpen) {
                $component.Status = 'Online'
                $component.StatusDetails = "Listening on $ServerPort"
            }
            else {
                $component.Status = 'Starting'
                $component.StatusDetails = 'Awaiting port'
            }
        }
    }
    [pscustomobject]@{
        Name = 'Desktop'
        ProcessNames = @('GGs.Desktop')
        Project = 'clients/GGs.Desktop/GGs.Desktop.csproj'
        Output = 'out/desktop'
        Executable = 'out/desktop/GGs.Desktop.exe'
        Arguments = { @() }
        Start = {
            param($component,$ForceBuild,$ForcePort,$ServerPort,$Test,$TestDurationSeconds)
            $ctx = $component.Context
            $spec = $component.Spec
            if (-not $Test) {
                Invoke-ProjectBuild -Context $ctx -ProjectPath $spec.Project -OutputDirectory $spec.Output -FriendlyName 'GGs Desktop' -Force:$ForceBuild | Out-Null
                $exePath = Resolve-ExecutablePath -Context $ctx -CandidatePaths @($spec.Executable) -FriendlyName 'GGs Desktop'
                Stop-ProcessTree -Context $ctx -ProcessNames $spec.ProcessNames
                $proc = Start-ManagedProcess -Context $ctx -FilePath $exePath
                if (-not (Wait-ForProcessStart -Process $proc -TimeoutSeconds 20)) {
                    throw 'GGs Desktop did not report ready state in time.'
                }
                return $proc
            }
            else {
                return New-TestProcess -Context $ctx -DurationSeconds $TestDurationSeconds -DisplayName 'Desktop simulation'
            }
        }
        Monitor = {
            param($component,$ServerPort)
            $component.Status = 'Running'
            $component.StatusDetails = 'GUI responsive'
        }
    }
    [pscustomobject]@{
        Name = 'Viewer'
        ProcessNames = @('GGs.ErrorLogViewer')
        Project = 'tools/GGs.ErrorLogViewer/GGs.ErrorLogViewer.csproj'
        Output = 'out/viewer'
        Executable = 'out/viewer/GGs.ErrorLogViewer.exe'
        Arguments = { @() }
        Start = {
            param($component,$ForceBuild,$ForcePort,$ServerPort,$Test,$TestDurationSeconds)
            $ctx = $component.Context
            $spec = $component.Spec
            if (-not $Test) {
                Invoke-ProjectBuild -Context $ctx -ProjectPath $spec.Project -OutputDirectory $spec.Output -FriendlyName 'GGs Error Log Viewer' -TargetFramework 'net9.0-windows' -Force:$ForceBuild | Out-Null
                $exePath = Resolve-ExecutablePath -Context $ctx -CandidatePaths @($spec.Executable) -FriendlyName 'GGs Error Log Viewer'
                Stop-ProcessTree -Context $ctx -ProcessNames $spec.ProcessNames
                $proc = Start-ManagedProcess -Context $ctx -FilePath $exePath
                if (-not (Wait-ForProcessStart -Process $proc -TimeoutSeconds 15)) {
                    throw 'Error Log Viewer did not report ready state in time.'
                }
                return $proc
            }
            else {
                return New-TestProcess -Context $ctx -DurationSeconds $TestDurationSeconds -DisplayName 'Viewer simulation'
            }
        }
        Monitor = {
            param($component,$ServerPort)
            $component.Status = 'Running'
            $component.StatusDetails = 'Monitoring in real-time'
        }
    }
)

$components = @()
foreach ($spec in $componentSpecs) {
    $componentContext = New-LauncherContext -ComponentName "Launch-$($spec.Name)" -BaseDirectory $root -TestMode:$Test
    $component = [pscustomobject]@{
        Name = $spec.Name
        Context = $componentContext
        Process = $null
        Status = 'Pending'
        StatusDetails = 'Awaiting start'
        Started = $null
        Uptime = $null
        Spec = $spec
        Stopping = $false
    }

    try {
        Write-LauncherLog -Context $context -Message "Starting $($component.Name) component." -Level 'INFO'
        $component.Process = & $spec.Start $component $ForceBuild $ForcePort $ServerPort $Test $TestDurationSeconds
        $component.Started = Get-Date
        $component.Status = 'Running'
        $component.StatusDetails = 'Initializing'
        Write-LauncherLog -Context $componentContext -Message "$($component.Name) started successfully." -Level 'SUCCESS'
    }
    catch {
        $component.Status = 'Failed'
        $component.StatusDetails = $_.Exception.Message
        Write-LauncherLog -Context $componentContext -Message $_.Exception.Message -Level 'ERROR'
        Write-LauncherLog -Context $context -Message "Failed to start $($component.Name) but continuing launch sequence." -Level 'WARN'
    }

    $components += $component
    Start-Sleep -Seconds 2
}

function Update-ComponentState {
    param($component,$ServerPort)

    if (-not $component.Process) { return }
    if ($component.Process.HasExited) {
        $component.Status = if ($component.Process.ExitCode -eq 0) { 'Stopped' } else { 'Error' }
        $component.StatusDetails = "Exit code $($component.Process.ExitCode)"
        if (-not $component.Uptime) { $component.Uptime = (Get-Date) - $component.Started }
        return
    }

    $component.Uptime = (Get-Date) - $component.Started
    & $component.Spec.Monitor $component $ServerPort
}

function Render-Dashboard {
    param($components,$context)
    Clear-Host
    Write-Host '=== GGs Launcher Suite Dashboard ==='
    Write-Host
    Write-Host ("{0,-18} {1,-10} {2,-10} {3}" -f 'Component','Status','Uptime','Details')
    Write-Host ('-' * 70)
    foreach ($component in $components) {
        $line = Format-StatusLine -Name $component.Name -State $component.Status -Uptime $component.Uptime -Details $component.StatusDetails
        Write-Host $line
    }
    Write-Host ('-' * 70)
    Write-Host 'Controls: [R]efresh  [L] View logs  [Q]uit all'
    Write-Host "Logs directory: $($context.LogDirectory)"
}

function Show-LogsMenu {
    param($components,$context)
    Write-Host 'Select component log to open:'
    for ($i = 0; $i -lt $components.Count; $i++) {
        Write-Host "  [$($i+1)] $($components[$i].Name) - $($components[$i].Context.LogFile)"
    }
    Write-Host '  [A] Orchestrator log'
    Write-Host '  [X] Cancel'
    $choice = Read-Host 'Choice'
    switch ($choice.ToUpperInvariant()) {
        'X' { return }
        'A' { Start-Process -FilePath 'notepad' -ArgumentList $context.LogFile | Out-Null; return }
    }
    $parsed = 0
    if ([int]::TryParse($choice, [ref]$parsed)) {
        $index = $parsed - 1
        if ($index -ge 0 -and $index -lt $components.Count) {
            Start-Process -FilePath 'notepad' -ArgumentList $components[$index].Context.LogFile | Out-Null
        }
    }
}

function Stop-AllComponents {
    param($components)
    foreach ($component in $components) {
        if ($component.Process -and -not $component.Process.HasExited) {
            try {
                $component.Stopping = $true
                Write-LauncherLog -Context $component.Context -Message 'Requesting shutdown.' -Level 'WARN'
                $component.Process.CloseMainWindow() | Out-Null
                Start-Sleep -Seconds 2
                if (-not $component.Process.HasExited) {
                    $component.Process.Kill()
                    $component.Process.WaitForExit(5000) | Out-Null
                }
                $component.Status = 'Stopped'
                $component.StatusDetails = 'Terminated by orchestrator'
            }
            catch {
                Write-LauncherLog -Context $component.Context -Message "Failed to stop process: $($_.Exception.Message)" -Level 'ERROR'
            }
        }
    }
}

try {
    $allComplete = $false
    Render-Dashboard -components $components -context $context
    while (-not $allComplete) {
        foreach ($component in $components) {
            Update-ComponentState -component $component -ServerPort $ServerPort
        }

        if ([Console]::KeyAvailable) {
            $key = [Console]::ReadKey($true)
            switch ($key.Key) {
                'Q' {
                    Write-LauncherLog -Context $context -Message 'Operator requested shutdown. Stopping all components.' -Level 'WARN'
                    Stop-AllComponents -components $components
                }
                'L' { Show-LogsMenu -components $components -context $context }
                'R' { Render-Dashboard -components $components -context $context }
            }
        }

        Render-Dashboard -components $components -context $context

        $running = $components | Where-Object { $_.Process -and -not $_.Process.HasExited }
        if ($running.Count -eq 0) {
            $allComplete = $true
        }
        else {
            Start-Sleep -Seconds 1
        }
    }
}
finally {
    Render-Dashboard -components $components -context $context
}

$failed = $components | Where-Object { $_.Status -in @('Failed','Error') }
if ($failed.Count -gt 0) {
    Write-LauncherLog -Context $context -Message "Launcher suite completed with errors in: $(($failed | Select-Object -ExpandProperty Name) -join ', ')." -Level 'ERROR'
    exit 1
}

Write-LauncherLog -Context $context -Message 'Launcher suite completed successfully.' -Level 'SUCCESS'
exit 0
