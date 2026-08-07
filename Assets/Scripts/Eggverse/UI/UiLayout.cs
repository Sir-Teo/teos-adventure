using UnityEngine;

namespace Eggverse
{
    /// <summary>
    /// Geometry for the two full-screen cards, shared by the view that draws them and the layout
    /// check that measures them.
    ///
    /// The check used to carry its own copy of these numbers, transcribed from the view. It was
    /// still checking a title heading at +250 and an ending heading at +156 after both had moved,
    /// which is the same fault as a renderer with a screen's text typed into it - it passes,
    /// confidently, about a layout the game does not have.
    ///
    /// These two screens get it because they are the ones a player reads rather than uses: the
    /// first thing they see and the last thing the game says.
    /// </summary>
    public static class UiLayout
    {
        // ---- title ----
        public static readonly Vector2 TitleHeadingAt = new Vector2(0f, 268f);
        public static readonly Vector2 TitleHeadingSize = new Vector2(1200f, 120f);
        public static readonly Vector2 TitleSubAt = new Vector2(0f, 150f);
        public static readonly Vector2 TitleSubSize = new Vector2(1200f, 50f);
        public static readonly Vector2 TitleBodyAt = new Vector2(0f, -110f);
        public static readonly Vector2 TitleBodySize = new Vector2(1500f, 460f);

        // ---- ending ----
        public static readonly Vector2 EndHeadingAt = new Vector2(0f, 186f);
        public static readonly Vector2 EndHeadingSize = new Vector2(1400f, 110f);
        // 500 tall at -160, not 460 at -140. The top edge stays where it was, under the
        // heading; the extra forty comes off the bottom, where there was room. The card can
        // carry five optional lines now - the scene, who carried the run, the coda for reading
        // every inscription, and two lines of tally - and at 460 the fullest version was fifteen
        // lines in fourteen.
        public static readonly Vector2 EndBodyAt = new Vector2(0f, -160f);
        public static readonly Vector2 EndBodySize = new Vector2(1300f, 500f);

        /// <summary>
        /// Vertical air between two centre-anchored boxes. Not overlap - air. Both of these
        /// screens shipped with a gap of single-digit pixels, which the overlap check called
        /// legal and which read as a 34pt line resting on the descenders of a 96pt one.
        /// </summary>
        public static float GapBetween(Vector2 upperAt, Vector2 upperSize, Vector2 lowerAt, Vector2 lowerSize)
        {
            float upperBottom = upperAt.y - upperSize.y * 0.5f;
            float lowerTop = lowerAt.y + lowerSize.y * 0.5f;
            return upperBottom - lowerTop;
        }

        /// <summary>A heading this much larger than the line under it needs real air.</summary>
        public const float MinHeadingGap = 24f;
    }
}
