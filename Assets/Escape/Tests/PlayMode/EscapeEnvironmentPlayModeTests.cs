using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace MachineLearning.Escape.Tests
{
    public sealed class EscapeEnvironmentPlayModeTests
    {
        [UnityTest]
        public IEnumerator GeneratedEnvironmentResetsWithValidRandomizedState()
        {
            yield return SceneManager.LoadSceneAsync("EscapePrototype", LoadSceneMode.Single);
            yield return null;

            var environment = Object.FindFirstObjectByType<EscapeEnvironmentController>();
            Assert.IsNotNull(environment);
            environment.ResetEnvironment(12345);

            Assert.AreEqual(EscapeEpisodeState.Running, environment.State);
            Assert.AreEqual(25, environment.BuildingCount);
            Assert.AreEqual(36, environment.SpawnPointCount);
            Assert.AreEqual(5, environment.ActiveButtonCount);
            Assert.AreEqual(4, environment.GateAnchorCount);
            Assert.AreEqual(4, environment.LastSpawnIndices.Distinct().Count());
            Assert.That(environment.SelectedGateIndex, Is.InRange(0, 3));
        }

        [UnityTest]
        public IEnumerator OneHundredResetsKeepEpisodeRandomizationValid()
        {
            yield return SceneManager.LoadSceneAsync("EscapePrototype", LoadSceneMode.Single);
            yield return null;

            var environment = Object.FindFirstObjectByType<EscapeEnvironmentController>();
            Assert.IsNotNull(environment);

            for (var seed = 0; seed < 100; seed++)
            {
                environment.ResetEnvironment(100000 + seed);
                Assert.AreEqual(4, environment.LastSpawnIndices.Distinct().Count(), $"Duplicate character spawn at reset {seed}.");
                Assert.AreEqual(5, environment.LastButtonBuildingIndices.Distinct().Count(), $"Duplicate button building at reset {seed}.");
                Assert.That(environment.SelectedGateIndex, Is.InRange(0, 3), $"Invalid gate at reset {seed}.");
                Assert.AreEqual(0, environment.PressedButtonCount, $"Button count leaked at reset {seed}.");
                Assert.IsFalse(environment.IsGateActive, $"Gate state leaked at reset {seed}.");
                Assert.AreEqual(3, environment.Player.Health.CurrentHealth, $"Health leaked at reset {seed}.");
                Assert.IsTrue(environment.Buttons.All(button => !button.IsPressed), $"Button state leaked at reset {seed}.");
            }
        }

        [UnityTest]
        public IEnumerator ThreeButtonsActivateGateAndDamageHasInvulnerability()
        {
            yield return SceneManager.LoadSceneAsync("EscapePrototype", LoadSceneMode.Single);
            yield return null;

            var environment = Object.FindFirstObjectByType<EscapeEnvironmentController>();
            Assert.IsNotNull(environment);
            environment.ResetEnvironment(67890);

            environment.TryPressButton(environment.Buttons[0]);
            environment.TryPressButton(environment.Buttons[1]);
            environment.TryPressButton(environment.Buttons[2]);
            Assert.AreEqual(3, environment.PressedButtonCount);
            Assert.IsTrue(environment.IsGateActive);

            var health = environment.Player.Health;
            Assert.IsTrue(health.TryDamage(out var died));
            Assert.IsFalse(died);
            Assert.AreEqual(2, health.CurrentHealth);
            Assert.IsFalse(health.TryDamage(out _));
            Assert.AreEqual(2, health.CurrentHealth);
        }
    }
}
