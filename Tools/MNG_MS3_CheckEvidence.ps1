[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$EvidenceDirectory,
    [string]$TrainerLog,
    [string]$OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$directory = (Resolve-Path -LiteralPath $EvidenceDirectory).Path
$files = @(Get-ChildItem -LiteralPath $directory -Filter 'worker-*.jsonl' -File)
if ($files.Count -lt 32) { throw "MS3 requires at least 32 worker evidence files; found $($files.Count)." }

$starts = 0
$episodes = 0
$processes = [System.Collections.Generic.HashSet[int]]::new()
$violations = [System.Collections.Generic.List[string]]::new()
$redCommands = [long[]]::new(6)
$navyCommands = [long[]]::new(6)

foreach ($file in $files) {
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $file.FullName -Encoding UTF8) {
        $lineNumber++
        if ([string]::IsNullOrWhiteSpace($line)) { continue }
        try { $event = $line | ConvertFrom-Json }
        catch { $violations.Add("invalid-json:$($file.Name):$lineNumber"); continue }
        if ($null -ne $event.process) { [void]$processes.Add([int]$event.process) }
        if ($event.event -eq 'worker-start') {
            $starts++
            if ($event.mode -ne 'SelfPlay' -or [int]$event.policyCount -ne 2 -or [int]$event.ruleCount -ne 0) {
                $violations.Add("invalid-start:$($file.Name):$lineNumber")
            }
            continue
        }
        if ($event.event -ne 'episode') { continue }
        $episodes++
        foreach ($team in @('red','navy')) {
            $prefix = "${team}Policy"
            $raw = @($event."${prefix}RawCommands")
            $effective = @($event."${prefix}EffectiveCommands")
            if ($event."${prefix}AssistMode" -ne 'None') {
                $violations.Add("assist:${team}:$($file.Name):${lineNumber}")
            }
            if ($raw.Count -ne 6 -or $effective.Count -ne 6) {
                $violations.Add("vector:${team}:$($file.Name):${lineNumber}")
                continue
            }
            for ($index = 0; $index -lt 6; $index++) {
                if ([long]$raw[$index] -ne [long]$effective[$index]) {
                    $violations.Add("mismatch:${team}:$($file.Name):${lineNumber}:$index")
                }
                if ($team -eq 'red') { $redCommands[$index] += [long]$effective[$index] }
                else { $navyCommands[$index] += [long]$effective[$index] }
            }
            if ([long]$event."${prefix}BlockedPassOverrides" -ne 0) {
                $violations.Add("override:${team}:$($file.Name):${lineNumber}")
            }
            if ([long]$event."${prefix}ExplicitBlockedPassRewards" -ne 0) {
                $violations.Add("direct-reward:${team}:$($file.Name):${lineNumber}")
            }
            if (-not [bool]$event."${prefix}IntegrityPassed") {
                $violations.Add("integrity:${team}:$($file.Name):${lineNumber}")
            }
        }
        if (-not [bool]$event.actionIntegrityPassed) {
            $violations.Add("episode-integrity:$($file.Name):$lineNumber")
        }
    }
}

$trainerText = if ([string]::IsNullOrWhiteSpace($TrainerLog) -or -not (Test-Path -LiteralPath $TrainerLog)) {
    ''
} else { Get-Content -LiteralPath $TrainerLog -Raw -Encoding UTF8 }
$snapshotSwaps = ([regex]::Matches($trainerText, 'Swapping snapshot')).Count
$teamChanges = ([regex]::Matches($trainerText, 'Learning team [01] swapped')).Count
$connectedTeam0 = $trainerText -match 'MNG_Manager\?team=0'
$connectedTeam1 = $trainerText -match 'MNG_Manager\?team=1'
$passed = $starts -ge 32 -and $processes.Count -ge 32 -and $violations.Count -eq 0 `
    -and $connectedTeam0 -and $connectedTeam1
$result = [ordered]@{
    schema = 'MNG-MS3-action-integrity-v1'
    checkedUtc = [DateTime]::UtcNow.ToString('O')
    evidenceDirectory = $directory
    workerFiles = $files.Count
    workerStartEvents = $starts
    uniqueProcesses = $processes.Count
    completedEpisodes = $episodes
    redCommandCounts = $redCommands
    navyCommandCounts = $navyCommands
    snapshotSwapEvents = $snapshotSwaps
    learningTeamChangeEvents = $teamChanges
    connectedTeam0 = $connectedTeam0
    connectedTeam1 = $connectedTeam1
    violations = $violations.ToArray()
    passed = $passed
}
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path (Split-Path -Parent $directory) 'ms3-action-integrity.json'
}
$result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $OutputPath -Encoding UTF8
$result
if (-not $passed) { throw "MS3 action-integrity evidence failed. See $OutputPath" }
