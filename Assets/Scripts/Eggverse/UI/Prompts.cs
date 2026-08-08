namespace Eggverse
{
    /// <summary>
    /// Every prompt the game puts along the bottom of the screen.
    ///
    /// These are the most-read strings in the whole game — a prompt is on screen for the entire
    /// time a player is walking, which is most of the time they are playing — and they were
    /// written where they were raised, so the prose pass that reads six hundred authored lines
    /// read none of them.
    ///
    /// They also share a grammar that only shows up when they sit together: a verb key in bold,
    /// the thing it acts on in bold, and fields separated by a double-spaced middot. Written
    /// eight lines apart in two files, that grammar was a thing to remember rather than a thing
    /// to see.
    /// </summary>
    public static class Prompts
    {
        /// <summary>The separator every prompt uses between fields.</summary>
        public const string Gap = "  ·  ";

        /// <summary>A key, in the bold the prompts put keys in.</summary>
        public static string Key(string k) => "<b>" + k + "</b>";

        public static string Walking() =>
            Key("WASD") + " walk" + Gap + Key("Q") + " lift off" + Gap + Key("Tab") + " party";

        public static string TalkTo(string who) => "Press " + Key("E") + " to talk to " + Key(who);

        public static string RestHere() =>
            "Press " + Key("E") + " to rest at the " + Key("Nest Station") + Gap + Key("Q") + " to lift off";

        public static string ReadLandmark(string name, bool alreadyRead) =>
            alreadyRead
                ? "Press " + Key("E") + " to read " + Key(name) + " again"
                : "Press " + Key("E") + " to read what is written here";

        public static string FaceAmy(bool alreadyBeaten) =>
            alreadyBeaten
                ? "Press " + Key("E") + " to rematch " + Key("Amy") + "."
                : "Press " + Key("E") + " to challenge " + Key("Amy") + ".";

        /// <summary>
        /// A world the story has not opened yet. The chart and the approach both say "your
        /// charts do not reach"; this is the same fact in the place you are pointed at it, so it
        /// is worded the same way rather than inventing a third phrasing.
        /// </summary>
        public static string BeyondCharts(string world) =>
            Key(world) + " is beyond your charted route.";
    }
}
