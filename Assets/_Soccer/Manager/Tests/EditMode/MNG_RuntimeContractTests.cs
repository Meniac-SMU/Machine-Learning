using System;
using MachineLearning.Soccer;
using NUnit.Framework;
using UnityEngine;

namespace MachineLearning.Soccer.Manager.Tests
{
    public sealed class MNG_RuntimeContractTests
    {
        [Test]
        public void ContestedBallEscapeRequiresBothTeamsAndCyclesUntilBallLeaves()
        {
            var escape = new MNG_ContestedBallEscape();
            for (var step = 0; step < 40; step++)
                escape.Update(Vector2.zero, true, true, 0.02f);

            Assert.That(escape.IsActive, Is.True);
            Assert.That(escape.ActivationCount, Is.EqualTo(1));
            var firstSequence = escape.Sequence;

            for (var step = 0; step < 75; step++)
                escape.Update(Vector2.zero, true, true, 0.02f);
            Assert.That(escape.Sequence, Is.GreaterThan(firstSequence),
                "A continuing deadlock must flip the lateral escape route.");

            escape.Update(Vector2.right * MNG_ContestedBallEscape.EscapeDistance, true, true, 0.02f);
            Assert.That(escape.IsActive, Is.False,
                "The escape override must release as soon as the ball has been extracted.");

            for (var step = 0; step < 40; step++)
                escape.Update(Vector2.zero, true, false, 0.02f);
            Assert.That(escape.IsActive, Is.False,
                "Ordinary one-team possession must never trigger contested escape.");
        }

        [Test]
        public void GlobalBallStallTrackerTriggersWithoutPossessionAndRetargetsUntilReleased()
        {
            var tracker = new MNG_GlobalBallStallTracker();
            for (var step = 0; step < 59; step++)
                tracker.Update(Vector2.zero, Vector2.zero, 0.02f);

            Assert.That(tracker.IsActive, Is.False);
            tracker.Update(Vector2.zero, Vector2.zero, 0.02f);
            tracker.Update(Vector2.zero, Vector2.zero, 0.02f);
            Assert.That(tracker.IsActive, Is.True);
            Assert.That(tracker.ActivationCount, Is.EqualTo(1));
            var firstSequence = tracker.Sequence;

            for (var step = 0; step < 51; step++)
                tracker.Update(Vector2.zero, Vector2.zero, 0.02f);
            Assert.That(tracker.Sequence, Is.GreaterThan(firstSequence),
                "A continuing global stall must force a new set of approach directions.");

            tracker.Update(
                Vector2.zero,
                Vector2.right * MNG_GlobalBallStallTracker.ReleaseSpeed,
                0.02f);
            Assert.That(tracker.IsActive, Is.False,
                "A real clearance must immediately release the common recovery override.");
        }

        [Test]
        public void HudPresenter_FormatsCeilingMatchTime()
        {
            Assert.That(MNG_HudPresenter.FormatTime(300f), Is.EqualTo("05:00"));
            Assert.That(MNG_HudPresenter.FormatTime(0.01f), Is.EqualTo("00:01"));
            Assert.That(MNG_HudPresenter.FormatTime(-1f), Is.EqualTo("00:00"));
        }

        [Test]
        public void ObservationWriter_WritesExactly133FiniteValues()
        {
            Assert.That(MNG_ObservationWriter.BallSpeedScale,
                Is.EqualTo(MNG_KickSolver.MaximumBallSpeed));
            var observations = new float[MNG_ObservationWriter.ObservationSize];
            Assert.That(MNG_ObservationWriter.Write(CreateSnapshot(), Team.Red, CreateDecision(), observations), Is.EqualTo(133));
            Assert.That(observations, Has.Length.EqualTo(133));
            Assert.That(observations, Has.All.Matches<float>(value => !float.IsNaN(value) && !float.IsInfinity(value)));
        }

        [Test]
        public void ObservationWriter_RotatesNavyPerspectiveBy180Degrees()
        {
            var redSnapshot = CreateSnapshot();
            var navySnapshot = CreateMirroredAndTeamSwappedSnapshot(redSnapshot);
            var decision = CreateDecision();
            var red = new float[MNG_ObservationWriter.ObservationSize];
            var navy = new float[MNG_ObservationWriter.ObservationSize];

            MNG_ObservationWriter.Write(redSnapshot, Team.Red, decision, red);
            MNG_ObservationWriter.Write(navySnapshot, Team.Navy, decision, navy);

            Assert.That(navy, Is.EqualTo(red).Within(0.00001f));
        }

        [Test]
        public void ObservationWriter_InactiveSlotIsTwelveZeros()
        {
            var snapshot = CreateSnapshot();
            var player = snapshot.GetPlayer(Team.Red, 2);
            player.Active = false;
            player.Position = new Vector2(10f, 20f);
            snapshot.SetPlayer(Team.Red, 2, player);
            var observations = new float[MNG_ObservationWriter.ObservationSize];

            MNG_ObservationWriter.Write(snapshot, Team.Red, CreateDecision(), observations);

            for (var i = 24; i < 36; i++) Assert.That(observations[i], Is.Zero, $"observation {i}");
        }

        [Test]
        public void CommandMask_HumanCarrierCannotReceiveCarryPassOrShot()
        {
            var snapshot = CreateSnapshot();
            var striker = snapshot.GetPlayer(Team.Red, 3);
            striker.IsHuman = true;
            snapshot.SetPlayer(Team.Red, 3, striker);
            snapshot.Carrier = MNG_CarrierRef.For(Team.Red, 3);
            var decision = CreateDecision();
            decision.ControlMask[3] = false;
            var mask = new bool[MNG_CommandMask.CommandCount];

            MNG_CommandMask.Write(snapshot, Team.Red, decision, mask);

            Assert.That(mask[(int)MNG_Command.AdvanceCarry], Is.False);
            Assert.That(mask[(int)MNG_Command.PassBuild], Is.False);
            Assert.That(mask[(int)MNG_Command.AttemptShot], Is.False);
            Assert.That(mask[(int)MNG_Command.ActiveRecover], Is.True);
            Assert.That(mask[(int)MNG_Command.Balanced], Is.True);
            Assert.That(mask[(int)MNG_Command.ProtectBack], Is.True);
        }

        [Test]
        public void CommandMask_AiCarrierUsesTargetAndCooldownConditions()
        {
            var snapshot = CreateSnapshot();
            snapshot.Carrier = MNG_CarrierRef.For(Team.Red, 3);
            var decision = CreateDecision();
            var mask = new bool[MNG_CommandMask.CommandCount];

            MNG_CommandMask.Write(snapshot, Team.Red, decision, mask);
            Assert.That(mask, Is.EqualTo(new[] { true, true, true, true, true, true }));

            var striker = snapshot.GetPlayer(Team.Red, 3);
            striker.KickCooldownSeconds = 0.25f;
            snapshot.SetPlayer(Team.Red, 3, striker);
            MNG_CommandMask.Write(snapshot, Team.Red, decision, mask);
            Assert.That(mask[(int)MNG_Command.AdvanceCarry], Is.True);
            Assert.That(mask[(int)MNG_Command.PassBuild], Is.False);
            Assert.That(mask[(int)MNG_Command.AttemptShot], Is.False);
        }

        [Test]
        public void R0RuleManager_SelectsAllSixCommandsFromSimpleMatchSituations()
        {
            var snapshot = CreateSnapshot();
            var decision = CreateDecision();
            decision.CommandAgeSeconds = 2f;
            decision.SecondsSincePossessionLoss = -1f;

            SetCarrier(snapshot, Team.Red, 3, Vector2.zero);
            MoveTeamFarFromPoint(snapshot, Team.Navy, new Vector2(35f, 20f));
            Assert.That(MNG_RuleBasedManager.Decide(snapshot, Team.Red, decision).Command,
                Is.EqualTo(MNG_Command.AdvanceCarry));

            var openReceiver = snapshot.GetPlayer(Team.Red, 1);
            openReceiver.Position = new Vector2(8f, 12f);
            snapshot.SetPlayer(Team.Red, 1, openReceiver);
            var pressingOpponent = snapshot.GetPlayer(Team.Navy, 3);
            pressingOpponent.Position = new Vector2(3f, 0f);
            snapshot.SetPlayer(Team.Navy, 3, pressingOpponent);
            Assert.That(MNG_RuleBasedManager.Decide(snapshot, Team.Red, decision).Command,
                Is.EqualTo(MNG_Command.PassBuild));

            SetCarrier(snapshot, Team.Red, 3, new Vector2(snapshot.FieldHalfLength - 18f, 0f));
            Assert.That(MNG_RuleBasedManager.Decide(snapshot, Team.Red, decision).Command,
                Is.EqualTo(MNG_Command.AttemptShot));

            SetCarrier(snapshot, Team.Navy, 3, Vector2.zero);
            Assert.That(MNG_RuleBasedManager.Decide(snapshot, Team.Red, decision).Command,
                Is.EqualTo(MNG_Command.ActiveRecover));

            SetCarrier(snapshot, Team.Navy, 3, new Vector2(-snapshot.FieldHalfLength * 0.7f, 0f));
            Assert.That(MNG_RuleBasedManager.Decide(snapshot, Team.Red, decision).Command,
                Is.EqualTo(MNG_Command.ProtectBack));

            SetCarrier(snapshot, Team.Navy, 3, new Vector2(snapshot.FieldHalfLength * 0.7f, 0f));
            MoveTeamFarFromPoint(snapshot, Team.Red, new Vector2(-35f, -20f));
            Assert.That(MNG_RuleBasedManager.Decide(snapshot, Team.Red, decision).Command,
                Is.EqualTo(MNG_Command.Balanced));

            snapshot.Possession = MNG_Possession.Neutral;
            snapshot.Carrier = MNG_CarrierRef.None;
            Assert.That(MNG_RuleBasedManager.Decide(snapshot, Team.Red, decision).Command,
                Is.EqualTo(MNG_Command.ActiveRecover),
                "R0 must not deadlock on a distant neutral ball.");
        }

        [Test]
        public void R0RuleManager_IsSymmetricForMirroredTeams()
        {
            var redSnapshot = CreateSnapshot();
            SetCarrier(redSnapshot, Team.Red, 3, Vector2.zero);
            MoveTeamFarFromPoint(redSnapshot, Team.Navy, new Vector2(35f, 20f));
            var navySnapshot = CreateMirroredAndTeamSwappedSnapshot(redSnapshot);
            var decision = CreateDecision();
            decision.CommandAgeSeconds = 2f;

            var red = MNG_RuleBasedManager.Decide(redSnapshot, Team.Red, decision);
            var navy = MNG_RuleBasedManager.Decide(navySnapshot, Team.Navy, decision);

            Assert.That(navy.Command, Is.EqualTo(red.Command));
            Assert.That(navy.Reason, Is.EqualTo(red.Reason));
        }

