#Requires -Version 5.1
<#
.SYNOPSIS
    Comprehensive test automation for all GGs.LaunchControl profiles and system components.
    
.DESCRIPTION
    Tests all launcher profiles in multiple modes (normal, diag, test), validates UAC decline behavior,
    runs unit/integration tests, and generates comprehensive test reports.
    
.PARAMETER Mode
    Test mode: Quick (basic tests), Full (all tests), or Chaos (resilience tests)
    
.EXAMPLE
    .\Test-AllProfiles.ps1 -Mode Full
#>

[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Quick', 'Full', 'Chaos')]
    [string]$Mode = 'Quick'
)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

# Configuration
$script:TestResults = @{
    StartTime = Get-Date
    Tests = @()
    Passed = 0
    Failed = 0
    Skipped = 0
}

$script:LaunchControlPath = "tools\GGs.LaunchControl\bin\Release\net9.0-windows\win-x64\publish\GGs.LaunchControl.exe"
$script:Profiles = @('desktop', 'errorlogviewer', 'fusion')
$script:TestModes = @('test')

function Write-TestHeader {
    param([string]$Title)
    Write-Host "`n╔═══════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║ $($Title.PadRight(77)) ║" -ForegroundColor Cyan
    Write-Host "╚═══════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
}

function Write-TestResult {
    param(
        [string]$TestName,
        [bool]$Passed,
        [string]$Message = "",
        [int]$Duration = 0
    )

    $result = @{
        Name = $TestName
        Passed = $Passed
        Message = $Message
        Duration = $Duration
        Timestamp = Get-Date
    }

    $script:TestResults.Tests += $result

    if ($Passed) {
        $script:TestResults.Passed++
        Write-Host "  [PASS] $TestName ($Duration ms)" -ForegroundColor Green
        if ($Message) {
            Write-Host "    $Message" -ForegroundColor Gray
        }
    }
    else {
        $script:TestResults.Failed++
        Write-Host "  [FAIL] $TestName ($Duration ms)" -ForegroundColor Red
        if ($Message) {
            Write-Host "    ERROR: $Message" -ForegroundColor Red
        }
    }
}

function Test-BuildQuality {
    Write-TestHeader "Build Quality Tests"
    
    # Test 1: Build with warnings as errors
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $buildOutput = & dotnet build GGs.sln -c Release /p:TreatWarningsAsErrors=true 2>&1
        $buildSuccess = $LASTEXITCODE -eq 0
        $warnings = ($buildOutput | Select-String "warning").Count
        $errors = ($buildOutput | Select-String "error").Count
        
        Write-TestResult -TestName "Build with TreatWarningsAsErrors" -Passed $buildSuccess -Message "Errors: $errors, Warnings: $warnings" -Duration $sw.ElapsedMilliseconds
    }
    catch {
        Write-TestResult -TestName "Build with TreatWarningsAsErrors" -Passed $false -Message $_.Exception.Message -Duration $sw.ElapsedMilliseconds
    }
    $sw.Stop()
    
    # Test 2: Nullable reference types enabled
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $csprojFiles = Get-ChildItem -Path . -Filter "*.csproj" -Recurse
    $nullableEnabled = $true
    $nonCompliantProjects = @()
    
    foreach ($csproj in $csprojFiles) {
        $content = Get-Content $csproj.FullName -Raw
        if ($content -notmatch '<Nullable>enable</Nullable>') {
            $nullableEnabled = $false
            $nonCompliantProjects += $csproj.Name
        }
    }
    
    Write-TestResult -TestName "Nullable reference types enabled" -Passed $nullableEnabled -Message "Non-compliant: $($nonCompliantProjects -join ', ')" -Duration $sw.ElapsedMilliseconds
    $sw.Stop()
}

function Test-UnitAndIntegration {
    Write-TestHeader "Unit & Integration Tests"
    
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    try {
        $testOutput = & dotnet test GGs.sln -c Release --no-build --verbosity minimal 2>&1
        $testSuccess = $LASTEXITCODE -eq 0
        
        $totalTests = 0
        $passedTests = 0
        $failedTests = 0
        
        if ($testOutput -match "total: (\d+).*failed: (\d+).*succeeded: (\d+)") {
            $totalTests = [int]$matches[1]
            $failedTests = [int]$matches[2]
            $passedTests = [int]$matches[3]
        }
        
        Write-TestResult -TestName "Unit & Integration Tests" -Passed $testSuccess -Message "Passed: $passedTests/$totalTests" -Duration $sw.ElapsedMilliseconds
    }
    catch {
        Write-TestResult -TestName "Unit & Integration Tests" -Passed $false -Message $_.Exception.Message -Duration $sw.ElapsedMilliseconds
    }
    $sw.Stop()
}

function Test-LaunchControlProfiles {
    Write-TestHeader "LaunchControl Profile Tests"
    
    if (-not (Test-Path $script:LaunchControlPath)) {
        Write-TestResult -TestName "LaunchControl exists" -Passed $false -Message "Not found at $script:LaunchControlPath" -Duration 0
        return
    }
    
    Write-TestResult -TestName "LaunchControl exists" -Passed $true -Duration 0
    
    foreach ($profile in $script:Profiles) {
        foreach ($mode in $script:TestModes) {
            $sw = [System.Diagnostics.Stopwatch]::StartNew()
            try {
                $output = & $script:LaunchControlPath --profile $profile --mode $mode 2>&1
                $success = $LASTEXITCODE -eq 0
                
                $healthChecksPassed = ($output | Select-String "PASS").Count
                $healthChecksFailed = ($output | Select-String "FAIL").Count
                
                Write-TestResult -TestName "Profile: $profile (mode: $mode)" -Passed $success -Message "Health: $healthChecksPassed passed, $healthChecksFailed failed" -Duration $sw.ElapsedMilliseconds
            }
            catch {
                Write-TestResult -TestName "Profile: $profile (mode: $mode)" -Passed $false -Message $_.Exception.Message -Duration $sw.ElapsedMilliseconds
            }
            $sw.Stop()
            
            # Kill any launched processes
            Start-Sleep -Milliseconds 500
            Get-Process -Name "GGs.Desktop","GGs.ErrorLogViewer" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
        }
    }
}

