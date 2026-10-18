using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveCommandContractPlayTests
    {
        static string Evidence=>FiveP7Evidence.DirectoryPath;
        [Serializable] sealed class Game
        {
            public int seed,mirror,redScore,navyScore,passes,receptions,shots,corrections,physicsSteps,teamPlans;
            public float elapsed;
            public int[] executionCounts;
            public List<FiveEvent> decisions;
            public float maximumStall;
            public string finalPhysicalState;
            public List<FiveEvent> events;
        }
        [Serializable] sealed class Report
        {
            public string[] commandNames=FiveHud.CommandNames;
            public List<Game> games=new List<Game>();
        }

        static void Selected(FiveMatch m)
        {
            Assert.That(m.decisions,Has.Length.EqualTo(2));
            Assert.That(m.decisions[0],Is.Not.SameAs(m.decisions[1]));
            foreach(Team team in new[]{Team.Red,Team.Navy})
            {
                Assert.That(m.Decision(team),Is.Not.Null);
                Assert.That((int)m.Decision(team).command,Is.InRange(0,7));
            }
        }

        static int AuditStep(FiveMatch m)
        {
            Selected(m);
            var before=(int[])m.tacticSteps.Clone();
            var beforeFormation=(int[])m.defensiveFormationSteps.Clone();
            var expectsPlan=!m.Paused&&m.State==FiveMatchState.Playing&&m.Elapsed+.01f<m.profile.duration;
            m.SimulatePhysicsStep(.01f);
            Selected(m);
            for(int t=0;t<2;t++)
            {
                var chosen=(int)m.Decision((Team)t).command;
                var formation=m.defensiveFormationSteps[t]-beforeFormation[t];
                Assert.That(formation,Is.InRange(0,1));
                Assert.That(Enumerable.Range(0,8).Sum(id=>m.tacticSteps[t*8+id]-before[t*8+id])+formation,
                    Is.EqualTo(expectsPlan?1:0),"Exactly one tactical execution or the fixed T9 state.");
                for(int id=0;id<8;id++)
                    Assert.That(m.tacticSteps[t*8+id]-before[t*8+id],Is.EqualTo(expectsPlan&&formation==0&&id==chosen?1:0),
                        $"team={t}, elapsed={m.Elapsed}, active={FiveHud.CommandNames[chosen]}, execution={FiveHud.CommandNames[id]}");
            }
            Assert.That(float.IsFinite(m.ball.position.x)&&float.IsFinite(m.ball.position.y)&&float.IsFinite(m.ball.position.z),Is.True);
            foreach(var p in m.players)
                Assert.That(float.IsFinite(p.Position.x)&&float.IsFinite(p.Position.y)&&float.IsFinite(p.Velocity.magnitude),Is.True);
            return expectsPlan?2:0;
        }

        [UnityTest] public IEnumerator SelectionSurvivesHoldInvalidInputPauseGoalRestartAndFinish()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;Selected(m);
                Assert.That(m.Decision(Team.Red).command,Is.EqualTo(FiveCommand.FastTransition));
                Assert.That(m.SelectCommand(Team.Red,FiveCommand.MarkReceivers),Is.True);
                Assert.That(m.SelectCommand(Team.Red,FiveCommand.GoalBlock),Is.False);
                Assert.Throws<ArgumentOutOfRangeException>(()=>m.SelectCommand(Team.Red,(FiveCommand)(-1)));
                Assert.Throws<ArgumentOutOfRangeException>(()=>m.SelectCommand(Team.Red,(FiveCommand)8));
                Assert.That(m.Decision(Team.Red).command,Is.EqualTo(FiveCommand.MarkReceivers));
                AuditStep(m);m.Paused=true;
                for(int i=0;i<10;i++)AuditStep(m);
                Assert.That(m.Decision(Team.Red).command,Is.EqualTo(FiveCommand.MarkReceivers));
                m.Paused=false;m.Score(Team.Red);
                while(m.State==FiveMatchState.GoalPause)AuditStep(m);
                Assert.That(m.Decision(Team.Red).command,Is.EqualTo(FiveCommand.MarkReceivers));
                Assert.That(m.SelectCommand(Team.Red,FiveCommand.GoalBlock),Is.True);
                AuditStep(m);
                m.profile.duration=m.Elapsed+.025f;
                while(m.State!=FiveMatchState.Finished)AuditStep(m);
                AuditStep(m);Assert.That(m.Decision(Team.Red).command,Is.EqualTo(FiveCommand.GoalBlock));
                m.ResetMatch();Selected(m);
                Assert.That(m.Decision(Team.Red).command,Is.EqualTo(FiveCommand.FastTransition));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator EveryPreviousCommandIsReplacedByOneGoalBlockFormation()
        {
            for(int id=0;id<8;id++)
            {
                using(var world=new FiveTestWorld())
                {
                    var m=world.match;m.keepersEnabled=false;
                    world.Place(6,new Vector2(0,22),Vector2.left);world.Give(6);
                    m.SelectCommand(Team.Red,(FiveCommand)id,"contract-prior-command",true);
                    AuditStep(m);
                    m.SelectCommand(Team.Red,FiveCommand.GoalBlock,"contract-replace-formation",true);
                    AuditStep(m);
                    foreach(int index in new[]{1,2,4})
                        Assert.That(m.players[index].Target.x,Is.LessThan(-40),FiveHud.CommandNames[id]+" left a previous field target");
                    Assert.That(m.players[3].Target.x,Is.GreaterThan(0));
                    Assert.That(m.Decision(Team.Red).command,Is.EqualTo(FiveCommand.GoalBlock));
                }
                yield return null;
            }
        }

        [UnityTest] public IEnumerator InFlightReceptionIsOneSkillWhileOnlyTheNewTeamCommandRuns()
        {
            using(var world=new FiveTestWorld())
            {
                var m=world.match;m.keepersEnabled=false;m.teamPlanningEnabled=new[]{false,false};
                for(int i=5;i<10;i++)world.Place(i,new Vector2(-40+(i-5)*15,35),Vector2.left);
                world.Place(1,new Vector2(-24,0),Vector2.right);world.Place(3,new Vector2(30,0),Vector2.left);
                world.Give(1);m.SelectCommand(Team.Red,FiveCommand.LongPass,"contract-pass-before-switch",true);
                world.Step(151);
                Assert.That(m.RequestPass(m.players[1],m.players[3]),Is.True);
                for(int step=0;step<100&&m.PassPhase!=FivePassPhase.InFlight;step++)world.Step(1);
                Assert.That(m.PassPhase,Is.EqualTo(FivePassPhase.InFlight));
                var pass=m.PassId;
                Assert.That(m.SelectCommand(Team.Red,FiveCommand.GoalBlock,"contract-legal-switch"),Is.True);
                m.teamPlanningEnabled=new[]{true,true};AuditStep(m);
                Assert.That(m.PassPhase,Is.EqualTo(FivePassPhase.InFlight));
                Assert.That(m.PassReceiver,Is.EqualTo(3));
                foreach(int index in new[]{1,2,4})Assert.That(m.players[index].Target.x,Is.LessThan(-40));
                for(int step=0;step<650&&!m.Events.Any(e=>e.kind=="pass-received"&&e.id==pass);step++)AuditStep(m);
                Assert.That(m.Events.Any(e=>e.kind=="pass-received"&&e.id==pass),Is.True);
                Assert.That(m.Decision(Team.Red).command,Is.EqualTo(FiveCommand.GoalBlock));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator TwoMirroredFullMatchesExecuteExactlyOneCommandPerTeamPerPhysicsStep()
        {
            var report=new Report();Directory.CreateDirectory(Evidence);
            for(int mirror=0;mirror<2;mirror++)
            {
                using(var world=new FiveTestWorld())
                {
                    var m=world.match;m.ruleManagers=new[]{true,true};m.profile.duration=300;
                    const int seed=10401;var random=new System.Random(seed);
                    var spawns=m.players.Select(p=>p.spawn).ToArray();
                    var jitter=Enumerable.Range(0,10).Select(_=>new Vector2((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*1.6f).ToArray();
                    for(int i=0;i<10;i++)
                    {
                        var source=mirror==0?i:(i+5)%10;var sign=mirror==0?1:-1;
                        var pos=(new Vector2(spawns[source].x,spawns[source].z)+jitter[source])*sign;
                        world.Place(i,pos,new Vector2(FiveMatch.AttackSign(m.players[i].team),0));
                        m.players[i].spawn=m.players[i].Body.position;
                    }
                    var game=new Game{seed=seed,mirror=mirror};
                    while(m.State!=FiveMatchState.Finished&&game.physicsSteps<48000)
                    {
                        game.teamPlans+=AuditStep(m);game.physicsSteps++;
                        game.maximumStall=Mathf.Max(game.maximumStall,m.ObservedStallSeconds);
                        if(game.physicsSteps%1000==0)yield return null;
                    }
                    game.elapsed=m.Elapsed;game.redScore=m.RedScore;game.navyScore=m.NavyScore;
                    game.passes=m.PassAttempts;game.receptions=m.PassReceptions;game.shots=m.Shots;game.corrections=m.BoundaryCorrections;
                    game.executionCounts=(int[])m.tacticSteps.Clone();game.decisions=m.Events.Where(e=>e.kind=="command").ToList();
                    game.events=new System.Collections.Generic.List<FiveEvent>(m.Events);
                    game.finalPhysicalState=$"ball={m.BallPosition};owner={m.Owner};players="+string.Join("/",m.players.Select(p=>$"{p.Index}:position={p.Position}:forward={p.Forward}:target={p.Target}"));
                    report.games.Add(game);File.WriteAllText(Evidence+"/single-command-matches.json",JsonUtility.ToJson(report,true));
                    Assert.That(m.State,Is.EqualTo(FiveMatchState.Finished));
                    Assert.That(m.BoundaryCorrections,Is.Zero);
                    Assert.That(m.PassAttempts,Is.GreaterThan(0));Assert.That(m.Shots,Is.GreaterThan(0));
                }
                yield return null;
            }
            Assert.That(report.games.Sum(g=>g.redScore+g.navyScore),Is.GreaterThan(0));
        }
    }
}
