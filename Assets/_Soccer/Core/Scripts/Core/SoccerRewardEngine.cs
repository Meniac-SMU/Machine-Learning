using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using UnityEngine;

namespace MachineLearning.Soccer
{
    public enum SoccerCurriculumRewardStage
    {
        Disabled,
        L0Possession,
        L1Finish,
        L1Carry,
        L2Pass,
        L3ProgressivePlay,
        L2Find,
        L2Score
    }

    /// <summary>
    /// 공 접촉 사건을 소유권·연계·수비 보상으로 변환하는 중앙 보상 장부.
    /// 사건 판정은 Neural과 Rule 공통이며 팀별 세기는 RewardProfile/RewardPolicy에서만 조정한다.
    /// </summary>
    public sealed class SoccerRewardEngine : MonoBehaviour
    {
        // 시간·거리·성공 조건은 공통 보상 사건 계약이다. 특정 전술을 위해 여기서 분기하지 않는다.
        const float MinimumPassDistance = 3f;
        const float MinimumProgressivePassDistance = 6f;
        const float PassControlConfirmationSeconds = 0.35f;
        const float PossessionConfirmationSeconds = 0.75f;
        const float PossessionTransitionTimeoutSeconds = 2f;
        const float PossessionControlDistance = 4f;
        const float PressWindowSeconds = 3f;
        const float CoordinatedPressRange = 8f;
        const float CoordinatedPressMinimumSpacing = 2f;
        const float CombinationWindowSeconds = 8f;
        const float AttackProgressDistance = 12f;
        const float DribbleOpponentRange = 2.5f;
        const float DribbleProgressDistance = 2f;
        const float DribbleBallProgressDistance = 1.5f;
        const float DribbleCandidateLifetimeSeconds = 3f;
        const float ControlledCarryProgressDistance = 2f;
        const float ControlledCarryMinimumSeconds = 0.4f;
        const float ControlledCarryCandidateLifetimeSeconds = 4f;
        const float ControlledBallDistance = 2.4f;
        const float MinimumStableProgressControlSeconds = 0.35f;
        const float FormationSampleSeconds = 0.75f;
        const float MinimumFormationImprovement = 0.08f;

        SoccerEnvController m_Environment;
        SoccerMatchSetup m_MatchSetup;
        AgentSoccer m_LastTouchAgent;
        Vector3 m_LastTouchPosition;
        float m_LastTouchTime;
        AgentSoccer m_PendingPossessionAgent;
        float m_PendingPossessionSince;
        AgentSoccer m_BallCarrier;
        float m_BallCarrierControlSince;
        Team? m_PossessionTeam;
        float m_PossessionStartBallX;
        bool m_AttackSuccessAwarded;
        bool m_PossessionTransitionPending;
        readonly float[] m_LastLossTimes = { float.NegativeInfinity, float.NegativeInfinity };
        readonly float[] m_PassRewardTotals = new float[2];
        readonly float[] m_PassIndividualRewardTotals = new float[2];
        readonly float[] m_ControlledCarryRewardTotals = new float[2];
        readonly float[] m_ProgressivePassRewardTotals = new float[2];
        readonly float[] m_CombinationRewardTotals = new float[2];
        readonly float[] m_FormationRewardTotals = new float[2];
        readonly float[] m_ShapingRewardTotals = new float[2];
        readonly float[] m_MatchShapingRewardTotals = new float[2];
        readonly float[] m_CrowdingPenaltyTotals = new float[2];
        readonly float[] m_WastefulStrongKickPenaltyTotals = new float[2];
        readonly float[] m_BehaviorPenaltyMatchTotals = new float[2];
        readonly float[] m_CumulativeTeamRewards = new float[2];
        readonly float[] m_LastFormationScores = new float[2];
        readonly bool[] m_HasFormationScore = new bool[2];
        readonly List<AgentSoccer>[] m_TeamAgentBuffers =
        {
            new List<AgentSoccer>(4),
            new List<AgentSoccer>(4)
        };
        readonly List<AgentSoccer> m_CombinationSequence = new();
        readonly HashSet<AgentSoccer> m_DribbleRewardedAgents = new();
        readonly HashSet<AgentSoccer> m_ControlledCarryRewardedAgents = new();
        float m_CombinationStartTime;
        float m_NextFormationSampleTime;
        DribbleCandidate m_DribbleCandidate;
        ControlledCarryCandidate m_ControlledCarryCandidate;
        PassCandidate m_PendingPass;
        bool m_SuppressTacticalTeamShape;
        bool m_CurriculumRewardMode;
        SoccerCurriculumRewardStage m_CurriculumRewardStage;

        public Team? PossessionTeam => m_PossessionTransitionPending ? null : m_PossessionTeam;
        public Team? LastTouchTeam => m_LastTouchAgent != null ? m_LastTouchAgent.Team : null;
        public AgentSoccer BallCarrier => m_BallCarrier;
        public bool SuppressesTacticalTeamShape => m_SuppressTacticalTeamShape;
        public bool UsesCurriculumRewards => m_CurriculumRewardMode;
        public SoccerCurriculumRewardStage CurriculumRewardStage => m_CurriculumRewardStage;

        public float GetCumulativeReward(Team team)
        {
            return m_CumulativeTeamRewards[(int)team];
        }

        /// <summary>
        /// 커리큘럼의 짧은 과제를 완료했을 때만 사용하는 명시적 보상 경로.
        /// Base/팀별 RewardProfile 수치는 바꾸지 않으며 공통 상한, 팀별 장부와 TensorBoard 태그는 유지한다.
        /// </summary>
        public float AwardCurriculumLessonSuccess(
            Team team,
            AgentSoccer actor,
            float groupReward,
            float individualReward)
        {
            var appliedGroup = ApplyGroupReward(
                team,
                SoccerRewardKind.CurriculumLessonSuccess,
                Mathf.Max(0f, groupReward));
            var appliedIndividual = ApplyExplicitIndividualReward(
                actor,
                SoccerRewardKind.CurriculumLessonSuccess,
                Mathf.Max(0f, individualReward));
            return appliedGroup + appliedIndividual;
        }

