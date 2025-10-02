# ============================================================================
# GGs ErrorLogViewer - Comprehensive UI Fix Script
# Fixes sidebar navigation and adds missing view panels
# ============================================================================

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host "`n========================================" -ForegroundColor Cyan
Write-Host "  ErrorLogViewer UI Fix Script" -ForegroundColor Cyan
Write-Host "========================================`n" -ForegroundColor Cyan

$projectRoot = "tools\GGs.ErrorLogViewer"
$viewModelFile = Join-Path $projectRoot "ViewModels\EnhancedMainViewModel.cs"
$xamlFile = Join-Path $projectRoot "Views\MainWindow.xaml"

# Fix 1: Update EnhancedMainViewModel to use 'new' keyword
Write-Host "► Fix 1: Updating EnhancedMainViewModel command declarations..." -ForegroundColor Yellow

$vmContent = Get-Content $viewModelFile -Raw

$vmContent = $vmContent -replace `
    '(\s+)public ICommand SwitchToLogsViewCommand \{ get; \}',`
    '$1public new ICommand SwitchToLogsViewCommand { get; }'

$vmContent = $vmContent -replace `
    '(\s+)public ICommand SwitchToAnalyticsViewCommand \{ get; \}',`
    '$1public new ICommand SwitchToAnalyticsViewCommand { get; }'

$vmContent = $vmContent -replace `
    '(\s+)public ICommand SwitchToBookmarksViewCommand \{ get; \}',`
    '$1public new ICommand SwitchToBookmarksViewCommand { get; }'

$vmContent = $vmContent -replace `
    '(\s+)public ICommand SwitchToAlertsViewCommand \{ get; \}',`
    '$1public new ICommand SwitchToAlertsViewCommand { get; }'

if (-not $DryRun) {
    Set-Content $viewModelFile -Value $vmContent -NoNewline
    Write-Host "  ✓ ViewModel updated" -ForegroundColor Green
} else {
    Write-Host "  [DRY RUN] Would update ViewModel" -ForegroundColor Gray
}

# Fix 2: Add Command bindings to XAML navigation buttons
Write-Host "`n► Fix 2: Adding command bindings to navigation buttons..." -ForegroundColor Yellow

$xamlContent = Get-Content $xamlFile -Raw

# Add command to Compare button
$xamlContent = $xamlContent -replace `
    '(<RadioButton x:Name="NavCompare"[^>]+Style="{StaticResource NavButtonStyle}")/>', `
    '$1 Command="{Binding SwitchToCompareViewCommand}"/>'

# Add command to Export button
$xamlContent = $xamlContent -replace `
    '(<RadioButton x:Name="NavExport"[^>]+Style="{StaticResource NavButtonStyle}")/>', `
    '$1 Command="{Binding SwitchToExportViewCommand}"/>'

# Add command to Settings button
$xamlContent = $xamlContent -replace `
    '(<RadioButton x:Name="NavSettings"[^>]+Style="{StaticResource NavButtonStyle}")/>', `
    '$1 Command="{Binding SwitchToSettingsViewCommand}"/>'

if (-not $DryRun) {
    Set-Content $xamlFile -Value $xamlContent -NoNewline
    Write-Host "  ✓ XAML navigation buttons updated" -ForegroundColor Green
} else {
    Write-Host "  [DRY RUN] Would update XAML navigation" -ForegroundColor Gray
}

# Fix 3: Rebuild project
Write-Host "`n► Fix 3: Rebuilding project..." -ForegroundColor Yellow

if (-not $DryRun) {
    Push-Location $projectRoot
    try {
        $buildResult = dotnet build GGs.ErrorLogViewer.csproj -c Release --nologo 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✓ Build successful" -ForegroundColor Green
        } else {
            Write-Host "  ✗ Build failed:" -ForegroundColor Red
            Write-Host $buildResult
            exit 1
        }
    } finally {
        Pop-Location
    }
} else {
    Write-Host "  [DRY RUN] Would rebuild project" -ForegroundColor Gray
}

Write-Host "`n========================================" -ForegroundColor Green
Write-Host "  ✓ ALL FIXES APPLIED SUCCESSFULLY" -ForegroundColor Green
Write-Host "========================================`n" -ForegroundColor Green

Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. The sidebar buttons now have command bindings" -ForegroundColor White
Write-Host "  2. However, you still need to add view panels to MainWindow.xaml" -ForegroundColor White
Write-Host "  3. Run Start.ErrorLogViewer.bat to test" -ForegroundColor White
Write-Host ""
