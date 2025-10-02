Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function New-LauncherContext {
    param(
        [Parameter(Mandatory)][string]$ComponentName,
        [string]$BaseDirectory = (Get-Location),
        [string]$LogDirectory = (Join-Path (Get-Location) 'launcher-logs'),
        [switch]$TestMode
    )

    if (-not (Test-Path -LiteralPath $LogDirectory)) {
        New-Item -ItemType Directory -Path $LogDirectory -Force | Out-Null
    }

    $timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    $sanitizedName = $ComponentName -replace '[^A-Za-z0-9_-]', '-'
    $logFileName = "${sanitizedName}-${timestamp}.log"
    $logPath = Join-Path $LogDirectory $logFileName

    $context = [pscustomobject]@{
        ComponentName = $ComponentName
        BaseDirectory = (Resolve-Path -Path $BaseDirectory).Path
        LogDirectory = (Resolve-Path -Path $LogDirectory).Path
        LogFile = $logPath
        StartedAt = Get-Date
        TestMode = [bool]$TestMode
        TransientData = @{}
    }

    return $context
}

function Write-LauncherLog {
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [Parameter(Mandatory)][string]$Message,
        [ValidateSet('INFO','WARN','ERROR','SUCCESS','DEBUG')][string]$Level = 'INFO',
        [switch]$NoConsole
    )

    $timestamp = Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'
    $line = "[$timestamp] [$Level] $Message"
    Add-Content -LiteralPath $Context.LogFile -Value $line
    if (-not $NoConsole) {
        Write-Host $line
    }
}

function Invoke-WithLogging {
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [Parameter(Mandatory)][scriptblock]$Action,
        [string]$Description = 'operation'
    )

    Write-LauncherLog -Context $Context -Message "Starting $Description"
    try {
        $result = & $Action
        Write-LauncherLog -Context $Context -Message "Completed $Description" -Level 'SUCCESS'
        return $result
    }
    catch {
        Write-LauncherLog -Context $Context -Message "Failed $Description: $($_.Exception.Message)" -Level 'ERROR'
        throw
    }
}

function Resolve-ExecutablePath {
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [Parameter(Mandatory)][string[]]$CandidatePaths,
        [string]$FriendlyName = 'application'
    )

    foreach ($candidate in $CandidatePaths) {
        $fullPath = if ([System.IO.Path]::IsPathRooted($candidate)) { $candidate } else { Join-Path $Context.BaseDirectory $candidate }
        if (Test-Path -LiteralPath $fullPath) {
            Write-LauncherLog -Context $Context -Message "Resolved $FriendlyName executable at '$fullPath'" -Level 'DEBUG'
            return (Resolve-Path -Path $fullPath).Path
        }
    }

    throw "Unable to locate $FriendlyName. Checked paths: $(($CandidatePaths | ForEach-Object { "'$_'" }) -join ', ')"
}

function Stop-ProcessTree {
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [Parameter(Mandatory)][string[]]$ProcessNames
    )

    $killed = 0
    foreach ($name in $ProcessNames | Sort-Object -Unique) {
        $procs = Get-Process -Name $name -ErrorAction SilentlyContinue
        foreach ($proc in $procs) {
            try {
                Write-LauncherLog -Context $Context -Message "Stopping existing process '$($proc.ProcessName)' (PID $($proc.Id))" -Level 'WARN'
                $proc.CloseMainWindow() | Out-Null
                Start-Sleep -Milliseconds 500
                if (-not $proc.HasExited) {
                    $proc.Kill()
                    $proc.WaitForExit(5000) | Out-Null
                }
                $killed++
            }
            catch {
                Write-LauncherLog -Context $Context -Message "Unable to terminate process '$($proc.ProcessName)' (PID $($proc.Id)): $($_.Exception.Message)" -Level 'ERROR'
            }
        }
    }

    if ($killed -gt 0) {
        Write-LauncherLog -Context $Context -Message "Terminated $killed conflicting process(es)." -Level 'WARN'
    }
}

