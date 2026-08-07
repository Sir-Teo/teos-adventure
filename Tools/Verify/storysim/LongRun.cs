using System;
using System.Collections.Generic;
using Eggverse;

/// Plays a whole run end to end — catching, levelling, evolving, advancing the story and
/// save/loading at every step — asserting the state invariants after each action.
static class LongRun
{
    static Action<bool,string> check;
    static int step;

    static void Invariants(GameState st, StoryState story, string where)
    {
        check(st.Party.Count <= GameState.PartySize, $"[{where}] party never exceeds {GameState.PartySize} (was {st.Party.Count})");
        check(st.Party.Count >= 1, $"[{where}] party is never empty");
        check(st.Cartons >= 0 && st.Cartons <= st.MaxCartons, $"[{where}] cartons in range (was {st.Cartons})");
        check(story.BeatIndex >= 0 && story.BeatIndex < StoryDatabase.Beats.Length, $"[{where}] beat index in range");

        foreach (var e in st.Party)
        {
            check(e.CurrentHP >= 0 && e.CurrentHP <= e.MaxHP, $"[{where}] {e.Name} hp in range ({e.CurrentHP}/{e.MaxHP})");
            check(e.Level >= 1 && e.Level <= EggInstance.MaxLevel, $"[{where}] {e.Name} level in range");
            check(e.Moves.Count >= 1 && e.Moves.Count <= EggInstance.MaxMoves, $"[{where}] {e.Name} has 1-4 moves (has {e.Moves.Count})");
            check(st.Caught.Contains(e.Species.Id), $"[{where}] {e.Species.Name} in party is recorded as caught");
            foreach (var m in e.Moves)
                check(m.PP >= 0 && m.PP <= m.Move.MaxPP, $"[{where}] {e.Name}/{m.Move.Name} pp in range");
        }
        foreach (var e in st.Nest)
            check(st.Caught.Contains(e.Species.Id), $"[{where}] {e.Species.Name} in nest is recorded as caught");
        check(st.Visited.Contains(st.CurrentPlanetId), $"[{where}] current planet is charted");
    }

    // Mirrors SaveSystem's pack/unpack without JsonUtility, which needs the engine.
    static (GameState, StoryState) RoundTrip(GameState st, StoryState story)
    {
        var copy = new GameState(false);
        foreach (var e in st.Party) copy.Party.Add(Clone(e));
        foreach (var e in st.Nest) copy.Nest.Add(Clone(e));
        foreach (var s in st.Seen) copy.Seen.Add(s);
        foreach (var s in st.Caught) copy.Caught.Add(s);
        foreach (var s in st.Visited) copy.Visited.Add(s);
        copy.Cartons = st.Cartons;
        copy.AmyDefeated = st.AmyDefeated;
        copy.CurrentPlanetId = st.CurrentPlanetId;

        var storyCopy = new StoryState();
        storyCopy.RestoreFrom(story.FlagsSnapshot(), story.BeatIndex);
        return (copy, storyCopy);
    }

    static EggInstance Clone(EggInstance e)
    {
        var ids = new string[e.Moves.Count];
        var pps = new int[e.Moves.Count];
        for (int i = 0; i < e.Moves.Count; i++) { ids[i] = e.Moves[i].Move.Id; pps[i] = e.Moves[i].PP; }
        return EggInstance.Restore(e.Species.Id, e.Nickname, e.Level, e.Xp, e.CurrentHP, ids, pps);
    }

    static void AssertSame(GameState a, GameState b, StoryState sa, StoryState sb, string where)
    {
        check(a.Party.Count == b.Party.Count, $"[{where}] party size survives a save/load");
        check(a.Nest.Count == b.Nest.Count, $"[{where}] nest size survives a save/load");
        check(a.Caught.Count == b.Caught.Count, $"[{where}] caught set survives a save/load");
        check(a.Visited.Count == b.Visited.Count, $"[{where}] charted worlds survive a save/load");
        check(sa.BeatIndex == sb.BeatIndex, $"[{where}] story position survives a save/load");
        check(a.HighestPartyLevel == b.HighestPartyLevel, $"[{where}] party levels survive a save/load");
        check(a.DistinctTypesHeld == b.DistinctTypesHeld, $"[{where}] type coverage survives a save/load");
        for (int i = 0; i < Math.Min(a.Party.Count, b.Party.Count); i++)
        {
            check(a.Party[i].Species.Id == b.Party[i].Species.Id, $"[{where}] party slot {i} species");
            check(a.Party[i].Name == b.Party[i].Name, $"[{where}] party slot {i} name");
            check(a.Party[i].CurrentHP == b.Party[i].CurrentHP, $"[{where}] party slot {i} hp");
        }
    }

