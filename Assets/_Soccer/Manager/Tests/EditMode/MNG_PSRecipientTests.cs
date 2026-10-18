using MachineLearning.Soccer;
using NUnit.Framework;
using UnityEngine;

namespace MachineLearning.Soccer.Manager.Tests
{
    public class MNG_PSRecipientTests
    {
        [TestCase(2f)] [TestCase(45f)]
        public void DirectedPassDoesNotMaskInefficientDistances(float distance)
        {
            var s=new MNG_MatchSnapshot();
            s.SetPlayer(Team.Red,3,new MNG_PlayerState{Active=true,Position=Vector2.zero});
            s.SetPlayer(Team.Red,0,new MNG_PlayerState{Active=true,Position=new Vector2(-distance,0)});
            Assert.IsTrue(MNG_PSPassing.Valid(s,Team.Red,3,0));
        }
        [TestCase(Team.Red)] [TestCase(Team.Navy)]
        public void DirectedPassAllowsBackwardKeeperAndBlockedLane(Team team)
        {
            var sign = team == Team.Red ? 1f : -1f;
            var other = team == Team.Red ? Team.Navy : Team.Red;
            var s = new MNG_MatchSnapshot { Carrier=MNG_CarrierRef.For(team,3),
                Possession=team==Team.Red?MNG_Possession.Red:MNG_Possession.Navy };
            s.SetPlayer(team,3,new MNG_PlayerState{Active=true,Position=Vector2.zero});
            s.SetPlayer(team,0,new MNG_PlayerState{Active=true,Role=MNG_PlayerRole.Keeper,Position=new Vector2(-10*sign,0)});
            s.SetPlayer(other,1,new MNG_PlayerState{Active=true,Position=new Vector2(-5*sign,0)});
            Assert.IsFalse(MNG_TacticalTargetResolver.IsPassBuildReceiverValid(s,team,3,0));
            Assert.IsTrue(MNG_PSPassing.Valid(s,team,3,0));
            var d=new MNG_TeamDecisionState{HasPassTarget=true,PendingPassReceiverSlot=0,DirectedPass=true};
            var tasks=new MNG_PlayerTask[4];
            MNG_TeamPlanner.PlanV2(s,team,MNG_Command.PassBuild,d,1,1,tasks);
            Assert.AreEqual(MNG_PlayerSkill.AimPass,tasks[3].Skill);
            Assert.AreEqual(MNG_PlayerSkill.ReceivePass,tasks[0].Skill);
            Assert.IsTrue(tasks[3].DirectedPass);
            Assert.AreEqual(s.GetPlayer(team,0).Position,tasks[3].Target);
            Assert.IsFalse(MNG_PSPassing.Valid(s,team,3,3));
            var keeper=s.GetPlayer(team,0);keeper.IsHuman=true;s.SetPlayer(team,0,keeper);
            Assert.IsFalse(MNG_PSPassing.Valid(s,team,3,0));
        }
    }
}
