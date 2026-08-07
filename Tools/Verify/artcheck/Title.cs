using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Eggverse;

/// The title card, from the real UiCopy strings. It is the first thing anyone sees and the only
/// screen a player reads before they know anything, so it is worth looking at rather than only
/// measuring.
static class Title
{
    static readonly Regex Tag = new Regex("<[^>]+>", RegexOptions.Compiled);

    /// Splits a rich-text line into runs, keeping any colour the game asked for.
    static List<(string text, Col col)> Runs(string line, Col baseCol, Col accent)
    {
        var outp = new List<(string, Col)>();
        int i = 0;
        Col current = baseCol;
        while (i < line.Length)
        {
            var m = Regex.Match(line.Substring(i), @"^<(/?)(b|color)(=#([0-9A-Fa-f]{6}))?>");
            if (m.Success)
            {
                if (m.Groups[1].Value == "/") current = baseCol;
                else if (m.Groups[2].Value == "color" && m.Groups[4].Success)
                    current = Col.Hex(Convert.ToInt32(m.Groups[4].Value, 16));
                else if (m.Groups[2].Value == "b") current = accent;
                i += m.Length;
                continue;
            }
            int next = line.IndexOf('<', i);
            if (next < 0) next = line.Length;
            outp.Add((line.Substring(i, next - i), current));
            i = next;
        }
        return outp;
    }

    public static Col[] Render()
    {
        var c = new Battle.Ctx { Px = new Col[Battle.W * Battle.H] };
        for (int i = 0; i < c.Px.Length; i++) c.Px[i] = Col.Hex(0x060710);

        float cx = Battle.W / 2f, cy = Battle.H / 2f;

        Battle.TextCentre(c, UiCopy.Title, cx, cy + 250f + 24f, 96, Battle.Accent);
        Battle.TextCentre(c, UiCopy.Subtitle, cx, cy + 170f + 12f, 34, Battle.Ink);

        // Body: 1500x460 anchored at centre + (0,-110), drawn from the top of that box.
        float top = cy - 110f + 230f;
        float step = 24f * 1.16f;
        var lines = UiCopy.TitleBody.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            float y = top - i * step;
            var runs = Runs(lines[i], Battle.Ink, Battle.Accent);

            // Centre the whole line, then lay the runs out left to right.
            float width = 0f;
            foreach (var r in runs) width += Battle.TextWidth(r.text, 24);
            float pen = cx - width / 2f;
            foreach (var r in runs)
            {
                if (r.text.Length > 0) Battle.Text(c, r.text, pen, y, 24, r.col);
                pen += Battle.TextWidth(r.text, 24);
            }
        }

        Battle.TextCentre(c, UiCopy.BeginHint, cx, 96f + 20f, 30, Battle.Accent);
        Battle.TextCentre(c, "saved run  -  Chapter 2 - The Long Drift  -  Glacierim  -  0:42",
                          cx, 62f + 14f, 21, Battle.InkDim);
        return c.Px;
    }
}
