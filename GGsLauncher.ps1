<#
.SYNOPSIS
    GGs Enterprise Application Launcher
    
.DESCRIPTION
    Enterprise-level launcher for GGs Desktop and ErrorLogViewer applications.
    Features comprehensive error handling, logging, dependency checks, and process monitoring.
    
.PARAMETER Desktop
    Launch only GGs Desktop application
    
.PARAMETER LogViewer
    Launch only ErrorLogViewer application
    
.PARAMETER NoLogViewer
    Launch GGs Desktop without ErrorLogViewer
    
.PARAMETER LogDirectory
    Specify custom log directory for ErrorLogViewer
    
.PARAMETER Configuration
    Build configuration (Debug/Release). Default: Release
    
.PARAMETER SkipBuild
    Skip building applications before launch
    
.PARAMETER Verbose
    Enable verbose logging output
    
.EXAMPLE
    .\GGsLauncher.ps1
    Launches both GGs Desktop and ErrorLogViewer
    
.EXAMPLE
    .\GGsLauncher.ps1 -Desktop
    Launches only GGs Desktop
    
.EXAMPLE
    .\GGsLauncher.ps1 -LogDirectory "C:\CustomLogs"
    Launches with custom log directory
#>

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$Desktop,
    
    [Parameter()]
    [switch]$LogViewer,
    
    [Parameter()]
    [switch]$NoLogViewer,
    
    [Parameter()]
    [string]$LogDirectory,
    
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [Parameter()]
    [switch]$SkipBuild,
    
    [Parameter()]
    [switch]$VerboseLogging
)

# Enterprise-grade error handling
$ErrorActionPreference = 'Stop'
$script:LaunchErrors = @()
$script:LaunchWarnings = @()
$script:StartTime = Get-Date

#region Logging Functions

function Write-LauncherLog {
    param(
        [Parameter(Mandatory)]
        [string]$Message,
        
        [Parameter()]
        [ValidateSet('Info', 'Success', 'Warning', 'Error', 'Debug')]
        [string]$Level = 'Info'
    )
    
    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'
    $logMessage = "[$timestamp] [$Level] $Message"
    
    # Color coding
    $color = switch ($Level) {
        'Success' { 'Green' }
        'Warning' { 'Yellow' }
        'Error' { 'Red' }
        'Debug' { 'Gray' }
        default { 'White' }
    }
    
    Write-Host $logMessage -ForegroundColor $color
    
    # Log to file
    $logDir = Join-Path $PSScriptRoot 'launcher-logs'
    if (!(Test-Path $logDir)) {
        New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    }
    
    $logFile = Join-Path $logDir "launcher-$(Get-Date -Format 'yyyyMMdd').log"
    $logMessage | Out-File -FilePath $logFile -Append -Encoding UTF8
    
    # Track errors and warnings
    if ($Level -eq 'Error') {
        $script:LaunchErrors += $Message
    } elseif ($Level -eq 'Warning') {
        $script:LaunchWarnings += $Message
    }
}

function Write-Header {
    param([string]$Text)
    
    Write-Host "`n" -NoNewline
    Write-Host ('='*80) -ForegroundColor Cyan
    Write-Host " $Text" -ForegroundColor Cyan
    Write-Host ('='*80) -ForegroundColor Cyan
}

function Write-SubHeader {
    param([string]$Text)
    
    Write-Host "`n" -NoNewline
    Write-Host ('-'*80) -ForegroundColor DarkCyan
    Write-Host " $Text" -ForegroundColor DarkCyan
    Write-Host ('-'*80) -ForegroundColor DarkCyan
}

#endregion

#region Environment Checks

