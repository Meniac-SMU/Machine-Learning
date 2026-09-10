using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public enum MNG_CurriculumStage
    {
        M1AttackChoice = 1,
        M2DefenseChoice = 2,
        M3FallbackMatch = 3
    }

    [CreateAssetMenu(
        menuName = "Machine Learning/Soccer Manager/Curriculum Catalog",
        fileName = "MNG_CurriculumCatalog")]
    public sealed class MNG_CurriculumCatalog : ScriptableObject
    {
        public const float M1EpisodeSeconds = 20f;
        public const float M1MinimumGoalDistance = 20f;
        public const float M1MaximumGoalDistance = 35f;
        public const int M1TrainingSeed = 11001;
        public const int M1ValidationSeed = 21001;
        public const int M1EvaluationScenarioCount = 100;
        public const float M1TimeScale = 20f;
        public const float M2EpisodeSeconds = 20f;
        public const float M2MinimumThreatDistance = 18f;
        public const float M2MaximumThreatDistance = 35f;
        public const float M2RecoveryDeadlineSeconds = 10f;
        public const float M2OpponentPossessionSeconds = 0.30f;
        public const float M2RecoveryPossessionSeconds = 0.20f;
        public const int M2TrainingSeed = 12001;
        public const int M2ValidationSeed = 22001;
        public const int M2EvaluationScenarioCount = 100;
        public const float M2TimeScale = 20f;
        public const float M3TrainingMatchSeconds = 60f;
        public const float M3FullMatchSeconds = 300f;
        public const int M3TrainingSeed = 13001;
        public const int M3ValidationSeed = 23001;
        public const int M3FormalEvaluationMatches = 40;
        public const float M3TimeScale = 20f;

        [SerializeField] float m1EpisodeSeconds = M1EpisodeSeconds;
        [SerializeField] float m1MinimumGoalDistance = M1MinimumGoalDistance;
        [SerializeField] float m1MaximumGoalDistance = M1MaximumGoalDistance;
        [SerializeField] int m1TrainingSeed = M1TrainingSeed;
        [SerializeField] int m1ValidationSeed = M1ValidationSeed;
        [SerializeField] int m1EvaluationScenarioCount = M1EvaluationScenarioCount;
        [SerializeField] float m1TimeScale = M1TimeScale;
        [SerializeField] float m2EpisodeSeconds = M2EpisodeSeconds;
        [SerializeField] float m2MinimumThreatDistance = M2MinimumThreatDistance;
        [SerializeField] float m2MaximumThreatDistance = M2MaximumThreatDistance;
        [SerializeField] float m2RecoveryDeadlineSeconds = M2RecoveryDeadlineSeconds;
        [SerializeField] float m2OpponentPossessionSeconds = M2OpponentPossessionSeconds;
        [SerializeField] float m2RecoveryPossessionSeconds = M2RecoveryPossessionSeconds;
        [SerializeField] int m2TrainingSeed = M2TrainingSeed;
        [SerializeField] int m2ValidationSeed = M2ValidationSeed;
        [SerializeField] int m2EvaluationScenarioCount = M2EvaluationScenarioCount;
        [SerializeField] float m2TimeScale = M2TimeScale;
        [SerializeField] float m3TrainingMatchSeconds = M3TrainingMatchSeconds;
        [SerializeField] float m3FullMatchSeconds = M3FullMatchSeconds;
        [SerializeField] int m3TrainingSeed = M3TrainingSeed;
        [SerializeField] int m3ValidationSeed = M3ValidationSeed;
        [SerializeField] int m3FormalEvaluationMatches = M3FormalEvaluationMatches;
        [SerializeField] float m3TimeScale = M3TimeScale;

        public float EpisodeSeconds => m1EpisodeSeconds;
        public float MinimumGoalDistance => m1MinimumGoalDistance;
        public float MaximumGoalDistance => m1MaximumGoalDistance;
        public int TrainingSeed => m1TrainingSeed;
        public int ValidationSeed => m1ValidationSeed;
        public int EvaluationScenarioCount => m1EvaluationScenarioCount;
        public float TimeScale => m1TimeScale;
        public float M2RecoveryDeadline => m2RecoveryDeadlineSeconds;

        public void ApplyM1Defaults()
        {
            m1EpisodeSeconds = M1EpisodeSeconds;
            m1MinimumGoalDistance = M1MinimumGoalDistance;
            m1MaximumGoalDistance = M1MaximumGoalDistance;
            m1TrainingSeed = M1TrainingSeed;
            m1ValidationSeed = M1ValidationSeed;
            m1EvaluationScenarioCount = M1EvaluationScenarioCount;
            m1TimeScale = M1TimeScale;
            m2EpisodeSeconds = M2EpisodeSeconds;
            m2MinimumThreatDistance = M2MinimumThreatDistance;
            m2MaximumThreatDistance = M2MaximumThreatDistance;
            m2RecoveryDeadlineSeconds = M2RecoveryDeadlineSeconds;
            m2OpponentPossessionSeconds = M2OpponentPossessionSeconds;
            m2RecoveryPossessionSeconds = M2RecoveryPossessionSeconds;
            m2TrainingSeed = M2TrainingSeed;
            m2ValidationSeed = M2ValidationSeed;
            m2EvaluationScenarioCount = M2EvaluationScenarioCount;
            m2TimeScale = M2TimeScale;
            m3TrainingMatchSeconds = M3TrainingMatchSeconds;
            m3FullMatchSeconds = M3FullMatchSeconds;
            m3TrainingSeed = M3TrainingSeed;
            m3ValidationSeed = M3ValidationSeed;
            m3FormalEvaluationMatches = M3FormalEvaluationMatches;
            m3TimeScale = M3TimeScale;
        }

        public float GetEpisodeSeconds(MNG_CurriculumStage requestedStage)
            => requestedStage switch
            {
                MNG_CurriculumStage.M2DefenseChoice => m2EpisodeSeconds,
                MNG_CurriculumStage.M3FallbackMatch => m3TrainingMatchSeconds,
                _ => m1EpisodeSeconds
            };

        public int GetSeed(MNG_CurriculumStage requestedStage, bool validation)
        {
            if (requestedStage == MNG_CurriculumStage.M2DefenseChoice)
                return validation ? m2ValidationSeed : m2TrainingSeed;
            if (requestedStage == MNG_CurriculumStage.M3FallbackMatch)
                return validation ? m3ValidationSeed : m3TrainingSeed;
            return validation ? m1ValidationSeed : m1TrainingSeed;
        }

        public int GetEvaluationScenarioCount(MNG_CurriculumStage requestedStage)
            => requestedStage switch
            {
                MNG_CurriculumStage.M2DefenseChoice => m2EvaluationScenarioCount,
                MNG_CurriculumStage.M3FallbackMatch => m3FormalEvaluationMatches,
                _ => m1EvaluationScenarioCount
            };

        public float GetTimeScale(MNG_CurriculumStage requestedStage)
            => requestedStage switch
            {
                MNG_CurriculumStage.M2DefenseChoice => m2TimeScale,
                MNG_CurriculumStage.M3FallbackMatch => m3TimeScale,
                _ => m1TimeScale
            };

        public float GetM3MatchSeconds(bool fullMatch)
            => fullMatch ? m3FullMatchSeconds : m3TrainingMatchSeconds;

        public void ValidateOrThrow()
        {
            if (Mathf.Abs(m1EpisodeSeconds - M1EpisodeSeconds) > 0.0001f
                || Mathf.Abs(m1MinimumGoalDistance - M1MinimumGoalDistance) > 0.0001f
                || Mathf.Abs(m1MaximumGoalDistance - M1MaximumGoalDistance) > 0.0001f
                || m1TrainingSeed != M1TrainingSeed
                || m1ValidationSeed != M1ValidationSeed
                || m1EvaluationScenarioCount != M1EvaluationScenarioCount
                || Mathf.Abs(m1TimeScale - M1TimeScale) > 0.0001f)
            {
                throw new InvalidOperationException("MNG M1 curriculum catalog differs from the approved contract.");
            }
            if (Mathf.Abs(m2EpisodeSeconds - M2EpisodeSeconds) > 0.0001f
                || Mathf.Abs(m2MinimumThreatDistance - M2MinimumThreatDistance) > 0.0001f
                || Mathf.Abs(m2MaximumThreatDistance - M2MaximumThreatDistance) > 0.0001f
                || Mathf.Abs(m2RecoveryDeadlineSeconds - M2RecoveryDeadlineSeconds) > 0.0001f
                || Mathf.Abs(m2OpponentPossessionSeconds - M2OpponentPossessionSeconds) > 0.0001f
                || Mathf.Abs(m2RecoveryPossessionSeconds - M2RecoveryPossessionSeconds) > 0.0001f
                || m2TrainingSeed != M2TrainingSeed
                || m2ValidationSeed != M2ValidationSeed
                || m2EvaluationScenarioCount != M2EvaluationScenarioCount
                || Mathf.Abs(m2TimeScale - M2TimeScale) > 0.0001f)
            {
                throw new InvalidOperationException("MNG M2 curriculum catalog differs from the approved contract.");
            }
            if (Mathf.Abs(m3TrainingMatchSeconds - M3TrainingMatchSeconds) > 0.0001f
                || Mathf.Abs(m3FullMatchSeconds - M3FullMatchSeconds) > 0.0001f
                || m3TrainingSeed != M3TrainingSeed
                || m3ValidationSeed != M3ValidationSeed
                || m3FormalEvaluationMatches != M3FormalEvaluationMatches
                || Mathf.Abs(m3TimeScale - M3TimeScale) > 0.0001f)
            {
                throw new InvalidOperationException("MNG M3 curriculum catalog differs from the approved contract.");
            }
        }
    }
}
