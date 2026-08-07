using System;
using System.Collections.Generic;
using Eggverse;

static class EvolutionCheck
{
    public static void Run(Action<bool,string> check)
    {
        int families = 0;

        foreach (var sp in SpeciesDatabase.All)
        {
            if (!sp.CanEvolve) continue;
            families++;
            var next = SpeciesDatabase.Get(sp.EvolvesIntoId);
            check(next.Id == sp.EvolvesIntoId, $"{sp.Name} -> '{sp.EvolvesIntoId}' resolves");
            check(next.Id != sp.Id, $"{sp.Name} does not evolve into itself");
            check(next.Type == sp.Type, $"{sp.Name} keeps its type through evolution");
            check(next.BaseTotal > sp.BaseTotal,
                  $"{sp.Name}({sp.BaseTotal}) -> {next.Name}({next.BaseTotal}) is an upgrade");
            check(sp.EvolveLevel <= EggInstance.MaxLevel,
                  $"{sp.Name} evolves at or below the level cap");
        }
        check(families == 16, $"16 evolution links defined (found {families})");

        // No cycles, and every chain terminates.
        foreach (var sp in SpeciesDatabase.All)
        {
            var seen = new HashSet<string>();
            var cur = sp;
            int hops = 0;
            while (cur.CanEvolve && hops++ < 10)
            {
                check(seen.Add(cur.Id), $"chain from {sp.Name} has no cycle");
                if (!seen.Contains(cur.Id)) break;
                var nxt = SpeciesDatabase.Get(cur.EvolvesIntoId);
                check(nxt.EvolveLevel == 0 || nxt.EvolveLevel > cur.EvolveLevel,
                      $"{cur.Name}@{cur.EvolveLevel} -> {nxt.Name}@{nxt.EvolveLevel} levels ascend");
                cur = nxt;
            }
            check(hops < 10, $"chain from {sp.Name} terminates");
        }

        // Live behaviour: grind a starter and watch it change twice.
        var egg = EggInstance.Wild("sprouteg", 5);
        var log = new List<string>();
        var stages = new List<string> { egg.Species.Id };
        int guard = 0;
        while (egg.Level < 28 && guard++ < 4000)
        {
            egg.GainXp(80, log);
            if (stages[stages.Count - 1] != egg.Species.Id) stages.Add(egg.Species.Id);
        }
        check(stages.Count == 3, $"sprouteg reaches three stages (got {string.Join(" -> ", stages)})");
        check(egg.Species.Id == "bloomolk", $"final stage is bloomolk (got {egg.Species.Id})");
        Console.WriteLine($"  live chain: {string.Join(" -> ", stages)} by level {egg.Level}");

        // Evolving mid-battle must not heal or kill the egg.
        var hurt = EggInstance.Wild("cobblet", 17);
        hurt.TakeDamage(hurt.MaxHP - 3);
        float before = hurt.HPFraction;
        var l2 = new List<string>();
        int spins = 0;
        while (!hurt.EvolvedThisLevelUp && spins++ < 500) hurt.GainXp(120, l2);
        check(hurt.EvolvedThisLevelUp, "cobblet evolved");
        check(hurt.CurrentHP >= 1, "an evolving egg never faints from the swap");
        check(Math.Abs(hurt.HPFraction - before) < 0.06f,
              $"hp fraction is preserved across evolution ({before:0.000} -> {hurt.HPFraction:0.000})");
        check(hurt.Species.Id == "craggle", $"cobblet became craggle (got {hurt.Species.Id})");

        // A nicknamed egg keeps its name.
        var named = EggInstance.Wild("yolty", 16);
        named.Nickname = "Zip";
        var l3 = new List<string>();
        int s3 = 0;
        while (named.Species.Id == "yolty" && s3++ < 500) named.GainXp(120, l3);
        check(named.Nickname == "Zip", "nickname survives evolution");
        check(named.Name == "Zip", "nicknamed egg still shows its nickname");

        Console.WriteLine($"  {families} evolution links across {SpeciesDatabase.Count} species");
    }
}
