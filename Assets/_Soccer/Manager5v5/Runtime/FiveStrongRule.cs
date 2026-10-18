using System;
using System.Linq;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    public enum FiveRuleVariant { Legacy,Strong }
    [Serializable] public sealed class FiveRuleDecision
    {
        public string version,reason;
        public int action,longReceivers,wideReceivers;
        public float[] scores;
        public float[] forecastOutcomes;
        public int[] forecastActions,forecastOpponents;
        public float forecastMilliseconds;
        public float pressureDistance,forwardSpace,ownArrival,opponentArrival,goalThreat,cutbackThreat,commandAge;
    }

    /// <summary>Public-state tactical evaluation. Never changes player techniques or reads opponent intentions.</summary>
    public static class FiveStrongRule
    {
        public const string Version="strong-v11";
        static readonly FiveRuleSettings settings=FiveRuleSettings.Load();
        public static FiveRuleDecision Legacy(FiveObservation seen)
        {
            var command=FiveRuleManager.Choose(seen,out var reason);
            return new FiveRuleDecision {version="legacy",action=(int)command,reason=reason};
        }
        public static bool PassingLaneOpen(FiveObservation s,Vector2 start,Vector2 end,int receiverSlot)
        {
            foreach(var p in s.opponents)
                if(FiveMatch.DistanceToSegment(p.position,start,end)<p.radius+s.ballRadius+.5f)return false;
            foreach(var p in s.own)
                if(p.slot!=receiverSlot&&Vector2.Distance(p.position,start)>2
                    &&FiveMatch.DistanceToSegment(p.position,start,end)<p.radius+s.ballRadius+.3f)return false;
            var path=end-start;var distance=path.magnitude;
            if(distance<.01f)return false;
            var direction=path/distance;
            var speed=Mathf.Clamp(distance/1.7f+s.freeBallDamping*distance,12,s.maximumBallSpeed);
            foreach(var p in s.opponents)
            {
                var along=Mathf.Clamp(Vector2.Dot(p.position-start,direction),0,distance);
                var arrival=s.freeBallDamping>.001f?
                    -Mathf.Log(Mathf.Max(.01f,1-s.freeBallDamping*along/speed))/s.freeBallDamping:along/speed;
                var predicted=p.position+p.velocity*Mathf.Min(arrival,1.2f);
                var reaction=Mathf.Min(arrival,.5f);
                var change=Mathf.Min(2,.5f*s.acceleration*reaction*reaction);
                if(FiveMatch.DistanceToSegment(predicted,start,end)<p.radius+s.ballRadius+.5f+change)return false;
            }
            return true;
        }
        public static int[] Receivers(FiveObservation s,FiveCommand command)
        {
            if(!s.ownPossession||s.ownerSlot<0)return Array.Empty<int>();
            var owner=s.own.First(p=>p.slot==s.ownerSlot);
            bool Eligible(FiveVisiblePlayer p,float minimum,float maximum)
            {
                var d=Vector2.Distance(owner.position,p.position);
                return p.slot!=owner.slot&&d>=minimum&&d<=maximum&&PassingLaneOpen(s,s.ball,p.position,p.slot);
            }
            if(command==FiveCommand.LongPass)
                return s.own.Where(p=>!p.keeper&&p.position.x>owner.position.x+16&&Eligible(p,22,85)).Select(p=>p.slot).ToArray();
            if(command!=FiveCommand.WideCirculation)return Array.Empty<int>();
            // Keeper distribution and field-player circulation have different existing predicates.
            if(owner.keeper)
                return s.own.Where(p=>!p.keeper&&Mathf.Abs(p.position.y-owner.position.y)>12&&Eligible(p,6,55)).Select(p=>p.slot).ToArray();
            var forward=s.own.Where(p=>!p.keeper&&p.position.x-owner.position.x>=4&&p.position.x-owner.position.x<=24&&Eligible(p,6,55)).Select(p=>p.slot).ToArray();
            if(forward.Length>0||s.opponents.Min(p=>Vector2.Distance(p.position,owner.position))>=5)return forward;
            return s.own.Where(p=>p.position.x-owner.position.x>=-10&&p.position.x-owner.position.x<=24&&Eligible(p,6,55)).Select(p=>p.slot).ToArray();
        }
        static float Arrival(FiveObservation s,FiveVisiblePlayer p,Vector2 point)
        {
            var offset=point-p.position;
            var speed=Mathf.Max(1,s.maximumPlayerSpeed*FiveStamina.SpeedFactor(p.stamina,false));
            var toward=offset.sqrMagnitude>.01f?Vector2.Dot(p.velocity,offset.normalized):speed;
            return offset.magnitude/speed+Mathf.Max(0,speed-toward)/Mathf.Max(1,s.acceleration)*.5f;
        }
        public static FiveRuleDecision Evaluate(FiveObservation s)
        {
            var fields=s.own.Where(p=>!p.keeper).ToArray();
            var scores=Enumerable.Repeat(-8f,8).ToArray();
            var reason=new string[8];
            void Offer(FiveCommand c,float value,string why){scores[(int)c]=value;reason[(int)c]=why;}
            var probe=s.ball+s.velocity*.3f;
            var ownArrival=fields.Min(p=>Arrival(s,p,probe));
            var otherArrival=s.opponents.Where(p=>!p.keeper).Min(p=>Arrival(s,p,probe));
            var d=new FiveRuleDecision {version=Version,scores=scores,ownArrival=ownArrival,opponentArrival=otherArrival,commandAge=s.commandAge};
            if(s.ownPassInFlight||s.ownPassPreparing)
            {
                Offer(s.ownCommand,10,"own-pass-commitment");
            }
            else if(s.ownPossession)
            {
                var owner=s.own.First(p=>p.slot==s.ownerSlot);
                var pressure=s.opponents.Min(p=>Vector2.Distance(p.position,owner.position));d.pressureDistance=pressure;
                var front=s.opponents.Where(p=>p.position.x>owner.position.x&&Mathf.Abs(p.position.y-owner.position.y)<5)
                    .Select(p=>p.position.x-owner.position.x).DefaultIfEmpty(30).Min();d.forwardSpace=front;
                var longs=Receivers(s,FiveCommand.LongPass);var wides=Receivers(s,FiveCommand.WideCirculation);
                d.longReceivers=longs.Length;d.wideReceivers=wides.Length;
                float Progress(int[] options)=>options.Length==0?0:options.Max(slot=>s.own.First(p=>p.slot==slot).position.x-owner.position.x);
                var tired=owner.stamina<.4f?1f:0f;
                Offer(FiveCommand.FastTransition,settings.carry+Mathf.Min(front,20)*.09f-(pressure<3?1:0),"carry-visible-forward-space");
                Offer(FiveCommand.LongPass,longs.Length>0?settings.longPass+Mathf.Min(3,Progress(longs)/15)+(pressure<7?1.8f:0)+tired: -5,"verified-distant-exit");
                Offer(FiveCommand.WideCirculation,wides.Length>0?settings.widePass+Mathf.Min(1.5f,Progress(wides)/12)+(pressure<7?1.4f:0)+(front<10?1.2f:0)+tired*.5f:-5,"verified-circulation-exit");
                var deep=s.opponents.Count(p=>!p.keeper&&p.position.x>s.halfLength-24&&Mathf.Abs(p.position.y)<16);
                var wideAttack=owner.position.x>0&&Mathf.Abs(owner.position.y)>12;
                Offer(FiveCommand.WingCutback,1.5f+(s.ball.x>0&&deep>=2?settings.cutbackDeep:0)+(wideAttack?1.5f:0)+(front<8?1:0),"spread-block-or-continue-wide-attack");
                if(s.unproductiveSeconds>6||s.stallSeconds>6)
                {
                    scores[(int)s.ownCommand]-=2.5f;
                    if(longs.Length==0&&wides.Length==0)scores[(int)FiveCommand.WingCutback]+=1;
                }
                if(s.scoreLead<0&&s.remaining<35)
                {scores[0]+=.4f;if(longs.Length>0)scores[1]+=.5f;}
            }
            else if(!s.enemyPossession)
            {
                var near=fields.Count(p=>Vector2.Distance(p.position,s.ball)<12);
                Offer(FiveCommand.FastTransition,3.5f,"loose-ball-one-chaser-and-forward-outlets");
                Offer(FiveCommand.AllRecover,near>=2&&ownArrival<otherArrival+.8f?5:2,"loose-ball-arrival-support");
                if(s.velocity.x<-3&&s.ball.x<0)
                {
                    var t=(-s.halfLength-s.ball.x)/s.velocity.x;
                    var y=s.ball.y+s.velocity.y*t;
                    if(t>0&&t<s.decisionSeconds&&Mathf.Abs(y)<s.goalHalfWidth+3)
                        Offer(FiveCommand.GoalBlock,10,"incoming-ball-intersects-goal");
                }
            }
            else
            {
                var b=s.carrierPosition;var motion=s.carrierVelocity;
                var attackDepth=b.x+s.halfLength;
                var central=Mathf.Abs(b.y)<19;
                var nearest=fields.Min(p=>Vector2.Distance(p.position,b));
                var support=fields.Count(p=>Vector2.Distance(p.position,b)<23);
                var predicted=b+motion*Mathf.Min(1.5f,s.decisionSeconds);
                var defenders=fields.Where(p=>p.slot<=2).ToArray();
                var intercept=defenders.Any(p=>p.position.x<b.x-2&&Arrival(s,p,predicted)<s.decisionSeconds);
                var nearestDefender=defenders.Min(p=>Vector2.Distance(p.position,b));
                var closing=Mathf.Max(0,-motion.x);
                var threat=central?Mathf.Clamp01((34-attackDepth)/24):0;
                if(central&&closing>3&&attackDepth-closing*s.decisionSeconds<20&&!intercept)threat=Mathf.Max(threat,.85f);
                d.goalThreat=threat;d.pressureDistance=nearest;
                Offer(FiveCommand.FastTransition,settings.singlePressure+(nearest<10?.6f:0),"single-pressure-with-transition-outlets");
                Offer(FiveCommand.WideTrap,intercept?settings.contain:nearestDefender<14?4.6f:2.4f,"defender-can-meet-visible-runner");
                Offer(FiveCommand.AllRecover,nearest<settings.recoverRange&&support>=settings.recoverSupport?settings.recover+threat*settings.recoverThreat:2.3f,"supported-ball-pressure-before-shot");
                // Read only visible geometry: a wide carrier with an unmarked central finisher
                // needs receiver coverage, even though the carrier itself has a poor shot angle.
                var passView=new FiveObservation {own=s.opponents,opponents=s.own,ballRadius=s.ballRadius,
                    freeBallDamping=s.freeBallDamping,maximumBallSpeed=s.maximumBallSpeed,acceleration=s.acceleration};
                var cutbackRisk=Mathf.Abs(b.y)>14&&attackDepth<35&&s.opponents.Any(p=>!p.keeper&&p.slot!=s.ownerSlot
                    &&p.position.x<-s.halfLength+25&&Mathf.Abs(p.position.y)<16
                    &&fields.Min(f=>Vector2.Distance(f.position,p.position))>3
                    &&PassingLaneOpen(passView,s.ball,p.position,p.slot));
                d.cutbackThreat=cutbackRisk?1:0;
                Offer(FiveCommand.MarkReceivers,cutbackRisk?7.2f:motion.magnitude<2&&nearest>12?3.8f:1.8f,"deny-visible-central-finisher");
                Offer(FiveCommand.GoalBlock,1+threat*settings.goalBlock,"goal-approach-time-and-central-angle");
                if(s.scoreLead<0&&s.remaining<35&&threat<.7f)scores[6]+=.8f;
                if(s.scoreLead>0&&s.remaining<35&&central&&attackDepth<35)scores[7]+=.8f;
                if(s.stallSeconds>6&&Mathf.Abs(b.y)>12&&motion.magnitude<1)
                {scores[4]+=2;scores[6]+=s.stallSeconds>12?3:0;scores[7]-=3;}
                if(s.stallSeconds>12)scores[(int)s.ownCommand]-=2;
                if(s.stallSeconds>12&&motion.magnitude<1&&s.ownCommand==FiveCommand.AllRecover)
                    Offer(FiveCommand.MarkReceivers,16,"stalled-recovery-release-crowding-and-deny-receivers");
            }
            // Hysteresis never changes the decision period or creates an additional command.
            if(s.commandAge<6&&scores[(int)s.ownCommand]>0)scores[(int)s.ownCommand]+=settings.switchBonus;
            var best=0;
            for(var i=0;i<8;i++)
            {
                if(!float.IsFinite(scores[i]))throw new InvalidOperationException("Non-finite rule score.");
                if(scores[i]>scores[best])best=i;
            }
            d.action=best;d.reason=reason[best]??"candidate-comparison";return d;
        }
    }
}
