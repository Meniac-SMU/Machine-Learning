using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Manager5v5
{
    // An opt-in rendered Player smoke run; no model, trainer or network connection.
    public sealed class FiveReviewRun : MonoBehaviour
    {
        [Serializable] sealed class Receipt
        {
            public string playerRuntime=FiveMatch.PlayerRuntimeVersion;
            public int contestEpisodes;
            public int frontCushions,keeperCushions;
            public int reinforcements,challengeRoutes,carrierEscapes;
            public int redScore,navyScore,passes,receptions,shots,boundaryCorrections,headBars;
            public float elapsed;
            public bool finished,trainingStarted=false;
        }
        string evidence;
        RenderTexture worldTexture,uiTexture;
        FiveMatch runningMatch;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,"--five-review-root");
            if(index<0||index+1>=args.Length)return;
            var path=Path.GetFullPath(args[index+1]);
            if(!path.StartsWith(@"C:\GitHub\Machine-Learning\",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Review files must stay in the project.");
            Application.runInBackground=true;
            var runner=new GameObject("Five rendered review").AddComponent<FiveReviewRun>();runner.evidence=path;
        }
        IEnumerator Start()
        {
            Directory.CreateDirectory(evidence);yield return null;
            var match=FindFirstObjectByType<FiveMatch>();
            if(match==null){Application.Quit(2);yield break;}
            runningMatch=match;
            worldTexture=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);
            uiTexture=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32);
            worldTexture.Create();uiTexture.Create();match.overview.targetTexture=worldTexture;
            var panel=FindFirstObjectByType<UIDocument>().panelSettings;
            panel.targetTexture=uiTexture;panel.clearColor=true;panel.colorClearValue=Color.clear;
            Application.targetFrameRate=60;Time.timeScale=4;
            bool captured=false;
            float nextReviewFrame=60;
            while(match.State!=FiveMatchState.Finished)
            {
                if(!captured&&match.Elapsed>12)
                {Capture("match");captured=true;}
                if(match.Elapsed>=nextReviewFrame)
                {Capture("minute-"+Mathf.RoundToInt(nextReviewFrame/60));nextReviewFrame+=60;}
                yield return null;
            }
            Time.timeScale=1;
            yield return null;yield return null;Capture("finished");
            yield return new WaitForSecondsRealtime(1);
            var receipt=new Receipt{redScore=match.RedScore,navyScore=match.NavyScore,passes=match.PassAttempts,receptions=match.PassReceptions,shots=match.Shots,
                contestEpisodes=match.Events.Count(e=>e.kind=="contest-start"),
                carrierEscapes=match.Events.Count(e=>e.kind=="carrier-escape"),
                frontCushions=match.Events.Count(e=>e.kind=="front-cushion"),
                keeperCushions=match.Events.Count(e=>e.kind=="front-cushion"&&e.reason=="keeper-front-save"),
                reinforcements=match.Events.Count(e=>e.kind=="contest-reinforce"),challengeRoutes=match.Events.Count(e=>e.kind=="challenge-route"),
                boundaryCorrections=match.BoundaryCorrections,headBars=FindFirstObjectByType<FiveHud>().BarCount,elapsed=match.Elapsed,finished=match.State==FiveMatchState.Finished};
            File.WriteAllText(Path.Combine(evidence,"player-result.json"),JsonUtility.ToJson(receipt,true));
            File.WriteAllText(Path.Combine(evidence,"events.json"),"["+string.Join(",",match.Events.Select(e=>JsonUtility.ToJson(e)))+"]");
            var screenshots=File.Exists(Path.Combine(evidence,"match.png"))&&File.Exists(Path.Combine(evidence,"finished.png"));
            Application.Quit(receipt.finished&&receipt.headBars==8&&receipt.boundaryCorrections==0&&screenshots?0:3);
        }
        void Capture(string name)
        {
            runningMatch.overview.Render();
            var old=RenderTexture.active;
            var world=new Texture2D(1600,1000,TextureFormat.RGBA32,false);
            var ui=new Texture2D(1600,1000,TextureFormat.RGBA32,false);
            RenderTexture.active=worldTexture;world.ReadPixels(new Rect(0,0,1600,1000),0,0);world.Apply();
            RenderTexture.active=uiTexture;ui.ReadPixels(new Rect(0,0,1600,1000),0,0);ui.Apply();RenderTexture.active=old;
            File.WriteAllBytes(Path.Combine(evidence,name+"-world.png"),world.EncodeToPNG());
            File.WriteAllBytes(Path.Combine(evidence,name+"-ui.png"),ui.EncodeToPNG());
            // Composite the actual camera and UI render targets solely for review output.
            var pixels=world.GetPixels32();var overlay=ui.GetPixels32();
            for(int i=0;i<pixels.Length;i++)
            {
                var a=overlay[i].a/255f;
                pixels[i]=new Color32((byte)(overlay[i].r+pixels[i].r*(1-a)),(byte)(overlay[i].g+pixels[i].g*(1-a)),(byte)(overlay[i].b+pixels[i].b*(1-a)),255);
            }
            world.SetPixels32(pixels);world.Apply();File.WriteAllBytes(Path.Combine(evidence,name+".png"),world.EncodeToPNG());
            Destroy(world);Destroy(ui);
        }
    }
}
