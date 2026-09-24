using NUnit.Framework;
using UnityEngine;

namespace MachineLearning.Soccer.Manager.Tests
{
    public sealed class MNG_MSContractTests
    {
        [TestCase(MNG_MSOpponentStrength.Rescue, 0.20f, 2f)]
        [TestCase(MNG_MSOpponentStrength.Easy, 0.35f, 1.5f)]
        [TestCase(MNG_MSOpponentStrength.Medium, 0.60f, 1f)]
        [TestCase(MNG_MSOpponentStrength.Full, 1f, 0.5f)]
        public void OpponentProfile_UsesFrozenManagerSimpleContract(
            MNG_MSOpponentStrength strength,
            float movement,
            float interval)
        {
            var profile = ScriptableObject.CreateInstance<MNG_MSOpponentProfile>();
            try
            {
                profile.Configure(strength, movement, interval);
                Assert.DoesNotThrow(profile.ValidateOrThrow);
                Assert.That(profile.MovementSpeedMultiplier, Is.EqualTo(movement));
                Assert.That(profile.DecisionIntervalSeconds, Is.EqualTo(interval));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void FullOpponentProfile_IsExactlyCurrentR0Contract()
        {
            var profile = ScriptableObject.CreateInstance<MNG_MSOpponentProfile>();
            try
            {
                profile.Configure(
                    MNG_MSOpponentStrength.Full,
                    1f,
                    MNG_RuleBasedManager.DecisionIntervalSeconds);
                Assert.That(profile.MovementSpeedMultiplier, Is.EqualTo(1f));
                Assert.That(profile.DecisionIntervalSeconds,
                    Is.EqualTo(MNG_RuleBasedManager.DecisionIntervalSeconds));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void SelfPlayTerminalRewards_HaveExactWinDrawLossSigns()
        {
            MNG_RewardEngine.GetSelfPlayTerminalRewards(2, 1, out var redWin, out var navyLoss);
            MNG_RewardEngine.GetSelfPlayTerminalRewards(1, 1, out var redDraw, out var navyDraw);
            MNG_RewardEngine.GetSelfPlayTerminalRewards(0, 3, out var redLoss, out var navyWin);
            Assert.That((redWin, navyLoss), Is.EqualTo((0.5f, -0.5f)));
            Assert.That((redDraw, navyDraw), Is.EqualTo((0f, 0f)));
            Assert.That((redLoss, navyWin), Is.EqualTo((-0.5f, 0.5f)));
        }

        [Test]
        public void WorkerIdentityAndSpawnSeed_AreIndependentByTrainerPort()
        {
            var workerA = MNG_MSController.ResolveWorkerIdentity(
                new[] { "player.exe", "--mlagents-port", "5705" });
            var workerB = MNG_MSController.ResolveWorkerIdentity(
                new[] { "player.exe", "--mlagents-port", "5706" });
            Assert.That(workerA, Is.EqualTo(5705));
            Assert.That(workerB, Is.EqualTo(5706));
            Assert.That(MNG_MatchController.CalculateSpawnSeed(2, 0, workerA),
                Is.Not.EqualTo(MNG_MatchController.CalculateSpawnSeed(2, 0, workerB)));
        }

        [Test]
        public void PlayerMotor_RejectsInvalidMSMovementMultipliers()
        {
            var gameObject = new GameObject("MS motor contract");
            try
            {
                gameObject.AddComponent<Rigidbody>();
                gameObject.AddComponent<MNG_PlayerAvatar>();
                var motor = gameObject.AddComponent<MNG_PlayerMotor>();
                Assert.DoesNotThrow(() => motor.ConfigureSpeedMultiplier(0.20f));
                Assert.That(motor.SpeedMultiplier, Is.EqualTo(0.20f));
                Assert.Throws<System.ArgumentOutOfRangeException>(
                    () => motor.ConfigureSpeedMultiplier(0f));
                Assert.Throws<System.ArgumentOutOfRangeException>(
                    () => motor.ConfigureSpeedMultiplier(1.01f));
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
