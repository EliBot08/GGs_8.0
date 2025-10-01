# Fix missing using statements in E2ETests
$testDir = "tests\GGs.E2ETests"

$files = Get-ChildItem -Path $testDir -Filter "*.cs" -File

foreach ($file in $files) {
    $content = Get-Content $file.FullName -Raw
    $modified = $false
    
    # Check if file uses UseEnvironment and doesn't have Microsoft.Extensions.Hosting
    if ($content -match "UseEnvironment" -and $content -notmatch "using Microsoft\.Extensions\.Hosting") {
        $content = $content -replace "(using [^\r\n]+\r?\n)+", "`$0using Microsoft.Extensions.Hosting;`r`n"
        $modified = $true
    }
    
    # Check if file uses AddInMemoryCollection and doesn't have Microsoft.Extensions.Configuration
    if ($content -match "AddInMemoryCollection" -and $content -notmatch "using Microsoft\.Extensions\.Configuration") {
        $content = $content -replace "(using [^\r\n]+\r?\n)+", "`$0using Microsoft.Extensions.Configuration;`r`n"
        $modified = $true
    }
    
    # Check if file uses HttpMethod or HttpRequestMessage and doesn't have System.Net.Http
    if (($content -match "HttpMethod\." -or $content -match "new HttpRequestMessage") -and $content -notmatch "using System\.Net\.Http;") {
        # Add after the first using statement
        $content = $content -replace "^(using [^\r\n]+)", "`$1`r`nusing System.Net.Http;"
        $modified = $true
    }
    
    if ($modified) {
        Set-Content -Path $file.FullName -Value $content -NoNewline
        Write-Host "Fixed: $($file.Name)"
    }
}

Write-Host "Done fixing using statements"
