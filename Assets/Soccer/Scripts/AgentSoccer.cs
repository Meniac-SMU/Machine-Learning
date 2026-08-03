using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MachineLearning.Soccer
{
    public enum Team
    {
        Blue = 0,
        Purple = 1
    }

    [RequireComponent(typeof(Rigidbody), typeof(BehaviorParameters), typeof(DecisionRequester))]
    public sealed class AgentSoccer : Agent
    {
        public enum Position
        {
            Striker,
            Goalie,
            Generic
        }

        const float KickPower = 5000f;
        const float DribblePushPower = 120f;
        const float InputDeadZone = 0.15f;
        const float AutonomousKickRange = 1.8f;
        const float AutonomousKickFacingDot = 0.55f;

        [HideInInspector] public Team team;
        public Position position;
        [SerializeField] bool humanControllable;
        [HideInInspector] public Rigidbody agentRb;
        public Vector3 initialPos;
        public float rotSign;

        SoccerEnvController m_Environment;
        SoccerSettings m_Settings;
        BehaviorParameters m_BehaviorParameters;
        DecisionRequester m_DecisionRequester;
        EnvironmentParameters m_ResetParameters;
        float m_BallTouch;
        float m_LateralSpeed;
        float m_ForwardSpeed;
        bool m_UseHumanInput;
        SoccerKickPlate m_KickPlate;

        public Team Team => team;
        public Position PositionRole => position;
        public bool HumanControllable => humanControllable;
        public bool IsUsingHumanInput => m_UseHumanInput;
        public Vector3 StartingPosition => initialPos;
        public SoccerKickPlate KickPlate => m_KickPlate;

        public override void Initialize()
        {
            m_Environment = GetComponentInParent<SoccerEnvController>();
            m_BehaviorParameters = GetComponent<BehaviorParameters>();
            m_DecisionRequester = GetComponent<DecisionRequester>();
            team = m_BehaviorParameters.TeamId == (int)Team.Blue ? Team.Blue : Team.Purple;
            if (initialPos == Vector3.zero)
            {
                initialPos = transform.position;
            }

            rotSign = team == Team.Blue ? 1f : -1f;
            switch (position)
            {
                case Position.Goalie:
                    m_LateralSpeed = 1f;
                    m_ForwardSpeed = 1f;
                    break;
                case Position.Striker:
                    m_LateralSpeed = 0.3f;
                    m_ForwardSpeed = 1.3f;
                    break;
                default:
                    m_LateralSpeed = 0.3f;
                    m_ForwardSpeed = 1f;
                    break;
            }

            m_Settings = FindFirstObjectByType<SoccerSettings>();
            agentRb = GetComponent<Rigidbody>();
            agentRb.maxAngularVelocity = 500f;
            m_KickPlate = GetComponentInChildren<SoccerKickPlate>(true);
            m_ResetParameters = Academy.Instance.EnvironmentParameters;
        }

        void Update()
        {
            if (!m_UseHumanInput || m_KickPlate == null || (m_Environment != null && !m_Environment.IsPlayActive))
            {
                return;
            }

            var keyboardKick = Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
            var gamepadKick = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;
            if (keyboardKick || gamepadKick)
            {
                m_KickPlate.TryKick();
            }
        }

        public void Configure(Team configuredTeam, Position configuredPosition, bool canUseHumanInput, Vector3 startingPosition)
        {
            team = configuredTeam;
            position = configuredPosition;
            humanControllable = canUseHumanInput;
            initialPos = startingPosition;
            rotSign = configuredTeam == Team.Blue ? 1f : -1f;
        }

        public void ConfigureControlMode(bool training, bool useHumanInput)
        {
            m_UseHumanInput = humanControllable && useHumanInput;
            m_BehaviorParameters ??= GetComponent<BehaviorParameters>();
            m_DecisionRequester ??= GetComponent<DecisionRequester>();

            if (training)
            {
                m_BehaviorParameters.BehaviorType = BehaviorType.Default;
                m_DecisionRequester.DecisionPeriod = 5;
            }
            else if (m_UseHumanInput)
            {
                m_BehaviorParameters.BehaviorType = BehaviorType.HeuristicOnly;
                m_DecisionRequester.DecisionPeriod = 1;
            }
            else
            {
                m_BehaviorParameters.BehaviorType = m_BehaviorParameters.Model != null
                    ? BehaviorType.InferenceOnly
                    : BehaviorType.HeuristicOnly;
                m_DecisionRequester.DecisionPeriod = 5;
            }
        }

        public void MoveAgent(ActionSegment<int> actions)
        {
            if (agentRb == null || m_Settings == null || (m_Environment != null && !m_Environment.IsPlayActive))
            {
                return;
            }

            var forwardAction = actions[0];
            var lateralAction = actions[1];
            var rotationAction = actions[2];
            var forwardInput = forwardAction == 1 ? 1f : forwardAction == 2 ? -1f : 0f;
            var lateralInput = lateralAction == 1 ? 1f : lateralAction == 2 ? -1f : 0f;
            var rotationInput = rotationAction == 1 ? -1f : rotationAction == 2 ? 1f : 0f;

            // Translation and rotation are deliberately applied independently so W+A,
            // W+D and their gamepad equivalents work at the same time.
            var movement = transform.forward * (forwardInput * m_ForwardSpeed)
                + transform.right * (lateralInput * m_LateralSpeed);
            movement = Vector3.ClampMagnitude(movement, 1.3f);
            transform.Rotate(0f, rotationInput * m_Settings.rotationSpeed * Time.fixedDeltaTime, 0f);
            agentRb.AddForce(movement * m_Settings.agentRunSpeed, ForceMode.VelocityChange);
            ClampPlanarVelocity();
        }

        public override void OnActionReceived(ActionBuffers actionBuffers)
        {
            MoveAgent(actionBuffers.DiscreteActions);
            TryAutonomousKick(actionBuffers.DiscreteActions);
        }

        public override void Heuristic(in ActionBuffers actionsOut)
        {
            var actions = actionsOut.DiscreteActions;
            for (var i = 0; i < actions.Length; i++)
            {
                actions[i] = 0;
            }

            if (m_UseHumanInput)
            {
                ReadHumanInput(actions);
            }
            else
            {
                ReadAutonomousInput(actions);
            }
        }

        void ReadHumanInput(ActionSegment<int> actions)
        {
            var throttle = 0f;
            var turn = 0f;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                throttle += keyboard.wKey.isPressed ? 1f : 0f;
                throttle -= keyboard.sKey.isPressed ? 1f : 0f;
                turn += keyboard.dKey.isPressed ? 1f : 0f;
                turn -= keyboard.aKey.isPressed ? 1f : 0f;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                var triggerInput = gamepad.rightTrigger.ReadValue() - gamepad.leftTrigger.ReadValue();
                if (Mathf.Abs(triggerInput) > Mathf.Abs(throttle))
                {
                    throttle = triggerInput;
                }

                var stickTurn = gamepad.leftStick.x.ReadValue();
                if (Mathf.Abs(stickTurn) > Mathf.Abs(turn))
                {
                    turn = stickTurn;
                }
            }

            actions[0] = throttle > InputDeadZone ? 1 : throttle < -InputDeadZone ? 2 : 0;
            actions[2] = turn > InputDeadZone ? 2 : turn < -InputDeadZone ? 1 : 0;
        }

        void ReadAutonomousInput(ActionSegment<int> actions)
        {
            if (m_Environment == null)
            {
                return;
            }

            var target = m_Environment.GetAutonomousTarget(this);
            var toTarget = target - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.25f)
            {
                return;
            }

            var localDirection = transform.InverseTransformDirection(toTarget.normalized);
            actions[0] = localDirection.z < -0.45f ? 2 : 1;
            actions[2] = localDirection.x > 0.08f ? 2 : localDirection.x < -0.08f ? 1 : 0;
        }

        void TryAutonomousKick(ActionSegment<int> actions)
        {
            if (m_UseHumanInput || m_KickPlate == null || actions.Length == 0 || actions[0] != 1
                || m_Environment == null || m_Environment.Ball == null || !m_Environment.IsPlayActive)
            {
                return;
            }

            var toBall = m_Environment.Ball.transform.position - transform.position;
            toBall.y = 0f;
            if (toBall.sqrMagnitude > AutonomousKickRange * AutonomousKickRange || toBall.sqrMagnitude < 0.0001f)
            {
                return;
            }

            if (Vector3.Dot(transform.forward, toBall.normalized) >= AutonomousKickFacingDot)
            {
                m_KickPlate.TryKick();
            }
        }

        void ClampPlanarVelocity()
        {
            var velocity = agentRb.linearVelocity;
            var planar = new Vector3(velocity.x, 0f, velocity.z);
            if (planar.sqrMagnitude <= m_Settings.maximumPlanarSpeed * m_Settings.maximumPlanarSpeed)
            {
                return;
            }

            planar = planar.normalized * m_Settings.maximumPlanarSpeed;
            agentRb.linearVelocity = new Vector3(planar.x, velocity.y, planar.z);
        }

        void OnCollisionEnter(Collision collision)
        {
            if (!collision.gameObject.CompareTag("ball"))
            {
                return;
            }

            PushBall(collision, true);
        }

        void OnCollisionStay(Collision collision)
        {
            if (collision.gameObject.CompareTag("ball") && m_KickPlate != null && m_KickPlate.IsStrikeActive)
            {
                PushBall(collision, false);
            }
        }

        void PushBall(Collision collision, bool allowDribblePush)
        {
            var strongKick = m_KickPlate != null && m_KickPlate.TryConsumeStrike();
            if (!strongKick && !allowDribblePush)
            {
                return;
            }

            AddReward(0.2f * m_BallTouch);
            var toBall = collision.transform.position - transform.position;
            toBall.y = 0f;
            var contactDirection = toBall.sqrMagnitude > 0.0001f ? toBall.normalized : transform.forward;
            var direction = Vector3.Slerp(contactDirection, transform.forward, strongKick ? 0.65f : 0.25f).normalized;
            var ballRigidbody = collision.rigidbody ?? collision.gameObject.GetComponent<Rigidbody>();
            if (ballRigidbody != null)
            {
                ballRigidbody.AddForce(direction * (strongKick ? KickPower : DribblePushPower));
            }
        }

        public void ResetKickPlate()
        {
            m_KickPlate ??= GetComponentInChildren<SoccerKickPlate>(true);
            m_KickPlate?.ResetPlate();
        }

        public override void OnEpisodeBegin()
        {
            ResetKickPlate();
            if (m_ResetParameters != null)
            {
                m_BallTouch = m_ResetParameters.GetWithDefault("ball_touch", 0f);
            }
        }
    }
}
