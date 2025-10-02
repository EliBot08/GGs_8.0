@echo off
setlocal EnableExtensions EnableDelayedExpansion

:: =============================================================
::  GGs // DeepOps Orchestrator (ALL) — hacker mode engaged
::  Clean builds. Kill conflicts. Orchestrate like a boss.
:: =============================================================
title GGs :: Launch-All (DeepOps)
color 0A

set "SCRIPT_DIR=%~dp0"
set "REPO_ROOT=%SCRIPT_DIR%"
if exist "%SCRIPT_DIR%..\" set "REPO_ROOT=%SCRIPT_DIR%..\"
set "LOG_DIR=%REPO_ROOT%launcher-logs"
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%" >nul 2>&1
set "LOG_FILE=%LOG_DIR%\all-launch.log"

set "TEST_MODE=0"
set "TEST_SECONDS=8"
set "FORCE_PORT=1"
set "SKIP_DOTNET_KILL=0"

:: args: --test [--duration N] [--no-forceport] [--skip-dotnet-kill]
:parse
if "%~1"=="" goto parsed
if /I "%~1"=="--test"            set "TEST_MODE=1"
if /I "%~1"=="--duration"        (set "TEST_SECONDS=%~2" & shift)
if /I "%~1"=="--no-forceport"    set "FORCE_PORT=0"
if /I "%~1"=="--skip-dotnet-kill" set "SKIP_DOTNET_KILL=1"
shift
goto parse

:parsed
echo ==============================================================================>>"%LOG_FILE%"
echo [ALL] Booting DeepOps Orchestrator at %DATE% %TIME%>>"%LOG_FILE%"

where dotnet >nul 2>&1 || (echo [ERROR] dotnet CLI not found & exit /b 1)

call :kill_conflicts
call :clean_workspace

set "PS_ARGS=-File \"%REPO_ROOT%tools\launcher\Launch-All-New.ps1\" -ForceBuild"
if %FORCE_PORT%==1 set "PS_ARGS=%PS_ARGS% -ForcePort"
if %TEST_MODE%==1 set "PS_ARGS=%PS_ARGS% -Test -TestDurationSeconds %TEST_SECONDS%"

echo [RUN] pwsh %PS_ARGS%>>"%LOG_FILE%"
powershell -NoLogo -NoProfile -ExecutionPolicy Bypass %PS_ARGS%
set "EXIT_CODE=%ERRORLEVEL%"

if not "%EXIT_CODE%"=="0" (
  echo [FAIL] Orchestrator exit code %EXIT_CODE% — see PowerShell logs.>>"%LOG_FILE%"
  exit /b %EXIT_CODE%
)

echo [OK] Launcher suite completed.>>"%LOG_FILE%"
exit /b 0

:kill_conflicts
echo [*] Neutralizing conflicting processes...>>"%LOG_FILE%"
for %%P in (GGs.Desktop.exe GGs.ErrorLogViewer.exe GGs.Server.exe msbuild.exe vstest.console.exe) do (
  taskkill /F /IM %%P /T >nul 2>&1 && echo   - killed %%P>>"%LOG_FILE%"
)
if %SKIP_DOTNET_KILL%==0 (
  taskkill /F /IM dotnet.exe /T >nul 2>&1 && echo   - killed dotnet.exe>>"%LOG_FILE%"
)
exit /b 0

:clean_workspace
echo [*] Cleaning solution and out/* ...>>"%LOG_FILE%"
if exist "%REPO_ROOT%out\server"  rmdir /s /q "%REPO_ROOT%out\server"  >nul 2>&1
if exist "%REPO_ROOT%out\desktop" rmdir /s /q "%REPO_ROOT%out\desktop" >nul 2>&1
if exist "%REPO_ROOT%out\viewer"  rmdir /s /q "%REPO_ROOT%out\viewer"  >nul 2>&1
dotnet clean "%REPO_ROOT%GGs.sln" -c Release --nologo >>"%LOG_FILE%" 2>&1
exit /b 0

