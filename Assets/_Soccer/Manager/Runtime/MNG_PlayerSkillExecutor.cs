using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MNG_PlayerAvatar), typeof(MNG_PlayerMotor))]
    public sealed class MNG_PlayerSkillExecutor : MonoBehaviour
    {
        [SerializeField] MNG_BallControl ballControl;
        [SerializeField] MNG_MatchController matchController;

        MNG_PlayerAvatar m_Avatar;
        MNG_PlayerMotor m_Motor;
        MNG_PlayerTask m_Task;
        long m_LatestTaskRevision = -1;
        bool m_KickConsumed;

        public MNG_PlayerTask CurrentTask => m_Task;

        void Awake()
        {
            m_Avatar = GetComponent<MNG_PlayerAvatar>();
            m_Motor = GetComponent<MNG_PlayerMotor>();
            if (matchController == null) matchController = GetComponentInParent<MNG_MatchController>();
            if (ballControl == null && matchController != null)
                ballControl = matchController.GetComponentInChildren<MNG_BallControl>();
        }

        public void Configure(MNG_MatchController configuredMatch, MNG_BallControl configuredBallControl)
        {
            matchController = configuredMatch ?? throw new ArgumentNullException(nameof(configuredMatch));
            ballControl = configuredBallControl ?? throw new ArgumentNullException(nameof(configuredBallControl));
            m_Avatar = GetComponent<MNG_PlayerAvatar>();
            m_Motor = GetComponent<MNG_PlayerMotor>();
        }

        void FixedUpdate()
        {
            if (matchController == null || m_Motor == null || m_Avatar == null) return;
            var revision = m_Avatar.Ownership.Revision;
            if (!matchController.IsPlayActive
                || m_Task.Skill == MNG_PlayerSkill.None
                || matchController.EpisodeElapsedSeconds > m_Task.ExpirySeconds
                || !m_Avatar.Ownership.CanWrite(MNG_InputOwner.Manager, revision))
            {
                if (m_Avatar.Ownership.Owner == MNG_InputOwner.Manager)
                    m_Motor.Stop(MNG_InputOwner.Manager, revision, Time.fixedDeltaTime);
                return;
            }

            if (ballControl != null
                && ballControl.TryGetContestedEscapeTarget(
                    m_Avatar.Team, m_Avatar.Slot, out var escapeTarget))
            {
                MoveWhileRespectingRoleFacing(escapeTarget, revision);
                return;
            }

            switch (m_Task.Skill)
            {
                case MNG_PlayerSkill.AimPass:
                    var passOrigin = new Vector2(m_Avatar.Body.position.x, m_Avatar.Body.position.z);
                    AimAndKick(MNG_KickSolver.PassExitSpeedForDistance(
                        Vector2.Distance(passOrigin, m_Task.Target)));
                    break;
                case MNG_PlayerSkill.AimShot:
                    AimAndKick(MNG_KickSolver.StrongExitSpeed);
                    break;
                default:
                    MoveWhileRespectingRoleFacing(m_Task.Target, revision);
                    break;
            }
        }

        void MoveWhileRespectingRoleFacing(Vector2 target, long revision)
        {
            if (m_Avatar.Role == MNG_PlayerRole.Keeper)
            {
                m_Motor.ApplyMoveTarget(
                    target,
                    matchController.Snapshot.BallPosition,
                    MNG_InputOwner.Manager,
                    revision,
                    Time.fixedDeltaTime);
                return;
            }

            m_Motor.ApplyMoveTarget(
                target,
                MNG_InputOwner.Manager,
                revision,
                Time.fixedDeltaTime);
        }

        public bool SetTask(MNG_PlayerTask task)
        {
            if (task.Revision < m_LatestTaskRevision) return false;
            m_LatestTaskRevision = task.Revision;
            m_Task = task;
            m_KickConsumed = false;
            return true;
        }

        public void Cancel()
        {
            m_LatestTaskRevision++;
            m_Task = new MNG_PlayerTask
            {
                Skill = MNG_PlayerSkill.None,
                Revision = m_LatestTaskRevision,
                ExpirySeconds = matchController != null ? matchController.EpisodeElapsedSeconds : 0f
            };
            m_KickConsumed = true;
        }

        public void ResetForRound()
        {
            m_LatestTaskRevision = -1;
            m_Task = default;
            m_KickConsumed = false;
        }

        void AimAndKick(float exitSpeed)
        {
            var target = new Vector3(m_Task.Target.x, m_Avatar.Body.position.y, m_Task.Target.y);
            var direction = target - m_Avatar.Body.position;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f) return;
            var desired = new Vector2(direction.x, direction.z).normalized * 0.01f;
            m_Motor.ApplyDesiredVelocity(
                desired,
                MNG_InputOwner.Manager,
                m_Avatar.Ownership.Revision,
                Time.fixedDeltaTime);
            if (m_KickConsumed || Vector3.Angle(m_Avatar.transform.forward, direction) > 10f) return;
            if (ballControl != null && ballControl.TryKick(
                    m_Avatar.Team, m_Avatar.Slot, target, exitSpeed, out _))
                m_KickConsumed = true;
        }
    }
}
