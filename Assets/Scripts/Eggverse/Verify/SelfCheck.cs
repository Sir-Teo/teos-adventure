using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Eggverse
{
    /// <summary>
    /// Design-level assertions about the game's data: the story can be finished, the type
    /// chart is balanced, counters are obtainable when needed, no authored text overflows its
    /// panel, colours stay distinguishable. Pure logic, no scene required.
    ///
    /// Run it from the editor menu (Eggverse ▸ Run Self-Check) or call Run() from anywhere.
    /// These are the checks that caught, among others: a story gate that gated nothing, a
    /// type chart where Stone dominated five other elements, a Tidal world with no obtainable
    /// counter, and two colours indistinguishable under deuteranopia.
    /// </summary>
    public static class SelfCheck
    {
        public class Report
        {
            public int Passed;
            public readonly List<string> Failures = new List<string>();
            public readonly List<string> Notes = new List<string>();
            public bool Ok => Failures.Count == 0;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.Append(Ok ? "Eggverse self-check PASSED" : "Eggverse self-check FAILED");
                sb.Append("  (").Append(Passed).Append(" checks");
                if (!Ok) sb.Append(", ").Append(Failures.Count).Append(" failures");
                sb.Append(")\n");
                foreach (var n in Notes) sb.Append("  · ").Append(n).Append('\n');
                foreach (var f in Failures) sb.Append("  FAIL: ").Append(f).Append('\n');
                return sb.ToString();
            }
        }

        public static Report Run()
        {
            var r = new Report();
            Action<bool, string> check = (ok, what) =>
            {
                if (ok) r.Passed++;
                else r.Failures.Add(what);
            };

            Story(r, check);
            Chart(r, check);
            Progression(r, check);
            Species(r, check);
            Ground(r, check);
            Palette(r, check);
            Text(r, check);
            return r;
        }

        // ------------------------------------------------------------------

        static void Story(Report r, Action<bool, string> check)
        {
            var state = new GameState();
            var story = new StoryState();
            int guard = 0;

            while (!story.Finished && guard++ < 40)
            {
                var beat = story.Current;
                foreach (var f in beat.RequiredFlags) story.SetFlag(f);

                string[] pool = { "sprouteg", "yolkano", "tidepoach", "yolty", "chillet", "cobblet" };
                int i = 0;
                while (state.TotalCollected < beat.RequiredEggs && i < pool.Length)
                    state.Collect(EggInstance.Wild(pool[i++], 5));
                i = 0;
                while (state.DistinctTypesHeld < beat.RequiredTypes && i < pool.Length)
                    state.Collect(EggInstance.Wild(pool[i++], 5));
                if (beat.RequiredLevel > 0)
                {
                    var log = new List<string>();
                    int spins = 0;
                    while (state.HighestPartyLevel < beat.RequiredLevel && spins++ < 4000)
                        foreach (var e in state.Party) state.AwardXp(e, 200, null, log);
                }

                int before = story.BeatIndex;
                story.Evaluate(state);
                if (story.BeatIndex == before) { check(false, "story is stuck on beat '" + beat.Id + "'"); return; }
            }
            check(story.Finished, "the story can be played to its end");
            r.Notes.Add("story: " + StoryDatabase.Beats.Length + " beats, reaches '" + story.Current.Id + "'");

            // Every required flag must come from someone reachable during that beat.
            var source = new Dictionary<string, string>
            {
                { "met_ori", "ori" }, { "ori_briefed", "ori" }, { "keeper_marn", "marn" },
                { "keeper_sable", "sable" }, { "beat_vess_1", "vess1" }, { "learned_truth", "pim" },
                { "beat_vess_2", "vess2" }, { "beat_amy", null },
            };
            foreach (var beat in StoryDatabase.Beats)
                foreach (var flag in beat.RequiredFlags)
                {
                    string npcId;
                    check(source.TryGetValue(flag, out npcId), "flag '" + flag + "' has a known source");
                    if (!source.TryGetValue(flag, out npcId)) continue;

                    Sector needed = Sector.Amaranth;
                    if (npcId != null)
                    {
                        NpcDef found = null;
                        foreach (var n in StoryDatabase.Npcs) if (n.Id == npcId) found = n;
                        check(found != null, "npc '" + npcId + "' exists");
                        if (found == null) continue;
                        needed = PlanetDatabase.Get(found.PlanetId).Sector;
                    }
                    check((int)needed <= (int)beat.MaxSector,
                          "beat '" + beat.Id + "' needs '" + flag + "' from " + needed +
                          " but only allows travel to " + beat.MaxSector);
                }

            // Everyone must have something to say at every point in the story.
            var probeState = new GameState();
            foreach (var npc in StoryDatabase.Npcs)
            {
                check(PlanetDatabase.Get(npc.PlanetId).Id == npc.PlanetId, npc.Name + " lives on a real planet");
                for (int b = 0; b < StoryDatabase.Beats.Length; b++)
                {
                    var probe = new StoryState();
                    probe.RestoreFrom(new string[0], b);
                    var script = StoryDatabase.GetDialogue(npc.Id, probe, probeState);
                    check(script != null && script.Lines.Length > 0,
                          npc.Name + " has something to say at beat " + b);
                }
            }

            // Every landable world should have a resident.
            int peopled = 0, landable = 0;
            foreach (var p in PlanetDatabase.All)
            {
                if (p.IsBossWorld) continue;
                landable++;
                foreach (var n in StoryDatabase.Npcs) if (n.PlanetId == p.Id) { peopled++; break; }
            }
            check(peopled == landable, "every landable world has a resident (" + peopled + "/" + landable + ")");
        }

        // ------------------------------------------------------------------

        static void Chart(Report r, Action<bool, string> check)
        {
            var types = new List<EggType>();
            foreach (EggType t in Enum.GetValues(typeof(EggType))) if (t != EggType.Plain) types.Add(t);

            int totalHits = 0, totalTakes = 0;
            foreach (var t in types)
            {
                var strong = TypeChart.StrongAgainst(t);
                var weakTo = TypeChart.VulnerableTo(t);
                var resists = TypeChart.Resists(t);

                // The ring guarantees 2/2/2. Anything else means the chart was edited off it.
                check(strong.Length == 2, t + " crushes exactly two elements (got " + strong.Length + ")");
                check(weakTo.Length == 2, t + " is crushed by exactly two (got " + weakTo.Length + ")");
                check(resists.Length == 2, t + " resists exactly two (got " + resists.Length + ")");

                foreach (var d in strong) check(TypeChart.Multiplier(t, d) > 1.2f, t + " really crushes " + d);
                foreach (var a in weakTo) check(TypeChart.Multiplier(a, t) > 1.2f, a + " really crushes " + t);
                foreach (var a in weakTo)
                    foreach (var x in resists)
                        check(a != x, t + " cannot be both weak to and resistant to " + a);

                totalHits += strong.Length;
                totalTakes += weakTo.Length;
            }
            check(totalHits == totalTakes,
                  "every super-effective matchup is someone's weakness (" + totalHits + " vs " + totalTakes + ")");

            // No element may be at least as good as another everywhere and better somewhere.
            foreach (var a in types)
                foreach (var b in types)
                {
                    if (a == b) continue;
                    int oa = TypeChart.StrongAgainst(a).Length, ob = TypeChart.StrongAgainst(b).Length;
                    int da = TypeChart.VulnerableTo(a).Length, db = TypeChart.VulnerableTo(b).Length;
                    int sa = TypeChart.Resists(a).Length, sb = TypeChart.Resists(b).Length;
                    bool atLeast = oa >= ob && da <= db && sa >= sb;
                    bool better = oa > ob || da < db || sa > sb;
                    check(!(atLeast && better), a + " must not strictly dominate " + b);
                }
            r.Notes.Add("type chart: " + totalHits + " super-effective matchups, no dominance");
        }

        // ------------------------------------------------------------------

        static void Progression(Report r, Action<bool, string> check)
        {
            // ---- supplies ----
            {
                var st = new GameState();
                check(st.Cartons == st.MaxCartons, "a new run starts with a full carton stack");
                check(st.Salves == GameState.MaxSalves, "a new run starts with a full salve stack");
                st.Cartons = 0; st.Salves = 0;
                st.RestockSupplies();
                check(st.Cartons == st.MaxCartons && st.Salves == GameState.MaxSalves,
                      "a Nest Station restocks both cartons and salves");

                // A salve has to be worth a turn: it must out-heal a typical hit but never
                // fully top an egg up, or there would be no reason to walk back to the nest.
                var egg = EggInstance.Wild("sprouteg", 20);
                egg.TakeHit(egg.MaxHP - 1);
                int before = egg.CurrentHP;
                egg.Heal(Mathf.Max(1, Mathf.RoundToInt(egg.MaxHP * 0.55f)));
                check(egg.CurrentHP - before > egg.MaxHP / 3, "a salve heals a meaningful chunk");
                check(egg.CurrentHP < egg.MaxHP, "a salve alone cannot fully restore an egg");
                egg.Heal(egg.MaxHP * 4);
                check(egg.CurrentHP == egg.MaxHP, "healing never overshoots max HP");
            }

            var reachable = new HashSet<string> { "sprouteg" };
            Sector[] order = { Sector.HatcheryReach, Sector.LongDrift, Sector.ShatteredBelt, Sector.Amaranth };

            foreach (var sector in order)
            {
                foreach (var p in PlanetDatabase.All)
                    if ((int)p.Sector <= (int)sector)
                        foreach (var s in p.Spawns)
                            if (SpeciesDatabase.Get(s.SpeciesId).CatchRate >= 20) reachable.Add(s.SpeciesId);

                foreach (var p in PlanetDatabase.All)
                {
                    if (p.Sector != sector || p.Id == PlanetDatabase.Home.Id) continue;

                    bool answered = false;
                    foreach (var id in reachable)
                        if (TypeChart.Multiplier(SpeciesDatabase.Get(id).Type, p.Theme) > 1.2f) answered = true;

                    check(answered, p.Name + " (" + p.Theme + ") has an obtainable counter by then");
                }
            }

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
                check(available, t + " eggs are catchable somewhere");
            }
        }

        // ------------------------------------------------------------------

        static void Species(Report r, Action<bool, string> check)
        {
            var dexNumbers = new HashSet<int>();
            int families = 0;

            foreach (var sp in SpeciesDatabase.All)
            {
                check(dexNumbers.Add(SpeciesDatabase.DexNumber(sp.Id)), sp.Name + " has a unique dex number");
                check(!string.IsNullOrEmpty(sp.Blurb), sp.Name + " has a field-record note");
                check(sp.Pattern != EggPattern.Auto, sp.Name + " resolved to a concrete shell pattern");
                check(sp.BaseHP <= 100 && sp.BaseAtk <= 100 && sp.BaseDef <= 100 && sp.BaseSpd <= 100,
                      sp.Name + "'s stats fit the 0-100 bars");

                foreach (var e in sp.Learnset)
                    check(MoveDatabase.Has(e.MoveId), sp.Name + " learns a real move '" + e.MoveId + "'");

                var capped = new EggInstance(sp, EggInstance.MaxLevel);
                bool damaging = false, stab = false;
                foreach (var m in capped.Moves)
                {
                    if (!m.Move.IsStatus) damaging = true;
                    if (m.Move.Type == sp.Type && !m.Move.IsStatus) stab = true;
                }
                check(damaging, sp.Name + " always has a damaging move");
                check(stab, sp.Name + " has a same-type attack at max level");

                if (!sp.CanEvolve) continue;
                families++;
                var next = SpeciesDatabase.Get(sp.EvolvesIntoId);
                check(next.Id == sp.EvolvesIntoId, sp.Name + " evolves into a real species");
                check(next.Type == sp.Type, sp.Name + " keeps its type when it evolves");
                check(next.BaseTotal > sp.BaseTotal, sp.Name + " -> " + next.Name + " is an upgrade");
                check(next.Pattern != sp.Pattern, sp.Name + " and " + next.Name + " look different");
            }

            // Every move must be able to describe itself for the battle menu, in one short line.
            var describedMoves = new HashSet<string>();
            foreach (var sp in SpeciesDatabase.All)
                foreach (var e in sp.Learnset)
                {
                    if (!describedMoves.Add(e.MoveId)) continue;
                    var move = MoveDatabase.Get(e.MoveId);
                    string d = move.Describe();
                    check(!string.IsNullOrEmpty(d), move.Name + " has a description");
                    check(d.Length <= 60, move.Name + "'s description fits the message box");
                }

            foreach (var p in PlanetDatabase.All)
            {
                check(p.Seed > 0, p.Name + " has a stable seed");
                foreach (var s in p.Spawns)
                    check(SpeciesDatabase.Get(s.SpeciesId).Id == s.SpeciesId,
                          p.Name + " spawns a real species '" + s.SpeciesId + "'");
            }

            r.Notes.Add("species: " + SpeciesDatabase.Count + " total, " +
                        SpeciesDatabase.CatchableCount + " catchable, " + families + " evolution links");
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Shell fields are where the eggs are. If they do not separate from the ground, the
        /// player cannot see where to walk — which is exactly what happened on the ice world.
        /// </summary>
        static void Ground(Report r, Action<bool, string> check)
        {
            float worst = 1f;
            string worstName = "";
            foreach (var p in PlanetDatabase.All)
            {
                if (p.IsBossWorld) continue;
                Color field = SurfaceMode.AgainstGround(TypeChart.ColorOf(p.Theme), p.Land, 0.55f, 0.30f);
                float delta = Mathf.Abs(SurfaceMode.Luminance(field) - SurfaceMode.Luminance(p.Land));
                check(delta >= 0.12f,
                      p.Name + "'s shell fields stand out from its ground (luminance gap " + delta.ToString("0.000") + ")");
                if (delta < worst) { worst = delta; worstName = p.Name; }
            }
            r.Notes.Add("ground: faintest shell fields are on " + worstName +
                        " at a luminance gap of " + worst.ToString("0.00"));
        }

        static void Palette(Report r, Action<bool, string> check)
        {
            var types = new List<EggType>();
            foreach (EggType t in Enum.GetValues(typeof(EggType))) types.Add(t);

            var tags = new HashSet<string>();
            foreach (var t in types)
            {
                if (t == EggType.Plain) continue;
                check(tags.Add(TypeChart.Abbrev(t)), t + "'s three-letter tag is unique");
            }

            double worst = double.MaxValue;
            string worstPair = "";
            for (int i = 0; i < types.Count; i++)
                for (int j = i + 1; j < types.Count; j++)
                {
                    double d = DeltaE(Deuteranope(TypeChart.ColorOf(types[i])),
                                      Deuteranope(TypeChart.ColorOf(types[j])));
                    if (d < worst) { worst = d; worstPair = types[i] + "/" + types[j]; }
                }
            check(worst >= 12.0, "type colours stay distinguishable under deuteranopia (" +
                                 worstPair + " at " + worst.ToString("0.0") + ")");
            r.Notes.Add("palette: closest pair under deuteranopia is " + worstPair +
                        " at " + worst.ToString("0.0"));
        }

        static Color Deuteranope(Color c)
        {
            float v = 0.330f * c.r + 0.666f * c.g + 0.004f * c.b;
            return new Color(v, v, 0.242f * c.g + 0.758f * c.b);
        }

        static double DeltaE(Color x, Color y)
        {
            var a = Lab(x); var b = Lab(y);
            double dL = a[0] - b[0], da = a[1] - b[1], db = a[2] - b[2];
            return Math.Sqrt(dL * dL + da * da + db * db);
        }

        static double[] Lab(Color c)
        {
            Func<double, double> f = v => v <= 0.04045 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
            double rr = f(c.r), gg = f(c.g), bb = f(c.b);
            double X = (rr * 0.4124564 + gg * 0.3575761 + bb * 0.1804375) / 0.95047;
            double Y = rr * 0.2126729 + gg * 0.7151522 + bb * 0.0721750;
            double Z = (rr * 0.0193339 + gg * 0.1191920 + bb * 0.9503041) / 1.08883;
            Func<double, double> k = t => t > 0.008856 ? Math.Pow(t, 1.0 / 3.0) : (7.787 * t) + 16.0 / 116.0;
            double fx = k(X), fy = k(Y), fz = k(Z);
            return new[] { 116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz) };
        }

        // ------------------------------------------------------------------

        static void Text(Report r, Action<bool, string> check)
        {
            // Estimated wrapped line count against the box each string is displayed in.
            Func<string, float, int, int> lines = (text, width, font) =>
            {
                string plain = System.Text.RegularExpressions.Regex.Replace(text ?? "", "<[^>]+>", "");
                float perLine = Mathf.Max(1f, width / (font * 0.52f));
                int n = 0;
                foreach (var para in plain.Split('\n'))
                    n += Mathf.Max(1, Mathf.CeilToInt(para.Length / perLine));
                return n;
            };
            Func<float, int, int> capacity = (height, font) => Mathf.FloorToInt(height / (font * 1.16f));

            var probeState = new GameState();
            int longest = 0;
            foreach (var npc in StoryDatabase.Npcs)
                for (int b = 0; b < StoryDatabase.Beats.Length; b++)
                {
                    var probe = new StoryState();
                    probe.RestoreFrom(new string[0], b);
                    var script = StoryDatabase.GetDialogue(npc.Id, probe, probeState);
                    if (script == null) continue;
                    foreach (var line in script.Lines)
                    {
                        int need = lines(line.Text, 1380f, 28);
                        check(need <= capacity(190f, 28),
                              "a " + line.Speaker + " line overflows the dialogue box");
                        longest = Mathf.Max(longest, need);
                    }
                }

            foreach (var beat in StoryDatabase.Beats)
            {
                string worst = beat.Chapter + "\n" + beat.Objective +
                               "\nStill needed: 6 more eggs, 4 more types, an egg at level 22";
                check(lines(worst, 524f, 20) <= capacity(150f, 20),
                      "objective '" + beat.Id + "' overflows the HUD panel");
            }

            foreach (var p in PlanetDatabase.All)
                check(lines(p.Tagline, 508f, 22) <= capacity(120f, 22),
                      p.Name + "'s tagline overflows the chart panel");

            // The HUD party strip and counter line are fixed-width and cannot wrap.
            foreach (var sp in SpeciesDatabase.All)
            {
                string slot = HudView.Shorten("Elder " + sp.Name, 14) + "  Lv 30  " +
                              TypeChart.Abbrev(sp.Type) + "  OUT";
                check(lines(slot, 300f, 18) == 1, "party slot for " + sp.Name + " wraps");
            }
            // Two authored lines in a 50px box; each must stay on one rendered line. The nest
            // count is the one that grows without bound, so measure it at three digits.
            check(lines("Cartons 12/12 · Salves 4/4", 524f, 19) == 1, "the HUD supply line wraps");
            check(lines("Nest 240 · Types 8/8 · Record 24/24", 524f, 19) == 1, "the HUD collection line wraps");

            // ---- the catch toast ----
            // The most repeated message in the game. It has to fit the 1000px toast bar for the
            // longest name in the roster, in every combination of Elder and new-to-the-record.
            foreach (var sp in SpeciesDatabase.All)
            {
                string who = "Elder " + sp.Name;
                foreach (bool joined in new[] { true, false })
                {
                    string where = joined ? who + " joined your party." : who + " is waiting at the nest.";
                    string toast = "An Elder! " + where + "  New to the record.";
                    check(lines(toast, 1000f - 40f, 24) == 1,
                          "catch toast fits: \"" + toast + "\"");
                }
            }

            // ---- prose that states a number the code owns ----
            // Some lines read better with the word in them than with a substitution: Nell saying
            // "Twelve is all you get between rests" is better writing than "12 is all you get".
            // The cost is that the line is true until somebody edits the constant. Where the
            // number cannot be spelled from the constant, the constant is held to the line - and
            // the message names the speaker so the fix is obvious.
            {
                check(GameState.BaseMaxCartons == 12,
                      "Nell on Brineholt says \"Twelve is all you get between rests\" - update her line " +
                      "if the base carton stack is not 12 (it is " + GameState.BaseMaxCartons + ")");
                check(GameState.MaxSalves == 4,
                      "Lune on Shimmerfen says \"Four in a stack, same as your cartons\" - update her line " +
                      "if the salve stack is not 4 (it is " + GameState.MaxSalves + ")");
                check(EggInstance.ElderLevelBonus == 3,
                      "Garrow on Cairnhold says an Elder is \"three levels past its neighbours\" - update his " +
                      "line if the bonus is not 3 (it is " + EggInstance.ElderLevelBonus + ")");
                check(GameState.PartySize == 6,
                      "Amy says \"Your six can hold it with me\" - update her line if the party is not 6 " +
                      "(it is " + GameState.PartySize + ")");

                // And the objectives, which do spell themselves, must actually agree with the gate.
                foreach (var beat in StoryDatabase.Beats)
                {
                    if (beat.RequiredEggs > 0)
                        check(beat.Objective.Contains(Words.Spell(beat.RequiredEggs)) ||
                              beat.Objective.Contains(beat.RequiredEggs.ToString()),
                              "beat '" + beat.Id + "' states the egg count it requires");
                    if (beat.RequiredLevel > 0)
                        check(beat.Objective.Contains(beat.RequiredLevel.ToString()),
                              "beat '" + beat.Id + "' states the level it requires");
                    if (beat.RequiredTypes > 0)
                        check(beat.Objective.Contains(Words.Spell(beat.RequiredTypes)) ||
                              beat.Objective.Contains(beat.RequiredTypes.ToString()),
                              "beat '" + beat.Id + "' states the type count it requires");
                }

                // The move descriptions spell their duration rather than stating it, so the only
                // thing worth asserting is that it still reads as a word in a sentence - pinning
                // it to "three" would just be restating the constant, and would fail on a change
                // the descriptions handle correctly by themselves.
                check(Words.Spell(EggInstance.StatusDuration) != EggInstance.StatusDuration.ToString(),
                      "the status duration reads as a word in \"it burns for ... rounds\" (got \"" +
                      Words.Spell(EggInstance.StatusDuration) + "\")");
                check(Words.Count(1, "world") == "1 world" && Words.Count(2, "world") == "2 worlds",
                      "counts pluralise");
            }

            // ---- moving eggs between nest and party ----
            // Until this existed, an egg that went to the nest stayed there: the party was
            // whichever six you caught first, for the whole run.
            {
                var st3 = new GameState();
                while (st3.Party.Count < GameState.PartySize)
                    st3.Collect(EggInstance.Wild("sprouteg", 5));
                st3.Collect(EggInstance.Wild("glacegg", 20));       // over the limit, so into the nest
                check(st3.Party.Count == GameState.PartySize, "the party fills to six");
                check(st3.Nest.Count == 1, "the seventh egg goes to the nest");

                string leadBefore = st3.Party[0].Name;
                check(st3.SwapWithNest(0, 0), "a nest egg can be swapped for the lead");
                check(st3.Party[0].Species.Id == "glacegg", "the nest egg is now leading");
                check(st3.Nest[0].Name == leadBefore, "and the old lead went to the nest");
                check(st3.Party.Count == GameState.PartySize && st3.Nest.Count == 1,
                      "a swap moves eggs rather than creating or losing them");

                // Out-of-range asks must not corrupt anything.
                check(!st3.SwapWithNest(-1, 0) && !st3.SwapWithNest(0, -1), "negative indices refuse");
                check(!st3.SwapWithNest(99, 0) && !st3.SwapWithNest(0, 99), "out-of-range indices refuse");
                check(st3.Party.Count == GameState.PartySize && st3.Nest.Count == 1,
                      "a refused swap changes nothing");

                // With room in the party, a nest egg moves across rather than trading.
                var st4 = new GameState();
                st4.Nest.Add(EggInstance.Wild("cobblet", 8));
                int before4 = st4.Party.Count;
                check(st4.TakeFromNest(0), "a nest egg fills an empty party slot");
                check(st4.Party.Count == before4 + 1 && st4.Nest.Count == 0, "and leaves the nest");
                check(!st4.TakeFromNest(0), "taking from an empty nest refuses");
            }

            // ---- a wild egg has to be visible on the world it lives on ----
            // Species colour comes from the element, ground colour from the planet, and the two
            // were never held against each other: a Frost egg on an ice world is pale on pale by
            // construction. The sprite carries a darkened rim, so either the body or the rim may
            // do the separating - but one of them has to.
            foreach (var w in PlanetDatabase.All)
            {
                float ground = SurfaceMode.Luminance(w.Land);
                for (int i = 0; i < w.Spawns.Length; i++)
                {
                    var sp = SpeciesDatabase.Get(w.Spawns[i].SpeciesId);
                    float byBody = Mathf.Abs(SurfaceMode.Luminance(sp.Body) - ground);
                    float byRim = Mathf.Abs(SurfaceMode.Luminance(sp.Accent * 0.55f) - ground);
                    // Roamers sit on a backing disc coloured against the ground, so that is a
                    // third way to separate and the one that always works.
                    var halo = SurfaceMode.AgainstGround(w.Land, w.Land, 0f, 0.62f);
                    float byHalo = Mathf.Abs(SurfaceMode.Luminance(halo) - ground);
                    check(Mathf.Max(Mathf.Max(byBody, byRim), byHalo) >= 0.20f,
                          sp.Name + " reads against " + w.Name + "'s ground (body " +
                          byBody.ToString("0.00") + ", rim " + byRim.ToString("0.00") +
                          ", halo " + byHalo.ToString("0.00") + ")");
                }
            }

            // ---- the player character must be visible on every world ----
            // Teo is a white suit, which vanished on ice: 0.064 luminance from Glacierim's
            // ground, with four more worlds under 0.25. He now carries a dark outline, so on a
            // bright world the outline separates him and on a dark one the suit does. Either
            // may do the work; at least one of them has to.
            foreach (var w in PlanetDatabase.All)
            {
                float ground = SurfaceMode.Luminance(w.Land);
                float bySuit = Mathf.Abs(SurfaceMode.Luminance(ProcArt.TeoSuit) - ground);
                float byLine = Mathf.Abs(SurfaceMode.Luminance(ProcArt.TeoOutline) - ground);
                check(Mathf.Max(bySuit, byLine) >= 0.25f,
                      "Teo stands out on " + w.Name + " (suit " + bySuit.ToString("0.00") +
                      ", outline " + byLine.ToString("0.00") + ")");
            }

            // ---- surface decoration has to be visible on the ground it sits on ----
            // A fixed colour reads on some worlds and vanishes on others. This has caught three
            // decorations now - shell fields on ice, caches on Nullreach, pools on Brineholt -
            // so every ground-relative decoration is measured against every world that has it.
            {
                foreach (var w in PlanetDatabase.All)
                {
                    float ground = SurfaceMode.Luminance(w.Land);

                    if (w.Theme == EggType.Tidal)
                    {
                        var pool = Color.Lerp(w.Land, w.Ocean, 0.85f);
                        var drawn = Color.Lerp(w.Land, pool, 0.88f);
                        float gap = Mathf.Abs(ground - SurfaceMode.Luminance(drawn));
                        check(gap >= 0.12f, w.Name + "'s pools read against its ground (gap " + gap.ToString("0.000") + ")");
                    }

                    if (w.Theme == EggType.Verdant)
                    {
                        var canopy = Color.Lerp(w.Land, Color.black, 0.34f);
                        float gap = Mathf.Abs(ground - SurfaceMode.Luminance(canopy));
                        check(gap >= 0.12f, w.Name + "'s canopies read against its ground (gap " + gap.ToString("0.000") + ")");
                    }

                    if (w.Theme == EggType.Stone)
                    {
                        var boulder = Color.Lerp(w.Ocean, Color.black, 0.22f);
                        float gap = Mathf.Abs(ground - SurfaceMode.Luminance(boulder));
                        check(gap >= 0.12f, w.Name + "'s boulders read against its ground (gap " + gap.ToString("0.000") + ")");
                    }

                    if (w.Theme == EggType.Void)
                    {
                        var rift = Color.Lerp(w.Land, new Color(0.03f, 0.02f, 0.08f), 0.88f);
                        float gap = Mathf.Abs(ground - SurfaceMode.Luminance(rift));
                        check(gap >= 0.12f, w.Name + "'s rifts read against its ground (gap " + gap.ToString("0.000") + ")");
                    }

                    if (w.Theme == EggType.Molten)
                    {
                        var cinder = Color.Lerp(w.Land, new Color(0.16f, 0.11f, 0.10f), 0.9f);
                        float gap = Mathf.Abs(ground - SurfaceMode.Luminance(cinder));
                        check(gap >= 0.12f, w.Name + "'s cinders read against its ground (gap " + gap.ToString("0.000") + ")");
                    }
                }
            }

            // ---- hidden caches ----
            {
                var st2 = new GameState();
                check(st2.MaxCartons == GameState.BaseMaxCartons,
                      "a fresh run carries the base carton stack (" + st2.MaxCartons + ")");

                foreach (var kv in PlanetDatabase.CacheWorlds)
                {
                    check(PlanetDatabase.Exists(kv.Key), "cache world " + kv.Key + " is a real world");
                    var world = PlanetDatabase.Get(kv.Key);
                    check(!world.IsBossWorld, kv.Key + " is not the boss world");
                    check(kv.Value > 0, kv.Key + "'s cache is worth something");
                }

                // Digging all of them up must stay a modest upgrade, not a new game.
                foreach (var kv in PlanetDatabase.CacheWorlds) st2.Caches.Add(kv.Key);
                int full = st2.MaxCartons;
                check(full > GameState.BaseMaxCartons, "caches actually raise the cap (" + full + ")");
                check(full <= GameState.BaseMaxCartons + 6,
                      "the cap stays in range even with every cache found (" + full + ")");

                // The strip is a fixed 524px and the number of digits can grow.
                // The footer adapts to what is possible, so every variant has to fit the strip.
                foreach (var variant in new[]
                {
                    "up/down move  ·  Tab closes",
                    "up/down move  ·  1-6 leads  ·  Tab closes",
                    "left/right pick a column  ·  up/down move  ·  Enter swaps a nest egg in  ·  Tab closes",
                    "left/right pick a column  ·  up/down move  ·  Enter swaps a nest egg in  ·  1-6 leads  ·  Tab closes",
                })
                    check(lines(variant, 1200f, 20) == 1,
                          "collection footer fits: \"" + variant + "\"");

                // The empty-nest explanation is two authored lines in a 780px column.
                foreach (var line in new[]
                {
                    "Empty. Once your party is full, anything else you",
                    "catch waits here — and you can trade it back in.",
                })
                    check(lines(line, 720f, 20) == 1, "empty-nest line fits: \"" + line + "\"");
                check(lines("Cartons " + full + "/" + full + " · Salves 4/4", 524f, 19) == 1,
                      "the supply line still fits at full capacity");

                // A cache the player cannot pick out of the ground is not hidden, it is absent.
                // The same failure that once buried the shell fields in ice: a fixed darkening
                // reads on a bright world and disappears on a dark one.
                foreach (var kv in PlanetDatabase.CacheWorlds)
                {
                    var w = PlanetDatabase.Get(kv.Key);
                    var mound = SurfaceMode.AgainstGround(new Color(0.32f, 0.24f, 0.15f), w.Land, 0.55f, 0.46f);
                    float gap = Mathf.Abs(SurfaceMode.Luminance(w.Land) - SurfaceMode.Luminance(mound));
                    check(gap >= 0.12f,
                          w.Name + "'s cache stands out from its ground (gap " + gap.ToString("0.000") + ")");
                }

                // Spread: a cache on every world would make them scenery.
                check(PlanetDatabase.CacheWorlds.Count * 4 <= PlanetDatabase.All.Count,
                      "caches stay rare relative to the number of worlds (" +
                      PlanetDatabase.CacheWorlds.Count + " of " + PlanetDatabase.All.Count + ")");
            }

            // ---- move buttons ----
            // Name, accuracy and rider share one 330px line; the sub-line carries the rest.
            foreach (var mv in MoveDatabase.All)
            {
                string acc = mv.Accuracy >= 100 ? "" : "  " + mv.Accuracy + "%";
                var rid = BattleCalc.RiderOf(mv.Effect);
                string rider = rid == EggStatus.Scorched ? "LEAVES SCORCHED"
                             : rid == EggStatus.Chilled ? "LEAVES CHILLED"
                             : rid == EggStatus.Dazed ? "LEAVES DAZED" : "";
                check(lines(mv.Name + acc, 330f - 44f, 24) == 1,
                      "move button top line fits: \"" + mv.Name + acc + "\"");

                string sub = TypeChart.Name(mv.Type) + (mv.IsStatus ? " · STATUS" : " · PWR " + mv.Power) +
                             " · PP " + mv.MaxPP + "/" + mv.MaxPP;
                check(lines(sub, 330f - 44f, 17) == 1, "move button sub-line fits: \"" + sub + "\"");
                if (rider.Length > 0)
                    check(lines(rider, 330f - 44f, 17) == 1, "move button rider line fits: \"" + rider + "\"");

                // Name at 24 plus one or two lines at 17, inside a 74px button.
                float used = 24f * 1.16f + 17f * 1.16f * (rider.Length > 0 ? 2 : 1);
                check(used <= 74f,
                      mv.Name + "'s button is tall enough (" + used.ToString("0") + " of 74px)");
            }

            // ---- lingering conditions ----
            {
                // Each condition is dealt out by one element and shrugged off by that element.
                var pairs = new[]
                {
                    new object[] { EggStatus.Scorched, EggType.Molten, "yolkano" },
                    new object[] { EggStatus.Chilled,  EggType.Frost,  "chillet" },
                    new object[] { EggStatus.Dazed,    EggType.Volt,   "yolty" },
                };
                foreach (var pair in pairs)
                {
                    var status = (EggStatus)pair[0];
                    var immuneType = (EggType)pair[1];
                    var immune = EggInstance.Wild((string)pair[2], 20);
                    check(immune.Type == immuneType, (string)pair[2] + " is " + immuneType);
                    check(!immune.CanCatch(status),
                          immuneType + " eggs shrug off " + status);

                    var victim = EggInstance.Wild("sprouteg", 20);
                    check(victim.CanCatch(status), "a Verdant egg can catch " + status);
                    victim.Afflict(status);
                    check(victim.Status == status, status + " sticks");
                    check(!victim.CanCatch(EggStatus.Scorched) && !victim.CanCatch(EggStatus.Chilled),
                          "an afflicted egg cannot take a second condition");

                    // It has to wear off, or a long fight is decided by it rather than shaped by it.
                    int rounds = 0;
                    while (victim.Status != EggStatus.None && rounds < 20) { victim.TickStatus(); rounds++; }
                    check(rounds == EggInstance.StatusDuration,
                          status + " lasts exactly " + EggInstance.StatusDuration + " rounds (took " + rounds + ")");
                }

                // Burn is a fixed fraction, and never rounds away to nothing.
                foreach (var sp in SpeciesDatabase.All)
                {
                    var e = EggInstance.Wild(sp.Id, 5);
                    e.Afflict(EggStatus.Scorched);
                    check(e.StatusTickDamage() >= 1, sp.Name + " burns for at least 1 even at level 5");
                    check(e.StatusTickDamage() <= e.MaxHP / 8,
                          sp.Name + "'s burn is not more than an eighth a round");
                    check(EggInstance.StatusDuration * e.StatusTickDamage() < e.MaxHP,
                          sp.Name + " cannot be killed by one application of scorch alone");
                }

                // Chill is the only condition that touches a stat, and it must actually bite.
                var brisk = EggInstance.Wild("sprouteg", 20);
                int before = brisk.Spd;
                brisk.Afflict(EggStatus.Chilled);
                check(brisk.Spd < before, "chill actually slows an egg (" + before + " -> " + brisk.Spd + ")");
                brisk.ClearStatus();
                check(brisk.Spd == before, "and it comes back when the chill wears off");

                // A move's rider must match its own element, or the immunity rule reads as random.
                foreach (var mv in MoveDatabase.All)
                {
                    var rider = BattleCalc.RiderOf(mv.Effect);
                    if (rider == EggStatus.None) continue;
                    var expect = rider == EggStatus.Scorched ? EggType.Molten
                               : rider == EggStatus.Chilled ? EggType.Frost : EggType.Volt;
                    check(mv.Type == expect,
                          mv.Name + " inflicts " + rider + ", so it should be " + expect + " (is " + mv.Type + ")");
                }
            }

            // ---- the title screen: the first prose anyone reads ----
            // 1500x460 at font 24, and the controls list is the widest thing in the game.
            {
                check(lines(UiCopy.TitleBody, 1500f, 24) <= capacity(460f, 24),
                      "the title body fits its panel (" + lines(UiCopy.TitleBody, 1500f, 24) +
                      " of " + capacity(460f, 24) + " lines)");
                foreach (var line in UiCopy.TitleBody.Split('\n'))
                    check(lines(line, 1500f, 24) <= 1,
                          "title line stays on one row: \"" + line + "\"");

                check(lines(UiCopy.Subtitle, 1200f, 34) == 1, "the subtitle fits");
                check(lines(UiCopy.Title, 1200f, 96) == 1, "the title fits");
                check(lines(UiCopy.BeginHint, 1300f, 30) == 1, "the begin hint fits");

                string ending = UiCopy.VictoryBody + UiCopy.VictoryTally(120, 24, 24, 8);
                check(lines(ending, 1300f, 28) <= capacity(300f, 28),
                      "the ending card fits (" + lines(ending, 1300f, 28) + " of " +
                      capacity(300f, 28) + " lines)");
                check(lines(UiCopy.VictoryHeading, 1400f, 82) == 1, "the ending heading fits");
            }

            // ---- every catchable egg must actually live somewhere ----
            // A species with a catchable rate and no spawn entry makes the record impossible to
            // finish, and nothing else in the game would ever say so.
            foreach (var sp in SpeciesDatabase.All)
            {
                var homes = PlanetDatabase.WorldsWith(sp.Id);
                if (sp.CatchRate >= 20)
                    check(homes.Count > 0, sp.Name + " is catchable but spawns on no world");

                // The dex lists their names in a 450px box; the longest roster must still fit.
                if (homes.Count > 0)
                {
                    var names = new List<string>();
                    foreach (var h in homes) names.Add(h.Name);
                    check(lines("FOUND ON\n" + string.Join(" · ", names.ToArray()), 450f, 19) <= 4,
                          sp.Name + "'s found-on list fits the dex box (" +
                          lines(string.Join(" · ", names.ToArray()), 450f, 19) + " lines)");
                }
            }

            // ---- the collection screen's three columns ----
            // Each column has a fixed height and content that grows with the game. The dex
            // lists every species with no window at all, so it is the one that breaks first.
            {
                const float ColH = 780f;
                int nestLines = 1 + GameState.PartySize + 1 + 1 + 20 + 1;   // party, headers, nest window
                check(nestLines <= capacity(ColH, 20),
                      "collection: the party and nest column fits (" + nestLines + " of " +
                      capacity(ColH, 20) + " lines)");

                int dexLines = 2 + SpeciesDatabase.Count;
                check(dexLines <= capacity(ColH, 19),
                      "collection: the field record lists every species without overrunning (" +
                      dexLines + " of " + capacity(ColH, 19) + " lines)");

                // The detail box is 280x160 and holds a name, a type line, a trait and its blurb.
                foreach (var sp in SpeciesDatabase.All)
                {
                    var trait = TypeChart.TraitOf(sp.Type);
                    int used = 2                                            // #NN Name, at size 26
                             + lines(TypeChart.Name(sp.Type) + "   " + sp.Pattern + " shell", 280f, 19)
                             + 1                                            // blank
                             + lines(TypeChart.TraitName(trait), 280f, 19)
                             + lines(TypeChart.TraitBlurb(trait), 280f, 19);
                    check(used <= capacity(160f, 19),
                          "collection: " + sp.Name + "'s detail box fits (" + used + " of " +
                          capacity(160f, 19) + " lines)");
                }
            }

            // The foe card's record note sits in a 500px box on one line.
            foreach (string note in new[] { "New species — not in your record", "Already in your record" })
                check(lines(note, 500f, 20) == 1, "foe record note \"" + note + "\" wraps");

            // The five battle actions share a 330px button each.
            foreach (string action in new[] { "FIGHT", "CARTON (12)", "SALVE (4)", "SWAP", "RUN" })
                check(lines(action, 330f, 26) == 1, "battle action \"" + action + "\" wraps its button");

            // Battle cards: name line and the trait/stage line each have to stay on one line,
            // worst case being an elder with every stat stage showing.
            foreach (var sp in SpeciesDatabase.All)
            {
                check(lines("Elder " + sp.Name + "  Lv 30", 440f, 30) == 1,
                      sp.Name + "'s battle card name line wraps");
                check(lines(TypeChart.TraitName(TypeChart.TraitOf(sp.Type)) + "  ATK000 DEF000 SPD000", 612f, 18) == 1,
                      sp.Name + "'s battle card trait line wraps");
            }

            // Party swap rows and star-map labels are the remaining fixed-width text sites.
            foreach (var sp in SpeciesDatabase.All)
            {
                var egg = EggInstance.WildElder(sp.Id, EggInstance.MaxLevel - 3);
                string row = "  *  " + egg.Name + "   Lv " + egg.Level + " · " +
                             TypeChart.Name(egg.Type) + " · " + egg.MaxHP + "/" + egg.MaxHP + " HP";
                check(lines(row, 800f, 24) == 1, "party swap row for " + sp.Name + " wraps");
            }
            foreach (var p in PlanetDatabase.All)
            {
                check(lines(p.Name, 420f, 26) == 1, p.Name + "'s map label wraps");
                check(lines("Lv " + p.MinLevel + "-" + p.MaxLevel + " · " + TypeChart.Name(p.Theme), 420f, 20) == 1,
                      p.Name + "'s map subtitle wraps");
            }

            r.Notes.Add("text: longest dialogue line uses " + longest + " of " +
                        capacity(190f, 28) + " available lines");
        }
    }
}
