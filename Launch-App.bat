@echo off
setlocal enableextensions enabledelayedexpansion

set "SCRIPT_NAME=Launch-App"
set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
set "LOG_DIR=%ROOT%\launcher-logs"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"
for /f %%I in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set "TIMESTAMP=%%I"
if not defined TIMESTAMP (
    set "TIMESTAMP=%DATE:~6,4%%DATE:~3,2%%DATE:~0,2%_%TIME:~0,2%%TIME:~3,2%%TIME:~6,2%"
)
set "TIMESTAMP=%TIMESTAMP::=%"
set "TIMESTAMP=%TIMESTAMP: =0%"
set "LOG_FILE=%LOG_DIR%\%SCRIPT_NAME%_%TIMESTAMP%.log"
set NO_BUILD=0
set WAIT_START=2
set VERIFY_ONLY=0

for %%A in (%*) do (
    if /I "%%~A"=="--nobuild" set NO_BUILD=1
    if /I "%%~A"=="--fast" set WAIT_START=1
    if /I "%%~A"=="--verify" set VERIFY_ONLY=1
)

call :log INFO "Starting %SCRIPT_NAME% launcher"
call :ensure_dotnet || goto :fail
if "%NO_BUILD%"=="0" (
    call :build_desktop || goto :fail
) else (
    call :log INFO "Skipping build (--nobuild specified)"
)
call :prepare_desktop || goto :fail
if "%VERIFY_ONLY%"=="1" (
    call :log SUCCESS "Verification completed successfully."
    goto :success
)
call :log INFO "Waiting %WAIT_START% seconds before launch"
timeout /t %WAIT_START% /nobreak >nul
call :launch_desktop || goto :fail
call :log SUCCESS "GGs desktop client launched."
call :log INFO "Press any key when finished to close the desktop client."
pause >nul
call :shutdown
goto :success

:ensure_dotnet
for /f %%V in ('dotnet --version 2^>nul') do set DOTNET_VERSION=%%V
if not defined DOTNET_VERSION (
    call :log ERROR "dotnet SDK not detected. Install .NET 9 SDK to continue."
    exit /b 1
)
call :log INFO "dotnet SDK %DOTNET_VERSION% detected"
exit /b 0

:build_desktop
call :log INFO "Building GGs.Desktop..."
pushd "%ROOT%" >nul 2>&1
if errorlevel 1 (
    call :log ERROR "Unable to access root directory %ROOT%."
    exit /b 1
)
dotnet build clients\GGs.Desktop\GGs.Desktop.csproj --configuration Debug --nologo --verbosity minimal >> "%LOG_FILE%" 2>&1
set "BUILD_RC=%ERRORLEVEL%"
popd >nul
if not "%BUILD_RC%"=="0" (
    call :log ERROR "Desktop build failed (exit code %BUILD_RC%). See %LOG_FILE%"
    exit /b 1
)
call :log SUCCESS "Desktop build succeeded"
exit /b 0

:prepare_desktop
set "DESKTOP_EXE=%ROOT%\clients\GGs.Desktop\bin\Debug\net9.0-windows\GGs.Desktop.exe"
if not exist "%DESKTOP_EXE%" (
    call :log ERROR "Desktop executable missing at %DESKTOP_EXE%. Build required."
    exit /b 1
)
exit /b 0

:launch_desktop
call :log INFO "Launching desktop client..."
start "GGs Desktop" "%DESKTOP_EXE%"
if errorlevel 1 (
    call :log ERROR "Failed to start desktop executable"
    exit /b 1
)
exit /b 0

:shutdown
call :log INFO "Stopping desktop client..."
taskkill /FI "WINDOWTITLE eq GGs Desktop" /F >nul 2>&1
taskkill /IM GGs.Desktop.exe /F >nul 2>&1
call :log SUCCESS "Desktop client closed"
exit /b 0

:log
set "LEVEL=%~1"
set "MESSAGE=%~2"
if "%LEVEL%"=="" set "LEVEL=INFO"
if "%MESSAGE%"=="" set "MESSAGE="
echo [%LEVEL%] %MESSAGE%
echo [%DATE% %TIME%] [%LEVEL%] %MESSAGE%>>"%LOG_FILE%"
exit /b 0

:fail
call :log ERROR "%SCRIPT_NAME% failed"
exit /b 1

:success
call :log INFO "%SCRIPT_NAME% completed"
exit /b 0
