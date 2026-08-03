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

    public sealed class BaseRewardPolicy : SoccerTeamRewardPolicyBase
    {
    }
}
