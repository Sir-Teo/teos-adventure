using System;
using System.Collections.Generic;
using Eggverse;

/// Plays real battles against the shipping formulas (BattleCalc, the AI scorer, the type
/// chart, PP, stat stages and effects) so the level curve can be measured rather than guessed.
static class Balance
{
    class Side
    {
        public List<EggInstance> Team;
        public int Active;
        public EggInstance Egg => Team[Active];
        public bool AnyAlive { get { foreach (var e in Team) if (!e.IsFainted) return true; return false; } }
        public bool SwapToHealthy()
        {
            for (int i = 0; i < Team.Count; i++)
                if (!Team[i].IsFainted) { Active = i; return true; }
            return false;
        }
    }

    /// Both sides pick the move the AI scorer rates highest — i.e. competent play.
    static MoveSlot Choose(EggInstance user, EggInstance target)
    {
        MoveSlot best = null; float bestScore = float.MinValue;
        foreach (var slot in user.Moves)
        {
            if (!slot.Usable) continue;
            float score = BattleCalc.AiScore(user, target, slot.Move);
            if (score > bestScore) { bestScore = score; best = slot; }
        }
        return best;
    }

    static void Apply(EggInstance user, EggInstance target, MoveSlot slot)
    {
        if (user.Status == EggStatus.Dazed && EggRandom.Value < 0.25f) return;   // lost the turn

        if (slot == null) { target.TakeDamage(Math.Max(1, user.Atk / 4)); return; }
        var move = slot.Move;
        slot.PP--;
        if (!BattleCalc.Hits(move)) return;

        if (move.IsStatus)
        {
            switch (move.Effect)
            {
                case MoveEffect.Heal50: user.Heal(user.MaxHP / 2); break;
                case MoveEffect.AtkUp: user.AtkStage = Math.Min(6, user.AtkStage + 1); break;
                case MoveEffect.DefUp: user.DefStage = Math.Min(6, user.DefStage + 1); break;
                case MoveEffect.SpdUp: user.SpdStage = Math.Min(6, user.SpdStage + 1); break;
                case MoveEffect.SpdDownFoe: { int st = target.SpdStage; if (target.TryLowerStage(ref st)) target.SpdStage = st; break; }
            }
            return;
        }

        int hits = move.Effect == MoveEffect.MultiHit2 ? 2 : 1;
        int dealt = 0;
        for (int h = 0; h < hits && !target.IsFainted; h++)
        {
            int raw = BattleCalc.Damage(user, target, move, out _, out _);
            int applied = target.TakeHit(raw);          // honours Sturdy
            dealt += applied;
            if (target.Trait == EggTrait.Static && applied > 0 && !user.IsFainted)
                user.TakeDamage(Math.Max(1, applied / 8));
        }
        // Lingering conditions, same rules as the battle screen: one at a time, and an egg
        // cannot catch the one its own element deals out.
        var rider = BattleCalc.RiderOf(move.Effect);
        if (rider != EggStatus.None && target.CanCatch(rider)) target.Afflict(rider);

        if (move.Effect == MoveEffect.Lifesteal50) user.Heal(Math.Max(1, dealt / 2));
        else if (move.Effect == MoveEffect.Recoil25) user.TakeDamage(Math.Max(1, dealt / 4));
        else if (move.Effect == MoveEffect.SpdDownFoe) { int st = target.SpdStage; if (target.TryLowerStage(ref st)) target.SpdStage = st; }
    }

