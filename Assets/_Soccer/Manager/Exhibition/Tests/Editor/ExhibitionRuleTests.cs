using System;
using NUnit.Framework;

namespace MachineLearning.Soccer.Manager.Exhibition.Tests
{
    public class ExhibitionRuleTests
    {
        [TestCase(6)] [TestCase(7)] [TestCase(8)]
        public void BaselinesMatchEvaluationForEveryMask(int model)
        {
            for(int bits=0;bits<64;bits++)
            {
                var valid=new bool[6];for(int i=0;i<6;i++)valid[i]=(bits&(1<<i))!=0;
                valid[4]=true; // Balanced is always valid in the runtime ABI.
                foreach(bool possession in new[]{false,true})
                {
                    int desired=model==6?3:model==8?(valid[2]?2:possession?0:3):4;
                    int expected=valid[desired]?desired:4;
                    Assert.That(ExhibitionRules.Select(model,valid,possession,new Random(42)),Is.EqualTo(expected));
                }
            }
        }
        [Test]
        public void RandomNeverSelectsAnUnavailableCommand()
        {
            var rng=new Random(917);
            for(int bits=1;bits<64;bits++)
            {
                var valid=new bool[6];for(int i=0;i<6;i++)valid[i]=(bits&(1<<i))!=0;
                var seen=new bool[6];
                for(int sample=0;sample<200;sample++){int selected=ExhibitionRules.Select(9,valid,false,rng);Assert.That(valid[selected],Is.True);seen[selected]=true;}
                Assert.That(seen,Is.EqualTo(valid));
            }
        }
        [Test] public void RandomRejectsEmptyMask()=>Assert.Throws<InvalidOperationException>(()=>ExhibitionRules.Select(9,new bool[6],false,new Random(42)));
    }
}
