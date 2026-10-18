using UnityEngine;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed partial class FiveDemoApp
    {
        VisualElement controls, cameraBadge;
        Label matchScore, matchClock, keeperLabel;
        readonly Label[] managerCommands=new Label[2], managerTimers=new Label[2];
        bool controlsHuman;
        FiveDemoCamera.View controlsView;

        void DrawMatchHud()
        {
            // Keep the shared HUD's world-space stamina bars; replace only its presentation panels.
            hud.rootVisualElement.Q<Label>("score").style.display=DisplayStyle.None;
            hud.rootVisualElement.Q<VisualElement>(className:"controls").style.display=DisplayStyle.None;
            matchScore=null;controls=null;
            PrepareHudLayout(DrawMeasuredMatchHud);
        }

        void DrawMeasuredMatchHud(HudLayout size)
        {
            hudLayout=size;
            float h=size.scoreHeight,x=8;
            var bug=Box(screen,29,29,size.ScoreWidth,h,C("#101C38"));bug.name="scoreboard";
            Box(bug,0,0,8,h,C("#D62C48")).name="red-score-line";
            Text(bug,"RED: "+ScoreModel(RedModel),x,0,size.teamWidth,h,24,3,Color.white,true).name="red-score-team";x+=size.teamWidth;
            Box(bug,x,0,size.scoreWidth,h,C("#FBFBF2"));
            matchScore=Text(bug,$"{match.RedScore} - {match.NavyScore}",x,0,size.scoreWidth,h,33,4,Ink,true);matchScore.name="match-score";x+=size.scoreWidth;
            Text(bug,"NAVY: "+ScoreModel(NavyModel),x,0,size.teamWidth,h,24,3,Color.white,true).name="navy-score-team";x+=size.teamWidth;
            Box(bug,x,0,8,h,C("#3979EF")).name="navy-score-line";x+=8;
            Box(bug,x,0,size.clockWidth,h,C("#101110"));
            matchClock=Text(bug,"",x,0,size.clockWidth,h,27,3,Color.white,true);matchClock.name="match-clock";
            var brand=Text(screen,FiveDemoAssets.GameName,1660,29,230,58,43.2f,5,Color.white,true);
            brand.name="match-brand";brand.style.unityTextOutlineColor=C("#101C38");brand.style.unityTextOutlineWidth=2f;

            cameraBadge=new VisualElement{name="cameraBadge"};cameraBadge.AddToClassList("keeper-badge");screen.Add(cameraBadge);
            Place(cameraBadge,1775-size.badgeWidth/2,94,size.badgeWidth,size.badgeHeight);
            keeperLabel=Text(cameraBadge,"",0,0,size.badgeWidth,size.badgeHeight,25,3,Color.white,true);keeperLabel.name="keeper-label";

            var managers=Box(screen,1891-size.ManagerWidth,1051-size.ManagerHeight,size.ManagerWidth,size.ManagerHeight,new Color(.063f,.137f,.098f,.93f));managers.name="managerHud";
            managers.style.top=StyleKeyword.Auto;managers.style.bottom=29;
            for(int i=0;i<2;i++)
            {
                float y=6+i*size.managerRow;var color=C(i==0?"#F45667":"#809DFF");x=16;
                Box(managers,8,y+4,4,size.managerRow-8,color);
                Text(managers,i==0?"RED":"NAVY",x,y,size.managerTeam,size.managerRow,23,4,color,true).name="manager-team-"+i;x+=size.managerTeam;
                Text(managers,"|",x,y,10,size.managerRow,22,1,C("#899C8C"),true);x+=10;
                Text(managers,FiveDemoAssets.Name(i==0?RedModel:NavyModel,English),x,y,size.managerModel,size.managerRow,22,1,C("#E2EADB"),true).name="manager-model-"+i;x+=size.managerModel;
                Text(managers,"|",x,y,10,size.managerRow,22,1,C("#899C8C"),true);x+=10;
                managerCommands[i]=Text(managers,"",x+10,y,size.managerCommand-20,size.managerRow,22,2);managerCommands[i].name="manager-command-"+i;x+=size.managerCommand;
                Text(managers,"|",x,y,10,size.managerRow,22,1,C("#899C8C"),true);x+=10;
                managerTimers[i]=Text(managers,"",x,y,size.managerTimer,size.managerRow,22,3,C("#D1F69D"),true);managerTimers[i].name="manager-timer-"+i;
            }
            controls=null;DrawMatchControls();UpdateMatchHud();
            IgnoreHudPicking(screen);
        }

        string ScoreModel(int id)=>FiveDemoAssets.ShortName(id,English);

        static void IgnoreHudPicking(VisualElement element)
        {
            element.pickingMode=PickingMode.Ignore;
            foreach(var child in element.Children())IgnoreHudPicking(child);
        }

        static void ScaleHud(VisualElement panel)
        {
            panel.style.transformOrigin=new TransformOrigin(0,0);
            panel.style.scale=new Scale(new Vector3(1.12f,1.12f,1));
        }

        void DrawMatchControls()
        {
            controls?.RemoveFromHierarchy();controlsHuman=human.IsHuman;controlsView=cameras.CurrentView;
            var size=controlsHuman?hudLayout.human:Simulation?hudLayout.spectator:hudLayout.ai;
            float width=size.width,height=size.height,row=size.rowHeight;
            controls=Box(screen,29,1051-height*1.12f,width,height,new Color(.063f,.137f,.098f,.93f));
            controls.name="controlsHud";ScaleHud(controls);
            controls.style.top=StyleKeyword.Auto;controls.style.bottom=29+height*.12f;
            if(controlsHuman)
            {
                string[] keys={"W/S · ↑/↓","A/D · ←/→","E / 1","SPACE / 0"},descriptions={"move","turn","softKick","strongKick"};
                for(int i=0;i<4;i++)
                {
                    float x=10+(i%2)*(size.leftColumn+12),y=6+(i/2)*row;
                    float keyWidth=i%2==0?size.leftKey:size.rightKey,column=i%2==0?size.leftColumn:size.rightColumn;
                    Text(controls,keys[i],x,y,keyWidth,row,i==3?14:17,3);
                    Text(controls,L(descriptions[i]),x+keyWidth+8,y,column-keyWidth-8,row,18,1);
                }
            }
            if(!Simulation)Text(controls,"H: "+L(controlsHuman?"aiSwitch":"humanSwitch"),10,6+(controlsHuman?2:0)*row,width-20,row,18,2);
            Text(controls,L(controlsHuman?"mouseLook":"cameraCycle"),10,6+(controlsHuman?3:Simulation?0:1)*row,width-20,row,18,2);
            IgnoreHudPicking(controls);
            bool showBadge=controlsView!=FiveDemoCamera.View.Broadcast;
            bool navy=controlsView==FiveDemoCamera.View.NavyKeeper;
            cameraBadge.EnableInClassList("navy",navy);cameraBadge.EnableInClassList("red",!navy);
            cameraBadge.style.display=showBadge?DisplayStyle.Flex:DisplayStyle.None;
            keeperLabel.text=navy?"NAVY - GK":controlsView==FiveDemoCamera.View.RedStriker?"RED - ST":"RED - GK";
            keeperLabel.style.color=C(navy?"#E5F4FF":"#FFE6EC");
        }

        void LateUpdate()
        {
            ApplyCursorVisibility();
            if(Page=="match"&&match!=null&&matchScore!=null)UpdateMatchHud();
        }

        void UpdateMatchHud()
        {
            if(controlsHuman!=human.IsHuman||controlsView!=cameras.CurrentView)DrawMatchControls();
            matchScore.text=$"{match.RedScore} - {match.NavyScore}";
            int seconds=Mathf.CeilToInt(Mathf.Max(0,match.profile.duration-match.Elapsed));
            matchClock.text=$"{seconds/60:00}:{seconds%60:00}";
            for(int i=0;i<2;i++)
            {
                var decision=match.Decision((Team)i);
                managerCommands[i].text=FiveHud.DisplayCommand(match,(Team)i,English);
                // Keeper possession suspends manager decisions until release, so a numeric ETA is unavailable.
                managerTimers[i].text=match.DefensiveFormationActive((Team)i)?T("GK 해제 후","GK release"):
                    match.State==FiveMatchState.GoalPause?T("재개 대기","Restart"):
                    Mathf.Max(0,decision.nextDecisionAt-match.Elapsed).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+T("초","s");
            }
        }
    }
}
