param(
    [Parameter(Mandatory=$true)][string]$RunId,
    [Parameter(Mandatory=$true)][ValidateSet(200000,400000)][int]$Boundary,
    [Parameter(Mandatory=$true)][string]$EvaluationBuild,
    [ValidateRange(1,8)][int]$Parallel=4
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$policy=Join-Path $root "results/$RunId/MNG_ManagerV2"
$evidence=Join-Path $root "Logs/MNG-Rebuild/$RunId"
$inspection=Get-Content "$evidence/inspect-$($Boundary/1000)k.json" -Raw | ConvertFrom-Json
if(!$inspection.finite -or $inspection.checkpointStep -lt $Boundary){throw 'Verified saved boundary required'}
$candidate=Join-Path $policy "MNG_ManagerV2-$($inspection.checkpointStep).onnx"
$initial=Join-Path $policy 'MNG_ManagerV2-0.onnx'
$jobs=@(@{name='step0';opponent=$initial;pairs=20;seed=592201})
if($Boundary -eq 400000){
    $anchor=Get-Content "$evidence/inspect-200k.json" -Raw | ConvertFrom-Json
    $jobs+=@{name='200k';opponent=(Join-Path $policy "MNG_ManagerV2-$($anchor.checkpointStep).onnx");pairs=20;seed=592201}
}
$jobs+=@{name='full';opponent='R0-Full-v2';pairs=20;seed=592201}
if($Boundary -eq 400000){
    foreach($baseline in @('uniform-valid','recover','balanced','carry-shot')){
        $jobs+=@{name=$baseline;opponent=$baseline;pairs=20;seed=592201}
    }
    $jobs+=@{name='step0-holdout';opponent=$initial;pairs=40;seed=882201}
    # A regression confirmation uses a new seed family for BOTH frozen policies.
    $jobs+=@{name='full-confirm';opponent='R0-Full-v2';pairs=20;seed=792201;conditionalR0Regression=$true}
    $jobs+=@{name='step0-full-confirm';opponent='R0-Full-v2';pairs=20;seed=792201;conditionalR0Regression=$true;candidateOverride=$initial}
}
$planPath=Join-Path $evidence "league-$($Boundary/1000)k-plan.json"
if(Test-Path $planPath){throw 'League already planned; preserve evidence and inspect before retry'}
foreach($job in $jobs){
    $job.output=Join-Path $root "Logs/MNG-Rebuild/R6-$($Boundary/1000)k-vs-$($job.name)"
    if(Test-Path $job.output){throw "Existing evaluation: $($job.output)"}
    $job.opponentSha=if(Test-Path $job.opponent){(Get-FileHash $job.opponent -Algorithm SHA256).Hash.ToLower()}else{$job.opponent}
}
[ordered]@{run=$RunId;step=$inspection.checkpointStep;candidate=$candidate;
    candidateSha=(Get-FileHash $candidate -Algorithm SHA256).Hash.ToLower();jobs=$jobs} |
    ConvertTo-Json -Depth 6 | Set-Content $planPath
Push-Location $root
try{
    foreach($job in $jobs){
        if($job.conditionalR0Regression){
            $r0=Get-Content "$root/Logs/MNG-Rebuild/R6-400k-vs-full/report.json" -Raw | ConvertFrom-Json
            $reference=Get-Content "$root/Logs/MNG-Rebuild/R5-r2-200k-vs-full/report.json" -Raw | ConvertFrom-Json
            if($reference.manifest.runtimeSha -ne $r0.manifest.runtimeSha -or $reference.manifest.protocol -ne $r0.manifest.protocol){throw 'Historical R0 reference must be reevaluated under the same runtime and protocol'}
            if($reference.manifest.candidate -ne (Get-FileHash $initial -Algorithm SHA256).Hash.ToLower()){throw 'Reference policy mismatch'}
            if($reference.summary.scoreRate-$r0.summary.scoreRate -le .05+1e-12){continue}
        }
        $evaluatedCandidate=if($job.candidateOverride){$job.candidateOverride}else{$candidate}
        & 'C:/Users/USER/miniconda3/envs/mlagents/python.exe' Tools/mng_v2_evaluate.py --build $EvaluationBuild --candidate $evaluatedCandidate --opponent $job.opponent --output $job.output --pairs $job.pairs --seed $job.seed --parallel $Parallel
        if($LASTEXITCODE -ne 0){throw "Evaluation failed: $($job.name)"}
    }
}finally{Pop-Location}
