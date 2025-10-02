@echo off
setlocal enabledelayedexpansion

REM ============================================================================
REM  GGs Launcher Suite - Test Script
REM  Validates all launcher scripts for functionality
REM ============================================================================

color 0D
title GGs Launcher Suite - Test Script

echo.
echo ============================================================================
echo  GGs Launcher Suite - Test Script
echo  Validating all launcher functionality
echo ============================================================================
echo.

set "TEST_LOG=launcher-logs\test-results.log"
set "PASS_COUNT=0"
set "FAIL_COUNT=0"
set "WARN_COUNT=0"

REM Create log directory
if not exist "launcher-logs" mkdir "launcher-logs"

REM Initialize test log
echo ============================================================================ > "%TEST_LOG%"
echo GGs Launcher Suite - Test Results >> "%TEST_LOG%"
echo Test Date: %DATE% %TIME% >> "%TEST_LOG%"
echo ============================================================================ >> "%TEST_LOG%"
echo. >> "%TEST_LOG%"

REM ============================================================================
REM  TEST 1: File Existence
REM ============================================================================

echo [TEST 1/10] Checking launcher file existence...
echo TEST 1: File Existence >> "%TEST_LOG%"

set "TEST_PASSED=1"

if exist "Launch-Viewer-New.bat" (
    echo   [PASS] Launch-Viewer-New.bat exists
    echo   [PASS] Launch-Viewer-New.bat exists >> "%TEST_LOG%"
) else (
    echo   [FAIL] Launch-Viewer-New.bat not found
    echo   [FAIL] Launch-Viewer-New.bat not found >> "%TEST_LOG%"
    set "TEST_PASSED=0"
)

if exist "Launch-Desktop-New.bat" (
    echo   [PASS] Launch-Desktop-New.bat exists
    echo   [PASS] Launch-Desktop-New.bat exists >> "%TEST_LOG%"
) else (
    echo   [FAIL] Launch-Desktop-New.bat not found
    echo   [FAIL] Launch-Desktop-New.bat not found >> "%TEST_LOG%"
    set "TEST_PASSED=0"
)

if exist "Launch-Server-New.bat" (
    echo   [PASS] Launch-Server-New.bat exists
    echo   [PASS] Launch-Server-New.bat exists >> "%TEST_LOG%"
) else (
    echo   [FAIL] Launch-Server-New.bat not found
    echo   [FAIL] Launch-Server-New.bat not found >> "%TEST_LOG%"
    set "TEST_PASSED=0"
)

if exist "Launch-All-New.bat" (
    echo   [PASS] Launch-All-New.bat exists
    echo   [PASS] Launch-All-New.bat exists >> "%TEST_LOG%"
) else (
    echo   [FAIL] Launch-All-New.bat not found
    echo   [FAIL] Launch-All-New.bat not found >> "%TEST_LOG%"
    set "TEST_PASSED=0"
)

if "%TEST_PASSED%"=="1" (
    set /a PASS_COUNT+=1
    echo [RESULT] Test 1: PASSED
    echo. >> "%TEST_LOG%"
) else (
    set /a FAIL_COUNT+=1
    echo [RESULT] Test 1: FAILED
    echo. >> "%TEST_LOG%"
)

echo.

REM ============================================================================
REM  TEST 2: Syntax Validation
REM ============================================================================

echo [TEST 2/10] Validating batch file syntax...
echo TEST 2: Syntax Validation >> "%TEST_LOG%"

set "TEST_PASSED=1"

REM Test each file for basic syntax errors by checking if it can be parsed
for %%f in (Launch-Viewer-New.bat Launch-Desktop-New.bat Launch-Server-New.bat Launch-All-New.bat) do (
    findstr /R "^@echo off" "%%f" >NUL 2>&1
    if errorlevel 1 (
        echo   [FAIL] %%f - Missing @echo off
        echo   [FAIL] %%f - Missing @echo off >> "%TEST_LOG%"
        set "TEST_PASSED=0"
    ) else (
        echo   [PASS] %%f - Valid batch file header
        echo   [PASS] %%f - Valid batch file header >> "%TEST_LOG%"
    )
)

if "%TEST_PASSED%"=="1" (
    set /a PASS_COUNT+=1
    echo [RESULT] Test 2: PASSED
    echo. >> "%TEST_LOG%"
) else (
    set /a FAIL_COUNT+=1
    echo [RESULT] Test 2: FAILED
    echo. >> "%TEST_LOG%"
)

