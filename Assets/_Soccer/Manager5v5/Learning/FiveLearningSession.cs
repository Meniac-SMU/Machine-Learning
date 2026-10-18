using System;
using System.IO;
using System.Linq;
using Unity.MLAgents;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    [DefaultExecutionOrder(-250)]
    public sealed class FiveLearningSession : MonoBehaviour
    {
        public FiveMatch match;
        public FiveManagerAgent[] agents;
        public bool automaticSteps = true;
        public bool stopAfterFirstMatch;
        public string shutdownFile;
        bool shutdownRequested;
        public bool ShutdownRequested
        {
            get
            {
                if(!shutdownRequested && !string.IsNullOrEmpty(shutdownFile))shutdownRequested=File.Exists(shutdownFile);
                return shutdownRequested;
            }
        }
        public int Episode { get; private set; }
        public int CompletedMatches { get; private set; }
        public bool Initialized { get; private set; }
        public string RunId { get; private set; }
        public readonly FiveScoreReward Rewards = new FiveScoreReward();
        public readonly FiveObservationHistory History = new FiveObservationHistory();
        public int RuleOpponentTeam { get; private set; } = -1;
        public event Action<FiveLearningTrace> Trace;
        bool previousAcademyStepping, priming, hasDecisions, terminalFlushed;
        SimulationMode previousPhysics;
        static FiveLearningSession active;
        StreamWriter writer;

        void Awake()
        {
            var args = Environment.GetCommandLineArgs();
            // Measurement switch for matched before/after evaluation; normal play uses the fix.
            var keeperIndex=Array.IndexOf(args,"--five-keeper-turn-first");
            if(keeperIndex>=0)
            {
                if(keeperIndex+1>=args.Length || !new[]{"on","off"}.Contains(args[keeperIndex+1]))
                    throw new ArgumentException("Keeper turn-first mode must be on or off.");
                match.keeperTurnFirstEnabled=args[keeperIndex+1]=="on";
            }
            stopAfterFirstMatch = stopAfterFirstMatch || Array.IndexOf(args,"--five-evaluate-once")>=0;
            var shutdownIndex=Array.IndexOf(args,"--five-shutdown-file");
            if(shutdownIndex>=0)
            {
                if(shutdownIndex+1>=args.Length)throw new ArgumentException("Missing shutdown file path.");
                shutdownFile=args[shutdownIndex+1];
            }
            var executionIndex=Array.IndexOf(args,"--five-execution-variant");
            if(executionIndex>=0)
            {
                if(executionIndex+1>=args.Length)throw new ArgumentException("Missing execution variant.");
                var execution=args[executionIndex+1];
                if(!new[]{"legacy","pass","cutback","block","all"}.Contains(execution))
                    throw new ArgumentException("Unknown execution variant.");
                match.improvedPassPreparationEnabled=execution!="legacy";
                match.expandedCutbackEnabled=execution=="cutback"||execution=="all";
                match.adaptiveGoalBlockEnabled=execution=="block"||execution=="all";
            }
            var index = Array.IndexOf(args, "--five-rule-team");
            if (index < 0) return;
            if (index + 1 >= args.Length || !int.TryParse(args[index + 1], out var team) || team < 0 || team > 1)
                throw new ArgumentException("Rule opponent team must be 0 or 1.");
            RuleOpponentTeam = team;
            var variantIndex=Array.IndexOf(args,"--five-rule-variant");
            if(variantIndex>=0)
            {
                if(variantIndex+1>=args.Length || (args[variantIndex+1]!="legacy"&&args[variantIndex+1]!="strong"))
                    throw new ArgumentException("Rule variant must be legacy or strong.");
                match.ruleVariants[team]=args[variantIndex+1]=="legacy"?FiveRuleVariant.Legacy:FiveRuleVariant.Strong;
            }
            // Session execution order precedes Agent initialization. Only the explicitly
            // selected opponent uses the explicitly chosen rule variant.
            agents[team].GetComponent<Unity.MLAgents.Policies.BehaviorParameters>().BehaviorType =
                Unity.MLAgents.Policies.BehaviorType.HeuristicOnly;
        }

        void Start() { if (automaticSteps) InitializeSession(); }
        void FixedUpdate() { if (automaticSteps && Initialized) Step(Time.fixedDeltaTime); }

        public void InitializeSession()
        {
            if (Initialized) return;
            if (active != null && active != this) throw new InvalidOperationException("P8 is one arena per process.");
            if (match == null || agents == null || agents.Length != 2 || agents[0].team != Team.Red || agents[1].team != Team.Navy
                || agents[0].session != this || agents[1].session != this)
                throw new InvalidOperationException("Expected two correctly bound managers.");
            match.Validate();
            if (match.profile.decisionSeconds != 3f || match.profile.duration != 300f)
                throw new InvalidOperationException("P8 locks the P7 three-second / 300-second contract.");
            active = this; match.automaticTick = false;
            match.ruleManagers = new[] {false, false};
            previousPhysics = Physics.simulationMode; Physics.simulationMode = SimulationMode.Script;
            previousAcademyStepping = Academy.Instance.AutomaticSteppingEnabled;
            Academy.Instance.AutomaticSteppingEnabled = false;
            Academy.Instance.OnEnvironmentReset += OnEnvironmentReset;
            RunId = Guid.NewGuid().ToString("N"); Initialized = true;
            var arguments = Environment.GetCommandLineArgs();
            var traceIndex = Array.IndexOf(arguments, "--five-p8-trace");
            var traceDirectoryIndex = Array.IndexOf(arguments, "--five-p9-trace-dir");
            if (traceIndex >= 0 && traceDirectoryIndex >= 0) throw new ArgumentException("Choose one trace destination.");
            if (traceIndex >= 0)
            {
                if (traceIndex + 1 >= arguments.Length) throw new ArgumentException("Missing P8 trace path.");
                OpenTrace(arguments[traceIndex + 1]);
            }
            if (traceDirectoryIndex >= 0)
            {
                if (traceDirectoryIndex + 1 >= arguments.Length) throw new ArgumentException("Missing P9 trace directory.");
                var directory = arguments[traceDirectoryIndex + 1];
                Directory.CreateDirectory(directory);
                OpenTrace(Path.Combine(directory, "trace-" + RunId + ".jsonl"));
            }
            BeginMatch();
            priming = true;
            Academy.Instance.EnvironmentStep(); // Complete the Academy's initial reset before requesting actions.
            priming = false;
        }
        void OnEnvironmentReset()
        {
            if (!Initialized || priming) return;
            // SDK force-reset does not clear cumulative rewards. Close an unfinished
            // episode explicitly before its shared state is replaced.
            if (hasDecisions && match.State != FiveMatchState.Finished)
                foreach (var agent in agents)
                {
                    agent.PrepareTerminal();
                    Emit("interrupted", (int)agent.team, -1, agent.GetCumulativeReward(), agent.Observation);
                    agent.EpisodeInterrupted();
                }
            BeginMatch();
        }
        void BeginMatch()
        {
            match.ResetMatch(); Rewards.Begin(match); History.Reset(match); hasDecisions = false; Episode++;
            Emit("reset", -1, -1, 0, null);
        }
        public void Step(float seconds)
        {
            if (!Initialized) throw new InvalidOperationException("Initialize the learning session first.");
            if (shutdownRequested) return;
            if (!float.IsFinite(seconds) || seconds <= 0 || seconds > .02f)
                throw new ArgumentOutOfRangeException(nameof(seconds), "P8 steps must be at most 20 ms.");
            if (stopAfterFirstMatch && CompletedMatches>0)
            {
                // Flush the final terminal packet without requesting a new match's actions.
                // The remote policy can return default actions while Application.Quit is pending.
                automaticSteps=false;
                if (!terminalFlushed)
                {
                    terminalFlushed=true;
                    Academy.Instance.EnvironmentStep();
                }
                return;
            }
            if (match.Paused) return;
            if (match.State == FiveMatchState.Finished) BeginMatch();
            bool request = false;
            foreach (var agent in agents)
                if (FiveLearningContract.CanDecide(match, agent.team)) { agent.PrepareDecision(); request = true; }
            if (request)
            {
                var requestedEpisode = Episode;
                Academy.Instance.EnvironmentStep();
                if(ShutdownRequested){automaticSteps=false;return;}
                // Python reset is handled inside EnvironmentStep and invalidates its pending
                // decisions. Do not move the reset world before its first fresh action arrives.
                if (Episode != requestedEpisode) return;
                foreach (var agent in agents)
                    if (agent.DecisionPending) throw new InvalidOperationException("Policy did not return a requested action.");
            }
            var previousState = match.State;
            match.SimulatePhysicsStep(seconds);
            if (previousState == FiveMatchState.GoalPause && match.State == FiveMatchState.Playing)
                History.Reset(match);
            else History.Record(match);
            var delta = Rewards.Consume(match);
            if (delta != 0)
            {
                agents[0].AddReward(delta); agents[1].AddReward(-delta);
                Emit("score-reward", -1, -1, delta, null);
            }
            if (match.State == FiveMatchState.Finished)
            {
                foreach (var agent in agents)
                {
                    agent.PrepareTerminal();
                    Emit("terminal", (int)agent.team, -1, agent.GetCumulativeReward(), agent.Observation);
                    agent.EndEpisode();
                }
                CompletedMatches++;
            }
        }
        public void RecordDecision(FiveManagerAgent agent, int action)
        {
            hasDecisions = true;
            Emit("decision", (int)agent.team, action, agent.GetCumulativeReward(), agent.Observation);
        }
        void Emit(string kind, int team, int action, float reward, float[] observation)
        {
            var row = new FiveLearningTrace { runId = RunId, episode = Episode, kind = kind, team = team, action = action,
                elapsed = match.Elapsed, reward = reward, redScore = match.RedScore, navyScore = match.NavyScore,
                nextDecisionAt = team < 0 ? 0 : match.Decision((Team)team).nextDecisionAt,
                observation = observation,
                ruleOpponentTeam = RuleOpponentTeam, boundaryCorrections = match.BoundaryCorrections,
                executionFeatures = new[]{match.improvedPassPreparationEnabled,match.expandedCutbackEnabled,match.adaptiveGoalBlockEnabled},
                keeperTurnFirstEnabled = match.keeperTurnFirstEnabled,
                ruleVariant = RuleOpponentTeam<0?"none":match.ruleVariants[RuleOpponentTeam]==FiveRuleVariant.Legacy?"legacy":FiveStrongRule.Version,
                ruleDecision = kind=="decision"&&team==RuleOpponentTeam?match.RuleDiagnostic((Team)team):null,
                stallSeconds = match.ObservedStallSeconds,
                passAttempts = match.PassAttempts, passReceptions = match.PassReceptions, shots = match.Shots,
                matchEvents = kind=="terminal"&&team==0?match.Events.ToArray():null,
                tacticPhysicsSteps = kind=="terminal"?(int[])match.tacticSteps.Clone():null,
                defensiveFormationSteps = (int[])match.defensiveFormationSteps.Clone(),
                defensiveFormationEntries = (int[])match.defensiveFormationEntries.Clone(),
                consecutiveRecover = team<0?0:match.Decision((Team)team).consecutiveRecover,
                recoverBlockedSelections = team<0?0:match.Decision((Team)team).recoverBlockedSelections,
                consecutiveLongPass = team<0?0:match.Decision((Team)team).consecutiveLongPass,
                longPassBlockedSelections = team<0?0:match.Decision((Team)team).longPassBlockedSelections,
                commandConsecutiveSelections = team<0?null:(int[])match.Decision((Team)team).consecutiveSelections.Clone(),
                commandBlockedSelections = team<0?null:(int[])match.Decision((Team)team).blockedSelections.Clone(),
                contestEpisodes = match.Events.Count(e=>e.kind=="contest-start"),
                frontCushionsByTeam = new[]{0,1}.Select(t=>match.Events.Count(e=>e.kind=="front-cushion"&&e.actor>=t*5&&e.actor<t*5+5)).ToArray(),
                reinforcementsByTeam = new[]{0,1}.Select(t=>match.Events.Count(e=>e.kind=="contest-reinforce"&&e.actor>=t*5&&e.actor<t*5+5)).ToArray(),
                challengeRoutesByTeam = new[]{0,1}.Select(t=>match.Events.Count(e=>e.kind=="challenge-route"&&e.actor>=t*5&&e.actor<t*5+5)).ToArray(),
                carrierEscapesByTeam = new[]{0,1}.Select(t=>match.Events.Count(e=>e.kind=="carrier-escape"&&e.actor>=t*5&&e.actor<t*5+5)).ToArray(),
                contestActionsByTeam = new[]{0,1}.Select(t=>match.Events.Count(e=>e.actor>=t*5&&e.actor<t*5+5&&
                    (e.kind=="contest-backoff"||e.kind=="contest-reangle"||e.kind=="contest-side-carry"||e.kind=="contest-outlet-pass"||e.kind=="contest-make-space"))).ToArray(),
                sampleTimes = team < 0 ? null : agents[team].HistoryObservation?.sampleTimes,
                sampleErrors = team < 0 ? null : agents[team].HistoryObservation?.sampleErrors };
            Trace?.Invoke(row);
            if (writer != null) { writer.WriteLine(JsonUtility.ToJson(row)); writer.Flush(); }
        }
        public void OpenTrace(string path)
        {
            if (writer != null) throw new InvalidOperationException("Trace already open.");
            writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
        }
        void OnDisable()
        {
            writer?.Dispose(); writer = null;
            if (!Initialized) return;
            if (Academy.IsInitialized)
            {
                Academy.Instance.OnEnvironmentReset -= OnEnvironmentReset;
                Academy.Instance.AutomaticSteppingEnabled = previousAcademyStepping;
            }
            Physics.simulationMode = previousPhysics;
            active = null; Initialized = false;
        }
    }

    [Serializable] public sealed class FiveLearningTrace
    {
        public string contract = FiveLearningContract.Version, lineage = FiveLearningContract.Lineage;
        public string playerRuntime = FiveMatch.PlayerRuntimeVersion;
        public bool[] executionFeatures;
        public bool keeperTurnFirstEnabled;
        public int contestEpisodes;
        public int[] contestActionsByTeam;
        public int[] frontCushionsByTeam;
        public int[] reinforcementsByTeam,challengeRoutesByTeam;
        public int[] carrierEscapesByTeam;
        public string runId, kind;
        public int episode, team, action, redScore, navyScore;
        public int ruleOpponentTeam, boundaryCorrections;
        public string ruleVariant;
        public FiveRuleDecision ruleDecision;
        public float stallSeconds;
        public int passAttempts,passReceptions,shots;
        public FiveEvent[] matchEvents;
        public int[] tacticPhysicsSteps;
        public int[] defensiveFormationSteps, defensiveFormationEntries;
        public int consecutiveRecover;
        public int recoverBlockedSelections;
        public int consecutiveLongPass;
        public int longPassBlockedSelections;
        public int[] commandConsecutiveSelections,commandBlockedSelections;
        public float elapsed, reward, nextDecisionAt;
        public float[] observation;
        public float[] sampleTimes, sampleErrors;
    }
}
