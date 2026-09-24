using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MachineLearning.Soccer;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using Unity.InferenceEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MachineLearning.Soccer.Manager.Editor
{
    public static class MNG_ProjectBuilder
    {
        public const string SourcePrefabPath =
            "Assets/_Soccer/Core/Prefabs/StadiumEnvironment_Base.prefab";
        public const string ManagerPrefabPath =
            "Assets/_Soccer/Manager/Prefabs/MNG_StadiumEnvironment.prefab";
        public const string ManagerScenePath =
            "Assets/_Soccer/Manager/Scenes/MNG_Stadium4v4.unity";
        public const string ConnectionSmokeScenePath =
            "Assets/_Soccer/Manager/Scenes/MNG_M1_ConnectionSmoke.unity";
        public const string RuleVsRuleScenePath =
            "Assets/_Soccer/Manager/Curriculum/R0_RuleBaseline/Scenes/MNG_R0_RuleVsRule.unity";
        public const string AttackChoiceScenePath =
            "Assets/_Soccer/Manager/Curriculum/M1_AttackChoice/Scenes/MNG_M1_AttackChoice.unity";
        public const string AttackChoiceEvaluationScenePath =
            "Assets/_Soccer/Manager/Curriculum/M1_AttackChoice/Scenes/MNG_M1_AttackChoice_Evaluation.unity";
        public const string AttackMovingScenePath =
            "Assets/_Soccer/Manager/Curriculum/M1_AttackMoving/Scenes/MNG_M1_AttackMoving.unity";
        public const string AttackMovingEvaluationScenePath =
            "Assets/_Soccer/Manager/Curriculum/M1_AttackMoving/Scenes/MNG_M1_AttackMoving_Evaluation.unity";
        public const string DefenseChoiceScenePath =
            "Assets/_Soccer/Manager/Curriculum/M2_DefenseChoice/Scenes/MNG_M2_DefenseChoice.unity";
        public const string DefenseChoiceEvaluationScenePath =
            "Assets/_Soccer/Manager/Curriculum/M2_DefenseChoice/Scenes/MNG_M2_DefenseChoice_Evaluation.unity";
        public const string FallbackMatch60ScenePath =
            "Assets/_Soccer/Manager/Curriculum/M3_FallbackMatch/Scenes/MNG_M3_FallbackMatch60.unity";
        public const string FallbackMatch300ScenePath =
            "Assets/_Soccer/Manager/Curriculum/M3_FallbackMatch/Scenes/MNG_M3_FallbackMatch300.unity";
        public const string FallbackMatchEvaluationScenePath =
            "Assets/_Soccer/Manager/Curriculum/M3_FallbackMatch/Scenes/MNG_M3_FallbackMatch_Evaluation.unity";
        public const string PhysicsProfilePath =
            "Assets/_Soccer/Manager/Profiles/MNG_Physics.asset";
        public const string CurriculumCatalogPath =
            "Assets/_Soccer/Manager/Profiles/MNG_CurriculumCatalog.asset";
        public const string BaseRewardProfilePath =
            "Assets/_Soccer/Manager/Profiles/MNG_BaseReward.asset";
        public const string BallPhysicsMaterialPath =
            "Assets/_Soccer/Manager/Profiles/MNG_BallPhysics.physicMaterial";
        public const string SharedPanelSettingsPath = "Assets/_Soccer/UI/SoccerPanelSettings.asset";
        public const string SharedHudUxmlPath = "Assets/_Soccer/UI/SoccerHud.uxml";

        [MenuItem("Tools/Soccer Manager/Build MNG M0 Stadium")]
        public static void BuildM0Stadium()
        {
            EnsureFolders();
            var physics = GetOrCreatePhysicsProfile();
            var redReward = GetOrCreateRewardProfile(BaseRewardProfilePath, MNG_TacticProfile.Base);
            var navyReward = GetOrCreateRewardProfile(
                "Assets/_Soccer/Manager/Profiles/MNG_FallbackReward.asset",
                MNG_TacticProfile.Base);
            var ballMaterial = GetOrCreateBallPhysicsMaterial();
            GetOrCreateCurriculumCatalog();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            Require(source != null, $"Missing source Stadium prefab: {SourcePrefabPath}");

            var sourceBall = source.GetComponentInChildren<SoccerBallController>(true);
            Require(sourceBall != null, "Source Stadium is missing SoccerBallController.");
            var sourceBallScale = sourceBall.transform.localScale;
            var sourceBallY = sourceBall.transform.position.y;
            var sourceMass = sourceBall.GetComponent<Rigidbody>().mass;
            var sourceGeometry = source.GetComponentInChildren<SoccerArenaGeometry>(true);
            Require(sourceGeometry != null, "Source Stadium is missing SoccerArenaGeometry.");

            var clone = UnityEngine.Object.Instantiate(source);
            clone.name = "MNG_StadiumEnvironment";
            try
            {
                BuildClone(clone, physics, redReward, navyReward, ballMaterial, sourceBallScale, sourceBallY);
                PrefabUtility.SaveAsPrefabAsset(clone, ManagerPrefabPath);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(clone);
            }

            CreateScene();
            CreateRuleVsRuleScene();
            CreateConnectionSmokeScene();
            CreateAttackChoiceScene();
            CreateAttackMovingScene();
            CreateDefenseChoiceScene();
            CreateFallbackMatchScene(false, FallbackMatch60ScenePath);
            CreateFallbackMatchScene(true, FallbackMatch300ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateM0Assets();
            WriteSourceSnapshot(sourceBallScale, sourceBallY, sourceMass, sourceGeometry);
            Debug.Log("MNG M0 BUILD PASS");
        }

        public static void BuildM0StadiumBatch()
        {
            try
            {
                BuildM0Stadium();
                Debug.Log("MNG M0 BUILD BATCH PASS");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        [MenuItem("Tools/Soccer Manager/Validate MNG M0 Stadium")]
        public static void ValidateM0Assets()
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(SourcePrefabPath);
            Require(source != null, $"Missing source Stadium prefab: {SourcePrefabPath}");
            var sourceBall = source.GetComponentInChildren<SoccerBallController>(true);
            var sourceGeometry = source.GetComponentInChildren<SoccerArenaGeometry>(true);
            var prefab = PrefabUtility.LoadPrefabContents(ManagerPrefabPath);
            try
            {
                Require(prefab.GetComponentsInChildren<AgentSoccer>(true).Length == 0,
                    "MNG prefab contains legacy AgentSoccer.");
                Require(prefab.GetComponentsInChildren<SoccerEnvController>(true).Length == 0,
                    "MNG prefab contains legacy SoccerEnvController.");
                Require(prefab.GetComponentsInChildren<SoccerMatchSetup>(true).Length == 0,
                    "MNG prefab contains legacy SoccerMatchSetup.");
                Require(prefab.GetComponentsInChildren<SoccerRewardEngine>(true).Length == 0,
                    "MNG prefab contains legacy SoccerRewardEngine.");
                Require(prefab.GetComponentsInChildren<RayPerceptionSensorComponent3D>(true).Length == 0,
                    "MNG prefab contains legacy policy Ray sensors.");
                Require(prefab.GetComponentsInChildren<MNG_PlayerAvatar>(true).Length == 8,
                    "MNG prefab must contain exactly eight player avatars.");
                Require(prefab.GetComponentsInChildren<MNG_PlayerMotor>(true).Length == 8,
                    "MNG prefab must contain exactly eight player motors.");
                Require(prefab.GetComponentsInChildren<MNG_PlayerSkillExecutor>(true).Length == 8,
                    "MNG prefab must contain exactly eight skill executors.");
                Require(prefab.GetComponentsInChildren<SoccerKickPlate>(true).Length == 0,
                    "MNG prefab contains the legacy AgentSoccer kick plate controller.");
                var kickPlates = prefab.GetComponentsInChildren<MNG_KickPlate>(true);
                Require(kickPlates.Length == 8,
                    "MNG prefab must contain exactly eight MNG kick plates.");
                foreach (var plate in kickPlates)
                {
                    Require(plate.Owner != null && plate.Owner.KickPlate == plate,
                        $"MNG kick plate owner mismatch: {plate.name}");
                    Require(plate.GetComponentsInChildren<Collider>(true).Length == 3,
                        $"MNG kick plate must retain center and two wing colliders: {plate.Owner.name}");
                }
                Require(prefab.GetComponentsInChildren<MNG_ManagerAgent>(true).Length == 2,
                    "MNG prefab must contain exactly two manager policy endpoints.");
                var allPolicyAgents = prefab.GetComponentsInChildren<Agent>(true);
                Require(allPolicyAgents.Length == 2 && allPolicyAgents.All(agent => agent is MNG_ManagerAgent),
                    "MNG policy endpoints must contain exactly one MNG_ManagerAgent each and no plain Agent.");
                Require(prefab.GetComponentsInChildren<MNG_FallbackManager>(true).Length == 2,
                    "MNG M0 prefab must contain two explicit fallback managers.");
                var ruleManagers = prefab.GetComponentsInChildren<MNG_RuleBasedManager>(true);
                Require(ruleManagers.Length == 2,
                    "MNG prefab must contain exactly two R0 rule managers.");
                Require(ruleManagers.All(manager => !manager.enabled)
                    && ruleManagers.Select(manager => manager.Team).Distinct().Count() == 2,
                    "MNG prefab R0 rule managers must be one disabled endpoint per team.");
                Require(ruleManagers.All(manager => manager.GetComponent<Agent>() == null
                    && manager.GetComponent<BehaviorParameters>() == null
                    && manager.GetComponent<DecisionRequester>() == null),
                    "MNG R0 rule managers must never contain ML-Agents policy components.");
                Require(prefab.GetComponentsInChildren<MNG_HumanInput>(true).Length == 1,
                    "MNG prefab must contain one Red Striker human adapter.");
                var cameras = prefab.GetComponentsInChildren<Camera>(true);
                Require(cameras.Length == 1 && cameras[0].enabled
                    && cameras[0].CompareTag("MainCamera"),
                    "MNG prefab must contain one enabled broadcast Main Camera.");
                var spectatorCamera = cameras[0].GetComponent<MNG_SpectatorCamera>();
                Require(spectatorCamera != null
                    && spectatorCamera.MatchController == prefab.GetComponent<MNG_MatchController>()
                    && spectatorCamera.HumanInput == prefab.GetComponentInChildren<MNG_HumanInput>(true),
                    "MNG spectator camera bindings are missing.");
                Require(cameras[0].GetComponent<AudioListener>() != null
                    && cameras[0].GetComponent<AudioListener>().enabled,
                    "MNG spectator camera requires one enabled AudioListener.");
                Require(prefab.GetComponentsInChildren<SoccerHudController>(true).Length == 0,
                    "MNG prefab contains the legacy Soccer HUD controller.");
                var hud = prefab.GetComponentsInChildren<MNG_HudPresenter>(true).SingleOrDefault();
                Require(hud != null && hud.MatchController == prefab.GetComponent<MNG_MatchController>()
                    && hud.GetComponent<UnityEngine.UIElements.UIDocument>().visualTreeAsset != null,
                    "MNG HUD presenter binding or shared UI document is missing.");

                var ballControl = prefab.GetComponentInChildren<MNG_BallControl>(true);
                Require(ballControl != null, "MNG prefab is missing MNG_BallControl.");
                Require(prefab.GetComponentsInChildren<MNG_TacticalRewardTracker>(true).Length == 1,
                    "MNG prefab must contain exactly one tactical reward tracker.");
                var body = ballControl.GetComponent<Rigidbody>();
                Require(Mathf.Abs(body.mass - MNG_PhysicsProfile.RequiredBallMass) < 0.0001f,
                    $"MNG ball mass mismatch: {body.mass}");
                var expectedScale = sourceBall.transform.localScale
                    * MNG_PhysicsProfile.RequiredBallScaleMultiplier;
                Require(Vector3.Distance(ballControl.transform.localScale, expectedScale) < 0.000001f,
                    $"MNG ball scale mismatch: {ballControl.transform.localScale} expected {expectedScale}");
                var expectedY = sourceBall.transform.position.y
                    * MNG_PhysicsProfile.RequiredBallScaleMultiplier;
                Require(Mathf.Abs(ballControl.transform.position.y - expectedY) < 0.000001f,
                    $"MNG ball centre Y mismatch: {ballControl.transform.position.y} expected {expectedY}");
                var profile = AssetDatabase.LoadAssetAtPath<MNG_PhysicsProfile>(PhysicsProfilePath);
                Require(profile != null, "MNG physics profile is missing.");
                profile.ValidateOrThrow();
                var ballMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(BallPhysicsMaterialPath);
                Require(ballMaterial != null, "MNG ball physics material is missing.");
                Require(Mathf.Abs(ballMaterial.dynamicFriction
                    - MNG_PhysicsProfile.RequiredBallDynamicFriction) < 0.0001f,
                    $"MNG ball dynamic friction mismatch: {ballMaterial.dynamicFriction}");
                Require(Mathf.Abs(ballMaterial.staticFriction
                    - MNG_PhysicsProfile.RequiredBallStaticFriction) < 0.0001f,
                    $"MNG ball static friction mismatch: {ballMaterial.staticFriction}");
                Require(Mathf.Abs(ballMaterial.bounciness
                    - MNG_PhysicsProfile.RequiredBallRestitution) < 0.0001f,
                    $"MNG ball restitution mismatch: {ballMaterial.bounciness}");
                Require(ballMaterial.frictionCombine == PhysicsMaterialCombine.Minimum,
                    "MNG ball friction combine must prefer the low-friction ball setting.");
                Require(ballMaterial.bounceCombine == PhysicsMaterialCombine.Maximum,
                    "MNG ball bounce combine must preserve modest ball restitution.");

                ValidateRoster(prefab.GetComponentsInChildren<MNG_PlayerAvatar>(true));
                ValidateManagers(prefab.GetComponentsInChildren<MNG_ManagerAgent>(true));
                var mngGeometry = prefab.GetComponentInChildren<SoccerArenaGeometry>(true);
                Require(Mathf.Abs(mngGeometry.HalfLength - sourceGeometry.HalfLength) < 0.000001f
                    && Mathf.Abs(mngGeometry.HalfWidth - sourceGeometry.HalfWidth) < 0.000001f
                    && Mathf.Abs(mngGeometry.GoalHalfWidth - sourceGeometry.GoalHalfWidth) < 0.000001f,
                    "MNG Stadium geometry diverged from the Core source.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ManagerScenePath);
            Require(scene != null, $"Missing MNG Scene: {ManagerScenePath}");
            var smokeScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ConnectionSmokeScenePath);
            Require(smokeScene != null, $"Missing MNG smoke Scene: {ConnectionSmokeScenePath}");
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(RuleVsRuleScenePath) != null,
                $"Missing MNG R0 Rule-vs-Rule Scene: {RuleVsRuleScenePath}");
            var attackScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(AttackChoiceScenePath);
            Require(attackScene != null, $"Missing MNG M1 attack Scene: {AttackChoiceScenePath}");
            var movingAttackScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(AttackMovingScenePath);
            Require(movingAttackScene != null,
                $"Missing MNG M1 moving-defense Scene: {AttackMovingScenePath}");
            var defenseScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(DefenseChoiceScenePath);
            Require(defenseScene != null, $"Missing MNG M2 defense Scene: {DefenseChoiceScenePath}");
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(FallbackMatch60ScenePath) != null,
                $"Missing MNG M3 60-second Scene: {FallbackMatch60ScenePath}");
            Require(AssetDatabase.LoadAssetAtPath<SceneAsset>(FallbackMatch300ScenePath) != null,
                $"Missing MNG M3 300-second Scene: {FallbackMatch300ScenePath}");
            var catalog = AssetDatabase.LoadAssetAtPath<MNG_CurriculumCatalog>(CurriculumCatalogPath);
            Require(catalog != null, $"Missing MNG curriculum catalog: {CurriculumCatalogPath}");
            catalog.ValidateOrThrow();
            Debug.Log("MNG M0 VALIDATION PASS");
        }

        public static void ValidateM0AssetsBatch()
        {
            try
            {
                ValidateM0Assets();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        static void BuildClone(
            GameObject root,
            MNG_PhysicsProfile physics,
            MNG_RewardProfile redReward,
            MNG_RewardProfile navyReward,
            PhysicsMaterial ballMaterial,
            Vector3 sourceBallScale,
            float sourceBallY)
        {
            var legacyAgents = root.GetComponentsInChildren<AgentSoccer>(true);
            Require(legacyAgents.Length == 8, $"Expected eight source players, found {legacyAgents.Length}.");
            var ordered = BuildRoster(legacyAgents);

            var avatars = new MNG_PlayerAvatar[8];
            for (var index = 0; index < ordered.Count; index++)
            {
                var gameObject = RebuildPlayerRoot(ordered[index].Agent.gameObject);
                var avatar = gameObject.AddComponent<MNG_PlayerAvatar>();
                avatar.Configure(ordered[index].Team, ordered[index].Slot, ordered[index].Role);
                avatars[index] = avatar;
                var motor = gameObject.AddComponent<MNG_PlayerMotor>();
                motor.Configure(physics);
                gameObject.AddComponent<MNG_PlayerSkillExecutor>();
                var legacyPlate = gameObject.GetComponentInChildren<SoccerKickPlate>(true);
                Require(legacyPlate != null, $"Source player {gameObject.name} is missing its kick plate.");
                var plateObject = legacyPlate.gameObject;
                var retracted = legacyPlate.RetractedLocalPosition;
                var extended = legacyPlate.ExtendedLocalPosition;
                UnityEngine.Object.DestroyImmediate(legacyPlate);
                var kickPlate = plateObject.AddComponent<MNG_KickPlate>();
                kickPlate.Configure(avatar, retracted, extended);
            }
            DestroyAll(root.GetComponentsInChildren<RayPerceptionSensorComponent3D>(true));
            DestroyLegacyCameraObjects(root);

            DestroyAll(root.GetComponentsInChildren<SoccerTrainingBootstrap>(true));
            DestroyAll(root.GetComponentsInChildren<SoccerHudController>(true));
            var hudObject = CreateHudObject(root.transform);
            DestroyAll(root.GetComponentsInChildren<SoccerMatchSetup>(true));
            DestroyAll(root.GetComponentsInChildren<SoccerRewardEngine>(true));
            DestroyAll(root.GetComponentsInChildren<SoccerEnvController>(true));

            var legacyBall = root.GetComponentInChildren<SoccerBallController>(true);
            Require(legacyBall != null, "Cloned Stadium is missing its source ball.");
            var ballObject = legacyBall.gameObject;
            UnityEngine.Object.DestroyImmediate(legacyBall);
            ballObject.name = "MNG_Ball";
            ballObject.transform.localScale = sourceBallScale * MNG_PhysicsProfile.RequiredBallScaleMultiplier;
            var ballPosition = ballObject.transform.position;
            ballPosition.y = sourceBallY * MNG_PhysicsProfile.RequiredBallScaleMultiplier;
            ballObject.transform.position = ballPosition;
            var ballBody = ballObject.GetComponent<Rigidbody>();
            ballBody.mass = MNG_PhysicsProfile.RequiredBallMass;
            ballBody.maxLinearVelocity = physics.MaximumBallSpeed;
            foreach (var collider in ballObject.GetComponents<Collider>())
                collider.material = ballMaterial;

            var geometry = root.GetComponentInChildren<SoccerArenaGeometry>(true);
            Require(geometry != null, "Cloned Stadium is missing SoccerArenaGeometry.");
            var rewardEngine = root.AddComponent<MNG_RewardEngine>();
            var match = root.AddComponent<MNG_MatchController>();
            match.Configure(geometry, ballBody, avatars);
            rewardEngine.Configure(match, redReward, navyReward);
            match.SetRewardEngine(rewardEngine);
            var hud = hudObject.AddComponent<MNG_HudPresenter>();
            hud.Configure(match, rewardEngine);
            var ballControl = ballObject.AddComponent<MNG_BallControl>();
            ballControl.Configure(physics, match);
            var tacticalRewards = root.AddComponent<MNG_TacticalRewardTracker>();
            tacticalRewards.Configure(match, ballControl, rewardEngine);

            for (var i = 0; i < avatars.Length; i++)
                avatars[i].GetComponent<MNG_PlayerSkillExecutor>().Configure(match, ballControl);
            var redStriker = avatars.Single(avatar =>
                avatar.Team == Team.Red && avatar.Role == MNG_PlayerRole.Striker);
            var human = redStriker.gameObject.AddComponent<MNG_HumanInput>();
            human.Configure(match, ballControl);
            CreateSpectatorCamera(root.transform, match, human);

            CreatePolicyEndpoint(root.transform, match, Team.Red);
            CreatePolicyEndpoint(root.transform, match, Team.Navy);
            CreateFallback(root.transform, match, Team.Red);
            CreateFallback(root.transform, match, Team.Navy);
            CreateRuleManager(root.transform, match, Team.Red);
            CreateRuleManager(root.transform, match, Team.Navy);
        }

        static GameObject CreateHudObject(Transform parent)
        {
            var panelSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(
                SharedPanelSettingsPath);
            var visualTree = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(
                SharedHudUxmlPath);
            Require(panelSettings != null && visualTree != null,
                "Shared Soccer HUD assets are missing.");
            var hudObject = new GameObject("MNG HUD");
            hudObject.transform.SetParent(parent, false);
            var document = hudObject.AddComponent<UnityEngine.UIElements.UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = visualTree;
            document.sortingOrder = 10;
            return hudObject;
        }

        static void DestroyLegacyCameraObjects(GameObject root)
        {
            var cameraObjects = root.GetComponentsInChildren<Camera>(true)
                .Select(camera => camera.gameObject)
                .Distinct()
                .ToArray();
            foreach (var cameraObject in cameraObjects)
                UnityEngine.Object.DestroyImmediate(cameraObject);
        }

        static void CreateSpectatorCamera(
            Transform parent,
            MNG_MatchController match,
            MNG_HumanInput human)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(parent, false);
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = true;
            camera.fieldOfView = SoccerPlayerCamera.DefaultOverviewFieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 350f;
            cameraObject.transform.position = SoccerPlayerCamera.DefaultOverviewPosition;
            cameraObject.transform.LookAt(Vector3.zero);
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<MNG_SpectatorCamera>().Configure(match, human);
        }

        static List<RosterEntry> BuildRoster(IReadOnlyList<AgentSoccer> agents)
        {
            var result = new List<RosterEntry>(8);
            foreach (var team in new[] { Team.Red, Team.Navy })
            {
                var teamAgents = agents.Where(agent => agent.Team == team).ToArray();
                Require(teamAgents.Length == 4, $"Expected four {team} players.");
                var keeper = teamAgents.Single(agent => agent.PositionRole == AgentSoccer.Position.DefenderKeeper);
                var striker = teamAgents.Single(agent => agent.PositionRole == AgentSoccer.Position.Striker);
                var sign = team == Team.Red ? 1f : -1f;
                var midfielders = teamAgents
                    .Where(agent => agent.PositionRole == AgentSoccer.Position.Midfielder)
                    .OrderBy(agent => agent.transform.position.z * sign)
                    .ToArray();
                Require(midfielders.Length == 2, $"Expected two {team} midfielders.");
                result.Add(new RosterEntry(keeper, team, 0, MNG_PlayerRole.Keeper));
                result.Add(new RosterEntry(midfielders[0], team, 1, MNG_PlayerRole.MidLeft));
                result.Add(new RosterEntry(midfielders[1], team, 2, MNG_PlayerRole.MidRight));
                result.Add(new RosterEntry(striker, team, 3, MNG_PlayerRole.Striker));
            }
            return result;
        }

        static GameObject RebuildPlayerRoot(GameObject source)
        {
            var replacement = new GameObject(source.name)
            {
                tag = source.tag,
                layer = source.layer
            };
            var sourceTransform = source.transform;
            replacement.transform.SetParent(sourceTransform.parent, false);
            replacement.transform.SetSiblingIndex(sourceTransform.GetSiblingIndex());
            replacement.transform.localPosition = sourceTransform.localPosition;
            replacement.transform.localRotation = sourceTransform.localRotation;
            replacement.transform.localScale = sourceTransform.localScale;

            var sourceBody = source.GetComponent<Rigidbody>();
            Require(sourceBody != null, $"Source player {source.name} has no Rigidbody.");
            var replacementBody = replacement.AddComponent<Rigidbody>();
            EditorUtility.CopySerialized(sourceBody, replacementBody);

            foreach (var sourceCollider in source.GetComponents<Collider>())
            {
                var replacementCollider = replacement.AddComponent(sourceCollider.GetType()) as Collider;
                Require(replacementCollider != null, $"Could not copy {sourceCollider.GetType().Name} on {source.name}.");
                EditorUtility.CopySerialized(sourceCollider, replacementCollider);
            }

            while (sourceTransform.childCount > 0)
                sourceTransform.GetChild(0).SetParent(replacement.transform, false);
            UnityEngine.Object.DestroyImmediate(source);
            return replacement;
        }

        static void CreatePolicyEndpoint(Transform parent, MNG_MatchController match, Team team)
        {
            var gameObject = new GameObject($"MNG_Manager_{team}");
            gameObject.transform.SetParent(parent, false);
            var behavior = gameObject.AddComponent<BehaviorParameters>();
            behavior.BehaviorName = MNG_ManagerAgent.BehaviorName;
            behavior.TeamId = (int)team;
            behavior.BehaviorType = BehaviorType.Default;
            behavior.BrainParameters.VectorObservationSize = MNG_ObservationWriter.ObservationSize;
            behavior.BrainParameters.NumStackedVectorObservations = 1;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(MNG_CommandMask.CommandCount);
            var manager = gameObject.AddComponent<MNG_ManagerAgent>();
            var requester = gameObject.AddComponent<DecisionRequester>();
            requester.DecisionPeriod = MNG_ManagerAgent.DecisionPeriod;
            requester.TakeActionsBetweenDecisions = false;
            manager.Configure(team, match);
            gameObject.SetActive(false);
        }

        static void CreateFallback(Transform parent, MNG_MatchController match, Team team)
        {
            var gameObject = new GameObject($"MNG_Fallback_{team}");
            gameObject.transform.SetParent(parent, false);
            var fallback = gameObject.AddComponent<MNG_FallbackManager>();
            fallback.Configure(team, match);
        }

        static MNG_RuleBasedManager CreateRuleManager(
            Transform parent,
            MNG_MatchController match,
            Team team)
        {
            var gameObject = new GameObject($"MNG_Rule_{team}");
            gameObject.transform.SetParent(parent, false);
            var manager = gameObject.AddComponent<MNG_RuleBasedManager>();
            manager.Configure(team, match);
            manager.enabled = false;
            return manager;
        }

        static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath);
            Require(prefab != null, $"Failed to save {ManagerPrefabPath}");
            PrefabUtility.InstantiatePrefab(prefab, scene);
            Require(EditorSceneManager.SaveScene(scene, ManagerScenePath), $"Failed to save {ManagerScenePath}");
        }

        static void CreateRuleVsRuleScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath);
            Require(prefab != null, $"Failed to load {ManagerPrefabPath}");
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            Require(instance != null, "Failed to instantiate the MNG Stadium for R0 Rule-vs-Rule.");

            foreach (var policy in instance.GetComponentsInChildren<MNG_ManagerAgent>(true))
                policy.gameObject.SetActive(false);
            foreach (var fallback in instance.GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = false;
            foreach (var human in instance.GetComponentsInChildren<MNG_HumanInput>(true))
                human.enabled = false;

            var ruleManagers = instance.GetComponentsInChildren<MNG_RuleBasedManager>(true);
            Require(ruleManagers.Length == 2, "R0 requires exactly two rule managers.");
            foreach (var ruleManager in ruleManagers) ruleManager.enabled = true;
            var red = ruleManagers.Single(manager => manager.Team == Team.Red);
            var navy = ruleManagers.Single(manager => manager.Team == Team.Navy);
            var match = instance.GetComponent<MNG_MatchController>();
            match.ConfigureMatchDuration(
                MNG_MatchController.MatchDurationSeconds,
                true,
                MNG_MatchFinishMode.TerminalResult);
            instance.AddComponent<MNG_R0RuleMatchMonitor>().Configure(match, red, navy, 1f);

            Require(EditorSceneManager.SaveScene(scene, RuleVsRuleScenePath),
                $"Failed to save {RuleVsRuleScenePath}");
        }

        [MenuItem("Tools/Soccer Manager/Build MNG R0 Rule Baseline")]
        public static void BuildR0RuleBaseline()
        {
            EnsureFolders();
            var prefab = PrefabUtility.LoadPrefabContents(ManagerPrefabPath);
            try
            {
                var match = prefab.GetComponent<MNG_MatchController>();
                Require(match != null, "MNG prefab is missing its match controller.");
                var existing = prefab.GetComponentsInChildren<MNG_RuleBasedManager>(true);
                if (existing.Length == 0)
                {
                    CreateRuleManager(prefab.transform, match, Team.Red);
                    CreateRuleManager(prefab.transform, match, Team.Navy);
                }
                else
                {
                    Require(existing.Length == 2, "MNG prefab has an invalid R0 rule manager count.");
                    foreach (var manager in existing)
                    {
                        manager.Configure(manager.Team, match);
                        manager.enabled = false;
                        EditorUtility.SetDirty(manager);
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(prefab, ManagerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefab);
            }

            CreateRuleVsRuleScene();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateM0Assets();
            Debug.Log("MNG R0 RULE BASELINE BUILD PASS");
        }

        static void CreateConnectionSmokeScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath);
            Require(prefab != null, $"Failed to load {ManagerPrefabPath}");
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            Require(instance != null, "Failed to instantiate the MNG Stadium for connection smoke.");

            var redManager = instance.GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Single(manager => manager.Team == Team.Red);
            redManager.gameObject.SetActive(true);
            redManager.enabled = true;
            redManager.GetComponent<DecisionRequester>().enabled = true;
            var redFallback = instance.GetComponentsInChildren<MNG_FallbackManager>(true)
                .Single(fallback => fallback.Team == Team.Red);
            redFallback.enabled = false;
            instance.AddComponent<MNG_TrainingBootstrap>();

            Require(EditorSceneManager.SaveScene(scene, ConnectionSmokeScenePath),
                $"Failed to save {ConnectionSmokeScenePath}");
        }

        static void CreateAttackChoiceScene()
            => CreateAttackChoiceScene(false, AttackChoiceScenePath);

        static void CreateAttackMovingScene()
            => CreateAttackChoiceScene(true, AttackMovingScenePath);

        static void CreateAttackChoiceScene(bool movingDefense, string scenePath)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath);
            Require(prefab != null, $"Failed to load {ManagerPrefabPath}");
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            Require(instance != null, "Failed to instantiate the MNG Stadium for M1 attack choice.");

            var redManager = instance.GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Single(manager => manager.Team == Team.Red);
            redManager.gameObject.SetActive(true);
            redManager.enabled = true;
            redManager.GetComponent<DecisionRequester>().enabled = true;
            foreach (var fallback in instance.GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = false;

            var match = instance.GetComponent<MNG_MatchController>();
            var ball = instance.GetComponentInChildren<MNG_BallControl>();
            var catalog = AssetDatabase.LoadAssetAtPath<MNG_CurriculumCatalog>(CurriculumCatalogPath);
            Require(catalog != null, $"Failed to reload {CurriculumCatalogPath}");
            var curriculum = instance.AddComponent<MNG_CurriculumController>();
            if (movingDefense) curriculum.ConfigureMovingDefense(catalog, match, ball);
            else curriculum.Configure(catalog, match, ball);
            instance.AddComponent<MNG_TrainingBootstrap>();

            Require(EditorSceneManager.SaveScene(scene, scenePath),
                $"Failed to save {scenePath}");
        }

        static void CreateDefenseChoiceScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath);
            Require(prefab != null, $"Failed to load {ManagerPrefabPath}");
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            Require(instance != null, "Failed to instantiate the MNG Stadium for M2 defense choice.");

            var redManager = instance.GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Single(manager => manager.Team == Team.Red);
            redManager.gameObject.SetActive(true);
            redManager.enabled = true;
            redManager.GetComponent<DecisionRequester>().enabled = true;
            foreach (var fallback in instance.GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = fallback.Team == Team.Navy;

            var match = instance.GetComponent<MNG_MatchController>();
            var ball = instance.GetComponentInChildren<MNG_BallControl>();
            var catalog = AssetDatabase.LoadAssetAtPath<MNG_CurriculumCatalog>(CurriculumCatalogPath);
            Require(catalog != null, $"Failed to reload {CurriculumCatalogPath}");
            var curriculum = instance.AddComponent<MNG_CurriculumController>();
            curriculum.ConfigureDefense(catalog, match, ball);
            instance.AddComponent<MNG_TrainingBootstrap>();

            Require(EditorSceneManager.SaveScene(scene, DefenseChoiceScenePath),
                $"Failed to save {DefenseChoiceScenePath}");
        }

        static void CreateFallbackMatchScene(bool fullMatch, string scenePath)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath);
            Require(prefab != null, $"Failed to load {ManagerPrefabPath}");
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            Require(instance != null, "Failed to instantiate the MNG Stadium for M3 fallback match.");

            var redManager = instance.GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Single(manager => manager.Team == Team.Red);
            redManager.gameObject.SetActive(true);
            redManager.enabled = true;
            redManager.GetComponent<DecisionRequester>().enabled = true;
            foreach (var fallback in instance.GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = fallback.Team == Team.Navy;

            var match = instance.GetComponent<MNG_MatchController>();
            var ball = instance.GetComponentInChildren<MNG_BallControl>();
            var catalog = AssetDatabase.LoadAssetAtPath<MNG_CurriculumCatalog>(CurriculumCatalogPath);
            Require(catalog != null, $"Failed to reload {CurriculumCatalogPath}");
            var controller = instance.AddComponent<MNG_FallbackMatchController>();
            controller.Configure(catalog, match, ball, fullMatch);
            instance.AddComponent<MNG_TrainingBootstrap>();

            Require(EditorSceneManager.SaveScene(scene, scenePath), $"Failed to save {scenePath}");
        }

        public static void CreateAttackChoiceEvaluationScene(
            ModelAsset model,
            string runId,
            string modelSha256,
            bool uniformRandom = false)
        {
            Require(model != null, "MNG M1 evaluation model is missing.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath);
            Require(prefab != null, $"Failed to load {ManagerPrefabPath}");
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            Require(instance != null, "Failed to instantiate the MNG Stadium for M1 evaluation.");

            var redManager = instance.GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Single(manager => manager.Team == Team.Red);
            redManager.gameObject.SetActive(true);
            redManager.enabled = true;
            redManager.GetComponent<DecisionRequester>().enabled = true;
            var behavior = redManager.GetComponent<BehaviorParameters>();
            behavior.Model = uniformRandom ? null : model;
            behavior.BehaviorType = uniformRandom ? BehaviorType.HeuristicOnly : BehaviorType.InferenceOnly;
            if (uniformRandom)
                redManager.ConfigureUniformRandomHeuristic(MNG_CurriculumCatalog.M1ValidationSeed);
            foreach (var fallback in instance.GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = false;

            var match = instance.GetComponent<MNG_MatchController>();
            var ball = instance.GetComponentInChildren<MNG_BallControl>();
            var catalog = AssetDatabase.LoadAssetAtPath<MNG_CurriculumCatalog>(CurriculumCatalogPath);
            Require(catalog != null, $"Failed to load {CurriculumCatalogPath}");
            var curriculum = instance.AddComponent<MNG_CurriculumController>();
            curriculum.Configure(catalog, match, ball, true);
            var evaluation = instance.AddComponent<MNG_M1EvaluationController>();
            evaluation.Configure(curriculum, runId, modelSha256,
                uniformRandom ? "uniform-valid-command" : "onnx");

            Require(EditorSceneManager.SaveScene(scene, AttackChoiceEvaluationScenePath),
                $"Failed to save {AttackChoiceEvaluationScenePath}");
        }

        public static void CreateAttackMovingEvaluationScene(
            ModelAsset model,
            string runId,
            string modelSha256)
        {
            Require(model != null, "MNG M1 moving-defense evaluation model is missing.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath);
            Require(prefab != null, $"Failed to load {ManagerPrefabPath}");
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            Require(instance != null,
                "Failed to instantiate the MNG Stadium for M1 moving-defense evaluation.");

            var redManager = instance.GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Single(manager => manager.Team == Team.Red);
            redManager.gameObject.SetActive(true);
            redManager.enabled = true;
            redManager.GetComponent<DecisionRequester>().enabled = true;
            var behavior = redManager.GetComponent<BehaviorParameters>();
            behavior.Model = model;
            behavior.BehaviorType = BehaviorType.InferenceOnly;
            foreach (var fallback in instance.GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = fallback.Team == Team.Navy;

            var match = instance.GetComponent<MNG_MatchController>();
            var ball = instance.GetComponentInChildren<MNG_BallControl>();
            var catalog = AssetDatabase.LoadAssetAtPath<MNG_CurriculumCatalog>(CurriculumCatalogPath);
            Require(catalog != null, $"Failed to load {CurriculumCatalogPath}");
            var curriculum = instance.AddComponent<MNG_CurriculumController>();
            curriculum.ConfigureMovingDefense(catalog, match, ball, true);
            var evaluation = instance.AddComponent<MNG_M1MovingEvaluationController>();
            evaluation.Configure(curriculum, runId, modelSha256);

            Require(EditorSceneManager.SaveScene(scene, AttackMovingEvaluationScenePath),
                $"Failed to save {AttackMovingEvaluationScenePath}");
        }

        public static void CreateDefenseChoiceEvaluationScene(
            ModelAsset model,
            string runId,
            string modelSha256)
        {
            Require(model != null, "MNG M2 evaluation model is missing.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath);
            Require(prefab != null, $"Failed to load {ManagerPrefabPath}");
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            Require(instance != null, "Failed to instantiate the MNG Stadium for M2 evaluation.");

            var redManager = instance.GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Single(manager => manager.Team == Team.Red);
            redManager.gameObject.SetActive(true);
            redManager.enabled = true;
            redManager.GetComponent<DecisionRequester>().enabled = true;
            var behavior = redManager.GetComponent<BehaviorParameters>();
            behavior.Model = model;
            behavior.BehaviorType = BehaviorType.InferenceOnly;
            foreach (var fallback in instance.GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = fallback.Team == Team.Navy;

            var match = instance.GetComponent<MNG_MatchController>();
            var ball = instance.GetComponentInChildren<MNG_BallControl>();
            var catalog = AssetDatabase.LoadAssetAtPath<MNG_CurriculumCatalog>(CurriculumCatalogPath);
            Require(catalog != null, $"Failed to load {CurriculumCatalogPath}");
            var curriculum = instance.AddComponent<MNG_CurriculumController>();
            curriculum.ConfigureDefense(catalog, match, ball, true);
            var evaluation = instance.AddComponent<MNG_M2EvaluationController>();
            evaluation.Configure(curriculum, runId, modelSha256);

            Require(EditorSceneManager.SaveScene(scene, DefenseChoiceEvaluationScenePath),
                $"Failed to save {DefenseChoiceEvaluationScenePath}");
        }

        public static void CreateFallbackMatchEvaluationScene(
            ModelAsset model,
            string runId,
            string modelSha256)
        {
            Require(model != null, "MNG M3 evaluation model is missing.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ManagerPrefabPath);
            Require(prefab != null, $"Failed to load {ManagerPrefabPath}");
            var instance = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            Require(instance != null, "Failed to instantiate the MNG Stadium for M3 evaluation.");

            var redManager = instance.GetComponentsInChildren<MNG_ManagerAgent>(true)
                .Single(manager => manager.Team == Team.Red);
            redManager.gameObject.SetActive(true);
            redManager.enabled = true;
            redManager.GetComponent<DecisionRequester>().enabled = true;
            var behavior = redManager.GetComponent<BehaviorParameters>();
            behavior.Model = model;
            behavior.BehaviorType = BehaviorType.InferenceOnly;
            foreach (var fallback in instance.GetComponentsInChildren<MNG_FallbackManager>(true))
                fallback.enabled = fallback.Team == Team.Navy;

            var match = instance.GetComponent<MNG_MatchController>();
            var ball = instance.GetComponentInChildren<MNG_BallControl>();
            var catalog = AssetDatabase.LoadAssetAtPath<MNG_CurriculumCatalog>(CurriculumCatalogPath);
            Require(catalog != null, $"Failed to load {CurriculumCatalogPath}");
            var controller = instance.AddComponent<MNG_FallbackMatchController>();
            controller.Configure(catalog, match, ball, true, true);
            var evaluation = instance.AddComponent<MNG_M3EvaluationController>();
            evaluation.Configure(controller, runId, modelSha256);

            Require(EditorSceneManager.SaveScene(scene, FallbackMatchEvaluationScenePath),
                $"Failed to save {FallbackMatchEvaluationScenePath}");
        }

        static MNG_CurriculumCatalog GetOrCreateCurriculumCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<MNG_CurriculumCatalog>(CurriculumCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MNG_CurriculumCatalog>();
                AssetDatabase.CreateAsset(catalog, CurriculumCatalogPath);
            }
            catalog.ApplyM1Defaults();
            catalog.ValidateOrThrow();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        static MNG_PhysicsProfile GetOrCreatePhysicsProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<MNG_PhysicsProfile>(PhysicsProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MNG_PhysicsProfile>();
                AssetDatabase.CreateAsset(profile, PhysicsProfilePath);
            }
            profile.ApplyRuntimeTuning();
            profile.ValidateOrThrow();
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static MNG_RewardProfile GetOrCreateRewardProfile(string path, MNG_TacticProfile tactic)
        {
            var profile = AssetDatabase.LoadAssetAtPath<MNG_RewardProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<MNG_RewardProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }
            profile.Configure(tactic);
            Require(MonoScript.FromScriptableObject(profile) != null,
                $"MNG reward profile must be backed by a matching MonoScript: {path}");
            EditorUtility.SetDirty(profile);
            return profile;
        }

        static PhysicsMaterial GetOrCreateBallPhysicsMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(BallPhysicsMaterialPath);
            if (material == null)
            {
                material = new PhysicsMaterial("MNG_BallPhysics");
                AssetDatabase.CreateAsset(material, BallPhysicsMaterialPath);
            }
            material.dynamicFriction = MNG_PhysicsProfile.RequiredBallDynamicFriction;
            material.staticFriction = MNG_PhysicsProfile.RequiredBallStaticFriction;
            material.frictionCombine = PhysicsMaterialCombine.Minimum;
            material.bounciness = MNG_PhysicsProfile.RequiredBallRestitution;
            material.bounceCombine = PhysicsMaterialCombine.Maximum;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void ValidateRoster(IReadOnlyList<MNG_PlayerAvatar> avatars)
        {
            var occupied = new bool[8];
            foreach (var avatar in avatars)
            {
                var index = (avatar.Team == Team.Red ? 0 : 4) + avatar.Slot;
                Require(!occupied[index], $"Duplicate MNG roster slot {avatar.Team}/{avatar.Slot}.");
                occupied[index] = true;
                Require(avatar.Role == (MNG_PlayerRole)avatar.Slot,
                    $"MNG role/slot mismatch {avatar.Team}/{avatar.Slot}/{avatar.Role}.");
            }
            Require(occupied.All(value => value), "MNG roster has an unfilled slot.");
        }

        static void ValidateManagers(IReadOnlyList<MNG_ManagerAgent> managers)
        {
            Require(managers.Count == 2, "MNG policy endpoints count mismatch.");
            foreach (var manager in managers)
            {
                var behavior = manager.GetComponent<BehaviorParameters>();
                var requester = manager.GetComponent<DecisionRequester>();
                Require(behavior.BehaviorName == MNG_ManagerAgent.BehaviorName,
                    $"MNG BehaviorName mismatch on {manager.name}.");
                Require(behavior.BrainParameters.VectorObservationSize == MNG_ObservationWriter.ObservationSize,
                    $"MNG observation size mismatch on {manager.name}.");
                Require(behavior.BrainParameters.ActionSpec.BranchSizes.SequenceEqual(new[] { 6 }),
                    $"MNG action branch mismatch on {manager.name}.");
                Require(requester.DecisionPeriod == MNG_ManagerAgent.DecisionPeriod,
                    $"MNG decision period mismatch on {manager.name}.");
                Require(!requester.TakeActionsBetweenDecisions,
                    $"MNG high-level command must execute only on decision ticks: {manager.name}.");
                var agents = manager.gameObject.GetComponents<Agent>();
                Require(agents.Length == 1 && agents[0] == manager,
                    $"MNG endpoint {manager.name} contains a duplicate or plain Agent component.");
                Require(!manager.gameObject.activeSelf,
                    $"M0 policy endpoint GameObject must stay inactive while explicit fallback drives {manager.Team}.");
            }
        }

        static void WriteSourceSnapshot(
            Vector3 ballScale,
            float ballY,
            float ballMass,
            SoccerArenaGeometry geometry)
        {
            Directory.CreateDirectory("Logs");
            File.WriteAllText(
                "Logs/MNG-M0-Source-Snapshot.txt",
                $"source={SourcePrefabPath}{Environment.NewLine}"
                + $"ballScale={ballScale.x:R},{ballScale.y:R},{ballScale.z:R}{Environment.NewLine}"
                + $"ballCenterY={ballY:R}{Environment.NewLine}"
                + $"ballMass={ballMass:R}{Environment.NewLine}"
                + $"fieldHalfLength={geometry.HalfLength:R}{Environment.NewLine}"
                + $"fieldHalfWidth={geometry.HalfWidth:R}{Environment.NewLine}"
                + $"goalHalfWidth={geometry.GoalHalfWidth:R}{Environment.NewLine}");
        }

        static void EnsureFolders()
        {
            EnsureFolder("Assets/_Soccer/Manager", "Editor");
            EnsureFolder("Assets/_Soccer/Manager", "Profiles");
            EnsureFolder("Assets/_Soccer/Manager", "Prefabs");
            EnsureFolder("Assets/_Soccer/Manager", "Scenes");
            EnsureFolder("Assets/_Soccer/Manager", "Curriculum");
            EnsureFolder("Assets/_Soccer/Manager/Curriculum", "R0_RuleBaseline");
            EnsureFolder("Assets/_Soccer/Manager/Curriculum/R0_RuleBaseline", "Scenes");
            EnsureFolder("Assets/_Soccer/Manager/Curriculum", "M1_AttackChoice");
            EnsureFolder("Assets/_Soccer/Manager/Curriculum/M1_AttackChoice", "Scenes");
            EnsureFolder("Assets/_Soccer/Manager/Curriculum", "M1_AttackMoving");
            EnsureFolder("Assets/_Soccer/Manager/Curriculum/M1_AttackMoving", "Scenes");
            EnsureFolder("Assets/_Soccer/Manager/Curriculum", "M2_DefenseChoice");
            EnsureFolder("Assets/_Soccer/Manager/Curriculum/M2_DefenseChoice", "Scenes");
            EnsureFolder("Assets/_Soccer/Manager/Curriculum", "M3_FallbackMatch");
            EnsureFolder("Assets/_Soccer/Manager/Curriculum/M3_FallbackMatch", "Scenes");
            EnsureFolder("Assets/_Soccer/Manager", "Evaluation");
            EnsureFolder("Assets/_Soccer/Manager", "Models");
        }

        static void EnsureFolder(string parent, string child)
        {
            var path = $"{parent}/{child}";
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
        }

        static void DestroyAll<T>(IEnumerable<T> components) where T : UnityEngine.Object
        {
            foreach (var component in components.ToArray())
                UnityEngine.Object.DestroyImmediate(component);
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        readonly struct RosterEntry
        {
            public readonly AgentSoccer Agent;
            public readonly Team Team;
            public readonly int Slot;
            public readonly MNG_PlayerRole Role;

            public RosterEntry(AgentSoccer agent, Team team, int slot, MNG_PlayerRole role)
            {
                Agent = agent;
                Team = team;
                Slot = slot;
                Role = role;
            }
        }
    }
}
