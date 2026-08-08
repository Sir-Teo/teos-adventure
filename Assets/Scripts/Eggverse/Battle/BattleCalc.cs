using UnityEngine;

namespace Eggverse
{
    public static class BattleCalc
    {
        public const float CritChance = 0.0625f;
        public const float CritMultiplier = 1.6f;

        /// <summary>Standard damage roll. Returns 0 when the move is a status move.</summary>
        /// <summary>
        /// Whether Tough Shell has anything to resist. Named because the card's arrow and this
        /// decision are two readings of one number, and they were two separate literals.
        /// </summary>
        /// <summary>
        /// The magnitudes the trait blurbs quote. Named so the sentence on the record page is
        /// built from the number the fight uses, rather than typed beside it and left to drift.
        /// </summary>
        public const float OverheatBoost = 1.3f, ToughShellResist = 0.75f;
        public const float StaticShare = 0.125f;

        public static bool ToughShellResists(float typeMultiplier) => TypeChart.IsStrong(typeMultiplier);

        public static int Damage(EggInstance attacker, EggInstance defender, MoveDef move,
                                 out float typeMultiplier, out bool critical)
        {
            typeMultiplier = TypeChart.Multiplier(move.Type, defender.Type);
            critical = false;
            if (move.IsStatus) return 0;

            float critChance = attacker.Trait == EggTrait.Lucky ? CritChance * 2.5f : CritChance;
            critical = EggRandom.Value < critChance;

            float stab = attacker.Type == move.Type ? 1.4f : 1f;
            float crit = critical ? CritMultiplier : 1f;
            float roll = EggRandom.Range(0.85f, 1.0f);

            // Passives: Overheat rewards fighting hurt, Tough Shell blunts type advantage.
            float traitBoost = attacker.Trait == EggTrait.Overheat && attacker.HPFraction < 0.34f ? OverheatBoost : 1f;
            float traitResist = defender.Trait == EggTrait.ToughShell && ToughShellResists(typeMultiplier) ? ToughShellResist : 1f;

            return Mathf.Max(1, Mathf.RoundToInt(
                TypicalDamage(attacker, defender, move) * crit * roll / AverageRoll));
        }

        /// <summary>The average of the variance roll, so a typical hit is the roll factored out.</summary>
        public const float AverageRoll = 0.925f;

        /// <summary>
        /// What a hit does before luck touches it: every term of the damage formula except the
        /// critical roll and the variance roll.
        ///
        /// Split out because Damage cannot run outside the engine — it rolls — and how long a
        /// fight lasts is the single most important number in a battle game and had never been
        /// measured because of it. Rewriting the formula in a measuring script would have been
        /// the same fault as a render typing out a panel: two copies, and the game moves.
        /// </summary>
        public static float TypicalDamage(EggInstance attacker, EggInstance defender, MoveDef move)
        {
            if (move.IsStatus) return 0f;

            float typeMultiplier = TypeChart.Multiplier(move.Type, defender.Type);
            float stab = attacker.Type == move.Type ? 1.4f : 1f;
            float traitBoost = attacker.Trait == EggTrait.Overheat && attacker.HPFraction < 0.34f ? OverheatBoost : 1f;
            float traitResist = defender.Trait == EggTrait.ToughShell && ToughShellResists(typeMultiplier) ? ToughShellResist : 1f;

            float baseDamage = ((2f * attacker.Level / 5f + 2f) * move.Power * attacker.Atk /
                                Mathf.Max(1, defender.Def)) / 28f + 2f;
            return baseDamage * stab * typeMultiplier * traitBoost * traitResist * AverageRoll;
        }

        public static bool Hits(MoveDef move) => EggRandom.Range(0, 100) < move.Accuracy;

        public static bool MoverGoesFirst(EggInstance a, EggInstance b)
        {
            if (a.Spd != b.Spd) return a.Spd > b.Spd;
            return EggRandom.Value < 0.5f;
        }

