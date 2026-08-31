using UnityEngine;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class SoccerHudController : MonoBehaviour
    {
        public const float RewardPanelEdgeInset = 20f;

        [SerializeField] SoccerEnvController environment;
        [SerializeField] bool rewardPanelBottomRight;

        UIDocument m_Document;
        VisualElement m_TacticRewardBox;
        Label m_ScoreLabel;
        Label m_TimeLabel;
        Label m_AILabel;
        Label m_HumanLabel;
        Label m_ResultLabel;
        Label m_RedTacticLabel;
        Label m_NavyTacticLabel;
        Label m_RedRewardLabel;
        Label m_NavyRewardLabel;

        public SoccerEnvController Environment => environment;
        public bool RewardPanelBottomRight => rewardPanelBottomRight;

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
            m_TacticRewardBox = root.Q<VisualElement>(className: "tactic-reward-box");
            m_ScoreLabel = root.Q<Label>("score-label");
            m_TimeLabel = root.Q<Label>("time-label");
            m_AILabel = root.Q<Label>("ai-label");
            m_HumanLabel = root.Q<Label>("human-label");
            m_ResultLabel = root.Q<Label>("result-label");
            m_RedTacticLabel = root.Q<Label>("red-tactic-label");
            m_NavyTacticLabel = root.Q<Label>("navy-tactic-label");
            m_RedRewardLabel = root.Q<Label>("red-reward-label");
            m_NavyRewardLabel = root.Q<Label>("navy-reward-label");
            ApplyRewardPanelPlacement();
        }

        void Update()
        {
            if (environment == null || m_ScoreLabel == null)
            {
                return;
            }

            m_ScoreLabel.text = $"{SoccerTeamVisuals.RedDisplayName}  {environment.RedScore}  :  "
                + $"{environment.NavyScore}  {SoccerTeamVisuals.NavyDisplayName}";
            m_TimeLabel.text = FormatTime(environment.RemainingTime);
            m_AILabel.text = environment.IsAIEnabled ? "AI  ON" : "AI  OFF";
            m_HumanLabel.text = environment.IsAIEnabled ? "HUMAN  OFF" : "HUMAN  ON";
            m_AILabel.EnableInClassList("mode-on", environment.IsAIEnabled);
            m_AILabel.EnableInClassList("mode-off", !environment.IsAIEnabled);
            m_HumanLabel.EnableInClassList("mode-on", !environment.IsAIEnabled);
            m_HumanLabel.EnableInClassList("mode-off", environment.IsAIEnabled);
            if (m_RedTacticLabel != null)
            {
                m_RedTacticLabel.text = $"{SoccerTeamVisuals.RedDisplayName}  {environment.GetActiveTacticLabel(Team.Red)}";
            }

            if (m_NavyTacticLabel != null)
            {
                m_NavyTacticLabel.text = $"{SoccerTeamVisuals.NavyDisplayName}  {environment.GetActiveTacticLabel(Team.Navy)}";
            }

            if (m_RedRewardLabel != null)
            {
                m_RedRewardLabel.text = $"{SoccerTeamVisuals.RedDisplayName}   {environment.GetCumulativeReward(Team.Red):+0.000;-0.000;0.000}";
            }

            if (m_NavyRewardLabel != null)
            {
                m_NavyRewardLabel.text = $"{SoccerTeamVisuals.NavyDisplayName} {environment.GetCumulativeReward(Team.Navy):+0.000;-0.000;0.000}";
            }

            var message = environment.GetCenterMessage();
            m_ResultLabel.text = message;
            m_ResultLabel.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        public void Configure(SoccerEnvController configuredEnvironment)
        {
            environment = configuredEnvironment;
        }

        public void ConfigureRewardPanelBottomRight(bool enabled)
        {
            rewardPanelBottomRight = enabled;
            ApplyRewardPanelPlacement();
        }

        void ApplyRewardPanelPlacement()
        {
            if (m_TacticRewardBox == null)
            {
                return;
            }

            if (rewardPanelBottomRight)
            {
                m_TacticRewardBox.style.top = StyleKeyword.Auto;
                m_TacticRewardBox.style.bottom = RewardPanelEdgeInset;
                m_TacticRewardBox.style.right = RewardPanelEdgeInset;
                m_TacticRewardBox.style.translate = new Translate(0f, 0f);
                return;
            }

            m_TacticRewardBox.style.top = StyleKeyword.Null;
            m_TacticRewardBox.style.bottom = StyleKeyword.Null;
            m_TacticRewardBox.style.right = StyleKeyword.Null;
            m_TacticRewardBox.style.translate = StyleKeyword.Null;
        }

        public static string FormatTime(float seconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }
    }
}
