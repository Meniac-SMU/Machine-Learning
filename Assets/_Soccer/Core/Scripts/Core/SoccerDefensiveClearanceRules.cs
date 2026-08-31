using UnityEngine;

namespace MachineLearning.Soccer
{
    /// <summary>
    /// 모든 Controller가 공유하는 골문 위험 예측과 안전한 중앙 걷어내기 규칙.
    /// Action Branch의 의미는 유지하고 실제 공 접촉 방향만 자책골 위험일 때 보정한다.
    /// </summary>
    public static class SoccerDefensiveClearanceRules
    {
        // 모든 활성 환경이 공유하는 Stadium 골문 기준 월드 좌표 계약.
        public const float GoalLineDepth = SoccerArenaGeometry.StadiumHalfLength;
        public const float GoalHalfWidth = SoccerArenaGeometry.StadiumGoalHalfWidth;
        public const float GoalDangerDepth = -48f;
        public const float GoalDangerLateralMargin = 4f;
        public const float DefensiveClearanceDepth = -20f;
        public const float MaximumOwnGoalPredictionDistance = 36f;
        public const float MinimumShootingDepth = 20f;
        // Strong 4000, mass 3, damping 1의 실제 도달 범위를 보수적으로 제한한다.
        public const float MaximumShotPredictionDistance = 24f;

        const float BallTrajectoryMargin = 0.75f;
        const float MinimumSafeAttackingComponent = 0.15f;

        public static float GetAttackSign(Team team)
        {
            return team == Team.Red ? 1f : -1f;
        }

        public static float GetAttackingDepth(Team team, float worldX)
        {
            return worldX * GetAttackSign(team);
        }

        public static bool IsBallInOwnGoalDanger(Team team, Vector3 ballPosition, SoccerArenaGeometry arena = null)
        {
            return GetAttackingDepth(team, ballPosition.x) <= GoalDangerDepth
                && Mathf.Abs(ballPosition.z) <= (arena != null ? arena.GoalHalfWidth : GoalHalfWidth) + GoalDangerLateralMargin;
        }

        public static Vector3 GetOwnGoalCenter(Team team, float y, SoccerArenaGeometry arena = null)
        {
            return arena != null ? arena.GetGoalCenter(team, y)
                : new Vector3(-GetAttackSign(team) * GoalLineDepth, y, 0f);
        }

        public static Vector3 GetOpponentGoalCenter(Team team, float y, SoccerArenaGeometry arena = null)
        {
            return GetOwnGoalCenter(team == Team.Red ? Team.Navy : Team.Red, y, arena);
        }

        /// <summary>
        /// 골대 안이나 골문 앞에서도 필드 중앙을 향하는 안전한 패스 목표를 반환한다.
        /// </summary>
        public static Vector3 GetCentralClearanceTarget(float y)
        {
            return new Vector3(0f, y, 0f);
        }

        public static Vector3 GetSafeClearanceDirection(Team team, Vector3 ballPosition)
        {
            var direction = GetCentralClearanceTarget(ballPosition.y) - ballPosition;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }

            return Vector3.right * GetAttackSign(team);
        }

        public static bool PredictsOwnGoal(Team team, Vector3 ballPosition, Vector3 kickDirection, SoccerArenaGeometry arena = null)
        {
            var direction = FlattenAndNormalize(kickDirection);
            if (direction == Vector3.zero)
            {
                return false;
            }

            var attackSign = GetAttackSign(team);
            var ballDepth = GetAttackingDepth(team, ballPosition.x);
            var directionDepth = direction.x * attackSign;
            var goalDepth = arena != null ? arena.HalfLength : GoalLineDepth;
            var goalWidth = arena != null ? arena.GoalHalfWidth : GoalHalfWidth;
            if (ballDepth <= -goalDepth)
            {
                // 이미 골라인 안쪽이라면 필드 방향으로 나오는 공은 자책골 궤적으로 보지 않는다.
                return Mathf.Abs(ballPosition.z) <= goalWidth + BallTrajectoryMargin
                    && directionDepth <= 0f;
            }

            return PredictsGoalPlane(
                ballPosition,
                direction,
                -attackSign * goalDepth,
                MaximumOwnGoalPredictionDistance,
                goalWidth);
        }

        public static bool PredictsOpponentGoal(Team team, Vector3 ballPosition, Vector3 kickDirection, SoccerArenaGeometry arena = null)
        {
            var direction = FlattenAndNormalize(kickDirection);
            if (direction == Vector3.zero)
            {
                return false;
            }

            return PredictsGoalPlane(
                ballPosition,
                direction,
                GetAttackSign(team) * (arena != null ? arena.HalfLength : GoalLineDepth),
                MaximumShotPredictionDistance,
                arena != null ? arena.GoalHalfWidth : GoalHalfWidth);
        }

