using System;
using Eggverse;

/// The collection screen, driven by the real SpeciesDatabase. Three columns in a 1760x940
/// panel: your nest, the field record, and detail on whatever the dex cursor is sitting on.
static class Collection
{
    const float PW = 1760f, PH = 940f;

    public static Col[] Render(int cursor, int caughtCount)
    {
        var c = new Battle.Ctx { Px = new Col[Battle.W * Battle.H] };
        for (int i = 0; i < c.Px.Length; i++) c.Px[i] = Col.Hex(0x05060E);

        float x0 = (Battle.W - PW) / 2f, x1 = x0 + PW;
        float y0 = (Battle.H - PH) / 2f, y1 = y0 + PH;
        Battle.Rect(c, x0, y0, x1, y1, Col.Hex(0x0B0D18));

        Battle.TextCentre(c, "YOUR COLLECTION", (x0 + x1) / 2f, y1 - 30, 34, Battle.Accent);

        var all = SpeciesDatabase.All;
        var dim = Battle.InkDim;
        var grey = new Col(0.35f, 0.38f, 0.45f, 1f);

        // ---- column 1: party and nest ----
        float bx = x0 + 34, by = y1 - 84;
        float step20 = 20f * 1.16f;
        Battle.Text(c, "PARTY", bx, by, 20, Battle.Accent);
        Battle.Text(c, "PRESS 1-6 TO LEAD WITH THAT EGG", bx + 130, by, 20, grey);
        string[] party =
        {
            "1 PEBBLES        LV 22  VERDANT   91/91",
            "2 FRIZZLEBOLT    LV 21  VOLT      78/78",
            "3 BUBBLENOG      LV 21  TIDAL     80/80",
            "4 ELDER GLACEGG  LV 24  FROST     96/96",
            "5 SHADOWHISK     LV 20  VOID      74/74",
            "6 BOULDEROO      LV 20  STONE     88/88",
        };
        for (int i = 0; i < party.Length; i++)
            Battle.Text(c, party[i], bx, by - step20 * (i + 1), 20, Battle.Ink);

        float ny = by - step20 * 8;
        Battle.Text(c, "NEST", bx, ny, 20, Battle.Accent);
        Battle.Text(c, "56 BACK HOME", bx + 110, ny, 20, dim);
        // 20, matching the game's window.
        string[] kinds = { "SPROUTEG", "TIDEPOACH", "COBBLET", "YOLKANO", "CHILLET" };
        string[] elems = { "VERDANT", "TIDAL", "STONE", "MOLTEN", "FROST" };
        const int focusRow = 4;   // the nest cursor, four rows down
        for (int i = 0; i < 20; i++)
        {
            int hp = 40 + i * 3;
            bool here = i == focusRow;
            if (here) Battle.Text(c, "*", bx, ny - step20 * (i + 1), 20, Battle.Accent);
            Battle.Text(c, $"  {kinds[i % 5],-11} LV {12 + i}  {elems[i % 5],-9} {hp}/{hp}",
                        bx + 18, ny - step20 * (i + 1), 20, here ? Battle.Accent : Battle.Ink);
        }
        Battle.Text(c, "      ...AND 36 MORE", bx, ny - step20 * 21, 20, grey);

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
            Battle.Text(c, $"{i + 1:00} {mark} {name}", dx + 8, ly, 19, tint);
        }

        // ---- column 3: detail on the cursor ----
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

        float lx = x0 + 1280, ly2 = y1 - 262;
        foreach (var line in WrapLines(cur.Blurb ?? "", 46))
        {
            Battle.Text(c, line.ToUpperInvariant(), lx, ly2, 19, Battle.Ink);
            ly2 -= step19;
        }

        Battle.TextCentre(c, "LEFT/RIGHT PICK A COLUMN · UP/DOWN MOVE · ENTER SWAPS A NEST EGG IN · 1-6 LEADS · TAB CLOSES",
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
}
