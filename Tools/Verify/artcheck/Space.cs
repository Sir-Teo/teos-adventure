using System;

/// Mirrors SpaceMode: parallax starfield, nebulae, planets at their real positions and radii.
/// Lets me see whether the hand-placed layout actually frames well through the game camera.
static class Space
{
    public struct World
    {
        public string Name; public float X, Y, R; public int Ocean, Land, Atmo; public int Sector;
    }

    // The 14 worlds, straight out of PlanetDatabase.
    public static readonly World[] Worlds =
    {
        new World { Name="Yolkhaven",   X=0,   Y=0,    R=6f,   Ocean=0x3E7A4E, Land=0x8FCB6B, Atmo=0xBFF0A8, Sector=0 },
        new World { Name="Cinderoost",  X=48,  Y=16,   R=5.5f, Ocean=0x5A1F0E, Land=0xD1552A, Atmo=0xFFB07A, Sector=0 },
        new World { Name="Brineholt",   X=-44, Y=22,   R=5.5f, Ocean=0x14486E, Land=0x3D93C4, Atmo=0x9FDCF5, Sector=0 },
        new World { Name="Mosswell",    X=10,  Y=-46,  R=5f,   Ocean=0x2C5B38, Land=0x6FA858, Atmo=0xA8DE94, Sector=0 },
        new World { Name="Tidewrack",   X=-72, Y=-30,  R=5.5f, Ocean=0x0E3A5A, Land=0x2F7CA8, Atmo=0x86C9E8, Sector=1 },
        new World { Name="Voltacrest",  X=60,  Y=-44,  R=5f,   Ocean=0x3B3410, Land=0xD9B429, Atmo=0xFFEF9C, Sector=1 },
        new World { Name="Emberfall",   X=78,  Y=66,   R=5.5f, Ocean=0x4A1508, Land=0xE0632C, Atmo=0xFFA36B, Sector=1 },
        new World { Name="Cobblestead", X=98,  Y=18,   R=5.5f, Ocean=0x3D3226, Land=0xA88C6B, Atmo=0xD9C4A8, Sector=1 },
        new World { Name="Glacierim",   X=-88, Y=26,   R=6f,   Ocean=0x2A5F76, Land=0xBEE8F4, Atmo=0xE4FAFF, Sector=1 },
        new World { Name="Umbralux",    X=30,  Y=96,   R=6f,   Ocean=0x1A1533, Land=0x54487F, Atmo=0x9B8BD6, Sector=2 },
        new World { Name="Aetherwake",  X=86,  Y=116,  R=5.5f, Ocean=0x2A1A4E, Land=0x9B6BD6, Atmo=0xD6B8FF, Sector=2 },
        new World { Name="Nullreach",   X=-42, Y=108,  R=5.5f, Ocean=0x110E24, Land=0x3E3663, Atmo=0x7A6DB0, Sector=2 },
        new World { Name="Vesper",      X=-96, Y=88,   R=5f,   Ocean=0x1E4256, Land=0x9FC8DA, Atmo=0xD2ECF7, Sector=2 },
        new World { Name="Shimmerfen",  X=-2,  Y=54,   R=5.5f, Ocean=0x2E2358, Land=0xB49BE8, Atmo=0xE8DCFF, Sector=1 },
        new World { Name="Arcmoor",     X=-58, Y=66,   R=5f,   Ocean=0x2B2E14, Land=0xBFD13A, Atmo=0xEEFFA8, Sector=1 },
        new World { Name="Cairnhold",   X=-82, Y=132,  R=5.5f, Ocean=0x2A2A33, Land=0x8A8794, Atmo=0xC6C3D4, Sector=2 },
        new World { Name="Amaranth",    X=-6,  Y=168,  R=12f,  Ocean=0x3A1152, Land=0xC46BE8, Atmo=0xF2B8FF, Sector=3 },
    };

