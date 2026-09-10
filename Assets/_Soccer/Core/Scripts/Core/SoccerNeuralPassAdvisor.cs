using UnityEngine;

namespace MachineLearning.Soccer
{
    /// <summary>L2-only common teaching layer. It never changes success judgment.</summary>
    public static class SoccerNeuralPassAdvisor
    {
        public static AgentSoccer FindTarget(AgentSoccer actor, Vector3 ball, Vector3 goal,
            System.Collections.Generic.IEnumerable<SoccerEnvController.PlayerInfo> agents)
        {
            AgentSoccer best = null;
            var bestDistance = float.PositiveInfinity;
            foreach (var item in agents)
            {
                var target = item?.Agent;
                if (target == null || target == actor || target.Team != actor.Team || !target.isActiveAndEnabled
                    || !SoccerCurriculumPassRules.IsAdvantageousTarget(actor.transform.position,
                        target.transform.position, goal)) continue;
                var distance = (target.transform.position - ball).sqrMagnitude;
                if (distance >= bestDistance) continue;
                best = target;
                bestDistance = distance;
            }
            return best;
        }

        public static Vector3 ResolveDirection(Vector3 ball, AgentSoccer target, Vector3 fallback)
        {
            if (target == null) return fallback;
            var direction = target.transform.position - ball;
            direction.y = 0f;
            return direction.sqrMagnitude > .0001f ? direction.normalized : fallback;
        }
    }
}
