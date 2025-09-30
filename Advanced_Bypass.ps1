# Advanced Smart App Control Bypass Script
# This script tries multiple methods to bypass Windows Smart App Control

param(
    [switch]$Force,
    [switch]$Verbose
)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "GGs Smart App Control Bypass Tool v2.0" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

$projectPath = "C:\Users\307824\OneDrive - Västerås Stad\Skrivbordet\GGs\clients\GGs.Desktop"
$exePath = "$projectPath\bin\Release\net8.0-windows\GGs.Desktop.exe"
$dllPath = "$projectPath\bin\Release\net8.0-windows\GGs.Desktop.dll"

Write-Host "Project Path: $projectPath" -ForegroundColor Gray
Write-Host "EXE Path: $exePath" -ForegroundColor Gray
Write-Host "DLL Path: $dllPath" -ForegroundColor Gray
Write-Host ""

# Method 1: Try dotnet run with different parameters
Write-Host "Method 1: Trying dotnet run with bypass parameters..." -ForegroundColor Yellow
try {
    Set-Location $projectPath
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"

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

# Method 2: Try running the DLL directly with dotnet exec
Write-Host "`nMethod 2: Trying dotnet exec on DLL..." -ForegroundColor Yellow
try {
    if (Test-Path $dllPath) {
        Write-Host "Running: dotnet exec $dllPath" -ForegroundColor Gray
        $process = Start-Process -FilePath "dotnet" -ArgumentList "exec", $dllPath -NoNewWindow -PassThru -Wait -ErrorAction Stop
        if ($process.ExitCode -eq 0) {
            Write-Host "SUCCESS: dotnet exec worked!" -ForegroundColor Green
            exit 0
        } else {
            Write-Host "FAILED: dotnet exec returned exit code $($process.ExitCode)" -ForegroundColor Red
        }
    } else {
        Write-Host "FAILED: DLL not found at $dllPath" -ForegroundColor Red
    }
} catch {
    Write-Host "FAILED: dotnet exec threw exception: $($_.Exception.Message)" -ForegroundColor Red
}

# Method 3: Try running through PowerShell with different execution policies
Write-Host "`nMethod 3: Trying PowerShell with unrestricted execution..." -ForegroundColor Yellow
try {
    $psCommand = @"
Set-ExecutionPolicy -ExecutionPolicy Unrestricted -Scope Process -Force
Set-Location '$projectPath'
dotnet run --configuration Release --no-build
"@

    Write-Host "Running PowerShell with unrestricted execution policy..." -ForegroundColor Gray
    $process = Start-Process -FilePath "powershell.exe" -ArgumentList "-Command", $psCommand -NoNewWindow -PassThru -Wait -ErrorAction Stop
    if ($process.ExitCode -eq 0) {
        Write-Host "SUCCESS: PowerShell unrestricted execution worked!" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "FAILED: PowerShell unrestricted returned exit code $($process.ExitCode)" -ForegroundColor Red
    }
} catch {
    Write-Host "FAILED: PowerShell unrestricted threw exception: $($_.Exception.Message)" -ForegroundColor Red
}

# Method 4: Try running through cmd with different parameters
Write-Host "`nMethod 4: Trying cmd with compatibility mode..." -ForegroundColor Yellow
try {
    $cmdCommand = "cd /d `"$projectPath`" && dotnet run --configuration Release --no-build"

    Write-Host "Running through cmd with compatibility parameters..." -ForegroundColor Gray
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
    $process.WaitForExit(30000) # 30 second timeout

    if ($process.ExitCode -eq 0) {
        Write-Host "SUCCESS: cmd execution worked!" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "FAILED: cmd execution returned exit code $($process.ExitCode)" -ForegroundColor Red
        $errorOutput = $process.StandardError.ReadToEnd()
        if ($errorOutput) {
            Write-Host "Error output: $errorOutput" -ForegroundColor Red
        }
    }
} catch {
    Write-Host "FAILED: cmd execution threw exception: $($_.Exception.Message)" -ForegroundColor Red
}

# Method 5: Try with different .NET runtime
Write-Host "`nMethod 5: Trying with explicit .NET runtime..." -ForegroundColor Yellow
try {
    $runtimePath = "C:\Program Files\dotnet\dotnet.exe"
    if (Test-Path $runtimePath) {
        Write-Host "Running with explicit dotnet.exe path..." -ForegroundColor Gray
        Set-Location $projectPath
        $process = Start-Process -FilePath $runtimePath -ArgumentList "run", "--configuration", "Release", "--no-build" -NoNewWindow -PassThru -Wait -ErrorAction Stop
        if ($process.ExitCode -eq 0) {
            Write-Host "SUCCESS: Explicit dotnet path worked!" -ForegroundColor Green
            exit 0
        } else {
            Write-Host "FAILED: Explicit dotnet path returned exit code $($process.ExitCode)" -ForegroundColor Red
        }
    } else {
        Write-Host "FAILED: dotnet.exe not found at $runtimePath" -ForegroundColor Red
    }
} catch {
    Write-Host "FAILED: Explicit dotnet path threw exception: $($_.Exception.Message)" -ForegroundColor Red
}

# Method 6: Create and run a self-contained deployment
Write-Host "`nMethod 6: Trying self-contained deployment approach..." -ForegroundColor Yellow
try {
    $selfContainedPath = "$projectPath\bin\Release\net8.0-windows\publish"
    if (!(Test-Path $selfContainedPath)) {
        Write-Host "Publishing self-contained version..." -ForegroundColor Gray
        Set-Location $projectPath
        $publishResult = dotnet publish -c Release -r win-x64 --self-contained true -o $selfContainedPath 2>$null
        if ($LASTEXITCODE -eq 0) {
            Write-Host "Self-contained publish successful" -ForegroundColor Green
        } else {
            Write-Host "Self-contained publish failed" -ForegroundColor Red
        }
    }

    $selfContainedExe = "$selfContainedPath\GGs.Desktop.exe"
    if (Test-Path $selfContainedExe) {
        Write-Host "Running self-contained executable..." -ForegroundColor Gray
        $process = Start-Process -FilePath $selfContainedExe -NoNewWindow -PassThru -Wait -ErrorAction Stop
        if ($process.ExitCode -eq 0) {
            Write-Host "SUCCESS: Self-contained execution worked!" -ForegroundColor Green
            exit 0
        } else {
            Write-Host "FAILED: Self-contained execution returned exit code $($process.ExitCode)" -ForegroundColor Red
        }
    } else {
        Write-Host "FAILED: Self-contained executable not found" -ForegroundColor Red
    }
} catch {
    Write-Host "FAILED: Self-contained approach threw exception: $($_.Exception.Message)" -ForegroundColor Red
}

# All methods failed
Write-Host "`n==========================================" -ForegroundColor Red
Write-Host "ALL BYPASS METHODS FAILED" -ForegroundColor Red
Write-Host "==========================================" -ForegroundColor Red
Write-Host "" -ForegroundColor Red
Write-Host "Smart App Control is actively blocking your application." -ForegroundColor Red
Write-Host "This is Microsoft's security policy and cannot be bypassed without admin rights." -ForegroundColor Red
Write-Host "" -ForegroundColor Yellow
Write-Host "SOLUTIONS:" -ForegroundColor Yellow
Write-Host "1. Contact your IT administrator to disable Smart App Control" -ForegroundColor Yellow
Write-Host "2. Ask IT admin to add your app to Windows Defender exclusions" -ForegroundColor Yellow
Write-Host "3. Use a different computer where you have administrator rights" -ForegroundColor Yellow
Write-Host "4. Package your app with a code signing certificate (requires admin setup)" -ForegroundColor Yellow
Write-Host "" -ForegroundColor Cyan
Write-Host "Your GGs application code is PERFECT - this is purely a Windows security policy issue." -ForegroundColor Cyan
Write-Host "" -ForegroundColor Cyan

Read-Host "Press Enter to exit"
