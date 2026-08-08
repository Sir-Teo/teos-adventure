using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

/// Every change to an egg's health in a fight shows a number.
///
/// Damage floated one since the fight screen was written. A salve, a drain, the recoil off your
/// own move, the tick off a burn, the Static trait jolting whoever threw the punch — none did.
/// Six ways to move a health bar and one of them said by how much.
///
/// This is a source scan because the thing being checked is a coroutine that needs the engine.
/// What can be verified from here is the property that actually went wrong: a place where health
/// changes and nothing was put on screen beside it. The next effect somebody adds gets asked the
/// same question.
static class Feedback
{
    static readonly Regex Change = new Regex(@"\.(TakeDamage|Heal)\s*\(", RegexOptions.Compiled);
    static readonly Regex Shown = new Regex(@"Spawn(Damage|Heal)Number\s*\(", RegexOptions.Compiled);

    /// How far from the health change the number is allowed to be.
    const int Window = 4;

    /// Every moment an egg appears in front of the player, it says something.
    ///
    /// Five places play a cry, at five volumes that were five numbers typed at five call sites -
    /// an ordering that existed in my head and nowhere a check could reach. Naming them made it
    /// a rule; this is the rule.
    public static void Cries(string[] sources, Action<bool, string> check)
    {
        int sites = 0;
        var raw = new List<string>();
        foreach (var path in sources)
        {
            if (!File.Exists(path)) { check(false, "the cry pass can find " + path); continue; }
            foreach (var line in File.ReadAllLines(path))
            {
                if (line.TrimStart().StartsWith("//")) continue;
                if (!line.Contains("PlayCry(")) continue;
                if (line.Contains("public void PlayCry")) continue;
                sites++;
                // A literal volume at a call site is the thing that used to be everywhere.
                var m = Regex.Match(line, @"PlayCry\([^,]+,\s*([0-9]*\.?[0-9]+f)\s*\)");
                if (m.Success) raw.Add(m.Groups[1].Value);
            }
        }

        Console.WriteLine($"  {sites} places play a cry, {raw.Count} with a volume typed in place");
        check(sites >= 5, $"an egg appearing is heard in every place it appears ({sites})");
        check(raw.Count == 0,
              raw.Count == 0
                  ? "every cry is played at a named volume"
                  : $"{raw.Count} cry volume(s) are typed at the call site: {string.Join(", ", raw)}");
    }

    /// Nobody writes 1.2 or 0.8 at a call site again.
    ///
    /// They were bare literals in thirteen places across five concerns, all meaning "strong" and
    /// "weak" — Tough Shell's trigger, the screen shake, a damage number's colour and size, the
    /// move card's arrow, the message box's wording, the swap menu's warning, and the record
    /// page's three matchup lists. Naming them made it a rule. This is what keeps it one.
    public static void Thresholds(string[] sources, Action<bool, string> check)
    {
        var loose = new List<string>();
        int scanned = 0;
        foreach (var path in sources)
        {
            if (!File.Exists(path)) { check(false, "the threshold pass can find " + path); continue; }
            var lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.TrimStart().StartsWith("//") || line.TrimStart().StartsWith("///")) continue;
                // The declarations themselves are where the numbers belong.
                if (line.Contains("StrongAbove =") || line.Contains("WeakBelow =")) continue;
                scanned++;
                if (Regex.IsMatch(line, @"[<>]=?\s*1\.2f") || Regex.IsMatch(line, @"[<>]=?\s*0\.8f"))
                    loose.Add(Path.GetFileName(path) + ":" + (i + 1) + "  " + line.Trim());
            }
        }

        Console.WriteLine($"  {scanned} lines scanned for loose effectiveness thresholds, {loose.Count} found");
        foreach (var l in loose) Console.WriteLine("    LOOSE  " + l);

        check(scanned > 0, "the threshold pass read the sources");
        check(loose.Count == 0,
              loose.Count == 0
                  ? "every reading of strong and weak goes through TypeChart"
                  : $"{loose.Count} loose threshold(s): {loose[0]}");
    }

    public static void Run(string sourcePath, Action<bool, string> check)
    {
        if (!File.Exists(sourcePath))
        {
            check(false, "BattleMode.cs is where the feedback pass expects it: " + sourcePath);
            return;
        }

        var lines = File.ReadAllLines(sourcePath);
        var silent = new List<int>();
        int found = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.TrimStart().StartsWith("//")) continue;
            if (!Change.IsMatch(line)) continue;
            // The declarations themselves, and the counter that reads health rather than moving it.
            if (line.Contains("public void") || line.Contains("public int")) continue;

            found++;
            bool shown = false;
            for (int j = Math.Max(0, i - Window); j <= Math.Min(lines.Length - 1, i + Window); j++)
                if (Shown.IsMatch(lines[j])) shown = true;
            if (!shown) silent.Add(i + 1);
        }

        Console.WriteLine($"  {found} health changes in a fight, {found - silent.Count} show a number");
        foreach (var at in silent)
            Console.WriteLine($"    SILENT  BattleMode.cs:{at}  {lines[at - 1].Trim()}");

        check(found > 0, "the feedback pass found the health changes");
        check(silent.Count == 0,
              silent.Count == 0
                  ? "every health change in a fight puts a number on screen"
                  : $"{silent.Count} health change(s) move a bar and say nothing - " +
                    $"the first is BattleMode.cs:{silent[0]}");
    }
}
