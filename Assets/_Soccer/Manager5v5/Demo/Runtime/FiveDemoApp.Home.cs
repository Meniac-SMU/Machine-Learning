using System;
using UnityEngine;
using UnityEngine.UIElements;
namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed partial class FiveDemoApp
    {
        void DrawHome()
        {
            NewPage("home", C("#101310"));
            var art=Box(screen,1075,0,845,1080,C("#CCFC4B"));art.name="home-art";art.style.overflow=Overflow.Hidden;
            string[] colors={"#185837","#91BE39","#CF8B50","#4944C5","#A3CAFB"};
            for(int i=0;i<5;i++)
            {
                var ribbon=Box(art,-192+i*77,192+i*72,1267-i*173,1574-i*140,C(colors[i]));
                ribbon.style.borderTopLeftRadius=ribbon.style.borderTopRightRadius=700;
                ribbon.style.rotate=new Rotate(new Angle(-32));
            }
            var artTitle=Text(art,"THE GAME\nIS LEARNING.",65,67,660,190,70,5,Ink);artTitle.style.rotate=new Rotate(new Angle(-6));
            var ball=Box(art,400,440,288,288,C("#F3F1DC"));ball.style.borderTopLeftRadius=ball.style.borderTopRightRadius=ball.style.borderBottomLeftRadius=ball.style.borderBottomRightRadius=144;Border(ball,Ink,8);
            ball.name="home-art-ball";
            ball.generateVisualContent += context =>
            {
                var p=context.painter2D;p.fillColor=Ink;p.BeginPath();
                p.MoveTo(new Vector2(144,37));p.LineTo(new Vector2(173,113));p.LineTo(new Vector2(249,144));p.LineTo(new Vector2(173,175));p.LineTo(new Vector2(144,251));p.LineTo(new Vector2(115,175));p.LineTo(new Vector2(39,144));p.LineTo(new Vector2(115,113));p.ClosePath();p.Fill();
            };
            Text(art,"RED × NAVY",500,958,275,80,43,5,Ink,true).name="home-art-footer";
            Box(screen,88,91,10,10,Lime);
            Text(screen,L("eyebrow"),115,77,872,58,20.4f,2);
            var logo=Text(screen,"S<color=#B9F54E>AI</color>CCER",88,250,935,240,185,5);
            logo.name="game-logo";logo.enableRichText=true;
            var tagline=Text(screen,L("tagline"),88,502,900,91,28.4f,1,C("#CDD2C9"));tagline.style.whiteSpace=WhiteSpace.Normal;tagline.style.unityTextAlign=TextAnchor.UpperLeft;
            var start=Button(screen,L("start"),88,662,827,104,Setup,"primary");start.style.fontSize=38.4f;start.style.unityTextAlign=TextAnchor.MiddleLeft;start.style.paddingLeft=32;
            var language=Button(screen,"Language",88,791,266,67,LanguagePopup,"text-button");
            var globe=Picture(language,assets.languageIcon,26,19.5f,28,28);globe.name="language-globe";globe.pickingMode=PickingMode.Ignore;
            Chevron(language,220,25,18,Color.white);
            Button(screen,L("credits"),368,791,266,67,()=>ShowCredits(),"text-button");
            Button(screen,L("guide"),648,791,267,67,()=>ShowGuide(0),"text-button");
            Text(screen,"MENIAC",88,997,250,26,22,5,C("#A4AFA1")).name="team-name";
            var quit=Button(screen,L("quitGame"),695,974,220,66,QuitGame,"text-button");
            quit.name="quitGame";Border(quit,Color.clear,0);quit.style.color=C("#BFC3BC");
            foreach(var child in screen.Children())child.userData=child.style.top.value.value;
            Resize();
        }
        void ResizeHome(float height)
        {
            if(screen==null||screen.name!="home")return;
            screen.style.height=height;
            float extra=height-1080;
            foreach(var child in screen.Children())
                if(child.userData is float top)child.style.top=top+extra*top/1080;
            var art=screen.Q("home-art");if(art==null)return;
            art.style.height=height;
            art.Q("home-art-ball").style.top=440+extra/2;
            art.Q("home-art-footer").style.top=958+extra;
        }
        VisualElement Box(VisualElement parent, float x, float y, float w, float h, Color color)
        { var e = new VisualElement(); e.style.position = Position.Absolute; e.style.left=x; e.style.top=y; e.style.width=w; e.style.height=h; e.style.backgroundColor=color; parent.Add(e); return e; }
        Label Text(VisualElement parent, string value, float x, float y, float w, float h, float size, int weight=2, Color? color=null, bool center=false)
        {
            var l = new Label(value); Place(l,x,y,w,h); l.style.fontSize=size;
            l.style.unityFontDefinition = FontDefinition.FromSDFFont(assets.fonts[weight]);
            l.style.color=color??Color.white; if(center)l.style.unityTextAlign=TextAnchor.MiddleCenter; parent.Add(l); return l;
        }
        Button Button(VisualElement parent, string value, float x, float y, float w, float h, Action action, string cls=null)
        {
            var b = new Button(action){text=value}; Place(b,x,y,w,h); b.style.unityFontDefinition=FontDefinition.FromSDFFont(assets.fonts[3]);
            if(cls!=null)b.AddToClassList(cls); parent.Add(b); return b;
        }
        static void Place(VisualElement e,float x,float y,float w,float h)
        { e.style.position=Position.Absolute;e.style.left=x;e.style.top=y;e.style.width=w;e.style.height=h; }
        static void Border(VisualElement e,Color c,float n)
        {e.style.borderLeftWidth=e.style.borderRightWidth=e.style.borderTopWidth=e.style.borderBottomWidth=n;e.style.borderLeftColor=e.style.borderRightColor=e.style.borderTopColor=e.style.borderBottomColor=c;}
        Image Picture(VisualElement p,Texture2D texture,float x,float y,float w,float h)
        { var image=new Image{image=texture,scaleMode=ScaleMode.ScaleToFit};Place(image,x,y,w,h);p.Add(image);return image; }
        void Chevron(VisualElement parent,float x,float y,float size,Color color)
        {
            var mark=Box(parent,x,y,size,size,Color.clear);mark.pickingMode=PickingMode.Ignore;
            mark.generateVisualContent+=context=>{var p=context.painter2D;p.strokeColor=color;p.lineWidth=2;p.BeginPath();p.MoveTo(new Vector2(1,size*.3f));p.LineTo(new Vector2(size/2,size*.7f));p.LineTo(new Vector2(size-1,size*.3f));p.Stroke();};
        }
    }
}
