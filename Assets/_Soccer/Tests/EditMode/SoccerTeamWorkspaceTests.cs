using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MachineLearning.Soccer.Editor;
using MachineLearning.Soccer.Teams.Rule;
using NUnit.Framework;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Tests
{
    public sealed class SoccerTeamWorkspaceTests
    {
        const string Root = "Assets/_Soccer";
        const string AttackRoot = Root + "/Teams/Attack_KMW";
        const string DefenseRoot = Root + "/Teams/Defense_PJH";
        const string PressRoot = Root + "/Teams/Press_KMG";
        const string RuleRoot = Root + "/Teams/Rule_PHC";

        static readonly string[] WorkspacePrefabPaths =
        {
            Root + "/Core/Prefabs/StadiumEnvironment_Base.prefab",
            AttackRoot + "/Prefabs/StadiumEnvironment_Attack.prefab",
            DefenseRoot + "/Prefabs/StadiumEnvironment_Defense.prefab",
            PressRoot + "/Prefabs/StadiumEnvironment_Press.prefab",
            RuleRoot + "/Prefabs/StadiumEnvironment_Rule.prefab"
        };

        static readonly string[] CommonArenaPrefabPaths = WorkspacePrefabPaths;

        static readonly string[] WorkspaceScenePaths =
        {
            Root + "/Core/Scenes/Stadium4v4_Base.unity",
            AttackRoot + "/Scenes/Stadium4v4_Attack.unity",
            DefenseRoot + "/Scenes/Stadium4v4_Defense.unity",
            PressRoot + "/Scenes/Stadium4v4_Press.unity",
            RuleRoot + "/Scenes/Stadium4v4_Rule.unity"
        };

        [Test]
        public void GeneratedPrefabUsesFourPlayerRolesAndV2PolicyContract()
        {
            Assert.AreEqual(2000f, AgentSoccer.ControlledKickPower);
            Assert.AreEqual(5000f, AgentSoccer.StrongKickPower);
            foreach (var prefabPath in WorkspacePrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.IsNotNull(prefab, prefabPath);
                var setup = prefab.GetComponent<SoccerMatchSetup>();
                Assert.IsNotNull(setup, prefabPath);
                var environment = prefab.GetComponent<SoccerEnvController>();
                Assert.AreEqual(2000f, environment.ControlledKickPower, 0.000001f, prefabPath);
                Assert.AreEqual(5000f, environment.StrongKickPower, 0.000001f, prefabPath);
                Assert.IsNotNull(environment.ArenaGeometry, prefabPath);
                var agents = prefab.GetComponentsInChildren<AgentSoccer>(true);
                Assert.AreEqual(8, agents.Length, prefabPath);
                foreach (var team in new[] { Team.Red, Team.Navy })
                {
                    var teamAgents = agents.Where(agent => agent.Team == team).ToArray();
                    Assert.AreEqual(1, teamAgents.Count(agent => agent.PositionRole == AgentSoccer.Position.DefenderKeeper));
                    Assert.AreEqual(2, teamAgents.Count(agent => agent.PositionRole == AgentSoccer.Position.Midfielder));
                    Assert.AreEqual(1, teamAgents.Count(agent => agent.PositionRole == AgentSoccer.Position.Striker));
                }

                foreach (var agent in agents)
                {
                    var behavior = agent.GetComponent<BehaviorParameters>();
                    Assert.AreEqual((int)agent.Team, behavior.TeamId, $"팀 직렬화 불일치: {prefabPath}/{agent.name}");
                    CollectionAssert.AreEqual(new[] { 3, 3, 3, 3 }, behavior.BrainParameters.ActionSpec.BranchSizes);
                    Assert.AreEqual(AgentSoccer.VectorObservationSize, behavior.BrainParameters.VectorObservationSize);
                    Assert.IsNull(behavior.Model, "모델은 선수별 슬롯이 아니라 환경의 단일 모델 슬롯으로 지정 필요");
                    var expectedBehaviorType = setup.IsTrainable(agent.Team)
                        ? BehaviorType.Default
                        : BehaviorType.HeuristicOnly;
                    Assert.AreEqual(expectedBehaviorType, behavior.BehaviorType,
                        $"비학습 팀의 Trainer 사전 등록 차단 필요: {prefabPath}/{agent.name}");
                }

                var goalRenderers = prefab.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(renderer => renderer.name.StartsWith("SM_S_Gate", StringComparison.Ordinal))
                    .OrderBy(renderer => renderer.bounds.center.x)
                    .ToArray();
                Assert.AreEqual(2, goalRenderers.Length, prefabPath);
                Assert.IsTrue(goalRenderers[0].CompareTag("redGoal"));
                Assert.IsTrue(goalRenderers[1].CompareTag("navyGoal"));
                Assert.IsTrue(goalRenderers.All(renderer => renderer.sharedMaterial != null
                    && renderer.sharedMaterial.name.StartsWith("Stadium", StringComparison.Ordinal)));
                Assert.IsTrue(goalRenderers.All(renderer =>
                    (GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.BatchingStatic) == 0));
            }
        }

        [Test]
        public void RedAndNavyVisualIdentityIsConsistentAcrossEveryActiveStadiumAndHud()
        {
            Assert.AreEqual(0, (int)Team.Red, "기존 TeamId 0 계약은 Red가 유지해야 함");
            Assert.AreEqual(1, (int)Team.Navy, "기존 TeamId 1 계약은 Navy가 유지해야 함");

            const string redPlayerPath = Root + "/Materials/AgentRed.mat";
            const string navyPlayerPath = Root + "/Materials/AgentNavy.mat";
            const string redKickPlatePath = Root + "/Materials/KickPlateRed.mat";
            const string navyKickPlatePath = Root + "/Materials/KickPlateNavy.mat";
            const string redGoalPath = Root + "/Core/Materials/StadiumRedGoal.mat";
            const string navyGoalPath = Root + "/Core/Materials/StadiumNavyGoal.mat";
            AssertMaterial(redPlayerPath, "AgentRed", SoccerTeamVisuals.RedPlayerColor);
            AssertMaterial(navyPlayerPath, "AgentNavy", SoccerTeamVisuals.NavyPlayerColor);
            AssertMaterial(redKickPlatePath, "KickPlateRed", SoccerTeamVisuals.RedKickPlateColor);
            AssertMaterial(navyKickPlatePath, "KickPlateNavy", SoccerTeamVisuals.NavyKickPlateColor);
            AssertMaterial(redGoalPath, "StadiumRedGoal", SoccerTeamVisuals.RedGoalColor);
            AssertMaterial(navyGoalPath, "StadiumNavyGoal", SoccerTeamVisuals.NavyGoalColor);

            foreach (var prefabPath in WorkspacePrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.IsNotNull(prefab, prefabPath);
                Assert.IsFalse(prefab.GetComponentsInChildren<Transform>(true).Any(item =>
                    item.name.Contains("Blue", StringComparison.Ordinal)
                    || item.name.Contains("Purple", StringComparison.Ordinal)), prefabPath);

                var ball = prefab.GetComponentsInChildren<SoccerBallController>(true).Single();
                Assert.AreEqual(SoccerTeamVisuals.RedGoalTag, ball.redGoalTag, prefabPath);
                Assert.AreEqual(SoccerTeamVisuals.NavyGoalTag, ball.navyGoalTag, prefabPath);

                foreach (var agent in prefab.GetComponentsInChildren<AgentSoccer>(true))
                {
                    var isRed = agent.Team == Team.Red;
                    var teamName = isRed ? "Red" : "Navy";
                    var playerPath = isRed ? redPlayerPath : navyPlayerPath;
                    var kickPlatePath = isRed ? redKickPlatePath : navyKickPlatePath;
                    Assert.IsTrue(agent.name.StartsWith(teamName + "Player", StringComparison.Ordinal),
                        $"{prefabPath}/{agent.name}");
                    Assert.IsTrue(agent.CompareTag(SoccerTeamVisuals.AgentTag(agent.Team)),
                        $"{prefabPath}/{agent.name}");

                    var body = agent.GetComponentsInChildren<Renderer>(true).Single(renderer =>
                        renderer.name == "AgentCube_" + teamName);
                    Assert.AreEqual(playerPath, AssetDatabase.GetAssetPath(body.sharedMaterial),
                        $"{prefabPath}/{agent.name}");
                    var plate = agent.GetComponentInChildren<SoccerKickPlate>(true);
                    Assert.IsNotNull(plate, $"{prefabPath}/{agent.name}");
                    Assert.IsTrue(plate.GetComponentsInChildren<Renderer>(true).All(renderer =>
                        AssetDatabase.GetAssetPath(renderer.sharedMaterial) == kickPlatePath),
                        $"{prefabPath}/{agent.name}");
                    Assert.IsTrue(plate.GetComponentsInChildren<Transform>(true).All(item =>
                        item.CompareTag(SoccerTeamVisuals.AgentTag(agent.Team))),
                        $"{prefabPath}/{agent.name}");

                    var expectedTags = isRed
                        ? new[] { "ball", "redGoal", "navyGoal", "wall", "redAgent", "navyAgent" }
                        : new[] { "ball", "navyGoal", "redGoal", "wall", "navyAgent", "redAgent" };
                    foreach (var sensor in agent.GetComponentsInChildren<RayPerceptionSensorComponent3D>(true))
                    {
                        StringAssert.StartsWith(teamName + "RayPerceptionSensor", sensor.SensorName,
                            $"{prefabPath}/{agent.name}");
                        CollectionAssert.AreEqual(expectedTags, sensor.DetectableTags,
                            $"{prefabPath}/{agent.name}/{sensor.SensorName}");
                    }
                }

                var goals = prefab.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(renderer => renderer.name.StartsWith("SM_S_Gate", StringComparison.Ordinal))
                    .OrderBy(renderer => renderer.bounds.center.x)
                    .ToArray();
                Assert.AreEqual(redGoalPath, AssetDatabase.GetAssetPath(goals[0].sharedMaterial), prefabPath);
                Assert.AreEqual(navyGoalPath, AssetDatabase.GetAssetPath(goals[1].sharedMaterial), prefabPath);
            }

            var uxmlPath = Root + "/UI/SoccerHud.uxml";
            var uxmlText = File.ReadAllText(uxmlPath);
            StringAssert.DoesNotContain("BLUE", uxmlText);
            StringAssert.DoesNotContain("PURPLE", uxmlText);
            StringAssert.Contains("red-tactic-label", uxmlText);
            StringAssert.Contains("navy-tactic-label", uxmlText);
            StringAssert.Contains("red-reward-label", uxmlText);
            StringAssert.Contains("navy-reward-label", uxmlText);
            var styleText = File.ReadAllText(Root + "/UI/SoccerHud.uss");
            StringAssert.Contains(".red-value", styleText);
            StringAssert.Contains(".navy-value", styleText);
            StringAssert.DoesNotContain(".blue-value", styleText);
            StringAssert.DoesNotContain(".purple-value", styleText);
        }

        [Test]
        public void CommonTemplateAndFiveWorkspacePrefabsKeepTheSameArenaContract()
        {
            foreach (var prefabPath in CommonArenaPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.IsNotNull(prefab, prefabPath);
                Assert.AreEqual(300f, prefab.GetComponent<SoccerEnvController>().matchDurationSeconds,
                    $"5분 경기 설정 필요: {prefabPath}");
            }

            var snapshots = CommonArenaPrefabPaths
                .Select(SoccerProjectBuilder.BuildCommonArenaSnapshotForTests)
                .ToArray();
            Assert.AreEqual(1, snapshots.Distinct().Count(),
                "공용 템플릿과 다섯 환경 프리팹의 공통 경기장 계약 일치 필요");

            var discoveredWorkspacePrefabs = AssetDatabase.FindAssets("t:Prefab", new[]
                {
                    Root + "/Core/Prefabs",
                    AttackRoot + "/Prefabs",
                    DefenseRoot + "/Prefabs",
                    PressRoot + "/Prefabs",
                    RuleRoot + "/Prefabs"
                })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileName(path).StartsWith("StadiumEnvironment_", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            CollectionAssert.AreEquivalent(WorkspacePrefabPaths, discoveredWorkspacePrefabs);
        }

        [Test]
        public void AllWorkspacesUseTheCanonicalStadiumGeometryBallWallsAndSensorContract()
        {
            foreach (var prefabPath in CommonArenaPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.IsNotNull(prefab, prefabPath);

                var environment = prefab.GetComponent<SoccerEnvController>();
                var arena = environment.ArenaGeometry;
                Assert.IsNotNull(arena, prefabPath);
                Assert.AreEqual(SoccerArenaGeometry.StadiumHalfLength, arena.HalfLength, 0.001f, prefabPath);
                Assert.AreEqual(SoccerArenaGeometry.StadiumHalfWidth, arena.HalfWidth, 0.01f, prefabPath);
                Assert.AreEqual(SoccerArenaGeometry.StadiumGoalHalfWidth, arena.GoalHalfWidth, 0.01f, prefabPath);
                Assert.AreEqual(2f, arena.GoalModelWidthMultiplier, 0.001f, prefabPath);

                var goals = prefab.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(renderer => renderer.name.StartsWith("SM_S_Gate", StringComparison.Ordinal))
                    .ToArray();
                Assert.AreEqual(2, goals.Length, prefabPath);

                var ball = prefab.GetComponentsInChildren<SoccerBallController>(true).Single();
                Assert.AreEqual(0.012705f, ball.transform.localScale.x, 0.000001f, prefabPath);
                Assert.AreEqual(0.012705f, ball.transform.localScale.y, 0.000001f, prefabPath);
                Assert.AreEqual(0.012705f, ball.transform.localScale.z, 0.000001f, prefabPath);
                Assert.IsTrue(ball.EnforcesStadiumPlanarMotion, prefabPath);
                var ballCollider = ball.GetComponent<SphereCollider>();
                Assert.IsNotNull(ballCollider, prefabPath);
                var ballRadius = ballCollider.radius * ball.transform.lossyScale.x;
                Assert.AreEqual(ballRadius, ball.LockedCenterHeight, 0.001f, prefabPath);
                Assert.AreEqual(ballRadius, ball.transform.localPosition.y, 0.001f, prefabPath);
                var ballBody = ball.GetComponent<Rigidbody>();
                Assert.AreEqual(3f, ballBody.mass, 0.000001f, prefabPath);
                Assert.AreEqual(1f, ballBody.linearDamping, 0.000001f, prefabPath);
                Assert.AreEqual(1f, ballBody.angularDamping, 0.000001f, prefabPath);
                Assert.IsTrue((ballBody.constraints & RigidbodyConstraints.FreezePositionY) != 0, prefabPath);
                var walls = prefab.GetComponentsInChildren<BoxCollider>(true)
                    .Where(collider => collider.CompareTag("wall"))
                    .ToArray();
                Assert.AreEqual(18, walls.Length, prefabPath);
                Assert.IsTrue(walls.All(wall => !wall.isTrigger), prefabPath);

                foreach (var agent in prefab.GetComponentsInChildren<AgentSoccer>(true))
                {
                    var sensors = agent.GetComponentsInChildren<RayPerceptionSensorComponent3D>(true);
                    Assert.AreEqual(2, sensors.Length,
                        $"전방 1개와 후방 1개만 허용: {prefabPath}/{agent.name}");
                    var expectedPrefix = agent.Team == Team.Red ? "Red" : "Navy";
                    var rear = sensors.Single(sensor =>
                        sensor.SensorName == $"{expectedPrefix}RayPerceptionSensorReverse");
                    Assert.AreEqual(1, rear.RaysPerDirection);
                    Assert.AreEqual(45f, rear.MaxRayDegrees, 0.000001f);
                    Assert.AreEqual(0.5f, rear.SphereCastRadius, 0.000001f);
                    Assert.AreEqual(80f, rear.RayLength, 0.000001f);
                    Assert.AreEqual(3, rear.ObservationStacks);
                    Assert.Less(Mathf.Abs(Mathf.DeltaAngle(rear.transform.localEulerAngles.y, 180f)), 0.1f);
                }
            }
        }

        [Test]
        public void HumanForwardAccelerationMatchesTheAiStrikerAndKeepsTheSameTopSpeed()
        {
            const float fixedStepSeconds = 0.02f;
            var humanVelocityChangePerStep = SoccerSettings.DefaultHumanAcceleration * fixedStepSeconds;
            var aiStrikerVelocityChangePerStep = SoccerSettings.DefaultAgentRunSpeed
                * AgentSoccer.StrikerForwardSpeedMultiplier;

            Assert.AreEqual(aiStrikerVelocityChangePerStep, humanVelocityChangePerStep, 0.000001f);
            Assert.AreEqual(9f, SoccerSettings.DefaultMaximumPlanarSpeed, 0.000001f);
        }

        [Test]
        public void SharedHudUsesOneCenteredThreeColumnControlTableWithoutTopRightInstructions()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root + "/UI/SoccerHud.uxml");
            Assert.IsNotNull(visualTree);
            var root = visualTree.CloneTree();
            Assert.IsNull(root.Q<Label>("hint-label"), "우측 상단 중복 조작 안내 제거 필요");

            var table = root.Q<VisualElement>("controls-table");
            Assert.IsNotNull(table);
            var rows = table.Query<VisualElement>(className: "controls-row").ToList();
            Assert.AreEqual(6, rows.Count, "머리글 1행과 조작 5행 필요");
            Assert.IsTrue(rows.All(row => row.Children().Count() == 3));
            Assert.IsTrue(rows.SelectMany(row => row.Children())
                .OfType<Label>()
                .All(label => label.ClassListContains("controls-cell")));
            CollectionAssert.AreEqual(
                new[]
                {
                    "동작", "키보드", "컨트롤러",
                    "전진", "W", "R트리거",
                    "후진", "S", "L트리거",
                    "회전", "A/D", "L조이스틱",
                    "패스", "E", "A버튼",
                    "슈팅", "Space", "B버튼"
                },
                rows.SelectMany(row => row.Children()).OfType<Label>().Select(label => label.text).ToArray());
        }

        [Test]
        public void WorkspaceEnvironmentPrefabsAreIndependentRegularAssets()
        {
            var guids = WorkspacePrefabPaths.Select(path =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.IsNotNull(prefab, path);
                Assert.AreEqual(PrefabAssetType.Regular, PrefabUtility.GetPrefabAssetType(prefab), path);
                return AssetDatabase.AssetPathToGUID(path);
            }).ToArray();

            Assert.AreEqual(WorkspacePrefabPaths.Length, guids.Distinct().Count());
        }

        [Test]
        public void BuildSettingsAndNewSceneDefaultUseOnlyTheFiveStadiumWorkspaces()
        {
            var soccerScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && scene.path.StartsWith(Root + "/", StringComparison.Ordinal))
                .Select(scene => scene.path)
                .ToArray();
            CollectionAssert.AreEqual(WorkspaceScenePaths, soccerScenes);
            var projectSettings = File.ReadAllText("ProjectSettings/ProjectSettings.asset");
            StringAssert.Contains(
                "templateDefaultScene: Assets/_Soccer/Core/Scenes/Stadium4v4_Base.unity",
                projectSettings);
        }

        [TestCase(Root + "/Core/Scenes/Stadium4v4_Base.unity", Root + "/Core/Prefabs/StadiumEnvironment_Base.prefab")]
        [TestCase(AttackRoot + "/Scenes/Stadium4v4_Attack.unity", AttackRoot + "/Prefabs/StadiumEnvironment_Attack.prefab")]
        [TestCase(DefenseRoot + "/Scenes/Stadium4v4_Defense.unity", DefenseRoot + "/Prefabs/StadiumEnvironment_Defense.prefab")]
        [TestCase(PressRoot + "/Scenes/Stadium4v4_Press.unity", PressRoot + "/Prefabs/StadiumEnvironment_Press.prefab")]
        [TestCase(RuleRoot + "/Scenes/Stadium4v4_Rule.unity", RuleRoot + "/Prefabs/StadiumEnvironment_Rule.prefab")]
        public void SceneUsesItsOwnEnvironmentPrefabAndVisibleHud(string scenePath, string expectedPrefabPath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var environment = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SoccerEnvController>(true))
                .Single();
            var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(environment.gameObject);
            Assert.AreEqual(expectedPrefabPath, PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot));

            var document = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<UIDocument>(true))
                .Single();
            Assert.IsNotNull(document.panelSettings);
            Assert.IsNotNull(document.visualTreeAsset);

            var settings = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<SoccerSettings>(true))
                .Single();
            Assert.AreEqual(SoccerSettings.DefaultHumanAcceleration, settings.humanAcceleration);
            Assert.AreEqual(SoccerSettings.DefaultHumanDeceleration, settings.humanDeceleration);
            Assert.AreEqual(SoccerSettings.DefaultAgentRunSpeed, settings.agentRunSpeed);
            Assert.AreEqual(SoccerSettings.DefaultMaximumPlanarSpeed, settings.maximumPlanarSpeed);
            Assert.AreEqual(SoccerSettings.DefaultRotationSpeed, settings.rotationSpeed);

            var mainCamera = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .Single(camera => camera.CompareTag("MainCamera"));
            var fader = mainCamera.GetComponent<SoccerGoalOcclusionFader>();
            Assert.IsNotNull(fader, scenePath);
            Assert.AreSame(environment, fader.Environment, scenePath);
            Assert.IsNotNull(mainCamera.GetComponent<SoccerPlayerCamera>(), scenePath);
        }

        [TestCase("Attack-20260809-v001", SoccerTacticType.Attack)]
        [TestCase("Defense-20261231-v099.onnx", SoccerTacticType.Defense)]
        public void ModelNameRuleParsesValidNames(string name, SoccerTacticType expectedTactic)
        {
            Assert.IsTrue(SoccerModelNaming.TryParse(name, out var tactic, out _, out var version));
            Assert.AreEqual(expectedTactic, tactic);
            Assert.Greater(version, 0);
        }

        [TestCase("Attack_v1")]
        [TestCase("Press-20261301-v001")]
        [TestCase("Rule-20260809-v001")]
        public void ModelNameRuleRejectsInvalidNames(string name)
        {
            Assert.IsFalse(SoccerModelNaming.TryParse(name, out _, out _, out _));
        }

        [Test]
        public void OwnerFolderSuffixesDoNotChangeSerializedTeamIdentities()
        {
            var definitions = new[]
            {
                LoadDefinition(AttackRoot + "/Profiles/AttackTeamDefinition.asset"),
                LoadDefinition(DefenseRoot + "/Profiles/DefenseTeamDefinition.asset"),
                LoadDefinition(PressRoot + "/Profiles/PressTeamDefinition.asset"),
                LoadDefinition(RuleRoot + "/Profiles/RuleTeamDefinition.asset")
            };

            CollectionAssert.AreEqual(new[] { "Attack", "Defense", "Press", "Rule" },
                definitions.Select(definition => definition.DisplayName).ToArray());
            CollectionAssert.AreEqual(new[] { "attack", "defense", "press", "rule" },
                definitions.Select(definition => definition.TeamId).ToArray());
            CollectionAssert.AreEqual(
                new[] { "Soccer4v4_Attack", "Soccer4v4_Defense", "Soccer4v4_Press", "Soccer4v4_Rule" },
                definitions.Select(definition => definition.BehaviorName).ToArray());
            Assert.IsFalse(AssetDatabase.IsValidFolder("Assets/Soccer"));
        }

        [Test]
        public void RewardProfilesExposeNewTeamplayRewardsAndTacticalEmphasis()
        {
            var baseProfile = LoadProfile(Root + "/Core/Profiles/BaseRewardProfile.asset");
            var attack = LoadProfile(AttackRoot + "/Profiles/AttackRewardProfile.asset");
            var defense = LoadProfile(DefenseRoot + "/Profiles/DefenseRewardProfile.asset");
            var press = LoadProfile(PressRoot + "/Profiles/PressRewardProfile.asset");
            var rule = LoadProfile(RuleRoot + "/Profiles/RuleRewardProfile.asset");

            Assert.Greater(baseProfile.progressivePass, 0f);
            Assert.Greater(baseProfile.threePlayerCombination, 0f);
            Assert.Greater(baseProfile.coordinatedPress, 0f);
            Assert.LessOrEqual(baseProfile.formationRewardLimitPerPossession, 0.01f);
            Assert.LessOrEqual(baseProfile.shapingRewardLimitPerPossession, 0.12f);
            Assert.LessOrEqual(baseProfile.shapingRewardLimitPerMatch, 0.5f);
            foreach (var profile in new[] { baseProfile, attack, defense, press, rule })
            {
                Assert.AreEqual(0.005f, profile.passSuccess, 0.000001f);
                Assert.AreEqual(0.005f, profile.passIndividual, 0.000001f);
                Assert.AreEqual(0.0025f, profile.controlledCarryGroup, 0.000001f);
                Assert.AreEqual(0.005f, profile.controlledCarryIndividual, 0.000001f);
                Assert.AreEqual(0.003f, profile.wastefulStrongKickPenalty, 0.000001f);
                Assert.AreEqual(0.02f, profile.unsafeOwnGoalKickPenalty, 0.000001f);
                Assert.AreEqual(0.015f, profile.wastefulStrongKickPenaltyLimitPerPossession, 0.000001f);
                Assert.AreEqual(0.1f, profile.behaviorPenaltyLimitPerMatch, 0.000001f);
                Assert.AreEqual(0.02f, profile.passIndividualRewardLimitPerPossession, 0.000001f);
                Assert.AreEqual(0.03f, profile.controlledCarryRewardLimitPerPossession, 0.000001f);
                Assert.AreEqual(0.0025f, profile.teammateCrowdingPenalty, 0.000001f);
                Assert.AreEqual(0.25f, profile.teammateCrowdingPenaltyLimitPerMatch, 0.000001f);
                Assert.AreEqual(
                    profile.teammateCrowdingPenalty,
                    profile.GetGroupReward(SoccerRewardKind.TeammateCrowding),
                    0.000001f);
                Assert.AreEqual(
                    profile.passIndividual,
                    profile.GetIndividualReward(SoccerRewardKind.PassIndividual),
                    0.000001f);
                Assert.AreEqual(
                    profile.controlledCarryGroup,
                    profile.GetGroupReward(SoccerRewardKind.ControlledCarry),
                    0.000001f);
                Assert.AreEqual(
                    profile.controlledCarryIndividual,
                    profile.GetIndividualReward(SoccerRewardKind.ControlledCarry),
                    0.000001f);
            }

            Assert.Greater(attack.attackSuccess, defense.attackSuccess);
            Assert.Greater(defense.defenseSuccess, attack.defenseSuccess);
            Assert.Greater(press.pressSuccess, attack.pressSuccess);
        }

        [Test]
        public void TeammateCrowdingUsesWorstPairWithLinearThreeToOneMeterSeverity()
        {
            var root = PrefabUtility.LoadPrefabContents(Root + "/Core/Prefabs/StadiumEnvironment_Base.prefab");
            try
            {
                var agents = root.GetComponentsInChildren<AgentSoccer>(true)
                    .Where(agent => agent.Team == Team.Red)
                    .ToArray();
                Assert.AreEqual(4, agents.Length);

                agents[0].transform.position = new Vector3(0f, 0.5f, 0f);
                agents[1].transform.position = new Vector3(3f, 0.5f, 0f);
                agents[2].transform.position = new Vector3(20f, 0.5f, 20f);
                agents[3].transform.position = new Vector3(30f, 0.5f, 30f);
                Assert.AreEqual(0f, SoccerFormationEvaluator.EvaluateTeammateCrowding(agents), 0.000001f);

                agents[1].transform.position = new Vector3(2f, 0.5f, 0f);
                Assert.AreEqual(0.5f, SoccerFormationEvaluator.EvaluateTeammateCrowding(agents), 0.000001f);

                agents[2].transform.position = new Vector3(20f, 0.5f, 20f);
                agents[3].transform.position = new Vector3(22f, 0.5f, 20f);
                Assert.AreEqual(0.5f, SoccerFormationEvaluator.EvaluateTeammateCrowding(agents), 0.000001f,
                    "두 과밀 쌍의 심각도를 합산하지 않고 최악 한 쌍만 사용 필요");

                agents[2].transform.position = new Vector3(1f, 0.5f, 0f);
                Assert.AreEqual(1f, SoccerFormationEvaluator.EvaluateTeammateCrowding(agents), 0.000001f);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void TeammateCrowdingUsesIndependentPerTeamMatchCapsAndResetMatchClearsThem()
        {
            var root = PrefabUtility.LoadPrefabContents(Root + "/Core/Prefabs/StadiumEnvironment_Base.prefab");
            var profile = AssetDatabase.LoadAssetAtPath<SoccerRewardProfile>(
                Root + "/Core/Profiles/BaseRewardProfile.asset");
            var originalLimit = profile.teammateCrowdingPenaltyLimitPerMatch;
            try
            {
                var environment = root.GetComponent<SoccerEnvController>();
                var matchSetup = root.GetComponent<SoccerMatchSetup>();
                var rewardEngine = root.GetComponent<SoccerRewardEngine>();
                Assert.IsNotNull(environment);
                Assert.IsNotNull(matchSetup);
                Assert.IsNotNull(rewardEngine);

                matchSetup.ApplyDefinitions();
                rewardEngine.Configure(environment, matchSetup);
                profile.teammateCrowdingPenaltyLimitPerMatch = 0.005f;

                var redAgents = root.GetComponentsInChildren<AgentSoccer>(true)
                    .Where(agent => agent.Team == Team.Red)
                    .ToArray();
                var navyAgents = root.GetComponentsInChildren<AgentSoccer>(true)
                    .Where(agent => agent.Team == Team.Navy)
                    .ToArray();
                Assert.AreEqual(4, redAgents.Length);
                Assert.AreEqual(4, navyAgents.Length);

                redAgents[0].transform.position = new Vector3(-20f, 0.5f, -20f);
                redAgents[1].transform.position = redAgents[0].transform.position;
                redAgents[2].transform.position = new Vector3(0f, 0.5f, -20f);
                redAgents[3].transform.position = new Vector3(20f, 0.5f, -20f);
                navyAgents[0].transform.position = new Vector3(-20f, 0.5f, 20f);
                navyAgents[1].transform.position = navyAgents[0].transform.position + Vector3.right * 2f;
                navyAgents[2].transform.position = new Vector3(0f, 0.5f, 20f);
                navyAgents[3].transform.position = new Vector3(20f, 0.5f, 20f);

                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var refreshBuffers = typeof(SoccerRewardEngine).GetMethod("RefreshTeamAgentBuffers", flags);
                var evaluateCrowding = typeof(SoccerRewardEngine).GetMethod("EvaluateTeammateCrowding", flags);
                Assert.IsNotNull(refreshBuffers);
                Assert.IsNotNull(evaluateCrowding);
                refreshBuffers.Invoke(rewardEngine, null);

                for (var sample = 0; sample < 4; sample++)
                {
                    evaluateCrowding.Invoke(rewardEngine, null);
                }

                Assert.AreEqual(-0.005f, rewardEngine.GetCumulativeReward(Team.Red), 0.000001f);
                Assert.AreEqual(-0.005f, rewardEngine.GetCumulativeReward(Team.Navy), 0.000001f);

                rewardEngine.ResetMatch();
                evaluateCrowding.Invoke(rewardEngine, null);
                Assert.AreEqual(-0.0025f, rewardEngine.GetCumulativeReward(Team.Red), 0.000001f);
                Assert.AreEqual(-0.00125f, rewardEngine.GetCumulativeReward(Team.Navy), 0.000001f);
            }
            finally
            {
                profile.teammateCrowdingPenaltyLimitPerMatch = originalLimit;
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void ConfirmedPassRewardsTheTeamAndPasserWithIndependentPossessionCaps()
        {
            var root = PrefabUtility.LoadPrefabContents(Root + "/Core/Prefabs/StadiumEnvironment_Base.prefab");
            try
            {
                var environment = root.GetComponent<SoccerEnvController>();
                var matchSetup = root.GetComponent<SoccerMatchSetup>();
                var rewardEngine = root.GetComponent<SoccerRewardEngine>();
                matchSetup.ApplyDefinitions();
                rewardEngine.Configure(environment, matchSetup);
                rewardEngine.ResetPossession();
                rewardEngine.ResetMatch();

                var redAgents = root.GetComponentsInChildren<AgentSoccer>(true)
                    .Where(agent => agent.Team == Team.Red)
                    .ToArray();
                var passer = redAgents[0];
                var receiver = redAgents[1];
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var awardPass = typeof(SoccerRewardEngine).GetMethod("AwardPass", flags);
                Assert.IsNotNull(awardPass);
                for (var pass = 0; pass < 10; pass++)
                {
                    awardPass.Invoke(
                        rewardEngine,
                        new object[]
                        {
                            Team.Red,
                            passer,
                            receiver,
                            new Vector3(0f, 0.5f, 0f),
                            new Vector3(0f, 0.5f, 4f)
                        });
                }

                Assert.AreEqual(0.045f, rewardEngine.GetCumulativeReward(Team.Red), 0.000001f,
                    "팀 Pass 0.025와 Passer 개인 0.02의 독립 cap 합이 필요");
                Assert.AreEqual(0f, rewardEngine.GetCumulativeReward(Team.Navy), 0.000001f);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void RewardTelemetryUsesDistinctStableRedAndNavyTensorBoardKeys()
        {
            var keys = new[]
            {
                SoccerRewardEngine.GetTeamRewardEventStatKey(Team.Red, SoccerRewardKind.GoalResult),
                SoccerRewardEngine.GetTeamRewardEventStatKey(Team.Navy, SoccerRewardKind.GoalResult),
                SoccerRewardEngine.GetTeamRewardSummaryTotalStatKey(Team.Red),
                SoccerRewardEngine.GetTeamRewardSummaryTotalStatKey(Team.Navy),
                SoccerRewardEngine.GetTeamMatchRewardStatKey(Team.Red),
                SoccerRewardEngine.GetTeamMatchRewardStatKey(Team.Navy)
            };

            CollectionAssert.AreEqual(
                new[]
                {
                    "Soccer/Red/Reward/GoalResult",
                    "Soccer/Navy/Reward/GoalResult",
                    "Soccer/Red/Reward/Summary Total",
                    "Soccer/Navy/Reward/Summary Total",
                    "Soccer/Red/Match Reward",
                    "Soccer/Navy/Match Reward"
                },
                keys);
            CollectionAssert.AllItemsAreUnique(keys);
        }

        [Test]
        public void RuleControllerSteersAwayFromNearbyTeammateWithoutChangingItsStateTarget()
        {
            var root = PrefabUtility.LoadPrefabContents(RuleRoot + "/Prefabs/StadiumEnvironment_Rule.prefab");
            try
            {
                var environment = root.GetComponent<SoccerEnvController>();
                var controller = root.GetComponentsInChildren<RuleBasedSoccerController>(true)
                    .First(candidate => candidate.GetComponent<AgentSoccer>().PositionRole
                        != AgentSoccer.Position.DefenderKeeper);
                var agent = controller.GetComponent<AgentSoccer>();
                var teammates = environment.AgentsList
                    .Select(item => item?.Agent)
                    .Where(candidate => candidate != null && candidate.Team == agent.Team && candidate != agent)
                    .ToArray();
                Assert.AreEqual(3, teammates.Length);

                agent.transform.SetPositionAndRotation(new Vector3(0f, 0.5f, 0f), Quaternion.identity);
                environment.ball.transform.position = new Vector3(0f, 0.5f, 10f);
                for (var index = 0; index < teammates.Length; index++)
                {
                    teammates[index].transform.position = new Vector3(20f + index * 5f, 0.5f, 20f);
                }

                controller.Configure(agent, environment);
                controller.ResetController();
                var baseline = controller.Decide();
                Assert.AreEqual(0, baseline.Rotation);

                teammates[0].transform.position = new Vector3(1.5f, 0.5f, 0f);
                controller.ResetController();
                var separated = controller.Decide();
                Assert.AreEqual(1, separated.Rotation, "오른쪽의 가까운 동료에게서 왼쪽으로 조향 필요");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void RuleControllersRespectTheSingleConfirmedBallCarrierWhenTeammatesCluster()
        {
            var root = PrefabUtility.LoadPrefabContents(RuleRoot + "/Prefabs/StadiumEnvironment_Rule.prefab");
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var environment = root.GetComponent<SoccerEnvController>();
                var rewardEngine = root.GetComponent<SoccerRewardEngine>();
                var controllers = root.GetComponentsInChildren<RuleBasedSoccerController>(true);
                var carrierController = controllers[0];
                var nearbyController = controllers[1];
                var carrier = carrierController.GetComponent<AgentSoccer>();
                var nearby = nearbyController.GetComponent<AgentSoccer>();
                carrier.transform.position = Vector3.zero;
                nearby.transform.position = Vector3.zero;
                environment.ball.transform.position = Vector3.zero;
                carrierController.Configure(carrier, environment);
                nearbyController.Configure(nearby, environment);

                var environmentRewardEngine = typeof(SoccerEnvController).GetField("m_RewardEngine", flags);
                var confirmedCarrier = typeof(SoccerRewardEngine).GetField("m_BallCarrier", flags);
                Assert.IsNotNull(environmentRewardEngine);
                Assert.IsNotNull(confirmedCarrier);
                environmentRewardEngine.SetValue(environment, rewardEngine);
                confirmedCarrier.SetValue(rewardEngine, carrier);
                var isBallCarrier = typeof(RuleBasedSoccerController).GetMethod("IsBallCarrier", flags);
                Assert.IsNotNull(isBallCarrier);

                Assert.IsTrue((bool)isBallCarrier.Invoke(carrierController, null));
                Assert.IsFalse((bool)isBallCarrier.Invoke(nearbyController, null),
                    "확정 carrier가 있으면 가까운 다른 선수는 중복 Kick 상태로 전환하면 안 됨");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void CommonFallbackCarriesPassesShootsAndClearsOnlyInTheirIntendedContexts()
        {
            var root = PrefabUtility.LoadPrefabContents(Root + "/Core/Prefabs/StadiumEnvironment_Base.prefab");
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var environment = root.GetComponent<SoccerEnvController>();
                var rewardEngine = root.GetComponent<SoccerRewardEngine>();
                var carrier = root.GetComponentsInChildren<AgentSoccer>(true)
                    .First(agent => agent.Team == Team.Red && agent.PositionRole == AgentSoccer.Position.Striker);
                var teammates = root.GetComponentsInChildren<AgentSoccer>(true)
                    .Where(agent => agent.Team == Team.Red && agent != carrier)
                    .ToArray();
                var opponents = root.GetComponentsInChildren<AgentSoccer>(true)
                    .Where(agent => agent.Team == Team.Navy)
                    .ToArray();
                var environmentRewardEngine = typeof(SoccerEnvController).GetField("m_RewardEngine", flags);
                var confirmedCarrier = typeof(SoccerRewardEngine).GetField("m_BallCarrier", flags);
                Assert.IsNotNull(environmentRewardEngine);
                Assert.IsNotNull(confirmedCarrier);
                environmentRewardEngine.SetValue(environment, rewardEngine);
                confirmedCarrier.SetValue(rewardEngine, carrier);

                carrier.transform.position = Vector3.zero;
                environment.ball.transform.position = Vector3.zero;
                for (var index = 0; index < teammates.Length; index++)
                {
                    teammates[index].transform.position = new Vector3(15f + index * 5f, 0.5f, 25f);
                }

                for (var index = 0; index < opponents.Length; index++)
                {
                    opponents[index].transform.position = new Vector3(0f, 0.5f, 25f + index * 4f);
                }

                Assert.IsFalse(environment.TryGetAutonomousKickTarget(carrier, out _, out _),
                    "압박 없는 중원에서는 Kick보다 carry를 유지해야 함");

                teammates[0].transform.position = new Vector3(10f, 0.5f, 0f);
                opponents[0].transform.position = new Vector3(2f, 0.5f, 0f);
                Assert.IsTrue(environment.TryGetAutonomousKickTarget(
                    carrier,
                    out var passTarget,
                    out var passAction));
                Assert.AreEqual(1, passAction);
                Assert.AreEqual(teammates[0].transform.position, passTarget);

                opponents[0].transform.position = new Vector3(40f, 0.5f, 30f);
                carrier.transform.position = new Vector3(30f, 0.5f, 0f);
                environment.ball.transform.position = carrier.transform.position;
                Assert.IsFalse(environment.TryGetAutonomousKickTarget(carrier, out _, out _),
                    "Goal까지 24m보다 먼 위치에서는 Strong shot을 선택하면 안 됨");

                carrier.transform.position = new Vector3(40f, 0.5f, 0f);
                environment.ball.transform.position = carrier.transform.position;
                Assert.IsTrue(environment.TryGetAutonomousKickTarget(
                    carrier,
                    out var shotTarget,
                    out var shotAction));
                Assert.AreEqual(2, shotAction);
                Assert.AreEqual(62f, shotTarget.x, 0.000001f);

                carrier.transform.position = new Vector3(-55f, 0.5f, 0f);
                environment.ball.transform.position = carrier.transform.position;
                Assert.IsTrue(environment.TryGetAutonomousKickTarget(
                    carrier,
                    out var clearanceTarget,
                    out var clearanceAction));
                Assert.AreEqual(1, clearanceAction);
                Assert.AreEqual(Vector3.zero.x, clearanceTarget.x, 0.000001f);
                Assert.AreEqual(Vector3.zero.z, clearanceTarget.z, 0.000001f);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void NeuralTacticsUseV2AndRuleWorkspaceIsFullyExcludedFromTraining()
        {
            var neuralDefinitions = new[]
            {
                LoadDefinition(Root + "/Core/Profiles/BaseTeamDefinition.asset"),
                LoadDefinition(AttackRoot + "/Profiles/AttackTeamDefinition.asset"),
                LoadDefinition(DefenseRoot + "/Profiles/DefenseTeamDefinition.asset"),
                LoadDefinition(PressRoot + "/Profiles/PressTeamDefinition.asset")
            };
            var ruleDefinition = LoadDefinition(RuleRoot + "/Profiles/RuleTeamDefinition.asset");

            Assert.IsTrue(neuralDefinitions.All(definition =>
                definition.PolicyContractVersion == SoccerTeamDefinition.CurrentPolicyContractVersion));
            Assert.IsTrue(neuralDefinitions.All(definition => definition.UsesNeuralPolicy));
            Assert.AreEqual(SoccerTeamControllerType.RuleBased, ruleDefinition.ControllerType);
            Assert.IsNull(ruleDefinition.InferenceModel);
            Assert.IsFalse(AssetDatabase.IsValidFolder(RuleRoot + "/Training"));
            Assert.IsFalse(AssetDatabase.IsValidFolder(RuleRoot + "/Models"));
        }

        static SoccerRewardProfile LoadProfile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<SoccerRewardProfile>(path);
            Assert.IsNotNull(profile, path);
            return profile;
        }

        static void AssertMaterial(string path, string expectedName, Color expectedColor)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.IsNotNull(material, path);
            Assert.AreEqual(expectedName, material.name, path);
            Assert.Less(Vector4.Distance(expectedColor, material.GetColor("_BaseColor")), 0.001f, path);
        }

        static SoccerTeamDefinition LoadDefinition(string path)
        {
            var definition = AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(path);
            Assert.IsNotNull(definition, path);
            return definition;
        }
    }
}
