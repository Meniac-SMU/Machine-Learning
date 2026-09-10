using NUnit.Framework;
using UnityEngine;

namespace MachineLearning.Soccer.Tests
{
    public sealed class SoccerCurriculumPassRulesTests
    {
        [Test]
        public void SelfForwardLossIsOneShotAndCannotFarmRecoveryOrStationaryPose()
        {
            var tracker = new SoccerCurriculumPassRules.OpportunityLossTracker();
            var receiver = Vector3.right * 8f;
            var goal = Vector3.right * 62f;
            Assert.IsFalse(tracker.Observe(true, false, Vector3.zero, receiver, goal));
            for (var i = 0; i < 20; i++)
                Assert.IsFalse(tracker.Observe(true, false, Vector3.zero, receiver, goal));
            Assert.IsTrue(tracker.Observe(true, false, Vector3.right * 6f, receiver, goal));
            Assert.IsFalse(tracker.Observe(true, false, Vector3.zero, receiver, goal));
            Assert.IsFalse(tracker.Observe(true, false, Vector3.right * 6f, receiver, goal));
        }

        [Test]
        public void PassAdvisorAimsAtTargetWithoutChangingFallback()
        {
            var target = new GameObject("PassTarget");
            try
            {
                var actor = target.AddComponent<AgentSoccer>();
                target.transform.position = new Vector3(8f, 1f, 8f);
                var resolved = SoccerNeuralPassAdvisor.ResolveDirection(Vector3.zero, actor, Vector3.left);
                Assert.AreEqual(1f, resolved.magnitude, .0001f);
                Assert.AreEqual(new Vector3(1f, 0f, 1f).normalized, resolved);
                Assert.AreEqual(Vector3.left,
                    SoccerNeuralPassAdvisor.ResolveDirection(Vector3.zero, null, Vector3.left));
            }
            finally { Object.DestroyImmediate(target); }
        }

        [TestCase(false, false)]
        [TestCase(true, true)]
        public void ControlGapOrKickPermanentlyEndsLossAttribution(bool controlled, bool kicked)
        {
            var tracker = new SoccerCurriculumPassRules.OpportunityLossTracker();
            var receiver = Vector3.right * 8f;
            var goal = Vector3.right * 62f;
            tracker.Observe(true, false, Vector3.zero, receiver, goal);
            Assert.IsFalse(tracker.Observe(controlled, kicked, Vector3.zero, receiver, goal));
            Assert.IsFalse(tracker.Observe(true, false, Vector3.right * 6f, receiver, goal));
        }

        [TestCase(0f, 2f)] // Receiver alone moved too close.
        [TestCase(6f, 20f)] // Self alone would fail but actual target is still valid.
        [TestCase(-10f, 8f)] // Backward travel caused excess distance.
        [TestCase(6f, 17f)] // Target-only failure must not be blamed on carrier.
        [TestCase(float.NaN, 8f)]
        public void LossAttributionExcludesOtherCauses(float passerX, float receiverX)
        {
            var tracker = new SoccerCurriculumPassRules.OpportunityLossTracker();
            var goal = Vector3.right * 62f;
            tracker.Observe(true, false, Vector3.zero, Vector3.right * 8f, goal);
            Assert.IsFalse(tracker.Observe(true, false, Vector3.right * passerX,
                Vector3.right * receiverX, goal));
        }

        [Test]
        public void OpportunityLossMirrorsNavyAndRejectsInitiallyInvalidTarget()
        {
            var tracker = new SoccerCurriculumPassRules.OpportunityLossTracker();
            tracker.Observe(true, false, Vector3.zero, Vector3.left * 8f, Vector3.left * 62f);
            Assert.IsTrue(tracker.Observe(true, false, Vector3.left * 6f,
                Vector3.left * 8f, Vector3.left * 62f));
            tracker = new SoccerCurriculumPassRules.OpportunityLossTracker();
            tracker.Observe(true, false, Vector3.zero, Vector3.right, Vector3.right * 62f);
            Assert.IsFalse(tracker.Observe(true, false, Vector3.zero, Vector3.right * 8f, Vector3.right * 62f));
        }

