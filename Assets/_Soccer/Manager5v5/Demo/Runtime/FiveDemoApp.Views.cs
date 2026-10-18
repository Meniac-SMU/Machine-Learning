using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed partial class FiveDemoApp
    {
        public void Setup()
        {
            NewPage("setup",C("#142319"));
            // Center the visible title-to-button bounds, including the shipped fonts' different top bearings.
            var content=Box(screen,0,English?-20f:-17.5f,1920,1080,Color.clear);content.name="setup-content";content.AddToClassList("match-setup");
            Text(content,L("matchSetup"),88,61,1000,92,55,5);
            Button(content,L("back"),1580,69,250,65,Home,"text-button");
            TeamCard(content,0,88);TeamCard(content,1,1068);
            Text(content,"VS",890,497,140,90,58,5,Color.white,true).name="setup-versus";
            Text(content,L("playMode"),88,912,650,40,21,2,C("#C1CCBA"));
            Text(content,L("duration"),840,912,400,40,21,2,C("#C1CCBA"));
            Button(content,L("humanMode"),88,960,310,73,()=>{Simulation=false;Setup();},Simulation?"option":"selected").name="human-mode";
            Button(content,L("simMode"),410,960,310,73,()=>{Simulation=true;Setup();},Simulation?"selected":"option").name="spectator-mode";
            Button(content,L("five"),840,960,115,73,()=>{Duration=300;Setup();},Duration==300?"selected":"option").name="duration-five";
            Button(content,L("ten"),964,960,115,73,()=>{Duration=600;Setup();},Duration==600?"selected":"option").name="duration-ten";
            Button(content,L("kickoff"),1524,948,307,85,Kickoff,"primary").name="kickoff";
        }
        void TeamCard(VisualElement parent,int team,float x)
        {
            int model=team==0?RedModel:NavyModel;
            var ink=C(team==0?"#591824":"#111B3D");
            var card=Box(parent,x,192,764,700,Color.clear);card.name="team-card-"+team;
            DrawTeamGradient(card,C(team==0?"#CA2A3E":"#25377D"));
            Text(card,team==0?"RED":"NAVY",46,4,672,85,62.5f,5).name="team-title-"+team;
            if(team==0&&!Simulation)
            {
                var yourTeam=Text(card,"Your Team",476,20,242,44,30,4,ink);
                yourTeam.name="your-team";yourTeam.style.unityTextAlign=TextAnchor.MiddleRight;
            }
            DrawCharacterSelector(card,team);
            Box(card,46,412,672,1,new Color(ink.r,ink.g,ink.b,.28f));
            Text(card,L("teamModel"),46,425,672,38,26,2,ink).name="team-model-title-"+team;
            var pick=Button(card,"",46,485.5f,672,150,()=>PickModel(team));pick.name=team==0?"red-model":"navy-model";
            pick.style.backgroundColor=C(team==0?"#591824":"#111B3D");Border(pick,new Color(1,1,1,.26f),1);
            Text(pick,FiveDemoAssets.Name(model,English),21,20,570,62,49,4).name="team-model-name-"+team;
            Text(pick,L(model==0?"rule":"neural"),21,101,570,32,23,1,C("#D6D9E1"));Chevron(pick,613,62,26,Color.white);
            var caption=Text(card,team==0&&Simulation?T("선택한 감독의 경기를 관전합니다.","Watch your chosen manager play."):L(team==0?"redCaption":"navyCaption"),46,661,672,30,20,1,ink);
            caption.style.whiteSpace=WhiteSpace.Normal;
        }
        static void DrawTeamGradient(VisualElement card,Color teamColor)
        {
            var gradient=new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(teamColor,0),new GradientColorKey(Color.Lerp(teamColor,Color.white,.70f),.5f),new GradientColorKey(teamColor,1)},
                new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(1,1)});
            card.generateVisualContent+=context=>
            {
                float w=card.contentRect.width,h=card.contentRect.height;
                if(w<1||h<1)return;
                var p=context.painter2D;
                p.fillGradient=FillGradient.MakeLinearGradient(gradient,Vector2.zero,new Vector2(w,h),AddressMode.Clamp);
                p.BeginPath();p.MoveTo(Vector2.zero);p.LineTo(new Vector2(w,0));p.LineTo(new Vector2(w,h));p.LineTo(new Vector2(0,h));p.ClosePath();p.Fill();
            };
        }
        public void PickModel(int team)
        {
            var teamInk=C(team==0?"#591824":"#111B3D");
            CloseModal();modalKind="models";modal=FullScreenOverlay(new Color(teamInk.r*.3f,teamInk.g*.3f,teamInk.b*.3f,.79f));
            modal.AddToClassList("model-picker");modal.AddToClassList(team==0?"red":"navy");
            int rows=Mathf.CeilToInt((FiveDemoAssets.Steps.Length+1)/3f);
            float panelHeight=206+rows*158;
            var panel=Box(CenteredOverlayContent(modal),240,(1080-panelHeight)*.5f,1440,panelHeight,C(team==0?"#F4EAEC":"#E9EDF5"));
            Box(panel,0,0,1440,73,teamInk);Text(panel,(team==0?"RED":"NAVY")+" · "+L("selectModel"),38,0,1250,73,26,3);
            Button(panel,"×",1360,12,48,48,CloseModal,"text-button");
            Text(panel,T("감독에 넣을 신경망을 선택하세요","Select a neural network for the manager"),38,98,1364,60,26,2,teamInk);
            for(int i=0;i<=FiveDemoAssets.Steps.Length;i++)
            {
                int id=i; bool selected=(team==0?RedModel:NavyModel)==id;
                var button=Button(panel,"",38+(i%3)*458,190+(i/3)*158,448,144,()=>{if(team==0)RedModel=id;else NavyModel=id;Setup();},selected?"selected":"option");
                button.name="model-"+id;var color=selected?Color.white:teamInk;
                Text(button,FiveDemoAssets.Name(id,English),20,15,408,59,English?32:38,4,color);
                Text(button,id==0?L("rule"):id==1?T("학습 전 초기 모델","Initial, untrained model"):L("neural"),20,84,408,37,21,1,color);
            }
        }
        void ShowCredits()
        {
            CloseModal();modalKind="credits";modal=FullScreenOverlay(new Color(0,0,0,.8f));
            var panel=Box(CenteredOverlayContent(modal),240,91,1440,898,Paper);Box(panel,0,0,1440,73,Ink);
            Text(panel,L("credits"),38,0,1200,73,26,3);Picture(panel,English?assets.creditsEn:assets.creditsKo,0,73,1440,720);
            Button(panel,"ESC · "+L("close"),550,815,340,58,CloseModal);
        }
        public void ShowGuide(int page)
        {
            CloseModal();modalKind="guide";guidePage=page;
            modal=FullScreenOverlay(new Color(.012f,.04f,.024f,.79f));
            var panel=Box(CenteredOverlayContent(modal),240,91,1440,898,Paper);panel.name="guide-page-"+page;
            Box(panel,0,0,1440,73,Ink);Text(panel,L("guide")+" · 5 v 5",38,0,1200,73,26,3);
            Button(panel,"×",1360,12,48,48,CloseModal,"text-button");
            string[] titles=English?new[]{"Two managers. Ten players.","Take control of RED attacker 1","Eight manager commands · Attack","Eight manager commands · Defense","Read the match. Compare the result."}
                :new[]{"두 감독, 열 명의 선수","RED 공격수 1에 직접 개입하세요","감독 명령 8개 · 공격과 전개","감독 명령 8개 · 수비와 압박","경기를 보고 결과를 비교하세요"};
            Text(panel,titles[page],48,105,1344,85,English?43:49,5,Ink);
            if(page==0)
            {
                GuideCard(panel,48,220,650,210,"5 v 5",T("팀마다 공격수 2명 · 수비수 2명 · 골키퍼 1명\n감독은 8개 명령 중 하나를 선택합니다.","Each team: 2 attackers · 2 defenders · 1 goalkeeper\nEach manager selects one of eight commands."));
                GuideCard(panel,722,220,670,210,T("하나의 모델 선택 화면","One model selection screen"),T("규칙형 + 0~360만 모델(40만 간격)\nRED와 NAVY에 각각 원하는 모델을 지정하세요.","Rule + 0–3.6M models (400k intervals)\nChoose a model independently for RED and NAVY."));
                DrawRoster(panel,48,465,650,250,false);DrawRoster(panel,722,465,670,250,true);
                GuideText(panel,T("5분 또는 10분 경기 · 0회는 학습 전 초기 모델입니다.","5- or 10-minute matches · The 0-step model is untrained."),48,740,1344,44);
            }
            else if(page==1)
            {
                GuideCard(panel,48,220,650,230,"H",T("플레이 모드에서 H를 누르면\nRED 공격수 1을 조작합니다. 다시 누르면 AI 복귀.","In Play mode, press H to control\nRED attacker 1. Press H again to return to AI."));
                GuideCard(panel,722,220,670,230,"WASD / ↑↓←→",T("W / S 또는 ↑ / ↓: 전진 / 후진\nA / D 또는 ← / →: 회전\nE / 1: 패스    Space / 0: 슈팅", "W / S or ↑ / ↓: forward / reverse\nA / D or ← / →: turn\nE / 1: pass    Space / 0: shoot"));
                GuideCard(panel,48,475,650,240,"← / →",T("AI 관전 중 시점을 전환합니다.\nRED 골키퍼 ↔ 전체 시점 ↔ NAVY 골키퍼", "Cycle spectator cameras while AI controls players.\nRED goalkeeper ↔ Broadcast ↔ NAVY goalkeeper"));
                GuideCard(panel,722,475,670,240,T("같은 선수 능력치","Equal player attributes"),T("속도·가속·회전·체력·킥 제한은 AI와 같습니다.\n인간 시점: 마우스 좌우 ±40°, 1.5초 뒤 정면 복귀.","Human and AI share speed, acceleration, turning,\nstamina and kicks. Mouse look: ±40°, recenter after 1.5s."));
                GuideText(panel,T("게임패드: Y 전환 · 트리거 이동 · 왼쪽 스틱 회전 · A 패스 · B 슛","Gamepad: Y toggle · Triggers move · Left stick turns · A pass · B shoot"),48,740,1344,44);
            }
            else if(page==2||page==3)
            {
                string[] ko={
                    "소유자가 전진하고 동료 2명이 전방을 지원합니다.\n가까운 압박에는 앞선 동료에게 패스를 시도합니다.",
                    "먼 전방 동료에게 지상 롱패스를 시도합니다.\n수신자와 경로가 없으면 공을 운반하며 전진합니다.",
                    "좌우로 넓혀 전진 패스 출구를 만듭니다.\n가까운 압박에는 옆·뒤쪽 동료나 골키퍼도 찾습니다.",
                    "측면으로 전개한 뒤 중앙 마무리 선수에게 연결합니다.\n골라인에 도달하기 전에도 중앙 패스가 가능합니다.",
                    "수비수 2명이 상대의 예상 이동 경로를 막습니다.\n다른 2명은 뒤를 받치며 중앙·측면 모두 대응합니다.",
                    "위험한 패스 수신자 2명을 수비수가 견제합니다.\n공과 수신자 사이에 서서 패스 연결을 방해합니다.",
                    "3명이 공 주변을 압박하고 가장 먼 1명은 대기합니다.\n탈환 후 동료는 공간을 주고 소유자가 전진합니다.",
                    "3명이 후방을 보호하고 1명은 전방 출구를 맡습니다.\n소유자는 전진하거나 전방 동료에게 패스할 수 있습니다."};
                string[] en={
                    "The carrier advances with two teammates ahead.\nNearby pressure prompts a forward support pass.",
                    "Seek a distant teammate for a long ground pass.\nCarry forward if no receiver or clear lane is available.",
                    "Spread wide to create forward passing options.\nUnder close pressure, also look sideways or back,\nincluding to the goalkeeper.",
                    "Build down the wing, then find a central finisher.\nThe central pass can start before the byline.",
                    "Two defenders block the predicted running route.\nTwo others provide cover, in central or wide areas.",
                    "Two defenders mark dangerous pass receivers.\nThey stand between the ball and their targets.",
                    "Three press near the ball; the farthest holds position.\nAfter recovery, teammates give the carrier space\nto advance.",
                    "Keep three in cover and one as a forward outlet.\nThe carrier can advance or pass to the outlet."};
                for(int i=0;i<4;i++)
                {
                    int command=(page-2)*4+i;
                    GuideCard(panel,48+(i%2)*674,220+(i/2)*245,650,220,English?FiveHud.EnglishCommandNames[command]:FiveHud.CommandNames[command],T(ko[command],en[command]));
                }
                GuideText(panel,page==2?T("감독은 기본 3초마다 명령 1개를 선택합니다. 모든 명령에서 열린 슛을 시도할 수 있습니다.","Managers choose one command every 3 seconds. All commands allow an open shot."):
                    T("상대 GK 소유 중에는 자동 수비 정렬을 적용하며, 소유 종료 시 감독 판단을 재개합니다.\n선택 명령은 8개입니다. 골키퍼의 선방·위치 선정은 독립 규칙으로 수행합니다.","Auto defensive shape applies during opponent GK possession; decisions resume on release.\nThere are eight selectable commands. Keeper saves and positioning follow independent rules."),48,732,1344,68);
            }
            else
            {
                GuideCard(panel,48,220,650,230,"ESC",T("경기 중 Esc: 일시정지 / 재개\n일시정지 화면에서 시작 화면으로 돌아갈 수 있습니다.","Esc pauses / resumes the match.\nUse End match in the pause screen to return home."));
                GuideCard(panel,722,220,670,230,T("명령의 연속 선택 제한","Consecutive command limits"),T("T7 집중 압박은 최대 2회, 나머지는 최대 3회 연속.\n제한 뒤 다른 명령을 2회 선택하면 다시 허용됩니다.\n시간 경과나 자동 수비 정렬은 대기를 줄이지 않습니다.","T7 Focused pressing: 2 in a row; other commands: 3.\nChoose another command twice to unlock it.\nTime and auto defensive shape do not count."));
                GuideCard(panel,48,475,1344,250,T("종료 통계 · RED와 NAVY의 한 막대 비교","Final statistics · One shared RED / NAVY bar"),T("슈팅 횟수 · 슈팅 성공률 · 패스 횟수 · 패스 성공률 · 골키퍼 선방 횟수\n슈팅 성공률 = 득점한 슛 / 전체 슛. 패스 성공률 = 안정적으로 받은 패스 / 전체 패스.\n막대는 두 팀 수치의 합을 100%로 나눕니다. 양쪽 모두 0이면 빈 막대입니다.","Shots · Shot conversion · Passes · Pass completion · Goalkeeper saves\nConversion = scoring shots / shots. Completion = stable receptions / passes.\nBars show each team's share of the two values. Two zeros leave an empty bar."));
                GuideText(panel,T("체력은 정지·저속 이동 중 회복됩니다. 인간 조작에도 같은 체력 규칙이 적용됩니다.","Stamina recovers while stopped or moving slowly. Human control uses the same stamina rules."),48,742,1344,44);
            }
            Text(panel,$"0{page+1} / 05",38,805,160,72,25,3,Ink);
            Button(panel,T("← 이전","← Previous"),240,815,245,58,()=>ShowGuide(Mathf.Max(0,page-1)),"option").SetEnabled(page>0);
            Text(panel,"ESC · "+L("closeGuide"),525,805,450,72,21,1,Ink);
            Button(panel,"ENTER · "+L(page==4?"finishGuide":"next"),1000,815,400,58,NextGuide);
        }
        void NextGuide(){if(guidePage==4)CloseModal();else ShowGuide(guidePage+1);}
        void GuideText(VisualElement parent,string text,float x,float y,float w,float h)
        {var label=Text(parent,text,x,y,w,h,English?23:25,1,Ink);label.style.whiteSpace=WhiteSpace.Normal;}
        void GuideCard(VisualElement parent,float x,float y,float w,float h,string title,string body)
        {
            var card=Box(parent,x,y,w,h,C("#DFE6D5"));Box(card,0,0,5,h,C("#76944B"));
            Text(card,title,24,18,w-48,59,English?29:33,4,Ink);
            GuideText(card,body,24,85,w-48,h-102);
        }
        void DrawRoster(VisualElement parent,float x,float y,float w,float h,bool navy)
        {
            var card=Box(parent,x,y,w,h,C(navy?"#25377D":"#CA2A3E"));
            Text(card,navy?"NAVY":"RED",24,15,w-48,45,28,4);
            Vector2[] p={new(90,150),new(270,100),new(270,200),new(480,100),new(480,200)};
            for(int i=0;i<5;i++)
            {
                var dot=Box(card,p[i].x-18,p[i].y-18,36,36,Color.white);
                dot.style.borderTopLeftRadius=dot.style.borderTopRightRadius=dot.style.borderBottomLeftRadius=dot.style.borderBottomRightRadius=18;
                Text(card,i==0?"GK":i<3?"DF "+i:"AT "+(i-2),p[i].x-45,p[i].y+18,90,30,18,3,Color.white,true);
            }
        }
        void Results()
        {
            statistics.Complete();Time.timeScale=0;human.enabled=false;cameras.InputEnabled=false;match.Paused=true;
            hud.rootVisualElement.style.display=DisplayStyle.None;
            Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            NewPage("results",C("#142319"));
            Text(screen,T("경기 종료","FULL TIME"),88,54,1744,85,54,5);
            Text(screen,"RED",230,155,470,110,86,5,C("#F0717E"),true);
            Text(screen,$"{match.RedScore}  :  {match.NavyScore}",700,155,520,110,85,5,Color.white,true);
            Text(screen,"NAVY",1220,155,470,110,86,5,C("#96B5FF"),true);
            Text(screen,FiveDemoAssets.Name(RedModel,English),230,260,470,65,38,3,Color.white,true);
            Text(screen,FiveDemoAssets.Name(NavyModel,English),1220,260,470,65,38,3,Color.white,true);
            var r=statistics.teams[0];var n=statistics.teams[1];
            ResultBar("shots",T("슈팅 횟수","Shots"),r.shots,n.shots,false,340);
            ResultBar("shot-rate",T("슈팅 성공률","Shot conversion"),r.ShotRate,n.ShotRate,true,448);
            ResultBar("passes",T("패스 횟수","Passes"),r.passes,n.passes,false,556);
            ResultBar("pass-rate",T("패스 성공률","Pass completion"),r.PassRate,n.PassRate,true,664);
            ResultBar("saves",T("골키퍼 선방 횟수","Goalkeeper saves"),r.saves,n.saves,false,772);
            Button(screen,L("back"),150,958,480,75,Home,"option");
            Button(screen,T("다음 경기 설정 ↗","Set up next match ↗"),1190,958,580,75,()=>{ReleaseArena();Setup();},"primary");
        }
        void ResultBar(string id,string title,float red,float navy,bool rate,float y)
        {
            var row=Box(screen,230,y,1460,96,new Color(1,1,1,.045f));row.name="stat-"+id;
            Text(row,rate?$"{red:0.0}%":red.ToString("0"),24,5,330,45,32,4,C("#F0717E"));
            Text(row,title,370,5,720,45,28,3,Color.white,true);
            var right=Text(row,rate?$"{navy:0.0}%":navy.ToString("0"),1106,5,330,45,32,4,C("#96B5FF"));right.style.unityTextAlign=TextAnchor.MiddleRight;
            var bar=new FiveComparisonBar(red,navy);bar.name="bar-"+id;Place(bar,24,62,1412,18);row.Add(bar);
        }
    }
    public sealed class FiveComparisonBar : VisualElement
    {
        public readonly float Red, Navy;
        public float RedShare=>FiveMatchStatistics.Share(Red,Navy);
        public FiveComparisonBar(float red,float navy)
        {
            Red=red;Navy=navy;
            generateVisualContent+=context=>
            {
                var p=context.painter2D;float w=contentRect.width,h=contentRect.height;
                void Rect(float x,float width,Color color)
                {if(width<=0)return;p.fillColor=color;p.BeginPath();p.MoveTo(new Vector2(x,0));p.LineTo(new Vector2(x+width,0));p.LineTo(new Vector2(x+width,h));p.LineTo(new Vector2(x,h));p.ClosePath();p.Fill();}
                if(Red+Navy<=0)Rect(0,w,new Color(.27f,.31f,.29f));
                else {Rect(0,w*RedShare,new Color(.79f,.165f,.243f));Rect(w*RedShare,w*(1-RedShare),new Color(.23f,.36f,.72f));}
            };
        }
    }
}
