using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.TestTools;

namespace MachineLearning.Soccer.Manager5v5.Tests
{
    public sealed class FiveLearningPlayTests
    {
        [TearDown] public void Cleanup() { FiveTestWorld.DisposeOutstanding(); }

        [UnityTest] public IEnumerator HistoryUsesPastSimulationSnapshotsInNewestFirstOrder()
        {
            using (var world = new FiveTestWorld())
            {
                var match = world.match; var history = new FiveObservationHistory(); history.Reset(match);
                var initial = history.Capture(match, Team.Red);
                Assert.That(initial.values.Skip(672), Is.EqualTo(new float[] {1, 0, 0, 0}));
                Assert.That(initial.values.Skip(168).Take(504).All(v => v == 0), Is.True);
                Assert.That(initial.values, Is.EqualTo(history.Capture(match, Team.Navy).values).Within(.00001));
                var expected = new List<(float time, float[] values)> {(match.Elapsed, FiveLearningContract.CaptureFrame(match, Team.Red))};
                for (var i = 0; i < 80; i++)
                {
                    // Different physical step sizes and non-aligned decision times must still use the simulation clock.
                    match.SimulatePhysicsStep(i % 2 == 0 ? .01f : .02f);
                    match.players[1].Stamina.SetForScenario(i / 100f);
                    history.Record(match);
                    expected.Add((match.Elapsed, FiveLearningContract.CaptureFrame(match, Team.Red)));
                    if (i == 20)
                        Assert.That(history.Capture(match, Team.Red).values.Skip(672), Is.EqualTo(new float[] {1, 1, 0, 0}));
                }
                var observation = history.Capture(match, Team.Red);
                Assert.That(observation.values, Has.Length.EqualTo(676));
                Assert.That(observation.values.Skip(672), Is.EqualTo(new float[] {1, 1, 1, 1}));
                for (var frame = 1; frame < 4; frame++)
                {
                    var target = match.Elapsed - .25f * frame;
                    var sample = expected.Last(s => s.time <= target);
                    Assert.That(observation.values.Skip(frame * 168).Take(168), Is.EqualTo(sample.values));
                    Assert.That(observation.sampleTimes[frame], Is.EqualTo(sample.time));
                    Assert.That(observation.sampleErrors[frame], Is.InRange(0, .0201f));
                }
                var frozen = (float[])observation.values.Clone();
                match.players[1].Stamina.SetForScenario(.99f);
                Assert.That(observation.values, Is.EqualTo(frozen), "Previously captured frames must be immutable.");
                Assert.That(history.Count, Is.LessThan(55), "The history must remain bounded.");
            }
            yield return null;
        }

