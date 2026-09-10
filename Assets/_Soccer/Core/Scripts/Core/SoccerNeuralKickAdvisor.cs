using UnityEngine;

namespace MachineLearning.Soccer
{
    public enum SoccerNeuralKickAdvice
    {
        None,
        ShotRecommended,
        OffTargetShotDeferred
    }

    /// <summary>
    /// Neural 정책이 사람에게도 어려운 근거리 공 접촉을 안정적으로 마무리하도록 돕는 공통 기술층.
    /// Action Branch나 킥 물리는 바꾸지 않고, 확정 소유와 골문 궤적이 명확한 순간에만 킥 선택을 보정한다.
    /// </summary>
    public static class SoccerNeuralKickAdvisor
    {
        public const float MaximumAdviceGoalDistance = 24f;
        public const float ControlledAdviceGoalDistance = 12f;
        public const float MaximumCarrierBallDistance = 1.8f;

        public static int ResolveShotAction(
            Team team,
            Vector3 agentPosition,
            Vector3 agentForward,
            Vector3 ballPosition,
            int requestedAction,
            bool isConfirmedCarrier,
            bool canKick,
            SoccerArenaGeometry arena,
            out SoccerNeuralKickAdvice advice)
        {
            advice = SoccerNeuralKickAdvice.None;
            if (!isConfirmedCarrier || !canKick || requestedAction < 0 || requestedAction > 2)
            {
                return requestedAction;
            }

            var agentToBall = ballPosition - agentPosition;
            agentToBall.y = 0f;
            if (agentToBall.sqrMagnitude > MaximumCarrierBallDistance * MaximumCarrierBallDistance)
            {
                return requestedAction;
            }

            var goal = SoccerDefensiveClearanceRules.GetOpponentGoalCenter(team, ballPosition.y, arena);
            var ballToGoal = goal - ballPosition;
            ballToGoal.y = 0f;
            if (ballToGoal.sqrMagnitude > MaximumAdviceGoalDistance * MaximumAdviceGoalDistance)
            {
                return requestedAction;
            }

            var strikeDirection = AgentSoccer.CalculateBallPushDirection(
                agentPosition,
                agentForward,
                ballPosition,
                true);
            var predictsGoal = PredictsScoringLane(team, ballPosition, strikeDirection, arena);
            if (!predictsGoal)
            {
                if (requestedAction == 0)
                {
                    return 0;
                }

                advice = SoccerNeuralKickAdvice.OffTargetShotDeferred;
                return 0;
            }

            if (requestedAction != 0)
            {
                return requestedAction;
            }

            advice = SoccerNeuralKickAdvice.ShotRecommended;
            return ballToGoal.sqrMagnitude <= ControlledAdviceGoalDistance * ControlledAdviceGoalDistance
                ? 1
                : 2;
        }

        public static bool PredictsScoringLane(
            Team team,
            Vector3 ballPosition,
            Vector3 strikeDirection,
            SoccerArenaGeometry arena)
        {
            strikeDirection.y = 0f;
            if (strikeDirection.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            strikeDirection.Normalize();
            var attackSign = SoccerDefensiveClearanceRules.GetAttackSign(team);
            if (strikeDirection.x * attackSign <= 0.05f)
            {
                return false;
            }

            var halfLength = arena != null ? arena.HalfLength : SoccerArenaGeometry.StadiumHalfLength;
            var goalHalfWidth = arena != null ? arena.GoalHalfWidth : SoccerArenaGeometry.StadiumGoalHalfWidth;
            var travel = (halfLength * attackSign - ballPosition.x) / strikeDirection.x;
            if (travel <= 0f || travel > MaximumAdviceGoalDistance)
            {
                return false;
            }

            var crossingZ = ballPosition.z + strikeDirection.z * travel;
            return Mathf.Abs(crossingZ) <= Mathf.Max(0f, goalHalfWidth - 0.5f);
        }
    }
}
