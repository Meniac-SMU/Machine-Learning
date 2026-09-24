using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    /// <summary>
    /// Converts code-executed football outcomes into manager learning events.
    /// It never rewards the command itself: a physical plate strike, reception,
    /// or measured ball advance must happen first.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class MNG_TacticalRewardTracker : MonoBehaviour
    {
        public const float CompletedPassMinimumTravel = 2.5f;
        public const float CompletedPassHoldSeconds = 0.15f;
        public const float PendingPassTimeoutSeconds = 4f;
        public const float AdvanceRewardMeters = 5f;
        public const float PossessionContinuationGraceSeconds = 4f;

        [SerializeField] MNG_MatchController matchController;
        [SerializeField] MNG_BallControl ballControl;
        [SerializeField] MNG_RewardEngine rewardEngine;

        readonly PendingPass[] m_Passes = { new PendingPass(), new PendingPass() };
        readonly PossessionCycle[] m_Cycles = { new PossessionCycle(), new PossessionCycle() };
        readonly int[] m_ValidShotCounts = new int[2];
        readonly int[] m_PassStrikeCounts = new int[2];
        readonly int[] m_ShotStrikeCounts = new int[2];
        readonly int[] m_CompletedPassCounts = new int[2];
        readonly int[] m_AdvanceRewardCounts = new int[2];
        readonly int[] m_IntendedReceptionCounts = new int[2];
        readonly MNG_ObservedRecovery[] m_ObservedRecoveries = { new(), new() };
        public int GetObservedRecoveryCount(Team team) => m_ObservedRecoveries[TeamIndex(team)].Count;
        readonly System.Collections.Generic.HashSet<long> m_SeenKickIds = new();
        public int GetIntendedReceptionCount(Team team) => m_IntendedReceptionCounts[TeamIndex(team)];
        public int? ObservedOnTargetCount => null;
        public int? ObservedBlockedCount => null;
        public int? ObservedSaveCount => null;

        long m_EpisodeId = long.MinValue;
        long m_AdvanceEventSequence;
        bool m_Subscribed;

        public int ValidShotCount => m_ValidShotCounts[0] + m_ValidShotCounts[1];
        public int PassStrikeCount => m_PassStrikeCounts[0] + m_PassStrikeCounts[1];
        public int ShotStrikeCount => m_ShotStrikeCounts[0] + m_ShotStrikeCounts[1];
        public int CompletedPassCount => m_CompletedPassCounts[0] + m_CompletedPassCounts[1];
        public int AdvanceRewardCount => m_AdvanceRewardCounts[0] + m_AdvanceRewardCounts[1];

        public int GetValidShotCount(Team team) => m_ValidShotCounts[TeamIndex(team)];
        public int GetPassStrikeCount(Team team) => m_PassStrikeCounts[TeamIndex(team)];
        public int GetShotStrikeCount(Team team) => m_ShotStrikeCounts[TeamIndex(team)];
        public int GetCompletedPassCount(Team team) => m_CompletedPassCounts[TeamIndex(team)];
        public int GetAdvanceRewardCount(Team team) => m_AdvanceRewardCounts[TeamIndex(team)];

        sealed class PendingPass
        {
            public bool Active;
            public long KickId;
            public int PasserSlot;
            public int ReceiverSlot = -1;
            public int IntendedReceiverSlot = -1;
            public Vector2 Origin;
            public float StartedAt;
            public float StableReceptionSeconds;
        }

        sealed class PossessionCycle
        {
            public bool Active;
            public float NeutralSeconds;
            public float NextRewardDepth;
        }

        void Awake()
        {
            matchController ??= GetComponent<MNG_MatchController>();
            rewardEngine ??= GetComponent<MNG_RewardEngine>();
            ballControl ??= GetComponentInChildren<MNG_BallControl>();
            ValidateBindingsOrThrow();
        }

        void OnEnable() => Subscribe();

        void OnDisable()
        {
            if (!m_Subscribed || ballControl == null) return;
            ballControl.PlateStrikeApplied -= OnPlateStrikeApplied;
            m_Subscribed = false;
        }

        public void Configure(
            MNG_MatchController configuredMatch,
            MNG_BallControl configuredBall,
            MNG_RewardEngine configuredRewardEngine)
        {
            if (m_Subscribed && ballControl != null)
                ballControl.PlateStrikeApplied -= OnPlateStrikeApplied;
            m_Subscribed = false;
            matchController = configuredMatch ?? throw new ArgumentNullException(nameof(configuredMatch));
            ballControl = configuredBall ?? throw new ArgumentNullException(nameof(configuredBall));
            rewardEngine = configuredRewardEngine ?? throw new ArgumentNullException(nameof(configuredRewardEngine));
            if (isActiveAndEnabled) Subscribe();
        }

        void FixedUpdate()
        {
            if (!matchController.TryGetSnapshot(out var snapshot)) return;
            if (snapshot.EpisodeId != m_EpisodeId) ResetForEpisode(snapshot.EpisodeId);

            if (matchController.UseRuntimeV2)
                for (var i = 0; i < 2; i++)
                    m_ObservedRecoveries[i].Step(snapshot.Possession == PossessionFor((Team)i),
                        snapshot.Possession == PossessionFor(Opponent((Team)i)),
                        matchController.IsPlayActive, Time.fixedDeltaTime);
            UpdatePendingPass(snapshot, Team.Red, Time.fixedDeltaTime);
            UpdatePendingPass(snapshot, Team.Navy, Time.fixedDeltaTime);
            UpdateAdvance(snapshot, Team.Red, Time.fixedDeltaTime);
            UpdateAdvance(snapshot, Team.Navy, Time.fixedDeltaTime);
        }

        void OnPlateStrikeApplied(MNG_PlateStrikeEvent strike)
        {
            if (!matchController.TryGetSnapshot(out var snapshot)) return;
            if (snapshot.EpisodeId != m_EpisodeId) ResetForEpisode(snapshot.EpisodeId);

            var teamIndex = TeamIndex(strike.Team);
            if (!m_SeenKickIds.Add(strike.KickId)) return;
            if (strike.Intent == MNG_KickIntent.Pass) m_PassStrikeCounts[teamIndex]++;
            else if (strike.Intent == MNG_KickIntent.Shot) m_ShotStrikeCounts[teamIndex]++;

            if (strike.Intent == MNG_KickIntent.Shot
                && IsGoalOpeningTrajectory(snapshot, strike.Team, strike.Origin, strike.Target))
            {
                if (rewardEngine.Award(strike.Team, MNG_RewardEventKind.ValidShot, strike.KickId) != 0f)
                    m_ValidShotCounts[teamIndex]++;
            }

            var pending = m_Passes[TeamIndex(strike.Team)];
            pending.Active = strike.Intent == MNG_KickIntent.Pass;
            pending.KickId = strike.KickId;
            pending.PasserSlot = strike.Slot;
            pending.ReceiverSlot = -1;
            pending.IntendedReceiverSlot = strike.IntendedReceiverSlot;
            pending.Origin = strike.Origin;
            pending.StartedAt = snapshot.EpisodeElapsedSeconds;
            pending.StableReceptionSeconds = 0f;
        }

        void UpdatePendingPass(MNG_MatchSnapshot snapshot, Team team, float deltaTime)
        {
            var pending = m_Passes[TeamIndex(team)];
            if (!pending.Active) return;
            if (snapshot.EpisodeElapsedSeconds - pending.StartedAt > PendingPassTimeoutSeconds
                || snapshot.Possession == PossessionFor(Opponent(team)))
            {
                pending.Active = false;
                return;
            }

            if (snapshot.Possession != PossessionFor(team)
                || !snapshot.Carrier.IsValid
                || snapshot.Carrier.Team != team)
            {
                pending.ReceiverSlot = -1;
                pending.StableReceptionSeconds = 0f;
                return;
            }

            if (snapshot.Carrier.Slot == pending.PasserSlot)
            {
                pending.Active = false;
                return;
            }

            if (Vector2.Distance(snapshot.BallPosition, pending.Origin) < CompletedPassMinimumTravel)
            {
                pending.StableReceptionSeconds = 0f;
                return;
            }

            if (pending.ReceiverSlot != snapshot.Carrier.Slot)
            {
                pending.ReceiverSlot = snapshot.Carrier.Slot;
                pending.StableReceptionSeconds = 0f;
            }
            pending.StableReceptionSeconds += deltaTime;
            if (pending.StableReceptionSeconds < CompletedPassHoldSeconds) return;
            if (pending.ReceiverSlot == pending.IntendedReceiverSlot)
                m_IntendedReceptionCounts[TeamIndex(team)]++;

            if (rewardEngine.Award(team, MNG_RewardEventKind.CompletedPass, pending.KickId) != 0f)
                m_CompletedPassCounts[TeamIndex(team)]++;
            pending.Active = false;
        }

        void UpdateAdvance(MNG_MatchSnapshot snapshot, Team team, float deltaTime)
        {
            var cycle = m_Cycles[TeamIndex(team)];
            var ownPossession = snapshot.Possession == PossessionFor(team)
                && snapshot.Carrier.IsValid
                && snapshot.Carrier.Team == team;
            if (ownPossession)
            {
                var attackSign = team == Team.Red ? 1f : -1f;
                var depth = snapshot.BallPosition.x * attackSign;
                if (!cycle.Active || cycle.NeutralSeconds > PossessionContinuationGraceSeconds)
                {
                    cycle.Active = true;
                    cycle.NextRewardDepth = depth + AdvanceRewardMeters;
                }
                cycle.NeutralSeconds = 0f;
                while (depth >= cycle.NextRewardDepth)
                {
                    var eventId = NextAdvanceEventId(snapshot.EpisodeId, team);
                    if (rewardEngine.Award(team, MNG_RewardEventKind.AdvancedFiveMeters, eventId) != 0f)
                        m_AdvanceRewardCounts[TeamIndex(team)]++;
                    cycle.NextRewardDepth += AdvanceRewardMeters;
                }
                return;
            }

            if (snapshot.Possession == PossessionFor(Opponent(team)))
            {
                cycle.Active = false;
                cycle.NeutralSeconds = 0f;
            }
            else if (cycle.Active)
            {
                cycle.NeutralSeconds += deltaTime;
            }
        }

        long NextAdvanceEventId(long episodeId, Team team)
        {
            m_AdvanceEventSequence++;
            return Math.Max(0L, episodeId) * 1_000_000L
                + TeamIndex(team) * 100_000L
                + m_AdvanceEventSequence;
        }

        void ResetForEpisode(long episodeId)
        {
            m_EpisodeId = episodeId;
            m_SeenKickIds.Clear();
            foreach (var recovery in m_ObservedRecoveries) recovery.ResetWindow();
            m_AdvanceEventSequence = 0;
            foreach (var pending in m_Passes)
            {
                pending.Active = false;
                pending.ReceiverSlot = -1;
                pending.StableReceptionSeconds = 0f;
            }
            foreach (var cycle in m_Cycles)
            {
                cycle.Active = false;
                cycle.NeutralSeconds = 0f;
                cycle.NextRewardDepth = 0f;
            }
        }

        void Subscribe()
        {
            if (m_Subscribed || ballControl == null) return;
            ballControl.PlateStrikeApplied += OnPlateStrikeApplied;
            m_Subscribed = true;
        }

        void ValidateBindingsOrThrow()
        {
            if (matchController == null || ballControl == null || rewardEngine == null)
                throw new InvalidOperationException(
                    "MNG tactical reward tracker requires match, ball control, and reward engine bindings.");
        }

        public static bool IsGoalOpeningTrajectory(
            MNG_MatchSnapshot snapshot,
            Team team,
            Vector2 origin,
            Vector2 target)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var goalX = (team == Team.Red ? 1f : -1f) * snapshot.FieldHalfLength;
            var direction = target - origin;
            if (Mathf.Abs(direction.x) <= 0.0001f) return false;
            var t = (goalX - origin.x) / direction.x;
            if (t <= 0f) return false;
            var crossingY = origin.y + direction.y * t;
            return Mathf.Abs(crossingY) <= snapshot.GoalHalfWidth;
        }

        static int TeamIndex(Team team) => team == Team.Red ? 0 : 1;
        static Team Opponent(Team team) => team == Team.Red ? Team.Navy : Team.Red;
        static MNG_Possession PossessionFor(Team team)
            => team == Team.Red ? MNG_Possession.Red : MNG_Possession.Navy;
    }
}
