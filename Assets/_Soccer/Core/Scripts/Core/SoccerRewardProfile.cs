using UnityEngine;

namespace MachineLearning.Soccer
{
    public enum SoccerRewardKind
    {
        GoalResult,
        MatchResult,
        AttackSuccess,
        PassSuccess,
        PassIndividual,
        DefenseSuccess,
        PressSuccess,
        ProgressivePass,
        ThreePlayerCombination,
        CoordinatedPress,
        DribbleSuccess,
        ControlledCarry,
        CurriculumApproachProgress,
        CurriculumPossessionEstablished,
        CurriculumSupportShape,
        CurriculumDribbleProgress,
        CurriculumShotAttempt,
        CurriculumShotOnTarget,
        CurriculumGoal,
        CurriculumLessonSuccess,
        FormationChange,
        TeammateCrowding,
        WastefulStrongKick,
        UnsafeOwnGoalKick,
        // Append to preserve existing serialized enum values.
        CurriculumShotAlignment,
        CurriculumPassAttempt,
        CurriculumPassReception,
        CurriculumPassDelivered,
        CurriculumPassDirection,
        CurriculumPassOpportunityLost,
        CurriculumReceiverProgress,
        CurriculumStableReceiver,
        CurriculumFindProgress,
        CurriculumScoreProgress,
        CurriculumLongPassGoalBonus,
        CurriculumOwnGoalPenalty,
        // Append to preserve all existing serialized enum values.
        CurriculumFindHeading,
        CurriculumFastFind,
        CurriculumFindTimeoutPenalty,
        CurriculumFindMissPenalty
    }

    /// <summary>
    /// 팀 전술별 보상 수치와 상한 설정.
    /// 팀 실험은 각 RewardProfile asset의 값만 바꾸며 종류와 의미는 공통 스키마로 유지한다.
    /// 개별 담당자는 자신의 Teams/&lt;담당 폴더&gt;/Profiles 자산만 조정하고 이 Core 스키마는 변경하지 않는다.
    /// </summary>
    [CreateAssetMenu(menuName = "Machine Learning/Soccer/Reward Profile", fileName = "SoccerRewardProfile")]
    public sealed class SoccerRewardProfile : ScriptableObject
    {
        // 아래 값은 새 asset의 기본값이다. 기존 팀별 실험값은 해당 RewardProfile asset에서 조정한다.
        [Header("필수 팀 보상")]
        [Min(0f)] public float goalResult = 1f;
        [Min(0f)] public float matchResult = 0.5f;

        [Header("선택 팀 보상")]
        [Min(0f)] public float attackSuccess = 0.03f;
        [Min(0f)] public float passSuccess = 0.005f;
        [Min(0f)] public float passIndividual = 0.005f;
        [Min(0f)] public float defenseSuccess = 0.03f;
        [Min(0f)] public float pressSuccess = 0.02f;
        [Min(0f)] public float progressivePass = 0.01f;
        [Min(0f)] public float threePlayerCombination = 0.02f;
        [Min(0f)] public float coordinatedPress = 0.015f;
        [Min(0f)] public float dribbleGroup = 0.005f;
        [Min(0f)] public float dribbleIndividual = 0.01f;
        [Min(0f)] public float controlledCarryGroup = 0.0025f;
        [Min(0f)] public float controlledCarryIndividual = 0.005f;
        [Min(0f)] public float formationChangeScale = 0.01f;

        // 패널티도 양수 크기로 저장하며 RewardEngine이 실제 지급 시 음수로 바꾼다.
        [Header("팀 간격 패널티")]
        [Min(0f)] public float teammateCrowdingPenalty = 0.0025f;
        [Min(0f)] public float teammateCrowdingPenaltyLimitPerMatch = 0.25f;

        [Header("행동 선택 패널티")]
        [Min(0f)] public float wastefulStrongKickPenalty = 0.003f;
        [Min(0f)] public float unsafeOwnGoalKickPenalty = 0.02f;
        [Min(0f)] public float wastefulStrongKickPenaltyLimitPerPossession = 0.015f;
        [Min(0f)] public float behaviorPenaltyLimitPerMatch = 0.1f;

        [Header("보상 악용 방지 상한")]
        [Min(0f)] public float passRewardLimitPerPossession = 0.025f;
        [Min(0f)] public float passIndividualRewardLimitPerPossession = 0.02f;
        [Min(0f)] public float controlledCarryRewardLimitPerPossession = 0.03f;
        [Min(0f)] public float progressivePassLimitPerPossession = 0.03f;
        [Min(0f)] public float combinationRewardLimitPerPossession = 0.04f;
        [Min(0f)] public float formationRewardLimitPerPossession = 0.01f;
        [Min(0f)] public float shapingRewardLimitPerPossession = 0.12f;
        [Min(0f)] public float shapingRewardLimitPerMatch = 0.5f;

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
                SoccerRewardKind.ProgressivePass => progressivePass,
                SoccerRewardKind.ThreePlayerCombination => threePlayerCombination,
                SoccerRewardKind.CoordinatedPress => coordinatedPress,
                SoccerRewardKind.DribbleSuccess => dribbleGroup,
                SoccerRewardKind.ControlledCarry => controlledCarryGroup,
                SoccerRewardKind.FormationChange => formationChangeScale,
                SoccerRewardKind.TeammateCrowding => teammateCrowdingPenalty,
                _ => 0f
            };
        }

        public float GetIndividualReward(SoccerRewardKind kind)
        {
            return kind switch
            {
                SoccerRewardKind.PassIndividual => passIndividual,
                SoccerRewardKind.DribbleSuccess => dribbleIndividual,
                SoccerRewardKind.ControlledCarry => controlledCarryIndividual,
                SoccerRewardKind.WastefulStrongKick => wastefulStrongKickPenalty,
                SoccerRewardKind.UnsafeOwnGoalKick => unsafeOwnGoalKickPenalty,
                _ => 0f
            };
        }

        // Builder가 새 프로필을 처음 만들 때만 사용하며 기존 프로필 값은 덮어쓰지 않는다.
        public void ApplyBaseDefaults()
        {
            goalResult = 1f;
            matchResult = 0.5f;
            attackSuccess = 0.03f;
            passSuccess = 0.005f;
            passIndividual = 0.005f;
            defenseSuccess = 0.03f;
            pressSuccess = 0.02f;
            progressivePass = 0.01f;
            threePlayerCombination = 0.02f;
            coordinatedPress = 0.015f;
            dribbleGroup = 0.005f;
            dribbleIndividual = 0.01f;
            controlledCarryGroup = 0.0025f;
            controlledCarryIndividual = 0.005f;
            formationChangeScale = 0.01f;
            teammateCrowdingPenalty = 0.0025f;
            teammateCrowdingPenaltyLimitPerMatch = 0.25f;
            wastefulStrongKickPenalty = 0.003f;
            unsafeOwnGoalKickPenalty = 0.02f;
            wastefulStrongKickPenaltyLimitPerPossession = 0.015f;
            behaviorPenaltyLimitPerMatch = 0.1f;
            passRewardLimitPerPossession = 0.025f;
            passIndividualRewardLimitPerPossession = 0.02f;
            controlledCarryRewardLimitPerPossession = 0.03f;
            progressivePassLimitPerPossession = 0.03f;
            combinationRewardLimitPerPossession = 0.04f;
            formationRewardLimitPerPossession = 0.01f;
            shapingRewardLimitPerPossession = 0.12f;
            shapingRewardLimitPerMatch = 0.5f;
        }
    }
}
