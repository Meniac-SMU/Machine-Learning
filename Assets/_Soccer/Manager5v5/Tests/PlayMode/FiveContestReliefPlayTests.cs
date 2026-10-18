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
    public sealed class FiveContestReliefPlayTests
    {
        [UnityTearDown] public IEnumerator Cleanup(){FiveTestWorld.DisposeOutstanding();yield return null;}
        static void Quiet(FiveTestWorld w)
        {
            w.match.keepersEnabled=false;w.match.teamPlanningEnabled=new[]{false,false};
            for(int i=0;i<10;i++)w.Place(i,new Vector2(-45+i*9,25),Vector2.down);
        }
        static void Ball(FiveTestWorld w,Vector2 point)
        {
            var m=w.match;m.ball.position=new Vector3(point.x,m.ballSpawn.y,point.y);m.ball.linearVelocity=Vector3.zero;Physics.SyncTransforms();
        }
        static void Crowd(FiveTestWorld w,Vector2 point,int mirror=1)
        {
            Quiet(w);var m=w.match;
            var offsets=new[]{new Vector2(-1.35f,0),new Vector2(-1.9f,1.4f),new Vector2(1.35f,0),new Vector2(1.9f,-1.4f)};
            var ids=new[]{1,2,6,7};
            for(int i=0;i<ids.Length;i++)
            {var p=m.ClampField((point+offsets[i])*mirror,m.players[ids[i]].radius);w.Place(ids[i],p,(point*mirror-p).normalized);}
            Ball(w,point*mirror);
        }
        [UnityTest] public IEnumerator AllEightOrdersAndLearningActionsShareReliefWithoutReplacingTheOrder()
        {
            foreach(bool learning in new[]{false,true})for(int command=0;command<8;command++)
            {
                using(var w=new FiveTestWorld())
                {
                    Crowd(w,Vector2.zero);var m=w.match;
                    foreach(var team in new[]{Team.Red,Team.Navy})
                        Assert.That(learning?FiveLearningContract.Apply(m,team,command):m.SelectCommand(team,(FiveCommand)command,"manual-test"),Is.True);
                    var revisions=m.decisions.Select(d=>d.revision).ToArray();
                    w.Step(200);
                    Assert.That(m.Events.Any(e=>e.kind=="contest-start"),Is.True,$"learning={learning}, command={command}");
                    Assert.That(m.decisions.Select(d=>(int)d.command),Is.All.EqualTo(command));
                    Assert.That(m.decisions.Select(d=>d.revision),Is.EqualTo(revisions));
                    Assert.That(m.Events.Count(e=>e.kind=="command"),Is.EqualTo(2));
                    Assert.That(m.Events.Any(e=>e.kind=="contest-backoff"||e.kind=="contest-reangle"||e.kind=="contest-side-carry"||e.kind=="contest-outlet-pass"),Is.True);
                }
                yield return null;
            }
        }
        [UnityTest] public IEnumerator KickoffPauseLonePlayerAndFastBallDoNotTriggerRelief()
        {
            using(var w=new FiveTestWorld())
            {
                Crowd(w,Vector2.zero);var m=w.match;w.Step(80);
                Assert.That(m.Events.Any(e=>e.kind=="contest-start"),Is.False);
                m.Paused=true;var elapsed=m.Elapsed;w.Step(150);
                Assert.That(m.Elapsed,Is.EqualTo(elapsed));Assert.That(m.ContestReliefActive,Is.False);
                m.Paused=false;w.Step(120);Assert.That(m.Events.Any(e=>e.kind=="contest-start"),Is.True);
                m.Score(Team.Red);while(m.State==FiveMatchState.GoalPause)w.Step(1);
                Assert.That(m.ContestReliefActive,Is.False);Assert.That(Enumerable.Range(0,10).Select(m.ReliefAction),Is.All.EqualTo(FiveReliefAction.None));
            }
            using(var w=new FiveTestWorld())
            {
                Quiet(w);w.Place(1,new Vector2(-1.3f,0),Vector2.right);Ball(w,Vector2.zero);w.Step(220);
                Assert.That(w.match.Events.Any(e=>e.kind=="contest-start"),Is.False);
            }
            using(var w=new FiveTestWorld())
            {
                Quiet(w);w.Step(150);
                // Keep this fast ball outside both kick plates so first-touch control does not slow it into a real contest.
                w.Place(1,new Vector2(-2.8f,0),Vector2.right);w.Place(6,new Vector2(2.8f,0),Vector2.left);
                Ball(w,Vector2.zero);w.match.ball.linearVelocity=new Vector3(0,0,-15);w.Step(60);
                Assert.That(w.match.Events.Any(e=>e.kind=="contest-start"),Is.False);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ReliefLeavesKeepersAndTheExcludedRecoveryFieldAlone()
        {
            using(var w=new FiveTestWorld())
            {
                Quiet(w);var m=w.match;m.teamPlanningEnabled[0]=true;
                w.Place(1,new Vector2(-2,0),Vector2.left);w.Place(2,new Vector2(0,2.4f),Vector2.up);
                w.Place(3,new Vector2(0,-2.8f),Vector2.down);w.Place(4,new Vector2(-3.3f,0),Vector2.left);
                Ball(w,Vector2.zero);m.SelectCommand(Team.Red,FiveCommand.AllRecover,"exclude-reserve",true);
                // A fixed blocked observation isolates participant eligibility from ensuing physical separation.
                for(int i=0;i<290;i++)m.Tick(.01f);
                Assert.That(m.Events.Any(e=>e.kind=="contest-start"),Is.True);
                Assert.That(m.ReliefAction(4),Is.EqualTo(FiveReliefAction.None));
                Assert.That(Vector2.Distance(m.players[4].Target,m.players[4].Position),Is.LessThan(.01f));
                Assert.That(m.ReliefAction(0),Is.EqualTo(FiveReliefAction.None));
                Assert.That(m.ReliefAction(5),Is.EqualTo(FiveReliefAction.None));
                Assert.That(m.Events.Any(e=>e.kind.StartsWith("contest-")&&e.actor==4),Is.False);
                w.Place(2,new Vector2(-3.4f,0),Vector2.left);
                for(int i=0;i<25;i++)m.Tick(.01f);
                Assert.That(m.ReliefAction(2),Is.EqualTo(FiveReliefAction.None),"A new farthest field must leave an already active relief task.");
                Assert.That(Vector2.Distance(m.players[2].Target,m.players[2].Position),Is.LessThan(.01f));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator SlowlyRollingScrumIsDetectedEvenWithoutAnAbsoluteBallStop()
        {
            using(var w=new FiveTestWorld())
            {
                Quiet(w);var m=w.match;w.Step(150);
                // Replay a slow translating contact group; the old 0.5m stationarity sensor alone never reaches 0.65 seconds here.
                for(int i=0;i<100;i++)
                {
                    var point=new Vector2(0,-30+i*.016f);
                    w.Place(1,point+new Vector2(-2,0),Vector2.right);w.Place(6,point+new Vector2(2,0),Vector2.left);
                    Ball(w,point);m.ball.linearVelocity=new Vector3(0,0,1.6f);m.Tick(.01f);
                }
                Assert.That(m.Events.Any(e=>e.kind=="contest-start"),Is.True);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator OpenNearbyTeammateReceivesAnOrdinaryReliefPass()
        {
            using(var w=new FiveTestWorld())
            {
                Quiet(w);var m=w.match;w.Place(1,Vector2.zero,Vector2.right);w.Place(6,new Vector2(-1.7f,1),Vector2.right);
                w.Place(3,new Vector2(10,0),Vector2.left);w.Give(1);w.Step(270);
                Assert.That(m.Events.Any(e=>e.kind=="contest-outlet-pass"&&e.actor==1&&e.other==3),Is.True);
                Assert.That(m.Events.Any(e=>e.kind=="pass-kicked"&&e.actor==1&&e.other==3),Is.True);
                Assert.That(m.PassAttempts,Is.GreaterThan(0));Assert.That(m.Events.Any(e=>e.kind=="contest-end"&&e.reason=="pass-away"),Is.True);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator OpenSideExitMovesTheOwnedBallThroughTheFrontPocket()
        {
            using(var w=new FiveTestWorld())
            {
                Quiet(w);var m=w.match;w.Place(1,Vector2.zero,Vector2.right);w.Place(6,new Vector2(-1.7f,1),Vector2.right);w.Give(1);
                float lateral=0;w.Step(280,()=>lateral=Mathf.Max(lateral,Mathf.Abs(m.BallPosition.y)));
                Assert.That(m.Events.Any(e=>e.kind=="contest-side-carry"&&e.actor==1),Is.True);
                Assert.That(lateral,Is.GreaterThan(.5f));Assert.That(m.BoundaryCorrections,Is.Zero);
            }
            yield return null;
        }
        [Serializable] sealed class Sample
        {public string place;public int mirror;public bool enabled;public float movement,maxStall,firstEscapeAt=-1,contestedSlowSeconds;public int starts,backoffs,reangles,passes,carries,corrections;}
        [Serializable] sealed class Report {public List<Sample> samples=new List<Sample>();}
        [UnityTest] public IEnumerator RecordedEndWallScrumCanRegainFrontPossessionWithoutChangingOrders()
        {
            var report=new Report();
            foreach(var enabled in new[]{false,true})using(var w=new FiveTestWorld())
            {
                // Observed final positions/headings from contest-final-regression-03 seed10401.
                // Velocities were not captured; replay starts stationary, without claiming an exact time replay.
                var m=w.match;m.keepersEnabled=false;m.contestReliefEnabled=enabled;
                var positions=new[]{new Vector2(-59,9.17f),new Vector2(-60.20f,33.87f),new Vector2(-59.94f,34.73f),new Vector2(-38.21f,-4.65f),new Vector2(-60,30.31f),
                    new Vector2(59,9.17f),new Vector2(-61.06f,34.49f),new Vector2(-44.36f,-9),new Vector2(-59.97f,12),new Vector2(-59.97f,-12)};
                var headings=new[]{new Vector2(-.1f,1),new Vector2(-.62f,-.78f),new Vector2(-1,.02f),new Vector2(-.52f,.85f),new Vector2(-.45f,.9f),
                    new Vector2(-.98f,.19f),new Vector2(-.23f,-.97f),new Vector2(-.37f,.93f),new Vector2(-.07f,1),new Vector2(-.03f,1)};
                for(int i=0;i<10;i++)w.Place(i,positions[i],headings[i]);
                var origin=new Vector2(-61.37f,33.06f);Ball(w,origin);
                m.SelectCommand(Team.Red,FiveCommand.AllRecover,"recorded-wall",true);
                m.SelectCommand(Team.Navy,FiveCommand.FastTransition,"recorded-wall",true);
                var sample=new Sample{place="recorded-end-wall",enabled=enabled};
                w.Step(650,()=>{sample.movement=Mathf.Max(sample.movement,Vector2.Distance(m.BallPosition,origin));sample.maxStall=Mathf.Max(sample.maxStall,m.ObservedStallSeconds);});
                sample.starts=m.Events.Count(e=>e.kind=="contest-start");sample.backoffs=m.Events.Count(e=>e.kind=="contest-backoff");
                sample.reangles=m.Events.Count(e=>e.kind=="contest-reangle");sample.carries=m.Events.Count(e=>e.kind=="contest-side-carry");sample.corrections=m.BoundaryCorrections;
                report.samples.Add(sample);File.WriteAllText(FiveP7Evidence.DirectoryPath+"/contest-recorded-wall.json",JsonUtility.ToJson(report,true));
                if(enabled)
                {
                    File.WriteAllText(FiveP7Evidence.DirectoryPath+"/recorded-wall-events.json","["+string.Join(",",m.Events.Select(e=>JsonUtility.ToJson(e)))+"]");
                    // New movement tuning may free this historical setup before the relief timer matures.
                    // Require the helper only if a real sustained stall remains, plus the same physical escape below.
                    Assert.That(sample.starts>0||sample.maxStall<m.profile.contestConfirmSeconds,Is.True);
                    Assert.That(sample.movement,Is.GreaterThan(3));
                    Assert.That(m.Events.Any(e=>e.kind=="possession"),Is.True,"The actual front pocket must recover the ball.");
                    Assert.That(sample.corrections,Is.Zero);
                    Assert.That(m.Decision(Team.Red).command,Is.EqualTo(FiveCommand.AllRecover));
                    Assert.That(m.Decision(Team.Navy).command,Is.EqualTo(FiveCommand.FastTransition));
                }
                yield return null;
            }
        }
        [UnityTest] public IEnumerator PhysicalCrowdsAtCentreWallAndCornerHaveARealExit()
        {
            var report=new Report();
            foreach(var place in new[]{"centre","wall","corner"})foreach(var mirror in new[]{1,-1})foreach(var enabled in new[]{false,true})
            {
                using(var w=new FiveTestWorld())
                {
                    var point=place=="centre"?Vector2.zero:place=="wall"?new Vector2(0,-41.7f):new Vector2(60.8f,-40.9f);
                    Crowd(w,point,mirror);var m=w.match;m.contestReliefEnabled=enabled;
                    var sample=new Sample{place=place,mirror=mirror,enabled=enabled};
                    w.Step(650,()=>{
                        sample.movement=Mathf.Max(sample.movement,Vector2.Distance(m.BallPosition,point*mirror));sample.maxStall=Mathf.Max(sample.maxStall,m.ObservedStallSeconds);
                        if(sample.firstEscapeAt<0&&sample.movement>3)sample.firstEscapeAt=m.Elapsed;
                        if(m.BallVelocity.magnitude<=m.profile.contestSlowSpeed&&m.players.Count(p=>!p.IsKeeper&&Vector2.Distance(p.Position,m.BallPosition)<m.profile.contestRadius)>=2)
                            sample.contestedSlowSeconds+=.02f;
                    });
                    sample.starts=m.Events.Count(e=>e.kind=="contest-start");sample.backoffs=m.Events.Count(e=>e.kind=="contest-backoff");
                    sample.reangles=m.Events.Count(e=>e.kind=="contest-reangle");sample.passes=m.PassAttempts;sample.carries=m.Events.Count(e=>e.kind=="contest-side-carry");
                    sample.corrections=m.BoundaryCorrections;report.samples.Add(sample);
                    File.WriteAllText(FiveP7Evidence.DirectoryPath+"/contest-physical-crowds.json",JsonUtility.ToJson(report,true));
                }
                yield return null;
            }
            foreach(var sample in report.samples.Where(s=>s.enabled))
            {
                Assert.That(sample.movement,Is.GreaterThan(3),JsonUtility.ToJson(sample));
                Assert.That(sample.corrections,Is.Zero,JsonUtility.ToJson(sample));
            }
            Assert.That(report.samples.Where(s=>s.enabled).Sum(s=>s.starts),Is.GreaterThan(0));
            Assert.That(report.samples.Where(s=>s.enabled).Sum(s=>s.backoffs),Is.GreaterThan(0));
            Assert.That(report.samples.Where(s=>s.enabled).Sum(s=>s.reangles),Is.GreaterThan(0));
        }
    }
}
