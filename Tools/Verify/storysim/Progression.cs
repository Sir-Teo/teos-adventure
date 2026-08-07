using System;
using System.Collections.Generic;
using Eggverse;

/// A type chart is only fair if the counters are actually obtainable when you need them.
/// Walk the sectors in story order and check that every world's theme can be answered with
/// something catchable at or before that point in the run.
static class Progression
{
    public static void Run(Action<bool,string> check)
    {
        var reachable = new HashSet<string>();          // species ids catchable so far
        reachable.Add("sprouteg");                       // the starter

        Sector[] order = { Sector.HatcheryReach, Sector.LongDrift, Sector.ShatteredBelt, Sector.Amaranth };

        Console.WriteLine("  sector              world          theme    counters available");
        foreach (var sector in order)
        {
            // Worlds in a sector unlock together, so the pool includes this sector's own
            // spawns — you can catch a counter on one world and carry it to its neighbour.
            foreach (var p in PlanetDatabase.All)
                if ((int)p.Sector <= (int)sector)
                    foreach (var s in p.Spawns)
                        if (SpeciesDatabase.Get(s.SpeciesId).CatchRate >= 20) reachable.Add(s.SpeciesId);

            foreach (var p in PlanetDatabase.All)
            {
                if (p.Sector != sector) continue;

                var counters = new List<EggType>();
                foreach (EggType t in Enum.GetValues(typeof(EggType)))
                    if (t != EggType.Plain && TypeChart.Multiplier(t, p.Theme) > 1.2f) counters.Add(t);

                var owned = new List<string>();
                foreach (var id in reachable)
                {
                    var sp = SpeciesDatabase.Get(id);
                    if (counters.Contains(sp.Type)) owned.Add(sp.Name);
                }

                string got = owned.Count == 0 ? "<none>" : string.Join(", ", owned.ToArray());
                if (got.Length > 46) got = got.Substring(0, 43) + "...";
                Console.WriteLine($"  {PlanetDatabase.SectorName(sector),-19} {p.Name,-14} {TypeChart.Name(p.Theme),-8} {got}");

                // The home world is exempt: you arrive with a starter at level 3-6 and are not
                // expected to have type mastery yet.
                if (p.Id == PlanetDatabase.Home.Id) continue;
                check(owned.Count > 0,
                      $"{p.Name} ({p.Theme}) can be answered with something catchable by then");
            }
        }

        // The starter must not be helpless on the very first world it will meet after home.
        var home = PlanetDatabase.Home;
        var second = PlanetDatabase.Get("cinderoost");
        bool homeOffersCounter = false;
        foreach (var s in home.Spawns)
        {
            var sp = SpeciesDatabase.Get(s.SpeciesId);
            if (sp.CatchRate >= 20 && TypeChart.Multiplier(sp.Type, second.Theme) > 1.2f) homeOffersCounter = true;
        }
        check(homeOffersCounter,
              $"{home.Name} offers something that counters {second.Name} ({second.Theme})");

        // And every element should be catchable somewhere, or its counters are academic.
        foreach (EggType t in Enum.GetValues(typeof(EggType)))
        {
            if (t == EggType.Plain) continue;
            bool available = false;
            foreach (var p in PlanetDatabase.All)
                foreach (var s in p.Spawns)
                {
                    var sp = SpeciesDatabase.Get(s.SpeciesId);
                    if (sp.Type == t && sp.CatchRate >= 20) available = true;
                }
            check(available, $"{t} eggs can be caught somewhere in the galaxy");
        }
    }
}
