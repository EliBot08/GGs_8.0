@echo off
setlocal EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
set "REPO_ROOT=%SCRIPT_DIR%"
if exist "%SCRIPT_DIR%..\" set "REPO_ROOT=%SCRIPT_DIR%..\"

set "CONFIG=Debug"
set "RESTORE=--restore"
set "RUN_TESTS=true"
set "LAUNCH_APPS=true"

:parse
if "%~1"=="" goto parsed
if "%~1"=="--release" (
    set "CONFIG=Release"
) else if "%~1"=="--no-restore" (
    set "RESTORE="
) else if "%~1"=="--skip-tests" (
    set "RUN_TESTS=false"
) else if "%~1"=="--no-launch" (
    set "LAUNCH_APPS=false"
) else (
    echo [WARN] Unknown option %~1
)
shift
goto parse

:parsed
set "LOG_DIR=%REPO_ROOT%launcher-logs"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%" >nul 2>&1
set "LOG_FILE=%LOG_DIR%\launch-all.log"
call :log "================================================================================"
call :log "GGs Launch Suite - Configuration: %CONFIG%"
call :log "Logs -> %LOG_FILE%"

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] dotnet CLI not found. Install .NET 9.0 SDK.
    call :log "dotnet CLI missing"
    exit /b 1
)

set "SERVER_PROJ=%REPO_ROOT%server\GGs.Server\GGs.Server.csproj"
set "DESKTOP_PROJ=%REPO_ROOT%clients\GGs.Desktop\GGs.Desktop.csproj"
set "VIEWER_PROJ=%REPO_ROOT%tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj"

call :run "dotnet clean \"%SERVER_PROJ%\" --configuration %CONFIG%" "Cleaning server"
call :run "dotnet clean \"%DESKTOP_PROJ%\" --configuration %CONFIG%" "Cleaning desktop"
call :run "dotnet clean \"%VIEWER_PROJ%\" --configuration %CONFIG%" "Cleaning viewer"

set "RESTORE_FLAG=%RESTORE%"
call :run "dotnet build \"%SERVER_PROJ%\" --configuration %CONFIG% %RESTORE_FLAG%" "Building server"
call :run "dotnet build \"%DESKTOP_PROJ%\" --configuration %CONFIG% --no-restore" "Building desktop"
call :run "dotnet build \"%VIEWER_PROJ%\" --configuration %CONFIG% --no-restore" "Building viewer"

if /I "%RUN_TESTS%"=="true" (
    call :run "dotnet test \"%REPO_ROOT%GGs.sln\" --configuration %CONFIG% --no-build" "Running solution tests"
) else (
    call :log "Skipping automated tests"
)

if /I "%LAUNCH_APPS%"=="false" goto done
call :log "Launching services and clients"

set "SERVER_ARGS=--urls http://localhost:5000"
start "GGs Server" cmd /c "dotnet run --project \"%SERVER_PROJ%\" --configuration %CONFIG% --no-build %SERVER_ARGS%"

set "DESKTOP_EXE=%REPO_ROOT%clients\GGs.Desktop\bin\%CONFIG%\net9.0-windows\GGs.Desktop.exe"
if exist "%DESKTOP_EXE%" (
    start "GGs Desktop" "%DESKTOP_EXE%"
) else (
    echo [WARN] Desktop executable not found at %DESKTOP_EXE%
    call :log "Desktop executable missing after build"
)

set "VIEWER_EXE=%REPO_ROOT%tools\GGs.ErrorLogViewer\bin\%CONFIG%\net9.0-windows\GGs.ErrorLogViewer.exe"
if exist "%VIEWER_EXE%" (
    start "Error Log Viewer" "%VIEWER_EXE%"
) else (
    echo [WARN] Viewer executable not found at %VIEWER_EXE%
    call :log "Viewer executable missing after build"
)

:done
echo [SUCCESS] Environment ready. See %LOG_FILE% for details.
call :log "Launch sequence completed"
exit /b 0

:run
set "CMD=%~1"
set "STEP=%~2"
call :log "[RUN] %STEP%"
call :log "      %CMD%"
call %CMD% >>"%LOG_FILE%" 2>&1
if errorlevel 1 (
    echo [ERROR] %STEP% failed. See %LOG_FILE% for details.
    exit /b 1
)
exit /b 0

:log
echo %~1>>"%LOG_FILE%"
exit /b 0

