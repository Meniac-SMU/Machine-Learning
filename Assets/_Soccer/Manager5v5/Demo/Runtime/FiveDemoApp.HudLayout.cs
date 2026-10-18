using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed partial class FiveDemoApp
    {
        [Serializable] sealed class ControlLayout
        {
            public float width,height,rowHeight,leftColumn,rightColumn,leftKey,rightKey;
        }
        [Serializable] sealed class HudLayout
        {
            public string language;
            public float teamWidth,scoreWidth,clockWidth,scoreHeight;
            public float managerTeam,managerModel,managerCommand,managerTimer,managerRow;
            public float badgeWidth,badgeHeight;
            public ControlLayout human,ai,spectator;
            public float ScoreWidth=>16+2*teamWidth+scoreWidth+clockWidth;
            public float ManagerWidth=>24+managerTeam+managerModel+managerCommand+managerTimer+30;
            public float ManagerHeight=>12+2*managerRow;
        }
        readonly Dictionary<bool,HudLayout> hudLayouts=new();
        HudLayout hudLayout;

        IEnumerable<string> ScoreTeamNames()=>new[]{"RED","NAVY"}.SelectMany(team=>
            Enumerable.Range(0,FiveDemoAssets.Steps.Length+1).Select(id=>team+": "+ScoreModel(id)));
        IEnumerable<string> ManagerModelNames()=>Enumerable.Range(0,FiveDemoAssets.Steps.Length+1)
            .Select(id=>FiveDemoAssets.Name(id,English));
        IEnumerable<string> ManagerCommandLabels()=>(English?FiveHud.EnglishCommandNames:FiveHud.CommandNames)
            .Concat(new[]{FiveHud.FormationName(English)});
        IEnumerable<string> TimerLabels()=>Enumerable.Range(0,100)
            .Select(n=>(n/10f).ToString("0.0",CultureInfo.InvariantCulture)+T("초","s"))
            .Concat(new[]{T("GK 해제 후","GK release"),T("재개 대기","Restart")});

        void PrepareHudLayout(Action<HudLayout> ready)
        {
            if(hudLayouts.TryGetValue(English,out var cached)){ready(cached);return;}
            var page=screen;bool english=English;
            var layer=Box(page,0,0,1920,1080,Color.clear);layer.style.visibility=Visibility.Hidden;
            var probes=new Dictionary<(int,int),Label>();
            Label Probe(int size,int weight)
            {
                var key=(size,weight);
                if(!probes.TryGetValue(key,out var label))
                {
                    label=Text(layer,"Measure",0,0,1600,100,size,weight);
                    label.pickingMode=PickingMode.Ignore;probes[key]=label;
                }
                return label;
            }
            foreach(var spec in new[]{(24,3),(33,4),(27,3),(23,4),(22,1),(22,2),(22,3),(25,3),(18,1),(18,2),(17,3),(14,3)})Probe(spec.Item1,spec.Item2);
            var sentinel=Text(layer,"",0,0,1,1,18);
            sentinel.RegisterCallback<GeometryChangedEvent>(_=>
            {
                if(layer.parent==null)return;
                if(screen!=page||Page!="match"||English!=english){layer.RemoveFromHierarchy();return;}
                float W(int size,int weight,IEnumerable<string> labels)=>Mathf.Ceil(labels.Max(s=>
                    Probe(size,weight).MeasureTextSize(s,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined).x));
                float H(int size,int weight)=>Mathf.Ceil(Probe(size,weight).MeasureTextSize("NAVY 초기 모델",0,
                    VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined).y);
                var m=new HudLayout{language=T("ko","en")};
                m.teamWidth=W(24,3,ScoreTeamNames())+24;
                m.scoreWidth=W(33,4,Enumerable.Range(0,100).Select(n=>n+" - "+n))+12;
                m.clockWidth=Mathf.Ceil((W(27,3,Enumerable.Range(0,10).Select(n=>$"{n}{n}:{n}{n}"))+12+110)/2);
                m.scoreHeight=Mathf.Max(H(24,3),H(33,4),H(27,3))+8;
                m.managerTeam=W(23,4,new[]{"RED","NAVY"})+12;
                m.managerModel=Mathf.Ceil((W(22,1,ManagerModelNames())+12+226)/2);
                m.managerCommand=Mathf.Ceil((W(22,2,ManagerCommandLabels())+12+312)/2);
                m.managerTimer=W(22,3,TimerLabels())+12;
                m.managerRow=Mathf.Max(H(23,4),H(22,1),H(22,2),H(22,3))+8;
                m.badgeWidth=W(25,3,new[]{"RED - GK","NAVY - GK","RED - ST"})+16;
                m.badgeHeight=H(25,3)+8;
                float row=H(18,2)+4;
                float leftKey=W(17,3,new[]{"W/S · ↑/↓","E / 1"}),rightKey=Mathf.Max(W(17,3,new[]{"A/D · ←/→"}),W(14,3,new[]{"SPACE / 0"}));
                float left=leftKey+8+W(18,1,new[]{L("move"),L("softKick")});
                float right=rightKey+8+W(18,1,new[]{L("turn"),L("strongKick")});
                m.human=new ControlLayout{width=16+Mathf.Max(left+12+right,W(18,2,new[]{"H: "+L("aiSwitch"),L("mouseLook")})),height=12+4*row,rowHeight=row,leftColumn=left,rightColumn=right,leftKey=leftKey,rightKey=rightKey};
                m.ai=new ControlLayout{width=16+W(18,2,new[]{"H: "+L("humanSwitch"),L("cameraCycle")}),height=12+2*row,rowHeight=row};
                m.spectator=new ControlLayout{width=16+W(18,2,new[]{L("cameraCycle")}),height=12+row,rowHeight=row};
                m.human.width=Mathf.Ceil(Mathf.Max(m.human.width,20+12+left+right));
                m.ai.width=Mathf.Ceil((m.ai.width+(English?300:246))/2);
                m.spectator.width=Mathf.Ceil((m.spectator.width+(English?240:210))/2);
                float extra=(m.human.width-20-12-left-right)/2;
                m.human.leftColumn+=extra;m.human.rightColumn+=extra;
                hudLayouts[english]=m;layer.RemoveFromHierarchy();ready(m);
            });
        }
    }
}