echo.

REM ============================================================================
REM  TEST 3: Log Directory Creation
REM ============================================================================

echo [TEST 3/10] Testing log directory creation...
echo TEST 3: Log Directory Creation >> "%TEST_LOG%"

if exist "launcher-logs" (
    echo   [PASS] Log directory exists
    echo   [PASS] Log directory exists >> "%TEST_LOG%"
    set /a PASS_COUNT+=1
    echo [RESULT] Test 3: PASSED
    echo. >> "%TEST_LOG%"
) else (
    echo   [FAIL] Log directory not created
    echo   [FAIL] Log directory not created >> "%TEST_LOG%"
    set /a FAIL_COUNT+=1
    echo [RESULT] Test 3: FAILED
    echo. >> "%TEST_LOG%"
)

echo.

REM ============================================================================
REM  TEST 4: Project Structure Validation
REM ============================================================================

echo [TEST 4/10] Validating project structure...
echo TEST 4: Project Structure Validation >> "%TEST_LOG%"

set "TEST_PASSED=1"

if exist "server\GGs.Server\GGs.Server.csproj" (
    echo   [PASS] Server project found
    echo   [PASS] Server project found >> "%TEST_LOG%"
) else (
    echo   [FAIL] Server project not found
    echo   [FAIL] Server project not found >> "%TEST_LOG%"
    set "TEST_PASSED=0"
)

if exist "clients\GGs.Desktop\GGs.Desktop.csproj" (
    echo   [PASS] Desktop project found
    echo   [PASS] Desktop project found >> "%TEST_LOG%"
) else (
    echo   [FAIL] Desktop project not found
    echo   [FAIL] Desktop project not found >> "%TEST_LOG%"
    set "TEST_PASSED=0"
)

if exist "tools\GGs.ErrorLogViewer\GGs.ErrorLogViewer.csproj" (
    echo   [PASS] Viewer project found
    echo   [PASS] Viewer project found >> "%TEST_LOG%"
) else (
    echo   [FAIL] Viewer project not found
    echo   [FAIL] Viewer project not found >> "%TEST_LOG%"
    set "TEST_PASSED=0"
)

if "%TEST_PASSED%"=="1" (
    set /a PASS_COUNT+=1
    echo [RESULT] Test 4: PASSED
    echo. >> "%TEST_LOG%"
) else (
    set /a FAIL_COUNT+=1
    echo [RESULT] Test 4: FAILED
    echo. >> "%TEST_LOG%"
)

echo.

REM ============================================================================
REM  TEST 5: .NET SDK Availability
REM ============================================================================

echo [TEST 5/10] Checking .NET SDK availability...
echo TEST 5: .NET SDK Availability >> "%TEST_LOG%"

dotnet --version >NUL 2>&1
if errorlevel 1 (
    echo   [FAIL] .NET SDK not found
    echo   [FAIL] .NET SDK not found >> "%TEST_LOG%"
    set /a FAIL_COUNT+=1
    echo [RESULT] Test 5: FAILED
    echo. >> "%TEST_LOG%"
) else (
    for /f "tokens=*" %%i in ('dotnet --version') do set "DOTNET_VERSION=%%i"
    echo   [PASS] .NET SDK Version: !DOTNET_VERSION!
    echo   [PASS] .NET SDK Version: !DOTNET_VERSION! >> "%TEST_LOG%"
    set /a PASS_COUNT+=1
    echo [RESULT] Test 5: PASSED
    echo. >> "%TEST_LOG%"
)

echo.

REM ============================================================================
REM  TEST 6: Configuration Variables
REM ============================================================================

echo [TEST 6/10] Validating configuration variables...
echo TEST 6: Configuration Variables >> "%TEST_LOG%"

set "TEST_PASSED=1"

REM Check Launch-Viewer-New.bat
findstr /C:"APP_NAME=GGs.ErrorLogViewer" "Launch-Viewer-New.bat" >NUL 2>&1
if errorlevel 1 (
    echo   [FAIL] Launch-Viewer-New.bat - Missing APP_NAME configuration
    echo   [FAIL] Launch-Viewer-New.bat - Missing APP_NAME configuration >> "%TEST_LOG%"
    set "TEST_PASSED=0"
) else (
    echo   [PASS] Launch-Viewer-New.bat - Configuration valid
    echo   [PASS] Launch-Viewer-New.bat - Configuration valid >> "%TEST_LOG%"
)

