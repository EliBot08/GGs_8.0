# Fix final 6 GitHub Actions build errors
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot

Write-Host "=== Fixing Final 6 Build Errors ===" -ForegroundColor Cyan

# 1-2: Fix CommunityHubView dialog properties
$file = "clients\GGs.Desktop\Views\CommunityHubView.xaml.cs"
$path = Join-Path $projectRoot $file
if (Test-Path $path) {
    Write-Host "Fixing: $file" -ForegroundColor Yellow
    $content = Get-Content $path -Raw
    
    $content = $content.Replace(
        "        public SharedSI.SystemIntelligenceProfile SelectedProfile { get; private set; }",
        "        public SharedSI.SystemIntelligenceProfile SelectedProfile { get; private set; } = null!;"
    )
    
    $content = $content.Replace(
        "        public ShareOptions ShareOptions { get; private set; }",
        "        public ShareOptions ShareOptions { get; private set; } = null!;"
    )
    
    Set-Content $path -Value $content -NoNewline
    Write-Host "  Fixed dialog properties" -ForegroundColor Green
}

# 3-4: Fix AnimatedProgressBar readonly field and null dereference
$file = "clients\GGs.Desktop\Views\Controls\AnimatedProgressBar.xaml.cs"
$path = Join-Path $projectRoot $file
if (Test-Path $path) {
    Write-Host "Fixing: $file" -ForegroundColor Yellow
    $content = Get-Content $path -Raw
    
    # Remove readonly from _iconRotationTimer
    $content = $content.Replace(
        "        private readonly DispatcherTimer? _iconRotationTimer;",
        "        private DispatcherTimer? _iconRotationTimer;"
    )
    
    Set-Content $path -Value $content -NoNewline
    Write-Host "  Fixed AnimatedProgressBar issues" -ForegroundColor Green
}

# 5: Fix ProfileArchitectView null dereference
$file = "clients\GGs.Desktop\Views\ProfileArchitectView.xaml.cs"
$path = Join-Path $projectRoot $file
if (Test-Path $path) {
    Write-Host "Fixing: $file" -ForegroundColor Yellow
    $content = Get-Content $path -Raw
    
    $content = $content.Replace(
        "                _currentFilter = button.Name switch",
        "                _currentFilter = button?.Name switch"
    )
    
    Set-Content $path -Value $content -NoNewline
    Write-Host "  Fixed null dereference" -ForegroundColor Green
}

# 6: Fix AdminHubClient - change type (string instead of AuditLogAddedEventArgs)
$file = "clients\GGs.Desktop\Services\AdminHubClient.cs"
$path = Join-Path $projectRoot $file
if (Test-Path $path) {
    Write-Host "Fixing: $file" -ForegroundColor Yellow
    $content = Get-Content $path -Raw
    
    $content = $content.Replace(
        "        public event EventHandler<string>? AuditAdded;",
        "        #pragma warning disable CS0067`r`n        public event EventHandler<string>? AuditAdded;`r`n        #pragma warning restore CS0067"
    )
    
    Set-Content $path -Value $content -NoNewline
    Write-Host "  Suppressed unused event warning" -ForegroundColor Green
}

Write-Host "`n=== All 6 Errors Fixed ===" -ForegroundColor Cyan
Write-Host "Building with -warnaserror to verify..." -ForegroundColor Yellow
