using System;
using System.Collections.Generic;
using Eggverse;
using UnityEngine;

/// The nine type colours carry real information — party dots, chips, damage numbers, map
/// labels. If two are perceptually close, players cannot tell Frost from Tidal at a glance,
/// and colour-blind players fare worse still. Measure it rather than eyeball it.
static class Palette
{
    // sRGB -> linear -> CIE XYZ (D65) -> CIE Lab
    static (double L, double a, double b) Lab(Color c)
    {
        double f(double v) => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        double r = f(c.r), g = f(c.g), bl = f(c.b);

        double X = (r * 0.4124564 + g * 0.3575761 + bl * 0.1804375) / 0.95047;
        double Y = (r * 0.2126729 + g * 0.7151522 + bl * 0.0721750);
        double Z = (r * 0.0193339 + g * 0.1191920 + bl * 0.9503041) / 1.08883;

        double k(double t) => t > 0.008856 ? Math.Cbrt(t) : (7.787 * t) + 16.0 / 116.0;
        double fx = k(X), fy = k(Y), fz = k(Z);
        return (116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz));
    }

    static double DeltaE(Color x, Color y)
    {
        var a = Lab(x); var b = Lab(y);
        double dL = a.L - b.L, da = a.a - b.a, db = a.b - b.b;
        return Math.Sqrt(dL * dL + da * da + db * db);
    }

    /// Rough dichromat simulations (Brettel-style approximations) for the two common forms.
    static Color Protanope(Color c) =>
        new Color(0.170f * c.r + 0.829f * c.g + 0.001f * c.b,
                  0.170f * c.r + 0.829f * c.g + 0.001f * c.b,
                  0.004f * c.r + 0.024f * c.g + 0.972f * c.b);

    static Color Deuteranope(Color c) =>
        new Color(0.330f * c.r + 0.666f * c.g + 0.004f * c.b,
                  0.330f * c.r + 0.666f * c.g + 0.004f * c.b,
                  0.000f * c.r + 0.242f * c.g + 0.758f * c.b);

    public static void Run(Action<bool,string> check)
    {
        var types = new List<EggType>();
        foreach (EggType t in Enum.GetValues(typeof(EggType))) types.Add(t);

        void Report(string label, Func<Color, Color> filter, double threshold)
        {
            double worst = double.MaxValue; string worstPair = "";
            for (int i = 0; i < types.Count; i++)
                for (int j = i + 1; j < types.Count; j++)
                {
                    double d = DeltaE(filter(TypeChart.ColorOf(types[i])), filter(TypeChart.ColorOf(types[j])));
                    if (d < worst) { worst = d; worstPair = $"{types[i]}/{types[j]}"; }
                }
            Console.WriteLine($"  {label,-14} closest pair {worstPair,-18} ΔE {worst:0.0}");
            check(worst >= threshold, $"{label}: {worstPair} are too close to tell apart (ΔE {worst:0.0}, want >= {threshold})");
        }

        Report("normal vision", c => c, 22.0);
        Report("protanopia", Protanope, 11.0);
        Report("deuteranopia", Deuteranope, 12.0);

        // Colour alone is never sufficient, so every type must also carry a distinct tag.
        var tags = new HashSet<string>();
        foreach (var t in types)
        {
            string tag = TypeChart.Abbrev(t);
            check(!string.IsNullOrEmpty(tag) && tag.Length == 3, $"{t} has a three-letter tag");
            check(tags.Add(tag), $"{t}'s tag '{tag}' is unique");
        }

        // Every type colour must also read against the dark UI it sits on.
        var background = new Color32(0x12, 0x14, 0x22, 0xFF);
        foreach (var t in types)
        {
            double d = DeltaE(TypeChart.ColorOf(t), background);
            check(d > 45.0, $"{t} stands out against the panel background (ΔE {d:0.0})");
        }
    }
}
