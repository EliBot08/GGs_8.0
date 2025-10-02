Import-Module Pester -ErrorAction Stop

function Invoke-Launcher {
    param(
        [Parameter(Mandatory)][string]$Script,
        [string[]]$ArgumentList
    )

    $fullPath = (Resolve-Path -Path $Script).Path
    $joinedArgs = if ($ArgumentList) { ($ArgumentList -join ' ') } else { '' }
    $command = if ([string]::IsNullOrEmpty($joinedArgs)) {
        [string]::Format('/c ""{0}""', $fullPath)
    } else {
        [string]::Format('/c ""{0}"" {1}', $fullPath, $joinedArgs)
    }

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'cmd.exe'
    $psi.Arguments = $command
    $psi.CreateNoWindow = $true
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true

    $proc = [System.Diagnostics.Process]::Start($psi)
    $stdout = $proc.StandardOutput.ReadToEnd()
    $stderr = $proc.StandardError.ReadToEnd()
    $proc.WaitForExit()

    return [pscustomobject]@{
        ExitCode = $proc.ExitCode
        StdOut = $stdout
        StdErr = $stderr
    }
}

Describe 'GGs Launcher scripts' {
    $logDir = Join-Path (Get-Location) 'launcher-logs'

    BeforeAll {
        if (-not (Test-Path -LiteralPath $logDir)) {
            New-Item -ItemType Directory -Path $logDir -Force | Out-Null
        }
    }

    BeforeEach {
        Get-ChildItem -Path $logDir -Filter '*.log' -ErrorAction SilentlyContinue | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
    }

    It 'Launch-Viewer-New creates log and exits cleanly in test mode' {
        $result = Invoke-Launcher -Script 'Launch-Viewer-New.bat' -ArgumentList @('--test','--TestDurationSeconds','2')
        $result.ExitCode | Should -Be 0
        $logs = Get-ChildItem -Path $logDir -Filter 'Launch-Viewer-New-*.log'
        $logs.Count | Should -BeGreaterThan 0
        $logContent = Get-Content -Path $logs[0].FullName -Raw
        $logContent | Should -Match 'Viewer started successfully'
    }

    It 'Launch-Desktop-New creates log and exits cleanly in test mode' {
        $result = Invoke-Launcher -Script 'Launch-Desktop-New.bat' -ArgumentList @('--test','--TestDurationSeconds','2')
        $result.ExitCode | Should -Be 0
        $logs = Get-ChildItem -Path $logDir -Filter 'Launch-Desktop-New-*.log'
        $logs.Count | Should -BeGreaterThan 0
        (Get-Content -Path $logs[0].FullName -Raw) | Should -Match 'Desktop client running'
    }

    It 'Launch-Server-New performs port checks and exits cleanly in test mode' {
        $result = Invoke-Launcher -Script 'Launch-Server-New.bat' -ArgumentList @('--test','--TestDurationSeconds','2')
        $result.ExitCode | Should -Be 0
        $logs = Get-ChildItem -Path $logDir -Filter 'Launch-Server-New-*.log'
        $logs.Count | Should -BeGreaterThan 0
        $logText = Get-Content -Path $logs[0].FullName -Raw
        $logText | Should -Match 'Skipping server build in test mode'
    }

    It 'Launch-All-New orchestrates components and exits cleanly in test mode' {
        $result = Invoke-Launcher -Script 'Launch-All-New.bat' -ArgumentList @('--test','--TestDurationSeconds','2')
        $result.ExitCode | Should -Be 0
        $logs = Get-ChildItem -Path $logDir -Filter 'Launch-All-New-*.log'
        $logs.Count | Should -BeGreaterThan 0
        (Get-Content -Path $logs[0].FullName -Raw) | Should -Match 'Launcher suite completed successfully'
    }
}
