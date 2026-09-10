using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    [DisallowMultipleComponent]
    public sealed class MNG_RewardEngine : MonoBehaviour
    {
        [SerializeField] MNG_MatchController matchController;
        [SerializeField] MNG_RewardProfile redProfile;
        [SerializeField] MNG_RewardProfile navyProfile;

        readonly MNG_EventLedger[] m_Ledgers = { new MNG_EventLedger(), new MNG_EventLedger() };
        readonly MNG_ManagerAgent[] m_Managers = new MNG_ManagerAgent[2];
        readonly float[] m_CumulativeRewards = new float[2];

        public float GetCumulativeReward(Team team)
            => m_CumulativeRewards[team == Team.Red ? 0 : 1];

        public void Configure(
            MNG_MatchController configuredMatch,
            MNG_RewardProfile configuredRedProfile,
            MNG_RewardProfile configuredNavyProfile)
        {
            matchController = configuredMatch ?? throw new ArgumentNullException(nameof(configuredMatch));
            redProfile = configuredRedProfile ?? throw new ArgumentNullException(nameof(configuredRedProfile));
            navyProfile = configuredNavyProfile ?? throw new ArgumentNullException(nameof(configuredNavyProfile));
        }

        public void RegisterManager(MNG_ManagerAgent manager)
        {
            if (manager == null) throw new ArgumentNullException(nameof(manager));
            m_Managers[manager.Team == Team.Red ? 0 : 1] = manager;
        }

        public float Award(Team team, MNG_RewardEventKind kind, long eventId)
        {
            var index = team == Team.Red ? 0 : 1;
            var profile = index == 0 ? redProfile : navyProfile;
            if (profile == null) throw new InvalidOperationException($"MNG reward profile for {team} is missing.");
            var now = matchController != null ? matchController.EpisodeElapsedSeconds : 0f;
            if (!m_Ledgers[index].TryRecord(new MNG_RewardEvent(kind, eventId), profile, now, out var reward))
                return 0f;
            m_CumulativeRewards[index] += reward;
            if (reward != 0f)
            {
                var manager = m_Managers[index];
                // M0 and the fixed M1 opponent are intentionally fallback-driven.
                // Their events remain in the ledger, but only an active policy endpoint receives reward.
                if (manager != null && manager.isActiveAndEnabled) manager.AddReward(reward);
            }
            return reward;
        }

        public void AwardGoal(Team scoringTeam, long goalId)
        {
            Award(scoringTeam, MNG_RewardEventKind.GoalFor, goalId);
            Award(Opponent(scoringTeam), MNG_RewardEventKind.GoalAgainst, goalId);
        }

        public void AwardMatchResult(Team winner, long matchId)
        {
            Award(winner, MNG_RewardEventKind.MatchWin, matchId);
            Award(Opponent(winner), MNG_RewardEventKind.MatchLoss, matchId);
        }

        public void ResetEpisode()
        {
            m_Ledgers[0].ResetEpisode();
            m_Ledgers[1].ResetEpisode();
            Array.Clear(m_CumulativeRewards, 0, m_CumulativeRewards.Length);
        }

        static Team Opponent(Team team) => team == Team.Red ? Team.Navy : Team.Red;
    }
}
