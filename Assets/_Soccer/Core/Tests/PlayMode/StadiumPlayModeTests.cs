using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace MachineLearning.Soccer.Tests
{
    public sealed class StadiumPlayModeTests
    {
        const string ScenePath = "Assets/_Soccer/Core/Scenes/Stadium4v4_Base.unity";
        SoccerEnvController environment;

        [UnitySetUp]
        public IEnumerator LoadStadium()
        {
            Time.timeScale = 1f;
#if UNITY_EDITOR
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
#else
            Assert.Ignore("This asset-isolated test loads the Core scene directly in the Editor.");
            yield break;
#endif
            yield return null;
            environment = Object.FindFirstObjectByType<SoccerEnvController>();
            Assert.IsNotNull(environment);
        }

        [UnityTearDown]
        public IEnumerator RestoreTime()
        {
            Time.timeScale = 1f;
            yield return null;
        }

        void ParkPlayers()
        {
            var index = 0;
            foreach (var item in environment.AgentsList)
            {
                var agent = item.Agent;
                agent.GetComponent<DecisionRequester>().enabled = false;
                agent.enabled = false;
                agent.agentRb.linearVelocity = Vector3.zero;
                agent.agentRb.angularVelocity = Vector3.zero;
                agent.agentRb.constraints = RigidbodyConstraints.FreezeAll;
                agent.transform.position = new Vector3(-28f + 8f * index++, 0.52f, -20f);
            }
            Physics.SyncTransforms();
        }

        void PlaceBall(Vector3 position, Vector3 velocity)
        {
            var controller = environment.ball.GetComponent<SoccerBallController>();
            position.y = controller.LockedCenterHeight;
            environment.ballRb.isKinematic = false;
            environment.ballRb.position = position;
            environment.ballRb.linearVelocity = velocity;
            environment.ballRb.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        [UnityTest]
        public IEnumerator StartsWithEightPlayersLargerBallHudAndHumanCamera()
        {
            Assert.AreEqual(8, environment.AgentsList.Count);
            Assert.AreEqual(4, environment.AgentsList.Count(item => item.Agent.Team == Team.Red));
            Assert.AreEqual(4, environment.AgentsList.Count(item => item.Agent.Team == Team.Navy));
            Assert.AreEqual(300f, environment.matchDurationSeconds);
            Assert.AreEqual(3f, environment.goalResetDelaySeconds);
            Assert.That(environment.RemainingTime, Is.InRange(298f, 300f));
            Assert.AreEqual(Vector3.one * (0.0105f * 1.1f * 1.1f), environment.ball.transform.localScale);
            Assert.AreEqual(2000f, environment.ControlledKickPower);
            Assert.IsTrue(environment.AgentsList.All(item => item.Agent.EffectiveControlledKickPower == 2000f));
            Assert.AreEqual(5000f, environment.StrongKickPower);
            Assert.IsTrue(environment.AgentsList.All(item => item.Agent.EffectiveStrongKickPower == 5000f));
            Assert.AreEqual(3f, environment.ballRb.mass);
            Assert.AreEqual(RigidbodyConstraints.FreezePositionY, environment.ballRb.constraints);
            var ballRadius = environment.ball.GetComponent<SphereCollider>().radius
                * environment.ball.transform.lossyScale.x;
            var ballController = environment.ball.GetComponent<SoccerBallController>();
            Assert.IsTrue(ballController.EnforcesStadiumPlanarMotion);
            Assert.AreEqual(ballRadius, ballController.LockedCenterHeight, 0.001f);
            Assert.AreEqual(ballRadius, environment.ball.transform.position.y, 0.001f);
            var walls = environment.GetComponentsInChildren<BoxCollider>()
                .Where(collider => collider.CompareTag("wall")).ToArray();
            Assert.AreEqual(18, walls.Length);
            Assert.IsTrue(walls.All(wall => Mathf.Abs(wall.bounds.size.y - 2.835f) < 0.001f));
            var goalFloors = environment.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.name.EndsWith("_Goal_Floor")).ToArray();
            Assert.AreEqual(2, goalFloors.Length);
            Assert.IsTrue(goalFloors.All(floor =>
                Vector4.Distance(floor.sharedMaterial.GetColor("_BaseColor"),
                    (Color)new Color32(210, 214, 219, 255)) < 0.001f
                && floor.GetComponent<BoxCollider>() != null
                && Mathf.Abs(floor.bounds.max.y) < 0.001f));
            var hud = Object.FindFirstObjectByType<SoccerHudController>(FindObjectsInactive.Include);
            Assert.AreSame(environment, hud.Environment);
            Assert.IsTrue(hud.RewardPanelBottomRight);
            var tree = hud.GetComponent<UIDocument>().visualTreeAsset.CloneTree();
            Assert.IsNotNull(tree.Q<Label>("score-label"));
            Assert.IsNotNull(tree.Q<Label>("time-label"));
            Assert.IsNotNull(tree.Q<Label>("red-reward-label"));
            Assert.IsNotNull(tree.Q<VisualElement>(className: "tactic-reward-box"));
            var camera = Object.FindFirstObjectByType<SoccerPlayerCamera>();
            Assert.IsTrue(environment.IsAIEnabled);
            environment.SetAIEnabled(false);
            yield return null;
            Assert.IsTrue(environment.HumanControlledAgent.IsUsingHumanInput);
            Assert.IsTrue(camera.IsFollowingHuman);
            environment.SetAIEnabled(true);
            yield return null;
            Assert.IsFalse(camera.IsFollowingHuman);
        }

        [UnityTest]
        public IEnumerator EveryTeamStadiumStartsWithItsOwnTacticAndTheSharedRuntimeContract()
        {
#if UNITY_EDITOR
            var cases = new[]
            {
                ("Assets/_Soccer/Core/Scenes/Stadium4v4_Base.unity", "base", true, true),
                ("Assets/_Soccer/Teams/Attack_KMW/Scenes/Stadium4v4_Attack.unity", "attack", true, false),
                ("Assets/_Soccer/Teams/Defense_PJH/Scenes/Stadium4v4_Defense.unity", "defense", true, false),
                ("Assets/_Soccer/Teams/Press_KMG/Scenes/Stadium4v4_Press.unity", "press", true, false),
                ("Assets/_Soccer/Teams/Rule_PHC/Scenes/Stadium4v4_Rule.unity", "rule", false, false)
            };
            foreach (var item in cases)
            {
                yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                    item.Item1,
                    new LoadSceneParameters(LoadSceneMode.Single));
                yield return null;
                var activeEnvironment = Object.FindFirstObjectByType<SoccerEnvController>();
                var setup = activeEnvironment.GetComponent<SoccerMatchSetup>();
                Assert.IsNotNull(activeEnvironment.ArenaGeometry, item.Item1);
                Assert.AreEqual(SoccerArenaGeometry.StadiumHalfLength, activeEnvironment.ArenaGeometry.HalfLength, 0.001f, item.Item1);
                Assert.AreEqual(SoccerArenaGeometry.StadiumHalfWidth, activeEnvironment.ArenaGeometry.HalfWidth, 0.01f, item.Item1);
                Assert.AreEqual(2000f, activeEnvironment.ControlledKickPower, 0.001f, item.Item1);
                Assert.AreEqual(5000f, activeEnvironment.StrongKickPower, 0.001f, item.Item1);
                Assert.AreEqual(item.Item2, setup.RedTeam.TeamId, item.Item1);
                Assert.AreEqual("base", setup.NavyTeam.TeamId, item.Item1);
                Assert.AreEqual(item.Item3, setup.TrainRed, item.Item1);
                Assert.AreEqual(item.Item4, setup.TrainNavy, item.Item1);
                Assert.AreEqual(8, activeEnvironment.AgentsList.Count, item.Item1);
                Assert.IsTrue(activeEnvironment.AgentsList.All(entry =>
                    entry.Agent.GetComponentsInChildren<RayPerceptionSensorComponent3D>().Length == 2), item.Item1);
                Assert.IsNotNull(Object.FindFirstObjectByType<SoccerHudController>(FindObjectsInactive.Include), item.Item1);
                var camera = Camera.main;
                Assert.IsNotNull(camera, item.Item1);
                Assert.AreSame(activeEnvironment, camera.GetComponent<SoccerGoalOcclusionFader>().Environment, item.Item1);
            }
#else
            Assert.Ignore("Team Stadium asset loading is verified in the Unity Editor.");
            yield break;
#endif
        }

        [UnityTest]
        public IEnumerator PhysicalShotsScoreBothGoalsOnceAndResetAllBodies()
        {
            var starts = environment.AgentsList.Select(item => item.Agent.StartingPosition).ToArray();
            foreach (var sign in new[] { -1f, 1f })
            {
                environment.RestartMatch();
                ParkPlayers();
                // This lane was outside the old 10.2m-wide mouth.
                PlaceBall(new Vector3(sign * 58f, 0.52f, 7f), Vector3.right * (sign * 18f));
                var trace = "";
                for (var i = 0; i < 120 && environment.State == SoccerMatchState.Playing; i++)
                {
                    yield return new WaitForFixedUpdate();
                    if (i % 10 == 0) trace += $"\n{i}: position={environment.ballRb.position:F3}, velocity={environment.ballRb.linearVelocity:F3}";
                }
                Assert.AreEqual(SoccerMatchState.GoalPause, environment.State, "Physical ball did not score on side " + sign + trace);
                Assert.AreEqual(sign < 0 ? 1 : 0, environment.NavyScore);
                Assert.AreEqual(sign > 0 ? 1 : 0, environment.RedScore);
                Assert.IsTrue(environment.ballRb.isKinematic);
                yield return new WaitForSeconds(0.3f);
                Assert.AreEqual(1, environment.RedScore + environment.NavyScore, "Duplicate score during pause.");
                for (var i = 0; i < 190 && environment.State == SoccerMatchState.GoalPause; i++)
                    yield return new WaitForFixedUpdate();
                Assert.AreEqual(SoccerMatchState.Playing, environment.State);
                Assert.IsFalse(environment.ballRb.isKinematic);
                Assert.Less(new Vector2(environment.ball.transform.position.x, environment.ball.transform.position.z).magnitude, 0.02f);
                for (var i = 0; i < starts.Length; i++)
                    Assert.Less(Vector3.Distance(starts[i], environment.AgentsList[i].Agent.transform.position), 0.1f);
            }
        }

        [UnityTest]
        public IEnumerator EveryCornerPanelAndJointRejectsTheBallAndIsSeenAsWall()
        {
            ParkPlayers();
            var arena = environment.ArenaGeometry;
            var ballRadius = environment.ball.GetComponent<SphereCollider>().radius * environment.ball.transform.lossyScale.x;
            var sensor = environment.AgentsList[0].Agent.GetComponentsInChildren<RayPerceptionSensorComponent3D>()
                .First(item => !item.SensorName.EndsWith("Reverse"));
            foreach (var xSign in new[] { -1, 1 })
            foreach (var zSign in new[] { -1, 1 })
            {
                // Midpoints and both inter-panel joints, plus both wall junctions.
                for (var sample = 0; sample <= 6; sample++)
                {
                    var pointIndex = sample / 2;
                    var boundary = arena.GetCornerPoint(xSign, zSign, pointIndex, 0.55f);
                    var angle = sample * 15f * Mathf.Deg2Rad;
                    var outward = new Vector3(xSign * Mathf.Cos(angle), 0f, zSign * Mathf.Sin(angle));
                    if (sample % 2 != 0)
                        boundary = (boundary + arena.GetCornerPoint(xSign, zSign, pointIndex + 1, 0.55f)) * 0.5f;
                    var origin = boundary - outward * 3f;
                    environment.ball.transform.position = new Vector3(0f, 0.55f, 0f);
                    var observer = environment.AgentsList[0].Agent;
                    observer.transform.position = origin;
                    observer.transform.rotation = Quaternion.LookRotation(outward);
                    Physics.SyncTransforms();
                    var rays = RayPerceptionSensor.Perceive(sensor.GetRayPerceptionInput(), false);
                    Assert.IsTrue(rays.RayOutputs.Any(ray => ray.HitTaggedObject && ray.HitGameObject.CompareTag("wall")));
                    ParkPlayers();
                    PlaceBall(origin, outward * 30f);
                    for (var step = 0; step < 18; step++)
                    {
                        yield return new WaitForFixedUpdate();
                        Assert.IsTrue(arena.IsInsideRoundedCorners(environment.ballRb.position, ballRadius - 0.04f),
                            $"Corner penetration at {xSign},{zSign}, sample {sample}: {environment.ballRb.position}");
                    }
                    Assert.Less(Vector3.Dot(environment.ballRb.linearVelocity, outward), 0f, "Ball must rebound out of the corner.");
                }
            }
        }

        [UnityTest]
        public IEnumerator ControlledPassUsesTheStadiumPowerInActualContact()
        {
            ParkPlayers();
            var agent = environment.AgentsList.First(item => item.Agent.Team == Team.Red
                && item.Agent.PositionRole == AgentSoccer.Position.Striker).Agent;
            var results = new float[2];
            for (var attempt = 0; attempt < 2; attempt++)
            {
                environment.ConfigureControlledKickPower(attempt == 0 ? 1500f : 2000f);
                agent.ResetKickPlate();
                agent.transform.SetPositionAndRotation(new Vector3(0f, 0.52f, 0f), Quaternion.LookRotation(Vector3.right));
                PlaceBall(new Vector3(1.2f, 0.55f, 0f), Vector3.zero);
                agent.OnActionReceived(new ActionBuffers(ActionSegment<float>.Empty, new ActionSegment<int>(new[] { 0, 0, 0, 1 })));
                for (var step = 0; step < 8; step++) yield return new WaitForFixedUpdate();
                results[attempt] = environment.ballRb.linearVelocity.x;
                Assert.Greater(results[attempt], 5f, "Pass did not make physical contact.");
            }
            Assert.That(results[1] / results[0], Is.InRange(1.25f, 1.42f), "The extra 500 power must affect the actual pass impulse.");
            environment.ConfigureControlledKickPower(2000f);
        }

        [UnityTest]
        public IEnumerator StrongShotUsesTheExtraFiveHundredPowerInActualContact()
        {
            ParkPlayers();
            var agent = environment.AgentsList.First(item => item.Agent.Team == Team.Red
                && item.Agent.PositionRole == AgentSoccer.Position.Striker).Agent;
            var results = new float[2];
            for (var attempt = 0; attempt < 2; attempt++)
            {
                environment.ConfigureStrongKickPower(attempt == 0 ? 4500f : 5000f);
                agent.ResetKickPlate();
                agent.transform.SetPositionAndRotation(new Vector3(0f, 0.52f, 0f), Quaternion.LookRotation(Vector3.right));
                PlaceBall(new Vector3(1.2f, 0.55f, 0f), Vector3.zero);
                agent.OnActionReceived(new ActionBuffers(ActionSegment<float>.Empty, new ActionSegment<int>(new[] { 0, 0, 0, 2 })));
                for (var step = 0; step < 8; step++) yield return new WaitForFixedUpdate();
                results[attempt] = environment.ballRb.linearVelocity.x;
                Assert.Greater(results[attempt], 10f, "Strong shot did not make physical contact.");
            }
            Assert.That(results[1] / results[0], Is.InRange(1.05f, 1.16f),
                "The extra 500 power must affect the actual Strong-shot impulse.");
            environment.ConfigureStrongKickPower(5000f);
        }

        [UnityTest]
        public IEnumerator PostsAndOutsideMouthDoNotScore()
        {
            foreach (var sign in new[] { -1f, 1f })
            {
                foreach (var z in new[] { environment.ArenaGeometry.GoalHalfWidth - 0.1f, 12f })
                {
                    environment.RestartMatch();
                    ParkPlayers();
                    PlaceBall(new Vector3(sign * 59f, 0.52f, z), Vector3.right * (sign * 14f));
                    for (var i = 0; i < 50; i++) yield return new WaitForFixedUpdate();
                    Assert.AreEqual(0, environment.RedScore + environment.NavyScore, "Frame/outside shot scored.");
                    Assert.AreEqual(SoccerMatchState.Playing, environment.State);
                }
            }
        }

        [UnityTest]
        public IEnumerator FastBallCannotPassThroughSideEndOrCornerWalls()
        {
            ParkPlayers();
            var arena = environment.ArenaGeometry;
            var origins = new[]
            {
                new Vector3(0f, 0.52f, arena.HalfWidth - 3f),
                new Vector3(0f, 0.52f, -arena.HalfWidth + 3f),
                new Vector3(59f, 0.52f, 20f),
                new Vector3(-59f, 0.52f, 20f),
                new Vector3(59f, 0.52f, arena.HalfWidth - 3f)
            };
            var directions = new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, new Vector3(1f, 0f, 1f).normalized };
            for (var i = 0; i < origins.Length; i++)
            {
                PlaceBall(origins[i], directions[i] * 80f);
                for (var step = 0; step < 25; step++)
                {
                    yield return new WaitForFixedUpdate();
                    var position = environment.ball.transform.position;
                    Assert.LessOrEqual(Mathf.Abs(position.z), arena.HalfWidth + 0.05f);
                    Assert.LessOrEqual(Mathf.Abs(position.x), arena.HalfLength + 0.05f);
                    Assert.Greater(position.y, 0.3f);
                }
                Assert.LessOrEqual(Vector3.Dot(environment.ballRb.linearVelocity, directions[i]), 0.1f);
            }
        }

        [UnityTest]
        public IEnumerator MovingBallVisiblyRollsAndEmergencyClampPreventsEveryEscapeRoute()
        {
            ParkPlayers();
            var controller = environment.ball.GetComponent<SoccerBallController>();
            var radius = environment.ball.GetComponent<SphereCollider>().radius
                * environment.ball.transform.lossyScale.x;
            var initialRotation = environment.ballRb.rotation;
            PlaceBall(Vector3.zero, Vector3.right * 10f);
            for (var step = 0; step < 5; step++) yield return new WaitForFixedUpdate();
            Assert.Greater(Quaternion.Angle(initialRotation, environment.ballRb.rotation), 20f,
                "A moving Stadium ball must visibly roll, not only yaw.");
            Assert.Less(environment.ballRb.angularVelocity.z, -5f);
            Assert.That(Mathf.Abs(environment.ballRb.angularVelocity.z),
                Is.EqualTo(environment.ballRb.linearVelocity.x / radius).Within(0.75f));
            Assert.AreEqual(controller.LockedCenterHeight, environment.ballRb.position.y, 0.001f);

            var arena = environment.ArenaGeometry;
            var outsideSamples = new[]
            {
                new Vector3(0f, controller.LockedCenterHeight, arena.HalfWidth + 5f),
                new Vector3(arena.HalfLength + 5f, controller.LockedCenterHeight, arena.GoalHalfWidth + 3f),
                new Vector3(arena.HalfLength + arena.GoalDepth + 5f, controller.LockedCenterHeight, 0f),
                new Vector3(arena.HalfLength + 3f, controller.LockedCenterHeight, arena.HalfWidth + 3f)
            };
            foreach (var outside in outsideSamples)
            {
                environment.RestartMatch();
                ParkPlayers();
                PlaceBall(outside, new Vector3(Mathf.Sign(outside.x), 0f, Mathf.Sign(outside.z)) * 25f);
                yield return new WaitForFixedUpdate();
                Assert.IsTrue(arena.ContainsBallPosition(environment.ballRb.position, radius, 0.02f),
                    "Emergency boundary clamp failed at " + outside);
                Assert.AreEqual(controller.LockedCenterHeight, environment.ballRb.position.y, 0.001f);
            }
        }

        [UnityTest]
        public IEnumerator EveryFrontAndRearSensorClassifiesBallWallsGoalsAndTeams()
        {
            ParkPlayers();
            environment.ballRb.isKinematic = true;
            foreach (var item in environment.AgentsList)
            {
                var agent = item.Agent;
                foreach (var sensor in agent.GetComponentsInChildren<RayPerceptionSensorComponent3D>())
                {
                    foreach (var tag in sensor.DetectableTags)
                    {
                        ParkPlayers();
                        environment.ball.transform.position = new Vector3(20f, 0.5f, 20f);
                        Vector3 origin = new Vector3(0f, 0.52f, 0f);
                        Vector3 direction = Vector3.right;
                        if (tag == "ball") environment.ball.transform.position = new Vector3(5f, 0.5f, 0f);
                        else if (tag == "wall") { origin.z = 32f; direction = Vector3.forward; }
                        else if (tag == "redGoal") { origin.x = -54f; direction = Vector3.left; }
                        else if (tag == "navyGoal") { origin.x = 54f; direction = Vector3.right; }
                        else
                        {
                            var target = environment.AgentsList.Select(entry => entry.Agent)
                                .First(other => other != agent && other.CompareTag(tag));
                            target.transform.position = new Vector3(5f, 0.52f, 0f);
                        }
                        agent.transform.position = origin;
                        agent.transform.rotation = Quaternion.LookRotation(sensor.SensorName.EndsWith("Reverse") ? -direction : direction);
                        Physics.SyncTransforms();
                        foreach (var batched in new[] { false, true })
                        {
                            var output = RayPerceptionSensor.Perceive(sensor.GetRayPerceptionInput(), batched);
                            Assert.IsTrue(output.RayOutputs.Any(ray => ray.HitTaggedObject
                                && ray.HitTagIndex == sensor.DetectableTags.IndexOf(tag)
                                && ray.HitGameObject.CompareTag(tag)), agent.name + "/" + sensor.SensorName + "/" + tag + "/batched=" + batched);
                        }
                    }
                }
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DemoGoalFadesAndRestoresWithoutChangingPhysicsOrStadiumMaterial()
        {
            ParkPlayers();
            var camera = Camera.main;
            camera.GetComponent<SoccerPlayerCamera>().enabled = false;
            var fader = camera.GetComponent<SoccerGoalOcclusionFader>();
            Assert.AreEqual(1, fader.RedGoalRendererCount);
            Assert.AreEqual(1, fader.NavyGoalRendererCount);
            var goals = environment.GetComponentsInChildren<MeshRenderer>()
                .Where(renderer => renderer.name.StartsWith("SM_S_Gate")).OrderBy(renderer => renderer.bounds.center.x).ToArray();
            var pitch = environment.GetComponentsInChildren<MeshRenderer>().Single(renderer => renderer.name == "SM_Football_Field");
            var pitchMaterial = pitch.sharedMaterial;
            var originalMaterials = goals.Select(goal => goal.sharedMaterial).ToArray();
            var colliderStates = goals.SelectMany(goal => goal.GetComponents<Collider>()).Select(collider => collider.enabled).ToArray();
            var arena = environment.ArenaGeometry;
            for (var i = 0; i < goals.Length; i++)
            {
                ParkPlayers();
                var teamColor = i == 0 ? SoccerTeamVisuals.RedGoalColor : SoccerTeamVisuals.NavyGoalColor;
                var originalColor = originalMaterials[i].GetColor("_BaseColor");
                Assert.Less(Vector4.Distance(teamColor, originalColor), 0.001f);
                Assert.IsNull(originalMaterials[i].GetTexture("_BaseMap"), "The yellow vendor atlas must not tint the team color.");
                var sign = i == 0 ? -1f : 1f;
                var agent = environment.AgentsList[0].Agent;
                var outsideGoal = new Vector3(sign * (arena.HalfLength - 5f), 0.5f, 0f);
                var insideGoal = new Vector3(sign * (arena.HalfLength + arena.GoalDepth * 0.5f), 0.5f, 0f);
                var targetLookPosition = insideGoal + Vector3.up * 0.75f;

                camera.transform.position = insideGoal + Vector3.right * (sign * 20f) + Vector3.up * 2f;
                camera.transform.LookAt(targetLookPosition);
                agent.transform.position = outsideGoal;
                Physics.SyncTransforms();
                fader.RefreshOcclusion(true);
                Assert.IsFalse(i == 0 ? fader.IsRedGoalOccluding : fader.IsNavyGoalOccluding,
                    "A player outside the goal must never trigger goal fading.");
                Assert.AreSame(originalMaterials[i], goals[i].sharedMaterial);

                agent.transform.position = insideGoal;
                camera.transform.position = new Vector3(0f, 2.5f, 0f);
                camera.transform.LookAt(targetLookPosition);
                Physics.SyncTransforms();
                fader.RefreshOcclusion(true);
                Assert.IsFalse(i == 0 ? fader.IsRedGoalOccluding : fader.IsNavyGoalOccluding,
                    "A player inside the goal must not fade it when viewed through the unobstructed mouth.");
                Assert.AreSame(originalMaterials[i], goals[i].sharedMaterial);

                camera.transform.position = insideGoal + Vector3.right * (sign * 20f) + Vector3.up * 2f;
                camera.transform.LookAt(targetLookPosition);
                Physics.SyncTransforms();
                fader.RefreshOcclusion(true);
                Assert.IsTrue(i == 0 ? fader.IsRedGoalOccluding : fader.IsNavyGoalOccluding);
                Assert.AreEqual(0.25f, goals[i].sharedMaterial.GetColor("_BaseColor").a, 0.001f);
                var fadedColor = goals[i].sharedMaterial.GetColor("_BaseColor");
                Assert.AreEqual(teamColor.r, fadedColor.r, 0.001f);
                Assert.AreEqual(teamColor.g, fadedColor.g, 0.001f);
                Assert.AreEqual(teamColor.b, fadedColor.b, 0.001f);
                Assert.AreEqual(originalColor, originalMaterials[i].GetColor("_BaseColor"), "Fading must not mutate the saved team material.");
                Assert.AreSame(pitchMaterial, pitch.sharedMaterial);
                CollectionAssert.AreEqual(colliderStates, goals.SelectMany(goal => goal.GetComponents<Collider>()).Select(collider => collider.enabled).ToArray());
                camera.transform.position = new Vector3(0f, 100f, -100f);
                camera.transform.LookAt(Vector3.zero);
                ParkPlayers();
                fader.RefreshOcclusion(true);
                Assert.IsFalse(fader.IsRedGoalOccluding);
                Assert.IsFalse(fader.IsNavyGoalOccluding);
                Assert.AreSame(originalMaterials[i], goals[i].sharedMaterial);
                Assert.AreEqual(originalColor, goals[i].sharedMaterial.GetColor("_BaseColor"));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullFiveMinuteMatchCompletesWithoutEscapesAndCanRestart()
        {
            var ballRadius = environment.ball.GetComponent<SphereCollider>().radius
                * environment.ball.transform.lossyScale.x;
            Time.timeScale = 20f;
            var deadline = Time.realtimeSinceStartup + 90f;
            while (environment.State != SoccerMatchState.Finished && Time.realtimeSinceStartup < deadline)
            {
                var position = environment.ball.transform.position;
                Assert.Greater(position.y, -0.5f, "Ball fell through field.");
                Assert.IsTrue(environment.ArenaGeometry.ContainsBallPosition(position, ballRadius, 0.05f),
                    "Ball escaped the radius-aware Stadium bounds: " + position);
                yield return new WaitForFixedUpdate();
            }
            Assert.AreEqual(SoccerMatchState.Finished, environment.State);
            Assert.AreEqual(0f, environment.RemainingTime);
            Time.timeScale = 1f;
            environment.RestartMatch();
            Assert.AreEqual(SoccerMatchState.Playing, environment.State);
            Assert.AreEqual(0, environment.RedScore + environment.NavyScore);
            Assert.AreEqual(300f, environment.RemainingTime);
        }
    }
}
