[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('R0Smoke', 'ParallelSmoke', 'SelfPlaySmoke')]
    [string]$Stage,

    [Parameter(Mandatory = $true)]
    [string]$RunId,

    [ValidateRange(1, 16)]
    [int]$NumEnvs = 1,

    [int]$Seed = 19001,

    [ValidateRange(1024, 65000)]
    [int]$BasePort = 5705,

    [ValidateSet('cuda', 'cpu')]
    [string]$TorchDevice = 'cuda',

    [switch]$Resume,

    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256Lower([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$isSelfPlay = $Stage -eq 'SelfPlaySmoke'
$buildDirectory = if ($isSelfPlay) {
    Join-Path $projectRoot 'Builds\MNG_MS\MS0_SelfPlay'
}
else {
    Join-Path $projectRoot 'Builds\MNG_MS\MS0_R0'
}
$executablePath = if ($isSelfPlay) {
    Join-Path $buildDirectory 'MNG_MS0_SelfPlay.exe'
}
else {
    Join-Path $buildDirectory 'MNG_MS0_R0.exe'
}
$dataDirectory = if ($isSelfPlay) { 'MNG_MS0_SelfPlay_Data' } else { 'MNG_MS0_R0_Data' }
$levelDataPath = Join-Path $buildDirectory "$dataDirectory\level0"
$buildInfoPath = Join-Path $buildDirectory 'build-info.json'
$configFile = switch ($Stage) {
    'R0Smoke' { 'MNG_MS0_R0Smoke.yaml' }
    'ParallelSmoke' { 'MNG_MS0_ParallelSmoke.yaml' }
    'SelfPlaySmoke' { 'MNG_MS0_SelfPlaySmoke.yaml' }
}
$configPath = Join-Path $projectRoot "Assets\_Soccer\Manager\Training\$configFile"
$resultsDirectory = Join-Path $projectRoot 'results'
$runDirectory = Join-Path $resultsDirectory $RunId
$logDirectory = Join-Path $projectRoot "Logs\MNG-MS\$RunId"
$evidenceDirectory = Join-Path $logDirectory 'workers'
$trainerLog = Join-Path $logDirectory 'trainer.log'
$requiredPattern = switch ($Stage) {
    'R0Smoke' { '^MNG_MS0R0-\d{8}-r\d{3}$' }
    'ParallelSmoke' { '^MNG_MS0Parallel(1|2|8|16)-\d{8}-r\d{3}$' }
    'SelfPlaySmoke' { '^MNG_MS0SelfPlay-\d{8}-r\d{3}$' }
}
if ($RunId -notmatch $requiredPattern) {
    throw "Run ID does not match the $Stage MS0 naming contract: $RunId"
}
foreach ($required in @($executablePath, $levelDataPath, $buildInfoPath, $configPath)) {
    if (-not (Test-Path -LiteralPath $required -PathType Leaf)) {
        throw "Required MS0 training artifact is missing: $required"
    }
}

$buildInfo = Get-Content -Raw -Encoding UTF8 -LiteralPath $buildInfoPath | ConvertFrom-Json
$configHashProperty = switch ($Stage) {
    'R0Smoke' { 'r0SmokeConfigSha256' }
    'ParallelSmoke' { 'parallelSmokeConfigSha256' }
    'SelfPlaySmoke' { 'selfPlaySmokeConfigSha256' }
}
if ((Get-Sha256Lower $configPath) -ne [string]$buildInfo.$configHashProperty) {
    throw 'MS0 YAML changed after the Player build. Rebuild before training.'
}
if ((Get-Sha256Lower $executablePath) -ne [string]$buildInfo.executableSha256) {
    throw 'MS0 executable hash does not match build-info.json.'
}
if ((Get-Sha256Lower $levelDataPath) -ne [string]$buildInfo.levelDataSha256) {
    throw 'MS0 level-data hash does not match build-info.json.'
}

if ($Resume) {
    if (-not (Test-Path -LiteralPath $runDirectory -PathType Container)) {
        throw "Cannot resume a missing MS0 Run: $runDirectory"
    }
}
elseif (Test-Path -LiteralPath $runDirectory) {
    throw "MS0 Run already exists. Use a new revision or -Resume: $runDirectory"
}

$lastPort = $BasePort + $NumEnvs - 1
$usedPorts = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object {
    $_.LocalPort -ge $BasePort -and $_.LocalPort -le $lastPort
}
if ($null -ne $usedPorts) {
    throw "An ML-Agents worker port in $BasePort..$lastPort is already listening."
}

$trainerPath = 'C:\Users\USER\miniconda3\envs\mlagents\Scripts\mlagents-learn.exe'
if (-not (Test-Path -LiteralPath $trainerPath -PathType Leaf)) {
    throw "The approved ML-Agents trainer is missing: $trainerPath"
}

Write-Host "Stage: $Stage"
Write-Host "Run ID: $RunId"
Write-Host "Workers: $NumEnvs"
Write-Host "Ports: $BasePort..$lastPort"
Write-Host "Config: $configPath"
Write-Host 'Budget: MS0 aggregate manager steps; never multiply max_steps by worker count.'
if ($ValidateOnly) {
    Write-Host 'MNG MS0 TRAINING VALIDATION PASS'
    return
}

New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
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
if ($Resume) { $trainerArguments += '--resume' }
$trainerArguments += @('--env-args', '-mngEvidenceDir', $evidenceDirectory)

Push-Location $projectRoot
try {
    & $trainerPath @trainerArguments 2>&1 | Tee-Object -FilePath $trainerLog -Append
    if ($LASTEXITCODE -ne 0) {
        throw "mlagents-learn exited with code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