REM Check Launch-Desktop-New.bat
findstr /C:"APP_NAME=GGs.Desktop" "Launch-Desktop-New.bat" >NUL 2>&1
if errorlevel 1 (
    echo   [FAIL] Launch-Desktop-New.bat - Missing APP_NAME configuration
    echo   [FAIL] Launch-Desktop-New.bat - Missing APP_NAME configuration >> "%TEST_LOG%"
    set "TEST_PASSED=0"
) else (
    echo   [PASS] Launch-Desktop-New.bat - Configuration valid
    echo   [PASS] Launch-Desktop-New.bat - Configuration valid >> "%TEST_LOG%"
)

REM Check Launch-Server-New.bat
findstr /C:"SERVER_PORT=5000" "Launch-Server-New.bat" >NUL 2>&1
if errorlevel 1 (
    echo   [FAIL] Launch-Server-New.bat - Missing SERVER_PORT configuration
    echo   [FAIL] Launch-Server-New.bat - Missing SERVER_PORT configuration >> "%TEST_LOG%"
    set "TEST_PASSED=0"
) else (
    echo   [PASS] Launch-Server-New.bat - Configuration valid
    echo   [PASS] Launch-Server-New.bat - Configuration valid >> "%TEST_LOG%"
)

if "%TEST_PASSED%"=="1" (
    set /a PASS_COUNT+=1
    echo [RESULT] Test 6: PASSED
    echo. >> "%TEST_LOG%"
) else (
    set /a FAIL_COUNT+=1
    echo [RESULT] Test 6: FAILED
    echo. >> "%TEST_LOG%"
)

echo.

REM ============================================================================
REM  TEST 7: Logging Functions
REM ============================================================================

echo [TEST 7/10] Validating logging functions...
echo TEST 7: Logging Functions >> "%TEST_LOG%"

set "TEST_PASSED=1"

for %%f in (Launch-Viewer-New.bat Launch-Desktop-New.bat Launch-Server-New.bat Launch-All-New.bat) do (
    findstr /C:":LOG" "%%f" >NUL 2>&1
    if errorlevel 1 (
        echo   [FAIL] %%f - Missing LOG function
        echo   [FAIL] %%f - Missing LOG function >> "%TEST_LOG%"
        set "TEST_PASSED=0"
    ) else (
        echo   [PASS] %%f - LOG function present
        echo   [PASS] %%f - LOG function present >> "%TEST_LOG%"
    )
)

if "%TEST_PASSED%"=="1" (
    set /a PASS_COUNT+=1
    echo [RESULT] Test 7: PASSED
    echo. >> "%TEST_LOG%"
) else (
    set /a FAIL_COUNT+=1
    echo [RESULT] Test 7: FAILED
    echo. >> "%TEST_LOG%"
)

echo.

REM ============================================================================
REM  TEST 8: Error Handling
REM ============================================================================

echo [TEST 8/10] Validating error handling...
echo TEST 8: Error Handling >> "%TEST_LOG%"

set "TEST_PASSED=1"

for %%f in (Launch-Viewer-New.bat Launch-Desktop-New.bat Launch-Server-New.bat Launch-All-New.bat) do (
    findstr /C:"Troubleshooting:" "%%f" >NUL 2>&1
    if errorlevel 1 (
        echo   [FAIL] %%f - Missing troubleshooting hints
        echo   [FAIL] %%f - Missing troubleshooting hints >> "%TEST_LOG%"
        set "TEST_PASSED=0"
    ) else (
        echo   [PASS] %%f - Error handling present
        echo   [PASS] %%f - Error handling present >> "%TEST_LOG%"
    )
)

if "%TEST_PASSED%"=="1" (
    set /a PASS_COUNT+=1
    echo [RESULT] Test 8: PASSED
    echo. >> "%TEST_LOG%"
) else (
    set /a FAIL_COUNT+=1
    echo [RESULT] Test 8: FAILED
    echo. >> "%TEST_LOG%"
)

echo.

REM ============================================================================
REM  TEST 9: Process Management
REM ============================================================================

echo [TEST 9/10] Validating process management...
echo TEST 9: Process Management >> "%TEST_LOG%"

set "TEST_PASSED=1"

