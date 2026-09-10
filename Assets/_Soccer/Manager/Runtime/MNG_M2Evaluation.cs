using System;

namespace MachineLearning.Soccer.Manager
{
    public static class MNG_M2EvaluationRules
    {
        public const int MinimumRecoveriesWithinDeadline = 60;
        public const int RandomBaselineRecoveriesWithinDeadline = 52;
        public const int RandomBaselineConcededGoals = 45;
        public const int MaximumConcededGoals = RandomBaselineConcededGoals - 1;

        public static bool Passes(
            int scenarioCount,
            int recoveriesWithinDeadline,
            int concededGoals,
            int redGoals,
            int timeouts)
        {
            if (scenarioCount != MNG_CurriculumCatalog.M2EvaluationScenarioCount)
                return false;
            if (recoveriesWithinDeadline < 0 || recoveriesWithinDeadline > scenarioCount
                || concededGoals < 0 || redGoals < 0 || timeouts < 0)
                return false;
            if (concededGoals + redGoals + timeouts != scenarioCount)
                return false;
            return recoveriesWithinDeadline >= MinimumRecoveriesWithinDeadline
                && concededGoals <= MaximumConcededGoals;
        }
    }

    [Serializable]
    public sealed class MNG_M2EvaluationResult
    {
        public string protocolVersion = "MNG_M2_v3";
        public string runId;
        public string modelSha256;
        public int validationSeed;
        public int generatorVersion;
        public int plannerVersion;
        public int scenarios;
        public int recoveriesWithinDeadline;
        public int allRecoveries;
        public int concededGoals;
        public int redGoals;
        public int timeouts;
        public int minimumRecoveriesWithinDeadline;
        public int randomBaselineRecoveriesWithinDeadline;
        public int randomBaselineConcededGoals;
        public int maximumConcededGoals;
        public bool passed;
    }
}
