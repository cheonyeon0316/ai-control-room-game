param([int]$TimeoutSeconds = 30)
$ErrorActionPreference = 'Stop'
$projectPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$playerPath = Join-Path $projectPath 'Builds\ControlRoom\ControlRoom.exe'
if (-not (Test-Path -LiteralPath $playerPath)) { throw "Windows player not found: $playerPath" }
$resultPath = Join-Path $projectPath 'TestResults'
$logPath = Join-Path $resultPath ('player-startup-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.log')
New-Item -ItemType Directory -Path $resultPath -Force | Out-Null
$process = Start-Process -FilePath $playerPath -ArgumentList @('-batchmode', '-logFile', ('"' + $logPath + '"')) -WindowStyle Hidden -PassThru
$deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
$readySince = $null
try {
    while ([DateTime]::UtcNow -lt $deadline) {
        if ($process.WaitForExit(500)) { throw "Windows player exited before startup verification: $($process.ExitCode). Log: $logPath" }
        if (Test-Path -LiteralPath $logPath) {
            $playerLog = Get-Content -LiteralPath $logPath -Raw
            if ($playerLog -match 'NullReferenceException|IndexOutOfRangeException|Shader error|Exception:|Failed to load') {
                throw "Windows player reported a runtime error. Log: $logPath"
            }
            if ($playerLog -match 'Control room ready: cameras=4, navigation=True, state=Briefing') {
                if ($null -eq $readySince) { $readySince = [DateTime]::UtcNow }
                if (([DateTime]::UtcNow - $readySince).TotalSeconds -ge 4) {
                    Copy-Item -LiteralPath $logPath -Destination (Join-Path $resultPath 'player-startup.log') -Force
                    Write-Output "PASS: actual Windows player Boot / Mission_01, four feeds, native navigation, Briefing, no startup errors. Log: $logPath"
                    return
                }
            }
        }
    }
    throw "Windows player did not confirm startup within $TimeoutSeconds seconds. Log: $logPath"
}
finally {
    if (-not $process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
}
