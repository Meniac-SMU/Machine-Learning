using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;
using Unity.Cinemachine;

namespace MachineLearning.Soccer.Manager.Exhibition
{
    public sealed partial class ExhibitionApp
    {
        void CheckCamera(ExhibitionCamera.View view,string label,string context)
        {
            Check(cameras.CurrentView==view&&!cameras.IsTransitioning,"Camera cuts directly to "+view+": "+context);
            Check(cameras.Brain.ActiveVirtualCamera==cameras.ActiveCamera,"Cinemachine drives the selected camera: "+context);
            if(label==null)Check(cameraBadge==null,"Broadcast has no camera label: "+context);
            else Check(cameraBadge.Query<Label>().First().text==label&&Mathf.Approximately(cameraBadge.resolvedStyle.backgroundColor.a,.95f),"Player camera badge "+label+": "+context);
        }
        IEnumerator CameraSmokeSteps()
        {
            var ui=UiSmokeSteps();while(ui.MoveNext())yield return ui.Current;
            foreach(var locale in new[]{"ko","en"})
            {
                Home();SetLanguage(locale);yield return new WaitForSecondsRealtime(.5f);
                ConfigureMatch(true,300,7,7);Kickoff();yield return new WaitForSecondsRealtime(.5f);
                CheckCamera(ExhibitionCamera.View.Broadcast,null,locale);
                Check(controls.Query<Label>().First().text==L("cameraCycle"),"Localized simulation camera keys: "+locale);
                yield return Press(Key.LeftArrow);yield return null;
                CheckCamera(ExhibitionCamera.View.RedKeeper,"RED: GK",locale);
                Check(cameras.ActiveCamera.Follow==match.GetPlayerAvatar(Team.Red,0).transform,"Red keeper tracking target: "+locale);
                yield return Capture("camera-red-gk-"+locale);
                yield return Press(Key.LeftArrow);yield return null;CheckCamera(ExhibitionCamera.View.NavyKeeper,"NAVY: GK",locale);
                yield return Capture("camera-navy-gk-"+locale);
                yield return Press(Key.LeftArrow);yield return null;CheckCamera(ExhibitionCamera.View.Broadcast,null,locale);
                foreach(var view in new[]{ExhibitionCamera.View.NavyKeeper,ExhibitionCamera.View.RedKeeper,ExhibitionCamera.View.Broadcast})
                {
                    yield return Press(Key.RightArrow);yield return null;
                    CheckCamera(view,view==ExhibitionCamera.View.Broadcast?null:view==ExhibitionCamera.View.RedKeeper?"RED: GK":"NAVY: GK",locale+" right cycle");
                }
                yield return Capture("camera-broadcast-"+locale);
                Home();ConfigureMatch(false,600,7,7);Kickoff();yield return new WaitForSecondsRealtime(.4f);
                yield return Press(Key.H);yield return new WaitForSecondsRealtime(1);
                CheckCamera(ExhibitionCamera.View.RedStriker,"RED: ST",locale);
                Check(cameras.ActiveCamera.GetComponent<CinemachineFollow>().TrackerSettings.PositionDamping==Vector3.one*.2f,"Cinemachine follow damping 0.2: "+locale);
                Check(controls.Query<Label>().ToList().Any(l=>l.text==L("mouseLook")),"Localized mouse-look hint: "+locale);
                var body=human.GetComponent<Rigidbody>();body.isKinematic=true;human.enabled=false;
                yield return new WaitForSecondsRealtime(.3f);
                var playerRotation=body.rotation;var playerPosition=body.position;var cameraRotation=cameras.transform.rotation;
                var centeredOffset=cameras.transform.position-playerPosition;
                var centeredViewport=cameras.GetComponent<Camera>().WorldToViewportPoint(playerPosition+Vector3.up);
                InputSystem.QueueStateEvent(smokeMouse,new MouseState{delta=new Vector2(1000,0)});yield return null;yield return null;
                Check(Mathf.Abs(cameras.MouseYaw-30)<.01f&&Quaternion.Angle(cameraRotation,cameras.transform.rotation)>25,"Mouse rotates only camera and clamps right at 30 degrees: "+locale);
                Check(Vector3.Distance(cameras.transform.position-playerPosition,Quaternion.AngleAxis(30,Vector3.up)*centeredOffset)<.1f,"Right orbit rotates camera position around player center: "+locale);
                Check(Vector3.Distance(centeredViewport,cameras.GetComponent<Camera>().WorldToViewportPoint(playerPosition+Vector3.up))<.02f,"Right orbit preserves player framing and distance: "+locale);
                Check(body.position==playerPosition&&Quaternion.Angle(body.rotation,playerRotation)<.001f,"Mouse leaves player transform unchanged: "+locale);
                yield return Capture("camera-human-right-"+locale);
                yield return Press(Key.LeftArrow);Check(cameras.CurrentView==ExhibitionCamera.View.RedStriker,"Arrow key does not override human camera: "+locale);
                InputSystem.QueueStateEvent(smokeMouse,new MouseState{delta=new Vector2(-2000,0)});yield return null;yield return null;
                Check(Mathf.Abs(cameras.MouseYaw+30)<.01f,"Mouse clamps left at 30 degrees: "+locale);
                Check(Vector3.Distance(cameras.transform.position-playerPosition,Quaternion.AngleAxis(-30,Vector3.up)*centeredOffset)<.1f,"Left orbit rotates camera position around player center: "+locale);
                Check(Vector3.Distance(centeredViewport,cameras.GetComponent<Camera>().WorldToViewportPoint(playerPosition+Vector3.up))<.02f,"Left orbit preserves player framing and distance: "+locale);
                yield return Capture("camera-human-look-"+locale);
                yield return new WaitForSecondsRealtime(1.9f);
                Check(Mathf.Abs(cameras.MouseYaw)<1,"Idle mouse recenters after delay: "+locale);
                Check(Vector3.Distance(cameras.transform.position-playerPosition,centeredOffset)<.15f,"Idle return restores centered camera position: "+locale);
                var before=cameras.transform.position;
                body.position+=Vector3.right*2;body.transform.position=body.position;Physics.SyncTransforms();
                yield return new WaitForEndOfFrame();yield return null;yield return new WaitForEndOfFrame();
                float early=Vector3.Distance(before,cameras.transform.position);
                Check(early>.01f&&early<1.95f,"Cinemachine follows movement with visible lag: "+locale);
                yield return new WaitForSecondsRealtime(.6f);
                Check(Vector3.Distance(before+Vector3.right*2,cameras.transform.position)<.15f,"Cinemachine catches up to tracked player: "+locale);
                yield return Capture("camera-human-front-"+locale);
                human.enabled=true;body.isKinematic=false;
                yield return Press(Key.Escape);var pausedPosition=cameras.transform.position;var pausedRotation=cameras.transform.rotation;
                InputSystem.QueueStateEvent(smokeMouse,new MouseState{delta=new Vector2(500,0)});yield return Press(Key.RightArrow);
                Check(Paused&&cameras.transform.position==pausedPosition&&cameras.transform.rotation==pausedRotation&&UnityEngine.Cursor.lockState==CursorLockMode.None,"Pause freezes camera and releases cursor: "+locale);
                yield return Press(Key.Escape);yield return Press(Key.H);yield return new WaitForSecondsRealtime(1);
                CheckCamera(ExhibitionCamera.View.Broadcast,null,locale+" return to AI");
                yield return Press(Key.RightArrow);yield return null;CheckCamera(ExhibitionCamera.View.NavyKeeper,"NAVY: GK",locale+" direct-play AI");
                Home();yield return null;Check(UnityEngine.Cursor.lockState==CursorLockMode.None&&UnityEngine.Cursor.visible,"Home restores menu cursor: "+locale);
            }
            Home();yield return Capture("quit-ready-en");
        }
    }
}
