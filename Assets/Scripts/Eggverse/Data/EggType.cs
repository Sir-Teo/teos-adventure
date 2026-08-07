using System;
using System.Collections.Generic;
using UnityEngine;

namespace Eggverse
{
    /// <summary>Elemental types every egg and move belongs to. Plain is neutral both ways.</summary>
    public enum EggType
    {
        Plain,
        Molten,
        Tidal,
        Verdant,
        Volt,
        Frost,
        Stone,
        Aether,
        Void
    }

    /// <summary>A passive that is always on in battle. One per element.</summary>
    public enum EggTrait
    {
        None = 0,
        Overheat,      // more damage when badly hurt
        Featherlight,  // faster than its stats suggest
        WarmYolk,      // regenerates each round
        Static,        // punishes attackers
        ToughShell,    // blunts super-effective hits
        Sturdy,        // survives one knockout from full health
        Lucky,         // crits far more often
        Hardhead       // immune to stat drops
    }

    public static class TypeChart
    {
        // Attacker -> the two elements it hits for 1.85x.
        // Arranged as a ring where each element beats the next two: that guarantees every
        // element has exactly two targets and exactly two counters, so no type can dominate.
        // Ring order: Molten -> Frost -> Verdant -> Stone -> Void -> Volt -> Aether -> Tidal ->
        static readonly Dictionary<EggType, EggType[]> Strong = new Dictionary<EggType, EggType[]>
        {
            { EggType.Plain,   new EggType[0] },
            { EggType.Molten,  new[] { EggType.Frost, EggType.Verdant } },
            { EggType.Tidal,   new[] { EggType.Frost, EggType.Molten } },
            { EggType.Verdant, new[] { EggType.Stone, EggType.Void } },
            { EggType.Volt,    new[] { EggType.Aether, EggType.Tidal } },
            { EggType.Frost,   new[] { EggType.Stone, EggType.Verdant } },
            { EggType.Stone,   new[] { EggType.Void, EggType.Volt } },
            { EggType.Aether,  new[] { EggType.Molten, EggType.Tidal } },
            { EggType.Void,    new[] { EggType.Aether, EggType.Volt } },
        };

        // Attacker -> what shrugs it off (0.55x): the two elements that beat it, plus its own kind.
        static readonly Dictionary<EggType, EggType[]> Weak = new Dictionary<EggType, EggType[]>
        {
            { EggType.Plain,   new EggType[0] },
            { EggType.Molten,  new[] { EggType.Aether, EggType.Tidal, EggType.Molten } },
            { EggType.Tidal,   new[] { EggType.Aether, EggType.Volt, EggType.Tidal } },
            { EggType.Verdant, new[] { EggType.Frost, EggType.Molten, EggType.Verdant } },
            { EggType.Volt,    new[] { EggType.Stone, EggType.Void, EggType.Volt } },
            { EggType.Frost,   new[] { EggType.Molten, EggType.Tidal, EggType.Frost } },
            { EggType.Stone,   new[] { EggType.Frost, EggType.Verdant, EggType.Stone } },
            { EggType.Aether,  new[] { EggType.Void, EggType.Volt, EggType.Aether } },
            { EggType.Void,    new[] { EggType.Stone, EggType.Verdant, EggType.Void } },
        };

        static readonly Color[] Colors =
        {
            new Color32(0xD9, 0xD2, 0xC5, 0xFF), // Plain
            new Color32(0xFF, 0x6B, 0x35, 0xFF), // Molten
            new Color32(0x3F, 0xA9, 0xF5, 0xFF), // Tidal
            new Color32(0x5F, 0xD0, 0x68, 0xFF), // Verdant
            new Color32(0xFF, 0xD2, 0x3F, 0xFF), // Volt
            new Color32(0x8F, 0xE3, 0xF2, 0xFF), // Frost
            new Color32(0x88, 0x70, 0x58, 0xFF), // Stone — darkened so it separates from
                                                 // Verdant under deuteranopia (ΔE 10 -> 20)
            new Color32(0xC7, 0x7D, 0xFF, 0xFF), // Aether
            new Color32(0x6C, 0x63, 0xA6, 0xFF), // Void
        };

        public static float Multiplier(EggType attack, EggType defend)
        {
            var strong = Strong[attack];
            for (int i = 0; i < strong.Length; i++)
                if (strong[i] == defend) return 1.85f;

            var weak = Weak[attack];
            for (int i = 0; i < weak.Length; i++)
                if (weak[i] == defend) return 0.55f;

            return 1f;
        }

