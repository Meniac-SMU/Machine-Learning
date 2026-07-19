using System;
using System.Collections.Generic;
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
    /// Owns the three-minute match, deterministic kick-off reset and both four-agent teams.
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
        [Min(1f)] public float matchDurationSeconds = 180f;
        [Min(0f)] public float goalResetDelaySeconds = 3f;

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

        public int BlueScore { get; private set; }
        public int PurpleScore { get; private set; }
        public float RemainingTime { get; private set; }
        public float GoalResetRemaining => m_GoalResetRemaining;
        public SoccerMatchState State { get; private set; } = SoccerMatchState.Playing;
        public bool IsPlayActive => State == SoccerMatchState.Playing && RemainingTime > 0f;
        public bool IsAIEnabled => m_AIEnabled;
        public bool IsTraining => m_IsTraining;
        public GameObject Ball => ball;
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
            ballRb = ball.GetComponent<Rigidbody>();
            m_BallStartingPos = ball.transform.position;
            m_BallStartingRotation = ball.transform.rotation;
            m_IsTraining = Academy.IsInitialized && Academy.Instance.IsCommunicatorOn;

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
                item.Agent.ConfigureControlMode(m_IsTraining, useHumanInput);
            }
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
                m_BlueAgentGroup.AddGroupReward(1f);
                m_PurpleAgentGroup.AddGroupReward(-1f);
            }
            else
            {
                PurpleScore++;
                m_PurpleAgentGroup.AddGroupReward(1f);
                m_BlueAgentGroup.AddGroupReward(-1f);
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
                if (item.Rb != null)
                {
                    item.Rb.linearVelocity = Vector3.zero;
                    item.Rb.angularVelocity = Vector3.zero;
                    item.Rb.Sleep();
                }
            }

            ResetBall();
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
                m_BlueAgentGroup.AddGroupReward(0.5f);
                m_PurpleAgentGroup.AddGroupReward(-0.5f);
            }
            else if (PurpleScore > BlueScore)
            {
                m_PurpleAgentGroup.AddGroupReward(0.5f);
                m_BlueAgentGroup.AddGroupReward(-0.5f);
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

            if (requester.PositionRole == AgentSoccer.Position.Goalie)
            {
                var goalieOffset = Mathf.Clamp(ball.transform.position.z * 0.35f, -9f, 9f);
                return requester.StartingPosition + Vector3.forward * goalieOffset;
            }

            AgentSoccer closest = null;
            var closestSqrDistance = float.PositiveInfinity;
            foreach (var item in AgentsList)
            {
                var teammate = item?.Agent;
                if (teammate == null || teammate.Team != requester.Team || teammate.PositionRole == AgentSoccer.Position.Goalie)
                {
                    continue;
                }

                var sqrDistance = (teammate.transform.position - ball.transform.position).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
                    closest = teammate;
                }
            }

            if (closest == requester)
            {
                return ball.transform.position;
            }

            var supportOffset = Mathf.Clamp(ball.transform.position.z * 0.25f, -6f, 6f);
            return requester.StartingPosition + Vector3.forward * supportOffset;
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
