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

        // ---- supplies ----
        public static string ThrewCarton(int left) =>
            "You lobbed an egg carton!  (" + Words.Count(left, "carton") + " left)";
        public static string UsedSalve(int left) =>
            "You rubbed on a yolk salve.  (" + Words.Count(left, "salve") + " left)";
    }
}
