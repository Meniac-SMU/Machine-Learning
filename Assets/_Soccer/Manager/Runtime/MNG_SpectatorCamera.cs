using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class MNG_SpectatorCamera : MonoBehaviour
    {
        [SerializeField] MNG_MatchController matchController;
        [SerializeField] MNG_HumanInput humanInput;
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

        public MNG_MatchController MatchController => matchController;
        public MNG_HumanInput HumanInput => humanInput;
        public bool IsFollowingHuman { get; private set; }

        void Awake()
        {
            m_Camera = GetComponent<Camera>();
            CaptureOverview();
        }

        void LateUpdate()
        {
            if (!m_OverviewCaptured) CaptureOverview();
            var wasFollowingHuman = IsFollowingHuman;
            IsFollowingHuman = humanInput != null && humanInput.IsHuman;
            if (IsFollowingHuman) FollowHuman(humanInput.transform);
            else if (wasFollowingHuman) SnapToOverview();
            else RestoreOverview();
        }

        public void Configure(MNG_MatchController configuredMatch, MNG_HumanInput configuredHuman)
        {
            matchController = configuredMatch ?? throw new ArgumentNullException(nameof(configuredMatch));
            humanInput = configuredHuman ?? throw new ArgumentNullException(nameof(configuredHuman));
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
            => MoveCamera(m_OverviewPosition, m_OverviewRotation, m_OverviewFieldOfView);

        void SnapToOverview()
        {
            transform.SetPositionAndRotation(m_OverviewPosition, m_OverviewRotation);
            m_Camera.fieldOfView = m_OverviewFieldOfView;
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
