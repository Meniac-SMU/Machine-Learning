using System;
using MachineLearning.Soccer;
using NUnit.Framework;
using UnityEngine;

namespace MachineLearning.Soccer.Manager.Tests
{
    public sealed class MNG_RebuildV2Tests
    {
        [Test]
        public void RecoveryRequiresQualifiedOpponentThenStableOwnAndDoesNotRepeat()
        {
            var r = new MNG_ObservedRecovery();
            r.Step(true, false, true, 1f); Assert.That(r.Count, Is.Zero);
            r.Step(false, true, true, .3f); r.Step(false, false, true, .2f);
            r.Step(true, false, true, .1f); Assert.That(r.Count, Is.Zero);
            r.Step(true, false, true, .1f); Assert.That(r.Count, Is.EqualTo(1));
            r.Step(true, false, true, 1f); Assert.That(r.Count, Is.EqualTo(1));
            r.Step(false, true, true, .3f); r.Step(false, false, false, .1f);
            r.Step(true, false, true, 1f); Assert.That(r.Count, Is.EqualTo(1));
        }
        [TestCase(MNG_Command.AdvanceCarry, MNG_PlayerSkill.Carry, 1, 1)]
        [TestCase(MNG_Command.PassBuild, MNG_PlayerSkill.AimPass, 0, 1)]
        [TestCase(MNG_Command.AttemptShot, MNG_PlayerSkill.AimShot, 1, 1)]
        [TestCase(MNG_Command.ActiveRecover, MNG_PlayerSkill.Carry, 2, 0)]
        [TestCase(MNG_Command.Balanced, MNG_PlayerSkill.Carry, 1, 1)]
        [TestCase(MNG_Command.ProtectBack, MNG_PlayerSkill.Carry, 0, 2)]
        public void OwnPossessionPreservesCarrierAndSupportRoles(MNG_Command command, MNG_PlayerSkill carrierSkill, int supports, int covers)
        {
            foreach (var team in new[] { Team.Red, Team.Navy })
            {
                var sign = team == Team.Red ? 1f : -1f;
                var s = new MNG_MatchSnapshot { BallPosition = new Vector2(sign * 20, 0),
                    Carrier = MNG_CarrierRef.For(team, 3), Possession = team == Team.Red ? MNG_Possession.Red : MNG_Possession.Navy,
                    BallStallRecoveryActive = true, BallStationarySeconds = 6 };
                for (var slot = 0; slot < 4; slot++) s.SetPlayer(team, slot, new MNG_PlayerState
                    { Active = true, Role = (MNG_PlayerRole)slot, Position = new Vector2(sign * (slot == 1 ? 28 : 20), sign * (slot - 2) * 8) });
                var decision = new MNG_TeamDecisionState { HasPassTarget = true, HasShotTarget = true, PendingPassReceiverSlot = 1 };
                var tasks = new MNG_PlayerTask[4];
                MNG_TeamPlanner.PlanV2(s, team, command, decision, 1, 1, tasks);
                Assert.That(tasks[3].Skill, Is.EqualTo(carrierSkill));
                var support = 0; var cover = 0;
                for (var i = 1; i < 3; i++) { if (tasks[i].Skill == MNG_PlayerSkill.SupportRun) support++; if (tasks[i].Skill == MNG_PlayerSkill.Cover) cover++; }
                Assert.That(support, Is.EqualTo(supports)); Assert.That(cover, Is.EqualTo(covers));
                if (command == MNG_Command.PassBuild) Assert.That(tasks[1].Skill, Is.EqualTo(MNG_PlayerSkill.ReceivePass));
                if (command == MNG_Command.ProtectBack) Assert.That(sign * (tasks[3].Target.x - s.BallPosition.x), Is.LessThanOrEqualTo(0.01f));
            }
        }

