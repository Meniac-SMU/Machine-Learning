using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;

namespace MachineLearning.Escape
{
    [RequireComponent(typeof(EscapeAgentMotor), typeof(BehaviorParameters))]
    public sealed class EscapeEnemyAgent : Agent
    {
        [SerializeField] float attackRange = 1.8f;
        [SerializeField] float attackCooldown = 1f;

        EscapeEnvironmentController m_Environment;
        EscapeAgentMotor m_Motor;
        BehaviorParameters m_BehaviorParameters;
        float m_AttackCooldownRemaining;

        public EscapeAgentMotor Motor => m_Motor;

        public override void Initialize()
        {
            m_Environment = GetComponentInParent<EscapeEnvironmentController>();
            m_Motor = GetComponent<EscapeAgentMotor>();
            m_BehaviorParameters = GetComponent<BehaviorParameters>();
        }

        public override void CollectObservations(VectorSensor sensor)
        {
            sensor.AddObservation(m_Motor.NormalizedForwardSpeed);
            sensor.AddObservation(m_Motor.NormalizedTurnSpeed);
            sensor.AddObservation(m_Environment != null ? m_Environment.RemainingTimeNormalized : 0f);
            sensor.AddObservation(m_Environment != null ? m_Environment.PlayerHealthNormalized : 0f);
            sensor.AddObservation(m_Environment != null && m_Environment.IsPlayerInvulnerable);
            sensor.AddObservation(m_Environment != null && m_Environment.IsGateActive);
            sensor.AddObservation(m_Environment != null ? m_Environment.RequiredButtonsNormalized : 0f);
        }

        public override void OnActionReceived(ActionBuffers actions)
        {
            if (m_Environment == null || !m_Environment.IsRunning)
            {
                m_Motor.Stop();
                return;
            }

            m_Motor.SetInput(DecodeMove(actions.DiscreteActions[0]), DecodeTurn(actions.DiscreteActions[1]));
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var actions = actionsOut.DiscreteActions;
            actions[0] = 0;
            actions[1] = 0;
            if (m_Environment == null || m_Environment.Player == null)
            {
                return;
            }

            var toPlayer = m_Environment.Player.transform.position - transform.position;
            toPlayer.y = 0f;
            var localDirection = transform.InverseTransformDirection(toPlayer.normalized);
            actions[0] = toPlayer.sqrMagnitude > 1.2f * 1.2f ? 1 : 0;
            if (localDirection.x < -0.06f)
            {
                actions[1] = 1;
            }
            else if (localDirection.x > 0.06f)
            {
                actions[1] = 2;
            }
        }

        public void TickAttack(float deltaTime)
        {
            m_AttackCooldownRemaining = Mathf.Max(0f, m_AttackCooldownRemaining - deltaTime);
            if (m_AttackCooldownRemaining > 0f || m_Environment == null || !m_Environment.IsRunning)
            {
                return;
            }

            var player = m_Environment.Player;
            var origin = transform.position + Vector3.up;
            var target = player.transform.position + Vector3.up;
            var delta = target - origin;
            if (delta.sqrMagnitude > attackRange * attackRange)
            {
                return;
            }

            if (!Physics.Raycast(origin, delta.normalized, out var hit, attackRange, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            if (hit.collider.GetComponentInParent<EscapePlayerAgent>() != player)
            {
                return;
            }

            m_AttackCooldownRemaining = attackCooldown;
            m_Environment.ApplyEnemyHit(this);
        }

        public void SetTraining(bool training)
        {
            if (m_BehaviorParameters == null)
            {
                m_BehaviorParameters = GetComponent<BehaviorParameters>();
            }

            m_BehaviorParameters.BehaviorType = training ? BehaviorType.Default : BehaviorType.HeuristicOnly;
        }

        public void ResetForEpisode(Vector3 position, Quaternion rotation)
        {
            m_AttackCooldownRemaining = 0f;
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
    }
}
