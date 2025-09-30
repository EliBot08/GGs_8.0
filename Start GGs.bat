@echo off
REM ================================================================
REM   GGs Gaming Optimization Suite - Simple Launcher
REM   Version: 4.0 - Production Ready
REM   For non-technical users
REM ================================================================

title GGs - Starting...
color 0B
cls

echo.
echo ================================================================
echo          GGs GAMING OPTIMIZATION SUITE
echo          Simple One-Click Launcher
echo ================================================================
echo.

REM Check if .NET is installed
echo [Step 1/5] Checking system requirements...
dotnet --version >nul 2>&1
if errorlevel 1 (
    color 0C
    echo.
    echo ERROR: .NET is not installed!
    echo.
    echo Please install .NET 8.0 SDK from:
    echo https://dotnet.microsoft.com/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)
echo     .NET is installed - OK!

REM Clean up any existing processes
echo.
echo [Step 2/5] Cleaning up old processes...
taskkill /F /IM "GGs.Desktop.exe" >nul 2>&1
taskkill /F /IM "GGs.Server.exe" >nul 2>&1
taskkill /F /IM "GGs.ErrorLogViewer.exe" >nul 2>&1

REM Wait for processes to fully terminate
timeout /t 2 /nobreak >nul
echo     Cleanup complete!

REM Start ErrorLogViewer for monitoring (optional - non-blocking)
echo.
echo [Step 3/5] Starting Error Log Viewer...
cd /d "%~dp0"
if exist "tools\GGs.ErrorLogViewer\bin\Release\net8.0-windows\GGs.ErrorLogViewer.exe" (
    start "" "tools\GGs.ErrorLogViewer\bin\Release\net8.0-windows\GGs.ErrorLogViewer.exe" --log-dir "%LOCALAPPDATA%\GGs\Logs"
    echo     Error Log Viewer started!
) else (
    echo     Building Error Log Viewer first...
    cd tools\GGs.ErrorLogViewer
    dotnet build -c Release >nul 2>&1
    if exist "bin\Release\net8.0-windows\GGs.ErrorLogViewer.exe" (
        start "" "bin\Release\net8.0-windows\GGs.ErrorLogViewer.exe" --log-dir "%LOCALAPPDATA%\GGs\Logs"
        echo     Error Log Viewer started!
    ) else (
        echo     Warning: Could not start Error Log Viewer (non-critical)
    )
    cd /d "%~dp0"
)

REM Start Server (required)
echo.
echo [Step 4/5] Starting GGs Server...
cd /d "%~dp0server\GGs.Server"
start /MIN "GGs Server" dotnet run -c Release
cd /d "%~dp0"

REM Wait for server to initialize
echo     Waiting for server to start...
timeout /t 8 /nobreak >nul
echo     Server started!

REM Start Desktop Client (required)
echo.
echo [Step 5/5] Starting GGs Desktop...
cd /d "%~dp0clients\GGs.Desktop"
start "GGs Desktop" dotnet run -c Release
cd /d "%~dp0"

REM Wait a moment to see if it starts successfully
timeout /t 3 /nobreak >nul

echo.
echo ================================================================
echo               APPLICATION STARTED SUCCESSFULLY!
echo ================================================================
echo.
echo GGs Desktop should now be visible on your screen.
echo.
echo IMPORTANT NOTES:
echo - The main window WILL appear (not background-only)
echo - Keep this window open while using GGs
echo - Close GGs Desktop to stop the application
echo - Error Log Viewer shows real-time diagnostics
echo.
echo Server API: http://localhost:5112
echo Logs folder: %LOCALAPPDATA%\GGs\Logs
echo.
echo ================================================================
echo.

REM Keep this window open and monitor
echo Monitoring GGs Desktop... (Close this window to stop monitoring)
echo.

:MONITOR
timeout /t 5 /nobreak >nul
tasklist /FI "IMAGENAME eq GGs.Desktop.exe" 2>nul | find /I "GGs.Desktop.exe" >nul
if errorlevel 1 (
    echo.
    echo [INFO] GGs Desktop has closed. Stopping server...
    taskkill /F /IM "GGs.Server.exe" >nul 2>&1
    taskkill /F /IM "dotnet.exe" /FI "WINDOWTITLE eq GGs Server*" >nul 2>&1
    echo.
    echo All GGs processes stopped.
    echo.
    echo Thank you for using GGs!
    timeout /t 3 /nobreak
    exit /b 0
)
goto MONITOR
