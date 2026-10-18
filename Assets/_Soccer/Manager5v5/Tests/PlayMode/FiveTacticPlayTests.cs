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
    public sealed class FiveTacticPlayTests
    {
        [Serializable] sealed class Case
        {
            public string command;
            public Vector2[] start,end,firstTargets;
            public int passes,owner;
            public List<FiveEvent> events;
        }
        [Serializable] sealed class Report {public List<Case> cases=new List<Case>();}

        [UnityTest]
        public IEnumerator EightCommandsProduceTheirOwnPhysicalBehaviour()
        {
            var report=new Report();
            for(int id=0;id<8;id++)
            {
                using(var world=new FiveTestWorld())
                {
                    var match=world.match;match.keepersEnabled=false;match.teamPlanningEnabled[1]=false;
                    world.Place(1,new Vector2(-20,0),Vector2.right);world.Place(2,new Vector2(-26,20),Vector2.right);
                    world.Place(3,new Vector2(13,-18),Vector2.right);world.Place(4,new Vector2(10,20),Vector2.right);
                    for(int p=5;p<10;p++)world.Place(p,new Vector2(28+(p-5)*6,34),Vector2.left);
                    if(id==3)world.Place(1,new Vector2(-20,0),new Vector2(10,match.geometry.HalfWidth*.66f).normalized);
                    if(id<4)world.Give(1);
                    else {world.Place(6,new Vector2(0,22),Vector2.left);world.Give(6);}
                    match.SelectCommand(Team.Red,(FiveCommand)id,"forced-functional-fixture",true);
                    var start=match.players.Take(5).Select(p=>p.Position).ToArray();
                    world.Step(1);
                    var targets=match.players.Take(5).Select(p=>p.Target).ToArray();
                    if(id==7)
                    {
                        Assert.That(new[]{1,2,4}.All(i=>targets[i].x<-45),Is.True,"Three fields must protect the goal.");
                        Assert.That(targets[3].x,Is.GreaterThan(0),"One attacker must remain an outlet.");
                    }
                    if(id==6)Assert.That(Enumerable.Range(1,4).Count(i=>Vector2.Distance(targets[i],match.BallPosition)<5),Is.EqualTo(3));
                    if(id==4)Assert.That(Enumerable.Range(1,4).Count(i=>Vector2.Distance(targets[i],match.BallPosition)>10),Is.GreaterThanOrEqualTo(1));
                    world.Step(220);
                    var end=match.players.Take(5).Select(p=>p.Position).ToArray();
                    Assert.That(Enumerable.Range(1,4).Sum(i=>Vector2.Distance(start[i],end[i])),Is.GreaterThan(8),"No actual field movement.");
                    Assert.That(match.Decision(Team.Red).command,Is.EqualTo((FiveCommand)id),"No hidden counter switch.");
                    Assert.That(match.tacticSteps[id],Is.GreaterThan(200));
                    if(id==0)Assert.That(end[1].x,Is.GreaterThan(start[1].x+5),"Fast transition must carry forward.");
                    if(id==1 || id==2)Assert.That(match.PassAttempts,Is.GreaterThanOrEqualTo(1),"Passing command never struck the ball.");
                    if(id==3)Assert.That(Mathf.Abs(end[1].y),Is.GreaterThan(8),"Wing play must actually widen.");
                    report.cases.Add(new Case {command=((FiveCommand)id).ToString(),start=start,end=end,firstTargets=targets,passes=match.PassAttempts,owner=match.Owner,events=new List<FiveEvent>(match.Events)});
                }
                yield return null;
            }
            File.WriteAllText(FiveP7Evidence.DirectoryPath+"/g3-command-behaviour.json",JsonUtility.ToJson(report,true));
        }
        [UnityTest]
        public IEnumerator DecisionHoldAndKeeperIndependence()
        {
            using(var world=new FiveTestWorld())
            {
                var match=world.match;
                Assert.That(match.SelectCommand(Team.Red,FiveCommand.AllRecover),Is.True);
                Assert.That(match.SelectCommand(Team.Red,FiveCommand.GoalBlock),Is.False);
                world.Step(100);
                Assert.That(match.Decision(Team.Red).command,Is.EqualTo(FiveCommand.AllRecover));
                world.Step(51);
                Assert.That(match.SelectCommand(Team.Red,FiveCommand.GoalBlock),Is.True);
                var selectedAt=match.Decision(Team.Red).selectedAt;
                world.Step(151);
                Assert.That(match.SelectCommand(Team.Red,FiveCommand.GoalBlock),Is.True);
                Assert.That(match.Decision(Team.Red).selectedAt,Is.EqualTo(selectedAt),"Reselection must not restart the tactic.");
            }
            yield return null;
            Vector2? expected=null;
            for(int id=0;id<8;id++)
            {
                using(var world=new FiveTestWorld())
                {
                    world.match.SelectCommand(Team.Red,(FiveCommand)id,"keeper-invariance",true);world.Step(1);
                    var target=world.match.players[0].Target;
                    if(expected.HasValue)Assert.That(Vector2.Distance(target,expected.Value),Is.LessThan(.0001));
                    expected=target;
                }
                yield return null;
            }
        }
        [UnityTest]
        public IEnumerator KeeperDistributionFollowsOnlyThePassingPartOfTheCommand()
        {
            foreach(var command in new[]{FiveCommand.FastTransition,FiveCommand.LongPass})
            {
                using(var world=new FiveTestWorld())
                {
                    var m=world.match;m.teamPlanningEnabled=new[]{false,false};m.keepersEnabled=true;
                    for(int i=5;i<10;i++)m.players[i].Body.constraints=RigidbodyConstraints.FreezeAll;
                    for(int i=5;i<10;i++)world.Place(i,new Vector2(-40+(i-5)*20,-35),Vector2.left);
                    world.Place(5,new Vector2(59,30),Vector2.left);
                    world.Place(0,new Vector2(-55,0),Vector2.right);world.Place(1,new Vector2(-30,7),Vector2.left);
                    world.Place(2,new Vector2(-32,18),Vector2.left);world.Place(3,new Vector2(10,-10),Vector2.left);world.Place(4,new Vector2(5,18),Vector2.left);
                    world.Give(0);m.SelectCommand(Team.Red,command,"keeper-distribution",true);
                    world.Step(400);
                    var order=m.Events.FirstOrDefault(e=>e.kind=="keeper-pass-order"&&e.actor==0);
                    Assert.That(order,Is.Not.Null);
                    if(command==FiveCommand.LongPass)Assert.That(order.other,Is.InRange(3,4),"A long distribution chooses an available distant striker.");
                    else Assert.That(order.other,Is.EqualTo(1),"Fast transition uses the prepared nearby forward link.");
                    Assert.That(m.Events.Any(e=>e.kind=="pass-received"&&e.actor==0&&e.other==order.other),Is.True,
                        string.Join("; ",m.Events.Where(e=>e.kind.StartsWith("pass")||e.kind=="possession").Select(e=>$"{e.time:0.00}:{e.kind}:{e.actor}->{e.other}:{e.reason}")));
                    Assert.That(m.Clearances,Is.Zero);
                }
                yield return null;
            }
        }
        [UnityTest]
        public IEnumerator ContainmentRetainsTwoDepthTargetsWhileRecoveryCommitsThree()
        {
            foreach(var command in new[]{FiveCommand.WideTrap,FiveCommand.AllRecover})
            {
                using(var world=new FiveTestWorld())
                {
                    var m=world.match;var own=FiveInterceptionPrototypePlayTests.ArrangeRunning(world,11501,false);
                    m.teamPlanningEnabled[(int)FiveMatch.Other(own)]=false;
                    m.SelectCommand(own,command,"defensive-shape-regression",true);world.Step(1);
                    var fields=m.players.Where(p=>p.team==own&&!p.IsKeeper).ToArray();
                    var starts=fields.Select(p=>p.Position).ToArray();
                    if(command==FiveCommand.WideTrap)
                        Assert.That(fields.Count(p=>Vector2.Distance(p.Target,m.BallPosition)>20),Is.EqualTo(2),"T5 keeps two depth players.");
                    else Assert.That(fields.Count(p=>Vector2.Distance(p.Target,m.BallPosition)<5),Is.EqualTo(3),"T7 excludes the farthest field player.");
                    world.Step(20);
                    Assert.That(fields.Select((p,i)=>Vector2.Distance(p.Position,starts[i])).Sum(),Is.GreaterThan(2),"Formation must produce real movement.");
                    Assert.That(m.Decision(own).command,Is.EqualTo(command));
                }
                yield return null;
            }
        }
        [UnityTest]
        public IEnumerator AttackingPresserAssignmentDoesNotJumpBetweenPlayersDuringOneWindow()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;m.keepersEnabled=false;m.teamPlanningEnabled[1]=false;
                world.Place(1,new Vector2(-6,0),Vector2.right);world.Place(2,new Vector2(0,20),Vector2.down);
                world.Place(3,new Vector2(-25,-25),Vector2.right);world.Place(4,new Vector2(-25,25),Vector2.right);
                world.Place(6,Vector2.zero,Vector2.up);world.Give(6);
                m.SelectCommand(Team.Red,FiveCommand.FastTransition,"presser-window",true);world.Step(1);
                var first=m.Events.Last(e=>e.kind=="attacking-chaser"&&e.actor==0).other;
                Assert.That(first,Is.EqualTo(1));
                // Move the opposing carrier along a visible route while the original assignment is still live.
                world.Place(6,new Vector2(0,16),Vector2.up);
                var anchor=m.Anchor(m.players[6]);m.ball.position=new Vector3(anchor.x,m.ballSpawn.y,anchor.y);m.ball.linearVelocity=Vector3.zero;Physics.SyncTransforms();
                world.Step(2);
                Assert.That(m.Events.Count(e=>e.kind=="attacking-chaser"&&e.actor==0),Is.EqualTo(1));
                // The assigned presser now goes around a blocking carrier; it must remain the same player.
                Assert.That(m.Events.Any(e=>e.kind=="challenge-route"&&e.actor==1),Is.True);
                Assert.That(Vector2.Distance(m.players[1].Target,m.players[6].Position),
                    Is.GreaterThan(m.players[1].radius+m.players[6].radius+.3f));
                Assert.That(Vector2.Distance(m.players[1].Target,m.BallPosition),Is.LessThan(5));
                Assert.That(Vector2.Distance(m.players[2].Target,m.BallPosition),Is.GreaterThan(3));
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator GoalBlockMovesIntoTheObservedShotWithoutRetreatingPastItsExistingLine()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;m.teamPlanningEnabled[1]=false;
                for(int i=5;i<10;i++)world.Place(i,new Vector2(20+(i-5)*5,30),Vector2.left);
                world.Place(1,new Vector2(-52,8),Vector2.right);world.Place(2,new Vector2(-52,-8),Vector2.right);
                world.Place(4,new Vector2(-49,12),Vector2.right);world.Place(3,new Vector2(5,-16),Vector2.right);
                m.ball.position=new Vector3(-35,m.ballSpawn.y,6);m.ball.linearVelocity=new Vector3(-25,0,-2);Physics.SyncTransforms();
                m.SelectCommand(Team.Red,FiveCommand.GoalBlock,"visible-shot-regression",true);world.Step(1);
                Assert.That(m.players[1].Target.x,Is.EqualTo(-52).Within(.1f));
                Assert.That(m.players[1].Target.y,Is.EqualTo(4.64f).Within(.15f));
                world.Step(100);
                Assert.That(m.NavyScore,Is.Zero,"The visible shot must be physically stopped.");
                Assert.That(m.Decision(Team.Red).command,Is.EqualTo(FiveCommand.GoalBlock));
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator GoalBlockUsesNearbySidesRatherThanCrossingDefenders()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;FiveMatchupPlayTests.Arrange(world,7,9109,false,1.8f);
                m.SelectCommand(Team.Navy,FiveCommand.GoalBlock,"crossed-defender-regression",true);
                m.teamPlanningEnabled[0]=false;
                Assert.That(m.RequestShot(m.Carrier,new Vector2(m.geometry.HalfLength+1,-m.geometry.GoalHalfWidth*.66f)),Is.True);
                world.Step(1);
                Assert.That(m.players[6].Target.y,Is.LessThan(m.players[7].Target.y),"Do not swap already occupied sides merely because slot labels differ.");
                world.Step(60);
                Assert.That(m.RedScore,Is.Zero,"The original first-shot gap should remain closed.");
            }
            yield return null;
        }
        [UnityTest]
        public IEnumerator OwnPassInFlightRetainsTheAttackingSupportShape()
        {
            for(int command=0;command<4;command++)
            {
                using(var world=new FiveTestWorld())
                {
                    var m=world.match;m.keepersEnabled=false;m.teamPlanningEnabled[1]=false;
                    for(int i=5;i<10;i++)world.Place(i,new Vector2(-42+(i-5)*16,-35),Vector2.left);
                    var direction=new Vector2(40,20).normalized;
                    world.Place(1,new Vector2(-24,0),direction);world.Place(3,new Vector2(16,20),-direction);
                    world.Place(2,new Vector2(-30,23),Vector2.right);world.Place(4,new Vector2(5,-24),Vector2.right);
                    world.Give(1);m.SelectCommand(Team.Red,(FiveCommand)command,"own-flight-support",true);
                    Assert.That(m.RequestPass(m.players[1],m.players[3]),Is.True);
                    for(int step=0;step<100&&m.PassPhase!=FivePassPhase.InFlight;step++)world.Step(1);
                    Assert.That(m.PassPhase,Is.EqualTo(FivePassPhase.InFlight));world.Step(2);
                    Assert.That(m.PassPhase,Is.EqualTo(FivePassPhase.InFlight));
                    foreach(var player in m.players.Where(p=>p.team==Team.Red&&!p.IsKeeper&&p.Index!=m.PassReceiver))
                        Assert.That(Vector2.Distance(player.Target,m.BallPosition),Is.GreaterThan(1),"Own pass must not trigger a loose-ball chase by "+player.name);
                }
                yield return null;
            }
        }
        [UnityTest]
        public IEnumerator LegalTacticChangeDoesNotEraseAnInFlightReception()
        {
            using(var world=new FiveTestWorld())
            {
                var match=world.match;match.keepersEnabled=false;match.teamPlanningEnabled[1]=false;
                for(int i=5;i<10;i++)world.Place(i,new Vector2(-40+(i-5)*15,35),Vector2.left);
                world.Place(1,new Vector2(-24,0),Vector2.right);world.Place(3,new Vector2(30,0),Vector2.right);
                world.Give(1);match.SelectCommand(Team.Red,FiveCommand.LongPass,"inflight-fixture",true);
                world.Step(151);
                var pass=match.PassId;
                Assert.That(match.PassAttempts,Is.GreaterThan(0));
                Assert.That(match.SelectCommand(Team.Red,FiveCommand.GoalBlock),Is.True);
                world.Step(250);
                Assert.That(match.Events.Any(e=>e.id==pass&&e.kind=="pass-received"),Is.True,"Already launched pass must retain its receiver.");
            }
            yield return null;
        }
    }
}
