using System.Collections;
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.MLAgents.Actuators;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveDefensiveStatePlayTests
    {
        [Serializable] sealed class KeeperFixture { public string source; public float[] observation; }
        [Serializable] sealed class KeeperFixtures { public KeeperFixture[] cases; }
        [UnityTest] public IEnumerator RecordedKeeperContactMustDisengageAndReleaseWithinTenSeconds()
        {
            var fixtures=JsonUtility.FromJson<KeeperFixtures>(File.ReadAllText(
                "Assets/_Soccer/Manager5v5/Tests/PlayMode/KeeperHoldFixtures.json"));
            var names=FiveLearningContract.FrameFeatureNames();
            foreach(var fixture in fixtures.cases)
            {
                using(var w=new FiveTestWorld())
                {
                    var m=w.match;var obs=fixture.observation;
                    float Value(string name)=>obs[Array.IndexOf(names,name)];
                    for(int t=0;t<2;t++)for(int slot=0;slot<5;slot++)
                    {
                        var prefix=$"player.{(t==0?"own":"opponent")}.{slot}.";
                        w.Place(t*5+slot,new Vector2(Value(prefix+"x")*m.geometry.HalfLength,Value(prefix+"z")*m.geometry.HalfWidth),
                            new Vector2(Value(prefix+"forwardX"),Value(prefix+"forwardZ")));
                    }
                    var keeper=Value("owner.own.0")>.5f?0:5;
                    // Restore public ownership and the recorded physical pocket, without moving a player out of contact.
                    m.ball.position=new Vector3(Value("ball.x")*m.geometry.HalfLength,m.ballSpawn.y,Value("ball.z")*m.geometry.HalfWidth);
                    typeof(FiveMatch).GetMethod("Acquire",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                        .Invoke(m,new object[]{m.players[keeper],"recorded-contact-fixture"});
                    Physics.SyncTransforms();
                    Assert.That(m.DefensiveFormationActive(FiveMatch.Other(m.players[keeper].team)),Is.True);
                    var released=false;
                    for(int step=0;step<500&&!released;step++){w.Step(1);released=m.Owner!=keeper;}
                    Assert.That(released,Is.True,fixture.source+": keeper and retreating field must physically separate; no indefinite T9 hold.");
                    Assert.That(m.BoundaryCorrections,Is.Zero);
                }
                yield return null;
            }
        }
        [UnityTearDown] public IEnumerator Cleanup(){FiveTestWorld.DisposeOutstanding();yield return null;}
        sealed class Mask : IDiscreteActionMask
        {
            public readonly bool[] blocked=new bool[8];
            public void SetActionEnabled(int branch,int actionIndex,bool isEnabled)
            {Assert.That(branch,Is.Zero);Assert.That(actionIndex,Is.InRange(0,7));blocked[actionIndex]=!isEnabled;}
        }
        [UnityTest] public IEnumerator KeeperPossessionOverridesImmediatelyAndReleaseReopensDecisionOnBothSides()
        {
            foreach(Team team in new[]{Team.Red,Team.Navy})
            {
                using(var w=new FiveTestWorld())
                {
                    var m=w.match;var sign=FiveMatch.AttackSign(team);var offset=(int)team*5;var keeper=(1-(int)team)*5;
                    m.keepersEnabled=false;
                    m.SelectCommand(team,FiveCommand.AllRecover);
                    w.Place(keeper,new Vector2(m.geometry.HalfLength-5,0)*sign,-Vector2.right*sign);
                    w.Place(offset+1,new Vector2(25,-12)*sign,-Vector2.right*sign);
                    w.Place(offset+2,new Vector2(25,12)*sign,-Vector2.right*sign);
                    w.Place(offset+3,new Vector2(m.geometry.HalfLength-7,-10)*sign,-Vector2.right*sign);
                    w.Place(offset+4,new Vector2(m.geometry.HalfLength-7,10)*sign,-Vector2.right*sign);
                    var revision=m.Decision(team).revision;
                    w.Give(keeper); Assert.That(m.Owner,Is.EqualTo(keeper));
                    Assert.That(m.DefensiveFormationActive(team),Is.True);
                    Assert.That(m.DefensiveFormationActive(FiveMatch.Other(team)),Is.False);
                    Assert.That(FiveLearningContract.CanDecide(m,team),Is.False);
                    Assert.That(m.SelectCommand(team,FiveCommand.LongPass),Is.False);
                    w.Step(1);
                    Assert.That(m.Decision(team).revision,Is.EqualTo(revision));
                    foreach(var p in m.players.Where(p=>p.team==team&&!p.IsKeeper))
                    {
                        Assert.That(p.Target.x*sign,p.slot<=2?Is.LessThan(0):Is.GreaterThan(0));
                        Assert.That(p.Target.x*sign,Is.LessThan(p.Position.x*sign));
                        Assert.That(Vector2.Distance(p.Target,m.players[keeper].Position),Is.GreaterThan(m.profile.keeperAreaDepth-1));
                        Assert.That(p.Velocity.x*sign,Is.LessThan(0));
                    }
                    Assert.That(m.Events.Count(e=>e.kind=="defensive-formation-enter"&&e.actor==(int)team),Is.EqualTo(1));
                    m.ball.position=new Vector3(0,m.ballSpawn.y,30);m.ball.linearVelocity=Vector3.zero;
                    Physics.SyncTransforms();w.Step(1);
                    Assert.That(m.DefensiveFormationActive(team),Is.False);
                    Assert.That(m.Elapsed,Is.LessThan(3));
                    Assert.That(FiveLearningContract.CanDecide(m,team),Is.True);
                    Assert.That(m.SelectCommand(team,FiveCommand.LongPass),Is.True);
                    Assert.That(m.Decision(team).nextDecisionAt,Is.EqualTo(m.Elapsed+3).Within(.001));
                    Assert.That(FiveLearningContract.ActionCount,Is.EqualTo(8));
                    Assert.That(System.Enum.GetValues(typeof(FiveCommand)).Length,Is.EqualTo(8));
                }
                yield return null;
            }
        }
        [UnityTest] public IEnumerator EveryCommandSharesManualRuleAndLearningLimitsWithT7Exception()
        {
            foreach(var action in Enumerable.Range(0,8))
            using(var w=new FiveTestWorld())
            {
                var m=w.match;m.techniquesEnabled=false;
                var session=FiveLearningFactory.Create(m,false,false);session.InitializeSession();
                Action<int> step=count=>w.Step(count,()=>session.History.Record(m));
                foreach(Team team in new[]{Team.Red,Team.Navy})
                {
                    m.ResetMatch();
                    session.History.Reset(m);
                    var command=(FiveCommand)action;
                    int limit=action==6?2:3;
                    int alternative=(action+1)%8;
                    for(int i=0;i<limit;i++)
                    {Assert.That(FiveLearningContract.Apply(m,team,action),Is.True);step(151);}
                    var mask=new Mask();session.agents[(int)team].WriteDiscreteActionMask(mask);
                    Assert.That(mask.blocked[action],Is.True);
                    Assert.That(mask.blocked.Count(b=>b),Is.EqualTo(1));
                    Assert.That(m.CommandBlockedSelections(FiveMatch.Other(team),command),Is.Zero);
                    Assert.That(FiveLearningContract.Apply(m,team,action),Is.False);
                    Assert.That(m.SelectCommand(team,command),Is.False,"Manual inputs share the same limit.");
                    foreach(var variant in new[]{FiveRuleVariant.Legacy,FiveRuleVariant.Strong})
                    {m.ruleVariants[(int)team]=variant;Assert.That(m.ChooseRule(team,out _),Is.Not.EqualTo(command));}
                    step(500); // Time passing alone cannot clear a selection-based cooldown.
                    Assert.That(m.CommandBlockedSelections(team,command),Is.EqualTo(2));
                    for(int remaining=1;remaining>=0;remaining--)
                    {
                        session.agents[(int)team].WriteDiscreteActionMask(mask);Assert.That(mask.blocked[action],Is.True);
                        foreach(var variant in new[]{FiveRuleVariant.Legacy,FiveRuleVariant.Strong})
                        {m.ruleVariants[(int)team]=variant;Assert.That(m.ChooseRule(team,out _),Is.Not.EqualTo(command));}
                        Assert.That(FiveLearningContract.Apply(m,team,action),Is.False);
                        Assert.That(FiveLearningContract.Apply(m,team,alternative),Is.True);
                        Assert.That(m.CommandBlockedSelections(team,command),Is.EqualTo(remaining));
                        Assert.That(FiveLearningContract.Apply(m,team,action),Is.False,"Alternative must retain its 3-second interval.");
                        Assert.That(FiveLearningContract.Apply(m,team,2),Is.False,"Rejected choices do not consume cooldown.");
                        Assert.That(m.CommandBlockedSelections(team,command),Is.EqualTo(remaining));
                        step(151);
                    }
                    Assert.That(FiveLearningContract.Apply(m,team,action),Is.True);
                    session.agents[(int)team].WriteDiscreteActionMask(mask);Assert.That(mask.blocked[action],Is.False);
                }
                m.ResetMatch();Assert.That(m.decisions.All(d=>d.consecutiveSelections.All(x=>x==0)
                    &&d.blockedSelections.All(x=>x==0)),Is.True);
                session.History.Reset(m);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator T9AndGoalRestartDoNotConsumeTheTwoBlockedSelections()
        {
            foreach(FiveCommand command in System.Enum.GetValues(typeof(FiveCommand)))
            foreach(Team team in new[]{Team.Red,Team.Navy})
            using(var w=new FiveTestWorld())
            {
                var m=w.match;m.keepersEnabled=false;
                for(int i=0;i<(command==FiveCommand.AllRecover?2:3);i++)
                {Assert.That(m.SelectCommand(team,command),Is.True);w.Step(151);}
                m.Paused=true;Assert.That(FiveLearningContract.Apply(m,team,0),Is.False);w.Step(10);m.Paused=false;
                m.ResetRound();Assert.That(m.CommandBlockedSelections(team,command),Is.EqualTo(2));
                w.Give((int)FiveMatch.Other(team)*5);w.Step(10);
                Assert.That(m.DefensiveFormationActive(team),Is.True);
                Assert.That(m.SelectCommand(team,FiveCommand.FastTransition),Is.False);
                Assert.That(m.CommandBlockedSelections(team,command),Is.EqualTo(2));
                m.ball.position=new Vector3(0,m.ballSpawn.y,25);m.ball.linearVelocity=Vector3.zero;
                Physics.SyncTransforms();w.Step(1);
                var alternative=command==FiveCommand.FastTransition?FiveCommand.LongPass:FiveCommand.FastTransition;
                Assert.That(m.SelectCommand(team,alternative),Is.True);
                Assert.That(m.CommandBlockedSelections(team,command),Is.EqualTo(1));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ThreeChoiceCommandAndT7CooldownsAlternateIndependently()
        {
            using(var w=new FiveTestWorld())
            {
                var m=w.match;m.techniquesEnabled=false;
                foreach(Team team in new[]{Team.Red,Team.Navy})
                {
                    m.ResetMatch();
                    foreach(var action in new[]{1,1,1,6,6,1,1,1,6,6})
                    {
                        Assert.That(FiveLearningContract.Apply(m,team,action),Is.True);w.Step(151);
                        var state=m.Decision(team);
                        Assert.That(state.consecutiveLongPass,Is.LessThanOrEqualTo(3));
                        Assert.That(state.consecutiveRecover,Is.LessThanOrEqualTo(2));
                        Assert.That(state.longPassBlockedSelections>0&&state.recoverBlockedSelections>0,Is.False);
                    }
                    Assert.That(m.CommandBlockedSelections(team,FiveCommand.LongPass),Is.Zero);
                    Assert.That(m.CommandBlockedSelections(team,FiveCommand.AllRecover),Is.EqualTo(2));
                }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator NonconsecutiveChoicesResetStreaksForEveryCommand()
        {
            using(var w=new FiveTestWorld())
            {
                var m=w.match;m.techniquesEnabled=false;
                foreach(Team team in new[]{Team.Red,Team.Navy})
                {
                    m.ResetMatch();
                    foreach(var action in Enumerable.Range(0,16).Select(i=>i%8))
                    {
                        Assert.That(FiveLearningContract.Apply(m,team,action),Is.True);w.Step(151);
                        Assert.That(m.Decision(team).blockedSelections.All(x=>x==0),Is.True);
                        Assert.That(m.Decision(team).consecutiveSelections[action],Is.EqualTo(1));
                    }
                }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator T9DetoursAroundOpposingFieldPlayersAndKeeperOnBothSides()
        {
            foreach(Team team in new[]{Team.Red,Team.Navy})foreach(var keeperObstacle in new[]{false,true})
            {
                using(var w=new FiveTestWorld())
                {
                    var m=w.match;var sign=FiveMatch.AttackSign(team);var offset=(int)team*5;var enemy=(1-(int)team)*5;
                    m.tacticsEnabled=false;m.keepersEnabled=false;
                    foreach(var p in m.players)w.Place(p.Index,new Vector2(-40+p.slot*8,25)*sign,Vector2.right*sign);
                    var actor=m.players[offset+(keeperObstacle?3:1)];var obstacle=m.players[enemy+(keeperObstacle?0:1)];
                    var lane=keeperObstacle?-m.geometry.HalfWidth*.35f:0;
                    w.Place(enemy,new Vector2(keeperObstacle?40:55,lane)*sign,-Vector2.right*sign);
                    if(!keeperObstacle)w.Place(obstacle.Index,new Vector2(14,0)*sign,Vector2.right*sign);
                    w.Place(actor.Index,new Vector2(keeperObstacle?48:20,lane)*sign,-Vector2.right*sign);
                    w.Give(enemy);
                    foreach(var p in m.players)if(p!=actor)p.Body.constraints=RigidbodyConstraints.FreezeAll;
                    m.ball.constraints=RigidbodyConstraints.FreezeAll;
                    var start=actor.Position.x*sign;float minimum=float.PositiveInfinity;
                    w.Step(180,()=>minimum=Mathf.Min(minimum,Vector2.Distance(actor.Position,obstacle.Position)));
                    Assert.That(m.DefensiveFormationActive(team),Is.True);
                    Assert.That(m.DefensiveFormationActive(FiveMatch.Other(team)),Is.False,"Own keeper must not activate T9.");
                    Assert.That(minimum,Is.GreaterThan(actor.radius+obstacle.radius+.1f));
                    Assert.That(actor.Position.x*sign,Is.LessThan(start-8),"Detour must continue toward the formation.");
                    Assert.That(m.Events.Any(e=>e.kind=="defensive-formation-avoid"&&e.actor==actor.Index),Is.True);
                }
                yield return null;
            }
        }
        [UnityTest] public IEnumerator KeeperDistributesWithinSixSecondsEvenWithoutTacticalReceiverOrReceiverReadiness()
        {
            foreach(Team team in new[]{Team.Red,Team.Navy})foreach(var frozenReceiver in new[]{false,true})
            {
                using(var w=new FiveTestWorld())
                {
                    var m=w.match;var sign=FiveMatch.AttackSign(team);var offset=(int)team*5;
                    m.tacticsEnabled=false;m.SelectCommand(team,FiveCommand.LongPass);
                    for(int t=0;t<2;t++)for(int slot=0;slot<5;slot++)
                        w.Place(t*5+slot,new Vector2(t==(int)team?-55+slot*2:30,slot*5)*sign,Vector2.right*sign);
                    w.Give(offset);var since=m.OwnerSince;
                    if(frozenReceiver)foreach(var p in m.players)if(p.team==team&&!p.IsKeeper)p.Body.constraints=RigidbodyConstraints.FreezeAll;
                    for(int step=0;step<300;step++)
                    {
                        if(frozenReceiver)foreach(var p in m.players)if(p.team==team&&!p.IsKeeper)
                        {
                            var away=p.Position-m.BallPosition;
                            p.Body.rotation=Quaternion.LookRotation(new Vector3(away.x,0,away.y));
                        }
                        w.Step(1);
                    }
                    var kick=m.Events.FirstOrDefault(e=>e.kind=="pass-kicked"&&e.actor==offset);
                    Assert.That(kick,Is.Not.Null);
                    Assert.That(kick.time-since,Is.LessThanOrEqualTo(6));
                    Assert.That(m.players[kick.other].team,Is.EqualTo(team));
                    Assert.That(m.players[kick.other].IsKeeper,Is.False);
                    Assert.That(m.Events.Any(e=>e.kind=="keeper-clearance"&&e.actor==offset),Is.False);
                    if(frozenReceiver)Assert.That(m.Events.Any(e=>e.kind=="keeper-deadline-pass"),Is.True);
                }
                yield return null;
            }
        }
        [UnityTest] public IEnumerator KeeperAccelerationAndSpeedAreOnePointTwo()
        {
            using(var w=new FiveTestWorld())
            {
                var m=w.match;m.techniquesEnabled=false;
                foreach(var offset in new[]{0,5})
                {
                    var keeper=m.players[offset];var field=m.players[offset+1];
                    keeper.Dribbling=field.Dribbling=false;field.Stamina.SetForScenario(1);
                    keeper.Body.linearVelocity=field.Body.linearVelocity=Vector3.zero;
                    keeper.SetTarget(keeper.Position+Vector2.right*30,Vector2.right*100);
                    field.SetTarget(field.Position+Vector2.right*30,Vector2.right*100);
                    keeper.Drive(.01f,m.profile);field.Drive(.01f,m.profile);
                    Assert.That(keeper.Velocity.magnitude/field.Velocity.magnitude,Is.EqualTo(1.2f).Within(.0001));
                    Assert.That(keeper.MaximumMoveSpeed(m.profile)/field.MaximumMoveSpeed(m.profile),Is.EqualTo(1.2f).Within(.0001));
                }
            }
            yield return null;
        }
    }
}
