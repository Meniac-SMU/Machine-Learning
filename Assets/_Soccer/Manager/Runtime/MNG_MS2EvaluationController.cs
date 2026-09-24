using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MachineLearning.Soccer;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class MNG_MS2EvaluationController : MonoBehaviour
    {
        public const string Protocol = "MNG-MS2-PREFLIGHT-v1";
        public const int EvaluationMatches = 10;
        public const int EvaluationSeedOffset = 491001;
        public const float EvaluationMatchSeconds = 120f;
        public const float EvaluationTimeScale = 10f;

        [SerializeField] MNG_MatchController matchController;
        [SerializeField] MNG_MSOpponentProfile mediumProfile;
        [SerializeField] MNG_MSOpponentProfile fullProfile;
        [SerializeField] string runId;
        [SerializeField] string candidateId;
        [SerializeField] string modelSha256;

        readonly List<MNG_MS2EvaluationMatch> m_Matches = new();
        readonly long[] m_PreviousRawCommands = new long[MNG_CommandMask.CommandCount];
        readonly long[] m_PreviousEffectiveCommands = new long[MNG_CommandMask.CommandCount];
        MNG_ManagerAgent m_Manager;
        MNG_TacticalRewardTracker m_Rewards;
        MNG_MSOpponentProfile m_Profile;
        Team m_PolicyTeam;
        bool m_RandomPolicy;
        bool m_Completed;
        string m_Protocol;
        int m_EvaluationMatches;
        int m_SeedOffset;
        float m_MatchSeconds;
        int m_LastRecordedEpisode = -1;
        int m_PreviousPassStrikes;
        int m_PreviousCompletedPasses;
        int m_PreviousShotStrikes;
        int m_PreviousValidShots;
        int m_PreviousAdvances;
        long m_PreviousOverrides;
        long m_PreviousExplicitBlockedPassRewards;

        public void Configure(
            MNG_MatchController configuredMatch,
            MNG_MSOpponentProfile configuredMedium,
            MNG_MSOpponentProfile configuredFull,
            string configuredRunId,
            string configuredCandidateId,
            string configuredModelSha256)
        {
            matchController = configuredMatch ?? throw new ArgumentNullException(nameof(configuredMatch));
            mediumProfile = configuredMedium ?? throw new ArgumentNullException(nameof(configuredMedium));
            fullProfile = configuredFull ?? throw new ArgumentNullException(nameof(configuredFull));
            runId = RequireText(configuredRunId, nameof(configuredRunId));
            candidateId = RequireText(configuredCandidateId, nameof(configuredCandidateId));
            modelSha256 = RequireText(configuredModelSha256, nameof(configuredModelSha256)).ToLowerInvariant();
        }

        void Awake()
        {
            matchController ??= GetComponent<MNG_MatchController>();
            m_Rewards = GetComponent<MNG_TacticalRewardTracker>();
            if (matchController == null || m_Rewards == null || mediumProfile == null
                || fullProfile == null)
                throw new InvalidOperationException("MS2 preflight evaluation bindings are incomplete.");

            var arguments = Environment.GetCommandLineArgs();
            m_PolicyTeam = ParseTeam(ReadArgument(arguments, "-mngPolicyTeam", "Red"));
            var strength = ReadArgument(arguments, "-mngOpponentStrength", "Full");
            m_Profile = string.Equals(strength, "Medium", StringComparison.OrdinalIgnoreCase)
                ? mediumProfile
                : string.Equals(strength, "Full", StringComparison.OrdinalIgnoreCase)
                    ? fullProfile
                    : throw new InvalidOperationException($"Unsupported MS2 opponent strength: {strength}");
            m_Profile.ValidateOrThrow();
            m_RandomPolicy = string.Equals(
                ReadArgument(arguments, "-mngRandomPolicy", "false"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            m_Protocol = ReadArgument(arguments, "-mngEvaluationProtocol", Protocol);
            m_EvaluationMatches = ReadPositiveInt(
                arguments, "-mngEvaluationMatches", EvaluationMatches);
            m_SeedOffset = ReadPositiveInt(
                arguments, "-mngEvaluationSeedOffset", EvaluationSeedOffset);
            m_MatchSeconds = ReadPositiveFloat(
                arguments, "-mngEvaluationMatchSeconds", EvaluationMatchSeconds);

            ConfigureControlEndpoints();
            matchController.ConfigureSpawnSeedOffset(m_SeedOffset);
            matchController.ConfigureMatchDuration(
                m_MatchSeconds,
                true,
                MNG_MatchFinishMode.TerminalResult);
            Time.timeScale = EvaluationTimeScale;
        }

        void Start()
        {
            matchController.ResetMatch();
            Debug.Log(
                $"MNG MS2 PREFLIGHT BOOTSTRAP policy={(m_RandomPolicy ? "uniform-valid-command" : "onnx")} "
                + $"team={m_PolicyTeam} opponent={m_Profile.Strength} "
                + $"matches={m_EvaluationMatches} seconds={m_MatchSeconds:R} "
                + $"seedOffset={m_SeedOffset} protocol={m_Protocol}");
        }

        void FixedUpdate()
        {
            if (m_Completed || matchController.State != MNG_MatchState.Finished) return;
            var episode = (int)matchController.Snapshot.EpisodeId;
            if (episode == m_LastRecordedEpisode) return;
            m_LastRecordedEpisode = episode;
            RecordMatch();
            if (m_Matches.Count < m_EvaluationMatches) return;
            WriteResultAndQuit();
        }

        void ConfigureControlEndpoints()
        {
            var opponentTeam = Opponent(m_PolicyTeam);
            foreach (var fallback in GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = false;
            foreach (var human in GetComponentsInChildren<MNG_HumanInput>(true))
                human.enabled = false;

            m_Manager = GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Single(manager => manager.Team == m_PolicyTeam);
            m_Manager.ConfigurePolicyAssistMode(MNG_PolicyAssistMode.None);
            foreach (var manager in GetComponentsInChildren<MNG_ManagerAgent>(true))
            {
                var active = manager == m_Manager;
                var behavior = manager.GetComponent<BehaviorParameters>();
                behavior.BehaviorType = active && m_RandomPolicy
                    ? BehaviorType.HeuristicOnly
                    : BehaviorType.InferenceOnly;
                if (active && m_RandomPolicy)
                    manager.ConfigureUniformRandomHeuristic(
                        m_SeedOffset + (m_PolicyTeam == Team.Red ? 0 : 100));
                manager.enabled = active;
                manager.GetComponent<Unity.MLAgents.DecisionRequester>().enabled = active;
                manager.gameObject.SetActive(active);
            }

            var activeRuleCount = 0;
            foreach (var rule in GetComponentsInChildren<MNG_RuleBasedManager>(true))
            {
                rule.enabled = rule.Team == opponentTeam;
                if (!rule.enabled) continue;
                rule.ConfigureDecisionInterval(m_Profile.DecisionIntervalSeconds);
                activeRuleCount++;
            }
            if (activeRuleCount != 1)
                throw new InvalidOperationException("MS2 preflight requires exactly one active R0 manager.");

            foreach (var avatar in GetComponentsInChildren<MNG_PlayerAvatar>(true))
            {
                avatar.GetComponent<MNG_PlayerMotor>().ConfigureSpeedMultiplier(
                    avatar.Team == opponentTeam ? m_Profile.MovementSpeedMultiplier : 1f);
            }
        }

        void RecordMatch()
        {
            var raw = new long[MNG_CommandMask.CommandCount];
            var effective = new long[MNG_CommandMask.CommandCount];
            m_Manager.CopyRawCommandCounts(raw);
            m_Manager.CopyCommandCounts(effective);
            var episodeRaw = Subtract(raw, m_PreviousRawCommands);
            var episodeEffective = Subtract(effective, m_PreviousEffectiveCommands);
            Array.Copy(raw, m_PreviousRawCommands, raw.Length);
            Array.Copy(effective, m_PreviousEffectiveCommands, effective.Length);

            var policyGoals = m_PolicyTeam == Team.Red
                ? matchController.RedScore : matchController.NavyScore;
            var opponentGoals = m_PolicyTeam == Team.Red
                ? matchController.NavyScore : matchController.RedScore;
            var score = policyGoals > opponentGoals ? 1f : policyGoals == opponentGoals ? 0.5f : 0f;
            var outcome = score > 0.5f ? "win" : score < 0.5f ? "loss" : "draw";
            var passStrikes = Delta(m_Rewards.GetPassStrikeCount(m_PolicyTeam), ref m_PreviousPassStrikes);
            var completedPasses = Delta(
                m_Rewards.GetCompletedPassCount(m_PolicyTeam), ref m_PreviousCompletedPasses);
            var shotStrikes = Delta(m_Rewards.GetShotStrikeCount(m_PolicyTeam), ref m_PreviousShotStrikes);
            var validShots = Delta(m_Rewards.GetValidShotCount(m_PolicyTeam), ref m_PreviousValidShots);
            var advances = Delta(m_Rewards.GetAdvanceRewardCount(m_PolicyTeam), ref m_PreviousAdvances);
            var overrides = m_Manager.BlockedForwardPassOverrideCount - m_PreviousOverrides;
            m_PreviousOverrides = m_Manager.BlockedForwardPassOverrideCount;
            var explicitBlockedPassRewards = m_Manager.ExplicitBlockedPassRewardCount
                - m_PreviousExplicitBlockedPassRewards;
            m_PreviousExplicitBlockedPassRewards = m_Manager.ExplicitBlockedPassRewardCount;

            m_Matches.Add(new MNG_MS2EvaluationMatch
            {
                matchIndex = m_Matches.Count,
                spawnSeedOffset = m_SeedOffset,
                policyTeam = m_PolicyTeam.ToString(),
                policyGoals = policyGoals,
                opponentGoals = opponentGoals,
                outcome = outcome,
                scoreValue = score,
                rawCommandCounts = episodeRaw,
                effectiveCommandCounts = episodeEffective,
                blockedPassOverrides = overrides,
                explicitBlockedPassRewards = explicitBlockedPassRewards,
                passStrikes = passStrikes,
                completedPasses = completedPasses,
                shotStrikes = shotStrikes,
                validShots = validShots,
                fiveMeterAdvances = advances
            });
            Debug.Log(
                $"MNG MS2 PREFLIGHT MATCH index={m_Matches.Count}/{m_EvaluationMatches} "
                + $"team={m_PolicyTeam} opponent={m_Profile.Strength} "
                + $"score={policyGoals}:{opponentGoals} outcome={outcome}");
        }

        void WriteResultAndQuit()
        {
            m_Completed = true;
            var output = ReadArgument(
                Environment.GetCommandLineArgs(),
                "-mngEvaluationOutput",
                string.Empty);
            if (string.IsNullOrWhiteSpace(output))
                throw new InvalidOperationException("MS2 preflight output path is missing.");

            var raw = Sum(item => item.rawCommandCounts);
            var effective = Sum(item => item.effectiveCommandCounts);
            var result = new MNG_MS2EvaluationResult
            {
                protocol = m_Protocol,
                runId = runId,
                candidateId = candidateId,
                modelSha256 = modelSha256,
                policyKind = m_RandomPolicy ? "uniform-valid-command" : "onnx",
                opponentStrength = m_Profile.Strength.ToString(),
                policyTeam = m_PolicyTeam.ToString(),
                seedOffset = m_SeedOffset,
                matchSeconds = m_MatchSeconds,
                matches = m_Matches.Count,
                wins = m_Matches.Count(item => item.outcome == "win"),
                draws = m_Matches.Count(item => item.outcome == "draw"),
                losses = m_Matches.Count(item => item.outcome == "loss"),
                goalsFor = m_Matches.Sum(item => item.policyGoals),
                goalsAgainst = m_Matches.Sum(item => item.opponentGoals),
                scoreRate = m_Matches.Average(item => item.scoreValue),
                rawCommandCounts = raw,
                effectiveCommandCounts = effective,
                blockedPassOverrides = m_Matches.Sum(item => item.blockedPassOverrides),
                explicitBlockedPassRewards = m_Matches.Sum(
                    item => item.explicitBlockedPassRewards),
                passStrikes = m_Matches.Sum(item => item.passStrikes),
                completedPasses = m_Matches.Sum(item => item.completedPasses),
                shotStrikes = m_Matches.Sum(item => item.shotStrikes),
                validShots = m_Matches.Sum(item => item.validShots),
                fiveMeterAdvances = m_Matches.Sum(item => item.fiveMeterAdvances),
                matchResults = m_Matches.ToArray()
            };
            output = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(output)
                ?? throw new InvalidOperationException("MS2 preflight output directory is invalid."));
            File.WriteAllText(output, JsonUtility.ToJson(result, true) + Environment.NewLine);
            Debug.Log(
                $"MNG MS2 PREFLIGHT COMPLETE policy={result.policyKind} team={result.policyTeam} "
                + $"opponent={result.opponentStrength} scoreRate={result.scoreRate:R} "
                + $"goals={result.goalsFor}:{result.goalsAgainst} output={output}");
            Application.Quit(0);
        }

        long[] Sum(Func<MNG_MS2EvaluationMatch, long[]> selector)
        {
            var sum = new long[MNG_CommandMask.CommandCount];
            foreach (var match in m_Matches)
            {
                var values = selector(match);
                for (var index = 0; index < sum.Length; index++) sum[index] += values[index];
            }
            return sum;
        }

        static long[] Subtract(long[] current, long[] previous)
        {
            var result = new long[current.Length];
            for (var index = 0; index < current.Length; index++)
                result[index] = current[index] - previous[index];
            return result;
        }

        static int Delta(int current, ref int previous)
        {
            var result = current - previous;
            previous = current;
            return result;
        }

        static Team ParseTeam(string value)
        {
            if (Enum.TryParse(value, true, out Team team)) return team;
            throw new InvalidOperationException($"Unsupported MS2 policy team: {value}");
        }

        static Team Opponent(Team team) => team == Team.Red ? Team.Navy : Team.Red;

        static string ReadArgument(string[] arguments, string name, string fallback)
        {
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            return fallback;
        }

        static int ReadPositiveInt(string[] arguments, string name, int fallback)
        {
            var value = ReadArgument(arguments, name, fallback.ToString());
            if (int.TryParse(value, out var parsed) && parsed > 0) return parsed;
            throw new InvalidOperationException($"{name} must be a positive integer: {value}");
        }

        static float ReadPositiveFloat(string[] arguments, string name, float fallback)
        {
            var value = ReadArgument(arguments, name, fallback.ToString("R"));
            if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                && MNG_MatchSnapshot.IsFinite(parsed) && parsed > 0f)
                return parsed;
            throw new InvalidOperationException($"{name} must be a positive finite number: {value}");
        }

        static string RequireText(string value, string parameter)
            => string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("MS2 evaluation identity is required.", parameter)
                : value;
    }
}
