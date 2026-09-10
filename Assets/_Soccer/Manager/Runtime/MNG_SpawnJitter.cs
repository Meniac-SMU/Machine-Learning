using System;
using UnityEngine;

namespace MachineLearning.Soccer.Manager
{
    /// <summary>
    /// Adds a small deterministic offset around authored formation positions.
    /// Mirrored scenario pairs share X offsets and invert Y offsets.
    /// </summary>
    public static class MNG_SpawnJitter
    {
        public const float MaximumAxisOffset = 0.75f;

        public static Vector2 Sample(System.Random random, float mirror = 1f)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            if (mirror != 1f && mirror != -1f)
                throw new ArgumentOutOfRangeException(nameof(mirror));
            return new Vector2(
                Signed(random) * MaximumAxisOffset,
                Signed(random) * MaximumAxisOffset * mirror);
        }

        public static void Apply(
            System.Random random,
            float mirror,
            Vector2[] positions,
            int lockedSlot = -1)
        {
            if (positions == null) throw new ArgumentNullException(nameof(positions));
            for (var slot = 0; slot < positions.Length; slot++)
            {
                if (slot == lockedSlot) continue;
                positions[slot] += Sample(random, mirror);
            }
        }

        static float Signed(System.Random random) => (float)(random.NextDouble() * 2.0 - 1.0);
    }
}
