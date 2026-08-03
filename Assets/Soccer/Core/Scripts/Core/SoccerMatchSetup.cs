using UnityEngine;

namespace MachineLearning.Soccer
{
    [DefaultExecutionOrder(-200)]
    public sealed class SoccerMatchSetup : MonoBehaviour
    {
        [SerializeField] SoccerTeamDefinition blueTeam;
        [SerializeField] SoccerTeamDefinition purpleTeam;
        [SerializeField] SoccerTeamRewardPolicyBase blueRewardPolicy;
        [SerializeField] SoccerTeamRewardPolicyBase purpleRewardPolicy;
        [SerializeField] bool trainBlue = true;
        [SerializeField] bool trainPurple;

        public SoccerTeamDefinition BlueTeam => blueTeam;
        public SoccerTeamDefinition PurpleTeam => purpleTeam;
        public bool TrainBlue => trainBlue;
        public bool TrainPurple => trainPurple;

        void Awake()
        {
            ApplyDefinitions();
        }

        public void Configure(
            SoccerTeamDefinition configuredBlueTeam,
            SoccerTeamDefinition configuredPurpleTeam,
            SoccerTeamRewardPolicyBase configuredBluePolicy,
            SoccerTeamRewardPolicyBase configuredPurplePolicy,
            bool configuredTrainBlue,
            bool configuredTrainPurple)
        {
            blueTeam = configuredBlueTeam;
            purpleTeam = configuredPurpleTeam;
            blueRewardPolicy = configuredBluePolicy;
            purpleRewardPolicy = configuredPurplePolicy;
            trainBlue = configuredTrainBlue;
            trainPurple = configuredTrainPurple;
            blueRewardPolicy?.Configure(blueTeam);
            purpleRewardPolicy?.Configure(purpleTeam);
        }

        public SoccerTeamDefinition GetDefinition(Team team)
        {
            return team == Team.Blue ? blueTeam : purpleTeam;
        }

        public SoccerTeamRewardPolicyBase GetRewardPolicy(Team team)
        {
            return team == Team.Blue ? blueRewardPolicy : purpleRewardPolicy;
        }

        public bool IsTrainable(Team team)
        {
            return team == Team.Blue ? trainBlue : trainPurple;
        }

        public void ApplyDefinitions()
        {
            blueRewardPolicy?.Configure(blueTeam);
            purpleRewardPolicy?.Configure(purpleTeam);
            foreach (var agent in GetComponentsInChildren<AgentSoccer>(true))
            {
                agent.ApplyTeamDefinition(GetDefinition(agent.Team));
            }
        }
    }
}
