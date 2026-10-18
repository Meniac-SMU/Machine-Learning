using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveControlsPlayTests
    {
        [UnityTest] public IEnumerator AutomaticArenaRestoresPreviousPhysicsModeWhenClosed()
        {
#if UNITY_EDITOR
            var previous=Physics.simulationMode;
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Soccer/Manager5v5/Prefabs/FiveArena.prefab"));
            try
            {
                yield return null;
                Assert.That(Physics.simulationMode,Is.EqualTo(SimulationMode.Script));
                root.SetActive(false);
                Assert.That(Physics.simulationMode,Is.EqualTo(previous),"Closing the standalone arena must restore the project's previous simulation mode.");
            }
            finally {Object.Destroy(root);Physics.simulationMode=previous;}
#endif
            yield return null;
        }
        [UnityTest] public IEnumerator PhysicalPauseResumeAndManualSelectionRespectHold()
        {
            using(var world=new FiveTestWorld())
            {
                var match=world.match;world.Step(50);yield return null;
                var old=match.players.Select(p=>p.Body.position).ToArray();var ball=match.ball.position;var elapsed=match.Elapsed;
                match.Paused=true;world.Step(100);
                for(int i=0;i<10;i++)Assert.That(Vector3.Distance(old[i],match.players[i].Body.position),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(ball,match.ball.position),Is.LessThan(.001f));Assert.That(match.Elapsed,Is.EqualTo(elapsed));
                match.Paused=false;world.Step(10);Assert.That(match.Elapsed,Is.GreaterThan(elapsed));
                match.SelectCommand(Team.Red,FiveCommand.FastTransition,"hold",true);
                var root=world.root.GetComponentInChildren<UIDocument>().rootVisualElement;
                root.Q<DropdownField>("red-mode").index=8;yield return null;
                Assert.That(match.ruleManagers[0],Is.False);Assert.That(match.Decision(Team.Red).command,Is.EqualTo(FiveCommand.FastTransition));
                world.Step(155);yield return null;yield return null;
                Assert.That(match.Decision(Team.Red).command,Is.EqualTo(FiveCommand.GoalBlock));
                match.ResetMatch();world.Step(5);Assert.That(match.RedScore+match.NavyScore,Is.Zero);Assert.That(match.Elapsed,Is.LessThan(.2));
            }
            yield return null;
        }
    }
}
