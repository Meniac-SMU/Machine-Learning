using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MachineLearning.Soccer;
using Unity.InferenceEngine;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class MNG_MS3CheckpointDuelController : MonoBehaviour
    {
        public const string Protocol = "MNG-MS3-CHECKPOINT-DUEL-v1";
        public const int EvaluationMatches = 20;
        public const int EvaluationSeedOffset = 493001;
        public const float EvaluationMatchSeconds = 300f;
        public const float EvaluationTimeScale = 10f;

        [SerializeField] MNG_MatchController matchController;
        [SerializeField] ModelAsset earlierModel;
        [SerializeField] ModelAsset finalModel;
        [SerializeField] string duelId;
        [SerializeField] string earlierCandidateId;
        [SerializeField] string earlierModelSha256;
        [SerializeField] string finalCandidateId;
        [SerializeField] string finalModelSha256;

        readonly List<MNG_MS3CheckpointDuelMatch> m_Matches = new();
        readonly MNG_ManagerAgent[] m_Managers =
            new MNG_ManagerAgent[MNG_MatchSnapshot.TeamCount];
        readonly long[,] m_PreviousRawCommands =
            new long[MNG_MatchSnapshot.TeamCount, MNG_CommandMask.CommandCount];
        readonly long[,] m_PreviousEffectiveCommands =
            new long[MNG_MatchSnapshot.TeamCount, MNG_CommandMask.CommandCount];
        readonly long[] m_PreviousOverrides = new long[MNG_MatchSnapshot.TeamCount];
        readonly long[] m_PreviousExplicitRewards = new long[MNG_MatchSnapshot.TeamCount];
        readonly int[] m_PreviousPassStrikes = new int[MNG_MatchSnapshot.TeamCount];
        readonly int[] m_PreviousCompletedPasses = new int[MNG_MatchSnapshot.TeamCount];
        readonly int[] m_PreviousShotStrikes = new int[MNG_MatchSnapshot.TeamCount];
        readonly int[] m_PreviousValidShots = new int[MNG_MatchSnapshot.TeamCount];
        readonly int[] m_PreviousAdvances = new int[MNG_MatchSnapshot.TeamCount];

        MNG_TacticalRewardTracker m_Rewards;
        Team m_EarlierTeam;
        int m_MatchCount;
        int m_SeedOffset;
        float m_MatchSeconds;
        string m_Protocol;
        string m_OutputPath;
        int m_LastRecordedEpisode = -1;
        bool m_Completed;

        public ModelAsset EarlierModel => earlierModel;
        public ModelAsset FinalModel => finalModel;
        public string DuelId => duelId;

        public void Configure(
            MNG_MatchController configuredMatch,
            ModelAsset configuredEarlierModel,
            ModelAsset configuredFinalModel,
            string configuredDuelId,
            string configuredEarlierCandidateId,
            string configuredEarlierModelSha256,
            string configuredFinalCandidateId,
            string configuredFinalModelSha256)
        {
            matchController = configuredMatch ?? throw new ArgumentNullException(nameof(configuredMatch));
            earlierModel = configuredEarlierModel
                ?? throw new ArgumentNullException(nameof(configuredEarlierModel));
            finalModel = configuredFinalModel
                ?? throw new ArgumentNullException(nameof(configuredFinalModel));
            duelId = RequireText(configuredDuelId, nameof(configuredDuelId));
            earlierCandidateId = RequireText(
                configuredEarlierCandidateId, nameof(configuredEarlierCandidateId));
            earlierModelSha256 = RequireText(
                configuredEarlierModelSha256, nameof(configuredEarlierModelSha256)).ToLowerInvariant();
            finalCandidateId = RequireText(
                configuredFinalCandidateId, nameof(configuredFinalCandidateId));
            finalModelSha256 = RequireText(
                configuredFinalModelSha256, nameof(configuredFinalModelSha256)).ToLowerInvariant();
        }

        void Awake()
        {
            matchController ??= GetComponent<MNG_MatchController>();
            m_Rewards = GetComponent<MNG_TacticalRewardTracker>();
            if (matchController == null || m_Rewards == null
                || earlierModel == null || finalModel == null)
                throw new InvalidOperationException(
                    "MS3 checkpoint duel bindings are incomplete.");

            var arguments = Environment.GetCommandLineArgs();
            m_EarlierTeam = ParseTeam(ReadArgument(arguments, "-mngEarlierTeam", "Red"));
            m_MatchCount = ReadPositiveInt(
                arguments, "-mngEvaluationMatches", EvaluationMatches);
            m_SeedOffset = ReadPositiveInt(
                arguments, "-mngEvaluationSeedOffset", EvaluationSeedOffset);
            m_MatchSeconds = ReadPositiveFloat(
                arguments, "-mngEvaluationMatchSeconds", EvaluationMatchSeconds);
            m_Protocol = ReadArgument(arguments, "-mngEvaluationProtocol", Protocol);
            m_OutputPath = ReadArgument(arguments, "-mngEvaluationOutput", string.Empty);
            if (string.IsNullOrWhiteSpace(m_OutputPath))
                throw new InvalidOperationException(
                    "MS3 checkpoint duel output path is missing.");

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
                $"MNG MS3 CHECKPOINT DUEL BOOTSTRAP duel={duelId} "
                + $"earlier={earlierCandidateId} final={finalCandidateId} "
                + $"earlierTeam={m_EarlierTeam} matches={m_MatchCount} "
                + $"seconds={m_MatchSeconds:R} seedOffset={m_SeedOffset} "
                + $"protocol={m_Protocol}");
        }

        void FixedUpdate()
        {
            if (m_Completed || matchController.State != MNG_MatchState.Finished) return;
            var episode = (int)matchController.Snapshot.EpisodeId;
            if (episode == m_LastRecordedEpisode) return;
            m_LastRecordedEpisode = episode;
            var match = RecordMatch();
            if (!match.actionIntegrityPassed)
                throw new InvalidOperationException(
                    "MS3 checkpoint duel action integrity failed.");
            if (m_Matches.Count >= m_MatchCount) WriteResultAndQuit();
        }

        void ConfigureControlEndpoints()
        {
            foreach (var fallback in GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = false;
            foreach (var rule in GetComponentsInChildren<MNG_RuleBasedManager>(true))
                rule.enabled = false;
            foreach (var human in GetComponentsInChildren<MNG_HumanInput>(true))
                human.enabled = false;

            var managers = GetComponentsInChildren<MNG_ManagerAgent>(true);
            if (managers.Length != MNG_MatchSnapshot.TeamCount
                || managers.Select(manager => manager.Team).Distinct().Count()
                    != MNG_MatchSnapshot.TeamCount)
                throw new InvalidOperationException(
                    "MS3 checkpoint duel requires one PPO manager per team.");
            foreach (var manager in managers)
            {
                var teamIndex = (int)manager.Team;
                m_Managers[teamIndex] = manager;
                manager.gameObject.SetActive(true);
                manager.enabled = true;
                manager.ConfigurePolicyAssistMode(MNG_PolicyAssistMode.None);
                manager.GetComponent<Unity.MLAgents.DecisionRequester>().enabled = true;
                var behavior = manager.GetComponent<BehaviorParameters>();
                behavior.BehaviorType = BehaviorType.InferenceOnly;
                behavior.Model = manager.Team == m_EarlierTeam ? earlierModel : finalModel;
            }
            foreach (var motor in GetComponentsInChildren<MNG_PlayerMotor>(true))
                motor.ConfigureSpeedMultiplier(1f);
        }

        MNG_MS3CheckpointDuelMatch RecordMatch()
        {
            var finalTeam = Opponent(m_EarlierTeam);
            var earlierIndex = (int)m_EarlierTeam;
            var finalIndex = (int)finalTeam;
            var earlierGoals = m_EarlierTeam == Team.Red
                ? matchController.RedScore : matchController.NavyScore;
            var finalGoals = m_EarlierTeam == Team.Red
                ? matchController.NavyScore : matchController.RedScore;
            var score = earlierGoals > finalGoals ? 1f : earlierGoals == finalGoals ? 0.5f : 0f;

            var earlierRaw = CaptureCommands(m_Managers[earlierIndex], earlierIndex, true);
            var earlierEffective = CaptureCommands(m_Managers[earlierIndex], earlierIndex, false);
            var finalRaw = CaptureCommands(m_Managers[finalIndex], finalIndex, true);
            var finalEffective = CaptureCommands(m_Managers[finalIndex], finalIndex, false);
            var earlierOverrides = Delta(
                m_Managers[earlierIndex].BlockedForwardPassOverrideCount,
                ref m_PreviousOverrides[earlierIndex]);
            var finalOverrides = Delta(
                m_Managers[finalIndex].BlockedForwardPassOverrideCount,
                ref m_PreviousOverrides[finalIndex]);
            var earlierExplicitRewards = Delta(
                m_Managers[earlierIndex].ExplicitBlockedPassRewardCount,
                ref m_PreviousExplicitRewards[earlierIndex]);
            var finalExplicitRewards = Delta(
                m_Managers[finalIndex].ExplicitBlockedPassRewardCount,
                ref m_PreviousExplicitRewards[finalIndex]);

            var match = new MNG_MS3CheckpointDuelMatch
            {
                matchIndex = m_Matches.Count,
                spawnSeedOffset = m_SeedOffset,
                earlierTeam = m_EarlierTeam.ToString(),
                earlierGoals = earlierGoals,
                finalGoals = finalGoals,
                earlierOutcome = score > 0.5f ? "win" : score < 0.5f ? "loss" : "draw",
                earlierScoreValue = score,
                earlierRawCommandCounts = earlierRaw,
                earlierEffectiveCommandCounts = earlierEffective,
                finalRawCommandCounts = finalRaw,
                finalEffectiveCommandCounts = finalEffective,
                earlierBlockedPassOverrides = earlierOverrides,
                finalBlockedPassOverrides = finalOverrides,
                earlierExplicitBlockedPassRewards = earlierExplicitRewards,
                finalExplicitBlockedPassRewards = finalExplicitRewards,
                earlierPassStrikes = RewardDelta(
                    m_Rewards.GetPassStrikeCount(m_EarlierTeam),
                    ref m_PreviousPassStrikes[earlierIndex]),
                finalPassStrikes = RewardDelta(
                    m_Rewards.GetPassStrikeCount(finalTeam),
                    ref m_PreviousPassStrikes[finalIndex]),
                earlierCompletedPasses = RewardDelta(
                    m_Rewards.GetCompletedPassCount(m_EarlierTeam),
                    ref m_PreviousCompletedPasses[earlierIndex]),
                finalCompletedPasses = RewardDelta(
                    m_Rewards.GetCompletedPassCount(finalTeam),
                    ref m_PreviousCompletedPasses[finalIndex]),
                earlierShotStrikes = RewardDelta(
                    m_Rewards.GetShotStrikeCount(m_EarlierTeam),
                    ref m_PreviousShotStrikes[earlierIndex]),
                finalShotStrikes = RewardDelta(
                    m_Rewards.GetShotStrikeCount(finalTeam),
                    ref m_PreviousShotStrikes[finalIndex]),
                earlierValidShots = RewardDelta(
                    m_Rewards.GetValidShotCount(m_EarlierTeam),
                    ref m_PreviousValidShots[earlierIndex]),
                finalValidShots = RewardDelta(
                    m_Rewards.GetValidShotCount(finalTeam),
                    ref m_PreviousValidShots[finalIndex]),
                earlierFiveMeterAdvances = RewardDelta(
                    m_Rewards.GetAdvanceRewardCount(m_EarlierTeam),
                    ref m_PreviousAdvances[earlierIndex]),
                finalFiveMeterAdvances = RewardDelta(
                    m_Rewards.GetAdvanceRewardCount(finalTeam),
                    ref m_PreviousAdvances[finalIndex])
            };
            match.actionIntegrityPassed =
                earlierRaw.SequenceEqual(earlierEffective)
                && finalRaw.SequenceEqual(finalEffective)
                && earlierOverrides == 0
                && finalOverrides == 0
                && earlierExplicitRewards == 0
                && finalExplicitRewards == 0
                && m_Managers.All(manager =>
                    manager.PolicyAssistMode == MNG_PolicyAssistMode.None);
            m_Matches.Add(match);
            Debug.Log(
                $"MNG MS3 CHECKPOINT DUEL MATCH index={m_Matches.Count}/{m_MatchCount} "
                + $"earlierTeam={m_EarlierTeam} score={earlierGoals}:{finalGoals} "
                + $"outcome={match.earlierOutcome}");
            return match;
        }

        long[] CaptureCommands(MNG_ManagerAgent manager, int teamIndex, bool raw)
        {
            var current = new long[MNG_CommandMask.CommandCount];
            if (raw) manager.CopyRawCommandCounts(current);
            else manager.CopyCommandCounts(current);
            var previous = raw ? m_PreviousRawCommands : m_PreviousEffectiveCommands;
            var episode = new long[MNG_CommandMask.CommandCount];
            for (var index = 0; index < episode.Length; index++)
            {
                episode[index] = current[index] - previous[teamIndex, index];
                previous[teamIndex, index] = current[index];
            }
            return episode;
        }

        void WriteResultAndQuit()
        {
            m_Completed = true;
            var earlierRaw = SumCommands(match => match.earlierRawCommandCounts);
            var earlierEffective = SumCommands(match => match.earlierEffectiveCommandCounts);
            var finalRaw = SumCommands(match => match.finalRawCommandCounts);
            var finalEffective = SumCommands(match => match.finalEffectiveCommandCounts);
            var earlierScoreRate = m_Matches.Average(match => match.earlierScoreValue);
            var result = new MNG_MS3CheckpointDuelResult
            {
                protocol = m_Protocol,
                duelId = duelId,
                earlierCandidateId = earlierCandidateId,
                earlierModelSha256 = earlierModelSha256,
                finalCandidateId = finalCandidateId,
                finalModelSha256 = finalModelSha256,
                earlierTeam = m_EarlierTeam.ToString(),
                seedOffset = m_SeedOffset,
                matchSeconds = m_MatchSeconds,
                matches = m_Matches.Count,
                earlierWins = m_Matches.Count(match => match.earlierScoreValue > 0.5f),
                draws = m_Matches.Count(match => Math.Abs(match.earlierScoreValue - 0.5f) < 0.001f),
                earlierLosses = m_Matches.Count(match => match.earlierScoreValue < 0.5f),
                earlierGoals = m_Matches.Sum(match => match.earlierGoals),
                finalGoals = m_Matches.Sum(match => match.finalGoals),
                earlierScoreRate = earlierScoreRate,
                finalScoreRate = 1f - earlierScoreRate,
                earlierRawCommandCounts = earlierRaw,
                earlierEffectiveCommandCounts = earlierEffective,
                finalRawCommandCounts = finalRaw,
                finalEffectiveCommandCounts = finalEffective,
                earlierBlockedPassOverrides = m_Matches.Sum(match => match.earlierBlockedPassOverrides),
                finalBlockedPassOverrides = m_Matches.Sum(match => match.finalBlockedPassOverrides),
                earlierExplicitBlockedPassRewards = m_Matches.Sum(
                    match => match.earlierExplicitBlockedPassRewards),
                finalExplicitBlockedPassRewards = m_Matches.Sum(
                    match => match.finalExplicitBlockedPassRewards),
                earlierPassStrikes = m_Matches.Sum(match => match.earlierPassStrikes),
                finalPassStrikes = m_Matches.Sum(match => match.finalPassStrikes),
                earlierCompletedPasses = m_Matches.Sum(match => match.earlierCompletedPasses),
                finalCompletedPasses = m_Matches.Sum(match => match.finalCompletedPasses),
                earlierShotStrikes = m_Matches.Sum(match => match.earlierShotStrikes),
                finalShotStrikes = m_Matches.Sum(match => match.finalShotStrikes),
                earlierValidShots = m_Matches.Sum(match => match.earlierValidShots),
                finalValidShots = m_Matches.Sum(match => match.finalValidShots),
                earlierFiveMeterAdvances = m_Matches.Sum(
                    match => match.earlierFiveMeterAdvances),
                finalFiveMeterAdvances = m_Matches.Sum(
                    match => match.finalFiveMeterAdvances),
                actionIntegrityPassed = m_Matches.All(match => match.actionIntegrityPassed),
                matchResults = m_Matches.ToArray()
            };
            var output = Path.GetFullPath(m_OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(output)
                ?? throw new InvalidOperationException(
                    "MS3 checkpoint duel output directory is invalid."));
            File.WriteAllText(output, JsonUtility.ToJson(result, true) + Environment.NewLine);
            Debug.Log(
                $"MNG MS3 CHECKPOINT DUEL COMPLETE duel={duelId} "
                + $"earlierTeam={m_EarlierTeam} scoreRate={result.earlierScoreRate:R} "
                + $"goals={result.earlierGoals}:{result.finalGoals} output={output}");
            Application.Quit(0);
        }

        long[] SumCommands(Func<MNG_MS3CheckpointDuelMatch, long[]> selector)
        {
            var sum = new long[MNG_CommandMask.CommandCount];
            foreach (var match in m_Matches)
            {
                var values = selector(match);
                for (var index = 0; index < sum.Length; index++) sum[index] += values[index];
            }
            return sum;
        }

        static long Delta(long current, ref long previous)
        {
            var result = current - previous;
            previous = current;
            return result;
        }

        static int RewardDelta(int current, ref int previous)
        {
            var result = current - previous;
            previous = current;
            return result;
        }

        static Team ParseTeam(string value)
        {
            if (Enum.TryParse(value, true, out Team team)) return team;
            throw new InvalidOperationException(
                $"Unsupported MS3 checkpoint duel team: {value}");
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
            throw new InvalidOperationException(
                $"{name} must be a positive integer: {value}");
        }

        static float ReadPositiveFloat(string[] arguments, string name, float fallback)
        {
            var value = ReadArgument(arguments, name, fallback.ToString("R"));
            if (float.TryParse(
                    value,
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var parsed)
                && MNG_MatchSnapshot.IsFinite(parsed)
                && parsed > 0f)
                return parsed;
            throw new InvalidOperationException(
                $"{name} must be a positive finite number: {value}");
        }

        static string RequireText(string value, string parameter)
            => string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException(
                    "MS3 checkpoint duel identity is required.", parameter)
                : value;
    }
}
