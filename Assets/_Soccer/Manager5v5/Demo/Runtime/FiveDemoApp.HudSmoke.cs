using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed partial class FiveDemoApp
    {
        IEnumerator HudSmokeBody()
        {
            foreach(int width in new[]{1280,1920})
            foreach(string locale in new[]{"ko","en"})
            {
                Screen.SetResolution(width,width*9/16,FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(.4f);
                Home();SetLanguage(locale);
                ConfigureMatch(false,300,FiveDemoAssets.LatestModelId,FiveDemoAssets.LatestModelId-1);
                Kickoff();yield return new WaitForSecondsRealtime(.2f);
                session.automaticSteps=false;
                string prefix=locale+"-"+width;
                CheckDemo(Page=="match"&&Screen.width==width,"hud-resolution-"+prefix,Screen.width+"x"+Screen.height);
                CheckDemo(hud.rootVisualElement.Q<Label>("score").resolvedStyle.display==DisplayStyle.None
                    &&hud.rootVisualElement.Q<VisualElement>(className:"controls").resolvedStyle.display==DisplayStyle.None,"old-panels-hidden-"+prefix);
                CheckDemo(arena.GetComponentInChildren<FiveHud>().BarCount==8,"stamina-retained-"+prefix);
                var benches=arena.GetComponentsInChildren<MeshFilter>().Where(m=>m.name.StartsWith("SM_Chair_Canopy")).ToArray();
                CheckDemo(benches.Length==4&&benches.All(m=>Mathf.Abs(Mathf.Abs(m.transform.TransformPoint(m.sharedMesh.bounds.center).x)-match.geometry.HalfLength/2)<.001f),"four-bench-midpoints-"+prefix);
                CheckDemo(screen.Q<Label>("match-brand").text=="SAICCER","brand-"+prefix);
                CheckDemo(screen.Q<Label>("match-brand").resolvedStyle.color==Color.white,"brand-white-broadcast-"+prefix);
                CheckDemo(cameraBadge.resolvedStyle.display==DisplayStyle.None,"broadcast-no-camera-badge-"+prefix);
                CheckDemo(controls.Query<Label>().ToList().Select(l=>l.text).SequenceEqual(new[]{"H: "+L("humanSwitch"),L("cameraCycle")}),"inherited-ai-controls-"+prefix);
                CheckDemo(screen.Q<Label>("red-score-team").text=="RED: "+T("360만","3.6M")
                    &&screen.Q<Label>("navy-score-team").text=="NAVY: "+T("320만","3.2M"),"colon-checkpoint-labels-"+prefix);
                var scoreSize=screen.Q<VisualElement>("scoreboard").layout.size;
                var managerSize=screen.Q<VisualElement>("managerHud").layout.size;
                System.IO.File.WriteAllText(System.IO.Path.Combine(smokeDirectory,prefix+"-layout.json"),JsonUtility.ToJson(hudLayout,true));
                yield return Capture(prefix+"-broadcast");
                CheckHudLayout(prefix);
                Vector2 badgeSize=Vector2.zero;
                foreach(var view in new[]{FiveDemoCamera.View.RedKeeper,FiveDemoCamera.View.NavyKeeper})
                {
                    cameras.Select(view,false);yield return new WaitForSecondsRealtime(.25f);
                    CheckDemo(screen.Q<Label>("match-brand").resolvedStyle.color==Color.white,"brand-white-follow-"+prefix+view);
                    bool navy=view==FiveDemoCamera.View.NavyKeeper;
                    CheckDemo(cameraBadge.resolvedStyle.display==DisplayStyle.Flex&&keeperLabel.text==(navy?"NAVY - GK":"RED - GK"),"keeper-badge-"+prefix+"-"+view);
                    CheckDemo(cameraBadge.resolvedStyle.backgroundColor==C(navy?"#0D142C":"#2C0B12")&&keeperLabel.resolvedStyle.color==C(navy?"#E5F4FF":"#FFE6EC"),"keeper-colors-"+prefix+"-"+view);
                    if(!navy)badgeSize=cameraBadge.worldBound.size;
                    else CheckDemo(Vector2.Distance(badgeSize,cameraBadge.worldBound.size)<.1f,"equal-keeper-badge-size-"+prefix);
                    CheckHudLayout(prefix+"-"+view);
                    yield return Capture(prefix+"-"+view);
                }
                human.ToggleOwner();yield return new WaitForSecondsRealtime(1f);
                CheckDemo(human.IsHuman&&cameras.CurrentView==FiveDemoCamera.View.RedStriker&&!cameras.IsTransitioning,"human-camera-settled-"+prefix);
                CheckDemo(cameraBadge.resolvedStyle.display==DisplayStyle.Flex&&keeperLabel.text=="RED - ST"
                    &&cameraBadge.resolvedStyle.backgroundColor==C("#2C0B12")&&keeperLabel.resolvedStyle.color==C("#FFE6EC")
                    &&Vector2.Distance(badgeSize,cameraBadge.worldBound.size)<.1f,"striker-badge-same-size-and-red-colors-"+prefix);
                var hints=controls.Query<Label>().ToList().Select(l=>l.text).ToArray();
                CheckDemo(hints.SequenceEqual(new[]{"W/S · ↑/↓",L("move"),"A/D · ←/→",L("turn"),"E / 1",L("softKick"),"SPACE / 0",L("strongKick"),"H: "+L("aiSwitch"),L("mouseLook")}),"additional-human-controls-"+prefix);
                yield return Capture(prefix+"-human");CheckHudLayout(prefix+"-human");
                human.ToggleOwner();yield return new WaitForSecondsRealtime(.15f);
                cameras.Select(FiveDemoCamera.View.Broadcast,false);yield return new WaitForSecondsRealtime(.15f);
                CheckDemo(cameraBadge.resolvedStyle.display==DisplayStyle.None,"badge-cleared-after-return-"+prefix);
                var before=match.Elapsed;
                session.Step(.2f);yield return null;yield return null;
                CheckDemo(match.Elapsed>before&&matchScore.text==$"{match.RedScore} - {match.NavyScore}","score-and-clock-live-"+prefix);
                int remaining=Mathf.CeilToInt(match.profile.duration-match.Elapsed);
                CheckDemo(matchClock.text==$"{remaining/60:00}:{remaining%60:00}","remaining-minutes-seconds-"+prefix);
                for(int team=0;team<2;team++)
                {
                    var d=match.Decision((Team)team);
                    CheckDemo(managerCommands[team].text==FiveHud.DisplayCommand(match,(Team)team,English),"live-command-"+prefix+"-"+team);
                    if(!match.DefensiveFormationActive((Team)team)&&match.State==FiveMatchState.Playing)
                        CheckDemo(managerTimers[team].text==Mathf.Max(0,d.nextDecisionAt-match.Elapsed).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+T("초","s"),"live-countdown-"+prefix+"-"+team);
                }
                HandleEscape();yield return null;
                CheckPauseLayout(prefix);yield return Capture(prefix+"-pause-layout");
                var clockText=matchClock.text;var timerText=managerTimers[0].text;
                session.Step(.2f);yield return new WaitForSecondsRealtime(.15f);
                CheckDemo(Paused&&matchClock.text==clockText&&managerTimers[0].text==timerText,"pause-freezes-hud-clock-"+prefix);
                HandleEscape();Home();yield return null;
                ConfigureMatch(true,600,0,1);Kickoff();yield return new WaitForSecondsRealtime(.15f);session.automaticSteps=false;
                CheckDemo(controls.Query<Label>().ToList().Select(l=>l.text).SequenceEqual(new[]{L("cameraCycle")}),"inherited-spectator-controls-"+prefix);
                CheckDemo(screen.Q<Label>("red-score-team").text=="RED: "+T("규칙형","Rule")
                    &&screen.Q<Label>("navy-score-team").text=="NAVY: "+T("초기 모델","0 steps"),"rule-initial-score-labels-"+prefix);
                CheckDemo(Vector2.Distance(scoreSize,screen.Q<VisualElement>("scoreboard").layout.size)<.1f
                    &&Vector2.Distance(managerSize,screen.Q<VisualElement>("managerHud").layout.size)<.1f,"background-fixed-across-models-"+prefix);
                CheckDemo(screen.Q<Label>("manager-model-0").text==FiveDemoAssets.Name(0,English)
                    &&screen.Q<Label>("manager-model-1").text==FiveDemoAssets.Name(1,English),"rule-initial-manager-labels-"+prefix);
                CheckHudLayout(prefix+"-spectator");yield return Capture(prefix+"-spectator");
                if(English)
                {
                    Home();yield return null;ConfigureMatch(true,600,2,3);Kickoff();yield return new WaitForSecondsRealtime(.15f);session.automaticSteps=false;
                    CheckDemo(screen.Q<Label>("red-score-team").text=="RED: 400K"&&screen.Q<Label>("navy-score-team").text=="NAVY: 800K","thousand-step-score-labels-"+prefix);
                    CheckDemo(screen.Q<Label>("manager-model-0").text=="400K steps"&&screen.Q<Label>("manager-model-1").text=="800K steps","thousand-step-manager-labels-"+prefix);
                    CheckHudLayout(prefix+"-thousands");yield return Capture(prefix+"-thousands");
                }
                foreach(var scoringTeam in new[]{Team.Red,Team.Navy})
                {
                    match.ResetRound();match.Score(scoringTeam);yield return null;
                    CheckDemo(goal!=null&&goal.Q<Label>().text==(scoringTeam==Team.Red?"RED":"NAVY")+"  GOAL","goal-from-score-event-"+prefix+scoringTeam);
                    CheckDemo(goal.layout==new Rect(0,179,1920,182)&&Mathf.Approximately(goal.resolvedStyle.backgroundColor.a,.92f)
                        &&goal.Q<Label>().resolvedStyle.fontSize==96,"inherited-goal-geometry-opacity-font-"+prefix+scoringTeam);
                    var expectedGoal=C(scoringTeam==Team.Red?"#CA2A3E":"#25377D");expectedGoal.a=.92f;
                    CheckDemo(goal.resolvedStyle.backgroundColor==expectedGoal,"inherited-goal-team-color-"+prefix+scoringTeam);
                    TogglePause();float held=goalRemaining;yield return new WaitForSecondsRealtime(.15f);
                    CheckDemo(goalRemaining==held&&goal!=null,"pause-holds-goal-"+prefix+scoringTeam);TogglePause();
                    yield return Capture(prefix+"-goal-"+scoringTeam);
                    yield return new WaitForSecondsRealtime(2.85f);
                    CheckDemo(goal==null,"goal-clears-after-2-8-seconds-"+prefix+scoringTeam);
                }
                match.ResetRound();cameras.Select(FiveDemoCamera.View.RedKeeper,false);yield return new WaitForSecondsRealtime(.25f);
                var first=match.players[1];var second=match.players[2];
                var spot=match.players[0].transform.position+match.players[0].transform.forward*12;
                foreach(var player in new[]{first,second})
                {
                    player.Body.interpolation=RigidbodyInterpolation.None;
                    player.Body.position=spot;player.Body.rotation=Quaternion.identity;
                    player.transform.SetPositionAndRotation(spot,Quaternion.identity);
                }
                Physics.SyncTransforms();yield return new WaitForSecondsRealtime(.2f);
                var firstBar=hud.rootVisualElement.Q("demo-stamina-1");var secondBar=hud.rootVisualElement.Q("demo-stamina-2");
                yield return Capture(prefix+"-stamina-overlap");
                CheckDemo(firstBar.resolvedStyle.display==DisplayStyle.Flex&&secondBar.resolvedStyle.display==DisplayStyle.Flex
                    &&firstBar.worldBound.Overlaps(secondBar.worldBound),"player-view-overlap-allowed-"+prefix,
                    $"positions={first.transform.position}/{second.transform.position}, bars={firstBar.worldBound}/{secondBar.worldBound}");
                var keeper=match.players[0].transform;
                var nearSpot=keeper.position+keeper.forward*12-match.overview.transform.right*2;
                var farSpot=keeper.position+keeper.forward*36+match.overview.transform.right*4;
                first.Body.position=nearSpot;first.transform.position=nearSpot;
                second.Body.position=farSpot;second.transform.position=farSpot;
                Physics.SyncTransforms();yield return new WaitForSecondsRealtime(.2f);
                yield return Capture(prefix+"-stamina-perspective");
                float nearDepth=match.overview.WorldToViewportPoint(StaminaHead(first)).z;
                float farDepth=match.overview.WorldToViewportPoint(StaminaHead(second)).z;
                float expectedRatio=farDepth/nearDepth;
                float widthRatio=firstBar.worldBound.width/secondBar.worldBound.width;
                float heightRatio=firstBar.worldBound.height/secondBar.worldBound.height;
                CheckDemo(expectedRatio>2&&Mathf.Abs(widthRatio-expectedRatio)<.01f&&Mathf.Abs(heightRatio-expectedRatio)<.01f,
                    "stamina-perspective-scales-with-distance-"+prefix,
                    $"nearDepth={nearDepth:R}, farDepth={farDepth:R}, nearSize={firstBar.worldBound.size}, farSize={secondBar.worldBound.size}, expectedRatio={expectedRatio:R}, widthRatio={widthRatio:R}, heightRatio={heightRatio:R}");
                CheckStaminaLayout(prefix+"-perspective");
                cameras.Select(FiveDemoCamera.View.Broadcast,false);yield return new WaitForSecondsRealtime(.25f);
                CheckStaminaLayout(prefix+"-return-broadcast");
                first.Stamina.SetForScenario(.7001f);second.Stamina.SetForScenario(.7f);match.players[3].Stamina.SetForScenario(.4f);
                foreach(var colorView in new[]{FiveDemoCamera.View.Broadcast,FiveDemoCamera.View.RedKeeper})
                {
                    cameras.Select(colorView,false);yield return new WaitForSecondsRealtime(.25f);
                    CheckStaminaLayout(prefix+"-color-boundaries-"+colorView);
                    yield return Capture(prefix+"-stamina-colors-"+colorView);
                }
                match.ResetRound();cameras.Select(FiveDemoCamera.View.Broadcast,false);
                for(int command=0;command<8;command+=2)
                {
                    for(int team=0;team<2;team++)
                        CheckDemo(match.SelectCommand((Team)team,(FiveCommand)(command+team),"display-check",true),"select-display-command-"+prefix+"-"+(command+team));
                    yield return null;yield return null;
                    for(int team=0;team<2;team++)
                    {
                        var expected=(English?FiveHud.EnglishCommandNames:FiveHud.CommandNames)[command+team];
                        CheckDemo(managerCommands[team].text==expected&&expected.StartsWith("T"+(command+team+1)+" "),"display-command-keeps-t-number-"+prefix+"-"+(command+team),managerCommands[team].text);
                    }
                    CheckHudLayout(prefix+"-commands-"+command);
                    yield return Capture(prefix+"-commands-"+(command+1)+"-"+(command+2));
                }
                foreach(Team defendingTeam in new[]{Team.Red,Team.Navy})
                {
                    match.ResetRound();
                    int keeperIndex=(int)FiveMatch.Other(defendingTeam)*5;
                    var retained=match.Decision(defendingTeam).command;
                    var revision=match.Decision(defendingTeam).revision;
                    bool tactics=match.tacticsEnabled,keepers=match.keepersEnabled;
                    match.tacticsEnabled=false;match.keepersEnabled=false;
                    var anchor=match.Anchor(match.players[keeperIndex]);
                    var point=new Vector3(anchor.x,match.ballSpawn.y,anchor.y);
                    match.ball.transform.position=point;match.ball.position=point;match.ball.linearVelocity=Vector3.zero;
                    Physics.SyncTransforms();match.SimulatePhysicsStep(.08f);
                    match.tacticsEnabled=tactics;match.keepersEnabled=keepers;
                    yield return null;yield return null;
                    CheckDemo(match.Owner==keeperIndex&&match.DefensiveFormationActive(defendingTeam),"actual-opponent-keeper-possession-"+prefix+"-"+defendingTeam);
                    CheckDemo(managerCommands[(int)defendingTeam].text==FiveHud.FormationName(English)
                        &&managerTimers[(int)defendingTeam].text==T("GK 해제 후","GK release"),"auto-state-replaces-stale-command-"+prefix+"-"+defendingTeam);
                    CheckDemo(match.Decision(defendingTeam).command==retained&&match.Decision(defendingTeam).revision==revision
                        &&managerCommands[1-(int)defendingTeam].text==(English?FiveHud.EnglishCommandNames:FiveHud.CommandNames)[(int)match.Decision(FiveMatch.Other(defendingTeam)).command],"auto-state-preserves-choice-and-other-team-"+prefix+"-"+defendingTeam);
                    CheckHudLayout(prefix+"-auto-"+defendingTeam);
                    yield return Capture(prefix+"-auto-"+defendingTeam);
                    match.ball.position=new Vector3(0,match.ballSpawn.y,25);match.ball.transform.position=match.ball.position;match.ball.linearVelocity=Vector3.zero;
                    Physics.SyncTransforms();match.SimulatePhysicsStep(.02f);yield return null;yield return null;
                    var resumed=match.Decision(defendingTeam);
                    var countdown=Mathf.Max(0,resumed.nextDecisionAt-match.Elapsed).ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+T("초","s");
                    CheckDemo(!match.DefensiveFormationActive(defendingTeam)
                        &&managerCommands[(int)defendingTeam].text==(English?FiveHud.EnglishCommandNames:FiveHud.CommandNames)[(int)resumed.command]
                        &&managerTimers[(int)defendingTeam].text==countdown,"command-restored-after-keeper-release-"+prefix+"-"+defendingTeam,
                        $"active={match.DefensiveFormationActive(defendingTeam)}, selected={resumed.command}, label={managerCommands[(int)defendingTeam].text}, countdown={managerTimers[(int)defendingTeam].text}/{countdown}");
                }
                Home();yield return null;
                CheckDemo(goal==null&&screen.Q("goalBanner")==null,"goal-clean-on-home-"+prefix);
                for(int page=2;page<5;page++)
                {
                    ShowGuide(page);yield return Capture(prefix+"-guide-"+(page+1));
                    CheckGuideLabels(prefix,page);
                }
                CloseModal();
            }
            CheckDemo(hudLayouts.Count==2&&hudLayouts[false].teamWidth!=hudLayouts[true].teamWidth
                &&hudLayouts[false].managerCommand!=hudLayouts[true].managerCommand
                &&hudLayouts[false].human.width!=hudLayouts[true].human.width,"independent-korean-english-sizes");
        }

        void CheckHudLayout(string prefix)
        {
            var scoreboard=screen.Q<VisualElement>("scoreboard");var managers=screen.Q<VisualElement>("managerHud");
            var brand=screen.Q<Label>("match-brand");var bounds=screen.worldBound;
            float margin=29*stage.resolvedStyle.scale.value.x;
            var full=viewport.worldBound;
            CheckDemo(Mathf.Abs(scoreboard.worldBound.yMin-full.yMin-margin)<1
                &&Mathf.Abs(brand.worldBound.yMin-full.yMin-margin)<1
                &&Mathf.Abs(full.yMax-managers.worldBound.yMax-margin)<1
                &&Mathf.Abs(full.yMax-controls.worldBound.yMax-margin)<1,"hud-anchored-to-screen-edges-"+prefix);
            CheckDemo(brand.resolvedStyle.color==Color.white&&brand.resolvedStyle.unityTextOutlineColor==C("#101C38")
                &&Mathf.Abs(brand.resolvedStyle.unityTextOutlineWidth-2f)<.001f,"white-brand-thin-navy-outline-"+prefix);
            CheckDemo(scoreboard.worldBound.xMax<brand.worldBound.xMin&&controls.worldBound.xMax<managers.worldBound.xMin,"corner-panels-separated-"+prefix);
            foreach(var panel in new[]{scoreboard,managers,controls,(VisualElement)brand})
                CheckDemo(bounds.Contains(panel.worldBound.min)&&bounds.Contains(panel.worldBound.max),"panel-onscreen-"+prefix+"-"+panel.name);
            CheckDemo(managers.Query<Label>().ToList().Count(l=>l.name.StartsWith("manager-team-"))==2,"two-manager-rows-"+prefix);
            for(int team=0;team<2;team++)
                CheckDemo(screen.Q<Label>("manager-team-"+team).resolvedStyle.unityTextAlign==TextAnchor.MiddleCenter
                    &&screen.Q<Label>("manager-model-"+team).resolvedStyle.unityTextAlign==TextAnchor.MiddleCenter
                    &&managerTimers[team].resolvedStyle.unityTextAlign==TextAnchor.MiddleCenter
                    &&managerCommands[team].resolvedStyle.unityTextAlign==TextAnchor.MiddleLeft,"manager-column-alignments-"+prefix+"-"+team);
            var scoreTeam=screen.Q<Label>("navy-score-team");
            float scoreNameMax=ScoreTeamNames().Max(s=>scoreTeam.MeasureTextSize(s,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined).x);
            // Allow the observed 3 px difference between the initial sizing probe and the rendered label.
            CheckDemo(scoreTeam.layout.width-scoreNameMax>=20&&scoreTeam.layout.width-scoreNameMax<=28
                &&scoreTeam.style.width==screen.Q<Label>("red-score-team").style.width,"equal-team-width-longest-name-expanded-padding-"+prefix,
                $"red={screen.Q<Label>("red-score-team").layout.width:R}, navy={scoreTeam.layout.width:R}, longest={scoreNameMax:R}, specified={hudLayout.teamWidth:R}");
            var modelLabel=screen.Q<Label>("manager-model-0");
            float modelMax=ManagerModelNames().Max(s=>modelLabel.MeasureTextSize(s,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined).x);
            float timerMax=TimerLabels().Max(s=>managerTimers[0].MeasureTextSize(s,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined).x);
            CheckDemo(modelLabel.layout.width>modelMax+12&&modelLabel.layout.width<226
                &&managerTimers[0].layout.width>=timerMax&&managerTimers[0].layout.width-timerMax<=16,"model-padding-between-prior-versions-timer-retained-"+prefix,
                $"model={modelLabel.layout.width:R}/{modelMax:R}, timer={managerTimers[0].layout.width:R}/{timerMax:R}");
            CheckDemo(matchClock.layout.width>91&&matchClock.layout.width<110
                &&hudLayout.managerCommand>(English?290:170)&&hudLayout.managerCommand<312,"clock-command-intermediate-width-"+prefix);
            foreach(var panel in new[]{scoreboard,managers,controls})
            foreach(var label in panel.Query<Label>().ToList())
            {
                var measured=label.MeasureTextSize(label.text,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined);
                CheckDemo(measured.x<=label.layout.width+1&&measured.y<=label.layout.height+1,"hud-text-fits-"+prefix,label.text+" "+measured+" / "+label.layout.size);
            }
            foreach(string command in ManagerCommandLabels())
                CheckDemo(managerCommands[0].MeasureTextSize(command,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined).x<=managerCommands[0].layout.width+1,"all-command-names-fit-"+prefix,command);
            if(cameraBadge.resolvedStyle.display!=DisplayStyle.None)
                CheckDemo(cameraBadge.worldBound.yMin>brand.worldBound.yMax&&Mathf.Abs(cameraBadge.worldBound.center.x-brand.worldBound.center.x)<1,"badge-below-brand-"+prefix);
            CheckStaminaLayout(prefix);
        }

        void CheckStaminaLayout(string prefix)
        {
            bool close=cameras.CurrentView!=FiveDemoCamera.View.Broadcast;
            var layer=hud.rootVisualElement.Q("demo-stamina-layer");
            CheckDemo(layer!=null&&hud.rootVisualElement.Q("heads").resolvedStyle.display==DisplayStyle.None,"demo-stamina-replaces-old-layer-"+prefix);
            CheckDemo(layer.childCount==8&&layer.Q("demo-stamina-0")==null&&layer.Q("demo-stamina-5")==null,"no-goalkeeper-stamina-in-any-view-"+prefix);
            var visible=layer.Children().Where(b=>b.resolvedStyle.display==DisplayStyle.Flex).ToArray();
            CheckDemo(visible.Length>0&&visible.All(b=>Mathf.Abs(b.layout.height-(close?18:6))<.1f
                &&Mathf.Abs(b.layout.width-(close?64:40))<.1f
                &&Mathf.Abs(b.resolvedStyle.opacity-(close?.7f:1))<.001f),"stamina-height-and-opacity-"+prefix,
                string.Join("; ",visible.Select(b=>$"{b.name}: layout={b.layout.size}, rendered={b.worldBound.size}, opacity={b.resolvedStyle.opacity}")));
            if(close)CheckDemo(visible.All(b=>Mathf.Abs(b.worldBound.width/b.worldBound.height-64*1.3f/18)<.01f),"player-stamina-width-plus-30-percent-height-retained-"+prefix);
            foreach(var player in match.players.Where(p=>!p.IsKeeper))
            {
                var expected=player.Stamina.Value>.7f?Color.white:player.Stamina.Value>.4f?C("#B88520"):C("#B53636");
                CheckDemo(layer.Q("demo-stamina-"+player.Index).Q("fill").resolvedStyle.backgroundColor==expected,"stamina-value-color-"+prefix+"-"+player.Index);
            }
            if(!close)CheckDemo(visible.Length==8&&visible.All(b=>Mathf.Abs(b.worldBound.width-40)<.1f&&Mathf.Abs(b.worldBound.height-6)<.1f),"eight-broadcast-stamina-bars-fixed-size-"+prefix);
            else
            {
                foreach(var player in match.players.Where(p=>!p.IsKeeper))
                {
                    var bar=layer.Q("demo-stamina-"+player.Index);
                    if(bar.resolvedStyle.display!=DisplayStyle.Flex)continue;
                    var anchor=RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel,StaminaHead(player),match.overview);
                    CheckDemo(Mathf.Abs(bar.worldBound.center.x-anchor.x)<1&&Mathf.Abs(bar.worldBound.yMax-anchor.y)<1,"stamina-anchored-directly-above-head-"+prefix+"-"+player.Index);
                }
                if(cameras.CurrentView==FiveDemoCamera.View.RedStriker)
                {
                    var own=layer.Q("demo-stamina-3");
                    CheckDemo(own.resolvedStyle.display==DisplayStyle.Flex&&own.ClassListContains("own"),"own-striker-head-stamina-visible-"+prefix);
                }
                else CheckDemo(!visible.Any(b=>b.ClassListContains("own")),"keeper-view-has-no-own-stamina-"+prefix);
            }
        }

        static Vector3 StaminaHead(FivePlayer player)
        {
            var motion=player.GetComponentInParent<FiveDemoMotion>();
            var head=motion.PlayerTarget(player.Index).position;
            head.y=motion.PlayerRenderers(player.Index).Where(r=>r.enabled).Max(r=>r.bounds.max.y)+.42f;
            return head;
        }
    }
}
