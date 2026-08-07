using System;
using Eggverse;

/// The card that follows Amy's last words.
///
/// It had never been rendered. It is the last thing the game says and the only screen a player
/// reaches once, so it is the one most worth looking at and the one nobody had.
static class Ending
{
    /// Both endings: the ordinary one, and the one a player who read all seventeen inscriptions
    /// gets.
    public static (Col[] px, int w, int h) Render(bool readEverything)
    {
        int w = Battle.W, h = Battle.H;
        var c = new Battle.Ctx { Px = new Col[w * h] };
        for (int i = 0; i < c.Px.Length; i++) c.Px[i] = Col.Hex(0x0A0614);

        float cx = w / 2f, cy = h / 2f;

        // Heading: 1400x110 centred at +186, font 82.
        Battle.TextCentre(c, UiCopy.VictoryHeading, cx, cy + 186f + 27f, 82, Battle.Accent);

        // Body: 1300x460 anchored at -140, drawn from the top of that box, font 28.
        string text = UiCopy.VictoryBody
                    + (readEverything ? UiCopy.VictoryCoda : "")
                    + UiCopy.VictoryTally(120, 24, 24, 8);

        // uGUI centres the block in the box, so the block is laid out from its own height -
        // the same arithmetic the move buttons need. Drawing from a fixed top made the shorter
        // ending look like it was falling out of the card.
        float step = 28f * 1.16f;
        int rows = text.Split('\n').Length;
        float top = cy - 140f + (rows * step) * 0.5f - step * 0.5f;
        int row = 0;
        foreach (var raw in text.Split('\n'))
        {
            var plain = Strip(raw);
            if (plain.Length > 0)
                Battle.TextCentre(c, plain, cx, top - row * step, 28, Colour(raw));
            row++;
        }

        Battle.TextCentre(c, "press SPACE to keep exploring", cx, 80f + 17f, 26, Battle.InkDim);
        return (c.Px, w, h);
    }

    static string Strip(string t) =>
        System.Text.RegularExpressions.Regex.Replace(t, "<[^>]+>", "");

    static Col Colour(string rich)
    {
        var m = System.Text.RegularExpressions.Regex.Match(rich, "<color=#([0-9A-Fa-f]{6})>");
        return m.Success ? Col.Hex(Convert.ToInt32(m.Groups[1].Value, 16)) : Battle.Ink;
    }
}
