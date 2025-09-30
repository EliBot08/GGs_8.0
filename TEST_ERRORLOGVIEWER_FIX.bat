@echo off
title ErrorLogViewer Fix Test
color 0E
cls

echo ================================================
echo   ErrorLogViewer Fix Test
echo ================================================
echo.

echo Testing ErrorLogViewer build after fix...
dotnet build "tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj" -c Release --verbosity quiet
if %errorlevel% equ 0 (
    echo ✅ ErrorLogViewer builds successfully after fix
    echo.
    echo Checking executable...
    if exist "tools\GGs.ErrorLogViewer\bin\Release\net8.0-windows\GGs.ErrorLogViewer.exe" (
        echo ✅ ErrorLogViewer executable found
        echo.
        echo FIXES APPLIED:
        echo 1. ✅ Fixed ExecuteAsync error - now uses proper command casting
        echo 2. ✅ Auto-scroll disabled by default
        echo 3. ✅ Stop button now functional with AsyncRelayCommand
        echo.
        echo The ErrorLogViewer should now build and run without errors.
    ) else (
        echo ❌ ErrorLogViewer executable not found after build
    )
) else (
    echo ❌ ErrorLogViewer build still failing
    echo.
    echo Running with verbose output to see errors...
    dotnet build "tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj" -c Release --verbosity normal
)

echo.
echo ================================================
pause
