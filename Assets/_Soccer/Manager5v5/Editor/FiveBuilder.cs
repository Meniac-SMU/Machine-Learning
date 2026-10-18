using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MachineLearning.Soccer.Manager;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace MachineLearning.Soccer.Manager5v5.Editor
{
    public static class FiveBuilder
    {
        public const string Root = "Assets/_Soccer/Manager5v5";
        public const string Source = "Assets/_Soccer/Manager/Exhibition/ExhibitionArena.prefab";
        public const string Prefab = Root + "/Prefabs/FiveArena.prefab";
        public const string Scene = Root + "/Scenes/FiveRuleMatch.unity";
        public static readonly string Evidence = ResolveEvidenceDirectory();
        static string ResolveEvidenceDirectory()
        {
            var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,"-fiveEvidenceDirectory");
            return index>=0&&index+1<args.Length?args[index+1]:"Logs/Manager5v5/manual-build-"+DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ");
        }
        [Serializable] sealed class BuildEvidence
        {
            public string source, originalGuid, copyGuid;
            public int sourcePlayers, players, staminaPlayers;
            public float halfLength, halfWidth, goalHalfWidth, ballRadius;
            public string[] sharedRenderAssets;
            public string[] stages;
        }
        public static void BuildBatch()
        {
            try { Build(); Debug.Log("MNG5 G0/G1 ASSET BUILD PASS"); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
        public static void BuildPlayerBatch()
        {
            try
            {
                var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,"-fivePlayerPath");
                if(index<0||index+1>=args.Length)throw new ArgumentException("A new -fivePlayerPath is required.");
                var path=Path.GetFullPath(args[index+1]);var allowed=Path.GetFullPath("Builds/Manager5v5")+Path.DirectorySeparatorChar;
                if(!path.StartsWith(allowed,StringComparison.OrdinalIgnoreCase)||File.Exists(path))throw new IOException("Expected a fresh Manager5v5 Player path.");
                Build();
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=path,target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
                if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new InvalidOperationException("5v5 Player build failed.");
                Debug.Log("MNG5 PLAYER BUILD PASS");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
        [MenuItem("Tools/Soccer 5v5/Build isolated arena")]
        public static void Build()
        {
            foreach (var dir in new[] {"Reference", "Prefabs", "Scenes", "Profiles", "UI"})
                Directory.CreateDirectory(Root + "/" + dir);
            Directory.CreateDirectory(Evidence);
            AssetDatabase.Refresh();
            CopyOnce(Source, Root + "/Reference/OriginalArena4v4.prefab");
            CopyOnce("Assets/_Soccer/Manager/Exhibition/Exhibition.unity", Root + "/Reference/OriginalExhibition.unity");
            CopyOnce("Assets/_Soccer/Manager/Profiles/MNG_Physics.asset", Root + "/Reference/OriginalPhysics.asset");
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            Require(original != null && original.GetComponentsInChildren<MNG_PlayerAvatar>(true).Length == 8,
                "Latest source must still be the original eight-player exhibition.");
            Require(AssetDatabase.AssetPathToGUID(Source) != AssetDatabase.AssetPathToGUID(Root + "/Reference/OriginalArena4v4.prefab"), "Copied prefab needs a new GUID.");
            var sourceGeometry = original.GetComponentInChildren<SoccerArenaGeometry>(true);
            var sourcePlayers = original.GetComponentsInChildren<MNG_PlayerAvatar>(true).OrderBy(p => p.Team).ThenBy(p => p.Slot).ToArray();
            var arena = PrefabUtility.LoadPrefabContents(Root + "/Reference/OriginalArena4v4.prefab");
            try
            {
                arena.SetActive(false);
                var oldPlayers = arena.GetComponentsInChildren<MNG_PlayerAvatar>(true).OrderBy(p => p.Team).ThenBy(p => p.Slot).ToArray();
                var oldBall = arena.GetComponentInChildren<MNG_BallControl>(true).gameObject;
                var rosterRoots = oldPlayers.Select(p => p.gameObject).ToArray();
                var baselineScales = rosterRoots.Select(p => p.transform.localScale).ToArray();
                var ballScale = oldBall.transform.localScale;
                StripOldControllers(arena);
                var profile = AssetDatabase.LoadAssetAtPath<FiveProfile>(Root + "/Profiles/FiveProfile.asset");
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<FiveProfile>();
                    AssetDatabase.CreateAsset(profile, Root + "/Profiles/FiveProfile.asset");
                }
                // Apply the approved recovery change to existing serialized profiles as well as new ones.
                profile.recoveryPerSecond = .03f;
                profile.slowRecoverySpeed = 3.6f;
                profile.Validate();
                EditorUtility.SetDirty(profile); // Persist newly added experiment fields, retaining any existing profile overrides.
                var match = arena.AddComponent<FiveMatch>();
                match.profile = profile;
                match.geometry = arena.GetComponentInChildren<SoccerArenaGeometry>(true);
                match.players = new FivePlayer[10];
                for (var team = 0; team < 2; team++)
                {
                    // Old keeper + two midfielders become keeper + defenders; copy the existing striker once.
                    var oldTeam = sourcePlayers.Where(p => (int)p.Team == team).ToArray();
                    var map = new[] {Array.FindIndex(oldTeam,p => p.Role == MNG_PlayerRole.Keeper),
                        Array.FindIndex(oldTeam,p => p.Role == MNG_PlayerRole.MidLeft),
                        Array.FindIndex(oldTeam,p => p.Role == MNG_PlayerRole.MidRight),
                        Array.FindIndex(oldTeam,p => p.Role == MNG_PlayerRole.Striker)};
                    for (var slot = 0; slot < 5; slot++)
                    {
                        var sourceIndex = team * 4 + map[Math.Min(slot,3)];
                        var playerObject = slot == 4 ? Object.Instantiate(rosterRoots[sourceIndex], rosterRoots[sourceIndex].transform.parent) : rosterRoots[sourceIndex];
                        if (slot == 4)
                        {
                            var copied = playerObject.GetComponent<FivePlayer>();
                            if (copied != null) Object.DestroyImmediate(copied);
                            var copiedStamina = playerObject.GetComponent<FiveStamina>();
                            if (copiedStamina != null) Object.DestroyImmediate(copiedStamina);
                        }
                        playerObject.name = $"Five_{(Team)team}_{(FiveRole)slot}";
                        playerObject.transform.localScale = baselineScales[sourceIndex] * FiveProfile.Scale;
                        var position = playerObject.transform.position;
                        float sign = team == 0 ? 1 : -1;
                        float x = slot == 0 ? -match.geometry.HalfLength + 4 : slot <= 2 ? -26 : -8;
                        float z = slot == 0 ? 0 : (slot == 1 || slot == 3 ? -1 : 1) * (slot <= 2 ? 16 : 13);
                        position = new Vector3(sign*x, rosterRoots[sourceIndex].transform.position.y * (slot == 4 ? 1 : FiveProfile.Scale), sign*z);
                        playerObject.transform.SetPositionAndRotation(position,Quaternion.LookRotation(new Vector3(sign,0,0)));
                        var player = playerObject.AddComponent<FivePlayer>();
                        player.team = (Team)team; player.role = (FiveRole)slot; player.slot = slot;
                        player.baselineScale = baselineScales[sourceIndex]; player.spawn = position;
                        if (slot != 0) playerObject.AddComponent<FiveStamina>();
                        var body = player.Body;
                        body.isKinematic = false; body.useGravity = false;
                        body.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                        body.interpolation = RigidbodyInterpolation.Interpolate;
                        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                        body.solverIterations = 48; body.solverVelocityIterations = 16;
                        var solid = playerObject.GetComponentsInChildren<Collider>(true).Where(c => !c.isTrigger).ToArray();
                        var torso = solid.FirstOrDefault(c => c.transform == playerObject.transform) ?? solid.First();
                        player.radius = Mathf.Max(torso.bounds.extents.x,torso.bounds.extents.z);
                        // Bounds are zero while inactive. Collider geometry is authoritative in that case.
                        if (torso is BoxCollider box) player.radius = Mathf.Max(box.size.x * box.transform.lossyScale.x, box.size.z * box.transform.lossyScale.z) * .5f;
                        if (torso is CapsuleCollider capsule) player.radius = capsule.radius * Mathf.Max(capsule.transform.lossyScale.x,capsule.transform.lossyScale.z);
                        if (torso is SphereCollider playerSphere) player.radius = playerSphere.radius * Mathf.Max(playerSphere.transform.lossyScale.x,playerSphere.transform.lossyScale.y,playerSphere.transform.lossyScale.z);
                        player.radius = Mathf.Max(.4f,player.radius);
                        player.headHeight = 3.5f * FiveProfile.Scale;
                        foreach (var marker in playerObject.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("HumanControlMarker"))) marker.gameObject.SetActive(false);
                        player.SetTarget(player.Position,player.Position + Vector2.right*sign);
                        match.players[player.Index] = player;
                    }
                }
                oldBall.name = "Five_Ball";
                oldBall.transform.localScale = ballScale * FiveProfile.Scale;
                var ballPosition = oldBall.transform.position; ballPosition.y *= FiveProfile.Scale;
                oldBall.transform.position = ballPosition;
                match.ball = oldBall.GetComponent<Rigidbody>();
                match.ball.isKinematic = false;
                match.ball.linearDamping = profile.EffectiveFreeBallDamping;
                match.ball.maxLinearVelocity = profile.maximumBallSpeed;
                match.ball.solverIterations = 48; match.ball.solverVelocityIterations = 16;
                match.ball.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                match.ballSpawn = ballPosition; match.baselineBallScale = ballScale;
                var sphere = oldBall.GetComponent<SphereCollider>();
                Require(sphere != null, "Expected the original spherical ball collider.");
                // Copy the source material; repeated builds always multiply the original, never the previous generated value.
                var sourceBallMaterial=sphere.sharedMaterial;
                Require(sourceBallMaterial!=null,"Expected the original ball material.");
                var materialPath=Root+"/Profiles/FiveBallPhysics.physicMaterial";
                var ballMaterial=AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(materialPath);
                if(ballMaterial==null){ballMaterial=Object.Instantiate(sourceBallMaterial);ballMaterial.name="FiveBallPhysics";AssetDatabase.CreateAsset(ballMaterial,materialPath);}
                ballMaterial.staticFriction=sourceBallMaterial.staticFriction*profile.ballFrictionMultiplier;
                ballMaterial.dynamicFriction=sourceBallMaterial.dynamicFriction*profile.ballFrictionMultiplier;
                ballMaterial.frictionCombine=sourceBallMaterial.frictionCombine;
                ballMaterial.bounciness=sourceBallMaterial.bounciness;ballMaterial.bounceCombine=sourceBallMaterial.bounceCombine;
                EditorUtility.SetDirty(ballMaterial);sphere.sharedMaterial=ballMaterial;
                sphere.contactOffset=profile.ballContactOffset;
                match.ballRadius = sphere.radius * Mathf.Max(Mathf.Abs(sphere.transform.lossyScale.x),Mathf.Abs(sphere.transform.lossyScale.y),Mathf.Abs(sphere.transform.lossyScale.z));
                oldBall.AddComponent<FiveGoalContact>().match = match;
                match.overview = arena.GetComponentInChildren<Camera>(true);
                Require(match.overview != null, "Source overview camera is missing.");
                match.overview.enabled = true;
                match.overview.transform.position = new Vector3(0,100,-55);
                match.overview.transform.LookAt(new Vector3(0,0,-5));
                match.overview.fieldOfView = 55;
                CopyOnce("Assets/_Soccer/Manager/Exhibition/UI/Panel.asset",Root+"/UI/Panel.asset");
                var hudObject = new GameObject("Five HUD"); hudObject.transform.SetParent(arena.transform,false);
                var document = hudObject.AddComponent<UIDocument>();
                document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(Root+"/UI/Panel.asset");
                document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Root+"/UI/FiveHud.uxml");
                hudObject.AddComponent<FiveHud>().match = match;
                match.Validate();
                Require(match.geometry.HalfLength == sourceGeometry.HalfLength && match.geometry.HalfWidth == sourceGeometry.HalfWidth, "Pitch size changed.");
                foreach(var player in match.players) Require(Vector3.Distance(player.transform.localScale,player.baselineScale*.9f)<.00001f,"Repeated player scaling.");
                if(AssetDatabase.LoadAssetAtPath<GameObject>(Root+"/Art/CubeRobots/Prefabs/RedRobot.prefab")!=null)
                    FiveRobotIntegration.Apply(arena);
                arena.name = "FiveArena"; arena.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(arena,Prefab);
                var report = new BuildEvidence {source=Source,originalGuid=AssetDatabase.AssetPathToGUID(Source),copyGuid=AssetDatabase.AssetPathToGUID(Prefab),sourcePlayers=8,players=10,staminaPlayers=8,
                    halfLength=match.geometry.HalfLength,halfWidth=match.geometry.HalfWidth,goalHalfWidth=match.geometry.GoalHalfWidth,ballRadius=match.ballRadius,
                    sharedRenderAssets=arena.GetComponentsInChildren<Renderer>(true).SelectMany(r=>r.sharedMaterials).Where(m=>m!=null).Select(AssetDatabase.GetAssetPath).Distinct().OrderBy(p=>p).ToArray(),
                    stages=new[]{"G0 original copy verified before conversion", "G1 asset shape validated; runtime tests still required"}};
                File.WriteAllText(Evidence+"/builder.json",JsonUtility.ToJson(report,true));
            }
            finally { PrefabUtility.UnloadPrefabContents(arena); }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),scene);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.72f,.75f,.8f);
            EditorSceneManager.SaveScene(scene,Scene);
            AssetDatabase.SaveAssets();
        }
        static void CopyOnce(string from,string to)
        {
            if (AssetDatabase.LoadMainAssetAtPath(to)==null && !AssetDatabase.CopyAsset(from,to)) throw new IOException("Could not copy "+from);
        }
        static void StripOldControllers(GameObject root)
        {
            var pending = root.GetComponentsInChildren<MonoBehaviour>(true).Where(c=>c!=null && !(c is SoccerArenaGeometry) && !(c is SoccerGoalSurface)
                && (c.GetType().Namespace?.StartsWith("MachineLearning.Soccer")==true || c.GetType().Namespace?.StartsWith("Unity.MLAgents")==true)).ToList();
            while(pending.Count>0)
            {
                var removable = pending.FirstOrDefault(candidate=>!pending.Any(other=>other!=candidate && other.gameObject==candidate.gameObject &&
                    other.GetType().GetCustomAttributes(typeof(RequireComponent),true).Cast<RequireComponent>().Any(r=>Requires(r,candidate.GetType()))));
                if(removable==null) throw new InvalidOperationException("Cyclic source component requirement.");
                pending.Remove(removable); Object.DestroyImmediate(removable);
            }
            foreach(var doc in root.GetComponentsInChildren<UIDocument>(true)) Object.DestroyImmediate(doc.gameObject);
            Require(!root.GetComponentsInChildren<MonoBehaviour>(true).Any(c=>c!=null &&
                (c.GetType().Namespace?.StartsWith("MachineLearning.Soccer.Manager")==true || c.GetType().Namespace?.StartsWith("Unity.MLAgents")==true)),
                "Legacy manager or ML-Agent survived conversion.");
        }
        static bool Requires(RequireComponent requirement,Type type)
            => requirement.m_Type0?.IsAssignableFrom(type)==true || requirement.m_Type1?.IsAssignableFrom(type)==true || requirement.m_Type2?.IsAssignableFrom(type)==true;
        static void Require(bool condition,string message) {if(!condition)throw new InvalidOperationException(message);}
    }
}
