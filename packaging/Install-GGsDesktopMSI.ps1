param(
  [Parameter(Mandatory=$true)][string]$MsiPath,
  [ValidateSet('stable','beta','dev')][string]$Channel = 'stable',
  [switch]$Autostart,
  [switch]$NoShortcut,
  [switch]$Silent
)

$ErrorActionPreference = 'Stop'
function Info($msg) { if (-not $Silent) { Write-Host $msg } }

if (-not (Test-Path -LiteralPath $MsiPath)) { throw "MSI not found: $MsiPath" }

# Per-user install flags; pass public properties for options
$props = @("CHANNEL=$Channel")
if ($Autostart.IsPresent) { $props += 'AUTOSTART=1' }
if ($NoShortcut.IsPresent) { $props += 'NOSHORTCUT=1' }

$propStr = ($props -join ' ')

# Silent/passive UI per user
$ui = if ($Silent) { '/qn' } else { '/qb!' }

# MSIINSTALLPERUSER forces per-user context
$cmd = "msiexec.exe /i `"$MsiPath`" MSIINSTALLPERUSER=1 $propStr $ui"
Info "Installing MSI per-user: $cmd"
$proc = Start-Process -FilePath msiexec.exe -ArgumentList "/i","$MsiPath","MSIINSTALLPERUSER=1",$props,$ui -PassThru -Wait
if ($proc.ExitCode -ne 0) { throw "msiexec exited with code $($proc.ExitCode)" }

Info 'Install complete.'

