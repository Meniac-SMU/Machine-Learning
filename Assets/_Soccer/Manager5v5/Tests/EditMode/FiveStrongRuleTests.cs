using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveStrongRuleTests
    {
        static FiveVisiblePlayer Player(int slot,float x,float y,bool keeper=false)
            =>new FiveVisiblePlayer {slot=slot,position=new Vector2(x,y),stamina=1,radius=.45f,keeper=keeper};
        static FiveObservation Scene()=>new FiveObservation {
            own=new[]{Player(0,-58,0,true),Player(1,-30,-30),Player(2,-30,30),Player(3,0,0),Player(4,15,15)},
            opponents=new[]{Player(0,58,0,true),Player(1,40,-30),Player(2,40,30),Player(3,50,-20),Player(4,50,20)},
            ownPossession=true,ownerSlot=3,ball=new Vector2(1,0),carrierPosition=Vector2.zero,
            halfLength=62,halfWidth=42.32727f,goalHalfWidth=10.196f,ballRadius=.5234f,
            maximumPlayerSpeed=9,acceleration=30,freeBallDamping=.2f,maximumBallSpeed=36.6667f,
            decisionSeconds=3,remaining=240,commandAge=9,ownCommand=FiveCommand.FastTransition};

        [Test] public void WideReceiverMustFitExecutorForwardRange()
        {
            var s=Scene();s.own[4]=Player(4,-19,18);
            Assert.That(FiveStrongRule.Receivers(s,FiveCommand.WideCirculation),Is.Empty);
            s.own[4]=Player(4,15,15);
            Assert.That(FiveStrongRule.Receivers(s,FiveCommand.WideCirculation),Is.EqualTo(new[]{4}));
        }
        [Test] public void BackwardOutletRequiresPressureAndTenMetreLimit()
        {
            var s=Scene();s.own[4]=Player(4,-8,18);
            Assert.That(FiveStrongRule.Receivers(s,FiveCommand.WideCirculation),Is.Empty);
            s.opponents[3]=Player(3,3,0);
            Assert.That(FiveStrongRule.Receivers(s,FiveCommand.WideCirculation),Is.EqualTo(new[]{4}));
            s.own[4]=Player(4,-11,18);
            Assert.That(FiveStrongRule.Receivers(s,FiveCommand.WideCirculation),Is.Empty);
        }
        [Test] public void MovingBlockerAndTeammateBlockRejectAnOtherwiseOpenLane()
        {
            var s=Scene();var end=new Vector2(25,0);
            Assert.That(FiveStrongRule.PassingLaneOpen(s,Vector2.zero,end,4),Is.True);
            s.opponents[3]=Player(3,12,6);s.opponents[3].velocity=new Vector2(0,-8);
            Assert.That(FiveStrongRule.PassingLaneOpen(s,Vector2.zero,end,4),Is.False);
            s.opponents[3]=Player(3,50,-20);s.own[1]=Player(1,12,0);
            Assert.That(FiveStrongRule.PassingLaneOpen(s,Vector2.zero,end,4),Is.False);
        }
        [Test] public void KeeperDistributionUsesItsOwnReceiverGeometry()
        {
            var s=Scene();s.ownerSlot=0;s.ball=s.own[0].position;s.own[4]=Player(4,-40,18);
            Assert.That(FiveStrongRule.Receivers(s,FiveCommand.WideCirculation),Does.Contain(4));
            s.own[4]=Player(4,-40,10);
            CollectionAssert.DoesNotContain(FiveStrongRule.Receivers(s,FiveCommand.WideCirculation),4);
        }
        [TestCase(true,false)] [TestCase(false,true)]
        public void StartedPassIsNotCancelledByAnotherTacticalChoice(bool preparing,bool flight)
        {
            var s=Scene();s.ownCommand=FiveCommand.LongPass;s.ownPassPreparing=preparing;s.ownPassInFlight=flight;
            Assert.That(FiveStrongRule.Evaluate(s).action,Is.EqualTo((int)FiveCommand.LongPass));
        }
        [Test] public void IncomingGoalRayIsDistinguishedFromWideMovingBall()
        {
            var s=Scene();s.ownPossession=false;s.ball=new Vector2(-40,0);s.velocity=new Vector2(-15,0);
            Assert.That(FiveStrongRule.Evaluate(s).action,Is.EqualTo((int)FiveCommand.GoalBlock));
            s.ball=new Vector2(-40,30);
            Assert.That(FiveStrongRule.Evaluate(s).action,Is.Not.EqualTo((int)FiveCommand.GoalBlock));
        }
        [Test] public void WideCarrierDoesNotAutomaticallyCallTheWholeTeamToGoal()
        {
            var s=Scene();s.ownPossession=false;s.enemyPossession=true;s.carrierPosition=s.ball=new Vector2(-50,30);
            s.carrierVelocity=new Vector2(-4,0);
            Assert.That(FiveStrongRule.Evaluate(s).action,Is.Not.EqualTo((int)FiveCommand.GoalBlock));
            s.carrierPosition=s.ball=new Vector2(-52,0);
            Assert.That(FiveStrongRule.Evaluate(s).action,Is.EqualTo((int)FiveCommand.GoalBlock));
        }
        [Test] public void LegacySelectorStillProducesItsHistoricalChoice()
        {
            var s=Scene();var command=FiveRuleManager.Choose(s,out var reason);var result=FiveStrongRule.Legacy(s);
            Assert.That(result.action,Is.EqualTo((int)command));Assert.That(result.reason,Is.EqualTo(reason));
            Assert.That(result.version,Is.EqualTo("legacy"));
            var strong=FiveStrongRule.Evaluate(s);Assert.That(strong.scores,Has.Length.EqualTo(8));
            Assert.That(strong.scores.All(float.IsFinite),Is.True);
        }
        [Test] public void InvalidSelectorSettingsFailBeforeAPlayerMatch()
        {
            var settings=new FiveRuleSettings();Assert.DoesNotThrow(settings.Validate);
            settings.carry=float.NaN;Assert.Throws<System.ArgumentException>(settings.Validate);
            settings.carry=3;settings.recoverSupport=1.5f;Assert.Throws<System.ArgumentException>(settings.Validate);
            settings.recoverSupport=2;settings.goalBlock=-1;Assert.Throws<System.ArgumentException>(settings.Validate);
        }
        [Test] public void OneAvailablePresserCanStartRecoveryBeforeTheCarrierReachesGoal()
        {
            var s=Scene();s.ownPossession=false;s.enemyPossession=true;s.carrierPosition=s.ball=new Vector2(-10,0);
            s.carrierVelocity=new Vector2(-5,0);
            Assert.That(FiveStrongRule.Evaluate(s).action,Is.EqualTo((int)FiveCommand.AllRecover));
            s.own[3]=Player(3,30,0);s.own[4]=Player(4,40,20);
            Assert.That(FiveStrongRule.Evaluate(s).action,Is.Not.EqualTo((int)FiveCommand.AllRecover));
        }
        [Test] public void StationaryCrowdedRecoveryMustChangeItsApproach()
        {
            var s=Scene();s.ownPossession=false;s.enemyPossession=true;s.carrierPosition=s.ball=new Vector2(53,17);
            s.own[3]=Player(3,52,17);s.ownCommand=FiveCommand.AllRecover;s.stallSeconds=14;
            Assert.That(FiveStrongRule.Evaluate(s).action,Is.EqualTo((int)FiveCommand.MarkReceivers));
        }
    }
}
