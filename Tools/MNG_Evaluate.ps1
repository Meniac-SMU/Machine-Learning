[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RunId,

    [string]$EvidenceId,

    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256Lower([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $algorithm = [System.Security.Cryptography.SHA256]::Create()
        try {
            return ([System.BitConverter]::ToString($algorithm.ComputeHash($stream))).Replace('-', '').ToLowerInvariant()
        }
        finally {
            $algorithm.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

if ($RunId -notmatch '^MNG_M1Attack-\d{8}-r\d{3}$') {
    throw "Run ID must match MNG_M1Attack-YYYYMMDD-rNNN. Received: $RunId"
}
if ([string]::IsNullOrWhiteSpace($EvidenceId)) {
    $EvidenceId = "$RunId-eval-r001"
}
if ($EvidenceId -notmatch '^MNG_M1Attack-\d{8}-r\d{3}-eval-r\d{3}$') {
    throw "Evidence ID must match MNG_M1Attack-YYYYMMDD-rNNN-eval-rNNN. Received: $EvidenceId"
}

$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$resolvedUnity = (Resolve-Path -LiteralPath $UnityPath).Path
$modelPath = Join-Path $projectRoot "results\$RunId\MNG_Manager.onnx"
$buildDirectory = Join-Path $projectRoot "Builds\MNG_M1Evaluation\$RunId"
$executablePath = Join-Path $buildDirectory 'MNG_M1Evaluation.exe'
$levelDataPath = Join-Path $buildDirectory 'MNG_M1Evaluation_Data\level0'
$buildInfoPath = Join-Path $buildDirectory 'evaluation-build-info.json'
$evidenceDirectory = Join-Path $projectRoot "Logs\$EvidenceId"
$buildLogPath = Join-Path $evidenceDirectory 'Evaluation-Build.log'
$playerLogPath = Join-Path $evidenceDirectory 'Player.log'
$resultPath = Join-Path $evidenceDirectory 'model-evaluation.json'

if (-not (Test-Path -LiteralPath $modelPath -PathType Leaf)) {
    throw "Frozen ONNX model is missing: $modelPath"
}
if (Test-Path -LiteralPath $evidenceDirectory) {
    throw "Evaluation evidence directory already exists; use a new evidence revision: $evidenceDirectory"
}
if (Get-Process Unity -ErrorAction SilentlyContinue) {
    throw 'Close the Unity Editor before starting the MNG evaluation build.'
}

New-Item -ItemType Directory -Path $evidenceDirectory | Out-Null
$unityArguments = @(
    '-batchmode',
    '-nographics',
    '-projectPath', $projectRoot,
    '-mngRunId', $RunId,
    '-mngModelPath', $modelPath,
    '-executeMethod', 'MachineLearning.Soccer.Manager.Editor.MNG_TrainingBuildBuilder.BuildM1EvaluationBatch',
    '-logFile', $buildLogPath
)
$unityProcess = Start-Process `
    -FilePath $resolvedUnity `
    -ArgumentList $unityArguments `
    -WindowStyle Hidden `
    -Wait `
    -PassThru
if ($unityProcess.ExitCode -ne 0) {
    throw "MNG evaluation build failed with exit code $($unityProcess.ExitCode). See $buildLogPath"
}

foreach ($requiredPath in @($executablePath, $levelDataPath, $buildInfoPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "MNG evaluation build artifact is missing: $requiredPath"
    }
}
$buildInfo = Get-Content -Raw -Encoding UTF8 -LiteralPath $buildInfoPath | ConvertFrom-Json
$sourceHash = Get-Sha256Lower $modelPath
if ($sourceHash -ne [string]$buildInfo.modelSha256) {
    throw 'Frozen ONNX hash does not match the evaluation build manifest.'
}
if ((Get-Sha256Lower $executablePath) -ne [string]$buildInfo.executableSha256) {
    throw 'Evaluation executable hash does not match its build manifest.'
}
if ((Get-Sha256Lower $levelDataPath) -ne [string]$buildInfo.levelDataSha256) {
    throw 'Evaluation level-data hash does not match its build manifest.'
}

$playerArguments = @(
    '-batchmode',
    '-nographics',
    '-logFile', $playerLogPath,
    '-mngEvaluationOutput', $resultPath
)
$playerProcess = Start-Process `
    -FilePath $executablePath `
    -ArgumentList $playerArguments `
    -WorkingDirectory $buildDirectory `
    -WindowStyle Hidden `
    -Wait `
    -PassThru
if ($playerProcess.ExitCode -ne 0) {
    throw "MNG evaluation Player failed with exit code $($playerProcess.ExitCode). See $playerLogPath"
}
if (-not (Test-Path -LiteralPath $resultPath -PathType Leaf)) {
    throw "MNG evaluation did not create a result: $resultPath"
}

$result = Get-Content -Raw -Encoding UTF8 -LiteralPath $resultPath | ConvertFrom-Json
if ([int]$result.scenarios -ne 100) {
    throw "MNG evaluation did not finish exactly 100 scenarios: $($result.scenarios)"
}
if ([string]$result.runId -ne $RunId -or [string]$result.modelSha256 -ne $sourceHash) {
    throw 'MNG evaluation result identity does not match the frozen candidate.'
}

[PSCustomObject]@{
    RunId = [string]$result.runId
    ModelSha256 = [string]$result.modelSha256
    Scenarios = [int]$result.scenarios
    Goals = [int]$result.goals
    OwnGoals = [int]$result.ownGoals
    Timeouts = [int]$result.timeouts
    RequiredGoals = [int]$result.requiredGoals
    Passed = [bool]$result.passed
    EvidenceDirectory = $evidenceDirectory
}
