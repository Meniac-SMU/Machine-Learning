[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RunId,
    [ValidateSet(32)][int]$NumEnvs = 32,
    [int]$Seed = 193001,
    [ValidateRange(1024,64985)][int]$BasePort = 6200,
    [ValidateSet('cuda','cpu')][string]$TorchDevice = 'cuda',
    [switch]$Resume,
    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($RunId -notmatch '^MNG_MS3-\d{8}-r\d{3}$') {
    throw "Run ID must match MNG_MS3-YYYYMMDD-rNNN. Received: $RunId"
}

function Get-Sha256Lower([string]$Path) {
    (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildRoot = Join-Path $projectRoot 'Builds\MNG_MS\MS3'
$executable = Join-Path $buildRoot 'MNG_MS3.exe'
$level = Join-Path $buildRoot 'MNG_MS3_Data\level0'
$managedAssembly = Join-Path $buildRoot 'MNG_MS3_Data\Managed\MNG.Runtime.dll'
$buildInfoPath = Join-Path $buildRoot 'build-info.json'
$config = Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_MS3.yaml'
$protocol = Join-Path $projectRoot 'Assets\_Soccer\Manager\Evaluation\MNG_MS3_Protocol_v1.json'
$sourceManifest = Join-Path $projectRoot 'Logs\MNG-MS\MS3-source-20260921\source-sha256.json'
$initialization = Join-Path $projectRoot 'results\MNG_MS2-20260921-r005\MNG_Manager\MNG_Manager-99968.pt'
$runDirectory = Join-Path $projectRoot "results\$RunId"
$logDirectory = Join-Path $projectRoot "Logs\MNG-MS\$RunId"
$evidenceDirectory = Join-Path $logDirectory 'workers'
$trainerLog = Join-Path $logDirectory 'trainer.log'
$trainer = 'C:\Users\USER\miniconda3\envs\mlagents\Scripts\mlagents-learn.exe'

foreach ($path in @($executable,$level,$managedAssembly,$buildInfoPath,$config,$protocol,$sourceManifest,$initialization,$trainer)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing MS3 artifact: $path" }
}
$buildInfo = Get-Content -LiteralPath $buildInfoPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ((Get-Sha256Lower $config) -ne [string]$buildInfo.configSha256) { throw 'MS3 YAML changed after build.' }
if ((Get-Sha256Lower $protocol) -ne [string]$buildInfo.protocolSha256) { throw 'MS3 protocol changed after build.' }
if ((Get-Sha256Lower $sourceManifest) -ne [string]$buildInfo.sourceManifestSha256) { throw 'MS3 source manifest changed after build.' }
if ((Get-Sha256Lower $initialization) -ne [string]$buildInfo.initializationSha256) { throw 'MS3 initialization changed after build.' }
if ((Get-Sha256Lower $executable) -ne [string]$buildInfo.executableSha256) { throw 'MS3 executable hash mismatch.' }
if ((Get-Sha256Lower $level) -ne [string]$buildInfo.levelDataSha256) { throw 'MS3 level hash mismatch.' }
if ((Get-Sha256Lower $managedAssembly) -ne [string]$buildInfo.managedAssemblySha256) { throw 'MS3 runtime assembly hash mismatch.' }

if ($Resume) {
    if (-not (Test-Path -LiteralPath $runDirectory -PathType Container)) { throw 'Cannot resume a missing MS3 Run.' }
}
elseif (Test-Path -LiteralPath $runDirectory) { throw 'MS3 Run already exists; use a new revision or -Resume.' }

$lastPort = $BasePort + 31
$used = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object {
    $_.LocalPort -ge $BasePort -and $_.LocalPort -le $lastPort
}
if ($null -ne $used) { throw "MS3 port range $BasePort..$lastPort is not free." }

Write-Host "MS3 Run: $RunId"
Write-Host 'Workers: 32'
Write-Host "Ports: $BasePort..$lastPort"
Write-Host 'Mode: PPO versus PPO self-play, TeamId Red=0 and Navy=1'
Write-Host 'Budget: minimum 300000, maximum 1000000 aggregate manager steps.'
Write-Host 'Monitoring: 50000; detailed frozen evaluation: 100000.'
Write-Host 'Initialization: provisional MS2 P0 step99968 policy weights with a new optimizer.'
if ($ValidateOnly) { Write-Host 'MNG MS3 TRAINING VALIDATION PASS'; return }

New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
$arguments = @(
    $config,'--run-id',$RunId,'--results-dir',(Join-Path $projectRoot 'results'),
    '--env',$executable,'--num-envs','32','--base-port',[string]$BasePort,
    '--seed',[string]$Seed,'--torch-device',$TorchDevice,'--timeout-wait','120','--no-graphics',
    '--debug'
)
if ($Resume) { $arguments += '--resume' }
$arguments += @(
    '--env-args','-mngEvidenceDir',$evidenceDirectory,'-mngPolicyAssistMode','none'
)
Push-Location $projectRoot
try {
    & $trainer @arguments 2>&1 | Tee-Object -FilePath $trainerLog -Append
    if ($LASTEXITCODE -ne 0) { throw "MS3 trainer exited with code $LASTEXITCODE." }
}
finally { Pop-Location }
