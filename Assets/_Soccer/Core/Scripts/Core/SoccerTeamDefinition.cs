using Unity.InferenceEngine;
using UnityEngine;

namespace MachineLearning.Soccer
{
    public enum SoccerTeamControllerType
    {
        NeuralPolicy = 0,
        RuleBased = 1
    }

    /// <summary>
    /// 팀별 프로필·모델 연결 자산. Behavior 이름은 Trainer YAML과 같아야 하며 공통 물리 계약은 담지 않는다.
    /// 담당자 접미사는 물리 폴더명에만 쓰며 teamId·displayName·BehaviorName·ONNX 논리명에는 붙이지 않는다.
    /// 학습형 담당 영역은 자신의 RewardProfile·Training YAML·Models/ONNX이고 이 Core 계약은 공통 관리한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Machine Learning/Soccer/Team Definition", fileName = "SoccerTeamDefinition")]
    public sealed class SoccerTeamDefinition : ScriptableObject
    {
        // 관측·행동의 순서나 크기가 바뀔 때만 올린다. 보상 수치·Trainer 튜닝은 버전 변경 대상이 아니다.
        public const int CurrentPolicyContractVersion = 2;

        // RuleBased 팀은 YAML·ONNX를 연결하지 않는다. RewardProfile은 평가·통계용이고 행동은 FSM에서 결정한다.
        [SerializeField] string teamId = "base";
        [SerializeField] string displayName = "Base";
        [SerializeField] string behaviorName = "Soccer4v4_Base";
        [SerializeField] int policyContractVersion = CurrentPolicyContractVersion;
        [SerializeField] ModelAsset inferenceModel;
        [SerializeField] SoccerRewardProfile rewardProfile;
        [SerializeField] SoccerTeamControllerType controllerType = SoccerTeamControllerType.NeuralPolicy;

        public string TeamId => teamId;
        public string DisplayName => displayName;
        public string BehaviorName => behaviorName;
        public int PolicyContractVersion => policyContractVersion;
        public ModelAsset InferenceModel => inferenceModel;
        public SoccerRewardProfile RewardProfile => rewardProfile;
        public SoccerTeamControllerType ControllerType => controllerType;
        public bool UsesNeuralPolicy => controllerType == SoccerTeamControllerType.NeuralPolicy;

        public void Configure(
            string configuredTeamId,
            string configuredDisplayName,
            string configuredBehaviorName,
            SoccerRewardProfile configuredRewardProfile,
            ModelAsset configuredModel,
            SoccerTeamControllerType configuredControllerType = SoccerTeamControllerType.NeuralPolicy)
        {
            teamId = configuredTeamId;
            displayName = configuredDisplayName;
            behaviorName = configuredBehaviorName;
            policyContractVersion = CurrentPolicyContractVersion;
            rewardProfile = configuredRewardProfile;
            controllerType = configuredControllerType;
            // 규칙형 팀의 신경망 참조 차단.
            inferenceModel = UsesNeuralPolicy ? configuredModel : null;
        }
    }
}
