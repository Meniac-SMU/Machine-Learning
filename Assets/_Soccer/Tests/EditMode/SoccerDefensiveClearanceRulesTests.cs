using NUnit.Framework;
using UnityEngine;

namespace MachineLearning.Soccer.Tests
{
    public sealed class SoccerDefensiveClearanceRulesTests
    {
        const float Tolerance = 0.0001f;

        [Test]
        public void OwnGoalPredictionIsMirroredForRedAndNavy()
        {
            foreach (var team in new[] { Team.Red, Team.Navy })
            {
                var attackSign = SoccerDefensiveClearanceRules.GetAttackSign(team);
                var ballPosition = new Vector3(-attackSign * 50f, 0.5f, 2f);

                Assert.IsTrue(SoccerDefensiveClearanceRules.PredictsOwnGoal(
                    team,
                    ballPosition,
                    Vector3.left * attackSign));
                Assert.IsFalse(SoccerDefensiveClearanceRules.PredictsOwnGoal(
                    team,
                    ballPosition,
                    Vector3.right * attackSign));
            }
        }

        [Test]
        public void UnsafeGoalMouthKickIsRedirectedTowardFieldCenterWithControlledDirection()
        {
            foreach (var team in new[] { Team.Red, Team.Navy })
            {
                var attackSign = SoccerDefensiveClearanceRules.GetAttackSign(team);
                var ballPosition = new Vector3(-attackSign * 58f, 0.5f, 8f);
                var direction = SoccerDefensiveClearanceRules.ResolveKickDirection(
                    team,
                    ballPosition,
                    Vector3.forward,
                    out var redirected);
                var expected = SoccerDefensiveClearanceRules.GetSafeClearanceDirection(team, ballPosition);

                Assert.IsTrue(redirected);
                Assert.AreEqual(expected.x, direction.x, Tolerance);
                Assert.AreEqual(expected.z, direction.z, Tolerance);
                Assert.Greater(direction.x * attackSign, 0f);
                Assert.Less(direction.z * ballPosition.z, 0f);
            }
        }

        [Test]
        public void BallInsideGoalCanBeClearedOutButCannotBeKickedDeeperIntoOwnGoal()
        {
            foreach (var team in new[] { Team.Red, Team.Navy })
            {
                var attackSign = SoccerDefensiveClearanceRules.GetAttackSign(team);
                var ballPosition = new Vector3(-attackSign * 64f, 0.5f, 0f);
                var towardField = Vector3.right * attackSign;
                var deeperIntoGoal = Vector3.left * attackSign;

                Assert.IsFalse(SoccerDefensiveClearanceRules.PredictsOwnGoal(team, ballPosition, towardField));
                Assert.IsTrue(SoccerDefensiveClearanceRules.PredictsOwnGoal(team, ballPosition, deeperIntoGoal));
                var resolved = SoccerDefensiveClearanceRules.ResolveKickDirection(
                    team,
                    ballPosition,
                    deeperIntoGoal,
                    out var redirected);
                Assert.IsTrue(redirected);
                Assert.Greater(resolved.x * attackSign, 0f);
            }
        }

        [Test]
        public void BallPastGoalLineOutsideThePostsIsNotMisclassifiedAsAnOwnGoalKick()
        {
            foreach (var team in new[] { Team.Red, Team.Navy })
            {
                var attackSign = SoccerDefensiveClearanceRules.GetAttackSign(team);
                var ballPosition = new Vector3(-attackSign * 64f, 0.5f, 25f);
                var deeperDirection = Vector3.left * attackSign;
                var resolved = SoccerDefensiveClearanceRules.ResolveKickDirection(
                    team,
                    ballPosition,
                    deeperDirection,
                    out var redirected);

                Assert.IsFalse(SoccerDefensiveClearanceRules.PredictsOwnGoal(
                    team,
                    ballPosition,
                    deeperDirection));
                Assert.IsFalse(redirected);
                Assert.AreEqual(deeperDirection.x, resolved.x, Tolerance);
            }
        }

        [Test]
        public void ExistingGoalwardVelocityIsIncludedAndCancelledWhenControlledKickCannotReverseIt()
        {
            foreach (var team in new[] { Team.Red, Team.Navy })
            {
                var attackSign = SoccerDefensiveClearanceRules.GetAttackSign(team);
                var ballPosition = new Vector3(-attackSign * 54f, 0.5f, 0f);
                var requestedDirection = Vector3.right * attackSign;
                var currentVelocity = Vector3.left * (attackSign * 20f);
                var resolvedDirection = SoccerDefensiveClearanceRules.ResolveKickDirection(
                    team,
                    ballPosition,
                    requestedDirection,
                    currentVelocity,
                    AgentSoccer.ControlledKickPower,
                    3f,
                    0.02f,
                    out var redirected);

                Assert.IsTrue(redirected,
                    $"{team} must use the post-force velocity, not only the requested direction.");
                var stillUnsafeVelocity = SoccerDefensiveClearanceRules.PredictPostStrikeVelocity(
                    currentVelocity,
                    resolvedDirection,
                    AgentSoccer.ControlledKickPower,
                    3f,
                    0.02f);
                Assert.IsTrue(SoccerDefensiveClearanceRules.PredictsOwnGoal(
                    team,
                    ballPosition,
                    stillUnsafeVelocity));

                var cancelledVelocity = SoccerDefensiveClearanceRules.RemoveOwnGoalwardVelocity(
                    team,
                    currentVelocity);
                var safeVelocity = SoccerDefensiveClearanceRules.PredictPostStrikeVelocity(
                    cancelledVelocity,
                    resolvedDirection,
                    AgentSoccer.ControlledKickPower,
                    3f,
                    0.02f);
                Assert.IsFalse(SoccerDefensiveClearanceRules.PredictsOwnGoal(
                    team,
                    ballPosition,
                    safeVelocity));
                Assert.Greater(safeVelocity.x * attackSign, 0f);
            }
        }

        [Test]
        public void StrongKickIsMeaningfulOnlyForClearancePassLaneOrRealShot()
        {
            foreach (var team in new[] { Team.Red, Team.Navy })
            {
                var attackSign = SoccerDefensiveClearanceRules.GetAttackSign(team);
                var attackDirection = Vector3.right * attackSign;
                Assert.IsTrue(SoccerDefensiveClearanceRules.IsMeaningfulStrongKick(
                    team,
                    new Vector3(-attackSign * 30f, 0.5f, 0f),
                    attackDirection,
                    false));
                Assert.IsTrue(SoccerDefensiveClearanceRules.IsMeaningfulStrongKick(
                    team,
                    Vector3.zero,
                    attackDirection,
                    true));
                Assert.IsTrue(SoccerDefensiveClearanceRules.IsMeaningfulStrongKick(
                    team,
                    new Vector3(attackSign * 40f, 0.5f, 0f),
                    attackDirection,
                    false));
                Assert.IsFalse(SoccerDefensiveClearanceRules.IsMeaningfulStrongKick(
                    team,
                    new Vector3(attackSign * 30f, 0.5f, 0f),
                    attackDirection,
                    false));
                Assert.IsFalse(SoccerDefensiveClearanceRules.IsMeaningfulStrongKick(
                    team,
                    Vector3.zero,
                    attackDirection,
                    false));
            }
        }
    }
}
