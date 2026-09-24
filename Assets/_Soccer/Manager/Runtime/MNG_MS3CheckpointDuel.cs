using System;

namespace MachineLearning.Soccer.Manager
{
    [Serializable]
    public sealed class MNG_MS3CheckpointDuelMatch
    {
        public int matchIndex;
        public int spawnSeedOffset;
        public string earlierTeam;
        public int earlierGoals;
        public int finalGoals;
        public string earlierOutcome;
        public float earlierScoreValue;
        public long[] earlierRawCommandCounts;
        public long[] earlierEffectiveCommandCounts;
        public long[] finalRawCommandCounts;
        public long[] finalEffectiveCommandCounts;
        public long earlierBlockedPassOverrides;
        public long finalBlockedPassOverrides;
        public long earlierExplicitBlockedPassRewards;
        public long finalExplicitBlockedPassRewards;
        public int earlierPassStrikes;
        public int finalPassStrikes;
        public int earlierCompletedPasses;
        public int finalCompletedPasses;
        public int earlierShotStrikes;
        public int finalShotStrikes;
        public int earlierValidShots;
        public int finalValidShots;
        public int earlierFiveMeterAdvances;
        public int finalFiveMeterAdvances;
        public bool actionIntegrityPassed;
    }

    [Serializable]
    public sealed class MNG_MS3CheckpointDuelResult
    {
        public string protocol;
        public string duelId;
        public string earlierCandidateId;
        public string earlierModelSha256;
        public string finalCandidateId;
        public string finalModelSha256;
        public string earlierTeam;
        public int seedOffset;
        public float matchSeconds;
        public int matches;
        public int earlierWins;
        public int draws;
        public int earlierLosses;
        public int earlierGoals;
        public int finalGoals;
        public float earlierScoreRate;
        public float finalScoreRate;
        public long[] earlierRawCommandCounts;
        public long[] earlierEffectiveCommandCounts;
        public long[] finalRawCommandCounts;
        public long[] finalEffectiveCommandCounts;
        public long earlierBlockedPassOverrides;
        public long finalBlockedPassOverrides;
        public long earlierExplicitBlockedPassRewards;
        public long finalExplicitBlockedPassRewards;
        public int earlierPassStrikes;
        public int finalPassStrikes;
        public int earlierCompletedPasses;
        public int finalCompletedPasses;
        public int earlierShotStrikes;
        public int finalShotStrikes;
        public int earlierValidShots;
        public int finalValidShots;
        public int earlierFiveMeterAdvances;
        public int finalFiveMeterAdvances;
        public bool actionIntegrityPassed;
        public MNG_MS3CheckpointDuelMatch[] matchResults;
    }
}
