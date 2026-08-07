using UnityEngine;

namespace Eggverse
{
    /// <summary>
    /// Where the two findable things sit on a landed world.
    ///
    /// This existed in three places at once - the surface builder, the shipped self-check, and
    /// the artcheck renderer - each with its own copy of the same trigonometry. The renders
    /// promptly disagreed with the game: Mosswell drew its landmark on top of its cache, because
    /// the mock had never heard of the clearance rule. Copies do not stay in step, so there is
    /// one copy and everything calls it.
    ///
    /// Both positions are a pure function of the planet. Nothing here may depend on save state:
    /// the first version keyed the clearance off whether the cache object still existed, which
    /// meant digging up a cache silently moved that world's landmark to the far side. A thing you
    /// walked to once has to be in the same place when you come back.
    /// </summary>
    public static class SurfaceLayout
    {
        /// <summary>How far a landmark keeps from a cache, so their prompts never fight.</summary>
        public const float LandmarkClearance = 9f;

        public static Vector2 Polar(float angle, float dist)
        {
            return new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);
        }

        public static Vector2 CachePosition(PlanetDef planet)
        {
            var rng = new System.Random(planet.Seed ^ 0x5EED);
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float dist = planet.SurfaceRadius * (0.68f + 0.22f * (float)rng.NextDouble());
            return Polar(angle, dist);
        }

        public static Vector2 LandmarkPosition(PlanetDef planet)
        {
            var rng = new System.Random(planet.Seed ^ 0x1A4D);
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float dist = planet.SurfaceRadius * (0.55f + 0.25f * (float)rng.NextDouble());

            // The cache draws from an independent stream in an overlapping band, so the two can
            // land together - Mosswell put them 5.0u apart against prompt ranges summing to 4.8.
            // That passed, but by 0.2u of luck. Half a turn away is deliberate.
            if (PlanetDatabase.HasCache(planet.Id) &&
                Vector2.Distance(CachePosition(planet), Polar(angle, dist)) < LandmarkClearance)
                angle += Mathf.PI;

            return Polar(angle, dist);
        }
    }
}
