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
    public sealed class FiveCounterTimingPlayTests
    {
        [Serializable] sealed class Choice {public int situation,mirror,selected;public string reason;}
        [Serializable] sealed class Timing {public float period,visibleAt,selectedAt,delay;}
        [Serializable] sealed class CutbackFrame
        {
            public float time;public int owner,phase;public Vector2 ball,velocity;
            public Vector2[] positions,velocities,forwards,targets;
        }
        [Serializable] sealed class Match {public int fixedOpponent,redScore,navyScore,passes,shots,corrections;public float period,elapsed;public int[] choices;public List<FiveEvent> events;}
        [Serializable] sealed class Report {public List<Choice> situations=new List<Choice>();public List<Timing> timing=new List<Timing>();public List<Match> matches=new List<Match>();}
        [UnityTest] public IEnumerator EightVisibleSituationsAndCounterDecisionPeriods()
        {
            var report=new Report();
            for(int scenario=0;scenario<8;scenario++)for(int mirror=0;mirror<2;mirror++)
            {
                using(var world=new FiveTestWorld())
                {
                    var team=FiveMatchupPlayTests.Arrange(world,scenario,2101,mirror==1);
                    var selected=FiveRuleManager.Choose(FiveObservation.Capture(world.match,team),out var reason);
                    report.situations.Add(new Choice{situation=scenario,mirror=mirror,selected=(int)selected,reason=reason});
                    Assert.That((int)selected,Is.EqualTo(scenario),"Visible reference situation "+scenario);
                }
                yield return null;
            }
            foreach(var period in new[]{2f,3f,4f})
            {
                using(var world=new FiveTestWorld())
                {
                    var m=world.match;m.profile.decisionSeconds=period;m.tacticsEnabled=false;
                    m.SelectCommand(Team.Navy,FiveCommand.MarkReceivers,"before-visible-change",true);world.Step(45);
                    FiveMatchupPlayTests.Arrange(world,7,2101,false);
                    var visibleAt=m.Elapsed;var due=m.Decision(Team.Navy).nextDecisionAt;
                    m.teamPlanningEnabled=new[]{false,false};m.ruleManagers=new[]{false,true};m.tacticsEnabled=true;
                    while(m.Elapsed+.021f<due){world.Step(1);Assert.That(m.Decision(Team.Navy).command,Is.EqualTo(FiveCommand.MarkReceivers));}
                    for(int step=0;step<3&&m.Decision(Team.Navy).command!=FiveCommand.GoalBlock;step++)world.Step(1);
                    Assert.That(m.Decision(Team.Navy).command,Is.EqualTo(FiveCommand.GoalBlock));
                    var selectedAt=m.Decision(Team.Navy).lastDecisionAt;
                    Assert.That(selectedAt,Is.InRange(due-.001f,due+.041f));
                    report.timing.Add(new Timing{period=period,visibleAt=visibleAt,selectedAt=selectedAt,delay=selectedAt-visibleAt});
                }
                yield return null;
            }
            File.WriteAllText(FiveP7Evidence.DirectoryPath+"/g5-rule-scenarios-timing.json",JsonUtility.ToJson(report,true));
        }
        [UnityTest] public IEnumerator RuleManagerAgainstRepeatedStrategies()
        {
            var report=new Report();
            foreach(var opponent in new[]{FiveCommand.AllRecover,FiveCommand.GoalBlock,FiveCommand.MarkReceivers})foreach(var period in new[]{2f,3f,4f})
            {
                using(var world=new FiveTestWorld())
                {
                    var m=world.match;m.profile.duration=120;m.profile.decisionSeconds=period;m.ruleManagers=new[]{true,false};
                    m.SelectCommand(Team.Navy,opponent,"repeated-visible-strategy",true);
                    int steps=0;while(m.State!=FiveMatchState.Finished&&steps<9000){world.Step(100);steps+=100;if(steps%1000==0)yield return null;}
                    var choices=new int[8];foreach(var e in m.Events.Where(e=>e.kind=="command"&&e.actor==0))choices[e.other]++;
                    report.matches.Add(new Match{fixedOpponent=(int)opponent,period=period,redScore=m.RedScore,navyScore=m.NavyScore,passes=m.PassAttempts,shots=m.Shots,corrections=m.BoundaryCorrections,elapsed=m.Elapsed,choices=choices,events=new List<FiveEvent>(m.Events)});
                }
                yield return null;
                File.WriteAllText(FiveP7Evidence.DirectoryPath+"/g5-rule-scenarios-repeated.json",JsonUtility.ToJson(report,true));
            }
            Assert.That(report.matches.All(m=>m.elapsed>=120&&m.corrections==0),Is.True);
        }
        [UnityTest] public IEnumerator OwnGoalRecoveryIsProtectedUntilAnObservedStallRequiresAnExit()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;var own=Team.Navy;m.tacticsEnabled=false;m.keepersEnabled=false;
                world.Place(6,new Vector2(54,0),Vector2.left);world.Place(7,new Vector2(57,12),Vector2.left);
                world.Place(8,new Vector2(55,20),Vector2.left);world.Place(9,new Vector2(54,-18),Vector2.left);
                world.Place(1,new Vector2(49,0),Vector2.right);world.Place(2,new Vector2(48,10),Vector2.right);
                world.Place(3,new Vector2(48,-10),Vector2.right);world.Place(4,new Vector2(35,0),Vector2.right);
                world.Give(6);Assert.That(m.Owner,Is.EqualTo(6));
                var start=m.Elapsed;m.SelectCommand(own,FiveCommand.GoalBlock,"goal-pressure-regression",true);
                while(m.Elapsed-start<3.02f)world.Step(1);
                m.teamPlanningEnabled=new[]{false,false};m.ruleManagers[(int)own]=true;m.tacticsEnabled=true;world.Step(1);
                Assert.That(m.Carrier.team,Is.EqualTo(own));
                Assert.That(m.Decision(own).command,Is.EqualTo(FiveCommand.GoalBlock),"A controlled close recovery still has no prepared safe exit.");
                Assert.That(m.Decision(own).reason,Is.EqualTo("own-goal-pressure-protect-before-exit"));
                while(m.Elapsed-start<6.1f)world.Step(1);
                Assert.That(m.Decision(own).command,Is.EqualTo(FiveCommand.FastTransition),"Observed prolonged stagnation must take priority at the next legal decision.");
            }
            yield return null;
        }
        [UnityTest] public IEnumerator WingPatternContinuesAndReceivesBeforeLiveShotOrOpponentTackle()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;var own=FiveMatchupPlayTests.Arrange(world,3,9207,false,1.8f);var start=m.Elapsed;
                m.SelectCommand(own,FiveCommand.WingCutback,"wide-continuity-regression",true);
                m.SelectCommand(FiveMatch.Other(own),FiveCommand.GoalBlock,"low-block",true);
                var frames=new List<CutbackFrame>();
                Action sample=()=>frames.Add(new CutbackFrame{time=m.Elapsed,owner=m.Owner,phase=(int)m.PassPhase,
                    ball=m.BallPosition,velocity=m.BallVelocity,positions=m.players.Select(p=>p.Position).ToArray(),
                    velocities=m.players.Select(p=>p.Velocity).ToArray(),forwards=m.players.Select(p=>p.Forward).ToArray(),
                    targets=m.players.Select(p=>p.Target).ToArray()});
                while(m.Elapsed-start<3.04f)world.Step(1,sample);
                m.ruleManagers[(int)own]=true;world.Step(1);
                Assert.That(m.Decision(own).command,Is.EqualTo(FiveCommand.WingCutback));
                while(m.Elapsed-start<12)world.Step(1,sample);
                File.WriteAllText(FiveP7Evidence.DirectoryPath+"/cutback-frames.json","["+string.Join(",",frames.Select(f=>JsonUtility.ToJson(f)))+"]");
                File.WriteAllText(FiveP7Evidence.DirectoryPath+"/cutback-continuity-events.json","["+string.Join(",",m.Events.Select(e=>JsonUtility.ToJson(e)))+"]");
                Assert.That(m.Events.Any(e=>e.kind=="pass-received"&&m.players[e.actor].team==own),Is.True,"Let the prepared cutback reach its teammate instead of dropping it at the first decision.");
                var received=m.Events.First(e=>e.kind=="pass-received"&&m.players[e.actor].team==own);
                var tackle=m.Events.FirstOrDefault(e=>e.kind=="possession"&&e.reason=="opponent-contact"&&e.other==received.other&&e.time>received.time);
                // With the frozen v8 movement this live defender can be beaten: the receiver
                // shoots and scores. Require a real continuation, not a predetermined loss.
                var shot=m.Events.FirstOrDefault(e=>e.kind=="shot"&&e.actor==received.other&&e.time>received.time);
                Assert.That(tackle!=null||shot!=null,Is.True,"The receiver must take a real shot or lose the ball through real opponent contact after reception.");
                if(tackle!=null)Assert.That(tackle.time,Is.GreaterThan(received.time));
                if(shot!=null)Assert.That(shot.time,Is.GreaterThan(received.time));
                Assert.That(m.BoundaryCorrections,Is.Zero);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator UncontestedCutbackReceivesAndShootsOnBothSides()
        {
            foreach(var mirror in new[]{false,true})
            {
                using(var world=new FiveTestWorld())
                {
                    var m=world.match;var own=FiveMatchupPlayTests.Arrange(world,3,9207,mirror,1.8f);
                    var other=FiveMatch.Other(own);var sign=FiveMatch.AttackSign(own);
                    // Isolate the receive-to-shot contract from a defender's legitimate tackle.
                    // Opponents retain their physical colliders and ordinary capabilities.
                    foreach(var p in m.players.Where(p=>p.team==other&&!p.IsKeeper))
                        world.Place(p.Index,new Vector2(-20*sign,(p.slot-2.5f)*10),Vector2.right*sign);
                    m.teamPlanningEnabled[(int)other]=false;
                    var start=m.Elapsed;m.SelectCommand(own,FiveCommand.WingCutback,"uncontested-cutback",true);
                    while(m.Elapsed-start<3.04f)world.Step(1);
                    Assert.That(m.SelectCommand(own,FiveCommand.WingCutback,"continue-uncontested-cutback"),Is.True);
                    world.Step(1);
                    Assert.That(m.Decision(own).command,Is.EqualTo(FiveCommand.WingCutback));
                    while(m.Elapsed-start<12)world.Step(1);
                    File.WriteAllText(FiveP7Evidence.DirectoryPath+"/cutback-uncontested-"+mirror+".json","["+string.Join(",",m.Events.Select(e=>JsonUtility.ToJson(e)))+"]");
                    Assert.That(m.Events.Any(e=>e.kind=="pass-received"&&m.players[e.actor].team==own),Is.True);
                    Assert.That(m.Events.Any(e=>e.kind=="shot"&&m.players[e.actor].team==own),Is.True);
                    Assert.That(m.BoundaryCorrections,Is.Zero);
                }
                yield return null;
            }
        }
    }
}
