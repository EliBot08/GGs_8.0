param(
  [Parameter(Mandatory=$true)][string]$PackagePath,
  [ValidateSet('stable','beta','dev')][string]$Channel = 'stable',
  [string]$CertPath,
  [switch]$Silent
)

$ErrorActionPreference = 'Stop'
function Info($m){ if(-not $Silent){ Write-Host $m } }

if (-not (Test-Path -LiteralPath $PackagePath)) { throw "Package not found: $PackagePath" }

# Optionally import certificate (if provided and not already trusted)
if ($CertPath) {
  if (-not (Test-Path -LiteralPath $CertPath)) { throw "Cert not found: $CertPath" }
  Info "Importing certificate to CurrentUser\\TrustedPeople ..."
  $cert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2 $CertPath
  $store = New-Object System.Security.Cryptography.X509Certificates.X509Store 'TrustedPeople','CurrentUser'
  $store.Open('ReadWrite')
  try {
    $store.Add($cert)
  } finally { $store.Close() }
}

# Install MSIX for current user
Info "Installing package: $PackagePath"
Add-AppxPackage -Path $PackagePath -ForceApplicationShutdown -ForceUpdateFromAnyVersion

# Seed update channel in HKCU for our SettingsManager to pick up
$seed = Join-Path $PSScriptRoot 'common/Seed-UpdateChannel.ps1'
& $seed -Channel $Channel

Info "Install complete. UpdateChannel set to '$Channel'."

