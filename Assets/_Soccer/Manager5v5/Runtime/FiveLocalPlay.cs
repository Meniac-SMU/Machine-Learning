using System;
using System.Linq;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    public sealed partial class FiveMatch
    {
        readonly bool[] contestReinforcements=new bool[10];
        readonly bool[] reinforceTeamPlanned=new bool[2];
        readonly int[] challengeOwner=new int[10];
        readonly float[] challengeSide=new float[10];
        float reinforceSlowTime,reinforceFastTime;
        Vector2 reinforceOrigin;
        bool reinforceTracking;
        public float ContestReinforcementWait=>Mathf.Max(0,profile.contestReinforceSeconds-reinforceSlowTime);
        public bool IsContestReinforcement(int index)=>contestReinforcements[index];

        void ResetLocalSupport()
        {
            Array.Clear(contestReinforcements,0,10);
            Array.Clear(reinforceTeamPlanned,0,2);
            for(int i=0;i<10;i++){challengeOwner[i]=-1;challengeSide[i]=0;}
            reinforceSlowTime=reinforceFastTime=0;reinforceTracking=false;
        }
        void EndReinforcements(string reason)
        {
            if(contestReinforcements.Any(p=>p))Record("contest-reinforce-end",reason:reason);
            for(int i=0;i<10;i++)if(contestReinforcements[i])
            {contestReinforcements[i]=false;players[i].SetTarget(players[i].Position,BallPosition);}
            reinforceSlowTime=reinforceFastTime=0;reinforceTracking=false;
            Array.Clear(reinforceTeamPlanned,0,2);
        }
        void PlanContestReinforcements(float dt)
        {
            // This never selects T7 or replaces the manager's current command.
            // The nearby players keep their 0.65s relief reaction. Distant helpers wait six seconds.
            if(!contestReliefEnabled||Elapsed-contestRoundStarted<profile.contestRestartGrace||PassPhase!=FivePassPhase.None)
            {EndReinforcements("restart-or-pass");return;}
            var local=players.Where(CanRelieve).ToArray();
            reinforceFastTime=BallVelocity.magnitude>profile.contestSlowSpeed*2?reinforceFastTime+dt:0;
            if(local.Length<2||reinforceFastTime>.2f||
                (reinforceTracking&&Vector2.Distance(BallPosition,reinforceOrigin)>.75f))
            {EndReinforcements("ball-released-or-moving");return;}
            if(!reinforceTracking)
            {reinforceOrigin=BallPosition;reinforceTracking=true;}
            if(BallVelocity.magnitude<=profile.contestSlowSpeed)reinforceSlowTime+=dt;
            if(reinforceSlowTime<profile.contestReinforceSeconds)return;

            foreach(var team in new[]{Team.Red,Team.Navy})
            {
                if(reinforceTeamPlanned[(int)team]||!local.Any(p=>p.team==team))continue;
                reinforceTeamPlanned[(int)team]=true;
                // Keep one field player out; never bring an independent goalkeeper into a scrum.
                var helpers=players.Where(p=>p.team==team&&!p.IsKeeper&&!IsRecoveryReserve(p))
                    .OrderBy(p=>Vector2.Distance(p.Position,BallPosition)).ThenBy(p=>p.Index).Take(3).ToArray();
                foreach(var p in helpers)
                {
                    if(Vector2.Distance(p.Position,BallPosition)<=profile.contestRadius||contestReinforcements[p.Index])continue;
                    contestReinforcements[p.Index]=true;
                    Record("contest-reinforce",p.Index,reason:"local-attempts-unresolved-for-six-seconds");
                }
            }
            foreach(var p in players)
            {
                if(!contestReinforcements[p.Index])continue;
                if(IsRecoveryReserve(p)){contestReinforcements[p.Index]=false;continue;}
                if(ContestControls(p)||Owner==p.Index||PassReceiver==p.Index&&PassPhase!=FivePassPhase.None)continue;
                if(Carrier!=null&&Carrier.team==p.team)
                {
                    // Offer room for the carrier's escape/pass instead of running through our own owner.
                    var outward=(p.Position-BallPosition).normalized;
                    p.SetTarget(ClampField(BallPosition+outward*4,profile.fieldWallClearance),BallPosition,true);
                }
                else ApproachBall(p);
            }
        }

        bool TryChallengeRoute(FivePlayer challenger)
        {
            var owner=Carrier;
            if(owner==null||owner.team==challenger.team||challenger.IsKeeper||IsRecoveryReserve(challenger))return false;
            var forward=(BallPosition-owner.Position).normalized;
            if(forward.sqrMagnitude<.01f)forward=owner.Forward;
            var left=new Vector2(-forward.y,forward.x);
            var clearance=owner.radius+challenger.radius+.5f;
            var relative=challenger.Position-owner.Position;
            var ahead=Vector2.Dot(relative,forward);
            var blocking=DistanceToSegment(owner.Position,challenger.Position,BallPosition)<clearance;
            // Get far enough ahead to put our own front plate between us and the ball.
            // Stopping beside the owner's plate leaves a turning challenger body-blocked.
            if(ahead>owner.CloseHoldOffset(ballRadius,profile.dribblePlateGap)
                +challenger.CloseHoldOffset(ballRadius,profile.dribblePlateGap)+.2f)return false;
            if(!blocking&&ahead>.1f)return false;
            if(challengeOwner[challenger.Index]!=owner.Index)
            {
                challengeOwner[challenger.Index]=owner.Index;
                var lateral=Vector2.Dot(relative,left);
                var plus=ClampField(owner.Position+left*(clearance+.7f),challenger.radius+.2f);
                var minus=ClampField(owner.Position-left*(clearance+.7f),challenger.radius+.2f);
                // Prefer the reachable side, then retain it through the encounter instead of oscillating.
                challengeSide[challenger.Index]=Mathf.Abs(lateral)>.3f?Mathf.Sign(lateral):
                    ReliefClearance(challenger,plus)>=ReliefClearance(challenger,minus)?1:-1;
                var plusBlocked=Vector2.Distance(plus,owner.Position+left*(clearance+.7f))>.25f;
                var minusBlocked=Vector2.Distance(minus,owner.Position-left*(clearance+.7f))>.25f;
                if(plusBlocked!=minusBlocked)challengeSide[challenger.Index]=plusBlocked?-1:1;
                Record("challenge-route",challenger.Index,owner.Index,reason:"around-carrier-to-front");
            }
            var horizon=Mathf.Min(.65f,relative.magnitude/(profile.maxSpeed+owner.Velocity.magnitude));
            var predicted=owner.Position+owner.Velocity*horizon;
            var front=predicted+forward*(owner.CloseHoldOffset(ballRadius,profile.dribblePlateGap)+challenger.radius+ballRadius+.4f);
            var route=front;
            if(DistanceToSegment(predicted,challenger.Position,front)<clearance+.15f)
            {
                var radial=(challenger.Position-predicted).normalized;
                if(radial.sqrMagnitude<.01f)radial=-forward;
                var side=challengeSide[challenger.Index];
                // Starting behind a right-facing carrier, a clockwise arc goes around its left side.
                var angle=-side*45;
                route=predicted+RotateRelief(radial,angle)*(clearance+.7f);
            }
            challenger.SetTarget(ClampField(route,challenger.radius+.2f),BallPosition,true);
            return true;
        }
        void PlanChallengeRoutes()
        {
            if(Carrier==null){for(int i=0;i<10;i++)challengeOwner[i]=-1;return;}
            foreach(var p in players)
            {
                if(p.team==Carrier.team||p.IsKeeper||ContestControls(p))continue;
                if(Vector2.Distance(p.Target,p.Position)>.1f&&Vector2.Distance(p.Target,BallPosition)<5)TryChallengeRoute(p);
            }
        }
        void KeepTargetsAwayFromWalls()
        {
            foreach(var p in players)
            {
                if(p.IsKeeper)continue;
                var boundaryDistance=Mathf.Min(geometry.HalfLength-Mathf.Abs(BallPosition.x),geometry.HalfWidth-Mathf.Abs(BallPosition.y));
                var retrieving=Owner!=p.Index&&boundaryDistance<profile.fieldWallClearance+ballRadius
                    &&Vector2.Distance(p.Target,BallPosition)<5;
                var margin=retrieving?p.radius+.1f:profile.fieldWallClearance+p.radius;
                p.LimitTarget(ClampField(p.Target,margin));
            }
        }
    }
}
