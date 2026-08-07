using System.Collections.Generic;
using UnityEngine;

namespace Eggverse
{
    /// <summary>
    /// A lingering battle condition. One at a time, no natural recovery, gone when the fight is.
    /// Each is dealt out by one element and cannot be caught by an egg of that element.
    /// </summary>
    public enum EggStatus
    {
        None,
        Scorched,   // Molten: burns off a sixteenth of its bulk at the end of every round
        Chilled,    // Frost: moves at half speed
        Dazed,      // Volt: sometimes loses the turn outright
    }

    public class MoveSlot
    {
        public readonly MoveDef Move;
        public int PP;
        public MoveSlot(MoveDef m) { Move = m; PP = m.MaxPP; }
        public bool Usable => PP > 0;
        public void Restore() { PP = Move.MaxPP; }
    }

    /// <summary>One concrete egg in the world: a species plus a level, HP, moves and battle stages.</summary>
    public class EggInstance
    {
        public const int MaxMoves = 4;
        public const int MaxLevel = 30;

        public SpeciesDef Species { get; private set; }
        public string Nickname;
        public int Level { get; private set; }
        public int Xp { get; private set; }
        public int CurrentHP;
        public readonly List<MoveSlot> Moves = new List<MoveSlot>();

        // Battle-only modifiers, cleared on switch-out and at battle end.
        public int AtkStage, DefStage, SpdStage;

        /// <summary>
        /// A condition that lasts the rest of the battle. Every other move effect in the game
        /// resolves the instant it lands, which makes every turn a self-contained trade; these
        /// are the ones you are still paying for three turns later.
        ///
        /// Not saved: they clear when the battle ends, so a run never carries one home.
        /// </summary>
        public EggStatus Status;
        public int StatusTurns;

        /// <summary>
        /// How long a condition sticks. Permanent ones wrecked the long fights: a boss match
        /// runs about twenty rounds, and a burn at a sixteenth a round is 119% of the target's
        /// health over that — it did not add tactics, it decided the fight. Four rounds is a
        /// quarter of a bar, worth landing and worth landing again.
        /// </summary>
        public const int StatusDuration = 3;

        /// <summary>A rare, older wild egg: tougher, worth more, and much harder to keep.</summary>
        public bool Elder { get; private set; }

        /// <summary>
        /// The egg Ori hands you in the opening brief - "It's been yours since it was the size
        /// of a thumbnail anyway." It was an ordinary Sprouteg, so the moment you caught a
        /// second one, or it grew into something else, nothing in the game knew which one it
        /// was. Survives evolution, because the instance does.
        /// </summary>
        public bool FromOri { get; private set; }

        public void MarkFromOri() { FromOri = true; }

        public string Name =>
            !string.IsNullOrEmpty(Nickname) ? Nickname
            : Elder ? "Elder " + Species.Name
            : Species.Name;
        public EggType Type => Species.Type;
        public EggTrait Trait => TypeChart.TraitOf(Species.Type);
        public bool IsFainted => CurrentHP <= 0;
        public float HPFraction => MaxHP <= 0 ? 0f : Mathf.Clamp01(CurrentHP / (float)MaxHP);

        public EggInstance(SpeciesDef species, int level)
        {
            Species = species;
            Level = Mathf.Clamp(level, 1, MaxLevel);
            RebuildMovesForLevel();
            CurrentHP = MaxHP;
        }

        // ---------- stats ----------

        public int MaxHP => Mathf.Max(1, Species.BaseHP * Level / 10 + Level + 14);
        public int RawAtk => Species.BaseAtk * Level / 22 + 5;
        public int RawDef => Species.BaseDef * Level / 22 + 5;
        public int RawSpd => Species.BaseSpd * Level / 22 + 5;

        public int Atk => Mathf.Max(1, Mathf.RoundToInt(RawAtk * StageMul(AtkStage)));
        public int Def => Mathf.Max(1, Mathf.RoundToInt(RawDef * StageMul(DefStage)));
        public int Spd
        {
            get
            {
                float speed = RawSpd * StageMul(SpdStage);
                if (Trait == EggTrait.Featherlight) speed *= 1.15f;
                if (Status == EggStatus.Chilled) speed *= 0.5f;
                return Mathf.Max(1, Mathf.RoundToInt(speed));
            }
        }

        /// <summary>Lowers a stat stage unless this egg's trait forbids it. Returns true if it moved.</summary>
        public bool TryLowerStage(ref int stage)
        {
            if (Trait == EggTrait.Hardhead) return false;
            if (stage <= -6) return false;
            stage--;
            return true;
        }

        /// <summary>Applies damage, honouring Sturdy. Returns the amount actually dealt.</summary>
        public int TakeHit(int amount)
        {
            if (Trait == EggTrait.Sturdy && CurrentHP == MaxHP && amount >= CurrentHP)
                amount = CurrentHP - 1;
            return TakeDamage(amount);
        }

        /// <summary>End-of-round regeneration for Warm Yolk. Returns how much was mended.</summary>
        public int TickRegen()
        {
            if (Trait != EggTrait.WarmYolk || IsFainted || CurrentHP >= MaxHP) return 0;
            int before = CurrentHP;
            Heal(Mathf.Max(1, MaxHP / 16));
            return CurrentHP - before;
        }

