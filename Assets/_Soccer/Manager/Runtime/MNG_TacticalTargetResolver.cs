using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public readonly struct MNG_TacticalTargets
    {
        public readonly int PassReceiverSlot;
        public readonly bool HasShotTarget;

        public MNG_TacticalTargets(int passReceiverSlot, bool hasShotTarget)
        {
            PassReceiverSlot = passReceiverSlot;
            HasShotTarget = hasShotTarget;
        }

        public bool HasPassTarget => PassReceiverSlot >= 0;
    }

    /// <summary>
    /// Resolves code-owned pass and shot targets for both neural and fallback managers.
    /// It does not move a player or kick the ball, and it never chooses the policy command.
    /// </summary>
    public static class MNG_TacticalTargetResolver
    {
        const float MinimumLaneClearance = 1.25f;
        public const float ForwardDribbleProbeDistance = 8f;
        public const float ForwardDribbleLaneHalfWidth = 3f;
        public const float ForwardDribbleMinimumBlockDepth = 0.5f;
        public const float MaximumShotDistance = 24f;
        public const float ShotLateralMargin = 12f;

        public static MNG_TacticalTargets Resolve(
            MNG_MatchSnapshot snapshot,
            Team team,
            MNG_TeamDecisionState decision = null)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            snapshot.ValidateOrThrow();
            if (!snapshot.Carrier.IsValid || snapshot.Carrier.Team != team)
                return new MNG_TacticalTargets(-1, false);

            var carrier = snapshot.GetPlayer(team, snapshot.Carrier.Slot);
            if (!carrier.Active)
                return new MNG_TacticalTargets(-1, false);

            var attackSign = team == Team.Red ? 1f : -1f;
            var goal = new Vector2(attackSign * snapshot.FieldHalfLength, 0f);
            var keeperClearance = carrier.Role == MNG_PlayerRole.Keeper;
            var hasShot = keeperClearance
                || (Vector2.Distance(snapshot.BallPosition, goal) <= MaximumShotDistance
                    && Mathf.Abs(snapshot.BallPosition.y)
                    <= snapshot.GoalHalfWidth + ShotLateralMargin);
            return new MNG_TacticalTargets(
                FindPassReceiver(snapshot, team, snapshot.Carrier.Slot, attackSign),
                hasShot);
        }

        public static bool IsPassBuildReceiverValid(MNG_MatchSnapshot snapshot, Team team, int carrierSlot, int receiverSlot)
        {
            if (carrierSlot < 0 || carrierSlot >= 4 || receiverSlot < 0 || receiverSlot >= 4 || carrierSlot == receiverSlot) return false;
            var sign = team == Team.Red ? 1f : -1f;
            if (Vector2.Distance(snapshot.BallPosition, new Vector2(sign * snapshot.FieldHalfLength, 0)) <= MaximumShotDistance) return false;
            var carrier = snapshot.GetPlayer(team, carrierSlot);
            var receiver = snapshot.GetPlayer(team, receiverSlot);
            if (!carrier.Active || !receiver.Active || receiver.IsHuman || (receiver.Position.x - carrier.Position.x) * sign <= 0f) return false;
            return IsPassBuildLaneOpen(snapshot, team, MNG_TeamPlanner.SelectPassTarget(snapshot, team, receiverSlot));
        }

        public static bool IsPassBuildLaneOpen(MNG_MatchSnapshot snapshot, Team team, Vector2 target)
        {
            var distance = Vector2.Distance(snapshot.BallPosition, target);
            return distance >= MNG_KickSolver.MinimumPassDistance && distance <= MNG_KickSolver.MaximumPassDistance
                && NearestOpponentToSegment(snapshot, team, snapshot.BallPosition, target) >= MinimumLaneClearance;
        }

        static int FindPassReceiver(MNG_MatchSnapshot snapshot, Team team, int carrierSlot, float attackSign)
        {
            var carrier = snapshot.GetPlayer(team, carrierSlot);
            var bestSlot = -1;
            var bestScore = float.NegativeInfinity;
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                if (slot == carrierSlot) continue;
                var teammate = snapshot.GetPlayer(team, slot);
                if (!IsPassBuildReceiverValid(snapshot, team, carrierSlot, slot)) continue;

                // Score the same lead point the executor will actually kick to.
                var predicted = MNG_TeamPlanner.SelectPassTarget(snapshot, team, slot);
                var offset = predicted - carrier.Position;
                var distance = offset.magnitude;
                if (distance < MNG_KickSolver.MinimumPassDistance
                    || distance > MNG_KickSolver.MaximumPassDistance)
                    continue;

                var laneClearance = NearestOpponentToSegment(snapshot, team, carrier.Position, predicted);
                var receiverPressure = NearestOpponentDistance(snapshot, team, predicted);
                var progress = offset.x * attackSign;
                var backwardPenalty = Mathf.Max(0f, -progress) * 1.4f;
                var laneRiskPenalty = Mathf.Max(0f, MinimumLaneClearance - laneClearance) * 6f;
                var score = progress * 1.2f
                    + Mathf.Min(receiverPressure, 10f) * 0.7f
                    + Mathf.Min(laneClearance, 8f) * 0.35f
                    - distance * 0.12f
                    - backwardPenalty
                    - laneRiskPenalty;
                if (score <= bestScore) continue;
                bestScore = score;
                bestSlot = slot;
            }
            return bestSlot;
        }

        /// <summary>
        /// Reports whether an active opponent occupies the carrier's immediate
        /// attacking corridor. Both the neural and rule managers use this same
        /// geometry when deciding whether a pass should be considered before a
        /// lateral or backward carry route.
        /// </summary>
        public static bool IsForwardDribbleBlocked(
            MNG_MatchSnapshot snapshot,
            Team team,
            int carrierSlot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (carrierSlot < 0 || carrierSlot >= MNG_MatchSnapshot.PlayersPerTeam)
                throw new ArgumentOutOfRangeException(nameof(carrierSlot));
            var carrier = snapshot.GetPlayer(team, carrierSlot);
            if (!carrier.Active) return false;

            var attackSign = team == Team.Red ? 1f : -1f;
            var opponent = team == Team.Red ? Team.Navy : Team.Red;
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var player = snapshot.GetPlayer(opponent, slot);
                if (!player.Active) continue;
                var offset = player.Position - carrier.Position;
                var forward = offset.x * attackSign;
                if (forward < ForwardDribbleMinimumBlockDepth
                    || forward > ForwardDribbleProbeDistance)
                    continue;
                if (Mathf.Abs(offset.y) <= ForwardDribbleLaneHalfWidth) return true;
            }
            return false;
        }

        static float NearestOpponentDistance(MNG_MatchSnapshot snapshot, Team team, Vector2 point)
        {
            var opponent = team == Team.Red ? Team.Navy : Team.Red;
            var nearest = float.PositiveInfinity;
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var player = snapshot.GetPlayer(opponent, slot);
                if (player.Active) nearest = Mathf.Min(nearest, Vector2.Distance(point, player.Position));
            }
            return nearest;
        }

        public static float NearestOpponentToSegment(
            MNG_MatchSnapshot snapshot,
            Team team,
            Vector2 start,
            Vector2 end)
        {
            var opponent = team == Team.Red ? Team.Navy : Team.Red;
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            var nearest = float.PositiveInfinity;
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var player = snapshot.GetPlayer(opponent, slot);
                if (!player.Active) continue;
                var t = lengthSquared > 0.0001f
                    ? Mathf.Clamp01(Vector2.Dot(player.Position - start, segment) / lengthSquared)
                    : 0f;
                nearest = Mathf.Min(nearest, Vector2.Distance(player.Position, start + segment * t));
            }
            return nearest;
        }
    }
}
