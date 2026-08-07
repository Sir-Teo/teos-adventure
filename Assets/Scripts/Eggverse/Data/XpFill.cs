using System.Collections.Generic;
using UnityEngine;

namespace Eggverse
{
    /// <summary>
    /// The path an XP bar takes when a fight ends, worked out before the egg is awarded anything.
    ///
    /// The HP bar has eased since it was written — AnimateHit lerps it over a third of a second
    /// while the egg flinches. The XP bar underneath it snapped. Win a fight and it jumped
    /// straight to wherever it landed, and if the award crossed a level it jumped *backwards*,
    /// from most-of-the-way-along to nearly-empty, with nothing in between. The player never
    /// once saw the bar complete — which is the whole payoff of a levelling system, and the
    /// single most satisfying beat in a fight the game already sounds a note for.
    ///
    /// This has to be computed up front because GainXp mutates the egg on the spot: by the time
    /// anything could animate, the level and the remainder are already the final ones and the
    /// path taken to get there is gone.
    ///
    /// Pure arithmetic on the same curve GainXp walks, so the check can walk it too.
    /// </summary>
    public static class XpFill
    {
        /// <summary>One stretch of bar.</summary>
        public struct Step
        {
            public float From, To;

            /// <summary>The bar filled and a level landed. It empties again afterwards.</summary>
            public bool Levels;

            /// <summary>
            /// That level was the last one. The bar stays full instead of emptying.
            ///
            /// This started as a trailing {1,1} step, which broke the invariant the check
            /// asserts - that a stretch begins where the previous one ended - and the check
            /// found it immediately. A flag beats a step that means something else.
            /// </summary>
            public bool Caps;
        }

        /// <summary>
        /// A very generous award - a low egg beating something far above it - could cross many
        /// levels at once. Past this many the animation stops being a payoff and becomes a wait,
        /// so the rest is collapsed into the final stretch.
        /// </summary>
        public const int MostSteps = 4;

        /// <summary>Where the bar sits once a step has finished and settled.</summary>
        public static float Rests(Step s) => s.Caps ? 1f : s.Levels ? 0f : s.To;

        /// <summary>The bar's path from where it is now to where the award leaves it.</summary>
        public static List<Step> Path(int level, int xp, int maxLevel, int amount)
        {
            var steps = new List<Step>();
            int need0 = Need(level, maxLevel);
            float at = need0 <= 0 ? 1f : xp / (float)need0;

            if (level >= maxLevel || amount <= 0)
            {
                // Nothing moves, but the caller still gets a step so it has something to draw
                // rather than a special case at every call site.
                steps.Add(new Step { From = at, To = at, Caps = level >= maxLevel });
                return steps;
            }

            int remaining = amount;
            while (remaining > 0 && level < maxLevel)
            {
                int need = Need(level, maxLevel);
                int toLevel = need - xp;
                if (remaining < toLevel)
                {
                    xp += remaining;
                    steps.Add(new Step { From = at, To = xp / (float)need });
                    break;
                }

                remaining -= toLevel;
                xp = 0;
                level++;
                steps.Add(new Step { From = at, To = 1f, Levels = true, Caps = level >= maxLevel });
                at = 0f;
                if (level >= maxLevel) break;
            }

            if (steps.Count == 0) steps.Add(new Step { From = at, To = at });

            // Too many levels at once: keep the first few and the last, so a player still sees
            // the bar complete and sees where it settles, without sitting through nine of them.
            if (steps.Count > MostSteps)
            {
                var trimmed = new List<Step>();
                for (int i = 0; i < MostSteps - 1; i++) trimmed.Add(steps[i]);
                var last = steps[steps.Count - 1];
                last.From = Rests(steps[MostSteps - 2]);
                trimmed.Add(last);
                return trimmed;
            }

            return steps;
        }

        /// <summary>EggInstance.XpToNext, as a function rather than a property on a live egg.</summary>
        public static int Need(int level, int maxLevel) => level >= maxLevel ? 0 : 24 + level * 24;

        /// <summary>How long one stretch takes, shorter when there are several to get through.</summary>
        public static float SecondsFor(int stepCount) => Mathf.Lerp(0.42f, 0.20f, (stepCount - 1) / 3f);
    }
}
