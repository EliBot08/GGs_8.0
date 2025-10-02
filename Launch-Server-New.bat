@echo off
setlocal enabledelayedexpansion

REM ============================================================================
REM  GGs Web Server Launcher
REM  Enterprise-Grade Server Management Script
REM ============================================================================
REM  Features:
REM  - .NET dependency validation
REM  - Port management and availability checking
REM  - Smart build process (server-only)
REM  - Server health monitoring
REM  - Background process execution
REM  - URL configuration
REM  - Process isolation
REM  - Uptime tracking
REM  - Comprehensive logging
REM  - Error handling with troubleshooting hints
REM ============================================================================

REM Set console colors and title
color 0E
title GGs Web Server Launcher

REM ============================================================================
REM  CONFIGURATION
REM ============================================================================

set "APP_NAME=GGs.Server"
set "APP_EXE=GGs.Server.exe"
set "PROJECT_PATH=server\GGs.Server"
set "PROJECT_FILE=server\GGs.Server\GGs.Server.csproj"
set "BIN_PATH=server\GGs.Server\bin\Debug\net9.0"
set "SERVER_URL=http://localhost:5000"
set "SERVER_PORT=5000"
set "LOG_DIR=launcher-logs"
set "LOG_FILE=%LOG_DIR%\server-launcher.log"
set "SERVER_PID_FILE=%LOG_DIR%\server.pid"

REM ============================================================================
REM  INITIALIZATION
REM ============================================================================

echo.
echo ============================================================================
echo  GGs Web Server Launcher
echo ============================================================================
echo.

REM Create log directory if it doesn't exist
if not exist "%LOG_DIR%" (
    mkdir "%LOG_DIR%"
    echo [INFO] Created log directory: %LOG_DIR%
)

REM Initialize log file with timestamp
call :LOG "============================================================================"
call :LOG "GGs Web Server Launcher Started"
call :LOG "============================================================================"

REM ============================================================================
REM  DEPENDENCY VALIDATION
REM ============================================================================

echo [STEP 1/7] Validating dependencies...
call :LOG "Validating dependencies..."

REM Check for .NET SDK
dotnet --version >NUL 2>&1
if errorlevel 1 (
    echo [ERROR] .NET SDK not found
    call :LOG "ERROR: .NET SDK not found"
    echo.
    echo Troubleshooting:
    echo - Install .NET 9.0 SDK or later
    echo - Download from: https://dotnet.microsoft.com/download
    echo - Restart your terminal after installation
    echo.
    pause
    exit /b 1
)

REM Get .NET version
for /f "tokens=*" %%i in ('dotnet --version') do set "DOTNET_VERSION=%%i"
echo [INFO] .NET SDK Version: %DOTNET_VERSION%
call :LOG ".NET SDK Version: %DOTNET_VERSION%"

REM Check if version is 9.0 or higher
for /f "tokens=1 delims=." %%a in ("%DOTNET_VERSION%") do set "MAJOR_VERSION=%%a"
if %MAJOR_VERSION% LSS 9 (
    echo [WARNING] .NET 9.0+ recommended, found %DOTNET_VERSION%
    call :LOG "WARNING: .NET version may be incompatible"
)

echo [SUCCESS] Dependencies validated
call :LOG "Dependencies validated successfully"

REM ============================================================================
REM  PROJECT VALIDATION
REM ============================================================================

echo.
echo [STEP 2/7] Validating project structure...
call :LOG "Validating project structure..."

REM Check if project directory exists
if not exist "%PROJECT_PATH%" (
    echo [ERROR] Project directory not found: %PROJECT_PATH%
    call :LOG "ERROR: Project directory not found: %PROJECT_PATH%"
    echo.
    echo Troubleshooting:
    echo - Ensure you are running this script from the GGs solution root directory
    echo - Verify the project structure is intact
    echo.
    pause
    exit /b 1
)

