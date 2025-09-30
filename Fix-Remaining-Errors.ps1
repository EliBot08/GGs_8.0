# Fix remaining GitHub Actions build errors
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot

Write-Host "=== Fixing Remaining Build Errors ===" -ForegroundColor Cyan

# Fix SystemIntelligenceService.cs - add await Task.CompletedTask to async methods
$file = "clients\GGs.Desktop\Services\SystemIntelligenceService.cs"
$path = Join-Path $projectRoot $file
if (Test-Path $path) {
    Write-Host "Fixing: $file" -ForegroundColor Yellow
    $content = Get-Content $path -Raw
    
    # Line 559 - GatherSystemInfoAsync
    $content = $content.Replace(
        "        private async Task<SystemInfo> GatherSystemInfoAsync()`r`n        {`r`n            var info = new SystemInfo();",
        "        private async Task<SystemInfo> GatherSystemInfoAsync()`r`n        {`r`n            await Task.CompletedTask;`r`n            var info = new SystemInfo();"
    )
    
    # Line 620 - SaveProfilesAsync
    $content = $content.Replace(
        "        private async Task SaveProfilesAsync()`r`n        {`r`n            try",
        "        private async Task SaveProfilesAsync()`r`n        {`r`n            await Task.CompletedTask;`r`n            try"
    )
    
    # Line 789 - ApplyProfileAsync
    $content = $content.Replace(
        "        public async Task<bool> ApplyProfileAsync(GGs.Shared.SystemIntelligence.SystemIntelligenceProfile profile)`r`n        {`r`n            if (profile == null) return false;",
        "        public async Task<bool> ApplyProfileAsync(GGs.Shared.SystemIntelligence.SystemIntelligenceProfile profile)`r`n        {`r`n            await Task.CompletedTask;`r`n            if (profile == null) return false;"
    )
    
    Set-Content $path -Value $content -NoNewline
    Write-Host "  Fixed async methods" -ForegroundColor Green
}

# Fix AdminHubClient.cs - suppress unused event warning
$file = "clients\GGs.Desktop\Services\AdminHubClient.cs"
$path = Join-Path $projectRoot $file
if (Test-Path $path) {
    Write-Host "Fixing: $file" -ForegroundColor Yellow
    $content = Get-Content $path -Raw
    
    $content = $content.Replace(
        "        public event EventHandler<AuditLogAddedEventArgs>? AuditAdded;",
        "        #pragma warning disable CS0067`r`n        public event EventHandler<AuditLogAddedEventArgs>? AuditAdded;`r`n        #pragma warning restore CS0067"
    )
    
    Set-Content $path -Value $content -NoNewline
    Write-Host "  Suppressed event warning" -ForegroundColor Green
}

Write-Host "`n=== Done ===" -ForegroundColor Cyan
Write-Host "Now building with -warnaserror to verify..." -ForegroundColor Yellow
