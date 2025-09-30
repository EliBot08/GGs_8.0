@echo off
REM GGs Enterprise Application Launcher
REM Wrapper for PowerShell launcher script

cd /d "%~dp0"

echo.
echo ========================================
echo   GGs Enterprise Application Launcher
echo ========================================
echo.

REM Execute PowerShell launcher
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0GGsLauncher.ps1"

REM Check exit code
if errorlevel 1 (
    echo.
    echo Launch failed! Check launcher logs for details.
    pause
    exit /b 1
)

exit /b 0
