using UnityEngine;

namespace MachineLearning.Soccer
{
    /// <summary>
    /// L2 event geometry only. No steering, automatic kick, reward delivery or
    /// replacement of the common possession detector. Caller snapshots the
    /// eligible receiver at the explicit strike and tracks continuous control.
    /// </summary>
    public static class SoccerCurriculumPassRules
    {
        public const float MinimumGoalAdvantage = 2f;
        public const float MinimumTargetDistance = 3f;
        public const float MaximumTargetDistance = 16f;
        public const float TargetLaneRadius = 2.5f;
        // Receiver contact happens before the ball reaches the receiver's center.
        // Keep target selection at >=3m, but accept >=2m of actual ball travel.
        public const float MinimumBallDisplacement = 2f;
        // L2 teaches a deliberate handoff, not post-reception ball retention.
        // Caller still requires the designated receiver to be the current confirmed carrier.
        public const float ReceiverControlSeconds = 0f;
        public const float ReceiverControlDistance = 2.4f;
        public const float MaximumFlightSeconds = 8f;

        // Tracks the first fixed receiver before any explicit kick. Control gaps
        // end attribution; reacquisition cannot create another penalty.
        public sealed class OpportunityLossTracker
        {
            bool initialized, retired;
            Vector3 initialPasser, initialReceiver;

            public bool Observe(bool sameConfirmedCarrier, bool anyKick,
                Vector3 passer, Vector3 receiver, Vector3 goal)
            {
                if (retired) return false;
                if (!sameConfirmedCarrier || anyKick || !IsFinite(passer)
                    || !IsFinite(receiver) || !IsFinite(goal))
                {
                    retired = true;
                    return false;
                }
                if (!initialized)
                {
                    retired = !IsAdvantageousTarget(passer, receiver, goal);
                    initialized = true;
                    initialPasser = passer;
                    initialReceiver = receiver;
                    return false;
                }
                if (IsAdvantageousTarget(passer, receiver, goal)) return false;
                retired = true;
                // Self-only loss with the receiver frozen, but no receiver-only
                // loss with the passer frozen. Backward travel is not penalized.
                return PlanarDistance(passer, goal) < PlanarDistance(initialPasser, goal)
                    && !IsAdvantageousTarget(passer, initialReceiver, goal)
                    && IsAdvantageousTarget(initialPasser, receiver, goal);
            }
        }

        [System.Flags]
        public enum TargetRejection
        {
            None = 0, InvalidPosition = 1, TooClose = 2, TooFar = 4, InsufficientAdvantage = 8
        }

        // Read-only diagnostic. Multiple reasons can coexist; never approves a pass.
        public static TargetRejection DiagnoseTarget(Vector3 passer, Vector3 receiver, Vector3 opposingGoal)
        {
            if (!IsFinite(passer) || !IsFinite(receiver) || !IsFinite(opposingGoal))
                return TargetRejection.InvalidPosition;
            var reasons = TargetRejection.None;
            var separation = PlanarDistance(passer, receiver);
            if (separation < MinimumTargetDistance) reasons |= TargetRejection.TooClose;
            if (separation > MaximumTargetDistance) reasons |= TargetRejection.TooFar;
            if (PlanarDistance(passer, opposingGoal) - PlanarDistance(receiver, opposingGoal)
                < MinimumGoalAdvantage) reasons |= TargetRejection.InsufficientAdvantage;
            return reasons;
        }

        public static bool IsAdvantageousTarget(
            Vector3 passer, Vector3 receiver, Vector3 opposingGoal)
        {
            if (!IsFinite(passer) || !IsFinite(receiver) || !IsFinite(opposingGoal))
                return false;
            var separation = PlanarDistance(passer, receiver);
            return separation >= MinimumTargetDistance && separation <= MaximumTargetDistance
                && PlanarDistance(passer, opposingGoal) - PlanarDistance(receiver, opposingGoal)
                    >= MinimumGoalAdvantage;
        }

        public static bool IsDirectedAtTarget(Vector3 ball, Vector3 kickDirection, Vector3 receiver)
        {
            if (!IsFinite(ball) || !IsFinite(kickDirection) || !IsFinite(receiver)) return false;
            kickDirection.y = 0f;
            if (kickDirection.sqrMagnitude < 0.0001f) return false;
            var offset = receiver - ball;
            offset.y = 0f;
            var direction = kickDirection.normalized;
            var forwardDistance = Vector3.Dot(offset, direction);
            return forwardDistance >= MinimumTargetDistance
                && forwardDistance <= MaximumTargetDistance
                && (offset - direction * forwardDistance).magnitude <= TargetLaneRadius;
        }

        // Reward-only quality for the predicted pre-kick direction or an actual
        // explicit strike. This never steers a kick and does not replace the
        // stricter lane / reception success predicates.
        public static float StrikeDirectionQuality(Vector3 passer, Vector3 ball,
            Vector3 direction, Vector3 receiver, Vector3 opposingGoal)
        {
            if (!IsAdvantageousTarget(passer, receiver, opposingGoal)
                || !IsFinite(ball) || !IsFinite(direction)) return 0f;
            direction.y = 0f;
            var offset = receiver - ball;
            offset.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || offset.sqrMagnitude < 0.0001f) return 0f;
            var alignment = Mathf.Clamp01(Vector3.Dot(direction.normalized, offset.normalized));
            return alignment * alignment;
        }

        // One team-wide high-water mark per round: no repeated kick or reacquisition farming.
        public static float DirectionQualityIncrement(float previousBest, float currentQuality)
        {
            if (!IsFinite(previousBest) || !IsFinite(currentQuality)) return 0f;
            return Mathf.Max(0f, Mathf.Clamp01(currentQuality) - Mathf.Clamp01(previousBest));
        }

        public static bool IsSuccessfulReception(
            bool eligibleExplicitStrike, bool receiverIsDifferentTeammate,
            bool receiverMatchesStrikeTarget, bool interruptedByOtherPlayer,
            Vector3 strikeBallPosition, Vector3 currentBallPosition,
            float flightSeconds, float continuousReceiverControlSeconds, float receiverBallDistance)
        {
            return eligibleExplicitStrike && receiverIsDifferentTeammate
                && receiverMatchesStrikeTarget && !interruptedByOtherPlayer
                && IsFinite(strikeBallPosition) && IsFinite(currentBallPosition)
                && IsFinite(flightSeconds) && flightSeconds >= 0f && flightSeconds <= MaximumFlightSeconds
                && IsFinite(continuousReceiverControlSeconds)
                && continuousReceiverControlSeconds >= ReceiverControlSeconds
                && continuousReceiverControlSeconds <= flightSeconds
                && IsFinite(receiverBallDistance) && receiverBallDistance >= 0f
                && receiverBallDistance <= ReceiverControlDistance
                && PlanarDistance(strikeBallPosition, currentBallPosition) >= MinimumBallDisplacement;
        }

        static float PlanarDistance(Vector3 first, Vector3 second)
        {
            var delta = first - second;
            delta.y = 0f;
            return delta.magnitude;
        }

        static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
