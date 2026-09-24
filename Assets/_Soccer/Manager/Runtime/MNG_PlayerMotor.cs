using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(MNG_PlayerAvatar))]
    public sealed class MNG_PlayerMotor : MonoBehaviour
    {
        [SerializeField] MNG_PhysicsProfile profile;
        MNG_PlayerAvatar m_Avatar;
        Rigidbody m_Body;
        Vector2 m_LastDesiredVelocity;
        float m_SpeedMultiplier = 1f;

        public MNG_PhysicsProfile Profile => profile;
        public Vector2 LastDesiredVelocity => m_LastDesiredVelocity;
        public float SpeedMultiplier => m_SpeedMultiplier;
        // Tactical requested pace; the physical speed/acceleration profile remains unchanged.
        public float ManagerPace { get; set; } = 1f;

        void Awake()
        {
            m_Avatar = GetComponent<MNG_PlayerAvatar>();
            m_Body = GetComponent<Rigidbody>();
            if (profile == null) throw new InvalidOperationException("MNG player motor requires a physics profile.");
            profile.ValidateOrThrow();
            MNG_PhysicsProfile.ConfigureContactSolver(m_Body);
        }

        public void Configure(MNG_PhysicsProfile configuredProfile)
        {
            profile = configuredProfile != null
                ? configuredProfile
                : throw new ArgumentNullException(nameof(configuredProfile));
            profile.ValidateOrThrow();
            m_Avatar = GetComponent<MNG_PlayerAvatar>();
            m_Body = GetComponent<Rigidbody>();
            MNG_PhysicsProfile.ConfigureContactSolver(m_Body);
        }

        public bool ApplyMoveTarget(
            Vector2 target,
            MNG_InputOwner requester,
            long ownershipRevision,
            float deltaTime)
            => ApplyMoveTarget(
                target,
                target,
                requester,
                ownershipRevision,
                deltaTime);

        public bool ApplyMoveTarget(
            Vector2 target,
            Vector2 facingTarget,
            MNG_InputOwner requester,
            long ownershipRevision,
            float deltaTime)
        {
            if (m_Body == null) m_Body = GetComponent<Rigidbody>();
            var position = new Vector2(m_Body.position.x, m_Body.position.z);
            var offset = target - position;
            var distance = offset.magnitude;
            var speed = profile.MaximumPlayerSpeed * m_SpeedMultiplier
                * (requester == MNG_InputOwner.Manager ? Mathf.Clamp01(ManagerPace) : 1f)
                * Mathf.Clamp01(distance / profile.ArrivalSlowRadius);
            var desired = distance > 0.0001f ? offset / distance * speed : Vector2.zero;
            return ApplyDesiredVelocity(
                desired,
                facingTarget - position,
                requester,
                ownershipRevision,
                deltaTime);
        }

        public bool ApplyDesiredVelocity(
            Vector2 desiredVelocity,
            MNG_InputOwner requester,
            long ownershipRevision,
            float deltaTime)
            => ApplyDesiredVelocity(
                desiredVelocity,
                desiredVelocity,
                requester,
                ownershipRevision,
                deltaTime);

        public bool ApplyDesiredVelocity(
            Vector2 desiredVelocity,
            Vector2 facingDirection,
            MNG_InputOwner requester,
            long ownershipRevision,
            float deltaTime)
        {
            if (m_Avatar == null) m_Avatar = GetComponent<MNG_PlayerAvatar>();
            if (m_Body == null) m_Body = GetComponent<Rigidbody>();
            if (!m_Avatar.Ownership.CanWrite(requester, ownershipRevision)) return false;
            if (!MNG_MatchSnapshot.IsFinite(desiredVelocity.x)
                || !MNG_MatchSnapshot.IsFinite(desiredVelocity.y)
                || !MNG_MatchSnapshot.IsFinite(facingDirection.x)
                || !MNG_MatchSnapshot.IsFinite(facingDirection.y)
                || !MNG_MatchSnapshot.IsFinite(deltaTime)
                || deltaTime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(desiredVelocity));

            desiredVelocity = Vector2.ClampMagnitude(
                desiredVelocity,
                profile.MaximumPlayerSpeed * m_SpeedMultiplier);
            m_LastDesiredVelocity = desiredVelocity;
            var current = new Vector2(m_Body.linearVelocity.x, m_Body.linearVelocity.z);
            var decelerating = desiredVelocity.sqrMagnitude < current.sqrMagnitude
                || Vector2.Dot(current, desiredVelocity) < 0f;
            var rate = decelerating ? profile.Deceleration : profile.Acceleration;
            var next = Vector2.MoveTowards(current, desiredVelocity, rate * deltaTime);
            m_Body.linearVelocity = new Vector3(next.x, m_Body.linearVelocity.y, next.y);

            if (facingDirection.sqrMagnitude > 0.0001f)
            {
                var facing = Quaternion.LookRotation(
                    new Vector3(facingDirection.x, 0f, facingDirection.y),
                    Vector3.up);
                m_Body.MoveRotation(Quaternion.RotateTowards(
                    m_Body.rotation,
                    facing,
                    profile.RotationDegreesPerSecond * deltaTime));
            }
            return true;
        }

        public bool Stop(MNG_InputOwner requester, long ownershipRevision, float deltaTime)
            => ApplyDesiredVelocity(Vector2.zero, requester, ownershipRevision, deltaTime);

        public void ResetDriveCommand() => m_LastDesiredVelocity = Vector2.zero;

        public void ConfigureSpeedMultiplier(float multiplier)
        {
            if (!MNG_MatchSnapshot.IsFinite(multiplier) || multiplier <= 0f || multiplier > 1f)
                throw new ArgumentOutOfRangeException(nameof(multiplier));
            m_SpeedMultiplier = multiplier;
        }

        public static Vector2 CalculateNextVelocity(
            Vector2 current,
            Vector2 desired,
            float maximumSpeed,
            float acceleration,
            float deceleration,
            float deltaTime)
        {
            desired = Vector2.ClampMagnitude(desired, maximumSpeed);
            var rate = desired.sqrMagnitude < current.sqrMagnitude || Vector2.Dot(current, desired) < 0f
                ? deceleration
                : acceleration;
            return Vector2.MoveTowards(current, desired, rate * deltaTime);
        }
    }
}
