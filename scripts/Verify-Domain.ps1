param(
    [string] $UnityEditorDirectory = '',
    [string] $ProjectDirectory = (Split-Path -Parent $PSScriptRoot)
)

$ErrorActionPreference = 'Stop'
$projectPath = [System.IO.Path]::GetFullPath($ProjectDirectory)
$resultsPath = Join-Path $projectPath 'TestResults/domain-results.json'
$resultsDirectory = Split-Path -Parent $resultsPath
New-Item -ItemType Directory -Force -Path $resultsDirectory | Out-Null
$runnerExitCode = 2
$previousMonoPath = $env:MONO_PATH

function Write-InfrastructureFailure([string] $message) {
    $report = [ordered]@{
        schemaVersion = 1
        kind = 'independent-domain'
        isUnityAcceptance = $false
        isPlayMode = $false
        description = 'Independent domain runner could not execute the repository NUnit cases.'
        finishedAtUtc = [System.DateTime]::UtcNow.ToString('o')
        total = 0
        passed = 0
        failed = 0
        unrun = 0
        infrastructureError = $message
        tests = @()
    }
    [System.IO.File]::WriteAllText($resultsPath, ($report | ConvertTo-Json -Depth 10), [System.Text.UTF8Encoding]::new($false))
}

try {
    if ([string]::IsNullOrWhiteSpace($UnityEditorDirectory)) {
        $versionFile = Join-Path $projectPath 'ProjectSettings/ProjectVersion.txt'
        $versionLine = Get-Content -LiteralPath $versionFile | Where-Object { $_ -match '^m_EditorVersion:\s*(\S+)' } | Select-Object -First 1
        if (-not $versionLine -or $versionLine -notmatch '^m_EditorVersion:\s*(\S+)') {
            throw 'ProjectSettings/ProjectVersion.txt does not contain an Editor version. Supply -UnityEditorDirectory.'
        }
        $UnityEditorDirectory = Join-Path $env:ProgramFiles ('Unity/Hub/Editor/' + $Matches[1] + '/Editor')
    }
    $editorPath = [System.IO.Path]::GetFullPath($UnityEditorDirectory)
    $monoPath = Join-Path $editorPath 'Data/MonoBleedingEdge/bin/mono.exe'
    $compilerPath = Join-Path $editorPath 'Data/MonoBleedingEdge/lib/mono/4.5/csc.exe'
    $nunitPath = Join-Path $editorPath 'Data/Resources/PackageManager/BuiltInPackages/com.unity.ext.nunit/net40/unity-custom/nunit.framework.dll'
    $managedPath = Join-Path $editorPath 'Data/Managed/UnityEngine'
    $references = @(
        $nunitPath,
        (Join-Path $editorPath 'Data/MonoBleedingEdge/lib/mono/4.5/Facades/netstandard.dll'),
        (Join-Path $managedPath 'UnityEngine.CoreModule.dll'),
        (Join-Path $managedPath 'UnityEngine.UnityWebRequestModule.dll')
    )
    foreach ($dependency in @($monoPath, $compilerPath) + $references) {
        if (-not (Test-Path -LiteralPath $dependency -PathType Leaf)) { throw "Required bundled runtime dependency missing: $dependency" }
    }

    # These are the actual repository sources. No generated substitutes, mocks or transformed contract copies.
    $sourcePaths = @(
        'Assets/Scripts/Core/Contracts.cs',
        'Assets/Scripts/Core/CommandJournal.cs',
        'Assets/Scripts/Command/CommandVocabulary.cs',
        'Assets/Scripts/Command/RuleCommandParser.cs',
        'Assets/Scripts/Command/CommandValidator.cs',
        'Assets/Scripts/Command/ClarificationContext.cs',
        'Assets/Scripts/Command/CommandJsonCodec.cs',
        'Assets/Scripts/Command/LlmCommandInterpreter.cs',
        'Assets/Scripts/Analysis/AnalysisService.cs',
        'Assets/Scripts/Evidence/EvidenceDatabase.cs',
        'Assets/Scripts/Mission/MissionRules.cs',
        'Assets/Tests/EditMode/CommandDomainTests.cs',
        'Assets/Tests/EditMode/AnalysisMissionTests.cs',
        'scripts/DomainTestRunner.cs'
    )
    $absoluteSources = @($sourcePaths | ForEach-Object { Join-Path $projectPath $_ })
    foreach ($source in $absoluteSources) {
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) { throw "Repository source missing: $source" }
    }
    $sourceManifest = @($absoluteSources | ForEach-Object {
        [ordered]@{ path = $_; sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() }
    })
    $runnerDirectory = Join-Path ([System.IO.Path]::GetTempPath()) ('ControlRoom-Domain-' + [System.Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Force -Path $runnerDirectory | Out-Null
    $runnerExecutable = Join-Path $runnerDirectory 'ControlRoom.DomainTests.exe'
    $runnerResultPath = Join-Path $runnerDirectory 'domain-results.json'
    $compilerArguments = @($compilerPath, '/nologo', '/target:exe', "/out:$runnerExecutable") +
        @($references | ForEach-Object { "/reference:$_" }) + $absoluteSources
    Write-Output 'Compiling actual repository domain sources and NUnit tests with bundled Mono.'
    & $monoPath @compilerArguments
    if ($LASTEXITCODE -ne 0) { throw "Domain compilation failed with exit code $LASTEXITCODE. No tests passed or failed were fabricated." }

    $env:MONO_PATH = (Split-Path -Parent $nunitPath) + ';' + $managedPath + ';' + $runnerDirectory
    & $monoPath $runnerExecutable $runnerResultPath
    $runnerExitCode = $LASTEXITCODE
    if (-not (Test-Path -LiteralPath $runnerResultPath -PathType Leaf)) {
        throw "The test runner exited with code $runnerExitCode without producing new domain-results.json. Earlier results were not reused."
    }
    $report = Get-Content -LiteralPath $runnerResultPath -Raw | ConvertFrom-Json
    $report | Add-Member -NotePropertyName unityEditorDirectory -NotePropertyValue $editorPath
    $report | Add-Member -NotePropertyName sourceManifest -NotePropertyValue $sourceManifest
    $report | Add-Member -NotePropertyName exitCode -NotePropertyValue $runnerExitCode
    [System.IO.File]::WriteAllText($resultsPath, ($report | ConvertTo-Json -Depth 20), [System.Text.UTF8Encoding]::new($false))
    Write-Output "Saved machine results: $resultsPath"
}
catch {
    Write-InfrastructureFailure $_.Exception.Message
    Write-Error -Message $_.Exception.Message -ErrorAction Continue
    $runnerExitCode = 2
}
finally {
    $env:MONO_PATH = $previousMonoPath
    # Temporary binaries are retained under the printed temporary directory for diagnosis; no workspace source is changed.
    if ($runnerDirectory) { Write-Output "Compiler output: $runnerDirectory" }
}

exit $runnerExitCode
