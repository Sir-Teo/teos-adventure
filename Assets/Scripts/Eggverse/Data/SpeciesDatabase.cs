using System.Collections.Generic;
using UnityEngine;

namespace Eggverse
{
    public struct LearnEntry
    {
        public readonly int Level;
        public readonly string MoveId;
        public LearnEntry(int level, string moveId) { Level = level; MoveId = moveId; }
    }

    /// <summary>How an egg's shell is marked. Auto derives one from the art seed.</summary>
    public enum EggPattern
    {
        Auto = 0, Speckled, Mottled, Banded, Striped, Swirled, Starry, Cracked, Glossy
    }

    public class SpeciesDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly EggType Type;
        public readonly int BaseHP, BaseAtk, BaseDef, BaseSpd;
        public readonly int CatchRate;   // 1 (near impossible) .. 200 (easy)
        public readonly Color Body, Accent;
        public readonly int ArtSeed;
        public readonly string Blurb;
        public readonly LearnEntry[] Learnset;
        public readonly string EvolvesIntoId;   // null when this is the last stage
        public readonly int EvolveLevel;
        public readonly EggPattern Pattern;

        public SpeciesDef(string id, string name, EggType type,
                          int hp, int atk, int def, int spd, int catchRate,
                          Color body, Color accent, int artSeed, string blurb,
                          LearnEntry[] learnset, string evolvesIntoId = null, int evolveLevel = 0,
                          EggPattern pattern = EggPattern.Auto)
        {
            // Spread the eight shell patterns deterministically across the roster, so no two
            // neighbours in the dex share one, while still allowing an explicit override.
            Pattern = pattern != EggPattern.Auto
                ? pattern
                : (EggPattern)(1 + Mathf.Abs(artSeed * 5 + 3) % 8);
            Id = id; Name = name; Type = type;
            BaseHP = hp; BaseAtk = atk; BaseDef = def; BaseSpd = spd;
            CatchRate = catchRate; Body = body; Accent = accent;
            ArtSeed = artSeed; Blurb = blurb; Learnset = learnset;
            EvolvesIntoId = evolvesIntoId; EvolveLevel = evolveLevel;
        }

