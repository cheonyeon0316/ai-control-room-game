param(
    [string]$EditorPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.11f1\Editor\Unity.exe',
    [string]$ProjectPath = (Join-Path $PSScriptRoot '..')
)

$ErrorActionPreference = 'Stop'
$projectCompilationRoot = (Resolve-Path -LiteralPath $ProjectPath).Path
$compilationOutputDirectory = Join-Path $projectCompilationRoot 'TestResults\ManagedCompile'
$compilationLogPath = Join-Path $projectCompilationRoot 'TestResults\managed-compile.log'
New-Item -ItemType Directory -Path $compilationOutputDirectory -Force | Out-Null
Set-Content -LiteralPath $compilationLogPath -Value 'Managed C# compilation against installed Unity assemblies. Unity Editor is not started.' -Encoding utf8
# Keep generated references and binaries out of source control even before the project ignore rule is installed.
Set-Content -LiteralPath (Join-Path $compilationOutputDirectory '.gitignore') -Value '*' -Encoding utf8

function Write-CompilationLog([string]$Message) {
    Write-Host $Message
    Add-Content -LiteralPath $compilationLogPath -Value $Message -Encoding utf8
}

function Get-CSharpInputs([string]$RelativeDirectory) {
    $sourceDirectory = Join-Path $projectCompilationRoot $RelativeDirectory
    if (!(Test-Path -LiteralPath $sourceDirectory)) { throw "Missing source directory: $RelativeDirectory" }
    $ripgrepCommand = Get-Command rg -ErrorAction SilentlyContinue
    if ($null -ne $ripgrepCommand) {
        $sourceFiles = @(& $ripgrepCommand.Source --files $sourceDirectory | Where-Object { $_ -match '\.cs$' } | Sort-Object)
    } else {
        $sourceFiles = @(Get-ChildItem -LiteralPath $sourceDirectory -Recurse -Filter '*.cs' | ForEach-Object FullName | Sort-Object)
    }
    if ($sourceFiles.Count -eq 0) { throw "No actual C# sources found: $RelativeDirectory" }
    return $sourceFiles
}

function Compile-UnityAssembly(
    [string]$AssemblyName,
    [string]$RelativeDirectory,
    [string[]]$References,
    [string]$Defines
) {
    $inputFiles = @(Get-CSharpInputs $RelativeDirectory)
    $outputAssembly = Join-Path $compilationOutputDirectory ($AssemblyName + '.dll')
    $argumentFile = Join-Path $compilationOutputDirectory ($AssemblyName + '.rsp')
    $compilerArguments = @('-nostdlib+', '-target:library', '-langversion:latest', '-utf8output', ('-define:' + $Defines), ('-out:"' + $outputAssembly + '"'))
    $compilerArguments += @($References | Sort-Object -Unique | ForEach-Object { '-r:"' + $_ + '"' })
    $compilerArguments += @($inputFiles | ForEach-Object { '"' + $_ + '"' })
    Set-Content -LiteralPath $argumentFile -Value $compilerArguments -Encoding utf8
    Write-CompilationLog ("Compiling {0}: {1} actual source files." -f $AssemblyName, $inputFiles.Count)
    $compilerOutput = @(& $compilationDotNetHost $compilationRoslynCompiler -noconfig "@$argumentFile" 2>&1)
    $compilerExitCode = $LASTEXITCODE
    foreach ($line in $compilerOutput) { Write-CompilationLog ([string]$line) }
    if ($compilerExitCode -ne 0) { throw "$AssemblyName compilation failed (exit $compilerExitCode)." }
    if (!(Test-Path -LiteralPath $outputAssembly)) { throw "$AssemblyName did not produce an output assembly." }
    Write-CompilationLog ("PASS {0}: {1}" -f $AssemblyName, $outputAssembly)
    return $outputAssembly
}

