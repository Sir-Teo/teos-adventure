using System;
using Eggverse;

/// The naming prompt, which had never been drawn.
///
/// It is a text field, and text fields are where the small failures live: what a keystroke does
/// when the buffer is full, whether the caret moves the text under it, what happens to the
/// characters a player is not supposed to type. All three were wrong here, and none of them
/// were visible from the source.
static class Naming
{
    public static Col[] Render(string typed, bool caretLit, bool refused)
    {
        int w = Battle.W, h = Battle.H;
        var c = new Battle.Ctx { Px = new Col[w * h] };

        // Over a surface, dimmed by the shade the view lays down. You name an egg where you
        // caught it.
        var def = PlanetDatabase.Get("glacierim");
        var behind = Surface.Render(w, Program.ToHexPublic(def.Ocean), Program.ToHexPublic(def.Land),
                                    Program.ToHexPublic(def.Atmosphere), def.Theme.ToString(),
                                    def.Seed, "Scattered", 26f, def.Id, true);
        int band = (w - h) / 2;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                c.Px[y * w + x] = Col.Lerp(behind[(y + band) * w + x], Col.Hex(0x000000), 0.62f);

        float cx = w / 2f, cy = h / 2f;
        const float BoxW = 900f, BoxH = 420f;
        float bl = cx - BoxW * 0.5f, bt = cy + BoxH * 0.5f, bb = cy - BoxH * 0.5f;

        Battle.Rect(c, bl, bb, bl + BoxW, bt, Col.Hex(0x121424));
        Battle.Rect(c, bl, bt - 4f, bl + BoxW, bt, Battle.Accent);

        // The egg: 130x130, top-centre pivot at -30 from the box top.
        var species = SpeciesDatabase.All[0];
        Battle.Disc(c, cx, bt - 30f - 65f, 58f, Col.Hex(0x9FD6A0), 0.10f);

        // Title: 820x40, top-centre pivot at -172.
        Battle.TextCentre(c, "Give this " + species.Name + " a nickname?", cx, bt - 172f - 8f, 30, Battle.Ink);

        // Field: 680x76, centred at -60 from the middle of the box.
        float fy = cy - 60f;
        Battle.Rect(c, cx - 340f, fy - 38f, cx + 340f, fy + 38f, Col.Hex(0x0A0C16));

        // The caret keeps its slot whether it is lit or not, which is the point: it used to swap
        // the bar for a space and shift the whole name sideways twice a second.
        float tw = Battle.TextWidth(typed + "|", 38);
        float pen = cx - tw * 0.5f;
        Battle.Text(c, typed, pen, fy + 19f, 38, Battle.Accent);
        if (caretLit)
            Battle.Rect(c, pen + Battle.TextWidth(typed, 38) + 4f, fy - 20f,
                        pen + Battle.TextWidth(typed, 38) + 8f, fy + 20f, Battle.Accent);

        // Hint: 840x28, bottom-centre pivot at +34.
        string hint = NameEntryView.Hint(typed.Length, refused);
        var runs = Strip(hint);
        float hw = 0f;
        foreach (var r in runs) hw += Battle.TextWidth(r.text, 20);
        float hp = cx - hw * 0.5f;
        foreach (var r in runs)
        {
            if (r.text.Trim().Length > 0) Battle.Text(c, r.text, hp, bb + 34f + 24f, 20, r.col);
            hp += Battle.TextWidth(r.text, 20);
        }

        return c.Px;
    }

    static System.Collections.Generic.List<(string text, Col col)> Strip(string line)
    {
        var outp = new System.Collections.Generic.List<(string, Col)>();
        int i = 0;
        Col current = Battle.InkDim;
        while (i < line.Length)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                line.Substring(i), @"^<(/?)(b|color)(=#([0-9A-Fa-f]{6}))?>");
            if (m.Success)
            {
                if (m.Groups[1].Value == "/") current = Battle.InkDim;
                else if (m.Groups[2].Value == "color" && m.Groups[4].Success)
                    current = Col.Hex(Convert.ToInt32(m.Groups[4].Value, 16));
                i += m.Length;
                continue;
            }
            int next = line.IndexOf('<', i + 1);
            if (next < 0) next = line.Length;
            outp.Add((line.Substring(i, next - i), current));
            i = next;
        }
        return outp;
    }

    public static string Report()
    {
        string full = new string('M', NameEntryView.MaxLength);
        float fieldPx = Battle.TextWidth(full + "|", 38);
        string hint = System.Text.RegularExpressions.Regex.Replace(
            NameEntryView.Hint(NameEntryView.MaxLength, true), "<[^>]+>", "");
        return $"  naming: a full {NameEntryView.MaxLength}-character name = {fieldPx:0}px in a 680px field\n" +
               $"  naming hint at the cap = {Battle.TextWidth(hint, 20):0}px in 840px";
    }
}
