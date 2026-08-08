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

        /// <summary>
        /// An egg's health, written the same way everywhere.
        ///
        /// It appeared on four screens in three forms: "45 / 135" on the battle plate and the
        /// egg panel, "45/135" in the collection list, "45/135 HP" on the swap row. The same
        /// number about the same egg, spaced three ways.
        ///
        /// The label is the caller's business - a list has no room for it and a stat line reads
        /// badly without it - but the figure itself is one format.
        /// </summary>
        public static string Health(int current, int max) => current + "/" + max;

        // ---- what the battle says ----
        //
        // Written inline in BattleMode, every one of them, and so outside the prose pass -
        // twenty-two lines a player reads more often than any dialogue in the game, never once
        // checked for a doubled word or a stray space. These are the ones with no name or
        // number in them; the rest are probed through the methods that build them.
        public static readonly string[] BattleLines =
        {
            "It is far older than the others, and it will not go quietly into a carton.",
            "You are out of egg cartons! Rest at a Nest Station to restock.",
            "No yolk salve left! Nest Stations carry more.",
            "No other egg is in any shape to fight.",
            "You drifted away safely.",
            "You could not get clear!",
            "It missed!",
            "A critical crack!",
            "Nothing much happened.",
            "Every egg you brought is out cold...",
        };

        public const string ElderWarning = "It is far older than the others, and it will not go quietly into a carton.";
        public const string OutOfCartons = "You are out of egg cartons! Rest at a Nest Station to restock.";
        public const string OutOfSalves = "No yolk salve left! Nest Stations carry more.";
        public const string NobodyLeft = "No other egg is in any shape to fight.";
        public const string FledSafely = "You drifted away safely.";
        public const string CouldNotFlee = "You could not get clear!";
        public const string Missed = "It missed!";
        public const string Critical = "A critical crack!";
        public const string NoEffect = "Nothing much happened.";
        public const string AllOutCold = "Every egg you brought is out cold...";

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
        /// <summary>
        /// Resting at a station that has gone cold. It still works, and the reason is the rule
        /// Ori sets out in the opening brief: a Nest Station runs warm off the eggs around it.
        /// Turning up with a full nest is what makes the pad work, so the game says so instead
        /// of quietly healing you on a dead pad.
        /// </summary>
        public static readonly string[] RestCold =
        {
            "The pad is cold. It takes a while, and your nest does it between them.",
            "Cold stone, warm eggs. It works the way Ori said it would.",
            "The straw is old and the pad is dead. Your six manage it anyway.",
        };

        /// <summary>
        /// Waking up after losing. There is no mechanical penalty and there should not be -
        /// this is a game about looking after eggs - but it read the same everywhere, on a
        /// world whose station the game had just spent four commits establishing was dead.
        /// </summary>
        public const string WokeWarm = "You woke at the Nest Station. The pad was warm, and everything is patched up.";
        public const string WokeCold = "You woke on cold stone. It took a while. Everything is patched up, and nobody is saying how.";

        // ---- what the game says when it will not do something ----
        //
        // These were fourteen string literals at fourteen call sites, and not one of them went
        // through the prose pass that reads all five hundred and seventy other authored lines.
        // They are the strings most likely to be clumsy: written in a hurry, seen rarely, never
        // re-read. Two of them turned out to be different refusals for the same situation.

        /// <summary>
        /// Trying to fly somewhere the story has not opened.
        ///
        /// The chart said "That route is sealed." and flying at the same world said "Your charts
        /// do not reach that far yet." — two answers to one question, and the second is the
        /// better one because it says whose limit it is. A sealed route sounds like a door
        /// somebody shut; charts that do not reach is a thing about you, and the thing about you
        /// is what changes.
        /// </summary>
        public const string RouteSealed = "Your charts do not reach that far yet.";

        public const string AlreadyThere = "You are already there.";
        public const string AlreadyWithYou = "That one is already with you. Pick an egg from the nest.";
        public const string SaveUnreadable = "That save could not be read. Starting a new run.";
        public const string SaveWritten = "Progress saved.";

        /// <summary>
        /// One sentence for a failed write, wherever it happens. A save the player asked for and
        /// an autosave that failed are the same failure, and the first said only "Could not write
        /// the save file" - leaving out the part that matters, which is that the run has stopped
        /// being written down.
        /// </summary>
        public const string SaveUnwritable = "Could not write the save file. Your progress is not being saved.";
        public const string BackToSpace = "Back in the black. M opens the chart.";

        /// <summary>
        /// What a resident does for you when a scene says so. "Cartons restocked." said what
        /// happened and not what it means, which is the whole of it: they have given you more
        /// than you can normally carry back out.
        /// </summary>
        public const string NestWarmedByScene = "Your nest is warm again.";
        public static string CartonsGiven(int now, int max) =>
            "Cartons restocked. " + Words.Count(now, "carton") + " to carry back out.";

        /// <summary>
        /// What a load had to repair, in words.
        ///
        /// SaveSystem drops entries it no longer recognises and moves eggs out of an over-long
        /// party, and it counted both and told nobody: StaleEntriesDropped was set on every load
        /// and read nowhere. A file quietly repaired is a file a player thinks was fine — and
        /// then wonders where the egg went.
        /// </summary>
        public static string SaveRepaired(int dropped, int moved)
        {
            if (dropped <= 0 && moved <= 0) return null;

            var parts = new System.Collections.Generic.List<string>();
            if (dropped > 0) parts.Add(Words.Count(dropped, "entry") + " your copy no longer knows");
            if (moved > 0) parts.Add(Words.Count(moved, "egg") + " moved from your party to the nest");
            return "That save needed patching up: " + string.Join(", ", parts.ToArray()) + ".";
        }

        /// <summary>
        /// What to do about having run out.
        ///
        /// The supply line says "Cartons none" in red, which is the state and not the recovery.
        /// A player three worlds in has worked out that a Nest Station restocks; a player on
        /// their second has not, and is the one it happens to — they threw everything at a
        /// Sprouteg and now the record has stopped moving for a reason the HUD is reporting and
        /// not explaining.
        ///
        /// Once, when it happens, and again only if it happens again.
        /// </summary>
        public static string OutOf(bool cartons, bool salves)
        {
            if (cartons && salves)
                return "Out of cartons and salves. A Nest Station restocks both — rest at the pad.";
            if (cartons)
                return "That was your last carton. A Nest Station restocks them — rest at the pad.";
            if (salves)
                return "That was your last salve. A Nest Station restocks them — rest at the pad.";
            return null;
        }

        /// <summary>
        /// Losing to somebody who was trying to beat you, rather than to something that was
        /// defending its field.
        ///
        /// The game had one thing to say about defeat and it was about the temperature of the
        /// pad. Losing to a wild Craggle on Mosswell and losing the fight the entire game builds
        /// to both read "You woke at the Nest Station." The pad line is right for a mauling in a
        /// shell field; it is not what a person who has just been beaten by another person
        /// notices.
        /// </summary>
        public static string WokeAfter(string trainerName, bool coldPad)
        {
            string pad = coldPad ? WokeCold : WokeWarm;
            if (string.IsNullOrEmpty(trainerName)) return pad;
            return trainerName + " sent you back. " + pad;
        }

        /// <summary>
        /// And losing to Amy, which is not the same as losing to anybody else. She is the reason
        /// every pad in the sector is cold, so a line about the pad being warm is the one thing
        /// that must not be the whole of it. She also does not gloat: eleven years of holding the
        /// first egg shut is not a thing you crow about.
        /// </summary>
        public static readonly string[] WokeAfterAmy =
        {
            "Amy carried you back up herself. She did not say anything on the way.",
            "You came round at the pad. Somebody had set your cartons in a neat row beside you.",
            "Amy put you back on the ship. \"Warm them up. Then come down again.\"",
        };

        public static readonly string[] RestIdle =
        {
            "Nothing needed doing. The pad is warm anyway.",
            "Everything is already whole. The Keeper nods at you.",
            "Your eggs settle in, then look at you expectantly.",
            "You sit a while. The station hums along with the shells.",
        };

        // The card that follows Amy's last words. It used to read "AMY IS BEATEN" and "Fine.
        // You cracked it." - written before the story was, and it undid the scene it followed.
        // Amy is not beaten. Amaranth is the first egg, she has held it shut for eleven years by
        // pulling warmth off every nest in the sector, and what she needed was somebody to turn
        // up with a full nest and take a share of the weight. The ending is a relief, not a win.
        public const string VictoryHeading = "THE SHELL HOLDS";

        public const string VictoryBody =
            "Amy moves over. It is not a ceremony; she shifts her weight and makes room.\n\n" +
            "The seam under Amaranth stops widening that afternoon. Nobody anywhere notices.\n" +
            "Cinderoost's ash goes warm. Brineholt's water stops climbing up the stones.\n" +
            "Vesper stays dark a while longer. Vess says she can wait, now that it means something.\n\n" +
            "Ori writes it down as a good year for hatching. That is all it will ever be called.\n\n";

        /// <summary>
        /// Only for a player who read every inscription. Sixteen of them record the year a thing
        /// started going wrong and not one records anybody fixing it - which is the argument the
        /// Shell Line's last word makes, and this is the moment it is answered.
        /// </summary>
        /// <summary>
        /// The egg that did the most of it. The game had no idea which of your six had carried
        /// the run, and by the end that is the one thing about your team you actually know.
        /// </summary>
        public static string VictoryCarried(string name, int fights) =>
            "<color=#D2D8E4>" + name + " was out in front for " + fights + " of them.</color>\n";

        public const string VictoryCoda =
            "<color=#A8B2C4>You read every stone in the sector. Not one of them recorded a mending.\n" +
            "Neither will this.</color>\n\n";

        /// <summary>The run's numbers, appended under the ending card.</summary>
        /// <summary>
        /// What the run came to. Two lines rather than one: the game keeps track of inscriptions
        /// read and caches dug and the tally mentioned neither, so a player who had walked every
        /// world end to end got the same closing line as one who flew straight through.
        /// </summary>
        public static string VictoryTally(int collected, int recorded, int catchable, int types,
                                          int inscriptions, int inscriptionsTotal,
                                          int caches, int cachesTotal) =>
            "<color=#A8B2C4>Eggs collected: " + collected +
            "   ·   Species recorded: " + recorded + " / " + catchable +
            "   ·   Types held: " + types + " / 8</color>\n" +
            "<color=#7A8090>Inscriptions read: " + inscriptions + " / " + inscriptionsTotal +
            "   ·   Caches recovered: " + caches + " / " + cachesTotal + "</color>";
    }
}
