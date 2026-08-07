using System;
using Eggverse;

/// The six landmark silhouettes, side by side at the scale the player actually sees them, with
/// Teo in each cell for size.
///
/// They were one grey disc each. Nothing in the assertions could have told me that a bell, a
/// ship's bow and nine hundred cairns all rendered as the same rock — only looking could.
static class Landmarks
{
    const int Cell = 280;
    const float Scale = 560f / 30f;   // px per world unit, the surface camera's framing

    static void Dot(Col[] px, int w, float ox, float oy, float wx, float wy, float d, Col col, float soft = 0.14f)
    {
        float cx = ox + wx * Scale, cy = oy + wy * Scale, rad = d * 0.5f * Scale;
        for (int y = Math.Max(0, (int)(cy - rad - 2)); y < Math.Min(px.Length / w, (int)(cy + rad + 2)); y++)
            for (int x = Math.Max(0, (int)(cx - rad - 2)); x < Math.Min(w, (int)(cx + rad + 2)); x++)
            {
                float dd = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / Math.Max(0.001f, rad);
                float a = M.Clamp01((1f - dd) / soft);
                if (a > 0f) Program.OverPublic(px, y * w + x, col, a * col.a);
            }
    }

    static void Bar(Col[] px, int w, float ox, float oy, float wx, float wy, float bw, float bh, Col col)
    {
        float cx = ox + wx * Scale, cy = oy + wy * Scale;
        float hw = bw * 0.5f * Scale, hh = bh * 0.5f * Scale;
        for (int y = Math.Max(0, (int)(cy - hh)); y < Math.Min(px.Length / w, (int)(cy + hh) + 1); y++)
            for (int x = Math.Max(0, (int)(cx - hw)); x < Math.Min(w, (int)(cx + hw) + 1); x++)
                Program.OverPublic(px, y * w + x, col, col.a);
    }

    /// A bar turned about its own centre, for the one form that is lying at an angle.
    static void BarRot(Col[] px, int w, float ox, float oy, float wx, float wy,
                       float bw, float bh, float deg, Col col)
    {
        float cx = ox + wx * Scale, cy = oy + wy * Scale;
        float hw = bw * 0.5f * Scale, hh = bh * 0.5f * Scale;
        double rad = -deg * Math.PI / 180.0;
        float cs = (float)Math.Cos(rad), sn = (float)Math.Sin(rad);
        int reach = (int)(Math.Max(hw, hh) + 2);
        for (int y = Math.Max(0, (int)cy - reach); y < Math.Min(px.Length / w, (int)cy + reach); y++)
            for (int x = Math.Max(0, (int)cx - reach); x < Math.Min(w, (int)cx + reach); x++)
            {
                float dx = x - cx, dy = y - cy;
                float lx = dx * cs - dy * sn, ly = dx * sn + dy * cs;
                if (Math.Abs(lx) <= hw && Math.Abs(ly) <= hh)
                    Program.OverPublic(px, y * w + x, col, col.a);
            }
    }

    static void Ring(Col[] px, int w, float ox, float oy, float d, float thick, Col col)
    {
        float cx = ox, cy = oy, rad = d * 0.5f * Scale, t = thick * d * Scale;
        for (int y = Math.Max(0, (int)(cy - rad - 2)); y < Math.Min(px.Length / w, (int)(cy + rad + 2)); y++)
            for (int x = Math.Max(0, (int)(cx - rad - 2)); x < Math.Min(w, (int)(cx + rad + 2)); x++)
            {
                float r = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float a = M.Clamp01(1f - Math.Abs(r - rad + t * 0.5f) / Math.Max(1f, t * 0.5f));
                if (a > 0f) Program.OverPublic(px, y * w + x, col, a * col.a);
            }
    }

