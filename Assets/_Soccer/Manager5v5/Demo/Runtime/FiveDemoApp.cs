using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    [DefaultExecutionOrder(-1000), RequireComponent(typeof(UIDocument))]
    public sealed partial class FiveDemoApp : MonoBehaviour
    {
        public FiveDemoAssets assets;
        public StyleSheet stylesheet;
        VisualElement viewport,stage,screen,modal,pause,goal;
        float goalRemaining;
        GameObject arena;
        FiveMatch match;
        FiveDemoSession session;
        FiveDemoInput human;
        FiveDemoCamera cameras;
        FiveMatchStatistics statistics;
        UIDocument hud;
        public string Page { get; private set; }="splash";
        public bool English { get; private set; }
        public bool Simulation { get; private set; }
        public int RedModel { get; private set; }=FiveDemoAssets.LatestModelId;
        public int NavyModel { get; private set; }=FiveDemoAssets.LatestModelId;
        public int Duration { get; private set; }=300;
        public bool Paused => match!=null && match.Paused;
        string modalKind;
        int guidePage;
        bool ready;
        [Serializable] class Entry { public string key,ko,en; }
        [Serializable] class Strings { public Entry[] entries; }
        readonly Dictionary<string,Entry> strings=new();
        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex,out var color); return color; }
        static readonly Color Ink=C("#172417"),Paper=C("#EDF0E6"),Lime=C("#B9F54E");
        string T(string ko,string en)=>English?en:ko;
        string L(string key)
        {
            var id=key.Contains('.')?key:"MNG.Exhibition."+key;
            return strings.TryGetValue(id,out var entry)?(English?entry.en:entry.ko):key;
        }
        IEnumerator Start()
        {
            Application.targetFrameRate=60;Application.runInBackground=true;
            Screen.sleepTimeout=SleepTimeout.NeverSleep;
            InitializeCursor();ApplyCursorVisibility();
            foreach(var entry in JsonUtility.FromJson<Strings>(assets.strings.text).entries)strings[entry.key]=entry;
            var root=GetComponent<UIDocument>().rootVisualElement;root.styleSheets.Add(stylesheet);
            root.Add(conePointer);
            viewport=root.Q("viewport");stage=root.Q("stage");
            viewport.RegisterCallback<GeometryChangedEvent>(_=>Resize());
            Box(stage,0,0,1920,1080,C("#101110"));Picture(stage,assets.teamLogo,662,425,595,230);Resize();
            yield return new WaitForSecondsRealtime(1.5f);
            ready=true;Home();
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--five-demo-smoke")>=0
                ||Array.IndexOf(Environment.GetCommandLineArgs(),"--five-demo-live")>=0
                ||Array.IndexOf(Environment.GetCommandLineArgs(),"--five-demo-hud-smoke")>=0
                ||Array.IndexOf(Environment.GetCommandLineArgs(),"--five-demo-motion")>=0)StartCoroutine(Smoke());
        }
        void Resize()
        {
            float w=viewport.resolvedStyle.width,h=viewport.resolvedStyle.height;
            if(!float.IsFinite(w)||w<=0||h<=0)return;
            var factor=Mathf.Min(w/1920f,h/1080f);
            float canvasHeight=h/factor;
            stage.style.height=canvasHeight;
            stage.style.scale=new Scale(new Vector3(factor,factor,1));
            stage.style.left=(w-1920*factor)/2;stage.style.top=(h-canvasHeight*factor)/2;
            if(Page=="home")ResizeHome(canvasHeight);
            else if(screen!=null)
            {
                screen.style.top=Page=="match"?0:(canvasHeight-1080)/2;
                screen.style.height=Page=="match"?canvasHeight:1080;
            }
        }
        VisualElement FullScreenOverlay(Color color)
        {
            var overlay=Box(stage,0,0,1920,1080,color);
            overlay.style.height=Length.Percent(100);
            return overlay;
        }
        VisualElement CenteredOverlayContent(VisualElement overlay)
        {
            var content=Box(overlay,0,0,1920,1080,Color.clear);
            content.style.top=Length.Percent(50);
            content.style.translate=new Translate(0,-540);
            return content;
        }
        void NewPage(string name,Color background)
        {
            Page=name;stage.Clear();modal=pause=goal=null;goalRemaining=0;modalKind=null;
            viewport.style.backgroundColor=background;
            screen=Box(stage,0,0,1920,1080,background);screen.name=name;
            Resize();
            if(name=="match")screen.pickingMode=PickingMode.Ignore;
            ApplyCursorVisibility();
        }
        void Update()
        {
            if(!ready)return;
            var k=Keyboard.current;
            if(k?.escapeKey.wasPressedThisFrame==true)HandleEscape();
            if(modalKind=="guide"&&k?.enterKey.wasPressedThisFrame==true)NextGuide();
            if(Page=="match"&&!Paused&&goal!=null)
            {
                goalRemaining-=Time.deltaTime;
                if(goalRemaining<=0){goal.RemoveFromHierarchy();goal=null;}
            }
            if(Page=="match"&&match!=null&&match.State==FiveMatchState.Finished)Results();
        }
        public void HandleEscape()
        { if(modal!=null)CloseModal();else if(Page=="match")TogglePause();else if(Page=="setup")Home(); }
        void ReleaseArena()
        {
            Time.timeScale=1;statistics?.Dispose();statistics=null;
            if(match!=null)match.Scored-=OnGoal;
            goal?.RemoveFromHierarchy();goal=null;goalRemaining=0;
            if(arena!=null){arena.SetActive(false);Destroy(arena);arena=null;}
            match=null;hud=null;human=null;cameras=null;session=null;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        public void Home(){ReleaseArena();DrawHome();}
        void OnDestroy(){ReleaseArena();ReleaseCharacterPreviews();ReleaseCursor();Time.timeScale=1;}
        void QuitGame(){Time.timeScale=1;Application.Quit(0);}
        public void SetLanguage(string code)
        { English=code=="en";CloseModal();if(Page=="home")DrawHome();else if(Page=="setup")Setup(); }
        void LanguagePopup()
        {
            if(modal!=null){CloseModal();return;}
            modal=Box(screen,88,630,350,154,Paper);modalKind="language";
            Button(modal,"한국어",10,10,330,67,()=>SetLanguage("ko"),English?"option":"selected");
            Button(modal,"English",10,77,330,67,()=>SetLanguage("en"),English?"selected":"option");
        }
        void CloseModal(){modal?.RemoveFromHierarchy();modal=null;modalKind=null;}
        public void ConfigureMatch(bool spectator,int seconds,int red,int navy)
        {
            if(seconds!=300&&seconds!=600)throw new ArgumentOutOfRangeException(nameof(seconds));
            if(red<0||red>FiveDemoAssets.Steps.Length||navy<0||navy>FiveDemoAssets.Steps.Length)throw new ArgumentOutOfRangeException(nameof(red));
            Simulation=spectator;Duration=seconds;RedModel=red;NavyModel=navy;
        }
        public void Kickoff()
        {
            ReleaseArena();
            try
            {
                arena=Instantiate(assets.arena);arena.SetActive(false);match=arena.GetComponent<FiveMatch>();match.automaticTick=false;
                session=arena.AddComponent<FiveDemoSession>();session.Initialize(match,assets,RedModel,NavyModel,Duration);
                human=arena.AddComponent<FiveDemoInput>();human.match=match;human.AllowHuman=!Simulation;
                cameras=match.overview.gameObject.AddComponent<FiveDemoCamera>();
                statistics=new FiveMatchStatistics(match);
                NewPage("match",Color.clear);arena.SetActive(true);
                hud=arena.GetComponentInChildren<UIDocument>();
                arena.GetComponentInChildren<FiveHud>().ConfigurePresentation(FiveDemoAssets.Name(RedModel,English),FiveDemoAssets.Name(NavyModel,English),English);
                arena.AddComponent<FiveDemoStaminaHud>().Initialize(match,cameras,hud,stylesheet);
                arena.AddComponent<FiveDemoBenchSpectators>().Initialize(match,CharacterSetForTeam(0),CharacterSetForTeam(1));
                match.Scored+=OnGoal;
                DrawMatchHud();
                Time.timeScale=1;
            }
            catch(Exception exception)
            {
                Debug.LogException(exception);ReleaseArena();Setup();
                modal=Box(stage,240,300,1440,440,Paper);modalKind="error";
                Text(modal,T("경기를 시작하지 못했습니다","Unable to start the match"),48,40,1344,80,44,4,Ink);
                var error=Text(modal,exception.Message,48,145,1344,160,25,1,Ink);error.style.whiteSpace=WhiteSpace.Normal;
                Button(modal,L("close"),1000,340,380,65,CloseModal);
            }
        }
        void OnGoal(Team team)
        {
            goal?.RemoveFromHierarchy();
            var color=C(team==Team.Red?"#CA2A3E":"#25377D");color.a=.92f;
            goal=Box(screen,0,179,1920,182,color);goal.name="goalBanner";
            Text(goal,(team==Team.Red?"RED":"NAVY")+"  GOAL",300,0,1320,182,96,5,center:true);
            IgnoreHudPicking(goal);goalRemaining=2.8f;
        }
        public void TogglePause()
        {
            if(Page!="match"||match==null||match.State==FiveMatchState.Finished)return;
            match.Paused=!match.Paused;Time.timeScale=match.Paused?0:1;
            human.enabled=!match.Paused;cameras.InputEnabled=!match.Paused;
            ApplyCursorVisibility();
            if(!match.Paused){pause?.RemoveFromHierarchy();pause=null;return;}
            pause=FullScreenOverlay(new Color(1,1,1,.15f));pause.name="pause";
            var panel=Box(pause,390,130,1140,139,C("#101C38"));panel.name="pause-title";
            Text(panel,L("pauseTitle"),24,12,1092,115,English?96:89,5,C("#EAF7FF"),true);
            float w=English?346:288,x=(1920-w*2-23)/2;
            float bottom=57+hudLayout.ManagerHeight;
            var resume=Button(pause,L("resumeMatch"),x,945,w,92,TogglePause,"pause-primary");resume.name="pause-resume";
            resume.style.top=StyleKeyword.Auto;resume.style.bottom=bottom;
            var quit=Button(pause,L("quitMatch"),x+w+23,945,w,92,Home,"pause-secondary");quit.name="pause-quit";
            quit.style.top=StyleKeyword.Auto;quit.style.bottom=bottom;
        }
    }
}
