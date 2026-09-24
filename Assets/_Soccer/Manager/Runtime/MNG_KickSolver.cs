using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public static class MNG_KickSolver
    {
        public const float MinimumPassDistance = 5f;
        public const float MaximumPassDistance = 28f;
        public const float PreviousControlledExitSpeed = 14f;
        public const float PreviousStrongExitSpeed = 28f;
        public const float PreviousMaximumBallSpeed = 30f;
        public const float RequestedKickStrengthIncrease = 1000f;
        public const float PreviousPassStrengthEquivalent = 2000f;
        public const float PreviousShotStrengthEquivalent = 5000f;
        public const float PassStrengthEquivalent =
            PreviousPassStrengthEquivalent + RequestedKickStrengthIncrease;
        public const float ShotStrengthEquivalent =
            PreviousShotStrengthEquivalent + RequestedKickStrengthIncrease;
        public const float ReferenceFixedDeltaTime = 0.02f;
        public const float ReferenceBallMass = 3f;
        public const float KickStrengthExitSpeedIncrease =
            RequestedKickStrengthIncrease * ReferenceFixedDeltaTime / ReferenceBallMass;
        public const float ControlledExitSpeed =
            PreviousControlledExitSpeed + KickStrengthExitSpeedIncrease;
        public const float StrongExitSpeed =
            PreviousStrongExitSpeed + KickStrengthExitSpeedIncrease;
        public const float MaximumBallSpeed =
            PreviousMaximumBallSpeed + KickStrengthExitSpeedIncrease;
        public const float FullStrengthPassDistance = 20f;

        public static float PassExitSpeedForDistance(float distance)
        {
            if (!MNG_MatchSnapshot.IsFinite(distance)) return ControlledExitSpeed;
            var blend = Mathf.InverseLerp(10f, FullStrengthPassDistance, Mathf.Max(0f, distance));
            return Mathf.Lerp(ControlledExitSpeed, StrongExitSpeed, blend);
        }

        public static bool TrySolvePlanarImpulse(
            Vector3 currentVelocity,
            Vector3 direction,
            float requestedExitSpeed,
            float mass,
            out Vector3 impulse)
        {
            impulse = Vector3.zero;
            direction.y = 0f;
            currentVelocity.y = 0f;
            if (!IsFinite(direction) || !IsFinite(currentVelocity)
                || !MNG_MatchSnapshot.IsFinite(requestedExitSpeed)
                || !MNG_MatchSnapshot.IsFinite(mass)
                || mass <= 0f || requestedExitSpeed <= 0f
                || direction.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            direction.Normalize();
            var targetVelocity = direction * Mathf.Min(requestedExitSpeed, MaximumBallSpeed);
            impulse = mass * (targetVelocity - currentVelocity);
            return IsFinite(impulse);
        }

        static bool IsFinite(Vector3 value) => MNG_MatchSnapshot.IsFinite(value.x)
            && MNG_MatchSnapshot.IsFinite(value.y)
            && MNG_MatchSnapshot.IsFinite(value.z);
    }
}
