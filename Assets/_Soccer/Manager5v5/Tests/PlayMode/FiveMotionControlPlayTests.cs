using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveMotionControlPlayTests
    {
        static void Isolate(FiveTestWorld world)
        {
            world.match.tacticsEnabled=false;world.match.keepersEnabled=false;
            for(int i=0;i<10;i++)world.Place(i,new Vector2(-45+i*9,32),Vector2.right);
        }
        static void BallAt(FiveMatch match,Vector2 point)
        {
            var position=new Vector3(point.x,match.ballSpawn.y,point.y);
            match.ball.position=position;match.ball.transform.position=position;
            match.ball.linearVelocity=Vector3.zero;Physics.SyncTransforms();
        }
        [UnityTest] public IEnumerator RecoveryExcludesTheFarthestFieldForBothTeamsAndReplans()
        {
            foreach(var team in new[]{Team.Red,Team.Navy})
            using(var world=new FiveTestWorld())
            {
                var m=world.match;Isolate(world);var offset=(int)team*5;
                world.Place(offset+1,new Vector2(-6,0),Vector2.right);
                world.Place(offset+2,new Vector2(0,-12),Vector2.up);
                world.Place(offset+3,new Vector2(0,18),Vector2.down);
                world.Place(offset+4,new Vector2(-35,0),Vector2.right);
                BallAt(m,Vector2.zero);
                m.tacticsEnabled=true;m.teamPlanningEnabled[(int)FiveMatch.Other(team)]=false;
                m.SelectCommand(team,FiveCommand.AllRecover,"three-player-recovery",true);world.Step(1);
                var reserve=m.players[offset+4];var held=reserve.Target;
                Assert.That(Vector2.Distance(held,new Vector2(-35,0)),Is.LessThan(.01f));
                Assert.That(Enumerable.Range(1,3).All(i=>Vector2.Distance(m.players[offset+i].Target,m.BallPosition)<4),Is.True);
                world.Step(30);
                Assert.That(Vector2.Distance(reserve.Position,held),Is.LessThan(.1f),"The excluded player must not join the crowd.");
                world.Place(offset+1,new Vector2(-50,0),Vector2.right);
                m.tacticsEnabled=false;world.Step(125);BallAt(m,Vector2.zero);m.tacticsEnabled=true;
                m.SelectCommand(team,FiveCommand.AllRecover,"new-recovery-window",true);world.Step(1);
                Assert.That(Vector2.Distance(m.players[offset+1].Target,new Vector2(-50,0)),Is.LessThan(.1f));
                Assert.That(Vector2.Distance(reserve.Target,m.BallPosition),Is.LessThan(4));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator KickPlateRejectsSideRearAndDistantBallsAtEveryHeading()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;Isolate(world);
                foreach(var direction in new[]{Vector2.right,Vector2.left,Vector2.up,Vector2.down})
                {
                    m.ResetMatch();world.Place(1,Vector2.zero,direction);var p=m.players[1];
                    world.Give(1);Assert.That(m.Owner,Is.EqualTo(1));Assert.That(m.InDribbleArea(p),Is.True);
                    foreach(var delta in new[]{new Vector2(-direction.y,direction.x)*m.profile.holdOffset,-direction*m.profile.holdOffset,direction*4})
                    {
                        BallAt(m,p.Position+delta);Assert.That(m.InDribbleArea(p),Is.False);
                        m.Tick(.01f);
                        Assert.That(m.Owner,Is.EqualTo(-1));Assert.That(p.Dribbling,Is.False);
                        Assert.That(m.BallVelocity.magnitude,Is.LessThan(.001f),"No first-touch or dribble force outside the plate.");
                    }
                }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator AbruptPhysicalRotationOutsideThePocketStillReleasesTheBall()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;Isolate(world);world.Place(1,Vector2.zero,Vector2.right);world.Give(1);
                var p=m.players[1];p.Body.rotation=Quaternion.LookRotation(Vector3.left);
                p.transform.rotation=p.Body.rotation;Physics.SyncTransforms();m.Tick(.01f);
                Assert.That(m.Owner,Is.EqualTo(-1),"An abrupt physical rotation cannot reacquire a ball now behind the body.");
                Assert.That(m.Events.Any(e=>e.kind=="release"&&e.reason=="outside-kick-plate"),Is.True);
                Assert.That(m.InDribbleArea(p),Is.False);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ShallowDribbleTurnKeepsTheBallAndWallRecoveryCanReachTheFrontPlate()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;Isolate(world);world.Place(1,Vector2.zero,Vector2.right);world.Give(1);
                var p=m.players[1];var direction=new Vector2(1,.2f).normalized;
                for(int i=0;i<150;i++)
                {
                    p.SetTarget(p.Position+direction*10,p.Position+direction*10);world.Step(1);
                    Assert.That(m.Owner,Is.EqualTo(1),"A shallow forward steering adjustment should keep control.");
                }
                m.ResetMatch();Isolate(world);
                world.Place(1,new Vector2(-20,-36),Vector2.down);
                world.Place(2,new Vector2(-40,20),Vector2.down);world.Place(3,new Vector2(30,20),Vector2.down);world.Place(4,new Vector2(40,30),Vector2.down);
                BallAt(m,new Vector2(-20,-m.geometry.HalfWidth+m.ballRadius));
                m.tacticsEnabled=true;m.teamPlanningEnabled[1]=false;m.SelectCommand(Team.Red,FiveCommand.AllRecover,"front-wall-recovery",true);
                var acquired=false;
                for(int i=0;i<150;i++){world.Step(1);acquired|=m.Owner==1;}
                Assert.That(acquired,Is.True,"The normal two-metre formation margin must not put a wall-side ball beyond the front plate's reach.");
                Assert.That(m.BoundaryCorrections,Is.Zero);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator MovingPlayersFaceTravelAndSuppressCollisionSpinWhileFreeFacingStillWorks()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;Isolate(world);m.techniquesEnabled=false;
                world.Place(1,Vector2.zero,Vector2.right);var p=m.players[1];
                var totalTurn=0f;
                for(int i=0;i<150;i++)
                {
                    var before=p.Body.rotation;
                    var angle=i*.8f;var nearby=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*.1f;
                    p.SetTarget(p.Position+Vector2.right*10,p.Position+nearby);
                    p.Body.angularVelocity=Vector3.up*12;
                    world.Step(1);totalTurn+=Quaternion.Angle(before,p.Body.rotation);
                    Assert.That(Vector2.Dot(p.Forward,Vector2.right),Is.GreaterThan(.99f));
                }
                Assert.That(totalTurn,Is.LessThan(5),"A jittering nearby ball must not make the moving player spin.");
                p.SetTarget(p.Position,p.Position+Vector2.left*10,true);world.Step(90);
                Assert.That(Vector2.Dot(p.Forward,Vector2.left),Is.GreaterThan(.99f),"A receiver can turn fully to face an incoming pass.");
            }
            yield return null;
        }
        [UnityTest] public IEnumerator StaminaIsEightThinBarsWithoutLabelsOrNumbers()
        {
            using(var world=new FiveTestWorld())
            {
                yield return null;yield return null;
                var root=world.root.GetComponentInChildren<UIDocument>().rootVisualElement;
                var bars=root.Query<ProgressBar>(className:"head-stamina").ToList();
                Assert.That(bars.Count,Is.EqualTo(8));
                foreach(var bar in bars)
                {
                    Assert.That(bar.title,Is.Empty);
                    Assert.That(bar.resolvedStyle.height,Is.LessThanOrEqualTo(5.1f));
                    foreach(var part in bar.Query<VisualElement>().ToList().Where(p=>p.resolvedStyle.display!=DisplayStyle.None))
                        Assert.That(part.worldBound.height,Is.LessThanOrEqualTo(5.1f),"Every rendered part of the bar must fit its thin outer frame: "+string.Join(",",part.GetClasses()));
                    Assert.That(bar.Q<Label>(className:"unity-progress-bar__title").resolvedStyle.display,Is.EqualTo(DisplayStyle.None));
                }
            }
        }
        [Serializable] sealed class MatchReport
        {
            public string rules=FiveStrongRule.Version,runtime=FiveMatch.PlayerRuntimeVersion;
            public float elapsed,maximumStall;
            public int redScore,navyScore,passes,receptions,shots,releases,boundaryCorrections;
            public int[] recoverySelections;
            public float minimumRecoveryInterval=300;
            public bool finished;
        }
        [UnityTest,Category("LongRunning"),Timeout(600000)] public IEnumerator UpdatedStrongRulesCompleteAFullThreeHundredSecondMatch()
        {
            var report=new MatchReport();
            using(var world=new FiveTestWorld())
            {
                var m=world.match;m.ruleVariants=new[]{FiveRuleVariant.Strong,FiveRuleVariant.Strong};m.ruleManagers=new[]{true,true};
                for(int step=0;step<20000&&m.State!=FiveMatchState.Finished;step++)
                {
                    world.Step(1);report.maximumStall=Mathf.Max(report.maximumStall,m.ObservedStallSeconds);
                    if(step%100==0)yield return null;
                }
                report.elapsed=m.Elapsed;report.redScore=m.RedScore;report.navyScore=m.NavyScore;
                report.passes=m.PassAttempts;report.receptions=m.PassReceptions;report.shots=m.Shots;
                report.boundaryCorrections=m.BoundaryCorrections;report.finished=m.State==FiveMatchState.Finished;
                report.releases=m.Events.Count(e=>e.kind=="release"&&e.reason=="outside-kick-plate");
                report.recoverySelections=new int[2];
                for(int team=0;team<2;team++)
                {
                    var choices=m.Events.Where(e=>e.kind=="command"&&e.actor==team&&e.other==(int)FiveCommand.AllRecover).ToArray();
                    report.recoverySelections[team]=choices.Length;
                    for(int i=1;i<choices.Length;i++)report.minimumRecoveryInterval=Mathf.Min(report.minimumRecoveryInterval,choices[i].time-choices[i-1].time);
                }
                File.WriteAllText(FiveP7Evidence.DirectoryPath+"/motion-front-control-match.json",JsonUtility.ToJson(report,true));
                Assert.That(report.finished,Is.True);Assert.That(report.elapsed,Is.GreaterThanOrEqualTo(300));
                Assert.That(report.boundaryCorrections,Is.Zero);Assert.That(report.maximumStall,Is.LessThan(30));
                Assert.That(report.passes,Is.GreaterThan(0));Assert.That(report.shots,Is.GreaterThan(0));
                Assert.That(report.minimumRecoveryInterval,Is.GreaterThanOrEqualTo(m.profile.decisionSeconds-.03f));
            }
        }
    }
}
