[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ReportDirectory,[Parameter(Mandatory=$true)][string]$OutputPath,[int]$FirstSeed=9407)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$directory=[IO.Path]::GetFullPath((Join-Path $repoRoot $ReportDirectory))
$outputFile=[IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
foreach($path in @($directory,$outputFile)){if(!$path.StartsWith($repoRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Paths must stay inside this project.'}}
if(Test-Path -LiteralPath $outputFile){throw 'Preserve existing reviews.'}
$rows=@(foreach($seed in $FirstSeed..($FirstSeed+3)){
    $report=Get-Content -LiteralPath (Join-Path $directory "g5-matchups-holdout-$seed.json") -Raw|ConvertFrom-Json
    if($report.trials.Count -ne 512){throw "Incomplete 16-situation report for seed $seed."}
    $report.trials
})
# Mission objectives retain the meaning of each situation; no weighted win score is manufactured.
$objectives=@{
    'central-opening'=@('earlyMaxOwnAdvance',1)
    'rear-pressure-far-receiver'=@('earlyMaxOwnAdvance',1)
    'wide-escape'=@('earlyMaxOwnAdvance',1)
    'deep-goal-block'=@('ownGoals',1)
    'wide-trap-window'=@('ownPossession',1)
    'distant-receiver-denial'=@('enemyReceptions',-1)
    'nearby-loose-ball'=@('earlyPossession',1)
    'central-goal-threat'=@('enemyGoals',-1)
    'central-pressure-open-wide-lanes'=@('earlyMaxOwnAdvance',1)
    'approach-to-low-block'=@('ownGoals',1)
    'cutback-interception-window'=@('enemyGoals',-1)
    'split-distant-receivers'=@('enemyReceptions',-1)
    'off-lane-distant-receiver-coverage'=@('enemyReceptions',-1)
    'wide-transition-to-attacking-half'=@('earlyMaxOwnAdvance',1)
    'wide-direct-exit'=@('enemyReceptions',-1)
    'prepared-pass-goal-threat'=@('enemyGoals',-1)
}
$contexts=@(foreach($scenario in ($objectives.Keys|Sort-Object)){foreach($continuation in @('fixed-twelve-seconds','rules-after-three-seconds')){
    $metric=$objectives[$scenario][0];$direction=[int]$objectives[$scenario][1]
    $values=@(foreach($command in 0..7){
        $samples=@($rows|Where-Object {$_.scenario -eq $scenario -and $_.continuation -eq $continuation -and $_.candidate -eq $command})
        if($samples.Count -ne 8){throw 'Missing or duplicated candidate samples.'}
        [pscustomobject]@{command=$command;missionMean=($samples|Measure-Object -Property $metric -Average).Average;staminaMean=($samples|Measure-Object minimumMeanStamina -Average).Average}
    })
    [pscustomobject]@{scenario=$scenario;continuation=$continuation;metric=$metric;direction=$direction;values=$values}
}})
$pairs=@(foreach($command in 0..7){foreach($alternative in 0..7){if($command -eq $alternative){continue}
    $better=0;$worse=0;$same=0;$energyBetter=0;$energyWorse=0
    foreach($context in $contexts){
        $a=$context.values[$command];$b=$context.values[$alternative]
        $difference=($a.missionMean-$b.missionMean)*$context.direction
        if($difference -gt .0001){$better++}elseif($difference -lt -.0001){$worse++}else{$same++}
        if($a.staminaMean-$b.staminaMean -gt .0001){$energyBetter++}
        if($a.staminaMean-$b.staminaMean -lt -.0001){$energyWorse++}
    }
    [pscustomobject]@{command=$command;alternative=$alternative;missionBetter=$better;missionWorse=$worse;missionTied=$same;staminaBetter=$energyBetter;staminaWorse=$energyWorse;
        dominatesMissionMeans=($worse -eq 0 -and $better -gt 0);
        dominatesMissionAndStaminaMeans=($worse -eq 0 -and $energyWorse -eq 0 -and ($better -gt 0 -or $energyBetter -gt 0))}
}})
[ordered]@{source=$ReportDirectory;firstSeed=$FirstSeed;trials=$rows.Count;contexts=$contexts;pairs=$pairs;exploratory=$true;balancePassed=$false;
    interpretation='Finite-situation mean comparisons only. Absence of observed dominance does not prove balance or unique necessity. Primary score and mission criteria remain separate.'}|
    ConvertTo-Json -Depth 8|Set-Content -LiteralPath $outputFile -Encoding UTF8
$pairs|Where-Object dominatesMissionMeans|Format-Table command,alternative,missionBetter,missionWorse,staminaWorse,dominatesMissionAndStaminaMeans
Write-Output ('Reviewed '+$rows.Count+' trials and '+$pairs.Count+' directed substitutions. This is not a balance pass.')
