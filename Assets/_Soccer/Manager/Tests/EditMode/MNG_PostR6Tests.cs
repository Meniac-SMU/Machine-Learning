using System;
using System.Collections.Generic;
using MachineLearning.Soccer;
using NUnit.Framework;
using UnityEngine;

namespace MachineLearning.Soccer.Manager.Tests
{
    public sealed class MNG_PostR6Tests
    {
        [Test]
        public void ConcurrentWorkersKeepIndependentCompleteEvidenceStreams()
        {
            var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "mng-evidence-" + Guid.NewGuid());
            System.IO.Directory.CreateDirectory(directory);
            try
            {
                System.Threading.Tasks.Parallel.For(0, 32, worker =>
                {
                    var path = System.IO.Path.Combine(directory, MNG_MSController.EvidenceFileName("spawns", 6500 + worker, 1000 + worker));
                    for (var reset = 0; reset < 64; reset++) System.IO.File.AppendAllText(path, reset + "\n");
                });
                var files = System.IO.Directory.GetFiles(directory);
                Assert.AreEqual(32, files.Length);
                foreach (var path in files) Assert.AreEqual(64, System.IO.File.ReadAllLines(path).Length);
                Assert.AreNotEqual(MNG_MSController.EvidenceFileName("spawns", 6500, 1000),
                    MNG_MSController.EvidenceFileName("spawns", 6500, 1001));
            }
            finally { System.IO.Directory.Delete(directory, true); }
        }

        public static IEnumerable<TestCaseData> MirrorCases()
        {
            foreach (var command in (MNG_Command[])Enum.GetValues(typeof(MNG_Command)))
            foreach (var own in new[] { false, true })
            foreach (var ball in new[] { Vector2.zero, new Vector2(45, 4), new Vector2(-48, -6) })
            foreach (var v2 in new[] { true, false })
                yield return new TestCaseData(command, own, ball, v2);
        }

        static MNG_MatchSnapshot State(Team perspective, bool own, Vector2 ball)
        {
            var sign = perspective == Team.Red ? 1f : -1f;
            var other = perspective == Team.Red ? Team.Navy : Team.Red;
            var s = new MNG_MatchSnapshot { BallPosition = sign * ball, BallVelocity = sign * new Vector2(.4f, -.2f),
                Possession = own ? (perspective == Team.Red ? MNG_Possession.Red : MNG_Possession.Navy) : MNG_Possession.Neutral,
                Carrier = own ? MNG_CarrierRef.For(perspective, 3) : MNG_CarrierRef.None };
            var positions = new[] { new Vector2(-55, 0), new Vector2(-12, -16), new Vector2(-12, 16), ball };
            for (var slot = 0; slot < 4; slot++)
            {
                var p = new MNG_PlayerState { Active = true, Role = (MNG_PlayerRole)slot,
                    Position = sign * positions[slot], Forward = new Vector2(sign, 0), MaximumKickCooldownSeconds = 1 };
                s.SetPlayer(perspective, slot, p);
                p.Position = -p.Position + sign * new Vector2(0, 3); p.Forward = -p.Forward;
                s.SetPlayer(other, slot, p);
            }
            return s;
        }

        [TestCaseSource(nameof(MirrorCases))]
        public void CommandsAndMasksRotateWithTheirTeam(MNG_Command command, bool own, Vector2 ball, bool v2)
        {
            var tasks = new[] { new MNG_PlayerTask[4], new MNG_PlayerTask[4] };
            var masks = new[] { new bool[6], new bool[6] };
            for (var i = 0; i < 2; i++)
            {
                var team = (Team)i; var s = State(team, own, ball); var d = new MNG_TeamDecisionState();
                var targets = MNG_TacticalTargetResolver.Resolve(s, team, d);
                d.HasPassTarget = targets.HasPassTarget; d.PendingPassReceiverSlot = targets.PassReceiverSlot;
                d.HasShotTarget = targets.HasShotTarget;
                MNG_CommandMask.Write(s, team, d, masks[i]);
                if (v2) MNG_TeamPlanner.PlanV2(s, team, command, d, 1, .75f, tasks[i]);
                else MNG_TeamPlanner.Plan(s, team, command, d, 1, .75f, tasks[i]);
            }
            Assert.That(masks[0], Is.EqualTo(masks[1]));
            for (var slot = 0; slot < 4; slot++)
            {
                Assert.That(tasks[0][slot].Skill, Is.EqualTo(tasks[1][slot].Skill), "slot " + slot);
                Assert.That(tasks[0][slot].ReceiverSlot, Is.EqualTo(tasks[1][slot].ReceiverSlot));
                Assert.That((tasks[0][slot].Target + tasks[1][slot].Target).magnitude, Is.LessThan(.0001f), "slot " + slot);
            }
        }

