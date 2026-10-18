using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    internal static class FiveP7Evidence
    {
        public static string DirectoryPath
        {
            get
            {
                var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,"-fiveEvidenceDirectory");
                var path=index>=0?args[index+1]:"Logs/Manager5v5/p7-20261012/manual";
                Directory.CreateDirectory(path);return path;
            }
        }
        public static void Save(string name,object report)=>File.WriteAllText(Path.Combine(DirectoryPath,name+".json"),JsonUtility.ToJson(report,true));
    }
    public sealed class FiveP7PolicyPlayTests
    {
        [UnityTest] public IEnumerator StalledCornerCanBeChallengedButCentralOrMovingThreatStaysProtected()
        {
            using(var world=new FiveTestWorld())
            {
                var own=FiveMatchupPlayTests.Arrange(world,7,11001,false);
                var seen=FiveObservation.Capture(world.match,own);
                seen.ball=new Vector2(-seen.halfLength+3,17);
                seen.carrierPosition=seen.ball;seen.velocity=seen.carrierVelocity=Vector2.zero;seen.stallSeconds=7;
                Assert.That(FiveRuleManager.Choose(seen,out var reason),Is.EqualTo(FiveCommand.WideTrap));
                Assert.That(reason,Is.EqualTo("stationary-wide-carrier-release-goal-block"));
                seen.stallSeconds=13;
                Assert.That(FiveRuleManager.Choose(seen,out _),Is.EqualTo(FiveCommand.AllRecover));
                seen.ownCommand=FiveCommand.AllRecover;
                Assert.That(FiveRuleManager.Choose(seen,out _),Is.EqualTo(FiveCommand.MarkReceivers));
                seen.ball.y=0;
                Assert.That(FiveRuleManager.Choose(seen,out _),Is.EqualTo(FiveCommand.GoalBlock));
                seen.ball.y=seen.halfWidth*.85f;seen.carrierVelocity=new Vector2(-5,-3);
                Assert.That(FiveRuleManager.Choose(seen,out _),Is.EqualTo(FiveCommand.GoalBlock));
                seen.carrierVelocity=Vector2.zero;seen.stallSeconds=0;
                Assert.That(FiveRuleManager.Choose(seen,out _),Is.EqualTo(FiveCommand.GoalBlock));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator LateDeficitCanCommitSupportedWideRecoveryWithoutStackingCommands()
        {
            foreach(bool trailing in new[]{false,true})
            {
                using(var world=new FiveTestWorld())
                {
                    var m=world.match;
                    if(trailing){m.Score(Team.Red);m.ResetRound();}
                    var own=FiveMatchupPlayTests.Arrange(world,4,11001,false);
                    m.profile.duration=m.Elapsed+20;m.ruleManagers[(int)own]=true;world.Step(1);
                    var expected=trailing?FiveCommand.AllRecover:FiveCommand.WideTrap;
                    Assert.That(m.Decision(own).command,Is.EqualTo(expected),FiveHud.CommandNames[(int)expected]);
                    Assert.That(m.tacticSteps.Skip((int)own*8).Take(8).Count(n=>n>0),Is.EqualTo(1));
                    if(trailing)Assert.That(m.Decision(own).reason,Is.EqualTo("late-deficit-supported-recovery-risk"));
                }
                yield return null;
            }
        }
        [UnityTest] public IEnumerator RunningCarrierUsesAvailableInterceptionLineButImmediateGoalAndFatigueStillWin()
        {
            using(var world=new FiveTestWorld())
            {
                var own=FiveInterceptionPrototypePlayTests.ArrangeRunning(world,11501,false);
                var seen=FiveObservation.Capture(world.match,own);
                seen.carrierVelocity=new Vector2(-8,0);
                Assert.That(FiveRuleManager.Choose(seen,out var reason),Is.EqualTo(FiveCommand.WideTrap));
                Assert.That(reason,Is.EqualTo("running-carrier-meets-defensive-line"));
                seen.ball.x=-seen.halfLength+20;
                Assert.That(FiveRuleManager.Choose(seen,out _),Is.EqualTo(FiveCommand.GoalBlock));
                seen.ball.x=-5;
                for(int i=0;i<seen.own.Length;i++){var p=seen.own[i];p.stamina=.3f;seen.own[i]=p;}
                Assert.That(FiveRuleManager.Choose(seen,out _),Is.EqualTo(FiveCommand.GoalBlock));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator LowEnergyAndLateDeficitUseAnActuallyOpenDistantExit()
        {
            for(int condition=0;condition<3;condition++)
            {
                using(var world=new FiveTestWorld())
                {
                    var m=world.match;
                    if(condition==2){m.Score(Team.Navy);m.ResetRound();}
                    var own=FiveMatchupPlayTests.Arrange(world,0,11001,false);
                    if(condition==1)foreach(var p in m.players.Where(p=>p.team==own&&!p.IsKeeper))p.Stamina.SetForScenario(.3f);
                    if(condition==2)m.profile.duration=m.Elapsed+20;
                    m.ruleManagers[(int)own]=true;world.Step(1);
                    Assert.That(m.Decision(own).command,Is.EqualTo(condition==0?FiveCommand.FastTransition:FiveCommand.LongPass));
                    if(condition!=0)
                    {
                        Assert.That(m.Events.Any(e=>e.kind=="pass-signal"&&m.players[e.actor].team==own),Is.True,"Chosen exit must request an actual teammate pass.");
                        var pass=m.PassId;
                        world.Step(250);
                        Assert.That(m.Events.Any(e=>e.kind=="pass-kicked"&&e.id==pass),Is.True);
                    }
                }
                yield return null;
            }
        }
        [UnityTest] public IEnumerator LowEnergyWithoutAnOpenLinkStillAllowsCarryAndLateDeficitProtectsTheGoal()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;FiveMatchupPlayTests.Arrange(world,0,11001,false);
                var carrier=m.Carrier;
                foreach(var p in m.players.Where(p=>p.team==Team.Red&&!p.IsKeeper))
                {p.Stamina.SetForScenario(.3f);if(p!=carrier)world.Place(p.Index,carrier.Position+new Vector2(-2,p.slot-2),Vector2.right);}
                var choice=FiveRuleManager.Choose(FiveObservation.Capture(m,Team.Red),out _);
                Assert.That(choice,Is.EqualTo(FiveCommand.FastTransition));
                m.Score(Team.Red);m.ResetRound();
                var own=FiveMatchupPlayTests.Arrange(world,7,11001,false);
                m.profile.duration=m.Elapsed+20;m.ruleManagers[(int)own]=true;world.Step(1);
                Assert.That(m.Decision(own).command,Is.EqualTo(FiveCommand.GoalBlock));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator CommandSpamCannotRefillStaminaResetKickCooldownOrDuplicatePassSignals()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;m.tacticsEnabled=false;m.keepersEnabled=false;
                world.Place(1,new Vector2(-24,0),Vector2.right);world.Place(3,new Vector2(24,0),Vector2.left);world.Give(1);
                m.SelectCommand(Team.Red,FiveCommand.LongPass,"spam-start",true);
                Assert.That(m.RequestPass(m.players[1],m.players[3]),Is.True);
                m.players[1].Stamina.SetForScenario(.4f);m.players[1].KickReadyAt=m.Elapsed+2;
                var ready=m.players[1].KickReadyAt;var pass=m.PassId;var selectedAt=m.Decision(Team.Red).selectedAt;
                for(int i=0;i<100;i++)
                {
                    Assert.That(m.SelectCommand(Team.Red,FiveCommand.GoalBlock),Is.False);
                    Assert.That(m.RequestPass(m.players[1],m.players[3]),Is.False);
                }
                Assert.That(m.players[1].Stamina.Value,Is.EqualTo(.4f));
                Assert.That(m.players[1].KickReadyAt,Is.EqualTo(ready));
                Assert.That(m.PassId,Is.EqualTo(pass));
                Assert.That(m.Decision(Team.Red).command,Is.EqualTo(FiveCommand.LongPass));
                Assert.That(m.Decision(Team.Red).selectedAt,Is.EqualTo(selectedAt));
                Assert.That(m.Events.Count(e=>e.kind=="pass-signal"&&e.id==pass),Is.EqualTo(1));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator LeavingKickPlateImmediatelyReleasesPossessionAndDribbleCost()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;m.tacticsEnabled=false;m.keepersEnabled=false;
                world.Place(1,Vector2.zero,Vector2.right);world.Give(1);var player=m.players[1];
                player.Stamina.SetForScenario(.39f);player.Body.constraints=RigidbodyConstraints.FreezeAll;
                m.ball.constraints=RigidbodyConstraints.FreezeAll;
                var point=new Vector3(m.profile.releaseDistance+.5f,m.ballSpawn.y,0);
                m.ball.position=point;m.ball.transform.position=point;Physics.SyncTransforms();
                world.Step(1);
                Assert.That(m.Owner,Is.EqualTo(-1));Assert.That(player.Dribbling,Is.False);
                Assert.That(player.SpeedFactor,Is.EqualTo(.90f).Within(.0001f));
                Assert.That(m.Events.Any(e=>e.kind=="release"&&e.reason=="outside-kick-plate"),Is.True);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator RecoveryUsesActualMotionEvenIfTheCommandRequestsAStop()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;m.tacticsEnabled=false;m.techniquesEnabled=false;var p=m.players[1];
                world.Place(1,Vector2.zero,Vector2.right);p.Stamina.SetForScenario(.5f);
                p.SetTarget(p.Position,p.Position+Vector2.right);p.Body.linearVelocity=Vector3.right*5;
                var before=p.Stamina.Value;m.SimulatePhysicsStep(.01f);
                Assert.That(p.Stamina.Value,Is.LessThan(before),"A stop order cannot grant recovery while the body is still moving fast.");
                world.Step(60);var resting=p.Stamina.Value;world.Step(50);
                Assert.That(p.Velocity.magnitude,Is.LessThan(m.profile.slowRecoverySpeed));
                Assert.That(p.Stamina.Value,Is.GreaterThan(resting),"Actual slow/resting motion must recover normally.");
            }
            yield return null;
        }
    }
}
