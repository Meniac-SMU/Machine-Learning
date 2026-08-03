using System.Linq;
using NUnit.Framework;
using Unity.MLAgents.Policies;
using UnityEditor;
using UnityEngine;

namespace MachineLearning.Soccer.Tests
{
    public sealed class SoccerTeamWorkspaceTests
    {
        const string Root = "Assets/Soccer";

        [Test]
        public void GeneratedPrefabUsesFourPlayerRolesAndV2PolicyContract()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Prefabs/SoccerField4v4.prefab");
            Assert.IsNotNull(prefab);
            var agents = prefab.GetComponentsInChildren<AgentSoccer>(true);
            Assert.AreEqual(8, agents.Length);

            foreach (var team in new[] { Team.Blue, Team.Purple })
            {
                var teamAgents = agents.Where(agent => agent.Team == team).ToArray();
                Assert.AreEqual(1, teamAgents.Count(agent => agent.PositionRole == AgentSoccer.Position.DefenderKeeper));
                Assert.AreEqual(2, teamAgents.Count(agent => agent.PositionRole == AgentSoccer.Position.Midfielder));
                Assert.AreEqual(1, teamAgents.Count(agent => agent.PositionRole == AgentSoccer.Position.Striker));
            }

            foreach (var agent in agents)
            {
                var behavior = agent.GetComponent<BehaviorParameters>();
                CollectionAssert.AreEqual(new[] { 3, 3, 3, 3 }, behavior.BrainParameters.ActionSpec.BranchSizes);
                Assert.AreEqual(AgentSoccer.VectorObservationSize, behavior.BrainParameters.VectorObservationSize);
                Assert.IsNull(behavior.Model, "The shared model is assigned through TeamDefinition, not the common prefab.");
            }
        }

        [Test]
        public void TeamProfilesStartWithExactlyTheBaseRewardValues()
        {
            var baseProfile = LoadProfile(Root + "/Core/Profiles/BaseRewardProfile.asset");
            foreach (var team in new[] { "Attack", "Defense", "Press", "Pass" })
            {
                var teamProfile = LoadProfile($"{Root}/Teams/{team}/Profiles/{team}RewardProfile.asset");
                Assert.AreEqual(EditorJsonUtility.ToJson(baseProfile), EditorJsonUtility.ToJson(teamProfile),
                    $"{team} must start from the unchanged Base reward values.");
            }
        }

        [Test]
        public void TeamDefinitionsUseV2AndInitiallyShareTheBaseModelSlot()
        {
            var definitions = new[]
            {
                LoadDefinition(Root + "/Core/Profiles/BaseTeamDefinition.asset"),
                LoadDefinition(Root + "/Teams/Attack/Profiles/AttackTeamDefinition.asset"),
                LoadDefinition(Root + "/Teams/Defense/Profiles/DefenseTeamDefinition.asset"),
                LoadDefinition(Root + "/Teams/Press/Profiles/PressTeamDefinition.asset"),
                LoadDefinition(Root + "/Teams/Pass/Profiles/PassTeamDefinition.asset")
            };

            Assert.IsTrue(definitions.All(definition =>
                definition.PolicyContractVersion == SoccerTeamDefinition.CurrentPolicyContractVersion));
            Assert.IsTrue(definitions.All(definition => definition.InferenceModel == definitions[0].InferenceModel));
        }

        static SoccerRewardProfile LoadProfile(string path)
        {
            var profile = AssetDatabase.LoadAssetAtPath<SoccerRewardProfile>(path);
            Assert.IsNotNull(profile, path);
            return profile;
        }

        static SoccerTeamDefinition LoadDefinition(string path)
        {
            var definition = AssetDatabase.LoadAssetAtPath<SoccerTeamDefinition>(path);
            Assert.IsNotNull(definition, path);
            return definition;
        }
    }
}
