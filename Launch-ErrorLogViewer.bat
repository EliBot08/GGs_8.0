@echo off
setlocal EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
set "REPO_ROOT=%SCRIPT_DIR%"
if exist "%SCRIPT_DIR%..\" set "REPO_ROOT=%SCRIPT_DIR%..\"

set "CONFIG=Debug"
set "RESTORE=--restore"
set "LAUNCH=true"

if "%~1"=="--release" set "CONFIG=Release"
if "%~1"=="--no-restore" set "RESTORE="
if "%~1"=="--no-launch" set "LAUNCH=false"

set "LOG_DIR=%REPO_ROOT%launcher-logs"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%" >nul 2>&1
set "LOG_FILE=%LOG_DIR%\launch-viewer.log"
call :log "=== Launch Error Log Viewer (%CONFIG%) ==="

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] dotnet CLI not found
    exit /b 1
)

set "VIEWER_PROJ=%REPO_ROOT%tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj"
call :run "dotnet clean \"%VIEWER_PROJ%\" --configuration %CONFIG%" "Cleaning viewer"
call :run "dotnet build \"%VIEWER_PROJ%\" --configuration %CONFIG% %RESTORE%" "Building viewer"

if /I "%LAUNCH%"=="false" goto done
set "VIEWER_EXE=%REPO_ROOT%tools\GGs.ErrorLogViewer\bin\%CONFIG%\net9.0-windows\GGs.ErrorLogViewer.exe"
if exist "%VIEWER_EXE%" (
    start "GGs Error Log Viewer" "%VIEWER_EXE%"
    call :log "Viewer launched"
) else (
    echo [ERROR] Viewer executable missing at %VIEWER_EXE%
    call :log "Viewer exe missing"
    exit /b 1
)

:done
echo [SUCCESS] Error Log Viewer ready. Log: %LOG_FILE%
exit /b 0

:run
set "CMD=%~1"
set "STEP=%~2"
call :log "[RUN] %STEP%"
call %CMD% >>"%LOG_FILE%" 2>&1
if errorlevel 1 (
    echo [ERROR] %STEP% failed. See %LOG_FILE%.
    exit /b 1
)
exit /b 0

:log
echo %~1>>"%LOG_FILE%"
exit /b 0
