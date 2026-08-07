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
    /// The game's own save and restore, with the disk left out of it.
    ///
    /// This used to be a hand-written copy of the packing: party, nest, seen, caught, visited,
    /// cartons, the Amy flag and the current planet. It had fallen five fields behind - caches,
    /// salves, landmarks, landmark asides - and the eggs it cloned lost their elder and starter
    /// marks. So a playthrough could "save and load" seventy-eight times and never once carry
    /// any of them, and the test would have reported success either way.
    static (GameState, StoryState) RoundTrip(GameState st, StoryState story)
    {
        var packed = SaveSystem.Capture(st, story, st.CurrentPlanetId, 0f);
        GameState loaded; StoryState loadedStory; string planet; float seconds;
        if (!SaveSystem.Restore(packed, out loaded, out loadedStory, out planet, out seconds))
            throw new Exception("the game could not restore its own save");
        loaded.CurrentPlanetId = planet;
        return (loaded, loadedStory);
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
        // Everything else a save carries. The comparison used to stop at party, nest, caught,
        // visited and the beat - so five fields could go missing from the packing without a
        // single one of seventy-eight save/loads noticing.
        check(a.Cartons == b.Cartons, $"[{where}] cartons survive a save/load");
        check(a.Salves == b.Salves, $"[{where}] salves survive a save/load");
        check(a.Caches.Count == b.Caches.Count, $"[{where}] dug caches survive a save/load");
        check(a.Landmarks.Count == b.Landmarks.Count, $"[{where}] inscriptions read survive a save/load");
        check(a.LandmarkAsides.Count == b.LandmarkAsides.Count, $"[{where}] residents' asides survive a save/load");
        check(a.Seen.Count == b.Seen.Count, $"[{where}] the seen set survives a save/load");
        check(a.AmyDefeated == b.AmyDefeated, $"[{where}] the Amy flag survives a save/load");
        check(a.MaxCartons == b.MaxCartons, $"[{where}] carton capacity survives a save/load");
        check((a.EggFromOri != null) == (b.EggFromOri != null),
              $"[{where}] the egg Ori gave you survives a save/load");

        for (int i = 0; i < Math.Min(a.Party.Count, b.Party.Count); i++)
        {
            check(a.Party[i].Species.Id == b.Party[i].Species.Id, $"[{where}] party slot {i} species");
            check(a.Party[i].Name == b.Party[i].Name, $"[{where}] party slot {i} name");
            check(a.Party[i].CurrentHP == b.Party[i].CurrentHP, $"[{where}] party slot {i} hp");
            check(a.Party[i].Elder == b.Party[i].Elder, $"[{where}] party slot {i} elder mark");
            check(a.Party[i].FromOri == b.Party[i].FromOri, $"[{where}] party slot {i} starter mark");
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

                // A player who walks out past the shell fields. Without this the run never read
                // an inscription, so the sets that record them stayed empty on both sides of a
                // save and comparing them proved nothing - dropping landmarks from the packing
                // entirely still passed.
                if (LandmarkDatabase.For(planet.Id) != null)
                {
                    state.Landmarks.Add(planet.Id);
                    foreach (var npc in StoryDatabase.Npcs)
                        if (npc.PlanetId == planet.Id) state.LandmarkAsides.Add(npc.Id);
                }
                if (PlanetDatabase.HasCache(planet.Id)) state.Caches.Add(planet.Id);

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
