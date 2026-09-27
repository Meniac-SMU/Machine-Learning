using System.Collections.Generic;
using UnityEngine;

namespace MachineLearning.Soccer.Manager.Exhibition
{
    // Exhibition statistics only. Does not add observations, rewards, or physics forces.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MNG_BallControl))]
    public sealed class ExhibitionKeeperTouches : MonoBehaviour
    {
        MNG_MatchController match;
        readonly HashSet<Collider>[] contacts = { new(), new() };
        readonly int[] touches = new int[2];
        readonly double[] lastTouchTime = { double.NegativeInfinity, double.NegativeInfinity };
        long episode = -1, spawn = -1;

        void Awake() => match = GetComponentInParent<MNG_MatchController>();
        public int GetTouches(Team team) => touches[team == Team.Red ? 0 : 1];

        public void ResetCounts()
        {
            touches[0] = touches[1] = 0;
            ClearContacts();
            if (match == null) match = GetComponentInParent<MNG_MatchController>();
            episode = match.Snapshot.EpisodeId;
            spawn = match.SpawnPlacementRevision;
        }

        void ClearContacts()
        {
            contacts[0].Clear(); contacts[1].Clear();
            lastTouchTime[0] = lastTouchTime[1] = double.NegativeInfinity;
        }

        void FixedUpdate() => SynchronizeRound();
        void SynchronizeRound()
        {
            if (episode != match.Snapshot.EpisodeId) ResetCounts();
            if (spawn != match.SpawnPlacementRevision || !match.IsPlayActive)
            {
                ClearContacts();
                spawn = match.SpawnPlacementRevision;
            }
        }

        void OnCollisionEnter(Collision collision) => Touch(collision.collider);
        void OnCollisionStay(Collision collision) => Touch(collision.collider);
        void OnCollisionExit(Collision collision)
        {
            contacts[0].Remove(collision.collider);
            contacts[1].Remove(collision.collider);
        }

        void Touch(Collider other)
        {
            SynchronizeRound();
            if (!match.IsPlayActive) return;
            var avatar = other.attachedRigidbody != null
                ? other.attachedRigidbody.GetComponent<MNG_PlayerAvatar>() : null;
            if (avatar == null || avatar.Role != MNG_PlayerRole.Keeper) return;
            int team = avatar.Team == Team.Red ? 0 : 1;
            bool newContact = contacts[team].Count == 0;
            if (!contacts[team].Add(other) || !newContact) return;
            // A held contact is one touch. Compound body/plate contacts in one physics step
            // are also one touch; after separation a new physical contact counts again.
            if (lastTouchTime[team] == Time.fixedTimeAsDouble) return;
            touches[team]++;
            lastTouchTime[team] = Time.fixedTimeAsDouble;
        }
    }
}
