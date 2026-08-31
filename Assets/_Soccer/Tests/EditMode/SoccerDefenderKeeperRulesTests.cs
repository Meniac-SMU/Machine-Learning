using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MachineLearning.Soccer.Tests
{
    public sealed class SoccerDefenderKeeperRulesTests
    {
        const float Tolerance = 0.0001f;

        static readonly string[] WorkspacePrefabPaths =
        {
            "Assets/_Soccer/Core/Prefabs/StadiumEnvironment_Base.prefab",
            "Assets/_Soccer/Teams/Attack_KMW/Prefabs/StadiumEnvironment_Attack.prefab",
            "Assets/_Soccer/Teams/Defense_PJH/Prefabs/StadiumEnvironment_Defense.prefab",
            "Assets/_Soccer/Teams/Press_KMG/Prefabs/StadiumEnvironment_Press.prefab",
            "Assets/_Soccer/Teams/Rule_PHC/Prefabs/StadiumEnvironment_Rule.prefab"
        };

        [Test]
        public void AttackingDepthUsesMirroredCoordinatesForRedAndNavy()
        {
            foreach (var depth in new[] { -54f, -4f, 0f, 17f, 54f })
            {
                Assert.That(
                    SoccerDefenderKeeperRules.GetAttackingDepth(Team.Red, depth),
                    Is.EqualTo(depth).Within(Tolerance));
                Assert.That(
                    SoccerDefenderKeeperRules.GetAttackingDepth(Team.Navy, -depth),
                    Is.EqualTo(depth).Within(Tolerance));
            }
        }

        [Test]
        public void FiveWorkspacePrefabsConstrainBothKeeperTargetsBehindTheSoftHalfLine()
        {
            foreach (var prefabPath in WorkspacePrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                Assert.IsNotNull(prefab, prefabPath);

                var environment = prefab.GetComponent<SoccerEnvController>();
                Assert.IsNotNull(environment, prefabPath);
                var agents = prefab.GetComponentsInChildren<AgentSoccer>(true);

                foreach (var team in new[] { Team.Red, Team.Navy })
                {
                    var keeper = agents.Single(agent =>
                        agent.Team == team
                        && agent.PositionRole == AgentSoccer.Position.DefenderKeeper);
                    var attackSign = SoccerDefenderKeeperRules.GetAttackSign(team);
                    var requestedTarget = new Vector3(attackSign * 50f, keeper.transform.position.y, 7f);
                    var constrainedTarget = SoccerDefenderKeeperRules.ConstrainTarget(
                        keeper,
                        environment,
                        requestedTarget);
                    var constrainedDepth = SoccerDefenderKeeperRules.GetAttackingDepth(
                        team,
                        constrainedTarget.x);

                    Assert.That(
                        constrainedDepth,
                        Is.LessThanOrEqualTo(SoccerDefenderKeeperRules.SoftHalfLineDepth + Tolerance),
                        $"{prefabPath}/{team} defender-keeper target crossed the -4m support line.");
                }
            }
        }

        [Test]
        public void HardHalfLineForcesRecoveryAndRemovesAttackingMovementAndVelocity()
        {
            foreach (var team in new[] { Team.Red, Team.Navy })
            {
                var context = CreateContext(team, AgentSoccer.Position.DefenderKeeper);
                try
                {
                    var attackDirection = Vector3.right * SoccerDefenderKeeperRules.GetAttackSign(team);
                    context.Agent.transform.position = new Vector3(
                        SoccerDefenderKeeperRules.HardHalfLineDepth
                            * SoccerDefenderKeeperRules.GetAttackSign(team),
                        0.5f,
                        0f);

                    var constrainedMovement = SoccerDefenderKeeperRules.ConstrainMovement(
                        context.Agent,
                        context.Environment,
                        attackDirection + Vector3.forward * 0.4f,
                        out var mode);
                    Assert.AreEqual(
                        SoccerDefenderKeeperRules.DefenderKeeperMode.RecoverGoal,
                        mode,
                        $"{team} defender-keeper did not enter recovery at the hard line.");
                    Assert.That(
                        Vector3.Dot(constrainedMovement, attackDirection),
                        Is.LessThanOrEqualTo(Tolerance),
                        $"{team} defender-keeper retained attacking movement while recovering.");

                    var constrainedVelocity = SoccerDefenderKeeperRules.ConstrainVelocity(
                        context.Agent,
                        attackDirection * 7f + Vector3.forward * 2f + Vector3.up,
                        mode);
                    Assert.That(
                        Vector3.Dot(constrainedVelocity, attackDirection),
                        Is.EqualTo(0f).Within(Tolerance),
                        $"{team} defender-keeper retained attacking velocity while recovering.");
                    Assert.That(constrainedVelocity.z, Is.EqualTo(2f).Within(Tolerance));
                    Assert.That(constrainedVelocity.y, Is.EqualTo(1f).Within(Tolerance));

                    var constrainedTarget = SoccerDefenderKeeperRules.ConstrainTarget(
                        context.Agent,
                        context.Environment,
                        attackDirection * 40f);
                    Assert.That(
                        constrainedTarget.x,
                        Is.EqualTo(context.Agent.StartingPosition.x).Within(Tolerance),
                        $"{team} defender-keeper recovery target was not the goal-front starting depth.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(context.Root);
                }
            }
        }

        [Test]
        public void NonKeeperTargetsMovementAndVelocityRemainUnchanged()
        {
            foreach (var team in new[] { Team.Red, Team.Navy })
            {
                var context = CreateContext(team, AgentSoccer.Position.Striker);
                try
                {
                    context.Agent.transform.position = new Vector3(0f, 0.5f, 0f);
                    var requestedTarget = new Vector3(31f, 0.5f, -9f);
                    var requestedMovement = new Vector3(0.8f, 0f, -0.35f);
                    var requestedVelocity = new Vector3(5f, 0.75f, -2f);

                    Assert.AreEqual(
                        requestedTarget,
                        SoccerDefenderKeeperRules.ConstrainTarget(
                            context.Agent,
                            context.Environment,
                            requestedTarget));
                    Assert.AreEqual(
                        requestedMovement,
                        SoccerDefenderKeeperRules.ConstrainMovement(
                            context.Agent,
                            context.Environment,
                            requestedMovement,
                            out var mode));
                    Assert.AreEqual(SoccerDefenderKeeperRules.DefenderKeeperMode.Free, mode);
                    Assert.AreEqual(
                        requestedVelocity,
                        SoccerDefenderKeeperRules.ConstrainVelocity(
                            context.Agent,
                            requestedVelocity,
                            SoccerDefenderKeeperRules.DefenderKeeperMode.RecoverGoal));
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(context.Root);
                }
            }
        }

        [Test]
        public void LooseThreatInDefensiveZoneMakesBothKeepersEngageBallInsteadOfOnlyRecoveringHome()
        {
            foreach (var team in new[] { Team.Red, Team.Navy })
            {
                var context = CreateContext(team, AgentSoccer.Position.DefenderKeeper, true);
                try
                {
                    var attackSign = SoccerDefenderKeeperRules.GetAttackSign(team);
                    context.Ball.transform.position = new Vector3(-attackSign * 24f, 0.5f, 12f);
                    context.Agent.transform.position = context.Agent.StartingPosition;

                    var mode = SoccerDefenderKeeperRules.GetMode(context.Agent, context.Environment);
                    var home = SoccerDefenderKeeperRules.GetHomeTarget(context.Agent, context.Environment);
                    var engagement = SoccerDefenderKeeperRules.GetEngagementTarget(
                        context.Agent,
                        context.Environment);
                    var homeDepth = SoccerDefenderKeeperRules.GetAttackingDepth(team, home.x);
                    var engagementDepth = SoccerDefenderKeeperRules.GetAttackingDepth(team, engagement.x);

                    Assert.AreEqual(SoccerDefenderKeeperRules.DefenderKeeperMode.EngageBall, mode);
                    Assert.Greater(engagementDepth, homeDepth,
                        $"{team} keeper should step forward between the ball and its own goal.");
                    Assert.LessOrEqual(
                        engagementDepth,
                        SoccerDefenderKeeperRules.MaximumEngagementTargetDepth + Tolerance);
                    Assert.Greater(Mathf.Abs(engagement.z), Mathf.Abs(home.z),
                        $"{team} keeper should track farther laterally than its passive home target.");
                    Assert.LessOrEqual(Mathf.Abs(engagement.z), 12f + Tolerance);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(context.Root);
                }
            }
        }

        [Test]
        public void BallInsideGoalLetsBothKeepersEnterAndReachTheBallForCentralClearance()
        {
            foreach (var team in new[] { Team.Red, Team.Navy })
            {
                var context = CreateContext(team, AgentSoccer.Position.DefenderKeeper, true);
                try
                {
                    var attackSign = SoccerDefenderKeeperRules.GetAttackSign(team);
                    context.Ball.transform.position = new Vector3(-attackSign * 64f, 0.5f, 4f);
                    var target = SoccerDefenderKeeperRules.ConstrainTarget(
                        context.Agent,
                        context.Environment,
                        Vector3.zero);

                    Assert.AreEqual(
                        SoccerDefenderKeeperRules.DefenderKeeperMode.EngageBall,
                        SoccerDefenderKeeperRules.GetMode(context.Agent, context.Environment));
                    Assert.AreEqual(context.Ball.transform.position.x, target.x, Tolerance);
                    Assert.AreEqual(context.Ball.transform.position.z, target.z, Tolerance);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(context.Root);
                }
            }
        }

        static TestContext CreateContext(Team team, AgentSoccer.Position role, bool includeBall = false)
        {
            var root = new GameObject($"DefenderKeeperRulesTest_{team}_{role}");
            var environment = root.AddComponent<SoccerEnvController>();
            var agentObject = new GameObject("Agent");
            agentObject.transform.SetParent(root.transform);
            var agent = agentObject.AddComponent<AgentSoccer>();
            var attackSign = SoccerDefenderKeeperRules.GetAttackSign(team);
            agent.Configure(team, role, false, new Vector3(-attackSign * 54f, 0.5f, 0f));
            agent.transform.position = agent.StartingPosition;
            GameObject ball = null;
            if (includeBall)
            {
                ball = new GameObject("Ball");
                ball.transform.SetParent(root.transform);
                environment.ball = ball;
                environment.ballRb = ball.AddComponent<Rigidbody>();
            }

            return new TestContext(root, environment, agent, ball);
        }

        readonly struct TestContext
        {
            public TestContext(
                GameObject root,
                SoccerEnvController environment,
                AgentSoccer agent,
                GameObject ball)
            {
                Root = root;
                Environment = environment;
                Agent = agent;
                Ball = ball;
            }

            public GameObject Root { get; }
            public SoccerEnvController Environment { get; }
            public AgentSoccer Agent { get; }
            public GameObject Ball { get; }
        }
    }
}
