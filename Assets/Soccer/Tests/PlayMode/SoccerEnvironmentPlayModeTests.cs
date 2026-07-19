using System.Collections;
using System.Linq;
using NUnit.Framework;
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
    }
}
