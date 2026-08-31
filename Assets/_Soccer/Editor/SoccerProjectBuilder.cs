using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using MachineLearning.Soccer.Teams.Attack;
using MachineLearning.Soccer.Teams.Defense;
using MachineLearning.Soccer.Teams.Press;
using MachineLearning.Soccer.Teams.Rule;
using Unity.InferenceEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace MachineLearning.Soccer.Editor
{
    /// <summary>
    /// 공통 경기 계약과 작업공간 배선을 생성·검증하는 단일 배포 지점.
    /// 팀별 실험 수치를 공통 생성 상수로 하드코딩하지 않는다.
    /// </summary>
    public static class SoccerProjectBuilder
    {
        const string Root = "Assets/_Soccer";
        const string LegacyRoot = "Assets/_Legacy";
        // 담당자 접미사는 물리 폴더에만 사용한다. 전술명, BehaviorName, 모델 이름 계약은 변경하지 않는다.
        const string AttackFolder = "Attack_KMW";
        const string DefenseFolder = "Defense_PJH";
        const string PressFolder = "Press_KMG";
        const string RuleFolder = "Rule_PHC";
        const string SourcePrefabPath = Root + "/Prefabs/SoccerFieldTwos.prefab";
        const string OutputPrefabPath = Root + "/Prefabs/SoccerField4v4.prefab";
        const string BaseEnvironmentPrefabPath = Root + "/Core/Prefabs/SoccerEnvironment_Base.prefab";
        const string AttackEnvironmentPrefabPath = Root + "/Teams/" + AttackFolder + "/Prefabs/SoccerEnvironment_Attack.prefab";
        const string DefenseEnvironmentPrefabPath = Root + "/Teams/" + DefenseFolder + "/Prefabs/SoccerEnvironment_Defense.prefab";
        const string PressEnvironmentPrefabPath = Root + "/Teams/" + PressFolder + "/Prefabs/SoccerEnvironment_Press.prefab";
        const string RuleEnvironmentPrefabPath = Root + "/Teams/" + RuleFolder + "/Prefabs/SoccerEnvironment_Rule.prefab";
        public const string StadiumBaseEnvironmentPrefabPath = Root + "/Core/Prefabs/StadiumEnvironment_Base.prefab";
        public const string StadiumAttackEnvironmentPrefabPath = Root + "/Teams/" + AttackFolder + "/Prefabs/StadiumEnvironment_Attack.prefab";
        public const string StadiumDefenseEnvironmentPrefabPath = Root + "/Teams/" + DefenseFolder + "/Prefabs/StadiumEnvironment_Defense.prefab";
        public const string StadiumPressEnvironmentPrefabPath = Root + "/Teams/" + PressFolder + "/Prefabs/StadiumEnvironment_Press.prefab";
        public const string StadiumRuleEnvironmentPrefabPath = Root + "/Teams/" + RuleFolder + "/Prefabs/StadiumEnvironment_Rule.prefab";
        const string ScenePath = Root + "/Scenes/Soccer4v4.unity";
        const string BaseScenePath = Root + "/Core/Scenes/Soccer4v4_Base.unity";
        const string AttackScenePath = Root + "/Teams/" + AttackFolder + "/Scenes/Soccer4v4_Attack.unity";
        const string DefenseScenePath = Root + "/Teams/" + DefenseFolder + "/Scenes/Soccer4v4_Defense.unity";
        const string PressScenePath = Root + "/Teams/" + PressFolder + "/Scenes/Soccer4v4_Press.unity";
        const string RuleScenePath = Root + "/Teams/" + RuleFolder + "/Scenes/Soccer4v4_Rule.unity";
        public const string StadiumBaseScenePath = Root + "/Core/Scenes/Stadium4v4_Base.unity";
        public const string StadiumAttackScenePath = Root + "/Teams/" + AttackFolder + "/Scenes/Stadium4v4_Attack.unity";
        public const string StadiumDefenseScenePath = Root + "/Teams/" + DefenseFolder + "/Scenes/Stadium4v4_Defense.unity";
        public const string StadiumPressScenePath = Root + "/Teams/" + PressFolder + "/Scenes/Stadium4v4_Press.unity";
        public const string StadiumRuleScenePath = Root + "/Teams/" + RuleFolder + "/Scenes/Stadium4v4_Rule.unity";
        const string BaseProfilePath = Root + "/Core/Profiles/BaseRewardProfile.asset";
        const string BaseDefinitionPath = Root + "/Core/Profiles/BaseTeamDefinition.asset";
        const string PanelSettingsPath = Root + "/UI/SoccerPanelSettings.asset";
        const string UxmlPath = Root + "/UI/SoccerHud.uxml";
        const string ThemePath = Root + "/UI/SoccerRuntimeTheme.tss";
        const string HumanMarkerMaterialPath = Root + "/Materials/HumanMarker.mat";
        const string StadiumRedGoalMaterialPath = Root + "/Core/Materials/StadiumRedGoal.mat";
        const string StadiumNavyGoalMaterialPath = Root + "/Core/Materials/StadiumNavyGoal.mat";
        const string WindowsBuildPath = "Builds/Soccer/Soccer4v4.exe";
        const string KickPlateLayerPrefix = "SoccerPlate";
        const int KickPlateLayerCount = 8;
        const float FieldScaleMultiplier = 4f;
        const float SourceFieldScale = 0.01f;
        const float SensorRange = 80f;
        const float GoalLateralScale = 0.7f;
        const float BallUniformScale = 0.0105f;
        const float BallResetHeight = 0.35f;
        const float MatchDurationSeconds = 300f;
        const float GoalResetDelaySeconds = 3f;

        static readonly HashSet<string> GoalRendererNames = new(StringComparer.Ordinal)
        {
            "GoalRed",
            "GoalNetRed",
            "GoalNetRedOuter",
            "GoalNavy",
            "GoalNetNavy",
            "GoalNetNavyOuter"
        };

        static readonly Vector3[] RedSpawns =
        {
            new(-22f, 0.5f, 0f),
            new(-34f, 0.5f, -10f),
            new(-34f, 0.5f, 10f),
            new(-54f, 0.5f, 0f)
        };

        static readonly Vector3[] NavySpawns =
        {
            new(22f, 0.5f, 0f),
            new(34f, 0.5f, 10f),
            new(34f, 0.5f, -10f),
            new(54f, 0.5f, 0f)
        };

        [MenuItem("Tools/Soccer/Build all 4v4 Stadium workspaces")]
        public static void BuildAll()
        {
            // 기존 사각 경기장은 보존만 하며 생성·Build Settings·학습 경로에서 더 이상 사용하지 않는다.
            // 모든 활성 작업공간은 검증된 Core Stadium 기준본에서 다시 생성한다.
            EnsureDirectories();
            EnsureTags("ball", "redGoal", "navyGoal", "wall", "redAgent", "navyAgent");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CreateVisualMaterialSet();
            PrepareStadiumGoalMaterials();
            CreatePanelSettings();
            var baseProfile = CreateRewardProfile(BaseProfilePath, null);
            var baseModel = FindLatestNamedModel(Root + "/Core/Models", SoccerTacticType.Base);
            var baseDefinition = CreateTeamDefinition(BaseDefinitionPath, "base", "Base", "Soccer4v4_Base", baseProfile, baseModel);
            var attackDefinition = CreateTeamWorkspace(AttackFolder, "Attack", "attack", "Soccer4v4_Attack", baseProfile, baseModel);
            var defenseDefinition = CreateTeamWorkspace(DefenseFolder, "Defense", "defense", "Soccer4v4_Defense", baseProfile, baseModel);
            var pressDefinition = CreateTeamWorkspace(PressFolder, "Press", "press", "Soccer4v4_Press", baseProfile, baseModel);
            var ruleDefinition = CreateRuleTeamWorkspace(baseProfile);

            BuildStadiumWorkspaces(
                baseDefinition,
                attackDefinition,
                defenseDefinition,
                pressDefinition,
                ruleDefinition);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateGeneratedAssets();
            Debug.Log("All five Soccer 4v4 Stadium workspaces generated successfully.");
        }

        public static void BuildAllBatch()
        {
            BuildAll();
        }

        /// <summary>
        /// Rebuilds every active team workspace from the canonical Core Stadium
        /// without regenerating profiles, models, or the archived rectangular arenas.
        /// </summary>
        public static void BuildStadiumWorkspacesBatch()
        {
            EnsureDirectories();
            EnsureTags("ball", "redGoal", "navyGoal", "wall", "redAgent", "navyAgent");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            CreateVisualMaterialSet();
            PrepareStadiumGoalMaterials();
            BuildStadiumWorkspaces(
                AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(BaseDefinitionPath),
                AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(Root + "/Teams/" + AttackFolder + "/Profiles/AttackTeamDefinition.asset"),
                AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(Root + "/Teams/" + DefenseFolder + "/Profiles/DefenseTeamDefinition.asset"),
                AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(Root + "/Teams/" + PressFolder + "/Profiles/PressTeamDefinition.asset"),
                AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(Root + "/Teams/" + RuleFolder + "/Profiles/RuleTeamDefinition.asset"));
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateActiveStadiumAssets();
        }

        [MenuItem("Tools/Soccer/Validate active 4v4 Stadiums")]
        public static void ValidateGeneratedAssets()
        {
            ValidateActiveStadiumAssets();
        }

        static void ValidateLegacyGeneratedAssets()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefabPath);
            Require(prefab != null, "SoccerField4v4.prefab is missing.");

            var root = PrefabUtility.LoadPrefabContents(OutputPrefabPath);
            try
            {
                var controller = root.GetComponent<SoccerEnvController>();
                Require(controller != null, "SoccerEnvController is missing.");
                Require(Mathf.Approximately(controller.matchDurationSeconds, MatchDurationSeconds),
                    "Match duration must be 300 seconds.");
                Require(Mathf.Approximately(controller.goalResetDelaySeconds, GoalResetDelaySeconds),
                    "Goal reset delay must be 3 seconds.");
                Require(controller.ball != null, "Ball reference is missing.");
                ValidateCommonGeometry(root, OutputPrefabPath);
                Require(Mathf.Approximately(AgentSoccer.ControlledKickPower, 2000f)
                    && Mathf.Approximately(AgentSoccer.StrongKickPower, 5000f),
                    "Kick power must be 2000 for controlled kicks and 5000 for strong kicks.");

                var field = root.transform.Find("Field");
                Require(field != null, "Copied ML-Agents Field hierarchy is missing.");
                Require(Mathf.Approximately(field.localScale.x, SourceFieldScale * FieldScaleMultiplier)
                    && Mathf.Approximately(field.localScale.z, SourceFieldScale * FieldScaleMultiplier),
                    "Field X/Z dimensions must be four times the source prefab.");

                var agents = root.GetComponentsInChildren<AgentSoccer>(true);
                Require(agents.Length == 8, $"Expected 8 players, found {agents.Length}.");
                Require(agents.Count(agent => agent.Team == Team.Red) == 4, "Red team must have four players.");
                Require(agents.Count(agent => agent.Team == Team.Navy) == 4, "Navy team must have four players.");
                Require(agents.Count(agent => agent.HumanControllable) == 1, "Exactly one player must be human-controllable.");
                Require(agents.Single(agent => agent.HumanControllable).Team == Team.Red, "Human player must belong to Red.");
                Require(controller.AgentsList.Count == 8, "Environment player registry must contain eight players.");
                Require(!root.GetComponentsInChildren<Transform>(true).Any(child => child.name == "Headband"),
                    "All player headbands must be removed.");
                ValidateRendererMaterials(root);

                var kickPlates = root.GetComponentsInChildren<SoccerKickPlate>(true);
                Require(kickPlates.Length == KickPlateLayerCount,
                    $"Expected {KickPlateLayerCount} kick plates, found {kickPlates.Length}.");
                var kickPlateLayers = kickPlates.Select(plate => plate.gameObject.layer).ToArray();
                Require(kickPlateLayers.Distinct().Count() == KickPlateLayerCount,
                    "Every player's kick plate must use a unique owner-filter layer.");

                foreach (var agent in agents)
                {
                    var behavior = agent.GetComponent<BehaviorParameters>();
                    var requester = agent.GetComponent<DecisionRequester>();
                    Require(behavior != null && behavior.BehaviorName == "Soccer4v4_Base", $"{agent.name} behavior mismatch.");
                    Require(behavior.TeamId == (int)agent.Team, $"{agent.name} team id mismatch.");
                    Require(behavior.BrainParameters.VectorObservationSize == AgentSoccer.VectorObservationSize,
                        $"{agent.name} vector observation size mismatch.");
                    Require(behavior.BrainParameters.ActionSpec.BranchSizes.SequenceEqual(new[] { 3, 3, 3, 3 }),
                        $"{agent.name} must use the v2 [3,3,3,3] action model.");
                    Require(requester != null && requester.DecisionPeriod == 5, $"{agent.name} DecisionRequester mismatch.");
                    var kickPlate = agent.GetComponentInChildren<SoccerKickPlate>(true);
                    Require(kickPlate != null && kickPlate.Owner == agent, $"{agent.name} kick plate owner mismatch.");
                    var plateParts = kickPlate.GetComponentsInChildren<Collider>(true);
                    Require(plateParts.Length == 3, $"{agent.name} kick plate must contain a center and two retaining wings.");
                    Require(plateParts.All(part => part.gameObject.layer == kickPlate.gameObject.layer),
                        $"{agent.name} kick plate parts must share their owner-filter layer.");
                    Require(plateParts.All(part => part.CompareTag(agent.Team == Team.Red ? "redAgent" : "navyAgent")),
                        $"{agent.name} kick plate parts must identify as their owning player team.");
                    Require(plateParts.All(part => Mathf.Approximately(part.transform.localScale.y, SoccerKickPlate.PlayerHeightRatio)),
                        $"{agent.name} kick plate height must be 30% of the original player height.");
                    var leftWing = kickPlate.transform.Find("KickPlateLeftWing");
                    var rightWing = kickPlate.transform.Find("KickPlateRightWing");
                    Require(leftWing != null && leftWing.localPosition.x < -0.6f
                        && Mathf.Abs(Mathf.DeltaAngle(leftWing.localEulerAngles.y, 45f)) < 0.1f,
                        $"{agent.name} left kick-plate wing must open outward.");
                    Require(rightWing != null && rightWing.localPosition.x > 0.6f
                        && Mathf.Abs(Mathf.DeltaAngle(rightWing.localEulerAngles.y, -45f)) < 0.1f,
                        $"{agent.name} right kick-plate wing must open outward.");
                    ValidateRaySensorContract(agent, OutputPrefabPath);
                    foreach (var sensor in agent.GetComponentsInChildren<RayPerceptionSensorComponent3D>(true))
                    {
                        Require(Mathf.Approximately(sensor.RayLength, SensorRange), $"{agent.name} ray range must be {SensorRange}.");
                        var sensorMask = (int)sensor.RayLayerMask;
                        Require((sensorMask & (1 << kickPlate.gameObject.layer)) == 0,
                            $"{agent.name} ray sensor must see through its own kick plate.");
                        foreach (var otherLayer in kickPlateLayers.Where(layer => layer != kickPlate.gameObject.layer))
                        {
                            Require((sensorMask & (1 << otherLayer)) != 0,
                                $"{agent.name} ray sensor must detect other players' kick plates.");
                        }
                    }
                }

                foreach (var team in new[] { Team.Red, Team.Navy })
                {
                    var teamAgents = agents.Where(agent => agent.Team == team).ToArray();
                    Require(teamAgents.Count(agent => agent.PositionRole == AgentSoccer.Position.DefenderKeeper) == 1,
                        $"{team} must have one defender-keeper.");
                    Require(teamAgents.Count(agent => agent.PositionRole == AgentSoccer.Position.Midfielder) == 2,
                        $"{team} must have two free midfielders.");
                    Require(teamAgents.Count(agent => agent.PositionRole == AgentSoccer.Position.Striker) == 1,
                        $"{team} must have one striker.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            foreach (var requiredScene in new[]
                     {
                         ScenePath, BaseScenePath, AttackScenePath, DefenseScenePath, PressScenePath, RuleScenePath
                     })
            {
                Require(File.Exists(Path.GetFullPath(requiredScene)), $"Required Soccer scene is missing: {requiredScene}");
            }

            var definitions = new[]
            {
                AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(BaseDefinitionPath),
                AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(Root + "/Teams/" + AttackFolder + "/Profiles/AttackTeamDefinition.asset"),
                AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(Root + "/Teams/" + DefenseFolder + "/Profiles/DefenseTeamDefinition.asset"),
                AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(Root + "/Teams/" + PressFolder + "/Profiles/PressTeamDefinition.asset"),
                AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(Root + "/Teams/" + RuleFolder + "/Profiles/RuleTeamDefinition.asset")
            };
            Require(definitions.All(definition => definition != null), "All five Soccer team definitions must exist.");
            Require(definitions.All(definition => definition.PolicyContractVersion == SoccerTeamDefinition.CurrentPolicyContractVersion),
                "All Soccer teams must use policy contract v2.");
            Require(definitions.Take(4).All(definition => definition.UsesNeuralPolicy),
                "Base, Attack, Defense and Press must use neural policies.");
            Require(!definitions[4].UsesNeuralPolicy && definitions[4].InferenceModel == null,
                "Rule must use only the rule controller without an ONNX model.");
            Require(definitions.Take(4).All(definition => definition.InferenceModel == null
                || SoccerModelNaming.TryParse(definition.InferenceModel.name, out _, out _, out _)),
                $"Neural model names must use {SoccerModelNaming.FileNamePattern}.");
            Require(definitions.All(definition => HasCommonBasicSkillRewards(definition.RewardProfile)),
                "All five RewardProfiles must share the common pass, carry and safe-kick reward contract.");
            Require(!Directory.Exists(Path.GetFullPath(Root + "/Teams/" + RuleFolder + "/Training"))
                && !Directory.Exists(Path.GetFullPath(Root + "/Teams/" + RuleFolder + "/Models")),
                "Rule must not contain Trainer or model directories.");

            var workspacePrefabs = new[]
            {
                BaseEnvironmentPrefabPath,
                AttackEnvironmentPrefabPath,
                DefenseEnvironmentPrefabPath,
                PressEnvironmentPrefabPath,
                RuleEnvironmentPrefabPath
            };
            Require(workspacePrefabs.All(path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null),
                "All five workspace environment prefabs must exist.");
            Require(workspacePrefabs.Select(AssetDatabase.AssetPathToGUID).Distinct().Count() == workspacePrefabs.Length,
                "Every workspace environment prefab must have an independent GUID.");
            Require(workspacePrefabs.All(path => PrefabUtility.GetPrefabAssetType(
                    AssetDatabase.LoadAssetAtPath<GameObject>(path)) == PrefabAssetType.Regular),
                "Workspace environments must be independent regular prefabs, not variants.");
            var commonArenaPrefabs = new[] { OutputPrefabPath }.Concat(workspacePrefabs).ToArray();
            Require(commonArenaPrefabs.Select(BuildPolicyContractSnapshot).Distinct().Count() == 1,
                "The common template and all workspace prefabs must keep identical arena, visual, observation and physics contracts.");

            var workspaceScenes = new[]
            {
                ScenePath,
                BaseScenePath,
                AttackScenePath,
                DefenseScenePath,
                PressScenePath,
                RuleScenePath
            };
            Require(workspaceScenes.Select(BuildSceneSettingsSnapshot).Distinct().Count() == 1,
                "Workspace scenes must keep identical movement settings and team materials.");

            ValidateWorkspaceEnvironment(BaseEnvironmentPrefabPath, definitions[0], definitions[0], true, true, false);
            ValidateWorkspaceEnvironment(AttackEnvironmentPrefabPath, definitions[1], definitions[0], true, false, false);
            ValidateWorkspaceEnvironment(DefenseEnvironmentPrefabPath, definitions[2], definitions[0], true, false, false);
            ValidateWorkspaceEnvironment(PressEnvironmentPrefabPath, definitions[3], definitions[0], true, false, false);
            ValidateWorkspaceEnvironment(RuleEnvironmentPrefabPath, definitions[4], definitions[0], false, false, true);

            ValidateSceneWiring(ScenePath, BaseEnvironmentPrefabPath);
            ValidateSceneWiring(BaseScenePath, BaseEnvironmentPrefabPath);
            ValidateSceneWiring(AttackScenePath, AttackEnvironmentPrefabPath);
            ValidateSceneWiring(DefenseScenePath, DefenseEnvironmentPrefabPath);
            ValidateSceneWiring(PressScenePath, PressEnvironmentPrefabPath);
            ValidateSceneWiring(RuleScenePath, RuleEnvironmentPrefabPath);

            var ruleScene = EditorSceneManager.OpenScene(RuleScenePath, OpenSceneMode.Single);
            var ruleRoots = ruleScene.GetRootGameObjects();
            var ruleSetup = ruleRoots.Select(rootObject => rootObject.GetComponentInChildren<SoccerMatchSetup>(true))
                .FirstOrDefault(setup => setup != null);
            var ruleControllers = ruleRoots
                .SelectMany(rootObject => rootObject.GetComponentsInChildren<RuleBasedSoccerController>(true))
                .ToArray();
            Require(ruleSetup != null && !ruleSetup.TrainRed && !ruleSetup.TrainNavy,
                "Rule scene must disable training for both teams.");
            Require(ruleControllers.Length == 4
                && ruleControllers.All(controller => controller.GetComponent<AgentSoccer>().Team == Team.Red),
                "Rule scene must attach one FSM controller to every Red player.");
            Require(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath) != null, "Soccer HUD UXML is missing.");
            Debug.Log("Soccer validation passed: v2 actions, 4v4 roles, three neural tactics and one rule workspace.");
        }

        static void ValidateActiveStadiumAssets()
        {
            var definitions = new[]
            {
                AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(BaseDefinitionPath),
                AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(Root + "/Teams/" + AttackFolder + "/Profiles/AttackTeamDefinition.asset"),
                AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(Root + "/Teams/" + DefenseFolder + "/Profiles/DefenseTeamDefinition.asset"),
                AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(Root + "/Teams/" + PressFolder + "/Profiles/PressTeamDefinition.asset"),
                AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(Root + "/Teams/" + RuleFolder + "/Profiles/RuleTeamDefinition.asset")
            };
            Require(definitions.All(definition => definition != null), "All five Soccer TeamDefinitions must exist.");
            Require(definitions.All(definition => definition.PolicyContractVersion == SoccerTeamDefinition.CurrentPolicyContractVersion),
                "All Soccer teams must use policy contract v2.");
            Require(definitions.Take(4).All(definition => definition.UsesNeuralPolicy),
                "Base, Attack, Defense and Press must use neural policies.");
            Require(!definitions[4].UsesNeuralPolicy && definitions[4].InferenceModel == null,
                "Rule must use only the rule controller without an ONNX model.");
            Require(definitions.All(definition => HasCommonBasicSkillRewards(definition.RewardProfile)),
                "All five RewardProfiles must keep the shared reward contract.");

            var workspacePrefabs = new[]
            {
                StadiumBaseEnvironmentPrefabPath,
                StadiumAttackEnvironmentPrefabPath,
                StadiumDefenseEnvironmentPrefabPath,
                StadiumPressEnvironmentPrefabPath,
                StadiumRuleEnvironmentPrefabPath
            };
            Require(workspacePrefabs.All(path => AssetDatabase.LoadAssetAtPath<GameObject>(path) != null),
                "All five active Stadium environment prefabs must exist.");
            Require(workspacePrefabs.Select(AssetDatabase.AssetPathToGUID).Distinct().Count() == workspacePrefabs.Length,
                "Every Stadium workspace prefab must have an independent GUID.");
            Require(workspacePrefabs.All(path => PrefabUtility.GetPrefabAssetType(
                    AssetDatabase.LoadAssetAtPath<GameObject>(path)) == PrefabAssetType.Regular),
                "Stadium workspaces must be independent regular prefabs, not variants.");
            Require(workspacePrefabs.Select(BuildPolicyContractSnapshot).Distinct().Count() == 1,
                "All five active Stadium prefabs must keep identical geometry, visual, observation and physics contracts.");

            var workspaceScenes = new[]
            {
                StadiumBaseScenePath,
                StadiumAttackScenePath,
                StadiumDefenseScenePath,
                StadiumPressScenePath,
                StadiumRuleScenePath
            };
            Require(workspaceScenes.All(path => File.Exists(Path.GetFullPath(path))),
                "All five active Stadium scenes must exist.");
            Require(workspaceScenes.Select(BuildSceneSettingsSnapshot).Distinct().Count() == 1,
                "All five Stadium scenes must keep identical movement, camera and team-material settings.");

            ValidateWorkspaceEnvironment(StadiumBaseEnvironmentPrefabPath, definitions[0], definitions[0], true, true, false);
            ValidateWorkspaceEnvironment(StadiumAttackEnvironmentPrefabPath, definitions[1], definitions[0], true, false, false);
            ValidateWorkspaceEnvironment(StadiumDefenseEnvironmentPrefabPath, definitions[2], definitions[0], true, false, false);
            ValidateWorkspaceEnvironment(StadiumPressEnvironmentPrefabPath, definitions[3], definitions[0], true, false, false);
            ValidateWorkspaceEnvironment(StadiumRuleEnvironmentPrefabPath, definitions[4], definitions[0], false, false, true);

            ValidateSceneWiring(StadiumBaseScenePath, StadiumBaseEnvironmentPrefabPath);
            ValidateSceneWiring(StadiumAttackScenePath, StadiumAttackEnvironmentPrefabPath);
            ValidateSceneWiring(StadiumDefenseScenePath, StadiumDefenseEnvironmentPrefabPath);
            ValidateSceneWiring(StadiumPressScenePath, StadiumPressEnvironmentPrefabPath);
            ValidateSceneWiring(StadiumRuleScenePath, StadiumRuleEnvironmentPrefabPath);

            var activeSoccerScenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled && scene.path.StartsWith(Root + "/", StringComparison.Ordinal))
                .Select(scene => scene.path)
                .ToArray();
            Require(activeSoccerScenes.SequenceEqual(workspaceScenes),
                "Build Settings must enable only the five Stadium Soccer scenes in Base/Attack/Defense/Press/Rule order.");

            var ruleScene = EditorSceneManager.OpenScene(StadiumRuleScenePath, OpenSceneMode.Single);
            var ruleRoots = ruleScene.GetRootGameObjects();
            var ruleSetup = ruleRoots.SelectMany(rootObject => rootObject.GetComponentsInChildren<SoccerMatchSetup>(true)).Single();
            var ruleControllers = ruleRoots.SelectMany(rootObject => rootObject.GetComponentsInChildren<RuleBasedSoccerController>(true)).ToArray();
            Require(!ruleSetup.TrainRed && !ruleSetup.TrainNavy,
                "Rule Stadium must disable training for both teams.");
            Require(ruleControllers.Length == 4
                && ruleControllers.All(controller => controller.GetComponent<AgentSoccer>().Team == Team.Red),
                "Rule Stadium must attach one FSM controller to every Red player.");
            Require(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath) != null,
                "Soccer HUD UXML is missing.");
            Debug.Log("ACTIVE STADIUM VALIDATION PASS: Base, Attack, Defense, Press and Rule share one Stadium contract.");
        }

        static void ValidateWorkspaceEnvironment(
            string prefabPath,
            SoccerTeamDefinition expectedRedDefinition,
            SoccerTeamDefinition expectedNavyDefinition,
            bool expectedTrainRed,
            bool expectedTrainNavy,
            bool expectsRuleControllers)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var environment = root.GetComponent<SoccerEnvController>();
                var setup = root.GetComponent<SoccerMatchSetup>();
                var agents = root.GetComponentsInChildren<AgentSoccer>(true);
                Require(environment != null && environment.StartsInAIMode,
                    $"Workspace must start in AI mode: {prefabPath}");
                Require(setup != null && setup.RedTeam == expectedRedDefinition
                    && setup.NavyTeam == expectedNavyDefinition,
                    $"Workspace TeamDefinition mismatch: {prefabPath}");
                Require(setup.TrainRed == expectedTrainRed && setup.TrainNavy == expectedTrainNavy,
                    $"Workspace training flags mismatch: {prefabPath}");
                Require(new[] { setup.RedModelOverride, setup.NavyModelOverride }
                        .Where(model => model != null)
                        .All(model => SoccerModelNaming.TryParse(model.name, out _, out _, out _)),
                    $"Workspace model override names must use {SoccerModelNaming.FileNamePattern}: {prefabPath}");
                Require(root.GetComponent<SoccerRewardEngine>() != null,
                    $"Workspace reward engine is missing: {prefabPath}");
                Require(agents.Length == 8, $"Workspace must contain eight players: {prefabPath}");
                ValidateCommonGeometry(root, prefabPath);
                ValidateGoalOcclusionContract(root, prefabPath);

                foreach (var agent in agents)
                {
                    var behavior = agent.GetComponent<BehaviorParameters>();
                    Require(behavior != null && behavior.Model == null,
                        $"Per-player model assignment is forbidden; use the workspace model slot: {prefabPath}/{agent.name}");
                    Require(behavior.TeamId == (int)agent.Team,
                        $"Agent team and BehaviorParameters.TeamId must match: {prefabPath}/{agent.name}");
                    var expectedBehaviorType = setup.IsTrainable(agent.Team)
                        ? BehaviorType.Default
                        : BehaviorType.HeuristicOnly;
                    Require(behavior.BehaviorType == expectedBehaviorType,
                        $"Non-trainable teams must be serialized outside Default to avoid Trainer registration: {prefabPath}/{agent.name}");
                    Require(behavior.BrainParameters.VectorObservationSize == AgentSoccer.VectorObservationSize
                        && behavior.BrainParameters.ActionSpec.BranchSizes.SequenceEqual(new[] { 3, 3, 3, 3 }),
                        $"Policy v2 vector/action contract mismatch: {prefabPath}/{agent.name}");
                    var rayObservationSize = agent.GetComponentsInChildren<RayPerceptionSensorComponent3D>(true)
                        .Sum(sensor => (sensor.RaysPerDirection * 2 + 1)
                            * (sensor.DetectableTags.Count + 2)
                            * sensor.ObservationStacks);
                    Require(AgentSoccer.VectorObservationSize + rayObservationSize == 379,
                        $"Policy v2 total observation size must be 379: {prefabPath}/{agent.name}");
                    ValidateRaySensorContract(agent, prefabPath);
                }

                var ruleControllers = root.GetComponentsInChildren<RuleBasedSoccerController>(true);
                Require(expectsRuleControllers
                        ? ruleControllers.Length == 4
                            && ruleControllers.All(controller => controller.GetComponent<AgentSoccer>().Team == Team.Red)
                        : ruleControllers.Length == 0,
                    $"Rule controller isolation mismatch: {prefabPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void ValidateGoalOcclusionContract(GameObject root, string assetPath)
        {
            if (root.GetComponent<SoccerEnvController>()?.ArenaGeometry != null)
            {
                var stadiumGoals = root.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(renderer => renderer.name.StartsWith("SM_S_Gate", StringComparison.Ordinal))
                    .OrderBy(renderer => renderer.bounds.center.x)
                    .ToArray();
                Require(stadiumGoals.Length == 2,
                    $"Both Demo Stadium goals are required: {assetPath}");
                Require(stadiumGoals[0].CompareTag("redGoal") && stadiumGoals[1].CompareTag("navyGoal"),
                    $"Demo Stadium goal tags must follow the defending teams: {assetPath}");
                Require(stadiumGoals.All(renderer => renderer.GetComponents<Collider>().Any(collider => collider.enabled)
                    && renderer.sharedMaterial != null
                    && renderer.sharedMaterial.name.StartsWith("Stadium", StringComparison.Ordinal)),
                    $"Demo Stadium goals need visible team materials and physical colliders: {assetPath}");
                Require(stadiumGoals.Select(renderer => renderer.sharedMaterial).Distinct().Count() == 2,
                    $"Red and Navy Stadium goals must use isolated materials: {assetPath}");
                return;
            }

            var goalRenderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => GoalRendererNames.Contains(renderer.gameObject.name))
                .ToArray();
            Require(goalRenderers.Length == GoalRendererNames.Count,
                $"All six goal renderers are required: {assetPath}");
            foreach (var renderer in goalRenderers)
            {
                var objectName = renderer.gameObject.name;
                var expectedTag = objectName.Contains("Red", StringComparison.Ordinal)
                    ? "redGoal"
                    : "navyGoal";
                var expectedColliderCount = objectName.StartsWith("GoalNet", StringComparison.Ordinal)
                    ? objectName.EndsWith("Outer", StringComparison.Ordinal) ? 0 : 4
                    : 1;
                Require(renderer.gameObject.CompareTag(expectedTag),
                    $"Goal renderer tag mismatch: {assetPath}/{objectName}");
                Require(renderer.GetComponents<Collider>().Length == expectedColliderCount,
                    $"Goal collider count mismatch: {assetPath}/{objectName}");
                Require(renderer.sharedMaterials.All(material => material != null
                        && material.name.StartsWith("Goal", StringComparison.Ordinal)),
                    $"Goal renderers must use dedicated goal materials: {assetPath}/{objectName}");
                Require((GameObjectUtility.GetStaticEditorFlags(renderer.gameObject)
                        & StaticEditorFlags.BatchingStatic) == 0,
                    $"Fading goal renderers cannot use static batching: {assetPath}/{objectName}");
            }

            var playerMaterials = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.gameObject.name.StartsWith("AgentCube_", StringComparison.Ordinal))
                .SelectMany(renderer => renderer.sharedMaterials)
                .ToHashSet();
            Require(goalRenderers.SelectMany(renderer => renderer.sharedMaterials)
                    .All(material => !playerMaterials.Contains(material)),
                $"Goal and player materials must be isolated: {assetPath}");
        }

        static string BuildPolicyContractSnapshot(string prefabPath)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var builder = new StringBuilder();
                var environment = root.GetComponent<SoccerEnvController>();
                var field = root.transform.Find("Field");
                var ballBody = environment.ball.GetComponent<Rigidbody>();
                builder.Append(environment.matchDurationSeconds).Append('|')
                    .Append(environment.goalResetDelaySeconds).Append('|')
                    .Append(environment.ControlledKickPower).Append('|')
                    .Append(environment.StrongKickPower).Append('|')
                    .Append(field != null ? field.localPosition.ToString() : "<stadium>").Append('|')
                    .Append(field != null ? field.localRotation.ToString() : "<stadium>").Append('|')
                    .Append(field != null ? field.localScale.ToString() : "<stadium>").Append('|')
                    .Append(environment.ball.transform.localPosition).Append('|')
                    .Append(ballBody.mass).Append('|')
                    .Append(ballBody.linearDamping).Append('|')
                    .Append(ballBody.angularDamping).Append('|');
                var arena = environment.ArenaGeometry;
                builder.Append(arena != null ? arena.HalfLength : -1f).Append('|')
                    .Append(arena != null ? arena.HalfWidth : -1f).Append('|')
                    .Append(arena != null ? arena.GoalHalfWidth : -1f).Append('|')
                    .Append(arena != null ? arena.GoalHeight : -1f).Append('|')
                    .Append(arena != null ? arena.GoalDepth : -1f).Append('|')
                    .Append(arena != null ? arena.CornerRadius : -1f).Append('|');

                foreach (var itemTransform in root.GetComponentsInChildren<Transform>(true)
                             .OrderBy(item => AnimationUtility.CalculateTransformPath(item, root.transform),
                                 StringComparer.Ordinal))
                {
                    var objectPath = AnimationUtility.CalculateTransformPath(itemTransform, root.transform);
                    var commonComponentTypes = itemTransform.GetComponents<Component>()
                        .Where(component => component == null || !IsTacticalPrefabComponent(component))
                        .Select(component => component == null ? "<missing>" : component.GetType().FullName)
                        .OrderBy(typeName => typeName, StringComparer.Ordinal);
                    builder.Append("OBJECT:")
                        .Append(objectPath).Append(':')
                        .Append(itemTransform.gameObject.activeSelf).Append(':')
                        .Append(itemTransform.gameObject.tag).Append(':')
                        .Append(itemTransform.gameObject.layer).Append(':')
                        .Append((int)GameObjectUtility.GetStaticEditorFlags(itemTransform.gameObject)).Append(':')
                        .Append(itemTransform.localPosition).Append(':')
                        .Append(itemTransform.localRotation).Append(':')
                        .Append(itemTransform.localScale).Append(':')
                        .Append(string.Join(",", commonComponentTypes)).Append('|');
                }

                foreach (var meshFilter in root.GetComponentsInChildren<MeshFilter>(true)
                             .OrderBy(filter => AnimationUtility.CalculateTransformPath(filter.transform, root.transform),
                                 StringComparer.Ordinal))
                {
                    builder.Append("MESH:")
                        .Append(AnimationUtility.CalculateTransformPath(meshFilter.transform, root.transform)).Append(':')
                        .Append(GetAssetIdentity(meshFilter.sharedMesh)).Append('|');
                }

                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true)
                             .OrderBy(item => AnimationUtility.CalculateTransformPath(item.transform, root.transform),
                                 StringComparer.Ordinal)
                             .ThenBy(item => item.GetType().Name, StringComparer.Ordinal))
                {
                    builder.Append("RENDERER:")
                        .Append(AnimationUtility.CalculateTransformPath(renderer.transform, root.transform)).Append(':')
                        .Append(renderer.GetType().FullName).Append(':')
                        .Append(renderer.enabled).Append(':')
                        .Append((int)renderer.shadowCastingMode).Append(':')
                        .Append(renderer.receiveShadows).Append(':')
                        .Append((int)renderer.lightProbeUsage).Append(':')
                        .Append((int)renderer.reflectionProbeUsage).Append(':')
                        .Append(renderer.sortingLayerID).Append(':')
                        .Append(renderer.sortingOrder).Append(':')
                        .Append(string.Join(",", renderer.sharedMaterials.Select(GetAssetIdentity))).Append('|');
                }

                foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)
                             .OrderBy(body => AnimationUtility.CalculateTransformPath(body.transform, root.transform),
                                 StringComparer.Ordinal))
                {
                    builder.Append("BODY:")
                        .Append(AnimationUtility.CalculateTransformPath(body.transform, root.transform)).Append(':')
                        .Append(body.mass).Append(':')
                        .Append(body.linearDamping).Append(':')
                        .Append(body.angularDamping).Append(':')
                        .Append(body.useGravity).Append(':')
                        .Append(body.isKinematic).Append(':')
                        .Append((int)body.interpolation).Append(':')
                        .Append((int)body.collisionDetectionMode).Append(':')
                        .Append((int)body.constraints).Append('|');
                }

                foreach (var collider in root.GetComponentsInChildren<Collider>(true)
                             .OrderBy(collider => AnimationUtility.CalculateTransformPath(collider.transform, root.transform),
                                 StringComparer.Ordinal)
                             .ThenBy(collider => collider.GetType().Name, StringComparer.Ordinal))
                {
                    builder.Append("COLLIDER:")
                        .Append(AnimationUtility.CalculateTransformPath(collider.transform, root.transform)).Append(':')
                        .Append(collider.GetType().Name).Append(':')
                        .Append(collider.gameObject.tag).Append(':')
                        .Append(collider.gameObject.layer).Append(':')
                        .Append(collider.transform.localPosition).Append(':')
                        .Append(collider.transform.localRotation).Append(':')
                        .Append(collider.transform.localScale).Append(':')
                        .Append(collider.enabled).Append(':')
                        .Append(collider.isTrigger).Append(':')
                        .Append(AssetDatabase.GetAssetPath(collider.sharedMaterial)).Append(':');
                    AppendColliderShape(builder, collider);
                    builder.Append('|');
                }

                foreach (var item in environment.AgentsList)
                {
                    var agent = item.Agent;
                    var behavior = agent.GetComponent<BehaviorParameters>();
                    var requester = agent.GetComponent<DecisionRequester>();
                    var body = agent.GetComponent<Rigidbody>();
                    builder.Append(agent.name).Append(':')
                        .Append((int)agent.Team).Append(':')
                        .Append(behavior.TeamId).Append(':')
                        .Append((int)agent.PositionRole).Append(':')
                        .Append(agent.StartingPosition).Append(':')
                        .Append(behavior.BrainParameters.VectorObservationSize).Append(':')
                        .Append(string.Join(",", behavior.BrainParameters.ActionSpec.BranchSizes)).Append(':')
                        .Append(requester.DecisionPeriod).Append(':')
                        .Append(requester.TakeActionsBetweenDecisions).Append(':')
                        .Append(body.mass).Append(':');

                    foreach (var sensor in agent.GetComponentsInChildren<RayPerceptionSensorComponent3D>(true)
                                 .OrderBy(sensor => sensor.SensorName, StringComparer.Ordinal))
                    {
                        builder.Append(sensor.SensorName).Append(',')
                            .Append(string.Join(",", sensor.DetectableTags)).Append(',')
                            .Append(sensor.RaysPerDirection).Append(',')
                            .Append(sensor.MaxRayDegrees).Append(',')
                            .Append(sensor.SphereCastRadius).Append(',')
                            .Append(sensor.RayLength).Append(',')
                            .Append(sensor.ObservationStacks).Append(',')
                            .Append(sensor.AlternatingRayOrder).Append(',')
                            .Append((int)sensor.RayLayerMask).Append(';');
                    }
                }

                return builder.ToString();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void ValidateCommonGeometry(GameObject root, string assetPath)
        {
            var environment = root.GetComponent<SoccerEnvController>();
            var arena = environment != null ? environment.ArenaGeometry : null;
            if (arena != null)
            {
                Require(Mathf.Abs(arena.HalfLength - SoccerArenaGeometry.StadiumHalfLength) < 0.001f
                    && Mathf.Abs(arena.HalfWidth - SoccerArenaGeometry.StadiumHalfWidth) < 0.01f
                    && Mathf.Abs(arena.GoalHalfWidth - SoccerArenaGeometry.StadiumGoalHalfWidth) < 0.01f,
                    $"Active Stadium dimensions changed unexpectedly: {assetPath}");
                Require(Mathf.Approximately(environment.ControlledKickPower, 2000f)
                    && Mathf.Approximately(environment.StrongKickPower, 5000f),
                    $"Active Stadium kick powers must remain pass=2000 and shot=5000: {assetPath}");
                var stadiumBalls = root.GetComponentsInChildren<SoccerBallController>(true);
                Require(stadiumBalls.Length == 1, $"Exactly one Stadium ball is required: {assetPath}");
                var stadiumBall = stadiumBalls[0];
                Require(Vector3.Distance(stadiumBall.transform.lossyScale, Vector3.one * 0.012705f) < 0.00001f
                    && stadiumBall.EnforcesStadiumPlanarMotion,
                    $"Stadium ball size or planar rolling contract changed: {assetPath}");
                var walls = root.GetComponentsInChildren<BoxCollider>(true)
                    .Where(collider => collider.CompareTag("wall"))
                    .ToArray();
                Require(walls.Length == 18 && walls.All(wall => !wall.isTrigger),
                    $"Stadium must contain six straight and twelve rounded-corner walls: {assetPath}");
                Require(root.GetComponentsInChildren<MeshRenderer>(true)
                        .Count(renderer => renderer.name.StartsWith("SM_S_Gate", StringComparison.Ordinal)) == 2,
                    $"Active Stadium must use both Demo goals: {assetPath}");
                return;
            }

            var goalRenderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => GoalRendererNames.Contains(renderer.gameObject.name))
                .ToArray();
            Require(goalRenderers.Length == GoalRendererNames.Count,
                $"All six goal renderers are required: {assetPath}");
            Require(goalRenderers.All(renderer =>
                    Mathf.Approximately(renderer.transform.localScale.x, 1f)
                    && Mathf.Approximately(renderer.transform.localScale.y, 1f)
                    && Mathf.Approximately(renderer.transform.localScale.z, GoalLateralScale)),
                $"Goal width must use the common 70% lateral scale: {assetPath}");

            var balls = root.GetComponentsInChildren<SoccerBallController>(true);
            Require(balls.Length == 1, $"Exactly one active Soccer ball is required: {assetPath}");
            var ball = balls[0];
            Require(Mathf.Approximately(ball.transform.localScale.x, BallUniformScale)
                && Mathf.Approximately(ball.transform.localScale.y, BallUniformScale)
                && Mathf.Approximately(ball.transform.localScale.z, BallUniformScale),
                $"Ball size must use the common 70% scale: {assetPath}");
            Require(Mathf.Approximately(ball.transform.localPosition.y, BallResetHeight),
                $"Ball reset height must follow its 70% size: {assetPath}");
            var ballBody = ball.GetComponent<Rigidbody>();
            Require(ball.GetComponent<SphereCollider>() != null
                && ballBody != null
                && Mathf.Approximately(ballBody.mass, 3f)
                && Mathf.Approximately(ballBody.linearDamping, 1f)
                && Mathf.Approximately(ballBody.angularDamping, 1f),
                $"Ball collider and Rigidbody contract changed unexpectedly: {assetPath}");
        }

        static void ValidateRaySensorContract(AgentSoccer agent, string assetPath)
        {
            var sensors = agent.GetComponentsInChildren<RayPerceptionSensorComponent3D>(true);
            Require(sensors.Length == 2,
                $"Each player must have exactly one front and one rear Ray sensor: {assetPath}/{agent.name}");
            var expectedPrefix = agent.Team == Team.Red ? "Red" : "Navy";
            var frontName = $"{expectedPrefix}RayPerceptionSensor";
            var rearName = $"{expectedPrefix}RayPerceptionSensorReverse";
            var front = sensors.FirstOrDefault(sensor => sensor.SensorName == frontName);
            var rear = sensors.FirstOrDefault(sensor => sensor.SensorName == rearName);
            Require(sensors.Count(sensor => sensor.SensorName == frontName) == 1
                && sensors.Count(sensor => sensor.SensorName == rearName) == 1,
                $"Front/rear Ray sensor names are part of the policy contract: {assetPath}/{agent.name}");
            var expectedTags = agent.Team == Team.Red
                ? new[] { "ball", "redGoal", "navyGoal", "wall", "redAgent", "navyAgent" }
                : new[] { "ball", "navyGoal", "redGoal", "wall", "navyAgent", "redAgent" };
            Require(front.DetectableTags.SequenceEqual(expectedTags)
                && rear.DetectableTags.SequenceEqual(expectedTags),
                $"Ray detectable tag order mismatch: {assetPath}/{agent.name}");
            Require(front.RaysPerDirection == 5
                && Mathf.Approximately(front.MaxRayDegrees, 60f)
                && front.ObservationStacks == 3,
                $"Front Ray contract mismatch: {assetPath}/{agent.name}");
            Require(rear.RaysPerDirection == 1
                && Mathf.Approximately(rear.MaxRayDegrees, 45f)
                && Mathf.Approximately(rear.SphereCastRadius, 0.5f)
                && Mathf.Approximately(rear.RayLength, SensorRange)
                && rear.ObservationStacks == 3
                && Mathf.Abs(Mathf.DeltaAngle(rear.transform.localEulerAngles.y, 180f)) < 0.1f,
                $"Rear Ray must remain one 180-degree sensor with three rays: {assetPath}/{agent.name}");
        }

        static bool IsTacticalPrefabComponent(Component component)
        {
            return component is SoccerMatchSetup
                || component is SoccerRewardEngine
                || component is SoccerTeamRewardPolicyBase
                || component is ISoccerRuleController;
        }

        static string GetAssetIdentity(UnityEngine.Object asset)
        {
            if (asset == null)
            {
                return "<null>";
            }

            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId)
                ? $"{guid}:{localId}"
                : $"{asset.GetType().FullName}:{asset.name}";
        }

        static string BuildSceneSettingsSnapshot(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var settings = roots
                .SelectMany(rootObject => rootObject.GetComponentsInChildren<SoccerSettings>(true))
                .Single();
            var environment = roots
                .SelectMany(rootObject => rootObject.GetComponentsInChildren<SoccerEnvController>(true))
                .Single();
            var camera = roots
                .SelectMany(rootObject => rootObject.GetComponentsInChildren<Camera>(true))
                .Single(item => item.CompareTag("MainCamera"));
            var fader = camera.GetComponent<SoccerGoalOcclusionFader>();
            return string.Join("|",
                settings.agentRunSpeed,
                settings.maximumPlanarSpeed,
                settings.rotationSpeed,
                settings.humanAcceleration,
                settings.humanDeceleration,
                settings.randomizePlayersTeamForTraining,
                AssetDatabase.GetAssetPath(settings.redMaterial),
                AssetDatabase.GetAssetPath(settings.navyMaterial),
                camera.transform.position,
                camera.transform.rotation,
                camera.fieldOfView,
                camera.nearClipPlane,
                camera.farClipPlane,
                fader != null,
                fader != null && fader.Environment == environment,
                fader != null ? fader.OccludedAlpha : -1f,
                fader != null ? fader.FadeSpeed : -1f,
                fader != null ? fader.BoundsPadding : -1f);
        }

        static void AppendColliderShape(StringBuilder builder, Collider collider)
        {
            switch (collider)
            {
                case BoxCollider box:
                    builder.Append(box.center).Append(':').Append(box.size);
                    break;
                case SphereCollider sphere:
                    builder.Append(sphere.center).Append(':').Append(sphere.radius);
                    break;
                case CapsuleCollider capsule:
                    builder.Append(capsule.center).Append(':')
                        .Append(capsule.radius).Append(':')
                        .Append(capsule.height).Append(':')
                        .Append(capsule.direction);
                    break;
                case MeshCollider mesh:
                    builder.Append(AssetDatabase.GetAssetPath(mesh.sharedMesh)).Append(':')
                        .Append(mesh.convex);
                    break;
            }
        }

        static void ValidateSceneWiring(string scenePath, string expectedPrefabPath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var environment = roots
                .SelectMany(root => root.GetComponentsInChildren<SoccerEnvController>(true))
                .SingleOrDefault();
            Require(environment != null, $"Scene environment is missing: {scenePath}");
            var settings = roots
                .SelectMany(root => root.GetComponentsInChildren<SoccerSettings>(true))
                .SingleOrDefault();
            Require(settings != null
                && Mathf.Approximately(settings.agentRunSpeed, SoccerSettings.DefaultAgentRunSpeed)
                && Mathf.Approximately(settings.maximumPlanarSpeed, SoccerSettings.DefaultMaximumPlanarSpeed)
                && Mathf.Approximately(settings.rotationSpeed, SoccerSettings.DefaultRotationSpeed)
                && Mathf.Approximately(settings.humanAcceleration, SoccerSettings.DefaultHumanAcceleration)
                && Mathf.Approximately(settings.humanDeceleration, SoccerSettings.DefaultHumanDeceleration),
                $"Human movement settings mismatch: {scenePath}");
            var instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(environment.gameObject);
            Require(instanceRoot != null
                && string.Equals(
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instanceRoot),
                    expectedPrefabPath,
                    StringComparison.Ordinal),
                $"Scene must use its own workspace prefab: {scenePath}");

            var document = roots.SelectMany(root => root.GetComponentsInChildren<UIDocument>(true)).SingleOrDefault();
            var hud = roots.SelectMany(root => root.GetComponentsInChildren<SoccerHudController>(true)).SingleOrDefault();
            Require(document != null
                && document.panelSettings == AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath)
                && document.visualTreeAsset == AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath),
                $"HUD PanelSettings or UXML reference is missing: {scenePath}");
            Require(hud != null && hud.Environment == environment,
                $"HUD environment reference is missing: {scenePath}");
            var mainCamera = roots.SelectMany(root => root.GetComponentsInChildren<Camera>(true))
                .SingleOrDefault(camera => camera.CompareTag("MainCamera"));
            var fader = mainCamera != null ? mainCamera.GetComponent<SoccerGoalOcclusionFader>() : null;
            Require(fader != null && fader.Environment == environment,
                $"Goal occlusion fader reference is missing: {scenePath}");
        }

        public static void ValidateBatch()
        {
            ValidateGeneratedAssets();
        }

        public static string BuildCommonArenaSnapshotForTests(string prefabPath)
        {
            return BuildPolicyContractSnapshot(prefabPath);
        }

        [MenuItem("Tools/Soccer/Build Windows Player")]
        public static void BuildWindowsPlayer()
        {
            BuildAll();
            Directory.CreateDirectory(Path.GetDirectoryName(WindowsBuildPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { StadiumBaseScenePath },
                locationPathName = WindowsBuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Soccer Windows build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            }

            Debug.Log($"Soccer Windows build succeeded: {WindowsBuildPath} ({report.summary.totalSize} bytes).");
        }

        public static void BuildWindowsBatch()
        {
            BuildWindowsPlayer();
        }

        public static void StartBaseTrainingInEditor()
        {
            if (Application.isBatchMode)
            {
                throw new InvalidOperationException("Base editor training must run in a normal Unity Editor process.");
            }

            var scene = EditorSceneManager.OpenScene(StadiumBaseScenePath, OpenSceneMode.Single);
            Require(scene.IsValid(), $"Could not open the Base Stadium training scene: {StadiumBaseScenePath}");
            EditorApplication.delayCall += () =>
            {
                Debug.Log("Starting Base 4v4 v2 training scene in Play Mode.");
                EditorApplication.isPlaying = true;
            };
        }

        static GameObject Create4v4Prefab(VisualMaterialSet visualMaterials, IReadOnlyList<int> kickPlateLayers)
        {
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath) != null,
                "Copied source SoccerFieldTwos.prefab is missing.");
            var root = PrefabUtility.LoadPrefabContents(SourcePrefabPath);
            try
            {
                root.name = "SoccerField4v4";
                var field = root.transform.Find("Field");
                Require(field != null, "Source Field child is missing.");
                field.localScale = new Vector3(
                    field.localScale.x * FieldScaleMultiplier,
                    field.localScale.y,
                    field.localScale.z * FieldScaleMultiplier);

                var existingAgents = root.GetComponentsInChildren<AgentSoccer>(true).ToList();
                Require(existingAgents.Count == 4, $"Source prefab should contain four agents, found {existingAgents.Count}.");
                var redAgents = existingAgents.Where(IsRed).ToList();
                var navyAgents = existingAgents.Where(agent => !IsRed(agent)).ToList();
                Require(redAgents.Count == 2 && navyAgents.Count == 2, "Source prefab must contain two agents per team.");
                ExpandTeam(redAgents, 4);
                ExpandTeam(navyAgents, 4);

                var humanMarkerMaterial = CreateHumanMarkerMaterial();
                ConfigureTeam(redAgents, Team.Red, RedSpawns, 90f, humanMarkerMaterial,
                    visualMaterials.RedKickPlate, kickPlateLayers.Take(4).ToArray());
                ConfigureTeam(navyAgents, Team.Navy, NavySpawns, -90f, null,
                    visualMaterials.NavyKickPlate, kickPlateLayers.Skip(4).Take(4).ToArray());
                RemoveHeadbands(root);

                var controller = root.GetComponent<SoccerEnvController>();
                Require(controller != null, "Source environment controller is missing.");
                ApplyCommonMatchConfiguration(controller);
                controller.AgentsList = redAgents.Concat(navyAgents)
                    .Select(agent => new SoccerEnvController.PlayerInfo { Agent = agent })
                    .ToList();

                var ballController = root.GetComponentInChildren<SoccerBallController>(true);
                Require(ballController != null, "Source ball controller is missing.");
                ballController.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                ballController.Configure(root);
                controller.ball = ballController.gameObject;
                ApplyCommonArenaConfiguration(root, visualMaterials);

                // ML-Agents 4.0.3에 없는 원본 시연 기록 컴포넌트 제거.
                // 복제 선수 프리팹의 저장 오류 방지 목적.
                RemoveMissingScripts(root);

                var saved = PrefabUtility.SaveAsPrefabAsset(root, OutputPrefabPath);
                Require(saved != null, "Could not save SoccerField4v4.prefab.");
                return saved;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static GameObject CreateWorkspaceEnvironmentPrefab(
            GameObject template,
            VisualMaterialSet visualMaterials,
            string assetPath,
            string rootName,
            SoccerTeamDefinition redDefinition,
            SoccerTeamDefinition navyDefinition,
            Type redPolicyType,
            bool trainRed,
            bool trainNavy,
            Type redRuleControllerType = null)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            var created = existing == null;
            if (created)
            {
                var templatePath = AssetDatabase.GetAssetPath(template);
                Require(AssetDatabase.CopyAsset(templatePath, assetPath),
                    $"Could not create independent workspace prefab: {assetPath}");
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var root = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                if (created)
                {
                    root.name = rootName;
                }

                var environment = root.GetComponent<SoccerEnvController>();
                Require(environment != null, $"Workspace prefab is missing SoccerEnvController: {assetPath}");
                ApplyCommonMatchConfiguration(environment);
                ApplyCommonArenaConfiguration(root, visualMaterials);
                var policies = root.GetComponents<SoccerTeamRewardPolicyBase>().ToList();
                SoccerTeamRewardPolicyBase redPolicy;
                BaseRewardPolicy navyPolicy;
                if (redPolicyType == typeof(BaseRewardPolicy))
                {
                    var basePolicies = policies.OfType<BaseRewardPolicy>().ToList();
                    while (basePolicies.Count < 2)
                    {
                        basePolicies.Add(root.AddComponent<BaseRewardPolicy>());
                    }

                    redPolicy = basePolicies[0];
                    navyPolicy = basePolicies[1];
                }
                else
                {
                    redPolicy = policies.FirstOrDefault(policy => policy.GetType() == redPolicyType)
                        ?? (SoccerTeamRewardPolicyBase)root.AddComponent(redPolicyType);
                    navyPolicy = policies.OfType<BaseRewardPolicy>().FirstOrDefault()
                        ?? root.AddComponent<BaseRewardPolicy>();
                }

                var matchSetup = root.GetComponent<SoccerMatchSetup>() ?? root.AddComponent<SoccerMatchSetup>();
                var preservedRedModel = created ? null : matchSetup.RedModelOverride;
                var preservedNavyModel = created ? null : matchSetup.NavyModelOverride;
                matchSetup.Configure(
                    redDefinition,
                    navyDefinition,
                    redPolicy,
                    navyPolicy,
                    trainRed,
                    trainNavy,
                    preservedRedModel,
                    preservedNavyModel);
                foreach (var agent in root.GetComponentsInChildren<AgentSoccer>(true))
                {
                    var behavior = agent.GetComponent<BehaviorParameters>();
                    behavior.BehaviorType = matchSetup.IsTrainable(agent.Team)
                        ? BehaviorType.Default
                        : BehaviorType.HeuristicOnly;
                }
                var rewardEngine = root.GetComponent<SoccerRewardEngine>() ?? root.AddComponent<SoccerRewardEngine>();
                rewardEngine.Configure(environment, matchSetup);

                if (redRuleControllerType != null)
                {
                    Require(typeof(MonoBehaviour).IsAssignableFrom(redRuleControllerType),
                        "Rule controller must be a MonoBehaviour.");
                    foreach (var agent in root.GetComponentsInChildren<AgentSoccer>(true)
                                 .Where(agent => agent.Team == Team.Red))
                    {
                        if (agent.GetComponent(redRuleControllerType) == null)
                        {
                            agent.gameObject.AddComponent(redRuleControllerType);
                        }
                    }
                }

                var saved = PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                Require(saved != null, $"Could not save workspace prefab: {assetPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        static void BuildStadiumWorkspaces(
            SoccerTeamDefinition baseDefinition,
            SoccerTeamDefinition attackDefinition,
            SoccerTeamDefinition defenseDefinition,
            SoccerTeamDefinition pressDefinition,
            SoccerTeamDefinition ruleDefinition)
        {
            var definitions = new[]
            {
                baseDefinition, attackDefinition, defenseDefinition, pressDefinition, ruleDefinition
            };
            Require(definitions.All(definition => definition != null),
                "All five TeamDefinitions must exist before Stadium workspaces are built.");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(StadiumBaseEnvironmentPrefabPath) != null,
                $"Canonical Core Stadium prefab is missing: {StadiumBaseEnvironmentPrefabPath}");
            Require(File.Exists(Path.GetFullPath(StadiumBaseScenePath)),
                $"Canonical Core Stadium scene is missing: {StadiumBaseScenePath}");

            var baseEnvironment = CreateStadiumWorkspaceEnvironmentPrefab(
                StadiumBaseEnvironmentPrefabPath,
                StadiumBaseEnvironmentPrefabPath,
                "StadiumEnvironment_Base",
                baseDefinition,
                baseDefinition,
                typeof(BaseRewardPolicy),
                true,
                true);
            var attackEnvironment = CreateStadiumWorkspaceEnvironmentPrefab(
                StadiumBaseEnvironmentPrefabPath,
                StadiumAttackEnvironmentPrefabPath,
                "StadiumEnvironment_Attack",
                attackDefinition,
                baseDefinition,
                typeof(AttackRewardPolicy),
                true,
                false);
            var defenseEnvironment = CreateStadiumWorkspaceEnvironmentPrefab(
                StadiumBaseEnvironmentPrefabPath,
                StadiumDefenseEnvironmentPrefabPath,
                "StadiumEnvironment_Defense",
                defenseDefinition,
                baseDefinition,
                typeof(DefenseRewardPolicy),
                true,
                false);
            var pressEnvironment = CreateStadiumWorkspaceEnvironmentPrefab(
                StadiumBaseEnvironmentPrefabPath,
                StadiumPressEnvironmentPrefabPath,
                "StadiumEnvironment_Press",
                pressDefinition,
                baseDefinition,
                typeof(PressRewardPolicy),
                true,
                false);
            var ruleEnvironment = CreateStadiumWorkspaceEnvironmentPrefab(
                StadiumBaseEnvironmentPrefabPath,
                StadiumRuleEnvironmentPrefabPath,
                "StadiumEnvironment_Rule",
                ruleDefinition,
                baseDefinition,
                typeof(RuleRewardPolicy),
                false,
                false,
                typeof(RuleBasedSoccerController));

            CreateStadiumScene(StadiumBaseScenePath, baseEnvironment, false);
            CreateStadiumScene(StadiumAttackScenePath, attackEnvironment, true);
            CreateStadiumScene(StadiumDefenseScenePath, defenseEnvironment, true);
            CreateStadiumScene(StadiumPressScenePath, pressEnvironment, true);
            CreateStadiumScene(StadiumRuleScenePath, ruleEnvironment, true);
        }

        static GameObject CreateStadiumWorkspaceEnvironmentPrefab(
            string sourcePath,
            string assetPath,
            string rootName,
            SoccerTeamDefinition redDefinition,
            SoccerTeamDefinition navyDefinition,
            Type redPolicyType,
            bool trainRed,
            bool trainNavy,
            Type redRuleControllerType = null)
        {
            ModelAsset preservedRedModel = null;
            ModelAsset preservedNavyModel = null;
            if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) != null)
            {
                var existingRoot = PrefabUtility.LoadPrefabContents(assetPath);
                try
                {
                    var existingSetup = existingRoot.GetComponent<SoccerMatchSetup>();
                    if (existingSetup != null)
                    {
                        preservedRedModel = existingSetup.RedModelOverride;
                        preservedNavyModel = existingSetup.NavyModelOverride;
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(existingRoot);
                }
            }

            var root = PrefabUtility.LoadPrefabContents(sourcePath);
            try
            {
                root.name = rootName;
                var environment = root.GetComponent<SoccerEnvController>();
                Require(environment != null && environment.ArenaGeometry != null,
                    $"Stadium workspace is missing its geometry: {sourcePath}");
                ApplyStadiumTeamVisualIdentity(root);

                foreach (var policy in root.GetComponents<SoccerTeamRewardPolicyBase>())
                {
                    UnityEngine.Object.DestroyImmediate(policy);
                }
                foreach (var ruleController in root.GetComponentsInChildren<MonoBehaviour>(true)
                             .Where(component => component is ISoccerRuleController))
                {
                    UnityEngine.Object.DestroyImmediate(ruleController);
                }

                SoccerTeamRewardPolicyBase redPolicy;
                BaseRewardPolicy navyPolicy;
                if (redPolicyType == typeof(BaseRewardPolicy))
                {
                    redPolicy = root.AddComponent<BaseRewardPolicy>();
                    navyPolicy = root.AddComponent<BaseRewardPolicy>();
                }
                else
                {
                    redPolicy = (SoccerTeamRewardPolicyBase)root.AddComponent(redPolicyType);
                    navyPolicy = root.AddComponent<BaseRewardPolicy>();
                }

                var matchSetup = root.GetComponent<SoccerMatchSetup>() ?? root.AddComponent<SoccerMatchSetup>();
                matchSetup.Configure(
                    redDefinition,
                    navyDefinition,
                    redPolicy,
                    navyPolicy,
                    trainRed,
                    trainNavy,
                    preservedRedModel,
                    preservedNavyModel);
                foreach (var agent in root.GetComponentsInChildren<AgentSoccer>(true))
                {
                    var behavior = agent.GetComponent<BehaviorParameters>();
                    var definition = agent.Team == Team.Red ? redDefinition : navyDefinition;
                    behavior.BehaviorName = definition.BehaviorName;
                    behavior.TeamId = (int)agent.Team;
                    behavior.Model = null;
                    behavior.BehaviorType = matchSetup.IsTrainable(agent.Team)
                        ? BehaviorType.Default
                        : BehaviorType.HeuristicOnly;

                    if (redRuleControllerType != null && agent.Team == Team.Red)
                    {
                        var controller = (RuleBasedSoccerController)agent.gameObject.AddComponent(redRuleControllerType);
                        controller.Configure(agent, environment);
                    }
                }

                var rewardEngine = root.GetComponent<SoccerRewardEngine>() ?? root.AddComponent<SoccerRewardEngine>();
                rewardEngine.Configure(environment, matchSetup);
                var saved = PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                Require(saved != null, $"Could not save Stadium workspace prefab: {assetPath}");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        static void CreateStadiumScene(string scenePath, GameObject environmentPrefab, bool copyCanonicalScene)
        {
            if (copyCanonicalScene)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(scenePath)));
                File.Copy(Path.GetFullPath(StadiumBaseScenePath), Path.GetFullPath(scenePath), true);
                AssetDatabase.ImportAsset(scenePath, ImportAssetOptions.ForceSynchronousImport);
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var environments = scene.GetRootGameObjects()
                .SelectMany(rootObject => rootObject.GetComponentsInChildren<SoccerEnvController>(true))
                .ToArray();
            Require(environments.Length == 1, $"Stadium scene must contain one environment: {scenePath}");
            SoccerEnvController environment;
            if (copyCanonicalScene)
            {
                var oldEnvironmentRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(environments[0].gameObject)
                    ?? environments[0].gameObject;
                UnityEngine.Object.DestroyImmediate(oldEnvironmentRoot);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(environmentPrefab, scene);
                environment = instance.GetComponent<SoccerEnvController>();
            }
            else
            {
                environment = environments[0];
            }

            var goals = environment.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.name.StartsWith("SM_S_Gate", StringComparison.Ordinal))
                .OrderBy(renderer => renderer.bounds.center.x)
                .ToArray();
            Require(goals.Length == 2, $"Stadium scene must contain two Demo goals: {scenePath}");
            var roots = scene.GetRootGameObjects();
            var hud = roots.SelectMany(rootObject => rootObject.GetComponentsInChildren<SoccerHudController>(true)).Single();
            hud.Configure(environment);
            hud.ConfigureRewardPanelBottomRight(true);
            var playerCamera = roots.SelectMany(rootObject => rootObject.GetComponentsInChildren<SoccerPlayerCamera>(true)).Single();
            playerCamera.Configure(environment);
            var fader = playerCamera.GetComponent<SoccerGoalOcclusionFader>();
            Require(fader != null, $"Stadium camera fader is missing: {scenePath}");
            fader.Configure(environment);
            fader.ConfigureGoalRenderers(new[] { goals[0] }, new[] { goals[1] });
            EditorUtility.SetDirty(hud);
            EditorUtility.SetDirty(playerCamera);
            EditorUtility.SetDirty(fader);
            Require(EditorSceneManager.SaveScene(scene), $"Could not save Stadium scene: {scenePath}");
        }

        static bool IsRed(AgentSoccer agent)
        {
            return agent.GetComponent<BehaviorParameters>().TeamId == (int)Team.Red;
        }

        static void ApplyStadiumTeamVisualIdentity(GameObject root)
        {
            var redPlayerMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/AgentRed.mat");
            var navyPlayerMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/AgentNavy.mat");
            var redKickPlateMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/KickPlateRed.mat");
            var navyKickPlateMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/KickPlateNavy.mat");
            var redGoalMaterial = AssetDatabase.LoadAssetAtPath<Material>(StadiumRedGoalMaterialPath);
            var navyGoalMaterial = AssetDatabase.LoadAssetAtPath<Material>(StadiumNavyGoalMaterialPath);
            Require(redPlayerMaterial != null && navyPlayerMaterial != null
                && redKickPlateMaterial != null && navyKickPlateMaterial != null
                && redGoalMaterial != null && navyGoalMaterial != null,
                "Red/Navy shared visual materials must exist before Stadium migration.");

            foreach (var item in root.GetComponentsInChildren<Transform>(true))
            {
                var currentTag = item.gameObject.tag;
                if (currentTag == "blueAgent") item.gameObject.tag = SoccerTeamVisuals.RedAgentTag;
                else if (currentTag == "purpleAgent") item.gameObject.tag = SoccerTeamVisuals.NavyAgentTag;
                else if (currentTag == "blueGoal") item.gameObject.tag = SoccerTeamVisuals.RedGoalTag;
                else if (currentTag == "purpleGoal") item.gameObject.tag = SoccerTeamVisuals.NavyGoalTag;

                item.name = item.name.Replace("Blue", "Red").Replace("Purple", "Navy");
            }

            foreach (var agent in root.GetComponentsInChildren<AgentSoccer>(true))
            {
                var isRed = agent.Team == Team.Red;
                var teamName = isRed ? "Red" : "Navy";
                var agentTag = SoccerTeamVisuals.AgentTag(agent.Team);
                agent.gameObject.tag = agentTag;
                agent.name = agent.name.Replace(isRed ? "Blue" : "Purple", teamName);

                var bodyRenderer = agent.GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(renderer => renderer.gameObject.name.StartsWith("AgentCube_", StringComparison.Ordinal));
                Require(bodyRenderer != null, $"Player body renderer is missing: {agent.name}");
                bodyRenderer.gameObject.name = "AgentCube_" + teamName;
                bodyRenderer.sharedMaterial = isRed ? redPlayerMaterial : navyPlayerMaterial;

                var kickPlate = agent.GetComponentInChildren<SoccerKickPlate>(true);
                Require(kickPlate != null, $"Kick plate is missing: {agent.name}");
                foreach (var part in kickPlate.GetComponentsInChildren<Transform>(true))
                {
                    part.gameObject.tag = agentTag;
                }
                foreach (var renderer in kickPlate.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterial = isRed ? redKickPlateMaterial : navyKickPlateMaterial;
                }

                var detectableTags = isRed
                    ? new[] { "ball", "redGoal", "navyGoal", "wall", "redAgent", "navyAgent" }
                    : new[] { "ball", "navyGoal", "redGoal", "wall", "navyAgent", "redAgent" };
                foreach (var sensor in agent.GetComponentsInChildren<RayPerceptionSensorComponent3D>(true))
                {
                    sensor.SensorName = sensor.SensorName.Replace(isRed ? "Blue" : "Purple", teamName);
                    sensor.DetectableTags.Clear();
                    foreach (var detectableTag in detectableTags)
                    {
                        sensor.DetectableTags.Add(detectableTag);
                    }
                }
            }

            var ballController = root.GetComponentsInChildren<SoccerBallController>(true).Single();
            ballController.redGoalTag = SoccerTeamVisuals.RedGoalTag;
            ballController.navyGoalTag = SoccerTeamVisuals.NavyGoalTag;

            var demoGoals = root.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.name.StartsWith("SM_S_Gate", StringComparison.Ordinal))
                .OrderBy(renderer => renderer.bounds.center.x)
                .ToArray();
            Require(demoGoals.Length == 2, "Stadium migration requires two Demo goal renderers.");
            demoGoals[0].gameObject.tag = SoccerTeamVisuals.RedGoalTag;
            demoGoals[0].sharedMaterials = Enumerable.Repeat(redGoalMaterial, demoGoals[0].sharedMaterials.Length).ToArray();
            demoGoals[1].gameObject.tag = SoccerTeamVisuals.NavyGoalTag;
            demoGoals[1].sharedMaterials = Enumerable.Repeat(navyGoalMaterial, demoGoals[1].sharedMaterials.Length).ToArray();
        }

        static void ExpandTeam(List<AgentSoccer> agents, int targetCount)
        {
            var template = agents[0];
            while (agents.Count < targetCount)
            {
                var clone = UnityEngine.Object.Instantiate(template.gameObject, template.transform.parent);
                clone.name = template.name + " Copy";
                agents.Add(clone.GetComponent<AgentSoccer>());
            }
        }

        static void ConfigureTeam(
            IReadOnlyList<AgentSoccer> agents,
            Team team,
            IReadOnlyList<Vector3> spawns,
            float facingYaw,
            Material humanMarkerMaterial,
            Material kickPlateMaterial,
            IReadOnlyList<int> kickPlateLayers)
        {
            for (var index = 0; index < agents.Count; index++)
            {
                var agent = agents[index];
                var role = index == 0
                    ? AgentSoccer.Position.Striker
                    : index == 3 ? AgentSoccer.Position.DefenderKeeper : AgentSoccer.Position.Midfielder;
                agent.name = $"{team}Player{index + 1}_{role}";
                agent.gameObject.tag = team == Team.Red ? "redAgent" : "navyAgent";
                agent.transform.localPosition = spawns[index];
                agent.transform.localRotation = Quaternion.Euler(0f, facingYaw, 0f);
                agent.Configure(team, role, team == Team.Red && index == 0, spawns[index]);

                var behavior = agent.GetComponent<BehaviorParameters>();
                behavior.BehaviorName = "Soccer4v4_Base";
                behavior.TeamId = (int)team;
                behavior.BehaviorType = BehaviorType.Default;
                behavior.Model = null;
                behavior.BrainParameters.VectorObservationSize = AgentSoccer.VectorObservationSize;
                behavior.BrainParameters.NumStackedVectorObservations = 1;
                behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(3, 3, 3, 3);
                var requester = agent.GetComponent<DecisionRequester>();
                requester.DecisionPeriod = 5;
                requester.TakeActionsBetweenDecisions = true;

                var kickPlate = CreateKickPlate(agent, kickPlateMaterial, kickPlateLayers[index]);

                foreach (var sensor in agent.GetComponentsInChildren<RayPerceptionSensorComponent3D>(true))
                {
                    sensor.RayLength = SensorRange;
                    sensor.UseBatchedRaycasts = true;
                    var sensorMask = sensor.RayLayerMask;
                    sensorMask.value &= ~(1 << kickPlate.gameObject.layer);
                    sensor.RayLayerMask = sensorMask;
                }

                if (humanMarkerMaterial != null && index == 0)
                {
                    CreateHumanMarker(agent.transform, humanMarkerMaterial);
                }
            }
        }

        static SoccerKickPlate CreateKickPlate(AgentSoccer agent, Material material, int layer)
        {
            var previous = agent.transform.Find("KickPlate");
            if (previous != null)
            {
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
            }

            var teamTag = agent.Team == Team.Red ? "redAgent" : "navyAgent";
            var root = new GameObject("KickPlate") { layer = layer, tag = teamTag };
            root.transform.SetParent(agent.transform, false);
            var plate = root.AddComponent<SoccerKickPlate>();

            CreateKickPlatePart(root.transform, "KickPlateCenter", new Vector3(0f, 0f, 0.66f),
                new Vector3(1.05f, SoccerKickPlate.PlayerHeightRatio, 0.18f), 0f, material, layer, teamTag);
            CreateKickPlatePart(root.transform, "KickPlateLeftWing", new Vector3(-0.66f, 0f, 0.80f),
                new Vector3(0.40f, SoccerKickPlate.PlayerHeightRatio, 0.18f), 45f, material, layer, teamTag);
            CreateKickPlatePart(root.transform, "KickPlateRightWing", new Vector3(0.66f, 0f, 0.80f),
                new Vector3(0.40f, SoccerKickPlate.PlayerHeightRatio, 0.18f), -45f, material, layer, teamTag);

            plate.Configure(agent, Vector3.zero, new Vector3(0f, 0f, 0.68f));
            return plate;
        }

        static void CreateKickPlatePart(
            Transform parent,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            float localYaw,
            Material material,
            int layer,
            string teamTag)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.layer = layer;
            part.tag = teamTag;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.Euler(0f, localYaw, 0f);
            part.transform.localScale = localScale;
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        static void CreateHumanMarker(Transform parent, Material material)
        {
            var previous = parent.Find("HumanControlMarker");
            if (previous != null)
            {
                UnityEngine.Object.DestroyImmediate(previous.gameObject);
            }

            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "HumanControlMarker";
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = new Vector3(0f, 0.78f, 0f);
            marker.transform.localScale = new Vector3(0.72f, 0.035f, 0.72f);
            marker.GetComponent<Renderer>().sharedMaterial = material;
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());
        }

        static void RemoveHeadbands(GameObject root)
        {
            var headbands = root.GetComponentsInChildren<Transform>(true)
                .Where(child => child.name == "Headband")
                .ToArray();
            foreach (var headband in headbands)
            {
                UnityEngine.Object.DestroyImmediate(headband.gameObject);
            }
        }

        static Material CreateHumanMarkerMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(HumanMarkerMaterialPath);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = "HumanMarker" };
                AssetDatabase.CreateAsset(material, HumanMarkerMaterialPath);
            }

            var color = new Color(1f, 0.78f, 0.08f);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", color * 2f);
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        static PanelSettings CreatePanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                settings.name = "SoccerPanelSettings";
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.match = 0.5f;
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        }

        static void CreateScene(
            GameObject environmentPrefab,
            PanelSettings panelSettings,
            string scenePath)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var fieldObject = (GameObject)PrefabUtility.InstantiatePrefab(environmentPrefab, scene);
            fieldObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var environment = fieldObject.GetComponent<SoccerEnvController>();
            Require(environment != null, $"Environment prefab is missing SoccerEnvController: {environmentPrefab.name}");

            var settingsObject = new GameObject("SoccerSettings");
            var settings = settingsObject.AddComponent<SoccerSettings>();
            settings.agentRunSpeed = SoccerSettings.DefaultAgentRunSpeed;
            settings.maximumPlanarSpeed = SoccerSettings.DefaultMaximumPlanarSpeed;
            settings.rotationSpeed = SoccerSettings.DefaultRotationSpeed;
            settings.humanAcceleration = SoccerSettings.DefaultHumanAcceleration;
            settings.humanDeceleration = SoccerSettings.DefaultHumanDeceleration;
            settings.redMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Red.mat");
            settings.navyMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Navy.mat");

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.25f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.35f, 0.44f, 0.55f);
            RenderSettings.ambientEquatorColor = new Color(0.2f, 0.26f, 0.3f);
            RenderSettings.ambientGroundColor = new Color(0.09f, 0.11f, 0.13f);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = SoccerPlayerCamera.DefaultOverviewFieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 350f;
            cameraObject.transform.position = SoccerPlayerCamera.DefaultOverviewPosition;
            cameraObject.transform.LookAt(new Vector3(0f, 0f, 0f));
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<SoccerGoalOcclusionFader>().Configure(environment);
            cameraObject.AddComponent<SoccerPlayerCamera>().Configure(environment);

            var hudObject = new GameObject("Soccer HUD");
            hudObject.SetActive(false);
            var document = hudObject.AddComponent<UIDocument>();
            document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath) ?? panelSettings;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            var hud = hudObject.AddComponent<SoccerHudController>();
            hud.Configure(environment);
            EditorUtility.SetDirty(document);
            EditorUtility.SetDirty(hud);
            hudObject.SetActive(true);

            EditorSceneManager.SaveScene(scene, scenePath);
        }

        static void AddSceneToBuildSettings()
        {
            var requiredScenes = new[]
            {
                StadiumBaseScenePath,
                StadiumAttackScenePath,
                StadiumDefenseScenePath,
                StadiumPressScenePath,
                StadiumRuleScenePath
            };
            var retiredSoccerScenes = new[]
            {
                ScenePath, BaseScenePath, AttackScenePath, DefenseScenePath, PressScenePath, RuleScenePath
            };
            var scenes = EditorBuildSettings.scenes
                .Where(scene => !requiredScenes.Contains(scene.path)
                    && !retiredSoccerScenes.Contains(scene.path)
                    && !scene.path.StartsWith(LegacyRoot + "/", StringComparison.Ordinal))
                .ToList();
            for (var index = requiredScenes.Length - 1; index >= 0; index--)
            {
                scenes.Insert(0, new EditorBuildSettingsScene(requiredScenes[index], true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // 기존 Profile은 의도적으로 보존하며 Base 복사는 최초 생성 때만 수행한다.
        static SoccerRewardProfile CreateRewardProfile(string assetPath, SoccerRewardProfile baseProfile)
        {
            var profile = AssetDatabase.LoadAssetAtPath<SoccerRewardProfile>(assetPath);
            if (profile != null)
            {
                return profile;
            }

            profile = ScriptableObject.CreateInstance<SoccerRewardProfile>();
            if (baseProfile != null)
            {
                EditorUtility.CopySerialized(baseProfile, profile);
            }
            else
            {
                profile.ApplyBaseDefaults();
            }

            AssetDatabase.CreateAsset(profile, assetPath);
            return profile;
        }

        static SoccerTeamDefinition CreateTeamDefinition(
            string assetPath,
            string teamId,
            string displayName,
            string behaviorName,
            SoccerRewardProfile profile,
            ModelAsset baseModel,
            SoccerTeamControllerType controllerType = SoccerTeamControllerType.NeuralPolicy)
        {
            var definition = AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(assetPath);
            var isNew = definition == null;
            if (isNew)
            {
                definition = ScriptableObject.CreateInstance<SoccerTeamDefinition>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            var selectedModel = controllerType == SoccerTeamControllerType.NeuralPolicy
                ? (definition.InferenceModel != null ? definition.InferenceModel : baseModel)
                : null;
            definition.Configure(teamId, displayName, behaviorName, profile, selectedModel, controllerType);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        static SoccerTeamDefinition CreateTeamWorkspace(
            string folderName,
            string displayName,
            string teamId,
            string behaviorName,
            SoccerRewardProfile baseProfile,
            ModelAsset baseModel)
        {
            // Builder는 공통 생성 계약이다. 팀별 실험은 자신의 Profile·Training YAML·Models에서 수행한다.
            // 실제 폴더는 담당자 이름을 포함하지만 직렬화된 팀 정체성은 displayName으로 고정한다.
            var teamRoot = $"{Root}/Teams/{folderName}";
            var profile = CreateRewardProfile($"{teamRoot}/Profiles/{displayName}RewardProfile.asset", baseProfile);
            var tactic = Enum.Parse<SoccerTacticType>(displayName);
            var workspaceModel = FindLatestNamedModel(teamRoot + "/Models", tactic) ?? baseModel;
            return CreateTeamDefinition(
                $"{teamRoot}/Profiles/{displayName}TeamDefinition.asset",
                teamId,
                displayName,
                behaviorName,
                profile,
                workspaceModel);
        }

        static SoccerTeamDefinition CreateRuleTeamWorkspace(SoccerRewardProfile baseProfile)
        {
            // Rule Profile은 비교용 점수 산출만 담당한다. 실제 행동 변경은 RuleBasedSoccerController의 FSM에서 한다.
            const string teamRoot = Root + "/Teams/" + RuleFolder;
            var profile = CreateRewardProfile(teamRoot + "/Profiles/RuleRewardProfile.asset", baseProfile);
            return CreateTeamDefinition(
                teamRoot + "/Profiles/RuleTeamDefinition.asset",
                "rule",
                "Rule",
                "Soccer4v4_Rule",
                profile,
                null,
                SoccerTeamControllerType.RuleBased);
        }

        static ModelAsset FindLatestNamedModel(string folder, SoccerTacticType expectedTactic)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return null;
            }

            return AssetDatabase.FindAssets("t:ModelAsset", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path => new
                {
                    Model = AssetDatabase.LoadAssetAtPath<ModelAsset>(path),
                    Name = Path.GetFileNameWithoutExtension(path)
                })
                .Where(item => item.Model != null
                    && SoccerModelNaming.TryParse(item.Name, out var tactic, out _, out _)
                    && tactic == expectedTactic)
                .Select(item =>
                {
                    SoccerModelNaming.TryParse(item.Name, out _, out var date, out var version);
                    return new { item.Model, Date = date, Version = version };
                })
                .OrderByDescending(item => item.Date)
                .ThenByDescending(item => item.Version)
                .Select(item => item.Model)
                .FirstOrDefault();
        }

        static VisualMaterialSet CreateVisualMaterialSet()
        {
            return new VisualMaterialSet
            {
                Red = CreateOrUpdateUrpMaterial("AgentRed", SoccerTeamVisuals.RedPlayerColor),
                Navy = CreateOrUpdateUrpMaterial("AgentNavy", SoccerTeamVisuals.NavyPlayerColor),
                RedKickPlate = CreateOrUpdateUrpMaterial("KickPlateRed", SoccerTeamVisuals.RedKickPlateColor),
                NavyKickPlate = CreateOrUpdateUrpMaterial("KickPlateNavy", SoccerTeamVisuals.NavyKickPlateColor),
                Eye = CreateOrUpdateUrpMaterial("Eye", new Color(0.06f, 0.06f, 0.06f)),
                Wall = CreateOrUpdateUrpMaterial("GrayMiddle", new Color(0.39f, 0.39f, 0.39f)),
                GoalRed = CreateOrUpdateUrpMaterial("GoalRed", SoccerTeamVisuals.RedGoalColor),
                GoalNavy = CreateOrUpdateUrpMaterial("GoalNavy", SoccerTeamVisuals.NavyGoalColor),
                GoalNetBlack = CreateOrUpdateUrpMaterial("GoalNetBlack", new Color(0.05f, 0.05f, 0.05f)),
                GoalNetWhite = CreateOrUpdateUrpMaterial("GoalNetWhite", Color.white),
                Glass = CreateOrUpdateUrpMaterial("ClearPlastic", new Color(0.62f, 0.84f, 0.96f, 0.18f), true)
            };
        }

        static void PrepareStadiumGoalMaterials()
        {
            foreach (var item in new[]
                     {
                         (Path: StadiumRedGoalMaterialPath, Name: "StadiumRedGoal", Color: SoccerTeamVisuals.RedGoalColor),
                         (Path: StadiumNavyGoalMaterialPath, Name: "StadiumNavyGoal", Color: SoccerTeamVisuals.NavyGoalColor)
                     })
            {
                var material = CreateOrUpdateUrpMaterialAtPath(item.Path, item.Name, item.Color);
                if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", null);
                if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", null);
                if (material.HasProperty("_Cull")) material.SetFloat("_Cull", (float)CullMode.Off);
                EditorUtility.SetDirty(material);
            }
        }

        static Material CreateOrUpdateUrpMaterial(string name, Color color, bool transparent = false)
        {
            return CreateOrUpdateUrpMaterialAtPath($"{Root}/Materials/{name}.mat", name, color, transparent);
        }

        static Material CreateOrUpdateUrpMaterialAtPath(
            string path,
            string name,
            Color color,
            bool transparent = false)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var fallbackShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(fallbackShader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            var urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader != null)
            {
                material.shader = urpShader;
            }

            material.name = name;
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", transparent ? 0.65f : 0.3f);
            }

            if (material.HasProperty("_Metallic"))
            {
                material.SetFloat("_Metallic", 0f);
            }

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", transparent ? 1f : 0f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_ZWrite", transparent ? 0f : 1f);
                material.renderQueue = transparent ? (int)RenderQueue.Transparent : -1;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        static void RepairVisualMaterials(GameObject root, VisualMaterialSet materials)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var objectName = renderer.gameObject.name;
                if (objectName == "AgentCube_Red")
                {
                    renderer.sharedMaterial = materials.Red;
                }
                else if (objectName == "AgentCube_Navy")
                {
                    renderer.sharedMaterial = materials.Navy;
                }
                else if (objectName == "GoalRed")
                {
                    renderer.sharedMaterial = materials.GoalRed;
                }
                else if (objectName == "GoalNavy")
                {
                    renderer.sharedMaterial = materials.GoalNavy;
                }
                else if (objectName is "eye" or "mouth")
                {
                    renderer.sharedMaterial = materials.Eye;
                }
                else if (objectName.StartsWith("GoalNet", StringComparison.Ordinal))
                {
                    renderer.sharedMaterials = objectName.EndsWith("Outer", StringComparison.Ordinal)
                        ? new[] { materials.GoalNetWhite }
                        : new[] { materials.GoalNetBlack, materials.GoalNetWhite };
                }
                else if (objectName.StartsWith("GlassSide", StringComparison.Ordinal))
                {
                    renderer.sharedMaterial = materials.Glass;
                }
                else if (objectName.StartsWith("Wall", StringComparison.Ordinal)
                    || objectName is "Side" or "SideA" or "SideB" or "Roof")
                {
                    renderer.sharedMaterial = materials.Wall;
                }
                else if (HasMissingMaterial(renderer))
                {
                    renderer.sharedMaterial = materials.Wall;
                }
            }
        }

        static void ApplyCommonArenaConfiguration(GameObject root, VisualMaterialSet visualMaterials)
        {
            RepairVisualMaterials(root, visualMaterials);
            var goalRenderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => GoalRendererNames.Contains(renderer.gameObject.name))
                .ToArray();
            Require(goalRenderers.Length == GoalRendererNames.Count,
                $"The common arena must contain all six goal renderers: {root.name}");
            foreach (var renderer in goalRenderers)
            {
                var expectedTag = renderer.gameObject.name.Contains("Red", StringComparison.Ordinal)
                    ? "redGoal"
                    : "navyGoal";
                Require(renderer.gameObject.CompareTag(expectedTag),
                    $"Goal renderer tag mismatch: {root.name}/{renderer.gameObject.name}");

                // 런타임 머티리얼 전환을 위한 정적 배칭 해제.
                var staticFlags = GameObjectUtility.GetStaticEditorFlags(renderer.gameObject);
                GameObjectUtility.SetStaticEditorFlags(
                    renderer.gameObject,
                    staticFlags & ~StaticEditorFlags.BatchingStatic);

                // Goal mesh/collider is authored with its opening on local Z.
                // Use an absolute scale so repeated Builder runs remain idempotent.
                renderer.transform.localScale = new Vector3(1f, 1f, GoalLateralScale);
            }

            var balls = root.GetComponentsInChildren<SoccerBallController>(true);
            Require(balls.Length == 1, $"The common arena must contain exactly one Soccer ball: {root.name}");
            var ball = balls[0];
            ball.transform.localScale = Vector3.one * BallUniformScale;
            var ballPosition = ball.transform.localPosition;
            ballPosition.y = BallResetHeight;
            ball.transform.localPosition = ballPosition;
        }

        static bool HasCommonBasicSkillRewards(SoccerRewardProfile profile)
        {
            return profile != null
                && Mathf.Approximately(profile.passSuccess, 0.005f)
                && Mathf.Approximately(profile.passIndividual, 0.005f)
                && Mathf.Approximately(profile.controlledCarryGroup, 0.0025f)
                && Mathf.Approximately(profile.controlledCarryIndividual, 0.005f)
                && Mathf.Approximately(profile.wastefulStrongKickPenalty, 0.003f)
                && Mathf.Approximately(profile.unsafeOwnGoalKickPenalty, 0.02f)
                && Mathf.Approximately(profile.passIndividualRewardLimitPerPossession, 0.02f)
                && Mathf.Approximately(profile.controlledCarryRewardLimitPerPossession, 0.03f)
                && Mathf.Approximately(profile.wastefulStrongKickPenaltyLimitPerPossession, 0.015f)
                && Mathf.Approximately(profile.behaviorPenaltyLimitPerMatch, 0.1f);
        }

        // 다섯 환경에 동일하게 배포한다. 변경 시 템플릿·프리팹·검증·공통 문서를 함께 갱신한다.
        static void ApplyCommonMatchConfiguration(SoccerEnvController controller)
        {
            controller.matchDurationSeconds = MatchDurationSeconds;
            controller.goalResetDelaySeconds = GoalResetDelaySeconds;
            controller.ConfigureStartMode(true);
        }

        static void ValidateRendererMaterials(GameObject root)
        {
            var missingRenderer = root.GetComponentsInChildren<Renderer>(true).FirstOrDefault(HasMissingMaterial);
            Require(missingRenderer == null, $"Renderer {missingRenderer?.name} has a missing material.");
        }

        static bool HasMissingMaterial(Renderer renderer)
        {
            var materials = renderer.sharedMaterials;
            return materials == null || materials.Length == 0 || materials.Any(material => material == null);
        }

        static void UpgradeCopiedMaterialsForUrp()
        {
            var urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null)
            {
                return;
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null || material.shader.name != "Standard")
                {
                    continue;
                }

                var texture = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
                var color = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
                material.shader = urpShader;
                if (material.HasProperty("_BaseMap"))
                {
                    material.SetTexture("_BaseMap", texture);
                }

                if (material.HasProperty("_BaseColor"))
                {
                    material.SetColor("_BaseColor", color);
                }

                EditorUtility.SetDirty(material);
            }
        }

        static void RemoveMissingScripts(GameObject root)
        {
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(child.gameObject);
            }
        }

        static void EnsureDirectories()
        {
            var directories = new List<string>
            {
                Root + "/Prefabs",
                Root + "/Scenes",
                Root + "/Materials",
                Root + "/UI",
                Root + "/Core/Profiles",
                Root + "/Core/Models",
                Root + "/Core/Prefabs",
                Root + "/Core/Scenes",
                Root + "/Core/Training"
            };
            foreach (var teamName in new[] { AttackFolder, DefenseFolder, PressFolder })
            {
                directories.Add($"{Root}/Teams/{teamName}/Profiles");
                directories.Add($"{Root}/Teams/{teamName}/Models");
                directories.Add($"{Root}/Teams/{teamName}/Prefabs");
                directories.Add($"{Root}/Teams/{teamName}/Scenes");
                directories.Add($"{Root}/Teams/{teamName}/Training");
            }

            directories.Add(Root + "/Teams/" + RuleFolder + "/Profiles");
            directories.Add(Root + "/Teams/" + RuleFolder + "/Prefabs");
            directories.Add(Root + "/Teams/" + RuleFolder + "/Scenes");

            foreach (var directory in directories)
            {
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
            }
        }

        static void EnsureTags(params string[] requiredTags)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0)
            {
                throw new BuildFailedException("TagManager.asset could not be loaded.");
            }

            var serializedTagManager = new SerializedObject(assets[0]);
            var tags = serializedTagManager.FindProperty("tags");
            foreach (var requiredTag in requiredTags)
            {
                var exists = false;
                for (var index = 0; index < tags.arraySize; index++)
                {
                    if (tags.GetArrayElementAtIndex(index).stringValue == requiredTag)
                    {
                        exists = true;
                        break;
                    }
                }

                if (exists)
                {
                    continue;
                }

                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = requiredTag;
            }

            serializedTagManager.ApplyModifiedProperties();
        }

        static int[] EnsureKickPlateLayers()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets.Length == 0)
            {
                throw new BuildFailedException("TagManager.asset could not be loaded.");
            }

            var serializedTagManager = new SerializedObject(assets[0]);
            var layers = serializedTagManager.FindProperty("layers");
            var assignedLayers = new int[KickPlateLayerCount];
            for (var plateIndex = 0; plateIndex < KickPlateLayerCount; plateIndex++)
            {
                var requiredName = $"{KickPlateLayerPrefix}{plateIndex + 1}";
                var layerIndex = -1;
                for (var index = 8; index < layers.arraySize; index++)
                {
                    if (layers.GetArrayElementAtIndex(index).stringValue == requiredName)
                    {
                        layerIndex = index;
                        break;
                    }
                }

                if (layerIndex < 0)
                {
                    for (var index = 8; index < layers.arraySize; index++)
                    {
                        if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(index).stringValue))
                        {
                            continue;
                        }

                        layerIndex = index;
                        layers.GetArrayElementAtIndex(index).stringValue = requiredName;
                        break;
                    }
                }

                Require(layerIndex >= 0, $"No free Unity layer is available for {requiredName}.");
                assignedLayers[plateIndex] = layerIndex;
            }

            serializedTagManager.ApplyModifiedProperties();
            return assignedLayers;
        }

        static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new BuildFailedException(message);
            }
        }

        sealed class VisualMaterialSet
        {
            public Material Red;
            public Material Navy;
            public Material RedKickPlate;
            public Material NavyKickPlate;
            public Material Eye;
            public Material Wall;
            public Material GoalRed;
            public Material GoalNavy;
            public Material GoalNetBlack;
            public Material GoalNetWhite;
            public Material Glass;
        }
    }
}
