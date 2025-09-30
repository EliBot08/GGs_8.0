# GGs Enterprise Startup Script
# Comprehensive production-ready startup with validation and error handling

param(
    [switch]$SkipDotNetCheck,
    [switch]$ForceStart,
    [switch]$ValidateOnly,
    [string]$LogLevel = "Information",
    [string]$Environment = "Production"
)

# Enterprise logging setup
$LogPath = Join-Path $PSScriptRoot "logs"
$StartupLog = Join-Path $LogPath "enterprise_startup_$(Get-Date -Format 'yyyy-MM-dd_HH-mm-ss').log"

if (-not (Test-Path $LogPath)) {
    New-Item -ItemType Directory -Path $LogPath -Force | Out-Null
}

function Write-EnterpriseLog {
    param(
        [string]$Message,
        [string]$Level = "INFO",
        [string]$Component = "STARTUP"
    )
    
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"
    $logEntry = "[$timestamp] [$Level] [$Component] $Message"
    
    Write-Host $logEntry -ForegroundColor $(
        switch ($Level) {
            "ERROR" { "Red" }
            "WARN" { "Yellow" }
            "SUCCESS" { "Green" }
            "INFO" { "Cyan" }
            default { "White" }
        }
    )
    
    Add-Content -Path $StartupLog -Value $logEntry
}

function Test-DotNetRuntime {
    Write-EnterpriseLog "Checking .NET Runtime availability..." "INFO" "DOTNET"
    
    try {
        $dotnetInfo = & dotnet --info 2>&1
        $runtimes = & dotnet --list-runtimes 2>&1
        
        Write-EnterpriseLog "Available .NET Runtimes:" "INFO" "DOTNET"
        $runtimes | ForEach-Object { Write-EnterpriseLog "  $_" "INFO" "DOTNET" }
        
        # Check for .NET 8.0
        $hasNet8 = $runtimes | Where-Object { $_ -match "Microsoft\.NETCore\.App 8\." }
        $hasNet9 = $runtimes | Where-Object { $_ -match "Microsoft\.NETCore\.App 9\." }
        
        if ($hasNet8) {
            Write-EnterpriseLog ".NET 8.0 runtime found - applications can run natively" "SUCCESS" "DOTNET"
            return @{ HasNet8 = $true; HasNet9 = $hasNet9 -ne $null; CanRun = $true }
        }
        elseif ($hasNet9) {
            Write-EnterpriseLog ".NET 8.0 runtime NOT found, but .NET 9.0 is available" "WARN" "DOTNET"
            Write-EnterpriseLog "Applications may fail to start without .NET 8.0 runtime" "WARN" "DOTNET"
            return @{ HasNet8 = $false; HasNet9 = $true; CanRun = $false }
        }
        else {
            Write-EnterpriseLog "No compatible .NET runtime found" "ERROR" "DOTNET"
            return @{ HasNet8 = $false; HasNet9 = $false; CanRun = $false }
        }
    }
    catch {
        Write-EnterpriseLog "Failed to check .NET runtime: $($_.Exception.Message)" "ERROR" "DOTNET"
        return @{ HasNet8 = $false; HasNet9 = $false; CanRun = $false; Error = $_.Exception.Message }
    }
}

function Test-ProjectCompilation {
    Write-EnterpriseLog "Validating project compilation..." "INFO" "BUILD"
    
    $projects = @(
        @{ Name = "GGs.Shared"; Path = "shared\GGs.Shared\GGs.Shared.csproj"; Critical = $true },
        @{ Name = "GGs.Server"; Path = "server\GGs.Server\GGs.Server.csproj"; Critical = $true },
        @{ Name = "GGs.Agent"; Path = "agent\GGs.Agent\GGs.Agent.csproj"; Critical = $true },
        @{ Name = "GGs.Desktop"; Path = "clients\GGs.Desktop\GGs.Desktop.csproj"; Critical = $false }
    )
    
    $results = @()
    
    foreach ($project in $projects) {
        Write-EnterpriseLog "Building $($project.Name)..." "INFO" "BUILD"
        
        try {
            $buildOutput = & dotnet build $project.Path --verbosity quiet 2>&1
            $buildSuccess = $LASTEXITCODE -eq 0
            
            if ($buildSuccess) {
                Write-EnterpriseLog "$($project.Name) built successfully" "SUCCESS" "BUILD"
                $results += @{ Project = $project.Name; Success = $true; Critical = $project.Critical }
            }
            else {
                $level = if ($project.Critical) { "ERROR" } else { "WARN" }
                Write-EnterpriseLog "$($project.Name) build failed" $level "BUILD"
                Write-EnterpriseLog "Build output: $buildOutput" $level "BUILD"
                $results += @{ Project = $project.Name; Success = $false; Critical = $project.Critical; Error = $buildOutput }
            }
        }
        catch {
            $level = if ($project.Critical) { "ERROR" } else { "WARN" }
            Write-EnterpriseLog "$($project.Name) build exception: $($_.Exception.Message)" $level "BUILD"
            $results += @{ Project = $project.Name; Success = $false; Critical = $project.Critical; Error = $_.Exception.Message }
        }
    }
    
    return $results
}

