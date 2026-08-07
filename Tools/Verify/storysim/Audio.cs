using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Eggverse;

// ProcAudio's synthesis is pure Mathf; only AudioClip.Create touches the engine and it lives
// in Make(). So the loop builders can be invoked directly and analysed here.
static class AudioCheck
{
    // The loudest music loop, kept so the mix check can hold effects against it.
    static float musicPeak;
    static float MusicPeak() => musicPeak;
    const int SR = 44100;

    public static void Run(string outDir, Action<bool,string> check)
    {
        System.IO.Directory.CreateDirectory(outDir);
        var t = typeof(ProcAudio);
        string[] builders = { "BuildExplore", "BuildBelt", "BuildBattle", "BuildAmaranth" };

        foreach (var name in builders)
        {
            var m = t.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
            check(m != null, $"{name} exists");
            if (m == null) continue;

            var buf = (float[])m.Invoke(null, null);
            check(buf != null && buf.Length > 0, $"{name} produced samples");
            if (buf == null || buf.Length == 0) continue;

            float peak = 0f, sum = 0f, dc = 0f;
            foreach (var s in buf) { peak = Math.Max(peak, Math.Abs(s)); sum += s * s; dc += s; }
            float rms = (float)Math.Sqrt(sum / buf.Length);
            dc /= buf.Length;

            // Seam: the jump from the final sample back to the first, compared with the
            // typical sample-to-sample step. A big ratio is an audible click every loop.
            float seam = Math.Abs(buf[buf.Length - 1] - buf[0]);
            float avgStep = 0f;
            for (int i = 1; i < buf.Length; i++) avgStep += Math.Abs(buf[i] - buf[i - 1]);
            avgStep /= (buf.Length - 1);
            float ratio = avgStep > 1e-9f ? seam / avgStep : 0f;

            Console.WriteLine($"  {name,-14} {buf.Length / (float)SR,5:0.0}s  peak {peak:0.000}  rms {rms:0.0000}  dc {dc:+0.0000;-0.0000}  seam {seam:0.00000} ({ratio:0.0}x step)");

            check(peak > 0.02f, $"{name} is not silent");
            check(peak <= 1.0f, $"{name} does not clip");
            if (peak > musicPeak) musicPeak = peak;   // the loudest loop, for the mix check below
            check(Math.Abs(dc) < 0.02f, $"{name} has no DC offset");
            check(ratio < 12f, $"{name} loop seam is smooth (was {ratio:0.0}x the average step)");

            WriteWav(Path.Combine(outDir, name.Replace("Build", "").ToLower() + ".wav"), buf);
        }


        // Every sound effect must actually make a sound, and none may clip.
        var sfxMethod = t.GetMethod("BuildSfx", BindingFlags.NonPublic | BindingFlags.Static);
        check(sfxMethod != null, "BuildSfx is reachable");
        if (sfxMethod != null)
        {
            int silent = 0;
            float quietest = 1f; string quietestName = "";
            var levels = new List<(string name, float peak, float rms)>();
            var clips = new List<(string name, float[] buf)>();
            var prints = new List<(string name, float[] fp)>();
            foreach (Sfx id in Enum.GetValues(typeof(Sfx)))
            {
                var buf = (float[])sfxMethod.Invoke(null, new object[] { id });
                check(buf != null && buf.Length > 0, $"{id} produces samples");
                if (buf == null || buf.Length == 0) continue;

                float peak = 0f, sum = 0f;
                foreach (var v in buf) { peak = Math.Max(peak, Math.Abs(v)); sum += v * v; }
                float rms = (float)Math.Sqrt(sum / buf.Length);

                check(peak > 0.02f, $"{id} is audible (peak {peak:0.000})");
                levels.Add((id.ToString(), peak, rms));
                check(peak <= 1.0f, $"{id} does not clip (peak {peak:0.000})");
                check(buf.Length < 44100 * 3, $"{id} is not absurdly long ({buf.Length / 44100f:0.0}s)");
                // A click at the very end is audible; effects should decay to near silence.
                float tail = Math.Abs(buf[buf.Length - 1]);
                check(tail < 0.12f, $"{id} decays before it ends (tail {tail:0.000})");
                if (peak <= 0.02f) silent++;
                if (peak < quietest) { quietest = peak; quietestName = id.ToString(); }

                clips.Add((id.ToString(), buf));
                prints.Add((id.ToString(), Fingerprint(buf)));
                WriteWav(Path.Combine(outDir, "sfx-" + id.ToString().ToLower() + ".wav"), buf);
            }

            // Every effect, end to end, in one file. The music loops have been written out since
            // they were written; the effects were measured and never once played.
            WritePalette(Path.Combine(outDir, "sfx-palette.wav"), clips);

            // No two effects may be the same sound. Twenty-five of them come from one switch
            // statement of tuned constants, and three families - Hit/HitStrong/HitWeak,
            // UiMove/UiConfirm/UiBack, Rustle/RustleDeep - are deliberately near neighbours. A
            // copy-paste that left two identical would have passed every check above: both are
            // audible, neither clips, both decay, and the pair sits comfortably inside the spread.
            {
                float closest = 999f; string ca = "", cb = "";
                for (int i = 0; i < prints.Count; i++)
                    for (int j = i + 1; j < prints.Count; j++)
                    {
                        float d = Apart(prints[i].fp, prints[j].fp);
                        if (d < closest) { closest = d; ca = prints[i].name; cb = prints[j].name; }
                    }
                Console.WriteLine($"  closest pair of effects: {ca} and {cb} at {closest:0.000}");
                check(closest > 0.08f,
                      $"no two effects are the same sound ({ca}/{cb} at {closest:0.000})");

                // The named pairs, printed. There is no upper bound here on purpose: the first
                // version asserted each pair stayed "in the same family" under an invented
                // ceiling of 1.10, and Rustle/RustleDeep came in at 1.64. The check was wrong,
                // not the sound - RustleDeep is documented as the same grit lower and longer,
                // and deliberately contrasted so a heavy thing in a shell field never reads as
                // a small one. Same for UiConfirm against UiBack, which is the point of them.
                // A threshold nobody measured before writing is not a check.
                var byName = new Dictionary<string, float[]>();
                foreach (var p in prints) byName[p.name] = p.fp;

                // Sounds a player hears within a second or two of each other have to be further
                // apart than sounds that never meet. A global floor cannot say that: it treats
                // Liftoff against Inscription - one heard in space, one on a surface, never
                // together - exactly like CatchSuccess against LevelUp, which land back to back
                // every time an egg is caught in a fight.
                //
                // That pair sat at 0.319 and was the closest in the set. Both were a C major
                // triangle arpeggio from C5 resolving onto C6; nothing said so, because nothing
                // had ever compared one sound to another. They are 1.9 apart now, and opposite
                // in motion: catching settles downward onto a held triad, levelling lifts and
                // leaves a fifth hanging.
                var heardTogether = new[]
                {
                    ("CatchSuccess", "LevelUp",      "an egg is caught, then it levels"),
                    ("CartonThrow",  "CartonWobble", "the carton leaves your hand and rocks"),
                    ("CartonWobble", "CatchSuccess", "the last wobble, then it holds"),
                    ("CartonWobble", "CatchFail",    "the last wobble, then it does not"),
                    ("Hit",          "Faint",        "the blow that finishes it"),
                    ("Crit",         "Faint",        "the same, harder"),
                    ("Hit",          "HitStrong",    "consecutive turns"),
                    ("Hit",          "HitWeak",      "consecutive turns"),
                    ("HitStrong",    "HitWeak",      "consecutive turns"),
                    ("Rustle",       "Encounter",    "grit in the field, then something in it"),
                    ("RustleDeep",   "Encounter",    "the same, heavier"),
                    ("Land",         "Rustle",       "touching down and walking off the pad"),
                    ("UiMove",       "UiConfirm",    "stepping down a menu and choosing"),
                    ("UiMove",       "UiBack",       "stepping down a menu and leaving"),
                    ("Faint",        "LevelUp",      "it goes down, you go up"),
                };

                float nearest = 999f; string na = "", nb = "";
                foreach (var pair in heardTogether)
                {
                    float d = Apart(byName[pair.Item1], byName[pair.Item2]);
                    if (d < nearest) { nearest = d; na = pair.Item1; nb = pair.Item2; }
                    check(d > 0.30f,
                          $"{pair.Item1} and {pair.Item2} are told apart where they meet - " +
                          $"{pair.Item3} ({d:0.000})");
                }
                Console.WriteLine($"  closest pair heard together: {na} and {nb} at {nearest:0.000}");
            }
            Console.WriteLine($"  {Enum.GetValues(typeof(Sfx)).Length} effects checked, none silent, quietest {quietestName} at {quietest:0.00}");

            // Every effect above was measured on its own. The one that matters is the one that
            // is never heard on its own: reading an inscription rings a 1.3s stone note and then
            // the dialogue box types over the whole of it, one blip per revealed character. A
            // sound that is clean alone and clips against its own typewriter is a defect you can
            // only hear, so mix them here at a cadence faster than any real reveal.
            {
                var stone = (float[])sfxMethod.Invoke(null, new object[] { Sfx.Inscription });
                var blip = (float[])sfxMethod.Invoke(null, new object[] { Sfx.Talk });
                var mix = (float[])stone.Clone();
                int step = (int)(SR * 0.03f);
                for (int at = 0; at < mix.Length; at += step)
                    for (int i = 0; i < blip.Length && at + i < mix.Length; i++)
                        mix[at + i] += blip[i];

                // Clipping is the wrong question here, and planting proved it: the note has to
                // clip on its own before the mix does, because the blips never land on its peak.
                // The real risk is masking - a sound that is technically playing and cannot be
                // picked out of the noise on top of it is a sound nobody hears.
                var blipsOnly = new float[mix.Length];
                for (int at = 0; at < blipsOnly.Length; at += step)
                    for (int i = 0; i < blip.Length && at + i < blipsOnly.Length; i++)
                        blipsOnly[at + i] += blip[i];

                float mixPeak = 0f, mixSum = 0f, blipSum = 0f;
                for (int i = 0; i < mix.Length; i++)
                {
                    mixPeak = Math.Max(mixPeak, Math.Abs(mix[i]));
                    mixSum += mix[i] * mix[i];
                    blipSum += blipsOnly[i] * blipsOnly[i];
                }
                float mixRms = (float)Math.Sqrt(mixSum / mix.Length);
                float blipRms = (float)Math.Sqrt(blipSum / blipsOnly.Length);
                float lift = mixRms / Math.Max(0.00001f, blipRms);

                float stoneSum = 0f;
                foreach (var v in stone) stoneSum += v * v;
                float stoneRms = (float)Math.Sqrt(stoneSum / stone.Length);

                Console.WriteLine($"  inscription under a full typewriter: peak {mixPeak:0.000}, " +
                                  $"note rms {stoneRms:0.0000} vs typewriter {blipRms:0.0000} " +
                                  $"({lift:0.00}x together)");
                check(mixPeak <= 1.0f, $"the inscription does not clip while the text types (peak {mixPeak:0.000})");

                // The falsifiable form: the note has to be louder than the blips laid over it.
                // A ratio threshold picked out of the air was so lenient that cutting the note
                // to a fifth of its amplitude still passed, which is not a check.
                check(stoneRms > blipRms,
                      $"the inscription is louder than the typewriter over it " +
                      $"({stoneRms:0.0000} vs {blipRms:0.0000})");

                // And it has to still be ringing when the line finishes, or it is just a click
                // at the start of a paragraph.
                check(stone.Length > SR * 0.8f,
                      $"the inscription outlasts the line it opens ({stone.Length / (float)SR:0.0}s)");
            }

            // ---- sounds stacked at the speed the game actually plays them ----
            //
            // Say() holds each battle line for a second, scaled by the text-speed setting - and
            // at "instant" that scale is zero, so the line appears and the coroutine moves on in
            // the same frame. Every sound in a sequence then fires on top of the one before it.
            //
            // The inscription-under-a-typewriter check further up was written for exactly this
            // and covers exactly one pair. These are the sequences the game actually produces,
            // mixed at the real gaps, at every text speed the pause menu offers.
            {
                var sfx = new Dictionary<Sfx, float[]>();
                foreach (Sfx id in Enum.GetValues(typeof(Sfx)))
                    sfx[id] = (float[])sfxMethod.Invoke(null, new object[] { id });

                // (sound, fixed seconds after it, Say-holds after it). The holds are what the
                // text-speed setting divides; the fixed seconds are animations, which it does not.
                var sequences = new (string what, (Sfx id, float fixedAfter, float holdAfter)[] steps)[]
                {
                    ("a carton lands", new[]
                    {
                        (Sfx.CartonThrow,  0f,   1.0f),   // "You lobbed an egg carton!"
                        (Sfx.CartonWobble, 0.4f, 0.35f),  // the wobble, then ". . ."
                        (Sfx.CartonWobble, 0.4f, 0.35f),
                        (Sfx.CartonWobble, 0.4f, 0.35f),
                        (Sfx.CatchSuccess, 0f,   1.0f),
                    }),
                    ("a carton fails", new[]
                    {
                        (Sfx.CartonThrow,  0f,   1.0f),
                        (Sfx.CartonWobble, 0.4f, 0.35f),
                        (Sfx.CatchFail,    0f,   1.0f),
                    }),
                    ("the foe goes down", new[]
                    {
                        (Sfx.Faint,   0f, 2.0f),   // "cracked and gave up", "gained N XP"
                        (Sfx.LevelUp, 0f, 1.0f),   // one line per level gained
                        (Sfx.Evolve,  0f, 1.0f),
                    }),
                    ("a critical finishes it", new[]
                    {
                        (Sfx.Crit,  0f, 1.0f),
                        (Sfx.Faint, 0f, 2.0f),
                        (Sfx.LevelUp, 0f, 1.0f),
                    }),
                    ("walking into something", new[]
                    {
                        (Sfx.Rustle,    0f, 0f),
                        (Sfx.Encounter, 0f, 0f),
                    }),
                };

                float[] speeds = { 0.6f, 1f, 1.8f, 0f };   // GameState.TextSpeedScale
                string[] speedNames = { "relaxed", "normal", "brisk", "instant" };

                float worst = 0f; string worstWhat = "", worstSpeed = "";
                for (int sp = 0; sp < speeds.Length; sp++)
                    foreach (var seq in sequences)
                    {
                        // Lay each sound down at the moment the game would start it.
                        float at = 0f;
                        var starts = new List<(int sample, Sfx id)>();
                        foreach (var step in seq.steps)
                        {
                            starts.Add(((int)(at * SR), step.id));
                            at += step.fixedAfter + (speeds[sp] <= 0f ? 0f : step.holdAfter / speeds[sp]);
                        }

                        int len = 0;
                        foreach (var st in starts) len = Math.Max(len, st.sample + sfx[st.id].Length);
                        var mix = new float[len];
                        foreach (var st in starts)
                        {
                            var b = sfx[st.id];
                            for (int i = 0; i < b.Length; i++) mix[st.sample + i] += b[i];
                        }

                        float peak = 0f;
                        foreach (var v in mix) peak = Math.Max(peak, Math.Abs(v));
                        if (peak > worst) { worst = peak; worstWhat = seq.what; worstSpeed = speedNames[sp]; }

                        check(peak <= 1.0f,
                              $"{seq.what} does not clip at \"{speedNames[sp]}\" text speed (peak {peak:0.000})");

                        if (sp == speeds.Length - 1)
                            WriteWav(Path.Combine(outDir, "seq-" +
                                     seq.what.Replace(' ', '-') + "-instant.wav"), mix);
                    }

                Console.WriteLine($"  worst stack: {worstWhat} at \"{worstSpeed}\" text speed, peak {worst:0.000}");

                // And the same headroom rule the single sounds are held to. A sequence that peaks
                // at 0.99 passes by a rounding error and the next sound anybody tunes puts it over.
                check(worst <= 0.97f,
                      $"the worst stack leaves headroom ({worst:0.000}: {worstWhat} at {worstSpeed})");
            }

            // Nothing was ever comparing one effect against another. A sound is not just
            // audible or clipping - it is loud or quiet *relative to the ones around it*, and a
            // single effect several times the loudness of its neighbours is the one that makes a
            // player reach for the volume.
            levels.Sort((a, b) => b.rms.CompareTo(a.rms));
            Console.WriteLine("  loudest and quietest by rms:");
            for (int i = 0; i < 3; i++)
                Console.WriteLine($"    {levels[i].name,-14} rms {levels[i].rms:0.0000}  peak {levels[i].peak:0.000}");
            Console.WriteLine("    ...");
            for (int i = levels.Count - 3; i < levels.Count; i++)
                Console.WriteLine($"    {levels[i].name,-14} rms {levels[i].rms:0.0000}  peak {levels[i].peak:0.000}");

            float loudRms = levels[0].rms, quietRms = levels[levels.Count - 1].rms;
            float spread = loudRms / Math.Max(0.00001f, quietRms);
            Console.WriteLine($"  rms spread across the set: {spread:0.0}x " +
                              $"({levels[0].name} over {levels[levels.Count - 1].name})");

            // A wide spread is normal - a faint UI tick should not match a crit - but past about
            // twenty to one the quiet end stops being heard at a volume where the loud end is
            // comfortable.
            // Effects are heard over music, and the per-effect checks only ever looked at one
            // sound alone. The pause menu lets effects reach 1.0 and music 0.6, so the loudest
            // of each have to sum under 1.0 or the mix clips at settings the game offers.
            float loudestSfx = 0f; string loudestName = "";
            foreach (var l in levels) if (l.peak > loudestSfx) { loudestSfx = l.peak; loudestName = l.name; }
            float loudestMusic = MusicPeak();
            float worstMix = loudestSfx * 1.0f + loudestMusic * 0.6f;
            Console.WriteLine($"  worst mix: {loudestName} at full over the loudest loop at 0.6 = {worstMix:0.000}");
            // 0.97, not 1.0. Landing on 0.999 is passing by a rounding error, and the next
            // sound anybody tweaks puts it over.
            check(worstMix <= 0.97f,
                  $"effects and music together leave headroom ({worstMix:0.000}: " +
                  $"{loudestName} {loudestSfx:0.000} + music {loudestMusic:0.000} x 0.6)");

            // Every music loop has to be long enough that the longest fade into it is a fade
            // rather than most of the track. Amaranth takes three seconds to arrive.
            foreach (var name in builders)
            {
                var mm = t.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
                if (mm == null) continue;
                var mbuf = (float[])mm.Invoke(null, null);
                float secs = mbuf.Length / (float)SR;
                check(secs >= 3.0f * 2f,
                      $"{name} is long enough for its fade to be a fade ({secs:0.0}s against a 3.0s crossfade)");
            }

            check(spread <= 20f,
                  $"the effects sit within a usable range of each other ({spread:0.0}x, " +
                  $"{levels[0].name} over {levels[levels.Count - 1].name})");
        }
    }

