using System;
using System.Collections.Generic;
using UnityEngine;

namespace Eggverse
{
    /// <summary>Where the player is in the main story, and everything they have already done.</summary>
    public class StoryState
    {
        readonly HashSet<string> flags = new HashSet<string>();
        public int BeatIndex { get; private set; }

        public event Action<StoryBeat> BeatAdvanced;

        public StoryBeat Current => StoryDatabase.Beats[Mathf.Clamp(BeatIndex, 0, StoryDatabase.Beats.Length - 1)];
        public bool Finished => BeatIndex >= StoryDatabase.Beats.Length - 1;

        public bool HasFlag(string flag) => !string.IsNullOrEmpty(flag) && flags.Contains(flag);

        public void SetFlag(string flag)
        {
            if (string.IsNullOrEmpty(flag)) return;
            flags.Add(flag);
        }

        public string[] FlagsSnapshot()
        {
            var copy = new string[flags.Count];
            flags.CopyTo(copy);
            return copy;
        }

        /// <summary>Restores a saved run. Does not fire BeatAdvanced.</summary>
        public void RestoreFrom(string[] savedFlags, int savedBeatIndex)
        {
            flags.Clear();
            if (savedFlags != null)
                for (int i = 0; i < savedFlags.Length; i++) flags.Add(savedFlags[i]);
            BeatIndex = Mathf.Clamp(savedBeatIndex, 0, StoryDatabase.Beats.Length - 1);
        }

        /// <summary>The furthest sector the player is currently allowed to travel to.</summary>
        public Sector MaxSector => Current.MaxSector;

        public bool CanEnter(Sector sector) => (int)sector <= (int)MaxSector;

        /// <summary>Why a sector is closed, in words the HUD can show.</summary>
        /// <summary>
        /// Why the chart will not plot a course there. Takes the state so it can say what is
        /// outstanding rather than only restating the objective - this is read at the moment a
        /// player is stopped, which is when the specifics matter most.
        /// </summary>
        public string SectorBlockerText(Sector sector, GameState state = null)
        {
            if (CanEnter(sector)) return null;

            string text = "The route to " + PlanetDatabase.SectorName(sector) + " opens later. " +
                          Current.Objective;
            string outstanding = state != null ? CurrentBlockerText(state) : null;
            if (outstanding != null) text += "\n\nStill needed: " + outstanding;
            return text;
        }

        // ------------------------------------------------------------------
        // progression
        // ------------------------------------------------------------------

        /// <summary>Advances through as many completed beats as possible. Safe to call every frame.</summary>
        public void Evaluate(GameState state)
        {
            int guard = 0;
            while (guard++ < StoryDatabase.Beats.Length && BeatIndex < StoryDatabase.Beats.Length - 1)
            {
                if (!IsComplete(StoryDatabase.Beats[BeatIndex], state)) break;
                BeatIndex++;
                if (BeatAdvanced != null) BeatAdvanced(Current);
            }
        }

        bool IsComplete(StoryBeat beat, GameState state)
        {
            for (int i = 0; i < beat.RequiredFlags.Length; i++)
                if (!flags.Contains(beat.RequiredFlags[i])) return false;

            if (beat.RequiredEggs > 0 && state.TotalCollected < beat.RequiredEggs) return false;
            if (beat.RequiredTypes > 0 && state.DistinctTypesHeld < beat.RequiredTypes) return false;
            if (beat.RequiredLevel > 0 && state.HighestPartyLevel < beat.RequiredLevel) return false;
            return true;
        }

        /// <summary>Short list of what the current beat is still waiting on, or null if it is only waiting on a conversation.</summary>
        public string CurrentBlockerText(GameState state)
        {
            var beat = Current;
            var parts = new List<string>();

            // Whoever is still to be found. A beat asking for two people said the same thing
            // whether you had found neither or one of them.
            if (beat.RequiredFlags != null)
                for (int i = 0; i < beat.RequiredFlags.Length; i++)
                {
                    if (HasFlag(beat.RequiredFlags[i])) continue;
                    string label = StoryDatabase.LabelForFlag(beat.RequiredFlags[i]);
                    if (label != null) parts.Add(label);
                }

            if (beat.RequiredEggs > 0 && state.TotalCollected < beat.RequiredEggs)
                parts.Add(Words.Count(beat.RequiredEggs - state.TotalCollected, "more egg"));
            if (beat.RequiredTypes > 0 && state.DistinctTypesHeld < beat.RequiredTypes)
                parts.Add(Words.Count(beat.RequiredTypes - state.DistinctTypesHeld, "more type"));
            if (beat.RequiredLevel > 0 && state.HighestPartyLevel < beat.RequiredLevel)
                parts.Add("an egg at level " + beat.RequiredLevel +
                          " (best is " + state.HighestPartyLevel + ")");

            return parts.Count == 0 ? null : string.Join(", ", parts.ToArray());
        }

        /// <summary>Should this NPC be standing on their planet right now?</summary>
        public bool IsNpcPresent(NpcDef npc)
        {
            switch (npc.Id)
            {
                // Vess only appears for her own confrontations, and stays afterwards.
                case "vess1": return BeatIndex >= IndexOf("vess_one");
                case "vess2": return BeatIndex >= IndexOf("vess_two");
                case "pim": return BeatIndex >= IndexOf("belt_open");
                case "marn":
                case "sable": return BeatIndex >= IndexOf("drift_keepers");
                default: return true;
            }
        }

        public static int IndexOf(string beatId)
        {
            for (int i = 0; i < StoryDatabase.Beats.Length; i++)
                if (StoryDatabase.Beats[i].Id == beatId) return i;
            return 0;
        }
    }
}
