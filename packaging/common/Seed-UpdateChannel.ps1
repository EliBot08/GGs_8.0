param([ValidateSet('stable','beta','dev')][string]$Channel)
$ErrorActionPreference = 'Stop'
if (-not $Channel) { throw 'Channel is required' }
New-Item -Path 'HKCU:\Software\GGs\Desktop' -Force | Out-Null
New-ItemProperty -Path 'HKCU:\Software\GGs\Desktop' -Name 'UpdateChannel' -Value $Channel -PropertyType String -Force | Out-Null

