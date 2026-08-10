using UnityEngine;

namespace MachineLearning.Soccer
{
    /// <summary>
    /// 선수 전면 킥 받침대의 전진·복귀 처리.
    /// 자식 Collider와 선수 Rigidbody의 복합 충돌체 구성.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SoccerKickPlate : MonoBehaviour
    {
        enum PlateState
        {
            Ready,
            Extending,
            Retracting
        }

        public const float PlayerHeightRatio = 0.3f;

        [SerializeField] AgentSoccer owner;
        [SerializeField] Vector3 retractedLocalPosition;
        [SerializeField] Vector3 extendedLocalPosition = new(0f, 0f, 0.68f);
        [SerializeField, Min(0.01f)] float extensionSeconds = 0.08f;
        [SerializeField, Min(0.01f)] float retractionSeconds = 0.5f;

        PlateState m_State = PlateState.Ready;
        float m_StateElapsed;
        bool m_StrikeConsumed;

        public AgentSoccer Owner => owner;
        public bool CanKick => isActiveAndEnabled && m_State == PlateState.Ready;
        public bool IsRetracted => m_State == PlateState.Ready;
        public bool IsStrikeActive => m_State == PlateState.Extending;
        public Vector3 RetractedLocalPosition => retractedLocalPosition;
        public Vector3 ExtendedLocalPosition => extendedLocalPosition;
        public float RetractionSeconds => retractionSeconds;

        void Awake()
        {
            owner ??= GetComponentInParent<AgentSoccer>();
            if (owner == null)
            {
                Debug.LogError("SoccerKickPlate requires an AgentSoccer owner.", this);
                enabled = false;
                return;
            }

            ResetPlate();
        }

        void FixedUpdate()
        {
            switch (m_State)
            {
                case PlateState.Extending:
                    m_StateElapsed += Time.fixedDeltaTime;
                    transform.localPosition = Vector3.Lerp(
                        retractedLocalPosition,
                        extendedLocalPosition,
                        Mathf.Clamp01(m_StateElapsed / extensionSeconds));
                    if (m_StateElapsed >= extensionSeconds)
                    {
                        SetState(PlateState.Retracting);
                    }

                    break;
                case PlateState.Retracting:
                    m_StateElapsed += Time.fixedDeltaTime;
                    transform.localPosition = Vector3.Lerp(
                        extendedLocalPosition,
                        retractedLocalPosition,
                        Mathf.Clamp01(m_StateElapsed / retractionSeconds));
                    if (m_StateElapsed >= retractionSeconds)
                    {
                        transform.localPosition = retractedLocalPosition;
                        SetState(PlateState.Ready);
                    }

                    break;
            }
        }

        public void Configure(AgentSoccer configuredOwner, Vector3 retractedPosition, Vector3 extendedPosition)
        {
            owner = configuredOwner;
            retractedLocalPosition = retractedPosition;
            extendedLocalPosition = extendedPosition;
            ResetPlate();
        }

        public bool TryKick()
        {
            if (!CanKick)
            {
                return false;
            }

            m_StrikeConsumed = false;
            SetState(PlateState.Extending);
            return true;
        }

        /// <summary>
        /// 한 번의 전진 동작에 한 번만 힘을 적용하기 위한 소비 판정.
        /// </summary>
        public bool TryConsumeStrike()
        {
            if (!IsStrikeActive || m_StrikeConsumed)
            {
                return false;
            }

            m_StrikeConsumed = true;
            return true;
        }

        public void ResetPlate()
        {
            transform.localPosition = retractedLocalPosition;
            m_StrikeConsumed = false;
            SetState(PlateState.Ready);
        }

        void SetState(PlateState nextState)
        {
            m_State = nextState;
            m_StateElapsed = 0f;
        }
    }
}
