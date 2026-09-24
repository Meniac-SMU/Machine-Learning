using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public readonly struct MNG_PlateStrikeEvent
    {
        public readonly Team Team;
        public readonly int Slot;
        public readonly long KickId;
        public readonly Vector2 Origin;
        public readonly Vector2 Target;
        public readonly float ExitSpeed;
        public readonly MNG_KickIntent Intent;
        public readonly long TaskId;
        public readonly long ParentCommandId;
        public readonly int IntendedReceiverSlot;
        public readonly MNG_ActionSource Source;

        public MNG_PlateStrikeEvent(
            Team team,
            int slot,
            long kickId,
            Vector2 origin,
            Vector2 target,
            float exitSpeed,
            MNG_KickIntent intent, long taskId = 0, long parentCommandId = 0,
            int intendedReceiverSlot = -1, MNG_ActionSource source = MNG_ActionSource.PolicyCommand)
        {
            Team = team;
            Slot = slot;
            KickId = kickId;
            Origin = origin;
            Target = target;
            ExitSpeed = exitSpeed;
            Intent = intent;
            TaskId = taskId;
            ParentCommandId = parentCommandId;
            IntendedReceiverSlot = intendedReceiverSlot;
            Source = source;
        }
    }

    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class MNG_BallControl : MonoBehaviour
    {
        const float ReceptionContactGraceSeconds = 0.24f;
        const float MaximumReceptionRelativeSpeed = 18f;
        const float NonForwardReleaseSeconds = 0.12f;
        public const float StealReacquireLockSeconds = 0.22f;
        const float MinimumDribbleMoveSpeed = 0.18f;
        const float MinimumForwardControlDot = 0.65f;
        const float DribbleWobbleLateralAmplitude = 0.18f;
        const float DribbleWobbleLongitudinalAmplitude = 0.10f;
        const float DribbleWobbleFrequency = 6.5f;
        const float DribbleMagnetStrengthMultiplier = 1.45f;
        const float DribbleMagnetDampingMultiplier = 1.20f;
        const float DribbleAvoidanceRadius = 4.75f;
        const float DribbleAvoidanceHalfWidth = 2.25f;
        const float DribbleAvoidanceAcceleration = 8f;
        const float ContenderRadius = 2.60f;
        public const float ContestedEscapeForwardDistance = 4f;
        public const float ContestedEscapeLateralDistance = 2.25f;
        const float YieldRadialDistance = 3.00f;
        const float YieldLateralDistance = 2.50f;
        public const float BoundaryProximity = 3f;
        public const float BoundaryStationarySpeed = 0.45f;
        public const float BoundaryStationarySeconds = 0.75f;
        public const float BoundaryEscapeSeconds = 3.50f;
        public const float BoundaryRetreatSeconds = 0.55f;
        public const float BoundaryRetreatDistance = 3.25f;
        public const float BoundaryKickDistance = 11f;
        public const float BoundaryReleaseDistance = 1.75f;

        [SerializeField] MNG_PhysicsProfile profile;
        [SerializeField] MNG_MatchController matchController;

        readonly bool[] m_Contacts = new bool[MNG_MatchSnapshot.PlayersPerTeam * MNG_MatchSnapshot.TeamCount];
        readonly float[] m_ContactGraceSeconds =
            new float[MNG_MatchSnapshot.PlayersPerTeam * MNG_MatchSnapshot.TeamCount];
        readonly int[] m_PhysicalContactCounts =
            new int[MNG_MatchSnapshot.PlayersPerTeam * MNG_MatchSnapshot.TeamCount];
        readonly bool[] m_StopBlocked =
            new bool[MNG_MatchSnapshot.PlayersPerTeam * MNG_MatchSnapshot.TeamCount];
        readonly bool[] m_KickBlocked =
            new bool[MNG_MatchSnapshot.PlayersPerTeam * MNG_MatchSnapshot.TeamCount];
        readonly float[] m_NonForwardSeconds =
            new float[MNG_MatchSnapshot.PlayersPerTeam * MNG_MatchSnapshot.TeamCount];
        readonly float[] m_StealReacquireSeconds =
            new float[MNG_MatchSnapshot.PlayersPerTeam * MNG_MatchSnapshot.TeamCount];
        readonly MNG_PossessionCandidate[] m_Candidates =
            new MNG_PossessionCandidate[MNG_MatchSnapshot.PlayersPerTeam * MNG_MatchSnapshot.TeamCount];

        Rigidbody m_Body;
#if UNITY_EDITOR
        // Read only from explicit first-contact diagnostic tests; absent from Players.
        internal bool SuppressContactResponseForDiagnostics;
        internal readonly System.Collections.Generic.List<int> ContactOrderForDiagnostics = new();
#endif
        Collider m_BallCollider;
        MNG_PossessionLedger m_Ledger;
        readonly MNG_ContestedBallEscape m_ContestedEscape = new();
        long m_KickId;
        int m_RedEscapeSlot = -1;
        int m_NavyEscapeSlot = -1;
        float m_BoundaryStationarySeconds;
        float m_BoundaryEscapeRemainingSeconds;
        float m_BoundaryRetreatRemainingSeconds;
        Vector2 m_BoundaryEscapeOrigin;
        Vector2 m_BoundaryRetreatTarget;
        Vector2 m_BoundaryKickTarget;
        Team m_BoundaryEscapeTeam;
        int m_BoundaryEscapeSlot = -1;
        int m_BoundaryEscapeActivationCount;

        public MNG_CarrierRef Carrier => m_Ledger?.Carrier ?? MNG_CarrierRef.None;
        public long LatestKickId => m_KickId;
        public MNG_PossessionLedger Ledger => m_Ledger;
        public bool DribbleCorrectionAppliedLastStep { get; private set; }
        public bool IsContestedEscapeActive => m_ContestedEscape.IsActive;
        public int ContestedEscapeActivationCount => m_ContestedEscape.ActivationCount;
        public bool IsBoundaryEscapeActive => m_BoundaryEscapeSlot >= 0
            && m_BoundaryEscapeRemainingSeconds > 0f;
        public int BoundaryEscapeActivationCount => m_BoundaryEscapeActivationCount;
        public event Action<MNG_PlateStrikeEvent> PlateStrikeApplied;

        public int GetPhysicalContactCount(Team team, int slot)
        {
            if (slot < 0 || slot >= MNG_MatchSnapshot.PlayersPerTeam)
                throw new ArgumentOutOfRangeException(nameof(slot));
            return m_PhysicalContactCounts[GetIndex(team, slot)];
        }

        public float GetAcquisitionDistance(Team team, int slot)
        {
            var avatar = matchController.GetPlayerAvatar(team, slot);
            return avatar.KickPlate != null ? MNG_KickPlate.DribbleCaptureRadius : 0f;
        }

        void Awake()
        {
            m_Body = GetComponent<Rigidbody>();
            m_BallCollider = GetComponent<Collider>();
            if (matchController == null) matchController = GetComponentInParent<MNG_MatchController>();
            InitializeLedger();
            ValidateBindingsOrThrow();
            m_Body.maxLinearVelocity = profile.MaximumBallSpeed;
            MNG_PhysicsProfile.ConfigureContactSolver(m_Body);
        }

        void FixedUpdate()
        {
#if UNITY_EDITOR
            ContactOrderForDiagnostics.Clear();
#endif
            DribbleCorrectionAppliedLastStep = false;
            if (m_Ledger == null) return;
            if (!matchController.IsPlayActive)
            {
                Array.Clear(m_Contacts, 0, m_Contacts.Length);
                Array.Clear(m_ContactGraceSeconds, 0, m_ContactGraceSeconds.Length);
                Array.Clear(m_NonForwardSeconds, 0, m_NonForwardSeconds.Length);
                Array.Clear(m_StealReacquireSeconds, 0, m_StealReacquireSeconds.Length);
                return;
            }
            ReleaseCarrierWhenForwardDriveStops(Time.fixedDeltaTime);
            var count = 0;
            for (var teamIndex = 0; teamIndex < 2; teamIndex++)
            {
                var team = teamIndex == 0 ? Team.Red : Team.Navy;
                for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
                {
                    var avatar = matchController.GetPlayerAvatar(team, slot);
                    var index = GetIndex(team, slot);
                    var plate = avatar.KickPlate;
                    var distance = plate != null
                        ? plate.DistanceToDribblePosition(m_Body.position)
                        : float.MaxValue;
                    if (plate != null && !plate.IsWithinReleasePosition(m_Body.position))
                    {
                        m_StopBlocked[index] = false;
                        m_KickBlocked[index] = false;
                    }
                    if (plate != null && IsDribbleMotionActive(avatar, plate))
                        m_StopBlocked[index] = false;
                    var acquisitionBlocked = m_StopBlocked[index]
                        || m_KickBlocked[index]
                        || m_StealReacquireSeconds[index] > 0f;
                    var inControlZone = plate != null
                        && plate.IsAtDribblePosition(m_Body.position)
                        && !acquisitionBlocked;
                    m_Candidates[count++] = new MNG_PossessionCandidate
                    {
                        Team = team,
                        Slot = slot,
                        Active = avatar.gameObject.activeInHierarchy && plate != null && plate.isActiveAndEnabled,
                        HasPhysicalContact = !acquisitionBlocked
                            && (m_Contacts[index] || m_ContactGraceSeconds[index] > 0f),
                        IsInControlZone = inControlZone,
                        CenterDistance = distance,
                        AcquisitionDistance = MNG_KickPlate.DribbleCaptureRadius
                    };
                }
            }

            m_Ledger.Update(m_Candidates, count, Time.fixedDeltaTime);
            PublishPossession();
            UpdateContestedEscape(Time.fixedDeltaTime);
            UpdateBoundaryEscape(Time.fixedDeltaTime);
            ApplyDribble();
            ApplyVisibleRolling();
            ClampBallSpeed();
            Array.Clear(m_Contacts, 0, m_Contacts.Length);
            for (var i = 0; i < m_ContactGraceSeconds.Length; i++)
            {
                m_ContactGraceSeconds[i] = Mathf.Max(0f,
                    m_ContactGraceSeconds[i] - Time.fixedDeltaTime);
                m_StealReacquireSeconds[i] = Mathf.Max(0f,
                    m_StealReacquireSeconds[i] - Time.fixedDeltaTime);
            }
        }

        public void Configure(MNG_PhysicsProfile configuredProfile, MNG_MatchController configuredMatch)
        {
            profile = configuredProfile ?? throw new ArgumentNullException(nameof(configuredProfile));
            matchController = configuredMatch ?? throw new ArgumentNullException(nameof(configuredMatch));
            m_Body = GetComponent<Rigidbody>();
            m_BallCollider = GetComponent<Collider>();
            InitializeLedger();
            ValidateBindingsOrThrow();
            MNG_PhysicsProfile.ConfigureContactSolver(m_Body);
        }

        public bool TryKick(Team team, int slot, Vector3 target, float exitSpeed, out long kickId)
            => TryKick(team, slot, target, exitSpeed, MNG_KickIntent.Unspecified, out kickId);

        public bool TryKick(
            Team team,
            int slot,
            Vector3 target,
            float exitSpeed,
            MNG_KickIntent intent,
            out long kickId)
        {
            kickId = 0;
            if (m_Ledger == null || !m_Ledger.Carrier.IsValid
                || m_Ledger.Carrier.Team != team || m_Ledger.Carrier.Slot != slot)
                return false;
            var avatar = matchController.GetPlayerAvatar(team, slot);
            if (avatar.KickCooldownSeconds > 0f) return false;
            var plate = avatar.KickPlate;
            var task = avatar.GetComponent<MNG_PlayerSkillExecutor>().CurrentTask;
            if (plate == null || !plate.TryArmKick(target, exitSpeed, intent, task)) return false;
            kickId = m_KickId + 1;
            return true;
        }

        public void ResetLedger()
        {
            m_Ledger?.Reset();
            Array.Clear(m_Contacts, 0, m_Contacts.Length);
            Array.Clear(m_ContactGraceSeconds, 0, m_ContactGraceSeconds.Length);
            Array.Clear(m_PhysicalContactCounts, 0, m_PhysicalContactCounts.Length);
            Array.Clear(m_StopBlocked, 0, m_StopBlocked.Length);
            Array.Clear(m_KickBlocked, 0, m_KickBlocked.Length);
            Array.Clear(m_NonForwardSeconds, 0, m_NonForwardSeconds.Length);
            Array.Clear(m_StealReacquireSeconds, 0, m_StealReacquireSeconds.Length);
            m_ContestedEscape.Reset();
            m_RedEscapeSlot = -1;
            m_NavyEscapeSlot = -1;
            ResetBoundaryEscapeState();
            if (matchController != null)
            {
                foreach (var team in new[] { Team.Red, Team.Navy })
                for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
                {
                    var avatar = matchController.GetPlayerAvatar(team, slot);
                    avatar.KickPlate?.ResetPlate();
                    avatar.GetComponent<MNG_PlayerMotor>()?.ResetDriveCommand();
                }
            }
            PublishPossession();
        }

        Team m_NextEscapeTeam, m_EscapeWinner;
        bool m_EscapeNeutralGranted;
        readonly int[] m_NeutralEscapeGrants = new int[2];
        public int NeutralTieWins(Team team) => m_Ledger?.NeutralTieWins(team) ?? 0;
        public int NeutralEscapeGrants(Team team) => m_NeutralEscapeGrants[(int)team];
        // Episode initialization only. A goal/round reset must not restore Red priority.
        public void ConfigureNeutralPriority(Team firstTeam)
        {
            m_Ledger?.ConfigureNeutralPriority(firstTeam);
            m_NextEscapeTeam = m_EscapeWinner = firstTeam;
            Array.Clear(m_NeutralEscapeGrants, 0, m_NeutralEscapeGrants.Length);
        }

        public bool ReleaseCarrierForFrontReposition(Team team, int slot)
        {
            if (m_Ledger == null
                || !m_Ledger.Carrier.IsValid
                || m_Ledger.Carrier.Team != team
                || m_Ledger.Carrier.Slot != slot)
                return false;

            var index = GetIndex(team, slot);
            m_Ledger.ReleaseForControlLoss(team, slot);
            m_StopBlocked[index] = true;
            m_ContactGraceSeconds[index] = 0f;
            m_NonForwardSeconds[index] = 0f;
            PublishPossession();
            return true;
        }

        public bool TryGetContestedEscapeTarget(Team team, int slot, out Vector2 target)
        {
            target = default;
            if (!m_ContestedEscape.IsActive) return false;
            var selectedSlot = team == Team.Red ? m_RedEscapeSlot : m_NavyEscapeSlot;
            if (slot != selectedSlot) return false;

            var snapshot = matchController.Snapshot;
            var teamSign = team == Team.Red ? 1f : -1f;
            var carrier = m_Ledger?.Carrier ?? MNG_CarrierRef.None;
            var winner = carrier.IsValid
                ? carrier.Team
                : m_EscapeWinner;
            var lateralSign = (winner == Team.Red ? 1f : -1f)
                * (((m_ContestedEscape.Sequence / 2) & 1) == 0 ? 1f : -1f);
            if (team == winner)
            {
                target = m_ContestedEscape.EscapeOrigin
                    + new Vector2(
                        teamSign * ContestedEscapeForwardDistance,
                        lateralSign * ContestedEscapeLateralDistance);
            }
            else
            {
                var playerPosition = snapshot.GetPlayer(team, slot).Position;
                var away = playerPosition - m_ContestedEscape.EscapeOrigin;
                if (away.sqrMagnitude <= 0.0001f) away = new Vector2(-teamSign, 0f);
                target = playerPosition + away.normalized * YieldRadialDistance
                    + new Vector2(0f, -lateralSign * YieldLateralDistance);
            }
            target.x = Mathf.Clamp(target.x, -snapshot.FieldHalfLength + 2f, snapshot.FieldHalfLength - 2f);
            target.y = Mathf.Clamp(target.y, -snapshot.FieldHalfWidth + 2f, snapshot.FieldHalfWidth - 2f);
            return true;
        }

        public bool TryGetBoundaryEscapePlan(
            Team team,
            int slot,
            out bool retreating,
            out Vector2 target)
        {
            retreating = false;
            target = default;
            if (!IsBoundaryEscapeActive
                || team != m_BoundaryEscapeTeam
                || slot != m_BoundaryEscapeSlot)
                return false;

            retreating = m_BoundaryRetreatRemainingSeconds > 0f;
            target = retreating ? m_BoundaryRetreatTarget : m_BoundaryKickTarget;
            return true;
        }

        public static bool IsNearBoundary(
            Vector2 ballPosition,
            float fieldHalfLength,
            float fieldHalfWidth)
            => Mathf.Abs(ballPosition.x) >= fieldHalfLength - BoundaryProximity
                || Mathf.Abs(ballPosition.y) >= fieldHalfWidth - BoundaryProximity;

        public static Vector2 CalculateBoundaryEscapeDirection(
            Vector2 ballPosition,
            Team team,
            float fieldHalfLength,
            float fieldHalfWidth)
        {
            var attackSign = team == Team.Red ? 1f : -1f;
            var attack = new Vector2(attackSign, 0f);
            var direction = attack;
            var nearEnd = Mathf.Abs(ballPosition.x)
                >= fieldHalfLength - BoundaryProximity;
            var nearSide = Mathf.Abs(ballPosition.y)
                >= fieldHalfWidth - BoundaryProximity;

            if (nearEnd)
            {
                var inwardEnd = new Vector2(ballPosition.x >= 0f ? -1f : 1f, 0f);
                var atOpponentEnd = ballPosition.x * attackSign > 0f;
                direction = atOpponentEnd
                    ? inwardEnd * 2.4f + attack * 0.35f
                    : inwardEnd * 1.4f + attack * 0.8f;
            }
            if (nearSide)
            {
                var inwardSide = new Vector2(0f, ballPosition.y >= 0f ? -1f : 1f);
                direction += inwardSide * (nearEnd ? 1.5f : 1.4f);
            }
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : attack;
        }

        void OnCollisionEnter(Collision collision) => RegisterContact(collision.collider);
        void OnCollisionStay(Collision collision) => RegisterContact(collision.collider);

        void OnTriggerEnter(Collider other)
        {
            var goal = other != null ? other.GetComponent<SoccerGoalSurface>() : null;
            if (goal == null || !goal.Scores) return;
            matchController.GoalTouched(goal.DefendingTeam == Team.Red ? Team.Navy : Team.Red);
        }

        void RegisterContact(Collider other)
        {
            var plate = other != null ? other.GetComponentInParent<MNG_KickPlate>() : null;
            var avatar = plate != null ? plate.Owner : null;
            if (avatar == null || !plate.OwnsCollider(other)) return;
            var index = GetIndex(avatar.Team, avatar.Slot);
            m_Contacts[index] = true;
            m_PhysicalContactCounts[index]++;
#if UNITY_EDITOR
            ContactOrderForDiagnostics.Add(index);
            if (SuppressContactResponseForDiagnostics) return;
#endif

            var previousCarrier = m_Ledger.Carrier;
            if (previousCarrier.IsValid
                && previousCarrier.Team != avatar.Team
                && plate.IsAtDribblePosition(m_Body.position))
            {
                var previousIndex = GetIndex(previousCarrier.Team, previousCarrier.Slot);
                m_Ledger.ReleaseForControlLoss(previousCarrier.Team, previousCarrier.Slot);
                m_StealReacquireSeconds[previousIndex] = StealReacquireLockSeconds;
                m_ContactGraceSeconds[index] = ReceptionContactGraceSeconds;
                m_Body.WakeUp();
                PublishPossession();
            }

            if (plate.TryConsumeStrike(other, out var kick))
            {
                ApplyPlateStrike(avatar, index, kick);
                return;
            }

            if (m_KickBlocked[index]) return;
            if (!plate.IsAtDribblePosition(m_Body.position)) return;
            var relativeVelocity = m_Body.linearVelocity - avatar.Body.linearVelocity;
            relativeVelocity.y = 0f;
            if (relativeVelocity.magnitude > MaximumReceptionRelativeSpeed) return;

            m_ContactGraceSeconds[index] = ReceptionContactGraceSeconds;
            var targetVelocity = avatar.Body.linearVelocity;
            targetVelocity.y = m_Body.linearVelocity.y;
            var impulse = m_Body.mass * (targetVelocity - m_Body.linearVelocity);
            impulse.y = 0f;
            m_Body.WakeUp();
            m_Body.AddForce(impulse, ForceMode.Impulse);
        }

        void ApplyDribble()
        {
            var carrier = m_Ledger.Carrier;
            if (!carrier.IsValid) return;
            var avatar = matchController.GetPlayerAvatar(carrier.Team, carrier.Slot);
            var plate = avatar.KickPlate;
            if (plate == null || !IsDribbleMotionActive(avatar, plate)
                || !IsBallInFrontForDribble(avatar, m_Body.position)
                || !plate.IsWithinReleasePosition(m_Body.position))
                return;
            var target = plate.DribblePosition;
            target.y = m_Body.position.y;
            var planarPlayerVelocity = new Vector2(
                avatar.Body.linearVelocity.x,
                avatar.Body.linearVelocity.z);
            var wobble = CalculateDribbleWobble(
                matchController.EpisodeElapsedSeconds,
                planarPlayerVelocity.magnitude,
                carrier.Slot);
            target += plate.transform.right * wobble.x
                + plate.transform.forward * wobble.y;
            target.y = m_Body.position.y;

            var avoidance = CalculateCarrierAvoidance(
                matchController.Snapshot,
                carrier.Team,
                carrier.Slot,
                planarPlayerVelocity);
            if (avoidance.sqrMagnitude > 0.0001f)
            {
                var avoidance3 = new Vector3(avoidance.x, 0f, avoidance.y);
                target += avoidance3 * 0.20f;
                avatar.Body.AddForce(
                    avoidance3 * DribbleAvoidanceAcceleration,
                    ForceMode.Acceleration);
            }

            var error = target - m_Body.position;
            error.y = 0f;
            var relativeVelocity = avatar.Body.linearVelocity - m_Body.linearVelocity;
            relativeVelocity.y = 0f;
            var maximumAcceleration = profile.DribbleAcceleration * DribbleMagnetStrengthMultiplier;
            var acceleration = error * maximumAcceleration
                + relativeVelocity
                * profile.DribbleDampingPerSecond
                * DribbleMagnetDampingMultiplier;
            acceleration = Vector3.ClampMagnitude(acceleration, maximumAcceleration);
            m_Body.WakeUp();
            m_Body.AddForce(acceleration, ForceMode.Acceleration);
            DribbleCorrectionAppliedLastStep = true;
        }

        void ApplyVisibleRolling()
        {
            var velocity = m_Body.linearVelocity;
            var planarVelocity = new Vector3(velocity.x, 0f, velocity.z);
            if (planarVelocity.sqrMagnitude < 0.01f) return;

            var extents = m_BallCollider.bounds.extents;
            var radius = Mathf.Max(0.15f, Mathf.Min(extents.x, extents.z));
            var targetAngularVelocity = Vector3.Cross(Vector3.up, planarVelocity) / radius;
            var correction = targetAngularVelocity - m_Body.angularVelocity;
            correction = Vector3.ClampMagnitude(correction, 45f);
            m_Body.WakeUp();
            m_Body.AddTorque(correction * 8f, ForceMode.Acceleration);
        }

        static bool IsDribbleMotionActive(MNG_PlayerAvatar avatar, MNG_KickPlate plate)
        {
            if (plate.HasPendingKick) return true;
            if (!plate.IsForwardDriveActive()) return false;
            var velocity = avatar.Body.linearVelocity;
            var planarVelocity = new Vector2(velocity.x, velocity.z);
            var planarSpeed = planarVelocity.magnitude;
            if (planarSpeed < MinimumDribbleMoveSpeed) return false;
            var forward = new Vector2(
                plate.transform.forward.x,
                plate.transform.forward.z).normalized;
            return Vector2.Dot(planarVelocity / planarSpeed, forward)
                >= MinimumForwardControlDot;
        }

        public static bool IsBallInFrontForDribble(
            MNG_PlayerAvatar avatar,
            Vector3 ballPosition)
        {
            if (avatar == null) return false;
            var offset = ballPosition - avatar.Body.position;
            offset.y = 0f;
            if (offset.sqrMagnitude <= 0.0001f) return false;
            var forward = avatar.transform.forward;
            forward.y = 0f;
            return Vector3.Dot(offset.normalized, forward.normalized) > 0.10f;
        }

        public static Vector2 CalculateDribbleWobble(float time, float planarSpeed, int slot)
        {
            var speedFactor = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(planarSpeed / 6f));
            var phase = time * (DribbleWobbleFrequency + planarSpeed * 0.35f)
                + slot * 1.618f;
            return new Vector2(
                Mathf.Sin(phase) * DribbleWobbleLateralAmplitude * speedFactor,
                Mathf.Sin(phase * 0.57f + 1.13f)
                * DribbleWobbleLongitudinalAmplitude
                * speedFactor);
        }

        public static Vector2 CalculateCarrierAvoidance(
            MNG_MatchSnapshot snapshot,
            Team team,
            int carrierSlot,
            Vector2 driveVelocity)
        {
            if (snapshot == null || carrierSlot < 0
                || carrierSlot >= MNG_MatchSnapshot.PlayersPerTeam)
                return Vector2.zero;
            var speed = driveVelocity.magnitude;
            if (speed < MinimumDribbleMoveSpeed) return Vector2.zero;

            var driveDirection = driveVelocity / speed;
            var attackDirection = team == Team.Red ? Vector2.right : Vector2.left;
            if (Vector2.Dot(driveDirection, attackDirection) < 0.20f)
                return Vector2.zero;

            var carrier = snapshot.GetPlayer(team, carrierSlot);
            var opponentTeam = team == Team.Red ? Team.Navy : Team.Red;
            var right = new Vector2(-driveDirection.y, driveDirection.x);
            var best = Vector2.zero;
            var bestUrgency = 0f;
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var opponent = snapshot.GetPlayer(opponentTeam, slot);
                if (!opponent.Active) continue;
                var offset = opponent.Position - carrier.Position;
                var distance = offset.magnitude;
                if (distance <= 0.001f || distance > DribbleAvoidanceRadius) continue;
                var forwardDistance = Vector2.Dot(offset, driveDirection);
                var lateralDistance = Vector2.Dot(offset, right);
                if (forwardDistance <= 0f || Mathf.Abs(lateralDistance) > DribbleAvoidanceHalfWidth)
                    continue;

                var urgency = 1f - distance / DribbleAvoidanceRadius;
                urgency *= 1f - Mathf.Abs(lateralDistance) / DribbleAvoidanceHalfWidth;
                if (urgency <= bestUrgency) continue;
                var side = Mathf.Abs(lateralDistance) < 0.05f
                    ? ((carrierSlot & 1) == 0 ? 1f : -1f)
                    : -Mathf.Sign(lateralDistance);
                best = right * side * urgency;
                bestUrgency = urgency;
            }
            return Vector2.ClampMagnitude(best, 1f);
        }

        void ApplyPlateStrike(MNG_PlayerAvatar avatar, int index, MNG_KickRequest kick)
        {
            if (!m_Ledger.Carrier.IsValid
                || m_Ledger.Carrier.Team != avatar.Team
                || m_Ledger.Carrier.Slot != avatar.Slot)
                return;
            if (kick.PassBuildRule && !matchController.IsPassBuildExecutionValid(avatar.Team, avatar.Slot,
                    kick.IntendedReceiverSlot, new Vector2(kick.Target.x, kick.Target.z))) return;
            var direction = kick.Target - m_Body.position;
            if (!MNG_KickSolver.TrySolvePlanarImpulse(
                    m_Body.linearVelocity, direction, kick.ExitSpeed, m_Body.mass, out var impulse))
                return;

            m_Body.AddForce(impulse, ForceMode.Impulse);
            avatar.StartKickCooldown();
            m_Ledger.ReleaseForKick(avatar.Team, avatar.Slot);
            m_KickBlocked[index] = true;
            m_ContactGraceSeconds[index] = 0f;
            m_KickId++;
            if(kick.CommonRule) matchController.RecordCommonRuleKick(avatar.Team,m_KickId);
            if(kick.PassBuildRule) matchController.RecordPassBuildStrike(avatar.Team);
            PublishPossession();
            PlateStrikeApplied?.Invoke(new MNG_PlateStrikeEvent(
                avatar.Team,
                avatar.Slot,
                m_KickId,
                new Vector2(m_Body.position.x, m_Body.position.z),
                new Vector2(kick.Target.x, kick.Target.z),
                kick.ExitSpeed,
                kick.Intent, kick.TaskId, kick.ParentCommandId, kick.IntendedReceiverSlot, kick.Source));
        }

        void ReleaseCarrierWhenForwardDriveStops(float deltaTime)
        {
            var carrier = m_Ledger.Carrier;
            if (!carrier.IsValid) return;
            var avatar = matchController.GetPlayerAvatar(carrier.Team, carrier.Slot);
            var plate = avatar.KickPlate;
            var index = GetIndex(carrier.Team, carrier.Slot);
            if (plate != null && IsDribbleMotionActive(avatar, plate))
            {
                m_NonForwardSeconds[index] = 0f;
                return;
            }

            m_NonForwardSeconds[index] += deltaTime;
            if (m_NonForwardSeconds[index] < NonForwardReleaseSeconds) return;
            m_Ledger.ReleaseForControlLoss(carrier.Team, carrier.Slot);
            m_StopBlocked[index] = true;
            m_NonForwardSeconds[index] = 0f;
            PublishPossession();
        }

        void ClampBallSpeed()
        {
            var velocity = m_Body.linearVelocity;
            var planar = new Vector2(velocity.x, velocity.z);
            planar = Vector2.ClampMagnitude(planar, profile.MaximumBallSpeed);
            m_Body.linearVelocity = new Vector3(planar.x, velocity.y, planar.y);
        }

        void UpdateContestedEscape(float deltaTime)
        {
            var ballPosition = new Vector2(m_Body.position.x, m_Body.position.z);
            var redSlot = FindNearestContender(Team.Red, ballPosition);
            var navySlot = FindNearestContender(Team.Navy, ballPosition);
            var wasActive = m_ContestedEscape.IsActive;
            var sequenceBefore = m_ContestedEscape.Sequence;
            var isActive = m_ContestedEscape.Update(
                ballPosition,
                redSlot >= 0,
                navySlot >= 0,
                deltaTime);

            if (!isActive)
            {
                m_RedEscapeSlot = -1;
                m_NavyEscapeSlot = -1;
                return;
            }

            if (!wasActive || sequenceBefore != m_ContestedEscape.Sequence)
                m_EscapeNeutralGranted = false;
            if (!m_Ledger.Carrier.IsValid && !m_EscapeNeutralGranted)
            {
                m_EscapeWinner = m_NextEscapeTeam;
                m_NextEscapeTeam = m_NextEscapeTeam == Team.Red ? Team.Navy : Team.Red;
                m_NeutralEscapeGrants[(int)m_EscapeWinner]++;
                m_EscapeNeutralGranted = true;
            }

            if (!wasActive
                || sequenceBefore != m_ContestedEscape.Sequence
                || !IsUsableContender(Team.Red, m_RedEscapeSlot, ballPosition)
                || !IsUsableContender(Team.Navy, m_NavyEscapeSlot, ballPosition))
            {
                m_RedEscapeSlot = redSlot;
                m_NavyEscapeSlot = navySlot;
            }
        }

        void UpdateBoundaryEscape(float deltaTime)
        {
            var snapshot = matchController.Snapshot;
            var ballPosition = new Vector2(m_Body.position.x, m_Body.position.z);
            if (IsBoundaryEscapeActive)
            {
                m_BoundaryEscapeRemainingSeconds = Mathf.Max(
                    0f, m_BoundaryEscapeRemainingSeconds - deltaTime);
                m_BoundaryRetreatRemainingSeconds = Mathf.Max(
                    0f, m_BoundaryRetreatRemainingSeconds - deltaTime);
                if (m_BoundaryEscapeRemainingSeconds <= 0f
                    || Vector2.Distance(ballPosition, m_BoundaryEscapeOrigin)
                    >= BoundaryReleaseDistance)
                    ResetBoundaryEscapeState();
                return;
            }

            if (!IsNearBoundary(
                    ballPosition, snapshot.FieldHalfLength, snapshot.FieldHalfWidth)
                || new Vector2(m_Body.linearVelocity.x, m_Body.linearVelocity.z).magnitude
                > BoundaryStationarySpeed)
            {
                m_BoundaryStationarySeconds = 0f;
                return;
            }

            m_BoundaryStationarySeconds += deltaTime;
            if (m_BoundaryStationarySeconds < BoundaryStationarySeconds
                || !TrySelectBoundaryEscapePlayer(
                    ballPosition, out var team, out var slot))
                return;

            var direction = CalculateBoundaryEscapeDirection(
                ballPosition,
                team,
                snapshot.FieldHalfLength,
                snapshot.FieldHalfWidth);
            m_BoundaryEscapeTeam = team;
            m_BoundaryEscapeSlot = slot;
            m_BoundaryEscapeOrigin = ballPosition;
            m_BoundaryEscapeRemainingSeconds = BoundaryEscapeSeconds;
            m_BoundaryRetreatRemainingSeconds = BoundaryRetreatSeconds;
            m_BoundaryRetreatTarget = ClampToField(
                ballPosition - direction * BoundaryRetreatDistance, snapshot);
            m_BoundaryKickTarget = ClampToField(
                ballPosition + direction * BoundaryKickDistance, snapshot);
            m_BoundaryStationarySeconds = 0f;
            m_BoundaryEscapeActivationCount++;
            m_Body.WakeUp();

            var carrier = m_Ledger.Carrier;
            if (!carrier.IsValid || carrier.Team != team || carrier.Slot != slot) return;
            var index = GetIndex(team, slot);
            m_Ledger.ReleaseForControlLoss(team, slot);
            m_StopBlocked[index] = true;
            m_ContactGraceSeconds[index] = 0f;
            m_NonForwardSeconds[index] = 0f;
            PublishPossession();
        }

        bool TrySelectBoundaryEscapePlayer(
            Vector2 ballPosition,
            out Team selectedTeam,
            out int selectedSlot)
        {
            var carrier = m_Ledger?.Carrier ?? MNG_CarrierRef.None;
            if (carrier.IsValid)
            {
                var carrierAvatar = matchController.GetPlayerAvatar(
                    carrier.Team, carrier.Slot);
                if (carrierAvatar.gameObject.activeInHierarchy && !carrierAvatar.IsHuman)
                {
                    selectedTeam = carrier.Team;
                    selectedSlot = carrier.Slot;
                    return true;
                }
            }

            selectedTeam = Team.Red;
            selectedSlot = -1;
            var bestDistance = float.PositiveInfinity;
            for (var teamIndex = 0; teamIndex < MNG_MatchSnapshot.TeamCount; teamIndex++)
            {
                var team = teamIndex == 0 ? Team.Red : Team.Navy;
                for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
                {
                    var avatar = matchController.GetPlayerAvatar(team, slot);
                    if (!avatar.gameObject.activeInHierarchy || avatar.IsHuman) continue;
                    var playerPosition = new Vector2(
                        avatar.Body.position.x, avatar.Body.position.z);
                    var distance = Vector2.Distance(playerPosition, ballPosition);
                    bestDistance = Mathf.Min(bestDistance, distance);
                }
            }
            // Anchor the tie band to the global minimum, not a previously selected
            // near-tie. Pairwise epsilon comparisons are not transitive.
            for (var teamIndex = 0; teamIndex < MNG_MatchSnapshot.TeamCount; teamIndex++)
            {
                var team = teamIndex == 0 ? Team.Red : Team.Navy;
                for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
                {
                    var avatar = matchController.GetPlayerAvatar(team, slot);
                    if (!avatar.gameObject.activeInHierarchy || avatar.IsHuman) continue;
                    var distance = Vector2.Distance(new Vector2(
                        avatar.Body.position.x, avatar.Body.position.z), ballPosition);
                    if (distance > bestDistance + MNG_PossessionLedger.TieDistanceTolerance) continue;
                    if (selectedSlot >= 0
                        && (team != m_Ledger.NeutralTieTeam || selectedTeam == team)) continue;
                    selectedTeam = team;
                    selectedSlot = slot;
                }
            }
            return selectedSlot >= 0;
        }

        static Vector2 ClampToField(
            Vector2 point,
            MNG_MatchSnapshot snapshot)
            => new(
                Mathf.Clamp(point.x,
                    -snapshot.FieldHalfLength + 2f,
                    snapshot.FieldHalfLength - 2f),
                Mathf.Clamp(point.y,
                    -snapshot.FieldHalfWidth + 2f,
                    snapshot.FieldHalfWidth - 2f));

        void ResetBoundaryEscapeState()
        {
            m_BoundaryStationarySeconds = 0f;
            m_BoundaryEscapeRemainingSeconds = 0f;
            m_BoundaryRetreatRemainingSeconds = 0f;
            m_BoundaryEscapeOrigin = default;
            m_BoundaryRetreatTarget = default;
            m_BoundaryKickTarget = default;
            m_BoundaryEscapeSlot = -1;
        }

        int FindNearestContender(Team team, Vector2 ballPosition)
        {
            var nearestSlot = -1;
            var nearestDistance = float.MaxValue;
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var avatar = matchController.GetPlayerAvatar(team, slot);
                if (!avatar.gameObject.activeInHierarchy) continue;
                var playerPosition = new Vector2(avatar.Body.position.x, avatar.Body.position.z);
                var distance = Vector2.Distance(playerPosition, ballPosition);
                if (distance > ContenderRadius || distance >= nearestDistance) continue;
                nearestSlot = slot;
                nearestDistance = distance;
            }
            return nearestSlot;
        }

        bool IsUsableContender(Team team, int slot, Vector2 ballPosition)
        {
            if (slot < 0 || slot >= MNG_MatchSnapshot.PlayersPerTeam) return false;
            var avatar = matchController.GetPlayerAvatar(team, slot);
            if (!avatar.gameObject.activeInHierarchy) return false;
            return Vector2.Distance(
                new Vector2(avatar.Body.position.x, avatar.Body.position.z),
                ballPosition) <= ContenderRadius;
        }

        void PublishPossession()
        {
            var carrier = m_Ledger != null ? m_Ledger.Carrier : MNG_CarrierRef.None;
            if (!carrier.IsValid)
            {
                matchController.SetPossession(MNG_Possession.Neutral, MNG_CarrierRef.None);
                return;
            }
            matchController.SetPossession(
                carrier.Team == Team.Red ? MNG_Possession.Red : MNG_Possession.Navy,
                carrier);
        }

        void InitializeLedger()
        {
            if (profile == null) return;
            profile.ValidateOrThrow();
            m_Ledger = new MNG_PossessionLedger(
                profile.ReleasePadding,
                profile.ReleaseDelaySeconds,
                profile.PossessionConfirmationSeconds,
                profile.PreviousOwnerLockSeconds);
        }

        void ValidateBindingsOrThrow()
        {
            if (profile == null) throw new InvalidOperationException("MNG ball control requires a physics profile.");
            if (matchController == null) throw new InvalidOperationException("MNG ball control requires a match controller.");
            if (Mathf.Abs(m_Body.mass - profile.BallMass) > 0.0001f)
                throw new InvalidOperationException($"MNG ball mass must be {profile.BallMass}, found {m_Body.mass}.");
        }

        static int GetIndex(Team team, int slot)
            => (team == Team.Red ? 0 : MNG_MatchSnapshot.PlayersPerTeam) + slot;

    }
}