        [Test]
        public void OpportunityLossIsIsolatedFromL0L1AndRegularPenaltyDelivery()
        {
            foreach (var stage in new[] { SoccerCurriculumRewardStage.L0Possession,
                SoccerCurriculumRewardStage.L1Finish, SoccerCurriculumRewardStage.L1Carry })
                Assert.IsFalse(SoccerRewardEngine.IsRewardAllowed(stage, SoccerRewardKind.CurriculumPassOpportunityLost));
            Assert.IsTrue(SoccerRewardEngine.IsRewardAllowed(SoccerCurriculumRewardStage.L2Pass,
                SoccerRewardKind.CurriculumPassOpportunityLost));
            var root = new GameObject("PenaltyIsolation");
            try
            {
                var engine = root.AddComponent<SoccerRewardEngine>();
                Assert.AreEqual(0f, engine.AwardCurriculumPassOpportunityLoss(null));
                engine.ConfigureCurriculumRewardStage(SoccerCurriculumRewardStage.L2Pass);
                Assert.AreEqual(0f, engine.AwardCurriculumPassOpportunityLoss(null));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(2f, 0f, 2)]
        [TestCase(3f, 0f, 0)]
        [TestCase(16f, 0f, 0)]
        [TestCase(16.01f, 0f, 4)]
        [TestCase(-8f, 0f, 8)]
        [TestCase(0f, 1f, 10)]
        [TestCase(-20f, 0f, 12)]
        [TestCase(float.NaN, 0f, 1)]
        public void DiagnosticRejectionMatchesOriginalPredicateWithoutChangingIt(float x, float z, int expected)
        {
            var receiver = new Vector3(x, 0f, z);
            var goal = Vector3.right * 62f;
            var reason = SoccerCurriculumPassRules.DiagnoseTarget(Vector3.zero, receiver, goal);
            Assert.AreEqual(expected, (int)reason);
            Assert.AreEqual(SoccerCurriculumPassRules.IsAdvantageousTarget(Vector3.zero, receiver, goal),
                reason == SoccerCurriculumPassRules.TargetRejection.None);
        }

        [TestCase(0f, 1f, true)]
        [TestCase(.5f, 1f, true)]
        [TestCase(1f, 1f, false)]
        [TestCase(0f, 0f, false)]
        [TestCase(float.NaN, 1f, false)]
        public void WaitingAssistIsOptInAndNeverEnabledForFinalTask(float difficulty, float requested, bool expected)
        {
            Assert.AreEqual(expected, Curriculum.SoccerCurriculumController.ShouldUseL2WaitingAssist(difficulty, requested));
        }

        [Test]
        public void WaitingReceiverStartsInsideMaximumPassRangeWithSettlingMargin()
        {
            Assert.AreEqual(15.5f, Curriculum.SoccerCurriculumController.L2WaitingReceiverDistance, .0001f);
            Assert.Less(Curriculum.SoccerCurriculumController.L2WaitingReceiverDistance,
                SoccerCurriculumPassRules.MaximumTargetDistance);
        }

        [Test]
        public void WaitingRegistryDefaultsOffClearsAndExcludesHeuristicFallback()
        {
            var root = new GameObject("WaitingRegistry");
            var player = new GameObject("WaitingPlayer");
            try
            {
                var environment = root.AddComponent<SoccerEnvController>();
                var behavior = player.AddComponent<Unity.MLAgents.Policies.BehaviorParameters>();
                var actor = player.AddComponent<AgentSoccer>();
                Assert.IsFalse(environment.IsCurriculumWaitingAgent(actor));
                behavior.BehaviorType = Unity.MLAgents.Policies.BehaviorType.Default;
                environment.SetCurriculumWaitingAgent(actor, true);
                Assert.IsTrue(environment.IsCurriculumWaitingAgent(actor));
                Assert.IsFalse(environment.CanRequestKick(actor));
                behavior.BehaviorType = Unity.MLAgents.Policies.BehaviorType.HeuristicOnly;
                Assert.IsFalse(environment.IsCurriculumWaitingAgent(actor));
                behavior.BehaviorType = Unity.MLAgents.Policies.BehaviorType.Default;
                environment.ClearCurriculumWaitingAgents();
                Assert.IsFalse(environment.IsCurriculumWaitingAgent(actor));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void L2PassRewardsAreIsolatedFromEarlierLessons()
        {
            foreach (var kind in new[] { SoccerRewardKind.CurriculumPassAttempt,
                SoccerRewardKind.CurriculumPassReception, SoccerRewardKind.CurriculumPassDelivered,
                SoccerRewardKind.CurriculumPassDirection })
            {
                Assert.IsTrue(SoccerRewardEngine.IsRewardAllowed(SoccerCurriculumRewardStage.L2Pass, kind));
                foreach (var stage in new[] { SoccerCurriculumRewardStage.L0Possession,
                    SoccerCurriculumRewardStage.L1Finish, SoccerCurriculumRewardStage.L1Carry })
                    Assert.IsFalse(SoccerRewardEngine.IsRewardAllowed(stage, kind));
            }
            foreach (var kind in new[] { SoccerRewardKind.CurriculumDribbleProgress,
                SoccerRewardKind.CurriculumGoal, SoccerRewardKind.CurriculumShotAttempt,
                SoccerRewardKind.PassSuccess, SoccerRewardKind.UnsafeOwnGoalKick })
                Assert.IsFalse(SoccerRewardEngine.IsRewardAllowed(SoccerCurriculumRewardStage.L2Pass, kind));
        }

        [Test]
        public void PassAimGateDefaultsOffExcludesFallbackAndClears()
        {
            var root = new GameObject("PassAimGate");
            var player = new GameObject("PassAimPlayer");
            try
            {
                var environment = root.AddComponent<SoccerEnvController>();
                var behavior = player.AddComponent<Unity.MLAgents.Policies.BehaviorParameters>();
                var actor = player.AddComponent<AgentSoccer>();
                behavior.BehaviorType = Unity.MLAgents.Policies.BehaviorType.Default;
                Assert.IsFalse(environment.IsCurriculumPassAimGateActive(actor));
                environment.ConfigureCurriculumPassAimGate(true);
                Assert.IsTrue(environment.IsCurriculumPassAimGateActive(actor));
                Assert.IsFalse(environment.IsCurriculumPassAimBlocked(actor), "No confirmed possession must not activate a pass gate.");
                behavior.BehaviorType = Unity.MLAgents.Policies.BehaviorType.HeuristicOnly;
                Assert.IsFalse(environment.IsCurriculumPassAimGateActive(actor));
                behavior.BehaviorType = Unity.MLAgents.Policies.BehaviorType.Default;
                environment.ConfigureCurriculumPassAimGate(false);
                Assert.IsFalse(environment.IsCurriculumPassAimGateActive(actor));
            }
            finally
            {
                Object.DestroyImmediate(player);
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TargetMustBeCloserToGoalNotMerelyAnotherTeammate()
        {
            var goal = new Vector3(62f, 0f, 0f);
            Assert.IsTrue(SoccerCurriculumPassRules.IsAdvantageousTarget(Vector3.zero, Vector3.right * 8f, goal));
            Assert.IsFalse(SoccerCurriculumPassRules.IsAdvantageousTarget(Vector3.zero, Vector3.left * 8f, goal));
            Assert.IsFalse(SoccerCurriculumPassRules.IsAdvantageousTarget(Vector3.zero, Vector3.forward * 8f, goal));
            Assert.IsFalse(SoccerCurriculumPassRules.IsAdvantageousTarget(Vector3.zero, Vector3.right * 2f, goal));
            Assert.IsFalse(SoccerCurriculumPassRules.IsAdvantageousTarget(Vector3.zero, Vector3.right * 17f, goal));
        }

        [Test]
        public void AdvantageIsMirroredForNavyAndRejectsInvalidPositions()
        {
            Assert.IsTrue(SoccerCurriculumPassRules.IsAdvantageousTarget(Vector3.zero, Vector3.left * 8f, Vector3.left * 62f));
            Assert.IsFalse(SoccerCurriculumPassRules.IsAdvantageousTarget(Vector3.zero,
                new Vector3(float.NaN, 0f, 0f), Vector3.right * 62f));
        }

        [Test]
        public void ActualKickMustPointAlongReceiverLane()
        {
            Assert.IsTrue(SoccerCurriculumPassRules.IsDirectedAtTarget(Vector3.zero, Vector3.right * 5f, new Vector3(8f, 0f, 2f)));
            Assert.IsFalse(SoccerCurriculumPassRules.IsDirectedAtTarget(Vector3.zero, Vector3.right, new Vector3(8f, 0f, 3f)));
            Assert.IsFalse(SoccerCurriculumPassRules.IsDirectedAtTarget(Vector3.zero, Vector3.left, Vector3.right * 8f));
            Assert.IsFalse(SoccerCurriculumPassRules.IsDirectedAtTarget(Vector3.zero, Vector3.zero, Vector3.right * 8f));
            Assert.IsFalse(SoccerCurriculumPassRules.IsDirectedAtTarget(Vector3.zero,
                new Vector3(float.PositiveInfinity, 0f, 0f), Vector3.right * 8f));
        }

        [TestCase(true, true, true, false, 3f, 1f, .75f, 2.4f, true)]
        [TestCase(true, true, true, false, 2f, .2f, 0f, 1.6f, true)]
        [TestCase(false, true, true, false, 8f, 1f, .75f, 1f, false)]
        [TestCase(true, false, true, false, 8f, 1f, .75f, 1f, false)]
        [TestCase(true, true, false, false, 8f, 1f, .75f, 1f, false)]
        [TestCase(true, true, true, true, 8f, 1f, .75f, 1f, false)]
        [TestCase(true, true, true, false, 1.99f, 1f, .75f, 1f, false)]
        [TestCase(true, true, true, false, 8f, 1f, .74f, 1f, true)]
        [TestCase(true, true, true, false, 8f, .2f, 0f, 1f, true)]
        [TestCase(true, true, true, false, 8f, .2f, -.01f, 1f, false)]
        [TestCase(true, true, true, false, 8f, 8.01f, .75f, 1f, false)]
        [TestCase(true, true, true, false, 8f, 1f, .75f, 2.41f, false)]
        [TestCase(true, true, true, false, 8f, .5f, .75f, 1f, false)]
        public void HandoffRequiresDirectedStrikeDisplacementAndConfirmedTargetButNotRetention(
            bool strike, bool differentTeammate, bool targetMatches, bool interrupted,
            float displacement, float flight, float control, float distance, bool expected)
        {
            Assert.AreEqual(expected, SoccerCurriculumPassRules.IsSuccessfulReception(
                strike, differentTeammate, targetMatches, interrupted,
                Vector3.zero, Vector3.right * displacement, flight, control, distance));
        }

        [Test]
        public void StrikeQualityRewardsOnlyForwardAdvantageAndDoesNotApproveNearMiss()
        {
            var receiver = Vector3.right * 8f;
            var goal = Vector3.right * 62f;
            Assert.AreEqual(1f, SoccerCurriculumPassRules.StrikeDirectionQuality(Vector3.zero,
                Vector3.zero, Vector3.right, receiver, goal), .0001f);
            Assert.AreEqual(.5f, SoccerCurriculumPassRules.StrikeDirectionQuality(Vector3.zero,
                Vector3.zero, Vector3.right + Vector3.forward, receiver, goal), .0001f);
            Assert.IsFalse(SoccerCurriculumPassRules.IsDirectedAtTarget(Vector3.zero,
                Vector3.right + Vector3.forward, receiver));
            Assert.AreEqual(0f, SoccerCurriculumPassRules.StrikeDirectionQuality(Vector3.zero,
                Vector3.zero, Vector3.left, receiver, goal));
            Assert.AreEqual(0f, SoccerCurriculumPassRules.StrikeDirectionQuality(Vector3.zero,
                Vector3.zero, Vector3.left, Vector3.left * 8f, goal));
        }

        [Test]
        public void StrikeQualityRejectsInvalidAndMirrorsNavy()
        {
            Assert.AreEqual(1f, SoccerCurriculumPassRules.StrikeDirectionQuality(Vector3.zero,
                Vector3.zero, Vector3.left, Vector3.left * 8f, Vector3.left * 62f), .0001f);
            foreach (var direction in new[] { Vector3.zero, new Vector3(float.NaN, 0f, 0f) })
                Assert.AreEqual(0f, SoccerCurriculumPassRules.StrikeDirectionQuality(Vector3.zero,
                    Vector3.zero, direction, Vector3.right * 8f, Vector3.right * 62f));
        }

        [Test]
        public void QualityHighWaterMarkCannotFarmRepeatedKicksOrExceedRoundCap()
        {
            float best = 0f, total = 0f;
            foreach (var quality in new[] { .5f, .2f, .5f, .8f, .1f, 1f, 1f })
            {
                total += SoccerCurriculumPassRules.DirectionQualityIncrement(best, quality);
                best = Mathf.Max(best, quality);
            }
            Assert.AreEqual(1f, total, .0001f);
            Assert.AreEqual(.1f, total * Curriculum.SoccerCurriculumController.L2PassDirectionRewardCap, .0001f);
            Assert.AreEqual(0f, SoccerCurriculumPassRules.DirectionQualityIncrement(0f, float.NaN));
        }

        [Test]
        public void PreparationBaselineIsUnpaidAndOnlyNewBestQualityEarnsReward()
        {
            const float baseline = .4f;
            Assert.AreEqual(0f, SoccerCurriculumPassRules.DirectionQualityIncrement(baseline, baseline));
            Assert.AreEqual(0f, SoccerCurriculumPassRules.DirectionQualityIncrement(baseline, .2f));
            Assert.AreEqual(.2f, SoccerCurriculumPassRules.DirectionQualityIncrement(baseline, .6f), .0001f);
        }

        [Test]
        public void DirectionUsesExplicitRoundCapInsteadOfGenericProfileCap()
        {
            var method = typeof(SoccerRewardEngine).GetMethod("BypassesProfileShapingCap",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            Assert.AreEqual(true, method.Invoke(null, new object[] { SoccerRewardKind.CurriculumPassDirection }));
            Assert.AreEqual(.15f, Curriculum.SoccerCurriculumController.L2PassAttemptReward, .0001f);
        }

        [Test]
        public void InvalidReceptionNumbersNeverPass()
        {
            Assert.IsFalse(SoccerCurriculumPassRules.IsSuccessfulReception(true, true, true, false,
                Vector3.zero, Vector3.right * 8f, float.PositiveInfinity, .75f, 1f));
            Assert.IsFalse(SoccerCurriculumPassRules.IsSuccessfulReception(true, true, true, false,
                Vector3.zero, Vector3.right * 8f, 1f, float.NaN, 1f));
        }
    }
}
