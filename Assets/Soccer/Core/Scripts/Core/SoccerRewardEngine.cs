using System;
using System.Collections.Generic;
using System.Linq;
using Unity.MLAgents;
using UnityEngine;

namespace MachineLearning.Soccer
{
    public sealed class SoccerRewardEngine : MonoBehaviour
    {
        const float MinimumPassDistance = 3f;
        const float PossessionConfirmationSeconds = 0.75f;
        const float PressWindowSeconds = 3f;
        const float AttackProgressDistance = 12f;
        const float DribbleOpponentRange = 2.5f;
        const float DribbleProgressDistance = 2f;
        const float FormationSampleSeconds = 0.25f;

        SoccerEnvController m_Environment;
        SoccerMatchSetup m_MatchSetup;
        AgentSoccer m_LastTouchAgent;
        Vector3 m_LastDistinctTouchPosition;
        float m_LastDistinctTouchTime;
        AgentSoccer m_PendingPossessionAgent;
        float m_PendingPossessionSince;
        AgentSoccer m_BallCarrier;
        Team? m_PossessionTeam;
        float m_PossessionStartBallX;
        bool m_AttackSuccessAwarded;
        readonly float[] m_LastLossTimes = { float.NegativeInfinity, float.NegativeInfinity };
        readonly float[] m_PassRewardTotals = new float[2];
        readonly float[] m_FormationRewardTotals = new float[2];
        readonly float[] m_LastFormationScores = new float[2];
        readonly bool[] m_HasFormationScore = new bool[2];
        float m_NextFormationSampleTime;
        DribbleCandidate m_DribbleCandidate;

        public Team? PossessionTeam => m_PossessionTeam;
        public AgentSoccer BallCarrier => m_BallCarrier;

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
            EvaluateAttackSuccess();
            EvaluateDribbleSuccess();
            if (Time.time >= m_NextFormationSampleTime)
            {
                m_NextFormationSampleTime = Time.time + FormationSampleSeconds;
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
            if (m_LastTouchAgent != null && m_LastTouchAgent != agent && m_LastTouchAgent.Team == agent.Team)
            {
                var passDistance = Vector3.Distance(m_LastDistinctTouchPosition, ballPosition);
                if (passDistance >= MinimumPassDistance && Time.time - m_LastDistinctTouchTime >= 0.15f)
                {
                    AwardPass(agent.Team, agent);
                }
            }

            if (m_LastTouchAgent == null || m_LastTouchAgent.Team != agent.Team)
            {
                m_PendingPossessionAgent = agent;
                m_PendingPossessionSince = Time.time;
            }
            else if (m_PendingPossessionAgent != null)
            {
                // Keep the confirmation timer, but track the teammate who most recently controlled the ball.
                m_PendingPossessionAgent = agent;
            }
            else if (m_PossessionTeam == agent.Team)
            {
                // A completed pass changes the carrier without starting a new team-possession window.
                m_BallCarrier = agent;
            }

            if (m_LastTouchAgent != agent)
            {
                m_LastDistinctTouchPosition = ballPosition;
                m_LastDistinctTouchTime = Time.time;
                BeginDribbleCandidate(agent);
            }

            m_LastTouchAgent = agent;
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
            m_PendingPossessionAgent = null;
            m_BallCarrier = null;
            m_PossessionTeam = null;
            m_AttackSuccessAwarded = false;
            m_DribbleCandidate = default;
            Array.Clear(m_PassRewardTotals, 0, m_PassRewardTotals.Length);
            Array.Clear(m_FormationRewardTotals, 0, m_FormationRewardTotals.Length);
            Array.Clear(m_HasFormationScore, 0, m_HasFormationScore.Length);
        }

