using System;
using MachineLearning.Soccer;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MachineLearning.Soccer.Manager
{
    [DefaultExecutionOrder(40)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MNG_PlayerAvatar), typeof(MNG_PlayerMotor))]
    public sealed class MNG_HumanInput : MonoBehaviour
    {
        const float DeadZone = 0.15f;

        [SerializeField] MNG_MatchController matchController;
        [SerializeField] MNG_BallControl ballControl;

        MNG_PlayerAvatar m_Avatar;
        MNG_PlayerMotor m_Motor;
        float m_Throttle;
        float m_Turn;
        bool m_WeakKickRequested;
        bool m_StrongKickRequested;

        public bool IsHuman => m_Avatar != null && m_Avatar.Ownership.Owner == MNG_InputOwner.Human;

        void Awake()
        {
            m_Avatar = GetComponent<MNG_PlayerAvatar>();
            m_Motor = GetComponent<MNG_PlayerMotor>();
            if (matchController == null) matchController = GetComponentInParent<MNG_MatchController>();
            if (ballControl == null && matchController != null)
                ballControl = matchController.GetComponentInChildren<MNG_BallControl>();
            if (m_Avatar.Team != Team.Red || m_Avatar.Role != MNG_PlayerRole.Striker)
                throw new InvalidOperationException("MNG human input is restricted to the Red Striker.");
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var gamepad = Gamepad.current;
            if ((keyboard != null && keyboard.hKey.wasPressedThisFrame)
                || (gamepad != null && gamepad.buttonNorth.wasPressedThisFrame))
            {
                ToggleOwner();
            }

            if (!IsHuman)
            {
                m_Throttle = 0f;
                m_Turn = 0f;
                return;
            }

            var keyboardThrottle = keyboard == null ? 0f
                : (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            var keyboardTurn = keyboard == null ? 0f
                : (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            var gamepadThrottle = gamepad == null ? 0f
                : gamepad.rightTrigger.ReadValue() - gamepad.leftTrigger.ReadValue();
            var gamepadTurn = gamepad == null ? 0f : gamepad.leftStick.x.ReadValue();
            m_Throttle = ApplyDeadZone(Mathf.Abs(gamepadThrottle) > Mathf.Abs(keyboardThrottle)
                ? gamepadThrottle : keyboardThrottle);
            m_Turn = ApplyDeadZone(Mathf.Abs(gamepadTurn) > Mathf.Abs(keyboardTurn)
                ? gamepadTurn : keyboardTurn);
            m_WeakKickRequested |= (keyboard != null && keyboard.eKey.wasPressedThisFrame)
                || (gamepad != null && gamepad.buttonSouth.wasPressedThisFrame);
            m_StrongKickRequested |= (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
                || (gamepad != null && gamepad.buttonEast.wasPressedThisFrame);
        }

        public void Configure(MNG_MatchController configuredMatch, MNG_BallControl configuredBallControl)
        {
            matchController = configuredMatch ?? throw new ArgumentNullException(nameof(configuredMatch));
            ballControl = configuredBallControl ?? throw new ArgumentNullException(nameof(configuredBallControl));
            m_Avatar = GetComponent<MNG_PlayerAvatar>();
            m_Motor = GetComponent<MNG_PlayerMotor>();
            if (m_Avatar.Team != Team.Red || m_Avatar.Role != MNG_PlayerRole.Striker)
                throw new InvalidOperationException("MNG human input is restricted to the Red Striker.");
        }

        void FixedUpdate()
        {
            if (!IsHuman) return;
            var revision = m_Avatar.Ownership.Revision;
            var facing = Quaternion.AngleAxis(
                m_Turn * m_Motor.Profile.RotationDegreesPerSecond * Time.fixedDeltaTime,
                Vector3.up) * transform.forward;
            var desired = new Vector2(facing.x, facing.z) * (m_Throttle * m_Motor.Profile.MaximumPlayerSpeed);
            m_Motor.ApplyDesiredVelocity(
                desired,
                new Vector2(facing.x, facing.z),
                MNG_InputOwner.Human,
                revision,
                Time.fixedDeltaTime);

            if (m_StrongKickRequested || m_WeakKickRequested)
            {
                var exitSpeed = m_StrongKickRequested
                    ? MNG_KickSolver.StrongExitSpeed
                    : MNG_KickSolver.ControlledExitSpeed;
                var target = m_Avatar.Body.position + facing.normalized * 30f;
                ballControl?.TryKick(m_Avatar.Team, m_Avatar.Slot, target, exitSpeed, out _);
            }
            m_WeakKickRequested = false;
            m_StrongKickRequested = false;
        }

        public MNG_InputOwner ToggleOwner()
        {
            var owner = m_Avatar.Ownership.Toggle();
            m_Avatar.SetHumanControlled(owner == MNG_InputOwner.Human);
            matchController.SetControlMask(m_Avatar.Team, m_Avatar.Slot, owner == MNG_InputOwner.Manager);
            GetComponent<MNG_PlayerSkillExecutor>()?.Cancel();
            m_WeakKickRequested = false;
            m_StrongKickRequested = false;
            return owner;
        }

        static float ApplyDeadZone(float value) => Mathf.Abs(value) < DeadZone ? 0f : Mathf.Clamp(value, -1f, 1f);
    }
}
