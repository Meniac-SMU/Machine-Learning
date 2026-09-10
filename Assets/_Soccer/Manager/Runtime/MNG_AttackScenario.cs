using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public enum MNG_AttackScenarioKind
    {
        Carry = 0,
        Pass = 1,
        Shot = 2
    }

    public readonly struct MNG_AttackScenario
    {
        public readonly int Index;
        public readonly int Seed;
        public readonly MNG_AttackScenarioKind Kind;
        public readonly int CarrierSlot;
        public readonly Vector2 BallPosition;
        public readonly Vector2[] RedPositions;
        public readonly Vector2[] NavyPositions;

        public MNG_AttackScenario(
            int index,
            int seed,
            MNG_AttackScenarioKind kind,
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

    public static class MNG_AttackScenarioGenerator
    {
        public const int GeneratorVersion = 2;

        public static MNG_AttackScenario Generate(int seed, int scenarioIndex, float fieldHalfLength)
        {
            if (scenarioIndex < 0) throw new ArgumentOutOfRangeException(nameof(scenarioIndex));
            if (!MNG_MatchSnapshot.IsFinite(fieldHalfLength) || fieldHalfLength <= 35f)
                throw new ArgumentOutOfRangeException(nameof(fieldHalfLength));

            var pairIndex = scenarioIndex / 2;
            var mirror = (scenarioIndex & 1) != 0 ? -1f : 1f;
            var random = new System.Random(unchecked(seed * 486187739 + pairIndex * 16777619));
            var kind = (MNG_AttackScenarioKind)(pairIndex % 3);
            var distance = kind switch
            {
                MNG_AttackScenarioKind.Shot => Lerp(20f, 23.5f, random.NextDouble()),
                MNG_AttackScenarioKind.Pass => Lerp(27f, 35f, random.NextDouble()),
                _ => Lerp(25f, 33f, random.NextDouble())
            };
            var lane = Lerp(-7.5f, 7.5f, random.NextDouble()) * mirror;
            var ball = new Vector2(fieldHalfLength - distance, lane);
            const int carrierSlot = 3;

            var red = new Vector2[MNG_MatchSnapshot.PlayersPerTeam];
            red[0] = new Vector2(-fieldHalfLength + 4f, 0f);
            red[1] = ball + new Vector2(-5f, -9f * mirror);
            red[2] = ball + new Vector2(kind == MNG_AttackScenarioKind.Pass ? 11f : 7f, 8f * mirror);
            red[carrierSlot] = ball - Vector2.right * MNG_KickPlate.DribbleAnchorForward;

            var navy = new Vector2[MNG_MatchSnapshot.PlayersPerTeam];
            navy[0] = new Vector2(fieldHalfLength - 3.5f, Mathf.Clamp(lane * 0.35f, -5f, 5f));
            navy[1] = ball + new Vector2(kind == MNG_AttackScenarioKind.Shot ? 7f : 5f, -5.5f * mirror);
            navy[2] = ball + new Vector2(kind == MNG_AttackScenarioKind.Pass ? 5f : 9f, 0.5f * mirror);
            navy[3] = ball + new Vector2(-4f, 8.5f * mirror);

            // Keep the carrier exactly behind the ball so possession starts through
            // the kick plate, while varying every other formation slot slightly.
            MNG_SpawnJitter.Apply(random, mirror, red, carrierSlot);
            MNG_SpawnJitter.Apply(random, mirror, navy);

            return new MNG_AttackScenario(
                scenarioIndex, seed, kind, carrierSlot, ball, red, navy);
        }

        static float Lerp(float minimum, float maximum, double t)
            => minimum + (maximum - minimum) * (float)t;
    }
}
