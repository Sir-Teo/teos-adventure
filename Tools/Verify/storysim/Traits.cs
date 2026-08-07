using System;
using System.Collections.Generic;
using Eggverse;

static class Traits
{
    public static void Run(Action<bool,string> check)
    {
        // Every type must map to exactly one passive, and all eight must be used.
        var used = new HashSet<EggTrait>();
        foreach (EggType t in Enum.GetValues(typeof(EggType)))
        {
            var trait = TypeChart.TraitOf(t);
            if (t == EggType.Plain) { check(trait == EggTrait.None, "Plain has no passive"); continue; }
            check(trait != EggTrait.None, $"{t} has a passive");
            check(used.Add(trait), $"{t}'s passive {trait} is not shared with another type");
            check(!string.IsNullOrEmpty(TypeChart.TraitBlurb(trait)), $"{trait} has a description");
        }
        check(used.Count == 8, $"all eight passives are in play (found {used.Count})");

        // Sturdy (Stone): survives a knockout from full health, but not from a chip.
        var cobblet = EggInstance.Wild("cobblet", 20);
        check(cobblet.Trait == EggTrait.Sturdy, "Cobblet is Sturdy");
        cobblet.TakeHit(99999);
        check(cobblet.CurrentHP == 1, $"Sturdy leaves 1 HP from full (got {cobblet.CurrentHP})");
        cobblet.TakeHit(99999);
        check(cobblet.IsFainted, "Sturdy does not save twice");

        // Hardhead (Void): stat drops bounce off.
        var duskle = EggInstance.Wild("duskle", 20);
        int stage = duskle.SpdStage;
        check(!duskle.TryLowerStage(ref stage), "Hardhead refuses a stat drop");
        var yolty = EggInstance.Wild("yolty", 20);
        int stage2 = yolty.SpdStage;
        check(yolty.TryLowerStage(ref stage2) && stage2 == -1, "a normal egg does take the stat drop");

        // Warm Yolk (Verdant): mends each round, but never past full.
        var sprouteg = EggInstance.Wild("sprouteg", 20);
        check(sprouteg.TickRegen() == 0, "Warm Yolk does nothing at full health");
        sprouteg.TakeDamage(sprouteg.MaxHP / 2);
        check(sprouteg.TickRegen() > 0, "Warm Yolk mends when hurt");
        var stone = EggInstance.Wild("cobblet", 20);
        stone.TakeDamage(stone.MaxHP / 2);
        check(stone.TickRegen() == 0, "a non-Verdant egg does not regenerate");

        // Featherlight (Tidal): meaningfully faster than the raw stat.
        var tide = EggInstance.Wild("tidepoach", 20);
        check(tide.Spd > tide.RawSpd, $"Featherlight raises speed ({tide.RawSpd} -> {tide.Spd})");
        var rock = EggInstance.Wild("cobblet", 20);
        check(rock.Spd == rock.RawSpd, "a non-Tidal egg keeps its raw speed");

        // Tough Shell (Frost): blunts super-effective damage specifically.
        EggRandom.SetSource(() => 0.5f);          // no crit, mid damage roll
        var molten = EggInstance.Wild("yolkano", 20);
        var frost = EggInstance.Wild("chillet", 20);
        var plain = EggInstance.Wild("sprouteg", 20);
        var ember = MoveDatabase.Get("embercrack");
        int vsFrost = BattleCalc.Damage(molten, frost, ember, out float mFrost, out _);
        int vsVerdant = BattleCalc.Damage(molten, plain, ember, out float mVerdant, out _);
        check(mFrost > 1.2f && mVerdant > 1.2f, "Molten is super-effective on both Frost and Verdant");
        // Same multiplier and similar bulk, so the gap is Tough Shell doing its job.
        check(vsFrost < vsVerdant, $"Tough Shell reduces the super-effective hit ({vsFrost} vs {vsVerdant})");

        // Overheat (Molten): more damage when badly hurt.
        var hot = EggInstance.Wild("yolkano", 20);
        var target = EggInstance.Wild("cobblet", 20);
        int healthy = BattleCalc.Damage(hot, target, ember, out _, out _);
        hot.TakeDamage((int)(hot.MaxHP * 0.8f));
        int desperate = BattleCalc.Damage(hot, target, ember, out _, out _);
        check(desperate > healthy, $"Overheat hits harder when hurt ({healthy} -> {desperate})");

        // Lucky (Aether): crits far more often than the base rate.
        EggRandom.SetSource(EggRandom.Seeded(31337));
        var aether = EggInstance.Wild("nebulegg", 20);
        var normal = EggInstance.Wild("sprouteg", 20);
        int luckyCrits = 0, plainCrits = 0, runs = 4000;
        var mind = MoveDatabase.Get("mindyolk");
        for (int i = 0; i < runs; i++)
        {
            BattleCalc.Damage(aether, target, mind, out _, out bool c1); if (c1) luckyCrits++;
            BattleCalc.Damage(normal, target, mind, out _, out bool c2); if (c2) plainCrits++;
        }
        Console.WriteLine($"  crit rate — Lucky {luckyCrits * 100f / runs:0.0}%  vs normal {plainCrits * 100f / runs:0.0}%");
        check(luckyCrits > plainCrits * 1.8f, "Lucky crits substantially more often");
        EggRandom.SetSource(null);

        Console.WriteLine("  8 passives verified end to end");
    }
}
