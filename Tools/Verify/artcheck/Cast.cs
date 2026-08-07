using System;
using System.Collections.Generic;
using Eggverse;

/// Every face in the game, at the size a player sees it in the dialogue box.
///
/// ProcArt.Portrait took a name, used it to key the cache, and drew the same hooded silhouette
/// for everybody. Nothing in eleven thousand assertions could have said so — they all passed —
/// and nobody had ever drawn two of them side by side.
///
/// The shapes here are ProcArt's own, listed once in the order it fills them and read by both.
/// The tints are the roster's.
static class Cast
{
    const int Tile = 196;          // the real portrait frame, inner size
    const int Pad = 22, Cols = 6;

    public static (Col[] px, int w, int h) Render()
    {
        var people = People();
        int rows = (people.Count + Cols - 1) / Cols;
        int cw = Tile + Pad * 2, ch = Tile + Pad * 2 + 26;

        // A full screen, with the grid centred in it. Battle.Rect and Battle.TextCentre clip to
        // the battle screen's own dimensions, so a sheet of any other size draws off the end of
        // its own array - which is exactly what the first version did.
        int w = Battle.W, h = Battle.H;
        int gx = (w - cw * Cols) / 2, gy = (h - ch * rows) / 2;

        var c = new Battle.Ctx { Px = new Col[w * h] };
        for (int i = 0; i < c.Px.Length; i++) c.Px[i] = Col.Hex(0x0A0C16);

        for (int i = 0; i < people.Count; i++)
        {
            // Rows come out bottom-up once the frame is flipped for writing, so the sheet read
            // with Teo and Amy last. Reversed here, not in People(), which the checks also use.
            int row = rows - 1 - i / Cols;
            int x0 = gx + (i % Cols) * cw + Pad, y0 = gy + row * ch + Pad;
            var face = Portrait(people[i].name, people[i].tint, Tile);

            // The frame the dialogue box puts around it, tinted the way the view tints it.
            // Drawn by hand: Battle.Rect clips to the battle screen's own dimensions, and this
            // sheet is its own size. It threw, and run.sh swallowed the throw whole.
            Col frame = Col.Lerp(Col.Hex(0x232A44), people[i].tint, 0.35f);
            for (int y = y0 - 5; y < y0 + Tile + 5; y++)
                for (int x = x0 - 5; x < x0 + Tile + 5; x++)
                    if (x >= 0 && x < w && y >= 0 && y < h) c.Px[y * w + x] = frame;

            for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                {
                    var p = face[y * Tile + x];   // both buffers are bottom-up; no flip here
                    if (p.a <= 0f) continue;
                    int px = x0 + x, py = y0 + y;
                    if (px < 0 || px >= w || py < 0 || py >= h) continue;
                    c.Px[py * w + px] = Col.Lerp(c.Px[py * w + px], p, p.a);
                }

            // The label sits below the frame on screen, which is the smaller y of the two here.
            Battle.TextCentre(c, people[i].name, x0 + Tile / 2f, y0 - 14f, 20, Battle.Ink);
        }
        return (c.Px, w, h);
    }

    /// PortraitForm.Shapes, exactly as ProcArt fills them. Neither this nor ProcArt has any
    /// shapes of its own any more: there is one list and both walk it.
    static Col[] Portrait(string key, Col tint, int size)
    {
        var px = new Col[size * size];
        var eyes = Col.Hex(0x0F0F1A);
        foreach (var b in PortraitForm.Shapes(PortraitForm.For(key)))
        {
            Col col = b.Eyes ? eyes : b.Glint ? new Col(1f, 1f, 1f, 1f) : tint.Mul(b.Shade);
            Ell(px, size, b.Cx, b.Cy, b.Rx, b.Ry, new Col(col.r, col.g, col.b, b.Alpha), b.Soft);
        }
        return px;
    }

    /// ProcArt.FillEllipse, to the letter. The first version flipped ny "to correct for" the
    /// BMP writer, which already flips - so the whole cast rendered upside down: shoulders above
    /// the head, crests hanging under the chin, brows sitting on cheekbones. It looked plausible
    /// enough at a glance that the first fix went into the marks instead of the transform.
    public static void EllPublic(Col[] px, int size, float cx, float cy, float rx, float ry, Col col, float soft)
        => Ell(px, size, cx, cy, rx, ry, col, soft);

    static void Ell(Col[] px, int size, float cx, float cy, float rx, float ry, Col col, float soft)
    {
        for (int y = 0; y < size; y++)
        {
            float ny = (y + 0.5f) / size * 2f - 1f;
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float dx = (nx - cx) / rx, dy = (ny - cy) / ry;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d > 1f) continue;
                float a = soft <= 0f ? 1f : M.Clamp01((1f - d) / soft);
                a *= col.a;
                var under = px[y * size + x];
                px[y * size + x] = new Col(
                    under.r + (col.r - under.r) * a,
                    under.g + (col.g - under.g) * a,
                    under.b + (col.b - under.b) * a,
                    under.a + (1f - under.a) * a);
            }
        }
    }

    public static List<(string name, Col tint)> People()
    {
        var outp = new List<(string, Col)>
        {
            ("Teo", Col.Hex(0xECF1F7)),
            ("Amy", Col.Hex(0xFFC24D)),
        };
        foreach (var npc in StoryDatabase.Npcs)
        {
            bool seen = false;
            foreach (var p in outp) if (p.Item1 == npc.Name) seen = true;
            if (!seen)
                outp.Add((npc.Name, new Col(npc.Tint.r, npc.Tint.g, npc.Tint.b, 1f)));
        }
        return outp;
    }

    /// The two faces that are hardest to tell apart, and the mark each person wears.
    public static string Report()
    {
        var people = People();
        float closest = 999f; string a = "", b = "";
        for (int i = 0; i < people.Count; i++)
            for (int j = i + 1; j < people.Count; j++)
            {
                float d = PortraitForm.Distance(PortraitForm.For(people[i].name),
                                                PortraitForm.For(people[j].name));
                if (d < closest) { closest = d; a = people[i].name; b = people[j].name; }
            }

        var tally = new Dictionary<PortraitMark, int>();
        foreach (var p in people)
        {
            var m = PortraitForm.For(p.name).Mark;
            tally[m] = tally.ContainsKey(m) ? tally[m] + 1 : 1;
        }
        var parts = new List<string>();
        foreach (PortraitMark m in Enum.GetValues(typeof(PortraitMark)))
            parts.Add($"{m} {(tally.ContainsKey(m) ? tally[m] : 0)}");

        return $"  cast: {people.Count} faces, closest pair {a}/{b} at {closest:0.000} " +
               $"(floor {PortraitForm.MinDistance:0.00})\n  marks: {string.Join("  ", parts)}";
    }
}
