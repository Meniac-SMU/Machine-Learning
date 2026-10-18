using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveRobotIntegrationPlayTests
    {
        [UnityTearDown] public IEnumerator Cleanup(){FiveTestWorld.DisposeOutstanding();yield return null;}
        static void Isolate(FiveTestWorld w)
        {
            w.match.tacticsEnabled=false;w.match.keepersEnabled=false;w.match.contestReliefEnabled=false;
            for(int i=0;i<10;i++)w.Place(i,new Vector2(-45+i*9,32),Vector2.right);
        }
        [UnityTest] public IEnumerator AllTenPlayersHaveFourFittedBoxesAndOriginalCubeSize()
        {
            using(var w=new FiveTestWorld())
            {
                foreach(var p in w.match.players)
                {
                    var boxes=p.GetComponentsInChildren<BoxCollider>();
                    Assert.That(boxes.Length,Is.EqualTo(4));Assert.That(p.GetComponentsInChildren<Collider>().Length,Is.EqualTo(4));
                    Assert.That(boxes.All(b=>!b.isTrigger),Is.True);
                    var body=boxes.Single(b=>b.name=="BodyCollider");
                    Assert.That(Vector3.Distance(Vector3.Scale(body.size,p.transform.lossyScale),Vector3.one*.9f),Is.LessThan(.0001f));
                    Assert.That(boxes.Count(b=>b.name.StartsWith("Wheel")),Is.EqualTo(2));
                    var plate=boxes.Single(b=>b.name=="KickPlateCenter");
                    Assert.That(plate.bounds.min.y,Is.EqualTo(0).Within(.0001f));
                    foreach(var wheel in boxes.Where(b=>b.name.StartsWith("Wheel")))Assert.That(wheel.bounds.min.y,Is.EqualTo(0).Within(.0001f));
                    var renders=p.GetComponentsInChildren<MeshRenderer>();Assert.That(renders.Length,Is.EqualTo(1));
                    Assert.That(renders[0].sharedMaterials.Length,Is.EqualTo(2));
                    Assert.That(renders[0].sharedMaterials.All(m=>m!=null&&m.shader.isSupported),Is.True);
                    Assert.That(renders[0].sharedMaterials[1].shader.name,Does.Contain("Unlit"));
                    var filter=renders[0].GetComponent<MeshFilter>();
                    var eyes=filter.transform.TransformPoint(filter.sharedMesh.GetSubMesh(1).bounds.center);
                    Assert.That(Vector3.Dot(eyes-p.transform.position,p.transform.forward),Is.GreaterThan(.4f),"Eyes and kick plate face gameplay forward.");
                }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator AllVariantsAcquireAndHoldAtTheirNewPlateContact()
        {
            foreach(int index in new[]{0,1,5,6})using(var w=new FiveTestWorld())
            {
                Isolate(w);w.Place(index,Vector2.zero,index<5?Vector2.right:Vector2.left);w.Give(index);
                var m=w.match;var p=m.players[index];Assert.That(m.Owner,Is.EqualTo(index),"initial acquisition "+index);
                var plate=p.GetComponentsInChildren<BoxCollider>().Single(b=>b.name=="KickPlateCenter");
                float maxGap=0;
                for(int i=0;i<100;i++)
                {
                    w.Step(1);Assert.That(m.Owner,Is.EqualTo(index));
                    float gap=Vector3.Distance(plate.ClosestPoint(m.ball.position),m.ball.position)-m.ballRadius;
                    maxGap=Mathf.Max(maxGap,Mathf.Abs(gap));
                }
                Assert.That(maxGap,Is.LessThan(.045f),"ball/plate gap "+index);
                var side=p.transform.position+p.transform.right*1.2f;
                Assert.That(p.InsideKickPlate(side,m.ballRadius,m.profile.ballContactOffset),Is.False);
                Assert.That(p.InsideKickPlate(p.transform.position-p.transform.forward,m.ballRadius,m.profile.ballContactOffset),Is.False);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator BothTeamsDribbleMovingAndShootClearOfTheNewColliders()
        {
            foreach(int index in new[]{1,6})using(var w=new FiveTestWorld())
            {
                Isolate(w);var forward=index<5?Vector2.right:Vector2.left;w.Place(index,Vector2.zero,forward);w.Give(index);
                var m=w.match;var p=m.players[index];int held=0;
                p.SetTarget(forward*10,forward*15);
                for(int i=0;i<60;i++){p.SetManualDrive(.25f,0);w.Step(1);if(m.Owner==index)held++;}
                Assert.That(held,Is.GreaterThanOrEqualTo(57));
                Assert.That(p.Position.magnitude,Is.GreaterThan(.5f));
                Assert.That(m.RequestShot(p,p.Position+forward*40),Is.True);
                w.Step(20);
                Assert.That(m.Shots,Is.EqualTo(1));Assert.That(m.Owner,Is.EqualTo(-1));
                Assert.That(Vector2.Dot(m.BallVelocity,forward),Is.GreaterThan(15));
                Assert.That(Vector2.Distance(m.BallPosition,p.Position),Is.GreaterThan(2));
            }
            yield return null;
        }
    }
}
