using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MachineLearning.Soccer;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MachineLearning.Soccer.Manager.Editor
{
    public static class MNG_MSBuilder
    {
        public const string CurriculumRoot =
            "Assets/_Soccer/Manager/Curriculum/MS_ManagerSimple";
        public const string SceneRoot = CurriculumRoot + "/Scenes";
        public const string TrainScenePath = SceneRoot + "/MNG_MS_Train.unity";
        public const string EvaluationScenePath = SceneRoot + "/MNG_MS_Evaluation.unity";
        public const string SelfPlayScenePath = SceneRoot + "/MNG_MS_SelfPlay.unity";
        public const string MS1TrainScenePath = SceneRoot + "/MNG_MS1_Train.unity";
        public const string MS1EvaluationScenePath = SceneRoot + "/MNG_MS1_Evaluation.unity";
        public const string MS1ReviewScenePath = SceneRoot + "/MNG_MS1_Review.unity";
        public const string MS2PreflightEvaluationScenePath =
            SceneRoot + "/MNG_MS2_Preflight_Evaluation.unity";
        public const string MS2TrainScenePath = SceneRoot + "/MNG_MS2_Train.unity";
        public const string MS2FinalReviewScenePath = SceneRoot + "/MNG_MS2_Final_Review.unity";
        public const string MS3TrainScenePath = SceneRoot + "/MNG_MS3_Train.unity";
        public const string MS3FinalReviewScenePath = SceneRoot + "/MNG_MS3_Final_Review.unity";
        public const string MS3CheckpointDuelScenePath =
            SceneRoot + "/MNG_MS3_Checkpoint_Duel.unity";
        public const string ProfileRoot = "Assets/_Soccer/Manager/Profiles/MS";
        public const string RescueProfilePath = ProfileRoot + "/MNG_MS_R0_Rescue.asset";
        public const string EasyProfilePath = ProfileRoot + "/MNG_MS_R0_Easy.asset";
        public const string MediumProfilePath = ProfileRoot + "/MNG_MS_R0_Medium.asset";
        public const string FullProfilePath = ProfileRoot + "/MNG_MS_R0_Full.asset";
        public const string R0SmokeConfigPath =
            "Assets/_Soccer/Manager/Training/MNG_MS0_R0Smoke.yaml";
        public const string ParallelSmokeConfigPath =
            "Assets/_Soccer/Manager/Training/MNG_MS0_ParallelSmoke.yaml";
        public const string SelfPlaySmokeConfigPath =
            "Assets/_Soccer/Manager/Training/MNG_MS0_SelfPlaySmoke.yaml";
        public const string ProtocolPath =
            "Assets/_Soccer/Manager/Evaluation/MNG_MS0_Protocol_v1.json";
        public const string MS1ConfigPath =
            "Assets/_Soccer/Manager/Training/MNG_MS1.yaml";
        public const string MS1PassRepairConfigPath =
            "Assets/_Soccer/Manager/Training/MNG_MS1_PassRepair.yaml";
        public const string MS1PassFineTuneConfigPath =
            "Assets/_Soccer/Manager/Training/MNG_MS1_PassFineTune.yaml";
        public const string MS1PassPolishConfigPath =
            "Assets/_Soccer/Manager/Training/MNG_MS1_PassPolish.yaml";
        public const string MS1PassContinuationConfigPath =
            "Assets/_Soccer/Manager/Training/MNG_MS1_PassContinuation.yaml";
        public const string MS1PassDecisionPolishConfigPath =
            "Assets/_Soccer/Manager/Training/MNG_MS1_PassDecisionPolish.yaml";
        public const string MS1ProtocolPath =
            "Assets/_Soccer/Manager/Evaluation/MNG_MS1_Protocol_v8.json";
        public const string MS2PreflightProtocolPath =
            "Assets/_Soccer/Manager/Evaluation/MNG_MS2_Preflight_Protocol_v1.json";
        public const string MS2ProtocolPath =
            "Assets/_Soccer/Manager/Evaluation/MNG_MS2_Protocol_v1.json";
        public const string MS2ConfigPath =
            "Assets/_Soccer/Manager/Training/MNG_MS2.yaml";
        public const string MS3ConfigPath =
            "Assets/_Soccer/Manager/Training/MNG_MS3.yaml";
        public const string MS3ProtocolPath =
            "Assets/_Soccer/Manager/Evaluation/MNG_MS3_Protocol_v1.json";
        public const string MS3CheckpointDuelProtocolPath =
            "Assets/_Soccer/Manager/Evaluation/MNG_MS3_CheckpointDuel_Protocol_v1.json";
        public const string R0BuildDirectory = "Builds/MNG_MS/MS0_R0";
        public const string R0ExecutablePath = R0BuildDirectory + "/MNG_MS0_R0.exe";
        public const string SelfPlayBuildDirectory = "Builds/MNG_MS/MS0_SelfPlay";
        public const string SelfPlayExecutablePath =
            SelfPlayBuildDirectory + "/MNG_MS0_SelfPlay.exe";
        public const string MS1BuildDirectory = "Builds/MNG_MS/MS1";
        public const string MS1ExecutablePath = MS1BuildDirectory + "/MNG_MS1.exe";
        public const string MS1EvaluationBuildRoot = "Builds/MNG_MS/MS1-Evaluation";
        public const string MS1ReviewBuildDirectory = "Builds/MNG_MS/MS1-Review";
        public const string MS1ReviewExecutablePath =
            MS1ReviewBuildDirectory + "/MNG_MS1_Review.exe";
        public const string MS1EvaluationModelRoot =
            "Assets/_Soccer/Manager/EvaluationModels/MS1";
        public const string MS2EvaluationModelRoot =
            "Assets/_Soccer/Manager/EvaluationModels/MS2";
        public const string MS3EvaluationModelRoot =
            "Assets/_Soccer/Manager/EvaluationModels/MS3";
        public const string MS3CheckpointDuelModelRoot =
            MS3EvaluationModelRoot + "/Duels";
        public const string MS2PreflightBuildRoot = "Builds/MNG_MS/MS2-Preflight";
        public const string MS2BuildDirectory = "Builds/MNG_MS/MS2";
        public const string MS2ExecutablePath = MS2BuildDirectory + "/MNG_MS2.exe";
        public const string MS2FinalReviewBuildDirectory = "Builds/MNG_MS/MS2-Final-Review";
        public const string MS2FinalReviewExecutablePath =
            MS2FinalReviewBuildDirectory + "/MNG_MS2_Final_Review.exe";
        public const string MS3BuildDirectory = "Builds/MNG_MS/MS3";
        public const string MS3ExecutablePath = MS3BuildDirectory + "/MNG_MS3.exe";
        public const string MS3FinalReviewBuildDirectory = "Builds/MNG_MS/MS3-Final-Review";
        public const string MS3FinalReviewExecutablePath =
            MS3FinalReviewBuildDirectory + "/MNG_MS3_Final_Review.exe";
        public const string MS3CheckpointDuelBuildRoot =
            "Builds/MNG_MS/MS3-Checkpoint-Duels";
        public const string MS2SourceManifestPath =
            "Logs/MNG-MS/MS2-p0-source-20260921/source-sha256.json";
        public const string MS2InitializationPath =
            "results/MNG_MS1-20260920-r007/MNG_Manager/MNG_Manager-7443.pt";
        public const string MS2InitializationSha256 =
            "ac8eb65dd7c633a6cb5e4dfa267daa92d63c8df5bfecb4031a4fe28c5cb4d3c3";
        public const string MS3InitializationPath =
            "results/MNG_MS2-20260921-r005/MNG_Manager/MNG_Manager-99968.pt";
        public const string MS3InitializationSha256 =
            "9ab8536762c0838960ec807fc943cc6e1cb4d166c4324db2d0406bb88ee3c0ca";
        public const string MS3SourceManifestPath =
            "Logs/MNG-MS/MS3-source-20260921/source-sha256.json";
        public const string MS1FinalSourceManifestPath =
            "Logs/MNG-MS/MS1-final-source-v19/source-sha256.json";

        [MenuItem("Tools/Soccer Manager/MS/Build MS3 Assets")]
        public static void BuildMS3Assets()
        {
            EnsureFolders();
            CreateSelfPlayScene(
                MS3TrainScenePath,
                MNG_MatchController.MatchDurationSeconds,
                MNG_MSController.DefaultTimeScale);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateMS3Assets();
            Debug.Log("MNG MS3 ASSET BUILD PASS");
        }

        public static void BuildMS3AssetsBatch()
        {
            try
            {
                BuildMS3Assets();
                Debug.Log("MNG MS3 ASSET BUILD BATCH PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/Soccer Manager/MS/Validate MS3 Assets")]
        public static void ValidateMS3Assets()
        {
            Require(File.Exists(MS3ConfigPath), $"Missing MS3 config: {MS3ConfigPath}");
            Require(File.Exists(MS3ProtocolPath), $"Missing MS3 protocol: {MS3ProtocolPath}");
            Require(File.Exists(MS3InitializationPath),
                $"Missing selected MS2 P0 initialization: {MS3InitializationPath}");
            Require(string.Equals(Sha256(MS3InitializationPath), MS3InitializationSha256,
                    StringComparison.OrdinalIgnoreCase),
                "Selected MS2 P0 initialization hash changed.");
            ValidateSelfPlayScene(
                MS3TrainScenePath,
                MNG_MatchController.MatchDurationSeconds,
                MNG_MSController.DefaultTimeScale);
            Debug.Log("MNG MS3 VALIDATION PASS");
        }

        public static void BuildMS3WindowsBatch()
        {
            try
            {
                ValidateMS3Assets();
                Require(File.Exists(MS3SourceManifestPath),
                    $"Missing MS3 source manifest: {MS3SourceManifestPath}");
                Directory.CreateDirectory(MS3BuildDirectory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MS3TrainScenePath },
                    locationPathName = MS3ExecutablePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                var summary = report.summary;
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                    throw new InvalidOperationException(
                        $"MS3 Windows build failed: result={summary.result}, errors={summary.totalErrors}.");
                var levelPath = MS3BuildDirectory + "/MNG_MS3_Data/level0";
                var managedAssemblyPath = MS3BuildDirectory
                    + "/MNG_MS3_Data/Managed/MNG.Runtime.dll";
                Require(File.Exists(MS3ExecutablePath) && File.Exists(levelPath)
                    && File.Exists(managedAssemblyPath),
                    "MS3 Windows Player artifacts are incomplete.");
                File.WriteAllText(
                    MS3BuildDirectory + "/build-info.json",
                    "{\n"
                    + "  \"stage\": \"MS3\",\n"
                    + $"  \"scene\": \"{MS3TrainScenePath}\",\n"
                    + $"  \"configSha256\": \"{Sha256(MS3ConfigPath)}\",\n"
                    + $"  \"protocolSha256\": \"{Sha256(MS3ProtocolPath)}\",\n"
                    + $"  \"sourceManifestSha256\": \"{Sha256(MS3SourceManifestPath)}\",\n"
                    + $"  \"initializationPath\": \"{MS3InitializationPath}\",\n"
                    + $"  \"initializationSha256\": \"{Sha256(MS3InitializationPath)}\",\n"
                    + "  \"initializationMode\": \"policy-weights-new-optimizer\",\n"
                    + "  \"policyAssistMode\": \"None\",\n"
                    + "  \"workers\": 32,\n"
                    + "  \"minimumAggregateSteps\": 300000,\n"
                    + "  \"maximumAggregateSteps\": 1000000,\n"
                    + "  \"monitorInterval\": 50000,\n"
                    + "  \"evaluationInterval\": 100000,\n"
                    + $"  \"executableSha256\": \"{Sha256(MS3ExecutablePath)}\",\n"
                    + $"  \"levelDataSha256\": \"{Sha256(levelPath)}\",\n"
                    + $"  \"managedAssemblySha256\": \"{Sha256(managedAssemblyPath)}\",\n"
                    + $"  \"unityVersion\": \"{Application.unityVersion}\",\n"
                    + $"  \"result\": \"{summary.result}\",\n"
                    + $"  \"errors\": {summary.totalErrors},\n"
                    + $"  \"warnings\": {summary.totalWarnings}\n"
                    + "}\n");
                Debug.Log($"MNG MS3 WINDOWS BUILD PASS output={MS3ExecutablePath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildMS3FinalReviewBatch()
        {
            try
            {
                var runId = ReadArgument("-mngRunId", string.Empty);
                var candidateId = ReadArgument("-mngCandidateId", runId);
                var suppliedModel = ReadArgument("-mngModelPath", string.Empty);
                if (string.IsNullOrWhiteSpace(runId)
                    || !runId.StartsWith("MNG_MS3-", StringComparison.Ordinal))
                    throw new InvalidOperationException($"Invalid MS3 final Run ID: {runId}");
                if (string.IsNullOrWhiteSpace(candidateId)
                    || candidateId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new InvalidOperationException($"Invalid MS3 final candidate ID: {candidateId}");
                if (string.IsNullOrWhiteSpace(suppliedModel) || !File.Exists(suppliedModel))
                    throw new InvalidOperationException($"Missing MS3 final model: {suppliedModel}");
                Require(File.Exists(MS3ProtocolPath), $"Missing MS3 protocol: {MS3ProtocolPath}");

                EnsureFolders();
                Directory.CreateDirectory(MS3EvaluationModelRoot);
                var sourceModel = Path.GetFullPath(suppliedModel);
                var modelSha = Sha256(sourceModel);
                var modelAssetPath = $"{MS3EvaluationModelRoot}/{candidateId}.onnx";
                var projectRoot = Path.GetDirectoryName(Application.dataPath)
                    ?? throw new InvalidOperationException("Could not resolve project root.");
                var importedModel = Path.Combine(
                    projectRoot,
                    modelAssetPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(importedModel))
                {
                    Require(string.Equals(Sha256(importedModel), modelSha,
                            StringComparison.OrdinalIgnoreCase),
                        $"Frozen MS3 final candidate exists with different bytes: {modelAssetPath}");
                }
                else File.Copy(sourceModel, importedModel, false);
                AssetDatabase.ImportAsset(modelAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(modelAssetPath);
                Require(model != null, $"Unity failed to import MS3 final ONNX: {modelAssetPath}");

                CreateMS3FinalReviewScene(model);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                ValidateMS3FinalReviewScene(model);

                Directory.CreateDirectory(MS3FinalReviewBuildDirectory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MS3FinalReviewScenePath },
                    locationPathName = MS3FinalReviewExecutablePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                var summary = report.summary;
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                    throw new InvalidOperationException(
                        $"MS3 final review build failed: result={summary.result}, "
                        + $"errors={summary.totalErrors}.");

                var levelPath = MS3FinalReviewBuildDirectory
                    + "/MNG_MS3_Final_Review_Data/level0";
                var managedAssemblyPath = MS3FinalReviewBuildDirectory
                    + "/MNG_MS3_Final_Review_Data/Managed/MNG.Runtime.dll";
                Require(File.Exists(MS3FinalReviewExecutablePath)
                        && File.Exists(levelPath)
                        && File.Exists(managedAssemblyPath),
                    "MS3 final review Player artifacts are incomplete.");

                var launcherPath = MS3FinalReviewBuildDirectory
                    + "/START_MS3_FINAL_REVIEW.cmd";
                File.WriteAllText(
                    launcherPath,
                    "@echo off\r\n"
                    + "setlocal\r\n"
                    + "cd /d \"%~dp0\"\r\n"
                    + "if not exist \"ReviewLogs\" mkdir \"ReviewLogs\"\r\n"
                    + "start \"MS3 Final Review\" \"%~dp0MNG_MS3_Final_Review.exe\" "
                    + "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 "
                    + "-mngPolicyAssistMode none "
                    + "-mngEvidenceDir \"%~dp0ReviewLogs\" "
                    + "-logFile \"%~dp0ReviewLogs\\player.log\"\r\n"
                    + "endlocal\r\n");
                File.WriteAllText(
                    MS3FinalReviewBuildDirectory + "/README.txt",
                    "MS3 FINAL REVIEW\n"
                    + "1. START_MS3_FINAL_REVIEW.cmd를 더블클릭합니다.\n"
                    + $"2. Red와 Navy 모두 {candidateId} PPO 정책을 사용합니다.\n"
                    + "3. 300초 경기, 1배속이며 경기는 계속 반복됩니다.\n"
                    + "4. 종료는 게임 창을 닫거나 Alt+F4를 누릅니다.\n"
                    + "5. 양 팀 모두 policy assist mode none이며 PPO action을 사후 교체하지 않습니다.\n");

                const string managerAgentPath =
                    "Assets/_Soccer/Manager/Runtime/MNG_ManagerAgent.cs";
                const string msControllerPath =
                    "Assets/_Soccer/Manager/Runtime/MNG_MSController.cs";
                File.WriteAllText(
                    MS3FinalReviewBuildDirectory + "/review-build-info.json",
                    "{\n"
                    + "  \"stage\": \"MS3-Final-Review\",\n"
                    + $"  \"runId\": \"{runId}\",\n"
                    + $"  \"candidateId\": \"{candidateId}\",\n"
                    + $"  \"scene\": \"{MS3FinalReviewScenePath}\",\n"
                    + $"  \"modelSha256\": \"{modelSha}\",\n"
                    + $"  \"protocolSha256\": \"{Sha256(MS3ProtocolPath)}\",\n"
                    + $"  \"managerAgentSha256\": \"{Sha256(managerAgentPath)}\",\n"
                    + $"  \"msControllerSha256\": \"{Sha256(msControllerPath)}\",\n"
                    + $"  \"executableSha256\": \"{Sha256(MS3FinalReviewExecutablePath)}\",\n"
                    + $"  \"levelDataSha256\": \"{Sha256(levelPath)}\",\n"
                    + $"  \"managedAssemblySha256\": \"{Sha256(managedAssemblyPath)}\",\n"
                    + $"  \"launcherSha256\": \"{Sha256(launcherPath)}\",\n"
                    + "  \"redPolicy\": \"PPO\",\n"
                    + "  \"navyPolicy\": \"PPO\",\n"
                    + "  \"policyAssistMode\": \"None\",\n"
                    + "  \"rawActionEqualsEffectiveAction\": true,\n"
                    + "  \"directBlockedPassDecisionRewardEnabled\": false,\n"
                    + "  \"matchSeconds\": 300,\n"
                    + "  \"timeScale\": 1,\n"
                    + "  \"autoQuit\": false,\n"
                    + $"  \"result\": \"{summary.result}\",\n"
                    + $"  \"errors\": {summary.totalErrors},\n"
                    + $"  \"warnings\": {summary.totalWarnings}\n"
                    + "}\n");
                Debug.Log(
                    $"MNG MS3 FINAL REVIEW BUILD PASS candidate={candidateId} "
                    + $"output={MS3FinalReviewExecutablePath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildMS3CheckpointDuelBatch()
        {
            try
            {
                var duelId = ReadArgument("-mngDuelId", string.Empty);
                var earlierCandidateId = ReadArgument("-mngEarlierCandidateId", string.Empty);
                var earlierModelPath = ReadArgument("-mngEarlierModelPath", string.Empty);
                var finalCandidateId = ReadArgument("-mngFinalCandidateId", string.Empty);
                var finalModelPath = ReadArgument("-mngFinalModelPath", string.Empty);
                RequireSafeFileName(duelId, "MS3 checkpoint duel ID");
                RequireSafeFileName(earlierCandidateId, "MS3 earlier candidate ID");
                RequireSafeFileName(finalCandidateId, "MS3 final candidate ID");
                Require(!string.Equals(
                        earlierCandidateId, finalCandidateId, StringComparison.OrdinalIgnoreCase),
                    "MS3 checkpoint duel candidates must be distinct.");
                Require(File.Exists(MS3CheckpointDuelProtocolPath),
                    $"Missing MS3 checkpoint duel protocol: {MS3CheckpointDuelProtocolPath}");

                EnsureFolders();
                Directory.CreateDirectory(MS3CheckpointDuelModelRoot);
                AssetDatabase.Refresh();
                var earlierModel = ImportFrozenModel(
                    earlierModelPath,
                    $"{MS3CheckpointDuelModelRoot}/{earlierCandidateId}.onnx",
                    out var earlierModelSha);
                var finalModel = ImportFrozenModel(
                    finalModelPath,
                    $"{MS3CheckpointDuelModelRoot}/{finalCandidateId}.onnx",
                    out var finalModelSha);
                Require(!string.Equals(
                        earlierModelSha, finalModelSha, StringComparison.OrdinalIgnoreCase),
                    "MS3 checkpoint duel model bytes must be distinct.");

                CreateMS3CheckpointDuelScene(
                    earlierModel,
                    finalModel,
                    duelId,
                    earlierCandidateId,
                    earlierModelSha,
                    finalCandidateId,
                    finalModelSha);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                ValidateMS3CheckpointDuelScene(earlierModel, finalModel, duelId);

                var buildDirectory = $"{MS3CheckpointDuelBuildRoot}/{duelId}";
                var executablePath = buildDirectory + "/MNG_MS3_Checkpoint_Duel.exe";
                Directory.CreateDirectory(buildDirectory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MS3CheckpointDuelScenePath },
                    locationPathName = executablePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                var summary = report.summary;
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                    throw new InvalidOperationException(
                        $"MS3 checkpoint duel build failed: result={summary.result}, "
                        + $"errors={summary.totalErrors}.");

                var levelPath = buildDirectory + "/MNG_MS3_Checkpoint_Duel_Data/level0";
                var managedAssemblyPath = buildDirectory
                    + "/MNG_MS3_Checkpoint_Duel_Data/Managed/MNG.Runtime.dll";
                Require(File.Exists(executablePath)
                        && File.Exists(levelPath)
                        && File.Exists(managedAssemblyPath),
                    "MS3 checkpoint duel Player artifacts are incomplete.");
                const string controllerPath =
                    "Assets/_Soccer/Manager/Runtime/MNG_MS3CheckpointDuelController.cs";
                File.WriteAllText(
                    buildDirectory + "/evaluation-build-info.json",
                    "{\n"
                    + "  \"stage\": \"MS3-Checkpoint-Duel\",\n"
                    + $"  \"duelId\": \"{duelId}\",\n"
                    + $"  \"scene\": \"{MS3CheckpointDuelScenePath}\",\n"
                    + $"  \"earlierCandidateId\": \"{earlierCandidateId}\",\n"
                    + $"  \"earlierModelSha256\": \"{earlierModelSha}\",\n"
                    + $"  \"finalCandidateId\": \"{finalCandidateId}\",\n"
                    + $"  \"finalModelSha256\": \"{finalModelSha}\",\n"
                    + $"  \"protocolSha256\": \"{Sha256(MS3CheckpointDuelProtocolPath)}\",\n"
                    + $"  \"controllerSha256\": \"{Sha256(controllerPath)}\",\n"
                    + $"  \"executableSha256\": \"{Sha256(executablePath)}\",\n"
                    + $"  \"levelDataSha256\": \"{Sha256(levelPath)}\",\n"
                    + $"  \"managedAssemblySha256\": \"{Sha256(managedAssemblyPath)}\",\n"
                    + "  \"policyAssistMode\": \"None\",\n"
                    + "  \"matchSeconds\": 300,\n"
                    + "  \"timeScale\": 10,\n"
                    + $"  \"result\": \"{summary.result}\",\n"
                    + $"  \"errors\": {summary.totalErrors},\n"
                    + $"  \"warnings\": {summary.totalWarnings}\n"
                    + "}\n");
                Debug.Log(
                    $"MNG MS3 CHECKPOINT DUEL BUILD PASS duel={duelId} "
                    + $"output={executablePath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/Soccer Manager/MS/Build MS1 Assets")]
        public static void BuildMS1Assets()
        {
            EnsureFolders();
            var easy = GetOrCreateProfile(
                EasyProfilePath, MNG_MSOpponentStrength.Easy, 0.35f, 1.5f);
            easy.ValidateOrThrow();
            CreateMS1Scene(MS1TrainScenePath, easy);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateMS1Assets();
            Debug.Log("MNG MS1 ASSET BUILD PASS");
        }

        [MenuItem("Tools/Soccer Manager/MS/Build MS2 Assets")]
        public static void BuildMS2Assets()
        {
            EnsureFolders();
            var full = GetOrCreateProfile(
                FullProfilePath,
                MNG_MSOpponentStrength.Full,
                1f,
                MNG_RuleBasedManager.DecisionIntervalSeconds);
            full.ValidateOrThrow();
            CreateR0Scene(
                MS2TrainScenePath,
                full,
                MNG_MatchController.MatchDurationSeconds,
                MNG_MSController.DefaultTimeScale);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateMS2Assets();
            Debug.Log("MNG MS2 ASSET BUILD PASS");
        }

        public static void BuildMS2AssetsBatch()
        {
            try
            {
                BuildMS2Assets();
                Debug.Log("MNG MS2 ASSET BUILD BATCH PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/Soccer Manager/MS/Validate MS2 Assets")]
        public static void ValidateMS2Assets()
        {
            Require(File.Exists(MS2ConfigPath), $"Missing MS2 config: {MS2ConfigPath}");
            Require(File.Exists(MS2ProtocolPath), $"Missing MS2 protocol: {MS2ProtocolPath}");
            Require(File.Exists(MS2InitializationPath),
                $"Missing selected MS1 initialization: {MS2InitializationPath}");
            Require(string.Equals(Sha256(MS2InitializationPath), MS2InitializationSha256,
                    StringComparison.OrdinalIgnoreCase),
                "Selected MS1 initialization hash changed.");
            ValidateProfile(
                FullProfilePath,
                MNG_MSOpponentStrength.Full,
                1f,
                MNG_RuleBasedManager.DecisionIntervalSeconds);
            ValidateR0Scene(
                MS2TrainScenePath,
                MNG_MatchController.MatchDurationSeconds,
                MNG_MSController.DefaultTimeScale);
            Debug.Log("MNG MS2 VALIDATION PASS");
        }

        public static void BuildMS2WindowsBatch()
        {
            try
            {
                ValidateMS2Assets();
                Require(File.Exists(MS2SourceManifestPath),
                    $"Missing MS2 source manifest: {MS2SourceManifestPath}");
                Directory.CreateDirectory(MS2BuildDirectory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MS2TrainScenePath },
                    locationPathName = MS2ExecutablePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                var summary = report.summary;
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                    throw new InvalidOperationException(
                        $"MS2 Windows build failed: result={summary.result}, errors={summary.totalErrors}.");
                var levelPath = MS2BuildDirectory + "/MNG_MS2_Data/level0";
                var managedAssemblyPath = MS2BuildDirectory
                    + "/MNG_MS2_Data/Managed/MNG.Runtime.dll";
                Require(File.Exists(MS2ExecutablePath) && File.Exists(levelPath)
                    && File.Exists(managedAssemblyPath),
                    "MS2 Windows Player artifacts are incomplete.");
                File.WriteAllText(
                    MS2BuildDirectory + "/build-info.json",
                    "{\n"
                    + "  \"stage\": \"MS2\",\n"
                    + $"  \"scene\": \"{MS2TrainScenePath}\",\n"
                    + $"  \"configSha256\": \"{Sha256(MS2ConfigPath)}\",\n"
                    + $"  \"protocolSha256\": \"{Sha256(MS2ProtocolPath)}\",\n"
                    + $"  \"sourceManifestSha256\": \"{Sha256(MS2SourceManifestPath)}\",\n"
                    + $"  \"initializationPath\": \"{MS2InitializationPath}\",\n"
                    + $"  \"initializationSha256\": \"{Sha256(MS2InitializationPath)}\",\n"
                    + "  \"initializationMode\": \"policy-weights-new-optimizer\",\n"
                    + $"  \"executableSha256\": \"{Sha256(MS2ExecutablePath)}\",\n"
                    + $"  \"levelDataSha256\": \"{Sha256(levelPath)}\",\n"
                    + $"  \"managedAssemblySha256\": \"{Sha256(managedAssemblyPath)}\",\n"
                    + $"  \"unityVersion\": \"{Application.unityVersion}\",\n"
                    + $"  \"result\": \"{summary.result}\",\n"
                    + $"  \"errors\": {summary.totalErrors},\n"
                    + $"  \"warnings\": {summary.totalWarnings}\n"
                    + "}\n");
                Debug.Log($"MNG MS2 WINDOWS BUILD PASS output={MS2ExecutablePath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildMS1AssetsBatch()
        {
            try
            {
                BuildMS1Assets();
                Debug.Log("MNG MS1 ASSET BUILD BATCH PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/Soccer Manager/MS/Validate MS1 Assets")]
        public static void ValidateMS1Assets()
        {
            Require(File.Exists(MS1ConfigPath), $"Missing MS1 config: {MS1ConfigPath}");
            Require(File.Exists(MS1PassRepairConfigPath),
                $"Missing MS1 pass-repair config: {MS1PassRepairConfigPath}");
            Require(File.Exists(MS1PassFineTuneConfigPath),
                $"Missing MS1 pass-fine-tune config: {MS1PassFineTuneConfigPath}");
            Require(File.Exists(MS1PassPolishConfigPath),
                $"Missing MS1 pass-polish config: {MS1PassPolishConfigPath}");
            Require(File.Exists(MS1PassContinuationConfigPath),
                $"Missing MS1 pass-continuation config: {MS1PassContinuationConfigPath}");
            Require(File.Exists(MS1PassDecisionPolishConfigPath),
                $"Missing MS1 pass-decision config: {MS1PassDecisionPolishConfigPath}");
            Require(File.Exists(MS1ProtocolPath), $"Missing MS1 protocol: {MS1ProtocolPath}");
            ValidateProfile(EasyProfilePath, MNG_MSOpponentStrength.Easy, 0.35f, 1.5f);
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(MS1TrainScenePath) != null,
                $"Missing MS1 training scene: {MS1TrainScenePath}");
            var scene = EditorSceneManager.OpenScene(MS1TrainScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().Single();
            var controller = root.GetComponent<MNG_MS1Controller>();
            Require(controller != null && controller.OpponentProfile != null
                && controller.OpponentProfile.Strength == MNG_MSOpponentStrength.Easy,
                "MS1 training scene requires the Easy R0 profile.");
            var managers = root.GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Where(manager => manager.gameObject.activeInHierarchy && manager.enabled).ToArray();
            var rules = root.GetComponentsInChildren<MNG_RuleBasedManager>(true)
                .Where(rule => rule.enabled).ToArray();
            Require(managers.Length == 1 && managers[0].Team == Team.Red,
                "MS1 training requires one Red PPO manager.");
            Require(rules.Length == 1 && rules[0].Team == Team.Navy,
                "MS1 training requires one Navy R0 manager.");
            Require(root.GetComponentsInChildren<MNG_FallbackManager>(true)
                    .All(fallback => !fallback.enabled),
                "MS1 cannot contain an active fallback writer.");
            Debug.Log("MNG MS1 VALIDATION PASS");
        }

        public static void BuildMS1WindowsBatch()
        {
            try
            {
                ValidateMS1Assets();
                Require(File.Exists(MS1FinalSourceManifestPath),
                    $"Missing final MS1 source manifest: {MS1FinalSourceManifestPath}");
                Directory.CreateDirectory(MS1BuildDirectory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MS1TrainScenePath },
                    locationPathName = MS1ExecutablePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                var summary = report.summary;
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                    throw new InvalidOperationException(
                        $"MS1 Windows build failed: result={summary.result}, errors={summary.totalErrors}.");
                var levelPath = MS1BuildDirectory + "/MNG_MS1_Data/level0";
                var managedAssemblyPath = MS1BuildDirectory
                    + "/MNG_MS1_Data/Managed/MNG.Runtime.dll";
                Require(File.Exists(MS1ExecutablePath) && File.Exists(levelPath)
                    && File.Exists(managedAssemblyPath),
                    "MS1 Windows Player artifacts are incomplete.");
                var sourceManifest = MS1FinalSourceManifestPath;
                File.WriteAllText(
                    MS1BuildDirectory + "/build-info.json",
                    "{\n"
                    + "  \"stage\": \"MS1\",\n"
                    + $"  \"scene\": \"{MS1TrainScenePath}\",\n"
                    + $"  \"configSha256\": \"{Sha256(MS1ConfigPath)}\",\n"
                    + $"  \"passRepairConfigSha256\": \"{Sha256(MS1PassRepairConfigPath)}\",\n"
                    + $"  \"passFineTuneConfigSha256\": \"{Sha256(MS1PassFineTuneConfigPath)}\",\n"
                    + $"  \"passPolishConfigSha256\": \"{Sha256(MS1PassPolishConfigPath)}\",\n"
                    + $"  \"passContinuationConfigSha256\": \"{Sha256(MS1PassContinuationConfigPath)}\",\n"
                    + $"  \"passDecisionPolishConfigSha256\": \"{Sha256(MS1PassDecisionPolishConfigPath)}\",\n"
                    + $"  \"protocolSha256\": \"{Sha256(MS1ProtocolPath)}\",\n"
                    + $"  \"sourceManifestSha256\": \"{(File.Exists(sourceManifest) ? Sha256(sourceManifest) : string.Empty)}\",\n"
                    + $"  \"executableSha256\": \"{Sha256(MS1ExecutablePath)}\",\n"
                    + $"  \"levelDataSha256\": \"{Sha256(levelPath)}\",\n"
                    + $"  \"managedAssemblySha256\": \"{Sha256(managedAssemblyPath)}\",\n"
                    + $"  \"unityVersion\": \"{Application.unityVersion}\",\n"
                    + $"  \"result\": \"{summary.result}\",\n"
                    + $"  \"errors\": {summary.totalErrors},\n"
                    + $"  \"warnings\": {summary.totalWarnings}\n"
                    + "}\n");
                Debug.Log($"MNG MS1 WINDOWS BUILD PASS output={MS1ExecutablePath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildMS1EvaluationBatch()
        {
            try
            {
                var runId = ReadArgument("-mngRunId", string.Empty);
                var candidateId = ReadArgument("-mngCandidateId", runId);
                var suppliedModel = ReadArgument("-mngModelPath", string.Empty);
                if (string.IsNullOrWhiteSpace(runId) || !runId.StartsWith("MNG_MS1-", StringComparison.Ordinal))
                    throw new InvalidOperationException($"Invalid MS1 evaluation run ID: {runId}");
                if (string.IsNullOrWhiteSpace(candidateId)
                    || candidateId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new InvalidOperationException($"Invalid MS1 candidate ID: {candidateId}");
                if (string.IsNullOrWhiteSpace(suppliedModel) || !File.Exists(suppliedModel))
                    throw new InvalidOperationException($"Missing MS1 evaluation model: {suppliedModel}");

                EnsureFolders();
                Directory.CreateDirectory(MS1EvaluationModelRoot);
                var sourceModel = Path.GetFullPath(suppliedModel);
                var modelSha = Sha256(sourceModel);
                var modelAssetPath = $"{MS1EvaluationModelRoot}/{candidateId}.onnx";
                var projectRoot = Path.GetDirectoryName(Application.dataPath)
                    ?? throw new InvalidOperationException("Could not resolve project root.");
                var importedModel = Path.Combine(projectRoot,
                    modelAssetPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(importedModel))
                {
                    if (!string.Equals(Sha256(importedModel), modelSha, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            $"Frozen MS1 candidate exists with different bytes: {modelAssetPath}");
                }
                else File.Copy(sourceModel, importedModel, false);
                AssetDatabase.ImportAsset(modelAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(modelAssetPath);
                Require(model != null, $"Unity failed to import MS1 ONNX: {modelAssetPath}");

                var easy = GetOrCreateProfile(
                    EasyProfilePath, MNG_MSOpponentStrength.Easy, 0.35f, 1.5f);
                CreateMS1EvaluationScene(model, easy, runId, candidateId, modelSha);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var outputDirectory = $"{MS1EvaluationBuildRoot}/{candidateId}";
                var executablePath = $"{outputDirectory}/MNG_MS1_Evaluation.exe";
                Directory.CreateDirectory(outputDirectory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MS1EvaluationScenePath },
                    locationPathName = executablePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                var summary = report.summary;
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                    throw new InvalidOperationException(
                        $"MS1 evaluation build failed: result={summary.result}, errors={summary.totalErrors}.");
                var levelPath = $"{outputDirectory}/MNG_MS1_Evaluation_Data/level0";
                Require(File.Exists(executablePath) && File.Exists(levelPath),
                    "MS1 evaluation Player artifacts are incomplete.");
                File.WriteAllText($"{outputDirectory}/evaluation-build-info.json",
                    "{\n"
                    + $"  \"stage\": \"MS1-Evaluation\",\n"
                    + $"  \"runId\": \"{runId}\",\n"
                    + $"  \"candidateId\": \"{candidateId}\",\n"
                    + $"  \"modelSha256\": \"{modelSha}\",\n"
                    + $"  \"protocolSha256\": \"{Sha256(MS1ProtocolPath)}\",\n"
                    + $"  \"executableSha256\": \"{Sha256(executablePath)}\",\n"
                    + $"  \"levelDataSha256\": \"{Sha256(levelPath)}\",\n"
                    + $"  \"result\": \"{summary.result}\",\n"
                    + $"  \"errors\": {summary.totalErrors},\n"
                    + $"  \"warnings\": {summary.totalWarnings}\n"
                    + "}\n");
                Debug.Log($"MNG MS1 EVALUATION BUILD PASS candidate={candidateId} output={executablePath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildMS1ReviewAssetsBatch()
        {
            try
            {
                PrepareMS1ReviewAssets(out _, out _, out _);
                Debug.Log("MNG MS1 REVIEW ASSET BUILD PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildMS1ReviewBatch()
        {
            try
            {
                ResolveMS1ReviewArguments(
                    out var runId,
                    out var candidateId,
                    out var modelSha,
                    out var modelAssetPath,
                    out var importedModel);
                Require(File.Exists(importedModel),
                    $"Missing frozen MS1 review model: {modelAssetPath}");
                Require(string.Equals(Sha256(importedModel), modelSha,
                        StringComparison.OrdinalIgnoreCase),
                    $"Frozen MS1 review model hash mismatch: {modelAssetPath}");
                var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(modelAssetPath);
                Require(model != null,
                    $"Unity failed to load frozen MS1 review model: {modelAssetPath}");
                ValidateMS1ReviewScene(model);
                Require(File.Exists(MS1FinalSourceManifestPath),
                    $"Missing final MS1 source manifest: {MS1FinalSourceManifestPath}");
                Directory.CreateDirectory(MS1ReviewBuildDirectory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MS1ReviewScenePath },
                    locationPathName = MS1ReviewExecutablePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                var summary = report.summary;
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                    throw new InvalidOperationException(
                        $"MS1 review build failed: result={summary.result}, errors={summary.totalErrors}.");
                var levelPath = MS1ReviewBuildDirectory + "/MNG_MS1_Review_Data/level0";
                var managedAssemblyPath = MS1ReviewBuildDirectory
                    + "/MNG_MS1_Review_Data/Managed/MNG.Runtime.dll";
                Require(File.Exists(MS1ReviewExecutablePath)
                    && File.Exists(levelPath)
                    && File.Exists(managedAssemblyPath),
                    "MS1 review Player artifacts are incomplete.");
                File.WriteAllText(MS1ReviewBuildDirectory + "/review-build-info.json",
                    "{\n"
                    + "  \"stage\": \"MS1-Review\",\n"
                    + $"  \"runId\": \"{runId}\",\n"
                    + $"  \"candidateId\": \"{candidateId}\",\n"
                    + $"  \"scene\": \"{MS1ReviewScenePath}\",\n"
                    + $"  \"modelSha256\": \"{modelSha}\",\n"
                    + $"  \"protocolSha256\": \"{Sha256(MS1ProtocolPath)}\",\n"
                    + $"  \"sourceManifestSha256\": \"{Sha256(MS1FinalSourceManifestPath)}\",\n"
                    + $"  \"executableSha256\": \"{Sha256(MS1ReviewExecutablePath)}\",\n"
                    + $"  \"levelDataSha256\": \"{Sha256(levelPath)}\",\n"
                    + $"  \"managedAssemblySha256\": \"{Sha256(managedAssemblyPath)}\",\n"
                    + "  \"timeScale\": 1,\n"
                    + "  \"opponent\": \"R0-Easy\",\n"
                    + $"  \"attackSeed\": {MNG_MS1Controller.EvaluationAttackSeed},\n"
                    + $"  \"defenseSeed\": {MNG_MS1Controller.EvaluationDefenseSeed},\n"
                    + $"  \"result\": \"{summary.result}\",\n"
                    + $"  \"errors\": {summary.totalErrors},\n"
                    + $"  \"warnings\": {summary.totalWarnings}\n"
                    + "}\n");
                Debug.Log($"MNG MS1 REVIEW BUILD PASS candidate={candidateId} "
                    + $"output={MS1ReviewExecutablePath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildMS2PreflightEvaluationBatch()
        {
            try
            {
                var runId = ReadArgument("-mngRunId", string.Empty);
                var candidateId = ReadArgument("-mngCandidateId", runId);
                var suppliedModel = ReadArgument("-mngModelPath", string.Empty);
                if (string.IsNullOrWhiteSpace(runId)
                    || (!runId.StartsWith("MNG_MS1-", StringComparison.Ordinal)
                        && !runId.StartsWith("MNG_MS2-", StringComparison.Ordinal)
                        && !runId.StartsWith("MNG_MS3-", StringComparison.Ordinal)))
                    throw new InvalidOperationException($"Invalid MS2 preflight source Run ID: {runId}");
                if (string.IsNullOrWhiteSpace(candidateId)
                    || candidateId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new InvalidOperationException($"Invalid MS2 preflight candidate ID: {candidateId}");
                if (string.IsNullOrWhiteSpace(suppliedModel) || !File.Exists(suppliedModel))
                    throw new InvalidOperationException($"Missing MS2 preflight model: {suppliedModel}");
                Require(File.Exists(MS2PreflightProtocolPath),
                    $"Missing MS2 preflight protocol: {MS2PreflightProtocolPath}");

                EnsureFolders();
                Directory.CreateDirectory(MS2EvaluationModelRoot);
                var sourceModel = Path.GetFullPath(suppliedModel);
                var modelSha = Sha256(sourceModel);
                var modelAssetPath = $"{MS2EvaluationModelRoot}/{candidateId}.onnx";
                var projectRoot = Path.GetDirectoryName(Application.dataPath)
                    ?? throw new InvalidOperationException("Could not resolve project root.");
                var importedModel = Path.Combine(
                    projectRoot,
                    modelAssetPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(importedModel))
                {
                    Require(string.Equals(Sha256(importedModel), modelSha,
                            StringComparison.OrdinalIgnoreCase),
                        $"Frozen MS2 preflight candidate exists with different bytes: {modelAssetPath}");
                }
                else File.Copy(sourceModel, importedModel, false);
                AssetDatabase.ImportAsset(modelAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(modelAssetPath);
                Require(model != null, $"Unity failed to import MS2 preflight ONNX: {modelAssetPath}");

                var medium = GetOrCreateProfile(
                    MediumProfilePath, MNG_MSOpponentStrength.Medium, 0.60f, 1f);
                var full = GetOrCreateProfile(
                    FullProfilePath,
                    MNG_MSOpponentStrength.Full,
                    1f,
                    MNG_RuleBasedManager.DecisionIntervalSeconds);
                CreateMS2PreflightEvaluationScene(
                    model, medium, full, runId, candidateId, modelSha);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var outputDirectory = $"{MS2PreflightBuildRoot}/{candidateId}";
                var executablePath = $"{outputDirectory}/MNG_MS2_Preflight.exe";
                Directory.CreateDirectory(outputDirectory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MS2PreflightEvaluationScenePath },
                    locationPathName = executablePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                var summary = report.summary;
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                    throw new InvalidOperationException(
                        $"MS2 preflight build failed: result={summary.result}, errors={summary.totalErrors}.");
                var levelPath = $"{outputDirectory}/MNG_MS2_Preflight_Data/level0";
                Require(File.Exists(executablePath) && File.Exists(levelPath),
                    "MS2 preflight Player artifacts are incomplete.");
                File.WriteAllText(
                    $"{outputDirectory}/evaluation-build-info.json",
                    "{\n"
                    + "  \"stage\": \"MS2-Preflight\",\n"
                    + $"  \"runId\": \"{runId}\",\n"
                    + $"  \"candidateId\": \"{candidateId}\",\n"
                    + $"  \"modelSha256\": \"{modelSha}\",\n"
                    + $"  \"protocolSha256\": \"{Sha256(MS2PreflightProtocolPath)}\",\n"
                    + $"  \"executableSha256\": \"{Sha256(executablePath)}\",\n"
                    + $"  \"levelDataSha256\": \"{Sha256(levelPath)}\",\n"
                    + $"  \"result\": \"{summary.result}\",\n"
                    + $"  \"errors\": {summary.totalErrors},\n"
                    + $"  \"warnings\": {summary.totalWarnings}\n"
                    + "}\n");
                Debug.Log(
                    $"MNG MS2 PREFLIGHT BUILD PASS candidate={candidateId} output={executablePath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildMS2FinalReviewBatch()
        {
            try
            {
                var runId = ReadArgument("-mngRunId", string.Empty);
                var candidateId = ReadArgument("-mngCandidateId", runId);
                var suppliedModel = ReadArgument("-mngModelPath", string.Empty);
                if (string.IsNullOrWhiteSpace(runId)
                    || !runId.StartsWith("MNG_MS2-", StringComparison.Ordinal))
                    throw new InvalidOperationException($"Invalid MS2 final Run ID: {runId}");
                if (string.IsNullOrWhiteSpace(candidateId)
                    || candidateId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                    throw new InvalidOperationException($"Invalid MS2 final candidate ID: {candidateId}");
                if (string.IsNullOrWhiteSpace(suppliedModel) || !File.Exists(suppliedModel))
                    throw new InvalidOperationException($"Missing MS2 final model: {suppliedModel}");
                Require(File.Exists(MS2ProtocolPath),
                    $"Missing MS2 final protocol: {MS2ProtocolPath}");

                EnsureFolders();
                Directory.CreateDirectory(MS2EvaluationModelRoot);
                var sourceModel = Path.GetFullPath(suppliedModel);
                var modelSha = Sha256(sourceModel);
                var modelAssetPath = $"{MS2EvaluationModelRoot}/{candidateId}.onnx";
                var projectRoot = Path.GetDirectoryName(Application.dataPath)
                    ?? throw new InvalidOperationException("Could not resolve project root.");
                var importedModel = Path.Combine(
                    projectRoot,
                    modelAssetPath.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(importedModel))
                {
                    Require(string.Equals(Sha256(importedModel), modelSha,
                            StringComparison.OrdinalIgnoreCase),
                        $"Frozen MS2 final candidate exists with different bytes: {modelAssetPath}");
                }
                else File.Copy(sourceModel, importedModel, false);
                AssetDatabase.ImportAsset(modelAssetPath,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(modelAssetPath);
                Require(model != null, $"Unity failed to import MS2 final ONNX: {modelAssetPath}");

                var full = GetOrCreateProfile(
                    FullProfilePath,
                    MNG_MSOpponentStrength.Full,
                    1f,
                    MNG_RuleBasedManager.DecisionIntervalSeconds);
                CreateMS2FinalReviewScene(model, full);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                ValidateMS2FinalReviewScene(model);

                Directory.CreateDirectory(MS2FinalReviewBuildDirectory);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { MS2FinalReviewScenePath },
                    locationPathName = MS2FinalReviewExecutablePath,
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None
                });
                var summary = report.summary;
                if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                    throw new InvalidOperationException(
                        $"MS2 final review build failed: result={summary.result}, "
                        + $"errors={summary.totalErrors}.");

                var levelPath = MS2FinalReviewBuildDirectory
                    + "/MNG_MS2_Final_Review_Data/level0";
                var managedAssemblyPath = MS2FinalReviewBuildDirectory
                    + "/MNG_MS2_Final_Review_Data/Managed/MNG.Runtime.dll";
                Require(File.Exists(MS2FinalReviewExecutablePath)
                        && File.Exists(levelPath)
                        && File.Exists(managedAssemblyPath),
                    "MS2 final review Player artifacts are incomplete.");

                var launcherPath = MS2FinalReviewBuildDirectory
                    + "/START_MS2_FINAL_REVIEW.cmd";
                File.WriteAllText(
                    launcherPath,
                    "@echo off\r\n"
                    + "setlocal\r\n"
                    + "cd /d \"%~dp0\"\r\n"
                    + "if not exist \"ReviewLogs\" mkdir \"ReviewLogs\"\r\n"
                    + "start \"MS2 Final Review\" \"%~dp0MNG_MS2_Final_Review.exe\" "
                    + "-screen-fullscreen 0 -screen-width 1280 -screen-height 720 "
                    + "-mngPolicyAssistMode none "
                    + "-mngEvidenceDir \"%~dp0ReviewLogs\" "
                    + "-logFile \"%~dp0ReviewLogs\\player.log\"\r\n"
                    + "endlocal\r\n");
                var guidePath = MS2FinalReviewBuildDirectory + "/README.txt";
                const string managerAgentPath =
                    "Assets/_Soccer/Manager/Runtime/MNG_ManagerAgent.cs";
                File.WriteAllText(
                    guidePath,
                    "MS2 FINAL REVIEW\n"
                    + "1. START_MS2_FINAL_REVIEW.cmd를 더블클릭합니다.\n"
                    + $"2. Red는 {candidateId} PPO, Navy는 R0-Full입니다.\n"
                    + "3. 300초 경기, 1배속이며 경기는 계속 반복됩니다.\n"
                    + "4. 종료는 게임 창을 닫거나 Alt+F4를 누릅니다.\n"
                    + "5. policy assist mode는 none이며 PPO action을 사후 교체하지 않습니다.\n");

                File.WriteAllText(
                    MS2FinalReviewBuildDirectory + "/review-build-info.json",
                    "{\n"
                    + "  \"stage\": \"MS2-Final-Review\",\n"
                    + $"  \"runId\": \"{runId}\",\n"
                    + $"  \"candidateId\": \"{candidateId}\",\n"
                    + $"  \"scene\": \"{MS2FinalReviewScenePath}\",\n"
                    + $"  \"modelSha256\": \"{modelSha}\",\n"
                    + $"  \"protocolSha256\": \"{Sha256(MS2ProtocolPath)}\",\n"
                    + $"  \"managerAgentSha256\": \"{Sha256(managerAgentPath)}\",\n"
                    + $"  \"executableSha256\": \"{Sha256(MS2FinalReviewExecutablePath)}\",\n"
                    + $"  \"levelDataSha256\": \"{Sha256(levelPath)}\",\n"
                    + $"  \"managedAssemblySha256\": \"{Sha256(managedAssemblyPath)}\",\n"
                    + $"  \"launcherSha256\": \"{Sha256(launcherPath)}\",\n"
                    + "  \"policyTeam\": \"Red\",\n"
                    + "  \"opponent\": \"R0-Full\",\n"
                    + "  \"opponentMovementMultiplier\": 1.0,\n"
                    + $"  \"opponentDecisionIntervalSeconds\": {MNG_RuleBasedManager.DecisionIntervalSeconds:R},\n"
                    + "  \"policyAssistMode\": \"None\",\n"
                    + "  \"rawActionEqualsEffectiveAction\": true,\n"
                    + "  \"directBlockedPassDecisionRewardEnabled\": false,\n"
                    + "  \"matchSeconds\": 300,\n"
                    + "  \"timeScale\": 1,\n"
                    + "  \"autoQuit\": false,\n"
                    + $"  \"result\": \"{summary.result}\",\n"
                    + $"  \"errors\": {summary.totalErrors},\n"
                    + $"  \"warnings\": {summary.totalWarnings}\n"
                    + "}\n");
                Debug.Log(
                    $"MNG MS2 FINAL REVIEW BUILD PASS candidate={candidateId} "
                    + $"output={MS2FinalReviewExecutablePath}");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/Soccer Manager/MS/Build MS0 Assets")]
        public static void BuildMS0Assets()
        {
            EnsureFolders();
            var rescue = GetOrCreateProfile(
                RescueProfilePath, MNG_MSOpponentStrength.Rescue, 0.20f, 2f);
            var easy = GetOrCreateProfile(
                EasyProfilePath, MNG_MSOpponentStrength.Easy, 0.35f, 1.5f);
            var medium = GetOrCreateProfile(
                MediumProfilePath, MNG_MSOpponentStrength.Medium, 0.60f, 1f);
            var full = GetOrCreateProfile(
                FullProfilePath,
                MNG_MSOpponentStrength.Full,
                1f,
                MNG_RuleBasedManager.DecisionIntervalSeconds);
            rescue.ValidateOrThrow();
            easy.ValidateOrThrow();
            medium.ValidateOrThrow();
            full.ValidateOrThrow();

            CreateR0Scene(
                TrainScenePath,
                full,
                MNG_MSController.SmokeEpisodeSeconds,
                MNG_MSController.DefaultTimeScale);
            CreateR0Scene(
                EvaluationScenePath,
                full,
                MNG_MatchController.MatchDurationSeconds,
                1f);
            CreateSelfPlayScene(
                SelfPlayScenePath,
                MNG_MSController.SmokeEpisodeSeconds,
                MNG_MSController.DefaultTimeScale);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateMS0Assets();
            Debug.Log("MNG MS0 ASSET BUILD PASS");
        }

        public static void BuildMS0AssetsBatch()
        {
            try
            {
                BuildMS0Assets();
                Debug.Log("MNG MS0 ASSET BUILD BATCH PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/Soccer Manager/MS/Validate MS0 Assets")]
        public static void ValidateMS0Assets()
        {
            MNG_ProjectBuilder.ValidateM0Assets();
            ValidateProfile(RescueProfilePath, MNG_MSOpponentStrength.Rescue, 0.20f, 2f);
            ValidateProfile(EasyProfilePath, MNG_MSOpponentStrength.Easy, 0.35f, 1.5f);
            ValidateProfile(MediumProfilePath, MNG_MSOpponentStrength.Medium, 0.60f, 1f);
            ValidateProfile(
                FullProfilePath,
                MNG_MSOpponentStrength.Full,
                1f,
                MNG_RuleBasedManager.DecisionIntervalSeconds);
            Require(File.Exists(R0SmokeConfigPath), $"Missing MS0 config: {R0SmokeConfigPath}");
            Require(File.Exists(ParallelSmokeConfigPath),
                $"Missing MS0 config: {ParallelSmokeConfigPath}");
            Require(File.Exists(SelfPlaySmokeConfigPath),
                $"Missing MS0 config: {SelfPlaySmokeConfigPath}");
            Require(File.Exists(ProtocolPath), $"Missing MS0 protocol: {ProtocolPath}");
            ValidateR0Scene(TrainScenePath, MNG_MSController.SmokeEpisodeSeconds, 10f);
            ValidateR0Scene(
                EvaluationScenePath,
                MNG_MatchController.MatchDurationSeconds,
                1f);
            ValidateSelfPlayScene(
                SelfPlayScenePath,
                MNG_MSController.SmokeEpisodeSeconds,
                MNG_MSController.DefaultTimeScale);
            Debug.Log("MNG MS0 VALIDATION PASS");
        }

        public static void ValidateMS0AssetsBatch()
        {
            try
            {
                ValidateMS0Assets();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildMS0WindowsBatch()
        {
            try
            {
                var requested = ReadArgument("-mngMSProfile", "R0Smoke");
                var requestedOutput = ReadArgument("-buildOutput", string.Empty);
                BuildMS0Windows(requested, requestedOutput);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        public static void BuildMS0Windows(string requested, string requestedOutput = "")
        {
            BuildMS0Assets();
            var selfPlay = string.Equals(
                requested, "SelfPlaySmoke", StringComparison.OrdinalIgnoreCase);
            if (!selfPlay && !string.Equals(
                    requested, "R0Smoke", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException($"Unsupported MS0 build profile: {requested}");

            var scenePath = selfPlay ? SelfPlayScenePath : TrainScenePath;
            var defaultOutput = selfPlay ? SelfPlayExecutablePath : R0ExecutablePath;
            var executablePath = string.IsNullOrWhiteSpace(requestedOutput)
                ? defaultOutput
                : MakeProjectRelative(requestedOutput);
            var outputDirectory = Path.GetDirectoryName(executablePath)
                ?.Replace('\\', '/')
                ?? throw new InvalidOperationException("Could not resolve the MS0 build directory.");
            Directory.CreateDirectory(outputDirectory);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { scenePath },
                locationPathName = executablePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            var summary = report.summary;
            if (summary.result != BuildResult.Succeeded || summary.totalErrors != 0)
                throw new InvalidOperationException(
                    $"MS0 Windows build failed: result={summary.result}, "
                    + $"errors={summary.totalErrors}.");

            var executable = new FileInfo(executablePath);
            var dataDirectory = Path.Combine(
                outputDirectory,
                Path.GetFileNameWithoutExtension(executablePath) + "_Data");
            var levelDataPath = Path.Combine(dataDirectory, "level0");
            if (!executable.Exists || executable.Length <= 0 || !File.Exists(levelDataPath))
                throw new InvalidOperationException("MS0 Player artifacts are incomplete.");

            var buildInfoPath = Path.Combine(outputDirectory, "build-info.json");
            var sourceManifest = "Logs/MNG-MS/MS0-final-source-v2/source-sha256.json";
            File.WriteAllText(
                buildInfoPath,
                "{\n"
                + $"  \"stage\": \"MS0-{requested}\",\n"
                + $"  \"scene\": \"{scenePath}\",\n"
                + $"  \"r0SmokeConfigSha256\": \"{Sha256(R0SmokeConfigPath)}\",\n"
                + $"  \"parallelSmokeConfigSha256\": \"{Sha256(ParallelSmokeConfigPath)}\",\n"
                + $"  \"selfPlaySmokeConfigSha256\": \"{Sha256(SelfPlaySmokeConfigPath)}\",\n"
                + $"  \"protocolSha256\": \"{Sha256(ProtocolPath)}\",\n"
                + $"  \"sourceManifestSha256\": \""
                + $"{(File.Exists(sourceManifest) ? Sha256(sourceManifest) : string.Empty)}\",\n"
                + $"  \"executableSha256\": \"{Sha256(executablePath)}\",\n"
                + $"  \"levelDataSha256\": \"{Sha256(levelDataPath)}\",\n"
                + $"  \"unityVersion\": \"{Application.unityVersion}\",\n"
                + $"  \"result\": \"{summary.result}\",\n"
                + $"  \"errors\": {summary.totalErrors},\n"
                + $"  \"warnings\": {summary.totalWarnings},\n"
                + $"  \"bytes\": {executable.Length}\n"
                + "}\n");
            Debug.Log(
                $"MNG MS0 WINDOWS BUILD PASS profile={requested} "
                + $"bytes={executable.Length} warnings={summary.totalWarnings} "
                + $"output={executablePath}");
        }

        static void CreateR0Scene(
            string scenePath,
            MNG_MSOpponentProfile profile,
            float episodeSeconds,
            float timeScale)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = InstantiateManagerPrefab(scene, "MS R0");
            foreach (var manager in instance.GetComponentsInChildren<MNG_ManagerAgent>(true))
            {
                var active = manager.Team == Team.Red;
                manager.gameObject.SetActive(active);
                manager.enabled = active;
                manager.GetComponent<Unity.MLAgents.DecisionRequester>().enabled = active;
            }
            foreach (var fallback in instance.GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = false;
            foreach (var rule in instance.GetComponentsInChildren<MNG_RuleBasedManager>(true))
                rule.enabled = rule.Team == Team.Navy;
            foreach (var human in instance.GetComponentsInChildren<MNG_HumanInput>(true))
                human.enabled = false;
            var match = instance.GetComponent<MNG_MatchController>();
            instance.AddComponent<MNG_MSController>().Configure(
                MNG_MSMode.R0Opponent,
                profile,
                match,
                episodeSeconds,
                timeScale);
            Require(EditorSceneManager.SaveScene(scene, scenePath),
                $"Failed to save MS scene: {scenePath}");
        }

        static void CreateSelfPlayScene(
            string scenePath,
            float episodeSeconds,
            float timeScale)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = InstantiateManagerPrefab(scene, "MS self-play");
            foreach (var manager in instance.GetComponentsInChildren<MNG_ManagerAgent>(true))
            {
                manager.gameObject.SetActive(true);
                manager.enabled = true;
                manager.GetComponent<Unity.MLAgents.DecisionRequester>().enabled = true;
            }
            foreach (var fallback in instance.GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = false;
            foreach (var rule in instance.GetComponentsInChildren<MNG_RuleBasedManager>(true))
                rule.enabled = false;
            foreach (var human in instance.GetComponentsInChildren<MNG_HumanInput>(true))
                human.enabled = false;
            var match = instance.GetComponent<MNG_MatchController>();
            instance.AddComponent<MNG_MSController>().Configure(
                MNG_MSMode.SelfPlay,
                null,
                match,
                episodeSeconds,
                timeScale);
            Require(EditorSceneManager.SaveScene(scene, scenePath),
                $"Failed to save MS self-play scene: {scenePath}");
        }

        static void CreateMS1Scene(string scenePath, MNG_MSOpponentProfile profile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = InstantiateManagerPrefab(scene, "MS1 mixed training");
            foreach (var manager in instance.GetComponentsInChildren<MNG_ManagerAgent>(true))
            {
                var active = manager.Team == Team.Red;
                manager.gameObject.SetActive(active);
                manager.enabled = active;
                manager.GetComponent<Unity.MLAgents.DecisionRequester>().enabled = active;
            }
            foreach (var fallback in instance.GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = false;
            foreach (var rule in instance.GetComponentsInChildren<MNG_RuleBasedManager>(true))
                rule.enabled = rule.Team == Team.Navy;
            foreach (var human in instance.GetComponentsInChildren<MNG_HumanInput>(true))
                human.enabled = false;
            var match = instance.GetComponent<MNG_MatchController>();
            var ball = instance.GetComponentInChildren<MNG_BallControl>();
            instance.AddComponent<MNG_MS1Controller>().Configure(
                profile,
                match,
                ball,
                MNG_MS1Controller.DefaultTimeScale);
            Require(EditorSceneManager.SaveScene(scene, scenePath),
                $"Failed to save MS1 scene: {scenePath}");
        }

        static void CreateMS1EvaluationScene(
            ModelAsset model,
            MNG_MSOpponentProfile profile,
            string runId,
            string candidateId,
            string modelSha)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = InstantiateManagerPrefab(scene, "MS1 evaluation");
            MNG_ManagerAgent policy = null;
            foreach (var manager in instance.GetComponentsInChildren<MNG_ManagerAgent>(true))
            {
                var active = manager.Team == Team.Red;
                manager.gameObject.SetActive(active);
                manager.enabled = active;
                manager.GetComponent<Unity.MLAgents.DecisionRequester>().enabled = active;
                if (active) policy = manager;
            }
            Require(policy != null, "MS1 evaluation requires the Red manager.");
            var behavior = policy.GetComponent<BehaviorParameters>();
            behavior.Model = model;
            behavior.BehaviorType = BehaviorType.InferenceOnly;
            foreach (var fallback in instance.GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = false;
            foreach (var rule in instance.GetComponentsInChildren<MNG_RuleBasedManager>(true))
                rule.enabled = rule.Team == Team.Navy;
            foreach (var human in instance.GetComponentsInChildren<MNG_HumanInput>(true))
                human.enabled = false;
            var match = instance.GetComponent<MNG_MatchController>();
            var ball = instance.GetComponentInChildren<MNG_BallControl>();
            var curriculum = instance.AddComponent<MNG_MS1Controller>();
            curriculum.Configure(profile, match, ball, MNG_MS1Controller.DefaultTimeScale);
            curriculum.ConfigureScenarioSeeds(
                MNG_MS1Controller.EvaluationAttackSeed,
                MNG_MS1Controller.EvaluationDefenseSeed);
            instance.AddComponent<MNG_MS1EvaluationController>()
                .Configure(curriculum, runId, candidateId, modelSha);
            Require(EditorSceneManager.SaveScene(scene, MS1EvaluationScenePath),
                $"Failed to save MS1 evaluation scene: {MS1EvaluationScenePath}");
        }

        static void PrepareMS1ReviewAssets(
            out string runId,
            out string candidateId,
            out string modelSha)
        {
            ResolveMS1ReviewArguments(
                out runId,
                out candidateId,
                out modelSha,
                out var modelAssetPath,
                out var importedModel);

            EnsureFolders();
            Directory.CreateDirectory(MS1EvaluationModelRoot);
            var suppliedModel = ReadArgument("-mngModelPath", string.Empty);
            var sourceModel = Path.GetFullPath(suppliedModel);
            if (File.Exists(importedModel))
            {
                if (!string.Equals(Sha256(importedModel), modelSha,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Frozen MS1 candidate exists with different bytes: {modelAssetPath}");
            }
            else File.Copy(sourceModel, importedModel, false);
            AssetDatabase.ImportAsset(modelAssetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(modelAssetPath);
            Require(model != null, $"Unity failed to import MS1 review ONNX: {modelAssetPath}");

            var easy = GetOrCreateProfile(
                EasyProfilePath, MNG_MSOpponentStrength.Easy, 0.35f, 1.5f);
            CreateMS1ReviewScene(model, easy);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void ResolveMS1ReviewArguments(
            out string runId,
            out string candidateId,
            out string modelSha,
            out string modelAssetPath,
            out string importedModel)
        {
            runId = ReadArgument("-mngRunId", string.Empty);
            candidateId = ReadArgument("-mngCandidateId", runId);
            var suppliedModel = ReadArgument("-mngModelPath", string.Empty);
            if (string.IsNullOrWhiteSpace(runId)
                || !runId.StartsWith("MNG_MS1-", StringComparison.Ordinal))
                throw new InvalidOperationException($"Invalid MS1 review run ID: {runId}");
            if (string.IsNullOrWhiteSpace(candidateId)
                || candidateId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                throw new InvalidOperationException($"Invalid MS1 review candidate ID: {candidateId}");
            if (string.IsNullOrWhiteSpace(suppliedModel) || !File.Exists(suppliedModel))
                throw new InvalidOperationException($"Missing MS1 review model: {suppliedModel}");

            var sourceModel = Path.GetFullPath(suppliedModel);
            modelSha = Sha256(sourceModel);
            modelAssetPath = $"{MS1EvaluationModelRoot}/{candidateId}.onnx";
            var projectRoot = Path.GetDirectoryName(Application.dataPath)
                ?? throw new InvalidOperationException("Could not resolve project root.");
            importedModel = Path.Combine(projectRoot,
                modelAssetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        static void ValidateMS1ReviewScene(ModelAsset expectedModel)
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(MS1ReviewScenePath) != null,
                $"Missing MS1 review scene: {MS1ReviewScenePath}");
            var scene = EditorSceneManager.OpenScene(MS1ReviewScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().Single();
            var controller = root.GetComponent<MNG_MS1Controller>();
            Require(controller != null
                    && controller.OpponentProfile != null
                    && controller.OpponentProfile.Strength == MNG_MSOpponentStrength.Easy
                    && Mathf.Abs(controller.TimeScale - 1f) < 0.00001f
                    && controller.AttackScenarioSeed == MNG_MS1Controller.EvaluationAttackSeed
                    && controller.DefenseScenarioSeed == MNG_MS1Controller.EvaluationDefenseSeed,
                "MS1 review scene requires Easy R0, 1x time, and frozen evaluation seeds.");
            Require(root.GetComponent<MNG_MS1EvaluationController>() == null,
                "MS1 review scene must not contain the auto-quit evaluation controller.");
            var managers = root.GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Where(manager => manager.gameObject.activeInHierarchy && manager.enabled).ToArray();
            Require(managers.Length == 1 && managers[0].Team == Team.Red,
                "MS1 review scene requires one active Red PPO manager.");
            var behavior = managers[0].GetComponent<BehaviorParameters>();
            Require(behavior != null
                    && behavior.BehaviorType == BehaviorType.InferenceOnly
                    && behavior.Model == expectedModel,
                "MS1 review scene must use the frozen ONNX in inference-only mode.");
            var rules = root.GetComponentsInChildren<MNG_RuleBasedManager>(true)
                .Where(rule => rule.enabled).ToArray();
            Require(rules.Length == 1 && rules[0].Team == Team.Navy,
                "MS1 review scene requires one Navy R0 manager.");
            Require(root.GetComponentsInChildren<MNG_FallbackManager>(true)
                    .All(fallback => !fallback.enabled),
                "MS1 review scene cannot contain an active fallback writer.");
            Require(root.GetComponentsInChildren<MNG_HumanInput>(true)
                    .All(human => !human.enabled),
                "MS1 review scene cannot contain active human input.");
        }

        static void CreateMS1ReviewScene(ModelAsset model, MNG_MSOpponentProfile profile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = InstantiateManagerPrefab(scene, "MS1 human review");
            MNG_ManagerAgent policy = null;
            foreach (var manager in instance.GetComponentsInChildren<MNG_ManagerAgent>(true))
            {
                var active = manager.Team == Team.Red;
                manager.gameObject.SetActive(active);
                manager.enabled = active;
                manager.GetComponent<Unity.MLAgents.DecisionRequester>().enabled = active;
                if (active) policy = manager;
            }
            Require(policy != null, "MS1 review requires the Red manager.");
            var behavior = policy.GetComponent<BehaviorParameters>();
            behavior.Model = model;
            behavior.BehaviorType = BehaviorType.InferenceOnly;
            foreach (var fallback in instance.GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = false;
            foreach (var rule in instance.GetComponentsInChildren<MNG_RuleBasedManager>(true))
                rule.enabled = rule.Team == Team.Navy;
            foreach (var human in instance.GetComponentsInChildren<MNG_HumanInput>(true))
                human.enabled = false;
            var match = instance.GetComponent<MNG_MatchController>();
            var ball = instance.GetComponentInChildren<MNG_BallControl>();
            var curriculum = instance.AddComponent<MNG_MS1Controller>();
            curriculum.Configure(profile, match, ball, 1f);
            curriculum.ConfigureScenarioSeeds(
                MNG_MS1Controller.EvaluationAttackSeed,
                MNG_MS1Controller.EvaluationDefenseSeed);
            Require(instance.GetComponent<MNG_MS1EvaluationController>() == null,
                "MS1 review scene must not auto-quit through the evaluation controller.");
            Require(EditorSceneManager.SaveScene(scene, MS1ReviewScenePath),
                $"Failed to save MS1 review scene: {MS1ReviewScenePath}");
        }

        static void CreateMS2FinalReviewScene(
            ModelAsset model,
            MNG_MSOpponentProfile fullProfile)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = InstantiateManagerPrefab(scene, "MS2 final human review");
            MNG_ManagerAgent policy = null;
            foreach (var manager in instance.GetComponentsInChildren<MNG_ManagerAgent>(true))
            {
                var active = manager.Team == Team.Red;
                manager.gameObject.SetActive(active);
                manager.enabled = active;
                manager.GetComponent<Unity.MLAgents.DecisionRequester>().enabled = active;
                if (!active) continue;
                policy = manager;
                policy.ConfigurePolicyAssistMode(MNG_PolicyAssistMode.None);
                var behavior = policy.GetComponent<BehaviorParameters>();
                behavior.Model = model;
                behavior.BehaviorType = BehaviorType.InferenceOnly;
            }
            Require(policy != null, "MS2 final review requires the Red manager.");
            foreach (var fallback in instance.GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = false;
            foreach (var rule in instance.GetComponentsInChildren<MNG_RuleBasedManager>(true))
                rule.enabled = rule.Team == Team.Navy;
            foreach (var human in instance.GetComponentsInChildren<MNG_HumanInput>(true))
                human.enabled = false;

            var match = instance.GetComponent<MNG_MatchController>();
            instance.AddComponent<MNG_MSController>().Configure(
                MNG_MSMode.R0Opponent,
                fullProfile,
                match,
                MNG_MatchController.MatchDurationSeconds,
                1f);
            Require(instance.GetComponent<MNG_MS2EvaluationController>() == null,
                "MS2 final review must not contain the auto-quit evaluation controller.");
            Require(EditorSceneManager.SaveScene(scene, MS2FinalReviewScenePath),
                $"Failed to save MS2 final review scene: {MS2FinalReviewScenePath}");
        }

        static void ValidateMS2FinalReviewScene(ModelAsset expectedModel)
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(MS2FinalReviewScenePath) != null,
                $"Missing MS2 final review scene: {MS2FinalReviewScenePath}");
            var scene = EditorSceneManager.OpenScene(MS2FinalReviewScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().Single();
            var controller = root.GetComponent<MNG_MSController>();
            Require(controller != null
                    && controller.Mode == MNG_MSMode.R0Opponent
                    && controller.OpponentProfile != null
                    && controller.OpponentProfile.Strength == MNG_MSOpponentStrength.Full
                    && Mathf.Abs(controller.OpponentProfile.MovementSpeedMultiplier - 1f) < 0.00001f
                    && Mathf.Abs(controller.OpponentProfile.DecisionIntervalSeconds
                        - MNG_RuleBasedManager.DecisionIntervalSeconds) < 0.00001f
                    && Mathf.Abs(controller.EpisodeSeconds
                        - MNG_MatchController.MatchDurationSeconds) < 0.0001f
                    && Mathf.Abs(controller.TimeScale - 1f) < 0.0001f,
                "MS2 final review requires R0-Full, 300 seconds, and 1x time.");
            Require(root.GetComponent<MNG_MS2EvaluationController>() == null,
                "MS2 final review cannot auto-quit through the evaluation controller.");

            var managers = root.GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Where(manager => manager.gameObject.activeInHierarchy && manager.enabled)
                .ToArray();
            Require(managers.Length == 1
                    && managers[0].Team == Team.Red
                    && managers[0].PolicyAssistMode == MNG_PolicyAssistMode.None,
                "MS2 final review requires one unassisted Red PPO manager.");
            var behavior = managers[0].GetComponent<BehaviorParameters>();
            Require(behavior != null
                    && behavior.BehaviorType == BehaviorType.InferenceOnly
                    && behavior.Model == expectedModel,
                "MS2 final review must use the frozen ONNX in inference-only mode.");
            var rules = root.GetComponentsInChildren<MNG_RuleBasedManager>(true)
                .Where(rule => rule.enabled).ToArray();
            Require(rules.Length == 1 && rules[0].Team == Team.Navy,
                "MS2 final review requires one Navy R0 manager.");
            Require(root.GetComponentsInChildren<MNG_FallbackManager>(true)
                    .All(fallback => !fallback.enabled),
                "MS2 final review cannot contain an active fallback writer.");
            Require(root.GetComponentsInChildren<MNG_HumanInput>(true)
                    .All(human => !human.enabled),
                "MS2 final review cannot contain active human input.");
        }

        static void CreateMS3FinalReviewScene(ModelAsset model)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = InstantiateManagerPrefab(scene, "MS3 final human review");
            foreach (var manager in instance.GetComponentsInChildren<MNG_ManagerAgent>(true))
            {
                manager.gameObject.SetActive(true);
                manager.enabled = true;
                manager.ConfigurePolicyAssistMode(MNG_PolicyAssistMode.None);
                manager.GetComponent<Unity.MLAgents.DecisionRequester>().enabled = true;
                var behavior = manager.GetComponent<BehaviorParameters>();
                behavior.Model = model;
                behavior.BehaviorType = BehaviorType.InferenceOnly;
            }
            foreach (var fallback in instance.GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = false;
            foreach (var rule in instance.GetComponentsInChildren<MNG_RuleBasedManager>(true))
                rule.enabled = false;
            foreach (var human in instance.GetComponentsInChildren<MNG_HumanInput>(true))
                human.enabled = false;

            var match = instance.GetComponent<MNG_MatchController>();
            instance.AddComponent<MNG_MSController>().Configure(
                MNG_MSMode.SelfPlay,
                null,
                match,
                MNG_MatchController.MatchDurationSeconds,
                1f);
            Require(instance.GetComponent<MNG_MS2EvaluationController>() == null,
                "MS3 final review must not contain an auto-quit evaluation controller.");
            Require(EditorSceneManager.SaveScene(scene, MS3FinalReviewScenePath),
                $"Failed to save MS3 final review scene: {MS3FinalReviewScenePath}");
        }

        static void ValidateMS3FinalReviewScene(ModelAsset expectedModel)
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(MS3FinalReviewScenePath) != null,
                $"Missing MS3 final review scene: {MS3FinalReviewScenePath}");
            var scene = EditorSceneManager.OpenScene(MS3FinalReviewScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().Single();
            var controller = root.GetComponent<MNG_MSController>();
            Require(controller != null
                    && controller.Mode == MNG_MSMode.SelfPlay
                    && Mathf.Abs(controller.EpisodeSeconds
                        - MNG_MatchController.MatchDurationSeconds) < 0.0001f
                    && Mathf.Abs(controller.TimeScale - 1f) < 0.0001f,
                "MS3 final review requires PPO self-play, 300 seconds, and 1x time.");
            Require(root.GetComponent<MNG_MS2EvaluationController>() == null,
                "MS3 final review cannot auto-quit through an evaluation controller.");

            var managers = root.GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Where(manager => manager.gameObject.activeInHierarchy && manager.enabled)
                .ToArray();
            Require(managers.Length == 2
                    && managers.Select(manager => manager.Team).Distinct().Count() == 2
                    && managers.All(manager => manager.PolicyAssistMode == MNG_PolicyAssistMode.None),
                "MS3 final review requires two distinct unassisted PPO managers.");
            var teamIds = managers
                .Select(manager => manager.GetComponent<BehaviorParameters>().TeamId)
                .OrderBy(value => value)
                .ToArray();
            Require(teamIds.SequenceEqual(new[] { (int)Team.Red, (int)Team.Navy }),
                "MS3 final review Team IDs must map to Red=0 and Navy=1.");
            Require(managers.All(manager =>
            {
                var behavior = manager.GetComponent<BehaviorParameters>();
                return behavior != null
                    && behavior.BehaviorType == BehaviorType.InferenceOnly
                    && behavior.Model == expectedModel
                    && behavior.BehaviorName == MNG_ManagerAgent.BehaviorName;
            }), "MS3 final review must use the frozen ONNX on both teams in inference-only mode.");
            Require(root.GetComponentsInChildren<MNG_RuleBasedManager>(true)
                    .All(rule => !rule.enabled)
                    && root.GetComponentsInChildren<MNG_FallbackManager>(true)
                        .All(fallback => !fallback.enabled),
                "MS3 final review cannot contain an active rule or fallback writer.");
            Require(root.GetComponentsInChildren<MNG_HumanInput>(true)
                    .All(human => !human.enabled),
                "MS3 final review cannot contain active human input.");
        }

        static void CreateMS3CheckpointDuelScene(
            ModelAsset earlierModel,
            ModelAsset finalModel,
            string duelId,
            string earlierCandidateId,
            string earlierModelSha,
            string finalCandidateId,
            string finalModelSha)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = InstantiateManagerPrefab(scene, "MS3 checkpoint duel");
            foreach (var manager in instance.GetComponentsInChildren<MNG_ManagerAgent>(true))
            {
                manager.gameObject.SetActive(true);
                manager.enabled = true;
                manager.ConfigurePolicyAssistMode(MNG_PolicyAssistMode.None);
                manager.GetComponent<Unity.MLAgents.DecisionRequester>().enabled = true;
                var behavior = manager.GetComponent<BehaviorParameters>();
                behavior.Model = manager.Team == Team.Red ? earlierModel : finalModel;
                behavior.BehaviorType = BehaviorType.InferenceOnly;
            }
            foreach (var fallback in instance.GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = false;
            foreach (var rule in instance.GetComponentsInChildren<MNG_RuleBasedManager>(true))
                rule.enabled = false;
            foreach (var human in instance.GetComponentsInChildren<MNG_HumanInput>(true))
                human.enabled = false;

            var match = instance.GetComponent<MNG_MatchController>();
            instance.AddComponent<MNG_MS3CheckpointDuelController>().Configure(
                match,
                earlierModel,
                finalModel,
                duelId,
                earlierCandidateId,
                earlierModelSha,
                finalCandidateId,
                finalModelSha);
            Require(instance.GetComponent<MNG_MSController>() == null,
                "MS3 checkpoint duel must use only its evaluation controller.");
            Require(EditorSceneManager.SaveScene(scene, MS3CheckpointDuelScenePath),
                $"Failed to save MS3 checkpoint duel scene: {MS3CheckpointDuelScenePath}");
        }

        static void ValidateMS3CheckpointDuelScene(
            ModelAsset expectedEarlierModel,
            ModelAsset expectedFinalModel,
            string expectedDuelId)
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(MS3CheckpointDuelScenePath) != null,
                $"Missing MS3 checkpoint duel scene: {MS3CheckpointDuelScenePath}");
            var scene = EditorSceneManager.OpenScene(
                MS3CheckpointDuelScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().Single();
            var controller = root.GetComponent<MNG_MS3CheckpointDuelController>();
            Require(controller != null
                    && controller.EarlierModel == expectedEarlierModel
                    && controller.FinalModel == expectedFinalModel
                    && string.Equals(
                        controller.DuelId, expectedDuelId, StringComparison.Ordinal),
                "MS3 checkpoint duel controller bindings diverged.");
            Require(root.GetComponent<MNG_MSController>() == null,
                "MS3 checkpoint duel cannot contain the training controller.");

            var managers = root.GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Where(manager => manager.gameObject.activeInHierarchy && manager.enabled)
                .ToArray();
            Require(managers.Length == 2
                    && managers.Select(manager => manager.Team).Distinct().Count() == 2
                    && managers.All(manager =>
                        manager.PolicyAssistMode == MNG_PolicyAssistMode.None),
                "MS3 checkpoint duel requires two distinct unassisted PPO managers.");
            var teamIds = managers
                .Select(manager => manager.GetComponent<BehaviorParameters>().TeamId)
                .OrderBy(value => value)
                .ToArray();
            Require(teamIds.SequenceEqual(new[] { (int)Team.Red, (int)Team.Navy }),
                "MS3 checkpoint duel Team IDs must map to Red=0 and Navy=1.");
            Require(managers.All(manager =>
            {
                var behavior = manager.GetComponent<BehaviorParameters>();
                var expected = manager.Team == Team.Red
                    ? expectedEarlierModel : expectedFinalModel;
                return behavior != null
                    && behavior.BehaviorType == BehaviorType.InferenceOnly
                    && behavior.Model == expected
                    && behavior.BehaviorName == MNG_ManagerAgent.BehaviorName;
            }), "MS3 checkpoint duel must freeze the intended ONNX model per team.");
            Require(root.GetComponentsInChildren<MNG_RuleBasedManager>(true)
                    .All(rule => !rule.enabled)
                    && root.GetComponentsInChildren<MNG_FallbackManager>(true)
                        .All(fallback => !fallback.enabled),
                "MS3 checkpoint duel cannot contain an active rule or fallback writer.");
            Require(root.GetComponentsInChildren<MNG_HumanInput>(true)
                    .All(human => !human.enabled),
                "MS3 checkpoint duel cannot contain active human input.");
        }

        static void CreateMS2PreflightEvaluationScene(
            ModelAsset model,
            MNG_MSOpponentProfile medium,
            MNG_MSOpponentProfile full,
            string runId,
            string candidateId,
            string modelSha)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var instance = InstantiateManagerPrefab(scene, "MS2 preflight evaluation");
            foreach (var manager in instance.GetComponentsInChildren<MNG_ManagerAgent>(true))
            {
                var behavior = manager.GetComponent<BehaviorParameters>();
                behavior.Model = model;
                behavior.BehaviorType = BehaviorType.InferenceOnly;
                manager.enabled = false;
                manager.GetComponent<Unity.MLAgents.DecisionRequester>().enabled = false;
                manager.gameObject.SetActive(false);
            }
            foreach (var fallback in instance.GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = false;
            foreach (var rule in instance.GetComponentsInChildren<MNG_RuleBasedManager>(true))
                rule.enabled = false;
            foreach (var human in instance.GetComponentsInChildren<MNG_HumanInput>(true))
                human.enabled = false;
            var match = instance.GetComponent<MNG_MatchController>();
            instance.AddComponent<MNG_MS2EvaluationController>().Configure(
                match,
                medium,
                full,
                runId,
                candidateId,
                modelSha);
            Require(EditorSceneManager.SaveScene(scene, MS2PreflightEvaluationScenePath),
                $"Failed to save MS2 preflight scene: {MS2PreflightEvaluationScenePath}");
        }

        static GameObject InstantiateManagerPrefab(Scene scene, string purpose)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MNG_ProjectBuilder.ManagerPrefabPath);
            Require(prefab != null, $"Missing MNG manager prefab: {MNG_ProjectBuilder.ManagerPrefabPath}");
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            Require(instance != null, $"Failed to instantiate MNG manager prefab for {purpose}.");
            return instance;
        }

        static void ValidateR0Scene(string scenePath, float seconds, float timeScale)
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null,
                $"Missing MS R0 scene: {scenePath}");
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().Single();
            var controller = root.GetComponent<MNG_MSController>();
            Require(controller != null && controller.Mode == MNG_MSMode.R0Opponent,
                $"MS R0 scene controller is invalid: {scenePath}");
            Require(controller.OpponentProfile != null
                && controller.OpponentProfile.Strength == MNG_MSOpponentStrength.Full
                && Mathf.Abs(controller.OpponentProfile.MovementSpeedMultiplier - 1f) < 0.00001f
                && Mathf.Abs(controller.OpponentProfile.DecisionIntervalSeconds
                    - MNG_RuleBasedManager.DecisionIntervalSeconds) < 0.00001f,
                "MS Full profile must be exactly equivalent to current R0 speed and interval.");
            Require(Mathf.Abs(controller.EpisodeSeconds - seconds) < 0.0001f
                && Mathf.Abs(controller.TimeScale - timeScale) < 0.0001f,
                $"MS R0 scene timing mismatch: {scenePath}");
            var managers = root.GetComponentsInChildren<MNG_ManagerAgent>(true);
            Require(managers.Count(manager => manager.gameObject.activeInHierarchy) == 1
                && managers.Single(manager => manager.gameObject.activeInHierarchy).Team == Team.Red,
                "MS R0 scene requires only the Red PPO manager active.");
            var rules = root.GetComponentsInChildren<MNG_RuleBasedManager>(true);
            Require(rules.Count(rule => rule.enabled) == 1
                && rules.Single(rule => rule.enabled).Team == Team.Navy,
                "MS R0 scene requires only the Navy R0 manager active.");
            Require(root.GetComponentsInChildren<MNG_FallbackManager>(true)
                    .All(fallback => !fallback.enabled),
                "MS scenes cannot use legacy fallback managers.");
        }

        static void ValidateSelfPlayScene(
            string scenePath,
            float episodeSeconds,
            float timeScale)
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) != null,
                $"Missing MS self-play scene: {scenePath}");
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().Single();
            var controller = root.GetComponent<MNG_MSController>();
            Require(controller != null
                    && controller.Mode == MNG_MSMode.SelfPlay
                    && Mathf.Abs(controller.EpisodeSeconds - episodeSeconds) < 0.0001f
                    && Mathf.Abs(controller.TimeScale - timeScale) < 0.0001f,
                "MS self-play controller is missing or misconfigured.");
            var managers = root.GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Where(manager => manager.gameObject.activeInHierarchy && manager.enabled)
                .ToArray();
            Require(managers.Length == 2 && managers.Select(manager => manager.Team).Distinct().Count() == 2,
                "MS self-play requires two active managers with distinct teams.");
            var teamIds = managers
                .Select(manager => manager.GetComponent<BehaviorParameters>().TeamId)
                .OrderBy(value => value)
                .ToArray();
            Require(teamIds.SequenceEqual(new[] { (int)Team.Red, (int)Team.Navy }),
                "MS self-play Team IDs must map to Red=0 and Navy=1.");
            Require(managers.All(manager => manager.GetComponent<BehaviorParameters>().BehaviorName
                    == MNG_ManagerAgent.BehaviorName),
                "MS self-play managers must share MNG_Manager BehaviorName.");
            Require(managers.All(manager => manager.PolicyAssistMode == MNG_PolicyAssistMode.None),
                "MS self-play managers must use exact no-assist policy actions.");
            Require(root.GetComponentsInChildren<MNG_RuleBasedManager>(true).All(rule => !rule.enabled)
                && root.GetComponentsInChildren<MNG_FallbackManager>(true)
                    .All(fallback => !fallback.enabled),
                "MS self-play cannot contain an active rule or fallback writer.");
        }

        static MNG_MSOpponentProfile GetOrCreateProfile(
            string path,
            MNG_MSOpponentStrength strength,
            float movement,
            float interval)
        {
            var profile = AssetDatabase.LoadAssetAtPath<MNG_MSOpponentProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MNG_MSOpponentProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            profile.Configure(strength, movement, interval);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static void ValidateProfile(
            string path,
            MNG_MSOpponentStrength strength,
            float movement,
            float interval)
        {
            var profile = AssetDatabase.LoadAssetAtPath<MNG_MSOpponentProfile>(path);
            Require(profile != null, $"Missing MS opponent profile: {path}");
            profile.ValidateOrThrow();
            Require(profile.Strength == strength
                && Mathf.Abs(profile.MovementSpeedMultiplier - movement) < 0.00001f
                && Mathf.Abs(profile.DecisionIntervalSeconds - interval) < 0.00001f,
                $"MS opponent profile values diverged: {path}");
        }

        static ModelAsset ImportFrozenModel(
            string suppliedPath,
            string modelAssetPath,
            out string modelSha)
        {
            if (string.IsNullOrWhiteSpace(suppliedPath) || !File.Exists(suppliedPath))
                throw new InvalidOperationException(
                    $"Missing frozen model for MS3 checkpoint duel: {suppliedPath}");
            var sourceModel = Path.GetFullPath(suppliedPath);
            modelSha = Sha256(sourceModel);
            var projectRoot = Path.GetDirectoryName(Application.dataPath)
                ?? throw new InvalidOperationException("Could not resolve project root.");
            var importedModel = Path.Combine(
                projectRoot,
                modelAssetPath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(importedModel)
                ?? throw new InvalidOperationException(
                    "MS3 checkpoint duel model directory is invalid."));
            if (File.Exists(importedModel))
            {
                Require(string.Equals(
                        Sha256(importedModel), modelSha, StringComparison.OrdinalIgnoreCase),
                    $"Frozen checkpoint duel model exists with different bytes: {modelAssetPath}");
            }
            else File.Copy(sourceModel, importedModel, false);
            AssetDatabase.ImportAsset(
                modelAssetPath,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var model = AssetDatabase.LoadAssetAtPath<ModelAsset>(modelAssetPath);
            Require(model != null,
                $"Unity failed to import checkpoint duel ONNX: {modelAssetPath}");
            return model;
        }

        static void RequireSafeFileName(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value)
                || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || value.Contains("..", StringComparison.Ordinal))
                throw new InvalidOperationException($"Invalid {label}: {value}");
        }

        static void EnsureFolders()
        {
            EnsureFolder("Assets/_Soccer/Manager/Curriculum", "MS_ManagerSimple");
            EnsureFolder(CurriculumRoot, "Scenes");
            EnsureFolder("Assets/_Soccer/Manager/Profiles", "MS");
        }

        static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }

        static string MakeProjectRelative(string path)
        {
            var projectRoot = Path.GetFullPath(
                Path.Combine(Application.dataPath, ".."));
            var fullPath = Path.GetFullPath(path);
            var prefix = projectRoot.TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (!fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("MS0 build output must remain inside the project.");
            return fullPath.Substring(prefix.Length).Replace('\\', '/');
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

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
