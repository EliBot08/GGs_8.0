@echo off
REM ============================================================================
REM  GGs Enterprise Suite - Unified Launcher
REM  Launches both GGs Desktop and ErrorLogViewer
REM  Enterprise Edition - Production Ready
REM  Version: 5.0.0
REM ============================================================================

setlocal EnableDelayedExpansion

REM Set console properties
title GGs Enterprise Suite - Unified Launcher
color 0A
mode con: cols=110 lines=35

REM Navigate to script directory
cd /d "%~dp0"

echo.
echo [92m╔══════════════════════════════════════════════════════════════════════════════════╗[0m
echo [92m║                    GGs Enterprise Suite - Unified Launcher                       ║[0m
echo [92m║                           Launch All Components v5.0                             ║[0m
echo [92m╚══════════════════════════════════════════════════════════════════════════════════╝[0m
echo.

REM Parse command line arguments
set "CUSTOM_LOG_DIR="
set "BUILD_MODE=Release"
set "SKIP_BUILD=false"
set "SKIP_LOGVIEWER=false"
set "SKIP_DESKTOP=false"
set "VERBOSE=false"
set "MONITOR=false"

:parse_args
if "%~1"=="" goto end_parse
if /i "%~1"=="--log-dir" (
    set "CUSTOM_LOG_DIR=%~2"
    shift
    shift
    goto parse_args
)
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
if /i "%~1"=="--skip-logviewer" (
    set "SKIP_LOGVIEWER=true"
    shift
    goto parse_args
)
if /i "%~1"=="--skip-desktop" (
    set "SKIP_DESKTOP=true"
    shift
    goto parse_args
)
if /i "%~1"=="--verbose" (
    set "VERBOSE=true"
    shift
    goto parse_args
)
if /i "%~1"=="--monitor" (
    set "MONITOR=true"
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
echo [96m┌─ Launch Configuration ───────────────────────────────────────────────────────────┐[0m
echo [96m│[0m Build Mode:          [97m!BUILD_MODE![0m
echo [96m│[0m Skip Build:          [97m!SKIP_BUILD![0m
echo [96m│[0m Launch Desktop:      [97m!SKIP_DESKTOP:false=Yes!SKIP_DESKTOP:true=No![0m
echo [96m│[0m Launch LogViewer:    [97m!SKIP_LOGVIEWER:false=Yes!SKIP_LOGVIEWER:true=No![0m
if not "!CUSTOM_LOG_DIR!"=="" (
    echo [96m│[0m Custom Log Dir:      [97m!CUSTOM_LOG_DIR![0m
)
echo [96m│[0m Process Monitoring:  [97m!MONITOR![0m
echo [96m└──────────────────────────────────────────────────────────────────────────────────┘[0m
echo.

REM Validate at least one component selected
if "!SKIP_DESKTOP!"=="true" if "!SKIP_LOGVIEWER!"=="true" (
    echo [91m✗ ERROR: Cannot skip both Desktop and LogViewer[0m
    echo [93m  At least one component must be launched[0m
    goto error_exit
)

REM Check .NET SDK
echo [36m► Verifying .NET SDK installation...[0m
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [91m✗ ERROR: .NET SDK not found[0m
    goto error_exit
)

for /f "tokens=*" %%i in ('dotnet --version 2^>^&1') do set DOTNET_VERSION=%%i
echo [92m✓ .NET SDK Version: %DOTNET_VERSION%[0m

REM Check PowerShell launcher
echo [36m► Checking PowerShell launcher...[0m
if not exist "GGsLauncher.ps1" (
    echo [93m⚠ PowerShell launcher not found, using batch fallback[0m
    set "USE_FALLBACK=true"
) else (
    echo [92m✓ PowerShell launcher available[0m
    set "USE_FALLBACK=false"
)

REM Build applications if needed
if "!SKIP_BUILD!"=="false" (
    echo.
    echo [96m╔══════════════════════════════════════════════════════════════════════════════════╗[0m
    echo [96m║                              Building Applications                                ║[0m
    echo [96m╚══════════════════════════════════════════════════════════════════════════════════╝[0m
    echo.
    
    if "!SKIP_DESKTOP!"=="false" (
        echo [36m► Building GGs.Desktop [!BUILD_MODE!]...[0m
        dotnet build "clients\GGs.Desktop\GGs.Desktop.csproj" -c !BUILD_MODE! --nologo
        if errorlevel 1 (
            echo [91m✗ Desktop build failed[0m
            goto error_exit
        )
        echo [92m✓ Desktop build completed[0m
    )
    
    if "!SKIP_LOGVIEWER!"=="false" (
        echo [36m► Building ErrorLogViewer [!BUILD_MODE!]...[0m
        dotnet build "tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj" -c !BUILD_MODE! --nologo
        if errorlevel 1 (
            echo [91m✗ ErrorLogViewer build failed[0m
            goto error_exit
        )
        echo [92m✓ ErrorLogViewer build completed[0m
    )
) else (
    echo [93m⚠ Skipping build phase[0m
)

REM Use PowerShell launcher if available
if "!USE_FALLBACK!"=="false" (
    echo.
    echo [96m╔══════════════════════════════════════════════════════════════════════════════════╗[0m
    echo [96m║                         Using PowerShell Enterprise Launcher                     ║[0m
    echo [96m╚══════════════════════════════════════════════════════════════════════════════════╝[0m
    echo.
    
    set "PS_ARGS=-Configuration !BUILD_MODE! -SkipBuild"
    if "!SKIP_DESKTOP!"=="true" set "PS_ARGS=!PS_ARGS! -LogViewer"
    if "!SKIP_LOGVIEWER!"=="true" set "PS_ARGS=!PS_ARGS! -Desktop"
    if not "!CUSTOM_LOG_DIR!"=="" set "PS_ARGS=!PS_ARGS! -LogDirectory "!CUSTOM_LOG_DIR!""
    if "!VERBOSE!"=="true" set "PS_ARGS=!PS_ARGS! -VerboseLogging"
    
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "GGsLauncher.ps1" !PS_ARGS!
    
    if errorlevel 1 (
        echo [91m✗ PowerShell launcher failed[0m
        goto error_exit
    )
    
    goto end_script
)

REM Fallback: Direct batch launch
echo.
echo [96m╔══════════════════════════════════════════════════════════════════════════════════╗[0m
echo [96m║                         Launching Applications (Batch Mode)                       ║[0m
echo [96m╚══════════════════════════════════════════════════════════════════════════════════╝[0m
echo.

set "LAUNCHED_COUNT=0"

REM Launch ErrorLogViewer first (if enabled)
if "!SKIP_LOGVIEWER!"=="false" (
    set "LOGVIEWER_EXE=tools\GGs.ErrorLogViewer\bin\!BUILD_MODE!\net9.0-windows\GGs.ErrorLogViewer.exe"
    
    echo [36m► Launching ErrorLogViewer...[0m
    if not exist "!LOGVIEWER_EXE!" (
        echo [91m✗ ErrorLogViewer executable not found[0m
        goto error_exit
    )
    
    if not "!CUSTOM_LOG_DIR!"=="" (
        start "" "!LOGVIEWER_EXE!" --log-dir "!CUSTOM_LOG_DIR!"
    ) else (
        start "" "!LOGVIEWER_EXE!"
    )
    
    timeout /t 1 /nobreak >nul
    
    tasklist /FI "IMAGENAME eq GGs.ErrorLogViewer.exe" 2>NUL | find /I "GGs.ErrorLogViewer.exe" >NUL
    if errorlevel 1 (
        echo [93m⚠ WARNING: ErrorLogViewer may not have started successfully[0m
    ) else (
        echo [92m✓ ErrorLogViewer launched (PID: [0m
        for /f "tokens=2" %%a in ('tasklist /FI "IMAGENAME eq GGs.ErrorLogViewer.exe" /NH 2^>NUL ^| findstr /I "GGs.ErrorLogViewer.exe"') do (
            echo [92m%%a)[0m
            goto :logviewer_pid_done
        )
        :logviewer_pid_done
        set /a LAUNCHED_COUNT+=1
    )
)

REM Launch Desktop (if enabled)
if "!SKIP_DESKTOP!"=="false" (
    set "DESKTOP_EXE=clients\GGs.Desktop\bin\!BUILD_MODE!\net9.0-windows\GGs.Desktop.exe"
    
    echo [36m► Launching GGs.Desktop...[0m
    if not exist "!DESKTOP_EXE!" (
        echo [91m✗ Desktop executable not found[0m
        goto error_exit
    )
    
    start "" "!DESKTOP_EXE!"
    
    timeout /t 1 /nobreak >nul
    
    tasklist /FI "IMAGENAME eq GGs.Desktop.exe" 2>NUL | find /I "GGs.Desktop.exe" >NUL
    if errorlevel 1 (
        echo [93m⚠ WARNING: Desktop may not have started successfully[0m
    ) else (
        echo [92m✓ Desktop launched (PID: [0m
        for /f "tokens=2" %%a in ('tasklist /FI "IMAGENAME eq GGs.Desktop.exe" /NH 2^>NUL ^| findstr /I "GGs.Desktop.exe"') do (
            echo [92m%%a)[0m
            goto :desktop_pid_done
        )
        :desktop_pid_done
        set /a LAUNCHED_COUNT+=1
    )
)

echo.
if !LAUNCHED_COUNT! GTR 0 (
    echo [92m╔══════════════════════════════════════════════════════════════════════════════════╗[0m
    echo [92m║                          ✓ Launch Successful (!LAUNCHED_COUNT! component(s^))                          ║[0m
    echo [92m╚══════════════════════════════════════════════════════════════════════════════════╝[0m
) else (
    echo [91m╔══════════════════════════════════════════════════════════════════════════════════╗[0m
    echo [91m║                              ✗ No Components Launched                            ║[0m
    echo [91m╚══════════════════════════════════════════════════════════════════════════════════╝[0m
    goto error_exit
)

REM Process monitoring (if requested)
if "!MONITOR!"=="true" (
    echo.
    echo [96m► Monitoring processes (Press Ctrl+C to exit)...[0m
    echo.
    
    :monitor_loop
    timeout /t 3 /nobreak >nul
    
    set "RUNNING=0"
    
    if "!SKIP_DESKTOP!"=="false" (
        tasklist /FI "IMAGENAME eq GGs.Desktop.exe" 2>NUL | find /I "GGs.Desktop.exe" >NUL
        if not errorlevel 1 set /a RUNNING+=1
    )
    
    if "!SKIP_LOGVIEWER!"=="false" (
        tasklist /FI "IMAGENAME eq GGs.ErrorLogViewer.exe" 2>NUL | find /I "GGs.ErrorLogViewer.exe" >NUL
        if not errorlevel 1 set /a RUNNING+=1
    )
    
    if !RUNNING! EQU 0 (
        echo [93m► All monitored processes have exited[0m
        goto end_script
    )
    
    goto monitor_loop
)

echo.
echo [92mYou can safely close this window.[0m
echo.
timeout /t 5 /nobreak >nul
goto end_script

:show_help
echo.
echo [96mGGs Enterprise Suite - Unified Launcher - Help[0m
echo.
echo [97mUSAGE:[0m
echo   %~nx0 [options]
echo.
echo [97mOPTIONS:[0m
echo   --log-dir DIR       Specify custom log directory for ErrorLogViewer
echo   --debug             Launch in Debug mode (default: Release)
echo   --skip-build        Skip building applications
echo   --skip-desktop      Don't launch GGs Desktop
echo   --skip-logviewer    Don't launch ErrorLogViewer
echo   --monitor           Monitor processes until they exit
echo   --verbose           Enable verbose output
echo   --help              Show this help message
echo.
echo [97mEXAMPLES:[0m
echo   [93m%~nx0[0m
echo     Launch both Desktop and ErrorLogViewer
echo.
echo   [93m%~nx0 --skip-logviewer[0m
echo     Launch only GGs Desktop
echo.
echo   [93m%~nx0 --log-dir "C:\Logs" --monitor[0m
echo     Launch both with custom log directory and process monitoring
echo.
goto end_script

:error_exit
echo.
echo [91m═══════════════════════════════════════════════════════════════════════════════════[0m
echo [91m LAUNCH FAILED - See errors above[0m
echo [91m═══════════════════════════════════════════════════════════════════════════════════[0m
echo.
pause
exit /b 1

:end_script
endlocal
exit /b 0
