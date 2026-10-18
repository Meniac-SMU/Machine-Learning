using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager5v5.Editor
{
    public static class FiveLearningBuilder
    {
        public const string ScenePath = FiveBuilder.Root + "/Scenes/FiveLearning.unity";
        public static void BuildBatch()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(args, "-fivePlayerPath");
                if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("A fresh -fivePlayerPath is required.");
                var path = Path.GetFullPath(args[index + 1]);
                var allowed = Path.GetFullPath("Builds/Manager5v5") + Path.DirectorySeparatorChar;
                if (!path.StartsWith(allowed, StringComparison.OrdinalIgnoreCase) || File.Exists(path)) throw new IOException("Preserve existing builds.");
                BuildScene();
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] {ScenePath},
                    locationPathName = path, target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new InvalidOperationException("P8 build failed.");
                Debug.Log("MNG5 P8 PLAYER BUILD PASS"); EditorApplication.Exit(0);
            }
            catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
        }
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var arena = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FiveBuilder.Prefab), scene);
            var match = arena.GetComponent<FiveMatch>(); match.automaticTick = false; match.ruleManagers = new[] {false, false};
            // Training ABI has no human-control authority. Keep the reviewed P7 scene and prefab untouched.
            foreach (var hud in arena.GetComponentsInChildren<FiveHud>(true)) hud.gameObject.SetActive(false);
            FiveLearningFactory.Create(match, true, true);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.72f, .75f, .8f);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Directory.CreateDirectory(FiveBuilder.Evidence);
            var schema = new Schema { featureNames = FiveLearningContract.FeatureNames(), commands = FiveHud.CommandNames };
            File.WriteAllText(Path.Combine(FiveBuilder.Evidence, "learning-schema.json"), JsonUtility.ToJson(schema, true));
            AssetDatabase.SaveAssets();
        }
        [Serializable] sealed class Schema
        {
            public string contract = FiveLearningContract.Version, behaviorName = FiveLearningContract.BehaviorName, lineage = FiveLearningContract.Lineage;
            public int observationSize = FiveLearningContract.ObservationSize, stackedObservations = 1, continuousActions = 0;
            public int frameSize = FiveLearningContract.FrameSize, historyFrames = FiveLearningContract.FrameCount;
            public float[] historyOffsetsSeconds = {0, .25f, .5f, .75f};
            public float maximumSampleErrorSeconds = FiveLearningContract.MaximumSampleError;
            public string samplingRule = "latest simulation snapshot at or before target; clear on match/goal restart/external reset";
            public int[] discreteBranches = {8};
            public float decisionSeconds = 3, episodeSeconds = 300, goalReward = 1, concededReward = -1;
            public float otherReward = 0, terminalBonus = 0;
            public bool actionMasks = true, trainingStarted = false, existingModelAllowed = false;
            public string[] featureNames, commands;
        }
    }
}
