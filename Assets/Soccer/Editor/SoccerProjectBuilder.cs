using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.MLAgents;
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
    public static class SoccerProjectBuilder
    {
        const string Root = "Assets/Soccer";
        const string SourcePrefabPath = Root + "/Prefabs/SoccerFieldTwos.prefab";
        const string OutputPrefabPath = Root + "/Prefabs/SoccerField4v4.prefab";
        const string ScenePath = Root + "/Scenes/Soccer4v4.unity";
        const string PanelSettingsPath = Root + "/UI/SoccerPanelSettings.asset";
        const string UxmlPath = Root + "/UI/SoccerHud.uxml";
        const string ThemePath = Root + "/UI/SoccerRuntimeTheme.tss";
        const string HumanMarkerMaterialPath = Root + "/Materials/HumanMarker.mat";
        const string WindowsBuildPath = "Builds/Soccer/Soccer4v4.exe";
        const string KickPlateLayerPrefix = "SoccerPlate";
        const int KickPlateLayerCount = 8;
        const float FieldScaleMultiplier = 4f;
        const float SourceFieldScale = 0.01f;
        const float SensorRange = 80f;

        static readonly Vector3[] BlueSpawns =
        {
            new(-22f, 0.5f, 0f),
            new(-34f, 0.5f, -10f),
            new(-34f, 0.5f, 10f),
            new(-54f, 0.5f, 0f)
        };

        static readonly Vector3[] PurpleSpawns =
        {
            new(22f, 0.5f, 0f),
            new(34f, 0.5f, 10f),
            new(34f, 0.5f, -10f),
            new(54f, 0.5f, 0f)
        };

        [MenuItem("Tools/Soccer/Build 4v4 Prototype")]
        public static void BuildAll()
        {
            EnsureDirectories();
            EnsureTags("ball", "blueGoal", "purpleGoal", "wall", "blueAgent", "purpleAgent");
            var kickPlateLayers = EnsureKickPlateLayers();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            UpgradeCopiedMaterialsForUrp();
            var visualMaterials = CreateVisualMaterialSet();
            var fieldPrefab = Create4v4Prefab(visualMaterials, kickPlateLayers);
            var panelSettings = CreatePanelSettings();
            CreateScene(fieldPrefab, panelSettings);
            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateGeneratedAssets();
            Debug.Log("Soccer 4v4 prototype assets generated successfully.");
        }

        public static void BuildAllBatch()
        {
            BuildAll();
        }

        [MenuItem("Tools/Soccer/Validate 4v4 Prototype")]
        public static void ValidateGeneratedAssets()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OutputPrefabPath);
            Require(prefab != null, "SoccerField4v4.prefab is missing.");

            var root = PrefabUtility.LoadPrefabContents(OutputPrefabPath);
            try
            {
                var controller = root.GetComponent<SoccerEnvController>();
                Require(controller != null, "SoccerEnvController is missing.");
                Require(Mathf.Approximately(controller.matchDurationSeconds, 180f), "Match duration must be 180 seconds.");
                Require(Mathf.Approximately(controller.goalResetDelaySeconds, 3f), "Goal reset delay must be 3 seconds.");
                Require(controller.ball != null, "Ball reference is missing.");

                var field = root.transform.Find("Field");
                Require(field != null, "Copied ML-Agents Field hierarchy is missing.");
                Require(Mathf.Approximately(field.localScale.x, SourceFieldScale * FieldScaleMultiplier)
                    && Mathf.Approximately(field.localScale.z, SourceFieldScale * FieldScaleMultiplier),
                    "Field X/Z dimensions must be four times the source prefab.");

                var agents = root.GetComponentsInChildren<AgentSoccer>(true);
                Require(agents.Length == 8, $"Expected 8 players, found {agents.Length}.");
                Require(agents.Count(agent => agent.Team == Team.Blue) == 4, "Blue team must have four players.");
                Require(agents.Count(agent => agent.Team == Team.Purple) == 4, "Purple team must have four players.");
                Require(agents.Count(agent => agent.HumanControllable) == 1, "Exactly one player must be human-controllable.");
                Require(agents.Single(agent => agent.HumanControllable).Team == Team.Blue, "Human player must belong to Blue.");
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
                    Require(behavior != null && behavior.BehaviorName == "Soccer4v4", $"{agent.name} behavior mismatch.");
                    Require(behavior.TeamId == (int)agent.Team, $"{agent.name} team id mismatch.");
                    Require(behavior.BrainParameters.ActionSpec.BranchSizes.SequenceEqual(new[] { 3, 3, 3 }),
                        $"{agent.name} must retain the source [3,3,3] action model.");
                    Require(requester != null && requester.DecisionPeriod == 5, $"{agent.name} DecisionRequester mismatch.");
                    var kickPlate = agent.GetComponentInChildren<SoccerKickPlate>(true);
                    Require(kickPlate != null && kickPlate.Owner == agent, $"{agent.name} kick plate owner mismatch.");
                    var plateParts = kickPlate.GetComponentsInChildren<Collider>(true);
                    Require(plateParts.Length == 3, $"{agent.name} kick plate must contain a center and two retaining wings.");
                    Require(plateParts.All(part => part.gameObject.layer == kickPlate.gameObject.layer),
                        $"{agent.name} kick plate parts must share their owner-filter layer.");
                    Require(plateParts.All(part => part.CompareTag(agent.Team == Team.Blue ? "blueAgent" : "purpleAgent")),
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
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Require(File.Exists(Path.GetFullPath(ScenePath)), "Soccer4v4 scene is missing.");
            Require(AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath) != null, "Soccer HUD UXML is missing.");
            Debug.Log("Soccer validation passed: 4x field, 4v4 teams, 180s match, 3s deterministic reset, Human/AI HUD.");
        }

        public static void ValidateBatch()
        {
            ValidateGeneratedAssets();
        }

        [MenuItem("Tools/Soccer/Build Windows Player")]
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
                throw new BuildFailedException($"Soccer Windows build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
            }

            Debug.Log($"Soccer Windows build succeeded: {WindowsBuildPath} ({report.summary.totalSize} bytes).");
        }

        public static void BuildWindowsBatch()
        {
            BuildWindowsPlayer();
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
                var blueAgents = existingAgents.Where(IsBlue).ToList();
                var purpleAgents = existingAgents.Where(agent => !IsBlue(agent)).ToList();
                Require(blueAgents.Count == 2 && purpleAgents.Count == 2, "Source prefab must contain two agents per team.");
                ExpandTeam(blueAgents, 4);
                ExpandTeam(purpleAgents, 4);

                var humanMarkerMaterial = CreateHumanMarkerMaterial();
                ConfigureTeam(blueAgents, Team.Blue, BlueSpawns, 90f, humanMarkerMaterial,
                    visualMaterials.BlueKickPlate, kickPlateLayers.Take(4).ToArray());
                ConfigureTeam(purpleAgents, Team.Purple, PurpleSpawns, -90f, null,
                    visualMaterials.PurpleKickPlate, kickPlateLayers.Skip(4).Take(4).ToArray());
                RemoveHeadbands(root);

                var controller = root.GetComponent<SoccerEnvController>();
                Require(controller != null, "Source environment controller is missing.");
                controller.matchDurationSeconds = 180f;
                controller.goalResetDelaySeconds = 3f;
                controller.AgentsList = blueAgents.Concat(purpleAgents)
                    .Select(agent => new SoccerEnvController.PlayerInfo { Agent = agent })
                    .ToList();

                var ballController = root.GetComponentInChildren<SoccerBallController>(true);
                Require(ballController != null, "Source ball controller is missing.");
                ballController.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                ballController.Configure(root);
                controller.ball = ballController.gameObject;
                RepairVisualMaterials(root, visualMaterials);

                // The upstream prefab still serializes a legacy demonstration recorder
                // that no longer exists in ML-Agents 4.0.3. It is unrelated to inference
                // or training, and Unity refuses to save newly cloned players until the
                // stale component is removed from the copied prefab contents.
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

        static bool IsBlue(AgentSoccer agent)
        {
            return agent.GetComponent<BehaviorParameters>().TeamId == (int)Team.Blue;
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
                    : index == 3 ? AgentSoccer.Position.Goalie : AgentSoccer.Position.Generic;
                agent.name = $"{team}Player{index + 1}_{role}";
                agent.gameObject.tag = team == Team.Blue ? "blueAgent" : "purpleAgent";
                agent.transform.localPosition = spawns[index];
                agent.transform.localRotation = Quaternion.Euler(0f, facingYaw, 0f);
                agent.Configure(team, role, team == Team.Blue && index == 0, spawns[index]);

                var behavior = agent.GetComponent<BehaviorParameters>();
                behavior.BehaviorName = "Soccer4v4";
                behavior.TeamId = (int)team;
                behavior.BehaviorType = BehaviorType.Default;
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

            var teamTag = agent.Team == Team.Blue ? "blueAgent" : "purpleAgent";
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
            return settings;
        }

        static void CreateScene(GameObject fieldPrefab, PanelSettings panelSettings)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var fieldObject = (GameObject)PrefabUtility.InstantiatePrefab(fieldPrefab, scene);
            fieldObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var environment = fieldObject.GetComponent<SoccerEnvController>();

            var settingsObject = new GameObject("SoccerSettings");
            var settings = settingsObject.AddComponent<SoccerSettings>();
            settings.agentRunSpeed = 2.2f;
            settings.maximumPlanarSpeed = 9f;
            settings.rotationSpeed = 125f;
            settings.blueMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Blue.mat");
            settings.purpleMaterial = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Purple.mat");

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
            camera.fieldOfView = 52f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 350f;
            cameraObject.transform.position = new Vector3(0f, 94f, -90f);
            cameraObject.transform.LookAt(new Vector3(0f, 0f, 0f));
            cameraObject.AddComponent<AudioListener>();
            cameraObject.AddComponent<SoccerPlayerCamera>().Configure(environment);

            var hudObject = new GameObject("Soccer HUD");
            var document = hudObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            hudObject.AddComponent<SoccerHudController>().Configure(environment);

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static void AddSceneToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            var existing = scenes.FindIndex(scene => scene.path == ScenePath);
            if (existing >= 0)
            {
                scenes[existing] = new EditorBuildSettingsScene(ScenePath, true);
            }
            else
            {
                scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        static VisualMaterialSet CreateVisualMaterialSet()
        {
            return new VisualMaterialSet
            {
                Blue = CreateOrUpdateUrpMaterial("AgentBlue", new Color(0.13f, 0.59f, 0.95f)),
                Purple = CreateOrUpdateUrpMaterial("AgentPurple", new Color(0.55f, 0.43f, 0.78f)),
                BlueKickPlate = CreateOrUpdateUrpMaterial("KickPlateBlue", new Color(0.03f, 0.30f, 0.46f)),
                PurpleKickPlate = CreateOrUpdateUrpMaterial("KickPlatePurple", new Color(0.29f, 0.17f, 0.48f)),
                Eye = CreateOrUpdateUrpMaterial("Eye", new Color(0.06f, 0.06f, 0.06f)),
                Wall = CreateOrUpdateUrpMaterial("GrayMiddle", new Color(0.39f, 0.39f, 0.39f)),
                Black = CreateOrUpdateUrpMaterial("Black", new Color(0.05f, 0.05f, 0.05f)),
                Net = CreateOrUpdateUrpMaterial("Net", Color.white),
                Glass = CreateOrUpdateUrpMaterial("ClearPlastic", new Color(0.62f, 0.84f, 0.96f, 0.18f), true)
            };
        }

        static Material CreateOrUpdateUrpMaterial(string name, Color color, bool transparent = false)
        {
            var path = $"{Root}/Materials/{name}.mat";
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
                if (objectName == "AgentCube_Blue" || objectName == "GoalBlue")
                {
                    renderer.sharedMaterial = materials.Blue;
                }
                else if (objectName == "AgentCube_Purple" || objectName == "GoalPurple")
                {
                    renderer.sharedMaterial = materials.Purple;
                }
                else if (objectName is "eye" or "mouth")
                {
                    renderer.sharedMaterial = materials.Eye;
                }
                else if (objectName.StartsWith("GoalNet", StringComparison.Ordinal))
                {
                    renderer.sharedMaterials = objectName.EndsWith("Outer", StringComparison.Ordinal)
                        ? new[] { materials.Net }
                        : new[] { materials.Black, materials.Net };
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
            foreach (var directory in new[] { Root + "/Prefabs", Root + "/Scenes", Root + "/Materials", Root + "/UI" })
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
            public Material Blue;
            public Material Purple;
            public Material BlueKickPlate;
            public Material PurpleKickPlate;
            public Material Eye;
            public Material Wall;
            public Material Black;
            public Material Net;
            public Material Glass;
        }
    }
}
