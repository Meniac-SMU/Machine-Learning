using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using UnityEngine;

namespace MachineLearning.Soccer
{
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
        Team? m_PossessionTeam;
        float m_PossessionStartBallX;
        bool m_AttackSuccessAwarded;
        bool m_PossessionTransitionPending;
        readonly float[] m_LastLossTimes = { float.NegativeInfinity, float.NegativeInfinity };
        readonly float[] m_PassRewardTotals = new float[2];
        readonly float[] m_ProgressivePassRewardTotals = new float[2];
        readonly float[] m_CombinationRewardTotals = new float[2];
        readonly float[] m_FormationRewardTotals = new float[2];
        readonly float[] m_ShapingRewardTotals = new float[2];
        readonly float[] m_MatchShapingRewardTotals = new float[2];
        readonly float[] m_CrowdingPenaltyTotals = new float[2];
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
        float m_CombinationStartTime;
        float m_NextFormationSampleTime;
        DribbleCandidate m_DribbleCandidate;
        PassCandidate m_PendingPass;

        public Team? PossessionTeam => m_PossessionTransitionPending ? null : m_PossessionTeam;
        public Team? LastTouchTeam => m_LastTouchAgent != null ? m_LastTouchAgent.Team : null;
        public AgentSoccer BallCarrier => m_BallCarrier;

        public float GetCumulativeReward(Team team)
        {
            return m_CumulativeTeamRewards[(int)team];
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
            var sampleTeamShape = Time.time >= m_NextFormationSampleTime;
            if (sampleTeamShape)
            {
                m_NextFormationSampleTime = Time.time + FormationSampleSeconds;
                RefreshTeamAgentBuffers();
                EvaluateTeammateCrowding();
            }

            if (m_PossessionTransitionPending || !m_PossessionTeam.HasValue)
            {
                return;
            }

            EvaluateAttackSuccess();
            EvaluateDribbleSuccess();
            if (sampleTeamShape)
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
                m_BallCarrier = agent;
            }

            if (m_LastTouchAgent != agent)
            {
                if (!m_PossessionTransitionPending && m_PossessionTeam == agent.Team)
                {
                    BeginDribbleCandidate(agent);
                }
            }
            else if (!m_DribbleCandidate.Valid
                && !m_PossessionTransitionPending
                && m_PossessionTeam == agent.Team)
            {
                BeginDribbleCandidate(agent);
            }

            m_LastTouchAgent = agent;
            m_LastTouchPosition = ballPosition;
            m_LastTouchTime = Time.time;
        }

        public void AwardGoal(Team scoredTeam)
        {
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
            m_PossessionTeam = null;
            m_AttackSuccessAwarded = false;
            m_PossessionTransitionPending = false;
            m_DribbleCandidate = default;
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
            m_DribbleCandidate = default;
            m_PossessionTransitionPending = false;
            m_PendingPossessionAgent = null;

            // 상대의 짧은 접촉 뒤 같은 팀이 재확보한 경우 기존 소유권 상한 유지.
            if (previousTeam == newTeam)
            {
                BeginDribbleCandidate(confirmedCarrier);
                return;
            }

            m_PossessionStartBallX = m_Environment.Ball.transform.position.x;
            m_AttackSuccessAwarded = false;
            ResetPossessionTracking();
            m_CombinationSequence.Add(confirmedCarrier);
            m_CombinationStartTime = Time.time;
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
            m_LastTouchAgent = null;
            m_LastTouchPosition = Vector3.zero;
            m_LastTouchTime = 0f;
            m_DribbleCandidate = default;
            m_PendingPass = default;
        }

        void EvaluateAttackSuccess()
        {
            if (!m_PossessionTeam.HasValue || m_AttackSuccessAwarded)
            {
                return;
            }

            var team = m_PossessionTeam.Value;
            var attackSign = team == Team.Blue ? 1f : -1f;
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

            var attackSign = team == Team.Blue ? 1f : -1f;
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

        void BeginDribbleCandidate(AgentSoccer agent)
        {
            if (m_DribbleRewardedAgents.Contains(agent))
            {
                return;
            }

            var nearestOpponent = m_Environment.AgentsList
                .Select(item => item?.Agent)
                .Where(candidate => candidate != null && candidate.Team != agent.Team)
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
            var attackSign = agent.Team == Team.Blue ? 1f : -1f;
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
                if (agent != null)
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
            if (Mathf.Abs(reward) < 0.00001f)
            {
                return 0f;
            }

            if (reward > 0f && kind is not SoccerRewardKind.GoalResult and not SoccerRewardKind.MatchResult)
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
            Record(kind, reward);
            return reward;
        }

        void AwardIndividual(AgentSoccer agent, SoccerRewardKind kind)
        {
            var profile = GetProfile(agent.Team);
            if (profile == null)
            {
                return;
            }

            var context = CreateContext(agent.Team, agent);
            var policy = GetPolicy(agent.Team);
            var reward = policy != null ? policy.EvaluateIndividualReward(kind, context) : profile.GetIndividualReward(kind);
            var possessionRemaining = profile.shapingRewardLimitPerPossession
                - m_ShapingRewardTotals[(int)agent.Team];
            var matchRemaining = profile.shapingRewardLimitPerMatch
                - m_MatchShapingRewardTotals[(int)agent.Team];
            reward = Mathf.Min(reward, Mathf.Min(possessionRemaining, matchRemaining));
            if (reward <= 0f)
            {
                return;
            }

            m_ShapingRewardTotals[(int)agent.Team] += reward;
            m_MatchShapingRewardTotals[(int)agent.Team] += reward;
            agent.AddTrainingReward(reward);
            m_CumulativeTeamRewards[(int)agent.Team] += reward;
            Record(kind, reward);
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
            Array.Clear(m_ProgressivePassRewardTotals, 0, m_ProgressivePassRewardTotals.Length);
            Array.Clear(m_CombinationRewardTotals, 0, m_CombinationRewardTotals.Length);
            Array.Clear(m_FormationRewardTotals, 0, m_FormationRewardTotals.Length);
            Array.Clear(m_ShapingRewardTotals, 0, m_ShapingRewardTotals.Length);
            Array.Clear(m_HasFormationScore, 0, m_HasFormationScore.Length);
            m_CombinationSequence.Clear();
            m_DribbleRewardedAgents.Clear();
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

        void Record(SoccerRewardKind kind, float reward)
        {
            if (Academy.IsInitialized)
            {
                Academy.Instance.StatsRecorder.Add($"Soccer/Reward/{kind}", reward, StatAggregationMethod.Sum);
            }
        }

        static bool IsInDefensiveThird(Team team, float ballX)
        {
            return team == Team.Blue ? ballX < -20f : ballX > 20f;
        }

        static Team Opponent(Team team)
        {
            return team == Team.Blue ? Team.Purple : Team.Blue;
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
