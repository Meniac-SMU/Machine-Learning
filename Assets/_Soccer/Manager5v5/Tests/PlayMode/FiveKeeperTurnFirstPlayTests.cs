using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveKeeperTurnFirstPlayTests
    {
        [Serializable] public sealed class Case
        {
            public int team,side,ownGoals,slowSideContactConceded;
            public bool goalward,captured;
            public float angle,gap,initialSpeed,initialOverlap,captureSeconds=-1,travel,ballGoalwardDisplacement,unalignedApproach;
        }
        [Serializable] public sealed class Report
        {
            public string runtime=FiveMatch.PlayerRuntimeVersion;
            public bool keeperTurnFirst;
            public List<Case> cases=new List<Case>();
        }
        [UnityTest] public IEnumerator SideCollectionMatrixRecordsPhysicalOutcomes()
            => CollectionMatrix(true);
        [UnityTest] public IEnumerator LegacyControlReproducesOriginalSideCollectionFailures()
            => CollectionMatrix(false);
        IEnumerator CollectionMatrix(bool turnFirst)
        {
            var report=new Report{keeperTurnFirst=turnFirst};
            foreach(var team in new[]{Team.Red,Team.Navy})foreach(int side in new[]{-1,1})
            foreach(bool goalward in new[]{false,true})foreach(float angle in new[]{90f,135f})
            foreach(float gap in new[]{.1f,.8f,2f})foreach(float initialSpeed in new[]{0f,4f})
            {
                using(var w=new FiveTestWorld())
                {
                    var m=w.match;m.tacticsEnabled=false;m.keepersEnabled=false;m.contestReliefEnabled=false;
                    m.keeperTurnFirstEnabled=turnFirst;
                    for(int i=0;i<10;i++)w.Place(i,new Vector2(-45+i*9,32),Vector2.right);
                    var keeper=m.players[(int)team*5];float sign=FiveMatch.AttackSign(team);
                    foreach(var p in m.players)if(p!=keeper)p.Body.constraints=RigidbodyConstraints.FreezeAll;
                    var distance=keeper.BallRotationClearance(m.ballRadius)+gap;
                    var direction=goalward?-Vector2.right*sign:Vector2.up*side;
                    var position=new Vector2((-m.geometry.HalfLength+(goalward?distance+m.ballRadius+.5f:7))*sign,0);
                    float radians=angle*side*Mathf.Deg2Rad;
                    var forward=new Vector2(direction.x*Mathf.Cos(radians)-direction.y*Mathf.Sin(radians),
                        direction.x*Mathf.Sin(radians)+direction.y*Mathf.Cos(radians));
                    w.Place(keeper.Index,position,forward);
                    keeper.Body.linearVelocity=new Vector3(direction.x,0,direction.y)*initialSpeed;
                    var ballStart=position+direction*distance;m.ball.position=new Vector3(ballStart.x,m.ballSpawn.y,ballStart.y);
                    m.ball.linearVelocity=Vector3.zero;keeper.KickReadyAt=100;Physics.SyncTransforms();
                    var row=new Case{team=(int)team,side=side,goalward=goalward,angle=angle,gap=gap,initialSpeed=initialSpeed};
                    foreach(var c in keeper.GetComponentsInChildren<Collider>())
                        if(!c.isTrigger&&Physics.ComputePenetration(m.ball.GetComponent<SphereCollider>(),m.ball.position,m.ball.rotation,
                            c,c.transform.position,c.transform.rotation,out _,out float depth))row.initialOverlap=Mathf.Max(row.initialOverlap,depth);
                    Assert.That(row.initialOverlap,Is.LessThan(.001f),"No initial physical overlap is allowed.");
                    Assert.That(ballStart.x*sign+m.geometry.HalfLength,Is.GreaterThan(m.ballRadius+.15f));
                    m.keepersEnabled=true;
                    for(int step=0;step<200;step++)
                    {
                        var previous=keeper.Position;var delta=m.BallPosition-previous;
                        var beforeAngle=Vector2.Angle(keeper.Forward,delta);
                        w.Step(1);var motion=keeper.Position-previous;row.travel+=motion.magnitude;
                        if(beforeAngle>20)row.unalignedApproach+=Mathf.Max(0,Vector2.Dot(motion,delta.normalized));
                        if(m.Owner==keeper.Index){row.captured=true;row.captureSeconds=m.Elapsed;break;}
                        if(m.RedScore+m.NavyScore>0)break;
                    }
                    row.ownGoals=team==Team.Red?m.NavyScore:m.RedScore;
                    row.slowSideContactConceded=m.Events.FindAll(e=>e.kind=="keeper-slow-side-contact-conceded").Count;
                    row.ballGoalwardDisplacement=-(m.BallPosition.x-ballStart.x)*sign;
                    report.cases.Add(row);
                }
                yield return null;
            }
            File.WriteAllText(FiveP7Evidence.DirectoryPath+(turnFirst?"/keeper-turn-matrix.json":"/keeper-legacy-matrix.json"),JsonUtility.ToJson(report,true));
            Assert.That(report.cases.Count,Is.EqualTo(96));
            if(turnFirst)
                Assert.That(report.cases.TrueForAll(c=>c.ownGoals==0 && c.captured),Is.True,
                    "Every valid side-ball fixture must be physically collected without an own goal.");
            else
            {
                Assert.That(report.cases.FindAll(c=>c.ownGoals==1).Count,Is.EqualTo(8));
                Assert.That(report.cases.FindAll(c=>c.captured).Count,Is.EqualTo(88));
            }
        }
        [UnityTest] public IEnumerator KeeperBrakesThenTurnsBeforeApproachingWithItsFront()
        {
            using(var w=new FiveTestWorld())
            {
                var m=w.match;m.techniquesEnabled=false;m.tacticsEnabled=false;
                w.Place(0,new Vector2(-40,0),Vector2.up);
                m.ball.position=new Vector3(0,m.ballSpawn.y,20);
                var p=m.players[0];var start=p.Position;var facing=p.Forward;
                p.Body.linearVelocity=new Vector3(4,0,0);
                p.SetTarget(start+Vector2.right*5,start+Vector2.right*5);p.RequireKeeperFrontApproach();
                w.Step(1);
                Assert.That(p.Velocity.magnitude,Is.LessThan(4));
                Assert.That(Vector2.Angle(facing,p.Forward),Is.LessThan(.1f),"Brake before rotating.");
                bool rotated=false,advanced=false;
                for(int i=0;i<200;i++)
                {
                    var before=p.Position;var angle=Vector2.Angle(p.Forward,Vector2.right);
                    m.SimulatePhysicsStep(.01f);
                    if(angle>6 && p.Velocity.magnitude<.03f)rotated=true;
                    if(rotated && angle>6)
                        Assert.That(Vector2.Distance(before,p.Position),Is.LessThan(.001f),"Rotate at rest.");
                    if(rotated && angle<=6 && p.Velocity.magnitude>.1f)
                    {
                        Assert.That(Vector2.Angle(p.Forward,p.Velocity),Is.LessThan(6),"Approach with the plate facing forward.");
                        advanced=true;break;
                    }
                }
                Assert.That(rotated&&advanced,Is.True);
                p.SetTarget(p.Position,p.Position+Vector2.up);
                Assert.That(p.KeeperFrontApproach,Is.False,"A new plan clears the collection state.");
                w.Place(1,new Vector2(-20,0),Vector2.up);
                var field=m.players[1];field.SetTarget(field.Position+Vector2.right*5,field.Position+Vector2.right*5);
                field.RequireKeeperFrontApproach();w.Step(1);
                Assert.That(field.KeeperFrontApproach,Is.False);
                Assert.That(field.Velocity.x,Is.GreaterThan(.1f),"Field movement must remain unchanged.");
            }
            yield return null;
        }
    }
}