        /// <summary>
        /// 커리큘럼 전용의 제한된 개별 shaping 보상을 공통 장부와 TensorBoard 태그로 지급한다.
        /// 호출 측 커리큘럼은 에피소드별 상한을 별도로 적용해야 한다.
        /// </summary>
        public float AwardCurriculumIndividualReward(
            AgentSoccer actor,
            SoccerRewardKind kind,
            float reward)
        {
            if (kind is not SoccerRewardKind.CurriculumApproachProgress
                and not SoccerRewardKind.CurriculumPossessionEstablished
                and not SoccerRewardKind.CurriculumSupportShape
                and not SoccerRewardKind.CurriculumDribbleProgress
                and not SoccerRewardKind.CurriculumShotAttempt
                and not SoccerRewardKind.CurriculumShotOnTarget
                and not SoccerRewardKind.CurriculumShotAlignment
                and not SoccerRewardKind.CurriculumGoal
                and not SoccerRewardKind.CurriculumPassAttempt
                and not SoccerRewardKind.CurriculumPassReception
                and not SoccerRewardKind.CurriculumPassDelivered
                and not SoccerRewardKind.CurriculumPassDirection
                and not SoccerRewardKind.CurriculumReceiverProgress
                and not SoccerRewardKind.CurriculumStableReceiver
                and not SoccerRewardKind.CurriculumFindProgress
                and not SoccerRewardKind.CurriculumFindHeading
                and not SoccerRewardKind.CurriculumFastFind
                and not SoccerRewardKind.CurriculumScoreProgress)
            {
                Debug.LogError($"{kind} is not a curriculum shaping reward.", this);
                return 0f;
            }

            return ApplyExplicitIndividualReward(actor, kind, Mathf.Max(0f, reward));
        }

        /// <summary>
        /// Signed L2-Find potential for one explicitly selected actor. This is kept
        /// separate so no other curriculum individual reward can become negative.
        /// </summary>
        public float AwardCurriculumSignedIndividualReward(
            AgentSoccer actor,
            SoccerRewardKind kind,
            float reward)
        {
            if (kind is not SoccerRewardKind.CurriculumFindProgress
                and not SoccerRewardKind.CurriculumFindHeading)
            {
                Debug.LogError($"{kind} is not a signed L2-Find shaping reward.", this);
                return 0f;
            }

            return ApplyExplicitSignedIndividualReward(actor, kind, reward);
        }

        /// <summary>
        /// Curriculum-only group shaping and terminal penalties. The controller owns
        /// per-episode caps and passes a signed value; the stage allowlist remains final.
        /// </summary>
        public float AwardCurriculumGroupReward(Team team, SoccerRewardKind kind, float reward)
        {
            if (kind is not SoccerRewardKind.CurriculumFindProgress
                and not SoccerRewardKind.CurriculumScoreProgress
                and not SoccerRewardKind.CurriculumLongPassGoalBonus
                and not SoccerRewardKind.CurriculumOwnGoalPenalty
                and not SoccerRewardKind.CurriculumFindTimeoutPenalty
                and not SoccerRewardKind.CurriculumFindMissPenalty)
            {
                Debug.LogError($"{kind} is not a curriculum group reward.", this);
                return 0f;
            }

            return ApplyGroupReward(team, kind, reward);
        }

        // Caller owns the once-per-round event. Isolated from ordinary match penalties.
        public float AwardCurriculumPassOpportunityLoss(AgentSoccer actor)
        {
            if (m_CurriculumRewardStage != SoccerCurriculumRewardStage.L2Pass
                || actor == null || GetProfile(actor.Team) == null) return 0f;
            const float penalty = -0.15f;
            actor.AddTrainingReward(penalty);
            m_CumulativeTeamRewards[(int)actor.Team] += penalty;
            Record(actor.Team, SoccerRewardKind.CurriculumPassOpportunityLost, penalty);
            return penalty;
        }

        /// <summary>
        /// 기존 bool 호출은 L0 허용 목록에 연결한다. L1 이상은 명시적 Stage를 사용한다.
        /// 소유권 사건 추적은 유지하되 실제 지급의 모든 경로에서 레슨별 허용 목록을 적용한다.
        /// </summary>
        public void ConfigureCurriculumRewardMode(bool suppressTacticalTeamShape)
        {
            ConfigureCurriculumRewardStage(suppressTacticalTeamShape
                ? SoccerCurriculumRewardStage.L0Possession
                : SoccerCurriculumRewardStage.Disabled);
        }

        public void ConfigureCurriculumRewardStage(SoccerCurriculumRewardStage stage)
        {
            m_CurriculumRewardStage = stage;
            m_CurriculumRewardMode = stage != SoccerCurriculumRewardStage.Disabled;
            m_SuppressTacticalTeamShape = m_CurriculumRewardMode;
            Array.Clear(m_HasFormationScore, 0, m_HasFormationScore.Length);
        }

        // Final delivery allowlist: tactical detectors still maintain possession state,
        // but neither positive rewards nor penalties from later lessons can leak in.
        public static bool IsRewardAllowed(SoccerCurriculumRewardStage stage, SoccerRewardKind kind)
        {
            if (stage == SoccerCurriculumRewardStage.Disabled)
                return true;
            if (kind is SoccerRewardKind.CurriculumPossessionEstablished
                or SoccerRewardKind.CurriculumLessonSuccess)
                return true;
            if (stage == SoccerCurriculumRewardStage.L0Possession)
                return kind == SoccerRewardKind.CurriculumApproachProgress;
            if (stage == SoccerCurriculumRewardStage.L2Find)
                return kind is SoccerRewardKind.CurriculumFindProgress
                    or SoccerRewardKind.CurriculumFindHeading
                    or SoccerRewardKind.CurriculumFastFind
                    or SoccerRewardKind.CurriculumFindTimeoutPenalty
                    or SoccerRewardKind.CurriculumFindMissPenalty;
            if (stage == SoccerCurriculumRewardStage.L2Score)
                return kind is SoccerRewardKind.CurriculumScoreProgress
                    or SoccerRewardKind.CurriculumShotAttempt
                    or SoccerRewardKind.CurriculumShotOnTarget
                    or SoccerRewardKind.CurriculumGoal
                    or SoccerRewardKind.CurriculumLongPassGoalBonus
                    or SoccerRewardKind.CurriculumOwnGoalPenalty;
            if (kind == SoccerRewardKind.CurriculumSupportShape)
                return true;
            if (kind == SoccerRewardKind.CurriculumShotAlignment)
                return stage == SoccerCurriculumRewardStage.L1Finish;
            if (stage is SoccerCurriculumRewardStage.L2Pass or SoccerCurriculumRewardStage.L3ProgressivePlay)
                return kind is SoccerRewardKind.CurriculumPassAttempt
                    or SoccerRewardKind.CurriculumPassReception
                    or SoccerRewardKind.CurriculumPassDelivered
                    or SoccerRewardKind.CurriculumPassDirection
                    or SoccerRewardKind.CurriculumPassOpportunityLost
                    || (stage == SoccerCurriculumRewardStage.L3ProgressivePlay
                        && kind is SoccerRewardKind.CurriculumReceiverProgress
                            or SoccerRewardKind.CurriculumStableReceiver);
            if (stage is SoccerCurriculumRewardStage.L1Finish or SoccerCurriculumRewardStage.L1Carry)
                return kind is SoccerRewardKind.CurriculumShotAttempt
                    or SoccerRewardKind.CurriculumShotOnTarget
                    or SoccerRewardKind.CurriculumGoal
                    || (stage == SoccerCurriculumRewardStage.L1Carry
                        && kind == SoccerRewardKind.CurriculumDribbleProgress);
            return false;
        }

