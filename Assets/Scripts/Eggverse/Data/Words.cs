namespace Eggverse
{
    /// <summary>
    /// Small text helpers for prose that has to agree with the game's numbers.
    ///
    /// A line like "it burns for three rounds" is true right up until somebody changes the
    /// constant it was written beside, and nothing complains. Where the number can be spelled
    /// from the constant it should be; where the writing is better with the word in it, the
    /// self-check holds the constant to what the line claims.
    /// </summary>
    public static class Words
    {
        static readonly string[] Small =
        {
            "zero", "one", "two", "three", "four", "five", "six",
            "seven", "eight", "nine", "ten", "eleven", "twelve",
        };

        /// <summary>Spells small numbers, which read better in a sentence, and gives up above twelve.</summary>
        public static string Spell(int n) =>
            n >= 0 && n < Small.Length ? Small[n] : n.ToString();

        /// <summary>Spells a number at the start of a sentence, capitalised.</summary>
        public static string SpellCapitalised(int n)
        {
            string w = Spell(n);
            return w.Length == 0 ? w : char.ToUpperInvariant(w[0]) + w.Substring(1);
        }

        /// <summary>"1 world" / "2 worlds", for counts that can legitimately be one.</summary>
        public static string Count(int n, string singular, string plural = null) =>
            n + " " + (n == 1 ? singular : (plural ?? singular + "s"));

        /// <summary>
        /// A small fraction in the words this game uses for them, so a sentence describing a
        /// formula can be built from the number rather than typed beside it. "an eighth", not
        /// "0.125".
        /// </summary>
        public static string Fraction(float part)
        {
            int denominator = UnityEngine.Mathf.RoundToInt(1f / UnityEngine.Mathf.Max(0.0001f, part));
            switch (denominator)
            {
                case 1: return "all";
                case 2: return "half";
                case 3: return "a third";
                case 4: return "a quarter";
                case 5: return "a fifth";
                case 6: return "a sixth";
                case 8: return "an eighth";
                case 10: return "a tenth";
                default: return "a " + denominator + "th";
            }
        }

        /// <summary>A multiplier as the percentage above one that it is: 1.3 becomes "30%".</summary>
        public static string PercentAbove(float multiplier) =>
            UnityEngine.Mathf.RoundToInt((multiplier - 1f) * 100f) + "%";
    }
}
