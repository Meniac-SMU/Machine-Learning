using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MachineLearning.Soccer.Tests
{
    public sealed class SoccerEnvironmentPlayModeTests
    {
        [UnityTest]
        public IEnumerator GeneratedSceneStartsAsDeterministicAIControlledFourVersusFourMatch()
        {
            yield return SceneManager.LoadSceneAsync("Soccer4v4", LoadSceneMode.Single);
            yield return null;

            var environment = Object.FindFirstObjectByType<SoccerEnvController>();
            Assert.IsNotNull(environment);
            Assert.AreEqual(8, environment.AgentsList.Count);
            Assert.AreEqual(4, environment.AgentsList.Count(item => item.Agent.Team == Team.Blue));
            Assert.AreEqual(4, environment.AgentsList.Count(item => item.Agent.Team == Team.Purple));
            foreach (var team in new[] { Team.Blue, Team.Purple })
            {
                var teamAgents = environment.AgentsList.Where(item => item.Agent.Team == team).Select(item => item.Agent).ToArray();
                Assert.AreEqual(1, teamAgents.Count(agent => agent.PositionRole == AgentSoccer.Position.DefenderKeeper));
                Assert.AreEqual(2, teamAgents.Count(agent => agent.PositionRole == AgentSoccer.Position.Midfielder));
                Assert.AreEqual(1, teamAgents.Count(agent => agent.PositionRole == AgentSoccer.Position.Striker));
                Assert.IsTrue(teamAgents.All(agent => agent.GetComponent<BehaviorParameters>()
                    .BrainParameters.ActionSpec.BranchSizes.SequenceEqual(new[] { 3, 3, 3, 3 })));
            }
            Assert.IsNotNull(environment.HumanControlledAgent);
            Assert.IsFalse(environment.HumanControlledAgent.IsUsingHumanInput);
            Assert.IsTrue(environment.IsAIEnabled);
            Assert.AreEqual(SoccerMatchState.Playing, environment.State);
            Assert.That(environment.RemainingTime, Is.InRange(299f, 300f));
            Assert.AreEqual(RigidbodyInterpolation.None, environment.HumanControlledAgent.agentRb.interpolation);
            Assert.IsFalse(
                Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Any(renderer => renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(material => material == null)),
                "The generated Soccer scene must not contain renderers with missing materials.");

            var playerCamera = Object.FindFirstObjectByType<SoccerPlayerCamera>();
            Assert.IsNotNull(playerCamera);
            Assert.IsFalse(playerCamera.IsFollowingHuman);
            Assert.That(
                Vector3.Distance(playerCamera.transform.position, SoccerPlayerCamera.DefaultOverviewPosition),
                Is.LessThan(0.01f));

            environment.SetAIEnabled(false);
            yield return null;
            Assert.IsTrue(environment.HumanControlledAgent.IsUsingHumanInput);
            Assert.AreEqual(RigidbodyInterpolation.Interpolate, environment.HumanControlledAgent.agentRb.interpolation);
            Assert.IsTrue(playerCamera.IsFollowingHuman);

            environment.SetAIEnabled(true);
            yield return null;
            Assert.IsFalse(environment.HumanControlledAgent.IsUsingHumanInput);
            Assert.AreEqual(RigidbodyInterpolation.None, environment.HumanControlledAgent.agentRb.interpolation);
            Assert.IsFalse(playerCamera.IsFollowingHuman);
        }

        [UnityTest]
        public IEnumerator TeammateCrowdingPenaltyIsSampledWithoutConfirmedPossession()
        {
            yield return SceneManager.LoadSceneAsync("Soccer4v4", LoadSceneMode.Single);
            yield return null;

            var environment = Object.FindFirstObjectByType<SoccerEnvController>();
            Assert.IsNotNull(environment);
            Assert.IsNull(environment.PossessionTeam);
            yield return new WaitForFixedUpdate();
            foreach (var item in environment.AgentsList)
            {
                item.Agent.enabled = false;
                item.Rb.linearVelocity = Vector3.zero;
                item.Rb.angularVelocity = Vector3.zero;
                item.Rb.isKinematic = true;
            }

            var blueAgents = environment.AgentsList
                .Where(item => item.Agent.Team == Team.Blue)
                .Select(item => item.Agent)
                .ToArray();
            blueAgents[0].transform.position = new Vector3(-40f, 0.5f, 30f);
            blueAgents[1].transform.position = blueAgents[0].transform.position;
            Physics.SyncTransforms();

            var rewardBeforeSample = environment.GetCumulativeReward(Team.Blue);
            yield return new WaitForSeconds(0.85f);
            var appliedPenalty = environment.GetCumulativeReward(Team.Blue) - rewardBeforeSample;

            Assert.That(appliedPenalty, Is.EqualTo(-0.0025f).Within(0.00001f));
            Assert.AreEqual(0f, environment.GetCumulativeReward(Team.Purple), 0.00001f);
        }

        [UnityTest]
        public IEnumerator GoalFreezesRoundThenRestoresEveryBodyToItsFixedKickoffPoint()
        {
            yield return SceneManager.LoadSceneAsync("Soccer4v4", LoadSceneMode.Single);
            yield return null;

            var environment = Object.FindFirstObjectByType<SoccerEnvController>();
            environment.goalResetDelaySeconds = 0.05f;
            var startingBallPosition = environment.ball.transform.position;
            var startingPlayerPositions = environment.AgentsList.Select(item => item.Agent.transform.position).ToArray();

            environment.ball.transform.position += Vector3.right * 5f;
            environment.AgentsList[0].Agent.transform.position += Vector3.forward * 4f;
            environment.GoalTouched(Team.Blue);

            Assert.AreEqual(1, environment.BlueScore);
            Assert.AreEqual(0, environment.PurpleScore);
            Assert.AreEqual(SoccerMatchState.GoalPause, environment.State);
            Assert.IsTrue(environment.ballRb.isKinematic);

            yield return new WaitForSeconds(0.08f);

            Assert.AreEqual(SoccerMatchState.Playing, environment.State);
            Assert.IsFalse(environment.ballRb.isKinematic);
            Assert.That(Vector3.Distance(startingBallPosition, environment.ball.transform.position), Is.LessThan(0.001f));
            for (var index = 0; index < environment.AgentsList.Count; index++)
            {
                Assert.That(
                    Vector3.Distance(startingPlayerPositions[index], environment.AgentsList[index].Agent.transform.position),
                    Is.LessThan(0.25f),
                    $"Player {index + 1} was not restored near the fixed kick-off position before play resumed.");
            }
        }

        [UnityTest]
        public IEnumerator KickPlatesOpenOutwardRetractForHalfSecondAndFilterOnlyTheirOwnersRays()
        {
            yield return SceneManager.LoadSceneAsync("Soccer4v4", LoadSceneMode.Single);
            yield return null;

            var environment = Object.FindFirstObjectByType<SoccerEnvController>();
            var agents = environment.AgentsList.Select(item => item.Agent).ToArray();
            var plates = agents.Select(agent => agent.KickPlate).ToArray();
            Assert.That(plates, Has.All.Not.Null);
            Assert.AreEqual(8, plates.Select(plate => plate.gameObject.layer).Distinct().Count());

            foreach (var agent in agents)
            {
                var plate = agent.KickPlate;
                Assert.AreSame(agent, plate.Owner);
                var colliders = plate.GetComponentsInChildren<Collider>(true);
                Assert.AreEqual(3, colliders.Length);
                Assert.IsTrue(colliders.All(part => part.gameObject.layer == plate.gameObject.layer));
                Assert.IsTrue(colliders.All(part => part.CompareTag(agent.Team == Team.Blue ? "blueAgent" : "purpleAgent")));
                var leftWing = plate.transform.Find("KickPlateLeftWing");
                var rightWing = plate.transform.Find("KickPlateRightWing");
                Assert.Less(leftWing.localPosition.x, -0.6f);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(leftWing.localEulerAngles.y, 45f)), Is.LessThan(0.1f));
                Assert.Greater(rightWing.localPosition.x, 0.6f);
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(rightWing.localEulerAngles.y, -45f)), Is.LessThan(0.1f));

                foreach (var sensor in agent.GetComponentsInChildren<RayPerceptionSensorComponent3D>(true))
                {
                    var mask = (int)sensor.RayLayerMask;
                    Assert.AreEqual(0, mask & (1 << plate.gameObject.layer),
                        $"{agent.name} ray sensor should ignore its own plate layer.");
                    foreach (var otherPlate in plates.Where(other => other != plate))
                    {
                        Assert.AreNotEqual(0, mask & (1 << otherPlate.gameObject.layer),
                            $"{agent.name} ray sensor should include {otherPlate.Owner.name}'s plate layer.");
                    }
                }
            }

            var humanPlate = environment.HumanControlledAgent.KickPlate;
            var retractedPosition = humanPlate.transform.localPosition;
            Assert.IsTrue(humanPlate.TryKick());
            Assert.IsFalse(humanPlate.TryKick(), "A kick cannot be restarted while the plate is moving.");

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.Greater(humanPlate.transform.localPosition.z, retractedPosition.z + 0.2f);

            yield return new WaitForSeconds(0.22f);
            Assert.Greater(humanPlate.transform.localPosition.z, retractedPosition.z + 0.2f);
            Assert.IsFalse(humanPlate.CanKick, "The plate cannot kick again while its half-second retraction is running.");

            yield return new WaitForSeconds(humanPlate.RetractionSeconds + 0.05f);
            Assert.That(Vector3.Distance(humanPlate.transform.localPosition, retractedPosition), Is.LessThan(0.001f));
            Assert.IsTrue(humanPlate.CanKick);
            Assert.IsTrue(humanPlate.TryKick());
        }

        [UnityTest]
        public IEnumerator GoalBecomesTransparentOnlyWhileItOccludesAnOnScreenPlayer()
        {
            yield return SceneManager.LoadSceneAsync("Soccer4v4", LoadSceneMode.Single);
            yield return null;

            var environment = Object.FindFirstObjectByType<SoccerEnvController>();
            var mainCamera = Camera.main;
            var fader = mainCamera.GetComponent<SoccerGoalOcclusionFader>();
            Assert.IsNotNull(fader);
            Assert.AreSame(environment, fader.Environment);
            Assert.AreEqual(3, fader.BlueGoalRendererCount);
            Assert.AreEqual(3, fader.PurpleGoalRendererCount);

            var blueGoalRenderers = environment.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.CompareTag("blueGoal")
                    && renderer.gameObject.name.StartsWith("Goal", System.StringComparison.Ordinal))
                .OrderBy(renderer => renderer.gameObject.name)
                .ToArray();
            var originalGoalMaterials = blueGoalRenderers
                .Select(renderer => renderer.sharedMaterials.ToArray())
                .ToArray();
            var goalColliders = blueGoalRenderers.SelectMany(renderer => renderer.GetComponents<Collider>()).ToArray();
            var originalColliderStates = goalColliders.Select(collider => collider.enabled).ToArray();
            var originalPlayerMaterials = environment.AgentsList
                .SelectMany(item => item.Agent.GetComponentsInChildren<Renderer>(true))
                .Where(renderer => renderer.gameObject.name.StartsWith("AgentCube_", System.StringComparison.Ordinal))
                .SelectMany(renderer => renderer.sharedMaterials)
                .ToArray();

            var agents = environment.AgentsList.Select(item => item.Agent).ToArray();
            for (var index = 0; index < agents.Length; index++)
            {
                agents[index].transform.position = new Vector3(-14f + index * 4f, 0.5f, 0f);
                agents[index].agentRb.linearVelocity = Vector3.zero;
                agents[index].agentRb.angularVelocity = Vector3.zero;
            }

            Physics.SyncTransforms();
            var goalBounds = blueGoalRenderers[0].bounds;
            foreach (var renderer in blueGoalRenderers.Skip(1))
            {
                goalBounds.Encapsulate(renderer.bounds);
            }

            var targetAgent = agents[0];
            var bodyRenderer = targetAgent.GetComponentsInChildren<Renderer>(true)
                .Single(renderer => renderer.gameObject.name.StartsWith("AgentCube_", System.StringComparison.Ordinal));
            var bodyOffset = bodyRenderer.bounds.center - targetAgent.transform.position;
            var throughGoal = (goalBounds.center - mainCamera.transform.position).normalized;
            targetAgent.transform.position = goalBounds.center + throughGoal * 6f - bodyOffset;
            Physics.SyncTransforms();

            fader.RefreshOcclusion(true);
            Assert.IsTrue(fader.IsBlueGoalOccluding);
            Assert.IsFalse(fader.IsPurpleGoalOccluding);
            Assert.IsTrue(blueGoalRenderers.SelectMany(renderer => renderer.sharedMaterials)
                .Any(material => material.name.Contains("Goal Fade Runtime")));
            CollectionAssert.AreEqual(originalColliderStates, goalColliders.Select(collider => collider.enabled).ToArray());
            Assert.IsTrue(goalColliders.All(collider => collider.CompareTag("blueGoal")));
            CollectionAssert.AreEqual(originalPlayerMaterials, environment.AgentsList
                .SelectMany(item => item.Agent.GetComponentsInChildren<Renderer>(true))
                .Where(renderer => renderer.gameObject.name.StartsWith("AgentCube_", System.StringComparison.Ordinal))
                .SelectMany(renderer => renderer.sharedMaterials)
                .ToArray());

            for (var index = 0; index < agents.Length; index++)
            {
                agents[index].transform.position = mainCamera.transform.position
                    - mainCamera.transform.forward * (5f + index)
                    - bodyOffset;
            }
            Physics.SyncTransforms();
            fader.RefreshOcclusion(true);
            Assert.IsFalse(fader.IsBlueGoalOccluding);
            for (var index = 0; index < blueGoalRenderers.Length; index++)
            {
                CollectionAssert.AreEqual(originalGoalMaterials[index], blueGoalRenderers[index].sharedMaterials);
            }
        }
    }
}
