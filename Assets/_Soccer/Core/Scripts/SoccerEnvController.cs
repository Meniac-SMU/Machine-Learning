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

        SimpleMultiAgentGroup m_BlueAgentGroup;
        SimpleMultiAgentGroup m_PurpleAgentGroup;
        Vector3 m_BallStartingPos;
        Quaternion m_BallStartingRotation;
        float m_GoalResetRemaining;
        bool m_IsTraining;
        bool m_AIEnabled;
        Team m_LastScoringTeam;
        SoccerMatchSetup m_MatchSetup;
        SoccerRewardEngine m_RewardEngine;

        public int BlueScore { get; private set; }
        public int PurpleScore { get; private set; }
        public float RemainingTime { get; private set; }
        public float GoalResetRemaining => m_GoalResetRemaining;
        public SoccerMatchState State { get; private set; } = SoccerMatchState.Playing;
        public bool IsPlayActive => State == SoccerMatchState.Playing && RemainingTime > 0f;
        public bool IsAIEnabled => m_AIEnabled;
        public bool StartsInAIMode => startInAIMode;
        public bool IsTraining => m_IsTraining;
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

            m_BlueAgentGroup = new SimpleMultiAgentGroup();
            m_PurpleAgentGroup = new SimpleMultiAgentGroup();
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

                if (item.Agent.Team == Team.Blue)
                {
                    m_BlueAgentGroup.RegisterAgent(item.Agent);
                }
                else
                {
                    m_PurpleAgentGroup.RegisterAgent(item.Agent);
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
        }

        public void AddTeamReward(Team rewardTeam, float reward)
        {
            if (rewardTeam == Team.Blue)
            {
                m_BlueAgentGroup?.AddGroupReward(reward);
            }
            else
            {
                m_PurpleAgentGroup?.AddGroupReward(reward);
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

        public void GoalTouched(Team scoredTeam)
        {
            if (!IsPlayActive)
            {
                return;
            }

            m_LastScoringTeam = scoredTeam;
            if (scoredTeam == Team.Blue)
            {
                BlueScore++;
            }
            else
            {
                PurpleScore++;
            }

            if (m_RewardEngine != null)
            {
                m_RewardEngine.AwardGoal(scoredTeam);
            }
            else
            {
                AddTeamReward(scoredTeam, 1f);
                AddTeamReward(scoredTeam == Team.Blue ? Team.Purple : Team.Blue, -1f);
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
            BlueScore = 0;
            PurpleScore = 0;
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

            if (BlueScore > PurpleScore)
            {
                if (m_RewardEngine != null)
                {
                    m_RewardEngine.AwardMatchResult(Team.Blue);
                }
                else
                {
                    AddTeamReward(Team.Blue, 0.5f);
                    AddTeamReward(Team.Purple, -0.5f);
                }
            }
            else if (PurpleScore > BlueScore)
            {
                if (m_RewardEngine != null)
                {
                    m_RewardEngine.AwardMatchResult(Team.Purple);
                }
                else
                {
                    AddTeamReward(Team.Purple, 0.5f);
                    AddTeamReward(Team.Blue, -0.5f);
                }
            }

            m_BlueAgentGroup.EndGroupEpisode();
            m_PurpleAgentGroup.EndGroupEpisode();
            if (m_IsTraining)
            {
                RestartMatch();
            }
        }

        public Vector3 GetAutonomousTarget(AgentSoccer requester)
        {
            if (requester == null || ball == null)
            {
                return transform.position;
            }

            var attackSign = requester.Team == Team.Blue ? 1f : -1f;
            var closest = FindClosestTeammateToBall(requester.Team);
            if (closest == requester || m_RewardEngine?.BallCarrier == requester)
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
                        + Vector3.right * (attackSign * (laneSign > 0f ? 4f : -4f))
                        + Vector3.forward * (laneSign * 11f)
                };
            }
            else if (opponentOwnsBall)
            {
                // 공과 자기 골문 사이의 동적 수비 지원 위치.
                var ownGoal = new Vector3(-attackSign * 58f, ball.transform.position.y, 0f);
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

            if (ball == null || ShouldDefenderKeeperRecover(agent))
            {
                return false;
            }

            return SoccerDefenderKeeperRules.GetAttackingDepth(agent.Team, ball.transform.position.x) <= -12f;
        }

        public Vector3 GetDefenderKeeperHomeTarget(AgentSoccer agent)
        {
            return SoccerDefenderKeeperRules.GetHomeTarget(agent, this);
        }

        public Vector3 ConstrainDefenderKeeperTarget(AgentSoccer agent, Vector3 target)
        {
            return SoccerDefenderKeeperRules.ConstrainTarget(agent, this, target);
        }

        AgentSoccer FindClosestTeammateToBall(Team team)
        {
            AgentSoccer closest = null;
            var closestSqrDistance = float.PositiveInfinity;
            foreach (var item in AgentsList)
            {
                var teammate = item?.Agent;
                if (teammate == null || teammate.Team != team)
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

            return agent.PositionRole == AgentSoccer.Position.Striker ? 1f : -1f;
        }

        static Vector3 ClampFieldTarget(Vector3 target)
        {
            target.x = Mathf.Clamp(target.x, -54f, 54f);
            target.z = Mathf.Clamp(target.z, -34f, 34f);
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

            if (BlueScore == PurpleScore)
            {
                return "FULL TIME\nDRAW";
            }

            return $"FULL TIME\n{(BlueScore > PurpleScore ? "BLUE" : "PURPLE")} WINS";
        }
    }
}
