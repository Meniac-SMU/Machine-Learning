using System.Collections;
using System.Linq;
using MachineLearning.Soccer;
using NUnit.Framework;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace MachineLearning.Soccer.Manager.Tests
{
    public sealed class MNG_RebuildV2PlayModeTests
    {
        bool m_Stepping;
        float m_Scale;
        [SetUp] public void SetUp() { m_Stepping = Academy.Instance.AutomaticSteppingEnabled; m_Scale = Time.timeScale; }
        [TearDown] public void TearDown() { Academy.Instance.AutomaticSteppingEnabled = m_Stepping; Time.timeScale = m_Scale; }
        [UnityTest]
        public IEnumerator V2SceneRunsFiniteObservationsAndCommandDependentPhysicalMovement()
        {
#if UNITY_EDITOR
            var oldStepping = Academy.Instance.AutomaticSteppingEnabled;
            var oldScale = Time.timeScale;
            Academy.Instance.AutomaticSteppingEnabled = false;
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Soccer/Manager/Curriculum/MS_V2/MNG_MS2V2_Train.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return null;
            var match = Object.FindFirstObjectByType<MNG_MatchController>();
            Assert.That(match.UseRuntimeV2, Is.True);
            foreach (var rule in Object.FindObjectsByType<MNG_RuleBasedManager>(FindObjectsSortMode.None)) rule.enabled = false;
            var results = new Vector3[2];
            var commands = new[] { MNG_Command.ActiveRecover, MNG_Command.ProtectBack };
            for (var run = 0; run < 2; run++)
            {
                match.ResetMatch();
                yield return new WaitForFixedUpdate();
                var starts = new Vector3[4];
                for (var slot = 1; slot < 4; slot++) starts[slot] = match.GetPlayerAvatar(Team.Red, slot).Body.position;
                Assert.That(match.AcceptCommand(Team.Red, commands[run]), Is.True);
                for (var frame = 0; frame < 15; frame++) yield return new WaitForFixedUpdate();
                var press = 0;
                for (var slot = 1; slot < 4; slot++)
                {
                    var avatar = match.GetPlayerAvatar(Team.Red, slot);
                    var executor = avatar.GetComponent<MNG_PlayerSkillExecutor>();
                    if (executor.CurrentTask.Skill == MNG_PlayerSkill.Press) press++;
                    results[run] += avatar.Body.position - starts[slot];
                    Assert.That(float.IsNaN(avatar.Body.position.x), Is.False);
                }
                Assert.That(press, Is.EqualTo(run == 0 ? 2 : 0));
                var observation = new float[244];
                Assert.That(MNG_ObservationWriter.WriteV2(match.Snapshot, Team.Red, match.GetDecisionState(Team.Red), observation, new float[133]), Is.EqualTo(244));
                Assert.That(observation.All(v => !float.IsNaN(v) && !float.IsInfinity(v)), Is.True);
            }
            Assert.That(results[0].magnitude, Is.GreaterThan(.01f));
            Assert.That((results[0] - results[1]).magnitude, Is.GreaterThan(.01f));
            Time.timeScale = oldScale; Academy.Instance.AutomaticSteppingEnabled = oldStepping;
#else
            yield return null;
#endif
        }
    }
}
