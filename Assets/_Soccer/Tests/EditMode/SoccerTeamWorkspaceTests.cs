using System;
using System.Linq;
using System.Reflection;
using MachineLearning.Soccer.Editor;
using MachineLearning.Soccer.Teams.Rule;
using NUnit.Framework;
using Unity.MLAgents.Policies;
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
            Root + "/Core/Prefabs/SoccerEnvironment_Base.prefab",
            AttackRoot + "/Prefabs/SoccerEnvironment_Attack.prefab",
            DefenseRoot + "/Prefabs/SoccerEnvironment_Defense.prefab",
            PressRoot + "/Prefabs/SoccerEnvironment_Press.prefab",
            RuleRoot + "/Prefabs/SoccerEnvironment_Rule.prefab"
        };

        static readonly string[] CommonArenaPrefabPaths = new[]
            { Root + "/Prefabs/SoccerField4v4.prefab" }.Concat(WorkspacePrefabPaths).ToArray();

        [Test]
        public void GeneratedPrefabUsesFourPlayerRolesAndV2PolicyContract()
        {
            Assert.AreEqual(1500f, AgentSoccer.ControlledKickPower);
            Assert.AreEqual(4000f, AgentSoccer.StrongKickPower);
            foreach (var prefabPath in WorkspacePrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.IsNotNull(prefab, prefabPath);
                var setup = prefab.GetComponent<SoccerMatchSetup>();
                Assert.IsNotNull(setup, prefabPath);
                var agents = prefab.GetComponentsInChildren<AgentSoccer>(true);
                Assert.AreEqual(8, agents.Length, prefabPath);
                foreach (var team in new[] { Team.Blue, Team.Purple })
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

                var goalRenderers = prefab.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.gameObject.name is "GoalBlue" or "GoalNetBlue" or "GoalNetBlueOuter"
                        or "GoalPurple" or "GoalNetPurple" or "GoalNetPurpleOuter")
                    .ToArray();
                Assert.AreEqual(6, goalRenderers.Length, prefabPath);
                Assert.IsTrue(goalRenderers.SelectMany(renderer => renderer.sharedMaterials)
                    .All(material => material != null && material.name.StartsWith("Goal", StringComparison.Ordinal)));
                Assert.IsTrue(goalRenderers.All(renderer =>
                    (GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.BatchingStatic) == 0));
            }
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
                .Where(path => path.Contains("SoccerEnvironment_", StringComparison.Ordinal))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            CollectionAssert.AreEquivalent(WorkspacePrefabPaths, discoveredWorkspacePrefabs);
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

        [TestCase(Root + "/Scenes/Soccer4v4.unity", Root + "/Core/Prefabs/SoccerEnvironment_Base.prefab")]
        [TestCase(Root + "/Core/Scenes/Soccer4v4_Base.unity", Root + "/Core/Prefabs/SoccerEnvironment_Base.prefab")]
        [TestCase(AttackRoot + "/Scenes/Soccer4v4_Attack.unity", AttackRoot + "/Prefabs/SoccerEnvironment_Attack.prefab")]
        [TestCase(DefenseRoot + "/Scenes/Soccer4v4_Defense.unity", DefenseRoot + "/Prefabs/SoccerEnvironment_Defense.prefab")]
        [TestCase(PressRoot + "/Scenes/Soccer4v4_Press.unity", PressRoot + "/Prefabs/SoccerEnvironment_Press.prefab")]
        [TestCase(RuleRoot + "/Scenes/Soccer4v4_Rule.unity", RuleRoot + "/Prefabs/SoccerEnvironment_Rule.prefab")]
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
                Assert.AreEqual(0.0025f, profile.teammateCrowdingPenalty, 0.000001f);
                Assert.AreEqual(0.25f, profile.teammateCrowdingPenaltyLimitPerMatch, 0.000001f);
                Assert.AreEqual(
                    profile.teammateCrowdingPenalty,
                    profile.GetGroupReward(SoccerRewardKind.TeammateCrowding),
                    0.000001f);
            }

            Assert.Greater(attack.attackSuccess, defense.attackSuccess);
            Assert.Greater(defense.defenseSuccess, attack.defenseSuccess);
            Assert.Greater(press.pressSuccess, attack.pressSuccess);
        }

        [Test]
        public void TeammateCrowdingUsesWorstPairWithLinearThreeToOneMeterSeverity()
        {
            var root = PrefabUtility.LoadPrefabContents(Root + "/Core/Prefabs/SoccerEnvironment_Base.prefab");
            try
            {
                var agents = root.GetComponentsInChildren<AgentSoccer>(true)
                    .Where(agent => agent.Team == Team.Blue)
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
            var root = PrefabUtility.LoadPrefabContents(Root + "/Core/Prefabs/SoccerEnvironment_Base.prefab");
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

                var blueAgents = root.GetComponentsInChildren<AgentSoccer>(true)
                    .Where(agent => agent.Team == Team.Blue)
                    .ToArray();
                var purpleAgents = root.GetComponentsInChildren<AgentSoccer>(true)
                    .Where(agent => agent.Team == Team.Purple)
                    .ToArray();
                Assert.AreEqual(4, blueAgents.Length);
                Assert.AreEqual(4, purpleAgents.Length);

                blueAgents[0].transform.position = new Vector3(-20f, 0.5f, -20f);
                blueAgents[1].transform.position = blueAgents[0].transform.position;
                blueAgents[2].transform.position = new Vector3(0f, 0.5f, -20f);
                blueAgents[3].transform.position = new Vector3(20f, 0.5f, -20f);
                purpleAgents[0].transform.position = new Vector3(-20f, 0.5f, 20f);
                purpleAgents[1].transform.position = purpleAgents[0].transform.position + Vector3.right * 2f;
                purpleAgents[2].transform.position = new Vector3(0f, 0.5f, 20f);
                purpleAgents[3].transform.position = new Vector3(20f, 0.5f, 20f);

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

                Assert.AreEqual(-0.005f, rewardEngine.GetCumulativeReward(Team.Blue), 0.000001f);
                Assert.AreEqual(-0.005f, rewardEngine.GetCumulativeReward(Team.Purple), 0.000001f);

                rewardEngine.ResetMatch();
                evaluateCrowding.Invoke(rewardEngine, null);
                Assert.AreEqual(-0.0025f, rewardEngine.GetCumulativeReward(Team.Blue), 0.000001f);
                Assert.AreEqual(-0.00125f, rewardEngine.GetCumulativeReward(Team.Purple), 0.000001f);
            }
            finally
            {
                profile.teammateCrowdingPenaltyLimitPerMatch = originalLimit;
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [Test]
        public void RuleControllerSteersAwayFromNearbyTeammateWithoutChangingItsStateTarget()
        {
            var root = PrefabUtility.LoadPrefabContents(RuleRoot + "/Prefabs/SoccerEnvironment_Rule.prefab");
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

        static SoccerTeamDefinition LoadDefinition(string path)
        {
            var definition = AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(path);
            Assert.IsNotNull(definition, path);
            return definition;
        }
    }
}
