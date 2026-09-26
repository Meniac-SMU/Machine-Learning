using System;
using System.IO;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    /// <summary>Bounded diagnostic trace, with aggregate records at episode boundaries.</summary>
    [DefaultExecutionOrder(150)]
    public sealed class MNG_V2Trace : MonoBehaviour
    {
        const int Capacity = 512;
        readonly Entry[] m_Ring = new Entry[Capacity];
        int m_Count, m_Next;
        long m_LastEpisode = -1;
        long m_Sequence;
        long m_SummaryEpisode = -1;
        float m_NextSummary;
        readonly long[] m_SourceTicks = new long[4];
        readonly long[] m_TeamSourceTicks = new long[8];
        readonly long[] m_TeamTaskResults = new long[18];
        readonly long[] m_AppliedTaskIds = new long[8];
        readonly float[] m_AppliedTaskTimes = new float[8];
        readonly int[] m_KickDelayCounts = new int[2];
        readonly float[] m_KickDelaySums = new float[2];
        MNG_MatchController m_Match;
        MNG_BallControl m_Ball;
        MNG_RewardEngine m_Reward;
        string m_Output;
        bool m_Full, m_Stream;
        readonly PassContext[] m_PassContexts = new PassContext[2];
        [Serializable] public struct PassContext
        {
            public int version, receiver;
            public long tick;
            public bool ownPossession, goalPause, forwardBlocked, hasTarget;
            public float targetDistance, laneClearance;
        }
        public static PassContext CapturePassContext(MNG_MatchSnapshot snapshot, Team team, MNG_TeamDecisionState decision)
        {
            var context = new PassContext { version = 1, tick = snapshot.TickId,
                goalPause = snapshot.GoalPauseActive, receiver = decision.PendingPassReceiverSlot,
                targetDistance = -1f, laneClearance = -1f,
                ownPossession = snapshot.Carrier.IsValid && snapshot.Carrier.Team == team };
            if (!context.ownPossession) return context;
            context.forwardBlocked = MNG_TacticalTargetResolver.IsForwardDribbleBlocked(snapshot, team, snapshot.Carrier.Slot);
            context.hasTarget = decision.HasPassTarget && context.receiver >= 0 && context.receiver < 4;
            if (!context.hasTarget) return context;
            var origin = snapshot.GetPlayer(team, snapshot.Carrier.Slot).Position;
            var target = MNG_TeamPlanner.SelectPassTarget(snapshot, team, context.receiver);
            context.targetDistance = Vector2.Distance(origin, target);
            var clearance = MNG_TacticalTargetResolver.NearestOpponentToSegment(snapshot, team, origin, target);
            // No active opponents: -1 means unbounded, never a measured zero clearance.
            context.laneClearance = float.IsPositiveInfinity(clearance) ? -1f : clearance;
            return context;
        }
        public void RecordPassContext(MNG_MatchSnapshot snapshot, Team team, MNG_TeamDecisionState decision)
            => m_PassContexts[(int)team] = CapturePassContext(snapshot, team, decision);
        [Serializable] sealed class SpawnRecord
        {
            public long episode; public int kickoff; public bool mirror;
            public float episodeTime, simulationFixedTime;
            public Vector3[] positions = new Vector3[8], forwards = new Vector3[8];
        }
        public void RecordSpawn(long episode, int kickoff)
        {
            if (string.IsNullOrEmpty(m_Output)) return;
            var row = new SpawnRecord { episode = episode, kickoff = kickoff, mirror = m_Match.EvaluationMirror,
                episodeTime = m_Match.EpisodeElapsedSeconds, simulationFixedTime = Time.fixedTime };
            for (var team = 0; team < 2; team++) for (var slot = 0; slot < 4; slot++)
            {
                var avatar = m_Match.GetPlayerAvatar((Team)team, slot);
                row.positions[team * 4 + slot] = avatar.Body.position;
                row.forwards[team * 4 + slot] = avatar.Body.rotation * Vector3.forward;
            }
            Directory.CreateDirectory(m_Output);
            File.AppendAllText(Path.Combine(m_Output, MNG_MSController.EvidenceFileName("spawns")), JsonUtility.ToJson(row) + "\n");
        }
        [Serializable] public struct Entry
        {
            public long sequence, episode, tick, taskId, parentCommandId, kickId;
            public long observationTick;
            public int rawCommand, effectiveCommand, maskBits;
            public int team, slot, intendedReceiver;
            public string kind, source;
            public string matchState;
            public float time;
            public PassContext passContext;
        }
        [Serializable] sealed class Report
        {
            public string schema = MNG_RuntimeV2.ObservationSchema;
            public string run, build;
            public int worker;
            public long episode;
            public float elapsedSeconds;
            public bool completed;
            public string counterScope = "reward/stall-per-episode; task/source/delay/strike/reception-run-cumulative; team arrays use team-major order";
            public int[] passStrikes, shotStrikes, intendedReceptions;
            public int[] observedRecoveries;
            public long[] executionSourceTicks;
            public long[] executionSourceTicksByTeam, taskResultsByTeam;
            public int[] kickDelayCounts;
            public float[] kickDelaySums;
            public int episodeStallActivations;
            public float episodeStallActiveSeconds;
            public long[] taskResults;
            public long[] raw, rewarded, capped;
            public Entry[] trace;
            public string observedOnTarget = "unavailable", observedBlocked = "unavailable", observedSave = "unavailable";
        }
        void Awake()
        {
            m_Match = GetComponent<MNG_MatchController>();
            m_Ball = GetComponentInChildren<MNG_BallControl>();
            m_Reward = GetComponent<MNG_RewardEngine>();
            m_Output = Argument("-mngEvidenceDir");
            m_Full = Argument("-mngFullTrace") == "true";
            m_Stream = Argument("-mngLifecycleTrace") == "true";
        }
        void OnEnable()
        {
            m_Match.TaskProcessed += OnTask;
            m_Match.CommandAccepted += OnCommand;
            m_Match.GoalScored += OnGoal;
            m_Ball.PlateStrikeApplied += OnKick;
        }
        void OnDisable()
        {
            m_Match.TaskProcessed -= OnTask; m_Match.CommandAccepted -= OnCommand;
            m_Match.GoalScored -= OnGoal; m_Ball.PlateStrikeApplied -= OnKick;
        }
        void OnTask(Team team, int slot, MNG_TaskResult result)
        {
            m_TeamTaskResults[(int)team * 9 + (int)result.Kind]++;
            if (result.Kind == MNG_TaskResultKind.Accepted)
            { m_AppliedTaskIds[(int)team * 4 + slot] = result.TaskId; m_AppliedTaskTimes[(int)team * 4 + slot] = m_Match.EpisodeElapsedSeconds; }
            Push(new Entry { kind = result.Kind.ToString(), team = (int)team, slot = slot,
                taskId = result.TaskId, parentCommandId = result.ParentCommandId,
                source = result.Source.ToString() });
        }
        void OnCommand(Team team, MNG_Command command, long tick)
            => Push(new Entry { kind = "Command:" + command, team = (int)team, slot = -1,
                parentCommandId = m_Match.GetCommandId(team) });
        public void RecordPolicyDecision(Team team, long observationTick, MNG_Command raw, MNG_Command effective, bool[] mask)
        {
            var bits = 0; for (var i = 0; i < mask.Length; i++) if (mask[i]) bits |= 1 << i;
            Push(new Entry { kind = "PolicyDecision", source = MNG_ActionSource.PolicyCommand.ToString(),
                team = (int)team, slot = -1, observationTick = observationTick, rawCommand = (int)raw,
                effectiveCommand = (int)effective, maskBits = bits, parentCommandId = m_Match.GetCommandId(team),
                passContext = m_PassContexts[(int)team] });
        }
        public void RecordExecutionSource(Team team, int slot, MNG_PlayerTask task, MNG_ActionSource source, bool changed)
        {
            m_SourceTicks[(int)source]++;
            m_TeamSourceTicks[(int)team * 4 + (int)source]++;
            if (changed) Push(new Entry { kind = "ExecutionSource", team = (int)team, slot = slot,
                source = source.ToString(), taskId = task.TaskId, parentCommandId = task.ParentCommandId });
        }
        void OnGoal(Team team, long goalId)
            => Push(new Entry { kind = "Goal", team = (int)team, sequence = goalId, slot = -1 });
        void OnKick(MNG_PlateStrikeEvent strike)
        {
            if (strike.TaskId <= 0 || strike.ParentCommandId <= 0)
                throw new InvalidOperationException("V2 physical strike has no parent task/command.");
            var index = (int)strike.Team * 4 + strike.Slot;
            if (m_AppliedTaskIds[index] == strike.TaskId)
            { m_KickDelayCounts[(int)strike.Team]++; m_KickDelaySums[(int)strike.Team] += m_Match.EpisodeElapsedSeconds - m_AppliedTaskTimes[index]; }
            Push(new Entry { kind = "Strike:" + strike.Intent, team = (int)strike.Team,
                slot = strike.Slot, taskId = strike.TaskId, parentCommandId = strike.ParentCommandId,
                kickId = strike.KickId, intendedReceiver = strike.IntendedReceiverSlot, source = strike.Source.ToString() });
        }
        void Push(Entry entry)
        {
            entry.sequence = ++m_Sequence; entry.episode = m_Match.Snapshot.EpisodeId;
            entry.tick = m_Match.Snapshot.TickId; entry.time = m_Match.EpisodeElapsedSeconds;
            entry.matchState = m_Match.State.ToString();
            m_Ring[m_Next] = entry; m_Next = (m_Next + 1) % Capacity; m_Count = Mathf.Min(Capacity, m_Count + 1);
            if (m_Stream && !string.IsNullOrEmpty(m_Output))
            {
                Directory.CreateDirectory(m_Output);
                File.AppendAllText(Path.Combine(m_Output, "lifecycle-" + MNG_MSController.ResolveWorkerIdentity(Environment.GetCommandLineArgs()) + ".jsonl"), JsonUtility.ToJson(entry) + "\n");
            }
        }
        void FixedUpdate()
        {
            if (m_SummaryEpisode != m_Match.Snapshot.EpisodeId)
            { m_SummaryEpisode = m_Match.Snapshot.EpisodeId; m_NextSummary = 30f; }
            var completed = m_Match.State == MNG_MatchState.Finished;
            if (completed && m_LastEpisode == m_Match.Snapshot.EpisodeId) return;
            if (!completed && m_Match.EpisodeElapsedSeconds < m_NextSummary) return;
            m_NextSummary = m_Match.EpisodeElapsedSeconds + 30f;
            if (completed) m_LastEpisode = m_Match.Snapshot.EpisodeId;
            if (string.IsNullOrEmpty(m_Output)) return;
            var report = new Report { run = Argument("-mngRunId"), build = Argument("-mngBuildSha"),
                worker = MNG_MSController.ResolveWorkerIdentity(Environment.GetCommandLineArgs()), episode = m_Match.Snapshot.EpisodeId,
                elapsedSeconds = m_Match.EpisodeElapsedSeconds, completed = completed,
                executionSourceTicks = (long[])m_SourceTicks.Clone(),
                executionSourceTicksByTeam = (long[])m_TeamSourceTicks.Clone(), taskResultsByTeam = (long[])m_TeamTaskResults.Clone(),
                kickDelayCounts = (int[])m_KickDelayCounts.Clone(), kickDelaySums = (float[])m_KickDelaySums.Clone(),
                episodeStallActivations = m_Match.EpisodeStallActivations, episodeStallActiveSeconds = m_Match.EpisodeStallActiveSeconds,
                taskResults = new long[9], raw = new long[22], rewarded = new long[22], capped = new long[22] };
            var tracker = GetComponent<MNG_TacticalRewardTracker>();
            if (tracker != null)
            {
                report.passStrikes = new[] { tracker.GetPassStrikeCount(Team.Red), tracker.GetPassStrikeCount(Team.Navy) };
                report.shotStrikes = new[] { tracker.GetShotStrikeCount(Team.Red), tracker.GetShotStrikeCount(Team.Navy) };
                report.intendedReceptions = new[] { tracker.GetIntendedReceptionCount(Team.Red), tracker.GetIntendedReceptionCount(Team.Navy) };
                report.observedRecoveries = new[] { tracker.GetObservedRecoveryCount(Team.Red), tracker.GetObservedRecoveryCount(Team.Navy) };
            }
            for (var i = 0; i < 9; i++) report.taskResults[i] = m_Match.GetTaskResultCount((MNG_TaskResultKind)i);
            for (var team = 0; team < 2; team++) for (var kind = 0; kind < 11; kind++)
            {
                var index = team * 11 + kind;
                report.raw[index] = m_Reward.GetRawCount((Team)team, (MNG_RewardEventKind)kind);
                report.rewarded[index] = m_Reward.GetRewardedCount((Team)team, (MNG_RewardEventKind)kind);
                report.capped[index] = m_Reward.GetCappedCount((Team)team, (MNG_RewardEventKind)kind);
            }
            if (m_Full)
            {
                report.trace = new Entry[m_Count];
                for (var i = 0; i < m_Count; i++) report.trace[i] = m_Ring[(m_Next - m_Count + i + Capacity) % Capacity];
            }
            Directory.CreateDirectory(m_Output);
            File.AppendAllText(Path.Combine(m_Output, "v2-" + report.worker + ".jsonl"), JsonUtility.ToJson(report) + "\n");
        }
        static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i + 1 < args.Length; i++) if (args[i] == name) return args[i + 1];
            return string.Empty;
        }
    }
}
