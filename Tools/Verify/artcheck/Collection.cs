using System;
using Eggverse;

/// The collection screen, driven by the real SpeciesDatabase. Three columns in a 1760x940
/// panel: your nest, the field record, and detail on whatever the dex cursor is sitting on.
static class Collection
{
    const float PW = 1760f, PH = 940f;

    public static Col[] Render(int cursor, int caughtCount, bool fresh = false, bool showEgg = false)
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
        float bx = x0 + 34, by = y1 - 84;
        float step20 = 20f * 1.16f;
        Battle.Text(c, "PARTY", bx, by, 20, Battle.Accent);
        if (!fresh) Battle.Text(c, "PRESS 1-6 TO LEAD WITH THAT EGG", bx + 130, by, 20, grey);
        string[] party =
        {
            "1 PEBBLES        LV 22  VERDANT   91/91",
            "2 FRIZZLEBOLT    LV 21  VOLT      78/78",
            "3 BUBBLENOG      LV 21  TIDAL     80/80",
            "4 ELDER GLACEGG  LV 24  FROST     96/96",
            "5 SHADOWHISK     LV 20  VOID      74/74",
            "6 BOULDEROO      LV 20  STONE     88/88",
        };
        int partyShown = fresh ? 1 : party.Length;
        for (int i = 0; i < partyShown; i++)
            Battle.Text(c, fresh ? "  SPROUTEG        LV  5  VERDANT   22/22" : party[i],
                        bx, by - step20 * (i + 1), 20, Battle.Ink);

        float ny = by - step20 * (partyShown + 2);
        Battle.Text(c, "NEST", bx, ny, 20, Battle.Accent);
        if (fresh)
        {
            Battle.Text(c, "EMPTY. ONCE YOUR PARTY IS FULL, ANYTHING ELSE YOU", bx, ny - step20, 20, grey);
            Battle.Text(c, "CATCH WAITS HERE - AND YOU CAN TRADE IT BACK IN.", bx, ny - step20 * 2, 20, grey);
        }
        else Battle.Text(c, "56 BACK HOME", bx + 110, ny, 20, dim);
        // 20, matching the game's window.
        string[] kinds = { "SPROUTEG", "TIDEPOACH", "COBBLET", "YOLKANO", "CHILLET" };
        string[] elems = { "VERDANT", "TIDAL", "STONE", "MOLTEN", "FROST" };
        const int focusRow = 4;   // the nest cursor, four rows down
        for (int i = 0; !fresh && i < 20; i++)
        {
            int hp = 40 + i * 3;
            bool here = i == focusRow;
            if (here) Battle.Text(c, "*", bx, ny - step20 * (i + 1), 20, Battle.Accent);
            Battle.Text(c, $"  {kinds[i % 5],-11} LV {12 + i}  {elems[i % 5],-9} {hp}/{hp}",
                        bx + 18, ny - step20 * (i + 1), 20, here ? Battle.Accent : Battle.Ink);
        }
        if (!fresh) Battle.Text(c, "      ...AND 36 MORE", bx, ny - step20 * 21, 20, grey);

        // dividers
        Battle.Rect(c, x0 + 772, y1 - 82 - 786, x0 + 774, y1 - 82, Col.Hex(0x2C3250));
        Battle.Rect(c, x0 + 1236, y1 - 82 - 786, x0 + 1238, y1 - 82, Col.Hex(0x2C3250));

        // ---- column 2: the field record, every species, no window ----
        float dx = x0 + 800, dy = y1 - 84;
        float step19 = 19f * 1.16f;
        Battle.Text(c, "FIELD RECORD", dx, dy, 19, Battle.Accent);
        Battle.Text(c, $"{caughtCount}/{SpeciesDatabase.CatchableCount} CAUGHT · {caughtCount + 2}/{SpeciesDatabase.Count} SEEN",
                    dx + 150, dy, 19, dim);

        for (int i = 0; i < all.Count; i++)
        {
            var sp = all[i];
            bool caught = i < caughtCount;
            bool seen = i < caughtCount + 2;
            bool here = i == cursor;
            float ly = dy - step19 * (i + 2);
            var tint = caught ? Col.Hex(TypeHex(sp.Type)) : seen ? dim : grey;
            string mark = caught ? "*" : seen ? "-" : "0";
            string name = (caught || seen) ? sp.Name.ToUpperInvariant() : "? ? ?";
            if (here) Battle.Text(c, ">", dx - 12, ly, 19, Battle.Accent);
            // The element beside the name, at the size the record draws it — the list used to
            // carry element by colour alone, which is the one thing this palette is not allowed
            // to do.
            string kind = (caught || seen) ? "  " + Eggverse.TypeChart.Abbrev(sp.Type) : "";
            Battle.Text(c, $"{i + 1:00} {mark} {name}", dx + 8, ly, 19, tint);
            if (kind.Length > 0)
                Battle.Text(c, kind, dx + 8 + Battle.TextWidth($"{i + 1:00} {mark} {name}", 19), ly, 15, dim);
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

            Battle.TextCentre(c, FooterFor(false),
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

        Battle.TextCentre(c, FooterFor(fresh),
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

    static string Strip(string t) =>
        System.Text.RegularExpressions.Regex.Replace(t, "<[^>]+>", "");

    static Col FirstCol(string rich, Col fallback)
    {
        var m = System.Text.RegularExpressions.Regex.Match(rich, "<color=#([0-9A-Fa-f]{6})>");
        return m.Success ? Col.Hex(Convert.ToInt32(m.Groups[1].Value, 16)) : fallback;
    }

    /// The game's own footer, which changes with what the player actually has.
    static string FooterFor(bool fresh)
    {
        var st = new Eggverse.GameState();
        if (!fresh)
        {
            while (st.Party.Count < Eggverse.GameState.PartySize)
                st.Party.Add(Eggverse.EggInstance.Wild("sprouteg", 5));
            st.Nest.Add(Eggverse.EggInstance.Wild("sprouteg", 5));
        }
        return Eggverse.HudView.HintFor(st).ToUpperInvariant();
    }
}