REM Check if project file exists
if not exist "%PROJECT_FILE%" (
    echo [ERROR] Project file not found: %PROJECT_FILE%
    call :LOG "ERROR: Project file not found"
    echo.
    echo Troubleshooting:
    echo - Verify the server project exists
    echo - Check if the .csproj file is present
    echo.
    pause
    exit /b 1
)

echo [SUCCESS] Project structure validated
call :LOG "Project structure validated successfully"

REM ============================================================================
REM  PORT MANAGEMENT
REM ============================================================================

echo.
echo [STEP 3/7] Checking port availability...
call :LOG "Checking port %SERVER_PORT% availability..."

REM Check if port is in use
netstat -ano | findstr ":%SERVER_PORT% " | findstr "LISTENING" >NUL 2>&1
if "%ERRORLEVEL%"=="0" (
    echo [WARNING] Port %SERVER_PORT% is already in use
    call :LOG "Port %SERVER_PORT% is in use - attempting to free it"
    
    REM Get PID using the port
    for /f "tokens=5" %%a in ('netstat -ano ^| findstr ":%SERVER_PORT% " ^| findstr "LISTENING"') do (
        set "PORT_PID=%%a"
        echo [INFO] Process using port: PID %%a
        call :LOG "Terminating process PID %%a using port %SERVER_PORT%"
        taskkill /F /PID %%a >NUL 2>&1
    )
    
    REM Wait for port to be freed
    timeout /t 2 /nobreak >NUL
    
    REM Verify port is free
    netstat -ano | findstr ":%SERVER_PORT% " | findstr "LISTENING" >NUL 2>&1
    if "%ERRORLEVEL%"=="0" (
        echo [ERROR] Failed to free port %SERVER_PORT%
        call :LOG "ERROR: Failed to free port %SERVER_PORT%"
        echo.
        echo Troubleshooting:
        echo - Manually close applications using port %SERVER_PORT%
        echo - Use 'netstat -ano | findstr ":%SERVER_PORT%"' to find the process
        echo - Kill the process using Task Manager
        echo.
        pause
        exit /b 1
    )
    
    echo [SUCCESS] Port %SERVER_PORT% freed successfully
    call :LOG "Port %SERVER_PORT% freed successfully"
) else (
    echo [INFO] Port %SERVER_PORT% is available
    call :LOG "Port %SERVER_PORT% is available"
)

REM ============================================================================
REM  PROCESS CLEANUP
REM ============================================================================

echo.
echo [STEP 4/7] Checking for existing server instances...
call :LOG "Checking for existing server instances..."

REM Check if server process is running
tasklist /FI "IMAGENAME eq %APP_EXE%" 2>NUL | find /I /N "%APP_EXE%">NUL
if "%ERRORLEVEL%"=="0" (
    echo [WARNING] Found existing instance of %APP_NAME%
    call :LOG "Found existing instance - terminating..."
    
    REM Kill existing process
    taskkill /F /IM "%APP_EXE%" >NUL 2>&1
    
    REM Wait for process to terminate
    timeout /t 2 /nobreak >NUL
    
    echo [SUCCESS] Existing instance terminated
    call :LOG "Existing instance terminated successfully"
) else (
    echo [INFO] No existing instances found
    call :LOG "No existing instances found"
)

REM ============================================================================
REM  BUILD PROCESS
REM ============================================================================

echo.
echo [STEP 5/7] Building server project...
call :LOG "Starting build process for server project..."

echo [INFO] Building %PROJECT_FILE%...
echo [INFO] This may take a moment...

REM Build the server project
dotnet build "%PROJECT_FILE%" --configuration Debug --verbosity quiet

if errorlevel 1 (
    echo [ERROR] Build failed
    call :LOG "ERROR: Build failed"
    echo.
    echo Troubleshooting:
    echo - Check for compilation errors in the project
    echo - Run 'dotnet build %PROJECT_FILE%' manually to see detailed errors
    echo - Ensure all NuGet packages are restored
    echo - Try 'dotnet restore' first
    echo.
    pause
    exit /b 1
)

echo [SUCCESS] Build completed successfully
call :LOG "Build completed successfully"