    /// Returns true if `mine` wins. `turns` counts rounds. `salves` is the player's stock;
    /// the sim spends one instead of attacking when the active egg is nearly out, which is
    /// how a real player uses them and the only way to see what they do to a tuned gate.
    static bool Fight(List<EggInstance> mine, List<EggInstance> foe, out int turns, int salves = 0)
    {
        var a = new Side { Team = mine }; a.SwapToHealthy();
        var b = new Side { Team = foe }; b.SwapToHealthy();
        turns = 0;

        while (turns++ < 200)
        {
            if (a.Egg.IsFainted && !a.SwapToHealthy()) return false;
            if (b.Egg.IsFainted && !b.SwapToHealthy()) return true;

            // Spend a salve rather than swing when the egg would likely not survive the round.
            if (salves > 0 && a.Egg.HPFraction < 0.35f)
            {
                salves--;
                a.Egg.Heal(Math.Max(1, (int)Math.Round(a.Egg.MaxHP * 0.55f)));
                var foeOnly = Choose(b.Egg, a.Egg);
                Apply(b.Egg, a.Egg, foeOnly);      // the salve costs the turn
                a.Egg.TickRegen(); b.Egg.TickRegen();
                if (!a.AnyAlive) return false;
                continue;
            }

            var mySlot = Choose(a.Egg, b.Egg);
            var foeSlot = Choose(b.Egg, a.Egg);
            bool meFirst = BattleCalc.MoverGoesFirst(a.Egg, b.Egg);

            if (meFirst)
            {
                Apply(a.Egg, b.Egg, mySlot);
                if (!b.Egg.IsFainted) Apply(b.Egg, a.Egg, foeSlot);
            }
            else
            {
                Apply(b.Egg, a.Egg, foeSlot);
                if (!a.Egg.IsFainted) Apply(a.Egg, b.Egg, mySlot);
            }

            // End-of-round upkeep: regeneration first, then anything still burning.
            a.Egg.TickRegen();
            b.Egg.TickRegen();
            int burnA = a.Egg.StatusTickDamage(); if (burnA > 0) a.Egg.TakeDamage(burnA);
            int burnB = b.Egg.StatusTickDamage(); if (burnB > 0) b.Egg.TakeDamage(burnB);
            a.Egg.TickStatus();
            b.Egg.TickStatus();

            if (!b.AnyAlive) return true;
            if (!a.AnyAlive) return false;
        }
        return false;   // stalemate counts as a loss
    }

    public static bool FightPublic(List<EggInstance> mine, List<EggInstance> foe) => Fight(mine, foe, out _);

    static List<EggInstance> Fresh(params (string id, int lv)[] spec)
    {
        var team = new List<EggInstance>();
        foreach (var (id, lv) in spec) team.Add(EggInstance.Wild(id, lv));
        return team;
    }

