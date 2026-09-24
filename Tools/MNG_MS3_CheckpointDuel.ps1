[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$DuelId,
    [Parameter(Mandatory = $true)][string]$EarlierCandidateId,
    [Parameter(Mandatory = $true)][string]$EarlierModelPath,
    [Parameter(Mandatory = $true)][string]$FinalCandidateId,
    [Parameter(Mandatory = $true)][string]$FinalModelPath,
    [ValidateRange(1,1000)][int]$MatchesPerOrientation = 20,
    [ValidateRange(1,3600)][int]$MatchSeconds = 300,
    [ValidateRange(1,2147483647)][int]$SeedOffset = 493001
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($DuelId -notmatch '^[A-Za-z0-9][A-Za-z0-9._-]+$') {
    throw "Invalid MS3 checkpoint duel ID: $DuelId"
}
foreach ($candidate in @($EarlierCandidateId,$FinalCandidateId)) {
    if ($candidate -notmatch '^MNG_MS(2|3)-\d{8}-r\d{3}-step\d+$') {
        throw "Invalid MS3 checkpoint candidate ID: $candidate"
    }
}
if ($EarlierCandidateId -eq $FinalCandidateId) {
    throw 'Earlier and final checkpoint IDs must be distinct.'
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
$earlierModel = (Resolve-Path -LiteralPath $EarlierModelPath).Path
$finalModel = (Resolve-Path -LiteralPath $FinalModelPath).Path
$protocolPath = Join-Path $projectRoot `
    'Assets\_Soccer\Manager\Evaluation\MNG_MS3_CheckpointDuel_Protocol_v1.json'
$protocol = Get-Content -LiteralPath $protocolPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ([int]$protocol.matchesPerOrientation -ne $MatchesPerOrientation `
    -or [int]$protocol.matchSeconds -ne $MatchSeconds `
    -or [int]$protocol.seedOffset -ne $SeedOffset) {
    throw 'Requested checkpoint duel parameters diverge from the frozen protocol.'
}

$buildDirectory = Join-Path $projectRoot "Builds\MNG_MS\MS3-Checkpoint-Duels\$DuelId"
$executable = Join-Path $buildDirectory 'MNG_MS3_Checkpoint_Duel.exe'
$buildInfoPath = Join-Path $buildDirectory 'evaluation-build-info.json'
$evidenceDirectory = Join-Path $projectRoot "Logs\MNG-MS\MS3-checkpoint-duels-20260921\$DuelId"
if (Test-Path -LiteralPath $buildDirectory) {
    throw "Checkpoint duel build already exists; use a new DuelId: $buildDirectory"
}
if (Test-Path -LiteralPath $evidenceDirectory) {
    throw "Checkpoint duel evidence already exists; use a new DuelId: $evidenceDirectory"
}
New-Item -ItemType Directory -Path $evidenceDirectory -Force | Out-Null

$buildArguments = @(
    '-batchmode','-nographics','-quit','-projectPath',$projectRoot,
    '-mngDuelId',$DuelId,
    '-mngEarlierCandidateId',$EarlierCandidateId,
    '-mngEarlierModelPath',$earlierModel,
    '-mngFinalCandidateId',$FinalCandidateId,
    '-mngFinalModelPath',$finalModel,
    '-executeMethod',
    'MachineLearning.Soccer.Manager.Editor.MNG_MSBuilder.BuildMS3CheckpointDuelBatch',
    '-logFile',(Join-Path $evidenceDirectory 'build.log'))
$build = Start-Process -FilePath $unity -ArgumentList $buildArguments `
    -WindowStyle Hidden -Wait -PassThru
if ($build.ExitCode -ne 0) {
    throw "MS3 checkpoint duel build failed: $($build.ExitCode)"
}
if (-not(Test-Path -LiteralPath $executable) `
    -or -not(Test-Path -LiteralPath $buildInfoPath)) {
    throw 'MS3 checkpoint duel build artifacts are missing.'
}

$earlierSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $earlierModel).Hash.ToLowerInvariant()
$finalSha = (Get-FileHash -Algorithm SHA256 -LiteralPath $finalModel).Hash.ToLowerInvariant()
$buildInfo = Get-Content -LiteralPath $buildInfoPath -Raw -Encoding UTF8 | ConvertFrom-Json
if ($buildInfo.earlierCandidateId -ne $EarlierCandidateId `
    -or $buildInfo.finalCandidateId -ne $FinalCandidateId `
    -or $buildInfo.earlierModelSha256 -ne $earlierSha `
    -or $buildInfo.finalModelSha256 -ne $finalSha `
    -or $buildInfo.result -ne 'Succeeded' `
    -or [int]$buildInfo.errors -ne 0) {
    throw 'MS3 checkpoint duel build manifest mismatch.'
}

$processes = @()
foreach ($team in @('Red','Navy')) {
    $stem = "earlier-$($team.ToLowerInvariant())"
    $resultPath = Join-Path $evidenceDirectory "$stem.json"
    $logPath = Join-Path $evidenceDirectory "$stem-player.log"
    $arguments = @(
        '-batchmode','-nographics','-logFile',$logPath,
        '-mngEvaluationOutput',$resultPath,
        '-mngEarlierTeam',$team,
        '-mngEvaluationProtocol',[string]$protocol.protocol,
        '-mngEvaluationMatches',[string]$MatchesPerOrientation,
        '-mngEvaluationMatchSeconds',[string]$MatchSeconds,
        '-mngEvaluationSeedOffset',[string]$SeedOffset,
        '-mngPolicyAssistMode','none')
    $processes += Start-Process -FilePath $executable -ArgumentList $arguments `
        -WorkingDirectory $buildDirectory -WindowStyle Hidden -PassThru
}
$processes | Wait-Process
foreach ($process in $processes) {
    $process.Refresh()
    if ($process.ExitCode -ne 0) {
        throw "MS3 checkpoint duel process $($process.Id) failed: $($process.ExitCode)"
    }
}

$rows = foreach ($team in @('red','navy')) {
    $path = Join-Path $evidenceDirectory "earlier-$team.json"
    if (-not(Test-Path -LiteralPath $path)) {
        throw "Missing MS3 checkpoint duel result: $path"
    }
    $row = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($row.protocol -ne $protocol.protocol `
        -or $row.duelId -ne $DuelId `
        -or $row.earlierCandidateId -ne $EarlierCandidateId `
        -or $row.finalCandidateId -ne $FinalCandidateId `
        -or $row.earlierModelSha256 -ne $earlierSha `
        -or $row.finalModelSha256 -ne $finalSha `
        -or [int]$row.matches -ne $MatchesPerOrientation `
        -or [int]$row.matchSeconds -ne $MatchSeconds `
        -or -not[bool]$row.actionIntegrityPassed) {
        throw "MS3 checkpoint duel result contract mismatch: $path"
    }
    $row
}

function Sum-CommandVector([object[]]$Rows,[string]$Property) {
    $sum = [long[]]::new(6)
    foreach ($row in $Rows) {
        $values = @($row.$Property)
        if ($values.Count -ne 6) { throw "Command vector size mismatch: $Property" }
        for ($index = 0; $index -lt 6; $index++) {
            $sum[$index] += [long]$values[$index]
        }
    }
    return $sum
}

$totalMatches = [int](($rows | Measure-Object matches -Sum).Sum)
$earlierWins = [int](($rows | Measure-Object earlierWins -Sum).Sum)
$draws = [int](($rows | Measure-Object draws -Sum).Sum)
$earlierLosses = [int](($rows | Measure-Object earlierLosses -Sum).Sum)
$earlierGoals = [int](($rows | Measure-Object earlierGoals -Sum).Sum)
$finalGoals = [int](($rows | Measure-Object finalGoals -Sum).Sum)
$earlierScoreRate = ($earlierWins + 0.5 * $draws) / $totalMatches
$finalScoreRate = 1.0 - $earlierScoreRate
$blockedPassOverrides = [long](
    ($rows | Measure-Object earlierBlockedPassOverrides -Sum).Sum) + [long](
    ($rows | Measure-Object finalBlockedPassOverrides -Sum).Sum)
$explicitBlockedPassRewards = [long](
    ($rows | Measure-Object earlierExplicitBlockedPassRewards -Sum).Sum) + [long](
    ($rows | Measure-Object finalExplicitBlockedPassRewards -Sum).Sum)
$summary = [ordered]@{
    schema = 'MNG-MS3-checkpoint-duel-summary-v1'
    completedUtc = [DateTime]::UtcNow.ToString('O')
    protocol = [string]$protocol.protocol
    duelId = $DuelId
    earlierCandidateId = $EarlierCandidateId
    earlierModelSha256 = $earlierSha
    finalCandidateId = $FinalCandidateId
    finalModelSha256 = $finalSha
    matchesPerOrientation = $MatchesPerOrientation
    totalMatches = $totalMatches
    matchSeconds = $MatchSeconds
    seedOffset = $SeedOffset
    earlier = [ordered]@{
        wins = $earlierWins
        draws = $draws
        losses = $earlierLosses
        goalsFor = $earlierGoals
        goalsAgainst = $finalGoals
        scoreRate = $earlierScoreRate
        rawCommandCounts = Sum-CommandVector $rows 'earlierRawCommandCounts'
        effectiveCommandCounts = Sum-CommandVector $rows 'earlierEffectiveCommandCounts'
        passStrikes = [int](($rows | Measure-Object earlierPassStrikes -Sum).Sum)
        completedPasses = [int](($rows | Measure-Object earlierCompletedPasses -Sum).Sum)
        shotStrikes = [int](($rows | Measure-Object earlierShotStrikes -Sum).Sum)
        validShots = [int](($rows | Measure-Object earlierValidShots -Sum).Sum)
        fiveMeterAdvances = [int](($rows | Measure-Object earlierFiveMeterAdvances -Sum).Sum)
    }
    final = [ordered]@{
        wins = $earlierLosses
        draws = $draws
        losses = $earlierWins
        goalsFor = $finalGoals
        goalsAgainst = $earlierGoals
        scoreRate = $finalScoreRate
        rawCommandCounts = Sum-CommandVector $rows 'finalRawCommandCounts'
        effectiveCommandCounts = Sum-CommandVector $rows 'finalEffectiveCommandCounts'
        passStrikes = [int](($rows | Measure-Object finalPassStrikes -Sum).Sum)
        completedPasses = [int](($rows | Measure-Object finalCompletedPasses -Sum).Sum)
        shotStrikes = [int](($rows | Measure-Object finalShotStrikes -Sum).Sum)
        validShots = [int](($rows | Measure-Object finalValidShots -Sum).Sum)
        fiveMeterAdvances = [int](($rows | Measure-Object finalFiveMeterAdvances -Sum).Sum)
    }
    orientations = $rows
    rawEffectiveMismatchCount = 0
    blockedPassOverrides = $blockedPassOverrides
    explicitBlockedPassRewards = $explicitBlockedPassRewards
    actionIntegrityPassed = $true
    buildInfoPath = $buildInfoPath.Substring($projectRoot.Length + 1).Replace('\','/')
    executableSha256 = [string]$buildInfo.executableSha256
}

$earlierRaw = @($summary.earlier.rawCommandCounts)
$earlierEffective = @($summary.earlier.effectiveCommandCounts)
$finalRaw = @($summary.final.rawCommandCounts)
$finalEffective = @($summary.final.effectiveCommandCounts)
for ($index = 0; $index -lt 6; $index++) {
    $summary.rawEffectiveMismatchCount += [Math]::Abs(
        [long]$earlierRaw[$index] - [long]$earlierEffective[$index])
    $summary.rawEffectiveMismatchCount += [Math]::Abs(
        [long]$finalRaw[$index] - [long]$finalEffective[$index])
}
$summary.actionIntegrityPassed = $summary.rawEffectiveMismatchCount -eq 0 `
    -and $summary.blockedPassOverrides -eq 0 `
    -and $summary.explicitBlockedPassRewards -eq 0
if (-not $summary.actionIntegrityPassed) {
    throw 'MS3 checkpoint duel aggregate action integrity failed.'
}

$summaryPath = Join-Path $evidenceDirectory 'summary.json'
$summary | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $summaryPath -Encoding UTF8
$summary
