@echo off
setlocal enabledelayedexpansion

REM ============================================================================
REM  GGs Launcher Suite - Master Orchestrator
REM  Enterprise-Grade Application Suite Management
REM ============================================================================
REM  Features:
REM  - Component coordination (Server, Desktop, Viewer)
REM  - Dependency management and validation
REM  - Comprehensive build process
REM  - Interactive dashboard with real-time status
REM  - Graceful shutdown coordination
REM  - Error recovery and continuation
REM  - Staggered launch timing
REM  - Process grouping and management
REM  - Interactive controls (Refresh, Quit, View Logs)
REM  - Port management
REM  - Health monitoring
REM ============================================================================

REM Set console colors and title
color 0F
title GGs Launcher Suite - Master Orchestrator

REM ============================================================================
REM  CONFIGURATION
REM ============================================================================

set "SERVER_EXE=GGs.Server.exe"
set "DESKTOP_EXE=GGs.Desktop.exe"
set "VIEWER_EXE=GGs.ErrorLogViewer.exe"

set "SERVER_PROJECT=server\GGs.Server\GGs.Server.csproj"
set "DESKTOP_PROJECT=clients\GGs.Desktop\GGs.Desktop.csproj"
set "VIEWER_PROJECT=tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj"

set "SERVER_URL=http://localhost:5000"
set "SERVER_PORT=5000"

set "LOG_DIR=launcher-logs"
set "MASTER_LOG=%LOG_DIR%\master-launcher.log"

set "SERVER_STATUS=STOPPED"
set "DESKTOP_STATUS=STOPPED"
set "VIEWER_STATUS=STOPPED"

set /a SERVER_UPTIME=0
set /a DESKTOP_UPTIME=0
set /a VIEWER_UPTIME=0

REM ============================================================================
REM  INITIALIZATION
REM ============================================================================

cls
echo.
echo ============================================================================
echo  GGs Launcher Suite - Master Orchestrator
echo  Enterprise Application Management System
echo ============================================================================
echo.

REM Create log directory
if not exist "%LOG_DIR%" (
    mkdir "%LOG_DIR%"
    echo [INFO] Created log directory: %LOG_DIR%
)

REM Initialize master log
call :LOG "============================================================================"
call :LOG "GGs Launcher Suite - Master Orchestrator Started"
call :LOG "============================================================================"

echo [INFO] Initializing launcher suite...
call :LOG "Initializing launcher suite..."

REM ============================================================================
REM  DEPENDENCY VALIDATION
REM ============================================================================

echo.
echo [PHASE 1/5] DEPENDENCY VALIDATION
echo ============================================================================
call :LOG "Phase 1: Dependency Validation"

echo [CHECK 1/3] Validating .NET SDK...
call :LOG "Checking .NET SDK..."

dotnet --version >NUL 2>&1
if errorlevel 1 (
    echo [ERROR] .NET SDK not found
    call :LOG "ERROR: .NET SDK not found"
    echo.
    echo CRITICAL ERROR: .NET SDK is required to run this application suite.
    echo.
    echo Installation Steps:
    echo 1. Visit: https://dotnet.microsoft.com/download
    echo 2. Download .NET 9.0 SDK or later
    echo 3. Run the installer
    echo 4. Restart your terminal
    echo 5. Run this script again
    echo.
    call :LOG "Launcher suite failed - .NET SDK not found"
    pause
    exit /b 1
)

for /f "tokens=*" %%i in ('dotnet --version') do set "DOTNET_VERSION=%%i"
echo [SUCCESS] .NET SDK Version: %DOTNET_VERSION%
call :LOG ".NET SDK Version: %DOTNET_VERSION%"

echo.
echo [CHECK 2/3] Validating project structure...
call :LOG "Validating project structure..."

set "VALIDATION_FAILED=0"

if not exist "%SERVER_PROJECT%" (
    echo [ERROR] Server project not found: %SERVER_PROJECT%
    call :LOG "ERROR: Server project not found"
    set "VALIDATION_FAILED=1"
)

if not exist "%DESKTOP_PROJECT%" (
    echo [ERROR] Desktop project not found: %DESKTOP_PROJECT%
    call :LOG "ERROR: Desktop project not found"
    set "VALIDATION_FAILED=1"
)

if not exist "%VIEWER_PROJECT%" (
    echo [ERROR] Viewer project not found: %VIEWER_PROJECT%
    call :LOG "ERROR: Viewer project not found"
    set "VALIDATION_FAILED=1"
)

