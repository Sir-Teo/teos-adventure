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
    }
}
