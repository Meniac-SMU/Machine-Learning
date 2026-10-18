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
    public sealed class FiveSoftReceptionPlayTests
    {
        [UnityTearDown] public IEnumerator Cleanup(){FiveTestWorld.DisposeOutstanding();yield return null;}
        static void Isolate(FiveTestWorld w)
        {
            w.match.tacticsEnabled=w.match.keepersEnabled=false;w.match.contestReliefEnabled=false;
            for(int i=0;i<10;i++)w.Place(i,new Vector2(-45+i*9,32),Vector2.right);
        }
        [Serializable] sealed class DribbleReport
        {
            public string runtime;
            public float holdOffset,ballRadius,ballHeight,plateFace,plateBottom,plateTop,restGap,meanGap,maxGap,meanDistance;
            public int ownedSamples,samples;
            public Vector3 visualExtents,visualCenter,colliderExtents;
        }
        static void Ball(FiveMatch m,Vector2 point,Vector2 velocity)
        {
            var pos=new Vector3(point.x,m.ballSpawn.y,point.y);
            m.ball.position=pos;m.ball.transform.position=pos;
            m.ball.linearVelocity=new Vector3(velocity.x,0,velocity.y);m.ball.angularVelocity=Vector3.zero;Physics.SyncTransforms();
        }
        [Serializable] sealed class CatchCase {public int receiver;public float speed,angle,relativeAfterCatch;public bool caught,stable;public int cushions;}
        [Serializable] sealed class CatchReport {public List<CatchCase> cases=new List<CatchCase>();}
        [UnityTest] public IEnumerator FastPassesAndShotsAreCushionedByFieldsAndKeepersFromBothSides()
        {
            var report=new CatchReport();
            foreach(int index in new[]{0,1,5,6})foreach(float speed in new[]{12f,36.666667f})foreach(float angle in new[]{0f,-15f,15f})
            {
                using(var w=new FiveTestWorld())
                {
                    var m=w.match;Isolate(w);var forward=index<5?Vector2.right:Vector2.left;w.Place(index,Vector2.zero,forward);
                    var direction=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad)*forward.x,Mathf.Sin(angle*Mathf.Deg2Rad));
                    Ball(m,direction*6,-direction*speed);var row=new CatchCase{receiver=index,speed=speed,angle=angle};
                    for(int i=0;i<100&&m.Owner!=index;i++)w.Step(1);
                    row.caught=m.Owner==index;row.relativeAfterCatch=(m.BallVelocity-m.players[index].Velocity).magnitude;
                    w.Step(20);row.stable=m.Owner==index;row.cushions=m.Events.Count(e=>e.kind=="front-cushion"&&e.actor==index);
                    report.cases.Add(row);
                    File.WriteAllText(FiveP7Evidence.DirectoryPath+"/front-catches.json",JsonUtility.ToJson(report,true));
                    Assert.That(row.caught&&row.stable,Is.True,$"receiver={index} speed={speed} angle={angle}");
                    Assert.That(row.cushions,Is.GreaterThan(0));Assert.That(row.relativeAfterCatch,Is.LessThan(3));
                    Assert.That(m.BoundaryCorrections,Is.Zero);
                }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator SideRearMissAndOutgoingBallReceiveNoFrontCushion()
        {
            using(var w=new FiveTestWorld())
            {
                var m=w.match;
                foreach(var sample in new[]{(new Vector2(0,2),new Vector2(0,-30)),(new Vector2(-2,0),new Vector2(30,0)),
                    (new Vector2(2,2),new Vector2(-30,0)),(new Vector2(2,0),new Vector2(30,0))})
                {
                    m.ResetMatch();Isolate(w);w.Place(1,Vector2.zero,Vector2.right);Ball(m,sample.Item1,sample.Item2);
                    m.Tick(.01f);
                    Assert.That(m.Events.Any(e=>e.kind=="front-cushion"),Is.False);
                    Assert.That(m.Owner,Is.EqualTo(-1));
                }
                m.ResetMatch();Isolate(w);w.Place(1,Vector2.zero,Vector2.right);w.Give(1);
                Assert.That(m.RequestShot(m.players[1],new Vector2(50,0)),Is.True);w.Step(6);
                Assert.That(m.Shots,Is.EqualTo(1));Assert.That(m.Owner,Is.EqualTo(-1));
                Assert.That(m.BallVelocity.x,Is.GreaterThan(20),"A launched ball must not be immediately caught by its kicker.");
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ActualPassHandshakeHasAStableSoftReception()
        {
            using(var w=new FiveTestWorld())
            {
                var m=w.match;Isolate(w);w.Place(1,new Vector2(-9,0),Vector2.right);w.Place(3,new Vector2(9,0),Vector2.left);w.Give(1);
                Assert.That(m.RequestPass(m.players[1],m.players[3]),Is.True);
                w.Step(250);
                Assert.That(m.PassAttempts,Is.EqualTo(1));Assert.That(m.PassReceptions,Is.EqualTo(1));
                Assert.That(m.Owner,Is.EqualTo(3));Assert.That(m.Events.Any(e=>e.kind=="front-cushion"&&e.actor==3),Is.True);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator RecoveryUsesTheOriginalDecisionPeriodAndKeepsAllLearningActions()
        {
            using(var w=new FiveTestWorld())
            {
                var m=w.match;Isolate(w);m.techniquesEnabled=false;m.ruleVariants=new[]{FiveRuleVariant.Strong,FiveRuleVariant.Strong};
                for(int i=1;i<=4;i++)w.Place(i,new Vector2(-5-i,i-2),Vector2.right);Ball(m,Vector2.zero,Vector2.zero);
                Assert.That(m.ChooseRule(Team.Red,out _),Is.EqualTo(FiveCommand.AllRecover));
                Assert.That(FiveLearningContract.Apply(m,Team.Red,6),Is.True);
                m.Tick(3);Assert.That(m.ChooseRule(Team.Red,out _),Is.EqualTo(FiveCommand.AllRecover));
                Assert.That(m.SelectCommand(Team.Red,FiveCommand.AllRecover,"rule-interval"),Is.True);
                m.techniquesEnabled=true;w.Place(8,new Vector2(2,0),Vector2.left);w.Give(8);
                m.ChooseRule(Team.Red,out _);
                CollectionAssert.Contains(m.RuleDiagnostic(Team.Red).forecastActions,6);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator AccelerationBrakingAndShallowSteeringKeepABallCloseAndRolling()
        {
            using(var w=new FiveTestWorld())
            {
                var m=w.match;Isolate(w);w.Place(1,Vector2.zero,Vector2.right);w.Give(1);var p=m.players[1];
                var plates=p.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("KickPlate")).ToArray();
                var maximumGap=0f;var rolled=false;
                for(int i=0;i<200;i++)
                {
                    var heading=i<80?Vector2.right:new Vector2(1,.15f).normalized;
                    var target=i>=140?p.Position:p.Position+heading*10;p.SetTarget(target,p.Position+heading*10);
                    w.Step(1);Assert.That(m.Owner,Is.EqualTo(1));
                    maximumGap=Mathf.Max(maximumGap,plates.Min(c=>Vector3.Distance(c.ClosestPoint(m.ball.position),m.ball.position))-m.ballRadius);
                    rolled|=m.ball.angularVelocity.magnitude>5;
                    Assert.That(m.ball.position.y,Is.EqualTo(m.ballSpawn.y).Within(.001));
                }
                Assert.That(maximumGap,Is.LessThan(.12f),"The carried ball must remain within 12cm of a plate through changes of speed.");
                Assert.That(rolled,Is.True);Assert.That(m.BallVelocity.magnitude,Is.LessThan(.5f));
                File.WriteAllText(FiveP7Evidence.DirectoryPath+"/dynamic-dribble-gap.json",JsonUtility.ToJson(new DribbleReport{runtime=FiveMatch.PlayerRuntimeVersion,maxGap=maximumGap},true));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator MeasureActualPlateGapDuringDribble()
        {
            using(var w=new FiveTestWorld())
            {
                var m=w.match;Isolate(w);w.Place(1,Vector2.zero,Vector2.right);w.Give(1);var p=m.players[1];
                var plates=p.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("KickPlate")).ToArray();
                var center=plates.First(c=>c.name=="KickPlateCenter");
                Func<float> gap=()=>plates.Min(c=>Vector3.Distance(c.ClosestPoint(m.ball.position),m.ball.position))-m.ballRadius;
                var renderer=m.ball.GetComponentInChildren<Renderer>();
                var report=new DribbleReport {runtime=FiveMatch.PlayerRuntimeVersion,holdOffset=m.profile.holdOffset,ballRadius=m.ballRadius,
                    visualExtents=renderer.bounds.extents,visualCenter=renderer.bounds.center-m.ball.position,colliderExtents=m.ball.GetComponent<SphereCollider>().bounds.extents,
                    ballHeight=m.ball.position.y,plateFace=center.bounds.max.x-p.Position.x,plateBottom=center.bounds.min.y,plateTop=center.bounds.max.y,restGap=gap()};
                for(int i=0;i<180;i++)
                {
                    p.SetTarget(new Vector2(45,0),new Vector2(45,0));w.Step(1);
                    if(i<40)continue;
                    report.samples++;if(m.Owner==1)report.ownedSamples++;
                    report.meanGap+=gap();report.maxGap=Mathf.Max(report.maxGap,gap());
                    report.meanDistance+=Vector2.Distance(m.BallPosition,p.Position);
                }
                report.meanGap/=report.samples;report.meanDistance/=report.samples;
                Directory.CreateDirectory(FiveP7Evidence.DirectoryPath);
                File.WriteAllText(FiveP7Evidence.DirectoryPath+"/dribble-plate-gap.json",JsonUtility.ToJson(report,true));
                Assert.That(report.ownedSamples,Is.EqualTo(report.samples));
            }
            yield return null;
        }
    }
}
