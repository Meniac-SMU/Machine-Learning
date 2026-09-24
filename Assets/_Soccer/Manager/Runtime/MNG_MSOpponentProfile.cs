using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public enum MNG_MSOpponentStrength
    {
        Rescue = 0,
        Easy = 1,
        Medium = 2,
        Full = 3
    }

    [CreateAssetMenu(
        menuName = "Machine Learning/Soccer Manager/MS Opponent Profile",
        fileName = "MNG_MS_Opponent")]
    public sealed class MNG_MSOpponentProfile : ScriptableObject
    {
        [SerializeField] MNG_MSOpponentStrength strength = MNG_MSOpponentStrength.Full;
        [SerializeField, Range(0.05f, 1f)] float movementSpeedMultiplier = 1f;
        [SerializeField, Min(MNG_RuleBasedManager.DecisionIntervalSeconds)]
        float decisionIntervalSeconds = MNG_RuleBasedManager.DecisionIntervalSeconds;

        public MNG_MSOpponentStrength Strength => strength;
        public float MovementSpeedMultiplier => movementSpeedMultiplier;
        public float DecisionIntervalSeconds => decisionIntervalSeconds;

        public void Configure(
            MNG_MSOpponentStrength configuredStrength,
            float configuredMovementSpeedMultiplier,
            float configuredDecisionIntervalSeconds)
        {
            strength = configuredStrength;
            movementSpeedMultiplier = configuredMovementSpeedMultiplier;
            decisionIntervalSeconds = configuredDecisionIntervalSeconds;
            ValidateOrThrow();
        }

        public void ValidateOrThrow()
        {
            if (!MNG_MatchSnapshot.IsFinite(movementSpeedMultiplier)
                || movementSpeedMultiplier <= 0f
                || movementSpeedMultiplier > 1f)
                throw new InvalidOperationException(
                    "MS opponent movement multiplier must be finite and in (0, 1].");
            if (!MNG_MatchSnapshot.IsFinite(decisionIntervalSeconds)
                || decisionIntervalSeconds < MNG_RuleBasedManager.DecisionIntervalSeconds)
                throw new InvalidOperationException(
                    $"MS opponent decision interval must be at least "
                    + $"{MNG_RuleBasedManager.DecisionIntervalSeconds:R} seconds.");

            var expectedMovement = strength switch
            {
                MNG_MSOpponentStrength.Rescue => 0.20f,
                MNG_MSOpponentStrength.Easy => 0.35f,
                MNG_MSOpponentStrength.Medium => 0.60f,
                MNG_MSOpponentStrength.Full => 1f,
                _ => throw new ArgumentOutOfRangeException(nameof(strength))
            };
            var expectedInterval = strength switch
            {
                MNG_MSOpponentStrength.Rescue => 2f,
                MNG_MSOpponentStrength.Easy => 1.5f,
                MNG_MSOpponentStrength.Medium => 1f,
                MNG_MSOpponentStrength.Full => MNG_RuleBasedManager.DecisionIntervalSeconds,
                _ => throw new ArgumentOutOfRangeException(nameof(strength))
            };
            if (Mathf.Abs(movementSpeedMultiplier - expectedMovement) > 0.00001f
                || Mathf.Abs(decisionIntervalSeconds - expectedInterval) > 0.00001f)
                throw new InvalidOperationException(
                    $"MS {strength} profile must remain movement={expectedMovement:R}, "
                    + $"decisionInterval={expectedInterval:R}.");
        }
    }
}
