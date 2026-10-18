using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager5v5
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class FiveHud : MonoBehaviour
    {
        public FiveMatch match;
        readonly Dictionary<FivePlayer, ProgressBar> bars = new Dictionary<FivePlayer, ProgressBar>();
        Label scoreboard,redCommand,navyCommand,status;
        public static readonly string[] CommandNames={"T1 속공 전개","T2 전방 롱패스","T3 측면 탈압박","T4 측면 컷백","T5 진로 차단 수비","T6 패스 수신자 견제","T7 집중 압박","T8 후방 수비 강화"};
        VisualElement root;
        public int BarCount => bars.Count;
        public bool PresentationMode { get; private set; }
        public bool English { get; set; }
        public static readonly string[] EnglishCommandNames = {"T1 Fast transition", "T2 Forward long pass", "T3 Wide press escape", "T4 Wing cutback", "T5 Route-blocking defense", "T6 Mark pass receivers", "T7 Focused pressing", "T8 Reinforce rear defense"};
        public static string FormationName(bool english)=>english?"Auto defensive shape":"자동 수비 정렬";
        public static string DisplayCommand(FiveMatch game,Team team,bool english)=>game.DefensiveFormationActive(team)
            ?FormationName(english):(english?EnglishCommandNames:CommandNames)[(int)game.Decision(team).command];
        public void ConfigurePresentation(string red, string navy, bool english)
        {
            PresentationMode = true; English = english;
            foreach(var name in new[]{"pause", "restart"}) root.Q<Button>(name)?.RemoveFromHierarchy();
            foreach(var team in new[]{0,1})
            {
                var selector=root.Q<DropdownField>(team==0?"red-mode":"navy-mode");
                selector.SetValueWithoutNotify(team==0?red:navy); selector.SetEnabled(false);
                requested[team]=-1;
            }
        }
        void OnEnable()
        {
            root = GetComponent<UIDocument>().rootVisualElement;
            scoreboard = root.Q<Label>("score");
            redCommand=root.Q<Label>("red-command");navyCommand=root.Q<Label>("navy-command");status=root.Q<Label>("status");
            root.Q<Button>("pause").clicked+=()=>match.Paused=!match.Paused;
            root.Q<Button>("restart").clicked+=()=>match.ResetMatch();
            BindSelector("red-mode",Team.Red);BindSelector("navy-mode",Team.Navy);
            bars.Clear();
            var layer = root.Q<VisualElement>("heads");
            if (layer == null || match == null) return;
            layer.Clear();
            foreach (var player in match.players)
            {
                if (player.IsKeeper) continue;
                var bar = new ProgressBar { lowValue = 0, highValue = 100, title = "", pickingMode = PickingMode.Ignore };
                bar.AddToClassList("head-stamina");
                bar.AddToClassList(player.team == Team.Red ? "red" : "navy");
                layer.Add(bar); bars[player] = bar;
            }
        }
        void BindSelector(string name,Team team)
        {
            var field=root.Q<DropdownField>(name);
            var choices=new List<string>{"규칙형 감독"};choices.AddRange(CommandNames);field.choices=choices;field.index=0;
            field.RegisterValueChangedCallback(e=>
            {
                if(PresentationMode)return;
                match.ruleManagers[(int)team]=field.index==0;
                requested[(int)team]=field.index-1;
            });
        }
        readonly int[] requested={-1,-1};
        readonly List<Rect> occupied=new List<Rect>();
        void LateUpdate()
        {
            if (root?.panel == null || match == null || match.overview == null) return;
            for(var t=0;t<2;t++)
                if(requested[t]>=0&&!match.ruleManagers[t]&&match.Decision((Team)t).command!=(FiveCommand)requested[t])
                    match.SelectCommand((Team)t,(FiveCommand)requested[t],"manual-after-hold");
            scoreboard.text = $"RED  {match.RedScore} : {match.NavyScore}  NAVY    {Mathf.Max(0,match.profile.duration-match.Elapsed):000}s";
            redCommand.text=Describe(Team.Red);navyCommand.text=Describe(Team.Navy);
            status.text=match.Paused?"일시정지":match.State==FiveMatchState.Finished?$"경기 종료 · 패스 {match.PassReceptions}/{match.PassAttempts} · 슛 {match.Shots}":match.State==FiveMatchState.GoalPause?"득점 · 경기 재개 대기":$"패스 {match.PassReceptions}/{match.PassAttempts} · 슛 {match.Shots} · {match.profile.decisionSeconds:0.#}초마다 전술 판단";
            if(English) status.text=match.Paused?"Paused":match.State==FiveMatchState.Finished?"Full time":match.State==FiveMatchState.GoalPause?"Goal · restarting":$"Passes {match.PassReceptions}/{match.PassAttempts} · Shots {match.Shots} · Decision every {match.profile.decisionSeconds:0.#}s";
            var pauseButton=root.Q<Button>("pause");
            if(pauseButton!=null){pauseButton.text=match.Paused?"계속":"일시정지";pauseButton.SetEnabled(match.State!=FiveMatchState.Finished);}
            occupied.Clear();
            foreach (var pair in bars)
            {
                var value = pair.Key.Stamina.Value * 100;
                pair.Value.value = value;
                pair.Value.EnableInClassList("tired", value <= 70 && value > 40);
                pair.Value.EnableInClassList("exhausted", value <= 40);
                var position = pair.Key.transform.position + Vector3.up * pair.Key.headHeight;
                var panelPosition = RuntimePanelUtils.CameraTransformWorldToPanel(root.panel, position, match.overview);
                var rect=new Rect(panelPosition.x-20,panelPosition.y-6,40,5);
                for(int tries=0;tries<8&&occupied.Exists(r=>r.Overlaps(rect));tries++)rect.y-=7;
                occupied.Add(rect);
                pair.Value.transform.position = new Vector3(rect.x,rect.y,0);
            }
        }
        string Describe(Team team)
        {
            if(English)
            {
                if(match.DefensiveFormationActive(team))return FormationName(true)+"\nOpponent keeper possession\nManager resumes on release";
                var decision=match.Decision(team);
                return $"{EnglishCommandNames[(int)decision.command]}\n{(match.State==FiveMatchState.Finished?"Final command":"Next decision "+Mathf.Max(0,decision.nextDecisionAt-match.Elapsed).ToString("0.0")+"s")}";
            }
            if(match.DefensiveFormationActive(team))return FormationName(false)+"\n상대 골키퍼 소유 · 대형 정리\n소유 종료 즉시 감독 판단 재개";
            var d=match.Decision(team);
            if(match.State==FiveMatchState.Finished)return $"{CommandNames[(int)d.command]}\n{Reason(d.reason)}\n최종 적용 전술";
            var waiting=Mathf.Max(0,d.nextDecisionAt-match.Elapsed);
            return $"{CommandNames[(int)d.command]}\n{Reason(d.reason)}\n다음 판단 {waiting:0.0}초";
        }
        static string Reason(string value)
        {
            switch(value)
            {
                case "own-pass-already-in-flight": return "진행 중인 패스 연결 유지";
                case "stationary-wide-carrier-release-goal-block": return "측면 정체 확인 · 압박으로 전환";
                case "wide-pressure-no-progress-commit-recovery": return "측면 압박 정체 · 집중 압박으로 전환";
                case "crowded-recovery-no-progress-rebuild-denial": return "밀집 회수 정체 · 패스 수신자 견제로 재배치";
                case "tired-carrier-open-distant-exit": return "체력 부족 · 열린 전방 출구 활용";
                case "late-deficit-open-distant-exit": return "막판 추격 · 열린 전방 출구 활용";
                case "tired-possession-open-wide-link": return "체력 부족 · 열린 측면 연결 활용";
                case "late-deficit-supported-recovery-risk": return "막판 추격 · 지원 선수와 공 회수";
                case "nearby-loose-ball-window": return "가까운 자유 공 회수 기회";
                case "one-player-loose-ball-approach": return "가까운 선수로 자유 공 접근";
                case "observed-stall-change-to-carry": return "공 정체 감지 · 운반으로 전환";
                case "observed-stall-change-to-wide": return "골문 앞 정체 · 측면 공략으로 전환";
                case "observed-stall-change-to-pass": return "운반 정체 · 넓은 패스로 전환";
                case "observed-cutback-stall-rebuild-wide-links": return "컷백 정체 · 넓은 연결로 재전개";
                case "observed-wide-links-still-blocked-try-carry": return "넓은 연결도 정체 · 운반 출구 시도";
                case "no-forward-progress-change-approach": return "전진 정체 · 공격 방법 변경";
                case "incoming-ball-risk-before-next-decision": return "빠른 공 접근 · 골문 선제 보호";
                case "visible-low-central-block": return "골문 앞 밀집 · 측면으로 우회";
                case "own-wide-pattern-in-progress": return "측면 공격 · 컷백 연결 유지";
                case "pressure-with-open-distant-receiver": return "압박 탈출 · 먼 수신 경로 확보";
                case "central-pressure-open-wide-link": return "중앙 압박 · 측면 연결 활용";
                case "available-direct-forward-space": return "열린 전방 공간으로 전진";
                case "immediate-goal-risk-or-tired-defence": return "골문 위협 · 후방 수비 강화";
                case "own-goal-pressure-protect-before-exit": return "골문 앞 압박 · 안전한 출구 대기";
                case "nearby-collective-recovery-window": return "주변 수적 우위 · 함께 탈환";
                case "central-carrier-with-nearby-recovery-support": return "중앙 운반자에 회수 지원 가능";
                case "visible-wide-reception-zone": return "측면 소유자의 진로 차단";
                case "running-carrier-meets-defensive-line": return "달리는 소유자 · 수비선에서 진로 차단";
                case "deny-advanced-visible-receivers": return "전방의 위험 수신자 차단";
                case "manual": case "manual-after-hold": return "수동 지정 명령 수행";
                default: return "전술 대형 준비";
            }
        }
    }
}
