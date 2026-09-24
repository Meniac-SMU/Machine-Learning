[CmdletBinding()]
param(
    [ValidateSet('Assets', 'R0Smoke', 'SelfPlaySmoke', 'Both')]
    [string]$Profile = 'Both'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$unityCli = 'C:\Users\USER\AppData\Local\Unity\bin\unity.exe'
$unityVersion = '6000.3.16f1'
$logRoot = Join-Path $projectRoot 'Logs\MNG-MS\MS0'
$sourceManifest = Join-Path $projectRoot 'Logs\MNG-MS\MS0-final-source-v2\source-sha256.json'
if (-not (Test-Path -LiteralPath $unityCli -PathType Leaf)) {
    throw "Unity CLI is missing: $unityCli"
}
if (-not (Test-Path -LiteralPath $sourceManifest -PathType Leaf)) {
    throw 'Run Tools\MNG_MS_Snapshot.ps1 before generating or building MS0 assets.'
}
New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

function Test-ConnectedEditor {
    $statusText = (& $unityCli status --project-path $projectRoot --format json 2>$null | Out-String)
    $jsonStart = $statusText.IndexOf('{')
    if ($LASTEXITCODE -ne 0 -or $jsonStart -lt 0) { return $false }
    $status = $statusText.Substring($jsonStart) | ConvertFrom-Json
    return [int]$status.data.count -gt 0
}

function Get-Sha256Lower([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

function Invoke-ConnectedBuild(
    [string]$BuildProfile,
    [string]$ScenePath,
    [string]$Executable,
    [string]$BuildDirectory
) {
    $sceneArray = '["' + $ScenePath.Replace('"', '\"') + '"]'
    $startPath = Join-Path $logRoot "build-$BuildProfile-start.json"
    $statusPath = Join-Path $logRoot "build-$BuildProfile-status.json"
    $startText = (& $unityCli command build `
        --project-path $projectRoot `
        --timeout 60 `
        --target StandaloneWindows64 `
        --outputPath $Executable `
        --scenes $sceneArray `
        --confirm true `
        --format json 2>&1 | Out-String)
    $startText | Set-Content -Encoding UTF8 -LiteralPath $startPath
    if ($LASTEXITCODE -ne 0) {
        throw "MS0 $BuildProfile async build could not start. See $startPath"
    }

    $deadline = [DateTime]::UtcNow.AddMinutes(30)
    $lastStatus = ''
    do {
        Start-Sleep -Seconds 2
        $statusText = (& $unityCli command build_status `
            --project-path $projectRoot `
            --timeout 60 `
            --format json 2>&1 | Out-String)
        $statusText | Set-Content -Encoding UTF8 -LiteralPath $statusPath
        if ($LASTEXITCODE -ne 0) {
            throw "MS0 $BuildProfile build status failed. See $statusPath"
        }
        $jsonStart = $statusText.IndexOf('{')
        if ($jsonStart -lt 0) { throw "MS0 $BuildProfile build status was not JSON." }
        $outer = $statusText.Substring($jsonStart) | ConvertFrom-Json
        $inner = [string]$outer.data.result | ConvertFrom-Json
        if ([string]$inner.status -ne $lastStatus) {
            $lastStatus = [string]$inner.status
            Write-Host "MNG MS0 BUILD STATUS profile=$BuildProfile status=$lastStatus"
        }
        if ([DateTime]::UtcNow -ge $deadline) {
            throw "MS0 $BuildProfile build timed out after 30 minutes."
        }
    } while ([string]$inner.status -ne 'completed')

    $statusJson = $inner | ConvertTo-Json -Depth 100
    if ($statusJson -notmatch 'Succeeded' -or $statusJson -match '"success"\s*:\s*false') {
        throw "MS0 $BuildProfile build did not succeed. See $statusPath"
    }

    $levelDataPath = Join-Path $BuildDirectory (([IO.Path]::GetFileNameWithoutExtension($Executable)) + '_Data\level0')
    if (-not (Test-Path -LiteralPath $Executable -PathType Leaf) -or
        -not (Test-Path -LiteralPath $levelDataPath -PathType Leaf)) {
        throw "MS0 $BuildProfile Player artifacts are incomplete."
    }
    $scene = $ScenePath
    $buildInfo = [ordered]@{
        stage = "MS0-$BuildProfile"
        scene = $scene
        r0SmokeConfigSha256 = Get-Sha256Lower (Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_MS0_R0Smoke.yaml')
        parallelSmokeConfigSha256 = Get-Sha256Lower (Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_MS0_ParallelSmoke.yaml')
        selfPlaySmokeConfigSha256 = Get-Sha256Lower (Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_MS0_SelfPlaySmoke.yaml')
        protocolSha256 = Get-Sha256Lower (Join-Path $projectRoot 'Assets\_Soccer\Manager\Evaluation\MNG_MS0_Protocol_v1.json')
        sourceManifestSha256 = Get-Sha256Lower $sourceManifest
        executableSha256 = Get-Sha256Lower $Executable
        levelDataSha256 = Get-Sha256Lower $levelDataPath
        unityVersion = $unityVersion
        result = 'Succeeded'
        errors = 0
        bytes = (Get-Item -LiteralPath $Executable).Length
    }
    $buildInfo | ConvertTo-Json -Depth 4 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $BuildDirectory 'build-info.json')
}

$connectedEditor = Test-ConnectedEditor

if ($Profile -eq 'Assets') {
    if ($connectedEditor) {
        & $unityCli command eval `
            --project-path $projectRoot `
            --timeout 300 `
            --code 'MachineLearning.Soccer.Manager.Editor.MNG_MSBuilder.BuildMS0Assets(); return "MNG MS0 ASSETS PASS";' `
            --format json | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $logRoot 'assets-command.json')
    }
    else {
        & $unityCli run $projectRoot `
            --editor-version $unityVersion `
            --timeout 900 `
            -- `
            -nographics `
            -executeMethod MachineLearning.Soccer.Manager.Editor.MNG_MSBuilder.BuildMS0AssetsBatch `
            -logFile (Join-Path $logRoot 'assets.log')
    }
    if ($LASTEXITCODE -ne 0) { throw "MS0 asset generation failed with exit code $LASTEXITCODE." }
    Write-Host 'MNG MS0 ASSET GENERATION PASS'
    return
}

$profiles = if ($Profile -eq 'Both') { @('R0Smoke', 'SelfPlaySmoke') } else { @($Profile) }
foreach ($buildProfile in $profiles) {
    $isSelfPlay = $buildProfile -eq 'SelfPlaySmoke'
    $buildDirectory = if ($isSelfPlay) {
        Join-Path $projectRoot 'Builds\MNG_MS\MS0_SelfPlay'
    }
    else {
        Join-Path $projectRoot 'Builds\MNG_MS\MS0_R0'
    }
    $executable = if ($isSelfPlay) {
        Join-Path $buildDirectory 'MNG_MS0_SelfPlay.exe'
    }
    else {
        Join-Path $buildDirectory 'MNG_MS0_R0.exe'
    }
    New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
    if ($connectedEditor) {
        $scenePath = if ($isSelfPlay) {
            'Assets/_Soccer/Manager/Curriculum/MS_ManagerSimple/Scenes/MNG_MS_SelfPlay.unity'
        }
        else {
            'Assets/_Soccer/Manager/Curriculum/MS_ManagerSimple/Scenes/MNG_MS_Train.unity'
        }
        Invoke-ConnectedBuild $buildProfile $scenePath $executable $buildDirectory
    }
    else {
        & $unityCli build $projectRoot `
            --target StandaloneWindows64 `
            --execute-method MachineLearning.Soccer.Manager.Editor.MNG_MSBuilder.BuildMS0WindowsBatch `
            --output-path $executable `
            --editor-version $unityVersion `
            --allow-dirty-build `
            --timeout 1800 `
            --log-file (Join-Path $logRoot "build-$buildProfile.log") `
            --provenance-path (Join-Path $buildDirectory 'unity-build-provenance.json') `
            --args "-mngMSProfile $buildProfile"
    }
    if ($LASTEXITCODE -ne 0) { throw "MS0 $buildProfile build failed with exit code $LASTEXITCODE." }
    if (-not (Test-Path -LiteralPath (Join-Path $buildDirectory 'build-info.json') -PathType Leaf)) {
        throw "MS0 $buildProfile build did not produce build-info.json."
    }
    Write-Host "MNG MS0 WINDOWS BUILD PASS profile=$buildProfile executable=$executable"
}