        public static string GetTeamRewardEventStatKey(Team team, SoccerRewardKind kind)
        {
            return $"Soccer/{GetTeamTelemetryName(team)}/Reward/{kind}";
        }

        public static string GetTeamRewardSummaryTotalStatKey(Team team)
        {
            return $"Soccer/{GetTeamTelemetryName(team)}/Reward/Summary Total";
        }

        public static string GetTeamMatchRewardStatKey(Team team)
        {
            return $"Soccer/{GetTeamTelemetryName(team)}/Match Reward";
        }

        public void RecordMatchRewardSummary()
        {
            if (!Academy.IsInitialized)
            {
                return;
            }

            var statsRecorder = Academy.Instance.StatsRecorder;
            statsRecorder.Add(
                GetTeamMatchRewardStatKey(Team.Red),
                GetCumulativeReward(Team.Red),
                StatAggregationMethod.Average);
            statsRecorder.Add(
                GetTeamMatchRewardStatKey(Team.Navy),
                GetCumulativeReward(Team.Navy),
                StatAggregationMethod.Average);
        }

        public void Configure(SoccerEnvController environment, SoccerMatchSetup matchSetup)
        {
            m_Environment = environment;
            m_MatchSetup = matchSetup;
        }

        void Awake()
        {
            m_Environment ??= GetComponent<SoccerEnvController>();
            m_MatchSetup ??= GetComponent<SoccerMatchSetup>();
        }

        void FixedUpdate()
        {
            if (m_Environment == null || !m_Environment.IsPlayActive)
            {
                return;
            }

            ConfirmPossessionIfStable();
            ConfirmPassIfStable();
            RefreshBallCarrierControl();
            var sampleTeamShape = Time.time >= m_NextFormationSampleTime;
            if (sampleTeamShape)
            {
                m_NextFormationSampleTime = Time.time + FormationSampleSeconds;
                RefreshTeamAgentBuffers();
                if (!m_SuppressTacticalTeamShape)
                {
                    EvaluateTeammateCrowding();
                }
            }

            if (m_PossessionTransitionPending || !m_PossessionTeam.HasValue)
            {
                return;
            }

            EvaluateAttackSuccess();
            EvaluateControlledCarrySuccess();
            EvaluateDribbleSuccess();
            if (sampleTeamShape && !m_SuppressTacticalTeamShape)
            {
                EvaluateFormationChange();
            }
        }

        public void NotifyBallTouch(AgentSoccer agent)
        {
            if (agent == null || m_Environment == null || !m_Environment.IsPlayActive)
            {
                return;
            }

            var ballPosition = m_Environment.Ball.transform.position;
            if (m_LastTouchAgent != null && m_LastTouchAgent != agent)
            {
                m_PendingPass = default;
            }

            if (m_LastTouchAgent != null && m_LastTouchAgent != agent && m_LastTouchAgent.Team == agent.Team)
            {
                var passDistance = Vector3.Distance(m_LastTouchPosition, ballPosition);
                if (!m_PossessionTransitionPending
                    && m_PossessionTeam == agent.Team
                    && passDistance >= MinimumPassDistance
                    && Time.time - m_LastTouchTime >= 0.1f)
                {
                    // 수신자의 제어 확인 전까지 보상 판정 보류.
                    m_PendingPass = new PassCandidate
                    {
                        Team = agent.Team,
                        Passer = m_LastTouchAgent,
                        Receiver = agent,
                        StartPosition = m_LastTouchPosition,
                        ReceivePosition = ballPosition,
                        ReceivedTime = Time.time,
                        Valid = true
                    };
                }
            }

            if (m_LastTouchAgent == null || m_LastTouchAgent.Team != agent.Team)
            {
                m_PendingPossessionAgent = agent;
                m_PendingPossessionSince = Time.time;
                m_PossessionTransitionPending = true;
                m_BallCarrier = null;
                m_DribbleCandidate = default;
                m_PendingPass = default;
            }
            else if (m_PendingPossessionAgent != null)
            {
                // 확인 시간은 유지하고 마지막 제어 선수만 갱신.
                m_PendingPossessionAgent = agent;
            }
            else if (m_PossessionTeam == agent.Team)
            {
                // 팀 소유권을 유지한 패스의 수신자 갱신.
                if (m_BallCarrier != agent)
                {
                    m_BallCarrierControlSince = Time.time;
                }

                m_BallCarrier = agent;
            }

            if (m_LastTouchAgent != agent)
            {
                if (!m_PossessionTransitionPending && m_PossessionTeam == agent.Team)
                {
                    BeginControlledCarryCandidate(agent);
                    BeginDribbleCandidate(agent);
                }
            }
            else if (!m_DribbleCandidate.Valid
                && !m_PossessionTransitionPending
                && m_PossessionTeam == agent.Team)
            {
                BeginControlledCarryCandidate(agent);
                BeginDribbleCandidate(agent);
            }

            m_LastTouchAgent = agent;
            m_LastTouchPosition = ballPosition;
            m_LastTouchTime = Time.time;
        }

        public void NotifyBallStrike(
            AgentSoccer agent,
            int kickAction,
            Vector3 kickDirection,
            bool safetyRedirected)
        {
            if (agent == null || m_Environment == null || !m_Environment.IsPlayActive || kickAction == 0)
            {
                return;
            }

            // 명시적 Kick 뒤의 공 비행을 일반 운반 checkpoint로 오인하지 않는다.
            m_ControlledCarryCandidate = default;
            if (safetyRedirected)
            {
                AwardBehaviorPenalty(agent, SoccerRewardKind.UnsafeOwnGoalKick, false);
                return;
            }

            if (kickAction != 2)
            {
                return;
            }

            var ballPosition = m_Environment.Ball.transform.position;
            var hasTeammateInLane = m_Environment.HasTeammateInKickLane(
                agent,
                ballPosition,
                kickDirection);
            if (!SoccerDefensiveClearanceRules.IsMeaningfulStrongKick(
                    agent.Team,
                    ballPosition,
                    kickDirection,
                    hasTeammateInLane,
                    m_Environment.ArenaGeometry))
            {
                AwardBehaviorPenalty(agent, SoccerRewardKind.WastefulStrongKick, true);
            }
        }

