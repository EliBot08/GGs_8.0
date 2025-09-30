param(
  [Parameter(Mandatory=$true)][string]$SourceDir,
  [string]$InstallDir = "$env:LOCALAPPDATA\Programs\GGs.Desktop",
  [switch]$NoShortcut,
  [switch]$Autostart,
  [ValidateSet('stable','beta','dev')][string]$Channel = 'stable',
  [switch]$Silent
)

$ErrorActionPreference = 'Stop'

function Write-Info($msg) { if (-not $Silent) { Write-Host $msg } }

try {
  if (-not (Test-Path -LiteralPath $SourceDir)) { throw "SourceDir not found: $SourceDir" }
  New-Item -ItemType Directory -Force -Path $InstallDir | Out-Null
  Write-Info "Copying files to $InstallDir ..."
  robocopy "$SourceDir" "$InstallDir" /E /NFL /NDL /NJH /NJS /NP | Out-Null

  $exe = Join-Path $InstallDir 'GGs.Desktop.exe'
  if (-not (Test-Path -LiteralPath $exe)) {
    # Try fallback if single-file publish not used
    $exe = Get-ChildItem -LiteralPath $InstallDir -Filter 'GGs.Desktop.exe' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1 | ForEach-Object { $_.FullName }
    if (-not $exe) { throw "GGs.Desktop.exe not found in $InstallDir" }
  }

  # Create Start Menu shortcut (per-user)
  if (-not $NoShortcut) {
    $startMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs'
    $lnk = Join-Path $startMenu 'GGs Desktop.lnk'
    Write-Info "Creating shortcut $lnk"
    $WshShell = New-Object -ComObject WScript.Shell
    $shortcut = $WshShell.CreateShortcut($lnk)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $InstallDir
    $shortcut.IconLocation = $exe
    $shortcut.Save()
  }

  # Register uninstall information under HKCU
  $uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\GGs.Desktop'
  if (-not (Test-Path $uninstallKey)) { New-Item -Path $uninstallKey -Force | Out-Null }
  $uninstallScript = Join-Path $InstallDir 'Uninstall-GGsDesktop.ps1'

  # Write uninstall script content
@'
param([switch]$Silent)

$ErrorActionPreference = 'Stop'
try {
  function Write-Info(
    
    $msg) { if (-not $Silent) { Write-Host $msg } }

  Write-Info 'Removing autostart...'
  Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'GGsDesktop' -ErrorAction SilentlyContinue

  Write-Info 'Removing uninstall registry...'
  Remove-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\GGs.Desktop' -Recurse -ErrorAction SilentlyContinue

  Write-Info 'Removing Start Menu shortcut...'
  $lnk = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\GGs Desktop.lnk'
  if (Test-Path -LiteralPath $lnk) { Remove-Item -LiteralPath $lnk -Force -ErrorAction SilentlyContinue }

  Write-Info 'Removing installation folder...'
  $dir = Split-Path -Parent $MyInvocation.MyCommand.Definition
  # Try to kill running process gracefully
  Get-Process -Name 'GGs.Desktop' -ErrorAction SilentlyContinue | ForEach-Object { try { $_.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 300; $_.Kill() } catch {} }
  Start-Sleep -Milliseconds 200
  Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue

  Write-Info 'Uninstall complete.'
  exit 0
}
catch {
  if (-not $Silent) { Write-Host $_ -ForegroundColor Red }
  exit 1
}
'@ | Set-Content -LiteralPath $uninstallScript -Encoding UTF8

  # Registry values
  $ver = '1.0.0'
  New-ItemProperty -Path $uninstallKey -Name 'DisplayName' -Value 'GGs Desktop' -PropertyType String -Force | Out-Null
  New-ItemProperty -Path $uninstallKey -Name 'Publisher' -Value 'GGs' -PropertyType String -Force | Out-Null
  New-ItemProperty -Path $uninstallKey -Name 'DisplayVersion' -Value $ver -PropertyType String -Force | Out-Null
  New-ItemProperty -Path $uninstallKey -Name 'InstallLocation' -Value $InstallDir -PropertyType String -Force | Out-Null
  $uninstCmd = "powershell -NoProfile -ExecutionPolicy Bypass -File `"$uninstallScript`" -Silent"
  New-ItemProperty -Path $uninstallKey -Name 'UninstallString' -Value $uninstCmd -PropertyType String -Force | Out-Null

  # Autostart toggle
  if ($Autostart.IsPresent) {
    Write-Info 'Enabling autostart...'
    $val = '"' + $exe + '"'
    New-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'GGsDesktop' -Value $val -PropertyType String -Force | Out-Null
  }

  # Channel
  New-Item -Path 'HKCU:\Software\GGs\Desktop' -Force | Out-Null
  New-ItemProperty -Path 'HKCU:\Software\GGs\Desktop' -Name 'UpdateChannel' -Value $Channel -PropertyType String -Force | Out-Null

  Write-Info 'Install complete.'
  exit 0
}
catch {
  if (-not $Silent) { Write-Host $_ -ForegroundColor Red }
  exit 1
}

