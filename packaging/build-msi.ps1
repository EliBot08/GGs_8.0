param(
  [string]$Project = "clients/GGs.Desktop/GGs.Desktop.csproj",
  [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
  [string]$Runtime = 'win-x64',
  [switch]$SelfContained,
  [string]$PublishDir,
  [string]$OutDir = "packaging/artifacts",
  [ValidateSet('stable','beta','dev')][string]$Channel = 'stable',
  [string[]]$FileAssociations = @('.ggs'),
  [switch]$Autostart,
  [switch]$NoShortcut,
  [string]$Version,
  [switch]$Silent
)

$ErrorActionPreference = 'Stop'
function Info($msg) { if (-not $Silent) { Write-Host $msg } }

# Resolve paths relative to repo root
$repoRoot = (Resolve-Path ".").Path
$projectPath = Join-Path $repoRoot $Project
$msiDir = Join-Path $repoRoot 'packaging/msi'
$genDir = Join-Path $msiDir '_generated'
$artifacts = Join-Path $repoRoot $OutDir

New-Item -ItemType Directory -Force -Path $genDir | Out-Null
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

# 1) Publish app if PublishDir not supplied
if (-not $PublishDir) {
  $PublishDir = Join-Path $artifacts 'publish.win-x64'
  $pubArgs = @('publish', $projectPath, '-c', $Configuration, '-r', $Runtime, '-o', $PublishDir)
  if ($SelfContained.IsPresent) { $pubArgs += '-p:SelfContained=true' } else { $pubArgs += '-p:SelfContained=false' }
  Info "Publishing $Project -> $PublishDir ..."
  dotnet @pubArgs | Out-Null
}
$PublishDir = (Resolve-Path $PublishDir).Path

# 2) Ensure WiX v4 CLI is available via local dotnet tool
$rootManifest = Join-Path $repoRoot 'dotnet-tools.json'
$configManifest = Join-Path $repoRoot '.config\dotnet-tools.json'

$manifestDir = $null
if (Test-Path $rootManifest) {
  $manifestDir = $repoRoot
} elseif (Test-Path $configManifest) {
  $manifestDir = Join-Path $repoRoot '.config'
} else {
  $manifestDir = $repoRoot
}

$manifestPath = Join-Path $manifestDir 'dotnet-tools.json'
if (Test-Path $manifestPath) {
  try { Unblock-File -LiteralPath $manifestPath -ErrorAction SilentlyContinue } catch { }
}

Push-Location $manifestDir
try {
  if (-not (Test-Path $manifestPath)) {
    Info "Creating local dotnet tool manifest in $manifestDir..."
    dotnet new tool-manifest --force | Out-Null
  }
  Info 'Ensuring WiX tool is installed (local manifest)...'
  # Pin a stable WiX 4 version
  $wixVersion = '4.0.2'
  try { dotnet tool update wix --version $wixVersion --tool-manifest $manifestPath | Out-Null } catch { dotnet tool install wix --version $wixVersion --tool-manifest $manifestPath | Out-Null }
  dotnet tool restore --tool-manifest $manifestPath | Out-Null
}
finally { Pop-Location }
# 3) Harvest published files into WiX authoring
$harvestWxs = Join-Path $genDir 'Harvested.wxs'
Info 'Harvesting published output...'
# Use WiX local tool via dotnet; generate stable-ish component guids; map to INSTALLFOLDER and var.PublishDir
Push-Location $manifestDir
try {
  dotnet tool run wix heat dir "$PublishDir" -o "$harvestWxs" -cg AppFiles -dr INSTALLFOLDER -var var.PublishDir -scom -sreg -sfrag -srd -gg | Out-Null
}
finally { Pop-Location }

# 4) Generate FileAssociations.wxs (registry-based, per-user under HKCU) from requested extensions
function New-AssocXml([string[]]$exts) {
  $sb = New-Object System.Text.StringBuilder
  [void]$sb.AppendLine('<?xml version="1.0" encoding="UTF-8"?>')
  [void]$sb.AppendLine('<Wix xmlns="http://wixtoolset.org/schemas/v4/wxs">')
  [void]$sb.AppendLine('  <Fragment>')
  [void]$sb.AppendLine('    <ComponentGroup Id="FileAssociations">')
  foreach ($e in $exts) {
    $ext = $e.Trim()
    if (-not $ext.StartsWith('.')) { $ext = '.' + $ext }
    $safe = $ext.TrimStart('.')
    $progId = "GGs.Desktop.$safe"
    [void]$sb.AppendLine([string]::Format('      <Component Id="Assoc_{0}" Directory="INSTALLFOLDER" Guid="*">', $safe))
    [void]$sb.AppendLine([string]::Format('        <RegistryValue Root="HKCU" Key="Software\Classes\{0}" Value="{1}" Type="string" KeyPath="yes" />', $ext, $progId))
    [void]$sb.AppendLine([string]::Format('        <RegistryValue Root="HKCU" Key="Software\Classes\{0}\OpenWithProgids" Name="{1}" Value="" Type="string" />', $ext, $progId))
    [void]$sb.AppendLine([string]::Format('        <RegistryValue Root="HKCU" Key="Software\Classes\{0}" Value="GGs {1} File" Type="string" />', $progId, $safe.ToUpper()))
    [void]$sb.AppendLine([string]::Format('        <RegistryValue Root="HKCU" Key="Software\Classes\{0}\DefaultIcon" Value="[INSTALLFOLDER]GGs.Desktop.exe,0" Type="string" />', $progId))
    [void]$sb.AppendLine([string]::Format('        <RegistryValue Root="HKCU" Key="Software\Classes\{0}\shell\open\command" Value="&quot;[INSTALLFOLDER]GGs.Desktop.exe&quot; &quot;%1&quot;" Type="string" />', $progId))
    [void]$sb.AppendLine('      </Component>')
  }
  # RegisteredApplications (helps Default Apps picker)
  [void]$sb.AppendLine('      <Component Id="Assoc_RegisteredApp" Directory="INSTALLFOLDER" Guid="*">')
  [void]$sb.AppendLine('        <RegistryValue Root="HKCU" Key="Software\RegisteredApplications" Name="GGs Desktop" Value="Software\GGs\Capabilities" Type="string" KeyPath="yes" />')
  [void]$sb.AppendLine('        <RegistryValue Root="HKCU" Key="Software\GGs\Capabilities" Name="ApplicationName" Value="GGs Desktop" Type="string" />')
  foreach ($e in $exts) {
    $ext = $e.Trim()
    if (-not $ext.StartsWith('.')) { $ext = '.' + $ext }
    $safe = $ext.TrimStart('.')
    $progId = "GGs.Desktop.$safe"
    [void]$sb.AppendLine([string]::Format('        <RegistryValue Root="HKCU" Key="Software\GGs\Capabilities\FileAssociations" Name="{0}" Value="{1}" Type="string" />', $ext, $progId))
  }
  [void]$sb.AppendLine('      </Component>')
  [void]$sb.AppendLine('    </ComponentGroup>')
  [void]$sb.AppendLine('  </Fragment>')
  [void]$sb.AppendLine('</Wix>')
  return $sb.ToString()
}

$fileAssocWxs = Join-Path $genDir 'FileAssociations.wxs'
(New-AssocXml -exts $FileAssociations) | Set-Content -LiteralPath $fileAssocWxs -Encoding UTF8

# 5) Build MSI with WiX; pass PublishDir to bind harvested authoring
$msiName = "GGs.Desktop"
if ($Version) { $msiName += "-$Version" }
$msiPath = Join-Path $artifacts ("$msiName.msi")

Info "Building MSI -> $msiPath ..."
Push-Location $manifestDir
try {
  dotnet tool run wix build (Join-Path $msiDir 'Product.wxs') $harvestWxs $fileAssocWxs -d PublishDir=$PublishDir -arch x64 -o "$msiPath" | Out-Null
}
finally { Pop-Location }

Info "Built: $msiPath"
Write-Output $msiPath