        public void AwardGoal(Team scoredTeam)
        {
            if (m_CurriculumRewardMode)
            {
                return;
            }

            AwardGroup(scoredTeam, SoccerRewardKind.GoalResult, 1f, null);
            AwardGroup(Opponent(scoredTeam), SoccerRewardKind.GoalResult, -1f, null);
        }

        public void AwardMatchResult(Team winningTeam)
        {
            AwardGroup(winningTeam, SoccerRewardKind.MatchResult, 1f, null);
            AwardGroup(Opponent(winningTeam), SoccerRewardKind.MatchResult, -1f, null);
        }

        public void ResetPossession()
        {
            m_LastTouchAgent = null;
            m_LastTouchPosition = Vector3.zero;
            m_LastTouchTime = 0f;
            m_PendingPossessionAgent = null;
            m_BallCarrier = null;
            m_BallCarrierControlSince = 0f;
            m_PossessionTeam = null;
            m_AttackSuccessAwarded = false;
            m_PossessionTransitionPending = false;
            m_DribbleCandidate = default;
            m_ControlledCarryCandidate = default;
            m_PendingPass = default;
            ResetPossessionTracking();
            for (var index = 0; index < m_LastLossTimes.Length; index++)
            {
                m_LastLossTimes[index] = float.NegativeInfinity;
            }
        }

        public void ResetMatch()
        {
            // 경기 단위 보조 보상 상한 초기화.
            Array.Clear(m_MatchShapingRewardTotals, 0, m_MatchShapingRewardTotals.Length);
            Array.Clear(m_CrowdingPenaltyTotals, 0, m_CrowdingPenaltyTotals.Length);
            Array.Clear(m_BehaviorPenaltyMatchTotals, 0, m_BehaviorPenaltyMatchTotals.Length);
            Array.Clear(m_CumulativeTeamRewards, 0, m_CumulativeTeamRewards.Length);
        }

        void ConfirmPassIfStable()
        {
            if (!m_PendingPass.Valid
                || m_PossessionTransitionPending
                || m_PossessionTeam != m_PendingPass.Team
                || m_LastTouchAgent != m_PendingPass.Receiver
                || Time.time - m_PendingPass.ReceivedTime < PassControlConfirmationSeconds)
            {
                return;
            }

            var receiver = m_PendingPass.Receiver;
            if (receiver == null
                || Vector3.Distance(receiver.transform.position, m_Environment.Ball.transform.position)
                    > PossessionControlDistance)
            {
                m_PendingPass = default;
                return;
            }

            AwardPass(
                m_PendingPass.Team,
                m_PendingPass.Passer,
                receiver,
                m_PendingPass.StartPosition,
                m_PendingPass.ReceivePosition);
            m_PendingPass = default;
        }

        void ConfirmPossessionIfStable()
        {
            if (m_PendingPossessionAgent == null)
            {
                return;
            }

            var pendingSeconds = Time.time - m_PendingPossessionSince;
            if (m_LastTouchAgent == null
                || m_LastTouchAgent.Team != m_PendingPossessionAgent.Team
                || pendingSeconds < PossessionConfirmationSeconds)
            {
                if (pendingSeconds >= PossessionTransitionTimeoutSeconds)
                {
                    CancelStalledPossessionTransition();
                }

                return;
            }

            // 공과 가까운 선수만 안정적인 소유자로 인정.
            if (Vector3.Distance(m_PendingPossessionAgent.transform.position, m_Environment.Ball.transform.position)
                > PossessionControlDistance)
            {
                if (pendingSeconds >= PossessionTransitionTimeoutSeconds)
                {
                    CancelStalledPossessionTransition();
                }

                return;
            }

            var newTeam = m_PendingPossessionAgent.Team;
            var previousTeam = m_PossessionTeam;
            var confirmedCarrier = m_PendingPossessionAgent;

            m_PossessionTeam = newTeam;
            m_BallCarrier = confirmedCarrier;
            m_BallCarrierControlSince = Time.time;
            m_DribbleCandidate = default;
            m_ControlledCarryCandidate = default;
            m_PossessionTransitionPending = false;
            m_PendingPossessionAgent = null;

            // 상대의 짧은 접촉 뒤 같은 팀이 재확보한 경우 기존 소유권 상한 유지.
            if (previousTeam == newTeam)
            {
                BeginControlledCarryCandidate(confirmedCarrier);
                BeginDribbleCandidate(confirmedCarrier);
                return;
            }

            m_PossessionStartBallX = m_Environment.Ball.transform.position.x;
            m_AttackSuccessAwarded = false;
            ResetPossessionTracking();
            m_CombinationSequence.Add(confirmedCarrier);
            m_CombinationStartTime = Time.time;
            BeginControlledCarryCandidate(confirmedCarrier);
            BeginDribbleCandidate(confirmedCarrier);

            if (previousTeam.HasValue && previousTeam.Value != newTeam)
            {
                var pressRecovery = Time.time - m_LastLossTimes[(int)newTeam] <= PressWindowSeconds
                    && !IsInDefensiveThird(newTeam, m_Environment.Ball.transform.position.x);
                m_LastLossTimes[(int)previousTeam.Value] = Time.time;
                AwardGroup(
                    newTeam,
                    pressRecovery ? SoccerRewardKind.PressSuccess : SoccerRewardKind.DefenseSuccess,
                    1f,
                    confirmedCarrier);

                if (pressRecovery && IsCoordinatedPress(newTeam, confirmedCarrier.transform.position))
                {
                    AwardGroup(newTeam, SoccerRewardKind.CoordinatedPress, 1f, confirmedCarrier);
                }
            }
        }

        void CancelStalledPossessionTransition()
        {
            // 장시간 제어되지 않은 공의 중립 소유권 처리.
            m_PendingPossessionAgent = null;
            m_PossessionTransitionPending = false;
            m_PossessionTeam = null;
            m_BallCarrier = null;
            m_BallCarrierControlSince = 0f;
            m_LastTouchAgent = null;
            m_LastTouchPosition = Vector3.zero;
            m_LastTouchTime = 0f;
            m_DribbleCandidate = default;
            m_ControlledCarryCandidate = default;
            m_PendingPass = default;
        }