function Test-Prerequisites {
    Write-SubHeader "Checking Prerequisites"
    
    $allGood = $true
    
    # Check .NET SDK
    Write-LauncherLog "Checking .NET SDK..." -Level Debug
    try {
        $dotnetVersion = & dotnet --version 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-LauncherLog ".NET SDK found: v$dotnetVersion" -Level Success
        } else {
            Write-LauncherLog ".NET SDK not found" -Level Error
            $allGood = $false
        }
    } catch {
        Write-LauncherLog ".NET SDK check failed: $_" -Level Error
        $allGood = $false
    }
    
    # Check solution file
    $solutionPath = Join-Path $PSScriptRoot 'GGs.sln'
    if (Test-Path $solutionPath) {
        Write-LauncherLog "Solution file found: GGs.sln" -Level Success
    } else {
        Write-LauncherLog "Solution file not found: GGs.sln" -Level Error
        $allGood = $false
    }
    
    # Check GGs.Desktop project
    $desktopProject = Join-Path $PSScriptRoot 'clients\GGs.Desktop\GGs.Desktop.csproj'
    if (Test-Path $desktopProject) {
        Write-LauncherLog "GGs.Desktop project found" -Level Success
    } else {
        Write-LauncherLog "GGs.Desktop project not found" -Level Error
        $allGood = $false
    }
    
    # Check ErrorLogViewer project
    $logViewerProject = Join-Path $PSScriptRoot 'tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj'
    if (Test-Path $logViewerProject) {
        Write-LauncherLog "ErrorLogViewer project found" -Level Success
    } else {
        Write-LauncherLog "ErrorLogViewer project not found" -Level Warning
    }
    
    if (-not $allGood) {
        throw "Prerequisites check failed. Please ensure .NET SDK is installed and project files exist."
    }
    
    return $allGood
}

#endregion

#region Build Functions

function Invoke-Build {
    param(
        [string]$ProjectPath,
        [string]$ProjectName
    )
    
    Write-LauncherLog "Building $ProjectName ($Configuration)..." -Level Info
    
    try {
        $buildOutput = & dotnet build $ProjectPath -c $Configuration --nologo 2>&1
        
        if ($LASTEXITCODE -eq 0) {
            Write-LauncherLog "$ProjectName built successfully" -Level Success
            return $true
        } else {
            Write-LauncherLog "$ProjectName build failed with exit code $LASTEXITCODE" -Level Error
            Write-LauncherLog "Build output: $buildOutput" -Level Debug
            return $false
        }
    } catch {
        Write-LauncherLog "$ProjectName build exception: $_" -Level Error
        return $false
    }
}

function Build-Applications {
    Write-SubHeader "Building Applications"
    
    $success = $true
    
    if ($Desktop -or -not ($LogViewer -or $NoLogViewer)) {
        $desktopProject = Join-Path $PSScriptRoot 'clients\GGs.Desktop\GGs.Desktop.csproj'
        if (-not (Invoke-Build -ProjectPath $desktopProject -ProjectName "GGs.Desktop")) {
            $success = $false
        }
    }
    
    if ($LogViewer -or (-not ($Desktop -or $NoLogViewer))) {
        $logViewerProject = Join-Path $PSScriptRoot 'tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj'
        if (Test-Path $logViewerProject) {
            if (-not (Invoke-Build -ProjectPath $logViewerProject -ProjectName "ErrorLogViewer")) {
                $success = $false
            }
        }
    }
    
    if (-not $success) {
        throw "One or more builds failed. Check log for details."
    }
}

#endregion

#region Launch Functions

function Start-GGsDesktop {
    Write-SubHeader "Launching GGs Desktop"
    
    $exePath = Join-Path $PSScriptRoot "clients\GGs.Desktop\bin\$Configuration\net9.0-windows\GGs.Desktop.exe"
    
    if (-not (Test-Path $exePath)) {
        Write-LauncherLog "GGs.Desktop executable not found: $exePath" -Level Error
        Write-LauncherLog "Please ensure the application is built successfully" -Level Error
        return $null
    }
    
    try {
        Write-LauncherLog "Starting GGs Desktop..." -Level Info
        $process = Start-Process -FilePath $exePath -PassThru -WindowStyle Normal
        
        # Wait briefly to check if process started successfully
        Start-Sleep -Milliseconds 500
        
        if ($process.HasExited) {
            Write-LauncherLog "GGs Desktop exited immediately (Exit Code: $($process.ExitCode))" -Level Error
            return $null
        }
        
        Write-LauncherLog "GGs Desktop started successfully (PID: $($process.Id))" -Level Success
        return $process
    } catch {
        Write-LauncherLog "Failed to start GGs Desktop: $_" -Level Error
        return $null
    }
}

