using System.IO;
using System.Linq;
using MachineLearning.Soccer.Curriculum;
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
        public void CatalogContainsElevenTrainableProfilesAndRuleEvaluation()
        {
            var profiles = SoccerTrainingProfileCatalog.Profiles;
            CollectionAssert.AreEqual(
                new[]
                {
                    "curriculum-l0", "curriculum-l1", "curriculum-l2", "curriculum-l2-find",
                    "curriculum-l2-score", "curriculum-l3", "base-fallback", "base-selfplay",
                    "attack", "defense", "press", "rule-eval"
                },
                profiles.Select(profile => profile.Key).ToArray());
            Assert.AreEqual(11, profiles.Count(profile => profile.SupportsTraining));
            Assert.AreEqual(12, profiles.Select(profile => profile.SceneAssetPath).Distinct().Count());
            Assert.AreEqual(11, profiles.Where(profile => profile.SupportsTraining)
                .Select(profile => profile.DefaultBasePort).Distinct().Count());
            CollectionAssert.AreEqual(
                new[] { "attack", "defense", "press" },
                profiles.Where(profile => profile.RequiresNavyModel).Select(profile => profile.Key).ToArray());
            Assert.AreEqual(
                SoccerTrainingProfileCatalog.BaseFallbackMaxSteps,
                profiles.Single(profile => profile.Key == SoccerTrainingProfileCatalog.BaseFallbackKey).MaxSteps);
            Assert.AreEqual(
                SoccerTrainingProfileCatalog.CurriculumL0MaxSteps,
                profiles.Single(profile => profile.Key == SoccerTrainingProfileCatalog.CurriculumL0Key).MaxSteps);
            Assert.AreEqual(
                SoccerTrainingProfileCatalog.CurriculumL1MaxSteps,
                profiles.Single(profile => profile.Key == SoccerTrainingProfileCatalog.CurriculumL1Key).MaxSteps);
            Assert.AreEqual(
                SoccerTrainingProfileCatalog.CurriculumL2FindMaxSteps,
                profiles.Single(profile => profile.Key == SoccerTrainingProfileCatalog.CurriculumL2FindKey).MaxSteps);
            Assert.AreEqual(
                SoccerTrainingProfileCatalog.CurriculumL2ScoreMaxSteps,
                profiles.Single(profile => profile.Key == SoccerTrainingProfileCatalog.CurriculumL2ScoreKey).MaxSteps);
            Assert.IsTrue(profiles.Where(profile => profile.SupportsTraining
                    && profile.Key != SoccerTrainingProfileCatalog.BaseFallbackKey
                    && profile.Key != SoccerTrainingProfileCatalog.CurriculumL0Key
                    && profile.Key != SoccerTrainingProfileCatalog.CurriculumL1Key
                    && profile.Key != SoccerTrainingProfileCatalog.CurriculumL2Key
                    && profile.Key != SoccerTrainingProfileCatalog.CurriculumL2FindKey
                    && profile.Key != SoccerTrainingProfileCatalog.CurriculumL2ScoreKey
                    && profile.Key != SoccerTrainingProfileCatalog.CurriculumL3Key)
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

        [TestCase(SoccerTrainingProfileCatalog.CurriculumL0Key, SoccerCurriculumLesson.L0BallApproach, 20f)]
        [TestCase(SoccerTrainingProfileCatalog.CurriculumL1Key, SoccerCurriculumLesson.L1CarryAndShoot, 30f)]
        [TestCase(SoccerTrainingProfileCatalog.CurriculumL2Key, SoccerCurriculumLesson.L2ShortPass, 20f)]
        [TestCase(SoccerTrainingProfileCatalog.CurriculumL2FindKey, SoccerCurriculumLesson.L2Find, 15f)]
        [TestCase(SoccerTrainingProfileCatalog.CurriculumL2ScoreKey, SoccerCurriculumLesson.L2Score, 30f)]
        [TestCase(SoccerTrainingProfileCatalog.CurriculumL3Key, SoccerCurriculumLesson.L3ProgressivePlay, 20f)]
        public void CurriculumScenesKeepBaseContractAndIsolateNavy(
            string profileKey,
            SoccerCurriculumLesson expectedLesson,
            float expectedDuration)
        {
            var profile = SoccerTrainingProfileCatalog.Profiles.Single(candidate => candidate.Key == profileKey);
            var previousSceneSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                var scene = EditorSceneManager.OpenScene(profile.SceneAssetPath, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var setup = roots.SelectMany(root => root.GetComponentsInChildren<SoccerMatchSetup>(true)).Single();
                var environment = roots.SelectMany(root => root.GetComponentsInChildren<SoccerEnvController>(true)).Single();
                var curriculum = roots.SelectMany(root => root.GetComponentsInChildren<SoccerCurriculumController>(true)).Single();
                Assert.IsTrue(setup.TrainRed);
                Assert.IsFalse(setup.TrainNavy);
                Assert.IsTrue(setup.ForceNavyFallback);
                Assert.IsNull(setup.GetConfiguredModel(Team.Navy));
                Assert.AreEqual("Soccer4v4_Base", setup.RedTeam.BehaviorName);
                Assert.AreEqual(expectedLesson, curriculum.Lesson);
                Assert.AreEqual(expectedDuration, environment.matchDurationSeconds, 0.001f);
                Assert.AreEqual(8, environment.AgentsList.Count);
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
        public void L1SupportShapeRequiresDistanceSeparationAndKeeperToRemainBehindCarrier()
        {
            var carrier = new UnityEngine.Vector3(-22f, 0.5f, 0f);
            var otherSupporters = new[]
            {
                new UnityEngine.Vector3(-34f, 0.5f, -10f),
                new UnityEngine.Vector3(-54f, 0.5f, 0f)
            };

            Assert.IsTrue(SoccerCurriculumController.IsValidSupportShape(
                new UnityEngine.Vector3(-34f, 0.5f, 10f),
                false,
                carrier,
                otherSupporters));
            Assert.IsFalse(SoccerCurriculumController.IsValidSupportShape(
                new UnityEngine.Vector3(-23f, 0.5f, 1f),
                false,
                carrier,
                otherSupporters),
                "A field supporter must not crowd the carrier.");
            Assert.IsFalse(SoccerCurriculumController.IsValidSupportShape(
                new UnityEngine.Vector3(-33f, 0.5f, -9f),
                false,
                carrier,
                otherSupporters),
                "Supporters must not crowd one another.");
            Assert.IsTrue(SoccerCurriculumController.IsValidSupportShape(
                new UnityEngine.Vector3(-54f, 0.5f, 0f),
                true,
                carrier,
                new[]
                {
                    new UnityEngine.Vector3(-34f, 0.5f, -10f),
                    new UnityEngine.Vector3(-34f, 0.5f, 10f)
                }));
            Assert.IsFalse(SoccerCurriculumController.IsValidSupportShape(
                new UnityEngine.Vector3(2f, 0.5f, 0f),
                true,
                carrier,
                otherSupporters),
                "The keeper cannot earn support reward outside its defensive half.");
        }

        [Test]
        public void RewardEngineCanSuppressGenericTeamShapeForCurriculumDrills()
        {
            var root = new UnityEngine.GameObject("CurriculumRewardModeTest");
            try
            {
                var environment = root.AddComponent<SoccerEnvController>();
                var engine = root.AddComponent<SoccerRewardEngine>();
                engine.ConfigureCurriculumRewardMode(true);
                Assert.IsTrue(engine.SuppressesTacticalTeamShape);
                Assert.IsTrue(engine.UsesCurriculumRewards);
                engine.ConfigureCurriculumRewardMode(false);
                Assert.IsFalse(engine.SuppressesTacticalTeamShape);
                Assert.IsFalse(engine.UsesCurriculumRewards);

                Assert.IsTrue(environment.KickActionsEnabled);
                environment.ConfigureCurriculumConstraints(false);
                Assert.IsFalse(environment.KickActionsEnabled);

                var bypassMethod = typeof(SoccerRewardEngine).GetMethod(
                    "BypassesProfileShapingCap",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
                Assert.IsNotNull(bypassMethod);
                Assert.IsTrue((bool)bypassMethod.Invoke(
                    null,
                    new object[] { SoccerRewardKind.CurriculumDribbleProgress }));
                Assert.IsFalse((bool)bypassMethod.Invoke(
                    null,
                    new object[] { SoccerRewardKind.ControlledCarry }));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void L1DefinesBridgingAndFinalCarryTargets()
        {
            Assert.AreEqual(2f, SoccerCurriculumController.L1RequiredDribbleProgress, 0.001f);
            Assert.AreEqual(0f, SoccerCurriculumController.L1ShortFinishRequiredCarryProgress, 0.001f);
            Assert.AreEqual(8f, SoccerCurriculumController.L1FinalRequiredCarryProgress, 0.001f);
        }

        [Test]
        public void CurriculumAllowlistRejectsEveryGenericRewardIncludingPenalties()
        {
            var curriculumKinds = new[] {
                SoccerRewardKind.CurriculumApproachProgress,
                SoccerRewardKind.CurriculumPossessionEstablished,
                SoccerRewardKind.CurriculumSupportShape,
                SoccerRewardKind.CurriculumDribbleProgress,
                SoccerRewardKind.CurriculumShotAttempt,
                SoccerRewardKind.CurriculumShotOnTarget,
                SoccerRewardKind.CurriculumGoal,
                SoccerRewardKind.CurriculumShotAlignment,
                SoccerRewardKind.CurriculumPassAttempt,
                SoccerRewardKind.CurriculumPassReception,
                SoccerRewardKind.CurriculumPassDelivered,
                SoccerRewardKind.CurriculumPassDirection,
                SoccerRewardKind.CurriculumPassOpportunityLost,
                SoccerRewardKind.CurriculumReceiverProgress,
                SoccerRewardKind.CurriculumStableReceiver,
                SoccerRewardKind.CurriculumFindProgress,
                SoccerRewardKind.CurriculumFindHeading,
                SoccerRewardKind.CurriculumFastFind,
                SoccerRewardKind.CurriculumFindTimeoutPenalty,
                SoccerRewardKind.CurriculumFindMissPenalty,
                SoccerRewardKind.CurriculumScoreProgress,
                SoccerRewardKind.CurriculumLongPassGoalBonus,
                SoccerRewardKind.CurriculumOwnGoalPenalty,
                SoccerRewardKind.CurriculumLessonSuccess };
            foreach (SoccerRewardKind kind in System.Enum.GetValues(typeof(SoccerRewardKind)))
            {
                Assert.IsTrue(SoccerRewardEngine.IsRewardAllowed(SoccerCurriculumRewardStage.Disabled, kind));
                if (curriculumKinds.Contains(kind)) continue;
                foreach (var stage in new[] { SoccerCurriculumRewardStage.L0Possession,
                    SoccerCurriculumRewardStage.L1Finish, SoccerCurriculumRewardStage.L1Carry,
                    SoccerCurriculumRewardStage.L2Pass, SoccerCurriculumRewardStage.L2Find,
                    SoccerCurriculumRewardStage.L2Score, SoccerCurriculumRewardStage.L3ProgressivePlay })
                    Assert.IsFalse(SoccerRewardEngine.IsRewardAllowed(stage, kind), $"{stage}/{kind}");
            }
            Assert.IsFalse(SoccerRewardEngine.IsRewardAllowed(
                SoccerCurriculumRewardStage.L1Finish, SoccerRewardKind.CurriculumDribbleProgress));
            Assert.IsTrue(SoccerRewardEngine.IsRewardAllowed(
                SoccerCurriculumRewardStage.L1Carry, SoccerRewardKind.CurriculumDribbleProgress));
            Assert.IsFalse(SoccerRewardEngine.IsRewardAllowed(
                SoccerCurriculumRewardStage.L2Pass, SoccerRewardKind.CurriculumGoal));
            Assert.IsTrue(SoccerRewardEngine.IsRewardAllowed(
                SoccerCurriculumRewardStage.L3ProgressivePlay, SoccerRewardKind.CurriculumReceiverProgress));
            Assert.IsTrue(SoccerRewardEngine.IsRewardAllowed(
                SoccerCurriculumRewardStage.L3ProgressivePlay, SoccerRewardKind.CurriculumStableReceiver));
            Assert.IsFalse(SoccerRewardEngine.IsRewardAllowed(
                SoccerCurriculumRewardStage.L2Pass, SoccerRewardKind.CurriculumReceiverProgress));
            Assert.IsFalse(SoccerRewardEngine.IsRewardAllowed(
                SoccerCurriculumRewardStage.L2Pass, SoccerRewardKind.CurriculumStableReceiver));
            Assert.IsTrue(SoccerRewardEngine.IsRewardAllowed(
                SoccerCurriculumRewardStage.L2Find, SoccerRewardKind.CurriculumFindProgress));
            Assert.IsTrue(SoccerRewardEngine.IsRewardAllowed(
                SoccerCurriculumRewardStage.L2Find, SoccerRewardKind.CurriculumFindHeading));
            Assert.IsTrue(SoccerRewardEngine.IsRewardAllowed(
                SoccerCurriculumRewardStage.L2Find, SoccerRewardKind.CurriculumFastFind));
            Assert.IsTrue(SoccerRewardEngine.IsRewardAllowed(
                SoccerCurriculumRewardStage.L2Find, SoccerRewardKind.CurriculumFindTimeoutPenalty));
            Assert.IsTrue(SoccerRewardEngine.IsRewardAllowed(
                SoccerCurriculumRewardStage.L2Find, SoccerRewardKind.CurriculumFindMissPenalty));
            Assert.IsFalse(SoccerRewardEngine.IsRewardAllowed(
                SoccerCurriculumRewardStage.L2Score, SoccerRewardKind.CurriculumFindTimeoutPenalty));
            Assert.IsFalse(SoccerRewardEngine.IsRewardAllowed(
                SoccerCurriculumRewardStage.L2Score, SoccerRewardKind.CurriculumFindMissPenalty));
            Assert.IsFalse(SoccerRewardEngine.IsRewardAllowed(
                SoccerCurriculumRewardStage.L2Find, SoccerRewardKind.CurriculumGoal));
            Assert.IsTrue(SoccerRewardEngine.IsRewardAllowed(
                SoccerCurriculumRewardStage.L2Score, SoccerRewardKind.CurriculumGoal));
            Assert.IsTrue(SoccerRewardEngine.IsRewardAllowed(
                SoccerCurriculumRewardStage.L2Score, SoccerRewardKind.CurriculumOwnGoalPenalty));
        }

        [Test]
        public void L2FindScoreSpawnExcludesWallsAndRedGoal()
        {
            Assert.IsTrue(SoccerCurriculumController.IsValidL2FindScoreBallSpawn(
                UnityEngine.Vector3.zero, null));
            Assert.IsFalse(SoccerCurriculumController.IsValidL2FindScoreBallSpawn(
                new UnityEngine.Vector3(SoccerArenaGeometry.StadiumHalfLength - .99f, .5f, 0f), null));
            Assert.IsFalse(SoccerCurriculumController.IsValidL2FindScoreBallSpawn(
                new UnityEngine.Vector3(-SoccerArenaGeometry.StadiumHalfLength + 2f, .5f, 0f), null));
            Assert.IsTrue(SoccerCurriculumController.IsValidL2FindScoreBallSpawn(
                new UnityEngine.Vector3(-SoccerArenaGeometry.StadiumHalfLength + 5f, .5f, 0f), null));
        }

        [Test]
        public void L2FindSelectsTwoNearestActivePursuers()
        {
            var objects = new[]
            {
                new UnityEngine.GameObject("Far"),
                new UnityEngine.GameObject("Nearest"),
                new UnityEngine.GameObject("SecondNearest"),
                new UnityEngine.GameObject("InactiveNearest")
            };
            try
            {
                var agents = objects.Select(item => item.AddComponent<AgentSoccer>()).ToArray();
                objects[0].transform.position = new UnityEngine.Vector3(10f, 0f, 0f);
                objects[1].transform.position = new UnityEngine.Vector3(2f, 0f, 0f);
                objects[2].transform.position = new UnityEngine.Vector3(5f, 0f, 0f);
                objects[3].transform.position = new UnityEngine.Vector3(1f, 0f, 0f);
                objects[3].SetActive(false);

                var selected = SoccerCurriculumController.SelectL2FindPursuers(
                    agents, UnityEngine.Vector3.zero);

                Assert.AreEqual(2, SoccerCurriculumController.L2FindPursuerCount);
                Assert.AreEqual(0.5f,
                    SoccerCurriculumController.L2FindPursuerIndividualRewardShare,
                    0.0001f);
                CollectionAssert.AreEqual(new[] { agents[1], agents[2] }, selected);
            }
            finally
            {
                foreach (var item in objects)
                    UnityEngine.Object.DestroyImmediate(item);
            }
        }

        [Test]
        public void L2FindPotentialRewardIsBoundedAndCannotFarmRecoveredDistance()
        {
            Assert.AreEqual(0.5f, SoccerCurriculumController.L2FindProgressRewardLimit, 0.0001f);
            Assert.AreEqual(0.1f,
                SoccerCurriculumController.CalculateL2FindPotentialReward(20f, 10f), 0.0001f);
            Assert.AreEqual(-0.1f,
                SoccerCurriculumController.CalculateL2FindPotentialReward(10f, 20f), 0.0001f);
            Assert.AreEqual(0f,
                SoccerCurriculumController.CalculateL2FindPotentialReward(20f, 20f), 0.0001f);
            Assert.AreEqual(SoccerCurriculumController.L2FindProgressRewardLimit,
                SoccerCurriculumController.CalculateL2FindPotentialReward(50f, 0f), 0.0001f);
            Assert.AreEqual(-SoccerCurriculumController.L2FindProgressRewardLimit,
                SoccerCurriculumController.CalculateL2FindPotentialReward(0f, 50f), 0.0001f);

            Assert.AreEqual(0.5f, SoccerCurriculumController.L2FindTimeoutPenalty, 0.0001f);
            Assert.AreEqual(0.2f, SoccerCurriculumController.L2FindMissPenalty, 0.0001f);
            Assert.IsFalse(SoccerCurriculumController.HasL2FindNearMiss(4.01f, 10f));
            Assert.IsFalse(SoccerCurriculumController.HasL2FindNearMiss(3f, 4.99f));
            Assert.IsTrue(SoccerCurriculumController.HasL2FindNearMiss(3f, 5f));
            Assert.IsFalse(SoccerCurriculumController.HasL2FindNearMiss(float.NaN, 5f));
            Assert.AreEqual(3f, SoccerCurriculumController.L2FindNearSpawnMinimumDistance, 0.0001f);
            Assert.AreEqual(12f, SoccerCurriculumController.L2FindNearSpawnMaximumDistance, 0.0001f);
            Assert.AreEqual("soccer_l2_find_near_spawn_probability",
                SoccerCurriculumController.L2FindNearSpawnProbabilityParameter);
            Assert.AreEqual("soccer_l2_find_maximum_initial_heading_error",
                SoccerCurriculumController.L2FindMaximumInitialHeadingErrorParameter);
        }

        [Test]
        public void L2FindHeadingAndFastTouchRewardsFavorEfficientSearchWithoutFarming()
        {
            Assert.AreEqual(0.09f,
                SoccerCurriculumController.CalculateL2FindHeadingPotentialReward(120f, 30f), 0.0001f);
            Assert.AreEqual(-0.09f,
                SoccerCurriculumController.CalculateL2FindHeadingPotentialReward(30f, 120f), 0.0001f);
            Assert.AreEqual(SoccerCurriculumController.L2FindHeadingRewardLimit,
                SoccerCurriculumController.CalculateL2FindHeadingPotentialReward(180f, 0f), 0.0001f);
            Assert.AreEqual(SoccerCurriculumController.L2FindFastTouchRewardLimit,
                SoccerCurriculumController.CalculateL2FindFastTouchReward(45f, 5f), 0.0001f);
            Assert.AreEqual(0.2f,
                SoccerCurriculumController.CalculateL2FindFastTouchReward(45f, 10f), 0.0001f);
            Assert.AreEqual(0f,
                SoccerCurriculumController.CalculateL2FindFastTouchReward(45f, 15f), 0.0001f);
            Assert.IsTrue(SoccerCurriculumController.IsL2FindHeadingWithinLimit(
                UnityEngine.Vector3.forward,
                UnityEngine.Quaternion.Euler(0f, 60f, 0f) * UnityEngine.Vector3.forward,
                60f));
            Assert.IsFalse(SoccerCurriculumController.IsL2FindHeadingWithinLimit(
                UnityEngine.Vector3.forward,
                UnityEngine.Quaternion.Euler(0f, 61f, 0f) * UnityEngine.Vector3.forward,
                60f));
            Assert.AreEqual("Front", SoccerCurriculumController.GetL2FindHeadingBand(60f));
            Assert.AreEqual("SideBlind", SoccerCurriculumController.GetL2FindHeadingBand(116f));
            Assert.AreEqual("Rear", SoccerCurriculumController.GetL2FindHeadingBand(136f));
            Assert.AreEqual("Invalid", SoccerCurriculumController.GetL2FindHeadingBand(float.NaN));
        }

        [Test]
        public void L2ScoreGoalPriorityRewardsFavorActualGoalOverBlindShotAttempt()
        {
            Assert.AreEqual(0.02f, SoccerCurriculumController.L2ScoreShotAttemptReward, 0.0001f);
            Assert.AreEqual(0.2f, SoccerCurriculumController.L2ScoreShotOnTargetReward, 0.0001f);
            Assert.AreEqual(1f, SoccerCurriculumController.L2ScoreGoalReward, 0.0001f);
            Assert.AreEqual(1f, SoccerCurriculumController.L2ScoreOwnGoalPenalty, 0.0001f);
            Assert.Greater(SoccerCurriculumController.L2ScoreGoalReward,
                SoccerCurriculumController.L2ScoreShotAttemptReward
                + SoccerCurriculumController.L2ScoreShotOnTargetReward
                + SoccerCurriculumController.L2ScoreProgressRewardLimit);
        }

        [Test]
        public void L2FindDiagnosticBandsSeparateDistanceAndFieldRegions()
        {
            Assert.AreEqual("Near", SoccerCurriculumController.GetL2FindDistanceBand(20f));
            Assert.AreEqual("Mid", SoccerCurriculumController.GetL2FindDistanceBand(40f));
            Assert.AreEqual("Far", SoccerCurriculumController.GetL2FindDistanceBand(40.01f));
            Assert.AreEqual("Invalid", SoccerCurriculumController.GetL2FindDistanceBand(float.NaN));
            Assert.AreEqual("RedThird", SoccerCurriculumController.GetL2FindLongitudinalBand(-20.01f));
            Assert.AreEqual("MiddleThird", SoccerCurriculumController.GetL2FindLongitudinalBand(20f));
            Assert.AreEqual("NavyThird", SoccerCurriculumController.GetL2FindLongitudinalBand(20.01f));
            Assert.AreEqual("Central", SoccerCurriculumController.GetL2FindLateralBand(-20f));
            Assert.AreEqual("Wide", SoccerCurriculumController.GetL2FindLateralBand(20.01f));
        }

        [Test]
        public void L2ScoreLongPassRequiresDistanceProgressLaneAndNoEasyShot()
        {
            var ball = new UnityEngine.Vector3(-20f, .5f, 0f);
            var passer = new UnityEngine.Vector3(-20f, .5f, 0f);
            var receiver = new UnityEngine.Vector3(-8f, .5f, 0f);
            var goal = new UnityEngine.Vector3(SoccerArenaGeometry.StadiumHalfLength, .5f, 0f);
            Assert.IsTrue(SoccerCurriculumController.IsEligibleL2ScoreLongPassAtStrike(
                passer, receiver, ball, UnityEngine.Vector3.right, goal));
            Assert.IsFalse(SoccerCurriculumController.IsEligibleL2ScoreLongPassAtStrike(
                passer, new UnityEngine.Vector3(-11f, .5f, 0f), ball, UnityEngine.Vector3.right, goal));
            Assert.IsFalse(SoccerCurriculumController.IsEligibleL2ScoreLongPassAtStrike(
                passer, new UnityEngine.Vector3(-8f, .5f, 5f), ball, UnityEngine.Vector3.right, goal));
            Assert.IsFalse(SoccerCurriculumController.IsEligibleL2ScoreLongPassAtStrike(
                new UnityEngine.Vector3(40f, .5f, 0f), new UnityEngine.Vector3(52f, .5f, 0f),
                new UnityEngine.Vector3(40f, .5f, 0f), UnityEngine.Vector3.right, goal));
        }

        [Test]
        public void L3ContinuationRequiresStableSameReceiverAndForwardBallProgress()
        {
            var start = new UnityEngine.Vector3(1f, 0f, 2f);
            var finish = new UnityEngine.Vector3(4.1f, 0f, 2f);
            Assert.IsTrue(SoccerCurriculumController.IsSuccessfulL3Continuation(
                true, true, false, start, finish, .35f, 2f, 1f));
            Assert.IsFalse(SoccerCurriculumController.IsSuccessfulL3Continuation(
                false, true, false, start, finish, .35f, 2f, 1f));
            Assert.IsFalse(SoccerCurriculumController.IsSuccessfulL3Continuation(
                true, true, true, start, finish, .35f, 2f, 1f));
            Assert.IsFalse(SoccerCurriculumController.IsSuccessfulL3Continuation(
                true, true, false, start, new UnityEngine.Vector3(-3f, 0f, 2f), .35f, 2f, 1f));
            Assert.IsFalse(SoccerCurriculumController.IsSuccessfulL3Continuation(
                true, true, false, start, finish, .2f, 2f, 1f));
        }

        [Test]
        public void L3ReceptionRecoveryIsBoundedAndEndsAfterStableControl()
        {
            const float controlledBallDistance = 2.4f;
            var outsideControlDistance = controlledBallDistance + .1f;
            Assert.IsTrue(SoccerCurriculumController.CanRecoverL3Reception(
                false, SoccerCurriculumController.L3MaximumReceptionRecoverySeconds, outsideControlDistance));
            Assert.IsFalse(SoccerCurriculumController.CanRecoverL3Reception(
                true, 1f, outsideControlDistance));
            Assert.IsFalse(SoccerCurriculumController.CanRecoverL3Reception(
                false, SoccerCurriculumController.L3MaximumReceptionRecoverySeconds + .01f, outsideControlDistance));
            Assert.IsFalse(SoccerCurriculumController.CanRecoverL3Reception(
                false, 1f, controlledBallDistance));
        }

        [Test]
        public void L3ProgressRewardStartsOnlyAfterStableReceiverControl()
        {
            Assert.AreEqual(0f, SoccerCurriculumController.CalculateL3ProgressRewardIncrement(
                false, 0f, 1f));
            Assert.AreEqual(.05f, SoccerCurriculumController.CalculateL3ProgressRewardIncrement(
                true, 0f, 1f), .0001f);
            Assert.AreEqual(0f, SoccerCurriculumController.CalculateL3ProgressRewardIncrement(
                true, 1f, .5f));
            Assert.AreEqual(.1f, SoccerCurriculumController.CalculateL3ProgressRewardIncrement(
                true, 1f, 3f), .0001f);
        }

        [Test]
        public void CurriculumDeliveryGuardsRejectRewardsBeforeProfileOrAgentLookup()
        {
            var root = new UnityEngine.GameObject("DeliveryGuardTest");
            try
            {
                var engine = root.AddComponent<SoccerRewardEngine>();
                engine.ConfigureCurriculumRewardStage(SoccerCurriculumRewardStage.L1Finish);
                var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
                foreach (var kind in new[] { SoccerRewardKind.AttackSuccess, SoccerRewardKind.PassSuccess,
                    SoccerRewardKind.ControlledCarry, SoccerRewardKind.GoalResult, SoccerRewardKind.MatchResult })
                {
                    Assert.AreEqual(0f, typeof(SoccerRewardEngine).GetMethod("ApplyGroupReward", flags)
                        .Invoke(engine, new object[] { Team.Red, kind, 1f }));
                    Assert.AreEqual(0f, typeof(SoccerRewardEngine).GetMethod("ApplyExplicitIndividualReward", flags)
                        .Invoke(engine, new object[] { null, kind, 1f }));
                }
                typeof(SoccerRewardEngine).GetMethod("AwardBehaviorPenalty", flags)
                    .Invoke(engine, new object[] { null, SoccerRewardKind.WastefulStrongKick, true });
                Assert.AreEqual(0f, engine.GetCumulativeReward(Team.Red));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void L1ShotTargetRejectsWideAndBackwardKicks()
        {
            var ball = new UnityEngine.Vector3(45f, 0.5f, 0f);
            Assert.IsTrue(SoccerCurriculumController.IsShotOnTarget(
                ball,
                UnityEngine.Vector3.right,
                Team.Red,
                null));
            Assert.IsFalse(SoccerCurriculumController.IsShotOnTarget(
                ball,
                new UnityEngine.Vector3(1f, 0f, 1f),
                Team.Red,
                null));
            Assert.IsFalse(SoccerCurriculumController.IsShotOnTarget(
                ball,
                UnityEngine.Vector3.left,
                Team.Red,
                null));
        }

        [Test]
        public void L1NoShotFinishLineRejectsPushInForEitherAttackDirection()
        {
            Assert.IsFalse(SoccerCurriculumController.CrossedNoShotFinishLine(
                new UnityEngine.Vector3(57.9f, 0.5f, 0f), Team.Red, null));
            Assert.IsTrue(SoccerCurriculumController.CrossedNoShotFinishLine(
                new UnityEngine.Vector3(58f, 0.5f, 0f), Team.Red, null));
            Assert.IsTrue(SoccerCurriculumController.CrossedNoShotFinishLine(
                new UnityEngine.Vector3(-58f, 0.5f, 0f), Team.Navy, null));
        }

        [Test]
        public void L1ScoringCreditRequiresARecentUnretouchedExplicitShot()
        {
            Assert.IsTrue(SoccerCurriculumController.IsEligibleScoringStrike(true, false, 1f));
            Assert.IsFalse(SoccerCurriculumController.IsEligibleScoringStrike(false, false, 1f));
            Assert.IsFalse(SoccerCurriculumController.IsEligibleScoringStrike(true, true, 1f));
            Assert.IsFalse(SoccerCurriculumController.IsEligibleScoringStrike(true, false, 8.01f));
        }

        [Test]
        public void AlignmentRewardIsBoundedAndCannotPayForWorseningOrHoldingAim()
        {
            Assert.AreEqual(0f, SoccerCurriculumController.CalculateShotAlignmentReward(float.NaN, 30f, 0f));
            Assert.AreEqual(0f, SoccerCurriculumController.CalculateShotAlignmentReward(30f, 30f, 0f));
            Assert.AreEqual(0f, SoccerCurriculumController.CalculateShotAlignmentReward(30f, 40f, 0f));
            Assert.AreEqual(0.02f, SoccerCurriculumController.CalculateShotAlignmentReward(30f, 20f, 0f), 0.0001f);
            Assert.AreEqual(0.08f, SoccerCurriculumController.CalculateShotAlignmentReward(90f, 0f, 0f), 0.0001f);
            Assert.AreEqual(0.01f, SoccerCurriculumController.CalculateShotAlignmentReward(90f, 0f, 0.07f), 0.0001f);
            Assert.AreEqual(0f, SoccerCurriculumController.CalculateShotAlignmentReward(90f, 0f, 0.08f));
            Assert.IsTrue(SoccerRewardEngine.IsRewardAllowed(SoccerCurriculumRewardStage.L1Finish, SoccerRewardKind.CurriculumShotAlignment));
            foreach (var stage in new[] { SoccerCurriculumRewardStage.L0Possession, SoccerCurriculumRewardStage.L1Carry, SoccerCurriculumRewardStage.L2Pass })
                Assert.IsFalse(SoccerRewardEngine.IsRewardAllowed(stage, SoccerRewardKind.CurriculumShotAlignment));
        }

        [Test]
        public void AlignmentUsesUnchangedRealContactDirectionForBothTeams()
        {
            var ball = new UnityEngine.Vector3(45f, 0.5f, 0f);
            Assert.AreEqual(0f, SoccerCurriculumController.GetShotAlignmentError(ball, UnityEngine.Vector3.right, Team.Red, null), 0.001f);
            Assert.AreEqual(180f, SoccerCurriculumController.GetShotAlignmentError(ball, UnityEngine.Vector3.left, Team.Red, null), 0.001f);
            Assert.AreEqual(0f, SoccerCurriculumController.GetShotAlignmentError(-ball, UnityEngine.Vector3.left, Team.Navy, null), 0.001f);
            var forward = UnityEngine.Vector3.forward;
            foreach (var explicitStrike in new[] { false, true })
            {
                var expected = UnityEngine.Vector3.Slerp(UnityEngine.Vector3.right, forward, explicitStrike ? .65f : .25f).normalized;
                var actual = AgentSoccer.CalculateBallPushDirection(UnityEngine.Vector3.zero, forward, UnityEngine.Vector3.right, explicitStrike);
                Assert.Less(UnityEngine.Vector3.Distance(expected, actual), 0.00001f);
            }
        }

        [Test]
        public void NeuralShotAdvisorOnlyActsForConfirmedCloseGoalOpportunities()
        {
            var ball = new UnityEngine.Vector3(52f, 0.5f, 0f);
            var agent = new UnityEngine.Vector3(51f, 0.5f, 0f);
            var action = SoccerNeuralKickAdvisor.ResolveShotAction(
                Team.Red, agent, UnityEngine.Vector3.right, ball, 0, true, true, null, out var advice);
            Assert.AreEqual(1, action);
            Assert.AreEqual(SoccerNeuralKickAdvice.ShotRecommended, advice);

            action = SoccerNeuralKickAdvisor.ResolveShotAction(
                Team.Red, agent, UnityEngine.Vector3.forward, ball, 1, true, true, null, out advice);
            Assert.AreEqual(0, action);
            Assert.AreEqual(SoccerNeuralKickAdvice.OffTargetShotDeferred, advice);

            action = SoccerNeuralKickAdvisor.ResolveShotAction(
                Team.Red, agent, UnityEngine.Vector3.right, ball, 2, true, true, null, out advice);
            Assert.AreEqual(2, action, "An already valid Neural kick keeps its requested power.");
            Assert.AreEqual(SoccerNeuralKickAdvice.None, advice);

            Assert.IsFalse(SoccerNeuralKickAdvisor.PredictsScoringLane(
                Team.Red,
                ball,
                new UnityEngine.Vector3(1f, 0f, 1f),
                null),
                "The advisor must use the curriculum's inside-post target, not the wider safety margin.");

            action = SoccerNeuralKickAdvisor.ResolveShotAction(
                Team.Red, agent, UnityEngine.Vector3.right, ball, 1, false, true, null, out advice);
            Assert.AreEqual(1, action, "Unconfirmed contact must not receive code assistance.");
            Assert.AreEqual(SoccerNeuralKickAdvice.None, advice);

            ball.x = 37f;
            agent.x = 36f;
            action = SoccerNeuralKickAdvisor.ResolveShotAction(
                Team.Red, agent, UnityEngine.Vector3.forward, ball, 1, true, true, null, out advice);
            Assert.AreEqual(1, action, "Play outside the 24m goal window remains policy-controlled.");
            Assert.AreEqual(SoccerNeuralKickAdvice.None, advice);

            ball.x = -52f;
            agent.x = -51f;
            action = SoccerNeuralKickAdvisor.ResolveShotAction(
                Team.Navy, agent, UnityEngine.Vector3.left, ball, 0, true, true, null, out advice);
            Assert.AreEqual(1, action, "The same shared skill must work for Navy.");
            Assert.AreEqual(SoccerNeuralKickAdvice.ShotRecommended, advice);
        }

        [Test]
        public void PreparatorySpawnCannotNarrowFinalCarryTask()
        {
            Assert.AreEqual(0f, SoccerCurriculumController.GetL1SpawnDifficulty(SoccerL1Phase.ShortRangeFinish, 0f));
            Assert.AreEqual(0.5f, SoccerCurriculumController.GetL1SpawnDifficulty(SoccerL1Phase.ShortRangeFinish, 0.5f));
            Assert.AreEqual(1f, SoccerCurriculumController.GetL1SpawnDifficulty(SoccerL1Phase.ShortRangeFinish, 1f));
            Assert.AreEqual(0f, SoccerCurriculumController.GetL1SpawnDifficulty(SoccerL1Phase.ShortRangeFinish, -1f));
            Assert.AreEqual(1f, SoccerCurriculumController.GetL1SpawnDifficulty(SoccerL1Phase.ShortRangeFinish, 2f));
            Assert.AreEqual(1f, SoccerCurriculumController.GetL1SpawnDifficulty(SoccerL1Phase.ShortRangeFinish, float.NaN));
            Assert.AreEqual(1f, SoccerCurriculumController.GetL1SpawnDifficulty(SoccerL1Phase.ShortRangeFinish, float.PositiveInfinity));
            Assert.AreEqual(1f, SoccerCurriculumController.GetL1SpawnDifficulty(SoccerL1Phase.CarryAndScore, 0f));
        }

        [Test]
        public void FinalL1MasksKicksUntilEightMeterCarryWhileShortFinishRemainsUnlocked()
        {
            Assert.IsTrue(SoccerCurriculumController.IsL1KickUnlocked(SoccerL1Phase.ShortRangeFinish, 0f));
            Assert.IsFalse(SoccerCurriculumController.IsL1KickUnlocked(SoccerL1Phase.CarryAndScore, 7.998f));
            Assert.IsTrue(SoccerCurriculumController.IsL1KickUnlocked(SoccerL1Phase.CarryAndScore, 7.999f));
            Assert.IsFalse(SoccerCurriculumController.IsL1KickUnlocked(SoccerL1Phase.CarryAndScore, float.NaN));
            Assert.IsFalse(SoccerCurriculumController.IsL1KickUnlocked(SoccerL1Phase.CarryAndScore, float.PositiveInfinity));
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
