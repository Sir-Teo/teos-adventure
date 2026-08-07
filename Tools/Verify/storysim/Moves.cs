using System;
using System.Collections.Generic;
using Eggverse;

static class Moves
{
    public static void Run(Action<bool,string> check)
    {
        // Which moves does anybody actually learn?
        var learners = new Dictionary<string,int>();
        var allMoveIds = new List<string>();
        foreach (var sp in SpeciesDatabase.All)
        {
            var seen = new HashSet<string>();
            foreach (var e in sp.Learnset)
            {
                if (!seen.Add(e.MoveId)) continue;
                learners.TryGetValue(e.MoveId, out int c);
                learners[e.MoveId] = c + 1;
            }
        }

        // Reflect the move table so unreachable moves show up.
        var field = typeof(MoveDatabase).GetField("byId",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        var byId = (Dictionary<string, MoveDef>)field.GetValue(null);
        foreach (var kv in byId) allMoveIds.Add(kv.Key);

        var unused = new List<string>();
        foreach (var id in allMoveIds)
            if (!learners.ContainsKey(id)) unused.Add(byId[id].Name + " (" + id + ")");

        Console.WriteLine($"  {allMoveIds.Count} moves defined, {learners.Count} reachable through learnsets");
        if (unused.Count > 0) Console.WriteLine("    unreachable: " + string.Join(", ", unused));

        // How varied is a species' kit, and how much do kits overlap?
        int minDistinct = 99; string thinnest = "";
        foreach (var sp in SpeciesDatabase.All)
        {
            var set = new HashSet<string>();
            foreach (var e in sp.Learnset) set.Add(e.MoveId);
            if (set.Count < minDistinct) { minDistinct = set.Count; thinnest = sp.Name; }
            check(set.Count >= 4, $"{sp.Name} learns at least 4 distinct moves (has {set.Count})");
        }
        Console.WriteLine($"  thinnest kit: {thinnest} with {minDistinct} distinct moves");

        // A level-capped egg should end up with a full, usable moveset.
        foreach (var sp in SpeciesDatabase.All)
        {
            var egg = new EggInstance(sp, EggInstance.MaxLevel);
            check(egg.Moves.Count == EggInstance.MaxMoves || sp.Learnset.Length < 4,
                  $"{sp.Name} fills all four move slots by max level (has {egg.Moves.Count})");
            bool anyDamaging = false;
            foreach (var m in egg.Moves) if (!m.Move.IsStatus) anyDamaging = true;
            check(anyDamaging, $"{sp.Name} always has a damaging move");
            // And it should have something that benefits from its own type.
            bool anyStab = false;
            foreach (var m in egg.Moves) if (m.Move.Type == sp.Type && !m.Move.IsStatus) anyStab = true;
            check(anyStab, $"{sp.Name} has a same-type attack at max level");
        }

        // Every move must be able to describe itself for the battle menu.
        int authored = 0, generated = 0, longestDesc = 0; string longestName = "";
        foreach (var id in allMoveIds)
        {
            var m = byId[id];
            string d = m.Describe();
            check(!string.IsNullOrWhiteSpace(d), $"{m.Name} has a description");
            check(d.EndsWith("."), $"{m.Name}'s description is a finished sentence");
            check(d.Length <= 60, $"{m.Name}'s description fits the message box ({d.Length} chars)");
            if (!string.IsNullOrEmpty(m.Blurb)) authored++; else generated++;
            if (d.Length > longestDesc) { longestDesc = d.Length; longestName = m.Name; }
        }
        Console.WriteLine($"  descriptions: {authored} authored, {generated} generated, longest {longestDesc} chars ({longestName})");

        // Coverage: for every type, does something exist that hits it hard?
        foreach (EggType def in Enum.GetValues(typeof(EggType)))
        {
            if (def == EggType.Plain) continue;
            bool covered = false;
            foreach (var id in allMoveIds)
            {
                var m = byId[id];
                if (!m.IsStatus && TypeChart.Multiplier(m.Type, def) > 1.2f) { covered = true; break; }
            }
            check(covered, $"something in the move list is super-effective against {def}");
        }
    }
}
