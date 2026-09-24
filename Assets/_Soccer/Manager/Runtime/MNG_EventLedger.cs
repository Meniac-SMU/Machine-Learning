using System;
using System.Collections.Generic;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public enum MNG_RewardEventKind
    {
        GoalFor = 0,
        GoalAgainst = 1,
        MatchWin = 2,
        MatchLoss = 3,
        ValidShot = 4,
        CompletedPass = 5,
        AdvancedFiveMeters = 6,
        Recovery = 7,
        FastRecovery = 8,
        Crowding = 9,
        BlockedForwardPassDecision = 10
    }

    public enum MNG_TacticProfile
    {
        Base = 0,
        Attack = 1,
        Defense = 2,
        Press = 3
    }

    public readonly struct MNG_RewardEvent
    {
        public readonly MNG_RewardEventKind Kind;
        public readonly long EventId;

        public MNG_RewardEvent(MNG_RewardEventKind kind, long eventId)
        {
            Kind = kind;
            EventId = eventId;
        }
    }

    /// <summary>
    /// Deduplicates event ids and applies both the per-kind and aggregate absolute
    /// shaping caps over a rolling 60 simulated-second window.
    /// </summary>
    public sealed class MNG_EventLedger
    {
        const float WindowSeconds = 60f;
        const int MaximumWindowRecords = 512;

        readonly HashSet<ulong> m_RecordedEvents = new HashSet<ulong>();
        readonly WindowRecord[] m_Window = new WindowRecord[MaximumWindowRecords];
        int m_WindowStart;
        int m_WindowCount;
        readonly long[] m_RawCounts = new long[11];
        readonly long[] m_RewardedCounts = new long[11];
        readonly long[] m_CappedCounts = new long[11];
        public long RawCount(MNG_RewardEventKind kind) => m_RawCounts[(int)kind];
        public long RewardedCount(MNG_RewardEventKind kind) => m_RewardedCounts[(int)kind];
        public long CappedCount(MNG_RewardEventKind kind) => m_CappedCounts[(int)kind];

        struct WindowRecord
        {
            public float Time;
            public MNG_RewardEventKind Kind;
            public float AbsoluteAmount;
        }

        public int RecordedEventCount => m_RecordedEvents.Count;

        public bool TryRecord(
            MNG_RewardEvent rewardEvent,
            MNG_RewardProfile profile,
            float simulatedTime,
            out float awardedAmount)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (rewardEvent.EventId < 0) throw new ArgumentOutOfRangeException(nameof(rewardEvent.EventId));
            if (!MNG_MatchSnapshot.IsFinite(simulatedTime) || simulatedTime < 0f)
                throw new ArgumentOutOfRangeException(nameof(simulatedTime));

            awardedAmount = 0f;
            var key = ((ulong)(uint)rewardEvent.Kind << 56) | (ulong)rewardEvent.EventId;
            if (!m_RecordedEvents.Add(key)) return false;
            m_RawCounts[(int)rewardEvent.Kind]++;

            var nominal = profile.GetNominalReward(rewardEvent.Kind);
            if (!profile.IsShaping(rewardEvent.Kind))
            {
                awardedAmount = nominal;
                if (awardedAmount != 0) m_RewardedCounts[(int)rewardEvent.Kind]++;
                return true;
            }

            Prune(simulatedTime);
            var totalUsed = 0f;
            var kindUsed = 0f;
            for (var i = 0; i < m_WindowCount; i++)
            {
                var record = m_Window[(m_WindowStart + i) % MaximumWindowRecords];
                totalUsed += record.AbsoluteAmount;
                if (record.Kind == rewardEvent.Kind) kindUsed += record.AbsoluteAmount;
            }

            var requested = Mathf.Abs(nominal);
            var totalAvailable = Mathf.Max(0f, profile.TotalShapingCapPerMinute - totalUsed);
            var kindAvailable = Mathf.Max(0f, profile.GetPerKindCap(rewardEvent.Kind) - kindUsed);
            var allowed = Mathf.Min(requested, totalAvailable, kindAvailable);
            awardedAmount = Mathf.Sign(nominal) * allowed;
            if (allowed < requested) m_CappedCounts[(int)rewardEvent.Kind]++;
            if (awardedAmount != 0) m_RewardedCounts[(int)rewardEvent.Kind]++;
            if (allowed > 0f) Push(simulatedTime, rewardEvent.Kind, allowed);
            return true;
        }

        public void ResetEpisode()
        {
            m_RecordedEvents.Clear();
            Array.Clear(m_RawCounts, 0, m_RawCounts.Length);
            Array.Clear(m_RewardedCounts, 0, m_RewardedCounts.Length);
            Array.Clear(m_CappedCounts, 0, m_CappedCounts.Length);
            m_WindowStart = 0;
            m_WindowCount = 0;
        }

        void Prune(float now)
        {
            while (m_WindowCount > 0)
            {
                var record = m_Window[m_WindowStart];
                if (now - record.Time < WindowSeconds) break;
                m_WindowStart = (m_WindowStart + 1) % MaximumWindowRecords;
                m_WindowCount--;
            }
        }

        void Push(float time, MNG_RewardEventKind kind, float amount)
        {
            if (m_WindowCount == MaximumWindowRecords)
            {
                m_WindowStart = (m_WindowStart + 1) % MaximumWindowRecords;
                m_WindowCount--;
            }
            var index = (m_WindowStart + m_WindowCount) % MaximumWindowRecords;
            m_Window[index] = new WindowRecord { Time = time, Kind = kind, AbsoluteAmount = amount };
            m_WindowCount++;
        }
    }
}
