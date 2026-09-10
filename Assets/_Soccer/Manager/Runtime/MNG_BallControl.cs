using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(Collider))]
    public sealed class MNG_BallControl : MonoBehaviour
    {
        const float ReceptionContactGraceSeconds = 0.24f;
        const float MaximumReceptionRelativeSpeed = 18f;
        const float NonForwardReleaseSeconds = 0.08f;
        const float ContenderRadius = 2.60f;
        const float EscapeForwardDistance = 1.25f;
        const float EscapeLateralDistance = 3.50f;
        const float YieldRadialDistance = 3.00f;
        const float YieldLateralDistance = 2.50f;

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
        readonly MNG_PossessionCandidate[] m_Candidates =
            new MNG_PossessionCandidate[MNG_MatchSnapshot.PlayersPerTeam * MNG_MatchSnapshot.TeamCount];

        Rigidbody m_Body;
        Collider m_BallCollider;
        MNG_PossessionLedger m_Ledger;
        readonly MNG_ContestedBallEscape m_ContestedEscape = new();
        long m_KickId;
        int m_RedEscapeSlot = -1;
        int m_NavyEscapeSlot = -1;

        public MNG_CarrierRef Carrier => m_Ledger?.Carrier ?? MNG_CarrierRef.None;
        public long LatestKickId => m_KickId;
        public MNG_PossessionLedger Ledger => m_Ledger;
        public bool DribbleCorrectionAppliedLastStep { get; private set; }
        public bool IsContestedEscapeActive => m_ContestedEscape.IsActive;
        public int ContestedEscapeActivationCount => m_ContestedEscape.ActivationCount;

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
        }

        void FixedUpdate()
        {
            DribbleCorrectionAppliedLastStep = false;
            if (m_Ledger == null) return;
            if (!matchController.IsPlayActive)
            {
                Array.Clear(m_Contacts, 0, m_Contacts.Length);
                Array.Clear(m_ContactGraceSeconds, 0, m_ContactGraceSeconds.Length);
                Array.Clear(m_NonForwardSeconds, 0, m_NonForwardSeconds.Length);
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
                    if (plate != null && plate.IsForwardDriveActive())
                        m_StopBlocked[index] = false;
                    var acquisitionBlocked = m_StopBlocked[index] || m_KickBlocked[index];
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
            ApplyDribble();
            ClampBallSpeed();
            Array.Clear(m_Contacts, 0, m_Contacts.Length);
            for (var i = 0; i < m_ContactGraceSeconds.Length; i++)
            {
                m_ContactGraceSeconds[i] = Mathf.Max(0f,
                    m_ContactGraceSeconds[i] - Time.fixedDeltaTime);
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
        }

        public bool TryKick(Team team, int slot, Vector3 target, float exitSpeed, out long kickId)
        {
            kickId = 0;
            if (m_Ledger == null || !m_Ledger.Carrier.IsValid
                || m_Ledger.Carrier.Team != team || m_Ledger.Carrier.Slot != slot)
                return false;
            var avatar = matchController.GetPlayerAvatar(team, slot);
            if (avatar.KickCooldownSeconds > 0f) return false;
            var plate = avatar.KickPlate;
            if (plate == null || !plate.TryArmKick(target, exitSpeed)) return false;
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
            m_ContestedEscape.Reset();
            m_RedEscapeSlot = -1;
            m_NavyEscapeSlot = -1;
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

        public bool TryGetContestedEscapeTarget(Team team, int slot, out Vector2 target)
        {
            target = default;
            if (!m_ContestedEscape.IsActive) return false;
            var selectedSlot = team == Team.Red ? m_RedEscapeSlot : m_NavyEscapeSlot;
            if (slot != selectedSlot) return false;

            var snapshot = matchController.Snapshot;
            var teamSign = team == Team.Red ? 1f : -1f;
            var winner = (m_ContestedEscape.Sequence & 1) == 1 ? Team.Red : Team.Navy;
            var lateralSign = ((m_ContestedEscape.Sequence / 2) & 1) == 0 ? 1f : -1f;
            if (team == winner)
            {
                target = m_ContestedEscape.EscapeOrigin
                    + new Vector2(teamSign * EscapeForwardDistance, lateralSign * EscapeLateralDistance);
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
            m_Body.AddForce(impulse, ForceMode.Impulse);
        }

        void ApplyDribble()
        {
            var carrier = m_Ledger.Carrier;
            if (!carrier.IsValid) return;
            var avatar = matchController.GetPlayerAvatar(carrier.Team, carrier.Slot);
            var plate = avatar.KickPlate;
            if (plate == null || !plate.IsForwardDriveActive()
                || !plate.IsWithinReleasePosition(m_Body.position))
                return;
            var target = plate.DribblePosition;
            target.y = m_Body.position.y;
            var error = target - m_Body.position;
            error.y = 0f;
            var relativeVelocity = avatar.Body.linearVelocity - m_Body.linearVelocity;
            relativeVelocity.y = 0f;
            var acceleration = error * profile.DribbleAcceleration
                + relativeVelocity * profile.DribbleDampingPerSecond;
            acceleration = Vector3.ClampMagnitude(acceleration, profile.DribbleAcceleration);
            m_Body.AddForce(acceleration, ForceMode.Acceleration);
            DribbleCorrectionAppliedLastStep = true;
        }

        void ApplyPlateStrike(MNG_PlayerAvatar avatar, int index, MNG_KickRequest kick)
        {
            if (!m_Ledger.Carrier.IsValid
                || m_Ledger.Carrier.Team != avatar.Team
                || m_Ledger.Carrier.Slot != avatar.Slot)
                return;
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
            PublishPossession();
        }

        void ReleaseCarrierWhenForwardDriveStops(float deltaTime)
        {
            var carrier = m_Ledger.Carrier;
            if (!carrier.IsValid) return;
            var avatar = matchController.GetPlayerAvatar(carrier.Team, carrier.Slot);
            var plate = avatar.KickPlate;
            var index = GetIndex(carrier.Team, carrier.Slot);
            if (plate != null && (plate.IsForwardDriveActive() || plate.HasPendingKick))
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

            if (!wasActive
                || sequenceBefore != m_ContestedEscape.Sequence
                || !IsUsableContender(Team.Red, m_RedEscapeSlot, ballPosition)
                || !IsUsableContender(Team.Navy, m_NavyEscapeSlot, ballPosition))
            {
                m_RedEscapeSlot = redSlot;
                m_NavyEscapeSlot = navySlot;
            }
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
