param([Parameter(Mandatory=$true)][string]$Directory)
$ErrorActionPreference = 'Stop'
if (-not (Test-Path -LiteralPath $Directory)) { Write-Output 'NO_DIR'; exit 1 }
$files = Get-ChildItem -LiteralPath $Directory -Recurse -Filter *.trx -ErrorAction SilentlyContinue
if (-not $files) { Write-Output 'NO_TRX'; exit 1 }
$total=0; $executed=0; $passed=0; $failed=0; $notExecuted=0; $warning=0
foreach ($f in $files) {
  try {
    [xml]$xml = Get-Content -LiteralPath $f.FullName
    $c = $xml.TestRun.ResultSummary.Counters
    $total += [int]$c.total
    $executed += [int]$c.executed
    $passed += [int]$c.passed
    $failed += [int]$c.failed
    $notExecuted += [int]$c.notExecuted
    $warning += [int]$c.warning
  } catch {}
}
Write-Output ("total=$total;executed=$executed;passed=$passed;failed=$failed;notExecuted=$notExecuted;warning=$warning")

