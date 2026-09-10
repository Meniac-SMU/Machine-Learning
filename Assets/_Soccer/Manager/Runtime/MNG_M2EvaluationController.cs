using System;
using System.IO;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class MNG_M2EvaluationController : MonoBehaviour
    {
        [SerializeField] MNG_CurriculumController curriculum;
        [SerializeField] string runId;
        [SerializeField] string modelSha256;

        bool m_Completed;

        public void Configure(
            MNG_CurriculumController configuredCurriculum,
            string configuredRunId,
            string configuredModelSha256)
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
        }

        void Awake()
        {
            if (curriculum == null) curriculum = GetComponent<MNG_CurriculumController>();
            if (curriculum == null || curriculum.Stage != MNG_CurriculumStage.M2DefenseChoice)
                throw new InvalidOperationException("MNG M2 evaluation requires an M2 curriculum controller.");
            if (string.IsNullOrWhiteSpace(runId) || !IsSha256(modelSha256))
                throw new InvalidOperationException("MNG M2 evaluation identity is incomplete.");
            curriculum.SetValidationScenarios(true);
            curriculum.EpisodeCompleted += OnEpisodeCompleted;
        }

        void OnDestroy()
        {
            if (curriculum != null) curriculum.EpisodeCompleted -= OnEpisodeCompleted;
        }

        void OnEpisodeCompleted(int scenarioIndex, MNG_CurriculumOutcome outcome)
        {
            if (m_Completed
                || curriculum.CompletedEpisodes < MNG_CurriculumCatalog.M2EvaluationScenarioCount)
                return;
            m_Completed = true;

            var result = new MNG_M2EvaluationResult
            {
                runId = runId,
                modelSha256 = modelSha256,
                validationSeed = MNG_CurriculumCatalog.M2ValidationSeed,
                generatorVersion = MNG_DefenseScenarioGenerator.GeneratorVersion,
                plannerVersion = MNG_TeamPlanner.PlannerVersion,
                scenarios = curriculum.CompletedEpisodes,
                recoveriesWithinDeadline = curriculum.RecoveriesWithinDeadline,
                allRecoveries = curriculum.Recoveries,
                concededGoals = curriculum.OwnGoals,
                redGoals = curriculum.Goals,
                timeouts = curriculum.Timeouts,
                minimumRecoveriesWithinDeadline = MNG_M2EvaluationRules.MinimumRecoveriesWithinDeadline,
                randomBaselineRecoveriesWithinDeadline =
                    MNG_M2EvaluationRules.RandomBaselineRecoveriesWithinDeadline,
                randomBaselineConcededGoals = MNG_M2EvaluationRules.RandomBaselineConcededGoals,
                maximumConcededGoals = MNG_M2EvaluationRules.MaximumConcededGoals
            };
            result.passed = MNG_M2EvaluationRules.Passes(
                result.scenarios,
                result.recoveriesWithinDeadline,
                result.concededGoals,
                result.redGoals,
                result.timeouts);

            var outputPath = ReadArgument("-mngEvaluationOutput");
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = Path.Combine(Application.persistentDataPath, "mng-m2-evaluation.json");
            outputPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, JsonUtility.ToJson(result, true) + Environment.NewLine);

            Debug.Log($"MNG M2 MODEL EVALUATION COMPLETE run={runId} scenarios={result.scenarios} "
                + $"fastRecoveries={result.recoveriesWithinDeadline} allRecoveries={result.allRecoveries} "
                + $"conceded={result.concededGoals} redGoals={result.redGoals} "
                + $"timeouts={result.timeouts} passed={result.passed} "
                + $"lastScenario={scenarioIndex} lastOutcome={outcome} output={outputPath}");
            Application.Quit(0);
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
