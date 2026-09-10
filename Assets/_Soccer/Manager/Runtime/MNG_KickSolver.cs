using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public static class MNG_KickSolver
    {
        public const float MinimumPassDistance = 5f;
        public const float MaximumPassDistance = 28f;
        public const float ControlledExitSpeed = 14f;
        public const float StrongExitSpeed = 28f;
        public const float MaximumBallSpeed = 30f;
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
