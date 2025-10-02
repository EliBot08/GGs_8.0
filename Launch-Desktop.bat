@echo off
setlocal EnableExtensions EnableDelayedExpansion

:: =============================================================
::  GGs // Desktop Launcher — stealth build + launch
:: =============================================================
title GGs :: Launch-Desktop (DeepOps)
color 0A

set "SCRIPT_DIR=%~dp0"
set "REPO_ROOT=%SCRIPT_DIR%"
if exist "%SCRIPT_DIR%..\" set "REPO_ROOT=%SCRIPT_DIR%..\"
set "LOG_DIR=%REPO_ROOT%launcher-logs"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%" >nul 2>&1
set "LOG_FILE=%LOG_DIR%\desktop-launcher.log"

set "WITH_SERVER=0"
set "TEST_MODE=0"
set "TEST_SECONDS=8"
set "SKIP_DOTNET_KILL=0"

:: args: [--with-server] [--test] [--duration N] [--skip-dotnet-kill]
:parse
if "%~1"=="" goto parsed
if /I "%~1"=="--with-server"     set "WITH_SERVER=1"
if /I "%~1"=="--test"            set "TEST_MODE=1"
if /I "%~1"=="--duration"        (set "TEST_SECONDS=%~2" & shift)
if /I "%~1"=="--skip-dotnet-kill" set "SKIP_DOTNET_KILL=1"
shift
goto parse

:parsed
echo ==============================================================================>>"%LOG_FILE%"
echo [DESKTOP] Starting at %DATE% %TIME%>>"%LOG_FILE%"

where dotnet >nul 2>&1 || (echo [ERROR] dotnet CLI not found & exit /b 1)

call :kill_conflicts
call :clean_target "clients\GGs.Desktop\GGs.Desktop.csproj" "out\desktop"

set "PS_ARGS=-File \"%REPO_ROOT%tools\launcher\Launch-Desktop-New.ps1\" -ForceBuild"
if %TEST_MODE%==1 set "PS_ARGS=%PS_ARGS% -Test -TestDurationSeconds %TEST_SECONDS%"

if %WITH_SERVER%==1 (
  echo [*] Spawning server in background ...>>"%LOG_FILE%"
  start "GGs Server" powershell -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%REPO_ROOT%tools\launcher\Launch-Server-New.ps1" -ForceBuild -ForcePort
)

powershell -NoLogo -NoProfile -ExecutionPolicy Bypass %PS_ARGS%
set "EXIT_CODE=%ERRORLEVEL%"
if not "%EXIT_CODE%"=="0" (
  echo [FAIL] Desktop launcher failed with exit code %EXIT_CODE%>>"%LOG_FILE%"
  exit /b %EXIT_CODE%
)

echo [OK] Desktop ready.>>"%LOG_FILE%"
exit /b 0

:kill_conflicts
echo [*] Neutralizing conflicting processes...>>"%LOG_FILE%"
for %%P in (GGs.Desktop.exe msbuild.exe vstest.console.exe) do (
  taskkill /F /IM %%P /T >nul 2>&1 && echo   - killed %%P>>"%LOG_FILE%"
)
if %SKIP_DOTNET_KILL%==0 (
  taskkill /F /IM dotnet.exe /T >nul 2>&1 && echo   - killed dotnet.exe>>"%LOG_FILE%"
)
exit /b 0

:clean_target
set "PROJ=%~1"
set "OUTDIR=%~2"
echo [*] Cleaning %PROJ% and %OUTDIR% ...>>"%LOG_FILE%"
if exist "%REPO_ROOT%%OUTDIR%" rmdir /s /q "%REPO_ROOT%%OUTDIR%" >nul 2>&1
dotnet clean "%REPO_ROOT%%PROJ%" -c Release --nologo >>"%LOG_FILE%" 2>&1
exit /b 0
