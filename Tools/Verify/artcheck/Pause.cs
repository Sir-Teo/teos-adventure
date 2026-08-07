using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Eggverse;

/// The pause menu, both pages.
///
/// It had never been rendered. It is the screen a player opens more often than any other outside
/// a fight — to save, to turn the music down, to check which key lifts off — and the only proof
/// it looked like anything was that the constants passed an overlap check.
///
/// Every string and every coordinate here comes from PauseView itself. Nothing is transcribed:
/// this is the fault that has been found on five screens already, and a screen nobody has looked
/// at is exactly where a second copy would have sat unnoticed.
static class Pause
{
    static readonly Regex Tag = new Regex("<[^>]+>", RegexOptions.Compiled);

    static List<(string text, Col col)> Runs(string line, Col baseCol)
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

    /// <param name="settings">The settings page rather than the main one.</param>
    /// <param name="cursor">Which row the cursor is on.</param>
    public static Col[] Render(bool settings, int cursor)
    {
        int w = Battle.W, h = Battle.H;
        var c = new Battle.Ctx { Px = new Col[w * h] };

        // Whatever is behind it, dimmed by the shade the view lays over the screen. Space is
        // what a player usually pauses from, and a menu judged against flat black is a menu
        // judged against a background the game never has.
        var behind = Space.Render(w, 0f, 0f, 30f, false);
        int band = (w - h) / 2;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                c.Px[y * w + x] = Col.Lerp(behind[(y + band) * w + x], Col.Hex(0x000000), 0.66f);

        float cx = w / 2f, cy = h / 2f;
        float boxTop = cy + PauseView.BoxHeight * 0.5f;
        float boxLeft = cx - PauseView.BoxWidth * 0.5f;

        // The box, then its accent edge along the top.
        Battle.Rect(c, boxLeft, cy - PauseView.BoxHeight * 0.5f,
                    boxLeft + PauseView.BoxWidth, boxTop, Col.Hex(0x111322));
        Battle.Rect(c, boxLeft, boxTop - 4f, boxLeft + PauseView.BoxWidth, boxTop, Battle.Accent);

        // Title: 700x48 at -38 from the top of the box, font 40.
        Battle.TextCentre(c, settings ? "SETTINGS" : "PAUSED", cx, boxTop - 38f - 12f, 40, Battle.Accent);

        // The widest state each page can reach: a fresh run mutes nothing, music sits at its
        // default 0.6 of 0.6, and "relaxed" is the longest of the four text speeds.
        var rows = settings
            ? PauseView.SettingsRows(false, 0.42f, 1f, true, "relaxed")
            : PauseView.MainRows(cursor == 3);

        for (int i = 0; i < rows.Length; i++)
        {
            bool sel = i == cursor;
            // Text() takes the top of the glyph band and draws downward, so a row centred on
            // rowY starts half a font above it. The first pass had this half a band out and the
            // drawn meters landed a whole row high - visible the moment it was looked at, and
            // invisible to every number the check prints.
            float rowY = boxTop + PauseView.FirstRowY - i * PauseView.RowStep;
            float y = rowY + PauseView.RowFont * 0.5f;

            // The caret sits in its own gutter, at the same x on every row. It used to be glued
            // to the front of a centre-anchored string, which slid it sideways down the list.
            // Drawn, not typed: the render font has no caret glyph, and a question mark on every
            // selected row would be the render lying about the one thing it exists to show.
            if (sel)
                for (int t = 0; t < 9; t++)
                    Battle.Rect(c, cx + PauseView.GutterX + 10f + t, rowY - 9f + t,
                                cx + PauseView.GutterX + 11f + t, rowY + 9f - t, Battle.Accent);

            Battle.Text(c, rows[i].label, cx + PauseView.LabelX, y, (int)PauseView.RowFont,
                        sel ? Battle.Accent : Battle.Ink);

            foreach (var r in Runs(rows[i].value, Battle.Ink))
            {
                // The volume meter is drawn rather than typed - the render font has no bar glyph,
                // and a row of question marks would have been a lie about what a player sees.
                float pen = cx + PauseView.ValueX;
                if (r.text.IndexOf('|') >= 0)
                {
                    for (int b = 0; b < r.text.Length; b++)
                    {
                        if (r.text[b] != '|') continue;
                        Battle.Rect(c, pen + b * 14f, rowY - 9f, pen + b * 14f + 7f, rowY + 9f, r.col);
                    }
                    continue;
                }
                if (r.text.Trim().Length > 0)
                    Battle.Text(c, r.text, pen, y, (int)PauseView.RowFont, r.col);
            }
        }

        // The controls reference sits on the main page only.
        if (!settings)
        {
            Battle.Rect(c, cx - 330f, boxTop + PauseView.RuleY, cx + 330f,
                        boxTop + PauseView.RuleY + 2f, Col.Hex(0x2C3250));
            for (int i = 0; i < UiCopy.PauseControls.Length; i++)
                Battle.TextCentre(c, UiCopy.PauseControls[i], cx,
                                  boxTop + PauseView.ControlsY - i * PauseView.ControlsStep
                                      - PauseView.ControlsFont * 0.5f,
                                  (int)PauseView.ControlsFont, Battle.InkDim);
        }

        // Footer: anchored to the bottom of the box, 60 tall, centred at +42.
        bool onSlider = settings && (cursor == 1 || cursor == 2);
        Battle.TextCentre(c, PauseView.FooterText(!settings, onSlider), cx,
                          cy - PauseView.BoxHeight * 0.5f + 42f - PauseView.FooterFont * 0.5f,
                          (int)PauseView.FooterFont, Battle.InkDim);

        return c.Px;
    }

    /// What the render measured, printed next to the numbers it had to fit inside.
    public static string Report()
    {
        float widest = 0f; string worst = "";
        var all = new List<(string label, string value)>();
        all.AddRange(PauseView.MainRows(true));
        all.AddRange(PauseView.SettingsRows(false, 0.6f, 1f, true, "relaxed"));
        foreach (var r in all)
        {
            float px = Battle.TextWidth(Tag.Replace(r.label, ""), (int)PauseView.RowFont);
            if (px > widest) { widest = px; worst = r.label; }
        }

        float ctrl = 0f;
        foreach (var line in UiCopy.PauseControls)
            ctrl = Math.Max(ctrl, Battle.TextWidth(line, (int)PauseView.ControlsFont));

        // Where the last settings row ends, against where the footer begins.
        float lastRow = PauseView.FirstRowY - 5 * PauseView.RowStep - PauseView.RowHeight * 0.5f;
        float footerTop = -PauseView.BoxHeight + 42f + 30f;

        return $"  pause labels: widest \"{worst}\" = {widest:0}px in {PauseView.LabelWidth:0}px\n" +
               $"  pause controls: widest line = {ctrl:0}px in {PauseView.RowWidth:0}px\n" +
               $"  pause settings: last row floor {lastRow:0}, footer ceiling {footerTop:0}, " +
               $"box floor {-PauseView.BoxHeight:0}";
    }
}
