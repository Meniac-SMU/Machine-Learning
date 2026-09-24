using NUnit.Framework;
using UnityEngine;

namespace MachineLearning.Soccer.Manager.Tests
{
    public sealed class MNG_FrontPushContractTests
    {
        [Test]
        public void PushSetup_IsBehindBallRelativeToDestination()
        {
            var ball = Vector2.zero;
            var destination = new Vector2(10f, 0f);

            var setup = MNG_PlayerSkillExecutor.CalculateBallPushSetup(
                ball,
                destination);

            Assert.That(setup.x, Is.EqualTo(
                -MNG_PlayerSkillExecutor.BallPushSetupDistance).Within(0.0001f));
            Assert.That(setup.y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(MNG_PlayerSkillExecutor.IsBehindBallForPush(
                setup,
                ball,
                destination), Is.True);
        }

        [Test]
        public void OrbitWaypoint_KeepsPlayerOutsideBallBeforeReachingSetup()
        {
            var ball = Vector2.zero;
            var destination = new Vector2(10f, 0f);
            var playerInFront = new Vector2(1.25f, 0f);

            var waypoint = MNG_PlayerSkillExecutor.CalculateBallOrbitWaypoint(
                playerInFront,
                ball,
                destination);

            Assert.That(Vector2.Distance(waypoint, ball), Is.EqualTo(
                MNG_PlayerSkillExecutor.BallOrbitRadius).Within(0.0001f));
            Assert.That(Mathf.Abs(waypoint.y), Is.GreaterThan(0.5f));
            Assert.That(waypoint, Is.Not.EqualTo(ball));
        }

        [Test]
        public void BehindPlayer_UsesFinalPushSetupInsteadOfCrossingBall()
        {
            var ball = new Vector2(3f, -2f);
            var destination = new Vector2(3f, 10f);
            var setup = MNG_PlayerSkillExecutor.CalculateBallPushSetup(
                ball,
                destination);

            Assert.That(MNG_PlayerSkillExecutor.IsBehindBallForPush(
                setup,
                ball,
                destination), Is.True);
            Assert.That(Vector2.Distance(setup, ball), Is.EqualTo(
                MNG_PlayerSkillExecutor.BallPushSetupDistance).Within(0.0001f));
        }

        [Test]
        public void KeeperActivityLimits_AreExactlyTwiceThePreviousContract()
        {
            Assert.That(MNG_TeamPlanner.KeeperActivityRadiusMultiplier, Is.EqualTo(2f));
            Assert.That(MNG_TeamPlanner.KeeperClaimDepth,
                Is.EqualTo(MNG_TeamPlanner.OriginalKeeperClaimDepth * 2f));
            Assert.That(MNG_TeamPlanner.KeeperClaimLateralMargin,
                Is.EqualTo(MNG_TeamPlanner.OriginalKeeperClaimLateralMargin * 2f));
            Assert.That(MNG_TeamPlanner.KeeperClaimMaximumAdvance,
                Is.EqualTo(MNG_TeamPlanner.OriginalKeeperClaimMaximumAdvance * 2f));
            Assert.That(MNG_TeamPlanner.KeeperBlockDepth,
                Is.EqualTo(MNG_TeamPlanner.OriginalKeeperBlockDepth * 2f));
            Assert.That(MNG_TeamPlanner.KeeperBlockLateralMargin,
                Is.EqualTo(MNG_TeamPlanner.OriginalKeeperBlockLateralMargin * 2f));
            Assert.That(MNG_TeamPlanner.KeeperBlockMaximumAdvance,
                Is.EqualTo(MNG_TeamPlanner.OriginalKeeperBlockMaximumAdvance * 2f));
        }

        [Test]
        public void FrontPush_IsReservedForBallRecoveryAndPossessionSkills()
        {
            Assert.That(MNG_PlayerSkillExecutor.UsesFrontPushForSkill(
                MNG_PlayerSkill.Carry), Is.True);
            Assert.That(MNG_PlayerSkillExecutor.UsesFrontPushForSkill(
                MNG_PlayerSkill.Press), Is.True);
            Assert.That(MNG_PlayerSkillExecutor.UsesFrontPushForSkill(
                MNG_PlayerSkill.ReceivePass), Is.False, "Reception must face the incoming ball, not orbit behind it for a carry.");
            Assert.That(MNG_PlayerSkillExecutor.UsesFrontPushForSkill(
                MNG_PlayerSkill.Cover), Is.False);
            Assert.That(MNG_PlayerSkillExecutor.UsesFrontPushForSkill(
                MNG_PlayerSkill.Mark), Is.False);
            Assert.That(MNG_PlayerSkillExecutor.UsesFrontPushForSkill(
                MNG_PlayerSkill.SupportRun), Is.False);
            Assert.That(MNG_PlayerSkillExecutor.UsesFrontPushForSkill(
                MNG_PlayerSkill.KeeperClaim), Is.True);
        }

        [TestCase(Team.Red, -20f, -35f, -14f)]
        [TestCase(Team.Navy, 20f, 35f, 14f)]
        public void CarryTarget_NeverRetreatsTowardOwnGoal(
            Team team,
            float ballX,
            float requestedX,
            float expectedX)
        {
            var corrected = MNG_TeamPlanner.EnforceAttackingCarryTarget(
                new Vector2(ballX, 2f),
                new Vector2(requestedX, 2f),
                team,
                55f,
                35f);

            Assert.That(corrected.x, Is.EqualTo(expectedX).Within(0.0001f));
        }

        [Test]
        public void PlannerVersionTen_PreservesCoordinatedRolesWithTeamRelativeLanes()
        {
            Assert.That(MNG_TeamPlanner.PlannerVersion, Is.EqualTo(10));
            Assert.That(MNG_TeamPlanner.MinimumFormationTargetSeparation, Is.EqualTo(6f));
            Assert.That(MNG_TeamPlanner.KeeperPassLaneClearance, Is.GreaterThanOrEqualTo(3f));
            Assert.That(MNG_GlobalBallStallTracker.TriggerSeconds, Is.GreaterThan(1f));
            Assert.That(MNG_TeamPlanner.MinimumDefensivePressers, Is.EqualTo(2));
            Assert.That(MNG_TeamPlanner.MinimumAttackingParticipants, Is.EqualTo(2));
            Assert.That(MNG_TeamPlanner.OpponentGoalAttackDepth, Is.GreaterThan(20f));
            Assert.That(MNG_TeamPlanner.OwnGoalThreatDepth, Is.GreaterThan(20f));
            Assert.That(MNG_TeamPlanner.StallShotAttemptSeconds, Is.GreaterThan(2f));
            Assert.That(MNG_TeamPlanner.StallShotMaximumStartDistance, Is.GreaterThan(5f));
            Assert.That(MNG_PlayerSkillExecutor.FieldPlayerPersonalSpaceRadius,
                Is.GreaterThanOrEqualTo(7f));
            Assert.That(MNG_PlayerSkillExecutor.FieldPlayerSpacingStrength,
                Is.GreaterThanOrEqualTo(2.5f));
        }

        [Test]
        public void FieldPlayerSpacingTarget_PushesApartNearbyTeammates()
        {
            var snapshot = new MNG_MatchSnapshot();
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                snapshot.SetPlayer(Team.Red, slot, new MNG_PlayerState
                {
                    Active = true,
                    Position = new Vector2(0f, slot == 1 ? 0f : slot == 2 ? 1f : 12f),
                    Role = (MNG_PlayerRole)slot
                });
            }

            var target = MNG_PlayerSkillExecutor.CalculateFieldPlayerSpacingTarget(
                snapshot,
                Team.Red,
                1,
                Vector2.zero,
                new Vector2(10f, 0f));

            Assert.That(target.y, Is.LessThan(0f));
            Assert.That(Vector2.Distance(target, new Vector2(10f, 0f)),
                Is.GreaterThan(0f));
        }

