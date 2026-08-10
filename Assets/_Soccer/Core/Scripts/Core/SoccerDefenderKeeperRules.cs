using UnityEngine;

namespace MachineLearning.Soccer
{
    /// <summary>
    /// Neural·fallback·Rule 선수가 공유하는 저비용 역할 보호 규칙.
    /// 공격 깊이는 양 팀 모두 양수가 상대 골문 방향이 되도록 정규화한다.
    /// 모든 제어 방식의 최종 안전층이므로 팀별 행동 코드에서 우회하지 않는다.
    /// </summary>
    public static class SoccerDefenderKeeperRules
    {
        public const float SoftHalfLineDepth = -4f;
        public const float HardHalfLineDepth = 0f;
        public const float LooseBallRecoveryLead = 6f;
        public const float OwnHalfThreatDepth = -6f;
        public const float GoalwardThreatSpeed = 1.5f;
        public const float HomeTrackingWidth = 10f;

        public static float GetAttackSign(Team team)
        {
            return team == Team.Blue ? 1f : -1f;
        }

        public static float GetAttackingDepth(Team team, float worldX)
        {
            return worldX * GetAttackSign(team);
        }

        public static bool ShouldRecover(AgentSoccer agent, SoccerEnvController environment)
        {
            if (!IsDefenderKeeper(agent) || environment == null)
            {
                return false;
            }

            var agentDepth = GetAttackingDepth(agent.Team, agent.transform.position.x);
            if (agentDepth >= HardHalfLineDepth)
            {
                return true;
            }

            var controllingTeam = environment.PossessionTeam ?? environment.LastTouchTeam;
            if (controllingTeam.HasValue && controllingTeam.Value != agent.Team)
            {
                return true;
            }

            if (environment.Ball == null)
            {
                return false;
            }

            var ballDepth = GetAttackingDepth(agent.Team, environment.Ball.transform.position.x);
            var goalwardSpeed = environment.ballRb != null
                ? environment.ballRb.linearVelocity.x * GetAttackSign(agent.Team)
                : 0f;
            var agentIsCaughtAhead = agentDepth > ballDepth + LooseBallRecoveryLead;
            var ballThreatensOwnHalf = ballDepth <= OwnHalfThreatDepth
                && goalwardSpeed <= -GoalwardThreatSpeed;
            return (agentIsCaughtAhead && ballDepth < 0f) || ballThreatensOwnHalf;
        }

        public static Vector3 GetHomeTarget(AgentSoccer agent, SoccerEnvController environment)
        {
            if (agent == null)
            {
                return Vector3.zero;
            }

            var target = agent.StartingPosition;
            if (environment != null && environment.Ball != null)
            {
                target.z = Mathf.Clamp(
                    environment.Ball.transform.position.z * 0.35f,
                    -HomeTrackingWidth,
                    HomeTrackingWidth);
            }

            target.y = agent.transform.position.y;
            return target;
        }

        public static Vector3 ConstrainTarget(
            AgentSoccer agent,
            SoccerEnvController environment,
            Vector3 target)
        {
            if (!IsDefenderKeeper(agent))
            {
                return target;
            }

            if (ShouldRecover(agent, environment))
            {
                return GetHomeTarget(agent, environment);
            }

            var attackSign = GetAttackSign(agent.Team);
            var targetDepth = Mathf.Min(target.x * attackSign, SoftHalfLineDepth);
            target.x = targetDepth * attackSign;
            return target;
        }

        public static Vector3 ConstrainMovement(
            AgentSoccer agent,
            SoccerEnvController environment,
            Vector3 desiredMovement,
            out bool recovering)
        {
            recovering = ShouldRecover(agent, environment);
            if (!IsDefenderKeeper(agent))
            {
                return desiredMovement;
            }

            if (recovering)
            {
                var homeOffset = GetHomeTarget(agent, environment) - agent.transform.position;
                homeOffset.y = 0f;
                return homeOffset.sqrMagnitude > 0.04f
                    ? homeOffset.normalized * 1.3f
                    : Vector3.zero;
            }

            var depth = GetAttackingDepth(agent.Team, agent.transform.position.x);
            if (depth <= SoftHalfLineDepth)
            {
                return desiredMovement;
            }

            var attackDirection = Vector3.right * GetAttackSign(agent.Team);
            var attackingComponent = Vector3.Dot(desiredMovement, attackDirection);
            if (attackingComponent <= 0f)
            {
                return desiredMovement;
            }

            var retainedFraction = 1f - Mathf.InverseLerp(
                SoftHalfLineDepth,
                HardHalfLineDepth,
                depth);
            return desiredMovement - attackDirection * attackingComponent * (1f - retainedFraction);
        }

        public static Vector3 ConstrainVelocity(
            AgentSoccer agent,
            Vector3 velocity,
            bool recovering)
        {
            if (!IsDefenderKeeper(agent))
            {
                return velocity;
            }

            var depth = GetAttackingDepth(agent.Team, agent.transform.position.x);
            if (!recovering && depth <= SoftHalfLineDepth)
            {
                return velocity;
            }

            var attackDirection = Vector3.right * GetAttackSign(agent.Team);
            var attackingSpeed = Vector3.Dot(velocity, attackDirection);
            if (attackingSpeed <= 0f)
            {
                return velocity;
            }

            var retainedFraction = recovering
                ? 0f
                : 1f - Mathf.InverseLerp(SoftHalfLineDepth, HardHalfLineDepth, depth);
            return velocity - attackDirection * attackingSpeed * (1f - retainedFraction);
        }

        static bool IsDefenderKeeper(AgentSoccer agent)
        {
            return agent != null && agent.PositionRole == AgentSoccer.Position.DefenderKeeper;
        }
    }
}
