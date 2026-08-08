namespace Eggverse
{
    /// <summary>
    /// Everything the battle log says, in one place.
    ///
    /// Thirty-two sentences were written where they were spoken, built inline from a name and a
    /// number. The prose pass reads every authored line in UiCopy and read none of these — and
    /// the battle log is the second most-read text in the game, after the dialogue box. A player
    /// who fights two hundred times reads "It cracked right through!" two hundred times and
    /// "Pebbles used Shell Bash!" a great deal more.
    ///
    /// Built rather than constant, because every one of them carries a name or a count. That is
    /// the same shape UiCopy.Health and VictoryCarried already have; what matters is that the
    /// wording lives somewhere a person can read all of it at once and notice that two lines
    /// disagree.
    /// </summary>
    public static class BattleLog
    {
        // ---- arriving ----
        public static string TrainerSendsOut(string trainer, string egg, int level) =>
            trainer + " sends out " + egg + "!  (Lv " + level + ")";

        public static string ElderAppears(string species, int level) =>
            "An <b>Elder " + species + "</b> heaves into view.  (Lv " + level + ")";

        public static string WildAppears(string egg, int level) =>
            "A wild " + egg + " appeared!  (Lv " + level + ")";

        public static string GoOut(string egg) => "Go, " + egg + "!";
        public static string ComeBack(string egg) => "Come back, " + egg + "!";

        // ---- refusals, which are the lines a player meets when they have misread something ----
        public static string SalveNotNeeded(string egg) => egg + " has not got a scratch on it.";
        public static string SalveOffered(string egg) =>
            egg + " is hurt. A <b>salve</b> mends it - it costs you the turn.";
        public static string NoStealing(string trainer) => trainer + "'s eggs are not yours to take.";
        public static string NoRunning(string trainer) => "There is no running from " + trainer + ".";

        // ---- the turn ----
        public static string TooDazed(string egg) => egg + " is too dazed to move!";
        public static string OutOfMoves(string egg) => egg + " has nothing left and flails!";
        public static string Used(string egg, string move) => egg + " used " + move + "!";
        public static string HeldOn(string egg) => egg + " held together on one shard of shell!";
        public static string HitTimes(int hits) => "Hit " + hits + " times!";

        // ---- health moving, which the floating numbers now echo ----
        public static string Jolted(string egg, int amount) => egg + " was jolted for " + amount + ".";
        public static string Drained(string egg, int amount) => egg + " drained " + amount + " HP.";
        public static string Recoiled(string egg, int amount) => egg + " took " + amount + " in recoil.";
        public static string Mended(string egg, int amount) => egg + " mended " + amount + " HP.";
        public static string MendedShort(string egg, int amount) => egg + " mended " + amount + ".";
        public static string Burned(string egg, int amount) => egg + " burned for " + amount + ".";
        public static string Recovered(string egg, int amount) => egg + " recovered " + amount + " HP.";

        // ---- stages ----
        public static string AttackRose(string egg) => egg + "'s attack rose!";
        public static string DefenceRose(string egg) => egg + "'s defence rose!";
        public static string SpeedRose(string egg) => egg + "'s speed rose!";
        public static string SpeedFell(string egg) => egg + "'s speed fell!";
        public static string TooHardheaded(string egg) => egg + " is too hardheaded to slow down.";

        // ---- conditions ----
        public static string AlreadyHas(string egg, EggStatus status) =>
            egg + " is already " + status.ToString().ToLowerInvariant() + ".";
        public static string ImmuneTo(EggType type) =>
            TypeChart.Name(type) + " eggs do not take that.";
        public static string ShookOff(string egg, EggStatus status) =>
            egg + " shook off the " + status.ToString().ToLowerInvariant() + ".";

        // ---- the swap menu ----
        //
        // The panel's heading and the message box under it asked the same question at the same
        // time, in two casings. The heading is where a question belongs; the box below it can
        // carry the thing a player actually needs at that moment, which is what swapping costs.
        public const string SwapHeading = "SEND OUT WHICH EGG?";
        public const string SwapCost = "Whoever comes out takes a hit on the way in.";
        public const string SwapForced = "Whoever comes out starts fresh.";

        // ---- a carton that did not hold ----
        public static string BrokeFree(int shakes) =>
            shakes >= 2 ? "So close! It broke free." : "It burst straight out!";

        public const string OfferName = "Press <b>N</b> to name it, or Space to carry on.";

        // ---- how the fight ended ----
        public static string WonElder(string egg) => "An Elder, no less. " + egg + " stands over it.";
        public static string WonUntouched(string egg) => "Not a scratch on " + egg + ".";
        public static string WonFast(string egg) => "One hit. " + egg + " barely looked up.";
        public static string WonNarrow(string egg) => "That was close. " + egg + " is still standing, just.";
        public const string WonLong = "A long one. Both of them are breathing hard.";
        public const string WonPlain = "You won the scrap.";

        /// <summary>
        /// What each action does, in the words a player needs at the moment they are choosing.
        ///
        /// The same job the move descriptions do one level down, and the same reason: this is
        /// where the mechanics actually get taught. Ori explains cartons once in the opening
        /// brief and Lune explains salves two sectors later; the menu explains both every time
        /// a player looks at it.
        /// </summary>
        public static readonly string[] ActionHelp =
        {
            "Choose a move.",
            "Throw one. Wear the egg down first — a healthy one kicks straight back out.",
            "Mends the egg in front of you. It costs you the turn.",
            "Bring another egg out. You take a hit on the way in.",
            "Get clear. A faster egg gets away more often.",
        };

        /// <summary>
        /// What a condition is doing to somebody, in the moment a player is choosing what to do
        /// about it.
        ///
        /// The card shows SCORCHED, CHILLED or DAZED, and the line that lands each one explains
        /// it — once. Three turns later the chip is a word with no meaning attached, and the
        /// player is picking a move without being reminded that their egg is moving at half
        /// speed. The chip is the state; this is what the state costs.
        /// </summary>
        public static string ConditionLine(EggInstance egg, bool yours)
        {
            if (egg == null || egg.Status == EggStatus.None) return "";
            string who = yours ? egg.Name : "It";
            switch (egg.Status)
            {
                case EggStatus.Scorched:
                    return "<color=#E5734A>" + who + " is scorched — losing shell every turn.</color>";
                case EggStatus.Chilled:
                    return "<color=#8FE3F2>" + who + " is chilled — moving at half speed.</color>";
                case EggStatus.Dazed:
                    return "<color=#FFC24D>" + who + " is dazed — may lose the turn outright.</color>";
            }
            return "";
        }

        public static string ActionMessageText(int index, EggInstance mine, bool isTrainer)
        {
            if (index < 0 || index >= ActionHelp.Length) return "";

            // The two the game refuses outright against a trainer say why, rather than sitting
            // greyed with no reason given.
            if (isTrainer && index == 1) return "<color=#9AA4B6>Nothing here is yours to take.</color>";
            if (isTrainer && index == 4) return "<color=#9AA4B6>There is no walking away from this one.</color>";

            // A salve on an untouched egg is the one case where the button is greyed for a
            // reason a player might not guess.
            if (index == 2 && mine != null && mine.CurrentHP >= mine.MaxHP)
                return "<color=#9AA4B6>" + mine.Name + " is not hurt.</color>";

            return ActionHelp[index];
        }

        // ---- supplies ----
        public static string ThrewCarton(int left) =>
            "You lobbed an egg carton!  (" + Words.Count(left, "carton") + " left)";
        public static string UsedSalve(int left) =>
            "You rubbed on a yolk salve.  (" + Words.Count(left, "salve") + " left)";
    }
}
