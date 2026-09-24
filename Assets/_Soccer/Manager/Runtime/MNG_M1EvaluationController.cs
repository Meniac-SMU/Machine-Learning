using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class MNG_M1EvaluationController : MonoBehaviour
    {
        [SerializeField] MNG_CurriculumController curriculum;
        [SerializeField] string runId;
        [SerializeField] string modelSha256;
        [SerializeField] string policyKind = "onnx";

        bool m_Completed;
        MNG_ManagerAgent m_Manager;
        readonly long[] m_PreviousCommandCounts = new long[MNG_CommandMask.CommandCount];
        readonly long[] m_CurrentCommandCounts = new long[MNG_CommandMask.CommandCount];
        readonly long[] m_PreviousMaskCounts = new long[MNG_CommandMask.CommandCount];
        readonly long[] m_CurrentMaskCounts = new long[MNG_CommandMask.CommandCount];
        readonly List<MNG_M1ScenarioEvaluation> m_ScenarioResults = new();
        MNG_TacticalRewardTracker m_TacticalRewards;
        int m_PreviousPassStrikes;
        int m_PreviousShotStrikes;
        int m_PreviousCompletedPasses;
        int m_PreviousValidShots;
        int m_PreviousAdvances;

        public string RunId => runId;
        public string ModelSha256 => modelSha256;

        public void Configure(
            MNG_CurriculumController configuredCurriculum,
            string configuredRunId,
            string configuredModelSha256,
            string configuredPolicyKind = "onnx")
        {
            curriculum = configuredCurriculum != null
                ? configuredCurriculum
                : throw new ArgumentNullException(nameof(configuredCurriculum));
            runId = !string.IsNullOrWhiteSpace(configuredRunId)
                ? configuredRunId
                : throw new ArgumentException("Evaluation run ID is required.", nameof(configuredRunId));
            modelSha256 = IsSha256(configuredModelSha256)
                ? configuredModelSha256.ToLowerInvariant()
                : throw new ArgumentException("Evaluation model SHA-256 is invalid.", nameof(configuredModelSha256));
            policyKind = configuredPolicyKind == "uniform-valid-command" ? configuredPolicyKind : "onnx";
        }

        void Awake()
        {
            if (curriculum == null) curriculum = GetComponent<MNG_CurriculumController>();
            if (curriculum == null)
                throw new InvalidOperationException("MNG M1 evaluation requires a curriculum controller.");
            m_Manager = GetComponentInChildren<MNG_ManagerAgent>(true);
            if (m_Manager == null || m_Manager.Team != MachineLearning.Soccer.Team.Red)
                throw new InvalidOperationException("MNG M1 evaluation requires the Red manager.");
            m_TacticalRewards = GetComponent<MNG_TacticalRewardTracker>();
            if (m_TacticalRewards == null)
                throw new InvalidOperationException("MNG M1 evaluation requires tactical reward telemetry.");
            if (string.IsNullOrWhiteSpace(runId) || !IsSha256(modelSha256))
                throw new InvalidOperationException("MNG M1 evaluation identity is incomplete.");
            curriculum.SetValidationScenarios(true);
            curriculum.EpisodeCompleted += OnEpisodeCompleted;
        }

        void OnDestroy()
        {
            if (curriculum != null) curriculum.EpisodeCompleted -= OnEpisodeCompleted;
        }

        void OnEpisodeCompleted(int scenarioIndex, MNG_CurriculumOutcome outcome)
        {
            if (m_Completed) return;
            RecordScenario(scenarioIndex, outcome);
            if (curriculum.CompletedEpisodes < MNG_CurriculumCatalog.M1EvaluationScenarioCount) return;

            m_Completed = true;
            var result = new MNG_M1EvaluationResult
            {
                runId = runId,
                policyKind = policyKind,
                modelSha256 = modelSha256,
                validationSeed = MNG_CurriculumCatalog.M1ValidationSeed,
                generatorVersion = MNG_AttackScenarioGenerator.GeneratorVersion,
                plannerVersion = MNG_TeamPlanner.PlannerVersion,
                scenarios = curriculum.CompletedEpisodes,
                goals = curriculum.Goals,
                ownGoals = curriculum.OwnGoals,
                timeouts = curriculum.Timeouts,
                randomBaselineGoals = MNG_M1EvaluationRules.RandomBaselineGoals,
                requiredGoals = MNG_M1EvaluationRules.RequiredGoals,
                maximumOwnGoals = MNG_M1EvaluationRules.MaximumOwnGoals,
                minimumPassPlateStrikes = MNG_M1EvaluationRules.MinimumPassPlateStrikes,
                minimumCompletedPasses = MNG_M1EvaluationRules.MinimumCompletedPasses
            };
            result.commandCounts = (long[])m_CurrentCommandCounts.Clone();
            result.passAvailableDecisions = m_CurrentMaskCounts[(int)MNG_Command.PassBuild];
            result.shotAvailableDecisions = m_CurrentMaskCounts[(int)MNG_Command.AttemptShot];
            result.passPlateStrikes = m_TacticalRewards.PassStrikeCount;
            result.shotPlateStrikes = m_TacticalRewards.ShotStrikeCount;
            result.completedPasses = m_TacticalRewards.CompletedPassCount;
            result.validShots = m_TacticalRewards.ValidShotCount;
            result.fiveMeterAdvances = m_TacticalRewards.AdvanceRewardCount;
            result.scenarioResults = m_ScenarioResults.ToArray();
            result.passed = MNG_M1EvaluationRules.Passes(
                result.scenarios,
                result.goals,
                result.ownGoals,
                result.timeouts,
                result.passPlateStrikes,
                result.completedPasses);

            var outputPath = ReadArgument("-mngEvaluationOutput");
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = Path.Combine(Application.persistentDataPath, "mng-m1-evaluation.json");
            outputPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, JsonUtility.ToJson(result, true) + Environment.NewLine);

            Debug.Log($"MNG M1 MODEL EVALUATION COMPLETE run={runId} "
                + $"scenarios={result.scenarios} goals={result.goals} "
                + $"ownGoals={result.ownGoals} timeouts={result.timeouts} "
                + $"requiredGoals={result.requiredGoals} passStrikes={result.passPlateStrikes}/"
                + $"{result.minimumPassPlateStrikes} completedPasses={result.completedPasses}/"
                + $"{result.minimumCompletedPasses} passed={result.passed} "
                + $"lastScenario={scenarioIndex} lastOutcome={outcome} output={outputPath}");
            Application.Quit(0);
        }

        void RecordScenario(int scenarioIndex, MNG_CurriculumOutcome outcome)
        {
            m_Manager.CopyCommandCounts(m_CurrentCommandCounts);
            m_Manager.CopyMaskAvailabilityCounts(m_CurrentMaskCounts);
            var episodeCounts = new long[MNG_CommandMask.CommandCount];
            for (var command = 0; command < episodeCounts.Length; command++)
            {
                episodeCounts[command] = m_CurrentCommandCounts[command]
                    - m_PreviousCommandCounts[command];
                m_PreviousCommandCounts[command] = m_CurrentCommandCounts[command];
            }

            var scenario = curriculum.CurrentScenario;
            var passStrikes = m_TacticalRewards.PassStrikeCount - m_PreviousPassStrikes;
            var shotStrikes = m_TacticalRewards.ShotStrikeCount - m_PreviousShotStrikes;
            var completedPasses = m_TacticalRewards.CompletedPassCount - m_PreviousCompletedPasses;
            var validShots = m_TacticalRewards.ValidShotCount - m_PreviousValidShots;
            var advances = m_TacticalRewards.AdvanceRewardCount - m_PreviousAdvances;
            m_ScenarioResults.Add(new MNG_M1ScenarioEvaluation
            {
                scenarioIndex = scenarioIndex,
                kind = scenario.Kind.ToString(),
                ballX = scenario.BallPosition.x,
                ballY = scenario.BallPosition.y,
                outcome = outcome.ToString(),
                elapsedSeconds = curriculum.CurrentEpisodeElapsedSeconds,
                commandCounts = episodeCounts,
                passAvailableDecisions = DeltaMask(MNG_Command.PassBuild),
                shotAvailableDecisions = DeltaMask(MNG_Command.AttemptShot),
                passPlateStrikes = passStrikes,
                shotPlateStrikes = shotStrikes,
                completedPasses = completedPasses,
                validShots = validShots,
                fiveMeterAdvances = advances
            });
            m_PreviousPassStrikes = m_TacticalRewards.PassStrikeCount;
            m_PreviousShotStrikes = m_TacticalRewards.ShotStrikeCount;
            m_PreviousCompletedPasses = m_TacticalRewards.CompletedPassCount;
            m_PreviousValidShots = m_TacticalRewards.ValidShotCount;
            m_PreviousAdvances = m_TacticalRewards.AdvanceRewardCount;
        }

        long DeltaMask(MNG_Command command)
        {
            var index = (int)command;
            var delta = m_CurrentMaskCounts[index] - m_PreviousMaskCounts[index];
            m_PreviousMaskCounts[index] = m_CurrentMaskCounts[index];
            return delta;
        }

        static string ReadArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            }
            return string.Empty;
        }

        static bool IsSha256(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length != 64) return false;
            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if (!((character >= '0' && character <= '9')
                    || (character >= 'a' && character <= 'f')
                    || (character >= 'A' && character <= 'F')))
                    return false;
            }
            return true;
        }
    }
}
