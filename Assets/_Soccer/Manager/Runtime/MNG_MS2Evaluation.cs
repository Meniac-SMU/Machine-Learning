using System;

namespace MachineLearning.Soccer.Manager
{
    [Serializable]
    public sealed class MNG_MS2EvaluationMatch
    {
        public int matchIndex;
        public int spawnSeedOffset;
        public string policyTeam;
        public int policyGoals;
        public int opponentGoals;
        public string outcome;
        public float scoreValue;
        public long[] rawCommandCounts;
        public long[] effectiveCommandCounts;
        public long blockedPassOverrides;
        public long explicitBlockedPassRewards;
        public int passStrikes;
        public int completedPasses;
        public int shotStrikes;
        public int validShots;
        public int fiveMeterAdvances;
    }

    [Serializable]
    public sealed class MNG_MS2EvaluationResult
    {
        public string protocol;
        public string runId;
        public string candidateId;
        public string modelSha256;
        public string policyKind;
        public string opponentStrength;
        public string policyTeam;
        public int seedOffset;
        public float matchSeconds;
        public int matches;
        public int wins;
        public int draws;
        public int losses;
        public int goalsFor;
        public int goalsAgainst;
        public float scoreRate;
        public long[] rawCommandCounts;
        public long[] effectiveCommandCounts;
        public long blockedPassOverrides;
        public long explicitBlockedPassRewards;
        public int passStrikes;
        public int completedPasses;
        public int shotStrikes;
        public int validShots;
        public int fiveMeterAdvances;
        public MNG_MS2EvaluationMatch[] matchResults;
    }
}
