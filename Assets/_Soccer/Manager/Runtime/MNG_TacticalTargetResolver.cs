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
        const float ReceiverLookAheadSeconds = 0.35f;
        const float MinimumLaneClearance = 1.25f;
        const float ShotGoalMargin = 0.5f;

        public static MNG_TacticalTargets Resolve(MNG_MatchSnapshot snapshot, Team team)
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
            var hasShot = Vector2.Distance(snapshot.BallPosition, goal) <= 24f
                && Mathf.Abs(snapshot.BallPosition.y) <= snapshot.GoalHalfWidth - ShotGoalMargin;
            return new MNG_TacticalTargets(
                FindPassReceiver(snapshot, team, snapshot.Carrier.Slot, attackSign),
                hasShot);
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
                if (!teammate.Active) continue;

                var predicted = teammate.Position
                    + Vector2.ClampMagnitude(teammate.Velocity, 9f) * ReceiverLookAheadSeconds;
                var offset = predicted - carrier.Position;
                var distance = offset.magnitude;
                if (distance < MNG_KickSolver.MinimumPassDistance
                    || distance > MNG_KickSolver.MaximumPassDistance
                    || offset.x * attackSign < -2f)
                    continue;

                var laneClearance = NearestOpponentToSegment(snapshot, team, carrier.Position, predicted);
                if (laneClearance < MinimumLaneClearance) continue;
                var receiverPressure = NearestOpponentDistance(snapshot, team, predicted);
                var score = offset.x * attackSign * 1.2f
                    + Mathf.Min(receiverPressure, 10f) * 0.7f
                    + Mathf.Min(laneClearance, 8f) * 0.35f
                    - distance * 0.12f;
                if (score <= bestScore) continue;
                bestScore = score;
                bestSlot = slot;
            }
            return bestSlot;
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

        static float NearestOpponentToSegment(
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
