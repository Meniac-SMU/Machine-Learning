using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed partial class FiveDemoApp
    {
        Texture2D coneCursor;
        Vector2 coneHotspot;
        bool splashCursorHidden;
        Image conePointer;
        const float CursorDisplaySize=38f;
        bool CursorShown=>conePointer!=null&&conePointer.style.display.value==DisplayStyle.Flex;

        void InitializeCursor()
        {
            const int size=128;
            var source=assets.cursorSource;
            float angle=17.5f*Mathf.Deg2Rad,c=Mathf.Cos(angle),s=Mathf.Sin(angle);
            Vector2 Rotate(Vector2 p)=>new Vector2(c*p.x-s*p.y,s*p.x+c*p.y);
            var center=new Vector2(source.width,source.height)*.5f;
            var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            int top=-1;float tipMin=0,tipMax=0;
            var pixels=source.GetPixels32();
            for(int y=0;y<source.height;y++)
            for(int x=0;x<source.width;x++)
            {
                if(pixels[y*source.width+x].a<26)continue;
                var p=Rotate(new Vector2(x+.5f,y+.5f)-center);
                min=Vector2.Min(min,p);max=Vector2.Max(max,p);
                if(y>top){top=y;tipMin=tipMax=x+.5f;}else if(y==top)tipMax=x+.5f;
            }
            float scale=(size-size/16f)/Mathf.Max(max.x-min.x,max.y-min.y);
            var offset=(Vector2.one*size-(max-min)*scale)*.5f;
            coneCursor=new Texture2D(size,size,TextureFormat.RGBA32,true){name="SAICCER cone cursor 17.5 degrees",filterMode=FilterMode.Trilinear,wrapMode=TextureWrapMode.Clamp};
            var output=new Color[size*size];
            for(int y=0;y<size;y++)
            for(int x=0;x<size;x++)
            {
                Color sum=Color.clear;
                for(int sy=0;sy<4;sy++)for(int sx=0;sx<4;sx++)
                {
                    var p=(new Vector2(x+(sx+.5f)/4,y+(sy+.5f)/4)-offset)/scale+min;
                    var uv=new Vector2(c*p.x+s*p.y,-s*p.x+c*p.y)+center;
                    if(uv.x<0||uv.y<0||uv.x>=source.width||uv.y>=source.height)continue;
                    var sample=source.GetPixelBilinear(uv.x/source.width,uv.y/source.height);
                    sum.r+=sample.r*sample.a;sum.g+=sample.g*sample.a;sum.b+=sample.b*sample.a;sum.a+=sample.a;
                }
                if(sum.a>0){sum.r/=sum.a;sum.g/=sum.a;sum.b/=sum.a;sum.a/=16;}
                output[y*size+x]=sum;
            }
            coneCursor.SetPixels(output);coneCursor.Apply();
            var tip=(Rotate(new Vector2((tipMin+tipMax)*.5f,top+.5f)-center)-min)*scale+offset;
            coneHotspot=new Vector2(tip.x,size-tip.y)*(CursorDisplaySize/size);
            conePointer=new Image{image=coneCursor,name="cone-pointer",pickingMode=PickingMode.Ignore,scaleMode=ScaleMode.StretchToFill};
            Place(conePointer,0,0,CursorDisplaySize,CursorDisplaySize);
            conePointer.style.display=DisplayStyle.None;
            Cursor.SetCursor(null,Vector2.zero,CursorMode.Auto);
        }

        void ApplyCursorVisibility()
        {
            bool visible=Page=="home"||Page=="setup"||Page=="results"||(Page=="match"&&Paused);
            if(visible||Page=="splash")Cursor.lockState=CursorLockMode.None;
            Cursor.visible=false;
            if(conePointer!=null)
            {
                conePointer.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;
                if(visible&&conePointer.panel!=null&&Mouse.current!=null)
                {
                    var mouse=Mouse.current.position.ReadValue();
                    var point=RuntimePanelUtils.ScreenToPanel(conePointer.panel,new Vector2(mouse.x,Screen.height-mouse.y));
                    conePointer.style.left=point.x-coneHotspot.x;conePointer.style.top=point.y-coneHotspot.y;
                }
            }
            if(Page=="splash")splashCursorHidden=!Cursor.visible&&!CursorShown;
        }

        void OnApplicationFocus(bool focused){if(focused&&coneCursor!=null)ApplyCursorVisibility();}
        void ReleaseCursor(){Cursor.SetCursor(null,Vector2.zero,CursorMode.Auto);if(coneCursor!=null)Destroy(coneCursor);}
    }
}
