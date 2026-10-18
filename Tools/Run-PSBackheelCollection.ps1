$ErrorActionPreference='Stop'
$taskRoot='C:/GitHub/Machine-Learning'
$evidence=Join-Path $taskRoot 'Logs/MNG-Rebuild/PS-Retry-20260929'
$buildRecord=Get-ChildItem (Join-Path $taskRoot 'Logs/BuildLifecycle/PS-Backheel*.json') | Sort-Object LastWriteTime -Descending | Select-Object -First 1
for($i=0;$i -lt 120;$i++) {
    if(Test-Path (Join-Path $evidence 'STOP')) { throw 'User quota stop marker present' }
    $record=Get-Content $buildRecord.FullName -Raw | ConvertFrom-Json
    if($record.status -eq 'completed'){ break }
    if($record.status -eq 'failed'){ throw 'Build failed' }
    Start-Sleep -Seconds 5
}
if($record.status -ne 'completed'){throw 'Build did not finish in bounded wait'}
$buildPath=Join-Path $taskRoot $record.output
$runtimeDll=Join-Path $buildPath 'EvaluationV2/MNG_EvaluationV2_Data/Managed/MNG.Runtime.dll'
$config=[ordered]@{build=$buildPath;actualRuntimeSha=(Get-FileHash $runtimeDll -Algorithm SHA256).Hash.ToLower();extraArgs=@('-mngPSContactPass');experimentRevision='PS-backheel-damping-20260929';goalAreaPassBan=$false;recipientDistanceMask=$false;observations=244;branches=@(6,5);physicsTest='backheel-playmode.xml: 24 actual keeper receptions with collision restoration';ownerPostStrikeContactGrace=$true;opponentCollisionUnchanged=$true;commonRuleKicksUnchanged=$true;compiledLegacyLabelIsNotUnchangedBehaviorProof=$true}
$configPath=Join-Path $evidence 'runtime-backheel.json'
if(Test-Path $configPath){throw 'Do not overwrite runtime receipt'}
$config|ConvertTo-Json -Depth 5|Set-Content $configPath -Encoding utf8NoBOM
& (Join-Path $taskRoot 'Tools/Build-Lifecycle.ps1') -Action Use -BuildDirectory (Join-Path $buildPath 'EvaluationV2') -Stage PS-Backheel -Purpose 'Exact-prefix passing counterfactual collection; Full excluded' -Evidence 'Logs/MNG-Rebuild/PS-Retry-20260929/counterfactual-backheel/manifest.json'
$env:MNG_PSR_RUNTIME=$configPath
& 'C:/Users/USER/miniconda3/envs/mlagents/python.exe' Tools/mng_psr_counterfactual.py --seeds 60 --parallel 4 --output (Join-Path $evidence 'counterfactual-backheel') *> (Join-Path $evidence 'counterfactual-backheel.stdout.log')
if($LASTEXITCODE -ne 0){throw 'Counterfactual collection failed; preserve logs'}
