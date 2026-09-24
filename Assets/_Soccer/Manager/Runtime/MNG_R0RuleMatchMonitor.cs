using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    /// <summary>
    /// R0-only diagnostics for a continuously viewable Rule-vs-Rule match.
    /// It observes accepted commands and results but never changes a decision.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class MNG_R0RuleMatchMonitor : MonoBehaviour
    {
        [SerializeField] MNG_MatchController matchController;
        [SerializeField] MNG_RuleBasedManager redManager;
        [SerializeField] MNG_RuleBasedManager navyManager;
        [SerializeField, Min(0.1f)] float presentationTimeScale = 1f;

        readonly int[,] m_CommandCounts = new int[2, MNG_CommandMask.CommandCount];
        float m_OriginalTimeScale;
        bool m_FinishedRecorded;

        public int CompletedMatches { get; private set; }
        public int TotalAcceptedCommands { get; private set; }
        public MNG_MatchController MatchController => matchController;
        public MNG_RuleBasedManager RedManager => redManager;
        public MNG_RuleBasedManager NavyManager => navyManager;

        void Awake()
        {
            if (matchController == null) matchController = GetComponent<MNG_MatchController>();
            if (matchController == null) throw new InvalidOperationException("R0 monitor requires the MNG match.");
            if (redManager == null || navyManager == null)
            {
                var managers = GetComponentsInChildren<MNG_RuleBasedManager>(true);
                foreach (var manager in managers)
                {
                    if (manager.Team == Team.Red) redManager = manager;
                    else navyManager = manager;
                }
            }
            ValidateBindingsOrThrow();
            m_OriginalTimeScale = Time.timeScale;
            Time.timeScale = presentationTimeScale;
        }

        void OnEnable()
        {
            if (matchController != null) matchController.CommandAccepted += OnCommandAccepted;
        }

        void OnDisable()
        {
            if (matchController != null) matchController.CommandAccepted -= OnCommandAccepted;
            Time.timeScale = m_OriginalTimeScale;
        }

        void FixedUpdate()
        {
            if (matchController.State == MNG_MatchState.Finished)
            {
                if (m_FinishedRecorded) return;
                m_FinishedRecorded = true;
                CompletedMatches++;
                Debug.Log(
                    $"MNG R0 RULE MATCH COMPLETE index={CompletedMatches} "
                    + $"score={matchController.RedScore}:{matchController.NavyScore} "
                    + $"commands={TotalAcceptedCommands} "
                    + $"redCommands=[{GetCommandSummary(Team.Red)}] "
                    + $"navyCommands=[{GetCommandSummary(Team.Navy)}] "
                    + $"redReason={redManager.LastDecision.Reason} "
                    + $"navyReason={navyManager.LastDecision.Reason}");
                return;
            }

            if (matchController.State == MNG_MatchState.Playing) m_FinishedRecorded = false;
        }

        public void Configure(
            MNG_MatchController configuredMatch,
            MNG_RuleBasedManager configuredRed,
            MNG_RuleBasedManager configuredNavy,
            float configuredPresentationTimeScale = 1f)
        {
            matchController = configuredMatch ?? throw new ArgumentNullException(nameof(configuredMatch));
            redManager = configuredRed ?? throw new ArgumentNullException(nameof(configuredRed));
            navyManager = configuredNavy ?? throw new ArgumentNullException(nameof(configuredNavy));
            presentationTimeScale = Mathf.Max(0.1f, configuredPresentationTimeScale);
            ValidateBindingsOrThrow();
        }

        public int GetCommandCount(Team team, MNG_Command command)
            => m_CommandCounts[team == Team.Red ? 0 : 1, (int)command];

        public string GetCommandSummary(Team team)
        {
            return $"Carry={GetCommandCount(team, MNG_Command.AdvanceCarry)},"
                + $"Pass={GetCommandCount(team, MNG_Command.PassBuild)},"
                + $"Shot={GetCommandCount(team, MNG_Command.AttemptShot)},"
                + $"Recover={GetCommandCount(team, MNG_Command.ActiveRecover)},"
                + $"Balanced={GetCommandCount(team, MNG_Command.Balanced)},"
                + $"Protect={GetCommandCount(team, MNG_Command.ProtectBack)}";
        }

        void OnCommandAccepted(Team team, MNG_Command command, long tick)
        {
            m_CommandCounts[team == Team.Red ? 0 : 1, (int)command]++;
            TotalAcceptedCommands++;
        }

        void ValidateBindingsOrThrow()
        {
            if (redManager == null || redManager.Team != Team.Red)
                throw new InvalidOperationException("R0 monitor requires the Red rule manager.");
            if (navyManager == null || navyManager.Team != Team.Navy)
                throw new InvalidOperationException("R0 monitor requires the Navy rule manager.");
            if (redManager == navyManager)
                throw new InvalidOperationException("R0 rule teams must use distinct manager components.");
        }
    }
}
