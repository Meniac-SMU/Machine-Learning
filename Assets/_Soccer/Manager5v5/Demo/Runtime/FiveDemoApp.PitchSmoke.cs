using System.Collections;
using System.Linq;
using UnityEngine;
namespace MachineLearning.Soccer.Manager5v5.Demo
{
 public sealed partial class FiveDemoApp
 {
  IEnumerator PitchSmokeBody()
  {
   ConfigureMatch(false,300,FiveDemoAssets.LatestModelId,FiveDemoAssets.LatestModelId);Kickoff();yield return null;
   var grass=match.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="Pitch_Grass");
   var lines=match.GetComponentsInChildren<MeshFilter>(true).Single(m=>m.name=="Pitch_WhiteLines");
   var original=grass.transform.parent.GetComponent<MeshFilter>();
   CheckDemo(!original.GetComponent<MeshRenderer>().enabled,"pitch-original-visual-disabled");
   CheckDemo(grass.sharedMesh!=lines.sharedMesh&&grass.GetComponent<MeshRenderer>().sharedMaterial!=lines.GetComponent<MeshRenderer>().sharedMaterial,"pitch-independent-mesh-and-material");
   CheckDemo(Vector3.Distance(grass.sharedMesh.bounds.min,original.sharedMesh.bounds.min)<.0001f&&Vector3.Distance(grass.sharedMesh.bounds.max,original.sharedMesh.bounds.max)<.0001f,"pitch-original-bounds");
   CheckDemo(grass.GetComponentsInChildren<Collider>().Length==0&&lines.GetComponentsInChildren<Collider>().Length==0,"pitch-visuals-no-new-colliders");
   CheckDemo(match.players.All(p=>p.GetComponentsInChildren<Collider>().Length==4),"pitch-ten-players-four-colliders");
   var go=new GameObject("Pitch QA camera");var cam=go.AddComponent<Camera>();var bounds=grass.GetComponent<MeshRenderer>().bounds;
   cam.orthographic=true;cam.orthographicSize=Mathf.Max(bounds.size.z/2,bounds.size.x/2/(16f/9f))+2;cam.aspect=16f/9f;cam.transform.position=bounds.center+Vector3.up*100;cam.transform.rotation=Quaternion.Euler(90,0,0);cam.farClipPlane=250;
   RobotCameraCapture(cam,"pitch-overhead");Destroy(go);
   human.ToggleOwner();yield return new WaitForSecondsRealtime(1.2f);
   var offset=cameras.ActiveCamera.GetComponent<Unity.Cinemachine.CinemachineFollow>().FollowOffset;
   CheckDemo(Mathf.Abs(offset.magnitude-new Vector3(0,2.55f,-5.625f).magnitude)<.001f,"human-camera-original-distance-75-percent",offset.ToString());
   yield return Capture("pitch-human-camera");Home();yield return null;
   var body=SmokeBody();while(body.MoveNext())yield return body.Current;
  }
 }
}
