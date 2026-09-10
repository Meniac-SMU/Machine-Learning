[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('M1-Smoke', 'M1-AttackChoice', 'M1-AttackMoving', 'M2-DefenseChoice', 'M3-FallbackMatch')]
    [string]$Stage,

    [Parameter(Mandatory = $true)]
    [string]$RunId,

    [ValidateSet(1)]
    [int]$NumEnvs = 1,

    [int]$Seed = 11001,

    [int]$BasePort = 5705,

    [string]$TorchDevice = 'cuda',

    [switch]$Resume,

    [string]$InitializeFrom,

    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256Lower([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $algorithm = [System.Security.Cryptography.SHA256]::Create()
        try {
            return ([System.BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
        }
        finally {
            $algorithm.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildDirectory = if ($Stage -eq 'M1-AttackChoice') {
    Join-Path $projectRoot 'Builds\MNG_AttackChoice'
}
elseif ($Stage -eq 'M1-AttackMoving') {
    Join-Path $projectRoot 'Builds\MNG_AttackMoving'
}
elseif ($Stage -eq 'M2-DefenseChoice') {
    Join-Path $projectRoot 'Builds\MNG_DefenseChoice'
}
elseif ($Stage -eq 'M3-FallbackMatch') {
    Join-Path $projectRoot 'Builds\MNG_FallbackMatch'
}
else {
    Join-Path $projectRoot 'Builds\MNG_Training'
}
$executablePath = if ($Stage -eq 'M1-AttackChoice') {
    Join-Path $buildDirectory 'MNG_AttackChoice.exe'
}
elseif ($Stage -eq 'M1-AttackMoving') {
    Join-Path $buildDirectory 'MNG_AttackMoving.exe'
}
elseif ($Stage -eq 'M2-DefenseChoice') {
    Join-Path $buildDirectory 'MNG_DefenseChoice.exe'
}
elseif ($Stage -eq 'M3-FallbackMatch') {
    Join-Path $buildDirectory 'MNG_FallbackMatch.exe'
}
else {
    Join-Path $buildDirectory 'MNG_Training.exe'
}
$buildInfoPath = Join-Path $buildDirectory 'build-info.json'
$levelDataPath = if ($Stage -eq 'M1-AttackChoice') {
    Join-Path $buildDirectory 'MNG_AttackChoice_Data\level0'
}
elseif ($Stage -eq 'M1-AttackMoving') {
    Join-Path $buildDirectory 'MNG_AttackMoving_Data\level0'
}
elseif ($Stage -eq 'M2-DefenseChoice') {
    Join-Path $buildDirectory 'MNG_DefenseChoice_Data\level0'
}
elseif ($Stage -eq 'M3-FallbackMatch') {
    Join-Path $buildDirectory 'MNG_FallbackMatch_Data\level0'
}
else {
    Join-Path $buildDirectory 'MNG_Training_Data\level0'
}
$configPath = if ($Stage -eq 'M1-AttackChoice') {
    Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_M1_AttackChoice.yaml'
}
elseif ($Stage -eq 'M1-AttackMoving') {
    Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_M1_AttackMoving.yaml'
}
elseif ($Stage -eq 'M2-DefenseChoice') {
    Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_M2_DefenseChoice.yaml'
}
elseif ($Stage -eq 'M3-FallbackMatch') {
    Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_M3_FallbackMatch.yaml'
}
else {
    Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_M1_ConnectionSmoke.yaml'
}
$resultsDirectory = Join-Path $projectRoot 'results'
$runDirectory = Join-Path $resultsDirectory $RunId

$requiredRunPattern = if ($Stage -eq 'M1-AttackChoice') {
    '^MNG_M1Attack-\d{8}-r\d{3}$'
}
elseif ($Stage -eq 'M1-AttackMoving') {
    '^MNG_M1Moving-\d{8}-r\d{3}$'
}
elseif ($Stage -eq 'M2-DefenseChoice') {
    '^MNG_M2Defense-\d{8}-r\d{3}$'
}
elseif ($Stage -eq 'M3-FallbackMatch') {
    '^MNG_M3Fallback-\d{8}-r\d{3}$'
}
else {
    '^MNG_M1Smoke-\d{8}-r\d{3}$'
}
$requiredRunExample = if ($Stage -eq 'M1-AttackChoice') {
    'MNG_M1Attack-YYYYMMDD-rNNN'
}
elseif ($Stage -eq 'M1-AttackMoving') {
    'MNG_M1Moving-YYYYMMDD-rNNN'
}
elseif ($Stage -eq 'M2-DefenseChoice') {
    'MNG_M2Defense-YYYYMMDD-rNNN'
}
elseif ($Stage -eq 'M3-FallbackMatch') {
    'MNG_M3Fallback-YYYYMMDD-rNNN'
}
else {
    'MNG_M1Smoke-YYYYMMDD-rNNN'
}
if ($RunId -notmatch $requiredRunPattern) {
    throw "Run ID must match $requiredRunExample. Received: $RunId"
}
if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "MNG training executable is missing. Run Tools\MNG_Build.ps1 first: $executablePath"
}
if (-not (Test-Path -LiteralPath $buildInfoPath -PathType Leaf)) {
    throw "MNG build manifest is missing: $buildInfoPath"
}
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "MNG trainer config is missing: $configPath"
}

$buildInfo = Get-Content -Raw -Encoding UTF8 -LiteralPath $buildInfoPath | ConvertFrom-Json
if ((Get-Sha256Lower $configPath) -ne [string]$buildInfo.trainerConfigSha256) {
    throw 'MNG YAML changed after the Player build. Rebuild before training.'
}
if ((Get-Sha256Lower $executablePath) -ne [string]$buildInfo.executableSha256) {
    throw 'MNG executable hash does not match build-info.json. Rebuild before training.'
}
if ((Get-Sha256Lower $levelDataPath) -ne [string]$buildInfo.levelDataSha256) {
    throw 'MNG Player level-data hash does not match build-info.json. Rebuild before training.'
}

if ($Resume) {
    if (-not (Test-Path -LiteralPath $runDirectory -PathType Container)) {
        throw "Cannot resume because the MNG Run does not exist: $runDirectory"
    }
}
elseif (Test-Path -LiteralPath $runDirectory) {
    throw "MNG Run already exists. Use a new revision or -Resume: $runDirectory"
}
if ($Resume -and -not [string]::IsNullOrWhiteSpace($InitializeFrom)) {
    throw 'Use either -Resume or -InitializeFrom, not both.'
}
if ($Stage -eq 'M1-AttackMoving' -or $Stage -eq 'M2-DefenseChoice' -or $Stage -eq 'M3-FallbackMatch') {
    if ([string]::IsNullOrWhiteSpace($InitializeFrom)) {
        throw "$Stage requires -InitializeFrom with its approved predecessor Run ID."
    }
    $initializationPattern = if ($Stage -eq 'M1-AttackMoving' -or $Stage -eq 'M2-DefenseChoice') {
        '^MNG_M1Attack-\d{8}-r\d{3}$'
    }
    else {
        '^MNG_M2Defense-\d{8}-r\d{3}$'
    }
    $initializationExample = if ($Stage -eq 'M1-AttackMoving' -or $Stage -eq 'M2-DefenseChoice') {
        'MNG_M1Attack-YYYYMMDD-rNNN'
    }
    else {
        'MNG_M2Defense-YYYYMMDD-rNNN'
    }
    if ($InitializeFrom -notmatch $initializationPattern) {
        throw "$Stage initialization Run ID must match ${initializationExample}: $InitializeFrom"
    }
    $initializeDirectory = Join-Path $resultsDirectory $InitializeFrom
    if (-not (Test-Path -LiteralPath $initializeDirectory -PathType Container)) {
        throw "M2 initialization Run does not exist: $initializeDirectory"
    }
}
elseif (-not [string]::IsNullOrWhiteSpace($InitializeFrom)) {
    throw '-InitializeFrom is currently supported only for M1-AttackMoving, M2-DefenseChoice and M3-FallbackMatch.'
}

$usedPort = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue |
    Where-Object { $_.LocalPort -eq $BasePort }
if ($null -ne $usedPort) {
    throw "ML-Agents base port $BasePort is already in use."
}

$trainerPath = 'C:\Users\USER\miniconda3\envs\mlagents\Scripts\mlagents-learn.exe'
if (-not (Test-Path -LiteralPath $trainerPath -PathType Leaf)) {
    $trainer = Get-Command mlagents-learn.exe -ErrorAction SilentlyContinue
    if ($null -eq $trainer) {
        throw 'mlagents-learn was not found in the existing ML-Agents environment.'
    }
    $trainerPath = $trainer.Source
}

New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
$trainerArguments = @(
    $configPath,
    '--run-id', $RunId,
    '--results-dir', $resultsDirectory,
    '--env', $executablePath,
    '--num-envs', [string]$NumEnvs,
    '--base-port', [string]$BasePort,
    '--seed', [string]$Seed,
    '--torch-device', $TorchDevice,
    '--timeout-wait', '120',
    '--no-graphics'
)
if ($Resume) {
    $trainerArguments += '--resume'
}
elseif (-not [string]::IsNullOrWhiteSpace($InitializeFrom)) {
    $trainerArguments += @('--initialize-from', $InitializeFrom)
}

Write-Host "Stage: $Stage"
Write-Host "Run ID: $RunId"
Write-Host "Workers: $NumEnvs"
Write-Host "Seed: $Seed"
Write-Host "Config: $configPath"
$targetDescription = if ($Stage -eq 'M3-FallbackMatch') {
    '100000 aggregate manager decisions or the externally enforced 180-minute wall limit'
}
elseif ($Stage -eq 'M1-AttackChoice' -or $Stage -eq 'M1-AttackMoving' -or $Stage -eq 'M2-DefenseChoice') {
    '20000 aggregate manager decisions or the externally enforced 60-minute wall limit'
}
else {
    '1000 aggregate manager decisions (connection smoke only)'
}
Write-Host "Target: $targetDescription"
if (-not [string]::IsNullOrWhiteSpace($InitializeFrom)) {
    Write-Host "Initialize from: $InitializeFrom"
}

if ($ValidateOnly) {
    Write-Host 'MNG training validation passed. Trainer was not started.'
    return
}

Push-Location $projectRoot
try {
    & $trainerPath @trainerArguments
    if ($LASTEXITCODE -ne 0) {
        throw "mlagents-learn exited with code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
