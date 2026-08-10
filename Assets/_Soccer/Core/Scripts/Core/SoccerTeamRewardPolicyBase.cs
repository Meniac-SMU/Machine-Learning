using UnityEngine;

namespace MachineLearning.Soccer
{
    public readonly struct SoccerRewardContext
    {
        public SoccerRewardContext(Team team, AgentSoccer actor, Vector3 ballPosition, float time)
        {
            Team = team;
            Actor = actor;
            BallPosition = ballPosition;
            Time = time;
        }

        public Team Team { get; }
        public AgentSoccer Actor { get; }
        public Vector3 BallPosition { get; }
        public float Time { get; }
    }

    /// <summary>
    /// 공통 사건에 대한 팀별 보상 크기 계산 경계.
    /// Override는 값만 반환하고 부호·상한·지급·기록은 RewardEngine에 맡긴다.
    /// 사건 의미와 지급 절차를 공유하는 Core 계약이므로 개별 팀 실험에서 수정하지 않는다.
    /// </summary>
    public abstract class SoccerTeamRewardPolicyBase : MonoBehaviour
    {
        [SerializeField] SoccerTeamDefinition teamDefinition;

        public SoccerTeamDefinition TeamDefinition => teamDefinition;
        public SoccerRewardProfile RewardProfile => teamDefinition != null ? teamDefinition.RewardProfile : null;

        public void Configure(SoccerTeamDefinition definition)
        {
            teamDefinition = definition;
        }

        public virtual float EvaluateGroupReward(SoccerRewardKind kind, in SoccerRewardContext context)
        {
            return RewardProfile != null ? RewardProfile.GetGroupReward(kind) : 0f;
        }

        public virtual float EvaluateIndividualReward(SoccerRewardKind kind, in SoccerRewardContext context)
        {
            return RewardProfile != null ? RewardProfile.GetIndividualReward(kind) : 0f;
        }
    }
}
