using System;
using MachineLearning.Soccer;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    public enum MNG_DefenseScenarioKind
    {
        Direct = 0,
        Wide = 1,
        NeutralContest = 2
    }

    public readonly struct MNG_DefenseScenario
    {
        public readonly int Index;
        public readonly int Seed;
        public readonly MNG_DefenseScenarioKind Kind;
        public readonly int NavyCarrierSlot;
        public readonly bool StartsNeutral;
        public readonly Vector2 BallPosition;
        public readonly Vector2[] RedPositions;
        public readonly Vector2[] NavyPositions;

        public MNG_DefenseScenario(
            int index,
            int seed,
            MNG_DefenseScenarioKind kind,
            int navyCarrierSlot,
            bool startsNeutral,
            Vector2 ballPosition,
            Vector2[] redPositions,
            Vector2[] navyPositions)
        {
            Index = index;
            Seed = seed;
            Kind = kind;
            NavyCarrierSlot = navyCarrierSlot;
            StartsNeutral = startsNeutral;
            BallPosition = ballPosition;
            RedPositions = redPositions;
            NavyPositions = navyPositions;
        }
    }

    public static class MNG_DefenseScenarioGenerator
    {
        public const int GeneratorVersion = 2;

        public static MNG_DefenseScenario Generate(int seed, int scenarioIndex, float fieldHalfLength)
        {
            if (scenarioIndex < 0) throw new ArgumentOutOfRangeException(nameof(scenarioIndex));
            if (!MNG_MatchSnapshot.IsFinite(fieldHalfLength) || fieldHalfLength <= 35f)
                throw new ArgumentOutOfRangeException(nameof(fieldHalfLength));

            var pairIndex = scenarioIndex / 2;
            var mirror = (scenarioIndex & 1) != 0 ? -1f : 1f;
            var random = new System.Random(unchecked(seed * 73856093 + pairIndex * 19349663));
            var kind = (MNG_DefenseScenarioKind)(pairIndex % 3);
            var distance = Lerp(
                MNG_CurriculumCatalog.M2MinimumThreatDistance,
                MNG_CurriculumCatalog.M2MaximumThreatDistance,
                random.NextDouble());
            var laneLimit = kind == MNG_DefenseScenarioKind.Wide ? 13f : 7f;
            var laneBase = kind == MNG_DefenseScenarioKind.Wide
                ? Lerp(8f, laneLimit, random.NextDouble())
                : Lerp(-laneLimit, laneLimit, random.NextDouble());
            var lane = laneBase * mirror;
            var ball = new Vector2(-fieldHalfLength + distance, lane);
            var startsNeutral = kind == MNG_DefenseScenarioKind.NeutralContest;
            const int carrierSlot = 3;

            var red = new Vector2[MNG_MatchSnapshot.PlayersPerTeam];
            red[0] = new Vector2(-fieldHalfLength + 3.5f, Mathf.Clamp(lane * 0.35f, -5f, 5f));
            red[1] = ball + new Vector2(-6f, -6f * mirror);
            red[2] = ball + new Vector2(-3f, 7f * mirror);
            red[3] = ball + new Vector2(startsNeutral ? -4f : 5f, -1.5f * mirror);

            var navy = new Vector2[MNG_MatchSnapshot.PlayersPerTeam];
            navy[0] = new Vector2(fieldHalfLength - 4f, 0f);
            navy[1] = ball + new Vector2(7f, -9f * mirror);
            navy[2] = ball + new Vector2(9f, 8f * mirror);
            navy[carrierSlot] = ball + Vector2.right
                * (startsNeutral ? 2f : MNG_KickPlate.DribbleAnchorForward);

            MNG_SpawnJitter.Apply(random, mirror, red);
            // A confirmed-possession carrier must remain on its exact kick-plate
            // anchor. In neutral starts it can vary like the other players.
            MNG_SpawnJitter.Apply(random, mirror, navy, startsNeutral ? -1 : carrierSlot);

            return new MNG_DefenseScenario(
                scenarioIndex,
                seed,
                kind,
                carrierSlot,
                startsNeutral,
                ball,
                red,
                navy);
        }

        static float Lerp(float minimum, float maximum, double t)
            => minimum + (maximum - minimum) * (float)t;
    }
}
