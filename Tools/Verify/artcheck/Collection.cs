using System;
using Eggverse;

/// The collection screen, driven by the real SpeciesDatabase. Three columns in a 1760x940
/// panel: your nest, the field record, and detail on whatever the dex cursor is sitting on.
static class Collection
{
    const float PW = 1760f, PH = 940f;

    public static Col[] Render(int cursor, int caughtCount, bool fresh = false, bool showEgg = false,
                               Eggverse.HudView.NestOrder order = Eggverse.HudView.NestOrder.Caught)
    {
        var c = new Battle.Ctx { Px = new Col[Battle.W * Battle.H] };
        for (int i = 0; i < c.Px.Length; i++) c.Px[i] = Col.Hex(0x05060E);

        float x0 = (Battle.W - PW) / 2f, x1 = x0 + PW;
        float y0 = (Battle.H - PH) / 2f, y1 = y0 + PH;
        Battle.Rect(c, x0, y0, x1, y1, Col.Hex(0x0B0D18));

        Battle.TextCentre(c, Eggverse.UiCopy.CollectionTitle, (x0 + x1) / 2f, y1 - 30, 34, Battle.Accent);

        var all = SpeciesDatabase.All;
        var dim = Battle.InkDim;
        var grey = new Col(0.35f, 0.38f, 0.45f, 1f);

        // ---- column 1: party and nest ----
        //
        // Drawn from CollectionBodyText, line for line. This used to lay the column out itself:
        // its own PARTY heading, its own rows, its own "56 BACK HOME", its own "36 MORE BELOW"
        // typed as a literal. Every one of those was a guess about a string the game composes,
        // and the column grew a line naming the nest's sort order that the render could never
        // have shown. The rows were already being asked for from HudView; the composition was
        // not.
        float bx = x0 + 34, by = y1 - 84;
        float step20 = 20f * 1.16f;

        // One roster for the whole picture. The footer used to be computed from a state built
        // separately, with one egg in the nest against the fifty-six drawn above it - so the
        // render showed a full nest and a footer describing a game that had almost nothing in
        // it. Two states in one screenshot is a render telling two different stories.
        var roster = new Eggverse.GameState(false);
        if (fresh) roster.Party.Add(Eggverse.EggInstance.Wild("sprouteg", 5));
        else
        {
            foreach (var id in new[] { "sprouteg", "frizzlebolt", "bubblenog", "glacegg", "shadowhisk", "boulderoo" })
                roster.Party.Add(Eggverse.EggInstance.Wild(id, 21));
            var stored = new[] { "sprouteg", "tidepoach", "cobblet", "yolkano", "chillet" };
            for (int i = 0; i < 56; i++)
                roster.Nest.Add(Eggverse.EggInstance.Wild(stored[i % stored.Length], 12 + i % 19));
        }

        int nestScroll = 0;
        string column = Eggverse.HudView.CollectionBodyText(
            roster, roster.Party.Count + 4, true, ref nestScroll, order);

        int row = 0;
        foreach (var raw in column.Split('\n'))
        {
            var runs = Runs(raw, Battle.Ink);
            float pen = bx;
            foreach (var r in runs)
            {
                if (r.text.Trim().Length > 0)
                    Battle.Text(c, r.text.ToUpperInvariant(), pen, by - step20 * row, 20, r.col);
                pen += Battle.TextWidth(r.text, 20);
            }
            row++;
        }

        // dividers
        Battle.Rect(c, x0 + 772, y1 - 82 - 786, x0 + 774, y1 - 82, Col.Hex(0x2C3250));
        Battle.Rect(c, x0 + 1236, y1 - 82 - 786, x0 + 1238, y1 - 82, Col.Hex(0x2C3250));

        // ---- column 2: the field record, every species, no window ----
        float dx = x0 + 800, dy = y1 - 84;
        // ---- column 2: the field record ----
        //
        // From CollectionDexText, like the column beside it. This laid out its own header, its
        // own "NN * NAME", its own caught/seen marks and its own counts - four guesses about
        // strings the game composes, and the marks were not even the ones it uses (the record
        // draws filled, half and empty circles; this drew *, - and 0).
        float step19 = 19f * 1.16f;
        for (int i = 0; i < caughtCount && i < all.Count; i++)
        {
            roster.Caught.Add(all[i].Id);
            roster.Seen.Add(all[i].Id);
        }
        for (int i = caughtCount; i < caughtCount + 2 && i < all.Count; i++)
            roster.Seen.Add(all[i].Id);

        int dexRow = 0;
        foreach (var raw in Eggverse.HudView.CollectionDexText(roster, cursor)
                                   .Replace("<size=15>", "").Replace("</size>", "").Split('\n'))
        {
            float pen = dx;
            foreach (var r in Runs(raw, Battle.Ink))
            {
                if (r.text.Trim().Length > 0)
                    Battle.Text(c, r.text.ToUpperInvariant(), pen, dy - step19 * dexRow, 19, r.col);
                pen += Battle.TextWidth(r.text, 19);
            }
            dexRow++;
        }

        // ---- column 3: detail on the cursor ----
        // With the cursor on your own eggs the panel shows that egg instead of the species entry.
        if (showEgg)
        {
            // The game's own egg panel. The transcription this replaces was missing the STATS
            // heading, missing nothing else visible - and had AHEAD above CONDITION, where the
            // game puts it below STATS. Five renders in a row have had a fault like this.
            var shown = Eggverse.EggInstance.WildElder("glacegg", 21);
            shown.Nickname = "Frosty";
            var estate = new Eggverse.GameState();
            foreach (var sp in Eggverse.SpeciesDatabase.All) { estate.Seen.Add(sp.Id); estate.Caught.Add(sp.Id); }

            float ex2 = x0 + 1280, ey2 = y1 - 92;
            Battle.Ellipse(c, ex2 + 75, ey2 - 75, 54, 72, Col.Hex(TypeHex(shown.Type)));
            Battle.Ellipse(c, ex2 + 95, ey2 - 50, 34, 44, new Col(0, 0, 0, 0.20f), 0.9f);

            float tx2 = x0 + 1450, ty2 = ey2;
            foreach (var raw in Eggverse.HudView.EggDetailText(shown).Split('\n'))
            {
                var plain = Strip(raw);
                if (plain.Length == 0) { ty2 -= step19; continue; }
                foreach (var line in WrapLines(plain, 28))
                {
                    Battle.Text(c, line.ToUpperInvariant(), tx2, ty2, 19, FirstCol(raw, Battle.Ink));
                    ty2 -= step19;
                }
            }

            float lx3 = x0 + 1280, ly3 = y1 - 262;
            foreach (var raw in Eggverse.HudView.EggLoreText(shown, estate).Split('\n'))
            {
                var plain = Strip(raw);
                if (plain.Length == 0) { ly3 -= step19; continue; }
                foreach (var line in WrapLines(plain, 46))
                {
                    Battle.Text(c, line.ToUpperInvariant(), lx3, ly3, 19, FirstCol(raw, Battle.Ink));
                    ly3 -= step19;
                }
            }

            Battle.TextCentre(c, FooterFor(roster),
                              Battle.W / 2f, y0 + 40, 20, dim);
            return c.Px;
        }

        var cur = all[Math.Min(cursor, all.Count - 1)];
        float ex = x0 + 1280, ey = y1 - 92;
        // portrait
        Battle.Ellipse(c, ex + 75, ey - 75, 54, 72, Col.Hex(TypeHex(cur.Type)));
        Battle.Ellipse(c, ex + 95, ey - 50, 34, 44, new Col(0, 0, 0, 0.20f), 0.9f);

        float tx = x0 + 1450;
        Battle.Text(c, $"#{cursor + 1:00}  {cur.Name.ToUpperInvariant()}", tx, ey, 26, Battle.Ink);
        Battle.Text(c, $"{TypeChart.Name(cur.Type).ToUpperInvariant()}   {cur.Pattern.ToString().ToUpperInvariant()} SHELL",
                    tx, ey - 34, 19, Col.Hex(TypeHex(cur.Type)));
        Battle.Text(c, TypeChart.TraitName(TypeChart.TraitOf(cur.Type)).ToUpperInvariant(), tx, ey - 76, 19, Battle.Accent);
        // Wrap, never truncate: a mock that silently cuts text hides the very overflow
        // it exists to reveal. 280px at font 19 is ~28 characters a line.
        float ty = ey - 102;
        foreach (var line in WrapLines(TypeChart.TraitBlurb(TypeChart.TraitOf(cur.Type)), 28))
        {
            Battle.Text(c, line.ToUpperInvariant(), tx, ty, 19, dim);
            ty -= step19;
        }

        // The game's own lore column. This used to draw the blurb and nothing else, so 500px
        // of matchups, base stats and the evolution line simply were not in the picture - the
        // column looked half empty in every render I ever took of this screen.
        float lx = x0 + 1280, ly2 = y1 - 262;
        // The state has to match the screen being drawn. Rendering the fresh-start collection
        // from an all-worlds-visited state put "FOUND ON  YOLKHAVEN, MOSSWELL" on a save that
        // has only ever seen Yolkhaven - the render disagreeing with its own premise.
        var seenState = new Eggverse.GameState();
        if (!fresh)
        {
            foreach (var w in Eggverse.PlanetDatabase.All) seenState.Visited.Add(w.Id);
            foreach (var sp in Eggverse.SpeciesDatabase.All) seenState.Seen.Add(sp.Id);
            seenState.Caught.Add(cur.Id);
        }

        foreach (var raw in Eggverse.HudView.DexLoreText(cur, seenState, true).Split('\n'))
        {
            var plain = System.Text.RegularExpressions.Regex.Replace(raw, "<[^>]+>", "");
            if (plain.Length == 0) { ly2 -= step19; continue; }   // blank rows are spacing, not nothing
            foreach (var line in WrapLines(plain, 46))
            {
                Battle.Text(c, line.ToUpperInvariant(), lx, ly2, 19, Battle.Ink);
                ly2 -= step19;
            }
        }

        Battle.TextCentre(c, FooterFor(roster),
                          Battle.W / 2f, y0 + 40, 20, dim);
        return c.Px;
    }

