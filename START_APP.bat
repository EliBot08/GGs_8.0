@echo off
title GGs Application - Easy Launcher
color 0E
cls

echo ==================================================
echo              GGs APPLICATION LAUNCHER
echo ==================================================
echo.

REM Clean up any existing processes
echo [1/3] Cleaning up old processes...
taskkill /F /IM GGs.Server.exe 2>nul
taskkill /F /IM GGs.Desktop.exe 2>nul
for /f "tokens=2" %%a in ('tasklist ^| findstr /i "dotnet"') do (
    taskkill /PID %%a /F 2>nul
)
timeout /t 2 /nobreak >nul

REM Start the server
echo [2/3] Starting Server (background)...
cd /d "%~dp0server\GGs.Server"
start /min "GGs Server" dotnet run
cd /d "%~dp0"

REM Give server time to start
echo      Waiting for server to initialize...
timeout /t 6 /nobreak >nul

REM Start the desktop app with normal window
echo [3/3] Starting Desktop Application...
cd /d "%~dp0clients\GGs.Desktop"
start "GGs Desktop" /MAX dotnet run
cd /d "%~dp0"

timeout /t 3 /nobreak >nul

echo.
echo ==================================================
echo         APPLICATION STARTED SUCCESSFULLY!
echo ==================================================
echo.
echo VALID LICENSE KEYS (Copy one of these):
echo --------------------------------------------------
echo   ADMIN KEY:  GGSP-2024-ADMI-NKEY
echo   PRO KEY:    GGSP-RO20-24PR-OFES  
echo   TEST KEY:   1234-5678-90AB-CDEF
echo --------------------------------------------------
echo.
echo TO LOGIN:
echo 1. Look for the GGs Desktop window (it should open)
echo 2. Copy one of the license keys above
echo 3. Paste it in the login field (will auto-format)
echo 4. Click "Log in" or press Enter
echo.
echo Server: http://localhost:5112
echo.
echo IMPORTANT: Keep this window open while using GGs!
echo ==================================================
echo.
pause