        void RefreshBallCarrierControl()
        {
            if (m_BallCarrier == null || m_Environment?.Ball == null)
            {
                return;
            }

            var controlDistance = Vector3.Distance(
                m_BallCarrier.transform.position,
                m_Environment.Ball.transform.position);
            if (controlDistance <= ControlledBallDistance)
            {
                return;
            }

            // 2.4m 밖에 있던 시간은 안정 제어·운반 시간에 포함하지 않는다.
            m_BallCarrierControlSince = Time.time;
            m_ControlledCarryCandidate = default;
            m_DribbleCandidate = default;
            if (controlDistance <= PossessionControlDistance)
            {
                return;
            }

            // 패스가 비행하는 동안 팀 소유권과 cap은 유지하되 이전 선수를 carrier로 남기지 않는다.
            m_BallCarrier = null;
            m_BallCarrierControlSince = 0f;
        }

        void EvaluateAttackSuccess()
        {
            if (!m_PossessionTeam.HasValue
                || m_AttackSuccessAwarded
                || m_BallCarrier == null
                || m_LastTouchAgent != m_BallCarrier
                || Time.time - m_BallCarrierControlSince < MinimumStableProgressControlSeconds
                || Vector3.Distance(m_BallCarrier.transform.position, m_Environment.Ball.transform.position)
                    > ControlledBallDistance)
            {
                return;
            }

            var team = m_PossessionTeam.Value;
            var attackSign = team == Team.Red ? 1f : -1f;
            var progress = (m_Environment.Ball.transform.position.x - m_PossessionStartBallX) * attackSign;
            if (progress < AttackProgressDistance)
            {
                return;
            }

            m_AttackSuccessAwarded = true;
            AwardGroup(team, SoccerRewardKind.AttackSuccess, 1f, m_BallCarrier);
        }

        void AwardPass(
            Team team,
            AgentSoccer passer,
            AgentSoccer receiver,
            Vector3 passStart,
            Vector3 receivePosition)
        {
            var profile = GetProfile(team);
            if (profile == null)
            {
                return;
            }

            AwardCappedGroup(
                team,
                SoccerRewardKind.PassSuccess,
                receiver,
                ref m_PassRewardTotals[(int)team],
                profile.passRewardLimitPerPossession);

            var individualRemaining = profile.passIndividualRewardLimitPerPossession
                - m_PassIndividualRewardTotals[(int)team];
            if (individualRemaining > 0f)
            {
                m_PassIndividualRewardTotals[(int)team] += AwardIndividual(
                    passer,
                    SoccerRewardKind.PassIndividual,
                    individualRemaining);
            }

            var attackSign = team == Team.Red ? 1f : -1f;
            var forwardProgress = (receivePosition.x - passStart.x) * attackSign;
            if (forwardProgress >= MinimumProgressivePassDistance)
            {
                AwardCappedGroup(
                    team,
                    SoccerRewardKind.ProgressivePass,
                    receiver,
                    ref m_ProgressivePassRewardTotals[(int)team],
                    profile.progressivePassLimitPerPossession);
            }

            UpdateCombination(team, passer, receiver, profile);
        }

        void UpdateCombination(
            Team team,
            AgentSoccer passer,
            AgentSoccer receiver,
            SoccerRewardProfile profile)
        {
            if (Time.time - m_CombinationStartTime > CombinationWindowSeconds
                || m_CombinationSequence.Count == 0
                || m_CombinationSequence[m_CombinationSequence.Count - 1] != passer)
            {
                m_CombinationSequence.Clear();
                m_CombinationSequence.Add(passer);
                m_CombinationStartTime = Time.time;
            }

            if (m_CombinationSequence[m_CombinationSequence.Count - 1] != receiver)
            {
                m_CombinationSequence.Add(receiver);
            }

            if (m_CombinationSequence.Count < 3)
            {
                return;
            }

            var lastIndex = m_CombinationSequence.Count - 1;
            var first = m_CombinationSequence[lastIndex - 2];
            var second = m_CombinationSequence[lastIndex - 1];
            var third = m_CombinationSequence[lastIndex];
            if (first == second || first == third || second == third)
            {
                if (m_CombinationSequence.Count > 2)
                {
                    m_CombinationSequence.RemoveRange(0, m_CombinationSequence.Count - 2);
                }

                return;
            }

            AwardCappedGroup(
                team,
                SoccerRewardKind.ThreePlayerCombination,
                receiver,
                ref m_CombinationRewardTotals[(int)team],
                profile.combinationRewardLimitPerPossession);
            m_CombinationSequence.Clear();
            m_CombinationSequence.Add(receiver);
            m_CombinationStartTime = Time.time;
        }

        void BeginControlledCarryCandidate(AgentSoccer agent)
        {
            if (agent == null
                || m_ControlledCarryRewardedAgents.Contains(agent)
                || m_Environment?.Ball == null)
            {
                return;
            }

            m_ControlledCarryCandidate = new ControlledCarryCandidate
            {
                Agent = agent,
                StartingPosition = agent.transform.position,
                StartingBallPosition = m_Environment.Ball.transform.position,
                StartingTime = Time.time,
                Valid = true
            };
        }

        void EvaluateControlledCarrySuccess()
        {
            if (!m_ControlledCarryCandidate.Valid)
            {
                return;
            }

            var candidateAge = Time.time - m_ControlledCarryCandidate.StartingTime;
            if (candidateAge > ControlledCarryCandidateLifetimeSeconds)
            {
                var carrier = m_ControlledCarryCandidate.Agent;
                m_ControlledCarryCandidate = default;
                if (carrier != null && carrier == m_BallCarrier && m_LastTouchAgent == carrier)
                {
                    BeginControlledCarryCandidate(carrier);
                }

                return;
            }

            var agent = m_ControlledCarryCandidate.Agent;
            if (agent == null
                || candidateAge < ControlledCarryMinimumSeconds
                || m_BallCarrier != agent
                || m_LastTouchAgent != agent
                || m_PossessionTeam != agent.Team)
            {
                return;
            }

            var attackSign = agent.Team == Team.Red ? 1f : -1f;
            var playerProgress = (agent.transform.position.x - m_ControlledCarryCandidate.StartingPosition.x)
                * attackSign;
            var ballProgress = (m_Environment.Ball.transform.position.x
                - m_ControlledCarryCandidate.StartingBallPosition.x) * attackSign;
            if (playerProgress < ControlledCarryProgressDistance
                || ballProgress < ControlledCarryProgressDistance
                || Vector3.Distance(agent.transform.position, m_Environment.Ball.transform.position)
                    > ControlledBallDistance)
            {
                return;
            }

            var profile = GetProfile(agent.Team);
            if (profile == null)
            {
                m_ControlledCarryCandidate = default;
                return;
            }

            var teamIndex = (int)agent.Team;
            var itemRemaining = profile.controlledCarryRewardLimitPerPossession
                - m_ControlledCarryRewardTotals[teamIndex];
            if (itemRemaining > 0f)
            {
                var groupNominal = Mathf.Max(
                    0f,
                    GetGroupRewardValue(agent.Team, SoccerRewardKind.ControlledCarry, agent));
                var individualNominal = Mathf.Max(
                    0f,
                    GetIndividualRewardValue(agent, SoccerRewardKind.ControlledCarry));
                var nominalTotal = groupNominal + individualNominal;
                var scale = nominalTotal > 0f ? Mathf.Min(1f, itemRemaining / nominalTotal) : 0f;
                var appliedGroup = ApplyGroupReward(
                    agent.Team,
                    SoccerRewardKind.ControlledCarry,
                    groupNominal * scale);
                var appliedIndividual = AwardIndividual(
                    agent,
                    SoccerRewardKind.ControlledCarry,
                    Mathf.Max(0f, itemRemaining - appliedGroup));
                m_ControlledCarryRewardTotals[teamIndex] += appliedGroup + appliedIndividual;
            }

            m_ControlledCarryRewardedAgents.Add(agent);
            m_ControlledCarryCandidate = default;
        }

