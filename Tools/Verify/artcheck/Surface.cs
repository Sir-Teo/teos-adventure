using System;

/// Mirrors SurfaceMode's composition so a landed planet can actually be looked at:
/// ground, terrain mottling, biome decor, shell fields in their three layouts, nest station.
static class Surface
{
    const float R = 34f;              // walkable radius in world units

    struct Ctx
    {
        public Col[] Px;
        public int Size;
        public float Scale;           // pixels per world unit
        public float Ox, Oy;          // world point the camera is centred on
    }

    static void Dot(Ctx c, float wx, float wy, float diameter, Col col, float soft = 0.12f)
    {
        // World -> pixel, y up.
        float cx = c.Size * 0.5f + (wx - c.Ox) * c.Scale;
        float cy = c.Size * 0.5f + (wy - c.Oy) * c.Scale;
        float rad = diameter * 0.5f * c.Scale;
        int x0 = (int)(cx - rad - 2), x1 = (int)(cx + rad + 2);
        int y0 = (int)(cy - rad - 2), y1 = (int)(cy + rad + 2);
        for (int y = Math.Max(0, y0); y < Math.Min(c.Size, y1); y++)
            for (int x = Math.Max(0, x0); x < Math.Min(c.Size, x1); x++)
            {
                float d = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / Math.Max(0.001f, rad);
                float a = M.Clamp01((1f - d) / soft);
                if (a > 0f) Program.OverPublic(c.Px, y * c.Size + x, col, a * col.a);
            }
    }

    static void Ellipse(Ctx c, float wx, float wy, float dw, float dh, Col col, float soft = 0.15f)
    {
        float cx = c.Size * 0.5f + (wx - c.Ox) * c.Scale, cy = c.Size * 0.5f + (wy - c.Oy) * c.Scale;
        float rx = dw * 0.5f * c.Scale, ry = dh * 0.5f * c.Scale;
        for (int y = Math.Max(0, (int)(cy - ry - 2)); y < Math.Min(c.Size, (int)(cy + ry + 2)); y++)
            for (int x = Math.Max(0, (int)(cx - rx - 2)); x < Math.Min(c.Size, (int)(cx + rx + 2)); x++)
            {
                float ddx = (x - cx) / Math.Max(0.001f, rx), ddy = (y - cy) / Math.Max(0.001f, ry);
                float d = (float)Math.Sqrt(ddx * ddx + ddy * ddy);
                float a = M.Clamp01((1f - d) / soft);
                if (a > 0f) Program.OverPublic(c.Px, y * c.Size + x, col, a * col.a);
            }
    }

    /// Irregular patch, the same wobble function ProcArt.Blob uses.
    static void Blob(Ctx c, float wx, float wy, float diameter, Col col, int seed)
    {
        float cx = c.Size * 0.5f + wx * c.Scale, cy = c.Size * 0.5f + wy * c.Scale;
        float rad = diameter * 0.5f * c.Scale;
        for (int y = Math.Max(0, (int)(cy - rad - 2)); y < Math.Min(c.Size, (int)(cy + rad + 2)); y++)
            for (int x = Math.Max(0, (int)(cx - rad - 2)); x < Math.Min(c.Size, (int)(cx + rad + 2)); x++)
            {
                float nx = (x - cx) / rad, ny = (y - cy) / rad;
                float r = (float)Math.Sqrt(nx * nx + ny * ny);
                if (r > 1f) continue;
                float ang = (float)Math.Atan2(ny, nx);
                float wob = Program.NoisePublic((float)Math.Cos(ang) * 1.7f + 8f, (float)Math.Sin(ang) * 1.7f + 8f, seed);
                float edge = 0.62f + 0.30f * wob;
                float a = M.Clamp01((edge - r) / 0.10f);
                if (a > 0f) Program.OverPublic(c.Px, y * c.Size + x, col, a * 0.9f * col.a);
            }
    }

