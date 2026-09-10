using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    [CreateAssetMenu(menuName = "Machine Learning/Soccer Manager/Reward Profile", fileName = "MNG_BaseReward")]
    public sealed class MNG_RewardProfile : ScriptableObject
    {
        [SerializeField] MNG_TacticProfile tactic = MNG_TacticProfile.Base;

        public MNG_TacticProfile Tactic => tactic;
        public float TotalShapingCapPerMinute => 0.25f;

        public float GetNominalReward(MNG_RewardEventKind kind)
        {
            var baseValue = kind switch
            {
                MNG_RewardEventKind.GoalFor => 1f,
                MNG_RewardEventKind.GoalAgainst => -1f,
                MNG_RewardEventKind.MatchWin => 0.5f,
                MNG_RewardEventKind.MatchLoss => -0.5f,
                MNG_RewardEventKind.ValidShot => 0.02f,
                MNG_RewardEventKind.CompletedPass => 0.02f,
                MNG_RewardEventKind.AdvancedFiveMeters => 0.01f,
                MNG_RewardEventKind.Recovery => 0.03f,
                MNG_RewardEventKind.FastRecovery => 0.02f,
                MNG_RewardEventKind.Crowding => -0.002f,
                _ => throw new ArgumentOutOfRangeException(nameof(kind))
            };
            return baseValue * GetMultiplier(kind);
        }

        public float GetPerKindCap(MNG_RewardEventKind kind)
        {
            return kind switch
            {
                MNG_RewardEventKind.ValidShot => 0.06f,
                MNG_RewardEventKind.CompletedPass => 0.10f,
                MNG_RewardEventKind.AdvancedFiveMeters => 0.06f,
                MNG_RewardEventKind.Recovery => 0.12f,
                MNG_RewardEventKind.FastRecovery => 0.06f,
                MNG_RewardEventKind.Crowding => 0.04f,
                _ => float.PositiveInfinity
            };
        }

        public bool IsShaping(MNG_RewardEventKind kind)
            => kind >= MNG_RewardEventKind.ValidShot;

        public void Configure(MNG_TacticProfile configuredTactic) => tactic = configuredTactic;

        float GetMultiplier(MNG_RewardEventKind kind)
        {
            if (kind <= MNG_RewardEventKind.MatchLoss || kind == MNG_RewardEventKind.Crowding) return 1f;
            return tactic switch
            {
                MNG_TacticProfile.Attack when kind == MNG_RewardEventKind.ValidShot
                    || kind == MNG_RewardEventKind.AdvancedFiveMeters => 2f,
                MNG_TacticProfile.Attack when kind == MNG_RewardEventKind.CompletedPass => 1.5f,
                MNG_TacticProfile.Defense when kind == MNG_RewardEventKind.ValidShot
                    || kind == MNG_RewardEventKind.AdvancedFiveMeters => 0.5f,
                MNG_TacticProfile.Defense when kind == MNG_RewardEventKind.Recovery => 1.5f,
                MNG_TacticProfile.Press when kind == MNG_RewardEventKind.Recovery => 1.5f,
                MNG_TacticProfile.Press when kind == MNG_RewardEventKind.FastRecovery => 2f,
                _ => 1f
            };
        }
    }
}
