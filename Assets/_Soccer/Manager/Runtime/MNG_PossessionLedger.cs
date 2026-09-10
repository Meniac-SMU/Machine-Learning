using System;
using MachineLearning.Soccer;

namespace MachineLearning.Soccer.Manager
{
    public struct MNG_PossessionCandidate
    {
        public Team Team;
        public int Slot;
        public bool Active;
        public bool HasPhysicalContact;
        public bool IsInControlZone;
        public float CenterDistance;
        public float AcquisitionDistance;
    }

    public readonly struct MNG_PossessionChange
    {
        public readonly long EventId;
        public readonly MNG_CarrierRef Previous;
        public readonly MNG_CarrierRef Current;

        public MNG_PossessionChange(long eventId, MNG_CarrierRef previous, MNG_CarrierRef current)
        {
            EventId = eventId;
            Previous = previous;
            Current = current;
        }
    }

    /// <summary>
    /// Sole carrier authority. Candidates must have real plate contact or be
    /// inside the plate's tight dribble-control zone.
    /// </summary>
    public sealed class MNG_PossessionLedger
    {
        readonly float m_ReleasePadding;
        readonly float m_ReleaseDelay;
        readonly float m_ConfirmationDelay;
        readonly float m_PreviousOwnerLock;

        MNG_CarrierRef m_Carrier = MNG_CarrierRef.None;
        MNG_CarrierRef m_Pending = MNG_CarrierRef.None;
        MNG_CarrierRef m_LockedPrevious = MNG_CarrierRef.None;
        float m_PendingSeconds;
        float m_ReleaseSeconds;
        float m_PreviousOwnerLockRemaining;
        long m_EventId;

        public MNG_CarrierRef Carrier => m_Carrier;
        public long LatestEventId => m_EventId;
        public event Action<MNG_PossessionChange> Changed;

        public MNG_PossessionLedger(
            float releasePadding = 0.40f,
            float releaseDelaySeconds = 0.15f,
            float confirmationDelaySeconds = 0.20f,
            float previousOwnerLockSeconds = 0.20f)
        {
            if (releasePadding < 0f) throw new ArgumentOutOfRangeException(nameof(releasePadding));
            if (releaseDelaySeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(releaseDelaySeconds));
            if (confirmationDelaySeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(confirmationDelaySeconds));
            if (previousOwnerLockSeconds <= 0f) throw new ArgumentOutOfRangeException(nameof(previousOwnerLockSeconds));
            m_ReleasePadding = releasePadding;
            m_ReleaseDelay = releaseDelaySeconds;
            m_ConfirmationDelay = confirmationDelaySeconds;
            m_PreviousOwnerLock = previousOwnerLockSeconds;
        }

