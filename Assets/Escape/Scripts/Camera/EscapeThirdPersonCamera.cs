using UnityEngine;
using UnityEngine.InputSystem;

namespace MachineLearning.Escape
{
    [RequireComponent(typeof(Camera))]
    public sealed class EscapeThirdPersonCamera : MonoBehaviour
    {
        [SerializeField] EscapePlayerAgent target;
        [SerializeField] float distance = 6f;
        [SerializeField] float pivotHeight = 3f;
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] float minimumPitch = -20f;
        [SerializeField] float maximumPitch = 65f;
        [SerializeField] float collisionRadius = 0.25f;

        float m_Yaw;
        float m_Pitch = 18f;

        void Start()
        {
            if (Application.isBatchMode)
            {
                gameObject.SetActive(false);
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            UpdateCursorState();
            if (target.ControlMode == EscapePlayerControlMode.Human && Cursor.lockState == CursorLockMode.Locked)
            {
                var mouse = Mouse.current;
                if (mouse != null)
                {
                    var delta = mouse.delta.ReadValue() * mouseSensitivity;
                    m_Yaw += delta.x;
                    m_Pitch = Mathf.Clamp(m_Pitch - delta.y, minimumPitch, maximumPitch);
                }
            }

            var pivot = target.transform.position + Vector3.up * pivotHeight;
            var rotation = Quaternion.Euler(m_Pitch, m_Yaw, 0f);
            var direction = rotation * Vector3.back;
            var actualDistance = distance;
            if (Physics.SphereCast(pivot, collisionRadius, direction, out var hit, distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                actualDistance = Mathf.Max(0.5f, hit.distance - 0.15f);
            }

            transform.SetPositionAndRotation(pivot + direction * actualDistance, rotation);
        }

        public void Configure(EscapePlayerAgent configuredTarget)
        {
            target = configuredTarget;
        }

        static void UpdateCursorState()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
}
