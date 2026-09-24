[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RunId,
    [Parameter(Mandatory = $true)][string]$CandidateId,
    [Parameter(Mandatory = $true)][string]$ModelPath,
    [ValidateSet('Diagnostic','Final')][string]$Mode = 'Diagnostic',
    [ValidateRange(1,2147483647)][int]$SeedOffset = 491001,
    [string]$EvidenceId = "$CandidateId-$($Mode.ToLowerInvariant())-eval-r001"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($RunId -notmatch '^MNG_MS(1|2|3)-\d{8}-r\d{3}$') { throw "Invalid MS1/MS2/MS3 Run ID: $RunId" }
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
$model = (Resolve-Path -LiteralPath $ModelPath).Path
$protocolPath = Join-Path $projectRoot 'Assets\_Soccer\Manager\Evaluation\MNG_MS2_Protocol_v1.json'
$protocol = Get-Content -LiteralPath $protocolPath -Raw -Encoding UTF8 | ConvertFrom-Json
$buildDirectory = Join-Path $projectRoot "Builds\MNG_MS\MS2-Preflight\$CandidateId"
$executable = Join-Path $buildDirectory 'MNG_MS2_Preflight.exe'
$buildInfo = Join-Path $buildDirectory 'evaluation-build-info.json'
$evidenceDirectory = Join-Path $projectRoot "Logs\MNG-MS\$EvidenceId"
if (Test-Path -LiteralPath $evidenceDirectory) { throw "Evaluation evidence already exists: $evidenceDirectory" }
New-Item -ItemType Directory -Path $evidenceDirectory | Out-Null

$buildArguments = @('-batchmode','-nographics','-quit','-projectPath',$projectRoot,
    '-mngRunId',$RunId,'-mngCandidateId',$CandidateId,'-mngModelPath',$model,
    '-executeMethod','MachineLearning.Soccer.Manager.Editor.MNG_MSBuilder.BuildMS2PreflightEvaluationBatch',
    '-logFile',(Join-Path $evidenceDirectory 'build.log'))
$build = Start-Process -FilePath $unity -ArgumentList $buildArguments -WindowStyle Hidden -Wait -PassThru
if ($build.ExitCode -ne 0) { throw "MS2 evaluation build failed: $($build.ExitCode)" }
if (-not (Test-Path -LiteralPath $executable) -or -not (Test-Path -LiteralPath $buildInfo)) {
    throw 'MS2 evaluation build artifacts are missing.'
}

$matches = if ($Mode -eq 'Final') { 20 } else { 10 }
$seconds = if ($Mode -eq 'Final') { 300 } else { 120 }
$processes = @()
foreach ($team in @('Red','Navy')) {
    foreach ($policyKind in @('onnx','uniform-valid-command')) {
        $stem = "Full-$team-$policyKind"
        $resultPath = Join-Path $evidenceDirectory "$stem.json"
        $logPath = Join-Path $evidenceDirectory "$stem-player.log"
        $arguments = @('-batchmode','-nographics','-logFile',$logPath,
            '-mngEvaluationOutput',$resultPath,'-mngPolicyTeam',$team,
            '-mngOpponentStrength','Full','-mngRandomPolicy',$(if($policyKind -eq 'onnx'){'false'}else{'true'}),
            '-mngEvaluationProtocol',[string]$protocol.protocol,
            '-mngEvaluationMatches',[string]$matches,
            '-mngEvaluationMatchSeconds',[string]$seconds,
            '-mngEvaluationSeedOffset',[string]$SeedOffset,
            '-mngPolicyAssistMode','none')
        $processes += Start-Process -FilePath $executable -ArgumentList $arguments `
            -WorkingDirectory $buildDirectory -WindowStyle Hidden -PassThru
    }
}
$processes | Wait-Process
foreach ($process in $processes) {
    $process.Refresh()
    if ($process.ExitCode -ne 0) { throw "MS2 evaluation process $($process.Id) failed: $($process.ExitCode)" }
}

$rows = foreach ($team in @('Red','Navy')) {
    foreach ($policyKind in @('onnx','uniform-valid-command')) {
        $path = Join-Path $evidenceDirectory "Full-$team-$policyKind.json"
        if (-not (Test-Path -LiteralPath $path)) { throw "Missing evaluation result: $path" }
        $value = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
        if ([int]$value.matches -ne $matches -or [double]$value.matchSeconds -ne $seconds) {
            throw "Evaluation contract mismatch: $path"
        }
        $value
    }
}
$onnx = @($rows | Where-Object policyKind -eq 'onnx')
$random = @($rows | Where-Object policyKind -eq 'uniform-valid-command')
$onnxRate = ($onnx | Measure-Object scoreRate -Average).Average
$randomRate = ($random | Measure-Object scoreRate -Average).Average
$goalsFor = ($onnx | Measure-Object goalsFor -Sum).Sum
$goalsAgainst = ($onnx | Measure-Object goalsAgainst -Sum).Sum
$redRate = [double]($onnx | Where-Object policyTeam -eq 'Red').scoreRate
$navyRate = [double]($onnx | Where-Object policyTeam -eq 'Navy').scoreRate
$gain = $onnxRate - $randomRate
$actionMismatchCount = 0L
$blockedPassOverrides = 0L
$explicitBlockedPassRewards = 0L
foreach ($row in $rows) {
    $raw = @($row.rawCommandCounts)
    $effective = @($row.effectiveCommandCounts)
    if ($raw.Count -ne $effective.Count) { throw 'MS2 action count vector size mismatch.' }
    for ($index = 0; $index -lt $raw.Count; $index++) {
        $actionMismatchCount += [Math]::Abs([long]$raw[$index] - [long]$effective[$index])
    }
    $blockedPassOverrides += [long]$row.blockedPassOverrides
    $explicitBlockedPassRewards += [long]$row.explicitBlockedPassRewards
}
$actionIntegrityPassed = $actionMismatchCount -eq 0 `
    -and $blockedPassOverrides -eq 0 `
    -and $explicitBlockedPassRewards -eq 0
$gate = $protocol.finalEvaluation
$passed = $onnxRate -ge [double]$gate.minimumAggregateScoreRate `
    -and $goalsFor -ge $goalsAgainst `
    -and $redRate -ge [double]$gate.minimumPerTeamScoreRate `
    -and $navyRate -ge [double]$gate.minimumPerTeamScoreRate `
    -and $gain -ge [double]$gate.minimumGainOverUniformRandom `
    -and $actionIntegrityPassed
$comparison = [ordered]@{
    protocol = [string]$protocol.protocol
    mode = $Mode
    runId = $RunId
    candidateId = $CandidateId
    modelSha256 = (Get-FileHash -LiteralPath $model -Algorithm SHA256).Hash.ToLowerInvariant()
    matchesPerTeamAndPolicy = $matches
    matchSeconds = $seconds
    seedOffset = $SeedOffset
    onnxScoreRate = $onnxRate
    randomScoreRate = $randomRate
    gainOverRandom = $gain
    onnxGoalsFor = $goalsFor
    onnxGoalsAgainst = $goalsAgainst
    redScoreRate = $redRate
    navyScoreRate = $navyRate
    rawEffectiveMismatchCount = $actionMismatchCount
    blockedPassOverrides = $blockedPassOverrides
    explicitBlockedPassRewards = $explicitBlockedPassRewards
    actionIntegrityPassed = $actionIntegrityPassed
    passed = $passed
}
$comparison | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $evidenceDirectory 'comparison.json')
$comparison
