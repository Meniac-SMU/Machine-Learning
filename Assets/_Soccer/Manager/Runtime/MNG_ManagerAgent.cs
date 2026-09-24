using System;
using MachineLearning.Soccer;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public enum MNG_PolicyAssistMode
    {
        None = 0,
        LegacyBlockedForwardPass = 1
    }

    public static class MNG_PolicyAssist
    {
        public static MNG_PolicyAssistMode Resolve(
            string[] arguments,
            MNG_PolicyAssistMode fallback = MNG_PolicyAssistMode.None)
        {
            if (!Enum.IsDefined(typeof(MNG_PolicyAssistMode), fallback))
                throw new ArgumentOutOfRangeException(nameof(fallback));
            var requested = ReadArgument(arguments, "-mngPolicyAssistMode");
            if (string.IsNullOrWhiteSpace(requested))
            {
                // Historical pass-choice experiments used this flag to disable the
                // post-policy override. Keep that invocation compatible while the
                // new default remains exact action passthrough.
                if (string.Equals(
                        ReadArgument(arguments, "-mngMS1LearnPassChoice"),
                        "true",
                        StringComparison.OrdinalIgnoreCase))
                    return MNG_PolicyAssistMode.None;
                return fallback;
            }

            if (string.Equals(requested, "none", StringComparison.OrdinalIgnoreCase))
                return MNG_PolicyAssistMode.None;
            if (string.Equals(requested, "legacy", StringComparison.OrdinalIgnoreCase)
                || string.Equals(
                    requested,
                    "legacy-blocked-forward-pass",
                    StringComparison.OrdinalIgnoreCase))
                return MNG_PolicyAssistMode.LegacyBlockedForwardPass;
            throw new InvalidOperationException(
                $"Unsupported MNG policy assist mode: {requested}");
        }

        static string ReadArgument(string[] arguments, string name)
        {
            if (arguments == null) return string.Empty;
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            return string.Empty;
        }
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(BehaviorParameters))]
    public sealed class MNG_ManagerAgent : Agent
    {
        public const string BehaviorName = "MNG_Manager";
        public const int DecisionPeriod = 25;

        [SerializeField] Team team;
        [SerializeField] MNG_MatchController matchController;
        [SerializeField] bool enableDiagnosticHeuristic;
        [SerializeField] bool uniformRandomDiagnostic;
        [SerializeField] int diagnosticSeed = MNG_CurriculumCatalog.M1ValidationSeed;
        [SerializeField] MNG_PolicyAssistMode policyAssistMode = MNG_PolicyAssistMode.None;

        readonly float[] m_ObservationBuffer = new float[MNG_ObservationWriter.ObservationSize];
        readonly float[] m_V2ObservationBuffer = new float[MNG_RuntimeV2.ObservationSize];
        readonly bool[] m_CommandMaskBuffer = new bool[MNG_CommandMask.CommandCount];
        readonly long[] m_CommandCounts = new long[MNG_CommandMask.CommandCount];
        readonly long[] m_RawCommandCounts = new long[MNG_CommandMask.CommandCount];
        long m_PolicyDecisionCount;
        long m_MaskDecisionCount;
        long m_PassAvailableCount;
        readonly long[] m_AllMaskAvailability = new long[6];
        public long PassAvailableActive { get; private set; }
        public long PassAvailableForwardBlocked { get; private set; }
        long m_ShotAvailableCount;
        long m_BlockedForwardPassOverrideCount;
        long m_ExplicitBlockedPassRewardCount;
        System.Random m_DiagnosticRandom;
        bool m_PassRepairMode;
        long m_LastObservationTick;

        public Team Team => team;
        public long PolicyDecisionCount => m_PolicyDecisionCount;
        public long BlockedForwardPassOverrideCount => m_BlockedForwardPassOverrideCount;
        public long ExplicitBlockedPassRewardCount => m_ExplicitBlockedPassRewardCount;
        public MNG_PolicyAssistMode PolicyAssistMode => policyAssistMode;

        public void CopyCommandCounts(long[] destination)
        {
            if (destination == null || destination.Length != m_CommandCounts.Length)
                throw new ArgumentException(
                    $"Command-count destination must contain {m_CommandCounts.Length} entries.",
                    nameof(destination));
            Array.Copy(m_CommandCounts, destination, m_CommandCounts.Length);
        }

        public void CopyRawCommandCounts(long[] destination)
        {
            if (destination == null || destination.Length != m_RawCommandCounts.Length)
                throw new ArgumentException(
                    $"Raw-command destination must contain {m_RawCommandCounts.Length} entries.",
                    nameof(destination));
            Array.Copy(m_RawCommandCounts, destination, m_RawCommandCounts.Length);
        }

        public void CopyMaskAvailabilityCounts(long[] destination)
        {
            if (destination == null || destination.Length != MNG_CommandMask.CommandCount)
                throw new ArgumentException(
                    $"Mask-count destination must contain {MNG_CommandMask.CommandCount} entries.",
                    nameof(destination));
            Array.Copy(m_AllMaskAvailability, destination, destination.Length);
        }

        public override void Initialize()
        {
            if (matchController == null) matchController = GetComponentInParent<MNG_MatchController>();
            if (matchController == null) throw new InvalidOperationException("MNG manager requires an MNG_MatchController.");

            var behavior = GetComponent<BehaviorParameters>();
            behavior.BehaviorName = matchController.UseRuntimeV2 ? MNG_RuntimeV2.BehaviorName : BehaviorName;
            if (matchController.UseRuntimeV2
                && behavior.BrainParameters.VectorObservationSize != MNG_RuntimeV2.ObservationSize)
                throw new InvalidOperationException("MNG v2 scene observation schema mismatch.");
            if (matchController.UseRuntimeV2) MNG_RuntimeV2.ValidateModelAsset(behavior.Model);
            var requester = GetComponent<DecisionRequester>();
            if (requester == null)
                throw new InvalidOperationException("MNG manager requires one DecisionRequester.");
            requester.DecisionPeriod = DecisionPeriod;
            requester.TakeActionsBetweenDecisions = false;
            m_PassRepairMode = string.Equals(
                ReadArgument("-mngMS1PassPriorityMask"),
                "true",
                StringComparison.OrdinalIgnoreCase);
            policyAssistMode = MNG_PolicyAssist.Resolve(
                Environment.GetCommandLineArgs(),
                policyAssistMode);
            if (matchController.UseRuntimeV2 && (m_PassRepairMode || policyAssistMode != MNG_PolicyAssistMode.None))
                throw new InvalidOperationException("V2 forbids strategic masks and policy overrides.");
            if (uniformRandomDiagnostic) m_DiagnosticRandom = new System.Random(diagnosticSeed);
            matchController.RegisterManager(this);
        }

        public void Configure(Team configuredTeam, MNG_MatchController configuredMatch)
        {
            team = configuredTeam;
            matchController = configuredMatch;
        }

        public void ConfigurePolicyAssistMode(MNG_PolicyAssistMode configuredMode)
        {
            if (!Enum.IsDefined(typeof(MNG_PolicyAssistMode), configuredMode))
                throw new ArgumentOutOfRangeException(nameof(configuredMode));
            policyAssistMode = configuredMode;
        }

        public void ConfigureUniformRandomHeuristic(int seed)
        {
            enableDiagnosticHeuristic = true;
            uniformRandomDiagnostic = true;
            diagnosticSeed = seed;
            m_DiagnosticRandom = new System.Random(seed);
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (!matchController.TryGetSnapshot(out var snapshot))
                throw new InvalidOperationException("MNG snapshot is unavailable at decision time.");
            if (matchController.UseRuntimeV2)
            {
                m_LastObservationTick = snapshot.TickId;
                MNG_ObservationWriter.WriteV2(snapshot, team, matchController.GetDecisionState(team),
                    m_V2ObservationBuffer, m_ObservationBuffer);
                for (var i = 0; i < m_V2ObservationBuffer.Length; i++) sensor.AddObservation(m_V2ObservationBuffer[i]);
                return;
            }
            MNG_ObservationWriter.Write(snapshot, team, matchController.GetDecisionState(team), m_ObservationBuffer);
            for (var i = 0; i < m_ObservationBuffer.Length; i++) sensor.AddObservation(m_ObservationBuffer[i]);
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            if (!matchController.TryGetSnapshot(out var snapshot))
                throw new InvalidOperationException("MNG snapshot is unavailable while writing the action mask.");
            MNG_CommandMask.Write(snapshot, team, matchController.GetDecisionState(team), m_CommandMaskBuffer);
            if (matchController.UseRuntimeV2)
                matchController.GetComponent<MNG_V2Trace>()?.RecordPassContext(snapshot, team, matchController.GetDecisionState(team));
            if (m_PassRepairMode)
                MNG_CommandMask.ApplyPassRepairPriority(
                    snapshot,
                    team,
                    matchController.GetDecisionState(team),
                    m_CommandMaskBuffer);
            for (var command = 0; command < m_CommandMaskBuffer.Length; command++)
                {
                actionMask.SetActionEnabled(0, command, m_CommandMaskBuffer[command]);
                if (m_CommandMaskBuffer[command]) m_AllMaskAvailability[command]++;
            }
            m_MaskDecisionCount++;
            if (m_CommandMaskBuffer[(int)MNG_Command.PassBuild])
            {
                m_PassAvailableCount++;
                if (!snapshot.GoalPauseActive)
                {
                    PassAvailableActive++;
                    if (MNG_TacticalTargetResolver.IsForwardDribbleBlocked(snapshot, team, snapshot.Carrier.Slot)) PassAvailableForwardBlocked++;
                }
            }
            if (m_CommandMaskBuffer[(int)MNG_Command.AttemptShot]) m_ShotAvailableCount++;
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (actions.DiscreteActions.Length != 1)
                throw new InvalidOperationException("MNG policy contract v1 requires one discrete branch.");

            var rawCommand = actions.DiscreteActions[0];
            if (rawCommand < 0 || rawCommand >= MNG_CommandMask.CommandCount)
                throw new InvalidOperationException($"MNG policy emitted invalid command {rawCommand}.");
            if (!matchController.TryGetSnapshot(out var snapshot))
                throw new InvalidOperationException("MNG snapshot is unavailable while applying a policy action.");
            var requested = (MNG_Command)rawCommand;
            m_RawCommandCounts[(int)requested]++;
            var decision = matchController.GetDecisionState(team);
            if (policyAssistMode == MNG_PolicyAssistMode.LegacyBlockedForwardPass
                && requested == MNG_Command.PassBuild
                && MNG_CommandMask.IsBlockedForwardPassRecommended(
                    snapshot,
                    team,
                    decision)
                && matchController.AwardBlockedForwardPassDecision(
                    team,
                    m_PolicyDecisionCount) > 0f)
                m_ExplicitBlockedPassRewardCount++;
            var effective = policyAssistMode == MNG_PolicyAssistMode.LegacyBlockedForwardPass
                ? MNG_CommandMask.ApplyBlockedForwardPassPriority(
                    snapshot,
                    team,
                    decision,
                    requested)
                : requested;
            matchController.AcceptPolicyCommand(team, effective);
            if (matchController.UseRuntimeV2)
                matchController.GetComponent<MNG_V2Trace>()?.RecordPolicyDecision(team,
                    m_LastObservationTick, requested, effective, m_CommandMaskBuffer);
            m_CommandCounts[(int)effective]++;
            if (effective != requested) m_BlockedForwardPassOverrideCount++;
            m_PolicyDecisionCount++;
            if (m_PolicyDecisionCount % 20 == 0) LogPolicyTelemetry();
        }

        void LogPolicyTelemetry()
        {
            Debug.Log(
                $"MNG POLICY TELEMETRY team={team} decisions={m_PolicyDecisionCount} "
                + $"masks={m_MaskDecisionCount} passAvailable={m_PassAvailableCount} "
                + $"shotAvailable={m_ShotAvailableCount} "
                + $"blockedPassOverrides={m_BlockedForwardPassOverrideCount} "
                + $"explicitBlockedPassRewards={m_ExplicitBlockedPassRewardCount} "
                + $"policyAssistMode={policyAssistMode} "
                + $"commands=[advance:{m_CommandCounts[(int)MNG_Command.AdvanceCarry]},"
                + $"pass:{m_CommandCounts[(int)MNG_Command.PassBuild]},"
                + $"shot:{m_CommandCounts[(int)MNG_Command.AttemptShot]},"
                + $"recover:{m_CommandCounts[(int)MNG_Command.ActiveRecover]},"
                + $"balanced:{m_CommandCounts[(int)MNG_Command.Balanced]},"
                + $"protect:{m_CommandCounts[(int)MNG_Command.ProtectBack]}]");
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            if (!enableDiagnosticHeuristic)
                throw new InvalidOperationException(
                    "MNG diagnostic heuristic is disabled. Attach a trainer/model or use explicit MNG_FallbackManager.");
            var actions = actionsOut.DiscreteActions;
            if (m_DiagnosticRandom == null)
            {
                actions[0] = (int)MNG_Command.Balanced;
                return;
            }
            if (!matchController.TryGetSnapshot(out var snapshot))
                throw new InvalidOperationException("MNG snapshot is unavailable for random baseline.");
            MNG_CommandMask.Write(snapshot, team, matchController.GetDecisionState(team), m_CommandMaskBuffer);
            var enabledCount = 0;
            for (var command = 0; command < m_CommandMaskBuffer.Length; command++)
                if (m_CommandMaskBuffer[command]) enabledCount++;
            if (enabledCount <= 0)
                throw new InvalidOperationException("MNG random baseline has no enabled command.");
            var selected = m_DiagnosticRandom.Next(enabledCount);
            for (var command = 0; command < m_CommandMaskBuffer.Length; command++)
            {
                if (!m_CommandMaskBuffer[command]) continue;
                if (selected-- != 0) continue;
                actions[0] = command;
                return;
            }
        }

        static string ReadArgument(string name)
        {
            var arguments = Environment.GetCommandLineArgs();
            if (arguments == null) return string.Empty;
            for (var index = 0; index < arguments.Length - 1; index++)
                if (string.Equals(arguments[index], name, StringComparison.OrdinalIgnoreCase))
                    return arguments[index + 1];
            return string.Empty;
        }
    }
}
