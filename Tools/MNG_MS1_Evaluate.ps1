[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RunId,
    [Parameter(Mandatory = $true)]
    [string]$CandidateId,
    [Parameter(Mandatory = $true)]
    [string]$ModelPath,
    [string]$EvidenceId = "$CandidateId-eval-r001"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$unity = 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
$model = (Resolve-Path -LiteralPath $ModelPath).Path
$protocolPath = Join-Path $projectRoot 'Assets\_Soccer\Manager\Evaluation\MNG_MS1_Protocol_v8.json'
$protocol = Get-Content -LiteralPath $protocolPath -Raw -Encoding UTF8 | ConvertFrom-Json
$buildDirectory = Join-Path $projectRoot "Builds\MNG_MS\MS1-Evaluation\$CandidateId"
$executable = Join-Path $buildDirectory 'MNG_MS1_Evaluation.exe'
$buildInfoPath = Join-Path $buildDirectory 'evaluation-build-info.json'
$evidenceDirectory = Join-Path $projectRoot "Logs\MNG-MS\$EvidenceId"
if (Test-Path -LiteralPath $evidenceDirectory) {
    throw "MS1 evaluation evidence already exists: $evidenceDirectory"
}
New-Item -ItemType Directory -Path $evidenceDirectory | Out-Null

$buildArguments = @(
    '-batchmode','-nographics','-quit','-projectPath',$projectRoot,
    '-mngRunId',$RunId,'-mngCandidateId',$CandidateId,'-mngModelPath',$model,
    '-executeMethod','MachineLearning.Soccer.Manager.Editor.MNG_MSBuilder.BuildMS1EvaluationBatch',
    '-logFile',(Join-Path $evidenceDirectory 'build.log')
)
$build = Start-Process -FilePath $unity -ArgumentList $buildArguments -WindowStyle Hidden -Wait -PassThru
if ($build.ExitCode -ne 0) { throw "MS1 evaluation build failed: $($build.ExitCode)" }
if (-not (Test-Path -LiteralPath $executable) -or -not (Test-Path -LiteralPath $buildInfoPath)) {
    throw 'MS1 evaluation build artifacts are missing.'
}

function Invoke-MS1Evaluation([string]$PolicyKind) {
    $random = $PolicyKind -eq 'uniform-valid-command'
    $resultPath = Join-Path $evidenceDirectory "$PolicyKind.json"
    $logPath = Join-Path $evidenceDirectory "$PolicyKind-player.log"
    $arguments = @('-batchmode','-nographics','-logFile',$logPath,
        '-mngEvaluationOutput',$resultPath,'-mngRandomPolicy',$(if($random){'true'}else{'false'}))
    $process = Start-Process -FilePath $executable -ArgumentList $arguments `
        -WorkingDirectory $buildDirectory -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0) { throw "$PolicyKind evaluation failed: $($process.ExitCode)" }
    if (-not (Test-Path -LiteralPath $resultPath)) { throw "$PolicyKind result is missing." }
    $result = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8 | ConvertFrom-Json
    if ([int]$result.episodes -ne 120 -or [int]$result.attackEpisodes -ne 40 `
        -or [int]$result.defenseEpisodes -ne 40) {
        throw "$PolicyKind evaluation episode contract failed."
    }
    return $result
}

$onnx = Invoke-MS1Evaluation 'onnx'
$random = Invoke-MS1Evaluation 'uniform-valid-command'
$attackGain = [int]$onnx.attackSuccesses - [int]$random.attackSuccesses
$defenseGain = [int]$onnx.defenseSuccesses - [int]$random.defenseSuccesses
$passGain = [int]$onnx.passSubsetCompletedPasses - [int]$random.passSubsetCompletedPasses
$gate = $protocol.finalEvaluation
$passed = [int]$onnx.attackSuccesses -ge [int]$gate.requiredAttackSuccesses `
    -and [int]$onnx.defenseSuccesses -ge [int]$gate.requiredDefenseSuccesses `
    -and $attackGain -ge -[int]$gate.maximumAttackDeficitEpisodes `
    -and $defenseGain -ge -[int]$gate.maximumDefenseDeficitEpisodes `
    -and [int]$onnx.passSubsetCompletedPasses -ge [int]$gate.requiredCompletedPasses `
    -and $passGain -ge [int]$gate.requiredPassImprovementCompletions
$comparison = [ordered]@{
    protocol = [string]$protocol.protocol
    protocolSha256 = (Get-FileHash -LiteralPath $protocolPath -Algorithm SHA256).Hash.ToLowerInvariant()
    runId = $RunId
    candidateId = $CandidateId
    modelSha256 = [string]$onnx.modelSha256
    onnxAttackSuccesses = [int]$onnx.attackSuccesses
    randomAttackSuccesses = [int]$random.attackSuccesses
    attackGainEpisodes = $attackGain
    onnxDefenseSuccesses = [int]$onnx.defenseSuccesses
    randomDefenseSuccesses = [int]$random.defenseSuccesses
    defenseGainEpisodes = $defenseGain
    passSubsetCompletedPasses = [int]$onnx.passSubsetCompletedPasses
    randomPassSubsetCompletedPasses = [int]$random.passSubsetCompletedPasses
    passGainCompletions = $passGain
    requiredAttackSuccesses = [int]$gate.requiredAttackSuccesses
    requiredDefenseSuccesses = [int]$gate.requiredDefenseSuccesses
    maximumAttackDeficitEpisodes = [int]$gate.maximumAttackDeficitEpisodes
    maximumDefenseDeficitEpisodes = [int]$gate.maximumDefenseDeficitEpisodes
    requiredCompletedPasses = [int]$gate.requiredCompletedPasses
    requiredPassImprovementCompletions = [int]$gate.requiredPassImprovementCompletions
    passed = $passed
}
$comparison | ConvertTo-Json -Depth 5 | Set-Content -Encoding UTF8 `
    -LiteralPath (Join-Path $evidenceDirectory 'comparison.json')
$comparison
