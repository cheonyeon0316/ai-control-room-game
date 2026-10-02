param(
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe',
    [int]$TimeoutSeconds = 1800
)
$ErrorActionPreference = 'Stop'
$webProjectPath = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$webLogDirectory = Join-Path $webProjectPath 'Logs'
New-Item -ItemType Directory -Path $webLogDirectory -Force | Out-Null
$webLogPath = Join-Path $webLogDirectory 'build-web.log'
if (-not (Test-Path -LiteralPath $EditorPath)) { throw "Unity Editor not found: $EditorPath" }
$webModulePath = Join-Path (Split-Path $EditorPath) 'Data\PlaybackEngines\WebGLSupport'
if (-not (Test-Path -LiteralPath $webModulePath)) { throw 'Install Web Build Support for this Editor in Unity Hub.' }
$webArguments = @('-batchmode', '-quit', '-projectPath', $webProjectPath, '-buildTarget', 'WebGL',
    '-executeMethod', 'ControlRoom.Editor.PrototypeProjectSetup.BuildWebGL', '-logFile', $webLogPath)
$quotedWebArguments = @($webArguments | ForEach-Object { if ($_ -match '\s') { '"' + $_ + '"' } else { $_ } })
$webEditorProcess = Start-Process -FilePath $EditorPath -ArgumentList $quotedWebArguments -WindowStyle Hidden -PassThru
$webDeadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
while (-not $webEditorProcess.WaitForExit(1000)) {
    if ([DateTime]::UtcNow -gt $webDeadline) {
        Stop-Process -Id $webEditorProcess.Id -ErrorAction SilentlyContinue
        throw "Web build exceeded $TimeoutSeconds seconds. Log: $webLogPath"
    }
}
$webEditorProcess.Refresh()
if ($webEditorProcess.ExitCode -ne 0) {
    Get-Content -LiteralPath $webLogPath -Tail 80
    throw "Unity Web build failed (exit $($webEditorProcess.ExitCode)). Log: $webLogPath"
}
$webBuildPath = Join-Path $webProjectPath 'Builds\WebGL'
if (-not (Test-Path -LiteralPath (Join-Path $webBuildPath 'index.html'))) { throw 'Web build did not produce index.html.' }
$webPublishPath = Join-Path $webProjectPath 'web\play'
New-Item -ItemType Directory -Path $webPublishPath -Force | Out-Null
Get-ChildItem -LiteralPath $webBuildPath | Copy-Item -Destination $webPublishPath -Recurse -Force
$webLoader = Get-ChildItem -LiteralPath (Join-Path $webPublishPath 'Build') -Filter '*.loader.js' -File | Select-Object -First 1
if ($null -eq $webLoader) { throw 'Web build loader was not found.' }
$webBuildName = $webLoader.Name.Replace('.loader.js', '')
$webTemplate = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'WebPlayerTemplate.html') -Raw
Set-Content -LiteralPath (Join-Path $webPublishPath 'index.html') -Value $webTemplate.Replace('{{BUILD_NAME}}', $webBuildName) -Encoding utf8NoBOM
$webLicensePath = Join-Path $webPublishPath 'licenses'
New-Item -ItemType Directory -Path $webLicensePath -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $webProjectPath 'Assets\Resources\Fonts\OFL.txt') -Destination (Join-Path $webLicensePath 'NotoSansCJKkr-OFL.txt') -Force
Write-Output "Web build published to $webPublishPath"
