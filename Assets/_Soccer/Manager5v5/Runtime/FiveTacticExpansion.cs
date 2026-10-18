using System.Linq;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    public sealed partial class FiveMatch
    {
        // Independent execution switches for controlled ablations. They never select a manager action.
        public bool improvedPassPreparationEnabled=true;
        public bool expandedCutbackEnabled=true;
        public bool adaptiveGoalBlockEnabled=true;
        readonly int[] cutbackReceiver={-1,-1};

        void ReceiveTarget(FivePlayer player,Vector2 point)
        {
            // Use the ordinary turn limit while arriving, rather than turning only after arrival.
            player.SetTarget(ClampField(point),BallPosition,
                improvedPassPreparationEnabled&&Vector2.Distance(player.Position,point)<6);
        }
        bool PassCanBeatPressure()
        {
            if(PassPhase!=FivePassPhase.Preparing||Owner!=PassSender)return false;
            var sender=players[PassSender];var receiver=players[PassReceiver];
            if(!PassingLaneOpen(BallPosition,receiverHold,sender.team,receiver.Index))return false;
            var senderTurn=Mathf.Max(0,Vector2.Angle(sender.Forward,receiverHold-sender.Position)-Mathf.Acos(.90f)*Mathf.Rad2Deg)
                /Mathf.Max(1,sender.MaximumTurnRate(profile));
            var receiverTurn=Mathf.Max(0,Vector2.Angle(receiver.Forward,BallPosition-receiver.Position)-Mathf.Acos(.99f)*Mathf.Rad2Deg)
                /Mathf.Max(1,receiver.MaximumTurnRate(profile));
            var braking=Mathf.Max(0,receiver.Velocity.magnitude-1.8f)/Mathf.Max(1,profile.deceleration);
            var alignment=Mathf.Max(0,Vector2.Distance(BallPosition,Anchor(sender))-.65f)/Mathf.Max(1,profile.frontReceiveSpeed);
            var readyIn=Mathf.Max(Mathf.Max(senderTurn,receiverTurn),Mathf.Max(braking,alignment))+.06f;
            var contactIn=float.PositiveInfinity;
            foreach(var opponent in players)
            {
                if(opponent.team==sender.team)continue;
                var reach=opponent.CloseHoldOffset(ballRadius,profile.dribblePlateGap)+ballRadius;
                var gap=Mathf.Max(0,Vector2.Distance(opponent.Position,BallPosition)-reach);
                contactIn=Mathf.Min(contactIn,gap/Mathf.Max(1,opponent.MaximumMoveSpeed(profile)));
            }
            return readyIn<contactIn&&Elapsed+readyIn<passDeadline;
        }
        void PlanExpandedCutback(Team team,Vector2 b,bool ours)
        {
            var side=Mathf.Abs(b.y)>2?Mathf.Sign(b.y):1;
            var t=(int)team;var length=geometry.HalfLength;var width=geometry.HalfWidth;
            Target(Player(team,1),new Vector2(b.x-10,side*width*.60f));
            Target(Player(team,2),new Vector2(b.x-22,-side*8));
            var first=Player(team,3);var second=Player(team,4);
            var current=cutbackReceiver[t]>=0?players[cutbackReceiver[t]]:null;
            var changed=current==null||ours&&current==Carrier;
            if(changed||Elapsed>=nextCutbackPlanAt[t])
            {
                FivePlayer best=null;Vector2 bestPoint=Vector2.zero;var bestScore=float.NegativeInfinity;
                foreach(var candidate in new[]{first,second})
                {
                    if(ours&&candidate==Carrier)continue;
                    var point=b.x>length-32?ChooseCutbackPoint(candidate,side):
                        World(new Vector2(Mathf.Min(length-14,b.x+17),-side*8),team);
                    var space=players.Where(p=>p.team!=team).Min(p=>Vector2.Distance(p.Position,point));
                    var score=Mathf.Min(space,10)-Vector2.Distance(candidate.Position,point)*.3f;
                    // Stable receiver/target through repeated selections; only a material advantage changes roles.
                    if(candidate==current)score+=3;
                    if(score>bestScore){best=candidate;bestPoint=point;bestScore=score;}
                }
                if(best!=null)
                {
                    if(best!=current||changed||Vector2.Distance(bestPoint,cutbackTargets[t])>5
                        &&(!PassingLaneOpen(BallPosition,cutbackTargets[t],team,best.Index)
                            ||Vector2.Distance(best.Position,cutbackTargets[t])>12))
                        cutbackTargets[t]=bestPoint;
                    cutbackReceiver[t]=best.Index;
                }
                nextCutbackPlanAt[t]=Elapsed+.5f;
            }
            var receiver=cutbackReceiver[t]>=0?players[cutbackReceiver[t]]:second;
            var winger=receiver==first?second:first;
            ReceiveTarget(winger,World(new Vector2(Mathf.Min(length-7,b.x+16),side*width*.66f),team));
            ReceiveTarget(receiver,cutbackTargets[t]);
        }
        void PlanExpandedCutbackCarrier(FivePlayer player,Vector2 b)
        {
            var team=player.team;var t=(int)team;var length=geometry.HalfLength;
            var side=Mathf.Abs(b.y)>2?Mathf.Sign(b.y):1;
            var receiver=cutbackReceiver[t]>=0?players[cutbackReceiver[t]]:null;
            // A cutback can start at the side of the box, before reaching the narrow end-line trigger.
            if(receiver!=null&&receiver!=player&&b.x>length-32&&Mathf.Abs(b.y)>10
                &&Local(receiver.Position,team).x>length-30&&Mathf.Abs(receiver.Position.y)<14
                &&Vector2.Distance(receiver.Position,cutbackTargets[t])<5
                &&PassingLaneOpen(BallPosition,receiver.Position,team,receiver.Index)
                &&RequestPass(player,receiver))return;
            // In buildup, connect to the advancing wide player instead of requiring a solo trip to the end line.
            var wide=FindReceiver(player,6,48,p=>Mathf.Abs(Local(p.Position,team).y)>14
                &&Local(p.Position,team).x>b.x+5,
                p=>Local(p.Position,team).x-b.x+NearestOpponentDistance(p)*.4f);
            if(b.x<length-25&&wide!=null&&RequestPass(player,wide))return;
            var winger=new Vector2(Mathf.Min(length-6,b.x+10),side*geometry.HalfWidth*.66f);
            player.SetTarget(ClampField(World(winger,team)),World(winger,team));
        }
        void PlanProtectedCarrier(FivePlayer player)
        {
            var b=Local(player.Position,player.team);
            var outletSlot=player.slot==3?4:3;
            var outlet=FindReceiver(player,4,70,p=>p.slot==outletSlot&&Local(p.Position,player.team).x>b.x+4,
                p=>NearestOpponentDistance(p)-Vector2.Distance(player.Position,p.Position)*.05f);
            // Retain the three-player cover and one outlet, without commanding the carrier to retreat.
            CarryForward(player);
            if(outlet!=null&&(NearestOpponentDistance(player)<9||b.x<0))RequestPass(player,outlet);
        }
    }
}
