using MachineLearning.Soccer.Editor;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MachineLearning.Soccer.Tests
{
    public sealed class StadiumGeometryTests
    {
        [Test]
        public void GeneratedStadiumHasIndependentAssetsAndCompleteSceneBindings()
        {
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                SoccerStadiumBuilder.ValidateScene(EditorSceneManager.OpenScene(SoccerStadiumBuilder.ScenePath));
                Assert.AreNotEqual(AssetDatabase.AssetPathToGUID(SoccerStadiumBuilder.SourceScenePath),
                    AssetDatabase.AssetPathToGUID(SoccerStadiumBuilder.ScenePath));
                Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<GameObject>(SoccerStadiumBuilder.PrefabPath));
            }
            finally
            {
                if (setup.Any(item => item.isLoaded && item.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [Test]
        public void EveryActiveStadiumOmitsApprovedExteriorOptimizationInstances()
        {
            var prefabPaths = new[]
            {
                "Assets/_Soccer/Core/Prefabs/StadiumEnvironment_Base.prefab",
                "Assets/_Soccer/Teams/Attack_KMW/Prefabs/StadiumEnvironment_Attack.prefab",
                "Assets/_Soccer/Teams/Defense_PJH/Prefabs/StadiumEnvironment_Defense.prefab",
                "Assets/_Soccer/Teams/Press_KMG/Prefabs/StadiumEnvironment_Press.prefab",
                "Assets/_Soccer/Teams/Rule_PHC/Prefabs/StadiumEnvironment_Rule.prefab"
            };
            foreach (var prefabPath in prefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.IsNotNull(prefab, prefabPath);
                var remaining = prefab.GetComponentsInChildren<Transform>(true)
                    .Where(item => SoccerStadiumBuilder.IsExteriorOptimizationPrefabInstance(item)
                        || SoccerStadiumBuilder.IsRemovedExteriorPrefabName(item.name))
                    .Select(item => item.name)
                    .ToArray();
                CollectionAssert.IsEmpty(remaining, prefabPath + ": " + string.Join(", ", remaining));
            }

            var scenePaths = new[]
            {
                "Assets/_Soccer/Core/Scenes/Stadium4v4_Base.unity",
                "Assets/_Soccer/Teams/Attack_KMW/Scenes/Stadium4v4_Attack.unity",
                "Assets/_Soccer/Teams/Defense_PJH/Scenes/Stadium4v4_Defense.unity",
                "Assets/_Soccer/Teams/Press_KMG/Scenes/Stadium4v4_Press.unity",
                "Assets/_Soccer/Teams/Rule_PHC/Scenes/Stadium4v4_Rule.unity"
            };
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                foreach (var scenePath in scenePaths)
                {
                    var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                    var remaining = scene.GetRootGameObjects()
                        .SelectMany(item => item.GetComponentsInChildren<Transform>(true))
                        .Where(item => SoccerStadiumBuilder.IsExteriorOptimizationPrefabInstance(item)
                            || SoccerStadiumBuilder.IsRemovedExteriorPrefabName(item.name))
                        .Select(item => item.name)
                        .ToArray();
                    CollectionAssert.IsEmpty(remaining, scenePath + ": " + string.Join(", ", remaining));
                }
            }
            finally
            {
                if (setup.Any(item => item.isLoaded && item.isActive))
                    EditorSceneManager.RestoreSceneManagerSetup(setup);
                else
                    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }

        [TestCase(Team.Red)]
        [TestCase(Team.Navy)]
        public void GoalPredictionFallbackUsesTheSameStadiumOpening(Team team)
        {
            var obj = new GameObject("GeometryTest");
            try
            {
                var arena = obj.AddComponent<SoccerArenaGeometry>();
                arena.Configure(124f, 84.65f, 20.4f, 6.3f, 5.6f);
                var sign = SoccerDefensiveClearanceRules.GetAttackSign(team);
                var position = new Vector3(-50f * sign, 0.5f, 11.5f);
                var direction = -Vector3.right * sign;
                Assert.IsFalse(SoccerDefensiveClearanceRules.PredictsOwnGoal(team, position, direction));
                Assert.IsFalse(SoccerDefensiveClearanceRules.PredictsOwnGoal(team, position, direction, arena));
                position.z = 0f;
                Assert.IsTrue(SoccerDefensiveClearanceRules.PredictsOwnGoal(team, position, direction));
                Assert.IsTrue(SoccerDefensiveClearanceRules.PredictsOwnGoal(team, position, direction, arena));
                Assert.IsFalse(SoccerDefensiveClearanceRules.PredictsOwnGoal(team, position, -direction, arena));
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [Test]
        public void ExpandedSidelinesAreReachableButEndWallsCannotBeTargetedThrough()
        {
            var obj = new GameObject("GeometryTest");
            try
            {
                var arena = obj.AddComponent<SoccerArenaGeometry>();
                arena.Configure(124f, 84.65f, 20.4f, 6.3f, 5.6f, SoccerStadiumBuilder.CornerRadius, 2f);
                Assert.AreEqual(40f, arena.ClampTarget(new Vector3(0f, 0.5f, 40f)).z);
                Assert.Less(arena.ClampTarget(new Vector3(66f, 0.5f, 12f)).x, 62f);
                Assert.AreEqual(66f, arena.ClampTarget(new Vector3(66f, 0.5f, 0f)).x);
                Assert.IsFalse(arena.ContainsGoalBall(Team.Navy, new Vector3(61.9f, 0.5f, 0f)));
                Assert.IsTrue(arena.ContainsGoalBall(Team.Navy, new Vector3(63f, 0.5f, 0f)));
                Assert.IsFalse(arena.ContainsGoalBall(Team.Navy, new Vector3(63f, 0.5f, 12f)));
                Assert.IsFalse(arena.ContainsGoalBall(Team.Navy, new Vector3(70f, 0.5f, 0f)));
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [Test]
        public void RoundedCornersHaveThreeOneAndAHalfMetreChordsAndExcludeUnreachableTargets()
        {
            var obj = new GameObject("CornerGeometryTest");
            try
            {
                var arena = obj.AddComponent<SoccerArenaGeometry>();
                arena.Configure(124f, 84.65f, 20.4f, 6.3f, 5.6f, SoccerStadiumBuilder.CornerRadius, 2f);
                foreach (var xSign in new[] { -1, 1 })
                foreach (var zSign in new[] { -1, 1 })
                {
                    for (var segment = 0; segment < 3; segment++)
                        Assert.AreEqual(1.5f, Vector3.Distance(arena.GetCornerPoint(xSign, zSign, segment),
                            arena.GetCornerPoint(xSign, zSign, segment + 1)), 0.001f);
                    var oldCorner = new Vector3(xSign * arena.HalfLength, 0.52f, zSign * arena.HalfWidth);
                    Assert.IsFalse(arena.IsInsideRoundedCorners(oldCorner));
                    var target = arena.ClampTarget(oldCorner);
                    Assert.IsTrue(arena.IsInsideRoundedCorners(target, 0.55f));
                    Assert.That(Vector3.Distance(target, oldCorner), Is.InRange(1.8f, 2.5f),
                        "The larger corner should move targets inward while excluding only a small patch.");
                }
                Assert.AreEqual(8.397f, arena.CornerRadius * arena.CornerRadius, 0.001f);
                Assert.Less(arena.CornerRadius * arena.CornerRadius / (124f * 84.65f), 0.0009f);
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [Test]
        public void BallClampAllowsGoalDepthButContainsSidesEndsAndRoundedCorners()
        {
            var obj = new GameObject("BallClampGeometryTest");
            try
            {
                const float radius = 0.53f;
                var arena = obj.AddComponent<SoccerArenaGeometry>();
                arena.Configure(124f, 84.65f, 20.4f, 6.3f, 5.6f,
                    SoccerStadiumBuilder.CornerRadius, 2f);

                var side = arena.ClampBallPosition(new Vector3(0f, radius, 100f), radius);
                Assert.AreEqual(arena.HalfWidth - radius, side.z, 0.001f);
                var end = arena.ClampBallPosition(new Vector3(100f, radius, 15f), radius);
                Assert.AreEqual(arena.HalfLength - radius, end.x, 0.001f);
                var goal = arena.ClampBallPosition(new Vector3(100f, radius, 0f), radius);
                Assert.AreEqual(arena.HalfLength + arena.GoalDepth - radius, goal.x, 0.001f);
                var corner = arena.ClampBallPosition(
                    new Vector3(arena.HalfLength, radius, arena.HalfWidth), radius);
                Assert.IsTrue(arena.IsInsideRoundedCorners(corner, radius));
                Assert.IsTrue(arena.ContainsBallPosition(corner, radius));
                Assert.IsFalse(arena.ContainsBallPosition(
                    new Vector3(arena.HalfLength + arena.GoalDepth + 1f, radius, 0f), radius));
            }
            finally { Object.DestroyImmediate(obj); }
        }

        [Test]
        public void ReapplyingStadiumTuningDoesNotAccumulateGoalScaleOrChangeTerrain()
        {
            var root = PrefabUtility.LoadPrefabContents(SoccerStadiumBuilder.PrefabPath);
            try
            {
                var terrain = root.GetComponentInChildren<Terrain>().terrainData;
                var beforeMaterials = root.GetComponentsInChildren<MeshRenderer>()
                    .Where(renderer => renderer.name.StartsWith("SM_S_Gate")).Select(renderer => renderer.sharedMaterial).ToArray();
                var beforeGoals = root.GetComponentsInChildren<MeshRenderer>()
                    .Where(renderer => renderer.name.StartsWith("SM_S_Gate")).Select(renderer => renderer.bounds.size).ToArray();
                SoccerStadiumBuilder.ApplyTuningToEnvironment(root);
                SoccerStadiumBuilder.ApplyTuningToEnvironment(root);
                var afterGoals = root.GetComponentsInChildren<MeshRenderer>()
                    .Where(renderer => renderer.name.StartsWith("SM_S_Gate")).Select(renderer => renderer.bounds.size).ToArray();
                for (var i = 0; i < beforeGoals.Length; i++)
                    Assert.Less(Vector3.Distance(beforeGoals[i], afterGoals[i]), 0.001f);
                Assert.AreSame(terrain, root.GetComponentInChildren<Terrain>().terrainData);
                CollectionAssert.AreEqual(beforeMaterials, root.GetComponentsInChildren<MeshRenderer>()
                    .Where(renderer => renderer.name.StartsWith("SM_S_Gate")).Select(renderer => renderer.sharedMaterial).ToArray());
                Assert.AreEqual(0, terrain.treeInstanceCount);
                Assert.IsFalse(root.GetComponentsInChildren<Transform>(true).Any(item =>
                    item.name.StartsWith("SM_Tree_") || item.name.StartsWith("SM_Fir_")));
                Assert.IsFalse(root.GetComponentsInChildren<Transform>(true).Any(item =>
                    SoccerStadiumBuilder.IsExteriorOptimizationPrefabInstance(item)
                    || SoccerStadiumBuilder.IsRemovedExteriorPrefabName(item.name)));
                var walls = root.GetComponentsInChildren<BoxCollider>().Where(collider => collider.CompareTag("wall")).ToArray();
                Assert.AreEqual(18, walls.Length);
                Assert.IsTrue(walls.All(wall => Mathf.Abs(wall.bounds.size.y - SoccerStadiumBuilder.WallHeight) < 0.001f));
                Assert.IsTrue(walls.All(wall => Mathf.Abs((wall.name.StartsWith("Wall_End_")
                    ? wall.transform.localScale.x : wall.transform.localScale.z) - SoccerStadiumBuilder.WallThickness) < 0.001f));
                Assert.IsTrue(walls.All(wall => Mathf.Abs(wall.GetComponent<MeshRenderer>().sharedMaterial
                    .GetColor("_BaseColor").a - SoccerStadiumBuilder.WallAlpha) < 0.001f));
                var stadium = root.GetComponent<SoccerEnvController>();
                Assert.AreEqual(2000f, stadium.ControlledKickPower);
                Assert.AreEqual(5000f, stadium.StrongKickPower);
                var stadiumBall = stadium.ball.GetComponent<SoccerBallController>();
                var stadiumRadius = stadium.ball.GetComponent<SphereCollider>().radius
                    * stadium.ball.transform.lossyScale.x;
                Assert.IsTrue(stadiumBall.EnforcesStadiumPlanarMotion);
                Assert.AreEqual(stadiumRadius, stadiumBall.LockedCenterHeight, 0.001f);
                Assert.AreEqual(stadiumRadius, stadium.ball.transform.position.y, 0.001f);
                var goalFloors = root.GetComponentsInChildren<MeshRenderer>(true)
                    .Where(renderer => renderer.name.EndsWith("_Goal_Floor")).ToArray();
                Assert.AreEqual(2, goalFloors.Length);
                Assert.IsTrue(goalFloors.All(floor =>
                    AssetDatabase.GetAssetPath(floor.sharedMaterial) == SoccerStadiumBuilder.GoalFloorMaterialPath
                    && Vector4.Distance(floor.sharedMaterial.GetColor("_BaseColor"),
                        SoccerStadiumBuilder.GoalFloorColor) < 0.001f
                    && Mathf.Abs(floor.bounds.max.y) < 0.001f));
                Assert.AreEqual(2000f, AgentSoccer.ControlledKickPower);
                Assert.AreEqual(5000f, AgentSoccer.StrongKickPower);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
