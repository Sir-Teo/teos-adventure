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

        Battle.Text(c, "NAVIGATION CHART", 70, 1034, 34, Battle.Accent);

        // Straight from the game: same database, same projection, same label placement.
        float scale = GalaxyMapView.ChartScale();
        float ccx = (cx0 + cx1) * 0.5f, ccy = (cy0 + cy1) * 0.5f;

        string[] sectorNames = { "HATCHERY REACH", "THE LONG DRIFT", "SHATTERED BELT" };
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
        }

        // detail panel content
        Battle.Text(c, "GLACIERIM", dx0 + 26, cy1 - 26, 30, Battle.Ink);
        string[] body =
        {
            "THE LONG DRIFT  ·  LEVELS 16-18  ·  FROST",
            "",
            "A shelf world. The ice sings when you walk on it,",
            "which Pim insists is only the shell fields settling.",
            "",
            "RECORDED HERE   SNOWPOACH, GLACEGG, CHILLET",
            "STILL MISSING   FROSTMALLOW",
            "",
            "NEST STATION    YES",
            "TRAVEL          1.5S FROM BRINEHOLT",
        };
        for (int i = 0; i < body.Length; i++)
            Battle.Text(c, body[i], dx0 + 26, cy1 - 112 - i * 34, 22,
                        i == 0 ? Battle.Accent : Battle.InkDim);

        Battle.TextCentre(c, "ARROWS SELECT  ·  ENTER TO SET COURSE  ·  M OR ESC TO CLOSE", 960, 64, 20, Battle.InkDim);
        return c.Px;
    }
}