function Invoke-ProjectBuild {
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [Parameter(Mandatory)][string]$ProjectPath,
        [Parameter(Mandatory)][string]$OutputDirectory,
        [string]$FriendlyName = 'project',
        [string]$TargetFramework,
        [switch]$Force
    )

    $fullProjectPath = if ([System.IO.Path]::IsPathRooted($ProjectPath)) { $ProjectPath } else { Join-Path $Context.BaseDirectory $ProjectPath }
    if (-not (Test-Path -LiteralPath $fullProjectPath)) {
        throw "Project file not found: $fullProjectPath"
    }

    $fullOutputDir = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) { $OutputDirectory } else { Join-Path $Context.BaseDirectory $OutputDirectory }
    if (-not (Test-Path -LiteralPath $fullOutputDir)) {
        New-Item -ItemType Directory -Path $fullOutputDir -Force | Out-Null
    }

    $needsBuild = $Force
    if (-not $needsBuild) {
        $projectStamp = (Get-Item -LiteralPath $fullProjectPath).LastWriteTimeUtc
        $existingExe = Get-ChildItem -Path $fullOutputDir -Filter '*.exe' -File -ErrorAction SilentlyContinue | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
        if ($null -eq $existingExe) {
            $needsBuild = $true
        }
        elseif ($existingExe.LastWriteTimeUtc -lt $projectStamp) {
            $needsBuild = $true
        }
    }

    if (-not $needsBuild) {
        Write-LauncherLog -Context $Context -Message "Skipping build for $FriendlyName; existing artifacts are up-to-date." -Level 'DEBUG'
        return $fullOutputDir
    }

    $arguments = @('publish', '"' + $fullProjectPath + '"', '-c', 'Release', '-o', '"' + $fullOutputDir + '"', '--nologo', '--verbosity', 'minimal')
    if ($TargetFramework) {
        $arguments += @('-f', $TargetFramework)
    }

    Write-LauncherLog -Context $Context -Message "Building $FriendlyName with 'dotnet $($arguments -join ' ')'." -Level 'INFO'
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'dotnet'
    $psi.Arguments = $arguments -join ' '
    $psi.WorkingDirectory = $Context.BaseDirectory
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    [void]$process.Start()
    $stdOut = $process.StandardOutput.ReadToEnd()
    $stdErr = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    if ($stdOut) { Write-LauncherLog -Context $Context -Message $stdOut.Trim() -Level 'DEBUG' }
    if ($process.ExitCode -ne 0) {
        if ($stdErr) { Write-LauncherLog -Context $Context -Message $stdErr.Trim() -Level 'ERROR' }
        throw "dotnet publish failed with exit code $($process.ExitCode)"
    }

    return $fullOutputDir
}

function Start-ManagedProcess {
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [Parameter(Mandatory)][string]$FilePath,
        [string[]]$Arguments = @(),
        [switch]$NoWindow
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $FilePath
    if ($Arguments.Count -gt 0) {
        $psi.Arguments = ($Arguments | ForEach-Object { $_.ToString() }) -join ' '
    }
    $psi.WorkingDirectory = Split-Path -Path $FilePath -Parent
    $psi.UseShellExecute = $false
    $psi.RedirectStandardOutput = $false
    $psi.RedirectStandardError = $false
    $psi.CreateNoWindow = [bool]$NoWindow

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi

    if (-not $process.Start()) {
        throw "Unable to start '$FilePath'"
    }

    Write-LauncherLog -Context $Context -Message "Started process '$FilePath' (PID $($process.Id))." -Level 'SUCCESS'
    return $process
}

function Wait-ForProcessStart {
    param(
        [Parameter(Mandatory)][System.Diagnostics.Process]$Process,
        [int]$TimeoutSeconds = 15
    )

    $stopWatch = [System.Diagnostics.Stopwatch]::StartNew()
    while ($stopWatch.Elapsed.TotalSeconds -lt $TimeoutSeconds) {
        if (-not $Process.HasExited) {
            return $true
        }
        Start-Sleep -Milliseconds 200
    }
    return -not $Process.HasExited
}

