[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CurrentEvidenceDirectory,
    [Parameter(Mandatory = $true)][int]$Step,
    [string]$PreviousAssessment,
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$currentRoot = (Resolve-Path -LiteralPath $CurrentEvidenceDirectory).Path
$baselineRoot = Join-Path $projectRoot 'Logs\MNG-MS\MNG_MS2-20260921-r005-step99968-p0-final-seed491001'

function Get-CurrentSummary([string]$Root) {
    $rows = @(
        Get-Content -LiteralPath (Join-Path $Root 'Full-Red-onnx.json') -Raw -Encoding UTF8 | ConvertFrom-Json
        Get-Content -LiteralPath (Join-Path $Root 'Full-Navy-onnx.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    )
    $matches = [int]$rows[0].matches
    $commands = [long[]]::new(6)
    foreach ($row in $rows) {
        for ($index = 0; $index -lt 6; $index++) { $commands[$index] += [long]$row.effectiveCommandCounts[$index] }
    }
    [ordered]@{
        matchesPerTeam = $matches
        matchSeconds = [double]$rows[0].matchSeconds
        aggregateScoreRate = [double](($rows | Measure-Object scoreRate -Average).Average)
        redScoreRate = [double]($rows | Where-Object policyTeam -eq 'Red').scoreRate
        navyScoreRate = [double]($rows | Where-Object policyTeam -eq 'Navy').scoreRate
        goalsFor = [long](($rows | Measure-Object goalsFor -Sum).Sum)
        goalsAgainst = [long](($rows | Measure-Object goalsAgainst -Sum).Sum)
        validShotsPerMatch = [double](($rows | Measure-Object validShots -Sum).Sum) / (2 * $matches)
        advancesPerMatch = [double](($rows | Measure-Object fiveMeterAdvances -Sum).Sum) / (2 * $matches)
        completedPassesPerMatch = [double](($rows | Measure-Object completedPasses -Sum).Sum) / (2 * $matches)
        validShotsPerMinute = ([double](($rows | Measure-Object validShots -Sum).Sum) / (2 * $matches)) / ([double]$rows[0].matchSeconds / 60.0)
        advancesPerMinute = ([double](($rows | Measure-Object fiveMeterAdvances -Sum).Sum) / (2 * $matches)) / ([double]$rows[0].matchSeconds / 60.0)
        completedPassesPerMinute = ([double](($rows | Measure-Object completedPasses -Sum).Sum) / (2 * $matches)) / ([double]$rows[0].matchSeconds / 60.0)
        effectiveCommandCounts = $commands
    }
}

function Get-BaselineSummary([string]$Root, [int]$Matches) {
    $red = Get-Content -LiteralPath (Join-Path $Root 'Full-Red-onnx.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $navy = Get-Content -LiteralPath (Join-Path $Root 'Full-Navy-onnx.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $rows = @($red,$navy)
    $teamSummaries = @()
    $commands = [long[]]::new(6)
    foreach ($row in $rows) {
        $selected = @($row.matchResults | Select-Object -First $Matches)
        $score = [double](($selected | Measure-Object scoreValue -Average).Average)
        $teamSummaries += [PSCustomObject]@{ Team=$row.policyTeam; Score=$score }
        foreach ($match in $selected) {
            for ($index = 0; $index -lt 6; $index++) { $commands[$index] += [long]$match.effectiveCommandCounts[$index] }
        }
    }
    $all = @($rows | ForEach-Object { @($_.matchResults | Select-Object -First $Matches) })
    [ordered]@{
        matchesPerTeam = $Matches
        matchSeconds = [double]$rows[0].matchSeconds
        aggregateScoreRate = [double](($teamSummaries | Measure-Object Score -Average).Average)
        redScoreRate = [double]($teamSummaries | Where-Object Team -eq 'Red').Score
        navyScoreRate = [double]($teamSummaries | Where-Object Team -eq 'Navy').Score
        goalsFor = [long](($all | Measure-Object policyGoals -Sum).Sum)
        goalsAgainst = [long](($all | Measure-Object opponentGoals -Sum).Sum)
        validShotsPerMatch = [double](($all | Measure-Object validShots -Sum).Sum) / (2 * $Matches)
        advancesPerMatch = [double](($all | Measure-Object fiveMeterAdvances -Sum).Sum) / (2 * $Matches)
        completedPassesPerMatch = [double](($all | Measure-Object completedPasses -Sum).Sum) / (2 * $Matches)
        validShotsPerMinute = ([double](($all | Measure-Object validShots -Sum).Sum) / (2 * $Matches)) / ([double]$rows[0].matchSeconds / 60.0)
        advancesPerMinute = ([double](($all | Measure-Object fiveMeterAdvances -Sum).Sum) / (2 * $Matches)) / ([double]$rows[0].matchSeconds / 60.0)
        completedPassesPerMinute = ([double](($all | Measure-Object completedPasses -Sum).Sum) / (2 * $Matches)) / ([double]$rows[0].matchSeconds / 60.0)
        effectiveCommandCounts = $commands
    }
}

function Test-RelativeImprovement([double]$Current, [double]$Baseline) {
    if ($Baseline -le 0) { return $Current -gt 0 }
    return $Current -ge ($Baseline * 1.05)
}

$current = Get-CurrentSummary $currentRoot
$baseline = Get-BaselineSummary $baselineRoot ([int]$current.matchesPerTeam)
$comparison = Get-Content -LiteralPath (Join-Path $currentRoot 'comparison.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$improvements = [ordered]@{
    validShotsPerMinute = Test-RelativeImprovement $current.validShotsPerMinute $baseline.validShotsPerMinute
    advancesPerMinute = Test-RelativeImprovement $current.advancesPerMinute $baseline.advancesPerMinute
    completedPassesPerMinute = (Test-RelativeImprovement $current.completedPassesPerMinute $baseline.completedPassesPerMinute) `
        -and [long]$current.effectiveCommandCounts[1] -gt 0
}
$tacticalImprovementCount = @($improvements.GetEnumerator() | Where-Object Value).Count
$baselineRegression = [double]$baseline.aggregateScoreRate - [double]$current.aggregateScoreRate
$previousProgress = $null
if (-not [string]::IsNullOrWhiteSpace($PreviousAssessment)) {
    $previous = Get-Content -LiteralPath $PreviousAssessment -Raw -Encoding UTF8 | ConvertFrom-Json
    $previousProgress = [ordered]@{
        scoreRateDelta = [double]$current.aggregateScoreRate - [double]$previous.current.aggregateScoreRate
        validShotsRateDelta = [double]$current.validShotsPerMinute - [double]$previous.current.validShotsPerMinute
        advancesRateDelta = [double]$current.advancesPerMinute - [double]$previous.current.advancesPerMinute
        completedPassesRateDelta = [double]$current.completedPassesPerMinute - [double]$previous.current.completedPassesPerMinute
    }
}
$completionPassed = $Step -ge 300000 `
    -and [double]$current.aggregateScoreRate -ge 0.65 `
    -and [double]$current.redScoreRate -ge 0.55 `
    -and [double]$current.navyScoreRate -ge 0.55 `
    -and [long]$current.goalsFor -ge [long]$current.goalsAgainst `
    -and $baselineRegression -le 0.10 `
    -and $tacticalImprovementCount -ge 1 `
    -and [bool]$comparison.actionIntegrityPassed

$result = [ordered]@{
    schema = 'MNG-MS3-checkpoint-assessment-v1'
    step = $Step
    evaluatedUtc = [DateTime]::UtcNow.ToString('O')
    evidenceDirectory = $currentRoot
    current = $current
    ms2P0BaselineSameSeeds = $baseline
    aggregateRegressionFromBaseline = $baselineRegression
    tacticalRateImprovements = $improvements
    tacticalImprovementCount = $tacticalImprovementCount
    previousCheckpointProgress = $previousProgress
    actionIntegrityPassed = [bool]$comparison.actionIntegrityPassed
    minimum300kCompletionGatePassed = $completionPassed
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) { $OutputPath = Join-Path $currentRoot 'ms3-assessment.json' }
$result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
$result
