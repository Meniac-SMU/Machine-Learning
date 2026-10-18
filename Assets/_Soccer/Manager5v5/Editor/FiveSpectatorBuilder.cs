using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5.Editor
{
    public static class FiveSpectatorBuilder
    {
        public static void BuildBatch()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                var path = args[Array.IndexOf(args, "-fivePlayerPath") + 1];
                if (File.Exists(path)) throw new IOException("Preserve existing display builds.");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var arena = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FiveBuilder.Prefab), scene);
                var match = arena.GetComponent<FiveMatch>();
                var session = FiveLearningFactory.Create(match, true, true);
                var spectator = arena.AddComponent<FiveSpectator>();
                spectator.session = session;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(.72f, .75f, .8f);
                const string scenePath = FiveBuilder.Root + "/Scenes/FiveSpectator.unity";
                EditorSceneManager.SaveScene(scene, scenePath);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] {scenePath}, locationPathName = path,
                    target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development });
                if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) throw new Exception("Display build failed");
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
