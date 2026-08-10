namespace MachineLearning.Soccer.Teams.Defense
{
    public sealed class DefenseRewardPolicy : SoccerTeamRewardPolicyBase
    {
        // Defense 담당 영역: Profiles/DefenseRewardProfile.asset, Training/defense_poca.yaml, Models/*.onnx.
        // 문맥별 가중만 여기서 Override하고 공통 RewardEngine·관측·행동 계약은 수정하지 않는다.
    }
}