    /// Renders a view centred on (camX, camY) covering `viewHeight` world units.
    public static Col[] Render(int size, float camX, float camY, float viewHeight, bool labels)
    {
        var px = new Col[size * size];
        var bg = Col.Hex(0x05060E);
        for (int i = 0; i < px.Length; i++) px[i] = bg;
        float scale = size / viewHeight;

        void Disc(float wx, float wy, float diameter, Col col, float soft)
        {
            float cx = size * 0.5f + (wx - camX) * scale;
            float cy = size * 0.5f + (wy - camY) * scale;
            float rad = diameter * 0.5f * scale;
            if (cx + rad < 0 || cx - rad > size || cy + rad < 0 || cy - rad > size) return;
            for (int y = Math.Max(0, (int)(cy - rad - 2)); y < Math.Min(size, (int)(cy + rad + 2)); y++)
                for (int x = Math.Max(0, (int)(cx - rad - 2)); x < Math.Min(size, (int)(cx + rad + 2)); x++)
                {
                    float d = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / Math.Max(0.001f, rad);
                    if (d > 1f) continue;
                    Program.OverPublic(px, y * size + x, col, M.Clamp01((1f - d) / soft) * col.a);
                }
        }

        var rng = new Random(20260807);

        // Tiled layers, matching SpaceMode: each wraps around the camera.
        float[] depths = { 0.86f, 0.55f, 0.18f };
        float[] tiles = { 120f, 140f, 160f };
        int[] counts = { 700, 450, 260 };
        float[] lo = { 0.05f, 0.09f, 0.14f }, hi = { 0.13f, 0.20f, 0.32f };
        float[] alo = { 0.26f, 0.42f, 0.60f }, ahi = { 0.55f, 0.80f, 1.00f };

        // Nebulae first, furthest back.
        {
            float tile = 200f;
            float driftX = camX * (1f - 0.93f), driftY = camY * (1f - 0.93f);
            float wrapX = (float)Math.Round(driftX / tile) * tile, wrapY = (float)Math.Round(driftY / tile) * tile;
            var tints = new[] { Col.Hex(0x59388C), Col.Hex(0x294D85), Col.Hex(0x73334D) };
            for (int i = 0; i < 18; i++)
            {
                var t = tints[i % 3];
                float lx = -100f + (float)rng.NextDouble() * 200f;
                float ly = -100f + (float)rng.NextDouble() * 200f;
                float sz = 26f + (float)rng.NextDouble() * 32f;
                for (int rx = -1; rx <= 1; rx++)
                    for (int ry = -1; ry <= 1; ry++)
                        Disc(lx + camX * 0.93f + wrapX + rx * tile, ly + camY * 0.93f + wrapY + ry * tile,
                             sz, new Col(t.r, t.g, t.b, 0.24f), 1.4f);
            }
        }

        for (int layer = 0; layer < 3; layer++)
        {
            float tile = tiles[layer], depth = depths[layer], half = tile * 0.5f;
            float driftX = camX * (1f - depth), driftY = camY * (1f - depth);
            float wrapX = (float)Math.Round(driftX / tile) * tile, wrapY = (float)Math.Round(driftY / tile) * tile;
            float warmth = layer / 2f;

            for (int i = 0; i < counts[layer]; i++)
            {
                float lx = -half + (float)rng.NextDouble() * tile;
                float ly = -half + (float)rng.NextDouble() * tile;
                float sz = lo[layer] + (float)rng.NextDouble() * (hi[layer] - lo[layer]);
                float a = alo[layer] + (float)rng.NextDouble() * (ahi[layer] - alo[layer]);
                var col = new Col(0.82f + 0.18f * warmth, 0.88f + 0.10f * warmth, 1f, a);
                // Draw the neighbouring tiles too, so the wrap is seamless in the preview.
                for (int rx = -1; rx <= 1; rx++)
                    for (int ry = -1; ry <= 1; ry++)
                        Disc(lx + camX * depth + wrapX + rx * tile, ly + camY * depth + wrapY + ry * tile,
                             sz * 2f, col, 1f);
            }
        }

        // Planets: glow, body, a simple terminator.
        foreach (var w in Worlds)
        {
            Disc(w.X, w.Y, w.R * 2.6f, new Col(Col.Hex(w.Atmo).r, Col.Hex(w.Atmo).g, Col.Hex(w.Atmo).b, 0.30f), 1.6f);
            Disc(w.X, w.Y, w.R * 2f, Col.Hex(w.Land), 0.04f);
            // Continent mottling and the shaded limb.
            var rng2 = new Random(w.Name.GetHashCode() & 0xFFFF);
            for (int i = 0; i < 9; i++)
            {
                float a = (float)rng2.NextDouble() * 6.2832f;
                float d = (float)Math.Sqrt(rng2.NextDouble()) * w.R * 0.72f;
                Disc(w.X + (float)Math.Cos(a) * d, w.Y + (float)Math.Sin(a) * d,
                     w.R * (0.4f + 0.5f * (float)rng2.NextDouble()), Col.Hex(w.Ocean), 0.5f);
            }
            Disc(w.X + w.R * 0.42f, w.Y - w.R * 0.42f, w.R * 1.5f, new Col(0, 0, 0, 0.35f), 1.2f);
        }
        return px;
    }
}
