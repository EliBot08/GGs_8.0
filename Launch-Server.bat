@echo off
setlocal enableextensions enabledelayedexpansion

set "SCRIPT_NAME=Launch-Server"
set "ROOT=%~dp0"
set "LOG_DIR=%ROOT%launcher-logs"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"
for /f %%I in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set "TIMESTAMP=%%I"
if not defined TIMESTAMP (
    set "TIMESTAMP=%DATE:~6,4%%DATE:~3,2%%DATE:~0,2%_%TIME:~0,2%%TIME:~3,2%%TIME:~6,2%"
)
set "TIMESTAMP=%TIMESTAMP::=%"
set "TIMESTAMP=%TIMESTAMP: =0%"
set "LOG_FILE=%LOG_DIR%\%SCRIPT_NAME%_%TIMESTAMP%.log"
set NO_BUILD=0
set VERIFY_ONLY=0
set HTTPS_PORT=5000
set HTTP_PORT=5001
set WAIT_READY=5

for %%A in (%*) do (
    if /I "%%~A"=="--nobuild" set NO_BUILD=1
    if /I "%%~A"=="--port" (
        set PORT_NEXT=1
        continue
    )
    if /I "%%~A"=="--verify" set VERIFY_ONLY=1
    if defined PORT_NEXT (
        for /f "tokens=1,2 delims=:" %%P in ("%%~A") do (
            set HTTPS_PORT=%%P
            if not "%%Q"=="" set HTTP_PORT=%%Q
        )
        set PORT_NEXT=
    )
)

call :log INFO "Starting %SCRIPT_NAME%"
call :ensure_dotnet || goto :fail
if "%NO_BUILD%"=="0" (
    call :build_server || goto :fail
) else (
    call :log INFO "Skipping build (--nobuild)"
)
call :prepare_server || goto :fail
if "%VERIFY_ONLY%"=="1" (
    call :log SUCCESS "Verification completed successfully."
    goto :success
)
call :launch_server || goto :fail
call :wait_for_port %HTTPS_PORT% || call :log WARN "Port %HTTPS_PORT% did not respond after %WAIT_READY% seconds"
call :log SUCCESS "Server running on https://localhost:%HTTPS_PORT%"
call :log INFO "Press any key to stop the server."
pause >nul
call :shutdown
:success
call :log INFO "%SCRIPT_NAME% completed"
exit /b 0

:ensure_dotnet
for /f %%V in ('dotnet --version 2^>nul') do set DOTNET_VERSION=%%V
if not defined DOTNET_VERSION (
    call :log ERROR "dotnet SDK not found"
    exit /b 1
)
call :log INFO "dotnet SDK %DOTNET_VERSION% detected"
exit /b 0

:build_server
call :log INFO "Building server project..."
pushd "%ROOT%" >nul 2>&1
if errorlevel 1 (
    call :log ERROR "Unable to access root directory %ROOT%."
    exit /b 1
)
dotnet build server\GGs.Server\GGs.Server.csproj --configuration Debug --nologo --verbosity minimal >> "%LOG_FILE%" 2>&1
set "BUILD_RC=%ERRORLEVEL%"
popd >nul
if not "%BUILD_RC%"=="0" (
    call :log ERROR "Server build failed (exit code %BUILD_RC%). See log."
    exit /b 1
)
call :log SUCCESS "Server build succeeded"
exit /b 0

:prepare_server
set "SERVER_DLL=%ROOT%\server\GGs.Server\bin\Debug\net9.0\GGs.Server.dll"
if not exist "%SERVER_DLL%" (
    call :log ERROR "Server DLL missing at %SERVER_DLL%"
    exit /b 1
)
exit /b 0

:launch_server
call :log INFO "Launching server on ports %HTTPS_PORT% (HTTPS) and %HTTP_PORT% (HTTP)"
set ASPNETCORE_URLS=https://localhost:%HTTPS_PORT%;http://localhost:%HTTP_PORT%
start "GGs Server" "%ComSpec%" /c "pushd \"%ROOT%\" && set ASPNETCORE_URLS=%ASPNETCORE_URLS% && dotnet run --project server\GGs.Server\GGs.Server.csproj --no-build >> \"%LOG_FILE%\" 2>&1"
if errorlevel 1 (
    call :log ERROR "Failed to start server"
    exit /b 1
)
exit /b 0

:wait_for_port
set PORT=%~1
if "%PORT%"=="" set PORT=%HTTPS_PORT%
call :log INFO "Waiting for port %PORT% to respond..."
for /l %%S in (1,1,%WAIT_READY%) do (
    powershell -Command "try { $tcp = New-Object Net.Sockets.TcpClient; $tcp.Connect('localhost', %PORT%); $tcp.Close(); exit 0 } catch { exit 1 }" >nul 2>&1
    if not errorlevel 1 goto :port_ready
    timeout /t 1 /nobreak >nul
)
exit /b 1
:port_ready
call :log SUCCESS "Port %PORT% is accepting connections"
exit /b 0

:shutdown
call :log INFO "Stopping server process..."
taskkill /FI "WINDOWTITLE eq GGs Server" /F >nul 2>&1
taskkill /IM dotnet.exe /F >nul 2>&1
call :log SUCCESS "Server stopped"
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
