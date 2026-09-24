using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public static class MNG_M1EvaluationRules
    {
        public const int RandomBaselineGoals = 72;
        public const int MinimumGoals = 76;
        public const int RequiredGainOverRandom = 10;
        public const int MaximumOwnGoals = 0;
        public const int MinimumPassPlateStrikes = 12;
        public const int MinimumCompletedPasses = 8;

        public static int RequiredGoals => Mathf.Max(
            MinimumGoals,
            RandomBaselineGoals + RequiredGainOverRandom);

        public static bool Passes(
            int scenarioCount,
            int goals,
            int ownGoals,
            int timeouts,
            int passPlateStrikes,
            int completedPasses)
        {
            if (scenarioCount != MNG_CurriculumCatalog.M1EvaluationScenarioCount)
                return false;
            if (goals < 0 || ownGoals < 0 || timeouts < 0
                || passPlateStrikes < 0 || completedPasses < 0)
                return false;
            if (goals + ownGoals + timeouts != scenarioCount)
                return false;
            return goals >= RequiredGoals
                && ownGoals <= MaximumOwnGoals
                && passPlateStrikes >= MinimumPassPlateStrikes
                && completedPasses >= MinimumCompletedPasses;
        }
    }

    [Serializable]
    public sealed class MNG_M1ScenarioEvaluation
    {
        public int scenarioIndex;
        public string kind;
        public float ballX;
        public float ballY;
        public string outcome;
        public float elapsedSeconds;
        public long[] commandCounts;
        public long passAvailableDecisions;
        public long shotAvailableDecisions;
        public int passPlateStrikes;
        public int shotPlateStrikes;
        public int completedPasses;
        public int validShots;
        public int fiveMeterAdvances;
    }

    [Serializable]
    public sealed class MNG_M1EvaluationResult
    {
        public string protocolVersion = "MNG_M1_v6";
        public string policyKind;
        public string runId;
        public string modelSha256;
        public int validationSeed;
        public int generatorVersion;
        public int plannerVersion;
        public int scenarios;
        public int goals;
        public int ownGoals;
        public int timeouts;
        public int randomBaselineGoals;
        public int requiredGoals;
        public int maximumOwnGoals;
        public int minimumPassPlateStrikes;
        public int minimumCompletedPasses;
        public bool passed;
        public long[] commandCounts;
        public long passAvailableDecisions;
        public long shotAvailableDecisions;
        public int passPlateStrikes;
        public int shotPlateStrikes;
        public int completedPasses;
        public int validShots;
        public int fiveMeterAdvances;
        public MNG_M1ScenarioEvaluation[] scenarioResults;
    }

}
