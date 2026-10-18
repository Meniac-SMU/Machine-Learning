using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed partial class FiveDemoApp
    {
        IEnumerator SaiccerSmokeBody()
        {
            yield return null;yield return new WaitForEndOfFrame();
            CheckDemo(splashCursorHidden,"team-logo-cursor-hidden");
            CheckDemo(coneCursor!=null&&coneCursor.width==128&&coneCursor.height==128&&coneCursor.mipmapCount>1,"cone-cursor-high-resolution");
            CheckDemo(Mathf.Approximately(conePointer.style.width.value.value,38f)&&Vector2.Distance(conePointer.layout.size,new Vector2(38f,38f))<.75f&&conePointer.pickingMode==PickingMode.Ignore,"cone-cursor-38-no-click-block",conePointer.layout.size.ToString());
            CheckDemo(coneHotspot.x>=0&&coneHotspot.x<38f&&coneHotspot.y>=0&&coneHotspot.y<38f,"cone-cursor-tip-hotspot",coneHotspot.ToString());
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(smokeDirectory,"cone-cursor.png"),coneCursor.EncodeToPNG());
            CheckDemo(Application.productName=="SAICCER"&&System.IO.Path.GetFileName(Application.dataPath)=="SAICCER_Data","saiccer-player-identity",Application.productName+" / "+Application.dataPath);
            CheckDemo(assets.redCharacters.Length==1&&assets.navyCharacters.Length==1,"one-current-character-set-per-team");
            var red=assets.redCharacters[0];var navy=assets.navyCharacters[0];
            CheckDemo(red.displayName=="RED CUBE"&&red.fieldPlayer.name=="RedRobot"&&red.goalkeeper.name=="PinkGoalkeeper","red-pink-character-set");
            CheckDemo(navy.displayName=="NAVY CUBE"&&navy.fieldPlayer.name=="NavyRobot"&&navy.goalkeeper.name=="SkyGoalkeeper","navy-sky-character-set");
            foreach(int width in new[]{1280,1920})
            foreach(string locale in new[]{"ko","en"})
            {
                Screen.SetResolution(width,width*9/16,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.4f);
                Mouse.current?.WarpCursorPosition(new Vector2(width*.5f,Screen.height*.85f));
                Home();SetLanguage(locale);yield return Capture("saiccer-"+locale+"-"+width+"-home");
                var logo=screen.Q<Label>("game-logo");
                CheckDemo(logo.text=="S<color=#B9F54E>AI</color>CCER"&&logo.enableRichText,"saiccer-only-ai-lime-"+locale+width);
                CheckDemo(logo.resolvedStyle.fontSize==185,"larger-home-logo-"+locale+width);
                CheckDemo(screen.Q<Label>("team-name").text=="MENIAC","meniac-home-team-credit-"+locale+width);
                var measured=logo.MeasureTextSize(logo.text,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined);
                CheckDemo(measured.x<=logo.contentRect.width&&measured.y<=logo.contentRect.height,"saiccer-logo-fits-"+locale+width,measured.ToString());
                Setup();yield return Capture("saiccer-"+locale+"-"+width+"-setup");
                var versus=screen.Q<Label>("setup-versus");var redCard=screen.Q("team-card-0");var navyCard=screen.Q("team-card-1");
                CheckDemo(Vector2.Distance(versus.layout.center,(redCard.layout.center+navyCard.layout.center)*.5f)<.1f
                    &&!screen.Query<Label>().ToList().Any(l=>l.text=="5 v 5"),"centered-vs-without-5v5-"+locale+width);
                for(int team=0;team<2;team++)
                {
                    var card=screen.Q("team-card-"+team);var title=screen.Q<Label>("team-title-"+team);
                    CheckDemo(card.layout.height==700&&title.resolvedStyle.fontSize==62.5f,"taller-card-half-team-title-"+locale+width+team);
                    CheckDemo(!card.Query<Label>().ToList().Any(l=>l.text=="01"||l.text=="02"),"team-numbers-removed-"+team);
                    var set=team==0?red:navy;var preview=screen.Q<Image>("character-preview-"+team);
                    var pick=screen.Q(team==0?"red-model":"navy-model");
                    CheckDemo(screen.Q<Label>("team-model-title-"+team).resolvedStyle.fontSize==26&&screen.Q<Label>("team-model-name-"+team).resolvedStyle.fontSize==49,"larger-neural-labels-"+locale+width+team);
                    CheckDemo(preview.layout.size==new Vector2(310,294)&&preview.layout.y==60&&pick.layout.height==150&&Mathf.Abs(pick.layout.y-485.5f)<=.5f,"preview-size-preserved-model-button-150-"+locale+width+team);
                    CheckDemo(preview.image==characterPreviews[set.fieldPlayer]&&screen.Q<Label>("character-name-"+team).text==set.displayName,"field-player-preview-and-name-"+team);
                    var pixels=((Texture2D)preview.image).GetPixels32();int opaque=pixels.Count(p=>p.a>200);
                    CheckDemo(opaque>pixels.Length/10&&opaque<pixels.Length*9/10,"preview-has-model-and-margin-"+team,"opaque="+opaque);
                    CheckDemo(!screen.Q<Button>("character-"+team+"-previous").enabledSelf&&!screen.Q<Button>("character-"+team+"-next").enabledSelf,"single-set-arrows-disabled-"+team);
                    foreach(var label in card.Query<Label>().ToList())
                    {
                        var size=label.MeasureTextSize(label.text,label.contentRect.width,VisualElement.MeasureMode.Exactly,0,VisualElement.MeasureMode.Undefined);
                        CheckDemo(size.y<=label.contentRect.height+1,"setup-text-fits-"+locale+width+team,label.text+" "+size+" / "+label.contentRect.size);
                    }
                }
                CheckDemo(screen.Q("human-mode").layout.y==960&&screen.Q("duration-five").layout.y==960&&screen.Q("kickoff").layout.y==948,"setup-buttons-lowered-"+locale+width);
                foreach(bool spectator in new[]{true,false})
                {
                    Simulation=spectator;Setup();yield return Capture("team-label-"+locale+width+"-"+(spectator?"spectator":"play"));
                    CheckDemo((screen.Q("your-team")!=null)==!spectator,"your-team-only-in-play-"+locale+width+spectator);
                    if(!spectator)
                    {
                        var label=screen.Q<Label>("your-team");var pick=screen.Q("red-model");
                        CheckDemo(label.resolvedStyle.fontSize==30&&label.resolvedStyle.unityTextAlign==TextAnchor.MiddleRight
                            &&Mathf.Abs(label.layout.xMax-pick.layout.xMax)<.1f,"your-team-larger-and-right-aligned-"+locale+width);
                    }
                    var selected=screen.Q(spectator?"spectator-mode":"human-mode");
                    CheckDemo(selected.resolvedStyle.backgroundColor==C("#4F7D35")&&screen.Q("duration-five").resolvedStyle.backgroundColor==C("#4F7D35"),"brighter-selected-mode-time-"+locale+width+spectator);
                }
            }
            var groundChecks=GroundSmokeBody();while(groundChecks.MoveNext())yield return groundChecks.Current;
            var benchChecks=BenchSmokeBody();while(benchChecks.MoveNext())yield return benchChecks.Current;
            var goalChecks=GoalContinuationSmokeBody();while(goalChecks.MoveNext())yield return goalChecks.Current;
            ConfigureMatch(false,300,0,0);Kickoff();yield return null;
            session.automaticSteps=false;human.enabled=false;
            var keyboard=InputSystem.AddDevice<Keyboard>();
            try
            {
                void Keys(params Key[] keys)
                {
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();
                    InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();
                    human.ReadInput(keyboard,null);
                }
                foreach(var key in new[]{Key.W,Key.UpArrow,Key.S,Key.DownArrow})
                {
                    RobotReset();match.SetHumanControl(true);RobotPlace(3,Vector2.zero,Vector2.right);
                    Keys(key);RobotStep(25);
                    bool forward=key==Key.W||key==Key.UpArrow;
                    CheckDemo(forward?match.players[3].Position.x>.3f:match.players[3].Position.x<-.3f,"keyboard-drive-"+key,match.players[3].Position.ToString());
                }
                foreach(var key in new[]{Key.A,Key.LeftArrow,Key.D,Key.RightArrow})
                {
                    RobotReset();match.SetHumanControl(true);RobotPlace(3,Vector2.zero,Vector2.up);
                    Keys(key);RobotStep(15);bool right=key==Key.D||key==Key.RightArrow;
                    CheckDemo(right?match.players[3].Forward.x>.1f:match.players[3].Forward.x<-.1f,"keyboard-turn-"+key,match.players[3].Forward.ToString());
                }
                foreach(var key in new[]{Key.E,Key.Digit1,Key.Numpad1,Key.Space,Key.Digit0,Key.Numpad0})
                {
                    RobotReset();match.SetHumanControl(true);RobotPlace(3,Vector2.zero,Vector2.right);RobotGive(3);
                    Keys(key);RobotStep(1);bool pass=key==Key.E||key==Key.Digit1||key==Key.Numpad1;
                    CheckDemo(pass?match.PassAttempts==1&&match.Shots==0:match.Shots==1&&match.PassAttempts==0,"keyboard-kick-"+key,"passes="+match.PassAttempts+" shots="+match.Shots);
                }
                RobotReset();match.SetHumanControl(true);RobotPlace(3,Vector2.zero,Vector2.right);
                var throttle=typeof(FiveMatch).GetField("humanThrottle",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                Keys(Key.W,Key.UpArrow);float both=(float)throttle.GetValue(match);
                Keys(Key.W);float single=(float)throttle.GetValue(match);
                CheckDemo(both==1&&single==1,"duplicate-keys-do-not-double-speed","both="+both+" single="+single);
                RobotReset();match.SetHumanControl(true);RobotPlace(3,Vector2.zero,Vector2.right);
                Keys(Key.W,Key.DownArrow);
                CheckDemo((float)throttle.GetValue(match)==0,"opposing-alias-keys-cancel");
                Keys();
            }
            finally{InputSystem.RemoveDevice(keyboard);}
            Home();yield return null;
            ConfigureMatch(false,300,FiveDemoAssets.LatestModelId,FiveDemoAssets.LatestModelId);Kickoff();yield return null;
            session.automaticSteps=false;human.ToggleOwner();yield return new WaitForSecondsRealtime(1);
            cameras.UpdateMouseLook(10000,.016f);CheckDemo(cameras.MouseYaw==40,"mouse-right-clamp-40");yield return Capture("saiccer-camera-right40");
            cameras.UpdateMouseLook(-10000,.016f);CheckDemo(cameras.MouseYaw==-40,"mouse-left-clamp-40");yield return Capture("saiccer-camera-left40");
            for(int i=0;i<300;i++)cameras.UpdateMouseLook(0,.016f);
            CheckDemo(Mathf.Abs(cameras.MouseYaw)<.1f,"mouse-recenters-after-idle");
            Home();yield return null;ConfigureMatch(false,300,FiveDemoAssets.LatestModelId,FiveDemoAssets.LatestModelId);
            var regression=PitchSmokeBody();while(regression.MoveNext())yield return regression.Current;
        }
    }
}