        void BeginDribbleCandidate(AgentSoccer agent)
        {
            if (m_DribbleRewardedAgents.Contains(agent))
            {
                return;
            }

            var nearestOpponent = m_Environment.AgentsList
                .Select(item => item?.Agent)
                .Where(candidate => candidate != null
                    && candidate.gameObject.activeInHierarchy
                    && candidate.Team != agent.Team)
                .OrderBy(candidate => (candidate.transform.position - agent.transform.position).sqrMagnitude)
                .FirstOrDefault();
            if (nearestOpponent == null
                || Vector3.Distance(nearestOpponent.transform.position, agent.transform.position) > DribbleOpponentRange)
            {
                m_DribbleCandidate = default;
                return;
            }

            m_DribbleCandidate = new DribbleCandidate
            {
                Agent = agent,
                Opponent = nearestOpponent,
                StartingPosition = agent.transform.position,
                StartingBallPosition = m_Environment.Ball.transform.position,
                StartingTime = Time.time,
                Valid = true
            };
        }

        void EvaluateDribbleSuccess()
        {
            if (m_DribbleCandidate.Valid
                && Time.time - m_DribbleCandidate.StartingTime > DribbleCandidateLifetimeSeconds)
            {
                m_DribbleCandidate = default;
                return;
            }

            if (!m_DribbleCandidate.Valid
                || m_LastTouchAgent != m_DribbleCandidate.Agent
                || m_PossessionTeam != m_DribbleCandidate.Agent.Team
                || Time.time - m_DribbleCandidate.StartingTime < 0.5f)
            {
                return;
            }

            var agent = m_DribbleCandidate.Agent;
            var attackSign = agent.Team == Team.Red ? 1f : -1f;
            var progress = (agent.transform.position.x - m_DribbleCandidate.StartingPosition.x) * attackSign;
            var ballProgress = (m_Environment.Ball.transform.position.x - m_DribbleCandidate.StartingBallPosition.x)
                * attackSign;
            var controlsBall = Vector3.Distance(agent.transform.position, m_Environment.Ball.transform.position)
                <= PossessionControlDistance;
            var aheadOfOpponent = (agent.transform.position.x - m_DribbleCandidate.Opponent.transform.position.x) * attackSign > 0f;
            if (progress < DribbleProgressDistance
                || ballProgress < DribbleBallProgressDistance
                || !controlsBall
                || !aheadOfOpponent)
            {
                return;
            }

            AwardGroup(agent.Team, SoccerRewardKind.DribbleSuccess, 1f, agent);
            AwardIndividual(agent, SoccerRewardKind.DribbleSuccess);
            m_DribbleRewardedAgents.Add(agent);
            m_DribbleCandidate = default;
        }

        void EvaluateFormationChange()
        {
            for (var teamIndex = 0; teamIndex < m_TeamAgentBuffers.Length; teamIndex++)
            {
                var team = (Team)teamIndex;
                var agents = m_TeamAgentBuffers[teamIndex];
                var currentScore = SoccerFormationEvaluator.Evaluate(
                    team,
                    agents,
                    m_BallCarrier,
                    m_Environment.Ball.transform.position,
                    m_PossessionTeam == team);
                if (!m_HasFormationScore[(int)team])
                {
                    m_LastFormationScores[(int)team] = currentScore;
                    m_HasFormationScore[(int)team] = true;
                    continue;
                }

                var profile = GetProfile(team);
                if (profile == null)
                {
                    continue;
                }

                var delta = currentScore - m_LastFormationScores[(int)team];
                m_LastFormationScores[(int)team] = currentScore;
                if (delta < MinimumFormationImprovement)
                {
                    continue;
                }

                var remaining = profile.formationRewardLimitPerPossession - m_FormationRewardTotals[(int)team];
                var context = CreateContext(team, null);
                var scale = GetPolicy(team)?.EvaluateGroupReward(SoccerRewardKind.FormationChange, context)
                    ?? profile.formationChangeScale;
                var reward = Mathf.Min(delta * scale, remaining);
                var appliedReward = ApplyGroupReward(team, SoccerRewardKind.FormationChange, reward);
                if (appliedReward <= 0f)
                {
                    continue;
                }

                m_FormationRewardTotals[(int)team] += appliedReward;
            }
        }

        void RefreshTeamAgentBuffers()
        {
            foreach (var buffer in m_TeamAgentBuffers)
            {
                buffer.Clear();
            }

            foreach (var item in m_Environment.AgentsList)
            {
                var agent = item?.Agent;
                if (agent != null && agent.gameObject.activeInHierarchy)
                {
                    m_TeamAgentBuffers[(int)agent.Team].Add(agent);
                }
            }
        }

        void EvaluateTeammateCrowding()
        {
            for (var teamIndex = 0; teamIndex < m_TeamAgentBuffers.Length; teamIndex++)
            {
                var team = (Team)teamIndex;
                var profile = GetProfile(team);
                if (profile == null)
                {
                    continue;
                }

                var remaining = profile.teammateCrowdingPenaltyLimitPerMatch
                    - m_CrowdingPenaltyTotals[teamIndex];
                if (remaining <= 0f)
                {
                    continue;
                }

                var severity = SoccerFormationEvaluator.EvaluateTeammateCrowding(
                    m_TeamAgentBuffers[teamIndex]);
                if (severity <= 0f)
                {
                    continue;
                }

                // 가장 가까운 한 쌍의 심각도만 사용해 팀당 샘플 최대치를 고정한다.
                var nominalPenalty = Mathf.Max(
                    0f,
                    GetGroupRewardValue(team, SoccerRewardKind.TeammateCrowding, null));
                var penalty = Mathf.Min(nominalPenalty * severity, remaining);
                var appliedPenalty = ApplyGroupReward(team, SoccerRewardKind.TeammateCrowding, -penalty);
                if (appliedPenalty < 0f)
                {
                    m_CrowdingPenaltyTotals[teamIndex] += -appliedPenalty;
                }
            }
        }

