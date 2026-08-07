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
            "throw a carton at it — a healthy egg kicks straight back out. Press <b>N</b> to name what you catch.";

        /// <summary>
        /// The controls, compact, for the pause menu. The title screen explains them in prose
        /// and is never seen again; this is where a player goes when they have forgotten which
        /// key opens the chart. The self-check requires both lists to mention the same keys, so
        /// one cannot be updated without the other.
        /// </summary>
        // ---- screen chrome ----
        //
        // Titles and control footers. They were literals in the views and literals again in the
        // renderers, matching only because both were typed from the same reading - which is how
        // "THE HATCHERY REACH" became "HATCHERY REACH" on the chart render and nobody noticed.
        public const string CollectionTitle = "YOUR COLLECTION";
        public const string ChartTitle = "NAVIGATION CHART";
        public const string ChartFooter = "Arrows select  ·  Enter to set course  ·  M or Esc to close";
        public const string BattleFooter = "Arrows/WASD move · Enter or Space select · Esc back";

        // ---- world labels ----
        //
        // The floating labels on a planet surface. They live here rather than inline in the
        // surface builder so the checks measure the strings the game draws, not a transcription
        // of them - the hint is drawn at a smaller size than the title it sits under, and that
        // is exactly the kind of detail a transcription gets wrong.
        public const int LabelBox = 420;              // px, the label's width
        public const int LabelHeight = 90;            // px
        public const int LabelHintSize = 18;

        public const string LabelNestStation = "<b>NEST STATION</b>";
        public const string LabelNestHint = "<size=18><color=#A8B2C4>press E to rest</color></size>";
        public const string LabelAmy = "<b><color=#FFC24D>AMY</color></b>";
        public const string LabelAmyHint = "<size=18><color=#A8B2C4>press E to challenge</color></size>";
        // 18, not 17: every other hint is 18, and the check that measures these measures at
        // LabelHintSize. A hint drawn one point off the size it is verified at is a small lie in
        // the one place whose entire job is measuring things at the size they are drawn.
        public const string LabelTalkHint = "<size=18><color=#A8B2C4>press E to talk</color></size>";
        public const string LabelLandmarkHint = "<size=18><color=#A8B2C4>something is written here</color></size>";

        public static readonly string[] PauseControls =
        {
            "MOVE  WASD or arrows      TALK OR REST  E      LIFT OFF  Q",
            "CHART  M      COLLECTION  Tab      NAME AN EGG  N      MUTE  0",
        };

        /// <summary>
        /// What a Nest Station says when there was nothing to fix. Authored rather than built,
        /// so the prose pass reads them along with everything else the player can be shown.
        /// </summary>
        public static readonly string[] RestIdle =
        {
            "Nothing needed doing. The pad is warm anyway.",
            "Everything is already whole. The Keeper nods at you.",
            "Your eggs settle in, then look at you expectantly.",
            "You sit a while. The station hums along with the shells.",
        };

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