    public static void Run(Action<bool,string> checker)
    {
        check = checker;
        EggRandom.SetSource(EggRandom.Seeded(20260808));

        var state = new GameState();
        var story = new StoryState();
        var log = new List<string>();
        Invariants(state, story, "start");

        int saves = 0, catches = 0, evolutions = 0;

        // Walk the story, doing what a player would do at each beat.
        int guard = 0;
        while (!story.Finished && guard++ < 60)
        {
            var beat = story.Current;

            // Visit every planet this beat allows, catching and levelling as we go.
            foreach (var planet in PlanetDatabase.All)
            {
                if (!story.CanEnter(planet.Sector) || planet.IsBossWorld) continue;
                state.Visited.Add(planet.Id);
                state.CurrentPlanetId = planet.Id;

                for (int enc = 0; enc < 3; enc++)
                {
                    var wild = EggInstance.Wild(planet.RollSpecies(), planet.RollLevel());
                    state.Seen.Add(wild.Species.Id);

                    // Fight it: the lead takes damage and gains xp.
                    var lead = state.Leader;
                    if (lead == null) break;
                    lead.TakeHit(Math.Max(1, lead.MaxHP / 6));
                    var evolvedNow = new List<EggInstance>();
                    state.AwardXp(lead, wild.XpRewardFor(), evolvedNow, log);
                    evolutions += evolvedNow.Count;

                    // Sometimes keep it.
                    if (state.Cartons > 0 && EggRandom.Value < 0.45f)
                    {
                        state.Cartons--;
                        wild.TakeDamage(wild.MaxHP * 3 / 4);
                        if (BattleCalc.RollCatch(wild, out _)) { state.Collect(wild); catches++; }
                    }

                    Invariants(state, story, $"after encounter on {planet.Name}");
                }

                // Rest at the nest station, then save.
                state.HealAll();
                var (loadedState, loadedStory) = RoundTrip(state, story);
                AssertSame(state, loadedState, story, loadedStory, $"save on {planet.Name}");
                Invariants(loadedState, loadedStory, $"loaded on {planet.Name}");
                saves++;
            }

            // Satisfy whatever this beat is waiting on.
            foreach (var flag in beat.RequiredFlags) story.SetFlag(flag);
            if (beat.RequiredLevel > 0)
            {
                int spins = 0;
                while (state.HighestPartyLevel < beat.RequiredLevel && spins++ < 4000)
                    foreach (var e in state.Party) state.AwardXp(e, 200, null, log);
            }
            if (beat.RequiredEggs > 0)
            {
                string[] pool = { "sprouteg", "yolkano", "tidepoach", "yolty", "chillet", "cobblet" };
                int i = 0;
                while (state.TotalCollected < beat.RequiredEggs && i < pool.Length)
                    state.Collect(EggInstance.Wild(pool[i++], 5));
            }

            int prev = story.BeatIndex;
            story.Evaluate(state);
            check(story.BeatIndex >= prev, "the story never runs backwards");
            if (story.BeatIndex == prev) { check(false, $"stuck on beat '{beat.Id}'"); break; }
            step++;
            Invariants(state, story, $"after beat {beat.Id}");
        }

        check(story.Finished, "a full playthrough reaches the epilogue");

        // Final round-trip with a big, messy nest.
        var (finalState, finalStory) = RoundTrip(state, story);
        AssertSame(state, finalState, story, finalStory, "final");

        Console.WriteLine($"  played {step} beats, {catches} caught, {evolutions} evolutions, {saves} save/loads");
        Console.WriteLine($"  ended with {finalState.Party.Count} in party, {finalState.Nest.Count} in nest, " +
                          $"{finalState.Caught.Count}/{SpeciesDatabase.Count} recorded, top level {finalState.HighestPartyLevel}");
        EggRandom.SetSource(null);
    }
}
