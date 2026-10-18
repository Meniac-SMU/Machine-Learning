using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed partial class FiveDemoApp
    {
        [Serializable] class Fixture { public int step; public float[] observation,mask,probabilities; }
        [Serializable] class Fixtures { public Fixture[] cases; }
        [Serializable] class Check { public string name; public bool passed; public string detail; }
        [Serializable] class SmokeResult
        {
            public bool passed; public string runtime=FiveMatch.PlayerRuntimeVersion;
            public List<Check> checks=new(); public int redScore,navyScore;
            public float elapsed; public FiveTeamStatistics[] stats;
            public bool trainingStarted=false; public string error;
        }
        SmokeResult smokeResult;
        string smokeDirectory;
        void CheckDemo(bool passed,string name,string detail="")
        {
            smokeResult.checks.Add(new Check{name=name,passed=passed,detail=detail});
            if(!passed)throw new InvalidOperationException(name+": "+detail);
        }
        IEnumerator Smoke()
        {
            var args=Environment.GetCommandLineArgs();int index=Array.IndexOf(args,"--five-demo-evidence");
            smokeDirectory=index>=0&&index+1<args.Length?args[index+1]:Path.Combine(Application.persistentDataPath,"five-demo-smoke");
            Directory.CreateDirectory(smokeDirectory);smokeResult=new SmokeResult();
            Application.logMessageReceived+=SmokeLog;
            var body=Array.IndexOf(args,"--laptop-smoke")>=0?LaptopSmokeBody():Array.IndexOf(args,"--saiccer-smoke")>=0?SaiccerSmokeBody():Array.IndexOf(args,"--five-demo-pitch")>=0?PitchSmokeBody():Array.IndexOf(args,"--five-demo-robots")>=0?RobotSmokeBody():Array.IndexOf(args,"--five-demo-motion")>=0?MotionSmokeBody():Array.IndexOf(args,"--five-demo-hud-smoke")>=0?HudSmokeBody():Array.IndexOf(args,"--five-demo-live")>=0?LiveBody():SmokeBody();
            while(true)
            {
                object current=null;bool next=false;
                try{next=body.MoveNext();if(next)current=body.Current;}
                catch(Exception e){smokeResult.error=e.ToString();}
                if(smokeResult.error!=null||!next)break;
                yield return current;
            }
            Application.logMessageReceived-=SmokeLog;
            smokeResult.passed=smokeResult.error==null&&smokeResult.checks.Count>0&&smokeResult.checks.All(c=>c.passed);
            File.WriteAllText(Path.Combine(smokeDirectory,"result.json"),JsonUtility.ToJson(smokeResult,true));
            Debug.Log("FIVE_DEMO_SMOKE "+(smokeResult.passed?"PASS":"FAIL")+" "+smokeResult.checks.Count);
            Home();yield return null;Application.Quit(smokeResult.passed?0:1);
        }
        void SmokeLog(string message,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)smokeResult.error=message+"\n"+stack;}
        IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(.12f);yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            bool cursorExpected=Page=="home"||Page=="setup"||Page=="results"||(Page=="match"&&Paused);
            CheckDemo(CursorShown==cursorExpected&&!UnityEngine.Cursor.visible,"cursor-visibility-"+name,Page+" paused="+Paused+" visible="+CursorShown);
            File.WriteAllBytes(Path.Combine(smokeDirectory,name+".png"),texture.EncodeToPNG());
            if(Page=="match"&&!Paused&&matchScore!=null&&name.Contains("-1920"))
            {
                var bounds=matchScore.worldBound;int ink=0;
                for(int x=Mathf.CeilToInt(bounds.xMin+10);x<bounds.xMax-10;x++)
                for(int y=Mathf.CeilToInt(bounds.yMin+5);y<bounds.yMax-5;y++)
                {
                    var pixel=texture.GetPixel(x,texture.height-1-y);
                    if(pixel.r<.25f&&pixel.g<.3f&&pixel.b<.25f)ink++;
                }
                CheckDemo(ink>10,"score-text-rendered-pixels-"+name,"darkPixels="+ink+"; text="+matchScore.text);
            }
            Destroy(texture);
            yield return null;
        }
        void Submit(Button button)
        {
            CheckDemo(button!=null,"button-present");button.Focus();
            using var submit=NavigationSubmitEvent.GetPooled();submit.target=button;button.SendEvent(submit);
        }
        void CheckGuideLabels(string prefix,int page)
        {
            var labels=modal.Query<Label>().ToList();
            foreach(var label in labels)
            {
                var measured=label.MeasureTextSize(label.text,label.contentRect.width,VisualElement.MeasureMode.Exactly,0,VisualElement.MeasureMode.Undefined);
                CheckDemo(measured.y<=label.contentRect.height+1,"guide-wrapped-text-fits-"+prefix+"-"+page,label.text+" "+measured+" / "+label.contentRect.size);
            }
            if(page==2||page==3)
            {
                var names=English?FiveHud.EnglishCommandNames:FiveHud.CommandNames;
                for(int i=(page-2)*4;i<(page-1)*4;i++)
                    CheckDemo(labels.Any(l=>l.text==names[i]),"guide-hud-name-agreement-"+prefix+"-"+i,names[i]);
            }
        }
        IEnumerator SmokeBody()
        {
            CheckDemo(RedModel==FiveDemoAssets.LatestModelId&&NavyModel==FiveDemoAssets.LatestModelId,"both-default-to-latest-checkpoint");
            foreach(var locale in new[]{"ko","en"})
            {
                Home();SetLanguage(locale);yield return Capture(locale+"-home");
                for(int page=0;page<5;page++)
                {
                    ShowGuide(page);yield return Capture(locale+"-guide-"+(page+1));
                    var labels=modal.Query<Label>().ToList();
                    CheckDemo(!labels.Any(l=>l.text.Contains("4 v 4")||l.text.Contains("4vs4")||l.text.Contains("MS3")),locale+"-guide-current-"+page);
                    foreach(var label in labels)
                        CheckDemo(label.layout.xMax<=label.parent.layout.width+2,locale+"-guide-horizontal-fit",label.text);
                    CheckGuideLabels(locale,page);
                }
                CloseModal();Setup();yield return Capture(locale+"-setup");
                for(int team=0;team<2;team++)
                {
                    PickModel(team);yield return Capture(locale+(team==0?"-models":"-models-navy"));
                    var modelButtons=modal.Query<Button>().ToList().Where(b=>b.name.StartsWith("model-")).ToList();
                    CheckDemo(modelButtons.Count==FiveDemoAssets.Steps.Length+1,locale+"-single-picker-all-models-"+team);
                    foreach(var button in modelButtons)
                    {
                        CheckDemo(button.layout.xMin>=0&&button.layout.yMin>=0&&button.layout.xMax<=button.parent.layout.width&&button.layout.yMax<=button.parent.layout.height,
                            locale+"-model-button-fits-"+team+"-"+button.name);
                        if(English)
                        {
                            string[] names={"Rule manager","0 steps","400K steps","800K steps","1.2M steps","1.6M steps","2M steps","2.4M steps","2.8M steps","3.2M steps","3.6M steps"};
                            CheckDemo(button.Q<Label>().text==names[int.Parse(button.name.Substring(6))],"english-formal-model-name-"+team+"-"+button.name,button.Q<Label>().text);
                        }
                    }
                    Submit(modal.Q<Button>("model-"+(FiveDemoAssets.LatestModelId-1)));yield return null;
                    CheckDemo((team==0?RedModel:NavyModel)==FiveDemoAssets.LatestModelId-1&&Page=="setup",locale+"-picker-selects-3200000-"+team);
                    PickModel(team);yield return null;
                    Submit(modal.Q<Button>("model-"+FiveDemoAssets.LatestModelId));yield return null;
                    CheckDemo((team==0?RedModel:NavyModel)==FiveDemoAssets.LatestModelId&&Page=="setup",locale+"-picker-click-selects-latest-"+team);
                }
            }
            var fixtures=JsonUtility.FromJson<Fixtures>(assets.inferenceFixtures.text);
            for(int model=0;model<FiveDemoAssets.Steps.Length;model++)
            {
                using(var policy=new FiveDemoPolicy(assets.models[model],1710))
                foreach(var fixture in fixtures.cases.Where(f=>f.step==FiveDemoAssets.Steps[model]))
                {
                    var actual=policy.Probabilities(fixture.observation,fixture.mask);
                    float error=actual.Zip(fixture.probabilities,(a,b)=>Mathf.Abs(a-b)).Max();
                    CheckDemo(error<.0001f,"inference-parity-"+fixture.step,"max absolute error="+error);
                }
                ConfigureMatch(false,300,model+1,((model+1)%FiveDemoAssets.Steps.Length)+1);Kickoff();yield return null;
                CheckDemo(Page=="match"&&match.players.Length==10,"model-start-"+FiveDemoAssets.Steps[model]);
                session.automaticSteps=false;human.enabled=false;
                for(int tick=0;tick<175;tick++){session.Step(.02f);if(tick%25==0)yield return null;}
                CheckDemo(session.decisions[0]>=2&&session.decisions[1]>=2,"model-decides-"+FiveDemoAssets.Steps[model]);
                Home();yield return null;
            }
            SetLanguage("ko");ConfigureMatch(false,300,FiveDemoAssets.LatestModelId,FiveDemoAssets.LatestModelId-1);Kickoff();yield return null;
            CheckDemo(hud.rootVisualElement.Q<Button>("pause")==null&&hud.rootVisualElement.Q<Button>("restart")==null,"removed-match-buttons");
            CheckDemo(arena.GetComponentInChildren<FiveHud>().BarCount==8,"eight-field-stamina-bars");
            yield return Capture("ko-match-broadcast");
            foreach(var view in new[]{FiveDemoCamera.View.RedKeeper,FiveDemoCamera.View.NavyKeeper})
            {
                cameras.Select(view,false);yield return new WaitForSecondsRealtime(.4f);yield return Capture("camera-"+view);
                CheckDemo(cameras.CurrentView==view,"camera-"+view);
                CheckDemo(Mathf.Abs(match.overview.transform.position.x)<match.geometry.HalfLength-.1f,"keeper-camera-in-front-of-goal-net-"+view);
                var keeper=match.players[view==FiveDemoCamera.View.RedKeeper?0:5];
                var framed=match.overview.WorldToViewportPoint(keeper.transform.position);
                CheckDemo(framed.z>0&&framed.y>.2f&&framed.y<.75f,"keeper-visible-above-hud-"+view,framed.ToString());
            }
            human.ToggleOwner();yield return null;
            CheckDemo(match.HumanPlayerIndex==3&&cameras.CurrentView==FiveDemoCamera.View.RedStriker,"human-red-attacker-one-and-camera");
            yield return new WaitForSecondsRealtime(1.1f);
            CheckDemo(Vector3.Distance(match.overview.transform.position,match.players[3].transform.position)<14,"human-camera-follows-attacker");
            yield return Capture("camera-human");
            HandleEscape();yield return null;
            var elapsed=match.Elapsed;var position=match.players[3].Position;
            yield return new WaitForSecondsRealtime(.2f);
            CheckDemo(Paused&&Time.timeScale==0&&match.Elapsed==elapsed&&match.players[3].Position==position,"escape-pauses-clock-and-player");
            yield return Capture("ko-pause");HandleEscape();yield return null;
            CheckDemo(!Paused&&Time.timeScale==1&&human.enabled,"escape-resumes");
            human.ToggleOwner();yield return null;CheckDemo(match.HumanPlayerIndex<0,"human-releases-to-ai");
            Home();yield return null;
            // A complete game through the same Session.Step and real physics used in the normal Player.
            ConfigureMatch(true,300,FiveDemoAssets.LatestModelId,FiveDemoAssets.LatestModelId-1);Kickoff();yield return null;session.automaticSteps=false;
            int count=0;
            while(match.State!=FiveMatchState.Finished)
            {
                session.Step(.02f);if(++count%25==0)yield return null;
                if(count>30000)throw new InvalidOperationException("Full match did not terminate.");
            }
            yield return null;
            CheckDemo(Page=="results"&&match.Elapsed>=300,"natural-300-second-game-to-results");
            CheckDemo(screen.Query<FiveComparisonBar>().ToList().Count==5,"five-shared-result-bars");
            CheckDemo(!screen.Query<Label>().ToList().Any(l=>l.text.Contains("RED + NAVY")||l.text.Contains("stable receptions / passes")),"result-explanation-removed");
            CheckDemo(statistics.teams.Sum(t=>t.shots)==match.Shots&&statistics.teams.Sum(t=>t.passes)==match.PassAttempts
                &&statistics.teams.Sum(t=>t.successfulPasses)==match.PassReceptions,"statistics-match-event-totals");
            foreach(var row in screen.Query<FiveComparisonBar>().ToList())CheckDemo(row.RedShare>=0&&row.RedShare<=1,"bar-share-bounded",row.name);
            smokeResult.redScore=match.RedScore;smokeResult.navyScore=match.NavyScore;smokeResult.elapsed=match.Elapsed;smokeResult.stats=statistics.teams;
            yield return Capture("ko-results");English=true;Results();yield return Capture("en-results");
            Home();yield return null;CheckDemo(Page=="home"&&arena==null&&Time.timeScale==1,"return-home-cleans-runtime");
            ConfigureMatch(true,600,0,0);Kickoff();yield return null;
            CheckDemo(match.profile.duration==600&&match.ruleManagers[0]&&match.ruleManagers[1]&&!human.AllowHuman,"ten-minute-rule-spectator");
            yield return new WaitForSecondsRealtime(.1f);HandleEscape();Home();yield return null;
            CheckDemo(Time.timeScale==1&&Page=="home","home-from-pause");
        }
        IEnumerator LiveBody()
        {
            ConfigureMatch(false,300,FiveDemoAssets.LatestModelId,FiveDemoAssets.LatestModelId-1);Kickoff();yield return null;
            CheckDemo(Page=="match"&&session.automaticSteps&&Time.timeScale==1,"normal-player-1x-start");
            var start=Time.realtimeSinceStartup;
            yield return new WaitForSecondsRealtime(2);
            human.ToggleOwner();yield return new WaitForSecondsRealtime(1.1f);
            CheckDemo(cameras.CurrentView==FiveDemoCamera.View.RedStriker,"live-human-camera");
            yield return Capture("live-human-720p");
            human.ToggleOwner();yield return new WaitForSecondsRealtime(.9f);
            foreach(var view in new[]{FiveDemoCamera.View.RedKeeper,FiveDemoCamera.View.NavyKeeper,FiveDemoCamera.View.Broadcast})
            {cameras.Select(view,false);yield return new WaitForSecondsRealtime(.2f);yield return Capture("live-"+view);}
            var checkpoint=30f;
            while(Page=="match")
            {
                CheckDemo(Time.timeScale==1&&!Paused,"live-1x");
                if(match.Elapsed>=checkpoint)
                {
                    File.WriteAllText(Path.Combine(smokeDirectory,"live.json"),JsonUtility.ToJson(new LiveReceipt{elapsed=match.Elapsed,realSeconds=Time.realtimeSinceStartup-start,red=match.RedScore,navy=match.NavyScore},true));
                    checkpoint+=30;
                }
                if(Time.realtimeSinceStartup-start>900)throw new InvalidOperationException("Real-time match timeout.");
                yield return new WaitForSecondsRealtime(1);
            }
            CheckDemo(Page=="results"&&match.Elapsed>=300,"live-natural-full-time");
            CheckDemo(Time.realtimeSinceStartup-start>=299,"live-wall-clock-at-least-300-seconds");
            smokeResult.redScore=match.RedScore;smokeResult.navyScore=match.NavyScore;smokeResult.elapsed=match.Elapsed;smokeResult.stats=statistics.teams;
            yield return Capture("live-results-720p");
            Home();yield return null;CheckDemo(Page=="home"&&Time.timeScale==1,"live-return-home");
        }
        [Serializable] class LiveReceipt { public float elapsed,realSeconds; public int red,navy; }
    }
}

