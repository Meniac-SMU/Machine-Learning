using System.Collections.Generic;
using UnityEngine;

namespace MachineLearning.Soccer
{
    public static class SoccerFormationEvaluator
    {
        public static float Evaluate(
            Team team,
            IReadOnlyList<AgentSoccer> agents,
            AgentSoccer ballCarrier,
            Vector3 ballPosition,
            bool ownsBall)
        {
            if (agents == null || agents.Count != 4)
            {
                return 0f;
            }

            var spacingScore = 0f;
            var pairCount = 0;
            for (var first = 0; first < agents.Count; first++)
            {
                for (var second = first + 1; second < agents.Count; second++)
                {
                    var distance = Vector3.Distance(agents[first].transform.position, agents[second].transform.position);
                    spacingScore += ScoreBand(distance, 3f, 8f, 26f, 40f);
                    pairCount++;
                }
            }

            spacingScore = pairCount > 0 ? spacingScore / pairCount : 0f;
            var attackSign = team == Team.Blue ? 1f : -1f;
            var defender = FindRole(agents, AgentSoccer.Position.DefenderKeeper);
            var striker = FindRole(agents, AgentSoccer.Position.Striker);

            var roleScore = 0.5f;
            if (defender != null && striker != null)
            {
                var defenderDepth = defender.transform.position.x * attackSign;
                var strikerDepth = striker.transform.position.x * attackSign;
                var ballDepth = ballPosition.x * attackSign;
                if (ownsBall)
                {
                    // The defender-keeper may advance, but should normally remain behind the striker.
                    roleScore = Mathf.Clamp01(0.5f + (strikerDepth - defenderDepth) / 30f);
                }
                else
                {
                    // During defense, reward the hybrid defender for providing cover behind the ball.
                    roleScore = Mathf.Clamp01(0.5f + (ballDepth - defenderDepth) / 30f);
                }
            }

            var supportScore = 0.5f;
            if (ownsBall && ballCarrier != null)
            {
                var supportCount = 0;
                foreach (var agent in agents)
                {
                    if (agent == ballCarrier)
                    {
                        continue;
                    }

                    var distance = Vector3.Distance(agent.transform.position, ballCarrier.transform.position);
                    if (distance is >= 5f and <= 22f)
                    {
                        supportCount++;
                    }
                }

                supportScore = supportCount / 3f;
            }

            // Midfielders intentionally have no fixed anchor. They are evaluated only by spacing and support.
            return Mathf.Clamp01(spacingScore * 0.45f + supportScore * 0.35f + roleScore * 0.20f);
        }

        static AgentSoccer FindRole(IReadOnlyList<AgentSoccer> agents, AgentSoccer.Position role)
        {
            foreach (var agent in agents)
            {
                if (agent != null && agent.PositionRole == role)
                {
                    return agent;
                }
            }

            return null;
        }

        static float ScoreBand(float value, float hardMinimum, float idealMinimum, float idealMaximum, float hardMaximum)
        {
            if (value <= hardMinimum || value >= hardMaximum)
            {
                return 0f;
            }

            if (value >= idealMinimum && value <= idealMaximum)
            {
                return 1f;
            }

            return value < idealMinimum
                ? Mathf.InverseLerp(hardMinimum, idealMinimum, value)
                : 1f - Mathf.InverseLerp(idealMaximum, hardMaximum, value);
        }
    }
}
