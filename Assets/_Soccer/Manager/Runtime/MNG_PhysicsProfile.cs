using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    [CreateAssetMenu(menuName = "Machine Learning/Soccer Manager/Physics Profile", fileName = "MNG_Physics")]
    public sealed class MNG_PhysicsProfile : ScriptableObject
    {
        public const float RequiredBallScaleMultiplier = 1.10f;
        public const float RequiredBallMass = 4.5f;
        public const float InitialBallRestitution = 0.05f;
        public const float KickPlateReleasePadding = 0.30f;
        public const float KickPlateReleaseDelaySeconds = 0.08f;
        public const float KickPlatePossessionConfirmationSeconds = 0.02f;
        public const float KickPlatePreviousOwnerLockSeconds = 0.12f;

        [Header("Ball contract")]
        [SerializeField] float ballScaleMultiplier = RequiredBallScaleMultiplier;
        [SerializeField] float ballMass = RequiredBallMass;
        [SerializeField] float ballRestitution = InitialBallRestitution;
        [SerializeField] float maximumBallSpeed = MNG_KickSolver.MaximumBallSpeed;

        [Header("Player motor")]
        [SerializeField] float maximumPlayerSpeed = 9f;
        [SerializeField] float acceleration = 30f;
        [SerializeField] float deceleration = 45f;
        [SerializeField] float rotationDegreesPerSecond = 125f;
        [SerializeField] float arrivalSlowRadius = 1.5f;

        [Header("Possession")]
        [SerializeField] float acquisitionPadding = 0.20f;
        [SerializeField] float releasePadding = KickPlateReleasePadding;
        [SerializeField] float releaseDelaySeconds = KickPlateReleaseDelaySeconds;
        [SerializeField] float possessionConfirmationSeconds = KickPlatePossessionConfirmationSeconds;
        [SerializeField] float previousOwnerLockSeconds = KickPlatePreviousOwnerLockSeconds;

        [Header("Dribble")]
        [SerializeField] float dribbleAcceleration = 30f;
        [SerializeField] float dribbleDampingPerSecond = 8f;
        [SerializeField] float dribbleForwardOffset = 0.35f;

        public float BallScaleMultiplier => ballScaleMultiplier;
        public float BallMass => ballMass;
        public float BallRestitution => ballRestitution;
        public float MaximumBallSpeed => maximumBallSpeed;
        public float MaximumPlayerSpeed => maximumPlayerSpeed;
        public float Acceleration => acceleration;
        public float Deceleration => deceleration;
        public float RotationDegreesPerSecond => rotationDegreesPerSecond;
        public float ArrivalSlowRadius => arrivalSlowRadius;
        public float AcquisitionPadding => acquisitionPadding;
        public float ReleasePadding => releasePadding;
        public float ReleaseDelaySeconds => releaseDelaySeconds;
        public float PossessionConfirmationSeconds => possessionConfirmationSeconds;
        public float PreviousOwnerLockSeconds => previousOwnerLockSeconds;
        public float DribbleAcceleration => dribbleAcceleration;
        public float DribbleDampingPerSecond => dribbleDampingPerSecond;
        public float DribbleForwardOffset => dribbleForwardOffset;

        public void ApplyKickPlateTuning()
        {
            releasePadding = KickPlateReleasePadding;
            releaseDelaySeconds = KickPlateReleaseDelaySeconds;
            possessionConfirmationSeconds = KickPlatePossessionConfirmationSeconds;
            previousOwnerLockSeconds = KickPlatePreviousOwnerLockSeconds;
        }

        public void ValidateOrThrow()
        {
            RequireExact(ballScaleMultiplier, RequiredBallScaleMultiplier, nameof(ballScaleMultiplier));
            RequireExact(ballMass, RequiredBallMass, nameof(ballMass));
            RequireExact(ballRestitution, InitialBallRestitution, nameof(ballRestitution));
            RequirePositive(maximumBallSpeed, nameof(maximumBallSpeed));
            RequirePositive(maximumPlayerSpeed, nameof(maximumPlayerSpeed));
            RequirePositive(acceleration, nameof(acceleration));
            RequirePositive(deceleration, nameof(deceleration));
            RequirePositive(rotationDegreesPerSecond, nameof(rotationDegreesPerSecond));
            RequirePositive(arrivalSlowRadius, nameof(arrivalSlowRadius));
            RequireNonNegative(acquisitionPadding, nameof(acquisitionPadding));
            RequireExact(releasePadding, KickPlateReleasePadding, nameof(releasePadding));
            RequireExact(releaseDelaySeconds, KickPlateReleaseDelaySeconds, nameof(releaseDelaySeconds));
            RequireExact(possessionConfirmationSeconds, KickPlatePossessionConfirmationSeconds,
                nameof(possessionConfirmationSeconds));
            RequireExact(previousOwnerLockSeconds, KickPlatePreviousOwnerLockSeconds,
                nameof(previousOwnerLockSeconds));
            RequirePositive(dribbleAcceleration, nameof(dribbleAcceleration));
            RequireNonNegative(dribbleDampingPerSecond, nameof(dribbleDampingPerSecond));
            RequireNonNegative(dribbleForwardOffset, nameof(dribbleForwardOffset));
        }

        static void RequireExact(float value, float expected, string name)
        {
            if (!MNG_MatchSnapshot.IsFinite(value) || Mathf.Abs(value - expected) > 0.00001f)
                throw new InvalidOperationException($"{name} must remain exactly {expected} in MNG contract v1.");
        }

        static void RequirePositive(float value, string name)
        {
            if (!MNG_MatchSnapshot.IsFinite(value) || value <= 0f)
                throw new InvalidOperationException($"{name} must be finite and positive.");
        }

        static void RequireNonNegative(float value, string name)
        {
            if (!MNG_MatchSnapshot.IsFinite(value) || value < 0f)
                throw new InvalidOperationException($"{name} must be finite and non-negative.");
        }
    }
}
