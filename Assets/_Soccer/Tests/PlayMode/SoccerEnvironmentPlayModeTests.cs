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
        float previousTimeScale;
        [SetUp] public void ResetTestTimeScale() { previousTimeScale=Time.timeScale;Time.timeScale=1f; }
        [TearDown] public void RestoreTestTimeScale() { Time.timeScale=previousTimeScale; }
        [UnityTest]
        public IEnumerator ClearanceOverridesIdleButStagnationDoesNotForAllRolesAndTeams()
        {
            yield return SceneManager.LoadSceneAsync("Stadium4v4_Base", LoadSceneMode.Single);
            yield return null;
            var environment=Object.FindFirstObjectByType<SoccerEnvController>();
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            var engine=typeof(SoccerEnvController).GetField("m_RewardEngine",flags).GetValue(environment);
            var carrier=engine.GetType().GetField("m_BallCarrier",flags);
            foreach(var item in environment.AgentsList)
            {
                var actor=item.Agent;actor.OnEpisodeBegin();
                var sign=SoccerDefensiveClearanceRules.GetAttackSign(actor.Team);
                var teammate=environment.AgentsList.Select(x=>x.Agent).First(x=>x.Team==actor.Team&&x!=actor);
                actor.transform.position=new Vector3(0,.52f,0);teammate.transform.position=new Vector3(sign*10,.52f,0);
                environment.ball.transform.position=new Vector3(sign, .58f,0);carrier.SetValue(engine,actor);
                actor.OnActionReceived(new Unity.MLAgents.Actuators.ActionBuffers(Unity.MLAgents.Actuators.ActionSegment<float>.Empty,new Unity.MLAgents.Actuators.ActionSegment<int>(new int[4])));
                Assert.AreEqual(0,actor.CommonRuleAttempts,$"No idle override {actor.Team}/{actor.PositionRole}");
                actor.OnEpisodeBegin();actor.transform.position=new Vector3(-sign*56,.52f,0);
                environment.ball.transform.position=new Vector3(-sign*55,.58f,0);teammate.transform.position=new Vector3(-sign*45,.52f,0);
                Assert.IsTrue(environment.TryGetCommonPossessionKick(actor,out var target,out var kick));
                Assert.Greater((target.x-environment.ball.transform.position.x)*sign,0,"Defensive kick must move away from own goal");
                Assert.That(kick,Is.InRange(1,2));
            }
        }
        [UnityTest]
        public IEnumerator GeneratedSceneStartsAsDeterministicAIControlledFourVersusFourMatch()
        {
            yield return SceneManager.LoadSceneAsync("Stadium4v4_Base", LoadSceneMode.Single);
            yield return null;

            var environment = Object.FindFirstObjectByType<SoccerEnvController>();
            Assert.IsNotNull(environment);
            Assert.AreEqual(8, environment.AgentsList.Count);
            Assert.AreEqual(4, environment.AgentsList.Count(item => item.Agent.Team == Team.Red));
            Assert.AreEqual(4, environment.AgentsList.Count(item => item.Agent.Team == Team.Navy));
            foreach (var team in new[] { Team.Red, Team.Navy })
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
            var settings = Object.FindFirstObjectByType<SoccerSettings>();
            Assert.IsNotNull(settings);
            Assert.AreEqual(2.2f, settings.agentRunSpeed, 0.000001f);
            Assert.AreEqual(9f, settings.maximumPlanarSpeed, 0.000001f);
            Assert.AreEqual(125f, settings.rotationSpeed, 0.000001f);
            Assert.AreEqual(137.5f, settings.humanAcceleration, 0.000001f);
            Assert.AreEqual(Vector3.one * 0.012705f, environment.ball.transform.localScale);
            var ball = environment.ball.GetComponent<SoccerBallController>();
            Assert.AreEqual(ball.LockedCenterHeight, environment.ball.transform.localPosition.y, 0.001f);
            Assert.IsTrue(ball.EnforcesStadiumPlanarMotion);
            var goalRenderers = environment.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.name.StartsWith("SM_S_Gate"))
                .ToArray();
            Assert.AreEqual(2, goalRenderers.Length);
            Assert.IsNotNull(environment.ArenaGeometry);
            Assert.AreEqual(SoccerArenaGeometry.StadiumHalfLength, environment.ArenaGeometry.HalfLength, 0.001f);
            Assert.AreEqual(SoccerArenaGeometry.StadiumHalfWidth, environment.ArenaGeometry.HalfWidth, 0.01f);
            Assert.AreEqual(2000f, environment.ControlledKickPower, 0.001f);
            Assert.AreEqual(5000f, environment.StrongKickPower, 0.001f);
            foreach (var item in environment.AgentsList)
            {
                var sensors = item.Agent.GetComponentsInChildren<RayPerceptionSensorComponent3D>(true);
                Assert.AreEqual(2, sensors.Length);
                Assert.AreEqual(1, sensors.Count(sensor => sensor.SensorName.EndsWith("Reverse")));
            }
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
            yield return SceneManager.LoadSceneAsync("Stadium4v4_Base", LoadSceneMode.Single);
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

            var redAgents = environment.AgentsList
                .Where(item => item.Agent.Team == Team.Red)
                .Select(item => item.Agent)
                .ToArray();
            redAgents[0].transform.position = new Vector3(-40f, 0.5f, 30f);
            redAgents[1].transform.position = redAgents[0].transform.position;
            Physics.SyncTransforms();

            var rewardBeforeSample = environment.GetCumulativeReward(Team.Red);
            yield return new WaitForSeconds(0.85f);
            var appliedPenalty = environment.GetCumulativeReward(Team.Red) - rewardBeforeSample;

            Assert.That(appliedPenalty, Is.EqualTo(-0.0025f).Within(0.00001f));
            Assert.AreEqual(0f, environment.GetCumulativeReward(Team.Navy), 0.00001f);
        }

        [UnityTest]
        public IEnumerator StrongKickAndUnsafeKickPenaltiesUsePossessionAndMatchCaps()
        {
            yield return SceneManager.LoadSceneAsync("Stadium4v4_Base", LoadSceneMode.Single);
            yield return null;

            var environment = Object.FindFirstObjectByType<SoccerEnvController>();
            var rewardEngine = Object.FindFirstObjectByType<SoccerRewardEngine>();
            Assert.IsNotNull(environment);
            Assert.IsNotNull(rewardEngine);
            foreach (var item in environment.AgentsList)
            {
                item.Agent.enabled = false;
                item.Rb.linearVelocity = Vector3.zero;
                item.Rb.angularVelocity = Vector3.zero;
            }

            rewardEngine.ResetPossession();
            rewardEngine.ResetMatch();
            var agent = environment.AgentsList.First(item => item.Agent.Team == Team.Red).Agent;
            environment.ball.transform.position = Vector3.zero;
            Physics.SyncTransforms();

            for (var kick = 0; kick < 10; kick++)
            {
                environment.NotifyBallStrike(agent, 2, Vector3.left, false);
            }

            Assert.AreEqual(-0.015f, environment.GetCumulativeReward(Team.Red), 0.000001f,
                "무의미한 Strong Kick은 소유권당 -0.015에서 멈춰야 함");

            for (var kick = 0; kick < 10; kick++)
            {
                environment.NotifyBallStrike(agent, 1, Vector3.right, true);
            }

            Assert.AreEqual(-0.1f, environment.GetCumulativeReward(Team.Red), 0.000001f,
                "Strong/unsafe 행동 패널티 합은 경기당 -0.1에서 멈춰야 함");
            Assert.AreEqual(0f, environment.GetCumulativeReward(Team.Navy), 0.000001f);
        }

        [UnityTest]
        public IEnumerator GoalFreezesRoundThenRestoresEveryBodyToItsFixedKickoffPoint()
        {
            yield return SceneManager.LoadSceneAsync("Stadium4v4_Base", LoadSceneMode.Single);
            yield return null;

            var environment = Object.FindFirstObjectByType<SoccerEnvController>();
            environment.goalResetDelaySeconds = 0.05f;
            var startingBallPosition = environment.ball.transform.position;
            var startingPlayerPositions = environment.AgentsList.Select(item => item.Agent.transform.position).ToArray();

            environment.ball.transform.position += Vector3.right * 5f;
            environment.AgentsList[0].Agent.transform.position += Vector3.forward * 4f;
            environment.GoalTouched(Team.Red);

            Assert.AreEqual(1, environment.RedScore);
            Assert.AreEqual(0, environment.NavyScore);
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
            yield return SceneManager.LoadSceneAsync("Stadium4v4_Base", LoadSceneMode.Single);
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
                Assert.IsTrue(colliders.All(part => part.CompareTag(agent.Team == Team.Red ? "redAgent" : "navyAgent")));
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
            yield return SceneManager.LoadSceneAsync("Stadium4v4_Base", LoadSceneMode.Single);
            yield return null;

            var environment = Object.FindFirstObjectByType<SoccerEnvController>();
            var mainCamera = Camera.main;
            mainCamera.GetComponent<SoccerPlayerCamera>().enabled = false;
            var fader = mainCamera.GetComponent<SoccerGoalOcclusionFader>();
            Assert.IsNotNull(fader);
            Assert.AreSame(environment, fader.Environment);
            Assert.AreEqual(1, fader.RedGoalRendererCount);
            Assert.AreEqual(1, fader.NavyGoalRendererCount);

            var redGoalRenderers = environment.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.CompareTag("redGoal")
                    && renderer.gameObject.name.StartsWith("SM_S_Gate", System.StringComparison.Ordinal))
                .OrderBy(renderer => renderer.gameObject.name)
                .ToArray();
            var originalGoalMaterials = redGoalRenderers
                .Select(renderer => renderer.sharedMaterials.ToArray())
                .ToArray();
            var goalColliders = redGoalRenderers.SelectMany(renderer => renderer.GetComponents<Collider>()).ToArray();
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

            var targetAgent = agents[0];
            var bodyRenderer = targetAgent.GetComponentsInChildren<Renderer>(true)
                .Single(renderer => renderer.gameObject.name.StartsWith("AgentCube_", System.StringComparison.Ordinal));
            var bodyOffset = bodyRenderer.bounds.center - targetAgent.transform.position;
            var arena = environment.ArenaGeometry;
            var insideRedGoal = new Vector3(-(arena.HalfLength + arena.GoalDepth * 0.5f), 0.5f, 0f);
            targetAgent.transform.position = insideRedGoal;
            mainCamera.transform.position = insideRedGoal - Vector3.right * 20f + Vector3.up * 2f;
            mainCamera.transform.LookAt(insideRedGoal + bodyOffset);
            Physics.SyncTransforms();

            Assert.IsTrue(arena.ContainsGoalInterior(Team.Red, bodyRenderer.bounds.center));
            fader.RefreshOcclusion(true);
            Assert.IsTrue(fader.IsRedGoalOccluding);
            Assert.IsFalse(fader.IsNavyGoalOccluding);
            Assert.IsTrue(redGoalRenderers.SelectMany(renderer => renderer.sharedMaterials)
                .Any(material => material.name.Contains("Goal Fade Runtime")));
            CollectionAssert.AreEqual(originalColliderStates, goalColliders.Select(collider => collider.enabled).ToArray());
            Assert.IsTrue(goalColliders.All(collider => collider.CompareTag("redGoal")));
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
            Assert.IsFalse(fader.IsRedGoalOccluding);
            for (var index = 0; index < redGoalRenderers.Length; index++)
            {
                CollectionAssert.AreEqual(originalGoalMaterials[index], redGoalRenderers[index].sharedMaterials);
            }
        }
    }
}
