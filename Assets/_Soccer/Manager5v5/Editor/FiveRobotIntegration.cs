using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace MachineLearning.Soccer.Manager5v5.Editor
{
    public static class FiveRobotIntegration
    {
        const string Art="Assets/_Soccer/Manager5v5/Art/CubeRobots";
        const string Evidence="Logs/RobotIntegration-20261017";
        [Serializable] public sealed class Record
        {
            public string name,variant; public Vector3 oldBodySize,newBodySize;
            public int colliders,renderers; public float anchorDistance,plateFront,ballGap;
        }
        [Serializable] public sealed class Report {public string prefab; public Record[] players;}
        public static void Install()
        {
            FiveRobotAssetSetup.Build();
            foreach(var path in new[]{FiveBuilder.Prefab,"Assets/_Soccer/Manager5v5/Demo/FiveDemoArena.prefab"})
            {
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var report=new Report{prefab=path,players=Apply(root)};
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                    Directory.CreateDirectory(Evidence);
                    File.WriteAllText(Evidence+"/"+Path.GetFileNameWithoutExtension(path)+"-integration.json",JsonUtility.ToJson(report,true));
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();
        }
        public static Record[] Apply(GameObject arena)
        {
            var match=arena.GetComponent<FiveMatch>();
            return match.players.Select(player=>ApplyPlayer(player,match)).ToArray();
        }
        static Record ApplyPlayer(FivePlayer player,FiveMatch match)
        {
            string variant=player.team==Team.Red?(player.IsKeeper?"PinkGoalkeeper":"RedRobot"):(player.IsKeeper?"SkyGoalkeeper":"NavyRobot");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/Prefabs/"+variant+".prefab");
            if(prefab==null)throw new InvalidOperationException("Generate robot assets before integrating.");
            var existing=player.transform.Find("RobotVisual");
            var oldCube=player.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(m=>m.name.StartsWith("AgentCube"));
            Vector3 oldBodySize=existing!=null?player.transform.lossyScale:oldCube!=null?
                Vector3.Scale(oldCube.sharedMesh.bounds.size,oldCube.transform.lossyScale):Vector3.zero;
            if(oldBodySize==Vector3.zero)throw new InvalidOperationException("Original cube not found for "+player.name);
            if(Mathf.Abs(oldBodySize.x-oldBodySize.y)>.001f||Mathf.Abs(oldBodySize.x-oldBodySize.z)>.001f)
                throw new InvalidOperationException("Expected a cubic original body.");
            var oldCollider=player.GetComponentsInChildren<Collider>(true).First(c=>!c.isTrigger);
            var physics=oldCollider.sharedMaterial;float contact=oldCollider.contactOffset;
            foreach(var c in player.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(c);
            foreach(var r in player.GetComponentsInChildren<MeshRenderer>(true))Object.DestroyImmediate(r);
            foreach(var f in player.GetComponentsInChildren<MeshFilter>(true))Object.DestroyImmediate(f);
            if(existing!=null)Object.DestroyImmediate(existing.gameObject);
            // Preserve the gameplay Rigidbody root/spawn. Offset the visual's ground pivot to world Y=0.
            float localGround=-player.spawn.y/Mathf.Abs(player.transform.lossyScale.y);
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(prefab,player.transform);
            visual.name="RobotVisual";visual.transform.localPosition=new Vector3(0,localGround,0);
            visual.transform.localRotation=Quaternion.identity;visual.transform.localScale=Vector3.one;
            foreach(Transform t in visual.GetComponentsInChildren<Transform>(true))t.gameObject.layer=player.gameObject.layer;
            // FBX meter dimensions, measured from named Blender parts. Body edge is exactly 1 before root scale.
            Box(player,"BodyCollider",new Vector3(0,localGround+.54f,0),Vector3.one,physics,contact);
            Box(player,"WheelLeftCollider",new Vector3(-.5605f,localGround+.355f,-.06f),new Vector3(.231f,.71f,.71f),physics,contact);
            Box(player,"WheelRightCollider",new Vector3(.5605f,localGround+.355f,-.06f),new Vector3(.231f,.71f,.71f),physics,contact);
            // One AABB covers the front crossbar and both rear mounting caps.
            var plate=Box(player,"KickPlateCenter",new Vector3(0,localGround+.17f,.63181675f),new Vector3(1.32f,.34f,.3563665f),physics,contact);
            player.InvalidateKickPlateCache();
            player.radius=.5f*Mathf.Abs(player.transform.lossyScale.x);
            // Keep existing HUD/head marker clearance, which is independent of the visible torso.
            var size=player.transform.lossyScale;
            if(Vector3.Distance(size,oldBodySize)>.001f)throw new InvalidOperationException("Body size changed.");
            var gap=match.profile.dribblePlateGap;
            var distance=player.CloseHoldOffset(match.ballRadius,gap,match.ballSpawn.y);
            if(player.GetComponentsInChildren<Collider>(true).Length!=4||player.GetComponentsInChildren<BoxCollider>(true).Length!=4)
                throw new InvalidOperationException("Exactly four BoxColliders required.");
            return new Record{name=player.name,variant=variant,oldBodySize=oldBodySize,newBodySize=size,colliders=4,
                renderers=player.GetComponentsInChildren<MeshRenderer>(true).Length,anchorDistance=distance,plateFront=.81f*size.z,ballGap=gap};
        }
        static BoxCollider Box(FivePlayer player,string name,Vector3 center,Vector3 size,PhysicsMaterial material,float contact)
        {
            // Reuse empty legacy nodes only by creating uniquely owned collider nodes.
            var node=new GameObject(name);node.layer=player.gameObject.layer;node.transform.SetParent(player.transform,false);
            var box=node.AddComponent<BoxCollider>();box.center=center;box.size=size;box.sharedMaterial=material;box.contactOffset=contact;
            return box;
        }
    }
}
