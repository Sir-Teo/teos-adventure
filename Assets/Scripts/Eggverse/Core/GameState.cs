using System;
using System.Collections.Generic;
using UnityEngine;

namespace Eggverse
{
    /// <summary>All persistent player progress for a run.</summary>
    public class GameState
    {
        public const int PartySize = 6;
        public const int BaseMaxCartons = 12;
        public const int MaxSalves = 4;

        public readonly List<EggInstance> Party = new List<EggInstance>();
        public readonly List<EggInstance> Nest = new List<EggInstance>();
        public readonly HashSet<string> Seen = new HashSet<string>();
        public readonly HashSet<string> Caught = new HashSet<string>();
        public readonly HashSet<string> Visited = new HashSet<string>();
        /// <summary>Ids of worlds whose hidden cache has been dug up. One each, for good.</summary>
        public readonly HashSet<string> Caches = new HashSet<string>();

        /// <summary>Twelve, plus one for every cache dug up.</summary>
        public int MaxCartons
        {
            get
            {
                int extra = 0;
                foreach (var id in Caches)
                    if (PlanetDatabase.CacheWorlds.TryGetValue(id, out int n)) extra += n;
                return BaseMaxCartons + extra;
            }
        }

        public int Cartons = BaseMaxCartons;
        public int Salves = MaxSalves;

        /// <summary>
        /// Whether the game is allowed to move the screen. Hits shake the battle scene and
        /// landing flashes the whole display; both are good feel and both are a problem for
        /// anyone sensitive to motion, with no way to turn either off.
        /// </summary>
        public bool ScreenMotion = true;

        /// <summary>
        /// How fast text arrives, as a multiplier on both the dialogue reveal and the dwell on a
        /// battle message. The game has 900 lines of dialogue and read them all at one fixed
        /// rate, which is slow for someone rereading and fast for someone who is not.
        /// </summary>
        public int TextSpeed = 1;                       // 0 relaxed, 1 normal, 2 brisk, 3 instant

        /// <summary>
        /// Whether the wild is allowed to produce an Elder yet.
        ///
        /// They are three levels above the world and 8.5% of encounters, with no gating - which
        /// meant a brand new player carrying one level-5 Sprouteg could meet a level-9 Elder on
        /// Yolkhaven inside their first few minutes. Simulated, that fight is lost 100% of the
        /// time: no second egg to swap to, and fleeing an Elder is not reliable.
        ///
        /// So they hold off until the player has the three eggs Ori asks for, which is the point
        /// the game stops being a tutorial and the player has something to swap to.
        /// </summary>
        public bool ElderesAllowed => TotalCollected >= StoryDatabase.FirstCatchEggs;
        public const int TextSpeedCount = 4;

        static readonly string[] TextSpeedNames = { "relaxed", "normal", "brisk", "instant" };
        static readonly float[] TextSpeedScale = { 0.6f, 1f, 1.8f, 0f };   // 0 means no wait at all

        public string TextSpeedName => TextSpeedNames[Mathf.Clamp(TextSpeed, 0, TextSpeedCount - 1)];

        /// <summary>Characters per second for the reveal; 0 when text should simply appear.</summary>
        public float RevealScale => TextSpeedScale[Mathf.Clamp(TextSpeed, 0, TextSpeedCount - 1)];
        public string CurrentPlanetId = PlanetDatabase.Home.Id;
        public bool AmyDefeated;

        public event Action Changed;
        public void RaiseChanged() { if (Changed != null) Changed(); }

        /// <summary>Pass false when the contents are about to be filled in from a save file.</summary>
        public GameState(bool withStarter = true)
        {
            if (!withStarter) return;

            var starter = EggInstance.Wild("sprouteg", 5);
            starter.Nickname = null;
            Party.Add(starter);
            Seen.Add(starter.Species.Id);
            Caught.Add(starter.Species.Id);
            Visited.Add(PlanetDatabase.Home.Id);
        }

        // ---------- party ----------

