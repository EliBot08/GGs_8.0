@echo off
setlocal enabledelayedexpansion

REM ============================================================================
REM  GGs Launcher Suite - Master Orchestrator (Enterprise Edition)
REM ============================================================================
REM  Features:
REM  - Always builds latest code for all components before launch
REM  - Shows build output and errors
REM  - Aborts launch if any build fails
REM  - Prints build timestamp and (if possible) app versions
REM  - Logs all actions
REM  - Robust for non-technical users
REM ============================================================================

REM Set console colors and title
color 0F
set "LAUNCHER_TITLE=GGs Launcher Suite - Master Orchestrator (Enterprise Edition)"
title %LAUNCHER_TITLE%

REM CONFIGURATION
set "SERVER_EXE=GGs.Server.exe"
set "DESKTOP_EXE=GGs.Desktop.exe"
set "VIEWER_EXE=GGs.ErrorLogViewer.exe"
set "SERVER_PROJECT=server\GGs.Server\GGs.Server.csproj"
set "DESKTOP_PROJECT=clients\GGs.Desktop\GGs.Desktop.csproj"
set "VIEWER_PROJECT=tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj"
set "SERVER_BIN=server\GGs.Server\bin\Debug\net9.0"
set "DESKTOP_BIN=clients\GGs.Desktop\bin\Debug\net9.0-windows"
set "VIEWER_BIN=tools\GGs.ErrorLogViewer\bin\Debug\net9.0-windows"
set "LOG_DIR=launcher-logs"
set "MASTER_LOG=%LOG_DIR%\master-launcher.log"
set "SERVER_BUILD_LOG=%LOG_DIR%\server-build.log"
set "DESKTOP_BUILD_LOG=%LOG_DIR%\desktop-build.log"
set "VIEWER_BUILD_LOG=%LOG_DIR%\viewer-build.log"

REM INITIALIZATION
if not exist "%LOG_DIR%" (
    mkdir "%LOG_DIR%"
    echo [INFO] Created log directory: %LOG_DIR%
)
call :LOG "============================================================================"
call :LOG "%LAUNCHER_TITLE% Started"
call :LOG "============================================================================"

REM ============================================================================
REM  STEP 1: PRE-BUILD PROCESS CLEANUP
REM ============================================================================
echo.
echo [STEP 1/4] Ensuring no existing instances are running before build...
call :LOG "Pre-build process cleanup..."

REM Kill any running server instances
tasklist /FI "IMAGENAME eq %SERVER_EXE%" 2>NUL | find /I /N "%SERVER_EXE%">NUL
if "%ERRORLEVEL%"=="0" (
    echo [INFO] Found running server instance, terminating...
    call :LOG "Found running server instance, terminating..."
    taskkill /F /IM "%SERVER_EXE%" >NUL 2>&1
    timeout /t 2 /nobreak >NUL
)

REM Kill any running desktop instances
tasklist /FI "IMAGENAME eq %DESKTOP_EXE%" 2>NUL | find /I /N "%DESKTOP_EXE%">NUL
if "%ERRORLEVEL%"=="0" (
    echo [INFO] Found running desktop instance, terminating...
    call :LOG "Found running desktop instance, terminating..."
    taskkill /F /IM "%DESKTOP_EXE%" >NUL 2>&1
    timeout /t 2 /nobreak >NUL
)

REM Kill any running viewer instances
tasklist /FI "IMAGENAME eq %VIEWER_EXE%" 2>NUL | find /I /N "%VIEWER_EXE%">NUL
if "%ERRORLEVEL%"=="0" (
    echo [INFO] Found running viewer instance, terminating...
    call :LOG "Found running viewer instance, terminating..."
    taskkill /F /IM "%VIEWER_EXE%" >NUL 2>&1
    timeout /t 2 /nobreak >NUL
)

echo [SUCCESS] Pre-build cleanup completed.
call :LOG "Pre-build cleanup completed."

REM ============================================================================
REM  STEP 2: BUILD ALL COMPONENTS
REM ============================================================================
echo.
echo [STEP 2/4] Building all components (Server, Desktop, Viewer)...
call :LOG "Building all components..."

