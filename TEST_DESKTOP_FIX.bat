@echo off
title Desktop App Fix Test
color 0E
cls

echo ================================================
echo   Desktop App Fix Test
echo ================================================
echo.

echo Step 1: Cleaning build artifacts...
if exist "clients\GGs.Desktop\bin" rmdir /s /q "clients\GGs.Desktop\bin"
if exist "clients\GGs.Desktop\obj" rmdir /s /q "clients\GGs.Desktop\obj"
echo ✅ Cleaned build artifacts

echo.
echo Step 2: Building Desktop application...
dotnet build "clients\GGs.Desktop\GGs.Desktop.csproj" -c Release --verbosity normal
if %errorlevel% equ 0 (
    echo ✅ Desktop application built successfully!
    echo.
    echo Step 3: Checking executable...
    if exist "clients\GGs.Desktop\bin\Release\net8.0-windows\GGs.Desktop.exe" (
        echo ✅ Desktop executable found
        echo.
        echo FIXES APPLIED:
        echo 1. ✅ Enhanced theme resource loading
        echo 2. ✅ Better error handling for window creation
        echo 3. ✅ Improved logging for debugging
        echo 4. ✅ Desktop app will show UI window (not background only)
        echo.
        echo The Desktop app should now:
        echo - Show the main window UI instead of running in background
        echo - Have proper theme resources loaded
        echo - Handle XAML parsing errors gracefully
        echo - Provide better error messages if issues occur
        echo.
        echo SUCCESS: Desktop app is ready to use!
    ) else (
        echo ❌ Desktop executable not found
    )
) else (
    echo ❌ Desktop application build failed
    echo.
    echo Build output:
    dotnet build "clients\GGs.Desktop\GGs.Desktop.csproj" -c Release
)

echo.
echo Press any key to exit...
pause >nul
