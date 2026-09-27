using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager.Exhibition
{
    // Same masked baselines as Tools/mng_v2_evaluate.py. Exhibition only.
    [DefaultExecutionOrder(-50)]
    public sealed class ExhibitionRules : MonoBehaviour
    {
        public Team team;
        public int model;
        public MNG_MatchController match;
        readonly bool[] available = new bool[6];
        System.Random random;
        float remaining;
        void Awake() => random = new System.Random(Environment.TickCount ^ (int)team * 7919);
        void FixedUpdate()
        {
            if (!match.IsPlayActive) return;
            remaining -= Time.fixedDeltaTime;
            if (remaining > 0) return;
            remaining = .5f;
            var snapshot = match.Snapshot;
            var decision = match.GetDecisionState(team);
            var targets = MNG_TacticalTargetResolver.Resolve(snapshot, team, decision);
            match.SetDecisionTargets(team, targets.HasPassTarget, targets.HasShotTarget, targets.PassReceiverSlot);
            MNG_CommandMask.Write(snapshot, team, decision, available);
            var own = snapshot.Possession == (team == Team.Red ? MNG_Possession.Red : MNG_Possession.Navy);
            var selected = Select(model, available, own, random);
            if (!match.AcceptCommand(team, (MNG_Command)selected)) Debug.LogError("Exhibition baseline selected a masked command.");
        }
        public static int Select(int model, bool[] valid, bool ownsBall, System.Random rng)
        {
            if (model == 9)
            {
                var count = 0;
                for (int i = 0; i < valid.Length; i++) if (valid[i]) count++;
                if (count == 0) throw new InvalidOperationException("No valid manager action.");
                var pick = rng.Next(count);
                for (int i = 0; i < valid.Length; i++) if (valid[i] && pick-- == 0) return i;
            }
            int action = model == 6 ? 3 : model == 8 ? (valid[2] ? 2 : ownsBall ? 0 : 3) : 4;
            if (valid[action]) return action;
            if (valid[4]) return 4;
            throw new InvalidOperationException("Balanced fallback must be available.");
        }
    }
}
