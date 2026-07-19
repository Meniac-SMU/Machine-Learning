using System;
using System.Collections.Generic;
using System.IO;
using MachineLearning.Escape;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Areas;
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

namespace MachineLearning.Escape.Editor
{
    public static class EscapeProjectBuilder
    {
        const string Root = "Assets/Escape";
        const string Materials = Root + "/Materials";
        const string EnvironmentPrefabs = Root + "/Prefabs/Environment";
        const string AgentPrefabs = Root + "/Prefabs/Agents";
        const string ScenePath = Root + "/Scenes/EscapePrototype.unity";
        const string PanelSettingsPath = Root + "/UI/EscapePanelSettings.asset";
        const string UxmlPath = Root + "/UI/EscapeHud.uxml";
        const string ThemePath = Root + "/UI/EscapeRuntimeTheme.tss";
        const string WindowsBuildPath = "Builds/Escape/EscapeTraining.exe";

        [MenuItem("Tools/Escape/Build Prototype")]
        public static void BuildAll()
        {
            EnsureDirectories();
            EnsureTags("Wall", "Enemy", "Building", "Gate", "Button");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var materialSet = CreateMaterials();
            var buildingPrefab = CreateBuildingPrefab(materialSet.Building);
            var buttonPrefab = CreateButtonPrefab(materialSet.ButtonInactive, materialSet.ButtonActive);
            var gatePrefab = CreateGatePrefab(materialSet.GateClosed, materialSet.GateActive);
            var playerPrefab = CreateAgentPrefab(true, materialSet.Player, materialSet.PlayerHit, materialSet.PlayerFace);
            var enemyPrefab = CreateAgentPrefab(false, materialSet.Enemy, materialSet.PlayerHit, materialSet.EnemyFace);
            var environmentPrefab = CreateEnvironmentPrefab(
                materialSet,
                buildingPrefab,
                buttonPrefab,
                gatePrefab,
                playerPrefab,
                enemyPrefab);

            var panelSettings = CreatePanelSettings();
            CreateScene(environmentPrefab, panelSettings);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateGeneratedAssets();
            Debug.Log("Escape prototype assets generated successfully.");
        }

        public static void BuildAllBatch()
        {
            BuildAll();
        }

        [MenuItem("Tools/Escape/Validate Prototype")]
        public static void ValidateGeneratedAssets()
        {
            var environmentPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnvironmentPrefabs + "/EscapeEnvironment.prefab");
            if (environmentPrefab == null)
            {
                throw new BuildFailedException("EscapeEnvironment.prefab is missing.");
            }

