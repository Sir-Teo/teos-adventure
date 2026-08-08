// Standalone preview of ProcArt's pixel maths (same formulas, Unity types stubbed out)
// so the generated art can be eyeballed before opening the editor.
using System;
using System.IO;

struct Col
{
    public float r, g, b, a;
    public Col(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
    public static Col Hex(int rgb, float a = 1f) =>
        new Col(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, a);
    public static Col Lerp(Col x, Col y, float t)
    {
        t = M.Clamp01(t);
        return new Col(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t);
    }
    public Col Mul(float m) => new Col(r * m, g * m, b * m, a);
}

static class M
{
    public static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    public static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);
    public static float Sqrt(float v) => (float)Math.Sqrt(v);
    public static float Max(float a, float b) => a > b ? a : b;
    public static float Min(float a, float b) => a < b ? a : b;
    public static float Abs(float v) => Math.Abs(v);
    public static float Pow(float a, float b) => (float)Math.Pow(a, b);
    public static int Floor(float v) => (int)Math.Floor(v);
    public static float SmoothStep(float from, float to, float t)
    {
        t = Clamp01((t - from) / (to - from == 0f ? 1e-6f : to - from));
        return t * t * (3f - 2f * t);
    }
    public static float Dot(float ax, float ay, float az, float bx, float by, float bz) => ax * bx + ay * by + az * bz;
}

