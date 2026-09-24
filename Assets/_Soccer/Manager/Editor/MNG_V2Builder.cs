using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using MachineLearning.Soccer;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MachineLearning.Soccer.Manager.Editor
{
    public static class MNG_V2Builder
    {
        public const string Root = "Assets/_Soccer/Manager/Curriculum/MS_V2";
        public const string TrainScene = Root + "/MNG_MS2V2_Train.unity";
        public const string SelfPlayScene = Root + "/MNG_MS3V2_Train.unity";
        public const string EvaluationScene = Root + "/MNG_V2_Evaluation.unity";

        public static void BuildAssetsBatch()
        {
            try
            {
                Directory.CreateDirectory(Root); AssetDatabase.Refresh();
                Create(TrainScene, false); Create(SelfPlayScene, true); Create(EvaluationScene, false);
                AssetDatabase.SaveAssets(); Validate();
                Debug.Log("MNG V2 ASSETS AND VALIDATION PASS"); EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void Create(string path, bool selfPlay)
        {
            if (File.Exists(path)) throw new InvalidOperationException("Preserve existing v2 scene: " + path);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MNG_ProjectBuilder.ManagerPrefabPath);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var match = root.GetComponent<MNG_MatchController>();
            match.ConfigureRuntimeV2(true);
            foreach (var manager in root.GetComponentsInChildren<MNG_ManagerAgent>(true))
            {
                var active = selfPlay || manager.Team == Team.Red;
                manager.gameObject.SetActive(active); manager.enabled = active;
                manager.GetComponent<DecisionRequester>().enabled = active;
                var behavior = manager.GetComponent<BehaviorParameters>();
                behavior.Model = null;
                behavior.BehaviorName = MNG_RuntimeV2.BehaviorName;
                behavior.BehaviorType = BehaviorType.Default;
                behavior.TeamId = (int)manager.Team;
                behavior.BrainParameters.VectorObservationSize = MNG_RuntimeV2.ObservationSize;
                manager.ConfigurePolicyAssistMode(MNG_PolicyAssistMode.None);
                manager.ConfigureUniformRandomHeuristic(20260922 + (int)manager.Team);
            }
            foreach (var fallback in root.GetComponentsInChildren<MNG_FallbackManager>(true)) fallback.enabled = false;
            foreach (var human in root.GetComponentsInChildren<MNG_HumanInput>(true)) human.enabled = false;
            foreach (var rule in root.GetComponentsInChildren<MNG_RuleBasedManager>(true)) rule.enabled = !selfPlay && rule.Team == Team.Navy;
            var profile = AssetDatabase.LoadAssetAtPath<MNG_MSOpponentProfile>(MNG_MSBuilder.FullProfilePath);
            root.AddComponent<MNG_MSController>().Configure(selfPlay ? MNG_MSMode.SelfPlay : MNG_MSMode.R0Opponent,
                selfPlay ? null : profile, match, 300f, 10f);
            root.AddComponent<MNG_V2Trace>();
            if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Scene save failed: " + path);
        }

        public static void Validate()
        {
            foreach (var path in new[] { TrainScene, SelfPlayScene, EvaluationScene })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single();
                if (!root.GetComponent<MNG_MatchController>().UseRuntimeV2) throw new InvalidOperationException("V2 runtime missing.");
                var ms = root.GetComponent<MNG_MSController>();
                if (ms.EpisodeSeconds != 300f) throw new InvalidOperationException("All v2 matches must be 300 seconds.");
                foreach (var manager in root.GetComponentsInChildren<MNG_ManagerAgent>(true))
                {
                    var b = manager.GetComponent<BehaviorParameters>();
                    MNG_RuntimeV2.ValidateModelContract(MNG_RuntimeV2.ObservationSchema, b.BehaviorName, b.BrainParameters.VectorObservationSize);
                    if (b.Model != null) throw new InvalidOperationException("Fresh v2 build must not select a model.");
                    if (b.TeamId != (int)manager.Team) throw new InvalidOperationException("Incorrect self-play team id.");
                }
            }
        }

        public static void BuildPlayersBatch()
        {
            try
            {
                Validate();
                Build(TrainScene, "MS2V2"); Build(SelfPlayScene, "MS3V2"); Build(EvaluationScene, "EvaluationV2");
                Debug.Log("MNG V2 WINDOWS PLAYERS PASS"); EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void BuildFormalPlayersBatch()
        {
            try
            {
                Validate();
                var scene = EditorSceneManager.OpenScene(EvaluationScene, OpenSceneMode.Single);
                var root = scene.GetRootGameObjects().Single();
                UnityEngine.Object.DestroyImmediate(root.GetComponent<MNG_MSController>());
                root.AddComponent<MNG_V2EvaluationController>();
                var eval = Root + "/MNG_V2_FrozenEvaluation.unity";
                if (File.Exists(eval)) throw new InvalidOperationException("Preserve existing frozen evaluation scene");
                EditorSceneManager.SaveScene(scene, eval);
                Build(TrainScene, "MS2V2", "Builds/MNG_V2/R5R6-20260922/");
                Build(SelfPlayScene, "MS3V2", "Builds/MNG_V2/R5R6-20260922/");
                Build(eval, "EvaluationV2", "Builds/MNG_V2/R5R6-20260922/");
                Debug.Log("MNG V2 FORMAL PLAYERS PASS"); EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        static void Build(string scene, string name, string prefix = "Builds/MNG_V2/")
        {
            var directory = prefix + name;
            if (Directory.Exists(directory)) throw new InvalidOperationException("Preserve existing build: " + directory);
            Directory.CreateDirectory(directory);
            var executable = directory + "/MNG_" + name + ".exe";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { scene },
                locationPathName = executable, target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
                throw new InvalidOperationException("V2 build failed: " + name);
            var data = directory + "/MNG_" + name + "_Data";
            File.WriteAllText(directory + "/build-info.json", JsonUtility.ToJson(new BuildInfo {
                executableSha256 = Sha(executable), levelSha256 = Sha(data + "/level0"),
                runtimeSha256 = Sha(data + "/Managed/MNG.Runtime.dll"), scene = scene }, true));
        }
        static string Sha(string path) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(path))).Replace("-", "").ToLowerInvariant(); }
        public static void BuildPostR6EvaluationBatch()
        {
            try
            {
                Validate();
                var args = Environment.GetCommandLineArgs();
                var index = Array.IndexOf(args, "-mngBuildRoot");
                var prefix = index >= 0 && index + 1 < args.Length ? args[index + 1].TrimEnd('/', '\\') + "/" : "Builds/MNG_V2/PostR6-20260923-r1/";
                Build(Root + "/MNG_V2_FrozenEvaluation.unity", "EvaluationV2", prefix);
                Debug.Log("POST R6 VALIDATION AND FROZEN PLAYER PASS"); EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void BuildFormalPlayersR2Batch()
        {
            try
            {
                Validate();
                const string prefix = "Builds/MNG_V2/R5R6-20260922-r2/";
                Build(TrainScene, "MS2V2", prefix); Build(SelfPlayScene, "MS3V2", prefix);
                Build(Root + "/MNG_V2_FrozenEvaluation.unity", "EvaluationV2", prefix);
                Debug.Log("MNG V2 FORMAL R2 PLAYERS PASS"); EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void BuildStabilityPreparationBatch()
        {
            try
            {
                Validate();
                var args=Environment.GetCommandLineArgs();
                var index=Array.IndexOf(args,"-mngBuildRoot");
                if(index<0 || index+1>=args.Length) throw new InvalidOperationException("Explicit new build root required");
                var prefix=args[index+1].TrimEnd('/','\\')+"/";
                Build(SelfPlayScene,"MS3V2",prefix);
                Build(Root+"/MNG_V2_FrozenEvaluation.unity","EvaluationV2",prefix);
                Debug.Log("MS3 STABILITY PREPARATION PLAYERS PASS"); EditorApplication.Exit(0);
            }
            catch(Exception e) {Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [Serializable] sealed class BuildInfo
        {
            public string schema = MNG_RuntimeV2.ObservationSchema;
            public string behavior = MNG_RuntimeV2.BehaviorName;
            public int observations = 244, taskVersion = 2, eventVersion = 2, protocolVersion = MNG_RuntimeV2.ProtocolVersion;
            public string environmentRevision = MNG_RuntimeV2.EnvironmentRevision;
            public string executableSha256, levelSha256, runtimeSha256, scene;
            public string candidateStatus = "smoke-only-untrained";
        }
    }
}
