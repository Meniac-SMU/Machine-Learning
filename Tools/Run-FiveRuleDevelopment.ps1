[CmdletBinding()]
param([string]$BuildDirectory='Builds/Manager5v5/rule-strong-v4-20261013',
    [string]$Series='v4',
    [string[]]$Profiles=@('carry','pressure','contain','carry-pressure','direct','wide-pressure'))
$ErrorActionPreference='Stop'
$evidence='Logs/Manager5v5/rule-strong-20261013'
$candidate='results/Manager5v5/MNG5_V2-20261013-r001/MNG5_Manager_v2/MNG5_Manager_v2-200011.onnx'
foreach($name in $Profiles){
    & (Join-Path $PSScriptRoot 'Run-FiveRuleEvaluation.ps1') -BuildDirectory $BuildDirectory -OutputDirectory "$evidence/dev-$Series-$name" -Candidate $candidate -Variant strong -Settings "$evidence/settings/$name.json" -Pairs 8 -Seed 31000 | Out-File "$evidence/dev-$Series-$name-runner.log" -Encoding utf8
    $summary=Get-Content "$evidence/dev-$Series-$name/summary.json" -Raw | ConvertFrom-Json
    [pscustomobject]@{profile=$name;wins=$summary.losses;draws=$summary.draws;losses=$summary.wins;goalsFor=$summary.goalsAgainst;goalsAgainst=$summary.goalsFor}|ConvertTo-Json -Compress
}
