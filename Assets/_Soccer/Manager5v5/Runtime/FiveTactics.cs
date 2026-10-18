using System;
using System.Linq;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5
{
    public enum FiveCommand { FastTransition, LongPass, WideCirculation, WingCutback, WideTrap, MarkReceivers, AllRecover, GoalBlock }
    [Serializable] public sealed class FiveDecision
    {
        public FiveCommand command=FiveCommand.FastTransition;
        public float selectedAt,lastDecisionAt,nextDecisionAt;
        public int revision;
        public int[] consecutiveSelections=new int[8];
        public int[] blockedSelections=new int[8];
        public int consecutiveRecover=>consecutiveSelections[(int)FiveCommand.AllRecover];
        public int recoverBlockedSelections=>blockedSelections[(int)FiveCommand.AllRecover];
        public int consecutiveLongPass=>consecutiveSelections[(int)FiveCommand.LongPass];
        public int longPassBlockedSelections=>blockedSelections[(int)FiveCommand.LongPass];
        public string reason="initial";
    }
    public sealed partial class FiveMatch
    {
        public bool tacticsEnabled=true;
        public bool[] teamPlanningEnabled={true,true};
        public FiveDecision[] decisions={new FiveDecision(),new FiveDecision()};
        public int[] tacticSteps=new int[16];
        readonly Vector2[] cutbackTargets=new Vector2[2];
        readonly float[] nextCutbackPlanAt=new float[2];
        readonly int[] attackingChaser={-1,-1};
        readonly int[] chaserRevision={-1,-1};
        readonly float[] nextChaserPlanAt=new float[2];
        readonly int[][] recoveryOrder=new int[2][];
        readonly int[] recoveryRevision={-1,-1};
        readonly float[] nextRecoveryPlanAt=new float[2];
        readonly Vector2[] recoveryHold=new Vector2[2];
        partial void ChooseRuleCommands();
        public FiveDecision Decision(Team team)=>decisions[(int)team];
        public static int CommandConsecutiveLimit(FiveCommand command)=>command==FiveCommand.AllRecover?2:3;
        public int CommandBlockedSelections(Team team,FiveCommand command)=>Decision(team).blockedSelections[(int)command];
        static void AdvanceCommandLimit(bool selected,int limit,ref int consecutive,ref int blocked)
        {
            if(selected)
            {
                consecutive++;
                if(consecutive>=limit)blocked=2;
            }
            else
            {
                consecutive=0;
                if(blocked>0)blocked--;
            }
        }
        public bool SelectCommand(Team team,FiveCommand command,string reason="manual",bool scenarioOverride=false)
        {
            if((int)command<0 || (int)command>7)throw new ArgumentOutOfRangeException(nameof(command));
            var state=Decision(team);
            if(!scenarioOverride && !CommandAvailable(team,command))return false;
            if(!scenarioOverride && Elapsed+.0001f<state.nextDecisionAt)return false;
            // Only accepted manager choices advance these independent limits.
            for(int action=0;action<8;action++)
                AdvanceCommandLimit((int)command==action,CommandConsecutiveLimit((FiveCommand)action),
                    ref state.consecutiveSelections[action],ref state.blockedSelections[action]);
            if(state.command!=command){state.selectedAt=Elapsed;state.revision++;}
            state.command=command;state.lastDecisionAt=Elapsed;state.nextDecisionAt=Elapsed+profile.decisionSeconds;state.reason=reason;
            Record("command",(int)team,(int)command,state.revision,reason);return true;
        }
        partial void ResetTactics(bool newMatch)
        {
            ResetDefensiveFormation(newMatch);
            if(newMatch){decisions=new[]{new FiveDecision(),new FiveDecision()};Array.Clear(tacticSteps,0,tacticSteps.Length);}
            // Goal restart preserves the chosen command and stamina, but permits a new decision on restart.
            foreach(var state in decisions)state.nextDecisionAt=Elapsed;
            Array.Clear(nextCutbackPlanAt,0,nextCutbackPlanAt.Length);
            for(int team=0;team<2;team++)cutbackReceiver[team]=-1;
            for(int team=0;team<2;team++){attackingChaser[team]=chaserRevision[team]=-1;nextChaserPlanAt[team]=0;}
            for(int team=0;team<2;team++){recoveryOrder[team]=null;recoveryRevision[team]=-1;nextRecoveryPlanAt[team]=0;}
        }
        partial void PlanTactics(float dt)
        {
            if(!tacticsEnabled)return;
            ChooseRuleCommands();
            if(teamPlanningEnabled[0])PlanTeam(Team.Red);
            if(teamPlanningEnabled[1])PlanTeam(Team.Navy);
        }
        Vector2 Local(Vector2 point,Team team)=>point*AttackSign(team);
        Vector2 World(Vector2 point,Team team)=>point*AttackSign(team);
        FivePlayer Player(Team team,int slot)=>players[(int)team*5+slot];
        void Target(FivePlayer player,Vector2 localPoint,Vector2? face=null)
        {
            var world=ClampField(World(localPoint,player.team));
            player.SetTarget(world,face ?? BallPosition);
        }
        void ApproachBall(FivePlayer player)
        {
            if(TryChallengeRoute(player))return;
            var direction=BallPosition-player.Position;
            var behind=BallPosition-direction.normalized*profile.holdOffset;
            player.SetTarget(ClampField(behind,player.radius),BallPosition,true);
        }
        void PlanTeam(Team team)
        {
            formationDeferredPlan[(int)team]=DefensiveFormationActive(team);
            if(formationDeferredPlan[(int)team])return;
            var command=Decision(team).command;
            tacticSteps[(int)team*8+(int)command]++;
            var p1=Player(team,1);var p2=Player(team,2);var p3=Player(team,3);var p4=Player(team,4);
            var b=Local(BallPosition,team);
            float length=geometry.HalfLength,width=geometry.HalfWidth;
            var side=Mathf.Abs(b.y)>2?Mathf.Sign(b.y):1;
            var ours=Carrier!=null&&Carrier.team==team;
            var ourPass=PassPhase==FivePassPhase.InFlight&&PassSender>=0&&players[PassSender].team==team;
            var ordered=new[]{p1,p2,p3,p4}.OrderBy(p=>(p.Position-BallPosition).sqrMagnitude).ThenBy(p=>p.slot).ToArray();
            switch(command)
            {
                case FiveCommand.FastTransition:
                    Target(p1,new Vector2(b.x-5,-10));Target(p2,new Vector2(b.x-17,9));
                    Target(p3,new Vector2(b.x+17,-12));Target(p4,new Vector2(b.x+13,12));
                    if(ours)PlanFastSupport(team,Carrier);
                    break;
                case FiveCommand.LongPass:
                    Target(p1,new Vector2(b.x-6,-10));Target(p2,new Vector2(b.x-22,8));
                    Target(p3,new Vector2(Mathf.Max(20,b.x+30),-16));Target(p4,new Vector2(Mathf.Max(16,b.x+24),16));
                    break;
                case FiveCommand.WideCirculation:
                    Target(p1,new Vector2(b.x-8,-width*.57f));Target(p2,new Vector2(b.x-8,width*.57f));
                    Target(p3,new Vector2(b.x+18,-width*.57f));Target(p4,new Vector2(b.x+18,width*.57f));
                    break;
                case FiveCommand.WingCutback:
                    if(expandedCutbackEnabled){PlanExpandedCutback(team,b,ours);break;}
                    Target(p1,new Vector2(b.x-10,side*width*.66f));Target(p2,new Vector2(b.x-22,0));
                    var finishing=ours&&Carrier==p4?p3:p4;
                    var wideRunner=finishing==p4?p3:p4;
                    Target(wideRunner,new Vector2(Mathf.Max(b.x+12,length-10),side*width*.66f));
                    if(Elapsed>=nextCutbackPlanAt[(int)team])
                    {
                        cutbackTargets[(int)team]=ChooseCutbackPoint(finishing,side);
                        nextCutbackPlanAt[(int)team]=Elapsed+.5f;
                    }
                    finishing.SetTarget(cutbackTargets[(int)team],BallPosition);
                    break;
                case FiveCommand.WideTrap:
                    if(!ours && !ourPass)
                    {
                        // T5 contains a running carrier with two defenders; two fields retain depth.
                        // The horizon collapses on approach so the defender meets, rather than retreats from, the runner.
                        var motion=Local(Carrier!=null?Carrier.Velocity:BallVelocity,team);
                        var distance=Mathf.Min(Vector2.Distance(p1.Position,BallPosition),Vector2.Distance(p2.Position,BallPosition));
                        var horizon=Mathf.Min(1.5f,distance/(profile.maxSpeed+motion.magnitude));
                        var intercept=b+motion*horizon;
                        AssignDefensivePair(p1,p2,World(intercept,team),World(intercept+new Vector2(-4,6),team));
                        Target(p3,new Vector2(b.x-25,-12));Target(p4,new Vector2(b.x-25,12));
                    }
                    else
                    {
                        Target(ordered[0],b+new Vector2(-3,0));
                        Target(ordered[1],new Vector2(b.x-3,side*width*.55f));
                        Target(ordered[2],new Vector2(b.x+6,side*width*.35f));
                        Target(ordered[3],new Vector2(b.x-22,0));
                    }
                    break;
                case FiveCommand.MarkReceivers:
                    var threats=players.Where(p=>p.team!=team&&!p.IsKeeper&&p.Index!=Owner)
                        .OrderBy(p=>Local(p.Position,team).x+Mathf.Abs(p.Position.y)*.15f).ThenBy(p=>p.slot).Take(2).ToArray();
                    if(threats.Length==2)
                    {
                        var first=threats[0].Position+(BallPosition-threats[0].Position).normalized*1.25f;
                        var second=threats[1].Position+(BallPosition-threats[1].Position).normalized*1.25f;
                        AssignDefensivePair(p1,p2,first,second);
                    }
                    Target(p3,b+new Vector2(-3,0));Target(p4,b+new Vector2(4,-side*8));
                    break;
                case FiveCommand.AllRecover:
                    if(ours)
                    {
                        // Recovery is complete. Hold the reached perimeter until the manager selects the next action.
                        // Driving all support players through the new owner caused immediate self-congestion.
                        foreach(var support in ordered.Where(p=>p!=Carrier))support.SetTarget(support.Position,BallPosition);
                    }
                    else
                    {
                        var t=(int)team;
                        if(recoveryOrder[t]==null||recoveryRevision[t]!=Decision(team).revision||Elapsed>=nextRecoveryPlanAt[t])
                        {
                            recoveryOrder[t]=ordered.Select(p=>p.Index).ToArray();
                            recoveryHold[t]=ordered[3].Position;
                            recoveryRevision[t]=Decision(team).revision;nextRecoveryPlanAt[t]=Elapsed+profile.decisionSeconds;
                        }
                        ApproachBall(players[recoveryOrder[t][0]]);
                        var approaches=players.Where(p=>p.team!=team&&!p.IsKeeper&&p.Index!=Owner)
                            .OrderBy(p=>(p.Position-BallPosition).sqrMagnitude).ThenBy(p=>p.slot).Take(2).ToArray();
                        for(int i=0;i<2;i++)
                        {
                            var approach=approaches[i].Position-BallPosition;
                            players[recoveryOrder[t][i+1]].SetTarget(ClampField(BallPosition+approach.normalized*Mathf.Min(3,approach.magnitude*.5f)),BallPosition);
                        }
                        players[recoveryOrder[t][3]].SetTarget(recoveryHold[t],BallPosition);
                    }
                    break;
                case FiveCommand.GoalBlock:
                    var blockX=Mathf.Clamp(b.x-4,-length+3,-length+7);
                    if(adaptiveGoalBlockEnabled)blockX=Mathf.Clamp(b.x-9,-length+4,-length+16);
                    if(!ours&&b.x<-length+27&&Mathf.Abs(b.y)<19)
                    {
                        var existingLine=Mathf.Min(Local(p1.Position,team).x,Local(p2.Position,team).x);
                        if(existingLine<b.x-3&&existingLine<-length+18)blockX=Mathf.Max(blockX,existingLine);
                    }
                    var fraction=Mathf.Clamp01((blockX-b.x)/Mathf.Min(-.01f,-length-b.x));
                    AssignDefensivePair(p1,p2,
                        World(new Vector2(blockX,Mathf.Lerp(b.y,-geometry.GoalHalfWidth*.75f,fraction)),team),
                        World(new Vector2(blockX,Mathf.Lerp(b.y,geometry.GoalHalfWidth*.75f,fraction)),team));
                    Target(p4,new Vector2(-length+13,Mathf.Clamp(b.y,-8,8)));
                    Target(p3,new Vector2(Mathf.Max(4,b.x+27),-side*16));
                    if(adaptiveGoalBlockEnabled&&ours&&Carrier==p3)
                    {
                        // The carrier joins the protected three; the other attacker becomes the outlet.
                        Target(p4,new Vector2(Mathf.Max(4,b.x+18),-side*16));
                    }
                    if(!ours&&b.x<-length+18&&Mathf.Abs(b.y)<12)
                    {
                        Target(p4,b+new Vector2(-.7f,0));
                    }
                    var incoming=Local(BallVelocity,team);
                    if(!ours&&!ourPass&&Carrier==null&&incoming.x<-3&&b.x<0)
                    {
                        var goalY=b.y+incoming.y*(-length-b.x)/incoming.x;
                        if(Mathf.Abs(goalY)<=geometry.GoalHalfWidth+ballRadius)
                        {
                            FivePlayer interceptor=null;Vector2 interceptPoint=Vector2.zero;float bestArrivalMargin=float.NegativeInfinity;
                            foreach(var blocker in new[]{p1,p2,p4})
                            {
                                var line=Local(blocker.Position,team).x;
                                var arrival=(line-b.x)/incoming.x;
                                if(arrival<=0||arrival>=1.5f)continue;
                                var point=new Vector2(line,b.y+incoming.y*arrival);
                                var movement=Vector2.Distance(Local(blocker.Position,team),point)/Mathf.Max(1,profile.maxSpeed*blocker.SpeedFactor);
                                if(arrival-movement>bestArrivalMargin){bestArrivalMargin=arrival-movement;interceptor=blocker;interceptPoint=point;}
                            }
                            // One nearby field meets the shot; the other two retain goal coverage instead of colliding on one ray.
                            if(interceptor!=null)Target(interceptor,interceptPoint);
                        }
                    }
                    break;
            }
            if(improvedPassPreparationEnabled&&(command==FiveCommand.LongPass||command==FiveCommand.WideCirculation))
                foreach(var receiver in new[]{p1,p2,p3,p4})
                    if(receiver!=Carrier)ReceiveTarget(receiver,receiver.Target);
            if(ours)
            {
                attackingChaser[(int)team]=-1;
                if(!Carrier.IsKeeper)PlanCarrier(Carrier,command);
            }
            else if(!ourPass&&(int)command<4)
            {
                // The attacking shape has one assigned presser for this decision window, not a new defender every physics tick.
                var t=(int)team;
                if(attackingChaser[t]<0||Elapsed>=nextChaserPlanAt[t]||chaserRevision[t]!=Decision(team).revision)
                {
                    var selected=ordered[0].Index;
                    if(selected!=attackingChaser[t])Record("attacking-chaser",t,selected,Decision(team).revision);
                    attackingChaser[t]=selected;chaserRevision[t]=Decision(team).revision;nextChaserPlanAt[t]=Elapsed+profile.decisionSeconds;
                }
                ApproachBall(players[attackingChaser[t]]);
            }
            else attackingChaser[(int)team]=-1;
        }
        void PlanFastSupport(Team team,FivePlayer carrier)
        {
            // Stable role order: two runners even when an attacker carries, with the remaining field covering.
            // Runners use the ordinary non-dribbling maximum; no extra speed or stamina exemption.
            var supports=players.Where(p=>p.team==team&&!p.IsKeeper&&p!=carrier)
                .OrderByDescending(p=>p.slot>=3).ThenBy(p=>p.slot).ToArray();
            var b=Local(carrier.Position,team);
            for(int i=0;i<supports.Length;i++)
            {
                var point=i<2
                    ?new Vector2(Mathf.Min(geometry.HalfLength-8,Mathf.Max(8,b.x+16+i*6)),i==0?-11:11)
                    :new Vector2(b.x-14,i==2?-6:6);
                Target(supports[i],point);
            }
        }
        void PlanCarrier(FivePlayer player,FiveCommand command)
        {
            if(IsHuman(player))return;
            var b=Local(player.Position,player.team);
            float length=geometry.HalfLength;
            if(b.x>length-27&&Mathf.Abs(b.y)<19)
            {
                var aim=ChooseShotTarget(player);
                if(LaneOpen(BallPosition,aim,player.team,.1f)&&RequestShot(player,aim))return;
            }
            switch(command)
            {
                case FiveCommand.FastTransition:
                    var goal=World(new Vector2(length,0),player.team);
                    var next=player.Position+(goal-player.Position).normalized*10;
                    player.SetTarget(ClampField(next),next);
                    if(NearestOpponentDistance(player)<5)
                    {
                        var support=FindReceiver(player,4,22,p=>Local(p.Position,player.team).x-b.x>=3,
                            p=>Local(p.Position,player.team).x-b.x-Vector2.Distance(p.Position,player.Position)*.2f);
                        if(support!=null)RequestPass(player,support);
                    }
                    break;
                case FiveCommand.LongPass:
                    var distant=FindReceiver(player,22,85,p=>Local(p.Position,player.team).x>b.x+16,
                        p=>Local(p.Position,player.team).x+NearestOpponentDistance(p)*.3f);
                    CarryForward(player);
                    if(distant!=null)RequestPass(player,distant);
                    break;
                case FiveCommand.WideCirculation:
                    var link=FindReceiver(player,6,55,p=>Local(p.Position,player.team).x-b.x>=4&&Local(p.Position,player.team).x-b.x<=24,
                        p=>Local(p.Position,player.team).x-b.x+Mathf.Abs(p.Position.y-player.Position.y)*.15f+NearestOpponentDistance(p)*.2f);
                    // Give the next wide runner time to advance. A safe sideways/backward escape is only needed under close pressure.
                    if(link==null&&NearestOpponentDistance(player)<5)
                        link=FindReceiver(player,6,55,p=>Local(p.Position,player.team).x-b.x>=-10&&Local(p.Position,player.team).x-b.x<=24,
                            p=>Local(p.Position,player.team).x-b.x+NearestOpponentDistance(p)*.2f,true);
                    CarryForward(player);
                    if(link!=null)RequestPass(player,link);
                    break;
                case FiveCommand.WingCutback:
                    if(expandedCutbackEnabled){PlanExpandedCutbackCarrier(player,b);break;}
                    var side=Mathf.Abs(b.y)>2?Mathf.Sign(b.y):1;
                    var winger=new Vector2(b.x>length-16?length-6:Mathf.Min(length-6,b.x+10),side*geometry.HalfWidth*.66f);
                    var cutback=Player(player.team,player.slot==4?3:4);
                    player.SetTarget(ClampField(World(winger,player.team)),World(winger,player.team));
                    if(b.x>length-17&&Mathf.Abs(b.y)>14&&cutback!=player&&Vector2.Distance(cutback.Position,cutbackTargets[(int)player.team])<3
                        &&PassingLaneOpen(BallPosition,cutback.Position,player.team,cutback.Index))RequestPass(player,cutback);
                    break;
                default:
                    if(command==FiveCommand.GoalBlock&&adaptiveGoalBlockEnabled)
                    {
                        PlanProtectedCarrier(player);break;
                    }
                    if(command==FiveCommand.GoalBlock&&player.slot!=3&&b.x>-length+20)
                    {
                        var retreat=World(new Vector2(-length+10,player.slot==1?-6.5f:6.5f),player.team);
                        player.SetTarget(ClampField(retreat),retreat);break;
                    }
                    // Recovery/protection must not pin the carrier in place until the next manager decision.
                    CarryForward(player);
                    if(command==FiveCommand.GoalBlock&&b.x<=-length+20)
                    {
                        var outlet=FindReceiver(player,4,70,
                            p=>p.slot==3,
                            p=>NearestOpponentDistance(p)-Vector2.Distance(player.Position,p.Position)*.05f);
                        if(outlet!=null&&NearestOpponentDistance(player)<7)RequestPass(player,outlet);
                    }
                    break;
            }
        }
        void AssignDefensivePair(FivePlayer first,FivePlayer second,Vector2 a,Vector2 b)
        {
            var direct=(first.Position-a).sqrMagnitude+(second.Position-b).sqrMagnitude;
            var swapped=(first.Position-b).sqrMagnitude+(second.Position-a).sqrMagnitude;
            first.SetTarget(ClampField(swapped<direct?b:a),BallPosition);
            second.SetTarget(ClampField(swapped<direct?a:b),BallPosition);
        }
        Vector2 ChooseShotTarget(FivePlayer player)
        {
            var sign=AttackSign(player.team);
            var best=new Vector2(sign*(geometry.HalfLength+1),0);
            float bestScore=float.NegativeInfinity;
            foreach(float lane in new[]{-.8f,-.4f,0,.4f,.8f})
            {
                var target=new Vector2(sign*(geometry.HalfLength+1),geometry.GoalHalfWidth*lane);
                if(!LaneOpen(BallPosition,target,player.team,.1f))continue;
                var direction=target-BallPosition;
                float preparation=Vector2.Angle(player.Forward,direction)/player.MaximumTurnRate(profile);
                float clearance=float.PositiveInfinity;
                foreach(var opponent in players.Where(p=>p.team!=player.team))
                {
                    var along=Mathf.Clamp01(Vector2.Dot(opponent.Position-BallPosition,direction)/direction.sqrMagnitude);
                    var arrival=direction.magnitude*along/profile.maximumBallSpeed;
                    var predicted=opponent.Position+opponent.Velocity*Mathf.Min(preparation+arrival,.6f);
                    clearance=Mathf.Min(clearance,DistanceToSegment(predicted,BallPosition,target)-opponent.radius-ballRadius);
                }
                // All field players use visible positions and movement; no command-dependent shooting bonus.
                var score=clearance-preparation*2;
                if(score>bestScore){bestScore=score;best=target;}
            }
            return best;
        }
        Vector2 ChooseCutbackPoint(FivePlayer receiver,float side)
        {
            var best=World(new Vector2(geometry.HalfLength-20,-side*8),receiver.team);
            float bestScore=float.NegativeInfinity;
            var goalkeeper=Player(Other(receiver.team),0);
            var goal=new Vector2(AttackSign(receiver.team)*(geometry.HalfLength+1),goalkeeper.Position.y>=0?-geometry.GoalHalfWidth*.66f:geometry.GoalHalfWidth*.66f);
            foreach(var depth in new[]{8f,13f,18f})foreach(var lateral in new[]{-side*8,0,side*8})
            {
                var point=ClampField(World(new Vector2(geometry.HalfLength-depth,lateral),receiver.team));
                var space=players.Where(p=>p.team!=receiver.team).Min(p=>Vector2.Distance(p.Position,point));
                var score=(PassingLaneOpen(BallPosition,point,receiver.team,receiver.Index)?20:0)+(LaneOpen(point,goal,receiver.team,.1f)?12:0)+Mathf.Min(space,8)-Vector2.Distance(receiver.Position,point)*.25f;
                if(score>bestScore){bestScore=score;best=point;}
            }
            return best;
        }
        FivePlayer FindKeeperDistribution(FivePlayer keeper,FiveCommand command)
        {
            var x=Local(keeper.Position,keeper.team).x;
            switch(command)
            {
                case FiveCommand.FastTransition:
                    return FindReceiver(keeper,4,35,p=>Local(p.Position,keeper.team).x>x+4,
                        p=>Local(p.Position,keeper.team).x-Vector2.Distance(p.Position,keeper.Position)*.2f);
                case FiveCommand.LongPass:
                    return FindReceiver(keeper,22,85,p=>Local(p.Position,keeper.team).x>x+16,
                        p=>Local(p.Position,keeper.team).x+NearestOpponentDistance(p)*.3f);
                case FiveCommand.WideCirculation:
                    return FindReceiver(keeper,6,55,p=>Mathf.Abs(p.Position.y-keeper.Position.y)>12,
                        p=>Local(p.Position,keeper.team).x-x-Vector2.Distance(p.Position,keeper.Position)*.2f);
                case FiveCommand.WingCutback:
                    return FindReceiver(keeper,6,70,p=>Mathf.Abs(p.Position.y)>12,
                        p=>NearestOpponentDistance(p)-Vector2.Distance(p.Position,keeper.Position)*.15f);
                case FiveCommand.GoalBlock:
                    return FindReceiver(keeper,4,85,p=>p.slot==3,p=>NearestOpponentDistance(p));
                default:
                    return FindReceiver(keeper,4,70,p=>true,p=>NearestOpponentDistance(p)-Vector2.Distance(p.Position,keeper.Position)*.15f);
            }
        }
        FivePlayer FindReceiver(FivePlayer sender,float minimum,float maximum,Func<FivePlayer,bool> predicate,Func<FivePlayer,float> score,bool includeKeeper=false)
        {
            FivePlayer best=null;float bestScore=float.NegativeInfinity;
            foreach(var player in players)
            {
                if(player==sender||(!includeKeeper&&player.IsKeeper)||player.team!=sender.team||!predicate(player))continue;
                var distance=Vector2.Distance(sender.Position,player.Position);
                if(distance<minimum||distance>maximum||!PassingLaneOpen(BallPosition,player.Position,sender.team,player.Index))continue;
                var value=score(player);if(value>bestScore){bestScore=value;best=player;}
            }
            return best;
        }
        bool PassingLaneOpen(Vector2 start,Vector2 end,Team team,int receiverIndex)
        {
            if(!LaneOpen(start,end,team))return false;
            foreach(var teammate in players)
                if(teammate.team==team&&teammate.Index!=receiverIndex&&Vector2.Distance(teammate.Position,start)>2
                    &&DistanceToSegment(teammate.Position,start,end)<teammate.radius+ballRadius+.3f)return false;
            var path=end-start;var distance=path.magnitude;
            if(distance<.01f)return false;
            var direction=path/distance;
            var speed=Mathf.Clamp(distance/1.7f+profile.EffectiveFreeBallDamping*distance,12,profile.maximumBallSpeed);
            foreach(var opponent in players)
            {
                if(opponent.team==team)continue;
                var along=Mathf.Clamp(Vector2.Dot(opponent.Position-start,direction),0,distance);
                var arrival=profile.EffectiveFreeBallDamping>.001f?
                    -Mathf.Log(Mathf.Max(.01f,1-profile.EffectiveFreeBallDamping*along/speed))/profile.EffectiveFreeBallDamping:along/speed;
                // Estimate only from visible current movement, without reading the opponent's command or pass reservation.
                var predicted=opponent.Position+opponent.Velocity*Mathf.Min(arrival,1.2f);
                var reaction=Mathf.Min(arrival,.5f);
                var changeOfDirection=Mathf.Min(2,.5f*profile.acceleration*reaction*reaction);
                if(DistanceToSegment(predicted,start,end)<opponent.radius+ballRadius+.5f+changeOfDirection)return false;
            }
            return true;
        }
    }
}