static class Program
{
    // ---------------- noise (verbatim from ProcArt) ----------------
    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263 + seed * 362437);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }

    static float Noise(float x, float y, int seed)
    {
        int xi = M.Floor(x), yi = M.Floor(y);
        float xf = x - xi, yf = y - yi;
        float u = xf * xf * (3f - 2f * xf);
        float v = yf * yf * (3f - 2f * yf);
        float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed);
        float c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
        return M.Lerp(M.Lerp(a, b, u), M.Lerp(c, d, u), v);
    }

    static float Fbm(float x, float y, int seed, int octaves = 4)
    {
        float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
        for (int i = 0; i < octaves; i++)
        {
            sum += Noise(x * freq, y * freq, seed + i * 71) * amp;
            norm += amp; amp *= 0.5f; freq *= 2.03f;
        }
        return norm > 0f ? sum / norm : 0f;
    }

    public static void OverPublic(Col[] px, int i, Col src, float alpha) => Over(px, i, src, alpha);
    public static float NoisePublic(float x, float y, int seed) => Noise(x, y, seed);

    static void Over(Col[] px, int i, Col src, float alpha)
    {
        if (alpha <= 0f) return;
        alpha = M.Clamp01(alpha * src.a);
        Col dst = px[i];
        float outA = alpha + dst.a * (1f - alpha);
        if (outA <= 0.0001f) { px[i] = new Col(0, 0, 0, 0); return; }
        px[i] = new Col(
            (src.r * alpha + dst.r * dst.a * (1f - alpha)) / outA,
            (src.g * alpha + dst.g * dst.a * (1f - alpha)) / outA,
            (src.b * alpha + dst.b * dst.a * (1f - alpha)) / outA, outA);
    }

    static void FillEllipse(Col[] px, int size, float cx, float cy, float rx, float ry, Col col, float soft = 0.05f)
    {
        for (int y = 0; y < size; y++)
        {
            float ny = (y + 0.5f) / size * 2f - 1f;
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float dx = (nx - cx) / M.Max(0.0001f, rx);
                float dy = (ny - cy) / M.Max(0.0001f, ry);
                float t = M.Sqrt(dx * dx + dy * dy);
                Over(px, y * size + x, col, M.Clamp01((1f - t) / M.Max(0.0001f, soft)));
            }
        }
    }

    static Col[] Buffer(int size) => new Col[size * size];

    // ---------------- egg ----------------

    static float PatternAmount(int pattern, float nx, float ny, float t, int seed, out bool towardLight)
    {
        towardLight = false;
        switch (pattern)
        {
            case 2: return M.SmoothStep(0.42f, 0.62f, Fbm(nx * 2.1f + seed * 0.2f, ny * 2.1f, seed, 3)) * 0.70f;
            case 3: {
                float w = Fbm(nx * 1.8f, ny * 1.8f, seed, 2) * 1.1f;
                float v = 0.5f + 0.5f * (float)Math.Sin((ny * 3.1f + seed * 0.11f) * Math.PI * 2f + w);
                return M.SmoothStep(0.52f, 0.78f, v) * 0.78f; }
            case 4: {
                float w = Fbm(nx * 2.2f, ny * 1.4f, seed, 2) * 0.9f;
                float v = 0.5f + 0.5f * (float)Math.Sin((nx * 3.6f + ny * 0.7f + seed * 0.09f) * Math.PI * 2f + w);
                return M.SmoothStep(0.55f, 0.80f, v) * 0.74f; }
            case 5: {
                float a = (float)Math.Atan2(ny, nx), r = M.Sqrt(nx * nx + ny * ny);
                float v = 0.5f + 0.5f * (float)Math.Sin(a * 3f + r * 7.5f + seed * 0.2f);
                return M.SmoothStep(0.48f, 0.74f, v) * 0.68f; }
            case 6: towardLight = true;
                return M.SmoothStep(0.72f, 0.82f, Fbm(nx * 9f + seed * 0.4f, ny * 9f, seed, 2)) * 0.95f;
            case 7: {
                float rid = 1f - M.Abs(2f * Fbm(nx * 3.0f + seed * 0.15f, ny * 3.0f, seed, 3) - 1f);
                return M.SmoothStep(0.87f, 0.98f, rid) * 0.88f; }
            case 8: { float g = 0.5f + 0.5f * (float)Math.Sin((ny * 1.15f + nx * 0.35f + seed * 0.07f) * Math.PI);
                      return M.SmoothStep(0.32f, 0.88f, g) * 0.34f; }
            default: return M.SmoothStep(0.56f, 0.68f, Fbm(nx * 4.6f + seed * 0.31f, ny * 4.6f, seed, 3)) * 0.62f;
        }
    }

    static Col[] Egg(Col body, Col accent, int artSeed, int size)
    {
        int pattern = 1 + Math.Abs(artSeed * 5 + 3) % 8;
        var px = Buffer(size);
        float lx = -0.45f, ly = 0.62f, lz = 0.64f;
        float ll = M.Sqrt(lx * lx + ly * ly + lz * lz); lx /= ll; ly /= ll; lz /= ll;
        Col rim = accent.Mul(0.55f);

        for (int y = 0; y < size; y++)
        {
            float ny = (y + 0.5f) / size * 2f - 1f;
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float halfW = 0.66f * (1f - 0.22f * ny);
                float halfH = 0.92f;
                float ex = nx / halfW, ey = ny / halfH;
                float f = ex * ex + ey * ey;
                float t = M.Sqrt(f);
                if (t > 1.06f) continue;
                float alpha = M.Clamp01((1f - t) / 0.045f);
                if (alpha <= 0f) continue;

                float z = M.Sqrt(M.Max(0f, 1f - M.Min(1f, f)));
                float vx = ex * 0.92f, vy = ey * 0.92f, vz = z + 0.15f;
                float vl = M.Sqrt(vx * vx + vy * vy + vz * vz);
                float lambert = M.Clamp01(M.Dot(vx / vl, vy / vl, vz / vl, lx, ly, lz));
                float shade = 0.38f + 0.78f * lambert;

                Col c = body.Mul(shade);
                bool towardLight;
                float amt = PatternAmount(pattern, nx, ny, t, artSeed, out towardLight);
                Col mark = towardLight ? Col.Lerp(body, new Col(1,1,1), 0.75f).Mul(shade) : accent.Mul(shade * 0.95f);
                c = Col.Lerp(c, mark, amt);
                c = Col.Lerp(c, rim, M.SmoothStep(0.80f, 1.0f, t) * 0.55f);
                Over(px, y * size + x, c, alpha);
            }
        }
        FillEllipse(px, size, -0.24f, 0.40f, 0.17f, 0.23f, new Col(1, 1, 1, 0.45f), 0.9f);
        FillEllipse(px, size, 0.16f, -0.62f, 0.28f, 0.13f, new Col(1, 1, 1, 0.13f), 1f);
        return px;
    }

    // ---------------- planet ----------------
    static Col[] Planet(Col ocean, Col land, Col atmo, int seed, int size)
    {
        var px = Buffer(size);
        const float extend = 1.32f;
        float lx = -0.5f, ly = 0.55f, lz = 0.67f;
        float ll = M.Sqrt(lx * lx + ly * ly + lz * lz); lx /= ll; ly /= ll; lz /= ll;

        for (int y = 0; y < size; y++)
        {
            float ny = ((y + 0.5f) / size * 2f - 1f) * extend;
            for (int x = 0; x < size; x++)
            {
                float nx = ((x + 0.5f) / size * 2f - 1f) * extend;
                float r = M.Sqrt(nx * nx + ny * ny);
                int i = y * size + x;

                if (r <= 1.02f)
                {
                    float alpha = M.Clamp01((1f - r) / 0.02f);
                    if (alpha <= 0f) continue;
                    float continents = Fbm(nx * 2.3f + seed * 0.13f, ny * 2.3f, seed, 4);
                    Col surface = Col.Lerp(ocean, land, M.SmoothStep(0.48f, 0.58f, continents));
                    float cap = M.SmoothStep(0.74f, 0.95f, M.Abs(ny));
                    surface = Col.Lerp(surface, new Col(0.92f, 0.96f, 1f), cap * 0.75f);
                    float z = M.Sqrt(M.Max(0f, 1f - M.Min(1f, r * r)));
                    float vl = M.Sqrt(nx * nx + ny * ny + z * z);
                    float lambert = M.Clamp01(M.Dot(nx / vl, ny / vl, z / vl, lx, ly, lz));
                    surface = surface.Mul(0.30f + 0.95f * lambert);
                    surface = Col.Lerp(surface, atmo, M.SmoothStep(0.80f, 1.0f, r) * 0.42f);
                    Over(px, i, surface, alpha);
                }
                else if (r < extend)
                {
                    float g = 1f - (r - 1.0f) / (extend - 1.0f);
                    Over(px, i, atmo, M.Pow(M.Clamp01(g), 2.6f) * 0.55f);
                }
            }
        }
        return px;
    }

    // ---------------- Teo ----------------
    static Col[] Teo(int size)
    {
        var px = Buffer(size);
        Col suit = Col.Hex(0xECF1F7), suitShade = Col.Hex(0xB4C1D1), pack = Col.Hex(0x465166);
        Col visor = Col.Hex(0x142A4A), glass = Col.Hex(0x5EC8F2), trim = Col.Hex(0xFF8A3D);
        FillEllipse(px, size, 0f, -0.10f, 0.52f, 0.46f, pack, 0.12f);
        FillEllipse(px, size, -0.20f, -0.72f, 0.16f, 0.14f, pack, 0.2f);
        FillEllipse(px, size, 0.20f, -0.72f, 0.16f, 0.14f, pack, 0.2f);
        FillEllipse(px, size, -0.44f, -0.16f, 0.15f, 0.22f, suitShade, 0.25f);
        FillEllipse(px, size, 0.44f, -0.16f, 0.15f, 0.22f, suitShade, 0.25f);
        FillEllipse(px, size, 0f, -0.28f, 0.36f, 0.40f, suit, 0.10f);
        FillEllipse(px, size, 0f, -0.42f, 0.30f, 0.10f, trim, 0.4f);
        FillEllipse(px, size, 0f, 0.30f, 0.44f, 0.44f, suit, 0.07f);
        FillEllipse(px, size, 0.03f, 0.29f, 0.31f, 0.29f, visor, 0.12f);
        FillEllipse(px, size, -0.11f, 0.38f, 0.12f, 0.09f, glass, 0.7f);
        FillEllipse(px, size, 0f, -0.22f, 0.08f, 0.08f, glass, 0.6f);
        return px;
    }

    static Col[] Blob(Col color, int seed, int size)
    {
        var px = Buffer(size);
        Col core = Col.Lerp(color, new Col(1, 1, 1), 0.22f);
        for (int y = 0; y < size; y++)
        {
            float ny = (y + 0.5f) / size * 2f - 1f;
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float r = M.Sqrt(nx * nx + ny * ny);
                if (r > 1f) continue;
                float ang = (float)Math.Atan2(ny, nx);
                float wobble = Noise((float)Math.Cos(ang) * 1.7f + 8f, (float)Math.Sin(ang) * 1.7f + 8f, seed);
                float edge = 0.62f + 0.30f * wobble;
                float a = M.Clamp01((edge - r) / 0.10f);
                if (a <= 0f) continue;
                Col c = Col.Lerp(color, core, M.SmoothStep(0.45f, 0.75f, Fbm(nx * 5f, ny * 5f, seed + 3, 3)));
                Over(px, y * size + x, c, a * 0.9f);
            }
        }
        return px;
    }

    // ---------------- sheet + BMP ----------------
    static void Blit(Col[] dst, int dstW, Col[] src, int srcSize, int ox, int oy)
    {
        for (int y = 0; y < srcSize; y++)
            for (int x = 0; x < srcSize; x++)
            {
                int dx = ox + x, dy = oy + y;
                if (dx < 0 || dy < 0 || dx >= dstW || dy >= dstW) continue;
                Over(dst, dy * dstW + dx, src[y * srcSize + x], src[y * srcSize + x].a);
            }
    }


    static Col[] Portrait(Col tint, int size)
    {
        var px = Buffer(size);
        Col backdrop = tint.Mul(0.22f), shoulders = tint.Mul(0.62f), hood = tint.Mul(0.44f);
        Col eyes = new Col(0.06f, 0.06f, 0.10f, 1f);
        FillEllipse(px, size, 0f, 0f, 0.99f, 0.99f, backdrop, 0.03f);
        FillEllipse(px, size, 0f, -0.74f, 0.72f, 0.50f, shoulders, 0.10f);
        FillEllipse(px, size, 0f, 0.26f, 0.46f, 0.40f, hood, 0.09f);
        FillEllipse(px, size, 0f, 0.04f, 0.39f, 0.44f, tint, 0.07f);
        FillEllipse(px, size, -0.15f, 0.06f, 0.055f, 0.075f, eyes, 0.35f);
        FillEllipse(px, size, 0.15f, 0.06f, 0.055f, 0.075f, eyes, 0.35f);
        FillEllipse(px, size, -0.13f, 0.10f, 0.022f, 0.026f, new Col(1,1,1,0.85f), 0.6f);
        FillEllipse(px, size, 0.17f, 0.10f, 0.022f, 0.026f, new Col(1,1,1,0.85f), 0.6f);
        return px;
    }

    static void Main()
    {
        // Top: what the player actually sees through the game camera (30 world units tall)
        // while sitting at Yolkhaven. Bottom: the whole galaxy, to judge the hand-placed layout.
        const int cellW = 1200, camH = 480, mapH = 720, W = 1200;
        var sheet = new Col[W * (camH + mapH)];
        for (int i = 0; i < sheet.Length; i++) sheet[i] = Col.Hex(0x05060E);

        // Camera view: ortho size 15 => 30 units tall, 16:9 => 53 wide. Render square then crop.
        var cam = Space.Render(camH, 0f, 0f, 30f, false);
        BlitRect(sheet, W, cam, camH, (cellW - camH) / 2, mapH);

        var map = Space.Render(mapH, 0f, 60f, 420f, true);
        BlitRect(sheet, W, map, mapH, (cellW - mapH) / 2, 0);

        WriteBmpRect(sheet, W, camH + mapH, "space.bmp");

        // How far apart are the worlds, really?
        float minGap = 1e9f; string pair = "";
        for (int i = 0; i < Space.Worlds.Count; i++)
            for (int j = i + 1; j < Space.Worlds.Count; j++)
            {
                var a = Space.Worlds[i]; var b = Space.Worlds[j];
                var pa = a.SpacePosition; var pb = b.SpacePosition;
                float d = (float)Math.Sqrt((pa.x-pb.x)*(pa.x-pb.x) + (pa.y-pb.y)*(pa.y-pb.y))
                          - a.SpaceRadius - b.SpaceRadius;
                if (d < minGap) { minGap = d; pair = a.Name + "/" + b.Name; }
            }
        Console.WriteLine($"  closest surface-to-surface gap: {pair} at {minGap:0.0} units");
        Console.WriteLine($"  at 22 u/s that is {minGap/22f:0.0}s of flying");
        Console.WriteLine("wrote space.bmp");

        // ---- battle screen, both right-hand menus ----
        foreach (var menu in new[] { "action", "move" })
        {
            var frame = Battle.Render(menu);
            WriteBmpRect(frame, 1920, 1080, "battle-" + menu + ".bmp");
            Console.WriteLine("wrote battle-" + menu + ".bmp");
        }
        // ---- cache worlds: is the buried cache findable on foot, and clear of everything? ----
        Console.WriteLine("buried caches:");
        // Also render the brightest and darkest worlds, to see Teo against both.
        foreach (var id in new[] { "glacierim", "nullreach" })
        {
            var d = Eggverse.PlanetDatabase.Get(id);
            // The camera framing the player actually has: 30 world units tall.
            var pxw = Surface.Render(560, ToHex(d.Ocean), ToHex(d.Land), ToHex(d.Atmosphere),
                                     d.Theme.ToString(), d.Seed, "Scattered", 30f, id);
            WriteBmp(pxw, 560, "teo-" + id + ".bmp");

            // And the same camera standing at the landmark, which is the only place the form,
            // the ground it is on and the decor around it are ever seen together.
            var pxl = Surface.Render(560, ToHex(d.Ocean), ToHex(d.Land), ToHex(d.Atmosphere),
                                     d.Theme.ToString(), d.Seed, "Scattered", 30f, id, true);
            WriteBmp(pxl, 560, "landmark-" + id + ".bmp");

            // Resting: the party out on the pad, doing the warming.
            var pxr = Surface.Render(560, ToHex(d.Ocean), ToHex(d.Land), ToHex(d.Atmosphere),
                                     d.Theme.ToString(), d.Seed, "Scattered", 24f, id, false, true);
            WriteBmp(pxr, 560, "resting-" + id + ".bmp");
        }

        {
            // The six landmark forms, at camera scale, with Teo for size.
            var (lpx, lw, lh) = Landmarks.Render();
            WriteBmpRect(lpx, lw, lh, "landmarks.bmp");
            Console.WriteLine(Landmarks.Coverage());
        }

        {
            // How many turns does a fight last, world by world?
            //
            // Median across every species as the attacker, not one. Measuring with a Sprouteg
            // gave 1.7 turns on Umbralux and 9.9 on Mosswell, which is the type chart talking
            // rather than the pacing - a player brings something suited to where they are.
            foreach (var w in Eggverse.PlanetDatabase.All)
            {
                if (w.Spawns == null || w.Spawns.Length == 0) continue;
                int band = (w.MinLevel + w.MaxLevel) / 2;

                var turns = new System.Collections.Generic.List<float>();
                foreach (var sp in w.Spawns)
                {
                    var foe = Eggverse.EggInstance.Wild(sp.SpeciesId, band);
                    foreach (var attacker in Eggverse.SpeciesDatabase.All)
                    {
                        var mine = Eggverse.EggInstance.Wild(attacker.Id, band);
                        float top = 0f;
                        foreach (var slot in mine.Moves)
                        {
                            float d = Eggverse.BattleCalc.TypicalDamage(mine, foe, slot.Move);
                            if (d > top) top = d;
                        }
                        if (top > 0f) turns.Add(foe.MaxHP / top);
                    }
                }
                turns.Sort();
                float median = turns[turns.Count / 2];
                Console.WriteLine($"  {w.Name,-14} Lv {band,2}  median fight {median:0.0} turns" +
                                  $"  (best {turns[0]:0.0}, worst {turns[turns.Count - 1]:0.0})");
            }
        }

        {
            // How much of a world is shell field, and so how far a player walks between fights?
            foreach (var w in Eggverse.PlanetDatabase.All)
            {
                var fields = Eggverse.SurfaceLayout.Fields(w);
                float area = 0f;
                foreach (var f in fields) area += (float)Math.PI * f.Walkable * f.Walkable;
                float world = (float)Math.PI * w.SurfaceRadius * w.SurfaceRadius;
                float share = area / world;

                // Encounters only tick inside a field, so walking distance per fight is the
                // in-field distance divided by the share of ground that is field.
                float inField = (Eggverse.SurfaceMode.EncounterWalkMin +
                                 Eggverse.SurfaceMode.EncounterWalkMax) * 0.5f;
                float walked = inField / Math.Max(0.001f, share);
                // And the other half: roamers, which you can see coming and walk around.
                int roamers = Eggverse.SurfaceMode.RoamerCount(w);
                float roamPath = world / Math.Max(0.001f, roamers * 2f * Eggverse.SurfaceMode.RoamerNoticeRange);
                float both = 1f / (1f / walked + 1f / roamPath);

                Console.WriteLine($"  {w.Name,-14} {fields.Count} fields ({share * 100f:0}%), {roamers} roamers" +
                                  $"  ->  field {walked / 13f:0.0}s, roamer {roamPath / 13f:0.0}s," +
                                  $" together {both / 13f:0.0}s");
            }
        }

        {
            // What does a catch cost, in cartons, at a sensibly worn-down target?
            foreach (var w in Eggverse.PlanetDatabase.All)
            {
                if (w.Spawns == null || w.Spawns.Length == 0) continue;
                int band = (w.MinLevel + w.MaxLevel) / 2;
                float worst = 0f, best = 99f; string worstName = "";
                foreach (var sp in w.Spawns)
                {
                    var foe = Eggverse.EggInstance.Wild(sp.SpeciesId, band);
                    foe.TakeDamage(foe.MaxHP - Math.Max(1, foe.MaxHP / 4));   // a quarter left
                    float p = Eggverse.BattleCalc.CatchChance(foe);
                    float cartons = 1f / Math.Max(0.0001f, p);
                    if (cartons > worst) { worst = cartons; worstName = foe.Species.Name; }
                    if (cartons < best) best = cartons;
                }
                Console.WriteLine($"  {w.Name,-14} Lv {band,2}  {best:0.0}-{worst:0.0} cartons a catch" +
                                  $"  (hardest {worstName})");
            }
        }

        {
            // How many fights does a level cost, at each stage of the run?
            foreach (var world in new[] { "yolkhaven", "mosswell", "glacierim", "arcmoor", "cairnhold", "amaranth" })
            {
                var w = Eggverse.PlanetDatabase.Get(world);
                int band = (w.MinLevel + w.MaxLevel) / 2;
                var mine = Eggverse.EggInstance.Wild("sprouteg", band);
                var foe = Eggverse.EggInstance.Wild(w.Spawns[0].SpeciesId, band);
                int reward = foe.XpRewardFor();
                int need = mine.XpToNext;
                Console.WriteLine($"  {w.Name,-14} Lv {band,2}  {reward,4} XP a fight, {need,4} to level" +
                                  $"  = {need / (float)reward:0.0} fights");
            }
        }

        {
            // Do any two species fight with the same four moves?
            var sets = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<string>>();
            foreach (var sp in Eggverse.SpeciesDatabase.All)
            {
                var egg = Eggverse.EggInstance.Wild(sp.Id, 30);
                var ids = new System.Collections.Generic.List<string>();
                foreach (var m in egg.Moves) ids.Add(m.Move.Id);
                ids.Sort();
                string key = string.Join(",", ids.ToArray());
                if (!sets.ContainsKey(key)) sets[key] = new System.Collections.Generic.List<string>();
                sets[key].Add(sp.Name);
            }
            int shared = 0;
            foreach (var kv in sets) if (kv.Value.Count > 1) shared++;
            Console.WriteLine($"  movesets at Lv 30: {sets.Count} distinct across " +
                              $"{Eggverse.SpeciesDatabase.All.Count} species, {shared} shared by more than one");
            foreach (var kv in sets)
                if (kv.Value.Count > 1)
                    Console.WriteLine("      moves: " + kv.Key);
            foreach (var kv in sets)
                if (kv.Value.Count > 1)
                    Console.WriteLine("    same four: " + string.Join(", ", kv.Value.ToArray()));
        }

        {
            // How much of the objective panel any beat actually uses.
            int worst = 0; string worstBeat = "";
            for (int b = 0; b < Eggverse.StoryDatabase.Beats.Length; b++)
            {
                var probe = new Eggverse.StoryState();
                probe.RestoreFrom(new string[0], b);
                var st = new Eggverse.GameState(false);
                string panel = Eggverse.HudView.ObjectiveText(
                    probe.Current.Chapter, probe.Current.Objective, probe.CurrentBlockerText(st));
                int rows = 0;
                foreach (var line in panel.Split('\n'))
                {
                    string plain = System.Text.RegularExpressions.Regex.Replace(line, "<[^>]+>", "");
                    rows += Math.Max(1, (int)Math.Ceiling(plain.Length / (524f / (20f * 0.52f))));
                }
                if (rows > worst) { worst = rows; worstBeat = probe.Current.Id; }
            }
            Console.WriteLine($"  objective panel: worst beat is {worstBeat} at {worst} rows " +
                              $"in a box holding {(int)(150f / (20f * 1.16f))}");
        }

        {
            // The plain surface HUD - the screen a player spends most of the game looking at,
            // and the last one nobody had drawn.
            WriteBmpRect(Hud.Render(false), Battle.W, Battle.H, "hud.bmp");
            WriteBmpRect(Hud.Render(false, true), Battle.W, Battle.H, "hud-space.bmp");
            Console.WriteLine("  surface HUD rendered");
        }

        {
            // The naming prompt, mid-type and at the cap. It had never been drawn.
            WriteBmpRect(Naming.Render("Pebbles", true, false), Battle.W, Battle.H, "naming.bmp");
            WriteBmpRect(Naming.Render(new string('W', Eggverse.NameEntryView.MaxLength), true, true),
                         Battle.W, Battle.H, "naming-full.bmp");
            Console.WriteLine(Naming.Report());
        }

        {
            // The dialogue box, at the longest line anybody says and at the moment it is still
            // typing itself out.
            var (dsp, dtx) = Dialogue.Longest();
            WriteBmpRect(Dialogue.Render(dsp, dtx, 1f, false), Battle.W, Battle.H, "dialogue.bmp");
            WriteBmpRect(Dialogue.Render("Amy", "You came all this way.", 0.55f, true),
                         Battle.W, Battle.H, "dialogue-typing.bmp");
            Console.WriteLine(Dialogue.Report());
        }

        {
            // Every face in the game, side by side. They had all been the same one.
            var (cpx, cwd, cht) = Cast.Render();
            WriteBmpRect(cpx, cwd, cht, "cast.bmp");
            Console.WriteLine(Cast.Report());
        }

        {
            // The pause menu, both pages. It had never been drawn.
            WriteBmpRect(Pause.Render(false, 0), Battle.W, Battle.H, "pause.bmp");
            WriteBmpRect(Pause.Render(false, 3), Battle.W, Battle.H, "pause-quit.bmp");
            WriteBmpRect(Pause.Render(true, 1), Battle.W, Battle.H, "pause-settings.bmp");
            Console.WriteLine(Pause.Report());
        }

        {
            // The ending, both ways it can land.
            foreach (var all in new[] { false, true })
            {
                var (epx, ew, eh) = Ending.Render(all);
                WriteBmpRect(epx, ew, eh, all ? "ending-complete.bmp" : "ending.bmp");
            }
            Console.WriteLine("  ending card rendered: 2 variants");
        }

        foreach (var kv in Eggverse.PlanetDatabase.CacheWorlds)
        {
            var def = Eggverse.PlanetDatabase.Get(kv.Key);
            float R = def.SurfaceRadius;

            // The game's own placement, called rather than copied.
            var cpos = Eggverse.SurfaceLayout.CachePosition(def);
            float cx = cpos.x, cy = cpos.y;
            float dist = (float)Math.Sqrt(cx * cx + cy * cy);

            float walkSeconds = dist / 13f;             // WalkSpeed
            Console.WriteLine($"  {def.Name,-12} ({cx,6:0.0},{cy,6:0.0})  {dist,5:0.0} of {R:0} units out"
                              + $"  ~{walkSeconds:0.0}s walk from the pad");

            var px = Surface.Render(560, ToHex(def.Ocean), ToHex(def.Land), ToHex(def.Atmosphere),
                                    def.Theme.ToString(), def.Seed, "Scattered", 0f, def.Id);
            // Mark where the cache sits, at the same scale Surface.Render uses.
            float scale = 560 / (R * 2.25f);
            // Draw it the way the game now does, through the contrast rule rather than a fixed
            // darkening, so the render reflects what the player would actually see.
            var mound = Eggverse.SurfaceMode.AgainstGround(
                new UnityEngine.Color(0.32f, 0.24f, 0.15f), def.Land, 0.55f, 0.46f);
            MarkCache(px, 560, 560 * 0.5f + cx * scale, 560 * 0.5f + cy * scale, 2.4f * 0.5f * scale,
                      new Col(mound.r, mound.g, mound.b, 1f));
            WriteBmp(px, 560, "cache-" + kv.Key + ".bmp");
        }

        WriteBmpRect(Map.Render(), 1920, 1080, "map.bmp");
        Console.WriteLine("wrote map.bmp");
        WriteBmpRect(Title.Render(), 1920, 1080, "title.bmp");
        Console.WriteLine("wrote title.bmp");
        WriteBmpRect(Collection.Render(9, 19), 1920, 1080, "collection.bmp");
        WriteBmpRect(Collection.Render(9, 19, false, false, Eggverse.HudView.NestOrder.Element),
                     1920, 1080, "collection-sorted.bmp");
        WriteBmpRect(Collection.Render(0, 1, true), 1920, 1080, "collection-fresh.bmp");
        WriteBmpRect(Collection.Render(9, 19, false, true), 1920, 1080, "collection-egg.bmp");
        Console.WriteLine("wrote collection-fresh.bmp");
        Console.WriteLine("wrote collection.bmp");
        Battle.Report();
    }


    public static int ToHexPublic(UnityEngine.Color c) => ToHex(c);

    static int ToHex(UnityEngine.Color c) =>
        ((int)(c.r * 255) << 16) | ((int)(c.g * 255) << 8) | (int)(c.b * 255);

    /// Draws the cache the way SurfaceMode does: a darkened mound with a faint warm glint.
    static void MarkCache(Col[] px, int size, float cx, float cy, float rad, Col mound)
    {
        for (int y = Math.Max(0, (int)(cy - rad * 3)); y < Math.Min(size, (int)(cy + rad * 3)); y++)
            for (int x = Math.Max(0, (int)(cx - rad * 3)); x < Math.Min(size, (int)(cx + rad * 3)); x++)
            {
                float d = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d <= rad) Over(px, y * size + x, mound, 0.88f);
                else if (d <= rad * 2.4f)
                    Over(px, y * size + x, new Col(1f, 0.92f, 0.68f, 1f),
                         0.40f * (1f - (d - rad) / (rad * 1.4f)));
            }
    }

    static void BlitRect(Col[] dst, int dstW, Col[] src, int srcSize, int ox, int oy)
    {
        for (int y = 0; y < srcSize; y++)
            for (int x = 0; x < srcSize; x++)
            {
                int dx = ox + x, dy = oy + y;
                if (dx < 0 || dy < 0 || dx >= dstW) continue;
                int idx = dy * dstW + dx;
                if (idx < 0 || idx >= dst.Length) continue;
                dst[idx] = src[y * srcSize + x];
            }
    }

    static void WriteBmpRect(Col[] px, int w, int h, string path)
    {
        int rowBytes = w * 3, pad = (4 - rowBytes % 4) % 4, dataSize = (rowBytes + pad) * h;
        using var fs = new FileStream(path, FileMode.Create);
        using var bw = new BinaryWriter(fs);
        bw.Write((byte)'B'); bw.Write((byte)'M');
        bw.Write(54 + dataSize); bw.Write(0); bw.Write(54);
        bw.Write(40); bw.Write(w); bw.Write(h);
        bw.Write((short)1); bw.Write((short)24); bw.Write(0); bw.Write(dataSize);
        bw.Write(2835); bw.Write(2835); bw.Write(0); bw.Write(0);
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                Col c = px[y * w + x];
                bw.Write((byte)(M.Clamp01(c.b) * 255f));
                bw.Write((byte)(M.Clamp01(c.g) * 255f));
                bw.Write((byte)(M.Clamp01(c.r) * 255f));
            }
            for (int i = 0; i < pad; i++) bw.Write((byte)0);
        }
    }

    static void WriteBmp(Col[] px, int size, string path)
    {
        int rowBytes = size * 3;
        int pad = (4 - rowBytes % 4) % 4;
        int dataSize = (rowBytes + pad) * size;
        using var fs = new FileStream(path, FileMode.Create);
        using var w = new BinaryWriter(fs);
        w.Write((byte)'B'); w.Write((byte)'M');
        w.Write(54 + dataSize); w.Write(0); w.Write(54);
        w.Write(40); w.Write(size); w.Write(size);
        w.Write((short)1); w.Write((short)24); w.Write(0); w.Write(dataSize);
        w.Write(2835); w.Write(2835); w.Write(0); w.Write(0);
        for (int y = 0; y < size; y++)   // BMP rows are bottom-up, matching our y-up buffer
        {
            for (int x = 0; x < size; x++)
            {
                Col c = px[y * size + x];
                w.Write((byte)(M.Clamp01(c.b) * 255f));
                w.Write((byte)(M.Clamp01(c.g) * 255f));
                w.Write((byte)(M.Clamp01(c.r) * 255f));
            }
            for (int p = 0; p < pad; p++) w.Write((byte)0);
        }
    }
}
