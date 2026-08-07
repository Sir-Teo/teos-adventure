using System;
using System.Collections.Generic;

/// I cannot render a frame, but uGUI anchor maths is arithmetic — so the panel rectangles
/// can be reproduced here and checked for overflow and unintended overlap.
/// Numbers mirror the UIKit.Place calls; if those move, these should be updated with them.
static class Layout
{
    const float W = 1920f, H = 1080f;   // CanvasScaler reference resolution

    struct Box
    {
        public string Name;
        public float X0, Y0, X1, Y1;
        public bool Modal;              // overlays are allowed to cover things
        public override string ToString() => $"{Name} [{X0:0}..{X1:0}, {Y0:0}..{Y1:0}]";
    }

    static Box Place(string name, float ax, float ay, float px, float py,
                     float posX, float posY, float w, float h, bool modal = false)
    {
        // anchor point -> pivot-relative rect, exactly as RectTransform resolves it
        float cx = ax * W + posX;
        float cy = ay * H + posY;
        float x0 = cx - px * w;
        float y0 = cy - py * h;
        return new Box { Name = name, X0 = x0, Y0 = y0, X1 = x0 + w, Y1 = y0 + h, Modal = modal };
    }

    static bool Overlaps(Box a, Box b) =>
        a.X0 < b.X1 && b.X0 < a.X1 && a.Y0 < b.Y1 && b.Y0 < a.Y1;

    static void CheckScreen(Action<bool,string> check, string screen, List<Box> boxes)
    {
        foreach (var b in boxes)
        {
            check(b.X0 >= -1f && b.X1 <= W + 1f, $"{screen}: {b.Name} fits horizontally ({b.X0:0}..{b.X1:0})");
            check(b.Y0 >= -1f && b.Y1 <= H + 1f, $"{screen}: {b.Name} fits vertically ({b.Y0:0}..{b.Y1:0})");
        }
        for (int i = 0; i < boxes.Count; i++)
            for (int j = i + 1; j < boxes.Count; j++)
            {
                if (boxes[i].Modal || boxes[j].Modal) continue;
                check(!Overlaps(boxes[i], boxes[j]),
                      $"{screen}: {boxes[i].Name} and {boxes[j].Name} do not overlap — {boxes[i]} vs {boxes[j]}");
            }
    }

