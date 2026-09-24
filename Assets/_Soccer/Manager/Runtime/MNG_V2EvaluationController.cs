using System;
using System.IO;
using MachineLearning.Soccer;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    // Frozen policies are supplied over the communicator by the ONNX evaluator.
    [DefaultExecutionOrder(-450)]
    public sealed class MNG_V2EvaluationController : MonoBehaviour
    {
        MNG_MatchController match;
        int seed, policyTeam;
        bool written;
        string output;
        void Awake()
        {
            match = GetComponent<MNG_MatchController>();
            if (!match.UseRuntimeV2) throw new InvalidOperationException("V2 evaluation only");
            seed = int.Parse(Argument("-mngEvaluationSeed", "20260922"));
            policyTeam = int.Parse(Argument("-mngPolicyTeam", "0"));
            var commonCandidate=Argument("-mngCandidateCommonRules","true")=="true";
            var commonOpponent=Argument("-mngOpponentCommonRules","true")=="true";
            match.ConfigureCommonRules(policyTeam==0?commonCandidate:commonOpponent,policyTeam==1?commonCandidate:commonOpponent);
            var neuralOpponent = Argument("-mngNeuralOpponent", "false") == "true";
            output = Argument("-mngEvaluationOutput", "");
            if (string.IsNullOrEmpty(output) || File.Exists(output)) throw new InvalidOperationException("New evaluation output required");
            foreach (var manager in GetComponentsInChildren<MNG_ManagerAgent>(true))
            {
                var active = neuralOpponent || (int)manager.Team == policyTeam;
                manager.GetComponent<BehaviorParameters>().BehaviorType = BehaviorType.Default;
                manager.GetComponent<BehaviorParameters>().Model = null;
                manager.enabled = active;
                manager.GetComponent<DecisionRequester>().enabled = active;
                manager.gameObject.SetActive(active);
            }
            foreach (var rule in GetComponentsInChildren<MNG_RuleBasedManager>(true))
                rule.enabled = !neuralOpponent && (int)rule.Team != policyTeam;
            foreach (var fallback in GetComponentsInChildren<MNG_FallbackManager>(true)) fallback.enabled = false;
            foreach (var human in GetComponentsInChildren<MNG_HumanInput>(true)) human.enabled = false;
            match.ConfigureSpawnSeedOffset(seed);
            match.ConfigureEvaluationMirror(Argument("-mngMirror", "false") == "true");
            match.ConfigureMatchDuration(300f, false, MNG_MatchFinishMode.TerminalResult);
            Time.timeScale = 20f;
        }
        void Start() => match.ResetMatch();
        void FixedUpdate()
        {
            if (written || match.State != MNG_MatchState.Finished) return;
            written = true;
            var result = new Result { seed = seed, policyTeam = policyTeam, redScore = match.RedScore,
                navyScore = match.NavyScore, elapsed = match.EpisodeElapsedSeconds,
                stallActivations = match.GlobalBallStallRecoveryActivations,
                episodeStallActivations = match.EpisodeStallActivations, episodeStallActiveSeconds = match.EpisodeStallActiveSeconds,
                mirror = match.EvaluationMirror, neutralFirstTeam = (int)match.EpisodeNeutralFirstTeam };
            for(var t=0;t<2;t++)
            {
                result.commonRulesEnabled[t]=match.CommonRulesEnabled((Team)t);
                result.commonPassAttempts[t]=match.CommonPassAttempts((Team)t);
                result.commonClearanceAttempts[t]=match.CommonClearanceAttempts((Team)t);
                result.commonNoTargetAttempts[t]=match.CommonNoTargetAttempts((Team)t);
                result.commonRuleStrikes[t]=match.CommonRuleStrikes((Team)t);
            }
            foreach (var manager in GetComponentsInChildren<MNG_ManagerAgent>())
            {
                if (!manager.isActiveAndEnabled) continue;
                var raw = new long[6]; var effective = new long[6];
                manager.CopyRawCommandCounts(raw); manager.CopyCommandCounts(effective);
                for (var i = 0; i < 6; i++) result.mismatches += Math.Abs(raw[i] - effective[i]);
                result.overrides += manager.BlockedForwardPassOverrideCount;
                result.directDecisionRewards += manager.ExplicitBlockedPassRewardCount;
                if ((int)manager.Team == policyTeam)
                {
                    result.commands = raw; manager.CopyMaskAvailabilityCounts(result.maskAvailability);
                    result.passAvailableActive = manager.PassAvailableActive;
                    result.passAvailableForwardBlocked = manager.PassAvailableForwardBlocked;
                }
            }
            var ball = GetComponentInChildren<MNG_BallControl>();
            for (var t = 0; t < 2; t++)
            { result.neutralTieWins[t] = ball.NeutralTieWins((Team)t); result.neutralEscapeGrants[t] = ball.NeutralEscapeGrants((Team)t); }
            var reward = GetComponent<MNG_RewardEngine>();
            var tracker = GetComponent<MNG_TacticalRewardTracker>();
            result.passStrikes = tracker.GetPassStrikeCount((Team)policyTeam);
            result.shotStrikes = tracker.GetShotStrikeCount((Team)policyTeam);
            result.intendedReceptions = tracker.GetIntendedReceptionCount((Team)policyTeam);
            result.observedRecoveries = tracker.GetObservedRecoveryCount((Team)policyTeam);
            for (var i = 0; i < 11; i++)
            {
                result.rawEvents[i] = reward.GetRawCount((Team)policyTeam, (MNG_RewardEventKind)i);
                result.rewardedEvents[i] = reward.GetRewardedCount((Team)policyTeam, (MNG_RewardEventKind)i);
            }
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            File.WriteAllText(output + ".tmp", JsonUtility.ToJson(result));
            File.Move(output + ".tmp", output);
        }
        static string Argument(string name, string fallback)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return fallback;
        }
        [Serializable] sealed class Result
        {
            public bool[] commonRulesEnabled=new bool[2];
            public long[] commonPassAttempts=new long[2],commonClearanceAttempts=new long[2],commonNoTargetAttempts=new long[2],commonRuleStrikes=new long[2];
            public string protocol = "MNG-V2-FROZEN-300-v2";
            public int seed, policyTeam, redScore, navyScore, stallActivations, passStrikes, shotStrikes, intendedReceptions;
            public float elapsed, episodeStallActiveSeconds;
            public int episodeStallActivations, neutralFirstTeam;
            public bool mirror;
            public string stallActivationsScope = "last-round only; use episodeStallActivations";
            public int[] neutralTieWins = new int[2], neutralEscapeGrants = new int[2];
            public long[] maskAvailability = new long[6];
            public long passAvailableActive, passAvailableForwardBlocked;
            public int observedRecoveries;
            public long mismatches, overrides, directDecisionRewards;
            public long[] commands, rawEvents = new long[11], rewardedEvents = new long[11];
        }
    }
}
