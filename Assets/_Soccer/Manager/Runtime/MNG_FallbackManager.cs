using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public readonly struct MNG_FallbackDecision
    {
        public readonly MNG_Command Command;
        public readonly int PassReceiverSlot;
        public readonly bool HasShotTarget;
        public readonly string Reason;

        public MNG_FallbackDecision(
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
    /// Explicit, non-neural opponent that ports the Core fallback priorities onto
    /// the MNG command/executor contract. It never registers with the trainer.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class MNG_FallbackManager : MonoBehaviour
    {
        public const float DecisionIntervalSeconds = 0.5f;

        [SerializeField] Team team;
        [SerializeField] MNG_MatchController matchController;
        float m_DecisionCountdown;

        public Team Team => team;
        public MNG_FallbackDecision LastDecision { get; private set; }

        void Awake()
        {
            if (matchController == null) matchController = GetComponentInParent<MNG_MatchController>();
            if (matchController == null) throw new InvalidOperationException("MNG fallback requires a match controller.");
        }

        void FixedUpdate()
        {
            if (!matchController.IsPlayActive) return;
            m_DecisionCountdown -= Time.fixedDeltaTime;
            if (m_DecisionCountdown > 0f) return;
            m_DecisionCountdown = DecisionIntervalSeconds;
            if (!matchController.TryGetSnapshot(out var snapshot)) return;

            LastDecision = Decide(snapshot, team);
            var hasPass = LastDecision.PassReceiverSlot >= 0;
            matchController.SetDecisionTargets(
                team,
                hasPass,
                LastDecision.HasShotTarget,
                LastDecision.PassReceiverSlot);
            if (!matchController.AcceptCommand(team, LastDecision.Command))
                throw new InvalidOperationException($"MNG fallback selected masked command {LastDecision.Command}.");
        }

        public void Configure(Team configuredTeam, MNG_MatchController configuredMatch)
        {
            team = configuredTeam;
            matchController = configuredMatch ?? throw new ArgumentNullException(nameof(configuredMatch));
            m_DecisionCountdown = 0f;
        }

        public static MNG_FallbackDecision Decide(MNG_MatchSnapshot snapshot, Team team)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            snapshot.ValidateOrThrow();
            var ownPossession = team == Team.Red ? MNG_Possession.Red : MNG_Possession.Navy;
            var opponentPossession = team == Team.Red ? MNG_Possession.Navy : MNG_Possession.Red;
            if (snapshot.Possession != ownPossession || !snapshot.Carrier.IsValid
                || snapshot.Carrier.Team != team)
            {
                return snapshot.Possession == opponentPossession
                    ? new MNG_FallbackDecision(MNG_Command.ActiveRecover, -1, false, "opponent-possession")
                    : new MNG_FallbackDecision(MNG_Command.Balanced, -1, false, "neutral-ball");
            }

            var carrier = snapshot.GetPlayer(team, snapshot.Carrier.Slot);
            if (carrier.IsHuman)
                return new MNG_FallbackDecision(MNG_Command.Balanced, -1, false, "human-carrier");

            var attackSign = team == Team.Red ? 1f : -1f;
            var targets = MNG_TacticalTargetResolver.Resolve(snapshot, team);
            if (targets.HasShotTarget && carrier.KickCooldownSeconds <= 0f)
                return new MNG_FallbackDecision(MNG_Command.AttemptShot, -1, true, "valid-near-shot");

            var passReceiver = targets.PassReceiverSlot;
            var attackingDepth = snapshot.BallPosition.x * attackSign;
            var ownGoalDanger = attackingDepth <= -48f;
            var underPressure = NearestOpponentDistance(snapshot, team, carrier.Position) <= 6f;
            var forwardDribbleBlocked = MNG_TacticalTargetResolver.IsForwardDribbleBlocked(
                snapshot, team, snapshot.Carrier.Slot);
            if ((ownGoalDanger || forwardDribbleBlocked || underPressure)
                && passReceiver >= 0
                && carrier.KickCooldownSeconds <= 0f)
            {
                return new MNG_FallbackDecision(
                    MNG_Command.PassBuild,
                    passReceiver,
                    false,
                    ownGoalDanger ? "danger-pass"
                        : forwardDribbleBlocked ? "blocked-forward-pass"
                        : "pressure-pass");
            }

            return new MNG_FallbackDecision(MNG_Command.AdvanceCarry, -1, false, "advance-carry");
        }

        static float NearestOpponentDistance(MNG_MatchSnapshot snapshot, Team team, Vector2 position)
        {
            var opponent = team == Team.Red ? Team.Navy : Team.Red;
            var nearest = float.PositiveInfinity;
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var player = snapshot.GetPlayer(opponent, slot);
                if (!player.Active) continue;
                nearest = Mathf.Min(nearest, Vector2.Distance(position, player.Position));
            }
            return nearest;
        }
    }
}
