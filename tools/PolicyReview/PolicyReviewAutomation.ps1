#Requires -Version 5.1
<#
.SYNOPSIS
    Automated quarterly policy review for GGs.Agent compliance.
    
.DESCRIPTION
    Reviews ScriptPolicy patterns, privacy tiers, and compliance alignment with GDPR/CCPA.
    Generates evidence bundles for audits and compliance badges for release notes.
    
.EXAMPLE
    .\PolicyReviewAutomation.ps1
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# Configuration
$script:ReviewResults = @{
    Timestamp = Get-Date
    WindowsVersion = [System.Environment]::OSVersion.Version.ToString()
    PolicyVersion = "1.0.0"
    Findings = @()
    ComplianceScore = 0
}

$script:ScriptPolicyPath = "..\..\agent\GGs.Agent\Policy\ScriptPolicy.cs"
$script:PrivacyTieringPath = "..\..\docs\ADR-006-Privacy-Tiering.md"

function Write-ReviewHeader {
    param([string]$Title)
    Write-Host "`n╔═══════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║ $($Title.PadRight(77)) ║" -ForegroundColor Cyan
    Write-Host "╚═══════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
}

function Add-Finding {
    param(
        [string]$Category,
        [string]$Finding,
        [ValidateSet('Pass', 'Warning', 'Fail')]
        [string]$Status,
        [string]$Recommendation = ""
    )
    
    $script:ReviewResults.Findings += @{
        Category = $Category
        Finding = $Finding
        Status = $Status
        Recommendation = $Recommendation
        Timestamp = Get-Date
    }
    
    $color = switch ($Status) {
        'Pass' { 'Green' }
        'Warning' { 'Yellow' }
        'Fail' { 'Red' }
    }
    
    $icon = switch ($Status) {
        'Pass' { '✓' }
        'Warning' { '⚠' }
        'Fail' { '✗' }
    }
    
    Write-Host "  $icon " -ForegroundColor $color -NoNewline
    Write-Host "$Finding" -ForegroundColor White
    if ($Recommendation) {
        Write-Host "    → $Recommendation" -ForegroundColor Gray
    }
}

function Review-ScriptPolicyPatterns {
    Write-ReviewHeader "Script Policy Pattern Review"
    
    if (-not (Test-Path $script:ScriptPolicyPath)) {
        Add-Finding -Category "ScriptPolicy" -Finding "ScriptPolicy.cs not found" -Status Fail -Recommendation "Ensure policy file exists"
        return
    }
    
    $content = Get-Content $script:ScriptPolicyPath -Raw
    
    # Check blocked patterns count
    $blockedMatches = [regex]::Matches($content, 'new\s+Regex\([^)]+\)\s*,\s*//\s*Blocked')
    $blockedCount = $blockedMatches.Count
    
    if ($blockedCount -ge 90) {
        Add-Finding -Category "ScriptPolicy" -Finding "Blocked patterns: $blockedCount (target: 90+)" -Status Pass
    } else {
        Add-Finding -Category "ScriptPolicy" -Finding "Blocked patterns: $blockedCount (target: 90+)" -Status Warning -Recommendation "Review and add more blocked patterns"
    }
    
    # Check allowed patterns count
    $allowedMatches = [regex]::Matches($content, 'new\s+Regex\([^)]+\)\s*,\s*//\s*Allowed')
    $allowedCount = $allowedMatches.Count
    
    if ($allowedCount -ge 30) {
        Add-Finding -Category "ScriptPolicy" -Finding "Allowed patterns: $allowedCount (target: 30+)" -Status Pass
    } else {
        Add-Finding -Category "ScriptPolicy" -Finding "Allowed patterns: $allowedCount (target: 30+)" -Status Warning -Recommendation "Review and add more allowed patterns"
    }
    
    # Check for dangerous patterns
    $dangerousPatterns = @(
        'Invoke-Expression',
        'IEX',
        'DownloadString',
        'DownloadFile',
        'Start-Process.*-Verb RunAs',
        'New-Object.*Net.WebClient',
        'Invoke-WebRequest.*-UseBasicParsing',
        'Set-ExecutionPolicy',
        'Add-MpPreference.*-ExclusionPath'
    )
    
    $missingPatterns = @()
    foreach ($pattern in $dangerousPatterns) {
        if ($content -notmatch [regex]::Escape($pattern)) {
            $missingPatterns += $pattern
        }
    }
    
    if ($missingPatterns.Count -eq 0) {
        Add-Finding -Category "ScriptPolicy" -Finding "All critical dangerous patterns blocked" -Status Pass
    } else {
        Add-Finding -Category "ScriptPolicy" -Finding "Missing dangerous patterns: $($missingPatterns.Count)" -Status Warning -Recommendation "Add patterns: $($missingPatterns -join ', ')"
    }
}