        [TestCase(Team.Red, false)] [TestCase(Team.Red, true)]
        [TestCase(Team.Navy, false)] [TestCase(Team.Navy, true)]
        public void NeutralTieUsesConfiguredPriorityAndSurvivesRoundReset(Team first, bool reverse)
        {
            var ledger = new MNG_PossessionLedger(); ledger.ConfigureNeutralPriority(first);
            for (var round = 0; round < 2; round++)
            {
                var candidates = new MNG_PossessionCandidate[2];
                for (var i = 0; i < 2; i++) candidates[reverse ? 1 - i : i] = new MNG_PossessionCandidate
                { Team = (Team)i, Slot = 3, Active = true, HasPhysicalContact = true, CenterDistance = .1f, AcquisitionDistance = .5f };
                for (var tick = 0; tick < 12; tick++) ledger.Update(candidates, 2, .02f);
                Assert.That(ledger.Carrier.IsValid, Is.True);
                Assert.That(ledger.Carrier.Team, Is.EqualTo(round == 0 ? first : (Team)(1 - (int)first)));
                // The existing owner remains preferred despite the next neutral priority switching.
                for (var tick = 0; tick < 12; tick++) ledger.Update(candidates, 2, .02f);
                Assert.That(ledger.NeutralTieWins(ledger.Carrier.Team), Is.EqualTo(1));
                ledger.Reset();
            }
        }

        [Test]
        public void NearTieSelectionIsIndependentOfThreeCandidatePermutation()
        {
            var orderings = new[] { new[]{0,1,2}, new[]{0,2,1}, new[]{1,0,2}, new[]{1,2,0}, new[]{2,0,1}, new[]{2,1,0} };
            var source = new[] {
                new MNG_PossessionCandidate { Team=Team.Navy, Slot=3, Active=true, HasPhysicalContact=true, CenterDistance=.1f, AcquisitionDistance=.5f },
                new MNG_PossessionCandidate { Team=Team.Red, Slot=2, Active=true, HasPhysicalContact=true, CenterDistance=.100009f, AcquisitionDistance=.5f },
                new MNG_PossessionCandidate { Team=Team.Red, Slot=1, Active=true, HasPhysicalContact=true, CenterDistance=.100018f, AcquisitionDistance=.5f } };
            foreach (var order in orderings)
            {
                var ledger = new MNG_PossessionLedger(); ledger.ConfigureNeutralPriority(Team.Red);
                var candidates = new[] { source[order[0]], source[order[1]], source[order[2]] };
                for (var tick=0; tick<12; tick++) ledger.Update(candidates,3,.02f);
                Assert.That(ledger.Carrier.Team, Is.EqualTo(Team.Red));
                Assert.That(ledger.Carrier.Slot, Is.EqualTo(2), "Outside the true minimum's tie band must not win");
            }
        }

        [Test]
        public void StallEpisodeCountersSurviveRoundResetButNotNewEpisode()
        {
            var tracker = new MNG_GlobalBallStallTracker();
            for (var round = 0; round < 2; round++)
            {
                for (var tick = 0; tick < 100; tick++) tracker.Update(Vector2.zero, Vector2.zero, .02f);
                Assert.That(tracker.ActivationCount, Is.EqualTo(1));
                tracker.Reset();
            }
            Assert.That(tracker.EpisodeActivationCount, Is.EqualTo(2));
            Assert.That(tracker.EpisodeActiveSeconds, Is.GreaterThan(1));
            tracker.ResetEpisode();
            Assert.That(tracker.EpisodeActivationCount, Is.Zero);
            Assert.That(tracker.EpisodeActiveSeconds, Is.Zero);
        }