            var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(environmentPrefab));
            try
            {
                var environment = root.GetComponent<EscapeEnvironmentController>();
                Require(environment != null, "Environment controller is missing.");
                Require(environment.BuildingCount == EscapeMapLayout.BuildingCount, $"Expected 25 buildings, found {environment.BuildingCount}.");
                Require(environment.SpawnPointCount == EscapeMapLayout.SpawnPointCount, $"Expected 36 spawn points, found {environment.SpawnPointCount}.");
                Require(environment.GateAnchorCount == EscapeMapLayout.GateAnchorCount, $"Expected 4 gate anchors, found {environment.GateAnchorCount}.");
                Require(environment.ActiveButtonCount == EscapeMapLayout.ButtonCount, $"Expected 5 buttons, found {environment.ActiveButtonCount}.");

                foreach (var building in root.GetComponentsInChildren<EscapeBuilding>(true))
                {
                    Require(building.SocketCount == 4, $"{building.name} does not have four button sockets.");
                }

                var player = root.GetComponentInChildren<EscapePlayerAgent>(true);
                Require(player != null, "Player agent is missing.");
                Require(root.GetComponentsInChildren<Agent>(true).Length == 4, "Environment must contain exactly four Agent components.");
                ValidateAgent(player.gameObject, "EscapePlayer", 0);
                var enemies = root.GetComponentsInChildren<EscapeEnemyAgent>(true);
                Require(enemies.Length == 3, $"Expected 3 enemies, found {enemies.Length}.");
                foreach (var enemy in enemies)
                {
                    ValidateAgent(enemy.gameObject, "EscapeEnemy", 1);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Require(File.Exists(Path.GetFullPath(ScenePath)), "EscapePrototype scene is missing.");
            Debug.Log("Escape prototype validation passed: 25 buildings, 36 spawns, 5 buttons, 4 gate anchors, 1 player, 3 enemies.");
        }

        public static void ValidateBatch()
        {
            ValidateGeneratedAssets();
        }

        [MenuItem("Tools/Escape/Build Windows Training Player")]
        public static void BuildWindowsPlayer()
        {
            BuildAll();
            Directory.CreateDirectory(Path.GetDirectoryName(WindowsBuildPath));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = WindowsBuildPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException($"Escape Windows build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            }

            Debug.Log($"Escape Windows training build succeeded: {WindowsBuildPath} ({report.summary.totalSize} bytes).");
        }

        public static void BuildWindowsBatch()
        {
            BuildWindowsPlayer();
        }

        static void ValidateAgent(GameObject agentObject, string behaviorName, int teamId)
        {
            var behavior = agentObject.GetComponent<BehaviorParameters>();
            var requester = agentObject.GetComponent<DecisionRequester>();
            var ray = agentObject.GetComponent<RayPerceptionSensorComponent3D>();
            Require(behavior != null, $"{agentObject.name} BehaviorParameters missing.");
            Require(behavior.BehaviorName == behaviorName, $"{agentObject.name} behavior name mismatch.");
            Require(behavior.TeamId == teamId, $"{agentObject.name} team id mismatch.");
            Require(behavior.BrainParameters.VectorObservationSize == 7, $"{agentObject.name} vector observation size mismatch.");
            Require(behavior.BrainParameters.ActionSpec.BranchSizes.Length == 2, $"{agentObject.name} action branch count mismatch.");
            Require(behavior.BrainParameters.ActionSpec.BranchSizes[0] == 3 && behavior.BrainParameters.ActionSpec.BranchSizes[1] == 3, $"{agentObject.name} action branch sizes mismatch.");
            Require(requester != null && requester.DecisionPeriod == 5 && requester.TakeActionsBetweenDecisions, $"{agentObject.name} DecisionRequester mismatch.");
            Require(ray != null && ray.RaysPerDirection == 12 && Mathf.Approximately(ray.MaxRayDegrees, 180f), $"{agentObject.name} Ray Sensor geometry mismatch.");
            Require(ray.DetectableTags.Count == 6, $"{agentObject.name} must detect six tags.");
            Require(ray.UseBatchedRaycasts, $"{agentObject.name} must use batched raycasts.");
            var facingMarker = agentObject.transform.Find("FacingMarker");
            Require(facingMarker != null && facingMarker.GetComponent<Renderer>() != null, $"{agentObject.name} facing marker missing.");
        }

        static MaterialSet CreateMaterials()
        {
            return new MaterialSet
            {
                Floor = CreateMaterial("FloorBrown", new Color(0.34f, 0.23f, 0.12f), false),
                FloorPlayerWin = CreateMaterial("FloorPlayerWin", new Color(0.35f, 1f, 0.35f), true),
                FloorEnemyWin = CreateMaterial("FloorEnemyWin", new Color(1f, 0.18f, 0.12f), true),
                Building = CreateMaterial("BuildingGray", new Color(0.38f, 0.41f, 0.45f), false),
                Wall = CreateMaterial("WallDark", new Color(0.18f, 0.20f, 0.23f), false),
                Player = CreateMaterial("PlayerBlue", new Color(0.10f, 0.35f, 1f), false),
                PlayerHit = CreateMaterial("PlayerHitRed", new Color(1f, 0.05f, 0.04f), true),
                PlayerFace = CreateMaterial("PlayerFaceCyan", new Color(0.15f, 1f, 1f), true),
                Enemy = CreateMaterial("EnemyOrange", new Color(1f, 0.34f, 0.06f), false),
                EnemyFace = CreateMaterial("EnemyFaceYellow", new Color(1f, 0.92f, 0.08f), true),
                ButtonInactive = CreateMaterial("ButtonRed", new Color(0.8f, 0.04f, 0.03f), false),
                ButtonActive = CreateMaterial("ButtonLime", new Color(0.45f, 1f, 0.18f), true),
                GateClosed = CreateMaterial("GateClosed", new Color(0.08f, 0.22f, 0.25f), false),
                GateActive = CreateMaterial("GateActive", new Color(0.12f, 1f, 0.72f), true)
            };
        }

        static Material CreateMaterial(string name, Color color, bool emission)
        {
            var path = $"{Materials}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emission ? color * 2.5f : Color.black);
                if (emission)
                {
                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else
                {
                    material.DisableKeyword("_EMISSION");
                }
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        static GameObject CreateBuildingPrefab(Material material)
        {
            var root = new GameObject("EscapeBuilding") { tag = "Building" };
            CreateBox(root.transform, "BuildingVisual", Vector3.zero, new Vector3(EscapeMapLayout.BuildingSize, EscapeMapLayout.BuildingHeight, EscapeMapLayout.BuildingSize), material, "Building");
            var building = root.AddComponent<EscapeBuilding>();
            var sockets = new Transform[4];
            var faceOffset = EscapeMapLayout.BuildingSize * 0.5f + 0.1f;
            var socketHeight = -EscapeMapLayout.BuildingHeight * 0.375f;
            sockets[0] = CreateSocket(root.transform, "ButtonSocket_North", new Vector3(0f, socketHeight, faceOffset), Quaternion.Euler(0f, 0f, 0f));
            sockets[1] = CreateSocket(root.transform, "ButtonSocket_East", new Vector3(faceOffset, socketHeight, 0f), Quaternion.Euler(0f, 90f, 0f));
            sockets[2] = CreateSocket(root.transform, "ButtonSocket_South", new Vector3(0f, socketHeight, -faceOffset), Quaternion.Euler(0f, 180f, 0f));
            sockets[3] = CreateSocket(root.transform, "ButtonSocket_West", new Vector3(-faceOffset, socketHeight, 0f), Quaternion.Euler(0f, -90f, 0f));
            building.Configure(sockets);
            return SavePrefabAndDestroy(root, EnvironmentPrefabs + "/EscapeBuilding.prefab");
        }

        static Transform CreateSocket(Transform parent, string name, Vector3 localPosition, Quaternion localRotation)
        {
            var socket = new GameObject(name).transform;
            socket.SetParent(parent, false);
            socket.localPosition = localPosition;
            socket.localRotation = localRotation;
            return socket;
        }

        static GameObject CreateButtonPrefab(Material inactive, Material active)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "EscapeButton";
            root.tag = "Button";
            root.transform.localScale = new Vector3(1.2f, 1.2f, 0.35f);
            var collider = root.GetComponent<BoxCollider>();
            collider.isTrigger = true;
            var renderer = root.GetComponent<Renderer>();
            renderer.sharedMaterial = inactive;
            root.AddComponent<EscapeButton>().Configure(renderer, inactive, active);
            return SavePrefabAndDestroy(root, EnvironmentPrefabs + "/EscapeButton.prefab");
        }

        static GameObject CreateGatePrefab(Material closed, Material active)
        {
            var root = new GameObject("EscapeGate");
            var door = CreateBox(root.transform, "GateDoor", new Vector3(0f, 2f, 0f), new Vector3(6f, 4f, 1f), closed, "Gate");
            var triggerObject = new GameObject("GateExitTrigger") { tag = "Gate" };
            triggerObject.transform.SetParent(root.transform, false);
            triggerObject.transform.localPosition = new Vector3(0f, 1.5f, 1.8f);
            var trigger = triggerObject.AddComponent<BoxCollider>();
            trigger.size = new Vector3(6f, 3f, 3f);
            trigger.isTrigger = true;
            triggerObject.AddComponent<EscapeGateExitTrigger>();
            var gate = root.AddComponent<EscapeGate>();
            gate.Configure(door.GetComponent<BoxCollider>(), trigger, door.GetComponent<Renderer>(), closed, active);
            return SavePrefabAndDestroy(root, EnvironmentPrefabs + "/EscapeGate.prefab");
        }

        static GameObject CreateAgentPrefab(bool isPlayer, Material material, Material hitMaterial, Material faceMaterial)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            root.name = isPlayer ? "EscapePlayer" : "EscapeEnemy";
            root.tag = isPlayer ? "Player" : "Enemy";
            var renderer = root.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            var facingMarker = CreateBox(root.transform, "FacingMarker", new Vector3(0f, 0.88f, 0.52f), new Vector3(0.5f, 0.5f, 0.25f), faceMaterial, "Untagged");
            UnityEngine.Object.DestroyImmediate(facingMarker.GetComponent<BoxCollider>());
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = 1f;
            rigidbody.useGravity = true;
            rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            var motor = root.AddComponent<EscapeAgentMotor>();
            motor.Configure(isPlayer ? 5f : 4.05f, 160f);
            Agent agent;
            if (isPlayer)
            {
                var health = root.AddComponent<EscapePlayerHealth>();
                health.Configure(renderer, material, hitMaterial);
                agent = root.AddComponent<EscapePlayerAgent>();
            }
            else
            {
                agent = root.AddComponent<EscapeEnemyAgent>();
            }

            agent.MaxStep = 0;
            var behavior = root.GetComponent<BehaviorParameters>();
            behavior.BehaviorName = isPlayer ? "EscapePlayer" : "EscapeEnemy";
            behavior.TeamId = isPlayer ? 0 : 1;
            behavior.BrainParameters.VectorObservationSize = 7;
            behavior.BrainParameters.NumStackedVectorObservations = 1;
            behavior.BrainParameters.ActionSpec = ActionSpec.MakeDiscrete(3, 3);
            behavior.BehaviorType = BehaviorType.HeuristicOnly;

            var requester = root.AddComponent<DecisionRequester>();
            requester.DecisionPeriod = 5;
            requester.DecisionStep = 0;
            requester.TakeActionsBetweenDecisions = true;

            var ray = root.AddComponent<RayPerceptionSensorComponent3D>();
            ray.SensorName = isPlayer ? "PlayerRaySensor" : "EnemyRaySensor";
            ray.DetectableTags = new List<string> { "Player", "Enemy", "Wall", "Building", "Gate", "Button" };
            ray.RaysPerDirection = 12;
            ray.MaxRayDegrees = 180f;
            ray.SphereCastRadius = 0f;
            ray.RayLength = 24f;
            ray.ObservationStacks = 1;
            ray.StartVerticalOffset = 0f;
            ray.EndVerticalOffset = 0f;
            ray.UseBatchedRaycasts = true;

            var path = AgentPrefabs + (isPlayer ? "/EscapePlayer.prefab" : "/EscapeEnemy.prefab");
            return SavePrefabAndDestroy(root, path);
        }

        static GameObject CreateEnvironmentPrefab(
            MaterialSet materials,
            GameObject buildingPrefab,
            GameObject buttonPrefab,
            GameObject gatePrefab,
            GameObject playerPrefab,
            GameObject enemyPrefab)
        {
            var root = new GameObject("EscapeEnvironment");
            var controller = root.AddComponent<EscapeEnvironmentController>();

            var floor = CreateBox(root.transform, "Floor", new Vector3(0f, -0.1f, 0f), new Vector3(96f, 0.2f, 96f), materials.Floor, "Untagged");
            var buildingsRoot = new GameObject("Buildings").transform;
            buildingsRoot.SetParent(root.transform, false);
            var buildings = new EscapeBuilding[EscapeMapLayout.BuildingCount];
            for (var i = 0; i < buildings.Length; i++)
            {
                var building = InstantiatePrefab(buildingPrefab, buildingsRoot);
                building.name = $"Building_{i:00}";
                building.transform.localPosition = EscapeMapLayout.GetBuildingPosition(i);
                buildings[i] = building.GetComponent<EscapeBuilding>();
            }

            var wallsRoot = new GameObject("BoundaryWalls").transform;
            wallsRoot.SetParent(root.transform, false);
            CreateBoundaryWallSegments(wallsRoot, materials.Wall);
            var gateBlockers = CreateGateBlockers(wallsRoot, materials.Wall);
            var gateAnchors = CreateGateAnchors(root.transform);

            var spawnRoot = new GameObject("SpawnPoints").transform;
            spawnRoot.SetParent(root.transform, false);
            var spawnPoints = new Transform[EscapeMapLayout.SpawnPointCount];
            for (var i = 0; i < spawnPoints.Length; i++)
            {
                var point = new GameObject($"SpawnPoint_{i:00}").transform;
                point.SetParent(spawnRoot, false);
                point.localPosition = EscapeMapLayout.GetSpawnPosition(i);
                spawnPoints[i] = point;
            }

            var interactables = new GameObject("Interactables").transform;
            interactables.SetParent(root.transform, false);
            var buttons = new EscapeButton[EscapeMapLayout.ButtonCount];
            for (var i = 0; i < buttons.Length; i++)
            {
                var button = InstantiatePrefab(buttonPrefab, interactables);
                button.name = $"Button_{i:00}";
                buttons[i] = button.GetComponent<EscapeButton>();
            }

            var gateObject = InstantiatePrefab(gatePrefab, interactables);
            var gate = gateObject.GetComponent<EscapeGate>();

            var agentsRoot = new GameObject("Agents").transform;
            agentsRoot.SetParent(root.transform, false);
            var playerObject = InstantiatePrefab(playerPrefab, agentsRoot);
            var player = playerObject.GetComponent<EscapePlayerAgent>();
            var enemies = new EscapeEnemyAgent[3];
            for (var i = 0; i < enemies.Length; i++)
            {
                var enemyObject = InstantiatePrefab(enemyPrefab, agentsRoot);
                enemyObject.name = $"EscapeEnemy_{i + 1}";
                enemies[i] = enemyObject.GetComponent<EscapeEnemyAgent>();
            }

            controller.Configure(
                floor.GetComponent<Renderer>(),
                materials.Floor,
                materials.FloorPlayerWin,
                materials.FloorEnemyWin,
                buildings,
                spawnPoints,
                gateAnchors,
                gateBlockers,
                buttons,
                gate,
                player,
                enemies);

            return SavePrefabAndDestroy(root, EnvironmentPrefabs + "/EscapeEnvironment.prefab");
        }

        static void CreateBoundaryWallSegments(Transform parent, Material material)
        {
            const float halfSegment = 25.5f;
            var horizontalScale = new Vector3(45f, 4f, 1f);
            var verticalScale = new Vector3(1f, 4f, 45f);
            CreateBox(parent, "NorthWall_Left", new Vector3(-halfSegment, 2f, 48f), horizontalScale, material, "Wall");
            CreateBox(parent, "NorthWall_Right", new Vector3(halfSegment, 2f, 48f), horizontalScale, material, "Wall");
            CreateBox(parent, "SouthWall_Left", new Vector3(-halfSegment, 2f, -48f), horizontalScale, material, "Wall");
            CreateBox(parent, "SouthWall_Right", new Vector3(halfSegment, 2f, -48f), horizontalScale, material, "Wall");
            CreateBox(parent, "EastWall_North", new Vector3(48f, 2f, halfSegment), verticalScale, material, "Wall");
            CreateBox(parent, "EastWall_South", new Vector3(48f, 2f, -halfSegment), verticalScale, material, "Wall");
            CreateBox(parent, "WestWall_North", new Vector3(-48f, 2f, halfSegment), verticalScale, material, "Wall");
            CreateBox(parent, "WestWall_South", new Vector3(-48f, 2f, -halfSegment), verticalScale, material, "Wall");
        }

        static GameObject[] CreateGateBlockers(Transform parent, Material material)
        {
            return new[]
            {
                CreateBox(parent, "GateBlocker_North", new Vector3(0f, 2f, 48f), new Vector3(6f, 4f, 1f), material, "Wall"),
                CreateBox(parent, "GateBlocker_East", new Vector3(48f, 2f, 0f), new Vector3(1f, 4f, 6f), material, "Wall"),
                CreateBox(parent, "GateBlocker_South", new Vector3(0f, 2f, -48f), new Vector3(6f, 4f, 1f), material, "Wall"),
                CreateBox(parent, "GateBlocker_West", new Vector3(-48f, 2f, 0f), new Vector3(1f, 4f, 6f), material, "Wall")
            };
        }

        static Transform[] CreateGateAnchors(Transform parent)
        {
            var root = new GameObject("GateAnchors").transform;
            root.SetParent(parent, false);
            return new[]
            {
                CreateAnchor(root, "GateAnchor_North", new Vector3(0f, 0f, 48f), 0f),
                CreateAnchor(root, "GateAnchor_East", new Vector3(48f, 0f, 0f), 90f),
                CreateAnchor(root, "GateAnchor_South", new Vector3(0f, 0f, -48f), 180f),
                CreateAnchor(root, "GateAnchor_West", new Vector3(-48f, 0f, 0f), -90f)
            };
        }

        static Transform CreateAnchor(Transform parent, string name, Vector3 localPosition, float yaw)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.localPosition = localPosition;
            anchor.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return anchor;
        }

        static PanelSettings CreatePanelSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, PanelSettingsPath);
            }

            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.match = 0.5f;
            settings.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        static void CreateScene(GameObject environmentPrefab, PanelSettings panelSettings)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var environmentObject = (GameObject)PrefabUtility.InstantiatePrefab(environmentPrefab, scene);
            environmentObject.transform.position = Vector3.zero;
            var environment = environmentObject.GetComponent<EscapeEnvironmentController>();

            var replicatorObject = new GameObject("TrainingAreaReplicator");
            var replicator = replicatorObject.AddComponent<TrainingAreaReplicator>();
            replicator.baseArea = environmentObject;
            replicator.numAreas = 1;
            replicator.separation = 120f;
            replicator.buildOnly = true;

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            var camera = cameraObject.AddComponent<UnityEngine.Camera>();
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 300f;
            cameraObject.AddComponent<AudioListener>();
            var cameraController = cameraObject.AddComponent<EscapeThirdPersonCamera>();
            cameraController.Configure(environment.Player);

            var hudObject = new GameObject("Escape HUD");
            var document = hudObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            hudObject.AddComponent<EscapeHudController>().Configure(environment);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static GameObject CreateBox(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, string tag)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.tag = tag;
            box.transform.SetParent(parent, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = localScale;
            box.GetComponent<Renderer>().sharedMaterial = material;
            return box;
        }

        static GameObject InstantiatePrefab(GameObject prefab, Transform parent)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false);
            return instance;
        }

        static GameObject SavePrefabAndDestroy(GameObject root, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        static void EnsureDirectories()
        {
            var directories = new[] { Materials, EnvironmentPrefabs, AgentPrefabs, Root + "/Scenes", Root + "/UI" };
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
                for (var i = 0; i < tags.arraySize; i++)
                {
                    if (tags.GetArrayElementAtIndex(i).stringValue == requiredTag)
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

        static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new BuildFailedException(message);
            }
        }

        sealed class MaterialSet
        {
            public Material Floor;
            public Material FloorPlayerWin;
            public Material FloorEnemyWin;
            public Material Building;
            public Material Wall;
            public Material Player;
            public Material PlayerHit;
            public Material PlayerFace;
            public Material Enemy;
            public Material EnemyFace;
            public Material ButtonInactive;
            public Material ButtonActive;
            public Material GateClosed;
            public Material GateActive;
        }
    }
}
