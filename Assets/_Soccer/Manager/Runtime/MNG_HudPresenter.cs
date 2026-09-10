using MachineLearning.Soccer;
using UnityEngine;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class MNG_HudPresenter : MonoBehaviour
    {
        [SerializeField] MNG_MatchController matchController;
        [SerializeField] MNG_RewardEngine rewardEngine;

        UIDocument m_Document;
        Label m_ScoreLabel;
        Label m_TimeLabel;
        Label m_AILabel;
        Label m_HumanLabel;
        Label m_ResultLabel;
        Label m_RedTacticLabel;
        Label m_NavyTacticLabel;
        Label m_RedRewardLabel;
        Label m_NavyRewardLabel;

        public MNG_MatchController MatchController => matchController;

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
            if (m_Document == null) return;
            var root = m_Document.rootVisualElement;
            m_ScoreLabel = root.Q<Label>("score-label");
            m_TimeLabel = root.Q<Label>("time-label");
            m_AILabel = root.Q<Label>("ai-label");
            m_HumanLabel = root.Q<Label>("human-label");
            m_ResultLabel = root.Q<Label>("result-label");
            m_RedTacticLabel = root.Q<Label>("red-tactic-label");
            m_NavyTacticLabel = root.Q<Label>("navy-tactic-label");
            m_RedRewardLabel = root.Q<Label>("red-reward-label");
            m_NavyRewardLabel = root.Q<Label>("navy-reward-label");
        }

        void Update()
        {
            if (matchController == null || m_ScoreLabel == null) return;
            m_ScoreLabel.text = $"{SoccerTeamVisuals.RedDisplayName}  {matchController.RedScore}  :  "
                + $"{matchController.NavyScore}  {SoccerTeamVisuals.NavyDisplayName}";
            m_TimeLabel.text = FormatTime(matchController.MatchRemainingSeconds);

            var humanEnabled = matchController.GetPlayerAvatar(Team.Red, 3).IsHuman;
            m_AILabel.text = humanEnabled ? "AI  SUPPORT" : "AI  ON";
            m_HumanLabel.text = humanEnabled ? "HUMAN  ON" : "HUMAN  OFF";
            m_AILabel.EnableInClassList("mode-on", !humanEnabled);
            m_AILabel.EnableInClassList("mode-off", humanEnabled);
            m_HumanLabel.EnableInClassList("mode-on", humanEnabled);
            m_HumanLabel.EnableInClassList("mode-off", !humanEnabled);

            SetText(m_RedTacticLabel,
                $"{SoccerTeamVisuals.RedDisplayName}  {matchController.GetDecisionState(Team.Red).PreviousCommand}");
            SetText(m_NavyTacticLabel,
                $"{SoccerTeamVisuals.NavyDisplayName}  {matchController.GetDecisionState(Team.Navy).PreviousCommand}");
            if (rewardEngine != null)
            {
                SetText(m_RedRewardLabel,
                    $"{SoccerTeamVisuals.RedDisplayName}   {rewardEngine.GetCumulativeReward(Team.Red):+0.000;-0.000;0.000}");
                SetText(m_NavyRewardLabel,
                    $"{SoccerTeamVisuals.NavyDisplayName} {rewardEngine.GetCumulativeReward(Team.Navy):+0.000;-0.000;0.000}");
            }

            if (m_ResultLabel == null) return;
            var message = GetCenterMessage();
            m_ResultLabel.text = message;
            m_ResultLabel.style.display = string.IsNullOrEmpty(message)
                ? DisplayStyle.None
                : DisplayStyle.Flex;
        }

        public void Configure(MNG_MatchController configuredMatch, MNG_RewardEngine configuredReward)
        {
            matchController = configuredMatch;
            rewardEngine = configuredReward;
        }

        public static string FormatTime(float seconds)
        {
            var totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
        }

        string GetCenterMessage()
        {
            if (matchController.State == MNG_MatchState.GoalPause) return "GOAL";
            if (matchController.State != MNG_MatchState.Finished) return string.Empty;
            if (matchController.RedScore == matchController.NavyScore) return "FULL TIME\nDRAW";
            var winner = matchController.RedScore > matchController.NavyScore
                ? SoccerTeamVisuals.RedDisplayName
                : SoccerTeamVisuals.NavyDisplayName;
            return $"FULL TIME\n{winner} WINS";
        }

        static void SetText(Label label, string value)
        {
            if (label != null) label.text = value;
        }
    }
}
