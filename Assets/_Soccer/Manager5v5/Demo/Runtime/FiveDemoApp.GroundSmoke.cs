using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed partial class FiveDemoApp
    {
        [Serializable] sealed class KickoffSample {public int round;public Vector3[] positions;}
        [Serializable] sealed class KickoffEvidence {public Vector3[] anchors;public KickoffSample[] samples;}
        IEnumerator GroundSmokeBody()
        {
            ConfigureMatch(true,300,0,0);Kickoff();yield return null;
            session.automaticSteps=false;human.enabled=false;
            var anchors=match.players.Select(p=>p.spawn).ToArray();
            var evidence=new KickoffEvidence{anchors=anchors,samples=new KickoffSample[101]};
            bool inside=true,keepersFixed=true,separate=true,changed=true,anchorsPreserved=true;
            Vector3[] previous=null;
            var randomState=JsonUtility.ToJson(UnityEngine.Random.state);
            for(int round=0;round<101;round++)
            {
                if(round%2==0)match.ResetMatch();
                else{match.Score(round%4==1?Team.Red:Team.Navy);match.Tick(match.PauseRemaining+.001f);}
                var positions=match.players.Select(p=>p.Body.position).ToArray();
                evidence.samples[round]=new KickoffSample{round=round,positions=positions};
                for(int i=0;i<10;i++)
                {
                    var p=match.players[i];var position=positions[i];
                    anchorsPreserved&=p.spawn==anchors[i];
                    if(p.IsKeeper)keepersFixed&=position==anchors[i];
                    else
                    {
                        inside&=Vector3.Distance(position,anchors[i])<=2.50001f&&Mathf.Abs(position.y-anchors[i].y)<.0001f
                            &&Mathf.Abs(position.x)<match.geometry.HalfLength-1.49f&&Mathf.Abs(position.z)<match.geometry.HalfWidth-1.49f
                            &&Mathf.Sign(position.x)==Mathf.Sign(anchors[i].x);
                        if(previous!=null)changed&=Vector3.Distance(position,previous[i])>.00001f;
                    }
                    for(int j=0;j<i;j++)separate&=Vector3.Distance(position,positions[j])>=2.9999f;
                }
                previous=positions;
            }
            File.WriteAllText(Path.Combine(smokeDirectory,"kickoff-layouts.json"),JsonUtility.ToJson(evidence,true));
            CheckDemo(inside&&keepersFixed&&anchorsPreserved,"kickoff-field-radius-2point5-keepers-fixed-anchors-preserved","inside="+inside+" keepers="+keepersFixed+" anchors="+anchorsPreserved);
            CheckDemo(separate&&changed,"kickoff-101-start-and-goal-resets-vary-without-overlap");
            CheckDemo(randomState==JsonUtility.ToJson(UnityEngine.Random.state),"kickoff-layout-keeps-unity-random-stream");
            var ground=arena.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Raised surrounding floor — visual only");
            CheckDemo(ground.sharedMesh.vertices.All(v=>Mathf.Abs(ground.transform.TransformPoint(v).y+.01f)<.0001f)
                &&ground.GetComponent<Collider>()==null,"surrounding-floor-one-centimetre-below-pitch");
            var benches=arena.GetComponentsInChildren<MeshFilter>().Where(m=>m.name.StartsWith("SM_Chair_Canopy")).ToArray();
            CheckDemo(benches.Length==4&&benches.All(m=>Mathf.Abs(m.GetComponent<Renderer>().bounds.min.y+.7241907f)<.001f),"four-benches-restored-original-height");
            var substitutes=arena.GetComponent<FiveDemoBenchSpectators>();
            CheckDemo(substitutes.Groups.SelectMany(g=>g.models).All(t=>Mathf.Abs(FiveDemoBenchSpectators.ModelBounds(t).min.y+.01f)<.001f),"six-bench-players-feet-aligned-to-raised-floor");
            foreach(var view in new[]{FiveDemoCamera.View.Broadcast,FiveDemoCamera.View.RedKeeper,FiveDemoCamera.View.NavyKeeper,FiveDemoCamera.View.RedStriker,FiveDemoCamera.View.Broadcast})
            {
                cameras.Select(view,false);yield return new WaitForSecondsRealtime(.15f);
                CheckDemo(screen.Q<Label>("match-brand").resolvedStyle.color==Color.white,"brand-white-all-cameras-"+view);
            }
            yield return Capture("lowered-ground-pale-lime-brand");
            var surfaceChecks=SurfaceSmokeBody();while(surfaceChecks.MoveNext())yield return surfaceChecks.Current;
            Home();yield return null;
        }
    }
}
