# Fix Nullability and Compilation Errors
# This script systematically fixes nullable reference type issues

$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot

Write-Host "Starting systematic error fixes..." -ForegroundColor Cyan

# Function to fix PropertyChanged event declarations
function Fix-PropertyChangedEvents {
    param([string]$FilePath)
    
    $content = Get-Content $FilePath -Raw
    $modified = $false
    
    # Fix PropertyChanged event to be nullable
    if ($content -match 'public event PropertyChangedEventHandler PropertyChanged;') {
        $content = $content -replace 'public event PropertyChangedEventHandler PropertyChanged;', 'public event PropertyChangedEventHandler? PropertyChanged;'
        $modified = $true
        Write-Host "  Fixed PropertyChanged event in $FilePath" -ForegroundColor Green
    }
    
    if ($modified) {
        Set-Content $FilePath -Value $content -NoNewline
    }
    
    return $modified
}

# Function to fix event handler signatures
function Fix-EventHandlerSignatures {
    param([string]$FilePath)
    
    $content = Get-Content $FilePath -Raw
    $modified = $false
    
    # Fix sender parameters to be nullable
    $patterns = @(
        @{ Pattern = 'private (async )?void (\w+)\(object sender,'; Replacement = 'private $1void $2(object? sender,' }
        @{ Pattern = 'private (async )?Task (\w+)\(object sender,'; Replacement = 'private $1Task $2(object? sender,' }
    )
    
    foreach ($pattern in $patterns) {
        if ($content -match $pattern.Pattern) {
            $content = $content -replace $pattern.Pattern, $pattern.Replacement
            $modified = $true
        }
    }
    
    if ($modified) {
        Set-Content $FilePath -Value $content -NoNewline
        Write-Host "  Fixed event handler signatures in $FilePath" -ForegroundColor Green
    }
    
    return $modified
}

# Fix specific files with known issues
$filesToFix = @(
    "clients\GGs.Desktop\Views\SystemIntelligenceView.xaml.cs",
    "clients\GGs.Desktop\Views\CommunityHubView.xaml.cs",
    "clients\GGs.Desktop\Views\ProfileArchitectView.xaml.cs",
    "clients\GGs.Desktop\Views\DashboardView.xaml.cs",
    "clients\GGs.Desktop\Views\ModernMainWindow.xaml.cs",
    "clients\GGs.Desktop\Views\Controls\SystemTweaksPanel.xaml.cs",
    "clients\GGs.Desktop\Views\ErrorLogViewer.xaml.cs",
    "clients\GGs.Desktop\Views\Controls\AnimatedProgressBar.xaml.cs"
)

$fixCount = 0

foreach ($file in $filesToFix) {
    $fullPath = Join-Path $projectRoot $file
    if (Test-Path $fullPath) {
        Write-Host "Processing: $file" -ForegroundColor Yellow
        
        if (Fix-PropertyChangedEvents $fullPath) { $fixCount++ }
        if (Fix-EventHandlerSignatures $fullPath) { $fixCount++ }
    }
    else {
        Write-Host "  File not found: $fullPath" -ForegroundColor Red
    }
}

Write-Host "`nFixed $fixCount issue(s)" -ForegroundColor Cyan
Write-Host "Now applying targeted fixes for specific errors..." -ForegroundColor Cyan
