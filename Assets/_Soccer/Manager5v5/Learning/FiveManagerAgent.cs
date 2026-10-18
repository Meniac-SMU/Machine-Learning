using System;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;

namespace MachineLearning.Soccer.Manager5v5
{
    public sealed class FiveManagerAgent : Agent
    {
        public FiveLearningSession session;
        public Team team;
        public bool DecisionPending { get; private set; }
        public int Decisions { get; private set; }
        public float[] Observation { get; private set; }
        public FiveHistoryObservation HistoryObservation { get; private set; }
        public override void Initialize()
        {
            var behavior = GetComponent<BehaviorParameters>();
            if (session == null || behavior.BehaviorName != FiveLearningContract.BehaviorName
                || behavior.BrainParameters.VectorObservationSize != FiveLearningContract.ObservationSize
                || behavior.BrainParameters.NumStackedVectorObservations != 1
                || behavior.BrainParameters.ActionSpec.NumContinuousActions != 0
                || behavior.BrainParameters.ActionSpec.BranchSizes.Length != 1
                || behavior.BrainParameters.ActionSpec.BranchSizes[0] != 8 || behavior.Model != null)
                throw new InvalidOperationException("P8 requires the isolated untrained 676/8 R1+M2 contract.");
            MaxStep = 0; // A full match is the episode; the session is the only reset owner.
        }
        public void PrepareDecision()
        {
            CaptureObservation();
            DecisionPending = true;
            RequestDecision();
        }
        public void PrepareTerminal()
        {
            CaptureObservation();
            DecisionPending = false;
        }
        public override void CollectObservations(VectorSensor sensor)
        {
            if (Observation == null) CaptureObservation();
            sensor.AddObservation(Observation);
        }
        public override void WriteDiscreteActionMask(IDiscreteActionMask mask)
        {
            // T7 allows two consecutive choices; all other commands allow three.
            // A command at its limit is masked until two other choices are accepted.
            // T9 suppresses requests entirely; never create an all-masked branch.
            for(int action=0;action<8;action++)
                mask.SetActionEnabled(0,action,session.match.CommandBlockedSelections(team,(FiveCommand)action)==0);
        }
        void CaptureObservation()
        {
            HistoryObservation = session.History.Capture(session.match, team);
            Observation = HistoryObservation.values;
        }
        public override void OnActionReceived(ActionBuffers actions)
        {
            if (!DecisionPending) return; // Academy may deliver a retained action on the other team's turn.
            // The trainer writes this explicit signal before saving/closing its communicator.
            // No shutdown default action is accepted as a real manager choice.
            if(session.ShutdownRequested){DecisionPending=false;return;}
            if (actions.DiscreteActions.Length != 1 || actions.ContinuousActions.Length != 0)
                throw new InvalidOperationException("Expected a single discrete action branch.");
            var action = actions.DiscreteActions[0];
            if (!FiveLearningContract.Apply(session.match, team, action))
                throw new InvalidOperationException("An action missed its legal decision boundary.");
            DecisionPending = false; Decisions++;
            session.RecordDecision(this, action);
        }
        public override void Heuristic(in ActionBuffers actionsOut)
        {
            // Explicitly labelled rule baseline, never a learned-policy result.
            actionsOut.DiscreteActions.Array[actionsOut.DiscreteActions.Offset] =
                (int)session.match.ChooseRule(team, out _);
        }
        public override void OnEpisodeBegin()
        {
            DecisionPending = false; Observation = null; HistoryObservation = null;
            // Do not reset shared physics or scores from either individual agent.
        }
    }
}
