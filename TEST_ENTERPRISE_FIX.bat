@echo off
title Test Enterprise Fix
color 0E
cls

echo ================================================
echo   Test Enterprise Fix
echo ================================================
echo.

echo Step 1: Testing ErrorLogViewer build...
echo.
dotnet build "tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj" -c Release --verbosity quiet
if %errorlevel% equ 0 (
    echo ✅ ErrorLogViewer builds successfully
) else (
    echo ❌ ErrorLogViewer build failed - this will cause enterprise startup to fail
    echo.
    echo Running ErrorLogViewer fix...
    call "FIX_ERRORLOGVIEWER_FINAL.bat"
    echo.
    echo Retesting ErrorLogViewer build...
    dotnet build "tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj" -c Release --verbosity quiet
    if %errorlevel% equ 0 (
        echo ✅ ErrorLogViewer now builds successfully
    ) else (
        echo ❌ ErrorLogViewer still has build issues
    )
)

echo.
echo Step 2: Testing Desktop build...
echo.
dotnet build "clients\GGs.Desktop\GGs.Desktop.csproj" -c Release --verbosity quiet
if %errorlevel% equ 0 (
    echo ✅ Desktop builds successfully
) else (
    echo ❌ Desktop build failed
)

echo.
echo Step 3: Testing Server build...
echo.
dotnet build "server\GGs.Server\GGs.Server.csproj" -c Release --verbosity quiet
if %errorlevel% equ 0 (
    echo ✅ Server builds successfully
) else (
    echo ❌ Server build failed
)

echo.
echo Step 4: Testing Enterprise Startup...
echo.
echo Running START_ENTERPRISE.bat...
echo.
echo NOTE: This will start the full enterprise application suite.
echo Press Ctrl+C to stop when you see the applications running.
echo.
pause
call "START_ENTERPRISE.bat"

echo.
echo Press any key to exit...
pause >nul
