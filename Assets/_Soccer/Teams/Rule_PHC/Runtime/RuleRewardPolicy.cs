namespace MachineLearning.Soccer.Teams.Rule
{
    /// <summary>
    /// 경기 평가·통계 호환용 RuleRewardProfile 연결부이며 학습과 행동 판단에는 사용하지 않는다.
    /// 수치를 바꾸면 평가 점수만 달라지며 선수 행동은 RuleBasedSoccerController의 FSM에서 수정한다.
    /// </summary>
    public sealed class RuleRewardPolicy : SoccerTeamRewardPolicyBase
    {
    }
}
