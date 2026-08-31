using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.Serialization;

namespace MachineLearning.Soccer
{
    [DefaultExecutionOrder(-1000)]
    public sealed class SoccerMatchSetup : MonoBehaviour
    {
        // Builder가 관리하는 팀 배선이다. 팀 담당자는 자기 프로필·모델 슬롯 외 상대와 학습 플래그를 재배선하지 않는다.
        [FormerlySerializedAs("blueTeam")] [SerializeField] SoccerTeamDefinition redTeam;
        [FormerlySerializedAs("purpleTeam")] [SerializeField] SoccerTeamDefinition navyTeam;
        [FormerlySerializedAs("blueRewardPolicy")] [SerializeField] SoccerTeamRewardPolicyBase redRewardPolicy;
        [FormerlySerializedAs("purpleRewardPolicy")] [SerializeField] SoccerTeamRewardPolicyBase navyRewardPolicy;
        [Header("프리팹 전용 모델 슬롯")]
        [FormerlySerializedAs("blueModelOverride")] [SerializeField] ModelAsset redModelOverride;
        [FormerlySerializedAs("purpleModelOverride")] [SerializeField] ModelAsset navyModelOverride;
        [FormerlySerializedAs("trainBlue")] [SerializeField] bool trainRed = true;
        [FormerlySerializedAs("trainPurple")] [SerializeField] bool trainNavy;
        [Header("규칙 기반 Fallback 강제")]
        [SerializeField] bool forceRedFallback;
        [SerializeField] bool forceNavyFallback;

        public SoccerTeamDefinition RedTeam => redTeam;
        public SoccerTeamDefinition NavyTeam => navyTeam;
        public bool TrainRed => trainRed;
        public bool TrainNavy => trainNavy;
        public ModelAsset RedModelOverride => redModelOverride;
        public ModelAsset NavyModelOverride => navyModelOverride;
        public bool ForceRedFallback => forceRedFallback;
        public bool ForceNavyFallback => forceNavyFallback;

        void Awake()
        {
            ApplyDefinitions();
            PrepareNonTrainableTeams();
        }

        public void Configure(
            SoccerTeamDefinition configuredRedTeam,
            SoccerTeamDefinition configuredNavyTeam,
            SoccerTeamRewardPolicyBase configuredRedPolicy,
            SoccerTeamRewardPolicyBase configuredNavyPolicy,
            bool configuredTrainRed,
            bool configuredTrainNavy,
            ModelAsset configuredRedModelOverride = null,
            ModelAsset configuredNavyModelOverride = null,
            bool configuredForceRedFallback = false,
            bool configuredForceNavyFallback = false)
        {
            redTeam = configuredRedTeam;
            navyTeam = configuredNavyTeam;
            redRewardPolicy = configuredRedPolicy;
            navyRewardPolicy = configuredNavyPolicy;
            trainRed = configuredTrainRed;
            trainNavy = configuredTrainNavy;
            redModelOverride = configuredRedModelOverride;
            navyModelOverride = configuredNavyModelOverride;
            forceRedFallback = configuredForceRedFallback;
            forceNavyFallback = configuredForceNavyFallback;
            redRewardPolicy?.Configure(redTeam);
            navyRewardPolicy?.Configure(navyTeam);
        }

        public SoccerTeamDefinition GetDefinition(Team team)
        {
            return team == Team.Red ? redTeam : navyTeam;
        }

        public SoccerTeamRewardPolicyBase GetRewardPolicy(Team team)
        {
            return team == Team.Red ? redRewardPolicy : navyRewardPolicy;
        }

        public bool IsTrainable(Team team)
        {
            var definition = GetDefinition(team);
            if (definition == null || !definition.UsesNeuralPolicy || IsFallbackForced(team))
            {
                return false;
            }

            return team == Team.Red ? trainRed : trainNavy;
        }

        public ModelAsset GetConfiguredModel(Team team)
        {
            if (IsFallbackForced(team))
            {
                return null;
            }

            var modelOverride = team == Team.Red ? redModelOverride : navyModelOverride;
            return modelOverride != null ? modelOverride : GetDefinition(team)?.InferenceModel;
        }

        public bool IsFallbackForced(Team team)
        {
            return team == Team.Red ? forceRedFallback : forceNavyFallback;
        }

        public void ApplyDefinitions()
        {
            redRewardPolicy?.Configure(redTeam);
            navyRewardPolicy?.Configure(navyTeam);
            foreach (var agent in GetComponentsInChildren<AgentSoccer>(true))
            {
                agent.ApplyTeamDefinition(
                    GetDefinition(agent.Team),
                    GetConfiguredModel(agent.Team),
                    !IsFallbackForced(agent.Team));
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