function Test-SystemRequirements {
    Write-EnterpriseLog "Validating system requirements..." "INFO" "SYSTEM"
    
    $requirements = @{
        OS = "Windows 10/11 or Windows Server 2019+"
        Memory = "4GB RAM minimum, 8GB recommended"
        Disk = "2GB free space minimum"
        Network = "Internet connection required"
        Privileges = "Administrator privileges for Agent service"
    }
    
    # Check OS
    $os = Get-WmiObject -Class Win32_OperatingSystem
    Write-EnterpriseLog "Operating System: $($os.Caption) $($os.Version)" "INFO" "SYSTEM"
    
    # Check Memory
    $memory = [math]::Round($os.TotalVisibleMemorySize / 1MB, 2)
    Write-EnterpriseLog "Total Memory: ${memory}GB" "INFO" "SYSTEM"
    
    if ($memory -lt 4) {
        Write-EnterpriseLog "WARNING: Less than 4GB RAM available" "WARN" "SYSTEM"
    }
    
    # Check Disk Space
    $disk = Get-WmiObject -Class Win32_LogicalDisk | Where-Object { $_.DriveType -eq 3 } | Select-Object -First 1
    $freeSpaceGB = [math]::Round($disk.FreeSpace / 1GB, 2)
    Write-EnterpriseLog "Free Disk Space: ${freeSpaceGB}GB" "INFO" "SYSTEM"
    
    if ($freeSpaceGB -lt 2) {
        Write-EnterpriseLog "WARNING: Less than 2GB free disk space" "WARN" "SYSTEM"
    }
    
    # Check Administrator privileges
    $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")
    Write-EnterpriseLog "Administrator Privileges: $isAdmin" "INFO" "SYSTEM"
    
    if (-not $isAdmin) {
        Write-EnterpriseLog "WARNING: Not running as Administrator - Agent service installation may fail" "WARN" "SYSTEM"
    }
    
    return @{
        OS = $os.Caption
        Memory = $memory
        DiskSpace = $freeSpaceGB
        IsAdmin = $isAdmin
        MeetsRequirements = $memory -ge 4 -and $freeSpaceGB -ge 2
    }
}

function Start-GGsServices {
    param([bool]$ForceStart = $false)
    
    Write-EnterpriseLog "Starting GGs Enterprise Services..." "INFO" "SERVICES"
    
    # Start Server
    Write-EnterpriseLog "Starting GGs Server..." "INFO" "SERVER"
    try {
        $serverProcess = Start-Process -FilePath "dotnet" -ArgumentList "run --project server\GGs.Server\GGs.Server.csproj" -PassThru -NoNewWindow
        Write-EnterpriseLog "GGs Server started with PID: $($serverProcess.Id)" "SUCCESS" "SERVER"
    }
    catch {
        Write-EnterpriseLog "Failed to start GGs Server: $($_.Exception.Message)" "ERROR" "SERVER"
    }
    
    # Start Agent
    Write-EnterpriseLog "Starting GGs Agent..." "INFO" "AGENT"
    try {
        $agentProcess = Start-Process -FilePath "dotnet" -ArgumentList "run --project agent\GGs.Agent\GGs.Agent.csproj" -PassThru -NoNewWindow
        Write-EnterpriseLog "GGs Agent started with PID: $($agentProcess.Id)" "SUCCESS" "AGENT"
    }
    catch {
        Write-EnterpriseLog "Failed to start GGs Agent: $($_.Exception.Message)" "ERROR" "AGENT"
    }
    
    # Try Desktop (if build succeeded)
    if (-not $ForceStart) {
        Write-EnterpriseLog "Starting GGs Desktop..." "INFO" "DESKTOP"
        try {
            $desktopProcess = Start-Process -FilePath "dotnet" -ArgumentList "run --project clients\GGs.Desktop\GGs.Desktop.csproj" -PassThru -NoNewWindow
            Write-EnterpriseLog "GGs Desktop started with PID: $($desktopProcess.Id)" "SUCCESS" "DESKTOP"
        }
        catch {
            Write-EnterpriseLog "Failed to start GGs Desktop: $($_.Exception.Message)" "WARN" "DESKTOP"
        }
    }
}