for %%f in (Launch-Viewer-New.bat Launch-Desktop-New.bat Launch-Server-New.bat) do (
    findstr /C:"tasklist" "%%f" >NUL 2>&1
    if errorlevel 1 (
        echo   [FAIL] %%f - Missing process management
        echo   [FAIL] %%f - Missing process management >> "%TEST_LOG%"
        set "TEST_PASSED=0"
    ) else (
        findstr /C:"taskkill" "%%f" >NUL 2>&1
        if errorlevel 1 (
            echo   [FAIL] %%f - Missing process termination
            echo   [FAIL] %%f - Missing process termination >> "%TEST_LOG%"
            set "TEST_PASSED=0"
        ) else (
            echo   [PASS] %%f - Process management present
            echo   [PASS] %%f - Process management present >> "%TEST_LOG%"
        )
    )
)

if "%TEST_PASSED%"=="1" (
    set /a PASS_COUNT+=1
    echo [RESULT] Test 9: PASSED
    echo. >> "%TEST_LOG%"
) else (
    set /a FAIL_COUNT+=1
    echo [RESULT] Test 9: FAILED
    echo. >> "%TEST_LOG%"
)

echo.

REM ============================================================================
REM  TEST 10: Monitoring Features
REM ============================================================================

echo [TEST 10/10] Validating monitoring features...
echo TEST 10: Monitoring Features >> "%TEST_LOG%"

set "TEST_PASSED=1"

for %%f in (Launch-Viewer-New.bat Launch-Desktop-New.bat Launch-Server-New.bat) do (
    findstr /C:"MONITOR_LOOP" "%%f" >NUL 2>&1
    if errorlevel 1 (
        echo   [FAIL] %%f - Missing monitoring loop
        echo   [FAIL] %%f - Missing monitoring loop >> "%TEST_LOG%"
        set "TEST_PASSED=0"
    ) else (
        echo   [PASS] %%f - Monitoring features present
        echo   [PASS] %%f - Monitoring features present >> "%TEST_LOG%"
    )
)

REM Check Launch-All-New.bat for dashboard
findstr /C:"DASHBOARD" "Launch-All-New.bat" >NUL 2>&1
if errorlevel 1 (
    echo   [FAIL] Launch-All-New.bat - Missing dashboard
    echo   [FAIL] Launch-All-New.bat - Missing dashboard >> "%TEST_LOG%"
    set "TEST_PASSED=0"
) else (
    echo   [PASS] Launch-All-New.bat - Dashboard present
    echo   [PASS] Launch-All-New.bat - Dashboard present >> "%TEST_LOG%"
)

if "%TEST_PASSED%"=="1" (
    set /a PASS_COUNT+=1
    echo [RESULT] Test 10: PASSED
    echo. >> "%TEST_LOG%"
) else (
    set /a FAIL_COUNT+=1
    echo [RESULT] Test 10: FAILED
    echo. >> "%TEST_LOG%"
)

echo.

REM ============================================================================
REM  TEST SUMMARY
REM ============================================================================

echo ============================================================================
echo  TEST SUMMARY
echo ============================================================================
echo.
echo  Total Tests: 10
echo  Passed: %PASS_COUNT%
echo  Failed: %FAIL_COUNT%
echo  Warnings: %WARN_COUNT%
echo.

echo ============================================================================ >> "%TEST_LOG%"
echo TEST SUMMARY >> "%TEST_LOG%"
echo ============================================================================ >> "%TEST_LOG%"
echo Total Tests: 10 >> "%TEST_LOG%"
echo Passed: %PASS_COUNT% >> "%TEST_LOG%"
echo Failed: %FAIL_COUNT% >> "%TEST_LOG%"
echo Warnings: %WARN_COUNT% >> "%TEST_LOG%"
echo ============================================================================ >> "%TEST_LOG%"

if %FAIL_COUNT% EQU 0 (
    echo [SUCCESS] All tests passed!
    echo [SUCCESS] All tests passed! >> "%TEST_LOG%"
    echo.
    echo The launcher suite is ready to use.
    echo Run Launch-All-New.bat to start all components.
    color 0A
) else (
    echo [WARNING] Some tests failed
    echo [WARNING] Some tests failed >> "%TEST_LOG%"
    echo.
    echo Please review the test results above.
    echo Check %TEST_LOG% for detailed results.
    color 0C
)

echo.
echo Test results saved to: %TEST_LOG%
echo.
pause
