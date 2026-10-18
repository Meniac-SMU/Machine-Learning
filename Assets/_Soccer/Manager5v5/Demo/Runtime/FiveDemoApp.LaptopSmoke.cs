using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed partial class FiveDemoApp
    {
        IEnumerator LaptopSmokeBody()
        {
            foreach(int width in new[]{1280,1600})foreach(string locale in new[]{"en","ko"})
            {
                int height=width*10/16;string prefix="laptop-"+locale+"-"+width;
                Home();Screen.SetResolution(width,height,FullScreenMode.Windowed);yield return new WaitForSecondsRealtime(.5f);
                SetLanguage(locale);yield return null;
                CheckDemo(Screen.width==width&&Screen.height==height,"native-16-10-"+prefix,Screen.width+"x"+Screen.height);
                CheckLaptopBounds(prefix+"-home");
                var art=screen.Q("home-art").worldBound;
                CheckDemo(Mathf.Abs(stage.worldBound.height-viewport.worldBound.height)<1&&Mathf.Abs(art.yMin)<1
                    &&Mathf.Abs(art.yMax-viewport.worldBound.yMax)<1,"home-fills-16-10-no-letterbox-"+prefix,art.ToString());
                var globe=screen.Q<Image>("language-globe");
                CheckDemo(globe!=null&&globe.image==assets.languageIcon&&assets.languageIcon.width==512&&assets.languageIcon.height==512
                    &&assets.languageIcon.mipmapCount>1&&assets.languageIcon.filterMode==FilterMode.Trilinear&&globe.layout.width==28,
                    "full-resolution-globe-"+prefix,assets.languageIcon.graphicsFormat.ToString());
                CheckDemo(conePointer.layout.width==38&&conePointer.layout.height==38,"cursor-38-"+prefix);
                yield return Capture(prefix+"-home");LanguagePopup();yield return null;CheckLaptopBounds(prefix+"-language");yield return Capture(prefix+"-language");CloseModal();
                ConfigureMatch(false,300,FiveDemoAssets.LatestModelId,FiveDemoAssets.LatestModelId);Setup();yield return new WaitForSecondsRealtime(.2f);
                CheckLaptopBounds(prefix+"-setup");yield return Capture(prefix+"-setup");
                PickModel(0);yield return null;CheckLaptopBounds(prefix+"-models");CheckFullOverlay(modal,prefix+"-models");yield return Capture(prefix+"-models");CloseModal();
                Kickoff();yield return new WaitForSecondsRealtime(.3f);session.automaticSteps=false;
                cameras.Select(FiveDemoCamera.View.Broadcast,false);yield return null;
                CheckLaptopBounds(prefix+"-broadcast");CheckHudLayout(prefix+"-broadcast");
                foreach(float x in new[]{-match.geometry.HalfLength,match.geometry.HalfLength})
                foreach(float z in new[]{-match.geometry.HalfWidth,match.geometry.HalfWidth})
                {
                    var v=match.overview.WorldToViewportPoint(new Vector3(x,0,z));
                    CheckDemo(v.z>0&&v.x>0&&v.x<1&&v.y>0&&v.y<1,"whole-pitch-visible-"+prefix,v.ToString());
                }
                yield return Capture(prefix+"-broadcast");
                foreach(var view in new[]{FiveDemoCamera.View.RedKeeper,FiveDemoCamera.View.NavyKeeper})
                {
                    cameras.Select(view,false);yield return new WaitForSecondsRealtime(.2f);
                    CheckLaptopBounds(prefix+view);CheckHudLayout(prefix+view);yield return Capture(prefix+"-"+view);
                }
                human.ToggleOwner();yield return new WaitForSecondsRealtime(1);
                CheckLaptopBounds(prefix+"-human");CheckHudLayout(prefix+"-human");
                var bar=hud.rootVisualElement.Q("demo-stamina-3");
                float top=float.PositiveInfinity;
                foreach(var renderer in match.GetComponent<FiveDemoMotion>().PlayerRenderers(3).Where(r=>r.enabled))
                {
                    var b=renderer.bounds;
                    foreach(float x in new[]{b.min.x,b.max.x})foreach(float y in new[]{b.min.y,b.max.y})foreach(float z in new[]{b.min.z,b.max.z})
                        top=Mathf.Min(top,RuntimePanelUtils.CameraTransformWorldToPanel(bar.panel,new Vector3(x,y,z),match.overview).y);
                }
                yield return Capture(prefix+"-human");
                CheckDemo(top-bar.worldBound.yMax>=2,"striker-bar-clears-body-"+prefix,"gap="+(top-bar.worldBound.yMax));
                HandleEscape();yield return null;CheckLaptopBounds(prefix+"-pause");CheckFullOverlay(pause,prefix+"-pause");yield return Capture(prefix+"-pause");HandleEscape();
                match.Tick(match.profile.duration);yield return new WaitForSecondsRealtime(.3f);
                CheckDemo(Page=="results","results-reachable-"+prefix);CheckLaptopBounds(prefix+"-results");yield return Capture(prefix+"-results");
            }
            Home();yield return null;
        }
        void CheckFullOverlay(VisualElement overlay,string name)
        {
            if(overlay==pause)CheckPauseLayout(name);
            var a=overlay.worldBound;var b=viewport.worldBound;
            CheckDemo(Mathf.Abs(a.xMin-b.xMin)<1&&Mathf.Abs(a.yMin-b.yMin)<1
                &&Mathf.Abs(a.xMax-b.xMax)<1&&Mathf.Abs(a.yMax-b.yMax)<1,"overlay-fills-screen-"+name,a.ToString());
        }
        void CheckPauseLayout(string name)
        {
            var managers=screen.Q("managerHud").worldBound;
            foreach(string buttonName in new[]{"pause-resume","pause-quit"})
            {
                var button=pause.Q<Button>(buttonName);
                CheckDemo(!button.worldBound.Overlaps(managers)&&!button.worldBound.Overlaps(controls.worldBound)
                    &&button.worldBound.yMax<managers.yMin,"pause-buttons-clear-hud-"+name+buttonName);
                float gap=(managers.yMin-button.worldBound.yMax)/stage.resolvedStyle.scale.value.x;
                CheckDemo(Mathf.Abs(gap-28)<1,"pause-manager-gap-28-"+name+buttonName,gap.ToString());
                bool resume=buttonName=="pause-resume";
                CheckDemo(button.resolvedStyle.color==(resume?C("#172B50"):Color.white)
                    &&button.resolvedStyle.backgroundColor==C(resume?"#F1F3F7":"#172B50"),"pause-button-colors-"+name+buttonName);
                CheckDemo(resume?button.resolvedStyle.borderTopWidth==2&&button.resolvedStyle.borderTopColor==C("#172B50")
                    :button.resolvedStyle.borderTopWidth==0,"pause-button-borders-"+name+buttonName);
            }
            CheckDemo(pause.Q("pause-title").layout.y==130,"pause-title-lowered-"+name);
        }
        void CheckLaptopBounds(string name)
        {
            var bounds=viewport.worldBound;var layout=stage.worldBound;
            bool Inside(Rect r)=>r.xMin>=bounds.xMin-.5f&&r.yMin>=bounds.yMin-.5f&&r.xMax<=bounds.xMax+.5f&&r.yMax<=bounds.yMax+.5f;
            CheckDemo(Inside(layout)&&Mathf.Abs(layout.center.x-bounds.center.x)<1&&Mathf.Abs(layout.center.y-bounds.center.y)<1,"stage-centered-and-contained-"+name,layout.ToString());
            foreach(var button in stage.Query<Button>().ToList().Where(b=>b.resolvedStyle.display!=DisplayStyle.None&&b.worldBound.width>0))
                CheckDemo(Inside(button.worldBound),"button-inside-screen-"+name,button.text+" "+button.worldBound);
        }
    }
}