        /// <summary>Chance an egg carton succeeds. Weakening the target matters far more than luck.</summary>
        public static float CatchChance(EggInstance target)
        {
            float hpFactor = (3f * target.MaxHP - 2f * target.CurrentHP) / (3f * target.MaxHP);
            float rate = target.Species.CatchRate / 255f;
            float levelFactor = Mathf.Clamp01(1.2f - target.Level / 40f);
            float elderResist = target.Elder ? 0.45f : 1f;
            return Mathf.Clamp01(hpFactor * rate * levelFactor * 1.6f * elderResist);
        }

        /// <summary>Rolls a catch and reports how many times the carton wobbled, for drama.</summary>
        public static bool RollCatch(EggInstance target, out int shakes)
        {
            float p = CatchChance(target);
            if (EggRandom.Value < p) { shakes = 3; return true; }

            // Near misses wobble more.
            float perShake = Mathf.Pow(Mathf.Clamp01(p), 0.35f);
            shakes = 0;
            for (int i = 0; i < 3; i++)
            {
                if (EggRandom.Value >= perShake) break;
                shakes++;
            }
            return false;
        }

        public static float FleeChance(EggInstance mine, EggInstance foe)
        {
            float ratio = (mine.Spd - foe.Spd) / Mathf.Max(1f, foe.Spd);
            return Mathf.Clamp(0.4f + 0.35f * ratio, 0.25f, 0.95f);
        }

        /// <summary>
        /// How good a move looks: expected damage, roughly. Mirrors the passives the damage
        /// formula applies, so the chooser does not recommend moves the maths then blunts.
        /// </summary>
        /// <summary>The lingering condition a move leaves behind, if any.</summary>
        public static EggStatus RiderOf(MoveEffect effect)
        {
            switch (effect)
            {
                case MoveEffect.Scorch: return EggStatus.Scorched;
                case MoveEffect.Chill:  return EggStatus.Chilled;
                case MoveEffect.Daze:   return EggStatus.Dazed;
            }
            return EggStatus.None;
        }

        public static float AiScore(EggInstance user, EggInstance target, MoveDef move)
        {
            if (move.IsStatus)
            {
                // Worth something early, never worth spamming.
                switch (move.Effect)
                {
                    case MoveEffect.Heal50: return user.HPFraction < 0.4f ? 70f : 5f;
                    case MoveEffect.AtkUp: return user.AtkStage < 2 ? 32f : 2f;
                    case MoveEffect.DefUp: return user.DefStage < 2 ? 28f : 2f;
                    case MoveEffect.SpdUp: return user.SpdStage < 2 ? 26f : 2f;
                    // Pointless into a Hardhead.
                    case MoveEffect.SpdDownFoe: return target.Trait == EggTrait.Hardhead ? 1f : 20f;
                    default: return 10f;
                }
            }

            float stab = user.Type == move.Type ? 1.4f : 1f;
            float mult = TypeChart.Multiplier(move.Type, target.Type);

            // Tough Shell eats a quarter of any super-effective hit, so it is worth less here too.
            if (target.Trait == EggTrait.ToughShell && ToughShellResists(mult)) mult *= ToughShellResist;

            float hits = move.Effect == MoveEffect.MultiHit2 ? 2f : 1f;
            float boost = user.Trait == EggTrait.Overheat && user.HPFraction < 0.34f ? OverheatBoost : 1f;

            float score = move.Power * stab * mult * hits * (move.Accuracy / 100f) * boost;

            // Striking a Static egg costs you health; multi-hit moves pay that twice.
            if (target.Trait == EggTrait.Static) score *= hits > 1f ? 0.86f : 0.93f;

            // Recoil is real damage to yourself, and worse when you are nearly out.
            if (move.Effect == MoveEffect.Recoil25) score *= user.HPFraction < 0.3f ? 0.7f : 0.9f;

            // A lingering condition is worth a great deal early and nothing at all once it has
            // landed, or against an element that shrugs it off. Without this the AI reads these
            // as ordinary 75-power moves and throws them at an already-scorched target.
            var rider = RiderOf(move.Effect);
            if (rider != EggStatus.None && target.CanCatch(rider))
                score *= target.HPFraction > 0.5f ? 1.35f : 1.1f;

            return score;
        }
    }
}
