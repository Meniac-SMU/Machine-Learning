using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed partial class FiveDemoApp
    {
        void RobotPlace(int index,Vector2 position,Vector2 forward)
        {
            var p=match.players[index];var point=new Vector3(position.x,p.spawn.y,position.y);
            var rotation=Quaternion.LookRotation(new Vector3(forward.x,0,forward.y));
            p.transform.SetPositionAndRotation(point,rotation);p.Body.position=point;p.Body.rotation=rotation;
            p.Body.linearVelocity=Vector3.zero;p.Body.angularVelocity=Vector3.zero;
            p.SetTarget(position,position+forward*10);Physics.SyncTransforms();
        }
        void RobotReset()
        {
            match.ResetMatch();match.ruleManagers=new[]{false,false};match.tacticsEnabled=false;
            match.keepersEnabled=false;match.contestReliefEnabled=false;
            for(int i=0;i<10;i++)RobotPlace(i,new Vector2(-45+i*9,32),Vector2.right);
        }
        void RobotStep(int ticks){for(int i=0;i<ticks;i++)match.SimulatePhysicsStep(.02f);}
        void RobotGive(int index)
        {
            var anchor=match.Anchor(match.players[index]);match.ball.position=new Vector3(anchor.x,match.ballSpawn.y,anchor.y);
            match.ball.linearVelocity=Vector3.zero;Physics.SyncTransforms();RobotStep(8);
            CheckDemo(match.Owner==index,"robot-acquire-"+index);
        }
        void RobotCameraCapture(Camera camera,string name)
        {
            var rt=new RenderTexture(1600,900,24,RenderTextureFormat.ARGB32);var previous=RenderTexture.active;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var image=new Texture2D(1600,900,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1600,900),0,0);image.Apply();
            File.WriteAllBytes(Path.Combine(smokeDirectory,name+".png"),image.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=previous;Destroy(image);rt.Release();Destroy(rt);
        }
        IEnumerator RobotSmokeBody()
        {
            ConfigureMatch(false,300,0,0);Kickoff();yield return null;
            session.automaticSteps=false;human.enabled=false;
            var motion=match.GetComponent<FiveDemoMotion>();
            foreach(var p in match.players)
            {
                var boxes=p.GetComponentsInChildren<BoxCollider>();
                CheckDemo(boxes.Length==4&&p.GetComponentsInChildren<Collider>().Length==4,"robot-four-boxes-"+p.Index);
                var body=boxes.Single(b=>b.name=="BodyCollider");
                CheckDemo(Vector3.Distance(Vector3.Scale(body.size,p.transform.lossyScale),Vector3.one*.9f)<.0001f,"robot-body-size-"+p.Index);
                var render=p.GetComponentInChildren<MeshRenderer>();var mesh=render.GetComponent<MeshFilter>().sharedMesh;
                CheckDemo(p.GetComponentsInChildren<MeshRenderer>().Length==1&&mesh.subMeshCount==2,"robot-single-mesh-"+p.Index);
                CheckDemo(render.sharedMaterials.All(m=>m!=null&&m.shader.isSupported),"robot-shaders-"+p.Index);
                var color=render.sharedMaterials[1].GetColor("_BaseColor");
                var expected=p.team==Team.Red?new Color(2.5f,.15f,.3f,1):new Color(.16200816f,.9406553f,2.2827466f,1);
                CheckDemo(Vector4.Distance(color,expected)<.00001f,"robot-eye-intensity-"+p.Index,color.ToString("F6"));
                CheckDemo(p.GetComponentsInChildren<Light>().Length==0,"robot-no-eye-lights-"+p.Index);
                CheckDemo(render.sharedMaterials[0].GetTexture("_BaseMap")!=null,"robot-palette-"+p.Index);
                CheckDemo(motion.PlayerRenderers(p.Index).Length==1&&motion.PlayerTarget(p.Index).GetComponentsInChildren<Collider>().Length==0,"robot-interpolated-display-"+p.Index);
            }
            var cam=new GameObject("Robot verification camera").AddComponent<Camera>();cam.enabled=false;cam.allowHDR=true;
            cam.orthographic=true;cam.orthographicSize=2.15f;cam.transform.position=new Vector3(2.5f,3.1f,8);cam.transform.LookAt(new Vector3(0,.45f,0));
            RobotReset();int[] variants={1,6,0,5};
            for(int i=0;i<4;i++)RobotPlace(variants[i],new Vector2((1.5f-i)*1.5f,0),Vector2.up);
            match.ball.position=new Vector3(0,match.ballSpawn.y,-20);
            yield return null;yield return new WaitForEndOfFrame();RobotCameraCapture(cam,"robots-front");
            cam.transform.position=new Vector3(-2.5f,2.5f,-8);cam.transform.LookAt(new Vector3(0,.45f,0));RobotCameraCapture(cam,"robots-back");
            foreach(int index in variants)
            {
                RobotReset();var forward=index<5?Vector2.right:Vector2.left;RobotPlace(index,Vector2.zero,forward);RobotGive(index);
                var p=match.players[index];p.SetTarget(forward*10,forward*15);int held=0;
                for(int i=0;i<60;i++){p.SetManualDrive(.25f,0);RobotStep(1);if(match.Owner==index)held++;}
                CheckDemo(held>=57&&p.Position.magnitude>.5f,"robot-moving-dribble-"+index,"held="+held+" distance="+p.Position.magnitude);
                var plate=p.GetComponentsInChildren<BoxCollider>().Single(b=>b.name=="KickPlateCenter");
                var gap=Vector3.Distance(plate.ClosestPoint(match.ball.position),match.ball.position)-match.ballRadius;
                CheckDemo(Mathf.Abs(gap)<.045f,"robot-ball-contact-gap-"+index,"gap="+gap);
                cam.orthographic=false;cam.fieldOfView=38;cam.transform.position=p.transform.position+p.transform.forward*3+p.transform.right*2+Vector3.up*1.8f;cam.transform.LookAt(p.transform.position);
                yield return null;yield return new WaitForEndOfFrame();RobotCameraCapture(cam,"dribble-"+index);
                CheckDemo(match.RequestShot(p,p.Position+forward*40),"robot-shot-request-"+index);RobotStep(20);
                CheckDemo(match.Shots==1&&match.Owner<0&&Vector2.Dot(match.BallVelocity,forward)>15&&Vector2.Distance(match.BallPosition,p.Position)>2,"robot-shot-flight-"+index,"velocity="+match.BallVelocity);
                RobotReset();RobotPlace(index,forward*-10,forward);int receiver=index<5?3:8;RobotPlace(receiver,forward*10,-forward);RobotGive(index);
                CheckDemo(match.RequestPass(match.players[index],match.players[receiver]),"robot-pass-request-"+index);
                for(int i=0;i<450&&match.PassReceptions==0;i++)RobotStep(1);
                CheckDemo(match.PassReceptions==1&&match.Owner==receiver,"robot-pass-received-"+index,"owner="+match.Owner+" receptions="+match.PassReceptions);
                yield return null;
            }
            Destroy(cam.gameObject);Home();yield return null;
            // Exercise the shipped latest-model selection and full match loop as well.
            ConfigureMatch(true,300,FiveDemoAssets.LatestModelId,FiveDemoAssets.LatestModelId);Kickoff();yield return null;session.automaticSteps=false;
            int ticks=0;while(match.State!=FiveMatchState.Finished){session.Step(.02f);if(++ticks%25==0)yield return null;if(ticks>30000)throw new InvalidOperationException("Robot match did not finish");}
            yield return null;CheckDemo(Page=="results"&&match.Elapsed>=300,"robot-latest-model-full-match");
            CheckDemo(match.Shots>0&&match.PassReceptions>0,"robot-live-match-techniques","shots="+match.Shots+" receptions="+match.PassReceptions);
            smokeResult.elapsed=match.Elapsed;smokeResult.redScore=match.RedScore;smokeResult.navyScore=match.NavyScore;smokeResult.stats=statistics.teams;
            yield return Capture("robots-match-results");
        }
    }
}
