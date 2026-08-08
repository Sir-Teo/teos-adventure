using System.Collections.Generic;

namespace Eggverse
{
    public enum MoveEffect
    {
        None,
        Heal50,      // restores half of the user's max HP
        AtkUp,       // +1 attack stage for the user
        DefUp,       // +1 defence stage for the user
        SpdUp,       // +1 speed stage for the user
        SpdDownFoe,  // -1 speed stage for the target
        Lifesteal50, // user recovers half the damage dealt
        MultiHit2,   // strikes twice
        Recoil25,    // user takes a quarter of the damage dealt
        Scorch,      // Molten: leaves the target burning for the rest of the fight
        Chill,       // Frost: halves the target's speed for the rest of the fight
        Daze         // Volt: the target sometimes loses its turn
    }

    public class MoveDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly EggType Type;
        public readonly int Power;     // 0 = status move, deals no damage
        public readonly int Accuracy;  // percent
        public readonly int MaxPP;
        public readonly MoveEffect Effect;
        public readonly string Blurb;

        public MoveDef(string id, string name, EggType type, int power, int accuracy, int pp,
                       MoveEffect effect = MoveEffect.None, string blurb = "")
        {
            Id = id; Name = name; Type = type; Power = power;
            Accuracy = accuracy; MaxPP = pp; Effect = effect; Blurb = blurb;
        }

        public bool IsStatus => Power <= 0;

        /// <summary>
        /// One line for the battle menu. Uses the authored blurb where there is one, and
        /// otherwise says something true about what the move actually does.
        /// </summary>
        /// <summary>The lingering condition this move leaves, in plain words, or null.</summary>
        public string RiderNote()
        {
            switch (Effect)
            {
                // Spelled from the constant, not written next to it. "three rounds" beside a
                // StatusDuration of 3 is true until somebody changes the 3.
                case MoveEffect.Scorch: return "Scorches the target: it burns for " + Rounds() + ".";
                case MoveEffect.Chill:  return "Chills the target: half speed for " + Rounds() + ".";
                case MoveEffect.Daze:   return "Dazes the target: it may skip a turn for " + Rounds() + ".";
            }
            return null;
        }

        static string Rounds()
        {
            int n = EggInstance.StatusDuration;
            return Words.Spell(n) + (n == 1 ? " round" : " rounds");
        }

        /// <summary>
        /// The shares three of these effects deal in. The names encode them — Lifesteal50,
        /// Recoil25, Heal50 — and the sentences quoted them in words, and the fight divided by
        /// 2 and 4 in a third place. Three statements of one number with nothing holding them
        /// together, on the line a player reads while choosing the move.
        /// </summary>
        public const float LifestealShare = 0.5f, RecoilShare = 0.25f, SelfHealShare = 0.5f;

