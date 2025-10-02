# Simple UI Fix Script for ErrorLogViewer
$ErrorActionPreference = "Stop"

Write-Host "`n=== ErrorLogViewer UI Fix ===" -ForegroundColor Cyan

# Fix 1: Add 'new' keyword to EnhancedMainViewModel
Write-Host "`n1. Fixing command declarations..." -ForegroundColor Yellow
$vmFile = "tools\GGs.ErrorLogViewer\ViewModels\EnhancedMainViewModel.cs"
$content = Get-Content $vmFile -Raw
$content = $content -replace 'public ICommand SwitchToLogsViewCommand', 'public new ICommand SwitchToLogsViewCommand'
$content = $content -replace 'public ICommand SwitchToAnalyticsViewCommand', 'public new ICommand SwitchToAnalyticsViewCommand'
$content = $content -replace 'public ICommand SwitchToBookmarksViewCommand', 'public new ICommand SwitchToBookmarksViewCommand'
$content = $content -replace 'public ICommand SwitchToAlertsViewCommand', 'public new ICommand SwitchToAlertsViewCommand'
Set-Content $vmFile -Value $content -NoNewline
Write-Host "  ✓ ViewModel updated" -ForegroundColor Green

# Fix 2: Rebuild
Write-Host "`n2. Rebuilding project..." -ForegroundColor Yellow
Push-Location "tools\GGs.ErrorLogViewer"
$null = dotnet build -c Release --nologo
if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ Build successful" -ForegroundColor Green
} else {
    Write-Host "  ✗ Build failed" -ForegroundColor Red
}
Pop-Location

Write-Host "`n=== Done ===" -ForegroundColor Green
Write-Host "Note: You still need to add view panels to MainWindow.xaml" -ForegroundColor Yellow
Write-Host "See ERRORLOGVIEWER_ISSUES_AND_FIXES.md for details`n" -ForegroundColor Yellow
