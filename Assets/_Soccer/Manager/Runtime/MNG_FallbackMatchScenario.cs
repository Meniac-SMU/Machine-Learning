using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public enum MNG_FallbackMatchStartKind
    {
        Kickoff = 0,
        RedPossession = 1,
        NavyPossession = 2,
        NeutralContest = 3
    }

    public readonly struct MNG_FallbackMatchScenario
    {
        public readonly int Index;
        public readonly int Seed;
        public readonly MNG_FallbackMatchStartKind Kind;
        public readonly int CarrierSlot;
        public readonly Vector2 BallPosition;
        public readonly Vector2[] RedPositions;
        public readonly Vector2[] NavyPositions;

        public MNG_FallbackMatchScenario(
            int index,
            int seed,
            MNG_FallbackMatchStartKind kind,
            int carrierSlot,
            Vector2 ballPosition,
            Vector2[] redPositions,
            Vector2[] navyPositions)
        {
            Index = index;
            Seed = seed;
            Kind = kind;
            CarrierSlot = carrierSlot;
            BallPosition = ballPosition;
            RedPositions = redPositions;
            NavyPositions = navyPositions;
        }
    }

    public static class MNG_FallbackMatchScenarioGenerator
    {
        public const int GeneratorVersion = 2;

        public static MNG_FallbackMatchScenario Generate(int seed, int scenarioIndex, float fieldHalfLength)
        {
            if (scenarioIndex < 0) throw new ArgumentOutOfRangeException(nameof(scenarioIndex));
            if (!MNG_MatchSnapshot.IsFinite(fieldHalfLength) || fieldHalfLength <= 35f)
                throw new ArgumentOutOfRangeException(nameof(fieldHalfLength));

            var pairIndex = scenarioIndex / 2;
            var mirror = (scenarioIndex & 1) == 0 ? 1f : -1f;
            var random = new System.Random(unchecked(seed * 83492791 + pairIndex * 297121507));
            var kind = (MNG_FallbackMatchStartKind)(pairIndex % 4);
            var ballX = kind switch
            {
                MNG_FallbackMatchStartKind.RedPossession => Lerp(-25f, 20f, random.NextDouble()),
                MNG_FallbackMatchStartKind.NavyPossession => Lerp(-20f, 25f, random.NextDouble()),
                _ => Lerp(-8f, 8f, random.NextDouble())
            };
            var ball = kind == MNG_FallbackMatchStartKind.Kickoff
                ? Vector2.zero
                : new Vector2(ballX, Lerp(-10f, 10f, random.NextDouble()) * mirror);
            var carrierSlot = kind == MNG_FallbackMatchStartKind.RedPossession
                || kind == MNG_FallbackMatchStartKind.NavyPossession ? 3 : -1;

            var red = new Vector2[MNG_MatchSnapshot.PlayersPerTeam];
            red[0] = new Vector2(-fieldHalfLength + 4f, Mathf.Clamp(ball.y * 0.3f, -5f, 5f));
            red[1] = ball + new Vector2(-12f, -10f * mirror);
            red[2] = ball + new Vector2(-8f, 10f * mirror);
            red[3] = ball + new Vector2(
                kind == MNG_FallbackMatchStartKind.RedPossession
                    ? -MNG_KickPlate.DribbleAnchorForward : -5f,
                0f);

            var navy = new Vector2[MNG_MatchSnapshot.PlayersPerTeam];
            navy[0] = new Vector2(fieldHalfLength - 4f, Mathf.Clamp(ball.y * 0.3f, -5f, 5f));
            navy[1] = ball + new Vector2(12f, -10f * mirror);
            navy[2] = ball + new Vector2(8f, 10f * mirror);
            navy[3] = ball + new Vector2(
                kind == MNG_FallbackMatchStartKind.NavyPossession
                    ? MNG_KickPlate.DribbleAnchorForward : 5f,
                0f);

            MNG_SpawnJitter.Apply(
                random,
                mirror,
                red,
                kind == MNG_FallbackMatchStartKind.RedPossession ? carrierSlot : -1);
            MNG_SpawnJitter.Apply(
                random,
                mirror,
                navy,
                kind == MNG_FallbackMatchStartKind.NavyPossession ? carrierSlot : -1);

            return new MNG_FallbackMatchScenario(
                scenarioIndex, seed, kind, carrierSlot, ball, red, navy);
        }

        static float Lerp(float minimum, float maximum, double t)
            => minimum + (maximum - minimum) * (float)t;
    }
}