        public string Describe()
        {
            // The message box holds 60 characters, which is not enough for flavour and
            // mechanics both. The rider is the part the player has to act on, so it wins.
            string rider = RiderNote();
            if (rider != null) return rider;

            if (!string.IsNullOrEmpty(Blurb)) return Blurb;

            switch (Effect)
            {
                case MoveEffect.MultiHit2: return "Strikes twice in a row.";
                case MoveEffect.Lifesteal50:
                    return "Gives back " + Words.Fraction(LifestealShare) + " the damage as health.";
                case MoveEffect.Recoil25:
                    return "Costs the user " + Words.Fraction(RecoilShare) + " of the damage dealt.";
                case MoveEffect.SpdDownFoe: return "Slows the target down.";
                case MoveEffect.Heal50:
                    return "Restores " + Words.Fraction(SelfHealShare) + " of the user's health.";
                case MoveEffect.AtkUp: return "Raises the user's attack.";
                case MoveEffect.DefUp: return "Raises the user's defence.";
                case MoveEffect.SpdUp: return "Raises the user's speed.";
            }

            if (Accuracy < 90) return "Hits hard, when it hits at all.";
            return Power >= 90 ? "A heavy blow." : Power >= 70 ? "A solid hit." : "A quick, reliable strike.";
        }
    }

    public static class MoveDatabase
    {
        static readonly Dictionary<string, MoveDef> byId = new Dictionary<string, MoveDef>();
        static readonly List<MoveDef> ordered = new List<MoveDef>();

        /// <summary>Every move, in declaration order — the self-check needs to sweep them.</summary>
        public static IReadOnlyList<MoveDef> All => ordered;

        public static MoveDef Get(string id)
        {
            MoveDef m;
            return byId.TryGetValue(id, out m) ? m : byId["tackle"];
        }

        public static bool Has(string id) => byId.ContainsKey(id);

        static void Add(MoveDef m) { byId[m.Id] = m; ordered.Add(m); }

        static MoveDatabase()
        {
            // ---- Plain: available to nearly everything ----
            Add(new MoveDef("tackle", "Shell Bash", EggType.Plain, 40, 100, 35, MoveEffect.None, "A blunt headbutt."));
            Add(new MoveDef("yolktackle", "Yolk Tackle", EggType.Plain, 60, 95, 25, MoveEffect.None, "A full-body slam."));
            Add(new MoveDef("rollout", "Roll Out", EggType.Plain, 30, 90, 20, MoveEffect.MultiHit2, "Rolls through twice."));
            Add(new MoveDef("harden", "Harden", EggType.Plain, 0, 100, 20, MoveEffect.DefUp, "Thickens the shell."));
            Add(new MoveDef("warmup", "Warm Up", EggType.Plain, 0, 100, 20, MoveEffect.AtkUp, "Raises internal heat."));
            Add(new MoveDef("restshell", "Rest Shell", EggType.Plain, 0, 100, 10, MoveEffect.Heal50, "Naps to mend cracks."));
            Add(new MoveDef("scramble", "Scramble", EggType.Plain, 85, 85, 10, MoveEffect.Recoil25, "Reckless full-force hit."));

            // ---- Molten ----
            Add(new MoveDef("embercrack", "Ember Crack", EggType.Molten, 45, 100, 25, MoveEffect.None,
                            "A hairline split that glows before it opens."));
            Add(new MoveDef("lavayolk", "Lava Yolk", EggType.Molten, 75, 95, 15, MoveEffect.Scorch,
                            "Sticks and keeps burning."));
            Add(new MoveDef("flareshell", "Flare Shell", EggType.Molten, 100, 80, 8, MoveEffect.None,
                            "The shell goes white-hot for exactly as long as it takes."));

            // ---- Tidal ----
            Add(new MoveDef("bubblepop", "Bubble Pop", EggType.Tidal, 45, 100, 25, MoveEffect.None,
                            "Small, wet, and closer to the eye than anybody likes."));
            Add(new MoveDef("tidalcrack", "Tidal Crack", EggType.Tidal, 75, 95, 15, MoveEffect.None,
                            "Water finds the seam. Water always finds the seam."));
            Add(new MoveDef("drizzledrain", "Drizzle Drain", EggType.Tidal, 55, 100, 15, MoveEffect.Lifesteal50, "Siphons moisture back."));

            // ---- Verdant ----
            Add(new MoveDef("vinewhisk", "Vine Whisk", EggType.Verdant, 45, 100, 25, MoveEffect.None,
                            "A shoot, whipped. It stings more than it has any right to."));
            Add(new MoveDef("sproutslam", "Sprout Slam", EggType.Verdant, 75, 95, 15, MoveEffect.None,
                            "All that stored growth, spent in one go."));
            Add(new MoveDef("photorest", "Photo Rest", EggType.Verdant, 0, 100, 10, MoveEffect.Heal50, "Basks to regrow shell."));

            // ---- Volt ----
            Add(new MoveDef("staticsnap", "Static Snap", EggType.Volt, 45, 100, 25, MoveEffect.None,
                            "The air tightens, and then it lets go."));
            Add(new MoveDef("voltcrack", "Volt Crack", EggType.Volt, 75, 95, 15, MoveEffect.Daze,
                            "Rattles the yolk about."));
            Add(new MoveDef("chargeup", "Charge Up", EggType.Volt, 0, 100, 20, MoveEffect.SpdUp, "Builds a static charge."));

            // ---- Frost ----
            Add(new MoveDef("chillshell", "Chill Shell", EggType.Frost, 45, 100, 25, MoveEffect.None,
                            "Frost across the surface, and everything under it slows."));
            Add(new MoveDef("frostcrack", "Frost Crack", EggType.Frost, 75, 95, 15, MoveEffect.Chill,
                            "Leaves a rime that will not shift."));
            Add(new MoveDef("coldsnap", "Cold Snap", EggType.Frost, 55, 95, 15, MoveEffect.SpdDownFoe, "Numbs the target."));

            // ---- Stone ----
            Add(new MoveDef("pebbletoss", "Pebble Toss", EggType.Stone, 45, 100, 25, MoveEffect.None,
                            "Unhurried, accurate, and somehow always the same pebble."));
            Add(new MoveDef("bouldercrack", "Boulder Crack", EggType.Stone, 85, 85, 12, MoveEffect.None,
                            "Weight, arriving. There is no trick to it at all."));
            Add(new MoveDef("fortify", "Fortify", EggType.Stone, 0, 100, 20, MoveEffect.DefUp, "Packs on mineral plating."));

            // ---- Aether ----
            Add(new MoveDef("mindyolk", "Mind Yolk", EggType.Aether, 45, 100, 25, MoveEffect.None,
                            "Pressure behind the eyes that was not there a moment ago."));
            Add(new MoveDef("astralcrack", "Astral Crack", EggType.Aether, 75, 95, 15, MoveEffect.None,
                            "A line opens somewhere that is not quite here."));
            Add(new MoveDef("warpveil", "Warp Veil", EggType.Aether, 0, 100, 20, MoveEffect.SpdUp, "Bends space to move first."));

            // ---- Void ----
            Add(new MoveDef("shadepeck", "Shade Peck", EggType.Void, 45, 100, 25, MoveEffect.None,
                            "Something takes a piece and does not show itself."));
            Add(new MoveDef("voidcrack", "Void Crack", EggType.Void, 75, 95, 15, MoveEffect.None,
                            "Nothing visible happens. The damage is done anyway."));
            Add(new MoveDef("draindark", "Drain Dark", EggType.Void, 55, 100, 15, MoveEffect.Lifesteal50, "Drinks the target's warmth."));

            // ---- Boss-tier ----
            Add(new MoveDef("supernova", "Supernova", EggType.Molten, 110, 90, 5, MoveEffect.Recoil25, "Amy's signature burn."));
            Add(new MoveDef("eventhorizon", "Event Horizon", EggType.Void, 95, 95, 8, MoveEffect.Lifesteal50, "Swallows all light."));
            Add(new MoveDef("omegashell", "Omega Shell", EggType.Aether, 105, 90, 6, MoveEffect.None, "A perfect, final shell."));
            Add(new MoveDef("tectonic", "Tectonic", EggType.Stone, 100, 85, 8, MoveEffect.None, "Splits the crust open."));

            // Obsidyolk's own. It is the rarest egg in the game — catch rate three, the heaviest
            // shell, the lowest voice — and it arrived knowing four moves every other Stone egg
            // also knows, so the one creature a player might spend twenty cartons on played
            // exactly like the one they met on their second world. Its blurb has said "its chips
            // are razor sharp" since it was written; nobody had made that a thing it does.
            Add(new MoveDef("glassshatter", "Glass Shatter", EggType.Stone, 55, 90, 10,
                            MoveEffect.MultiHit2, "Sheds a shower of glass. Twice over."));

            // Four elements had a hundred-power finisher and four stopped at seventy-five, so
            // half the roster's late game was strictly weaker than the other half's. These are
            // the missing four, on the same terms: high power, thinner accuracy, few uses.
            Add(new MoveDef("deluge", "Deluge", EggType.Tidal, 100, 85, 8, MoveEffect.None,
                            "Brings the whole shelf down at once."));
            Add(new MoveDef("overgrowth", "Overgrowth", EggType.Verdant, 95, 90, 8, MoveEffect.Recoil25,
                            "Everything blooms, including what should not."));
            Add(new MoveDef("thunderyolk", "Thunderyolk", EggType.Volt, 100, 85, 8, MoveEffect.Daze,
                            "One strike, straight down."));
            Add(new MoveDef("whiteout", "Whiteout", EggType.Frost, 100, 85, 8, MoveEffect.Chill,
                            "The horizon disappears and does not come back."));
        }
    }
}
