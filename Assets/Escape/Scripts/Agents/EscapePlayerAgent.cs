using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MachineLearning.Escape
{
    [RequireComponent(typeof(EscapeAgentMotor), typeof(EscapePlayerHealth), typeof(BehaviorParameters))]
    public sealed class EscapePlayerAgent : Agent
    {
        EscapeEnvironmentController m_Environment;
        EscapeAgentMotor m_Motor;
        EscapePlayerHealth m_Health;
        BehaviorParameters m_BehaviorParameters;
        readonly EscapeRewardLedger m_RewardLedger = new();

        public EscapePlayerControlMode ControlMode { get; private set; } = EscapePlayerControlMode.Human;
        public EscapeAgentMotor Motor => m_Motor;
        public EscapePlayerHealth Health => m_Health;
        public float EpisodeReward => m_RewardLedger.EpisodeReward;
        public bool IsAIEnabled => ControlMode != EscapePlayerControlMode.Human;

        public override void Initialize()
        {
            m_Environment = GetComponentInParent<EscapeEnvironmentController>();
            m_Motor = GetComponent<EscapeAgentMotor>();
            m_Health = GetComponent<EscapePlayerHealth>();
            m_BehaviorParameters = GetComponent<BehaviorParameters>();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            sensor.AddObservation(m_Health.CurrentHealth / (float)m_Health.MaxHealth);
            sensor.AddObservation(m_Environment != null ? m_Environment.RemainingTimeNormalized : 0f);
            sensor.AddObservation(m_Environment != null ? m_Environment.RequiredButtonsNormalized : 0f);
            sensor.AddObservation(m_Environment != null && m_Environment.IsGateActive);
            sensor.AddObservation(m_Motor.NormalizedForwardSpeed);
            sensor.AddObservation(m_Motor.NormalizedTurnSpeed);
            sensor.AddObservation(m_Health.IsInvulnerable);
        }

        void Update()
        {
            if (Application.isBatchMode)
            {
                return;
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.iKey.wasPressedThisFrame)
            {
                ToggleAIControl();
            }
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (m_Environment == null || !m_Environment.IsRunning)
            {
                m_Motor.Stop();
                return;
            }

            var move = DecodeMove(actions.DiscreteActions[0]);
            var turn = DecodeTurn(actions.DiscreteActions[1]);
            m_Motor.SetInput(move, turn);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var actions = actionsOut.DiscreteActions;
            actions[0] = 0;
            actions[1] = 0;

            if (ControlMode == EscapePlayerControlMode.Human)
            {
                ReadHumanInput(actions);
            }
            else if (ControlMode == EscapePlayerControlMode.AutonomousHeuristic)
            {
                ReadAutonomousInput(actions);
            }
        }

        public void SetControlMode(EscapePlayerControlMode mode)
        {
            ControlMode = mode;
            if (m_BehaviorParameters == null)
            {
                m_BehaviorParameters = GetComponent<BehaviorParameters>();
            }

            switch (mode)
            {
                case EscapePlayerControlMode.Training:
                    m_BehaviorParameters.BehaviorType = BehaviorType.Default;
                    break;
                case EscapePlayerControlMode.Inference:
                    m_BehaviorParameters.BehaviorType = m_BehaviorParameters.Model != null
                        ? BehaviorType.InferenceOnly
                        : BehaviorType.Default;
                    break;
                default:
                    m_BehaviorParameters.BehaviorType = BehaviorType.HeuristicOnly;
                    break;
            }
        }

        public void SetAIEnabled(bool enabled)
        {
            if (!enabled)
            {
                SetControlMode(EscapePlayerControlMode.Human);
                return;
            }

            SetControlMode(m_BehaviorParameters != null && m_BehaviorParameters.Model != null
                ? EscapePlayerControlMode.Inference
                : EscapePlayerControlMode.Training);
        }

        public void ToggleAIControl()
        {
            SetAIEnabled(!IsAIEnabled);
        }

        public void AddGameReward(float reward)
        {
            AddReward(reward);
            m_RewardLedger.Add(reward);
        }

        public void ResetForEpisode(Vector3 position, Quaternion rotation)
        {
            m_RewardLedger.Reset();
            m_Health.ResetHealth();
            m_Motor.Teleport(position, rotation);
            gameObject.SetActive(true);
        }

        static float DecodeMove(int action)
        {
            return action switch
            {
                1 => 1f,
                2 => -1f,
                _ => 0f
            };
        }

        static float DecodeTurn(int action)
        {
            return action switch
            {
                1 => -1f,
                2 => 1f,
                _ => 0f
            };
        }

        static void ReadHumanInput(ActionSegment<int> actions)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            if (keyboard.wKey.isPressed)
            {
                actions[0] = 1;
            }
            else if (keyboard.sKey.isPressed)
            {
                actions[0] = 2;
            }

            if (keyboard.aKey.isPressed)
            {
                actions[1] = 1;
            }
            else if (keyboard.dKey.isPressed)
            {
                actions[1] = 2;
            }
        }

        void ReadAutonomousInput(ActionSegment<int> actions)
        {
            if (m_Environment == null || !m_Environment.TryGetPlayerObjective(out var objective))
            {
                return;
            }

            var toTarget = objective - transform.position;
            toTarget.y = 0f;
            var localDirection = transform.InverseTransformDirection(toTarget.normalized);
            actions[0] = toTarget.sqrMagnitude > 1.5f * 1.5f ? 1 : 0;
            if (localDirection.x < -0.08f)
            {
                actions[1] = 1;
            }
            else if (localDirection.x > 0.08f)
            {
                actions[1] = 2;
            }
        }
    }
}