function Show-EnterpriseReport {
    param(
        $DotNetStatus,
        $BuildResults,
        $SystemStatus
    )
    
    Write-Host "`n" -NoNewline
    Write-Host "=" * 80 -ForegroundColor Cyan
    Write-Host "GGs ENTERPRISE VALIDATION REPORT" -ForegroundColor Cyan
    Write-Host "=" * 80 -ForegroundColor Cyan
    
    # .NET Status
    Write-Host "`n.NET RUNTIME STATUS:" -ForegroundColor Yellow
    if ($DotNetStatus.HasNet8) {
        Write-Host "  [OK] .NET 8.0 Runtime: Available" -ForegroundColor Green
    } else {
        Write-Host "  [FAIL] .NET 8.0 Runtime: Missing" -ForegroundColor Red
    }
    
    if ($DotNetStatus.HasNet9) {
        Write-Host "  [OK] .NET 9.0 Runtime: Available" -ForegroundColor Green
    }
    
    # Build Status
    Write-Host "`nBUILD STATUS:" -ForegroundColor Yellow
    foreach ($result in $BuildResults) {
        $icon = if ($result.Success) { "[OK]" } else { "[FAIL]" }
        $color = if ($result.Success) { "Green" } else { if ($result.Critical) { "Red" } else { "Yellow" } }
        Write-Host "  $icon $($result.Project): $(if ($result.Success) { 'Success' } else { 'Failed' })" -ForegroundColor $color
    }
    
    # System Status
    Write-Host "`nSYSTEM STATUS:" -ForegroundColor Yellow
    Write-Host "  OS: $($SystemStatus.OS)" -ForegroundColor Cyan
    Write-Host "  Memory: $($SystemStatus.Memory)GB" -ForegroundColor Cyan
    Write-Host "  Disk Space: $($SystemStatus.DiskSpace)GB" -ForegroundColor Cyan
    Write-Host "  Admin Rights: $($SystemStatus.IsAdmin)" -ForegroundColor Cyan
    
    # Overall Status
    $criticalBuildsOk = ($BuildResults | Where-Object { $_.Critical -and -not $_.Success }).Count -eq 0
    $canRun = $criticalBuildsOk -and $SystemStatus.MeetsRequirements
    
    Write-Host "`nOVERALL STATUS:" -ForegroundColor Yellow
    if ($canRun) {
        Write-Host "  [READY] READY FOR PRODUCTION" -ForegroundColor Green
    } else {
        Write-Host "  [ISSUES] ISSUES DETECTED - Review above" -ForegroundColor Red
    }
    
    Write-Host "`nLog file: $StartupLog" -ForegroundColor Gray
    Write-Host "=" * 80 -ForegroundColor Cyan
}

# Main execution
try {
    Write-EnterpriseLog "GGs Enterprise Startup initiated" "INFO" "MAIN"
    Write-EnterpriseLog "Parameters: SkipDotNetCheck=$SkipDotNetCheck, ForceStart=$ForceStart, ValidateOnly=$ValidateOnly" "INFO" "MAIN"
    
    # System Requirements Check
    $systemStatus = Test-SystemRequirements
    
    # .NET Runtime Check
    $dotnetStatus = if ($SkipDotNetCheck) {
        Write-EnterpriseLog "Skipping .NET runtime check as requested" "WARN" "MAIN"
        @{ HasNet8 = $false; HasNet9 = $true; CanRun = $false; Skipped = $true }
    } else {
        Test-DotNetRuntime
    }
    
    # Build Validation
    $buildResults = Test-ProjectCompilation
    
    # Show comprehensive report
    Show-EnterpriseReport -DotNetStatus $dotnetStatus -BuildResults $buildResults -SystemStatus $systemStatus
    
    # Determine if we should start services
    $criticalBuildsOk = ($buildResults | Where-Object { $_.Critical -and -not $_.Success }).Count -eq 0
    
    if ($ValidateOnly) {
        Write-EnterpriseLog "Validation complete - exiting as requested" "INFO" "MAIN"
        exit 0
    }
    
    if ($ForceStart -or ($criticalBuildsOk -and $systemStatus.MeetsRequirements)) {
        if ($ForceStart) {
            Write-EnterpriseLog "Force start requested - attempting to start services despite issues" "WARN" "MAIN"
        }
        
        Start-GGsServices -ForceStart $ForceStart
        
        Write-EnterpriseLog "Enterprise startup completed" "SUCCESS" "MAIN"
        Write-Host "`nGGs Enterprise is starting up!" -ForegroundColor Green
        Write-Host "Monitor the log file for detailed information: $StartupLog" -ForegroundColor Cyan
    } else {
        Write-EnterpriseLog "Cannot start services due to validation failures" "ERROR" "MAIN"
        Write-Host "`nCannot start GGs Enterprise due to validation failures" -ForegroundColor Red
        Write-Host "Use -ForceStart to override, or fix the issues above" -ForegroundColor Yellow
        exit 1
    }
}
catch {
    Write-EnterpriseLog "Enterprise startup failed with exception: $($_.Exception.Message)" "ERROR" "MAIN"
    Write-Host "`nEnterprise startup failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}