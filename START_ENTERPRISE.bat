@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0LAUNCH_ENTERPRISE.ps1"
exit /b %errorlevel%

