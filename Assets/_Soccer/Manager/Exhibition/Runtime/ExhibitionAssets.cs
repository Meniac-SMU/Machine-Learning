using UnityEngine;
using UnityEngine.TextCore.Text;
using Unity.InferenceEngine;

namespace MachineLearning.Soccer.Manager.Exhibition
{
    public sealed class ExhibitionAssets : ScriptableObject
    {
        public GameObject arena;
        public ModelAsset[] models;
        public FontAsset[] fonts;
        public Texture2D teamLogo, gameLogo;
        public static readonly string[] Ids = { "stage-0", "stage-400000", "stage-800000", "stage-1200000", "stage-1600000", "stage-2000000", "reference-recover", "reference-balanced", "reference-carry-shot", "reference-uniform-valid" };
        public static readonly int[] Steps = { 0, 400719, 801184, 1200569, 1600333, 2001834 };
    }
}
