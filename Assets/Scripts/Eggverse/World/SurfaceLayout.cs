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

        /// <summary>How a world scatters its shell fields.</summary>
        public enum FieldLayout { Scattered, Ring, Clustered }

        /// <summary>One shell field: where it sits and how far into it counts as inside.</summary>
        public struct Field
        {
            public Vector2 At;
            public float Radius;      // what the eye sees
            public float Walkable;    // what the encounter counter uses, which is 0.8 of it
        }

        /// <summary>
        /// Where the shell fields fall on a world.
        ///
        /// Lifted out of the surface builder for the same reason the cache and the landmark
        /// were: encounters only tick inside these patches, so how much of a world is field
        /// decides how often a player meets anything — and that could not be measured, because
        /// the layout only existed as GameObjects.
        ///
        /// Same seed, same stream, same order as the builder, which is what makes this the
        /// layout rather than a second opinion about it.
        /// </summary>
        public static System.Collections.Generic.List<Field> Fields(PlanetDef planet)
        {
            float R = planet.SurfaceRadius;
            var rng = new System.Random(planet.Seed);
            var outp = new System.Collections.Generic.List<Field>();

            int layout = planet.Seed % 3;
            int count = 7 + (planet.Seed / 7) % 5;

            int clumps = 2 + (planet.Seed / 13) % 2;
            var clumpCentres = new Vector2[clumps];
            for (int c = 0; c < clumps; c++)
            {
                float ca = (float)rng.NextDouble() * Mathf.PI * 2f;
                float cd = Mathf.Lerp(R * 0.30f, R * 0.70f, (float)rng.NextDouble());
                clumpCentres[c] = Polar(ca, cd);
            }

            for (int i = 0; i < count; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector2 pos;
                float radius;

                if (layout == (int)FieldLayout.Ring)
                {
                    pos = Polar(a, Mathf.Lerp(R * 0.60f, R * 0.82f, (float)rng.NextDouble()));
                    radius = Mathf.Lerp(4.5f, 6.0f, (float)rng.NextDouble());
                }
                else if (layout == (int)FieldLayout.Clustered)
                {
                    var centre = clumpCentres[i % clumps];
                    pos = centre + Polar(a, Mathf.Lerp(0f, 8f, (float)rng.NextDouble()));
                    pos = Vector2.ClampMagnitude(pos, R * 0.90f);
                    radius = Mathf.Lerp(3.5f, 5.5f, (float)rng.NextDouble());
                }
                else
                {
                    pos = Polar(a, Mathf.Lerp(R * 0.28f, R * 0.86f, (float)rng.NextDouble()));
                    radius = Mathf.Lerp(4f, 6.5f, (float)rng.NextDouble());
                }

                outp.Add(new Field { At = pos, Radius = radius, Walkable = radius * 0.8f });
            }
            return outp;
        }

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