function Monitor-Process {
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [Parameter(Mandatory)][System.Diagnostics.Process]$Process,
        [string]$StatusLabel = 'Runtime',
        [switch]$Silent
    )

    $start = Get-Date
    while (-not $Process.HasExited) {
        $elapsed = (Get-Date) - $start
        $status = "${StatusLabel}: {0:hh\:mm\:ss}" -f $elapsed
        if (-not $Silent) {
            Write-Progress -Activity $Context.ComponentName -Status $status -PercentComplete -1
        }
        Start-Sleep -Seconds 1
    }

    if (-not $Silent) {
        Write-Progress -Activity $Context.ComponentName -Completed -Status "$StatusLabel complete"
    }

    return $Process.ExitCode
}

function Ensure-DotNetSdk {
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [Version]$MinimumVersion = [Version]'9.0'
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'dotnet'
    $psi.Arguments = '--list-sdks'
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    [void]$process.Start()
    $output = $process.StandardOutput.ReadToEnd()
    $err = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    if ($process.ExitCode -ne 0) {
        Write-LauncherLog -Context $Context -Message $err.Trim() -Level 'ERROR'
        throw 'Unable to determine available .NET SDK versions.'
    }

    $versions = @()
    foreach ($line in $output -split [Environment]::NewLine) {
        if ($line -match '^(?<version>\d+\.\d+(?:\.\d+)?) ') {
            $versions += [Version]$Matches.version
        }
    }

    if ($versions.Count -eq 0) {
        throw 'No .NET SDK installations detected. Install .NET SDK 9.0 or later.'
    }

    $max = ($versions | Sort-Object)[-1]
    if ($max -lt $MinimumVersion) {
        throw "Found .NET SDK version $max but need $MinimumVersion or later."
    }

    Write-LauncherLog -Context $Context -Message "Detected .NET SDK versions: $(($versions | Sort-Object -Unique) -join ', ')." -Level 'DEBUG'
    return $true
}

function Get-ProcessByPort {
    param(
        [Parameter(Mandatory)][int]$Port
    )

    $connections = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue
    if ($null -eq $connections) {
        return @()
    }

    $pids = $connections | Select-Object -ExpandProperty OwningProcess -Unique
    return $pids | ForEach-Object { Get-Process -Id $_ -ErrorAction SilentlyContinue } | Where-Object { $_ -ne $null }
}

function Ensure-PortAvailable {
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [int]$Port = 5000,
        [switch]$Force
    )

    $procs = Get-ProcessByPort -Port $Port
    if ($procs.Count -eq 0) {
        Write-LauncherLog -Context $Context -Message "Port $Port is available." -Level 'DEBUG'
        return
    }

    if (-not $Force) {
        Write-LauncherLog -Context $Context -Message "Port $Port is in use by: $(($procs | ForEach-Object { "$($_.ProcessName) (PID $($_.Id))" }) -join ', ')." -Level 'WARN'
        throw "Port $Port is currently in use. Run again with Force switch to terminate conflicting processes."
    }

    foreach ($proc in $procs) {
        try {
            Write-LauncherLog -Context $Context -Message "Stopping process '$($proc.ProcessName)' (PID $($proc.Id)) holding port $Port." -Level 'WARN'
            $proc.Kill()
            $proc.WaitForExit(5000) | Out-Null
        }
        catch {
            Write-LauncherLog -Context $Context -Message "Failed to stop process on port $Port: $($_.Exception.Message)" -Level 'ERROR'
            throw
        }
    }
}

function New-TestProcess {
    param(
        [Parameter(Mandatory)][psobject]$Context,
        [int]$DurationSeconds = 5,
        [string]$DisplayName = 'Simulated Process'
    )

    $script = "Start-Sleep -Seconds $DurationSeconds"
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = 'powershell'
    $psi.Arguments = '-NoProfile -Command "' + $script + '"'
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    [void]$process.Start()
    Write-LauncherLog -Context $Context -Message "Started test stub for $DisplayName (PID $($process.Id))" -Level 'DEBUG'
    return $process
}

function Format-StatusLine {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][string]$State,
        [TimeSpan]$Uptime,
        [string]$Details
    )

    $uptimeText = if ($null -ne $Uptime) { '{0:hh\:mm\:ss}' -f $Uptime } else { '--:--:--' }
    return "{0,-18} {1,-10} {2,-10} {3}" -f $Name, $State, $uptimeText, $Details
}

Export-ModuleMember -Function *
