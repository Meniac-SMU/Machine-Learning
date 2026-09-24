[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$EvidenceDirectory,
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$directory = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path (Split-Path -Parent $directory) 'p0-action-integrity.json'
}

$files = @(Get-ChildItem -LiteralPath $directory -Filter 'worker-*-process-*.jsonl' -File)
if ($files.Count -lt 32) { throw "P0 evidence requires at least 32 worker files; found $($files.Count)." }

$starts = @()
$episodes = @()
foreach ($file in $files) {
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $file.FullName -Encoding UTF8) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try { $event = $line | ConvertFrom-Json }
        catch { throw "Invalid JSON evidence: $($file.FullName):$lineNumber" }
        if ($event.event -eq 'worker-start') { $starts += $event }
        elseif ($event.event -eq 'episode') { $episodes += $event }
    }
}
if ($starts.Count -lt 32) { throw "P0 evidence has fewer than 32 worker-start events: $($starts.Count)." }
if ($episodes.Count -eq 0) { throw 'P0 evidence contains no completed training episode.' }

$mismatch = 0L
$overrides = 0L
$explicitRewards = 0L
$failedFlags = 0
$assistModeViolations = 0
foreach ($episode in $episodes) {
    $raw = @($episode.policyRawCommands)
    $effective = @($episode.policyEffectiveCommands)
    if ($raw.Count -ne 6 -or $effective.Count -ne 6) {
        throw "P0 episode command vectors must contain six entries: process=$($episode.process)."
    }
    for ($index = 0; $index -lt 6; $index++) {
        $mismatch += [Math]::Abs([long]$raw[$index] - [long]$effective[$index])
    }
    $overrides += [long]$episode.blockedPassOverrides
    $explicitRewards += [long]$episode.explicitBlockedPassRewards
    if (-not [bool]$episode.actionIntegrityPassed) { $failedFlags++ }
    if ([string]$episode.policyAssistMode -ne 'None') { $assistModeViolations++ }
}

$passed = $mismatch -eq 0 -and $overrides -eq 0 -and $explicitRewards -eq 0 `
    -and $failedFlags -eq 0 -and $assistModeViolations -eq 0
$result = [ordered]@{
    schema = 'MNG-MS2-P0-action-integrity-v1'
    checkedUtc = [DateTime]::UtcNow.ToString('O')
    evidenceDirectory = $directory
    workerFiles = $files.Count
    workerStartEvents = $starts.Count
    uniqueProcesses = @($starts.process | Sort-Object -Unique).Count
    completedEpisodes = $episodes.Count
    rawEffectiveMismatchCount = $mismatch
    blockedPassOverrides = $overrides
    explicitBlockedPassRewards = $explicitRewards
    failedIntegrityFlags = $failedFlags
    policyAssistModeViolations = $assistModeViolations
    passed = $passed
}
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
$result
if (-not $passed) { throw 'MS2 P0 training action-integrity proof failed.' }
