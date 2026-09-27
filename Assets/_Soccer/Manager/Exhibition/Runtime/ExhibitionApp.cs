using System;
using System.Collections;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;
using Unity.MLAgents;
using Unity.MLAgents.Policies;

namespace MachineLearning.Soccer.Manager.Exhibition
{
    [DefaultExecutionOrder(-1000)]
    [RequireComponent(typeof(UIDocument))]
    public sealed partial class ExhibitionApp : MonoBehaviour
    {
        public ExhibitionAssets assets;
        public StyleSheet stylesheet;
        VisualElement stage, viewport, screen, modal, pause, goal, controls, cameraBadge;
        ExhibitionCamera cameras;
        ExhibitionCamera.View controlsView;
        bool controlsHuman;
        Label score, clock, redDecision, navyDecision, redReward, navyReward;
        GameObject arena;
        MNG_MatchController match;
        MNG_HumanInput human;
        MNG_RewardEngine rewards;
        MNG_TacticalRewardTracker tracker;
        ExhibitionKeeperTouches keeperTouches;
        readonly int[] aiShots = new int[2], humanShots = new int[2];
        readonly System.Collections.Generic.HashSet<long> strikes = new();
        public string Page { get; private set; } = "splash";
        public bool Paused { get; private set; }
        public bool Simulation { get; private set; }
        public int Duration { get; private set; } = 300;
        public int RedModel { get; private set; } = 5;
        public int NavyModel { get; private set; } = 5;
        public MNG_MatchController Match => match;
        public bool English => LocalizationSettings.SelectedLocale?.Identifier.Code == "en";
        int guidePage, pickerTeam, pickerGroup;
        string modalKind;
        float goalRemaining;
        bool ready;
        static Color C(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
        static readonly Color Ink = C("#172417"), Paper = C("#EDF0E6"), Lime = C("#B9F54E");

        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true;
            var root = GetComponent<UIDocument>().rootVisualElement;
            root.styleSheets.Add(stylesheet);
            viewport = root.Q("viewport"); stage = root.Q("stage");
            viewport.RegisterCallback<GeometryChangedEvent>(_ => Resize());
            Box(stage, 0, 0, 1920, 1080, C("#101110"));
            Picture(stage, assets.teamLogo, 662, 425, 595, 230);
            Resize();
            yield return LocalizationSettings.InitializationOperation;
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale("ko");
            yield return LocalizationSettings.StringDatabase.GetTableAsync("Exhibition");
            LocalizationSettings.SelectedLocaleChanged += LocaleChanged;
            ready = true;
            yield return new WaitForSecondsRealtime(1.5f);
            float elapsed = 0;
            while (elapsed < .7f) { elapsed += Time.unscaledDeltaTime; stage.style.opacity = 1 - elapsed / .7f; yield return null; }
            stage.style.opacity = 1; Home();
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-exhibition-smoke") >= 0
                || Array.IndexOf(Environment.GetCommandLineArgs(), "-exhibition-ui-smoke") >= 0
                || Array.IndexOf(Environment.GetCommandLineArgs(), "-exhibition-camera-smoke") >= 0) StartCoroutine(Smoke());
        }
        void Resize()
        {
            float w = viewport.resolvedStyle.width, h = viewport.resolvedStyle.height;
            if (float.IsNaN(w) || w <= 0 || h <= 0) return;
            var factor = Mathf.Min(w / 1920f, h / 1080f);
            stage.style.scale = new Scale(new Vector3(factor, factor, 1));
            stage.style.left = (w - 1920 * factor) / 2; stage.style.top = (h - 1080 * factor) / 2;
            FitCamera();
        }
        void FitCamera()
        {
            if (arena == null) return;
            float aspect = (float)Screen.width / Screen.height, target = 16f / 9f;
            var rect = aspect > target ? new Rect((1 - target / aspect) / 2, 0, target / aspect, 1)
                : new Rect(0, (1 - aspect / target) / 2, 1, aspect / target);
            foreach (var camera in arena.GetComponentsInChildren<Camera>()) camera.rect = rect;
        }
        void OnDestroy()
        {
            if (ready) LocalizationSettings.SelectedLocaleChanged -= LocaleChanged;
            Time.timeScale = 1;
        }
        void LocaleChanged(Locale locale) { if (Page == "home") Home(); else if (Page == "setup") Setup(); }
        public void SetLanguage(string code)
        {
            CloseModal();
            LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.GetLocale(code);
        }
        string L(string key) => LocalizationSettings.StringDatabase.GetLocalizedString("Exhibition", key.Contains('.') ? key : "MNG.Exhibition." + key);
        string ModelName(int id, bool selection = false) => L((selection ? "selection." : "model.") + ExhibitionAssets.Ids[id]);
        string Kind(int id) => L(id < 6 ? "neural" : "rule");
        void NewPage(string name, Color background)
        {
            Page = name; stage.Clear(); modal = pause = goal = null; modalKind = null;
            screen = Box(stage, 0, 0, 1920, 1080, background); screen.name = name;
        }
        void Update()
        {
            if (!ready) return;
            var keyboard = Keyboard.current;
            if (keyboard?.escapeKey.wasPressedThisFrame == true)
            {
                if (modal != null) CloseModal();
                else if (Page == "match") TogglePause();
                else if (Page == "setup") Home();
            }
            if (modalKind == "guide" && keyboard?.enterKey.wasPressedThisFrame == true) NextGuide();
            if (Page != "match" || match == null) return;
            if (controlsHuman != human.IsHuman || controlsView != cameras.CurrentView) DrawControls();
            if (match.State == MNG_MatchState.Finished) { Results(); return; }
            score.text = $"{match.RedScore}  :  {match.NavyScore}";
            int secs = Mathf.CeilToInt(match.MatchRemainingSeconds);
            clock.text = $"{secs / 60:00}:{secs % 60:00}";
            redDecision.text = L("command" + (int)match.GetDecisionState(Team.Red).PreviousCommand);
            navyDecision.text = L("command" + (int)match.GetDecisionState(Team.Navy).PreviousCommand);
            redReward.text = rewards.GetCumulativeReward(Team.Red).ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture);
            navyReward.text = rewards.GetCumulativeReward(Team.Navy).ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture);
            if (!Paused && goal != null) { goalRemaining -= Time.deltaTime; if (goalRemaining <= 0) { goal.RemoveFromHierarchy(); goal = null; } }
        }
        public void Home()
        {
            Time.timeScale = 1; Paused = false;
            if (arena != null) { arena.SetActive(false); Destroy(arena); arena = null; match = null; }
            DrawHome();
        }
        void QuitGame()
        {
            Debug.Log("EXHIBITION QUIT GAME REQUESTED");
            Time.timeScale = 1;
            Application.Quit(0);
        }
        public void ConfigureMatch(bool simulation, int seconds, int red, int navy)
        {
            if (seconds != 300 && seconds != 600) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (red < 0 || red > 9 || navy < 0 || navy > 9) throw new ArgumentOutOfRangeException(nameof(red));
            Simulation = simulation; Duration = seconds; RedModel = red; NavyModel = navy;
        }
        public void Kickoff()
        {
            if (arena != null) { arena.SetActive(false); Destroy(arena); }
            arena = Instantiate(assets.arena);
            match = arena.GetComponent<MNG_MatchController>();
            match.ConfigureRuntimeV2(true);
            match.ConfigureMatchDuration(Duration, false, MNG_MatchFinishMode.SelfPlayTerminalResult);
            match.ConfigureSpawnSeedOffset(UnityEngine.Random.Range(0, 1000000));
            foreach (var agent in arena.GetComponentsInChildren<MNG_ManagerAgent>(true))
            {
                var id = agent.Team == Team.Red ? RedModel : NavyModel;
                var behavior = agent.GetComponent<BehaviorParameters>();
                behavior.BehaviorName = MNG_RuntimeV2.BehaviorName;
                behavior.BrainParameters.VectorObservationSize = MNG_RuntimeV2.ObservationSize;
                behavior.TeamId = (int)agent.Team;
                behavior.Model = id < 6 ? assets.models[id] : null;
                behavior.BehaviorType = BehaviorType.InferenceOnly;
                agent.ConfigurePolicyAssistMode(MNG_PolicyAssistMode.None);
                agent.enabled = id < 6; agent.GetComponent<DecisionRequester>().enabled = id < 6;
                agent.gameObject.SetActive(id < 6);
                if (id >= 6) { var rule = arena.AddComponent<ExhibitionRules>(); rule.team = agent.Team; rule.model = id; rule.match = match; }
            }
            arena.SetActive(true);
            FitCamera();
            match.ResetMatch();
            human = arena.GetComponentInChildren<MNG_HumanInput>(true);
            cameras = arena.GetComponentInChildren<ExhibitionCamera>();
            human.enabled = !Simulation;
            if (human.IsHuman) human.ToggleOwner();
            rewards = arena.GetComponent<MNG_RewardEngine>();
            tracker = arena.GetComponent<MNG_TacticalRewardTracker>();
            keeperTouches = arena.GetComponentInChildren<ExhibitionKeeperTouches>();
            keeperTouches.ResetCounts();
            Array.Clear(aiShots, 0, 2); Array.Clear(humanShots, 0, 2); strikes.Clear();
            arena.GetComponentInChildren<MNG_BallControl>().PlateStrikeApplied += OnStrike;
            match.GoalScored += OnGoal;
            Time.timeScale = Simulation ? 2 : 1; Paused = false;
            DrawMatch();
        }
        void OnStrike(MNG_PlateStrikeEvent e)
        {
            if (!strikes.Add(e.KickId)) return;
            int team = e.Team == Team.Red ? 0 : 1;
            // Human kicks retain Unspecified intent in the existing gameplay contract.
            // Passes can reach shot speed; classify by the armed input, not ball speed.
            if (e.Intent == MNG_KickIntent.Unspecified && human.LastArmedKickIntent == MNG_KickIntent.Shot
                && match.GetPlayerAvatar(e.Team, e.Slot).IsHuman)
                humanShots[team]++;
            else if (e.Intent == MNG_KickIntent.Shot) aiShots[team]++;
        }
        void OnGoal(Team team, long tick)
        {
            goal?.RemoveFromHierarchy();
            var color = C(team == Team.Red ? "#CA2A3E" : "#25377D"); color.a = .92f;
            goal = Box(screen, 0, 179, 1920, 182, color); goal.name = "goalBanner";
            Text(goal, (team == Team.Red ? "RED" : "NAVY") + "  GOAL", 300, 0, 1320, 182, 96, 5, center:true);
            goalRemaining = 2.8f;
        }
        public void TogglePause()
        {
            if (Page != "match") return;
            Paused = !Paused; Time.timeScale = Paused ? 0 : Simulation ? 2 : 1;
            human.enabled = !Paused && !Simulation;
            if (!Paused) { pause?.RemoveFromHierarchy(); pause = null; return; }
            pause = Box(stage, 0, 0, 1920, 1080, new Color(1, 1, 1, .15f)); pause.name = "pause";
            float titleWidth=English?1100:1140;
            var titleColor=C("#101C38");titleColor.a=.95f;
            var titlePanel=Box(pause,(1920-titleWidth)/2,90,titleWidth,139,titleColor);titlePanel.name="pauseTitlePanel";
            var title=Text(titlePanel, L("pauseTitle"), 24, 12, titleWidth-48, 115, English ? 96 : 89.3f, 5, C("#EAF7FF"), true);
            title.name="pauseTitle";title.style.unityTextOutlineWidth=0;
            float w = English ? 346 : 288, x = (1920 - w * 2 - 23) / 2;
            Button(pause, L("resumeMatch"), x, 945, w, 92, TogglePause);
            var quit = Button(pause, L("quitMatch"), x + w + 23, 945, w, 92, Home);
            quit.style.backgroundColor = new Color(1, 1, 1, .36f); quit.style.color = C("#193A25"); Border(quit, C("#193A25"), 2);
        }
        void CloseModal() { modal?.RemoveFromHierarchy(); modal = null; modalKind = null; }

        VisualElement Box(VisualElement parent, float x, float y, float w, float h, Color color)
        { var e = new VisualElement(); e.style.position = Position.Absolute; e.style.left=x; e.style.top=y; e.style.width=w; e.style.height=h; e.style.backgroundColor=color; parent.Add(e); return e; }
        Label Text(VisualElement parent, string value, float x, float y, float w, float h, float size, int weight=2, Color? color=null, bool center=false)
        {
            var l = new Label(value); Place(l,x,y,w,h); l.style.fontSize=size;
            l.style.unityFontDefinition = FontDefinition.FromSDFFont(assets.fonts[weight]);
            l.style.color=color??Color.white; if(center)l.style.unityTextAlign=TextAnchor.MiddleCenter; parent.Add(l); return l;
        }
        Button Button(VisualElement parent, string value, float x, float y, float w, float h, Action action, string cls=null)
        {
            var b = new Button(action){text=value}; Place(b,x,y,w,h); b.style.unityFontDefinition=FontDefinition.FromSDFFont(assets.fonts[3]);
            if(cls!=null)b.AddToClassList(cls); parent.Add(b); return b;
        }
        static void Place(VisualElement e,float x,float y,float w,float h)
        { e.style.position=Position.Absolute;e.style.left=x;e.style.top=y;e.style.width=w;e.style.height=h; }
        static void Border(VisualElement e,Color c,float n)
        {e.style.borderLeftWidth=e.style.borderRightWidth=e.style.borderTopWidth=e.style.borderBottomWidth=n;e.style.borderLeftColor=e.style.borderRightColor=e.style.borderTopColor=e.style.borderBottomColor=c;}
        void Picture(VisualElement p,Texture2D texture,float x,float y,float w,float h)
        { var image=new Image{image=texture,scaleMode=ScaleMode.ScaleToFit};Place(image,x,y,w,h);p.Add(image); }
    }
}