try {
    if (!(Test-Path -LiteralPath $EditorPath)) {
        $installedEditors = @(Get-ChildItem -LiteralPath 'C:\Program Files\Unity\Hub\Editor' -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending | ForEach-Object { Join-Path $_.FullName 'Editor\Unity.exe' } | Where-Object { Test-Path -LiteralPath $_ })
        if ($installedEditors.Count -eq 0) { throw "Unity Editor installation was not found. Pass -EditorPath with its Unity.exe or Editor directory." }
        $EditorPath = $installedEditors[0]
    }
    $editorCompilationItem = Get-Item -LiteralPath $EditorPath
    $editorCompilationDirectory = if ($editorCompilationItem.PSIsContainer) { $editorCompilationItem.FullName } else { $editorCompilationItem.DirectoryName }
    $unityCompilationDataDirectory = Join-Path $editorCompilationDirectory 'Data'
    $compilationRoslynCompiler = Join-Path $unityCompilationDataDirectory 'DotNetSdkRoslyn\csc.dll'
    $unityCompilationApiDirectory = Join-Path $unityCompilationDataDirectory 'UnityReferenceAssemblies\unity-4.8-api'
    $unityCompilationEngineDirectory = Join-Path $unityCompilationDataDirectory 'Managed\UnityEngine'
    $unityCompilationManagedDirectory = Join-Path $unityCompilationDataDirectory 'Managed'
    $unityCompilationEditorDirectory = Join-Path $unityCompilationDataDirectory 'Managed\UnityEditor'
    $unityCompilationTemplateCache = Join-Path $unityCompilationDataDirectory 'Resources\PackageManager\ProjectTemplates\libcache'
    $unityCompilationNunit = Join-Path $unityCompilationDataDirectory 'Resources\PackageManager\BuiltInPackages\com.unity.ext.nunit\net40\unity-custom\nunit.framework.dll'
    $compilationDotNetHost = (Get-Command dotnet.exe -ErrorAction Stop).Source
    foreach ($requiredPath in @($compilationRoslynCompiler, $unityCompilationApiDirectory, $unityCompilationEngineDirectory, $unityCompilationTemplateCache, $unityCompilationNunit)) {
        if (!(Test-Path -LiteralPath $requiredPath)) { throw "Installed Unity dependency is unavailable: $requiredPath" }
    }
    $templateCompilationAssemblyDirectory = @(Get-ChildItem -LiteralPath $unityCompilationTemplateCache -Directory |
        Where-Object { $_.Name -like 'com.unity.template.3d-cross-platform-*' } | Sort-Object Name -Descending |
        ForEach-Object { Join-Path $_.FullName 'ScriptAssemblies' } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1)
    if ($templateCompilationAssemblyDirectory.Count -ne 1) { throw 'Installed URP template ScriptAssemblies were not found.' }
    $baseCompilationReferences = @(Get-ChildItem -LiteralPath $unityCompilationApiDirectory -Recurse -Filter '*.dll' | ForEach-Object FullName) +
        @(Get-ChildItem -LiteralPath $unityCompilationEngineDirectory -Filter '*.dll' | ForEach-Object FullName) +
        @(Get-ChildItem -LiteralPath $unityCompilationManagedDirectory -Filter '*.dll' | ForEach-Object FullName) +
        @(Get-ChildItem -LiteralPath $templateCompilationAssemblyDirectory[0] -Filter '*.dll' | ForEach-Object FullName)
    $editorCompilationReferences = $baseCompilationReferences
    if (Test-Path -LiteralPath $unityCompilationEditorDirectory) {
        $editorCompilationReferences += @(Get-ChildItem -LiteralPath $unityCompilationEditorDirectory -Filter '*.dll' | ForEach-Object FullName)
    }
    Write-CompilationLog ("Unity installation: " + $editorCompilationDirectory)
    Write-CompilationLog ("Project: " + $projectCompilationRoot)
    Write-CompilationLog 'Validation scope: actual C# compiler/type checks only; engine behavior and acceptance tests require Unity EditMode/PlayMode execution.'
    $runtimeCompilationDll = Compile-UnityAssembly 'ControlRoom.Runtime' 'Assets\Scripts' $baseCompilationReferences 'UNITY_6000_0_OR_NEWER,UNITY_STANDALONE_WIN,ENABLE_LEGACY_INPUT_MANAGER'
    $editorCompilationDll = Compile-UnityAssembly 'ControlRoom.Editor' 'Assets\Editor' ($editorCompilationReferences + @($runtimeCompilationDll)) 'UNITY_EDITOR,UNITY_EDITOR_WIN,UNITY_6000_0_OR_NEWER'
    $testCompilationReferences = $editorCompilationReferences + @($runtimeCompilationDll, $unityCompilationNunit)
    $editModeCompilationDll = Compile-UnityAssembly 'ControlRoom.EditModeTests' 'Assets\Tests\EditMode' $testCompilationReferences 'UNITY_EDITOR,UNITY_EDITOR_WIN,UNITY_INCLUDE_TESTS,UNITY_6000_0_OR_NEWER'
    $playModeCompilationDll = Compile-UnityAssembly 'ControlRoom.PlayModeTests' 'Assets\Tests\PlayMode' $testCompilationReferences 'UNITY_EDITOR,UNITY_EDITOR_WIN,UNITY_INCLUDE_TESTS,UNITY_6000_0_OR_NEWER,ENABLE_LEGACY_INPUT_MANAGER'
    Write-CompilationLog 'PASS: Runtime, Editor, EditMode test and PlayMode test assemblies compiled against the installed Unity API.'
    exit 0
} catch {
    Write-CompilationLog ('FAIL: ' + $_.Exception.Message)
    exit 1
}