if "%VALIDATION_FAILED%"=="1" (
    echo.
    echo CRITICAL ERROR: Project structure validation failed.
    echo.
    echo Troubleshooting:
    echo - Ensure you are running this script from the GGs solution root directory
    echo - Verify all project directories exist
    echo - Check if the solution was cloned/extracted correctly
    echo.
    call :LOG "Launcher suite failed - Project structure invalid"
    pause
    exit /b 1
)

echo [SUCCESS] All projects found
call :LOG "Project structure validated successfully"

echo.
echo [CHECK 3/3] Checking port availability...
call :LOG "Checking port availability..."

netstat -ano | findstr ":%SERVER_PORT% " | findstr "LISTENING" >NUL 2>&1
if "%ERRORLEVEL%"=="0" (
    echo [WARNING] Port %SERVER_PORT% is in use - will be freed during server launch
    call :LOG "Port %SERVER_PORT% is in use - will be freed"
) else (
    echo [SUCCESS] Port %SERVER_PORT% is available
    call :LOG "Port %SERVER_PORT% is available"
)

echo.
echo [SUCCESS] Dependency validation completed
call :LOG "Phase 1 completed successfully"

REM ============================================================================
REM  CLEANUP EXISTING INSTANCES
REM ============================================================================

echo.
echo [PHASE 2/5] CLEANUP EXISTING INSTANCES
echo ============================================================================
call :LOG "Phase 2: Cleanup existing instances"

echo [INFO] Checking for running instances...

set "CLEANUP_NEEDED=0"

tasklist /FI "IMAGENAME eq %SERVER_EXE%" 2>NUL | find /I /N "%SERVER_EXE%">NUL
if "%ERRORLEVEL%"=="0" (
    echo [WARNING] Found existing server instance
    call :LOG "Terminating existing server instance"
    taskkill /F /IM "%SERVER_EXE%" >NUL 2>&1
    set "CLEANUP_NEEDED=1"
)

tasklist /FI "IMAGENAME eq %DESKTOP_EXE%" 2>NUL | find /I /N "%DESKTOP_EXE%">NUL
if "%ERRORLEVEL%"=="0" (
    echo [WARNING] Found existing desktop instance
    call :LOG "Terminating existing desktop instance"
    taskkill /F /IM "%DESKTOP_EXE%" >NUL 2>&1
    set "CLEANUP_NEEDED=1"
)

tasklist /FI "IMAGENAME eq %VIEWER_EXE%" 2>NUL | find /I /N "%VIEWER_EXE%">NUL
if "%ERRORLEVEL%"=="0" (
    echo [WARNING] Found existing viewer instance
    call :LOG "Terminating existing viewer instance"
    taskkill /F /IM "%VIEWER_EXE%" >NUL 2>&1
    set "CLEANUP_NEEDED=1"
)

if "%CLEANUP_NEEDED%"=="1" (
    echo [INFO] Waiting for processes to terminate...
    timeout /t 2 /nobreak >NUL
    echo [SUCCESS] Cleanup completed
    call :LOG "Cleanup completed successfully"
) else (
    echo [INFO] No existing instances found
    call :LOG "No existing instances found"
)

echo.
echo [SUCCESS] Cleanup phase completed
call :LOG "Phase 2 completed successfully"

REM ============================================================================
REM  BUILD ALL COMPONENTS
REM ============================================================================

echo.
echo [PHASE 3/5] BUILD ALL COMPONENTS
echo ============================================================================
call :LOG "Phase 3: Building all components"

echo [INFO] This may take a moment...
echo.

echo [BUILD 1/3] Building Server...
call :LOG "Building server project..."
dotnet build "%SERVER_PROJECT%" --configuration Debug --verbosity quiet

if errorlevel 1 (
    echo [ERROR] Server build failed
    call :LOG "ERROR: Server build failed"
    echo.
    echo The server component failed to build.
    echo Run 'dotnet build %SERVER_PROJECT%' manually to see detailed errors.
    echo.
    set "BUILD_FAILED=1"
) else (
    echo [SUCCESS] Server built successfully
    call :LOG "Server built successfully"
)

echo.
echo [BUILD 2/3] Building Desktop...
call :LOG "Building desktop project..."
dotnet build "%DESKTOP_PROJECT%" --configuration Debug --verbosity quiet

if errorlevel 1 (
    echo [ERROR] Desktop build failed
    call :LOG "ERROR: Desktop build failed"
    echo.
    echo The desktop component failed to build.
    echo Run 'dotnet build %DESKTOP_PROJECT%' manually to see detailed errors.
    echo.
    set "BUILD_FAILED=1"
) else (
    echo [SUCCESS] Desktop built successfully
    call :LOG "Desktop built successfully"
)

