using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Eggverse;

/// Panel geometry being right does not mean the words fit inside it. Estimate how many
/// lines each authored string wraps to at its display size, and compare against the box.
/// Deliberately pessimistic: real Arial is narrower than 0.52em on average.
static class TextFit
{
    const float CharWidthRatio = 0.52f;   // average glyph width as a fraction of font size
    const float LineHeightRatio = 1.16f;

    static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);

    /// Rich-text tags occupy no pixels, so strip them before measuring.
    public static string Strip(string s) => Tags.Replace(s ?? "", "");

    public static int LinesNeeded(string text, float boxWidth, int fontSize)
    {
        string plain = Strip(text);
        float perLine = Math.Max(1f, boxWidth / (fontSize * CharWidthRatio));
        int lines = 0;
        foreach (var paragraph in plain.Split('\n'))
            lines += Math.Max(1, (int)Math.Ceiling(paragraph.Length / perLine));
        return lines;
    }

    public static int LinesAvailable(float boxHeight, int fontSize) =>
        (int)Math.Floor(boxHeight / (fontSize * LineHeightRatio));

    static void Fits(Action<bool,string> check, string what, string text,
                     float w, float h, int font)
    {
        int need = LinesNeeded(text, w, font);
        int have = LinesAvailable(h, font);
        check(need <= have,
              $"{what} fits its panel ({need} lines needed, {have} available) — \"{Trim(Strip(text))}\"");
    }

    static string Trim(string s) => s.Length <= 58 ? s : s.Substring(0, 55) + "...";

    public static void Run(Action<bool,string> check)
    {
        // Toasts are a single centred line in a 1000x56 bar; the longest authored one decides
        // whether that bar can stay its current width.
        foreach (var toast in new[]
        {
            "You woke up at the Nest Station. Everything is patched up.",
            "Your eggs are warm and whole again. Cartons restocked.",
            "That save could not be read. Starting a new run.",
        })
            Fits(check, "toast", toast, 1000f - 40f, 56f, 24);

        // --- dialogue box: body area 1380 x 190 at font 28 ---
        int longest = 0; string longestLine = "";
        var story = new StoryState();
        var state = new GameState();
        int lineCount = 0;

        foreach (var npc in StoryDatabase.Npcs)
            for (int beat = 0; beat < StoryDatabase.Beats.Length; beat++)
            {
                var probe = new StoryState();
                probe.RestoreFrom(new string[0], beat);
                var script = StoryDatabase.GetDialogue(npc.Id, probe, state);
                if (script == null) continue;
                foreach (var line in script.Lines)
                {
                    Fits(check, $"dialogue ({line.Speaker})", line.Text, 1380f, 190f, 28);
                    int len = Strip(line.Text).Length;
                    if (len > longest) { longest = len; longestLine = line.Text; }
                    lineCount++;
                }
            }

        foreach (var id in new[] { "vess_1", "vess_2", "amy" })
        {
            var t = StoryDatabase.GetTrainer(id);
            foreach (var line in t.OnDefeat)
            {
                Fits(check, $"defeat line ({line.Speaker})", line.Text, 1380f, 190f, 28);
                int len = Strip(line.Text).Length;
                if (len > longest) { longest = len; longestLine = line.Text; }
            }
        }
        foreach (var line in StoryDatabase.AmyIntro(story).Lines)
            Fits(check, "Amy intro", line.Text, 1380f, 190f, 28);

        Console.WriteLine($"  dialogue: {lineCount} line-instances, longest {longest} chars " +
                          $"({LinesNeeded(longestLine, 1380f, 28)} of {LinesAvailable(190f, 28)} lines)");

        // --- field-record detail column: the whole composed panel, 450 x 600 at font 19 ---
        foreach (var sp in SpeciesDatabase.All)
        {
            // Reconstruct the worst case the panel can hold: matchups, stats, evolution, note.
            var panel = new System.Text.StringBuilder();
            panel.Append("MATCHUPS\n");
            panel.Append("hits hard  ").Append(TypeChart.Join(TypeChart.StrongAgainst(sp.Type))).Append('\n');
            panel.Append("weak to    ").Append(TypeChart.Join(TypeChart.VulnerableTo(sp.Type))).Append('\n');
            panel.Append("shrugs off ").Append(TypeChart.Join(TypeChart.Resists(sp.Type))).Append("\n\n");
            panel.Append("BASE STATS\n");
            for (int i = 0; i < 4; i++) panel.Append("HP  100  ============\n");
            panel.Append("total 999\n\n");
            panel.Append(sp.CanEvolve
                ? "-> becomes " + SpeciesDatabase.Get(sp.EvolvesIntoId).Name + " at level " + sp.EvolveLevel + "\n\n"
                : "Final form.\n\n");
            panel.Append(sp.Blurb);

            Fits(check, $"{sp.Name} detail panel", panel.ToString(), 450f, 600f, 19);
        }

        // --- trait blurbs in the detail column: 280 wide, 160 tall at font 19 ---
        foreach (EggType t in Enum.GetValues(typeof(EggType)))
        {
            if (t == EggType.Plain) continue;
            Fits(check, $"{t} trait blurb", TypeChart.TraitBlurb(TypeChart.TraitOf(t)), 280f, 90f, 19);
        }

        // --- HUD objective: 524 x 150 at font 20, plus a chapter line and a blocker line ---
        foreach (var beat in StoryDatabase.Beats)
        {
            string worst = beat.Chapter + "\n" + beat.Objective +
                           "\nStill needed: 6 more eggs, 4 more types, an egg at level 22";
            Fits(check, $"objective '{beat.Id}'", worst, 524f, 150f, 20);
        }

        // --- map detail: planet taglines in a 508-wide column at font 22 ---
        foreach (var planet in PlanetDatabase.All)
            Fits(check, $"{planet.Name} tagline", planet.Tagline, 508f, 120f, 22);

        // --- battle messages: the longest lines the battle can construct ---
        var longEgg = EggInstance.WildElder("frizzlebolt", 27);
        longEgg.Nickname = "Bartholomew";
        string[] battleMessages =
        {
            "An <b>Elder " + longEgg.Species.Name + "</b> heaves into view.  (Lv " + longEgg.Level + ")",
            "It is far older than the others, and it will not go quietly into a carton.",
            longEgg.Name + " is cracking... " + longEgg.Name + " became " + longEgg.Nickname + " the Sparkshell!",
            longEgg.Name + " forgot Drizzle Drain and learned Event Horizon!",
            "You lobbed an egg carton!  (12 left)",
            longEgg.Name + " was sent to the nest back home.",
            longEgg.Name + " is too hardheaded to slow down.",
            "Press <b>N</b> to name it, or Space to carry on.",
        };
        foreach (var m in battleMessages)
            Fits(check, "battle message", m, 1016f, 140f, 30);

        // --- HUD party strip: a fixed 300x24 slot that cannot wrap ---
        foreach (var sp in SpeciesDatabase.All)
        {
            string worst = HudView.Shorten("Elder " + sp.Name, 14) + "  Lv 30  " +
                           TypeChart.Abbrev(sp.Type) + "  OUT";
            check(LinesNeeded(worst, 300f, 18) == 1,
                  $"party slot for {sp.Name} stays on one line (\"{worst}\")");
        }

        // --- HUD objective footer: one line at 524 wide, font 19 ---
        {
            string footer = "Cartons 12/12 · Nest 24 · Types 8/8 · Record 24/24";
            check(LinesNeeded(footer, 524f, 19) == 1, $"the HUD counters line fits (\"{footer}\")");
        }

        // --- battle cards: name line 440 wide at 30, meta line 612 wide at 18, one line each ---
        foreach (var sp in SpeciesDatabase.All)
        {
            // Worst case: an elder with the longest trait name and every stat stage showing.
            string nameLine = "Elder " + sp.Name + "  Lv 30";
            check(LinesNeeded(nameLine, 440f, 30) == 1,
                  $"{sp.Name}'s card name line fits on one line (\"{nameLine}\")");

            string metaLine = TypeChart.TraitName(TypeChart.TraitOf(sp.Type)) + "  ATK000 DEF000 SPD000";
            check(LinesNeeded(metaLine, 612f, 18) == 1,
                  $"{sp.Name}'s card meta line fits on one line (\"{metaLine}\")");
        }

        // --- move buttons: 330 wide, two lines at font 24/17 ---
        foreach (var sp in SpeciesDatabase.All)
        {
            var capped = new EggInstance(sp, EggInstance.MaxLevel);
            foreach (var slot in capped.Moves)
            {
                string top = slot.Move.Name + (slot.Move.Accuracy >= 100 ? "" : "  " + slot.Move.Accuracy + "%");
                check(LinesNeeded(top, 310f, 24) == 1, $"move button '{top}' fits its width");
                string bottom = TypeChart.Name(slot.Move.Type) +
                                (slot.Move.IsStatus ? " · STATUS" : " · PWR " + slot.Move.Power) +
                                " · PP " + slot.PP + "/" + slot.Move.MaxPP;
                check(LinesNeeded(bottom, 310f, 17) == 1, $"move button detail '{bottom}' fits its width");
            }
        }

        // --- party swap rows: 820-wide button, name at 24 then details at 19 ---
        foreach (var sp in SpeciesDatabase.All)
        {
            var egg = EggInstance.WildElder(sp.Id, EggInstance.MaxLevel - 3);
            string row = "  *  " + egg.Name + "   Lv " + egg.Level + " · " +
                         TypeChart.Name(egg.Type) + " · " + egg.MaxHP + "/" + egg.MaxHP + " HP";
            check(LinesNeeded(row, 800f, 24) == 1, $"party swap row for {sp.Name} fits (\"{row}\")");
        }

        // --- star-map planet labels: 420 wide, name at 26 over a subtitle at 20 ---
        foreach (var planet in PlanetDatabase.All)
        {
            check(LinesNeeded(planet.Name, 420f, 26) == 1, $"{planet.Name}'s map label fits");
            string sub = "UNCHARTED · Lv " + planet.MinLevel + "-" + planet.MaxLevel;
            check(LinesNeeded(sub, 420f, 20) == 1, $"{planet.Name}'s map subtitle fits");
            string sub2 = "Lv " + planet.MinLevel + "-" + planet.MaxLevel + " · " + TypeChart.Name(planet.Theme);
            check(LinesNeeded(sub2, 420f, 20) == 1, $"{planet.Name}'s charted subtitle fits");
        }

        // --- surface world labels: NPC names over a prompt, 420 wide ---
        foreach (var npc in StoryDatabase.Npcs)
            check(LinesNeeded(npc.Name, 420f, 23) == 1, $"{npc.Name}'s world label fits");

        // --- pause menu rows: 700 wide, single line at font 28 ---
        foreach (var row in new[] { "Resume", "Sound  off", "Music  ||||||||||",
                                    "Quit to title — press Enter again" })
            Fits(check, "pause row", row, 700f, 52f, 28);

        // Positive controls: a checker that can never fail is not checking anything.
        check(LinesNeeded(new string('x', 4000), 1380f, 28) > LinesAvailable(190f, 28),
              "control: the fit checker rejects text that plainly overflows");
        check(LinesNeeded("<b><color=#FFC24D>hi</color></b>", 400f, 20) == 1,
              "control: markup does not count toward text length");
        check(LinesNeeded("a\nb\nc", 4000f, 20) == 3,
              "control: explicit line breaks are counted");
        check(LinesAvailable(190f, 28) < LinesAvailable(190f, 14),
              "control: a smaller font fits more lines");

        Console.WriteLine("  every authored string checked against its panel");
    }
}
