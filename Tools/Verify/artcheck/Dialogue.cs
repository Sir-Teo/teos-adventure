using System;
using System.Collections.Generic;
using Eggverse;

/// The dialogue box, with a real speaker, a real portrait and a real line.
///
/// It had never been drawn either. It is where every word of the story is delivered — seventeen
/// residents, an opening brief, and Amy at the end of it — and the only thing anyone had checked
/// was that the strings fit a rectangle.
///
/// Geometry read from DialogueView.Layout, which was extracted so this file could not transcribe
/// it. Every box on that screen pivots from a corner, not its centre.
static class Dialogue
{
    /// <param name="revealed">How much of the line has typed itself out, 0..1.</param>
    public static Col[] Render(string speaker, string line, float revealed, bool last)
    {
        int w = Battle.W, h = Battle.H;
        var c = new Battle.Ctx { Px = new Col[w * h] };

        // Standing on a world, which is what the box is nearly always over.
        var def = PlanetDatabase.Get("mosswell");
        var behind = Surface.Render(w, Program.ToHexPublic(def.Ocean), Program.ToHexPublic(def.Land),
                                    Program.ToHexPublic(def.Atmosphere), def.Theme.ToString(),
                                    def.Seed, "Scattered", 26f, def.Id, true);
        int band = (w - h) / 2;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                c.Px[y * w + x] = Col.Lerp(behind[(y + band) * w + x], Col.Hex(0x000000), 0.35f);

        float bl = w / 2f - DialogueView.BoxWidth * 0.5f;
        float bb = DialogueView.BoxBottom, bt = bb + DialogueView.BoxHeight;

        Battle.Rect(c, bl, bb, bl + DialogueView.BoxWidth, bt, Col.Hex(0x101220));
        Battle.Rect(c, bl, bt - 4f, bl + DialogueView.BoxWidth, bt, Battle.Accent);

        var tint = TintOf(speaker);

        // Portrait frame: top-left pivot, so the offset is the corner.
        float px0 = bl + DialogueView.PortraitX, py1 = bt + DialogueView.PortraitY;
        float ps = DialogueView.PortraitSize;
        Col frame = Col.Lerp(Col.Hex(0x232A44), tint, 0.35f);
        Battle.Rect(c, px0, py1 - ps, px0 + ps, py1, frame);

        int inner = (int)ps - DialogueView.PortraitInset * 2;
        var face = Face(speaker, tint, inner);
        for (int y = 0; y < inner; y++)
            for (int x = 0; x < inner; x++)
            {
                var p = face[y * inner + x];
                if (p.a <= 0f) continue;
                int sx = (int)px0 + DialogueView.PortraitInset + x;
                int sy = (int)(py1 - ps) + DialogueView.PortraitInset + y;
                if (sx < 0 || sx >= w || sy < 0 || sy >= h) continue;
                c.Px[sy * w + sx] = Col.Lerp(c.Px[sy * w + sx], p, p.a);
            }

        // Speaker: top-left pivot at (SpeakerX, SpeakerY) from the box's top-left corner.
        Battle.Text(c, speaker, bl + DialogueView.TextX, bt + DialogueView.SpeakerY,
                    DialogueView.SpeakerFont, tint);

        // Body: top-left pivot, wrapped at the real width, drawn downward from the real top.
        int shown = Math.Max(0, Math.Min(line.Length, (int)(line.Length * revealed)));
        float step = DialogueView.BodyFont * 1.16f;
        float top = bt + DialogueView.BodyY;
        var rows = Wrap(line.Substring(0, shown), DialogueView.BodyWidth, DialogueView.BodyFont);
        for (int i = 0; i < rows.Count; i++)
            Battle.Text(c, rows[i], bl + DialogueView.TextX, top - i * step,
                        DialogueView.BodyFont, Battle.Ink);

        // Hint: bottom-right pivot, so the offset is that corner.
        string hint = DialogueView.HintText(revealed >= 1f, last);
        float hw = Battle.TextWidth(hint, DialogueView.HintFont);
        Battle.Text(c, hint,
                    bl + DialogueView.BoxWidth + DialogueView.HintX - hw,
                    bb + DialogueView.HintY + DialogueView.HintFont,
                    DialogueView.HintFont, Battle.InkDim);

        return c.Px;
    }

    static List<string> Wrap(string text, float width, int font)
    {
        var outp = new List<string>();
        var line = "";
        foreach (var word in text.Split(' '))
        {
            var next = line.Length == 0 ? word : line + " " + word;
            if (Battle.TextWidth(next, font) > width && line.Length > 0) { outp.Add(line); line = word; }
            else line = next;
        }
        if (line.Length > 0) outp.Add(line);
        return outp;
    }

    static Col TintOf(string speaker)
    {
        foreach (var p in Cast.People()) if (p.name == speaker) return p.tint;
        return Col.Hex(0xA8B2C4);
    }

    static Col[] Face(string key, Col tint, int size)
    {
        var px = new Col[size * size];
        var eyes = Col.Hex(0x0F0F1A);
        foreach (var b in PortraitForm.Shapes(PortraitForm.For(key)))
        {
            Col col = b.Eyes ? eyes : b.Glint ? new Col(1f, 1f, 1f, 1f) : tint.Mul(b.Shade);
            Cast.EllPublic(px, size, b.Cx, b.Cy, b.Rx, b.Ry,
                           new Col(col.r, col.g, col.b, b.Alpha), b.Soft);
        }
        return px;
    }

    /// The longest line anybody says, walked the same way the self-check walks it.
    public static (string speaker, string text) Longest()
    {
        string bs = "", bt = "";
        int worstRows = 0;
        foreach (var l in StoryDatabase.EveryLine())
        {
            int rows = Wrap(l.Text, DialogueView.BodyWidth, DialogueView.BodyFont).Count;
            // Rows, not characters. The longest string is not always the tallest block once it
            // wraps, and the box only cares about the second one.
            if (rows > worstRows || (rows == worstRows && l.Text.Length > bt.Length))
            { worstRows = rows; bt = l.Text; bs = l.Speaker; }
        }
        return (bs, bt);
    }

    public static string Report()
    {
        var (sp, text) = Longest();
        int rows = Wrap(text, DialogueView.BodyWidth, DialogueView.BodyFont).Count;
        int cap = (int)(DialogueView.BodyHeight / (DialogueView.BodyFont * 1.16f));
        float bodyFloor = DialogueView.BoxHeight + DialogueView.BodyY - DialogueView.BodyHeight;
        return $"  dialogue: tallest line is {sp}'s at {text.Length} chars -> {rows} wrapped rows in {cap}\n" +
               $"  dialogue box: {DialogueView.BoxHeight:0} tall, body floor {bodyFloor:0} above it, " +
               $"hint ceiling {DialogueView.HintY + 26f:0}";
    }
}
