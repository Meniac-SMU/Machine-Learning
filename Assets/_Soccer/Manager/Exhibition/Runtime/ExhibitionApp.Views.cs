using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager.Exhibition
{
    public sealed partial class ExhibitionApp
    {
        void DrawHome()
        {
            NewPage("home", C("#101310"));
            var art=Box(screen,1075,0,845,1080,C("#CCFC4B"));art.style.overflow=Overflow.Hidden;
            string[] colors={"#185837","#91BE39","#CF8B50","#4944C5","#A3CAFB"};
            for(int i=0;i<5;i++)
            {
                var ribbon=Box(art,-192+i*77,192+i*72,1267-i*173,1574-i*140,C(colors[i]));
                ribbon.style.borderTopLeftRadius=ribbon.style.borderTopRightRadius=700;
                ribbon.style.rotate=new Rotate(new Angle(-32));
            }
            var artTitle=Text(art,"THE GAME\nIS LEARNING.",65,67,660,190,70,5,Ink);artTitle.style.rotate=new Rotate(new Angle(-6));
            var ball=Box(art,400,440,288,288,C("#F3F1DC"));ball.style.borderTopLeftRadius=ball.style.borderTopRightRadius=ball.style.borderBottomLeftRadius=ball.style.borderBottomRightRadius=144;Border(ball,Ink,8);
            ball.generateVisualContent += context =>
            {
                var p=context.painter2D;p.fillColor=Ink;p.BeginPath();
                p.MoveTo(new Vector2(144,37));p.LineTo(new Vector2(173,113));p.LineTo(new Vector2(249,144));p.LineTo(new Vector2(173,175));p.LineTo(new Vector2(144,251));p.LineTo(new Vector2(115,175));p.LineTo(new Vector2(39,144));p.LineTo(new Vector2(115,113));p.ClosePath();p.Fill();
            };
            Text(art,"RED × NAVY",500,958,275,80,43,5,Ink,true);
            Box(screen,88,91,10,10,Lime);
            Text(screen,L("eyebrow"),115,77,872,58,20.4f,2);
            Picture(screen,assets.gameLogo,88,276,935,187);
            var tagline=Text(screen,L("tagline"),88,502,900,91,28.4f,1,C("#CDD2C9"));tagline.style.whiteSpace=WhiteSpace.Normal;tagline.style.unityTextAlign=TextAnchor.UpperLeft;
            var start=Button(screen,L("start"),88,662,827,104,Setup,"primary");start.style.fontSize=38.4f;start.style.unityTextAlign=TextAnchor.MiddleLeft;start.style.paddingLeft=32;
            var language=Button(screen,"Language",88,791,266,67,LanguagePopup,"text-button");
            var globe=Box(language,28,23,22,22,Color.clear);Border(globe,Color.white,1.5f);globe.style.borderTopLeftRadius=globe.style.borderTopRightRadius=globe.style.borderBottomLeftRadius=globe.style.borderBottomRightRadius=11;
            Box(globe,9,0,1,19,Color.white);Box(globe,0,9,19,1,Color.white);Chevron(language,220,25,18,Color.white);
            Button(screen,L("credits"),368,791,266,67,()=>ShowImage("credits",0),"text-button");
            Button(screen,L("guide"),648,791,267,67,()=>ShowImage("guide",0),"text-button");
            Text(screen,"MENIAC",88,997,250,26,22,5,C("#A4AFA1"));
            var quit=Button(screen,L("quitGame"),695,974,220,66,QuitGame,"text-button");
            quit.name="quitGame";Border(quit,Color.clear,0);quit.style.color=C("#BFC3BC");
        }
        void LanguagePopup()
        {
            if(modal!=null){CloseModal();return;}
            modal=Box(screen,88,630,350,154,Paper);modalKind="language";
            Button(modal,"한국어",10,10,330,67,()=>SetLanguage("ko"),English?"option":"selected");
            Button(modal,"English",10,77,330,67,()=>SetLanguage("en"),English?"selected":"option");
        }
        public void Setup()
        {
            NewPage("setup",C("#142319"));
            Text(screen,L("matchSetup"),88,61,1000,92,55,5);
            Button(screen,L("back"),English?1580:1638,69,English?250:192,65,Home,"text-button");
            TeamCard(0,88);TeamCard(1,1068);
            Text(screen,"VS",891,350,139,90,58,5,Color.white,true);Box(screen,959,471,1,77,C("#55624F"));Text(screen,"4 v 4",891,570,139,40,18,2,Color.white,true);
            Text(screen,L("playMode"),88,786,400,40,21,2,C("#C1CCBA"));
            Text(screen,L("duration"),840,786,400,40,21,2,C("#C1CCBA"));
            Button(screen,L("humanMode"),88,834,English?202:182,73,()=>{Simulation=false;Setup();},Simulation?"option":"selected");
            Button(screen,L("simMode"),English?298:278,834,English?220:202,73,()=>{Simulation=true;Setup();},Simulation?"selected":"option");
            Button(screen,L("five"),840,834,115,73,()=>{Duration=300;Setup();},Duration==300?"selected":"option");
            Button(screen,L("ten"),964,834,115,73,()=>{Duration=600;Setup();},Duration==600?"selected":"option");
            Button(screen,L("kickoff"),1524,822,307,85,Kickoff,"primary");
        }
        void TeamCard(int team,float x)
        {
            int model=team==0?RedModel:NavyModel;
            var card=Box(screen,x,192,764,561,C(team==0?"#CA2A3E":"#25377D"));
            Text(card,team==0?"01":"02",46,35,120,50,22,3);
            float pw=English?192:154;var pill=Box(card,718-pw,35,pw,50,new Color(0,0,0,.15f));Border(pill,new Color(1,1,1,.5f),1);
            Text(pill,L(team==0?(Simulation?"aiTeam":"yourTeam"):"opponent"),0,0,pw,50,23,3,Color.white,true);
            Text(card,team==0?"RED":"NAVY",46,96,672,150,125,5);Box(card,46,255,672,1,new Color(1,1,1,.28f));
            Text(card,L("teamModel"),46,282,672,25,21,2);
            var pick=Button(card,"",46,320,672,129,()=>PickModel(team,0));pick.style.backgroundColor=new Color(.039f,.047f,.082f,.83f);Border(pick,new Color(1,1,1,.26f),1);
            Text(pick,ModelName(model),21,17,570,57,42,4);
            Text(pick,model<6?"MS3-v3 · "+Kind(model):Kind(model),21,80,570,30,20,1,C("#D6D9E1"));
            Chevron(pick,613,51,26,Color.white);
            var caption=Text(card,L(team==0?"redCaption":"navyCaption"),46,471,672,60,20,1);
            caption.style.whiteSpace=WhiteSpace.Normal;
        }
        void PickModel(int team,int group)
        {
            CloseModal();pickerTeam=team;pickerGroup=group;modalKind="models";
            modal=Box(stage,0,0,1920,1080,new Color(.012f,.04f,.024f,.79f));
            float h=English?538:499,y=(1080-h)/2;
            var panel=Box(modal,240,y,1440,h,Paper);
            Box(panel,0,0,1440,73,Ink);
            Text(panel,(team==0?"RED":"NAVY")+" · "+L("selectModel"),38,0,1250,73,26,3);
            Button(panel,"×",1360,12,48,48,CloseModal,"text-button");
            float tabWidth=English?384:288;
            Button(panel,L("stepTab"),720-tabWidth-20,78,tabWidth,68,()=>PickModel(team,0),group==0?"selected":"option");
            Button(panel,L("referenceTab"),740,78,tabWidth,68,()=>PickModel(team,1),group==1?"selected":"option");
            Text(panel,L(group==0?"stepsNotice":"referenceNotice"),38,155,1364,42,20,1,C("#4D6143"));
            int count=group==0?6:4,cols=group==0?3:2;float gap=11,w=(1364-gap*(cols-1))/cols,ch=(h-226)/2;
            for(int i=0;i<count;i++)
            {
                int id=group==0?i:i+6;
                var b=Button(panel,"",38+(i%cols)*(w+gap),208+(i/cols)*(ch+11),w,ch-4,()=>{if(team==0)RedModel=id;else NavyModel=id;Setup();},(team==0?RedModel:NavyModel)==id?"selected":"option");
                var col=(team==0?RedModel:NavyModel)==id?Color.white:Ink;
                Text(b,ModelName(id,true),20,7,w-40,50,group==0?33.6f:30,4,col);
                var details=Text(b,id<6?ExhibitionAssets.Steps[id].ToString("N0",System.Globalization.CultureInfo.InvariantCulture)+" steps":L("description."+ExhibitionAssets.Ids[id]),20,62,w-40,ch-72,20,1,col);
                details.style.whiteSpace=WhiteSpace.Normal;
            }
        }
        void ShowImage(string kind,int page)
        {
            CloseModal();modalKind=kind;guidePage=page;
            modal=Box(stage,0,0,1920,1080,new Color(.012f,.04f,.024f,.79f));
            var panel=Box(modal,240,91,1440,898,Paper);
            Box(panel,0,0,1440,73,Ink);
            Text(panel,L(kind=="guide"?"guide":"credits"),38,0,1200,73,26,3);
            Button(panel,"×",1360,12,48,48,CloseModal,"text-button");
            var texture=LocalizationSettings.AssetDatabase.GetLocalizedAsset<Texture2D>("ExhibitionArt",kind=="guide"?"guide"+(page+1):"credits");
            Picture(panel,texture,0,73,1440,720);
            if(kind=="guide")
            {
                Text(panel,$"0{page+1} / 05",38,805,160,72,25,3,Ink);
                Text(panel,"ESC  ·  "+L("closeGuide"),280,805,440,72,21,1,Ink);
                Button(panel,"ENTER  ·  "+L(page==4?"finishGuide":"next"),English?1000:1114,815,English?400:288,58,NextGuide);
            }
            else Button(panel,"ESC  ·  "+L("close"),550,815,340,58,CloseModal);
        }
        void NextGuide(){if(guidePage>=4)CloseModal();else ShowImage("guide",guidePage+1);}
        void DrawMatch()
        {
            NewPage("match",Color.clear);viewport.style.backgroundColor=Color.clear;
            var bug=Box(screen,29,29,471,57,Ink);
            Box(bug,0,0,368,57,C("#FBFBF2"));
            Text(bug,"RED",0,0,99,57,24,4,Ink,true);Box(bug,0,53,99,4,C("#D62C48"));
            score=Text(bug,"0 : 0",99,0,170,57,29,4,Ink,true);
            Text(bug,"NAVY",269,0,99,57,24,4,Ink,true);Box(bug,269,53,99,4,C("#273B80"));
            clock=Text(bug,"05:00",368,0,103,57,25,3,Color.white,true);
            if(Simulation){float width=English?206:142;var badge=Box(screen,(1920-width)/2,29,width,57,C("#FFE6E9"));badge.name="speedBadge";Text(badge,L("speedBadge"),0,0,width,57,28,4,C("#CA2A3E"),true);}
            Text(screen,"MENIAC",1660,29,230,58,43.2f,5,Color.white,true);
            controls=null;DrawControls();
            float mw=English?538:461,mh=English?154:140;
            var managers=Box(screen,1891-mw*1.12f,1051-mh*1.12f,mw,mh,new Color(.063f,.137f,.098f,.93f));
            managers.name="managerHud";ScaleHud(managers);
            float first=English?220:195,second=English?176:139;
            Text(managers,L("managerLive"),13,6,first,25,15,2,C("#BE CDB9".Replace(" ","")));
            Text(managers,L("currentDecision"),first,6,second,25,15,2,C("#BECDB9"));
            Text(managers,L("cumulativeReward"),first+second,6,mw-first-second-10,25,15,2,C("#BECDB9"),true);
            for(int i=0;i<2;i++)
            {
                float y=English?39+i*53:35+i*49;
                Box(managers,13,y,4,44,C(i==0?"#F45667":"#809DFF"));
                Text(managers,i==0?"RED":"NAVY",23,y,first-30,19,18,4);
                Text(managers,Kind(i==0?RedModel:NavyModel)+" · "+ModelName(i==0?RedModel:NavyModel),23,y+21,first-26,20,15,1,C("#C5D1C2"));
                var d=Text(managers,"",first,y+7,second,30,16.3f,2);
                var r=Text(managers,"0.000",first+second,y+7,mw-first-second-10,30,20.5f,3,C("#D1F69D"),true);
                if(i==0){redDecision=d;redReward=r;}else{navyDecision=d;navyReward=r;}
            }
        }
        static void ScaleHud(VisualElement panel)
        {
            panel.style.transformOrigin=new TransformOrigin(0,0);
            panel.style.scale=new Scale(new Vector3(1.12f,1.12f,1));
        }
        void DrawControls()
        {
            controls?.RemoveFromHierarchy();controls=null;controlsHuman=human.IsHuman;
            if(Simulation)return;
            float width=English?332:288,height=controlsHuman?126:46;
            controls=Box(screen,29,1051-height*1.12f,width,height,new Color(.063f,.137f,.098f,.93f));
            controls.name="controlsHud";ScaleHud(controls);
            if(controlsHuman)
            {
                string[] keys={"W/S","A/D","E","SPACE"},descriptions={"move","turn","softKick","strongKick"};
                for(int i=0;i<4;i++)
                {
                    float x=12+(i%2)*width/2,y=6+(i/2)*38;
                    Text(controls,keys[i],x,y,i==3?60:39,32,i==3?14:17,3);
                    Text(controls,L(descriptions[i]),x+(i==3?62:43),y,width/2-(i==3?75:56),32,18,1);
                }
            }
            Text(controls,"H: "+L(controlsHuman?"aiSwitch":"humanSwitch"),12,controlsHuman?84:7,width-24,32,18,2);
        }
        void Results()
        {
            Time.timeScale=0;Paused=false;human.enabled=false;
            NewPage("result",C("#E4EBCF"));
            Text(screen,L("fullTime"),88,52,700,40,22.4f,3,Ink);
            Text(screen,"MENIAC",1620,50,210,44,26.4f,5,Ink,true);
            Text(screen,match.RedScore==match.NavyScore?L("draw"):(match.RedScore>match.NavyScore?"RED":"NAVY")+" "+L("wins"),240,97,1440,74,61.2f,5,Ink,true);
            Text(screen,"RED",450,224,270,85,59.1f,5,C("#CA2A3E"),true);
            Text(screen,"NAVY",1200,224,270,85,59.1f,5,C("#25377D"),true);
            Text(screen,ModelName(RedModel),430,319,310,34,21.5f,2,Ink,true);
            Text(screen,ModelName(NavyModel),1180,319,310,34,21.5f,2,Ink,true);
            Text(screen,$"{match.RedScore} : {match.NavyScore}",720,185,480,163,133,5,Ink,true);
            Text(screen,L("matchStats"),240,392,1440,38,22.8f,3,Ink,true);
            Stat("finalReward",rewards.GetCumulativeReward(Team.Red).ToString("+0.000;-0.000;0.000"),rewards.GetCumulativeReward(Team.Navy).ToString("+0.000;-0.000;0.000"),450,68,true);
            Stat("shots",(aiShots[0]+humanShots[0]).ToString(),(aiShots[1]+humanShots[1]).ToString(),530,62);
            Stat("aiShots",aiShots[0].ToString(),aiShots[1].ToString(),599,47);
            Stat("humanShots",humanShots[0].ToString(),humanShots[1].ToString(),646,47);
            Stat("recoveries",tracker.GetObservedRecoveryCount(Team.Red).ToString(),tracker.GetObservedRecoveryCount(Team.Navy).ToString(),710,62);
            Stat("saves",keeperTouches.GetTouches(Team.Red).ToString(),keeperTouches.GetTouches(Team.Navy).ToString(),781,62);
            Text(screen,L("resultHelp"),230,946,1030,84,23.2f,1,C("#52614B"));
            Button(screen,L("exit"),1440,946,250,84,Home);
        }
        void Stat(string key,string red,string navy,float y,float h,bool reward=false)
        {
            var row=Box(screen,230,y,1460,h,reward?C("#19311D"):Color.clear);
            row.name="stat-"+key;
            var c=reward?C("#ECF3E7"):Ink;
            Text(row,red,22,0,330,h,key=="aiShots"||key=="humanShots"?28:34.8f,3,reward?C("#C9EE99"):c);
            Text(row,L(key),352,0,756,h,24,2,c,true);
            var number=Text(row,navy,1108,0,330,h,key=="aiShots"||key=="humanShots"?28:34.8f,3,reward?C("#C9EE99"):c);number.style.unityTextAlign=TextAnchor.MiddleRight;
            if(!reward)Box(row,0,h-1,1460,1,C("#CAD3C2"));
        }
        void Chevron(VisualElement parent,float x,float y,float size,Color color)
        {
            var mark=Box(parent,x,y,size,size,Color.clear);mark.pickingMode=PickingMode.Ignore;
            mark.generateVisualContent+=context=>{var p=context.painter2D;p.strokeColor=color;p.lineWidth=2;p.BeginPath();p.MoveTo(new Vector2(1,size*.3f));p.LineTo(new Vector2(size/2,size*.7f));p.LineTo(new Vector2(size-1,size*.3f));p.Stroke();};
        }
    }
}
