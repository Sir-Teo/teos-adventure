using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Eggverse;

/// A consistency pass over every string the game can actually put on screen, taken from the
/// databases rather than scraped out of source. Grepping the .cs files matched across string
/// boundaries and reported code as prose; the data cannot lie about what it contains.
static class Prose
{
    static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);
    /// Strip markup, and treat an authored line break as a sentence boundary rather than
    /// gluing the last word of one line to the first of the next.
    static string Plain(string s) => Tags.Replace(s ?? "", "");
    static IEnumerable<string> Sentences(string s)
    {
        foreach (var part in Plain(s).Split('\n'))
            if (part.Length > 0) yield return part;
    }

    const char CurlyRight = '’';
    const char CurlyLeft = '‘';
    const char Ellipsis = '…';

    public static void Run(Action<bool, string> check)
    {
        // Whitespace rules apply to prose. They do not apply to copy that is laid out on
        // purpose: the controls list aligns its columns with runs of spaces, and the ending
        // body ends in a break because the run's tally is appended to it. Those are not typos,
        // and a checker that calls them typos trains you to ignore it.
        var lines = new List<KeyValuePair<string, string>>();
        var layout = new HashSet<string>();
        void Add(string where, string text) =>
            lines.Add(new KeyValuePair<string, string>(where, text));
        void AddLayout(string where, string text) { Add(where, text); layout.Add(where); }

        // ---- every conversation, in every phase of the story ----
        var st = new GameState();
        foreach (var id in new[] { "sprouteg", "yolkano", "tidepoach", "yolty", "chillet", "cobblet" })
        {
            var e = EggInstance.Wild(id, 25);
            if (st.Party.Count < GameState.PartySize) st.Party.Add(e); else st.Nest.Add(e);
            st.Seen.Add(id); st.Caught.Add(id);
        }

        // Walk the story one flag at a time. Jumping straight from "nothing set" to "everything
        // set" skips the branches that fire only in between - Ori's briefing, which is the one
        // place the Keepers are named, is reachable only with met_ori set and ori_briefed not.
        var order = new[] { "met_ori", "ori_briefed", "keeper_marn", "keeper_sable",
                            "learned_truth", "beat_vess_1", "beat_vess_2", "beat_amy" };
        var flagSets = new List<string[]>();
        var acc = new List<string>();
        flagSets.Add(new string[0]);
        foreach (var f in order) { acc.Add(f); flagSets.Add(acc.ToArray()); }

        foreach (var flags in flagSets)
            for (int beat = 0; beat < StoryDatabase.Beats.Length; beat++)
            {
                var probe = new StoryState();
                probe.RestoreFrom(flags, beat);
                foreach (var npc in StoryDatabase.Npcs)
                {
                    var script = StoryDatabase.GetDialogue(npc.Id, probe, st);
                    if (script == null) continue;
                    foreach (var l in script.Lines) Add(npc.Name, l.Text);
                }
                var amy = StoryDatabase.AmyIntro(probe);
                if (amy != null) foreach (var l in amy.Lines) Add("Amy", l.Text);
            }

        // ---- trainers' parting words ----
        foreach (var tid in new[] { "vess_1", "vess_2", "amy" })
        {
            var t = StoryDatabase.GetTrainer(tid);
            if (t == null || t.OnDefeat == null) continue;
            foreach (var l in t.OnDefeat) Add(t.Name + " (defeat)", l.Text);
        }

        // ---- authored copy outside dialogue ----
        foreach (var b in StoryDatabase.Beats) { Add("beat " + b.Id, b.Objective); Add("chapter", b.Chapter); }
        foreach (var p in PlanetDatabase.All) { Add(p.Name + " tagline", p.Tagline); Add("planet name", p.Name); }
        foreach (var s in SpeciesDatabase.All) { Add(s.Name + " blurb", s.Blurb); Add("species name", s.Name); }

        // The words the game uses for its own machinery. These are on every battle plate and
        // every egg panel in the game and had never been through the pass - the trait a player
        // reads a hundred times a run was less checked than a landmark they may never find.
        foreach (EggTrait t in Enum.GetValues(typeof(EggTrait)))
        {
            if (t == EggTrait.None) continue;
            Add("trait name", TypeChart.TraitName(t));
            Add("trait blurb", TypeChart.TraitBlurb(t));
        }
        foreach (EggType t in Enum.GetValues(typeof(EggType)))
        {
            Add("element name", TypeChart.Name(t));
            Add("element tag", TypeChart.Abbrev(t));
        }
        foreach (Sector sec in Enum.GetValues(typeof(Sector)))
            Add("sector name", PlanetDatabase.SectorName(sec));
        for (int i = 0; i < UiCopy.PauseControls.Length; i++)
            AddLayout("pause control " + i, UiCopy.PauseControls[i]);

        // ---- screen copy that is not dialogue ----
        AddLayout("title body", UiCopy.TitleBody);
        Add("title", UiCopy.Title);
        Add("subtitle", UiCopy.Subtitle);
        Add("begin hint", UiCopy.BeginHint);
        Add("ending heading", UiCopy.VictoryHeading);
        AddLayout("ending body", UiCopy.VictoryBody);
        AddLayout("ending tally", UiCopy.VictoryTally(120, 24, 24, 8, LandmarkDatabase.Count, LandmarkDatabase.Count,
                                           PlanetDatabase.CacheWorlds.Count, PlanetDatabase.CacheWorlds.Count));
        AddLayout("ending coda", UiCopy.VictoryCoda);
        for (int i = 0; i < UiCopy.RestIdle.Length; i++) Add("rest line " + i, UiCopy.RestIdle[i]);
        for (int i = 0; i < UiCopy.RestCold.Length; i++) Add("cold rest line " + i, UiCopy.RestCold[i]);
        // Ori's line about the egg he handed over, which only appears once it has grown.
        {
            var briefed = new StoryState();
            briefed.RestoreFrom(new[] { "met_ori", "ori_briefed" }, 2);
            var grown = new GameState(false);
            var raised = EggInstance.Wild("sprouteg", StoryDatabase.OriNoticesLevel);
            raised.MarkFromOri();
            grown.Party.Add(raised);
            var script = StoryDatabase.GetDialogue("ori", briefed, grown);
            foreach (var line in script.Lines) Add("Ori on his egg", line.Text);
        }

        // Amy's rematch scene, which only exists once she has been beaten.
        {
            var after = new StoryState();
            after.RestoreFrom(new[] { "beat_amy" }, StoryDatabase.Beats.Length - 1);
            foreach (var l in StoryDatabase.DefeatLinesFor(StoryDatabase.GetTrainer("amy"), after))
                Add("Amy rematch", l.Text);
        }

        // Move names and the line each one shows in the battle message box. These were never
        // in the pass at all - thirty-nine names and thirty-nine descriptions that had skipped
        // the doubled-word, spacing and apostrophe checks every other string goes through.
        foreach (var mv in MoveDatabase.All)
        {
            Add("move " + mv.Name, mv.Name);
            Add("move " + mv.Name, mv.Describe());
        }

        for (int i = 0; i < BattleMode.ActionHelp.Length; i++)
            Add("action help " + i, BattleMode.ActionHelp[i]);

        for (int i = 0; i < UiCopy.BattleLines.Length; i++)
            Add("battle line " + i, UiCopy.BattleLines[i]);

        Add("woke warm", UiCopy.WokeWarm);
        Add("woke cold", UiCopy.WokeCold);
        for (int i = 0; i < UiCopy.WokeAfterAmy.Length; i++)
            Add("woke after Amy " + i, UiCopy.WokeAfterAmy[i]);
        Add("woke after a trainer", UiCopy.WokeAfter("Marn", false));
        Add("out of cartons", UiCopy.OutOf(true, false));
        Add("out of salves", UiCopy.OutOf(false, true));
        Add("out of both", UiCopy.OutOf(true, true));

        // And what each resident says about their own world's landmark.
        foreach (var npc in StoryDatabase.Npcs)
        {
            var lit = new GameState();
            lit.Landmarks.Add(npc.PlanetId);
            var plain = StoryDatabase.GetDialogue(npc.Id, new StoryState(), new GameState());
            var withAside = StoryDatabase.GetDialogue(npc.Id, new StoryState(), lit);
            if (plain == null || withAside == null) continue;
            for (int i = 0; i < withAside.Lines.Length - plain.Lines.Length; i++)
                Add(npc.Name + " aside", withAside.Lines[i].Text);
        }

        // Landmarks are authored prose too - seventeen of them, read in the dialogue box.
        foreach (var lm in LandmarkDatabase.All)
        {
            Add(lm.Name, lm.Name);
            foreach (var line in lm.Lines) Add(lm.Name, line);
            if (lm.Coda != null) foreach (var line in lm.Coda) Add(lm.Name + " coda", line);
        }

        // the same conversation is reachable from many beats
        var seen = new HashSet<string>();
        var unique = new List<KeyValuePair<string, string>>();
        foreach (var l in lines)
            if (!string.IsNullOrEmpty(l.Value) && seen.Add(l.Key + "" + l.Value)) unique.Add(l);

        // How long the sector has been cold is stated sixteen times across four files and is
        // never once a constant. Making it one would mean interpolating a number into sixteen
        // authored lines and reading them all worse, so instead: no new duration may appear.
        //
        // Three are legitimate and each is somebody's own span - eleven years is the cold,
        // forty is how long Ori has been doing this, two is how long Sable has been keeping her
        // notebook. A fourth turning up means either a typo or a fact somebody changed in one
        // place, and both are things a player can catch by talking to two characters.
        {
            var allowed = new HashSet<string> { "eleven", "forty", "two" };
            var found = new SortedSet<string>();
            var rx = new System.Text.RegularExpressions.Regex(
                @"\b(one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|thirteen|twenty|thirty|forty|fifty)\s+years?\b",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            foreach (var entry in unique)
                foreach (System.Text.RegularExpressions.Match m in rx.Matches(Plain(entry.Value)))
                    found.Add(m.Groups[1].Value.ToLowerInvariant());

            foreach (var d in found)
                check(allowed.Contains(d),
                      "no unexplained span of years in the writing: \"" + d + " years\"");

            check(found.Contains("eleven"),
                  "the sector is still eleven years cold somewhere in the writing");
        }

        int curly = 0, straight = 0, uniEllipsis = 0, asciiEllipsis = 0;
        foreach (var entry in unique)
        {
            string where = entry.Key, t = Plain(entry.Value);

            if (!layout.Contains(where))
            {
                check(t == t.Trim(), "[" + where + "] no leading or trailing space: \"" + Clip(t) + "\"");
                check(!t.Contains("  "), "[" + where + "] no double space: \"" + Clip(t) + "\"");
                check(!t.Contains(" ,") && !t.Contains(" ."),
                      "[" + where + "] no space before punctuation: \"" + Clip(t) + "\"");
            }
            // A doubled word is a mistake in any kind of string, laid out or not.
            check(!Regex.IsMatch(t, @"\b(\w+) \1\b", RegexOptions.IgnoreCase),
                  "[" + where + "] no doubled word: \"" + Clip(t) + "\"");

            if (t.IndexOf(CurlyRight) >= 0 || t.IndexOf(CurlyLeft) >= 0) curly++;
            if (t.IndexOf('\'') >= 0) straight++;
            if (t.IndexOf(Ellipsis) >= 0) uniEllipsis++;
            if (t.Contains("...")) asciiEllipsis++;
        }

        // ---- terminology ----
        // The script invents its own nouns. Whichever way each is written, it should be written
        // that way everywhere; a Nest Station on one world and a nest station on the next reads
        // like two different things.
        string[] terms = { "Nest Station", "Prime Egg", "Shell Field", "Elder", "Hatcher",
                           "Keeper", "Long Drift", "Shattered Belt", "Hatchery Reach" };
        foreach (var term in terms)
        {
            int upper = 0, lower = 0;
            foreach (var entry in unique)
            {
                string t = Plain(entry.Value);
                // Whole words only. A bare substring match reads "Hatcher" inside "The
                // Hatchery Reach" and reports the sector name as an inconsistent capitalisation
                // of a job title.
                foreach (Match m in Regex.Matches(t, @"\b" + Regex.Escape(term) + @"s?\b",
                                                  RegexOptions.IgnoreCase))
                {
                    // Sentence-initial words are capitalised for grammar, not as proper nouns.
                    // Look back past the whitespace: "...all year round. Hatchers tuck one in"
                    // is a new sentence, and reading only the single preceding character sees a
                    // space and calls it a proper noun.
                    if (m.Index == 0) continue;
                    int k = m.Index - 1;
                    while (k >= 0 && (t[k] == ' ' || t[k] == '\n')) k--;
                    if (k < 0) continue;
                    char before = t[k];
                    if (before == '.' || before == '!' || before == '?' || before == '"' ||
                        before == '*' || before == ':') continue;
                    // Compare only the term's own length: "Hatchers" is the plural of the
                    // same word and capitalises the same way.
                    string head = m.Value.Substring(0, Math.Min(term.Length, m.Value.Length));
                    if (head == term) upper++; else lower++;
                }
            }
            if (upper + lower == 0) continue;
            check(upper == 0 || lower == 0,
                  "\"" + term + "\" is capitalised consistently (" + upper + " as written, " +
                  lower + " otherwise)");
            Console.WriteLine("  " + term.PadRight(16) + upper + " as \"" + term + "\", " + lower + " otherwise");
        }

        // Mixed conventions are the thing worth catching. Either is fine; both is sloppy.
        check(curly == 0 || straight == 0,
              "apostrophes use one convention (" + straight + " straight, " + curly + " curly)");
        check(uniEllipsis == 0 || asciiEllipsis == 0,
              "ellipses use one convention (" + asciiEllipsis + " ascii, " + uniEllipsis + " unicode)");

        Console.WriteLine("  " + unique.Count + " distinct authored strings checked");
        Console.WriteLine("  apostrophes: " + straight + " straight, " + curly + " curly"
                          + "  |  ellipses: " + asciiEllipsis + " ascii, " + uniEllipsis + " unicode");
    }

    static string Clip(string s) => s.Length <= 56 ? s : s.Substring(0, 53) + "...";
}