        [Test]
        public void R0RuleManager_KeeperChoosesSafePassOrLongClearance()
        {
            var snapshot = CreateSnapshot();
            SetCarrier(snapshot, Team.Red, 0, new Vector2(-50f, 0f));
            var teammatePositions = new[]
            {
                new Vector2(-50f, 0f),
                new Vector2(-31f, -10f),
                new Vector2(-29f, 9f),
                new Vector2(-24f, 0f)
            };
            for (var slot = 1; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var teammate = snapshot.GetPlayer(Team.Red, slot);
                teammate.Position = teammatePositions[slot];
                teammate.Velocity = Vector2.zero;
                snapshot.SetPlayer(Team.Red, slot, teammate);
            }
            MoveTeamFarFromPoint(snapshot, Team.Navy, new Vector2(25f, -20f));
            var decision = CreateDecision();
            decision.CommandAgeSeconds = 2f;

            var safeTargets = MNG_TacticalTargetResolver.Resolve(snapshot, Team.Red, decision);
            Assert.That(safeTargets.HasPassTarget, Is.True);
            Assert.That(safeTargets.HasShotTarget, Is.True,
                "A keeper clearance toward the opponent goal must be available at any field depth.");
            Assert.That(MNG_RuleBasedManager.Decide(snapshot, Team.Red, decision).Command,
                Is.EqualTo(MNG_Command.PassBuild));

            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var opponent = snapshot.GetPlayer(Team.Navy, slot);
                opponent.Position = (snapshot.BallPosition + MNG_TeamPlanner.SelectPassTarget(snapshot, Team.Red, Mathf.Max(1, slot))) * 0.5f;
                opponent.Velocity = Vector2.zero;
                snapshot.SetPlayer(Team.Navy, slot, opponent);
            }
            var pressuredTargets = MNG_TacticalTargetResolver.Resolve(snapshot, Team.Red, decision);
            Assert.That(pressuredTargets.HasPassTarget, Is.False);
            Assert.That(pressuredTargets.HasShotTarget, Is.True);
            Assert.That(MNG_RuleBasedManager.Decide(snapshot, Team.Red, decision).Command,
                Is.EqualTo(MNG_Command.AttemptShot));
        }

        [Test]
        public void TacticalTargetResolver_UsesOpenPredictedReceiverAndRealGoalWidth()
        {
            var snapshot = CreateSnapshot();
            var carrier = snapshot.GetPlayer(Team.Red, 3);
            carrier.Position = new Vector2(42f, 0f);
            carrier.Velocity = Vector2.zero;
            snapshot.SetPlayer(Team.Red, 3, carrier);
            snapshot.BallPosition = carrier.Position;

            var receiver = snapshot.GetPlayer(Team.Red, 1);
            receiver.Position = new Vector2(52f, -6f);
            receiver.Velocity = new Vector2(1f, 0f);
            snapshot.SetPlayer(Team.Red, 1, receiver);
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var opponent = snapshot.GetPlayer(Team.Navy, slot);
                opponent.Position = new Vector2(10f, 24f + slot * 3f);
                snapshot.SetPlayer(Team.Navy, slot, opponent);
            }

            var open = MNG_TacticalTargetResolver.Resolve(snapshot, Team.Red);
            Assert.That(open.HasPassTarget, Is.False, "Near-goal choices prefer shooting");
            Assert.That(open.HasShotTarget, Is.True);

            snapshot.BallPosition = new Vector2(42f, snapshot.GoalHalfWidth + 0.1f);
            var angledShot = MNG_TacticalTargetResolver.Resolve(snapshot, Team.Red);
            Assert.That(angledShot.HasShotTarget, Is.True);

