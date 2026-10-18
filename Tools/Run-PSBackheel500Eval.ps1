$ErrorActionPreference='Stop'
$evidence='C:/GitHub/Machine-Learning/Logs/MNG-Rebuild/PS-Retry-20260929'
$completion=Join-Path $evidence 'train-to-500000/completion.json'
for($i=0;$i -lt 360;$i++){
 if(Test-Path (Join-Path $evidence 'STOP')){throw 'Quota stop marker present'}
 if(Test-Path $completion){break};Start-Sleep -Seconds 5
}
if(!(Test-Path $completion)){throw '500k incomplete'}
$gate=Get-Content $completion -Raw|ConvertFrom-Json
if($gate.status -ne 'completed' -or $gate.additionalSteps -ne 500000){throw '500k save gate failed'}
$env:MNG_PSR_RUNTIME=Join-Path $evidence 'runtime-backheel.json'
$env:MNG_PSR_EVAL_PORT='24500'
& 'C:/Users/USER/miniconda3/envs/mlagents/python.exe' Tools/mng_psr_play.py --candidate results/MNG_MS3V3-20260929-r003/PS-500000.pt --pairs 20 --seed 933100 --parallel 8 --output (Join-Path $evidence 'duel-500k') *> (Join-Path $evidence 'duel-500k.stdout.log')
if($LASTEXITCODE -ne 0){throw '500k duel incomplete; raw matches retained'}
