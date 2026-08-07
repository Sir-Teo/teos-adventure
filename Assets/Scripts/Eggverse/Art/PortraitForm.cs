namespace Eggverse
{
    /// <summary>
    /// A distinguishing feature above and beyond proportion. Two people can share a hood width;
    /// nobody shares a crest with a scarf, at any size, in any light.
    /// </summary>
    public enum PortraitMark
    {
        Plain,      // no addition - the quiet ones
        Brow,       // a bar across the forehead, above the eyes
        Crest,      // a ridge standing above the hood
        Collar,     // a raised band across the shoulders
        Temples,    // a pair of marks at the outer edges of the face, at eye height
        Scarf,      // a band across the lower face
    }

    // The first pass had a "Cheeks" mark - two dots below the eyes - and a "Visor" drawn at eye
    // height with the eyes laid on top of it. Drawn side by side, the first read as nostrils and
    // the second as a grimacing mouth, so seven of seventeen residents were piglets and three
    // were scowling. A mark has to land somewhere a face does not already have a feature.

    /// <summary>
    /// One person's face, as numbers.
    ///
    /// ProcArt.Portrait took a name and threw it away: it was used to key the cache and for
    /// nothing else. Every resident of every world, plus Teo and plus Amy, was the same hooded
    /// silhouette in a different colour — and the roster is five shades of purple deep (Vess,
    /// Moth, Wren, Lune, Pim), which collapse further for a dichromat player. A cast told apart
    /// only by tint is a cast a colourblind player cannot tell apart at all.
    ///
    /// So the shape comes from the name too. Everyone stays recognisably the same species —
    /// shoulders, a hood, a face, two eyes — and varies within it, the way the eggs and the
    /// landmarks already do.
    ///
    /// Plain floats, no UnityEngine types: the shipped self-check and the renderer both build
    /// these, and both run outside the engine.
    /// </summary>
    public struct PortraitForm
    {
        public float HoodWidth, HoodHeight, HoodRise;
        public float HeadWidth, HeadHeight, HeadRise;
        public float ShoulderWidth, ShoulderRise;
        public float EyeSpacing, EyeSize, EyeRise;
        public PortraitMark Mark;

        /// <summary>
        /// A stable hash. Not string.GetHashCode: that is allowed to differ between runs and
        /// between platforms, which would give a player a different face on Tuesday.
        /// </summary>
        public static int Hash(string key)
        {
            unchecked
            {
                int h = unchecked((int)2166136261u);
                for (int i = 0; i < (key ?? "").Length; i++)
                {
                    h ^= key[i];
                    h *= 16777619;
                }
                return h & 0x7FFFFFFF;
            }
        }

        /// <summary>One of `steps` evenly spaced values in [lo, hi], chosen by the nth hash draw.</summary>
        static float Pick(int hash, int draw, float lo, float hi, int steps)
        {
            // Each draw gets its own avalanche rather than its own slice of one hash. Slicing
            // looked fine on the numbers and was not: across seventeen residents it put eight of
            // them in one bucket of six and left another empty, which on the contact sheet was
            // eight people wearing the same brow and a crest nobody wore. Bit slices of an FNV
            // hash are correlated; a finalizer is what breaks that.
            uint v = unchecked((uint)hash * 2654435761u + (uint)draw * 2246822519u);
            v ^= v >> 15; v = unchecked(v * 2246822519u);
            v ^= v >> 13; v = unchecked(v * 3266489917u);
            v ^= v >> 16;
            int k = (int)(v % (uint)steps);
            return steps == 1 ? lo : lo + (hi - lo) * k / (steps - 1);
        }

        /// <summary>
        /// Who wears what.
        ///
        /// The mark was hashed like everything else, and with seventeen names falling into six
        /// buckets a bucket comes up empty about a quarter of the time - a shape drawn, checked
        /// and never worn by anybody. Better mixing does not fix that; it is what random
        /// assignment does. So the one trait that reads as character is authored, and coverage
        /// becomes a fact about the cast rather than a coin landing right.
        ///
        /// Proportions stay hashed. Nobody needs to decide how wide Bram's hood is.
        /// </summary>
        static readonly System.Collections.Generic.Dictionary<string, PortraitMark> Authored =
            new System.Collections.Generic.Dictionary<string, PortraitMark>
            {
                { "Teo",    PortraitMark.Plain },    // the player, and the plainest of them
                { "Amy",    PortraitMark.Crest },    // the one waiting at the end of it

                { "Ori",    PortraitMark.Brow },     // gives the opening brief; a teacher's frown
                { "Marn",   PortraitMark.Crest },    // Voltacrest, and it shows
                { "Sable",  PortraitMark.Scarf },    // wrapped up against Glacierim
                { "Vess",   PortraitMark.Temples },  // the same on both worlds she stands on
                { "Pim",    PortraitMark.Plain },
                { "Hob",    PortraitMark.Collar },   // forge collar, Cinderoost
                { "Nell",   PortraitMark.Scarf },    // the wind off Brineholt
                { "Bram",   PortraitMark.Temples },
                { "Sax",    PortraitMark.Collar },   // salvage rig, Tidewrack
                { "Quill",  PortraitMark.Brow },
                { "Moth",   PortraitMark.Temples },
                { "Wren",   PortraitMark.Crest },
                { "Lune",   PortraitMark.Plain },
                { "Tilda",  PortraitMark.Brow },
                { "Garrow", PortraitMark.Scarf },    // the old keeper of Cairnhold
            };

        /// <summary>Whether this speaker's mark was chosen rather than hashed.</summary>
        public static bool IsAuthored(string key) => Authored.ContainsKey(key ?? "");

        public static PortraitForm For(string key)
        {
            int h = Hash(key);
            PortraitMark mark;
            if (!Authored.TryGetValue(key ?? "", out mark))
                mark = (PortraitMark)(Pick(h, 11, 0f, 5f, 6) + 0.5f);
            return new PortraitForm
            {
                // The hood is the biggest read at a glance, so it gets the widest range.
                HoodWidth     = Pick(h, 0, 0.40f, 0.56f, 5),
                HoodHeight    = Pick(h, 1, 0.34f, 0.46f, 4),
                HoodRise      = Pick(h, 2, 0.20f, 0.32f, 4),

                HeadWidth     = Pick(h, 3, 0.34f, 0.42f, 4),
                HeadHeight    = Pick(h, 4, 0.40f, 0.48f, 4),
                HeadRise      = Pick(h, 5, 0.00f, 0.08f, 3),

                ShoulderWidth = Pick(h, 6, 0.62f, 0.84f, 4),
                ShoulderRise  = Pick(h, 7, -0.80f, -0.66f, 3),

                EyeSpacing    = Pick(h, 8, 0.115f, 0.185f, 4),
                EyeSize       = Pick(h, 9, 0.044f, 0.070f, 3),
                EyeRise       = Pick(h, 10, 0.01f, 0.10f, 4),

                Mark          = mark,
            };
        }

        /// <summary>
        /// One ellipse of a portrait. `Shade` multiplies the speaker's tint; `Glint` and `Eyes`
        /// are fixed, because a white highlight and a dark pupil are not that person's colour.
        /// </summary>
        public struct Blob
        {
            public float Cx, Cy, Rx, Ry, Shade, Soft, Alpha;
            public bool Eyes, Glint;
        }

        /// <summary>
        /// The whole face, in the order it is filled.
        ///
        /// This lives here rather than inside ProcArt because ProcArt cannot run outside the
        /// engine, and because the fault this file exists to fix was a function that took a name
        /// and drew the same thing regardless. Planting that fault back in - `For(key)` replaced
        /// with `For("Ori")` - went uncaught: the checks proved the forms differed and nothing
        /// proved the drawing used them. With the shapes out here, ProcArt has no shapes of its
        /// own to fall back on, and the renderer draws what the game draws.
        /// </summary>
        public static Blob[] Shapes(PortraitForm f)
        {
            var list = new System.Collections.Generic.List<Blob>
            {
                new Blob { Cx = 0f, Cy = 0f, Rx = 0.99f, Ry = 0.99f, Shade = 0.22f, Soft = 0.03f, Alpha = 1f },
                new Blob { Cx = 0f, Cy = f.ShoulderRise, Rx = f.ShoulderWidth, Ry = 0.50f,
                           Shade = 0.62f, Soft = 0.10f, Alpha = 1f },
            };

            if (f.Mark == PortraitMark.Crest)
                list.Add(new Blob { Cx = 0f, Cy = f.HoodRise + f.HoodHeight * 0.82f,
                                    Rx = f.HoodWidth * 0.30f, Ry = f.HoodHeight * 0.42f,
                                    Shade = 0.70f, Soft = 0.12f, Alpha = 1f });

            list.Add(new Blob { Cx = 0f, Cy = f.HoodRise, Rx = f.HoodWidth, Ry = f.HoodHeight,
                                Shade = 0.44f, Soft = 0.09f, Alpha = 1f });

            if (f.Mark == PortraitMark.Collar)
                list.Add(new Blob { Cx = 0f, Cy = f.ShoulderRise + 0.40f,
                                    Rx = f.ShoulderWidth * 0.72f, Ry = 0.09f,
                                    Shade = 0.82f, Soft = 0.10f, Alpha = 1f });

            list.Add(new Blob { Cx = 0f, Cy = f.HeadRise, Rx = f.HeadWidth, Ry = f.HeadHeight,
                                Shade = 1f, Soft = 0.07f, Alpha = 1f });

            if (f.Mark == PortraitMark.Brow)
                list.Add(new Blob { Cx = 0f, Cy = f.EyeRise + f.EyeSize * 2.4f,
                                    Rx = f.HeadWidth * 0.62f, Ry = 0.030f,
                                    Shade = 0.52f, Soft = 0.20f, Alpha = 1f });

            if (f.Mark == PortraitMark.Scarf)
                list.Add(new Blob { Cx = 0f, Cy = f.HeadRise - f.HeadHeight * 0.62f,
                                    Rx = f.HeadWidth * 1.06f, Ry = f.HeadHeight * 0.30f,
                                    Shade = 0.52f, Soft = 0.16f, Alpha = 1f });

            if (f.Mark == PortraitMark.Temples)
                for (int side = -1; side <= 1; side += 2)
                    list.Add(new Blob { Cx = side * f.HeadWidth * 0.76f, Cy = f.EyeRise,
                                        Rx = 0.030f, Ry = f.HeadHeight * 0.26f,
                                        Shade = 0.55f, Soft = 0.28f, Alpha = 1f });

            for (int side = -1; side <= 1; side += 2)
                list.Add(new Blob { Cx = side * f.EyeSpacing, Cy = f.EyeRise,
                                    Rx = f.EyeSize, Ry = f.EyeSize * 1.36f,
                                    Soft = 0.35f, Alpha = 1f, Eyes = true });
            for (int side = -1; side <= 1; side += 2)
                list.Add(new Blob { Cx = side * f.EyeSpacing + 0.02f, Cy = f.EyeRise + 0.04f,
                                    Rx = 0.022f, Ry = 0.026f,
                                    Soft = 0.6f, Alpha = 0.85f, Glint = true });

            return list.ToArray();
        }

        /// <summary>
        /// How far apart two faces are, in the units the shapes are drawn in. Proportions count
        /// once; a different mark counts for a lot, because it is the one difference that
        /// survives being small, dim, or seen by someone who cannot separate the two tints.
        /// </summary>
        public static float Distance(PortraitForm a, PortraitForm b)
        {
            float d = System.Math.Abs(a.HoodWidth - b.HoodWidth)
                    + System.Math.Abs(a.HoodHeight - b.HoodHeight)
                    + System.Math.Abs(a.HoodRise - b.HoodRise)
                    + System.Math.Abs(a.HeadWidth - b.HeadWidth)
                    + System.Math.Abs(a.HeadHeight - b.HeadHeight)
                    + System.Math.Abs(a.HeadRise - b.HeadRise)
                    + System.Math.Abs(a.ShoulderWidth - b.ShoulderWidth)
                    + System.Math.Abs(a.ShoulderRise - b.ShoulderRise)
                    + System.Math.Abs(a.EyeSpacing - b.EyeSpacing) * 2f
                    + System.Math.Abs(a.EyeSize - b.EyeSize) * 2f
                    + System.Math.Abs(a.EyeRise - b.EyeRise) * 2f;
            if (a.Mark != b.Mark) d += 0.30f;
            return d;
        }

        /// <summary>
        /// Below this two people read as the same person in two colours, which is the state this
        /// whole file exists to leave behind.
        /// </summary>
        public const float MinDistance = 0.12f;
    }
}
