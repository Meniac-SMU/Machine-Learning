[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RunId,
    [ValidateSet(16)]
    [int]$NumEnvs = 16,
    [int]$Seed = 191001,
    [ValidateRange(1024, 64985)]
    [int]$BasePort = 5800,
    [ValidateSet('cuda', 'cpu')]
    [string]$TorchDevice = 'cuda',
    [ValidateSet('Base', 'PassRepair', 'PassFineTune', 'PassPolish', 'PassContinuation', 'PassDecisionPolish')]
    [string]$TrainingProfile = 'Base',
    [switch]$Resume,
    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($RunId -notmatch '^MNG_MS1-\d{8}-r\d{3}$') {
    throw "Run ID must match MNG_MS1-YYYYMMDD-rNNN. Received: $RunId"
}
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildRoot = Join-Path $projectRoot 'Builds\MNG_MS\MS1'
$executable = Join-Path $buildRoot 'MNG_MS1.exe'
$level = Join-Path $buildRoot 'MNG_MS1_Data\level0'
$buildInfoPath = Join-Path $buildRoot 'build-info.json'
$config = switch ($TrainingProfile) {
    'PassRepair' { Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_MS1_PassRepair.yaml' }
    'PassFineTune' { Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_MS1_PassFineTune.yaml' }
    'PassPolish' { Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_MS1_PassPolish.yaml' }
    'PassContinuation' { Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_MS1_PassContinuation.yaml' }
    'PassDecisionPolish' { Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_MS1_PassDecisionPolish.yaml' }
    default { Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_MS1.yaml' }
}
$protocol = Join-Path $projectRoot 'Assets\_Soccer\Manager\Evaluation\MNG_MS1_Protocol_v8.json'
$sourceManifest = Join-Path $projectRoot 'Logs\MNG-MS\MS1-final-source-v19\source-sha256.json'
$managedAssembly = Join-Path $buildRoot 'MNG_MS1_Data\Managed\MNG.Runtime.dll'
$runDirectory = Join-Path $projectRoot "results\$RunId"
$logDirectory = Join-Path $projectRoot "Logs\MNG-MS\$RunId"
$evidenceDirectory = Join-Path $logDirectory 'workers'
$trainerLog = Join-Path $logDirectory 'trainer.log'
$trainer = 'C:\Users\USER\miniconda3\envs\mlagents\Scripts\mlagents-learn.exe'

function Get-Sha256Lower([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}
foreach ($path in @($executable, $level, $managedAssembly, $buildInfoPath, $config, $protocol, $sourceManifest, $trainer)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing MS1 artifact: $path" }
}
$buildInfo = Get-Content -LiteralPath $buildInfoPath -Raw -Encoding UTF8 | ConvertFrom-Json
$expectedConfigSha = if ($TrainingProfile -eq 'PassRepair') {
    [string]$buildInfo.passRepairConfigSha256
}
elseif ($TrainingProfile -eq 'PassFineTune') {
    [string]$buildInfo.passFineTuneConfigSha256
}
elseif ($TrainingProfile -eq 'PassPolish') {
    [string]$buildInfo.passPolishConfigSha256
}
elseif ($TrainingProfile -eq 'PassContinuation') {
    [string]$buildInfo.passContinuationConfigSha256
}
elseif ($TrainingProfile -eq 'PassDecisionPolish') {
    [string]$buildInfo.passDecisionPolishConfigSha256
}
else { [string]$buildInfo.configSha256 }
if ([string]::IsNullOrWhiteSpace($expectedConfigSha) -or
    (Get-Sha256Lower $config) -ne $expectedConfigSha) {
    throw "MS1 $TrainingProfile YAML changed after build."
}
if ((Get-Sha256Lower $protocol) -ne [string]$buildInfo.protocolSha256) { throw 'MS1 protocol changed after build.' }
if ((Get-Sha256Lower $sourceManifest) -ne [string]$buildInfo.sourceManifestSha256) {
    throw 'MS1 source manifest changed after build.'
}
if ((Get-Sha256Lower $executable) -ne [string]$buildInfo.executableSha256) { throw 'MS1 executable hash mismatch.' }
if ((Get-Sha256Lower $level) -ne [string]$buildInfo.levelDataSha256) { throw 'MS1 level hash mismatch.' }
if ((Get-Sha256Lower $managedAssembly) -ne [string]$buildInfo.managedAssemblySha256) {
    throw 'MS1 managed gameplay assembly hash mismatch.'
}
if ($Resume -and $TrainingProfile -ne 'Base') {
    throw "$TrainingProfile uses a frozen initialization and must start as a new Run."
}
if ($Resume) {
    if (-not (Test-Path -LiteralPath $runDirectory -PathType Container)) { throw 'Cannot resume a missing MS1 Run.' }
}
elseif (Test-Path -LiteralPath $runDirectory) { throw 'MS1 Run already exists; use a new revision or -Resume.' }

$lastPort = $BasePort + 15
$used = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object {
    $_.LocalPort -ge $BasePort -and $_.LocalPort -le $lastPort
}
if ($null -ne $used) { throw "MS1 port range $BasePort..$lastPort is not free." }
Write-Host "MS1 Run: $RunId"
Write-Host "Workers: 16"
Write-Host "Ports: $BasePort..$lastPort"
Write-Host "Training profile: $TrainingProfile"
Write-Host $(if ($TrainingProfile -eq 'PassRepair') {
    'Budget: aggregate 80000 pass-bootstrap steps; do not multiply by 16.'
} elseif ($TrainingProfile -eq 'PassFineTune') {
    'Budget: aggregate 60000 six-command pass fine-tune steps; do not multiply by 16.'
} elseif ($TrainingProfile -eq 'PassPolish') {
    'Budget: aggregate 30000 six-command pass-polish steps; do not multiply by 16.'
} elseif ($TrainingProfile -eq 'PassContinuation') {
    'Budget: aggregate 15000 six-command pass continuation steps; do not multiply by 16.'
} elseif ($TrainingProfile -eq 'PassDecisionPolish') {
    'Budget: aggregate 20000 explicit pass-decision polish steps; do not multiply by 16.'
} else {
    'Budget: aggregate 100000 manager steps with a 120000 conditional ceiling; do not multiply by 16.'
})
if ($ValidateOnly) { Write-Host 'MNG MS1 TRAINING VALIDATION PASS'; return }

New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
$arguments = @(
    $config,
    '--run-id', $RunId,
    '--results-dir', (Join-Path $projectRoot 'results'),
    '--env', $executable,
    '--num-envs', '16',
    '--base-port', [string]$BasePort,
    '--seed', [string]$Seed,
    '--torch-device', $TorchDevice,
    '--timeout-wait', '120',
    '--no-graphics'
)
if ($Resume) { $arguments += '--resume' }
$environmentArguments = @('-mngEvidenceDir', $evidenceDirectory)
if ($TrainingProfile -eq 'PassRepair' -or $TrainingProfile -eq 'PassFineTune' -or
    $TrainingProfile -eq 'PassPolish' -or $TrainingProfile -eq 'PassContinuation' -or
    $TrainingProfile -eq 'PassDecisionPolish') {
    $environmentArguments += @('-mngMS1PassRepair', 'true')
}
if ($TrainingProfile -eq 'PassRepair') {
    $environmentArguments += @('-mngMS1PassPriorityMask', 'true')
}
if ($TrainingProfile -eq 'PassDecisionPolish') {
    $environmentArguments += @('-mngMS1LearnPassChoice', 'true')
}
$arguments += @('--env-args') + $environmentArguments
Push-Location $projectRoot
try {
    & $trainer @arguments 2>&1 | Tee-Object -FilePath $trainerLog -Append
    if ($LASTEXITCODE -ne 0) { throw "MS1 trainer exited with code $LASTEXITCODE." }
}
finally { Pop-Location }
