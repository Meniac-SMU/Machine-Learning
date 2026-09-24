using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public enum MNG_KickIntent
    {
        Unspecified = 0,
        Pass = 1,
        Shot = 2
    }

    public readonly struct MNG_KickRequest
    {
        public readonly Vector3 Target;
        public readonly float ExitSpeed;
        public readonly MNG_KickIntent Intent;
        public readonly long TaskId;
        public readonly long ParentCommandId;
        public readonly int IntendedReceiverSlot;
        public readonly MNG_ActionSource Source;
        public readonly bool CommonRule;
        public readonly bool PassBuildRule;

        public MNG_KickRequest(Vector3 target, float exitSpeed)
            : this(target, exitSpeed, MNG_KickIntent.Unspecified)
        {
        }

        public MNG_KickRequest(Vector3 target, float exitSpeed, MNG_KickIntent intent,
            long taskId = 0, long parentCommandId = 0, int intendedReceiverSlot = -1,
            MNG_ActionSource source = MNG_ActionSource.PolicyCommand, bool commonRule = false, bool passBuildRule = false)
        {
            Target = target;
            ExitSpeed = exitSpeed;
            Intent = intent;
            TaskId = taskId;
            ParentCommandId = parentCommandId;
            IntendedReceiverSlot = intendedReceiverSlot;
            Source = source;
            CommonRule = commonRule;
            PassBuildRule = passBuildRule;
        }
    }

    /// <summary>
    /// MNG-owned physical kick plate. A kick request only arms and extends the
    /// plate; the ball applies the requested strike after real plate contact.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class MNG_KickPlate : MonoBehaviour
    {
        enum PlateState
        {
            Ready,
            Extending,
            Retracting
        }

        public const float DribbleAnchorForward = 1.32f;
        public const float DribbleCaptureRadius = 0.72f;
        public const float DribbleReleaseRadius = 1.02f;
        public const float MinimumForwardCommandSpeed = 0.40f;
        public const float MinimumForwardAlignment = 0.70f;

        [SerializeField] MNG_PlayerAvatar owner;
        [SerializeField] Vector3 retractedLocalPosition;
        [SerializeField] Vector3 extendedLocalPosition = new(0f, 0f, 0.68f);
        [SerializeField, Min(0.01f)] float extensionSeconds = 0.08f;
        [SerializeField, Min(0.01f)] float retractionSeconds = 0.50f;
        [SerializeField] Vector3 dribbleAnchorLocalPosition = new(0f, 0f, DribbleAnchorForward);

        PlateState m_State = PlateState.Ready;
        float m_StateElapsed;
        bool m_StrikeConsumed;
        MNG_KickRequest m_PendingKick;

        public MNG_PlayerAvatar Owner => owner != null ? owner : owner = GetComponentInParent<MNG_PlayerAvatar>();
        public bool CanKick => isActiveAndEnabled && m_State == PlateState.Ready;
        public bool IsRetracted => m_State == PlateState.Ready;
        public bool IsStrikeActive => m_State == PlateState.Extending;
        public bool HasPendingKick => IsStrikeActive && !m_StrikeConsumed;
        public Vector3 RetractedLocalPosition => retractedLocalPosition;
        public Vector3 ExtendedLocalPosition => extendedLocalPosition;
        public float ExtensionSeconds => extensionSeconds;
        public float RetractionSeconds => retractionSeconds;
        public float CommitmentRemainingSeconds => IsStrikeActive ? Mathf.Max(0f, extensionSeconds - m_StateElapsed) : 0f;
        public float CommitmentElapsedSeconds => IsStrikeActive ? m_StateElapsed : 0f;
        public Vector3 DribblePosition => ParentTransform.TransformPoint(
            retractedLocalPosition + dribbleAnchorLocalPosition);

        Transform ParentTransform => transform.parent != null ? transform.parent : transform;

        void Awake()
        {
            owner ??= GetComponentInParent<MNG_PlayerAvatar>();
            if (owner == null)
            {
                Debug.LogError("MNG_KickPlate requires an MNG_PlayerAvatar owner.", this);
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
                        SetState(PlateState.Retracting);
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
                        m_PendingKick = default;
                        SetState(PlateState.Ready);
                    }
                    break;
            }
        }

        public void Configure(
            MNG_PlayerAvatar configuredOwner,
            Vector3 configuredRetractedPosition,
            Vector3 configuredExtendedPosition)
        {
            owner = configuredOwner != null
                ? configuredOwner
                : throw new ArgumentNullException(nameof(configuredOwner));
            retractedLocalPosition = configuredRetractedPosition;
            extendedLocalPosition = configuredExtendedPosition;
            ResetPlate();
        }

        public bool TryArmKick(
            Vector3 target,
            float exitSpeed,
            MNG_KickIntent intent = MNG_KickIntent.Unspecified,
            MNG_PlayerTask task = default)
        {
            if (!CanKick || !IsFinite(target)
                || !MNG_MatchSnapshot.IsFinite(exitSpeed) || exitSpeed <= 0f)
                return false;

            m_PendingKick = new MNG_KickRequest(target, exitSpeed, intent,
                task.TaskId, task.ParentCommandId,
                intent == MNG_KickIntent.Pass && task.TaskId > 0 ? task.ReceiverSlot : -1, task.Source, task.CommonRule, task.PassBuildRule);
            m_StrikeConsumed = false;
            SetState(PlateState.Extending);
            return true;
        }

        public bool TryConsumeStrike(Collider contactCollider, out MNG_KickRequest request)
        {
            request = default;
            if (!HasPendingKick || !OwnsCollider(contactCollider)) return false;
            m_StrikeConsumed = true;
            request = m_PendingKick;
            return true;
        }

        public bool OwnsCollider(Collider collider)
            => collider != null && (collider.transform == transform || collider.transform.IsChildOf(transform));

        public float DistanceToDribblePosition(Vector3 ballPosition)
        {
            var delta = ballPosition - DribblePosition;
            delta.y = 0f;
            return delta.magnitude;
        }

        public bool IsAtDribblePosition(Vector3 ballPosition)
            => DistanceToDribblePosition(ballPosition) <= DribbleCaptureRadius;

        public bool IsWithinReleasePosition(Vector3 ballPosition)
            => DistanceToDribblePosition(ballPosition) <= DribbleReleaseRadius;

        public bool IsForwardDriveActive()
        {
            var motor = Owner != null ? Owner.GetComponent<MNG_PlayerMotor>() : null;
            if (motor == null) return false;
            var forward = new Vector2(Owner.transform.forward.x, Owner.transform.forward.z);
            return IsForwardCommand(motor.LastDesiredVelocity, forward);
        }

        public void ResetPlate()
        {
            transform.localPosition = retractedLocalPosition;
            m_PendingKick = default;
            m_StrikeConsumed = false;
            SetState(PlateState.Ready);
        }

        public static bool IsForwardCommand(Vector2 desiredVelocity, Vector2 forward)
        {
            if (desiredVelocity.magnitude < MinimumForwardCommandSpeed
                || forward.sqrMagnitude <= 0.0001f)
                return false;
            return Vector2.Dot(desiredVelocity.normalized, forward.normalized)
                >= MinimumForwardAlignment;
        }

        static bool IsFinite(Vector3 value) => MNG_MatchSnapshot.IsFinite(value.x)
            && MNG_MatchSnapshot.IsFinite(value.y)
            && MNG_MatchSnapshot.IsFinite(value.z);

        void SetState(PlateState next)
        {
            m_State = next;
            m_StateElapsed = 0f;
        }
    }
}