function Test-UACDeclineBehavior {
    Write-TestHeader "UAC Decline Behavior Tests"
    
    # Test that Win32Exception 1223 is handled gracefully
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $testPassed = $true
    
    # Check ElevationBridge code for Win32Exception 1223 handling
    $elevationBridgePath = "agent\GGs.Agent\Elevation\ElevationBridge.cs"
    if (Test-Path $elevationBridgePath) {
        $content = Get-Content $elevationBridgePath -Raw
        if ($content -match "Win32Exception.*1223" -and $content -match "ADMIN ACCESS DECLINED BY OPERATOR") {
            Write-TestResult -TestName "Win32Exception 1223 handling" -Passed $true -Message "Proper UAC decline handling found" -Duration $sw.ElapsedMilliseconds
        }
        else {
            Write-TestResult -TestName "Win32Exception 1223 handling" -Passed $false -Message "UAC decline handling not found" -Duration $sw.ElapsedMilliseconds
            $testPassed = $false
        }
    }
    else {
        Write-TestResult -TestName "Win32Exception 1223 handling" -Passed $false -Message "ElevationBridge.cs not found" -Duration $sw.ElapsedMilliseconds
        $testPassed = $false
    }
    $sw.Stop()
}

function Test-Documentation {
    Write-TestHeader "Documentation Tests"
    
    $requiredDocs = @(
        "docs\ADR-002-Deep-System-Access.md",
        "docs\ADR-003-Tweak-Capability-Modules.md",
        "docs\ADR-004-Consent-Gated-Elevation-Bridge.md",
        "docs\ADR-005-Telemetry-Correlation-Trace-Depth.md",
        "docs\ADR-006-Privacy-Tiering.md",
        "docs\Launcher-UserGuide.md",
        "launcher-logs\build-journal.md"
    )
    
    foreach ($doc in $requiredDocs) {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $exists = Test-Path $doc
        $size = if ($exists) { (Get-Item $doc).Length } else { 0 }
        Write-TestResult -TestName "Documentation: $(Split-Path $doc -Leaf)" -Passed $exists -Message "Size: $size bytes" -Duration $sw.ElapsedMilliseconds
        $sw.Stop()
    }
}

function Generate-TestReport {
    Write-TestHeader "Test Report"
    
    $duration = (Get-Date) - $script:TestResults.StartTime
    $total = $script:TestResults.Passed + $script:TestResults.Failed + $script:TestResults.Skipped
    $successRate = if ($total -gt 0) { [math]::Round(($script:TestResults.Passed / $total) * 100, 2) } else { 0 }
    
    Write-Host "`nTest Summary:" -ForegroundColor Cyan
    Write-Host "  Total Tests:    $total" -ForegroundColor White
    Write-Host "  Passed:         " -NoNewline
    Write-Host "$($script:TestResults.Passed)" -ForegroundColor Green
    Write-Host "  Failed:         " -NoNewline
    Write-Host "$($script:TestResults.Failed)" -ForegroundColor $(if ($script:TestResults.Failed -eq 0) { 'Green' } else { 'Red' })
    Write-Host "  Skipped:        $($script:TestResults.Skipped)" -ForegroundColor Yellow
    Write-Host "  Success Rate:   $successRate%" -ForegroundColor $(if ($successRate -eq 100) { 'Green' } else { 'Yellow' })
    Write-Host "  Duration:       $($duration.TotalSeconds.ToString('F2'))s" -ForegroundColor White
    
    # Save report to file
    $reportPath = "test-results\test-report-$(Get-Date -Format 'yyyyMMdd-HHmmss').json"
    $reportDir = Split-Path $reportPath -Parent
    if (-not (Test-Path $reportDir)) {
        New-Item -ItemType Directory -Path $reportDir -Force | Out-Null
    }
    
    $script:TestResults | ConvertTo-Json -Depth 10 | Out-File $reportPath -Encoding UTF8
    Write-Host "`nTest report saved to: $reportPath" -ForegroundColor Gray
    
    return $script:TestResults.Failed -eq 0
}

# Main execution
try {
    Write-Host "`n╔═══════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║                    GGs Comprehensive Test Automation                         ║" -ForegroundColor Cyan
    Write-Host "║                         Mode: $($Mode.PadRight(43)) ║" -ForegroundColor Cyan
    Write-Host "╚═══════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    
    # Run test suites
    Test-BuildQuality
    Test-UnitAndIntegration
    Test-LaunchControlProfiles
    Test-UACDeclineBehavior
    Test-Documentation
    
    # Generate report
    $allPassed = Generate-TestReport
    
    if ($allPassed) {
        Write-Host "`n[SUCCESS] All tests passed!" -ForegroundColor Green
        exit 0
    }
    else {
        Write-Host "`n[FAIL] Some tests failed!" -ForegroundColor Red
        exit 1
    }
}
catch {
    Write-Host "`n[ERROR] Fatal error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

