using System.Collections.Generic;
using UnityEngine;

namespace Eggverse
{
    /// <summary>
    /// What one species sounds like, as numbers.
    ///
    /// Twenty-eight species that look distinct — their own shell colour, their own pattern, their
    /// own silhouette — and every single one of them arrived on the same three-note sting. The
    /// encounter sound said "something is here" and nothing about what.
    ///
    /// So a cry, built the way the portraits are: the element decides the shape it makes, and the
    /// creature's own numbers decide where it sits. A Stone egg is low and slow whatever else it
    /// is; a Volt egg chatters. Within an element, a heavier egg is lower and a faster one is
    /// quicker, so Cobblet and Craggle are recognisably the same kind of thing and not the same
    /// thing.
    ///
    /// Plain floats, no engine types: ProcAudio builds the samples and the checks build the
    /// fingerprints, and only one of those can run inside Unity.
    /// </summary>
    public static class CryForm
    {
        /// <summary>One note of a cry. Shape: 0 sine, 1 triangle, 2 soft square.</summary>
        public struct Note
        {
            public float At, Length, From, To, Gain, Power;
            public int Shape;
        }

        /// <summary>
        /// How an element sounds, before the individual is taken into account.
        ///
        /// `Steps` are semitone offsets from the creature's own root, one per note, and their
        /// signs are the character: Molten falls away, Aether climbs and hangs, Volt chatters on
        /// one note, Tidal swings up and back down.
        /// </summary>
        public struct Voice
        {
            public int Shape;
            public float[] Steps;
            public float Beat;      // seconds per note before speed adjusts it
            public float Tail;      // how long the last note rings, as a multiple of Beat
            public float Power;     // decay sharpness
        }

        public static Voice VoiceOf(EggType t)
        {
            switch (t)
            {
                // Plain is a move type rather than a creature one - it is what the desperation
                // move Flail is written in, and nothing hatches with it. This case exists so the
                // switch is total, and the check says out loud that nobody ever reaches it.
                case EggType.Plain:   return new Voice { Shape = 0, Steps = new[] { 0f, 0f }, Beat = 0.115f, Tail = 2.4f, Power = 3.0f };
                // Falls away, like something going out.
                case EggType.Molten:  return new Voice { Shape = 2, Steps = new[] { 0f, -3f, -7f }, Beat = 0.095f, Tail = 2.6f, Power = 2.4f };
                // Up and back down: a swell.
                case EggType.Tidal:   return new Voice { Shape = 0, Steps = new[] { 0f, 5f, 0f }, Beat = 0.120f, Tail = 3.0f, Power = 2.0f };
                // Two notes a third apart, soft edged.
                case EggType.Verdant: return new Voice { Shape = 1, Steps = new[] { 0f, 4f }, Beat = 0.130f, Tail = 2.8f, Power = 2.6f };
                // Chatter. Four fast notes on almost one pitch.
                case EggType.Volt:    return new Voice { Shape = 2, Steps = new[] { 0f, 1f, 0f, 1f }, Beat = 0.055f, Tail = 1.8f, Power = 4.2f };
                // High, thin, and it hangs.
                case EggType.Frost:   return new Voice { Shape = 0, Steps = new[] { 0f, 7f }, Beat = 0.135f, Tail = 4.2f, Power = 1.5f };
                // One note, low, and that is all it has to say.
                case EggType.Stone:   return new Voice { Shape = 1, Steps = new[] { 0f }, Beat = 0.240f, Tail = 2.2f, Power = 2.2f };
                // Climbs and stays up there.
                case EggType.Aether:  return new Voice { Shape = 0, Steps = new[] { 0f, 4f, 9f }, Beat = 0.110f, Tail = 3.6f, Power = 1.8f };
                // Down, and further down.
                default:              return new Voice { Shape = 2, Steps = new[] { 0f, -5f, -12f }, Beat = 0.130f, Tail = 2.8f, Power = 2.2f };
            }
        }

        /// <summary>The lowest and highest a cry may sit, so nothing lands under the music or in a whistle.</summary>
        public const float LowestHz = 165f, HighestHz = 1320f;

