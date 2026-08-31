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
        public enum DefenderKeeperMode
        {
            Free,
            EngageBall,
            RecoverGoal
        }

        public const float SoftHalfLineDepth = -4f;
        public const float HardHalfLineDepth = 0f;
        public const float LooseBallRecoveryLead = 6f;
        public const float DefensiveEngagementDepth = -8f;
        public const float OwnHalfThreatDepth = -6f;
        public const float GoalwardThreatSpeed = 1.5f;
        public const float HomeTrackingWidth = 14f;
        public const float EngagementTrackingWidth = 18f;
        public const float MaximumEngagementTargetDepth = -6f;

        const float HomeTrackingRatio = 0.55f;
        const float EngagementBlend = 0.72f;

        public static float GetAttackSign(Team team)
        {
            return team == Team.Red ? 1f : -1f;
        }

        public static float GetAttackingDepth(Team team, float worldX)
        {
            return worldX * GetAttackSign(team);
        }

        public static bool ShouldRecover(AgentSoccer agent, SoccerEnvController environment)
        {
            return GetMode(agent, environment) == DefenderKeeperMode.RecoverGoal;
        }

        public static bool ShouldEngageBall(AgentSoccer agent, SoccerEnvController environment)
        {
            return GetMode(agent, environment) == DefenderKeeperMode.EngageBall;
        }

        public static DefenderKeeperMode GetMode(AgentSoccer agent, SoccerEnvController environment)
        {
            if (!IsDefenderKeeper(agent) || environment == null)
            {
                return DefenderKeeperMode.Free;
            }

            var agentDepth = GetAttackingDepth(agent.Team, agent.transform.position.x);
            if (agentDepth >= HardHalfLineDepth)
            {
                return DefenderKeeperMode.RecoverGoal;
            }

            var controllingTeam = environment.PossessionTeam ?? environment.LastTouchTeam;
            if (environment.Ball == null)
            {
                return controllingTeam.HasValue && controllingTeam.Value != agent.Team
                    ? DefenderKeeperMode.RecoverGoal
                    : DefenderKeeperMode.Free;
            }

            var ballDepth = GetAttackingDepth(agent.Team, environment.Ball.transform.position.x);
            var goalwardSpeed = environment.ballRb != null
                ? environment.ballRb.linearVelocity.x * GetAttackSign(agent.Team)
                : 0f;
            var agentIsCaughtAhead = agentDepth > ballDepth + LooseBallRecoveryLead;
            var ballThreatensOwnHalf = ballDepth <= OwnHalfThreatDepth
                && goalwardSpeed <= -GoalwardThreatSpeed;
            var opponentOrLooseBall = !controllingTeam.HasValue || controllingTeam.Value != agent.Team;
            if (opponentOrLooseBall
                && (ballDepth <= DefensiveEngagementDepth || ballThreatensOwnHalf))
            {
                // 자기 진영의 위협 공에는 홈으로 물러서기만 하지 않고 공-골문 사이를 적극 차단한다.
                return DefenderKeeperMode.EngageBall;
            }

            if (controllingTeam.HasValue && controllingTeam.Value != agent.Team)
            {
                return DefenderKeeperMode.RecoverGoal;
            }

            return agentIsCaughtAhead && ballDepth < 0f
                ? DefenderKeeperMode.RecoverGoal
                : DefenderKeeperMode.Free;
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
                    environment.Ball.transform.position.z * HomeTrackingRatio,
                    -HomeTrackingWidth,
                    HomeTrackingWidth);
            }

            target.y = agent.transform.position.y;
            return environment != null && environment.ArenaGeometry != null
                ? environment.ArenaGeometry.ClampTarget(target) : target;
        }

        public static Vector3 GetEngagementTarget(AgentSoccer agent, SoccerEnvController environment)
        {
            if (agent == null || environment == null || environment.Ball == null)
            {
                return GetHomeTarget(agent, environment);
            }

            var ballPosition = environment.Ball.transform.position;
            var ballDepth = GetAttackingDepth(agent.Team, ballPosition.x);
            var target = ballDepth <= SoccerDefensiveClearanceRules.GoalDangerDepth
                ? ballPosition
                : Vector3.Lerp(GetHomeTarget(agent, environment), ballPosition, EngagementBlend);
            var attackSign = GetAttackSign(agent.Team);
            var targetDepth = CalculateEngagementTargetDepth(
                GetAttackingDepth(agent.Team, agent.StartingPosition.x),
                ballDepth);
            target.x = targetDepth * attackSign;
            target.y = agent.transform.position.y;
            target.z = Mathf.Clamp(target.z, -EngagementTrackingWidth, EngagementTrackingWidth);
            return environment.ArenaGeometry != null ? environment.ArenaGeometry.ClampTarget(target) : target;
        }

        public static float CalculateEngagementTargetDepth(float homeDepth, float ballDepth)
        {
            var targetDepth = ballDepth <= SoccerDefensiveClearanceRules.GoalDangerDepth
                ? ballDepth
                : Mathf.Lerp(homeDepth, ballDepth, EngagementBlend);
            return Mathf.Min(targetDepth, MaximumEngagementTargetDepth);
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

            var mode = GetMode(agent, environment);
            if (mode == DefenderKeeperMode.RecoverGoal)
            {
                return GetHomeTarget(agent, environment);
            }

            if (mode == DefenderKeeperMode.EngageBall)
            {
                return GetEngagementTarget(agent, environment);
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
            out DefenderKeeperMode mode)
        {
            mode = GetMode(agent, environment);
            if (!IsDefenderKeeper(agent))
            {
                return desiredMovement;
            }

            if (mode is DefenderKeeperMode.RecoverGoal or DefenderKeeperMode.EngageBall)
            {
                var target = mode == DefenderKeeperMode.RecoverGoal
                    ? GetHomeTarget(agent, environment)
                    : GetEngagementTarget(agent, environment);
                var targetOffset = target - agent.transform.position;
                targetOffset.y = 0f;
                return targetOffset.sqrMagnitude > 0.04f
                    ? targetOffset.normalized * 1.3f
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
            DefenderKeeperMode mode)
        {
            if (!IsDefenderKeeper(agent))
            {
                return velocity;
            }

            var depth = GetAttackingDepth(agent.Team, agent.transform.position.x);
            if (mode != DefenderKeeperMode.RecoverGoal && depth <= SoftHalfLineDepth)
            {
                return velocity;
            }

            var attackDirection = Vector3.right * GetAttackSign(agent.Team);
            var attackingSpeed = Vector3.Dot(velocity, attackDirection);
            if (attackingSpeed <= 0f)
            {
                return velocity;
            }

            var retainedFraction = mode == DefenderKeeperMode.RecoverGoal
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
