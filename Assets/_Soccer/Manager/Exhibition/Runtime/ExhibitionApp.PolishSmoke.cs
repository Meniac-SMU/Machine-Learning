using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager.Exhibition
{
    public sealed partial class ExhibitionApp
    {
        IEnumerator PolishChecks()
        {
            var players=arena.GetComponentsInChildren<MNG_PlayerAvatar>();
            var avatar=human.GetComponent<MNG_PlayerAvatar>();
            var motor=avatar.GetComponent<MNG_PlayerMotor>();
            var ball=arena.GetComponentInChildren<MNG_BallControl>();
            var ballBody=ball.GetComponent<Rigidbody>();
            Check(players.Length==8&&players.All(a=>a.Body.interpolation==RigidbodyInterpolation.Interpolate)
                &&ballBody.interpolation==RigidbodyInterpolation.Interpolate,"Eight exhibition players and ball interpolate rendered motion");
            var torso=avatar.GetComponentsInChildren<Renderer>().First(r=>r.name.StartsWith("AgentCube_"));
            var marker=avatar.transform.Find("HumanControlMarker").GetComponent<Renderer>();
            Check(Mathf.Abs(marker.bounds.min.y-torso.bounds.max.y)<.01f,"Human marker rests directly on striker head");
            foreach(var keeper in players.Where(a=>a.Role==MNG_PlayerRole.Keeper))
            {
                var body=keeper.GetComponentsInChildren<Renderer>().First(r=>r.name.StartsWith("AgentCube_"));
                Check(body.sharedMaterial.GetColor("_BaseColor")==C(keeper.Team==Team.Red?"#F05278":"#429CF2"),"Goalkeeper torso color: "+keeper.Team);
            }
            var goals=arena.GetComponentsInChildren<Renderer>().Where(r=>r.CompareTag("redGoal")||r.CompareTag("navyGoal")).ToArray();
            Check(goals.Any(r=>r.CompareTag("redGoal"))&&goals.Any(r=>r.CompareTag("navyGoal"))&&goals.All(r=>r.sharedMaterials.All(m=>Mathf.Approximately(m.GetColor("_BaseColor").a,.40f)&&m.renderQueue==3000))
                &&arena.GetComponentInChildren<SoccerGoalOcclusionFader>()==null,"Both goals keep fixed translucent materials without dynamic fader");
            ball.enabled=false;ballBody.position=new Vector3(20,.6f,20);
            avatar.transform.SetPositionAndRotation(new Vector3(0,.52f,-5),Quaternion.identity);
            avatar.Body.position=avatar.transform.position;avatar.Body.rotation=Quaternion.identity;avatar.Body.linearVelocity=Vector3.zero;
            Physics.SyncTransforms();
            InputSystem.QueueStateEvent(smokeKeyboard,new KeyboardState(Key.W));
            yield return new WaitForSecondsRealtime(1.6f);
            var actualSpeed=new Vector2(avatar.Body.linearVelocity.x,avatar.Body.linearVelocity.z).magnitude;
            Check(Mathf.Approximately(motor.LastDesiredVelocity.magnitude,9)&&actualSpeed>1,"Held W requests the full 9 m/s motor cap before shared damping and friction");
            var samples=new List<string>{"frame,fixedTime,renderZ,bodyZ,frameMs"};int interpolated=0;
            for(int i=0;i<24;i++)
            {
                yield return new WaitForEndOfFrame();
                if(Mathf.Abs(avatar.transform.position.z-avatar.Body.position.z)>.001f)interpolated++;
                samples.Add($"{Time.frameCount},{Time.fixedTimeAsDouble:R},{avatar.transform.position.z:R},{avatar.Body.position.z:R},{Time.unscaledDeltaTime*1000:R}");
            }
            File.WriteAllLines(Path.Combine(smokeDirectory,"interpolation-samples.csv"),samples);
            InputSystem.QueueStateEvent(smokeKeyboard,new KeyboardState());yield return null;
            Check(interpolated>0,"Rendered moving player has interpolated poses between physical positions");
            human.enabled=false;avatar.GetComponent<MNG_PlayerSkillExecutor>().enabled=false;
            var velocities=new List<string>{"owner,tick,speed"};float humanFirst=0,aiFirst=0;
            foreach(var owner in new[]{MNG_InputOwner.Human,MNG_InputOwner.Manager})
            {
                if(avatar.Ownership.Owner!=owner)human.ToggleOwner();
                avatar.Body.linearVelocity=Vector3.zero;motor.ManagerPace=1;
                for(int i=0;i<20;i++)
                {
                    bool applied=motor.ApplyDesiredVelocity(Vector2.up*9,owner,avatar.Ownership.Revision,.02f);
                    if(!applied)throw new System.Exception("Motor ownership fixture rejected "+owner);
                    float speed=new Vector2(avatar.Body.linearVelocity.x,avatar.Body.linearVelocity.z).magnitude;
                    if(i==0){if(owner==MNG_InputOwner.Human)humanFirst=speed;else aiFirst=speed;}
                    velocities.Add($"{owner},{i},{speed:R}");
                }
                Check(Mathf.Approximately(avatar.Body.linearVelocity.z,9),"Identical motor cap for "+owner);
            }
            File.WriteAllLines(Path.Combine(smokeDirectory,"motor-parity.csv"),velocities);
            Check(Mathf.Approximately(humanFirst,.6f)&&Mathf.Approximately(humanFirst,aiFirst),"Human and AI acceleration both 30 m/s2 with 0.6 m/s per 20 ms tick");
            avatar.transform.SetPositionAndRotation(new Vector3(0,.52f,-5),Quaternion.identity);
            avatar.Body.position=avatar.transform.position;avatar.Body.rotation=Quaternion.identity;avatar.Body.linearVelocity=Vector3.zero;avatar.Body.angularVelocity=Vector3.zero;Physics.SyncTransforms();
            for(int tick=0;tick<80;tick++)
            {
                motor.ApplyMoveTarget(new Vector2(0,15),MNG_InputOwner.Manager,avatar.Ownership.Revision,Time.fixedDeltaTime);
                yield return new WaitForFixedUpdate();
            }
            float aiPhysicalSpeed=new Vector2(avatar.Body.linearVelocity.x,avatar.Body.linearVelocity.z).magnitude;
            File.WriteAllText(Path.Combine(smokeDirectory,"physical-speed-parity.txt"),$"human={actualSpeed:R}\nai={aiPhysicalSpeed:R}\nlinearDamping={avatar.Body.linearDamping:R}\nrequestedMaximum={motor.Profile.MaximumPlayerSpeed:R}\n");
            Check(Mathf.Abs(actualSpeed-aiPhysicalSpeed)<.15f,"Human W and AI move target have equal actual speed after the same physical damping");
            // Real collision fixture: isolate contacts, without synthetic statistic events.
            match.enabled=false;
            foreach(var player in players)
            {
                player.GetComponent<MNG_PlayerSkillExecutor>().enabled=false;
                player.Body.linearVelocity=Vector3.zero;player.Body.isKinematic=true;
                player.transform.position=new Vector3(-20+(int)player.Team*10,.52f,-15+player.Slot*5);
                player.Body.position=player.transform.position;
            }
            ballBody.constraints=RigidbodyConstraints.FreezeAll;ballBody.useGravity=false;
            keeperTouches.ResetCounts();
            foreach(var keeper in players.Where(a=>a.Role==MNG_PlayerRole.Keeper))
            {
                keeper.transform.SetPositionAndRotation(new Vector3(0,.52f,0),Quaternion.identity);
                keeper.Body.position=keeper.transform.position;keeper.Body.rotation=Quaternion.identity;
                Physics.SyncTransforms();
                var body=keeper.GetComponentsInChildren<Renderer>().First(r=>r.name.StartsWith("AgentCube_"));
                float radius=ball.GetComponent<SphereCollider>().radius*ball.transform.lossyScale.x;
                var contact=new Vector3(body.bounds.max.x+radius-.04f,.6f,0);
                for(int touch=1;touch<=2;touch++)
                {
                    ballBody.position=new Vector3(5,.6f,0);Physics.SyncTransforms();
                    for(int f=0;f<3;f++)yield return new WaitForFixedUpdate();
                    ballBody.position=contact;Physics.SyncTransforms();
                    for(int f=0;f<5;f++)yield return new WaitForFixedUpdate();
                    Check(keeperTouches.GetTouches(keeper.Team)==touch,"Keeper physical touch counted once while held: "+keeper.Team+" contact "+touch);
                }
                ballBody.position=new Vector3(5,.6f,0);Physics.SyncTransforms();
                for(int f=0;f<3;f++)yield return new WaitForFixedUpdate();
                var plate=keeper.KickPlate.GetComponentInChildren<Collider>();
                ballBody.position=new Vector3(plate.bounds.center.x,.6f,plate.bounds.max.z+radius-.04f);Physics.SyncTransforms();
                for(int f=0;f<5;f++)yield return new WaitForFixedUpdate();
                Check(keeperTouches.GetTouches(keeper.Team)==3,"Keeper plate contact counted: "+keeper.Team);
                keeper.transform.position=new Vector3(15,.52f,15);keeper.Body.position=keeper.transform.position;
                ballBody.position=new Vector3(5,.6f,0);Physics.SyncTransforms();
                for(int f=0;f<3;f++)yield return new WaitForFixedUpdate();
            }
            int red=keeperTouches.GetTouches(Team.Red),navy=keeperTouches.GetTouches(Team.Navy);
            avatar.transform.SetPositionAndRotation(new Vector3(0,.52f,0),Quaternion.identity);avatar.Body.position=avatar.transform.position;
            Physics.SyncTransforms();ballBody.position=new Vector3(torso.bounds.max.x+ball.GetComponent<SphereCollider>().radius*ball.transform.lossyScale.x-.04f,.6f,0);Physics.SyncTransforms();
            for(int f=0;f<5;f++)yield return new WaitForFixedUpdate();
            Check(keeperTouches.GetTouches(Team.Red)==red&&keeperTouches.GetTouches(Team.Navy)==navy,"Outfield contact does not count as a keeper touch");
            match.GoalTouched(Team.Red);yield return new WaitForFixedUpdate();
            Check(keeperTouches.GetTouches(Team.Red)==red&&keeperTouches.GetTouches(Team.Navy)==navy,"Keeper touches survive a goal pause");
            Results();yield return null;
            var labels=screen.Q("stat-saves").Query<Label>().ToList();
            Check(labels[0].text==red.ToString()&&labels[2].text==navy.ToString(),"Korean scoreboard shows both physical keeper touch totals");
            yield return Capture("18-keeper-counts-ko");
            Time.timeScale=1;match.ResetMatch();yield return new WaitForFixedUpdate();
            Check(keeperTouches.GetTouches(Team.Red)==0&&keeperTouches.GetTouches(Team.Navy)==0,"New match resets keeper counters");
        }
    }
}
