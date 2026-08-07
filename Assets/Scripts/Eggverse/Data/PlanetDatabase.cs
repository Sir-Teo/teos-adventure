using System.Collections.Generic;
using UnityEngine;

namespace Eggverse
{
    public struct SpawnEntry
    {
        public readonly string SpeciesId;
        public readonly int Weight;
        public SpawnEntry(string speciesId, int weight) { SpeciesId = speciesId; Weight = weight; }
    }

    public enum Sector
    {
        HatcheryReach = 0,
        LongDrift = 1,
        ShatteredBelt = 2,
        Amaranth = 3
    }

    public class PlanetDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly EggType Theme;
        public readonly Sector Sector;
        public readonly Vector2 SpacePosition;
        public readonly float SpaceRadius;      // drawn size on the star map
        public readonly float SurfaceRadius;    // walkable disc when landed
        public readonly Color Ocean, Land, Atmosphere;
        public readonly int MinLevel, MaxLevel;
        public readonly SpawnEntry[] Spawns;
        public readonly string Tagline;
        public readonly bool IsBossWorld;

        /// <summary>
        /// Deterministic per-world seed. string.GetHashCode is not guaranteed stable between
        /// runs, and a planet that rearranges itself every session is not a place — it is noise.
        /// FNV-1a over the id gives the same layout forever.
        /// </summary>
        public readonly int Seed;

