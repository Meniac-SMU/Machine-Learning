using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    [Serializable] public sealed class FiveTeamStatistics
    {
        public int shots, successfulShots, passes, successfulPasses, saves;
        public float ShotRate => shots == 0 ? 0 : 100f * successfulShots / shots;
        public float PassRate => passes == 0 ? 0 : 100f * successfulPasses / passes;
    }
    // Presentation ledger only. Never changes physics, rewards or observations.
    public sealed class FiveMatchStatistics : IDisposable
    {
        public readonly FiveTeamStatistics[] teams = { new FiveTeamStatistics(), new FiveTeamStatistics() };
        FiveMatch match;
        int shotTeam = -1, pendingSave = -1;
        float shotTime;
        public FiveMatchStatistics(FiveMatch match)
        { this.match = match; match.EventRecorded += OnEvent; match.BallContact += OnContact; }
        public static float Share(float red, float navy) => red + navy > 0 ? red / (red + navy) : 0;
        void ResolveShot(bool goal = false, int scoring = -1)
        {
            if (goal && scoring == shotTeam) teams[scoring].successfulShots++;
            if (pendingSave >= 0 && (!goal || scoring == pendingSave)) teams[pendingSave].saves++;
            shotTeam = pendingSave = -1;
        }
        void OnContact(FivePlayer player)
        {
            if (shotTeam < 0) return;
            if (match.Elapsed - shotTime > 10) { ResolveShot(); return; }
            if (player.IsKeeper && (int)player.team != shotTeam) pendingSave = (int)player.team;
        }
        void OnEvent(FiveEvent e)
        {
            if(shotTeam>=0 && e.time-shotTime>10)ResolveShot();
            if (e.kind == "shot")
            { ResolveShot(); shotTeam = e.actor / 5; shotTime = e.time; teams[shotTeam].shots++; }
            else if (e.kind == "pass-kicked")
            { ResolveShot(); teams[e.actor / 5].passes++; }
            else if (e.kind == "pass-received") teams[e.actor / 5].successfulPasses++;
            else if (e.kind == "front-cushion" && e.actor % 5 == 0 && shotTeam >= 0 && e.actor / 5 != shotTeam)
                pendingSave = e.actor / 5;
            else if (e.kind == "possession")
            {
                if (shotTeam >= 0 && e.actor % 5 == 0 && e.actor / 5 != shotTeam) pendingSave = e.actor / 5;
                ResolveShot();
            }
            else if (e.kind == "goal") ResolveShot(true, e.actor);
            else if (e.kind == "keeper-clearance") ResolveShot();
        }
        public void Complete() => ResolveShot();
        public void Dispose()
        { if (match == null) return; match.EventRecorded -= OnEvent; match.BallContact -= OnContact; match = null; }
    }
}
