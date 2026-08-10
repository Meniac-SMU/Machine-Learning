using UnityEngine;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class SoccerHudController : MonoBehaviour
    {
        [SerializeField] SoccerEnvController environment;

        UIDocument m_Document;
        Label m_ScoreLabel;
        Label m_TimeLabel;
        Label m_AILabel;
        Label m_HumanLabel;
        Label m_ResultLabel;
        Label m_BlueTacticLabel;
        Label m_PurpleTacticLabel;
        Label m_BlueRewardLabel;
        Label m_PurpleRewardLabel;

        public SoccerEnvController Environment => environment;

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
            m_ScoreLabel = root.Q<Label>("score-label");
            m_TimeLabel = root.Q<Label>("time-label");
            m_AILabel = root.Q<Label>("ai-label");
            m_HumanLabel = root.Q<Label>("human-label");
            m_ResultLabel = root.Q<Label>("result-label");
            m_BlueTacticLabel = root.Q<Label>("blue-tactic-label");
            m_PurpleTacticLabel = root.Q<Label>("purple-tactic-label");
            m_BlueRewardLabel = root.Q<Label>("blue-reward-label");
            m_PurpleRewardLabel = root.Q<Label>("purple-reward-label");
        }

        void Update()
        {
            if (environment == null || m_ScoreLabel == null)
            {
                return;
            }

            m_ScoreLabel.text = $"BLUE  {environment.BlueScore}  :  {environment.PurpleScore}  PURPLE";
            m_TimeLabel.text = FormatTime(environment.RemainingTime);
            m_AILabel.text = environment.IsAIEnabled ? "AI  ON" : "AI  OFF";
            m_HumanLabel.text = environment.IsAIEnabled ? "HUMAN  OFF" : "HUMAN  ON";
            m_AILabel.EnableInClassList("mode-on", environment.IsAIEnabled);
            m_AILabel.EnableInClassList("mode-off", !environment.IsAIEnabled);
            m_HumanLabel.EnableInClassList("mode-on", !environment.IsAIEnabled);
            m_HumanLabel.EnableInClassList("mode-off", environment.IsAIEnabled);
            if (m_BlueTacticLabel != null)
            {
                m_BlueTacticLabel.text = $"BLUE  {environment.GetActiveTacticLabel(Team.Blue)}";
            }

            if (m_PurpleTacticLabel != null)
            {
                m_PurpleTacticLabel.text = $"PURPLE  {environment.GetActiveTacticLabel(Team.Purple)}";
            }

            if (m_BlueRewardLabel != null)
            {
                m_BlueRewardLabel.text = $"BLUE   {environment.GetCumulativeReward(Team.Blue):+0.000;-0.000;0.000}";
            }

            if (m_PurpleRewardLabel != null)
            {
                m_PurpleRewardLabel.text = $"PURPLE {environment.GetCumulativeReward(Team.Purple):+0.000;-0.000;0.000}";
            }

            var message = environment.GetCenterMessage();
            m_ResultLabel.text = message;
            m_ResultLabel.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void Configure(SoccerEnvController configuredEnvironment)
        {
            environment = configuredEnvironment;
        }

        public static string FormatTime(float seconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }
    }
}
