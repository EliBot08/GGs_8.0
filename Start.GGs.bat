@echo off
REM ============================================================================
REM  GGs Desktop - Advanced Standalone Launcher
REM  Enterprise Edition - Production Ready
REM  Version: 5.0.0
REM ============================================================================

setlocal EnableDelayedExpansion

REM Set console properties
title GGs Desktop - Enterprise Application Launcher
color 0E
mode con: cols=100 lines=30

REM Navigate to script directory
cd /d "%~dp0"

echo.
echo [93m╔════════════════════════════════════════════════════════════════════════════╗[0m
echo [93m║                    GGs Desktop Advanced Launcher                           ║[0m
echo [93m║                        Enterprise Edition v5.0                             ║[0m
echo [93m╚════════════════════════════════════════════════════════════════════════════╝[0m
echo.

REM Parse command line arguments
set "BUILD_MODE=Release"
set "SKIP_BUILD=false"
set "VERBOSE=false"
set "WITH_LOGVIEWER=false"
set "CUSTOM_LOG_DIR="

:parse_args
if "%~1"=="" goto end_parse
if /i "%~1"=="--debug" (
    set "BUILD_MODE=Debug"
    shift
    goto parse_args
)
if /i "%~1"=="--skip-build" (
    set "SKIP_BUILD=true"
    shift
    goto parse_args
)
if /i "%~1"=="--verbose" (
    set "VERBOSE=true"
    shift
    goto parse_args
)
if /i "%~1"=="--with-logviewer" (
    set "WITH_LOGVIEWER=true"
    shift
    goto parse_args
)
if /i "%~1"=="--log-dir" (
    set "WITH_LOGVIEWER=true"
    set "CUSTOM_LOG_DIR=%~2"
    shift
    shift
    goto parse_args
)
if /i "%~1"=="--help" (
    goto show_help
)
shift
goto parse_args

:end_parse

REM Display configuration
echo [96m┌─ Launch Configuration ─────────────────────────────────────────────────────┐[0m
echo [96m│[0m Build Mode:          [97m!BUILD_MODE![0m
echo [96m│[0m Skip Build:          [97m!SKIP_BUILD![0m
echo [96m│[0m With LogViewer:      [97m!WITH_LOGVIEWER![0m
if not "!CUSTOM_LOG_DIR!"=="" (
    echo [96m│[0m Log Directory:       [97m!CUSTOM_LOG_DIR![0m
)
echo [96m└────────────────────────────────────────────────────────────────────────────┘[0m
echo.

REM Check for existing instance
echo [36m► Checking for existing GGs Desktop instance...[0m
tasklist /FI "IMAGENAME eq GGs.Desktop.exe" 2>NUL | find /I /N "GGs.Desktop.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [93m⚠ WARNING: GGs Desktop is already running[0m
    echo.
    choice /C YN /M "Do you want to start another instance"
    if errorlevel 2 (
        echo [91m✗ Launch cancelled by user[0m
        goto end_script
    )
    echo [92m✓ Starting new instance...[0m
)

REM Check .NET SDK
echo [36m► Verifying .NET SDK installation...[0m
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [91m✗ ERROR: .NET SDK not found in PATH[0m
    echo [91m  Please install .NET 9.0 SDK from: https://dotnet.microsoft.com/download[0m
    goto error_exit
)

for /f "tokens=*" %%i in ('dotnet --version 2^>^&1') do set DOTNET_VERSION=%%i
echo [92m✓ .NET SDK Version: %DOTNET_VERSION%[0m

REM Check project file
set "PROJECT_PATH=clients\GGs.Desktop\GGs.Desktop.csproj"
echo [36m► Checking project file...[0m
if not exist "%PROJECT_PATH%" (
    echo [91m✗ ERROR: Project file not found: %PROJECT_PATH%[0m
    goto error_exit
)
echo [92m✓ Project file found[0m

REM Build application (if not skipped)
if "!SKIP_BUILD!"=="false" (
    echo.
    echo [96m╔════════════════════════════════════════════════════════════════════════════╗[0m
    echo [96m║                           Building Application                             ║[0m
    echo [96m╚════════════════════════════════════════════════════════════════════════════╝[0m
    echo.
    echo [36m► Building GGs Desktop [!BUILD_MODE!]...[0m
    
    dotnet build "%PROJECT_PATH%" -c !BUILD_MODE! --nologo
    
    if errorlevel 1 (
        echo.
        echo [91m✗ BUILD FAILED[0m
        echo [91m  Please check the build output above for errors[0m
        goto error_exit
    )
    
    echo [92m✓ Build completed successfully[0m
) else (
    echo [93m⚠ Skipping build (--skip-build flag)[0m
)

REM Verify executable
set "EXE_PATH=clients\GGs.Desktop\bin\!BUILD_MODE!\net9.0-windows\GGs.Desktop.exe"
echo [36m► Verifying executable...[0m
if not exist "%EXE_PATH%" (
    echo [91m✗ ERROR: Executable not found: %EXE_PATH%[0m
    echo [91m  Please ensure the build completed successfully[0m
    goto error_exit
)

REM Get file info
for %%A in ("%EXE_PATH%") do (
    set "EXE_SIZE=%%~zA"
    set "EXE_DATE=%%~tA"
)
set /a EXE_SIZE_MB=!EXE_SIZE! / 1048576
echo [92m✓ Executable found (!EXE_SIZE_MB! MB, Modified: !EXE_DATE!)[0m

REM Check dependencies
echo [36m► Checking critical dependencies...[0m
if not exist "clients\GGs.Desktop\bin\!BUILD_MODE!\net9.0-windows\*.dll" (
    echo [93m⚠ WARNING: Some dependencies may be missing[0m
) else (
    echo [92m✓ Dependencies present[0m
)

REM Launch ErrorLogViewer first if requested
if "!WITH_LOGVIEWER!"=="true" (
    echo.
    echo [96m► Launching ErrorLogViewer first...[0m
    
    set "LOGVIEWER_EXE=tools\GGs.ErrorLogViewer\bin\!BUILD_MODE!\net9.0-windows\GGs.ErrorLogViewer.exe"
    
    if exist "!LOGVIEWER_EXE!" (
        if not "!CUSTOM_LOG_DIR!"=="" (
            start "" "!LOGVIEWER_EXE!" --log-dir "!CUSTOM_LOG_DIR!"
        ) else (
            start "" "!LOGVIEWER_EXE!"
        )
        timeout /t 2 /nobreak >nul
        echo [92m✓ ErrorLogViewer launched[0m
    ) else (
        echo [93m⚠ ErrorLogViewer not found, continuing without it[0m
    )
)

REM Launch application
echo.
echo [96m╔════════════════════════════════════════════════════════════════════════════╗[0m
echo [96m║                         Launching GGs Desktop                              ║[0m
echo [96m╚════════════════════════════════════════════════════════════════════════════╝[0m
echo.

echo [36m► Starting GGs Desktop...[0m
if "!VERBOSE!"=="true" (
    echo [90m  Command: "%EXE_PATH%"[0m
)

REM Start the application
start "" "%EXE_PATH%"

REM Wait and verify startup
timeout /t 2 /nobreak >nul

tasklist /FI "IMAGENAME eq GGs.Desktop.exe" 2>NUL | find /I /N "GGs.Desktop.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo.
    echo [92m╔════════════════════════════════════════════════════════════════════════════╗[0m
    echo [92m║                      ✓ GGs Desktop Started Successfully                    ║[0m
    echo [92m╚════════════════════════════════════════════════════════════════════════════╝[0m
    echo.
    
    REM Get PID
    for /f "tokens=2" %%a in ('tasklist /FI "IMAGENAME eq GGs.Desktop.exe" /NH 2^>NUL ^| findstr /I "GGs.Desktop.exe"') do (
        echo [96m► Process ID: [97m%%a[0m
        goto :pid_found
    )
    :pid_found
    
    echo.
    echo [93m┌─ Quick Info ───────────────────────────────────────────────────────────────┐[0m
    echo [93m│[0m  • Application running in [97m!BUILD_MODE![0m mode
    if "!WITH_LOGVIEWER!"=="true" (
        echo [93m│[0m  • ErrorLogViewer also launched for monitoring
    )
    echo [93m│[0m  • Check application window for full interface
    echo [93m└────────────────────────────────────────────────────────────────────────────┘[0m
    echo.
    echo [92mYou can safely close this window.[0m
    echo.
    timeout /t 5 /nobreak >nul
    goto end_script
) else (
    echo.
    echo [91m╔════════════════════════════════════════════════════════════════════════════╗[0m
    echo [91m║                       ✗ Launch Failed                                      ║[0m
    echo [91m╚════════════════════════════════════════════════════════════════════════════╝[0m
    echo.
    echo [91mPossible causes:[0m
    echo [91m  1. Application crashed immediately on startup[0m
    echo [91m  2. Missing dependencies or configuration files[0m
    echo [91m  3. Insufficient permissions[0m
    echo.
    echo [93mTroubleshooting:[0m
    echo [93m  • Try running in Debug mode: %~nx0 --debug[0m
    echo [93m  • Rebuild: %~nx0 --skip-build=false[0m
    echo [93m  • Check event logs for crash details[0m
    echo.
    goto error_exit
)

:show_help
echo.
echo [96mGGs Desktop Advanced Launcher - Help[0m
echo.
echo [97mUSAGE:[0m
echo   %~nx0 [options]
echo.
echo [97mOPTIONS:[0m
echo   --debug              Launch in Debug mode (default: Release)
echo   --skip-build         Skip building the application
echo   --with-logviewer     Also launch ErrorLogViewer
echo   --log-dir DIR        Launch with ErrorLogViewer using custom log directory
echo   --verbose            Enable verbose output
echo   --help               Show this help message
echo.
echo [97mEXAMPLES:[0m
echo   [93m%~nx0[0m
echo     Launch GGs Desktop only
echo.
echo   [93m%~nx0 --with-logviewer[0m
echo     Launch Desktop with ErrorLogViewer
echo.
echo   [93m%~nx0 --log-dir "C:\Logs\MyApp"[0m
echo     Launch Desktop and ErrorLogViewer with custom log directory
echo.
echo   [93m%~nx0 --debug --verbose[0m
echo     Launch in debug mode with verbose output
echo.
goto end_script

:error_exit
echo.
echo [91m═══════════════════════════════════════════════════════════════════════════[0m
echo [91m LAUNCH FAILED - See errors above[0m
echo [91m═══════════════════════════════════════════════════════════════════════════[0m
echo.
pause
exit /b 1

:end_script
endlocal
exit /b 0
