using System;
using System.Collections.Generic;
using Eggverse;

/// The screen a player spends most of the game looking at, and the last one nobody had drawn.
///
/// Every menu in the game has been rendered and looked at. The plain surface HUD — the party
/// strip, the objective panel, the supplies, the prompt, a toast — had not, and it is what is on
/// screen for the whole of every walk between fights.
///
/// Geometry and text both from HudView. Nothing here is typed.
static class Hud
{
    public static Col[] Render(bool cold, bool inSpace = false)
    {
        int w = Battle.W, h = Battle.H;
        var c = new Battle.Ctx { Px = new Col[w * h] };

        var def = PlanetDatabase.Get(cold ? "mosswell" : "glacierim");
        var behind = inSpace
            ? Space.Render(w, 0f, 0f, 30f, false)
            : Surface.Render(w, Program.ToHexPublic(def.Ocean), Program.ToHexPublic(def.Land),
                             Program.ToHexPublic(def.Atmosphere), def.Theme.ToString(),
                             def.Seed, "Scattered", 26f, def.Id, true);
        int band = (w - h) / 2;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                c.Px[y * w + x] = behind[(y + band) * w + x];

        var state = Roster(def);
        var story = new StoryState();
        story.RestoreFrom(new string[0], 2);

        // ---- party strip, top left ----
        float px = HudView.PartyAt.x, ptop = h + HudView.PartyAt.y;
        Panel(c, px, ptop - HudView.PartySize.y, px + HudView.PartySize.x, ptop);
        Battle.Text(c, "PARTY", px + 16f, ptop + HudView.PartyTitleY - 12f, 20, Battle.Accent);

        for (int i = 0; i < state.Party.Count; i++)
        {
            float rowTop = ptop + HudView.PartyRowY - i * HudView.PartyRowStep;
            // The label anchors to the top of its slot at -2, and Text draws downward from the
            // top of the glyph band. Centring it on the row instead put the text straight
            // through the health bar underneath.
            float y = rowTop - 2f;
            var egg = state.Party[i];

            // The leader's dot runs the full row height; the others are short. Shape as well as
            // colour, which is the rule this palette works to everywhere.
            bool leads = ReferenceEquals(egg, state.Leader);
            var tint = TypeChart.ColorOf(egg.Type);
            float dotH = leads ? 44f : 34f;
            Battle.Rect(c, px + 14f, rowTop - HudView.PartyRowHeight * 0.5f - dotH * 0.5f,
                        px + 22f, rowTop - HudView.PartyRowHeight * 0.5f + dotH * 0.5f,
                        new Col(tint.r, tint.g, tint.b, 1f));

            Runs(c, HudView.PartyRow(egg, leads), px + 38f, y, HudView.PartyRowFont, Battle.Ink);

            // The health bar under each row: 356x8 at (24,6) inside a 392x50 slot. Leaving it
            // out made the render silent about the one thing a player checks this panel for -
            // the party strip exists to answer "who is hurt", and a picture of it that cannot
            // show a hurt egg is a picture of a different panel.
            float barY = rowTop - HudView.PartyRowHeight + 6f;
            float bx0 = px + 14f + 24f, bx1 = bx0 + 356f;
            Battle.Rect(c, bx0, barY, bx1, barY + 8f, Col.Hex(0x0C0E18));
            var hc = UIKit.HealthColor(egg.HPFraction);
            Battle.Rect(c, bx0, barY, bx0 + (bx1 - bx0) * egg.HPFraction, barY + 8f,
                        new Col(hc.r, hc.g, hc.b, 1f));
        }

        // ---- objective panel, top right ----
        float ox = w + HudView.ObjectiveAt.x - HudView.ObjectiveSize.x, otop = h + HudView.ObjectiveAt.y;
        Panel(c, ox, otop - HudView.ObjectiveSize.y, ox + HudView.ObjectiveSize.x, otop);

        // What the panel calls where you are. In space it is not a world.
        Battle.Text(c, inSpace ? "DEEP SPACE" : def.Name.ToUpperInvariant(),
                    ox + 18f, otop + HudView.PlanetY - 6f, HudView.PlanetFont, Battle.Accent);

        var beat = story.Current;
        string blocker = story.CurrentBlockerText(state);
        string objective = "<color=#FFC24D>" + beat.Chapter + "</color>\n" + beat.Objective +
                           (blocker != null ? "\n<color=#A8B2C4>Still needed: " + blocker + "</color>" : "");
        int row = 0;
        foreach (var line in Wrap(objective, 524f, HudView.ObjectiveFont))
            Runs(c, line, ox + 18f, otop + HudView.ObjectiveY - 6f - row++ * HudView.ObjectiveFont * 1.16f,
                 HudView.ObjectiveFont, Battle.Ink);

        // Supplies sit on the floor of the same panel.
        var supply = new List<string>(Wrap(HudView.SuppliesLine(state), 524f, HudView.SuppliesFont));
        for (int i = 0; i < supply.Count; i++)
            Runs(c, supply[i], ox + 18f,
                 otop - HudView.ObjectiveSize.y + HudView.SuppliesY +
                     (supply.Count - i) * HudView.SuppliesFont * 1.16f,
                 HudView.SuppliesFont, Battle.InkDim);

        // ---- prompt, bottom centre ----
        // The approach prompt, which is what a player flying actually reads. On the surface it
        // is the movement line.
        string prompt = inSpace
            ? "Press E to land on " + def.Name + "  ·  Lv " + def.MinLevel + "-" + def.MaxLevel +
              "  ·  " + Owed(def, state)
            : "WASD walk  ·  Q lift off  ·  Tab party";
        Battle.TextCentre(c, Strip(prompt), w / 2f, HudView.PromptAt.y + 20f, 24, Battle.Ink);

        // ---- toast, under the top edge ----
        string toast = cold ? UiCopy.RestCold[0] : UiCopy.RestIdle[0];
        float ty = h + HudView.ToastAt.y - HudView.ToastSize.y * 0.5f;
        Panel(c, w / 2f - HudView.ToastSize.x * 0.5f, ty - 24f,
              w / 2f + HudView.ToastSize.x * 0.5f, ty + 24f);
        Battle.TextCentre(c, Strip(toast), w / 2f, ty + 8f, 22, Battle.Ink);

        return c.Px;
    }

