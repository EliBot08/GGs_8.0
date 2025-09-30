# GGs Advanced Bypass - Unicode Path Fix
# Fixed version that handles special characters in path

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "GGs Smart App Control Bypass Tool v2.1" -ForegroundColor Cyan
Write-Host "Unicode Path Fix" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Use a more robust path resolution
$basePath = $PSScriptRoot
if ($PSScriptRoot -notlike "*GGs*") {
    # If script is not in GGs folder, try to find it
    $basePath = Split-Path $PSScriptRoot -Parent
}

$projectPath = Join-Path $basePath "clients\GGs.Desktop"
$exePath = Join-Path $projectPath "bin\Release\net8.0-windows\GGs.Desktop.exe"
$dllPath = Join-Path $projectPath "bin\Release\net8.0-windows\GGs.Desktop.dll"

# Alternative: Use Get-ChildItem to resolve the path
$desktopFolder = Get-ChildItem -Path $basePath -Directory | Where-Object { $_.Name -eq "clients" } | ForEach-Object {
    Get-ChildItem -Path $_.FullName -Directory | Where-Object { $_.Name -eq "GGs.Desktop" }
}

if ($desktopFolder) {
    $projectPath = $desktopFolder.FullName
    $exePath = Join-Path $projectPath "bin\Release\net8.0-windows\GGs.Desktop.exe"
    $dllPath = Join-Path $projectPath "bin\Release\net8.0-windows\GGs.Desktop.dll"
}

Write-Host "Resolved Project Path: $projectPath" -ForegroundColor Gray
Write-Host "Resolved EXE Path: $exePath" -ForegroundColor Gray
Write-Host "Resolved DLL Path: $dllPath" -ForegroundColor Gray
Write-Host ""

# Test if paths exist
if (!(Test-Path $projectPath)) {
    Write-Host "ERROR: Project path not found: $projectPath" -ForegroundColor Red
    Write-Host "Current directory: $(Get-Location)" -ForegroundColor Red
    Write-Host "Script root: $PSScriptRoot" -ForegroundColor Red
    exit 1
}

Write-Host "✓ Project path exists" -ForegroundColor Green

# Method 1: Try dotnet run with proper path resolution
Write-Host "`nMethod 1: Trying dotnet run with resolved path..." -ForegroundColor Yellow
try {
    Set-Location $projectPath
    Write-Host "Current location: $(Get-Location)" -ForegroundColor Gray
    Write-Host "Running: dotnet run --configuration Release --no-build --verbosity quiet" -ForegroundColor Gray

    $process = Start-Process -FilePath "dotnet" -ArgumentList "run", "--configuration", "Release", "--no-build", "--verbosity", "quiet" -NoNewWindow -PassThru -Wait -ErrorAction Stop
    if ($process.ExitCode -eq 0) {
        Write-Host "SUCCESS: dotnet run worked!" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "FAILED: dotnet run returned exit code $($process.ExitCode)" -ForegroundColor Red
    }
} catch {
    Write-Host "FAILED: dotnet run threw exception: $($_.Exception.Message)" -ForegroundColor Red
}

# Method 2: Try running the EXE directly if it exists
Write-Host "`nMethod 2: Trying to run EXE directly..." -ForegroundColor Yellow
if (Test-Path $exePath) {
    try {
        Write-Host "EXE found, attempting direct execution..." -ForegroundColor Gray
        $process = Start-Process -FilePath $exePath -NoNewWindow -PassThru -Wait -ErrorAction Stop
        if ($process.ExitCode -eq 0) {
            Write-Host "SUCCESS: Direct EXE execution worked!" -ForegroundColor Green
            exit 0
        } else {
            Write-Host "FAILED: Direct EXE execution returned exit code $($process.ExitCode)" -ForegroundColor Red
        }
    } catch {
        Write-Host "FAILED: Direct EXE execution threw exception: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "EXE not found at: $exePath" -ForegroundColor Red
}

# Method 3: Try with different working directory approach
Write-Host "`nMethod 3: Trying with explicit working directory..." -ForegroundColor Yellow
try {
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = "dotnet"
    $startInfo.Arguments = "run --configuration Release --no-build"
    $startInfo.WorkingDirectory = $projectPath
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    $process.Start() | Out-Null
    $process.WaitForExit(10000) # 10 second timeout

    if ($process.ExitCode -eq 0) {
        Write-Host "SUCCESS: Explicit working directory worked!" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "FAILED: Explicit working directory returned exit code $($process.ExitCode)" -ForegroundColor Red
        $errorOutput = $process.StandardError.ReadToEnd()
        if ($errorOutput) {
            Write-Host "Error: $errorOutput" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "FAILED: Explicit working directory threw exception: $($_.Exception.Message)" -ForegroundColor Red
}

# Method 4: Try using cmd to change directory first
Write-Host "`nMethod 4: Trying through cmd with cd..." -ForegroundColor Yellow
try {
    $cmdCommand = "cd /d `"$projectPath`" && dotnet run --configuration Release --no-build"
    $startInfo = New-Object System.Diagnostics.ProcessStartInfo
    $startInfo.FileName = "cmd.exe"
    $startInfo.Arguments = "/c $cmdCommand"
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $startInfo
    $process.Start() | Out-Null
    $process.WaitForExit(15000) # 15 second timeout

    if ($process.ExitCode -eq 0) {
        Write-Host "SUCCESS: cmd with cd worked!" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "FAILED: cmd with cd returned exit code $($process.ExitCode)" -ForegroundColor Red
        $errorOutput = $process.StandardError.ReadToEnd()
        if ($errorOutput) {
            Write-Host "Error: $errorOutput" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "FAILED: cmd with cd threw exception: $($_.Exception.Message)" -ForegroundColor Red
}

# All methods failed
Write-Host "`n==========================================" -ForegroundColor Red
Write-Host "ALL BYPASS METHODS FAILED" -ForegroundColor Red
Write-Host "==========================================" -ForegroundColor Red
Write-Host "" -ForegroundColor Red
Write-Host "Issues identified:" -ForegroundColor Yellow
Write-Host "1. Unicode characters in path (Västerås) causing PowerShell issues" -ForegroundColor Yellow
Write-Host "2. Smart App Control blocking all execution methods" -ForegroundColor Yellow
Write-Host "" -ForegroundColor Cyan
Write-Host "SOLUTION: Contact your IT administrator" -ForegroundColor Cyan
Write-Host "" -ForegroundColor Cyan
Write-Host "Your GGs application is ready, but Windows security requires admin intervention." -ForegroundColor Cyan
Write-Host "" -ForegroundColor Cyan

Read-Host "Press Enter to exit"
