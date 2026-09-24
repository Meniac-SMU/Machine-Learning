using NUnit.Framework;
using UnityEngine;

namespace MachineLearning.Soccer.Manager.Tests
{
    public sealed class MNG_MS1ContractTests
    {
        [Test]
        public void PolicyAssistDefaultsToExactActionPassthrough()
        {
            Assert.That(
                MNG_PolicyAssist.Resolve(System.Array.Empty<string>()),
                Is.EqualTo(MNG_PolicyAssistMode.None));
            Assert.That(
                MNG_PolicyAssist.Resolve(
                    new[] { "player.exe", "-mngPolicyAssistMode", "none" }),
                Is.EqualTo(MNG_PolicyAssistMode.None));
            Assert.That(
                MNG_PolicyAssist.Resolve(
                    new[] { "player.exe", "-mngMS1LearnPassChoice", "true" }),
                Is.EqualTo(MNG_PolicyAssistMode.None));
        }

        [Test]
        public void LegacyBlockedPassOverrideRequiresExplicitMode()
        {
            Assert.That(
                MNG_PolicyAssist.Resolve(
                    new[] { "player.exe", "-mngPolicyAssistMode", "legacy" }),
                Is.EqualTo(MNG_PolicyAssistMode.LegacyBlockedForwardPass));
            Assert.Throws<System.InvalidOperationException>(() =>
                MNG_PolicyAssist.Resolve(
                    new[] { "player.exe", "-mngPolicyAssistMode", "unexpected" }));
        }

        [Test]
        public void SchedulerUsesExactHalfAttackAndDefense()
        {
            var attack = 0;
            var defense = 0;
            for (var episode = 0; episode < 200; episode++)
            {
                if (MNG_MS1Controller.SituationForEpisode(episode)
                    == MNG_MS1SituationGroup.Attack) attack++;
                else defense++;
            }
            Assert.That(attack, Is.EqualTo(100));
            Assert.That(defense, Is.EqualTo(100));
        }

        [Test]
        public void DefenseSchedulerUsesHalfNeutralAndBalancesDirectWide()
        {
            var neutral = 0;
            var direct = 0;
            var wide = 0;
            for (var episode = 0; episode < 200; episode++)
            {
                var source = MNG_MS1Controller.DefenseSourceScenarioIndex(episode);
                var scenario = MNG_DefenseScenarioGenerator.Generate(191001, source, 50f);
                if (scenario.StartsNeutral) neutral++;
                else if (scenario.Kind == MNG_DefenseScenarioKind.Direct) direct++;
                else if (scenario.Kind == MNG_DefenseScenarioKind.Wide) wide++;
            }
            Assert.That(neutral, Is.EqualTo(100));
            Assert.That(direct, Is.EqualTo(50));
            Assert.That(wide, Is.EqualTo(50));
        }

        [Test]
        public void MS1TrainingConstantsMatchFrozenProtocol()
        {
            Assert.That(MNG_MS1Controller.EpisodeSeconds, Is.EqualTo(30f));
            Assert.That(MNG_MS1Controller.DefaultTimeScale, Is.EqualTo(10f));
            Assert.That(MNG_MS1Controller.RecoveryHoldSeconds, Is.EqualTo(0.5f));
            Assert.That(MNG_MS1Controller.TrainingSeed, Is.EqualTo(191001));
            Assert.That(MNG_MS1Controller.EvaluationAttackSeed, Is.EqualTo(405001));
            Assert.That(MNG_MS1Controller.EvaluationDefenseSeed, Is.EqualTo(406001));
            Assert.That(MNG_MS1EvaluationController.EvaluationEpisodes, Is.EqualTo(120));
            Assert.That(MNG_MS1EvaluationController.GateEpisodesPerGroup, Is.EqualTo(40));
            Assert.That(MNG_MS1EvaluationController.PassSubsetEpisodes, Is.EqualTo(20));
        }

        [Test]
        public void FinalEvaluationScheduleContainsExactPassSubset()
        {
            var attack = 0;
            var defense = 0;
            var pass = 0;
            for (var episode = 0;
                 episode < MNG_MS1EvaluationController.EvaluationEpisodes;
                 episode++)
            {
                if (MNG_MS1Controller.SituationForEpisode(episode)
                    == MNG_MS1SituationGroup.DefenseTransition)
                {
                    defense++;
                    continue;
                }
                attack++;
                var scenario = MNG_AttackScenarioGenerator.Generate(
                    MNG_MS1Controller.EvaluationAttackSeed,
                    episode / 2,
                    55f);
                if (scenario.Kind == MNG_AttackScenarioKind.Pass) pass++;
            }
            Assert.That(attack, Is.EqualTo(60));
            Assert.That(defense, Is.EqualTo(60));
            Assert.That(pass, Is.EqualTo(MNG_MS1EvaluationController.PassSubsetEpisodes));
        }

