[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ReportDirectory,[Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$reportRoot=[IO.Path]::GetFullPath((Join-Path $repoRoot $ReportDirectory))
$output=[IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
foreach($path in @($reportRoot,$output)){if(!$path.StartsWith($repoRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Keep evidence inside this project.'}}
if(Test-Path -LiteralPath $output){throw 'Preserve the previous assessment; use a new filename.'}
$summaries=@();$rows=@();$referenceKeys=$null
foreach($period in 2,3,4){
    $source=Join-Path $reportRoot ('g4-rule-matches-period-'+$period+'.json')
    $matches=@((Get-Content -LiteralPath $source -Raw | ConvertFrom-Json).matches)
    if($matches.Count -ne 16){throw "Expected sixteen games for period $period."}
    $keys=@($matches | ForEach-Object {"$($_.seed):$($_.mirror)"} | Sort-Object)
    if(@($keys | Select-Object -Unique).Count -ne 16){throw 'Duplicate seed/mirror.'}
    if($null -ne $referenceKeys -and (Compare-Object $referenceKeys $keys)){throw 'Periods must use the same starting conditions.'}
    $referenceKeys=$keys;$periodRows=@();$selections=New-Object int[] 8
    foreach($match in $matches){
        if($match.decisionPeriod -ne $period){throw 'Incorrect period recorded.'}
        $issues=@()
        if(!$match.finished -or $match.elapsed -lt 300){$issues+='not-finished'}
        if($match.passes -le 0){$issues+='no-pass'}
        if($match.shots -le 0){$issues+='no-shot'}
        if($match.recoveries -le 1){$issues+='no-recovery'}
        if($match.maximumStall -ge 30){$issues+='stall-at-least-30s'}
        if($match.corrections -ne 0){$issues+='boundary-correction'}
        $last=@{};$changes=0;$decisions=0;$timeouts=0;$preparationTimeouts=0;$kicked=@{}
        foreach($event in $match.events){
            if($event.kind -eq 'command'){
                $decisions++;$team=[int]$event.actor;$command=[int]$event.other
                if($last.ContainsKey($team) -and $last[$team] -ne $command){$changes++}
                $last[$team]=$command;$selections[$command]++
            }
            if($event.kind -eq 'pass-kicked'){$kicked[[int]$event.id]=$true}
            if($event.kind -eq 'pass-failed' -and $event.reason -eq 'timeout'){
                $timeouts++;if(!$kicked.ContainsKey([int]$event.id)){$preparationTimeouts++}
            }
        }
        $row=[pscustomobject]@{period=$period;seed=$match.seed;mirror=$match.mirror;healthy=$issues.Count -eq 0;issues=$issues;goals=$match.redScore+$match.navyScore;passes=$match.passes;receptions=$match.receptions;shots=$match.shots;maximumStall=$match.maximumStall;corrections=$match.corrections;decisions=$decisions;changedConsecutiveDecisions=$changes;passTimeouts=$timeouts;preparationTimeouts=$preparationTimeouts}
        $periodRows+=$row;$rows+=$row
    }
    $summaries+=[pscustomobject]@{period=$period;games=$periodRows.Count;healthy=@($periodRows | Where-Object healthy).Count;goals=($periodRows | Measure-Object goals -Sum).Sum;passes=($periodRows | Measure-Object passes -Sum).Sum;receptions=($periodRows | Measure-Object receptions -Sum).Sum;shots=($periodRows | Measure-Object shots -Sum).Sum;maximumStall=($periodRows | Measure-Object maximumStall -Maximum).Maximum;corrections=($periodRows | Measure-Object corrections -Sum).Sum;changedConsecutiveDecisions=($periodRows | Measure-Object changedConsecutiveDecisions -Sum).Sum;preparationTimeouts=($periodRows | Measure-Object preparationTimeouts -Sum).Sum;selections=$selections}
}
[ordered]@{source=$ReportDirectory;createdUtc=[DateTime]::UtcNow.ToString('o');interpretation='Matched eight-seed pilot; collection completion is separate from per-game health. Selection counts include reselection. Changes compare consecutive recorded decisions per team; first selection is excluded. No optimal-period or global-balance claim.';periods=$summaries;matches=$rows} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $output -Encoding UTF8
$summaries | Format-Table period,games,healthy,goals,passes,receptions,maximumStall,corrections,changedConsecutiveDecisions,preparationTimeouts
$rows | Where-Object {!$_.healthy} | Format-Table period,seed,mirror,issues,maximumStall
