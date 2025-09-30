# Enterprise-Grade GGs Application Launcher
# Version: 2.0.0 - Production Ready
# Features: Comprehensive error handling, logging, health checks, recovery mechanisms

$ErrorActionPreference = "Stop"
$global:LaunchTimestamp = Get-Date -Format "yyyy-MM-dd_HH-mm-ss"

# Resolve repository root relative to this script (portable paths)
$global:RepoRoot = $PSScriptRoot
$global:ServerProjectPath = Join-Path $global:RepoRoot "server\GGs.Server"
$global:DesktopProjectPath = Join-Path $global:RepoRoot "clients\GGs.Desktop"
$global:ErrorLogViewerPath = Join-Path $global:RepoRoot "tools\GGs.ErrorLogViewer"

# Log directory under current user's profile (no admin-specific hard-coding)
$global:GlobalLogDir = Join-Path $env:LOCALAPPDATA "GGs\logs"
$global:LogFile = Join-Path $global:GlobalLogDir "launch_$LaunchTimestamp.log"
$global:MaxRetryAttempts = 3
$global:RetryDelaySeconds = 2

# Create logs directory if it doesn't exist
if (!(Test-Path $global:GlobalLogDir)) {
    New-Item -ItemType Directory -Path $global:GlobalLogDir -Force | Out-Null
}
# Ensure subfolders
$serverLogDir = Join-Path $global:GlobalLogDir "server"
$desktopLogDir = $global:GlobalLogDir # desktop writes here via env var
New-Item -ItemType Directory -Path $serverLogDir -Force | Out-Null
New-Item -ItemType Directory -Path $desktopLogDir -Force | Out-Null

# Export log dir for child processes (desktop picks this up)
$env:GGS_LOG_DIR = $global:GlobalLogDir