    static void Bar(Ctx c, float wx, float wy, float bw, float bh, Col col, float deg = 0f)
    {
        float cx = c.Size * 0.5f + (wx - c.Ox) * c.Scale, cy = c.Size * 0.5f + (wy - c.Oy) * c.Scale;
        float hw = bw * 0.5f * c.Scale, hh = bh * 0.5f * c.Scale;
        double rad = deg * Math.PI / 180.0;
        float cs = (float)Math.Cos(rad), sn = (float)Math.Sin(rad);
        int reach = (int)(Math.Max(hw, hh) + 2);
        for (int y = Math.Max(0, (int)cy - reach); y < Math.Min(c.Size, (int)cy + reach); y++)
            for (int x = Math.Max(0, (int)cx - reach); x < Math.Min(c.Size, (int)cx + reach); x++)
            {
                float dx = x - cx, dy = y - cy;
                float lx = dx * cs - dy * sn, ly = dx * sn + dy * cs;
                if (Math.Abs(lx) <= hw && Math.Abs(ly) <= hh)
                    Program.OverPublic(c.Px, y * c.Size + x, col, col.a);
            }
    }

    static void RingAt(Ctx c, float wx, float wy, float d, float thick, Col col)
    {
        float cx = c.Size * 0.5f + (wx - c.Ox) * c.Scale, cy = c.Size * 0.5f + (wy - c.Oy) * c.Scale;
        float rad = d * 0.5f * c.Scale, t = Math.Max(1f, thick * d * c.Scale);
        for (int y = Math.Max(0, (int)(cy - rad - 2)); y < Math.Min(c.Size, (int)(cy + rad + 2)); y++)
            for (int x = Math.Max(0, (int)(cx - rad - 2)); x < Math.Min(c.Size, (int)(cx + rad + 2)); x++)
            {
                float r = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                float a = M.Clamp01(1f - Math.Abs(r - rad + t * 0.5f) / (t * 0.5f));
                if (a > 0f) Program.OverPublic(c.Px, y * c.Size + x, col, a * col.a);
            }
    }

    /// Mirrors SurfaceMode.BuildLandmarkForm. This is the only place the mocks draw a landmark -
    /// there were briefly two, and the one embedded in the world render was a generic grey stone
    /// that had never heard of the six forms.
    static void DrawForm(Ctx c, float lx, float ly, Eggverse.LandmarkForm form, Col stone)
    {
        var dark = new Col(stone.r * 0.66f, stone.g * 0.66f, stone.b * 0.72f, 1f);
        var outline = new Col(0.07f, 0.08f, 0.13f, 0.85f);

        if (form != Eggverse.LandmarkForm.Seam)
            Dot(c, lx, ly - 1.0f, 3.4f, new Col(dark.r, dark.g, dark.b, 0.55f), 0.5f);

        switch (form)
        {
            case Eggverse.LandmarkForm.Post:
                Bar(c, lx, ly + 0.55f, 1.06f, 3.66f, outline);
                Bar(c, lx, ly + 0.55f, 0.82f, 3.4f, stone);
                Bar(c, lx, ly + 2.3f, 1.74f, 0.86f, outline);
                Bar(c, lx, ly + 2.3f, 1.5f, 0.62f, stone);
                Dot(c, lx, ly + 2.5f, 2.8f, new Col(1f, 0.92f, 0.7f, 0.26f), 0.9f);
                break;

            case Eggverse.LandmarkForm.Frame:
                Bar(c, lx - 1.0f, ly + 0.5f, 0.58f, 3.24f, outline);
                Bar(c, lx + 1.0f, ly + 0.5f, 0.58f, 3.24f, outline);
                Bar(c, lx, ly + 1.9f, 2.84f, 0.54f, outline);
                Bar(c, lx - 1.0f, ly + 0.5f, 0.34f, 3.0f, stone);
                Bar(c, lx + 1.0f, ly + 0.5f, 0.34f, 3.0f, stone);
                Bar(c, lx, ly + 1.9f, 2.6f, 0.3f, stone);
                Dot(c, lx, ly + 1.0f, 1.42f, outline, 0.16f);
                Dot(c, lx, ly + 1.0f, 1.15f, dark, 0.16f);
                break;

            case Eggverse.LandmarkForm.Stones:
                for (int i = 0; i < 5; i++)
                {
                    float t = (i - 2f) * 1.18f, h = 1.5f - Math.Abs(i - 2f) * 0.26f;
                    Dot(c, lx + t, ly + h * 0.34f, h * 1.22f, outline, 0.14f);
                    Dot(c, lx + t, ly + h * 0.34f, h, stone, 0.14f);
                }
                break;

            case Eggverse.LandmarkForm.Hollow:
                RingAt(c, lx, ly, 3.5f, 0.26f, outline);
                RingAt(c, lx, ly, 3.2f, 0.20f, stone);
                Dot(c, lx, ly, 2.1f, new Col(0.04f, 0.05f, 0.09f, 0.92f), 0.06f);
                break;

            case Eggverse.LandmarkForm.Hulk:
                Dot(c, lx, ly + 0.5f, 4.0f, outline, 0.16f);
                Dot(c, lx, ly + 0.5f, 3.6f, dark, 0.16f);
                Bar(c, lx + 0.75f, ly + 1.85f, 3.5f, 0.78f, outline, 26f);
                Bar(c, lx + 0.75f, ly + 1.85f, 3.2f, 0.5f, stone, 26f);
                break;

            case Eggverse.LandmarkForm.Seam:
                Bar(c, lx, ly, 7.0f, 0.26f, new Col(0.05f, 0.05f, 0.09f, 0.9f));
                Bar(c, lx, ly + 0.02f, 6.6f, 0.13f, new Col(stone.r, stone.g, stone.b, 0.8f));
                break;
        }
    }

