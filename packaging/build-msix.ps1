param(
  [ValidateSet('Debug','Release')][string]$Configuration = 'Release',
  [string]$Platform = 'x64',
  [switch]$UseMsBuild,
  [switch]$SelfContained,
  [switch]$Silent
)

$ErrorActionPreference = 'Stop'
function Info($m){ if(-not $Silent){ Write-Host $m } }

$repo = (Resolve-Path ".").Path
$wapproj = Join-Path $repo 'packaging/msix/GGs.Desktop.Package/GGs.Desktop.Package.wapproj'
$artifacts = Join-Path $repo 'packaging/artifacts/msix'
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null

if (!(Test-Path $wapproj)) { throw "WAP project not found: $wapproj" }

# Preferred: build via MSBuild if available (VS installed)
$msbuildCmd = Get-Command msbuild.exe -ErrorAction SilentlyContinue
$msbuild = if ($msbuildCmd) { $msbuildCmd.Source } else { $null }
if ($UseMsBuild -or $msbuild) {
  Info "Building MSIX via MSBuild..."
  & $msbuild $wapproj /t:Restore,Build /p:Configuration=$Configuration /p:Platform=$Platform /p:UapAppxPackageBuildMode=SideloadOnly /p:AppxBundle=Never /p:AppxPackageDir=$artifacts\ | Out-Null
  $pkg = Get-ChildItem -LiteralPath $artifacts -Filter '*.msix' -Recurse | Sort-Object LastWriteTime -Descending | Select-Object -First 1
  if (-not $pkg) { throw 'MSIX build produced no .msix. Ensure certificate/signing settings are valid or set AppxPackageSigningEnabled=false.' }
  Write-Output $pkg.FullName
  exit 0
}

# Fallback: use MakeAppx with the published output and manifest
Info 'Falling back to MakeAppx-based packaging...'
# Locate MakeAppx.exe from Windows 10/11 SDK
$kits = "${env:ProgramFiles(x86)}\Windows Kits\10\bin","$env:ProgramFiles\Windows Kits\10\bin"
$makeAppx = $null
foreach($root in $kits){ if(Test-Path $root){ $cand = Get-ChildItem -Recurse -LiteralPath $root -Filter MakeAppx.exe -ErrorAction SilentlyContinue | Where-Object { $_.FullName -like '*x64*' } | Select-Object -First 1; if($cand){ $makeAppx = $cand.FullName; break } } }
if (-not $makeAppx) { throw 'MakeAppx.exe not found. Install Windows 10 SDK or build with MSBuild/Visual Studio.' }

# Publish desktop app
$appProj = Join-Path $repo 'clients/GGs.Desktop/GGs.Desktop.csproj'
$pubDir = Join-Path $artifacts 'publish.win-x64'
$pubArgs = @('publish', $appProj, '-c', $Configuration, '-r', 'win-x64', '-o', $pubDir)
if ($SelfContained.IsPresent) { $pubArgs += '-p:SelfContained=true' } else { $pubArgs += '-p:SelfContained=false' }
Info "Publishing desktop app to $pubDir ..."
dotnet @pubArgs | Out-Null

# Copy manifest and assets next to published exe
$manifestSrc = Join-Path $repo 'packaging/msix/GGs.Desktop.Package/Package.appxmanifest'
Copy-Item -LiteralPath $manifestSrc -Destination (Join-Path $pubDir 'AppxManifest.xml') -Force
Copy-Item -LiteralPath (Join-Path $repo 'packaging/msix/GGs.Desktop.Package/Assets') -Destination (Join-Path $pubDir 'Assets') -Recurse -Force

# Create mapping list file
$mapFile = Join-Path $artifacts 'mapping.txt'
@("[Files]","$pubDir\AppxManifest.xml","$pubDir\Assets\Square150x150Logo.png","$pubDir\Assets\Square44x44Logo.png","$pubDir\Assets\StoreLogo.png") + (Get-ChildItem -LiteralPath $pubDir -File | ForEach-Object { $_.FullName }) | Set-Content -LiteralPath $mapFile -Encoding ASCII

# Make the .msix
$msixPath = Join-Path $artifacts 'GGs.Desktop.msix'
Info "Packing MSIX -> $msixPath ..."
& $makeAppx pack /o /m (Join-Path $pubDir 'AppxManifest.xml') /f $mapFile /p $msixPath | Out-Null
Write-Output $msixPath