        /// <summary>
        /// 자책골이 예상되거나 골문 위험 지역에서 골 쪽·옆으로 차려는 입력만 중앙 패스로 바꾼다.
        /// 이미 안전한 측면 걷어내기와 정상 슛·패스는 그대로 둔다.
        /// </summary>
        public static Vector3 ResolveKickDirection(
            Team team,
            Vector3 ballPosition,
            Vector3 requestedDirection,
            out bool redirected)
        {
            return ResolveKickDirection(
                team,
                ballPosition,
                requestedDirection,
                Vector3.zero,
                1f,
                1f,
                1f,
                out redirected);
        }

        public static Vector3 ResolveKickDirection(
            Team team,
            Vector3 ballPosition,
            Vector3 requestedDirection,
            Vector3 currentBallVelocity,
            float kickPower,
            float ballMass,
            float fixedDeltaTime,
            out bool redirected,
            SoccerArenaGeometry arena = null)
        {
            var flattened = FlattenAndNormalize(requestedDirection);
            if (flattened == Vector3.zero)
            {
                flattened = Vector3.right * GetAttackSign(team);
            }

            var predictedVelocity = PredictPostStrikeVelocity(
                currentBallVelocity,
                flattened,
                kickPower,
                ballMass,
                fixedDeltaTime);
            var predictedDirection = FlattenAndNormalize(predictedVelocity);
            var attackingComponent = predictedDirection.x * GetAttackSign(team);
            redirected = PredictsOwnGoal(team, ballPosition, predictedVelocity, arena)
                || (IsBallInOwnGoalDanger(team, ballPosition, arena)
                    && attackingComponent <= MinimumSafeAttackingComponent);
            return redirected ? GetSafeClearanceDirection(team, ballPosition) : flattened;
        }

        public static Vector3 PredictPostStrikeVelocity(
            Vector3 currentBallVelocity,
            Vector3 strikeDirection,
            float kickPower,
            float ballMass,
            float fixedDeltaTime)
        {
            currentBallVelocity.y = 0f;
            var direction = FlattenAndNormalize(strikeDirection);
            var safeMass = Mathf.Max(0.0001f, ballMass);
            var velocityChange = Mathf.Max(0f, kickPower)
                / safeMass
                * Mathf.Max(0f, fixedDeltaTime);
            return currentBallVelocity + direction * velocityChange;
        }

        public static Vector3 RemoveOwnGoalwardVelocity(Team team, Vector3 velocity)
        {
            var attackSign = GetAttackSign(team);
            var attackingSpeed = velocity.x * attackSign;
            if (attackingSpeed < 0f)
            {
                velocity.x -= attackingSpeed * attackSign;
            }

            return velocity;
        }

        /// <summary>
        /// Strong Kick은 골문을 겨냥한 슛, 동료를 향한 긴 패스, 또는 안전한 수비 걷어내기일 때만 의미가 있다.
        /// </summary>
        public static bool IsMeaningfulStrongKick(
            Team team,
            Vector3 ballPosition,
            Vector3 kickDirection,
            bool hasTeammateInLane,
            SoccerArenaGeometry arena = null)
        {
            var direction = FlattenAndNormalize(kickDirection);
            if (direction == Vector3.zero)
            {
                return false;
            }

            var attackingComponent = direction.x * GetAttackSign(team);
            if (GetAttackingDepth(team, ballPosition.x) <= DefensiveClearanceDepth
                && attackingComponent > 0.35f)
            {
                return true;
            }

            if (hasTeammateInLane && attackingComponent > -0.1f)
            {
                return true;
            }

            return GetAttackingDepth(team, ballPosition.x) >= MinimumShootingDepth
                && PredictsOpponentGoal(team, ballPosition, direction, arena);
        }

        static bool PredictsGoalPlane(
            Vector3 origin,
            Vector3 direction,
            float goalWorldX,
            float maximumDistance,
            float goalHalfWidth)
        {
            if (Mathf.Abs(direction.x) < 0.0001f)
            {
                return false;
            }

            var travelDistance = (goalWorldX - origin.x) / direction.x;
            if (travelDistance < 0f || travelDistance > maximumDistance)
            {
                return false;
            }

            var crossingZ = origin.z + direction.z * travelDistance;
            return Mathf.Abs(crossingZ) <= goalHalfWidth + BallTrajectoryMargin;
        }

        static Vector3 FlattenAndNormalize(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.zero;
        }
    }
}