    public static void Run(Action<bool, string> check)
    {
        EggRandom.SetSource(EggRandom.Seeded(20260807));

        // ---- wild encounters: what a real player brings is a mixed team, not one egg ----
        // Tier the roster the way the progression does: stage 1 early, stage 2 mid, stage 3 late.
        string[] TeamFor(int lv) =>
            lv < 16 ? new[] { "sprouteg", "yolkano", "tidepoach", "yolty", "chillet", "cobblet" }
          : lv < 26 ? new[] { "mossmallow", "emberoo", "bubblenog", "frizzlebolt", "glacegg", "craggle" }
                    : new[] { "bloomolk", "sizzlette", "wavelet", "sparkshell", "snowpoach", "boulderoo" };

        // A party of three never wipes to a single wild egg, so "win%" is a floor check, not a
        // difficulty reading. What tells you whether a wild fight has any tension is what it
        // *costs*: how much of the lead egg's HP it takes, and how often something actually faints.
        Console.WriteLine("  planet            lvl   win%   turns  lead hp lost  faint%");
        foreach (var planet in PlanetDatabase.All)
        {
            if (planet.IsBossWorld) continue;
            int lv = planet.MaxLevel;
            var roster = TeamFor(lv);
            int wins = 0, totalTurns = 0, runs = 300, fainted = 0;
            float hpLost = 0f;
            for (int i = 0; i < runs; i++)
            {
                // Three of the six, so type coverage is good but not guaranteed perfect.
                var mine = Fresh((roster[i % 6], lv), (roster[(i + 2) % 6], lv - 1), (roster[(i + 4) % 6], lv - 1));
                // Real encounters include Elders at their real rate - three levels above the
                // world's advertised range, tougher to catch, and 8.5% of what a player meets.
                // The sim rolled ordinary eggs only, so every difficulty figure here was quoted
                // for a game slightly easier than the one being played.
                var foe = new List<EggInstance>();
                string wildId = planet.RollSpecies();
                int wildLv = planet.RollLevel();
                foe.Add(EggRandom.Value < EggInstance.ElderChance
                        ? EggInstance.WildElder(wildId, wildLv)
                        : EggInstance.Wild(wildId, wildLv));
                if (Fight(mine, foe, out int t)) wins++;
                totalTurns += t;
                hpLost += 1f - mine[0].HPFraction;
                foreach (var e in mine) if (e.IsFainted) { fainted++; break; }
            }
            float winPct = wins * 100f / runs;
            float avgTurns = totalTurns / (float)runs;
            float lead = hpLost * 100f / runs, faintPct = fainted * 100f / runs;
            Console.WriteLine($"  {planet.Name,-16} {lv,3}  {winPct,5:0.0}  {avgTurns,6:0.0}  {lead,10:0.0}%  {faintPct,6:0.0}");
            check(winPct > 70f, $"{planet.Name}: a paced mixed team should win comfortably (was {winPct:0}%)");
            check(avgTurns >= 2.5f, $"{planet.Name}: fights should not be one-shots (avg {avgTurns:0.0} turns)");
            check(avgTurns <= 14f, $"{planet.Name}: fights should not drag (avg {avgTurns:0.0} turns)");
        }

        // ---- an Elder met by a brand new player ----
        // Elders are 8.5% of encounters everywhere, with no early gating, and are three levels
        // above the world. On Yolkhaven that is a level 9 against a starter, which is the very
        // first thing many players will meet.
        {
            int wins = 0, runs = 400, faints = 0;
            var home = PlanetDatabase.Home;
            for (int i = 0; i < runs; i++)
            {
                var mine = Fresh(("sprouteg", 5));
                var foe = new List<EggInstance> { EggInstance.WildElder(home.RollSpecies(), home.MaxLevel) };
                if (Fight(mine, foe, out _)) wins++;
                if (mine[0].IsFainted) faints++;
            }
            float pct = wins * 100f / runs;
            Console.WriteLine($"  a lone level-5 starter against a Yolkhaven Elder: {pct:0}% wins, " +
                              $"{faints * 100f / runs:0}% lose the egg  (which is why they are gated)");

            // The fight is unwinnable, so the answer is not to tune it - it is that the game
            // must not offer it. Elders hold off until the player has the three eggs Ori asks
            // for, which is the first point they have anything to swap to.
            var fresh = new GameState();
            check(!fresh.ElderesAllowed, "a brand new run meets no Elders");
            while (fresh.TotalCollected < StoryDatabase.FirstCatchEggs)
                fresh.Collect(EggInstance.Wild("sprouteg", 5));
            check(fresh.ElderesAllowed,
                  $"Elders arrive once the player has {StoryDatabase.FirstCatchEggs} eggs");
        }

        // ---- type advantage has to actually decide fights ----
        {
            // Verdant lead into a Molten world versus a Tidal lead into the same world.
            var molten = PlanetDatabase.Get("emberfall");
            int badWins = 0, goodWins = 0, runs = 400;
            for (int i = 0; i < runs; i++)
            {
                var bad = Fresh(("mossmallow", molten.MaxLevel));
                var f1 = Fresh((molten.RollSpecies(), molten.RollLevel()));
                if (Fight(bad, f1, out _)) badWins++;

                var good = Fresh(("bubblenog", molten.MaxLevel));
                var f2 = Fresh((molten.RollSpecies(), molten.RollLevel()));
                if (Fight(good, f2, out _)) goodWins++;
            }
            float bad_ = badWins * 100f / runs, good_ = goodWins * 100f / runs;
            Console.WriteLine($"  Emberfall solo lead — Verdant {bad_:0}% vs Tidal {good_:0}%");
            check(good_ > bad_ + 25f, $"bringing the right type should matter a lot ({bad_:0}% vs {good_:0}%)");
        }

        // ---- the two Vess duels ----
        foreach (var (id, lv, label) in new[] { ("vess_1", 14, "Vess I"), ("vess_2", 19, "Vess II") })
        {
            var trainer = StoryDatabase.GetTrainer(id);
            int wins = 0, runs = 300, totalTurns = 0;
            for (int i = 0; i < runs; i++)
            {
                var mine = id == "vess_1"
                    ? Fresh(("sprouteg", lv), ("yolkano", lv), ("tidepoach", lv), ("yolty", lv - 1),
                            ("chillet", lv - 1), ("cobblet", lv - 1))
                    : Fresh(("mossmallow", lv), ("emberoo", lv), ("bubblenog", lv - 1),
                            ("frizzlebolt", lv - 1), ("glacegg", lv - 2), ("craggle", lv - 2));
                var foe = new List<EggInstance>();
                for (int e = 0; e < trainer.SpeciesIds.Length; e++)
                    foe.Add(EggInstance.Wild(trainer.SpeciesIds[e], trainer.Levels[e]));
                if (Fight(mine, foe, out int t)) wins++;
                totalTurns += t;
            }
            float pct = wins * 100f / runs;
            Console.WriteLine($"  {label} (nest Lv{lv}): {pct:0.0}% wins, {totalTurns / (float)runs:0.0} turns");
            check(pct > 75f, $"{label} is a mandatory story fight and must be beatable (was {pct:0}%)");
            check(pct < 99f, $"{label} should still cost you something (was {pct:0}%)");
        }

        // ---- Amy, with the minimum team the gate actually allows ----
        {
            var amy = StoryDatabase.GetTrainer("amy");
            int wins = 0, runs = 400, totalTurns = 0;
            for (int i = 0; i < runs; i++)
            {
                // Exactly gate-legal: six eggs, four types, top level 18.
                var mine = Fresh(("mossmallow", 22), ("emberoo", 21), ("bubblenog", 21),
                                 ("frizzlebolt", 20), ("glacegg", 20), ("shadowhisk", 20));
                var foe = new List<EggInstance>();
                for (int e = 0; e < amy.SpeciesIds.Length; e++)
                    foe.Add(EggInstance.Wild(amy.SpeciesIds[e], amy.Levels[e]));
                if (Fight(mine, foe, out int t)) wins++;
                totalTurns += t;
            }
            float pct = wins * 100f / runs;
            Console.WriteLine($"  Amy vs a minimum gate-legal nest: {pct:0.0}% wins, {totalTurns / (float)runs:0.0} turns");

            // Same team, same gate, but carrying a full stack of salves. Salves are meant to
            // soften a loss streak, not to hand the fight to an under-levelled nest.
            int salvedWins = 0;
            for (int i = 0; i < runs; i++)
            {
                var mine = Fresh(("mossmallow", 22), ("emberoo", 21), ("bubblenog", 21),
                                 ("frizzlebolt", 20), ("glacegg", 20), ("shadowhisk", 20));
                var foe = new List<EggInstance>();
                for (int e = 0; e < amy.SpeciesIds.Length; e++)
                    foe.Add(EggInstance.Wild(amy.SpeciesIds[e], amy.Levels[e]));
                if (Fight(mine, foe, out _, GameState.MaxSalves)) salvedWins++;
            }
            float salved = salvedWins * 100f / runs;
            Console.WriteLine($"  ... same nest carrying {GameState.MaxSalves} salves:     {salved:0.0}% wins  ({salved - pct:+0.0} points)");
            check(salved > pct, "salves should help at all");
            check(salved - pct < 25f, $"salves must not trivialise the Amy gate (+{salved - pct:0.0} points)");
            check(pct > 40f, $"Amy must be winnable at the gate minimum (was {pct:0}%)");
            check(pct < 75f, $"Amy must still feel like a wall (was {pct:0}%)");
        }

        // ---- and with a well-raised nest she should be clearly beatable ----
        {
            var amy = StoryDatabase.GetTrainer("amy");
            int wins = 0, runs = 400;
            for (int i = 0; i < runs; i++)
            {
                var mine = Fresh(("bloomolk", 25), ("sizzlette", 24), ("wavelet", 24),
                                 ("sparkshell", 24), ("snowpoach", 23), ("gloomolk", 23));
                var foe = new List<EggInstance>();
                for (int e = 0; e < amy.SpeciesIds.Length; e++)
                    foe.Add(EggInstance.Wild(amy.SpeciesIds[e], amy.Levels[e]));
                if (Fight(mine, foe, out _)) wins++;
            }
            float pct = wins * 100f / runs;
            Console.WriteLine($"  Amy vs a well-raised nest:        {pct:0.0}% wins");
            check(pct > 75f, $"a strong nest should beat Amy comfortably (was {pct:0}%)");
        }

        // ---- catching should reward weakening, not spamming ----
        Console.WriteLine("  catch chance          full HP   at 25% HP");
        foreach (var id in new[] { "sprouteg", "bubblenog", "frizzlebolt", "shadowhisk", "reginova" })
        {
            var egg = EggInstance.Wild(id, id == "reginova" ? 23 : 14);
            float full = BattleCalc.CatchChance(egg);
            egg.TakeDamage((int)(egg.MaxHP * 0.75f));
            float weak = BattleCalc.CatchChance(egg);
            Console.WriteLine($"  {egg.Name,-18} {full * 100,6:0.0}%    {weak * 100,6:0.0}%");
            if (id != "reginova")
            {
                check(full < 0.55f, $"{egg.Name} should rarely be caught at full HP (was {full * 100:0}%)");
                check(weak > full * 1.5f, $"{egg.Name}: weakening should matter a lot");
            }
            else check(weak < 0.05f, "Amy's ace must be effectively uncatchable");
        }

        EggRandom.SetSource(null);
    }
}
