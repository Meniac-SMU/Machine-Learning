using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed partial class FiveDemoApp
    {
        void CheckMotion(bool passed,string name,string detail="")=>smokeResult.checks.Add(new Check{name=name,passed=passed,detail=detail});
        IEnumerator MotionSmokeBody()
        {
            QualitySettings.vSyncCount=0;
            foreach(int rate in new[]{120,60})
            {
                Application.targetFrameRate=rate;
                ConfigureMatch(false,300,1,1);Kickoff();yield return null;
                human.ToggleOwner();human.enabled=false;
                var player=match.players[3];var motion=match.GetComponent<FiveDemoMotion>();
                var target=motion.PlayerTarget(3);
                var colliders=match.GetComponentsInChildren<Collider>().Length;
                foreach(var other in match.players)
                    if(other!=player){other.Body.isKinematic=true;other.Body.position=new Vector3(-40+other.Index*7,other.Body.position.y,28);}
                player.Body.position=new Vector3(-20,player.Body.position.y,0);player.Body.rotation=Quaternion.LookRotation(Vector3.right);
                match.ball.position=new Vector3(0,match.ball.position.y,-25);match.ball.isKinematic=true;
                Physics.SyncTransforms();match.SetHumanInput(1,0,false,false);
                yield return new WaitForSecondsRealtime(1);
                CheckMotion(cameras.ActiveCamera.Follow==target,"camera-follows-interpolated-striker-"+rate);
                foreach(var p in match.players)
                {
                    CheckMotion(motion.PlayerTarget(p.Index).GetComponentsInChildren<Collider>().Length==0
                        &&motion.PlayerTarget(p.Index).GetComponentsInChildren<Rigidbody>().Length==0,"visual-only-no-physics-"+rate+"-"+p.Index);
                    CheckMotion(motion.PlayerRenderers(p.Index).Length==p.GetComponentsInChildren<MeshRenderer>(true).Length,"all-player-meshes-preserved-"+rate+"-"+p.Index);
                }
                CheckMotion(motion.BallTarget.GetComponentsInChildren<Collider>().Length==0,"ball-visual-only-"+rate);
                var rows=new List<string>{"frame,time,fixedTime,dt,bodyX,nativeX,renderX,bodySpeed,fraction"};
                var speeds=new List<double>();int stalls=0;double previousTime=0;float previousX=0;
                for(int i=0;i<rate*2;i++)
                {
                    yield return new WaitForEndOfFrame();
                    double now=Time.timeAsDouble;float x=target.position.x;
                    rows.Add(FormattableString.Invariant($"{Time.frameCount},{now:R},{Time.fixedTimeAsDouble:R},{Time.deltaTime:R},{player.Body.position.x:R},{player.transform.position.x:R},{x:R},{player.Body.linearVelocity.x:R},{motion.Fraction:R}"));
                    if(i>0&&now>previousTime)
                    {speeds.Add((x-previousX)/(now-previousTime));if(Mathf.Abs(x-previousX)<.00001f)stalls++;}
                    previousTime=now;previousX=x;
                }
                File.WriteAllLines(Path.Combine(smokeDirectory,"motion-"+rate+".csv"),rows);
                double mean=speeds.Average(),std=Math.Sqrt(speeds.Average(s=>(s-mean)*(s-mean)));
                CheckMotion(mean>7&&stalls==0&&std/mean<.12,"continuous-render-motion-"+rate,
                    FormattableString.Invariant($"frames={speeds.Count}; stalls={stalls}; speed={mean:R}; std={std:R}; CV={std/mean:R}"));
                var bodies=match.players.Select(p=>p.Body).Concat(new[]{match.ball}).ToArray();
                var positions=bodies.Select(b=>b.position).ToArray();var rotations=bodies.Select(b=>b.rotation).ToArray();
                var velocities=bodies.Select(b=>b.linearVelocity).ToArray();
                motion.SendMessage("LateUpdate");Physics.SyncTransforms();
                CheckMotion(bodies.Select((b,i)=>b.position==positions[i]&&b.rotation==rotations[i]&&b.linearVelocity==velocities[i]).All(v=>v)
                    &&colliders==match.GetComponentsInChildren<Collider>().Length,"rendering-does-not-change-physics-"+rate);
                match.SetHumanInput(1,.6f,false,false);yield return new WaitForSecondsRealtime(.3f);
                int intermediateRotations=0;
                for(int i=0;i<30;i++)
                {
                    yield return new WaitForEndOfFrame();
                    if(Quaternion.Angle(target.rotation,player.Body.rotation)>.05f)intermediateRotations++;
                    CheckMotion(Quaternion.Angle(target.rotation,player.Body.rotation)<6,"rotation-delay-bounded-"+rate+"-"+i);
                }
                CheckMotion(intermediateRotations>0,"rotation-interpolates-between-physics-"+rate);
                TogglePause();yield return new WaitForEndOfFrame();
                var held=target.position;var heldRotation=target.rotation;
                yield return new WaitForSecondsRealtime(.2f);
                CheckMotion(target.position==held&&target.rotation==heldRotation,"pause-freezes-visible-pose-"+rate);
                TogglePause();human.enabled=false;match.SetHumanInput(1,0,false,false);
                yield return new WaitForSecondsRealtime(.1f);
                CheckMotion(Vector3.Distance(target.position,held)>0,"resume-continues-visible-motion-"+rate);
                session.automaticSteps=false;match.ResetRound();yield return null;yield return new WaitForEndOfFrame();
                CheckMotion(match.players.All(p=>Vector3.Distance(motion.PlayerTarget(p.Index).position,p.Body.position)<.001f)
                    &&Vector3.Distance(motion.BallTarget.position,match.ball.position)<.001f,"reset-snaps-without-trailing-"+rate);
                if(human.IsHuman)human.ToggleOwner();
                yield return new WaitForSecondsRealtime(.9f);
                foreach(var view in new[]{FiveDemoCamera.View.RedKeeper,FiveDemoCamera.View.NavyKeeper})
                {
                    cameras.Select(view,false);yield return null;
                    CheckMotion(cameras.ActiveCamera.Follow==motion.PlayerTarget(view==FiveDemoCamera.View.RedKeeper?0:5),"keeper-camera-interpolated-target-"+rate+"-"+view);
                }
                Home();yield return null;
            }
            Application.targetFrameRate=60;
            ConfigureMatch(true,300,FiveDemoAssets.LatestModelId,FiveDemoAssets.LatestModelId-1);Kickoff();
            yield return new WaitForSecondsRealtime(5);
            var allMotion=match.GetComponent<FiveDemoMotion>();
            CheckMotion(match.players.All(p=>Vector3.Distance(allMotion.PlayerTarget(p.Index).position,p.Body.position)<1)
                &&Vector3.Distance(allMotion.BallTarget.position,match.ball.position)<2,"live-all-eleven-render-poses-bounded");
            yield return Capture("motion-live-broadcast");
            CheckStaminaLayout("motion-live-broadcast");
            Home();yield return null;
        }
    }
}
