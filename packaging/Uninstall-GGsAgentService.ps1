param(
    [Parameter(Mandatory=$false)]
    [string]$ServiceName = "GGsAgent"
)

# Requires Administrator
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator."
    exit 1
}

if (Get-Service -Name $ServiceName -ErrorAction SilentlyContinue) {
    try { Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue } catch {}
    sc.exe delete $ServiceName | Out-Null
    Write-Host "Service '$ServiceName' removed."
} else {
    Write-Host "Service '$ServiceName' not found."
}
