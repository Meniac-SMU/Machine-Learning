using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace MachineLearning.Soccer.Manager.Editor
{
    public static class MNG_TrainingBuildBuilder
    {
        public const string OutputDirectory = "Builds/MNG_Training";
        public const string ExecutablePath = OutputDirectory + "/MNG_Training.exe";
        public const string TrainerConfigPath =
            "Assets/_Soccer/Manager/Training/MNG_M1_ConnectionSmoke.yaml";
        public const string LevelDataPath = OutputDirectory + "/MNG_Training_Data/level0";
        public const string AttackOutputDirectory = "Builds/MNG_AttackChoice";
        public const string AttackExecutablePath = AttackOutputDirectory + "/MNG_AttackChoice.exe";
        public const string AttackTrainerConfigPath =
            "Assets/_Soccer/Manager/Training/MNG_M1_AttackChoice.yaml";
        public const string AttackLevelDataPath =
            AttackOutputDirectory + "/MNG_AttackChoice_Data/level0";
        public const string AttackMovingOutputDirectory = "Builds/MNG_AttackMoving";
        public const string AttackMovingExecutablePath =
            AttackMovingOutputDirectory + "/MNG_AttackMoving.exe";
        public const string AttackMovingTrainerConfigPath =
            "Assets/_Soccer/Manager/Training/MNG_M1_AttackMoving.yaml";
        public const string AttackMovingLevelDataPath =
            AttackMovingOutputDirectory + "/MNG_AttackMoving_Data/level0";
        public const string DefenseOutputDirectory = "Builds/MNG_DefenseChoice";
        public const string DefenseExecutablePath = DefenseOutputDirectory + "/MNG_DefenseChoice.exe";
        public const string DefenseTrainerConfigPath =
            "Assets/_Soccer/Manager/Training/MNG_M2_DefenseChoice.yaml";
        public const string DefenseLevelDataPath =
            DefenseOutputDirectory + "/MNG_DefenseChoice_Data/level0";
        public const string FallbackMatchOutputDirectory = "Builds/MNG_FallbackMatch";
        public const string FallbackMatchExecutablePath =
            FallbackMatchOutputDirectory + "/MNG_FallbackMatch.exe";
        public const string FallbackMatchTrainerConfigPath =
            "Assets/_Soccer/Manager/Training/MNG_M3_FallbackMatch.yaml";
        public const string FallbackMatchLevelDataPath =
            FallbackMatchOutputDirectory + "/MNG_FallbackMatch_Data/level0";
        public const string EvaluationOutputRoot = "Builds/MNG_M1Evaluation";
        public const string EvaluationProtocolPath =
            "Assets/_Soccer/Manager/Evaluation/MNG_M1_Protocol_v6.json";
        public const string AttackMovingEvaluationOutputRoot = "Builds/MNG_M1MovingEvaluation";
        public const string AttackMovingEvaluationProtocolPath =
            "Assets/_Soccer/Manager/Evaluation/MNG_M1Moving_Protocol_v1.json";
        public const string EvaluationModelDirectory =
            "Assets/_Soccer/Manager/Models";
        public const string DefenseEvaluationOutputRoot = "Builds/MNG_M2Evaluation";
        public const string DefenseEvaluationProtocolPath =
            "Assets/_Soccer/Manager/Evaluation/MNG_M2_Protocol_v3.json";
        public const string FallbackMatchEvaluationOutputRoot = "Builds/MNG_M3Evaluation";
        public const string FallbackMatchEvaluationProtocolPath =
            "Assets/_Soccer/Manager/Evaluation/MNG_M3_Protocol_v3.json";

        public static void BuildWindowsBatch()
        {
            try
            {
                MNG_ProjectBuilder.ValidateM0Assets();
                var requestedStage = ReadArgument("-mngStage", "M1-Smoke");
                var attackChoice = string.Equals(
                    requestedStage, "M1-AttackChoice", StringComparison.OrdinalIgnoreCase);
                var attackMoving = string.Equals(
                    requestedStage, "M1-AttackMoving", StringComparison.OrdinalIgnoreCase);
                var defenseChoice = string.Equals(
                    requestedStage, "M2-DefenseChoice", StringComparison.OrdinalIgnoreCase);
                var fallbackMatch = string.Equals(
                    requestedStage, "M3-FallbackMatch", StringComparison.OrdinalIgnoreCase);
                if (!attackChoice && !attackMoving && !defenseChoice && !fallbackMatch && !string.Equals(
                        requestedStage, "M1-Smoke", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Unsupported MNG build stage: {requestedStage}");

                var outputDirectory = fallbackMatch ? FallbackMatchOutputDirectory
                    : defenseChoice ? DefenseOutputDirectory
                    : attackMoving ? AttackMovingOutputDirectory
                    : attackChoice ? AttackOutputDirectory : OutputDirectory;
                var executablePath = fallbackMatch ? FallbackMatchExecutablePath
                    : defenseChoice ? DefenseExecutablePath
                    : attackMoving ? AttackMovingExecutablePath
                    : attackChoice ? AttackExecutablePath : ExecutablePath;
                var trainerConfigPath = fallbackMatch ? FallbackMatchTrainerConfigPath
                    : defenseChoice ? DefenseTrainerConfigPath
                    : attackMoving ? AttackMovingTrainerConfigPath
                    : attackChoice ? AttackTrainerConfigPath : TrainerConfigPath;
                var levelDataPath = fallbackMatch ? FallbackMatchLevelDataPath
                    : defenseChoice ? DefenseLevelDataPath
                    : attackMoving ? AttackMovingLevelDataPath
                    : attackChoice ? AttackLevelDataPath : LevelDataPath;
                var scenePath = fallbackMatch ? MNG_ProjectBuilder.FallbackMatch60ScenePath
                    : defenseChoice ? MNG_ProjectBuilder.DefenseChoiceScenePath
                    : attackMoving ? MNG_ProjectBuilder.AttackMovingScenePath
                    : attackChoice ? MNG_ProjectBuilder.AttackChoiceScenePath
                    : MNG_ProjectBuilder.ConnectionSmokeScenePath;
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
                if (scene == null)
                    throw new InvalidOperationException(
                        $"Missing training Scene: {scenePath}");

                Directory.CreateDirectory(outputDirectory);
                var options = new BuildPlayerOptions
                {
                    scenes = new[] { scenePath },
                    locationPathName = executablePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                };
                var report = BuildPipeline.BuildPlayer(options);
                var summary = report.summary;
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                    throw new InvalidOperationException(
                        $"MNG Windows build failed: result={summary.result}, errors={summary.totalErrors}.");

                var executable = new FileInfo(executablePath);
                if (!executable.Exists || executable.Length <= 0)
                    throw new InvalidOperationException($"MNG build executable is missing: {executablePath}");
                var config = new FileInfo(trainerConfigPath);
                if (!config.Exists) throw new InvalidOperationException($"MNG trainer config is missing: {trainerConfigPath}");
                if (!File.Exists(levelDataPath)) throw new InvalidOperationException($"MNG Player level data is missing: {levelDataPath}");
                File.WriteAllText(
                    Path.Combine(outputDirectory, "build-info.json"),
                    "{\n"
                    + $"  \"stage\": \"{requestedStage}\",\n"
                    + $"  \"scene\": \"{scenePath}\",\n"
                    + $"  \"trainerConfig\": \"{trainerConfigPath}\",\n"
                    + $"  \"trainerConfigSha256\": \"{Sha256(trainerConfigPath)}\",\n"
                    + $"  \"executableSha256\": \"{Sha256(executablePath)}\",\n"
                    + $"  \"levelDataSha256\": \"{Sha256(levelDataPath)}\",\n"
                    + $"  \"unityVersion\": \"{Application.unityVersion}\",\n"
                    + $"  \"result\": \"{summary.result}\",\n"
                    + $"  \"errors\": {summary.totalErrors},\n"
                    + $"  \"warnings\": {summary.totalWarnings},\n"
                    + $"  \"bytes\": {executable.Length}\n"
                    + "}\n");
                Debug.Log(
                    $"MNG WINDOWS BUILD PASS stage={requestedStage} bytes={executable.Length} "
                    + $"warnings={summary.totalWarnings} output={executablePath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildM1EvaluationBatch()
        {
            try
            {
                MNG_ProjectBuilder.ValidateM0Assets();
                var runId = ReadArgument("-mngRunId", string.Empty);
                var movingDefense = Regex.IsMatch(
                    runId ?? string.Empty, @"^MNG_M1Moving-\d{8}-r\d{3}$");
                if (!movingDefense
                    && !Regex.IsMatch(runId ?? string.Empty, @"^MNG_M1Attack-\d{8}-r\d{3}$"))
                    throw new InvalidOperationException($"Invalid MNG M1 evaluation run ID: {runId}");

                var candidateId = ReadArgument("-mngCandidateId", runId);
                var uniformRandom = string.Equals(
                    ReadArgument("-mngRandomPolicy", "false"), "true",
                    StringComparison.OrdinalIgnoreCase);
                var candidatePattern = movingDefense
                    ? @"^MNG_M1Moving-\d{8}-r\d{3}(-step\d+)?(-diag-r\d{3})?$"
                    : @"^MNG_M1Attack-\d{8}-r\d{3}(-step\d+)?(-diag-r\d{3})?$";
                if (!Regex.IsMatch(candidateId ?? string.Empty, candidatePattern))
                    throw new InvalidOperationException(
                        $"Invalid MNG M1 evaluation candidate ID: {candidateId}");

                var suppliedModelPath = ReadArgument("-mngModelPath", string.Empty);
                if (string.IsNullOrWhiteSpace(suppliedModelPath))
                    throw new InvalidOperationException("-mngModelPath is required for MNG M1 evaluation.");
                var sourceModelPath = Path.GetFullPath(suppliedModelPath);
                if (!File.Exists(sourceModelPath))
                    throw new InvalidOperationException($"MNG M1 evaluation model is missing: {sourceModelPath}");

                var sourceModelSha256 = Sha256(sourceModelPath);
                var projectRoot = Path.GetDirectoryName(Application.dataPath)
                    ?? throw new InvalidOperationException("Could not resolve the Unity project root.");
                var modelAssetPath = $"{EvaluationModelDirectory}/{candidateId}.onnx";
                var importedModelPath = Path.Combine(
                    projectRoot,
                    modelAssetPath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(importedModelPath)
                    ?? throw new InvalidOperationException("Could not resolve the model asset directory."));
                if (File.Exists(importedModelPath))
                {
                    if (!string.Equals(Sha256(importedModelPath), sourceModelSha256,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"Frozen candidate already exists with different bytes: {modelAssetPath}");
                    }
                }
                else
                {
                    File.Copy(sourceModelPath, importedModelPath, false);
                }

                AssetDatabase.ImportAsset(
                    modelAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var modelAsset = AssetDatabase.LoadAssetAtPath<ModelAsset>(modelAssetPath);
                if (modelAsset == null)
                    throw new InvalidOperationException($"Unity failed to import ONNX ModelAsset: {modelAssetPath}");

                if (movingDefense)
                    MNG_ProjectBuilder.CreateAttackMovingEvaluationScene(
                        modelAsset, runId, sourceModelSha256);
                else
                    MNG_ProjectBuilder.CreateAttackChoiceEvaluationScene(
                        modelAsset, runId, sourceModelSha256, uniformRandom);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var evaluationScenePath = movingDefense
                    ? MNG_ProjectBuilder.AttackMovingEvaluationScenePath
                    : MNG_ProjectBuilder.AttackChoiceEvaluationScenePath;
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(evaluationScenePath);
                if (scene == null)
                    throw new InvalidOperationException(
                        $"Missing MNG M1 evaluation Scene: {evaluationScenePath}");

                var evaluationOutputRoot = movingDefense
                    ? AttackMovingEvaluationOutputRoot
                    : EvaluationOutputRoot;
                var evaluationProtocolPath = movingDefense
                    ? AttackMovingEvaluationProtocolPath
                    : EvaluationProtocolPath;
                var outputDirectory = $"{evaluationOutputRoot}/{candidateId}";
                var executableName = movingDefense
                    ? "MNG_M1MovingEvaluation.exe"
                    : "MNG_M1Evaluation.exe";
                var executablePath = $"{outputDirectory}/{executableName}";
                Directory.CreateDirectory(outputDirectory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { evaluationScenePath },
                    locationPathName = executablePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                var summary = report.summary;
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                    throw new InvalidOperationException(
                        $"MNG M1 evaluation build failed: result={summary.result}, errors={summary.totalErrors}.");

                var executable = new FileInfo(executablePath);
                var dataDirectoryName = movingDefense
                    ? "MNG_M1MovingEvaluation_Data"
                    : "MNG_M1Evaluation_Data";
                var levelDataPath = $"{outputDirectory}/{dataDirectoryName}/level0";
                if (!executable.Exists || executable.Length <= 0 || !File.Exists(levelDataPath))
                    throw new InvalidOperationException("MNG M1 evaluation Player artifacts are incomplete.");
                if (!File.Exists(evaluationProtocolPath))
                    throw new InvalidOperationException($"MNG M1 protocol is missing: {evaluationProtocolPath}");

                File.WriteAllText(
                    Path.Combine(outputDirectory, "evaluation-build-info.json"),
                    "{\n"
                    + $"  \"stage\": \"{(movingDefense ? "M1-AttackMoving-Evaluation" : "M1-AttackChoice-Evaluation")}\",\n"
                    + $"  \"runId\": \"{runId}\",\n"
                    + $"  \"candidateId\": \"{candidateId}\",\n"
                    + $"  \"policyKind\": \"{(uniformRandom ? "uniform-valid-command" : "onnx")}\",\n"
                    + $"  \"scene\": \"{evaluationScenePath}\",\n"
                    + $"  \"modelAsset\": \"{modelAssetPath}\",\n"
                    + $"  \"modelSha256\": \"{sourceModelSha256}\",\n"
                    + $"  \"protocol\": \"{evaluationProtocolPath}\",\n"
                    + $"  \"protocolSha256\": \"{Sha256(evaluationProtocolPath)}\",\n"
                    + $"  \"executableSha256\": \"{Sha256(executablePath)}\",\n"
                    + $"  \"levelDataSha256\": \"{Sha256(levelDataPath)}\",\n"
                    + $"  \"unityVersion\": \"{Application.unityVersion}\",\n"
                    + $"  \"result\": \"{summary.result}\",\n"
                    + $"  \"errors\": {summary.totalErrors},\n"
                    + $"  \"warnings\": {summary.totalWarnings},\n"
                    + $"  \"bytes\": {executable.Length}\n"
                    + "}\n");
                Debug.Log($"MNG M1 EVALUATION BUILD PASS run={runId} candidate={candidateId} modelSha256={sourceModelSha256} "
                    + $"bytes={executable.Length} warnings={summary.totalWarnings} output={executablePath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildM2EvaluationBatch()
        {
            try
            {
                MNG_ProjectBuilder.ValidateM0Assets();
                var runId = ReadArgument("-mngRunId", string.Empty);
                if (!Regex.IsMatch(runId ?? string.Empty, @"^MNG_M2Defense-\d{8}-r\d{3}$"))
                    throw new InvalidOperationException($"Invalid MNG M2 evaluation run ID: {runId}");

                var suppliedModelPath = ReadArgument("-mngModelPath", string.Empty);
                if (string.IsNullOrWhiteSpace(suppliedModelPath))
                    throw new InvalidOperationException("-mngModelPath is required for MNG M2 evaluation.");
                var sourceModelPath = Path.GetFullPath(suppliedModelPath);
                if (!File.Exists(sourceModelPath))
                    throw new InvalidOperationException($"MNG M2 evaluation model is missing: {sourceModelPath}");

                var sourceModelSha256 = Sha256(sourceModelPath);
                var projectRoot = Path.GetDirectoryName(Application.dataPath)
                    ?? throw new InvalidOperationException("Could not resolve the Unity project root.");
                var modelAssetPath = $"{EvaluationModelDirectory}/{runId}.onnx";
                var importedModelPath = Path.Combine(
                    projectRoot,
                    modelAssetPath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(importedModelPath)
                    ?? throw new InvalidOperationException("Could not resolve the model asset directory."));
                if (File.Exists(importedModelPath))
                {
                    if (!string.Equals(Sha256(importedModelPath), sourceModelSha256,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException(
                            $"Frozen candidate already exists with different bytes: {modelAssetPath}");
                    }
                }
                else
                {
                    File.Copy(sourceModelPath, importedModelPath, false);
                }

                AssetDatabase.ImportAsset(
                    modelAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var modelAsset = AssetDatabase.LoadAssetAtPath<ModelAsset>(modelAssetPath);
                if (modelAsset == null)
                    throw new InvalidOperationException($"Unity failed to import ONNX ModelAsset: {modelAssetPath}");

                MNG_ProjectBuilder.CreateDefenseChoiceEvaluationScene(
                    modelAsset,
                    runId,
                    sourceModelSha256);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(
                        MNG_ProjectBuilder.DefenseChoiceEvaluationScenePath) == null)
                {
                    throw new InvalidOperationException(
                        $"Missing MNG M2 evaluation Scene: {MNG_ProjectBuilder.DefenseChoiceEvaluationScenePath}");
                }

                var outputDirectory = $"{DefenseEvaluationOutputRoot}/{runId}";
                var executablePath = $"{outputDirectory}/MNG_M2Evaluation.exe";
                Directory.CreateDirectory(outputDirectory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MNG_ProjectBuilder.DefenseChoiceEvaluationScenePath },
                    locationPathName = executablePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                var summary = report.summary;
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                    throw new InvalidOperationException(
                        $"MNG M2 evaluation build failed: result={summary.result}, errors={summary.totalErrors}.");

                var executable = new FileInfo(executablePath);
                var levelDataPath = $"{outputDirectory}/MNG_M2Evaluation_Data/level0";
                if (!executable.Exists || executable.Length <= 0 || !File.Exists(levelDataPath))
                    throw new InvalidOperationException("MNG M2 evaluation Player artifacts are incomplete.");
                if (!File.Exists(DefenseEvaluationProtocolPath))
                    throw new InvalidOperationException(
                        $"MNG M2 protocol is missing: {DefenseEvaluationProtocolPath}");

                File.WriteAllText(
                    Path.Combine(outputDirectory, "evaluation-build-info.json"),
                    "{\n"
                    + $"  \"stage\": \"M2-DefenseChoice-Evaluation\",\n"
                    + $"  \"runId\": \"{runId}\",\n"
                    + $"  \"scene\": \"{MNG_ProjectBuilder.DefenseChoiceEvaluationScenePath}\",\n"
                    + $"  \"modelAsset\": \"{modelAssetPath}\",\n"
                    + $"  \"modelSha256\": \"{sourceModelSha256}\",\n"
                    + $"  \"protocol\": \"{DefenseEvaluationProtocolPath}\",\n"
                    + $"  \"protocolSha256\": \"{Sha256(DefenseEvaluationProtocolPath)}\",\n"
                    + $"  \"executableSha256\": \"{Sha256(executablePath)}\",\n"
                    + $"  \"levelDataSha256\": \"{Sha256(levelDataPath)}\",\n"
                    + $"  \"unityVersion\": \"{Application.unityVersion}\",\n"
                    + $"  \"result\": \"{summary.result}\",\n"
                    + $"  \"errors\": {summary.totalErrors},\n"
                    + $"  \"warnings\": {summary.totalWarnings},\n"
                    + $"  \"bytes\": {executable.Length}\n"
                    + "}\n");
                Debug.Log($"MNG M2 EVALUATION BUILD PASS run={runId} modelSha256={sourceModelSha256} "
                    + $"bytes={executable.Length} warnings={summary.totalWarnings} output={executablePath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildM3EvaluationBatch()
        {
            try
            {
                MNG_ProjectBuilder.ValidateM0Assets();
                var runId = ReadArgument("-mngRunId", string.Empty);
                if (!Regex.IsMatch(runId ?? string.Empty, @"^MNG_M3Fallback-\d{8}-r\d{3}$"))
                    throw new InvalidOperationException($"Invalid MNG M3 evaluation run ID: {runId}");

                var suppliedModelPath = ReadArgument("-mngModelPath", string.Empty);
                if (string.IsNullOrWhiteSpace(suppliedModelPath))
                    throw new InvalidOperationException("-mngModelPath is required for MNG M3 evaluation.");
                var sourceModelPath = Path.GetFullPath(suppliedModelPath);
                if (!File.Exists(sourceModelPath))
                    throw new InvalidOperationException($"MNG M3 evaluation model is missing: {sourceModelPath}");

                var sourceModelSha256 = Sha256(sourceModelPath);
                var projectRoot = Path.GetDirectoryName(Application.dataPath)
                    ?? throw new InvalidOperationException("Could not resolve the Unity project root.");
                var modelAssetPath = $"{EvaluationModelDirectory}/{runId}.onnx";
                var importedModelPath = Path.Combine(
                    projectRoot,
                    modelAssetPath.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(importedModelPath)
                    ?? throw new InvalidOperationException("Could not resolve the model asset directory."));
                if (File.Exists(importedModelPath))
                {
                    if (!string.Equals(Sha256(importedModelPath), sourceModelSha256,
                            StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            $"Frozen candidate already exists with different bytes: {modelAssetPath}");
                }
                else
                {
                    File.Copy(sourceModelPath, importedModelPath, false);
                }

                AssetDatabase.ImportAsset(
                    modelAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var modelAsset = AssetDatabase.LoadAssetAtPath<ModelAsset>(modelAssetPath);
                if (modelAsset == null)
                    throw new InvalidOperationException($"Unity failed to import ONNX ModelAsset: {modelAssetPath}");

                MNG_ProjectBuilder.CreateFallbackMatchEvaluationScene(
                    modelAsset, runId, sourceModelSha256);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var outputDirectory = $"{FallbackMatchEvaluationOutputRoot}/{runId}";
                var executablePath = $"{outputDirectory}/MNG_M3Evaluation.exe";
                Directory.CreateDirectory(outputDirectory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MNG_ProjectBuilder.FallbackMatchEvaluationScenePath },
                    locationPathName = executablePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                var summary = report.summary;
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                    throw new InvalidOperationException(
                        $"MNG M3 evaluation build failed: result={summary.result}, errors={summary.totalErrors}.");

                var executable = new FileInfo(executablePath);
                var levelDataPath = $"{outputDirectory}/MNG_M3Evaluation_Data/level0";
                if (!executable.Exists || executable.Length <= 0 || !File.Exists(levelDataPath))
                    throw new InvalidOperationException("MNG M3 evaluation Player artifacts are incomplete.");
                if (!File.Exists(FallbackMatchEvaluationProtocolPath))
                    throw new InvalidOperationException(
                        $"MNG M3 protocol is missing: {FallbackMatchEvaluationProtocolPath}");

                File.WriteAllText(
                    Path.Combine(outputDirectory, "evaluation-build-info.json"),
                    "{\n"
                    + $"  \"stage\": \"M3-FallbackMatch-Evaluation\",\n"
                    + $"  \"runId\": \"{runId}\",\n"
                    + $"  \"scene\": \"{MNG_ProjectBuilder.FallbackMatchEvaluationScenePath}\",\n"
                    + $"  \"modelAsset\": \"{modelAssetPath}\",\n"
                    + $"  \"modelSha256\": \"{sourceModelSha256}\",\n"
                    + $"  \"protocol\": \"{FallbackMatchEvaluationProtocolPath}\",\n"
                    + $"  \"protocolSha256\": \"{Sha256(FallbackMatchEvaluationProtocolPath)}\",\n"
                    + $"  \"executableSha256\": \"{Sha256(executablePath)}\",\n"
                    + $"  \"levelDataSha256\": \"{Sha256(levelDataPath)}\",\n"
                    + $"  \"unityVersion\": \"{Application.unityVersion}\",\n"
                    + $"  \"result\": \"{summary.result}\",\n"
                    + $"  \"errors\": {summary.totalErrors},\n"
                    + $"  \"warnings\": {summary.totalWarnings},\n"
                    + $"  \"bytes\": {executable.Length}\n"
                    + "}\n");
                Debug.Log($"MNG M3 EVALUATION BUILD PASS run={runId} modelSha256={sourceModelSha256} "
                    + $"bytes={executable.Length} warnings={summary.totalWarnings} output={executablePath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        static string Sha256(string path)
        {
            using var stream = File.OpenRead(path);
            using var algorithm = SHA256.Create();
            return BitConverter.ToString(algorithm.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
        }

        static string ReadArgument(string name, string fallback)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            }
            return fallback;
        }
    }
}
