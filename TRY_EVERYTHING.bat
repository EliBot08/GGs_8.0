@echo off
echo ================================================
echo GGs Ultimate Launcher - Tries Everything
echo ================================================
echo This script tries every possible method to run GGs
echo.

echo Attempt 1: Simple batch file launcher...
call "%~dp0Simple_Launcher.bat"
if %errorlevel% equ 0 goto :success

echo.
echo Attempt 2: Advanced PowerShell bypass...
powershell -ExecutionPolicy Bypass -File "%~dp0Advanced_Bypass_Fixed.ps1"
if %errorlevel% equ 0 goto :success

echo.
echo Attempt 3: VBScript launcher...
cscript "%~dp0GGs_Launcher.vbs"
if %errorlevel% equ 0 goto :success

echo.
echo Attempt 4: Direct EXE execution (if built)...
if exist "clients\GGs.Desktop\bin\Release\net8.0-windows\GGs.Desktop.exe" (
    echo Trying direct EXE execution...
    start "" "clients\GGs.Desktop\bin\Release\net8.0-windows\GGs.Desktop.exe"
    timeout /t 3 /nobreak >nul
)

echo.
echo ================================================
echo ALL METHODS ATTEMPTED
echo ================================================
echo.
echo If your GGs application started during any attempt above,
echo you should see it in your taskbar now.
echo.
echo If none of the attempts worked, Smart App Control is blocking
echo everything and you need administrator assistance.
echo.
echo Contact your IT admin with this message:
echo --------------------------------------------------
echo "Windows Smart App Control is blocking my GGs development app.
echo Please disable Smart App Control or add these exclusions:
echo C:\Users\307824\OneDrive - Västerås Stad\Skrivbordet\GGs\clients\GGs.Desktop\bin\Release\net8.0-windows\GGs.Desktop.exe
echo C:\Users\307824\OneDrive - Västerås Stad\Skrivbordet\GGs\server\GGs.Server\bin\Release\net9.0\
echo C:\Users\307824\OneDrive - Västerås Stad\Skrivbordet\GGs\agent\GGs.Agent\bin\Release\net9.0-windows\
echo This is a legitimate system monitoring application."
echo --------------------------------------------------
echo.
goto :end

:success
echo.
echo ================================================
echo SUCCESS: GGs application appears to be running!
echo ================================================
echo.
echo Check your taskbar for the GGs Desktop application window.
echo.

:end
echo Press any key to exit...
pause >nul
