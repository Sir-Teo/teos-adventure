using System;
using System.Collections.Generic;
using Eggverse;

/// Mirrors SpaceMode: parallax starfield, nebulae, planets at their real positions and radii.
/// Lets me see whether the hand-placed layout actually frames well through the game camera.
static class Space
{
    // No table here any more. This kept its own copy of every world - positions, radii and
    // three colours each - which had already drifted: the comment above it said "the 14 worlds"
    // while the array held 17, because adding planets meant remembering to edit two places.
    public static IReadOnlyList<PlanetDef> Worlds => PlanetDatabase.All;

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

        // The drift: motes of warmth crossing the sector, every one of them toward Amaranth.
        // Drawn with a trail so a still picture shows the direction - in motion the direction is
        // what you notice, and a single frame would otherwise show only scattered dots.
        void DrawDrift()
        {
            var amaranth = PlanetDatabase.Get("amaranth");
            var drng = new Random(0x0D71F7);
            for (int i = 0; i < SpaceMode.DriftCount; i++)
            {
                float a = (float)drng.NextDouble() * (float)Math.PI * 2f;
                float r = SpaceMode.DriftFieldRadius * (float)Math.Sqrt(drng.NextDouble());
                float mx = camX + (float)Math.Cos(a) * r, my = camY + (float)Math.Sin(a) * r;

                float tx = amaranth.SpacePosition.x - mx, ty = amaranth.SpacePosition.y - my;
                float len = (float)Math.Sqrt(tx * tx + ty * ty);
                if (len < 0.001f) continue;
                tx /= len; ty /= len;

                float alpha = 0.34f + 0.34f * (float)drng.NextDouble();
                float sz = 0.14f + 0.20f * (float)drng.NextDouble();
                // Tightly spaced so the trail is a streak rather than a row of dots. In motion
                // the direction is the whole effect; a still has only the streak to say it with.
                for (int k = 0; k < 9; k++)
                    Disc(mx - tx * k * 0.20f, my - ty * k * 0.20f, sz * (1f - k * 0.07f),
                         new Col(1f, 0.84f, 0.58f, alpha * (1f - k * 0.10f)), 0.9f);
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

        DrawDrift();

        // Planets: glow, body, a simple terminator.
        foreach (var w in Worlds)
        {
            float wx = w.SpacePosition.x, wy = w.SpacePosition.y, wr = w.SpaceRadius;
            var atmo = new Col(w.Atmosphere.r, w.Atmosphere.g, w.Atmosphere.b, 0.30f);
            var land = new Col(w.Land.r, w.Land.g, w.Land.b, 1f);
            var ocean = new Col(w.Ocean.r, w.Ocean.g, w.Ocean.b, 1f);

            Disc(wx, wy, wr * 2.6f, atmo, 1.6f);
            Disc(wx, wy, wr * 2f, land, 0.04f);

            // The first egg's seam, drawn the way the sprite draws it - corner to corner,
            // widest at the middle where it started.
            if (w.IsBossWorld)
                for (int k = -22; k <= 22; k++)
                {
                    float nx = k / 22f;
                    float along = Math.Max(0f, 1f - Math.Abs(nx) * 0.9f);
                    if (along <= 0f) continue;
                    float wander = 0.06f * (float)Math.Sin(nx * 6.1f) + 0.03f * (float)Math.Sin(nx * 13.7f);
                    float ny = nx * 0.42f + wander;
                    Disc(wx + nx * wr, wy + ny * wr, wr * (0.10f + 0.10f * along),
                         new Col(0.05f, 0.03f, 0.08f, 0.85f), 0.5f);
                }
            // Continent mottling and the shaded limb.
            var rng2 = new Random(w.Seed & 0xFFFF);   // the planet's own stable seed
            for (int i = 0; i < 9; i++)
            {
                float a = (float)rng2.NextDouble() * 6.2832f;
                float d = (float)Math.Sqrt(rng2.NextDouble()) * wr * 0.72f;
                Disc(wx + (float)Math.Cos(a) * d, wy + (float)Math.Sin(a) * d,
                     wr * (0.4f + 0.5f * (float)rng2.NextDouble()), ocean, 0.5f);
            }
            Disc(wx + wr * 0.42f, wy - wr * 0.42f, wr * 1.5f, new Col(0, 0, 0, 0.35f), 1.2f);
        }
        return px;
    }
}
