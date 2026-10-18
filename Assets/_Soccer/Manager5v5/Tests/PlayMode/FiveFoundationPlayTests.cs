using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveFoundationPlayTests
    {
        [UnityTest]
        public IEnumerator RosterScaleStaminaUIAndRealMovement()
        {
#if UNITY_EDITOR
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Soccer/Manager5v5/Prefabs/FiveArena.prefab");
            Assert.That(prefab,Is.Not.Null);
            var root=Object.Instantiate(prefab); var match=root.GetComponent<FiveMatch>();
            match.automaticTick=false;
            match.techniquesEnabled=false;
            try
            {
                match.ResetMatch(); yield return null;
                Assert.That(match.players.Length,Is.EqualTo(10));
                Assert.That(match.players.Count(p=>p.IsKeeper),Is.EqualTo(2));
                Assert.That(root.GetComponentsInChildren<FiveStamina>().Length,Is.EqualTo(8));
                Assert.That(root.GetComponentInChildren<FiveHud>().BarCount,Is.EqualTo(8));
                Assert.That(Vector3.Distance(match.ball.transform.localScale,match.baselineBallScale*.9f),Is.LessThan(.00001));
                foreach(var p in match.players) Assert.That(Vector3.Distance(p.transform.localScale,p.baselineScale*.9f),Is.LessThan(.00001));
                var mover=match.players[1]; var start=mover.Position;
                mover.SetTarget(start+Vector2.right*35,start+Vector2.right*35);
                for(var i=0;i<100;i++){match.Tick(.02f);yield return new WaitForFixedUpdate();}
                Assert.That(Vector2.Distance(mover.Position,start),Is.GreaterThan(12),"Rigidbody must really move.");
                Assert.That(mover.Stamina.Value,Is.LessThan(.995f));
                mover.Stamina.SetForScenario(.4f); mover.Dribbling=true;
                for(var i=0;i<25;i++){match.Tick(.02f);yield return new WaitForFixedUpdate();}
                Assert.That(mover.Velocity.magnitude,Is.LessThanOrEqualTo(9*.8f+.03f));
                var beforeGoal=mover.Stamina.Value;match.Score(Team.Red);match.Tick(.02f);
                Assert.That(mover.Stamina.Value,Is.EqualTo(beforeGoal));
                match.ResetRound();Assert.That(mover.Stamina.Value,Is.EqualTo(beforeGoal));
                match.Paused=true;match.Tick(1);Assert.That(mover.Stamina.Value,Is.EqualTo(beforeGoal));
                match.ResetMatch();Assert.That(mover.Stamina.Value,Is.EqualTo(1));
                Assert.That(match.RedScore,Is.Zero);
            }
            finally {Object.Destroy(root);}
            yield return null;
#else
            Assert.Ignore("Editor prefab integration test."); yield break;
#endif
        }
    }
}
