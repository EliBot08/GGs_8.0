param(
    [Parameter(Mandatory=$false)]
    [string]$BinaryPath = "",
    [Parameter(Mandatory=$false)]
    [string]$ServiceName = "GGsAgent",
    [Parameter(Mandatory=$false)]
    [string]$DisplayName = "GGs Agent",
    [Parameter(Mandatory=$false)]
    [string]$Description = "GGs Agent background service (runs as LocalSystem)",
    [Parameter(Mandatory=$false)]
    [switch]$StartNow
)

# Requires Administrator
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator."
    exit 1
}

if (-not $BinaryPath) {
    # Try to locate from artifacts if not provided
    $candidate = Join-Path -Path $PSScriptRoot -ChildPath "artifacts\publish.win-x64\GGs.Agent.exe"
    if (Test-Path $candidate) { $BinaryPath = $candidate }
}

if (-not (Test-Path $BinaryPath)) {
    Write-Error "BinaryPath not found. Provide -BinaryPath to GGs.Agent.exe."
    exit 1
}

# Create or update service
$binEsc = '"' + $BinaryPath + '"'
Write-Host "Installing service '$ServiceName' -> $binEsc"

# Stop existing if present
if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    try { Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue } catch {}
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 1
}

$create = sc.exe create $ServiceName binPath= $binEsc start= auto obj= LocalSystem DisplayName= "$DisplayName"
if ($LASTEXITCODE -ne 0) { Write-Error "Failed to create service: $create"; exit 1 }

# Description and delayed auto-start
sc.exe description $ServiceName "$Description" | Out-Null
sc.exe config $ServiceName start= delayed-auto | Out-Null

# Recovery options: restart service on failure (3 times)
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/5000/restart/5000 | Out-Null
sc.exe failureflag $ServiceName 1 | Out-Null

# Grant service the ability to write event logs implicitly via LocalSystem

if ($StartNow) {
    Start-Service -Name $ServiceName
    Write-Host "Service '$ServiceName' started."
} else {
    Write-Host "Service '$ServiceName' installed. Use 'Start-Service $ServiceName' to start."
}
