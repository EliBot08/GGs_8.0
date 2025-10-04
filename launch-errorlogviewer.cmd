@echo off
setlocal enabledelayedexpansion

:: ============================================================================
:: GGs ErrorLogViewer Launcher
:: Enterprise-grade launcher for GGs ErrorLogViewer
:: Zero coding knowledge required - just double-click to run
:: ============================================================================

title GGs ErrorLogViewer Launcher

:: Set colors
color 0E

:: Display neon ASCII intro
echo.
echo  ███████╗ ██████╗  ██████╗ ███████╗
echo  ██╔════╝██╔════╝ ██╔════╝ ██╔════╝
echo  ██║  ███╗██║  ███╗███████╗ ███████╗
echo  ██║   ██║██║   ██║╚════██║ ╚════██║
echo  ╚██████╔╝╚██████╔╝██████╔╝ ███████║
echo   ╚═════╝  ╚═════╝ ╚═════╝  ╚══════╝
echo.
echo  ErrorLogViewer Launcher
echo  ============================================================================
echo.

:: Set paths and change to GGs directory
set "SCRIPT_DIR=%~dp0"
cd /d "%SCRIPT_DIR%"
set "LAUNCH_CONTROL=%SCRIPT_DIR%tools\GGs.LaunchControl\bin\Release\net9.0-windows\win-x64\publish\GGs.LaunchControl.exe"
set "LOG_DIR=%SCRIPT_DIR%launcher-logs"

:: Create log directory if it doesn't exist
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

:: Check if LaunchControl exists
if not exist "%LAUNCH_CONTROL%" (
    echo [ERROR] LaunchControl not found at: %LAUNCH_CONTROL%
    echo.
    echo Please build the solution first:
    echo   dotnet build GGs\GGs.sln -c Release
    echo   dotnet publish GGs\tools\GGs.LaunchControl\GGs.LaunchControl.csproj -c Release
    echo.
    pause
    exit /b 1
)

:: Launch with errorlogviewer profile
echo [INFO] Launching GGs ErrorLogViewer...
echo.

"%LAUNCH_CONTROL%" --profile errorlogviewer --mode normal

if errorlevel 1 (
    echo.
    echo [ERROR] Launch failed. Check logs in: %LOG_DIR%
    echo.
    pause
    exit /b 1
)

echo.
echo [SUCCESS] ErrorLogViewer launched successfully
echo.
timeout /t 3 /nobreak >nul
exit /b 0

