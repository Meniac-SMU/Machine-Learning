using System.Collections.Generic;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    [DefaultExecutionOrder(50)]
    public sealed class FiveDemoMotion : MonoBehaviour
    {
        sealed class Visual
        {
            public Rigidbody body;
            public Transform target;
            public Renderer[] sources,copies;
            public bool[] originalHidden;
            public Vector3 previousPosition,currentPosition;
            public Quaternion previousRotation,currentRotation;
        }
        readonly List<Visual> visuals=new();
        FiveDemoSession session;
        Transform displayRoot;
        double fixedTime;
        float stepDuration;
        FiveMatchState previousState;
        float previousElapsed;
        public Transform PlayerTarget(int index)=>visuals[index].target;
        public Renderer[] PlayerRenderers(int index)=>visuals[index].copies;
        public Transform BallTarget=>visuals[10].target;
        public float Fraction { get; private set; }

        public void Initialize(FiveDemoSession current)
        {
            session=current;
            displayRoot=new GameObject("Interpolated match visuals").transform;
            displayRoot.SetParent(transform,false);
            foreach(var player in session.match.players)Add(player.Body);
            Add(session.match.ball);
            Snap();
        }
        void Add(Rigidbody body)
        {
            var target=new GameObject(body.name+" display").transform;
            target.SetParent(displayRoot,false);target.localScale=body.transform.lossyScale;
            target.SetPositionAndRotation(body.position,body.rotation);
            var sources=body.GetComponentsInChildren<MeshRenderer>(true);
            var visual=new Visual{body=body,target=target,sources=sources,copies=new Renderer[sources.Length],originalHidden=new bool[sources.Length]};
            for(int i=0;i<sources.Length;i++)
            {
                var source=sources[i];var mesh=source.GetComponent<MeshFilter>();
                if(mesh==null)throw new System.InvalidOperationException("Demo visual requires a mesh: "+source.name);
                var node=new GameObject(source.name);node.layer=source.gameObject.layer;node.transform.SetParent(target,false);
                node.transform.localPosition=body.transform.InverseTransformPoint(source.transform.position);
                node.transform.localRotation=Quaternion.Inverse(body.transform.rotation)*source.transform.rotation;
                var scale=source.transform.lossyScale;var parentScale=body.transform.lossyScale;
                node.transform.localScale=new Vector3(scale.x/parentScale.x,scale.y/parentScale.y,scale.z/parentScale.z);
                node.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;
                var copy=node.AddComponent<MeshRenderer>();copy.sharedMaterials=source.sharedMaterials;
                copy.shadowCastingMode=source.shadowCastingMode;copy.receiveShadows=source.receiveShadows;
                copy.lightProbeUsage=source.lightProbeUsage;copy.reflectionProbeUsage=source.reflectionProbeUsage;
                copy.renderingLayerMask=source.renderingLayerMask;copy.motionVectorGenerationMode=source.motionVectorGenerationMode;
                visual.copies[i]=copy;visual.originalHidden[i]=source.forceRenderingOff;source.forceRenderingOff=true;
            }
            visuals.Add(visual);
        }
        public void BeginStep()
        {
            previousState=session.match.State;previousElapsed=session.match.Elapsed;
            foreach(var visual in visuals)
            {
                visual.previousPosition=visual.body.position;visual.previousRotation=visual.body.rotation;
            }
        }
        public void EndStep(float seconds)
        {
            stepDuration=seconds;fixedTime=Time.fixedTimeAsDouble;
            bool reset=session.match.Elapsed<previousElapsed||session.match.State!=previousState;
            foreach(var visual in visuals)
            {
                visual.currentPosition=visual.body.position;visual.currentRotation=visual.body.rotation;
                if(reset||Vector3.Distance(visual.previousPosition,visual.currentPosition)>4)
                {visual.previousPosition=visual.currentPosition;visual.previousRotation=visual.currentRotation;}
            }
        }
        void Snap()
        {
            foreach(var visual in visuals)
            {
                visual.previousPosition=visual.currentPosition=visual.body.position;
                visual.previousRotation=visual.currentRotation=visual.body.rotation;
                visual.target.SetPositionAndRotation(visual.currentPosition,visual.currentRotation);
            }
            fixedTime=Time.fixedTimeAsDouble;stepDuration=0;Fraction=1;
        }
        void LateUpdate()
        {
            if(session==null||!session.Ready)return;
            if(!session.automaticSteps)Snap();
            Fraction=stepDuration>0?Mathf.Clamp01((float)((Time.timeAsDouble-fixedTime)/stepDuration)):1;
            foreach(var visual in visuals)
            {
                // Render-only meshes have no colliders or scripts; simulation transforms remain untouched.
                if(Vector3.Distance(visual.body.position,visual.currentPosition)>4)
                {visual.previousPosition=visual.currentPosition=visual.body.position;visual.previousRotation=visual.currentRotation=visual.body.rotation;}
                visual.target.SetPositionAndRotation(Vector3.Lerp(visual.previousPosition,visual.currentPosition,Fraction),
                    Quaternion.Slerp(visual.previousRotation,visual.currentRotation,Fraction));
                for(int i=0;i<visual.copies.Length;i++)
                    visual.copies[i].enabled=visual.sources[i].enabled&&visual.sources[i].gameObject.activeInHierarchy&&!visual.originalHidden[i];
            }
        }
        void OnDisable()
        {
            foreach(var visual in visuals)for(int i=0;i<visual.sources.Length;i++)
                if(visual.sources[i]!=null)visual.sources[i].forceRenderingOff=visual.originalHidden[i];
            if(displayRoot!=null)displayRoot.gameObject.SetActive(false);
        }
    }
}
