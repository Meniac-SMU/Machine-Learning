[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$ReportPath,[Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference='Stop'
$repoRoot=(Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$reportFile=[IO.Path]::GetFullPath((Join-Path $repoRoot $ReportPath))
$outputFile=[IO.Path]::GetFullPath((Join-Path $repoRoot $OutputPath))
foreach($path in @($reportFile,$outputFile)){if(!$path.StartsWith($repoRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Paths must stay in the project.'}}
if(Test-Path -LiteralPath $outputFile){throw 'Use a new output filename to retain past analysis.'}
$report=Get-Content -LiteralPath $reportFile -Raw | ConvertFrom-Json
$summary=@($report.trials | Group-Object phase,scenario,continuation,candidate,opponent | ForEach-Object {
    $rows=$_.Group;$first=$rows[0]
    function Mean([string]$key){[Math]::Round(($rows|Measure-Object -Property $key -Average).Average,3)}
    [ordered]@{phase=$first.phase;scenario=$first.scenario;continuation=$first.continuation;candidate=$first.candidate;opponent=$first.opponent;n=$rows.Count;
        goals=Mean 'ownGoals';conceded=Mean 'enemyGoals';shots=Mean 'ownShots';enemyShots=Mean 'enemyShots';passes=Mean 'ownPasses';receptions=Mean 'ownReceptions';enemyPasses=Mean 'enemyPasses';enemyReceptions=Mean 'enemyReceptions';
        possessionSeconds=Mean 'ownPossession';enemyPossessionSeconds=Mean 'enemyPossession';maximumControlledX=Mean 'maxOwnAdvance';enemyMaximumControlledX=Mean 'maxEnemyAdvance';
        passFlightSeconds=if($first.PSObject.Properties.Name -contains 'ownPassFlight'){Mean 'ownPassFlight'}else{$null};
        earlyMaximumControlledX=Mean 'earlyMaxOwnAdvance';earlyPossessionSeconds=Mean 'earlyPossession';
        minimumMeanStamina=Mean 'minimumMeanStamina';neverSecure=@($rows|Where-Object firstSecureAt -lt 0).Count;boundaryCorrections=($rows|Measure-Object corrections -Sum).Sum}
})
[ordered]@{source=$ReportPath;trials=$report.trials.Count;balanced=$false;verdict='Descriptive initial diagnostics; no statistical or balance pass inferred';groups=$summary} |
    ConvertTo-Json -Depth 7 | Set-Content -LiteralPath $outputFile -Encoding UTF8
Write-Output ('Summarized '+$report.trials.Count+' trials into '+$summary.Count+' groups.')