        public EggInstance Leader
        {
            get
            {
                for (int i = 0; i < Party.Count; i++)
                    if (!Party[i].IsFainted) return Party[i];
                return Party.Count > 0 ? Party[0] : null;
            }
        }

        public int HighestPartyLevel
        {
            get
            {
                int best = 0;
                for (int i = 0; i < Party.Count; i++) best = Mathf.Max(best, Party[i].Level);
                return best;
            }
        }

        public int TotalCollected => Party.Count + Nest.Count;

        public int DistinctTypesHeld
        {
            get
            {
                var set = new HashSet<EggType>();
                for (int i = 0; i < Party.Count; i++) set.Add(Party[i].Type);
                for (int i = 0; i < Nest.Count; i++) set.Add(Nest[i].Type);
                return set.Count;
            }
        }

        /// <summary>Records a species in the field record. Safe to call repeatedly.</summary>
        public void RegisterSpecies(EggInstance egg)
        {
            if (egg == null) return;
            Seen.Add(egg.Species.Id);
            Caught.Add(egg.Species.Id);
        }

        /// <summary>
        /// Grants experience and keeps the field record in step. Evolution changes an egg's
        /// species, so every XP award has to be able to register the new one — routing all of
        /// them through here means no future caller can forget.
        /// </summary>
        public void AwardXp(EggInstance egg, int amount, List<EggInstance> evolved, List<string> log)
        {
            if (egg == null) return;
            egg.GainXp(amount, log);
            if (!egg.EvolvedThisLevelUp) return;

            RegisterSpecies(egg);
            if (evolved != null) evolved.Add(egg);
        }

        /// <summary>Adds a newly caught egg. Returns true if it went into the party, false if into the nest.</summary>
        public bool Collect(EggInstance egg)
        {
            Caught.Add(egg.Species.Id);
            Seen.Add(egg.Species.Id);
            bool toParty = Party.Count < PartySize;
            if (toParty) Party.Add(egg); else Nest.Add(egg);
            RaiseChanged();
            return toParty;
        }

        /// <summary>Restores health and PP, but not supplies.</summary>
        public void RestoreEggs()
        {
            for (int i = 0; i < Party.Count; i++) Party[i].FullRestore();
            for (int i = 0; i < Nest.Count; i++) Nest[i].FullRestore();
            RaiseChanged();
        }

        /// <summary>Cartons and salves both come from the same Nest Station restock.</summary>
        public void RestockSupplies()
        {
            Cartons = MaxCartons;
            Salves = MaxSalves;
            RaiseChanged();
        }

        /// <summary>A full rest: eggs mended and supplies replenished. Nest Stations only.</summary>
        public void HealAll()
        {
            RestoreEggs();
            RestockSupplies();
        }

        /// <summary>
        /// Trades a nest egg for a party egg. Until this existed an egg that went to the nest
        /// stayed there: your party was whichever six you happened to catch first, for the whole
        /// run, while fifty more sat at home unusable.
        /// </summary>
        public bool SwapWithNest(int nestIndex, int partySlot)
        {
            if (nestIndex < 0 || nestIndex >= Nest.Count) return false;
            if (partySlot < 0 || partySlot >= Party.Count) return false;

            var incoming = Nest[nestIndex];
            Nest[nestIndex] = Party[partySlot];
            Party[partySlot] = incoming;
            RaiseChanged();
            return true;
        }

        /// <summary>Moves a nest egg into an empty party slot, when there is room.</summary>
        public bool TakeFromNest(int nestIndex)
        {
            if (nestIndex < 0 || nestIndex >= Nest.Count || Party.Count >= PartySize) return false;
            Party.Add(Nest[nestIndex]);
            Nest.RemoveAt(nestIndex);
            RaiseChanged();
            return true;
        }

        public void SwapPartySlots(int a, int b)
        {
            if (a < 0 || b < 0 || a >= Party.Count || b >= Party.Count || a == b) return;
            var tmp = Party[a]; Party[a] = Party[b]; Party[b] = tmp;
            RaiseChanged();
        }

    }
}
