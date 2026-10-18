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
        [Serializable] sealed class BenchEvidence
        {
            public int team,keeperSlot;
            public Bounds bench;
            public Rect area;
            public float radius,spacing,travel;
            public string[] sources;
            public Vector3[] positions;
        }
        [Serializable] sealed class BenchEvidenceList {public BenchEvidence[] groups;}

        IEnumerator BenchSmokeBody()
        {
            Home();SetLanguage("ko");ConfigureMatch(true,300,0,0);Kickoff();yield return null;
            CheckDemo(Page=="match","bench-match-started");
            session.automaticSteps=false;human.enabled=false;
            var bench=arena.GetComponent<FiveDemoBenchSpectators>();bench.enabled=false;
            var root=bench.VisualRoot;
            CheckDemo(bench.Groups.Length==2&&bench.Groups.All(g=>g.models.Length==3),"six-bench-substitutes");
            CheckDemo(root.GetComponentsInChildren<Component>(true).All(c=>c is Transform||c is MeshFilter||c is MeshRenderer),"bench-components-render-only");
            CheckDemo(root.GetComponentsInChildren<Transform>(true).All(t=>t.gameObject.layer==2&&t.CompareTag("Untagged")),"bench-no-player-tags-or-detection-layer");
            CheckDemo(match.players.Length==10&&arena.GetComponentsInChildren<FivePlayer>(true).Length==10
                &&arena.GetComponentsInChildren<Collider>(true).Length==assets.arena.GetComponentsInChildren<Collider>(true).Length
                &&arena.GetComponentsInChildren<Rigidbody>(true).Length==assets.arena.GetComponentsInChildren<Rigidbody>(true).Length,"bench-gameplay-roster-and-physics-counts-unchanged");
            var upper=arena.GetComponentsInChildren<MeshFilter>().Where(m=>m.name.StartsWith("SM_Chair_Canopy"))
                .Select(m=>m.GetComponent<Renderer>().bounds).OrderByDescending(b=>match.overview.WorldToViewportPoint(b.center).y).Take(2).ToArray();
            CheckDemo(bench.Groups.All(g=>upper.Any(b=>b==g.bench)),"substitutes-at-upper-broadcast-benches");
            CheckDemo(!bench.Groups[0].area.Overlaps(bench.Groups[1].area),"team-bench-areas-separate");
            CheckDemo(bench.Groups.All(g=>Mathf.Abs(g.area.width-30)<.001f&&g.travel>11f&&g.spacing==3),"bench-thirty-metre-area-three-metre-gap");
            bench.Advance(new Vector3(-62,1,0),100);
            bool speedValid=true,fullSpeed=false,independent=false,variableGap=false;
            for(int frame=0;frame<2400;frame++)
            {
                var before=bench.Groups.SelectMany(g=>g.models).Select(m=>m.position.x).ToArray();
                bench.Advance(new Vector3(10000,1,0),.02f);
                var delta=bench.Groups.SelectMany(g=>g.models).Select((m,i)=>Mathf.Abs(m.position.x-before[i])).ToArray();
                speedValid&=delta.All(d=>d<=.01001f);fullSpeed|=delta.Any(d=>Mathf.Abs(d-.01f)<.00001f);
                for(int team=0;team<2;team++)
                {
                    independent|=delta.Skip(team*3).Take(3).Any(d=>d<.00001f)&&delta.Skip(team*3).Take(3).Any(d=>d>.005f);
                    var g=bench.Groups[team];variableGap|=Mathf.Abs((g.models[1].position.x-g.models[0].position.x)-(g.models[2].position.x-g.models[1].position.x))>.15f;
                }
            }
            CheckDemo(speedValid&&fullSpeed,"bench-movement-half-metre-per-second");
            CheckDemo(independent&&variableGap,"bench-independent-pauses-and-variable-positions");
            bench.Advance(new Vector3(62,1,0),100);
            CheckDemo(bench.Groups.All(g=>g.models[1].position.x-g.area.center.x>g.travel-2),"bench-reaches-expanded-right-area");
            bench.Advance(new Vector3(-62,1,0),100);
            CheckDemo(bench.Groups.All(g=>g.models[1].position.x-g.area.center.x<-g.travel+2),"bench-reaches-expanded-left-area");
            var random=new System.Random(7117);var pairs=new HashSet<string>();
            for(int i=0;i<120;i++)
            {
                var slots=FiveDemoBenchSpectators.DrawKeeperSlots(random);
                pairs.Add(slots[0]+"/"+slots[1]);
            }
            CheckDemo(pairs.Count==9,"independent-keeper-draw-all-nine-pairs",string.Join(",",pairs.OrderBy(p=>p)));
            var savedRandom=JsonUtility.ToJson(UnityEngine.Random.state);
            var probe=new GameObject("Temporary substitute isolation probe");
            probe.AddComponent<FiveDemoBenchSpectators>().Initialize(match,CharacterSetForTeam(0),CharacterSetForTeam(1));
            CheckDemo(savedRandom==JsonUtility.ToJson(UnityEngine.Random.state),"bench-generation-preserves-gameplay-random");
            DestroyImmediate(probe);
            foreach(var group in bench.Groups)
            {
                var set=CharacterSetForTeam(group.team);
                CheckDemo(group.sources.Count(s=>s==set.goalkeeper)==1&&group.sources.Count(s=>s==set.fieldPlayer)==2
                    &&group.sources[group.keeperSlot]==set.goalkeeper,"bench-selected-set-two-outfield-one-keeper-"+group.team);
                for(int slot=0;slot<3;slot++)
                {
                    var actual=group.models[slot].GetComponentInChildren<MeshRenderer>();var original=group.sources[slot].GetComponentInChildren<MeshRenderer>();
                    CheckDemo(actual.sharedMaterials.SequenceEqual(original.sharedMaterials)
                        &&actual.GetComponent<MeshFilter>().sharedMesh==original.GetComponent<MeshFilter>().sharedMesh,"bench-original-mesh-and-materials-"+group.team+slot);
                    CheckDemo(Vector3.Distance(group.models[slot].lossyScale,match.players[group.team*5].transform.lossyScale)<.0001f,"bench-player-scale-preserved-"+group.team+slot);
                }
            }
            var poses=new[]{new Vector3(-62,1,0),new Vector3(62,1,0),new Vector3(0,1,-42),new Vector3(0,1,42),new Vector3(-10000,1,10000),new Vector3(10000,1,-10000)};
            foreach(var target in poses)
            {
                bool bounded=true,separate=true,facing=true;
                var before=bench.Groups.Select(g=>g.models[1].position.x).ToArray();
                for(int frame=0;frame<3000;frame++)
                {
                    bench.Advance(target,1/60f);
                    foreach(var group in bench.Groups)
                    {
                        foreach(var model in group.models)
                        {
                            var bounds=FiveDemoBenchSpectators.ModelBounds(model);
                            bounded&=bounds.min.x>=group.area.xMin&&bounds.max.x<=group.area.xMax&&bounds.min.z>=group.area.yMin&&bounds.max.z<=group.area.yMax;
                        }
                        for(int i=0;i<2;i++)separate&=Vector3.Distance(group.models[i].position,group.models[i+1].position)>=2.9999f;
                    }
                }
                foreach(var group in bench.Groups)foreach(var model in group.models)
                    facing&=Vector3.Dot(model.forward,Vector3.ProjectOnPlane(target-model.position,Vector3.up).normalized)>.999f;
                CheckDemo(bounded&&separate&&facing,"bench-motion-bounds-gap-look-"+target,"bounded="+bounded+" separate="+separate+" facing="+facing);
                if(target.x==62)CheckDemo(bench.Groups.Select((g,i)=>g.models[1].position.x-before[i]>.2f).All(v=>v),"both-benches-follow-ball-laterally");
            }
            foreach(float x in new[]{-62f,62f})
            {
                match.ball.position=new Vector3(x,match.ballSpawn.y,0);bench.Advance(match.ball.position,100);
                yield return Capture(x<0?"bench-wide-left":"bench-wide-right");
            }
            match.ResetMatch();session.automaticSteps=false;
            bench.Advance(match.ball.position,100);
            bench.enabled=true;match.Paused=true;
            var paused=bench.Groups.SelectMany(g=>g.models).Select(t=>t.position).ToArray();
            match.ball.position+=Vector3.right*60;yield return new WaitForSecondsRealtime(.2f);
            CheckDemo(bench.Groups.SelectMany(g=>g.models).Select(t=>t.position).SequenceEqual(paused),"bench-pauses-with-match");
            match.Paused=false;match.ResetMatch();
            yield return Capture("bench-1920-broadcast");
            var evidence=new BenchEvidenceList{groups=bench.Groups.Select(g=>new BenchEvidence{team=g.team,keeperSlot=g.keeperSlot,bench=g.bench,area=g.area,radius=g.radius,spacing=g.spacing,travel=g.travel,sources=g.sources.Select(s=>s.name).ToArray(),positions=g.models.Select(t=>t.position).ToArray()}).ToArray()};
            File.WriteAllText(Path.Combine(smokeDirectory,"bench-substitutes.json"),JsonUtility.ToJson(evidence,true));
            var camera=new GameObject("Bench evidence camera").AddComponent<Camera>();camera.enabled=false;
            camera.orthographic=true;camera.orthographicSize=4.7f;camera.allowHDR=true;
            foreach(var group in bench.Groups)
            {
                var center=new Vector3(group.area.center.x,.4f,group.depth);
                camera.transform.position=center+new Vector3(3,8.5f,-10);camera.transform.LookAt(center);
                RobotCameraCapture(camera,group.team==0?"bench-red-closeup":"bench-navy-closeup");
            }
            Destroy(camera.gameObject);Home();yield return null;
            // Compare each visual update against the same physics state. Separate PhysX runs are not
            // bitwise replayable, even with the bench OFF in both controls (see retained r002 evidence).
            ConfigureMatch(true,300,0,0);Kickoff();session.automaticSteps=false;human.enabled=false;
            bench=arena.GetComponent<FiveDemoBenchSpectators>();bench.enabled=false;
            RobotReset();RobotPlace(3,Vector2.zero,Vector2.right);
            match.ball.position=new Vector3(3,match.ballSpawn.y,0);match.ball.linearVelocity=new Vector3(5,0,2);Physics.SyncTransforms();
            var bodies=arena.GetComponentsInChildren<Rigidbody>();
            Vector3[] BodyState()=>bodies.SelectMany(b=>new[]{b.position,b.rotation.eulerAngles,b.linearVelocity,b.angularVelocity}).ToArray();
            var colliders=arena.GetComponentsInChildren<Collider>().Select(c=>c.GetInstanceID()).ToArray();
            var history=new FiveObservationHistory();history.Reset(match);
            bool unchangedBodies=true,unchangedObservations=true,unchangedColliders=true;
            for(int frame=0;frame<240;frame++)
            {
                match.players[3].SetManualDrive(.4f,0);RobotStep(1);history.Record(match);
                var state=BodyState();
                var redObservation=history.Capture(match,Team.Red).values;var navyObservation=history.Capture(match,Team.Navy).values;
                bench.VisualRoot.gameObject.SetActive(frame%2==0);bench.Advance(match.ball.position,.02f);Physics.SyncTransforms();
                unchangedBodies&=state.SequenceEqual(BodyState());
                unchangedObservations&=redObservation.SequenceEqual(history.Capture(match,Team.Red).values)&&navyObservation.SequenceEqual(history.Capture(match,Team.Navy).values);
                unchangedColliders&=colliders.SequenceEqual(arena.GetComponentsInChildren<Collider>().Select(c=>c.GetInstanceID()));
            }
            CheckDemo(unchangedBodies,"bench-updates-never-write-gameplay-physics","240 live physics steps; 11 bodies: position, rotation, linear/angular velocity");
            CheckDemo(unchangedObservations,"bench-invisible-to-both-manager-observations","480 complete observations x 676 values unchanged");
            CheckDemo(unchangedColliders,"bench-toggle-and-motion-preserve-collision-targets","240 steps: identical collider identities");
            Home();yield return null;
        }
    }
}