        public static float StageMul(int stage)
        {
            stage = Mathf.Clamp(stage, -6, 6);
            return stage >= 0 ? (2f + stage) / 2f : 2f / (2f - stage);
        }

        public void ClearStages() { AtkStage = DefStage = SpdStage = 0; }
        public void ClearStatus() { Status = EggStatus.None; StatusTurns = 0; }

        public void Afflict(EggStatus status)
        {
            Status = status;
            StatusTurns = StatusDuration;
        }

        /// <summary>Counts a condition down at the end of a round. True when it has just worn off.</summary>
        public bool TickStatus()
        {
            if (Status == EggStatus.None) return false;
            if (--StatusTurns > 0) return false;
            ClearStatus();
            return true;
        }

        /// <summary>An egg cannot catch the condition its own element deals out.</summary>
        public bool CanCatch(EggStatus status)
        {
            if (status == EggStatus.None || Status != EggStatus.None || IsFainted) return false;
            switch (status)
            {
                case EggStatus.Scorched: return Type != EggType.Molten;
                case EggStatus.Chilled:  return Type != EggType.Frost;
                case EggStatus.Dazed:    return Type != EggType.Volt;
            }
            return false;
        }

        /// <summary>Damage taken at the end of a round from a lingering condition, or zero.</summary>
        public int StatusTickDamage() =>
            Status == EggStatus.Scorched ? Mathf.Max(1, MaxHP / 16) : 0;

        // ---------- health ----------

        public void Heal(int amount)
        {
            CurrentHP = Mathf.Clamp(CurrentHP + amount, 0, MaxHP);
        }

        public void FullRestore()
        {
            CurrentHP = MaxHP;
            ClearStages();
            for (int i = 0; i < Moves.Count; i++) Moves[i].Restore();
        }

        /// <summary>Returns damage actually applied.</summary>
        public int TakeDamage(int amount)
        {
            int before = CurrentHP;
            CurrentHP = Mathf.Clamp(CurrentHP - Mathf.Max(0, amount), 0, MaxHP);
            return before - CurrentHP;
        }

        // ---------- experience ----------

        // Roughly two battles per level, all the way up: enough that a 14-planet run
        // has room to breathe, without ever turning into a grind wall.
        public int XpToNext => Level >= MaxLevel ? 0 : 24 + Level * 24;

        /// <summary>Adds XP and reports every level gained, plus any moves learned along the way.</summary>
        public void GainXp(int amount, List<string> log)
        {
            EvolvedThisLevelUp = false;
            if (Level >= MaxLevel) return;
            Xp += Mathf.Max(0, amount);

            while (Level < MaxLevel && Xp >= XpToNext)
            {
                Xp -= XpToNext;
                int hpBefore = MaxHP, atkBefore = RawAtk, defBefore = RawDef, spdBefore = RawSpd;
                Level++;
                // Levelling up grants the HP difference so the egg is not suddenly hurt.
                CurrentHP += MaxHP - hpBefore;

                // Say what actually improved. "Grew to level 12!" is the payoff for every fight
                // in the game and it used to report nothing about the payoff - and because the
                // stats step on integer division, some levels genuinely move only one of them.
                if (log != null)
                {
                    log.Add(Name + " grew to level " + Level + "!");
                    string gains = StatGains(MaxHP - hpBefore, RawAtk - atkBefore,
                                             RawDef - defBefore, RawSpd - spdBefore);
                    if (gains.Length > 0) log.Add(gains);
                }
                LearnMovesForLevel(Level, log);
                TryEvolve(log);
            }

            if (Level >= MaxLevel) Xp = 0;
        }

        /// <summary>
        /// The one-line stat report shown under a level-up. Only what moved is listed, so a
        /// level that lifts a single stat reads as exactly that rather than a wall of "+0".
        /// </summary>
        static string StatGains(int hp, int atk, int def, int spd)
        {
            var parts = new List<string>();
            if (hp > 0) parts.Add("HP +" + hp);
            if (atk > 0) parts.Add("ATK +" + atk);
            if (def > 0) parts.Add("DEF +" + def);
            if (spd > 0) parts.Add("SPD +" + spd);
            return parts.Count == 0 ? "" : string.Join("   ", parts.ToArray());
        }

        /// <summary>True on the level-up where this egg changed species.</summary>
        public bool EvolvedThisLevelUp { get; private set; }

        void TryEvolve(List<string> log)
        {
            if (!Species.CanEvolve || Level < Species.EvolveLevel) return;

            var next = SpeciesDatabase.Get(Species.EvolvesIntoId);
            if (next == null || next.Id == Species.Id) return;

            string before = Name;
            float hpFraction = HPFraction;
            bool wasNicknamed = !string.IsNullOrEmpty(Nickname);

            Species = next;
            // Keep the same proportion of health across the new, larger HP pool.
            CurrentHP = Mathf.Max(1, Mathf.RoundToInt(MaxHP * hpFraction));
            EvolvedThisLevelUp = true;

            if (log != null)
                log.Add(before + " is cracking... " + before + " became " +
                        (wasNicknamed ? Nickname + " the " + next.Name : next.Name) + "!");
        }

