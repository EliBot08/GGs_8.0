@echo off
title Diagnose Enterprise Issues
color 0E
cls

echo ================================================
echo   Diagnose Enterprise Issues
echo ================================================
echo.

echo Checking ErrorLogViewer compilation...
echo.
dotnet build "tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj" -c Release --verbosity quiet
if %errorlevel% equ 0 (
    echo ✅ ErrorLogViewer compiles successfully
) else (
    echo ❌ ErrorLogViewer has compilation errors
    echo.
    echo Checking for ExecuteAsync errors...
    findstr /i "ExecuteAsync" "tools\GGs.ErrorLogViewer\Views\MainWindow.xaml.cs"
    if %errorlevel% equ 0 (
        echo Found ExecuteAsync in MainWindow.xaml.cs
    ) else (
        echo No ExecuteAsync found in MainWindow.xaml.cs
    )
)

echo.
echo Checking Desktop compilation...
echo.
dotnet build "clients\GGs.Desktop\GGs.Desktop.csproj" -c Release --verbosity quiet
if %errorlevel% equ 0 (
    echo ✅ Desktop compiles successfully
) else (
    echo ❌ Desktop has compilation errors
)

echo.
echo Checking Server compilation...
echo.
dotnet build "server\GGs.Server\GGs.Server.csproj" -c Release --verbosity quiet
if %errorlevel% equ 0 (
    echo ✅ Server compiles successfully
) else (
    echo ❌ Server has compilation errors
)

echo.
echo Checking for running processes...
echo.
tasklist /fi "imagename eq GGs.Desktop.exe" 2>nul | find /i "GGs.Desktop.exe" >nul
if %errorlevel% equ 0 (
    echo ⚠️  GGs.Desktop is already running
) else (
    echo ✅ GGs.Desktop is not running
)

tasklist /fi "imagename eq dotnet.exe" 2>nul | find /i "dotnet.exe" >nul
if %errorlevel% equ 0 (
    echo ⚠️  dotnet processes are running
) else (
    echo ✅ No dotnet processes running
)

echo.
echo Checking log directory...
if exist "%LOCALAPPDATA%\GGs\logs" (
    echo ✅ Log directory exists: %LOCALAPPDATA%\GGs\logs
) else (
    echo ❌ Log directory missing: %LOCALAPPDATA%\GGs\logs
)

echo.
echo Checking PowerShell execution policy...
powershell -Command "Get-ExecutionPolicy" 2>nul
if %errorlevel% equ 0 (
    echo ✅ PowerShell execution policy is accessible
) else (
    echo ❌ PowerShell execution policy check failed
)

echo.
echo ================================================
echo   Diagnosis Complete
echo ================================================
echo.
echo If ErrorLogViewer has compilation errors, run: FIX_ERRORLOGVIEWER_FINAL.bat
echo If all applications compile, run: TEST_ENTERPRISE_FIX.bat
echo.
pause
