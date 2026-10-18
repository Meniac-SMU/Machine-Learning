using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveWallPlayTests
    {
        [System.Serializable] sealed class CornerContact {public Vector3 position;public bool originalContains,fiveContains;public float deepest;public string collider;}
        [System.Serializable] sealed class CornerReport {public List<CornerContact> cases=new List<CornerContact>();}
        [UnityTest] public IEnumerator GoalMouthCornerBoundsAgreeWithOriginalPhysicalColliders()
        {
            var report=new CornerReport();
            using(var world=new FiveTestWorld())
            {
                var m=world.match;var sphere=m.ball.GetComponent<SphereCollider>();
                var points=new[]{new Vector2(61.49883f,9.85956f),new Vector2(61.57077f,9.83238f),new Vector2(61.52492f,9.73502f),new Vector2(61.8f,9.95f),new Vector2(62,15),new Vector2(61.8f,9.3f)};
                foreach(var point in points)foreach(var sx in new[]{-1,1})foreach(var sz in new[]{-1,1})
                {
                    var position=new Vector3(point.x*sx,m.ballSpawn.y,point.y*sz);
                    var row=new CornerContact{position=position,originalContains=m.geometry.ContainsBallPosition(position,m.ballRadius,.02f),fiveContains=m.BallInsidePlayableBounds(position)};
                    foreach(var collider in world.root.GetComponentsInChildren<Collider>())
                    {
                        if(collider.isTrigger||collider.attachedRigidbody!=null)continue;
                        if(Physics.ComputePenetration(sphere,position,m.ball.rotation,collider,collider.transform.position,collider.transform.rotation,out _,out var depth)&&depth>row.deepest)
                        {row.deepest=depth;row.collider=collider.name;}
                    }
                    report.cases.Add(row);
                    Assert.That(row.fiveContains,Is.EqualTo(row.deepest<=.02f),"Accept clear spherical corner space and reject real wall penetration: "+position);
                }
            }
            File.WriteAllText(FiveP7Evidence.DirectoryPath+"/g5-rule-scenarios-corner-contacts.json",JsonUtility.ToJson(report,true));
            yield return null;
        }
        [UnityTest] public IEnumerator DrivenBallStaysInsideAllWallsAndCorners()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;m.tacticsEnabled=false;m.keepersEnabled=false;
                for(int side=0;side<8;side++)
                {
                    m.ResetMatch();
                    for(int i=0;i<10;i++)world.Place(i,new Vector2(-40+i*8,25),Vector2.right);
                    var angle=side*Mathf.PI/4;var direction=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                    var start=new Vector2(direction.x*55,direction.y*35);
                    // Avoid the goal opening when testing the end wall.
                    if(Mathf.Abs(direction.y)<.1f)start.y=22;
                    world.Place(1,start,direction);world.Give(1);
                    var target=m.ClampField(start+direction*20);
                    m.players[1].SetTarget(target,target+direction*20);
                    world.Step(500);
                    Assert.That(m.BoundaryCorrections,Is.Zero,"Driven ball wall/corner "+side);
                    Assert.That(m.geometry.ContainsBallPosition(m.ball.position,m.ballRadius,.02f),Is.True);
                }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator FreePassSpeedReflectsBeforeEndWallPenetration()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;m.techniquesEnabled=false;
                var p=new Vector3(61.38f,m.ballSpawn.y,-20.67f);
                m.ball.transform.position=p;m.ball.position=p;m.ball.linearVelocity=new Vector3(9.05449f,0,-4.39660f);
                Physics.SyncTransforms();world.Step(3);
                Assert.That(m.AnticipatedWallBounces,Is.GreaterThan(0));
                Assert.That(m.ball.linearVelocity.x,Is.LessThan(0));Assert.That(m.BoundaryCorrections,Is.Zero);
                Assert.That(m.PassReceptions,Is.Zero);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator CongestedGoalpostRecoveryDoesNotNeedBoundaryCorrection()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;var own=FiveMatchupPlayTests.Arrange(world,7,9107,false,1.8f);var start=m.Elapsed;
                m.SelectCommand(own,FiveCommand.GoalBlock,"goalpost-contact-regression",true);
                m.SelectCommand(FiveMatch.Other(own),FiveCommand.FastTransition,"goalpost-attacker",true);
                while(m.Elapsed-start<12)world.Step(1);
                Assert.That(m.BoundaryCorrections,Is.Zero,"Congested physical contact at the goalpost must not escape the original pitch bounds.");
            }
            yield return null;
        }
    }
}
