@echo off
setlocal enabledelayedexpansion

REM ============================================================================
REM  GGs Web Server Launcher - Enterprise Edition (Process-Safe)
REM ============================================================================
REM  Kills any running instance of GGs.Server.exe before building to avoid file lock errors.
REM ============================================================================

REM Set console colors and title
color 0E
set "LAUNCHER_TITLE=GGs Web Server Launcher - Enterprise Edition"
title %LAUNCHER_TITLE%

REM CONFIGURATION
set "APP_NAME=GGs.Server"
set "APP_EXE=GGs.Server.exe"
set "PROJECT_PATH=server\GGs.Server"
set "PROJECT_FILE=server\GGs.Server\GGs.Server.csproj"
set "BIN_PATH=server\GGs.Server\bin\Debug\net9.0"
set "SERVER_URL=http://localhost:5000"
set "LOG_DIR=launcher-logs"
set "LOG_FILE=%LOG_DIR%\server-launcher.log"
set "BUILD_LOG=%LOG_DIR%\server-build.log"

REM INITIALIZATION
if not exist "%LOG_DIR%" (
    mkdir "%LOG_DIR%"
    echo [INFO] Created log directory: %LOG_DIR%
)

call :LOG "============================================================================"
call :LOG "%LAUNCHER_TITLE% Started"
call :LOG "============================================================================"

REM ============================================================================
REM  STEP 0: KILL RUNNING INSTANCE BEFORE BUILD
REM ============================================================================
echo.
echo [STEP 0/4] Checking for running server instances...
call :LOG "Checking for running server instances..."
tasklist /FI "IMAGENAME eq %APP_EXE%" 2>NUL | find /I /N "%APP_EXE%">NUL
if "%ERRORLEVEL%"=="0" (
    echo [INFO] Found running instance of %APP_NAME%, terminating...
    call :LOG "Found running instance, terminating..."
    taskkill /F /IM "%APP_EXE%" >NUL 2>&1
    REM Wait for process to exit
    set /a WAIT_COUNT=0
    :WAIT_KILL
    timeout /t 1 /nobreak >NUL
    tasklist /FI "IMAGENAME eq %APP_EXE%" 2>NUL | find /I /N "%APP_EXE%">NUL
    if "%ERRORLEVEL%"=="0" (
        set /a WAIT_COUNT+=1
        if %WAIT_COUNT% GEQ 10 (
            echo [ERROR] Failed to terminate %APP_NAME% after 10 seconds.
            call :LOG "ERROR: Failed to terminate %APP_NAME% after 10 seconds."
            pause
            exit /b 1
        )
        goto WAIT_KILL
    )
    echo [SUCCESS] Previous instance terminated.
    call :LOG "Previous instance terminated."
) else (
    echo [INFO] No running instances found.
    call :LOG "No running instances found."
)

REM ============================================================================
REM  STEP 1: BUILD LATEST CODE
REM ============================================================================
echo.
echo [STEP 1/4] Building latest Web Server code...
call :LOG "Building project: %PROJECT_FILE%"

REM Clean previous build
call :LOG "Cleaning previous build..."
dotnet clean "%PROJECT_FILE%" --verbosity quiet > "%BUILD_LOG%" 2>&1

REM Build project
call :LOG "Running dotnet build..."
dotnet build "%PROJECT_FILE%" --configuration Debug --verbosity minimal >> "%BUILD_LOG%" 2>&1
if errorlevel 1 (
    echo [ERROR] Build failed. See %BUILD_LOG% for details.
    call :LOG "ERROR: Build failed. Aborting launch."
    type "%BUILD_LOG%"
    echo.
    pause
    exit /b 1
)
echo [SUCCESS] Build completed successfully.
call :LOG "Build completed successfully."

REM ============================================================================
REM  STEP 2: VALIDATE EXECUTABLE
REM ============================================================================
if not exist "%BIN_PATH%\%APP_EXE%" (
    echo [ERROR] Application executable not found after build: %BIN_PATH%\%APP_EXE%
    call :LOG "ERROR: Executable not found after build. Aborting launch."
    echo.
    pause
    exit /b 1
)
echo [SUCCESS] Executable found: %BIN_PATH%\%APP_EXE%"
call :LOG "Executable found after build."

REM ============================================================================
REM  STEP 3: PRINT BUILD INFO
REM ============================================================================
set "BUILD_TIME=%DATE% %TIME%"
echo.
echo ============================================================================
echo  %LAUNCHER_TITLE%
echo ============================================================================
echo  Build Time: %BUILD_TIME%
echo  Project:    %PROJECT_FILE%
echo  Executable: %BIN_PATH%\%APP_EXE%"
echo ============================================================================
echo.
call :LOG "Build time: %BUILD_TIME%"

REM Try to print app version from AssemblyInfo if available
for /f "tokens=2 delims== " %%A in ('findstr /C:"AssemblyVersion" "%PROJECT_PATH%\Properties\AssemblyInfo.cs"') do set "APP_VERSION=%%~A"
if defined APP_VERSION (
    echo  Version:    %APP_VERSION%
    call :LOG "App version: %APP_VERSION%"
)
echo.

REM ============================================================================
REM  STEP 4: LAUNCH APPLICATION
REM ============================================================================
echo [STEP 4/4] Launching Web Server...
call :LOG "Launching application..."
start "GGs Web Server" /B dotnet run --project "%PROJECT_FILE%" --no-build --urls %SERVER_URL%
if errorlevel 1 (
    echo [ERROR] Failed to launch application.
    call :LOG "ERROR: Failed to launch application."
    pause
    exit /b 1
)
echo [SUCCESS] Launch command executed.
call :LOG "Launch command executed."
echo.
echo Application is now running. You can close this window.
call :LOG "Server launcher finished."
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
echo [%LOG_DATE% %LOG_TIME%] %~1 >> "%LOG_FILE%"
goto :eof