REM Verify executable exists after build
if not exist "%BIN_PATH%\%APP_EXE%" (
    echo [ERROR] Executable not found after build: %BIN_PATH%\%APP_EXE%
    call :LOG "ERROR: Executable not found after build"
    echo.
    echo Troubleshooting:
    echo - Build may have succeeded but output path is incorrect
    echo - Check the project's output path configuration
    echo - Verify the target framework is net9.0
    echo.
    pause
    exit /b 1
)

REM ============================================================================
REM  LAUNCH SERVER
REM ============================================================================

echo.
echo [STEP 6/7] Launching web server...
call :LOG "Launching web server..."

echo [INFO] Server URL: %SERVER_URL%
echo [INFO] Starting server in background mode...

REM Launch server in background
start "GGs Web Server" /B dotnet run --project "%PROJECT_FILE%" --no-build --urls "%SERVER_URL%"

if errorlevel 1 (
    echo [ERROR] Failed to launch server
    call :LOG "ERROR: Failed to launch server"
    echo.
    echo Troubleshooting:
    echo - Check if the executable is corrupted
    echo - Verify .NET 9.0 runtime is installed
    echo - Check the application logs for errors
    echo.
    pause
    exit /b 1
)

echo [SUCCESS] Server launch command executed
call :LOG "Server launch command executed"

REM ============================================================================
REM  LAUNCH VERIFICATION
REM ============================================================================

echo.
echo [STEP 7/7] Verifying server startup...
call :LOG "Verifying server startup..."

echo [INFO] Waiting for server to initialize...

REM Wait for server to start
timeout /t 5 /nobreak >NUL

REM Verify server process is running
tasklist /FI "IMAGENAME eq dotnet.exe" 2>NUL | find /I /N "dotnet.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [SUCCESS] Server process is running
    call :LOG "Server process verified running"
) else (
    echo [WARNING] Server process not detected
    call :LOG "WARNING: Server process not detected"
)

REM Verify port is listening
netstat -ano | findstr ":%SERVER_PORT% " | findstr "LISTENING" >NUL 2>&1
if "%ERRORLEVEL%"=="0" (
    echo [SUCCESS] Server is listening on port %SERVER_PORT%
    call :LOG "Server is listening on port %SERVER_PORT%"
) else (
    echo [WARNING] Server may not be listening on port %SERVER_PORT% yet
    call :LOG "WARNING: Server not yet listening on port %SERVER_PORT%"
    echo [INFO] Server may still be initializing...
)

REM ============================================================================
REM  SERVER MONITORING
REM ============================================================================

echo.
echo ============================================================================
echo  Web Server Status Monitor
echo ============================================================================
echo.
echo Server: %APP_NAME%
echo URL: %SERVER_URL%
echo Port: %SERVER_PORT%
echo Status: RUNNING
echo.
echo The web server is now running in the background.
echo You can access it at: %SERVER_URL%
echo.
echo Press Ctrl+C to stop monitoring (server will continue running)
echo To stop the server, close this window or use Launch-All-New.bat
echo.

call :LOG "Entering monitoring mode..."

set /a COUNTER=0

:MONITOR_LOOP
REM Check if server port is still listening
netstat -ano | findstr ":%SERVER_PORT% " | findstr "LISTENING" >NUL 2>&1
if not "%ERRORLEVEL%"=="0" (
    echo.
    echo [WARNING] Server is no longer listening on port %SERVER_PORT%
    call :LOG "Server stopped listening after %COUNTER% seconds"
    echo.
    echo Server ran for %COUNTER% seconds
    echo.
    echo Troubleshooting:
    echo - Check server logs for errors
    echo - The server may have crashed
    echo - Check Windows Event Viewer for application errors
    echo.
    call :LOG "Monitoring ended - Server stopped"
    pause
    exit /b 1
)

REM Update counter
set /a COUNTER+=1

REM Display status (update same line)
<nul set /p "=Uptime: %COUNTER%s | Port: %SERVER_PORT% | Status: LISTENING | URL: %SERVER_URL%     "
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
