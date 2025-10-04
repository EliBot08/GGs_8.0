@echo off
setlocal enabledelayedexpansion

:: ═══════════════════════════════════════════════════════════════════════════════
::  GGs Metrics Dashboard Launcher
::  Displays real-time 25000%% capability uplift metrics
:: ═══════════════════════════════════════════════════════════════════════════════

echo.
echo ╔═══════════════════════════════════════════════════════════════════════════════╗
echo ║                                                                               ║
echo ║      ____    ____           __  __        _          _                        ║
echo ║     / ___|  / ___|  ___    ^|  \/  ^|  ___ ^| ^|_  _ __ ^(_)  ___  ___            ║
echo ║    ^| ^|  _  ^| ^|  _  / __^|   ^| ^|\/^| ^| / _ \^| __^|^| '__^|^| ^| / __^|/ __^|           ║
echo ║    ^| ^|_^| ^| ^| ^|_^| ^| \__ \   ^| ^|  ^| ^|^|  __/^| ^|_ ^| ^|   ^| ^|^| (__ \__ \           ║
echo ║     \____^|  \____^| ^|___/   ^|_^|  ^|_^| \___^| \__^|^|_^|   ^|_^| \___^|^|___/           ║
echo ║                                                                               ║
echo ╚═══════════════════════════════════════════════════════════════════════════════╝
echo.

:: Set paths and change to GGs directory
set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"
set "METRICS_DASHBOARD=%SCRIPT_DIR%tools\GGs.MetricsDashboard\bin\Release\net9.0-windows\win-x64\GGs.MetricsDashboard.exe"

:: Check if metrics dashboard exists
if not exist "%METRICS_DASHBOARD%" (
    echo [ERROR] Metrics dashboard not found at:
    echo %METRICS_DASHBOARD%
    echo.
    echo Please build the solution first:
    echo   dotnet build GGs.sln -c Release
    echo.
    pause
    exit /b 1
)

:: Launch metrics dashboard
echo [INFO] Launching GGs Metrics Dashboard...
echo [INFO] Press Ctrl+C to exit
echo.

"%METRICS_DASHBOARD%"

if errorlevel 1 (
    echo.
    echo [ERROR] Metrics dashboard exited with error code: %errorlevel%
    pause
    exit /b %errorlevel%
)

echo.
echo [SUCCESS] Metrics dashboard closed successfully
exit /b 0

