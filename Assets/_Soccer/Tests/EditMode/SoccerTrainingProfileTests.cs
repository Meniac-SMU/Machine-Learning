using System.IO;
using System.Linq;
using MachineLearning.Soccer.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace MachineLearning.Soccer.Tests
{
    public sealed class SoccerTrainingProfileTests
    {
        [Test]
        public void CatalogContainsFiveTrainableProfilesAndRuleEvaluation()
        {
            var profiles = SoccerTrainingProfileCatalog.Profiles;
            CollectionAssert.AreEqual(
                new[] { "base-fallback", "base-selfplay", "attack", "defense", "press", "rule-eval" },
                profiles.Select(profile => profile.Key).ToArray());
            Assert.AreEqual(5, profiles.Count(profile => profile.SupportsTraining));
            Assert.AreEqual(6, profiles.Select(profile => profile.SceneAssetPath).Distinct().Count());
            Assert.AreEqual(5, profiles.Where(profile => profile.SupportsTraining)
                .Select(profile => profile.DefaultBasePort).Distinct().Count());
            CollectionAssert.AreEqual(
                new[] { "attack", "defense", "press" },
                profiles.Where(profile => profile.RequiresNavyModel).Select(profile => profile.Key).ToArray());
            Assert.AreEqual(
                SoccerTrainingProfileCatalog.BaseFallbackMaxSteps,
                profiles.Single(profile => profile.Key == SoccerTrainingProfileCatalog.BaseFallbackKey).MaxSteps);
            Assert.IsTrue(profiles.Where(profile => profile.SupportsTraining
                    && profile.Key != SoccerTrainingProfileCatalog.BaseFallbackKey)
                .All(profile => profile.MaxSteps == SoccerTrainingProfileCatalog.DefaultTrainingMaxSteps));

            CollectionAssert.AreEqual(
                new[] { SoccerTrainingBuildBuilder.BootstrapScenePath }
                    .Concat(profiles.Select(profile => profile.SceneAssetPath))
                    .ToArray(),
                SoccerTrainingBuildBuilder.GetBuildScenePathsForTests());
        }

        [TestCase("--training-profile", "attack", "attack")]
        [TestCase("--training-profile=base-selfplay", null, "base-selfplay")]
        [TestCase("--training-profile=BASE-FALLBACK", null, "base-fallback")]
        public void BootstrapParsesSupportedProfileArguments(string argument, string separateValue, string expected)
        {
            var args = separateValue == null
                ? new[] { "SoccerTraining.exe", argument }
                : new[] { "SoccerTraining.exe", argument, separateValue };

            Assert.IsTrue(SoccerTrainingBootstrap.TryReadProfileArgument(args, out var profileKey, out var error));
            Assert.AreEqual(expected, profileKey);
            Assert.IsNull(error);
        }

        [Test]
        public void BootstrapRejectsUnknownOrDuplicateProfiles()
        {
            Assert.IsFalse(SoccerTrainingBootstrap.TryReadProfileArgument(
                new[] { "SoccerTraining.exe", "--training-profile", "unknown" },
                out _,
                out var unknownError));
            StringAssert.Contains("Unknown training profile", unknownError);

            Assert.IsFalse(SoccerTrainingBootstrap.TryReadProfileArgument(
                new[]
                {
                    "SoccerTraining.exe",
                    "--training-profile", "attack",
                    "--training-profile", "defense"
                },
                out _,
                out var duplicateError));
            StringAssert.Contains("more than once", duplicateError);
        }

        [Test]
        public void EveryTrainableProfileUsesAsciiSafeCatalogStepTarget()
        {
            foreach (var profile in SoccerTrainingProfileCatalog.TrainableProfiles)
            {
                var text = File.ReadAllText(profile.TrainerConfigAssetPath).Replace("\r\n", "\n");
                Assert.IsTrue(text.All(character => character <= 0x7f), profile.TrainerConfigAssetPath);
                StringAssert.Contains("\n  " + profile.RedBehaviorName + ":", text, profile.Key);
                StringAssert.Contains($"\n    max_steps: {profile.MaxSteps}", text, profile.Key);
                Assert.AreEqual(profile.RequiresSelfPlay, text.Contains("\n    self_play:"), profile.Key);
            }
        }

        [Test]
        public void BaseFallbackSceneForcesNavyHeuristicIndependentlyOfRegisteredModel()
        {
            var profile = SoccerTrainingProfileCatalog.Profiles.Single(candidate =>
                candidate.Key == SoccerTrainingProfileCatalog.BaseFallbackKey);
            var previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(profile.SceneAssetPath, OpenSceneMode.Single);
                var setup = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SoccerMatchSetup>(true))
                    .Single();
                Assert.IsTrue(setup.TrainRed);
                Assert.IsFalse(setup.TrainNavy);
                Assert.IsFalse(setup.ForceRedFallback);
                Assert.IsTrue(setup.ForceNavyFallback);
                Assert.IsTrue(setup.IsTrainable(Team.Red));
                Assert.IsFalse(setup.IsTrainable(Team.Navy));
                Assert.IsNull(setup.GetConfiguredModel(Team.Navy));
            }
            finally
            {
                if (previousSceneSetup.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousSceneSetup);
                }
            }
        }

        [Test]
        public void CatalogScenesAndTrainerConfigsExist()
        {
            foreach (var profile in SoccerTrainingProfileCatalog.Profiles)
            {
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SceneAsset>(profile.SceneAssetPath), profile.Key);
                if (profile.SupportsTraining)
                {
                    Assert.IsTrue(File.Exists(profile.TrainerConfigAssetPath), profile.TrainerConfigAssetPath);
                }
                else
                {
                    Assert.IsEmpty(profile.TrainerConfigAssetPath);
                }
            }
        }
    }
}
