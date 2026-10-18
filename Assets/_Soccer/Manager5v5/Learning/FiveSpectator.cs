using System;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager5v5
{
    // Display-only controls; the existing session owns observations, actions and physics.
    public sealed class FiveSpectator : MonoBehaviour
    {
        public FiveLearningSession session;
        string output;
        bool finished;
        float nextReceipt;
        string redLabel, navyLabel;
        [Serializable] class Receipt
        {
            public string playerRuntime = FiveMatch.PlayerRuntimeVersion;
            public float elapsed, timeScale;
            public int redScore, navyScore, headBars;
            public bool finished;
            public float keeperAccelerationMultiplier;
            public string redLabel, navyLabel;
        }
        void Start()
        {
            var args = Environment.GetCommandLineArgs();
            output = args[Array.IndexOf(args, "--five-display-receipt") + 1];
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Time.timeScale = 1;
            var root = FindFirstObjectByType<UIDocument>().rootVisualElement;
            redLabel = Label(args, "--five-red-step", "학습 모델");
            navyLabel = session.RuleOpponentTeam == 1 ? "규칙형 · strong-v11" : Label(args, "--five-navy-step", "학습 모델");
            root.Q<DropdownField>("red-mode").SetValueWithoutNotify(redLabel);
            root.Q<DropdownField>("navy-mode").SetValueWithoutNotify(navyLabel);
            root.Q<DropdownField>("red-mode").SetEnabled(false);
            root.Q<DropdownField>("navy-mode").SetEnabled(false);
            root.Q<Button>("restart").SetEnabled(false);
        }
        static string Label(string[] args, string flag, string fallback)
        {
            int index = Array.IndexOf(args, flag);
            return index >= 0 && index + 1 < args.Length && int.TryParse(args[index + 1], out int step)
                ? $"학습 모델 · {step:N0}회" : fallback;
        }
        void LateUpdate()
        {
            if (finished) return;
            var match = session.match;
            finished = match.State == FiveMatchState.Finished;
            if (finished) session.automaticSteps = false;
            if (!finished && Time.unscaledTime < nextReceipt) return;
            nextReceipt = Time.unscaledTime + 2;
            File.WriteAllText(output, JsonUtility.ToJson(new Receipt { elapsed = match.Elapsed, timeScale = Time.timeScale,
                redScore = match.RedScore, navyScore = match.NavyScore, finished = finished,
                headBars = FindFirstObjectByType<FiveHud>().BarCount,
                keeperAccelerationMultiplier = match.profile.keeperAccelerationMultiplier,
                redLabel = redLabel, navyLabel = navyLabel }, true));
        }
    }
}
