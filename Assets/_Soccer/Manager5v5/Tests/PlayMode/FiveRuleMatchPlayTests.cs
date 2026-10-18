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
    public sealed class FiveRuleMatchPlayTests
    {
        [UnityTearDown] public IEnumerator CleanupOutstandingWorlds()
        {FiveTestWorld.DisposeOutstanding();yield return null;}
        [Serializable] sealed class MatchResult
        {
            public int seed,mirror,redScore,navyScore,passes,receptions,shots,recoveries,corrections,anticipatedBounces;
            public float elapsed,maximumStall,decisionPeriod;
            public bool finished;
            public int[] selections;
            public float[] stamina;
            public List<FiveEvent> events;
        }
        [Serializable] sealed class Report {public List<MatchResult> matches=new List<MatchResult>();}
        [UnityTest]
        public IEnumerator MirroredThreeHundredSecondRuleMatches()
            => RunMatches(7301,4,"g4-rule-matches.json");
        [UnityTest,Category("LongRunning"),Timeout(600000)]
        public IEnumerator RetentionRuleMatches()
            => RunMatches(9901,4,"g4-rule-matches-retention.json");
        [UnityTest,Category("LongRunning")]
        public IEnumerator IndependentThirtyTwoRuleMatches()
            => RunMatches(8701,16,"g4-rule-matches-independent.json");
        [UnityTest,Category("LongRunning"),Timeout(600000)]
        public IEnumerator IndependentSixtyFourRuleMatches()
            => RunMatches(8901,32,"g4-rule-matches-independent-64.json");
        [UnityTest,Category("LongRunning")]
        public IEnumerator DevelopmentPreviousThirtyTwoRuleMatches()
            => RunMatches(8301,16,"g4-rule-matches-refinement.json");
        [UnityTest,Category("LongRunning")]
        public IEnumerator DevelopmentSecondThirtyTwoRuleMatches()
            => RunMatches(8401,16,"g4-rule-matches-refinement-2.json");
        [UnityTest]
        public IEnumerator RecordedGoalBlockStallMustFindAnotherApproach()
            => RunMatches(8416,1,"g4-rule-matches-stall-regression.json");
        [UnityTest]
        public IEnumerator RecordedCutbackStallMustTryAWideRebuild()
            => RunMatches(8601,1,"g4-rule-matches-cutback-stall-regression.json");
        [UnityTest]
        public IEnumerator RecordedFourSecondStallMustFindAnExit()
            => RunMatches(8608,1,"g4-rule-matches-four-second-stall-regression.json",4);
        [UnityTest]
        public IEnumerator RecordedEndWallPressureStaysInsideThePitch()
            => RunMatches(8702,1,"g4-rule-matches-end-wall-regression.json");
        [UnityTest]
        public IEnumerator RecordedCrowdedCutbackStallMustFindAnExit()
            => RunMatches(8703,1,"g4-rule-matches-crowded-cutback-regression.json");
        [UnityTest]
        public IEnumerator RecordedRepeatedShotPreparationMustNotPinTheCarrier()
            => RunMatches(8830,1,"g4-rule-matches-shot-preparation-regression.json");
        [UnityTest,Category("LongRunning"),Timeout(600000)]
        public IEnumerator MatchedDecisionPeriodExperiment()
        {
            foreach(var period in new[]{2f,3f,4f})
                yield return RunMatches(8601,8,"g4-rule-matches-period-"+period.ToString("0")+".json",period,false);
        }
        IEnumerator RunMatches(int firstSeed,int pairs,string reportName,float period=3,bool requireHealthyGames=true)
        {
            var report=new Report();
            for(int run=0;run<pairs*2;run++)
            {
                using(var world=new FiveTestWorld())
                {
                    var match=world.match;match.ruleManagers=new[]{true,true};match.profile.decisionSeconds=period;
                    var seed=firstSeed+run/2;
                    var random=new System.Random(seed);
                    // Mirrored, team-swapped starts use the same perturbations rather than different luck.
                    var original=match.players.Select(p=>p.spawn).ToArray();
                    var jitters=Enumerable.Range(0,10).Select(_=>new Vector2((float)random.NextDouble()-.5f,(float)random.NextDouble()-.5f)*1.6f).ToArray();
                    for(int index=0;index<10;index++)
                    {
                        var source=run%2==0?index:(index+5)%10;
                        var basePoint=original[source];var sign=run%2==0?1:-1;
                        var jitter=jitters[source];
                        var pos=(new Vector2(basePoint.x,basePoint.z)+jitter)*sign;
                        world.Place(index,pos,new Vector2(FiveMatch.AttackSign(match.players[index].team),0));
                        match.players[index].spawn=match.players[index].Body.position;
                    }
                    float maximumStall=0;int steps=0;float nextStallSnapshot=10;
                    while(match.State!=FiveMatchState.Finished&&steps<24000)
                    {
                        world.Step(50,()=>
                        {
                            maximumStall=Mathf.Max(maximumStall,match.ObservedStallSeconds);
                            if(match.ObservedStallSeconds>=nextStallSnapshot)
                            {
                                nextStallSnapshot+=10;
                                match.Record("physical-stall-state",match.Owner,reason:$"ball={match.BallPosition.ToString("F3")};velocity={match.BallVelocity.ToString("F3")};players="+string.Join("/",match.players.Select(p=>$"{p.Index}:pos={p.Position.ToString("F3")}:target={p.Target.ToString("F3")}:velocity={p.Velocity.ToString("F3")}:forward={p.Forward.ToString("F3")}:plate={match.InDribbleArea(p)}:ray={string.Join(",",Physics.RaycastAll(new Vector3(p.Position.x,match.ball.position.y,p.Position.y), (match.ball.position-p.Body.position).normalized,Vector2.Distance(p.Position,match.BallPosition),~0,QueryTriggerInteraction.Ignore).Where(h=>h.rigidbody!=p.Body&&h.rigidbody!=match.ball).Select(h=>h.collider.name))}")));
                            }
                        });steps+=50;
                        if(steps%1000==0)yield return null;
                    }
                    var selections=new int[16];
                    foreach(var e in match.Events.Where(e=>e.kind=="command"))selections[e.actor*8+e.other]++;
                    report.matches.Add(new MatchResult {seed=seed,mirror=run%2,decisionPeriod=period,finished=match.State==FiveMatchState.Finished,elapsed=match.Elapsed,
                        redScore=match.RedScore,navyScore=match.NavyScore,passes=match.PassAttempts,receptions=match.PassReceptions,shots=match.Shots,
                        recoveries=match.Events.Count(e=>e.kind=="possession"),corrections=match.BoundaryCorrections,anticipatedBounces=match.AnticipatedWallBounces,maximumStall=maximumStall,
                        selections=selections,stamina=match.players.Select(p=>p.IsKeeper?1:p.Stamina.Value).ToArray(),events=new List<FiveEvent>(match.Events)});
                    File.WriteAllText(FiveP7Evidence.DirectoryPath+"/"+reportName,JsonUtility.ToJson(report,true));
                }
                yield return null;
            }
            foreach(var result in report.matches)
            {
                Assert.That(result.finished,Is.True);
                Assert.That(result.elapsed,Is.GreaterThanOrEqualTo(300));
                if(!requireHealthyGames)continue; // Collect every period first; health and quality are assessed from the saved reports.
                // A healthy 300-second match can score through carries and shots without a pass.
                // Exercise passing across the paired suite, not as an artificial per-game quota.
                Assert.That(result.shots,Is.GreaterThan(0));
                Assert.That(result.recoveries,Is.GreaterThan(1));
                Assert.That(result.maximumStall,Is.LessThan(30),"A ball stalled for 30 seconds is a failed game, not an acceptable draw.");
                Assert.That(result.corrections,Is.Zero,"The physical walls should contain this match without fallback correction.");
            }
            if(requireHealthyGames)
            {
                Assert.That(report.matches.Sum(m=>m.passes),Is.GreaterThan(0),"The paired suite must exercise actual passing.");
                Assert.That(report.matches.Sum(m=>m.redScore+m.navyScore),Is.GreaterThan(0),"Rule matches must exercise actual goal contact and restarts.");
            }
        }
    }
}
