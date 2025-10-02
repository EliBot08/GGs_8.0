# FINAL FIX - Bulletproof approach
$ErrorActionPreference = "Stop"

Write-Host "`nApplying fixes to ErrorLogViewer..." -ForegroundColor Cyan

$xamlPath = "tools\GGs.ErrorLogViewer\Views\MainWindow.xaml"
$lines = Get-Content $xamlPath

# Track changes
$changed = $false

# Process line by line
for ($i = 0; $i -lt $lines.Count; $i++) {
    # Fix command bindings on nav buttons
    if ($lines[$i] -match 'x:Name="NavCompare"' -and $lines[$i+2] -notmatch 'Command=') {
        $lines[$i+2] = $lines[$i+2] -replace '/>$', ''
        $lines = $lines[0..$($i+2)] + '                                 Command="{Binding SwitchToCompareViewCommand}"/>' + $lines[($i+3)..($lines.Count-1)]
        $changed = $true
        Write-Host "  Fixed NavCompare command binding" -ForegroundColor Green
    }
    
    if ($lines[$i] -match 'x:Name="NavExport"' -and $lines[$i+2] -notmatch 'Command=') {
        $lines[$i+2] = $lines[$i+2] -replace '/>$', ''
        $lines = $lines[0..$($i+2)] + '                                 Command="{Binding SwitchToExportViewCommand}"/>' + $lines[($i+3)..($lines.Count-1)]
        $changed = $true
        Write-Host "  Fixed NavExport command binding" -ForegroundColor Green
    }
    
    if ($lines[$i] -match 'x:Name="NavSettings"' -and $lines[$i+2] -notmatch 'Command=') {
        $lines[$i+2] = $lines[$i+2] -replace '/>$', ''
        $lines = $lines[0..$($i+2)] + '                                 Command="{Binding SwitchToSettingsViewCommand}"/>' + $lines[($i+3)..($lines.Count-1)]
        $changed = $true
        Write-Host "  Fixed NavSettings command binding" -ForegroundColor Green
    }
}

if ($changed) {
    Set-Content $xamlPath -Value $lines
    Write-Host "`nXAML updated successfully" -ForegroundColor Green
}

# Build to test
Write-Host "`nBuilding..." -ForegroundColor Yellow
Push-Location "tools\GGs.ErrorLogViewer"
$output = dotnet build -c Release --nologo 2>&1
$success = $LASTEXITCODE -eq 0
Pop-Location

if ($success) {
    Write-Host "✓ BUILD SUCCESS" -ForegroundColor Green
    Write-Host "`nYou can now launch:" -ForegroundColor Cyan
    Write-Host "  Start.ErrorLogViewer.bat" -ForegroundColor White
} else {
    Write-Host "✗ BUILD FAILED" -ForegroundColor Red
    Write-Host $output
}
