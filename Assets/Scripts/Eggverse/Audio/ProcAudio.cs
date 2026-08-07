using System.Collections.Generic;
using UnityEngine;

namespace Eggverse
{
    public enum Sfx
    {
        UiMove, UiConfirm, UiBack,
        Hit, HitStrong, HitWeak, Crit,
        Faint, LevelUp, Evolve, Heal,
        CartonThrow, CartonWobble, CatchSuccess, CatchFail,
        Encounter, Land, Liftoff, Talk, Chart, Save, Inscription, Rustle, RustleDeep,
        Debuff
    }

    public enum MusicTrack { None, Explore, Belt, Battle, Amaranth }

    /// <summary>
    /// Every sound in the game is synthesised here at runtime, so the project needs no audio assets.
    /// Clips are built once on first use and cached.
    /// </summary>
    public static class ProcAudio
    {
        const int SampleRate = 44100;
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        // ------------------------------------------------------------------
        // primitives
        // ------------------------------------------------------------------

        static uint rngState = 0x9E3779B9;
        static float NextNoise()
        {
            rngState ^= rngState << 13;
            rngState ^= rngState >> 17;
            rngState ^= rngState << 5;
            return (rngState & 0xFFFFFF) / (float)0x7FFFFF - 1f;
        }

        static AudioClip Make(string key, float[] samples)
        {
            // Guard against clipping and hand the buffer to Unity.
            float peak = 0f;
            for (int i = 0; i < samples.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(samples[i]));
            if (peak > 1f)
            {
                float scale = 0.98f / peak;
                for (int i = 0; i < samples.Length; i++) samples[i] *= scale;
            }

            var clip = AudioClip.Create("eggverse_" + key, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            cache[key] = clip;
            return clip;
        }

        static float[] Buffer(float seconds) => new float[Mathf.Max(1, Mathf.RoundToInt(seconds * SampleRate))];

        /// <summary>Percussive envelope: quick attack, exponential tail.</summary>
        static float Env(float t, float duration, float attack = 0.006f, float power = 3.2f)
        {
            if (t < 0f || t > duration) return 0f;
            if (t < attack) return t / attack;
            float k = (t - attack) / Mathf.Max(0.0001f, duration - attack);
            return Mathf.Pow(1f - k, power);
        }

        static float Sine(float phase) => Mathf.Sin(phase * Mathf.PI * 2f);
        static float Tri(float phase) { float x = phase - Mathf.Floor(phase); return 4f * Mathf.Abs(x - 0.5f) - 1f; }
        static float Square(float phase) { float x = phase - Mathf.Floor(phase); return x < 0.5f ? 0.6f : -0.6f; }

        /// <summary>A single enveloped note. Shape: 0 sine, 1 triangle, 2 soft square.</summary>
        static void AddNote(float[] buf, float startSeconds, float duration, float freqStart, float freqEnd,
                            float gain, int shape = 0, float power = 3.2f)
        {
            int start = Mathf.RoundToInt(startSeconds * SampleRate);
            int count = Mathf.RoundToInt(duration * SampleRate);
            float phase = 0f;
            for (int i = 0; i < count; i++)
            {
                int index = start + i;
                if (index < 0 || index >= buf.Length) continue;
                float t = i / (float)SampleRate;
                float freq = Mathf.Lerp(freqStart, freqEnd, count <= 1 ? 0f : i / (float)(count - 1));
                phase += freq / SampleRate;
                float wave = shape == 1 ? Tri(phase) : shape == 2 ? Square(phase) : Sine(phase);
                buf[index] += wave * Env(t, duration, 0.006f, power) * gain;
            }
        }

        static void AddNoise(float[] buf, float startSeconds, float duration, float gain,
                             float smoothing = 0.25f, float power = 3.2f)
        {
            int start = Mathf.RoundToInt(startSeconds * SampleRate);
            int count = Mathf.RoundToInt(duration * SampleRate);
            float last = 0f;
            for (int i = 0; i < count; i++)
            {
                int index = start + i;
                if (index < 0 || index >= buf.Length) continue;
                float t = i / (float)SampleRate;
                // A one-pole lowpass turns white noise into something less harsh.
                last = Mathf.Lerp(NextNoise(), last, smoothing);
                buf[index] += last * Env(t, duration, 0.004f, power) * gain;
            }
        }

        // ------------------------------------------------------------------
        // sound effects
        // ------------------------------------------------------------------

        public static AudioClip Get(Sfx id)
        {
            string key = "sfx_" + id;
            AudioClip cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;
            return Make(key, BuildSfx(id));
        }

        /// <summary>
        /// One species' cry. Cached like everything else, and built from CryForm - which has all
        /// of the shape in it, so the checks and the synth cannot drift.
        /// </summary>
        public static AudioClip Cry(SpeciesDef sp)
        {
            string key = "cry_" + sp.Id;
            AudioClip cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;
            return Make(key, BuildCry(sp));
        }

        /// <summary>Synthesises one cry's samples. Pure maths, so it is testable offline.</summary>
        static float[] BuildCry(SpeciesDef sp)
        {
            var notes = CryForm.Notes(sp);
            var buf = Buffer(CryForm.Seconds(sp) + 0.05f);
            foreach (var n in notes)
                AddNote(buf, n.At, n.Length, n.From, n.To, n.Gain, n.Shape, n.Power);
            return buf;
        }

        /// <summary>Synthesises one effect's samples. Pure maths, so it is testable offline.</summary>
        static float[] BuildSfx(Sfx id)
        {
            float[] buf;
            switch (id)
            {
                case Sfx.UiMove:
                    buf = Buffer(0.06f);
                    AddNote(buf, 0f, 0.05f, 720f, 720f, 0.22f, 2, 4f);
                    break;

                case Sfx.UiConfirm:
                    buf = Buffer(0.14f);
                    AddNote(buf, 0f, 0.05f, 660f, 660f, 0.24f, 1, 3f);
                    AddNote(buf, 0.045f, 0.08f, 990f, 990f, 0.24f, 1, 3f);
                    break;

                case Sfx.UiBack:
                    buf = Buffer(0.14f);
                    AddNote(buf, 0f, 0.05f, 520f, 520f, 0.22f, 1, 3f);
                    AddNote(buf, 0.045f, 0.08f, 350f, 350f, 0.22f, 1, 3f);
                    break;

                case Sfx.Hit:
                    buf = Buffer(0.20f);
                    AddNoise(buf, 0f, 0.13f, 0.34f, 0.42f, 3.4f);
                    AddNote(buf, 0f, 0.16f, 190f, 90f, 0.42f, 0, 3f);
                    break;

                // Scaled down about a tenth from where it was. It peaked at 0.945, and effects
                // are checked one at a time - but they are heard over music. With effects at
                // full and music at its own maximum, a strong hit summed to 1.106 and clipped,
                // on exactly the moments the sound exists to sell.
                case Sfx.HitStrong:
                    buf = Buffer(0.34f);
                    AddNoise(buf, 0f, 0.22f, 0.38f, 0.22f, 2.6f);
                    AddNote(buf, 0f, 0.28f, 260f, 70f, 0.42f, 0, 2.4f);
                    AddNote(buf, 0.02f, 0.20f, 130f, 55f, 0.28f, 2, 2.6f);
                    break;

                case Sfx.HitWeak:
                    buf = Buffer(0.16f);
                    AddNoise(buf, 0f, 0.09f, 0.16f, 0.66f, 4f);
                    AddNote(buf, 0f, 0.12f, 150f, 96f, 0.22f, 0, 3.6f);
                    break;

                case Sfx.Crit:
                    buf = Buffer(0.30f);
                    AddNoise(buf, 0f, 0.16f, 0.34f, 0.12f, 3f);
                    AddNote(buf, 0f, 0.10f, 1400f, 900f, 0.28f, 2, 3f);
                    AddNote(buf, 0.05f, 0.22f, 300f, 80f, 0.42f, 0, 2.6f);
                    break;

                case Sfx.Faint:
                    buf = Buffer(0.62f);
                    AddNote(buf, 0f, 0.58f, 440f, 105f, 0.34f, 1, 1.7f);
                    AddNote(buf, 0.02f, 0.52f, 220f, 60f, 0.20f, 0, 1.7f);
                    break;

                case Sfx.LevelUp:
                    // Up and open. This and CatchSuccess were both a C major triangle arpeggio
                    // from C5 resolving onto C6 - close enough that nothing told them apart, and
                    // they play back to back: you catch an egg and then it levels. A player
                    // could not hear which had happened.
                    //
                    // So this one goes to G and stays high, with nothing under it, and ends on a
                    // fifth left hanging rather than a resolved octave. Levelling up is not an
                    // arrival; there is always another one.
                    buf = Buffer(0.72f);
                    AddArpeggio(buf, new[] { 783.99f, 987.77f, 1174.66f, 1567.98f }, 0.075f, 0.20f, 1);
                    AddNote(buf, 0.30f, 0.40f, 1567.98f, 1567.98f, 0.14f, 0, 2.2f);
                    AddNote(buf, 0.30f, 0.40f, 2349.32f, 2349.32f, 0.06f, 0, 2.4f);
                    break;

                case Sfx.Evolve:
                    // A long shimmer that resolves upward, over a swelling pad.
                    buf = Buffer(1.9f);
                    for (int i = 0; i < 10; i++)
                        AddNote(buf, i * 0.11f, 0.34f, 440f * Mathf.Pow(1.09f, i), 440f * Mathf.Pow(1.09f, i + 1), 0.13f, 0, 2.4f);
                    AddNote(buf, 0f, 1.30f, 130.81f, 261.63f, 0.16f, 0, 1.1f);
                    AddArpeggio(buf, new[] { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f, 1567.98f }, 0.10f, 0.20f, 1);
                    AddNote(buf, 1.20f, 0.66f, 1046.5f, 1046.5f, 0.18f, 0, 1.6f);
                    AddNote(buf, 1.20f, 0.66f, 1567.98f, 1567.98f, 0.11f, 0, 1.6f);
                    break;

                case Sfx.Heal:
                    buf = Buffer(0.85f);
                    AddNote(buf, 0f, 0.80f, 523.25f, 523.25f, 0.14f, 0, 1.4f);
                    AddNote(buf, 0.06f, 0.74f, 659.25f, 659.25f, 0.12f, 0, 1.4f);
                    AddNote(buf, 0.12f, 0.68f, 783.99f, 783.99f, 0.11f, 0, 1.4f);
                    break;

                case Sfx.CartonThrow:
                    buf = Buffer(0.30f);
                    AddNoise(buf, 0f, 0.26f, 0.20f, 0.55f, 1.6f);
                    AddNote(buf, 0f, 0.24f, 260f, 620f, 0.16f, 1, 2f);
                    break;

                case Sfx.CartonWobble:
                    buf = Buffer(0.18f);
                    AddNote(buf, 0f, 0.07f, 330f, 300f, 0.26f, 2, 3.4f);
                    AddNote(buf, 0.08f, 0.07f, 300f, 275f, 0.20f, 2, 3.4f);
                    break;

                case Sfx.CatchSuccess:
                    // Down and closed - the opposite motion to LevelUp, which is what tells them
                    // apart when they land within a second of each other. The carton latches
                    // first, low and short, and then the whole triad settles together and holds.
                    // Catching something is an arrival.
                    buf = Buffer(1.0f);
                    AddNote(buf, 0f, 0.09f, 150f, 96f, 0.22f, 0, 3.4f);
                    AddNoise(buf, 0f, 0.05f, 0.09f, 0.42f, 4f);
                    AddArpeggio(buf, new[] { 523.25f, 659.25f, 783.99f }, 0.12f, 0.20f, 1);
                    AddNote(buf, 0.42f, 0.54f, 261.63f, 261.63f, 0.16f, 0, 1.5f);
                    AddNote(buf, 0.42f, 0.54f, 329.63f, 329.63f, 0.12f, 0, 1.5f);
                    AddNote(buf, 0.42f, 0.54f, 392.00f, 392.00f, 0.11f, 0, 1.5f);
                    break;

                // A condition landing: two notes sagging apart, so it reads as something
                // settling in rather than a hit that is over.
                case Sfx.Debuff:
                    buf = Buffer(0.55f);
                    AddNote(buf, 0f,    0.50f, 392.00f, 311.13f, 0.17f, 2, 1.8f);
                    AddNote(buf, 0.05f, 0.46f, 261.63f, 207.65f, 0.14f, 2, 1.8f);
                    AddNoise(buf, 0f, 0.10f, 0.05f, 0.30f, 5f);
                    break;

                case Sfx.CatchFail:
                    buf = Buffer(0.40f);
                    AddNote(buf, 0f, 0.36f, 420f, 150f, 0.26f, 2, 2.2f);
                    AddNoise(buf, 0f, 0.16f, 0.14f, 0.5f, 3f);
                    break;

                case Sfx.Encounter:
                    buf = Buffer(0.60f);
                    AddNote(buf, 0f, 0.16f, 880f, 660f, 0.30f, 2, 3f);
                    AddNote(buf, 0.16f, 0.16f, 660f, 494f, 0.30f, 2, 3f);
                    AddNote(buf, 0.32f, 0.28f, 494f, 330f, 0.34f, 2, 2.4f);
                    break;

                case Sfx.Land:
                    buf = Buffer(0.70f);
                    AddNoise(buf, 0f, 0.66f, 0.26f, 0.72f, 1.5f);
                    AddNote(buf, 0f, 0.60f, 220f, 70f, 0.24f, 0, 1.8f);
                    break;

                case Sfx.Liftoff:
                    buf = Buffer(0.80f);
                    AddNoise(buf, 0f, 0.76f, 0.26f, 0.68f, 1.1f);
                    AddNote(buf, 0f, 0.70f, 90f, 300f, 0.24f, 0, 1.2f);
                    break;

                case Sfx.Talk:
                    buf = Buffer(0.045f);
                    AddNote(buf, 0f, 0.035f, 420f, 400f, 0.10f, 1, 4f);
                    break;

                case Sfx.Rustle:
                    // Shell grit shifting, not a creature. Short, quiet and high, so it sits
                    // clear of everything else on the surface and never competes with the
                    // encounter sting that may be two steps behind it.
                    buf = Buffer(0.22f);
                    // Smoothing 0.08 keeps it bright - shell grit rather than a rumble - and
                    // the decay power is high so it is gone almost before you register it.
                    AddNoise(buf, 0f, 0.20f, 0.075f, 0.08f, 4.2f);
                    AddNote(buf, 0.01f, 0.06f, 1760f, 1520f, 0.025f, 1, 6f);
                    break;

                case Sfx.RustleDeep:
                    // The same grit, lower and longer, with a note under it. Something with
                    // weight shifting in the shell field rather than something small scuffling.
                    buf = Buffer(0.42f);
                    AddNoise(buf, 0f, 0.40f, 0.10f, 0.34f, 2.6f);
                    AddNote(buf, 0.02f, 0.30f, 128f, 112f, 0.05f, 0, 2.2f);
                    break;

                case Sfx.Inscription:
                    // Stone, not machinery. A low struck note with a long tail and a fifth
                    // above it - the only sound in the game that is older than the player.
                    buf = Buffer(1.35f);
                    AddNote(buf, 0f, 1.30f, 146.8f, 145.4f, 0.16f, 0, 1.1f);
                    AddNote(buf, 0.02f, 1.10f, 220.0f, 219.0f, 0.09f, 0, 1.0f);
                    AddNote(buf, 0.00f, 0.16f, 587.3f, 520.0f, 0.05f, 1, 5f);
                    break;

                case Sfx.Chart:
                    buf = Buffer(0.30f);
                    AddNote(buf, 0f, 0.08f, 880f, 880f, 0.18f, 0, 3f);
                    AddNote(buf, 0.07f, 0.22f, 1318.5f, 1318.5f, 0.16f, 0, 2.4f);
                    break;

                case Sfx.Save:
                    buf = Buffer(0.36f);
                    AddNote(buf, 0f, 0.10f, 783.99f, 783.99f, 0.16f, 0, 3f);
                    AddNote(buf, 0.09f, 0.26f, 1046.5f, 1046.5f, 0.14f, 0, 2.4f);
                    break;

                default:
                    buf = Buffer(0.05f);
                    break;
            }

            return buf;
        }

        static void AddArpeggio(float[] buf, float[] freqs, float noteDuration, float gain, int shape)
        {
            for (int i = 0; i < freqs.Length; i++)
                AddNote(buf, i * noteDuration, noteDuration * 2.2f, freqs[i], freqs[i], gain, shape, 2.6f);
        }

        // ------------------------------------------------------------------
        // music
        // ------------------------------------------------------------------

        /// <summary>Snaps a frequency so a whole number of cycles fits the loop, keeping the seam silent.</summary>
        static float LoopSafe(float freq, float duration) => Mathf.Round(freq * duration) / duration;

        public static AudioClip Music(MusicTrack track)
        {
            if (track == MusicTrack.None) return null;

            string key = "mus_" + track;
            AudioClip cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;

            switch (track)
            {
                case MusicTrack.Battle: return Make(key, BuildBattle());
                case MusicTrack.Belt: return Make(key, BuildBelt());
                case MusicTrack.Amaranth: return Make(key, BuildAmaranth());
                default: return Make(key, BuildExplore());
            }
        }

        /// <summary>Sustained pad built from a few detuned partials, with a slow breathing LFO.</summary>
        static void AddPad(float[] buf, float duration, float freq, float gain, float lfoHz, float lfoDepth)
        {
            freq = LoopSafe(freq, duration);
            float detune = LoopSafe(freq * 1.005f, duration);
            float lfo = LoopSafe(lfoHz, duration);
            int count = buf.Length;
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)SampleRate;
                float breathe = 1f - lfoDepth + lfoDepth * (0.5f + 0.5f * Sine(lfo * t));
                buf[i] += (Sine(freq * t) * 0.6f + Sine(detune * t) * 0.4f + Sine(freq * 2f * t) * 0.12f)
                          * gain * breathe;
            }
        }

