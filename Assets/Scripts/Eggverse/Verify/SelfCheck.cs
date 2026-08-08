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
        /// <summary>
        /// An assertion, and where it was written.
        ///
        /// The line number is filled in by the compiler at the call site, which is the only way
        /// to answer the question that matters about a suite this size: not "did anything fail"
        /// but "did every check actually run". A check inside a guard that is never true passes
        /// forever and proves nothing, and there is no way to see one by reading.
        /// </summary>
        public delegate void Check(bool ok, string what,
                                   [System.Runtime.CompilerServices.CallerLineNumber] int line = 0);

        public class Report
        {
            public int Passed;
            public readonly List<string> Failures = new List<string>();
            public readonly List<string> Notes = new List<string>();

            /// <summary>Every line of SelfCheck.cs that actually asserted something.</summary>
            public readonly HashSet<int> LinesRun = new HashSet<int>();

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
            Check check = (ok, what, line) =>
            {
                r.LinesRun.Add(line);
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

        static void Story(Report r, Check check)
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
                // tripwire: the walk advancing is the point; this only fires if it stops.
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

        /// <summary>
        /// Whether a line says every egg on the speaker's own world is one element. Both halves
        /// matter: "every egg" alone is usually about eggs in general, and a locality phrase
        /// alone is not a claim about elements.
        /// </summary>
        static bool ClaimsWholeWorld(string text, EggType t)
        {
            string low = (text ?? "").ToLowerInvariant();
            if (!low.Contains("every egg") && !low.Contains("all the eggs")) return false;
            if (!low.Contains("this rock") && !low.Contains("this world") &&
                !low.Contains("this place") && !low.Contains("round here")) return false;
            return low.Contains(TypeChart.Name(t).ToLowerInvariant());
        }

        static void Chart(Report r, Check check)
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

        static void Progression(Report r, Check check)
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
                            if (SpeciesDatabase.Get(s.SpeciesId).CatchRate >= SpeciesDatabase.CatchableThreshold) reachable.Add(s.SpeciesId);

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
                        if (sp.Type == t && sp.CatchRate >= SpeciesDatabase.CatchableThreshold) available = true;
                    }
                check(available, t + " eggs are catchable somewhere");
            }
        }

        // ------------------------------------------------------------------

        static void Species(Report r, Check check)
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
        static void Ground(Report r, Check check)
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

        static void Palette(Report r, Check check)
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

        static void Text(Report r, Check check)
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

            // The first species of an element, for probing a matchup without hardcoding an id.
            Func<EggType, string> FirstSpeciesOfType = t =>
            {
                foreach (var sp in SpeciesDatabase.All) if (sp.Type == t) return sp.Id;
                return SpeciesDatabase.All[0].Id;
            };

            var probeState = new GameState();
            int longest = 0;
            {
                // EveryLine, not the NPC roster. This walked StoryDatabase.Npcs, and Amy is not
                // an NPC - she is reached through AmyIntro from the surface - so the boss's
                // dialogue, the climax of the game, had never been measured against the box it
                // is delivered in.
                bool sawAmy = false;
                foreach (var line in StoryDatabase.EveryLine())
                {
                    if (line.Speaker == "Amy") sawAmy = true;

                    // The view's own numbers. This read 1380 and 190 - transcribed, and the body
                    // box had been cut to 180 to stop it overlapping the hint.
                    int need = lines(line.Text, DialogueView.BodyWidth, DialogueView.BodyFont);
                    check(need <= capacity(DialogueView.BodyHeight, DialogueView.BodyFont),
                          "a " + line.Speaker + " line overflows the dialogue box");
                    longest = Mathf.Max(longest, need);
                }
                check(sawAmy, "the dialogue walk reaches Amy, who is not in the NPC roster");

                // The box is sized to what is said in it, in both directions. Too small and a
                // line clips; too large and every line in the game floats above dead air, which
                // is what five rows over a two-row worst case actually looked like once it was
                // drawn. One row of headroom, no more than two.
                int room = capacity(DialogueView.BodyHeight, DialogueView.BodyFont);
                check(room >= longest + 1,
                      "the dialogue box has a row of headroom (" + room + " for " + longest + ")");
                check(room <= longest + 2,
                      "the dialogue box is not mostly empty (" + room + " for " + longest + ")");
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
                check(lines(slot, 360f, 18) == 1, "party slot for " + sp.Name + " wraps");
            }
            // Two authored lines in a 50px box; each must stay on one rendered line. The nest
            // count is the one that grows without bound, so measure it at three digits.
            check(lines("Cartons 12/12 · Salves 4/4", 524f, 19) == 1, "the HUD supply line wraps");
            check(lines("Nest 240 · Types 8/8 · Record 24/24", 524f, 19) == 1, "the HUD collection line wraps");

            // ---- the pause menu's controls reference ----
            // Two places document the controls: the title screen in prose, and the pause menu in
            // a grid. They are read months apart and edited independently, so the check is that
            // they mention the same keys - not that they read the same, which they should not.
            {
                string pause = string.Join("  ", UiCopy.PauseControls);
                foreach (var line in UiCopy.PauseControls)
                    check(lines(line, 700f, 20) == 1, "pause controls line fits: \"" + line + "\"");

                foreach (var key in new[] { "WASD", "E", "Q", "M", "Tab", "N", "0" })
                {
                    bool onTitle = System.Text.RegularExpressions.Regex.IsMatch(
                        UiCopy.TitleBody, @"\b" + System.Text.RegularExpressions.Regex.Escape(key) + @"\b");
                    bool onPause = System.Text.RegularExpressions.Regex.IsMatch(
                        pause, @"\b" + System.Text.RegularExpressions.Regex.Escape(key) + @"\b");
                    check(onTitle == onPause,
                          "the title screen and the pause menu agree about the '" + key + "' key (title " +
                          (onTitle ? "yes" : "no") + ", pause " + (onPause ? "yes" : "no") + ")");
                }

                // The box holds both pages without resizing, so it has to fit the longer of them.
                // Main is four rows plus the controls reference; Settings is six rows without it.
                const float BoxH = 620f, FirstRow = 118f, Step = 62f, RowH = 52f;
                const float RuleAt = 390f, ControlsAt = 424f;
                const int MainRows = 4;      // resume, settings, save, quit
                const int SettingsRows = 6;  // sound, music, effects, motion, text speed, back

                float mainEnd = FirstRow + (MainRows - 1) * Step + RowH;
                float setEnd = FirstRow + (SettingsRows - 1) * Step + RowH;
                float controlsEnd = ControlsAt + 32f + 28f;
                float footerTop = BoxH - 42f - 60f;

                check(mainEnd < RuleAt, "the rule clears the main page's rows");
                check(controlsEnd < footerTop, "the controls reference clears the footer");
                check(setEnd < footerTop,
                      "the settings page's rows clear the footer (" + setEnd + " of " + footerTop + ")");
                check(BoxH <= 1080f - 80f, "the pause box fits the screen with margin");

                // Splitting the menu was to stop the box chasing settings down the screen. If a
                // future setting pushes the longer page past the box again, this is the line that
                // should complain rather than the box quietly growing.
                check(SettingsRows <= 7,
                      "the settings page still fits without growing the box (" + SettingsRows + " rows)");
            }

            // ---- the egg detail panel ----
            // Shows one of your own eggs in the 280x160 header and the 450x600 body. Worst case
            // is a nicknamed Elder at max level with four long moves.
            {
                foreach (var sp in SpeciesDatabase.All)
                {
                    var egg = EggInstance.Wild(sp.Id, 30);
                    // The real maximum the name entry screen allows, not a shorter stand-in.
                    egg.Nickname = new string('W', NameEntryView.MaxLength);
                    string header = egg.Name + "  " + sp.Name + "\n" +
                                    TypeChart.Name(egg.Type) + "   Lv " + egg.Level + "   ELDER\n\n" +
                                    TypeChart.TraitName(egg.Trait) + "\n" + TypeChart.TraitBlurb(egg.Trait);
                    check(lines(header, 280f, 19) <= capacity(160f, 19),
                          sp.Name + "'s egg header fits (" + lines(header, 280f, 19) + " of " +
                          capacity(160f, 19) + " lines)");

                    var body = new StringBuilder();
                    if (sp.CanEvolve)
                    {
                        var nx = SpeciesDatabase.Get(sp.EvolvesIntoId);
                        body.Append("AHEAD\nBecomes ").Append(nx != null ? nx.Name : "?")
                            .Append(" at level ").Append(sp.EvolveLevel).Append(".\n\n");
                    }
                    body.Append("CONDITION\nHP  ").Append(egg.CurrentHP).Append(" / ").Append(egg.MaxHP)
                        .Append("\nXP  fully grown\n\nSTATS\nATK  ").Append(egg.Atk)
                        .Append("     DEF  ").Append(egg.Def).Append("     SPD  ").Append(egg.Spd)
                        .Append("\n\nMOVES\n");
                    foreach (var slot in egg.Moves)
                        body.Append(slot.Move.Name).Append("  pwr ").Append(slot.Move.Power)
                            .Append("  ").Append(slot.Move.MaxPP).Append("/").Append(slot.Move.MaxPP)
                            .Append(" pp\n");
                    check(lines(body.ToString(), 450f, 19) <= capacity(600f, 19),
                          sp.Name + "'s egg body fits (" + lines(body.ToString(), 450f, 19) + " of " +
                          capacity(600f, 19) + " lines)");
                }
            }

            // ---- what a level-up reports ----
            // The payoff for every fight in the game. It has to fit the message box, and it must
            // never come up blank - HP grows on every level by construction, so a silent level-up
            // would mean the reporting broke rather than that nothing improved.
            foreach (var sp in SpeciesDatabase.All)
            {
                var egg = EggInstance.Wild(sp.Id, 5);
                var log = new List<string>();
                egg.GainXp(100000, log);                       // run it all the way up

                int levelUps = 0, gainLines = 0;
                foreach (var line in log)
                {
                    if (line.Contains("grew to level")) levelUps++;
                    if (line.StartsWith("HP +")) gainLines++;
                    check(line.Length <= 60, sp.Name + "'s level-up line fits the message box: \"" + line + "\"");
                }
                check(levelUps > 0, sp.Name + " levels up at all");
                check(gainLines == levelUps,
                      sp.Name + " reports gains on every level (" + gainLines + " of " + levelUps + ")");
            }

            // ---- toasts, at the longest thing each can say ----
            // The bar is 1000x76: 960 usable at font 24, which is two lines. A first arrival is
            // the longest toast in the game and nothing was measuring it.
            {
                const float W = 1000f - 40f, H = 76f;
                foreach (var w in PlanetDatabase.All)
                {
                    string arrival = "Charted " + w.Name + ". " + w.Tagline;
                    check(lines(arrival, W, 24) <= capacity(H, 24),
                          "arrival toast fits for " + w.Name + " (" + lines(arrival, W, 24) +
                          " of " + capacity(H, 24) + " lines)");

                    string ret = w.Name + ". " + Words.Count(9, "egg") + " here you have not recorded yet.";
                    check(lines(ret, W, 24) <= capacity(H, 24), "return toast fits for " + w.Name);
                    string done = w.Name + ". Every egg here is already in your record.";
                    check(lines(done, W, 24) <= capacity(H, 24), "completed-world toast fits for " + w.Name);
                }
            }

            // ---- what the game says after a wild win ----
            // The single most repeated line in the game. Every variant is a message-box line, so
            // every variant has to fit at the longest name a player can be carrying.
            {
                string longestName = "";
                foreach (var sp in SpeciesDatabase.All)
                    if (("Elder " + sp.Name).Length > longestName.Length) longestName = "Elder " + sp.Name;

                foreach (var line in new[]
                {
                    "An Elder, no less. " + longestName + " stands over it.",
                    "Not a scratch on " + longestName + ".",
                    "One hit. " + longestName + " barely looked up.",
                    "That was close. " + longestName + " is still standing, just.",
                    "A long one. Both of them are breathing hard.",
                    "You won the scrap.",
                })
                    check(lines(line, 1016f, 30) == 1,
                          "wild victory line fits the message box: \"" + line + "\"");
            }

            // ---- what the Nest Station says ----
            // Every variant is a toast, so every variant has to fit the 1000px bar - including
            // the plural forms, which read differently at one.
            {
                foreach (var line in new[]
                {
                    Words.Count(1, "egg") + " back on its feet. Shells mended, supplies restocked.",
                    Words.Count(6, "egg") + " back on their feet. Shells mended, supplies restocked.",
                    "Shells mended, supplies restocked.",
                    "Supplies restocked. Nothing else needed doing.",
                    "Nothing needed doing. The pad is warm anyway.",
                    "Everything is already whole. The keeper nods at you.",
                    "Your eggs settle in, then look at you expectantly.",
                    "You sit a while. The station hums along with the shells.",
                })
                    check(lines(line, 1000f - 40f, 24) == 1, "rest line fits the toast bar: \"" + line + "\"");

                // The singular and plural forms must actually differ, or the pluralisation is
                // decorative and "1 eggs back on their feet" is one edit away.
                check(Words.Count(1, "egg").StartsWith("1 egg") && !Words.Count(1, "egg").Contains("eggs"),
                      "one egg reads as \"1 egg\"");
                check(Words.Count(2, "egg") == "2 eggs", "two eggs read as \"2 eggs\"");
            }

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
                check(UiCopy.TitleBody.Contains(PlanetDatabase.Home.Name),
                      "the title screen's \"a junior hatcher on ...\" line does not name " +
                      PlanetDatabase.Home.Name + ", which is the world the player actually starts on");

                check(GameState.PartySize == 6,
                      "Amy says \"Your six can hold it with me\" - update her line if the party is not 6 " +
                      "(it is " + GameState.PartySize + ")");

                // And the objectives, which do spell themselves, must actually agree with the gate.
                // Counted, not assumed. Each of these only fires on beats that have the
                // requirement at all, so removing every requirement would leave them silently
                // passing - which is how three checks in this file came to assert nothing.
                int statedEggs = 0, statedLevel = 0, statedTypes = 0;
                foreach (var beat in StoryDatabase.Beats)
                {
                    if (beat.RequiredEggs > 0) statedEggs++;
                    if (beat.RequiredLevel > 0) statedLevel++;
                    if (beat.RequiredTypes > 0) statedTypes++;

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

                check(statedEggs > 0 && statedLevel > 0 && statedTypes > 0,
                      "the beat-requirement checks actually ran (" + statedEggs + " egg, " +
                      statedLevel + " level, " + statedTypes + " type gates)");

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

            // ---- the "Still needed" line ----
            // Every gate flag has to have a phrase, or a beat waiting on it says nothing at all
            // about what is left - which is precisely when a player is looking.
            {
                foreach (var beat in StoryDatabase.Beats)
                {
                    if (beat.RequiredFlags == null) continue;
                    foreach (var flag in beat.RequiredFlags)
                        check(StoryDatabase.LabelForFlag(flag) != null,
                              "beat '" + beat.Id + "' waits on '" + flag + "', which has no phrase " +
                              "for the Still needed line");
                }

                // The toast shown on loading a save. It prefers the outstanding list, which is
                // shorter as well as more useful, but must still fit when it falls back to the
                // objective for a beat with nothing countable left.
                for (int i = 0; i < StoryDatabase.Beats.Length; i++)
                {
                    var walk3 = new StoryState();
                    walk3.RestoreFrom(new string[0], i);
                    string outstanding = walk3.CurrentBlockerText(new GameState());
                    string toast = outstanding != null
                        ? "Welcome back. Still needed: " + outstanding
                        : "Welcome back. " + StoryDatabase.Beats[i].Objective;
                    check(lines(toast, 1000f - 40f, 24) <= capacity(76f, 24),
                          "welcome-back toast fits at beat '" + StoryDatabase.Beats[i].Id +
                          "' (" + lines(toast, 1000f - 40f, 24) + " of " + capacity(76f, 24) + " lines)");
                }

                // The chart says the same thing when it refuses to plot a course, in a 900px
                // detail panel. That panel carries the tagline and spawn list too, so the sealed
                // message has to leave room for them.
                for (int i = 0; i < StoryDatabase.Beats.Length; i++)
                {
                    var walk2 = new StoryState();
                    walk2.RestoreFrom(new string[0], i);
                    foreach (Sector sec in new[] { Sector.LongDrift, Sector.ShatteredBelt, Sector.Amaranth })
                    {
                        string sealed_ = walk2.SectorBlockerText(sec, new GameState());
                        if (sealed_ == null) continue;
                        check(lines(sealed_, 848f, 22) <= 8,
                              "sealed-route text stays short at beat '" + StoryDatabase.Beats[i].Id +
                              "' (" + lines(sealed_, 848f, 22) + " lines)");
                    }
                }

                // It sits under the objective in a 524x150 box at font 20, and the worst case is
                // a beat waiting on two people at once.
                // Two states, because the blocker is longest in neither of the obvious ones.
                // A fresh save is missing seven elements, which is too many to list, so it gets
                // a bare number - and this check used only a fresh save, which meant the longest
                // form of the text it exists to measure was never measured at all.
                var fresh10 = new GameState();
                var threeTypes = new GameState(false);
                foreach (var id in new[] { "sprouteg", "yolkano", "tidepoach" })
                    threeTypes.Party.Add(EggInstance.Wild(id, 10));

                int panelsMeasured = 0;
                for (int i = 0; i < StoryDatabase.Beats.Length; i++)
                    foreach (var st in new[] { fresh10, threeTypes })
                    {
                        var walk = new StoryState();
                        walk.RestoreFrom(new string[0], i);
                        string blocker = walk.CurrentBlockerText(st);
                        if (blocker == null) continue;

                        panelsMeasured++;
                        string full = StoryDatabase.Beats[i].Chapter + "\n" +
                                      StoryDatabase.Beats[i].Objective + "\nStill needed: " + blocker;
                        check(lines(full, 524f, 20) <= capacity(150f, 20),
                              "beat '" + StoryDatabase.Beats[i].Id + "' objective and blocker fit the panel (" +
                              lines(full, 524f, 20) + " of " + capacity(150f, 20) + " lines)");
                    }

                check(panelsMeasured > 0,
                      "some beat does block, so the objective panel was measured (" +
                      panelsMeasured + " states)");

                // And the widest the hint itself can get: five element names at once.
                {
                    var five = new GameState(false);
                    foreach (var id in new[] { "sprouteg", "yolkano", "tidepoach" })
                        five.Party.Add(EggInstance.Wild(id, 10));
                    check(five.TypesMissing.Count == 5,
                          "a three-type party is missing five elements (" + five.TypesMissing.Count + ")");

                    var gate = new StoryState();
                    gate.RestoreFrom(new string[0], StoryDatabase.Beats.Length - 3);
                    string widest = gate.CurrentBlockerText(five);
                    check(widest != null && widest.Contains("nothing yet from"),
                          "the gate beat does block a three-type party, and names the elements: " + widest);
                    // No separate width assertion on the hint alone. Planting a hint three times
                    // longer did not overflow anything, because the panel holds six lines and a
                    // blocker is three - so the check would have been another one that cannot
                    // fail. What does the measuring is the combined objective-and-blocker check
                    // above, which now runs against a three-type party as well as a fresh save.
                }
            }

            // ---- record milestones ----
            // Ori has a reward at each threshold but only hands it over face to face, so the
            // moment of crossing gets its own line. Both places now read the same constants.
            {
                foreach (var line in new[]
                {
                    Words.SpellCapitalised(StoryDatabase.RecordNoticeFirst) +
                        " species recorded. Ori would want to see that.",
                    Words.SpellCapitalised(StoryDatabase.RecordNoticeSecond) +
                        " species recorded. Ori would want to see that.",
                    "The record is complete. All " + SpeciesDatabase.CatchableCount +
                        " of them. Ori will not believe it.",
                })
                    check(lines(line, 1000f - 40f, 24) <= capacity(76f, 24),
                          "milestone toast fits: \"" + line + "\"");

                // The thresholds have to be reachable and in order, or a milestone never fires.
                check(StoryDatabase.RecordNoticeFirst < StoryDatabase.RecordNoticeSecond,
                      "the first record milestone comes before the second");
                check(StoryDatabase.RecordNoticeSecond < SpeciesDatabase.CatchableCount,
                      "the second milestone comes before a complete record (" +
                      StoryDatabase.RecordNoticeSecond + " of " + SpeciesDatabase.CatchableCount + ")");
                check(Words.SpellCapitalised(StoryDatabase.RecordNoticeFirst) != 
                      StoryDatabase.RecordNoticeFirst.ToString(),
                      "the first milestone reads as a word in Ori's line");
            }

            // ---- text speed ----
            // Every setting has to name itself for the pause row, produce a sane multiplier, and
            // survive a save. "Instant" is the one worth checking hardest: it is a zero, and a
            // zero used as a divisor or a rate is how a reveal loop spins forever.
            {
                var st8 = new GameState();
                check(st8.TextSpeed == 1, "text speed starts at normal");

                for (int i = 0; i < GameState.TextSpeedCount; i++)
                {
                    st8.TextSpeed = i;
                    check(!string.IsNullOrEmpty(st8.TextSpeedName), "text speed " + i + " has a name");
                    check(lines("Text speed  " + st8.TextSpeedName, 700f, 28) == 1,
                          "the text speed row fits: \"Text speed  " + st8.TextSpeedName + "\"");
                    check(st8.RevealScale >= 0f, "text speed " + i + " has a non-negative rate");
                }

                st8.TextSpeed = GameState.TextSpeedCount - 1;
                check(st8.RevealScale == 0f, "the fastest setting means no wait at all");
                check(st8.TextSpeedName == "instant", "and it is called instant");

                // Out-of-range values from a stale or edited save must not index off the end.
                var data2 = new SaveData { version = 1, textSpeed = 99, cartons = 12, salves = 4 };
                GameState back3; StoryState st9; string pl3; float s3;
                SaveSystem.Restore(data2, out back3, out st9, out pl3, out s3);
                check(back3 != null && back3.TextSpeed < GameState.TextSpeedCount,
                      "an out-of-range text speed is clamped on load (got " +
                      (back3 == null ? "null" : back3.TextSpeed.ToString()) + ")");
                check(back3 != null && !string.IsNullOrEmpty(back3.TextSpeedName),
                      "and still names itself afterwards");
            }

            // ---- screen motion can be turned off ----
            // Hits shake the battle scene and landing flashes the display. Both are good feel and
            // both are a problem for anyone sensitive to motion, so both answer to one setting -
            // and it has to survive a save, or it is an option the player sets once per session.
            {
                var st5 = new GameState();
                check(st5.ScreenMotion, "screen motion starts on");

                st5.ScreenMotion = false;
                var data = new SaveData { version = 1, screenMotion = false, cartons = 12, salves = 4 };
                GameState back; StoryState st6; string pl; float secs;
                check(SaveSystem.Restore(data, out back, out st6, out pl, out secs), "a save with motion off loads");
                check(back != null && !back.ScreenMotion, "and motion is still off after loading");

                // A file written before the option existed has no key for it and must default on,
                // rather than silently turning the game's feedback off for a returning player.
                var old7 = new SaveData { version = 1, cartons = 12, salves = 4 };
                GameState back2; StoryState st7; string pl2; float secs2;
                SaveSystem.Restore(old7, out back2, out st7, out pl2, out secs2);
                check(back2 != null && back2.ScreenMotion, "an older save loads with motion on");
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
                    var halo = SurfaceMode.AgainstGround(w.Land, w.Land, 0f, 0.84f);
                    float byHalo = Mathf.Abs(SurfaceMode.Luminance(halo) - ground);
                    check(Mathf.Max(Mathf.Max(byBody, byRim), byHalo) >= 0.20f,
                          sp.Name + " reads against " + w.Name + "'s ground (body " +
                          byBody.ToString("0.00") + ", rim " + byRim.ToString("0.00") +
                          ", halo " + byHalo.ToString("0.00") + ")");
                }
            }

            // ---- the approach prompt ----
            // The bar is 1200px at font 22. Worst case is the longest world name with the
            // longest owed phrase, which is the "picked clean" one rather than a count.
            {
                foreach (var w in PlanetDatabase.All)
                {
                    if (w.IsBossWorld) continue;
                    int catchable = 0;
                    foreach (var sp in w.Spawns)
                        if (SpeciesDatabase.Get(sp.SpeciesId).CatchRate >= SpeciesDatabase.CatchableThreshold) catchable++;

                    foreach (var owed in new[]
                    {
                        Words.Count(catchable, "egg") + " here you have not recorded yet.",
                        "Every egg here is already in your record.",
                    })
                    {
                        string prompt = "Press E to land on " + w.Name + "  ·  Lv " +
                                        w.MinLevel + "-" + w.MaxLevel + "  ·  " + owed;
                        check(lines(prompt, 1200f - 40f, 22) == 1,
                              "approach prompt fits for " + w.Name + " (" + prompt.Length + " chars)");
                    }
                }

                // And the sealed form, which carries the whole outstanding list instead.
                for (int i = 0; i < StoryDatabase.Beats.Length; i++)
                {
                    var walk4 = new StoryState();
                    walk4.RestoreFrom(new string[0], i);
                    string missing = walk4.CurrentBlockerText(new GameState());
                    if (missing == null) continue;
                    string prompt = "Cobblestead is beyond your charted route.  Still needed: " + missing + ".";
                    check(lines(prompt, 1200f - 40f, 22) <= 2,
                          "sealed approach prompt stays short at beat '" + StoryDatabase.Beats[i].Id + "'");
                }
            }

            // ---- the chart's distance line ----
            // Drawn in an 848px body at font 22, from every world to every other. The longest
            // pairing is what has to fit, not a convenient one.
            {
                float worstPx = 0f; string worstPair = "";
                foreach (var a in PlanetDatabase.All)
                    foreach (var b in PlanetDatabase.All)
                    {
                        if (a.Id == b.Id) continue;
                        float gap = Mathf.Max(0f, Vector2.Distance(a.SpacePosition, b.SpacePosition)
                                                  - a.SpaceRadius - b.SpaceRadius);
                        string line = Mathf.RoundToInt(gap) + " units from " + a.Name +
                                      "  ·  about " + (gap / TeoController.FlyMaxSpeed).ToString("0.0") +
                                      "s of flying";
                        float px = line.Length * 22f * 0.52f;
                        if (px > worstPx) { worstPx = px; worstPair = a.Name + " to " + b.Name; }
                        check(lines(line, 848f, 22) == 1, "chart distance line fits: \"" + line + "\"");

                        // A time of zero would read as though the world were free to reach.
                        check(gap <= 0f || gap / TeoController.FlyMaxSpeed >= 0.05f,
                              "the flight time from " + a.Name + " to " + b.Name + " is not rounded away");
                    }
                check(worstPx <= 848f,
                      "the widest distance line fits (" + worstPx.ToString("0") + "px, " + worstPair + ")");
            }

            // ---- the chart's detail panel, in its fullest state ----
            {
                // 900px wide with 26px padding either side, 660px tall at font 22. Everything
                // competes for the same 25 lines: tagline, spawn list, distance, cache, landmark,
                // boss note and the course line. Nothing measured this until a landmark line was
                // added to it, which is the wrong order to find out.
                var full = new GameState();
                var late = new StoryState();
                late.RestoreFrom(new string[0], StoryDatabase.Beats.Length - 1);

                foreach (var w in PlanetDatabase.All)
                {
                    full.Visited.Add(w.Id);
                    if (PlanetDatabase.HasCache(w.Id)) full.Caches.Add(w.Id);
                    if (LandmarkDatabase.For(w.Id) != null) full.Landmarks.Add(w.Id);
                }
                foreach (var sp in SpeciesDatabase.All) { full.Seen.Add(sp.Id); full.Caught.Add(sp.Id); }

                var farthest = PlanetDatabase.Get("yolkhaven");
                foreach (var w in PlanetDatabase.All)
                    foreach (GalaxyMapView.Presence pres in
                             new[] { GalaxyMapView.Presence.Elsewhere, GalaxyMapView.Presence.InOrbit })
                    {
                        // The body box lost its bottom 100px to the course footer and its rule.
                        string text = GalaxyMapView.DetailBody(w, full, late, farthest, pres);
                        int used = lines(text, 848f, 22);
                        check(used <= capacity(560f, 22),
                              "chart detail fits for " + w.Name + " (" + used + " of " +
                              capacity(560f, 22) + " lines)");

                        string course = GalaxyMapView.CourseLine(w, full, late, pres);
                        check(lines(course, 848f, 22) == 1,
                              "chart course line is one line for " + w.Name + ": " + course);
                    }
            }

            // ---- nothing carries element by colour alone ----
            {
                // Swept rather than spot-checked. I had already claimed this was handled once,
                // on the strength of three screens, and the fourth - the field record's own
                // list - was the one a colour-blind player would spend the most time reading.
                //
                // Every surface that tints something by element, and what carries it besides
                // the hue:
                //   battle plates      the type chip's own text
                //   move cards         the type name on the sub-line
                //   move message       the element spelled in the sentence
                //   party strip        the abbreviation after the level
                //   entry panel        the type name under the species name
                //   field record list  the abbreviation after the name
                //   chart labels       the abbreviation after the level range
                //   egg panel          the type name under the egg's name
                // and the two that are shape rather than text: the effectiveness arrows on the
                // move cards, and the caught/seen/unmet marks in the record.
                // Through the chart's own builder. Building the string here instead meant
                // deleting the element from the real one changed nothing at all.
                var charted = new GameState(false);
                var lateStory = new StoryState();
                lateStory.RestoreFrom(new string[0], StoryDatabase.Beats.Length - 1);
                foreach (var w in PlanetDatabase.All) charted.Visited.Add(w.Id);

                foreach (var w in PlanetDatabase.All)
                {
                    string chartSuffix = GalaxyMapView.MarkerSuffix(
                        w, charted, lateStory, GalaxyMapView.Presence.Elsewhere);

                    check(chartSuffix.Contains(TypeChart.Abbrev(w.Theme)),
                          w.Name + "'s chart label names its element: " + chartSuffix);
                    check(lines(chartSuffix, 240f, 15) == 1,
                          w.Name + "'s chart label fits at its widest: " + chartSuffix);
                }

                // And an uncharted world still says only that.
                var nothing = new GameState(false);
                foreach (var w in PlanetDatabase.All)
                {
                    if (nothing.Visited.Contains(w.Id)) continue;
                    string s2 = GalaxyMapView.MarkerSuffix(w, nothing, lateStory,
                                                           GalaxyMapView.Presence.Elsewhere);
                    check(!s2.Contains(TypeChart.Abbrev(w.Theme)),
                          w.Name + "'s element is not given away before you have been: " + s2);
                }

                // The abbreviations are doing the work the colour was, so a collision would put
                // us back where we started - but that is already asserted where the tags are
                // defined ("Frost's three-letter tag is unique"), and planting a collision fires
                // it. A second copy here would be clutter that passes for rigour.
            }

            // ---- the field record does not carry element by colour alone ----
            {
                // Every other screen in the game spells the element beside the colour, because
                // the palette work assumes hue carries nothing on its own - and the two closest
                // type colours sit twelve units apart under deuteranopia. The record's list was
                // the exception: twenty-eight names, coloured by element, saying nothing else.
                foreach (var sp in SpeciesDatabase.All)
                {
                    string known = HudView.DexRow(sp, true, true, 1, false);
                    check(known.Contains(TypeChart.Abbrev(sp.Type)),
                          sp.Name + "'s record row names its element: " + known);

                    // 420px at font 19, and the element sits at 15.
                    check(lines(known, 420f, 19) == 1,
                          sp.Name + "'s record row fits its column: " + known);

                    // A species you have never met gives nothing away - not its name, and not
                    // its element either.
                    string unknown = HudView.DexRow(sp, false, false, 1, false);
                    check(!unknown.Contains(TypeChart.Abbrev(sp.Type)),
                          sp.Name + "'s element is not shown before you have met one");
                    check(unknown.Contains("? ? ?"), "and neither is its name");
                }

                // The cursor row is bold and one line wider; it still has to fit.
                foreach (var sp in SpeciesDatabase.All)
                    check(lines(HudView.DexRow(sp, true, true, 28, true), 420f, 19) == 1,
                          sp.Name + "'s row fits while the cursor is on it");
            }

            // ---- entering a world sets everything a world needs ----
            {
                // The radius and the ground colour were separate assignments. Four places set
                // the radius; one set the colour. Among the three that forgot was the start of
                // a new run - so the first world anybody ever walks threw up default grey.
                // They are one call now, and this is the check that the call does both.
                foreach (var w in PlanetDatabase.All)
                {
                    check(Mathf.Approximately(TeoController.SurfaceEntryRadius(w), w.SurfaceRadius),
                          "entering " + w.Name + " sets how far you can walk");
                    check(TeoController.SurfaceEntryGround(w) == w.Land,
                          "entering " + w.Name + " sets what you are walking on");
                }

                // The home world in particular, because that is the one a new run starts on and
                // the one the separate assignments got wrong.
                var home2 = PlanetDatabase.Home;
                check(TeoController.SurfaceEntryGround(home2) == home2.Land,
                      home2.Name + " is not left as default grey");
            }

            // ---- walking kicks up the ground ----
            {
                // Minutes of walking on ground the game has gone to some trouble to make
                // specific, and nothing came off it. The motes take the world's own colour,
                // so they have to clear that colour the way everything else on a surface does.
                foreach (var w in PlanetDatabase.All)
                {
                    var mote = TeoController.StepMoteTint(w.Land);
                    float gapMote = Mathf.Abs(SurfaceMode.Luminance(mote) - SurfaceMode.Luminance(w.Land));
                    check(gapMote > 0.10f,
                          "a step throws up something you can see on " + w.Name +
                          " (luminance gap " + gapMote.ToString("0.00") + ")");
                    check(mote.a > 0.2f && mote.a < 0.85f,
                          w.Name + "'s motes are ground, not smoke (alpha " + mote.a.ToString("0.00") + ")");
                }

                // Slower than the thruster: walking is not flying, and a mote every frame
                // would read as a dust storm.
                check(TeoController.StepPuffInterval > 0.09f,
                      "walking throws up less than flying does (" +
                      TeoController.StepPuffInterval.ToString("0.00") + "s between motes)");

                // At walking speed that is a mote roughly every two units, which is a trail
                // rather than a smear.
                float spacing = TeoController.WalkSpeed * TeoController.StepPuffInterval;
                check(spacing > 1f && spacing < 3f,
                      "the motes space out into a trail (" + spacing.ToString("0.0") + " units apart)");
            }

            // ---- what the nest header says ----
            {
                // The reason a player opens the nest is to ask whether it holds anything worth
                // swapping in, and with fifty-nine eggs the answer was thirty keypresses away.
                var empty = new GameState(false);
                check(HudView.NestSummary(empty) == "empty", "an empty nest says so");

                var full = new GameState(false);
                foreach (var id in new[] { "sprouteg", "cobblet", "yolkano", "tidepoach" })
                    full.Nest.Add(EggInstance.Wild(id, 12));
                full.Nest.Add(EggInstance.Wild("shadowhisk", 29));

                string line = HudView.NestSummary(full);
                check(line.Contains("5 back home"), "the nest header counts what is in it: " + line);
                check(line.Contains("best Lv 29"), "and names the best level in it: " + line);
                check(line.Contains("5 elements"), "and how many elements: " + line);

                // One of something reads as one, not as a plural with a 1 in front.
                var single = new GameState(false);
                single.Nest.Add(EggInstance.Wild("sprouteg", 7));
                check(HudView.NestSummary(single).Contains("1 element") &&
                      !HudView.NestSummary(single).Contains("1 elements"),
                      "a one-element nest reads properly: " + HudView.NestSummary(single));

                // The header shares a 720px column at font 20 with everything else.
                var huge = new GameState(false);
                for (int i = 0; i < 99; i++) huge.Nest.Add(EggInstance.Wild("cobblet", 30));
                foreach (var sp in SpeciesDatabase.All) huge.Nest.Add(EggInstance.Wild(sp.Id, 30));
                check(lines("NEST  " + HudView.NestSummary(huge), 720f, 20) == 1,
                      "the nest header fits at its widest: " + HudView.NestSummary(huge));
            }

            // ---- every egg in the nest can be reached ----
            {
                // The nest list showed the first twenty and the cursor could not leave them,
                // so on a run that ends with fifty-nine eggs back home, thirty-nine of them
                // could never be looked at, let alone swapped back into the party. They were
                // not hidden - they were unreachable, and nothing said so.
                var big = new GameState(false);
                for (int i = 0; i < GameState.PartySize; i++) big.Party.Add(EggInstance.Wild("sprouteg", 10));
                for (int i = 0; i < 59; i++) big.Nest.Add(EggInstance.Wild("cobblet", 10));

                check(HudView.CollectionRows(big) == big.Party.Count + big.Nest.Count,
                      "the cursor can reach every egg you own (" + HudView.CollectionRows(big) +
                      " of " + (big.Party.Count + big.Nest.Count) + ")");
                check(HudView.CollectionRows(big) > big.Party.Count + HudView.NestWindowSize,
                      "and is not capped at the window (" + HudView.CollectionRows(big) +
                      " against a window of " + HudView.NestWindowSize + ")");

                // A nest smaller than the window still works.
                var small = new GameState(false);
                small.Party.Add(EggInstance.Wild("sprouteg", 5));
                small.Nest.Add(EggInstance.Wild("cobblet", 5));
                check(HudView.CollectionRows(small) == 2, "a two-egg collection has two rows");

                var empty = new GameState(false);
                check(HudView.CollectionRows(empty) == 0, "an empty collection has none");
            }

            // ---- the chart marks worlds you have not finished ----
            {
                // Choosing where to fly meant selecting each of seventeen worlds in turn to
                // read what it still owed you. The hollow ring is the same mark the field
                // record uses for an unrecorded species, so it means one thing in both places.
                var fresh = new GameState();
                var home = PlanetDatabase.Home;
                check(fresh.EggFromOri != null, "the fresh state is the one a player starts in");

                // A world whose whole roster is recorded owes nothing.
                var doneWithHome = new GameState();
                foreach (var sp in home.Spawns) doneWithHome.Caught.Add(sp.SpeciesId);

                // Through the game's own counter, not a second copy of the same loop. The first
                // version of this check recomputed it here, so planting a fault in the real one
                // changed nothing and the suite passed.
                int owedFresh = fresh.UnrecordedOn(home);
                int owedDone = doneWithHome.UnrecordedOn(home);
                check(owedFresh > 0, home.Name + " owes a new player something (" + owedFresh + ")");
                check(owedDone == 0, home.Name + " owes nothing once its roster is recorded");

                // Every world must be finishable, or the mark would never clear.
                foreach (var w in PlanetDatabase.All)
                {
                    if (w.Spawns == null) continue;
                    var all = new GameState(false);
                    foreach (var sp in w.Spawns) all.Caught.Add(sp.SpeciesId);
                    check(all.UnrecordedOn(w) == 0, w.Name + " can be finished");
                }
            }

            // ---- the egg Ori gave you ----
            {
                // It was an ordinary Sprouteg, so the moment a player caught a second one the
                // game had no idea which was which - and the one thing Ori would certainly
                // notice was the one thing he could not.
                var fresh = new GameState();
                check(fresh.EggFromOri != null, "a new game starts holding the egg Ori gave you");
                check(fresh.EggFromOri.Species.Id == "sprouteg", "and it is his Sprouteg");

                // A second one of the same species is not his.
                fresh.Party.Add(EggInstance.Wild("sprouteg", 5));
                check(ReferenceEquals(fresh.EggFromOri, fresh.Party[0]),
                      "catching another Sprouteg does not confuse which one is his");

                // It survives being put in the nest, and being let go.
                var kept = new GameState();
                var mine = kept.EggFromOri;
                kept.Party.Remove(mine); kept.Nest.Add(mine);
                check(ReferenceEquals(kept.EggFromOri, mine), "it is still his while it sits at the nest");

                // He says nothing until it has grown.
                var briefed = new StoryState();
                briefed.RestoreFrom(new[] { "met_ori", "ori_briefed" }, 2);
                var young = new GameState();
                var small = StoryDatabase.GetDialogue("ori", briefed, young);
                bool remarks = false;
                foreach (var line in small.Lines) if (line.Text.Contains("size of my thumb")) remarks = true;
                check(!remarks, "Ori does not remark on a level 5 Sprouteg he handed over this morning");

                // Built through the real constructor at the level Ori notices, rather than by
                // reaching into the egg - the flag is what identifies it, not the level.
                var grown = new GameState(false);
                var raised = EggInstance.Wild("sprouteg", StoryDatabase.OriNoticesLevel);
                raised.MarkFromOri();
                grown.Party.Add(raised);
                var big = StoryDatabase.GetDialogue("ori", briefed, grown);
                bool notices = false;
                foreach (var line in big.Lines) if (line.Text.Contains("size of my thumb")) notices = true;
                check(notices, "Ori notices it once it has grown");
                check(big.SetsFlag == "ori_saw_starter", "and only says it once");

                // Every line of it fits the dialogue box.
                foreach (var line in big.Lines)
                    check(lines(line.Text, 1380f, 28) <= capacity(180f, 28),
                          "Ori's line about his egg fits: " + line.Text);
            }

            // ---- the gate says which elements are missing ----
            {
                // "Two more types" is a number. The elements you have nothing of are
                // somewhere to fly to - and once you have seen one of a type, the field
                // record will tell you which worlds it lives on.
                var st = new GameState(false);
                st.Party.Add(EggInstance.Wild("sprouteg", 10));   // Verdant only

                check(st.TypesMissing.Count == 7,
                      "a one-type party is missing seven elements (" + st.TypesMissing.Count + ")");
                foreach (var t in st.TypesMissing)
                    check(t != EggType.Plain, "Plain is not an element you can be missing");

                // With four or fewer missing they are named; with more it stays a number,
                // because listing seven elements in a 524px panel is not a hint, it is a wall.
                var walk = new StoryState();
                for (int i = 0; i < StoryDatabase.Beats.Length; i++)
                {
                    var at = new StoryState();
                    at.RestoreFrom(new string[0], i);
                    if (at.Current.RequiredTypes <= 0) continue;

                    string wall = at.CurrentBlockerText(st);
                    check(wall != null && !wall.Contains("nothing yet from"),
                          "seven missing elements are not all listed at '" + at.Current.Id + "'");

                    // One short of whatever the beat actually asks for, rather than of a
                    // number I assumed. The gate wants four types, not six, so a five-type
                    // party already satisfied it and the check below never ran.
                    var nearly = new GameState(false);
                    var stock = new[] { "sprouteg", "yolkano", "tidepoach", "yolty", "chillet",
                                        "cobblet", "nebulegg", "duskle" };
                    for (int k = 0; k < at.Current.RequiredTypes - 1 && k < stock.Length; k++)
                        nearly.Party.Add(EggInstance.Wild(stock[k], 10));
                    string close = at.CurrentBlockerText(nearly);
                    // Not conditional. A check that only runs when the string happens to say
                    // something can pass by never running - disabling the hint entirely left
                    // this silent, which is the same vacuity the party-row check had.
                    check(close != null && close.Contains("more type"),
                          "a five-type party is still short of the gate at '" + at.Current.Id +
                          "': " + close);
                    check(close != null && close.Contains("nothing yet from"),
                          "and is told which elements: " + close);
                }
            }

            // ---- waking up after losing ----
            {
                // Both lines land in the toast, which is 1000px wide with 20px either side at
                // font 24 and holds 76px of height.
                foreach (var line in new[] { UiCopy.WokeWarm, UiCopy.WokeCold })
                    check(lines(line, 1000f - 40f, 24) <= capacity(76f, 24),
                          "the waking line fits its toast: " + line);

                // They have to be different, or there was no point looking at which world it
                // was - and the cold one must not promise a warm pad.
                check(UiCopy.WokeWarm != UiCopy.WokeCold, "waking cold reads differently from waking warm");
                check(!UiCopy.WokeCold.Contains("warm"),
                      "the cold line does not claim a warm pad: " + UiCopy.WokeCold);
                check(UiCopy.WokeWarm.Contains("warm"),
                      "and the warm one says so");
            }

            // ---- the chart never asks for what you already have ----
            {
                // Counted: if no remainder ever names anybody, this whole section asserts
                // nothing and says so.
                int namedInRemainder = 0;

                // The sealed-route panel used to print the whole objective and then the
                // outstanding list under it, so a player who had found one of two keepers read
                // "Find Marn on Voltacrest and Sable on Glacierim" directly above "Still
                // needed: Sable on Glacierim".
                for (int i = 0; i < StoryDatabase.Beats.Length; i++)
                {
                    var walk = new StoryState();
                    walk.RestoreFrom(new string[0], i);

                    foreach (Sector sec in new[] { Sector.LongDrift, Sector.ShatteredBelt, Sector.Amaranth })
                    {
                        var partial = new GameState();
                        string text = walk.SectorBlockerText(sec, partial);
                        if (text == null || !text.Contains("Still needed:")) continue;

                        string asked = text.Substring(text.IndexOf("Still needed:"));
                        string before = text.Substring(0, text.IndexOf("Still needed:"));

                        // Nothing named in the remainder may also be named above it.
                        foreach (var npc in StoryDatabase.Npcs)
                            if (asked.Contains(npc.Name))
                            {
                                namedInRemainder++;
                                check(!before.Contains(npc.Name),
                                      "the chart does not ask twice for " + npc.Name +
                                      " at beat '" + StoryDatabase.Beats[i].Id + "'");
                            }
                    }
                }

                check(namedInRemainder > 0,
                      "some sealed route does name a person in its remainder (" +
                      namedInRemainder + " of them)");
            }

            // ---- what the boss world tells you before you go down ----
            {
                // Amy fields the highest levels in the game, and the pacing run says a player
                // who follows the story efficiently arrives with 48 of the run's 54 forced
                // encounters still ahead of them. The prompt is the last thing they read
                // before committing, and it used to say only that Amy was down there.
                int amy = StoryDatabase.TopLevelOf("amy");
                check(amy > 0, "Amy's top level is knowable from the trainer data (" + amy + ")");

                // She outclasses everything you meet on the way to her - but not Amaranth's own
                // wildlife, which runs to 26 against her 23. That is deliberate: the boss world
                // is the first egg and the oldest things in the game live on it. It does mean
                // the prompt cannot quote only her level, or a player reads "up to Lv 23" and is
                // then jumped by a 26 on the walk over.
                int wildTop = 0; string wildTopName = "";
                foreach (var w in PlanetDatabase.All)
                {
                    if (w.IsBossWorld) continue;
                    if (w.MaxLevel > wildTop) { wildTop = w.MaxLevel; wildTopName = w.Name; }
                }
                check(amy >= wildTop,
                      "Amy outclasses everything on the way to her (" + amy + " against " +
                      wildTopName + "'s " + wildTop + ")");

                var boss = PlanetDatabase.Get("amaranth");
                check(SpaceMode.BossPrompt(boss, 22).Contains("Lv " + boss.MinLevel + "-" + boss.MaxLevel),
                      "the descent prompt quotes Amaranth's own wild range, not only Amy's team");
                check(SpaceMode.BossPrompt(boss, 22).Contains("Lv " + amy),
                      "and quotes what Amy fields");

                // The prompt shares the bottom of the screen with everything else and cannot
                // wrap; it is the longest one in the game now.
                check(lines(SpaceMode.BossPrompt(boss, 22), 1600f, 24) == 1,
                      "the descent prompt fits: " + SpaceMode.BossPrompt(boss, 22));

                // Underlevelled is marked, ready is not.
                check(SpaceMode.BossPrompt(boss, amy - SpaceMode.AmyComfortableGap - 1).Contains("E55555"),
                      "a player well under Amy's level is told so");
                check(!SpaceMode.BossPrompt(boss, amy).Contains("E55555"),
                      "a player at her level is not nagged");

                // And the warning has to leave room to act. If the gap were zero a player at
                // exactly her level would be told they are short.
                check(SpaceMode.AmyComfortableGap > 0 && SpaceMode.AmyComfortableGap < 6,
                      "the readiness warning has a usable margin (" + SpaceMode.AmyComfortableGap + ")");

                // The Amaranth gate asks for a level; it should not ask for more than Amy has.
                check(StoryDatabase.GateLevel <= amy,
                      "the gate does not ask for more than Amy fields (" +
                      StoryDatabase.GateLevel + " against " + amy + ")");
            }

            // ---- the record cannot exceed its own maximum ----
            {
                // Evolution registers the grown form, and some grown forms cannot be caught -
                // they exist only by evolving one. That pushed a completionist's counter past
                // the total it was counting toward: "Record 28/24".
                var everything = new GameState(false);
                foreach (var sp in SpeciesDatabase.All) everything.RegisterSpecies(EggInstance.Wild(sp.Id, 5));

                check(everything.RecordedCatchable == SpeciesDatabase.CatchableCount,
                      "a full record reads " + SpeciesDatabase.CatchableCount + "/" +
                      SpeciesDatabase.CatchableCount + ", not " + everything.Caught.Count);
                check(everything.Caught.Count > SpeciesDatabase.CatchableCount,
                      "and there really are species you can only get by evolving one (" +
                      (everything.Caught.Count - SpeciesDatabase.CatchableCount) + " of them)");

                // Every counter in the game shows the same figure.
                check(HudView.SuppliesLine(everything).Contains(
                          "Record " + SpeciesDatabase.CatchableCount + "/" + SpeciesDatabase.CatchableCount),
                      "the supplies strip agrees: " + HudView.SuppliesLine(everything).Split('\n')[1]);
            }

            // ---- the supplies strip ----
            {
                // 524px at font 19, two rows, and it cannot wrap. The widest state is a full
                // record with every cache dug up, which is also the state a player reaches
                // last and is least likely to have been looked at.
                var rich = new GameState(false);
                foreach (var id in PlanetDatabase.CacheWorlds.Keys) rich.Caches.Add(id);
                foreach (var sp in SpeciesDatabase.All) { rich.Seen.Add(sp.Id); rich.Caught.Add(sp.Id); }
                rich.Cartons = rich.MaxCartons;
                for (int i = 0; i < GameState.PartySize; i++) rich.Party.Add(EggInstance.Wild("sprouteg", 5));

                foreach (int carts in new[] { 0, 1, 3, rich.MaxCartons })
                    foreach (int salves in new[] { 0, 1, GameState.MaxSalves })
                    {
                        rich.Cartons = carts; rich.Salves = salves;
                        foreach (var row in HudView.SuppliesLine(rich).Split('\n'))
                            check(lines(row, 524f, 19) == 1,
                                  "the supplies strip fits at " + carts + " cartons and " +
                                  salves + " salves: " + row);
                    }

                // Empty says so in a word. A zero among other numbers is the easiest thing on
                // a HUD to read straight past.
                rich.Cartons = 0; rich.Salves = 0;
                string empty = HudView.SuppliesLine(rich);
                check(empty.Contains("Cartons none"), "an empty carton count says none: " + empty);
                check(empty.Contains("Salves none"), "an empty salve count says none");
                check(!empty.Contains("Cartons 0"), "and never shows a bare zero");

                rich.Cartons = 12; rich.Salves = 5;
                check(!HudView.SuppliesLine(rich).Contains("none"),
                      "a stocked player is not told they are empty");
            }

            // ---- the party strip ----
            {
                // 300px at font 18. The row has to fit with the lead arrow on it and with OUT
                // on it, and those two can never appear together - a fainted egg is never the
                // leader - so the worst case is whichever of them is wider.
                // The widest row is not a nicknamed egg - a nickname is capped at 12. It is an
                // Elder with no nickname, whose name is "Elder " plus the species, which is
                // what Shorten is there for. Measuring the nickname case first made the check
                // untestable: widening Shorten to 24 changed nothing, because no nickname is
                // ever that long.
                foreach (var sp in SpeciesDatabase.All)
                    foreach (bool nicknamed in new[] { false, true })
                    {
                        var egg = EggInstance.WildElder(sp.Id, 30);
                        if (nicknamed) egg.Nickname = new string('W', NameEntryView.MaxLength);

                        string leading = HudView.PartyRow(egg, true);
                        check(lines(leading, 360f, 18) == 1,
                              sp.Name + "'s row fits while leading: " + leading);

                        egg.CurrentHP = 0;
                        string down = HudView.PartyRow(egg, false);
                        check(lines(down, 360f, 18) == 1,
                              sp.Name + "'s row fits while out: " + down);
                    }

                // The leader is the first egg still standing, not simply the first egg.
                var st = new GameState(false);
                st.Party.Add(EggInstance.Wild("sprouteg", 10));
                st.Party.Add(EggInstance.Wild("cobblet", 10));
                st.Party[0].CurrentHP = 0;
                check(ReferenceEquals(st.Leader, st.Party[1]),
                      "a fainted egg does not lead");
                check(HudView.PartyRow(st.Party[0], ReferenceEquals(st.Party[0], st.Leader)).Contains("OUT"),
                      "the fainted one is marked OUT");
                check(!HudView.PartyRow(st.Party[0], ReferenceEquals(st.Party[0], st.Leader)).Contains("\u25b8"),
                      "and is not also marked as leading");
            }

            // ---- your nest, warming the pad ----
            {
                // Ori's rule is that a station runs warm off the eggs around it, which is why
                // a cold one still works when you turn up with a full nest. Resting shows it
                // now, so the eggs have to land on the station and not on its furniture.
                check(SurfaceMode.WarmingRingRadius > SurfaceMode.NestRingDiameter * 0.5f,
                      "the nest stands outside the station's ring (" + SurfaceMode.WarmingRingRadius +
                      " against " + (SurfaceMode.NestRingDiameter * 0.5f) + ")");
                check(SurfaceMode.WarmingRingRadius < SurfaceMode.NestPadDiameter * 0.5f,
                      "the nest stands on the pad rather than off the edge of it (" +
                      SurfaceMode.WarmingRingRadius + " against " + (SurfaceMode.NestPadDiameter * 0.5f) + ")");

                // Six eggs at the widest, so no two may overlap on the ring.
                float step = 2f * Mathf.PI * SurfaceMode.WarmingRingRadius / GameState.PartySize;
                check(step > 1.15f,
                      "six eggs fit round the pad without touching (" + step.ToString("0.00") +
                      " apart, each 1.15 wide)");

                // And it is over before the line explaining it has gone.
                float whole = SurfaceMode.WarmingRise + SurfaceMode.WarmingHold + SurfaceMode.WarmingFall;
                check(whole < 3f, "the warming is done inside the toast that describes it (" +
                      whole.ToString("0.0") + "s)");
            }

            // ---- the game explains salves before Shimmerfen ----
            {
                // Lune explains them properly, on Shimmerfen, in the second sector. A player
                // carries four from the first minute, and the balance run puts them at
                // seventeen points on the Amy fight - so waiting until the second sector to
                // mention them at all is a long time to leave that on the table.
                check(GameState.MaxSalves > 0, "a new player is carrying salves from the start");
                check(new GameState().Salves == GameState.MaxSalves,
                      "and has a full stack of them before anybody has said what they are");

                // The hint fires while there is still a fight left to use it in - not at a
                // sliver of health, when the answer is to swap or run.
                check(BattleMode.SalveHintFraction > 0.25f && BattleMode.SalveHintFraction < 0.6f,
                      "the salve hint lands while a salve is still the right move (" +
                      (BattleMode.SalveHintFraction * 100f).ToString("0") + "% health)");

                // Lune still says the full version - the hint is a prompt, not a replacement.
                var lune = StoryDatabase.GetDialogue("lune", new StoryState(), new GameState());
                bool explains = false;
                foreach (var line in lune.Lines) if (line.Text.Contains("salve")) explains = true;
                check(explains, "Lune still explains salves in full");
            }

            // ---- an Elder announces itself ----
            {
                // The stir is rolled when it starts rather than when it finishes, so it can be
                // about something. An Elder is the biggest thing a field turns up - three
                // levels above its neighbours, lit round the shell - and it used to arrive with
                // no more warning than a Sprouteg.
                check(EggInstance.ElderChance > 0f && EggInstance.ElderChance < 0.25f,
                      "an Elder stays rare enough for the deeper stir to mean something (" +
                      (EggInstance.ElderChance * 100f).ToString("0") + "%)");

                // That Elders are gated behind Ori's three eggs is asserted in the balance
                // suite already ("a brand new run meets no Elders"), and planting the gate away
                // fires it. Not repeated here.

                // An Elder is genuinely three levels up, which is what the warning is for.
                var ordinary = EggInstance.Wild("cobblet", 12);
                var elder = EggInstance.WildElder("cobblet", 12);
                check(elder.Level > ordinary.Level,
                      "an Elder outlevels its neighbours (" + elder.Level + " against " +
                      ordinary.Level + ")");
                check(elder.Elder && !ordinary.Elder, "and knows it");
            }

            // ---- every reading of 'strong' is the same reading ----
            {
                // 1.2 and 0.8 were bare literals in thirteen places across five concerns: what
                // counts as strong enough for Tough Shell to resist, how hard the screen
                // shakes, the colour and size of a damage number, whether the move card shows
                // an arrow, what the message box says, what the swap menu warns about, and the
                // three matchup lists on the record page. Every one meant the same thing and
                // nothing held them together - so an arrow could promise a strong hit that
                // Tough Shell then refused to treat as one.
                check(TypeChart.WeakBelow < 1f && TypeChart.StrongAbove > 1f,
                      "strong is above even and weak is below it");

                foreach (EggType a in System.Enum.GetValues(typeof(EggType)))
                    foreach (EggType d in System.Enum.GetValues(typeof(EggType)))
                    {
                        float m = TypeChart.Multiplier(a, d);
                        check(!(TypeChart.IsStrong(m) && TypeChart.IsWeak(m)),
                              a + " on " + d + " is not both strong and weak");

                        // The battle line, the card's arrow and the record's lists all have to
                        // agree about this pair. They are four separate readings of one number.
                        string line = TypeChart.EffectivenessLine(m);
                        check((line != null) == (TypeChart.IsStrong(m) || TypeChart.IsWeak(m)),
                              "the battle line appears exactly when the hit is not even (" +
                              a + " on " + d + ")");

                        if (a == d) continue;
                        bool listedStrong = System.Array.IndexOf(TypeChart.StrongAgainst(a), d) >= 0;
                        check(listedStrong == TypeChart.IsStrong(m),
                              "the record lists " + a + " as strong on " + d +
                              " exactly when a fight would be (" + m.ToString("0.00") + ")");

                        bool listedVuln = System.Array.IndexOf(TypeChart.VulnerableTo(d), a) >= 0;
                        check(listedVuln == TypeChart.IsStrong(m),
                              "and the other way round, from " + d + "'s page");

                        bool listedResist = System.Array.IndexOf(TypeChart.Resists(d), a) >= 0;
                        check(listedResist == TypeChart.IsWeak(m),
                              d + " is listed as resisting " + a + " exactly when it does");
                    }

                // Tough Shell resists a strong hit and nothing else. This is the pair that
                // would have gone wrong in silence: the card promises an arrow, the trait
                // decides separately whether to fire.
                foreach (EggType a in System.Enum.GetValues(typeof(EggType)))
                    foreach (EggType d in System.Enum.GetValues(typeof(EggType)))
                    {
                        float m = TypeChart.Multiplier(a, d);
                        bool arrow = TypeChart.IsStrong(m);
                        bool resisted = BattleCalc.ToughShellResists(m);
                        check(arrow == resisted,
                              "Tough Shell fires exactly when the card promised a strong hit (" +
                              a + " on " + d + ")");
                    }
            }

            // ---- the approach prompt warns when you are outmatched ----
            {
                // The comment above this prompt says it is where the choice is actually made,
                // and it was the one place that gave the level band and left the player to do
                // the arithmetic. The chart says where you stand and the descent prompt says it
                // in front of Amy; approaching any other world said nothing.
                foreach (var w in PlanetDatabase.All)
                {
                    // Well under, just under, in it, and well over.
                    foreach (int lv in new[] { 1, Mathf.Max(1, w.MinLevel - 1), w.MinLevel, w.MaxLevel + 10 })
                    {
                        var st = new GameState(false);
                        st.Party.Add(EggInstance.Wild(SpeciesDatabase.All[0].Id, lv));
                        string line = SpaceMode.LandPrompt(w, st, "3 eggs here you have not recorded yet.");

                        // It always says the thing it was already saying.
                        check(line.Contains(w.Name), w.Name + "'s prompt names the world at Lv " + lv);
                        check(line.Contains("Lv " + w.MinLevel + "-" + w.MaxLevel),
                              "and its band at Lv " + lv);

                        // The warning appears exactly when the lead is under everything down
                        // there. A warning that appears every time is not a warning.
                        bool warns = line.Contains("E55555");
                        check(warns == (lv + GalaxyMapView.ComfortGap < w.MinLevel),
                              w.Name + " warns exactly when it should at Lv " + lv +
                              " (band " + w.MinLevel + "-" + w.MaxLevel + ")");
                        if (warns) check(line.Contains("Lv " + lv), "and quotes the real lead level");

                        // And the whole thing still fits the 1200px panel it is drawn in.
                        check(lines(line, 1200f, 24) == 1,
                              w.Name + "'s prompt fits its panel at Lv " + lv + ": " +
                              System.Text.RegularExpressions.Regex.Replace(line, "<[^>]+>", ""));
                    }
                }

                // It agrees with the chart, which is the other place the same question gets
                // answered - two readings of "you are outmatched" that disagreed would be worse
                // than one of them not existing.
                foreach (var w in PlanetDatabase.All)
                    for (int lv = 1; lv <= EggInstance.MaxLevel; lv += 3)
                    {
                        var st = new GameState(false);
                        st.Party.Add(EggInstance.Wild(SpeciesDatabase.All[0].Id, lv));
                        bool promptWarns = SpaceMode.LandPrompt(w, st, null).Contains("E55555");
                        bool chartWarns = GalaxyMapView.Readiness(w, st).Contains("E55555");
                        check(promptWarns == chartWarns,
                              "the chart and the approach agree about " + w.Name + " at Lv " + lv);
                    }

                // With no eggs at all it says nothing about a lead it does not have.
                check(!SpaceMode.LandPrompt(PlanetDatabase.Home, new GameState(false), null)
                          .Contains("your lead"),
                      "a player with no eggs is not told about their lead");
            }

            // ---- the 'still needed' line earns its place or is not there ----
            {
                // It exists for a beat that wants two things: the moment a player most wants
                // to be told which one is left. On a beat that wants one thing it came out
                // directly under the objective saying the same words back - "Take your three
                // eggs back to Ori." over "Still needed: your three eggs, back to Ori" - which
                // teaches a player the second line is never worth reading, on the panel where
                // it sometimes is.
                check(HudView.ObjectiveText("Ch", "Take your three eggs back to Ori.",
                                            "your three eggs, back to Ori").IndexOf("Still needed") < 0,
                      "a blocker that restates the objective is dropped");
                check(HudView.ObjectiveText("Ch", "Find the two keepers.",
                                            "Marn on Voltacrest").Contains("Still needed"),
                      "a blocker that names something new is kept");
                check(HudView.ObjectiveText("Ch", "Find the two keepers.", null)
                          .IndexOf("Still needed") < 0,
                      "and no blocker means no line");

                // Every beat, walked with nothing done, and again with everything but one flag.
                // Wherever the line survives it has to be telling the player something the
                // objective above it does not.
                int kept = 0, dropped = 0;
                for (int b = 0; b < StoryDatabase.Beats.Length; b++)
                {
                    var probe = new StoryState();
                    probe.RestoreFrom(new string[0], b);
                    var beat = probe.Current;
                    var st = new GameState(false);

                    string blocker = probe.CurrentBlockerText(st);
                    string panel = HudView.ObjectiveText(beat.Chapter, beat.Objective, blocker);

                    check(panel.Contains(beat.Objective),
                          beat.Id + " still says what it wants");
                    check(panel.Contains(beat.Chapter), beat.Id + " still says which chapter");

                    if (panel.Contains("Still needed"))
                    {
                        kept++;
                        // The whole point: what survives has to add a word.
                        check(blocker != null && !HudView.ObjectiveText(beat.Chapter, beat.Objective, blocker)
                                  .Equals(HudView.ObjectiveText(beat.Chapter, beat.Objective, null)),
                              beat.Id + "'s still-needed line adds something: " + blocker);
                    }
                    else dropped++;

                    // And the panel is 524px at font 20 in a box 150 tall.
                    check(lines(panel, 524f, 20) <= capacity(150f, 20),
                          beat.Id + "'s objective panel fits (" + lines(panel, 524f, 20) +
                          " of " + capacity(150f, 20) + ")");
                }

                // Both outcomes have to happen on the real story, or the rule is decoration one
                // way or the other.
                check(kept > 0, "some beat genuinely needs the line (" + kept + ")");
                check(dropped > 0, "and some beat is better without it (" + dropped + ")");
            }

            // ---- standing still does not look like walking ----
            {
                // Teo used one bob rate and one amplitude for both, so a player who let go of
                // the keys kept bouncing at footfall pace. A character at rest and a character
                // mid-stride looked identical, on the sprite a player looks at more than any
                // other thing in the game.
                check(TeoController.IdleBobRate < TeoController.WalkBobRate,
                      "standing is slower than walking (" + TeoController.IdleBobRate +
                      " against " + TeoController.WalkBobRate + ")");
                check(TeoController.IdleBobRise < TeoController.WalkBobRise,
                      "and shallower");

                // Flying is its own thing: slow and loose, because nothing is pushing off
                // anything. It should not read as either of the other two.
                check(TeoController.FlyBobRate < TeoController.WalkBobRate,
                      "flight is not a walk");
                check(TeoController.FlyBobRise > TeoController.WalkBobRise,
                      "and drifts further than one");

                // Breathing, not bouncing, at rest - the same bound the eggs in a fight get.
                check(TeoController.IdleBobRise > 0f, "a standing Teo is still alive");
                check(TeoController.IdleBobRise < TeoController.WalkBobRise * 0.6f,
                      "and is visibly at rest rather than nearly walking");

                // The blend has to actually run from one to the other across the speeds the
                // game produces, or it is two states with a name in between.
                check(TeoController.Gait(0f) == 0f, "standing still is nought gait");
                check(TeoController.Gait(TeoController.WalkSpeed) == 1f, "walking flat out is full");
                check(TeoController.Gait(TeoController.WalkSpeed * 2f) == 1f,
                      "and nothing goes past it");

                float last = -1f;
                for (int i = 0; i <= 20; i++)
                {
                    float g = TeoController.Gait(TeoController.WalkSpeed * i / 20f);
                    check(g >= last, "gait only rises with speed");
                    last = g;
                }

                // And a half-speed walk is between the two, not at one end of them.
                float half = Mathf.Lerp(TeoController.IdleBobRate, TeoController.WalkBobRate,
                                        TeoController.Gait(TeoController.WalkSpeed * 0.5f));
                check(half > TeoController.IdleBobRate && half < TeoController.WalkBobRate,
                      "an amble is an amble (" + half.ToString("0.00") + ")");
            }

            // ---- neither egg sits on top of a card ----
            {
                // Four things on one screen, and each egg is diagonally opposite its own card:
                // the foe upper right with its card upper left, yours lower left with its card
                // on the right. That is what stops any of them covering another, and it was
                // four sets of numbers with nothing holding them apart.
                var foeEgg = BattleMode.FoeEggRect;
                var foeCard = BattleMode.FoeCardRect;
                var myEgg = BattleMode.MyEggRect;
                var myCard = BattleMode.MyCardRect;

                var all = new (string name, Rect r)[]
                {
                    ("the foe's egg", foeEgg), ("the foe's card", foeCard),
                    ("your egg", myEgg), ("your card", myCard),
                };

                for (int i = 0; i < all.Length; i++)
                {
                    check(all[i].r.xMin >= 0f && all[i].r.xMax <= 1920f,
                          all[i].name + " is on the screen horizontally");
                    check(all[i].r.yMin >= 0f && all[i].r.yMax <= 1080f,
                          all[i].name + " is on the screen vertically");

                    for (int j = i + 1; j < all.Length; j++)
                        check(!all[i].r.Overlaps(all[j].r),
                              all[i].name + " does not cover " + all[j].name);
                }

                // Diagonally opposite, which is the property the four numbers were chosen for
                // and the one a render can get wrong without anybody noticing.
                check((foeEgg.center.x > 960f) != (foeCard.center.x > 960f),
                      "the foe's egg is on the far side from its card");
                check((myEgg.center.x > 960f) != (myCard.center.x > 960f),
                      "and yours from its own");
                // Against each other, not against a midline. The first version asked whether
                // yours sat below the middle of the screen and it sits five pixels above it -
                // a line the layout was never built around, failing a design that is fine. What
                // was meant is that the foe is the further of the two, and it is, by 205px.
                check(myEgg.center.y < foeEgg.center.y,
                      "the foe is the one further away (" + foeEgg.center.y + " against " +
                      myEgg.center.y + ")");

                // Yours is nearer, so yours is bigger. A player looking at two eggs should be
                // able to tell which one is theirs without reading either card.
                check(myEgg.width > foeEgg.width,
                      "your egg is the larger of the two (" + myEgg.width + " against " +
                      foeEgg.width + ")");
            }

            // ---- the ending card and the world it hands back agree ----
            {
                // The card says "Vesper stays dark a while longer. Vess says she can wait, now
                // that it means something." Every cold pad in the sector warmed the moment Amy
                // stopped pulling on them, Vesper with them - so the last thing the game says
                // was contradicted by the first screen a player opens afterwards.
                //
                // The prose is the better version. Vesper staying dark is the only note in the
                // ending that is not a tidy resolution, and Vess waiting for it is the point
                // of her.
                var beaten = new StoryState();
                beaten.SetFlag("beat_amy");
                var before = new StoryState();

                string card = UiCopy.VictoryBody;

                // Any world the card says stays dark has to actually stay dark, and any world
                // it does not mention that was cold has to actually come back. This reads the
                // card rather than a list beside it, so rewriting the card without moving the
                // data fails here.
                int staysDark = 0, comesBack = 0;
                foreach (var w in PlanetDatabase.All)
                {
                    if (!PlanetDatabase.StationCold(w.Id)) continue;

                    bool cardSaysDark = card.Contains(w.Name + " stays dark");
                    bool actuallyDark = PlanetDatabase.StationColdNow(w.Id, true);

                    check(cardSaysDark == actuallyDark,
                          w.Name + ": the ending card and the world agree about the pad (card " +
                          (cardSaysDark ? "dark" : "silent") + ", world " +
                          (actuallyDark ? "dark" : "warm") + ")");

                    // And it was cold to begin with, or the card is describing a recovery from
                    // nothing.
                    check(PlanetDatabase.StationColdNow(w.Id, false),
                          w.Name + "'s pad is cold before Amy is beaten");

                    if (actuallyDark) staysDark++; else comesBack++;
                }

                check(comesBack > 0, "beating Amy warms something (" + comesBack + " pads)");
                check(staysDark > 0,
                      "and the ending's one unresolved note is still unresolved (" + staysDark + ")");

                // The chart says the same thing the ending does, in its own words.
                foreach (var w in PlanetDatabase.All)
                {
                    var been = new GameState(false);
                    been.Visited.Add(w.Id);
                    string after = GalaxyMapView.StationLine(w, been, beaten);
                    bool dark = PlanetDatabase.StationColdNow(w.Id, true);

                    check(after.Length > 0, w.Name + "'s pad still says something after Amy");
                    check(after.Contains("warm") != dark,
                          w.Name + "'s chart line matches its pad after Amy: " + after);

                    // Before she is beaten, every cold pad reads as a warning and no warm one does.
                    string early = GalaxyMapView.StationLine(w, been, before);
                    check(early.Contains("E55555") == PlanetDatabase.StationCold(w.Id),
                          w.Name + "'s chart line warns exactly when its pad is dead");
                }
            }

            // ---- an Elder says what an Elder is ----
            {
                // The panel printed the word ELDER and stopped, directly above a trait that
                // gets a name and a line saying what it does. A player who catches the rarest
                // thing on the world had no way to learn it was anything but a gold word.
                // Through WildElder, which is what actually rolls one - Elder is set-private on
                // purpose, and a check that reached around that would be testing a state the
                // game cannot produce.
                var elder = EggInstance.WildElder(SpeciesDatabase.All[0].Id, 20);
                var plain = EggInstance.Wild(SpeciesDatabase.All[0].Id, 20);
                check(elder.Elder, "WildElder makes an Elder");

                var st = new GameState(false);
                check(HudView.EggDetailText(elder).Contains("ELDER"), "an Elder is still marked as one");
                check(!HudView.EggDetailText(plain).Contains("ELDER"), "and an ordinary egg is not");

                string elderPanel = HudView.EggLoreText(elder, st);
                string plainPanel = HudView.EggLoreText(plain, st);
                check(elderPanel.Contains(EggInstance.ElderBlurb),
                      "and the panel below says what an Elder is");
                check(!plainPanel.Contains(EggInstance.ElderBlurb),
                      "only where there is one to explain");

                // Built from the constant, not restating it - the line and the roll cannot
                // drift apart. Garrow says the same number out loud on Cairnhold.
                check(EggInstance.ElderBlurb.Contains(Words.SpellCapitalised(EggInstance.ElderLevelBonus)),
                      "the blurb quotes the real bonus (" + EggInstance.ElderLevelBonus + "): " +
                      EggInstance.ElderBlurb);

                // And it explains rather than repeats, the same test the conditions get.
                check(!EggInstance.ElderBlurb.ToUpperInvariant().Contains("ELDER"),
                      "the blurb does not just say the word again: " + EggInstance.ElderBlurb);

                // The panel is 640px at font 20 and the Elder version is the tallest it gets.
                foreach (var e in new[] { elder, plain })
                    check(lines(HudView.EggLoreText(e, st), 640f, 20) <= capacity(700f, 20),
                          "the egg panel fits with everything it can carry (" +
                          lines(HudView.EggLoreText(e, st), 640f, 20) + " of " + capacity(700f, 20) + ")");

                // An Elder really is above its world, or the line is a lie. Wild() is what
                // rolls one, so ask it rather than the constant.
                for (int lv = 5; lv <= 30; lv += 5)
                {
                    var rolled = EggInstance.WildElder(SpeciesDatabase.All[0].Id, lv);
                    check(rolled.Level == Mathf.Min(EggInstance.MaxLevel, lv + EggInstance.ElderLevelBonus),
                          "an Elder really is " + EggInstance.ElderLevelBonus + " above its world at Lv " +
                          lv + " (got " + rolled.Level + ")");
                }
            }

            // ---- a condition says what it is costing, not just that it is there ----
            {
                // The card shows SCORCHED, CHILLED or DAZED, and the line that lands each one
                // explains it - once. Three turns later the chip is a word with no meaning
                // attached, and the player is picking a move without being reminded their egg
                // is moving at half speed.
                var well = EggInstance.Wild(SpeciesDatabase.All[0].Id, 20);
                check(BattleMode.ConditionLine(well, true).Length == 0,
                      "a healthy egg has nothing to report");
                check(BattleMode.ConditionLine(null, true).Length == 0,
                      "and neither does no egg at all");

                foreach (EggStatus st in System.Enum.GetValues(typeof(EggStatus)))
                {
                    if (st == EggStatus.None) continue;
                    var egg = EggInstance.Wild(SpeciesDatabase.All[0].Id, 20);
                    egg.Status = st;

                    string yours = BattleMode.ConditionLine(egg, true);
                    string theirs = BattleMode.ConditionLine(egg, false);

                    check(yours.Length > 0, st + " says something");
                    check(yours.Contains(egg.Name), "and names your egg: " + yours);
                    check(!theirs.Contains(egg.Name), "and does not name theirs: " + theirs);

                    // The whole point: the cost, not the label. A line that only repeats the
                    // word already on the card is the state again rather than the effect.
                    string plain = System.Text.RegularExpressions.Regex.Replace(yours, "<[^>]+>", "");
                    check(plain.Contains("\u2014"),
                          st + " separates the state from what it costs: " + plain);
                    check(plain.Length > egg.Name.Length + st.ToString().Length + 12,
                          st + " says more than the chip does: " + plain);

                    // It has to fit under the action description in the message box, which is
                    // 1040 wide and 210 tall at font 28.
                    for (int i = 0; i < BattleMode.ActionHelp.Length; i++)
                    {
                        string whole = BattleMode.ActionMessageText(i, egg, false) + "\n" + yours + "\n" + theirs;
                        check(lines(whole, 1040f, 28) <= capacity(210f, 28),
                              "action " + i + " plus two conditions still fits the box (" +
                              lines(whole, 1040f, 28) + " of " + capacity(210f, 28) + ")");
                    }
                }

                // Every condition the game can inflict gets a line. One that lands on a player
                // and says nothing is the state this replaces.
                foreach (EggStatus st in System.Enum.GetValues(typeof(EggStatus)))
                {
                    if (st == EggStatus.None) continue;
                    var egg = EggInstance.Wild(SpeciesDatabase.All[0].Id, 20);
                    egg.Status = st;
                    var other = EggInstance.Wild(SpeciesDatabase.All[1].Id, 20);
                    other.Status = st;
                    check(BattleMode.ConditionLine(egg, true) != BattleMode.ConditionLine(other, true),
                          st + "'s line names whichever egg has it");
                }
            }

            // ---- running out says where more come from ----
            {
                // The supply line says "Cartons none" in red, which is the state and not the
                // recovery. A player three worlds in has worked out that a Nest Station
                // restocks; a player on their second has not, and is the one it happens to.
                check(UiCopy.OutOf(false, false) == null,
                      "a player with supplies is told nothing");

                foreach (var pair in new[] { (true, false), (false, true), (true, true) })
                {
                    string line = UiCopy.OutOf(pair.Item1, pair.Item2);
                    check(line != null, "running out of something says so");
                    check(lines(line, 900f, 22) <= 2, "and fits the toast: " + line);

                    // The whole point: it names the recovery, not just the problem.
                    check(line.Contains("Nest Station"),
                          "and names where more come from: " + line);
                    check(line.Contains("rest"),
                          "and what to do there: " + line);

                    // It names what actually ran out, or a player with salves goes looking
                    // for a problem they do not have.
                    check(line.Contains("carton") == pair.Item1,
                          "cartons are mentioned exactly when they are gone: " + line);
                    check(line.Contains("salve") == pair.Item2,
                          "and salves likewise: " + line);
                }

                // Three different lines, or two of the three states are being told the wrong
                // thing about themselves.
                check(UiCopy.OutOf(true, false) != UiCopy.OutOf(false, true),
                      "cartons and salves do not share a line");
                check(UiCopy.OutOf(true, true) != UiCopy.OutOf(true, false),
                      "and running out of both is its own line");

                // And a station really does restock both, or the line is a lie.
                var dry = new GameState();
                dry.Cartons = 0;
                dry.Salves = 0;
                dry.RestockSupplies();
                check(dry.Cartons > 0 && dry.Salves > 0,
                      "resting at a station restocks what the line promises (" +
                      dry.Cartons + " cartons, " + dry.Salves + " salves)");
            }

            // ---- the game has more than one thing to say about losing ----
            {
                // It had exactly one, and it was about the temperature of the pad. Losing to a
                // wild Craggle on Mosswell and losing the fight the whole game builds to both
                // read "You woke at the Nest Station."
                foreach (bool cold in new[] { false, true })
                {
                    string wild = UiCopy.WokeAfter(null, cold);
                    string beaten = UiCopy.WokeAfter("Marn", cold);
                    check(wild == (cold ? UiCopy.WokeCold : UiCopy.WokeWarm),
                          "something wild that beat you leaves the pad line alone");
                    check(beaten != wild, "a person who beat you gets named");
                    check(beaten.Contains("Marn"), "and named by name: " + beaten);
                    check(beaten.Contains(cold ? "cold stone" : "pad was warm"),
                          "while still saying what the pad was like");
                }

                // Every resident who can beat you produces a line that fits a toast.
                foreach (var npc in StoryDatabase.Npcs)
                    foreach (bool cold in new[] { false, true })
                    {
                        string line = UiCopy.WokeAfter(npc.Name, cold);
                        check(lines(line, 900f, 22) <= 2,
                              "losing to " + npc.Name + " fits the toast: " + line);
                    }

                // Amy is not the same as anybody else. She is the reason every pad in the
                // sector is cold, so a line about the pad being warm is the one thing that
                // must not be the whole of it.
                check(UiCopy.WokeAfterAmy.Length >= 3,
                      "Amy has more than one thing to say about beating you (" +
                      UiCopy.WokeAfterAmy.Length + ")");
                foreach (var line in UiCopy.WokeAfterAmy)
                {
                    check(line != UiCopy.WokeWarm && line != UiCopy.WokeCold,
                          "Amy's defeat line is not the pad line: " + line);
                    check(!line.Contains("pad was warm"),
                          "and does not tell you the pad is warm on the world that made it cold");
                    check(lines(line, 900f, 22) <= 2, "Amy's defeat line fits the toast: " + line);
                }

                // All three different, or the rotation is decoration.
                for (int i = 0; i < UiCopy.WokeAfterAmy.Length; i++)
                    for (int j = i + 1; j < UiCopy.WokeAfterAmy.Length; j++)
                        check(UiCopy.WokeAfterAmy[i] != UiCopy.WokeAfterAmy[j],
                              "Amy does not repeat herself (" + i + " and " + j + ")");
            }

            // ---- a cry is as loud as the moment is worth ----
            {
                // Five places play one, and the ordering matters: something arriving to fight
                // you is the loudest, something becoming yours is next, one you sent in
                // yourself is quieter because you knew it was coming, and browsing a list is
                // quietest of all because it happens dozens of times a minute.
                check(AudioDirector.CryEncounter > AudioDirector.CryCaught,
                      "a wild egg arriving is louder than one becoming yours");
                check(AudioDirector.CryCaught > AudioDirector.CrySentOut,
                      "and that is louder than one you chose to send in");
                check(AudioDirector.CrySentOut > AudioDirector.CryRecord,
                      "and that is louder than reading the book");
                check(AudioDirector.CryRecord > AudioDirector.CryOwn,
                      "and that is louder than running the cursor down your own nest");

                // Nothing is so quiet it is not there, and nothing is at full - a cry lands
                // over music and often over an effect.
                foreach (var v in new[] { AudioDirector.CryEncounter, AudioDirector.CryCaught,
                                          AudioDirector.CrySentOut, AudioDirector.CryRecord,
                                          AudioDirector.CryOwn })
                {
                    check(v > 0.35f, "a cry is always audible (" + v + ")");
                    check(v <= 0.9f, "and never at full (" + v + ")");
                }

                // The quietest is heard dozens of times a minute against the loudest, which is
                // heard once a fight. Too wide a spread and the browsing one vanishes at a
                // volume where the arrival is comfortable.
                check(AudioDirector.CryEncounter / AudioDirector.CryOwn <= 2f,
                      "the quietest cry is not lost against the loudest (" +
                      (AudioDirector.CryEncounter / AudioDirector.CryOwn).ToString("0.00") + "x)");
            }

            // ---- the eggs in a fight are alive between turns ----
            {
                // The surface has bobbed its wildlife since it was written. The battle screen -
                // the one a player spends most of the game looking at - held both eggs
                // perfectly still for the whole of every fight, except for the third of a
                // second one of them was being hit.
                check(BattleMode.IdleRise > 0f, "an egg in a fight moves at all");

                // Breathing, not bouncing. The egg is 300-360px and this plays for the whole
                // fight; anything you would notice as motion is something you would notice for
                // ten minutes.
                check(BattleMode.IdleRise <= 12f,
                      "and not so much that it reads as bouncing (" + BattleMode.IdleRise + "px)");
                check(BattleMode.IdleRate > 0.6f && BattleMode.IdleRate < 2.5f,
                      "at the pace of something breathing (" + BattleMode.IdleRate + "/s)");

                // Two eggs that rise together read as one object being lifted. The warming ring
                // on the surface learned this with six of them.
                float apart = Mathf.Abs(Mathf.Sin(BattleMode.MinePhase) - Mathf.Sin(BattleMode.FoePhase));
                check(apart > 0.5f,
                      "the two eggs do not move as one object (phases " + BattleMode.MinePhase +
                      " and " + BattleMode.FoePhase + " differ by " + apart.ToString("0.00") + ")");

                // Over a whole fight the phases must not drift into agreement either - they are
                // driven off one clock at one rate, so this is a property of the constants.
                float worst = 0f;
                for (int frame = 0; frame < 600; frame++)
                {
                    float t = frame / 60f * BattleMode.IdleRate;
                    float gap = Mathf.Abs(Mathf.Sin(t + BattleMode.MinePhase) - Mathf.Sin(t + BattleMode.FoePhase));
                    worst = Mathf.Max(worst, gap);
                }
                check(worst > 1.0f,
                      "and are visibly apart at some point in every cycle (" + worst.ToString("0.00") + ")");
            }

            // ---- a nickname cannot break the panel it is drawn in ----
            {
                // Everything typed at the naming prompt is interpolated straight into rich
                // text - the party strip, the battle log, every toast, the ending card - and
                // into the save file. A nickname of "<b>" bolds the rest of the line it lands
                // in. One containing "</color>" ends the colour it was wrapped in and repaints
                // whatever follows. "<3" is a name somebody types on their first run.
                var nasty = new[]
                {
                    "<b>", "</color>", "<color=#FF0000>", "<3", "a<b>b", "<size=99>",
                    "</b></b></b>", "<", ">", "<<<<<<<<<<<<<<<<",
                };
                foreach (var raw in nasty)
                {
                    string clean = NameEntryView.Clean(raw);
                    check(clean == null || clean.IndexOf('<') < 0,
                          "a nickname never carries a '<' out of the prompt: \"" + raw + "\" -> " +
                          (clean ?? "(nothing)"));
                    check(clean == null || clean.IndexOf('>') < 0,
                          "nor a '>': \"" + raw + "\"");
                }

                // Ordinary names survive intact. A filter that eats real names is worse than
                // the problem it solves.
                foreach (var good in new[] { "Pebbles", "Sir Yolk", "O'Neil", "Half-Shell", "R2", "a" })
                    check(NameEntryView.Clean(good) == good,
                          "\"" + good + "\" is left alone (got \"" + (NameEntryView.Clean(good) ?? "null") + "\")");

                // Nothing usable in means nothing out, and the egg keeps its species name.
                foreach (var empty in new[] { "", "   ", "<>", "\t\t", (string)null })
                    check(NameEntryView.Clean(empty) == null,
                          "a name with nothing in it stays unnamed");

                // The cap holds however it is reached.
                check(NameEntryView.Clean(new string('x', 40)).Length == NameEntryView.MaxLength,
                      "a long name is cut to " + NameEntryView.MaxLength);
                check(NameEntryView.Clean("a          b") == "a b",
                      "runs of spaces collapse (got \"" + NameEntryView.Clean("a          b") + "\")");
                check(NameEntryView.Clean("  Pebbles  ") == "Pebbles",
                      "a name does not keep the space you leaned on");

                // Whatever comes out has to survive being drawn where names are drawn. The
                // party row is the tightest of them.
                foreach (var raw in nasty)
                {
                    string clean = NameEntryView.Clean(raw);
                    if (clean == null) continue;
                    var egg = EggInstance.Wild(SpeciesDatabase.All[0].Id, 30);
                    egg.Nickname = clean;
                    string row = HudView.PartyRow(egg, true);
                    int opens = 0, closes = 0;
                    foreach (var ch in row) { if (ch == '<') opens++; if (ch == '>') closes++; }
                    check(opens == closes,
                          "a nicknamed party row has balanced markup (\"" + clean + "\")");
                    check(lines(row, 524f, 20) == 1,
                          "and still fits the strip (\"" + clean + "\")");
                }

                // Loading is the other door into this field, and a save file is a text file.
                // One edited by hand, or written by a build from before names were filtered,
                // can carry markup straight back into every panel the name appears in.
                foreach (var raw in nasty)
                {
                    var loaded = EggInstance.Restore(SpeciesDatabase.All[0].Id, raw, 12, 0, 30,
                                                     null, null);
                    check(loaded.Name.IndexOf('<') < 0 && loaded.Name.IndexOf('>') < 0,
                          "a loaded save cannot smuggle markup into a name: \"" + raw +
                          "\" -> \"" + loaded.Name + "\"");
                }

                // The hint under the field carries the count. A limit a player only discovers
                // by hitting it reads as the game having stopped working.
                // Only states the prompt can be in. A keystroke can only be refused when the
                // field is already full, so "refused with two characters typed" is a case the
                // game cannot reach and a case the check has no business failing on.
                for (int used = 0; used <= NameEntryView.MaxLength; used++)
                    foreach (bool refused in used >= NameEntryView.MaxLength
                                                 ? new[] { false, true } : new[] { false })
                    {
                        string hint = NameEntryView.Hint(used, refused);
                        check(hint.Contains(used + "/" + NameEntryView.MaxLength),
                              "the naming hint shows " + used + " of " + NameEntryView.MaxLength);
                        check(lines(hint, 840f, 20) == 1, "the naming hint fits its line: " + hint);
                    }
                check(NameEntryView.Hint(NameEntryView.MaxLength, true) !=
                      NameEntryView.Hint(NameEntryView.MaxLength, false),
                      "a refused keystroke says so rather than doing nothing");
            }

            // ---- the chart knows which pads are dead ----
            {
                // Four stations have gone cold and the chart never said so - on the screen
                // where you decide where to fly, about the place you heal and restock. It is
                // also the plot, and the plot has a payoff: after Amy they come back.
                var fresh = new GameState(false);
                var early = new StoryState();
                var after = new StoryState();
                after.SetFlag("beat_amy");

                int cold = 0, warm = 0, returned = 0;
                foreach (var w in PlanetDatabase.All)
                {
                    // Nothing is announced before it is found. Same rule as the cache and the
                    // landmark on this panel.
                    check(GalaxyMapView.StationLine(w, fresh, early).Length == 0,
                          w.Name + "'s pad is not described before you have stood on it");

                    var been = new GameState(false);
                    been.Visited.Add(w.Id);

                    string before = GalaxyMapView.StationLine(w, been, early);
                    string later  = GalaxyMapView.StationLine(w, been, after);
                    check(before.Length > 0, w.Name + "'s pad says something once you have been");
                    check(lines(before, 720f, 22) == 1, w.Name + "'s pad line fits: " + before);
                    check(lines(later, 720f, 22) == 1, w.Name + "'s later pad line fits: " + later);

                    if (PlanetDatabase.StationCold(w.Id))
                    {
                        cold++;
                        check(before.Contains("E55555"),
                              w.Name + "'s dead pad reads as a warning");
                        // The payoff. If this ever stops changing, beating Amy stops being
                        // visible anywhere a player would look for it.
                        check(later != before, w.Name + "'s pad comes back after Amy");
                        check(!later.Contains("E55555"), w.Name + "'s pad stops warning after Amy");
                        returned++;
                    }
                    else
                    {
                        warm++;
                        check(!before.Contains("E55555"), w.Name + "'s live pad is not a warning");
                        check(later == before,
                              w.Name + " was never cold, so beating Amy does not change it");
                    }
                }

                // Both states have to exist on the real roster, or one branch is decoration.
                check(cold > 0, "some world in the game has a cold station (" + cold + ")");
                check(warm > 0, "and some world does not (" + warm + ")");
                check(returned == cold, "every cold pad comes back (" + returned + " of " + cold + ")");

                // The chart and the surface have to agree about which pads are dead. The
                // surface gates its cold pad on the same story flag; two lists would drift.
                foreach (var id in PlanetDatabase.ColdStations)
                    check(PlanetDatabase.Exists(id), "cold station " + id + " is a real world");
                check(PlanetDatabase.StationCold(PlanetDatabase.Home.Id),
                      "your own station is one of the cold ones - it is what starts the game");
            }

            // ---- the chart says where you stand against a world ----
            {
                // The panel said "Wild eggs Lv 14-18" and never said what yours are. So the
                // chart described seventeen worlds in detail and answered none of the question
                // a player opens it with - whether this is a good place for them right now.
                // GameState() is not empty - a new run already carries Sprouteg, which is the
                // whole opening of the game. The first version of this block built its fixtures
                // with it and then added an egg, so Leader stayed the starter and every level it
                // claimed to be testing was really level five. Test data invented rather than
                // read fails the same way every time.
                check(GalaxyMapView.Readiness(PlanetDatabase.Home, new GameState(false)).Length == 0,
                      "a chart with no eggs on it says nothing about your lead");

                // Both readings of "comfortable" have to be the same number, or the chart and
                // the descent prompt tell a player different things about the same decision.
                check(GalaxyMapView.ComfortGap == SpaceMode.AmyComfortableGap,
                      "the chart and the descent prompt agree on what comfortable means (" +
                      GalaxyMapView.ComfortGap + " against " + SpaceMode.AmyComfortableGap + ")");

                foreach (var w in PlanetDatabase.All)
                {
                    // Every level a lead can actually be, against every world.
                    string last = null;
                    int changes = 0;
                    for (int lv = 1; lv <= EggInstance.MaxLevel; lv++)
                    {
                        var st = new GameState(false);
                        st.Party.Add(EggInstance.Wild(SpeciesDatabase.All[0].Id, lv));
                        string line = GalaxyMapView.Readiness(w, st);

                        check(line.Length > 0, w.Name + " says something at Lv " + lv);
                        check(line.Contains("Lv " + lv), w.Name + " quotes the real level at Lv " + lv);
                        check(lines(line, 720f, 22) == 1,
                              w.Name + "'s readiness fits the panel at Lv " + lv + ": " + line);

                        // Red is for the one case that changes a decision. A world you are
                        // merely a little under is not a warning.
                        bool red = line.Contains("E55555");
                        check(red == (lv + GalaxyMapView.ComfortGap < w.MinLevel),
                              w.Name + " warns exactly when your lead is under everything on it " +
                              "(Lv " + lv + ", band " + w.MinLevel + "-" + w.MaxLevel + ")");

                        if (line != last) changes++;
                        last = line;
                    }

                    // The line has to move as you level, or it is decoration.
                    check(changes >= 3,
                          w.Name + "'s readiness changes as you level (" + changes + ")");
                }

                // And no band may be unreachable. "changes >= 4 per world" was here instead,
                // which is a number picked rather than a property: deleting the "a little over"
                // band entirely left every world still changing four times and the check passed.
                // The question worth asking is the same one the portrait marks get asked -
                // does some real combination in the game actually produce each of these?
                var bands = new[] { "under everything here", "a shade under the low end",
                                    "in among them", "a little over", "Nothing here will trouble you" };
                foreach (var band in bands)
                {
                    bool reached = false;
                    foreach (var w in PlanetDatabase.All)
                        for (int lv = 1; lv <= EggInstance.MaxLevel && !reached; lv++)
                        {
                            var st = new GameState(false);
                            st.Party.Add(EggInstance.Wild(SpeciesDatabase.All[0].Id, lv));
                            if (GalaxyMapView.Readiness(w, st).Contains(band)) reached = true;
                        }
                    check(reached, "some world and some level actually reads \"" + band + "\"");
                }
            }

            // ---- the selected world says so in more than colour ----
            {
                // The chart's whole job is picking one of seventeen things, and the halo that
                // said which was tinted in that world's own theme colour. So "selected" was a
                // slightly brighter blur of the colour already there - hue, and only hue, on
                // the one screen where getting it wrong sends you to the wrong planet.
                //
                // Every other list in the game marks its cursor in amber and with a caret. The
                // party strip even runs the leader's dot to full row height so the mark is not
                // colour either. This is that rule, applied to the screen that needed it most.
                var stM = new GameState();
                var storyM = new StoryState();
                foreach (var w in PlanetDatabase.All)
                {
                    string suffix = GalaxyMapView.MarkerSuffix(w, stM, storyM,
                                                               GalaxyMapView.Presence.Elsewhere);
                    string off = GalaxyMapView.MarkerLabel(w, suffix, true, false);
                    string on  = GalaxyMapView.MarkerLabel(w, suffix, true, true);

                    check(off != on, w.Name + "'s marker changes when it is selected");

                    // The falsifiable half: strip every colour tag and the two must still
                    // differ. A selection that survives only in the tags is a selection a
                    // dichromat player cannot see, and the halo was exactly that.
                    string plainOff = System.Text.RegularExpressions.Regex.Replace(off, "<[^>]+>", "");
                    string plainOn  = System.Text.RegularExpressions.Regex.Replace(on,  "<[^>]+>", "");
                    // Trimmed. Whitespace is not a mark: deleting the caret and leaving the
                    // empty colour tags behind still changed the string, by one space, and this
                    // passed. A difference a player cannot see is not a difference.
                    check(plainOff.Trim() != plainOn.Trim(),
                          w.Name + "'s selection survives having the colour taken out of it");

                    // And the mark is the one every other cursor in the game uses.
                    check(on.Contains("\u25b8"), w.Name + " is marked with the same caret as everything else");
                    check(on.Contains("#FFC24D"), w.Name + " is marked in the same amber as everything else");

                    // The label still has to fit the space under a marker.
                    foreach (var line in plainOn.Split('\n'))
                        check(lines(line, 420f, 22) == 1,
                              w.Name + "'s selected marker still fits: \"" + line + "\"");
                }
            }

            // ---- the nest can be reordered without losing track of an egg ----
            {
                // Fifty-odd eggs in the order you happened to catch them is the one arrangement
                // that makes the question you opened the screen to ask - is there anything in
                // here worth swapping in - hard to answer.
                //
                // Reordering a list that three separate things index into by position is where
                // this goes wrong quietly: the detail panel, N to name, and Enter to swap all
                // read the row under the cursor, and all three used to reach straight into the
                // nest. So the checks are about identity, not about sort keys.
                var st = new GameState();
                var ids = new System.Collections.Generic.List<string>();
                foreach (var sp in SpeciesDatabase.All) ids.Add(sp.Id);
                for (int i = 0; i < 47; i++)
                    st.Nest.Add(EggInstance.Wild(ids[(i * 7) % ids.Count], 3 + (i * 5) % 34));
                // Deliberate ties. Forty-seven distinct eggs never exercise the tiebreak, and a
                // sort that reorders equal rows is exactly what a nest full of duplicate
                // Sproutegs at the same level would show a player.
                for (int i = 0; i < 6; i++) st.Nest.Add(EggInstance.Wild(ids[0], 12));

                foreach (HudView.NestOrder order in System.Enum.GetValues(typeof(HudView.NestOrder)))
                {
                    var rows = HudView.NestRows(st, order);

                    check(rows.Count == st.Nest.Count,
                          order + " shows every egg in the nest (" + rows.Count + " of " + st.Nest.Count + ")");

                    // Every egg exactly once. A sort that drops or doubles one is a sort that
                    // loses an egg a player spent a fight catching.
                    var seen = new System.Collections.Generic.HashSet<int>();
                    foreach (var idx in rows)
                    {
                        check(idx >= 0 && idx < st.Nest.Count, order + " points at a real egg");
                        check(seen.Add(idx), order + " shows egg " + idx + " only once");
                    }

                    // The row a player is looking at holds the egg they think it holds. This is
                    // the one that would have been silently wrong.
                    for (int row = 0; row < rows.Count; row++)
                        check(ReferenceEquals(HudView.NestEggAt(st, st.Party.Count + row, order),
                                              st.Nest[rows[row]]),
                              order + " row " + row + " is the egg it is showing");

                    // Stable: the same order twice must be the same list, or the nest reshuffles
                    // under the cursor between one frame and the next.
                    var again = HudView.NestRows(st, order);
                    for (int i = 0; i < rows.Count; i++)
                        check(rows[i] == again[i], order + " is the same list every time it is built");

                    // And stable in the sense that matters: eggs the order cannot separate stay
                    // in the sequence they were caught. Removing the tiebreak from the comparer
                    // survived the check above - List.Sort is deterministic for identical input,
                    // so "twice the same" says nothing about it. This is what it protects.
                    for (int i = 1; i < rows.Count; i++)
                    {
                        var a = st.Nest[rows[i - 1]];
                        var b = st.Nest[rows[i]];
                        bool tied = a.Level == b.Level && a.MaxHP == b.MaxHP &&
                                    a.Species.Name == b.Species.Name && a.Type == b.Type;
                        if (tied)
                            check(rows[i - 1] < rows[i],
                                  order + " keeps eggs it cannot separate in the order they were " +
                                  "caught (" + rows[i - 1] + " before " + rows[i] + ")");
                    }

                    check(HudView.NestOrderName(order).Length > 0, order + " says what it is");
                }

                // Each order actually orders by what it claims.
                var strong = HudView.NestRows(st, HudView.NestOrder.Strongest);
                for (int i = 1; i < strong.Count; i++)
                    check(st.Nest[strong[i - 1]].Level >= st.Nest[strong[i]].Level,
                          "strongest-first really is strongest first");

                var byType = HudView.NestRows(st, HudView.NestOrder.Element);
                for (int i = 1; i < byType.Count; i++)
                    check((int)st.Nest[byType[i - 1]].Type <= (int)st.Nest[byType[i]].Type,
                          "by-element groups the elements");

                var bySpecies = HudView.NestRows(st, HudView.NestOrder.Species);
                for (int i = 1; i < bySpecies.Count; i++)
                    check(string.CompareOrdinal(st.Nest[bySpecies[i - 1]].Species.Name,
                                                st.Nest[bySpecies[i]].Species.Name) <= 0,
                          "by-species groups the species");

                var caught = HudView.NestRows(st, HudView.NestOrder.Caught);
                for (int i = 0; i < caught.Count; i++)
                    check(caught[i] == i, "caught-order is the order they were caught");

                // Cycling reaches every order and comes back. A cycle that skips one leaves a
                // sort nobody can select and a check nobody notices is dead.
                var reached = new System.Collections.Generic.HashSet<HudView.NestOrder>();
                var at = HudView.NestOrder.Caught;
                for (int i = 0; i < 4; i++) { reached.Add(at); at = HudView.NextOrder(at); }
                check(at == HudView.NestOrder.Caught, "S cycles back round to where it started");
                check(reached.Count == System.Enum.GetValues(typeof(HudView.NestOrder)).Length,
                      "S reaches every order (" + reached.Count + ")");

                // And the column still fits with the order line in it.
                int nestScroll = 0;
                foreach (HudView.NestOrder order in System.Enum.GetValues(typeof(HudView.NestOrder)))
                {
                    string column = HudView.CollectionBodyText(st, st.Party.Count + 30, true, ref nestScroll, order);
                    check(lines(column, 780f, 20) <= capacity(660f, 20),
                          "the nest column fits with " + order + " named in it (" +
                          lines(column, 780f, 20) + " of " + capacity(660f, 20) + ")");
                    check(column.Contains(HudView.NestOrderName(order)),
                          "the column says which order it is in (" + order + ")");
                }
            }

            // ---- the clearance rule is big enough to be worth having ----
            {
                // Separate from the per-world measurement, and it has to be: seventeen sets of
                // seeds landing far enough apart says nothing about whether the rule that put
                // them there is sound. The eighteenth world, or a reseeded one, is decided by
                // the constant rather than by luck.
                float reach = SurfaceLayout.CacheRange + SurfaceLayout.LandmarkRange;
                check(SurfaceLayout.LandmarkClearance > reach,
                      "the landmark clearance exceeds what both prompts can reach (" +
                      SurfaceLayout.LandmarkClearance + " against " + reach.ToString("0.0") + ")");

                // The rule has to be one some world actually needs. A branch no roster reaches
                // is a branch nobody has tested, and this one rescues exactly one world -
                // Mosswell, from 5.0u to 44.0u. If a reseed ever leaves every world clear, this
                // says so rather than letting the push quietly become decoration.
                int pushed = 0; string who = "";
                foreach (var w in PlanetDatabase.All)
                {
                    if (!PlanetDatabase.HasCache(w.Id)) continue;
                    if (Vector2.Distance(SurfaceLayout.CachePosition(w),
                                         SurfaceLayout.RawLandmarkPosition(w)) < SurfaceLayout.LandmarkClearance)
                    { pushed++; who = w.Name; }
                }
                check(pushed > 0,
                      "some world actually needs the clearance push (" + pushed + ", " + who + ")");

                // And a landmark pushed to the far side has to still be on the world.
                foreach (var w in PlanetDatabase.All)
                {
                    var lm = SurfaceLayout.LandmarkPosition(w);
                    check(lm.magnitude <= w.SurfaceRadius,
                          w.Name + "'s landmark stays on the world (" + lm.magnitude.ToString("0.0") +
                          " of " + w.SurfaceRadius + ")");
                    if (PlanetDatabase.HasCache(w.Id))
                        check(SurfaceLayout.CachePosition(w).magnitude <= w.SurfaceRadius,
                              w.Name + "'s cache stays on the world");
                }
            }

            // ---- the number and the bar say the same thing ----
            {
                // TakeDamage lands before AnimateHit runs, and RefreshCards only ran when the
                // slide finished - so for a third of a second the bar showed a third full
                // while the text beside it still read 120/135. The two things on the card whose
                // whole job is saying the same number, saying different ones, on every hit.
                // Fractions built from whole hit points, because that is the only kind an egg
                // can have. The first version fed in maxHp 1 with a target of half a bar and the
                // check duly failed - on a state no egg in the game can be in. Test data made up
                // out of the air fails the same way a threshold made up out of the air does.
                //
                // Sampled at 200 steps, not 20. Planting a small wobble into the lerp went
                // uncaught at 20: the animation runs about twenty frames, so twenty samples felt
                // like the honest number, and it is - for what a player sees. It is not enough to
                // prove a claim about the whole slide, and the claim is what is written down.
                //
                // The rise cases end on thirds and sevenths on purpose. Every earlier pair landed
                // on a whole fraction, where to * maxHp is exact in float and the k >= 1 guard is
                // unreachable - so removing that guard also went uncaught. 100/135 is where it
                // earns its place.
                foreach (int maxHp in new[] { 1, 7, 24, 135, 400 })
                    foreach (var pair in new[] { (1f, 0f), (1f, 0.5f), (0.5f, 0f), (0.34f, 0.31f),
                                                 (0.4f, 1f), (1f / 7f, 3f / 7f), (100f / 135f, 134f / 135f),
                                                 (2f / 3f, 1f / 3f) })
                    {
                        int fromHp = Mathf.RoundToInt(pair.Item1 * maxHp);
                        int toHp = Mathf.RoundToInt(pair.Item2 * maxHp);
                        float from = fromHp / (float)maxHp, to = toHp / (float)maxHp;

                        int last = BattleMode.HpShown(from, to, 0f, maxHp);
                        for (int step = 0; step <= 200; step++)
                        {
                            float k = step / 200f;
                            int hp = BattleMode.HpShown(from, to, k, maxHp);

                            check(hp >= 0 && hp <= maxHp,
                                  "the count stays inside the egg (" + hp + " of " + maxHp + ")");

                            // It only ever moves the way the bar is moving. A number that ticks
                            // back up mid-slide reads as a second, smaller heal.
                            if (toHp < fromHp) check(hp <= last, "the count only falls while the bar falls");
                            else check(hp >= last, "the count only rises while the bar rises");
                            last = hp;

                            // Which side of the bar it rounds to, said as a property rather than
                            // named as a function. Swapping Ceil for Floor went uncaught without
                            // this: the number came out one low all the way down a fall, which
                            // is not zero and not backwards, so nothing above it noticed.
                            // Departing value, not arriving - a hit should never under-report
                            // what the egg still has.
                            float exact = Mathf.Lerp(from, to, k) * maxHp;
                            if (toHp < fromHp)
                                check(hp >= exact - 0.001f,
                                      "a falling count never reads under the bar (" + hp +
                                      " against " + exact.ToString("0.00") + ")");
                            else
                                check(hp <= exact + 0.001f,
                                      "a rising count never reads over the bar (" + hp +
                                      " against " + exact.ToString("0.00") + ")");
                        }

                        // It lands on the truth, not on a rounding of it. This is the one that
                        // matters: the last frame of the slide and the RefreshCards after it
                        // must agree, or the number twitches once the animation stops.
                        check(BattleMode.HpShown(from, to, 1f, maxHp) == toHp,
                              "the count ends on the egg's real health (" + maxHp + " hp, " +
                              fromHp + " to " + toHp + ")");

                        // And nothing reads zero before the bar is empty. An egg showing 0 HP
                        // and still standing is the game telling a player it has fainted when it
                        // has not.
                        //
                        // Falling slides only. On a heal the number lags behind the fill by
                        // design - it rounds toward the value being left - so mending a fainted
                        // egg genuinely reads 0 until the first whole point is back, which is
                        // true rather than a lie. The check said "never zero" and had to mean
                        // "never zero while it is being hurt".
                        if (toHp > 0 && toHp < fromHp)
                            for (int step = 0; step < 200; step++)
                                check(BattleMode.HpShown(from, to, step / 200f, maxHp) > 0,
                                      "a surviving egg never shows zero mid-slide (" +
                                      fromHp + " to " + toHp + " of " + maxHp + ")");
                    }
            }

            // ---- the XP bar takes the path the egg actually took ----
            {
                // The HP bar has eased since it was written; the XP bar under it snapped, and
                // when an award crossed a level it snapped backwards - from most of the way
                // along to nearly empty, with nothing in between. The bar completing is the
                // whole payoff of a levelling system and it never once happened on screen.
                //
                // The path is computed before the award because GainXp mutates the egg on the
                // spot. So the arithmetic exists twice, in two files, and the only thing worth
                // checking is that they agree - against the real egg, not against itself.
                foreach (var species in SpeciesDatabase.All)
                    for (int lv = 1; lv <= 40; lv += 7)
                        foreach (int award in new[] { 0, 1, 12, 40, 96, 300, 4000 })
                        {
                            var egg = EggInstance.Wild(species.Id, lv);
                            int startLevel = egg.Level, startXp = egg.Xp;

                            var path = XpFill.Path(startLevel, startXp, EggInstance.MaxLevel, award);
                            egg.GainXp(award, null);

                            check(path.Count > 0,
                                  "there is always something to draw (" + species.Id + " lv" + lv + " +" + award + ")");

                            // Every stretch runs forwards, and each one starts where the last
                            // ended - or at nothing, if the last one filled the bar.
                            for (int i = 0; i < path.Count; i++)
                            {
                                check(path[i].To >= path[i].From,
                                      "the XP bar never runs backwards (" + species.Id + " +" + award + ")");
                                check(path[i].From >= 0f && path[i].To <= 1f,
                                      "the XP bar stays inside itself (" + species.Id + " +" + award + ")");
                                if (i > 0)
                                    check(Mathf.Approximately(path[i].From, XpFill.Rests(path[i - 1])),
                                          "the XP bar does not jump between stretches (" + species.Id + " +" + award + ")");
                                if (path[i].Levels)
                                    check(Mathf.Approximately(path[i].To, 1f),
                                          "a level only lands when the bar is full (" + species.Id + " +" + award + ")");
                            }

                            // And it has to stop where the egg actually is. This is the one that
                            // catches the two curves drifting apart.
                            int need = XpFill.Need(egg.Level, EggInstance.MaxLevel);
                            float real = need <= 0 ? 1f : egg.Xp / (float)need;
                            float ends = XpFill.Rests(path[path.Count - 1]);
                            check(Mathf.Abs(ends - real) < 0.001f,
                                  "the XP bar stops where the egg is (" + species.Id + " lv" + lv +
                                  " +" + award + ": bar " + ends.ToString("0.000") +
                                  ", egg " + real.ToString("0.000") + ")");

                            // A big award must not turn into a long wait.
                            check(path.Count <= XpFill.MostSteps,
                                  "no award takes more than " + XpFill.MostSteps + " stretches (" +
                                  species.Id + " +" + award + " took " + path.Count + ")");
                            check(path.Count * XpFill.SecondsFor(path.Count) <= 1.3f,
                                  "the whole fill is over inside a beat and a half (" +
                                  (path.Count * XpFill.SecondsFor(path.Count)).ToString("0.00") + "s)");
                        }

                // XpToNext on a live egg and Need() off it are the same curve, or the bar draws
                // one thing while the egg counts another.
                for (int lv = 1; lv <= EggInstance.MaxLevel; lv++)
                {
                    var egg = EggInstance.Wild(SpeciesDatabase.All[0].Id, lv);
                    check(egg.XpToNext == XpFill.Need(egg.Level, EggInstance.MaxLevel),
                          "the bar and the egg agree on level " + lv + " (" + egg.XpToNext +
                          " against " + XpFill.Need(egg.Level, EggInstance.MaxLevel) + ")");
                }
            }

            // ---- the dialogue hint says what the key actually does ----
            {
                // It read "Space to continue" at every moment of every line - including while
                // the line was still typing, where the key skips the reveal rather than
                // continuing anything, and on the last line of a script, where it continues to
                // nothing because the box closes. Two small lies on the prompt a player reads
                // more often than any other text in the game.
                check(DialogueView.HintText(false, false) != DialogueView.HintText(true, false),
                      "a line still typing does not offer to continue");
                check(DialogueView.HintText(false, true) == DialogueView.HintText(false, false),
                      "while it is typing, the last line reads like any other");
                check(DialogueView.HintText(true, true) != DialogueView.HintText(true, false),
                      "the last line of a script does not promise another one");
                foreach (bool rev in new[] { false, true })
                    foreach (bool last in new[] { false, true })
                    {
                        string hint = DialogueView.HintText(rev, last);
                        check(hint.StartsWith("Space"), "every hint names the key: " + hint);
                        check(lines(hint, 500f, DialogueView.HintFont) == 1,
                              "the hint fits its corner: " + hint);
                    }

                // The portrait, the speaker and the body all have to fit beside each other.
                check(DialogueView.PortraitX + DialogueView.PortraitSize <= DialogueView.TextX,
                      "the portrait stops before the text starts");
                check(DialogueView.TextX + DialogueView.BodyWidth <= DialogueView.BoxWidth,
                      "the body stays inside the box");
                check(-DialogueView.PortraitY + DialogueView.PortraitSize <= DialogueView.BoxHeight,
                      "the portrait stays inside the box");

                // The body's floor against the hint's ceiling. These overlapped by six pixels
                // once, which is why the body is 180 tall and not 190.
                float bodyFloor = DialogueView.BoxHeight + DialogueView.BodyY - DialogueView.BodyHeight;
                float hintCeiling = DialogueView.HintY + 26f;
                check(bodyFloor >= hintCeiling,
                      "the body clears the hint (" + Mathf.RoundToInt(bodyFloor) + " against " +
                      Mathf.RoundToInt(hintCeiling) + ")");

                // And the speaker's name above the body it introduces.
                float speakerFloor = DialogueView.BoxHeight + DialogueView.SpeakerY - 38f;
                float bodyTop = DialogueView.BoxHeight + DialogueView.BodyY;
                check(speakerFloor >= bodyTop, "the speaker's name clears their line");
            }

            // ---- every person in the game has their own face ----
            {
                // ProcArt.Portrait took a name, used it to key the cache, and drew the same
                // hooded silhouette for everybody. The roster is five purples deep - Vess,
                // Moth, Wren, Lune, Pim - which collapse further for a dichromat player, so a
                // cast separated only by tint was a cast some players could not separate.
                var names = new System.Collections.Generic.List<string> { "Teo", "Amy" };
                foreach (var npc in StoryDatabase.Npcs)
                    if (!names.Contains(npc.Name)) names.Add(npc.Name);

                float closest = 999f; string pairA = "", pairB = "";
                for (int i = 0; i < names.Count; i++)
                    for (int j = i + 1; j < names.Count; j++)
                    {
                        float d = PortraitForm.Distance(PortraitForm.For(names[i]),
                                                        PortraitForm.For(names[j]));
                        if (d < closest) { closest = d; pairA = names[i]; pairB = names[j]; }
                    }
                check(closest >= PortraitForm.MinDistance,
                      "no two people share a face - closest are " + pairA + " and " + pairB +
                      " at " + closest.ToString("0.000") + " against " + PortraitForm.MinDistance);

                // Every mark is worn by somebody, or a shape ships that nobody ever sees. This
                // is a fact about the roster, which is why the marks are authored: hashed, a
                // bucket of six comes up empty about a quarter of the time with seventeen names,
                // and no amount of better mixing changes that.
                foreach (PortraitMark m in System.Enum.GetValues(typeof(PortraitMark)))
                {
                    int worn = 0;
                    foreach (var n in names) if (PortraitForm.For(n).Mark == m) worn++;
                    check(worn > 0, "somebody in the cast wears the " + m + " mark");
                    check(worn <= names.Count / 3,
                          "the " + m + " mark is not most of the cast (" + worn + " of " + names.Count + ")");
                }

                // Everybody who speaks has a face decided on purpose rather than by hash.
                foreach (var n in names)
                    check(PortraitForm.IsAuthored(n), n + "'s mark is authored, not drawn from a hash");

                // A face has to be the same on Tuesday. The first draft asserted this by
                // comparing a form with itself, which is true of any function ever written; the
                // thing that can actually break is the hash, so the hash is what gets pinned.
                // string.GetHashCode is explicitly allowed to differ between runs and platforms.
                check(PortraitForm.Hash("Ori") == 145266615,
                      "the portrait hash is the one the faces were drawn from (Ori = " +
                      PortraitForm.Hash("Ori") + ")");
                check(PortraitForm.Hash("") == (unchecked((int)2166136261u) & 0x7FFFFFFF),
                      "the hash starts from the FNV offset basis");

                // Vess is one person standing on two worlds. Portraits are keyed on the spoken
                // name, so the face follows for free - what does not follow is the tint, which
                // is written out twice in the roster and could drift on one of them.
                foreach (var a in StoryDatabase.Npcs)
                    foreach (var b in StoryDatabase.Npcs)
                        if (a.Name == b.Name)
                            check(a.Tint == b.Tint,
                                  a.Name + " is the same colour on every world they stand on");

                // The drawing has to use the face. Planting `For(key)` -> `For(\"Ori\")` back
                // into ProcArt went uncaught the first time: the checks proved the forms
                // differed and nothing proved anything drew them. Shapes() is now the only
                // source of geometry either side has, so this covers both.
                for (int i = 0; i < names.Count; i++)
                    for (int j = i + 1; j < names.Count; j++)
                    {
                        var sa = PortraitForm.Shapes(PortraitForm.For(names[i]));
                        var sb = PortraitForm.Shapes(PortraitForm.For(names[j]));
                        bool same = sa.Length == sb.Length;
                        for (int k = 0; same && k < sa.Length; k++)
                            same = sa[k].Cx == sb[k].Cx && sa[k].Cy == sb[k].Cy &&
                                   sa[k].Rx == sb[k].Rx && sa[k].Ry == sb[k].Ry;
                        check(!same, names[i] + " and " + names[j] + " are not drawn identically");
                    }

                // Every face is made of something, and of the same things: a backdrop, shoulders,
                // a hood, a head, two eyes and two glints, plus whatever mark they wear.
                foreach (var n in names)
                {
                    var shapes = PortraitForm.Shapes(PortraitForm.For(n));
                    check(shapes.Length >= 8, n + " is drawn from a whole figure (" + shapes.Length + " parts)");
                    int eyes = 0, glints = 0;
                    foreach (var b in shapes) { if (b.Eyes) eyes++; if (b.Glint) glints++; }
                    check(eyes == 2, n + " has two eyes");
                    check(glints == 2, n + " has a light in both of them");
                }

                // Nothing may grow outside the 1x1 portrait square.
                foreach (var n in names)
                {
                    var f = PortraitForm.For(n);
                    check(f.HoodRise + f.HoodHeight * 1.24f <= 1f,
                          n + "'s crest stays inside the portrait");
                    check(f.EyeSpacing * 1.5f + 0.035f <= f.HeadWidth,
                          n + "'s temple marks stay on their face");
                    check(f.EyeSpacing + f.EyeSize <= f.HeadWidth * 0.92f,
                          n + "'s eyes stay on their face");
                }
            }

            // ---- the pause menu is a list, not four centred sentences ----
            {
                // This screen had never been rendered. Drawing it showed every row laid out
                // from its own width: nothing lined up, the caret slid sideways down the list,
                // and on the settings page the values change width - so toggling sound on to
                // off, or stepping text speed to "instant", moved the row sideways underneath
                // the cursor while the player was looking straight at it.
                Func<string, float> w = t =>
                    System.Text.RegularExpressions.Regex.Replace(t ?? "", "<[^>]+>", "").Length
                    * PauseView.RowFont * 0.52f;

                // Every reachable state of both pages, not one flattering sample.
                var pages = new System.Collections.Generic.List<(string label, string value)[]>();
                pages.Add(PauseView.MainRows(false));
                pages.Add(PauseView.MainRows(true));
                for (int sp = 0; sp < GameState.TextSpeedCount; sp++)
                    foreach (bool muted in new[] { false, true })
                        foreach (bool motion in new[] { false, true })
                            pages.Add(PauseView.SettingsRows(
                                muted, 0.6f, 1f, motion, new GameState { TextSpeed = sp }.TextSpeedName));

                float widestLabel = 0f, widestValue = 0f;
                foreach (var page in pages)
                    foreach (var row in page)
                    {
                        check(row.label.Length > 0, "every pause row has a label");
                        widestLabel = Mathf.Max(widestLabel, w(row.label));
                        widestValue = Mathf.Max(widestValue, w(row.value));
                    }

                check(widestLabel <= PauseView.LabelWidth,
                      "the widest pause label fits its column (" + Mathf.CeilToInt(widestLabel) +
                      " of " + PauseView.LabelWidth + ")");
                check(widestValue <= PauseView.ValueWidth,
                      "the widest pause value fits its column (" + Mathf.CeilToInt(widestValue) +
                      " of " + PauseView.ValueWidth + ")");

                // The two columns must not run into each other, or aligning them bought nothing.
                check(PauseView.LabelX + widestLabel <= PauseView.ValueX,
                      "the label column stops before the value column starts (" +
                      Mathf.CeilToInt(PauseView.LabelX + widestLabel) + " against " + PauseView.ValueX + ")");
                check(PauseView.GutterX + PauseView.GutterWidth <= PauseView.LabelX,
                      "the caret gutter stops before the labels start");
                check(PauseView.ValueX + PauseView.ValueWidth <= PauseView.RowWidth * 0.5f,
                      "the value column stays inside the box");

                // The defect itself: what the cursor is pointing at must not move when the
                // value beside it changes. Labels are the anchor, so they are what gets fixed.
                for (int i = 0; i < pages[0].Length; i++)
                    check(pages[0][i].label == pages[1][i].label,
                          "confirming a quit does not reword the row (" + pages[0][i].label + ")");
                for (int p = 3; p < pages.Count; p++)
                    for (int i = 0; i < pages[2].Length; i++)
                        check(pages[2][i].label == pages[p][i].label,
                              "settings row " + i + " reads the same whatever it is set to");

                // The heading rule the title and ending cards follow, applied to the one other
                // screen in the game with a heading over a block of text.
                // Pivots, not centres. UIKit.Place's second argument is the pivot, and every box
                // on this screen pivots from its top edge - so an offset of -38 is where the box
                // starts, not where it is centred. The first version of these three checks did
                // the centre arithmetic and measured a layout the game does not have. They still
                // passed, by two pixels, which is the worst way for a wrong check to behave.
                float headingFloor = -38f - 48f;
                check(headingFloor - PauseView.FirstRowY >= UiLayout.MinHeadingGap,
                      "PAUSED has air under it before the first row (" +
                      Mathf.RoundToInt(headingFloor - PauseView.FirstRowY) + "px)");

                // Six settings rows, then the footer. The main page also has a rule and the
                // controls block between them, and both pages share one box that must not move.
                float lastSettingRow = PauseView.FirstRowY - 5 * PauseView.RowStep - PauseView.RowHeight;
                float footerTop = -PauseView.BoxHeight + 42f + 60f;
                check(lastSettingRow > footerTop,
                      "the last setting clears the footer (" + Mathf.RoundToInt(lastSettingRow) +
                      " against " + Mathf.RoundToInt(footerTop) + ")");

                float lastMainRow = PauseView.FirstRowY - 3 * PauseView.RowStep - PauseView.RowHeight;
                check(lastMainRow > PauseView.RuleY, "the last main row clears the rule");
                float lastControl = PauseView.ControlsY - (UiCopy.PauseControls.Length - 1) *
                                    PauseView.ControlsStep - 28f;
                check(lastControl > footerTop, "the controls block clears the footer");

                foreach (var line in UiCopy.PauseControls)
                    check(lines(line, PauseView.RowWidth, (int)PauseView.ControlsFont) == 1,
                          "a pause controls line fits on one line: " + line);
            }

            // ---- the action menu explains itself ----
            {
                // The move menu has explained whichever move is highlighted since it was
                // written. One level up, the same box carried a single line above six hundred
                // pixels of nothing - on the screen a player makes every decision in the game
                // on, and where the mechanics actually get taught.
                var mine = EggInstance.Wild("sprouteg", 20);
                mine.CurrentHP = mine.MaxHP / 2;

                for (int i = 0; i < BattleMode.ActionHelp.Length; i++)
                {
                    string wild = BattleMode.ActionMessageText(i, mine, false);
                    check(wild.Length > 0, "action " + i + " says what it does");
                    check(lines(wild, 1040f, 28) <= capacity(210f, 28),
                          "action " + i + "'s line fits the message box: " + wild);
                }

                // The two the game refuses against a trainer say why, rather than sitting
                // greyed with no reason given.
                check(BattleMode.ActionMessageText(1, mine, true) != BattleMode.ActionHelp[1],
                      "the carton says why it is greyed against a trainer");
                check(BattleMode.ActionMessageText(4, mine, true) != BattleMode.ActionHelp[4],
                      "and so does running");

                // And the salve, which is greyed for a reason a player might not guess.
                var whole = EggInstance.Wild("sprouteg", 20);
                check(BattleMode.ActionMessageText(2, whole, false).Contains("not hurt"),
                      "the salve says why it is greyed on an untouched egg: " +
                      BattleMode.ActionMessageText(2, whole, false));
                check(BattleMode.ActionMessageText(2, mine, false) == BattleMode.ActionHelp[2],
                      "and explains itself normally on a hurt one");

                // One line per button in the menu, or the indices stop lining up.
                check(BattleMode.ActionHelp.Length == 5,
                      "there is a line for every action (" + BattleMode.ActionHelp.Length + ")");
            }

            // ---- running low says so in a word too ----
            {
                // Empty was given a word because a figure that reads the same to somebody the
                // colour does not reach is not telling them anything. The state just above
                // empty - the one where a player can still do something about it - was left
                // in hue alone.
                var st = new GameState(false);
                foreach (var id in PlanetDatabase.CacheWorlds.Keys) st.Caches.Add(id);

                st.Cartons = HudView.LowSupply; st.Salves = GameState.MaxSalves;
                string low = HudView.SuppliesLine(st);
                check(low.Contains("low"), "a low stack says so: " + low.Split('\n')[0]);

                st.Cartons = st.MaxCartons;
                check(!HudView.SuppliesLine(st).Contains("low"),
                      "a full stack does not");

                st.Cartons = 0;
                // The supplies row only. The second row carries "Types 0/8" on a fresh state,
                // which is a zero this rule has nothing to say about.
                string gone = HudView.SuppliesLine(st).Split('\n')[0];
                check(gone.Contains("Cartons none") && !gone.Contains("Cartons 0"),
                      "and empty still reads as none rather than a zero: " + gone);

                // The threshold has to leave a player somewhere to act. At one, the warning
                // arrives with a single throw left in the stack.
                check(HudView.LowSupply >= 2,
                      "the warning arrives with something still in the stack (" +
                      HudView.LowSupply + ")");

                // And the widest form still fits the 524px strip.
                st.Cartons = HudView.LowSupply; st.Salves = HudView.LowSupply;
                foreach (var row in HudView.SuppliesLine(st).Split('\n'))
                    check(lines(row, 524f, 19) == 1,
                          "the strip fits with both stacks low: " + row);
            }

            // ---- nothing in the menu takes an input to refuse it ----
            {
                // The salve has greyed itself out when it would be wasted since it was
                // written, with a comment saying why. The carton did not until two commits ago
                // and swapping did not until this one - it took the input and then said "No
                // other egg is in any shape to fight."
                var alone = new GameState(false);
                alone.Party.Add(EggInstance.Wild("sprouteg", 12));
                check(!BattleMode.CanSwap(alone, 0),
                      "a party of one cannot swap");

                var pair = new GameState(false);
                pair.Party.Add(EggInstance.Wild("sprouteg", 12));
                pair.Party.Add(EggInstance.Wild("cobblet", 12));
                check(BattleMode.CanSwap(pair, 0), "a party of two can");

                pair.Party[1].CurrentHP = 0;
                check(!BattleMode.CanSwap(pair, 0),
                      "and cannot once the other one is out cold");

                // The egg already out does not count as somebody to swap to.
                var three = new GameState(false);
                for (int i = 0; i < 3; i++) three.Party.Add(EggInstance.Wild("sprouteg", 12));
                three.Party[0].CurrentHP = 0;
                three.Party[2].CurrentHP = 0;
                check(!BattleMode.CanSwap(three, 1),
                      "the one already standing there is not a swap target");

                // The line it used to spend an input on still exists, for the case where an
                // egg faints and the game has to say why nobody is coming out.
                check(System.Array.IndexOf(UiCopy.BattleLines, UiCopy.NobodyLeft) >= 0,
                      "the refusal line is still there for when it is the only thing to say");
            }

            // ---- an empty stack says so ----
            {
                // The supplies strip was fixed a while back so that running out reads as a
                // word rather than a zero. The battle's own action menu still said
                // "CARTON (0)" - and unlike the salve beside it, which greys itself out when
                // it would be wasted, an empty carton stack stayed selectable and told the
                // player it was empty only after they had spent the input on it.
                check(BattleMode.SupplyAction("CARTON", 12) == "CARTON (12)",
                      "a full stack shows its count");
                check(BattleMode.SupplyAction("CARTON", 0) == "CARTON (none)",
                      "and an empty one says none: " + BattleMode.SupplyAction("CARTON", 0));
                check(!BattleMode.SupplyAction("SALVE", 0).Contains("(0)"),
                      "the salve says it the same way");

                // It reads the same way the strip does, which is the point of doing it twice.
                var empty = new GameState(false);
                empty.Cartons = 0; empty.Salves = 0;
                string strip = HudView.SuppliesLine(empty);
                check(strip.Contains("none") && BattleMode.SupplyAction("CARTON", 0).Contains("none"),
                      "the strip and the battle menu use the same word for empty");

                // 286px buttons at font 20 - the widest label is a full stack after every
                // cache has been dug.
                var rich = new GameState(false);
                foreach (var id in PlanetDatabase.CacheWorlds.Keys) rich.Caches.Add(id);
                check(lines(BattleMode.SupplyAction("CARTON", rich.MaxCartons), 286f, 20) == 1,
                      "the fullest carton label fits its button: " +
                      BattleMode.SupplyAction("CARTON", rich.MaxCartons));
            }

            // ---- one number, one format ----
            {
                // An egg's health appeared on four screens in three forms: "45 / 135" on the
                // battle plate and the egg panel, "45/135" in the collection list, "45/135 HP"
                // on the swap row. The same number about the same egg, spaced three ways.
                var egg = EggInstance.Wild("sprouteg", 20);
                egg.CurrentHP = egg.MaxHP / 3;
                string want = UiCopy.Health(egg.CurrentHP, egg.MaxHP);

                check(!want.Contains(" "), "health is written without spaces: " + want);
                check(want.Contains("/"), "and as a fraction");

                var st = new GameState(false);
                foreach (var sp0 in SpeciesDatabase.All) { st.Seen.Add(sp0.Id); st.Caught.Add(sp0.Id); }

                check(HudView.DescribeStoredEgg(egg).Contains(want),
                      "the collection list writes it that way: " + HudView.DescribeStoredEgg(egg));
                check(HudView.EggLoreText(egg, st).Contains(want),
                      "the egg panel writes it that way");
                check(BattleMode.SwapRow(egg, EggInstance.Wild("yolkano", 20), false).Contains(want),
                      "the swap row writes it that way");

                // A full-health egg and a fainted one, since those are the two a player looks
                // at hardest.
                var full = EggInstance.Wild("cobblet", 20);
                check(UiCopy.Health(full.CurrentHP, full.MaxHP) == full.MaxHP + "/" + full.MaxHP,
                      "a full egg reads as its own maximum twice");
                var down = EggInstance.Wild("cobblet", 20);
                down.CurrentHP = 0;
                check(UiCopy.Health(down.CurrentHP, down.MaxHP).StartsWith("0/"),
                      "and a fainted one starts at zero");
            }

            // ---- both screens point at the same egg ----
            {
                // The surface HUD marks the egg that is actually going out first. The
                // collection highlighted slot one. Those are the same egg right up until a
                // player's favourite gets knocked out, and then they are not.
                var st = new GameState(false);
                st.Party.Add(EggInstance.Wild("sprouteg", 12));
                st.Party.Add(EggInstance.Wild("cobblet", 12));
                st.Party.Add(EggInstance.Wild("yolkano", 12));
                st.Party[0].CurrentHP = 0;

                int scroll = 0;
                string column = HudView.CollectionBodyText(st, 0, true, ref scroll);

                // The leader's slot number is in the accent colour; the others are not.
                int leaderSlot = st.Party.IndexOf(st.Leader) + 1;
                check(leaderSlot == 2, "the leader really is the second egg here (" + leaderSlot + ")");
                check(column.Contains("<color=#FFC24D>2</color>"),
                      "the collection marks the egg that is going out first");
                check(!column.Contains("<color=#FFC24D>1</color>"),
                      "and does not mark the fainted one at the top");

                // And it agrees with the strip, which is the whole point.
                check(HudView.PartyRow(st.Party[1], true).Contains("\u25b8"),
                      "the party strip marks the same egg");
                check(!HudView.PartyRow(st.Party[0], false).Contains("\u25b8"),
                      "and not the fainted one either");
            }

            // ---- the chart and the sky agree ----
            {
                // Two screens describing the same seventeen worlds, disagreeing about what a
                // player is allowed to know. The space label gave an uncharted world's level
                // range and the chart withheld it; the chart marked a world you had walked and
                // not finished and the space label did not.
                var charted = new GameState(false);
                var late = new StoryState();
                late.RestoreFrom(new string[0], StoryDatabase.Beats.Length - 1);
                foreach (var w in PlanetDatabase.All) charted.Visited.Add(w.Id);

                var nothing = new GameState(false);

                foreach (var w in PlanetDatabase.All)
                {
                    string uncharted = GalaxyMapView.MarkerSuffix(w, nothing, late,
                                                                  GalaxyMapView.Presence.Elsewhere);
                    if (nothing.Visited.Contains(w.Id)) continue;

                    // Level yes, element no: survey data against what you learn by going.
                    check(uncharted.Contains("Lv " + w.MinLevel + "-" + w.MaxLevel),
                          w.Name + " shows its level range before you have been: " + uncharted);
                    check(!uncharted.Contains(TypeChart.Abbrev(w.Theme)),
                          w.Name + " still keeps its element back");

                    // And it still fits the 240px label with the range on it.
                    check(lines(uncharted, 240f, 15) == 1,
                          w.Name + "'s uncharted label fits: " + uncharted);
                }

                // A charted world that still owes you something is marked on the chart; the
                // space label now carries the same ring, keyed on the same count.
                foreach (var w in PlanetDatabase.All)
                {
                    if (w.Spawns == null || w.Spawns.Length == 0) continue;
                    string marked = GalaxyMapView.MarkerSuffix(w, charted, late,
                                                               GalaxyMapView.Presence.Elsewhere);
                    bool owes = charted.UnrecordedOn(w) > 0;
                    check(marked.Contains("\u25cb") == owes,
                          w.Name + "'s ring matches what it still owes (" +
                          charted.UnrecordedOn(w) + " unrecorded)");
                }
            }

            // ---- nothing defined goes unused ----
            {
                // Dead content is quiet: a sound nobody plays, an effect no move has, a shell
                // pattern no species wears. It costs nothing at runtime and it is invisible in
                // review, which is exactly why it accumulates. Checked as a sweep rather than
                // by hand, because the hand version only ever gets run once.
                foreach (MoveEffect e in System.Enum.GetValues(typeof(MoveEffect)))
                {
                    if (e == MoveEffect.None) continue;
                    bool used = false;
                    foreach (var mv in MoveDatabase.All) if (mv.Effect == e) used = true;
                    check(used, "some move uses the " + e + " effect");
                }

                foreach (EggTrait t in System.Enum.GetValues(typeof(EggTrait)))
                {
                    if (t == EggTrait.None) continue;
                    bool worn = false;
                    foreach (EggType ty in System.Enum.GetValues(typeof(EggType)))
                        if (TypeChart.TraitOf(ty) == t) worn = true;
                    check(worn, "some element carries the " + t + " trait");
                }

                foreach (EggPattern pat in System.Enum.GetValues(typeof(EggPattern)))
                {
                    // Auto is a sentinel, not a pattern: it is the default argument meaning
                    // "pick one from the seed", and the constructor resolves it before anything
                    // sees it. That no species keeps it is asserted where the resolution happens.
                    if (pat == EggPattern.Auto) continue;

                    bool worn = false;
                    foreach (var sp in SpeciesDatabase.All) if (sp.Pattern == pat) worn = true;
                    check(worn, "some species wears the " + pat + " shell");
                }

                // And every element is on at least one species, or a whole column of the type
                // chart would be unreachable.
                foreach (EggType ty in System.Enum.GetValues(typeof(EggType)))
                {
                    if (ty == EggType.Plain) continue;   // moves only, never a species
                    bool exists = false;
                    foreach (var sp in SpeciesDatabase.All) if (sp.Type == ty) exists = true;
                    check(exists, "some species is " + TypeChart.Name(ty));
                }
            }

            // ---- the Belt is stripped, and then it is not ----
            {
                // Garrow, on Cairnhold, after Amy: "The old ones are coming out of the rock
                // again. Big, lit up round the shell. Years since the Belt had Elders in it."
                //
                // Which means the Belt is not Elder-rich - it is the sector nearest the thing
                // draining the sector, stripped for eleven years, and the old ones come back
                // when the warmth does. I built it the other way round first, on a half-
                // remembered line, and it would have contradicted the only character who
                // mentions Elders at all.
                var reach = PlanetDatabase.Get("yolkhaven");
                var belt = PlanetDatabase.Get("cairnhold");

                check(EggInstance.ElderChanceOn(belt, false) < EggInstance.ElderChanceOn(reach, false),
                      "the Belt has fewer Elders than the Reach while the cold is on");
                check(EggInstance.ElderChanceOn(belt, true) > EggInstance.ElderChanceOn(reach, true),
                      "and more of them once the warmth is back");
                check(Mathf.Approximately(EggInstance.ElderChanceOn(reach, false),
                                          EggInstance.ElderChanceOn(reach, true)),
                      "the Reach is unchanged either way - Garrow was talking about his own sector");

                // Commoner is not common: the deeper stir that warns of an Elder has to stay
                // worth noticing even at the Belt's best.
                check(EggInstance.ElderChanceOn(belt, true) < 0.25f,
                      "and are still rare enough for the deep stir to mean something (" +
                      (EggInstance.ElderChanceOn(belt, true) * 100f).ToString("0") + "%)");

                // The line is his, and it is in the set he says after Amy - not before.
                var garrow = StoryDatabase.NpcById("garrow");
                check(PlanetDatabase.Get(garrow.PlanetId).Sector == Sector.ShatteredBelt,
                      "Garrow lives in the sector he is talking about");

                var after = new StoryState();
                after.RestoreFrom(new[] { "beat_amy" }, StoryDatabase.Beats.Length - 1);
                var later = new System.Text.StringBuilder();
                foreach (var l in StoryDatabase.GetDialogue("garrow", after, new GameState()).Lines)
                    later.Append(l.Text).Append(' ');
                check(later.ToString().Contains("Years since the Belt had Elders"),
                      "and says it after Amy, which is what makes the drought the right way round");

                var before = new System.Text.StringBuilder();
                foreach (var l in StoryDatabase.GetDialogue("garrow", new StoryState(), new GameState()).Lines)
                    before.Append(l.Text).Append(' ');
                check(!before.ToString().Contains("coming out of the rock again"),
                      "and not before it");
            }
            // ---- the first egg looks like one ----
            {
                // The title screen's opening line: every world is a piece of shell, the core of
                // it is still up there and still whole, and eleven years ago it started to
                // split. Amaranth is that core, and it was drawn as one more coloured disc on
                // the screen a player stares at for the whole game.
                int bosses = 0; string bossName = "";
                foreach (var w in PlanetDatabase.All)
                    if (w.IsBossWorld) { bosses++; bossName = w.Name; }

                check(bosses == 1,
                      "exactly one world is the first egg (" + bosses + ")");
                check(bossName == "Amaranth Prime",
                      "and it is the one the story is about: " + bossName);

                // The seam is drawn from IsBossWorld, so the thing that makes it look cracked
                // and the thing that puts Amy on it are one fact rather than two.
                var amaranth = PlanetDatabase.Get("amaranth");
                check(amaranth != null && amaranth.IsBossWorld,
                      "the world Amy is on is the world drawn with a seam");

                // And its own landmark is the seam, so the picture and the inscription agree.
                var shellLine = LandmarkDatabase.For("amaranth");
                check(shellLine != null && shellLine.Form == LandmarkForm.Seam,
                      "and the landmark you can walk to there is a seam too");
            }

            // ---- eggs gather where the pad is alive ----
            {
                // Ori's rule is that a Nest Station runs warm off the eggs around it. The game
                // states it, draws the cold pads grey, lets it explain why resting still works
                // on them, and shows your own nest doing the warming - and the wild eggs, the
                // ones the rule is actually about, took no notice of a station at all.
                check(SurfaceMode.StationPull > 0f,
                      "a living station draws the eggs around it (" + SurfaceMode.StationPull + ")");

                // Weak enough to be a tendency over a minute rather than a pull. At this rate
                // a roamer needs several seconds to turn even halfway toward the pad, which is
                // what keeps it from overriding bolting and coming-to-look.
                check(SurfaceMode.StationPull < 0.5f,
                      "and does it gently (" + SurfaceMode.StationPull + " per second)");

                // The four cold worlds are the ones where it must not happen, and they are the
                // same four the pads are drawn grey on - one fact, not two.
                int coldCount = 0;
                foreach (var w in PlanetDatabase.All)
                    if (PlanetDatabase.StationCold(w.Id)) coldCount++;
                check(coldCount > 0 && coldCount < PlanetDatabase.All.Count,
                      "some worlds gather and some do not (" + coldCount + " cold of " +
                      PlanetDatabase.All.Count + ")");

                // Including the one a new player starts on, so the difference is visible from
                // the first world rather than only after the Belt.
                check(PlanetDatabase.StationCold(PlanetDatabase.Home.Id),
                      PlanetDatabase.Home.Name + " is cold, so a new player sees the empty version first");
            }

            // ---- the battle's own lines ----
            {
                // They were written inline in BattleMode, so they were outside the prose pass
                // and outside every fit check - twenty-two lines a player reads more often than
                // any dialogue in the game.
                foreach (var line in UiCopy.BattleLines)
                    check(lines(line, 1040f, 28) <= capacity(210f, 28),
                          "the battle line fits its box: " + line);

                // The array and the constants are the same set - it would be a poor trade to
                // give them a home and then have the pass read a stale copy of it.
                var known = new HashSet<string>(UiCopy.BattleLines);
                foreach (var one in new[] { UiCopy.ElderWarning, UiCopy.OutOfCartons,
                                            UiCopy.OutOfSalves, UiCopy.NobodyLeft,
                                            UiCopy.FledSafely, UiCopy.CouldNotFlee,
                                            UiCopy.Missed, UiCopy.Critical,
                                            UiCopy.NoEffect, UiCopy.AllOutCold })
                    check(known.Contains(one),
                          "the prose pass sees this battle line: " + one);

                check(UiCopy.BattleLines.Length == 10,
                      "every battle line in the set is accounted for (" +
                      UiCopy.BattleLines.Length + ")");
            }

            // ---- a cache is worth seeing arrive ----
            {
                // Four worlds hide one, it is not signposted, and finding it takes a deliberate
                // walk out past the shell fields. Then it vanished between frames with a line
                // of text. The burst is half warm and half the ground it came out of, so it has
                // to read against that ground the way everything else on a surface does.
                foreach (var id in PlanetDatabase.CacheWorlds.Keys)
                {
                    var w = PlanetDatabase.Get(id);
                    var bit = TeoController.StepMoteTint(w.Land);
                    float gap = Mathf.Abs(SurfaceMode.Luminance(bit) - SurfaceMode.Luminance(w.Land));
                    check(gap > 0.10f,
                          "the ground half of " + w.Name + "'s cache burst reads against its ground (" +
                          gap.ToString("0.00") + ")");
                }

                // And the reward itself is real: every cache world gives capacity, and the
                // total is what the supplies strip promises.
                var st = new GameState(false);
                int baseline = st.MaxCartons;
                foreach (var id in PlanetDatabase.CacheWorlds.Keys) st.Caches.Add(id);
                check(st.MaxCartons > baseline,
                      "digging every cache raises what you can carry (" + baseline + " to " +
                      st.MaxCartons + ")");

                foreach (var id in PlanetDatabase.CacheWorlds.Keys)
                {
                    var one = new GameState(false);
                    one.Caches.Add(id);
                    check(one.MaxCartons > baseline,
                          PlanetDatabase.Get(id).Name + "'s cache is worth carrying capacity");
                }
            }

            // ---- a shell field acknowledges you ----
            {
                // Encounters only happen inside these patches and nothing confirmed you were
                // in one. The stir did not begin until fifty-five percent of the way to a
                // fight, so most of the time spent in a shell field looked exactly like
                // walking on bare ground - on the one mechanic the whole catching loop rests on.
                check(SurfaceMode.InsideLift > 0f,
                      "standing in a field shows on the field (" + SurfaceMode.InsideLift + ")");

                // Well below the stir, or the warning and the acknowledgement read the same.
                check(SurfaceMode.InsideLift < 0.4f,
                      "and is far quieter than the stir that means something is coming (" +
                      SurfaceMode.InsideLift + ")");

                // The lift and the stir share a scale, so the stir has to have somewhere to go
                // above the floor - otherwise a field that is about to give looks like one you
                // have just walked into.
                check(1f - SurfaceMode.InsideLift > 0.5f,
                      "the stir has room to build above it (" +
                      (1f - SurfaceMode.InsideLift).ToString("0.00") + " of range left)");
            }

            // ---- the stir before an encounter ----
            {
                // A field turning something up used to be a cut with no tell at all. The stir
                // has to last long enough to be a warning rather than a flicker, and the
                // shortest walk is the one that decides it.
                float stirShort = SurfaceMode.EncounterWalkMin * (1f - SurfaceMode.StirBegins);
                float stirLong = SurfaceMode.EncounterWalkMax * (1f - SurfaceMode.StirBegins);
                float shortSeconds = stirShort / TeoController.WalkSpeed;
                float longSeconds = stirLong / TeoController.WalkSpeed;

                check(shortSeconds > 0.2f,
                      "the shortest stir is long enough to notice (" + shortSeconds.ToString("0.00") + "s)");
                check(longSeconds < 1.5f,
                      "the longest stir does not outstay its welcome (" + longSeconds.ToString("0.00") + "s)");

                // And it must not be so early that a field is stirring most of the time you
                // are standing in one, which would make it noise rather than a signal.
                check(SurfaceMode.StirBegins > 0.4f,
                      "a field is calm for most of the walk across it (stirs from " +
                      (SurfaceMode.StirBegins * 100f).ToString("0") + "%)");
            }

            // ---- roamer temperament ----
            {
                // Split on the median, so both behaviours ship no matter how the roster grows.
                // A threshold written down as a number would drift out from under the species
                // the first time somebody retuned speed across the board.
                int bold = 0, skittish = 0;
                foreach (var sp in SpeciesDatabase.All)
                    if (SpeciesDatabase.IsSkittish(sp.Id)) skittish++; else bold++;

                check(bold > 0 && skittish > 0,
                      "some eggs run and some come to look (" + bold + " bold, " + skittish + " skittish)");
                check(Mathf.Min(bold, skittish) >= SpeciesDatabase.All.Count / 4,
                      "neither temperament is a rarity (" + bold + " / " + skittish + " of " +
                      SpeciesDatabase.All.Count + ")");

                // Not "every world has both" - that was the rule I nearly imposed, and the
                // roster is better than it. Speed rises with tier, so the whole Hatchery Reach
                // comes to have a look at you while you are still learning to walk, and the two
                // Volt worlds are the opposite: nothing on Voltacrest or Arcmoor will let you
                // near. That is a change of texture worth keeping, not a gap worth filling.
                var home = PlanetDatabase.Home;
                foreach (var sp in home.Spawns)
                    check(!SpeciesDatabase.IsSkittish(sp.SpeciesId),
                          "nothing on " + home.Name + " runs from a new player: " +
                          SpeciesDatabase.Get(sp.SpeciesId).Name);

                int allShy = 0;
                foreach (var w in PlanetDatabase.All)
                {
                    if (w.Spawns == null || w.Spawns.Length < 3) continue;
                    bool everyOne = true;
                    foreach (var sp in w.Spawns)
                        if (!SpeciesDatabase.IsSkittish(sp.SpeciesId)) everyOne = false;
                    if (everyOne) allShy++;
                }
                check(allShy > 0,
                      "somewhere out there is a world where nothing lets you near (" + allShy + " of them)");
            }

            // ---- the number Ori asks for ----
            {
                // The objective line spells FirstCatchEggs from the constant. Ori says "Three"
                // in plain prose, three times over, and nothing connected the two: moving the
                // constant to four left him asking for three while the objective under it asked
                // for four, and the game would have been telling a new player two numbers in
                // the same breath.
                string want = Words.Spell(StoryDatabase.FirstCatchEggs);

                var brief = StoryDatabase.GetDialogue("ori", new StoryState(), new GameState());
                var said = new System.Text.StringBuilder();
                foreach (var line in brief.Lines) said.Append(line.Text).Append(' ');

                check(said.ToString().ToLowerInvariant().Contains(want.ToLowerInvariant()),
                      "Ori asks for " + want + " eggs in words, the same as the objective does in numbers");

                // And the nag he gives you for turning up short.
                var short_ = new StoryState();
                short_.RestoreFrom(new[] { "met_ori" }, 0);
                var nag = StoryDatabase.GetDialogue("ori", short_, new GameState());
                var nagText = new System.Text.StringBuilder();
                foreach (var line in nag.Lines) nagText.Append(line.Text).Append(' ');
                check(nagText.ToString().ToLowerInvariant().Contains(want.ToLowerInvariant()),
                      "Ori's reminder asks for " + want + " too: " + nagText.ToString().Trim());
            }

            // ---- what characters say a trait does ----
            {
                // Three lines in the game explain a trait in a character's own words. They are
                // right today; they were checked by hand. What they are not is connected to the
                // traits, so retuning one would leave a resident confidently describing an
                // effect the game no longer has, and nothing would say so.
                //
                // Each entry is: the phrase somebody says, the element it is about, and a word
                // the real trait blurb has to keep. Both halves have to survive together.
                var claims = new[]
                {
                    ("mend themselves as they fight", EggType.Verdant, "Mends"),
                    ("will not go down from full health", EggType.Stone, "full health"),
                    ("can't be rattled", EggType.Void, "lowered"),
                };

                var everything = new System.Text.StringBuilder();
                foreach (var npc in StoryDatabase.Npcs)
                {
                    var script = StoryDatabase.GetDialogue(npc.Id, new StoryState(), new GameState());
                    if (script == null) continue;
                    foreach (var line in script.Lines) everything.Append(line.Text).Append('\n');
                }
                string spoken = everything.ToString();

                foreach (var (phrase, type, keyword) in claims)
                {
                    check(spoken.Contains(phrase),
                          "somebody still says \"" + phrase + "\"");
                    string blurb = TypeChart.TraitBlurb(TypeChart.TraitOf(type));
                    check(blurb.Contains(keyword),
                          TypeChart.Name(type) + "'s trait still does what the game says it does: \"" +
                          blurb + "\" against \"" + phrase + "\"");
                }
            }

            // ---- what residents claim about their own worlds ----
            {
                // Every world carries at least one off-element egg. That is deliberate - it is
                // why there is a reason to look at a world whose theme you already have - and it
                // means no resident can truthfully say their rock is all one thing. Hob did:
                // "Every egg on this rock is Molten", on a world where one spawn in five is a
                // Stone Cobblet, and his advice for the place is to bring a Molten counter.
                foreach (var w in PlanetDatabase.All)
                {
                    if (w.Spawns == null || w.Spawns.Length == 0) continue;
                    // Amaranth is exempt and should be: it is the boss world, it is the first
                    // egg, and everything on it being one thing is the point of the place.
                    if (w.IsBossWorld) continue;

                    bool anyOff = false;
                    foreach (var sp in w.Spawns)
                        if (SpeciesDatabase.Get(sp.SpeciesId).Type != w.Theme) anyOff = true;
                    check(anyOff, w.Name + " has something on it that is not " + TypeChart.Name(w.Theme));
                }

                // And no resident claims otherwise. Anything saying "every egg" alongside an
                // element name is checked against the world that resident lives on.
                foreach (var npc in StoryDatabase.Npcs)
                {
                    var script = StoryDatabase.GetDialogue(npc.Id, new StoryState(), new GameState());
                    if (script == null) continue;
                    var world = PlanetDatabase.Get(npc.PlanetId);
                    if (world == null || world.Spawns == null) continue;

                    foreach (var line in script.Lines)
                    {
                        // Only claims pinned to the speaker's own world. The first version
                        // caught Moth saying "every egg carries a knack from its element" -
                        // true, and about eggs everywhere rather than about Umbralux.
                        foreach (EggType t in System.Enum.GetValues(typeof(EggType)))
                        {
                            if (!ClaimsWholeWorld(line.Text, t)) continue;
                            bool allThat = true;
                            foreach (var sp in world.Spawns)
                                if (SpeciesDatabase.Get(sp.SpeciesId).Type != t) allThat = false;
                            // tripwire: nobody currently writes a line like this. The detector
                            // itself is tested below, so this will notice when somebody does.
                            check(allThat,
                                  npc.Name + " does not overclaim " + TypeChart.Name(t) +
                                  " on " + world.Name + ": \"" + line.Text + "\"");
                        }
                    }
                }

                // Nothing in the cast currently writes a line like this, so the assertion above
                // has never once run - which the coverage pass found and which is exactly the
                // shape of check that passes forever and proves nothing.
                //
                // The corpus cannot be made to exercise it without writing a bad line on
                // purpose, so the detector is what gets tested. If somebody later writes the
                // sentence this exists to catch, these say the filter will notice.
                check(ClaimsWholeWorld("Every egg on this rock is " + TypeChart.Name(EggType.Molten) + ", top to bottom.", EggType.Molten),
                      "an overclaim about a whole world is recognised");
                check(ClaimsWholeWorld("All the eggs round here are " + TypeChart.Name(EggType.Tidal) + ".", EggType.Tidal),
                      "and so is the other way of saying it");

                // And the false positive the filter was narrowed for. Moth's line is true and is
                // about eggs everywhere, not about Umbralux; the first version failed it.
                check(!ClaimsWholeWorld("Every egg carries a knack from its element.", EggType.Molten),
                      "a claim about eggs everywhere is not read as a claim about one world");
                check(!ClaimsWholeWorld("Every egg on this rock has a name.", EggType.Molten),
                      "a claim with no element in it is not read as an element claim");
            }

            // ---- cold stations ----
            {
                // The list and the dialogue have to agree. Ori names the dead ones in the
                // opening brief; if somebody adds a world to the list and not to his line, or
                // writes a line about a world that still glows, one of them is lying and there
                // is no way to tell which from inside the game.
                var oriBrief = StoryDatabase.GetDialogue("ori", new StoryState(), new GameState());
                var said = new System.Text.StringBuilder();
                foreach (var line in oriBrief.Lines) said.Append(line.Text).Append(' ');
                string brief = said.ToString();

                var home = PlanetDatabase.Home;
                foreach (var id in PlanetDatabase.ColdStations)
                {
                    var w = PlanetDatabase.Get(id);
                    check(w != null, "the cold-station list names a real world: " + id);
                    if (w == null) continue;

                    // Home is "and now ours" and Vesper says it with its own landmark.
                    if (id == home.Id || id == "vesper") continue;
                    check(brief.Contains(w.Name),
                          "Ori's brief names " + w.Name + " as cold, because the game draws it that way");
                }

                // Vesper's landmark is the two dark pads. If Vesper ever left the list, that
                // landmark would be describing something the player can see is not true.
                check(PlanetDatabase.StationCold("vesper"),
                      "Vesper's station is cold, as its own landmark says");
                var vesper = LandmarkDatabase.For("vesper");
                check(vesper != null && vesper.Lines[0].Contains("dark"),
                      "Vesper's landmark still describes dark pads");

                // And a warm world stays warm.
                check(!PlanetDatabase.StationCold("cinderoost"),
                      "Cinderoost's station is not cold - Hob never says it is");

                foreach (var line in UiCopy.RestCold)
                    check(lines(line, 1000f - 40f, 24) <= capacity(76f, 24),
                          "the cold rest line fits the toast: " + line);
            }

            // ---- the drift ----
            {
                // Motes spawn in a disc around Amaranth and fall toward it. If that disc does
                // not reach a world, a player standing at that world never sees any of this -
                // and it would be a silent hole, because nothing in the game ever mentions it.
                // The field is carried around the player, so it reaches everywhere by
                // construction - but it has to be wider than the camera, or a player would
                // watch motes appear out of nothing at the edge of the screen.
                var amaranth = PlanetDatabase.Get("amaranth");
                float farthest = 0f; string farthestName = "";
                foreach (var w in PlanetDatabase.All)
                {
                    float d = Vector2.Distance(w.SpacePosition, amaranth.SpacePosition);
                    if (d > farthest) { farthest = d; farthestName = w.Name; }
                }
                const float spaceViewHalfWidth = 15f * 16f / 9f;      // SpaceZoom, 16:9
                check(SpaceMode.DriftFieldRadius > spaceViewHalfWidth * 1.5f,
                      "the drift field is wider than the view (" + SpaceMode.DriftFieldRadius +
                      " against a half-view of " + spaceViewHalfWidth.ToString("0") + ")");

                // And it has to read as drift, not as debris going past the window. A player
                // crosses 33 units between neighbours in under two seconds; a mote takes minutes.
                float flightSpeed = 33f / TeoController.FlightSeconds(33f);
                check(SpaceMode.DriftSpeed < flightSpeed * 0.2f,
                      "the drift is slow next to flying (" + SpaceMode.DriftSpeed.ToString("0.00") +
                      " vs " + flightSpeed.ToString("0.0") + " units a second)");

                // It crosses the sector in minutes, not seconds - long enough that a player
                // notices the direction rather than the movement.
                float crossing = farthest / SpaceMode.DriftSpeed;
                check(crossing > 60f,
                      "a mote takes minutes to cross the sector - " + farthestName + " to Amaranth is " +
                      crossing.ToString("0") + "s");
            }

            // ---- screen chrome ----
            {
                // Footers are the longest fixed strings in the game and none had been measured.
                check(lines(UiCopy.ChartFooter, 1400f, 22) == 1, "the chart footer fits: " + UiCopy.ChartFooter);
                check(lines(UiCopy.BattleFooter, 1040f, 22) == 1, "the battle footer fits: " + UiCopy.BattleFooter);
                check(lines(UiCopy.ChartTitle, 900f, 34) == 1, "the chart title fits");
                check(lines(UiCopy.CollectionTitle, 1600f, 34) == 1, "the collection title fits");

                // The collection footer grows with what you own. Worst case: a nest, a full
                // party, everything switched on at once.
                var full = new GameState();
                while (full.Party.Count < GameState.PartySize)
                    full.Party.Add(EggInstance.Wild("sprouteg", 5));
                full.Nest.Add(EggInstance.Wild("sprouteg", 5));
                string hint = HudView.HintFor(full);
                check(lines(hint, 1500f, 20) == 1, "the collection footer fits at its longest: " + hint);

                // And shrinks when there is nothing to say. A player with one egg and no nest
                // is not told about columns or about leading with 1-6.
                string bare = HudView.HintFor(new GameState());
                check(!bare.Contains("column"), "no column hint before you have a nest: " + bare);
                check(!bare.Contains("leads"), "no lead hint with a single egg: " + bare);
            }

            // ---- battle plates ----
            {
                // The plate is 640px wide. The worst case is a long nickname, a status tag and
                // three stat stages at once - which is exactly what a few turns of a real fight
                // produces, and none of it had ever been measured.
                var st = new GameState();
                foreach (var sp0 in SpeciesDatabase.All) st.Caught.Add(sp0.Id);
                // No starter. With one, Sprouteg is already in the record on turn zero - Ori
                // hands you one - and the plate says so correctly. That is the second check
                // this session written as though a new save were empty; it is not, and both
                // times the game was right.
                var fresh = new GameState(false);

                foreach (var sp in SpeciesDatabase.All)
                {
                    var egg = EggInstance.WildElder(sp.Id, 30);
                    egg.Nickname = "Bartholomew";

                    check(lines(BattleMode.PlateName(egg), 640f, 30) == 1,
                          sp.Name + "'s plate name fits: " + BattleMode.PlateName(egg));
                    // With the turn-order tag on it, which only the player's plate carries and
                    // which is the widest the line ever gets.
                    var rival = EggInstance.WildElder(sp.Id, 30);
                    check(lines(BattleMode.PlateMeta(egg, rival), 640f, 20) == 1,
                          sp.Name + "'s plate meta fits with the turn-order tag: " +
                          BattleMode.PlateMeta(egg, rival));
                    check(lines(BattleMode.PlateMeta(egg), 640f, 20) == 1,
                          sp.Name + "'s plate meta fits without it: " + BattleMode.PlateMeta(egg));

                    check(BattleMode.PlateRecord(egg, fresh, false).Contains("New species"),
                          sp.Name + " is flagged as new before you have caught one");
                    check(BattleMode.PlateRecord(egg, st, false).Contains("Already"),
                          sp.Name + " is flagged as recorded once you have");
                    check(BattleMode.PlateRecord(egg, fresh, true) == "",
                          sp.Name + " carries no carton note when it belongs to a trainer");
                }
            }

            // ---- the drift, seen from the ground ----
            {
                // Space shows warmth crossing the sector toward Amaranth. The story is that it
                // is being pulled off every nest - which means off the world you are standing
                // on - and from the ground there was no sign of it.
                //
                // Rare on purpose: a player who never looks up never sees one. The numbers are
                // what make that true rather than annoying.
                check(SurfaceMode.OverheadEvery > 10f,
                      "a mote overhead stays rare (one every " + SurfaceMode.OverheadEvery + "s)");
                check(SurfaceMode.OverheadLife < SurfaceMode.OverheadEvery * 0.4f,
                      "and is gone long before the next (" + SurfaceMode.OverheadLife + "s of " +
                      SurfaceMode.OverheadEvery + ")");

                // Long enough to be noticed at all. Under a second and it is a flicker.
                check(SurfaceMode.OverheadLife > 2f,
                      "and lasts long enough to be seen (" + SurfaceMode.OverheadLife + "s)");

                // It is the same thing the sky shows, so it has to stop when that does.
                // Both are keyed on the same flag.
                var beaten = new StoryState();
                beaten.RestoreFrom(new[] { "beat_amy" }, StoryDatabase.Beats.Length - 1);
                check(beaten.HasFlag("beat_amy"),
                      "the flag that stops the drift is the one Amy's defeat sets");
            }

            // ---- a rematch does not replay the revelation ----
            {
                // Amy's first defeat is the scene the whole story walks toward: the first egg,
                // eleven years of holding it, the offer to share it. She replayed every word
                // of it on a rematch, while her greeting already knew the difference.
                var amy = StoryDatabase.GetTrainer("amy");
                check(amy != null, "Amy is a trainer the game can look up");

                var first = new StoryState();
                var again = new StoryState();
                again.RestoreFrom(new[] { "beat_amy" }, StoryDatabase.Beats.Length - 1);

                var firstScene = StoryDatabase.DefeatLinesFor(amy, first);
                var rematch = StoryDatabase.DefeatLinesFor(amy, again);

                check(firstScene.Length == amy.OnDefeat.Length,
                      "the first win still gets the whole scene (" + firstScene.Length + " lines)");
                check(rematch.Length < firstScene.Length,
                      "a rematch is shorter (" + rematch.Length + " against " + firstScene.Length + ")");

                // The revelation is in one and not the other.
                var firstText = new System.Text.StringBuilder();
                foreach (var l in firstScene) firstText.Append(l.Text).Append(' ');
                var againText = new System.Text.StringBuilder();
                foreach (var l in rematch) againText.Append(l.Text).Append(' ');

                check(firstText.ToString().Contains("An egg. The first one."),
                      "the first win explains what Amaranth is");
                check(!againText.ToString().Contains("The first one"),
                      "and the rematch does not explain it again");

                // Every line of both fits the dialogue box.
                foreach (var l in rematch)
                    check(lines(l.Text, 1380f, 28) <= capacity(180f, 28),
                          "Amy's rematch line fits: " + l.Text);

                // Other trainers are untouched - they only ever fight you once.
                var vess = StoryDatabase.GetTrainer("vess_1");
                check(StoryDatabase.DefeatLinesFor(vess, again).Length == vess.OnDefeat.Length,
                      "other trainers keep their one scene");
            }

            // ---- naming is not a 2.6 second window ----
            {
                // Naming used to be a prompt that ran for 2.6 seconds after a catch, and
                // nothing else. Miss it - reach for a drink, read the toast, look away - and
                // that egg was unnamed for the rest of the run, in a game that otherwise works
                // hard at making them yours.
                var st = new GameState(false);
                st.Party.Add(EggInstance.Wild("sprouteg", 10));
                st.Nest.Add(EggInstance.Wild("cobblet", 10));

                string hint = HudView.HintFor(st);
                check(hint.Contains("N names"), "the collection says naming is available: " + hint);
                check(lines(hint, 1500f, 20) == 1, "and the footer still fits: " + hint);

                // A player with nothing at all is not told about a key that would do nothing.
                check(!HudView.HintFor(new GameState(false)).Contains("N names"),
                      "an empty collection does not offer to name anything");

                // A name is capped where the entry screen caps it, and both the party strip
                // and the egg panel were measured against that cap - so renaming later cannot
                // produce a row the catch-time prompt could not have.
                check(NameEntryView.MaxLength > 0 && NameEntryView.MaxLength <= 16,
                      "a nickname stays inside what the rows were measured for (" +
                      NameEntryView.MaxLength + ")");
            }

            // ---- the line a returning player reads first ----
            {
                // It said "6 in nest" when it meant the party, on a run with fifty-nine
                // actually at the nest. It is the only place the game summarises a run back
                // to the player, and it is the first thing they read on coming back to it.
                var st = new GameState(false);
                for (int i = 0; i < GameState.PartySize; i++) st.Party.Add(EggInstance.Wild("sprouteg", 22));
                for (int i = 0; i < 59; i++) st.Nest.Add(EggInstance.Wild("cobblet", 30));

                var story = new StoryState();
                story.RestoreFrom(new string[0], StoryDatabase.Beats.Length - 1);
                string line = SaveSystem.Describe(SaveSystem.Capture(st, story, "glacierim", 2500f));

                check(line.Contains("6 in party"), "the summary counts the party as the party: " + line);
                check(line.Contains("59 at the nest"), "and the nest as the nest");

                // Top level looks at everything you own, not only what you are carrying - the
                // best egg of a long run is often sitting at home.
                check(line.Contains("top level 30"),
                      "top level counts eggs at the nest too: " + line);

                // 1300px at font 21, and it cannot wrap.
                check(lines(line, 1600f, 21) == 1, "the summary fits the title screen: " + line);

                // An empty-nest run says so rather than going blank.
                var thin = new GameState();
                string thinLine = SaveSystem.Describe(SaveSystem.Capture(thin, new StoryState(),
                                                                          PlanetDatabase.Home.Id, 30f));
                check(thinLine.Contains("0 at the nest"), "a fresh run reads properly: " + thinLine);
                check(lines(thinLine, 1600f, 21) == 1, "and a fresh run fits too: " + thinLine);
            }

            // ---- an egg's own record, in its panel ----
            {
                // The panel carried every number the game computes about an egg and not the
                // one thing that is only true of yours. It also moves the note about Ori's egg
                // out of the header, where it was competing with the trait blurb, and into the
                // section that is about what this particular egg has been through.
                var st = new GameState(false);
                foreach (var sp0 in SpeciesDatabase.All) { st.Seen.Add(sp0.Id); st.Caught.Add(sp0.Id); }

                var fresh = EggInstance.Wild("sprouteg", 10);
                check(!HudView.EggLoreText(fresh, st).Contains("RECORD"),
                      "an egg that has not fought yet has no record to show");

                var veteran = EggInstance.Wild("sprouteg", 30);
                veteran.RecordFight();
                check(HudView.EggLoreText(veteran, st).Contains("1 fight."),
                      "one fight reads as one: " + HudView.EggLoreText(veteran, st));
                veteran.RecordFight();
                check(HudView.EggLoreText(veteran, st).Contains("2 fights."),
                      "and two read as two");

                // Ori's egg says so here rather than in the header.
                var his = EggInstance.Wild("sprouteg", 30);
                his.MarkFromOri();
                his.RecordFight();
                check(HudView.EggLoreText(his, st).Contains("Ori's"),
                      "the egg Ori gave you is noted in its record");
                check(!HudView.EggDetailText(his).Contains("Ori"),
                      "and no longer crowds the header");

                // The panel still fits with the extra section on it, for every species at the
                // level where it has the most to say.
                foreach (var sp in SpeciesDatabase.All)
                {
                    var egg = EggInstance.WildElder(sp.Id, EggInstance.MaxLevel - 3);
                    egg.Nickname = new string('W', NameEntryView.MaxLength);
                    for (int k = 0; k < 999; k++) egg.RecordFight();
                    int used = 0;
                    foreach (var row in HudView.EggLoreText(egg, st).Split('\n'))
                        used += lines(row, 450f, 19);
                    check(used <= capacity(600f, 19),
                          sp.Name + "'s panel fits with a fight record on it (" + used +
                          " of " + capacity(600f, 19) + ")");
                }
            }

            // ---- the egg that carried the run ----
            {
                // Not a stat and it changes nothing. The game simply had no idea which of your
                // six had done the work, and by the end of a run that is the one thing about
                // your team a player actually knows.
                var st = new GameState(false);
                check(st.MostFought == null, "nothing has carried anything before a fight happens");

                var workhorse = EggInstance.Wild("sprouteg", 20);
                var passenger = EggInstance.Wild("cobblet", 20);
                st.Party.Add(workhorse); st.Party.Add(passenger);
                for (int i = 0; i < 40; i++) workhorse.RecordFight();
                for (int i = 0; i < 3; i++) passenger.RecordFight();

                check(ReferenceEquals(st.MostFought, workhorse),
                      "the one that fought most is the one named (" + st.MostFought.Fought + ")");

                // It counts eggs sitting at the nest too - a favourite you retired still did
                // the work.
                var retired = EggInstance.Wild("yolkano", 20);
                for (int i = 0; i < 99; i++) retired.RecordFight();
                st.Nest.Add(retired);
                check(ReferenceEquals(st.MostFought, retired),
                      "an egg retired to the nest still counts");

                // And the line it produces fits the card.
                string line = UiCopy.VictoryCarried("Bartholomew", 999);
                foreach (var row in line.Split('\n'))
                    check(lines(row, 1300f, 28) <= 1,
                          "the carried line fits the ending card: " + row);

                // The whole card, with every optional line on it at once.
                string full = UiCopy.VictoryBody + line + UiCopy.VictoryCoda +
                              UiCopy.VictoryTally(120, 24, 24, 8, LandmarkDatabase.Count,
                                                  LandmarkDatabase.Count,
                                                  PlanetDatabase.CacheWorlds.Count,
                                                  PlanetDatabase.CacheWorlds.Count);
                int used = 0;
                foreach (var row in full.Split('\n')) used += lines(row, 1300f, 28);
                check(used <= capacity(UiLayout.EndBodySize.y, 28),
                      "the ending card fits with every line it can carry (" + used + " of " +
                      capacity(UiLayout.EndBodySize.y, 28) + ")");
            }

            // ---- the swap menu says how the switch lands ----
            {
                // It listed name, level, element and health - everything except the one thing
                // the menu is opened to decide. Swapping means eating a hit on the way in, and
                // the game knows exactly how that hit lands against each egg on the list.
                //
                // The read is defensive on purpose: the move cards already say whether your
                // attacks land, and this is the half a player cannot work out from them.
                foreach (EggType foeType in System.Enum.GetValues(typeof(EggType)))
                {
                    var foe = EggInstance.Wild(FirstSpeciesOfType(foeType), 20);
                    if (foe == null) continue;

                    foreach (var sp in SpeciesDatabase.All)
                    {
                        var mine = EggInstance.Wild(sp.Id, 20);
                        string row = BattleMode.SwapRow(mine, foe, false);
                        float incoming = TypeChart.Multiplier(foe.Type, mine.Type);

                        if (incoming > 1.2f)
                            check(row.Contains("weak to"),
                                  sp.Name + " is told it is weak to " + TypeChart.Name(foe.Type));
                        else if (incoming < 0.8f)
                            check(row.Contains("resists"),
                                  sp.Name + " is told it resists " + TypeChart.Name(foe.Type));
                        else
                            check(!row.Contains("weak to") && !row.Contains("resists"),
                                  sp.Name + " is told nothing about an even matchup");

                        // 860px at font 22, inside a 900px menu.
                        check(lines(row, 860f, 22) == 1,
                              sp.Name + "'s swap row fits: " + row);
                    }
                }

                // A fainted egg, and the one already out, are told nothing - they are not
                // choices, and a matchup beside them would read as an option.
                var down = EggInstance.Wild("sprouteg", 20);
                down.CurrentHP = 0;
                var molten = EggInstance.Wild(FirstSpeciesOfType(EggType.Molten), 20);
                check(!BattleMode.SwapRow(down, molten, false).Contains("weak to"),
                      "a fainted egg is not offered a matchup");
                check(!BattleMode.SwapRow(EggInstance.Wild("sprouteg", 20), molten, true).Contains("weak to"),
                      "and neither is the one already out there");
            }

            // ---- who moves first ----
            {
                // Turn order is decided by speed and the game said nothing about it. A player
                // chose a move without knowing whether they would live to use it - hardest to
                // work out in exactly the moment it matters most, after a Chill has halved
                // their speed mid-fight.
                var quick = EggInstance.Wild("frizzlebolt", 20);   // fastest tier
                var slow = EggInstance.Wild("cobblet", 20);        // slowest tier
                check(quick.Spd > slow.Spd, "the two probe eggs really do differ in speed");

                check(BattleMode.PlateMeta(quick, slow).Contains("moves first"),
                      "a faster egg is told it moves first");
                check(BattleMode.PlateMeta(slow, quick).Contains("moves second"),
                      "a slower egg is told it moves second");

                var twin = EggInstance.Wild("cobblet", 20);
                check(BattleMode.PlateMeta(slow, twin).Contains("coin flip"),
                      "an exact tie is called what it is");

                // It agrees with the rule that actually orders the turn.
                check(BattleCalc.MoverGoesFirst(quick, slow) &&
                      BattleMode.PlateMeta(quick, slow).Contains("moves first"),
                      "the tag agrees with the turn the game takes");

                // Only your plate carries it - the foe's says nothing about order.
                check(!BattleMode.PlateMeta(quick).Contains("moves"),
                      "the foe's plate does not carry a turn-order tag");

                // And a chilled egg is compared on its chilled speed, which is the whole point.
                var chilled = EggInstance.Wild("frizzlebolt", 20);
                chilled.SpdStage = -2;
                check(chilled.Spd < quick.Spd,
                      "a chilled egg really is slower (" + chilled.Spd + " against " + quick.Spd + ")");
            }

            // ---- your own egg's panel ----
            {
                // The worst case for this panel is an egg that has everything to say at once:
                // a nickname beside a long species name, an elder tag, four moves, and an
                // evolution still ahead of it. It is 300x160 for the header and 450x600 for the
                // numbers, and nothing had ever measured the numbers.
                var st = new GameState();
                foreach (var sp0 in SpeciesDatabase.All) { st.Seen.Add(sp0.Id); st.Caught.Add(sp0.Id); }

                foreach (var sp in SpeciesDatabase.All)
                    foreach (int lv in new[] { 5, sp.EvolveLevel > 0 ? sp.EvolveLevel : 30, EggInstance.MaxLevel })
                    {
                        // An elder, because the ELDER tag is the widest the header ever gets.
                        var egg = EggInstance.WildElder(sp.Id, Mathf.Clamp(lv, 1, EggInstance.MaxLevel - 3));
                        egg.Nickname = "Bartholomew";        // the longest a player can enter

                        var rows = HudView.EggDetailText(egg).Split('\n');
                        int usedH = 0;
                        foreach (var row in rows) usedH += lines(row, 300f, 19);
                        check(usedH <= capacity(160f, 19),
                              "egg header fits for " + sp.Name + " at Lv " + egg.Level +
                              " (" + usedH + " of " + capacity(160f, 19) + " lines)");

                        int usedL = 0;
                        foreach (var row in HudView.EggLoreText(egg, st).Split('\n'))
                            usedL += lines(row, 450f, 19);
                        check(usedL <= capacity(600f, 19),
                              "egg numbers fit for " + sp.Name + " at Lv " + egg.Level +
                              " (" + usedL + " of " + capacity(600f, 19) + " lines)");
                    }

                // A fainted egg says so instead of showing an HP line, and a fully grown one
                // says so instead of an XP target it can never reach.
                var top = new EggInstance(SpeciesDatabase.All[0], EggInstance.MaxLevel);
                check(HudView.EggLoreText(top, st).Contains("fully grown"),
                      "a maxed egg is not asked for XP it cannot earn");

                var down = new EggInstance(SpeciesDatabase.All[0], 20);
                down.CurrentHP = 0;
                check(HudView.EggLoreText(down, st).Contains("Out cold"),
                      "a fainted egg says so where its HP would be");
            }

            // ---- the field record's lore column ----
            {
                // 450x600 at font 19 - 27 lines for matchups, stats, where it is found, the
                // evolution line and the notes. The FOUND ON line is the one that grows: a
                // species that spawns on six charted worlds names all six.
                var everywhere = new GameState();
                foreach (var w in PlanetDatabase.All) everywhere.Visited.Add(w.Id);
                foreach (var sp0 in SpeciesDatabase.All) { everywhere.Seen.Add(sp0.Id); everywhere.Caught.Add(sp0.Id); }

                var nowhere = new GameState();

                foreach (var sp in SpeciesDatabase.All)
                    foreach (var st in new[] { everywhere, nowhere })
                    {
                        bool caught = st.Caught.Contains(sp.Id);
                        string lore = HudView.DexLoreText(sp, st, caught);
                        int used = 0;
                        foreach (var row in lore.Split('\n')) used += lines(row, 450f, 19);
                        check(used <= capacity(600f, 19),
                              "field record lore fits for " + sp.Name + " (" + used + " of " +
                              capacity(600f, 19) + " lines)");
                    }

                // A species you can only get by evolving says so, rather than leaving the
                // section blank and looking like a bug.
                foreach (var sp in SpeciesDatabase.All)
                {
                    var worlds = PlanetDatabase.WorldsSpawning(sp.Id);
                    string lore = HudView.DexLoreText(sp, everywhere, true);
                    check(lore.Contains("FOUND ON"), sp.Name + "'s entry says where it is found");
                    if (worlds.Count == 0)
                        check(lore.Contains("It grows into this"),
                              sp.Name + " is only reachable by evolution, and says so");
                    else
                        check(lore.Contains(worlds[0].Name),
                              sp.Name + "'s entry names " + worlds[0].Name);
                }

                // And it names only worlds you have charted. Stated the other way round first -
                // "names no world at all on a fresh save" - and it failed, correctly: a new game
                // has already visited Yolkhaven, because that is where you wake up.
                foreach (var sp in SpeciesDatabase.All)
                {
                    string lore = HudView.DexLoreText(sp, nowhere, false);
                    foreach (var w in PlanetDatabase.All)
                    {
                        if (nowhere.Visited.Contains(w.Id)) continue;
                        check(!lore.Contains(w.Name),
                              sp.Name + "'s entry does not name uncharted " + w.Name);
                    }
                }

                // The home world is the one exception, and it is named because you have been.
                {
                    var home = PlanetDatabase.Home;
                    var homeSpawn = home.Spawns[0].SpeciesId;
                    check(HudView.DexLoreText(SpeciesDatabase.Get(homeSpawn), nowhere, false).Contains(home.Name),
                          "the field record names " + home.Name + " from the start, because you woke up there");
                }
            }

            // ---- move cards and the message under them ----
            {
                // 286px buttons, three lines at 20/17/17. Every move is measured against every
                // element it can face, because the effectiveness arrow and the word for it only
                // appear for some pairings - checking one matchup checks the easy case.
                foreach (var mv in MoveDatabase.All)
                {
                    var slot = new MoveSlot(mv);
                    foreach (EggType foe in System.Enum.GetValues(typeof(EggType)))
                    {
                        string card = BattleMode.MoveCardText(slot, foe);
                        var rows = card.Split('\n');
                        check(rows.Length <= 3,
                              mv.Name + "'s card is at most three lines vs " + TypeChart.Name(foe));
                        check(lines(rows[0], 286f, 20) == 1,
                              mv.Name + "'s name row fits its button vs " + TypeChart.Name(foe) + ": " + rows[0]);
                        for (int row = 1; row < rows.Length; row++)
                            check(lines(rows[row], 286f, 17) == 1,
                                  mv.Name + "'s row " + row + " fits its button: " + rows[row]);

                        string msg = BattleMode.MoveMessageText(slot, foe);
                        var mrows = msg.Split('\n');
                        check(lines(mrows[0], 1040f, 28) == 1,
                              mv.Name + "'s message headline fits vs " + TypeChart.Name(foe) + ": " + mrows[0]);
                        check(lines(mrows[1], 1040f, 24) <= 2,
                              mv.Name + "'s message body fits: " + mrows[1]);
                    }
                }

                // The arrow on the card and the word in the message must never disagree - they
                // are the same fact told twice, and the whole reason the card carries an arrow
                // is so four of them can be compared without reading four sentences.
                foreach (var mv in MoveDatabase.All)
                {
                    var slot = new MoveSlot(mv);
                    foreach (EggType foe in System.Enum.GetValues(typeof(EggType)))
                    {
                        string card = BattleMode.MoveCardText(slot, foe);
                        string msg = BattleMode.MoveMessageText(slot, foe);
                        check(card.Contains("\u25b2") == msg.Contains("strong against"),
                              mv.Name + "'s up arrow agrees with its message vs " + TypeChart.Name(foe));
                        check(card.Contains("\u25bc") == msg.Contains("weak against"),
                              mv.Name + "'s down arrow agrees with its message vs " + TypeChart.Name(foe));
                    }
                }
            }

            // ---- the chart's tally ----
            {
                var fresh = new GameState();
                check(!GalaxyMapView.Tally(fresh).Contains("inscription"),
                      "the chart says nothing about inscriptions before you have found one");

                var some = new GameState();
                some.Visited.Add("yolkhaven");
                some.Landmarks.Add("yolkhaven");
                check(GalaxyMapView.Tally(some).Contains("1 of " + LandmarkDatabase.Count),
                      "the chart counts inscriptions once you have read one");

                var done = new GameState();
                foreach (var w in PlanetDatabase.All) { done.Visited.Add(w.Id); done.Landmarks.Add(w.Id); }
                string full = GalaxyMapView.Tally(done);
                check(full.Contains(PlanetDatabase.All.Count + " of " + PlanetDatabase.All.Count),
                      "the chart can reach every world: " + full);

                // Right-aligned in a 900px box at font 22, sharing the header row with a title
                // that runs to 970 of the 1920.
                check(lines(full, 900f, 22) == 1, "the chart's tally fits its header box: " + full);
            }

            // ---- landmark asides ----
            {
                // Every resident lives on a world that has a landmark, so every resident owes it
                // a line. One missing is a world where you walk out, read a stone about somebody
                // by name, come back, and they have nothing to say about it.
                foreach (var npc in StoryDatabase.Npcs)
                {
                    var lm = LandmarkDatabase.For(npc.PlanetId);
                    if (lm == null) continue;

                    var withRead = new GameState();
                    withRead.Landmarks.Add(npc.PlanetId);
                    var virgin = new StoryState();

                    var plain = StoryDatabase.GetDialogue(npc.Id, virgin, new GameState());
                    var withAside = StoryDatabase.GetDialogue(npc.Id, virgin, withRead);
                    if (plain == null) continue;

                    check(withAside.Lines.Length > plain.Lines.Length,
                          npc.Name + " remarks on " + lm.Name + " once you have read it");

                    // It rides in front of the normal script - nothing the story needed is lost.
                    check(withAside.SetsFlag == plain.SetsFlag &&
                          withAside.StartsTrainer == plain.StartsTrainer &&
                          withAside.GivesSpeciesId == plain.GivesSpeciesId &&
                          withAside.HealsParty == plain.HealsParty &&
                          withAside.RestocksCartons == plain.RestocksCartons,
                          npc.Name + "'s aside does not swallow what the story needed him to do");

                    for (int i = 0; i < withAside.Lines.Length - plain.Lines.Length; i++)
                    {
                        check(withAside.Lines[i].Speaker == npc.Name,
                              npc.Name + "'s aside is spoken by " + npc.Name);
                        check(lines(withAside.Lines[i].Text, 1380f, 28) == 1,
                              npc.Name + "'s aside reads as one line: \"" + withAside.Lines[i].Text + "\"");
                    }

                    // Once, and then never again.
                    var again = StoryDatabase.GetDialogue(npc.Id, virgin, withRead);
                    check(again.Lines.Length == plain.Lines.Length,
                          npc.Name + " only says it once");
                }
            }

            // ---- floating world labels ----
            {
                // 420x90, and the hint under each title is drawn two to five points smaller
                // than the title is. Measuring the whole thing at one size is how the egg
                // panel's header came out 51px wrong, so each part is measured at its own.
                int box = UiCopy.LabelBox;

                void label(string title, int titleSize, string hint, string who)
                {
                    check(lines(title, box, titleSize) <= 1, who + "'s name fits its label: " + title);
                    check(lines(hint, box, UiCopy.LabelHintSize) <= 1, who + "'s hint fits its label: " + hint);
                    // Title at its size plus hint at its size, against the 90px box.
                    float used = titleSize * 1.16f + UiCopy.LabelHintSize * 1.16f;
                    check(used <= UiCopy.LabelHeight,
                          who + "'s label is not taller than its box (" + used.ToString("0") +
                          " of " + UiCopy.LabelHeight + "px)");
                }

                label(UiCopy.LabelNestStation, 22, UiCopy.LabelNestHint, "the Nest Station");
                label(UiCopy.LabelAmy, 26, UiCopy.LabelAmyHint, "Amy");

                foreach (var npc in StoryDatabase.Npcs)
                    label("<b>" + npc.Name + "</b>", 23, UiCopy.LabelTalkHint, npc.Name);

                foreach (var lm in LandmarkDatabase.All)
                    label("<b>" + lm.Name + "</b>", 22, UiCopy.LabelLandmarkHint, lm.Name);
            }

            // ---- landmarks ----
            {
                check(LandmarkDatabase.Count == PlanetDatabase.All.Count,
                      "every world has a landmark (" + LandmarkDatabase.Count + " of " +
                      PlanetDatabase.All.Count + ")");

                // A form nobody uses is art that never ships; a form used once is a shape the
                // player sees exactly once and never learns to recognise.
                var formUse = new Dictionary<LandmarkForm, int>();
                foreach (LandmarkForm f in System.Enum.GetValues(typeof(LandmarkForm))) formUse[f] = 0;
                foreach (var lm in LandmarkDatabase.All) formUse[lm.Form]++;
                foreach (var kv in formUse)
                    check(kv.Value >= 1, "the " + kv.Key + " form is used somewhere (" + kv.Value + " worlds)");

                // Exactly one coda. Two would be a pattern the player starts expecting; none
                // would mean seventeen inscriptions that never add up to anything.
                int codas = 0;
                foreach (var lm in LandmarkDatabase.All) if (lm.Coda != null) codas++;
                check(codas == 1, "exactly one inscription has a last word (" + codas + ")");

                // And it waits for the other sixteen.
                var readAll = new HashSet<string>();
                foreach (var w in PlanetDatabase.All) readAll.Add(w.Id);
                foreach (var lm in LandmarkDatabase.All)
                {
                    if (lm.Coda == null) continue;
                    check(!LandmarkDatabase.AllOthersRead(lm.PlanetId, new HashSet<string>()),
                          lm.Name + " keeps its last word until the others are read");
                    check(LandmarkDatabase.AllOthersRead(lm.PlanetId, readAll),
                          lm.Name + " gives up its last word once they are");

                    var oneShort = new HashSet<string>(readAll);
                    oneShort.Remove("yolkhaven");
                    check(!LandmarkDatabase.AllOthersRead(lm.PlanetId, oneShort),
                          lm.Name + " still waits when one inscription is missing");

                    // Reading it does not count as reading the others.
                    var itselfOnly = new HashSet<string> { lm.PlanetId };
                    check(!LandmarkDatabase.AllOthersRead(lm.PlanetId, itselfOnly),
                          lm.Name + " does not count itself");

                    foreach (var line in lm.Coda)
                        check(lines(line, 1380f, 28) == 1,
                              lm.Name + "'s last word reads as one line: \"" + line + "\"");
                }

                var seenNames = new HashSet<string>();
                foreach (var w in PlanetDatabase.All)
                {
                    var lm = LandmarkDatabase.For(w.Id);
                    check(lm != null, w.Name + " has something worth walking to");
                    if (lm == null) continue;

                    check(seenNames.Add(lm.Name), "landmark name \"" + lm.Name + "\" is not reused");
                    check(lm.Lines.Length >= 2 && lm.Lines.Length <= 4,
                          lm.Name + " says two to four lines (has " + lm.Lines.Length + ")");

                    // The dialogue box holds five lines, so "does it fit" is nearly impossible to
                    // fail and proves nothing. Every authored line in the game already draws as
                    // exactly one line, and that is the real rule: one line, one beat, one pause
                    // in the typewriter reveal. A landmark that wraps reads as a paragraph.
                    foreach (var line in lm.Lines)
                        check(lines(line, 1380f, 28) == 1,
                              lm.Name + " reads as one line: \"" + line + "\"");

                    // The cache sits at 0.68-0.90 of the radius and the landmark at 0.55-0.80,
                    // from independent streams - so on a cache world they can land on each
                    // other, and two "press E" prompts would fight over the same patch of
                    // ground. Reproduce both placements and measure.
                    if (PlanetDatabase.HasCache(w.Id))
                    {
                        // Against the prompt ranges, not against the clearance constant. The
                        // clearance is what LandmarkPosition enforces, so asserting the gap
                        // beats it was the code agreeing with itself - set it to zero and
                        // "gap > 0" passed. What a player would notice is two "press E" prompts
                        // offering themselves from the same patch of ground.
                        float reach = SurfaceLayout.CacheRange + SurfaceLayout.LandmarkRange;
                        float gap = Vector2.Distance(SurfaceLayout.CachePosition(w),
                                                     SurfaceLayout.LandmarkPosition(w));
                        check(gap > reach,
                              w.Name + "'s landmark and cache never offer themselves together (" +
                              gap.ToString("0.0") + "u apart, prompts reach " +
                              reach.ToString("0.0") + "u)");

                        // And the placement honours its own rule. Alone this is the code agreeing
                        // with itself - it was, for several revisions, and setting the clearance
                        // to zero made it pass. It is worth having only because the check above
                        // pins the constant against the prompts, and only because it catches what
                        // the prompt check cannot: deleting the push leaves Mosswell 5.0u apart,
                        // which clears the 4.2u the prompts reach and is nowhere near the margin
                        // the rule exists to give.
                        check(gap >= SurfaceLayout.LandmarkClearance,
                              w.Name + "'s placement honours the clearance rule (" +
                              gap.ToString("0.0") + " against " + SurfaceLayout.LandmarkClearance + ")");
                    }

                    // And the label that floats over it while you walk up.
                    check(lines("<b>" + lm.Name + "</b>", 420f, 22) == 1,
                          lm.Name + "'s marker label fits");
                }
            }

            // ---- evolution lines read both ways ----
            {
                int roots = 0, grown = 0;
                foreach (var sp in SpeciesDatabase.All)
                {
                    var from = SpeciesDatabase.EvolvesFrom(sp.Id);
                    if (from == null) { roots++; continue; }
                    grown++;

                    // The reverse lookup has to agree with the forward one, or the entry would
                    // show a line that does not exist.
                    check(from.EvolvesIntoId == sp.Id,
                          from.Name + " -> " + sp.Name + " agrees in both directions");
                    check(from.Id != sp.Id, sp.Name + " does not grow from itself");

                    // Exactly one thing may evolve into a given species; two would make the
                    // "grows from" line a lie for one of them.
                    int parents = 0;
                    foreach (var other in SpeciesDatabase.All)
                        if (other.CanEvolve && other.EvolvesIntoId == sp.Id) parents++;
                    check(parents == 1,
                          sp.Name + " has exactly one earlier form (found " + parents + ")");

                    check(lines("← grows from " + from.Name + " at level " + from.EvolveLevel, 450f, 19) == 1,
                          sp.Name + "'s grows-from line fits the entry");
                }
                check(roots > 0 && grown > 0,
                      "the roster has both starting forms and grown ones (" + roots + " and " + grown + ")");
            }

            // ---- a nickname at full length, everywhere a name is shown ----
            // The entry screen allows twelve characters. Nothing had ever put twelve of them
            // through the places a name is drawn - the checks used a nine-letter stand-in.
            {
                string fullName = new string('W', NameEntryView.MaxLength);

                check(lines(fullName + "  Lv 30", 440f, 30) == 1,
                      "a full-length nickname fits the battle card's name line");
                check(lines(HudView.Shorten(fullName, 14) + "  Lv 30  VOI  OUT", 300f, 18) == 1,
                      "a full-length nickname fits the party strip");
                // At the widest values the game can produce, not convenient ones: the highest
                // MaxHP any species reaches at level 30, and a nest count with no upper bound.
                int widestHp = 0; string fattest = "";
                foreach (var sp3 in SpeciesDatabase.All)
                {
                    var at30 = EggInstance.Wild(sp3.Id, EggInstance.MaxLevel);
                    if (at30.MaxHP > widestHp) { widestHp = at30.MaxHP; fattest = sp3.Name; }
                }
                string hp = widestHp + "/" + widestHp;
                check(lines(fullName + "  Lv " + EggInstance.MaxLevel + "  VERDANT   " + hp, 720f, 20) == 1,
                      "a full-length nickname fits a collection row at " + fattest + "'s " + hp + " HP");
                check(lines(fullName + "  Lv " + EggInstance.MaxLevel + "  " + hp, 500f, 30) == 1,
                      "the battle card holds a full nickname at " + hp + " HP");

                // TotalCollected has no ceiling, so the strip is measured at six figures rather
                // than at the couple of hundred a normal run reaches.
                check(lines("Nest 100000 · Types 8/8 · Record 24/24", 524f, 19) == 1,
                      "the collection strip survives a nest count of six figures");
                // Measured at the sizes it is actually drawn at: the nickname at 26, the species
                // name beside it at 17. Measuring the whole string at 26 overstates it by 50px,
                // which is the sort of wrong that reports a problem that is not there - though
                // in this case it happened to be pointing at one that was.
                string widestSpecies = "";
                foreach (var sp2 in SpeciesDatabase.All)
                    if (sp2.Name.Length > widestSpecies.Length) widestSpecies = sp2.Name;
                float headerPx = (NameEntryView.MaxLength + 2) * 26f * 0.52f
                               + widestSpecies.Length * 17f * 0.52f;
                check(headerPx <= 300f,
                      "a full-length nickname beside " + widestSpecies +
                      " fits the egg panel header (" + headerPx.ToString("0") + "px of 300)");

                // And in the sentences the game builds around a name.
                foreach (var line in new[]
                {
                    fullName + " joined your party.",
                    "An Elder! " + fullName + " is waiting at the nest.  New to the record.",
                    fullName + " grew to level 30!",
                    "Not a scratch on " + fullName + ".",
                    "That was close. " + fullName + " is still standing, just.",
                })
                    check(lines(line, 1000f - 40f, 24) <= capacity(76f, 24),
                          "a full-length nickname fits: \"" + line + "\"");

                check(lines("What will " + fullName + " do?", 1016f, 30) == 1,
                      "a full-length nickname fits the battle prompt");
            }

            // ---- data that is wrong without ever complaining ----
            {
                // A move nobody learns is data, not content - it costs authoring and appears in
                // no game. The same goes for a spawn nobody can roll.
                var taughtIds = new HashSet<string>();
                foreach (var sp in SpeciesDatabase.All)
                    foreach (var e in sp.Learnset) taughtIds.Add(e.MoveId);
                foreach (var mv in MoveDatabase.All)
                    check(taughtIds.Contains(mv.Id),
                          mv.Name + " is learned by at least one species (otherwise it is unreachable)");

                foreach (var w in PlanetDatabase.All)
                {
                    int total = 0;
                    foreach (var sp2 in w.Spawns)
                    {
                        check(sp2.Weight > 0,
                              w.Name + " has no zero-weight spawn (" + sp2.SpeciesId + " could never appear)");
                        total += sp2.Weight;
                        check(SpeciesDatabase.Exists(sp2.SpeciesId),
                              w.Name + " spawns a real species (" + sp2.SpeciesId + ")");
                    }
                    check(total > 0, w.Name + " has something to spawn");
                    check(w.MinLevel <= w.MaxLevel,
                          w.Name + "'s level range runs the right way (" + w.MinLevel + "-" + w.MaxLevel + ")");
                }

                // An evolution that points at itself, or loops, would spin TryEvolve.
                foreach (var sp in SpeciesDatabase.All)
                {
                    if (!sp.CanEvolve) continue;
                    check(sp.EvolvesIntoId != sp.Id, sp.Name + " does not evolve into itself");
                    var walk = sp; int steps = 0;
                    while (walk != null && walk.CanEvolve && steps++ < 10)
                        walk = SpeciesDatabase.Get(walk.EvolvesIntoId);
                    check(steps < 10, sp.Name + "'s evolution chain terminates");
                    check(sp.EvolveLevel > 1 && sp.EvolveLevel <= EggInstance.MaxLevel,
                          sp.Name + " evolves at a reachable level (" + sp.EvolveLevel + ")");
                }

                // Learnset entries are read in order; one out of sequence means an egg can be
                // handed a move before the level it was meant to arrive at.
                foreach (var sp in SpeciesDatabase.All)
                {
                    int last = 0;
                    foreach (var e in sp.Learnset)
                    {
                        check(e.Level >= last,
                              sp.Name + "'s learnset is in level order (" + e.Level + " after " + last + ")");
                        last = e.Level;
                    }
                }
            }

            // ---- learnsets ----
            // A learnset entry for a move the egg already knows is skipped, so the level-up it
            // sits on teaches nothing at all. Three of the four stage-three species had their
            // level-28 entry repeat a move they learned at 16 - a capstone that silently did
            // nothing, which no amount of playing would make obvious.
            foreach (var sp in SpeciesDatabase.All)
            {
                var seenMoves = new HashSet<string>();
                foreach (var e in sp.Learnset)
                    check(seenMoves.Add(e.MoveId),
                          sp.Name + " does not learn " + MoveDatabase.Get(e.MoveId).Name +
                          " twice (level " + e.Level + " teaches nothing)");
            }

            // Every element should have a finisher, or half the roster's late game is strictly
            // weaker than the other half's.
            foreach (EggType t in System.Enum.GetValues(typeof(EggType)))
            {
                if (t == EggType.Plain) continue;
                int best = 0;
                foreach (var mv in MoveDatabase.All) if (mv.Type == t && mv.Power > best) best = mv.Power;
                check(best >= 95, TypeChart.Name(t) + " has a high-power move (best is " + best + ")");
            }

            // ...and somebody has to actually learn it, or it is data nobody meets.
            foreach (var mv in MoveDatabase.All)
            {
                if (mv.Power < 95) continue;
                bool taught = false;
                int atLevel = 0;
                foreach (var sp in SpeciesDatabase.All)
                    foreach (var e in sp.Learnset)
                        if (e.MoveId == mv.Id) { taught = true; atLevel = e.Level; }
                check(taught, mv.Name + " is learned by somebody");
                if (taught)
                    check(atLevel <= EggInstance.MaxLevel,
                          mv.Name + " is learned at a level an egg can reach (" + atLevel +
                          " of " + EggInstance.MaxLevel + ")");
            }

            // ---- every background an egg is drawn on ----
            // Six contrast defects came from asking, one screen at a time, what a sprite is
            // actually drawn over. Rather than keep discovering the next one, this enumerates
            // every background the game composes an egg sprite onto. Adding a screen that draws
            // an egg means adding it here, and the list is short enough to be honest about.
            {
                var wheres = new[]
                {
                    new object[] { "the chart's planet panel", new Color32(0x0B, 0x0D, 0x1C, 0xFF) },
                    new object[] { "the collection panel",     new Color32(0x0B, 0x0D, 0x18, 0xFF) },
                    new object[] { "the dialogue box",         new Color32(0x12, 0x14, 0x22, 0xFF) },
                    new object[] { "the name entry box",       new Color32(0x12, 0x14, 0x22, 0xFF) },
                    new object[] { "the star map",             new Color32(0x05, 0x06, 0x0E, 0xFF) },
                };

                foreach (var where in wheres)
                {
                    string label = (string)where[0];
                    float bg = SurfaceMode.Luminance((Color)(Color32)where[1]);

                    foreach (var sp in SpeciesDatabase.All)
                    {
                        float body = SurfaceMode.Luminance(sp.Body);
                        float rim = SurfaceMode.Luminance(
                            SurfaceMode.Luminance(sp.Body) < 0.30f
                                ? Color.Lerp(sp.Accent, Color.white, 0.45f)
                                : sp.Accent * 0.55f);
                        check(Mathf.Max(Mathf.Abs(body - bg), Mathf.Abs(rim - bg)) >= 0.15f,
                              sp.Name + " reads on " + label);
                    }
                }
            }

            // ---- eggs against the battle backdrop ----
            // The battle draws each egg over a near-black backdrop with a soft glow behind it.
            // The glow is the catch: it lifts the local background toward the middle, which
            // helps a bright egg and hurts a dark one by moving the backdrop *towards* it.
            // Nothing had measured either, and the darkest species are Void.
            {
                var backdrop = new Color32(0x08, 0x0A, 0x14, 0xFF);
                var foeGlow = new Color(0.35f, 0.28f, 0.6f);      // behind the foe, 0.35 alpha
                var mineGlow = new Color(0.25f, 0.45f, 0.55f);    // behind yours,  0.30 alpha

                float behindFoe = SurfaceMode.Luminance(Color.Lerp(backdrop, foeGlow, 0.35f));
                float behindMine = SurfaceMode.Luminance(Color.Lerp(backdrop, mineGlow, 0.30f));

                foreach (var sp in SpeciesDatabase.All)
                {
                    float body = SurfaceMode.Luminance(sp.Body);
                    // Mirrors ProcArt: a dark egg is rimmed with light rather than shadow.
                    float rim = SurfaceMode.Luminance(
                        SurfaceMode.Luminance(sp.Body) < 0.30f
                            ? Color.Lerp(sp.Accent, Color.white, 0.45f)
                            : sp.Accent * 0.55f);

                    foreach (var pair in new[] { new[] { behindFoe, 0f }, new[] { behindMine, 1f } })
                    {
                        float bg = pair[0];
                        string side = pair[1] == 0f ? "the foe's side" : "your side";
                        float byBody = Mathf.Abs(body - bg);
                        float byRim = Mathf.Abs(rim - bg);
                        check(Mathf.Max(byBody, byRim) >= 0.15f,
                              sp.Name + " reads against the battle glow on " + side +
                              " (body " + byBody.ToString("0.00") + ", rim " + byRim.ToString("0.00") + ")");
                    }
                }
            }

            // ---- world labels ----
            // NEST STATION and every NPC name float on the planet with no panel behind them.
            // Their ink is near-white, so on a bright world they had nothing to read against -
            // 0.08 on Glacierim's ice. They carry a dark outline now, so either the ink or the
            // outline has to separate, exactly as with Teo.
            {
                var labelOutline = new Color32(0x0C, 0x0F, 0x18, 0xE6);
                foreach (var w in PlanetDatabase.All)
                {
                    float ground = SurfaceMode.Luminance(w.Land);
                    float byInk = Mathf.Abs(SurfaceMode.Luminance(UIKit.Ink) - ground);
                    float byLine = Mathf.Abs(SurfaceMode.Luminance(labelOutline) - ground);
                    check(Mathf.Max(byInk, byLine) >= 0.25f,
                          "world labels read on " + w.Name + " (ink " + byInk.ToString("0.00") +
                          ", outline " + byLine.ToString("0.00") + ")");

                    // And over a shell field, which is what they are usually near.
                    var field = SurfaceMode.AgainstGround(TypeChart.ColorOf(w.Theme), w.Land, 0.55f, 0.30f);
                    float fieldLum2 = SurfaceMode.Luminance(Color.Lerp(w.Land, field, 0.85f));
                    float inkOnField = Mathf.Abs(SurfaceMode.Luminance(UIKit.Ink) - fieldLum2);
                    float lineOnField = Mathf.Abs(SurfaceMode.Luminance(labelOutline) - fieldLum2);
                    check(Mathf.Max(inkOnField, lineOnField) >= 0.25f,
                          "world labels read on " + w.Name + "'s shell fields");
                }
            }

            // ---- and against the shell fields, not only the bare ground ----
            // Every visibility check so far measured against a world's ground colour. Eggs and
            // Teo spend much of their time standing on shell fields, which are a different
            // colour on top of it - the same mistake the audio checks made by testing each sound
            // alone when they are all heard over music.
            foreach (var w in PlanetDatabase.All)
            {
                var field = SurfaceMode.AgainstGround(TypeChart.ColorOf(w.Theme), w.Land, 0.55f, 0.30f);
                // Drawn at 0.85 alpha over the ground, so that is what the eye actually receives.
                var seen = Color.Lerp(w.Land, field, 0.85f);
                float fieldLum = SurfaceMode.Luminance(seen);

                float teoSuit = Mathf.Abs(SurfaceMode.Luminance(ProcArt.TeoSuit) - fieldLum);
                float teoLine = Mathf.Abs(SurfaceMode.Luminance(ProcArt.TeoOutline) - fieldLum);
                check(Mathf.Max(teoSuit, teoLine) >= 0.25f,
                      "Teo stands out on " + w.Name + "'s shell fields (suit " + teoSuit.ToString("0.00") +
                      ", outline " + teoLine.ToString("0.00") + ")");

                var halo = SurfaceMode.AgainstGround(w.Land, w.Land, 0f, 0.84f);
                float haloGap = Mathf.Abs(SurfaceMode.Luminance(halo) - fieldLum);
                for (int i = 0; i < w.Spawns.Length; i++)
                {
                    var sp = SpeciesDatabase.Get(w.Spawns[i].SpeciesId);
                    float body = Mathf.Abs(SurfaceMode.Luminance(sp.Body) - fieldLum);
                    check(Mathf.Max(body, haloGap) >= 0.20f,
                          sp.Name + " reads on " + w.Name + "'s shell fields (body " +
                          body.ToString("0.00") + ", halo " + haloGap.ToString("0.00") + ")");
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
            int ridersSeen = 0;
            foreach (var mv in MoveDatabase.All)
            {
                string acc = mv.Accuracy >= 100 ? "" : "  " + mv.Accuracy + "%";
                var rid = BattleCalc.RiderOf(mv.Effect);
                string rider = rid == EggStatus.Scorched ? "LEAVES SCORCHED"
                             : rid == EggStatus.Chilled ? "LEAVES CHILLED"
                             : rid == EggStatus.Dazed ? "LEAVES DAZED" : "";
                // The name line also carries an effectiveness arrow against the current foe.
                check(lines(mv.Name + acc + " \u25b2", 330f - 44f, 24) == 1,
                      "move button top line fits with its effectiveness arrow: \"" + mv.Name + acc + "\"");

                string sub = TypeChart.Name(mv.Type) + (mv.IsStatus ? " · STATUS" : " · PWR " + mv.Power) +
                             " · PP " + mv.MaxPP + "/" + mv.MaxPP;
                check(lines(sub, 330f - 44f, 17) == 1, "move button sub-line fits: \"" + sub + "\"");
                if (rider.Length > 0)
                {
                    ridersSeen++;
                    check(lines(rider, 330f - 44f, 17) == 1, "move button rider line fits: \"" + rider + "\"");
                }

                // Name at 24 plus one or two lines at 17, inside a 74px button.
                float used = 24f * 1.16f + 17f * 1.16f * (rider.Length > 0 ? 2 : 1);
                check(used <= 74f,
                      mv.Name + "'s button is tall enough (" + used.ToString("0") + " of 74px)");
            }

            check(ridersSeen > 0,
                  "some move does leave a condition behind, so the rider line was measured (" +
                  ridersSeen + " moves)");

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

                // Both endings, counted a row at a time - the card is many lines and measuring
                // the whole string as one ignores every newline in it.
                foreach (var coda in new[] { "", UiCopy.VictoryCoda })
                {
                    string ending = UiCopy.VictoryBody + coda + UiCopy.VictoryTally(120, 24, 24, 8, LandmarkDatabase.Count, LandmarkDatabase.Count,
                                        PlanetDatabase.CacheWorlds.Count, PlanetDatabase.CacheWorlds.Count);
                    int used = 0;
                    foreach (var row in ending.Split('\n')) used += lines(row, 1300f, 28);
                    check(used <= capacity(UiLayout.EndBodySize.y, 28),
                          "the ending card fits" + (coda.Length > 0 ? " with its last word" : "") +
                          " (" + used + " of " + capacity(460f, 28) + " lines)");
                }
                check(lines(UiCopy.VictoryHeading, 1400f, 82) == 1, "the ending heading fits");

                // Air, not merely absence of overlap. The title's 96pt wordmark cleared its
                // subtitle by 7px and the ending's 82pt heading cleared its body by 11px; both
                // passed the overlap check and both read as a small line resting on a large
                // one's descenders. Overlap is the wrong question for a heading.
                float titleGap = UiLayout.GapBetween(UiLayout.TitleHeadingAt, UiLayout.TitleHeadingSize,
                                                     UiLayout.TitleSubAt, UiLayout.TitleSubSize);
                check(titleGap >= UiLayout.MinHeadingGap,
                      "the wordmark has air under it (" + titleGap.ToString("0") + "px, needs " +
                      UiLayout.MinHeadingGap + ")");

                float endGap = UiLayout.GapBetween(UiLayout.EndHeadingAt, UiLayout.EndHeadingSize,
                                                   UiLayout.EndBodyAt, UiLayout.EndBodySize);
                check(endGap >= UiLayout.MinHeadingGap,
                      "the ending heading has air under it (" + endGap.ToString("0") + "px, needs " +
                      UiLayout.MinHeadingGap + ")");

                // The card must not contradict the scene it follows. Amy is relieved, not
                // beaten; she says so in her own last lines, and the card used to say otherwise.
                check(!UiCopy.VictoryHeading.Contains("BEATEN") && !UiCopy.VictoryBody.Contains("cracked it"),
                      "the ending card does not call Amy beaten");
            }

            // ---- every catchable egg must actually live somewhere ----
            // A species with a catchable rate and no spawn entry makes the record impossible to
            // finish, and nothing else in the game would ever say so.
            foreach (var sp in SpeciesDatabase.All)
            {
                var homes = PlanetDatabase.WorldsWith(sp.Id);
                if (sp.CatchRate >= SpeciesDatabase.CatchableThreshold)
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

                // Worst case: a full party, both headers, a blank, the whole nest window, and
                // both scroll markers at once - which happens whenever the cursor is somewhere
                // in the middle of a long nest. The window scrolls now, so both markers can be
                // on screen together and the old count of one was short by a line.
                // The column the game actually composes, at its worst: a full party, a nest
                // long enough that both scroll markers show, and the cursor in the middle of it.
                //
                // Measuring the row builders on their own could not see the real fault, which
                // was that the nest was being drawn with the two-line party row - six party
                // eggs and twenty nest eggs at two lines each is fifty-seven lines in a column
                // that holds thirty-three, and the old count of one line per egg passed.
                var worst = new GameState(false);
                for (int i = 0; i < GameState.PartySize; i++)
                    worst.Party.Add(EggInstance.Wild("sprouteg", 20));
                for (int i = 0; i < HudView.NestWindowSize * 3; i++)
                    worst.Nest.Add(EggInstance.Wild("cobblet", 20));

                int scroll = HudView.NestWindowSize;      // mid-nest: both markers on screen
                string column = HudView.CollectionBodyText(
                    worst, worst.Party.Count + HudView.NestWindowSize + 2, true, ref scroll);

                int nestLines = 0;
                foreach (var row in column.Split('\n')) nestLines += lines(row, 720f, 20);
                check(nestLines <= capacity(ColH, 20),
                      "collection: the party and nest column fits with both scroll markers (" +
                      nestLines + " of " + capacity(ColH, 20) + " lines)");

                check(column.Contains("more above") && column.Contains("more below"),
                      "and that worst case really does show both markers");



                // Measured, not counted. "Two plus the species count" assumes every row is one
                // drawn line, which is the assumption that let the party column ask for
                // fifty-seven lines of thirty-three. The record has no window at all, so it is
                // the column that breaks first as species are added.
                var recorded = new GameState(false);
                foreach (var sp in SpeciesDatabase.All) { recorded.Seen.Add(sp.Id); recorded.Caught.Add(sp.Id); }

                int dexLines = 0;
                foreach (var row in HudView.CollectionDexText(recorded, 0).Split('\n'))
                    dexLines += lines(row, 420f, 19);
                check(dexLines <= capacity(ColH, 19),
                      "collection: the field record lists every species without overrunning (" +
                      dexLines + " of " + capacity(ColH, 19) + " lines)");

                // And with nothing recorded, where every row is "? ? ?" and the element is
                // withheld - a shorter row, but the count is the same and worth pinning.
                var blank = new GameState(false);
                int blankLines = 0;
                foreach (var row in HudView.CollectionDexText(blank, 0).Split('\n'))
                    blankLines += lines(row, 420f, 19);
                check(blankLines <= capacity(ColH, 19),
                      "collection: and lists them all before you have met any (" + blankLines +
                      " of " + capacity(ColH, 19) + ")");

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