        /// <summary>
        /// Where this creature's voice sits. Bulk pulls it down, speed pushes it up — the two
        /// stats a player can see on the record page, so the sound agrees with the numbers rather
        /// than being sprinkled on top of them.
        /// </summary>
        public static float RootHz(SpeciesDef sp)
        {
            float bulk = (sp.BaseHP + sp.BaseDef) * 0.5f;      // roughly 30..90
            float quick = sp.BaseSpd;                          // roughly 20..110

            // Semitones away from a middle A, up for quick and down for heavy.
            float steps = (quick - 60f) * 0.10f - (bulk - 55f) * 0.14f;

            // Two creatures with the same numbers are still two different creatures. Craggle and
            // Obsidyolk came out 0.061 apart against a floor of 0.05 - inside their family, but
            // barely told apart at all - because their bulk and speed are nearly identical and
            // nothing else was distinguishing them. A semitone either way, stable per species,
            // and far too small to disturb "heavier is lower" across the roster.
            steps += (Hash(sp.Id) % 5 - 2) * 0.5f;

            float hz = 440f * Mathf.Pow(2f, steps / 12f);
            return Mathf.Clamp(hz, LowestHz, HighestHz);
        }

        /// <summary>
        /// The same stable hash the portraits use. Not string.GetHashCode, which is allowed to
        /// differ between runs - a creature that changed voice on Tuesday would be worse than
        /// one that never had a voice.
        /// </summary>
        static int Hash(string key)
        {
            unchecked
            {
                int h = unchecked((int)2166136261u);
                for (int i = 0; i < (key ?? "").Length; i++) { h ^= key[i]; h *= 16777619; }
                return h & 0x7FFFFFFF;
            }
        }

        /// <summary>The whole cry, note by note, in the order it is played.</summary>
        public static List<Note> Notes(SpeciesDef sp)
        {
            var voice = VoiceOf(sp.Type);
            float root = RootHz(sp);

            // A quick creature says it faster. Bounded, or a Volt egg with high speed becomes a
            // click and a Stone egg becomes a drone.
            float beat = voice.Beat * Mathf.Clamp(1f - (sp.BaseSpd - 60f) * 0.0035f, 0.72f, 1.30f);

            var outp = new List<Note>();
            for (int i = 0; i < voice.Steps.Length; i++)
            {
                bool last = i == voice.Steps.Length - 1;
                float hz = root * Mathf.Pow(2f, voice.Steps[i] / 12f);

                // Every note slides a little toward the next one, which is what stops three
                // separate beeps sounding like three separate beeps.
                float next = last ? hz * 0.94f
                                  : root * Mathf.Pow(2f, voice.Steps[i + 1] / 12f);
                float to = Mathf.Lerp(hz, next, last ? 1f : 0.18f);

                outp.Add(new Note
                {
                    At = i * beat,
                    Length = beat * (last ? voice.Tail : 1.7f),
                    From = hz,
                    To = to,
                    Gain = last ? 0.24f : 0.20f,
                    Shape = voice.Shape,
                    Power = voice.Power,
                });

                // A quiet partial over the top, at one of three intervals. This is where a
                // creature's own colour lives.
                //
                // Stone is a single low note, so two Stone eggs had almost nothing separating
                // them - Craggle and Obsidyolk sit 0.9 semitones apart on bulk and speed alone,
                // and a semitone of pitch jitter moved them together as readily as apart. Pitch
                // was the only dimension in play. An overtone changes the timbre, which is a
                // different axis entirely, and it is what makes two rocks sound like two rocks.
                float over = Overtone(sp);
                outp.Add(new Note
                {
                    At = i * beat,
                    Length = beat * (last ? voice.Tail * 0.7f : 1.4f),
                    From = hz * over,
                    To = to * over,
                    Gain = (last ? 0.24f : 0.20f) * 0.34f,
                    Shape = 0,
                    Power = voice.Power + 0.8f,
                });
            }
            return outp;
        }

        /// <summary>
        /// The interval of a creature's overtone: an octave, an octave and a fourth, or an
        /// octave and a fifth. Three timbral families crossed with the nine elements, which is
        /// what gives two creatures of one element somewhere to differ other than pitch.
        /// </summary>
        public static float Overtone(SpeciesDef sp)
        {
            switch (Hash(sp.Id) % 3)
            {
                case 0:  return 2.0f;                                  // an octave
                case 1:  return Mathf.Pow(2f, 17f / 12f);              // and a fourth
                default: return Mathf.Pow(2f, 19f / 12f);              // and a fifth
            }
        }

        /// <summary>How long the whole thing takes, so callers can wait for it if they need to.</summary>
        public static float Seconds(SpeciesDef sp)
        {
            float end = 0f;
            foreach (var n in Notes(sp)) end = Mathf.Max(end, n.At + n.Length);
            return end;
        }
    }
}
