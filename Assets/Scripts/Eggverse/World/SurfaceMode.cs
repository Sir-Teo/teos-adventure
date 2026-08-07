using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Eggverse
{
    /// <summary>Walking around on a landed planet: shell fields, roaming eggs, and the nest station.</summary>
    public class SurfaceMode : MonoBehaviour
    {
        class Roamer
        {
            public Transform Root;
            public SpriteRenderer Sprite;
            public string SpeciesId;
            public int Level;
            public Vector2 Heading;
            public float TurnTimer;
            public float RespawnTimer;
            public float Bob;
            public bool Alive;
            public bool IsElder;
            public SpriteRenderer Aura;
        }

        class WorldLabel
        {
            public Transform Anchor;
            public Vector2 Offset;
            public RectTransform Rect;
            public Text Text;
        }

        class NpcView
        {
            public NpcDef Def;
            public Transform Root;
            public Transform Visual;
            public float Bob;
        }

        GameDirector dir;
        Transform root;
        Canvas labelCanvas;
        RectTransform labelCanvasRect;

        PlanetDef current;
        readonly List<Transform> fields = new List<Transform>();
        readonly List<float> fieldRadii = new List<float>();
        readonly List<Roamer> roamers = new List<Roamer>();
        readonly List<WorldLabel> labels = new List<WorldLabel>();
        readonly List<NpcView> npcs = new List<NpcView>();

        Transform nestStation;
        Transform cache;
        Vector2 cachePos;
        Transform amyFigure;

        float fieldDistance;
        float nextEncounterDistance = 10f;
        float encounterCooldown;
        int engagedRoamer = -1;

        const float NestRange = 3.4f;
        const float RoamerSpeed = 3.4f;
        const float RoamerTouchRange = 1.15f;

        public PlanetDef Current => current;

        public void Build(GameDirector director)
        {
            dir = director;
            root = new GameObject("Surface").transform;
            root.SetParent(transform, false);
            labelCanvas = UIKit.CreateCanvas("SurfaceLabels", 5, transform);
            labelCanvasRect = (RectTransform)labelCanvas.transform;
        }

        public void SetActive(bool active)
        {
            if (root != null) root.gameObject.SetActive(active);
            if (labelCanvas != null) labelCanvas.gameObject.SetActive(active);
        }

        // ------------------------------------------------------------------
        // generation
        // ------------------------------------------------------------------

        public void Enter(PlanetDef planet)
        {
            current = planet;
            Clear();

            int seed = planet.Seed;
            var rng = new System.Random(seed);
            float R = planet.SurfaceRadius;

            BuildGround(planet, rng, R);
            if (!planet.IsBossWorld) BuildShellFields(planet, rng, R);
            BuildNestStation(planet);
            BuildCache(planet, R);
            if (planet.IsBossWorld) BuildAmy(planet);
            else BuildRoamers(planet, rng, R);
            BuildNpcs(planet);

            fieldDistance = 0f;
            nextEncounterDistance = Random.Range(7f, 15f);
            encounterCooldown = 1.0f;
            engagedRoamer = -1;
        }

        void Clear()
        {
            for (int i = root.childCount - 1; i >= 0; i--) Destroy(root.GetChild(i).gameObject);
            for (int i = 0; i < labels.Count; i++) if (labels[i].Rect != null) Destroy(labels[i].Rect.gameObject);
            labels.Clear();
            fields.Clear();
            fieldRadii.Clear();
            roamers.Clear();
            npcs.Clear();
            nestStation = null;
            amyFigure = null;
            cache = null;
        }

        SpriteRenderer Spawn(string name, Sprite sprite, Vector2 pos, float diameter, Color tint, int order, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent != null ? parent : root, false);
            // Local, not world: decorations parented to an NPC or to Amy must follow them.
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.material = ProcArt.SpriteMaterial;
            sr.color = tint;
            sr.sortingOrder = order;
            float spriteWorld = sprite.rect.width / sprite.pixelsPerUnit;
            go.transform.localScale = Vector3.one * (diameter / spriteWorld);
            return sr;
        }

        void BuildGround(PlanetDef planet, System.Random rng, float R)
        {
            Color deep = Color.Lerp(planet.Ocean, Color.black, 0.25f);
            Spawn("Ground", ProcArt.Disc("ground_" + planet.Id, planet.Land, deep, 1.4f, 256, 64f),
                  Vector2.zero, R * 2f, Color.white, -60);

            // Terrain mottling so the disc does not read as a flat circle.
            for (int i = 0; i < 46; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = Mathf.Sqrt((float)rng.NextDouble()) * R * 0.97f;
                var pos = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                float size = Mathf.Lerp(3f, 11f, (float)rng.NextDouble());
                bool lighter = rng.NextDouble() > 0.5;
                Color tint = Color.Lerp(planet.Land, lighter ? Color.white : Color.black, 0.16f);
                tint.a = 0.5f;
                Spawn("patch", ProcArt.Blob("terrain", Color.white, (i % 6) * 7 + 3), pos, size, tint, -55);
            }

            BuildBiomeDecor(planet, rng, R);

            // Horizon ring.
            var edge = Spawn("Edge", ProcArt.Ring("horizon", Color.white, 0.02f), Vector2.zero, R * 2f,
                             new Color(planet.Atmosphere.r, planet.Atmosphere.g, planet.Atmosphere.b, 0.55f), -35);
            edge.sortingOrder = -35;

            var glow = Spawn("EdgeGlow", ProcArt.Disc("edgeglow", new Color(0, 0, 0, 0), planet.Atmosphere, 3.2f, 128, 64f),
                             Vector2.zero, R * 2.25f, new Color(1f, 1f, 1f, 0.30f), -58);
            glow.sortingOrder = -58;
        }

        /// <summary>Scatters props that belong to this world's element, so biomes read apart at a glance.</summary>
        void BuildBiomeDecor(PlanetDef planet, System.Random rng, float R)
        {
            Color themeColor = TypeChart.ColorOf(planet.Theme);
            int count = 34;

            for (int i = 0; i < count; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float d = Mathf.Sqrt((float)rng.NextDouble()) * R * 0.94f;
                var pos = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                float roll = (float)rng.NextDouble();
                float scale = Mathf.Lerp(0.8f, 1.6f, (float)rng.NextDouble());

                switch (planet.Theme)
                {
                    case EggType.Verdant:
                    {
                        // Trunk plus canopy.
                        var trunk = Spawn("trunk", ProcArt.Disc("trunk", new Color(0.34f, 0.24f, 0.15f), new Color(0.22f, 0.15f, 0.09f), 1f, 64, 64f),
                                          pos + new Vector2(0f, -0.55f * scale), 1f, Color.white, -42);
                        trunk.transform.localScale = new Vector3(0.18f * scale, 0.62f * scale, 1f);
                        Spawn("canopy", ProcArt.Blob("canopy", Color.white, (i % 6) * 13 + 5), pos + new Vector2(0f, 0.35f * scale),
                              2.3f * scale, Color.Lerp(planet.Land, Color.black, 0.34f), -41);
                        break;
                    }

                    case EggType.Molten:
                        if (roll < 0.45f)
                        {
                            // A glowing vent.
                            Spawn("ventglow", ProcArt.Disc("vent", new Color(1f, 0.62f, 0.20f), new Color(1f, 0.25f, 0f, 0f), 1.7f, 64, 64f),
                                  pos, 3.4f * scale, new Color(1f, 1f, 1f, 0.55f), -44);
                            Spawn("vent", ProcArt.Blob("vent", Color.white, (i % 5) * 7 + 2), pos, 1.1f * scale,
                                  new Color(1f, 0.55f, 0.18f, 0.95f), -41);
                        }
                        else
                        {
                            Spawn("cinder", ProcArt.Blob("cinder", Color.white, (i % 5) * 11 + 3), pos, 1.3f * scale,
                                  new Color(0.16f, 0.11f, 0.10f, 0.9f), -41);
                        }
                        break;

                    case EggType.Tidal:
                    {
                        var pool = Spawn("pool", ProcArt.Blob("pool", Color.white, (i % 5) * 9 + 4), pos, 3.2f * scale,
                                         new Color(0.42f, 0.76f, 0.95f, 0.42f), -44);
                        pool.transform.localScale = new Vector3(pool.transform.localScale.x, pool.transform.localScale.y * 0.55f, 1f);
                        if (roll < 0.4f)
                            Spawn("reed", ProcArt.Disc("reed", new Color(0.35f, 0.62f, 0.45f), new Color(0.2f, 0.4f, 0.3f), 1f, 32, 64f),
                                  pos + new Vector2(0.4f, 0.5f), 1.1f * scale, Color.white, -41)
                                .transform.localScale = new Vector3(0.12f * scale, 0.8f * scale, 1f);
                        break;
                    }

                    case EggType.Volt:
                    {
                        // Charged spires with a halo.
                        var spire = Spawn("spire", ProcArt.Disc("spire", themeColor, Color.Lerp(themeColor, Color.black, 0.6f), 1f, 64, 64f),
                                          pos, 1.6f * scale, Color.white, -41);
                        spire.transform.localScale = new Vector3(0.22f * scale, 1.0f * scale, 1f);
                        Spawn("spark", ProcArt.Disc("spark", themeColor, new Color(themeColor.r, themeColor.g, themeColor.b, 0f), 1.8f, 64, 64f),
                              pos + new Vector2(0f, 0.7f * scale), 2.2f * scale, new Color(1f, 1f, 1f, 0.4f), -43);
                        break;
                    }

                    case EggType.Frost:
                    {
                        Color shardTint = AgainstGround(new Color(0.72f, 0.88f, 1f), planet.Land, 0.6f, 0.40f);
                        shardTint.a = 0.95f;
                        var shard = Spawn("shard", ProcArt.Disc("shard", new Color(0.86f, 0.96f, 1f), new Color(0.55f, 0.78f, 0.9f), 1f, 64, 64f),
                                          pos, 1.8f * scale, shardTint, -41);
                        shard.transform.localScale = new Vector3(0.30f * scale, 0.95f * scale, 1f);
                        shard.transform.localRotation = Quaternion.Euler(0f, 0f, ((float)rng.NextDouble() - 0.5f) * 34f);
                        break;
                    }

                    case EggType.Stone:
                        Spawn("boulder", ProcArt.Blob("boulder", Color.white, (i % 6) * 13 + 5), pos, 1.7f * scale,
                              Color.Lerp(planet.Ocean, Color.black, 0.22f), -41);
                        break;

                    case EggType.Aether:
                    {
                        Color orbTint = AgainstGround(new Color(0.92f, 0.76f, 1f), planet.Land, 0.7f, 0.35f);
                        orbTint.a = 0.72f;
                        Spawn("orb", ProcArt.Disc("orb", new Color(0.92f, 0.76f, 1f), new Color(0.55f, 0.25f, 0.85f, 0f), 1.5f, 64, 64f),
                              pos, 2.0f * scale, orbTint, -41);
                        break;
                    }

                    case EggType.Void:
                    {
                        var rift = Spawn("rift", ProcArt.Blob("rift", Color.white, (i % 5) * 17 + 6), pos, 2.4f * scale,
                                         new Color(0.03f, 0.02f, 0.08f, 0.88f), -41);
                        rift.transform.localScale = new Vector3(rift.transform.localScale.x, rift.transform.localScale.y * 0.5f, 1f);
                        rift.transform.localRotation = Quaternion.Euler(0f, 0f, (float)rng.NextDouble() * 180f);
                        break;
                    }

                    default:
                        Spawn("rock", ProcArt.Blob("rock", Color.white, (i % 6) * 13 + 5), pos, 1.3f * scale,
                              Color.Lerp(planet.Ocean, Color.black, 0.3f), -41);
                        break;
                }
            }
        }

        /// <summary>How a world arranges its shell fields. Derived from the planet seed, so
        /// each one is consistent between visits but no two feel like the same walk.</summary>
        enum FieldLayout { Scattered, Ring, Clustered }

        /// <summary>
        /// Relative luminance, for deciding whether something must be lightened or darkened
        /// to stand out against the ground it sits on.
        /// </summary>
        public static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>
        /// Tints toward an element's colour, then forces separation from the ground. Blending
        /// the theme colour into the land alone is not enough: on a Frost world the ice and the
        /// shell fields are both pale blue, and the patches where eggs hide become invisible.
        /// </summary>
        public static Color AgainstGround(Color tint, Color ground, float tintAmount, float separation)
        {
            Color c = Color.Lerp(ground, tint, tintAmount);
            // Push away from the ground's luminance, in whichever direction has room.
            Color target = Luminance(ground) > 0.5f ? Color.black : Color.white;
            return Color.Lerp(c, target, separation);
        }

        void BuildShellFields(PlanetDef planet, System.Random rng, float R)
        {
            Color fieldColor = AgainstGround(TypeChart.ColorOf(planet.Theme), planet.Land, 0.55f, 0.30f);
            fieldColor.a = 0.85f;

            var layout = (FieldLayout)(planet.Seed % 3);
            int fieldCount = 7 + (planet.Seed / 7) % 5;   // 7..11

            // Clustered worlds seed a few clumps and hang fields off them.
            int clumps = 2 + (planet.Seed / 13) % 2;
            var clumpCentres = new Vector2[clumps];
            for (int c = 0; c < clumps; c++)
            {
                float ca = (float)rng.NextDouble() * Mathf.PI * 2f;
                float cd = Mathf.Lerp(R * 0.30f, R * 0.70f, (float)rng.NextDouble());
                clumpCentres[c] = new Vector2(Mathf.Cos(ca), Mathf.Sin(ca)) * cd;
            }

            for (int i = 0; i < fieldCount; i++)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                Vector2 pos;
                float radius;

                switch (layout)
                {
                    case FieldLayout.Ring:
                        // A band around the middle distance: long circular walks.
                        pos = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) *
                              Mathf.Lerp(R * 0.60f, R * 0.82f, (float)rng.NextDouble());
                        radius = Mathf.Lerp(4.5f, 6.0f, (float)rng.NextDouble());
                        break;

                    case FieldLayout.Clustered:
                        // Dense pockets with empty ground between them.
                        var centre = clumpCentres[i % clumps];
                        pos = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) *
                              Mathf.Lerp(0f, 8f, (float)rng.NextDouble());
                        pos = Vector2.ClampMagnitude(pos, R * 0.90f);
                        radius = Mathf.Lerp(3.5f, 5.5f, (float)rng.NextDouble());
                        break;

                    default:
                        pos = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) *
                              Mathf.Lerp(R * 0.28f, R * 0.86f, (float)rng.NextDouble());
                        radius = Mathf.Lerp(4f, 6.5f, (float)rng.NextDouble());
                        break;
                }

                var sr = Spawn("ShellField", ProcArt.Blob("field", Color.white, i * 31 + 11), pos, radius * 2f, fieldColor, -30);
                fields.Add(sr.transform);
                fieldRadii.Add(radius * 0.8f);

                // Speckles of shell fragments so the patch reads as "eggs hide here".
                for (int j = 0; j < 5; j++)
                {
                    var off = new Vector2((float)rng.NextDouble() - 0.5f, (float)rng.NextDouble() - 0.5f) * radius * 1.1f;
                    Color fleck = AgainstGround(Color.white, planet.Land, 0.9f, 0.45f);
                    fleck.a = 0.65f;
                    Spawn("shard", ProcArt.Blob("shard", Color.white, (j * 17 + i) % 7), pos + off, 0.55f,
                          fleck, -28);
                }
            }
        }

        void BuildNestStation(PlanetDef planet)
        {
            var go = new GameObject("NestStation");
            go.transform.SetParent(root, false);
            go.transform.position = Vector2.zero;
            nestStation = go.transform;

            Spawn("pad", ProcArt.Disc("nestpad", new Color(1f, 0.95f, 0.8f, 1f), new Color(1f, 0.7f, 0.2f, 0f), 1.6f, 128, 64f),
                  Vector2.zero, 7f, new Color(1f, 1f, 1f, 0.45f), -25, go.transform);
            Spawn("ring", ProcArt.Ring("nestring", Color.white, 0.05f), Vector2.zero, 5.4f,
                  new Color(1f, 0.78f, 0.30f, 0.9f), -24, go.transform);
            Spawn("hut", ProcArt.Disc("nesthut", new Color(1f, 0.88f, 0.62f, 1f), new Color(0.75f, 0.5f, 0.25f, 1f), 1.2f, 128, 64f),
                  Vector2.zero, 2.6f, Color.white, -20, go.transform);

            AddLabel(go.transform, new Vector2(0f, 2.4f),
                     "<b>NEST STATION</b>\n<size=18><color=#A8B2C4>press E to rest</color></size>", 22);
        }

        /// <summary>
        /// The hidden cache, if this world has one and it has not been dug up yet.
        ///
        /// Placed from the planet's own seed, so it is in the same spot every time you land, and
        /// out past two thirds of the radius so it is never on the way to anything. It is drawn
        /// faintly: findable by walking, not by glancing.
        /// </summary>
        void BuildCache(PlanetDef planet, float R)
        {
            if (!PlanetDatabase.HasCache(planet.Id)) return;
            if (dir.State.Caches.Contains(planet.Id)) return;

            var rng = new System.Random(planet.Seed ^ 0x5EED);
            float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
            float dist = R * (0.68f + 0.22f * (float)rng.NextDouble());
            cachePos = new Vector2(Mathf.Cos(angle) * dist, Mathf.Sin(angle) * dist);

            var go = new GameObject("Cache");
            go.transform.SetParent(root, false);
            go.transform.localPosition = cachePos;
            cache = go.transform;

            // Darkening the ground by a fixed amount works on a bright world and vanishes on a
            // dark one: Nullreach's ground sits at 0.23 luminance, so a 30% darker mound left a
            // gap of 0.07 - the same way shell fields once disappeared into ice. Separate from
            // the ground rather than a fixed step off it.
            var mound = AgainstGround(new Color(0.32f, 0.24f, 0.15f), planet.Land, 0.55f, 0.46f);
            Spawn("mound", ProcArt.Blob("cachemound", Color.white, 21), Vector2.zero, 2.4f,
                  new Color(mound.r, mound.g, mound.b, 0.88f), -28, go.transform);
            Spawn("glint", ProcArt.Disc("cacheglint", new Color(1f, 0.92f, 0.68f, 1f),
                                        new Color(1f, 0.8f, 0.3f, 0f), 1.5f, 64, 64f),
                  new Vector2(0f, 0.2f), 1.5f, new Color(1f, 1f, 1f, 0.40f), -27, go.transform);
        }

        const float CacheRange = 1.6f;

        /// <summary>Walking over the cache digs it up. No prompt: finding it is the point.</summary>
        void TickCache()
        {
            if (cache == null || dir.Mode != GameMode.Surface) return;
            if (Vector2.Distance(dir.Teo.transform.position, cache.position) > CacheRange) return;

            var planet = current;
            dir.State.Caches.Add(planet.Id);
            int gained = PlanetDatabase.CacheWorlds[planet.Id];
            dir.State.Cartons = Mathf.Min(dir.State.MaxCartons, dir.State.Cartons + gained);
            dir.State.RaiseChanged();

            Destroy(cache.gameObject);
            cache = null;

            dir.Audio.Play(Sfx.CatchSuccess);
            dir.Hud.Toast("A buried supply cache! You can carry " + dir.State.MaxCartons + " cartons now.");
        }

        void BuildRoamers(PlanetDef planet, System.Random rng, float R)
        {
            // Busier worlds feel different to walk; 3 on a quiet rock, up to 6 on a crowded one.
            int count = 3 + (planet.Seed / 3) % 4;
            for (int i = 0; i < count; i++)
            {
                var r = new Roamer();
                var go = new GameObject("Roamer" + i);
                go.transform.SetParent(root, false);
                r.Root = go.transform;

                var spriteGo = new GameObject("Sprite");
                spriteGo.transform.SetParent(go.transform, false);
                r.Sprite = spriteGo.AddComponent<SpriteRenderer>();
                r.Sprite.material = ProcArt.SpriteMaterial;
                r.Sprite.sortingOrder = 8;

                RespawnRoamer(r, R, true);
                roamers.Add(r);
            }
        }

        void RespawnRoamer(Roamer r, float R, bool immediate)
        {
            r.SpeciesId = current.RollSpecies();
            r.Level = current.RollLevel();
            r.IsElder = EggRandom.Value < EggInstance.ElderChance;
            var species = SpeciesDatabase.Get(r.SpeciesId);

            r.Sprite.sprite = ProcArt.Egg(species);
            float spriteWorld = r.Sprite.sprite.rect.width / r.Sprite.sprite.pixelsPerUnit;
            // Elders are visibly bigger, so you can decide whether to approach.
            r.Sprite.transform.localScale = Vector3.one * ((r.IsElder ? 2.15f : 1.5f) / spriteWorld);
            r.Sprite.color = Color.white;

            if (r.Aura != null) Destroy(r.Aura.gameObject);
            r.Aura = null;
            if (r.IsElder)
            {
                Color tint = TypeChart.ColorOf(species.Type);
                r.Aura = Spawn("elderaura", ProcArt.Disc("elderaura", tint, new Color(tint.r, tint.g, tint.b, 0f), 1.5f, 128, 64f),
                               Vector2.zero, 4.2f, new Color(1f, 1f, 1f, 0.55f), 7, r.Root);
            }

            // Keep a respectful distance from wherever Teo is standing.
            Vector2 pos;
            int guard = 0;
            do
            {
                float a = Random.value * Mathf.PI * 2f;
                float d = Mathf.Lerp(R * 0.25f, R * 0.9f, Random.value);
                pos = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d;
                guard++;
            }
            while (guard < 24 && dir != null && dir.Teo != null &&
                   Vector2.Distance(pos, dir.Teo.transform.position) < 9f);

            r.Root.position = pos;
            r.Heading = Random.insideUnitCircle.normalized;
            r.TurnTimer = Random.Range(1.2f, 3f);
            r.RespawnTimer = immediate ? 0f : Random.Range(3f, 6f);
            r.Alive = immediate;
            r.Root.gameObject.SetActive(immediate);
        }

        void BuildAmy(PlanetDef planet)
        {
            var go = new GameObject("Amy");
            go.transform.SetParent(root, false);
            go.transform.position = new Vector2(0f, 12f);
            amyFigure = go.transform;

            Spawn("aura", ProcArt.Disc("amyaura", new Color(1f, 0.75f, 1f, 1f), new Color(0.6f, 0.2f, 0.9f, 0f), 1.5f, 128, 64f),
                  Vector2.zero, 12f, new Color(1f, 1f, 1f, 0.5f), -15, go.transform);

            var teoSprite = Spawn("figure", ProcArt.Teo(), Vector2.zero, 2.6f, new Color(1f, 0.85f, 0.95f, 1f), 9, go.transform);
            teoSprite.transform.localPosition = Vector3.zero;

            var trio = new[] { "solyolk", "obsidyolk", "reginova" };
            for (int i = 0; i < trio.Length; i++)
            {
                var species = SpeciesDatabase.Get(trio[i]);
                Spawn("ace" + i, ProcArt.Egg(species), new Vector2((i - 1) * 2.3f, -2.2f), 1.5f, Color.white, 8, go.transform);
            }

            AddLabel(go.transform, new Vector2(0f, 3.2f),
                     "<b><color=#FFC24D>AMY</color></b>\n<size=18><color=#A8B2C4>press E to challenge</color></size>", 26);
        }

        void BuildNpcs(PlanetDef planet)
        {
            var all = StoryDatabase.Npcs;
            for (int i = 0; i < all.Length; i++)
            {
                var def = all[i];
                if (def.PlanetId != planet.Id) continue;
                if (dir != null && !dir.Story.IsNpcPresent(def)) continue;

                var go = new GameObject("Npc_" + def.Id);
                go.transform.SetParent(root, false);
                go.transform.position = def.Position;

                // Keepers stand in a soft pool of their own light; rivals get a hard ring
                // and no glow, so you can tell a conversation from a fight at a glance.
                if (def.IsKeeper)
                {
                    Spawn("glow", ProcArt.Disc("npcglow_" + def.Id, def.Tint, new Color(def.Tint.r, def.Tint.g, def.Tint.b, 0f), 1.6f, 128, 64f),
                          Vector2.zero, 5.5f, new Color(1f, 1f, 1f, 0.35f), -18, go.transform);
                    Spawn("mark", ProcArt.Ring("npcring", Color.white, 0.05f), Vector2.zero, 2.6f,
                          new Color(def.Tint.r, def.Tint.g, def.Tint.b, 0.75f), -17, go.transform);
                }
                else
                {
                    Spawn("mark", ProcArt.Ring("npcringhard", Color.white, 0.13f), Vector2.zero, 3.0f,
                          new Color(1f, 0.42f, 0.42f, 0.85f), -17, go.transform);
                }

                var visual = new GameObject("Visual").transform;
                visual.SetParent(go.transform, false);
                var sr = Spawn("body", ProcArt.Portrait(def.Name, def.Tint), Vector2.zero, 2.1f, Color.white, 9, visual);
                sr.transform.localPosition = Vector3.zero;

                AddLabel(go.transform, new Vector2(0f, 2.0f),
                         "<b>" + def.Name + "</b>\n<size=17><color=#A8B2C4>press E to talk</color></size>", 23);

                npcs.Add(new NpcView { Def = def, Root = go.transform, Visual = visual });
            }
        }

        void AddLabel(Transform anchor, Vector2 offset, string text, int size)
        {
            var rect = UIKit.Node(labelCanvas.transform, "WorldLabel");
            UIKit.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 90f));
            var t = UIKit.Label(rect, "Text", text, size, UIKit.Ink, TextAnchor.UpperCenter, FontStyle.Normal);
            UIKit.Stretch(t.rectTransform, 0f, 0f, 0f, 0f);
            labels.Add(new WorldLabel { Anchor = anchor, Offset = offset, Rect = rect, Text = t });
        }

        // ------------------------------------------------------------------
        // per-frame
        // ------------------------------------------------------------------

        void Update()
        {
            if (dir == null || current == null) return;
            if (labelCanvas != null && labelCanvas.gameObject.activeInHierarchy) UpdateLabels();
            if (dir.Mode != GameMode.Surface) return;
            // A conversation, the chart, or the collection screen suspends the world:
            // without this a roamer could wander into Teo mid-dialogue and start a battle.
            if (dir.OverlayOpen) return;

            float dt = Time.deltaTime;
            if (encounterCooldown > 0f) encounterCooldown -= dt;

            TickNpcs(dt);
            TickRoamers(dt);
            TickInteractions();
            TickCache();
            if (!current.IsBossWorld) TickFieldEncounters(dt);
        }

        void UpdateLabels()
        {
            for (int i = 0; i < labels.Count; i++)
            {
                var l = labels[i];
                if (l.Anchor == null) continue;
                Vector3 world = l.Anchor.position + (Vector3)l.Offset;
                Vector3 screen = dir.Cam.WorldToScreenPoint(world);
                bool visible = screen.z > 0f;
                if (l.Rect.gameObject.activeSelf != visible) l.Rect.gameObject.SetActive(visible);
                if (!visible) continue;
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(labelCanvasRect, screen, null, out local);
                l.Rect.anchoredPosition = local;
            }
        }

        void TickNpcs(float dt)
        {
            for (int i = 0; i < npcs.Count; i++)
            {
                var n = npcs[i];
                n.Bob += dt * 1.8f;
                n.Visual.localPosition = new Vector3(0f, Mathf.Sin(n.Bob) * 0.13f, 0f);
            }
        }

        void TickRoamers(float dt)
        {
            float R = current.SurfaceRadius;
            for (int i = 0; i < roamers.Count; i++)
            {
                var r = roamers[i];

                if (!r.Alive)
                {
                    r.RespawnTimer -= dt;
                    if (r.RespawnTimer <= 0f)
                    {
                        RespawnRoamer(r, R, true);
                    }
                    continue;
                }

                r.TurnTimer -= dt;
                if (r.TurnTimer <= 0f)
                {
                    r.TurnTimer = Random.Range(1.2f, 3f);
                    r.Heading = Random.insideUnitCircle.normalized;
                }

                Vector2 pos = r.Root.position;
                pos += r.Heading * (RoamerSpeed * dt);
                if (pos.magnitude > R * 0.94f)
                {
                    r.Heading = (-pos).normalized;
                    pos = pos.normalized * (R * 0.94f);
                }
                r.Root.position = pos;

                r.Bob += dt * 7f;
                r.Sprite.transform.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(r.Bob)) * 0.22f, 0f);

                if (dir.Mode == GameMode.Surface && encounterCooldown <= 0f &&
                    Vector2.Distance(pos, dir.Teo.transform.position) < RoamerTouchRange)
                {
                    engagedRoamer = i;
                    r.Alive = false;
                    r.Root.gameObject.SetActive(false);
                    dir.BeginWildBattle(r.IsElder
                        ? EggInstance.WildElder(r.SpeciesId, r.Level)
                        : EggInstance.Wild(r.SpeciesId, r.Level));
                    return;
                }
            }
        }

        void TickFieldEncounters(float dt)
        {
            if (encounterCooldown > 0f) return;

            Vector2 teo = dir.Teo.transform.position;
            bool inside = false;
            for (int i = 0; i < fields.Count; i++)
            {
                if (Vector2.Distance(teo, fields[i].position) <= fieldRadii[i]) { inside = true; break; }
            }

            if (!inside)
            {
                fieldDistance = Mathf.Max(0f, fieldDistance - dt * 0.5f);
                return;
            }

            fieldDistance += dir.Teo.DistanceMovedThisFrame;
            if (fieldDistance < nextEncounterDistance) return;

            fieldDistance = 0f;
            nextEncounterDistance = Random.Range(7f, 15f);
            engagedRoamer = -1;

            string speciesId = current.RollSpecies();
            int level = current.RollLevel();
            dir.BeginWildBattle(EggRandom.Value < EggInstance.ElderChance
                ? EggInstance.WildElder(speciesId, level)
                : EggInstance.Wild(speciesId, level));
        }

        void TickInteractions()
        {
            Vector2 teo = dir.Teo.transform.position;
            string prompt = null;

            // Someone standing in front of you takes priority over the scenery.
            NpcView nearest = null;
            float nearestDist = float.MaxValue;
            for (int i = 0; i < npcs.Count; i++)
            {
                float d = Vector2.Distance(teo, npcs[i].Root.position);
                if (d < 3.4f && d < nearestDist) { nearest = npcs[i]; nearestDist = d; }
            }

            if (nearest != null)
            {
                prompt = "Press <b>E</b> to talk to <b>" + nearest.Def.Name + "</b>";
                dir.Hud.SetPrompt(prompt);
                if (EggInput.InteractPressed)
                {
                    var script = StoryDatabase.GetDialogue(nearest.Def.Id, dir.Story, dir.State);
                    if (script != null) { dir.PlayDialogue(script); return; }
                }
                if (EggInput.LiftoffPressed) dir.LiftOff();
                return;
            }

            if (current.IsBossWorld && amyFigure != null && Vector2.Distance(teo, amyFigure.position) < 4.5f)
            {
                prompt = dir.State.AmyDefeated
                    ? "Press <b>E</b> to rematch <b>Amy</b>."
                    : "Press <b>E</b> to challenge <b>Amy</b>.";
                if (EggInput.InteractPressed)
                {
                    dir.PlayDialogue(StoryDatabase.AmyIntro(dir.Story));
                    return;
                }
            }
            else if (nestStation != null && Vector2.Distance(teo, nestStation.position) < NestRange)
            {
                prompt = "Press <b>E</b> to rest at the <b>Nest Station</b>  ·  <b>Q</b> to lift off";
                if (EggInput.InteractPressed)
                {
                    dir.State.HealAll();
                    dir.Hud.Toast("Your eggs are warm and whole again. Cartons restocked.");
                    dir.Audio.Play(Sfx.Heal);
                    dir.SaveNow(true);
                }
            }
            else
            {
                prompt = "<b>WASD</b> walk  ·  <b>Q</b> lift off  ·  <b>Tab</b> party";
            }

            dir.Hud.SetPrompt(prompt);

            if (EggInput.LiftoffPressed) dir.LiftOff();
        }

        /// <summary>Called when a battle that started here has finished.</summary>
        public void NotifyBattleEnded()
        {
            encounterCooldown = 1.4f;
            fieldDistance = 0f;
            if (engagedRoamer >= 0 && engagedRoamer < roamers.Count)
            {
                var r = roamers[engagedRoamer];
                r.Alive = false;
                r.RespawnTimer = Random.Range(4f, 8f);
                r.Root.gameObject.SetActive(false);
            }
            engagedRoamer = -1;
        }

        public Vector2 LandingPoint()
        {
            return new Vector2(0f, -6.5f);
        }
    }
}
