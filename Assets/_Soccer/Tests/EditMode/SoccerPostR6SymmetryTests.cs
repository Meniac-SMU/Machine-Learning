using System.Linq;
using System.Reflection;
using MachineLearning.Soccer.Teams.Rule;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MachineLearning.Soccer.Tests
{
    public sealed class SoccerPostR6SymmetryTests
    {
        [TestCase(AgentSoccer.Position.Striker, 0f)]
        [TestCase(AgentSoccer.Position.Midfielder, 0f)]
        [TestCase(AgentSoccer.Position.Midfielder, 16f)]
        [TestCase(AgentSoccer.Position.Midfielder, -16f)]
        [TestCase(AgentSoccer.Position.DefenderKeeper, 0f)]
        public void RuleSupportAndFallbackLanesRotateWithTheRoster(AgentSoccer.Position role, float lane)
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/_Soccer/Teams/Rule_PHC/Prefabs/StadiumEnvironment_Rule.prefab");
            try
            {
                var env = root.GetComponent<SoccerEnvController>();
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var support = typeof(RuleBasedSoccerController).GetMethod("GetAttackSupportTarget", flags);
                var fallbackLane = typeof(SoccerEnvController).GetMethod("GetLaneSign", BindingFlags.Static | BindingFlags.NonPublic);
                var targets = new Vector3[2]; var lanes = new float[2];
                for (var side = 0; side < 2; side++)
                {
                    var sign = side == 0 ? 1f : -1f;
                    var agent = root.GetComponentsInChildren<AgentSoccer>(true).First(a => (int)a.Team == side);
                    agent.Configure((Team)side, role, false, new Vector3(-sign * 15, .5f, sign * lane));
                    env.ball.transform.position = new Vector3(sign * 3, .5f, sign * 4);
                    var rule = agent.GetComponent<RuleBasedSoccerController>();
                    if (rule == null) rule = agent.gameObject.AddComponent<RuleBasedSoccerController>();
                    rule.Configure(agent, env);
                    targets[side] = (Vector3)support.Invoke(rule, null);
                    lanes[side] = (float)fallbackLane.Invoke(null, new object[] { agent });
                }
                Assert.That(targets[0].x, Is.EqualTo(-targets[1].x).Within(.0001f));
                Assert.That(targets[0].z, Is.EqualTo(-targets[1].z).Within(.0001f));
                Assert.That(lanes[0], Is.EqualTo(-lanes[1]));
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
