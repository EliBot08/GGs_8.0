@echo off
setlocal EnableExtensions

set "PROC=GGs.Server.exe"
set "TITLE=GGs - Kill Server"
title %TITLE%

echo.
echo [GGs] Kill script starting...

rem Check for administrator rights via PowerShell
for /f "usebackq tokens=*" %%A in (`powershell -NoProfile -ExecutionPolicy Bypass -Command "(New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)"`) do set "ISADMIN=%%A"
if /I not "%ISADMIN%"=="True" (
  echo [GGs] Administrator rights required. Prompting for elevation...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  echo [GGs] If nothing happens, right-click this file and choose 'Run as administrator'.
  goto :pause_and_exit
)

echo [GGs] Looking for %PROC%...
tasklist /FI "IMAGENAME eq %PROC%" | find /I "%PROC%" >NUL
if errorlevel 1 (
  echo [GGs] %PROC% is not running.
  goto :pause_and_exit
)

echo [GGs] Stopping %PROC% (and any child processes)...
taskkill /F /IM %PROC% /T
if errorlevel 1 (
  echo [GGs] taskkill reported an error. Trying PowerShell fallback...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Get-Process -Name 'GGs.Server' -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue"
)

rem Verify it is gone
timeout /t 1 >NUL
tasklist /FI "IMAGENAME eq %PROC%" | find /I "%PROC%" >NUL
if errorlevel 1 (
  echo [GGs] %PROC% stopped successfully.
) else (
  echo [GGs] Failed to stop %PROC%. Ensure you have permissions and try again.
)

:pause_and_exit
echo.
echo Press any key to close this window...
pause >NUL
endlocal
exit /B 0

