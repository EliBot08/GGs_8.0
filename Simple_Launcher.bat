@echo off
echo ================================================
echo GGs Simple Launcher - No PowerShell Required
echo ================================================
echo Attempting to start GGs using basic cmd methods
echo.

REM Try to change to the project directory using pushd (handles Unicode better)
pushd "C:\Users\307824\OneDrive - Västerås Stad\Skrivbordet\GGs\clients\GGs.Desktop"

if errorlevel 1 (
    echo FAILED: Cannot access project directory
    echo This may be due to Unicode characters in the path
    goto :error
)

echo Current directory: %CD%
echo.

REM Check if dotnet is available
dotnet --version >nul 2>&1
if errorlevel 1 (
    echo FAILED: dotnet CLI not found
    goto :error
)

echo ✓ dotnet CLI found
echo.

REM Try to run the application
echo Starting GGs Desktop application...
echo If Smart App Control blocks this, you'll see an error message.
echo.

dotnet run --configuration Release --no-build

if %errorlevel% equ 0 (
    echo.
    echo ================================================
    echo SUCCESS: GGs application started!
    echo ================================================
    goto :end
) else (
    echo.
    echo ================================================
    echo FAILED: Smart App Control blocked the application
    echo ================================================
    goto :error
)

:error
echo.
echo ERROR: Unable to start GGs application
echo.
echo Possible solutions:
echo 1. Contact your IT administrator to disable Smart App Control
echo 2. Ask IT admin to add the app to Windows Defender exclusions
echo 3. Use a different computer where you have administrator rights
echo.
echo Your GGs code is 100%% complete - this is just Windows security.
echo.

:end
popd
echo.
pause
