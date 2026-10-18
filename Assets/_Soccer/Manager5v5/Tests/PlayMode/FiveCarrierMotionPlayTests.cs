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
    public sealed class FiveCarrierMotionPlayTests
    {
        [Serializable] sealed class Sample
        {
            public string scenario;
            public int team,command,ownerAtEnd,releases,passes;
            public float ownedSeconds,stoppedOwnedSeconds,travel,displacement,forwardProgress,lateral;
        }
        [Serializable] sealed class Report {public string runtime=FiveMatch.PlayerRuntimeVersion;public List<Sample> cases=new List<Sample>();}
        static void Isolate(FiveTestWorld w)
        {
            w.match.tacticsEnabled=false;w.match.keepersEnabled=false;w.match.contestReliefEnabled=false;
            for(int i=0;i<10;i++)w.Place(i,new Vector2(-45+i*9,32),Vector2.right);
        }
        static Sample Measure(FiveTestWorld w,int owner,string name,int command,int steps,Action<int> plan=null)
        {
            var m=w.match;var p=m.players[owner];var start=p.Position;var previous=start;
            var row=new Sample{scenario=name,team=(int)p.team,command=command};
            for(int i=0;i<steps;i++)
            {
                plan?.Invoke(i);w.Step(1);
                row.travel+=Vector2.Distance(p.Position,previous);previous=p.Position;
                if(m.Owner==owner){row.ownedSeconds+=.02f;if(p.Velocity.magnitude<1)row.stoppedOwnedSeconds+=.02f;}
            }
            row.displacement=Vector2.Distance(start,p.Position);row.forwardProgress=(p.Position.x-start.x)*FiveMatch.AttackSign(p.team);
            row.lateral=Mathf.Abs(p.Position.y-start.y);row.ownerAtEnd=m.Owner;
            row.releases=m.Events.Count(e=>e.kind=="release"&&e.reason=="outside-kick-plate");row.passes=m.PassAttempts;
            return row;
        }
        static void Save(string name,Report r)=>File.WriteAllText(FiveP7Evidence.DirectoryPath+"/"+name+".json",JsonUtility.ToJson(r,true));
        [UnityTest] public IEnumerator ControlledTurnsRetainTheFrontPocketWhileStationaryAndMoving()
        {
            var report=new Report();
            foreach(var team in new[]{Team.Red,Team.Navy})foreach(bool moving in new[]{false,true})foreach(float angle in new[]{-90f,90f,180f})
            using(var w=new FiveTestWorld())
            {
                Isolate(w);var sign=FiveMatch.AttackSign(team);int index=(int)team*5+1;
                w.Place(index,Vector2.zero,Vector2.right*sign);w.Give(index);var m=w.match;var p=m.players[index];
                var r=angle*Mathf.Deg2Rad;var direction=new Vector2(Mathf.Cos(r),Mathf.Sin(r))*sign;
                var row=Measure(w,index,"turn-"+angle+"-moving-"+moving,-1,150,i=>p.SetTarget(p.Position+(moving?direction*10:Vector2.zero),p.Position+direction*10,!moving));
                report.cases.Add(row);Save("controlled-carrier-turns",report);
                Assert.That(row.ownedSeconds,Is.GreaterThan(2.9f));Assert.That(row.releases,Is.Zero);
                Assert.That(m.InDribbleArea(p),Is.True);Assert.That(Vector2.Dot(p.Forward,direction),Is.GreaterThan(.99f));
                if(moving)Assert.That(row.displacement,Is.GreaterThan(10));
                Assert.That(m.BoundaryCorrections,Is.Zero);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ACarrierEscapesFrontAndRearPressureWithoutChangingTheCommand()
        {
            var report=new Report();
            foreach(var team in new[]{Team.Red,Team.Navy})foreach(bool frontal in new[]{false,true})foreach(bool learning in new[]{false,true})
            using(var w=new FiveTestWorld())
            {
                Isolate(w);var m=w.match;var sign=FiveMatch.AttackSign(team);int index=(int)team*5+1,opponent=(int)FiveMatch.Other(team)*5+1;
                w.Place(index,new Vector2(-15*sign,0),Vector2.right*sign);w.Give(index);
                w.Place(opponent,m.players[index].Position+Vector2.right*sign*(frontal?4:-3),Vector2.right*(frontal?-sign:sign));
                m.tacticsEnabled=true;m.teamPlanningEnabled[(int)FiveMatch.Other(team)]=false;
                if(learning)Assert.That(FiveLearningContract.Apply(m,team,(int)FiveCommand.FastTransition),Is.True);
                else m.SelectCommand(team,FiveCommand.FastTransition,"pressure-dribble-test",true);
                m.players[index].KickReadyAt=m.Elapsed+4; // Isolate the requested carry escape from passing.
                var decision=m.Decision(team);var revision=decision.revision;var next=decision.nextDecisionAt;
                var row=Measure(w,index,(frontal?"front-pressure":"rear-pressure")+"-learning-"+learning,0,100,i=>m.players[opponent].SetTarget(m.BallPosition,m.BallPosition));
                report.cases.Add(row);Save("pressure-carrier-escape",report);
                Assert.That(m.Events.Any(e=>e.kind=="carrier-escape"&&e.actor==index),Is.True);
                Assert.That(row.ownedSeconds,Is.GreaterThan(1.9f));Assert.That(row.ownerAtEnd,Is.EqualTo(index));
                Assert.That(row.displacement,Is.GreaterThan(6));
                if(frontal)Assert.That(row.lateral,Is.GreaterThan(1.5f));
                else Assert.That(row.forwardProgress,Is.GreaterThan(4));
                Assert.That(decision.command,Is.EqualTo(FiveCommand.FastTransition));Assert.That(decision.revision,Is.EqualTo(revision));Assert.That(decision.nextDecisionAt,Is.EqualTo(next));
                Assert.That(m.BoundaryCorrections,Is.Zero);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator WaitingForAPassOrProtectingPossessionDoesNotPinTheCarrier()
        {
            foreach(var team in new[]{Team.Red,Team.Navy})foreach(var command in new[]{FiveCommand.LongPass,FiveCommand.WideCirculation,FiveCommand.WideTrap,FiveCommand.MarkReceivers,FiveCommand.AllRecover})
            using(var w=new FiveTestWorld())
            {
                Isolate(w);var m=w.match;var sign=FiveMatch.AttackSign(team);int index=(int)team*5+1;
                w.Place(index,new Vector2(-12*sign,0),Vector2.right*sign);w.Give(index);
                // No pass is available during this short kick cooldown, so the carrier must move with the ball.
                m.players[index].KickReadyAt=m.Elapsed+4;
                m.tacticsEnabled=true;m.teamPlanningEnabled[(int)FiveMatch.Other(team)]=false;m.SelectCommand(team,command,"waiting-progress",true);
                var row=Measure(w,index,"waiting-progress",(int)command,100);
                Assert.That(row.ownedSeconds,Is.GreaterThan(1.9f),command+" must retain actual front control while moving");
                Assert.That(row.forwardProgress,Is.GreaterThan(8),command+" must keep carrying instead of waiting stationary");
                Assert.That(row.stoppedOwnedSeconds,Is.LessThan(.3f));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator CaptureCarrierTurnAndProgressBaseline()
        {
            var report=new Report();
            foreach(var team in new[]{Team.Red,Team.Navy})
            {
                var offset=(int)team*5;var sign=FiveMatch.AttackSign(team);
                foreach(float angle in new[]{90f,180f})using(var w=new FiveTestWorld())
                {
                    Isolate(w);w.Place(offset+1,Vector2.zero,Vector2.right*sign);w.Give(offset+1);var p=w.match.players[offset+1];
                    var radians=angle*Mathf.Deg2Rad;var facing=new Vector2(Mathf.Cos(radians),Mathf.Sin(radians))*sign;
                    report.cases.Add(Measure(w,p.Index,"controlled-turn-"+angle,-1,150,i=>p.SetTarget(p.Position,p.Position+facing*10,true)));
                }
                foreach(var command in (FiveCommand[])Enum.GetValues(typeof(FiveCommand)))using(var w=new FiveTestWorld())
                {
                    Isolate(w);var m=w.match;w.Place(offset+1,new Vector2(-12*sign,0),Vector2.right*sign);w.Give(offset+1);
                    m.tacticsEnabled=true;m.teamPlanningEnabled[(int)FiveMatch.Other(team)]=false;m.SelectCommand(team,command,"carrier-progress-probe",true);
                    report.cases.Add(Measure(w,offset+1,"command-progress",(int)command,150));
                }
            }
            Save("carrier-motion-probe",report);yield return null;
        }
    }
}
