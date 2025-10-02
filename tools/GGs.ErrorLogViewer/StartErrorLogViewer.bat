@echo off
REM ============================================
REM  GGs ErrorLogViewer Startup Script
REM  Version: 5.0 Enterprise Edition
REM ============================================

echo.
echo [92m=========================================[0m
echo [92m   GGs ErrorLogViewer Starting...[0m
echo [92m=========================================[0m
echo.

REM Set window title
title GGs ErrorLogViewer - Log Monitoring Tool

REM Navigate to the application directory
cd /d "%~dp0bin\Release\net9.0-windows\"

REM Check if executable exists
if not exist "GGs.ErrorLogViewer.exe" (
    echo [91mERROR: GGs.ErrorLogViewer.exe not found![0m
    echo [91mPlease build the project first using: dotnet build --configuration Release[0m
    echo.
    pause
    exit /b 1
)

REM Launch the application
echo [96mLaunching ErrorLogViewer...[0m
echo [93mMonitoring logs in real-time with analytics enabled[0m
echo.

start "" "GGs.ErrorLogViewer.exe"

REM Wait a moment to check if it started successfully
timeout /t 2 /nobreak >nul

REM Check if process is running
tasklist /FI "IMAGENAME eq GGs.ErrorLogViewer.exe" 2>NUL | find /I /N "GGs.ErrorLogViewer.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [92m✓ ErrorLogViewer started successfully![0m
    echo [96mTo monitor logs from a specific directory, use:[0m
    echo [93m  GGs.ErrorLogViewer.exe --log-dir "C:\Path\To\Logs"[0m
    echo.
) else (
    echo [91m✗ Failed to start ErrorLogViewer[0m
    echo [91mPlease check the logs for error details[0m
    echo.
    pause
    exit /b 1
)

echo [92mYou can now close this window.[0m
echo.
timeout /t 3 /nobreak >nul
