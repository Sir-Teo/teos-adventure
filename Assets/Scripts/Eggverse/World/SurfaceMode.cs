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
            public SpriteRenderer Halo;
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

            /// <summary>The name, always shown.</summary>
            public string Title;
            /// <summary>What this thing is for. Dropped once the HUD is saying the same thing.</summary>
            public string Hint;
            /// <summary>The range at which the HUD takes over the instruction.</summary>
            public float Range;
            public bool HintShown = true;

            /// <summary>An unread landmark has no title yet, so the separator has to be earned.</summary>
            public string Compose(bool withHint)
            {
                if (!withHint || Hint.Length == 0) return Title;
                return Title.Length == 0 ? Hint : Title + "\n" + Hint;
            }
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
        Transform landmark;
        WorldLabel landmarkLabel;
        LandmarkDef landmarkDef;
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
            BuildLandmark(planet, R);
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
            landmark = null;
            landmarkDef = null;
            landmarkLabel = null;
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

        /// <summary>
        /// A rectangle in world units. Spawn scales uniformly, which is right for eggs and
        /// blobs and useless for a post, a beam or a seam - the things landmarks are made of.
        /// </summary>
        SpriteRenderer SpawnBar(string name, Vector2 pos, Vector2 size, Color tint, int order, Transform parent)
        {
            var sr = Spawn(name, ProcArt.White, pos, size.x, tint, order, parent);
            float spriteWorld = ProcArt.White.rect.width / ProcArt.White.pixelsPerUnit;
            sr.transform.localScale = new Vector3(size.x / spriteWorld, size.y / spriteWorld, 1f);
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
                        // Tinted toward this world's own ocean rather than a fixed bright blue.
                        // A pale blue over a blue world is nearly the same colour: on Brineholt
                        // the pools sat 0.077 luminance from the ground they lay on, which is
                        // less separation than the shell fields have and those were once
                        // invisible. Water should read darker than the land in any case.
                        Color poolCol = Color.Lerp(planet.Land, planet.Ocean, 0.85f);
                        var pool = Spawn("pool", ProcArt.Blob("pool", Color.white, (i % 5) * 9 + 4), pos, 3.2f * scale,
                                         new Color(poolCol.r, poolCol.g, poolCol.b, 0.88f), -44);
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

            // A station that has gone cold looks it. Ori spends the opening brief telling you
            // Brineholt and Mosswell are dead and that yours has just joined them, and then you
            // walk ten paces to a pad glowing exactly as warm as every other pad in the game.
            // Vesper was worse: its own landmark is two dark pads with the straw still in them,
            // twenty paces from a third one blazing away.
            bool cold = PlanetDatabase.StationCold(planet.Id) && !dir.Story.HasFlag("beat_amy");

            float halo = cold ? 0.13f : 0.45f;
            var ringCol = cold ? new Color(0.42f, 0.46f, 0.58f, 0.75f)
                               : new Color(1f, 0.78f, 0.30f, 0.9f);
            var hutCol = cold ? new Color(0.62f, 0.66f, 0.76f, 1f) : Color.white;

            Spawn("pad", ProcArt.Disc("nestpad", new Color(1f, 0.95f, 0.8f, 1f), new Color(1f, 0.7f, 0.2f, 0f), 1.6f, 128, 64f),
                  Vector2.zero, 7f, new Color(1f, 1f, 1f, halo), -25, go.transform);
            Spawn("ring", ProcArt.Ring("nestring", Color.white, 0.05f), Vector2.zero, 5.4f,
                  ringCol, -24, go.transform);
            Spawn("hut", ProcArt.Disc("nesthut", new Color(1f, 0.88f, 0.62f, 1f), new Color(0.75f, 0.5f, 0.25f, 1f), 1.2f, 128, 64f),
                  Vector2.zero, 2.6f, hutCol, -20, go.transform);

            AddLabel(go.transform, new Vector2(0f, 2.4f), UiCopy.LabelNestStation, UiCopy.LabelNestHint, 22, NestRange);
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

            cachePos = SurfaceLayout.CachePosition(planet);

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
        const float LandmarkRange = 2.6f;

        /// <summary>
        /// The one thing on this world worth walking to. Placed from the planet's own seed, out
        /// past the shell fields and on the opposite side from the cache, so a world with both
        /// is worth crossing twice.
        /// </summary>
        void BuildLandmark(PlanetDef planet, float R)
        {
            landmarkDef = LandmarkDatabase.For(planet.Id);
            if (landmarkDef == null) return;

            var pos = SurfaceLayout.LandmarkPosition(planet);

            var go = new GameObject("Landmark");
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos;
            landmark = go.transform;

            // Coloured against the ground like everything else out here, then built into
            // whatever this particular landmark actually is.
            var stone = AgainstGround(new Color(0.62f, 0.64f, 0.72f), planet.Land, 0.5f, 0.42f);
            BuildLandmarkForm(go.transform, landmarkDef.Form, stone);

            // A person's name is written on their face; an inscription's is the thing you walked
            // out here to find. Before you read it the label says only that there is something
            // to read, and afterwards it is a name you earned and the world remembers.
            landmarkLabel = AddLabel(go.transform, new Vector2(0f, 2.1f), LandmarkTitle(),
                                     UiCopy.LabelLandmarkHint, 22, LandmarkRange);
        }

        /// <summary>Walking over the cache digs it up. No prompt: finding it is the point.</summary>
        int restIdle;

        /// <summary>
        /// What the station says, based on what it actually just fixed. A player who walks in
        /// whole and fully stocked should not be told their eggs are mended.
        /// </summary>
        string RestLine(int fainted, int hurt, bool shortOfSupplies)
        {
            // On a dead pad the news is that it worked at all, which is worth more than which
            // of your eggs needed mending.
            if (PlanetDatabase.StationCold(current.Id) && !dir.Story.HasFlag("beat_amy"))
                return UiCopy.RestCold[restIdle++ % UiCopy.RestCold.Length];

            if (fainted > 0)
            {
                restIdle = 0;
                return Words.Count(fainted, "egg") + " back on " +
                       (fainted == 1 ? "its" : "their") + " feet. Shells mended, supplies restocked.";
            }
            if (hurt > 0)
            {
                restIdle = 0;
                return "Shells mended, supplies restocked.";
            }
            if (shortOfSupplies)
            {
                restIdle = 0;
                return "Supplies restocked. Nothing else needed doing.";
            }

            // Nothing to fix. Rather than lie about mending anything, say so - and vary it,
            // because a player standing on the pad pressing E is usually just fond of the place.
            return UiCopy.RestIdle[restIdle++ % UiCopy.RestIdle.Length];
        }

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

        /// <summary>
        /// Builds the landmark's silhouette. Every world had the same grey disc, which at any
        /// distance is a rock - so a bell, a ship's bow and nine hundred cairns all read as
        /// "some scenery". Each form is built from the same two primitives, arranged so the
        /// shape is legible before the label is.
        /// </summary>
        void BuildLandmarkForm(Transform parent, LandmarkForm form, Color stone)
        {
            var dark = new Color(stone.r * 0.66f, stone.g * 0.66f, stone.b * 0.72f, 1f);
            var shadow = new Color(dark.r, dark.g, dark.b, 0.85f);
            var glow = new Color(1f, 0.92f, 0.7f, 0.26f);

            // The same trick that makes Teo legible on a bright world: a dark shape behind the
            // light one. Without it every form was a grey smudge the colour of terrain mottling,
            // which the render made obvious and no assertion ever could.
            var outline = new Color(0.07f, 0.08f, 0.13f, 0.85f);

            // Everything stands on a scuffed patch - except the seam, which is not built on
            // anything. It is a line in the ground; giving it a plinth made it a monument.
            if (form != LandmarkForm.Seam)
                Spawn("base", ProcArt.Blob("landmarkbase", Color.white, 13), new Vector2(0f, -1.0f), 3.4f,
                      new Color(shadow.r, shadow.g, shadow.b, 0.55f), -29, parent);

            switch (form)
            {
                case LandmarkForm.Post:
                    // One upright with a carved head. It was a 0.5u stick that read as a twig;
                    // the head is what tells you somebody put it there.
                    SpawnBar("shaftEdge", new Vector2(0f, 0.55f), new Vector2(1.06f, 3.66f), outline, -21, parent);
                    SpawnBar("shaft", new Vector2(0f, 0.55f), new Vector2(0.82f, 3.4f), stone, -19, parent);
                    SpawnBar("headEdge", new Vector2(0f, 2.3f), new Vector2(1.74f, 0.86f), outline, -21, parent);
                    SpawnBar("head", new Vector2(0f, 2.3f), new Vector2(1.5f, 0.62f), stone, -19, parent);
                    Spawn("cap", ProcArt.Disc("lmcap", Color.white, new Color(1f, 1f, 1f, 0f), 1.4f, 64, 64f),
                          new Vector2(0f, 2.5f), 2.8f, glow, -18, parent);
                    break;

                case LandmarkForm.Frame:
                    // Two uprights with something hung between them.
                    SpawnBar("postLEdge", new Vector2(-1.0f, 0.5f), new Vector2(0.58f, 3.24f), outline, -21, parent);
                    SpawnBar("postREdge", new Vector2(1.0f, 0.5f), new Vector2(0.58f, 3.24f), outline, -21, parent);
                    SpawnBar("beamEdge", new Vector2(0f, 1.9f), new Vector2(2.84f, 0.54f), outline, -21, parent);
                    SpawnBar("postL", new Vector2(-1.0f, 0.5f), new Vector2(0.34f, 3.0f), stone, -19, parent);
                    SpawnBar("postR", new Vector2(1.0f, 0.5f), new Vector2(0.34f, 3.0f), stone, -19, parent);
                    SpawnBar("beam", new Vector2(0f, 1.9f), new Vector2(2.6f, 0.3f), stone, -19, parent);
                    Spawn("hungEdge", ProcArt.Blob("lmhung", Color.white, 4), new Vector2(0f, 1.0f), 1.42f,
                          outline, -18, parent);
                    Spawn("hung", ProcArt.Blob("lmhung", Color.white, 4), new Vector2(0f, 1.0f), 1.15f,
                          dark, -17, parent);
                    break;

                case LandmarkForm.Stones:
                    // Five, set apart. At 0.72u spacing they merged into one dark caterpillar;
                    // you have to be able to count them for it to read as somebody's work.
                    for (int i = 0; i < 5; i++)
                    {
                        float t = (i - 2f) * 1.18f;
                        float h = 1.5f - Mathf.Abs(i - 2f) * 0.26f;
                        Spawn("stoneEdge" + i, ProcArt.Blob("lmstone", Color.white, i * 3 + 1),
                              new Vector2(t, h * 0.34f), h * 1.22f, outline, -20 - i, parent);
                        // All five in the light stone, not four dark ones around a light middle.
                        // On a world whose decor is dark rounded blobs - Brineholt, Tidewrack -
                        // dark stones read as more scenery, and the whole point of a landmark is
                        // that somebody set it there.
                        Spawn("stone" + i, ProcArt.Blob("lmstone", Color.white, i * 3 + 1),
                              new Vector2(t, h * 0.34f), h, stone, -19 - i, parent);
                    }
                    break;

                case LandmarkForm.Hollow:
                    // A ring around an opening, and the opening is darker than anything near it.
                    Spawn("rimEdge", ProcArt.Ring("lmrimE", Color.white, 0.26f, 128, 64f),
                          Vector2.zero, 3.5f, outline, -20, parent);
                    Spawn("rim", ProcArt.Ring("lmrim", Color.white, 0.20f, 128, 64f),
                          Vector2.zero, 3.2f, stone, -19, parent);
                    Spawn("mouth", ProcArt.Disc("lmmouth", Color.white, Color.white, 0.05f, 64, 64f),
                          Vector2.zero, 2.1f, new Color(0.04f, 0.05f, 0.09f, 0.92f), -18, parent);
                    break;

                case LandmarkForm.Hulk:
                    // Canted. Symmetrical it was a rock with a stripe; the whole point is that it
                    // is a made thing lying at an angle nothing natural would.
                    Spawn("massEdge", ProcArt.Blob("lmhulk", Color.white, 9), new Vector2(0f, 0.5f), 4.0f,
                          outline, -21, parent);
                    Spawn("mass", ProcArt.Blob("lmhulk", Color.white, 9), new Vector2(0f, 0.5f), 3.6f,
                          dark, -19, parent);
                    var prowEdge = SpawnBar("prowEdge", new Vector2(0.75f, 1.85f), new Vector2(3.5f, 0.78f), outline, -18, parent);
                    prowEdge.transform.localRotation = Quaternion.Euler(0f, 0f, 26f);
                    var prow = SpawnBar("prow", new Vector2(0.75f, 1.85f), new Vector2(3.2f, 0.5f), stone, -17, parent);
                    prow.transform.localRotation = Quaternion.Euler(0f, 0f, 26f);
                    break;

                case LandmarkForm.Seam:
                    // Not built - a line across the ground, filled and refilled.
                    SpawnBar("seam", Vector2.zero, new Vector2(7.0f, 0.26f),
                             new Color(0.05f, 0.05f, 0.09f, 0.9f), -19, parent);
                    SpawnBar("mortar", new Vector2(0f, 0.02f), new Vector2(6.6f, 0.13f),
                             new Color(stone.r, stone.g, stone.b, 0.8f), -18, parent);
                    break;
            }
        }

        /// <summary>
        /// Reading a landmark uses the dialogue box, with the landmark as the speaker. It is the
        /// same weight as talking to somebody, which is right - these are the only voices on the
        /// map older than the people living on it.
        /// </summary>
        /// <summary>Blank until read, then the name - the label's own small reward.</summary>
        string LandmarkTitle()
        {
            return dir.State.Landmarks.Contains(current.Id) ? "<b>" + landmarkDef.Name + "</b>" : "";
        }

        static DialogueScript LandmarkScript(LandmarkDef def, GameState state)
        {
            bool coda = def.Coda != null && LandmarkDatabase.AllOthersRead(def.PlanetId, state.Landmarks);
            int extra = coda ? def.Coda.Length : 0;

            var lines = new DialogueLine[def.Lines.Length + extra];
            for (int i = 0; i < def.Lines.Length; i++) lines[i] = new DialogueLine(def.Name, def.Lines[i]);
            for (int i = 0; i < extra; i++) lines[def.Lines.Length + i] = new DialogueLine(def.Name, def.Coda[i]);
            return new DialogueScript(lines);
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
                // A soft backing disc, coloured against this world's ground. Species colour comes
                // from the element and ground colour from the planet, so the two collide whenever
                // an egg lives on a world of its own element - a Void Shadowhisk on Void
                // Nullreach sat 0.13 luminance from the ground, and its darkened rim only 0.19.
                // Dark on a bright world, pale on a dark one, the same rule the shell fields and
                // the caches use.
                var haloGo = new GameObject("Halo");
                haloGo.transform.SetParent(r.Root, false);
                r.Halo = haloGo.AddComponent<SpriteRenderer>();
                r.Halo.sprite = ProcArt.Disc("roamerhalo", Color.white, new Color(1f, 1f, 1f, 0f), 1.5f, 64, 64f);
                r.Halo.material = ProcArt.SpriteMaterial;
                r.Halo.sortingOrder = 7;
                // 0.84, not 0.62. The halo was pitched against the bare ground, but a roamer
                // spends much of its time standing on a shell field - which is tinted toward
                // the world's own element, and so are the eggs that spawn there. A Molten egg
                // on Cinderoost's Molten fields sat 0.06 luminance from what it stood on.
                var haloTint = AgainstGround(planet.Land, planet.Land, 0f, 0.84f);
                r.Halo.color = new Color(haloTint.r, haloTint.g, haloTint.b, 0.55f);

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
            r.IsElder = dir.State.ElderesAllowed && EggRandom.Value < EggInstance.ElderChance;
            var species = SpeciesDatabase.Get(r.SpeciesId);

            r.Sprite.sprite = ProcArt.Egg(species);
            float spriteWorld = r.Sprite.sprite.rect.width / r.Sprite.sprite.pixelsPerUnit;
            // Elders are visibly bigger, so you can decide whether to approach.
            r.Sprite.transform.localScale = Vector3.one * ((r.IsElder ? 2.15f : 1.5f) / spriteWorld);
            r.Sprite.color = Color.white;
            if (r.Halo != null)
                r.Halo.transform.localScale = Vector3.one * (r.IsElder ? 2.9f : 2.1f);

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

            AddLabel(go.transform, new Vector2(0f, 3.2f), UiCopy.LabelAmy, UiCopy.LabelAmyHint, 26, 4.5f);
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

                AddLabel(go.transform, new Vector2(0f, 2.0f), "<b>" + def.Name + "</b>",
                         UiCopy.LabelTalkHint, 23, 3.4f);

                npcs.Add(new NpcView { Def = def, Root = go.transform, Visual = visual });
            }
        }

        WorldLabel AddLabel(Transform anchor, Vector2 offset, string title, string hint, int size, float range)
        {
            var rect = UIKit.Node(labelCanvas.transform, "WorldLabel");
            UIKit.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 90f));
            var t = UIKit.Label(rect, "Text", title + (title.Length > 0 && hint.Length > 0 ? "\n" : "") + hint, size, UIKit.Ink, TextAnchor.UpperCenter, FontStyle.Normal);
            UIKit.Stretch(t.rectTransform, 0f, 0f, 0f, 0f);

            // World labels float on the planet itself with no panel behind them, so their only
            // contrast is against whatever ground they happen to be over. Near-white ink on
            // Glacierim's ice is a gap of 0.08 - and these are the NEST STATION and every NPC
            // name, which is the game's wayfinding. An outline rather than a backing plate:
            // the same answer as Teo's, and it keeps the label feeling part of the world.
            var outline = t.gameObject.AddComponent<UnityEngine.UI.Outline>();
            outline.effectColor = new Color32(0x0C, 0x0F, 0x18, 0xE6);
            outline.effectDistance = new Vector2(2f, -2f);
            var label = new WorldLabel
            {
                Anchor = anchor, Offset = offset, Rect = rect, Text = t,
                Title = title, Hint = hint, Range = range,
            };
            labels.Add(label);
            return label;
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

                // Standing next to the Nest Station, the screen said "press E to rest" floating
                // over the pad and "Press E to rest at the Nest Station" along the bottom. The
                // hint's job is to tell you from a distance that this is worth walking to; once
                // you are close enough for the HUD to say it, it is the same sentence twice.
                bool wantHint = l.Hint.Length > 0 && l.Range > 0f &&
                                Vector2.Distance(dir.Teo.transform.position, l.Anchor.position) > l.Range;
                if (wantHint != l.HintShown)
                {
                    l.HintShown = wantHint;
                    l.Text.text = l.Compose(wantHint);
                }
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
            dir.BeginWildBattle(dir.State.ElderesAllowed && EggRandom.Value < EggInstance.ElderChance
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

            // The landmark sits between people and the station: worth stopping for, never in
            // the way of resting.
            if (landmark != null && landmarkDef != null &&
                Vector2.Distance(teo, landmark.position) < LandmarkRange &&
                (nestStation == null || Vector2.Distance(teo, nestStation.position) >= NestRange))
            {
                dir.Hud.SetPrompt(dir.State.Landmarks.Contains(current.Id)
                    ? "Press <b>E</b> to read <b>" + landmarkDef.Name + "</b> again"
                    : "Press <b>E</b> to read what is written here");
                if (EggInput.InteractPressed)
                {
                    // Reading it changes nothing and unlocks nothing. It is remembered because
                    // the player will want to know which ones they have found, and because a
                    // thing you found should stay found across a save.
                    bool firstReading = dir.State.Landmarks.Add(current.Id);
                    if (firstReading)
                    {
                        dir.Audio.Play(Sfx.Inscription);
                        // The label learns the name at the moment you do.
                        if (landmarkLabel != null)
                        {
                            landmarkLabel.Title = LandmarkTitle();
                            landmarkLabel.Text.text = landmarkLabel.Compose(landmarkLabel.HintShown);
                        }
                    }
                    dir.PlayDialogue(LandmarkScript(landmarkDef, dir.State));
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
                    // Look at what resting actually did before doing it. The same line every time
                    // for the most repeated interaction in the game reads like a vending machine,
                    // and it says "cartons restocked" whether or not you had spent any.
                    var st = dir.State;
                    int fainted = 0, hurt = 0;
                    foreach (var e in st.Party) { if (e.IsFainted) fainted++; else if (e.CurrentHP < e.MaxHP) hurt++; }
                    bool shortOfSupplies = st.Cartons < st.MaxCartons || st.Salves < GameState.MaxSalves;

                    st.HealAll();
                    dir.Hud.Toast(RestLine(fainted, hurt, shortOfSupplies));
                    dir.Audio.Play(Sfx.Heal);
                    // Quietly, in the corner: the rest message is the one worth reading here.
                    dir.SaveNow();
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