        [TestCase(Team.Red)] [TestCase(Team.Navy)]
        public void BoundaryNearTieUsesTrueMinimumAfterTeamRotation(Team priority)
        {
            var root = new GameObject("BoundaryNearTie");
            var profile = ScriptableObject.CreateInstance<MNG_PhysicsProfile>();
            try
            {
                var geometry = root.AddComponent<SoccerArenaGeometry>();
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.transform.SetParent(root.transform);
                var body = ball.AddComponent<Rigidbody>();
                body.mass = profile.BallMass;
                var avatars = new MNG_PlayerAvatar[8];
                for (var i = 0; i < 8; i++)
                {
                    var player = new GameObject("Player" + i);
                    player.transform.SetParent(root.transform);
                    player.AddComponent<Rigidbody>();
                    avatars[i] = player.AddComponent<MNG_PlayerAvatar>();
                    avatars[i].Configure((Team)(i / 4), i % 4, (MNG_PlayerRole)(i % 4));
                    avatars[i].Body.position = new Vector3(10, 0, 0);
                }
                var sign = priority == Team.Red ? 1f : -1f;
                // Slot 1 is outside the true minimum's tie band. Slot 2 is inside it.
                avatars[(int)priority * 4 + 1].Body.position = new Vector3(sign * .100018f, 0, 0);
                avatars[(int)priority * 4 + 2].Body.position = new Vector3(sign * .100009f, 0, 0);
                avatars[(1 - (int)priority) * 4 + 3].Body.position = new Vector3(sign * .1f, 0, 0);
                var match = root.AddComponent<MNG_MatchController>();
                match.Configure(geometry, body, avatars);
                var control = ball.AddComponent<MNG_BallControl>();
                control.Configure(profile, match);
                control.ConfigureNeutralPriority(priority);
                var method = typeof(MNG_BallControl).GetMethod("TrySelectBoundaryEscapePlayer",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                object[] args = { Vector2.zero, Team.Red, -1 };
                Assert.That(method.Invoke(control, args), Is.True);
                Assert.That(args[1], Is.EqualTo(priority));
                Assert.That(args[2], Is.EqualTo(2));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void SpawnMirrorSwapsEveryRosterOffsetAtEveryKickoff()
        {
            for (var episode = 0; episode < 3; episode++)
            for (var kickoff = 0; kickoff < 4; kickoff++)
            {
                var a = MNG_MatchController.CreateSpawnOffsets(episode, kickoff, 592301, false);
                var b = MNG_MatchController.CreateSpawnOffsets(episode, kickoff, 592301, true);
                for (var slot = 0; slot < 8; slot++) Assert.That(a[slot], Is.EqualTo(-b[(slot + 4) % 8]));
            }
        }

        [Test]
        public void WallSeparationDoesNotPushOutsideAndRotatesWithTheTeam()
        {
            var a = MNG_PlayerSkillExecutor.ConstrainSeparationDirection(new Vector2(61,24), new Vector2(1,9), new Vector2(1,.2f).normalized,62,42);
            var b = MNG_PlayerSkillExecutor.ConstrainSeparationDirection(new Vector2(-61,-24), new Vector2(-1,-9), new Vector2(-1,-.2f).normalized,62,42);
            Assert.That(a.x, Is.Zero); Assert.That(a.y, Is.LessThan(0)); Assert.That(a, Is.EqualTo(-b));
        }

        [Test]
        public void PassDiagnosticContextUsesActualTargetAndRotatesWithoutChangingDecision()
        {
            var contexts = new MNG_V2Trace.PassContext[2];
            for (var i = 0; i < 2; i++)
            {
                var team = (Team)i;
                var snapshot = State(team, true, Vector2.zero);
                var decision = new MNG_TeamDecisionState { HasPassTarget = true, PendingPassReceiverSlot = 2 };
                var target = MNG_TeamPlanner.SelectPassTarget(snapshot, team, 2);
                contexts[i] = MNG_V2Trace.CapturePassContext(snapshot, team, decision);
                Assert.That(contexts[i].targetDistance, Is.EqualTo(Vector2.Distance(snapshot.GetPlayer(team,3).Position,target)).Within(.0001f));
                Assert.That(decision.PendingPassReceiverSlot, Is.EqualTo(2));
                Assert.That(decision.HasPassTarget, Is.True);
                Assert.That(snapshot.BallPosition, Is.EqualTo(Vector2.zero));
            }
            Assert.That(contexts[0].targetDistance, Is.EqualTo(contexts[1].targetDistance).Within(.0001f));
            Assert.That(contexts[0].laneClearance, Is.EqualTo(contexts[1].laneClearance).Within(.0001f));
            Assert.That(contexts[0].forwardBlocked, Is.EqualTo(contexts[1].forwardBlocked));
        }

        [Test]
        public void CenterlineShotAndOverlappingSeparationAreTeamRelative()
        {
            var s = new MNG_MatchSnapshot();
            var a = MNG_TeamPlanner.SelectShotTarget(s, Team.Red, 3);
            var b = MNG_TeamPlanner.SelectShotTarget(s, Team.Navy, 3);
            Assert.That(a, Is.EqualTo(-b));
            a = MNG_PlayerSkillExecutor.CalculateEmergencySeparationDirection(Vector2.zero, Vector2.zero, 2, 1, Team.Red);
            b = MNG_PlayerSkillExecutor.CalculateEmergencySeparationDirection(Vector2.zero, Vector2.zero, 2, 1, Team.Navy);
            Assert.That(a, Is.EqualTo(-b));
        }
    }
}