        public void Update(MNG_PossessionCandidate[] candidates, int count, float deltaTime)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));
            if (count < 0 || count > candidates.Length) throw new ArgumentOutOfRangeException(nameof(count));
            if (!MNG_MatchSnapshot.IsFinite(deltaTime) || deltaTime <= 0f)
                throw new ArgumentOutOfRangeException(nameof(deltaTime));

            m_PreviousOwnerLockRemaining = Math.Max(0f, m_PreviousOwnerLockRemaining - deltaTime);
            var currentSatisfiesRelease = false;
            var best = MNG_CarrierRef.None;
            var bestDistance = float.PositiveInfinity;

            for (var i = 0; i < count; i++)
            {
                var candidate = candidates[i];
                ValidateCandidate(candidate);
                if (!candidate.Active) continue;
                var hasControl = candidate.HasPhysicalContact || candidate.IsInControlZone;
                var sameAsCarrier = IsSame(m_Carrier, candidate.Team, candidate.Slot);
                if (sameAsCarrier
                    && hasControl
                    && candidate.CenterDistance <= candidate.AcquisitionDistance + m_ReleasePadding)
                {
                    currentSatisfiesRelease = true;
                }

                if (!hasControl || candidate.CenterDistance > candidate.AcquisitionDistance)
                    continue;
                if (m_PreviousOwnerLockRemaining > 0f
                    && IsSame(m_LockedPrevious, candidate.Team, candidate.Slot))
                    continue;

                if (candidate.CenterDistance < bestDistance
                    || (Math.Abs(candidate.CenterDistance - bestDistance) <= 0.00001f
                        && PreferCandidate(candidate.Team, candidate.Slot, best)))
                {
                    best = MNG_CarrierRef.For(candidate.Team, candidate.Slot);
                    bestDistance = candidate.CenterDistance;
                }
            }

            if (m_Carrier.IsValid && !currentSatisfiesRelease)
            {
                m_ReleaseSeconds += deltaTime;
                if (m_ReleaseSeconds >= m_ReleaseDelay)
                    ChangeCarrier(MNG_CarrierRef.None, true);
            }
            else
            {
                m_ReleaseSeconds = 0f;
            }

            if (!best.IsValid || (m_Carrier.IsValid && Same(m_Carrier, best)))
            {
                ClearPending();
                return;
            }

            if (!Same(m_Pending, best))
            {
                m_Pending = best;
                m_PendingSeconds = 0f;
            }
            m_PendingSeconds += deltaTime;
            if (m_PendingSeconds >= m_ConfirmationDelay)
            {
                ChangeCarrier(best, true);
                ClearPending();
            }
        }

        public void ReleaseForKick(Team team, int slot)
        {
            if (!IsSame(m_Carrier, team, slot)) return;
            ChangeCarrier(MNG_CarrierRef.None, true);
            ClearPending();
        }

        public void ReleaseForControlLoss(Team team, int slot)
        {
            if (!IsSame(m_Carrier, team, slot)) return;
            ChangeCarrier(MNG_CarrierRef.None, true);
            ClearPending();
        }

        public void Reset()
        {
            m_Carrier = MNG_CarrierRef.None;
            m_Pending = MNG_CarrierRef.None;
            m_LockedPrevious = MNG_CarrierRef.None;
            m_PendingSeconds = 0f;
            m_ReleaseSeconds = 0f;
            m_PreviousOwnerLockRemaining = 0f;
        }

        void ChangeCarrier(MNG_CarrierRef next, bool lockPrevious)
        {
            if (Same(m_Carrier, next)) return;
            var previous = m_Carrier;
            m_Carrier = next;
            m_ReleaseSeconds = 0f;
            if (lockPrevious && previous.IsValid)
            {
                m_LockedPrevious = previous;
                m_PreviousOwnerLockRemaining = m_PreviousOwnerLock;
            }
            m_EventId++;
            Changed?.Invoke(new MNG_PossessionChange(m_EventId, previous, next));
        }

        bool PreferCandidate(Team team, int slot, MNG_CarrierRef incumbentBest)
        {
            if (IsSame(m_Carrier, team, slot)) return true;
            if (incumbentBest.IsValid && Same(m_Carrier, incumbentBest)) return false;
            var candidateIndex = (team == Team.Red ? 0 : MNG_MatchSnapshot.PlayersPerTeam) + slot;
            var bestIndex = incumbentBest.IsValid
                ? (incumbentBest.Team == Team.Red ? 0 : MNG_MatchSnapshot.PlayersPerTeam) + incumbentBest.Slot
                : int.MaxValue;
            return candidateIndex < bestIndex;
        }

        static bool Same(MNG_CarrierRef left, MNG_CarrierRef right)
            => left.IsValid == right.IsValid
                && (!left.IsValid || left.Team == right.Team && left.Slot == right.Slot);

        static bool IsSame(MNG_CarrierRef carrier, Team team, int slot)
            => carrier.IsValid && carrier.Team == team && carrier.Slot == slot;

        static void ValidateCandidate(MNG_PossessionCandidate candidate)
        {
            if (candidate.Slot < 0 || candidate.Slot >= MNG_MatchSnapshot.PlayersPerTeam)
                throw new ArgumentOutOfRangeException(nameof(candidate.Slot));
            if (!MNG_MatchSnapshot.IsFinite(candidate.CenterDistance) || candidate.CenterDistance < 0f)
                throw new ArgumentOutOfRangeException(nameof(candidate.CenterDistance));
            if (!MNG_MatchSnapshot.IsFinite(candidate.AcquisitionDistance) || candidate.AcquisitionDistance < 0f)
                throw new ArgumentOutOfRangeException(nameof(candidate.AcquisitionDistance));
        }

        void ClearPending()
        {
            m_Pending = MNG_CarrierRef.None;
            m_PendingSeconds = 0f;
        }
    }
}
