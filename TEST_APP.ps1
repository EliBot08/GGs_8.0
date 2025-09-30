# GGs Application Test Script

Write-Host "================================" -ForegroundColor Cyan
Write-Host "  GGs Application Test Suite" -ForegroundColor Cyan  
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""

# Test 1: Check if login window is open
Write-Host "Test 1: Login Window Check" -ForegroundColor Yellow
$loginWindow = Get-Process | Where-Object {$_.MainWindowTitle -eq "GGs - Login"}
if ($loginWindow) {
    Write-Host "✅ Login window is open" -ForegroundColor Green
    Write-Host "   Process: $($loginWindow.ProcessName)" -ForegroundColor Gray
    Write-Host "   PID: $($loginWindow.Id)" -ForegroundColor Gray
} else {
    Write-Host "❌ Login window not found" -ForegroundColor Red
}

# Test 2: Check server status
Write-Host ""
Write-Host "Test 2: Server Status" -ForegroundColor Yellow
$serverRunning = netstat -an | Select-String ":5112" | Select-String "LISTENING"
if ($serverRunning) {
    Write-Host "✅ Server is running on port 5112" -ForegroundColor Green
} else {
    Write-Host "❌ Server is not running" -ForegroundColor Red
}

# Test 3: Check for errors in log
Write-Host ""
Write-Host "Test 3: Error Log Check" -ForegroundColor Yellow
$logPath = Join-Path $env:LOCALAPPDATA "GGs\logs\desktop.log"
if (Test-Path $logPath) {
    $recentErrors = Get-Content $logPath -Tail 50 | Select-String "ERROR" | Select-Object -Last 3
    if ($recentErrors) {
        Write-Host "⚠️  Recent errors found in log:" -ForegroundColor Yellow
        $recentErrors | ForEach-Object { Write-Host "   $_" -ForegroundColor Red }
    } else {
        Write-Host "✅ No recent errors in log" -ForegroundColor Green
    }
} else {
    Write-Host "⚠️  Log file not found" -ForegroundColor Yellow
}

# Display valid license keys
Write-Host ""
Write-Host "================================" -ForegroundColor Cyan
Write-Host "  Valid License Keys" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Admin Key: " -NoNewline -ForegroundColor Yellow
Write-Host "GGSP-2024-ADMI-NKEY" -ForegroundColor White
Write-Host "Pro Key:   " -NoNewline -ForegroundColor Yellow
Write-Host "GGSP-RO20-24PR-OFES" -ForegroundColor White
Write-Host "Test Key:  " -NoNewline -ForegroundColor Yellow
Write-Host "1234-5678-90AB-CDEF" -ForegroundColor White
Write-Host ""
Write-Host "Instructions:" -ForegroundColor Cyan
Write-Host "1. Copy one of the license keys above"
Write-Host "2. Paste it in the login window"
Write-Host "3. Click 'Log in' or press Enter"
Write-Host "4. The main application should open without errors"
Write-Host ""
Write-Host "================================" -ForegroundColor Cyan

# Monitor for main window opening
Write-Host ""
Write-Host "Monitoring for main window to open..." -ForegroundColor Yellow
Write-Host "Press Ctrl+C to stop monitoring" -ForegroundColor Gray
Write-Host ""

$mainWindowFound = $false
$attempts = 0
while (-not $mainWindowFound -and $attempts -lt 60) {
    $mainWindow = Get-Process | Where-Object {
        $_.MainWindowTitle -like "*Gaming Optimization*" -or 
        $_.MainWindowTitle -like "*GGs - Gaming*" -or
        $_.MainWindowTitle -eq "GGs - Gaming Optimization Suite"
    }
    
    if ($mainWindow) {
        Write-Host "✅ MAIN WINDOW OPENED SUCCESSFULLY!" -ForegroundColor Green
        Write-Host "   Window: $($mainWindow.MainWindowTitle)" -ForegroundColor White
        $mainWindowFound = $true
    } else {
        Write-Host "." -NoNewline
        Start-Sleep -Seconds 1
        $attempts++
    }
}

if (-not $mainWindowFound) {
    Write-Host ""
    Write-Host "⏱️  Timeout: Main window did not open after 60 seconds" -ForegroundColor Yellow
}
