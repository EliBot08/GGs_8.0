@echo off
title Test Desktop Window Visibility
color 0E
cls

echo ================================================
echo   Test Desktop Window Visibility
echo ================================================
echo.

echo Step 1: Building Desktop application...
dotnet build "clients\GGs.Desktop\GGs.Desktop.csproj" -c Release --verbosity quiet
if %errorlevel% neq 0 (
    echo ❌ Desktop build failed
    goto :error
)
echo ✅ Desktop built successfully

echo.
echo Step 2: Testing Desktop window creation...
echo.
echo Starting Desktop application directly...
echo.
echo NOTE: The window should appear on screen. If it doesn't, there's a visibility issue.
echo.

cd "clients\GGs.Desktop\bin\Release\net8.0-windows"
start "" "GGs.Desktop.exe"

echo.
echo Desktop application started. Check if the window is visible on screen.
echo.
echo If the window is not visible, the issue is with window visibility logic.
echo If the window is visible, the issue is with the enterprise startup script.
echo.
pause

goto :end

:error
echo.
echo ERROR: Build failed
echo.

:end
echo.
echo Press any key to exit...
pause >nul
