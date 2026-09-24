using System;
using System.Linq;
using UnityEngine;

namespace MachineLearning.Soccer.Teams.Rule
{
    /// <summary>
    /// 선수별 규칙 상태.
    /// 신경망과 Trainer를 사용하지 않는 완전한 FSM 구성.
    /// </summary>
    public enum RuleSoccerState
    {
        ChaseLooseBall,
        CarryBall,
        PassBall,
        ShootBall,
        ClearBall,
        SupportAttack,
        PressBall,
        CoverDefense,
        RecoverGoal
    }

    /// <summary>
    /// Raycast와 경기 상태만으로 행동을 결정하는 규칙형 선수 두뇌.
    /// 공통 경기 규칙과 행동 계약은 강화학습 선수와 동일한 구성.
    /// Rule 담당자는 이 클래스의 FSM·목표·조향만 조정하고 공통 물리·보상 판정은 수정하지 않는다.
    /// RuleRewardProfile 값은 평가용이므로 변경해도 이 FSM의 Decide 결과에는 영향을 주지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RuleBasedSoccerController : MonoBehaviour, ISoccerRuleController
    {
        // Rule 전용 튜닝 영역. 한 가설에 필요한 임계값·가중치만 바꾸고 공통 계약은 복제하지 않는다.
        const float MinimumStateSeconds = 0.35f;
        const float BallControlDistance = 2.4f;
        const float PassMinimumDistance = 5f;
        const float PassMaximumDistance = 30f;
        const float PressureDistance = 6f;
        const float ShootDistance = 22f;
        const float MaximumCarryBeforePassSeconds = 1.5f;
        const float ObstacleRayDistance = 3.5f;
        const float AimThreshold = 0.62f;
        const float OwnDefensiveThird = 20f;
        const float TeammateSeparationWeight = 0.85f;

        [SerializeField] LayerMask rayMask = ~0;

        AgentSoccer m_Agent;
        SoccerEnvController m_Environment;
        RuleSoccerState m_State = RuleSoccerState.ChaseLooseBall;
        float m_StateEnteredTime;
        float m_CarryStartedTime;
        bool m_WasBallCarrier;
        AgentSoccer m_PassTarget;

        public RuleSoccerState State => m_State;

        public void Configure(AgentSoccer agent, SoccerEnvController environment)
        {
            m_Agent = agent;
            m_Environment = environment != null ? environment : GetComponentInParent<SoccerEnvController>();
        }

        public SoccerRuleCommand Decide()
        {
            EnsureReferences();
            if (m_Agent == null || m_Environment == null || m_Environment.Ball == null)
            {
                return new SoccerRuleCommand(0, 0, 0, 0);
            }

            SelectState();
            var actionTarget = GetStateTarget();
            var kick = GetKickAction(actionTarget);
            // 새 FSM 상태의 목표도 반드시 공통 골키퍼 보호 규칙을 통과한다.
            var movementTarget = m_Environment.ConstrainDefenderKeeperTarget(m_Agent, actionTarget);
            return BuildMovementCommand(movementTarget, kick);
        }

        public void ResetController()
        {
            m_State = RuleSoccerState.ChaseLooseBall;
            m_StateEnteredTime = Time.time;
            m_CarryStartedTime = Time.time;
            m_WasBallCarrier = false;
            m_PassTarget = null;
        }

        void EnsureReferences()
        {
            m_Agent ??= GetComponent<AgentSoccer>();
            m_Environment ??= GetComponentInParent<SoccerEnvController>();
        }

        /// <summary>
        /// 소유권·역할·거리 순서의 상태 선택.
        /// 짧은 상태 유지 시간을 통한 판단 진동 방지.
        /// </summary>
        void SelectState()
        {
            if (m_Agent.PositionRole == AgentSoccer.Position.DefenderKeeper
                && m_Environment.ShouldDefenderKeeperRecover(m_Agent))
            {
                m_PassTarget = null;
                SetState(RuleSoccerState.RecoverGoal, true);
                return;
            }

            var ownsBall = m_Environment.PossessionTeam == m_Agent.Team;
            var opponentOwnsBall = m_Environment.PossessionTeam.HasValue
                && m_Environment.PossessionTeam != m_Agent.Team;
            var controlsBall = IsBallCarrier();
            if (controlsBall && !m_WasBallCarrier)
            {
                m_CarryStartedTime = Time.time;
            }

            m_WasBallCarrier = controlsBall;
            var closestToBall = FindClosestTeamAgentToBall() == m_Agent;
            var nextState = m_State;

            if (controlsBall)
            {
                m_PassTarget = FindBestPassTarget();
                if (NeedsEmergencyClearance())
                {
                    nextState = RuleSoccerState.ClearBall;
                }
                else if (CanShoot())
                {
                    nextState = RuleSoccerState.ShootBall;
                }
                else if (m_PassTarget != null
                    && (IsUnderPressure() || Time.time - m_CarryStartedTime >= MaximumCarryBeforePassSeconds))
                {
                    nextState = RuleSoccerState.PassBall;
                }
                else
                {
                    nextState = RuleSoccerState.CarryBall;
                }
            }
            else if (ownsBall)
            {
                nextState = RuleSoccerState.SupportAttack;
            }
            else if (opponentOwnsBall)
            {
                nextState = closestToBall ? RuleSoccerState.PressBall : RuleSoccerState.CoverDefense;
            }
            else
            {
                nextState = closestToBall ? RuleSoccerState.ChaseLooseBall : RuleSoccerState.CoverDefense;
            }

            SetState(nextState, nextState is RuleSoccerState.ShootBall or RuleSoccerState.ClearBall);
        }

        void SetState(RuleSoccerState nextState, bool urgent)
        {
            if (nextState == m_State)
            {
                return;
            }

            if (!urgent && Time.time - m_StateEnteredTime < MinimumStateSeconds)
            {
                return;
            }

            m_State = nextState;
            m_StateEnteredTime = Time.time;
        }

        Vector3 GetStateTarget()
        {
            var ballPosition = m_Environment.Ball.transform.position;
            return m_State switch
            {
                RuleSoccerState.PassBall when m_PassTarget != null => m_PassTarget.transform.position,
                RuleSoccerState.ShootBall => GetOpponentGoal(),
                RuleSoccerState.ClearBall => GetClearanceTarget(),
                RuleSoccerState.CarryBall => GetCarryTarget(),
                RuleSoccerState.SupportAttack => GetAttackSupportTarget(),
                RuleSoccerState.CoverDefense => GetDefenseCoverTarget(),
                RuleSoccerState.RecoverGoal => m_Environment.GetDefenderKeeperHomeTarget(m_Agent),
                _ => ballPosition
            };
        }

        Vector3 GetCarryTarget()
        {
            var goal = GetOpponentGoal();
            var ball = m_Environment.Ball.transform.position;
            var laneSign = GetLaneSign();
            var target = Vector3.Lerp(ball, goal, 0.65f) + Vector3.forward * laneSign * 4f;
            return ClampToField(target);
        }

        Vector3 GetAttackSupportTarget()
        {
            var ball = m_Environment.Ball.transform.position;
            var attackSign = GetAttackSign();
            var laneSign = GetLaneSign();
            var target = m_Agent.PositionRole switch
            {
                AgentSoccer.Position.Striker => ball
                    + Vector3.right * attackSign * 14f
                    + Vector3.forward * laneSign * 9f,
                AgentSoccer.Position.DefenderKeeper => ball
                    - Vector3.right * attackSign * 16f
                    + Vector3.forward * laneSign * 7f,
                _ => ball
                    + Vector3.right * attackSign * (laneSign * attackSign > 0f ? 5f : -3f)
                    + Vector3.forward * laneSign * 12f
            };
            return ClampToField(target);
        }

        Vector3 GetDefenseCoverTarget()
        {
            var ball = m_Environment.Ball.transform.position;
            var ownGoal = GetOwnGoal();
            var laneSign = GetLaneSign();
            var coverRatio = m_Agent.PositionRole switch
            {
                AgentSoccer.Position.DefenderKeeper => 0.40f,
                AgentSoccer.Position.Midfielder => 0.28f,
                _ => 0.18f
            };
            var target = Vector3.Lerp(ball, ownGoal, coverRatio)
                + Vector3.forward * laneSign * 8f;
            return ClampToField(target);
        }

        Vector3 GetClearanceTarget()
        {
            if (SoccerDefensiveClearanceRules.IsBallInOwnGoalDanger(
                    m_Agent.Team,
                    m_Environment.Ball.transform.position))
            {
                return SoccerDefensiveClearanceRules.GetCentralClearanceTarget(
                    m_Agent.transform.position.y);
            }

            var attackSign = GetAttackSign();
            var laneSign = Mathf.Abs(m_Agent.transform.position.z) < 8f ? GetLaneSign() : -Mathf.Sign(m_Agent.transform.position.z);
            return new Vector3(attackSign * 26f, m_Agent.transform.position.y, laneSign * 24f);
        }

        int GetKickAction(Vector3 target)
        {
            if (m_State is not RuleSoccerState.PassBall
                and not RuleSoccerState.ShootBall
                and not RuleSoccerState.ClearBall)
            {
                return 0;
            }

            var ballOffset = m_Environment.Ball.transform.position - m_Agent.transform.position;
            ballOffset.y = 0f;
            var aimDirection = target - m_Agent.transform.position;
            aimDirection.y = 0f;
            if (ballOffset.sqrMagnitude > BallControlDistance * BallControlDistance
                || aimDirection.sqrMagnitude < 0.01f
                || Vector3.Dot(m_Agent.transform.forward, aimDirection.normalized) < AimThreshold)
            {
                return 0;
            }

            if (m_State == RuleSoccerState.PassBall)
            {
                return 1;
            }

            return m_State == RuleSoccerState.ClearBall
                && SoccerDefensiveClearanceRules.IsBallInOwnGoalDanger(
                    m_Agent.Team,
                    m_Environment.Ball.transform.position)
                ? 1
                : 2;
        }

        SoccerRuleCommand BuildMovementCommand(Vector3 target, int kick)
        {
            var movementDirection = GetMovementDirection(target);
            if (movementDirection.sqrMagnitude < 0.0001f)
            {
                return new SoccerRuleCommand(0, 0, 0, kick);
            }

            var localDirection = m_Agent.transform.InverseTransformDirection(movementDirection);
            var forward = localDirection.z < -0.35f ? 2 : 1;
            var rotation = localDirection.x > 0.10f ? 2 : localDirection.x < -0.10f ? 1 : 0;
            var lateral = GetObstacleAvoidanceAction();
            return new SoccerRuleCommand(forward, lateral, rotation, kick);
        }

        Vector3 GetMovementDirection(Vector3 target)
        {
            var offset = target - m_Agent.transform.position;
            offset.y = 0f;
            var desiredDirection = offset.sqrMagnitude >= 0.4f ? offset.normalized : Vector3.zero;
            var separation = GetTeammateSeparation();
            var steeredDirection = desiredDirection + separation * TeammateSeparationWeight;
            return steeredDirection.sqrMagnitude >= 0.0001f ? steeredDirection.normalized : Vector3.zero;
        }

        /// <summary>
        /// 가까운 동료에게서 멀어지는 Reynolds separation 조향.
        /// 물리 힘이나 탐색 할당 없이 기존 목표 방향에만 낮은 비중으로 합성한다.
        /// </summary>
        Vector3 GetTeammateSeparation()
        {
            var separation = Vector3.zero;
            var maximumDistanceSquared = SoccerFormationEvaluator.TeammateCrowdingDistance
                * SoccerFormationEvaluator.TeammateCrowdingDistance;
            foreach (var item in m_Environment.AgentsList)
            {
                var teammate = item?.Agent;
                if (teammate == null || teammate == m_Agent || teammate.Team != m_Agent.Team)
                {
                    continue;
                }

                var away = m_Agent.transform.position - teammate.transform.position;
                away.y = 0f;
                var distanceSquared = away.sqrMagnitude;
                if (distanceSquared >= maximumDistanceSquared)
                {
                    continue;
                }

                float distance;
                if (distanceSquared < 0.0001f)
                {
                    var side = m_Agent.GetInstanceID() <= teammate.GetInstanceID() ? -1f : 1f;
                    away = m_Agent.transform.right * side;
                    distance = 0f;
                }
                else
                {
                    distance = Mathf.Sqrt(distanceSquared);
                    away /= distance;
                }

                separation += away * SoccerFormationEvaluator.CalculateCrowdingSeverity(distance);
            }

            return Vector3.ClampMagnitude(separation, 1f);
        }

        /// <summary>
        /// 전방과 좌우 Raycast를 이용한 최종 조향 보정.
        /// FSM 상태와 분리된 장애물 회피 구성.
        /// </summary>
        int GetObstacleAvoidanceAction()
        {
            var origin = m_Agent.transform.position + Vector3.up * 0.45f;
            if (!IsBlockingRay(origin, m_Agent.transform.forward, ObstacleRayDistance))
            {
                return 0;
            }

            var leftDirection = Quaternion.Euler(0f, -35f, 0f) * m_Agent.transform.forward;
            var rightDirection = Quaternion.Euler(0f, 35f, 0f) * m_Agent.transform.forward;
            var leftBlocked = IsBlockingRay(origin, leftDirection, ObstacleRayDistance);
            var rightBlocked = IsBlockingRay(origin, rightDirection, ObstacleRayDistance);
            if (leftBlocked == rightBlocked)
            {
                return GetLaneSign() * GetAttackSign() > 0f ? 1 : 2;
            }

            return leftBlocked ? 1 : 2;
        }

        AgentSoccer FindBestPassTarget()
        {
            AgentSoccer bestTarget = null;
            var bestScore = float.NegativeInfinity;
            var attackSign = GetAttackSign();
            foreach (var teammate in GetTeamAgents())
            {
                if (teammate == m_Agent)
                {
                    continue;
                }

                var distance = Vector3.Distance(m_Agent.transform.position, teammate.transform.position);
                if (distance is < PassMinimumDistance or > PassMaximumDistance
                    || !HasClearLane(teammate.transform.position, teammate))
                {
                    continue;
                }

                var progress = (teammate.transform.position.x - m_Agent.transform.position.x) * attackSign;
                var space = GetNearestOpponentDistance(teammate.transform.position);
                var score = progress * 1.4f + Mathf.Min(space, 10f) * 0.7f - distance * 0.15f;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestTarget = teammate;
            }

            return bestTarget;
        }

        bool CanShoot()
        {
            var goal = GetOpponentGoal();
            return Vector3.Distance(m_Agent.transform.position, goal) <= ShootDistance
                && HasClearLane(goal, null);
        }

        bool NeedsEmergencyClearance()
        {
            var ballPosition = m_Environment.Ball.transform.position;
            if (SoccerDefensiveClearanceRules.IsBallInOwnGoalDanger(m_Agent.Team, ballPosition))
            {
                return true;
            }

            var defensiveDepth = SoccerDefensiveClearanceRules.GetAttackingDepth(
                m_Agent.Team,
                ballPosition.x) < -OwnDefensiveThird;
            return defensiveDepth && IsUnderPressure();
        }

        bool IsUnderPressure()
        {
            return GetNearestOpponentDistance(m_Agent.transform.position) <= PressureDistance;
        }

        bool IsBallCarrier()
        {
            if (m_Environment.BallCarrier != null)
            {
                return m_Environment.BallCarrier == m_Agent;
            }

            // 패스 비행 등 carrier가 잠시 비어 있는 경우에만 거리 기반 보조 판정을 사용한다.
            return m_Environment.PossessionTeam == m_Agent.Team
                && Vector3.Distance(m_Agent.transform.position, m_Environment.Ball.transform.position) <= BallControlDistance;
        }

        AgentSoccer FindClosestTeamAgentToBall()
        {
            var ballPosition = m_Environment.Ball.transform.position;
            return GetTeamAgents()
                .Where(agent => m_Environment.CanChaseBall(agent))
                .OrderBy(agent => (agent.transform.position - ballPosition).sqrMagnitude)
                .FirstOrDefault();
        }

        AgentSoccer[] GetTeamAgents()
        {
            return m_Environment.AgentsList
                .Select(item => item?.Agent)
                .Where(agent => agent != null && agent.Team == m_Agent.Team)
                .ToArray();
        }

        float GetNearestOpponentDistance(Vector3 position)
        {
            var distance = float.PositiveInfinity;
            foreach (var item in m_Environment.AgentsList)
            {
                var opponent = item?.Agent;
                if (opponent == null || opponent.Team == m_Agent.Team)
                {
                    continue;
                }

                distance = Mathf.Min(distance, Vector3.Distance(position, opponent.transform.position));
            }

            return distance;
        }

        /// <summary>
        /// 패스와 슛 경로의 Raycast 가시성 판정.
        /// 자기 몸체와 공은 무시하고 첫 장애물만 판정하는 구성.
        /// </summary>
        bool HasClearLane(Vector3 target, AgentSoccer intendedReceiver)
        {
            var origin = m_Agent.transform.position + Vector3.up * 0.45f;
            var offset = target - origin;
            var distance = offset.magnitude;
            if (distance < 0.1f)
            {
                return true;
            }

            var hits = Physics.RaycastAll(origin, offset / distance, distance, rayMask, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(m_Agent.transform) || hit.collider.CompareTag("ball"))
                {
                    continue;
                }

                var hitAgent = hit.collider.GetComponentInParent<AgentSoccer>();
                if (intendedReceiver != null && hitAgent == intendedReceiver)
                {
                    return true;
                }

                return false;
            }

            return true;
        }

