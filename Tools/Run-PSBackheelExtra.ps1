$ErrorActionPreference='Stop'
$taskRoot='C:/GitHub/Machine-Learning'
$evidence=Join-Path $taskRoot 'Logs/MNG-Rebuild/PS-Retry-20260929'
$report=Join-Path $evidence 'counterfactual-backheel/report.json'
for($i=0;$i -lt 360;$i++) {
 if(Test-Path (Join-Path $evidence 'STOP')){throw 'Quota stop marker present'}
 if(Test-Path $report){break}; Start-Sleep -Seconds 5
}
if(!(Test-Path $report)){throw 'Collection unfinished'}
$first=Join-Path $evidence 'counterfactual-backheel/examples.jsonl'
$rows=@(Get-Content $first|ForEach-Object {$_|ConvertFrom-Json})
$positives=@($rows|Where-Object {$_.split -eq 'train' -and $_.preferredRecipient -gt 0}).Count
if($positives -ge 8){'Initial collector has enough examples; original training wrapper owns next stage';exit 0}
$env:MNG_PSR_RUNTIME=Join-Path $evidence 'runtime-backheel.json'
& 'C:/Users/USER/miniconda3/envs/mlagents/python.exe' Tools/mng_psr_counterfactual.py --start 60 --seeds 120 --parallel 4 --output (Join-Path $evidence 'counterfactual-backheel-extra') *> (Join-Path $evidence 'counterfactual-backheel-extra.stdout.log')
if($LASTEXITCODE -ne 0){throw 'Extra collection failed'}
$second=Join-Path $evidence 'counterfactual-backheel-extra/examples.jsonl'
$combined=Join-Path $evidence 'counterfactual-backheel-combined.jsonl'
if(Test-Path $combined){throw 'Preserve prior examples'}
Get-Content $first,$second|Set-Content $combined -Encoding utf8NoBOM
$rows=@(Get-Content $combined|ForEach-Object {$_|ConvertFrom-Json})
$positives=@($rows|Where-Object {$_.split -eq 'train' -and $_.preferredRecipient -gt 0}).Count
if($positives -lt 8){"Only $positives verified train positives after 180 seeds; do not start verified-demo training";exit 0}
& 'C:/Users/USER/miniconda3/envs/mlagents/python.exe' Tools/mng_psr_train.py --target 500000 --workers 16 --curriculum 0.3 --resume --verified-demos $combined *> (Join-Path $evidence 'train-500k.stdout.log')
if($LASTEXITCODE -ne 0){throw 'Training failed; preserve checkpoint and logs'}