function Start-ErrorLogViewer {
    param([string]$CustomLogDir)

    Write-SubHeader "Launching ErrorLogViewer"

    $exePath = Join-Path $PSScriptRoot "tools\GGs.ErrorLogViewer\bin\$Configuration\net9.0-windows\GGs.ErrorLogViewer.exe"

    if (-not (Test-Path $exePath)) {
        Write-LauncherLog "ErrorLogViewer executable not found: $exePath" -Level Warning
        Write-LauncherLog "Continuing without ErrorLogViewer" -Level Warning
        return $null
    }

    # Reuse existing ErrorLogViewer instance if it is already running
    try {
        $existing = Get-Process -Name "GGs.ErrorLogViewer" -ErrorAction SilentlyContinue
    } catch {
        $existing = $null
    }

    if ($existing) {
        $pidList = ($existing | Select-Object -ExpandProperty Id) -join ', '
        Write-LauncherLog "ErrorLogViewer already running (PID(s): $pidList). Reusing existing instance." -Level Warning
        try {
            Add-Type -AssemblyName Microsoft.VisualBasic -ErrorAction Stop
            [Microsoft.VisualBasic.Interaction]::AppActivate($existing[0].Id) | Out-Null
            Write-LauncherLog "Brought existing ErrorLogViewer window to the foreground" -Level Success
        } catch {
            Write-LauncherLog "Unable to focus existing ErrorLogViewer window: $_" -Level Debug
        }
        return $existing[0]
    }

    try {
        Write-LauncherLog "Starting ErrorLogViewer..." -Level Info

        if ($CustomLogDir) {
            Write-LauncherLog "Using custom log directory: $CustomLogDir" -Level Info
            $process = Start-Process -FilePath $exePath -ArgumentList @("--log-dir", $CustomLogDir) -PassThru -WindowStyle Normal
        } else {
            $process = Start-Process -FilePath $exePath -PassThru -WindowStyle Normal
        }
        
        # Wait briefly to check if process started successfully
        Start-Sleep -Milliseconds 500
        
        if ($process.HasExited) {
            Write-LauncherLog "ErrorLogViewer exited immediately (Exit Code: $($process.ExitCode))" -Level Warning
            return $null
        }
        
        Write-LauncherLog "ErrorLogViewer started successfully (PID: $($process.Id))" -Level Success
        return $process
    } catch {
        Write-LauncherLog "Failed to start ErrorLogViewer: $_" -Level Warning
        return $null
    }
}

#endregion

#region Process Monitoring

function Watch-Processes {
    param(
        [System.Diagnostics.Process]$DesktopProcess,
        [System.Diagnostics.Process]$LogViewerProcess
    )
    
    Write-SubHeader "Process Monitoring Active"
    Write-LauncherLog "Press Ctrl+C to stop monitoring and exit applications" -Level Info
    Write-Host ""
    
    try {
        while ($true) {
            $desktopRunning = $DesktopProcess -and -not $DesktopProcess.HasExited
            $logViewerRunning = $LogViewerProcess -and -not $LogViewerProcess.HasExited
            
            if (-not $desktopRunning -and -not $logViewerRunning) {
                Write-LauncherLog "All monitored processes have exited" -Level Info
                break
            }
            
            if ($DesktopProcess -and -not $desktopRunning) {
                Write-LauncherLog "GGs Desktop has exited (Exit Code: $($DesktopProcess.ExitCode))" -Level Warning
                $DesktopProcess = $null
            }
            
            if ($LogViewerProcess -and -not $logViewerRunning) {
                Write-LauncherLog "ErrorLogViewer has exited (Exit Code: $($LogViewerProcess.ExitCode))" -Level Warning
                $LogViewerProcess = $null
            }
            
            Start-Sleep -Seconds 2
        }
    } catch {
        Write-LauncherLog "Process monitoring interrupted" -Level Info
    }
}

#endregion

#region Cleanup

