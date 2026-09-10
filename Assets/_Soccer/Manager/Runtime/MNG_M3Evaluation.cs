using System;

namespace MachineLearning.Soccer.Manager
{
    public static class MNG_M3EvaluationRules
    {
        public const float MinimumScoreRate = 0.50f;
        public const float MaximumScorelessMatchRate = 0.20f;

        public static int MaximumScorelessMatches =>
            (int)(MNG_CurriculumCatalog.M3FormalEvaluationMatches * MaximumScorelessMatchRate);

        public static bool Passes(
            int matchCount,
            int redWins,
            int draws,
            int redLosses,
            int redGoals,
            int navyGoals,
            int scorelessMatches)
        {
            if (matchCount != MNG_CurriculumCatalog.M3FormalEvaluationMatches)
                return false;
            if (redWins < 0 || draws < 0 || redLosses < 0
                || redGoals < 0 || navyGoals < 0 || scorelessMatches < 0)
                return false;
            if (redWins + draws + redLosses != matchCount
                || scorelessMatches > matchCount)
                return false;

            var scoreRate = (redWins + 0.5f * draws) / matchCount;
            return scoreRate >= MinimumScoreRate
                && scorelessMatches <= MaximumScorelessMatches
                && redGoals > 0
                && navyGoals > 0;
        }
    }

    [Serializable]
    public sealed class MNG_M3EvaluationResult
    {
        public string protocolVersion = "MNG_M3_v3";
        public string runId;
        public string modelSha256;
        public int validationSeed;
        public int generatorVersion;
        public int plannerVersion;
        public int matches;
        public int redWins;
        public int draws;
        public int redLosses;
        public int redGoals;
        public int navyGoals;
        public int scorelessMatches;
        public float scoreRate;
        public float scorelessMatchRate;
        public float minimumScoreRate;
        public int maximumScorelessMatches;
        public bool passed;
    }
}
