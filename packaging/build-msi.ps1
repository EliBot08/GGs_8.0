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
  dotnet @pubArgs
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
  dotnet wix msi decompile "$PublishDir" -o "$harvestWxs" 2>&1 | Out-Null
  # WiX v4 removed heat command, using manual file generation instead
  if (-not (Test-Path $harvestWxs)) {
    # Fallback: create simple component group manually
    Info "Generating component manifest manually (WiX v4 no heat support)..."
    $files = Get-ChildItem -Recurse -File "$PublishDir"
    $wxsContent = @"
<?xml version='1.0' encoding='UTF-8'?>
<Wix xmlns='http://wixtoolset.org/schemas/v4/wxs'>
  <Fragment>
    <ComponentGroup Id='AppFiles' Directory='INSTALLFOLDER'>
"@
    $index = 0
    foreach ($file in $files) {
      $relPath = $file.FullName.Substring($PublishDir.Length + 1)
      $safeRelPath = $relPath -replace '[^A-Za-z0-9_.]','_'
      $compId = "cmp_" + $safeRelPath.Replace('.','_') + "_" + $index
      $fileId = "fil_" + $safeRelPath.Replace('.','_') + "_" + $index
      $guid = (New-Guid).ToString('D').ToUpper()
      $index++
      $wxsContent += "`n      <Component Id='$compId' Guid='$guid'>"
      $wxsContent += "`n        <File Id='$fileId' Source='`$(var.PublishDir)\$relPath' />"
      $wxsContent += "`n      </Component>"
    }
    $wxsContent += @"

    </ComponentGroup>
  </Fragment>
</Wix>
"@
    Set-Content -Path $harvestWxs -Value $wxsContent -Encoding UTF8
  }
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
    $safe = $ext.TrimStart('.') -replace '[^A-Za-z0-9_]','_'
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
    $safe = $ext.TrimStart('.') -replace '[^A-Za-z0-9_]','_'
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
  $wixOutput = dotnet wix build (Join-Path $msiDir 'Product.wxs') $harvestWxs $fileAssocWxs -d PublishDir=$PublishDir -arch x64 -o "$msiPath" 2>&1
  $wixOutput | Where-Object { $_ -notmatch 'warning WIX107' } | Write-Host
  # Check if MSI was actually created (WiX v4 returns non-zero for warnings)
  if (-not (Test-Path $msiPath)) { 
    throw "MSI build failed - output file not created at $msiPath" 
  }
}
finally { Pop-Location }

Info "Built: $msiPath"
Write-Output $msiPath

