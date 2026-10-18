using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object=UnityEngine.Object;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveTestWorld:IDisposable
    {
        public readonly GameObject root;
        public readonly FiveMatch match;
        readonly SimulationMode previous;
        static readonly List<FiveTestWorld> active=new List<FiveTestWorld>();
        bool disposed;
        public FiveTestWorld()
        {
#if UNITY_EDITOR
            root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Soccer/Manager5v5/Prefabs/FiveArena.prefab"));
            match=root.GetComponent<FiveMatch>();match.automaticTick=false;
            match.ruleManagers=new[]{false,false};
            // Historical P7 fixtures explicitly retain the P7 selector. Strong-rule tests opt in.
            match.ruleVariants=new[]{FiveRuleVariant.Legacy,FiveRuleVariant.Legacy};
            match.profile=Object.Instantiate(match.profile);
            previous=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
            match.ResetMatch();
            active.Add(this);
#endif
        }
        public void Step(int count,Action sample=null)
        {
            for(int i=0;i<count;i++)
            {
                match.SimulatePhysicsStep(.02f);
                CheckFinite(match.ball);
                foreach(var player in match.players)CheckFinite(player.Body);
                sample?.Invoke();
            }
        }
        static void CheckFinite(Rigidbody body)
        {
            var p=body.position;var v=body.linearVelocity;
            if(!float.IsFinite(p.x)||!float.IsFinite(p.y)||!float.IsFinite(p.z)||!float.IsFinite(v.x)||!float.IsFinite(v.y)||!float.IsFinite(v.z))
                throw new InvalidOperationException("Non-finite physical state: "+body.name);
        }
        public void Place(int index,Vector2 position,Vector2 forward)
        {
            var player=match.players[index];var point=new Vector3(position.x,player.spawn.y,position.y);
            var rotation=Quaternion.LookRotation(new Vector3(forward.x,0,forward.y));
            player.transform.SetPositionAndRotation(point,rotation);player.Body.position=point;player.Body.rotation=rotation;
            player.Body.linearVelocity=Vector3.zero;player.Body.angularVelocity=Vector3.zero;
            player.SetTarget(position,position+forward*10);Physics.SyncTransforms();
        }
        public void Give(int index)
        {
            var enabled=match.tacticsEnabled;var keeper=match.keepersEnabled;
            match.tacticsEnabled=false;match.keepersEnabled=false;
            var anchor=match.Anchor(match.players[index]);var point=new Vector3(anchor.x,match.ballSpawn.y,anchor.y);
            match.ball.transform.position=point;match.ball.position=point;match.ball.linearVelocity=Vector3.zero;
            Physics.SyncTransforms();Step(8);match.tacticsEnabled=enabled;match.keepersEnabled=keeper;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;active.Remove(this);
            if(root!=null){root.SetActive(false);Object.Destroy(match.profile);Object.Destroy(root);}
            // Session.OnDisable restores its own mode first; the outer fixture restores last.
            Physics.simulationMode=previous;
        }
        public static void DisposeOutstanding()
        {
            // UnityTest timeouts need explicit teardown as an abandoned iterator may retain its world.
            while(active.Count>0)active[active.Count-1].Dispose();
        }
    }
}
