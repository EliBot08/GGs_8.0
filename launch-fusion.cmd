@echo off
setlocal enabledelayedexpansion

:: ============================================================================
:: GGs Fusion Launcher
:: Enterprise-grade launcher for Desktop + ErrorLogViewer together
:: Zero coding knowledge required - just double-click to run
:: ============================================================================

title GGs Fusion Launcher

:: Set colors
color 0A

:: Display neon ASCII intro
echo.
echo  ███████╗ ██████╗  ██████╗ ███████╗
echo  ██╔════╝██╔════╝ ██╔════╝ ██╔════╝
echo  ██║  ███╗██║  ███╗███████╗ ███████╗
echo  ██║   ██║██║   ██║╚════██║ ╚════██║
echo  ╚██████╔╝╚██████╔╝██████╔╝ ███████║
echo   ╚═════╝  ╚═════╝ ╚═════╝  ╚══════╝
echo.
echo  Fusion Mode Launcher (Desktop + ErrorLogViewer)
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

:: Launch with fusion profile
echo [INFO] Launching GGs Fusion Mode...
echo.

"%LAUNCH_CONTROL%" --profile fusion --mode normal

if errorlevel 1 (
    echo.
    echo [ERROR] Launch failed. Check logs in: %LOG_DIR%
    echo.
    pause
    exit /b 1
)

echo.
echo [SUCCESS] Fusion mode launched successfully
echo.
timeout /t 3 /nobreak >nul
exit /b 0