        [TestCase(MNG_Command.AdvanceCarry)]
        [TestCase(MNG_Command.ActiveRecover)]
        [TestCase(MNG_Command.Balanced)]
        [TestCase(MNG_Command.ProtectBack)]
        public void KeeperAndStallCannotGenerateAutomaticShot(MNG_Command command)
        {
            var s = new MNG_MatchSnapshot { Carrier = MNG_CarrierRef.For(Team.Red, 0), Possession = MNG_Possession.Red,
                BallStallRecoveryActive = true, BallStationarySeconds = 6 };
            for (var slot = 0; slot < 4; slot++) s.SetPlayer(Team.Red, slot, new MNG_PlayerState
                { Active = true, Role = (MNG_PlayerRole)slot, Position = new Vector2(slot * 8, slot * 6) });
            var tasks = new MNG_PlayerTask[4];
            MNG_TeamPlanner.PlanV2(s, Team.Red, command, new MNG_TeamDecisionState(), 1, 1, tasks);
            foreach (var task in tasks) Assert.That(MNG_TaskLifetime.IsKick(task.Skill), Is.False);
        }
        [TestCase(MNG_Command.ActiveRecover, 2)]
        [TestCase(MNG_Command.Balanced, 1)]
        [TestCase(MNG_Command.ProtectBack, 0)]
        public void PressureCountPreservesCommandEvenNearOwnGoalAndDuringStall(MNG_Command command, int expected)
        {
            foreach (var team in new[] { Team.Red, Team.Navy })
            {
                var sign = team == Team.Red ? 1f : -1f;
                var snapshot = new MNG_MatchSnapshot { BallPosition = new Vector2(-sign * 45, 0),
                    BallStallRecoveryActive = true, BallStationarySeconds = 5,
                    Possession = team == Team.Red ? MNG_Possession.Navy : MNG_Possession.Red };
                for (var slot = 0; slot < 4; slot++) snapshot.SetPlayer(team, slot, new MNG_PlayerState
                    { Active = true, Role = (MNG_PlayerRole)slot, Position = new Vector2(-sign * 35, slot * 8) });
                var tasks = new MNG_PlayerTask[4];
                MNG_TeamPlanner.PlanV2(snapshot, team, command, new MNG_TeamDecisionState(), 1, 1, tasks);
                var press = 0; var cover = 0;
                for (var slot = 1; slot < 4; slot++)
                { if (tasks[slot].Skill == MNG_PlayerSkill.Press) press++; if (tasks[slot].Skill == MNG_PlayerSkill.Cover) cover++; }
                Assert.That(press, Is.EqualTo(expected)); Assert.That(press + cover, Is.EqualTo(3));
            }
        }
        static MNG_PlayerTask Task(long id, MNG_PlayerSkill skill, float expiry = 2f) => new()
        { TaskId = id, ParentCommandId = id, Revision = id, Skill = skill,
          Target = new Vector2(10, 3), ReceiverSlot = -1, ExpirySeconds = expiry };

        [Test]
        public void PreparingCanBeReplacedAndEquivalentDoesNotExtendDeadline()
        {
            var life = new MNG_TaskLifetime();
            life.Request(Task(1, MNG_PlayerSkill.AimShot), 1, 0, true, true, false);
            Assert.That(life.Request(Task(2, MNG_PlayerSkill.AimShot, 3), 2, .5f, true, true, false).Kind,
                Is.EqualTo(MNG_TaskResultKind.RetainedEquivalent));
            Assert.That(life.Current.ExpirySeconds, Is.EqualTo(2));
            Assert.That(life.Request(Task(3, MNG_PlayerSkill.Cover), 3, .6f, true, true, false).Kind,
                Is.EqualTo(MNG_TaskResultKind.Accepted));
            Assert.That(life.Current.Skill, Is.EqualTo(MNG_PlayerSkill.Cover));
        }

        [Test]
        public void CommitQueueKeepsOnlyLatestWithoutExtendingDeadline()
        {
            var life = new MNG_TaskLifetime();
            life.Request(Task(1, MNG_PlayerSkill.AimShot), 1, 0, true, true, false);
            life.ConsumeKick();
            Assert.That(life.Request(Task(2, MNG_PlayerSkill.Press, 1), 2, .02f, true, true, true).Kind,
                Is.EqualTo(MNG_TaskResultKind.DeferredCommit));
            life.Request(Task(3, MNG_PlayerSkill.Cover, 2), 3, .04f, true, true, true);
            Assert.That(life.Current.TaskId, Is.EqualTo(1));
            Assert.That(life.TakeDeferred(out var task), Is.True);
            Assert.That(task.TaskId, Is.EqualTo(3));
            Assert.That(task.ExpirySeconds, Is.EqualTo(1));
            Assert.That(life.TakeDeferred(out _), Is.False);
            Assert.That(life.Request(task, 5, .1f, true, true, false).Kind, Is.EqualTo(MNG_TaskResultKind.Accepted));
        }

        [TestCase(false, true, 0f, MNG_TaskResultKind.RejectedOwner)]
        [TestCase(true, false, 0f, MNG_TaskResultKind.RejectedInvalidState)]
        [TestCase(true, true, 3f, MNG_TaskResultKind.Expired)]
        public void EveryRejectedRequestHasAReason(bool owner, bool valid, float now, MNG_TaskResultKind reason)
        {
            var life = new MNG_TaskLifetime();
            Assert.That(life.Request(Task(1, MNG_PlayerSkill.Press), 7, now, owner, valid, false).Kind, Is.EqualTo(reason));
            Assert.That(life.Current.Skill, Is.EqualTo(MNG_PlayerSkill.None));
        }

