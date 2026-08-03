using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.MLAgents.Sensors;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MachineLearning.Soccer.Tests
{
    public sealed class SoccerEnvironmentPlayModeTests
    {
        [UnityTest]
        public IEnumerator GeneratedSceneStartsAsDeterministicHumanControlledFourVersusFourMatch()
        {
            yield return SceneManager.LoadSceneAsync("Soccer4v4", LoadSceneMode.Single);
            yield return null;

            var environment = Object.FindFirstObjectByType<SoccerEnvController>();
            Assert.IsNotNull(environment);
            Assert.AreEqual(8, environment.AgentsList.Count);
            Assert.AreEqual(4, environment.AgentsList.Count(item => item.Agent.Team == Team.Blue));
            Assert.AreEqual(4, environment.AgentsList.Count(item => item.Agent.Team == Team.Purple));
            Assert.IsNotNull(environment.HumanControlledAgent);
            Assert.IsTrue(environment.HumanControlledAgent.IsUsingHumanInput);
            Assert.IsFalse(environment.IsAIEnabled);
            Assert.AreEqual(SoccerMatchState.Playing, environment.State);
            Assert.That(environment.RemainingTime, Is.InRange(179f, 180f));
            Assert.IsFalse(
                Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Any(renderer => renderer.sharedMaterials.Length == 0 || renderer.sharedMaterials.Any(material => material == null)),
                "The generated Soccer scene must not contain renderers with missing materials.");

            var playerCamera = Object.FindFirstObjectByType<SoccerPlayerCamera>();
            Assert.IsNotNull(playerCamera);
            Assert.IsTrue(playerCamera.IsFollowingHuman);

            environment.SetAIEnabled(true);
            yield return null;
            Assert.IsFalse(playerCamera.IsFollowingHuman);

            environment.SetAIEnabled(false);
            yield return null;
            Assert.IsTrue(playerCamera.IsFollowingHuman);
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
    }
}