        bool IsBlockingRay(Vector3 origin, Vector3 direction, float distance)
        {
            var hits = Physics.RaycastAll(origin, direction, distance, rayMask, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.transform.IsChildOf(m_Agent.transform) || hit.collider.CompareTag("ball"))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        float GetAttackSign()
        {
            return m_Agent.Team == Team.Red ? 1f : -1f;
        }

        float GetLaneSign()
        {
            if (Mathf.Abs(m_Agent.StartingPosition.z) > 0.5f)
            {
                return Mathf.Sign(m_Agent.StartingPosition.z);
            }

            return (m_Agent.Team == Team.Red ? 1f : -1f)
                * (m_Agent.PositionRole == AgentSoccer.Position.Striker ? 1f : -1f);
        }

        Vector3 GetOwnGoal()
        {
            return SoccerDefensiveClearanceRules.GetOwnGoalCenter(
                m_Agent.Team,
                m_Agent.transform.position.y,
                m_Environment != null ? m_Environment.ArenaGeometry : null);
        }

        Vector3 GetOpponentGoal()
        {
            return SoccerDefensiveClearanceRules.GetOpponentGoalCenter(
                m_Agent.Team,
                m_Agent.transform.position.y,
                m_Environment != null ? m_Environment.ArenaGeometry : null);
        }

        Vector3 ClampToField(Vector3 target)
        {
            if (m_Environment != null && m_Environment.ArenaGeometry != null)
            {
                return m_Environment.ArenaGeometry.ClampTarget(target);
            }

            const float playerMargin = 0.55f;
            target.z = Mathf.Clamp(target.z,
                -SoccerArenaGeometry.StadiumHalfWidth + playerMargin,
                SoccerArenaGeometry.StadiumHalfWidth - playerMargin);
            target.x = Mathf.Clamp(target.x,
                -SoccerArenaGeometry.StadiumHalfLength - SoccerArenaGeometry.StadiumGoalDepth + playerMargin,
                SoccerArenaGeometry.StadiumHalfLength + SoccerArenaGeometry.StadiumGoalDepth - playerMargin);
            return target;
        }
    }
}
