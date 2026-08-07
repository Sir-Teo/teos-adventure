using System;
using Eggverse;
using UnityEngine;

/// The navigation chart overlay, at canvas reference resolution. This is the screen the
/// player reads to decide where to go next, so it is worth looking at rather than assuming.
static class Map
{
    const float ChartW = 760f, ChartH = 820f, DetailW = 900f;

    public static Col[] Render()
    {
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
            bool seen = (visited++ % 3) != 2;          // a plausible mid-run save
            var land = new Col(def.Land.r, def.Land.g, def.Land.b, 1f);
            var atmo = new Col(def.Atmosphere.r, def.Atmosphere.g, def.Atmosphere.b, 0.30f);
            if (seen)
            {
                Battle.Disc(c, px, py, dot * 0.90f, atmo, 1.3f);
                Battle.Disc(c, px, py, dot * 0.52f, land, 0.08f);
            }
            else
                for (int a = 0; a < 720; a++)
                {
                    double t = a * Math.PI / 360.0;
                    Battle.Rect(c, px + (float)Math.Cos(t)*dot*0.52f, py + (float)Math.Sin(t)*dot*0.52f,
                                   px + (float)Math.Cos(t)*dot*0.52f + 2, py + (float)Math.Sin(t)*dot*0.52f + 2,
                                   new Col(0.50f, 0.54f, 0.68f, 0.8f));
                }
            Battle.TextCentre(c, def.Name, px, py - dot * 0.66f - 6, 18,
                              seen ? Battle.Ink : Battle.InkDim);

            // The suffix line under each name, at the size the chart draws it.
            if (seen)
            {
                var owing = new Eggverse.GameState();
                int owed = 0;
                foreach (var sp in def.Spawns)
                    if (Eggverse.SpeciesDatabase.Get(sp.SpeciesId).CatchRate >=
                            Eggverse.SpeciesDatabase.CatchableThreshold &&
                        !owing.Caught.Contains(sp.SpeciesId)) owed++;

                // The level in dim ink and the ring in accent, as the chart draws them. Drawing
                // the whole suffix in one colour made the mark look like part of the number.
                string lv = "Lv " + def.MinLevel + "-" + def.MaxLevel;
                string ring = owed > 0 ? "  \u25cb" : "";
                float wholeW = Battle.TextWidth(lv + ring, 15);
                float left = px - wholeW * 0.5f;
                float subY = py - dot * 0.66f - 26;
                Battle.Text(c, lv, left, subY, 15, Battle.InkDim);
                if (ring.Length > 0)
                    Battle.Text(c, ring, left + Battle.TextWidth(lv, 15), subY, 15, Battle.Accent);
            }
        }

        // Detail panel: the game's own text, not a plausible-looking imitation of it.
        //
        // This panel used to be ten invented lines - "RECORDED HERE", "NEST STATION  YES",
        // "TRAVEL  1.5S FROM BRINEHOLT" - none of which the game has ever produced. It looked
        // entirely convincing, which is exactly the problem: the render was reassuring me about
        // a screen that did not exist.
        {
            var def = Eggverse.PlanetDatabase.Get("glacierim");
            var state = new Eggverse.GameState();
            var story = new Eggverse.StoryState();
            story.RestoreFrom(new string[0], Eggverse.StoryDatabase.Beats.Length - 1);
            foreach (var w in Eggverse.PlanetDatabase.All) state.Visited.Add(w.Id);
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
