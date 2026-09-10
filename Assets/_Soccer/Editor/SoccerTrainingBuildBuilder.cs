using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MachineLearning.Soccer.Curriculum;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MachineLearning.Soccer.Editor
{
    public static class SoccerTrainingBuildBuilder
    {
        public const string BootstrapScenePath = "Assets/_Soccer/Training/Scenes/SoccerTrainingBootstrap.unity";
        public const string WindowsBuildPath = "Builds/SoccerTraining/SoccerTraining.exe";
        public const string ManifestPath = "Builds/SoccerTraining/training-profiles.json";
        public const string BuildInfoPath = "Builds/SoccerTraining/build-info.json";
        const string BaseDefinitionPath = "Assets/_Soccer/Core/Profiles/BaseTeamDefinition.asset";

        static readonly UTF8Encoding Utf8WithoutBom = new UTF8Encoding(false, true);

        [MenuItem("Tools/Soccer/Training/Validate training build profiles")]
        public static void ValidateTrainingBuildProfiles()
        {
            var previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                SoccerProjectBuilder.ValidateGeneratedAssets();
                ValidateCatalog();
                CreateBootstrapScene();
                ValidateBootstrapScene();
                ValidateProfileScenesAndConfigs();
            }
            finally
            {
                RestoreSceneSetup(previousSceneSetup);
            }

            Debug.Log(
                $"Soccer training build profile validation passed: " +
                $"{SoccerTrainingProfileCatalog.Profiles.Count} profiles, " +
                $"{SoccerTrainingProfileCatalog.TrainableProfiles.Count()} trainable.");
        }

        public static void ValidateBatch()
        {
            ValidateTrainingBuildProfiles();
        }

        [MenuItem("Tools/Soccer/Training/Build Windows training player")]
        public static void BuildWindowsPlayer()
        {
            ValidateTrainingBuildProfiles();

            var absoluteBuildPath = ToAbsoluteProjectPath(WindowsBuildPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteBuildPath));
            var buildScenes = GetBuildScenePaths();
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = buildScenes,
                locationPathName = absoluteBuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Soccer training Windows build failed: {report.summary.result}, " +
                    $"{report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings.");
            }

            WriteBuildMetadata(report, buildScenes);
            Debug.Log(
                $"Soccer training Windows build succeeded: {absoluteBuildPath} " +
                $"({report.summary.totalSize} bytes, {SoccerTrainingProfileCatalog.Profiles.Count} profiles).");
        }

        public static void BuildWindowsBatch()
        {
            BuildWindowsPlayer();
        }

        public static string[] GetBuildScenePathsForTests()
        {
            return GetBuildScenePaths();
        }

        static void ValidateCatalog()
        {
            var profiles = SoccerTrainingProfileCatalog.Profiles;
            Require(profiles.Count == 12,
                "Training build must expose six curriculum profiles, five existing training/evaluation profiles and Rule evaluation.");
            Require(profiles.Select(profile => profile.Key).Distinct(StringComparer.OrdinalIgnoreCase).Count() == profiles.Count,
                "Training profile keys must be unique.");
            Require(profiles.Select(profile => profile.SceneAssetPath).Distinct(StringComparer.Ordinal).Count() == profiles.Count,
                "Each training profile must map to one explicit scene.");
            Require(SoccerTrainingProfileCatalog.TrainableProfiles.Count() == 11,
                "All six curricula, Base fallback, Base self-play, Attack, Defense and Press must be trainable.");
            Require(profiles.Where(profile => profile.SupportsTraining)
                    .Select(profile => profile.DefaultBasePort)
                    .Distinct()
                    .Count() == 11,
                "Trainable profiles must use distinct default base ports.");
        }

        static void ValidateProfileScenesAndConfigs()
        {
            foreach (var profile in SoccerTrainingProfileCatalog.Profiles)
            {
                Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(profile.SceneAssetPath) != null,
                    $"Training profile scene is missing: {profile.Key} -> {profile.SceneAssetPath}");
                ValidateProfileScene(profile);
                ValidateTrainerConfig(profile);
            }
        }

        static void ValidateProfileScene(SoccerTrainingProfile profile)
        {
            var scene = EditorSceneManager.OpenScene(profile.SceneAssetPath, OpenSceneMode.Single);
            var setups = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SoccerMatchSetup>(true))
                .ToArray();
            Require(setups.Length == 1,
                $"Training profile scene must contain exactly one SoccerMatchSetup: {profile.SceneAssetPath}");

            var setup = setups[0];
            Require(setup.TrainRed == profile.TrainRed && setup.TrainNavy == profile.TrainNavy,
                $"Training flags do not match profile '{profile.Key}'.");
            Require(setup.ForceRedFallback == profile.ForceRedFallback
                    && setup.ForceNavyFallback == profile.ForceNavyFallback,
                $"Fallback flags do not match profile '{profile.Key}'.");
            Require(setup.RedTeam != null && setup.RedTeam.BehaviorName == profile.RedBehaviorName,
                $"Red BehaviorName does not match profile '{profile.Key}'.");
            Require(setup.NavyTeam != null && setup.NavyTeam.BehaviorName == profile.NavyBehaviorName,
                $"Navy BehaviorName does not match profile '{profile.Key}'.");

            if (profile.ForceRedFallback)
            {
                Require(setup.GetConfiguredModel(Team.Red) == null && !setup.IsTrainable(Team.Red),
                    $"Red must be isolated from Trainer and ONNX in fallback profile '{profile.Key}'.");
            }

            if (profile.ForceNavyFallback)
            {
                Require(setup.GetConfiguredModel(Team.Navy) == null && !setup.IsTrainable(Team.Navy),
                    $"Navy must be isolated from Trainer and ONNX in fallback profile '{profile.Key}'.");
            }

            if (!profile.SupportsTraining)
            {
                Require(!setup.IsTrainable(Team.Red) && !setup.IsTrainable(Team.Navy),
                    $"Evaluation-only profile '{profile.Key}' must not expose a trainable team.");
            }

            if (profile.Key == SoccerTrainingProfileCatalog.CurriculumL0Key
                || profile.Key == SoccerTrainingProfileCatalog.CurriculumL1Key
                || profile.Key == SoccerTrainingProfileCatalog.CurriculumL2Key
                || profile.Key == SoccerTrainingProfileCatalog.CurriculumL2FindKey
                || profile.Key == SoccerTrainingProfileCatalog.CurriculumL2ScoreKey
                || profile.Key == SoccerTrainingProfileCatalog.CurriculumL3Key)
            {
                var curricula = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SoccerCurriculumController>(true))
                    .ToArray();
                var expectedLesson = profile.Key == SoccerTrainingProfileCatalog.CurriculumL0Key
                    ? SoccerCurriculumLesson.L0BallApproach
                    : profile.Key == SoccerTrainingProfileCatalog.CurriculumL1Key
                        ? SoccerCurriculumLesson.L1CarryAndShoot
                        : profile.Key == SoccerTrainingProfileCatalog.CurriculumL2Key
                            ? SoccerCurriculumLesson.L2ShortPass
                            : profile.Key == SoccerTrainingProfileCatalog.CurriculumL2FindKey
                                ? SoccerCurriculumLesson.L2Find
                                : profile.Key == SoccerTrainingProfileCatalog.CurriculumL2ScoreKey
                                    ? SoccerCurriculumLesson.L2Score
                                    : SoccerCurriculumLesson.L3ProgressivePlay;
                Require(curricula.Length == 1 && curricula[0].Lesson == expectedLesson,
                    $"Curriculum profile '{profile.Key}' must contain exactly one matching lesson controller.");
            }
        }

        static void ValidateTrainerConfig(SoccerTrainingProfile profile)
        {
            if (!profile.SupportsTraining)
            {
                Require(string.IsNullOrEmpty(profile.TrainerConfigAssetPath),
                    $"Evaluation-only profile '{profile.Key}' must not have a Trainer config.");
                return;
            }

            var absoluteConfigPath = ToAbsoluteProjectPath(profile.TrainerConfigAssetPath);
            Require(File.Exists(absoluteConfigPath),
                $"Trainer config is missing: {profile.Key} -> {profile.TrainerConfigAssetPath}");
            var text = File.ReadAllText(absoluteConfigPath, Utf8WithoutBom).Replace("\r\n", "\n");
            Require(text.All(character => character <= 0x7f),
                $"Trainer config must stay ASCII-safe for the current Windows Python locale: {profile.TrainerConfigAssetPath}");
            Require(text.Contains("\n  " + profile.RedBehaviorName + ":"),
                $"Trainer config BehaviorName does not match profile '{profile.Key}'.");
            Require(text.Contains($"\n    max_steps: {profile.MaxSteps}"),
                $"Trainer config must use the catalog aggregate max_steps of {profile.MaxSteps}: " +
                profile.TrainerConfigAssetPath);

            var hasSelfPlay = text.Contains("\n    self_play:");
            Require(hasSelfPlay == profile.RequiresSelfPlay,
                $"Self-play setting does not match profile '{profile.Key}'.");
        }

        static void CreateBootstrapScene()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath) != null)
            {
                var existingScene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
                var existingBootstraps = existingScene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SoccerTrainingBootstrap>(true))
                    .ToArray();
                if (existingBootstraps.Length == 1)
                {
                    return;
                }
            }

            var absoluteScenePath = ToAbsoluteProjectPath(BootstrapScenePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var bootstrapObject = new GameObject("Soccer Training Bootstrap");
            bootstrapObject.AddComponent<SoccerTrainingBootstrap>();
            Require(EditorSceneManager.SaveScene(scene, BootstrapScenePath),
                $"Could not save the training bootstrap scene: {BootstrapScenePath}");
            AssetDatabase.ImportAsset(BootstrapScenePath, ImportAssetOptions.ForceSynchronousImport);
        }

        static void ValidateBootstrapScene()
        {
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(BootstrapScenePath) != null,
                $"Training bootstrap scene is missing: {BootstrapScenePath}");
            var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
            var bootstraps = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SoccerTrainingBootstrap>(true))
                .ToArray();
            Require(bootstraps.Length == 1,
                "Training bootstrap scene must contain exactly one SoccerTrainingBootstrap.");
        }

        static string[] GetBuildScenePaths()
        {
            return new[] { BootstrapScenePath }
                .Concat(SoccerTrainingProfileCatalog.Profiles.Select(profile => profile.SceneAssetPath))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        static void WriteBuildMetadata(BuildReport report, IReadOnlyList<string> buildScenes)
        {
            var generatedUtc = DateTime.UtcNow.ToString("O");
            var baseDefinition = AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(BaseDefinitionPath);
            var baseModelPath = baseDefinition != null && baseDefinition.InferenceModel != null
                ? AssetDatabase.GetAssetPath(baseDefinition.InferenceModel)
                : string.Empty;
            var manifest = new TrainingProfileManifest
            {
                schemaVersion = 2,
                policyContractVersion = SoccerTrainingProfileCatalog.PolicyContractVersion,
                generatedUtc = generatedUtc,
                executable = WindowsBuildPath.Replace('\\', '/'),
                profiles = SoccerTrainingProfileCatalog.Profiles.Select(profile => new TrainingProfileEntry
                {
                    key = profile.Key,
                    displayName = profile.DisplayName,
                    scene = profile.SceneAssetPath,
                    sceneGuid = AssetDatabase.AssetPathToGUID(profile.SceneAssetPath),
                    trainerConfig = profile.TrainerConfigAssetPath,
                    trainerConfigSha256 = string.IsNullOrEmpty(profile.TrainerConfigAssetPath)
                        ? string.Empty
                        : ComputeSha256(ToAbsoluteProjectPath(profile.TrainerConfigAssetPath)),
                    redBehaviorName = profile.RedBehaviorName,
                    navyBehaviorName = profile.NavyBehaviorName,
                    runIdPrefix = profile.RunIdPrefix,
                    defaultBasePort = profile.DefaultBasePort,
                    maxSteps = profile.MaxSteps,
                    trainRed = profile.TrainRed,
                    trainNavy = profile.TrainNavy,
                    forceRedFallback = profile.ForceRedFallback,
                    forceNavyFallback = profile.ForceNavyFallback,
                    selfPlay = profile.RequiresSelfPlay,
                    supportsTraining = profile.SupportsTraining,
                    requiresNavyModel = profile.RequiresNavyModel,
                    navyModelConfiguredInBuild = !string.IsNullOrEmpty(baseModelPath),
                    navyModelAsset = baseModelPath,
                    navyModelSha256 = string.IsNullOrEmpty(baseModelPath)
                        ? string.Empty
                        : ComputeSha256(ToAbsoluteProjectPath(baseModelPath))
                }).ToArray()
            };

            var absoluteExecutablePath = ToAbsoluteProjectPath(WindowsBuildPath);
            var buildInfo = new TrainingBuildInfo
            {
                schemaVersion = 1,
                generatedUtc = generatedUtc,
                unityVersion = Application.unityVersion,
                buildTarget = report.summary.platform.ToString(),
                result = report.summary.result.ToString(),
                totalBytes = checked((long)report.summary.totalSize),
                totalWarnings = report.summary.totalWarnings,
                totalErrors = report.summary.totalErrors,
                executable = WindowsBuildPath.Replace('\\', '/'),
                executableSha256 = ComputeSha256(absoluteExecutablePath),
                scenes = buildScenes.ToArray()
            };

            WriteJson(ManifestPath, manifest);
            WriteJson(BuildInfoPath, buildInfo);
        }

        static void WriteJson<T>(string relativePath, T value)
        {
            var absolutePath = ToAbsoluteProjectPath(relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllText(absolutePath, JsonUtility.ToJson(value, true) + Environment.NewLine, Utf8WithoutBom);
        }

        static string ComputeSha256(string absolutePath)
        {
            using var stream = File.OpenRead(absolutePath);
            using var sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
        }

        static string ToAbsoluteProjectPath(string relativePath)
        {
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName
                ?? throw new InvalidOperationException("Unity project root could not be resolved.");
            return Path.GetFullPath(Path.Combine(projectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        static void RestoreSceneSetup(SceneSetup[] previousSceneSetup)
        {
            if (previousSceneSetup != null && previousSceneSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
            }
        }

        static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new BuildFailedException(message);
            }
        }

        [Serializable]
        sealed class TrainingProfileManifest
        {
            public int schemaVersion;
            public int policyContractVersion;
            public string generatedUtc;
            public string executable;
            public TrainingProfileEntry[] profiles;
        }

        [Serializable]
        sealed class TrainingProfileEntry
        {
            public string key;
            public string displayName;
            public string scene;
            public string sceneGuid;
            public string trainerConfig;
            public string trainerConfigSha256;
            public string redBehaviorName;
            public string navyBehaviorName;
            public string runIdPrefix;
            public int defaultBasePort;
            public int maxSteps;
            public bool trainRed;
            public bool trainNavy;
            public bool forceRedFallback;
            public bool forceNavyFallback;
            public bool selfPlay;
            public bool supportsTraining;
            public bool requiresNavyModel;
            public bool navyModelConfiguredInBuild;
            public string navyModelAsset;
            public string navyModelSha256;
        }

        [Serializable]
        sealed class TrainingBuildInfo
        {
            public int schemaVersion;
            public string generatedUtc;
            public string unityVersion;
            public string buildTarget;
            public string result;
            public long totalBytes;
            public int totalWarnings;
            public int totalErrors;
            public string executable;
            public string executableSha256;
            public string[] scenes;
        }
    }
}