            snapshot.BallPosition = new Vector2(
                42f,
                snapshot.GoalHalfWidth + MNG_TacticalTargetResolver.ShotLateralMargin + 0.1f);
            var outsideOpening = MNG_TacticalTargetResolver.Resolve(snapshot, Team.Red);
            Assert.That(outsideOpening.HasShotTarget, Is.False);
        }

        [Test]
        public void TeamPlanner_ShootsInsideFarPostAwayFromKeeper()
        {
            var snapshot = CreateSnapshot();
            var navyKeeper = snapshot.GetPlayer(Team.Navy, 0);
            navyKeeper.Position = new Vector2(snapshot.FieldHalfLength - 3f, 6f);
            snapshot.SetPlayer(Team.Navy, 0, navyKeeper);

            var target = MNG_TeamPlanner.SelectShotTarget(snapshot, Team.Red, 3);

            Assert.That(target.x, Is.EqualTo(snapshot.FieldHalfLength));
            Assert.That(target.y, Is.LessThan(0f));
            Assert.That(Mathf.Abs(target.y), Is.LessThan(snapshot.GoalHalfWidth));
            Assert.That(
                snapshot.GoalHalfWidth - Mathf.Abs(target.y),
                Is.EqualTo(MNG_TeamPlanner.ShotTargetPostMargin).Within(0.0001f));
        }

        [Test]
        public void TacticalTargetResolver_RejectsBackwardBlockedPass()
        {
            var snapshot = CreateSnapshot();
            var carrier = snapshot.GetPlayer(Team.Red, 3);
            carrier.Position = Vector2.zero;
            snapshot.SetPlayer(Team.Red, 3, carrier);
            snapshot.BallPosition = Vector2.zero;
            for (var slot = 0; slot < 3; slot++)
            {
                var teammate = snapshot.GetPlayer(Team.Red, slot);
                teammate.Active = slot == 1;
                teammate.Position = new Vector2(-10f, 0f);
                teammate.Velocity = Vector2.zero;
                snapshot.SetPlayer(Team.Red, slot, teammate);
            }
            var blocker = snapshot.GetPlayer(Team.Navy, 0);
            blocker.Position = new Vector2(-5f, 0f);
            snapshot.SetPlayer(Team.Navy, 0, blocker);

            var targets = MNG_TacticalTargetResolver.Resolve(snapshot, Team.Red);

            Assert.That(targets.HasPassTarget, Is.False, "Backward and blocked passes are not a PassBuild option.");
        }

        [Test]
        public void R0RuleManager_ConsidersPassBeforeEscapingBlockedForwardLane()
        {
            var snapshot = CreateSnapshot();
            SetCarrier(snapshot, Team.Red, 3, Vector2.zero);
            var receiver = snapshot.GetPlayer(Team.Red, 1);
            receiver.Position = new Vector2(6f, 8f);
            receiver.Velocity = Vector2.zero;
            snapshot.SetPlayer(Team.Red, 1, receiver);
            MoveTeamFarFromPoint(snapshot, Team.Navy, new Vector2(35f, 20f));
            var blocker = snapshot.GetPlayer(Team.Navy, 3);
            blocker.Position = new Vector2(6f, 0f);
            snapshot.SetPlayer(Team.Navy, 3, blocker);
            var decision = CreateDecision();
            decision.PreviousCommand = MNG_Command.AdvanceCarry;
            decision.CommandAgeSeconds = 2f;

            Assert.That(MNG_TacticalTargetResolver.IsForwardDribbleBlocked(
                snapshot, Team.Red, 3), Is.True);
            var rule = MNG_RuleBasedManager.Decide(snapshot, Team.Red, decision);
            Assert.That(rule.Command, Is.EqualTo(MNG_Command.PassBuild));
            Assert.That(rule.Reason, Is.EqualTo("blocked-forward-pass"));

            blocker.Position = new Vector2(6f,
                MNG_TacticalTargetResolver.ForwardDribbleLaneHalfWidth + 1f);
            snapshot.SetPlayer(Team.Navy, 3, blocker);
            Assert.That(MNG_TacticalTargetResolver.IsForwardDribbleBlocked(
                snapshot, Team.Red, 3), Is.False);
        }

        [Test]
        public void TeamPlanner_PassTargetLeadsForwardAndTowardFieldCenter()
        {
            var snapshot = CreateSnapshot();
            var receiver = snapshot.GetPlayer(Team.Red, 1);
            receiver.Position = new Vector2(10f, 9f);
            receiver.Velocity = Vector2.zero;
            snapshot.SetPlayer(Team.Red, 1, receiver);

            var redTarget = MNG_TeamPlanner.SelectPassTarget(snapshot, Team.Red, 1);
            Assert.That(redTarget.x, Is.EqualTo(
                receiver.Position.x + MNG_TeamPlanner.PassTargetForwardLead).Within(0.0001f));
            Assert.That(redTarget.y, Is.EqualTo(
                receiver.Position.y - MNG_TeamPlanner.PassTargetCenterBias).Within(0.0001f));
            Assert.That(Mathf.Abs(redTarget.y), Is.LessThan(Mathf.Abs(receiver.Position.y)));

            receiver.Position = new Vector2(-10f, -9f);
            snapshot.SetPlayer(Team.Navy, 1, receiver);
            var navyTarget = MNG_TeamPlanner.SelectPassTarget(snapshot, Team.Navy, 1);
            Assert.That(navyTarget.x, Is.EqualTo(
                receiver.Position.x - MNG_TeamPlanner.PassTargetForwardLead).Within(0.0001f));
            Assert.That(navyTarget.y, Is.EqualTo(
                receiver.Position.y + MNG_TeamPlanner.PassTargetCenterBias).Within(0.0001f));
            Assert.That(Mathf.Abs(navyTarget.y), Is.LessThan(Mathf.Abs(receiver.Position.y)));
        }

        [Test]
        public void TacticalRewardTracker_ValidShotRequiresOpponentGoalOpeningTrajectory()
        {
            var snapshot = CreateSnapshot();
            var origin = new Vector2(40f, 2f);

            Assert.That(MNG_TacticalRewardTracker.IsGoalOpeningTrajectory(
                snapshot, Team.Red, origin, new Vector2(snapshot.FieldHalfLength, 0f)), Is.True);
            Assert.That(MNG_TacticalRewardTracker.IsGoalOpeningTrajectory(
                snapshot, Team.Red, origin,
                new Vector2(snapshot.FieldHalfLength, snapshot.GoalHalfWidth + 2f)), Is.False);
            Assert.That(MNG_TacticalRewardTracker.IsGoalOpeningTrajectory(
                snapshot, Team.Red, origin, new Vector2(-20f, 0f)), Is.False);
        }

        [Test]
        public void KickSolver_UsesMassTimesVelocityDeltaAndClampsSpeed()
        {
            Assert.That(MNG_KickSolver.RequestedKickStrengthIncrease, Is.EqualTo(1000f));
            Assert.That(MNG_KickSolver.PassStrengthEquivalent, Is.EqualTo(3000f));
            Assert.That(MNG_KickSolver.ShotStrengthEquivalent, Is.EqualTo(6000f));
            Assert.That(MNG_KickSolver.KickStrengthExitSpeedIncrease,
                Is.EqualTo(1000f * 0.02f / 3f).Within(0.0001f));
            Assert.That(MNG_KickSolver.ControlledExitSpeed,
                Is.EqualTo(14f + MNG_KickSolver.KickStrengthExitSpeedIncrease).Within(0.0001f));
            Assert.That(MNG_KickSolver.StrongExitSpeed,
                Is.EqualTo(28f + MNG_KickSolver.KickStrengthExitSpeedIncrease).Within(0.0001f));
            Assert.That(MNG_KickSolver.StrongExitSpeed, Is.LessThanOrEqualTo(MNG_KickSolver.MaximumBallSpeed));
            Assert.That(MNG_KickSolver.PassExitSpeedForDistance(5f),
                Is.EqualTo(MNG_KickSolver.ControlledExitSpeed));
            Assert.That(MNG_KickSolver.PassExitSpeedForDistance(10f),
                Is.EqualTo(MNG_KickSolver.ControlledExitSpeed));
            Assert.That(MNG_KickSolver.PassExitSpeedForDistance(15f),
                Is.EqualTo(21f + MNG_KickSolver.KickStrengthExitSpeedIncrease).Within(0.0001f));
            Assert.That(MNG_KickSolver.PassExitSpeedForDistance(20f),
                Is.EqualTo(MNG_KickSolver.StrongExitSpeed));
            Assert.That(MNG_KickSolver.PassExitSpeedForDistance(28f),
                Is.EqualTo(MNG_KickSolver.StrongExitSpeed));
            Assert.That(MNG_KickSolver.TrySolvePlanarImpulse(
                new Vector3(2f, 0f, 0f), Vector3.right, 40f, 4.5f, out var impulse), Is.True);

            Assert.That(impulse.x,
                Is.EqualTo(4.5f * (MNG_KickSolver.MaximumBallSpeed - 2f)).Within(0.0001f));
            Assert.That(impulse.y, Is.Zero);
            Assert.That(impulse.z, Is.Zero);
            Assert.That((new Vector3(2f, 0f, 0f) + impulse / 4.5f).magnitude,
                Is.EqualTo(MNG_KickSolver.MaximumBallSpeed).Within(0.0001f));
        }

        [Test]
        public void ControlOwnership_RevisionInvalidatesOldWriter()
        {
            var ownership = new MNG_ControlOwnership(Team.Red, 3);
            var managerRevision = ownership.Revision;
            Assert.That(ownership.CanWrite(MNG_InputOwner.Manager, managerRevision), Is.True);

            Assert.That(ownership.Toggle(), Is.EqualTo(MNG_InputOwner.Human));

            Assert.That(ownership.CanWrite(MNG_InputOwner.Manager, managerRevision), Is.False);
            Assert.That(ownership.CanWrite(MNG_InputOwner.Human, ownership.Revision), Is.True);
        }

        [Test]
        public void Snapshot_RejectsNaNBeforePolicyObservation()
        {
            var snapshot = CreateSnapshot();
            snapshot.BallVelocity = new Vector2(float.NaN, 0f);

            Assert.Throws<InvalidOperationException>(() => MNG_ObservationWriter.Write(
                snapshot, Team.Red, CreateDecision(), new float[MNG_ObservationWriter.ObservationSize]));
        }

        [Test]
        public void TeamPlanner_LeavesHumanSlotWithoutManagerTask()
        {
            var snapshot = CreateSnapshot();
            var human = snapshot.GetPlayer(Team.Red, 3);
            human.IsHuman = true;
            snapshot.SetPlayer(Team.Red, 3, human);
            var decision = CreateDecision();
            decision.ControlMask[3] = false;
            var tasks = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];

            MNG_TeamPlanner.Plan(snapshot, Team.Red, MNG_Command.Balanced, decision, 4, 0.5f, tasks);

            Assert.That(tasks[3].Skill, Is.EqualTo(MNG_PlayerSkill.None));
            Assert.That(tasks[3].Revision, Is.EqualTo(4));
        }

        [Test]
        public void TeamPlanner_RecoverBalancedAndProtectProduceDifferentAssignments()
        {
            var snapshot = CreateSnapshot();
            snapshot.Possession = MNG_Possession.Navy;
            snapshot.Carrier = MNG_CarrierRef.For(Team.Navy, 3);
            var decision = CreateDecision();
            var recover = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];
            var balanced = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];
            var protect = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];

            MNG_TeamPlanner.Plan(snapshot, Team.Red, MNG_Command.ActiveRecover, decision, 1, 0.5f, recover);
            MNG_TeamPlanner.Plan(snapshot, Team.Red, MNG_Command.Balanced, decision, 2, 0.5f, balanced);
            MNG_TeamPlanner.Plan(snapshot, Team.Red, MNG_Command.ProtectBack, decision, 3, 0.5f, protect);

            Assert.That(Array.FindAll(recover,
                task => task.Skill == MNG_PlayerSkill.Press).Length, Is.EqualTo(2));
            Assert.That(Array.FindAll(balanced,
                task => task.Skill == MNG_PlayerSkill.Press).Length, Is.EqualTo(2));
            Assert.That(Array.FindAll(protect,
                task => task.Skill == MNG_PlayerSkill.Press).Length, Is.EqualTo(2),
                "ProtectBack must not leave a single defender fighting alone.");
            Assert.That(recover[1].Target, Is.Not.EqualTo(balanced[1].Target));
            Assert.That(protect[1].Target, Is.Not.EqualTo(balanced[1].Target));
        }

        [Test]
        public void TeamPlanner_RoleAnchorsProduceDistinctSupportReactions()
        {
            var snapshot = CreateSnapshot();
            var decision = CreateDecision();
            var original = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];
            var shifted = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];

            MNG_TeamPlanner.Plan(snapshot, Team.Red, MNG_Command.Balanced, decision, 1, 0.5f, original);
            snapshot.BallPosition += new Vector2(10f, 6f);
            MNG_TeamPlanner.Plan(snapshot, Team.Red, MNG_Command.Balanced, decision, 2, 0.5f, shifted);

            Assert.That(original[1].Target, Is.Not.EqualTo(original[2].Target));
            Assert.That(original[2].Target, Is.Not.EqualTo(original[3].Target));
            Assert.That(shifted[1].Target - original[1].Target,
                Is.Not.EqualTo(shifted[2].Target - original[2].Target));
            Assert.That(shifted[2].Target - original[2].Target,
                Is.Not.EqualTo(shifted[3].Target - original[3].Target));
        }

        [Test]
        public void TeamPlanner_PrimaryPresserUsesPositionAndSwitchHysteresis()
        {
            var snapshot = CreateSnapshot();
            snapshot.Possession = MNG_Possession.Navy;
            snapshot.Carrier = MNG_CarrierRef.For(Team.Navy, 3);
            snapshot.BallPosition = Vector2.zero;
            var left = snapshot.GetPlayer(Team.Red, 1);
            left.Position = new Vector2(-2f, 0f);
            snapshot.SetPlayer(Team.Red, 1, left);
            var right = snapshot.GetPlayer(Team.Red, 2);
            right.Position = new Vector2(-5f, 0f);
            snapshot.SetPlayer(Team.Red, 2, right);
            var striker = snapshot.GetPlayer(Team.Red, 3);
            striker.Position = new Vector2(-12f, 0f);
            snapshot.SetPlayer(Team.Red, 3, striker);
            var decision = CreateDecision();
            var tasks = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];

            snapshot.EpisodeElapsedSeconds = 1f;
            MNG_TeamPlanner.Plan(snapshot, Team.Red, MNG_Command.ActiveRecover, decision, 1, 0.5f, tasks);
            Assert.That(decision.PrimaryPresserSlot, Is.EqualTo(1));
            Assert.That(tasks[1].Skill, Is.EqualTo(MNG_PlayerSkill.Press));
            Assert.That(tasks[2].Skill, Is.EqualTo(MNG_PlayerSkill.Press));
            Assert.That(tasks[1].Target, Is.Not.EqualTo(tasks[2].Target));

            left.Position = new Vector2(-2.5f, 0f);
            snapshot.SetPlayer(Team.Red, 1, left);
            right.Position = new Vector2(-2f, 0f);
            snapshot.SetPlayer(Team.Red, 2, right);
            snapshot.EpisodeElapsedSeconds = 1.5f;
            MNG_TeamPlanner.Plan(snapshot, Team.Red, MNG_Command.ActiveRecover, decision, 2, 0.5f, tasks);
            Assert.That(decision.PrimaryPresserSlot, Is.EqualTo(1), "Hold time prevents oscillation.");

            left.Position = new Vector2(-8f, 0f);
            snapshot.SetPlayer(Team.Red, 1, left);
            snapshot.EpisodeElapsedSeconds = 1.6f;
            MNG_TeamPlanner.Plan(snapshot, Team.Red, MNG_Command.ActiveRecover, decision, 3, 0.5f, tasks);
            Assert.That(decision.PrimaryPresserSlot, Is.EqualTo(2));
            Assert.That(tasks[2].Skill, Is.EqualTo(MNG_PlayerSkill.Press));
            Assert.That(tasks[1].Skill, Is.EqualTo(MNG_PlayerSkill.Press),
                "The previous primary becomes the supporting presser instead of watching.");
            Assert.That(MNG_TeamPlanner.LocalBallAwarenessRadius, Is.EqualTo(16f));
            Assert.That(MNG_TeamPlanner.KickExecutionSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void TeamPlanner_OpponentPossessionAlwaysAssignsTwoDistinctPressers()
        {
            var snapshot = CreateSnapshot();
            snapshot.Possession = MNG_Possession.Navy;
            snapshot.Carrier = MNG_CarrierRef.For(Team.Navy, 3);
            snapshot.BallPosition = new Vector2(15f, 3f);
            var tasks = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];

            foreach (var command in new[]
                     {
                         MNG_Command.ActiveRecover,
                         MNG_Command.Balanced,
                         MNG_Command.ProtectBack
                     })
            {
                MNG_TeamPlanner.Plan(
                    snapshot, Team.Red, command, CreateDecision(), 20, 1f, tasks);
                var pressers = new System.Collections.Generic.List<int>();
                for (var slot = 1; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
                    if (tasks[slot].Skill == MNG_PlayerSkill.Press) pressers.Add(slot);

                Assert.That(pressers.Count, Is.EqualTo(MNG_TeamPlanner.MinimumDefensivePressers),
                    $"{command} must preserve the two-player defensive press.");
                Assert.That(tasks[pressers[0]].Target, Is.Not.EqualTo(tasks[pressers[1]].Target),
                    "The primary ball challenger and secondary lane blocker need distinct targets.");
            }
        }

        [TestCase(Team.Red, 1f)]
        [TestCase(Team.Navy, -1f)]
        public void TeamPlanner_OpponentGoalPossessionAddsDistinctSecondAttacker(
            Team team,
            float attackSign)
        {
            var snapshot = CreateSnapshot();
            var ball = new Vector2(
                attackSign * (snapshot.FieldHalfLength - 16f),
                7f);
            SetCarrier(snapshot, team, 3, ball);
            var decision = CreateDecision();
            for (var slot = 1; slot <= 2; slot++)
            {
                var midfielder = snapshot.GetPlayer(team, slot);
                midfielder.Position = new Vector2(
                    -attackSign * (20f + slot * 5f),
                    slot == 1 ? -12f : 12f);
                snapshot.SetPlayer(team, slot, midfielder);
            }
            var supportSlot = MNG_TeamPlanner.SelectAttackingSupport(
                snapshot, team, decision, 3);
            var tasks = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];

            MNG_TeamPlanner.Plan(
                snapshot, team, MNG_Command.AdvanceCarry, decision, 22, 1f, tasks);

            Assert.That(MNG_TeamPlanner.RequiresAttackingSupport(snapshot, team), Is.True);
            Assert.That(supportSlot, Is.GreaterThanOrEqualTo(1).And.Not.EqualTo(3));
            Assert.That(tasks[3].Skill, Is.EqualTo(MNG_PlayerSkill.Carry));
            Assert.That(tasks[supportSlot].Skill, Is.EqualTo(MNG_PlayerSkill.SupportRun));
            Assert.That(Vector2.Distance(tasks[supportSlot].Target, ball),
                Is.InRange(3f, 8f),
                "The second attacker must join the ball without occupying the carrier's point.");
            Assert.That((tasks[supportSlot].Target.x - ball.x) * attackSign,
                Is.GreaterThan(0f),
                "The helper joins from the attacking side instead of pulling play backward.");
        }

        [TestCase(Team.Red)]
        [TestCase(Team.Navy)]
        public void TeamPlanner_OwnGoalThreatForcesBothMidfieldersHome(Team team)
        {
            var snapshot = CreateSnapshot();
            var sign = team == Team.Red ? 1f : -1f;
            var opponent = team == Team.Red ? Team.Navy : Team.Red;
            snapshot.BallPosition = new Vector2(
                -sign * snapshot.FieldHalfLength + sign * 12f,
                2f);
            snapshot.BallVelocity = new Vector2(-sign * 2f, 0f);
            snapshot.Possession = opponent == Team.Red
                ? MNG_Possession.Red
                : MNG_Possession.Navy;
            snapshot.Carrier = MNG_CarrierRef.For(opponent, 3);
            for (var slot = 1; slot <= 2; slot++)
            {
                var midfielder = snapshot.GetPlayer(team, slot);
                midfielder.Position = new Vector2(sign * 40f, midfielder.Position.y);
                snapshot.SetPlayer(team, slot, midfielder);
            }
            var tasks = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];

            MNG_TeamPlanner.Plan(
                snapshot, team, MNG_Command.AttemptShot, CreateDecision(), 21, 1f, tasks);

            Assert.That(MNG_TeamPlanner.IsOwnGoalThreat(snapshot, team), Is.True);
            for (var slot = 1; slot <= 2; slot++)
            {
                var targetDepth = (tasks[slot].Target.x
                    - (-sign * snapshot.FieldHalfLength)) * sign;
                Assert.That(targetDepth, Is.LessThanOrEqualTo(MNG_TeamPlanner.OwnGoalThreatDepth),
                    $"Midfielder {slot} must return from the attacking half to the own-goal defense.");
                Assert.That(tasks[slot].Skill,
                    Is.EqualTo(MNG_PlayerSkill.Press).Or.EqualTo(MNG_PlayerSkill.Cover));
            }
        }

        [TestCase(Team.Red, 1f)]
        [TestCase(Team.Navy, -1f)]
        public void TeamPlanner_GlobalStallTriesSafeShotBeforeRepositioning(
            Team team,
            float expectedDirection)
        {
            var snapshot = CreateSnapshot();
            snapshot.BallPosition = Vector2.zero;
            snapshot.BallVelocity = Vector2.zero;
            snapshot.Possession = MNG_Possession.Neutral;
            snapshot.Carrier = MNG_CarrierRef.None;
            snapshot.BallStallRecoveryActive = true;
            snapshot.BallStallRecoverySequence = 1;
            snapshot.BallStationarySeconds = MNG_GlobalBallStallTracker.TriggerSeconds + 0.1f;
            for (var slot = 1; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var player = snapshot.GetPlayer(team, slot);
                player.Position = new Vector2(expectedDirection * (slot + 1f), slot - 2f);
                player.KickCooldownSeconds = 0f;
                snapshot.SetPlayer(team, slot, player);
            }
            var tasks = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];

            MNG_TeamPlanner.Plan(
                snapshot, team, MNG_Command.ProtectBack, CreateDecision(), 22, 1f, tasks);

            var shooters = Array.FindAll(tasks,
                task => task.Skill == MNG_PlayerSkill.AimShot);
            Assert.That(shooters.Length, Is.EqualTo(1));
            Assert.That(Mathf.Sign(shooters[0].Target.x), Is.EqualTo(expectedDirection));
            Assert.That(MNG_TeamPlanner.IsSafeStallShotDirection(
                snapshot, team, shooters[0].Target), Is.True);
            Assert.That(MNG_TeamPlanner.IsSafeStallShotDirection(
                snapshot,
                team,
                new Vector2(-expectedDirection * snapshot.FieldHalfLength, 0f)), Is.False,
                "A stalled-ball recovery kick toward the own goal must be rejected.");

            snapshot.BallStationarySeconds = MNG_GlobalBallStallTracker.TriggerSeconds
                + MNG_TeamPlanner.StallShotAttemptSeconds + 0.1f;
            MNG_TeamPlanner.Plan(
                snapshot, team, MNG_Command.ProtectBack, CreateDecision(), 23, 1f, tasks);
            Assert.That(Array.FindAll(tasks,
                task => task.Skill == MNG_PlayerSkill.AimShot).Length, Is.Zero,
                "After the first safe-shot window, the planner must continue with repositioning.");
            Assert.That(tasks[1].Skill, Is.Not.EqualTo(MNG_PlayerSkill.None));
            Assert.That(tasks[2].Skill, Is.Not.EqualTo(MNG_PlayerSkill.None));
            Assert.That(tasks[3].Skill, Is.Not.EqualTo(MNG_PlayerSkill.None));
        }

        [Test]
        public void TeamPlanner_GlobalStallMobilizesEveryFieldPlayerAcrossPossessionStates()
        {
            var snapshot = CreateSnapshot();
            snapshot.BallPosition = Vector2.zero;
            snapshot.BallVelocity = Vector2.zero;
            snapshot.Possession = MNG_Possession.Neutral;
            snapshot.Carrier = MNG_CarrierRef.None;
            snapshot.BallStallRecoveryActive = true;
            snapshot.BallStallRecoverySequence = 1;
            snapshot.BallStationarySeconds = MNG_GlobalBallStallTracker.TriggerSeconds
                + MNG_TeamPlanner.StallShotAttemptSeconds + 0.1f;
            var decision = CreateDecision();
            var tasks = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];

            MNG_TeamPlanner.Plan(
                snapshot, Team.Red, MNG_Command.ProtectBack, decision, 30, 0.75f, tasks);

            var pressCount = 0;
            for (var slot = 1; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                Assert.That(tasks[slot].Skill, Is.Not.EqualTo(MNG_PlayerSkill.None));
                if (tasks[slot].Skill == MNG_PlayerSkill.Press) pressCount++;
            }
            Assert.That(pressCount, Is.EqualTo(1),
                "One player challenges the ball while the others take active recovery lanes.");
            Assert.That(tasks[1].Target, Is.Not.EqualTo(tasks[2].Target));
            Assert.That(tasks[2].Target, Is.Not.EqualTo(tasks[3].Target));
            var firstTargets = new[] { tasks[1].Target, tasks[2].Target, tasks[3].Target };

            snapshot.BallStallRecoverySequence++;
            MNG_TeamPlanner.Plan(
                snapshot, Team.Red, MNG_Command.ProtectBack, decision, 31, 0.75f, tasks);
            Assert.That(
                tasks[1].Target != firstTargets[0]
                || tasks[2].Target != firstTargets[1]
                || tasks[3].Target != firstTargets[2],
                Is.True,
                "A continuing stall must retarget at least one supporting player.");

            SetCarrier(snapshot, Team.Red, 3, Vector2.zero);
            snapshot.BallStallRecoveryActive = true;
            snapshot.BallStallRecoverySequence++;
            MNG_TeamPlanner.Plan(
                snapshot, Team.Red, MNG_Command.Balanced, decision, 32, 0.75f, tasks);
            Assert.That(tasks[3].Skill, Is.EqualTo(MNG_PlayerSkill.Carry));
            Assert.That(tasks[1].Skill, Is.Not.EqualTo(MNG_PlayerSkill.None));
            Assert.That(tasks[2].Skill, Is.Not.EqualTo(MNG_PlayerSkill.None));

            MNG_TeamPlanner.Plan(
                snapshot, Team.Navy, MNG_Command.ProtectBack, CreateDecision(), 33, 0.75f, tasks);
            Assert.That(tasks[1].Skill, Is.Not.EqualTo(MNG_PlayerSkill.None));
            Assert.That(tasks[2].Skill, Is.Not.EqualTo(MNG_PlayerSkill.None));
            Assert.That(tasks[3].Skill, Is.Not.EqualTo(MNG_PlayerSkill.None));
            Assert.That(Array.FindAll(tasks,
                task => task.Skill == MNG_PlayerSkill.Press).Length, Is.EqualTo(2),
                "An opponent-owned stalled ball still requires two coordinated defenders.");
        }

        [Test]
        public void BallControl_DribbleAssistWobblesAndAvoidsAnAdvancingDefender()
        {
            var snapshot = CreateSnapshot();
            SetCarrier(snapshot, Team.Red, 3, Vector2.zero);
            MoveTeamFarFromPoint(snapshot, Team.Navy, new Vector2(30f, 20f));
            var defender = snapshot.GetPlayer(Team.Navy, 1);
            defender.Active = true;
            defender.Position = new Vector2(2f, 1f);
            snapshot.SetPlayer(Team.Navy, 1, defender);

            var avoidance = MNG_BallControl.CalculateCarrierAvoidance(
                snapshot, Team.Red, 3, Vector2.right * 4f);
            Assert.That(avoidance.sqrMagnitude, Is.GreaterThan(0f));
            Assert.That(avoidance.y, Is.LessThan(0f),
                "A defender on the carrier's left must steer the dribble to the right.");
            Assert.That(MNG_BallControl.CalculateCarrierAvoidance(
                snapshot, Team.Red, 3, Vector2.left * 4f), Is.EqualTo(Vector2.zero),
                "Avoidance is only added while advancing toward the opposing half.");

            var wobbleA = MNG_BallControl.CalculateDribbleWobble(0.25f, 4f, 3);
            var wobbleB = MNG_BallControl.CalculateDribbleWobble(0.55f, 4f, 3);
            Assert.That(wobbleA.sqrMagnitude, Is.GreaterThan(0f));
            Assert.That(wobbleB, Is.Not.EqualTo(wobbleA));
            Assert.That(MNG_BallControl.StealReacquireLockSeconds, Is.GreaterThanOrEqualTo(0.20f));
        }

        [Test]
        public void TeamPlanner_LocalPressureProducesDifferentSupportRoutes()
        {
            var snapshot = CreateSnapshot();
            snapshot.BallPosition = Vector2.zero;
            snapshot.BallVelocity = new Vector2(4f, 2f);
            snapshot.Possession = MNG_Possession.Red;
            snapshot.Carrier = MNG_CarrierRef.For(Team.Red, 3);
            var left = snapshot.GetPlayer(Team.Red, 1);
            left.Position = new Vector2(-6f, -3f);
            snapshot.SetPlayer(Team.Red, 1, left);
            var right = snapshot.GetPlayer(Team.Red, 2);
            right.Position = new Vector2(-6f, 3f);
            snapshot.SetPlayer(Team.Red, 2, right);
            var navyLeft = snapshot.GetPlayer(Team.Navy, 1);
            navyLeft.Position = new Vector2(5f, -9f);
            snapshot.SetPlayer(Team.Navy, 1, navyLeft);
            var navyRight = snapshot.GetPlayer(Team.Navy, 2);
            navyRight.Position = new Vector2(5f, 9f);
            snapshot.SetPlayer(Team.Navy, 2, navyRight);
            var tasks = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];

            MNG_TeamPlanner.Plan(
                snapshot, Team.Red, MNG_Command.AdvanceCarry, CreateDecision(), 10, 0.75f, tasks);

            Assert.That(tasks[1].Skill, Is.EqualTo(MNG_PlayerSkill.SupportRun));
            Assert.That(tasks[2].Skill, Is.EqualTo(MNG_PlayerSkill.SupportRun));
            Assert.That(tasks[1].Target, Is.Not.EqualTo(tasks[2].Target));
            Assert.That(Mathf.Sign(tasks[1].Target.y), Is.Not.EqualTo(Mathf.Sign(tasks[2].Target.y)),
                "The two midfielders should preserve different local lanes instead of tracing one ball path.");
        }

        [Test]
        public void TeamPlanner_KeeperClaimsLooseThreatChallengesCloseCarrierAndBlocksAtRange()
        {
            var snapshot = CreateSnapshot();
            var keeper = snapshot.GetPlayer(Team.Red, 0);
            var ownGoalX = -snapshot.FieldHalfLength;
            keeper.Position = new Vector2(ownGoalX + 4f, 0f);
            snapshot.SetPlayer(Team.Red, 0, keeper);
            for (var slot = 1; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var fieldPlayer = snapshot.GetPlayer(Team.Red, slot);
                fieldPlayer.Position = new Vector2(5f + slot, slot * 8f);
                snapshot.SetPlayer(Team.Red, slot, fieldPlayer);
            }
            snapshot.BallPosition = new Vector2(ownGoalX + 9f, 3f);
            snapshot.BallVelocity = new Vector2(-3f, 0f);
            snapshot.Possession = MNG_Possession.Neutral;
            snapshot.Carrier = MNG_CarrierRef.None;
            var tasks = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];

            MNG_TeamPlanner.Plan(
                snapshot, Team.Red, MNG_Command.Balanced, CreateDecision(), 11, 0.75f, tasks);

            Assert.That(tasks[0].Skill, Is.EqualTo(MNG_PlayerSkill.KeeperClaim));
            Assert.That(tasks[0].Target.x, Is.GreaterThan(ownGoalX + 2f));

            snapshot.Possession = MNG_Possession.Navy;
            snapshot.Carrier = MNG_CarrierRef.For(Team.Navy, 3);
            MNG_TeamPlanner.Plan(
                snapshot, Team.Red, MNG_Command.ActiveRecover, CreateDecision(), 12, 0.75f, tasks);

            Assert.That(tasks[0].Skill, Is.EqualTo(MNG_PlayerSkill.KeeperClaim),
                "The keeper must actively challenge an opponent carrying the ball nearby.");

            snapshot.BallPosition = new Vector2(ownGoalX + 20f, 3f);
            var opponentCarrier = snapshot.GetPlayer(Team.Navy, 3);
            opponentCarrier.Position = snapshot.BallPosition;
            snapshot.SetPlayer(Team.Navy, 3, opponentCarrier);
            MNG_TeamPlanner.Plan(
                snapshot, Team.Red, MNG_Command.ActiveRecover, CreateDecision(), 13, 0.75f, tasks);

            Assert.That(tasks[0].Skill, Is.EqualTo(MNG_PlayerSkill.KeeperBlock));
            Assert.That(tasks[0].Target.x, Is.GreaterThan(ownGoalX + 2f));
            Assert.That(tasks[0].Target.x, Is.LessThan(ownGoalX + 14f),
                "The keeper may narrow the angle but must not abandon the defensive zone.");
        }

        [Test]
        public void TeamPlanner_KeeperPassesWhenSafeAndClearsWhenReceiversArePressed()
        {
            var snapshot = CreateSnapshot();
            SetCarrier(snapshot, Team.Red, 0, new Vector2(-50f, 0f));
            var teammatePositions = new[]
            {
                new Vector2(-50f, 0f),
                new Vector2(-30f, -10f),
                new Vector2(-28f, 8f),
                new Vector2(-20f, 0f)
            };
            for (var slot = 1; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var teammate = snapshot.GetPlayer(Team.Red, slot);
                teammate.Position = teammatePositions[slot];
                teammate.Velocity = Vector2.zero;
                snapshot.SetPlayer(Team.Red, slot, teammate);
            }
            MoveTeamFarFromPoint(snapshot, Team.Navy, new Vector2(20f, -18f));
            var tasks = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];

            MNG_TeamPlanner.Plan(
                snapshot, Team.Red, MNG_Command.Balanced, CreateDecision(), 20, 0.75f, tasks);

            Assert.That(tasks[0].Skill, Is.EqualTo(MNG_PlayerSkill.AimPass));
            Assert.That(tasks[0].ReceiverSlot, Is.InRange(1, 3));
            var receiverSlot = tasks[0].ReceiverSlot;
            Assert.That(tasks[receiverSlot].Skill, Is.EqualTo(MNG_PlayerSkill.ReceivePass));
            Assert.That(tasks[receiverSlot].Target.x,
                Is.EqualTo(tasks[0].Target.x).Within(0.0001f));
            Assert.That(tasks[receiverSlot].Target.y,
                Is.EqualTo(tasks[0].Target.y).Within(0.0001f));

            for (var slot = 1; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var opponent = snapshot.GetPlayer(Team.Navy, slot);
                opponent.Position = teammatePositions[slot];
                opponent.Velocity = Vector2.zero;
                snapshot.SetPlayer(Team.Navy, slot, opponent);
            }
            MNG_TeamPlanner.Plan(
                snapshot, Team.Red, MNG_Command.Balanced, CreateDecision(), 21, 0.75f, tasks);

            Assert.That(tasks[0].Skill, Is.EqualTo(MNG_PlayerSkill.AimShot));
            Assert.That(tasks[0].ReceiverSlot, Is.EqualTo(-1));
            Assert.That(tasks[0].Target.x,
                Is.EqualTo(snapshot.FieldHalfLength).Within(0.0001f));
        }

        [Test]
        public void PossessionLedger_RequiresContactDistanceAndConfirmation()
        {
            var ledger = new MNG_PossessionLedger();
            var candidates = new[]
            {
                Candidate(Team.Red, 3, true, 1f, 1.2f)
            };

            ledger.Update(candidates, 1, 0.10f);
            Assert.That(ledger.Carrier.IsValid, Is.False);
            ledger.Update(candidates, 1, 0.10f);
            Assert.That(ledger.Carrier.IsValid, Is.True);
            Assert.That(ledger.Carrier.Team, Is.EqualTo(Team.Red));
            Assert.That(ledger.Carrier.Slot, Is.EqualTo(3));

            candidates[0] = Candidate(Team.Red, 3, false, 1f, 1.2f);
            ledger.Reset();
            ledger.Update(candidates, 1, 0.25f);
            Assert.That(ledger.Carrier.IsValid, Is.False);
        }

        [Test]
        public void PossessionLedger_UsesDistanceThenStableSlotTieBreak()
        {
            var ledger = new MNG_PossessionLedger();
            var candidates = new[]
            {
                Candidate(Team.Navy, 2, true, 1f, 1.2f),
                Candidate(Team.Red, 1, true, 1f, 1.2f)
            };

            ledger.Update(candidates, 2, 0.20f);

            Assert.That(ledger.Carrier.Team, Is.EqualTo(Team.Red));
            Assert.That(ledger.Carrier.Slot, Is.EqualTo(1));
        }

        [Test]
        public void PossessionLedger_DribbleZoneAcquiresWithoutCollisionAndCloserOpponentStealsQuickly()
        {
            var ledger = new MNG_PossessionLedger(
                MNG_PhysicsProfile.KickPlateReleasePadding,
                MNG_PhysicsProfile.KickPlateReleaseDelaySeconds,
                MNG_PhysicsProfile.KickPlatePossessionConfirmationSeconds,
                MNG_PhysicsProfile.KickPlatePreviousOwnerLockSeconds);
            var candidates = new[]
            {
                Candidate(Team.Red, 3, false, 0.10f, MNG_KickPlate.DribbleCaptureRadius),
                Candidate(Team.Navy, 1, false, 2f, MNG_KickPlate.DribbleCaptureRadius)
            };
            candidates[0].IsInControlZone = true;
            ledger.Update(candidates, 2, 0.02f);
            Assert.That(ledger.Carrier.Team, Is.EqualTo(Team.Red));

            candidates[1].IsInControlZone = true;
            candidates[1].CenterDistance = 0.02f;
            ledger.Update(candidates, 2, 0.02f);
            Assert.That(ledger.Carrier.Team, Is.EqualTo(Team.Navy));
            Assert.That(ledger.Carrier.Slot, Is.EqualTo(1));
        }

        [Test]
        public void PossessionLedger_KickReleasesAndTemporarilyLocksPreviousOwner()
        {
            var ledger = new MNG_PossessionLedger();
            var candidates = new[] { Candidate(Team.Red, 3, true, 1f, 1.2f) };
            ledger.Update(candidates, 1, 0.20f);
            ledger.ReleaseForKick(Team.Red, 3);
            Assert.That(ledger.Carrier.IsValid, Is.False);

            ledger.Update(candidates, 1, 0.10f);
            ledger.Update(candidates, 1, 0.10f);
            Assert.That(ledger.Carrier.IsValid, Is.False);
            ledger.Update(candidates, 1, 0.20f);
            Assert.That(ledger.Carrier.IsValid, Is.True);
        }

        [Test]
        public void PhysicsProfile_DefaultsMatchFixedBallContract()
        {
            var profile = ScriptableObject.CreateInstance<MNG_PhysicsProfile>();
            try
            {
                Assert.DoesNotThrow(profile.ValidateOrThrow);
                Assert.That(profile.BallScaleMultiplier, Is.EqualTo(1.10f));
                Assert.That(profile.BallMass, Is.EqualTo(3f));
                Assert.That(profile.BallRestitution, Is.EqualTo(0.15f));
                Assert.That(profile.MaximumBallSpeed,
                    Is.EqualTo(MNG_KickSolver.MaximumBallSpeed));
                Assert.That(profile.DribbleAcceleration, Is.EqualTo(24f));
                Assert.That(profile.DribbleDampingPerSecond, Is.EqualTo(5.5f));
                Assert.That(profile.ReleasePadding,
                    Is.EqualTo(MNG_PhysicsProfile.KickPlateReleasePadding));
                Assert.That(profile.ReleaseDelaySeconds,
                    Is.EqualTo(MNG_PhysicsProfile.KickPlateReleaseDelaySeconds));
                Assert.That(profile.PossessionConfirmationSeconds,
                    Is.EqualTo(MNG_PhysicsProfile.KickPlatePossessionConfirmationSeconds));
                Assert.That(profile.PreviousOwnerLockSeconds,
                    Is.EqualTo(MNG_PhysicsProfile.KickPlatePreviousOwnerLockSeconds));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void PlayerMotor_AccelerationAndDecelerationRespectConfiguredRates()
        {
            var accelerated = MNG_PlayerMotor.CalculateNextVelocity(
                Vector2.zero, Vector2.right * 20f, 9f, 30f, 45f, 0.1f);
            Assert.That(accelerated.x, Is.EqualTo(3f).Within(0.0001f));

            var decelerated = MNG_PlayerMotor.CalculateNextVelocity(
                Vector2.right * 9f, Vector2.zero, 9f, 30f, 45f, 0.1f);
            Assert.That(decelerated.x, Is.EqualTo(4.5f).Within(0.0001f));
        }

        [Test]
        public void KickPlate_ForwardCorrectionRejectsStopReverseAndSidewaysCommands()
        {
            Assert.That(MNG_KickPlate.IsForwardCommand(Vector2.right * 5f, Vector2.right), Is.True);
            Assert.That(MNG_KickPlate.IsForwardCommand(Vector2.zero, Vector2.right), Is.False);
            Assert.That(MNG_KickPlate.IsForwardCommand(Vector2.left * 5f, Vector2.right), Is.False);
            Assert.That(MNG_KickPlate.IsForwardCommand(Vector2.up * 5f, Vector2.right), Is.False);
            Assert.That(MNG_KickPlate.IsForwardCommand(
                new Vector2(0.8f, 0.2f), Vector2.right), Is.True);
        }

        [Test]
        public void KickPlate_ArmsOnlyValidRequestsAndConsumesOwnColliderOnce()
        {
            var player = new GameObject("MNG_KickPlateTestPlayer");
            var plateObject = new GameObject("KickPlate");
            var platePart = new GameObject("KickPlateCenter");
            var foreign = new GameObject("ForeignCollider");
            try
            {
                player.AddComponent<Rigidbody>();
                var avatar = player.AddComponent<MNG_PlayerAvatar>();
                avatar.Configure(Team.Red, 3, MNG_PlayerRole.Striker);
                plateObject.transform.SetParent(player.transform, false);
                platePart.transform.SetParent(plateObject.transform, false);
                var collider = platePart.AddComponent<BoxCollider>();
                var foreignCollider = foreign.AddComponent<BoxCollider>();
                var plate = plateObject.AddComponent<MNG_KickPlate>();
                plate.Configure(avatar, Vector3.zero, new Vector3(0f, 0f, 0.68f));

                Assert.That(plate.TryArmKick(Vector3.right * 10f, 14f), Is.True);
                Assert.That(plate.TryConsumeStrike(foreignCollider, out _), Is.False);
                Assert.That(plate.TryConsumeStrike(collider, out var request), Is.True);
                Assert.That(request.Target, Is.EqualTo(Vector3.right * 10f));
                Assert.That(request.ExitSpeed, Is.EqualTo(14f));
                Assert.That(request.Intent, Is.EqualTo(MNG_KickIntent.Unspecified));
                Assert.That(plate.TryConsumeStrike(collider, out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(foreign);
            }
        }

        [Test]
        public void PlayerMotor_ApproachFixtureReachesAtLeastNinetyFivePercentWithoutNaNOrLeavingField()
        {
            const float deltaTime = 0.02f;
            const float maximumSpeed = 9f;
            const float acceleration = 30f;
            const float deceleration = 45f;
            const float arrivalSlowRadius = 1.5f;
            var distances = new[] { 5f, 20f, 40f };
            var successes = 0;
            var invalidSamples = 0;
            var outsideSamples = 0;

            foreach (var distance in distances)
            for (var directionIndex = 0; directionIndex < 24; directionIndex++)
            {
                var angle = directionIndex * Mathf.PI * 2f / 24f;
                var position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                var velocity = Vector2.zero;
                for (var step = 0; step < 300; step++)
                {
                    var offset = -position;
                    var speed = maximumSpeed * Mathf.Clamp01(offset.magnitude / arrivalSlowRadius);
                    var desired = offset.sqrMagnitude > 0.000001f
                        ? offset.normalized * speed
                        : Vector2.zero;
                    velocity = MNG_PlayerMotor.CalculateNextVelocity(
                        velocity, desired, maximumSpeed, acceleration, deceleration, deltaTime);
                    position += velocity * deltaTime;
                    if (!IsFinite(position.x)
                        || !IsFinite(position.y)
                        || !IsFinite(velocity.x)
                        || !IsFinite(velocity.y))
                        invalidSamples++;
                    if (Mathf.Abs(position.x) > SoccerArenaGeometry.StadiumHalfLength
                        || Mathf.Abs(position.y) > SoccerArenaGeometry.StadiumHalfWidth)
                        outsideSamples++;
                }
                if (position.magnitude <= 0.5f && velocity.magnitude <= 0.5f) successes++;
            }

            Assert.That(successes, Is.GreaterThanOrEqualTo(69), $"Reached {successes}/72 fixtures.");
            Assert.That(invalidSamples, Is.Zero);
            Assert.That(outsideSamples, Is.Zero);
        }

        static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        [Test]
        public void TeamPlanner_PassTaskTargetsChosenReceiver()
        {
            var snapshot = CreateSnapshot();
            SetCarrier(snapshot, Team.Red, 3, Vector2.zero);
            var receiver = snapshot.GetPlayer(Team.Red, 1);receiver.Position=new Vector2(10,10);receiver.Velocity=Vector2.zero;snapshot.SetPlayer(Team.Red,1,receiver);
            MoveTeamFarFromPoint(snapshot, Team.Navy, new Vector2(35,20));
            var decision = CreateDecision();
            decision.PendingPassReceiverSlot = 1;
            var tasks = new MNG_PlayerTask[MNG_MatchSnapshot.PlayersPerTeam];

            MNG_TeamPlanner.Plan(snapshot, Team.Red, MNG_Command.PassBuild, decision, 2, 0.75f, tasks);

            Assert.That(tasks[3].Skill, Is.EqualTo(MNG_PlayerSkill.AimPass));
            Assert.That(tasks[3].ReceiverSlot, Is.EqualTo(1));
            Assert.That(tasks[1].Skill, Is.EqualTo(MNG_PlayerSkill.ReceivePass));
            Assert.That(tasks[3].Target, Is.EqualTo(tasks[1].Target));
            Assert.That(tasks[3].Target.x,
                Is.GreaterThan(snapshot.GetPlayer(Team.Red, 1).Position.x));
            Assert.That(tasks[1].ExpirySeconds,
                Is.EqualTo(snapshot.EpisodeElapsedSeconds + MNG_TeamPlanner.KickExecutionSeconds));
        }

        [Test]
        public void EventLedger_DeduplicatesAndKeepsTerminalRewardsOutsideShapingCap()
        {
            var profile = ScriptableObject.CreateInstance<MNG_RewardProfile>();
            try
            {
                var ledger = new MNG_EventLedger();
                Assert.That(ledger.TryRecord(
                    new MNG_RewardEvent(MNG_RewardEventKind.GoalFor, 7), profile, 10f, out var first), Is.True);
                Assert.That(first, Is.EqualTo(1f));
                Assert.That(ledger.TryRecord(
                    new MNG_RewardEvent(MNG_RewardEventKind.GoalFor, 7), profile, 11f, out var duplicate), Is.False);
                Assert.That(duplicate, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void EventLedger_AppliesTacticMultiplierThenPerKindCap()
        {
            var profile = ScriptableObject.CreateInstance<MNG_RewardProfile>();
            profile.Configure(MNG_TacticProfile.Attack);
            try
            {
                var ledger = new MNG_EventLedger();
                ledger.TryRecord(new MNG_RewardEvent(MNG_RewardEventKind.ValidShot, 1), profile, 1f, out var first);
                ledger.TryRecord(new MNG_RewardEvent(MNG_RewardEventKind.ValidShot, 2), profile, 2f, out var second);
                ledger.TryRecord(new MNG_RewardEvent(MNG_RewardEventKind.ValidShot, 3), profile, 3f, out var third);

                Assert.That(first, Is.EqualTo(0.04f).Within(0.0001f));
                Assert.That(second, Is.EqualTo(0.02f).Within(0.0001f));
                Assert.That(third, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void EventLedger_RollingWindowExpiresAtSixtySeconds()
        {
            var profile = ScriptableObject.CreateInstance<MNG_RewardProfile>();
            try
            {
                var ledger = new MNG_EventLedger();
                for (var i = 0; i < 3; i++)
                    ledger.TryRecord(new MNG_RewardEvent(MNG_RewardEventKind.ValidShot, i), profile, i, out _);
                ledger.TryRecord(new MNG_RewardEvent(MNG_RewardEventKind.ValidShot, 10), profile, 3f, out var capped);
                ledger.TryRecord(new MNG_RewardEvent(MNG_RewardEventKind.ValidShot, 11), profile, 61f, out var expired);

                Assert.That(capped, Is.Zero);
                Assert.That(expired, Is.EqualTo(0.02f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void MatchController_CountsGoalOnceAndEntersThreeSecondPause()
        {
            var root = new GameObject("MNG_TestMatch");
            try
            {
                var geometry = root.AddComponent<SoccerArenaGeometry>();
                geometry.Configure(124f, 84.655f, 20f, 6f, 5f);
                var ball = new GameObject("MNG_Ball");
                ball.transform.SetParent(root.transform);
                var ballBody = ball.AddComponent<Rigidbody>();
                var avatars = new MNG_PlayerAvatar[8];
                for (var teamIndex = 0; teamIndex < 2; teamIndex++)
                {
                    for (var slot = 0; slot < 4; slot++)
                    {
                        var player = new GameObject($"MNG_Player_{teamIndex}_{slot}");
                        player.transform.SetParent(root.transform);
                        player.AddComponent<Rigidbody>();
                        var avatar = player.AddComponent<MNG_PlayerAvatar>();
                        avatar.Configure(
                            teamIndex == 0 ? Team.Red : Team.Navy,
                            slot,
                            (MNG_PlayerRole)slot);
                        avatars[teamIndex * 4 + slot] = avatar;
                    }
                }
                var match = root.AddComponent<MNG_MatchController>();
                match.Configure(geometry, ballBody, avatars);

                match.SetPossession(MNG_Possession.Red, MNG_CarrierRef.For(Team.Red, 3));
                var sentMask = new bool[MNG_CommandMask.CommandCount];
                MNG_CommandMask.Write(match.Snapshot, Team.Red, match.GetDecisionState(Team.Red), sentMask);
                Assert.That(sentMask[(int)MNG_Command.AdvanceCarry], Is.True);
                match.SetPossession(MNG_Possession.Neutral, MNG_CarrierRef.None);
                Assert.DoesNotThrow(() => match.AcceptPolicyCommand(Team.Red, MNG_Command.AdvanceCarry));
                Assert.That(match.GetDecisionState(Team.Red).PreviousCommand,
                    Is.EqualTo(MNG_Command.AdvanceCarry));

                Assert.That(match.GoalTouched(Team.Red), Is.True);
                Assert.That(match.GoalTouched(Team.Red), Is.False);
                Assert.That(match.RedScore, Is.EqualTo(1));
                Assert.That(match.NavyScore, Is.Zero);
                Assert.That(match.State, Is.EqualTo(MNG_MatchState.GoalPause));
                Assert.That(match.GoalPauseRemainingSeconds, Is.EqualTo(3f));
                Assert.That(ballBody.isKinematic, Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MatchController_RejectsPossessionWithoutMatchingCarrier()
        {
            var root = new GameObject("MNG_TestMatch");
            try
            {
                var geometry = root.AddComponent<SoccerArenaGeometry>();
                var ball = new GameObject("MNG_Ball");
                ball.transform.SetParent(root.transform);
                var ballBody = ball.AddComponent<Rigidbody>();
                var avatars = new MNG_PlayerAvatar[8];
                for (var i = 0; i < avatars.Length; i++)
                {
                    var player = new GameObject($"MNG_Player_{i}");
                    player.transform.SetParent(root.transform);
                    player.AddComponent<Rigidbody>();
                    avatars[i] = player.AddComponent<MNG_PlayerAvatar>();
                    avatars[i].Configure(i < 4 ? Team.Red : Team.Navy, i % 4, (MNG_PlayerRole)(i % 4));
                }
                var match = root.AddComponent<MNG_MatchController>();
                match.Configure(geometry, ballBody, avatars);

                Assert.Throws<ArgumentException>(() => match.SetPossession(
                    MNG_Possession.Red,
                    MNG_CarrierRef.For(Team.Navy, 1)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MatchController_CommandAgeResetsOnlyWhenCommandChanges()
        {
            var root = new GameObject("MNG_TestCommandAge");
            try
            {
                var geometry = root.AddComponent<SoccerArenaGeometry>();
                var ball = new GameObject("MNG_Ball");
                ball.transform.SetParent(root.transform);
                var ballBody = ball.AddComponent<Rigidbody>();
                var avatars = new MNG_PlayerAvatar[8];
                for (var i = 0; i < avatars.Length; i++)
                {
                    var player = new GameObject($"MNG_Player_{i}");
                    player.transform.SetParent(root.transform);
                    player.AddComponent<Rigidbody>();
                    avatars[i] = player.AddComponent<MNG_PlayerAvatar>();
                    avatars[i].Configure(i < 4 ? Team.Red : Team.Navy, i % 4, (MNG_PlayerRole)(i % 4));
                }

                var match = root.AddComponent<MNG_MatchController>();
                match.Configure(geometry, ballBody, avatars);
                var decision = match.GetDecisionState(Team.Red);
                decision.PreviousCommand = MNG_Command.Balanced;
                decision.CommandAgeSeconds = 0.75f;

                Assert.That(match.AcceptCommand(Team.Red, MNG_Command.Balanced), Is.True);
                Assert.That(decision.CommandAgeSeconds, Is.EqualTo(0.75f).Within(0.0001f));

                Assert.That(match.AcceptCommand(Team.Red, MNG_Command.ActiveRecover), Is.True);
                Assert.That(decision.CommandAgeSeconds, Is.Zero);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FallbackDecision_UsesShotPressurePassAndRecoveryPriorities()
        {
            var snapshot = CreateSnapshot();
            snapshot.BallPosition = new Vector2(50f, 0f);
            var shot = MNG_FallbackManager.Decide(snapshot, Team.Red);
            Assert.That(shot.Command, Is.EqualTo(MNG_Command.AttemptShot));

            snapshot.BallPosition = snapshot.GetPlayer(Team.Red, 3).Position;
            var opponent = snapshot.GetPlayer(Team.Navy, 0);
            opponent.Position = snapshot.GetPlayer(Team.Red, 3).Position + Vector2.right * 2f;
            snapshot.SetPlayer(Team.Navy, 0, opponent);
            var receiver=snapshot.GetPlayer(Team.Red,1);receiver.Position=snapshot.BallPosition+new Vector2(5,12);snapshot.SetPlayer(Team.Red,1,receiver);
            var pressure = MNG_FallbackManager.Decide(snapshot, Team.Red);
            Assert.That(pressure.Command, Is.EqualTo(MNG_Command.PassBuild));
            Assert.That(pressure.PassReceiverSlot, Is.GreaterThanOrEqualTo(0));

            snapshot.Possession = MNG_Possession.Navy;
            snapshot.Carrier = MNG_CarrierRef.For(Team.Navy, 3);
            var recovery = MNG_FallbackManager.Decide(snapshot, Team.Red);
            Assert.That(recovery.Command, Is.EqualTo(MNG_Command.ActiveRecover));
        }

        [Test]
        public void FallbackDecision_HumanCarrierLeavesBallActionsToHuman()
        {
            var snapshot = CreateSnapshot();
            var striker = snapshot.GetPlayer(Team.Red, 3);
            striker.IsHuman = true;
            snapshot.SetPlayer(Team.Red, 3, striker);

            var decision = MNG_FallbackManager.Decide(snapshot, Team.Red);

            Assert.That(decision.Command, Is.EqualTo(MNG_Command.Balanced));
            Assert.That(decision.Reason, Is.EqualTo("human-carrier"));
        }

        [Test]
        public void CurriculumCatalog_M1DefaultsMatchApprovedBudgetAndProtocol()
        {
            var catalog = ScriptableObject.CreateInstance<MNG_CurriculumCatalog>();
            try
            {
                catalog.ApplyM1Defaults();
                Assert.DoesNotThrow(catalog.ValidateOrThrow);
                Assert.That(catalog.EpisodeSeconds, Is.EqualTo(20f));
                Assert.That(catalog.MinimumGoalDistance, Is.EqualTo(20f));
                Assert.That(catalog.MaximumGoalDistance, Is.EqualTo(35f));
                Assert.That(catalog.TrainingSeed, Is.EqualTo(11001));
                Assert.That(catalog.ValidationSeed, Is.EqualTo(21001));
                Assert.That(catalog.EvaluationScenarioCount, Is.EqualTo(100));
                Assert.That(catalog.TimeScale, Is.EqualTo(20f));
                Assert.That(catalog.GetEpisodeSeconds(MNG_CurriculumStage.M2DefenseChoice), Is.EqualTo(20f));
                Assert.That(catalog.GetSeed(MNG_CurriculumStage.M2DefenseChoice, false), Is.EqualTo(12001));
                Assert.That(catalog.GetSeed(MNG_CurriculumStage.M2DefenseChoice, true), Is.EqualTo(22001));
                Assert.That(catalog.GetEvaluationScenarioCount(MNG_CurriculumStage.M2DefenseChoice), Is.EqualTo(100));
                Assert.That(catalog.M2RecoveryDeadline, Is.EqualTo(10f));
                Assert.That(catalog.GetM3MatchSeconds(false), Is.EqualTo(60f));
                Assert.That(catalog.GetM3MatchSeconds(true), Is.EqualTo(300f));
                Assert.That(catalog.GetSeed(MNG_CurriculumStage.M3FallbackMatch, false), Is.EqualTo(13001));
                Assert.That(catalog.GetSeed(MNG_CurriculumStage.M3FallbackMatch, true), Is.EqualTo(23001));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void AttackScenarioGenerator_IsDeterministicBoundedAndMirroredInPairs()
        {
            const float fieldHalfLength = 62f;
            for (var index = 0; index < 100; index += 2)
            {
                var left = MNG_AttackScenarioGenerator.Generate(21001, index, fieldHalfLength);
                var repeated = MNG_AttackScenarioGenerator.Generate(21001, index, fieldHalfLength);
                var right = MNG_AttackScenarioGenerator.Generate(21001, index + 1, fieldHalfLength);
                var distance = fieldHalfLength - left.BallPosition.x;

                Assert.That(repeated.BallPosition, Is.EqualTo(left.BallPosition));
                Assert.That(distance, Is.InRange(20f, 35f));
                Assert.That(right.Kind, Is.EqualTo(left.Kind));
                Assert.That(right.BallPosition.x, Is.EqualTo(left.BallPosition.x).Within(0.0001f));
                Assert.That(right.BallPosition.y, Is.EqualTo(-left.BallPosition.y).Within(0.0001f));
                Assert.That(left.CarrierSlot, Is.EqualTo(3));
                Assert.That(right.CarrierSlot, Is.EqualTo(3));
                for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
                {
                    Assert.That(right.RedPositions[slot].x,
                        Is.EqualTo(left.RedPositions[slot].x).Within(0.0001f));
                    Assert.That(right.RedPositions[slot].y,
                        Is.EqualTo(-left.RedPositions[slot].y).Within(0.0001f));
                    Assert.That(right.NavyPositions[slot].x,
                        Is.EqualTo(left.NavyPositions[slot].x).Within(0.0001f));
                    Assert.That(right.NavyPositions[slot].y,
                        Is.EqualTo(-left.NavyPositions[slot].y).Within(0.0001f));
                }
            }
        }

        [Test]
        public void SpawnJitter_IsExpandedDeterministicMirroredAndCanLockCarrier()
        {
            Assert.That(MNG_SpawnJitter.MaximumAxisOffset, Is.EqualTo(3.00f));
            var original = new[]
            {
                new Vector2(-20f, 0f),
                new Vector2(-5f, -8f),
                new Vector2(-5f, 8f),
                new Vector2(0f, 0f)
            };
            var left = (Vector2[])original.Clone();
            var repeated = (Vector2[])original.Clone();
            var mirroredBase = new[]
            {
                new Vector2(-20f, 0f),
                new Vector2(-5f, 8f),
                new Vector2(-5f, -8f),
                new Vector2(0f, 0f)
            };
            var mirrored = (Vector2[])mirroredBase.Clone();

            MNG_SpawnJitter.Apply(new System.Random(12345), 1f, left, 3);
            MNG_SpawnJitter.Apply(new System.Random(12345), 1f, repeated, 3);
            MNG_SpawnJitter.Apply(new System.Random(12345), -1f, mirrored, 3);

            Assert.That(left[3], Is.EqualTo(original[3]));
            Assert.That(repeated, Is.EqualTo(left));
            var anyChanged = false;
            for (var slot = 0; slot < 3; slot++)
            {
                var delta = left[slot] - original[slot];
                anyChanged |= delta.sqrMagnitude > 0.000001f;
                Assert.That(Mathf.Abs(delta.x), Is.LessThanOrEqualTo(MNG_SpawnJitter.MaximumAxisOffset));
                Assert.That(Mathf.Abs(delta.y), Is.LessThanOrEqualTo(MNG_SpawnJitter.MaximumAxisOffset));
                Assert.That(mirrored[slot].x - mirroredBase[slot].x,
                    Is.EqualTo(delta.x).Within(0.0001f));
                Assert.That(mirrored[slot].y - mirroredBase[slot].y,
                    Is.EqualTo(-delta.y).Within(0.0001f));
            }
            Assert.That(anyChanged, Is.True);
        }

        [Test]
        public void M1EvaluationRules_V6RequiresBaselineGainZeroOwnGoalsAndRealPasses()
        {
            Assert.That(MNG_M1EvaluationRules.RandomBaselineGoals, Is.EqualTo(72));
            Assert.That(MNG_M1EvaluationRules.RequiredGoals, Is.EqualTo(82));
            Assert.That(MNG_M1EvaluationRules.Passes(100, 82, 0, 18, 12, 8), Is.True);
            Assert.That(MNG_M1EvaluationRules.Passes(100, 81, 0, 19, 12, 8), Is.False);
            Assert.That(MNG_M1EvaluationRules.Passes(100, 99, 1, 0, 12, 8), Is.False);
            Assert.That(MNG_M1EvaluationRules.Passes(99, 99, 0, 0, 12, 8), Is.False);
            Assert.That(MNG_M1EvaluationRules.Passes(100, 82, 0, 17, 12, 8), Is.False);
            Assert.That(MNG_M1EvaluationRules.Passes(100, 82, 0, 18, 11, 8), Is.False);
            Assert.That(MNG_M1EvaluationRules.Passes(100, 82, 0, 18, 12, 7), Is.False);
        }

        [Test]
        public void M1MovingEvaluationRules_RequireTenPointGainOverMovingDefenseBaseline()
        {
            Assert.That(MNG_M1MovingEvaluationRules.BaselineIsCalibrated, Is.True);
            Assert.That(MNG_M1MovingEvaluationRules.RandomBaselineGoals, Is.EqualTo(20));
            Assert.That(MNG_M1MovingEvaluationRules.RequiredGoals, Is.EqualTo(30));
            Assert.That(MNG_M1MovingEvaluationRules.Passes(100, 30, 0, 70), Is.True);
            Assert.That(MNG_M1MovingEvaluationRules.Passes(100, 29, 0, 71), Is.False);
            Assert.That(MNG_M1MovingEvaluationRules.Passes(100, 99, 1, 0), Is.False);
        }

        [Test]
        public void DefenseScenarioGenerator_IsDeterministicBoundedMirroredAndMixed()
        {
            const float fieldHalfLength = 62f;
            var neutralCount = 0;
            for (var index = 0; index < 100; index += 2)
            {
                var left = MNG_DefenseScenarioGenerator.Generate(22001, index, fieldHalfLength);
                var repeated = MNG_DefenseScenarioGenerator.Generate(22001, index, fieldHalfLength);
                var right = MNG_DefenseScenarioGenerator.Generate(22001, index + 1, fieldHalfLength);
                var threatDistance = left.BallPosition.x + fieldHalfLength;

                Assert.That(repeated.BallPosition, Is.EqualTo(left.BallPosition));
                Assert.That(threatDistance, Is.InRange(18f, 35f));
                Assert.That(right.Kind, Is.EqualTo(left.Kind));
                Assert.That(right.StartsNeutral, Is.EqualTo(left.StartsNeutral));
                Assert.That(right.BallPosition.x, Is.EqualTo(left.BallPosition.x).Within(0.0001f));
                Assert.That(right.BallPosition.y, Is.EqualTo(-left.BallPosition.y).Within(0.0001f));
                if (left.StartsNeutral) neutralCount += 2;
                for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
                {
                    Assert.That(right.RedPositions[slot].x,
                        Is.EqualTo(left.RedPositions[slot].x).Within(0.0001f));
                    Assert.That(right.RedPositions[slot].y,
                        Is.EqualTo(-left.RedPositions[slot].y).Within(0.0001f));
                    Assert.That(right.NavyPositions[slot].x,
                        Is.EqualTo(left.NavyPositions[slot].x).Within(0.0001f));
                    Assert.That(right.NavyPositions[slot].y,
                        Is.EqualTo(-left.NavyPositions[slot].y).Within(0.0001f));
                }
            }
            Assert.That(neutralCount, Is.InRange(32, 34));
        }

        [Test]
        public void M2EvaluationRules_RequireFastRecoveryAndFewerConcessionsThanRandom()
        {
            Assert.That(MNG_M2EvaluationRules.RandomBaselineRecoveriesWithinDeadline, Is.EqualTo(52));
            Assert.That(MNG_M2EvaluationRules.RandomBaselineConcededGoals, Is.EqualTo(45));
            Assert.That(MNG_M2EvaluationRules.MaximumConcededGoals, Is.EqualTo(44));
            Assert.That(MNG_M2EvaluationRules.Passes(100, 60, 44, 0, 56), Is.True);
            Assert.That(MNG_M2EvaluationRules.Passes(100, 59, 44, 0, 56), Is.False);
            Assert.That(MNG_M2EvaluationRules.Passes(100, 80, 45, 0, 55), Is.False);
            Assert.That(MNG_M2EvaluationRules.Passes(99, 80, 19, 0, 80), Is.False);
            Assert.That(MNG_M2EvaluationRules.Passes(100, 80, 19, 0, 80), Is.False);
        }

        [Test]
        public void M3EvaluationRules_RequireScoreRateScoringAndBothTeamsGoalSamples()
        {
            Assert.That(MNG_M3EvaluationRules.MaximumScorelessMatches, Is.EqualTo(8));
            Assert.That(MNG_M3EvaluationRules.Passes(40, 18, 4, 18, 20, 18, 8), Is.True);
            Assert.That(MNG_M3EvaluationRules.Passes(40, 17, 4, 19, 20, 18, 8), Is.False);
            Assert.That(MNG_M3EvaluationRules.Passes(40, 18, 4, 18, 20, 18, 9), Is.False);
            Assert.That(MNG_M3EvaluationRules.Passes(40, 20, 0, 20, 20, 0, 8), Is.False);
            Assert.That(MNG_M3EvaluationRules.Passes(39, 20, 0, 19, 20, 18, 8), Is.False);
        }

        [Test]
        public void FallbackMatchScenarioGenerator_MixesFourStartsInMirroredPairs()
        {
            const float fieldHalfLength = 62f;
            var kinds = new bool[4];
            for (var index = 0; index < 16; index += 2)
            {
                var left = MNG_FallbackMatchScenarioGenerator.Generate(23001, index, fieldHalfLength);
                var right = MNG_FallbackMatchScenarioGenerator.Generate(23001, index + 1, fieldHalfLength);
                kinds[(int)left.Kind] = true;
                Assert.That(right.Kind, Is.EqualTo(left.Kind));
                Assert.That(right.BallPosition.x, Is.EqualTo(left.BallPosition.x).Within(0.0001f));
                Assert.That(right.BallPosition.y, Is.EqualTo(-left.BallPosition.y).Within(0.0001f));
                Assert.That(Mathf.Abs(left.BallPosition.x), Is.LessThan(26f));
                Assert.That(Mathf.Abs(left.BallPosition.y), Is.LessThanOrEqualTo(10f));
                for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
                {
                    Assert.That(right.RedPositions[slot].x,
                        Is.EqualTo(left.RedPositions[slot].x).Within(0.0001f));
                    Assert.That(right.RedPositions[slot].y,
                        Is.EqualTo(-left.RedPositions[slot].y).Within(0.0001f));
                    Assert.That(right.NavyPositions[slot].x,
                        Is.EqualTo(left.NavyPositions[slot].x).Within(0.0001f));
                    Assert.That(right.NavyPositions[slot].y,
                        Is.EqualTo(-left.NavyPositions[slot].y).Within(0.0001f));
                }
            }
            Assert.That(kinds, Is.All.True);
        }

        static MNG_MatchSnapshot CreateSnapshot()
        {
            var snapshot = new MNG_MatchSnapshot
            {
                TickId = 25,
                EpisodeId = 2,
                BallPosition = new Vector2(12f, -4f),
                BallVelocity = new Vector2(6f, 3f),
                Possession = MNG_Possession.Red,
                Carrier = MNG_CarrierRef.For(Team.Red, 3),
                EpisodeElapsedSeconds = 10f,
                EpisodeDurationSeconds = 20f,
                MatchRemainingSeconds = 240f,
                RedScore = 2,
                NavyScore = 1
            };

            for (var teamIndex = 0; teamIndex < 2; teamIndex++)
            {
                var team = teamIndex == 0 ? Team.Red : Team.Navy;
                for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
                {
                    snapshot.SetPlayer(team, slot, new MNG_PlayerState
                    {
                        Active = true,
                        Position = new Vector2((teamIndex == 0 ? -1f : 1f) * (20f - slot), slot * 3f - 4f),
                        Velocity = new Vector2(teamIndex == 0 ? 2f : -2f, slot - 1f),
                        Forward = new Vector2(teamIndex == 0 ? 1f : -1f, 0.25f),
                        Role = (MNG_PlayerRole)slot,
                        KickCooldownSeconds = 0f,
                        MaximumKickCooldownSeconds = 1f
                    });
                }
            }
            return snapshot;
        }

        static void SetCarrier(
            MNG_MatchSnapshot snapshot,
            Team team,
            int slot,
            Vector2 position)
        {
            var carrier = snapshot.GetPlayer(team, slot);
            carrier.Active = true;
            carrier.IsHuman = false;
            carrier.Position = position;
            carrier.Velocity = Vector2.zero;
            carrier.KickCooldownSeconds = 0f;
            snapshot.SetPlayer(team, slot, carrier);
            snapshot.BallPosition = position;
            snapshot.BallVelocity = Vector2.zero;
            snapshot.Possession = team == Team.Red ? MNG_Possession.Red : MNG_Possession.Navy;
            snapshot.Carrier = MNG_CarrierRef.For(team, slot);
        }

        static void MoveTeamFarFromPoint(MNG_MatchSnapshot snapshot, Team team, Vector2 origin)
        {
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var player = snapshot.GetPlayer(team, slot);
                player.Position = origin + new Vector2(slot * 3f, slot * 2f);
                player.Velocity = Vector2.zero;
                snapshot.SetPlayer(team, slot, player);
            }
        }

        static MNG_PossessionCandidate Candidate(
            Team team,
            int slot,
            bool contact,
            float distance,
            float acquisitionDistance)
        {
            return new MNG_PossessionCandidate
            {
                Team = team,
                Slot = slot,
                Active = true,
                HasPhysicalContact = contact,
                CenterDistance = distance,
                AcquisitionDistance = acquisitionDistance
            };
        }

        static MNG_TeamDecisionState CreateDecision()
        {
            return new MNG_TeamDecisionState
            {
                PreviousCommand = MNG_Command.PassBuild,
                CommandAgeSeconds = 0.75f,
                PendingPassReceiverSlot = 1,
                SecondsSincePossessionLoss = 2f,
                HasPassTarget = true,
                HasShotTarget = true
            };
        }

        static MNG_MatchSnapshot CreateMirroredAndTeamSwappedSnapshot(MNG_MatchSnapshot source)
        {
            var mirrored = new MNG_MatchSnapshot
            {
                TickId = source.TickId,
                EpisodeId = source.EpisodeId,
                BallPosition = -source.BallPosition,
                BallVelocity = -source.BallVelocity,
                BallStallRecoveryActive = source.BallStallRecoveryActive,
                BallStallRecoverySequence = source.BallStallRecoverySequence,
                BallStationarySeconds = source.BallStationarySeconds,
                Possession = source.Possession == MNG_Possession.Red ? MNG_Possession.Navy : MNG_Possession.Red,
                Carrier = MNG_CarrierRef.For(Team.Navy, source.Carrier.Slot),
                EpisodeElapsedSeconds = source.EpisodeElapsedSeconds,
                EpisodeDurationSeconds = source.EpisodeDurationSeconds,
                MatchRemainingSeconds = source.MatchRemainingSeconds,
                RedScore = source.NavyScore,
                NavyScore = source.RedScore,
                FieldHalfLength = source.FieldHalfLength,
                FieldHalfWidth = source.FieldHalfWidth,
                GoalHalfWidth = source.GoalHalfWidth
            };

            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                var red = source.GetPlayer(Team.Red, slot);
                red.Position = -red.Position;
                red.Velocity = -red.Velocity;
                red.Forward = -red.Forward;
                mirrored.SetPlayer(Team.Navy, slot, red);

                var navy = source.GetPlayer(Team.Navy, slot);
                navy.Position = -navy.Position;
                navy.Velocity = -navy.Velocity;
                navy.Forward = -navy.Forward;
                mirrored.SetPlayer(Team.Red, slot, navy);
            }
            return mirrored;
        }
    }
}
