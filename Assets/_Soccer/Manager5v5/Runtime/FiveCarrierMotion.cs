using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    public sealed partial class FiveMatch
    {
        public bool carrierMobilityEnabled=true;
        int escapeCarrier=-1;
        float escapeUntil;
        Vector2 escapeDirection;
        bool CarrierEscaping(FivePlayer player)=>escapeCarrier==player.Index&&Elapsed<escapeUntil;
        void ResetCarrierMobility(){escapeCarrier=-1;escapeUntil=0;escapeDirection=Vector2.zero;}
        void CarryForward(FivePlayer player)
        {
            var goal=new Vector2(AttackSign(player.team)*geometry.HalfLength,0);
            var target=ClampField(player.Position+(goal-player.Position).normalized*10,profile.fieldWallClearance+player.radius);
            player.SetTarget(target,target);
        }
        void PlanCarrierMobility()
        {
            foreach(var player in players)player.Evading=false;
            var owner=Carrier;
            if(!carrierMobilityEnabled||owner==null||owner.IsKeeper||IsHuman(owner)||!tacticsEnabled||!teamPlanningEnabled[(int)owner.team])
            {ResetCarrierMobility();return;}
            if(pendingReception==Owner||shotPlayer==Owner)return;
            var pressure=false;
            foreach(var p in players)
            {
                if(p.team==owner.team)continue;
                var predicted=p.Position+p.Velocity*.25f;
                if(Vector2.Distance(predicted,BallPosition)<3.4f||Vector2.Distance(predicted,owner.Position)<4)
                {pressure=true;break;}
            }
            if(PassPhase==FivePassPhase.Preparing)
            {
                // Allow ordinary alignment, but do not stand in a tackle waiting for a receiver.
                if(!pressure||Elapsed<passDeadline-1.85f||improvedPassPreparationEnabled&&PassCanBeatPressure())return;
            }
            else if(PassPhase!=FivePassPhase.None)return;

            var intended=owner.Target-owner.Position;
            if(intended.sqrMagnitude<1)intended=new Vector2(AttackSign(owner.team)*geometry.HalfLength,0)-owner.Position;
            if(CarrierEscaping(owner))
            {
                if(CarryLaneClear(owner,escapeDirection,3))
                {
                    owner.Evading=true;
                    owner.SetTarget(ClampField(owner.Position+escapeDirection*7,profile.fieldWallClearance+owner.radius),owner.Position+escapeDirection*10);
                    return;
                }
                escapeUntil=0;
            }
            if(!pressure&&!ContestControls(owner))return;
            var best=Vector2.zero;var bestScore=float.NegativeInfinity;
            foreach(var angle in new[]{0f,25f,-25f,50f,-50f,75f,-75f,100f,-100f})
            {
                var direction=RotateRelief(owner.Forward,angle);
                if(!CarryLaneClear(owner,direction,5))continue;
                var clearance=8f;
                foreach(var other in players)
                    if(other.team!=owner.team)clearance=Mathf.Min(clearance,Vector2.Distance(owner.Position+direction*5,other.Position+other.Velocity*.25f));
                var score=Vector2.Dot(direction,intended.normalized)*2.2f+Vector2.Dot(direction,new Vector2(AttackSign(owner.team),0))
                    +clearance*.3f-Mathf.Abs(angle)*.014f;
                if(score>bestScore){bestScore=score;best=direction;}
            }
            if(best.sqrMagnitude<.5f)return; // The existing local backoff/pass can still resolve a fully closed scrum.
            if(PassPhase==FivePassPhase.Preparing)CancelPass("pressure-escape");
            reliefTasks[owner.Index]=null;
            var changed=escapeCarrier!=owner.Index||Vector2.Angle(escapeDirection,best)>15;
            escapeCarrier=owner.Index;escapeUntil=Elapsed+.75f;escapeDirection=best;owner.Evading=true;
            owner.SetTarget(ClampField(owner.Position+best*7,profile.fieldWallClearance+owner.radius),owner.Position+best*10);
            if(changed)Record("carrier-escape",owner.Index,reason:Mathf.Abs(Vector2.SignedAngle(owner.Forward,best))<10?"forward-acceleration":"side-turn");
        }
        bool CarryLaneClear(FivePlayer owner,Vector2 direction,float length)
        {
            var end=owner.Position+direction*length;
            if(Vector2.Distance(end,ClampField(end,profile.fieldWallClearance+owner.radius))>.2f)return false;
            foreach(var other in players)
            {
                if(other==owner)continue;
                var position=other.Position+other.Velocity*.15f;
                // A pursuer behind the starting point does not block an accelerating escape away from it.
                if(Vector2.Dot(position-owner.Position,direction)<-.2f)continue;
                if(DistanceToSegment(position,owner.Position,end)<owner.radius+other.radius+.25f)return false;
                // The ball is exposed ahead of the body: a body-clear lane can still enter a tackling plate.
                if(other.team!=owner.team&&DistanceToSegment(position,BallPosition,
                    end+direction*owner.CloseHoldOffset(ballRadius,profile.dribblePlateGap))<other.radius+ballRadius+.55f)return false;
            }
            return true;
        }
    }
}
