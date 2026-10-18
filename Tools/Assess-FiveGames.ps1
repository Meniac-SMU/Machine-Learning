[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ReportPath,[Parameter(Mandatory=$true)][string]$OutputPath,[int]$ExpectedGames=32)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$source=[IO.Path]::GetFullPath((Join-Path $repoRoot $ReportPath))
$output=[IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
foreach($path in @($source,$output)){if(!$path.StartsWith($repoRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Keep evidence inside this project.'}}
if(Test-Path -LiteralPath $output){throw 'Preserve the previous assessment.'}
$matches=@((Get-Content -LiteralPath $source -Raw|ConvertFrom-Json).matches)
if($matches.Count -ne $ExpectedGames){throw 'Incomplete game collection.'}
$rows=@();$counts=New-Object int[] 8
foreach($match in $matches){
    $issues=@()
    if(!$match.finished -or $match.elapsed -lt 300){$issues+='not-finished'}
    if($match.passes -le 0){$issues+='no-pass'}
    if($match.shots -le 0){$issues+='no-shot'}
    if($match.recoveries -le 1){$issues+='no-recovery'}
    if($match.maximumStall -ge 30){$issues+='stall-at-least-30s'}
    if($match.corrections -ne 0){$issues+='boundary-correction'}
    for($command=0;$command -lt 8;$command++){$counts[$command]+=$match.selections[$command]+$match.selections[$command+8]}
    $rows+=[pscustomobject]@{seed=$match.seed;mirror=$match.mirror;period=$match.decisionPeriod;healthy=$issues.Count -eq 0;issues=$issues;redScore=$match.redScore;navyScore=$match.navyScore;passes=$match.passes;receptions=$match.receptions;shots=$match.shots;maximumStall=$match.maximumStall;corrections=$match.corrections}
}
$summary=[ordered]@{games=$matches.Count;healthy=@($rows|Where-Object healthy).Count;goals=($matches|ForEach-Object {$_.redScore+$_.navyScore}|Measure-Object -Sum).Sum;passes=($matches|Measure-Object passes -Sum).Sum;receptions=($matches|Measure-Object receptions -Sum).Sum;shots=($matches|Measure-Object shots -Sum).Sum;maximumStall=($matches|Measure-Object maximumStall -Maximum).Maximum;corrections=($matches|Measure-Object corrections -Sum).Sum;selectionsIncludingReselection=$counts}
[ordered]@{source=$ReportPath;createdUtc=[DateTime]::UtcNow.ToString('o');summary=$summary;matches=$rows;trainingStarted=$false;interpretation='Quality assessment of this recorded run, not tactical balance or exact PhysX determinism.'}|ConvertTo-Json -Depth 7|Set-Content -LiteralPath $output -Encoding UTF8
$summary|ConvertTo-Json
$rows|Where-Object {!$_.healthy}|Format-Table seed,mirror,issues,maximumStall,corrections
