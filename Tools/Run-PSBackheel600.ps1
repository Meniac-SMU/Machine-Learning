$ErrorActionPreference='Stop'
$taskRoot='C:/GitHub/Machine-Learning'
$evidence=Join-Path $taskRoot 'Logs/MNG-Rebuild/PS-Retry-20260929'
$completion=Join-Path $evidence 'train-to-500000/completion.json'
for($i=0;$i -lt 1080;$i++){
 if(Test-Path (Join-Path $evidence 'STOP')){throw 'Quota stop marker present'}
 if(Test-Path $completion){break};Start-Sleep -Seconds 5
}
if(!(Test-Path $completion)){throw 'PS-R4 did not finish'}
$gate=Get-Content $completion -Raw|ConvertFrom-Json
if($gate.status -ne 'completed' -or $gate.additionalSteps -ne 500000){throw 'PS-R4 did not pass save gate'}
$env:MNG_PSR_RUNTIME=Join-Path $evidence 'runtime-backheel.json'
$examples=Join-Path $evidence 'counterfactual-backheel-combined.jsonl'
if(!(Test-Path $examples)){$examples=Join-Path $evidence 'counterfactual-backheel/examples.jsonl'}
& 'C:/Users/USER/miniconda3/envs/mlagents/python.exe' Tools/mng_psr_train.py --target 600000 --workers 16 --curriculum 0 --resume --verified-demos $examples *> (Join-Path $evidence 'train-600k.stdout.log')
if($LASTEXITCODE -ne 0){throw 'PS-R5 failed; checkpoint retained'}
$gate=Get-Content (Join-Path $evidence 'train-to-600000/completion.json') -Raw|ConvertFrom-Json
if($gate.status -ne 'completed' -or $gate.additionalSteps -ne 600000){throw 'PS-R5 incomplete; do not evaluate'}
$env:MNG_PSR_EVAL_PORT='22500'
& 'C:/Users/USER/miniconda3/envs/mlagents/python.exe' Tools/mng_psr_play.py --candidate results/MNG_MS3V3-20260929-r003/PS-600000.pt --pairs 20 --seed 933100 --parallel 12 --output (Join-Path $evidence 'duel-600k') *> (Join-Path $evidence 'duel-600k.stdout.log')
if($LASTEXITCODE -ne 0){throw '600k duel failed; retain evidence'}
