using System;
using System.IO;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    [DefaultExecutionOrder(-500)]
    [DisallowMultipleComponent]
    public sealed class MNG_M3EvaluationController : MonoBehaviour
    {
        [SerializeField] MNG_FallbackMatchController matches;
        [SerializeField] string runId;
        [SerializeField] string modelSha256;

        int m_ScorelessMatches;
        bool m_Completed;

        public void Configure(
            MNG_FallbackMatchController configuredMatches,
            string configuredRunId,
            string configuredModelSha256)
        {
            matches = configuredMatches != null
                ? configuredMatches
                : throw new ArgumentNullException(nameof(configuredMatches));
            runId = !string.IsNullOrWhiteSpace(configuredRunId)
                ? configuredRunId
                : throw new ArgumentException("Evaluation run ID is required.", nameof(configuredRunId));
            modelSha256 = IsSha256(configuredModelSha256)
                ? configuredModelSha256.ToLowerInvariant()
                : throw new ArgumentException("Evaluation model SHA-256 is invalid.", nameof(configuredModelSha256));
        }

        void Awake()
        {
            if (matches == null) matches = GetComponent<MNG_FallbackMatchController>();
            if (matches == null || !matches.IsFullMatch)
                throw new InvalidOperationException("MNG M3 evaluation requires the 300-second match controller.");
            if (string.IsNullOrWhiteSpace(runId) || !IsSha256(modelSha256))
                throw new InvalidOperationException("MNG M3 evaluation identity is incomplete.");
            matches.SetValidationScenarios(true);
            matches.MatchCompleted += OnMatchCompleted;
        }

        void OnDestroy()
        {
            if (matches != null) matches.MatchCompleted -= OnMatchCompleted;
        }

        void OnMatchCompleted(int matchIndex, int redScore, int navyScore)
        {
            if (m_Completed) return;
            if (redScore == 0 && navyScore == 0) m_ScorelessMatches++;
            if (matches.CompletedMatches < MNG_CurriculumCatalog.M3FormalEvaluationMatches) return;
            m_Completed = true;

            var result = new MNG_M3EvaluationResult
            {
                runId = runId,
                modelSha256 = modelSha256,
                validationSeed = MNG_CurriculumCatalog.M3ValidationSeed,
                generatorVersion = MNG_FallbackMatchScenarioGenerator.GeneratorVersion,
                plannerVersion = MNG_TeamPlanner.PlannerVersion,
                matches = matches.CompletedMatches,
                redWins = matches.RedWins,
                draws = matches.Draws,
                redLosses = matches.RedLosses,
                redGoals = matches.RedGoals,
                navyGoals = matches.NavyGoals,
                scorelessMatches = m_ScorelessMatches,
                scoreRate = matches.ScoreRate,
                scorelessMatchRate = (float)m_ScorelessMatches / matches.CompletedMatches,
                minimumScoreRate = MNG_M3EvaluationRules.MinimumScoreRate,
                maximumScorelessMatches = MNG_M3EvaluationRules.MaximumScorelessMatches
            };
            result.passed = MNG_M3EvaluationRules.Passes(
                result.matches,
                result.redWins,
                result.draws,
                result.redLosses,
                result.redGoals,
                result.navyGoals,
                result.scorelessMatches);

            var outputPath = ReadArgument("-mngEvaluationOutput");
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = Path.Combine(Application.persistentDataPath, "mng-m3-evaluation.json");
            outputPath = Path.GetFullPath(outputPath);
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(outputPath, JsonUtility.ToJson(result, true) + Environment.NewLine);

            Debug.Log($"MNG M3 MODEL EVALUATION COMPLETE run={runId} matches={result.matches} "
                + $"record={result.redWins}-{result.draws}-{result.redLosses} "
                + $"goals={result.redGoals}:{result.navyGoals} scoreRate={result.scoreRate:R} "
                + $"scoreless={result.scorelessMatches} passed={result.passed} "
                + $"lastMatch={matchIndex} output={outputPath}");
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