        [Test]
        public void StaleRequestCannotReplaceCurrentAndCancelClearsQueue()
        {
            var life = new MNG_TaskLifetime();
            life.Request(Task(3, MNG_PlayerSkill.Press), 3, 0, true, true, false);
            Assert.That(life.Request(Task(2, MNG_PlayerSkill.Cover), 4, 0, true, true, false).Kind, Is.EqualTo(MNG_TaskResultKind.RejectedStale));
            life.Request(Task(4, MNG_PlayerSkill.Cover), 4, 0, true, true, true);
            life.Cancel(MNG_TaskResultKind.CancelledMatchState, 5);
            Assert.That(life.HasDeferred, Is.False);
            Assert.That(life.Current.Skill, Is.EqualTo(MNG_PlayerSkill.None));
        }

        [TestCase(Team.Red)]
        [TestCase(Team.Navy)]
        public void ObservationAppendsActualExecutionAndPreservesLegacyPrefix(Team team)
        {
            var sign = team == Team.Red ? 1 : -1;
            var snapshot = new MNG_MatchSnapshot();
            snapshot.SetPlayer(team, 0, new MNG_PlayerState { Active = true,
                ExecutingSkill = MNG_PlayerSkill.AimPass, ExecutingPhase = MNG_TaskPhase.Preparing,
                TaskRemainingSeconds = 1.5f, ExecutingReceiverIndex = 3,
                ExecutingTarget = new Vector2(sign * 10, sign * 4) });
            var legacy = new float[133]; var output = new float[244];
            Assert.That(MNG_ObservationWriter.WriteV2(snapshot, team, new MNG_TeamDecisionState(), output, legacy), Is.EqualTo(244));
            for (var i = 0; i < 133; i++) Assert.That(output[i], Is.EqualTo(legacy[i]));
            Assert.That(output[137], Is.EqualTo(1));
            Assert.That(output[147], Is.EqualTo(1));
            Assert.That(output[151], Is.EqualTo(.5f));
            Assert.That(output[156], Is.EqualTo(1));
            Assert.That(output[158], Is.EqualTo(10f / snapshot.FieldHalfLength).Within(1e-6f));
            Assert.That(output[159], Is.EqualTo(4f / snapshot.FieldHalfWidth).Within(1e-6f));
            for (var i = 160; i < 241; i++) Assert.That(output[i], Is.Zero);
        }

        [Test]
        public void ActiveIdleHasNoneCategoriesAndInvalidExecutionFails()
        {
            var snapshot = new MNG_MatchSnapshot();
            snapshot.SetPlayer(Team.Red, 0, new MNG_PlayerState { Active = true });
            var output = new float[244];
            MNG_ObservationWriter.WriteV2(snapshot, Team.Red, new MNG_TeamDecisionState(), output, new float[133]);
            Assert.That(output[133], Is.EqualTo(1)); Assert.That(output[146], Is.EqualTo(1)); Assert.That(output[153], Is.EqualTo(1));
            snapshot.SetPlayer(Team.Red, 0, new MNG_PlayerState { Active = true, ExecutingTarget = new Vector2(float.NaN, 0) });
            Assert.Throws<InvalidOperationException>(() => MNG_ObservationWriter.WriteV2(snapshot, Team.Red, new MNG_TeamDecisionState(), output, new float[133]));
        }

        [Test]
        public void DuplicateEventsDoNotChangeRawCounters()
        {
            var profile = ScriptableObject.CreateInstance<MNG_RewardProfile>();
            try
            {
                var ledger = new MNG_EventLedger();
                var evt = new MNG_RewardEvent(MNG_RewardEventKind.ValidShot, 42);
                Assert.That(ledger.TryRecord(evt, profile, 0, out _), Is.True);
                Assert.That(ledger.TryRecord(evt, profile, 0, out _), Is.False);
                Assert.That(ledger.RawCount(evt.Kind), Is.EqualTo(1));
                for (var id = 43; id < 53; id++) ledger.TryRecord(new MNG_RewardEvent(evt.Kind, id), profile, 0, out _);
                Assert.That(ledger.RawCount(evt.Kind), Is.EqualTo(11));
                Assert.That(ledger.CappedCount(evt.Kind), Is.GreaterThan(0));
                Assert.That(ledger.RewardedCount(evt.Kind), Is.LessThan(ledger.RawCount(evt.Kind)));
                ledger.ResetEpisode(); Assert.That(ledger.RawCount(evt.Kind), Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }

        [Test]
        public void V1ModelsFailV2Contract()
        {
            Assert.Throws<InvalidOperationException>(() => MNG_RuntimeV2.ValidateModelContract("v1", "MNG_Manager", 133));
            Assert.DoesNotThrow(() => MNG_RuntimeV2.ValidateModelContract(MNG_RuntimeV2.ObservationSchema, MNG_RuntimeV2.BehaviorName, 244));
        }
    }
}
