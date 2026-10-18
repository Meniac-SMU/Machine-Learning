using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Unity.InferenceEngine;
using TextAsset = UnityEngine.TextAsset;

namespace MachineLearning.Soccer.Manager5v5.Demo.Editor
{
    public static class FiveDemoBuilder
    {
        const string Root="Assets/_Soccer/Manager5v5/Demo",Old="Assets/_Soccer/Manager/Exhibition";
        public const string Scene=Root+"/FiveDemo.unity";
        public static void BuildBatch()
        {
            try
            {
                var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-fivePlayerPath");
                if(index<0||index+1>=args.Length)throw new ArgumentException("New output path required.");
                var output=Path.GetFullPath(args[index+1]);
                if(!output.StartsWith(Path.GetFullPath("Builds/Manager5v5")+Path.DirectorySeparatorChar)||File.Exists(output))throw new IOException("Preserve existing builds.");
                Directory.CreateDirectory(Path.GetDirectoryName(output));
                if(AssetDatabase.IsValidFolder("Assets/_Soccer/Manager5v5/Art/CubeRobots"))FiveRobotAssetSetup.Build();
                MachineLearning.Soccer.Manager5v5.Editor.FivePitchSetup.Build();
                Prepare(Path.Combine(Path.GetDirectoryName(output),"bench-layout.json"));
                var dependencies=AssetDatabase.GetDependencies(Scene,true);
                var models=dependencies.Where(p=>p.EndsWith(".onnx",StringComparison.OrdinalIgnoreCase)).ToArray();
                if(models.Length!=FiveDemoAssets.Steps.Length||models.Any(p=>!p.StartsWith(Root+"/Models/stage-")))throw new InvalidOperationException("Unexpected or legacy neural dependency.");
                var name=PlayerSettings.productName;var splash=PlayerSettings.SplashScreen.show;
                try
                {
                    PlayerSettings.productName=FiveDemoAssets.GameName;PlayerSettings.SplashScreen.show=false;
                    var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
                    if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("5v5 demo build failed.");
                    File.WriteAllLines(Path.Combine(Path.GetDirectoryName(output),"asset-dependencies.txt"),dependencies);
                    File.Copy(Root+"/Models/catalog.json",Path.Combine(Path.GetDirectoryName(output),"model-catalog.json"));
                    File.Copy(Old+"/Fonts/LICENSE.txt",Path.Combine(Path.GetDirectoryName(output),"Pretendard-LICENSE.txt"));
                }
                finally{PlayerSettings.productName=name;PlayerSettings.SplashScreen.show=splash;AssetDatabase.SaveAssets();}
                Debug.Log($"FIVE_DEMO_BUILD_PASS: {FiveDemoAssets.Steps.Length+1} choices, {FiveDemoAssets.Steps.Length} current policies, no legacy neural dependency");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        public static void Prepare(string benchEvidence=null)
        {
            AssetDatabase.Refresh();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var assets=AssetDatabase.LoadAssetAtPath<FiveDemoAssets>(Root+"/FiveDemoAssets.asset");
            if(assets==null){assets=ScriptableObject.CreateInstance<FiveDemoAssets>();AssetDatabase.CreateAsset(assets,Root+"/FiveDemoAssets.asset");}
            var original=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Soccer/Manager5v5/Prefabs/FiveArena.prefab");
            var arena=(GameObject)PrefabUtility.InstantiatePrefab(original,scene);
            PrefabUtility.UnpackPrefabInstance(arena,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            arena.name="Five Demo Arena";arena.SetActive(false);arena.GetComponent<FiveMatch>().automaticTick=false;
            foreach(var review in arena.GetComponentsInChildren<FiveReviewRun>(true))UnityEngine.Object.DestroyImmediate(review);
            AlignBenches(arena,benchEvidence);
            FiveDemoGroundSetup.Build(arena,benchEvidence==null?null:Path.Combine(Path.GetDirectoryName(benchEvidence),"ground-layout.json"));
            var walls=arena.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("Wall_",StringComparison.Ordinal)).ToArray();
            var wallMaterial=new Material(walls[0].sharedMaterial){name="DemoSkyBlueWall"};
            var wallColor=wallMaterial.GetColor("_BaseColor");wallColor.a=.1f;wallMaterial.SetColor("_BaseColor",wallColor);
            const string wallPath=Root+"/Art/DemoSkyBlueWall.mat";
            var savedWall=AssetDatabase.LoadAssetAtPath<Material>(wallPath);
            if(savedWall==null){AssetDatabase.CreateAsset(wallMaterial,wallPath);savedWall=wallMaterial;}
            else{EditorUtility.CopySerialized(wallMaterial,savedWall);UnityEngine.Object.DestroyImmediate(wallMaterial);EditorUtility.SetDirty(savedWall);}
            foreach(var wall in walls)wall.sharedMaterial=savedWall;
            foreach(var team in new[]{"Red","Navy"})
            {
                ColorUtility.TryParseHtmlString(team=="Red"?"#E9A3AC":"#9AAAC9",out var goalColor);
                var floorMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Demo"+team+"GoalFloor"};
                floorMaterial.SetColor("_BaseColor",goalColor);floorMaterial.SetFloat("_Smoothness",.15f);
                floorMaterial.SetFloat("_Surface",0);floorMaterial.SetFloat("_ZWrite",1);
                floorMaterial.SetFloat("_SrcBlend",1);floorMaterial.SetFloat("_DstBlend",0);
                floorMaterial.SetOverrideTag("RenderType","Opaque");floorMaterial.renderQueue=2000;
                string path=Root+"/Art/"+team+"GoalFloor.mat";
                var saved=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(saved==null){AssetDatabase.CreateAsset(floorMaterial,path);saved=floorMaterial;}
                else{EditorUtility.CopySerialized(floorMaterial,saved);UnityEngine.Object.DestroyImmediate(floorMaterial);EditorUtility.SetDirty(saved);}
                arena.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name==team+"_Goal_Floor").sharedMaterial=saved;
            }
            assets.arena=PrefabUtility.SaveAsPrefabAsset(arena,Root+"/FiveDemoArena.prefab");UnityEngine.Object.DestroyImmediate(arena);
            assets.models=FiveDemoAssets.Steps.Select(step=>AssetDatabase.LoadAssetAtPath<ModelAsset>(Root+"/Models/stage-"+step+".onnx")).ToArray();
            if(assets.models.Any(m=>m==null))throw new Exception("Missing checkpoint.");
            string[] weights={"Regular","Medium","SemiBold","Bold","ExtraBold","Black"};
            assets.fonts=weights.Select(w=>AssetDatabase.LoadAssetAtPath<FontAsset>(Old+"/Fonts/Pretendard-"+w+"-SDF.asset")).ToArray();
            assets.teamLogo=AssetDatabase.LoadAssetAtPath<Texture2D>(Old+"/Art/team-logo.png");
            const string cursorPath=Root+"/Art/traffic-cone.png";
            var cursorImporter=(TextureImporter)AssetImporter.GetAtPath(cursorPath);
            cursorImporter.textureType=TextureImporterType.Default;cursorImporter.isReadable=true;cursorImporter.mipmapEnabled=false;
            cursorImporter.textureCompression=TextureImporterCompression.Uncompressed;cursorImporter.alphaIsTransparency=true;
            cursorImporter.wrapMode=TextureWrapMode.Clamp;cursorImporter.filterMode=FilterMode.Bilinear;cursorImporter.SaveAndReimport();
            assets.cursorSource=AssetDatabase.LoadAssetAtPath<Texture2D>(cursorPath);
            const string globePath=Root+"/Art/globe.png";
            var globeImporter=(TextureImporter)AssetImporter.GetAtPath(globePath);
            globeImporter.textureType=TextureImporterType.Default;globeImporter.textureShape=TextureImporterShape.Texture2D;globeImporter.maxTextureSize=512;
            globeImporter.textureCompression=TextureImporterCompression.Uncompressed;globeImporter.mipmapEnabled=true;
            globeImporter.alphaIsTransparency=true;globeImporter.wrapMode=TextureWrapMode.Clamp;globeImporter.filterMode=FilterMode.Trilinear;
            globeImporter.SaveAndReimport();assets.languageIcon=AssetDatabase.LoadAssetAtPath<Texture2D>(globePath);
            if(assets.languageIcon==null||assets.languageIcon.width!=512||assets.languageIcon.height!=512)throw new Exception("Language globe must import as the original 512x512 Texture2D.");
            const string characters="Assets/_Soccer/Manager5v5/Art/CubeRobots/Prefabs/";
            assets.redCharacters=new[]{new FiveDemoAssets.CharacterSet{id="red-cube",displayName="RED CUBE",
                fieldPlayer=AssetDatabase.LoadAssetAtPath<GameObject>(characters+"RedRobot.prefab"),goalkeeper=AssetDatabase.LoadAssetAtPath<GameObject>(characters+"PinkGoalkeeper.prefab")}};
            assets.navyCharacters=new[]{new FiveDemoAssets.CharacterSet{id="navy-cube",displayName="NAVY CUBE",
                fieldPlayer=AssetDatabase.LoadAssetAtPath<GameObject>(characters+"NavyRobot.prefab"),goalkeeper=AssetDatabase.LoadAssetAtPath<GameObject>(characters+"SkyGoalkeeper.prefab")}};
            if(assets.redCharacters.Concat(assets.navyCharacters).Any(s=>s.fieldPlayer==null||s.goalkeeper==null))throw new Exception("Missing character set prefab.");
            assets.creditsKo=AssetDatabase.LoadAssetAtPath<Texture2D>(Old+"/Art/credits-ko.png");assets.creditsEn=AssetDatabase.LoadAssetAtPath<Texture2D>(Old+"/Art/credits-en.png");
            assets.strings=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/Data/strings.json");assets.catalog=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/Models/catalog.json");
            assets.inferenceFixtures=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/Models/inference-fixtures.json");EditorUtility.SetDirty(assets);
            var panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(Root+"/UI/Panel.asset");
            if(panel==null){AssetDatabase.CopyAsset(Old+"/UI/Panel.asset",Root+"/UI/Panel.asset");panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(Root+"/UI/Panel.asset");}
            panel.sortingOrder=200;panel.scaleMode=PanelScaleMode.ConstantPixelSize;EditorUtility.SetDirty(panel);
            var ui=new GameObject("Five Demo UI");var doc=ui.AddComponent<UIDocument>();doc.panelSettings=panel;doc.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root+"/UI/FiveDemo.uxml");
            var app=ui.AddComponent<FiveDemoApp>();app.assets=assets;app.stylesheet=AssetDatabase.LoadAssetAtPath<StyleSheet>(Root+"/UI/FiveDemo.uss");
            var menu=new GameObject("Menu Camera").AddComponent<Camera>();menu.cullingMask=0;menu.depth=-100;menu.clearFlags=CameraClearFlags.SolidColor;menu.backgroundColor=Color.black;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.72f,.75f,.8f);
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,Scene);
        }
        [Serializable] sealed class BenchRecord
        {
            public string name;
            public Vector3 beforePosition,afterPosition,beforeCenter,afterCenter,beforeScale,afterScale;
            public Quaternion beforeRotation,afterRotation;
        }
        [Serializable] sealed class BenchRecords { public float halfLength; public BenchRecord[] benches; }
        static void AlignBenches(GameObject arena,string evidence)
        {
            var benches=arena.GetComponentsInChildren<MeshFilter>(true).Where(m=>m.name.StartsWith("SM_Chair_Canopy",StringComparison.Ordinal)).ToArray();
            if(benches.Length!=4)throw new InvalidOperationException("Expected exactly four player benches.");
            float halfLength=arena.GetComponent<FiveMatch>().geometry.HalfLength;
            var records=benches.Select(mesh=>
            {
                var t=mesh.transform;var center=t.TransformPoint(mesh.sharedMesh.bounds.center);
                var record=new BenchRecord{name=t.name,beforePosition=t.position,beforeCenter=center,beforeScale=t.localScale,beforeRotation=t.rotation};
                float targetX=Mathf.Sign(center.x)*halfLength*.5f;
                t.position+=Vector3.right*(targetX-center.x);
                record.afterPosition=t.position;record.afterCenter=t.TransformPoint(mesh.sharedMesh.bounds.center);
                record.afterScale=t.localScale;record.afterRotation=t.rotation;
                if(Mathf.Abs(record.afterCenter.x-targetX)>.001f||Mathf.Abs(t.position.y-record.beforePosition.y)>.001f
                    ||Mathf.Abs(t.position.z-record.beforePosition.z)>.001f||t.localScale!=record.beforeScale||Quaternion.Angle(t.rotation,record.beforeRotation)>.001f)
                    throw new InvalidOperationException("Bench adjustment must change only world X.");
                return record;
            }).ToArray();
            if(evidence!=null)File.WriteAllText(evidence,JsonUtility.ToJson(new BenchRecords{halfLength=halfLength,benches=records},true));
        }
    }
}
