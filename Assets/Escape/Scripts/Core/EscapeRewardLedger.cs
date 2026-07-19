namespace MachineLearning.Escape
{
    public sealed class EscapeRewardLedger
    {
        public float EpisodeReward { get; private set; }

        public void Add(float reward)
        {
            EpisodeReward += reward;
        }

        public void Reset()
        {
            EpisodeReward = 0f;
        }
    }
}
