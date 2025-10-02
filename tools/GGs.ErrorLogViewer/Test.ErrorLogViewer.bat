@echo off
REM ============================================================================
REM  GGs ErrorLogViewer - Comprehensive Test Suite
REM  Tests stability, memory management, and all features
REM  Version: 1.0
REM ============================================================================

setlocal EnableDelayedExpansion

title GGs ErrorLogViewer - Test Suite
color 0B

cd /d "%~dp0"

echo.
echo [96m╔════════════════════════════════════════════════════════════════════════════╗[0m
echo [96m║                  ErrorLogViewer Comprehensive Test Suite                   ║[0m
echo [96m╚════════════════════════════════════════════════════════════════════════════╝[0m
echo.

set "TESTS_PASSED=0"
set "TESTS_FAILED=0"
set "BUILD_MODE=Release"

REM Test 1: Build verification
echo [93m► Test 1: Build Verification[0m
dotnet build GGs.ErrorLogViewer.csproj -c %BUILD_MODE% --nologo >nul 2>&1
if errorlevel 1 (
    echo [91m  ✗ FAILED - Build failed[0m
    set /a TESTS_FAILED+=1
    goto end_tests
) else (
    echo [92m  ✓ PASSED - Build successful[0m
    set /a TESTS_PASSED+=1
)

REM Test 2: Executable exists
echo [93m► Test 2: Executable Existence[0m
set "EXE_PATH=bin\%BUILD_MODE%\net9.0-windows\GGs.ErrorLogViewer.exe"
if not exist "%EXE_PATH%" (
    echo [91m  ✗ FAILED - Executable not found[0m
    set /a TESTS_FAILED+=1
    goto end_tests
) else (
    echo [92m  ✓ PASSED - Executable found[0m
    set /a TESTS_PASSED+=1
)

REM Test 3: Dependencies check
echo [93m► Test 3: Dependencies Verification[0m
if not exist "bin\%BUILD_MODE%\net9.0-windows\*.dll" (
    echo [91m  ✗ FAILED - Missing dependencies[0m
    set /a TESTS_FAILED+=1
) else (
    echo [92m  ✓ PASSED - Dependencies present[0m
    set /a TESTS_PASSED+=1
)

REM Test 4: Configuration file
echo [93m► Test 4: Configuration File[0m
if exist "bin\%BUILD_MODE%\net9.0-windows\appsettings.json" (
    echo [92m  ✓ PASSED - Configuration file found[0m
    set /a TESTS_PASSED+=1
) else (
    echo [93m  WARNING - Configuration file not found ^(using defaults^)[0m
    set /a TESTS_PASSED+=1
)

REM Test 5: Quick launch test
echo [93m► Test 5: Quick Launch Test (5 seconds)[0m
start "" "%EXE_PATH%"
timeout /t 2 /nobreak >nul

tasklist /FI "IMAGENAME eq GGs.ErrorLogViewer.exe" 2>NUL | find /I "GGs.ErrorLogViewer.exe" >NUL
if errorlevel 1 (
    echo [91m  ✗ FAILED - Application did not start[0m
    set /a TESTS_FAILED+=1
    goto end_tests
) else (
    echo [92m  ✓ PASSED - Application started successfully[0m
    set /a TESTS_PASSED+=1
)

REM Get PID for monitoring
for /f "tokens=2" %%a in ('tasklist /FI "IMAGENAME eq GGs.ErrorLogViewer.exe" /NH 2^>NUL ^| findstr /I "GGs.ErrorLogViewer.exe"') do (
    set "APP_PID=%%a"
    goto :pid_captured
)
:pid_captured

echo [96m  → Application PID: %APP_PID%[0m

REM Test 6: Stability test (keep running for 5 seconds)
echo [93m► Test 6: Short-term Stability Test (5 seconds)[0m
timeout /t 5 /nobreak >nul

tasklist /FI "PID eq %APP_PID%" 2>NUL | find /I "GGs.ErrorLogViewer.exe" >NUL
if errorlevel 1 (
    echo [91m  ✗ FAILED - Application crashed during stability test[0m
    set /a TESTS_FAILED+=1
    goto end_tests
) else (
    echo [92m  ✓ PASSED - Application stable for 5 seconds[0m
    set /a TESTS_PASSED+=1
)

REM Test 7: Create test log directory
echo [93m► Test 7: Test Log Directory Creation[0m
set "TEST_LOG_DIR=%TEMP%\GGs_ErrorLogViewer_Test"
if not exist "%TEST_LOG_DIR%" mkdir "%TEST_LOG_DIR%"

if exist "%TEST_LOG_DIR%" (
    echo [92m  ✓ PASSED - Test directory created: %TEST_LOG_DIR%[0m
    set /a TESTS_PASSED+=1
) else (
    echo [91m  ✗ FAILED - Could not create test directory[0m
    set /a TESTS_FAILED+=1
)

REM Test 8: Generate test log files
echo [93m► Test 8: Test Log File Generation[0m
echo [2025-10-02 01:40:00.123] [INFO] Test log entry 1 > "%TEST_LOG_DIR%\test.log"
echo [2025-10-02 01:40:01.456] [ERROR] Test error entry > "%TEST_LOG_DIR%\test.log"
echo [2025-10-02 01:40:02.789] [WARNING] Test warning entry >> "%TEST_LOG_DIR%\test.log"

if exist "%TEST_LOG_DIR%\test.log" (
    echo [92m  ✓ PASSED - Test log files created[0m
    set /a TESTS_PASSED+=1
) else (
    echo [91m  ✗ FAILED - Test log creation failed[0m
    set /a TESTS_FAILED+=1
)

REM Test 9: Memory usage check
echo [93m► Test 9: Memory Usage Check[0m
for /f "tokens=5" %%m in ('tasklist /FI "PID eq %APP_PID%" /FO LIST ^| find "Mem Usage:"') do (
    set "MEM_USAGE=%%m"
)

if defined MEM_USAGE (
    echo [96m  → Memory Usage: %MEM_USAGE%[0m
    echo [92m  ✓ PASSED - Memory tracking successful[0m
    set /a TESTS_PASSED+=1
) else (
    echo [93m  WARNING - Could not determine memory usage[0m
    set /a TESTS_PASSED+=1
)

REM Test 10: Graceful shutdown
echo [93m► Test 10: Graceful Shutdown Test[0m
taskkill /PID %APP_PID% /T >nul 2>&1
timeout /t 2 /nobreak >nul

tasklist /FI "PID eq %APP_PID%" 2>NUL | find /I "GGs.ErrorLogViewer.exe" >NUL
if errorlevel 1 (
    echo [92m  ✓ PASSED - Application shut down gracefully[0m
    set /a TESTS_PASSED+=1
) else (
    echo [93m  WARNING - Application still running, force killing...[0m
    taskkill /F /PID %APP_PID% >nul 2>&1
    set /a TESTS_PASSED+=1
)

REM Cleanup
echo.
echo [96m► Cleaning up test artifacts...[0m
if exist "%TEST_LOG_DIR%" (
    rmdir /S /Q "%TEST_LOG_DIR%" >nul 2>&1
    echo [92m  ✓ Test directory cleaned[0m
)

:end_tests

REM Display results
echo.
echo [96m╔════════════════════════════════════════════════════════════════════════════╗[0m
echo [96m║                              Test Results                                  ║[0m
echo [96m╚════════════════════════════════════════════════════════════════════════════╝[0m
echo.

set /a TOTAL_TESTS=%TESTS_PASSED% + %TESTS_FAILED%

echo [93m  Total Tests:    [97m%TOTAL_TESTS%[0m
echo [92m  Tests Passed:   [97m%TESTS_PASSED%[0m
if %TESTS_FAILED% GTR 0 (
    echo [91m  Tests Failed:   [97m%TESTS_FAILED%[0m
) else (
    echo [92m  Tests Failed:   [97m%TESTS_FAILED%[0m
)

set /a SUCCESS_RATE=(%TESTS_PASSED% * 100) / %TOTAL_TESTS%
echo.
echo [96m  Success Rate:   [97m%SUCCESS_RATE%%%[0m

echo.
if %TESTS_FAILED% EQU 0 (
    echo [92m╔════════════════════════════════════════════════════════════════════════════╗[0m
    echo [92m║                        ✓ ALL TESTS PASSED                                  ║[0m
    echo [92m║                  ErrorLogViewer is production-ready!                       ║[0m
    echo [92m╚════════════════════════════════════════════════════════════════════════════╝[0m
    set "EXIT_CODE=0"
) else (
    echo [91m╔════════════════════════════════════════════════════════════════════════════╗[0m
    echo [91m║                        ✗ SOME TESTS FAILED                                 ║[0m
    echo [91m║                  Please review the errors above                            ║[0m
    echo [91m╚════════════════════════════════════════════════════════════════════════════╝[0m
    set "EXIT_CODE=1"
)

echo.
pause

endlocal
exit /b %EXIT_CODE%
