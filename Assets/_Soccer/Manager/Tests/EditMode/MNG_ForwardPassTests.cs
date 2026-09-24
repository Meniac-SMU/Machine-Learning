using MachineLearning.Soccer;
using NUnit.Framework;
using UnityEngine;

namespace MachineLearning.Soccer.Manager.Tests
{
    public sealed class MNG_ForwardPassTests
    {
        static MNG_MatchSnapshot Fixture(Team team, int carrier)
        {
            var sign = team == Team.Red ? 1f : -1f;
            var s = new MNG_MatchSnapshot { Carrier = MNG_CarrierRef.For(team, carrier),
                Possession = team == Team.Red ? MNG_Possession.Red : MNG_Possession.Navy };
            s.SetPlayer(team, carrier, new MNG_PlayerState { Active=true, Position=Vector2.zero });
            s.SetPlayer(team, 1, new MNG_PlayerState { Active=true, Position=sign*new Vector2(10,6) });
            return s;
        }
        [TestCase(Team.Red,0)] [TestCase(Team.Red,3)]
        [TestCase(Team.Navy,0)] [TestCase(Team.Navy,3)]
        public void PassRequiresAheadOpenLaneAndDistanceFromGoal(Team team, int carrier)
        {
            var s=Fixture(team,carrier);var sign=team==Team.Red?1f:-1f;
            Assert.IsTrue(MNG_TacticalTargetResolver.Resolve(s,team).HasPassTarget);
            var receiver=s.GetPlayer(team,1);receiver.Position=sign*new Vector2(-5,6);s.SetPlayer(team,1,receiver);
            Assert.IsFalse(MNG_TacticalTargetResolver.Resolve(s,team).HasPassTarget,"Backward teammate cannot become forward merely through lead offset");
            receiver.Position=sign*new Vector2(10,6);s.SetPlayer(team,1,receiver);
            var target=MNG_TeamPlanner.SelectPassTarget(s,team,1);
            var other=team==Team.Red?Team.Navy:Team.Red;
            s.SetPlayer(other,3,new MNG_PlayerState {Active=true,Position=target*.5f});
            Assert.IsFalse(MNG_TacticalTargetResolver.Resolve(s,team).HasPassTarget,"Opponent blocks actual kick segment");
            s.SetPlayer(other,3,default);
            s.BallPosition=new Vector2(sign*(s.FieldHalfLength-24),0);
            var sender=s.GetPlayer(team,carrier);sender.Position=s.BallPosition;s.SetPlayer(team,carrier,sender);
            receiver.Position=s.BallPosition+sign*new Vector2(10,0);s.SetPlayer(team,1,receiver);
            var targets=MNG_TacticalTargetResolver.Resolve(s,team);
            Assert.IsFalse(targets.HasPassTarget);Assert.IsTrue(targets.HasShotTarget);
        }
        [TestCase(Team.Red,30f)] [TestCase(Team.Red,-30f)]
        [TestCase(Team.Navy,30f)] [TestCase(Team.Navy,-30f)]
        public void AdvanceCarryMovesInwardWithoutCrossingCenter(Team team,float lateral)
        {
            var sign=team==Team.Red?1f:-1f;
            var target=MNG_TeamPlanner.EnforceAttackingCarryTarget(new Vector2(0,lateral),new Vector2(sign*10,lateral+Mathf.Sign(lateral)*5),team,63,40);
            Assert.AreEqual(lateral-Mathf.Sign(lateral)*3,target.y,.001f);
            Assert.Greater(target.x*sign,0);
            target=MNG_TeamPlanner.EnforceAttackingCarryTarget(new Vector2(0,.5f),new Vector2(sign*10,5),team,63,40);
            Assert.AreEqual(0,target.y);
        }
    }
}