        public int BaseTotal => BaseHP + BaseAtk + BaseDef + BaseSpd;
        public bool CanEvolve => !string.IsNullOrEmpty(EvolvesIntoId) && EvolveLevel > 0;
    }

    public static class SpeciesDatabase
    {
        static readonly Dictionary<string, SpeciesDef> byId = new Dictionary<string, SpeciesDef>();
        static readonly List<SpeciesDef> ordered = new List<SpeciesDef>();

        public static SpeciesDef Get(string id)
        {
            SpeciesDef s;
            return byId.TryGetValue(id, out s) ? s : ordered[0];
        }

        /// <summary>Whether this id names a real species — see PlanetDatabase.Exists.</summary>
        /// <summary>
        /// Whatever evolves into this species, or null. The entry has always shown what an egg
        /// becomes and never what it came from, so a player looking at a grown form had no way
        /// to see the line it belongs to - which in a collecting game is most of the point.
        /// </summary>
        public static SpeciesDef EvolvesFrom(string id)
        {
            for (int i = 0; i < ordered.Count; i++)
                if (ordered[i].CanEvolve && ordered[i].EvolvesIntoId == id) return ordered[i];
            return null;
        }

        public static bool Exists(string id) => !string.IsNullOrEmpty(id) && byId.ContainsKey(id);

        public static IReadOnlyList<SpeciesDef> All => ordered;
        public static int Count => ordered.Count;

        /// <summary>
        /// How many species a player can realistically collect. Amy's trio and Vess's ace have
        /// catch rates low enough to be decorative, so a "complete" record excludes them.
        /// </summary>
        /// <summary>
        /// The median base speed across the roster, used to decide which eggs run from you and
        /// which come to have a look. Computed rather than written down, so adding a species
        /// cannot quietly put every egg in the game on one side of the line.
        /// </summary>
        public static int MedianBaseSpeed
        {
            get
            {
                var speeds = new List<int>();
                foreach (var sp in All) speeds.Add(sp.BaseSpd);
                speeds.Sort();
                return speeds[speeds.Count / 2];
            }
        }

        /// <summary>
        /// True for an egg that bolts when you get near. Ori calls the ones out in the open
        /// "the bold ones", and until now they all behaved identically: a random walk that took
        /// no notice of the player at all.
        /// </summary>
        public static bool IsSkittish(string speciesId)
        {
            var sp = Get(speciesId);
            return sp != null && sp.BaseSpd > MedianBaseSpeed;
        }

        /// <summary>Below this a species exists only by evolving one.</summary>
        public const int CatchableThreshold = 20;

        public static int CatchableCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < ordered.Count; i++)
                    if (ordered[i].CatchRate >= CatchableThreshold) n++;
                return n;
            }
        }

        /// <summary>Dex number, 1-based, in registration order.</summary>
        public static int DexNumber(string id)
        {
            for (int i = 0; i < ordered.Count; i++)
                if (ordered[i].Id == id) return i + 1;
            return 0;
        }

        static void Add(SpeciesDef s) { byId[s.Id] = s; ordered.Add(s); }
        static Color C(int rgb) => new Color32((byte)(rgb >> 16 & 0xFF), (byte)(rgb >> 8 & 0xFF), (byte)(rgb & 0xFF), 0xFF);
        static LearnEntry[] L(params LearnEntry[] e) => e;
        static LearnEntry E(int lv, string id) => new LearnEntry(lv, id);

        static SpeciesDatabase()
        {
            // ---------------- Verdant ----------------
            Add(new SpeciesDef("sprouteg", "Sprouteg", EggType.Verdant,
                45, 49, 49, 45, 180, C(0xB8E986), C(0x4F9A3C), 11,
                "A freckled little egg with one stubborn shoot on top. Refuses to be trimmed.",
                L(E(1, "tackle"), E(1, "vinewhisk"), E(6, "harden"), E(11, "sproutslam"), E(16, "photorest"), E(22, "yolktackle")),
                "mossmallow", 16));

            Add(new SpeciesDef("mossmallow", "Mossmallow", EggType.Verdant,
                60, 52, 62, 38, 120, C(0x7FBF6A), C(0x2F5D2A), 12,
                "Squishy, mossy, and completely unsquashable. Smells like rain on a windowsill.",
                L(E(1, "tackle"), E(1, "vinewhisk"), E(7, "fortify"), E(13, "sproutslam"), E(19, "photorest"), E(25, "scramble")),
                "bloomolk", 26));

            // ---------------- Molten ----------------
            Add(new SpeciesDef("yolkano", "Yolkano", EggType.Molten,
                44, 58, 44, 52, 170, C(0xFF9A52), C(0xB33511), 21,
                "Toasty all year round. Hatchers tuck one into their sleeping bag on cold nights.",
                L(E(1, "tackle"), E(1, "embercrack"), E(6, "warmup"), E(12, "lavayolk"), E(18, "rollout"), E(24, "flareshell")),
                "emberoo", 16));

            Add(new SpeciesDef("emberoo", "Emberoo", EggType.Molten,
                55, 64, 58, 48, 110, C(0xE0562B), C(0x4A1408), 22,
                "Hops instead of rolling. Its little cracks glow orange when it gets excited.",
                L(E(1, "tackle"), E(1, "embercrack"), E(8, "harden"), E(14, "lavayolk"), E(20, "scramble"), E(26, "flareshell")),
                "sizzlette", 26));

            // ---------------- Tidal ----------------
            Add(new SpeciesDef("tidepoach", "Tidepoach", EggType.Tidal,
                48, 50, 52, 48, 175, C(0x8FD4F5), C(0x1E6FA8), 31,
                "Bobs on its side in the shallows all day. Perpetually, blissfully poached.",
                L(E(1, "tackle"), E(1, "bubblepop"), E(6, "harden"), E(12, "drizzledrain"), E(17, "tidalcrack"), E(23, "yolktackle")),
                "bubblenog", 16));

            Add(new SpeciesDef("bubblenog", "Bubblenog", EggType.Tidal,
                58, 55, 60, 45, 115, C(0x4FA8D8), C(0x0D3B5E), 32,
                "Fizzes gently when happy. A salt crust builds into armour as it ages.",
                L(E(1, "tackle"), E(1, "bubblepop"), E(9, "fortify"), E(15, "tidalcrack"), E(21, "drizzledrain"), E(27, "scramble")),
                "wavelet", 26));

            // ---------------- Volt ----------------
            Add(new SpeciesDef("yolty", "Yolty", EggType.Volt,
                40, 54, 40, 62, 165, C(0xFFE45C), C(0xC28A00), 41,
                "Rolls nonstop to keep its charge topped up. Has never once sat still.",
                L(E(1, "tackle"), E(1, "staticsnap"), E(5, "chargeup"), E(11, "rollout"), E(17, "voltcrack"), E(23, "yolktackle")),
                "frizzlebolt", 17));

            Add(new SpeciesDef("frizzlebolt", "Frizzlebolt", EggType.Volt,
                52, 62, 48, 68, 100, C(0xFFC61A), C(0x6B4A00), 42,
                "Storm-born and permanently staticky. Hairline cracks light up before it pounces.",
                L(E(1, "tackle"), E(1, "staticsnap"), E(7, "chargeup"), E(13, "voltcrack"), E(19, "scramble"), E(25, "yolktackle")),
                "sparkshell", 26));

            // ---------------- Frost ----------------
            Add(new SpeciesDef("chillet", "Chillet", EggType.Frost,
                46, 48, 56, 44, 160, C(0xC9EEF7), C(0x4E93AD), 51,
                "Wears a tiny cap of frost that never melts, no matter how sunny it gets.",
                L(E(1, "tackle"), E(1, "chillshell"), E(6, "harden"), E(12, "coldsnap"), E(18, "frostcrack"), E(24, "restshell")),
                "glacegg", 18));

            Add(new SpeciesDef("glacegg", "Glacegg", EggType.Frost,
                58, 60, 64, 42, 95, C(0x9FDCEE), C(0x1F5E77), 52,
                "Clear as glacier ice. If you squint you can watch the yolk drifting inside.",
                L(E(1, "tackle"), E(1, "chillshell"), E(8, "fortify"), E(14, "frostcrack"), E(20, "coldsnap"), E(26, "scramble")),
                "snowpoach", 26));

            // ---------------- Stone ----------------
            Add(new SpeciesDef("cobblet", "Cobblet", EggType.Stone,
                52, 55, 66, 35, 170, C(0xC9AE8C), C(0x6B4E31), 61,
                "Utterly indistinguishable from a river pebble, right up until it blinks at you.",
                L(E(1, "tackle"), E(1, "pebbletoss"), E(5, "harden"), E(11, "rollout"), E(17, "bouldercrack"), E(23, "fortify")),
                "craggle", 18));

            Add(new SpeciesDef("craggle", "Craggle", EggType.Stone,
                64, 66, 74, 32, 90, C(0x9B8069), C(0x3B2C1D), 62,
                "Far heavier than it looks. Sinks instantly, then grumpily rolls back to shore.",
                L(E(1, "tackle"), E(1, "pebbletoss"), E(9, "fortify"), E(15, "bouldercrack"), E(21, "scramble"), E(27, "tectonic")),
                "boulderoo", 26));

            // ---------------- Aether ----------------
            Add(new SpeciesDef("nebulegg", "Nebulegg", EggType.Aether,
                45, 56, 46, 58, 130, C(0xD9A6FF), C(0x6B2FA8), 71,
                "Its shell shows a slow-turning nebula. The pattern has never once repeated.",
                L(E(1, "tackle"), E(1, "mindyolk"), E(6, "warpveil"), E(12, "astralcrack"), E(18, "restshell"), E(24, "yolktackle")),
                "cosmolette", 20));

            Add(new SpeciesDef("cosmolette", "Cosmolette", EggType.Aether,
                56, 68, 52, 62, 80, C(0xB57BF0), C(0x39146B), 72,
                "Hums one clear note, forever. Hatchers are fairly sure it is counting down.",
                L(E(1, "tackle"), E(1, "mindyolk"), E(8, "warpveil"), E(14, "astralcrack"), E(20, "scramble"), E(26, "omegashell")),
                "starlette", 26));

            // ---------------- Void ----------------
            Add(new SpeciesDef("duskle", "Duskle", EggType.Void,
                47, 60, 45, 55, 125, C(0x8B84C4), C(0x2A2350), 81,
                "Casts a shadow just a little bigger than itself. Nobody likes to mention it.",
                L(E(1, "tackle"), E(1, "shadepeck"), E(6, "harden"), E(12, "draindark"), E(18, "voidcrack"), E(24, "scramble")),
                "shadowhisk", 20));

            Add(new SpeciesDef("shadowhisk", "Shadowhisk", EggType.Void,
                60, 70, 55, 58, 70, C(0x5E5691), C(0x14102E), 82,
                "Light that lands on it does not come back. Pleasantly cool to hold, though.",
                L(E(1, "tackle"), E(1, "shadepeck"), E(9, "warmup"), E(15, "voidcrack"), E(21, "draindark"), E(27, "eventhorizon")),
                "gloomolk", 26));

            // ---------------- second wave: the Long Drift and the Shattered Belt ----------------
            Add(new SpeciesDef("sizzlette", "Sizzlette", EggType.Molten,
                48, 74, 42, 74, 90, C(0xFF7A2F), C(0x7A1A00), 23,
                "Too hot to hold for more than a second. Leaves scorch marks on the nest straw.",
                L(E(1, "embercrack"), E(1, "warmup"), E(10, "rollout"), E(16, "lavayolk"), E(22, "scramble"), E(28, "flareshell"))));

            Add(new SpeciesDef("wavelet", "Wavelet", EggType.Tidal,
                66, 52, 74, 40, 100, C(0x6FC3E8), C(0x0B4A70), 33,
                "Rolls in with the surf and refuses, politely but firmly, to roll back out.",
                L(E(1, "bubblepop"), E(1, "harden"), E(9, "fortify"), E(15, "drizzledrain"), E(21, "tidalcrack"), E(27, "deluge"))));

            Add(new SpeciesDef("bloomolk", "Bloomolk", EggType.Verdant,
                68, 70, 58, 44, 95, C(0x8FD46F), C(0x2A6B24), 13,
                "Flowers once a year, for about an hour. Hatchers plan whole trips around it.",
                L(E(1, "vinewhisk"), E(1, "harden"), E(10, "sproutslam"), E(16, "photorest"), E(22, "scramble"), E(28, "overgrowth"))));

            Add(new SpeciesDef("sparkshell", "Sparkshell", EggType.Volt,
                58, 66, 54, 64, 90, C(0xFFD84A), C(0x8A5E00), 43,
                "Hums near power lines. Nest Stations keep a padded box just for this one.",
                L(E(1, "staticsnap"), E(1, "chargeup"), E(10, "rollout"), E(16, "voltcrack"), E(22, "scramble"), E(28, "thunderyolk"))));

            Add(new SpeciesDef("snowpoach", "Snowpoach", EggType.Frost,
                52, 62, 50, 68, 95, C(0xDFF4FB), C(0x3E7E9C), 53,
                "Skates rather than rolls. Enormously pleased with itself about this.",
                L(E(1, "chillshell"), E(1, "coldsnap"), E(10, "rollout"), E(16, "frostcrack"), E(22, "scramble"), E(28, "whiteout"))));

            Add(new SpeciesDef("boulderoo", "Boulderoo", EggType.Stone,
                76, 68, 84, 28, 75, C(0x8C7359), C(0x2E2216), 63,
                "Hops. Somehow. Nobody has filmed it, but the craters are the right shape.",
                L(E(1, "pebbletoss"), E(1, "fortify"), E(10, "bouldercrack"), E(17, "harden"), E(23, "tectonic"), E(29, "scramble"))));

            Add(new SpeciesDef("starlette", "Starlette", EggType.Aether,
                50, 80, 46, 72, 70, C(0xE0B4FF), C(0x4B1A8C), 73,
                "Faintly warm and faintly wrong. Compasses near it point at it instead.",
                L(E(1, "mindyolk"), E(1, "warpveil"), E(10, "astralcrack"), E(17, "scramble"), E(23, "restshell"), E(29, "omegashell"))));

            Add(new SpeciesDef("gloomolk", "Gloomolk", EggType.Void,
                72, 72, 62, 50, 65, C(0x4C4478), C(0x0D0A22), 83,
                "Absorbs sound as well as light. A nest of them is unnervingly quiet.",
                L(E(1, "shadepeck"), E(1, "harden"), E(10, "draindark"), E(17, "voidcrack"), E(23, "scramble"), E(29, "eventhorizon"))));

            // ---------------- rival and Keeper aces (catchable only in theory) ----------------
            Add(new SpeciesDef("vesperling", "Vesperling", EggType.Void,
                70, 78, 66, 72, 8, C(0x7A6FB8), C(0x1A1440), 84,
                "Vess found it in the Belt and never explained how. It answers only to her.",
                L(E(1, "voidcrack"), E(1, "draindark"), E(1, "harden"), E(1, "eventhorizon"))));

            // ---------------- Amy's team (uncatchable) ----------------
            Add(new SpeciesDef("solyolk", "Solyolk", EggType.Molten,
                71, 69, 64, 62, 3, C(0xFFB13D), C(0x8F1D00), 91,
                "Amy's furnace. Rumoured to have hatched once already, and gone back in.",
                L(E(1, "lavayolk"), E(1, "flareshell"), E(1, "warmup"), E(1, "supernova"))));

            Add(new SpeciesDef("obsidyolk", "Obsidyolk", EggType.Stone,
                83, 71, 88, 46, 3, C(0x4A4550), C(0x111015), 92,
                "Volcanic glass wrapped around a molten core. Its chips are razor sharp.",
                L(E(1, "bouldercrack"), E(1, "fortify"), E(1, "tectonic"), E(1, "scramble"))));

            Add(new SpeciesDef("reginova", "Reginova", EggType.Aether,
                80, 81, 74, 78, 3, C(0xEBC7FF), C(0x5B1E9E), 93,
                "Amy's ace, and the only egg on record that chose its own trainer.",
                L(E(1, "astralcrack"), E(1, "omegashell"), E(1, "warpveil"), E(1, "eventhorizon"))));
        }
    }
}
