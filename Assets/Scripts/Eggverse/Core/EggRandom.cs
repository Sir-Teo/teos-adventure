using System;

namespace Eggverse
{
    /// <summary>
    /// The single source of randomness for battle maths and encounter rolls.
    /// Defaults to Unity's generator, but can be swapped for a seeded one so battles
    /// are reproducible — which is what lets the balance harness play thousands of
    /// fights against the real formulas outside the editor.
    /// </summary>
    public static class EggRandom
    {
        static Func<float> source = () => UnityEngine.Random.value;

        /// <summary>Replaces the generator. Pass null to go back to Unity's.</summary>
        public static void SetSource(Func<float> next)
        {
            source = next ?? (() => UnityEngine.Random.value);
        }

        /// <summary>Uniform in [0, 1).</summary>
        public static float Value => source();

        public static float Range(float min, float max) => min + source() * (max - min);

        /// <summary>Uniform integer in [min, maxExclusive).</summary>
        public static int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            int span = maxExclusive - min;
            int roll = min + (int)(source() * span);
            return roll >= maxExclusive ? maxExclusive - 1 : roll;
        }

        /// <summary>Deterministic 32-bit xorshift, for seeded runs.</summary>
        public static Func<float> Seeded(uint seed)
        {
            uint state = seed == 0 ? 0x9E3779B9u : seed;
            return () =>
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return (state & 0xFFFFFF) / (float)0x1000000;
            };
        }
    }
}