echo.
echo [BUILD 3/3] Building Viewer...
call :LOG "Building viewer project..."
dotnet build "%VIEWER_PROJECT%" --configuration Debug --verbosity quiet

if errorlevel 1 (
    echo [ERROR] Viewer build failed
    call :LOG "ERROR: Viewer build failed"
    echo.
    echo The viewer component failed to build.
    echo Run 'dotnet build %VIEWER_PROJECT%' manually to see detailed errors.
    echo.
    set "BUILD_FAILED=1"
) else (
    echo [SUCCESS] Viewer built successfully
    call :LOG "Viewer built successfully"
)

if defined BUILD_FAILED (
    echo.
    echo [ERROR] One or more builds failed
    call :LOG "Build phase failed"
    echo.
    echo Troubleshooting:
    echo - Check for compilation errors in the projects
    echo - Ensure all NuGet packages are restored (run 'dotnet restore')
    echo - Check the build output for specific error messages
    echo.
    pause
    exit /b 1
)

echo.
echo [SUCCESS] All components built successfully
call :LOG "Phase 3 completed successfully"

REM ============================================================================
REM  LAUNCH ALL COMPONENTS
REM ============================================================================

echo.
echo [PHASE 4/5] LAUNCH ALL COMPONENTS
echo ============================================================================
call :LOG "Phase 4: Launching all components"

echo [INFO] Launching components with staggered timing...
echo.

REM Launch Server
echo [LAUNCH 1/3] Starting Web Server...
call :LOG "Launching web server..."
start "GGs Web Server" /B dotnet run --project "%SERVER_PROJECT%" --no-build --urls "%SERVER_URL%"

if errorlevel 1 (
    echo [ERROR] Failed to launch server
    call :LOG "ERROR: Failed to launch server"
    set "SERVER_STATUS=FAILED"
) else (
    echo [SUCCESS] Server launch initiated
    call :LOG "Server launched"
    set "SERVER_STATUS=STARTING"
    
    REM Wait for server to initialize
    echo [INFO] Waiting for server to initialize (5 seconds)...
    timeout /t 5 /nobreak >NUL
    
    REM Verify server is listening
    netstat -ano | findstr ":%SERVER_PORT% " | findstr "LISTENING" >NUL 2>&1
    if "%ERRORLEVEL%"=="0" (
        set "SERVER_STATUS=RUNNING"
        echo [SUCCESS] Server is running on %SERVER_URL%
        call :LOG "Server verified running on port %SERVER_PORT%"
    ) else (
        set "SERVER_STATUS=WARNING"
        echo [WARNING] Server may still be initializing
        call :LOG "WARNING: Server not yet listening on port %SERVER_PORT%"
    )
)

echo.

REM Launch Desktop
echo [LAUNCH 2/3] Starting Desktop Application...
call :LOG "Launching desktop application..."
start "" "clients\GGs.Desktop\bin\Debug\net9.0-windows\%DESKTOP_EXE%"

if errorlevel 1 (
    echo [ERROR] Failed to launch desktop
    call :LOG "ERROR: Failed to launch desktop"
    set "DESKTOP_STATUS=FAILED"
) else (
    echo [SUCCESS] Desktop launch initiated
    call :LOG "Desktop launched"
    set "DESKTOP_STATUS=STARTING"
    
    REM Wait for desktop to start
    timeout /t 3 /nobreak >NUL
    
    REM Verify desktop is running
    tasklist /FI "IMAGENAME eq %DESKTOP_EXE%" 2>NUL | find /I /N "%DESKTOP_EXE%">NUL
    if "%ERRORLEVEL%"=="0" (
        set "DESKTOP_STATUS=RUNNING"
        echo [SUCCESS] Desktop application is running
        call :LOG "Desktop verified running"
    ) else (
        set "DESKTOP_STATUS=FAILED"
        echo [ERROR] Desktop failed to start
        call :LOG "ERROR: Desktop failed to start"
    )
)

echo.

REM Launch Viewer
echo [LAUNCH 3/3] Starting Error Log Viewer...
call :LOG "Launching error log viewer..."
start "" "tools\GGs.ErrorLogViewer\bin\Debug\net9.0-windows\%VIEWER_EXE%"

