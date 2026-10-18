using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    [DefaultExecutionOrder(200)]
    public sealed class FiveDemoStaminaHud : MonoBehaviour
    {
        sealed class Bar
        {
            public FivePlayer player;
            public VisualElement root,fill;
            public Renderer[] renderers;
        }
        readonly List<Bar> bars=new();
        readonly List<Rect> occupied=new();
        FiveMatch match;
        FiveDemoCamera cameras;
        FiveDemoMotion motion;
        VisualElement layer;

        public void Initialize(FiveMatch current,FiveDemoCamera views,UIDocument document,StyleSheet style)
        {
            match=current;cameras=views;motion=match.GetComponent<FiveDemoMotion>();
            var root=document.rootVisualElement;
            root.Q("heads").style.display=DisplayStyle.None;
            root.styleSheets.Add(style);
            layer=new VisualElement{name="demo-stamina-layer",pickingMode=PickingMode.Ignore};
            layer.AddToClassList("demo-stamina-layer");root.Add(layer);
            foreach(var player in match.players)
            {
                if(player.IsKeeper)continue;
                var bar=new VisualElement{name="demo-stamina-"+player.Index,pickingMode=PickingMode.Ignore};
                bar.AddToClassList("demo-stamina");bar.AddToClassList(player.team==Team.Red?"red":"navy");
                var fill=new VisualElement{name="fill",pickingMode=PickingMode.Ignore};
                fill.AddToClassList("demo-stamina-fill");bar.Add(fill);layer.Add(bar);
                bars.Add(new Bar{player=player,root=bar,fill=fill,renderers=motion.PlayerRenderers(player.Index)});
            }
        }

        void LateUpdate()
        {
            if(layer?.panel==null||match==null)return;
            bool close=cameras.CurrentView!=FiveDemoCamera.View.Broadcast;
            int owner=cameras.CurrentView==FiveDemoCamera.View.RedKeeper?0:cameras.CurrentView==FiveDemoCamera.View.NavyKeeper?5:3;
            occupied.Clear();
            foreach(var bar in bars)
            {
                var player=bar.player;
                bar.root.EnableInClassList("close",close);
                bar.root.EnableInClassList("own",close&&player.Index==owner);
                float value=player.Stamina.Value;
                bar.fill.style.width=Length.Percent(100*Mathf.Clamp01(value));
                bar.root.EnableInClassList("tired",value<=.7f&&value>.4f);
                bar.root.EnableInClassList("exhausted",value<=.4f);
                var head=motion.PlayerTarget(player.Index).position+Vector3.up*player.headHeight;
                if(close&&bar.renderers.Length>0)head.y=bar.renderers.Where(r=>r.enabled).Max(r=>r.bounds.max.y)+.42f;
                var viewport=match.overview.WorldToViewportPoint(head);
                bool visible=viewport.z>match.overview.nearClipPlane;
                bar.root.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;
                if(!visible)continue;
                var anchor=layer.WorldToLocal(RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel,head,match.overview));
                float width=close?64:40,height=close?18:6;
                float scale=1;
                if(close)
                {
                    // Project a camera-facing, 0.936 m wide bar. Scale its border and padding
                    // with the fill so the whole bar follows perspective, without a pixel-size clamp.
                    var halfWidth=match.overview.transform.right*.468f;
                    var left=RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel,head-halfWidth,match.overview);
                    var right=RuntimePanelUtils.CameraTransformWorldToPanel(layer.panel,head+halfWidth,match.overview);
                    scale=Vector2.Distance(layer.WorldToLocal(left),layer.WorldToLocal(right))/width;
                }
                // Only the horizontal world extent increased by 30%; preserve vertical thickness.
                bar.root.style.scale=new Scale(new Vector3(scale,close?scale/1.3f:scale,1));
                var rect=new Rect(anchor.x-width/2,anchor.y-height-(close?0:1),width,height);
                if(!close)
                {
                    for(int tries=0;tries<8&&occupied.Exists(r=>r.Overlaps(rect));tries++)rect.y-=10;
                    occupied.Add(rect);
                }
                bar.root.style.left=rect.x;bar.root.style.top=rect.y;
            }
        }

        void OnDestroy(){layer?.RemoveFromHierarchy();}
    }
}
