using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    public sealed partial class FiveMatch
    {
        readonly bool[] defensiveFormation = new bool[2];
        readonly bool[] formationDeferredPlan = new bool[2];
        readonly bool[] formationDisengaging = new bool[10];
        readonly bool[] formationAvoiding = new bool[10];
        public readonly int[] defensiveFormationSteps = new int[2];
        public readonly int[] defensiveFormationEntries = new int[2];
        public bool DefensiveFormationActive(Team team) => State == FiveMatchState.Playing &&
            Carrier != null && Carrier.IsKeeper && Carrier.team != team;

        // T9 is an ownership-driven field-player state, never a ninth manager action.
        void RefreshDefensiveFormation()
        {
            for (int t = 0; t < 2; t++)
            {
                var activeNow = DefensiveFormationActive((Team)t);
                if (activeNow == defensiveFormation[t]) continue;
                defensiveFormation[t] = activeNow;
                if (activeNow) defensiveFormationEntries[t]++;
                else decisions[t].nextDecisionAt = Elapsed;
                Record(activeNow ? "defensive-formation-enter" : "defensive-formation-exit", t,
                    reason: activeNow ? "opponent-keeper-possession" : "keeper-possession-ended");
            }
        }
        void ResetDefensiveFormation(bool newMatch)
        {
            Array.Clear(defensiveFormation, 0, 2);
            Array.Clear(formationDisengaging,0,10);
            Array.Clear(formationAvoiding,0,10);
            if (!newMatch) return;
            Array.Clear(defensiveFormationSteps, 0, 2);
            Array.Clear(defensiveFormationEntries, 0, 2);
        }
        void PlanDefensiveFormation(Team team)
        {
            var keeper = Carrier;
            var length = geometry.HalfLength;
            var width = geometry.HalfWidth;
            var keeperX = Local(keeper.Position, team).x;
            // Reuse the existing penalty-area depth as the retreat clearance, without teleporting.
            var attackLimit = Mathf.Max(2, Mathf.Min(length - profile.keeperAreaDepth,
                keeperX - profile.keeperAreaDepth));
            for (int slot = 1; slot <= 4; slot++)
            {
                var player = Player(team, slot);
                var p = Local(player.Position, team);
                var side = slot == 1 || slot == 3 ? -1 : 1;
                var target = slot <= 2
                    ? new Vector2(-length * .5f, side * width * .28f)
                    : new Vector2(Mathf.Clamp(p.x, 2, attackLimit), side * width * .35f);
                var close=Vector2.Distance(player.Position,keeper.Position)<player.radius+keeper.radius+1.5f;
                if(close)
                {
                    // First withdraw the kick plate along its own entry axis. Rotating a plate
                    // while it overlaps the keeper's front pocket can lock both rigidbodies.
                    var away=player.Position-keeper.Position;
                    var exit=-player.Forward;
                    if(Vector2.Dot(exit,away)<0)exit=away.normalized;
                    player.SetTarget(ClampField(player.Position+exit*3,player.radius),player.Position+player.Forward*5,true);
                    if(!formationDisengaging[player.Index])Record("defensive-formation-disengage",player.Index,keeper.Index);
                }
                else player.SetTarget(AvoidFormationOpponents(player,ClampField(World(target,team))), keeper.Position, true);
                formationDisengaging[player.Index]=close;
                player.Evading = false;
            }
        }
        Vector2 AvoidFormationOpponents(FivePlayer player,Vector2 destination)
        {
            var delta=destination-player.Position;
            if(delta.magnitude<.1f)return destination;
            var distance=Mathf.Min(8,delta.magnitude);
            var direction=delta.normalized;
            Vector2 best=player.Position;float bestScore=float.NegativeInfinity;var bestSafe=false;
            // Test short swept routes, including the opponents' near-future positions.
            // A local detour changes movement only; it never changes a manager command.
            for(int n=0;n<19;n++)
            {
                var angle=(n==0?0:((n+1)/2)*20*(n%2==1?1:-1))*Mathf.Deg2Rad;
                var travel=new Vector2(direction.x*Mathf.Cos(angle)-direction.y*Mathf.Sin(angle),
                    direction.x*Mathf.Sin(angle)+direction.y*Mathf.Cos(angle));
                var end=ClampField(player.Position+travel*distance,player.radius+.2f);
                var clearance=float.PositiveInfinity;var safe=true;
                foreach(var other in players)
                {
                    if(other.team==player.team)continue;
                    var radius=player.radius+other.radius+.8f;
                    for(int sample=0;sample<2;sample++)
                    {
                        var obstacle=other.Position+other.Velocity*(sample*.3f);
                        var startDistance=Vector2.Distance(player.Position,obstacle);
                        var gap=DistanceToSegment(obstacle,player.Position,end)-radius;
                        // An already-close opponent must be left behind, not orbited into.
                        if(startDistance<radius)
                            safe&=Vector2.Dot(end-player.Position,player.Position-obstacle)>0
                                && Vector2.Distance(end,obstacle)>startDistance;
                        else safe&=gap>=0;
                        clearance=Mathf.Min(clearance,gap);
                    }
                }
                if(n==0&&safe){formationAvoiding[player.Index]=false;return destination;}
                var score=(safe?1000:0)+Mathf.Min(clearance,2)*2+Vector2.Dot(travel,direction)*5;
                if(score>bestScore){bestScore=score;best=end;bestSafe=safe;}
            }
            if(!formationAvoiding[player.Index])Record("defensive-formation-avoid",player.Index);
            formationAvoiding[player.Index]=true;
            return bestSafe?best:player.Position;
        }
        void ApplyDefensiveFormationTargets()
        {
            RefreshDefensiveFormation();
            for (int t = 0; t < 2; t++)
                if (DefensiveFormationActive((Team)t))
                {
                    PlanDefensiveFormation((Team)t);
                    defensiveFormationSteps[t]++;
                }
                else if(formationDeferredPlan[t] && tacticsEnabled && teamPlanningEnabled[t])
                    PlanTeam((Team)t); // A keeper kicked during this substep: no stale retreat target.
        }
        public bool CommandAvailable(Team team, FiveCommand command) =>
            (int)command >= 0 && (int)command < 8 && !DefensiveFormationActive(team) &&
            CommandBlockedSelections(team,command) == 0;
    }
}