        void ConfirmPossessionIfStable()
        {
            if (m_PendingPossessionAgent == null
                || m_LastTouchAgent == null
                || m_LastTouchAgent.Team != m_PendingPossessionAgent.Team
                || Time.time - m_PendingPossessionSince < PossessionConfirmationSeconds)
            {
                return;
            }

            var newTeam = m_PendingPossessionAgent.Team;
            if (m_PossessionTeam.HasValue && m_PossessionTeam.Value != newTeam)
            {
                var previousTeam = m_PossessionTeam.Value;
                m_LastLossTimes[(int)previousTeam] = Time.time;
                var pressRecovery = Time.time - m_LastLossTimes[(int)newTeam] <= PressWindowSeconds
                    && !IsInDefensiveThird(newTeam, m_Environment.Ball.transform.position.x);
                AwardGroup(
                    newTeam,
                    pressRecovery ? SoccerRewardKind.PressSuccess : SoccerRewardKind.DefenseSuccess,
                    1f,
                    m_PendingPossessionAgent);
            }

            m_PossessionTeam = newTeam;
            m_BallCarrier = m_PendingPossessionAgent;
            m_PossessionStartBallX = m_Environment.Ball.transform.position.x;
            m_AttackSuccessAwarded = false;
            m_DribbleCandidate = default;
            Array.Clear(m_PassRewardTotals, 0, m_PassRewardTotals.Length);
            Array.Clear(m_FormationRewardTotals, 0, m_FormationRewardTotals.Length);
            Array.Clear(m_HasFormationScore, 0, m_HasFormationScore.Length);
            m_PendingPossessionAgent = null;
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

        void AwardPass(Team team, AgentSoccer receiver)
        {
            var profile = GetProfile(team);
            if (profile == null || m_PassRewardTotals[(int)team] >= profile.passRewardLimitPerPossession)
            {
                return;
            }

            var policy = GetPolicy(team);
            var context = CreateContext(team, receiver);
            var reward = policy != null ? policy.EvaluateGroupReward(SoccerRewardKind.PassSuccess, context) : profile.passSuccess;
            reward = Mathf.Min(reward, profile.passRewardLimitPerPossession - m_PassRewardTotals[(int)team]);
            if (reward <= 0f)
            {
                return;
            }

            m_PassRewardTotals[(int)team] += reward;
            m_Environment.AddTeamReward(team, reward);
            Record(SoccerRewardKind.PassSuccess, reward);
        }

        void BeginDribbleCandidate(AgentSoccer agent)
        {
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
                StartingTime = Time.time,
                Valid = true
            };
        }

        void EvaluateDribbleSuccess()
        {
            if (!m_DribbleCandidate.Valid
                || m_LastTouchAgent != m_DribbleCandidate.Agent
                || Time.time - m_DribbleCandidate.StartingTime < 0.5f)
            {
                return;
            }

            var agent = m_DribbleCandidate.Agent;
            var attackSign = agent.Team == Team.Blue ? 1f : -1f;
            var progress = (agent.transform.position.x - m_DribbleCandidate.StartingPosition.x) * attackSign;
            var aheadOfOpponent = (agent.transform.position.x - m_DribbleCandidate.Opponent.transform.position.x) * attackSign > 0f;
            if (progress < DribbleProgressDistance || !aheadOfOpponent)
            {
                return;
            }

            AwardGroup(agent.Team, SoccerRewardKind.DribbleSuccess, 1f, agent);
            AwardIndividual(agent, SoccerRewardKind.DribbleSuccess);
            m_DribbleCandidate = default;
        }

        void EvaluateFormationChange()
        {
            foreach (var team in new[] { Team.Blue, Team.Purple })
            {
                var agents = m_Environment.AgentsList
                    .Select(item => item?.Agent)
                    .Where(agent => agent != null && agent.Team == team)
                    .ToArray();
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
                var remainingPositive = profile.formationRewardLimitPerPossession - m_FormationRewardTotals[(int)team];
                var remainingNegative = -profile.formationRewardLimitPerPossession - m_FormationRewardTotals[(int)team];
                var context = CreateContext(team, null);
                var scale = GetPolicy(team)?.EvaluateGroupReward(SoccerRewardKind.FormationChange, context)
                    ?? profile.formationChangeScale;
                var reward = Mathf.Clamp(delta * scale, remainingNegative, remainingPositive);
                if (Mathf.Abs(reward) < 0.00001f)
                {
                    continue;
                }

                m_FormationRewardTotals[(int)team] += reward;
                m_Environment.AddTeamReward(team, reward);
                Record(SoccerRewardKind.FormationChange, reward);
            }
        }

        void AwardGroup(Team team, SoccerRewardKind kind, float sign, AgentSoccer actor)
        {
            var profile = GetProfile(team);
            if (profile == null)
            {
                return;
            }

            var context = CreateContext(team, actor);
            var policy = GetPolicy(team);
            var reward = (policy != null ? policy.EvaluateGroupReward(kind, context) : profile.GetGroupReward(kind)) * sign;
            if (Mathf.Abs(reward) < 0.00001f)
            {
                return;
            }

            m_Environment.AddTeamReward(team, reward);
            Record(kind, reward);
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
            if (reward <= 0f)
            {
                return;
            }

            agent.AddTrainingReward(reward);
            Record(kind, reward);
        }

        SoccerRewardContext CreateContext(Team team, AgentSoccer actor)
        {
            var ballPosition = m_Environment != null && m_Environment.Ball != null
                ? m_Environment.Ball.transform.position
                : Vector3.zero;
            return new SoccerRewardContext(team, actor, ballPosition, Time.time);
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
            public float StartingTime;
            public bool Valid;
        }
    }
}