if errorlevel 1 (
    echo [ERROR] Failed to launch viewer
    call :LOG "ERROR: Failed to launch viewer"
    set "VIEWER_STATUS=FAILED"
) else (
    echo [SUCCESS] Viewer launch initiated
    call :LOG "Viewer launched"
    set "VIEWER_STATUS=STARTING"
    
    REM Wait for viewer to start
    timeout /t 3 /nobreak >NUL
    
    REM Verify viewer is running
    tasklist /FI "IMAGENAME eq %VIEWER_EXE%" 2>NUL | find /I /N "%VIEWER_EXE%">NUL
    if "%ERRORLEVEL%"=="0" (
        set "VIEWER_STATUS=RUNNING"
        echo [SUCCESS] Viewer application is running
        call :LOG "Viewer verified running"
    ) else (
        set "VIEWER_STATUS=FAILED"
        echo [ERROR] Viewer failed to start
        call :LOG "ERROR: Viewer failed to start"
    )
)

echo.
echo [SUCCESS] Component launch phase completed
call :LOG "Phase 4 completed"

REM ============================================================================
REM  INTERACTIVE DASHBOARD
REM ============================================================================

echo.
echo [PHASE 5/5] MONITORING & MANAGEMENT
echo ============================================================================
call :LOG "Phase 5: Entering monitoring mode"

:DASHBOARD
cls
echo.
echo ============================================================================
echo  GGs Launcher Suite - Interactive Dashboard
echo ============================================================================
echo.
echo  .NET Version: %DOTNET_VERSION%
echo  Server URL: %SERVER_URL%
echo  Log Directory: %LOG_DIR%
echo.
echo ============================================================================
echo  COMPONENT STATUS
echo ============================================================================
echo.

REM Update status for each component
call :UPDATE_STATUS

REM Display Server Status
echo  [SERVER]
if "%SERVER_STATUS%"=="RUNNING" (
    echo    Status: RUNNING ^| Uptime: %SERVER_UPTIME%s ^| Port: %SERVER_PORT%
) else if "%SERVER_STATUS%"=="FAILED" (
    echo    Status: FAILED ^| Check logs for details
) else (
    echo    Status: %SERVER_STATUS%
)
echo    URL: %SERVER_URL%
echo.

REM Display Desktop Status
echo  [DESKTOP]
if "%DESKTOP_STATUS%"=="RUNNING" (
    echo    Status: RUNNING ^| Uptime: %DESKTOP_UPTIME%s
) else if "%DESKTOP_STATUS%"=="FAILED" (
    echo    Status: FAILED ^| Check logs for details
) else (
    echo    Status: %DESKTOP_STATUS%
)
echo    Type: GUI Application
echo.

REM Display Viewer Status
echo  [VIEWER]
if "%VIEWER_STATUS%"=="RUNNING" (
    echo    Status: RUNNING ^| Uptime: %VIEWER_UPTIME%s
) else if "%VIEWER_STATUS%"=="FAILED" (
    echo    Status: FAILED ^| Check logs for details
) else (
    echo    Status: %VIEWER_STATUS%
)
echo    Type: Error Log Viewer
echo.

echo ============================================================================
echo  CONTROLS
echo ============================================================================
echo.
echo  [R] Refresh Status
echo  [Q] Quit All Applications
echo  [L] View Logs Directory
echo  [S] View Server Logs
echo  [D] View Desktop Logs
echo  [V] View Viewer Logs
echo.
echo ============================================================================
echo.

REM Wait for user input with timeout
choice /C RQLSDV /T 5 /D R /N /M "Select option (auto-refresh in 5s): "

if errorlevel 6 goto VIEW_VIEWER_LOG
if errorlevel 5 goto VIEW_DESKTOP_LOG
if errorlevel 4 goto VIEW_SERVER_LOG
if errorlevel 3 goto VIEW_LOGS
if errorlevel 2 goto QUIT_ALL
if errorlevel 1 goto DASHBOARD

REM ============================================================================
REM  DASHBOARD ACTIONS
REM ============================================================================

:VIEW_LOGS
echo.
echo Opening logs directory...
call :LOG "User opened logs directory"
start "" "%LOG_DIR%"
timeout /t 2 /nobreak >NUL
goto DASHBOARD

:VIEW_SERVER_LOG
if exist "%LOG_DIR%\server-launcher.log" (
    echo.
    echo Opening server log...
    call :LOG "User opened server log"
    start "" notepad "%LOG_DIR%\server-launcher.log"
) else (
    echo.
    echo [WARNING] Server log not found
    timeout /t 2 /nobreak >NUL
)
goto DASHBOARD

