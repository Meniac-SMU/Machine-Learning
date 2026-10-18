[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$RunName,
    [Parameter(Mandatory=$true)][string]$Segment,
    [Parameter(Mandatory=$true)][int]$StopAt,
    [string]$Build='Builds/Manager5v5/p9-selfplay-frozen-20261014/Soccer5v5Learning.exe',
    [string]$Config='docs/soccer/rebuild-5v5/p9-selfplay-1200k.yaml',
    [string]$EvidenceDirectory='Logs/Manager5v5/p9-selfplay-20261014',
    [ValidateSet('attack-keeper-v2-reception','tactics-v3-expansion')][string]$PlayerRuntime='attack-keeper-v2-reception',
    [int]$Workers=32,
    [int]$Seed=19014,
    [ValidateSet('cpu','cuda')][string]$Device='cpu',
    [switch]$Resume
)
$ErrorActionPreference='Stop'
$repo=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if($RunName -notmatch '^(P9-[a-zA-Z0-9_-]+|MNG5_V2-[0-9]{8}-r[0-9]{3})$' -or $Segment -notmatch '^[a-zA-Z0-9_-]+$'){throw 'Use an isolated P9 run and a new segment name.'}
if($StopAt -lt 0 -or $StopAt -gt 1200000 -or $Workers -ne 32){throw 'Expected 32 environments and at most 1,200,000 decisions.'}
function LocalPath([string]$value) {
    $resolved=[IO.Path]::GetFullPath((Join-Path $repo $value))
    if(!$resolved.StartsWith($repo+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'All artifacts must stay in this project.'}
    return $resolved
}
$exe=LocalPath $Build
$configPath=LocalPath $Config
$run=LocalPath "results/Manager5v5/$RunName"
$base=LocalPath "$EvidenceDirectory/$RunName"
$evidence=Join-Path $base $Segment
if(Test-Path -LiteralPath $evidence){throw 'Preserve existing segment evidence.'}
$payload=@(Get-ChildItem -LiteralPath (Split-Path $exe) -File -Recurse | Where-Object {
    $relative=[IO.Path]::GetRelativePath((Split-Path $exe),$_.FullName).Replace('\','/')
    $relative -notin @('build-usage.jsonl','Soccer5v5Learning_Data/ML-Agents/Timers/FiveLearning_timers.json')
} | Sort-Object FullName | ForEach-Object { @{path=[IO.Path]::GetRelativePath((Split-Path $exe),$_.FullName).Replace('\','/');sha256=(Get-FileHash -LiteralPath $_.FullName).Hash} })
$contract=[ordered]@{contract='MNG5-OBS676-A8-R1-M2-v2';run=$RunName;seed=$Seed;workers=$Workers;device=$Device;build=$exe;config=$configPath;configSha256=(Get-FileHash -LiteralPath $configPath).Hash;trainerSha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'five_selfplay_train.py')).Hash;payload=$payload}
$contractJson=$contract | ConvertTo-Json -Depth 8
$identity=Join-Path $base 'identity.json'
if($Resume){
    if(!(Test-Path -LiteralPath $identity) -or !(Test-Path -LiteralPath "$run/MNG5_Manager_v2/checkpoint.pt") -or !(Test-Path -LiteralPath "$run/MNG5_Manager_v2/five-pool.pt")){throw 'Resume requires original identity, policy/optimizer and pool.'}
    $saved=Get-Content -LiteralPath $identity -Raw | ConvertFrom-Json
    $savedRuntime=if($saved.playerRuntime){$saved.playerRuntime}else{'attack-keeper-v2-reception'}
    if($savedRuntime -ne $PlayerRuntime){throw 'Resume player runtime changed.'}
    foreach($key in @('contract','run','seed','workers','device','build','config','configSha256','trainerSha256')) {
        if($saved.$key -ne $contract[$key]){throw "Resume identity changed: $key"}
    }
    if($saved.payload.Count -ne $payload.Count){throw 'Resume payload file count changed.'}
    $savedHashes=@{}
    foreach($file in $saved.payload){$savedHashes[$file.path]=$file.sha256}
    foreach($file in $payload){if($savedHashes[$file.path] -ne $file.sha256){throw "Resume payload changed: $($file.path)"}}
} else {
    if((Test-Path -LiteralPath $run) -or (Test-Path -LiteralPath $identity)){throw 'Start from scratch in a fresh run.'}
}
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
if(!$Resume){$contract.playerRuntime=$PlayerRuntime;[IO.File]::WriteAllText($identity,($contract | ConvertTo-Json -Depth 8))}
& (Join-Path $PSScriptRoot 'Save-FiveSource.ps1') -OutputPath (Join-Path $evidence 'source.zip')
$playerLogs=Join-Path $evidence 'players'
$trace=Join-Path $evidence 'traces'
New-Item -ItemType Directory -Path $playerLogs,$trace | Out-Null
$env:FIVE_EVIDENCE=$evidence
$env:FIVE_EXPECTED_RUNTIME=$PlayerRuntime
$env:FIVE_STOP_AT=[string]$StopAt
$env:FIVE_PLAYER_LOGS=$playerLogs
$env:OPENBLAS_NUM_THREADS='1'
$env:OMP_NUM_THREADS='1'
$env:MKL_NUM_THREADS='1'
$env:PYTHONUNBUFFERED='1'
if($Device -eq 'cpu'){$env:CUDA_VISIBLE_DEVICES='-1'}
else{throw 'P9 CUDA execution is quarantined after the native driver failure; use the validated CPU route.'}
$python='C:\Users\USER\miniconda3\envs\mlagents\python.exe'
$arguments=@('-X','faulthandler',(Join-Path $PSScriptRoot 'five_selfplay_train.py'),$configPath,'--env',$exe,'--run-id',$RunName,'--results-dir',(LocalPath 'results/Manager5v5'),'--num-envs',[string]$Workers,'--seed',[string]$Seed,'--base-port','5605','--no-graphics','--torch-device',$Device,'--time-scale','100','--max-lifetime-restarts','0')
if($Resume){$arguments+='--resume'}
$arguments+=@('--env-args','--five-p9-trace-dir',$trace,'-job-worker-count','1')
$start=[DateTime]::UtcNow
@{run=$RunName;segment=$Segment;stopAt=$StopAt;resume=[bool]$Resume;startedUtc=$start.ToString('o');arguments=$arguments} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $evidence 'launch.json') -Encoding utf8
$quoted=@($arguments | ForEach-Object {'"'+$_+'"'})
$process=Start-Process -FilePath $python -ArgumentList $quoted -WorkingDirectory $repo -WindowStyle Hidden -RedirectStandardOutput (Join-Path $evidence 'stdout.log') -RedirectStandardError (Join-Path $evidence 'stderr.log') -PassThru
Write-Output ('PYTHON_PID='+$process.Id)
$process.WaitForExit()
@{exitCode=$process.ExitCode;startedUtc=$start.ToString('o');endedUtc=[DateTime]::UtcNow.ToString('o');stopAt=$StopAt;resume=[bool]$Resume} | ConvertTo-Json | Set-Content (Join-Path $evidence 'exit.json') -Encoding utf8
if($process.ExitCode -ne 0){throw "P9 process failed; preserve $evidence and do not resume without reviewing the failure."}
Get-Content (Join-Path $evidence 'process-result.json')