# Enhanced logging function
function Write-LogEntry {
    param(
        [Parameter(Mandatory=$true)]
        [string]$Message,
        [Parameter(Mandatory=$false)]
        [ValidateSet("INFO", "WARNING", "ERROR", "SUCCESS", "DEBUG")]
        [string]$Level = "INFO"
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    $logMessage = "[$timestamp] [$Level] $Message"
    
    # Write to console with color
    switch ($Level) {
        "ERROR" { Write-Host $logMessage -ForegroundColor Red }
        "WARNING" { Write-Host $logMessage -ForegroundColor Yellow }
        "SUCCESS" { Write-Host $logMessage -ForegroundColor Green }
        "DEBUG" { Write-Host $logMessage -ForegroundColor Gray }
        default { Write-Host $logMessage }
    }
    
    # Write to log file
    Add-Content -Path $global:LogFile -Value $logMessage -Force
}

# Start ErrorLogViewer for enterprise monitoring
function Start-ErrorLogViewer {
    Write-LogEntry "Starting ErrorLogViewer for enterprise monitoring..." -Level INFO
    
    try {
        $errorLogViewerPath = $global:ErrorLogViewerPath
        if (!(Test-Path $errorLogViewerPath)) {
            Write-LogEntry "ErrorLogViewer path not found: $errorLogViewerPath" -Level WARNING
            return $null
        }
        
        # Check if already built
        $exePath = "$errorLogViewerPath\bin\Release\net8.0-windows\GGs.ErrorLogViewer.exe"
        if (!(Test-Path $exePath)) {
            # Build ErrorLogViewer with enhanced error handling
            Write-LogEntry "Building ErrorLogViewer..." -Level INFO
            $buildResult = Build-Application -Path $errorLogViewerPath -ProjectName "GGs.ErrorLogViewer"
            if (!$buildResult) {
                Write-LogEntry "ErrorLogViewer build failed, but continuing without it" -Level WARNING
                Write-LogEntry "This is not critical - the main GGs application will still work" -Level INFO
                return $null
            }
        }
        
        # Verify executable exists
        if (!(Test-Path $exePath)) {
            Write-LogEntry "ErrorLogViewer executable not found" -Level WARNING
            return $null
        }
        
        Write-LogEntry "Starting ErrorLogViewer from: $exePath" -Level INFO
        
        # Set environment variables for ErrorLogViewer
        $env:GGS_LOG_DIR = $global:GlobalLogDir
        $env:DOTNET_ENVIRONMENT = "Production"
        
        # Prepare command line arguments
        $arguments = @("--log-dir", $global:GlobalLogDir)
        
        # Start the ErrorLogViewer application with log directory argument
        $errorLogViewerProcess = Start-Process -FilePath $exePath `
            -ArgumentList $arguments `
            -WorkingDirectory $errorLogViewerPath `
            -PassThru `
            -WindowStyle Normal
        
        Write-LogEntry "ErrorLogViewer started (PID: $($errorLogViewerProcess.Id)) with log directory: $global:GlobalLogDir" -Level SUCCESS
        
        # Give it a moment to initialize
        Start-Sleep -Seconds 2
        if ($errorLogViewerProcess.HasExited) {
            Write-LogEntry "ErrorLogViewer crashed immediately after startup" -Level WARNING
            return $null
        }
        
        return $errorLogViewerProcess
        
    } catch {
        Write-LogEntry "Failed to start ErrorLogViewer: $_" -Level WARNING
        return $null
    }
}

# System requirements check
function Test-SystemRequirements {
    Write-LogEntry "Checking system requirements..." -Level INFO
    
    # Check .NET SDK
    try {
        $dotnetVersion = dotnet --version 2>$null
        if ($dotnetVersion) {
            Write-LogEntry ".NET SDK version: $dotnetVersion" -Level SUCCESS
        } else {
            throw ".NET SDK not found"
        }
    } catch {
        Write-LogEntry ".NET SDK check failed: $_" -Level ERROR
        return $false
    }
    
    # Check available memory
    $memInfo = Get-CimInstance Win32_OperatingSystem
    $availableMemGB = [math]::Round($memInfo.FreePhysicalMemory / 1GB, 2)
    if ($availableMemGB -lt 0.5) {
        Write-LogEntry "Low memory warning: Only $availableMemGB GB available" -Level WARNING
    } else {
        Write-LogEntry "Available memory: $availableMemGB GB" -Level INFO
    }
    
    # Check disk space
    $drive = Get-PSDrive C
    $freeSpaceGB = [math]::Round($drive.Free / 1GB, 2)
    if ($freeSpaceGB -lt 1) {
        Write-LogEntry "Low disk space warning: Only $freeSpaceGB GB available" -Level WARNING
    } else {
        Write-LogEntry "Available disk space: $freeSpaceGB GB" -Level INFO
    }
    
    return $true
}

# Port availability check
function Test-PortAvailability {
    param([int]$Port)
    
    try {
        $connection = New-Object System.Net.Sockets.TcpClient
        $connection.Connect("localhost", $Port)
        $connection.Close()
        return $false  # Port is in use
    } catch {
        return $true   # Port is available
    }
}

# Kill existing processes
function Stop-ExistingProcesses {
    Write-LogEntry "Checking for existing GGs processes..." -Level INFO
    
    $processes = @("GGs.Desktop", "GGs.Server", "GGs.ErrorLogViewer", "dotnet")
    foreach ($proc in $processes) {
        $existing = Get-Process -Name $proc -ErrorAction SilentlyContinue
        if ($existing) {
            Write-LogEntry "Stopping existing $proc processes..." -Level WARNING
            $existing | Stop-Process -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 1
        }
    }
    
    # Clean up any hanging port listeners
    $portsToCheck = @(5112, 5001, 5000)
    foreach ($port in $portsToCheck) {
        if (!(Test-PortAvailability -Port $port)) {
            Write-LogEntry "Port $port is in use, attempting to free it..." -Level WARNING
            netsh http delete urlacl url=http://+:$port/ 2>$null | Out-Null
            Start-Sleep -Milliseconds 500
        }
    }
}

# Build application with retry logic
function Build-Application {
    param([string]$Path, [string]$ProjectName, [bool]$SkipClean = $false)
    
    Write-LogEntry "Building $ProjectName..." -Level INFO
    
    $retryCount = 0
    while ($retryCount -lt $global:MaxRetryAttempts) {
        try {
            Push-Location $Path
            
            # Only clean if explicitly requested (speeds up subsequent builds)
            if (!$SkipClean) {
                Write-LogEntry "Cleaning previous builds..." -Level DEBUG
                if (Test-Path "bin\Release") {
                    Remove-Item -Path "bin\Release" -Recurse -Force -ErrorAction SilentlyContinue
                }
                if (Test-Path "obj\Release") {
                    Remove-Item -Path "obj\Release" -Recurse -Force -ErrorAction SilentlyContinue
                }
            }
            
            # Build the project
            $buildResult = dotnet build -c Release --no-incremental 2>&1
            $buildExitCode = $LASTEXITCODE
            
            if ($buildExitCode -eq 0) {
                Write-LogEntry "$ProjectName built successfully" -Level SUCCESS
                Pop-Location
                return $true
            } else {
                throw "Build failed with exit code $buildExitCode : $buildResult"
            }
        } catch {
            $retryCount++
            Write-LogEntry "Build attempt $retryCount failed: $_" -Level ERROR
            
            if ($retryCount -lt $global:MaxRetryAttempts) {
                Write-LogEntry "Retrying in $global:RetryDelaySeconds seconds..." -Level INFO
                Start-Sleep -Seconds $global:RetryDelaySeconds
            } else {
                Pop-Location
                return $false
            }
        }
    }
    Pop-Location
    return $false
}

# Start server with health check
function Start-Server {
    Write-LogEntry "Starting GGs Server..." -Level INFO
    
    try {
        $serverPath = $global:ServerProjectPath
        if (!(Test-Path $serverPath)) {
            throw "Server path not found: $serverPath"
        }
        
        # Build server
        if (!(Build-Application -Path $serverPath -ProjectName "GGs.Server")) {
            throw "Failed to build server"
        }
        
        # Start server process
        $serverStdOut = Join-Path $serverLogDir "server_stdout_$($global:LaunchTimestamp).log"
        $serverStdErr = Join-Path $serverLogDir "server_stderr_$($global:LaunchTimestamp).log"
$serverProcess = Start-Process -FilePath "dotnet" `
            -ArgumentList "run", "-c", "Release", "--no-build" `
            -WorkingDirectory $serverPath `
            -WindowStyle Hidden `
            -PassThru `
            -RedirectStandardOutput $serverStdOut `
            -RedirectStandardError $serverStdErr
        
        Write-LogEntry "Server process started (PID: $($serverProcess.Id))" -Level INFO
        
        # Wait for server to be ready with health check
        $serverReady = $false
        $checkAttempts = 0
        $maxCheckAttempts = 30
        
        while (!$serverReady -and $checkAttempts -lt $maxCheckAttempts) {
            Start-Sleep -Seconds 1
            $checkAttempts++
            
            # Check if process is still running
            if ($serverProcess.HasExited) {
                $errorLog = Get-Content $serverStdErr -ErrorAction SilentlyContinue
                throw "Server process exited unexpectedly. Error: $errorLog"
            }
            
            # Check port availability
            if (!(Test-PortAvailability -Port 5112)) {
                Write-LogEntry "Server is responding on port 5112" -Level SUCCESS
                $serverReady = $true
                
                # Try a simple HTTP request to verify
                try {
                    $response = Invoke-WebRequest -Uri "http://localhost:5112/api/health" -TimeoutSec 2 -ErrorAction SilentlyContinue
                    Write-LogEntry "Server health check passed" -Level SUCCESS
                } catch {
                    Write-LogEntry "Server is running but health check endpoint not available (this is okay)" -Level INFO
                }
            }
        }
        
        if (!$serverReady) {
            throw "Server failed to start within timeout period"
        }
        
        return $serverProcess
        
    } catch {
        Write-LogEntry "Failed to start server: $_" -Level ERROR
        return $null
    }
}

# Start desktop client with recovery
function Start-DesktopClient {
    Write-LogEntry "Starting GGs Desktop Client..." -Level INFO
    
    try {
        $desktopPath = $global:DesktopProjectPath
        if (!(Test-Path $desktopPath)) {
            throw "Desktop client path not found: $desktopPath"
        }
        
        # Build desktop client
        if (!(Build-Application -Path $desktopPath -ProjectName "GGs.Desktop")) {
            throw "Failed to build desktop client"
        }
        
        # Find the executable
        $exePath = "$desktopPath\bin\Release\net8.0-windows\GGs.Desktop.exe"
        if (!(Test-Path $exePath)) {
            # Try alternate path
            $exePath = "$desktopPath\bin\Release\net8.0-windows\win-x64\GGs.Desktop.exe"
            if (!(Test-Path $exePath)) {
                throw "Desktop executable not found after build"
            }
        }
        
        Write-LogEntry "Starting desktop client from: $exePath" -Level INFO
        
        # Set environment variables
        $env:ASPNETCORE_ENVIRONMENT = "Production"
        $env:GGS_SERVER_URL = "http://localhost:5112"
        # Force Owner role in demo by setting a demo license and role env var for the desktop
        $env:GGS_DEMO_OWNER = "true"
        $env:GGS_LOG_DIR = $global:GlobalLogDir
        
        # Ensure PATH is properly expanded
        $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
        
        # Start the desktop application
        $desktopProcess = Start-Process -FilePath $exePath `
            -WorkingDirectory $desktopPath `
            -PassThru `
            -WindowStyle Normal
        
        Write-LogEntry "Desktop client started (PID: $($desktopProcess.Id))" -Level SUCCESS
        
        # Monitor for early crash
        Start-Sleep -Seconds 3
        if ($desktopProcess.HasExited) {
            throw "Desktop client crashed immediately after startup"
        }
        
        return $desktopProcess
        
    } catch {
        Write-LogEntry "Failed to start desktop client: $_" -Level ERROR
        return $null
    }
}

# Health monitoring function
function Start-HealthMonitoring {
    param(
        [System.Diagnostics.Process]$ServerProcess,
        [System.Diagnostics.Process]$DesktopProcess,
        [System.Diagnostics.Process]$ErrorLogViewerProcess
    )
    
    Write-LogEntry "Starting health monitoring..." -Level INFO
    
    $monitorScript = {
        param($ServerPID, $DesktopPID, $ErrorLogViewerPID, $LogFile)
        
        while ($true) {
            Start-Sleep -Seconds 5
            
            # Check server health
            $server = Get-Process -Id $ServerPID -ErrorAction SilentlyContinue
            if (!$server) {
                Add-Content -Path $LogFile -Value "[$(Get-Date)] WARNING: Server process has stopped"
            }
            
            # Check ErrorLogViewer health
            if ($ErrorLogViewerPID -and $ErrorLogViewerPID -gt 0) {
                $errorLogViewer = Get-Process -Id $ErrorLogViewerPID -ErrorAction SilentlyContinue
                if (!$errorLogViewer) {
                    Add-Content -Path $LogFile -Value "[$(Get-Date)] INFO: ErrorLogViewer process has stopped"
                }
            }
            
            # Check desktop health
            $desktop = Get-Process -Id $DesktopPID -ErrorAction SilentlyContinue
            if (!$desktop) {
                Add-Content -Path $LogFile -Value "[$(Get-Date)] INFO: Desktop application closed"
                break
            }
        }
    }
    
    $errorLogViewerPID = if ($ErrorLogViewerProcess) { $ErrorLogViewerProcess.Id } else { 0 }
    Start-Job -ScriptBlock $monitorScript -ArgumentList $ServerProcess.Id, $DesktopProcess.Id, $errorLogViewerPID, $global:LogFile | Out-Null
}

# Main execution
function Start-GGsApplication {
    try {
        Write-LogEntry "============================================" -Level INFO
        Write-LogEntry "GGs Enterprise Application Launcher v2.0" -Level INFO
        Write-LogEntry "============================================" -Level INFO
        
        # Step 1: System requirements check
        if (!(Test-SystemRequirements)) {
            throw "System requirements not met"
        }
        
        # Step 2: Clean up existing processes
        Stop-ExistingProcesses
        
        # Step 3: Start ErrorLogViewer for enterprise monitoring (optional)
        Write-LogEntry "Attempting to start ErrorLogViewer for enterprise monitoring..." -Level INFO
        $errorLogViewerProcess = Start-ErrorLogViewer
        if ($errorLogViewerProcess) {
            Write-LogEntry "ErrorLogViewer started for enterprise monitoring" -Level SUCCESS
        } else {
            Write-LogEntry "Continuing without ErrorLogViewer - this is not critical" -Level INFO
            Write-LogEntry "The main GGs application will work without ErrorLogViewer" -Level INFO
        }
        
        # Step 4: Start server
        Write-LogEntry "Starting GGs Server..." -Level INFO
        $serverProcess = Start-Server
        if (!$serverProcess) {
            Write-LogEntry "CRITICAL: Failed to start server - this is required" -Level ERROR
            throw "Failed to start server - this is a critical component"
        }
        Write-LogEntry "Server started successfully" -Level SUCCESS
        
        # Step 5: Start desktop client
        Write-LogEntry "Starting GGs Desktop Client..." -Level INFO
        $desktopProcess = Start-DesktopClient
        if (!$desktopProcess) {
            Write-LogEntry "CRITICAL: Failed to start desktop client - this is required" -Level ERROR
            # Cleanup server if desktop fails
            if ($serverProcess -and !$serverProcess.HasExited) {
                Write-LogEntry "Cleaning up server process..." -Level WARNING
                $serverProcess.Kill()
            }
            throw "Failed to start desktop client - this is a critical component"
        }
        Write-LogEntry "Desktop client started successfully" -Level SUCCESS
        
        # Step 6: Start health monitoring
        Start-HealthMonitoring -ServerProcess $serverProcess -DesktopProcess $desktopProcess -ErrorLogViewerProcess $errorLogViewerProcess
        
        Write-LogEntry "============================================" -Level SUCCESS
        Write-LogEntry "GGs Application launched successfully!" -Level SUCCESS
        Write-LogEntry "Server PID: $($serverProcess.Id)" -Level SUCCESS
        Write-LogEntry "Desktop PID: $($desktopProcess.Id)" -Level SUCCESS
        if ($errorLogViewerProcess) {
            Write-LogEntry "ErrorLogViewer PID: $($errorLogViewerProcess.Id)" -Level SUCCESS
        }
        Write-LogEntry "Log file: $global:LogFile" -Level SUCCESS
        Write-LogEntry "============================================" -Level SUCCESS
        
        Write-Host "`nApplication is running. Press Ctrl+C to stop monitoring." -ForegroundColor Cyan
        
        # Keep script running for monitoring
        while (!$desktopProcess.HasExited) {
            Start-Sleep -Seconds 1
        }
        
        Write-LogEntry "Desktop application closed" -Level INFO
        
        # Cleanup server
        if ($serverProcess -and !$serverProcess.HasExited) {
            Write-LogEntry "Stopping server..." -Level INFO
            $serverProcess.Kill()
        }
        
        # Cleanup ErrorLogViewer
        if ($errorLogViewerProcess -and !$errorLogViewerProcess.HasExited) {
            Write-LogEntry "Stopping ErrorLogViewer..." -Level INFO
            $errorLogViewerProcess.Kill()
        }
        
        Write-LogEntry "Application shutdown complete" -Level INFO
        
    } catch {
        Write-LogEntry "CRITICAL ERROR: $_" -Level ERROR
        Write-LogEntry "Stack trace: $($_.ScriptStackTrace)" -Level ERROR
        
        # Emergency cleanup
        Write-LogEntry "Performing emergency cleanup..." -Level WARNING
        Stop-ExistingProcesses
        
        exit 1
    }
}

# Execute main function
Start-GGsApplication
