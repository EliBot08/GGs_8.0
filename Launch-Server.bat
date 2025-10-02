@echo off
setlocal EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
set "REPO_ROOT=%SCRIPT_DIR%"
if exist "%SCRIPT_DIR%..\" set "REPO_ROOT=%SCRIPT_DIR%..\"

set "CONFIG=Debug"
set "RESTORE=--restore"
set "LAUNCH=true"
set "WATCH=false"

:parse
if "%~1"=="" goto parsed
if "%~1"=="--release" (
    set "CONFIG=Release"
) else if "%~1"=="--no-restore" (
    set "RESTORE="
) else if "%~1"=="--no-launch" (
    set "LAUNCH=false"
) else if "%~1"=="--watch" (
    set "WATCH=true"
) else (
    echo [WARN] Unknown option %~1
)
shift
goto parse

:parsed
set "LOG_DIR=%REPO_ROOT%launcher-logs"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%" >nul 2>&1
set "LOG_FILE=%LOG_DIR%\launch-server.log"
call :log "=== Launch Server (%CONFIG%) ==="

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] dotnet CLI not found.
    exit /b 1
)

set "SERVER_PROJ=%REPO_ROOT%server\GGs.Server\GGs.Server.csproj"
call :log "Project: %SERVER_PROJ%"

call :run "dotnet clean \"%SERVER_PROJ%\" --configuration %CONFIG%" "Cleaning server"
call :run "dotnet build \"%SERVER_PROJ%\" --configuration %CONFIG% %RESTORE%" "Building server"

if /I "%LAUNCH%"=="false" goto done
if /I "%WATCH%"=="true" (
    start "GGs Server (watch)" cmd /c "cd /d %REPO_ROOT%server\GGs.Server && dotnet watch --project \"%SERVER_PROJ%\" run --no-hot-reload"
) else (
    start "GGs Server" cmd /c "dotnet run --project \"%SERVER_PROJ%\" --configuration %CONFIG% --no-build --urls http://localhost:5000"
)
call :log "Server launch command issued"

:done
echo [SUCCESS] Server pipeline completed. Log: %LOG_FILE%
exit /b 0

:run
set "CMD=%~1"
set "STEP=%~2"
call :log "[RUN] %STEP%"
call %CMD% >>"%LOG_FILE%" 2>&1
if errorlevel 1 (
    echo [ERROR] %STEP% failed. Check %LOG_FILE%.
    exit /b 1
)
exit /b 0

:log
echo %~1>>"%LOG_FILE%"
exit /b 0
