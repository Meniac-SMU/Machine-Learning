using Unity.InferenceEngine;
using UnityEngine;

namespace MachineLearning.Soccer
{
    [DefaultExecutionOrder(-1000)]
    public sealed class SoccerMatchSetup : MonoBehaviour
    {
        // Builder가 관리하는 팀 배선이다. 팀 담당자는 자기 프로필·모델 슬롯 외 상대와 학습 플래그를 재배선하지 않는다.
        [SerializeField] SoccerTeamDefinition blueTeam;
        [SerializeField] SoccerTeamDefinition purpleTeam;
        [SerializeField] SoccerTeamRewardPolicyBase blueRewardPolicy;
        [SerializeField] SoccerTeamRewardPolicyBase purpleRewardPolicy;
        [Header("프리팹 전용 모델 슬롯")]
        [SerializeField] ModelAsset blueModelOverride;
        [SerializeField] ModelAsset purpleModelOverride;
        [SerializeField] bool trainBlue = true;
        [SerializeField] bool trainPurple;

        public SoccerTeamDefinition BlueTeam => blueTeam;
        public SoccerTeamDefinition PurpleTeam => purpleTeam;
        public bool TrainBlue => trainBlue;
        public bool TrainPurple => trainPurple;
        public ModelAsset BlueModelOverride => blueModelOverride;
        public ModelAsset PurpleModelOverride => purpleModelOverride;

        void Awake()
        {
            ApplyDefinitions();
            PrepareNonTrainableTeams();
        }

        public void Configure(
            SoccerTeamDefinition configuredBlueTeam,
            SoccerTeamDefinition configuredPurpleTeam,
            SoccerTeamRewardPolicyBase configuredBluePolicy,
            SoccerTeamRewardPolicyBase configuredPurplePolicy,
            bool configuredTrainBlue,
            bool configuredTrainPurple,
            ModelAsset configuredBlueModelOverride = null,
            ModelAsset configuredPurpleModelOverride = null)
        {
            blueTeam = configuredBlueTeam;
            purpleTeam = configuredPurpleTeam;
            blueRewardPolicy = configuredBluePolicy;
            purpleRewardPolicy = configuredPurplePolicy;
            trainBlue = configuredTrainBlue;
            trainPurple = configuredTrainPurple;
            blueModelOverride = configuredBlueModelOverride;
            purpleModelOverride = configuredPurpleModelOverride;
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
            var definition = GetDefinition(team);
            if (definition == null || !definition.UsesNeuralPolicy)
            {
                return false;
            }

            return team == Team.Blue ? trainBlue : trainPurple;
        }

        public ModelAsset GetConfiguredModel(Team team)
        {
            var modelOverride = team == Team.Blue ? blueModelOverride : purpleModelOverride;
            return modelOverride != null ? modelOverride : GetDefinition(team)?.InferenceModel;
        }

        public void ApplyDefinitions()
        {
            blueRewardPolicy?.Configure(blueTeam);
            purpleRewardPolicy?.Configure(purpleTeam);
            foreach (var agent in GetComponentsInChildren<AgentSoccer>(true))
            {
                agent.ApplyTeamDefinition(GetDefinition(agent.Team), GetConfiguredModel(agent.Team));
            }
        }

        void PrepareNonTrainableTeams()
        {
            foreach (var agent in GetComponentsInChildren<AgentSoccer>(true))
            {
                if (!IsTrainable(agent.Team))
                {
                    // Trainer 등록 전 비학습 팀의 추론 모드 확정.
                    agent.ConfigureControlMode(true, false, false);
                }
            }
        }
    }
}
