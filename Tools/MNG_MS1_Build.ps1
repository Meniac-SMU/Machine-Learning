[CmdletBinding()]
param(
    [ValidateSet('Assets', 'Player')]
    [string]$Profile = 'Player'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$unityCli = 'C:\Users\USER\AppData\Local\Unity\bin\unity.exe'
$sourceManifest = Join-Path $projectRoot 'Logs\MNG-MS\MS1-final-source-v19\source-sha256.json'
$logRoot = Join-Path $projectRoot 'Logs\MNG-MS\MS1'
$buildRoot = Join-Path $projectRoot 'Builds\MNG_MS\MS1'
$executable = Join-Path $buildRoot 'MNG_MS1.exe'
$scene = 'Assets/_Soccer/Manager/Curriculum/MS_ManagerSimple/Scenes/MNG_MS1_Train.unity'
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

if (-not (Test-Path -LiteralPath $unityCli -PathType Leaf)) { throw "Unity CLI is missing: $unityCli" }

function Get-Sha256Lower([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

if ($Profile -eq 'Assets') {
    $text = (& $unityCli command eval --project-path $projectRoot --timeout 300 `
        --code 'MachineLearning.Soccer.Manager.Editor.MNG_MSBuilder.BuildMS1Assets(); return "MNG MS1 ASSETS PASS";' `
        --format json 2>&1 | Out-String)
    $text | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $logRoot 'assets-command.json')
    if ($LASTEXITCODE -ne 0 -or $text -notmatch 'MNG MS1 ASSETS PASS') {
        throw 'MS1 asset generation failed. See Logs\MNG-MS\MS1\assets-command.json.'
    }
    Write-Host 'MNG MS1 ASSET GENERATION PASS'
    return
}

if (-not (Test-Path -LiteralPath $sourceManifest -PathType Leaf)) {
    throw 'Create Logs\MNG-MS\MS1-final-source-v19 before building the MS1 Player.'
}
New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
$sceneArray = '["' + $scene + '"]'
$startText = (& $unityCli command build --project-path $projectRoot --timeout 60 `
    --target StandaloneWindows64 --outputPath $executable --scenes $sceneArray `
    --confirm true --format json 2>&1 | Out-String)
$startText | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $logRoot 'build-start.json')
if ($LASTEXITCODE -ne 0) { throw 'MS1 Player build could not start.' }

$deadline = [DateTime]::UtcNow.AddMinutes(30)
do {
    Start-Sleep -Seconds 2
    $statusText = (& $unityCli command build_status --project-path $projectRoot --timeout 60 --format json 2>&1 | Out-String)
    $statusText | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $logRoot 'build-status.json')
    if ($LASTEXITCODE -ne 0) { throw 'MS1 Player build status failed.' }
    $jsonStart = $statusText.IndexOf('{')
    if ($jsonStart -lt 0) { throw 'MS1 build status was not JSON.' }
    $outer = $statusText.Substring($jsonStart) | ConvertFrom-Json
    $inner = [string]$outer.data.result | ConvertFrom-Json
    if ([DateTime]::UtcNow -ge $deadline) { throw 'MS1 Player build timed out.' }
} while ([string]$inner.status -ne 'completed')

$statusJson = $inner | ConvertTo-Json -Depth 100
if ($statusJson -notmatch 'Succeeded' -or $statusJson -match '"success"\s*:\s*false') {
    throw 'MS1 Player build did not succeed.'
}
$level = Join-Path $buildRoot 'MNG_MS1_Data\level0'
$managedAssembly = Join-Path $buildRoot 'MNG_MS1_Data\Managed\MNG.Runtime.dll'
foreach ($path in @($executable, $level, $managedAssembly)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing MS1 build artifact: $path" }
}
$config = Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_MS1.yaml'
$passRepairConfig = Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_MS1_PassRepair.yaml'
$passFineTuneConfig = Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_MS1_PassFineTune.yaml'
$passPolishConfig = Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_MS1_PassPolish.yaml'
$passContinuationConfig = Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_MS1_PassContinuation.yaml'
$passDecisionPolishConfig = Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_MS1_PassDecisionPolish.yaml'
$protocol = Join-Path $projectRoot 'Assets\_Soccer\Manager\Evaluation\MNG_MS1_Protocol_v8.json'
[ordered]@{
    stage = 'MS1'
    scene = $scene
    configSha256 = Get-Sha256Lower $config
    passRepairConfigSha256 = Get-Sha256Lower $passRepairConfig
    passFineTuneConfigSha256 = Get-Sha256Lower $passFineTuneConfig
    passPolishConfigSha256 = Get-Sha256Lower $passPolishConfig
    passContinuationConfigSha256 = Get-Sha256Lower $passContinuationConfig
    passDecisionPolishConfigSha256 = Get-Sha256Lower $passDecisionPolishConfig
    protocolSha256 = Get-Sha256Lower $protocol
    sourceManifestSha256 = Get-Sha256Lower $sourceManifest
    executableSha256 = Get-Sha256Lower $executable
    levelDataSha256 = Get-Sha256Lower $level
    managedAssemblySha256 = Get-Sha256Lower $managedAssembly
    unityVersion = '6000.3.16f1'
    result = 'Succeeded'
    errors = 0
    bytes = (Get-Item -LiteralPath $executable).Length
} | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $buildRoot 'build-info.json')
Write-Host "MNG MS1 WINDOWS BUILD PASS executable=$executable"
