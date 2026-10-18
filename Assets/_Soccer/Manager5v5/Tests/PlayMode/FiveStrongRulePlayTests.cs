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
    public sealed class FiveStrongRulePlayTests
    {
        [UnityTearDown] public IEnumerator Cleanup(){FiveTestWorld.DisposeOutstanding();yield return null;}
        [Serializable] sealed class Result
        {
            public int seed,strongTeam,goalsFor,goalsAgainst,corrections,passes,shots;
            public float elapsed,maximumStall;
            public int[] actions;
        }
        [Serializable] sealed class Report {public string version=FiveStrongRule.Version;public List<Result> matches=new List<Result>();}
        [UnityTest] public IEnumerator NativeSelectionUsesVariantAndOnlyAtDecisionBoundaries()
        {
            using(var world=new FiveTestWorld())
            {
                var match=world.match;match.ruleVariants=new[]{FiveRuleVariant.Strong,FiveRuleVariant.Legacy};
                match.ruleManagers=new[]{true,true};world.Step(175);
                Assert.That(match.RuleDiagnostic(Team.Red).version,Is.EqualTo(FiveStrongRule.Version));
                Assert.That(match.RuleDiagnostic(Team.Navy).version,Is.EqualTo("legacy"));
                var at=match.Decision(Team.Red).lastDecisionAt;world.Step(25);
                Assert.That(match.Decision(Team.Red).lastDecisionAt,Is.EqualTo(at));
                world.Step(150);Assert.That(match.Decision(Team.Red).lastDecisionAt,Is.GreaterThan(at));
            }
            yield return null;
        }
        [UnityTest] public IEnumerator PublicSnapshotIgnoresOpponentOrdersAndMatchesExecutorLanes()
        {
            using(var world=new FiveTestWorld())
            {
                var match=world.match;world.Give(3);
                var before=FiveObservation.Capture(match,Team.Red);
                var lane=typeof(FiveMatch).GetMethod("PassingLaneOpen",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                foreach(var player in match.players.Where(p=>p.team==Team.Red&&p.slot!=3))
                {
                    var actual=(bool)lane.Invoke(match,new object[]{match.BallPosition,player.Position,Team.Red,player.Index});
                    Assert.That(FiveStrongRule.PassingLaneOpen(before,before.ball,player.Position,player.slot),Is.EqualTo(actual));
                }
                var decision=FiveStrongRule.Evaluate(before);
                match.SelectCommand(Team.Navy,FiveCommand.GoalBlock,"hidden-order-test",true);
                foreach(var p in match.players.Where(p=>p.team==Team.Navy))p.SetTarget(new Vector2(30,30),Vector2.zero);
                var after=FiveStrongRule.Evaluate(FiveObservation.Capture(match,Team.Red));
                Assert.That(after.action,Is.EqualTo(decision.action));CollectionAssert.AreEqual(decision.scores,after.scores);
            }
            yield return null;
        }
        [UnityTest,Category("LongRunning"),Timeout(600000)]
        public IEnumerator DevelopmentStrongAgainstLegacy()=>Matches(31000,8,"strong-legacy-development.json");
        [UnityTest] public IEnumerator ForecastCannotAdvanceOrMutateTheActualMatch()
        {
            using(var world=new FiveTestWorld())
            {
                var match=world.match;world.Give(3);match.ruleVariants[0]=FiveRuleVariant.Strong;
                var bodies=match.players.Select(p=>p.Body).Concat(new[]{match.ball}).ToArray();
                var positions=bodies.Select(b=>b.position).ToArray();var velocities=bodies.Select(b=>b.linearVelocity).ToArray();
                var rotations=bodies.Select(b=>b.rotation).ToArray();var owner=match.Owner;var elapsed=match.Elapsed;
                var events=match.Events.Count;var rewards=match.RedScore-match.NavyScore;
                var stamina=match.players.Where(p=>!p.IsKeeper).Select(p=>p.Stamina.Value).ToArray();
                var scenes=UnityEngine.SceneManagement.SceneManager.sceneCount;
                match.ChooseRule(Team.Red,out var reason);
                Assert.That(reason,Is.EqualTo("visible-state-three-second-tactic-forecast"));
                Assert.That(match.RuleDiagnostic(Team.Red).forecastOutcomes,Has.Length.EqualTo(8));
                CollectionAssert.AreEqual(positions,bodies.Select(b=>b.position).ToArray());
                CollectionAssert.AreEqual(velocities,bodies.Select(b=>b.linearVelocity).ToArray());
                CollectionAssert.AreEqual(rotations,bodies.Select(b=>b.rotation).ToArray());
                Assert.That(match.Owner,Is.EqualTo(owner));Assert.That(match.Elapsed,Is.EqualTo(elapsed));
                Assert.That(match.Events.Count,Is.EqualTo(events));Assert.That(match.RedScore-match.NavyScore,Is.EqualTo(rewards));
                CollectionAssert.AreEqual(stamina,match.players.Where(p=>!p.IsKeeper).Select(p=>p.Stamina.Value).ToArray());
                Assert.That(UnityEngine.SceneManagement.SceneManager.sceneCount,Is.EqualTo(scenes+1));
            }
            yield return null;yield return null;
        }
        [UnityTest] public IEnumerator ForecastRecognizesPhysicalGoalsOnBothSidesWithoutScoringInTheRealMatch()
        {
            for(var t=0;t<2;t++)
            {
                using(var world=new FiveTestWorld())
                {
                    var match=world.match;var team=(Team)t;var sign=FiveMatch.AttackSign(team);
                    for(var i=0;i<10;i++)world.Place(i,new Vector2(-20+i*3,(i%2==0?-1:1)*30),Vector2.right);
                    world.Place(t*5+3,new Vector2(48,0)*sign,new Vector2(sign,0));world.Give(t*5+3);
                    match.ruleVariants[t]=FiveRuleVariant.Strong;match.ChooseRule(team,out _);
                    Assert.That(match.RuleDiagnostic(team).forecastOutcomes.Max(),Is.GreaterThan(200),"The copied scoring surface must register a predicted physical goal.");
                    Assert.That(match.RedScore+match.NavyScore,Is.Zero);
                }
                yield return null;yield return null;
            }
        }
        [UnityTest] public IEnumerator DefensiveForecastIsIsolatedAndItsSceneIsReleased()
        {
            var scenes=UnityEngine.SceneManagement.SceneManager.sceneCount;
            using(var world=new FiveTestWorld())
            {
                var match=world.match;world.Give(8);match.ruleVariants[0]=FiveRuleVariant.Strong;
                var positions=match.players.Select(p=>p.Body.position).Concat(new[]{match.ball.position}).ToArray();
                var elapsed=match.Elapsed;var events=match.Events.Count;
                match.ChooseRule(Team.Red,out _);
                Assert.That(match.RuleDiagnostic(Team.Red).forecastOutcomes,Has.Length.EqualTo(16));
                CollectionAssert.AreEqual(positions,match.players.Select(p=>p.Body.position).Concat(new[]{match.ball.position}).ToArray());
                Assert.That(match.Elapsed,Is.EqualTo(elapsed));Assert.That(match.Events.Count,Is.EqualTo(events));
            }
            for(var frame=0;frame<20&&UnityEngine.SceneManagement.SceneManager.sceneCount>scenes;frame++)yield return null;
            Assert.That(UnityEngine.SceneManagement.SceneManager.sceneCount,Is.EqualTo(scenes));
        }
        [UnityTest,Category("LongRunning"),Timeout(1200000)]
        public IEnumerator SecondHoldoutStrongAgainstLegacy()=>Matches(44000,16,"strong-legacy-holdout-second.json");
        [UnityTest,Category("LongRunning"),Timeout(600000)]
        public IEnumerator HoldoutStrongAgainstLegacy()=>Matches(34000,16,"strong-legacy-holdout.json");
        IEnumerator Matches(int firstSeed,int pairs,string filename)
        {
            var report=new Report();
            for(var run=0;run<pairs*2;run++)
            {
                using(var world=new FiveTestWorld())
                {
                    var match=world.match;var strong=run%2;match.ruleManagers=new[]{true,true};
                    match.ruleVariants=new[]{strong==0?FiveRuleVariant.Strong:FiveRuleVariant.Legacy,strong==1?FiveRuleVariant.Strong:FiveRuleVariant.Legacy};
                    var random=new System.Random(firstSeed+run/2);var original=match.players.Select(p=>p.spawn).ToArray();
                    var jitter=Enumerable.Range(0,10).Select(_=>new Vector2((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*1.6f).ToArray();
                    for(var index=0;index<10;index++)
                    {
                        var source=strong==0?index:(index+5)%10;var point=original[source];
                        world.Place(index,(new Vector2(point.x,point.z)+jitter[source])*(strong==0?1:-1),new Vector2(FiveMatch.AttackSign(match.players[index].team),0));
                        match.players[index].spawn=match.players[index].Body.position;
                    }
                    var actions=new int[16];
                    match.RuleDecisionMade+=(team,seen,command,reason)=>actions[(int)team*8+(int)command]++;
                    float stall=0;var steps=0;
                    while(match.State!=FiveMatchState.Finished&&steps<24000)
                    {world.Step(50,()=>stall=Mathf.Max(stall,match.ObservedStallSeconds));steps+=50;if(steps%1000==0)yield return null;}
                    report.matches.Add(new Result {seed=firstSeed+run/2,strongTeam=strong,
                        goalsFor=strong==0?match.RedScore:match.NavyScore,goalsAgainst=strong==0?match.NavyScore:match.RedScore,
                        elapsed=match.Elapsed,maximumStall=stall,corrections=match.BoundaryCorrections,passes=match.PassAttempts,shots=match.Shots,actions=actions});
                    File.WriteAllText(FiveP7Evidence.DirectoryPath+"/"+filename,JsonUtility.ToJson(report,true));
                    Assert.That(match.State,Is.EqualTo(FiveMatchState.Finished));
                }
                yield return null;
            }
            // Quality is judged from every saved result, not by discarding unfavourable seeds.
            Assert.That(report.matches.All(r=>r.corrections==0&&r.elapsed>=300),Is.True);
        }
    }
}
