@echo off
setlocal enabledelayedexpansion

REM ============================================================================
REM  GGs Error Log Viewer Launcher
REM  Enterprise-Grade Application Management Script
REM ============================================================================
REM  Features:
REM  - Application path validation
REM  - Process cleanup and management
REM  - Launch verification
REM  - Real-time monitoring with uptime counter
REM  - Shutdown detection
REM  - Comprehensive logging
REM  - Error handling with troubleshooting hints
REM ============================================================================

REM Set console colors and title
color 0A
title GGs Error Log Viewer Launcher

REM ============================================================================
REM  CONFIGURATION
REM ============================================================================

set "APP_NAME=GGs.ErrorLogViewer"
set "APP_EXE=GGs.ErrorLogViewer.exe"
set "PROJECT_PATH=tools\GGs.ErrorLogViewer"
set "BIN_PATH=tools\GGs.ErrorLogViewer\bin\Debug\net9.0-windows"
set "LOG_DIR=launcher-logs"
set "LOG_FILE=%LOG_DIR%\viewer-launcher.log"

REM ============================================================================
REM  INITIALIZATION
REM ============================================================================

echo.
echo ============================================================================
echo  GGs Error Log Viewer Launcher
echo ============================================================================
echo.

REM Create log directory if it doesn't exist
if not exist "%LOG_DIR%" (
    mkdir "%LOG_DIR%"
    echo [INFO] Created log directory: %LOG_DIR%
)

REM Initialize log file with timestamp
call :LOG "============================================================================"
call :LOG "GGs Error Log Viewer Launcher Started"
call :LOG "============================================================================"

REM ============================================================================
REM  VALIDATION
REM ============================================================================

echo [STEP 1/5] Validating application path...
call :LOG "Validating application path..."

REM Check if project directory exists
if not exist "%PROJECT_PATH%" (
    echo [ERROR] Project directory not found: %PROJECT_PATH%
    call :LOG "ERROR: Project directory not found: %PROJECT_PATH%"
    echo.
    echo Troubleshooting:
    echo - Ensure you are running this script from the GGs solution root directory
    echo - Verify the project structure is intact
    echo.
    call :LOG "Launcher failed - Project directory not found"
    pause
    exit /b 1
)

REM Check if executable exists
if not exist "%BIN_PATH%\%APP_EXE%" (
    echo [ERROR] Application executable not found: %BIN_PATH%\%APP_EXE%
    call :LOG "ERROR: Application executable not found"
    echo.
    echo Troubleshooting:
    echo - The application needs to be built first
    echo - Run: dotnet build %PROJECT_PATH%\GGs.ErrorLogViewer.csproj
    echo - Or use Launch-All-New.bat to build and launch all components
    echo.
    call :LOG "Launcher failed - Executable not found"
    pause
    exit /b 1
)

echo [SUCCESS] Application path validated
call :LOG "Application path validated successfully"

REM ============================================================================
REM  PROCESS CLEANUP
REM ============================================================================

echo.
echo [STEP 2/5] Checking for existing instances...
call :LOG "Checking for existing instances..."

REM Check if process is running
tasklist /FI "IMAGENAME eq %APP_EXE%" 2>NUL | find /I /N "%APP_EXE%">NUL
if "%ERRORLEVEL%"=="0" (
    echo [WARNING] Found existing instance of %APP_NAME%
    call :LOG "Found existing instance - terminating..."
    
    REM Kill existing process
    taskkill /F /IM "%APP_EXE%" >NUL 2>&1
    
    REM Wait for process to terminate
    timeout /t 2 /nobreak >NUL
    
    REM Verify termination
    tasklist /FI "IMAGENAME eq %APP_EXE%" 2>NUL | find /I /N "%APP_EXE%">NUL
    if "%ERRORLEVEL%"=="0" (
        echo [ERROR] Failed to terminate existing instance
        call :LOG "ERROR: Failed to terminate existing instance"
        pause
        exit /b 1
    )
    
    echo [SUCCESS] Existing instance terminated
    call :LOG "Existing instance terminated successfully"
) else (
    echo [INFO] No existing instances found
    call :LOG "No existing instances found"
)

REM ============================================================================
REM  LAUNCH APPLICATION
REM ============================================================================

echo.
echo [STEP 3/5] Launching %APP_NAME%...
call :LOG "Launching application..."

REM Launch the application
start "" "%BIN_PATH%\%APP_EXE%"

if errorlevel 1 (
    echo [ERROR] Failed to launch application
    call :LOG "ERROR: Failed to launch application"
    echo.
    echo Troubleshooting:
    echo - Check if the executable is corrupted
    echo - Verify .NET 9.0 runtime is installed
    echo - Check Windows Event Viewer for application errors
    echo.
    pause
    exit /b 1
)

echo [SUCCESS] Launch command executed
call :LOG "Launch command executed"

REM ============================================================================
REM  LAUNCH VERIFICATION
REM ============================================================================

echo.
echo [STEP 4/5] Verifying application startup...
call :LOG "Verifying application startup..."

REM Wait for application to start
timeout /t 3 /nobreak >NUL

REM Verify process is running
tasklist /FI "IMAGENAME eq %APP_EXE%" 2>NUL | find /I /N "%APP_EXE%">NUL
if "%ERRORLEVEL%"=="0" (
    echo [SUCCESS] Application started successfully
    call :LOG "Application verified running"
) else (
    echo [ERROR] Application failed to start
    call :LOG "ERROR: Application failed to start"
    echo.
    echo Troubleshooting:
    echo - The application may have crashed immediately
    echo - Check the application's error logs
    echo - Verify all dependencies are installed
    echo.
    pause
    exit /b 1
)

REM ============================================================================
REM  REAL-TIME MONITORING
REM ============================================================================

echo.
echo [STEP 5/5] Monitoring application...
call :LOG "Entering monitoring mode..."
echo.
echo ============================================================================
echo  Application Status Monitor
echo ============================================================================
echo.
echo Application: %APP_NAME%
echo Status: RUNNING
echo.
echo Press Ctrl+C to stop monitoring (application will continue running)
echo.

set /a COUNTER=0

:MONITOR_LOOP
REM Check if process is still running
tasklist /FI "IMAGENAME eq %APP_EXE%" 2>NUL | find /I /N "%APP_EXE%">NUL
if not "%ERRORLEVEL%"=="0" (
    echo.
    echo [INFO] Application has been closed by user
    call :LOG "Application closed by user after %COUNTER% seconds"
    echo.
    echo Application ran for %COUNTER% seconds
    echo.
    call :LOG "Monitoring ended - Launcher exiting"
    timeout /t 3 /nobreak >NUL
    exit /b 0
)

REM Update counter
set /a COUNTER+=1

REM Display status (update same line)
<nul set /p "=Runtime: %COUNTER% seconds | Status: RUNNING | Press Ctrl+C to exit monitor     "
echo.

REM Wait 1 second
timeout /t 1 /nobreak >NUL

REM Move cursor up to overwrite the line
for /l %%i in (1,1,1) do echo.

goto MONITOR_LOOP

REM ============================================================================
REM  LOGGING FUNCTION
REM ============================================================================

:LOG
REM Get current timestamp
for /f "tokens=1-4 delims=/ " %%a in ('date /t') do (
    set "LOG_DATE=%%a-%%b-%%c"
)
for /f "tokens=1-2 delims=: " %%a in ('time /t') do (
    set "LOG_TIME=%%a:%%b"
)
echo [%LOG_DATE% %LOG_TIME%] %~1 >> "%LOG_FILE%"
goto :eof

REM ============================================================================
REM  END OF SCRIPT
REM ============================================================================
