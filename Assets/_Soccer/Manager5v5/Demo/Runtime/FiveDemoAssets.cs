using UnityEngine;
using UnityEngine.TextCore.Text;
using Unity.InferenceEngine;
using TextAsset = UnityEngine.TextAsset;

namespace MachineLearning.Soccer.Manager5v5.Demo
{
    public sealed class FiveDemoAssets : ScriptableObject
    {
        public const string GameName="SAICCER";
        public const float SurroundingFloorY=-.01f;
        public Texture2D languageIcon;
        [System.Serializable]
        public sealed class CharacterSet
        {
            public string id,displayName;
            public GameObject fieldPlayer,goalkeeper;
        }
        public CharacterSet[] redCharacters,navyCharacters;
        public GameObject arena;
        public ModelAsset[] models;
        public FontAsset[] fonts;
        public Texture2D teamLogo, creditsKo, creditsEn, cursorSource;
        public TextAsset strings, catalog, inferenceFixtures;
        public static readonly int[] Steps = {0,400000,800000,1200000,1600000,2000000,2400000,2800000,3200000,3600000};
        // 0 is the rule manager; 1..Steps.Length are the actual checkpoints above.
        public static int LatestModelId
        {
            get { int latest=0; for(int i=1;i<Steps.Length;i++)if(Steps[i]>Steps[latest])latest=i; return latest+1; }
        }
        public static bool IsNeural(int id) => id >= 1 && id <= Steps.Length;
        public static string Name(int id, bool english) => id == 0 ? (english ? "Rule manager" : "규칙형 감독")
            : english ? (id==1 ? "0 steps" : ShortName(id,true)+" steps") : id==1 ? "0회 초기 모델" : $"{Steps[id-1]/10000}만 회 모델";

        public static string ShortName(int id,bool english)
        {
            if(id==0)return english?"Rule":"규칙형";
            if(!english)return id==1?"초기 모델":(Steps[id-1]/10000)+"만";
            int steps=Steps[id-1];
            if(steps==0)return "0 steps";
            bool millions=steps>=1000000;
            return (steps/(millions?1000000f:1000f)).ToString("0.#",System.Globalization.CultureInfo.InvariantCulture)+(millions?"M":"K");
        }
    }
}
