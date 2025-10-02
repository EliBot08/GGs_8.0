@echo off
setlocal EnableDelayedExpansion

set "SCRIPT_DIR=%~dp0"
set "REPO_ROOT=%SCRIPT_DIR%"
if exist "%SCRIPT_DIR%..\" set "REPO_ROOT=%SCRIPT_DIR%..\"

set "CONFIG=Debug"
set "RESTORE=--restore"
set "LAUNCH=true"
set "WITH_SERVER=false"

:parse
if "%~1"=="" goto parsed
if "%~1"=="--release" (
    set "CONFIG=Release"
) else if "%~1"=="--no-restore" (
    set "RESTORE="
) else if "%~1"=="--no-launch" (
    set "LAUNCH=false"
) else if "%~1"=="--with-server" (
    set "WITH_SERVER=true"
) else (
    echo [WARN] Unknown option %~1
)
shift
goto parse

:parsed
set "LOG_DIR=%REPO_ROOT%launcher-logs"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%" >nul 2>&1
set "LOG_FILE=%LOG_DIR%\launch-desktop.log"
call :log "=== Launch Desktop (%CONFIG%) ==="

where dotnet >nul 2>&1
if errorlevel 1 (
    echo [ERROR] dotnet CLI not found
    exit /b 1
)

set "DESKTOP_PROJ=%REPO_ROOT%clients\GGs.Desktop\GGs.Desktop.csproj"
call :run "dotnet clean \"%DESKTOP_PROJ%\" --configuration %CONFIG%" "Cleaning desktop"
call :run "dotnet build \"%DESKTOP_PROJ%\" --configuration %CONFIG% %RESTORE%" "Building desktop"

if /I "%WITH_SERVER%"=="true" (
    set "SERVER_PROJ=%REPO_ROOT%server\GGs.Server\GGs.Server.csproj"
    call :log "Auto-starting local server"
    start "GGs Server" cmd /c "dotnet run --project \"%SERVER_PROJ%\" --configuration %CONFIG% --no-build --urls http://localhost:5000"
)

if /I "%LAUNCH%"=="false" goto done
set "DESKTOP_EXE=%REPO_ROOT%clients\GGs.Desktop\bin\%CONFIG%\net9.0-windows\GGs.Desktop.exe"
if exist "%DESKTOP_EXE%" (
    start "GGs Desktop" "%DESKTOP_EXE%"
    call :log "Desktop launched from %DESKTOP_EXE%"
) else (
    echo [ERROR] Desktop executable not found at %DESKTOP_EXE%
    call :log "Desktop executable missing"
    exit /b 1
)

:done
echo [SUCCESS] Desktop ready. Log: %LOG_FILE%
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
