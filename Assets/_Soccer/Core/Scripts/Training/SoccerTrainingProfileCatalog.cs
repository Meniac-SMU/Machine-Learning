using System;
using System.Collections.Generic;
using System.Linq;

namespace MachineLearning.Soccer
{
    public sealed class SoccerTrainingProfile
    {
        public SoccerTrainingProfile(
            string key,
            string displayName,
            string sceneAssetPath,
            string trainerConfigAssetPath,
            string redBehaviorName,
            string navyBehaviorName,
            string runIdPrefix,
            int defaultBasePort,
            int maxSteps,
            bool trainRed,
            bool trainNavy,
            bool forceRedFallback,
            bool forceNavyFallback,
            bool requiresSelfPlay,
            bool supportsTraining,
            bool requiresNavyModel)
        {
            Key = key;
            DisplayName = displayName;
            SceneAssetPath = sceneAssetPath;
            TrainerConfigAssetPath = trainerConfigAssetPath;
            RedBehaviorName = redBehaviorName;
            NavyBehaviorName = navyBehaviorName;
            RunIdPrefix = runIdPrefix;
            DefaultBasePort = defaultBasePort;
            MaxSteps = maxSteps;
            TrainRed = trainRed;
            TrainNavy = trainNavy;
            ForceRedFallback = forceRedFallback;
            ForceNavyFallback = forceNavyFallback;
            RequiresSelfPlay = requiresSelfPlay;
            SupportsTraining = supportsTraining;
            RequiresNavyModel = requiresNavyModel;
        }

        public string Key { get; }
        public string DisplayName { get; }
        public string SceneAssetPath { get; }
        public string SceneName => System.IO.Path.GetFileNameWithoutExtension(SceneAssetPath);
        public string TrainerConfigAssetPath { get; }
        public string RedBehaviorName { get; }
        public string NavyBehaviorName { get; }
        public string RunIdPrefix { get; }
        public int DefaultBasePort { get; }
        public int MaxSteps { get; }
        public bool TrainRed { get; }
        public bool TrainNavy { get; }
        public bool ForceRedFallback { get; }
        public bool ForceNavyFallback { get; }
        public bool RequiresSelfPlay { get; }
        public bool SupportsTraining { get; }
        public bool RequiresNavyModel { get; }
    }

    public static class SoccerTrainingProfileCatalog
    {
        public const int PolicyContractVersion = 2;
        public const string BaseFallbackKey = "base-fallback";
        public const string BaseSelfPlayKey = "base-selfplay";
        public const string AttackKey = "attack";
        public const string DefenseKey = "defense";
        public const string PressKey = "press";
        public const string RuleEvaluationKey = "rule-eval";
        public const int BaseFallbackMaxSteps = 5_000_000;
        public const int DefaultTrainingMaxSteps = 500_000;

        static readonly SoccerTrainingProfile[] AllProfiles =
        {
            new SoccerTrainingProfile(
                BaseFallbackKey,
                "Base - Red Training vs Navy Fallback",
                "Assets/_Soccer/Core/Scenes/Stadium4v4_Base_FallbackTraining.unity",
                "Assets/_Soccer/Training/soccer_4v4_poca_base_fallback.yaml",
                "Soccer4v4_Base",
                "Soccer4v4_Base",
                "Base",
                5055,
                BaseFallbackMaxSteps,
                true,
                false,
                false,
                true,
                false,
                true,
                false),
            new SoccerTrainingProfile(
                BaseSelfPlayKey,
                "Base - Self-Play",
                "Assets/_Soccer/Core/Scenes/Stadium4v4_Base.unity",
                "Assets/_Soccer/Training/soccer_4v4_poca.yaml",
                "Soccer4v4_Base",
                "Soccer4v4_Base",
                "Base",
                5005,
                DefaultTrainingMaxSteps,
                true,
                true,
                false,
                false,
                true,
                true,
                false),
            new SoccerTrainingProfile(
                AttackKey,
                "Attack - Red Training vs Navy Base",
                "Assets/_Soccer/Teams/Attack_KMW/Scenes/Stadium4v4_Attack.unity",
                "Assets/_Soccer/Teams/Attack_KMW/Training/attack_poca.yaml",
                "Soccer4v4_Attack",
                "Soccer4v4_Base",
                "Attack",
                5105,
                DefaultTrainingMaxSteps,
                true,
                false,
                false,
                false,
                false,
                true,
                true),
            new SoccerTrainingProfile(
                DefenseKey,
                "Defense - Red Training vs Navy Base",
                "Assets/_Soccer/Teams/Defense_PJH/Scenes/Stadium4v4_Defense.unity",
                "Assets/_Soccer/Teams/Defense_PJH/Training/defense_poca.yaml",
                "Soccer4v4_Defense",
                "Soccer4v4_Base",
                "Defense",
                5205,
                DefaultTrainingMaxSteps,
                true,
                false,
                false,
                false,
                false,
                true,
                true),
            new SoccerTrainingProfile(
                PressKey,
                "Press - Red Training vs Navy Base",
                "Assets/_Soccer/Teams/Press_KMG/Scenes/Stadium4v4_Press.unity",
                "Assets/_Soccer/Teams/Press_KMG/Training/press_poca.yaml",
                "Soccer4v4_Press",
                "Soccer4v4_Base",
                "Press",
                5305,
                DefaultTrainingMaxSteps,
                true,
                false,
                false,
                false,
                false,
                true,
                true),
            new SoccerTrainingProfile(
                RuleEvaluationKey,
                "Rule - Evaluation Only",
                "Assets/_Soccer/Teams/Rule_PHC/Scenes/Stadium4v4_Rule.unity",
                string.Empty,
                "Soccer4v4_Rule",
                "Soccer4v4_Base",
                "Rule",
                5405,
                0,
                false,
                false,
                false,
                false,
                false,
                false,
                false)
        };

        public static IReadOnlyList<SoccerTrainingProfile> Profiles => AllProfiles;

        public static IEnumerable<SoccerTrainingProfile> TrainableProfiles =>
            AllProfiles.Where(profile => profile.SupportsTraining);

        public static bool TryGet(string key, out SoccerTrainingProfile profile)
        {
            profile = AllProfiles.FirstOrDefault(candidate =>
                string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));
            return profile != null;
        }

        public static string ValidKeys => string.Join(", ", AllProfiles.Select(profile => profile.Key));
    }
}