        public static Color ColorOf(EggType t) => Colors[(int)t];

        static EggType[] Where(Func<EggType, bool> predicate)
        {
            var list = new List<EggType>();
            foreach (EggType t in System.Enum.GetValues(typeof(EggType)))
                if (t != EggType.Plain && predicate(t)) list.Add(t);
            return list.ToArray();
        }

        /// <summary>What this element's own attacks crush.</summary>
        public static EggType[] StrongAgainst(EggType t) => Where(d => d != t && Multiplier(t, d) > 1.2f);

        /// <summary>What crushes this element in return.</summary>
        public static EggType[] VulnerableTo(EggType t) => Where(a => a != t && Multiplier(a, t) > 1.2f);

        /// <summary>What bounces off it.</summary>
        public static EggType[] Resists(EggType t) => Where(a => a != t && Multiplier(a, t) < 0.8f);

        /// <summary>Comma-joined tags for the collection screen.</summary>
        public static string Join(EggType[] types)
        {
            if (types == null || types.Length == 0) return "nothing";
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < types.Length; i++)
            {
                if (i > 0) sb.Append("  ");
                sb.Append("<color=#").Append(ColorUtility.ToHtmlStringRGB(ColorOf(types[i])))
                  .Append(">").Append(Abbrev(types[i])).Append("</color>");
            }
            return sb.ToString();
        }

        public static string Name(EggType t) => t.ToString().ToUpperInvariant();

        /// <summary>Three-letter tag, so type is never conveyed by colour alone.</summary>
        public static string Abbrev(EggType t)
        {
            switch (t)
            {
                case EggType.Molten: return "MLT";
                case EggType.Tidal: return "TDL";
                case EggType.Verdant: return "VRD";
                case EggType.Volt: return "VLT";
                case EggType.Frost: return "FRS";
                case EggType.Stone: return "STN";
                case EggType.Aether: return "AET";
                case EggType.Void: return "VOI";
                default: return "PLN";
            }
        }

        /// <summary>
        /// Every egg of a type shares its passive. Tying traits to types rather than to
        /// individual species keeps them learnable: once you know what Frost does, you know
        /// it for every Frost egg you ever meet.
        /// </summary>
        public static EggTrait TraitOf(EggType t)
        {
            switch (t)
            {
                case EggType.Molten: return EggTrait.Overheat;
                case EggType.Tidal: return EggTrait.Featherlight;
                case EggType.Verdant: return EggTrait.WarmYolk;
                case EggType.Volt: return EggTrait.Static;
                case EggType.Frost: return EggTrait.ToughShell;
                case EggType.Stone: return EggTrait.Sturdy;
                case EggType.Aether: return EggTrait.Lucky;
                case EggType.Void: return EggTrait.Hardhead;
                default: return EggTrait.None;
            }
        }

        public static string TraitName(EggTrait trait)
        {
            switch (trait)
            {
                case EggTrait.Overheat: return "Overheat";
                case EggTrait.Featherlight: return "Featherlight";
                case EggTrait.WarmYolk: return "Warm Yolk";
                case EggTrait.Static: return "Static";
                case EggTrait.ToughShell: return "Tough Shell";
                case EggTrait.Sturdy: return "Sturdy";
                case EggTrait.Lucky: return "Lucky";
                case EggTrait.Hardhead: return "Hardhead";
                default: return "None";
            }
        }

        public static string TraitBlurb(EggTrait trait)
        {
            switch (trait)
            {
                case EggTrait.Overheat: return "Hits 30% harder below a third health.";
                case EggTrait.Featherlight: return "15% faster than its bulk suggests.";
                case EggTrait.WarmYolk: return "Mends a little every round.";
                case EggTrait.Static: return "Attackers take an eighth of the damage back.";
                case EggTrait.ToughShell: return "Takes a quarter less from super-effective hits.";
                case EggTrait.Sturdy: return "Survives a knockout from full health with 1 HP.";
                case EggTrait.Lucky: return "Cracks critically far more often.";
                case EggTrait.Hardhead: return "Its stats cannot be lowered.";
                default: return "";
            }
        }

        /// <summary>Flavour line shown after a hit lands.</summary>
        public static string EffectivenessLine(float mult)
        {
            if (mult > 1.2f) return "It cracked right through!";
            if (mult < 0.8f) return "The shell barely dented...";
            return null;
        }
    }
}
