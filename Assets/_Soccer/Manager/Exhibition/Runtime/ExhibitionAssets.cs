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
        public const int PassModelId = 10;
        public static bool IsNeural(int id) => id >= 0 && (id < 6 || id == PassModelId);
        public static int NeuralIndex(int id) => id == PassModelId ? 6 : id;
        public static readonly int[] NeuralIds = { 0, 1, 2, 3, 4, 5, PassModelId };
        public static readonly string[] Ids = { "stage-0", "stage-400000", "stage-800000", "stage-1200000", "stage-1600000", "stage-2000000", "reference-recover", "reference-balanced", "reference-carry-shot", "reference-uniform-valid", "stage-2400000-pass" };
        public static readonly int[] Steps = { 0, 400719, 801184, 1200569, 1600333, 2001834 };
    }
}
