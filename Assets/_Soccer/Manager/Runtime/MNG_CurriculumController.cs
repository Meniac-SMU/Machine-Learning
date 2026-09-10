using System;
using System.Linq;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public enum MNG_CurriculumOutcome
    {
        None = 0,
        Goal = 1,
        OwnGoal = 2,
        Timeout = 3
    }

    [DefaultExecutionOrder(-450)]
    [DisallowMultipleComponent]
    public sealed class MNG_CurriculumController : MonoBehaviour
    {
        [SerializeField] MNG_CurriculumCatalog catalog;
        [SerializeField] MNG_CurriculumStage stage = MNG_CurriculumStage.M1AttackChoice;
        [SerializeField] MNG_MatchController matchController;
        [SerializeField] MNG_BallControl ballControl;
        [SerializeField] MNG_RewardEngine rewardEngine;
        [SerializeField] bool useValidationScenarios;
        [SerializeField] bool movingNavyDefense;

        MNG_ManagerAgent m_TrainingManager;
        int m_EpisodeIndex;
        bool m_EpisodePending;
        MNG_CurriculumOutcome m_PendingOutcome;
        float m_OpponentPossessionSeconds;
        float m_RecoveryPossessionSeconds;
        bool m_OpponentPossessionQualified;
        bool m_CurrentEpisodeRecovered;

        public MNG_CurriculumStage Stage => stage;
        public int EpisodeIndex => m_EpisodeIndex;
        public MNG_AttackScenario CurrentScenario { get; private set; }
        public MNG_DefenseScenario CurrentDefenseScenario { get; private set; }
        public MNG_CurriculumOutcome LastOutcome { get; private set; }
        public int CompletedEpisodes { get; private set; }
        public int Goals { get; private set; }
        public int OwnGoals { get; private set; }
        public int Timeouts { get; private set; }
        public int Recoveries { get; private set; }
        public int RecoveriesWithinDeadline { get; private set; }
        public bool CurrentEpisodeRecovered => m_CurrentEpisodeRecovered;
        public bool UsesValidationScenarios => useValidationScenarios;
        public bool UsesMovingNavyDefense => movingNavyDefense;

        public event Action<int, MNG_CurriculumOutcome> EpisodeCompleted;

        void Awake()
        {
            if (catalog == null) throw new InvalidOperationException("MNG curriculum requires a catalog.");
            catalog.ValidateOrThrow();
            if (stage != MNG_CurriculumStage.M1AttackChoice
                && stage != MNG_CurriculumStage.M2DefenseChoice)
                throw new InvalidOperationException($"Unsupported MNG curriculum stage: {stage}.");
            if (matchController == null) matchController = GetComponent<MNG_MatchController>();
            if (ballControl == null) ballControl = GetComponentInChildren<MNG_BallControl>();
            if (rewardEngine == null) rewardEngine = GetComponent<MNG_RewardEngine>();
            if (matchController == null || ballControl == null)
                throw new InvalidOperationException("MNG curriculum requires match and ball controllers.");
        }

        void OnEnable() => matchController.GoalScored += OnGoalScored;
        void OnDisable()
        {
            if (matchController != null) matchController.GoalScored -= OnGoalScored;
        }

        void Start()
        {
            var activeManagers = GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Where(manager => manager.isActiveAndEnabled)
                .ToArray();
            if (activeManagers.Length != 1 || activeManagers[0].Team != Team.Red)
                throw new InvalidOperationException(
                    $"MNG M1 requires exactly one active Red manager, found {activeManagers.Length}.");
            m_TrainingManager = activeManagers[0];

            foreach (var fallback in GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = (stage == MNG_CurriculumStage.M2DefenseChoice
                        || (stage == MNG_CurriculumStage.M1AttackChoice && movingNavyDefense))
                    && fallback.Team == Team.Navy;
            foreach (var avatar in GetComponentsInChildren<MNG_PlayerAvatar>(true))
            {
                if (stage == MNG_CurriculumStage.M1AttackChoice
                    && !movingNavyDefense
                    && avatar.Team == Team.Navy)
                    avatar.GetComponent<MNG_PlayerSkillExecutor>().enabled = false;
            }

            matchController.ConfigureMatchDuration(catalog.GetEpisodeSeconds(stage), false);
            Time.timeScale = catalog.GetTimeScale(stage);
            BeginScenario(0);
        }

        void FixedUpdate()
        {
            if (m_EpisodePending)
            {
                CompleteAndRestart(m_PendingOutcome);
                return;
            }
            if (stage == MNG_CurriculumStage.M2DefenseChoice) UpdateDefenseRecovery();
            if (matchController.EpisodeElapsedSeconds >= catalog.GetEpisodeSeconds(stage))
                CompleteAndRestart(MNG_CurriculumOutcome.Timeout);
        }

        public void Configure(
            MNG_CurriculumCatalog configuredCatalog,
            MNG_MatchController configuredMatch,
            MNG_BallControl configuredBall,
            bool validationScenarios = false)
        {
            catalog = configuredCatalog != null
                ? configuredCatalog
                : throw new ArgumentNullException(nameof(configuredCatalog));
            matchController = configuredMatch != null
                ? configuredMatch
                : throw new ArgumentNullException(nameof(configuredMatch));
            ballControl = configuredBall != null
                ? configuredBall
                : throw new ArgumentNullException(nameof(configuredBall));
            stage = MNG_CurriculumStage.M1AttackChoice;
            useValidationScenarios = validationScenarios;
            movingNavyDefense = false;
        }

        public void ConfigureMovingDefense(
            MNG_CurriculumCatalog configuredCatalog,
            MNG_MatchController configuredMatch,
            MNG_BallControl configuredBall,
            bool validationScenarios = false)
        {
            Configure(configuredCatalog, configuredMatch, configuredBall, validationScenarios);
            movingNavyDefense = true;
        }

        public void ConfigureDefense(
            MNG_CurriculumCatalog configuredCatalog,
            MNG_MatchController configuredMatch,
            MNG_BallControl configuredBall,
            bool validationScenarios = false)
        {
            Configure(configuredCatalog, configuredMatch, configuredBall, validationScenarios);
            stage = MNG_CurriculumStage.M2DefenseChoice;
        }

        public void BeginScenario(int scenarioIndex)
        {
            if (scenarioIndex < 0) throw new ArgumentOutOfRangeException(nameof(scenarioIndex));
            m_EpisodeIndex = scenarioIndex;
            m_EpisodePending = false;
            m_PendingOutcome = MNG_CurriculumOutcome.None;
            m_OpponentPossessionSeconds = 0f;
            m_RecoveryPossessionSeconds = 0f;
            m_OpponentPossessionQualified = false;
            m_CurrentEpisodeRecovered = false;
            matchController.ResetMatch();

            var seed = catalog.GetSeed(stage, useValidationScenarios);
            if (stage == MNG_CurriculumStage.M2DefenseChoice)
            {
                CurrentDefenseScenario = MNG_DefenseScenarioGenerator.Generate(
                    seed, scenarioIndex, matchController.Snapshot.FieldHalfLength);
                ApplyScenario(CurrentDefenseScenario);
                Debug.Log($"MNG M2 EPISODE START index={scenarioIndex} seed={seed} "
                    + $"kind={CurrentDefenseScenario.Kind} neutral={CurrentDefenseScenario.StartsNeutral} "
                    + $"ball={CurrentDefenseScenario.BallPosition}");
            }
            else
            {
                CurrentScenario = MNG_AttackScenarioGenerator.Generate(
                    seed, scenarioIndex, matchController.Snapshot.FieldHalfLength);
                ApplyScenario(CurrentScenario);
                Debug.Log($"MNG M1 EPISODE START index={scenarioIndex} seed={seed} "
                    + $"kind={CurrentScenario.Kind} ball={CurrentScenario.BallPosition}");
            }
        }

        public void SetValidationScenarios(bool enabled)
        {
            useValidationScenarios = enabled;
        }

        void ApplyScenario(MNG_AttackScenario scenario)
        {
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                SetPlayerPose(matchController.GetPlayerAvatar(Team.Red, slot), scenario.RedPositions[slot], Vector2.right);
                SetPlayerPose(matchController.GetPlayerAvatar(Team.Navy, slot), scenario.NavyPositions[slot], Vector2.left);
            }

            var body = ballControl.GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.position = new Vector3(scenario.BallPosition.x, body.position.y, scenario.BallPosition.y);
            body.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            ballControl.ResetLedger();
            Physics.SyncTransforms();
        }

        void ApplyScenario(MNG_DefenseScenario scenario)
        {
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                SetPlayerPose(matchController.GetPlayerAvatar(Team.Red, slot), scenario.RedPositions[slot], Vector2.right);
                SetPlayerPose(matchController.GetPlayerAvatar(Team.Navy, slot), scenario.NavyPositions[slot], Vector2.left);
            }

            var body = ballControl.GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.position = new Vector3(scenario.BallPosition.x, body.position.y, scenario.BallPosition.y);
            body.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            ballControl.ResetLedger();
            Physics.SyncTransforms();
        }

        void UpdateDefenseRecovery()
        {
            var possession = matchController.Snapshot.Possession;
            if (!m_OpponentPossessionQualified)
            {
                m_OpponentPossessionSeconds = possession == MNG_Possession.Navy
                    ? m_OpponentPossessionSeconds + Time.fixedDeltaTime
                    : 0f;
                m_OpponentPossessionQualified = m_OpponentPossessionSeconds
                    >= MNG_CurriculumCatalog.M2OpponentPossessionSeconds;
                return;
            }
            if (m_CurrentEpisodeRecovered) return;

            m_RecoveryPossessionSeconds = possession == MNG_Possession.Red
                ? m_RecoveryPossessionSeconds + Time.fixedDeltaTime
                : 0f;
            if (m_RecoveryPossessionSeconds < MNG_CurriculumCatalog.M2RecoveryPossessionSeconds)
                return;

            m_CurrentEpisodeRecovered = true;
            Recoveries++;
            var withinDeadline = matchController.EpisodeElapsedSeconds
                <= MNG_CurriculumCatalog.M2RecoveryDeadlineSeconds;
            if (withinDeadline) RecoveriesWithinDeadline++;
            var eventId = ((long)m_EpisodeIndex << 1) | 1L;
            rewardEngine?.Award(Team.Red, MNG_RewardEventKind.Recovery, eventId);
            if (withinDeadline)
                rewardEngine?.Award(Team.Red, MNG_RewardEventKind.FastRecovery, eventId);
            Debug.Log($"MNG M2 RECOVERY index={m_EpisodeIndex} "
                + $"elapsed={matchController.EpisodeElapsedSeconds:R} fast={withinDeadline}");
        }

        void OnGoalScored(Team scoringTeam, long goalId)
        {
            if (m_EpisodePending) return;
            m_PendingOutcome = scoringTeam == Team.Red
                ? MNG_CurriculumOutcome.Goal
                : MNG_CurriculumOutcome.OwnGoal;
            m_EpisodePending = true;
        }

        void CompleteAndRestart(MNG_CurriculumOutcome outcome)
        {
            LastOutcome = outcome;
            CompletedEpisodes++;
            if (outcome == MNG_CurriculumOutcome.Goal) Goals++;
            else if (outcome == MNG_CurriculumOutcome.OwnGoal) OwnGoals++;
            else Timeouts++;

            Debug.Log($"MNG {stage} EPISODE END index={m_EpisodeIndex} outcome={outcome} "
                + $"elapsed={matchController.EpisodeElapsedSeconds:R} score="
                + $"{matchController.RedScore}:{matchController.NavyScore} "
                + $"recovered={m_CurrentEpisodeRecovered}");
            EpisodeCompleted?.Invoke(m_EpisodeIndex, outcome);
            matchController.EndActivePolicyEpisodes();
            BeginScenario(m_EpisodeIndex + 1);
        }

        static void SetPlayerPose(MNG_PlayerAvatar avatar, Vector2 position, Vector2 forward)
        {
            var worldPosition = new Vector3(position.x, avatar.Body.position.y, position.y);
            var rotation = Quaternion.LookRotation(new Vector3(forward.x, 0f, forward.y), Vector3.up);
            avatar.transform.SetPositionAndRotation(worldPosition, rotation);
            avatar.Body.position = worldPosition;
            avatar.Body.rotation = rotation;
            avatar.Body.linearVelocity = Vector3.zero;
            avatar.Body.angularVelocity = Vector3.zero;
            avatar.ResetKickCooldown();
            avatar.GetComponent<MNG_PlayerMotor>().ResetDriveCommand();
            avatar.GetComponent<MNG_PlayerSkillExecutor>().ResetForRound();
        }
    }
}
