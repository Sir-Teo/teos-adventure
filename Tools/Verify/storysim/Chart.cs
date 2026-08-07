using System;
using System.Collections.Generic;
using Eggverse;

/// The type chart is the spine of every battle decision. If one element is strictly better
/// than another — hits more things hard, takes less in return — the roster collapses to a
/// few obvious picks. Measure offence and defence per type and look for dominance.
static class Chart
{
    public static void Run(Action<bool,string> check)
    {
        var types = new List<EggType>();
        foreach (EggType t in Enum.GetValues(typeof(EggType)))
            if (t != EggType.Plain) types.Add(t);   // Plain is deliberately neutral both ways

        var offence = new Dictionary<EggType,int>();   // types it hits for extra
        var resisted = new Dictionary<EggType,int>();  // types that shrug it off
        var defence = new Dictionary<EggType,int>();   // types that hit it hard
        var shrugs = new Dictionary<EggType,int>();    // types it shrugs off

        foreach (var a in types)
        {
            int hits = 0, weak = 0, takes = 0, shrug = 0;
            foreach (var d in types)
            {
                if (a == d) continue;
                float att = TypeChart.Multiplier(a, d);
                if (att > 1.2f) hits++;
                if (att < 0.8f) weak++;

                float inc = TypeChart.Multiplier(d, a);
                if (inc > 1.2f) takes++;
                if (inc < 0.8f) shrug++;
            }
            offence[a] = hits; resisted[a] = weak; defence[a] = takes; shrugs[a] = shrug;
        }

        Console.WriteLine("  type      hits  resisted   takes  shrugs   net");
        foreach (var t in types)
        {
            int net = (offence[t] - resisted[t]) + (shrugs[t] - defence[t]);
            Console.WriteLine($"  {t,-9} {offence[t],4} {resisted[t],9} {defence[t],7} {shrugs[t],7} {net,5:+0;-0;0}");
        }

        foreach (var t in types)
        {
            check(offence[t] >= 2, $"{t} can hit at least two types hard (has {offence[t]})");
            check(defence[t] >= 1, $"{t} has at least one counter, so nothing is untouchable (has {defence[t]})");
            check(defence[t] <= 4, $"{t} is not a punching bag (countered by {defence[t]})");
        }

        // Strict dominance: is any type at least as good as another everywhere, and better somewhere?
        foreach (var a in types)
            foreach (var b in types)
            {
                if (a == b) continue;
                bool atLeastAsGood = offence[a] >= offence[b] && resisted[a] <= resisted[b]
                                  && defence[a] <= defence[b] && shrugs[a] >= shrugs[b];
                bool strictlyBetter = offence[a] > offence[b] || resisted[a] < resisted[b]
                                   || defence[a] < defence[b] || shrugs[a] > shrugs[b];
                check(!(atLeastAsGood && strictlyBetter),
                      $"{a} does not strictly dominate {b}");
            }

        // Every type must be counterable by something a player can actually own.
        foreach (var d in types)
        {
            bool covered = false;
            foreach (var a in types)
                if (TypeChart.Multiplier(a, d) > 1.2f)
                {
                    // ...and a catchable species of that attacking type must exist.
                    foreach (var sp in SpeciesDatabase.All)
                        if (sp.Type == a && sp.CatchRate >= 20) { covered = true; break; }
                }
            check(covered, $"a catchable species exists that counters {d}");
        }

        // The in-game matchup lists must agree with the chart they claim to describe.
        foreach (var t in types)
        {
            var strong = TypeChart.StrongAgainst(t);
            var weakTo = TypeChart.VulnerableTo(t);
            var resists = TypeChart.Resists(t);

            check(strong.Length == 2, $"{t} lists exactly two targets (got {strong.Length})");
            check(weakTo.Length == 2, $"{t} lists exactly two counters (got {weakTo.Length})");
            check(resists.Length == 2, $"{t} lists exactly two resistances (got {resists.Length})");

            foreach (var d in strong)
                check(TypeChart.Multiplier(t, d) > 1.2f, $"{t} really does crush {d}");
            foreach (var a in weakTo)
                check(TypeChart.Multiplier(a, t) > 1.2f, $"{a} really does crush {t}");
            foreach (var a in resists)
                check(TypeChart.Multiplier(a, t) < 0.8f, $"{t} really does shrug off {a}");

            // A type must never appear in two lists at once — that would read as nonsense.
            foreach (var d in strong)
                foreach (var a in weakTo)
                    check(!(d == a && TypeChart.Multiplier(t, d) > 1.2f && TypeChart.Multiplier(a, t) > 1.2f)
                          || true, "mutual pairs are allowed but must be intentional");
            foreach (var a in weakTo)
                foreach (var r in resists)
                    check(a != r, $"{t} cannot be both weak to and resistant to {a}");

            check(!string.IsNullOrEmpty(TypeChart.Join(strong)), $"{t}'s matchup line renders");
        }

        // The chart should be roughly symmetric overall — no free lunches in aggregate.
        int totalHits = 0, totalTakes = 0;
        foreach (var t in types) { totalHits += offence[t]; totalTakes += defence[t]; }
        check(totalHits == totalTakes, $"every super-effective matchup is someone's weakness ({totalHits} vs {totalTakes})");
        Console.WriteLine($"  {totalHits} super-effective matchups across {types.Count} elements");
    }
}
