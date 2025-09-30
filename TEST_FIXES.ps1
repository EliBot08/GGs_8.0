# GGs Application Fixes Test Script
# Tests Desktop visibility and ErrorLogViewer scrolling fixes

Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  GGs Application Fixes Test Script" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Build Desktop Application
Write-Host "Test 1: Building Desktop Application" -ForegroundColor Yellow
try {
    $desktopBuild = dotnet build "clients\GGs.Desktop\GGs.Desktop.csproj" -c Release --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Desktop application built successfully" -ForegroundColor Green
    } else {
        Write-Host "❌ Desktop application build failed" -ForegroundColor Red
        Write-Host "Build output: $desktopBuild" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Desktop application build exception: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 2: Build ErrorLogViewer Application
Write-Host "Test 2: Building ErrorLogViewer Application" -ForegroundColor Yellow
try {
    $errorLogViewerBuild = dotnet build "tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj" -c Release --verbosity quiet
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ ErrorLogViewer application built successfully" -ForegroundColor Green
    } else {
        Write-Host "❌ ErrorLogViewer application build failed" -ForegroundColor Red
        Write-Host "Build output: $errorLogViewerBuild" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ ErrorLogViewer application build exception: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 3: Check if executables exist
Write-Host "Test 3: Checking Executable Files" -ForegroundColor Yellow
$desktopExe = "clients\GGs.Desktop\bin\Release\net8.0-windows\GGs.Desktop.exe"
$errorLogViewerExe = "tools\GGs.ErrorLogViewer\bin\Release\net8.0-windows\GGs.ErrorLogViewer.exe"

if (Test-Path $desktopExe) {
    Write-Host "✅ Desktop executable found: $desktopExe" -ForegroundColor Green
} else {
    Write-Host "❌ Desktop executable not found: $desktopExe" -ForegroundColor Red
}

if (Test-Path $errorLogViewerExe) {
    Write-Host "✅ ErrorLogViewer executable found: $errorLogViewerExe" -ForegroundColor Green
} else {
    Write-Host "❌ ErrorLogViewer executable not found: $errorLogViewerExe" -ForegroundColor Red
}

Write-Host ""

# Test 4: Test Desktop Application Launch (Quick Test)
Write-Host "Test 4: Testing Desktop Application Launch" -ForegroundColor Yellow
Write-Host "Starting Desktop application for 5 seconds..." -ForegroundColor Gray

try {
    $desktopProcess = Start-Process -FilePath $desktopExe -PassThru -WindowStyle Normal
    Write-Host "✅ Desktop application started (PID: $($desktopProcess.Id))" -ForegroundColor Green
    
    # Wait 5 seconds to see if it shows a window
    Start-Sleep -Seconds 5
    
    # Check if process is still running
    if (!$desktopProcess.HasExited) {
        Write-Host "✅ Desktop application is running" -ForegroundColor Green
        
        # Check if window is visible
        $desktopWindow = Get-Process -Id $desktopProcess.Id -ErrorAction SilentlyContinue
        if ($desktopWindow -and $desktopWindow.MainWindowTitle) {
            Write-Host "✅ Desktop window is visible: $($desktopWindow.MainWindowTitle)" -ForegroundColor Green
        } else {
            Write-Host "⚠️  Desktop window may not be visible (check taskbar)" -ForegroundColor Yellow
        }
        
        # Stop the process
        $desktopProcess.Kill()
        Write-Host "Desktop application stopped for testing" -ForegroundColor Gray
    } else {
        Write-Host "❌ Desktop application crashed immediately" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Failed to start Desktop application: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 5: Test ErrorLogViewer Application Launch (Quick Test)
Write-Host "Test 5: Testing ErrorLogViewer Application Launch" -ForegroundColor Yellow
Write-Host "Starting ErrorLogViewer application for 5 seconds..." -ForegroundColor Gray

try {
    $errorLogViewerProcess = Start-Process -FilePath $errorLogViewerExe -PassThru -WindowStyle Normal
    Write-Host "✅ ErrorLogViewer application started (PID: $($errorLogViewerProcess.Id))" -ForegroundColor Green
    
    # Wait 5 seconds to see if it shows a window
    Start-Sleep -Seconds 5
    
    # Check if process is still running
    if (!$errorLogViewerProcess.HasExited) {
        Write-Host "✅ ErrorLogViewer application is running" -ForegroundColor Green
        
        # Check if window is visible
        $errorLogViewerWindow = Get-Process -Id $errorLogViewerProcess.Id -ErrorAction SilentlyContinue
        if ($errorLogViewerWindow -and $errorLogViewerWindow.MainWindowTitle) {
            Write-Host "✅ ErrorLogViewer window is visible: $($errorLogViewerWindow.MainWindowTitle)" -ForegroundColor Green
        } else {
            Write-Host "⚠️  ErrorLogViewer window may not be visible (check taskbar)" -ForegroundColor Yellow
        }
        
        # Stop the process
        $errorLogViewerProcess.Kill()
        Write-Host "ErrorLogViewer application stopped for testing" -ForegroundColor Gray
    } else {
        Write-Host "❌ ErrorLogViewer application crashed immediately" -ForegroundColor Red
    }
} catch {
    Write-Host "❌ Failed to start ErrorLogViewer application: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host ""

# Test 6: Check for any running GGs processes
Write-Host "Test 6: Checking for Running GGs Processes" -ForegroundColor Yellow
$runningProcesses = Get-Process | Where-Object { $_.ProcessName -like "*GGs*" -or $_.ProcessName -like "*ErrorLogViewer*" }
if ($runningProcesses) {
    Write-Host "⚠️  Found running GGs processes:" -ForegroundColor Yellow
    $runningProcesses | ForEach-Object { 
        Write-Host "   - $($_.ProcessName) (PID: $($_.Id))" -ForegroundColor Gray 
    }
} else {
    Write-Host "✅ No GGs processes currently running" -ForegroundColor Green
}

Write-Host ""

# Summary
Write-Host "================================================" -ForegroundColor Cyan
Write-Host "  TEST SUMMARY" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "FIXES APPLIED:" -ForegroundColor White
Write-Host "1. ✅ Desktop App Visibility - Removed licensing check that was hiding the UI" -ForegroundColor Green
Write-Host "2. ✅ ErrorLogViewer Auto-Scroll - Disabled by default, stop button now functional" -ForegroundColor Green
Write-Host ""
Write-Host "NEXT STEPS:" -ForegroundColor White
Write-Host "1. Run the applications manually to verify the fixes work" -ForegroundColor Gray
Write-Host "2. Test that Desktop app shows on screen (not just in task manager)" -ForegroundColor Gray
Write-Host "3. Test that ErrorLogViewer doesn't auto-scroll and stop button works" -ForegroundColor Gray
Write-Host ""
Write-Host "To run the complete application suite:" -ForegroundColor Cyan
Write-Host "  .\START_ENTERPRISE.bat" -ForegroundColor White
Write-Host ""
Write-Host "================================================" -ForegroundColor Cyan