function Stop-Applications {
    param(
        [System.Diagnostics.Process]$DesktopProcess,
        [System.Diagnostics.Process]$LogViewerProcess
    )
    
    Write-SubHeader "Shutting Down Applications"
    
    if ($DesktopProcess -and -not $DesktopProcess.HasExited) {
        Write-LauncherLog "Stopping GGs Desktop..." -Level Info
        try {
            $DesktopProcess.CloseMainWindow() | Out-Null
            if (-not $DesktopProcess.WaitForExit(5000)) {
                Write-LauncherLog "Force killing GGs Desktop..." -Level Warning
                $DesktopProcess.Kill()
            }
            Write-LauncherLog "GGs Desktop stopped" -Level Success
        } catch {
            Write-LauncherLog "Error stopping GGs Desktop: $_" -Level Error
        }
    }
    
    if ($LogViewerProcess -and -not $LogViewerProcess.HasExited) {
        Write-LauncherLog "Stopping ErrorLogViewer..." -Level Info
        try {
            $LogViewerProcess.CloseMainWindow() | Out-Null
            if (-not $LogViewerProcess.WaitForExit(5000)) {
                Write-LauncherLog "Force killing ErrorLogViewer..." -Level Warning
                $LogViewerProcess.Kill()
            }
            Write-LauncherLog "ErrorLogViewer stopped" -Level Success
        } catch {
            Write-LauncherLog "Error stopping ErrorLogViewer: $_" -Level Error
        }
    }
}

#endregion

#region Main Execution

function Show-Summary {
    $duration = (Get-Date) - $script:StartTime
    
    Write-SubHeader "Launch Summary"
    
    Write-Host "Duration: " -NoNewline
    Write-Host "$($duration.TotalSeconds.ToString('F2')) seconds" -ForegroundColor Cyan
    
    if ($script:LaunchErrors.Count -gt 0) {
        Write-Host "`nErrors ($($script:LaunchErrors.Count)):" -ForegroundColor Red
        foreach ($error in $script:LaunchErrors) {
            Write-Host "  - $error" -ForegroundColor Red
        }
    }
    
    if ($script:LaunchWarnings.Count -gt 0) {
        Write-Host "`nWarnings ($($script:LaunchWarnings.Count)):" -ForegroundColor Yellow
        foreach ($warning in $script:LaunchWarnings) {
            Write-Host "  - $warning" -ForegroundColor Yellow
        }
    }
    
    if ($script:LaunchErrors.Count -eq 0 -and $script:LaunchWarnings.Count -eq 0) {
        Write-Host "`nStatus: " -NoNewline
        Write-Host "All systems operational" -ForegroundColor Green
    }
    
    Write-Host ""
}

try {
    Write-Header "GGs Enterprise Application Launcher v1.0"
    
    Write-LauncherLog "Launcher started with configuration: $Configuration" -Level Info
    
    # Step 1: Prerequisites check
    Test-Prerequisites
    
    # Step 2: Build applications (if not skipped)
    if (-not $SkipBuild) {
        Build-Applications
    } else {
        Write-LauncherLog "Skipping build (SkipBuild flag enabled)" -Level Warning
    }
    
    # Step 3: Launch applications
    $desktopProcess = $null
    $logViewerProcess = $null
    
    if ($LogViewer) {
        # Launch only LogViewer
        $logViewerProcess = Start-ErrorLogViewer -CustomLogDir $LogDirectory
    } elseif ($Desktop) {
        # Launch only Desktop
        $desktopProcess = Start-GGsDesktop
    } else {
        # Launch both (default)
        if (-not $NoLogViewer) {
            $logViewerProcess = Start-ErrorLogViewer -CustomLogDir $LogDirectory
            Start-Sleep -Milliseconds 1000  # Stagger launches
        }
        
        $desktopProcess = Start-GGsDesktop
    }
    
    # Step 4: Monitor processes
    if ($desktopProcess -or $logViewerProcess) {
        Watch-Processes -DesktopProcess $desktopProcess -LogViewerProcess $logViewerProcess
    } else {
        Write-LauncherLog "No processes were started successfully" -Level Error
        exit 1
    }
    
} catch {
    Write-LauncherLog "Launcher failed: $_" -Level Error
    Write-LauncherLog "Stack Trace: $($_.ScriptStackTrace)" -Level Debug
    exit 1
} finally {
    Stop-Applications -DesktopProcess $desktopProcess -LogViewerProcess $logViewerProcess
    Show-Summary
}

#endregion