    public static void Run(Action<bool,string> check)
    {
        // ---- battle screen ----
        var battle = new List<Box>
        {
            Place("FoeEgg",      1f, 1f, 0.5f, 0.5f, -430f, -330f, 300f, 300f),
            Place("FoeCard",     0f, 1f, 0f,   1f,     70f,  -70f, 660f, 150f),
            Place("MyEgg",       0f, 0f, 0.5f, 0.5f,  440f,  545f, 360f, 360f),
            Place("MyCard",      1f, 0.5f, 1f, 0.5f,  -70f, -110f, 700f, 210f),
            Place("MessageBox",  0f, 0f, 0f,   0f,     40f,   40f, 1080f, 210f),
            Place("ActionMenu",  1f, 0f, 1f,   0f,    -40f,   40f, 740f, 260f),   // 2x3 since SALVE
            Place("PartyMenu",   0.5f, 0.5f, 0.5f, 0.5f, 0f,   0f, 900f, 660f, modal: true),
        };
        CheckScreen(check, "battle", battle);

        // ---- explore HUD ----
        // This screen was never listed here, which is exactly how the toast bar came to sit on
        // top of the objective panel: the checker can only catch what it is told about.
        var hud = new List<Box>
        {
            Place("PartyStrip",   0f,   1f, 0f,   1f,   28f, -28f,  360f, 380f),
            Place("Objective",    1f,   1f, 1f,   1f,  -28f, -28f,  560f, 268f),
            Place("Toast",        0.5f, 1f, 0.5f, 1f,    0f, -308f, 1000f, 76f),
            Place("Prompt",       0.5f, 0f, 0.5f, 0f,    0f,  40f, 1200f,  60f),
            Place("Autosave",     1f,   0f, 1f,   0f,  -28f,  30f,  240f,  26f),
            Place("Collection",   0.5f, 0.5f, 0.5f, 0.5f, 0f,  0f, 1760f, 940f, modal: true),
        };
        CheckScreen(check, "hud", hud);

        // ---- collection screen: the panel, and its columns inside it ----
        var collection = new List<Box>
        {
            Place("CollectionPanel", 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 1760f, 940f),
        };
        CheckScreen(check, "collection", collection);

        // Columns are positioned inside the panel, so check them in panel space.
        const float PW = 1760f, PH = 940f;
        var inner = new (string name, float x, float w, float yTop, float h)[]
        {
            ("Nest",    34f,  720f,  84f, 780f),
            ("Divider1",772f,   2f,  82f, 786f),
            ("Dex",     800f, 420f,  84f, 780f),
            ("Divider2",1236f,  2f,  82f, 786f),
            ("Portrait",1280f,150f,  92f, 150f),
            ("Detail",  1450f,280f,  92f, 160f),
            ("Lore",    1280f,450f, 262f, 600f),
        };
        foreach (var c in inner)
        {
            check(c.x >= 0f && c.x + c.w <= PW,
                  $"collection: {c.name} fits the panel width ({c.x:0}..{c.x + c.w:0} of {PW:0})");
            check(c.yTop + c.h <= PH - 40f,
                  $"collection: {c.name} clears the footer ({c.yTop + c.h:0} of {PH - 40f:0})");
        }
        // The three columns must not run into each other.
        check(34f + 720f < 772f, "collection: nest column clears the first divider");
        check(800f + 420f < 1236f, "collection: dex column clears the second divider");
        check(1280f + 450f <= PW - 20f, "collection: detail column clears the panel edge");

        // ---- children inside a panel ----
        // Same anchor maths, but resolved against the panel's own size rather than the screen.
        // Three overlays were only ever checked as whole boxes on the screen; nothing looked
        // inside them, which is how the title card's heading came to sit on its subtitle.
        List<Box> Inside(float panelW, float panelH, params (string name, float ax, float ay,
                                                             float px, float py, float x, float y,
                                                             float w, float h)[] parts)
        {
            var made = new List<Box>();
            foreach (var p2 in parts)
            {
                float cx2 = p2.ax * panelW + p2.x;
                float cy2 = p2.ay * panelH + p2.y;
                made.Add(new Box
                {
                    Name = p2.name,
                    X0 = cx2 - p2.px * p2.w, Y0 = cy2 - p2.py * p2.h,
                    X1 = cx2 - p2.px * p2.w + p2.w, Y1 = cy2 - p2.py * p2.h + p2.h,
                });
            }
            return made;
        }

        // ---- dialogue box internals: 1660 x 300 ----
        CheckScreen(check, "dialogue", Inside(1660f, 300f,
            ("Portrait", 0f, 1f, 0f, 1f,  26f, -26f, 196f, 196f),
            ("Speaker",  0f, 1f, 0f, 1f, 246f, -26f, 900f,  38f),
            ("Body",     0f, 1f, 0f, 1f, 246f, -74f, 1380f, 180f),
            ("Hint",     1f, 0f, 1f, 0f, -26f,  16f, 500f,  26f)));

        // ---- the ending card, drawn straight onto the screen ----
        CheckScreen(check, "victory", Inside(1920f, 1080f,
            ("Heading", 0.5f, 0.5f, 0.5f, 0.5f, 0f,  156f, 1400f, 110f),
            ("Body",    0.5f, 0.5f, 0.5f, 0.5f, 0f,  -60f, 1300f, 300f),
            ("Hint",    0.5f, 0f,   0.5f, 0f,   0f,   80f,  800f,  34f)));

        // ---- name entry: 900 x 420 ----
        CheckScreen(check, "name entry", Inside(900f, 420f,
            ("Portrait", 0.5f, 1f,   0.5f, 1f,   0f,  -30f, 130f, 130f),
            ("Title",    0.5f, 1f,   0.5f, 1f,   0f, -172f, 820f,  40f),
            ("Field",    0.5f, 0.5f, 0.5f, 0.5f, 0f,  -60f, 680f,  76f),
            ("Hint",     0.5f, 0f,   0.5f, 0f,   0f,   34f, 840f,  28f)));

        // ---- title card ----
        // Never listed here: only the text-fit checks covered it, which measure whether a string
        // fits its box, not whether the boxes fit each other. The heading and subtitle were
        // overlapping by 5px, hidden because both are vertically centred inside their boxes.
        var titleCard = new List<Box>
        {
            Place("Heading",   0.5f, 0.5f, 0.5f, 0.5f,  0f,  250f, 1200f, 120f),
            Place("Subtitle",  0.5f, 0.5f, 0.5f, 0.5f,  0f,  158f, 1200f,  50f),
            Place("TitleBody", 0.5f, 0.5f, 0.5f, 0.5f,  0f, -110f, 1500f, 460f),
            Place("BeginHint", 0.5f, 0f,   0.5f, 0f,    0f,   96f, 1300f,  40f),
            Place("SaveLine",  0.5f, 0f,   0.5f, 0f,    0f,   62f, 1300f,  28f),
        };
        CheckScreen(check, "title", titleCard);

        // ---- pause and dialogue, both centred modals ----
        var modals = new List<Box>
        {
            Place("PauseBox",    0.5f, 0.5f, 0.5f, 0.5f, 0f,  0f, 760f, 620f),
            Place("DialogueBox", 0.5f, 0f,   0.5f, 0f,   0f, 46f, 1660f, 300f),
            Place("NameBox",     0.5f, 0.5f, 0.5f, 0.5f, 0f,  0f, 900f, 420f),
            Place("MapChart",    0f,   0.5f, 0f,   0.5f, 70f, -20f, 760f, 820f),
            Place("MapDetail",   1f,   0.5f, 1f,   0.5f, -70f, -20f, 900f, 820f),
        };
        foreach (var b in modals)
        {
            check(b.X0 >= -1f && b.X1 <= W + 1f, $"overlay: {b.Name} fits horizontally ({b.X0:0}..{b.X1:0})");
            check(b.Y0 >= -1f && b.Y1 <= H + 1f, $"overlay: {b.Name} fits vertically ({b.Y0:0}..{b.Y1:0})");
        }
        // The map's two panels sit side by side and must not collide.
        check(!Overlaps(modals[3], modals[4]), "map: chart and detail panel do not overlap");

        // ---- navigation chart: every dot and every name inside the chart panel ----
        // This calls the game's own projection rather than a copy of it, so the check moves
        // with the real thing. The chart is masked now, so anything outside is silently cut
        // off rather than drawn over the detail panel — which is worse, not better.
        {
            const float CW = 760f, CH = 820f, HW = CW / 2f, HH = CH / 2f;
            float scale = Eggverse.GalaxyMapView.ChartScale();
            foreach (var p in Eggverse.PlanetDatabase.All)
            {
                var c2 = Eggverse.GalaxyMapView.WorldToChart(p.SpacePosition);
                float dot = Math.Min(62f, Math.Max(18f, p.SpaceRadius * 2f * scale));

                check(Math.Abs(c2.x) + dot * 0.5f <= HW,
                      $"chart: {p.Name}'s dot fits horizontally ({Math.Abs(c2.x) + dot * 0.5f:0} of {HW:0})");
                check(Math.Abs(c2.y) + dot * 0.5f <= HH,
                      $"chart: {p.Name}'s dot fits vertically ({Math.Abs(c2.y) + dot * 0.5f:0} of {HH:0})");

                // The name sits below the dot, centred, at font 18.
                float halfText = TextFit.LinesNeeded(p.Name, 1e6f, 18) * 0f + p.Name.Length * 18f * 0.52f / 2f;
                check(Math.Abs(c2.x) + halfText <= HW,
                      $"chart: \"{p.Name}\" label fits horizontally ({Math.Abs(c2.x) + halfText:0} of {HW:0})");
                float textBottom = c2.y - dot * 0.66f - 4f - 21f;   // one 18px line inside a 44px box
                check(textBottom >= -HH,
                      $"chart: \"{p.Name}\" label clears the chart floor ({textBottom:0} of {-HH:0})");
            }

            // Amaranth must read as beyond the Belt, not inside it.
            var belt = Eggverse.GalaxyMapView.WorldToChart(
                Eggverse.PlanetDatabase.SectorCentre(Eggverse.Sector.ShatteredBelt));
            var amaranth = Eggverse.GalaxyMapView.WorldToChart(
                Eggverse.PlanetDatabase.Get("amaranth").SpacePosition);
            check(amaranth.y > belt.y, "chart: Amaranth sits beyond the Shattered Belt, not among it");

            // Every sector caption must find a spot clear of all worlds, not fall through to
            // the clamp. "THE LONG DRIFT" at the bare centroid sat straight on top of Umbralux.
            var sectors = new[] { Eggverse.Sector.HatcheryReach, Eggverse.Sector.LongDrift, Eggverse.Sector.ShatteredBelt };
            foreach (var sector in sectors)
            {
                float top = float.NegativeInfinity;
                foreach (var p2 in Eggverse.PlanetDatabase.All)
                    if (p2.Sector == sector)
                        top = Math.Max(top, Eggverse.GalaxyMapView.WorldToChart(p2.SpacePosition).y + p2.SpaceRadius * scale);

                string caption = Eggverse.PlanetDatabase.SectorName(sector).ToUpperInvariant();
                float y2 = Math.Min(top + 34f, CH / 2f - 20f);
                float x2 = Eggverse.GalaxyMapView.PlaceSectorLabel(
                    Eggverse.GalaxyMapView.WorldToChart(Eggverse.PlanetDatabase.SectorCentre(sector)).x,
                    y2, caption, scale);
                float half = Eggverse.GalaxyMapView.CaptionHalfWidth(caption, 19);

                check(Eggverse.GalaxyMapView.ClearOfWorlds(x2, y2, half, Eggverse.PlanetDatabase.All, scale),
                      $"chart: \"{caption}\" finds a spot clear of every world");
                check(Math.Abs(x2) + half <= HW,
                      $"chart: \"{caption}\" stays inside the chart ({Math.Abs(x2) + half:0} of {HW:0})");
                float centroidX = Eggverse.GalaxyMapView.WorldToChart(Eggverse.PlanetDatabase.SectorCentre(sector)).x;
                Console.WriteLine($"  {caption,-16} centroid x {centroidX,6:0} -> placed at {x2,6:0}"
                                  + (Math.Abs(x2 - centroidX) > 1f ? $"  (nudged {Math.Abs(x2 - centroidX):0}px clear)" : "  (centroid was clear)"));
            }
        }

        // Positive controls for the geometry maths itself.
        var offscreen = Place("control-offscreen", 1f, 1f, 0f, 0f, 100f, 100f, 400f, 400f);
        check(offscreen.X1 > W && offscreen.Y1 > H, "control: the checker sees a box pushed off screen");
        var ctlA = Place("control-a", 0f, 0f, 0f, 0f, 0f, 0f, 100f, 100f);
        var ctlB = Place("control-b", 0f, 0f, 0f, 0f, 50f, 50f, 100f, 100f);
        check(Overlaps(ctlA, ctlB), "control: the checker sees two boxes overlapping");
        var ctlC = Place("control-c", 0f, 0f, 0f, 0f, 200f, 200f, 100f, 100f);
        check(!Overlaps(ctlA, ctlC), "control: the checker sees two boxes that do not overlap");
        // Pivot handling: a centred pivot must place the box around the anchor.
        var centred = Place("control-centre", 0.5f, 0.5f, 0.5f, 0.5f, 0f, 0f, 200f, 200f);
        check(Math.Abs(centred.X0 - (W / 2f - 100f)) < 0.01f, "control: centre pivot resolves correctly");

        Console.WriteLine($"  {battle.Count} battle elements, {hud.Count} HUD panels, {titleCard.Count} title elements, {inner.Length} collection columns, {modals.Count} overlays checked");
    }
}