function Review-PrivacyTiering {
    Write-ReviewHeader "Privacy Tiering Review"
    
    if (-not (Test-Path $script:PrivacyTieringPath)) {
        Add-Finding -Category "Privacy" -Finding "Privacy tiering ADR not found" -Status Fail -Recommendation "Create ADR-006-Privacy-Tiering.md"
        return
    }
    
    $content = Get-Content $script:PrivacyTieringPath -Raw
    
    # Check for required privacy tiers
    $requiredTiers = @('Public', 'Internal', 'Confidential', 'Restricted', 'Secret')
    $missingTiers = @()
    
    foreach ($tier in $requiredTiers) {
        if ($content -notmatch $tier) {
            $missingTiers += $tier
        }
    }
    
    if ($missingTiers.Count -eq 0) {
        Add-Finding -Category "Privacy" -Finding "All 5 privacy tiers documented" -Status Pass
    } else {
        Add-Finding -Category "Privacy" -Finding "Missing privacy tiers: $($missingTiers -join ', ')" -Status Fail -Recommendation "Document all 5 privacy tiers"
    }
    
    # Check for GDPR compliance
    if ($content -match 'GDPR') {
        Add-Finding -Category "Privacy" -Finding "GDPR compliance documented" -Status Pass
    } else {
        Add-Finding -Category "Privacy" -Finding "GDPR compliance not documented" -Status Warning -Recommendation "Add GDPR compliance section"
    }
    
    # Check for CCPA compliance
    if ($content -match 'CCPA') {
        Add-Finding -Category "Privacy" -Finding "CCPA compliance documented" -Status Pass
    } else {
        Add-Finding -Category "Privacy" -Finding "CCPA compliance not documented" -Status Warning -Recommendation "Add CCPA compliance section"
    }
}

function Review-TestCoverage {
    Write-ReviewHeader "Policy Test Coverage Review"
    
    # Check for policy tests
    $policyTestPath = "..\..\tests\GGs.Enterprise.Tests"
    if (Test-Path $policyTestPath) {
        $testFiles = Get-ChildItem -Path $policyTestPath -Filter "*Policy*Tests.cs" -Recurse
        
        if ($testFiles.Count -gt 0) {
            Add-Finding -Category "Testing" -Finding "Policy test files found: $($testFiles.Count)" -Status Pass
        } else {
            Add-Finding -Category "Testing" -Finding "No policy test files found" -Status Warning -Recommendation "Create policy-specific test files"
        }
    } else {
        Add-Finding -Category "Testing" -Finding "Test directory not found" -Status Fail -Recommendation "Ensure test project exists"
    }
    
    # Check test results
    try {
        $testOutput = & dotnet test ..\..\GGs.sln -c Release --no-build --verbosity minimal --filter "FullyQualifiedName~Policy" 2>&1
        if ($LASTEXITCODE -eq 0) {
            Add-Finding -Category "Testing" -Finding "Policy tests passing" -Status Pass
        } else {
            Add-Finding -Category "Testing" -Finding "Policy tests failing" -Status Fail -Recommendation "Fix failing policy tests"
        }
    } catch {
        Add-Finding -Category "Testing" -Finding "Could not run policy tests" -Status Warning -Recommendation "Ensure solution is built"
    }
}

function Review-WindowsCompatibility {
    Write-ReviewHeader "Windows Version Compatibility Review"
    
    $windowsVersion = [System.Environment]::OSVersion.Version
    
    # Check Windows version
    if ($windowsVersion.Major -ge 10) {
        Add-Finding -Category "Compatibility" -Finding "Windows version: $($windowsVersion.ToString())" -Status Pass
    } else {
        Add-Finding -Category "Compatibility" -Finding "Windows version: $($windowsVersion.ToString())" -Status Warning -Recommendation "Test on Windows 10/11"
    }
    
    # Check .NET version
    try {
        $dotnetVersion = & dotnet --version 2>&1
        if ($dotnetVersion -match '^9\.') {
            Add-Finding -Category "Compatibility" -Finding ".NET version: $dotnetVersion" -Status Pass
        } else {
            Add-Finding -Category "Compatibility" -Finding ".NET version: $dotnetVersion" -Status Warning -Recommendation "Upgrade to .NET 9"
        }
    } catch {
        Add-Finding -Category "Compatibility" -Finding ".NET not found" -Status Fail -Recommendation "Install .NET 9 SDK"
    }
}

function Calculate-ComplianceScore {
    $total = $script:ReviewResults.Findings.Count
    $passed = ($script:ReviewResults.Findings | Where-Object { $_.Status -eq 'Pass' }).Count
    
    if ($total -gt 0) {
        $script:ReviewResults.ComplianceScore = [math]::Round(($passed / $total) * 100, 2)
    } else {
        $script:ReviewResults.ComplianceScore = 0
    }
}

