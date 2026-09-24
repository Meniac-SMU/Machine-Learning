param([Parameter(Mandatory=$true)][string]$Output)
$ErrorActionPreference = 'Stop'
if (Test-Path -LiteralPath $Output) { throw "Preserve existing evidence: $Output" }
$root = Split-Path $PSScriptRoot -Parent
$managed = Join-Path $root 'Builds/MNG_V2/R5R6-20260922-r2/EvaluationV2/MNG_EvaluationV2_Data/Managed'
foreach ($name in @('UnityEngine.CoreModule.dll','Soccer.Runtime.dll','MNG.Runtime.dll')) {
    [void][System.Reflection.Assembly]::LoadFrom((Join-Path $managed $name))
}
$runtimeHash = (Get-FileHash (Join-Path $managed 'MNG.Runtime.dll')).Hash.ToLowerInvariant()
if ($runtimeHash -ne '8505e2cfbab3dcc5a24f60685caac4302c3e4e64a979c310bcf67eb08ad1b598') { throw 'Unexpected R6 runtime' }
$flags = [System.Reflection.BindingFlags]'Static,NonPublic'
$planner = [MachineLearning.Soccer.Manager.MNG_TeamPlanner]
$anchorMethod = $planner.GetMethod('FormationAnchor', $flags)
$defenseMethod = $planner.GetMethod('DefensiveTarget', $flags)
$anchorRows = @()
$defenseRows = @()
$snapshot = [MachineLearning.Soccer.Manager.MNG_MatchSnapshot]::new()
foreach ($team in @([MachineLearning.Soccer.Team]::Red,[MachineLearning.Soccer.Team]::Navy)) {
    for ($slot = 0; $slot -lt 4; $slot++) {
        $state = [MachineLearning.Soccer.Manager.MNG_PlayerState]::new()
        $state.Active = $true
        $state.Role = [MachineLearning.Soccer.Manager.MNG_PlayerRole]$slot
        $snapshot.SetPlayer($team,$slot,$state)
    }
    foreach ($command in @([MachineLearning.Soccer.Manager.MNG_Command]::ActiveRecover,[MachineLearning.Soccer.Manager.MNG_Command]::Balanced,[MachineLearning.Soccer.Manager.MNG_Command]::ProtectBack)) {
        foreach ($slot in @(1,2,3)) {
            $role = [MachineLearning.Soccer.Manager.MNG_PlayerRole]$slot
            $a = $anchorMethod.Invoke($null,@($snapshot,$team,$role,$command))
            $d = $defenseMethod.Invoke($null,@($snapshot,$team,$slot,$command))
            $anchorRows += [ordered]@{team=$team.ToString();slot=$slot;command=$command.ToString();x=$a.x;y=$a.y}
            $defenseRows += [ordered]@{team=$team.ToString();slot=$slot;command=$command.ToString();x=$d.x;y=$d.y}
        }
    }
}
# Invoke the public P1 entry point on exact 180-degree team-swapped states.
$planRows = @()
$observations = @()
foreach ($team in @([MachineLearning.Soccer.Team]::Red,[MachineLearning.Soccer.Team]::Navy)) {
    $state = [MachineLearning.Soccer.Manager.MNG_MatchSnapshot]::new()
    $sign = if ($team -eq [MachineLearning.Soccer.Team]::Red) { 1 } else { -1 }
    $opponent = if ($sign -eq 1) { [MachineLearning.Soccer.Team]::Navy } else { [MachineLearning.Soccer.Team]::Red }
    $positions = @(@(-45,0),@(-12,-16),@(-12,16),@(-2,3))
    for ($slot = 0; $slot -lt 4; $slot++) {
        $p = [MachineLearning.Soccer.Manager.MNG_PlayerState]::new()
        $p.Active=$true; $p.Role=[MachineLearning.Soccer.Manager.MNG_PlayerRole]$slot
        $p.Position=[UnityEngine.Vector2]::new($sign*$positions[$slot][0],$sign*$positions[$slot][1])
        $p.Forward=[UnityEngine.Vector2]::new($sign,0)
        $state.SetPlayer($team,$slot,$p)
        $p.Position=[UnityEngine.Vector2]::new(-$p.Position.x,-$p.Position.y)
        $p.Forward=[UnityEngine.Vector2]::new(-$sign,0)
        $state.SetPlayer($opponent,$slot,$p)
    }
    $decision = [MachineLearning.Soccer.Manager.MNG_TeamDecisionState]::new()
    $tasks = [MachineLearning.Soccer.Manager.MNG_PlayerTask[]]::new(4)
    [MachineLearning.Soccer.Manager.MNG_TeamPlanner]::PlanV2($state,$team,[MachineLearning.Soccer.Manager.MNG_Command]::ProtectBack,$decision,1,0.75,$tasks)
    $obs = [float[]]::new(244)
    $scratch = [float[]]::new(133)
    [void][MachineLearning.Soccer.Manager.MNG_ObservationWriter]::WriteV2($state,$team,$decision,$obs,$scratch)
    $observations += ,$obs
    foreach ($slot in @(1,2,3)) {
        $planRows += [ordered]@{team=$team.ToString();slot=$slot;skill=$tasks[$slot].Skill.ToString();x=$tasks[$slot].Target.x;y=$tasks[$slot].Target.y}
    }
}
$observationMaxDifference = 0.0
for ($i=0; $i -lt 244; $i++) { $observationMaxDifference = [Math]::Max($observationMaxDifference,[Math]::Abs($observations[0][$i]-$observations[1][$i])) }
# An exact neutral acquisition tie must not be mistaken for spawn randomness.
$tieRows = @()
foreach ($reverse in @($false,$true)) {
    $ledger = [MachineLearning.Soccer.Manager.MNG_PossessionLedger]::new()
    $candidates = [MachineLearning.Soccer.Manager.MNG_PossessionCandidate[]]::new(2)
    for ($i = 0; $i -lt 2; $i++) {
        $c = [MachineLearning.Soccer.Manager.MNG_PossessionCandidate]::new()
        $c.Team=[MachineLearning.Soccer.Team]$i; $c.Slot=3; $c.Active=$true
        $c.HasPhysicalContact=$true; $c.IsInControlZone=$true
        $c.CenterDistance=0.1; $c.AcquisitionDistance=0.5
        $index = if ($reverse) { 1-$i } else { $i }
        $candidates[$index]=$c
    }
    for ($i=0; $i -lt 12; $i++) { $ledger.Update($candidates,2,0.02) }
    $tieRows += [ordered]@{reversedCandidateOrder=$reverse;owner=$ledger.Carrier.Team.ToString();valid=$ledger.Carrier.IsValid}
}
$escape = [MachineLearning.Soccer.Manager.MNG_ContestedBallEscape]::new()
$escapeRows = @()
for ($round=0; $round -lt 2; $round++) {
    $escape.Reset()
    for ($i=0; $i -lt 32; $i++) { [void]$escape.Update([UnityEngine.Vector2]::zero,$true,$true,0.02) }
    $escapeRows += [ordered]@{round=$round;active=$escape.IsActive;sequence=$escape.Sequence}
}
$shotRows = @()
foreach ($team in @([MachineLearning.Soccer.Team]::Red,[MachineLearning.Soccer.Team]::Navy)) {
    $target = [MachineLearning.Soccer.Manager.MNG_TeamPlanner]::SelectShotTarget($snapshot,$team,3)
    $shotRows += [ordered]@{team=$team.ToString();x=$target.x;y=$target.y}
}
$stall = [MachineLearning.Soccer.Manager.MNG_GlobalBallStallTracker]::new()
for ($i=0; $i -lt 65; $i++) { [void]$stall.Update([UnityEngine.Vector2]::zero,[UnityEngine.Vector2]::zero,0.02) }
$beforeReset = $stall.ActivationCount
$stall.Reset()
$stallReset = [ordered]@{beforeReset=$beforeReset;afterReset=$stall.ActivationCount;note='Match.ResetRound calls this Reset after goals; evaluator reads this counter at match end.'}
$result = [ordered]@{
    scope='Actual frozen R6 managed DLL pure logic calls; no Unity process, scene, PhysX, training or code mutation'
    runtimeSha256=$runtimeHash
    anchors=$anchorRows;defensiveTargets=$defenseRows;publicPlanV2=$planRows
    neutralAcquisitionTies=$tieRows;contestedEscapeReset=$escapeRows;centerlineShotTargets=$shotRows
    v2ObservationMaxDifference=$observationMaxDifference;globalStallCounterReset=$stallReset
}
$parent = Split-Path ([System.IO.Path]::GetFullPath($Output)) -Parent
[void][System.IO.Directory]::CreateDirectory($parent)
$result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $Output -Encoding utf8
$result | ConvertTo-Json -Depth 12