    /// GameDirector.StillOwed, which is an instance method - the same two sentences, built from
    /// the same count, so the render cannot invent a third phrasing of it.
    static string Owed(PlanetDef planet, GameState state)
    {
        int missing = state.UnrecordedOn(planet);
        return missing == 0
            ? "Every egg here is already in your record."
            : Words.Count(missing, "egg") + " here you have not recorded yet.";
    }

    static GameState Roster(PlanetDef where)
    {
        var st = new GameState(false);
        foreach (var id in new[] { "sprouteg", "glacegg", "frizzlebolt", "cobblet" })
            st.Party.Add(EggInstance.Wild(id, 14 + st.Party.Count * 2));
        st.Party[0].Nickname = "Pebbles";
        st.Party[1].TakeDamage(st.Party[1].MaxHP / 3);
        st.Cartons = 2;             // low, so the amber "low" word shows
        st.CurrentPlanetId = where.Id;
        st.Visited.Add(where.Id);
        foreach (var sp in where.Spawns) st.Seen.Add(sp.SpeciesId);
        st.Caught.Add(where.Spawns[0].SpeciesId);
        return st;
    }

    static void Panel(Battle.Ctx c, float x0, float y0, float x1, float y1)
    {
        for (int y = (int)Math.Max(0, y0); y < (int)Math.Min(Battle.H, y1); y++)
            for (int x = (int)Math.Max(0, x0); x < (int)Math.Min(Battle.W, x1); x++)
            {
                var u = c.Px[y * Battle.W + x];
                c.Px[y * Battle.W + x] = Col.Lerp(u, Col.Hex(0x11131F), 0.88f);
            }
    }

    static IEnumerable<string> Wrap(string text, float width, int font)
    {
        foreach (var para in text.Split('\n'))
        {
            var line = "";
            foreach (var word in para.Split(' '))
            {
                var next = line.Length == 0 ? word : line + " " + word;
                if (Battle.TextWidth(Strip(next), font) > width && line.Length > 0)
                { yield return line; line = word; }
                else line = next;
            }
            yield return line;
        }
    }

    static string Strip(string t) => System.Text.RegularExpressions.Regex.Replace(t, "<[^>]+>", "");

    static void Runs(Battle.Ctx c, string line, float x, float y, int size, Col baseCol)
    {
        int i = 0;
        Col cur = baseCol;
        float pen = x;
        while (i < line.Length)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                line.Substring(i), @"^<(/?)(b|color|size)(=#?[0-9A-Fa-f]*)?>");
            if (m.Success)
            {
                if (m.Groups[1].Value == "/") cur = baseCol;
                else if (m.Groups[2].Value == "color" && m.Groups[3].Value.Length == 8)
                    cur = Col.Hex(Convert.ToInt32(m.Groups[3].Value.Substring(2), 16));
                i += m.Length;
                continue;
            }
            int next = line.IndexOf('<', i + 1);
            if (next < 0) next = line.Length;
            string run = line.Substring(i, next - i);
            if (run.Trim().Length > 0) Battle.Text(c, run, pen, y, size, cur);
            pen += Battle.TextWidth(run, size);
            i = next;
        }
    }
}