    static System.Collections.Generic.List<string> WrapLines(string s, int n)
    {
        var outp = new System.Collections.Generic.List<string>();
        var words = s.Split(' ');
        string cur = "";
        foreach (var w in words)
        {
            if ((cur + " " + w).Trim().Length > n) { outp.Add(cur.Trim()); cur = w; }
            else cur = (cur + " " + w).Trim();
        }
        if (cur.Length > 0) outp.Add(cur);
        return outp;
    }

    static int TypeHex(EggType t)
    {
        var col = TypeChart.ColorOf(t);
        return ((int)(col.r * 255) << 16) | ((int)(col.g * 255) << 8) | (int)(col.b * 255);
    }

    /// Splits a rich-text line into coloured runs, the same way the title card does.
    static System.Collections.Generic.List<(string text, Col col)> Runs(string line, Col baseCol)
    {
        var outp = new System.Collections.Generic.List<(string, Col)>();
        int i = 0;
        Col current = baseCol;
        while (i < line.Length)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                line.Substring(i), @"^<(/?)(b|color)(=#([0-9A-Fa-f]{6}))?>");
            if (m.Success)
            {
                if (m.Groups[1].Value == "/") current = baseCol;
                else if (m.Groups[2].Value == "color" && m.Groups[4].Success)
                    current = Col.Hex(Convert.ToInt32(m.Groups[4].Value, 16));
                i += m.Length;
                continue;
            }
            // A '<' that is not one of the tags above is literal text, and consuming zero
            // characters here spins forever - which is what "Out of memory" after title.bmp
            // actually was. The record column carries <size=...>, which this never matched.
            int next = line.IndexOf('<', i + 1);
            if (next < 0) next = line.Length;
            outp.Add((line.Substring(i, next - i), current));
            i = next;
        }
        return outp;
    }

    static string Strip(string t) =>
        System.Text.RegularExpressions.Regex.Replace(t, "<[^>]+>", "");

    static Col FirstCol(string rich, Col fallback)
    {
        var m = System.Text.RegularExpressions.Regex.Match(rich, "<color=#([0-9A-Fa-f]{6})>");
        return m.Success ? Col.Hex(Convert.ToInt32(m.Groups[1].Value, 16)) : fallback;
    }

    /// The game's own footer, which changes with what the player actually has.
    static string FooterFor(Eggverse.GameState roster) =>
        Eggverse.HudView.HintFor(roster).ToUpperInvariant();
}
