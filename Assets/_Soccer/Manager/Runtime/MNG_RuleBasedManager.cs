using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public readonly struct MNG_RuleDecision
    {
        public readonly MNG_Command Command;
        public readonly int PassReceiverSlot;
        public readonly bool HasShotTarget;
        public readonly string Reason;

        public MNG_RuleDecision(
            MNG_Command command,
            int passReceiverSlot,
            bool hasShotTarget,
            string reason)
        {
            Command = command;
            PassReceiverSlot = passReceiverSlot;
            HasShotTarget = hasShotTarget;
            Reason = reason;
        }
    }

    /// <summary>
    /// R0's deterministic, non-neural manager. It consumes the same world snapshot
    /// and emits the same six high-level commands as MNG_ManagerAgent, while the
    /// shared planner, executors, kick plates, possession and physics remain the
    /// only writers below the manager layer.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class MNG_RuleBasedManager : MonoBehaviour
    {
        public const int RuleVersion = 1;
        public const float DecisionIntervalSeconds = 0.5f;
        public const float MinimumCommandHoldSeconds = 1f;

        const float PressureDistance = 4f;
        const float ComfortablePassPressureDistance = 7f;
        const float NeutralChaseDistance = 18f;
        const float SuperiorReceiverSpace = 4f;
        const float UsefulPassProgress = 8f;
        const float FastBallTowardOwnGoal = 4f;
        const float LateMatchSeconds = 45f;
        const float RecentLossSeconds = 3f;

        [SerializeField] Team team;
        [SerializeField] MNG_MatchController matchController;
        [SerializeField, Min(DecisionIntervalSeconds)]
        float configuredDecisionIntervalSeconds = DecisionIntervalSeconds;

        float m_DecisionCountdown;

        public Team Team => team;
        public int DecisionCount { get; private set; }
        public MNG_RuleDecision LastDecision { get; private set; }
        public float ConfiguredDecisionIntervalSeconds => configuredDecisionIntervalSeconds;

        void Awake()
        {
            if (matchController == null) matchController = GetComponentInParent<MNG_MatchController>();
            if (matchController == null)
                throw new InvalidOperationException("MNG rule manager requires a match controller.");
        }

        void FixedUpdate()
        {
            if (!matchController.IsPlayActive) return;
            m_DecisionCountdown -= Time.fixedDeltaTime;
            if (m_DecisionCountdown > 0f) return;
            m_DecisionCountdown = configuredDecisionIntervalSeconds;
            if (!matchController.TryGetSnapshot(out var snapshot)) return;

            var decision = matchController.GetDecisionState(team);
            var targets = MNG_TacticalTargetResolver.Resolve(snapshot, team, decision);
            matchController.SetDecisionTargets(
                team,
                targets.HasPassTarget,
                targets.HasShotTarget,
                targets.PassReceiverSlot);

            LastDecision = Decide(snapshot, team, decision);
            if (!matchController.AcceptCommand(team, LastDecision.Command))
            {
                throw new InvalidOperationException(
                    $"MNG R0 rule selected masked command {LastDecision.Command}: {LastDecision.Reason}");
            }
            DecisionCount++;
        }

        public void Configure(Team configuredTeam, MNG_MatchController configuredMatch)
        {
            team = configuredTeam;
            matchController = configuredMatch ?? throw new ArgumentNullException(nameof(configuredMatch));
            m_DecisionCountdown = 0f;
            DecisionCount = 0;
            LastDecision = default;
        }

        public void ConfigureDecisionInterval(float seconds)
        {
            if (!MNG_MatchSnapshot.IsFinite(seconds) || seconds < DecisionIntervalSeconds)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            configuredDecisionIntervalSeconds = seconds;
            m_DecisionCountdown = 0f;
        }

        public static MNG_RuleDecision Decide(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_TeamDecisionState decision)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (decision == null) throw new ArgumentNullException(nameof(decision));
            snapshot.ValidateOrThrow();
            decision.ValidateOrThrow();

            var targets = MNG_TacticalTargetResolver.Resolve(snapshot, team, decision);
            var desired = SelectDesiredCommand(snapshot, team, decision, targets, out var reason, out var urgent);
            if (!IsAvailable(snapshot, team, decision, targets, desired))
            {
                desired = SelectSafeAvailableCommand(snapshot, team, decision, targets);
                reason = $"safe-{desired}";
                urgent = true;
            }

            if (!urgent
                && desired != decision.PreviousCommand
                && decision.CommandAgeSeconds < MinimumCommandHoldSeconds
                && IsAvailable(snapshot, team, decision, targets, decision.PreviousCommand))
            {
                desired = decision.PreviousCommand;
                reason = $"hold-{desired}";
            }

            return new MNG_RuleDecision(
                desired,
                targets.PassReceiverSlot,
                targets.HasShotTarget,
                reason);
        }

        static MNG_Command SelectDesiredCommand(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_TeamDecisionState decision,
            MNG_TacticalTargets targets,
            out string reason,
            out bool urgent)
        {
            var ownPossession = team == Team.Red ? MNG_Possession.Red : MNG_Possession.Navy;
            var opponentPossession = team == Team.Red ? MNG_Possession.Navy : MNG_Possession.Red;
            var attackSign = team == Team.Red ? 1f : -1f;
            var attackingDepth = snapshot.BallPosition.x * attackSign;
            var ownGoalDanger = attackingDepth <= -snapshot.FieldHalfLength * 0.42f;
            var criticalOwnGoalDanger = attackingDepth <= -snapshot.FieldHalfLength * 0.62f;
            var scoreDifference = team == Team.Red
                ? snapshot.RedScore - snapshot.NavyScore
                : snapshot.NavyScore - snapshot.RedScore;
            var lateMatch = snapshot.MatchRemainingSeconds <= LateMatchSeconds;
            var lateLeading = lateMatch && scoreDifference > 0;
            var lateTrailing = lateMatch && scoreDifference < 0;
            urgent = false;

            if (snapshot.BallStallRecoveryActive)
            {
                reason = "global-stall-recovery";
                urgent = true;
                return MNG_Command.ActiveRecover;
            }

            if (snapshot.Possession == ownPossession
                && snapshot.Carrier.IsValid
                && snapshot.Carrier.Team == team)
            {
                urgent = decision.PreviousCommand == MNG_Command.ActiveRecover
                    || decision.PreviousCommand == MNG_Command.ProtectBack;
                var carrier = snapshot.GetPlayer(team, snapshot.Carrier.Slot);
                if (carrier.IsHuman)
                {
                    reason = "human-carrier-support";
                    return MNG_Command.Balanced;
                }

                var kickReady = carrier.KickCooldownSeconds <= 0f;
                if (carrier.Role == MNG_PlayerRole.Keeper && kickReady)
                {
                    reason = targets.HasPassTarget
                        ? "keeper-distribute-pass"
                        : "keeper-clear-long";
                    urgent = true;
                    return targets.HasPassTarget
                        ? MNG_Command.PassBuild
                        : MNG_Command.AttemptShot;
                }

                if (targets.HasShotTarget && kickReady)
                {
                    reason = lateTrailing ? "late-shot-window" : "shot-window";
                    urgent = true;
                    return MNG_Command.AttemptShot;
                }

                var carrierPressure = NearestOpponentDistance(snapshot, team, carrier.Position);
                var forwardDribbleBlocked = MNG_TacticalTargetResolver.IsForwardDribbleBlocked(
                    snapshot, team, snapshot.Carrier.Slot);
                if (targets.HasPassTarget && kickReady)
                {
                    var receiver = snapshot.GetPlayer(team, targets.PassReceiverSlot);
                    var receiverPressure = NearestOpponentDistance(snapshot, team, receiver.Position);
                    var progress = (receiver.Position.x - carrier.Position.x) * attackSign;
                    var underPressure = carrierPressure <= PressureDistance;
                    var receiverClearlyBetter = progress >= UsefulPassProgress
                        && receiverPressure >= Mathf.Max(4f, carrierPressure + SuperiorReceiverSpace);
                    var pressuredOutlet = underPressure
                        && progress >= 3f
                        && receiverPressure >= carrierPressure + SuperiorReceiverSpace;
                    var safeForwardPass = carrierPressure >= ComfortablePassPressureDistance
                        && progress >= 4f
                        && receiverPressure >= 4f;
                    var blockedOutlet = forwardDribbleBlocked && receiverPressure >= 2.5f;
                    if (ownGoalDanger || blockedOutlet || pressuredOutlet
                        || receiverClearlyBetter || safeForwardPass
                        || (lateTrailing && progress >= 3f))
                    {
                        reason = ownGoalDanger ? "danger-pass"
                            : blockedOutlet ? "blocked-forward-pass"
                            : pressuredOutlet ? "pressure-pass"
                            : safeForwardPass ? "safe-forward-pass"
                            : lateTrailing ? "late-forward-pass"
                            : "open-forward-pass";
                        urgent = urgent || ownGoalDanger || blockedOutlet || pressuredOutlet;
                        return MNG_Command.PassBuild;
                    }
                }

                reason = lateLeading ? "controlled-carry" : "advance-space";
                return MNG_Command.AdvanceCarry;
            }

            if (snapshot.Possession == opponentPossession)
            {
                if (criticalOwnGoalDanger || (lateLeading && attackingDepth < 0f))
                {
                    reason = criticalOwnGoalDanger ? "protect-box" : "protect-late-lead";
                    urgent = criticalOwnGoalDanger;
                    return MNG_Command.ProtectBack;
                }

                var nearest = NearestOwnFieldPlayerDistance(snapshot, team, snapshot.BallPosition);
                var recentLoss = decision.SecondsSincePossessionLoss >= 0f
                    && decision.SecondsSincePossessionLoss <= RecentLossSeconds;
                if (recentLoss || lateTrailing || ownGoalDanger || nearest <= NeutralChaseDistance)
                {
                    reason = recentLoss ? "counter-press"
                        : lateTrailing ? "late-recover"
                        : ownGoalDanger ? "recover-danger"
                        : "press-reachable";
                    return MNG_Command.ActiveRecover;
                }

                reason = "shape-behind-ball";
                return MNG_Command.Balanced;
            }

            var ballTowardOwnGoal = snapshot.BallVelocity.x * attackSign <= -FastBallTowardOwnGoal;
            if (criticalOwnGoalDanger || (ownGoalDanger && ballTowardOwnGoal))
            {
                reason = ballTowardOwnGoal ? "protect-fast-loose-ball" : "protect-loose-ball";
                urgent = true;
                return MNG_Command.ProtectBack;
            }

            reason = lateTrailing ? "late-loose-ball" : "claim-loose-ball";
            return MNG_Command.ActiveRecover;
        }

        static MNG_Command SelectSafeAvailableCommand(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_TeamDecisionState decision,
            MNG_TacticalTargets targets)
        {
            if (IsAvailable(snapshot, team, decision, targets, MNG_Command.Balanced))
                return MNG_Command.Balanced;
            if (IsAvailable(snapshot, team, decision, targets, MNG_Command.ProtectBack))
                return MNG_Command.ProtectBack;
            if (IsAvailable(snapshot, team, decision, targets, MNG_Command.ActiveRecover))
                return MNG_Command.ActiveRecover;
            throw new InvalidOperationException("MNG R0 rule has no valid command.");
        }

        static bool IsAvailable(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_TeamDecisionState decision,
            MNG_TacticalTargets targets,
            MNG_Command command)
        {
            var hasAiCarrier = false;
            var kickReady = false;
            if (snapshot.Carrier.IsValid && snapshot.Carrier.Team == team)
            {
                var carrier = snapshot.GetPlayer(team, snapshot.Carrier.Slot);
                hasAiCarrier = carrier.Active
                    && !carrier.IsHuman
                    && decision.ControlMask[snapshot.Carrier.Slot];
                kickReady = hasAiCarrier && carrier.KickCooldownSeconds <= 0f;
            }

            var hasControllablePlayer = false;
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                if (decision.ControlMask[slot] && snapshot.GetPlayer(team, slot).Active)
                {
                    hasControllablePlayer = true;
                    break;
                }
            }

            return command switch
            {
                MNG_Command.AdvanceCarry => hasAiCarrier,
                MNG_Command.PassBuild => kickReady && targets.HasPassTarget,
                MNG_Command.AttemptShot => kickReady && targets.HasShotTarget,
                MNG_Command.ActiveRecover => hasControllablePlayer,
                MNG_Command.Balanced => true,
                MNG_Command.ProtectBack => true,
                _ => false
            };
        }

        static float NearestOwnFieldPlayerDistance(
            MNG_MatchSnapshot snapshot,
            Team team,
            Vector2 point)
        {
            var nearest = float.PositiveInfinity;
            for (var slot = 1; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var player = snapshot.GetPlayer(team, slot);
                if (player.Active) nearest = Mathf.Min(nearest, Vector2.Distance(player.Position, point));
            }
            return nearest;
        }

        static float NearestOpponentDistance(MNG_MatchSnapshot snapshot, Team team, Vector2 point)
        {
            var opponent = team == Team.Red ? Team.Navy : Team.Red;
            var nearest = float.PositiveInfinity;
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var player = snapshot.GetPlayer(opponent, slot);
                if (player.Active) nearest = Mathf.Min(nearest, Vector2.Distance(player.Position, point));
            }
            return nearest;
        }
    }
}
