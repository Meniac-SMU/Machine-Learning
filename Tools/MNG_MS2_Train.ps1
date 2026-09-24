[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RunId,
    [ValidateSet(32)]
    [int]$NumEnvs = 32,
    [int]$Seed = 192001,
    [ValidateRange(1024, 64985)]
    [int]$BasePort = 5900,
    [ValidateSet('cuda', 'cpu')]
    [string]$TorchDevice = 'cuda',
    [switch]$Resume,
    [switch]$ValidateOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($RunId -notmatch '^MNG_MS2-\d{8}-r\d{3}$') {
    throw "Run ID must match MNG_MS2-YYYYMMDD-rNNN. Received: $RunId"
}

function Get-Sha256Lower([string]$Path) {
    return (Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash.ToLowerInvariant()
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$buildRoot = Join-Path $projectRoot 'Builds\MNG_MS\MS2'
$executable = Join-Path $buildRoot 'MNG_MS2.exe'
$level = Join-Path $buildRoot 'MNG_MS2_Data\level0'
$managedAssembly = Join-Path $buildRoot 'MNG_MS2_Data\Managed\MNG.Runtime.dll'
$buildInfoPath = Join-Path $buildRoot 'build-info.json'
$config = Join-Path $projectRoot 'Assets\_Soccer\Manager\Training\MNG_MS2.yaml'
$protocol = Join-Path $projectRoot 'Assets\_Soccer\Manager\Evaluation\MNG_MS2_Protocol_v1.json'
$sourceManifest = Join-Path $projectRoot 'Logs\MNG-MS\MS2-p0-source-20260921\source-sha256.json'
$initialization = Join-Path $projectRoot 'results\MNG_MS1-20260920-r007\MNG_Manager\MNG_Manager-7443.pt'
$runDirectory = Join-Path $projectRoot "results\$RunId"
$logDirectory = Join-Path $projectRoot "Logs\MNG-MS\$RunId"
$evidenceDirectory = Join-Path $logDirectory 'workers'
$trainerLog = Join-Path $logDirectory 'trainer.log'
$trainer = 'C:\Users\USER\miniconda3\envs\mlagents\Scripts\mlagents-learn.exe'

foreach ($path in @($executable,$level,$managedAssembly,$buildInfoPath,$config,$protocol,$sourceManifest,$initialization,$trainer)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing MS2 artifact: $path" }
}
$buildInfo = Get-Content -LiteralPath $buildInfoPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ((Get-Sha256Lower $config) -ne [string]$buildInfo.configSha256) { throw 'MS2 YAML changed after build.' }
if ((Get-Sha256Lower $protocol) -ne [string]$buildInfo.protocolSha256) { throw 'MS2 protocol changed after build.' }
if ((Get-Sha256Lower $sourceManifest) -ne [string]$buildInfo.sourceManifestSha256) { throw 'MS2 source manifest changed after build.' }
if ((Get-Sha256Lower $initialization) -ne [string]$buildInfo.initializationSha256) { throw 'MS2 initialization changed after build.' }
if ((Get-Sha256Lower $executable) -ne [string]$buildInfo.executableSha256) { throw 'MS2 executable hash mismatch.' }
if ((Get-Sha256Lower $level) -ne [string]$buildInfo.levelDataSha256) { throw 'MS2 level hash mismatch.' }
if ((Get-Sha256Lower $managedAssembly) -ne [string]$buildInfo.managedAssemblySha256) { throw 'MS2 runtime assembly hash mismatch.' }
if ($Resume) {
    if (-not (Test-Path -LiteralPath $runDirectory -PathType Container)) { throw 'Cannot resume a missing MS2 Run.' }
}
elseif (Test-Path -LiteralPath $runDirectory) { throw 'MS2 Run already exists; use a new revision or -Resume.' }

$lastPort = $BasePort + 31
$used = Get-NetTCPConnection -State Listen -ErrorAction SilentlyContinue | Where-Object {
    $_.LocalPort -ge $BasePort -and $_.LocalPort -le $lastPort
}
if ($null -ne $used) { throw "MS2 port range $BasePort..$lastPort is not free." }
Write-Host "MS2 Run: $RunId"
Write-Host 'Workers: 32'
Write-Host "Ports: $BasePort..$lastPort"
Write-Host 'Opponent: R0-Full (1.0 movement, 0.5 second decisions)'
Write-Host 'Budget: aggregate maximum 500000 manager steps; do not multiply by 32.'
Write-Host 'Initialization: selected MS1 step 7443 policy weights with a new optimizer.'
if ($ValidateOnly) { Write-Host 'MNG MS2 TRAINING VALIDATION PASS'; return }

New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null
$arguments = @(
    $config,'--run-id',$RunId,'--results-dir',(Join-Path $projectRoot 'results'),
    '--env',$executable,'--num-envs','32','--base-port',[string]$BasePort,
    '--seed',[string]$Seed,'--torch-device',$TorchDevice,'--timeout-wait','120','--no-graphics',
    '--env-args','-mngEvidenceDir',$evidenceDirectory,'-mngPolicyAssistMode','none'
)
if ($Resume) { $arguments += '--resume' }
Push-Location $projectRoot
try {
    & $trainer @arguments 2>&1 | Tee-Object -FilePath $trainerLog -Append
    if ($LASTEXITCODE -ne 0) { throw "MS2 trainer exited with code $LASTEXITCODE." }
}
finally { Pop-Location }
