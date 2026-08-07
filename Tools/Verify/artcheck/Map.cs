using System;
using Eggverse;
using UnityEngine;

/// The navigation chart overlay, at canvas reference resolution. This is the screen the
/// player reads to decide where to go next, so it is worth looking at rather than assuming.
static class Map
{
    /// The world the detail panel describes. The map and the panel have to agree, or the render
    /// shows one world highlighted and describes another.
    const string Selected = "glacierim";

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
            int next = line.IndexOf('<', i + 1);
            if (next < 0) next = line.Length;
            outp.Add((line.Substring(i, next - i), current));
            i = next;
        }
        return outp;
    }

    const float ChartW = 760f, ChartH = 820f, DetailW = 900f;

    /// One state for the whole picture. The markers used to be drawn from a state built inside
    /// the marker loop and the panel from another built below it - so the chart showed sixteen
    /// worlds marked "sealed" under a header reading "17 of 17 worlds charted". The collection
    /// screen had the same fault in its footer. Two states in one screenshot is a render telling
    /// two different stories, and it is convincing either way round.
    static Eggverse.GameState Roster()
    {
        // A real mid-run save rather than a finished one: thirteen worlds charted, the outer
        // sectors still dark. The header counts off this same state, so the picture and the
        // number agree by construction instead of by coincidence.
        var st = new Eggverse.GameState();
        // A lead a few levels past Glacierim's band, so the readiness line has something to say.
        st.Party[0].GainXp(4200, null);
        int i = 0;
        foreach (var w in Eggverse.PlanetDatabase.All)
            if (i++ < 13 || w.Id == Selected) st.Visited.Add(w.Id);
        st.Landmarks.Add(Selected);
        return st;
    }

    static Eggverse.StoryState Chapters()
    {
        var story = new Eggverse.StoryState();
        story.RestoreFrom(new string[0], Eggverse.StoryDatabase.Beats.Length - 1);
        return story;
    }

    public static Col[] Render()
    {
        var mstate = Roster();
        var mstory = Chapters();
        var c = new Battle.Ctx { Px = new Col[Battle.W * Battle.H] };
        for (int i = 0; i < c.Px.Length; i++) c.Px[i] = Col.Hex(0x060711);

        // panels
        float cx0 = 70, cx1 = cx0 + ChartW, cy0 = 110, cy1 = cy0 + ChartH;
        float dx1 = 1850, dx0 = dx1 - DetailW;
        Battle.Rect(c, cx0, cy0, cx1, cy1, Col.Hex(0x0B0D1C));
        Battle.Rect(c, dx0, cy0, dx1, cy1, Col.Hex(0x121426));

        Battle.Text(c, Eggverse.UiCopy.ChartTitle, 70, 1034, 34, Battle.Accent);

        // Straight from the game: same database, same projection, same label placement.
        float scale = GalaxyMapView.ChartScale();
        float ccx = (cx0 + cx1) * 0.5f, ccy = (cy0 + cy1) * 0.5f;

        // The game's own captions. These were typed out, and two of the three had lost their
        // "The" - "HATCHERY REACH" and "SHATTERED BELT" against the game's "THE HATCHERY REACH"
        // and "THE SHATTERED BELT", while "THE LONG DRIFT" kept it. Inconsistent with the game
        // and with itself, which is what typing them out gets you.
        var sectors = new[] { Sector.HatcheryReach, Sector.LongDrift, Sector.ShatteredBelt };
        for (int si = 0; si < sectors.Length; si++)
        {
            float top = float.NegativeInfinity;
            foreach (var p2 in PlanetDatabase.All)
                if (p2.Sector == sectors[si])
                    top = Math.Max(top, GalaxyMapView.WorldToChart(p2.SpacePosition).y + p2.SpaceRadius * scale);
            string caption = PlanetDatabase.SectorName(sectors[si]).ToUpperInvariant();
            float ly = Math.Min(top + 34f, 820f * 0.5f - 20f);
            float lx = GalaxyMapView.PlaceSectorLabel(
                GalaxyMapView.WorldToChart(PlanetDatabase.SectorCentre(sectors[si])).x, ly, caption, scale);
            Battle.TextCentre(c, caption, ccx + lx, ccy + ly, 19, new Col(0.62f, 0.66f, 0.82f, 0.55f));
        }

        int visited = 0;
        foreach (var def in PlanetDatabase.All)
        {
            var cp = GalaxyMapView.WorldToChart(def.SpacePosition);
            float px = ccx + cp.x, py = ccy + cp.y;
            float dot = Math.Min(62f, Math.Max(18f, def.SpaceRadius * 2f * scale));
            // From the roster, not from an arithmetic pattern. This read
            // "(visited++ % 3) != 2" - every third world unvisited, arbitrarily - under a header
            // counting charted worlds off a fully-visited state and beside a panel built from a
            // third one. Three states in one screenshot, each of them convincing.
            bool seen = mstate.Visited.Contains(def.Id);
            var land = new Col(def.Land.r, def.Land.g, def.Land.b, 1f);
            var atmo = new Col(def.Atmosphere.r, def.Atmosphere.g, def.Atmosphere.b, 0.30f);
            if (seen)
            {
                // The halo goes behind the world, which is where the view puts it in sibling
                // order. Drawn on top it turned Glacierim's ice-blue brown - a selection marker
                // that hides the thing it is selecting.
                if (def.Id == Selected)
                    Battle.Disc(c, px, py, dot * 1.25f, new Col(1f, 0.76f, 0.30f, 0.55f), 0.85f);
                Battle.Disc(c, px, py, dot * 0.90f, atmo, 1.3f);
                Battle.Disc(c, px, py, dot * 0.52f, land, 0.08f);

            // The first egg's seam. The chart is where a player looks at Amaranth for the whole
            // game, and it was one more coloured dot on it.
            if (def.IsBossWorld)
                for (int k = -20; k <= 20; k++)
                {
                    float nx = k / 20f;
                    float along = Math.Max(0f, 1f - Math.Abs(nx) * 0.9f);
                    if (along <= 0f) continue;
                    float wander = 0.06f * (float)Math.Sin(nx * 6.1f) + 0.03f * (float)Math.Sin(nx * 13.7f);
                    Battle.Disc(c, px + nx * dot * 0.26f, py + (nx * 0.42f + wander) * dot * 0.26f,
                                dot * (0.030f + 0.030f * along), new Col(0.05f, 0.03f, 0.08f, 0.9f), 0.5f);
                }
            }
            else
                for (int a = 0; a < 720; a++)
                {
                    double t = a * Math.PI / 360.0;
                    Battle.Rect(c, px + (float)Math.Cos(t)*dot*0.52f, py + (float)Math.Sin(t)*dot*0.52f,
                                   px + (float)Math.Cos(t)*dot*0.52f + 2, py + (float)Math.Sin(t)*dot*0.52f + 2,
                                   new Col(0.50f, 0.54f, 0.68f, 0.8f));
                }
            // Name and suffix from GalaxyMapView.MarkerLabel, which is what the chart draws.
            // This laid the two lines out itself - its own colour for the name, its own
            // "Lv a-b ABBR", its own ring - so when the selected world grew a caret the render
            // could not have shown it, and the halo behind the selected dot had never been drawn
            // here at all. Two ways of missing the same thing.
            {
                bool picked = def.Id == Selected;

                string suffix = seen
                    ? Eggverse.GalaxyMapView.MarkerSuffix(def, mstate, mstory,
                          Eggverse.GalaxyMapView.Presence.Elsewhere)
                    : "";
                string label = Eggverse.GalaxyMapView.MarkerLabel(def, suffix, seen, picked);

                var rows = label.Replace("<size=15>", "").Replace("</size>", "").Split('\n');
                for (int li = 0; li < rows.Length; li++)
                {
                    int size = li == 0 ? 18 : 15;
                    var runs = Runs(rows[li], seen ? Battle.Ink : Battle.InkDim);
                    float wide = 0f;
                    foreach (var r in runs) wide += Battle.TextWidth(r.text, size);
                    float pen = px - wide * 0.5f;
                    float ly = py - dot * 0.66f - 6 - li * 20f;
                    foreach (var r in runs)
                    {
                        if (r.text.Trim().Length > 0)
                            Battle.Text(c, r.text.ToUpperInvariant(), pen, ly, size, r.col);
                        pen += Battle.TextWidth(r.text, size);
                    }
                }
            }
        }

        // Detail panel: the game's own text, not a plausible-looking imitation of it.
        //
        // This panel used to be ten invented lines - "RECORDED HERE", "NEST STATION  YES",
        // "TRAVEL  1.5S FROM BRINEHOLT" - none of which the game has ever produced. It looked
        // entirely convincing, which is exactly the problem: the render was reassuring me about
        // a screen that did not exist.
        {
            var def = Eggverse.PlanetDatabase.Get(Selected);
            var state = mstate;
            var story = mstory;
            foreach (var sp in Eggverse.SpeciesDatabase.All) state.Seen.Add(sp.Id);
            state.Caught.Add("snowpoach");
            state.Landmarks.Add("glacierim");

            Battle.Text(c, def.Name.ToUpperInvariant(), dx0 + 26, cy1 - 26, 30, Battle.Ink);

            // The header tally, right-aligned against the same edge the detail panel ends on.
            string tally = Strip(Eggverse.GalaxyMapView.Tally(state));
            Battle.Text(c, tally, 1850 - Battle.TextWidth(tally, 22), 1080 - 46 - 11, 22, Battle.InkDim);

            string text = Eggverse.GalaxyMapView.DetailBody(
                def, state, story, Eggverse.PlanetDatabase.Get("brineholt"),
                Eggverse.GalaxyMapView.Presence.Elsewhere);

            int row = 0;
            foreach (var raw in text.Split('\n'))
            {
                foreach (var line in Wrap(Strip(raw), 848f, 22))
                {
                    Battle.Text(c, line, dx0 + 26, cy1 - 112 - row * 26, 22, Battle.InkDim);
                    row++;
                }
            }

            // The course footer, pinned to the panel floor above its rule.
            Battle.Rect(c, dx0 + 26, cy0 + 86, dx0 + 26 + 848, cy0 + 88, new Col(0.17f, 0.20f, 0.31f, 1f));
            Battle.TextCentre(c, Strip(Eggverse.GalaxyMapView.CourseLine(
                                  def, state, story, Eggverse.GalaxyMapView.Presence.Elsewhere)),
                              dx0 + 26 + 424, cy0 + 48, 22, Battle.Ink);
        }

        Battle.TextCentre(c, Eggverse.UiCopy.ChartFooter.ToUpperInvariant(), 960, 64, 20, Battle.InkDim);
        return c.Px;
    }

    static string Strip(string t)
    {
        return System.Text.RegularExpressions.Regex.Replace(t, "<[^>]+>", "");
    }

    /// The same greedy wrap uGUI does, measured with the harness's own font model.
    static System.Collections.Generic.List<string> Wrap(string t, float width, int font)
    {
        var outp = new System.Collections.Generic.List<string>();
        if (t.Length == 0) { outp.Add(""); return outp; }
        var sb = new System.Text.StringBuilder();
        foreach (var word in t.Split(' '))
        {
            string trial = sb.Length == 0 ? word : sb + " " + word;
            if (Battle.TextWidth(trial, font) > width && sb.Length > 0)
            {
                outp.Add(sb.ToString());
                sb.Clear();
                sb.Append(word);
            }
            else { sb.Clear(); sb.Append(trial); }
        }
        outp.Add(sb.ToString());
        return outp;
    }
}
