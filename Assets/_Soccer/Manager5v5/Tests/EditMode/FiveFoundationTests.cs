using NUnit.Framework;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveFoundationTests
    {
        [TestCase(1,false,1)] [TestCase(.7001f,false,1)] [TestCase(.7f,false,.95f)]
        [TestCase(.4001f,false,.95f)] [TestCase(.4f,false,.9f)] [TestCase(0,false,.9f)]
        [TestCase(1,true,.9f)] [TestCase(.7f,true,.85f)] [TestCase(.4f,true,.8f)] [TestCase(0,true,.8f)]
        public void SpeedBoundaries(float stamina,bool dribble,float expected)
            => Assert.That(FiveStamina.SpeedFactor(stamina,dribble),Is.EqualTo(expected).Within(.00001f));

        [Test]
        public void MovingCostsAndSlowMovementRecoversWithoutErasingCost()
        {
            var go=new GameObject(); var stamina=go.AddComponent<FiveStamina>(); var profile=ScriptableObject.CreateInstance<FiveProfile>();
            try
            {
                stamina.SetForScenario(.5f); stamina.Advance(9,10,profile);
                Assert.That(stamina.Value,Is.EqualTo(.3596f).Within(.0001));
                stamina.SetForScenario(.5f); stamina.Advance(1,10,profile);
                var slow=stamina.Value;
                Assert.That(slow,Is.GreaterThan(.5f));
                stamina.SetForScenario(.5f); stamina.Advance(0,10,profile);
                Assert.That(stamina.Value,Is.GreaterThan(slow));
                stamina.Advance(0,10000,profile); Assert.That(stamina.Value,Is.EqualTo(1));
                stamina.Advance(9,10000,profile); Assert.That(stamina.Value,Is.EqualTo(0));
            }
            finally {Object.DestroyImmediate(go);Object.DestroyImmediate(profile);}
        }

        [TestCase(0,.8f)] [TestCase(1.8f,.77192f)] [TestCase(3.6f,.74384f)] [TestCase(3.6001f,.44383844f)]
        public void FasterRecoveryIncludesTheExpandedSlowMovementBoundary(float speed,float expected)
        {
            var go=new GameObject();var stamina=go.AddComponent<FiveStamina>();var profile=ScriptableObject.CreateInstance<FiveProfile>();
            try {stamina.SetForScenario(.5f);stamina.Advance(speed,10,profile);Assert.That(stamina.Value,Is.EqualTo(expected).Within(.00001f));}
            finally {Object.DestroyImmediate(go);Object.DestroyImmediate(profile);}
        }
    }
}
