using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    public static class FiveLearningFactory
    {
        public static FiveLearningSession Create(FiveMatch match, bool automaticSteps, bool communicator)
        {
            var root = new GameObject("P8 Learning Contract");
            root.SetActive(false); root.transform.SetParent(match.transform, false);
            match.automaticTick = false;
            var session = root.AddComponent<FiveLearningSession>();
            session.match = match; session.automaticSteps = automaticSteps;
            session.agents = new FiveManagerAgent[2];
            for (var team = 0; team < 2; team++)
            {
                var child = new GameObject("Manager " + (Team)team); child.transform.SetParent(root.transform, false);
                var behavior = child.AddComponent<BehaviorParameters>();
                behavior.BehaviorName = FiveLearningContract.BehaviorName;
                behavior.TeamId = team;
                behavior.BrainParameters.VectorObservationSize = FiveLearningContract.ObservationSize;
                behavior.BrainParameters.NumStackedVectorObservations = 1;
                behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(FiveLearningContract.ActionCount);
                behavior.BehaviorType = communicator ? BehaviorType.Default : BehaviorType.HeuristicOnly;
                behavior.UseChildSensors = false; behavior.UseChildActuators = false;
                var agent = child.AddComponent<FiveManagerAgent>(); agent.session = session; agent.team = (Team)team;
                session.agents[team] = agent;
            }
            root.SetActive(true);
            return session;
        }
    }
}