    /// Mirrors SurfaceMode.BuildLandmarkForm. The shapes are the mock's business; which form each
    /// world gets comes from the real database.
    static void Form(Col[] px, int w, float ox, float oy, LandmarkForm form, Col stone)
    {
        var dark = new Col(stone.r * 0.66f, stone.g * 0.66f, stone.b * 0.72f, 1f);
        var outline = new Col(0.07f, 0.08f, 0.13f, 0.85f);

        if (form != LandmarkForm.Seam)
            Dot(px, w, ox, oy, 0f, -1.0f, 3.4f, new Col(dark.r, dark.g, dark.b, 0.55f), 0.5f);

        switch (form)
        {
            case LandmarkForm.Post:
                Bar(px, w, ox, oy, 0f, 0.55f, 1.06f, 3.66f, outline);
                Bar(px, w, ox, oy, 0f, 0.55f, 0.82f, 3.4f, stone);
                Bar(px, w, ox, oy, 0f, 2.3f, 1.74f, 0.86f, outline);
                Bar(px, w, ox, oy, 0f, 2.3f, 1.5f, 0.62f, stone);
                Dot(px, w, ox, oy, 0f, 2.5f, 2.8f, new Col(1f, 0.92f, 0.7f, 0.26f), 0.9f);
                break;

            case LandmarkForm.Frame:
                Bar(px, w, ox, oy, -1.0f, 0.5f, 0.58f, 3.24f, outline);
                Bar(px, w, ox, oy, 1.0f, 0.5f, 0.58f, 3.24f, outline);
                Bar(px, w, ox, oy, 0f, 1.9f, 2.84f, 0.54f, outline);
                Bar(px, w, ox, oy, -1.0f, 0.5f, 0.34f, 3.0f, stone);
                Bar(px, w, ox, oy, 1.0f, 0.5f, 0.34f, 3.0f, stone);
                Bar(px, w, ox, oy, 0f, 1.9f, 2.6f, 0.3f, stone);
                Dot(px, w, ox, oy, 0f, 1.0f, 1.42f, outline, 0.16f);
                Dot(px, w, ox, oy, 0f, 1.0f, 1.15f, dark, 0.16f);
                break;

            case LandmarkForm.Stones:
                for (int i = 0; i < 5; i++)
                {
                    float t = (i - 2f) * 1.18f, h = 1.5f - Math.Abs(i - 2f) * 0.26f;
                    Dot(px, w, ox, oy, t, h * 0.34f, h * 1.22f, outline, 0.14f);
                    Dot(px, w, ox, oy, t, h * 0.34f, h, i == 2 ? stone : dark, 0.14f);
                }
                break;

            case LandmarkForm.Hollow:
                Ring(px, w, ox, oy, 3.5f, 0.26f, outline);
                Ring(px, w, ox, oy, 3.2f, 0.20f, stone);
                Dot(px, w, ox, oy, 0f, 0f, 2.1f, new Col(0.04f, 0.05f, 0.09f, 0.92f), 0.06f);
                break;

            case LandmarkForm.Hulk:
                Dot(px, w, ox, oy, 0f, 0.5f, 4.0f, outline, 0.16f);
                Dot(px, w, ox, oy, 0f, 0.5f, 3.6f, dark, 0.16f);
                BarRot(px, w, ox, oy, 0.75f, 1.85f, 3.5f, 0.78f, 26f, outline);
                BarRot(px, w, ox, oy, 0.75f, 1.85f, 3.2f, 0.5f, 26f, stone);
                break;

            case LandmarkForm.Seam:
                Bar(px, w, ox, oy, 0f, 0f, 7.0f, 0.26f, new Col(0.05f, 0.05f, 0.09f, 0.9f));
                Bar(px, w, ox, oy, 0f, 0.02f, 6.6f, 0.13f, new Col(stone.r, stone.g, stone.b, 0.8f));
                break;
        }

        // Teo, two units to the left, so every shape is judged against the thing you steer.
        Dot(px, w, ox, oy, -3.4f, 0f, 1.6f * 1.30f, Col.Hex(0x141824), 0.30f);
        Dot(px, w, ox, oy, -3.4f, 0f, 1.6f, Col.Hex(0xECF1F7), 0.25f);
    }

    public static (Col[] px, int w, int h) Render()
    {
        int w = Cell * 3, h = Cell * 2;
        var px = new Col[w * h];

        var forms = new[] { LandmarkForm.Post, LandmarkForm.Frame, LandmarkForm.Stones,
                            LandmarkForm.Hollow, LandmarkForm.Hulk, LandmarkForm.Seam };

        // One representative world per form, so the ground is a real ground and the stone colour
        // comes out of the real contrast rule rather than a guess.
        var worlds = new[] { "yolkhaven", "cinderoost", "brineholt", "mosswell", "tidewrack", "amaranth" };

        for (int i = 0; i < forms.Length; i++)
        {
            int col = i % 3, row = i / 3;
            int x0 = col * Cell, y0 = row * Cell;
            var def = PlanetDatabase.Get(worlds[i]);
            var land = new Col(def.Land.r, def.Land.g, def.Land.b, 1f);

            for (int y = y0; y < y0 + Cell; y++)
                for (int x = x0; x < x0 + Cell; x++)
                    px[y * w + x] = land;

            // The same AgainstGround the game uses: tint halfway to the ground, then separate.
            float lum = 0.2126f * land.r + 0.7152f * land.g + 0.0722f * land.b;
            var toward = lum > 0.5f ? new Col(0, 0, 0) : new Col(1, 1, 1);
            var stone = Col.Lerp(Col.Lerp(new Col(0.62f, 0.64f, 0.72f), land, 0.5f), toward, 0.42f);

            Form(px, w, x0 + Cell * 0.55f, y0 + Cell * 0.62f, forms[i], stone);
        }
        return (px, w, h);
    }
}
