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
            }
            Console.WriteLine($"  {Enum.GetValues(typeof(Sfx)).Length} effects checked, none silent, quietest {quietestName} at {quietest:0.00}");

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
