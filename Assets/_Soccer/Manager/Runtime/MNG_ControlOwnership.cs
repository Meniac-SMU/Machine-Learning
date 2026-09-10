using System;
using MachineLearning.Soccer;

namespace MachineLearning.Soccer.Manager
{
    public enum MNG_InputOwner
    {
        Manager = 0,
        Human = 1
    }

    /// <summary>
    /// Revision changes invalidate pending movement, aim, and kick tokens.
    /// </summary>
    public sealed class MNG_ControlOwnership
    {
        public Team Team { get; }
        public int Slot { get; }
        public MNG_InputOwner Owner { get; private set; } = MNG_InputOwner.Manager;
        public long Revision { get; private set; }

        public event Action<MNG_InputOwner, long> Changed;

        public MNG_ControlOwnership(Team team, int slot)
        {
            if (slot < 0 || slot >= MNG_MatchSnapshot.PlayersPerTeam)
                throw new ArgumentOutOfRangeException(nameof(slot));
            Team = team;
            Slot = slot;
        }

        public bool SetOwner(MNG_InputOwner owner)
        {
            if (Owner == owner) return false;
            Owner = owner;
            Revision++;
            Changed?.Invoke(Owner, Revision);
            return true;
        }

        public MNG_InputOwner Toggle()
        {
            SetOwner(Owner == MNG_InputOwner.Manager ? MNG_InputOwner.Human : MNG_InputOwner.Manager);
            return Owner;
        }

        public bool CanWrite(MNG_InputOwner requester, long observedRevision)
            => requester == Owner && observedRevision == Revision;
    }
}
