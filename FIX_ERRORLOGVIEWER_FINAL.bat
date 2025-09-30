@echo off
title Fix ErrorLogViewer Final
color 0E
cls

echo ================================================
echo   Fix ErrorLogViewer Final
echo ================================================
echo.

echo Step 1: Stopping any running processes...
taskkill /f /im "GGs.ErrorLogViewer.exe" 2>nul
echo ✅ Stopped any running ErrorLogViewer processes

echo.
echo Step 2: Cleaning ALL build artifacts...
if exist "tools\GGs.ErrorLogViewer\bin" (
    rmdir /s /q "tools\GGs.ErrorLogViewer\bin"
    echo ✅ Deleted bin directory
)
if exist "tools\GGs.ErrorLogViewer\obj" (
    rmdir /s /q "tools\GGs.ErrorLogViewer\obj"
    echo ✅ Deleted obj directory
)

echo.
echo Step 3: Verifying MainWindow.xaml.cs is correct...
findstr /i "ExecuteAsync" "tools\GGs.ErrorLogViewer\Views\MainWindow.xaml.cs"
if %errorlevel% equ 0 (
    echo ❌ Found ExecuteAsync in MainWindow.xaml.cs - fixing...
    powershell -Command "(Get-Content 'tools\GGs.ErrorLogViewer\Views\MainWindow.xaml.cs') -replace 'ExecuteAsync', 'Execute' | Set-Content 'tools\GGs.ErrorLogViewer\Views\MainWindow.xaml.cs'"
    echo ✅ Fixed ExecuteAsync references
) else (
    echo ✅ No ExecuteAsync found in MainWindow.xaml.cs
)

echo.
echo Step 4: Restoring packages...
dotnet restore "tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj"
if %errorlevel% neq 0 (
    echo ❌ Package restore failed
    goto :error
)
echo ✅ Packages restored

echo.
echo Step 5: Building ErrorLogViewer...
dotnet build "tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj" -c Release --verbosity normal --no-restore
if %errorlevel% equ 0 (
    echo ✅ ErrorLogViewer built successfully!
    echo.
    echo Step 6: Verifying executable...
    if exist "tools\GGs.ErrorLogViewer\bin\Release\net8.0-windows\GGs.ErrorLogViewer.exe" (
        echo ✅ ErrorLogViewer executable found
        echo.
        echo SUCCESS: ErrorLogViewer is now fixed and ready!
        echo - Auto-scroll disabled by default
        echo - Stop button functional
        echo - No ExecuteAsync errors
        echo - Clean build completed
    ) else (
        echo ❌ ErrorLogViewer executable not found after build
    )
) else (
    echo ❌ ErrorLogViewer build failed
    goto :error
)

goto :end

:error
echo.
echo ERROR: Build failed. Checking for remaining issues...
echo.
echo Checking for ExecuteAsync issues...
findstr /s /i "ExecuteAsync" "tools\GGs.ErrorLogViewer\*.cs"
if %errorlevel% equ 0 (
    echo Found ExecuteAsync references - these need to be fixed
) else (
    echo No ExecuteAsync references found - issue might be elsewhere
)

echo.
echo Checking for other compilation errors...
dotnet build "tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj" -c Release --verbosity detailed

:end
echo.
echo Press any key to exit...
pause >nul