        static float[] BuildExplore()
        {
            const float duration = 16f;
            var buf = Buffer(duration);

            // A minor drone with a fifth above it.
            AddPad(buf, duration, 110f, 0.075f, 0.0625f, 0.5f);
            AddPad(buf, duration, 164.81f, 0.050f, 0.09f, 0.6f);
            AddPad(buf, duration, 220f, 0.030f, 0.125f, 0.7f);

            // Sparse pentatonic motes drifting over the top.
            float[] scale = { 440f, 523.25f, 587.33f, 659.25f, 783.99f, 880f };
            int[] pattern = { 0, 2, 4, 3, 5, 4, 2, 1, 0, 3, 4, 2 };
            for (int i = 0; i < pattern.Length; i++)
            {
                float at = 0.6f + i * 1.25f;
                if (at > duration - 1.2f) break;
                AddNote(buf, at, 1.1f, scale[pattern[i]], scale[pattern[i]], 0.055f, 0, 2.6f);
            }
            return buf;
        }

        static float[] BuildBattle()
        {
            const float duration = 8f;
            var buf = Buffer(duration);

            AddPad(buf, duration, 98f, 0.070f, 0.25f, 0.35f);
            AddPad(buf, duration, 146.83f, 0.040f, 0.5f, 0.4f);

            // Driving eighth-note bass.
            for (int i = 0; i < 32; i++)
            {
                float at = i * 0.25f;
                float f = (i % 8 == 6) ? 110f : (i % 4 == 2 ? 87.31f : 98f);
                AddNote(buf, at, 0.22f, f, f * 0.94f, 0.20f, 2, 3.4f);
            }

            // Urgent riff on top.
            float[] riff = { 587.33f, 698.46f, 587.33f, 493.88f, 587.33f, 783.99f, 698.46f, 587.33f };
            for (int i = 0; i < 16; i++)
            {
                float at = 0.5f * i;
                AddNote(buf, at, 0.34f, riff[i % riff.Length], riff[i % riff.Length], 0.075f, 1, 3f);
            }
            return buf;
        }

