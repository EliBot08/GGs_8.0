@echo off
echo ================================================
echo GGs Advanced Smart App Control Bypass Tool v2.1
echo Unicode Path Fix
echo ================================================
echo This script tries multiple advanced methods
echo to bypass Windows Smart App Control restrictions.
echo.
echo FIXED: Unicode character handling in paths
echo.
pause

powershell -ExecutionPolicy Bypass -File "%~dp0Advanced_Bypass_Fixed.ps1"

echo.
echo ================================================
echo Script completed.
echo ================================================
echo.
echo If your GGs application started, you're all set!
echo If not, contact your IT administrator.
echo.
pause