    public static Col[] Render(int size, int ocean, int land, int atmo, string theme, int seed, string layout, float viewUnits = 0f, string planetId = null, bool onLandmark = false,
                               bool showWarming = false)
    {
        // viewUnits > 0 renders the in-game camera framing (30 units tall) rather than the disc.
        float span = viewUnits > 0f ? viewUnits : R * 2.25f;
        var c = new Ctx { Px = new Col[size * size], Size = size, Scale = size / span };
        if (onLandmark && planetId != null)
        {
            var focus = Eggverse.SurfaceLayout.LandmarkPosition(Eggverse.PlanetDatabase.Get(planetId));
            c.Ox = focus.x; c.Oy = focus.y;
        }
        var bg = Col.Hex(0x05060E);
        for (int i = 0; i < c.Px.Length; i++) c.Px[i] = bg;

        var rng = new Random(seed);
        Col landC = Col.Hex(land), oceanC = Col.Hex(ocean), atmoC = Col.Hex(atmo);

        // Atmosphere glow, then the ground disc as a radial gradient.
        Dot(c, 0, 0, R * 2.25f, new Col(atmoC.r, atmoC.g, atmoC.b, 0.30f), 0.55f);
        for (int i = 24; i >= 0; i--)
        {
            float t = i / 24f;
            var col = Col.Lerp(landC, Col.Lerp(oceanC, new Col(0, 0, 0), 0.25f), (float)Math.Pow(t, 1.4f));
            Dot(c, 0, 0, R * 2f * (t * 0.02f + 1f) - (1f - t) * 0f, col, 0.04f);
            if (i > 0) Dot(c, 0, 0, R * 2f * t, col, 0.06f);
        }

        // Terrain mottling.
        for (int i = 0; i < 46; i++)
        {
            float a = (float)rng.NextDouble() * 6.2832f;
            float d = (float)Math.Sqrt(rng.NextDouble()) * R * 0.97f;
            float sz = 3f + (float)rng.NextDouble() * 8f;
            bool lighter = rng.NextDouble() > 0.5;
            var tint = Col.Lerp(landC, lighter ? new Col(1, 1, 1) : new Col(0, 0, 0), 0.16f);
            tint.a = 0.5f;
            Blob(c, (float)Math.Cos(a) * d, (float)Math.Sin(a) * d, sz, tint, (i % 6) * 7 + 3);
        }

        // Biome decor.
        for (int i = 0; i < 34; i++)
        {
            float a = (float)rng.NextDouble() * 6.2832f;
            float d = (float)Math.Sqrt(rng.NextDouble()) * R * 0.94f;
            float x = (float)Math.Cos(a) * d, y = (float)Math.Sin(a) * d;
            float s = 0.8f + (float)rng.NextDouble() * 0.8f;
            float roll = (float)rng.NextDouble();

            switch (theme)
            {
                case "Verdant":
                    Ellipse(c, x, y - 0.55f * s, 0.18f * s, 0.62f * s, Col.Hex(0x573D26), 0.3f);
                    Blob(c, x, y + 0.35f * s, 2.3f * s, Col.Lerp(landC, new Col(0, 0, 0), 0.34f), (i % 6) * 13 + 5);
                    break;
                case "Molten":
                    if (roll < 0.45f)
                    {
                        Dot(c, x, y, 3.4f * s, new Col(1f, 0.62f, 0.20f, 0.45f), 0.7f);
                        Blob(c, x, y, 1.1f * s, new Col(1f, 0.55f, 0.18f, 0.95f), (i % 5) * 7 + 2);
                    }
                    else Blob(c, x, y, 1.3f * s, new Col(0.16f, 0.11f, 0.10f, 0.9f), (i % 5) * 11 + 3);
                    break;
                case "Frost":
                {
                    float l2 = 0.2126f * landC.r + 0.7152f * landC.g + 0.0722f * landC.b;
                    var shard = Col.Lerp(Col.Lerp(landC, new Col(0.72f, 0.88f, 1f), 0.6f),
                                         l2 > 0.5f ? new Col(0,0,0) : new Col(1,1,1), 0.40f);
                    shard.a = 0.95f;
                    Ellipse(c, x, y, 0.30f * s * 1.8f, 0.95f * s * 1.8f, shard, 0.25f);
                    break;
                }
                case "Void":
                    Ellipse(c, x, y, 2.4f * s, 1.2f * s, new Col(0.03f, 0.02f, 0.08f, 0.88f), 0.3f);
                    break;
                case "Volt":
                    Dot(c, x, y + 0.7f * s, 2.2f * s, new Col(1f, 0.85f, 0.25f, 0.35f), 0.8f);
                    Ellipse(c, x, y, 0.22f * s * 1.6f, 1.0f * s * 1.6f, Col.Hex(0xFFD23F), 0.25f);
                    break;
                default:
                    Blob(c, x, y, 1.7f * s, Col.Lerp(oceanC, new Col(0, 0, 0), 0.22f), (i % 6) * 13 + 5);
                    break;
            }
        }

        // Shell fields, in whichever layout this world uses.
        // Mirror SurfaceMode.AgainstGround: tint toward the element, then force separation.
        float lum = 0.2126f * landC.r + 0.7152f * landC.g + 0.0722f * landC.b;
        var themeCol = theme == "Verdant" ? Col.Hex(0x5FD068) : theme == "Molten" ? Col.Hex(0xFF6B35)
                     : theme == "Frost" ? Col.Hex(0x8FE3F2) : theme == "Void" ? Col.Hex(0x6C63A6)
                     : Col.Hex(0xFFD23F);
        var fieldCol = Col.Lerp(Col.Lerp(landC, themeCol, 0.55f),
                                lum > 0.5f ? new Col(0,0,0) : new Col(1,1,1), 0.30f);
        fieldCol.a = 0.85f;
        int clumps = 3;
        var cxs = new float[clumps]; var cys = new float[clumps];
        for (int k = 0; k < clumps; k++)
        {
            float ca = (float)rng.NextDouble() * 6.2832f;
            float cd = R * (0.30f + 0.40f * (float)rng.NextDouble());
            cxs[k] = (float)Math.Cos(ca) * cd; cys[k] = (float)Math.Sin(ca) * cd;
        }
        for (int i = 0; i < 9; i++)
        {
            float a = (float)rng.NextDouble() * 6.2832f;
            float x, y, rad;
            if (layout == "Ring")
            {
                float d = R * (0.60f + 0.22f * (float)rng.NextDouble());
                x = (float)Math.Cos(a) * d; y = (float)Math.Sin(a) * d; rad = 4.5f + 1.5f * (float)rng.NextDouble();
            }
            else if (layout == "Clustered")
            {
                float off = 8f * (float)rng.NextDouble();
                x = cxs[i % clumps] + (float)Math.Cos(a) * off;
                y = cys[i % clumps] + (float)Math.Sin(a) * off;
                rad = 3.5f + 2f * (float)rng.NextDouble();
            }
            else
            {
                float d = R * (0.28f + 0.58f * (float)rng.NextDouble());
                x = (float)Math.Cos(a) * d; y = (float)Math.Sin(a) * d; rad = 4f + 2.5f * (float)rng.NextDouble();
            }
            Blob(c, x, y, rad * 2f, fieldCol, i * 31 + 11);
        }

        // A roamer standing on a shell field, with the backing disc it now carries. This is the
        // case the numbers were failing: an egg of the world's own element, on fields tinted
        // toward that same element.
        {
            float lum2 = 0.2126f * landC.r + 0.7152f * landC.g + 0.0722f * landC.b;
            var haloTarget = lum2 > 0.5f ? new Col(0, 0, 0) : new Col(1, 1, 1);
            var halo = Col.Lerp(landC, haloTarget, 0.84f);
            Dot(c, cxs[0], cys[0], 4.2f, new Col(halo.r, halo.g, halo.b, 0.55f), 0.9f);
            Ellipse(c, cxs[0], cys[0], 1.5f, 1.9f, themeCol, 0.10f);
        }

        // The landmark, at the real placement, so the one thing on this world worth walking to
        // can actually be looked at against the ground it stands on.
        {
            var lm = planetId == null ? null : Eggverse.LandmarkDatabase.For(planetId);
            if (lm != null)
            {
                var lp = Eggverse.SurfaceLayout.LandmarkPosition(Eggverse.PlanetDatabase.Get(planetId));
                float lx = lp.x, ly = lp.y;

                float lum3 = 0.2126f * landC.r + 0.7152f * landC.g + 0.0722f * landC.b;
                var toward = lum3 > 0.5f ? new Col(0, 0, 0) : new Col(1, 1, 1);
                var stone = Col.Lerp(Col.Lerp(new Col(0.62f, 0.64f, 0.72f), landC, 0.5f), toward, 0.42f);
                DrawForm(c, lx, ly, lm.Form, stone);
            }
        }

        // Nest station at the centre, and Teo just below it. A station that has gone cold is
        // drawn cold - the game says four of them are dead and drew all seventeen the same.
        bool coldPad = planetId != null && Eggverse.PlanetDatabase.StationCold(planetId);
        Dot(c, 0, 0, 7f, new Col(1f, 0.9f, 0.6f, coldPad ? 0.13f : 0.45f), 0.8f);
        Dot(c, 0, 0, 5.4f, coldPad ? new Col(0.42f, 0.46f, 0.58f, 0.55f)
                                   : new Col(1f, 0.78f, 0.30f, 0.55f), 0.10f);
        Dot(c, 0, 0, 4.9f, Col.Lerp(landC, new Col(0, 0, 0), 0.2f), 0.06f);
        Dot(c, 0, 0, 2.6f, coldPad ? Col.Hex(0x9EA8BC) : Col.Hex(0xFFE0A0), 0.2f);
        // Teo, with the dark outline the sprite now carries. On a bright world this is the
        // only thing separating a white suit from the ground.
        float tx = c.Ox, ty = c.Oy - (onLandmark ? 3.2f : 6.5f);
        Dot(c, tx, ty, 1.6f * 1.30f, Col.Hex(0x141824), 0.30f);
        Dot(c, tx, ty, 1.6f, Col.Hex(0xECF1F7), 0.25f);
        Dot(c, tx, ty - 0.4f, 0.9f, Col.Hex(0x142A4A), 0.35f);

        // The nest out on the pad, warming it. Six eggs at the same radius the game uses, so
        // the layout can be checked by eye as well as by arithmetic.
        if (showWarming)
        {
            Dot(c, 0, 0, 8.5f, new Col(1f, 0.86f, 0.55f, 0.30f), 0.9f);
            for (int i = 0; i < 6; i++)
            {
                double ang = Math.PI * 0.5 + i * (Math.PI * 2.0 / 6.0);
                float ex = (float)Math.Cos(ang) * Eggverse.SurfaceMode.WarmingRingRadius;
                float ey = (float)Math.Sin(ang) * Eggverse.SurfaceMode.WarmingRingRadius - 0.2f;
                Dot(c, ex, ey, 1.15f * 1.20f, Col.Hex(0x141824), 0.30f);
                Dot(c, ex, ey, 1.15f, Col.Hex(0xBFE39A), 0.25f);
            }
        }

        // Horizon ring.
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float nx = (x - size * 0.5f) / c.Scale + c.Ox, ny = (y - size * 0.5f) / c.Scale + c.Oy;
                float r = (float)Math.Sqrt(nx * nx + ny * ny);
                float a = M.Clamp01(1f - Math.Abs(r - R) / 0.5f);
                if (a > 0f) Program.OverPublic(c.Px, y * size + x, atmoC, a * 0.55f);
            }
        return c.Px;
    }
}