    /// <summary>
    /// What a sound is, as numbers: where its energy sits across twelve log-spaced bands, and
    /// how that energy is shaped over eight slices of its length.
    ///
    /// Both halves normalised, so this describes character rather than loudness - two effects at
    /// different volumes that are otherwise the same sound should still come out identical, which
    /// is the case worth catching.
    /// </summary>
    static float[] Fingerprint(float[] buf)
    {
        var bands = new float[12];
        // 80Hz to 12.8kHz, an octave and a bit per band. Goertzel per band centre: no FFT needed
        // for twelve numbers, and an exact answer beats a windowed approximation here.
        for (int b = 0; b < bands.Length; b++)
        {
            float freq = 80f * (float)Math.Pow(2.0, b * 7.32 / 11.0);
            double w = 2.0 * Math.PI * freq / SR;
            double coeff = 2.0 * Math.Cos(w);
            double s1 = 0, s2 = 0;
            // The first half second is where an effect's character lives; the tail is decay.
            int n = Math.Min(buf.Length, SR / 2);
            for (int i = 0; i < n; i++)
            {
                double s0 = buf[i] + coeff * s1 - s2;
                s2 = s1; s1 = s0;
            }
            bands[b] = (float)Math.Sqrt(s1 * s1 + s2 * s2 - coeff * s1 * s2) / Math.Max(1, Math.Min(buf.Length, SR / 2));
        }

        var shape = new float[8];
        for (int i = 0; i < buf.Length; i++)
            shape[Math.Min(7, i * 8 / buf.Length)] += buf[i] * buf[i];
        for (int i = 0; i < shape.Length; i++) shape[i] = (float)Math.Sqrt(shape[i]);

        var outp = new float[bands.Length + shape.Length];
        Normalise(bands, outp, 0);
        Normalise(shape, outp, bands.Length);
        return outp;
    }

