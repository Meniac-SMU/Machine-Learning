using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MNG_PlayerAvatar), typeof(MNG_PlayerMotor))]
    public sealed class MNG_PlayerSkillExecutor : MonoBehaviour
    {
        public const float BallPushSetupDistance = 1.25f;
        public const float BallOrbitRadius = 1.65f;
        public const float BallBehindLongitudinalTolerance = 0.70f;
        public const float BallBehindLateralTolerance = 0.90f;
        public const float FieldPlayerPersonalSpaceRadius = 7f;
        public const float MaximumFieldPlayerSpacingOffset = 10f;
        public const float FieldPlayerSpacingStrength = 2.5f;
        public const float EmergencySeparationEnterDistance = 4.25f;
        public const float EmergencySeparationExitDistance = 5.25f;
        public const float EmergencySeparationSpeedFraction = 0.85f;
        public const float EmergencySeparationHoldSeconds = 0.30f;
        const float BallTaskTolerance = 2.25f;
        const float BallSetupArrivalDistance = 0.42f;
        const float ContinueCarryFacingDot = 0.65f;

        [SerializeField] MNG_BallControl ballControl;
        [SerializeField] MNG_MatchController matchController;

        MNG_PlayerAvatar m_Avatar;
        MNG_PlayerMotor m_Motor;
        MNG_PlayerTask m_Task;
        long m_LatestTaskRevision = -1;
        bool m_KickConsumed;
        bool m_RoutingBehindBall;
        bool m_PushingFromBehind;
        Vector2 m_PushDestination;
        int m_EmergencySeparationPartnerSlot = -1;
        float m_EmergencySeparationRemainingSeconds;
        Vector2 m_EmergencySeparationDirection;
        long m_ReceivingTaskId = -1;
        Vector2 m_ReceiveApproach;

        public MNG_PlayerTask CurrentTask => m_Task;
        readonly MNG_TaskLifetime m_Lifetime = new MNG_TaskLifetime();
        readonly MNG_PlayerTask[] m_DeferredRevalidation = new MNG_PlayerTask[4];
        MNG_ActionSource m_LastExecutionSource;
        bool m_HasExecutionSource;
        void ReportExecutionSource(MNG_ActionSource source)
        {
            if (!IsV2) return;
            var trace = matchController.GetComponent<MNG_V2Trace>();
            trace?.RecordExecutionSource(m_Avatar.Team, m_Avatar.Slot, m_Task, source,
                !m_HasExecutionSource || m_LastExecutionSource != source);
            m_LastExecutionSource = source; m_HasExecutionSource = true;
        }
        bool IsV2 => matchController != null && matchController.UseRuntimeV2;

        public MNG_TaskResult RequestTaskV2(MNG_PlayerTask task)
        {
            var carrier = ballControl != null ? ballControl.Carrier : MNG_CarrierRef.None;
            var isOwner = m_Avatar != null && m_Avatar.Ownership.CanWrite(MNG_InputOwner.Manager, m_Avatar.Ownership.Revision);
            var valid = matchController.IsPlayActive && !task.StateChanged && (!IsKickSkill(task.Skill)
                || (carrier.IsValid && carrier.Team == m_Avatar.Team && carrier.Slot == m_Avatar.Slot));
            var result = m_Lifetime.Request(task, matchController.Snapshot.TickId,
                matchController.EpisodeElapsedSeconds, isOwner, valid || m_Avatar.KickPlate.IsStrikeActive, m_Avatar.KickPlate.IsStrikeActive);
            m_Task = m_Lifetime.Current;
            m_KickConsumed = m_Lifetime.KickConsumed;
            matchController.RecordTaskResult(m_Avatar.Team, m_Avatar.Slot, result);
            if (result.Kind == MNG_TaskResultKind.RejectedInvalidState && !m_Avatar.KickPlate.IsStrikeActive)
                CancelV2(matchController.IsPlayActive
                    ? MNG_TaskResultKind.CancelledPossessionLost : MNG_TaskResultKind.CancelledMatchState);
            return result;
        }

        public void CaptureExecution(ref MNG_PlayerState state)
        {
            // Match Awake captures before this executor's later execution-order Awake.
            m_Avatar ??= GetComponent<MNG_PlayerAvatar>();
            matchController ??= GetComponentInParent<MNG_MatchController>();
            var plate = m_Avatar.KickPlate;
            var active = m_Task.Skill != MNG_PlayerSkill.None && matchController.EpisodeElapsedSeconds < m_Task.ExpirySeconds;
            state.ExecutingSkill = active ? m_Task.Skill : MNG_PlayerSkill.None;
            state.ExecutingPhase = plate.IsStrikeActive ? MNG_TaskPhase.Committed
                : !plate.IsRetracted ? MNG_TaskPhase.Recovering
                : active && IsKickSkill(m_Task.Skill) && !m_KickConsumed ? MNG_TaskPhase.Preparing
                : active && m_Task.Skill == MNG_PlayerSkill.ReceivePass ? MNG_TaskPhase.Receiving : MNG_TaskPhase.Idle;
            state.TaskRemainingSeconds = active ? Mathf.Max(0, m_Task.ExpirySeconds - matchController.EpisodeElapsedSeconds) : 0;
            state.CommitmentRemainingSeconds = plate.CommitmentRemainingSeconds;
            state.ExecutingReceiverIndex = active && m_Task.ReceiverSlot >= 0 ? m_Task.ReceiverSlot + 1 : 0;
            state.ExecutingTarget = active ? m_Task.Target : Vector2.zero;
        }

        void CancelV2(MNG_TaskResultKind reason)
        {
            if (m_Task.Skill != MNG_PlayerSkill.None || m_Lifetime.HasDeferred)
                matchController.RecordTaskResult(m_Avatar.Team, m_Avatar.Slot,
                    m_Lifetime.Cancel(reason, matchController.Snapshot.TickId));
            m_Task = m_Lifetime.Current;
            m_KickConsumed = true;
            ResetFrontPushState();
        }

        public void CancelPassBuild(long parentCommandId)
        {
            if (!m_Task.PassBuildRule || m_Task.ParentCommandId != parentCommandId) return;
            if (m_Task.Skill == MNG_PlayerSkill.AimPass) m_Avatar.KickPlate.ResetPlate();
            if (IsV2) CancelV2(MNG_TaskResultKind.CancelledMatchState);
            else { m_Task = default; m_KickConsumed = true; }
        }

        void UpdateLifetimeV2()
        {
            var plate = m_Avatar.KickPlate;
            if (plate.IsStrikeActive && (plate.ExtensionSeconds > MNG_RuntimeV2.CommitmentWatchdogSeconds
                || plate.CommitmentElapsedSeconds > MNG_RuntimeV2.CommitmentWatchdogSeconds))
                throw new InvalidOperationException("MNG commitment watchdog exceeded.");
            if (!matchController.IsPlayActive || m_Avatar.IsHuman)
            {
                CancelV2(MNG_TaskResultKind.CancelledMatchState);
                plate.ResetPlate();
                return;
            }
            if (plate.IsStrikeActive) return;
            if (m_Lifetime.TakeDeferred(out var pending))
            {
                if (pending.CommonRule || pending.PassBuildRule) RequestTaskV2(pending);
                else
                {
                var decision = matchController.GetDecisionState(m_Avatar.Team);
                MNG_TeamPlanner.PlanV2(matchController.Snapshot, m_Avatar.Team, decision.PreviousCommand,
                    decision, pending.Revision, pending.ExpirySeconds, m_DeferredRevalidation);
                var recalculated = m_DeferredRevalidation[m_Avatar.Slot];
                recalculated.TaskId = pending.TaskId; recalculated.ParentCommandId = pending.ParentCommandId;
                recalculated.Source = pending.Source; recalculated.ExpirySeconds = pending.ExpirySeconds;
                RequestTaskV2(recalculated);
                }
            }
            var carrier = ballControl != null ? ballControl.Carrier : MNG_CarrierRef.None;
            if (m_Task.Skill == MNG_PlayerSkill.None) return;
            if (matchController.EpisodeElapsedSeconds >= m_Task.ExpirySeconds)
                CancelV2(MNG_TaskResultKind.Expired);
            else if (IsKickSkill(m_Task.Skill) && !m_KickConsumed
                && (!carrier.IsValid || carrier.Team != m_Avatar.Team || carrier.Slot != m_Avatar.Slot))
                CancelV2(MNG_TaskResultKind.CancelledPossessionLost);
            else if (m_Task.Skill == MNG_PlayerSkill.ReceivePass && carrier.IsValid
                && carrier.Team != m_Avatar.Team)
                CancelV2(MNG_TaskResultKind.CancelledPossessionLost);
        }

        void Awake()
        {
            m_Avatar = GetComponent<MNG_PlayerAvatar>();
            m_Motor = GetComponent<MNG_PlayerMotor>();
            if (matchController == null) matchController = GetComponentInParent<MNG_MatchController>();
            if (ballControl == null && matchController != null)
                ballControl = matchController.GetComponentInChildren<MNG_BallControl>();
        }

        void OnDisable()
        {
            if (ballControl != null) ballControl.PlateStrikeApplied -= OnPassFlightStarted;
            if (IsV2 && m_Avatar != null) Cancel();
        }

        void OnEnable()
        {
            if (ballControl != null) ballControl.PlateStrikeApplied += OnPassFlightStarted;
        }

        void OnPassFlightStarted(MNG_PlateStrikeEvent strike)
        {
            if (!IsV2 || !matchController.IsPlayActive || m_Avatar.IsHuman
                || strike.Intent != MNG_KickIntent.Pass || strike.Team != m_Avatar.Team
                || strike.IntendedReceiverSlot != m_Avatar.Slot
                || m_Task.Skill != MNG_PlayerSkill.ReceivePass
                || m_Task.ParentCommandId != strike.ParentCommandId
                || m_Task.ExpirySeconds <= matchController.EpisodeElapsedSeconds) return;
            // A real strike starts the existing two-second reception budget. This
            // refresh does not commit the receiver or reinstate a replaced command.
            var reception = m_Task;
            reception.ExpirySeconds = matchController.EpisodeElapsedSeconds + MNG_TeamPlanner.KickExecutionSeconds;
            RequestTaskV2(reception);
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
            if (IsV2) UpdateLifetimeV2();
            if (IsV2)
            {
                var command = matchController.GetDecisionState(m_Avatar.Team).PreviousCommand;
                m_Motor.ManagerPace = m_Task.Skill == MNG_PlayerSkill.Carry
                    ? command == MNG_Command.ProtectBack ? 0.3f : command == MNG_Command.Balanced ? 0.6f : 1f : 1f;
            }
            var revision = m_Avatar.Ownership.Revision;
            if (!matchController.IsPlayActive
                || !m_Avatar.Ownership.CanWrite(MNG_InputOwner.Manager, revision))
            {
                if (m_Avatar.Ownership.Owner == MNG_InputOwner.Manager)
                    m_Motor.Stop(MNG_InputOwner.Manager, revision, Time.fixedDeltaTime);
                return;
            }

            if (ballControl != null
                && ballControl.TryGetBoundaryEscapePlan(
                    m_Avatar.Team,
                    m_Avatar.Slot,
                    out var retreatingFromBoundary,
                    out var boundaryTarget))
            {
                ResetFrontPushState();
                ReportExecutionSource(MNG_ActionSource.SafetyEscape);
                if (retreatingFromBoundary || IsV2)
                {
                    m_KickConsumed = false;
                    MoveWhileRespectingRoleFacing(boundaryTarget, revision);
                }
                else
                {
                    AimAndKick(
                        boundaryTarget,
                        MNG_KickSolver.StrongExitSpeed * 0.85f,
                        MNG_KickIntent.Unspecified);
                }
                return;
            }

            if (ballControl != null
                && ballControl.TryGetContestedEscapeTarget(
                    m_Avatar.Team, m_Avatar.Slot, out var escapeTarget))
            {
                ReportExecutionSource(MNG_ActionSource.SafetyEscape);
                MoveWhileRespectingRoleFacing(escapeTarget, revision);
                return;
            }

            if (TryExecuteEmergencyTeamSeparation(revision))
            { ReportExecutionSource(MNG_ActionSource.SafetyEscape); return; }
            ReportExecutionSource(m_Task.Source);

            if (m_Task.Skill == MNG_PlayerSkill.None
                || matchController.EpisodeElapsedSeconds > m_Task.ExpirySeconds)
            {
                m_Motor.Stop(MNG_InputOwner.Manager, revision, Time.fixedDeltaTime);
                return;
            }

            switch (m_Task.Skill)
            {
                case MNG_PlayerSkill.ReceivePass:
                    // Meet the pass with the plate facing the ball. A receiving
                    // task must not orbit behind it as if it were a carry task.
                    var receiveBall = matchController.Snapshot.BallPosition;
                    var receivingId = IsV2 ? m_Task.TaskId : m_Task.Revision;
                    if (m_ReceivingTaskId != receivingId)
                    { m_ReceivingTaskId = receivingId; m_ReceiveApproach = (m_Task.Target - receiveBall).normalized; }
                    var receivePosition = m_Task.Target + m_ReceiveApproach * MNG_KickPlate.DribbleAnchorForward;
                    ResetFrontPushState();
                    var received = ballControl.Carrier;
                    if (received.IsValid && received.Team == m_Avatar.Team && received.Slot == m_Avatar.Slot)
                    {
                        var facing = new Vector2(m_Avatar.transform.forward.x, m_Avatar.transform.forward.z);
                        m_Motor.ApplyDesiredVelocity(facing * .5f, facing, MNG_InputOwner.Manager, revision, Time.fixedDeltaTime);
                        break;
                    }
                    m_Motor.ApplyMoveTarget(receivePosition, receiveBall,
                        MNG_InputOwner.Manager, revision, Time.fixedDeltaTime);
                    break;
                case MNG_PlayerSkill.AimPass:
                    var passOrigin = new Vector2(m_Avatar.Body.position.x, m_Avatar.Body.position.z);
                    AimAndKick(
                        m_Task.Target,
                        MNG_KickSolver.PassExitSpeedForDistance(
                            Vector2.Distance(passOrigin, m_Task.Target)),
                        MNG_KickIntent.Pass);
                    break;
                case MNG_PlayerSkill.AimShot:
                    AimAndKick(
                        m_Task.Target,
                        MNG_KickSolver.StrongExitSpeed,
                        MNG_KickIntent.Shot);
                    break;
                default:
                    if (!TryExecuteFrontPush(revision))
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

            var playerPosition = new Vector2(
                m_Avatar.Body.position.x,
                m_Avatar.Body.position.z);
            target = CalculateFieldPlayerSpacingTarget(
                matchController.Snapshot,
                m_Avatar.Team,
                m_Avatar.Slot,
                playerPosition,
                target);
            m_Motor.ApplyMoveTarget(
                target,
                MNG_InputOwner.Manager,
                revision,
                Time.fixedDeltaTime);
        }

        public static Vector2 CalculateFieldPlayerSpacingTarget(
            MNG_MatchSnapshot snapshot,
            MachineLearning.Soccer.Team team,
            int slot,
            Vector2 playerPosition,
            Vector2 target)
        {
            if (snapshot == null || slot <= 0 || slot >= MNG_MatchSnapshot.PlayersPerTeam)
                return target;

            var separation = Vector2.zero;
            for (var teammateSlot = 1;
                 teammateSlot < MNG_MatchSnapshot.PlayersPerTeam;
                 teammateSlot++)
            {
                if (teammateSlot == slot) continue;
                var teammate = snapshot.GetPlayer(team, teammateSlot);
                if (!teammate.Active) continue;
                var away = playerPosition - teammate.Position;
                var distance = away.magnitude;
                if (distance >= FieldPlayerPersonalSpaceRadius) continue;
                if (distance <= 0.001f)
                    away = new Vector2(0f, (team == MachineLearning.Soccer.Team.Red ? 1f : -1f) * (slot < teammateSlot ? -1f : 1f));
                else
                    away /= distance;
                separation += away * (FieldPlayerPersonalSpaceRadius - distance);
            }

            separation = Vector2.ClampMagnitude(
                separation * FieldPlayerSpacingStrength,
                MaximumFieldPlayerSpacingOffset);
            var spaced = target + separation;
            return new Vector2(
                Mathf.Clamp(spaced.x,
                    -snapshot.FieldHalfLength + 1f,
                    snapshot.FieldHalfLength - 1f),
                Mathf.Clamp(spaced.y,
                    -snapshot.FieldHalfWidth + 1f,
                    snapshot.FieldHalfWidth - 1f));
        }

        bool TryExecuteEmergencyTeamSeparation(long revision)
        {
            if (m_Avatar.Role == MNG_PlayerRole.Keeper) return false;

            var carrier = ballControl != null
                ? ballControl.Carrier
                : MNG_CarrierRef.None;
            var selfIsCarrier = carrier.IsValid
                && carrier.Team == m_Avatar.Team
                && carrier.Slot == m_Avatar.Slot;
            var selfPosition = new Vector2(
                m_Avatar.Body.position.x,
                m_Avatar.Body.position.z);
            var selectedPartner = -1;
            var selectedDirection = Vector2.zero;
            var greatestUrgency = float.NegativeInfinity;

            for (var teammateSlot = 1;
                 teammateSlot < MNG_MatchSnapshot.PlayersPerTeam;
                 teammateSlot++)
            {
                if (teammateSlot == m_Avatar.Slot) continue;
                var teammate = matchController.GetPlayerAvatar(
                    m_Avatar.Team, teammateSlot);
                if (!teammate.gameObject.activeInHierarchy) continue;

                var teammatePosition = new Vector2(
                    teammate.Body.position.x,
                    teammate.Body.position.z);
                var distance = Vector2.Distance(selfPosition, teammatePosition);
                if (distance >= EmergencySeparationEnterDistance) continue;

                var teammateExecutor = teammate.GetComponent<MNG_PlayerSkillExecutor>();
                var teammateSkill = teammateExecutor != null
                    ? teammateExecutor.CurrentTask.Skill
                    : MNG_PlayerSkill.None;
                var teammateIsCarrier = carrier.IsValid
                    && carrier.Team == m_Avatar.Team
                    && carrier.Slot == teammateSlot;
                var teammateHasForcedBallTask = ballControl != null
                    && (ballControl.TryGetBoundaryEscapePlan(
                            m_Avatar.Team, teammateSlot, out _, out _)
                        || ballControl.TryGetContestedEscapeTarget(
                            m_Avatar.Team, teammateSlot, out _));
                if (!ShouldYieldForEmergencySeparation(
                        m_Avatar.Slot,
                        m_Task.Skill,
                        selfIsCarrier,
                        teammateSlot,
                        teammateSkill,
                        teammateIsCarrier,
                        teammate.IsHuman || teammateHasForcedBallTask))
                    continue;

                var urgency = EmergencySeparationEnterDistance - distance;
                if (urgency <= greatestUrgency) continue;
                greatestUrgency = urgency;
                selectedPartner = teammateSlot;
                selectedDirection = CalculateEmergencySeparationDirection(
                    selfPosition,
                    teammatePosition,
                    m_Avatar.Slot,
                    teammateSlot, m_Avatar.Team);
            }

            if (selectedPartner >= 0)
            {
                m_EmergencySeparationPartnerSlot = selectedPartner;
                m_EmergencySeparationDirection = selectedDirection;
                m_EmergencySeparationRemainingSeconds = EmergencySeparationHoldSeconds;
            }
            else if (m_EmergencySeparationRemainingSeconds > 0f
                && m_EmergencySeparationPartnerSlot >= 1)
            {
                var partner = matchController.GetPlayerAvatar(
                    m_Avatar.Team, m_EmergencySeparationPartnerSlot);
                var partnerPosition = new Vector2(
                    partner.Body.position.x,
                    partner.Body.position.z);
                var distance = Vector2.Distance(selfPosition, partnerPosition);
                if (!partner.gameObject.activeInHierarchy
                    || distance >= EmergencySeparationExitDistance)
                {
                    ResetEmergencySeparationState();
                    return false;
                }
                m_EmergencySeparationDirection = CalculateEmergencySeparationDirection(
                    selfPosition,
                    partnerPosition,
                    m_Avatar.Slot,
                    m_EmergencySeparationPartnerSlot, m_Avatar.Team);
                m_EmergencySeparationRemainingSeconds = Mathf.Max(
                    0f,
                    m_EmergencySeparationRemainingSeconds - Time.fixedDeltaTime);
            }
            else
            {
                ResetEmergencySeparationState();
                return false;
            }

            ResetFrontPushState();
            var facing = matchController.Snapshot.BallPosition - selfPosition;
            m_EmergencySeparationDirection = ConstrainSeparationDirection(selfPosition,
                matchController.Snapshot.GetPlayer(m_Avatar.Team, m_EmergencySeparationPartnerSlot).Velocity,
                m_EmergencySeparationDirection, matchController.Snapshot.FieldHalfLength, matchController.Snapshot.FieldHalfWidth);
            m_Motor.ApplyDesiredVelocity(
                m_EmergencySeparationDirection
                    * m_Motor.Profile.MaximumPlayerSpeed
                    * EmergencySeparationSpeedFraction,
                facing,
                MNG_InputOwner.Manager,
                revision,
                Time.fixedDeltaTime);
            return true;
        }

        public static Vector2 ConstrainSeparationDirection(Vector2 position, Vector2 partnerVelocity,
            Vector2 direction, float halfLength, float halfWidth)
        {
            var projected = position + direction * EmergencySeparationExitDistance;
            var blocksX = Mathf.Abs(projected.x) > halfLength - 1f && position.x * direction.x > 0f;
            var blocksY = Mathf.Abs(projected.y) > halfWidth - 1f && position.y * direction.y > 0f;
            if (blocksX && blocksY) return new Vector2(-Mathf.Sign(position.x), -Mathf.Sign(position.y)).normalized;
            // The radial escape is blocked by a wall. Move along it opposite
            // the approaching teammate instead of repeatedly pushing the wall.
            if (blocksX) direction = new Vector2(0f, Mathf.Abs(partnerVelocity.y) > .0001f
                ? -Mathf.Sign(partnerVelocity.y) : Mathf.Abs(direction.y) > .0001f ? Mathf.Sign(direction.y) : -Mathf.Sign(position.x));
            if (blocksY) direction = new Vector2(Mathf.Abs(partnerVelocity.x) > .0001f
                ? -Mathf.Sign(partnerVelocity.x) : Mathf.Abs(direction.x) > .0001f ? Mathf.Sign(direction.x) : -Mathf.Sign(position.y), 0f);
            return direction.normalized;
        }

        public static bool ShouldYieldForEmergencySeparation(
            int selfSlot,
            MNG_PlayerSkill selfSkill,
            bool selfIsCarrier,
            int teammateSlot,
            MNG_PlayerSkill teammateSkill,
            bool teammateIsCarrier,
            bool teammateIsHumanOrForced)
        {
            var selfPriority = EmergencyTaskPriority(
                selfSkill, selfIsCarrier, false);
            var teammatePriority = EmergencyTaskPriority(
                teammateSkill, teammateIsCarrier, teammateIsHumanOrForced);
            if (selfPriority != teammatePriority)
                return selfPriority < teammatePriority;
            return selfSlot > teammateSlot;
        }

        public static Vector2 CalculateEmergencySeparationDirection(
            Vector2 selfPosition,
            Vector2 teammatePosition,
            int selfSlot,
            int teammateSlot,
            MachineLearning.Soccer.Team team = MachineLearning.Soccer.Team.Red)
        {
            var away = selfPosition - teammatePosition;
            if (away.sqrMagnitude > 0.0001f) return away.normalized;
            return new Vector2(0f, (team == MachineLearning.Soccer.Team.Red ? 1f : -1f) * (selfSlot > teammateSlot ? 1f : -1f));
        }

        static int EmergencyTaskPriority(
            MNG_PlayerSkill skill,
            bool isCarrier,
            bool isHumanOrForced)
        {
            if (isHumanOrForced) return 1000;
            if (isCarrier) return 900;
            return skill switch
            {
                MNG_PlayerSkill.AimPass => 800,
                MNG_PlayerSkill.AimShot => 800,
                MNG_PlayerSkill.ReceivePass => 700,
                MNG_PlayerSkill.Carry => 600,
                MNG_PlayerSkill.Press => 500,
                MNG_PlayerSkill.SupportRun => 400,
                MNG_PlayerSkill.Mark => 300,
                MNG_PlayerSkill.Cover => 250,
                MNG_PlayerSkill.MoveTo => 200,
                _ => 100
            };
        }

        void ResetEmergencySeparationState()
        {
            m_EmergencySeparationPartnerSlot = -1;
            m_EmergencySeparationRemainingSeconds = 0f;
            m_EmergencySeparationDirection = default;
        }

        public bool SetTask(MNG_PlayerTask task)
        {
            if (task.Revision < m_LatestTaskRevision) return false;
            if (IsKickSkill(m_Task.Skill)
                && !m_KickConsumed
                && matchController != null
                && matchController.EpisodeElapsedSeconds <= m_Task.ExpirySeconds)
                return false;
            if (m_Task.Skill == MNG_PlayerSkill.ReceivePass
                && matchController != null
                && matchController.EpisodeElapsedSeconds <= m_Task.ExpirySeconds)
            {
                var carrier = ballControl != null
                    ? ballControl.Carrier
                    : MNG_CarrierRef.None;
                var passStillInFlight = !carrier.IsValid;
                var receivedBySelf = carrier.IsValid
                    && carrier.Team == m_Avatar.Team
                    && carrier.Slot == m_Avatar.Slot;
                if (task.Skill == MNG_PlayerSkill.ReceivePass
                    || passStillInFlight
                    || receivedBySelf)
                    return false;
            }
            m_LatestTaskRevision = task.Revision;
            m_Task = task;
            m_KickConsumed = false;
            return true;
        }

        public void Cancel()
        {
            if (IsV2)
            {
                CancelV2(MNG_TaskResultKind.CancelledMatchState);
                m_Avatar.KickPlate.ResetPlate();
                return;
            }
            m_LatestTaskRevision++;
            m_Task = new MNG_PlayerTask
            {
                Skill = MNG_PlayerSkill.None,
                Revision = m_LatestTaskRevision,
                ExpirySeconds = matchController != null ? matchController.EpisodeElapsedSeconds : 0f
            };
            m_KickConsumed = true;
            ResetFrontPushState();
            ResetEmergencySeparationState();
        }

        public void ResetForRound()
        {
            m_ReceivingTaskId = -1;
            m_Lifetime.Reset();
            m_LatestTaskRevision = -1;
            m_Task = default;
            m_KickConsumed = false;
            ResetFrontPushState();
            ResetEmergencySeparationState();
        }

        bool TryExecuteFrontPush(long revision)
        {
            if (ballControl == null || matchController == null) return false;

            var carrier = ballControl.Carrier;
            var selfOwnsBall = carrier.IsValid
                && carrier.Team == m_Avatar.Team
                && carrier.Slot == m_Avatar.Slot;
            if (!UsesFrontPushForSkill(m_Task.Skill))
            {
                ResetFrontPushState();
                return false;
            }
            if (carrier.IsValid && !selfOwnsBall)
            {
                ResetFrontPushState();
                return false;
            }

            var ball3 = ballControl.transform.position;
            var ball = new Vector2(ball3.x, ball3.z);
            var requestedDestination = m_Task.Target;
            var targetIsBall = Vector2.Distance(requestedDestination, ball) <= BallTaskTolerance;
            if (!selfOwnsBall && !m_RoutingBehindBall && !targetIsBall)
                return false;

            var attackSign = m_Avatar.Team == MachineLearning.Soccer.Team.Red ? 1f : -1f;
            var pushDestination = targetIsBall
                ? new Vector2(
                    attackSign * (matchController.Snapshot.FieldHalfLength - 2f),
                    Mathf.Clamp(ball.y,
                        -matchController.Snapshot.GoalHalfWidth + 0.75f,
                        matchController.Snapshot.GoalHalfWidth - 0.75f))
                : requestedDestination;
            if (!IsV2) pushDestination = MNG_TeamPlanner.EnforceAttackingCarryTarget(
                ball,
                pushDestination,
                m_Avatar.Team,
                matchController.Snapshot.FieldHalfLength,
                matchController.Snapshot.FieldHalfWidth);
            var currentPlayerPosition = new Vector2(
                m_Avatar.Body.position.x,
                m_Avatar.Body.position.z);
            pushDestination = CalculateFieldPlayerSpacingTarget(
                matchController.Snapshot,
                m_Avatar.Team,
                m_Avatar.Slot,
                currentPlayerPosition,
                pushDestination);
            if (!IsV2) pushDestination = MNG_TeamPlanner.EnforceAttackingCarryTarget(
                ball,
                pushDestination,
                m_Avatar.Team,
                matchController.Snapshot.FieldHalfLength,
                matchController.Snapshot.FieldHalfWidth);
            var pushOffset = pushDestination - ball;
            if (pushOffset.sqrMagnitude <= 0.0001f) return false;
            var pushDirection = pushOffset.normalized;
            var forward = new Vector2(
                m_Avatar.transform.forward.x,
                m_Avatar.transform.forward.z).normalized;

            if (selfOwnsBall
                && Vector2.Dot(forward, pushDirection) >= ContinueCarryFacingDot
                && MNG_BallControl.IsBallInFrontForDribble(m_Avatar, ball3))
            {
                ResetFrontPushState();
                m_Motor.ApplyMoveTarget(
                    pushDestination,
                    pushDestination,
                    MNG_InputOwner.Manager,
                    revision,
                    Time.fixedDeltaTime);
                return true;
            }

            if (selfOwnsBall)
            {
                ballControl.ReleaseCarrierForFrontReposition(
                    m_Avatar.Team,
                    m_Avatar.Slot);
                m_RoutingBehindBall = true;
                m_PushingFromBehind = false;
                m_PushDestination = pushDestination;
            }
            else if (!m_RoutingBehindBall)
            {
                m_RoutingBehindBall = true;
                m_PushingFromBehind = false;
                m_PushDestination = pushDestination;
            }
            else if (targetIsBall)
            {
                m_PushDestination = pushDestination;
            }

            if (m_PushingFromBehind)
            {
                m_Motor.ApplyMoveTarget(
                    m_PushDestination,
                    m_PushDestination,
                    MNG_InputOwner.Manager,
                    revision,
                    Time.fixedDeltaTime);
                return true;
            }

            var player = new Vector2(
                m_Avatar.Body.position.x,
                m_Avatar.Body.position.z);
            var setup = CalculateBallPushSetup(ball, m_PushDestination);
            if (IsBehindBallForPush(player, ball, m_PushDestination)
                && Vector2.Distance(player, setup) <= BallSetupArrivalDistance)
            {
                m_PushingFromBehind = true;
                m_Motor.ApplyMoveTarget(
                    m_PushDestination,
                    m_PushDestination,
                    MNG_InputOwner.Manager,
                    revision,
                    Time.fixedDeltaTime);
                return true;
            }

            var waypoint = CalculateBallOrbitWaypoint(
                player,
                ball,
                m_PushDestination);
            m_Motor.ApplyMoveTarget(
                waypoint,
                ball,
                MNG_InputOwner.Manager,
                revision,
                Time.fixedDeltaTime);
            return true;
        }

        public static bool UsesFrontPushForSkill(MNG_PlayerSkill skill)
            => skill == MNG_PlayerSkill.Carry
                || skill == MNG_PlayerSkill.Press
                || skill == MNG_PlayerSkill.KeeperClaim;

        void ResetFrontPushState()
        {
            m_RoutingBehindBall = false;
            m_PushingFromBehind = false;
            m_PushDestination = default;
        }

        public static Vector2 CalculateBallPushSetup(
            Vector2 ball,
            Vector2 pushDestination)
        {
            var offset = pushDestination - ball;
            if (offset.sqrMagnitude <= 0.0001f) return ball;
            return ball - offset.normalized * BallPushSetupDistance;
        }

        public static bool IsBehindBallForPush(
            Vector2 player,
            Vector2 ball,
            Vector2 pushDestination)
        {
            var pushOffset = pushDestination - ball;
            if (pushOffset.sqrMagnitude <= 0.0001f) return false;
            var pushDirection = pushOffset.normalized;
            var playerOffset = player - ball;
            var longitudinal = Vector2.Dot(playerOffset, pushDirection);
            var lateral = Mathf.Abs(
                pushDirection.x * playerOffset.y
                - pushDirection.y * playerOffset.x);
            return longitudinal <= -BallBehindLongitudinalTolerance
                && lateral <= BallBehindLateralTolerance;
        }

        public static Vector2 CalculateBallOrbitWaypoint(
            Vector2 player,
            Vector2 ball,
            Vector2 pushDestination)
        {
            var pushOffset = pushDestination - ball;
            if (pushOffset.sqrMagnitude <= 0.0001f) return ball;
            var behindDirection = -pushOffset.normalized;
            var radial = player - ball;
            if (radial.sqrMagnitude <= 0.0001f)
                radial = new Vector2(-behindDirection.y, behindDirection.x);
            radial.Normalize();

            var signedAngle = Vector2.SignedAngle(radial, behindDirection);
            var step = Mathf.Clamp(signedAngle, -70f, 70f);
            var radians = step * Mathf.Deg2Rad;
            var rotated = new Vector2(
                radial.x * Mathf.Cos(radians) - radial.y * Mathf.Sin(radians),
                radial.x * Mathf.Sin(radians) + radial.y * Mathf.Cos(radians));
            return ball + rotated * BallOrbitRadius;
        }

        void AimAndKick(
            Vector2 target2,
            float exitSpeed,
            MNG_KickIntent intent)
        {
            if (m_Task.PassBuildRule && !matchController.IsPassBuildExecutionValid(m_Avatar.Team, m_Avatar.Slot, m_Task.ReceiverSlot, target2))
            {
                if (IsV2) CancelV2(MNG_TaskResultKind.CancelledMatchState);
                else { m_Task = default; m_KickConsumed = true; }
                return;
            }
            var target = new Vector3(target2.x, m_Avatar.Body.position.y, target2.y);
            var ballPosition = ballControl != null
                ? ballControl.transform.position
                : m_Avatar.KickPlate.DribblePosition;
            var kickDirection = target - ballPosition;
            kickDirection.y = 0f;
            if (kickDirection.sqrMagnitude <= 0.0001f) return;
            kickDirection.Normalize();
            var approach = ballPosition - kickDirection * MNG_KickPlate.DribbleAnchorForward;
            var approach2 = new Vector2(approach.x, approach.z);
            var playerPosition = new Vector2(m_Avatar.Body.position.x, m_Avatar.Body.position.z);
            var approachDistance = Vector2.Distance(playerPosition, approach2);
            var carrier = ballControl != null ? ballControl.Carrier : MNG_CarrierRef.None;
            var ownsBall = carrier.IsValid && carrier.Team == m_Avatar.Team
                && carrier.Slot == m_Avatar.Slot;
            // Turn the occupied plate with the ball; moving toward a new approach point
            // first would pull the carrier away from its own ball during a lateral kick.
            if (approachDistance <= 0.75f || ownsBall)
            {
                var planarKickDirection = new Vector2(kickDirection.x, kickDirection.z);
                if (ownsBall)
                {
                    var toBall = ballPosition - m_Avatar.Body.position;
                    toBall.y = 0f;
                    // Keep the turning plate within its capture radius of the
                    // real ball bearing. The motor still applies its normal turn limit.
                    var captureAngle = Mathf.Atan(MNG_KickPlate.DribbleCaptureRadius
                        / MNG_KickPlate.DribbleAnchorForward);
                    var controlledFacing = Vector3.RotateTowards(
                        toBall.normalized, kickDirection, captureAngle, 0f);
                    planarKickDirection = new Vector2(controlledFacing.x, controlledFacing.z);
                }
                var plateForward = new Vector2(
                    m_Avatar.transform.forward.x,
                    m_Avatar.transform.forward.z).normalized;
                var controlVelocity = plateForward * 0.5f;
                if (ownsBall)
                {
                    var plateError = ballPosition - m_Avatar.KickPlate.DribblePosition;
                    var error = new Vector2(plateError.x, plateError.z);
                    var lateral = error - plateForward * Vector2.Dot(error, plateForward);
                    // Maintain forward-drive eligibility while closing the plate error.
                    controlVelocity += lateral + plateForward * Mathf.Max(
                        lateral.magnitude, Vector2.Dot(error, plateForward));
                }
                m_Motor.ApplyDesiredVelocity(
                    controlVelocity,
                    planarKickDirection,
                    MNG_InputOwner.Manager,
                    m_Avatar.Ownership.Revision,
                    Time.fixedDeltaTime);
            }
            else
            {
                m_Motor.ApplyMoveTarget(
                    approach2,
                    target2,
                    MNG_InputOwner.Manager,
                    m_Avatar.Ownership.Revision,
                    Time.fixedDeltaTime);
            }
            if (m_KickConsumed
                || approachDistance > 0.45f
                || Vector3.Angle(m_Avatar.transform.forward, kickDirection) > 10f)
                return;
            if (intent == MNG_KickIntent.Pass && m_Task.ReceiverSlot >= 0
                && !matchController.GetPlayerAvatar(m_Avatar.Team, m_Task.ReceiverSlot).IsHuman)
            {
                var receiver = matchController.GetPlayerAvatar(m_Avatar.Team, m_Task.ReceiverSlot);
                var receiving = receiver.GetComponent<MNG_PlayerSkillExecutor>().CurrentTask;
                if (receiving.Skill != MNG_PlayerSkill.ReceivePass) return;
                var incoming = ballPosition - receiver.Body.position; incoming.y = 0;
                var turnSeconds = Vector3.Angle(receiver.transform.forward, incoming)
                    / receiver.GetComponent<MNG_PlayerMotor>().Profile.RotationDegreesPerSecond;
                var flightSeconds = Vector3.Distance(ballPosition, target) / exitSpeed;
                if (turnSeconds > flightSeconds) return;
            }
            if (ballControl != null && ballControl.TryKick(
                    m_Avatar.Team, m_Avatar.Slot, target, exitSpeed, intent, out _))
            {
                m_KickConsumed = true;
                if (IsV2) m_Lifetime.ConsumeKick();
            }
        }

        static bool IsKickSkill(MNG_PlayerSkill skill)
            => skill == MNG_PlayerSkill.AimPass || skill == MNG_PlayerSkill.AimShot;
    }
}
