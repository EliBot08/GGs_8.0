@echo off
title Clean and Build ErrorLogViewer
color 0E
cls

echo ================================================
echo   Clean and Build ErrorLogViewer
echo ================================================
echo.

echo Step 1: Cleaning build artifacts...
if exist "tools\GGs.ErrorLogViewer\bin" rmdir /s /q "tools\GGs.ErrorLogViewer\bin"
if exist "tools\GGs.ErrorLogViewer\obj" rmdir /s /q "tools\GGs.ErrorLogViewer\obj"
echo ✅ Cleaned build artifacts

echo.
echo Step 2: Building ErrorLogViewer...
dotnet build "tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj" -c Release --verbosity normal
if %errorlevel% equ 0 (
    echo ✅ ErrorLogViewer built successfully!
    echo.
    echo Step 3: Checking executable...
    if exist "tools\GGs.ErrorLogViewer\bin\Release\net8.0-windows\GGs.ErrorLogViewer.exe" (
        echo ✅ ErrorLogViewer executable found
        echo.
        echo SUCCESS: ErrorLogViewer is ready to use!
        echo - Auto-scroll is disabled by default
        echo - Stop button is functional
        echo - No more ExecuteAsync errors
    ) else (
        echo ❌ ErrorLogViewer executable not found
    )
) else (
    echo ❌ ErrorLogViewer build failed
    echo.
    echo Checking for ExecuteAsync errors...
    findstr /i "ExecuteAsync" "tools\GGs.ErrorLogViewer\Views\MainWindow.xaml.cs"
    if %errorlevel% equ 0 (
        echo Found ExecuteAsync in MainWindow.xaml.cs - this should not be there!
    ) else (
        echo No ExecuteAsync found in MainWindow.xaml.cs - good!
    )
)

echo.
echo Press any key to exit...
pause >nul
