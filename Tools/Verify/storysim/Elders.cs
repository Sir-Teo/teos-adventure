using System;
using Eggverse;

static class Elders
{
    public static void Run(Action<bool,string> check)
    {
        var normal = EggInstance.Wild("tidepoach", 12);
        var elder  = EggInstance.WildElder("tidepoach", 12);

        check(elder.Elder, "an elder is flagged as one");
        check(!normal.Elder, "an ordinary wild egg is not");
        check(elder.Level == normal.Level + 3, $"an elder is three levels older (was {elder.Level} vs {normal.Level})");
        check(elder.MaxHP > normal.MaxHP, "an elder is bulkier");
        check(elder.CurrentHP == elder.MaxHP, "an elder starts at full health");
        check(elder.Name.StartsWith("Elder "), $"an elder reads as one (got '{elder.Name}')");
        check(elder.XpRewardFor() > normal.XpRewardFor() * 1.5f,
              $"an elder is worth appreciably more xp ({normal.XpRewardFor()} -> {elder.XpRewardFor()})");

        // Catching: much harder, but still possible once worn right down.
        float elderFull = BattleCalc.CatchChance(elder);
        float normalFull = BattleCalc.CatchChance(normal);
        check(elderFull < normalFull * 0.6f, $"an elder resists the carton at full health ({elderFull:P0} vs {normalFull:P0})");

        elder.TakeDamage(elder.MaxHP - 1);
        float elderWeak = BattleCalc.CatchChance(elder);
        check(elderWeak > elderFull * 2f, "wearing an elder down still helps a lot");
        check(elderWeak > 0.10f, $"a worn-down elder is genuinely catchable ({elderWeak:P0})");
        Console.WriteLine($"  elder catch odds: {elderFull:P0} at full health, {elderWeak:P0} at 1 HP" +
                          $"   (ordinary: {normalFull:P0} / {BattleCalc.CatchChance(NearDead("tidepoach", 12)):P0})");

        // A nickname should win over the Elder prefix.
        var named = EggInstance.WildElder("cobblet", 10);
        named.Nickname = "Grandad";
        check(named.Name == "Grandad", "a nickname replaces the Elder prefix");

        // And the flag has to survive being saved.
        var ids = new string[named.Moves.Count];
        var pps = new int[named.Moves.Count];
        for (int i = 0; i < named.Moves.Count; i++) { ids[i] = named.Moves[i].Move.Id; pps[i] = named.Moves[i].PP; }
        var restored = EggInstance.Restore(named.Species.Id, named.Nickname, named.Level, named.Xp,
                                           named.CurrentHP, ids, pps, named.Elder);
        check(restored.Elder, "elder status survives a save/load");
        check(restored.Level == named.Level, "an elder's level survives a save/load");
        check(restored.XpRewardFor() == named.XpRewardFor(), "an elder's worth survives a save/load");

        // A restored ordinary egg must not silently become an elder.
        var plain = EggInstance.Restore("cobblet", null, 10, 0, 20, ids, pps);
        check(!plain.Elder, "restoring without the flag yields an ordinary egg");
    }

    static EggInstance NearDead(string id, int level)
    {
        var e = EggInstance.Wild(id, level);
        e.TakeDamage(e.MaxHP - 1);
        return e;
    }
}
