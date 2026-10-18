using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed partial class FiveDemoApp
    {
        [Serializable] sealed class FloorFrame {public string team;public int frame,samples,beforeMismatch,afterMismatch;public float surfaceGap;}
        [Serializable] sealed class FloorReport {public FloorFrame[] frames;public int samples,beforeMismatch,afterMismatch;}
        IEnumerator SurfaceSmokeBody()
        {
            var walls=arena.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name.StartsWith("Wall_",StringComparison.Ordinal)).ToArray();
            CheckDemo(walls.Length==18&&walls.All(r=>Mathf.Abs(r.sharedMaterial.GetColor("_BaseColor").a-.1f)<.00001f),"wall-transparency-ninety-percent");
            var ground=arena.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Raised surrounding floor — visual only");
            var renderer=ground.GetComponent<Renderer>();var original=ground.transform.position;
            var camera=new GameObject("Goal floor verification camera").AddComponent<Camera>();camera.enabled=false;
            camera.fieldOfView=60;camera.nearClipPlane=.1f;camera.farClipPlane=500;camera.allowHDR=false;
            var rt=new RenderTexture(640,360,24,RenderTextureFormat.ARGB32);camera.targetTexture=rt;
            var texture=new Texture2D(640,360,TextureFormat.RGB24,false);
            var frames=new List<FloorFrame>();
            Color32[] Render(string name=null)
            {
                var previous=RenderTexture.active;camera.Render();RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,640,360),0,0);texture.Apply();RenderTexture.active=previous;
                if(name!=null)File.WriteAllBytes(Path.Combine(smokeDirectory,name+".png"),texture.EncodeToPNG());
                return texture.GetPixels32();
            }
            bool Different(Color32 a,Color32 b)=>Mathf.Abs(a.r-b.r)+Mathf.Abs(a.g-b.g)+Mathf.Abs(a.b-b.b)>8;
            foreach(var team in new[]{"Red","Navy"})
            {
                var floor=arena.GetComponentsInChildren<MeshRenderer>().Single(r=>r.name==team+"_Goal_Floor");
                var material=floor.sharedMaterial;
                CheckDemo(material.GetColor("_BaseColor")==C(team=="Red"?"#E9A3AC":"#9AAAC9")
                    &&material.GetFloat("_Surface")==0&&material.GetFloat("_ZWrite")==1&&material.renderQueue==2000,
                    "goal-floor-opaque-pale-team-color-"+team);
                var b=floor.bounds;float sign=Mathf.Sign(b.center.x);
                for(int frame=0;frame<24;frame++)
                {
                    float angle=frame*Mathf.PI*2/24;
                    camera.transform.position=new Vector3(sign*(match.geometry.HalfLength-(frame<12?14:75)),frame<12?5+2*Mathf.Sin(angle):45,12*Mathf.Cos(angle));
                    camera.transform.LookAt(new Vector3(b.center.x,0,0));
                    // Compare with a safely separated backing surface at the same camera pose.
                    ground.transform.position=original-Vector3.up*.01f;
                    var reference=Render(frame%8==0?"goal-floor-"+team+"-reference-"+frame:null);
                    ground.transform.position=original+Vector3.up*.01f;
                    var before=Render(frame%8==0?"goal-floor-"+team+"-before-"+frame:null);
                    ground.transform.position=original;
                    var after=Render(frame%8==0?"goal-floor-"+team+"-after-"+frame:null);
                    var row=new FloorFrame{team=team,frame=frame,surfaceGap=b.max.y-ground.GetComponent<Renderer>().bounds.max.y};
                    for(int x=0;x<12;x++)for(int z=0;z<32;z++)
                    {
                        var point=new Vector3(Mathf.Lerp(b.min.x+.5f,b.max.x-.5f,(x+.5f)/12),b.max.y,Mathf.Lerp(b.min.z+1,b.max.z-1,(z+.5f)/32));
                        var view=camera.WorldToViewportPoint(point);if(view.z<=0||view.x<.01f||view.x>.99f||view.y<.01f||view.y>.99f)continue;
                        int pixel=Mathf.FloorToInt(view.y*360)*640+Mathf.FloorToInt(view.x*640);
                        row.samples++;if(Different(before[pixel],reference[pixel]))row.beforeMismatch++;
                        if(Different(after[pixel],reference[pixel]))row.afterMismatch++;
                    }
                    frames.Add(row);yield return null;
                }
            }
            camera.targetTexture=null;Destroy(camera.gameObject);rt.Release();Destroy(rt);Destroy(texture);
            var report=new FloorReport{frames=frames.ToArray(),samples=frames.Sum(f=>f.samples),beforeMismatch=frames.Sum(f=>f.beforeMismatch),afterMismatch=frames.Sum(f=>f.afterMismatch)};
            File.WriteAllText(Path.Combine(smokeDirectory,"goal-floor-render-check.json"),JsonUtility.ToJson(report,true));
            CheckDemo(frames.All(f=>f.surfaceGap>.0099f),"both-goal-floors-have-one-centimetre-depth-separation");
            CheckDemo(report.samples>10000&&report.afterMismatch<=report.samples*.005f&&report.beforeMismatch>report.afterMismatch,
                "goal-floor-coplanar-artifact-removed-across-48-camera-positions","samples="+report.samples+" before="+report.beforeMismatch+" after="+report.afterMismatch);
        }
    }
}
