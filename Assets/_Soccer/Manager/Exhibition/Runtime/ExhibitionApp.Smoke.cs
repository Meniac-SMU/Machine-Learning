using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace MachineLearning.Soccer.Manager.Exhibition
{
    public sealed partial class ExhibitionApp
    {
        [Serializable] sealed class SmokeEvidence
        {
            public bool passed;
            public string completedAtUtc;
            public string[] checks, errors;
        }
        readonly List<string> smokeChecks=new(),smokeErrors=new();
        string smokeDirectory;
        Keyboard smokeKeyboard;
        Mouse smokeMouse;
        IEnumerator Smoke()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"-exhibitionEvidence");
            smokeDirectory=index>=0?args[index+1]:Path.Combine(Application.persistentDataPath,"ExhibitionSmoke");
            Directory.CreateDirectory(smokeDirectory);
            smokeKeyboard=InputSystem.AddDevice<Keyboard>("ExhibitionSmokeKeyboard");
            smokeMouse=InputSystem.AddDevice<Mouse>("ExhibitionSmokeMouse");
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.logMessageReceived+=SmokeLog;
            bool uiOnly=Array.IndexOf(args,"-exhibition-ui-smoke")>=0;
            var routine=uiOnly?UiSmokeSteps():SmokeSteps();
            while(true)
            {
                bool next=false;object current=null;
                try{next=routine.MoveNext();if(next)current=routine.Current;}
                catch(Exception e){smokeErrors.Add(e.ToString());}
                if(!next)break;
                yield return current;
            }
            Application.logMessageReceived-=SmokeLog;
            var evidence=new SmokeEvidence{passed=smokeErrors.Count==0,completedAtUtc=DateTime.UtcNow.ToString("O"),checks=smokeChecks.ToArray(),errors=smokeErrors.ToArray()};
            File.WriteAllText(Path.Combine(smokeDirectory,"result.json"),JsonUtility.ToJson(evidence,true));
            Debug.Log("EXHIBITION SMOKE "+(evidence.passed?"PASS":"FAIL"));
            if(uiOnly&&evidence.passed)
            {
                Application.wantsToQuit+=()=>{File.WriteAllText(Path.Combine(smokeDirectory,"quit-observed.json"),"{\"buttonClickRequestedApplicationQuit\":true}");return true;};
                File.WriteAllText(Path.Combine(smokeDirectory,"quit-button-layout.txt"),screen.Q<Button>("quitGame").worldBound.ToString());
                yield return Click(L("quitGame"));
                yield return new WaitForSecondsRealtime(5);
                evidence.passed=false;evidence.errors=new[]{"Quit button did not terminate the Player."};
                File.WriteAllText(Path.Combine(smokeDirectory,"result.json"),JsonUtility.ToJson(evidence,true));
            }
            InputSystem.RemoveDevice(smokeKeyboard);
            InputSystem.RemoveDevice(smokeMouse);
            Time.timeScale=1;Application.Quit(evidence.passed?0:1);
        }
        void SmokeLog(string condition,string trace,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)smokeErrors.Add(condition+"\n"+trace);}
        void Check(bool ok,string description){if(!ok)throw new Exception("Smoke check failed: "+description);smokeChecks.Add(description);}
        IEnumerator Press(Key key)
        {
            InputSystem.QueueStateEvent(smokeKeyboard,new KeyboardState(key));yield return null;yield return null;
            InputSystem.QueueStateEvent(smokeKeyboard,new KeyboardState());yield return null;
        }
        IEnumerator Click(string text)
        {
            var button=stage.Query<Button>().ToList().Single(b=>b.text==text);
            var center=button.worldBound.center;var position=new Vector2(center.x,Screen.height-center.y);
            InputSystem.QueueStateEvent(smokeMouse,new MouseState{position=position});yield return null;
            InputSystem.QueueStateEvent(smokeMouse,new MouseState{position=position,buttons=1});yield return null;yield return null;
            InputSystem.QueueStateEvent(smokeMouse,new MouseState{position=position});yield return null;yield return null;
        }
        IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.25f);
            ScreenCapture.CaptureScreenshot(Path.Combine(smokeDirectory,name+".png"));
            yield return new WaitForSecondsRealtime(.5f);
            var layout=new List<string>();
            stage.Query<Label>().ForEach(l=>{var r=l.worldBound;layout.Add(l.text.Replace('\n',' ') + " | " + r.ToString());});
            File.WriteAllLines(Path.Combine(smokeDirectory,name+".layout.txt"),layout);
        }
        IEnumerator UiSmokeSteps()
        {
            foreach(var locale in new[]{"ko","en"})
            {
                Home();SetLanguage(locale);
                float localeDeadline=Time.realtimeSinceStartup+5;
                while(screen.Q<Button>("quitGame").text!=(locale=="ko"?"종료하기":"Quit game")
                    &&Time.realtimeSinceStartup<localeDeadline)yield return null;
                Check(screen.Q<Button>("quitGame").text==(locale=="ko"?"종료하기":"Quit game"),"Localized title-screen quit button: "+locale);
                Check(!screen.Query<Label>().ToList().Any(l=>l.text=="REINFORCEMENT LEARNING FOOTBALL"),"Old footer sentence removed: "+locale);
                yield return Capture("home-"+locale);
                var quit=screen.Q<Button>("quitGame");
                Check(Mathf.Approximately(quit.layout.xMax,915)&&quit.layout.width==220&&quit.resolvedStyle.borderRightWidth==0&&quit.resolvedStyle.color==C("#BFC3BC"),"Quit button is compact, borderless, gray and aligned: "+locale);
                ShowImage("guide",0);
                for(int page=0;page<5;page++)
                {
                    yield return Capture("guide-"+locale+"-"+(page+1));
                    Check(guidePage==page&&modal!=null,"Guide page available: "+locale+" "+(page+1));
                    yield return Press(Key.Enter);
                }
                Check(modal==null,"Enter closes fifth guide page: "+locale);
                ShowImage("guide",2);yield return Press(Key.Escape);Check(modal==null,"Escape closes guide: "+locale);
                ConfigureMatch(true,300,7,7);Kickoff();yield return new WaitForSecondsRealtime(.5f);
                var goals=arena.GetComponentsInChildren<Renderer>().Where(r=>r.CompareTag("redGoal")||r.CompareTag("navyGoal")).ToArray();
                Check(goals.Length>0&&goals.All(r=>r.sharedMaterials.All(m=>Mathf.Approximately(m.GetColor("_BaseColor").a,.6f))),"Goals retain 40 percent transparency: "+locale);
                foreach(var keeper in arena.GetComponentsInChildren<MNG_PlayerAvatar>().Where(a=>a.Role==MNG_PlayerRole.Keeper))
                {
                    var torso=keeper.GetComponentsInChildren<Renderer>().First(r=>r.name.StartsWith("AgentCube_"));
                    Check(torso.sharedMaterial.GetColor("_BaseColor")==C(keeper.Team==Team.Red?"#F05278":"#429CF2"),"Saturated keeper torso: "+locale+" "+keeper.Team);
                    var plates=keeper.KickPlate.GetComponentsInChildren<Renderer>();
                    Check(plates.Length>0&&plates.All(r=>r.sharedMaterial.GetColor("_BaseColor")==C("#E6FF00")),"Fluorescent yellow keeper kick plate: "+locale+" "+keeper.Team);
                }
                yield return Capture("match-"+locale);
                TogglePause();yield return null;
                var title=pause.Q<Label>("pauseTitle");
                Check(title.resolvedStyle.color==C("#EAF7FF")&&title.resolvedStyle.unityTextOutlineWidth==0,"Pale sky blue hydration title without outline: "+locale);
                var titlePanel=pause.Q("pauseTitlePanel");var expectedPanel=C("#101C38");expectedPanel.a=.95f;
                Check(titlePanel.resolvedStyle.backgroundColor==expectedPanel&&title.parent==titlePanel&&titlePanel.childCount==1&&titlePanel.layout.height==139,"Title-only navy panel at 5 percent transparency: "+locale);
                yield return Capture("pause-"+locale);TogglePause();
                match.GoalTouched(locale=="ko"?Team.Red:Team.Navy);yield return null;
                Check(Mathf.Approximately(goal.resolvedStyle.backgroundColor.a,.92f),"GOAL banner uses 8 percent transparency: "+locale);
                yield return Capture("goal-"+locale);
            }
            Home();yield return Capture("quit-ready-en");
        }
        IEnumerator SmokeSteps()
        {
            Check(!English&&Page=="home","Korean default and splash-to-home");
            yield return Capture("01-home-ko");
            yield return Click(L("start"));Check(Page=="setup","Pointer click opens match setup");Home();yield return null;
            LanguagePopup();Check(modalKind=="language","In-place language menu");CloseModal();
            ShowImage("guide",0);yield return Press(Key.Enter);Check(guidePage==1,"Enter advances guide");yield return Press(Key.Escape);Check(modal==null,"Escape closes guide");ShowImage("guide",4);NextGuide();Check(modal==null,"Guide ends");
            ShowImage("credits",0);yield return Capture("02-credits-ko");CloseModal();
            Setup();yield return Capture("03-setup-ko");PickModel(0,0);yield return Capture("04-models-ko");PickModel(0,1);yield return Capture("05-rules-ko");CloseModal();
            SetLanguage("en");yield return null;Home();yield return Capture("06-home-en");ShowImage("guide",1);yield return Capture("07-guide-en");CloseModal();Setup();PickModel(1,1);yield return Capture("08-rules-en");CloseModal();
            // Every imported checkpoint executes real inference on both teams.
            for(int id=0;id<10;id++)
            {
                ConfigureMatch(true,300,id,id);Kickoff();yield return new WaitForSecondsRealtime(2);
                Check(match.EpisodeElapsedSeconds>2,"Model "+id+" advances actual simulation");
                if(id<6)Check(arena.GetComponentsInChildren<MNG_ManagerAgent>().All(a=>a.PolicyDecisionCount>0),"Both neural agents decide: "+id);
                Check(!human.enabled&&!human.IsHuman,"Simulation excludes human ownership: "+id);
                Home();yield return null;
            }
            SetLanguage("ko");yield return null;
            ConfigureMatch(false,600,5,7);Kickoff();yield return new WaitForSecondsRealtime(2);
            Check(!human.IsHuman&&human.enabled,"Direct play starts under AI control");Check(Time.timeScale==1&&match.ConfiguredMatchDurationSeconds==600,"Direct play 1x and 10-minute option");
            Check(controls.Query<Label>().ToList().Count==1&&controls.Query<Label>().First().text.Contains(L("humanSwitch")),"AI controls show only H to human");
            var spectator=arena.GetComponentInChildren<MNG_SpectatorCamera>();
            var overview=spectator.transform.position;
            yield return Capture("15-ai-start-ko");
            yield return Press(Key.H);yield return new WaitForSecondsRealtime(.5f);
            Check(human.IsHuman&&spectator.IsFollowingHuman&&Vector3.Distance(overview,spectator.transform.position)>1,"H transfers ownership and camera to RED striker");
            Check(spectator.IsTransitioning,"Human camera travels through an intermediate pose");
            yield return new WaitForSecondsRealtime(.5f);Check(!spectator.IsTransitioning,"Human camera transition completes");
            Check(controls.Query<Label>().ToList().Count==9,"Human mode reveals movement pass shoot and H to AI");
            var humanStart=human.transform.position;
            InputSystem.QueueStateEvent(smokeKeyboard,new KeyboardState(Key.W));yield return new WaitForSecondsRealtime(.4f);
            InputSystem.QueueStateEvent(smokeKeyboard,new KeyboardState());yield return null;
            Check(Vector3.Distance(humanStart,human.transform.position)>.1f,"W input physically moves RED striker");
            yield return Press(Key.H);Check(!human.IsHuman&&!spectator.IsFollowingHuman&&spectator.IsTransitioning&&Vector3.Distance(overview,spectator.transform.position)>1,"AI camera return travels without snapping");
            yield return new WaitForSecondsRealtime(1);Check(!spectator.IsTransitioning&&Vector3.Distance(overview,spectator.transform.position)<.01f,"Camera returns exactly to AI overview");
            yield return Press(Key.H);Check(human.IsHuman,"H restores human ownership");
            // Controlled contact fixture: keep the human executor active so it cannot reset the plate.
            Home();yield return null;ConfigureMatch(false,600,7,7);Kickoff();yield return Press(Key.H);
            foreach(var a in arena.GetComponentsInChildren<MNG_ManagerAgent>())a.enabled=false;
            foreach(var r in arena.GetComponents<ExhibitionRules>())r.enabled=false;
            foreach(var a in arena.GetComponentsInChildren<MNG_PlayerAvatar>())
            {
                a.GetComponent<MNG_PlayerSkillExecutor>().Cancel();
                if(a.gameObject==human.gameObject)continue;
                a.GetComponent<MNG_PlayerSkillExecutor>().enabled=false;
                a.Body.position=new Vector3(20+(int)a.Team*10,a.Body.position.y,-20+a.Slot*5);
                a.Body.linearVelocity=Vector3.zero;
            }
            var avatar=human.GetComponent<MNG_PlayerAvatar>();
            var ballControl=arena.GetComponentInChildren<MNG_BallControl>();var ballBody=ballControl.GetComponent<Rigidbody>();
            var actualStrikes=new List<MNG_PlateStrikeEvent>();
            ballControl.PlateStrikeApplied+=actualStrikes.Add;
            foreach(var key in new[]{Key.E,Key.Space})
            {
                ballControl.ResetLedger();avatar.ResetKickCooldown();
                avatar.transform.SetPositionAndRotation(new Vector3(0,.52f,0),Quaternion.identity);
                avatar.Body.position=new Vector3(0,avatar.Body.position.y,0);avatar.Body.rotation=Quaternion.identity;
                avatar.Body.linearVelocity=Vector3.zero;avatar.Body.angularVelocity=Vector3.zero;
                Physics.SyncTransforms();
                var anchor=avatar.KickPlate.DribblePosition;anchor.y=.581541f;
                ballBody.isKinematic=false;ballBody.position=anchor;ballBody.linearVelocity=Vector3.zero;ballBody.angularVelocity=Vector3.zero;
                Physics.SyncTransforms();
                int count=actualStrikes.Count,shots=humanShots[0];
                Check(match.IsPlayActive&&human.enabled&&human.IsHuman,"Physical input fixture is in active human play: "+key);
                var trace=new List<string>();
                trace.Add($"before key={key} player={avatar.transform.position} body={avatar.Body.position} forward={avatar.transform.forward} ball={ballBody.position} anchor={anchor} canKick={avatar.KickPlate.CanKick}");
                yield return Press(key);
                for(int sample=0;sample<15;sample++)
                {
                    trace.Add($"sample={sample} state={match.State} human={human.IsHuman} plate={avatar.KickPlate.transform.localPosition} pending={avatar.KickPlate.HasPendingKick} ball={ballBody.position} velocity={ballBody.linearVelocity} contacts={ballControl.GetPhysicalContactCount(avatar.Team,avatar.Slot)} strikes={actualStrikes.Count} cooldown={avatar.KickCooldownSeconds}");
                    yield return new WaitForFixedUpdate();
                }
                File.WriteAllLines(Path.Combine(smokeDirectory,"physical-"+key+".txt"),trace);
                Check(actualStrikes.Count==count+1,"Actual human plate-ball collision from "+key);
                Check(actualStrikes.Last().ExitSpeed==(key==Key.E?MNG_KickSolver.PassExitSpeedForDistance(30):MNG_KickSolver.StrongExitSpeed),"Human physical pass/shot speed matches AI for same distance: "+key);
                Check(humanShots[0]==shots+(key==Key.Space?1:0),"Human shot accounting excludes pass: "+key);
                Check(avatar.KickCooldownSeconds>0,"Human contact starts cooldown: "+key);
                yield return Press(key);yield return new WaitForSecondsRealtime(.1f);
                Check(actualStrikes.Count==count+1&&!avatar.KickPlate.IsStrikeActive,"Cooldown prevents immediate repeat: "+key);
                yield return new WaitForSecondsRealtime(1);
            }
            ballControl.ResetLedger();avatar.ResetKickCooldown();ballBody.position=new Vector3(0,.52f,20);
            int noContactCount=actualStrikes.Count;
            yield return Press(Key.Space);yield return new WaitForSecondsRealtime(.2f);
            Check(actualStrikes.Count==noContactCount,"No remote strike without physical contact");
            var polish=PolishChecks();while(polish.MoveNext())yield return polish.Current;
            Home();yield return null;ConfigureMatch(false,600,5,7);Kickoff();yield return Press(Key.H);yield return new WaitForSecondsRealtime(1);
            yield return Capture("09-match-ko");yield return Press(Key.Escape);float before=match.MatchRemainingSeconds;var position=match.Snapshot.BallPosition;
            yield return new WaitForSecondsRealtime(1);
            Check(Paused&&Time.timeScale==0&&!human.enabled&&match.MatchRemainingSeconds==before&&match.Snapshot.BallPosition==position,"Pause freezes clock, physics and human input");
            yield return Capture("10-pause-ko");yield return Press(Key.Escape);yield return new WaitForSecondsRealtime(.5f);Check(match.MatchRemainingSeconds<before,"Escape resumes same match");
            match.GoalTouched(Team.Red);yield return Capture("11-red-goal");
            Check(Mathf.Approximately(goal.layout.center.y,270)&&Mathf.Approximately(goal.resolvedStyle.backgroundColor.a,.92f),"GOAL center at 75 percent height from bottom with 8 percent transparency");
            TogglePause();Home();Check(Page=="home"&&Time.timeScale==1,"End paused match returns home");
            SetLanguage("en");yield return null;ConfigureMatch(false,300,5,7);Kickoff();yield return new WaitForSecondsRealtime(.5f);
            Check(controls.Query<Label>().First().text=="H: Switch to human control","English AI switch instruction");yield return Capture("16-ai-start-en");
            yield return Press(Key.H);yield return new WaitForSecondsRealtime(.5f);Check(controls.Query<Label>().ToList().Any(l=>l.text=="H: Switch to AI"),"English human switch instruction");yield return Capture("17-human-en");
            Home();yield return null;ConfigureMatch(true,300,5,8);Kickoff();yield return new WaitForSecondsRealtime(2);
            var speed=screen.Q<VisualElement>("speedBadge");yield return null;
            Check(controls==null&&Mathf.Approximately(speed.layout.center.x,960),"Simulation badge centered and no inactive human controls");
            Check(speed.resolvedStyle.backgroundColor==C("#FFE6E9"),"Speed badge uses pale red background");
            Check(arena.GetComponentsInChildren<MNG_PlayerAvatar>().All(a=>a.Body.interpolation==RigidbodyInterpolation.Interpolate),"Exhibition simulation also keeps render interpolation");
            Check(Time.timeScale==2,"AI simulation runs at 2x");yield return Capture("12-match-en");TogglePause();yield return Capture("13-pause-en");TogglePause();
            // Complete a real five-minute match at its authorized 2x speed.
            float deadline=Time.realtimeSinceStartup+170;
            while(Page=="match"&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(Page=="result"&&match.State==MNG_MatchState.Finished,"Full real match reaches final scoreboard");
            Check(aiShots.Sum()>0&&humanShots.Sum()==0,"Actual AI strikes populate simulation statistics without human shots");
            Check(screen.Q("stat-saves").Query<Label>().First().text==keeperTouches.GetTouches(Team.Red).ToString(),"Finished scoreboard displays observed keeper touches");
            yield return Capture("14-result-en");yield return new WaitForSecondsRealtime(1);
            Check(Page=="result","Scoreboard remains until Exit");Home();Check(Page=="home","Result Exit returns home");
            SetLanguage("ko");yield return null;
        }
    }
}
