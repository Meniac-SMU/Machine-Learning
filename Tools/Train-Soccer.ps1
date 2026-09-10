[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('curriculum-l0', 'curriculum-l1', 'curriculum-l2', 'curriculum-l2-find', 'curriculum-l2-score', 'curriculum-l3', 'base-fallback', 'base-selfplay', 'attack', 'defense', 'press')]
    [string]$Profile,

    [Parameter(Mandatory = $true)]
    [string]$RunId,

    [ValidateSet(1, 2, 4, 8, 16, 32)]
    [int]$NumEnvs = 32,

    [int]$Seed = 12345,

    [Nullable[int]]$BasePort,

    [string]$TorchDevice = 'cuda',

    [switch]$Resume,

    [string]$InitializeFrom,

    [switch]$AllowFallbackOpponent,

    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256Lower([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $sha256 = [System.Security.Cryptography.SHA256]::Create()
        try {
            return ([System.BitConverter]::ToString($sha256.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
        }
        finally {
            $sha256.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

if ($Resume -and -not [string]::IsNullOrWhiteSpace($InitializeFrom)) {
    throw 'Use either -Resume or -InitializeFrom, not both.'
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$executablePath = Join-Path $projectRoot 'Builds\SoccerTraining\SoccerTraining.exe'
$manifestPath = Join-Path $projectRoot 'Builds\SoccerTraining\training-profiles.json'
$resultsDirectory = Join-Path $projectRoot 'results'
$runDirectory = Join-Path $resultsDirectory $RunId

if (-not (Test-Path -LiteralPath $executablePath -PathType Leaf)) {
    throw "Training executable is missing. Run Tools\Build-SoccerTraining.ps1 first: $executablePath"
}

if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Training profile manifest is missing. Rebuild the training player: $manifestPath"
}

$manifest = Get-Content -Raw -Encoding UTF8 -LiteralPath $manifestPath | ConvertFrom-Json
if ([int]$manifest.schemaVersion -lt 2) {
    throw 'The training profile manifest predates per-profile max_steps metadata. Rebuild the training player.'
}
$selectedProfile = @($manifest.profiles | Where-Object { $_.key -eq $Profile })
if ($selectedProfile.Count -ne 1) {
    throw "Profile '$Profile' is not present exactly once in the build manifest. Rebuild the training player."
}
$selectedProfile = $selectedProfile[0]

if (-not $selectedProfile.supportsTraining) {
    throw "Profile '$Profile' is evaluation-only and cannot be passed to mlagents-learn."
}

if ($selectedProfile.requiresNavyModel -and -not $AllowFallbackOpponent) {
    if (-not $selectedProfile.navyModelConfiguredInBuild) {
        throw "Profile '$Profile' requires an approved Base v2 Navy model inside this Windows Build. Register it in BaseTeamDefinition and rebuild, or explicitly use -AllowFallbackOpponent for an intentional fallback-opponent experiment."
    }
}

$expectedRunId = '^{0}-\d{{8}}-r\d{{3}}$' -f [Regex]::Escape([string]$selectedProfile.runIdPrefix)
if ($RunId -notmatch $expectedRunId) {
    throw "Run ID must match $($selectedProfile.runIdPrefix)-YYYYMMDD-rNNN. Received: $RunId"
}

$configPath = Join-Path $projectRoot ([string]$selectedProfile.trainerConfig).Replace('/', '\')
if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
    throw "Trainer config is missing: $configPath"
}

$configHash = Get-Sha256Lower $configPath
if ($configHash -ne [string]$selectedProfile.trainerConfigSha256) {
    throw 'The YAML changed after the executable manifest was generated. Rebuild so the shared build and config snapshot agree.'
}

if ($Resume) {
    if (-not (Test-Path -LiteralPath $runDirectory -PathType Container)) {
        throw "Cannot resume because the run directory does not exist: $runDirectory"
    }
} elseif (Test-Path -LiteralPath $runDirectory) {
    throw "Run ID already exists. Use a new revision, or use -Resume only for the identical interrupted run: $runDirectory"
}

if (-not [string]::IsNullOrWhiteSpace($InitializeFrom)) {
    $sourceRunDirectory = Join-Path $resultsDirectory $InitializeFrom
    if (-not (Test-Path -LiteralPath $sourceRunDirectory -PathType Container)) {
        throw "Initialize-from run does not exist: $sourceRunDirectory"
    }
}

$selectedBasePort = if ($null -ne $BasePort) { $BasePort.Value } else { [int]$selectedProfile.defaultBasePort }
if ($selectedBasePort -lt 1024 -or ($selectedBasePort + $NumEnvs - 1) -gt 65535) {
    throw "Invalid port range: $selectedBasePort through $($selectedBasePort + $NumEnvs - 1)"
}

$usedPorts = @(Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object {
    $_.LocalPort -ge $selectedBasePort -and $_.LocalPort -lt ($selectedBasePort + $NumEnvs)
})
if ($usedPorts.Count -gt 0) {
    $portList = ($usedPorts | Select-Object -ExpandProperty LocalPort -Unique | Sort-Object) -join ', '
    throw "One or more ML-Agents ports are already in use: $portList"
}

$trainer = Get-Command mlagents-learn.exe -ErrorAction SilentlyContinue
if ($null -eq $trainer) {
    $trainer = Get-Command mlagents-learn -ErrorAction SilentlyContinue
}
if ($null -eq $trainer) {
    throw 'mlagents-learn was not found. Activate the existing mlagents Conda environment first.'
}

New-Item -ItemType Directory -Path $resultsDirectory -Force | Out-Null

$trainerArguments = @(
    $configPath,
    '--run-id', $RunId,
    '--results-dir', $resultsDirectory,
    '--env', $executablePath,
    '--num-envs', [string]$NumEnvs,
    '--base-port', [string]$selectedBasePort,
    '--seed', [string]$Seed,
    '--torch-device', $TorchDevice,
    '--timeout-wait', '120',
    '--no-graphics'
)

if ($Resume) {
    $trainerArguments += '--resume'
}
if (-not [string]::IsNullOrWhiteSpace($InitializeFrom)) {
    $trainerArguments += @('--initialize-from', $InitializeFrom)
}

# Everything after --env-args is forwarded to every Unity worker, so it must stay last.
$trainerArguments += @('--env-args', '--training-profile', $Profile, '-job-worker-count', '1')

Write-Host "Profile: $Profile"
Write-Host "Run ID: $RunId"
Write-Host "Workers: $NumEnvs executable instances"
Write-Host "Ports: $selectedBasePort through $($selectedBasePort + $NumEnvs - 1)"
Write-Host "Config: $configPath"
Write-Host "Total target steps: $($selectedProfile.maxSteps) (aggregate across all workers)"

if ($ValidateOnly) {
    Write-Host 'Validation passed. mlagents-learn was not started.'
    return
}

Push-Location $projectRoot
try {
    & $trainer.Source @trainerArguments
    if ($LASTEXITCODE -ne 0) {
        throw "mlagents-learn exited with code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
