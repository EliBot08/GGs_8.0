param([Parameter(Mandatory=$true)][string]$Path)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Path)) { Write-Output 'NO_TRX'; exit 1 }
[xml]$xml = Get-Content -LiteralPath $Path
$c = $xml.TestRun.ResultSummary.Counters
Write-Output ("total=$($c.total);executed=$($c.executed);passed=$($c.passed);failed=$($c.failed);notExecuted=$($c.notExecuted);warning=$($c.warning)")

