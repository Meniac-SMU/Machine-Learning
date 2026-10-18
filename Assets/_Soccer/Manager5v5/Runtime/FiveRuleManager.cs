using System;
using System.Linq;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    public struct FiveVisiblePlayer
    {
        public Vector2 position,velocity,forward;
        public float stamina,radius;
        public bool keeper;
        public int slot;
    }
    public sealed class FiveObservation
    {
        public Vector2 ball,velocity,carrierPosition,carrierVelocity;
        public FiveVisiblePlayer[] own,opponents;
        public bool ownPossession,enemyPossession,ownPassInFlight,ownPassPreparing;
        public float halfLength,halfWidth,remaining,stallSeconds,unproductiveSeconds,decisionSeconds,ballRadius,maximumPlayerSpeed;
        public float goalHalfWidth,acceleration,freeBallDamping,maximumBallSpeed,commandAge;
        public int ownerSlot=-1,passReceiverSlot=-1;
        public int scoreLead;
        public FiveCommand ownCommand;
        public static FiveObservation Capture(FiveMatch match,Team team)
        {
            var sign=FiveMatch.AttackSign(team);
            Func<FivePlayer,FiveVisiblePlayer> visible=p=>new FiveVisiblePlayer {position=p.Position*sign,velocity=p.Velocity*sign,forward=p.Forward*sign,stamina=p.IsKeeper?1:p.Stamina.Value,radius=p.radius,keeper=p.IsKeeper,slot=p.slot};
            var ownPass=match.PassPhase!=FivePassPhase.None&&match.PassSender>=0&&match.players[match.PassSender].team==team;
            return new FiveObservation {ball=match.BallPosition*sign,velocity=match.BallVelocity*sign,carrierPosition=(match.Carrier!=null?match.Carrier.Position:match.BallPosition)*sign,
                carrierVelocity=(match.Carrier!=null?match.Carrier.Velocity:Vector2.zero)*sign,maximumPlayerSpeed=match.profile.maxSpeed,
                own=match.players.Where(p=>p.team==team).Select(visible).ToArray(),
                opponents=match.players.Where(p=>p.team!=team).Select(visible).ToArray(),
                ownPossession=match.Carrier!=null&&match.Carrier.team==team,enemyPossession=match.Carrier!=null&&match.Carrier.team!=team,
                ownPassInFlight=match.PassPhase==FivePassPhase.InFlight&&match.players[match.PassSender].team==team,
                ownPassPreparing=ownPass&&match.PassPhase==FivePassPhase.Preparing,
                ownerSlot=match.Carrier!=null?match.Carrier.slot:-1,passReceiverSlot=ownPass?match.PassReceiver%5:-1,
                goalHalfWidth=match.geometry.GoalHalfWidth,acceleration=match.profile.acceleration,
                freeBallDamping=match.profile.EffectiveFreeBallDamping,maximumBallSpeed=match.profile.maximumBallSpeed,
                commandAge=Mathf.Max(0,match.Elapsed-match.Decision(team).selectedAt),
                halfLength=match.geometry.HalfLength,halfWidth=match.geometry.HalfWidth,remaining=match.profile.duration-match.Elapsed,
                stallSeconds=match.ObservedStallSeconds,unproductiveSeconds=match.ObservedUnproductiveSeconds(team),decisionSeconds=match.profile.decisionSeconds,ballRadius=match.ballRadius,
                scoreLead=(match.RedScore-match.NavyScore)*(team==Team.Red?1:-1),ownCommand=match.Decision(team).command};
        }
    }
    public static class FiveRuleManager
    {
        public static FiveCommand Choose(FiveObservation seen,out string reason)
        {
            var fields=seen.own.Where(p=>!p.keeper).ToArray();
            var close=fields.Count(p=>Vector2.Distance(p.position,seen.ball)<9);
            var energy=fields.Average(p=>p.stamina);
            var lateChase=seen.scoreLead<0&&seen.remaining<35;
            if(!seen.ownPossession&&!seen.enemyPossession)
            {
                if(seen.ownPassInFlight){reason="own-pass-already-in-flight";return seen.ownCommand;}
                if(seen.ball.x<0&&seen.velocity.x<-3&&seen.ball.x+seen.velocity.x*seen.decisionSeconds<-seen.halfLength+23)
                {reason="incoming-ball-risk-before-next-decision";return FiveCommand.GoalBlock;}
                if(close>=2 || seen.ball.x<-seen.halfLength+15){reason="nearby-loose-ball-window";return FiveCommand.AllRecover;}
                reason="one-player-loose-ball-approach";return FiveCommand.FastTransition;
            }
            if(seen.ownPossession)
            {
                if(seen.stallSeconds>6)
                {
                    if(seen.ownCommand==FiveCommand.WingCutback)
                    {reason="observed-cutback-stall-rebuild-wide-links";return FiveCommand.WideCirculation;}
                    if(seen.ownCommand==FiveCommand.WideCirculation)
                    {reason="observed-wide-links-still-blocked-try-carry";return FiveCommand.FastTransition;}
                    if(seen.ball.x>seen.halfLength-30&&seen.ownCommand!=FiveCommand.WingCutback)
                    {reason="observed-stall-change-to-wide";return FiveCommand.WingCutback;}
                    if(seen.ownCommand==FiveCommand.FastTransition)
                    {reason="observed-stall-change-to-pass";return FiveCommand.WideCirculation;}
                    reason="observed-stall-change-to-carry";return FiveCommand.FastTransition;
                }
                if(seen.unproductiveSeconds>6)
                {
                    reason="no-forward-progress-change-approach";
                    return seen.ownCommand==FiveCommand.FastTransition?FiveCommand.WideCirculation:FiveCommand.FastTransition;
                }
                var pressure=seen.opponents.Count(p=>Mathf.Min(Vector2.Distance(p.position,seen.ball),Vector2.Distance(p.position,seen.carrierPosition))<7);
                var longOption=fields.Any(p=>p.position.x>seen.ball.x+18&&Vector2.Distance(p.position,seen.ball)>22&&Open(seen,p.position));
                if(seen.ball.x<-seen.halfLength+20&&pressure>0&&!longOption)
                {reason="own-goal-pressure-protect-before-exit";return FiveCommand.GoalBlock;}
                if(seen.ownCommand==FiveCommand.WingCutback&&seen.ball.x>seen.halfLength-22&&Mathf.Abs(seen.ball.y)>seen.halfWidth*.4f)
                {reason="own-wide-pattern-in-progress";return FiveCommand.WingCutback;}
                var deep=seen.opponents.Count(p=>!p.keeper&&p.position.x>seen.halfLength-24&&Mathf.Abs(p.position.y)<16);
                if(deep>=3&&seen.ball.x>0){reason="visible-low-central-block";return FiveCommand.WingCutback;}
                if(pressure>0&&longOption){reason="pressure-with-open-distant-receiver";return FiveCommand.LongPass;}
                if(longOption&&seen.ball.x<seen.halfLength/3&&(energy<.35f||lateChase))
                {reason=energy<.35f?"tired-carrier-open-distant-exit":"late-deficit-open-distant-exit";return FiveCommand.LongPass;}
                var frontBlocked=seen.opponents.Any(p=>p.position.x>seen.ball.x&&p.position.x<seen.ball.x+15&&Mathf.Abs(p.position.y-seen.ball.y)<4);
                var wideOption=fields.Any(p=>Mathf.Abs(p.position.y-seen.ball.y)>12&&Mathf.Abs(p.position.x-seen.ball.x)<24&&Open(seen,p.position));
                if((pressure>0||frontBlocked)&&wideOption){reason="central-pressure-open-wide-link";return FiveCommand.WideCirculation;}
                if(energy<.35f&&wideOption)
                {reason="tired-possession-open-wide-link";return FiveCommand.WideCirculation;}
                reason="available-direct-forward-space";return FiveCommand.FastTransition;
            }
            // A controlled ball can briefly slow or reverse within its dribble anchor. Read the visible runner's movement for a carry threat.
            // A stationary wide carrier is not a central shot threat. Challenge the visible corner hold
            // at the normal decision boundary instead of letting a goal block concede unlimited time.
            // Sustained observed ball displacement already defines the stall. A controlled ball's
            // contact/anchor velocity can remain nonzero against a wall while its owner is stationary.
            var stationaryCarrier=seen.carrierVelocity.magnitude<1;
            if(seen.stallSeconds>12&&stationaryCarrier&&seen.ownCommand==FiveCommand.AllRecover&&(seen.ball.x>0||Mathf.Abs(seen.ball.y)>12))
            {reason="crowded-recovery-no-progress-rebuild-denial";return FiveCommand.MarkReceivers;}
            if(seen.stallSeconds>6&&Mathf.Abs(seen.ball.y)>12&&stationaryCarrier)
            {
                if(seen.stallSeconds>12)
                {reason="wide-pressure-no-progress-commit-recovery";return FiveCommand.AllRecover;}
                reason="stationary-wide-carrier-release-goal-block";return FiveCommand.WideTrap;
            }
            var closingVelocity=seen.enemyPossession?seen.carrierVelocity.x:seen.velocity.x;
            var projectedDanger=closingVelocity<-3&&seen.ball.x+closingVelocity*seen.decisionSeconds<-seen.halfLength+23;
            if(closingVelocity<-3&&seen.ball.x<seen.halfLength/3)
            {
                var attackTime=Mathf.Max(0,(seen.ball.x+seen.halfLength-23)/-closingVelocity);
                var retreatTime=fields.Where(p=>p.slot==1||p.slot==2).Max(p=>Mathf.Max(0,p.position.x+seen.halfLength-7)/Mathf.Max(1,seen.maximumPlayerSpeed*.9f));
                projectedDanger|=attackTime<retreatTime+seen.decisionSeconds;
            }
            var interceptionLine=closingVelocity<-3&&seen.ball.x>-seen.halfLength+30&&energy>=.35f
                &&fields.Where(p=>p.slot==1||p.slot==2).All(p=>p.position.x<seen.ball.x-3&&Vector2.Distance(p.position,seen.ball)<35);
            if(seen.ball.x<-seen.halfLength+23 || (projectedDanger&&!interceptionLine) || (seen.scoreLead>0&&seen.remaining<35&&seen.ball.x<0) || (energy<.35f&&seen.ball.x<0))
            {reason="immediate-goal-risk-or-tired-defence";return FiveCommand.GoalBlock;}
            if(interceptionLine)
            {reason="running-carrier-meets-defensive-line";return FiveCommand.WideTrap;}
            if(close>=3&&energy>.45f){reason="nearby-collective-recovery-window";return FiveCommand.AllRecover;}
            if(lateChase&&close>=1&&energy>.45f&&fields.Count(p=>Vector2.Distance(p.position,seen.ball)<23)>=2)
            {reason="late-deficit-supported-recovery-risk";return FiveCommand.AllRecover;}
            if(Mathf.Abs(seen.ball.y)<12&&close>=1&&energy>.45f&&fields.Count(p=>Vector2.Distance(p.position,seen.ball)<23)>=2)
            {reason="central-carrier-with-nearby-recovery-support";return FiveCommand.AllRecover;}
            if(Mathf.Abs(seen.ball.y)>seen.halfWidth*.36f && fields.Count(p=>Vector2.Distance(p.position,seen.ball)<23)>=2)
            {reason="visible-wide-reception-zone";return FiveCommand.WideTrap;}
            reason="deny-advanced-visible-receivers";return FiveCommand.MarkReceivers;
        }
        static bool Open(FiveObservation seen,Vector2 receiver)
            => seen.opponents.All(p=>FiveMatch.DistanceToSegment(p.position,seen.ball,receiver)>p.radius+seen.ballRadius+.5f);
    }
    public sealed partial class FiveMatch
    {
        public bool[] ruleManagers={true,true};
        public FiveRuleVariant[] ruleVariants={FiveRuleVariant.Strong,FiveRuleVariant.Strong};
        readonly FiveRuleDecision[] ruleDiagnostics=new FiveRuleDecision[2];
        public FiveRuleDecision RuleDiagnostic(Team team)=>ruleDiagnostics[(int)team];
        public FiveCommand ChooseRule(Team team,out string reason)
        {
            var seen=FiveObservation.Capture(this,team);
            var result=ruleVariants[(int)team]==FiveRuleVariant.Strong?FiveStrongRule.Evaluate(seen):FiveStrongRule.Legacy(seen);
            if(ruleVariants[(int)team]==FiveRuleVariant.Strong)result=ForecastRule(team,seen,result);
            if(CommandBlockedSelections(team,(FiveCommand)result.action)>0)
            {
                var scores=result.scores??FiveStrongRule.Evaluate(seen).scores;
                result.action=Enumerable.Range(0,8).Where(a=>CommandBlockedSelections(team,(FiveCommand)a)==0)
                    .OrderByDescending(a=>scores[a]).First();
                result.reason="command-consecutive-limit-two-alternative-selections";
            }
            ruleDiagnostics[(int)team]=result;reason=result.reason;return (FiveCommand)result.action;
        }
        // Diagnostics receive exactly the public observation used by the selector, never the opponent command.
        public event Action<Team,FiveObservation,FiveCommand,string> RuleDecisionMade;
        Vector2 lastObservedBall;
        float lastBallMotionAt;
        readonly float[] lastProgressAt=new float[2],bestProgressX=new float[2];
        readonly bool[] controlledLastTick=new bool[2];
        public float ObservedStallSeconds=>Mathf.Max(0,Elapsed-lastBallMotionAt);
        public float ObservedUnproductiveSeconds(Team team)=>Mathf.Max(0,Elapsed-lastProgressAt[(int)team]);
        partial void ResetRuleObservation()
        {
            Array.Clear(ruleDiagnostics,0,ruleDiagnostics.Length);
            lastObservedBall=BallPosition;lastBallMotionAt=Elapsed;
            for(int t=0;t<2;t++){lastProgressAt[t]=Elapsed;bestProgressX[t]=BallPosition.x*AttackSign((Team)t);controlledLastTick[t]=false;}
        }
        partial void ObserveBall()
        {
            if(Vector2.Distance(lastObservedBall,BallPosition)>.5f){lastObservedBall=BallPosition;lastBallMotionAt=Elapsed;}
            for(int t=0;t<2;t++)
            {
                var team=(Team)t;
                var controlled=(Carrier!=null&&Carrier.team==team)||(PassPhase==FivePassPhase.InFlight&&PassSender>=0&&players[PassSender].team==team);
                var x=BallPosition.x*AttackSign(team);
                if(!controlled||!controlledLastTick[t]){lastProgressAt[t]=Elapsed;bestProgressX[t]=x;}
                else if(x>=bestProgressX[t]+2){bestProgressX[t]=x;lastProgressAt[t]=Elapsed;}
                controlledLastTick[t]=controlled;
            }
        }
        partial void ChooseRuleCommands()
        {
            for(var t=0;t<2;t++)
            {
                if(!ruleManagers[t]||DefensiveFormationActive((Team)t)||Elapsed+.0001f<decisions[t].nextDecisionAt)continue;
                var observation=FiveObservation.Capture(this,(Team)t);
                var command=ChooseRule((Team)t,out var reason);
                if(SelectCommand((Team)t,command,reason))RuleDecisionMade?.Invoke((Team)t,observation,command,reason);
            }
        }
    }
}
