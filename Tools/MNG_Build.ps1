[CmdletBinding()]
param(
    [ValidateSet('MNG_Training', 'M1-AttackChoice', 'M1-AttackMoving', 'M2-DefenseChoice', 'M3-FallbackMatch')]
    [string]$Profile = 'MNG_Training',
    [string]$UnityPath = 'C:\\Program Files\\Unity\\Hub\\Editor\\6000.3.16f1\\Editor\\Unity.exe',
    [string]$LogPath = 'Logs\\MNG-Windows-Build.log'
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$resolvedUnity = (Resolve-Path -LiteralPath $UnityPath).Path
$resolvedLog = Join-Path $projectRoot $LogPath

if (Get-Process Unity -ErrorAction SilentlyContinue) {
    throw 'Close the Unity Editor before starting the MNG batch build.'
}

Push-Location $projectRoot
try {
    $unityArguments = @(
        '-batchmode',
        '-nographics',
        '-projectPath', $projectRoot,
        '-mngStage', $(if ($Profile -eq 'M1-AttackChoice') { 'M1-AttackChoice' } elseif ($Profile -eq 'M1-AttackMoving') { 'M1-AttackMoving' } elseif ($Profile -eq 'M2-DefenseChoice') { 'M2-DefenseChoice' } elseif ($Profile -eq 'M3-FallbackMatch') { 'M3-FallbackMatch' } else { 'M1-Smoke' }),
        '-executeMethod', 'MachineLearning.Soccer.Manager.Editor.MNG_TrainingBuildBuilder.BuildWindowsBatch',
        '-logFile', $resolvedLog
    )
    $unityProcess = Start-Process `
        -FilePath $resolvedUnity `
        -ArgumentList $unityArguments `
        -WindowStyle Hidden `
        -Wait `
        -PassThru
    if ($unityProcess.ExitCode -ne 0) {
        throw "MNG Windows build failed with exit code $($unityProcess.ExitCode). See $resolvedLog"
    }

    $executableRelative = if ($Profile -eq 'M1-AttackChoice') {
        'Builds\\MNG_AttackChoice\\MNG_AttackChoice.exe'
    }
    elseif ($Profile -eq 'M1-AttackMoving') {
        'Builds\\MNG_AttackMoving\\MNG_AttackMoving.exe'
    }
    elseif ($Profile -eq 'M2-DefenseChoice') {
        'Builds\\MNG_DefenseChoice\\MNG_DefenseChoice.exe'
    }
    elseif ($Profile -eq 'M3-FallbackMatch') {
        'Builds\\MNG_FallbackMatch\\MNG_FallbackMatch.exe'
    }
    else {
        'Builds\\MNG_Training\\MNG_Training.exe'
    }
    $executable = Join-Path $projectRoot $executableRelative
    if (-not (Test-Path -LiteralPath $executable -PathType Leaf)) {
        throw "MNG build did not create $executable"
    }
    Get-Item -LiteralPath $executable | Select-Object FullName, Length, LastWriteTime
}
finally {
    Pop-Location
}
