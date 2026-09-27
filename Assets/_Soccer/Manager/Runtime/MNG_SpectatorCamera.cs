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
        [Min(0.1f)] [SerializeField] float transitionSeconds = .8f;

        Camera m_Camera;
        Vector3 m_OverviewPosition;
        Quaternion m_OverviewRotation;
        float m_OverviewFieldOfView;
        bool m_OverviewCaptured;
        Vector3 m_TransitionPosition;
        Quaternion m_TransitionRotation;
        float m_TransitionFieldOfView, m_TransitionElapsed;

        public MNG_MatchController MatchController => matchController;
        public MNG_HumanInput HumanInput => humanInput;
        public bool IsFollowingHuman { get; private set; }
        public bool IsTransitioning { get; private set; }

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
            if (wasFollowingHuman != IsFollowingHuman)
            {
                m_TransitionPosition = transform.position;
                m_TransitionRotation = transform.rotation;
                m_TransitionFieldOfView = m_Camera.fieldOfView;
                m_TransitionElapsed = 0f;
                IsTransitioning = true;
            }
            var position = m_OverviewPosition;
            var rotation = m_OverviewRotation;
            var fieldOfView = m_OverviewFieldOfView;
            if (IsFollowingHuman)
            {
                var player = humanInput.transform;
                position = player.position - player.forward * followDistance + Vector3.up * followHeight;
                var lookTarget = player.position + Vector3.up * 1.1f + player.forward * lookAhead;
                rotation = Quaternion.LookRotation(lookTarget - position);
                fieldOfView = humanFieldOfView;
            }
            // Reversal starts at the current rendered pose, without snapping to either endpoint.
            if (IsTransitioning)
            {
                if (Time.timeScale > 0) m_TransitionElapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(m_TransitionElapsed / transitionSeconds);
                float eased = t * t * (3f - 2f * t);
                transform.SetPositionAndRotation(Vector3.Lerp(m_TransitionPosition, position, eased),
                    Quaternion.Slerp(m_TransitionRotation, rotation, eased));
                m_Camera.fieldOfView = Mathf.Lerp(m_TransitionFieldOfView, fieldOfView, eased);
                IsTransitioning = t < 1f;
            }
            else if (Time.timeScale > 0) MoveCamera(position, rotation, fieldOfView);
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