REM Clean and build Server
call :LOG "Cleaning and building server..."
dotnet clean "%SERVER_PROJECT%" --verbosity quiet > "%SERVER_BUILD_LOG%" 2>&1
dotnet build "%SERVER_PROJECT%" --configuration Debug --verbosity minimal >> "%SERVER_BUILD_LOG%" 2>&1
if errorlevel 1 (
    echo [ERROR] Server build failed. See %SERVER_BUILD_LOG% for details.
    call :LOG "ERROR: Server build failed. Aborting launch."
    type "%SERVER_BUILD_LOG%"
    pause
    exit /b 1
)
REM Clean and build Desktop
call :LOG "Cleaning and building desktop..."
dotnet clean "%DESKTOP_PROJECT%" --verbosity quiet > "%DESKTOP_BUILD_LOG%" 2>&1
dotnet build "%DESKTOP_PROJECT%" --configuration Debug --verbosity minimal >> "%DESKTOP_BUILD_LOG%" 2>&1
if errorlevel 1 (
    echo [ERROR] Desktop build failed. See %DESKTOP_BUILD_LOG% for details.
    call :LOG "ERROR: Desktop build failed. Aborting launch."
    type "%DESKTOP_BUILD_LOG%"
    pause
    exit /b 1
)
REM Clean and build Viewer
call :LOG "Cleaning and building viewer..."
dotnet clean "%VIEWER_PROJECT%" --verbosity quiet > "%VIEWER_BUILD_LOG%" 2>&1
dotnet build "%VIEWER_PROJECT%" --configuration Debug --verbosity minimal >> "%VIEWER_BUILD_LOG%" 2>&1
if errorlevel 1 (
    echo [ERROR] Viewer build failed. See %VIEWER_BUILD_LOG% for details.
    call :LOG "ERROR: Viewer build failed. Aborting launch."
    type "%VIEWER_BUILD_LOG%"
    pause
    exit /b 1
)
echo [SUCCESS] All components built successfully.
call :LOG "All components built successfully."

REM ============================================================================
REM  STEP 3: VALIDATE EXECUTABLES
REM ============================================================================
if not exist "%SERVER_BIN%\%SERVER_EXE%" (
    echo [ERROR] Server executable not found after build: %SERVER_BIN%\%SERVER_EXE%
    call :LOG "ERROR: Server executable not found after build. Aborting launch."
    pause
    exit /b 1
)
if not exist "%DESKTOP_BIN%\%DESKTOP_EXE%" (
    echo [ERROR] Desktop executable not found after build: %DESKTOP_BIN%\%DESKTOP_EXE%
    call :LOG "ERROR: Desktop executable not found after build. Aborting launch."
    pause
    exit /b 1
)
if not exist "%VIEWER_BIN%\%VIEWER_EXE%" (
    echo [ERROR] Viewer executable not found after build: %VIEWER_BIN%\%VIEWER_EXE%
    call :LOG "ERROR: Viewer executable not found after build. Aborting launch."
    pause
    exit /b 1
)
echo [SUCCESS] All executables found after build.
call :LOG "All executables found after build."

REM ============================================================================
REM  STEP 4: PRINT BUILD INFO
REM ============================================================================
set "BUILD_TIME=%DATE% %TIME%"
echo.
echo ============================================================================
echo  %LAUNCHER_TITLE%
echo ============================================================================
echo  Build Time: %BUILD_TIME%
echo  Server:     %SERVER_BIN%\%SERVER_EXE%"
echo  Desktop:    %DESKTOP_BIN%\%DESKTOP_EXE%"
echo  Viewer:     %VIEWER_BIN%\%VIEWER_EXE%"
echo ============================================================================
echo.
call :LOG "Build time: %BUILD_TIME%"

REM ============================================================================
REM  STEP 5: LAUNCH ALL COMPONENTS
REM ============================================================================
echo [STEP 5/5] Launching all components...
call :LOG "Launching all components..."
start "GGs Web Server" /B dotnet run --project "%SERVER_PROJECT%" --no-build --urls http://localhost:5000
start "" "%DESKTOP_BIN%\%DESKTOP_EXE%"
start "" "%VIEWER_BIN%\%VIEWER_EXE%"
call :LOG "All launch commands executed."
echo [SUCCESS] All components launched. You can close this window.
exit /b 0

REM ============================================================================
REM  LOGGING FUNCTION
REM ============================================================================
:LOG
for /f "tokens=1-4 delims=/ " %%a in ('date /t') do (
    set "LOG_DATE=%%a-%%b-%%c"
)
for /f "tokens=1-2 delims=: " %%a in ('time /t') do (
    set "LOG_TIME=%%a:%%b"
)
echo [%LOG_DATE% %LOG_TIME%] %~1 >> "%MASTER_LOG%"
goto :eof
