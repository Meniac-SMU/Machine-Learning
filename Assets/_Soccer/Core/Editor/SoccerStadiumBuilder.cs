using System;
using System.IO;
using System.Linq;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MachineLearning.Soccer.Editor
{
    /// <summary>Builds the canonical Core Stadium and propagates it to every active team workspace.</summary>
    public static class SoccerStadiumBuilder
    {
        public const string ScenePath = "Assets/_Soccer/Core/Scenes/Stadium4v4_Base.unity";
        public const string PrefabPath = "Assets/_Soccer/Core/Prefabs/StadiumEnvironment_Base.prefab";
        public const string SourceScenePath = "Assets/Hayq Art/GrantStadium/Scenes/Demo.unity";
        const string TerrainPath = "Assets/_Soccer/Core/Terrain/StadiumTerrain.asset";
        const string WallMaterialPath = "Assets/_Soccer/Core/Materials/StadiumSkyBlueWall.mat";
        public const string RedGoalMaterialPath = "Assets/_Soccer/Core/Materials/StadiumRedGoal.mat";
        public const string NavyGoalMaterialPath = "Assets/_Soccer/Core/Materials/StadiumNavyGoal.mat";
        public const string GoalFloorMaterialPath = "Assets/_Soccer/Core/Materials/StadiumGoalFloor.mat";
        const string RedPlayerMaterialPath = "Assets/_Soccer/Materials/AgentRed.mat";
        const string NavyPlayerMaterialPath = "Assets/_Soccer/Materials/AgentNavy.mat";
        const string PanelSettingsPath = "Assets/_Soccer/UI/SoccerPanelSettings.asset";
        const string HudUxmlPath = "Assets/_Soccer/UI/SoccerHud.uxml";
        const float Length = 124f;
        public const float WallHeight = 4.5f * 0.7f * 0.9f;
        public const float WallThickness = 0.12f;
        public const float WallAlpha = 0.28f * 0.8f;
        public const float BallScale = 0.0105f * 1.1f * 1.1f;
        public const float BallGroundClearance = 0f;
        public const float GoalWidthMultiplier = 2f;
        public const float PassPower = 2000f;
        public const float ShotPower = 5000f;
        public const float CornerPanelThickness = WallThickness;
        public const float CornerChordLength = 1.5f;
        const float CornerJointOverlap = 0.04f;
        public static readonly float CornerRadius = CornerChordLength / (2f * Mathf.Sin(15f * Mathf.Deg2Rad));
        public static readonly Color RedGoalColor = SoccerTeamVisuals.RedGoalColor;
        public static readonly Color NavyGoalColor = SoccerTeamVisuals.NavyGoalColor;
        public static readonly Color GoalFloorColor = new Color32(210, 214, 219, 255);
        static readonly string[] ExteriorOptimizationPrefabNames =
        {
            // Road and parking instances approved for removal.
            "SM_Road_P2", "SM_Road_P6", "SM_Road_P", "SM_R_P8", "SM_Road_P3",
            "SM_Road_P5", "SM_Air_Road6", "Parking", "SM_R_Gr3", "SM_R_Gr1",
            // Exterior bushes and flowers approved for removal.
            "SM_Bush_10", "SM_Bush_03", "SM_Bush_02", "SM_Bush_09", "SM_Bush_11",
            "SM_Bush_08", "SM_Bush_05", "SM_Bush_06", "SM_Flower_A", "SM_Flowers_01",
            // Exterior city props approved for removal.
            "SM_City_Light", "SM_City_Bench7", "SM_City_Bench1", "SM_City_Bench2",
            "SM_City_Fence1", "SM_City_Barrier", "SM_Traffic_Light2", "SM_Bike"
        };

        [MenuItem("Tools/Soccer/Stadium/Build all team Stadiums")]
        public static void BuildBatch()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EnsureDirectory("Assets/_Soccer/Core/Terrain");
            EnsureDirectory("Assets/_Soccer/Core/Materials");
            PrepareTeamGoalMaterials();
            Directory.CreateDirectory("Logs");
            if (File.Exists(ScenePath))
                File.Copy(ScenePath, "Logs/Stadium4v4-before-rebuild-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", false);

            var scene = EditorSceneManager.OpenScene(SourceScenePath, OpenSceneMode.Single);
            Require(EditorSceneManager.SaveScene(scene, ScenePath), "Could not duplicate Demo into Core.");
            var sourceRoots = scene.GetRootGameObjects();
            foreach (var item in sourceRoots.SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
            {
                var missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject);
                if (missing == 0) continue;
                Require(item.name == "Main Camera" || item.name == "PP_Demo",
                    "Unexpected missing Demo script: " + item.name);
                // The supplied Demo references absent post-processing components.
                // Remove only those broken components in our copy, retaining the objects.
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(item.gameObject);
                Debug.Log($"STADIUM: removed {missing} unavailable Demo camera/post-process component(s) from copy: {item.name}");
            }
            var field = sourceRoots.SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true))
                .Single(renderer => renderer.name == "SM_Football_Field");
            var goals = sourceRoots.SelectMany(root => root.GetComponentsInChildren<MeshRenderer>(true))
                .Where(renderer => renderer.name.StartsWith("SM_S_Gate", StringComparison.Ordinal))
                .OrderBy(renderer => renderer.bounds.center.x).ToArray();
            Require(goals.Length == 2, "Demo must contain exactly two SM_S_Gate goal meshes.");
            var sourceBounds = field.bounds;
            var sourceCentre = new Vector3(sourceBounds.center.x, sourceBounds.max.y, sourceBounds.center.z);
            var scale = Length / sourceBounds.size.x;
            Require(scale >= 1f && scale < 2f, "Unexpected Demo scale; inspect before resizing.");
            Require(Mathf.Abs(Vector3.Dot(field.transform.right.normalized, Vector3.right)) > 0.9999f,
                "Demo pitch is not aligned with the shared X attack axis.");
            var records = sourceRoots.Select(root => new RootRecord
            {
                name = root.name, sourcePosition = root.transform.position, sourceScale = root.transform.lossyScale
            }).ToArray();

            foreach (var root in sourceRoots)
            {
                root.transform.position = (root.transform.position - sourceCentre) * scale;
                var terrain = root.GetComponent<Terrain>();
                if (terrain != null)
                {
                    ConfigureTerrain(terrain, scale);
                    root.transform.localScale = Vector3.one;
                }
                else root.transform.localScale *= scale;
            }
            Physics.SyncTransforms();
            var width = field.bounds.size.z;
            // Stadium is the only active arena. Preserve the already migrated players,
            // rules and match setup from the canonical Stadium prefab instead of taking
            // a hidden dependency on the retired rectangular Base environment.
            var canonicalEnvironment = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Require(canonicalEnvironment != null,
                "Canonical Stadium prefab is missing. Restore StadiumEnvironment_Base before rebuilding its Demo design.");
            var environmentObject = (GameObject)PrefabUtility.InstantiatePrefab(canonicalEnvironment, scene);
            PrefabUtility.UnpackPrefabInstance(environmentObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            environmentObject.name = "StadiumEnvironment_Base";
            var environment = environmentObject.GetComponent<SoccerEnvController>();
            Require(environment != null, "Canonical Stadium prefab is missing SoccerEnvController.");
            var oldPhysics = environmentObject.transform.Find("StadiumPhysics");
            var oldPitch = oldPhysics != null ? oldPhysics.Find("PitchCollision") : null;
            var oldPitchCollider = oldPitch != null ? oldPitch.GetComponent<Collider>() : null;
            Require(oldPitchCollider != null && oldPitchCollider.sharedMaterial != null,
                "Canonical Stadium prefab is missing its pitch physics material.");
            var pitchMaterial = oldPitchCollider.sharedMaterial;
            var oldDesign = environmentObject.transform.Find("DemoDesign");
            if (oldDesign != null) Object.DestroyImmediate(oldDesign.gameObject);
            if (oldPhysics != null) Object.DestroyImmediate(oldPhysics.gameObject);
            var oldGeometry = environmentObject.GetComponent<SoccerArenaGeometry>();
            if (oldGeometry != null) Object.DestroyImmediate(oldGeometry);
            var design = new GameObject("DemoDesign");
            design.transform.SetParent(environmentObject.transform, false);
            foreach (var root in sourceRoots) root.transform.SetParent(design.transform, true);
            RemoveStadiumTrees(environmentObject);
            RemoveExteriorOptimizationInstances(environmentObject);
            foreach (var root in sourceRoots)
            {
                foreach (var collider in root.GetComponentsInChildren<Collider>(true))
                {
                    if (collider is TerrainCollider || goals.Any(goal => goal.gameObject == collider.gameObject)) continue;
                    collider.enabled = false; // no scenery occlusion or duplicate pitch floor
                }
                foreach (var camera in root.GetComponentsInChildren<Camera>(true))
                {
                    camera.enabled = false;
                    if (camera.CompareTag("MainCamera")) camera.tag = "Untagged";
                }
                foreach (var listener in root.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;
            }
            var physicsRoot = new GameObject("StadiumPhysics");
            physicsRoot.transform.SetParent(environmentObject.transform, false);
            var pitch = AddBox(physicsRoot.transform, "PitchCollision", new Vector3(0f, -0.25f, 0f),
                new Vector3(Length, 0.5f, width), "Untagged", pitchMaterial);
            pitch.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");

            for (var index = 0; index < goals.Length; index++)
            {
                var goal = goals[index];
                ScaleGoalWidth(goal, GoalWidthMultiplier);
                var team = index == 0 ? Team.Red : Team.Navy;
                var sign = index == 0 ? -1f : 1f;
                var bounds = goal.bounds;
                var mouthX = index == 0 ? bounds.max.x : bounds.min.x;
                goal.transform.position += new Vector3(sign * Length * 0.5f - mouthX, -bounds.min.y, -bounds.center.z);
                goal.gameObject.tag = index == 0 ? "redGoal" : "navyGoal";
                GameObjectUtility.SetStaticEditorFlags(goal.gameObject, 0);
                goal.gameObject.AddComponent<SoccerGoalSurface>().Configure(team, false);
                foreach (var collider in goal.GetComponents<Collider>())
                {
                    collider.enabled = true;
                    collider.sharedMaterial = pitchMaterial;
                }
            }
            ConfigureGoalAppearance(goals);
            Physics.SyncTransforms();
            // The posts widen with the same local horizontal axis as the mesh.
            var goalWidth = Mathf.Min(goals[0].bounds.size.z, goals[1].bounds.size.z) - 0.4f * GoalWidthMultiplier;
            var goalHeight = Mathf.Min(goals[0].bounds.size.y, goals[1].bounds.size.y) - 0.2f;
            var goalDepth = Mathf.Min(goals[0].bounds.size.x, goals[1].bounds.size.x);
            var geometry = environmentObject.AddComponent<SoccerArenaGeometry>();
            geometry.Configure(Length, width, goalWidth, goalHeight, goalDepth, CornerRadius, GoalWidthMultiplier);
            environment.ConfigureArena(geometry);
            environment.ConfigureControlledKickPower(PassPower);
            environment.ConfigureStrongKickPower(ShotPower);
            var wallMaterial = CreateWallMaterial();
            var goalFloorMaterial = CreateGoalFloorMaterial();
            BuildWalls(physicsRoot.transform, geometry, wallMaterial, pitchMaterial);
            BuildGoalInterior(physicsRoot.transform, geometry, Team.Red, pitchMaterial, goalFloorMaterial);
            BuildGoalInterior(physicsRoot.transform, geometry, Team.Navy, pitchMaterial, goalFloorMaterial);

            environment.ball.transform.localScale = Vector3.one * BallScale;
            var ballRadius = environment.ball.GetComponent<SphereCollider>().radius * BallScale;
            environment.ball.transform.position = new Vector3(0f, ballRadius + BallGroundClearance, 0f);
            var ballController = environment.ball.GetComponent<SoccerBallController>();
            ballController.Configure(environmentObject);
            ballController.ConfigureStadiumMotion(ballRadius + BallGroundClearance);
            foreach (var item in environment.AgentsList)
            {
                var agent = item.Agent;
                var position = agent.transform.position;
                position.y = 0.52f;
                agent.transform.position = position;
                agent.Configure(agent.Team, agent.PositionRole, agent.HumanControllable, position);
            }
            CopyPresentation(scene, environment, goals);
            Require(PrefabUtility.SaveAsPrefabAssetAndConnect(environmentObject, PrefabPath,
                InteractionMode.AutomatedAction) != null, "Could not save Core stadium prefab.");
            Require(EditorSceneManager.SaveScene(scene), "Could not save stadium scene.");
            for (var i = 0; i < sourceRoots.Length; i++)
            {
                records[i].finalPosition = sourceRoots[i].transform.position;
                records[i].finalScale = sourceRoots[i].transform.lossyScale;
                Require(Vector3.Distance(records[i].finalPosition,
                    (records[i].sourcePosition - sourceCentre) * scale) < 0.002f,
                    "A Demo root did not receive the common transform: " + records[i].name);
            }
            var report = new BuildReport
            {
                sourceFieldBounds = sourceBounds, sourceFieldCentre = sourceCentre,
                additionalUniformScale = scale, finalFieldBounds = field.bounds,
                goalOpeningWidth = goalWidth, goalHeight = goalHeight, goalDepth = goalDepth,
                ballDiameter = ballRadius * 2f, roots = records
            };
            File.WriteAllText("Logs/Stadium4v4-Geometry.json", JsonUtility.ToJson(report, true));
            ValidateScene(scene);
            WriteTuningReport(scene);
            SoccerProjectBuilder.BuildStadiumWorkspacesBatch();
            Debug.Log("ALL TEAM STADIUM BUILD PASS " + JsonUtility.ToJson(report));
        }

        [MenuItem("Tools/Soccer/Stadium/Apply current Stadium tuning (preserves terrain and UI)")]
        public static void ApplyTuningBatch()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory("Logs");
            File.Copy(ScenePath, "Logs/Stadium4v4-before-tuning-" + stamp + ".unity", false);
            File.Copy(PrefabPath, "Logs/Stadium4v4-before-tuning-" + stamp + ".prefab", false);
            PrepareTeamGoalMaterials();
            CreateWallMaterial();
            CreateGoalFloorMaterial();
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                ApplyTuningToEnvironment(contents);
                Require(PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath) != null, "Could not save Stadium tuning.");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var hud = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<SoccerHudController>(true)).Single();
            hud.ConfigureRewardPanelBottomRight(true);
            EditorUtility.SetDirty(hud);
            ValidateScene(scene);
            Require(EditorSceneManager.SaveScene(scene), "Could not save Stadium scene.");
            WriteTuningReport(scene);
            SoccerProjectBuilder.BuildStadiumWorkspacesBatch();
            Debug.Log("ALL TEAM STADIUM TUNING PASS");
        }

        // Absolute targets and the stored authoring multiplier make repeated
        // tuning safe: neither ball size nor goal width grows on each invocation.
        public static void ApplyTuningToEnvironment(GameObject root)
        {
            var environment = root.GetComponent<SoccerEnvController>();
            Require(environment != null && environment.ArenaGeometry != null, "Not a Stadium environment.");
            var arena = environment.ArenaGeometry;
            var goals = root.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.name.StartsWith("SM_S_Gate", StringComparison.Ordinal))
                .OrderBy(renderer => renderer.bounds.center.x).ToArray();
            Require(goals.Length == 2, "Expected two Demo goals.");
            var ratio = GoalWidthMultiplier / arena.GoalModelWidthMultiplier;
            foreach (var goal in goals)
            {
                var sign = goal.bounds.center.x < 0f ? -1f : 1f;
                ScaleGoalWidth(goal, ratio);
                var bounds = goal.bounds;
                var mouthX = sign < 0f ? bounds.max.x : bounds.min.x;
                goal.transform.position += new Vector3(sign * arena.HalfLength - mouthX, -bounds.min.y, -bounds.center.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(goal.transform);
            }
            arena.Configure(arena.HalfLength * 2f, arena.HalfWidth * 2f, arena.GoalHalfWidth * 2f * ratio,
                arena.GoalHeight, goals.Min(goal => goal.bounds.size.x), CornerRadius, GoalWidthMultiplier);
            environment.ConfigureControlledKickPower(PassPower);
            environment.ConfigureStrongKickPower(ShotPower);
            environment.ball.transform.localScale = Vector3.one * BallScale;
            var radius = environment.ball.GetComponent<SphereCollider>().radius * BallScale;
            environment.ball.transform.position = new Vector3(0f, radius + BallGroundClearance, 0f);
            var ballController = environment.ball.GetComponent<SoccerBallController>();
            ballController.Configure(root);
            ballController.ConfigureStadiumMotion(radius + BallGroundClearance);
            var physicsRoot = root.transform.Find("StadiumPhysics");
            Require(physicsRoot != null, "Missing Stadium physics root.");
            var physics = physicsRoot.Find("PitchCollision").GetComponent<Collider>().sharedMaterial;
            var wallMaterial = AssetDatabase.LoadAssetAtPath<Material>(WallMaterialPath);
            Require(wallMaterial != null, "Missing Stadium wall material.");
            var goalFloorMaterial = AssetDatabase.LoadAssetAtPath<Material>(GoalFloorMaterialPath);
            Require(goalFloorMaterial != null, "Missing Stadium goal-floor material.");
            // Replace only generated boundary/goal pieces in this owned copy.
            foreach (var child in physicsRoot.Cast<Transform>().ToArray())
            {
                if (child.name.StartsWith("Wall_", StringComparison.Ordinal)
                    || child.name.StartsWith("Red_Goal_", StringComparison.Ordinal)
                    || child.name.StartsWith("Navy_Goal_", StringComparison.Ordinal))
                    Object.DestroyImmediate(child.gameObject);
            }
            BuildWalls(physicsRoot, arena, wallMaterial, physics);
            BuildGoalInterior(physicsRoot, arena, Team.Red, physics, goalFloorMaterial);
            BuildGoalInterior(physicsRoot, arena, Team.Navy, physics, goalFloorMaterial);
            ConfigureGoalAppearance(goals);
            RemoveStadiumTrees(root);
            RemoveExteriorOptimizationInstances(root);
            Physics.SyncTransforms();
        }

        public static void PrepareTeamGoalMaterials()
        {
            EnsureDirectory("Assets/_Soccer/Core/Materials");
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Require(shader != null, "Missing URP Lit shader for Stadium goals.");
            foreach (var team in new[] { Team.Red, Team.Navy })
            {
                var material = new Material(shader) { name = "Stadium" + team + "Goal" };
                // A fresh material avoids tinting the vendor's yellow texture atlas.
                // The existing fader clones this material and changes alpha only.
                material.SetColor("_BaseColor", team == Team.Red ? RedGoalColor : NavyGoalColor);
                material.SetFloat("_Smoothness", 0.25f);
                material.SetFloat("_Cull", (float)CullMode.Off);
                SaveOwnedAsset(material, team == Team.Red ? RedGoalMaterialPath : NavyGoalMaterialPath);
            }
        }

        static void ConfigureGoalAppearance(Renderer[] goals)
        {
            foreach (var goal in goals)
            {
                Require(goal.CompareTag("redGoal") || goal.CompareTag("navyGoal"), "Goal has no defending team tag.");
                var path = goal.CompareTag("redGoal") ? RedGoalMaterialPath : NavyGoalMaterialPath;
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Require(material != null, "Missing Stadium team goal material: " + path);
                goal.sharedMaterials = Enumerable.Repeat(material, goal.sharedMaterials.Length).ToArray();
                PrefabUtility.RecordPrefabInstancePropertyModifications(goal);
            }
        }

        static bool IsTreeName(string value)
        {
            return value.StartsWith("SM_Tree_", StringComparison.Ordinal)
                || value.StartsWith("SM_Fir_", StringComparison.Ordinal);
        }

        static bool IsTreeObject(Transform item)
        {
            return IsTreeName(item.name) || (item.TryGetComponent<MeshFilter>(out var filter)
                && filter.sharedMesh != null && IsTreeName(filter.sharedMesh.name));
        }

        static void RemoveStadiumTrees(GameObject root)
        {
            var trees = root.GetComponentsInChildren<Transform>(true).Where(IsTreeObject).ToArray();
            var removedObjects = 0;
            foreach (var tree in trees)
            {
                if (tree == null) continue;
                Require(!PrefabUtility.IsPartOfPrefabInstance(tree.gameObject)
                    || PrefabUtility.IsAnyPrefabInstanceRoot(tree.gameObject), "Unexpected nested tree structure: " + tree.name);
                Object.DestroyImmediate(tree.gameObject); // authorized removal of scene instances, never source assets
                removedObjects++;
            }
            var removedTerrainTrees = 0;
            foreach (var terrain in root.GetComponentsInChildren<Terrain>(true))
            {
                var data = terrain.terrainData;
                if (data.treeInstanceCount == 0) continue;
                Require(AssetDatabase.GetAssetPath(data) == TerrainPath, "Refusing to modify a shared TerrainData asset.");
                removedTerrainTrees += data.treeInstanceCount;
                data.treeInstances = Array.Empty<TreeInstance>();
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssetIfDirty(data);
            }
            Debug.Log($"STADIUM TREE REMOVAL objects={removedObjects}, terrainInstances={removedTerrainTrees}");
        }

        public static bool IsRemovedExteriorPrefabName(string value)
        {
            return !string.IsNullOrEmpty(value)
                && ExteriorOptimizationPrefabNames.Contains(value, StringComparer.Ordinal);
        }

        public static bool IsExteriorOptimizationPrefabInstance(Transform item)
        {
            if (item == null || !PrefabUtility.IsAnyPrefabInstanceRoot(item.gameObject))
            {
                return false;
            }

            var sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(item.gameObject);
            var sourceName = Path.GetFileNameWithoutExtension(sourcePath);
            return IsRemovedExteriorPrefabName(sourceName) || IsRemovedExteriorPrefabName(item.name);
        }

        static void RemoveExteriorOptimizationInstances(GameObject root)
        {
            var targets = root.GetComponentsInChildren<Transform>(true)
                .Where(IsExteriorOptimizationPrefabInstance)
                .ToArray();
            foreach (var target in targets)
            {
                if (target != null)
                {
                    Object.DestroyImmediate(target.gameObject);
                }
            }

            Require(!root.GetComponentsInChildren<Transform>(true)
                    .Any(item => IsExteriorOptimizationPrefabInstance(item)
                        || IsRemovedExteriorPrefabName(item.name)),
                "An approved road, landscaping, or exterior city-prop instance remains in the Stadium.");
            Debug.Log($"STADIUM EXTERIOR OPTIMIZATION REMOVAL objects={targets.Length}");
        }

        static void ScaleGoalWidth(Renderer goal, float multiplier)
        {
            var transform = goal.transform;
            var xAlignment = Mathf.Abs(Vector3.Dot(transform.right, Vector3.forward));
            var zAlignment = Mathf.Abs(Vector3.Dot(transform.forward, Vector3.forward));
            Require(Mathf.Max(xAlignment, zAlignment) > 0.9999f, "Demo goal width axis is not aligned with world Z.");
            var scale = transform.localScale;
            if (xAlignment > zAlignment) scale.x *= multiplier;
            else scale.z *= multiplier;
            transform.localScale = scale;
        }

        static void WriteTuningReport(Scene scene)
        {
            var root = scene.GetRootGameObjects().Single(item => item.GetComponent<SoccerEnvController>() != null);
            var environment = root.GetComponent<SoccerEnvController>();
            var arena = environment.ArenaGeometry;
            var hud = scene.GetRootGameObjects().SelectMany(item => item.GetComponentsInChildren<SoccerHudController>(true)).Single();
            var wallMaterial = AssetDatabase.LoadAssetAtPath<Material>(WallMaterialPath);
            var goalFloorMaterial = AssetDatabase.LoadAssetAtPath<Material>(GoalFloorMaterialPath);
            var ballController = environment.ball.GetComponent<SoccerBallController>();
            var report = new TuningReport
            {
                fieldLength = arena.HalfLength * 2f, fieldWidth = arena.HalfWidth * 2f,
                ballScale = BallScale, ballDiameter = environment.ball.GetComponent<SphereCollider>().radius * BallScale * 2f,
                ballGroundClearance = BallGroundClearance, ballLockedCenterHeight = ballController.LockedCenterHeight,
                stadiumPlanarRolling = ballController.EnforcesStadiumPlanarMotion,
                wallHeight = WallHeight, goalOpeningWidth = arena.GoalHalfWidth * 2f,
                goalDepth = arena.GoalDepth, goalHeight = arena.GoalHeight,
                goalModelWidthMultiplier = arena.GoalModelWidthMultiplier, passPower = environment.ControlledKickPower,
                shotPower = environment.StrongKickPower, wallThickness = WallThickness,
                wallAlpha = wallMaterial.GetColor("_BaseColor").a, rewardPanelBottomRight = hud.RewardPanelBottomRight,
                cornerRadius = arena.CornerRadius, cornerPanels = SoccerArenaGeometry.CornerSegments * 4,
                cornerPanelLength = CornerChordLength + CornerJointOverlap, cornerPanelThickness = CornerPanelThickness,
                removedCornerArea = arena.CornerRadius * arena.CornerRadius,
                treeObjects = root.GetComponentsInChildren<Transform>(true).Count(IsTreeObject),
                terrainTrees = root.GetComponentsInChildren<Terrain>(true).Sum(terrain => terrain.terrainData.treeInstanceCount),
                goalFloorRenderers = root.GetComponentsInChildren<MeshRenderer>(true)
                    .Count(renderer => renderer.name.EndsWith("_Goal_Floor", StringComparison.Ordinal)),
                goalFloorColor = goalFloorMaterial.GetColor("_BaseColor"),
                redGoalColor = root.GetComponentsInChildren<MeshRenderer>().Single(renderer =>
                    renderer.name.StartsWith("SM_S_Gate", StringComparison.Ordinal) && renderer.CompareTag("redGoal")).sharedMaterial.GetColor("_BaseColor"),
                navyGoalColor = root.GetComponentsInChildren<MeshRenderer>().Single(renderer =>
                    renderer.name.StartsWith("SM_S_Gate", StringComparison.Ordinal) && renderer.CompareTag("navyGoal")).sharedMaterial.GetColor("_BaseColor"),
                goalMeshSizes = root.GetComponentsInChildren<MeshRenderer>()
                    .Where(renderer => renderer.name.StartsWith("SM_S_Gate", StringComparison.Ordinal))
                    .Select(renderer => renderer.bounds.size).ToArray()
            };
            File.WriteAllText("Logs/Stadium4v4-Tuning-Geometry.json", JsonUtility.ToJson(report, true));
            Debug.Log("STADIUM TUNING GEOMETRY " + JsonUtility.ToJson(report));
        }

        static void ConfigureTerrain(Terrain terrain, float scale)
        {
            var source = terrain.terrainData;
            var copy = Object.Instantiate(source);
            copy.name = "StadiumTerrain";
            copy.size = source.size * scale;
            var trees = copy.treeInstances;
            for (var i = 0; i < trees.Length; i++)
            {
                trees[i].widthScale *= scale;
                trees[i].heightScale *= scale;
            }
            copy.treeInstances = trees;
            var details = copy.detailPrototypes;
            foreach (var detail in details)
            {
                detail.minWidth *= scale; detail.maxWidth *= scale;
                detail.minHeight *= scale; detail.maxHeight *= scale;
            }
            copy.detailPrototypes = details;
            var layers = copy.terrainLayers;
            for (var i = 0; i < layers.Length; i++)
            {
                if (layers[i] == null) continue;
                var layer = Object.Instantiate(layers[i]);
                layer.tileSize *= scale;
                layer.tileOffset *= scale;
                layers[i] = SaveOwnedAsset(layer, $"Assets/_Soccer/Core/Terrain/StadiumLayer{i}.terrainlayer");
            }
            copy.terrainLayers = layers;
            terrain.terrainData = SaveOwnedAsset(copy, TerrainPath);
            terrain.GetComponent<TerrainCollider>().terrainData = terrain.terrainData;
            terrain.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        }

        static T SaveOwnedAsset<T>(T value, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(value, existing);
                Object.DestroyImmediate(value);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(value, path);
            return value;
        }

        static Material CreateWallMaterial()
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Soccer/Materials/ClearPlastic.mat");
            var material = new Material(source) { name = "StadiumSkyBlueWall" };
            material.SetColor("_BaseColor", new Color(0.40f, 0.78f, 1f, WallAlpha));
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = (int)RenderQueue.Transparent;
            return SaveOwnedAsset(material, WallMaterialPath);
        }

        static Material CreateGoalFloorMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            Require(shader != null, "Missing URP Lit shader for Stadium goal floors.");
            var material = new Material(shader) { name = "StadiumGoalFloor" };
            material.SetColor("_BaseColor", GoalFloorColor);
            material.SetFloat("_Smoothness", 0.15f);
            material.SetFloat("_Cull", (float)CullMode.Back);
            return SaveOwnedAsset(material, GoalFloorMaterialPath);
        }

        static BoxCollider AddBox(Transform parent, string name, Vector3 position, Vector3 size,
            string tag, PhysicsMaterial material, Material visibleMaterial = null)
        {
            var obj = visibleMaterial != null ? GameObject.CreatePrimitive(PrimitiveType.Cube) : new GameObject();
            obj.name = name;
            obj.tag = tag;
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            var collider = obj.GetComponent<BoxCollider>();
            if (collider == null) collider = obj.AddComponent<BoxCollider>();
            if (visibleMaterial != null)
            {
                obj.transform.localScale = size;
                var renderer = obj.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = visibleMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            else collider.size = size;
            collider.sharedMaterial = material;
            return collider;
        }

        static void BuildWalls(Transform parent, SoccerArenaGeometry geometry, Material visible, PhysicsMaterial physics)
        {
            foreach (var sign in new[] { -1f, 1f })
            {
                AddBox(parent, sign < 0 ? "Wall_South" : "Wall_North",
                    new Vector3(0f, WallHeight * 0.5f, sign * (geometry.HalfWidth + WallThickness * 0.5f)),
                    new Vector3(Length - geometry.CornerRadius * 2f + CornerJointOverlap, WallHeight, WallThickness), "wall", physics, visible);
                var endWidth = geometry.HalfWidth - geometry.CornerRadius - geometry.GoalHalfWidth;
                foreach (var lateral in new[] { -1f, 1f })
                    AddBox(parent, $"Wall_End_{sign}_{lateral}",
                        new Vector3(sign * (geometry.HalfLength + WallThickness * 0.5f), WallHeight * 0.5f,
                            lateral * (geometry.GoalHalfWidth + endWidth * 0.5f)),
                        new Vector3(WallThickness, WallHeight, endWidth + CornerJointOverlap), "wall", physics, visible);
            }
            foreach (var xSign in new[] { -1, 1 })
            foreach (var zSign in new[] { -1, 1 })
            for (var segment = 0; segment < SoccerArenaGeometry.CornerSegments; segment++)
            {
                var start = geometry.GetCornerPoint(xSign, zSign, segment, WallHeight * 0.5f);
                var end = geometry.GetCornerPoint(xSign, zSign, segment + 1, WallHeight * 0.5f);
                var outward = geometry.GetCornerNormal(xSign, zSign, segment);
                var direction = (end - start).normalized;
                var panel = AddBox(parent, $"Wall_Corner_{xSign}_{zSign}_{segment}",
                    (start + end) * 0.5f + outward * (CornerPanelThickness * 0.5f),
                    new Vector3(Vector3.Distance(start, end) + CornerJointOverlap, WallHeight, CornerPanelThickness),
                    "wall", physics, visible);
                panel.transform.rotation = Quaternion.LookRotation(Vector3.Cross(direction, Vector3.up), Vector3.up);
            }
        }

        static void BuildGoalInterior(Transform parent, SoccerArenaGeometry geometry, Team defendingTeam,
            PhysicsMaterial material, Material floorMaterial)
        {
            var sign = defendingTeam == Team.Red ? -1f : 1f;
            var tag = defendingTeam == Team.Red ? "redGoal" : "navyGoal";
            var centreX = sign * (geometry.HalfLength + geometry.GoalDepth * 0.5f);
            // The shared ball locks Y, so scoring must not depend on touching
            // the floor. Keep this volume out of the unchanged ray layer masks.
            var volume = AddBox(parent, defendingTeam + "_Goal_ScoringVolume",
                new Vector3(centreX, geometry.GoalHeight * 0.5f, 0f),
                new Vector3(geometry.GoalDepth, geometry.GoalHeight, geometry.GoalHalfWidth * 2f),
                "Untagged", null);
            volume.isTrigger = true;
            volume.gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
            volume.gameObject.AddComponent<SoccerGoalSurface>().Configure(defendingTeam, true);
            void Surface(string name, Vector3 position, Vector3 size, Material visibleMaterial = null)
            {
                var collider = AddBox(parent, defendingTeam + "_Goal_" + name, position, size, tag, material,
                    visibleMaterial);
                collider.gameObject.AddComponent<SoccerGoalSurface>().Configure(defendingTeam, true);
            }
            Surface("Floor", new Vector3(centreX, -0.125f, 0f),
                new Vector3(geometry.GoalDepth, 0.25f, geometry.GoalHalfWidth * 2f), floorMaterial);
            Surface("Back", new Vector3(sign * (geometry.HalfLength + geometry.GoalDepth + 0.05f), geometry.GoalHeight * 0.5f, 0f),
                new Vector3(0.1f, geometry.GoalHeight, geometry.GoalHalfWidth * 2f));
            foreach (var lateral in new[] { -1f, 1f })
                Surface("Side_" + lateral, new Vector3(centreX, geometry.GoalHeight * 0.5f, lateral * (geometry.GoalHalfWidth + 0.05f)),
                    new Vector3(geometry.GoalDepth, geometry.GoalHeight, 0.1f));
            Surface("Roof", new Vector3(centreX, geometry.GoalHeight + 0.05f, 0f),
                new Vector3(geometry.GoalDepth, 0.1f, geometry.GoalHalfWidth * 2f));
        }

        static void CopyPresentation(Scene destination, SoccerEnvController environment, Renderer[] goals)
        {
            SceneManager.SetActiveScene(destination);

            var settingsObject = new GameObject("SoccerSettings");
            var settings = settingsObject.AddComponent<SoccerSettings>();
            settings.agentRunSpeed = SoccerSettings.DefaultAgentRunSpeed;
            settings.maximumPlanarSpeed = SoccerSettings.DefaultMaximumPlanarSpeed;
            settings.rotationSpeed = SoccerSettings.DefaultRotationSpeed;
            settings.humanAcceleration = SoccerSettings.DefaultHumanAcceleration;
            settings.humanDeceleration = SoccerSettings.DefaultHumanDeceleration;
            settings.redMaterial = AssetDatabase.LoadAssetAtPath<Material>(RedPlayerMaterialPath);
            settings.navyMaterial = AssetDatabase.LoadAssetAtPath<Material>(NavyPlayerMaterialPath);
            Require(settings.redMaterial != null && settings.navyMaterial != null,
                "Shared player materials are missing.");

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = SoccerPlayerCamera.DefaultOverviewFieldOfView;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 350f;
            cameraObject.transform.position = SoccerPlayerCamera.DefaultOverviewPosition;
            cameraObject.transform.LookAt(Vector3.zero);
            cameraObject.AddComponent<AudioListener>();
            var fader = cameraObject.AddComponent<SoccerGoalOcclusionFader>();
            fader.Configure(environment);
            fader.ConfigureGoalRenderers(new[] { goals[0] }, new[] { goals[1] });
            cameraObject.AddComponent<SoccerPlayerCamera>().Configure(environment);

            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            var hudTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudUxmlPath);
            Require(panelSettings != null && hudTree != null, "Shared Soccer HUD assets are missing.");
            var hudObject = new GameObject("Soccer HUD");
            hudObject.SetActive(false);
            var document = hudObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = hudTree;
            var hud = hudObject.AddComponent<SoccerHudController>();
            hud.Configure(environment);
            hud.ConfigureRewardPanelBottomRight(true);
            hudObject.SetActive(true);
        }

        [MenuItem("Tools/Soccer/Stadium/Validate all team Stadiums")]
        public static void ValidateBatch()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            var previousSetup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                ValidateScene(EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single));
            }
            finally
            {
                // A fresh batch process can have no loaded/active scene.
                if (previousSetup.Any(item => item.isLoaded && item.isActive && !string.IsNullOrEmpty(item.path)))
                    EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
                else if (!Application.isBatchMode)
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
            SoccerProjectBuilder.ValidateGeneratedAssets();
            Debug.Log("ALL TEAM STADIUM VALIDATION PASS");
        }

        public static void OpenForReview()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        public static void InspectDesignBatch()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var trees = root.GetComponentsInChildren<Transform>(true).Where(IsTreeObject).ToArray();
                Debug.Log($"STADIUM TREE INVENTORY objects={trees.Length}, prefabRoots={trees.Count(item => PrefabUtility.IsAnyPrefabInstanceRoot(item.gameObject))}");
                foreach (var group in trees.GroupBy(item => item.name.Split(' ')[0]))
                    Debug.Log($"STADIUM TREE TYPE {group.Key}: {group.Count()}");
                foreach (var terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    var data = terrain.terrainData;
                    Debug.Log($"STADIUM TERRAIN {AssetDatabase.GetAssetPath(data)} trees={data.treeInstanceCount} prototypes={data.treePrototypes.Length} details={data.detailPrototypes.Length}");
                    foreach (var prototype in data.treePrototypes)
                        Debug.Log("STADIUM TERRAIN TREE PREFAB " + AssetDatabase.GetAssetPath(prototype.prefab));
                    foreach (var prototype in data.detailPrototypes)
                        Debug.Log("STADIUM TERRAIN DETAIL " + AssetDatabase.GetAssetPath(prototype.prototype));
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void ValidateScene(Scene scene)
        {
            var all = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            Require(all.All(item => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(item.gameObject) == 0), "Missing script in stadium.");
            var environment = all.Select(item => item.GetComponent<SoccerEnvController>()).Single(item => item != null);
            var arena = environment.ArenaGeometry;
            Require(arena != null && Mathf.Abs(arena.HalfLength - 62f) < 0.001f, "Arena length must be 124.");
            Require(arena.HalfWidth > 40f && arena.HalfWidth < 44f, "Unexpected stadium width.");
            Require(environment.transform.lossyScale == Vector3.one, "Gameplay root must be unscaled.");
            Require(environment.AgentsList.Count == 8 && environment.GetComponentsInChildren<AgentSoccer>().Length == 8, "Expected eight players.");
            foreach (var agent in environment.GetComponentsInChildren<AgentSoccer>())
            {
                var sensors = agent.GetComponentsInChildren<RayPerceptionSensorComponent3D>();
                Require(sensors.Length == 2 && sensors.All(sensor => Mathf.Approximately(sensor.RayLength, 80f)), "Ray contract changed.");
                Require(agent.transform.lossyScale == Vector3.one, "Player scale changed.");
                Require(agent.GetComponent<BehaviorParameters>().BrainParameters.ActionSpec.BranchSizes.SequenceEqual(new[] { 3, 3, 3, 3 }), "Action contract changed.");
            }
            Require(Vector3.Distance(environment.ball.transform.lossyScale, Vector3.one * BallScale) < 0.000001f, "Ball must be 121% of legacy Base.");
            Require(Mathf.Approximately(environment.ControlledKickPower, PassPower), "Stadium pass power must be 2000.");
            Require(Mathf.Approximately(environment.StrongKickPower, ShotPower), "Stadium shot power must be 5000.");
            var ballCollider = environment.ball.GetComponent<SphereCollider>();
            var ballRadius = ballCollider.radius * environment.ball.transform.lossyScale.x;
            var ballController = environment.ball.GetComponent<SoccerBallController>();
            Require(ballController.EnforcesStadiumPlanarMotion
                && Mathf.Abs(ballController.LockedCenterHeight - ballRadius) < 0.001f
                && Mathf.Abs(environment.ball.transform.position.y - ballRadius) < 0.001f,
                "Stadium ball must touch the pitch and use planar rolling at its physical radius.");
            Require((environment.ball.GetComponent<Rigidbody>().constraints & RigidbodyConstraints.FreezePositionY) != 0,
                "Stadium ball must retain its vertical position constraint.");
            Require(arena.GoalHalfWidth > 10f && arena.GoalHalfWidth < 10.4f
                && Mathf.Approximately(arena.GoalModelWidthMultiplier, GoalWidthMultiplier), "Expected double-width Demo goals.");
            Require(Mathf.Abs(arena.CornerRadius - CornerRadius) < 0.001f, "Unexpected rounded-corner radius.");
            var walls = environment.GetComponentsInChildren<BoxCollider>().Where(collider => collider.CompareTag("wall")).ToArray();
            Require(walls.Length == 18, "Expected six straight walls and twelve corner panels.");
            Require(walls.All(wall => Mathf.Abs(wall.bounds.size.y - WallHeight) < 0.001f && !wall.isTrigger), "Wall heights/physics mismatch.");
            Require(walls.All(wall => Mathf.Abs((wall.name.StartsWith("Wall_End_", StringComparison.Ordinal)
                ? wall.transform.localScale.x : wall.transform.localScale.z) - WallThickness) < 0.001f),
                "All straight and corner walls must use the same 0.12m thickness.");
            Require(walls.All(wall => AssetDatabase.GetAssetPath(wall.GetComponent<MeshRenderer>().sharedMaterial) == WallMaterialPath
                && Mathf.Abs(wall.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_BaseColor").a - WallAlpha) < 0.001f),
                "All walls must use the 20% more transparent Stadium wall material.");
            var corners = walls.Where(wall => wall.name.StartsWith("Wall_Corner_", StringComparison.Ordinal)).ToArray();
            Require(corners.Length == 12 && corners.All(wall => Mathf.Abs(wall.transform.localScale.x - (CornerChordLength + CornerJointOverlap)) < 0.001f
                && Mathf.Abs(wall.transform.localScale.z - CornerPanelThickness) < 0.001f), "Corner panels must be thin, approximately 1.5m plates.");
            Require(!all.Any(IsTreeObject), "A tree instance remains in the Stadium scene.");
            Require(!all.Any(item => IsExteriorOptimizationPrefabInstance(item)
                    || IsRemovedExteriorPrefabName(item.name)),
                "An approved road, landscaping, or exterior city-prop instance remains in the Stadium scene.");
            var demoGoals = environment.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.name.StartsWith("SM_S_Gate", StringComparison.Ordinal)).ToArray();
            Require(demoGoals.Length == 2, "Expected two Demo goal renderers.");
            foreach (var team in new[] { Team.Red, Team.Navy })
            {
                var goal = demoGoals.Single(renderer => renderer.CompareTag(team == Team.Red ? "redGoal" : "navyGoal"));
                var material = goal.sharedMaterial;
                Require(AssetDatabase.GetAssetPath(material) == (team == Team.Red ? RedGoalMaterialPath : NavyGoalMaterialPath),
                    "Goal must use its own Stadium team material.");
                Require(Vector4.Distance(material.GetColor("_BaseColor"), team == Team.Red ? RedGoalColor : NavyGoalColor) < 0.001f
                    && material.GetTexture("_BaseMap") == null && material.GetFloat("_Surface") == 0f,
                    "Goal must be opaque in its bright team color without the yellow texture atlas.");
            }
            Require(environment.GetComponentsInChildren<SoccerGoalSurface>().Count(surface =>
                surface.Scores && surface.GetComponent<Collider>().isTrigger
                && surface.gameObject.layer == LayerMask.NameToLayer("Ignore Raycast")) == 2,
                "Expected two sensor-excluded scoring volumes.");
            var goalFloors = environment.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.name.EndsWith("_Goal_Floor", StringComparison.Ordinal)).ToArray();
            Require(goalFloors.Length == 2 && goalFloors.All(floor =>
                    AssetDatabase.GetAssetPath(floor.sharedMaterial) == GoalFloorMaterialPath
                    && Vector4.Distance(floor.sharedMaterial.GetColor("_BaseColor"), GoalFloorColor) < 0.001f
                    && floor.GetComponent<BoxCollider>() != null
                    && floor.GetComponent<SoccerGoalSurface>() != null
                    && Mathf.Abs(floor.bounds.max.y) < 0.001f),
                "Each goal must reuse its collider as a light-gray floor resting on pitch height.");
            var hud = all.Select(item => item.GetComponent<SoccerHudController>()).Single(item => item != null);
            Require(hud.Environment == environment && hud.GetComponent<UIDocument>().visualTreeAsset != null
                && hud.RewardPanelBottomRight, "HUD binding or Stadium bottom-right reward-panel placement missing.");
            Require(all.Select(item => item.GetComponent<Camera>()).Count(camera => camera != null && camera.enabled && camera.CompareTag("MainCamera")) == 1, "Exactly one active MainCamera required.");
            Require(all.Select(item => item.GetComponent<AudioListener>()).Count(listener => listener != null && listener.enabled) == 1, "Exactly one AudioListener required.");
            var terrain = environment.GetComponentInChildren<Terrain>();
            Require(terrain.transform.lossyScale == Vector3.one && AssetDatabase.GetAssetPath(terrain.terrainData) == TerrainPath, "Terrain must use a private, unscaled data copy.");
            Require(terrain.GetComponent<TerrainCollider>().terrainData == terrain.terrainData, "Terrain collider data mismatch.");
            Require(all.Select(item => item.GetComponent<Terrain>()).Where(item => item != null)
                .All(item => item.terrainData.treeInstanceCount == 0), "Terrain tree instances remain in the Stadium.");
        }

        static void EnsureDirectory(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureDirectory(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        [Serializable] sealed class RootRecord
        {
            public string name;
            public Vector3 sourcePosition;
            public Vector3 sourceScale;
            public Vector3 finalPosition;
            public Vector3 finalScale;
        }

        [Serializable] sealed class BuildReport
        {
            public Bounds sourceFieldBounds;
            public Vector3 sourceFieldCentre;
            public float additionalUniformScale;
            public Bounds finalFieldBounds;
            public float goalOpeningWidth;
            public float goalHeight;
            public float goalDepth;
            public float ballDiameter;
            public RootRecord[] roots;
        }

        [Serializable] sealed class TuningReport
        {
            public float fieldLength, fieldWidth, ballScale, ballDiameter, ballGroundClearance, ballLockedCenterHeight;
            public float wallHeight, wallThickness, wallAlpha;
            public float goalOpeningWidth, goalHeight, goalDepth, goalModelWidthMultiplier, passPower, shotPower;
            public float cornerRadius, cornerPanelLength, cornerPanelThickness, removedCornerArea;
            public int cornerPanels, treeObjects, terrainTrees, goalFloorRenderers;
            public bool rewardPanelBottomRight, stadiumPlanarRolling;
            public Color redGoalColor, navyGoalColor, goalFloorColor;
            public Vector3[] goalMeshSizes;
        }
    }
}
