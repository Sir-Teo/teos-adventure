using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Eggverse
{
    [Serializable]
    public class EggSave
    {
        public string species;
        public string nickname;
        public int level;
        public int xp;
        public int hp;
        public string[] moveIds;
        public int[] movePP;
        public bool elder;
    }

    [Serializable]
    public class SaveData
    {
        public int version = 1;
        public EggSave[] party;
        public EggSave[] nest;
        public string[] seen;
        public string[] caught;
        public string[] visited;
        public string[] flags;
        public string[] caches;
        public string[] landmarks;
        // Defaulted true, so a save written before the option existed loads with motion on.
        public bool screenMotion = true;
        // Defaults to normal, which is also what a file written before the option existed gets.
        public int textSpeed = 1;
        public int cartons;
        // Defaulted, not zero: a save written before salves existed has no key for them, and
        // JsonUtility leaves the field initializer in place — so those runs load fully stocked.
        public int salves = GameState.MaxSalves;
        public int beatIndex;
        public string planet;
        public bool amyDefeated;
        public float playSeconds;
        public string savedAt;
    }

    /// <summary>One save slot, written as JSON next to the player's other Unity data.</summary>
    public static class SaveSystem
    {
        const string FileName = "eggverse_save.json";

        static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        public static bool Exists
        {
            get
            {
                try { return File.Exists(Path); }
                catch { return false; }
            }
        }

        // ------------------------------------------------------------------
        // writing
        // ------------------------------------------------------------------

        public static bool Save(GameState state, StoryState story, string planetId, float playSeconds)
        {
            try
            {
                var data = new SaveData
                {
                    version = 1,
                    party = ToSaves(state.Party),
                    nest = ToSaves(state.Nest),
                    seen = ToArray(state.Seen),
                    caught = ToArray(state.Caught),
                    visited = ToArray(state.Visited),
                    flags = story.FlagsSnapshot(),
                    cartons = state.Cartons,
                    caches = ToArray(state.Caches),
                    landmarks = ToArray(state.Landmarks),
                    screenMotion = state.ScreenMotion,
                    textSpeed = state.TextSpeed,
                    salves = state.Salves,
                    beatIndex = story.BeatIndex,
                    planet = planetId,
                    amyDefeated = state.AmyDefeated,
                    playSeconds = playSeconds,
                    savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
                };

                File.WriteAllText(Path, JsonUtility.ToJson(data, true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Eggverse: could not save — " + e.Message);
                return false;
            }
        }

        static EggSave[] ToSaves(List<EggInstance> eggs)
        {
            var result = new EggSave[eggs.Count];
            for (int i = 0; i < eggs.Count; i++)
            {
                var egg = eggs[i];
                var ids = new string[egg.Moves.Count];
                var pps = new int[egg.Moves.Count];
                for (int m = 0; m < egg.Moves.Count; m++)
                {
                    ids[m] = egg.Moves[m].Move.Id;
                    pps[m] = egg.Moves[m].PP;
                }
                result[i] = new EggSave
                {
                    species = egg.Species.Id,
                    nickname = egg.Nickname,
                    level = egg.Level,
                    xp = egg.Xp,
                    hp = egg.CurrentHP,
                    moveIds = ids,
                    movePP = pps,
                    elder = egg.Elder,
                };
            }
            return result;
        }

        static string[] ToArray(HashSet<string> set)
        {
            var copy = new string[set.Count];
            set.CopyTo(copy);
            return copy;
        }

        // ------------------------------------------------------------------
        // reading
        // ------------------------------------------------------------------

        /// <summary>Reads the save file. Returns null if there is nothing valid to load.</summary>
        public static SaveData Peek()
        {
            try
            {
                if (!File.Exists(Path)) return null;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(Path));
                if (data == null || data.version != 1) return null;
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Eggverse: save file unreadable — " + e.Message);
                return null;
            }
        }

        public static bool Load(out GameState state, out StoryState story, out string planetId, out float playSeconds)
        {
            state = null; story = null; planetId = null; playSeconds = 0f;

            var data = Peek();
            if (data == null) return false;
            if (!Restore(data, out state, out story, out planetId, out playSeconds)) return false;

            if (StaleEntriesDropped > 0)
                Debug.LogWarning("Eggverse: dropped " + StaleEntriesDropped +
                                 " save entries naming content that no longer exists.");
            return true;
        }

        /// <summary>How many entries the last Restore threw away for naming missing content.</summary>
        public static int StaleEntriesDropped { get; private set; }

        /// <summary>
        /// Turns save data into live state. Split from Load so it can be exercised without a
        /// filesystem — the interesting failures are all about hostile *content*, not bad bytes.
        /// Deliberately free of Debug logging: UnityEngine.Debug cannot run outside a player, and
        /// a purity check that cannot be run headlessly is not much of a check. Load does the
        /// reporting instead.
        /// </summary>
        public static bool Restore(SaveData data, out GameState state, out StoryState story,
                                   out string planetId, out float playSeconds)
        {
            state = null; story = null; planetId = null; playSeconds = 0f;
            if (data == null) return false;

            try
            {
                state = new GameState(false);
                int stale = 0;
                stale += Fill(state.Party, data.party);
                stale += Fill(state.Nest, data.nest);
                stale += AddKnown(state.Seen, data.seen, SpeciesDatabase.Exists);
                stale += AddKnown(state.Caught, data.caught, SpeciesDatabase.Exists);
                stale += AddKnown(state.Visited, data.visited, PlanetDatabase.Exists);
                // Caches before cartons: carton capacity is derived from them.
                stale += AddKnown(state.Caches, data.caches, PlanetDatabase.HasCache);
                stale += AddKnown(state.Landmarks, data.landmarks, id => LandmarkDatabase.For(id) != null);
                StaleEntriesDropped = stale;
                state.Cartons = Mathf.Clamp(data.cartons, 0, state.MaxCartons);
                state.Salves = Mathf.Clamp(data.salves, 0, GameState.MaxSalves);
                state.AmyDefeated = data.amyDefeated;
                state.ScreenMotion = data.screenMotion;
                state.TextSpeed = Mathf.Clamp(data.textSpeed, 0, GameState.TextSpeedCount - 1);

                // A save with an empty party would be unplayable; hand back a starter.
                if (state.Party.Count == 0) state.Party.Add(EggInstance.Wild("sprouteg", 5));
                if (state.Visited.Count == 0) state.Visited.Add(PlanetDatabase.Home.Id);

                story = new StoryState();
                story.RestoreFrom(data.flags, data.beatIndex);

                // An unknown world would be stored, re-saved, and carried forward forever; Get()
                // would quietly land the player on Yolkhaven while the file still said otherwise.
                planetId = PlanetDatabase.Exists(data.planet) ? data.planet : PlanetDatabase.Home.Id;
                state.CurrentPlanetId = planetId;
                playSeconds = Mathf.Max(0f, data.playSeconds);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Eggverse: save file could not be restored — " + e.Message);
                state = null; story = null;
                return false;
            }
        }

        /// <summary>
        /// Restores a list of eggs, skipping any whose species no longer exists.
        ///
        /// SpeciesDatabase.Get falls back to the first species, which is the right answer for a
        /// lookup and the wrong one here: a save naming a species that has since been renamed or
        /// removed would silently turn the player's Elder Glacegg into a level-30 Sprouteg. Better
        /// to drop it and say so than to hand back something the player never caught.
        /// </summary>
        static int Fill(List<EggInstance> target, EggSave[] saves)
        {
            target.Clear();
            if (saves == null) return 0;
            int dropped = 0;
            for (int i = 0; i < saves.Length; i++)
            {
                var s = saves[i];
                if (s == null || string.IsNullOrEmpty(s.species)) continue;
                if (!SpeciesDatabase.Exists(s.species)) { dropped++; continue; }
                target.Add(EggInstance.Restore(s.species, s.nickname, s.level, s.xp, s.hp, s.moveIds, s.movePP, s.elder));
            }
            return dropped;
        }

        /// <summary>Copies in only the ids that still name something, and reports how many did not.</summary>
        static int AddKnown(HashSet<string> target, string[] ids, Func<string, bool> exists)
        {
            if (ids == null) return 0;
            int dropped = 0;
            for (int i = 0; i < ids.Length; i++)
            {
                if (string.IsNullOrEmpty(ids[i])) continue;
                if (exists(ids[i])) target.Add(ids[i]);
                else dropped++;
            }
            return dropped;
        }

        public static void Delete()
        {
            try { if (File.Exists(Path)) File.Delete(Path); }
            catch (Exception e) { Debug.LogWarning("Eggverse: could not delete save — " + e.Message); }
        }

        /// <summary>One-line description of the save, for the title screen.</summary>
        public static string Describe(SaveData data)
        {
            if (data == null) return null;
            int partyCount = data.party != null ? data.party.Length : 0;
            int best = 0;
            if (data.party != null)
                for (int i = 0; i < data.party.Length; i++)
                    if (data.party[i] != null) best = Mathf.Max(best, data.party[i].level);

            var beat = StoryDatabase.Beats[Mathf.Clamp(data.beatIndex, 0, StoryDatabase.Beats.Length - 1)];
            var planet = PlanetDatabase.Get(string.IsNullOrEmpty(data.planet) ? PlanetDatabase.Home.Id : data.planet);

            int minutes = Mathf.FloorToInt(data.playSeconds / 60f);
            string time = minutes >= 60 ? (minutes / 60) + "h " + (minutes % 60) + "m" : minutes + "m";
            string when = string.IsNullOrEmpty(data.savedAt) ? "" : "  ·  saved " + data.savedAt;

            return beat.Chapter + "  ·  " + planet.Name + "  ·  " + partyCount + " in nest, top level " + best +
                   "  ·  " + time + when;
        }
    }
}