        /// <summary>
        /// The Shattered Belt. Pim says every rock out here hums the same note, because it all
        /// came off the same shell — so this is one sustained pitch with its own overtones
        /// drifting in and out, and almost nothing else. Cold and very sparse on purpose.
        /// </summary>
        static float[] BuildBelt()
        {
            const float duration = 20f;
            var buf = Buffer(duration);

            // The note itself, and the octaves and fifths that live inside it.
            AddPad(buf, duration, 110f, 0.085f, 0.05f, 0.30f);
            AddPad(buf, duration, 220f, 0.038f, 0.0375f, 0.65f);
            AddPad(buf, duration, 329.63f, 0.022f, 0.075f, 0.80f);
            AddPad(buf, duration, 440f, 0.012f, 0.1f, 0.90f);

            // A single low pulse every five seconds, like something very large settling.
            for (int i = 0; i < 4; i++)
                AddNote(buf, i * 5f, 2.4f, 55f, 52f, 0.10f, 0, 1.3f);

            // Occasional high harmonics that fade in from nowhere.
            float[] shards = { 659.25f, 880f, 987.77f, 783.99f, 1318.5f };
            for (int i = 0; i < shards.Length; i++)
            {
                float at = 1.8f + i * 3.7f;
                if (at > duration - 2.6f) break;
                AddNote(buf, at, 2.4f, shards[i], shards[i], 0.026f, 0, 1.6f);
            }
            return buf;
        }

        static float[] BuildAmaranth()
        {
            const float duration = 16f;
            var buf = Buffer(duration);

            // One long, slightly wrong chord — the note the Belt has been humming.
            AddPad(buf, duration, 73.42f, 0.085f, 0.0625f, 0.35f);
            AddPad(buf, duration, 110f, 0.055f, 0.05f, 0.45f);
            AddPad(buf, duration, 138.59f, 0.040f, 0.075f, 0.55f);
            AddPad(buf, duration, 329.63f, 0.022f, 0.125f, 0.8f);

            float[] scale = { 329.63f, 392f, 440f, 493.88f, 587.33f };
            int[] pattern = { 0, 1, 3, 2, 4, 3, 1, 0 };
            for (int i = 0; i < pattern.Length; i++)
            {
                float at = 1.2f + i * 1.85f;
                if (at > duration - 1.6f) break;
                AddNote(buf, at, 1.6f, scale[pattern[i]], scale[pattern[i]], 0.048f, 0, 2.2f);
            }
            return buf;
        }
    }
}
