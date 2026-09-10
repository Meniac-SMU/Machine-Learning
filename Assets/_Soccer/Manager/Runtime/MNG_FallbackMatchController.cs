using System;
using System.Linq;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    [DefaultExecutionOrder(-450)]
    [DisallowMultipleComponent]
    public sealed class MNG_FallbackMatchController : MonoBehaviour
    {
        [SerializeField] MNG_CurriculumCatalog catalog;
        [SerializeField] MNG_MatchController matchController;
        [SerializeField] MNG_BallControl ballControl;
        [SerializeField] bool fullMatch;
        [SerializeField] bool useValidationScenarios;

        int m_MatchIndex;

        public MNG_CurriculumStage Stage => MNG_CurriculumStage.M3FallbackMatch;
        public int MatchIndex => m_MatchIndex;
        public bool IsFullMatch => fullMatch;
        public bool UsesValidationScenarios => useValidationScenarios;
        public MNG_FallbackMatchScenario CurrentScenario { get; private set; }
        public int CompletedMatches { get; private set; }
        public int RedWins { get; private set; }
        public int Draws { get; private set; }
        public int RedLosses { get; private set; }
        public int RedGoals { get; private set; }
        public int NavyGoals { get; private set; }
        public float ScoreRate => CompletedMatches > 0
            ? (RedWins + 0.5f * Draws) / CompletedMatches
            : 0f;

        public event Action<int, int, int> MatchCompleted;

        void Awake()
        {
            if (catalog == null) throw new InvalidOperationException("MNG M3 requires a curriculum catalog.");
            catalog.ValidateOrThrow();
            if (matchController == null) matchController = GetComponent<MNG_MatchController>();
            if (ballControl == null) ballControl = GetComponentInChildren<MNG_BallControl>();
            if (matchController == null || ballControl == null)
                throw new InvalidOperationException("MNG M3 requires match and ball controllers.");
        }

        void Start()
        {
            var activeManagers = GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Where(manager => manager.isActiveAndEnabled)
                .ToArray();
            if (activeManagers.Length != 1 || activeManagers[0].Team != Team.Red)
                throw new InvalidOperationException(
                    $"MNG M3 requires exactly one active Red manager, found {activeManagers.Length}.");

            foreach (var fallback in GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = fallback.Team == Team.Navy;
            matchController.ConfigureMatchDuration(
                catalog.GetM3MatchSeconds(fullMatch),
                false,
                fullMatch
                    ? MNG_MatchFinishMode.TerminalResult
                    : MNG_MatchFinishMode.InterruptedCollection);
            Time.timeScale = catalog.GetTimeScale(Stage);
            BeginMatch(0);
        }

        void FixedUpdate()
        {
            if (matchController.State != MNG_MatchState.Finished) return;
            CompleteMatch();
            BeginMatch(m_MatchIndex + 1);
        }

        public void Configure(
            MNG_CurriculumCatalog configuredCatalog,
            MNG_MatchController configuredMatch,
            MNG_BallControl configuredBall,
            bool configuredFullMatch,
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
            fullMatch = configuredFullMatch;
            useValidationScenarios = validationScenarios;
        }

        public void SetValidationScenarios(bool enabled) => useValidationScenarios = enabled;

        public void BeginMatch(int matchIndex)
        {
            if (matchIndex < 0) throw new ArgumentOutOfRangeException(nameof(matchIndex));
            m_MatchIndex = matchIndex;
            matchController.ResetMatch();
            var seed = catalog.GetSeed(Stage, useValidationScenarios);
            CurrentScenario = MNG_FallbackMatchScenarioGenerator.Generate(
                seed, matchIndex, matchController.Snapshot.FieldHalfLength);
            ApplyScenario(CurrentScenario);
            Debug.Log($"MNG M3 MATCH START index={matchIndex} seed={seed} "
                + $"kind={CurrentScenario.Kind} full={fullMatch} ball={CurrentScenario.BallPosition}");
        }

        void CompleteMatch()
        {
            var redScore = matchController.RedScore;
            var navyScore = matchController.NavyScore;
            CompletedMatches++;
            RedGoals += redScore;
            NavyGoals += navyScore;
            if (redScore > navyScore) RedWins++;
            else if (redScore < navyScore) RedLosses++;
            else Draws++;
            Debug.Log($"MNG M3 MATCH END index={m_MatchIndex} full={fullMatch} "
                + $"score={redScore}:{navyScore} aggregate={RedWins}-{Draws}-{RedLosses} "
                + $"scoreRate={ScoreRate:R}");
            MatchCompleted?.Invoke(m_MatchIndex, redScore, navyScore);
        }

        void ApplyScenario(MNG_FallbackMatchScenario scenario)
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
