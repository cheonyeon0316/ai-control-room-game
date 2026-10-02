param(
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe',
    [switch]$Build,
    [int]$TimeoutSeconds = 1200
)
$ErrorActionPreference = 'Stop'
$projectPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$resultPath = Join-Path $projectPath 'TestResults'
New-Item -ItemType Directory -Path $resultPath -Force | Out-Null
$runPath = Join-Path $resultPath ('runs\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $runPath -Force | Out-Null
if (-not (Test-Path -LiteralPath $EditorPath)) { throw "Unity Editor not found: $EditorPath" }

function Invoke-UnityStep([string]$name, [string[]]$extraArguments, [int[]]$allowedExitCodes = @(0)) {
    $logPath = Join-Path $runPath ($name + '.log')
    $arguments = @('-batchmode', '-projectPath', $projectPath, '-logFile', $logPath) + $extraArguments
    $quotedArguments = @($arguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } })
    $process = Start-Process -FilePath $EditorPath -ArgumentList $quotedArguments -WindowStyle Hidden -PassThru
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    $licensingIssueSince = $null
    while (-not $process.WaitForExit(1000)) {
        if (Test-Path -LiteralPath $logPath) {
            $recentLog = Get-Content -LiteralPath $logPath -Tail 30 -ErrorAction SilentlyContinue
            $invalidLicense = $recentLog -match "No valid Unity Editor license found|Licensing initialization failed|your license doesn't allow it"
            if ($recentLog -match 'Connection to channel LicenseClient.*refused|connection with the Unity Licensing Client has been lost') {
                if ($null -eq $licensingIssueSince) { $licensingIssueSince = [DateTime]::UtcNow }
            }
            else { $licensingIssueSince = $null }
            $licensingStalled = $null -ne $licensingIssueSince -and ([DateTime]::UtcNow - $licensingIssueSince).TotalSeconds -ge 90
            if ($invalidLicense -or $licensingStalled) {
                Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
                Copy-Item -LiteralPath $logPath -Destination (Join-Path $resultPath ($name + '.log')) -Force
                throw "Unity license activation is required. Log: $logPath"
            }
        }
        if ([DateTime]::UtcNow -gt $deadline) {
            Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
            throw "Unity $name exceeded $TimeoutSeconds seconds. Log: $logPath"
        }
    }
    $process.Refresh()
    Copy-Item -LiteralPath $logPath -Destination (Join-Path $resultPath ($name + '.log')) -Force
    if ($process.ExitCode -notin $allowedExitCodes) {
        Get-Content -LiteralPath $logPath -Tail 60
        throw "Unity $name failed (exit $($process.ExitCode)); log: $logPath"
    }
}

Invoke-UnityStep 'prepare' @('-quit', '-executeMethod', 'ControlRoom.Editor.PrototypeProjectSetup.PrepareProject')
foreach ($mode in @('EditMode', 'PlayMode')) {
    $xmlPath = Join-Path $runPath ($mode + '.xml')
    Invoke-UnityStep $mode @('-runTests', '-testPlatform', $mode, '-testResults', $xmlPath) @(0, 2)
    if (-not (Test-Path -LiteralPath $xmlPath)) { throw "$mode did not produce NUnit XML results." }
    [xml]$report = Get-Content -LiteralPath $xmlPath -Raw
    Copy-Item -LiteralPath $xmlPath -Destination (Join-Path $resultPath ($mode + '.xml')) -Force
    $run = $report.'test-run'
    if ($mode -eq 'PlayMode') {
        foreach ($capture in Get-ChildItem -LiteralPath $resultPath -Filter '*.png' -File) {
            if ($capture.LastWriteTimeUtc -ge (Get-Item -LiteralPath $runPath).CreationTimeUtc) {
                Copy-Item -LiteralPath $capture.FullName -Destination (Join-Path $runPath $capture.Name) -Force
            }
        }
    }
    Write-Output "$mode : total=$($run.total) passed=$($run.passed) failed=$($run.failed) skipped=$($run.skipped)"
    if ($run.result -ne 'Passed' -or [int]$run.total -le 0 -or [int]$run.passed -ne [int]$run.total -or
        [int]$run.failed -gt 0 -or [int]$run.skipped -gt 0 -or [int]$run.inconclusive -gt 0) {
        throw "$mode acceptance did not fully pass: $xmlPath"
    }
    if ($mode -eq 'PlayMode') {
        $suiteSource = Get-Content -LiteralPath (Join-Path $projectPath 'Assets\Tests\PlayMode\PrototypeAcceptanceTests.cs') -Raw
        $requiredTests = [regex]::Matches($suiteSource, '\[UnityTest\]\s*public IEnumerator (\w+)\(')
        $actualTests = $report.SelectNodes('//test-case')
        foreach ($required in $requiredTests) {
            $testName = $required.Groups[1].Value
            $matching = @($actualTests | Where-Object { $_.name -eq $testName -or $_.fullname -like "*.$testName" })
            if ($matching.Count -ne 1 -or $matching[0].result -ne 'Passed') {
                throw "Required PlayMode case missing or unsuccessful: $testName. Report: $xmlPath"
            }
        }
        if ($requiredTests.Count -eq 0) { throw 'No required PlayMode acceptance cases were discovered in source.' }
    }
}
if ($Build) {
    Invoke-UnityStep 'build-windows' @('-quit', '-executeMethod', 'ControlRoom.Editor.PrototypeProjectSetup.BuildWindows')
    & (Join-Path $PSScriptRoot 'Verify-PlayerStartup.ps1')
    Copy-Item -LiteralPath (Join-Path $resultPath 'player-startup.log') -Destination (Join-Path $runPath 'player-startup.log') -Force
}
Write-Output "Unity acceptance passed. Results: $resultPath"
