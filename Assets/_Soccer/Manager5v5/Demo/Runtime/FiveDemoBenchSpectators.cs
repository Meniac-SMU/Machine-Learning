using System;
using System.Linq;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    // Presentation only: never instantiate a player, AI agent, collider, sensor, or Rigidbody.
    public sealed class FiveDemoBenchSpectators : MonoBehaviour
    {
        internal sealed class Group
        {
            public int team,keeperSlot;
            public Bounds bench;
            public Rect area;
            public float radius,spacing,travel,depth;
            public readonly float[] targets=new float[3],remaining=new float[3];
            public readonly bool[] moving=new bool[3];
            public readonly Transform[] models=new Transform[3];
            public readonly GameObject[] sources=new GameObject[3];
        }

        const int VisualLayer=2; // Ignore Raycast; every descendant is also Untagged and collider-free.
        FiveMatch match;
        System.Random motionRandom;
        internal Transform VisualRoot { get; private set; }
        internal Group[] Groups { get; private set; }

        public void Initialize(FiveMatch owner,FiveDemoAssets.CharacterSet red,FiveDemoAssets.CharacterSet navy)
        {
            match=owner;
            var camera=match.overview;
            // Resolve the upper pair once from the initial broadcast view, not from the current player camera.
            var benches=match.GetComponentsInChildren<MeshFilter>(true)
                .Where(m=>m.name.StartsWith("SM_Chair_Canopy",StringComparison.Ordinal))
                .Select(m=>m.GetComponent<Renderer>().bounds)
                .OrderByDescending(b=>camera.WorldToViewportPoint(b.center).y).Take(2)
                .OrderBy(b=>camera.WorldToViewportPoint(b.center).x).ToArray();
            if(benches.Length!=2)throw new InvalidOperationException("Two upper benches are required.");
            VisualRoot=NewVisual("Bench substitutes — visuals only",transform);
            Groups=new Group[2];
            // An independent generator avoids consuming the gameplay/training Unity random sequence.
            motionRandom=new System.Random(Guid.NewGuid().GetHashCode());
            var keeperSlots=DrawKeeperSlots(motionRandom);
            for(int team=0;team<2;team++)
            {
                var set=team==0?red:navy;
                var group=new Group{team=team,keeperSlot=keeperSlots[team],bench=benches[team]};
                Groups[team]=group;
                var groupRoot=NewVisual(team==0?"RED bench":"NAVY bench",VisualRoot);
                for(int slot=0;slot<3;slot++)
                {
                    var source=slot==group.keeperSlot?set.goalkeeper:set.fieldPlayer;
                    var model=CopyMeshHierarchy(source.transform,groupRoot);
                    model.name="Substitute "+(slot+1)+(slot==group.keeperSlot?" — goalkeeper":" — outfield");
                    // Match the field player's world scale, including the existing 0.9 m body size.
                    var player=match.players.First(p=>(int)p.team==team&&p.IsKeeper==(slot==group.keeperSlot));
                    model.localScale=Vector3.Scale(model.localScale,player.transform.lossyScale);
                    var bounds=ModelBounds(model);
                    model.position+=Vector3.up*(FiveDemoAssets.SurroundingFloorY-bounds.min.y);
                    float reachX=Mathf.Max(Mathf.Abs(bounds.min.x-model.position.x),Mathf.Abs(bounds.max.x-model.position.x));
                    float reachZ=Mathf.Max(Mathf.Abs(bounds.min.z-model.position.z),Mathf.Abs(bounds.max.z-model.position.z));
                    group.radius=Mathf.Max(group.radius,new Vector2(reachX,reachZ).magnitude);
                    group.models[slot]=model;group.sources[slot]=source;
                }
                float side=Mathf.Sign(group.bench.center.z);
                var wall=match.GetComponentsInChildren<Collider>(true).First(c=>c.name==(side>0?"Wall_North":"Wall_South")).bounds;
                float near=side>0?wall.max.z+.2f:group.bench.max.z+.2f;
                float far=side>0?group.bench.min.z-.2f:wall.min.z-.2f;
                float halfWidth=Mathf.Min(15f,group.bench.extents.x-.3f);
                group.area=Rect.MinMaxRect(group.bench.center.x-halfWidth,near,group.bench.center.x+halfWidth,far);
                group.spacing=3f;
                group.travel=halfWidth-group.spacing-group.radius;
                if(far-near<group.radius*2||group.travel<.25f)
                    throw new InvalidOperationException("The bench corridor is too small for three separate substitutes.");
                // Stand immediately in front of the seats, with room for the full rotating model.
                group.depth=side>0?far-group.radius-.08f:near+group.radius+.08f;
                for(int slot=0;slot<3;slot++)
                {
                    var model=group.models[slot];
                    group.targets[slot]=group.area.center.x+(slot-1)*group.spacing;
                    model.position=new Vector3(group.targets[slot],model.position.y,group.depth);
                    group.remaining[slot]=slot*.55f+Range(.1f,.3f);
                }
                Place(group,match.ball.position,0,true);
            }
        }

        internal static int[] DrawKeeperSlots(System.Random random)=>new[]{random.Next(3),random.Next(3)};

        void LateUpdate()
        {
            if(match==null||match.Paused||match.State==FiveMatchState.Finished)return;
            Advance(match.ball.position,Time.deltaTime);
        }

        internal void Advance(Vector3 ballPosition,float seconds)
        {
            if(Groups==null||seconds<=0)return;
            int steps=Mathf.CeilToInt(seconds/.05f);float dt=seconds/steps;
            for(int step=0;step<steps;step++)
            foreach(var group in Groups)
            {
                for(int slot=0;slot<3;slot++)
                {
                    group.remaining[slot]-=dt;
                    if(group.remaining[slot]<=0)
                    {
                        group.moving[slot]=!group.moving[slot];
                        group.remaining[slot]=group.moving[slot]?Range(1.8f,4.2f):Range(.4f,1.4f);
                        if(group.moving[slot])group.targets[slot]=group.area.center.x
                            +Mathf.Clamp(ballPosition.x/match.geometry.HalfLength,-1,1)*group.travel
                            +(slot-1)*group.spacing+Range(-1.2f,1.2f);
                    }
                    if(!group.moving[slot])continue;
                    // Clamp against current neighbours before moving: never push another substitute.
                    float left=slot==0?group.area.xMin+group.radius:group.models[slot-1].position.x+group.spacing;
                    float right=slot==2?group.area.xMax-group.radius:group.models[slot+1].position.x-group.spacing;
                    var model=group.models[slot];var position=model.position;
                    position.x=Mathf.MoveTowards(position.x,Mathf.Clamp(group.targets[slot],left,right),.5f*dt);
                    model.position=position;
                }
                Place(group,ballPosition,dt,false);
            }
        }

        float Range(float min,float max)=>min+(float)motionRandom.NextDouble()*(max-min);

        static void Place(Group group,Vector3 ballPosition,float seconds,bool immediate)
        {
            for(int slot=0;slot<3;slot++)
            {
                var model=group.models[slot];
                var direction=Vector3.ProjectOnPlane(ballPosition-model.position,Vector3.up);
                if(direction.sqrMagnitude<.0001f)continue;
                var rotation=Quaternion.LookRotation(direction);
                model.rotation=immediate?rotation:Quaternion.RotateTowards(model.rotation,rotation,110*seconds);
            }
        }

        static Transform NewVisual(string name,Transform parent)
        {
            var go=new GameObject(name){layer=VisualLayer,tag="Untagged"};
            go.transform.SetParent(parent,false);return go.transform;
        }

        static Transform CopyMeshHierarchy(Transform source,Transform parent)
        {
            var target=NewVisual(source.name,parent);
            target.localPosition=source.localPosition;target.localRotation=source.localRotation;target.localScale=source.localScale;
            var mesh=source.GetComponent<MeshFilter>();var renderer=source.GetComponent<MeshRenderer>();
            if(mesh!=null&&renderer!=null)
            {
                target.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
                var copy=target.gameObject.AddComponent<MeshRenderer>();copy.sharedMaterials=renderer.sharedMaterials;
                copy.shadowCastingMode=renderer.shadowCastingMode;copy.receiveShadows=renderer.receiveShadows;
            }
            foreach(Transform child in source)if(child.gameObject.activeSelf)CopyMeshHierarchy(child,target);
            return target;
        }

        internal static Bounds ModelBounds(Transform model)
        {
            var renderers=model.GetComponentsInChildren<Renderer>();
            if(renderers.Length==0)throw new InvalidOperationException("The selected character has no visible mesh.");
            var bounds=renderers[0].bounds;
            foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