        void AwardGroup(Team team, SoccerRewardKind kind, float sign, AgentSoccer actor)
        {
            var profile = GetProfile(team);
            if (profile == null)
            {
                return;
            }

            var reward = GetGroupRewardValue(team, kind, actor) * sign;
            ApplyGroupReward(team, kind, reward);
        }

        void AwardCappedGroup(
            Team team,
            SoccerRewardKind kind,
            AgentSoccer actor,
            ref float awardedTotal,
            float limit)
        {
            var remaining = limit - awardedTotal;
            if (remaining <= 0f)
            {
                return;
            }

            var reward = Mathf.Min(GetGroupRewardValue(team, kind, actor), remaining);
            awardedTotal += ApplyGroupReward(team, kind, reward);
        }

        float GetGroupRewardValue(Team team, SoccerRewardKind kind, AgentSoccer actor)
        {
            var profile = GetProfile(team);
            if (profile == null)
            {
                return 0f;
            }

            var policy = GetPolicy(team);
            return policy != null
                ? policy.EvaluateGroupReward(kind, CreateContext(team, actor))
                : profile.GetGroupReward(kind);
        }

        // 그룹 보상은 공통 상한·누적 장부·Stats 기록을 보장하기 위해 이 경로를 거친다.
        float ApplyGroupReward(Team team, SoccerRewardKind kind, float reward)
        {
            if (!IsRewardAllowed(m_CurriculumRewardStage, kind))
                return 0f;
            if (Mathf.Abs(reward) < 0.00001f)
            {
                return 0f;
            }

            if (reward > 0f && !BypassesProfileShapingCap(kind))
            {
                var profile = GetProfile(team);
                if (profile == null)
                {
                    return 0f;
                }

                var possessionRemaining = profile.shapingRewardLimitPerPossession
                    - m_ShapingRewardTotals[(int)team];
                var matchRemaining = profile.shapingRewardLimitPerMatch
                    - m_MatchShapingRewardTotals[(int)team];
                reward = Mathf.Min(reward, Mathf.Min(possessionRemaining, matchRemaining));
                if (reward <= 0f)
                {
                    return 0f;
                }

                m_ShapingRewardTotals[(int)team] += reward;
                m_MatchShapingRewardTotals[(int)team] += reward;
            }

            m_Environment.AddTeamReward(team, reward);
            m_CumulativeTeamRewards[(int)team] += reward;
            Record(team, kind, reward);
            return reward;
        }

        float GetIndividualRewardValue(AgentSoccer agent, SoccerRewardKind kind)
        {
            if (agent == null)
            {
                return 0f;
            }

            var profile = GetProfile(agent.Team);
            if (profile == null)
            {
                return 0f;
            }

            var context = CreateContext(agent.Team, agent);
            var policy = GetPolicy(agent.Team);
            return policy != null
                ? policy.EvaluateIndividualReward(kind, context)
                : profile.GetIndividualReward(kind);
        }

        float AwardIndividual(
            AgentSoccer agent,
            SoccerRewardKind kind,
            float maximumReward = float.PositiveInfinity)
        {
            var profile = agent != null ? GetProfile(agent.Team) : null;
            if (profile == null)
            {
                return 0f;
            }

            var reward = Mathf.Min(GetIndividualRewardValue(agent, kind), maximumReward);
            return ApplyExplicitIndividualReward(agent, kind, reward);
        }

        float ApplyExplicitIndividualReward(AgentSoccer agent, SoccerRewardKind kind, float reward)
        {
            if (!IsRewardAllowed(m_CurriculumRewardStage, kind))
                return 0f;
            var profile = agent != null ? GetProfile(agent.Team) : null;
            if (profile == null)
            {
                return 0f;
            }

            if (!BypassesProfileShapingCap(kind))
            {
                var possessionRemaining = profile.shapingRewardLimitPerPossession
                    - m_ShapingRewardTotals[(int)agent.Team];
                var matchRemaining = profile.shapingRewardLimitPerMatch
                    - m_MatchShapingRewardTotals[(int)agent.Team];
                reward = Mathf.Min(reward, Mathf.Min(possessionRemaining, matchRemaining));
            }
            if (reward <= 0f)
            {
                return 0f;
            }

            if (!BypassesProfileShapingCap(kind))
            {
                m_ShapingRewardTotals[(int)agent.Team] += reward;
                m_MatchShapingRewardTotals[(int)agent.Team] += reward;
            }
            agent.AddTrainingReward(reward);
            m_CumulativeTeamRewards[(int)agent.Team] += reward;
            Record(agent.Team, kind, reward);
            return reward;
        }

        float ApplyExplicitSignedIndividualReward(
            AgentSoccer agent,
            SoccerRewardKind kind,
            float reward)
        {
            if (!IsRewardAllowed(m_CurriculumRewardStage, kind)
                || agent == null
                || GetProfile(agent.Team) == null
                || Mathf.Abs(reward) <= 0.000001f)
                return 0f;

            agent.AddTrainingReward(reward);
            m_CumulativeTeamRewards[(int)agent.Team] += reward;
            Record(agent.Team, kind, reward);
            return reward;
        }

        void AwardBehaviorPenalty(
            AgentSoccer agent,
            SoccerRewardKind kind,
            bool applyWastefulPossessionCap)
        {
            if (!IsRewardAllowed(m_CurriculumRewardStage, kind))
                return;
            var profile = agent != null ? GetProfile(agent.Team) : null;
            if (profile == null)
            {
                return;
            }

            var teamIndex = (int)agent.Team;
            var matchRemaining = profile.behaviorPenaltyLimitPerMatch
                - m_BehaviorPenaltyMatchTotals[teamIndex];
            var possessionRemaining = applyWastefulPossessionCap
                ? profile.wastefulStrongKickPenaltyLimitPerPossession
                    - m_WastefulStrongKickPenaltyTotals[teamIndex]
                : float.PositiveInfinity;
            var penalty = Mathf.Min(
                Mathf.Max(0f, GetIndividualRewardValue(agent, kind)),
                Mathf.Min(matchRemaining, possessionRemaining));
            if (penalty <= 0f)
            {
                return;
            }

            if (applyWastefulPossessionCap)
            {
                m_WastefulStrongKickPenaltyTotals[teamIndex] += penalty;
            }

            m_BehaviorPenaltyMatchTotals[teamIndex] += penalty;
            agent.AddTrainingReward(-penalty);
            m_CumulativeTeamRewards[teamIndex] -= penalty;
            Record(agent.Team, kind, -penalty);
        }

