using UnityEngine;
using UnityEngine.UIElements;

namespace MachineLearning.Escape
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class EscapeHudController : MonoBehaviour
    {
        [SerializeField] EscapeEnvironmentController environment;

        UIDocument m_Document;
        Label m_TimeLabel;
        Label m_ButtonLabel;
        Label m_HealthLabel;
        Label m_RewardLabel;
        Label m_ModeLabel;
        Label m_ResultLabel;
        Toggle m_AIToggle;

        void Awake()
        {
            if (Application.isBatchMode)
            {
                gameObject.SetActive(false);
                return;
            }

            m_Document = GetComponent<UIDocument>();
        }

        void OnEnable()
        {
            if (m_Document == null)
            {
                return;
            }

            var root = m_Document.rootVisualElement;
            m_TimeLabel = root.Q<Label>("time-label");
            m_ButtonLabel = root.Q<Label>("button-label");
            m_HealthLabel = root.Q<Label>("health-label");
            m_RewardLabel = root.Q<Label>("reward-label");
            m_ModeLabel = root.Q<Label>("mode-label");
            m_ResultLabel = root.Q<Label>("result-label");
            m_AIToggle = root.Q<Toggle>("ai-toggle");
            if (m_AIToggle != null)
            {
                m_AIToggle.pickingMode = PickingMode.Ignore;
                m_AIToggle.focusable = false;
            }
        }

        void Update()
        {
            if (environment == null || environment.Player == null)
            {
                return;
            }

            var player = environment.Player;
            m_TimeLabel.text = $"남은 시간 {environment.RemainingTime:0.0}";
            m_ButtonLabel.text = $"버튼 {Mathf.Min(environment.PressedButtonCount, EscapeMapLayout.RequiredButtonCount)}/{EscapeMapLayout.RequiredButtonCount}";
            m_HealthLabel.text = $"체력 {player.Health.CurrentHealth}/{player.Health.MaxHealth}";
            m_RewardLabel.text = $"보상 {player.EpisodeReward:+0.000;-0.000;0.000}";
            m_ModeLabel.text = player.ControlMode.ToString();
            m_AIToggle.SetValueWithoutNotify(player.IsAIEnabled);

            var showResult = environment.State == EscapeEpisodeState.ResultFeedback;
            m_ResultLabel.style.display = showResult ? DisplayStyle.Flex : DisplayStyle.None;
            if (showResult)
            {
                m_ResultLabel.text = environment.LastResult == EscapeEpisodeState.PlayerWin ? "탈출 성공" : "적 승리";
            }
        }

        public void Configure(EscapeEnvironmentController configuredEnvironment)
        {
            environment = configuredEnvironment;
        }
    }
}
