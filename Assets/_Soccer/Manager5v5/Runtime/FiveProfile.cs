using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    [CreateAssetMenu(menuName = "Soccer/5v5 Experimental Profile")]
    public sealed class FiveProfile : ScriptableObject
    {
        public const float Scale = .9f;
        [Min(.1f)] public float maxSpeed = 9f;
        [Min(1)] public float keeperSpeedMultiplier = 1.2f;
        [Min(1)] public float keeperAccelerationMultiplier = 1.2f;
        [Min(.1f)] public float acceleration = 30f;
        [Min(.1f)] public float deceleration = 45f;
        [Min(.1f)] public float turnRate = 125f;
        [Range(.1f,1)] public float dribbleTurnMultiplier = .64f;
        [Min(0)] public float staminaPerMetre = .00156f;
        [Min(0)] public float recoveryPerSecond = .03f;
        [Min(0)] public float slowRecoverySpeed = 3.6f;
        // Outer penalty markings measured on SM_Football_Field at the arena's 2.0217125 scale.
        [Min(.1f)] public float keeperAreaDepth = 17.7f;
        [Min(.1f)] public float keeperAreaHalfWidth = 21.1f;
        [Min(.1f)] public float decisionSeconds = 3f;
        [Min(1)] public float duration = 300f;
        [Min(0)] public float goalPause = 3f;
        [Header("Assisted technique experiments")]
        [Min(0)] public float freeBallDamping = .2f;
        [Min(1)] public float ballFrictionMultiplier = 2f;
        public float EffectiveFreeBallDamping => freeBallDamping * ballFrictionMultiplier;
        [Min(.1f)] public float maximumBallSpeed = 36.666667f;
        [Min(.001f)] public float ballContactOffset = .06f;
        [Min(.1f)] public float holdOffset = 1.188f;
        [Min(.1f)] public float acquirePadding = .5f;
        [Min(.1f)] public float releaseDistance = 2.8f;
        [Min(0)] public float releaseGrace = .24f;
        [Min(.1f)] public float dribbleGain = 12f;
        [Min(.1f)] public float dribbleAcceleration = 100f;
        [Min(0)] public float dribbleWallMargin = .16f;
        [Min(0)] public float challengeRecoverySeconds = .75f;
        [Header("Close dribble and soft front reception")]
        [Min(0)] public float dribblePlateGap = .01f;
        [Min(.1f)] public float closeDribbleGain = 30f;
        [Min(.1f)] public float closeDribbleAcceleration = 160f;
        [Min(.1f)] public float frontReceiveSpeed = 1.2f;
        [Header("Shared local contest relief")]
        [Min(.1f)] public float contestSlowSpeed = 1.8f;
        [Min(.1f)] public float contestConfirmSeconds = .65f;
        [Min(.1f)] public float contestRadius = 3.5f;
        [Min(.1f)] public float contestActionSeconds = 2.4f;
        [Min(0)] public float contestRestartGrace = 2f;
        [Min(.1f)] public float contestReinforceSeconds = 6f;
        [Min(.1f)] public float fieldWallClearance = 4f;

        public void Validate()
        {
            foreach (var value in new[] {maxSpeed, keeperSpeedMultiplier, keeperAccelerationMultiplier, dribbleTurnMultiplier, keeperAreaDepth, keeperAreaHalfWidth, acceleration, deceleration, turnRate, decisionSeconds, duration,
                maximumBallSpeed,ballContactOffset,holdOffset,acquirePadding,releaseDistance,dribbleGain,dribbleAcceleration,
                contestSlowSpeed,contestConfirmSeconds,contestRadius,contestActionSeconds,contestReinforceSeconds,fieldWallClearance,ballFrictionMultiplier,
                closeDribbleGain,closeDribbleAcceleration,frontReceiveSpeed})
                if (!float.IsFinite(value) || value <= 0) throw new System.InvalidOperationException("Invalid 5v5 profile.");
            foreach (var value in new[] {staminaPerMetre, recoveryPerSecond, slowRecoverySpeed, goalPause,freeBallDamping,releaseGrace,challengeRecoverySeconds,dribbleWallMargin,contestRestartGrace,dribblePlateGap})
                if (!float.IsFinite(value) || value < 0) throw new System.InvalidOperationException("Invalid 5v5 stamina/time.");
            if(contestReinforceSeconds<contestConfirmSeconds*3)throw new System.InvalidOperationException("Local players need time before reinforcements.");
        }
    }
}
