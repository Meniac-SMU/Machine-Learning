using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public static class MNG_M1MovingEvaluationRules
    {
        public const bool BaselineIsCalibrated = true;
        public const int RandomBaselineGoals = 20;
        public const int MinimumGoals = 30;
        public const int RequiredGainOverRandom = 10;
        public const int MaximumOwnGoals = 0;

        public static int RequiredGoals => Mathf.Max(
            MinimumGoals,
            RandomBaselineGoals + RequiredGainOverRandom);

        public static bool Passes(int scenarioCount, int goals, int ownGoals, int timeouts)
        {
            if (!BaselineIsCalibrated
                || scenarioCount != MNG_CurriculumCatalog.M1EvaluationScenarioCount)
                return false;
            if (goals < 0 || ownGoals < 0 || timeouts < 0
                || goals + ownGoals + timeouts != scenarioCount)
                return false;
            return goals >= RequiredGoals && ownGoals <= MaximumOwnGoals;
        }
    }

    [Serializable]
    public sealed class MNG_M1MovingEvaluationResult
    {
        public string protocolVersion = "MNG_M1Moving_v1";
        public string runId;
        public string modelSha256;
        public int validationSeed;
        public int generatorVersion;
        public int plannerVersion;
        public bool movingNavyDefense;
        public int scenarios;
        public int goals;
        public int ownGoals;
        public int timeouts;
        public int randomBaselineGoals;
        public int requiredGoals;
        public int maximumOwnGoals;
        public bool passed;
    }
}
