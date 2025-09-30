@echo off
title GGs Fixes Test
color 0E
cls

echo ================================================
echo   GGs Application Fixes Test Script
echo ================================================
echo.

echo Test 1: Building Desktop Application...
dotnet build "clients\GGs.Desktop\GGs.Desktop.csproj" -c Release --verbosity quiet
if %errorlevel% equ 0 (
    echo ✅ Desktop application built successfully
) else (
    echo ❌ Desktop application build failed
)
echo.

echo Test 2: Building ErrorLogViewer Application...
dotnet build "tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj" -c Release --verbosity quiet
if %errorlevel% equ 0 (
    echo ✅ ErrorLogViewer application built successfully
) else (
    echo ❌ ErrorLogViewer application build failed
)
echo.

echo Test 3: Checking Executable Files...
if exist "clients\GGs.Desktop\bin\Release\net8.0-windows\GGs.Desktop.exe" (
    echo ✅ Desktop executable found
) else (
    echo ❌ Desktop executable not found
)

if exist "tools\GGs.ErrorLogViewer\bin\Release\net8.0-windows\GGs.ErrorLogViewer.exe" (
    echo ✅ ErrorLogViewer executable found
) else (
    echo ❌ ErrorLogViewer executable not found
)
echo.

echo ================================================
echo   FIXES APPLIED
echo ================================================
echo.
echo 1. ✅ Desktop App Visibility - Removed licensing check that was hiding the UI
echo 2. ✅ ErrorLogViewer Auto-Scroll - Disabled by default, stop button now functional
echo.
echo NEXT STEPS:
echo 1. Run the applications manually to verify the fixes work
echo 2. Test that Desktop app shows on screen (not just in task manager)
echo 3. Test that ErrorLogViewer doesn't auto-scroll and stop button works
echo.
echo To run the complete application suite:
echo   .\START_ENTERPRISE.bat
echo.
echo ================================================
echo.
pause
