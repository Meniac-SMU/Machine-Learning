using System;
using System.IO;
using System.Linq;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public enum MNG_MSMode
    {
        R0Opponent = 0,
        SelfPlay = 1
    }

    [DefaultExecutionOrder(-450)]
    [DisallowMultipleComponent]
    public sealed class MNG_MSController : MonoBehaviour
    {
        public const float DefaultTimeScale = 10f;
        public const float SmokeEpisodeSeconds = 20f;

        [SerializeField] MNG_MSMode mode;
        [SerializeField] MNG_MSOpponentProfile opponentProfile;
        [SerializeField] MNG_MatchController matchController;
        [SerializeField, Min(1f)] float episodeSeconds = SmokeEpisodeSeconds;
        [SerializeField, Min(1f)] float timeScale = DefaultTimeScale;

        readonly long[,] m_CommandCounts =
            new long[MNG_MatchSnapshot.TeamCount, MNG_CommandMask.CommandCount];
        readonly long[,] m_PreviousPolicyRawCommands =
            new long[MNG_MatchSnapshot.TeamCount, MNG_CommandMask.CommandCount];
        readonly long[,] m_PreviousPolicyEffectiveCommands =
            new long[MNG_MatchSnapshot.TeamCount, MNG_CommandMask.CommandCount];
        readonly MNG_ManagerAgent[] m_Policies =
            new MNG_ManagerAgent[MNG_MatchSnapshot.TeamCount];
        readonly long[] m_PreviousPolicyOverrides =
            new long[MNG_MatchSnapshot.TeamCount];
        readonly long[] m_PreviousExplicitBlockedPassRewards =
            new long[MNG_MatchSnapshot.TeamCount];
        int m_LastCompletedEpisode = -1;
        int m_WorkerIdentity;
        int m_ProcessId;
        string m_EvidenceFilePath;

        public MNG_MSMode Mode => mode;
        public MNG_MSOpponentProfile OpponentProfile => opponentProfile;
        public int WorkerIdentity => m_WorkerIdentity;
        public float EpisodeSeconds => episodeSeconds;
        public float TimeScale => timeScale;

        void Awake()
        {
            if (matchController == null) matchController = GetComponent<MNG_MatchController>();
            if (matchController == null)
                throw new InvalidOperationException("MS requires an MNG match controller.");
            if (!MNG_MatchSnapshot.IsFinite(episodeSeconds) || episodeSeconds < 1f)
                throw new InvalidOperationException("MS episode seconds must be finite and positive.");
            if (!MNG_MatchSnapshot.IsFinite(timeScale) || timeScale < 1f)
                throw new InvalidOperationException("MS time scale must be finite and at least one.");
            if (mode == MNG_MSMode.R0Opponent)
            {
                if (opponentProfile == null)
                    throw new InvalidOperationException("MS R0 mode requires an opponent profile.");
                opponentProfile.ValidateOrThrow();
            }

            m_WorkerIdentity = ResolveWorkerIdentity(Environment.GetCommandLineArgs());
            m_ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
            matchController.ConfigureSpawnSeedOffset(m_WorkerIdentity);
            matchController.ConfigureMatchDuration(
                episodeSeconds,
                true,
                mode == MNG_MSMode.SelfPlay
                    ? MNG_MatchFinishMode.SelfPlayTerminalResult
                    : MNG_MatchFinishMode.TerminalResult);
            Time.timeScale = timeScale;
        }

        void OnEnable()
        {
            if (matchController != null) matchController.CommandAccepted += OnCommandAccepted;
        }

        void OnDisable()
        {
            if (matchController != null) matchController.CommandAccepted -= OnCommandAccepted;
        }

        void Start()
        {
            foreach (var human in GetComponentsInChildren<MNG_HumanInput>(true))
                human.enabled = false;
            foreach (var fallback in GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = false;

            var activeManagers = GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Where(manager => manager.isActiveAndEnabled)
                .ToArray();
            var activeRules = GetComponentsInChildren<MNG_RuleBasedManager>(true)
                .Where(manager => manager.isActiveAndEnabled)
                .ToArray();
            foreach (var manager in activeManagers)
            {
                manager.ConfigurePolicyAssistMode(MNG_PolicyAssistMode.None);
                m_Policies[(int)manager.Team] = manager;
            }
            if (mode == MNG_MSMode.R0Opponent)
            {
                if (activeManagers.Length != 1 || activeManagers[0].Team != Team.Red)
                    throw new InvalidOperationException(
                        $"MS R0 mode requires one active Red policy, found {activeManagers.Length}.");
                if (activeRules.Length != 1 || activeRules[0].Team != Team.Navy)
                    throw new InvalidOperationException(
                        $"MS R0 mode requires one active Navy rule manager, found {activeRules.Length}.");
                activeRules[0].ConfigureDecisionInterval(opponentProfile.DecisionIntervalSeconds);
                foreach (var avatar in GetComponentsInChildren<MNG_PlayerAvatar>(true))
                {
                    avatar.GetComponent<MNG_PlayerMotor>().ConfigureSpeedMultiplier(
                        avatar.Team == Team.Navy
                            ? opponentProfile.MovementSpeedMultiplier
                            : 1f);
                }
            }
            else
            {
                if (activeManagers.Length != 2
                    || activeManagers.Select(manager => manager.Team).Distinct().Count() != 2)
                    throw new InvalidOperationException(
                        "MS self-play requires one active policy manager per team.");
                if (activeRules.Length != 0)
                    throw new InvalidOperationException("MS self-play cannot run a rule manager.");
                foreach (var motor in GetComponentsInChildren<MNG_PlayerMotor>(true))
                    motor.ConfigureSpeedMultiplier(1f);
            }

            matchController.ResetMatch();
            InitializeEvidence(Environment.GetCommandLineArgs(), activeManagers.Length, activeRules.Length);
            Debug.Log(
                $"MNG MS BOOTSTRAP PASS mode={mode} worker={m_WorkerIdentity} "
                + $"process={m_ProcessId} policies={activeManagers.Length} "
                + $"rules={activeRules.Length} episodeSeconds={episodeSeconds:R} "
                + $"timeScale={timeScale:R} opponent="
                + $"{(opponentProfile != null ? opponentProfile.Strength.ToString() : "Policy")}");
        }

        void FixedUpdate()
        {
            if (matchController.State != MNG_MatchState.Finished) return;
            var episode = (int)matchController.Snapshot.EpisodeId;
            if (episode == m_LastCompletedEpisode) return;
            m_LastCompletedEpisode = episode;
            var message =
                $"MNG MS EPISODE END mode={mode} worker={m_WorkerIdentity} episode={episode} "
                + $"score={matchController.RedScore}:{matchController.NavyScore} "
                + $"redCommands={FormatCommands(Team.Red)} navyCommands={FormatCommands(Team.Navy)}";
            Debug.Log(message);
            var actionEvidence = CapturePolicyActionEvidence(out var actionIntegrityPassed);
            AppendEvidence(
                "episode",
                $"\"episode\":{episode},\"redScore\":{matchController.RedScore},"
                + $"\"navyScore\":{matchController.NavyScore},"
                + $"\"redCommands\":{FormatCommands(Team.Red)},"
                + $"\"navyCommands\":{FormatCommands(Team.Navy)}"
                + actionEvidence);
            if (!actionIntegrityPassed)
                throw new InvalidOperationException(
                    "MS P0 action integrity failed: raw/effective mismatch, policy override, "
                    + "or direct blocked-pass decision reward was observed.");
        }

        public void Configure(
            MNG_MSMode configuredMode,
            MNG_MSOpponentProfile configuredProfile,
            MNG_MatchController configuredMatch,
            float configuredEpisodeSeconds = SmokeEpisodeSeconds,
            float configuredTimeScale = DefaultTimeScale)
        {
            mode = configuredMode;
            opponentProfile = configuredProfile;
            matchController = configuredMatch ?? throw new ArgumentNullException(nameof(configuredMatch));
            episodeSeconds = configuredEpisodeSeconds;
            timeScale = configuredTimeScale;
        }

        public static int ResolveWorkerIdentity(string[] arguments)
        {
            if (arguments == null) return 0;
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (!string.Equals(arguments[index], "--mlagents-port", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(arguments[index], "-mlagents-port", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (int.TryParse(arguments[index + 1], out var port) && port > 0) return port;
            }
            return 0;
        }

        static string ResolveArgument(string[] arguments, string name)
        {
            if (arguments == null) return string.Empty;
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            }
            return string.Empty;
        }

        void InitializeEvidence(string[] arguments, int policyCount, int ruleCount)
        {
            var directory = ResolveArgument(arguments, "-mngEvidenceDir");
            if (string.IsNullOrWhiteSpace(directory)) return;
            directory = Path.GetFullPath(directory);
            Directory.CreateDirectory(directory);
            m_EvidenceFilePath = Path.Combine(
                directory,
                $"worker-{m_WorkerIdentity}-process-{m_ProcessId}.jsonl");
            if (File.Exists(m_EvidenceFilePath))
                throw new InvalidOperationException(
                    $"MS worker evidence already exists: {m_EvidenceFilePath}");
            AppendEvidence(
                "worker-start",
                $"\"policyCount\":{policyCount},\"ruleCount\":{ruleCount},"
                + $"\"episodeSeconds\":{episodeSeconds:R},\"timeScale\":{timeScale:R},"
                + $"\"spawnSeedOffset\":{matchController.SpawnSeedOffset}");
        }

        void AppendEvidence(string eventName, string payload)
        {
            if (string.IsNullOrEmpty(m_EvidenceFilePath)) return;
            var line = "{"
                + $"\"event\":\"{eventName}\","
                + $"\"utc\":\"{DateTime.UtcNow:O}\","
                + $"\"mode\":\"{mode}\","
                + $"\"worker\":{m_WorkerIdentity},"
                + $"\"process\":{m_ProcessId},"
                + payload
                + "}\n";
            File.AppendAllText(m_EvidenceFilePath, line);
        }

        void OnCommandAccepted(Team team, MNG_Command command, long _)
            => m_CommandCounts[(int)team, (int)command]++;

        string CapturePolicyActionEvidence(out bool passed)
        {
            if (mode == MNG_MSMode.R0Opponent)
            {
                var evidence = CaptureTeamPolicyActionEvidence(Team.Red, "policy", out passed);
                return evidence
                    + $",\"actionIntegrityPassed\":{passed.ToString().ToLowerInvariant()}";
            }

            var red = CaptureTeamPolicyActionEvidence(Team.Red, "redPolicy", out var redPassed);
            var navy = CaptureTeamPolicyActionEvidence(Team.Navy, "navyPolicy", out var navyPassed);
            passed = redPassed && navyPassed;
            return red + navy
                + $",\"actionIntegrityPassed\":{passed.ToString().ToLowerInvariant()}";
        }

        string CaptureTeamPolicyActionEvidence(Team team, string prefix, out bool passed)
        {
            var teamIndex = (int)team;
            var policy = m_Policies[teamIndex];
            if (policy == null)
            {
                passed = false;
                return $",\"{prefix}Missing\":true";
            }
            var raw = new long[MNG_CommandMask.CommandCount];
            var effective = new long[MNG_CommandMask.CommandCount];
            policy.CopyRawCommandCounts(raw);
            policy.CopyCommandCounts(effective);
            for (var index = 0; index < raw.Length; index++)
            {
                var currentRaw = raw[index];
                var currentEffective = effective[index];
                raw[index] -= m_PreviousPolicyRawCommands[teamIndex, index];
                effective[index] -= m_PreviousPolicyEffectiveCommands[teamIndex, index];
                m_PreviousPolicyRawCommands[teamIndex, index] = currentRaw;
                m_PreviousPolicyEffectiveCommands[teamIndex, index] = currentEffective;
            }
            var overrides = policy.BlockedForwardPassOverrideCount
                - m_PreviousPolicyOverrides[teamIndex];
            m_PreviousPolicyOverrides[teamIndex] = policy.BlockedForwardPassOverrideCount;
            var explicitRewards = policy.ExplicitBlockedPassRewardCount
                - m_PreviousExplicitBlockedPassRewards[teamIndex];
            m_PreviousExplicitBlockedPassRewards[teamIndex] =
                policy.ExplicitBlockedPassRewardCount;
            passed = policy.PolicyAssistMode == MNG_PolicyAssistMode.None
                && raw.SequenceEqual(effective)
                && overrides == 0
                && explicitRewards == 0;
            return $",\"{prefix}AssistMode\":\"{policy.PolicyAssistMode}\"," 
                + $"\"{prefix}RawCommands\":{FormatCommands(raw)},"
                + $"\"{prefix}EffectiveCommands\":{FormatCommands(effective)},"
                + $"\"{prefix}BlockedPassOverrides\":{overrides},"
                + $"\"{prefix}ExplicitBlockedPassRewards\":{explicitRewards},"
                + $"\"{prefix}IntegrityPassed\":{passed.ToString().ToLowerInvariant()}";
        }

        string FormatCommands(Team team)
        {
            var index = (int)team;
            return $"[{m_CommandCounts[index, 0]},{m_CommandCounts[index, 1]},"
                + $"{m_CommandCounts[index, 2]},{m_CommandCounts[index, 3]},"
                + $"{m_CommandCounts[index, 4]},{m_CommandCounts[index, 5]}]";
        }

        static string FormatCommands(long[] counts)
            => $"[{counts[0]},{counts[1]},{counts[2]},"
                + $"{counts[3]},{counts[4]},{counts[5]}]";
    }
}
