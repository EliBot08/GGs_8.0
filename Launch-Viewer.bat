@echo off
setlocal enableextensions enabledelayedexpansion

set "SCRIPT_NAME=Launch-Viewer"
set "ROOT=%~dp0"
if "%ROOT:~-1%"=="\" set "ROOT=%ROOT:~0,-1%"
set "LOG_DIR=%ROOT%\launcher-logs"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"
for /f %%I in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmmss"') do set "TIMESTAMP=%%I"
if not defined TIMESTAMP (
    set "TIMESTAMP=%DATE:~6,4%%DATE:~3,2%%DATE:~0,2%_%TIME:~0,2%%TIME:~3,2%%TIME:~6,2%"
)
set "TIMESTAMP=%TIMESTAMP::=%"
set "TIMESTAMP=%TIMESTAMP: =0%"
set "LOG_FILE=%LOG_DIR%\%SCRIPT_NAME%_%TIMESTAMP%.log"
set NO_BUILD=0
set VERIFY_ONLY=0

for %%A in (%*) do (
    if /I "%%~A"=="--nobuild" set NO_BUILD=1
    if /I "%%~A"=="--verify" set VERIFY_ONLY=1
)

call :log INFO "Launching %SCRIPT_NAME%"
call :ensure_dotnet || goto :fail
if "%NO_BUILD%"=="0" (
    call :build_viewer || goto :fail
) else (
    call :log INFO "Skipping build (--nobuild)"
)
call :prepare_viewer || goto :fail
if "%VERIFY_ONLY%"=="1" (
    call :log SUCCESS "Verification completed successfully."
    goto :success
)
call :launch_viewer || goto :fail
call :log SUCCESS "Error Log Viewer started. Press any key to close it."
pause >nul
call :shutdown
:success
call :log INFO "%SCRIPT_NAME% completed"
exit /b 0

:ensure_dotnet
for /f %%V in ('dotnet --version 2^>nul') do set DOTNET_VERSION=%%V
if not defined DOTNET_VERSION (
    call :log ERROR "dotnet SDK not detected."
    exit /b 1
)
call :log INFO "dotnet SDK %DOTNET_VERSION% detected"
exit /b 0

:build_viewer
call :log INFO "Building ErrorLogViewer project..."
pushd "%ROOT%" >nul 2>&1
if errorlevel 1 (
    call :log ERROR "Unable to access root directory %ROOT%."
    exit /b 1
)
dotnet build tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj --configuration Debug --nologo --verbosity minimal >> "%LOG_FILE%" 2>&1
set "BUILD_RC=%ERRORLEVEL%"
popd >nul
if not "%BUILD_RC%"=="0" (
    call :log ERROR "ErrorLogViewer build failed (exit code %BUILD_RC%). Check %LOG_FILE%"
    exit /b 1
)
call :log SUCCESS "Build succeeded."
exit /b 0

:prepare_viewer
set "VIEWER_EXE=%ROOT%\tools\GGs.ErrorLogViewer\bin\Debug\net9.0-windows\GGs.ErrorLogViewer.exe"
if not exist "%VIEWER_EXE%" (
    call :log ERROR "Executable not found at %VIEWER_EXE%"
    exit /b 1
)
exit /b 0

:launch_viewer
call :log INFO "Starting Error Log Viewer..."
start "GGs ErrorLogViewer" "%VIEWER_EXE%"
if errorlevel 1 (
    call :log ERROR "Failed to start Error Log Viewer"
    exit /b 1
)
exit /b 0

:shutdown
call :log INFO "Closing Error Log Viewer..."
taskkill /FI "WINDOWTITLE eq GGs ErrorLogViewer" /F >nul 2>&1
taskkill /IM GGs.ErrorLogViewer.exe /F >nul 2>&1
call :log SUCCESS "Error Log Viewer closed"
exit /b 0

:log
set "LEVEL=%~1"
set "MESSAGE=%~2"
if "%LEVEL%"=="" set "LEVEL=INFO"
if "%MESSAGE%"=="" set "MESSAGE="
echo [%LEVEL%] %MESSAGE%
echo [%DATE% %TIME%] [%LEVEL%] %MESSAGE%>>"%LOG_FILE%"
exit /b 0

:fail
call :log ERROR "%SCRIPT_NAME% failed"
exit /b 1