        [Test]
        public void CompletedPassRewardIsSlightlyRaisedWithoutChangingCap()
        {
            var profile = ScriptableObject.CreateInstance<MNG_RewardProfile>();
            try
            {
                Assert.That(profile.GetNominalReward(MNG_RewardEventKind.CompletedPass),
                    Is.EqualTo(0.06f).Within(0.0001f));
                Assert.That(profile.GetPerKindCap(MNG_RewardEventKind.CompletedPass),
                    Is.EqualTo(0.10f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void ExplicitBlockedPassDecisionRewardIsSmallAndCappedOnce()
        {
            var profile = ScriptableObject.CreateInstance<MNG_RewardProfile>();
            try
            {
                Assert.That(profile.GetNominalReward(
                    MNG_RewardEventKind.BlockedForwardPassDecision),
                    Is.EqualTo(0.01f).Within(0.0001f));
                Assert.That(profile.GetPerKindCap(
                    MNG_RewardEventKind.BlockedForwardPassDecision),
                    Is.EqualTo(0.01f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(profile);
            }
        }

        [Test]
        public void CompletedPassPhysicalThresholdsAreSlightlyRelaxed()
        {
            Assert.That(MNG_TacticalRewardTracker.CompletedPassMinimumTravel,
                Is.EqualTo(2.5f).Within(0.0001f));
            Assert.That(MNG_TacticalRewardTracker.CompletedPassHoldSeconds,
                Is.EqualTo(0.15f).Within(0.0001f));
        }

        [Test]
        public void PassRepairScheduleUsesThreePassAttacksAndOneDefense()
        {
            for (var episode = 0; episode < 40; episode++)
            {
                var expected = episode % 4 == 3
                    ? MNG_MS1SituationGroup.DefenseTransition
                    : MNG_MS1SituationGroup.Attack;
                Assert.That(MNG_MS1Controller.PassRepairSituationForEpisode(episode),
                    Is.EqualTo(expected));
            }

            for (var attack = 0; attack < 30; attack++)
            {
                var source = MNG_MS1Controller.PassRepairSourceScenarioIndex(attack);
                var scenario = MNG_AttackScenarioGenerator.Generate(191001, source, 55f);
                Assert.That(scenario.Kind, Is.EqualTo(MNG_AttackScenarioKind.Pass));
            }
        }

        [Test]
        public void PassFixtureHasOpenForwardOutletAndBlockedCarryLane()
        {
            var source = MNG_AttackScenarioGenerator.Generate(191001, 2, 55f);
            Assert.That(source.Kind, Is.EqualTo(MNG_AttackScenarioKind.Pass));
            var prepared = MNG_MS1Controller.PrepareAttackScenarioForMS1(source);
            var ball = prepared.BallPosition;
            Assert.That(prepared.RedPositions[2].x - ball.x, Is.EqualTo(8f).Within(0.001f));
            Assert.That(Mathf.Abs(prepared.RedPositions[2].y - ball.y), Is.EqualTo(7f).Within(0.001f));
            Assert.That(prepared.NavyPositions[1].x - ball.x, Is.EqualTo(3f).Within(0.001f));
            Assert.That(Mathf.Abs(prepared.NavyPositions[1].y - ball.y), Is.EqualTo(1f).Within(0.001f));
            Assert.That(Vector2.Distance(prepared.NavyPositions[1], prepared.RedPositions[2]),
                Is.GreaterThan(7f));
        }

        [Test]
        public void PassRepairPriorityKeepsOnlyPassAndBalancedWhenForwardLaneIsBlocked()
        {
            var source = MNG_AttackScenarioGenerator.Generate(191001, 2, 55f);
            var scenario = MNG_MS1Controller.PrepareAttackScenarioForMS1(source);
            var snapshot = new MNG_MatchSnapshot
            {
                FieldHalfLength = 55f,
                FieldHalfWidth = 35f,
                GoalHalfWidth = 8f,
                BallPosition = scenario.BallPosition,
                Carrier = MNG_CarrierRef.For(Team.Red, scenario.CarrierSlot),
                Possession = MNG_Possession.Red
            };
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
            {
                snapshot.SetPlayer(Team.Red, slot, new MNG_PlayerState
                {
                    Active = true,
                    Position = scenario.RedPositions[slot],
                    Role = (MNG_PlayerRole)slot
                });
                snapshot.SetPlayer(Team.Navy, slot, new MNG_PlayerState
                {
                    Active = true,
                    Position = scenario.NavyPositions[slot],
                    Role = (MNG_PlayerRole)slot
                });
            }
            var decision = new MNG_TeamDecisionState();
            for (var slot = 0; slot < MNG_MatchSnapshot.PlayersPerTeam; slot++)
                decision.ControlMask[slot] = true;
            var mask = new[] { true, true, true, true, true, true };

            MNG_CommandMask.ApplyPassRepairPriority(
                snapshot, Team.Red, decision, mask);

            Assert.That(mask, Is.EqualTo(
                new[] { false, true, false, false, true, false }));

            decision.HasPassTarget = true;
            Assert.That(MNG_CommandMask.IsBlockedForwardPassRecommended(
                snapshot, Team.Red, decision), Is.True);
            Assert.That(MNG_CommandMask.ApplyBlockedForwardPassPriority(
                snapshot, Team.Red, decision, MNG_Command.AdvanceCarry),
                Is.EqualTo(MNG_Command.PassBuild));
            Assert.That(MNG_CommandMask.ApplyBlockedForwardPassPriority(
                snapshot, Team.Red, decision, MNG_Command.Balanced),
                Is.EqualTo(MNG_Command.PassBuild));
            Assert.That(MNG_CommandMask.ApplyBlockedForwardPassPriority(
                snapshot, Team.Red, decision, MNG_Command.ProtectBack),
                Is.EqualTo(MNG_Command.PassBuild));
            Assert.That(MNG_CommandMask.ApplyBlockedForwardPassPriority(
                snapshot, Team.Red, decision, MNG_Command.AttemptShot),
                Is.EqualTo(MNG_Command.AttemptShot));
        }
    }
}