        [UnityTest] public IEnumerator HistoryDoesNotLeakPrivateOpponentIntentAcrossFrames()
        {
            using (var world = new FiveTestWorld())
            {
                var match = world.match; var before = new FiveObservationHistory(); var after = new FiveObservationHistory();
                before.Reset(match); after.Reset(match);
                for (var i = 0; i < 50; i++)
                {
                    world.Step(1); before.Record(match);
                    match.SelectCommand(Team.Navy, (FiveCommand)(i % 8), "private", true);
                    match.players[7].SetTarget(new Vector2(i, -i), Vector2.one);
                    after.Record(match);
                    Assert.That(after.Capture(match, Team.Red).values, Is.EqualTo(before.Capture(match, Team.Red).values));
                }
                Assert.That(after.Capture(match, Team.Red).values.Skip(672), Is.EqualTo(new float[] {1, 1, 1, 1}));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator PauseFreezesHistoryAndGoalRestartClearsItWithoutResettingStaminaOrReward()
        {
            using (var world = new FiveTestWorld())
            {
                var match = world.match; var session = FiveLearningFactory.Create(match, false, false);
                session.InitializeSession();
                for (var i = 0; i < 60; i++) session.Step(.02f);
                var before = session.History.Capture(match, Team.Red);
                match.Paused = true;
                for (var i = 0; i < 100; i++) session.Step(.02f);
                var paused = session.History.Capture(match, Team.Red);
                Assert.That(paused.sampleTimes, Is.EqualTo(before.sampleTimes));
                Assert.That(paused.values.Skip(168), Is.EqualTo(before.values.Skip(168)));
                match.Paused = false; match.players[1].Stamina.SetForScenario(.3f);
                var goalTime = match.Elapsed;
                match.Score(Team.Red);
                for (var i = 0; match.State == FiveMatchState.GoalPause && i < 500; i++) session.Step(.02f);
                Assert.That(match.State, Is.EqualTo(FiveMatchState.Playing));
                Assert.That(session.History.Capture(match, Team.Red).values.Skip(672), Is.EqualTo(new float[] {1, 0, 0, 0}));
                // P7 physics uses 10 ms substeps: a restart in the first substep can
                // legitimately recover stamina during the second, active substep.
                Assert.That(match.players[1].Stamina.Value,
                    Is.EqualTo(.3f + match.profile.recoveryPerSecond * (match.Elapsed - goalTime)).Within(.000001));
                Assert.That(session.agents[0].GetCumulativeReward(), Is.EqualTo(1));
                Assert.That(session.agents[1].GetCumulativeReward(), Is.EqualTo(-1));
                session.Step(.02f);
                foreach (var agent in session.agents)
                    Assert.That(agent.Observation.Skip(672), Is.EqualTo(new float[] {1, 0, 0, 0}));
                Assert.That(session.agents[0].GetCumulativeReward(), Is.EqualTo(1));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator HistoryRejectsMissingSamplesAndRequiresResetAfterNewMatch()
        {
            using (var world = new FiveTestWorld())
            {
                var history = new FiveObservationHistory(); history.Reset(world.match);
                world.Step(50); history.Record(world.match);
                Assert.Throws<InvalidOperationException>(() => history.Capture(world.match, Team.Red));
                world.match.ResetMatch();
                Assert.Throws<InvalidOperationException>(() => history.Record(world.match));
                history.Reset(world.match);
                Assert.That(history.Capture(world.match, Team.Red).values.Skip(672), Is.EqualTo(new float[] {1, 0, 0, 0}));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator SchemaAndMirroredPublicStateAreStable()
        {
            using (var world = new FiveTestWorld())
            {
                var match = world.match;
                var names = FiveLearningContract.FrameFeatureNames();
                Assert.That(FiveLearningContract.FeatureNames().Length, Is.EqualTo(676));
                Assert.That(FiveLearningContract.FeatureNames().Distinct().Count(), Is.EqualTo(676));
                Assert.That(names, Has.Length.EqualTo(168)); Assert.That(names.Distinct().Count(), Is.EqualTo(168));
                var red = FiveLearningContract.CaptureFrame(match, Team.Red);
                var navy = FiveLearningContract.CaptureFrame(match, Team.Navy);
                for (var i = 0; i < red.Length; i++) Assert.That(red[i], Is.EqualTo(navy[i]).Within(.00001), names[i]);
                match.ball.linearVelocity = new Vector3(5, 0, 7);
                world.Step(10);
                Assert.That(FiveLearningContract.CaptureFrame(match, Team.Red).All(v => float.IsFinite(v) && v >= -1 && v <= 1), Is.True);
                Assert.Throws<ArgumentOutOfRangeException>(() => FiveLearningContract.CaptureFrame(match, (Team)2));
                match.ball.linearVelocity = Vector3.zero;
                match.players[1].Stamina.SetForScenario(float.NaN);
                Assert.Throws<InvalidOperationException>(() => FiveLearningContract.CaptureFrame(match, Team.Red));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator OpponentIntentTargetsAndReservationsDoNotLeak()
        {
            using (var world = new FiveTestWorld())
            {
                var match = world.match;
                world.Place(6, new Vector2(5, 8), Vector2.left); world.Give(6);
                var before = FiveLearningContract.CaptureFrame(match, Team.Red);
                for (var command = 0; command < 8; command++)
                {
                    match.SelectCommand(Team.Navy, (FiveCommand)command, "private-opponent", true);
                    match.players[7].SetTarget(new Vector2(command, -command), Vector2.one);
                    Assert.That(FiveLearningContract.CaptureFrame(match, Team.Red), Is.EqualTo(before));
                }
                Assert.That(match.RequestPass(match.players[6], match.players[7]), Is.True);
                var opponentPass = FiveLearningContract.CaptureFrame(match, Team.Red);
                Assert.That(opponentPass, Is.EqualTo(before));
                Assert.That(FiveLearningContract.CaptureFrame(match, Team.Navy)[35], Is.EqualTo(1), "Own preparation is observable.");
                match.CancelPass("test-reservation-change");
                Assert.That(match.RequestPass(match.players[6], match.players[8]), Is.True);
                Assert.That(FiveLearningContract.CaptureFrame(match, Team.Red), Is.EqualTo(before));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator EveryActionRemainsLegalInEveryPossessionPhase()
        {
            using (var world = new FiveTestWorld())
            {
                var match = world.match;
                foreach (var owner in new[] {-1, 1, 6}) foreach (var team in new[] {Team.Red, Team.Navy})
                    for (var action = 0; action < 8; action++)
                    {
                        match.ResetMatch(); if (owner >= 0) world.Give(owner);
                        Assert.That(FiveLearningContract.Apply(match, team, action), Is.True);
                        Assert.That((int)match.Decision(team).command, Is.EqualTo(action));
                        Assert.That(FiveLearningContract.Apply(match, team, (action + 1) % 8), Is.False);
                    }
                Assert.Throws<ArgumentOutOfRangeException>(() => FiveLearningContract.Apply(match, Team.Red, -1));
                Assert.Throws<ArgumentOutOfRangeException>(() => FiveLearningContract.Apply(match, Team.Red, 8));
                match.ResetMatch(); match.ruleManagers[0] = true;
                Assert.Throws<InvalidOperationException>(() => FiveLearningContract.Apply(match, Team.Red, 0));
            }
            yield return null;
        }

        [UnityTest] public IEnumerator PauseGoalWaitAndReselectionPreserveTheExistingContract()
        {
            using (var world = new FiveTestWorld())
            {
                var match = world.match;
                match.players[1].Stamina.SetForScenario(.3f);
                Assert.That(FiveLearningContract.Apply(match, Team.Red, 2), Is.True);
                match.Paused = true; world.Step(160);
                Assert.That(match.Elapsed, Is.Zero); Assert.That(FiveLearningContract.Apply(match, Team.Red, 7), Is.False);
                match.Paused = false; world.Step(149);
                Assert.That(FiveLearningContract.Apply(match, Team.Red, 7), Is.False);
                world.Step(2); var stamina = match.players[1].Stamina.Value;
                var revision = match.Decision(Team.Red).revision;
                Assert.That(FiveLearningContract.Apply(match, Team.Red, 2), Is.True);
                Assert.That(match.Decision(Team.Red).revision, Is.EqualTo(revision));
                Assert.That(match.players[1].Stamina.Value, Is.EqualTo(stamina));
                match.Score(Team.Red);
                Assert.That(FiveLearningContract.Apply(match, Team.Red, 7), Is.False);
                while (match.State == FiveMatchState.GoalPause) world.Step(1);
                Assert.That(FiveLearningContract.Apply(match, Team.Red, 7), Is.True);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator ScoresPayOnceWithNoPassCommandOrResetBonus()
        {
            using (var world = new FiveTestWorld())
            {
                var match = world.match; var rewards = new FiveScoreReward(); rewards.Begin(match);
                for (var command = 0; command < 8; command++) match.SelectCommand(Team.Red, (FiveCommand)command, "audit", true);
                world.Step(50); Assert.That(rewards.Consume(match), Is.Zero);
                match.Score(Team.Red); match.Score(Team.Red);
                Assert.That(rewards.Consume(match), Is.EqualTo(1)); Assert.That(rewards.Consume(match), Is.Zero);
                match.ResetRound(); Assert.That(rewards.Consume(match), Is.Zero);
                match.Score(Team.Navy); Assert.That(rewards.Consume(match), Is.EqualTo(-1));
                Assert.That(rewards.RedTotal, Is.Zero);
                match.ResetMatch(); Assert.Throws<InvalidOperationException>(() => rewards.Consume(match));
                rewards.Begin(match); Assert.That(rewards.Consume(match), Is.Zero);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator LearningBridgeCompletesTwoMatchesAndResetsBothTeamsTogether()
        {
            var traces = new List<FiveLearningTrace>();
            using (var world = new FiveTestWorld())
            {
                var session = FiveLearningFactory.Create(world.match, false, false);
                session.Trace += traces.Add; session.InitializeSession();
                var initialEpisode = session.Episode;
                for (var step = 0; session.CompletedMatches < 2 && step < 40000; step++)
                {
                    session.Step(.02f);
                    if (step % 500 == 0) yield return null;
                }
                Assert.That(session.CompletedMatches, Is.EqualTo(2));
                Assert.That(session.Episode, Is.EqualTo(initialEpisode + 1));
                Assert.That(world.match.State, Is.EqualTo(FiveMatchState.Finished));
                Assert.That(world.match.BoundaryCorrections, Is.Zero);
                var terminals = traces.Where(t => t.kind == "terminal").ToArray();
                Assert.That(terminals, Has.Length.EqualTo(4));
                foreach (var terminal in terminals)
                {
                    Assert.That(terminal.observation, Has.Length.EqualTo(676));
                    Assert.That(terminal.elapsed, Is.GreaterThanOrEqualTo(300));
                    Assert.That(terminal.reward, Is.EqualTo((terminal.redScore - terminal.navyScore) * (terminal.team == 0 ? 1 : -1)));
                    Assert.That(terminal.observation[20], Is.EqualTo(1));
                    Assert.That(terminal.observation.All(float.IsFinite), Is.True);
                }
                foreach (var group in traces.Where(t => t.kind == "decision").GroupBy(t => (t.episode, t.team)))
                {
                    Assert.That(group.Count(), Is.GreaterThanOrEqualTo(100));
                    Assert.That(group.First().elapsed, Is.EqualTo(0));
                    foreach (var row in group)
                    {
                        Assert.That(row.nextDecisionAt - row.elapsed, Is.EqualTo(3).Within(.0001));
                        Assert.That(row.observation, Has.Length.EqualTo(676));
                        for (var frame = 0; frame < 4; frame++)
                            if (row.observation[672 + frame] == 1)
                            {
                                Assert.That(row.sampleErrors[frame], Is.InRange(0, .0201f));
                                Assert.That(row.elapsed - frame * .25f - row.sampleTimes[frame], Is.EqualTo(row.sampleErrors[frame]).Within(.00001));
                            }
                            else Assert.That(row.observation.Skip(frame * 168).Take(168).All(v => v == 0), Is.True);
                    }
                }
                Directory.CreateDirectory(FiveP7Evidence.DirectoryPath);
                File.WriteAllLines(Path.Combine(FiveP7Evidence.DirectoryPath, "p8-two-matches.jsonl"), traces.Select(t => JsonUtility.ToJson(t)));
                session.gameObject.SetActive(false);
                Assert.That(session.Initialized, Is.False);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator ExplicitTrainerShutdownDoesNotApplyDefaultActions()
        {
            using(var world=new FiveTestWorld())
            {
                var session=FiveLearningFactory.Create(world.match,false,false);
                Directory.CreateDirectory(FiveP7Evidence.DirectoryPath);
                var path=Path.Combine(FiveP7Evidence.DirectoryPath,"shutdown-"+Guid.NewGuid()+".signal");
                session.shutdownFile=path;
                try
                {
                    session.InitializeSession();session.Step(.02f);
                    var elapsed=world.match.Elapsed;
                    var decisions=session.agents[0].Decisions;
                    var streak=(int[])world.match.Decision(Team.Red).consecutiveSelections.Clone();
                    session.agents[0].PrepareDecision();
                    File.WriteAllText(path,"safe shutdown");
                    session.agents[0].OnActionReceived(new Unity.MLAgents.Actuators.ActionBuffers(
                        Unity.MLAgents.Actuators.ActionSegment<float>.Empty,
                        new Unity.MLAgents.Actuators.ActionSegment<int>(new[]{0})));
                    session.Step(.02f);
                    Assert.That(session.agents[0].DecisionPending,Is.False);
                    Assert.That(session.agents[0].Decisions,Is.EqualTo(decisions));
                    Assert.That(world.match.Elapsed,Is.EqualTo(elapsed));
                    Assert.That(world.match.Decision(Team.Red).consecutiveSelections,Is.EqualTo(streak));
                }
                finally{if(File.Exists(path))File.Delete(path);}
            }
            yield return null;
        }

        [UnityTest] public IEnumerator OneMatchEvaluationFlushesTerminalWithoutStartingAnotherMatch()
        {
            using (var world = new FiveTestWorld())
            {
                var session=FiveLearningFactory.Create(world.match,false,false);
                session.stopAfterFirstMatch=true;
                var traces=new List<FiveLearningTrace>();session.Trace+=traces.Add;
                session.InitializeSession();
                var episode=session.Episode;
                for(int step=0;session.CompletedMatches<1&&step<20000;step++)
                {
                    session.Step(.02f);
                    if(step%500==0)yield return null;
                }
                Assert.That(session.CompletedMatches,Is.EqualTo(1));
                var decisions=traces.Count(t=>t.kind=="decision");
                var elapsed=world.match.Elapsed;
                for(int i=0;i<1000;i++)session.Step(.02f);
                Assert.That(session.Episode,Is.EqualTo(episode));
                Assert.That(session.CompletedMatches,Is.EqualTo(1));
                Assert.That(session.automaticSteps,Is.False);
                Assert.That(world.match.State,Is.EqualTo(FiveMatchState.Finished));
                Assert.That(world.match.Elapsed,Is.EqualTo(elapsed));
                Assert.That(traces.Count(t=>t.kind=="terminal"),Is.EqualTo(2));
                Assert.That(traces.Count(t=>t.kind=="decision"),Is.EqualTo(decisions));
                Assert.That(session.agents.All(a=>!a.DecisionPending),Is.True);
            }
            yield return null;
        }

        [UnityTest] public IEnumerator ExternalResetDiscardsPriorMatchRewardAndWaitsForFreshActions()
        {
            using (var world = new FiveTestWorld())
            {
                var session = FiveLearningFactory.Create(world.match, false, false);
                session.InitializeSession(); session.Step(.02f);
                world.match.Score(Team.Red); session.Step(.02f);
                Assert.That(session.agents[0].GetCumulativeReward(), Is.EqualTo(1));
                var episode = session.Episode;
                // Same SDK path used for a Python reset; no private game-state mutation.
                typeof(Academy).GetMethod("ForcedFullReset", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(Academy.Instance, null);
                Assert.That(session.Episode, Is.EqualTo(episode + 1));
                Assert.That(world.match.Elapsed, Is.Zero);
                Assert.That(world.match.RedScore, Is.Zero);
                foreach (var agent in session.agents)
                {
                    Assert.That(agent.GetCumulativeReward(), Is.Zero, "Prior-episode reward leaked across external reset.");
                    Assert.That(agent.DecisionPending, Is.False);
                    Assert.That(session.History.Capture(world.match, agent.team).values.Skip(672), Is.EqualTo(new float[] {1, 0, 0, 0}));
                }
                session.Step(.02f);
                foreach (var team in new[] {Team.Red, Team.Navy}) Assert.That(world.match.Decision(team).lastDecisionAt, Is.Zero);
            }
            yield return null;
        }
    }
}
