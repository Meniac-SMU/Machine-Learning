using System.Collections.Generic;
using UnityEngine;

namespace MachineLearning.Soccer
{
    /// <summary>
    /// 고정 좌표가 아닌 간격·지원·공수 균형 중심의 포메이션 평가.
    /// 판정 거리와 점수 의미는 학습형 보상과 Rule 행동이 공유하는 공통 계약이다.
    /// </summary>
    public static class SoccerFormationEvaluator
    {
        // 팀별 차이는 RewardProfile의 패널티 크기·상한이나 Rule 조향 가중치로만 둔다.
        public const float TeammateCrowdingDistance = 3f;
        public const float FullCrowdingSeverityDistance = 1f;

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

            var spacingScore = EvaluateSpacing(agents);
            var supportScore = EvaluateSupport(agents, ownsBall && ballCarrier != null
                ? ballCarrier.transform.position
                : ballPosition);
            var balanceScore = EvaluateBalance(team, agents, ballPosition, ownsBall);
            var roleFreedomScore = EvaluateDefenderFreedom(team, agents, ballPosition, ownsBall);

            // 넓은 허용 범위와 낮은 역할 비중.
            return Mathf.Clamp01(
                spacingScore * 0.30f
                + supportScore * 0.35f
                + balanceScore * 0.25f
                + roleFreedomScore * 0.10f);
        }

        /// <summary>
        /// 가장 가까이 뭉친 한 쌍의 심각도를 반환한다.
        /// 여러 쌍이 동시에 가까워도 팀당 한 샘플의 패널티가 중첩되지 않는다.
        /// </summary>
        public static float EvaluateTeammateCrowding(IReadOnlyList<AgentSoccer> agents)
        {
            if (agents == null || agents.Count < 2)
            {
                return 0f;
            }

            var maximumSeverity = 0f;
            var crowdingDistanceSquared = TeammateCrowdingDistance * TeammateCrowdingDistance;
            for (var first = 0; first < agents.Count; first++)
            {
                if (agents[first] == null)
                {
                    continue;
                }

                for (var second = first + 1; second < agents.Count; second++)
                {
                    if (agents[second] == null)
                    {
                        continue;
                    }

                    var offset = agents[first].transform.position - agents[second].transform.position;
                    offset.y = 0f;
                    if (offset.sqrMagnitude >= crowdingDistanceSquared)
                    {
                        continue;
                    }

                    var severity = CalculateCrowdingSeverity(Mathf.Sqrt(offset.sqrMagnitude));
                    maximumSeverity = Mathf.Max(maximumSeverity, severity);
                    if (maximumSeverity >= 1f)
                    {
                        return 1f;
                    }
                }
            }

            return maximumSeverity;
        }

        public static float CalculateCrowdingSeverity(float distance)
        {
            return Mathf.Clamp01(
                (TeammateCrowdingDistance - distance)
                / (TeammateCrowdingDistance - FullCrowdingSeverityDistance));
        }

        static float EvaluateSpacing(IReadOnlyList<AgentSoccer> agents)
        {
            var score = 0f;
            var pairCount = 0;
            for (var first = 0; first < agents.Count; first++)
            {
                for (var second = first + 1; second < agents.Count; second++)
                {
                    var distance = Vector3.Distance(agents[first].transform.position, agents[second].transform.position);
                    score += ScoreBand(distance, 1.5f, 5f, 34f, 52f);
                    pairCount++;
                }
            }

            return pairCount > 0 ? score / pairCount : 0f;
        }

        static float EvaluateSupport(IReadOnlyList<AgentSoccer> agents, Vector3 focusPosition)
        {
            var supportCount = 0;
            foreach (var agent in agents)
            {
                var distance = Vector3.Distance(agent.transform.position, focusPosition);
                if (distance is >= 3f and <= 32f)
                {
                    supportCount++;
                }
            }

            // 두 명의 지원 선택지만으로 최대 점수 부여.
            return Mathf.Clamp01(supportCount / 2f);
        }

        static float EvaluateBalance(
            Team team,
            IReadOnlyList<AgentSoccer> agents,
            Vector3 ballPosition,
            bool ownsBall)
        {
            var attackSign = team == Team.Blue ? 1f : -1f;
            var ballDepth = ballPosition.x * attackSign;
            var behindCount = 0;
            var aheadCount = 0;

            foreach (var agent in agents)
            {
                var depth = agent.transform.position.x * attackSign;
                var behindDistance = ballDepth - depth;
                if (behindDistance is >= -8f and <= 45f)
                {
                    behindCount++;
                }

                if (ownsBall && depth - ballDepth is >= 2f and <= 34f)
                {
                    aheadCount++;
                }
            }

            if (!ownsBall)
            {
                return Mathf.Clamp01(behindCount / 2f);
            }

            return (Mathf.Clamp01(behindCount) + Mathf.Clamp01(aheadCount)) * 0.5f;
        }

        static float EvaluateDefenderFreedom(
            Team team,
            IReadOnlyList<AgentSoccer> agents,
            Vector3 ballPosition,
            bool ownsBall)
        {
            var attackSign = team == Team.Blue ? 1f : -1f;
            var defender = FindRole(agents, AgentSoccer.Position.DefenderKeeper);
            if (defender == null)
            {
                return 0.5f;
            }

            if (ownsBall)
            {
                // 팀이 공격할 때도 하프라인 안쪽 지원 위치를 유지한다.
                var possessionDepth = defender.transform.position.x * attackSign;
                return 1f - Mathf.InverseLerp(
                    SoccerDefenderKeeperRules.SoftHalfLineDepth,
                    SoccerDefenderKeeperRules.HardHalfLineDepth + 4f,
                    possessionDepth);
            }

            // 수비 전환과 중립 상황에서는 골문 앞 시작 깊이로 돌아갈수록 높게 평가한다.
            var defenderDepth = defender.transform.position.x * attackSign;
            var homeDepth = defender.StartingPosition.x * attackSign;
            var distanceFromHomeDepth = Mathf.Abs(defenderDepth - homeDepth);
            return 1f - Mathf.InverseLerp(6f, 30f, distanceFromHomeDepth);
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
