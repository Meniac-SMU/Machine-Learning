[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RunId,
    [Parameter(Mandatory = $true)][int]$Step,
    [Parameter(Mandatory = $true)][string]$ModelPath,
    [int]$SeedOffset = 491001,
    [string]$PreviousAssessment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($RunId -notmatch '^MNG_MS3-\d{8}-r\d{3}$') { throw "Invalid MS3 Run ID: $RunId" }
if ($Step -le 0 -or $Step % 100000 -ne 0) { throw 'MS3 detailed evaluation step must be a positive 100k multiple.' }
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$mode = if ($Step -ge 300000) { 'Final' } else { 'Diagnostic' }
$candidate = "$RunId-step$Step"
$evidenceId = "$candidate-ms3-r0-$($mode.ToLowerInvariant())-seed$SeedOffset"
$evidenceDirectory = Join-Path $projectRoot "Logs\MNG-MS\$evidenceId"

& (Join-Path $PSScriptRoot 'MNG_MS2_Evaluate.ps1') `
    -RunId $RunId `
    -CandidateId $candidate `
    -ModelPath $ModelPath `
    -Mode $mode `
    -SeedOffset $SeedOffset `
    -EvidenceId $evidenceId
if (-not $?) { throw 'MS3 R0 evaluation script failed.' }

$assessmentArguments = @{
    CurrentEvidenceDirectory = $evidenceDirectory
    Step = $Step
    OutputPath = (Join-Path $evidenceDirectory 'ms3-assessment.json')
}
if (-not [string]::IsNullOrWhiteSpace($PreviousAssessment)) {
    $assessmentArguments.PreviousAssessment = $PreviousAssessment
}
& (Join-Path $PSScriptRoot 'MNG_MS3_Assess.ps1') @assessmentArguments