    static void Normalise(float[] src, float[] dst, int at)
    {
        float total = 0f;
        foreach (var v in src) total += v;
        for (int i = 0; i < src.Length; i++) dst[at + i] = total > 1e-9f ? src[i] / total : 0f;
    }

    static float Apart(float[] a, float[] b)
    {
        float d = 0f;
        for (int i = 0; i < a.Length; i++) d += Math.Abs(a[i] - b[i]);
        return d;
    }

    /// <summary>
    /// Every effect end to end, with a beat between each, so the whole palette can be heard in
    /// one listen. The music loops have been written out since they were written; the twenty-five
    /// effects were measured and never once played.
    /// </summary>
    static void WritePalette(string path, List<(string name, float[] buf)> all)
    {
        int gap = SR / 2;
        int total = 0;
        foreach (var s in all) total += s.buf.Length + gap;
        var sheet = new float[total];
        int at = 0;
        foreach (var s in all)
        {
            Array.Copy(s.buf, 0, sheet, at, s.buf.Length);
            at += s.buf.Length + gap;
        }
        WriteWav(path, sheet);
    }

    static void WriteWav(string path, float[] samples)
    {
        using var fs = new FileStream(path, FileMode.Create);
        using var w = new BinaryWriter(fs);
        int dataBytes = samples.Length * 2;
        w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + dataBytes);
        w.Write(new[] { 'W', 'A', 'V', 'E' });
        w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(SR); w.Write(SR * 2); w.Write((short)2); w.Write((short)16);
        w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(dataBytes);
        foreach (var s in samples) w.Write((short)(Math.Clamp(s, -1f, 1f) * 32767));
    }
}
