[CmdletBinding()]
param(
    [string]$BuildDirectory='Builds/Manager5v5/rule-strong-v7-final-20261013',
    [string]$OutputRoot='Logs/Manager5v5/rule-strong-20261013/holdout-v7',
    [int]$SeedOffset=10000
)
$ErrorActionPreference='Stop'
if(Test-Path -LiteralPath $OutputRoot){throw 'Use a fresh holdout directory.'}
New-Item -ItemType Directory -Path $OutputRoot | Out-Null
$models='results/Manager5v5/MNG5_V2-20261013-r001/MNG5_Manager_v2'
$cases=@(
    @{name='final';candidate="$models/MNG5_Manager_v2-200011.onnx";pairs=16;seed=33000},
    @{name='middle';candidate="$models/MNG5_Manager_v2-100042.onnx";pairs=8;seed=33100},
    @{name='initial';candidate="$models/MNG5_Manager_v2-0.onnx";pairs=8;seed=33200},
    @{name='fixed-0';candidate='fixed-0';pairs=4;seed=33300},
    @{name='fixed-3';candidate='fixed-3';pairs=4;seed=33400},
    @{name='fixed-6';candidate='fixed-6';pairs=4;seed=33500}
)
foreach($case in $cases){
    foreach($variant in @('strong','legacy')){
        $name=$case.name+'-'+$variant
        & (Join-Path $PSScriptRoot 'Run-FiveRuleEvaluation.ps1') -BuildDirectory $BuildDirectory -OutputDirectory "$OutputRoot/$name" -Candidate $case.candidate -Variant $variant -Pairs $case.pairs -Seed ($case.seed+$SeedOffset) | Out-File "$OutputRoot/$name-runner.log" -Encoding utf8
        $summary=Get-Content "$OutputRoot/$name/summary.json" -Raw | ConvertFrom-Json
        [pscustomobject]@{opponent=$case.name;rule=$variant;wins=$summary.losses;draws=$summary.draws;losses=$summary.wins;goalsFor=$summary.goalsAgainst;goalsAgainst=$summary.goalsFor}|ConvertTo-Json -Compress
    }
}