function Generate-ComplianceBadge {
    $score = $script:ReviewResults.ComplianceScore
    
    $color = if ($score -ge 90) { 'brightgreen' }
              elseif ($score -ge 75) { 'green' }
              elseif ($score -ge 60) { 'yellow' }
              elseif ($score -ge 40) { 'orange' }
              else { 'red' }
    
    $badge = "![Compliance Score](https://img.shields.io/badge/Compliance-$score%25-$color)"
    
    return $badge
}

function Generate-EvidenceBundle {
    Write-ReviewHeader "Generating Evidence Bundle"
    
    $bundlePath = "policy-review-$(Get-Date -Format 'yyyyMMdd-HHmmss')"
    New-Item -ItemType Directory -Path $bundlePath -Force | Out-Null
    
    # Save review results
    $script:ReviewResults | ConvertTo-Json -Depth 10 | Out-File "$bundlePath\review-results.json" -Encoding UTF8
    
    # Copy policy files
    if (Test-Path $script:ScriptPolicyPath) {
        Copy-Item $script:ScriptPolicyPath "$bundlePath\ScriptPolicy.cs"
    }
    
    if (Test-Path $script:PrivacyTieringPath) {
        Copy-Item $script:PrivacyTieringPath "$bundlePath\ADR-006-Privacy-Tiering.md"
    }
    
    # Generate compliance badge
    $badge = Generate-ComplianceBadge
    $badge | Out-File "$bundlePath\compliance-badge.md" -Encoding UTF8
    
    # Generate summary report
    $summary = @"
# Policy Review Summary
**Date:** $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')
**Windows Version:** $($script:ReviewResults.WindowsVersion)
**Policy Version:** $($script:ReviewResults.PolicyVersion)
**Compliance Score:** $($script:ReviewResults.ComplianceScore)%

$badge

## Findings

$(foreach ($finding in $script:ReviewResults.Findings) {
    "- [$($finding.Status)] **$($finding.Category):** $($finding.Finding)"
    if ($finding.Recommendation) {
        "  - Recommendation: $($finding.Recommendation)"
    }
})

## Next Steps

1. Address all FAIL findings immediately
2. Review and resolve WARNING findings
3. Schedule next quarterly review
4. Update compliance documentation

---
*Generated by GGs Policy Review Automation*
"@
    
    $summary | Out-File "$bundlePath\SUMMARY.md" -Encoding UTF8
    
    Write-Host "`nEvidence bundle saved to: $bundlePath" -ForegroundColor Green
    
    return $bundlePath
}

# Main execution
try {
    Write-Host "`n╔═══════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║                    GGs Policy Review Automation                               ║" -ForegroundColor Cyan
    Write-Host "║                    Quarterly Compliance Review                                ║" -ForegroundColor Cyan
    Write-Host "╚═══════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    
    # Run reviews
    Review-ScriptPolicyPatterns
    Review-PrivacyTiering
    Review-TestCoverage
    Review-WindowsCompatibility
    
    # Calculate compliance score
    Calculate-ComplianceScore
    
    # Generate evidence bundle
    $bundlePath = Generate-EvidenceBundle
    
    # Display summary
    Write-Host "`n╔═══════════════════════════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
    Write-Host "║                           Review Summary                                      ║" -ForegroundColor Cyan
    Write-Host "╚═══════════════════════════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
    
    $passCount = ($script:ReviewResults.Findings | Where-Object { $_.Status -eq 'Pass' }).Count
    $warnCount = ($script:ReviewResults.Findings | Where-Object { $_.Status -eq 'Warning' }).Count
    $failCount = ($script:ReviewResults.Findings | Where-Object { $_.Status -eq 'Fail' }).Count
    
    Write-Host "`nCompliance Score: " -NoNewline
    $scoreColor = if ($script:ReviewResults.ComplianceScore -ge 90) { 'Green' }
                  elseif ($script:ReviewResults.ComplianceScore -ge 75) { 'Yellow' }
                  else { 'Red' }
    Write-Host "$($script:ReviewResults.ComplianceScore)%" -ForegroundColor $scoreColor
    
    Write-Host "`nFindings:"
    Write-Host "  Pass:    " -NoNewline; Write-Host $passCount -ForegroundColor Green
    Write-Host "  Warning: " -NoNewline; Write-Host $warnCount -ForegroundColor Yellow
    Write-Host "  Fail:    " -NoNewline; Write-Host $failCount -ForegroundColor Red
    
    Write-Host "`nEvidence Bundle: $bundlePath" -ForegroundColor Gray
    Write-Host "Compliance Badge: $(Generate-ComplianceBadge)" -ForegroundColor Gray
    
    if ($failCount -eq 0) {
        Write-Host "`n✓ Policy review completed successfully!" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "`n⚠ Policy review completed with failures!" -ForegroundColor Yellow
        exit 1
    }
} catch {
    Write-Host "`n✗ Fatal error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

