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
        /// <summary>
        /// How close you have to stand before each thing offers itself. These live here rather
        /// than in SurfaceMode because the clearance rule below is only meaningful in terms of
        /// them, and the check that verifies it has to be able to say so.
        /// </summary>
        public const float CacheRange = 1.6f, LandmarkRange = 2.6f;

        /// <summary>
        /// How far a landmark keeps from a cache, so their prompts never fight.
        ///
        /// This was verified by asserting that the gap between the two exceeded this constant -
        /// against a placement function that produces the gap by enforcing this constant. The
        /// check tested the code against itself: setting the clearance to zero made "gap > 0"
        /// trivially true and the whole thing passed. It read like real work for several
        /// revisions.
        ///
        /// What actually matters is the two prompt ranges, which is a fact about the game rather
        /// than about this number, and that is what the check measures now - both that the rule
        /// is big enough to be worth having, and that every world's seeds actually clear it.
        /// </summary>
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

        /// <summary>
        /// Where the landmark would fall before the clearance rule looks at it. Exposed so the
        /// check can ask whether the rule ever actually fires: a branch no world needs is a
        /// branch nobody has tested, and this one rescues exactly one world.
        /// </summary>
        public static Vector2 RawLandmarkPosition(PlanetDef planet)
        {
            var rng = new System.Random(planet.Seed ^ 0x1A4D);
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float dist = planet.SurfaceRadius * (0.55f + 0.25f * (float)rng.NextDouble());
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
