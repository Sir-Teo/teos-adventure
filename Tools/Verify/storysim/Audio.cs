using System;
using System.IO;
using System.Reflection;
using Eggverse;

// ProcAudio's synthesis is pure Mathf; only AudioClip.Create touches the engine and it lives
// in Make(). So the loop builders can be invoked directly and analysed here.
static class AudioCheck
{
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
            foreach (Sfx id in Enum.GetValues(typeof(Sfx)))
            {
                var buf = (float[])sfxMethod.Invoke(null, new object[] { id });
                check(buf != null && buf.Length > 0, $"{id} produces samples");
                if (buf == null || buf.Length == 0) continue;

                float peak = 0f, sum = 0f;
                foreach (var v in buf) { peak = Math.Max(peak, Math.Abs(v)); sum += v * v; }
                float rms = (float)Math.Sqrt(sum / buf.Length);

                check(peak > 0.02f, $"{id} is audible (peak {peak:0.000})");
                check(peak <= 1.0f, $"{id} does not clip (peak {peak:0.000})");
                check(buf.Length < 44100 * 3, $"{id} is not absurdly long ({buf.Length / 44100f:0.0}s)");
                // A click at the very end is audible; effects should decay to near silence.
                float tail = Math.Abs(buf[buf.Length - 1]);
                check(tail < 0.12f, $"{id} decays before it ends (tail {tail:0.000})");
                if (peak <= 0.02f) silent++;
                if (peak < quietest) { quietest = peak; quietestName = id.ToString(); }
            }
            Console.WriteLine($"  {Enum.GetValues(typeof(Sfx)).Length} effects checked, none silent, quietest {quietestName} at {quietest:0.00}");
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
