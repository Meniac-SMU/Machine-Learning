using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MachineLearning.Soccer
{
    public enum Team
    {
        Blue = 0,
        Purple = 1
    }

    /// <summary>
    /// 사람 입력·신경망 행동·규칙 행동을 공통 물리 동작으로 변환하는 선수 본체.
    /// 관측·행동·물리 의미는 모든 전술의 정책 계약이므로 팀별 코드에서 분기하지 않는다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody), typeof(BehaviorParameters), typeof(DecisionRequester))]
    public sealed class AgentSoccer : Agent
    {
        public enum Position
        {
            Striker = 0,
            DefenderKeeper = 1,
            Midfielder = 2
        }

        public const int VectorObservationSize = 43;
        public const int KickActionBranch = 3;

        public const float ControlledKickPower = 1500f;
        public const float StrongKickPower = 4000f;
        const float DribblePushPower = 120f;
        const float InputDeadZone = 0.15f;
        const float ObservationDistanceScale = 80f;
        const float ObservationVelocityScale = 20f;

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
        float m_LateralSpeed;
        float m_ForwardSpeed;
        float m_HumanThrottle;
        float m_HumanTurn;
        bool m_UseHumanInput;
        SoccerKickPlate m_KickPlate;
        SoccerTeamDefinition m_TeamDefinition;
        ISoccerRuleController m_RuleController;
        float m_ActiveKickPower = StrongKickPower;

        public Team Team => team;
        public Position PositionRole => position;
        public bool HumanControllable => humanControllable;
        public bool IsUsingHumanInput => m_UseHumanInput;
        public Vector3 StartingPosition => initialPos;
        public SoccerKickPlate KickPlate => m_KickPlate;
        public SoccerTeamDefinition TeamDefinition => m_TeamDefinition;
        public bool IsRuleControlled => m_TeamDefinition != null && !m_TeamDefinition.UsesNeuralPolicy;
        public ModelAsset ActiveModel
        {
            get
            {
                m_BehaviorParameters ??= GetComponent<BehaviorParameters>();
                return m_BehaviorParameters.Model;
            }
        }

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
                case Position.DefenderKeeper:
                    m_LateralSpeed = 0.9f;
                    m_ForwardSpeed = 1.05f;
                    break;
                case Position.Striker:
                    m_LateralSpeed = 0.65f;
                    m_ForwardSpeed = 1.25f;
                    break;
                default:
                    m_LateralSpeed = 0.75f;
                    m_ForwardSpeed = 1.1f;
                    break;
            }

            m_Settings = FindFirstObjectByType<SoccerSettings>();
            agentRb = GetComponent<Rigidbody>();
            agentRb.maxAngularVelocity = 500f;
            m_KickPlate = GetComponentInChildren<SoccerKickPlate>(true);
            FindRuleController();
        }

        public void Configure(Team configuredTeam, Position configuredPosition, bool canUseHumanInput, Vector3 startingPosition)
        {
            team = configuredTeam;
            position = configuredPosition;
            humanControllable = canUseHumanInput;
            initialPos = startingPosition;
            rotSign = configuredTeam == Team.Blue ? 1f : -1f;
        }

        public void ApplyTeamDefinition(SoccerTeamDefinition definition, ModelAsset modelOverride = null)
        {
            if (definition == null)
            {
                return;
            }

            m_TeamDefinition = definition;
            m_BehaviorParameters ??= GetComponent<BehaviorParameters>();
            m_BehaviorParameters.BehaviorName = definition.BehaviorName;
            m_BehaviorParameters.Model = definition.UsesNeuralPolicy
                ? modelOverride != null ? modelOverride : definition.InferenceModel
                : null;
            FindRuleController();
        }

        // BehaviorType과 DecisionPeriod는 환경 공통 실행 계약이다. Trainer 튜닝 목적으로 바꾸지 않는다.
        public void ConfigureControlMode(bool training, bool useHumanInput, bool trainable)
        {
            m_UseHumanInput = humanControllable && useHumanInput;
            m_HumanThrottle = 0f;
            m_HumanTurn = 0f;
            m_BehaviorParameters ??= GetComponent<BehaviorParameters>();
            m_DecisionRequester ??= GetComponent<DecisionRequester>();
            agentRb ??= GetComponent<Rigidbody>();
            agentRb.interpolation = !training && m_UseHumanInput
                ? RigidbodyInterpolation.Interpolate
                : RigidbodyInterpolation.None;

            if (m_UseHumanInput)
            {
                m_BehaviorParameters.BehaviorType = BehaviorType.HeuristicOnly;
                m_DecisionRequester.DecisionPeriod = 1;
            }
            else if (IsRuleControlled)
            {
                // 규칙형 팀의 Trainer 및 ONNX 연결 차단.
                m_BehaviorParameters.BehaviorType = BehaviorType.HeuristicOnly;
                m_DecisionRequester.DecisionPeriod = 5;
            }
            else if (training && trainable)
            {
                m_BehaviorParameters.BehaviorType = BehaviorType.Default;
                m_DecisionRequester.DecisionPeriod = 5;
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

            if (m_UseHumanInput)
            {
                MoveHumanAgent();
                return;
            }

            var forwardAction = actions[0];
            var lateralAction = actions[1];
            var rotationAction = actions[2];
            var forwardInput = forwardAction == 1 ? 1f : forwardAction == 2 ? -1f : 0f;
            var lateralInput = lateralAction == 1 ? 1f : lateralAction == 2 ? -1f : 0f;
            var rotationInput = rotationAction == 1 ? -1f : rotationAction == 2 ? 1f : 0f;

            // 이동과 회전의 동시 입력 처리.
            var movement = transform.forward * (forwardInput * m_ForwardSpeed)
                + transform.right * (lateralInput * m_LateralSpeed);
            movement = Vector3.ClampMagnitude(movement, 1.3f);
            // Neural·fallback·Rule 모두 거치는 공통 골키퍼 최종 보호층이다. 전술 코드에서 우회하지 않는다.
            movement = SoccerDefenderKeeperRules.ConstrainMovement(
                this,
                m_Environment,
                movement,
                out var recoveringToGoal);
            if (recoveringToGoal && movement.sqrMagnitude > 0.0001f)
            {
                var localRecoveryDirection = transform.InverseTransformDirection(movement.normalized);
                rotationInput = localRecoveryDirection.x > 0.08f
                    ? 1f
                    : localRecoveryDirection.x < -0.08f ? -1f : 0f;
            }

            transform.Rotate(0f, rotationInput * m_Settings.rotationSpeed * Time.fixedDeltaTime, 0f);
            agentRb.AddForce(movement * m_Settings.agentRunSpeed, ForceMode.VelocityChange);
            agentRb.linearVelocity = SoccerDefenderKeeperRules.ConstrainVelocity(
                this,
                agentRb.linearVelocity,
                recoveringToGoal);
            ClampPlanarVelocity();
        }

        void MoveHumanAgent()
        {
            if (Mathf.Abs(m_HumanTurn) > 0.0001f)
            {
                // 선수 Rigidbody는 Y 회전도 잠겨 있으므로 기존 충돌 계약을 바꾸지 않고
                // 사람 입력이 있을 때만 회전을 직접 적용한다.
                transform.Rotate(0f, m_HumanTurn * m_Settings.rotationSpeed * Time.fixedDeltaTime, 0f);
            }

            var velocity = agentRb.linearVelocity;
            var planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
            var targetVelocity = transform.forward * (m_HumanThrottle * m_Settings.maximumPlanarSpeed);
            var isReversing = targetVelocity.sqrMagnitude > 0.0001f
                && Vector3.Dot(planarVelocity, targetVelocity) < 0f;
            var changeRate = targetVelocity.sqrMagnitude <= 0.0001f || isReversing
                ? m_Settings.humanDeceleration
                : m_Settings.humanAcceleration;
            var nextPlanarVelocity = Vector3.MoveTowards(
                planarVelocity,
                targetVelocity,
                Mathf.Max(0.1f, changeRate) * Time.fixedDeltaTime);
            agentRb.linearVelocity = new Vector3(nextPlanarVelocity.x, velocity.y, nextPlanarVelocity.z);
            ClampPlanarVelocity();
        }

        public override void OnActionReceived(ActionBuffers actionBuffers)
        {
            MoveAgent(actionBuffers.DiscreteActions);
            ApplyKickAction(actionBuffers.DiscreteActions);
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

            m_HumanThrottle = ApplyInputDeadZone(throttle);
            m_HumanTurn = ApplyInputDeadZone(turn);
            actions[0] = m_HumanThrottle > 0f ? 1 : m_HumanThrottle < 0f ? 2 : 0;
            actions[2] = m_HumanTurn > 0f ? 2 : m_HumanTurn < 0f ? 1 : 0;
            if (actions.Length > KickActionBranch)
            {
                var controlledKick = (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                    || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
                var strongKick = (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                    || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
                actions[KickActionBranch] = strongKick ? 2 : controlledKick ? 1 : 0;
            }
        }

        static float ApplyInputDeadZone(float value)
        {
            var clamped = Mathf.Clamp(value, -1f, 1f);
            var magnitude = Mathf.Abs(clamped);
            if (magnitude <= InputDeadZone)
            {
                return 0f;
            }

            return Mathf.Sign(clamped) * Mathf.InverseLerp(InputDeadZone, 1f, magnitude);
        }

        void ReadAutonomousInput(ActionSegment<int> actions)
        {
            if (m_Environment == null)
            {
                return;
            }

            if (IsRuleControlled && m_RuleController != null)
            {
                m_RuleController.Decide().WriteTo(actions);
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
            if (actions.Length <= KickActionBranch || m_Environment.Ball == null)
            {
                return;
            }

            var toBall = m_Environment.Ball.transform.position - transform.position;
            toBall.y = 0f;
            actions[KickActionBranch] = toBall.sqrMagnitude <= 1.8f * 1.8f
                && toBall.sqrMagnitude > 0.0001f
                && Vector3.Dot(transform.forward, toBall.normalized) >= 0.55f
                ? 2
                : 0;
        }

        void ApplyKickAction(ActionSegment<int> actions)
        {
            if (m_KickPlate == null || actions.Length <= KickActionBranch || !m_KickPlate.CanKick)
            {
                return;
            }

            switch (actions[KickActionBranch])
            {
                case 1:
                    m_ActiveKickPower = ControlledKickPower;
                    m_KickPlate.TryKick();
                    break;
                case 2:
                    m_ActiveKickPower = StrongKickPower;
                    m_KickPlate.TryKick();
                    break;
            }
        }

        public override void WriteDiscreteActionMask(IDiscreteActionMask actionMask)
        {
            if (m_KickPlate != null && !m_KickPlate.CanKick)
            {
                actionMask.SetActionEnabled(KickActionBranch, 1, false);
                actionMask.SetActionEnabled(KickActionBranch, 2, false);
            }
        }

        // 정책 계약 v2의 고정 관측 순서다. 변경 시 계약 버전·모든 학습형 팀·ONNX를 함께 갱신한다.
        public override void CollectObservations(VectorSensor sensor)
        {
            sensor.AddOneHotObservation((int)position, 3);
            var attackSign = team == Team.Blue ? 1f : -1f;
            sensor.AddObservation(transform.position.x * attackSign / 60f);
            sensor.AddObservation(transform.position.z / 40f);
            var velocity = agentRb != null ? agentRb.linearVelocity : Vector3.zero;
            sensor.AddObservation(velocity.x * attackSign / ObservationVelocityScale);
            sensor.AddObservation(velocity.z / ObservationVelocityScale);

            var ball = m_Environment != null ? m_Environment.Ball : null;
            var ballOffset = ball != null ? ball.transform.position - transform.position : Vector3.zero;
            sensor.AddObservation(ballOffset.x * attackSign / ObservationDistanceScale);
            sensor.AddObservation(ballOffset.z / ObservationDistanceScale);
            var ballVelocity = m_Environment != null && m_Environment.ballRb != null
                ? m_Environment.ballRb.linearVelocity
                : Vector3.zero;
            sensor.AddObservation(ballVelocity.x * attackSign / ObservationVelocityScale);
            sensor.AddObservation(ballVelocity.z / ObservationVelocityScale);

            var possession = m_Environment != null ? m_Environment.PossessionTeam : null;
            sensor.AddObservation(!possession.HasValue ? 1f : 0f);
            sensor.AddObservation(possession == team ? 1f : 0f);
            sensor.AddObservation(possession.HasValue && possession != team ? 1f : 0f);
            sensor.AddObservation(m_KickPlate != null && m_KickPlate.CanKick ? 1f : 0f);

            AddOtherAgentObservations(sensor, team, 3, attackSign);
            AddOtherAgentObservations(sensor, team == Team.Blue ? Team.Purple : Team.Blue, 4, attackSign);
        }

        void AddOtherAgentObservations(VectorSensor sensor, Team observedTeam, int requiredCount, float attackSign)
        {
            var added = 0;
            if (m_Environment != null)
            {
                foreach (var item in m_Environment.AgentsList)
                {
                    var other = item?.Agent;
                    if (other == null || other == this || other.Team != observedTeam || added >= requiredCount)
                    {
                        continue;
                    }

                    var offset = other.transform.position - transform.position;
                    var otherVelocity = other.agentRb != null ? other.agentRb.linearVelocity : Vector3.zero;
                    sensor.AddObservation(offset.x * attackSign / ObservationDistanceScale);
                    sensor.AddObservation(offset.z / ObservationDistanceScale);
                    sensor.AddObservation(otherVelocity.x * attackSign / ObservationVelocityScale);
                    sensor.AddObservation(otherVelocity.z / ObservationVelocityScale);
                    added++;
                }
            }

            while (added < requiredCount)
            {
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                sensor.AddObservation(0f);
                added++;
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

            m_Environment?.NotifyBallTouch(this);
            PushBall(collision, true);
        }

        void OnCollisionStay(Collision collision)
        {
            if (collision.gameObject.CompareTag("ball") && m_KickPlate != null && m_KickPlate.IsStrikeActive)
            {
                m_Environment?.NotifyBallTouch(this);
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

            var toBall = collision.transform.position - transform.position;
            toBall.y = 0f;
            var contactDirection = toBall.sqrMagnitude > 0.0001f ? toBall.normalized : transform.forward;
            var direction = Vector3.Slerp(contactDirection, transform.forward, strongKick ? 0.65f : 0.25f).normalized;
            var ballRigidbody = collision.rigidbody ?? collision.gameObject.GetComponent<Rigidbody>();
            if (ballRigidbody != null)
            {
                ballRigidbody.AddForce(direction * (strongKick ? m_ActiveKickPower : DribblePushPower));
            }
        }

        public void AddTrainingReward(float reward)
        {
            AddReward(reward);
        }

        public void ResetKickPlate()
        {
            m_KickPlate ??= GetComponentInChildren<SoccerKickPlate>(true);
            m_KickPlate?.ResetPlate();
            m_ActiveKickPower = StrongKickPower;
        }

        public override void OnEpisodeBegin()
        {
            ResetKickPlate();
            m_RuleController?.ResetController();
        }

        void FindRuleController()
        {
            m_RuleController = null;
            foreach (var component in GetComponents<MonoBehaviour>())
            {
                if (component is not ISoccerRuleController ruleController)
                {
                    continue;
                }

                m_RuleController = ruleController;
                m_RuleController.Configure(this, m_Environment);
                break;
            }
        }
    }
}
