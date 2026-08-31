[CmdletBinding()]
param(
    [string]$UnityEditor = 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$logDirectory = Join-Path $projectRoot 'Logs'
$logPath = Join-Path $logDirectory 'Soccer-Training-Build.log'
$executablePath = Join-Path $projectRoot 'Builds\SoccerTraining\SoccerTraining.exe'
$manifestPath = Join-Path $projectRoot 'Builds\SoccerTraining\training-profiles.json'

if (-not (Test-Path -LiteralPath $UnityEditor -PathType Leaf)) {
    throw "Unity Editor was not found: $UnityEditor"
}

$runningEditors = @(Get-Process -Name Unity -ErrorAction SilentlyContinue)
if ($runningEditors.Count -gt 0) {
    $editorIds = ($runningEditors | ForEach-Object { $_.Id }) -join ', '
    throw "Close every Unity Editor before batch build. Running process IDs: $editorIds"
}

$runningHub = @(Get-Process -Name 'Unity Hub',UnityHub -ErrorAction SilentlyContinue)
if ($runningHub.Count -gt 0) {
    $hubIds = ($runningHub | ForEach-Object { $_.Id }) -join ', '
    Write-Warning "Unity Hub is running and can trigger the known licensing protocol conflict. Continuing with direct Editor batch mode. Hub process IDs: $hubIds"
}

New-Item -ItemType Directory -Path $logDirectory -Force | Out-Null

Write-Host 'Building the shared Soccer training player...'
$unityArguments = @(
    '-batchmode',
    '-nographics',
    '-quit',
    '-projectPath', ('"{0}"' -f $projectRoot),
    '-executeMethod', 'MachineLearning.Soccer.Editor.SoccerTrainingBuildBuilder.BuildWindowsBatch',
    '-logFile', ('"{0}"' -f $logPath)
) -join ' '
$unityProcess = Start-Process `
    -FilePath $UnityEditor `
    -ArgumentList $unityArguments `
    -WindowStyle Hidden `
    -Wait `
    -PassThru

if ($unityProcess.ExitCode -ne 0) {
    throw "Unity training build failed with exit code $($unityProcess.ExitCode). See: $logPath"
}

if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Unity reported success but the executable is missing: $executablePath"
}

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Unity reported success but the profile manifest is missing: $manifestPath"
}

$sizeBytes = (Get-Item -LiteralPath $executablePath).Length
$sha256 = (Get-FileHash -LiteralPath $executablePath -Algorithm SHA256).Hash.ToLowerInvariant()
Write-Host "Build succeeded: $executablePath"
Write-Host "Size: $sizeBytes bytes"
Write-Host "SHA-256: $sha256"
Write-Host "Profiles: $manifestPath"
