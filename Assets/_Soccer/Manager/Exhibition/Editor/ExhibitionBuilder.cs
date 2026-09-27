using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEditor.Localization;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.TextCore.Text;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UIElements;
using Unity.InferenceEngine;
using MachineLearning.Soccer.Manager.Editor;

namespace MachineLearning.Soccer.Manager.Exhibition.Editor
{
    public static class ExhibitionBuilder
    {
        public const string Root="Assets/_Soccer/Manager/Exhibition";
        public const string ScenePath=Root+"/Exhibition.unity";
        [Serializable] class Entries { public Entry[] entries; }
        [Serializable] class Entry { public string key,ko,en; }
        public static void PrepareBatch(){try{Prepare();Debug.Log("EXHIBITION PREPARE PASS");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
        public static void Prepare()
        {
            AssetDatabase.Refresh();
            foreach(var file in Directory.GetFiles(Root+"/Art","*.png"))
            {
                var importer=(TextureImporter)AssetImporter.GetAtPath(file.Replace('\\','/'));
                if(importer.npotScale!=TextureImporterNPOTScale.None||importer.textureCompression!=TextureImporterCompression.Uncompressed)
                {importer.npotScale=TextureImporterNPOTScale.None;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.alphaIsTransparency=true;importer.SaveAndReimport();}
            }
            var entries=JsonUtility.FromJson<Entries>(File.ReadAllText(Root+"/Data/strings.json")).entries;
            Directory.CreateDirectory(Root+"/Localization");AssetDatabase.Refresh();
            var settings=AssetDatabase.LoadAssetAtPath<LocalizationSettings>(Root+"/Localization/Settings.asset");
            if(settings==null){settings=ScriptableObject.CreateInstance<LocalizationSettings>();AssetDatabase.CreateAsset(settings,Root+"/Localization/Settings.asset");}
            LocalizationEditorSettings.ActiveLocalizationSettings=settings;
            foreach(var code in new[]{"ko","en"})
            {
                var locale=AssetDatabase.LoadAssetAtPath<Locale>(Root+"/Localization/"+code+".asset");
                if(locale==null){locale=Locale.CreateLocale(code);AssetDatabase.CreateAsset(locale,Root+"/Localization/"+code+".asset");}
                if(!LocalizationEditorSettings.GetLocales().Any(l=>l.Identifier.Code==code))LocalizationEditorSettings.AddLocale(locale);
            }
            settings.GetStartupLocaleSelectors().Clear();settings.GetStartupLocaleSelectors().Add(new SpecificLocaleSelector{LocaleId=new LocaleIdentifier("ko")});EditorUtility.SetDirty(settings);
            var strings=LocalizationEditorSettings.GetStringTableCollection("Exhibition")??LocalizationEditorSettings.CreateStringTableCollection("Exhibition",Root+"/Localization");
            foreach(var table in strings.StringTables)
            {
                foreach(var e in entries){var value=table.LocaleIdentifier.Code=="ko"?e.ko:e.en;if(string.IsNullOrWhiteSpace(value))throw new Exception("Empty string: "+e.key);table.AddEntry(e.key,value);}
                EditorUtility.SetDirty(table);
            }
            strings.SetPreloadTableFlag(true);EditorUtility.SetDirty(strings.SharedData);EditorUtility.SetDirty(strings);
            var art=LocalizationEditorSettings.GetAssetTableCollection("ExhibitionArt")??LocalizationEditorSettings.CreateAssetTableCollection("ExhibitionArt",Root+"/Localization");
            foreach(var table in art.AssetTables)
            {
                var code=table.LocaleIdentifier.Code;
                art.AddAssetToTable(table,"credits",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/credits-"+code+".png"));
                for(int i=1;i<=5;i++)art.AddAssetToTable(table,"guide"+i,AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/guide-"+code+"-"+i+".png"));
                EditorUtility.SetDirty(table);
            }
            art.SetPreloadTableFlag(true);EditorUtility.SetDirty(art.SharedData);EditorUtility.SetDirty(art);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,strings);
            LocalizationEditorSettings.EditorEvents.RaiseCollectionModified(null,art);
            var assets=AssetDatabase.LoadAssetAtPath<ExhibitionAssets>(Root+"/ExhibitionAssets.asset");
            if(assets==null){assets=ScriptableObject.CreateInstance<ExhibitionAssets>();AssetDatabase.CreateAsset(assets,Root+"/ExhibitionAssets.asset");}
            assets.models=ExhibitionAssets.Ids.Take(6).Select(id=>AssetDatabase.LoadAssetAtPath<ModelAsset>(Root+"/Models/"+id+".onnx")).ToArray();
            if(assets.models.Any(m=>m==null))throw new Exception("Missing exhibition neural model.");
            foreach(var model in assets.models)MNG_RuntimeV2.ValidateModelAsset(model);
            string[] weights={"Regular","Medium","SemiBold","Bold","ExtraBold","Black"};
            assets.fonts=new FontAsset[6];
            var corpus=string.Concat(entries.Select(e=>e.ko+e.en))+"0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz:.,+-×/↗←→—";
            for(int i=0;i<6;i++)
            {
                string path=Root+"/Fonts/Pretendard-"+weights[i]+"-SDF.asset";
                var font=AssetDatabase.LoadAssetAtPath<FontAsset>(path);
                if(font==null)
                {
                    font=FontAsset.CreateFontAsset(AssetDatabase.LoadAssetAtPath<Font>(Root+"/Fonts/Pretendard-"+weights[i]+".otf"),64,8,GlyphRenderMode.SDFAA,2048,2048,AtlasPopulationMode.Dynamic,true);
                    font.name="Pretendard "+weights[i];AssetDatabase.CreateAsset(font,path);
                    AssetDatabase.AddObjectToAsset(font.material,font);
                    foreach(var atlas in font.atlasTextures)AssetDatabase.AddObjectToAsset(atlas,font);
                }
                font.TryAddCharacters(corpus,out string missing);
                if(!string.IsNullOrEmpty(missing))Debug.LogWarning("Exhibition font missing: "+missing);
                EditorUtility.SetDirty(font);assets.fonts[i]=font;
            }
            assets.teamLogo=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/team-logo.png");assets.gameLogo=AssetDatabase.LoadAssetAtPath<Texture2D>(Root+"/Art/game-logo.png");
            EditorUtility.SetDirty(assets);AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            assets=AssetDatabase.LoadAssetAtPath<ExhibitionAssets>(Root+"/ExhibitionAssets.asset");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(MNG_ProjectBuilder.ManagerPrefabPath);
            var arena=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);PrefabUtility.UnpackPrefabInstance(arena,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            arena.name="Exhibition Arena";arena.GetComponent<MNG_MatchController>().ConfigureRuntimeV2(true);
            foreach(var hud in arena.GetComponentsInChildren<MNG_HudPresenter>(true))UnityEngine.Object.DestroyImmediate(hud.gameObject);
            foreach(var fallback in arena.GetComponentsInChildren<MNG_FallbackManager>(true))fallback.enabled=false;
            foreach(var rule in arena.GetComponentsInChildren<MNG_RuleBasedManager>(true))rule.enabled=false;
            foreach(var camera in arena.GetComponentsInChildren<Camera>(true))
            {
                camera.transform.position*=.88f;
                var spectator=camera.GetComponent<MNG_SpectatorCamera>();
                if(spectator!=null)UnityEngine.Object.DestroyImmediate(spectator);
                camera.gameObject.AddComponent<ExhibitionCamera>();
            }
            PrepareExhibitionPresentation(arena);
            arena.SetActive(false);
            assets.arena=PrefabUtility.SaveAsPrefabAsset(arena,Root+"/ExhibitionArena.prefab");UnityEngine.Object.DestroyImmediate(arena);
            EditorUtility.SetDirty(assets);
            var panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(Root+"/UI/Panel.asset");
            if(panel==null){panel=ScriptableObject.CreateInstance<PanelSettings>();AssetDatabase.CreateAsset(panel,Root+"/UI/Panel.asset");}
            panel.scaleMode=PanelScaleMode.ConstantPixelSize;panel.sortingOrder=100;
            panel.themeStyleSheet=AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/_Soccer/UI/SoccerRuntimeTheme.tss");
            var text=AssetDatabase.LoadAssetAtPath<PanelTextSettings>(Root+"/UI/TextSettings.asset");
            if(text==null){text=ScriptableObject.CreateInstance<PanelTextSettings>();AssetDatabase.CreateAsset(text,Root+"/UI/TextSettings.asset");}
            text.defaultFontAsset=assets.fonts[0];panel.textSettings=text;EditorUtility.SetDirty(text);EditorUtility.SetDirty(panel);
            var ui=new GameObject("Exhibition UI");var doc=ui.AddComponent<UIDocument>();doc.panelSettings=panel;doc.visualTreeAsset=AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root+"/UI/Exhibition.uxml");
            var app=ui.AddComponent<ExhibitionApp>();app.assets=assets;app.stylesheet=AssetDatabase.LoadAssetAtPath<StyleSheet>(Root+"/UI/Exhibition.uss");
            var cameraObject=new GameObject("Menu Camera");var menuCamera=cameraObject.AddComponent<Camera>();menuCamera.clearFlags=CameraClearFlags.SolidColor;menuCamera.backgroundColor=Color.black;menuCamera.cullingMask=0;menuCamera.depth=-100;
            AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,ScenePath);
        }
        static void PrepareExhibitionPresentation(GameObject arena)
        {
            // Only the exhibition copy is modified. Training prefabs keep interpolation off.
            Directory.CreateDirectory(Root+"/Materials");AssetDatabase.Refresh();
            foreach(var avatar in arena.GetComponentsInChildren<MNG_PlayerAvatar>(true))
            {
                avatar.Body.interpolation=RigidbodyInterpolation.Interpolate;
                var torso=avatar.GetComponentsInChildren<Renderer>(true).First(r=>r.name.StartsWith("AgentCube_",StringComparison.Ordinal));
                if(avatar.Role==MNG_PlayerRole.Keeper)
                {
                    string hex=avatar.Team==Team.Red?"#F05278":"#429CF2";
                    ColorUtility.TryParseHtmlString(hex,out var color);
                    torso.sharedMaterial=PresentationMaterial(torso.sharedMaterial,"Goalkeeper-"+avatar.Team,color,false);
                    ColorUtility.TryParseHtmlString("#E6FF00",out var plateColor);
                    foreach(var plate in avatar.KickPlate.GetComponentsInChildren<Renderer>(true))
                        plate.sharedMaterial=PresentationMaterial(plate.sharedMaterial,"Goalkeeper-KickPlate",plateColor,false);
                }
                var marker=avatar.transform.Find("HumanControlMarker");
                if(marker!=null)
                {
                    var position=marker.position;
                    position.y=torso.bounds.max.y+marker.GetComponent<Renderer>().bounds.extents.y-.005f;
                    marker.position=position;
                }
            }
            var ball=arena.GetComponentInChildren<MNG_BallControl>(true);
            ball.GetComponent<Rigidbody>().interpolation=RigidbodyInterpolation.Interpolate;
            if(ball.GetComponent<ExhibitionKeeperTouches>()==null)ball.gameObject.AddComponent<ExhibitionKeeperTouches>();
            foreach(var fader in arena.GetComponentsInChildren<SoccerGoalOcclusionFader>(true))UnityEngine.Object.DestroyImmediate(fader);
            foreach(var renderer in arena.GetComponentsInChildren<Renderer>(true).Where(r=>r.CompareTag("redGoal")||r.CompareTag("navyGoal")))
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select((source,index)=>
                {
                    var color=source.HasProperty("_BaseColor")?source.GetColor("_BaseColor"):source.color;
                    color.a=.60f;
                    return PresentationMaterial(source,renderer.name+"-"+index,color,true);
                }).ToArray();
            }
        }
        static Material PresentationMaterial(Material source,string name,Color color,bool transparent)
        {
            string path=Root+"/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(source);AssetDatabase.CreateAsset(material,path);}
            else material.CopyPropertiesFromMaterial(source);
            material.name=name;
            if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",color);
            if(material.HasProperty("_Color"))material.SetColor("_Color",color);
            if(transparent)
            {
                // Same URP transparency setup as the historical goal occlusion fader,
                // persisted once as an asset instead of cloning/fading every match.
                material.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent;
                material.SetOverrideTag("RenderType","Transparent");
                material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);
                material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite",0);
                material.DisableKeyword("_ALPHATEST_ON");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetShaderPassEnabled("ShadowCaster",false);
            }
            EditorUtility.SetDirty(material);return material;
        }
        public static void BuildBatch()
        {
            var originalName=PlayerSettings.productName;var originalWidth=PlayerSettings.defaultScreenWidth;var originalHeight=PlayerSettings.defaultScreenHeight;var originalMode=PlayerSettings.fullScreenMode;
            var originalSplash=PlayerSettings.SplashScreen.show;var originalUnityLogo=PlayerSettings.SplashScreen.showUnityLogo;
            int exit=1;
            try
            {
                Prepare();
                AddressableAssetSettings.BuildPlayerContent(out var addressables);
                if(!string.IsNullOrEmpty(addressables.Error))throw new Exception(addressables.Error);
                var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-exhibitionBuildPath");
                if(index<0)throw new Exception("Provide -exhibitionBuildPath to a new directory.");
                string output=args[index+1];if(File.Exists(Path.Combine(output,"MANAGER.exe")))throw new Exception("Existing builds are immutable; choose a new output.");Directory.CreateDirectory(output);
                PlayerSettings.productName="MANAGER";PlayerSettings.defaultScreenWidth=1920;PlayerSettings.defaultScreenHeight=1080;PlayerSettings.fullScreenMode=FullScreenMode.FullScreenWindow;
                PlayerSettings.SplashScreen.show=false;PlayerSettings.SplashScreen.showUnityLogo=false;
                if(PlayerSettings.SplashScreen.show||PlayerSettings.SplashScreen.showUnityLogo)throw new Exception("Unity splash must be disabled for the exhibition Player.");
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName=Path.Combine(output,"MANAGER.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
                if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Exhibition build failed: "+report.summary.result);
                File.WriteAllText(Path.Combine(output,"splash-settings.json"),"{\"showSplashScreen\":false,\"showUnityLogo\":false,\"teamLogoPreserved\":true}");
                File.WriteAllText(Path.Combine(output,"build-info.json"),"{\"stage\":\"UI-v1-final\",\"scene\":\""+ScenePath+"\",\"builtAtUtc\":\""+DateTime.UtcNow.ToString("O")+"\",\"environmentRevision\":\""+MNG_RuntimeV2.EnvironmentRevision+"\"}");
                Debug.Log("EXHIBITION WINDOWS BUILD PASS "+output);exit=0;
            }
            catch(Exception e){Debug.LogException(e);}
            finally{PlayerSettings.productName=originalName;PlayerSettings.defaultScreenWidth=originalWidth;PlayerSettings.defaultScreenHeight=originalHeight;PlayerSettings.fullScreenMode=originalMode;PlayerSettings.SplashScreen.show=originalSplash;PlayerSettings.SplashScreen.showUnityLogo=originalUnityLogo;AssetDatabase.SaveAssets();}
            EditorApplication.Exit(exit);
        }
    }
}
