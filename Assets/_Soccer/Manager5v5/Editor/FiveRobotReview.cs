using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
namespace MachineLearning.Soccer.Manager5v5.Editor
{
    public static class FiveRobotReview
    {
        public static void Create()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play mode first.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            string root="Assets/_Soccer/Manager5v5/Art/CubeRobots";
            var names=new[]{"RedRobot","NavyRobot","PinkGoalkeeper","SkyGoalkeeper"};
            for(int i=0;i<4;i++)
            {
                var obj=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(root+"/Prefabs/"+names[i]+".prefab"));
                obj.transform.position=new Vector3((1.5f-i)*1.5f,0,0);obj.transform.localScale=Vector3.one*.9f;
            }
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Review ground";floor.transform.position=new Vector3(0,-.06f,0);floor.transform.localScale=new Vector3(200,.1f,200);
            Object.DestroyImmediate(floor.GetComponent<Collider>());
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.SetColor("_BaseColor",new Color(.17f,.19f,.23f));
            string matpath=root+"/Materials/ReviewGround.mat";
            var saved=AssetDatabase.LoadAssetAtPath<Material>(matpath);
            if(saved==null){AssetDatabase.CreateAsset(material,matpath);saved=material;}else Object.DestroyImmediate(material);
            floor.GetComponent<Renderer>().sharedMaterial=saved;
            var light=new GameObject("Review key").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(45,-155,0);light.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.55f,.55f);
            var camera=new GameObject("RobotReviewCamera").AddComponent<Camera>();camera.tag="MainCamera";camera.transform.position=new Vector3(2.5f,3.1f,8);camera.transform.LookAt(new Vector3(0,.45f,0));camera.orthographic=true;camera.orthographicSize=2.15f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.1f,.15f);camera.allowHDR=true;
            EditorSceneManager.SaveScene(scene,"Assets/_Soccer/Manager5v5/Scenes/RobotReview.unity");AssetDatabase.SaveAssets();
            Selection.activeGameObject=camera.gameObject;
        }
    }
}
