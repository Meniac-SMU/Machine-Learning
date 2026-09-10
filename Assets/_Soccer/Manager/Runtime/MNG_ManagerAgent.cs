using System;
using MachineLearning.Soccer;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BehaviorParameters))]
    public sealed class MNG_ManagerAgent : Agent
    {
        public const string BehaviorName = "MNG_Manager";
        public const int DecisionPeriod = 25;

        [SerializeField] Team team;
        [SerializeField] MNG_MatchController matchController;
        [SerializeField] bool enableDiagnosticHeuristic;

        readonly float[] m_ObservationBuffer = new float[MNG_ObservationWriter.ObservationSize];
        readonly bool[] m_CommandMaskBuffer = new bool[MNG_CommandMask.CommandCount];
        readonly long[] m_CommandCounts = new long[MNG_CommandMask.CommandCount];
        long m_PolicyDecisionCount;
        long m_MaskDecisionCount;
        long m_PassAvailableCount;
        long m_ShotAvailableCount;

        public Team Team => team;

        public override void Initialize()
        {
            if (matchController == null) matchController = GetComponentInParent<MNG_MatchController>();
            if (matchController == null) throw new InvalidOperationException("MNG manager requires an MNG_MatchController.");

            var behavior = GetComponent<BehaviorParameters>();
            behavior.BehaviorName = BehaviorName;
            var requester = GetComponent<DecisionRequester>();
            if (requester == null)
                throw new InvalidOperationException("MNG manager requires one DecisionRequester.");
            requester.DecisionPeriod = DecisionPeriod;
            requester.TakeActionsBetweenDecisions = false;
            matchController.RegisterManager(this);
        }

        public void Configure(Team configuredTeam, MNG_MatchController configuredMatch)
        {
            team = configuredTeam;
            matchController = configuredMatch;
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            if (!matchController.TryGetSnapshot(out var snapshot))
                throw new InvalidOperationException("MNG snapshot is unavailable at decision time.");
            MNG_ObservationWriter.Write(snapshot, team, matchController.GetDecisionState(team), m_ObservationBuffer);
            for (var i = 0; i < m_ObservationBuffer.Length; i++) sensor.AddObservation(m_ObservationBuffer[i]);
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            if (!matchController.TryGetSnapshot(out var snapshot))
                throw new InvalidOperationException("MNG snapshot is unavailable while writing the action mask.");
            MNG_CommandMask.Write(snapshot, team, matchController.GetDecisionState(team), m_CommandMaskBuffer);
            for (var command = 0; command < m_CommandMaskBuffer.Length; command++)
                actionMask.SetActionEnabled(0, command, m_CommandMaskBuffer[command]);
            m_MaskDecisionCount++;
            if (m_CommandMaskBuffer[(int)MNG_Command.PassBuild]) m_PassAvailableCount++;
            if (m_CommandMaskBuffer[(int)MNG_Command.AttemptShot]) m_ShotAvailableCount++;
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (actions.DiscreteActions.Length != 1)
                throw new InvalidOperationException("MNG policy contract v1 requires one discrete branch.");

            var rawCommand = actions.DiscreteActions[0];
            if (rawCommand < 0 || rawCommand >= MNG_CommandMask.CommandCount)
                throw new InvalidOperationException($"MNG policy emitted invalid command {rawCommand}.");
            matchController.AcceptPolicyCommand(team, (MNG_Command)rawCommand);
            m_CommandCounts[rawCommand]++;
            m_PolicyDecisionCount++;
            if (m_PolicyDecisionCount % 20 == 0) LogPolicyTelemetry();
        }

        void LogPolicyTelemetry()
        {
            Debug.Log(
                $"MNG POLICY TELEMETRY team={team} decisions={m_PolicyDecisionCount} "
                + $"masks={m_MaskDecisionCount} passAvailable={m_PassAvailableCount} "
                + $"shotAvailable={m_ShotAvailableCount} "
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
            actions[0] = (int)MNG_Command.Balanced;
        }
    }
}