        /// <summary>What this egg turns into next, for the collection screen. Null if it is final.</summary>
        public string EvolutionHint()
        {
            if (!Species.CanEvolve) return null;
            var next = SpeciesDatabase.Get(Species.EvolvesIntoId);
            return next.Name + " at Lv " + Species.EvolveLevel;
        }

        public int XpRewardFor()
        {
            // What a defeated egg is worth to the winner.
            int reward = Level * 12 + 6 + Species.BaseTotal / 12;
            return Elder ? Mathf.RoundToInt(reward * 1.7f) : reward;
        }

        // ---------- moves ----------

        void RebuildMovesForLevel()
        {
            Moves.Clear();
            var set = Species.Learnset;
            // Walk the learnset in order and keep the last four moves available at this level.
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i].Level > Level) continue;
                AddOrReplace(MoveDatabase.Get(set[i].MoveId), null);
            }
            if (Moves.Count == 0) Moves.Add(new MoveSlot(MoveDatabase.Get("tackle")));
        }

        void LearnMovesForLevel(int level, List<string> log)
        {
            var set = Species.Learnset;
            for (int i = 0; i < set.Length; i++)
            {
                if (set[i].Level != level) continue;
                var move = MoveDatabase.Get(set[i].MoveId);
                if (KnowsMove(move.Id)) continue;
                string replaced = AddOrReplace(move, log);
                if (log == null) continue;
                log.Add(replaced == null
                    ? Name + " learned " + move.Name + "!"
                    : Name + " forgot " + replaced + " and learned " + move.Name + "!");
            }
        }

        public bool KnowsMove(string moveId)
        {
            for (int i = 0; i < Moves.Count; i++)
                if (Moves[i].Move.Id == moveId) return true;
            return false;
        }

        /// <summary>Adds a move; if full, drops the weakest damaging move. Returns the forgotten move's name.</summary>
        string AddOrReplace(MoveDef move, List<string> log)
        {
            if (KnowsMove(move.Id)) return null;

            if (Moves.Count < MaxMoves)
            {
                Moves.Add(new MoveSlot(move));
                return null;
            }

            int worst = 0;
            for (int i = 1; i < Moves.Count; i++)
                if (Moves[i].Move.Power < Moves[worst].Move.Power) worst = i;

            // Never trade a strong move for a weaker one.
            if (Moves[worst].Move.Power >= move.Power && !move.IsStatus) return null;

            string forgotten = Moves[worst].Move.Name;
            Moves[worst] = new MoveSlot(move);
            return forgotten;
        }

        public bool HasUsableMove()
        {
            for (int i = 0; i < Moves.Count; i++)
                if (Moves[i].Usable) return true;
            return false;
        }

        // ---------- creation helpers ----------

        public static EggInstance Wild(string speciesId, int level)
        {
            return new EggInstance(SpeciesDatabase.Get(speciesId), level);
        }

        /// <summary>An elder of the species: three levels older, and it knows it.</summary>
        public static EggInstance WildElder(string speciesId, int level)
        {
            var egg = new EggInstance(SpeciesDatabase.Get(speciesId), Mathf.Min(MaxLevel, level + ElderLevelBonus));
            egg.Elder = true;
            egg.CurrentHP = egg.MaxHP;
            return egg;
        }

        /// <summary>Roughly one in twelve wild eggs. Never on the boss world.</summary>
        public const float ElderChance = 0.085f;
        /// <summary>How many levels an Elder is above its neighbours. Garrow says this out
        /// loud on Cairnhold, so it is a constant rather than a literal in two places.</summary>
        public const int ElderLevelBonus = 3;

        /// <summary>Rebuilds a saved egg exactly, including its remembered moves and PP.</summary>
        public static EggInstance Restore(string speciesId, string nickname, int level, int xp, int currentHP,
                                          string[] moveIds, int[] movePP, bool elder = false, bool fromOri = false)
        {
            var egg = new EggInstance(SpeciesDatabase.Get(speciesId), level);
            egg.Elder = elder;
            egg.Nickname = string.IsNullOrEmpty(nickname) ? null : nickname;
            egg.Xp = Mathf.Max(0, xp);

            if (moveIds != null && moveIds.Length > 0)
            {
                egg.Moves.Clear();
                for (int i = 0; i < moveIds.Length && egg.Moves.Count < MaxMoves; i++)
                {
                    if (!MoveDatabase.Has(moveIds[i])) continue;
                    var slot = new MoveSlot(MoveDatabase.Get(moveIds[i]));
                    if (movePP != null && i < movePP.Length) slot.PP = Mathf.Clamp(movePP[i], 0, slot.Move.MaxPP);
                    egg.Moves.Add(slot);
                }
                if (egg.Moves.Count == 0) egg.Moves.Add(new MoveSlot(MoveDatabase.Get("tackle")));
            }

            egg.CurrentHP = Mathf.Clamp(currentHP, 0, egg.MaxHP);
            egg.FromOri = fromOri;
            return egg;
        }
    }
}
