using NUnit.Framework;
using UnityEngine;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveHumanDemoPlayTests
    {
        [TearDown] public void Cleanup()=>FiveTestWorld.DisposeOutstanding();
        [Test] public void HumanInputOverridesManagerTargetsAndUsesSharedLimits()
        {
            using var world=new FiveTestWorld();var m=world.match;var p=m.players[3];
            world.Place(3,new Vector2(-10,-20),Vector2.right);m.SetHumanControl(true);
            for(int i=0;i<50;i++){m.SetHumanInput(1,0,false,false);world.Step(1);}
            Assert.Greater(p.Position.x,-8);Assert.LessOrEqual(p.Velocity.magnitude,m.profile.maxSpeed+.05f);
            Assert.Less(p.Stamina.Value,1);Assert.AreEqual(m.profile.maxSpeed*FiveStamina.SpeedFactor(p.Stamina.Value,false),p.MaximumMoveSpeed(m.profile),.001f);
            m.SetHumanInput(0,0,false,false);world.Step(60);var stopped=p.Position;world.Step(30);
            Assert.Less(Vector2.Distance(stopped,p.Position),.05f,"Manager must not move the human player.");
            Assert.AreEqual(m.profile.turnRate,p.MaximumTurnRate(m.profile));
            m.SetHumanControl(false);world.Step(80);Assert.Greater(Vector2.Distance(stopped,p.Position),.5f);
        }
        [Test] public void HumanPlayerCannotBeOrderedToKickByTheManager()
        {
            using var world=new FiveTestWorld();world.Give(3);var m=world.match;m.SetHumanControl(true);
            Assert.False(m.RequestShot(m.players[3],new Vector2(60,0)));
            Assert.False(m.RequestPass(m.players[3],m.players[4]));
            m.SetHumanInput(0,0,false,true);world.Step(1);
            Assert.AreEqual(1,m.Shots);Assert.Greater(m.BallVelocity.magnitude,25);
            m.SetHumanInput(0,0,false,true);world.Step(1);Assert.AreEqual(1,m.Shots,"Same cooldown and contact gate.");
        }
        [Test] public void HumanCannotKickARearBallOrBufferAKickAcrossPause()
        {
            using var world=new FiveTestWorld();var m=world.match;m.SetHumanControl(true);
            m.ball.position=m.players[3].Body.position-Vector3.right*3;Physics.SyncTransforms();
            m.SetHumanInput(0,0,false,true);world.Step(1);Assert.AreEqual(0,m.Shots);
            m.Paused=true;m.SetHumanInput(1,1,false,true);world.Step(10);Assert.AreEqual(0,m.Shots);
            m.Paused=false;world.Give(3);world.Step(1);Assert.AreEqual(0,m.Shots);
        }
        [Test] public void StatisticsCountStablePassesAndDoNotCountConcededKeeperTouchesAsSaves()
        {
            using var world=new FiveTestWorld();var m=world.match;using var stats=new FiveMatchStatistics(m);
            m.Record("pass-kicked",3,4,1);m.Record("pass-received",3,4,1);
            m.Record("shot",3);m.Record("front-cushion",5);m.Record("goal",0);
            Assert.AreEqual(1,stats.teams[0].shots);Assert.AreEqual(1,stats.teams[0].successfulShots);
            Assert.AreEqual(0,stats.teams[1].saves);Assert.AreEqual(100,stats.teams[0].PassRate);
            m.Record("shot",3);m.Record("front-cushion",5);m.Record("front-cushion",5);m.Record("possession",5);
            Assert.AreEqual(1,stats.teams[1].saves);Assert.AreEqual(50,stats.teams[0].ShotRate);
            Assert.AreEqual(0,stats.teams[1].ShotRate);Assert.AreEqual(0,FiveMatchStatistics.Share(0,0));
            Assert.AreEqual(.25f,FiveMatchStatistics.Share(1,3));Assert.AreEqual(1,FiveMatchStatistics.Share(5,0));
        }
    }
}
