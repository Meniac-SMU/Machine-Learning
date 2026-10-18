using System;
using System.Collections.Generic;

namespace MachineLearning.Soccer.Manager5v5
{
    /// <summary>Simulation-clock history of the same public whitelist. No interpolated or future state.</summary>
    public sealed class FiveObservationHistory
    {
        sealed class Sample
        {
            public float time;
            public float[][] teams;
        }
        readonly List<Sample> samples = new List<Sample>();
        public int Count => samples.Count;

        public void Reset(FiveMatch match)
        {
            samples.Clear();
            Record(match);
        }

        // Keep actual physics snapshots so arbitrary decision times can select the latest
        // sample at/before t-0.25/0.50/0.75, with <= one 20 ms physics-step error.
        public void Record(FiveMatch match)
        {
            if (match.Paused || match.State != FiveMatchState.Playing) return;
            if (samples.Count > 0 && match.Elapsed < samples[samples.Count - 1].time)
                throw new InvalidOperationException("A new match requires a history reset.");
            if (samples.Count > 0 && match.Elapsed == samples[samples.Count - 1].time) return;
            samples.Add(new Sample { time = match.Elapsed, teams = new[] {
                FiveLearningContract.CaptureFrame(match, Team.Red), FiveLearningContract.CaptureFrame(match, Team.Navy) } });
            var oldestNeeded = match.Elapsed - FiveLearningContract.HistoryInterval * (FiveLearningContract.FrameCount - 1);
            while (samples.Count > 1 && samples[1].time <= oldestNeeded) samples.RemoveAt(0);
        }

        public FiveHistoryObservation Capture(FiveMatch match, Team team)
        {
            FiveLearningContract.CheckTeam(team);
            var result = new FiveHistoryObservation();
            Array.Copy(FiveLearningContract.CaptureFrame(match, team), result.values, FiveLearningContract.FrameSize);
            result.values[FiveLearningContract.FrameSize * FiveLearningContract.FrameCount] = 1;
            result.sampleTimes[0] = match.Elapsed; result.sampleErrors[0] = 0;
            for (var frame = 1; frame < FiveLearningContract.FrameCount; frame++)
            {
                var target = match.Elapsed - frame * FiveLearningContract.HistoryInterval;
                Sample selected = null;
                for (var i = samples.Count - 1; i >= 0; i--)
                    if (samples[i].time <= target) { selected = samples[i]; break; }
                if (selected == null) continue; // Cleared or not yet enough history: zeros + invalid flag.
                var error = target - selected.time;
                if (error > FiveLearningContract.MaximumSampleError)
                    throw new InvalidOperationException("Observation history has a missing simulation sample.");
                Array.Copy(selected.teams[(int)team], 0, result.values, frame * FiveLearningContract.FrameSize, FiveLearningContract.FrameSize);
                result.values[FiveLearningContract.FrameSize * FiveLearningContract.FrameCount + frame] = 1;
                result.sampleTimes[frame] = selected.time; result.sampleErrors[frame] = error;
            }
            return result;
        }
    }

    public sealed class FiveHistoryObservation
    {
        public readonly float[] values = new float[FiveLearningContract.ObservationSize];
        public readonly float[] sampleTimes = { -1, -1, -1, -1 };
        public readonly float[] sampleErrors = { -1, -1, -1, -1 };
    }
}
