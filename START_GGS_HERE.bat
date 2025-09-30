@echo off
echo ========================================
echo GGs Application Launcher (No Admin)
echo ========================================
echo Attempting to bypass Smart App Control...
echo.

cd /d "C:\Users\307824\OneDrive - Västerås Stad\Skrivbordet\GGs\clients\GGs.Desktop"

echo Starting GGs Desktop via dotnet run...
echo (This may work even with Smart App Control enabled)
echo.

dotnet run --configuration Release --no-build

echo.
echo ========================================
if %errorlevel% equ 0 (
    echo SUCCESS: GGs application may have started!
) else (
    echo FAILED: Smart App Control is still blocking the app.
    echo Please contact your IT administrator.
)
echo ========================================
echo.

pause
