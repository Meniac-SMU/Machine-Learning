using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace MachineLearning.Soccer
{
    [DefaultExecutionOrder(-32000)]
    public sealed class SoccerTrainingBootstrap : MonoBehaviour
    {
        const string ProfileArgument = "--training-profile";
        bool showSelector;
        bool loading;

        void Awake()
        {
            if (TryReadProfileArgument(Environment.GetCommandLineArgs(), out var profileKey, out var error))
            {
                LoadProfile(profileKey);
                return;
            }

            if (!string.IsNullOrEmpty(error))
            {
                FailAndQuit(error);
                return;
            }

            showSelector = !Application.isBatchMode && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null;
            if (!showSelector)
            {
                FailAndQuit(
                    $"Headless training requires {ProfileArgument} <profile>. Valid profiles: " +
                    SoccerTrainingProfileCatalog.ValidKeys);
            }
        }

        void OnGUI()
        {
            if (!showSelector || loading)
            {
                return;
            }

            const float width = 520f;
            const float rowHeight = 42f;
            const float margin = 24f;
            var height = 94f + SoccerTrainingProfileCatalog.Profiles.Count * rowHeight;
            var panel = new Rect(
                Mathf.Max(margin, (Screen.width - width) * 0.5f),
                Mathf.Max(margin, (Screen.height - height) * 0.5f),
                width,
                height);
            GUI.Box(panel, "Soccer Training Profile");
            GUI.Label(
                new Rect(panel.x + 20f, panel.y + 30f, panel.width - 40f, 36f),
                "Choose one Stadium profile. Rule is evaluation-only and does not connect to a Trainer.");

            var y = panel.y + 70f;
            foreach (var profile in SoccerTrainingProfileCatalog.Profiles)
            {
                var label = $"{profile.DisplayName}  [{profile.Key}]";
                if (GUI.Button(new Rect(panel.x + 20f, y, panel.width - 40f, 34f), label))
                {
                    LoadProfile(profile.Key);
                }

                y += rowHeight;
            }
        }

        public static bool TryReadProfileArgument(string[] args, out string profileKey, out string error)
        {
            profileKey = null;
            error = null;
            if (args == null)
            {
                return false;
            }

            for (var index = 0; index < args.Length; index++)
            {
                var argument = args[index] ?? string.Empty;
                string value = null;
                if (argument.StartsWith(ProfileArgument + "=", StringComparison.OrdinalIgnoreCase))
                {
                    value = argument.Substring(ProfileArgument.Length + 1);
                }
                else if (string.Equals(argument, ProfileArgument, StringComparison.OrdinalIgnoreCase))
                {
                    if (index + 1 >= args.Length || (args[index + 1] ?? string.Empty).StartsWith("--", StringComparison.Ordinal))
                    {
                        error = $"{ProfileArgument} requires a profile value.";
                        return false;
                    }

                    value = args[++index];
                }
                else
                {
                    continue;
                }

                if (profileKey != null)
                {
                    error = $"{ProfileArgument} was specified more than once.";
                    return false;
                }

                profileKey = value?.Trim();
            }

            if (profileKey == null)
            {
                return false;
            }

            if (profileKey.Length == 0)
            {
                error = $"{ProfileArgument} requires a non-empty profile value.";
                return false;
            }

            if (!SoccerTrainingProfileCatalog.TryGet(profileKey, out var profile))
            {
                error = $"Unknown training profile '{profileKey}'. Valid profiles: {SoccerTrainingProfileCatalog.ValidKeys}";
                return false;
            }

            profileKey = profile.Key;
            return true;
        }

        void LoadProfile(string profileKey)
        {
            if (loading)
            {
                return;
            }

            if (!SoccerTrainingProfileCatalog.TryGet(profileKey, out var profile))
            {
                FailAndQuit($"Unknown training profile '{profileKey}'. Valid profiles: {SoccerTrainingProfileCatalog.ValidKeys}");
                return;
            }

            loading = true;
            showSelector = false;
            Debug.Log(
                $"SOCCER TRAINING PROFILE key={profile.Key} scene={profile.SceneName} " +
                $"redTraining={profile.TrainRed} navyTraining={profile.TrainNavy} " +
                $"redFallback={profile.ForceRedFallback} navyFallback={profile.ForceNavyFallback}");
            SceneManager.LoadScene(profile.SceneName, LoadSceneMode.Single);
        }

        static void FailAndQuit(string message)
        {
            Debug.LogError("SOCCER TRAINING BOOTSTRAP ERROR: " + message);
            Application.Quit(2);
        }
    }
}
