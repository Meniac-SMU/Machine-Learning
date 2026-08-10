using UnityEngine;

namespace MachineLearning.Escape
{
    [RequireComponent(typeof(Rigidbody))]
    public sealed class EscapeAgentMotor : MonoBehaviour
    {
        [SerializeField] float moveSpeed = 5f;
        [SerializeField] float turnSpeed = 160f;

        Rigidbody m_Rigidbody;
        float m_MoveInput;
        float m_TurnInput;

        public Rigidbody Body => m_Rigidbody;
        public float MoveSpeed => moveSpeed;
        public float TurnSpeed => turnSpeed;
        public float NormalizedForwardSpeed => m_Rigidbody == null || moveSpeed <= 0f
            ? 0f
            : Mathf.Clamp(Vector3.Dot(m_Rigidbody.linearVelocity, transform.forward) / moveSpeed, -1f, 1f);
        public float NormalizedTurnSpeed => m_Rigidbody == null || turnSpeed <= 0f
            ? 0f
            : Mathf.Clamp(m_Rigidbody.angularVelocity.y / (turnSpeed * Mathf.Deg2Rad), -1f, 1f);

        void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
        }

        void FixedUpdate()
        {
            var delta = Time.fixedDeltaTime;
            var nextPosition = m_Rigidbody.position + transform.forward * (m_MoveInput * moveSpeed * delta);
            var turn = Quaternion.Euler(0f, m_TurnInput * turnSpeed * delta, 0f);
            m_Rigidbody.MovePosition(nextPosition);
            m_Rigidbody.MoveRotation(m_Rigidbody.rotation * turn);
        }

        public void SetInput(float move, float turn)
        {
            m_MoveInput = Mathf.Clamp(move, -1f, 1f);
            m_TurnInput = Mathf.Clamp(turn, -1f, 1f);
        }

        public void Stop()
        {
            m_MoveInput = 0f;
            m_TurnInput = 0f;
            if (m_Rigidbody == null)
            {
                return;
            }

            m_Rigidbody.linearVelocity = Vector3.zero;
            m_Rigidbody.angularVelocity = Vector3.zero;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            if (m_Rigidbody == null)
            {
                m_Rigidbody = GetComponent<Rigidbody>();
            }

            m_Rigidbody.position = position;
            m_Rigidbody.rotation = rotation;
            Stop();
        }

        public void Configure(float speed, float degreesPerSecond)
        {
            moveSpeed = speed;
            turnSpeed = degreesPerSecond;
        }
    }
}
