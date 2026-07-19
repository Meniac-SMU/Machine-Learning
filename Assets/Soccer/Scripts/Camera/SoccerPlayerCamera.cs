using UnityEngine;

namespace MachineLearning.Soccer
{
    /// <summary>
    /// The H/Y Human-AI toggle selects either a player-follow camera or the
    /// original broadcast overview camera.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class SoccerPlayerCamera : MonoBehaviour
    {
        [SerializeField] SoccerEnvController environment;
        [Header("Human third-person view")]
        [Min(0.1f)] [SerializeField] float followDistance = 7.5f;
        [SerializeField] float followHeight = 3.4f;
        [SerializeField] float lookAhead = 2.6f;
        [Min(0.1f)] [SerializeField] float followSharpness = 11f;
        [SerializeField] float humanFieldOfView = 62f;

        Camera m_Camera;
        Vector3 m_OverviewPosition;
        Quaternion m_OverviewRotation;
        float m_OverviewFieldOfView;
        bool m_OverviewCaptured;

        public bool IsFollowingHuman { get; private set; }

        void Awake()
        {
            m_Camera = GetComponent<Camera>();
            CaptureOverview();
        }

        void LateUpdate()
        {
            if (!m_OverviewCaptured)
            {
                CaptureOverview();
            }

            var human = environment != null ? environment.HumanControlledAgent : null;
            IsFollowingHuman = environment != null && !environment.IsAIEnabled && human != null;
            if (IsFollowingHuman)
            {
                FollowHuman(human.transform);
            }
            else
            {
                RestoreOverview();
            }
        }

        public void Configure(SoccerEnvController configuredEnvironment)
        {
            environment = configuredEnvironment;
            CaptureOverview();
        }

        void CaptureOverview()
        {
            m_Camera ??= GetComponent<Camera>();
            m_OverviewPosition = transform.position;
            m_OverviewRotation = transform.rotation;
            m_OverviewFieldOfView = m_Camera.fieldOfView;
            m_OverviewCaptured = true;
        }

        void FollowHuman(Transform player)
        {
            var desiredPosition = player.position - player.forward * followDistance + Vector3.up * followHeight;
            var lookTarget = player.position + Vector3.up * 1.1f + player.forward * lookAhead;
            MoveCamera(desiredPosition, Quaternion.LookRotation(lookTarget - desiredPosition), humanFieldOfView);
        }

        void RestoreOverview()
        {
            MoveCamera(m_OverviewPosition, m_OverviewRotation, m_OverviewFieldOfView);
        }

        void MoveCamera(Vector3 position, Quaternion rotation, float fieldOfView)
        {
            var interpolation = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
            transform.SetPositionAndRotation(
                Vector3.Lerp(transform.position, position, interpolation),
                Quaternion.Slerp(transform.rotation, rotation, interpolation));
            m_Camera.fieldOfView = Mathf.Lerp(m_Camera.fieldOfView, fieldOfView, interpolation);
        }
    }
}
