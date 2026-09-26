[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^MNG_(MS[23]V2|MS3V3)-\d{8}-r\d{3}$')][string]$RunId,
    [Parameter(Mandatory)][ValidateRange(100000,2000000)][ValidateScript({$_ % 100000 -eq 0})][int]$StopAt,
    [Parameter(Mandatory)][string]$TrainingBuild,
    [ValidateRange(100000,2000000)][int]$MaximumSteps=200000,
    [int]$Seed=20260923,
    [switch]$Resume,
    [ValidatePattern('^[a-zA-Z0-9][a-zA-Z0-9-]{0,63}$')][string]$SegmentSuffix,
    [string]$InitialPolicy,
    [string]$HistoricalPolicy,
    [string]$PreparationGate,
    [string]$ExtensionGate,
    [string]$EntryRepairGate,
    [switch]$ValidateOnly
)
Set-StrictMode -Version Latest
$ErrorActionPreference='Stop'
$root=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$selfPlay=$RunId -like 'MNG_MS3V*-*'
$isV3=$RunId -like 'MNG_MS3V3-*'
$generation=if($isV3){'v3'}else{'v2'}
$baseActorSha=$null
$baseSnapshotSha=$null
if($isV3){
    if($HistoricalPolicy){throw 'v3 forbids external historical policies'}
    $registry=Get-Content (Join-Path $root 'docs/soccer/training/ms-v3-model-registry.json') -Raw | ConvertFrom-Json
    $baseActorSha=$registry.baseActor.sha256
}
$stage=if($selfPlay){'MS3V2'}else{'MS2V2'}
if (!$selfPlay -and $StopAt -gt 200000) { throw 'MS2 preparation hard cap is 200k.' }
if($StopAt -gt $MaximumSteps){throw 'StopAt exceeds the declared experiment maximum.'}
$build=(Resolve-Path -LiteralPath $TrainingBuild).Path
$info=Get-Content (Join-Path $build 'build-info.json') -Raw | ConvertFrom-Json
$currentSchema=Get-Content (Join-Path $root "docs/soccer/training/ms-$generation-schema.json") -Raw | ConvertFrom-Json
if($info.environmentRevision -ne $currentSchema.environmentRevision){throw 'Training build uses an older gameplay contract; prepare a new validated experiment'}
function Sha($p){(Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLowerInvariant()}
$exe=Join-Path $build "MNG_$stage.exe"
if ($info.schema -ne 'MNG-OBS-v2-244' -or (Sha $exe) -ne $info.executableSha256 -or
    (Sha (Join-Path $build "MNG_${stage}_Data/Managed/MNG.Runtime.dll")) -ne $info.runtimeSha256 -or
    (Sha (Join-Path $build "MNG_${stage}_Data/level0")) -ne $info.levelSha256) { throw 'Formal build integrity failure' }
if(!$Resume -and $selfPlay){
    if(!$InitialPolicy -or !$PreparationGate){throw 'MS3 needs an approved actor-only policy and preparation gate'}
    $gate=Get-Content -LiteralPath $PreparationGate -Raw | ConvertFrom-Json
    if($isV3){
        if($gate.generation -ne 'v3' -or $gate.baseActorSourceSha -ne $baseActorSha -or !$gate.baseSnapshotSha){throw 'Invalid v3 initialization lineage'}
        $baseSnapshotSha=$gate.baseSnapshotSha
    }
    if(!$gate.passed -or $gate.initialPolicySha -ne (Sha $InitialPolicy) -or $gate.runtimeSha -ne $info.runtimeSha256){throw 'Preparation gate failed'}
    if($gate.PSObject.Properties.Name -contains 'maximumSteps' -and $MaximumSteps -gt $gate.maximumSteps){throw 'Preparation gate maximum exceeded'}
    if($gate.PSObject.Properties.Name -contains 'historicalPolicySha'){
        if(!$HistoricalPolicy -or (Sha $HistoricalPolicy) -ne $gate.historicalPolicySha){throw 'Historical reference mismatch'}
    } elseif($HistoricalPolicy){throw 'Historical policy must be pinned in the preparation gate'}
}
$evidence=Join-Path $root "Logs/MNG-Rebuild/$RunId"
$result=Join-Path $root "results/$RunId"
$adapter=Sha (Join-Path $root 'Tools/mng_v2_learn.py')
$entry=Sha (Join-Path $root 'Tools/mng_v2_formal_learn.py')
if ($Resume) {
    if(Test-Path (Join-Path $evidence 'STOP')){throw 'Run has an unresolved STOP marker; do not resume'}
    $prior=Get-Content (Join-Path $evidence 'manifest.json') -Raw | ConvertFrom-Json
    if($prior.entrySha -ne $entry){
        if(!$EntryRepairGate){throw 'Changed entry requires a validated repair gate'}
        $repair=Get-Content -LiteralPath $EntryRepairGate -Raw | ConvertFrom-Json
        if(!$repair.passed -or $repair.runId -ne $RunId -or $repair.oldEntrySha -ne $prior.entrySha -or
            $repair.newEntrySha -ne $entry -or $repair.adapterSha -ne $adapter -or
            $repair.originalManifestSha -ne (Sha (Join-Path $evidence 'manifest.json')) -or
            $repair.launcherSha -ne (Sha $PSCommandPath)){throw 'Invalid entry repair gate'}
    }
    if($prior.adapterSha -ne $adapter -or $prior.runtimeSha -ne $info.runtimeSha256 -or $prior.seed -ne $Seed){throw 'Resume source or experiment contract mismatch'}
    if($prior.maximumSteps -ne $MaximumSteps){
        if(!$ExtensionGate){throw 'Maximum extension requires an explicit validated extension gate'}
        $extension=Get-Content -LiteralPath $ExtensionGate -Raw | ConvertFrom-Json
        if(!$extension.passed -or $extension.runId -ne $RunId -or $extension.fromMaximum -ne $prior.maximumSteps -or
            $extension.toMaximum -ne $MaximumSteps -or $MaximumSteps -le $prior.maximumSteps -or
            $extension.originalManifestSha -ne (Sha (Join-Path $evidence 'manifest.json')) -or
            $extension.adapterSha -ne $adapter -or $extension.entrySha -ne $entry -or
            $extension.runtimeSha -ne $info.runtimeSha256 -or
            $extension.configSourceSha -ne (Sha (Join-Path $root "Assets/_Soccer/Manager/Training/MNG_$stage.yaml"))){throw 'Invalid extension gate'}
        if($MaximumSteps -gt 1000000 -and $extension.artifactKeepCheckpoints -ne 64){throw '2M extension must preserve 64 checkpoints'}
        $configText=Get-Content (Join-Path $root "Assets/_Soccer/Manager/Training/MNG_$stage.yaml") -Raw
        foreach($schedule in @('learning_rate_schedule','beta_schedule','epsilon_schedule')){
            if($configText -notmatch "(?m)^      ${schedule}: constant\s*$"){throw 'Extension requires unchanged constant PPO schedules'}
        }
    }
    if($selfPlay -and !(Test-Path "$result/MNG_ManagerV2/mng-v2-pool.pt")){throw 'Missing pool sidecar'}
    if($isV3){
        if($prior.generation -ne 'v3' -or $prior.historicalPolicy -or $prior.baseActorSourceSha -ne $baseActorSha){throw 'Invalid v3 resume lineage'}
        $baseSnapshotSha=$prior.baseSnapshotSha
    }
} else {
    if((Test-Path $result) -or (Test-Path $evidence)){throw 'Preserve existing formal Run'}
}
$segmentName="to-$StopAt"
if($SegmentSuffix){$segmentName+="-$SegmentSuffix"}
$segment=Join-Path $evidence $segmentName
if(Test-Path $segment){throw 'Preserve existing segment'}
if($ValidateOnly){
    [ordered]@{validated=$true;generation=$generation;runId=$RunId;trainingStarted=$false;runtimeSha=$info.runtimeSha256;historicalPolicy=$HistoricalPolicy} | ConvertTo-Json
    return
}
if(!$Resume){
    New-Item -ItemType Directory -Path $evidence | Out-Null
    New-Item -ItemType Directory -Path "$evidence/source" | Out-Null
    Copy-Item "$root/Tools/mng_v2_learn.py","$root/Tools/mng_v2_formal_learn.py","$root/Tools/mng_player_log_guard.py",$PSCommandPath -Destination "$evidence/source"
    Copy-Item (Join-Path $build 'build-info.json') "$evidence/build-info.json"
}
New-Item -ItemType Directory -Path $segment | Out-Null
$yaml=Get-Content (Join-Path $root "Assets/_Soccer/Manager/Training/MNG_$stage.yaml") -Raw
$yaml=[regex]::Replace(($yaml -join "`n"),'(?m)^    max_steps: \d+\s*$',"    max_steps: $MaximumSteps")
if($Resume -and $MaximumSteps -gt 1000000){
    $yaml=[regex]::Replace($yaml,'(?m)^    keep_checkpoints: \d+\s*$',"    keep_checkpoints: $($extension.artifactKeepCheckpoints)")
}
if(!$Resume -and $selfPlay){
    $yaml=$yaml.Replace('trainer_type: ppo', "trainer_type: ppo`n    init_path: $($InitialPolicy.Replace('\','/'))")
}
if(!$selfPlay -and $yaml -match 'init_path'){throw 'MS2 must be fresh'}
$config=Join-Path $segment 'config.yaml'
$yaml | Set-Content $config -Encoding ascii
if(!$Resume){
    [ordered]@{run=$RunId;generation=$generation;baseActorSourceSha=$baseActorSha;baseSnapshotSha=$baseSnapshotSha;schema=$info.schema;runtimeSha=$info.runtimeSha256;adapterSha=$adapter;entrySha=$entry;
        workers=32;seed=$Seed;maximumSteps=$MaximumSteps;environmentRevision=$info.environmentRevision;status='training-candidate';initialPolicy=$InitialPolicy;
        policySource=$(if($selfPlay){'preparation-gate-approved actor only; fresh optimizer'}else{'fresh network and optimizer'});
        historicalPolicy=$HistoricalPolicy;poolSeed=20260922;
        trainingConfigSha=(Sha $config)} | ConvertTo-Json | Set-Content "$evidence/manifest.json"
}
$env:MNG_V2_STOP_AT=[string]$StopAt
$env:MNG_V2_EVIDENCE=$evidence
$env:MNG_V2_PLAYER_LOGS=Join-Path $result 'run_logs'
$env:MNG_V2_POOL_SEED='20260922'
$env:MNG_EXPERIMENT_GENERATION=$generation
if($isV3){$env:MNG_V3_INITIAL_SNAPSHOT_SHA=$baseSnapshotSha}
else{Remove-Item Env:MNG_V3_INITIAL_SNAPSHOT_SHA -ErrorAction SilentlyContinue}
if($HistoricalPolicy){$env:MNG_V2_PINNED_POLICY=(Resolve-Path -LiteralPath $HistoricalPolicy).Path}
else {Remove-Item Env:MNG_V2_PINNED_POLICY -ErrorAction SilentlyContinue}
$argsList=@("$root/Tools/mng_v2_formal_learn.py",$config,'--run-id',$RunId,'--results-dir',"$root/results",
    '--env',$exe,'--num-envs','32','--base-port','6500','--seed',[string]$Seed,'--torch-device','cuda',
    '--timeout-wait','120','--max-lifetime-restarts','0','--no-graphics')
if($Resume){$argsList+='--resume'}
$argsList+=@('--env-args','-mngEvidenceDir',"$segment/workers",'-mngRunId',$RunId,'-mngBuildSha',$info.runtimeSha256,
    '-mngPolicyAssistMode','none','-mngFullTrace','true')
Push-Location $root
try{
    & 'C:/Users/USER/miniconda3/envs/mlagents/python.exe' @argsList 2>&1 | Tee-Object "$segment/trainer.log"
    if($LASTEXITCODE -ne 0){throw "Formal trainer failed: $LASTEXITCODE"}
    Copy-Item -LiteralPath "$result/run_logs" -Destination "$segment/run_logs" -Recurse
    $last=Get-ChildItem "$result/MNG_ManagerV2" -Filter 'MNG_ManagerV2-*.pt' |
        Sort-Object { [int]($_.BaseName -split '-')[-1] } | Select-Object -Last 1
    [ordered]@{step=[int]($last.BaseName -split '-')[-1];status='saved';stopAt=$StopAt;runtimeSha=$info.runtimeSha256} |
        ConvertTo-Json | Set-Content "$evidence/progress.json"
    if(Test-Path (Join-Path $evidence 'player-failure.json')){throw 'Player failure detected; checkpoint saved, run blocked'}
}finally{Pop-Location; Remove-Item Env:MNG_V2_STOP_AT,Env:MNG_V2_EVIDENCE,Env:MNG_V2_PLAYER_LOGS,Env:MNG_V2_PINNED_POLICY,Env:MNG_V2_POOL_SEED,Env:MNG_EXPERIMENT_GENERATION,Env:MNG_V3_INITIAL_SNAPSHOT_SHA -ErrorAction SilentlyContinue}
