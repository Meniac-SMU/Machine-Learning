using UnityEngine;

namespace MachineLearning.Soccer
{
    public enum SoccerRewardKind
    {
        GoalResult,
        MatchResult,
        AttackSuccess,
        PassSuccess,
        DefenseSuccess,
        PressSuccess,
        DribbleSuccess,
        FormationChange
    }

    [CreateAssetMenu(menuName = "Machine Learning/Soccer/Reward Profile", fileName = "SoccerRewardProfile")]
    public sealed class SoccerRewardProfile : ScriptableObject
    {
        [Header("Required group rewards")]
        [Min(0f)] public float goalResult = 1f;
        [Min(0f)] public float matchResult = 0.5f;

        [Header("Optional base rewards")]
        [Min(0f)] public float attackSuccess = 0.03f;
        [Min(0f)] public float passSuccess = 0.01f;
        [Min(0f)] public float defenseSuccess = 0.03f;
        [Min(0f)] public float pressSuccess = 0.02f;
        [Min(0f)] public float dribbleGroup = 0.005f;
        [Min(0f)] public float dribbleIndividual = 0.02f;
        [Min(0f)] public float formationChangeScale = 0.02f;

        [Header("Anti-exploit limits")]
        [Min(0f)] public float passRewardLimitPerPossession = 0.05f;
        [Min(0f)] public float formationRewardLimitPerPossession = 0.03f;

        public float GetGroupReward(SoccerRewardKind kind)
        {
            return kind switch
            {
                SoccerRewardKind.GoalResult => goalResult,
                SoccerRewardKind.MatchResult => matchResult,
                SoccerRewardKind.AttackSuccess => attackSuccess,
                SoccerRewardKind.PassSuccess => passSuccess,
                SoccerRewardKind.DefenseSuccess => defenseSuccess,
                SoccerRewardKind.PressSuccess => pressSuccess,
                SoccerRewardKind.DribbleSuccess => dribbleGroup,
                SoccerRewardKind.FormationChange => formationChangeScale,
                _ => 0f
            };
        }

        public float GetIndividualReward(SoccerRewardKind kind)
        {
            return kind == SoccerRewardKind.DribbleSuccess ? dribbleIndividual : 0f;
        }

        public void ApplyBaseDefaults()
        {
            goalResult = 1f;
            matchResult = 0.5f;
            attackSuccess = 0.03f;
            passSuccess = 0.01f;
            defenseSuccess = 0.03f;
            pressSuccess = 0.02f;
            dribbleGroup = 0.005f;
            dribbleIndividual = 0.02f;
            formationChangeScale = 0.02f;
            passRewardLimitPerPossession = 0.05f;
            formationRewardLimitPerPossession = 0.03f;
        }
    }
}
