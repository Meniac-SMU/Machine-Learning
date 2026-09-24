using System;

namespace MachineLearning.Soccer.Manager
{
    [Serializable]
    public sealed class MNG_MS1EvaluationEpisode
    {
        public int episodeIndex;
        public string group;
        public string kind;
        public string outcome;
        public float elapsedSeconds;
        public bool recovered;
        public bool success;
        public long[] commandCounts;
        public int passStrikes;
        public int shotStrikes;
        public int completedPasses;
        public int validShots;
        public int fiveMeterAdvances;
    }

    [Serializable]
    public sealed class MNG_MS1EvaluationResult
    {
        public string protocolVersion = "MNG-MS1-v8";
        public string runId;
        public string candidateId;
        public string policyKind;
        public string modelSha256;
        public int episodes;
        public int attackEpisodes;
        public int defenseEpisodes;
        public int attackSuccesses;
        public int defenseSuccesses;
        public int passSubsetEpisodes;
        public int passSubsetCompletedPasses;
        public int passStrikes;
        public int shotStrikes;
        public int completedPasses;
        public int validShots;
        public int fiveMeterAdvances;
        public long[] commandCounts;
        public MNG_MS1EvaluationEpisode[] episodeResults;
    }
}
