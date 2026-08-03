using Unity.InferenceEngine;
using UnityEngine;

namespace MachineLearning.Soccer
{
    [CreateAssetMenu(menuName = "Machine Learning/Soccer/Team Definition", fileName = "SoccerTeamDefinition")]
    public sealed class SoccerTeamDefinition : ScriptableObject
    {
        public const int CurrentPolicyContractVersion = 2;

        [SerializeField] string teamId = "base";
        [SerializeField] string displayName = "Base";
        [SerializeField] string behaviorName = "Soccer4v4_Base";
        [SerializeField] int policyContractVersion = CurrentPolicyContractVersion;
        [SerializeField] ModelAsset inferenceModel;
        [SerializeField] SoccerRewardProfile rewardProfile;

        public string TeamId => teamId;
        public string DisplayName => displayName;
        public string BehaviorName => behaviorName;
        public int PolicyContractVersion => policyContractVersion;
        public ModelAsset InferenceModel => inferenceModel;
        public SoccerRewardProfile RewardProfile => rewardProfile;

        public void Configure(
            string configuredTeamId,
            string configuredDisplayName,
            string configuredBehaviorName,
            SoccerRewardProfile configuredRewardProfile,
            ModelAsset configuredModel)
        {
            teamId = configuredTeamId;
            displayName = configuredDisplayName;
            behaviorName = configuredBehaviorName;
            policyContractVersion = CurrentPolicyContractVersion;
            rewardProfile = configuredRewardProfile;
            inferenceModel = configuredModel;
        }
    }
}
