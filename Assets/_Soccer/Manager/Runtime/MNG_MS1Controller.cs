using System;
using System.IO;
using System.Linq;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public enum MNG_MS1SituationGroup
    {
        Attack = 0,
        DefenseTransition = 1
    }

    public readonly struct MNG_MS1EpisodeResult
    {
        public readonly int EpisodeIndex;
        public readonly MNG_MS1SituationGroup Group;
        public readonly string Kind;
        public readonly string Outcome;
        public readonly float ElapsedSeconds;
        public readonly bool Recovered;

        public MNG_MS1EpisodeResult(
            int episodeIndex,
            MNG_MS1SituationGroup group,
            string kind,
            string outcome,
            float elapsedSeconds,
            bool recovered)
        {
            EpisodeIndex = episodeIndex;
            Group = group;
            Kind = kind;
            Outcome = outcome;
            ElapsedSeconds = elapsedSeconds;
            Recovered = recovered;
        }
    }

    [DefaultExecutionOrder(-450)]
    [DisallowMultipleComponent]
    public sealed class MNG_MS1Controller : MonoBehaviour
    {
        public const float EpisodeSeconds = 30f;
        public const float DefaultTimeScale = 10f;
        public const float RecoveryHoldSeconds = 0.5f;
        public const int TrainingSeed = 191001;
        public const int EvaluationAttackSeed = 405001;
        public const int EvaluationDefenseSeed = 406001;

        [SerializeField] MNG_MSOpponentProfile opponentProfile;
        [SerializeField] MNG_MatchController matchController;
        [SerializeField] MNG_BallControl ballControl;
        [SerializeField] MNG_RewardEngine rewardEngine;
        [SerializeField, Min(1f)] float timeScale = DefaultTimeScale;

        readonly long[] m_CommandCounts = new long[MNG_CommandMask.CommandCount];
        MNG_ManagerAgent m_Manager;
        int m_WorkerIdentity;
        int m_ProcessId;
        int m_EpisodeIndex;
        bool m_PassRepairMode;
        [SerializeField] int m_AttackScenarioSeed = TrainingSeed;
        [SerializeField] int m_DefenseScenarioSeed = TrainingSeed;
        bool m_GoalPending;
        Team m_ScoringTeam;
        float m_RecoverySeconds;
        bool m_Recovered;
        string m_EvidencePath;

        public MNG_MSOpponentProfile OpponentProfile => opponentProfile;
        public MNG_MS1SituationGroup CurrentGroup { get; private set; }
        public MNG_AttackScenario CurrentAttackScenario { get; private set; }
        public MNG_DefenseScenario CurrentDefenseScenario { get; private set; }
        public int EpisodeIndex => m_EpisodeIndex;
        public int WorkerIdentity => m_WorkerIdentity;
        public float TimeScale => timeScale;
        public int AttackScenarioSeed => m_AttackScenarioSeed;
        public int DefenseScenarioSeed => m_DefenseScenarioSeed;
        public bool CurrentEpisodeRecovered => m_Recovered;
        public event Action<MNG_MS1EpisodeResult> EpisodeCompleted;

        void Awake()
        {
            if (matchController == null) matchController = GetComponent<MNG_MatchController>();
            if (ballControl == null) ballControl = GetComponentInChildren<MNG_BallControl>();
            if (rewardEngine == null) rewardEngine = GetComponent<MNG_RewardEngine>();
            if (matchController == null || ballControl == null)
                throw new InvalidOperationException("MS1 requires match and ball controllers.");
            if (opponentProfile == null)
                throw new InvalidOperationException("MS1 requires an R0 opponent profile.");
            opponentProfile.ValidateOrThrow();
            if (opponentProfile.Strength != MNG_MSOpponentStrength.Easy
                && opponentProfile.Strength != MNG_MSOpponentStrength.Rescue)
                throw new InvalidOperationException("MS1 only supports Easy or Rescue R0 profiles.");
            if (!MNG_MatchSnapshot.IsFinite(timeScale) || timeScale < 1f)
                throw new InvalidOperationException("MS1 time scale must be finite and at least one.");

            m_WorkerIdentity = MNG_MSController.ResolveWorkerIdentity(Environment.GetCommandLineArgs());
            m_ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
            m_PassRepairMode = string.Equals(
                ResolveArgument(Environment.GetCommandLineArgs(), "-mngMS1PassRepair"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            matchController.ConfigureSpawnSeedOffset(m_WorkerIdentity);
            matchController.ConfigureMatchDuration(
                EpisodeSeconds,
                false,
                MNG_MatchFinishMode.InterruptedCollection);
            Time.timeScale = timeScale;
        }

        void OnEnable()
        {
            if (matchController == null) return;
            matchController.GoalScored += OnGoalScored;
            matchController.CommandAccepted += OnCommandAccepted;
        }

        void OnDisable()
        {
            if (matchController == null) return;
            matchController.GoalScored -= OnGoalScored;
            matchController.CommandAccepted -= OnCommandAccepted;
        }

        void Start()
        {
            foreach (var human in GetComponentsInChildren<MNG_HumanInput>(true)) human.enabled = false;
            foreach (var fallback in GetComponentsInChildren<MNG_FallbackManager>(true)) fallback.enabled = false;

            var managers = GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Where(manager => manager.isActiveAndEnabled).ToArray();
            var rules = GetComponentsInChildren<MNG_RuleBasedManager>(true)
                .Where(rule => rule.isActiveAndEnabled).ToArray();
            if (managers.Length != 1 || managers[0].Team != Team.Red)
                throw new InvalidOperationException(
                    $"MS1 requires exactly one active Red policy manager, found {managers.Length}.");
            if (rules.Length != 1 || rules[0].Team != Team.Navy)
                throw new InvalidOperationException(
                    $"MS1 requires exactly one active Navy rule manager, found {rules.Length}.");
            m_Manager = managers[0];
            rules[0].ConfigureDecisionInterval(opponentProfile.DecisionIntervalSeconds);
            foreach (var avatar in GetComponentsInChildren<MNG_PlayerAvatar>(true))
            {
                avatar.GetComponent<MNG_PlayerMotor>().ConfigureSpeedMultiplier(
                    avatar.Team == Team.Navy ? opponentProfile.MovementSpeedMultiplier : 1f);
            }

            InitializeEvidence(Environment.GetCommandLineArgs());
            BeginEpisode(0);
            Debug.Log(
                $"MNG MS1 BOOTSTRAP PASS worker={m_WorkerIdentity} process={m_ProcessId} "
                + $"profile={opponentProfile.Strength} episodeSeconds={EpisodeSeconds:R} "
                + $"timeScale={timeScale:R}");
        }

        void FixedUpdate()
        {
            if (m_GoalPending)
            {
                CompleteEpisode(m_ScoringTeam == Team.Red ? "goal" : "conceded", false);
                return;
            }
            if (CurrentGroup == MNG_MS1SituationGroup.DefenseTransition && !m_Recovered)
            {
                m_RecoverySeconds = matchController.Snapshot.Possession == MNG_Possession.Red
                    ? m_RecoverySeconds + Time.fixedDeltaTime
                    : 0f;
                if (m_RecoverySeconds >= RecoveryHoldSeconds)
                {
                    m_Recovered = true;
                    Debug.Log($"MNG MS1 RECOVERY index={m_EpisodeIndex} "
                        + $"elapsed={matchController.EpisodeElapsedSeconds:R}");
                }
            }
            if (matchController.State == MNG_MatchState.Finished)
                CompleteEpisode("timeout", true);
        }

        public void Configure(
            MNG_MSOpponentProfile configuredProfile,
            MNG_MatchController configuredMatch,
            MNG_BallControl configuredBall,
            float configuredTimeScale = DefaultTimeScale)
        {
            opponentProfile = configuredProfile != null
                ? configuredProfile
                : throw new ArgumentNullException(nameof(configuredProfile));
            matchController = configuredMatch != null
                ? configuredMatch
                : throw new ArgumentNullException(nameof(configuredMatch));
            ballControl = configuredBall != null
                ? configuredBall
                : throw new ArgumentNullException(nameof(configuredBall));
            timeScale = configuredTimeScale;
        }

        public void ConfigureScenarioSeeds(int attackSeed, int defenseSeed)
        {
            if (attackSeed <= 0) throw new ArgumentOutOfRangeException(nameof(attackSeed));
            if (defenseSeed <= 0) throw new ArgumentOutOfRangeException(nameof(defenseSeed));
            m_AttackScenarioSeed = attackSeed;
            m_DefenseScenarioSeed = defenseSeed;
        }

        public static MNG_MS1SituationGroup SituationForEpisode(int episodeIndex)
        {
            if (episodeIndex < 0) throw new ArgumentOutOfRangeException(nameof(episodeIndex));
            return (episodeIndex & 1) == 0
                ? MNG_MS1SituationGroup.Attack
                : MNG_MS1SituationGroup.DefenseTransition;
        }

        public static MNG_MS1SituationGroup PassRepairSituationForEpisode(int episodeIndex)
        {
            if (episodeIndex < 0) throw new ArgumentOutOfRangeException(nameof(episodeIndex));
            return episodeIndex % 4 == 3
                ? MNG_MS1SituationGroup.DefenseTransition
                : MNG_MS1SituationGroup.Attack;
        }

        public static int PassRepairSourceScenarioIndex(int attackEpisodeIndex)
        {
            if (attackEpisodeIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(attackEpisodeIndex));
            var pair = attackEpisodeIndex / 2;
            var mirror = attackEpisodeIndex & 1;
            return pair * 6 + 2 + mirror;
        }

        public static int DefenseSourceScenarioIndex(int defenseEpisodeIndex)
        {
            if (defenseEpisodeIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(defenseEpisodeIndex));
            var pair = defenseEpisodeIndex / 2;
            var mirror = defenseEpisodeIndex & 1;
            var kind = (pair & 1) == 0 ? 2 : ((pair / 2) & 1);
            var sourcePair = (pair / 2) * 3 + kind;
            return sourcePair * 2 + mirror;
        }

        void BeginEpisode(int episodeIndex)
        {
            m_EpisodeIndex = episodeIndex;
            m_GoalPending = false;
            m_RecoverySeconds = 0f;
            m_Recovered = false;
            Array.Clear(m_CommandCounts, 0, m_CommandCounts.Length);
            matchController.ResetMatch();
            CurrentGroup = m_PassRepairMode
                ? PassRepairSituationForEpisode(episodeIndex)
                : SituationForEpisode(episodeIndex);
            var scenarioSeed = CurrentGroup == MNG_MS1SituationGroup.Attack
                ? m_AttackScenarioSeed
                : m_DefenseScenarioSeed;
            var seed = unchecked(scenarioSeed + m_WorkerIdentity * 1009);
            if (CurrentGroup == MNG_MS1SituationGroup.Attack)
            {
                var attackIndex = m_PassRepairMode
                    ? episodeIndex - (episodeIndex + 1) / 4
                    : episodeIndex / 2;
                var sourceIndex = m_PassRepairMode
                    ? PassRepairSourceScenarioIndex(attackIndex)
                    : attackIndex;
                CurrentAttackScenario = PrepareAttackScenarioForMS1(
                    MNG_AttackScenarioGenerator.Generate(
                        seed, sourceIndex, matchController.Snapshot.FieldHalfLength));
                Apply(CurrentAttackScenario);
                matchController.SetPossession(
                    MNG_Possession.Red,
                    MNG_CarrierRef.For(Team.Red, CurrentAttackScenario.CarrierSlot));
                AppendEvidence("episode-start", $"\"episode\":{episodeIndex},"
                    + $"\"group\":\"Attack\",\"kind\":\"{CurrentAttackScenario.Kind}\","
                    + $"\"seed\":{CurrentAttackScenario.Seed},\"startsNeutral\":false");
            }
            else
            {
                var defenseIndex = m_PassRepairMode
                    ? episodeIndex / 4
                    : episodeIndex / 2;
                var sourceIndex = DefenseSourceScenarioIndex(defenseIndex);
                CurrentDefenseScenario = MNG_DefenseScenarioGenerator.Generate(
                    seed, sourceIndex, matchController.Snapshot.FieldHalfLength);
                Apply(CurrentDefenseScenario);
                matchController.SetPossession(
                    CurrentDefenseScenario.StartsNeutral ? MNG_Possession.Neutral : MNG_Possession.Navy,
                    CurrentDefenseScenario.StartsNeutral
                        ? MNG_CarrierRef.None
                        : MNG_CarrierRef.For(Team.Navy, CurrentDefenseScenario.NavyCarrierSlot));
                AppendEvidence("episode-start", $"\"episode\":{episodeIndex},"
                    + $"\"group\":\"DefenseTransition\","
                    + $"\"kind\":\"{CurrentDefenseScenario.Kind}\","
                    + $"\"seed\":{CurrentDefenseScenario.Seed},"
                    + $"\"startsNeutral\":{CurrentDefenseScenario.StartsNeutral.ToString().ToLowerInvariant()}");
            }
            Physics.SyncTransforms();
        }

        public static MNG_AttackScenario PrepareAttackScenarioForMS1(MNG_AttackScenario source)
        {
            if (source.Kind != MNG_AttackScenarioKind.Pass) return source;
            var red = (Vector2[])source.RedPositions.Clone();
            var navy = (Vector2[])source.NavyPositions.Clone();
            var mirror = source.BallPosition.y < 0f ? -1f : 1f;

            // Keep a physical diagonal outlet open while a defender blocks the
            // direct carry lane. The pass still needs a kick-plate strike and a
            // stable reception; this only removes the old impossible fixture.
            red[1] = source.BallPosition + new Vector2(-6f, -10f * mirror);
            red[2] = source.BallPosition + new Vector2(8f, 7f * mirror);
            navy[1] = source.BallPosition + new Vector2(3f, -1f * mirror);
            navy[2] = source.BallPosition + new Vector2(3f, -7f * mirror);
            navy[3] = source.BallPosition + new Vector2(-3f, -8f * mirror);
            return new MNG_AttackScenario(
                source.Index,
                source.Seed,
                source.Kind,
                source.CarrierSlot,
                source.BallPosition,
                red,
                navy);
        }

        void Apply(MNG_AttackScenario scenario)
        {
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                SetPlayerPose(matchController.GetPlayerAvatar(Team.Red, slot), scenario.RedPositions[slot], Vector2.right);
                SetPlayerPose(matchController.GetPlayerAvatar(Team.Navy, slot), scenario.NavyPositions[slot], Vector2.left);
            }
            SetBall(scenario.BallPosition);
        }

        void Apply(MNG_DefenseScenario scenario)
        {
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                SetPlayerPose(matchController.GetPlayerAvatar(Team.Red, slot), scenario.RedPositions[slot], Vector2.right);
                SetPlayerPose(matchController.GetPlayerAvatar(Team.Navy, slot), scenario.NavyPositions[slot], Vector2.left);
            }
            SetBall(scenario.BallPosition);
        }

        void SetBall(Vector2 position)
        {
            var body = ballControl.GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.position = new Vector3(position.x, body.position.y, position.y);
            body.rotation = Quaternion.identity;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            ballControl.ResetLedger();
        }

        void OnGoalScored(Team scoringTeam, long _)
        {
            if (m_GoalPending) return;
            m_ScoringTeam = scoringTeam;
            m_GoalPending = true;
        }

        void CompleteEpisode(string outcome, bool alreadyInterrupted)
        {
            var kind = CurrentGroup == MNG_MS1SituationGroup.Attack
                ? CurrentAttackScenario.Kind.ToString()
                : CurrentDefenseScenario.Kind.ToString();
            var result = new MNG_MS1EpisodeResult(
                m_EpisodeIndex,
                CurrentGroup,
                kind,
                outcome,
                matchController.EpisodeElapsedSeconds,
                m_Recovered);
            AppendEvidence("episode-end", $"\"episode\":{m_EpisodeIndex},"
                + $"\"group\":\"{CurrentGroup}\",\"kind\":\"{kind}\","
                + $"\"outcome\":\"{outcome}\","
                + $"\"elapsed\":{matchController.EpisodeElapsedSeconds:R},"
                + $"\"recovered\":{m_Recovered.ToString().ToLowerInvariant()},"
                + $"\"commands\":{FormatCommands()}");
            EpisodeCompleted?.Invoke(result);
            if (!alreadyInterrupted) matchController.EndActivePolicyEpisodes();
            BeginEpisode(m_EpisodeIndex + 1);
        }

        void OnCommandAccepted(Team team, MNG_Command command, long _)
        {
            if (team == Team.Red) m_CommandCounts[(int)command]++;
        }

        void InitializeEvidence(string[] arguments)
        {
            var directory = ResolveArgument(arguments, "-mngEvidenceDir");
            if (string.IsNullOrWhiteSpace(directory)) return;
            directory = Path.GetFullPath(directory);
            Directory.CreateDirectory(directory);
            m_EvidencePath = Path.Combine(
                directory,
                $"worker-{m_WorkerIdentity}-process-{m_ProcessId}.jsonl");
            if (File.Exists(m_EvidencePath))
                throw new InvalidOperationException($"MS1 worker evidence already exists: {m_EvidencePath}");
            AppendEvidence("worker-start", $"\"profile\":\"{opponentProfile.Strength}\","
                + $"\"passRepairMode\":{m_PassRepairMode.ToString().ToLowerInvariant()},"
                + $"\"episodeSeconds\":{EpisodeSeconds:R},\"timeScale\":{timeScale:R},"
                + $"\"spawnSeedOffset\":{matchController.SpawnSeedOffset}");
        }

        void AppendEvidence(string eventName, string payload)
        {
            if (string.IsNullOrEmpty(m_EvidencePath)) return;
            File.AppendAllText(m_EvidencePath, "{"
                + $"\"event\":\"{eventName}\",\"utc\":\"{DateTime.UtcNow:O}\","
                + $"\"worker\":{m_WorkerIdentity},\"process\":{m_ProcessId},"
                + payload + "}\n");
        }

        string FormatCommands()
            => $"[{m_CommandCounts[0]},{m_CommandCounts[1]},{m_CommandCounts[2]},"
                + $"{m_CommandCounts[3]},{m_CommandCounts[4]},{m_CommandCounts[5]}]";

        static string ResolveArgument(string[] arguments, string name)
        {
            if (arguments == null) return string.Empty;
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            return string.Empty;
        }

        static void SetPlayerPose(MNG_PlayerAvatar avatar, Vector2 position, Vector2 forward)
        {
            var world = new Vector3(position.x, avatar.Body.position.y, position.y);
            var rotation = Quaternion.LookRotation(new Vector3(forward.x, 0f, forward.y), Vector3.up);
            avatar.transform.SetPositionAndRotation(world, rotation);
            avatar.Body.position = world;
            avatar.Body.rotation = rotation;
            avatar.Body.linearVelocity = Vector3.zero;
            avatar.Body.angularVelocity = Vector3.zero;
            avatar.ResetKickCooldown();
            avatar.GetComponent<MNG_PlayerMotor>().ResetDriveCommand();
            avatar.GetComponent<MNG_PlayerSkillExecutor>().ResetForRound();
        }
    }
}
