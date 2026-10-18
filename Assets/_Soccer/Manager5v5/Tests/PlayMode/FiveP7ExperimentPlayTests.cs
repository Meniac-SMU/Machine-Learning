using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveP7ExperimentPlayTests
    {
        [UnityTearDown] public IEnumerator Cleanup(){FiveTestWorld.DisposeOutstanding();yield return null;}
        static readonly FiveCommand[] RoleOpponents={FiveCommand.MarkReceivers,FiveCommand.AllRecover,FiveCommand.AllRecover,FiveCommand.GoalBlock,
            FiveCommand.WideCirculation,FiveCommand.LongPass,FiveCommand.FastTransition,FiveCommand.FastTransition,
            FiveCommand.AllRecover,FiveCommand.WideTrap,FiveCommand.LongPass};
        static readonly string[] FocusNames={"short-wide-outlet","wide-carrier-switch","wide-trap-with-deep-outlet"};
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator RolesDevelopment()
        {for(int seed=11001;seed<=11002;seed++)yield return Roles(seed,"development");}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator RolesConfirmation()
        {for(int seed=11101;seed<=11104;seed++)yield return Roles(seed,"confirmation");}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator RolesFinalConfirmation()
        {for(int seed=11111;seed<=11114;seed++)yield return Roles(seed,"final-confirmation");}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator WideTrapRoleConfirmation()
        {for(int seed=11121;seed<=11128;seed++)yield return Roles(seed,"wide-role-confirmation",new[]{4,10});}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator WideTrapSupportDevelopment()
        {for(int seed=11121;seed<=11128;seed++)yield return Roles(seed,"wide-support-development",new[]{4,10});}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator WideCutbackDevelopment()
        {for(int seed=11121;seed<=11128;seed++)yield return Roles(seed,"wide-cutback-development",new[]{4},FiveCommand.WingCutback);}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator RecoveryCompletionDevelopment()
        {for(int seed=11121;seed<=11128;seed++)yield return Roles(seed,"recovery-completion-development",new[]{4,10});}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator EconomicalWideRecoveryConfirmation()
        {
            for(int seed=11141;seed<=11148;seed++)foreach(float energy in new[]{1f,.35f})
                yield return Roles(seed,energy<1?"economical-tired":"economical-fresh",new[]{4},null,energy);
        }

        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator ContainmentRolesDevelopment()
        {for(int seed=11111;seed<=11114;seed++)yield return Roles(seed,"containment-development",Enumerable.Range(0,12).ToArray());}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator ContainmentRolesConfirmation()
        {for(int seed=11601;seed<=11608;seed++)yield return Roles(seed,"containment-confirmation",Enumerable.Range(0,12).ToArray());}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator ContainmentRecoveryDevelopment()
        {for(int seed=11111;seed<=11114;seed++)yield return Roles(seed,"containment-recovery-development",new[]{6,11});}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator ContainmentDefenceDevelopment()
        {for(int seed=11111;seed<=11114;seed++)yield return Roles(seed,"containment-defence-development",new[]{6,7,11});}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator CutbackSpaceDevelopment()
        {for(int seed=11601;seed<=11608;seed++)yield return Roles(seed,"cutback-space-development",new[]{3});}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator FinalRolesConfirmation()
        {for(int seed=11801;seed<=11808;seed++)yield return Roles(seed,"final-confirmation",Enumerable.Range(0,12).ToArray());}

        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator DefenceBoundaryDevelopment()
        {for(int seed=11801;seed<=11808;seed++)yield return Roles(seed,"defence-boundary-development",new[]{3,5,7});}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator BoundaryRolesConfirmation()
        {for(int seed=11901;seed<=11908;seed++)yield return Roles(seed,"boundary-confirmation",Enumerable.Range(0,12).ToArray());}

        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator ApproachingGoalDevelopment()
        {for(int seed=11801;seed<=11808;seed++)yield return Roles(seed,"approaching-goal-development",new[]{3,5,7},null,1,true);}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator ApproachingRolesConfirmation()
        {for(int seed=11901;seed<=11908;seed++)yield return Roles(seed,"approaching-confirmation",Enumerable.Range(0,12).ToArray(),null,1,true);}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator RoleBoundaryDevelopment()
        {for(int seed=11901;seed<=11908;seed++)yield return Roles(seed,"role-boundary-development",new[]{0,1,3,6},null,1,true);}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator RoleBoundaryConfirmation()
        {for(int seed=12001;seed<=12008;seed++)yield return Roles(seed,"role-boundary-confirmation",Enumerable.Range(0,12).ToArray(),null,1,true);}
        static Team ArrangeApproachingGoal(FiveTestWorld world,int seed,bool mirror)
        {
            var own=FiveMatchupPlayTests.Arrange(world,7,seed,mirror,3f);
            var attack=FiveMatch.Other(own);var index=(int)attack*5+3;var carrier=world.match.players[index];
            var forward=new Vector2(FiveMatch.AttackSign(attack),0);
            // The initial carrier is outside shooting range, so each candidate has actual time to form its response.
            world.Place(index,carrier.Position-forward*18,forward);world.Give(index);
            return own;
        }

        static IEnumerator Roles(int seed,string phase,int[] scenarios=null,FiveCommand? opponentOverride=null,float initialEnergy=1,bool approachingGoal=false)
        {
            var report=new FiveMatchupPlayTests.Report();
            var selectedScenarios=scenarios??Enumerable.Range(0,11).ToArray();
            foreach(int scenario in selectedScenarios)for(int command=0;command<8;command++)for(int mirror=0;mirror<2;mirror++)for(int continuation=0;continuation<2;continuation++)
            {
                using(var world=new FiveTestWorld())
                {
                    var m=world.match;
                    var own=approachingGoal&&scenario==7?ArrangeApproachingGoal(world,seed,mirror==1):scenario==11?FiveInterceptionPrototypePlayTests.ArrangeRunning(world,seed,mirror==1):scenario<8?FiveMatchupPlayTests.Arrange(world,scenario,seed,mirror==1,3f):FiveMatchupPlayTests.ArrangeFocused(world,scenario-8,seed,mirror==1);
                    if(initialEnergy<1)foreach(var p in m.players.Where(p=>!p.IsKeeper))p.Stamina.SetForScenario(initialEnergy);
                    var row=FiveMatchupPlayTests.Begin(m,own,Mathf.Min(scenario,7),command,seed,mirror==1,continuation==1,phase);
                    if(approachingGoal&&scenario==7)row.scenario="central-approaching-threat";
                    if(scenario>=8)row.scenario=scenario==11?"central-running-threat":FocusNames[scenario-8];var opponent=opponentOverride??(scenario==11?FiveCommand.FastTransition:RoleOpponents[scenario]);row.opponent=(int)opponent;
                    if(seed%100==1&&mirror==0&&continuation==0&&command==(scenario<8?scenario:scenario==9?2:4))row.frames=new List<FiveMatchupPlayTests.Frame>();
                    m.SelectCommand(own,(FiveCommand)command,"p7-role-candidate",true);
                    m.SelectCommand(FiveMatch.Other(own),opponent,"p7-role-opponent",true);
                    float start=m.Elapsed,secure=0,safe=0;int steps=0;
                    while(m.Elapsed-start<12&&steps++<2000)
                    {
                        if(continuation==1&&m.Elapsed-start>=3)m.ruleManagers[(int)own]=true;
                        world.Step(1);FiveMatchupPlayTests.Sample(m,own,row,start,ref secure);
                        var escaped=m.State==FiveMatchState.Playing&&m.Carrier!=null&&m.Carrier.team==own
                            &&m.BallPosition.x*FiveMatch.AttackSign(own)>=row.initialOwnBallX+4
                            &&m.players.Where(p=>p.team!=own).Min(p=>Vector2.Distance(p.Position,m.Carrier.Position))>=6;
                        safe=escaped?safe+.02f:0;if(row.safeExitAt<0&&safe>=.12f)row.safeExitAt=m.Elapsed-start;
                    }
                    FiveMatchupPlayTests.Finish(m,own,row,start);report.trials.Add(row);
                }
                yield return null;
                if(command==7&&mirror==1&&continuation==1)FiveP7Evidence.Save($"roles-{phase}-{seed}",report);
            }
            Assert.That(report.trials,Has.Count.EqualTo(selectedScenarios.Length*32));
            Assert.That(report.trials.All(r=>r.elapsed>=12&&r.corrections==0),Is.True,"Physical integrity; tactical assessment is separate.");
        }

        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator MatrixDevelopment()
        {for(int seed=11201;seed<=11202;seed++)yield return Matrix(seed,"development");}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator MatrixConfirmation()
        {for(int seed=11301;seed<=11302;seed++)yield return Matrix(seed,"confirmation");}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator ContainmentMatrixConfirmation()
        {for(int seed=11711;seed<=11712;seed++)yield return Matrix(seed,"containment-confirmation",true);}
        static IEnumerator Matrix(int seed,string phase,bool includeRunning=false)
        {
            var report=new FiveMatchupPlayTests.Report();
            foreach(int scenario in (includeRunning?new[]{0,1,3,4,5,6,7,9,11}:new[]{0,3,4,5}))foreach(float energy in new[]{1f,.35f})for(int command=0;command<8;command++)for(int opponent=0;opponent<8;opponent++)for(int mirror=0;mirror<2;mirror++)
            {
                using(var world=new FiveTestWorld())
                {
                    var m=world.match;var own=scenario==9?FiveMatchupPlayTests.ArrangeFocused(world,1,seed,mirror==1):scenario==7?ArrangeApproachingGoal(world,seed,mirror==1):scenario==11?FiveInterceptionPrototypePlayTests.ArrangeRunning(world,seed,mirror==1):FiveMatchupPlayTests.Arrange(world,scenario,seed,mirror==1,3f);
                    foreach(var p in m.players.Where(p=>!p.IsKeeper))p.Stamina.SetForScenario(energy);
                    var row=FiveMatchupPlayTests.Begin(m,own,Mathf.Min(scenario,7),command,seed,mirror==1,false,phase+"-energy-"+(energy==1?"100":"35"));
                    if(scenario==11)row.scenario="central-running-threat";
                    if(scenario==7)row.scenario="central-approaching-threat";
                    if(scenario==9)row.scenario="wide-carrier-switch";
                    row.opponent=opponent;row.minimumMeanStamina=energy;
                    m.SelectCommand(own,(FiveCommand)command,"p7-cross-candidate",true);
                    m.SelectCommand(FiveMatch.Other(own),(FiveCommand)opponent,"p7-cross-opponent",true);
                    float start=m.Elapsed,secure=0,safe=0;int steps=0;
                    while(m.Elapsed-start<12&&steps++<2000)
                    {
                        world.Step(1);FiveMatchupPlayTests.Sample(m,own,row,start,ref secure);
                        var escaped=m.State==FiveMatchState.Playing&&m.Carrier!=null&&m.Carrier.team==own
                            &&m.BallPosition.x*FiveMatch.AttackSign(own)>=row.initialOwnBallX+4
                            &&m.players.Where(p=>p.team!=own).Min(p=>Vector2.Distance(p.Position,m.Carrier.Position))>=6;
                        safe=escaped?safe+.02f:0;if(row.safeExitAt<0&&safe>=.12f)row.safeExitAt=m.Elapsed-start;
                    }
                    FiveMatchupPlayTests.Finish(m,own,row,start);report.trials.Add(row);
                }
                yield return null;
                if(command==7&&opponent==7&&mirror==1)FiveP7Evidence.Save($"matrix-{phase}-{seed}",report);
            }
            Assert.That(report.trials,Has.Count.EqualTo(includeRunning?2304:1024));
            Assert.That(report.trials.All(r=>r.elapsed>=12&&r.corrections==0),Is.True);
        }

        [Serializable] public sealed class DecisionSample
        {
            public float time,remaining,energy;public int team,command,opportunities,scoreLead;public string reason;
            public Vector2 ball,carrierVelocity;public bool ownPossession,enemyPossession;
            public Vector2[] ownPositions,opponentPositions;public float[] ownStamina;
        }
        [Serializable] public sealed class Game
        {
            public int seed,mirror,candidateTeam,fixedOpponent=-1,redScore,navyScore,passes,receptions,shots,recoveries,corrections;
            public float elapsed,duration,decisionPeriod,maximumStall;public bool finished;
            public List<FiveEvent> events;public List<DecisionSample> decisions=new List<DecisionSample>();
        }
        [Serializable] public sealed class GameReport {public string[] commandNames=FiveHud.CommandNames;public List<Game> matches=new List<Game>();}

        // Overlapping observable situation indicators, not an action mask or the selector's chosen branch.
        static int Opportunities(FiveObservation s)
        {
            var fields=s.own.Where(p=>!p.keeper).ToArray();int bits=0;
            var near=fields.Count(p=>Vector2.Distance(p.position,s.ball)<9);
            Func<FiveVisiblePlayer,bool> open=p=>s.opponents.All(e=>FiveMatch.DistanceToSegment(e.position,s.ball,p.position)>e.radius+s.ballRadius+.5f);
            if(s.ownPossession)
            {
                if(!s.opponents.Any(p=>p.position.x>s.ball.x&&p.position.x<s.ball.x+15&&Mathf.Abs(p.position.y-s.ball.y)<4))bits|=1;
                if(fields.Any(p=>p.position.x>s.ball.x+18&&Vector2.Distance(p.position,s.ball)>22&&open(p)))bits|=2;
                if(fields.Any(p=>Mathf.Abs(p.position.y-s.ball.y)>12&&Mathf.Abs(p.position.x-s.ball.x)<24&&open(p)))bits|=4;
                if(s.ball.x>0&&s.opponents.Count(p=>!p.keeper&&p.position.x>s.halfLength-24&&Mathf.Abs(p.position.y)<16)>=3)bits|=8;
            }
            else if(!s.ownPassInFlight)
            {
                if(s.enemyPossession&&((Mathf.Abs(s.ball.y)>s.halfWidth*.36f&&fields.Count(p=>Vector2.Distance(p.position,s.ball)<23)>=2)
                    ||(s.carrierVelocity.x<-3&&fields.Count(p=>p.position.x<s.ball.x-3&&Vector2.Distance(p.position,s.ball)<35)>=2)))bits|=16;
                if(s.enemyPossession&&s.opponents.Count(p=>!p.keeper&&p.position.x<s.ball.x-8)>=1)bits|=32;
                if(near>=2||(!s.enemyPossession&&s.ball.x<-s.halfLength+15))bits|=64;
                if(s.ball.x<-s.halfLength+23||(s.ball.x<0&&(fields.Average(p=>p.stamina)<.35f||s.carrierVelocity.x<-3)))bits|=128;
            }
            return bits;
        }
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator NaturalSixtyFourGames()
        {yield return Games(13001,32,300,3,"natural");}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator MatchedFortyEightPeriodGames()
        {foreach(float period in new[]{2f,3f,4f})yield return Games(13201,8,300,period,"period-"+period);}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator ThirtyTwoRepeatedOpponentGames()
        {yield return Repeated(13401,"repeat");}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator ThirtyTwoRepeatedOpponentConfirmation()
        {yield return Repeated(13501,"repeat-confirmation");}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator ContainmentNaturalConfirmation()
        {yield return Games(14101,32,300,3,"natural-final");}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator ContainmentPeriodConfirmation()
        {foreach(float period in new[]{2f,3f,4f})yield return Games(14201,8,300,period,"period-final-"+period);}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator ContainmentRepeatConfirmation()
        {yield return Repeated(14301,"repeat-final");}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator StationaryCarrierRepeatRegression()
        {yield return Games(14302,1,120,3,"stationary-carrier-regression",7);}
        [UnityTest,Category("LongRunning"),Timeout(1800000)] public IEnumerator StationaryCarrierRepeatConfirmation()
        {yield return Repeated(14401,"repeat-stationary-final");}
        static IEnumerator Repeated(int firstSeed,string prefix)
        {
            var report=new GameReport();
            for(int opponent=0;opponent<8;opponent++)
            {
                var mode=prefix+"-"+opponent;
                yield return Games(firstSeed,2,120,3,mode,opponent,false);
                var path=System.IO.Path.Combine(FiveP7Evidence.DirectoryPath,"games-"+mode+".json");
                report.matches.AddRange(JsonUtility.FromJson<GameReport>(System.IO.File.ReadAllText(path)).matches);
            }
            AssertGames(report,32);
        }

        static IEnumerator Games(int firstSeed,int pairs,float duration,float period,string mode,int opponent=-1,bool validate=true)
        {
            var report=new GameReport();
            for(int run=0;run<pairs*2;run++)
            {
                using(var world=new FiveTestWorld())
                {
                    var m=world.match;var mirror=run%2;var own=mirror==0?Team.Red:Team.Navy;
                    m.profile.duration=duration;m.profile.decisionSeconds=period;m.ruleManagers=new[]{true,true};
                    var seed=firstSeed+run/2;var random=new System.Random(seed);var spawns=m.players.Select(p=>p.spawn).ToArray();
                    var offsets=Enumerable.Range(0,10).Select(_=>new Vector2((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*1.6f).ToArray();
                    for(int i=0;i<10;i++)
                    {
                        var source=mirror==0?i:(i+5)%10;var sign=mirror==0?1:-1;
                        world.Place(i,(new Vector2(spawns[source].x,spawns[source].z)+offsets[source])*sign,new Vector2(FiveMatch.AttackSign(m.players[i].team),0));
                        m.players[i].spawn=m.players[i].Body.position;
                    }
                    if(opponent>=0){m.ruleManagers[(int)FiveMatch.Other(own)]=false;m.SelectCommand(FiveMatch.Other(own),(FiveCommand)opponent,"p7-fixed-repeat",true);}
                    var game=new Game{seed=seed,mirror=mirror,candidateTeam=(int)own,fixedOpponent=opponent,duration=duration,decisionPeriod=period};
                    m.RuleDecisionMade+=(team,seen,command,reason)=>game.decisions.Add(new DecisionSample{time=m.Elapsed,remaining=seen.remaining,
                        energy=seen.own.Where(p=>!p.keeper).Average(p=>p.stamina),team=(int)team,command=(int)command,scoreLead=seen.scoreLead,reason=reason,opportunities=Opportunities(seen),
                        ball=seen.ball,carrierVelocity=seen.carrierVelocity,ownPossession=seen.ownPossession,enemyPossession=seen.enemyPossession,
                        ownPositions=seen.own.Select(p=>p.position).ToArray(),opponentPositions=seen.opponents.Select(p=>p.position).ToArray(),ownStamina=seen.own.Select(p=>p.stamina).ToArray()});
                    int steps=0;
                    while(m.State!=FiveMatchState.Finished&&steps<24000)
                    {
                        world.Step(50,()=>game.maximumStall=Mathf.Max(game.maximumStall,m.ObservedStallSeconds));steps+=50;
                        if(steps%1000==0)yield return null;
                    }
                    game.finished=m.State==FiveMatchState.Finished;game.elapsed=m.Elapsed;game.redScore=m.RedScore;game.navyScore=m.NavyScore;
                    game.passes=m.PassAttempts;game.receptions=m.PassReceptions;game.shots=m.Shots;game.corrections=m.BoundaryCorrections;
                    game.recoveries=m.Events.Count(e=>e.kind=="possession");game.events=new List<FiveEvent>(m.Events);report.matches.Add(game);
                }
                FiveP7Evidence.Save("games-"+mode,report);yield return null;
            }
            if(validate)AssertGames(report,pairs*2);
        }
        static void AssertGames(GameReport report,int expected)
        {
            Assert.That(report.matches,Has.Count.EqualTo(expected));
            foreach(var game in report.matches)
            {
                Assert.That(game.finished&&game.elapsed>=game.duration,Is.True);
                Assert.That(game.corrections,Is.Zero,$"seed={game.seed} mirror={game.mirror} period={game.decisionPeriod}");
                Assert.That(game.maximumStall,Is.LessThan(30),$"seed={game.seed} mirror={game.mirror} opponent={game.fixedOpponent} period={game.decisionPeriod}");
                // Deliberately fixed defensive commands may not create a shot/pass on their own.
                if(game.fixedOpponent<0){Assert.That(game.passes,Is.GreaterThan(0));Assert.That(game.shots,Is.GreaterThan(0));Assert.That(game.recoveries,Is.GreaterThan(1));}
            }
        }
    }
}
