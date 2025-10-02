@echo off
setlocal enableextensions enabledelayedexpansion

set "SCRIPT_NAME=Launch-All"
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
set VERIFY_MODE=0
for %%A in (%*) do (
    if /I "%%~A"=="--verify" set VERIFY_MODE=1
)

call :log INFO "Starting %SCRIPT_NAME% launcher"
call :ensure_dotnet || goto :fail
call :build_solution || goto :fail
if "%VERIFY_MODE%"=="1" (
    call :log SUCCESS "Verification run complete."
    goto :success
)
call :prepare_runtime || goto :fail
call :launch_server || goto :fail
call :sleep 5
call :launch_desktop || goto :fail
call :sleep 3
call :launch_viewer || goto :fail
call :log SUCCESS "Server, desktop client, and error log viewer launched."
call :log INFO "Press any key when you are ready to shut everything down."
pause >nul
:cleanup
call :log INFO "Shutting down launched processes..."
taskkill /FI "WINDOWTITLE eq GGs Server" /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq GGs Desktop" /F >nul 2>&1
taskkill /FI "WINDOWTITLE eq GGs ErrorLogViewer" /F >nul 2>&1
taskkill /IM GGs.Desktop.exe /F >nul 2>&1
taskkill /IM GGs.ErrorLogViewer.exe /F >nul 2>&1
taskkill /IM dotnet.exe /F >nul 2>&1
call :log SUCCESS "Shutdown complete."
goto :success

:ensure_dotnet
for /f %%V in ('dotnet --version 2^>nul') do set DOTNET_VERSION=%%V
if not defined DOTNET_VERSION (
    call :log ERROR "dotnet SDK not found. Install .NET 9 SDK before launching."
    exit /b 1
)
call :log INFO "Detected dotnet SDK version %DOTNET_VERSION%"
exit /b 0

:build_solution
call :log INFO "Building solution..."
pushd "%ROOT%" >nul 2>&1
if errorlevel 1 (
    call :log ERROR "Unable to access root directory %ROOT%."
    exit /b 1
)
dotnet build GGs.sln --configuration Debug --nologo --verbosity minimal >> "%LOG_FILE%" 2>&1
set "BUILD_RC=%ERRORLEVEL%"
popd >nul
if not "%BUILD_RC%"=="0" (
    call :log ERROR "dotnet build failed (exit code %BUILD_RC%). See %LOG_FILE%"
    exit /b 1
)
call :log SUCCESS "Build succeeded."
exit /b 0

:prepare_runtime
if not exist "%ROOT%\launcher-logs" mkdir "%ROOT%\launcher-logs"
if not exist "%ROOT%\server\GGs.Server\bin\Debug\net9.0\GGs.Server.dll" (
    call :log WARN "Server binaries missing; rebuilding server project."
    pushd "%ROOT%" >nul 2>&1
    if errorlevel 1 (
        call :log ERROR "Unable to access root directory %ROOT%."
        exit /b 1
    )
    dotnet build server\GGs.Server\GGs.Server.csproj --configuration Debug --nologo --verbosity quiet >> "%LOG_FILE%" 2>&1
    set "TMP_RC=%ERRORLEVEL%"
    popd >nul
    if not "%TMP_RC%"=="0" (
        call :log ERROR "Server rebuild failed (exit code %TMP_RC%)."
        exit /b 1
    )
)
if not exist "%ROOT%\clients\GGs.Desktop\bin\Debug\net9.0-windows\GGs.Desktop.exe" (
    call :log WARN "Desktop binaries missing; rebuilding desktop project."
    pushd "%ROOT%" >nul 2>&1
    if errorlevel 1 (
        call :log ERROR "Unable to access root directory %ROOT%."
        exit /b 1
    )
    dotnet build clients\GGs.Desktop\GGs.Desktop.csproj --configuration Debug --nologo --verbosity quiet >> "%LOG_FILE%" 2>&1
    set "TMP_RC=%ERRORLEVEL%"
    popd >nul
    if not "%TMP_RC%"=="0" (
        call :log ERROR "Desktop rebuild failed (exit code %TMP_RC%)."
        exit /b 1
    )
)
if not exist "%ROOT%\tools\GGs.ErrorLogViewer\bin\Debug\net9.0-windows\GGs.ErrorLogViewer.exe" (
    call :log WARN "ErrorLogViewer binaries missing; rebuilding tool project."
    pushd "%ROOT%" >nul 2>&1
    if errorlevel 1 (
        call :log ERROR "Unable to access root directory %ROOT%."
        exit /b 1
    )
    dotnet build tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj --configuration Debug --nologo --verbosity quiet >> "%LOG_FILE%" 2>&1
    set "TMP_RC=%ERRORLEVEL%"
    popd >nul
    if not "%TMP_RC%"=="0" (
        call :log ERROR "ErrorLogViewer rebuild failed (exit code %TMP_RC%)."
        exit /b 1
    )
)
exit /b 0

:launch_server
call :log INFO "Starting GGs.Server on https://localhost:5000 ..."
start "GGs Server" "%ComSpec%" /c "pushd \"%ROOT%\" && dotnet run --project server\GGs.Server\GGs.Server.csproj --no-build >> \"%LOG_FILE%\" 2>&1"
if errorlevel 1 (
    call :log ERROR "Failed to launch server."
    exit /b 1
)
call :log SUCCESS "Server launch command issued."
exit /b 0

:launch_desktop
call :log INFO "Starting GGs.Desktop client..."
start "GGs Desktop" "%ROOT%\clients\GGs.Desktop\bin\Debug\net9.0-windows\GGs.Desktop.exe"
if errorlevel 1 (
    call :log ERROR "Failed to launch desktop client."
    exit /b 1
)
call :log SUCCESS "Desktop client launch command issued."
exit /b 0

:launch_viewer
call :log INFO "Starting GGs.ErrorLogViewer tool..."
start "GGs ErrorLogViewer" "%ROOT%\tools\GGs.ErrorLogViewer\bin\Debug\net9.0-windows\GGs.ErrorLogViewer.exe"
if errorlevel 1 (
    call :log ERROR "Failed to launch error log viewer."
    exit /b 1
)
call :log SUCCESS "Error Log Viewer launch command issued."
exit /b 0

:sleep
if "%~1"=="" (set WAIT=3) else set WAIT=%~1
call :log INFO "Waiting %WAIT% seconds..."
timeout /t %WAIT% /nobreak >nul
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
call :log ERROR "%SCRIPT_NAME% encountered a fatal error."
exit /b 1

:success
call :log INFO "%SCRIPT_NAME% completed."
exit /b 0
