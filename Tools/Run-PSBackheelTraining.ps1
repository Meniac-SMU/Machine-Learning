$ErrorActionPreference='Stop'
$taskRoot='C:/GitHub/Machine-Learning'
$evidence=Join-Path $taskRoot 'Logs/MNG-Rebuild/PS-Retry-20260929'
$report=Join-Path $evidence 'counterfactual-backheel/report.json'
for($i=0;$i -lt 360;$i++) {
    if(Test-Path (Join-Path $evidence 'STOP')) { throw 'Quota stop marker present' }
    if(Test-Path $report){break}
    Start-Sleep -Seconds 5
}
if(!(Test-Path $report)){throw 'Collection unfinished; do not train'}
$examples=Join-Path $evidence 'counterfactual-backheel/examples.jsonl'
$rows=@(Get-Content $examples | ForEach-Object {$_|ConvertFrom-Json})
$positives=@($rows|Where-Object {$_.split -eq 'train' -and $_.preferredRecipient -gt 0}).Count
if($positives -lt 8){"Only $positives verified train positives; do not start new training";exit 0}
$env:MNG_PSR_RUNTIME=Join-Path $evidence 'runtime-backheel.json'
& 'C:/Users/USER/miniconda3/envs/mlagents/python.exe' Tools/mng_psr_train.py --target 500000 --workers 16 --curriculum 0.3 --resume --verified-demos $examples *> (Join-Path $evidence 'train-500k.stdout.log')
if($LASTEXITCODE -ne 0){throw 'Training failed; preserve checkpoint and logs'}
