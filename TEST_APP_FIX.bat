@echo off
title GGs Application Test - Fixed Version
color 0A
cls

echo ==================================================
echo           TESTING FIXED GGs APPLICATION
echo ==================================================
echo.

echo [1/3] Building application...
cd /d "%~dp0clients\GGs.Desktop"
dotnet build -c Release
if %errorlevel% neq 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)
echo Build successful!

echo.
echo [2/3] Starting application...
start "GGs Desktop" dotnet run -c Release
echo Application started!

echo.
echo [3/3] Waiting for application to initialize...
timeout /t 8 /nobreak >nul

echo.
echo ==================================================
echo              TEST RESULTS
echo ==================================================
echo.

echo Checking if GGs.Desktop process is running...
tasklist | findstr "GGs.Desktop" >nul
if %errorlevel% equ 0 (
    echo ✅ SUCCESS: GGs.Desktop process is running!
    echo.
    echo The application should now be visible on your screen.
    echo If you can see the GGs window, the fix was successful!
    echo.
    echo If you don't see the window, check:
    echo - Is it minimized to system tray?
    echo - Is it behind other windows?
    echo - Check the logs in the logs/ folder
) else (
    echo ❌ FAILED: GGs.Desktop process is not running
    echo.
    echo This means the application crashed during startup.
    echo Check the logs in the logs/ folder for error details.
)

echo.
echo ==================================================
echo Press any key to continue...
pause >nul
