using System;
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

        bool m_Completed;

        public string RunId => runId;
        public string ModelSha256 => modelSha256;

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
            if (curriculum == null)
                throw new InvalidOperationException("MNG M1 evaluation requires a curriculum controller.");
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
            if (m_Completed
                || curriculum.CompletedEpisodes < MNG_CurriculumCatalog.M1EvaluationScenarioCount)
                return;

            m_Completed = true;
            var result = new MNG_M1EvaluationResult
            {
                runId = runId,
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
                maximumOwnGoals = MNG_M1EvaluationRules.MaximumOwnGoals
            };
            result.passed = MNG_M1EvaluationRules.Passes(
                result.scenarios, result.goals, result.ownGoals, result.timeouts);

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
                + $"requiredGoals={result.requiredGoals} passed={result.passed} "
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
