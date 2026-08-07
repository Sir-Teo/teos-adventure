using System.Collections.Generic;
using UnityEngine;

namespace Eggverse
{
    /// <summary>
    /// Every visual in the game is generated here at runtime, so the project needs no art assets.
    /// Sprites are cached by key; textures are created once and reused.
    /// </summary>
    public static class ProcArt
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static Material spriteMaterial;

        public static Material SpriteMaterial
        {
            get
            {
                if (spriteMaterial == null)
                {
                    // The project renders through URP's 2D renderer; fall back progressively
                    // so a missing shader degrades rather than throwing on every sprite.
                    Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                    if (sh == null) sh = Shader.Find("Sprites/Default");
                    if (sh == null) sh = Shader.Find("Unlit/Transparent");
                    if (sh == null)
                    {
                        Debug.LogError("Eggverse: no sprite shader found — world art will not render. " +
                                       "Expected URP's 2D Sprite-Unlit-Default.");
                        return null;
                    }
                    spriteMaterial = new Material(sh) { name = "EggverseSprite" };
                }
                return spriteMaterial;
            }
        }

        // ------------------------------------------------------------------
        // noise helpers
        // ------------------------------------------------------------------

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
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float u = xf * xf * (3f - 2f * xf);
            float v = yf * yf * (3f - 2f * yf);
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed);
            float c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        static float Fbm(float x, float y, int seed, int octaves = 4)
        {
            float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x * freq, y * freq, seed + i * 71) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.03f;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        // ------------------------------------------------------------------
        // pixel helpers
        // ------------------------------------------------------------------

        static void Over(Color[] px, int i, Color src, float alpha)
        {
            if (alpha <= 0f) return;
            alpha = Mathf.Clamp01(alpha * src.a);
            Color dst = px[i];
            float outA = alpha + dst.a * (1f - alpha);
            if (outA <= 0.0001f) { px[i] = new Color(0, 0, 0, 0); return; }
            float r = (src.r * alpha + dst.r * dst.a * (1f - alpha)) / outA;
            float g = (src.g * alpha + dst.g * dst.a * (1f - alpha)) / outA;
            float b = (src.b * alpha + dst.b * dst.a * (1f - alpha)) / outA;
            px[i] = new Color(r, g, b, outA);
        }

        static void FillEllipse(Color[] px, int size, float cx, float cy, float rx, float ry, Color col, float soft = 0.05f)
        {
            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float dx = (nx - cx) / Mathf.Max(0.0001f, rx);
                    float dy = (ny - cy) / Mathf.Max(0.0001f, ry);
                    float t = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((1f - t) / Mathf.Max(0.0001f, soft));
                    Over(px, y * size + x, col, a);
                }
            }
        }

        static Sprite Finish(string key, Color[] px, int w, int h, float ppu)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "eggverse_" + key,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            tex.SetPixels(px);
            tex.Apply();
            var sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
            sprite.name = key;
            cache[key] = sprite;
            return sprite;
        }

        static Color[] NewBuffer(int size)
        {
            var px = new Color[size * size];
            var clear = new Color(0, 0, 0, 0);
            for (int i = 0; i < px.Length; i++) px[i] = clear;
            return px;
        }

        static Color Mul(Color c, float m) => new Color(c.r * m, c.g * m, c.b * m, c.a);

        // ------------------------------------------------------------------
        // eggs
        // ------------------------------------------------------------------

        /// <summary>
        /// How strongly the shell marking shows at this point, and what it blends toward.
        /// Returns 0..1; `towardLight` picks a pale marking instead of the accent colour.
        /// </summary>
        static float PatternAmount(EggPattern pattern, float nx, float ny, float t, int seed, out bool towardLight)
        {
            towardLight = false;
            switch (pattern)
            {
                case EggPattern.Mottled:
                    return Mathf.SmoothStep(0.42f, 0.62f, Fbm(nx * 2.1f + seed * 0.2f, ny * 2.1f, seed, 3)) * 0.70f;

                case EggPattern.Banded:
                {
                    float wobble = Fbm(nx * 1.8f, ny * 1.8f, seed, 2) * 1.1f;
                    float v = 0.5f + 0.5f * Mathf.Sin((ny * 3.1f + seed * 0.11f) * Mathf.PI * 2f + wobble);
                    return Mathf.SmoothStep(0.52f, 0.78f, v) * 0.78f;
                }

                case EggPattern.Striped:
                {
                    float wobble = Fbm(nx * 2.2f, ny * 1.4f, seed, 2) * 0.9f;
                    float v = 0.5f + 0.5f * Mathf.Sin((nx * 3.6f + ny * 0.7f + seed * 0.09f) * Mathf.PI * 2f + wobble);
                    return Mathf.SmoothStep(0.55f, 0.80f, v) * 0.74f;
                }

                case EggPattern.Swirled:
                {
                    float angle = Mathf.Atan2(ny, nx);
                    float radius = Mathf.Sqrt(nx * nx + ny * ny);
                    // The angle multiplier must be a whole number, otherwise the swirl does not
                    // meet itself across the atan2 branch cut and leaves a hard seam.
                    float v = 0.5f + 0.5f * Mathf.Sin(angle * 3f + radius * 7.5f + seed * 0.2f);
                    return Mathf.SmoothStep(0.48f, 0.74f, v) * 0.68f;
                }

                case EggPattern.Starry:
                    towardLight = true;
                    return Mathf.SmoothStep(0.72f, 0.82f, Fbm(nx * 9f + seed * 0.4f, ny * 9f, seed, 2)) * 0.95f;

                case EggPattern.Cracked:
                {
                    // Ridged noise makes thin veins rather than blobs.
                    float ridged = 1f - Mathf.Abs(2f * Fbm(nx * 3.0f + seed * 0.15f, ny * 3.0f, seed, 3) - 1f);
                    return Mathf.SmoothStep(0.87f, 0.98f, ridged) * 0.88f;
                }

                case EggPattern.Glossy:
                {
                    // Deliberately calm: one broad sheen band, so it reads as polished
                    // rather than patterned, and gives the eye a rest next to the busy shells.
                    float v = 0.5f + 0.5f * Mathf.Sin((ny * 1.15f + nx * 0.35f + seed * 0.07f) * Mathf.PI);
                    return Mathf.SmoothStep(0.32f, 0.88f, v) * 0.34f;
                }

                default: // Speckled
                    return Mathf.SmoothStep(0.56f, 0.68f, Fbm(nx * 4.6f + seed * 0.31f, ny * 4.6f, seed, 3)) * 0.62f;
            }
        }

        public static Sprite Egg(SpeciesDef species, int size = 128, float ppu = 96f)
        {
            string key = "egg_" + species.Id + "_" + size;
            Sprite cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;

            var px = NewBuffer(size);
            Vector3 light = new Vector3(-0.45f, 0.62f, 0.64f).normalized;
            Color rim = Mul(species.Accent, 0.55f);

            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;

                    // Egg silhouette: narrower toward the top.
                    float halfW = 0.66f * (1f - 0.22f * ny);
                    float halfH = 0.92f;
                    float ex = nx / halfW, ey = ny / halfH;
                    float f = ex * ex + ey * ey;
                    float t = Mathf.Sqrt(f);
                    if (t > 1.06f) continue;

                    float alpha = Mathf.Clamp01((1f - t) / 0.045f);
                    if (alpha <= 0f) continue;

                    // Fake sphere normal for shading.
                    float z = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Min(1f, f)));
                    Vector3 n = new Vector3(ex * 0.92f, ey * 0.92f, z + 0.15f).normalized;
                    float lambert = Mathf.Clamp01(Vector3.Dot(n, light));
                    float shade = 0.38f + 0.78f * lambert;

                    Color c = Mul(species.Body, shade);

                    // Shell markings, in whichever style this species wears.
                    bool towardLight;
                    float markAmount = PatternAmount(species.Pattern, nx, ny, t, species.ArtSeed, out towardLight);
                    Color markColor = towardLight
                        ? Mul(Color.Lerp(species.Body, Color.white, 0.75f), shade)
                        : Mul(species.Accent, shade * 0.95f);
                    c = Color.Lerp(c, markColor, markAmount);

                    // Darker band around the rim reads as thickness.
                    float rimAmt = Mathf.SmoothStep(0.80f, 1.0f, t) * 0.55f;
                    c = Color.Lerp(c, rim, rimAmt);

                    Over(px, y * size + x, c, alpha);
                }
            }

            // Specular highlight and a soft bounce light from below.
            FillEllipse(px, size, -0.24f, 0.40f, 0.17f, 0.23f, new Color(1f, 1f, 1f, 0.45f), 0.9f);
            FillEllipse(px, size, 0.16f, -0.62f, 0.28f, 0.13f, new Color(1f, 1f, 1f, 0.13f), 1f);

            return Finish(key, px, size, size, ppu);
        }

        // ------------------------------------------------------------------
        // planets
        // ------------------------------------------------------------------

        public static Sprite Planet(PlanetDef planet, int size = 384, float ppu = 64f)
        {
            string key = "planet_" + planet.Id + "_" + size;
            Sprite cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;

            var px = NewBuffer(size);
            const float extend = 1.32f;   // leaves room for the atmosphere glow
            int seed = planet.Seed & 0xFFFF;   // stable across runs, so a world keeps its face
            Vector3 light = new Vector3(-0.5f, 0.55f, 0.67f).normalized;

            for (int y = 0; y < size; y++)
            {
                float ny = ((y + 0.5f) / size * 2f - 1f) * extend;
                for (int x = 0; x < size; x++)
                {
                    float nx = ((x + 0.5f) / size * 2f - 1f) * extend;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    int i = y * size + x;

                    if (r <= 1.02f)
                    {
                        float alpha = Mathf.Clamp01((1f - r) / 0.02f);
                        if (alpha <= 0f) continue;

                        float continents = Fbm(nx * 2.3f + seed * 0.13f, ny * 2.3f, seed, 4);
                        float landAmt = Mathf.SmoothStep(0.48f, 0.58f, continents);
                        Color surface = Color.Lerp(planet.Ocean, planet.Land, landAmt);

                        // Ice caps.
                        float cap = Mathf.SmoothStep(0.74f, 0.95f, Mathf.Abs(ny));
                        surface = Color.Lerp(surface, new Color(0.92f, 0.96f, 1f), cap * 0.75f);

                        float z = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Min(1f, r * r)));
                        Vector3 n = new Vector3(nx, ny, z).normalized;
                        float lambert = Mathf.Clamp01(Vector3.Dot(n, light));
                        surface = Mul(surface, 0.30f + 0.95f * lambert);

                        // Atmosphere haze hugging the limb.
                        float limb = Mathf.SmoothStep(0.80f, 1.0f, r);
                        surface = Color.Lerp(surface, planet.Atmosphere, limb * 0.42f);

                        Over(px, i, surface, alpha);
                    }
                    else if (r < extend)
                    {
                        float g = 1f - Mathf.InverseLerp(1.0f, extend, r);
                        float a = Mathf.Pow(Mathf.Clamp01(g), 2.6f) * 0.55f;
                        Over(px, i, planet.Atmosphere, a);
                    }
                }
            }

            return Finish(key, px, size, size, ppu);
        }

        // ------------------------------------------------------------------
        // Teo
        // ------------------------------------------------------------------

        /// <summary>Teo's two defining tones. Public so the self-check can hold them against
        /// every world's ground without duplicating the numbers.</summary>
        public static readonly Color TeoSuit = new Color32(0xEC, 0xF1, 0xF7, 0xFF);
        public static readonly Color TeoOutline = new Color32(0x14, 0x18, 0x24, 0xFF);

        public static Sprite Teo(int size = 128, float ppu = 96f)
        {
            string key = "teo_" + size;
            Sprite cached;
            if (cache.TryGetValue(key, out cached) && cached != null) return cached;

            var px = NewBuffer(size);
            Color suit = TeoSuit;
            Color suitShade = new Color32(0xB4, 0xC1, 0xD1, 0xFF);
            Color pack = new Color32(0x46, 0x51, 0x66, 0xFF);
            Color visor = new Color32(0x14, 0x2A, 0x4A, 0xFF);
            Color glass = new Color32(0x5E, 0xC8, 0xF2, 0xFF);
            Color trim = new Color32(0xFF, 0x8A, 0x3D, 0xFF);

            // A dark silhouette a shade larger than the figure, drawn first so everything else
            // sits on top of it. Teo is a white suit: against Glacierim's ice his luminance gap
            // was 0.064, which is less separation than anything else in the game is allowed, and
            // four more worlds sat under 0.25. Tinting him per planet would have fixed it and
            // made the protagonist a different colour on every world; an outline keeps him
            // himself and only shows where it is needed - on a dark world it disappears into
            // the ground, which is exactly where he already stands out.
            Color outline = TeoOutline;
            const float O = 0.055f;   // how far the outline extends past the silhouette
            FillEllipse(px, size, 0f, -0.10f, 0.52f + O, 0.46f + O, outline, 0.12f);
            FillEllipse(px, size, -0.20f, -0.72f, 0.16f + O, 0.14f + O, outline, 0.2f);
            FillEllipse(px, size, 0.20f, -0.72f, 0.16f + O, 0.14f + O, outline, 0.2f);
            FillEllipse(px, size, -0.44f, -0.16f, 0.15f + O, 0.22f + O, outline, 0.25f);
            FillEllipse(px, size, 0.44f, -0.16f, 0.15f + O, 0.22f + O, outline, 0.25f);
            FillEllipse(px, size, 0f, -0.28f, 0.36f + O, 0.40f + O, outline, 0.10f);
            FillEllipse(px, size, 0f, 0.30f, 0.44f + O, 0.44f + O, outline, 0.07f);

            FillEllipse(px, size, 0f, -0.10f, 0.52f, 0.46f, pack, 0.12f);          // jetpack
            FillEllipse(px, size, -0.20f, -0.72f, 0.16f, 0.14f, pack, 0.2f);       // boots
            FillEllipse(px, size, 0.20f, -0.72f, 0.16f, 0.14f, pack, 0.2f);
            FillEllipse(px, size, -0.44f, -0.16f, 0.15f, 0.22f, suitShade, 0.25f); // arms
            FillEllipse(px, size, 0.44f, -0.16f, 0.15f, 0.22f, suitShade, 0.25f);
            FillEllipse(px, size, 0f, -0.28f, 0.36f, 0.40f, suit, 0.10f);          // torso
            FillEllipse(px, size, 0f, -0.42f, 0.30f, 0.10f, trim, 0.4f);           // belt
            FillEllipse(px, size, 0f, 0.30f, 0.44f, 0.44f, suit, 0.07f);           // helmet
            FillEllipse(px, size, 0.03f, 0.29f, 0.31f, 0.29f, visor, 0.12f);       // visor
            FillEllipse(px, size, -0.11f, 0.38f, 0.12f, 0.09f, glass, 0.7f);       // glint
            FillEllipse(px, size, 0f, -0.22f, 0.08f, 0.08f, glass, 0.6f);          // chest light

            return Finish(key, px, size, size, ppu);
        }

        // ------------------------------------------------------------------
        // character portraits
        // ------------------------------------------------------------------

        /// <summary>A small bust used in dialogue: hood, head, shoulders, two eyes.</summary>
        public static Sprite Portrait(string key, Color tint, int size = 160, float ppu = 96f)
        {
            string k = "portrait_" + key + "_" + size;
            Sprite cached;
            if (cache.TryGetValue(k, out cached) && cached != null) return cached;

            var px = NewBuffer(size);
            Color backdrop = Mul(tint, 0.22f);
            Color shoulders = Mul(tint, 0.62f);
            Color hood = Mul(tint, 0.44f);
            Color eyes = new Color(0.06f, 0.06f, 0.10f, 1f);

            FillEllipse(px, size, 0f, 0f, 0.99f, 0.99f, backdrop, 0.03f);
            FillEllipse(px, size, 0f, -0.74f, 0.72f, 0.50f, shoulders, 0.10f);
            FillEllipse(px, size, 0f, 0.26f, 0.46f, 0.40f, hood, 0.09f);
            FillEllipse(px, size, 0f, 0.04f, 0.39f, 0.44f, tint, 0.07f);
            FillEllipse(px, size, -0.15f, 0.06f, 0.055f, 0.075f, eyes, 0.35f);
            FillEllipse(px, size, 0.15f, 0.06f, 0.055f, 0.075f, eyes, 0.35f);
            FillEllipse(px, size, -0.13f, 0.10f, 0.022f, 0.026f, new Color(1f, 1f, 1f, 0.85f), 0.6f);
            FillEllipse(px, size, 0.17f, 0.10f, 0.022f, 0.026f, new Color(1f, 1f, 1f, 0.85f), 0.6f);

            return Finish(k, px, size, size, ppu);
        }

        // ------------------------------------------------------------------
        // generic shapes
        // ------------------------------------------------------------------

        /// <summary>Soft radial disc, used for glows and markers.</summary>
        public static Sprite Disc(string key, Color inner, Color outer, float falloff = 1f, int size = 128, float ppu = 64f)
        {
            string k = "disc_" + key + "_" + size;
            Sprite cached;
            if (cache.TryGetValue(k, out cached) && cached != null) return cached;

            var px = NewBuffer(size);
            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    if (r > 1f) continue;
                    Color c = Color.Lerp(inner, outer, Mathf.Pow(r, falloff));
                    float a = Mathf.Clamp01((1f - r) / 0.04f) * c.a;
                    Over(px, y * size + x, new Color(c.r, c.g, c.b, 1f), a);
                }
            }
            return Finish(k, px, size, size, ppu);
        }

        public static Sprite Ring(string key, Color color, float thickness = 0.10f, int size = 256, float ppu = 64f)
        {
            string k = "ring_" + key + "_" + size;
            Sprite cached;
            if (cache.TryGetValue(k, out cached) && cached != null) return cached;

            var px = NewBuffer(size);
            float outerR = 0.98f, innerR = Mathf.Max(0.02f, outerR - thickness);
            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    float a = Mathf.Clamp01((outerR - r) / 0.02f) * Mathf.Clamp01((r - innerR) / 0.02f);
                    if (a <= 0f) continue;
                    Over(px, y * size + x, color, a * color.a);
                }
            }
            return Finish(k, px, size, size, ppu);
        }

        /// <summary>Irregular patch used for the shell fields that hide wild eggs.</summary>
        public static Sprite Blob(string key, Color color, int seed, int size = 128, float ppu = 32f)
        {
            string k = "blob_" + key + "_" + seed + "_" + size;
            Sprite cached;
            if (cache.TryGetValue(k, out cached) && cached != null) return cached;

            var px = NewBuffer(size);
            Color core = Color.Lerp(color, Color.white, 0.22f);
            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    if (r > 1f) continue;
                    float ang = Mathf.Atan2(ny, nx);
                    float wobble = Noise(Mathf.Cos(ang) * 1.7f + 8f, Mathf.Sin(ang) * 1.7f + 8f, seed);
                    float edge = 0.62f + 0.30f * wobble;
                    float a = Mathf.Clamp01((edge - r) / 0.10f);
                    if (a <= 0f) continue;
                    float tuft = Fbm(nx * 5f, ny * 5f, seed + 3, 3);
                    Color c = Color.Lerp(color, core, Mathf.SmoothStep(0.45f, 0.75f, tuft));
                    Over(px, y * size + x, c, a * 0.9f);
                }
            }
            return Finish(k, px, size, size, ppu);
        }

        public static Sprite Star(int size = 16, float ppu = 64f)
        {
            string k = "star_" + size;
            Sprite cached;
            if (cache.TryGetValue(k, out cached) && cached != null) return cached;

            var px = NewBuffer(size);
            for (int y = 0; y < size; y++)
            {
                float ny = (y + 0.5f) / size * 2f - 1f;
                for (int x = 0; x < size; x++)
                {
                    float nx = (x + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny);
                    if (r > 1f) continue;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                    Over(px, y * size + x, Color.white, a);
                }
            }
            return Finish(k, px, size, size, ppu);
        }

        /// <summary>Flat white sprite for UI fills and bars.</summary>
        public static Sprite White
        {
            get
            {
                Sprite cached;
                if (cache.TryGetValue("white", out cached) && cached != null) return cached;
                var px = new Color[16];
                for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                return Finish("white", px, 4, 4, 4f);
            }
        }
    }
}
