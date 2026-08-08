using System;
using System.Collections.Generic;
using Eggverse;

class Sim {
    static int failures = 0;
    static void Check(bool ok, string what) { if (!ok) { Console.WriteLine("  FAIL: " + what); failures++; } }

    static void Main() {
        StoryWalk();
        SaveRoundTrip();
        HostileSave();
        Console.WriteLine("== pacing (playing it, not forcing it) ==");
        Pacing.Run_(Check);
        Console.WriteLine("== walking against the rim ==");
        Movement.Run(Check);
        Console.WriteLine("== authored prose ==");
        Prose.Run(Check);
        XpCurve();
        Console.WriteLine("== shell patterns ==");
        var counts = new Dictionary<EggPattern,int>();
        foreach (var sp in SpeciesDatabase.All) {
            Check(sp.Pattern != EggPattern.Auto, $"{sp.Name} resolved to a concrete pattern");
            counts.TryGetValue(sp.Pattern, out int c); counts[sp.Pattern] = c + 1;
        }
        Check(counts.Count == 8, $"all eight patterns are in use (found {counts.Count})");
        foreach (var kv in counts) Console.WriteLine($"  {kv.Key,-9} {kv.Value}");
        // Evolution stages should look different from each other.
        foreach (var sp in SpeciesDatabase.All) {
            if (!sp.CanEvolve) continue;
            var next = SpeciesDatabase.Get(sp.EvolvesIntoId);
            Check(sp.Pattern != next.Pattern, $"{sp.Name} and {next.Name} wear different patterns");
        }
        Console.WriteLine("== evolution ==");
        EvolutionCheck.Run(Check);
        Console.WriteLine("== text fit ==");
        TextFit.Run(Check);
        Console.WriteLine("== ui layout ==");
        Layout.Run(Check);
        Console.WriteLine("== progression (counters when you need them) ==");
        Progression.Run(Check);
        Console.WriteLine("== type chart ==");
        Chart.Run(Check);
        Console.WriteLine("== type palette ==");
        Palette.Run(Check);
        Console.WriteLine("== world seeds ==");
        {
            var seeds = new Dictionary<int,string>();
            foreach (var planet in PlanetDatabase.All)
            {
                Check(planet.Seed > 0, $"{planet.Name} has a positive seed");
                Check(!seeds.ContainsKey(planet.Seed),
                      $"{planet.Name}'s seed does not collide with {(seeds.ContainsKey(planet.Seed) ? seeds[planet.Seed] : "")}");
                seeds[planet.Seed] = planet.Name;
            }
            // The same id must always give the same seed — that is the whole point.
            var again = PlanetDatabase.Get("umbralux");
            Check(again.Seed == PlanetDatabase.Get("umbralux").Seed, "seeds are stable within a run");
            Check(PlanetDatabase.Get("yolkhaven").Seed != PlanetDatabase.Get("mosswell").Seed,
                  "different worlds get different seeds");

            // Layout spread: no single arrangement should dominate the galaxy.
            var layouts = new int[3];
            var roamerCounts = new Dictionary<int,int>();
            foreach (var planet in PlanetDatabase.All)
            {
                if (planet.IsBossWorld) continue;
                layouts[planet.Seed % 3]++;
                int roamers = 3 + (planet.Seed / 3) % 4;
                Check(roamers >= 3 && roamers <= 6, $"{planet.Name} has 3-6 roamers (got {roamers})");
                int fields = 7 + (planet.Seed / 7) % 5;
                Check(fields >= 7 && fields <= 11, $"{planet.Name} has 7-11 shell fields (got {fields})");
                roamerCounts.TryGetValue(roamers, out int c); roamerCounts[roamers] = c + 1;
            }
            Console.WriteLine($"  field layouts — scattered {layouts[0]}, ring {layouts[1]}, clustered {layouts[2]}");
            var parts = new List<string>();
            foreach (var kv in roamerCounts) parts.Add($"{kv.Key} roamers x{kv.Value}");
            Console.WriteLine("  " + string.Join(", ", parts));
            foreach (var n in layouts) Check(n > 0, "every field layout is used by at least one world");
        }
        Console.WriteLine("== elders ==");
        Elders.Run(Check);
        Console.WriteLine("== movesets ==");
        Moves.Run(Check);
        Console.WriteLine("== field record ==");
        {
            var numbers = new HashSet<int>();
            int longest = 0; string longestName = "";
            foreach (var sp in SpeciesDatabase.All)
            {
                int n = SpeciesDatabase.DexNumber(sp.Id);
                Check(n >= 1 && n <= SpeciesDatabase.Count, $"{sp.Name} has a valid dex number");
                Check(numbers.Add(n), $"{sp.Name}'s dex number {n} is unique");
                Check(!string.IsNullOrWhiteSpace(sp.Blurb), $"{sp.Name} has a field-record note");
                Check(sp.Blurb.Length <= 130, $"{sp.Name}'s note fits the panel ({sp.Blurb.Length} chars)");
                Check(sp.Blurb.EndsWith(".") || sp.Blurb.EndsWith("!") || sp.Blurb.EndsWith("?"),
                      $"{sp.Name}'s note is a finished sentence");
                Check(sp.BaseHP <= 100 && sp.BaseAtk <= 100 && sp.BaseDef <= 100 && sp.BaseSpd <= 100,
                      $"{sp.Name}'s stats fit the 0-100 bars (got {sp.BaseHP}/{sp.BaseAtk}/{sp.BaseDef}/{sp.BaseSpd})");
                if (sp.Blurb.Length > longest) { longest = sp.Blurb.Length; longestName = sp.Name; }
            }
            Check(numbers.Count == SpeciesDatabase.Count, "every species has its own dex number");
            Console.WriteLine($"  {SpeciesDatabase.Count} entries, longest note {longest} chars ({longestName})");
        }
        Console.WriteLine("== field-record rewards ==");
        {
            int catchable = SpeciesDatabase.CatchableCount;
            Check(catchable > 0 && catchable < SpeciesDatabase.Count,
                  $"some species are uncatchable by design (catchable {catchable} of {SpeciesDatabase.Count})");
            foreach (var id in new[] { "solyolk", "obsidyolk", "reginova", "vesperling" })
                Check(SpeciesDatabase.Get(id).CatchRate < 20, $"{SpeciesDatabase.Get(id).Name} is not realistically catchable");

            // Walk Ori's milestones the way a collector would, and make sure each fires once.
            var st = new GameState();
            var story = new StoryState();
            story.SetFlag("met_ori"); story.SetFlag("ori_briefed");

            var pool = new List<string>();
            foreach (var sp in SpeciesDatabase.All) if (sp.CatchRate >= 20) pool.Add(sp.Id);

            var fired = new List<string>();
            string gifted = null;
            for (int i = 0; i < pool.Count; i++)
            {
                st.Caught.Add(pool[i]);
                var script = StoryDatabase.GetDialogue("ori", story, st);
                Check(script != null, "Ori always responds");
                if (script == null) continue;
                if (!string.IsNullOrEmpty(script.SetsFlag) && script.SetsFlag.StartsWith("ori_record"))
                {
                    Check(!fired.Contains(script.SetsFlag), $"milestone {script.SetsFlag} fires only once");
                    fired.Add(script.SetsFlag);
                    story.SetFlag(script.SetsFlag);
                    if (!string.IsNullOrEmpty(script.GivesSpeciesId))
                    {
                        gifted = script.GivesSpeciesId;
                        Check(SpeciesDatabase.Get(gifted).Id == gifted, $"the gift '{gifted}' is a real species");
                        Check(script.GivesSpeciesLevel > 0, "the gift has a sensible level");
                    }
                }
            }
            Check(fired.Count == 3, $"all three record milestones fire across a full collection (got {fired.Count})");
            Check(gifted != null, "completing the record hands over an egg");
            Console.WriteLine($"  {catchable} catchable of {SpeciesDatabase.Count}; milestones {string.Join(", ", fired)}; gift = {gifted}");

            // Re-talking after the last milestone must not loop it.
            var again = StoryDatabase.GetDialogue("ori", story, st);
            Check(again != null && (string.IsNullOrEmpty(again.SetsFlag) || !again.SetsFlag.StartsWith("ori_record")),
                  "Ori stops handing out milestones once they are all done");
        }
        Console.WriteLine("== cast ==");
        {
            var st = new GameState();
            int speaking = 0, totalLines = 0;
            foreach (var npc in StoryDatabase.Npcs)
            {
                Check(PlanetDatabase.Get(npc.PlanetId).Id == npc.PlanetId, $"{npc.Name} lives on a real planet");
                // Every NPC must have something to say at every point in the story.
                bool everSilent = false;
                for (int beat = 0; beat < StoryDatabase.Beats.Length; beat++)
                {
                    var probe = new StoryState();
                    probe.RestoreFrom(new string[0], beat);
                    var script = StoryDatabase.GetDialogue(npc.Id, probe, st);
                    if (script == null || script.Lines.Length == 0) { everSilent = true; break; }
                    foreach (var line in script.Lines)
                    {
                        Check(!string.IsNullOrWhiteSpace(line.Text), $"{npc.Name} has no empty lines");
                        Check(!string.IsNullOrWhiteSpace(line.Speaker), $"{npc.Name} lines all name a speaker");
                        // Not "is not default" - the fallback returns a real grey, so that
                        // passed for three residents who had no tint at all for several
                        // revisions. Ask whether the speaker has one of their own.
                        Check(DialogueView.HasTintFor(line.Speaker),
                              $"speaker '{line.Speaker}' has a portrait tint of their own");
                    }
                    totalLines += script.Lines.Length;
                }
                Check(!everSilent, $"{npc.Name} always has something to say");
                if (!everSilent) speaking++;
            }
            // ---- how findable is each egg, now that the dex can tell you ----
            {
                var rarest = new List<string>();
                int fewest = int.MaxValue;
                foreach (var sp in SpeciesDatabase.All)
                {
                    if (sp.CatchRate < 20) continue;
                    int n = PlanetDatabase.WorldsWith(sp.Id).Count;
                    if (n < fewest) { fewest = n; rarest.Clear(); }
                    if (n == fewest) rarest.Add(sp.Name);
                }
                Console.WriteLine($"  rarest catchable eggs live on {fewest} world(s): {string.Join(", ", rarest)}");
            }

            // ---- every gate must actually be openable ----
            // A beat that requires a flag nothing can grant is a soft-lock: the objective sits
            // there forever and the sector never opens. Walk the dialogue tree to a fixed point,
            // collecting every flag any conversation or trainer victory can hand out, and
            // require each beat's prerequisites to be inside that closure.
            {
                // Walk as a player who is actually collecting, not one frozen at the starter egg.
                // Several conversations only hand over their flag once you are carrying enough
                // ("Three, I said. You've got 1."), so an empty nest makes reachable gates look
                // unreachable — the check has to model a player who does the objective.
                var stocked = new GameState();
                foreach (var id in new[] { "sprouteg", "yolkano", "tidepoach", "yolty", "chillet", "cobblet" })
                {
                    var egg = EggInstance.Wild(id, 25);
                    if (stocked.Party.Count < GameState.PartySize) stocked.Party.Add(egg);
                    else stocked.Nest.Add(egg);
                    stocked.Seen.Add(id); stocked.Caught.Add(id);
                }

                var granted = new HashSet<string>();
                for (int pass = 0; pass < 12; pass++)
                {
                    int before = granted.Count;
                    for (int beat = 0; beat < StoryDatabase.Beats.Length; beat++)
                    {
                        var probe = new StoryState();
                        probe.RestoreFrom(new List<string>(granted).ToArray(), beat);

                        var scripts = new List<DialogueScript>();
                        foreach (var npc in StoryDatabase.Npcs)
                            scripts.Add(StoryDatabase.GetDialogue(npc.Id, probe, stocked));
                        scripts.Add(StoryDatabase.AmyIntro(probe));

                        foreach (var script in scripts)
                        {
                            if (script == null) continue;
                            if (!string.IsNullOrEmpty(script.SetsFlag)) granted.Add(script.SetsFlag);
                            if (!string.IsNullOrEmpty(script.StartsTrainer))
                            {
                                var t = StoryDatabase.GetTrainer(script.StartsTrainer);
                                if (t != null && !string.IsNullOrEmpty(t.VictoryFlag)) granted.Add(t.VictoryFlag);
                            }
                        }
                    }
                    if (granted.Count == before) break;
                }

                int gates = 0;
                foreach (var beat in StoryDatabase.Beats)
                {
                    if (beat.RequiredFlags == null) continue;
                    foreach (var flag in beat.RequiredFlags)
                    {
                        gates++;
                        Check(granted.Contains(flag),
                              $"beat '{beat.Id}' needs '{flag}', which nothing in the game can grant");
                    }
                }
                Console.WriteLine($"  {gates} story gates, all reachable from {granted.Count} grantable flags");
            }

            // ---- what the objective panel says as a beat is half-done ----
            {
                var stX = new GameState();
                var drift = new StoryState();
                int driftIdx = 0;
                for (int i = 0; i < StoryDatabase.Beats.Length; i++)
                    if (StoryDatabase.Beats[i].Id == "drift_keepers") driftIdx = i;

                drift.RestoreFrom(new[] { "met_ori", "ori_briefed" }, driftIdx);
                string both = drift.CurrentBlockerText(stX);

                var half = new StoryState();
                half.RestoreFrom(new[] { "met_ori", "ori_briefed", "keeper_marn" }, driftIdx);
                string one = half.CurrentBlockerText(stX);

                Console.WriteLine($"  neither keeper found: \"{both}\"");
                Console.WriteLine($"  Marn found:           \"{one}\"");
                Check(both != one, "the objective panel notices half-finished progress");

                // And the chart says the same when it refuses to plot a course.
                string sealedNow = half.SectorBlockerText(Sector.ShatteredBelt, stX);
                Console.WriteLine($"  chart, route sealed:  \"{(sealedNow ?? "").Replace("\n", " / ")}\"");
                Check(sealedNow != null && sealedNow.Contains("Sable"),
                      "the sealed-route message names what is outstanding");
                Check(one != null && !one.Contains("Marn"), "and stops asking for the one already found");
            }

            // ---- the ending has to reach the people who set it up ----
            // Every named character with hand-written dialogue must say something different
            // once Amy is beaten. Marn, Sable, Pim and Vess all still spoke their mid-game
            // lines over the credits, because nothing checked that they had noticed.
            {
                var named = new List<string>();
                foreach (var npc in StoryDatabase.Npcs) named.Add(npc.Id);
                var before = new StoryState();
                before.RestoreFrom(new[] { "met_ori", "ori_briefed", "keeper_marn", "keeper_sable",
                                           "learned_truth", "beat_vess_1", "beat_vess_2" },
                                   StoryDatabase.Beats.Length - 2);
                var after = new StoryState();
                after.RestoreFrom(new[] { "met_ori", "ori_briefed", "keeper_marn", "keeper_sable",
                                          "learned_truth", "beat_vess_1", "beat_vess_2", "beat_amy" },
                                  StoryDatabase.Beats.Length - 1);

                int reacted = 0;
                foreach (var id in named)
                {
                    var pre = StoryDatabase.GetDialogue(id, before, st);
                    var post = StoryDatabase.GetDialogue(id, after, st);
                    Check(pre != null && post != null, $"{id} speaks both before and after the finale");
                    if (pre == null || post == null) continue;

                    string a = string.Join("|", System.Array.ConvertAll(pre.Lines, l => l.Text));
                    string b = string.Join("|", System.Array.ConvertAll(post.Lines, l => l.Text));
                    Check(a != b, $"{id} says something new once Amy is beaten");
                    if (a != b) reacted++;
                }
                Console.WriteLine($"  {reacted}/{named.Count} characters have an ending of their own");
            }

            // Every planet worth landing on should have someone on it.
            int peopled = 0, inhabitable = 0;
            foreach (var planet in PlanetDatabase.All)
            {
                if (planet.IsBossWorld) continue;
                inhabitable++;
                bool has = false;
                foreach (var npc in StoryDatabase.Npcs) if (npc.PlanetId == planet.Id) has = true;
                if (has) peopled++; else Console.WriteLine($"    (no resident on {planet.Name})");
            }
            Console.WriteLine($"  {speaking} characters, {totalLines} lines across all story states, {peopled}/{inhabitable} worlds peopled");
            Check(peopled == inhabitable, $"every non-boss world has a resident ({peopled} of {inhabitable})");
        }
        Console.WriteLine("== full playthrough (state invariants) ==");
        LongRun.Run(Check);
        Console.WriteLine("== passive traits ==");
        Traits.Run(Check);
        Console.WriteLine("== balance (simulated battles) ==");
        Balance.Run(Check);
        Console.WriteLine("== audio ==");
        // Beside the renders, not in a scratch directory belonging to one session. The music
        // loops have been written since they were written and nobody could find them.
        // Which assertions never ran. Answering "did anything fail" is not the same as
        // answering "did everything get asked".
        {
            var selfReport = SelfCheck.Run();
            {
                string root = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
                    "..", "..", "..", "..", "..", "..", "Assets", "Scripts", "Eggverse");
                    Feedback.StraySentences(root, new[]
                    {
                        "UiCopy.cs", "BattleLog.cs", "Prompts.cs", "StoryDatabase.cs", "SpeciesDatabase.cs",
                        "LandmarkDatabase.cs", "MoveDatabase.cs", "PlanetDatabase.cs", "SelfCheck.cs",
                        "EggType.cs", "EggInstance.cs", "UIKit.cs", "ProcArt.cs",
                    }, Check);
                    Feedback.Toasts(new[]
                    {
                        System.IO.Path.Combine(root, "Core", "GameDirector.cs"),
                        System.IO.Path.Combine(root, "UI", "GalaxyMapView.cs"),
                        System.IO.Path.Combine(root, "UI", "HudView.cs"),
                        System.IO.Path.Combine(root, "World", "SpaceMode.cs"),
                        System.IO.Path.Combine(root, "World", "SurfaceMode.cs"),
                    }, Check);
                    Feedback.Thresholds(new[]
                    {
                        System.IO.Path.Combine(root, "Battle", "BattleMode.cs"),
                        System.IO.Path.Combine(root, "Battle", "BattleCalc.cs"),
                        System.IO.Path.Combine(root, "Data", "EggType.cs"),
                        System.IO.Path.Combine(root, "UI", "HudView.cs"),
                    }, Check);
                Feedback.Cries(new[]
                {
                    System.IO.Path.Combine(root, "Battle", "BattleMode.cs"),
                    System.IO.Path.Combine(root, "UI", "HudView.cs"),
                    System.IO.Path.Combine(root, "Core", "GameDirector.cs"),
                }, Check);
            }
            Feedback.Run(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
                "..", "..", "..", "..", "..", "..", "Assets", "Scripts", "Eggverse", "Battle", "BattleMode.cs"), Check);
            Coverage.Run(System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
                "..", "..", "..", "..", "..", "..", "Assets", "Scripts", "Eggverse", "Verify", "SelfCheck.cs"),
                selfReport.LinesRun, Check);
        }
        AudioCheck.Run(System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? ".",
            "..", "..", "..", "..", "audio"), Check);
        Console.WriteLine(failures == 0 ? "\nALL CHECKS PASSED" : $"\n{failures} CHECK(S) FAILED");
    }

    // ---------------------------------------------------------------- story
    static void StoryWalk() {
        Console.WriteLine("== in-project self-check ==");
        {
            // The suite that ships with the game, exercised through the same harness.
            var report = SelfCheck.Run();
            foreach (var note in report.Notes) Console.WriteLine("  " + note);
            foreach (var f in report.Failures) Console.WriteLine("  SELFCHECK FAIL: " + f);
            Check(report.Ok, $"the shipped self-check passes ({report.Passed} checks, {report.Failures.Count} failures)");
            Console.WriteLine($"  {report.Passed} assertions, {report.Failures.Count} failures");
        }
        Console.WriteLine("== story walk ==");
        var state = new GameState();
        var story = new StoryState();
        int guard = 0;
        while (!story.Finished && guard++ < 40) {
            var beat = story.Current;
            foreach (var f in beat.RequiredFlags) story.SetFlag(f);
            string[] pool = { "sprouteg","yolkano","tidepoach","yolty","chillet","cobblet","nebulegg","duskle" };
            int i = 0;
            while (state.TotalCollected < beat.RequiredEggs && i < pool.Length) state.Collect(EggInstance.Wild(pool[i++], 5));
            i = 0;
            while (state.DistinctTypesHeld < beat.RequiredTypes && i < pool.Length) state.Collect(EggInstance.Wild(pool[i++], 5));
            if (beat.RequiredLevel > 0 && state.HighestPartyLevel < beat.RequiredLevel) {
                var log = new List<string>(); int spins = 0;
                while (state.Party[0].Level < beat.RequiredLevel && spins++ < 500) state.Party[0].GainXp(200, log);
            }
            int before = story.BeatIndex;
            story.Evaluate(state);
            Check(story.BeatIndex != before, $"beat '{beat.Id}' should advance");
            if (story.BeatIndex == before) return;
        }
        Check(story.Finished, "story reaches the epilogue");
        Console.WriteLine($"  walked {guard} beats to '{story.Current.Id}'");

        foreach (var beat in StoryDatabase.Beats) {
            var src = new Dictionary<string,string> {
                {"met_ori","ori"},{"ori_briefed","ori"},{"keeper_marn","marn"},{"keeper_sable","sable"},
                {"beat_vess_1","vess1"},{"learned_truth","pim"},{"beat_vess_2","vess2"},{"beat_amy","AMARANTH"} };
            foreach (var flag in beat.RequiredFlags) {
                Check(src.ContainsKey(flag), $"flag '{flag}' has a known source");
                if (!src.ContainsKey(flag)) continue;
                Sector needed = Sector.Amaranth;
                if (src[flag] != "AMARANTH") {
                    NpcDef found = null;
                    foreach (var n in StoryDatabase.Npcs) if (n.Id == src[flag]) found = n;
                    Check(found != null, $"npc '{src[flag]}' exists");
                    if (found == null) continue;
                    needed = PlanetDatabase.Get(found.PlanetId).Sector;
                }
                Check((int)needed <= (int)beat.MaxSector,
                      $"beat '{beat.Id}' needs '{flag}' from {needed} but only allows {beat.MaxSector}");
            }
        }
        foreach (var p in PlanetDatabase.All)
            foreach (var s in p.Spawns)
                Check(SpeciesDatabase.Get(s.SpeciesId).Id == s.SpeciesId, $"spawn '{s.SpeciesId}' on {p.Name} exists");
        foreach (var sp in SpeciesDatabase.All)
            foreach (var e in sp.Learnset)
                Check(MoveDatabase.Has(e.MoveId), $"move '{e.MoveId}' on {sp.Name} exists");
        foreach (var n in StoryDatabase.Npcs)
            Check(PlanetDatabase.Get(n.PlanetId).Id == n.PlanetId, $"npc {n.Name} planet '{n.PlanetId}' exists");
        foreach (var id in new[]{"vess_1","vess_2","amy"}) {
            var t = StoryDatabase.GetTrainer(id);
            Check(t != null, $"trainer '{id}' exists");
            if (t == null) continue;
            Check(t.SpeciesIds.Length == t.Levels.Length, $"trainer '{id}' has a level per egg");
            foreach (var s in t.SpeciesIds) Check(SpeciesDatabase.Get(s).Id == s, $"trainer '{id}' species '{s}' exists");
        }
    }

    // ------------------------------------------------------------ save/load
    static EggSave Pack(EggInstance e) {
        var ids = new string[e.Moves.Count]; var pps = new int[e.Moves.Count];
        for (int m = 0; m < e.Moves.Count; m++) { ids[m] = e.Moves[m].Move.Id; pps[m] = e.Moves[m].PP; }
        return new EggSave { species = e.Species.Id, nickname = e.Nickname, level = e.Level,
                             xp = e.Xp, hp = e.CurrentHP, moveIds = ids, movePP = pps };
    }
    static EggInstance Unpack(EggSave s) =>
        EggInstance.Restore(s.species, s.nickname, s.level, s.xp, s.hp, s.moveIds, s.movePP);

    /// A save file that names content the game no longer has. This is not a hypothetical:
    /// three planets were added this session, and renaming or dropping any id would produce
    /// exactly this file on a returning player's disk.
    static void HostileSave() {
        Console.WriteLine("== save file naming content that no longer exists ==");

        var data = new SaveData {
            version = 1,
            party = new[] {
                new EggSave { species = "glacegg",     level = 24, hp = 90, elder = true, nickname = "Frost" },
                new EggSave { species = "ghostofachance", level = 30, hp = 99 },   // never existed
            },
            nest = new[] {
                new EggSave { species = "sprouteg", level = 9, hp = 30 },
                new EggSave { species = "",         level = 9, hp = 30 },          // blank id
                new EggSave { species = "removed_species", level = 12, hp = 40 },
            },
            caches  = new[] { "mosswell", "atlantis", "yolkhaven" },   // real, fake, and one with no cache
            seen    = new[] { "sprouteg", "glacegg", "removed_species" },
            caught  = new[] { "sprouteg", "glacegg", "removed_species", "another_ghost" },
            visited = new[] { "yolkhaven", "shimmerfen", "atlantis" },
            flags   = new[] { "met_ori" },
            cartons = 99,                       // out of range
            salves  = -4,                       // out of range
            beatIndex = 2,
            planet  = "atlantis",               // a world that does not exist
            playSeconds = 1234f,
        };

        GameState st; StoryState story; string planet; float secs;
        Check(SaveSystem.Restore(data, out st, out story, out planet, out secs),
              "a save full of stale ids still loads");
        if (st == null) return;

        Check(st.Party.Count == 1, $"the party keeps only the real egg (got {st.Party.Count})");
        if (st.Party.Count > 0) {
            Check(st.Party[0].Species.Id == "glacegg", "the surviving egg is the one that was saved");
            Check(st.Party[0].Elder, "and it is still an Elder");
            Check(st.Party[0].Nickname == "Frost", "and it kept its nickname");
        }
        Check(st.Nest.Count == 1, $"the nest keeps only the real egg (got {st.Nest.Count})");

        Check(!st.Caught.Contains("removed_species") && !st.Caught.Contains("another_ghost"),
              "stale ids do not enter the record");
        Check(st.Caught.Count <= SpeciesDatabase.CatchableCount,
              $"the record cannot exceed what is catchable (got {st.Caught.Count}/{SpeciesDatabase.CatchableCount})");
        Check(!st.Visited.Contains("atlantis"), "a world that does not exist is not marked visited");
        Check(st.Visited.Contains("shimmerfen"), "a world that does exist survives");

        Check(st.Caches.Contains("mosswell"), "a real cache survives the load");
        Check(!st.Caches.Contains("atlantis"), "a cache on a world that does not exist is dropped");
        Check(!st.Caches.Contains("yolkhaven"), "a cache on a world that has none is dropped");
        Check(st.MaxCartons == GameState.BaseMaxCartons + 1,
              $"carton capacity follows the caches that survived (got {st.MaxCartons})");

        Check(planet == PlanetDatabase.Home.Id,
              $"an unknown current world falls back to home, and is not carried forward (got '{planet}')");
        Check(st.Cartons <= st.MaxCartons && st.Cartons >= 0, $"cartons clamped (got {st.Cartons})");
        Check(st.Salves  <= GameState.MaxSalves  && st.Salves  >= 0, $"salves clamped (got {st.Salves})");

        Console.WriteLine($"  survived: {st.Party.Count} in party, {st.Nest.Count} in nest, " +
                          $"{st.Caught.Count} recorded, on {planet}");

        // And an empty save must still be playable rather than handing back a Teo with nothing.
        GameState empty; StoryState s2; string p2; float f2;
        SaveSystem.Restore(new SaveData { version = 1 }, out empty, out s2, out p2, out f2);
        Check(empty != null && empty.Party.Count == 1, "an empty save still yields a starter egg");
        Check(empty != null && empty.Visited.Count >= 1, "an empty save still knows about home");
    }

    static void SaveRoundTrip() {
        Console.WriteLine("== save round-trip ==");
        var originals = new List<EggInstance>();
        var log = new List<string>();

        var a = EggInstance.Wild("sprouteg", 5);
        a.GainXp(500, log);                       // level ups + learned moves
        a.TakeDamage(a.MaxHP / 3);                // partial HP
        a.Moves[0].PP -= 3;                       // spent PP
        a.Nickname = "Sprout";
        originals.Add(a);

        var b = EggInstance.Wild("shadowhisk", 27);
        b.TakeDamage(b.MaxHP);                    // fainted
        originals.Add(b);

        originals.Add(EggInstance.Wild("boulderoo", 1));   // floor case
        originals.Add(EggInstance.Wild("reginova", 30));   // ceiling case

        foreach (var original in originals) {
            var restored = Unpack(Pack(original));
            string who = original.Name;
            Check(restored.Species.Id == original.Species.Id, $"{who} species");
            Check(restored.Nickname == original.Nickname, $"{who} nickname");
            Check(restored.Level == original.Level, $"{who} level ({restored.Level} vs {original.Level})");
            Check(restored.Xp == original.Xp, $"{who} xp ({restored.Xp} vs {original.Xp})");
            Check(restored.CurrentHP == original.CurrentHP, $"{who} hp ({restored.CurrentHP} vs {original.CurrentHP})");
            Check(restored.MaxHP == original.MaxHP, $"{who} maxhp");
            Check(restored.IsFainted == original.IsFainted, $"{who} fainted flag");
            Check(restored.Moves.Count == original.Moves.Count, $"{who} move count ({restored.Moves.Count} vs {original.Moves.Count})");
            for (int i = 0; i < Math.Min(restored.Moves.Count, original.Moves.Count); i++) {
                Check(restored.Moves[i].Move.Id == original.Moves[i].Move.Id, $"{who} move {i} id");
                Check(restored.Moves[i].PP == original.Moves[i].PP, $"{who} move {i} pp");
            }
        }

        // A save referencing a deleted move must not wipe the egg's moveset.
        var junk = EggInstance.Restore("sprouteg", null, 10, 0, 30, new[]{"no_such_move"}, new[]{5});
        Check(junk.Moves.Count >= 1, "unknown move id falls back to something usable");
        Check(junk.HasUsableMove(), "fallback move is usable");

        // Overfull HP in a tampered save must clamp.
        var over = EggInstance.Restore("sprouteg", null, 10, 0, 99999, null, null);
        Check(over.CurrentHP == over.MaxHP, "hp clamps to max");

        var empty = new GameState(false);
        Check(empty.Party.Count == 0, "GameState(false) starts empty for loading");
        Console.WriteLine($"  round-tripped {originals.Count} eggs");
    }

    // ----------------------------------------------------------- xp pacing
    static void XpCurve() {
        Console.WriteLine("== xp pacing ==");
        var log = new List<string>();

        // Solo lead, fighting wild eggs at the local level.
        var lead = EggInstance.Wild("sprouteg", 5);
        int battles = 0, next = 0;
        int[] checkpoints = { 8, 12, 16, 18, 22 };
        while (lead.Level < 22 && battles < 900) {
            var foe = EggInstance.Wild("cobblet", Math.Max(3, lead.Level));
            lead.GainXp(foe.XpRewardFor(), log);
            battles++;
            if (next < checkpoints.Length && lead.Level >= checkpoints[next])
                Console.WriteLine($"  solo lead: level {checkpoints[next++],2} after {battles,3} battles");
        }
        Check(battles > 20, $"level 22 should take more than 20 battles (took {battles})");
        Check(battles < 120, $"level 22 should take fewer than 120 battles (took {battles})");

        // A realistic six-egg nest: the active egg takes the kill, the bench gets 35%.
        var party = new List<EggInstance> {
            EggInstance.Wild("sprouteg", 5), EggInstance.Wild("yolkano", 5), EggInstance.Wild("tidepoach", 6),
            EggInstance.Wild("yolty", 7), EggInstance.Wild("chillet", 8), EggInstance.Wild("cobblet", 8) };
        int teamBattles = 0;
        while (teamBattles < 900) {
            int best = 0; for (int i = 1; i < party.Count; i++) if (party[i].Level > party[best].Level) best = i;
            int active = teamBattles % party.Count;                    // rotate who fights
            var foe = EggInstance.Wild("cobblet", Math.Max(3, party[active].Level));
            int reward = foe.XpRewardFor();
            party[active].GainXp(reward, log);
            for (int i = 0; i < party.Count; i++) if (i != active) party[i].GainXp(Math.Max(1, reward * 35 / 100), log);
            teamBattles++;
            if (party[best].Level >= 22) break;
        }
        int lowest = 99, highest = 0;
        foreach (var e in party) { lowest = Math.Min(lowest, e.Level); highest = Math.Max(highest, e.Level); }
        Console.WriteLine($"  six-egg nest: top level 22 (the Amaranth gate) after {teamBattles} battles (spread {lowest}-{highest})");
        Check(teamBattles < 110, $"a six-egg nest should reach the Amaranth gate in under 110 battles (took {teamBattles})");
        Check(highest - lowest <= 6, $"shared xp should keep the nest within 6 levels (spread was {highest - lowest})");
    }
}