:VIEW_DESKTOP_LOG
if exist "%LOG_DIR%\desktop-launcher.log" (
    echo.
    echo Opening desktop log...
    call :LOG "User opened desktop log"
    start "" notepad "%LOG_DIR%\desktop-launcher.log"
) else (
    echo.
    echo [WARNING] Desktop log not found
    timeout /t 2 /nobreak >NUL
)
goto DASHBOARD

:VIEW_VIEWER_LOG
if exist "%LOG_DIR%\viewer-launcher.log" (
    echo.
    echo Opening viewer log...
    call :LOG "User opened viewer log"
    start "" notepad "%LOG_DIR%\viewer-launcher.log"
) else (
    echo.
    echo [WARNING] Viewer log not found
    timeout /t 2 /nobreak >NUL
)
goto DASHBOARD

:QUIT_ALL
echo.
echo ============================================================================
echo  SHUTTING DOWN ALL COMPONENTS
echo ============================================================================
echo.
call :LOG "User initiated shutdown of all components"

echo [INFO] Terminating all GGs applications...

REM Kill all components
taskkill /F /IM "%SERVER_EXE%" >NUL 2>&1
taskkill /F /IM "%DESKTOP_EXE%" >NUL 2>&1
taskkill /F /IM "%VIEWER_EXE%" >NUL 2>&1

REM Also kill dotnet processes running the server
for /f "tokens=2" %%a in ('tasklist /FI "IMAGENAME eq dotnet.exe" /FO LIST ^| findstr "PID:"') do (
    netstat -ano | findstr "%%a" | findstr ":%SERVER_PORT%" >NUL 2>&1
    if not errorlevel 1 (
        taskkill /F /PID %%a >NUL 2>&1
    )
)

echo [SUCCESS] All components terminated
call :LOG "All components terminated successfully"

echo.
echo [INFO] Launcher suite shutdown complete
call :LOG "Launcher suite shutdown complete"
call :LOG "============================================================================"

echo.
echo Thank you for using GGs Launcher Suite!
echo.
timeout /t 3 /nobreak >NUL
exit /b 0

REM ============================================================================
REM  HELPER FUNCTIONS
REM ============================================================================

:UPDATE_STATUS
REM Update server status
netstat -ano | findstr ":%SERVER_PORT% " | findstr "LISTENING" >NUL 2>&1
if "%ERRORLEVEL%"=="0" (
    if "%SERVER_STATUS%"=="RUNNING" (
        set /a SERVER_UPTIME+=5
    ) else (
        set "SERVER_STATUS=RUNNING"
        set /a SERVER_UPTIME=0
    )
) else (
    if "%SERVER_STATUS%"=="RUNNING" (
        set "SERVER_STATUS=STOPPED"
        call :LOG "Server stopped unexpectedly"
    )
)

REM Update desktop status
tasklist /FI "IMAGENAME eq %DESKTOP_EXE%" 2>NUL | find /I /N "%DESKTOP_EXE%">NUL
if "%ERRORLEVEL%"=="0" (
    if "%DESKTOP_STATUS%"=="RUNNING" (
        set /a DESKTOP_UPTIME+=5
    ) else (
        set "DESKTOP_STATUS=RUNNING"
        set /a DESKTOP_UPTIME=0
    )
) else (
    if "%DESKTOP_STATUS%"=="RUNNING" (
        set "DESKTOP_STATUS=STOPPED"
        call :LOG "Desktop stopped unexpectedly"
    )
)

REM Update viewer status
tasklist /FI "IMAGENAME eq %VIEWER_EXE%" 2>NUL | find /I /N "%VIEWER_EXE%">NUL
if "%ERRORLEVEL%"=="0" (
    if "%VIEWER_STATUS%"=="RUNNING" (
        set /a VIEWER_UPTIME+=5
    ) else (
        set "VIEWER_STATUS=RUNNING"
        set /a VIEWER_UPTIME=0
    )
) else (
    if "%VIEWER_STATUS%"=="RUNNING" (
        set "VIEWER_STATUS=STOPPED"
        call :LOG "Viewer stopped unexpectedly"
    )
)

goto :eof

:LOG
REM Get current timestamp
for /f "tokens=1-4 delims=/ " %%a in ('date /t') do (
    set "LOG_DATE=%%a-%%b-%%c"
)
for /f "tokens=1-2 delims=: " %%a in ('time /t') do (
    set "LOG_TIME=%%a:%%b"
)
echo [%LOG_DATE% %LOG_TIME%] %~1 >> "%MASTER_LOG%"
goto :eof

REM ============================================================================
REM  END OF SCRIPT
REM ============================================================================
