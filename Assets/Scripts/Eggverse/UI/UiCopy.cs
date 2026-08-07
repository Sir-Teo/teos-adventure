namespace Eggverse
{
    /// <summary>
    /// Authored copy that is not dialogue: the title screen, the controls list, the ending card.
    ///
    /// This lived inside HudView's builders, which meant the first screen a player ever reads was
    /// the only prose in the game nothing checked — the consistency pass reads the databases, and
    /// the fit checks need the string separately from the RectTransform it goes into. Pulling it
    /// out as data costs nothing and puts it under both.
    /// </summary>
    public static class UiCopy
    {
        public const string Title = "EGGVERSE";
        public const string Subtitle = "Teo and the Long Way to Amy";
        public const string BeginHint = "press SPACE to begin";

        /// <summary>The premise, the controls, and the one rule nothing else states up front.</summary>
        public const string TitleBody =
            "Every planet you have ever stood on is a piece of shell. One egg broke, a long time ago, and the pieces\n" +
            "cooled into worlds. The core of it is still up there, still whole — and eleven years ago it started to split.\n\n" +
            "You are <b>Teo</b>, a junior hatcher on Yolkhaven, and the Nest Stations have started going cold.\n\n" +
            "<b><color=#FFC24D>SPACE</color></b>   WASD or arrows to fly    ·    E to land on a planet\n" +
            "<b><color=#FFC24D>PLANET</color></b>  WASD to walk    ·    E to talk or rest    ·    Q to lift off\n" +
            "<b><color=#FFC24D>BATTLE</color></b>  arrows to choose    ·    Enter or Space to confirm    ·    Esc to go back\n" +
            "<b><color=#FFC24D>ANY TIME</color></b>  M for the chart    ·    Tab for your collection    ·    0 to mute\n\n" +
            "Wild eggs hide in the pale <b>shell fields</b>, and the bold ones wander in the open. Weaken one before you\n" +
            "throw a carton at it — a healthy egg kicks straight back out.";

        public const string VictoryHeading = "AMY IS BEATEN";

        public const string VictoryBody =
            "Amy folds her arms, looks at your team, and nods once.\n\n" +
            "\"Fine. You cracked it.\"\n\n";

        /// <summary>The run's numbers, appended under the ending card.</summary>
        public static string VictoryTally(int collected, int recorded, int catchable, int types) =>
            "<color=#A8B2C4>Eggs collected: " + collected +
            "   ·   Species recorded: " + recorded + " / " + catchable +
            "   ·   Types held: " + types + " / 8</color>";
    }
}
