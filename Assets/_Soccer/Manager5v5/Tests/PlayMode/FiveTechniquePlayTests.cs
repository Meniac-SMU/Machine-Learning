using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object=UnityEngine.Object;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveTechniquePlayTests
    {
        GameObject root;
        FiveMatch match;
        SimulationMode previousSimulation;
        [Serializable] sealed class PassCase
        {
            public int sample,sender,receiver,attempts,receptions,owner;
            public float distance,angle;
            public string phase;
            public Vector2 ballPosition;
            public List<FiveEvent> events;
        }
        [Serializable] sealed class PassReport {public int count,success;public List<PassCase> cases=new List<PassCase>();}
        [UnitySetUp]
        public IEnumerator Setup()
        {
#if UNITY_EDITOR
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Soccer/Manager5v5/Prefabs/FiveArena.prefab");
            root=Object.Instantiate(prefab);match=root.GetComponent<FiveMatch>();
            match.profile=Object.Instantiate(match.profile);
            match.automaticTick=false;match.keepersEnabled=false;
            match.tacticsEnabled=false;
            previousSimulation=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            match.ResetMatch();Isolate();
            yield return null;
#else
            Assert.Ignore("Editor integration tests.");yield break;
#endif
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Physics.simulationMode=previousSimulation;
            if(match!=null)Object.Destroy(match.profile);
            if(root!=null)Object.Destroy(root);
            yield return null;
        }
        void Isolate()
        {
            match.ResetMatch();match.keepersEnabled=false;
            for(int i=0;i<10;i++)Place(match.players[i],new Vector2(-45+i*9,32),Vector2.right);
            // This is an unopposed technique fixture. T9 must not turn parked opponents into movers.
            for(int i=5;i<10;i++)match.players[i].Body.constraints=RigidbodyConstraints.FreezeAll;
            match.ball.linearDamping=match.profile.EffectiveFreeBallDamping;
        }
        static void Place(FivePlayer player,Vector2 point,Vector2 forward)
        {
            var pos=new Vector3(point.x,player.spawn.y,point.y);
            var rot=Quaternion.LookRotation(new Vector3(forward.x,0,forward.y));
            player.transform.SetPositionAndRotation(pos,rot);player.Body.position=pos;player.Body.rotation=rot;
            player.Body.linearVelocity=Vector3.zero;player.Body.angularVelocity=Vector3.zero;
            player.SetTarget(point,point+forward*10);
        }
        void Give(FivePlayer player)
        {
            var point=match.Anchor(player);
            var pos=new Vector3(point.x,match.ballSpawn.y,point.y);
            match.ball.transform.position=pos;match.ball.position=pos;match.ball.linearVelocity=Vector3.zero;
            Physics.SyncTransforms();Step(8);
            Assert.That(match.Owner,Is.EqualTo(player.Index),"Initial natural acquisition failed.");
        }
        void Step(int count)
        {for(var i=0;i<count;i++)match.SimulatePhysicsStep(.02f);}

        [UnityTest]
        public IEnumerator ThirtyTwoClearPassesThroughMaximumRangeIncludingKeeperAndRotation()
        {
            var report=new PassReport();
            for(var sample=0;sample<32;sample++)
            {
                Isolate();
                var distance=8+sample*(77f/31);
                var angle=new[]{0f,25f,-25f,180f}[sample%4];
                var direction=new Vector2(Mathf.Cos(angle*Mathf.Deg2Rad),Mathf.Sin(angle*Mathf.Deg2Rad));
                var sender=match.players[sample%5==0?0:1];
                var receiver=match.players[sample%5==1?0:3];
                var start=-direction*distance*.5f;
                Place(sender,start,direction);Place(receiver,start+direction*distance,direction);
                Give(sender);
                // Natural acquisition may nudge the sender. Establish the intended actual range afterwards.
                Place(receiver,sender.Position+direction*distance,direction);Physics.SyncTransforms();
                Assert.That(match.RequestPass(sender,receiver),Is.True,$"sample={sample}, nominal={distance:R}, actual={Vector2.Distance(sender.Position,receiver.Position):R}, owner={match.Owner}, time={match.Elapsed:R}, readyAt={sender.KickReadyAt:R}, phase={match.PassPhase}");
                for(var step=0;step<450 && match.PassReceptions==0;step++)Step(1);
                report.count++;if(match.PassReceptions==1)report.success++;
                report.cases.Add(new PassCase {sample=sample,sender=sender.Index,receiver=receiver.Index,distance=distance,angle=angle,
                    attempts=match.PassAttempts,receptions=match.PassReceptions,phase=match.PassPhase.ToString(),owner=match.Owner,
                    ballPosition=match.BallPosition,events=new List<FiveEvent>(match.Events)});
                yield return null;
            }
            Directory.CreateDirectory(FiveP7Evidence.DirectoryPath);
            File.WriteAllText(FiveP7Evidence.DirectoryPath+"/g2-clear-passes.json",JsonUtility.ToJson(report,true));
            Assert.That(report.success,Is.GreaterThanOrEqualTo(31),"Unopposed actual receiver control must reach 95%, including the 85m maximum.");
            Assert.That(report.cases.Sum(c=>c.attempts),Is.EqualTo(32));
        }
        [UnityTest]
        public IEnumerator StraightDribblingAndStoppingKeepPossessionAndDistantBallIsReleased()
        {
            var owner=match.players[1];Place(owner,Vector2.zero,Vector2.right);Give(owner);
            float maximumDistance=0;
            for(var i=0;i<600;i++)
            {
                var direction=Vector2.right;
                owner.SetTarget(i<180?owner.Position+direction*5:owner.Position,owner.Position+direction*5);
                Step(1);maximumDistance=Mathf.Max(maximumDistance,Vector2.Distance(match.BallPosition,owner.Position));
                Assert.That(match.Owner,Is.EqualTo(owner.Index),$"Lost possession at step {i}; distance {maximumDistance}");
                if(i%100==0)yield return null;
            }
            Assert.That(maximumDistance,Is.LessThan(match.profile.releaseDistance));
            var escaped=new Vector3(owner.Position.x+8,match.ballSpawn.y,owner.Position.y);
            match.ball.position=escaped;match.ball.transform.position=escaped;Physics.SyncTransforms();Step(2);
            Assert.That(match.Owner,Is.EqualTo(-1));
            Assert.That(Vector3.Distance(match.ball.position,escaped),Is.LessThan(1),"No magnetic teleport from beyond the control area.");
        }
        [UnityTest]
        public IEnumerator OpponentInThePassLaneReallyIntercepts()
        {
            var sender=match.players[1];var receiver=match.players[3];var opponent=match.players[6];
            Place(sender,new Vector2(-16,0),Vector2.right);Place(receiver,new Vector2(16,0),Vector2.left);
            Place(opponent,Vector2.zero,Vector2.left);Give(sender);
            Assert.That(match.RequestPass(sender,receiver),Is.True);Step(300);
            Assert.That(match.PassAttempts,Is.EqualTo(1));Assert.That(match.PassReceptions,Is.Zero);
            Assert.That(match.Interceptions,Is.GreaterThanOrEqualTo(1));
            Assert.That(match.Events.Any(e=>e.kind=="possession"&&e.actor==opponent.Index),Is.True);
            yield return null;
        }
        [UnityTest]
        public IEnumerator KeeperPredictsAFastIncomingBallInsteadOfChasingItsCurrentPosition()
        {
            var keeper=match.players[5];Place(keeper,new Vector2(59,0),Vector2.left);
            match.ball.position=new Vector3(51,match.ballSpawn.y,-2);match.ball.linearVelocity=new Vector3(32,0,-16);
            Physics.SyncTransforms();
            var target=match.KeeperTarget(keeper);
            Assert.That(target.x,Is.EqualTo(59).Within(.01f));
            Assert.That(target.y,Is.EqualTo(-6).Within(.01f));
            match.ball.linearVelocity=Vector3.zero;
            var looseTarget=match.KeeperTarget(keeper);
            Assert.That(Vector2.Distance(looseTarget,new Vector2(51,-2)),Is.LessThan(.01f));
            yield return null;
        }
        [UnityTest]
        public IEnumerator KeeperPassesToTeammateUnderThreatAndOutfielderCannotEmergencyClear()
        {
            var keeper=match.players[0];var enemy=match.players[6];
            Place(keeper,new Vector2(-55,0),Vector2.right);Place(enemy,new Vector2(-52,3),Vector2.left);Give(keeper);
            var since=match.OwnerSince;match.keepersEnabled=true;Step(300);
            var pass=match.Events.FirstOrDefault(e=>e.kind=="pass-kicked"&&e.actor==keeper.Index);
            Assert.That(pass,Is.Not.Null);
            Assert.That(pass.time-since,Is.LessThanOrEqualTo(6));
            Assert.That(match.players[pass.other].team,Is.EqualTo(keeper.team));
            Isolate();var field=match.players[1];Place(field,Vector2.zero,Vector2.right);Give(field);
            Assert.That(match.RequestShot(field,new Vector2(50,0),true),Is.False);
            yield return null;
        }
        [UnityTest]
        public IEnumerator FailedShotPreparationReleasesThePlayerForTacticalMovement()
        {
            var player=match.players[3];Place(player,new Vector2(40,0),Vector2.right);Give(player);
            var originalPlayerConstraints=player.Body.constraints;var originalBallConstraints=match.ball.constraints;
            player.Body.constraints=RigidbodyConstraints.FreezeAll;match.ball.constraints=RigidbodyConstraints.FreezeAll;
            var turnRate=match.profile.turnRate;match.profile.turnRate=.001f;
            Assert.That(match.RequestShot(player,new Vector2(-63,0)),Is.True);Step(115);
            match.profile.turnRate=turnRate;
            Assert.That(match.Shots,Is.Zero);Assert.That(match.Events.Any(e=>e.kind=="shot-failed"),Is.True);
            Assert.That(match.RequestShot(player,new Vector2(63,0)),Is.False,"Failed preparation must allow one decision period for movement instead of immediately pinning the player again.");
            player.Body.constraints=originalPlayerConstraints;match.ball.constraints=originalBallConstraints;
            var start=player.Position;player.SetTarget(start+Vector2.up*8,start+Vector2.up*8);Step(50);
            Assert.That(Vector2.Distance(player.Position,start),Is.GreaterThan(3));
            yield return null;
        }
    }
}
