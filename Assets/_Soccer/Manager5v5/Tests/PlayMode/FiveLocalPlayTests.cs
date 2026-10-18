using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveLocalPlayTests
    {
        [UnityTearDown] public IEnumerator Cleanup(){FiveTestWorld.DisposeOutstanding();yield return null;}
        static void Quiet(FiveTestWorld w)
        {
            var m=w.match;m.keepersEnabled=false;m.teamPlanningEnabled=new[]{false,false};
            for(int i=0;i<10;i++)w.Place(i,new Vector2(-45+i*9,28),Vector2.down);
        }
        static void Ball(FiveMatch m,Vector2 point,Vector2 velocity)
        {
            m.ball.position=new Vector3(point.x,m.ballSpawn.y,point.y);m.ball.transform.position=m.ball.position;
            m.ball.linearVelocity=new Vector3(velocity.x,0,velocity.y);m.ball.angularVelocity=Vector3.zero;Physics.SyncTransforms();
        }
        static void Stuck(FiveTestWorld w)
        {
            Quiet(w);var m=w.match;
            w.Place(1,new Vector2(-2,0),Vector2.left);w.Place(6,new Vector2(2,0),Vector2.right);
            w.Place(2,new Vector2(-9,-5),Vector2.left);w.Place(3,new Vector2(-16,8),Vector2.left);w.Place(4,new Vector2(-35,-20),Vector2.left);
            w.Place(7,new Vector2(9,5),Vector2.right);w.Place(8,new Vector2(16,-8),Vector2.right);w.Place(9,new Vector2(35,20),Vector2.right);
            Ball(m,Vector2.zero,Vector2.zero);
        }
        static void FixedObservation(FiveMatch m,int steps)
        {
            // Hold the observed blocked layout, without physics, to measure the timer independently of escape success.
            for(int i=0;i<steps;i++){m.ball.linearVelocity=Vector3.zero;m.Tick(.01f);}
        }
        [Serializable] sealed class DelayCase {public int command;public bool learning;public float localAt,reinforceAt;public int helpers;}
        [Serializable] sealed class DelayReport {public List<DelayCase> cases=new List<DelayCase>();}
        [UnityTest] public IEnumerator EveryOrderLetsLocalPlayersTryBeforeUnchangedOrderReinforcements()
        {
            var report=new DelayReport();
            foreach(bool learning in new[]{false,true})for(int action=0;action<8;action++)
            {
                using(var w=new FiveTestWorld())
                {
                    Stuck(w);var m=w.match;
                    foreach(var team in new[]{Team.Red,Team.Navy})
                        Assert.That(learning?FiveLearningContract.Apply(m,team,action):m.SelectCommand(team,(FiveCommand)action),Is.True);
                    var revisions=m.decisions.Select(d=>d.revision).ToArray();var next=m.decisions.Select(d=>d.nextDecisionAt).ToArray();
                    FixedObservation(m,795);
                    Assert.That(m.Events.Any(e=>e.kind=="contest-start"),Is.True);
                    Assert.That(m.Events.Any(e=>e.kind=="contest-reinforce"),Is.False);
                    FixedObservation(m,12);
                    var entries=m.Events.Where(e=>e.kind=="contest-reinforce").ToArray();
                    Assert.That(entries.Length,Is.EqualTo(4));
                    Assert.That(entries.Select(e=>e.actor).Distinct().OrderBy(x=>x),Is.EqualTo(new[]{2,3,7,8}));
                    Assert.That(m.decisions.Select(d=>(int)d.command),Is.All.EqualTo(action));
                    Assert.That(m.decisions.Select(d=>d.revision),Is.EqualTo(revisions));Assert.That(m.decisions.Select(d=>d.nextDecisionAt),Is.EqualTo(next));
                    Assert.That(m.Events.Count(e=>e.kind=="command"),Is.EqualTo(2));
                    report.cases.Add(new DelayCase {command=action,learning=learning,localAt=m.Events.First(e=>e.kind=="contest-start").time,
                        reinforceAt=entries[0].time,helpers=entries.Length});
                    Ball(m,new Vector2(2,0),Vector2.zero);m.Tick(.01f);
                    Assert.That(Enumerable.Range(0,10).Any(m.IsContestReinforcement),Is.False);
                    Assert.That(m.ContestReinforcementWait,Is.EqualTo(6).Within(.02));
                }
                yield return null;
            }
            File.WriteAllText(FiveP7Evidence.DirectoryPath+"/local-reinforcement-delays.json",JsonUtility.ToJson(report,true));
        }
        [UnityTest] public IEnumerator MovingBallPauseAndRoundResetCannotAccumulateReinforcementTime()
        {
            using(var w=new FiveTestWorld())
            {
                Stuck(w);var m=w.match;FixedObservation(m,500);var remaining=m.ContestReinforcementWait;
                m.Paused=true;FixedObservation(m,500);Assert.That(m.ContestReinforcementWait,Is.EqualTo(remaining));m.Paused=false;
                Ball(m,new Vector2(1,0),Vector2.zero);m.Tick(.01f);Assert.That(m.ContestReinforcementWait,Is.EqualTo(6).Within(.02));
                m.ResetRound();Assert.That(m.ContestReinforcementWait,Is.EqualTo(6));
                Stuck(w);FixedObservation(m,180);Assert.That(m.Events.Any(e=>e.kind=="contest-reinforce"),Is.False);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator CarrierChallengeDoesNotRecruitTheExcludedRecoveryField()
        {
            using(var w=new FiveTestWorld())
            {
                Quiet(w);var m=w.match;m.contestReliefEnabled=false;
                w.Place(6,Vector2.zero,Vector2.right);w.Give(6);
                w.Place(1,new Vector2(-2,0),Vector2.right);w.Place(2,new Vector2(0,2.2f),Vector2.down);
                w.Place(3,new Vector2(0,-2.7f),Vector2.up);w.Place(4,new Vector2(-3.5f,0),Vector2.right);
                m.teamPlanningEnabled[0]=true;m.SelectCommand(Team.Red,FiveCommand.AllRecover,"excluded-field",true);
                var reserve=m.players[4].Position;m.Tick(.01f);
                Assert.That(Vector2.Distance(m.players[4].Target,reserve),Is.LessThan(.001f));
                Assert.That(m.Events.Any(e=>e.kind=="challenge-route"&&e.actor==4),Is.False);
            }
            yield return null;
        }
        [Serializable] sealed class FrictionReport {public float oldStatic,newStatic,oldDynamic,newDynamic,oldDrag,newDrag,oldDistance,newDistance,oldSpeed,newSpeed;}
        [UnityTest] public IEnumerator BallFrictionIsDoubledWithoutEditingTheOriginalAndFreeSlidingIsReduced()
        {
            var report=new FrictionReport();
            foreach(bool current in new[]{false,true})using(var w=new FiveTestWorld())
            {
                Quiet(w);var m=w.match;m.tacticsEnabled=m.contestReliefEnabled=false;
                var original=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/_Soccer/Manager/Profiles/MNG_BallPhysics.physicMaterial");
                var copy=m.ball.GetComponent<SphereCollider>().sharedMaterial;
                Assert.That(copy,Is.Not.SameAs(original));Assert.That(copy.staticFriction,Is.EqualTo(original.staticFriction*2).Within(.00001));
                Assert.That(copy.dynamicFriction,Is.EqualTo(original.dynamicFriction*2).Within(.00001));
                Assert.That(original.staticFriction,Is.EqualTo(.05f));Assert.That(original.dynamicFriction,Is.EqualTo(.05f));
                report.oldStatic=original.staticFriction;report.newStatic=copy.staticFriction;report.oldDynamic=original.dynamicFriction;report.newDynamic=copy.dynamicFriction;
                if(!current){m.ball.GetComponent<SphereCollider>().sharedMaterial=original;m.ball.linearDamping=m.profile.freeBallDamping;}
                else Assert.That(m.ball.linearDamping,Is.EqualTo(m.profile.freeBallDamping*2));
                Ball(m,new Vector2(-15,0),new Vector2(8,0));w.Step(150);
                if(current){report.newDistance=m.BallPosition.x+15;report.newSpeed=m.BallVelocity.magnitude;report.newDrag=m.ball.linearDamping;}
                else{report.oldDistance=m.BallPosition.x+15;report.oldSpeed=m.BallVelocity.magnitude;report.oldDrag=m.ball.linearDamping;}
            }
            File.WriteAllText(FiveP7Evidence.DirectoryPath+"/ball-friction-comparison.json",JsonUtility.ToJson(report,true));
            Assert.That(report.newDistance,Is.LessThan(report.oldDistance*.9f));Assert.That(report.newSpeed,Is.LessThan(report.oldSpeed*.8f));
            yield return null;
        }
        [Serializable] sealed class AlignCase {public int sender,receiver;public float initialAngle,launchAngle;public bool received;}
        [Serializable] sealed class AlignReport {public List<AlignCase> cases=new List<AlignCase>();}
        [UnityTest] public IEnumerator PassReceiverPrealignsFromBothSidesBeforeKickAndKeepsAStableReception()
        {
            var report=new AlignReport();
            foreach(int team in new[]{0,1})foreach(int slot in new[]{0,3})foreach(float angle in new[]{0f,90f,180f})
            {
                using(var w=new FiveTestWorld())
                {
                    Quiet(w);var m=w.match;m.contestReliefEnabled=false;var sign=team==0?1:-1;
                    var direction=Vector2.right*sign;int sender=team*5+1,receiver=team*5+slot;
                    w.Place(sender,-direction*10,direction);
                    var radians=angle*Mathf.Deg2Rad;
                    w.Place(receiver,direction*10,new Vector2(-direction.x*Mathf.Cos(radians),Mathf.Sin(radians)));
                    w.Give(sender);Assert.That(m.RequestPass(m.players[sender],m.players[receiver]),Is.True);
                    for(int i=0;i<450&&m.PassReceptions==0;i++)w.Step(1);
                    var aligned=m.Events.FirstOrDefault(e=>e.kind=="pass-receiver-ready");
                    var row=new AlignCase{sender=sender,receiver=receiver,initialAngle=angle,received=m.PassReceptions==1,
                        launchAngle=aligned==null?180:float.Parse(aligned.reason,System.Globalization.CultureInfo.InvariantCulture)};
                    report.cases.Add(row);File.WriteAllText(FiveP7Evidence.DirectoryPath+"/pass-prealignment.json",JsonUtility.ToJson(report,true));
                    Assert.That(row.launchAngle,Is.LessThan(9));Assert.That(row.received,Is.True,$"team={team},slot={slot},angle={angle}");
                    w.Step(20);Assert.That(m.Owner,Is.EqualTo(receiver));
                }
                yield return null;
            }
        }
        [Serializable] sealed class StealCase {public int mirror;public bool moving,won;public float maximumAhead,maximumLateral;public int routes;}
        [Serializable] sealed class StealReport {public List<StealCase> cases=new List<StealCase>();}
        [UnityTest] public IEnumerator RearChallengerGoesAroundTheCarrierAndReachesTheBallFromAhead()
        {
            var report=new StealReport();
            foreach(int mirror in new[]{1,-1})foreach(bool moving in new[]{false,true})using(var w=new FiveTestWorld())
            {
                Quiet(w);var m=w.match;m.contestReliefEnabled=false;var forward=Vector2.right*mirror;
                var attacker=mirror==1?1:6;var owner=mirror==1?6:1;var team=m.players[attacker].team;
                w.Place(owner,Vector2.zero,forward);w.Place(attacker,-forward*6,forward);w.Give(owner);
                m.teamPlanningEnabled[(int)team]=true;m.SelectCommand(team,FiveCommand.FastTransition,"front-challenge",true);
                var row=new StealCase{mirror=mirror,moving=moving};
                for(int i=0;i<450&&m.Owner!=attacker;i++)
                {
                    if(moving&&m.Owner==owner)m.players[owner].SetTarget(m.players[owner].Position+forward*.45f,m.players[owner].Position+forward*10);
                    w.Step(1);var relative=m.players[attacker].Position-m.players[owner].Position;
                    row.maximumAhead=Mathf.Max(row.maximumAhead,Vector2.Dot(relative,forward));row.maximumLateral=Mathf.Max(row.maximumLateral,Mathf.Abs(relative.y));
                }
                row.won=m.Owner==attacker;row.routes=m.Events.Count(e=>e.kind=="challenge-route"&&e.actor==attacker);
                report.cases.Add(row);File.WriteAllText(FiveP7Evidence.DirectoryPath+"/front-challenges.json",JsonUtility.ToJson(report,true));
                Assert.That(row.routes,Is.GreaterThan(0));Assert.That(row.maximumLateral,Is.GreaterThan(1.2f));
                Assert.That(row.maximumAhead,Is.GreaterThan(m.profile.holdOffset));Assert.That(row.won,Is.True,$"mirror={mirror},moving={moving}");
                Assert.That(m.BoundaryCorrections,Is.Zero);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ForwardCarryAimsAtGoalCentreAndOrdinaryTargetsStayAwayFromWalls()
        {
            foreach(var team in new[]{Team.Red,Team.Navy})using(var w=new FiveTestWorld())
            {
                Quiet(w);var m=w.match;m.contestReliefEnabled=false;int owner=(int)team*5+1;var sign=FiveMatch.AttackSign(team);
                w.Place(owner,new Vector2(0,24*sign),Vector2.right*sign);w.Give(owner);m.teamPlanningEnabled[(int)team]=true;
                m.SelectCommand(team,FiveCommand.FastTransition,"goal-centre-carry",true);m.Tick(.01f);
                var p=m.players[owner];var goal=new Vector2(sign*m.geometry.HalfLength,0);
                Assert.That(Vector2.Dot((p.Target-p.Position).normalized,(goal-p.Position).normalized),Is.GreaterThan(.999f));
                m.tacticsEnabled=false;
                foreach(var player in m.players.Where(q=>!q.IsKeeper))player.SetTarget(new Vector2(m.geometry.HalfLength,m.geometry.HalfWidth),Vector2.zero,true);
                m.Tick(.01f);
                foreach(var player in m.players.Where(q=>!q.IsKeeper))
                {Assert.That(Mathf.Abs(player.Target.x),Is.LessThanOrEqualTo(m.geometry.HalfLength-4));Assert.That(Mathf.Abs(player.Target.y),Is.LessThanOrEqualTo(m.geometry.HalfWidth-4));}
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ChallengerUsesTheOpenSideWhenItsPreferredArcWouldHitTheWall()
        {
            foreach(int mirror in new[]{1,-1})foreach(int side in new[]{1,-1})using(var w=new FiveTestWorld())
            {
                Quiet(w);var m=w.match;m.contestReliefEnabled=false;var forward=Vector2.right*mirror;
                var attacker=mirror==1?1:6;var owner=mirror==1?6:1;var team=m.players[attacker].team;
                var position=new Vector2(0,side*(m.geometry.HalfWidth-1.4f));
                w.Place(owner,position,forward);w.Place(attacker,position-forward*6+Vector2.up*side*.6f,forward);w.Give(owner);
                m.teamPlanningEnabled[(int)team]=true;m.SelectCommand(team,FiveCommand.FastTransition,"wall-arc",true);m.Tick(.01f);
                Assert.That(m.players[attacker].Target.y*side,Is.LessThan(position.y*side-.5f));
                Assert.That(m.Events.Any(e=>e.kind=="challenge-route"&&e.actor==attacker),Is.True);
            }
            yield return null;
        }
        [UnityTest] public IEnumerator OnlyAPlayerRetrievingTheWallBallMayApproachTheWall()
        {
            using(var w=new FiveTestWorld())
            {
                Quiet(w);var m=w.match;m.tacticsEnabled=m.contestReliefEnabled=false;
                var point=new Vector2(0,m.geometry.HalfWidth-.6f);Ball(m,point,Vector2.zero);
                m.players[1].SetTarget(point,point,true);m.players[2].SetTarget(new Vector2(20,m.geometry.HalfWidth),point,true);m.Tick(.01f);
                Assert.That(m.players[1].Target.y,Is.GreaterThan(m.geometry.HalfWidth-2));
                Assert.That(m.players[2].Target.y,Is.LessThanOrEqualTo(m.geometry.HalfWidth-4));
            }
            yield return null;
        }
    }
}
