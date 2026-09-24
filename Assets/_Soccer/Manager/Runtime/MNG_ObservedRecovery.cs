namespace MachineLearning.Soccer.Manager
{
    // Observation only; keeps existing M2 qualification windows and pays no reward.
    public sealed class MNG_ObservedRecovery
    {
        float opponentSeconds, ownSeconds;
        bool armed;
        public int Count { get; private set; }
        public void ResetWindow() { opponentSeconds = ownSeconds = 0; armed = false; }
        public void Step(bool own, bool opponent, bool active, float dt)
        {
            if (!active) { ResetWindow(); return; }
            if (!armed)
            {
                opponentSeconds = opponent ? opponentSeconds + dt : 0;
                if (opponentSeconds >= MNG_CurriculumCatalog.M2OpponentPossessionSeconds) armed = true;
                return;
            }
            ownSeconds = own ? ownSeconds + dt : 0;
            if (ownSeconds < MNG_CurriculumCatalog.M2RecoveryPossessionSeconds) return;
            Count++;
            ResetWindow();
        }
    }
}
