using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MachineLearning.Soccer
{
    public enum SoccerMatchState
    {
        Playing,
        GoalPause,
        Finished
    }

    /// <summary>
    /// 5분 경기, 킥오프 복귀, 양 팀 그룹의 단일 관리 지점.
    /// </summary>
    public sealed class SoccerEnvController : MonoBehaviour
    {
        [Serializable]
        public sealed class PlayerInfo
        {
            public AgentSoccer Agent;
            [HideInInspector] public Vector3 StartingPos;
            [HideInInspector] public Quaternion StartingRot;
            [HideInInspector] public Rigidbody Rb;
        }

        [Header("Match")]
        [Min(1f)] public float matchDurationSeconds = 300f;
        [Min(0f)] public float goalResetDelaySeconds = 3f;
        [SerializeField] bool startInAIMode = true;

        [Header("References")]
        public GameObject ball;
        [HideInInspector] public Rigidbody ballRb;
        public List<PlayerInfo> AgentsList = new();
        [SerializeField] SoccerArenaGeometry arenaGeometry;

        [Header("Kick tuning")]
        [SerializeField, Min(0f)] float controlledKickPower = AgentSoccer.ControlledKickPower;
        [SerializeField, Min(0f)] float strongKickPower = AgentSoccer.StrongKickPower;

        public SoccerArenaGeometry ArenaGeometry => arenaGeometry;
        public float ControlledKickPower => controlledKickPower;
        public float StrongKickPower => strongKickPower;

        public void ConfigureControlledKickPower(float power)
        {
            controlledKickPower = Mathf.Max(0f, power);
        }

        public void ConfigureStrongKickPower(float power)
        {
            strongKickPower = Mathf.Max(0f, power);
        }

        public void ConfigureArena(SoccerArenaGeometry geometry)
        {
            arenaGeometry = geometry;
        }

        SimpleMultiAgentGroup m_RedAgentGroup;
        SimpleMultiAgentGroup m_NavyAgentGroup;
        Vector3 m_BallStartingPos;
        Quaternion m_BallStartingRotation;
        float m_GoalResetRemaining;
        bool m_IsTraining;
        bool m_AIEnabled;
        bool m_KickActionsEnabled = true;
        bool m_CurriculumControlledKicks;
        bool m_NeuralShotAdviceEnabled = true;
        bool m_CurriculumPassAimGate;
        bool m_CurriculumPassAdvice;
        readonly HashSet<AgentSoccer> m_CurriculumWaitingAgents = new();
        readonly HashSet<AgentSoccer> m_CurriculumReceivingAgents = new();
        AgentSoccer m_CurriculumPassSupportAgent;
        AgentSoccer m_CurriculumPassSupportAnchor;
        Team m_LastScoringTeam;
        SoccerMatchSetup m_MatchSetup;
        SoccerRewardEngine m_RewardEngine;

        public event Action RoundResetCompleted;
        public event Action<AgentSoccer, int, Vector3, bool> BallStrikeCompleted;
        public event Action<AgentSoccer> BallTouchCompleted;
        public event Action MatchTimedOut;

        public int RedScore { get; private set; }
        public int NavyScore { get; private set; }
        public float RemainingTime { get; private set; }
        public float GoalResetRemaining => m_GoalResetRemaining;
        public SoccerMatchState State { get; private set; } = SoccerMatchState.Playing;
        public bool IsPlayActive => State == SoccerMatchState.Playing && RemainingTime > 0f;
        public bool IsAIEnabled => m_AIEnabled;
        public bool StartsInAIMode => startInAIMode;
        public bool IsTraining => m_IsTraining;
        public bool KickActionsEnabled => m_KickActionsEnabled;
        public GameObject Ball => ball;
        public Team? PossessionTeam => m_RewardEngine != null ? m_RewardEngine.PossessionTeam : null;
        public Team? LastTouchTeam => m_RewardEngine != null ? m_RewardEngine.LastTouchTeam : null;
        public AgentSoccer BallCarrier => m_RewardEngine != null ? m_RewardEngine.BallCarrier : null;
        public AgentSoccer HumanControlledAgent { get; private set; }

        void Start()
        {
            if (ball == null)
            {
                Debug.LogError("SoccerEnvController requires a ball reference.", this);
                enabled = false;
                return;
            }

            m_RedAgentGroup = new SimpleMultiAgentGroup();
            m_NavyAgentGroup = new SimpleMultiAgentGroup();
            m_MatchSetup = GetComponent<SoccerMatchSetup>();
            m_RewardEngine = GetComponent<SoccerRewardEngine>();
            m_RewardEngine?.Configure(this, m_MatchSetup);
            m_MatchSetup?.ApplyDefinitions();
            ballRb = ball.GetComponent<Rigidbody>();
            m_BallStartingPos = ball.transform.position;
            m_BallStartingRotation = ball.transform.rotation;
            m_IsTraining = Academy.IsInitialized && Academy.Instance.IsCommunicatorOn;
            m_AIEnabled = startInAIMode || m_IsTraining;

            foreach (var item in AgentsList)
            {
                if (item?.Agent == null)
                {
                    continue;
                }

                item.StartingPos = item.Agent.transform.position;
                item.StartingRot = item.Agent.transform.rotation;
                item.Rb = item.Agent.GetComponent<Rigidbody>();
                if (item.Agent.HumanControllable && HumanControlledAgent == null)
                {
                    HumanControlledAgent = item.Agent;
                }

                if (item.Agent.Team == Team.Red)
                {
                    m_RedAgentGroup.RegisterAgent(item.Agent);
                }
                else
                {
                    m_NavyAgentGroup.RegisterAgent(item.Agent);
                }
            }

            ApplyControlModes();
            RestartMatch();
        }

        void Update()
        {
            ReadModeToggle();
            if (State == SoccerMatchState.Finished)
            {
                return;
            }

            RemainingTime = Mathf.Max(0f, RemainingTime - Time.deltaTime);
            if (RemainingTime <= 0f)
            {
                FinishMatch();
                return;
            }

            if (State != SoccerMatchState.GoalPause)
            {
                return;
            }

            m_GoalResetRemaining = Mathf.Max(0f, m_GoalResetRemaining - Time.deltaTime);
            if (m_GoalResetRemaining <= 0f)
            {
                ResetRound();
            }
        }

        void ReadModeToggle()
        {
            if (Application.isBatchMode || m_IsTraining)
            {
                return;
            }

            var keyboardToggle = Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame;
            var gamepadToggle = Gamepad.current != null && Gamepad.current.buttonNorth.wasPressedThisFrame;
            if (keyboardToggle || gamepadToggle)
            {
                SetAIEnabled(!m_AIEnabled);
            }
        }

        public void SetAIEnabled(bool enabledAI)
        {
            if (m_IsTraining || m_AIEnabled == enabledAI)
            {
                return;
            }

            m_AIEnabled = enabledAI;
            ApplyControlModes();
        }

        void ApplyControlModes()
        {
            foreach (var item in AgentsList)
            {
                if (item?.Agent == null)
                {
                    continue;
                }

                var useHumanInput = !m_IsTraining && !m_AIEnabled && item.Agent == HumanControlledAgent;
                var trainable = m_MatchSetup == null || m_MatchSetup.IsTrainable(item.Agent.Team);
                item.Agent.ConfigureControlMode(m_IsTraining, useHumanInput, trainable);
            }
        }

        public void NotifyBallTouch(AgentSoccer agent)
        {
            m_RewardEngine?.NotifyBallTouch(agent);
            BallTouchCompleted?.Invoke(agent);
        }

        public void NotifyBallStrike(
            AgentSoccer agent,
            int kickAction,
            Vector3 kickDirection,
            bool safetyRedirected)
        {
            m_RewardEngine?.NotifyBallStrike(agent, kickAction, kickDirection, safetyRedirected);
            BallStrikeCompleted?.Invoke(agent, kickAction, kickDirection, safetyRedirected);
        }

        public void AddTeamReward(Team rewardTeam, float reward)
        {
            if (rewardTeam == Team.Red)
            {
                m_RedAgentGroup?.AddGroupReward(reward);
            }
            else
            {
                m_NavyAgentGroup?.AddGroupReward(reward);
            }
        }

        public float GetCumulativeReward(Team team)
        {
            return m_RewardEngine != null ? m_RewardEngine.GetCumulativeReward(team) : 0f;
        }

        public string GetActiveTacticLabel(Team team)
        {
            var teamAgents = AgentsList
                .Where(item => item?.Agent != null && item.Agent.Team == team)
                .Select(item => item.Agent)
                .ToArray();
            if (teamAgents.Length == 0)
            {
                return "UNKNOWN";
            }

            var modelKeys = teamAgents
                .Select(agent => agent.ActiveModel != null ? agent.ActiveModel.name : "<none>")
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (modelKeys.Length != 1)
            {
                return "MIXED · 모델 불일치";
            }

            if (modelKeys[0] != "<none>")
            {
                return SoccerModelNaming.TryParse(modelKeys[0], out var tactic, out _, out _)
                    ? $"{SoccerModelNaming.GetKoreanName(tactic)} · {modelKeys[0]}"
                    : $"UNKNOWN · {modelKeys[0]}";
            }

            m_MatchSetup ??= GetComponent<SoccerMatchSetup>();
            var definition = m_MatchSetup != null ? m_MatchSetup.GetDefinition(team) : null;
            if (definition != null && !definition.UsesNeuralPolicy)
            {
                return "규칙형 · RULE";
            }

            return definition != null
                ? $"{definition.DisplayName.ToUpperInvariant()} · 모델 없음"
                : "모델 없음";
        }

        public void ConfigureStartMode(bool startsWithAI)
        {
            startInAIMode = startsWithAI;
        }

        public void ConfigureCurriculumConstraints(bool kickActionsEnabled, bool controlledKicks = false)
        {
            m_KickActionsEnabled = kickActionsEnabled;
            m_CurriculumControlledKicks = controlledKicks;
        }

        public void ConfigureNeuralShotAdvice(bool enabled)
        {
            m_NeuralShotAdviceEnabled = enabled;
        }

        public void SetCurriculumWaitingAgent(AgentSoccer actor, bool waiting)
        {
            if (actor == null) return;
            if (waiting) m_CurriculumWaitingAgents.Add(actor);
            else m_CurriculumWaitingAgents.Remove(actor);
        }

        public void ClearCurriculumWaitingAgents() => m_CurriculumWaitingAgents.Clear();

        public void ConfigureCurriculumPassAimGate(bool enabled) => m_CurriculumPassAimGate = enabled;

        public void ConfigureCurriculumPassAdvice(bool enabled) => m_CurriculumPassAdvice = enabled;

        public void ConfigureCurriculumPassSupport(AgentSoccer actor, AgentSoccer anchor)
        {
            m_CurriculumPassSupportAgent = actor;
            m_CurriculumPassSupportAnchor = anchor;
        }

        public bool TryGetCurriculumPassSupportDirection(AgentSoccer actor, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (!m_CurriculumPassAdvice || actor == null || actor != m_CurriculumPassSupportAgent
                || m_CurriculumReceivingAgents.Contains(actor)
                || actor.IsRuleControlled || actor.IsUsingHumanInput) return false;
            var anchor = BallCarrier != null ? BallCarrier : m_CurriculumPassSupportAnchor;
            if (anchor == null || anchor == actor) return false;
            var sign = actor.Team == Team.Red ? 1f : -1f;
            var destination = anchor.transform.position + new Vector3(8f * sign, 0f, 0f);
            direction = destination - actor.transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < .25f) direction = Vector3.zero;
            else direction.Normalize();
            if (Academy.IsInitialized)
            {
                var recorder = Academy.Instance.StatsRecorder;
                recorder.Add("Soccer/Skill Advice/Pass Support Positioning Seconds",
                    Time.fixedDeltaTime, StatAggregationMethod.Sum);
                recorder.Add($"Soccer/{actor.Team}/Skill Advice/Pass Support Positioning Seconds",
                    Time.fixedDeltaTime, StatAggregationMethod.Sum);
            }
            return true;
        }

        public void SetCurriculumReceivingAgent(AgentSoccer actor, bool receiving)
        {
            if (actor == null) return;
            if (receiving) m_CurriculumReceivingAgents.Add(actor);
            else m_CurriculumReceivingAgents.Remove(actor);
        }

        public void ClearCurriculumReceivingAgents() => m_CurriculumReceivingAgents.Clear();

        public bool IsCurriculumReceivingAgent(AgentSoccer actor)
        {
            if (!m_CurriculumPassAdvice || actor == null || !m_CurriculumReceivingAgents.Contains(actor)
                || actor.IsRuleControlled || actor.IsUsingHumanInput) return false;
            if (Academy.IsInitialized)
            {
                var recorder = Academy.Instance.StatsRecorder;
                recorder.Add("Soccer/Skill Advice/Pass Receiver Chase Seconds",
                    Time.fixedDeltaTime, StatAggregationMethod.Sum);
                recorder.Add($"Soccer/{actor.Team}/Skill Advice/Pass Receiver Chase Seconds",
                    Time.fixedDeltaTime, StatAggregationMethod.Sum);
            }
            return true;
        }

        public bool IsCurriculumPassAimGateActive(AgentSoccer actor)
        {
            if (!m_CurriculumPassAimGate || actor == null || actor.IsUsingHumanInput || actor.IsRuleControlled)
                return false;
            var behavior = actor.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
            return behavior != null
                && behavior.BehaviorType != Unity.MLAgents.Policies.BehaviorType.HeuristicOnly;
        }

        public bool IsCurriculumPassAimBlocked(AgentSoccer actor)
        {
            // Physical kick readiness remains separate from this opt-in teaching gate.
            if (!IsCurriculumPassAimGateActive(actor) || !CanRequestKick(actor)
                || ball == null || BallCarrier != actor) return false;
            var position = ball.transform.position;
            var direction = AgentSoccer.CalculateBallPushDirection(
                actor.transform.position, actor.transform.forward, position, true);
            var goal = SoccerDefensiveClearanceRules.GetOpponentGoalCenter(actor.Team, position.y, arenaGeometry);
            foreach (var item in AgentsList)
            {
                var target = item?.Agent;
                if (target == null || target == actor || !target.isActiveAndEnabled || target.Team != actor.Team) continue;
                if (SoccerCurriculumPassRules.IsAdvantageousTarget(actor.transform.position, target.transform.position, goal)
                    && SoccerCurriculumPassRules.IsDirectedAtTarget(position, direction, target.transform.position)) return false;
            }
            return true;
        }

        public bool IsCurriculumWaitingAgent(AgentSoccer actor)
        {
            if (actor == null || !m_CurriculumWaitingAgents.Contains(actor)
                || actor.IsUsingHumanInput || actor.IsRuleControlled) return false;
            var behavior = actor.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
            return behavior != null
                && behavior.BehaviorType != Unity.MLAgents.Policies.BehaviorType.HeuristicOnly;
        }

        public bool CanRequestKick(AgentSoccer actor)
        {
            if (IsCurriculumWaitingAgent(actor)) return false;
            if (!m_KickActionsEnabled) return false;
            if (!m_CurriculumControlledKicks) return true;
            if (actor == null || ball == null || BallCarrier != actor) return false;
            var offset = ball.transform.position - actor.transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= 1.8f * 1.8f;
        }

        public int ResolveNeuralKickAction(AgentSoccer actor, int requestedAction)
        {
            if (IsCurriculumPassAdviceActive(actor) && FindCurriculumPassAdviceTarget(actor) != null)
            {
                RecordPassAdvice(actor, requestedAction == 0 ? "Pass Recommended" : "Requested Pass Aimed");
                // A 6-16m handoff must leave the passer's plate before its next
                // contact; teach Strong while keeping the Neural timing/target gate.
                return 2;
            }
            if (!m_NeuralShotAdviceEnabled || actor == null || ball == null
                || actor.IsRuleControlled || actor.IsUsingHumanInput)
            {
                return requestedAction;
            }

            var resolved = SoccerNeuralKickAdvisor.ResolveShotAction(
                actor.Team,
                actor.transform.position,
                actor.transform.forward,
                ball.transform.position,
                requestedAction,
                BallCarrier == actor,
                CanRequestKick(actor) && actor.KickPlate != null && actor.KickPlate.CanKick,
                arenaGeometry,
                out var advice);
            if (advice != SoccerNeuralKickAdvice.None && Academy.IsInitialized)
            {
                var label = advice == SoccerNeuralKickAdvice.ShotRecommended
                    ? "Shot Recommended"
                    : "Off Target Shot Deferred";
                var recorder = Academy.Instance.StatsRecorder;
                recorder.Add($"Soccer/Skill Advice/{label}", 1f, StatAggregationMethod.Sum);
                recorder.Add($"Soccer/{actor.Team}/Skill Advice/{label}", 1f, StatAggregationMethod.Sum);
            }

            return resolved;
        }

        public Vector3 ResolveNeuralPassDirection(AgentSoccer actor, Vector3 fallback)
        {
            if (!m_CurriculumPassAdvice || actor == null || ball == null || BallCarrier != actor
                || actor.IsRuleControlled || actor.IsUsingHumanInput) return fallback;
            var behavior = actor.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
            if (behavior == null || behavior.BehaviorType == Unity.MLAgents.Policies.BehaviorType.HeuristicOnly)
                return fallback;
            var target = FindCurriculumPassAdviceTarget(actor);
            if (target == null) return fallback;
            RecordPassAdvice(actor, "Pass Direction Aimed");
            return SoccerNeuralPassAdvisor.ResolveDirection(ball.transform.position, target, fallback);
        }

        bool IsCurriculumPassAdviceActive(AgentSoccer actor)
        {
            if (!m_CurriculumPassAdvice || actor == null || ball == null || BallCarrier != actor
                || actor.IsRuleControlled || actor.IsUsingHumanInput || !CanRequestKick(actor)
                || actor.KickPlate == null || !actor.KickPlate.CanKick) return false;
            var behavior = actor.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
            return behavior != null && behavior.BehaviorType != Unity.MLAgents.Policies.BehaviorType.HeuristicOnly;
        }

        AgentSoccer FindCurriculumPassAdviceTarget(AgentSoccer actor)
        {
            var position = ball.transform.position;
            var goal = SoccerDefensiveClearanceRules.GetOpponentGoalCenter(actor.Team, position.y, arenaGeometry);
            return SoccerNeuralPassAdvisor.FindTarget(actor, position, goal, AgentsList);
        }

        static void RecordPassAdvice(AgentSoccer actor, string label)
        {
            if (!Academy.IsInitialized) return;
            var recorder = Academy.Instance.StatsRecorder;
            recorder.Add($"Soccer/Skill Advice/{label}", 1f, StatAggregationMethod.Sum);
            recorder.Add($"Soccer/{actor.Team}/Skill Advice/{label}", 1f, StatAggregationMethod.Sum);
        }

        public void GoalTouched(Team scoredTeam)
        {
            if (!IsPlayActive)
            {
                return;
            }

            m_LastScoringTeam = scoredTeam;
            if (scoredTeam == Team.Red)
            {
                RedScore++;
            }
            else
            {
                NavyScore++;
            }

            if (m_RewardEngine != null)
            {
                m_RewardEngine.AwardGoal(scoredTeam);
            }
            else
            {
                AddTeamReward(scoredTeam, 1f);
                AddTeamReward(scoredTeam == Team.Red ? Team.Navy : Team.Red, -1f);
            }

            State = SoccerMatchState.GoalPause;
            m_GoalResetRemaining = goalResetDelaySeconds;
            FreezeRound();
            if (goalResetDelaySeconds <= 0f)
            {
                ResetRound();
            }
        }

        public void RestartMatch()
        {
            RedScore = 0;
            NavyScore = 0;
            RemainingTime = matchDurationSeconds;
            m_GoalResetRemaining = 0f;
            m_RewardEngine?.ResetMatch();
            ResetRound();
        }

        public void ResetScene()
        {
            ResetRound();
        }

        public void ResetBall()
        {
            if (ballRb != null)
            {
                ballRb.isKinematic = false;
            }

            ball.transform.SetPositionAndRotation(m_BallStartingPos, m_BallStartingRotation);
            if (ballRb != null)
            {
                ballRb.linearVelocity = Vector3.zero;
                ballRb.angularVelocity = Vector3.zero;
                ballRb.Sleep();
            }
        }

        void ResetRound()
        {
            foreach (var item in AgentsList)
            {
                if (item?.Agent == null)
                {
                    continue;
                }

                item.Agent.transform.SetPositionAndRotation(item.StartingPos, item.StartingRot);
                item.Agent.ResetKickPlate();
                if (item.Rb != null)
                {
                    item.Rb.linearVelocity = Vector3.zero;
                    item.Rb.angularVelocity = Vector3.zero;
                    item.Rb.Sleep();
                }
            }

            ResetBall();
            m_RewardEngine?.ResetPossession();
            State = SoccerMatchState.Playing;
            m_GoalResetRemaining = 0f;
            RoundResetCompleted?.Invoke();
        }

        void FreezeRound()
        {
            if (ballRb != null)
            {
                ballRb.linearVelocity = Vector3.zero;
                ballRb.angularVelocity = Vector3.zero;
                ballRb.isKinematic = true;
            }

            foreach (var item in AgentsList)
            {
                if (item?.Rb == null)
                {
                    continue;
                }

                item.Rb.linearVelocity = Vector3.zero;
                item.Rb.angularVelocity = Vector3.zero;
            }
        }

        void FinishMatch()
        {
            State = SoccerMatchState.Finished;
            RemainingTime = 0f;
            FreezeRound();
            MatchTimedOut?.Invoke();

            if (RedScore > NavyScore)
            {
                if (m_RewardEngine != null)
                {
                    m_RewardEngine.AwardMatchResult(Team.Red);
                }
                else
                {
                    AddTeamReward(Team.Red, 0.5f);
                    AddTeamReward(Team.Navy, -0.5f);
                }
            }
            else if (NavyScore > RedScore)
            {
                if (m_RewardEngine != null)
                {
                    m_RewardEngine.AwardMatchResult(Team.Navy);
                }
                else
                {
                    AddTeamReward(Team.Navy, 0.5f);
                    AddTeamReward(Team.Red, -0.5f);
                }
            }

            m_RewardEngine?.RecordMatchRewardSummary();

            m_RedAgentGroup.EndGroupEpisode();
            m_NavyAgentGroup.EndGroupEpisode();
            if (m_IsTraining)
            {
                RestartMatch();
            }
        }

        /// <summary>
        /// 짧은 커리큘럼 과제를 성공 즉시 하나의 그룹 에피소드로 확정하고 다음 과제를 시작한다.
        /// 일반 경기의 득점/종료 경로에는 영향을 주지 않는다.
        /// </summary>
        public void CompleteTrainingDrill()
        {
            if (State == SoccerMatchState.Finished || m_RedAgentGroup == null || m_NavyAgentGroup == null)
            {
                return;
            }

            State = SoccerMatchState.Finished;
            RemainingTime = 0f;
            FreezeRound();
            m_RewardEngine?.RecordMatchRewardSummary();
            m_RedAgentGroup.EndGroupEpisode();
            m_NavyAgentGroup.EndGroupEpisode();
            RestartMatch();
        }

        public Vector3 GetAutonomousTarget(AgentSoccer requester)
        {
            if (requester == null || ball == null)
            {
                return transform.position;
            }

            var attackSign = requester.Team == Team.Red ? 1f : -1f;
            if (m_RewardEngine?.BallCarrier == requester)
            {
                if (TryGetAutonomousKickTarget(requester, out var kickTarget, out _))
                {
                    return ClampFieldTarget(ConstrainDefenderKeeperTarget(requester, kickTarget));
                }

                // 모델 없는 공통 fallback은 중원에서 즉시 Strong Kick을 반복하지 않고
                // 공을 몸 앞에 둔 채 전진 lane으로 운반한다.
                var carryTarget = ball.transform.position
                    + Vector3.right * (attackSign * 10f)
                    + Vector3.forward * (GetLaneSign(requester) * 3f);
                return ClampFieldTarget(ConstrainDefenderKeeperTarget(requester, carryTarget));
            }

            var closest = FindClosestTeammateToBall(requester.Team);
            if (closest == requester)
            {
                return ClampFieldTarget(ConstrainDefenderKeeperTarget(requester, ball.transform.position));
            }

            var laneSign = GetLaneSign(requester);
            var ownsBall = PossessionTeam == requester.Team;
            var opponentOwnsBall = PossessionTeam.HasValue && PossessionTeam != requester.Team;
            Vector3 target;

            if (ownsBall)
            {
                // 공 위치 기준의 유동적인 공격 지원 위치.
                target = requester.PositionRole switch
                {
                    AgentSoccer.Position.DefenderKeeper => ball.transform.position
                        + Vector3.right * (-attackSign * 16f)
                        + Vector3.forward * (laneSign * 6f),
                    AgentSoccer.Position.Striker => ball.transform.position
                        + Vector3.right * (attackSign * 14f)
                        + Vector3.forward * (laneSign * 8f),
                    _ => ball.transform.position
                        + Vector3.right * (attackSign * (laneSign * attackSign > 0f ? 4f : -4f))
                        + Vector3.forward * (laneSign * 11f)
                };
            }
            else if (opponentOwnsBall)
            {
                // 공과 자기 골문 사이의 동적 수비 지원 위치.
                var ownGoal = arenaGeometry != null
                    ? arenaGeometry.GetGoalCenter(requester.Team, ball.transform.position.y)
                        + Vector3.right * (attackSign * 4f)
                    : new Vector3(-attackSign * 58f, ball.transform.position.y, 0f);
                var coverRatio = requester.PositionRole switch
                {
                    AgentSoccer.Position.DefenderKeeper => 0.38f,
                    AgentSoccer.Position.Midfielder => 0.27f,
                    _ => 0.18f
                };
                target = Vector3.Lerp(ball.transform.position, ownGoal, coverRatio)
                    + Vector3.forward * (laneSign * (requester.PositionRole == AgentSoccer.Position.Striker ? 5f : 9f));
            }
            else
            {
                // 소유권 미정 상황의 넓은 세컨드 볼 대형.
                var depth = requester.PositionRole == AgentSoccer.Position.DefenderKeeper ? -14f
                    : requester.PositionRole == AgentSoccer.Position.Striker ? 10f : 0f;
                target = ball.transform.position
                    + Vector3.right * (attackSign * depth)
                    + Vector3.forward * (laneSign * 10f);
            }

            return ClampFieldTarget(ConstrainDefenderKeeperTarget(requester, target));
        }

        public bool ShouldDefenderKeeperRecover(AgentSoccer agent)
        {
            return SoccerDefenderKeeperRules.ShouldRecover(agent, this);
        }

        public bool CanChaseBall(AgentSoccer agent)
        {
            if (agent == null || agent.PositionRole != AgentSoccer.Position.DefenderKeeper)
            {
                return true;
            }

            if (ball == null)
            {
                return false;
            }

            var mode = SoccerDefenderKeeperRules.GetMode(agent, this);
            if (mode == SoccerDefenderKeeperRules.DefenderKeeperMode.RecoverGoal)
            {
                return false;
            }

            return mode == SoccerDefenderKeeperRules.DefenderKeeperMode.EngageBall
                || SoccerDefenderKeeperRules.GetAttackingDepth(agent.Team, ball.transform.position.x)
                    <= SoccerDefenderKeeperRules.DefensiveEngagementDepth;
        }

        public Vector3 GetDefenderKeeperHomeTarget(AgentSoccer agent)
        {
            return SoccerDefenderKeeperRules.GetHomeTarget(agent, this);
        }

        public Vector3 ConstrainDefenderKeeperTarget(AgentSoccer agent, Vector3 target)
        {
            return SoccerDefenderKeeperRules.ConstrainTarget(agent, this, target);
        }

        /// <summary>
        /// 모델 없는 공통 fallback의 제한된 Pass/Shoot 선택. Neural의 Action 출력에는 개입하지 않는다.
        /// </summary>
        public bool TryGetAutonomousKickTarget(
            AgentSoccer requester,
            out Vector3 target,
            out int kickAction)
        {
            target = ball != null ? ball.transform.position : Vector3.zero;
            kickAction = 0;
            if (requester == null || ball == null || m_RewardEngine?.BallCarrier != requester)
            {
                return false;
            }

            var ballPosition = ball.transform.position;
            if (SoccerDefensiveClearanceRules.IsBallInOwnGoalDanger(requester.Team, ballPosition, arenaGeometry))
            {
                target = SoccerDefensiveClearanceRules.GetCentralClearanceTarget(ballPosition.y);
                kickAction = 1;
                return true;
            }

            var attackDepth = SoccerDefensiveClearanceRules.GetAttackingDepth(
                requester.Team,
                ballPosition.x);
            if (attackDepth >= SoccerDefensiveClearanceRules.MinimumShootingDepth)
            {
                var shotTarget = SoccerDefensiveClearanceRules.GetOpponentGoalCenter(
                    requester.Team,
                    ballPosition.y,
                    arenaGeometry);
                if (SoccerDefensiveClearanceRules.PredictsOpponentGoal(
                        requester.Team,
                        ballPosition,
                        shotTarget - ballPosition,
                        arenaGeometry))
                {
                    target = shotTarget;
                    kickAction = 2;
                    return true;
                }
            }

            if (IsUnderPressure(requester, 7f)
                && TryFindAutonomousPassTarget(requester, out var passTarget))
            {
                target = passTarget.transform.position;
                kickAction = 1;
                return true;
            }

            return false;
        }

        public bool TryGetCommonPossessionKick(AgentSoccer requester, out Vector3 target, out int kickAction)
        {
            target=Vector3.zero;kickAction=0;
            if(requester==null || ball==null || BallCarrier!=requester || !IsPlayActive)return false;
            var origin=ball.transform.position;
            var danger=SoccerDefensiveClearanceRules.IsBallInOwnGoalDanger(requester.Team,origin,arenaGeometry);
            if(!danger)return false;
            var sign=SoccerDefensiveClearanceRules.GetAttackSign(requester.Team);
            var best=float.NegativeInfinity;AgentSoccer receiver=null;
            foreach(var item in AgentsList)
            {
                var other=item.Agent;
                if(other==null || other==requester || other.Team!=requester.Team || !other.isActiveAndEnabled)continue;
                var offset=other.transform.position-origin;offset.y=0;
                var distance=offset.magnitude;
                if(distance<5f || distance>28f || (danger && offset.x*sign<=0f))continue;
                var score=offset.x*sign-distance*0.12f;
                if(score<=best)continue;
                best=score;receiver=other;
            }
            if(receiver!=null) {target=receiver.transform.position;kickAction=1;return true;}
            if(danger) {target=SoccerDefensiveClearanceRules.GetCentralClearanceTarget(origin.y);kickAction=2;return true;}
            return false;
        }

        public bool HasTeammateInKickLane(
            AgentSoccer requester,
            Vector3 origin,
            Vector3 direction)
        {
            if (requester == null)
            {
                return false;
            }

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            direction.Normalize();
            const float minimumDistance = 5f;
            const float maximumDistance = 34f;
            const float laneHalfWidth = 4.5f;
            var laneHalfWidthSquared = laneHalfWidth * laneHalfWidth;
            foreach (var item in AgentsList)
            {
                var teammate = item?.Agent;
                if (teammate == null
                    || !teammate.gameObject.activeInHierarchy
                    || teammate == requester
                    || teammate.Team != requester.Team)
                {
                    continue;
                }

                var offset = teammate.transform.position - origin;
                offset.y = 0f;
                var projectedDistance = Vector3.Dot(offset, direction);
                if (projectedDistance < minimumDistance || projectedDistance > maximumDistance)
                {
                    continue;
                }

                var lateralDistanceSquared = Mathf.Max(
                    0f,
                    offset.sqrMagnitude - projectedDistance * projectedDistance);
                if (lateralDistanceSquared <= laneHalfWidthSquared)
                {
                    return true;
                }
            }

            return false;
        }

        bool TryFindAutonomousPassTarget(AgentSoccer requester, out AgentSoccer bestTarget)
        {
            bestTarget = null;
            var bestScore = float.NegativeInfinity;
            var attackSign = requester.Team == Team.Red ? 1f : -1f;
            foreach (var item in AgentsList)
            {
                var teammate = item?.Agent;
                if (teammate == null
                    || !teammate.gameObject.activeInHierarchy
                    || teammate == requester
                    || teammate.Team != requester.Team)
                {
                    continue;
                }

                var offset = teammate.transform.position - requester.transform.position;
                offset.y = 0f;
                var distance = offset.magnitude;
                if (distance < 5f || distance > 28f)
                {
                    continue;
                }

                var progress = offset.x * attackSign;
                if (progress < -2f)
                {
                    continue;
                }

                var score = progress * 1.2f
                    + Mathf.Min(GetNearestOpponentDistance(teammate.transform.position, requester.Team), 10f) * 0.7f
                    - distance * 0.12f;
                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestTarget = teammate;
            }

            return bestTarget != null;
        }

        bool IsUnderPressure(AgentSoccer requester, float range)
        {
            var rangeSquared = range * range;
            foreach (var item in AgentsList)
            {
                var opponent = item?.Agent;
                if (opponent == null || !opponent.gameObject.activeInHierarchy || opponent.Team == requester.Team)
                {
                    continue;
                }

                if ((opponent.transform.position - requester.transform.position).sqrMagnitude <= rangeSquared)
                {
                    return true;
                }
            }

            return false;
        }

        float GetNearestOpponentDistance(Vector3 position, Team requesterTeam)
        {
            var nearestSquared = float.PositiveInfinity;
            foreach (var item in AgentsList)
            {
                var opponent = item?.Agent;
                if (opponent == null || !opponent.gameObject.activeInHierarchy || opponent.Team == requesterTeam)
                {
                    continue;
                }

                nearestSquared = Mathf.Min(
                    nearestSquared,
                    (opponent.transform.position - position).sqrMagnitude);
            }

            return float.IsPositiveInfinity(nearestSquared)
                ? 20f
                : Mathf.Sqrt(nearestSquared);
        }

        AgentSoccer FindClosestTeammateToBall(Team team)
        {
            AgentSoccer closest = null;
            var closestSqrDistance = float.PositiveInfinity;
            foreach (var item in AgentsList)
            {
                var teammate = item?.Agent;
                if (teammate == null || !teammate.gameObject.activeInHierarchy || teammate.Team != team)
                {
                    continue;
                }

                if (!CanChaseBall(teammate))
                {
                    continue;
                }

                var sqrDistance = (teammate.transform.position - ball.transform.position).sqrMagnitude;
                if (sqrDistance >= closestSqrDistance)
                {
                    continue;
                }

                closestSqrDistance = sqrDistance;
                closest = teammate;
            }

            return closest;
        }

        static float GetLaneSign(AgentSoccer agent)
        {
            if (Mathf.Abs(agent.StartingPosition.z) > 0.5f)
            {
                return Mathf.Sign(agent.StartingPosition.z);
            }

            return (agent.Team == Team.Red ? 1f : -1f)
                * (agent.PositionRole == AgentSoccer.Position.Striker ? 1f : -1f);
        }

        Vector3 ClampFieldTarget(Vector3 target)
        {
            if (arenaGeometry != null)
            {
                return arenaGeometry.ClampTarget(target);
            }

            // 활성 Prefab은 항상 ArenaGeometry를 제공한다. 편집 중 임시 누락에도
            // 예전 경기장 수치가 아니라 동일한 Stadium 경계를 사용한다.
            const float playerMargin = 0.55f;
            target.z = Mathf.Clamp(target.z,
                -SoccerArenaGeometry.StadiumHalfWidth + playerMargin,
                SoccerArenaGeometry.StadiumHalfWidth - playerMargin);
            var inGoalLane = Mathf.Abs(target.z) <= SoccerArenaGeometry.StadiumGoalHalfWidth - playerMargin;
            var depth = SoccerArenaGeometry.StadiumHalfLength
                + (inGoalLane ? SoccerArenaGeometry.StadiumGoalDepth : 0f)
                - playerMargin;
            target.x = Mathf.Clamp(target.x, -depth, depth);
            return target;
        }

        public string GetCenterMessage()
        {
            if (State == SoccerMatchState.GoalPause)
            {
                return $"{m_LastScoringTeam.ToString().ToUpperInvariant()} GOAL!\nKick-off in {Mathf.CeilToInt(m_GoalResetRemaining)}";
            }

            if (State != SoccerMatchState.Finished)
            {
                return string.Empty;
            }

            if (RedScore == NavyScore)
            {
                return "FULL TIME\nDRAW";
            }

            return $"FULL TIME\n{SoccerTeamVisuals.DisplayName(RedScore > NavyScore ? Team.Red : Team.Navy)} WINS";
        }
    }
}