        static int StableHash(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < s.Length; i++) { h ^= s[i]; h *= 16777619u; }
                return (int)(h & 0x7FFFFFFF);
            }
        }

        public PlanetDef(string id, string name, EggType theme, Sector sector, Vector2 pos,
                         float spaceRadius, float surfaceRadius,
                         Color ocean, Color land, Color atmosphere,
                         int minLevel, int maxLevel, SpawnEntry[] spawns,
                         string tagline, bool isBossWorld = false)
        {
            Id = id; Name = name; Theme = theme; Sector = sector; SpacePosition = pos;
            SpaceRadius = spaceRadius; SurfaceRadius = surfaceRadius;
            Ocean = ocean; Land = land; Atmosphere = atmosphere;
            MinLevel = minLevel; MaxLevel = maxLevel; Spawns = spawns;
            Tagline = tagline; IsBossWorld = isBossWorld;
            Seed = StableHash(id);
        }

        public string RollSpecies()
        {
            if (Spawns == null || Spawns.Length == 0) return "sprouteg";
            int total = 0;
            for (int i = 0; i < Spawns.Length; i++) total += Spawns[i].Weight;
            int roll = EggRandom.Range(0, Mathf.Max(1, total));
            for (int i = 0; i < Spawns.Length; i++)
            {
                roll -= Spawns[i].Weight;
                if (roll < 0) return Spawns[i].SpeciesId;
            }
            return Spawns[Spawns.Length - 1].SpeciesId;
        }

        public int RollLevel() => EggRandom.Range(MinLevel, MaxLevel + 1);
    }

    public static class PlanetDatabase
    {
        static readonly List<PlanetDef> ordered = new List<PlanetDef>();
        static readonly Dictionary<string, PlanetDef> byId = new Dictionary<string, PlanetDef>();

        public static IReadOnlyList<PlanetDef> All => ordered;

        public static PlanetDef Get(string id)
        {
            PlanetDef p;
            return byId.TryGetValue(id, out p) ? p : ordered[0];
        }

        /// <summary>Whether this id names a real world. Get() falls back to Yolkhaven, which
        /// is right for a lookup and wrong for validating a save file.</summary>
        public static bool Exists(string id) => !string.IsNullOrEmpty(id) && byId.ContainsKey(id);

        public static PlanetDef Home => ordered[0];

        public static string SectorName(Sector s)
        {
            switch (s)
            {
                case Sector.HatcheryReach: return "The Hatchery Reach";
                case Sector.LongDrift: return "The Long Drift";
                case Sector.ShatteredBelt: return "The Shattered Belt";
                default: return "Amaranth";
            }
        }

        /// <summary>Roughly where a sector sits, for drawing its label on the chart.</summary>
        /// <summary>
        /// Every world this species can be found on. The collection screen needs the reverse of
        /// the star map's "what lives here" — a collector hunting the last few eggs otherwise has
        /// to fly to all seventeen worlds and read each one.
        /// </summary>
        public static List<PlanetDef> WorldsWith(string speciesId)
        {
            var found = new List<PlanetDef>();
            for (int i = 0; i < ordered.Count; i++)
                for (int j = 0; j < ordered[i].Spawns.Length; j++)
                    if (ordered[i].Spawns[j].SpeciesId == speciesId) { found.Add(ordered[i]); break; }
            return found;
        }

        public static Vector2 SectorCentre(Sector s)
        {
            Vector2 sum = Vector2.zero;
            int n = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i].Sector != s) continue;
                sum += ordered[i].SpacePosition;
                n++;
            }
            return n == 0 ? Vector2.zero : sum / n;
        }

        static void Add(PlanetDef p) { ordered.Add(p); byId[p.Id] = p; }
        static Color C(int rgb) => new Color32((byte)(rgb >> 16 & 0xFF), (byte)(rgb >> 8 & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        static SpawnEntry[] S(params SpawnEntry[] e) => e;
        static SpawnEntry W(string id, int weight) => new SpawnEntry(id, weight);

        static PlanetDatabase()
        {
            // ===================== Sector I — The Hatchery Reach =====================

            Add(new PlanetDef("yolkhaven", "Yolkhaven", EggType.Verdant, Sector.HatcheryReach,
                new Vector2(0f, 0f), 6f, 34f,
                C(0x3E7A4E), C(0x8FCB6B), C(0xBFF0A8),
                3, 6,
                S(W("sprouteg", 5), W("cobblet", 3), W("tidepoach", 2)),
                "Green, gentle, and full of shoots. Every hatcher starts here."));

            Add(new PlanetDef("cinderoost", "Cinderoost", EggType.Molten, Sector.HatcheryReach,
                new Vector2(48f, 16f), 5.5f, 34f,
                C(0x5A1F0E), C(0xD1552A), C(0xFFB07A),
                5, 9,
                S(W("yolkano", 5), W("emberoo", 3), W("cobblet", 2)),
                "Ash plains that crunch underfoot. Warm enough to sleep on."));

            Add(new PlanetDef("brineholt", "Brineholt", EggType.Tidal, Sector.HatcheryReach,
                new Vector2(-44f, 22f), 5.5f, 34f,
                C(0x14486E), C(0x3D93C4), C(0x9FDCF5),
                6, 10,
                // Yolty is the only Volt egg in the Reach: without it a Tidal world has no
                // counter this early, and storm-born eggs washing up on the tide is fair enough.
                S(W("tidepoach", 5), W("bubblenog", 3), W("chillet", 2), W("yolty", 2)),
                "One shallow ocean, one very long tide, and storms that never quite land."));

            Add(new PlanetDef("mosswell", "Mosswell", EggType.Verdant, Sector.HatcheryReach,
                new Vector2(10f, -46f), 5f, 32f,
                C(0x2C5B38), C(0x6FA858), C(0xA8DE94),
                7, 11,
                S(W("mossmallow", 4), W("sprouteg", 3), W("bloomolk", 2), W("cobblet", 1)),
                "Soft ground, deep wells, and moss that grows over anything left still."));

            // ===================== Sector II — The Long Drift =====================

            Add(new PlanetDef("tidewrack", "Tidewrack", EggType.Tidal, Sector.LongDrift,
                new Vector2(-72f, -30f), 5.5f, 34f,
                C(0x0E3A5A), C(0x2F7CA8), C(0x86C9E8),
                10, 14,
                S(W("wavelet", 4), W("bubblenog", 3), W("tidepoach", 2), W("chillet", 1)),
                "A shipbreaker coast. The eggs here have learned to hide in the hulls."));

            Add(new PlanetDef("voltacrest", "Voltacrest", EggType.Volt, Sector.LongDrift,
                new Vector2(60f, -44f), 5f, 34f,
                C(0x3B3410), C(0xD9B429), C(0xFFEF9C),
                11, 15,
                S(W("yolty", 4), W("frizzlebolt", 3), W("sparkshell", 3), W("nebulegg", 1)),
                "A permanent storm with a planet somewhere under it."));

            Add(new PlanetDef("emberfall", "Emberfall", EggType.Molten, Sector.LongDrift,
                new Vector2(78f, 66f), 5.5f, 34f,
                C(0x4A1508), C(0xE0632C), C(0xFFA36B),
                12, 16,
                S(W("sizzlette", 4), W("emberoo", 3), W("yolkano", 2), W("boulderoo", 1)),
                "Ash falls upward here. Nobody has a good explanation."));

            Add(new PlanetDef("cobblestead", "Cobblestead", EggType.Stone, Sector.LongDrift,
                new Vector2(98f, 18f), 5.5f, 34f,
                C(0x3D3226), C(0xA88C6B), C(0xD9C4A8),
                13, 17,
                S(W("boulderoo", 4), W("craggle", 3), W("cobblet", 2), W("sizzlette", 1)),
                "Fields of round grey stones. Roughly a tenth of them are not stones."));

            Add(new PlanetDef("glacierim", "Glacierim", EggType.Frost, Sector.LongDrift,
                new Vector2(-88f, 26f), 6f, 36f,
                C(0x2A5F76), C(0xBEE8F4), C(0xE4FAFF),
                14, 18,
                S(W("snowpoach", 4), W("glacegg", 3), W("chillet", 2), W("craggle", 1)),
                "Ice sheets over old stone. Sound carries far out here."));

            Add(new PlanetDef("shimmerfen", "Shimmerfen", EggType.Aether, Sector.LongDrift,
                new Vector2(-2f, 54f), 5.5f, 34f,
                C(0x2E2358), C(0xB49BE8), C(0xE8DCFF),
                10, 14,
                S(W("nebulegg", 4), W("cosmolette", 2), W("chillet", 2), W("bubblenog", 1)),
                "A fen of standing light. Nothing casts a shadow here, including you."));

            Add(new PlanetDef("arcmoor", "Arcmoor", EggType.Volt, Sector.LongDrift,
                new Vector2(-58f, 66f), 5f, 34f,
                C(0x2B2E14), C(0xBFD13A), C(0xEEFFA8),
                13, 17,
                S(W("sparkshell", 4), W("frizzlebolt", 3), W("yolty", 2), W("nebulegg", 1)),
                "Heather to the horizon, and every stem of it humming."));

            // ===================== Sector III — The Shattered Belt =====================

            Add(new PlanetDef("umbralux", "Umbralux", EggType.Void, Sector.ShatteredBelt,
                new Vector2(30f, 96f), 6f, 34f,
                C(0x1A1533), C(0x54487F), C(0x9B8BD6),
                16, 20,
                S(W("duskle", 4), W("gloomolk", 3), W("shadowhisk", 2), W("nebulegg", 1)),
                "The dark side is the only side. Bring your own light."));

            Add(new PlanetDef("aetherwake", "Aetherwake", EggType.Aether, Sector.ShatteredBelt,
                new Vector2(86f, 116f), 5.5f, 34f,
                C(0x2A1A4E), C(0x9B6BD6), C(0xD6B8FF),
                17, 21,
                S(W("starlette", 4), W("cosmolette", 3), W("nebulegg", 2), W("gloomolk", 1)),
                "The debris still hums the note the old world was singing when it broke."));

            Add(new PlanetDef("nullreach", "Nullreach", EggType.Void, Sector.ShatteredBelt,
                new Vector2(-42f, 108f), 5.5f, 34f,
                C(0x110E24), C(0x3E3663), C(0x7A6DB0),
                18, 22,
                S(W("shadowhisk", 4), W("gloomolk", 3), W("duskle", 2), W("starlette", 1)),
                "Instruments read nothing here. The eggs read it fine."));

            Add(new PlanetDef("vesper", "Vesper", EggType.Frost, Sector.ShatteredBelt,
                new Vector2(-96f, 88f), 5f, 32f,
                C(0x1E4256), C(0x9FC8DA), C(0xD2ECF7),
                19, 23,
                S(W("snowpoach", 3), W("glacegg", 3), W("gloomolk", 2), W("starlette", 2)),
                "A cold, quiet waystation. Vess grew up here, and does not like to say so."));

            // ===================== Final =====================

            Add(new PlanetDef("cairnhold", "Cairnhold", EggType.Stone, Sector.ShatteredBelt,
                new Vector2(-82f, 132f), 5.5f, 34f,
                C(0x2A2A33), C(0x8A8794), C(0xC6C3D4),
                19, 23,
                S(W("boulderoo", 4), W("craggle", 3), W("shadowhisk", 2), W("cobblet", 1)),
                "The Belt's largest surviving piece. Someone stacked the rest into cairns."));

            Add(new PlanetDef("amaranth", "Amaranth Prime", EggType.Aether, Sector.Amaranth,
                new Vector2(0f, 168f), 12f, 42f,
                C(0x3A1152), C(0xC46BE8), C(0xF2B8FF),
                22, 26,
                S(W("cosmolette", 1)),
                "Amy's world. Enormous, quiet, and expecting you.",
                true));
        }
    }
}