        SoccerRewardContext CreateContext(Team team, AgentSoccer actor)
        {
            var ballPosition = m_Environment != null && m_Environment.Ball != null
                ? m_Environment.Ball.transform.position
                : Vector3.zero;
            return new SoccerRewardContext(team, actor, ballPosition, Time.time);
        }

        bool IsCoordinatedPress(Team team, Vector3 recoveryPosition)
        {
            var nearbyAgents = m_Environment.AgentsList
                .Select(item => item?.Agent)
                .Where(agent => agent != null
                    && agent.gameObject.activeInHierarchy
                    && agent.Team == team
                    && Vector3.Distance(agent.transform.position, recoveryPosition) <= CoordinatedPressRange)
                .ToArray();
            if (nearbyAgents.Length < 2)
            {
                return false;
            }

            for (var first = 0; first < nearbyAgents.Length; first++)
            {
                for (var second = first + 1; second < nearbyAgents.Length; second++)
                {
                    if (Vector3.Distance(
                            nearbyAgents[first].transform.position,
                            nearbyAgents[second].transform.position) >= CoordinatedPressMinimumSpacing)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        void ResetPossessionTracking()
        {
            Array.Clear(m_PassRewardTotals, 0, m_PassRewardTotals.Length);
            Array.Clear(m_PassIndividualRewardTotals, 0, m_PassIndividualRewardTotals.Length);
            Array.Clear(m_ControlledCarryRewardTotals, 0, m_ControlledCarryRewardTotals.Length);
            Array.Clear(m_ProgressivePassRewardTotals, 0, m_ProgressivePassRewardTotals.Length);
            Array.Clear(m_CombinationRewardTotals, 0, m_CombinationRewardTotals.Length);
            Array.Clear(m_FormationRewardTotals, 0, m_FormationRewardTotals.Length);
            Array.Clear(m_ShapingRewardTotals, 0, m_ShapingRewardTotals.Length);
            Array.Clear(m_WastefulStrongKickPenaltyTotals, 0, m_WastefulStrongKickPenaltyTotals.Length);
            Array.Clear(m_HasFormationScore, 0, m_HasFormationScore.Length);
            m_CombinationSequence.Clear();
            m_DribbleRewardedAgents.Clear();
            m_ControlledCarryRewardedAgents.Clear();
            m_ControlledCarryCandidate = default;
        }

        SoccerTeamRewardPolicyBase GetPolicy(Team team)
        {
            m_MatchSetup ??= GetComponent<SoccerMatchSetup>();
            return m_MatchSetup != null ? m_MatchSetup.GetRewardPolicy(team) : null;
        }

        SoccerRewardProfile GetProfile(Team team)
        {
            var policyProfile = GetPolicy(team)?.RewardProfile;
            if (policyProfile != null)
            {
                return policyProfile;
            }

            m_MatchSetup ??= GetComponent<SoccerMatchSetup>();
            return m_MatchSetup != null ? m_MatchSetup.GetDefinition(team)?.RewardProfile : null;
        }

        void Record(Team team, SoccerRewardKind kind, float reward)
        {
            if (Academy.IsInitialized)
            {
                var statsRecorder = Academy.Instance.StatsRecorder;
                // 기존 합산 태그는 과거 Run과 Dashboard 호환용으로 유지한다.
                statsRecorder.Add($"Soccer/Reward/{kind}", reward, StatAggregationMethod.Sum);
                statsRecorder.Add(GetTeamRewardEventStatKey(team, kind), reward, StatAggregationMethod.Sum);
                statsRecorder.Add(GetTeamRewardSummaryTotalStatKey(team), reward, StatAggregationMethod.Sum);
            }
        }

        static string GetTeamTelemetryName(Team team)
        {
            return team == Team.Red ? "Red" : "Navy";
        }

        static bool BypassesProfileShapingCap(SoccerRewardKind kind)
        {
            return kind is SoccerRewardKind.GoalResult
                or SoccerRewardKind.MatchResult
                or SoccerRewardKind.CurriculumApproachProgress
                or SoccerRewardKind.CurriculumPossessionEstablished
                or SoccerRewardKind.CurriculumSupportShape
                or SoccerRewardKind.CurriculumDribbleProgress
                or SoccerRewardKind.CurriculumShotAttempt
                or SoccerRewardKind.CurriculumShotOnTarget
                or SoccerRewardKind.CurriculumGoal
                or SoccerRewardKind.CurriculumLessonSuccess
                or SoccerRewardKind.CurriculumShotAlignment
                or SoccerRewardKind.CurriculumPassAttempt
                or SoccerRewardKind.CurriculumPassReception
                or SoccerRewardKind.CurriculumPassDelivered
                or SoccerRewardKind.CurriculumPassDirection
                or SoccerRewardKind.CurriculumReceiverProgress
                or SoccerRewardKind.CurriculumStableReceiver
                or SoccerRewardKind.CurriculumFindProgress
                or SoccerRewardKind.CurriculumScoreProgress
                or SoccerRewardKind.CurriculumLongPassGoalBonus
                or SoccerRewardKind.CurriculumOwnGoalPenalty
                or SoccerRewardKind.CurriculumFindTimeoutPenalty
                or SoccerRewardKind.CurriculumFindMissPenalty;
        }

        static bool IsInDefensiveThird(Team team, float ballX)
        {
            return team == Team.Red ? ballX < -20f : ballX > 20f;
        }

        static Team Opponent(Team team)
        {
            return team == Team.Red ? Team.Navy : Team.Red;
        }

        struct DribbleCandidate
        {
            public AgentSoccer Agent;
            public AgentSoccer Opponent;
            public Vector3 StartingPosition;
            public Vector3 StartingBallPosition;
            public float StartingTime;
            public bool Valid;
        }

        struct ControlledCarryCandidate
        {
            public AgentSoccer Agent;
            public Vector3 StartingPosition;
            public Vector3 StartingBallPosition;
            public float StartingTime;
            public bool Valid;
        }

        struct PassCandidate
        {
            public Team Team;
            public AgentSoccer Passer;
            public AgentSoccer Receiver;
            public Vector3 StartPosition;
            public Vector3 ReceivePosition;
            public float ReceivedTime;
            public bool Valid;
        }
    }
}
