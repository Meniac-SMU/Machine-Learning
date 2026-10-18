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
    public sealed class FiveAttackKeeperPlayTests
    {
        [Serializable] sealed class Row
        {
            public string scenario;
            public int team,carrier;
            public float speed,turnDegrees,forwardProgress,minimumLead,keeperDepth,keeperLateral;
            public float captureSeconds,travelDistance,ballStartDepth,ballRadius,playerRadius;
            public int recoveryEvents,goals;
        }
        [Serializable] sealed class Report {public string runtime=FiveMatch.PlayerRuntimeVersion;public List<Row> cases=new List<Row>();}
        [Serializable] sealed class SideSample {public float time,angle,distance;public int phase;public Vector2 keeper,ball,velocity,forward;}
        [Serializable] sealed class SideTrace {public float initialOverlap,turnClearance;public List<SideSample> samples=new List<SideSample>();}
        static void Save(string name,Report report)=>File.WriteAllText(FiveP7Evidence.DirectoryPath+"/"+name+".json",JsonUtility.ToJson(report,true));
        static void Isolate(FiveTestWorld w)
        {
            w.match.tacticsEnabled=false;w.match.keepersEnabled=false;w.match.contestReliefEnabled=false;
            for(int i=0;i<10;i++)w.Place(i,new Vector2(-45+i*9,32),Vector2.right);
        }
        [UnityTest] public IEnumerator KeeperCollectsSideContactWithoutCirclingIntoOwnGoal()
        {
            var report=new Report();
            foreach(var team in new[]{Team.Red,Team.Navy})foreach(int side in new[]{-1,1})
            foreach(bool goalward in new[]{false,true})
            using(var w=new FiveTestWorld())
            {
                Isolate(w);var m=w.match;var sign=FiveMatch.AttackSign(team);var keeper=m.players[(int)team*5];
                var contact=keeper.radius+m.ballRadius+.08f;
                var position=new Vector2((-m.geometry.HalfLength+(goalward?contact+m.ballRadius+.5f:5))*sign,0);
                var forward=goalward?Vector2.up*side:Vector2.right*sign;
                w.Place(keeper.Index,position,forward);
                foreach(var p in m.players)if(p!=keeper)p.Body.constraints=RigidbodyConstraints.FreezeAll;
                var offset=(goalward?-Vector2.right*sign:Vector2.up*side)*contact;
                var ballStart=position+offset;m.ball.position=new Vector3(ballStart.x,m.ballSpawn.y,ballStart.y);
                Assert.That(ballStart.x*sign+m.geometry.HalfLength,Is.GreaterThan(m.ballRadius+.15f),"The fixture must start completely outside the goal.");
                m.ball.linearVelocity=Vector3.zero;Physics.SyncTransforms();m.keepersEnabled=true;
                keeper.KickReadyAt=100;float path=0;var previous=keeper.Position;float capturedAt=-1;
                var trace=new SideTrace{turnClearance=keeper.BallRotationClearance(m.ballRadius)};
                foreach(var collider in keeper.GetComponentsInChildren<Collider>())
                    if(!collider.isTrigger&&Physics.ComputePenetration(m.ball.GetComponent<SphereCollider>(),m.ball.position,m.ball.rotation,
                        collider,collider.transform.position,collider.transform.rotation,out _,out var overlap))trace.initialOverlap=Mathf.Max(trace.initialOverlap,overlap);
                for(int step=0;step<200;step++)
                {
                    w.Step(1);path+=Vector2.Distance(previous,keeper.Position);previous=keeper.Position;
                    trace.samples.Add(new SideSample{time=m.Elapsed,keeper=keeper.Position,ball=m.BallPosition,velocity=m.BallVelocity,
                        forward=keeper.Forward,angle=Vector2.Angle(keeper.Forward,m.BallPosition-keeper.Position),
                        distance=Vector2.Distance(keeper.Position,m.BallPosition),phase=m.Events.LastOrDefault(e=>e.kind=="keeper-side-recovery-phase")?.id??0});
                    if(m.Owner==keeper.Index){capturedAt=m.Elapsed;break;}
                    if(m.RedScore+m.NavyScore>0)break;
                }
                File.WriteAllText(FiveP7Evidence.DirectoryPath+$"/side-trace-{team}-{side}-{goalward}.json",JsonUtility.ToJson(trace,true));
                report.cases.Add(new Row{scenario="side-contact-goalward-"+goalward+"-side-"+side,
                    team=(int)team,captureSeconds=capturedAt,travelDistance=path,ballStartDepth=ballStart.x*sign+m.geometry.HalfLength,
                    ballRadius=m.ballRadius,playerRadius=keeper.radius,goals=m.RedScore+m.NavyScore,
                    recoveryEvents=m.Events.Count(e=>e.kind=="keeper-side-recovery"&&e.actor==keeper.Index)});Save("keeper-side-contact",report);
                Assert.That(m.RedScore+m.NavyScore,Is.Zero,"Side correction must not push the ball into the own goal.");
                Assert.That(capturedAt,Is.GreaterThan(0),"A nearby side ball must reach the front pocket within four seconds.");
                Assert.That(path,Is.LessThan(3),"Use a short adjustment, not a wide orbit.");
            }
            yield return null;
        }
        [UnityTest] public IEnumerator RoleAndPossessionSpeedCapsAreAppliedInPhysicsOnBothTeams()
        {
            var report=new Report();
            foreach(var team in new[]{Team.Red,Team.Navy})foreach(bool keeper in new[]{false,true})foreach(bool owned in new[]{false,true})
            using(var w=new FiveTestWorld())
            {
                Isolate(w);var m=w.match;var sign=FiveMatch.AttackSign(team);var index=(int)team*5+(keeper?0:1);
                m.techniquesEnabled=false;m.ball.position=new Vector3(0,m.ballSpawn.y,-30);w.Place(index,Vector2.zero,Vector2.right*sign);var p=m.players[index];
                p.Dribbling=owned;if(!keeper)p.Stamina.SetForScenario(1);p.SetTarget(Vector2.right*40*sign,Vector2.right*40*sign);
                w.Step(70);var speed=p.Velocity.magnitude;
                // Sample after the .01 s physics substep: PhysX applies this body's damping after Drive.
                var expected=9*(keeper?1.2f:1)*(owned?.9f:1)*(1-p.Body.linearDamping*.01f);
                report.cases.Add(new Row{scenario="speed-keeper-"+keeper+"-owned-"+owned,team=(int)team,speed=speed});Save("role-speeds",report);
                Assert.That(speed,Is.EqualTo(expected).Within(.08f));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator PossessionUsesSixtyFourPercentBaseRotationForNormalAndEvasiveMovement()
        {
            var report=new Report();
            foreach(var team in new[]{Team.Red,Team.Navy})foreach(bool owned in new[]{false,true})foreach(bool evade in new[]{false,true})
            using(var w=new FiveTestWorld())
            {
                Isolate(w);var m=w.match;m.techniquesEnabled=false;m.ball.position=new Vector3(0,m.ballSpawn.y,-30);var sign=FiveMatch.AttackSign(team);var index=(int)team*5+1;
                w.Place(index,Vector2.zero,Vector2.right*sign);var p=m.players[index];p.Dribbling=owned;p.Evading=evade;
                p.SetTarget(p.Position,Vector2.up*20*sign,true);w.Step(10);
                var turned=Vector2.Angle(Vector2.right*sign,p.Forward);var expected=125*(owned?.64f:1)*(evade?2:1)*.2f;
                report.cases.Add(new Row{scenario="turn-owned-"+owned+"-evade-"+evade,team=(int)team,turnDegrees=turned});Save("owned-turn-rates",report);
                Assert.That(turned,Is.EqualTo(expected).Within(.15f));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator FastTransitionSendsTwoRunnersAheadForEveryFieldCarrierAndBothManagerInputs()
        {
            var report=new Report();
            foreach(var team in new[]{Team.Red,Team.Navy})foreach(bool learning in new[]{false,true})foreach(int slot in new[]{1,2,3,4})
            using(var w=new FiveTestWorld())
            {
                Isolate(w);var m=w.match;var sign=FiveMatch.AttackSign(team);var owner=m.players[(int)team*5+slot];
                var supports=m.players.Where(p=>p.team==team&&!p.IsKeeper&&p!=owner).OrderByDescending(p=>p.slot>=3).ThenBy(p=>p.slot).ToArray();
                w.Place(owner.Index,new Vector2(-12*sign,0),Vector2.right*sign);
                for(int i=0;i<supports.Length;i++)w.Place(supports[i].Index,new Vector2(i<2?-13:-30,i==0?-11:11)*sign,Vector2.right*sign);
                w.Give(owner.Index);owner.KickReadyAt=m.Elapsed+10;
                m.tacticsEnabled=true;m.teamPlanningEnabled[(int)FiveMatch.Other(team)]=false;
                if(learning)Assert.That(FiveLearningContract.Apply(m,team,0),Is.True);
                else m.SelectCommand(team,FiveCommand.FastTransition,"fast-support-test",true);
                var decision=m.Decision(team);var revision=decision.revision;var next=decision.nextDecisionAt;
                var start=owner.Position;w.Step(200);
                var lead=supports.Take(2).Min(p=>(p.Position.x-owner.Position.x)*sign);
                report.cases.Add(new Row{scenario="fast-support-learning-"+learning,team=(int)team,carrier=slot,
                    forwardProgress=(owner.Position.x-start.x)*sign,minimumLead=lead});Save("fast-support",report);
                Assert.That(m.Owner,Is.EqualTo(owner.Index));Assert.That(lead,Is.GreaterThan(1));
                Assert.That(supports.Take(2).All(p=>p.Position.x*sign>0),Is.True);
                Assert.That((supports[2].Target.x-owner.Position.x)*sign,Is.LessThan(-10));
                Assert.That(decision.command,Is.EqualTo(FiveCommand.FastTransition));Assert.That(decision.revision,Is.EqualTo(revision));Assert.That(decision.nextDecisionAt,Is.EqualTo(next));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator KeepersReachLooseBallsAcrossThePenaltyAreaAndStayWithinItsTargets()
        {
            var report=new Report();
            foreach(var team in new[]{Team.Red,Team.Navy})foreach(int side in new[]{-1,1})
            using(var w=new FiveTestWorld())
            {
                Isolate(w);var m=w.match;var sign=FiveMatch.AttackSign(team);var keeper=m.players[(int)team*5];
                w.Place(keeper.Index,new Vector2((-m.geometry.HalfLength+3)*sign,0),Vector2.right*sign);
                var target=new Vector2((-m.geometry.HalfLength+16)*sign,side*18);
                m.ball.position=new Vector3(target.x,m.ballSpawn.y,target.y);m.ball.linearVelocity=Vector3.zero;Physics.SyncTransforms();
                Assert.That(Vector2.Distance(m.KeeperTarget(keeper),target),Is.LessThan(.01f));
                m.keepersEnabled=true;keeper.KickReadyAt=m.Elapsed+10;
                var row=new Row{scenario="keeper-area-side-"+side,team=(int)team};
                w.Step(240,()=>{
                    row.keeperDepth=Mathf.Max(row.keeperDepth,keeper.Position.x*sign+m.geometry.HalfLength);
                    row.keeperLateral=Mathf.Max(row.keeperLateral,Mathf.Abs(keeper.Position.y));
                    Assert.That(m.InsideKeeperArea(team,keeper.Target),Is.True);
                    Assert.That(m.InsideKeeperArea(team,keeper.Position),Is.True);
                });
                report.cases.Add(row);Save("keeper-area",report);
                Assert.That(row.keeperDepth,Is.GreaterThan(14));Assert.That(row.keeperLateral,Is.GreaterThan(16));
                Assert.That(m.Events.Any(e=>e.kind=="possession"&&e.actor==keeper.Index),Is.True);
                foreach(var outside in new[]{new Vector2(-40*sign,0),new Vector2(-50*sign,25),new Vector2(-70*sign,-30)})
                {
                    Assert.That(m.InsideKeeperArea(team,outside),Is.False);
                    Assert.That(m.InsideKeeperArea(team,m.ClampKeeperTarget(keeper,outside)),Is.True);
                }
            }
            yield return null;
        }
    }
}