        [Test]
        public void EmergencySeparation_PreservesCarrierAndPassReceiverPriority()
        {
            Assert.That(MNG_PlayerSkillExecutor.ShouldYieldForEmergencySeparation(
                2,
                MNG_PlayerSkill.SupportRun,
                false,
                3,
                MNG_PlayerSkill.Carry,
                true,
                false), Is.True);
            Assert.That(MNG_PlayerSkillExecutor.ShouldYieldForEmergencySeparation(
                3,
                MNG_PlayerSkill.Carry,
                true,
                2,
                MNG_PlayerSkill.SupportRun,
                false,
                false), Is.False);
            Assert.That(MNG_PlayerSkillExecutor.ShouldYieldForEmergencySeparation(
                1,
                MNG_PlayerSkill.Cover,
                false,
                2,
                MNG_PlayerSkill.ReceivePass,
                false,
                false), Is.True);
        }

        [Test]
        public void EmergencySeparation_EqualTasksChooseExactlyOneYieldingPlayer()
        {
            var firstYields = MNG_PlayerSkillExecutor.ShouldYieldForEmergencySeparation(
                1, MNG_PlayerSkill.SupportRun, false,
                2, MNG_PlayerSkill.SupportRun, false, false);
            var secondYields = MNG_PlayerSkillExecutor.ShouldYieldForEmergencySeparation(
                2, MNG_PlayerSkill.SupportRun, false,
                1, MNG_PlayerSkill.SupportRun, false, false);

            Assert.That(firstYields, Is.False);
            Assert.That(secondYields, Is.True);
            Assert.That(firstYields ^ secondYields, Is.True);
            Assert.That(MNG_PlayerSkillExecutor.CalculateEmergencySeparationDirection(
                Vector2.zero, Vector2.zero, 2, 1), Is.EqualTo(Vector2.up));
        }

        [Test]
        public void ContestedEscapeRoute_PrioritizesAttackForwardMovement()
        {
            Assert.That(
                MNG_BallControl.ContestedEscapeForwardDistance,
                Is.GreaterThan(MNG_BallControl.ContestedEscapeLateralDistance));
            var direction = new Vector2(
                MNG_BallControl.ContestedEscapeForwardDistance,
                MNG_BallControl.ContestedEscapeLateralDistance).normalized;
            Assert.That(Vector2.Dot(direction, Vector2.right), Is.GreaterThan(0.65f));
        }

        [Test]
        public void SideWallEscapeMovesForwardAndBackIntoTheField()
        {
            var direction = MNG_BallControl.CalculateBoundaryEscapeDirection(
                new Vector2(0f, 34f), Team.Red, 55f, 35f);

            Assert.That(direction.x, Is.GreaterThan(0f));
            Assert.That(direction.y, Is.LessThan(-0.5f));
        }

        [Test]
        public void OpponentCornerEscapePrioritizesReturningToPlayableSpace()
        {
            var direction = MNG_BallControl.CalculateBoundaryEscapeDirection(
                new Vector2(54f, 34f), Team.Red, 55f, 35f);

            Assert.That(direction.x, Is.LessThan(-0.5f));
            Assert.That(direction.y, Is.LessThan(-0.25f));
        }
    }
}
