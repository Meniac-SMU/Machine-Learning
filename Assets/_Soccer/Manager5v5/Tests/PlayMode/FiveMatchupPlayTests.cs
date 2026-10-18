using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveMatchupPlayTests
    {
        [UnityTearDown] public IEnumerator CleanupOutstandingWorlds()
        {FiveTestWorld.DisposeOutstanding();yield return null;}
        [Serializable] public sealed class Frame {public float time;public int owner,receiver;public string phase;public Vector2 ball,velocity,receiverPosition;public Vector2[] players,targets;}
        [Serializable] public sealed class Trial
        {
            public string scenario,phase,continuation;
            public int candidate,opponent,seed,mirror,ownGoals,enemyGoals,ownPasses,ownReceptions,enemyPasses,enemyReceptions,ownShots,enemyShots,corrections,ruleChoice;
            public string ruleReason;
            public float ownPossession,enemyPossession,maxOwnAdvance,maxEnemyAdvance,minimumMeanStamina,firstSecureAt=-1,elapsed;
            public float earlyMaxOwnAdvance=-62,earlyPossession,firstAdvance25At=-1,initialOwnBallX;
            public float ownPassFlight,enemyPassFlight;
            public float safeExitAt=-1;
            public Vector2[] start;
            public List<FiveEvent> events;
            public List<Frame> frames;
        }
        [Serializable] public sealed class Report
        {
            public string verdict="Diagnostic collection only; not a balance pass";
            public bool trainingStarted=false;
            public List<Trial> trials=new List<Trial>();
        }
        static readonly string[] Names={"central-opening","rear-pressure-far-receiver","wide-escape","deep-goal-block","wide-trap-window","distant-receiver-denial","nearby-loose-ball","central-goal-threat","central-pressure-open-wide-lanes","approach-to-low-block","cutback-interception-window","split-distant-receivers","off-lane-distant-receiver-coverage","wide-transition-to-attacking-half","wide-direct-exit","prepared-pass-goal-threat"};
        static readonly FiveCommand[] Opponents={FiveCommand.MarkReceivers,FiveCommand.AllRecover,FiveCommand.AllRecover,FiveCommand.GoalBlock,FiveCommand.WideCirculation,FiveCommand.LongPass,FiveCommand.FastTransition,FiveCommand.FastTransition,FiveCommand.AllRecover,FiveCommand.GoalBlock,FiveCommand.WingCutback,FiveCommand.LongPass,FiveCommand.LongPass,FiveCommand.AllRecover,FiveCommand.LongPass,FiveCommand.LongPass};

        public static Team Arrange(FiveTestWorld world,int scenario,int seed,bool mirror,float jitter=.6f)
        {
            var m=world.match;var points=new Vector2[10];
            points[0]=new Vector2(-59,0);points[5]=new Vector2(59,0);
            Vector2[] red,navy;int carrier=1;
            switch(scenario)
            {
                case 0:
                    red=new[]{new Vector2(-18,0),new Vector2(-28,10),new Vector2(4,-14),new Vector2(6,14)};
                    navy=new[]{new Vector2(0,22),new Vector2(0,-24),new Vector2(14,28),new Vector2(18,-28)};break;
                case 1:
                    red=new[]{new Vector2(-25,-8),new Vector2(-38,12),new Vector2(10,-8),new Vector2(8,22)};
                    navy=new[]{new Vector2(-31,-8),new Vector2(-28,0),new Vector2(-26,-18),new Vector2(-18,12)};break;
                case 2:
                    red=new[]{new Vector2(-15,0),new Vector2(-23,24),new Vector2(4,-24),new Vector2(3,24)};
                    navy=new[]{new Vector2(-9,0),new Vector2(-12,7),new Vector2(-12,-7),new Vector2(12,0)};break;
                case 3:
                    carrier=3;red=new[]{new Vector2(25,25),new Vector2(10,0),new Vector2(48,25),new Vector2(44,-4)};
                    navy=new[]{new Vector2(55,-6.5f),new Vector2(55,6.5f),new Vector2(5,-16),new Vector2(49,3)};break;
                case 4:
                    carrier=3;red=new[]{new Vector2(-12,16),new Vector2(-26,0),new Vector2(-5,24),new Vector2(15,-16)};
                    navy=new[]{new Vector2(1,26),new Vector2(4,16),new Vector2(7,32),new Vector2(21,0)};break;
                case 5:
                    red=new[]{new Vector2(-25,-8),new Vector2(-35,12),new Vector2(14,-8),new Vector2(8,18)};
                    navy=new[]{new Vector2(10,-8),new Vector2(5,18),new Vector2(-12,12),new Vector2(-8,-18)};break;
                case 6:
                    carrier=-1;red=new[]{new Vector2(-5,-5),new Vector2(-4,4),new Vector2(-7,-1),new Vector2(-8,7)};
                    navy=new[]{new Vector2(5,-4),new Vector2(6,4),new Vector2(12,0),new Vector2(10,10)};break;
                case 7:
                    carrier=3;red=new[]{new Vector2(20,-8),new Vector2(15,15),new Vector2(43,0),new Vector2(47,12)};
                    navy=new[]{new Vector2(52,-4),new Vector2(52,5),new Vector2(5,15),new Vector2(46,9)};break;
                case 8:
                    red=new[]{new Vector2(-15,0),new Vector2(-23,24),new Vector2(-5,-24),new Vector2(-4,24)};
                    navy=new[]{new Vector2(-5,0),new Vector2(-2,4),new Vector2(-2,-4),new Vector2(3,0)};break;
                case 9:
                    carrier=3;red=new[]{new Vector2(5,24),new Vector2(-2,0),new Vector2(20,18),new Vector2(18,-8)};
                    navy=new[]{new Vector2(55,-6.5f),new Vector2(55,6.5f),new Vector2(5,-16),new Vector2(49,3)};break;
                case 10:
                    carrier=4;red=new[]{new Vector2(29,27),new Vector2(15,0),new Vector2(43,-4),new Vector2(47,25)};
                    navy=new[]{new Vector2(50,30),new Vector2(52,18),new Vector2(43,19),new Vector2(55,0)};break;
                case 11:
                    red=new[]{new Vector2(-28,0),new Vector2(-40,0),new Vector2(28,-24),new Vector2(26,24)};
                    navy=new[]{new Vector2(35,-24),new Vector2(35,24),new Vector2(-8,10),new Vector2(-8,-10)};break;
                case 12:
                    red=new[]{new Vector2(-25,-8),new Vector2(-35,12),new Vector2(14,-8),new Vector2(8,18)};
                    navy=new[]{new Vector2(14,-18),new Vector2(8,30),new Vector2(0,30),new Vector2(0,-30)};break;
                case 13:
                    red=new[]{new Vector2(-10,0),new Vector2(-20,24),new Vector2(10,-24),new Vector2(12,24)};
                    navy=new[]{new Vector2(5,0),new Vector2(7,3),new Vector2(7,-3),new Vector2(12,0)};break;
                case 14:
                    carrier=3;red=new[]{new Vector2(-12,16),new Vector2(-26,0),new Vector2(-5,24),new Vector2(32,-16)};
                    navy=new[]{new Vector2(1,26),new Vector2(4,16),new Vector2(7,32),new Vector2(27,-8)};break;
                default:
                    red=new[]{new Vector2(18,0),new Vector2(-5,18),new Vector2(50,-12),new Vector2(44,13)};
                    navy=new[]{new Vector2(53,-4),new Vector2(53,5),new Vector2(5,15),new Vector2(45,20)};break;
            }
            for(int i=0;i<4;i++){points[i+1]=red[i];points[i+6]=navy[i];}
            var random=new System.Random(seed+scenario*101);
            for(int i=0;i<10;i++)if(i%5!=0)points[i]+=new Vector2((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*jitter;
            var ball=carrier>=0?points[carrier]:Vector2.zero;
            for(int i=0;i<10;i++)
            {
                var index=mirror?(i+5)%10:i;var sign=mirror?-1:1;
                var face=i==carrier?Vector2.right:(ball-points[i]).normalized;
                world.Place(index,points[i]*sign,face*sign);
            }
            if(carrier>=0)
            {
                var index=mirror?(carrier+5)%10:carrier;world.Give(index);Assert.That(m.Owner,Is.EqualTo(index),"Scene must begin with the intended actual carrier.");
            }
            else {m.ball.position=m.ballSpawn;m.ball.linearVelocity=Vector3.zero;Physics.SyncTransforms();}
            var candidate=scenario==4||scenario==5||scenario==7||scenario==10||scenario==11||scenario==12||scenario==14||scenario==15?Team.Navy:Team.Red;
            return mirror?FiveMatch.Other(candidate):candidate;
        }
        internal static Trial Begin(FiveMatch m,Team own,int scenario,int command,int seed,bool mirror,bool continuation,string phase)
        {
            var observed=FiveRuleManager.Choose(FiveObservation.Capture(m,own),out var reason);
            return new Trial{scenario=Names[scenario],candidate=command,opponent=(int)Opponents[scenario],seed=seed,mirror=mirror?1:0,
                phase=phase,continuation=continuation?"rules-after-three-seconds":"fixed-twelve-seconds",ruleChoice=(int)observed,ruleReason=reason,
                maxOwnAdvance=-m.geometry.HalfLength,maxEnemyAdvance=-m.geometry.HalfLength,minimumMeanStamina=1,
                initialOwnBallX=m.BallPosition.x*FiveMatch.AttackSign(own),start=m.players.Select(p=>p.Position).ToArray()};
        }
        internal static void Sample(FiveMatch m,Team own,Trial trial,float start,ref float secure)
        {
            if(trial.frames!=null&&(trial.frames.Count==0||m.Elapsed-trial.frames.Last().time>=.1f))
                trial.frames.Add(new Frame{time=m.Elapsed,owner=m.Owner,receiver=m.PassReceiver,phase=m.PassPhase.ToString(),ball=m.BallPosition,velocity=m.BallVelocity,receiverPosition=m.PassReceiver>=0?m.players[m.PassReceiver].Position:Vector2.zero,players=m.players.Select(p=>p.Position).ToArray(),targets=m.players.Select(p=>p.Target).ToArray()});
            if(m.State!=FiveMatchState.Playing)return;
            var fields=m.players.Where(p=>p.team==own&&!p.IsKeeper).ToArray();
            trial.minimumMeanStamina=Mathf.Min(trial.minimumMeanStamina,fields.Average(p=>p.Stamina.Value));
            if(m.PassPhase==FivePassPhase.InFlight)
            {
                if(m.players[m.PassSender].team==own)trial.ownPassFlight+=.02f;
                else trial.enemyPassFlight+=.02f;
            }
            if(m.Carrier==null){secure=0;return;}
            if(m.Carrier.team==own)
            {
                trial.ownPossession+=.02f;secure+=.02f;
                trial.maxOwnAdvance=Mathf.Max(trial.maxOwnAdvance,m.BallPosition.x*FiveMatch.AttackSign(own));
                if(m.Elapsed-start<=3){trial.earlyPossession+=.02f;trial.earlyMaxOwnAdvance=Mathf.Max(trial.earlyMaxOwnAdvance,m.BallPosition.x*FiveMatch.AttackSign(own));}
                if(trial.firstAdvance25At<0&&m.BallPosition.x*FiveMatch.AttackSign(own)>=trial.initialOwnBallX+25)trial.firstAdvance25At=m.Elapsed-start;
                if(secure>=.5f&&trial.firstSecureAt<0)trial.firstSecureAt=m.Elapsed-start;
            }
            else
            {
                secure=0;trial.enemyPossession+=.02f;
                trial.maxEnemyAdvance=Mathf.Max(trial.maxEnemyAdvance,m.BallPosition.x*FiveMatch.AttackSign(FiveMatch.Other(own)));
            }
        }
        internal static void Finish(FiveMatch m,Team own,Trial trial,float start)
        {
            trial.elapsed=m.Elapsed-start;trial.ownGoals=own==Team.Red?m.RedScore:m.NavyScore;trial.enemyGoals=own==Team.Red?m.NavyScore:m.RedScore;
            Func<FiveEvent,bool> ours=e=>e.actor>=0&&e.actor<10&&m.players[e.actor].team==own;
            trial.ownPasses=m.Events.Count(e=>e.kind=="pass-kicked"&&ours(e));trial.ownReceptions=m.Events.Count(e=>e.kind=="pass-received"&&ours(e));
            trial.enemyPasses=m.Events.Count(e=>e.kind=="pass-kicked"&&!ours(e));trial.enemyReceptions=m.Events.Count(e=>e.kind=="pass-received"&&!ours(e));
            trial.ownShots=m.Events.Count(e=>e.kind=="shot"&&ours(e));trial.enemyShots=m.Events.Count(e=>e.kind=="shot"&&!ours(e));
            trial.corrections=m.BoundaryCorrections;trial.events=new List<FiveEvent>(m.Events);
        }
        static void Save(Report report,string suffix)=>File.WriteAllText(FiveP7Evidence.DirectoryPath+"/g5-matchups-"+suffix+".json",JsonUtility.ToJson(report,true));

        [UnityTest] public IEnumerator DevelopmentSameStateSubstitutions(){return Substitutions(2101,"development");}
        [UnityTest,Category("LongRunning"),Timeout(600000)] public IEnumerator RetentionFocusedDevelopment()
        {for(int seed=10001;seed<=10004;seed++)yield return FocusedSubstitutions(seed);}
        [UnityTest,Category("LongRunning"),Timeout(600000)] public IEnumerator RetentionFocusedConfirmation()
        {for(int seed=10011;seed<=10016;seed++)yield return FocusedSubstitutions(seed);}
        internal static Team ArrangeFocused(FiveTestWorld world,int scenario,int seed,bool mirror)
        {
            Vector2[] red,navy;int carrier;
            if(scenario==0)
            {
                carrier=1;
                red=new[]{new Vector2(-15,0),new Vector2(-24,16),new Vector2(-8,-18),new Vector2(-6,18)};
                navy=new[]{new Vector2(-8,0),new Vector2(-5,5),new Vector2(-5,-5),new Vector2(-3,14)};
            }
            else
            {
                carrier=3;
                red=new[]{new Vector2(-12,16),new Vector2(-26,0),new Vector2(-5,24),scenario==1?new Vector2(2,-12):new Vector2(32,-16)};
                navy=scenario==1?new[]{new Vector2(1,26),new Vector2(4,16),new Vector2(7,32),new Vector2(21,0)}
                    :new[]{new Vector2(3,25),new Vector2(5,12),new Vector2(30,-10),new Vector2(30,-25)};
            }
            var points=new Vector2[10];points[0]=new Vector2(-59,0);points[5]=new Vector2(59,0);
            var random=new System.Random(seed+scenario*101);
            for(int i=0;i<4;i++){points[i+1]=red[i];points[i+6]=navy[i];}
            for(int i=0;i<10;i++)if(i%5!=0)points[i]+=new Vector2((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*3;
            var ball=points[carrier];var sign=mirror?-1:1;
            for(int i=0;i<10;i++)world.Place(mirror?(i+5)%10:i,points[i]*sign,(i==carrier?Vector2.right:(ball-points[i]).normalized)*sign);
            world.Give(mirror?(carrier+5)%10:carrier);
            Assert.That(world.match.Owner,Is.EqualTo(mirror?(carrier+5)%10:carrier));
            var own=scenario==2?Team.Navy:Team.Red;
            return mirror?FiveMatch.Other(own):own;
        }
        IEnumerator FocusedSubstitutions(int seed)
        {
            var names=new[]{"short-wide-outlet","wide-carrier-switch","wide-trap-with-deep-outlet"};
            var opponents=new[]{FiveCommand.AllRecover,FiveCommand.WideTrap,FiveCommand.LongPass};
            var report=new Report();
            for(int scenario=0;scenario<3;scenario++)for(int command=0;command<8;command++)for(int mirrored=0;mirrored<2;mirrored++)for(int continuation=0;continuation<2;continuation++)
            {
                using(var world=new FiveTestWorld())
                {
                    var m=world.match;var own=ArrangeFocused(world,scenario,seed,mirrored==1);var enemy=FiveMatch.Other(own);
                    var row=Begin(m,own,0,command,seed,mirrored==1,continuation==1,"retention-focused");
                    row.scenario=names[scenario];row.opponent=(int)opponents[scenario];
                    if((seed==10001||seed==10011)&&mirrored==0&&continuation==0)row.frames=new List<Frame>();
                    float start=m.Elapsed,secure=0,safe=0;int steps=0;
                    m.SelectCommand(own,(FiveCommand)command,"focused-candidate",true);m.SelectCommand(enemy,opponents[scenario],"focused-opponent",true);
                    while(m.Elapsed-start<12&&steps++<2000)
                    {
                        if(continuation==1&&m.Elapsed-start>=3)m.ruleManagers[(int)own]=true;
                        world.Step(1);Sample(m,own,row,start,ref secure);
                        bool escaped=m.State==FiveMatchState.Playing&&m.Carrier!=null&&m.Carrier.team==own
                            &&m.BallPosition.x*FiveMatch.AttackSign(own)>=row.initialOwnBallX+4
                            &&m.players.Where(p=>p.team==enemy).Min(p=>Vector2.Distance(p.Position,m.Carrier.Position))>=6;
                        safe=escaped?safe+.02f:0;
                        if(row.safeExitAt<0&&safe>=.12f)row.safeExitAt=m.Elapsed-start;
                    }
                    Finish(m,own,row,start);report.trials.Add(row);
                }
                yield return null;
                if(command==7&&mirrored==1&&continuation==1)Save(report,"retention-focused-"+seed);
            }
            Assert.That(report.trials.Count,Is.EqualTo(96));
            Assert.That(report.trials.All(t=>t.elapsed>=12&&t.corrections==0),Is.True,"Physical collection integrity only; not retention approval.");
        }
        [UnityTest,Category("LongRunning"),Timeout(600000)] public IEnumerator RetentionScenarioAudit()
        {
            for(int seed=9801;seed<=9806;seed++)yield return Substitutions(seed,"retention-"+seed,3f);
        }
        [UnityTest] public IEnumerator DevelopmentReceiverCoverage(){return Substitutions(2101,"receiver-coverage",.6f,12);}
        [UnityTest] public IEnumerator DevelopmentMissionWindows(){return Substitutions(2101,"mission-windows",.6f,13);}
        [UnityTest,Category("LongRunning"),Timeout(600000)] public IEnumerator HoldoutSameStateSubstitutions()
        {
            for(int seed=9607;seed<=9610;seed++)yield return Substitutions(seed,"holdout-"+seed,1.8f);
        }
        [UnityTest,Category("LongRunning")] public IEnumerator DevelopmentPreviouslyFailedSeeds()
        {
            for(int seed=9107;seed<=9110;seed++)yield return Substitutions(seed,"refinement-"+seed,1.8f);
        }
        [UnityTest] public IEnumerator DiagnoseWidePass()
        {
            var report=new Report();
            using(var world=new FiveTestWorld())
            {
                var m=world.match;var own=Arrange(world,2,2101,false);var start=m.Elapsed;float secure=0;
                var row=Begin(m,own,2,2,2101,false,false,"diagnostic");row.frames=new List<Frame>();
                m.SelectCommand(own,FiveCommand.WideCirculation,"trace",true);m.SelectCommand(FiveMatch.Other(own),FiveCommand.AllRecover,"trace",true);
                world.Step(600,()=>Sample(m,own,row,start,ref secure));Finish(m,own,row,start);report.trials.Add(row);Save(report,"pass-trace");
            }
            yield return null;
        }
        [UnityTest] public IEnumerator DiagnoseGoalBlockShots()
        {
            var report=new Report();
            for(int seed=9109;seed<=9110;seed++)for(int mirror=0;mirror<2;mirror++)
            {
                using(var world=new FiveTestWorld())
                {
                    var m=world.match;var own=Arrange(world,7,seed,mirror==1,1.8f);var start=m.Elapsed;float secure=0;
                    var row=Begin(m,own,7,7,seed,mirror==1,false,"diagnostic-goal-block");row.frames=new List<Frame>();
                    m.SelectCommand(own,FiveCommand.GoalBlock,"goal-block-trace",true);m.SelectCommand(FiveMatch.Other(own),FiveCommand.FastTransition,"fixed-opponent",true);
                    while(m.Elapsed-start<3)world.Step(1,()=>Sample(m,own,row,start,ref secure));
                    Finish(m,own,row,start);report.trials.Add(row);
                }
                yield return null;
            }
            Save(report,"goal-block-trace");
        }
        IEnumerator Substitutions(int seed,string phase,float jitter=.6f,int firstScenario=0)
        {
            var report=new Report();
            for(int scenario=firstScenario;scenario<Names.Length;scenario++)
            {
                for(int command=0;command<8;command++)for(int mirrored=0;mirrored<2;mirrored++)for(int continuation=0;continuation<2;continuation++)
                {
                    using(var world=new FiveTestWorld())
                    {
                        var m=world.match;var own=Arrange(world,scenario,seed,mirrored==1,jitter);var enemy=FiveMatch.Other(own);
                        var row=Begin(m,own,scenario,command,seed,mirrored==1,continuation==1,phase);var start=m.Elapsed;float secure=0;
                        m.SelectCommand(own,(FiveCommand)command,"same-state-candidate",true);m.SelectCommand(enemy,Opponents[scenario],"fixed-opponent",true);
                        int steps=0;
                        while(m.Elapsed-start<12&&steps++<2000)
                        {
                            if(continuation==1&&m.Elapsed-start>=3)m.ruleManagers[(int)own]=true;
                            world.Step(1);Sample(m,own,row,start,ref secure);
                        }
                        Finish(m,own,row,start);report.trials.Add(row);
                    }
                    yield return null;
                }
                Save(report,phase);
            }
            Assert.That(report.trials.Count,Is.EqualTo((Names.Length-firstScenario)*32));
            Assert.That(report.trials.All(t=>t.elapsed>=12&&t.corrections==0),Is.True,"Collecting results is not a balance pass; any physical failure still fails this diagnostic.");
        }
        [UnityTest] public IEnumerator DevelopmentFixedCommandCrossGames()
        {
            var report=new Report();
            for(int red=0;red<8;red++)for(int navy=0;navy<8;navy++)
            {
                using(var world=new FiveTestWorld())
                {
                    var m=world.match;m.profile.duration=45;
                    m.SelectCommand(Team.Red,(FiveCommand)red,"fixed-cross-game",true);m.SelectCommand(Team.Navy,(FiveCommand)navy,"fixed-cross-game",true);
                    var row=new Trial{scenario="neutral-kickoff-cross-game",phase="development",continuation="fixed-forty-five-seconds",candidate=red,opponent=navy,seed=2101,
                        maxOwnAdvance=-62,maxEnemyAdvance=-62,minimumMeanStamina=1,start=m.players.Select(p=>p.Position).ToArray()};
                    float secure=0;int steps=0;
                    while(m.State!=FiveMatchState.Finished&&steps++<4000){world.Step(1);Sample(m,Team.Red,row,0,ref secure);}
                    Finish(m,Team.Red,row,0);report.trials.Add(row);
                }
                yield return null;if(navy==7)Save(report,"cross-games");
            }
            Assert.That(report.trials.All(t=>t.elapsed>=45&&t.corrections==0),Is.True);
        }
    }
}
