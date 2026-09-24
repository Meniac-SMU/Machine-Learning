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
    public sealed class MNG_MS1EvaluationController : MonoBehaviour
    {
        public const int EvaluationEpisodes = 120;
        public const int GateEpisodesPerGroup = 40;
        public const int PassSubsetEpisodes = 20;

        [SerializeField] MNG_MS1Controller curriculum;
        [SerializeField] string runId;
        [SerializeField] string candidateId;
        [SerializeField] string modelSha256;

        readonly List<MNG_MS1EvaluationEpisode> m_Results = new();
        readonly long[] m_PreviousCommands = new long[MNG_CommandMask.CommandCount];
        MNG_ManagerAgent m_Manager;
        MNG_TacticalRewardTracker m_Rewards;
        int m_PreviousPassStrikes;
        int m_PreviousShotStrikes;
        int m_PreviousCompletedPasses;
        int m_PreviousValidShots;
        int m_PreviousAdvances;
        bool m_Completed;
        bool m_RandomPolicy;

        public void Configure(
            MNG_MS1Controller configuredCurriculum,
            string configuredRunId,
            string configuredCandidateId,
            string configuredModelSha256)
        {
            curriculum = configuredCurriculum ?? throw new ArgumentNullException(nameof(configuredCurriculum));
            runId = RequireIdentity(configuredRunId, nameof(configuredRunId));
            candidateId = RequireIdentity(configuredCandidateId, nameof(configuredCandidateId));
            modelSha256 = IsSha256(configuredModelSha256)
                ? configuredModelSha256.ToLowerInvariant()
                : throw new ArgumentException("MS1 evaluation model SHA-256 is invalid.", nameof(configuredModelSha256));
        }

        void Awake()
        {
            curriculum ??= GetComponent<MNG_MS1Controller>();
            m_Manager = GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Single(manager => manager.Team == Team.Red && manager.gameObject.activeInHierarchy);
            m_Rewards = GetComponent<MNG_TacticalRewardTracker>();
            if (curriculum == null || m_Rewards == null)
                throw new InvalidOperationException("MS1 evaluation bindings are incomplete.");
            if (string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(candidateId)
                || !IsSha256(modelSha256))
                throw new InvalidOperationException("MS1 evaluation identity is incomplete.");

            m_RandomPolicy = string.Equals(ReadArgument("-mngRandomPolicy"), "true",
                StringComparison.OrdinalIgnoreCase);
            if (m_RandomPolicy)
            {
                var behavior = m_Manager.GetComponent<BehaviorParameters>();
                behavior.BehaviorType = BehaviorType.HeuristicOnly;
                m_Manager.ConfigureUniformRandomHeuristic(391001);
            }
            curriculum.EpisodeCompleted += OnEpisodeCompleted;
        }

        void OnDestroy()
        {
            if (curriculum != null) curriculum.EpisodeCompleted -= OnEpisodeCompleted;
        }

        void OnEpisodeCompleted(MNG_MS1EpisodeResult episode)
        {
            if (m_Completed) return;
            var currentCommands = new long[MNG_CommandMask.CommandCount];
            m_Manager.CopyCommandCounts(currentCommands);
            var episodeCommands = new long[MNG_CommandMask.CommandCount];
            for (var index = 0; index < episodeCommands.Length; index++)
            {
                episodeCommands[index] = currentCommands[index] - m_PreviousCommands[index];
                m_PreviousCommands[index] = currentCommands[index];
            }

            var passStrikes = m_Rewards.GetPassStrikeCount(Team.Red) - m_PreviousPassStrikes;
            var shotStrikes = m_Rewards.GetShotStrikeCount(Team.Red) - m_PreviousShotStrikes;
            var completedPasses = m_Rewards.GetCompletedPassCount(Team.Red) - m_PreviousCompletedPasses;
            var validShots = m_Rewards.GetValidShotCount(Team.Red) - m_PreviousValidShots;
            var advances = m_Rewards.GetAdvanceRewardCount(Team.Red) - m_PreviousAdvances;
            var attack = episode.Group == MNG_MS1SituationGroup.Attack;
            var success = attack
                ? episode.Outcome == "goal" || validShots > 0
                : episode.Outcome != "conceded" && episode.Recovered;
            m_Results.Add(new MNG_MS1EvaluationEpisode
            {
                episodeIndex = episode.EpisodeIndex,
                group = episode.Group.ToString(),
                kind = episode.Kind,
                outcome = episode.Outcome,
                elapsedSeconds = episode.ElapsedSeconds,
                recovered = episode.Recovered,
                success = success,
                commandCounts = episodeCommands,
                passStrikes = passStrikes,
                shotStrikes = shotStrikes,
                completedPasses = completedPasses,
                validShots = validShots,
                fiveMeterAdvances = advances
            });
            m_PreviousPassStrikes = m_Rewards.GetPassStrikeCount(Team.Red);
            m_PreviousShotStrikes = m_Rewards.GetShotStrikeCount(Team.Red);
            m_PreviousCompletedPasses = m_Rewards.GetCompletedPassCount(Team.Red);
            m_PreviousValidShots = m_Rewards.GetValidShotCount(Team.Red);
            m_PreviousAdvances = m_Rewards.GetAdvanceRewardCount(Team.Red);

            if (m_Results.Count < EvaluationEpisodes) return;
            WriteResultAndQuit();
        }

        void WriteResultAndQuit()
        {
            m_Completed = true;
            var allAttack = m_Results
                .Where(result => result.group == MNG_MS1SituationGroup.Attack.ToString())
                .ToArray();
            var allDefense = m_Results
                .Where(result => result.group == MNG_MS1SituationGroup.DefenseTransition.ToString())
                .ToArray();
            var attack = allAttack.Take(GateEpisodesPerGroup).ToArray();
            var defense = allDefense.Take(GateEpisodesPerGroup).ToArray();
            var passSubset = allAttack
                .Where(result => result.kind == MNG_AttackScenarioKind.Pass.ToString())
                .Take(PassSubsetEpisodes)
                .ToArray();
            if (attack.Length != GateEpisodesPerGroup
                || defense.Length != GateEpisodesPerGroup
                || passSubset.Length != PassSubsetEpisodes)
                throw new InvalidOperationException(
                    "MS1 evaluation did not produce the fixed 40/40 gate and 20 Pass fixtures.");
            var commands = new long[MNG_CommandMask.CommandCount];
            foreach (var episode in m_Results)
                for (var index = 0; index < commands.Length; index++)
                    commands[index] += episode.commandCounts[index];
            var result = new MNG_MS1EvaluationResult
            {
                runId = runId,
                candidateId = candidateId,
                policyKind = m_RandomPolicy ? "uniform-valid-command" : "onnx",
                modelSha256 = modelSha256,
                episodes = m_Results.Count,
                attackEpisodes = attack.Length,
                defenseEpisodes = defense.Length,
                attackSuccesses = attack.Count(item => item.success),
                defenseSuccesses = defense.Count(item => item.success),
                passSubsetEpisodes = passSubset.Length,
                passSubsetCompletedPasses = passSubset.Sum(item => item.completedPasses),
                passStrikes = m_Results.Sum(item => item.passStrikes),
                shotStrikes = m_Results.Sum(item => item.shotStrikes),
                completedPasses = m_Results.Sum(item => item.completedPasses),
                validShots = m_Results.Sum(item => item.validShots),
                fiveMeterAdvances = m_Results.Sum(item => item.fiveMeterAdvances),
                commandCounts = commands,
                episodeResults = m_Results.ToArray()
            };
            var output = ReadArgument("-mngEvaluationOutput");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(Application.persistentDataPath, "mng-ms1-evaluation.json");
            output = Path.GetFullPath(output);
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? Application.persistentDataPath);
            File.WriteAllText(output, JsonUtility.ToJson(result, true) + Environment.NewLine);
            Debug.Log($"MNG MS1 EVALUATION COMPLETE policy={result.policyKind} "
                + $"attack={result.attackSuccesses}/{result.attackEpisodes} "
                + $"defense={result.defenseSuccesses}/{result.defenseEpisodes} "
                + $"passes={result.passSubsetCompletedPasses}/{result.passSubsetEpisodes} output={output}");
            Application.Quit(0);
        }

        static string RequireIdentity(string value, string name)
            => !string.IsNullOrWhiteSpace(value) ? value : throw new ArgumentException("Identity is required.", name);

        static string ReadArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            return string.Empty;
        }

        static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
            return value.All(character => (character >= '0' && character <= '9')
                || (character >= 'a' && character <= 'f')
                || (character >= 'A' && character <= 'F'));
        }
    }
}
