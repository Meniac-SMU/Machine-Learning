[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ReportDirectory,[Parameter(Mandatory=$true)][string]$OutputPath,[int]$FirstSeed=9407,[string]$ReportPrefix='holdout',[int]$ExpectedScenes=16,[ValidateSet('score','mission')][string]$ComparisonSet='score')
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$directory=[IO.Path]::GetFullPath((Join-Path $repoRoot $ReportDirectory))
$outputFile=[IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
foreach($path in @($directory,$outputFile)){if(!$path.StartsWith($repoRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Paths must stay in this project.'}}
if(Test-Path -LiteralPath $outputFile){throw 'Preserve previous assessments; choose a new output path.'}
$rows=@(foreach($seed in $FirstSeed..($FirstSeed+3)){
    $report=Get-Content -LiteralPath (Join-Path $directory "g5-matchups-$ReportPrefix-$seed.json") -Raw | ConvertFrom-Json
    if($report.trials.Count -ne $ExpectedScenes*32){throw "Incomplete holdout seed $seed."}
    $report.trials
})
$rules=@(
    @{command=0;alternative=1;scenario='central-opening';metric='earlyMaxOwnAdvance';sign=1},
    @{command=1;alternative=0;scenario='rear-pressure-far-receiver';metric='earlyMaxOwnAdvance';sign=1},
    @{command=2;alternative=0;scenario='central-pressure-open-wide-lanes';metric='ownGoals';sign=1},
    @{command=3;alternative=0;scenario='deep-goal-block';metric='ownGoals';sign=1},
    @{command=4;alternative=6;scenario='cutback-interception-window';metric='enemyGoals';sign=-1},
    @{command=5;alternative=4;scenario='off-lane-distant-receiver-coverage';metric='enemyReceptions';sign=-1},
    @{command=6;alternative=0;scenario='nearby-loose-ball';metric='enemyGoals';sign=-1},
    @{command=7;alternative=0;scenario='central-goal-threat';metric='enemyGoals';sign=-1}
)
if($ComparisonSet -eq 'mission'){
    $rules[2]=@{command=2;alternative=0;scenario='central-pressure-open-wide-lanes';metric='earlyMaxOwnAdvance';sign=1}
    $rules[4]=@{command=4;alternative=5;scenario='wide-trap-window';metric='ownPossession';sign=1;continuation='fixed-twelve-seconds'}
    $rules[6]=@{command=6;alternative=0;scenario='nearby-loose-ball';metric='earlyPossession';sign=1}
    $rules[7]=@{command=7;alternative=0;scenario='prepared-pass-goal-threat';metric='enemyGoals';sign=-1}
}
$comparisons=@(foreach($rule in $rules){
    $continuation=if($rule.continuation){$rule.continuation}elseif($rule.command -eq 5){'fixed-twelve-seconds'}else{'rules-after-three-seconds'}
    $scene=@($rows | Where-Object {$_.scenario -eq $rule.scenario -and $_.continuation -eq $continuation})
    $pairs=@(foreach($seed in $FirstSeed..($FirstSeed+3)){foreach($mirror in 0..1){
        $a=@($scene | Where-Object {$_.candidate -eq $rule.command -and $_.seed -eq $seed -and $_.mirror -eq $mirror})
        $b=@($scene | Where-Object {$_.candidate -eq $rule.alternative -and $_.seed -eq $seed -and $_.mirror -eq $mirror})
        if($a.Count -ne 1 -or $b.Count -ne 1){throw 'Missing or duplicate paired comparison.'}
        $value=[double]$a[0].($rule.metric);$alternative=[double]$b[0].($rule.metric)
        [pscustomobject][ordered]@{seed=$seed;mirror=$mirror;candidate=$value;alternative=$alternative;favourableDifference=($value-$alternative)*$rule.sign}
    }})
    $win=@($pairs|Where-Object favourableDifference -gt .0001).Count
    $loss=@($pairs|Where-Object favourableDifference -lt -.0001).Count
    $mean=($pairs|Measure-Object favourableDifference -Average).Average
    [pscustomobject][ordered]@{command=$rule.command;alternative=$rule.alternative;scenario=$rule.scenario;continuation=$continuation;metric=$rule.metric;
        candidateMean=($pairs|Measure-Object candidate -Average).Average;alternativeMean=($pairs|Measure-Object alternative -Average).Average;
        favourable=$win;tied=8-$win-$loss;unfavourable=$loss;meanFavourableDifference=$mean;primaryCriterionPassed=($win -ge 6 -and $mean -gt 0);pairs=$pairs}
})
[ordered]@{source=$ReportDirectory;comparisonSet=$ComparisonSet;firstSeed=$FirstSeed;expectedScenes=$ExpectedScenes;trials=$rows.Count;trainingStarted=$false;
    collectionPhysicallyValid=(@($rows|Where-Object {$_.elapsed -lt 12 -or $_.corrections -ne 0}).Count -eq 0);
    allPrimaryCriteriaPassed=(@($comparisons|Where-Object {!$_.primaryCriterionPassed}).Count -eq 0);
    verdict='Prespecified conditional comparisons only; weakness review and balance judgement remain separate';comparisons=$comparisons} |
    ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $outputFile -Encoding UTF8
$comparisons | Select-Object command,alternative,metric,candidateMean,alternativeMean,favourable,tied,unfavourable,primaryCriterionPassed | Format-Table
