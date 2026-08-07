using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Eggverse;

/// Which assertions never ran.
///
/// A suite this size answers "did anything fail" well and "did every check actually run" not at
/// all. A check sitting inside a guard that is never true passes forever and proves nothing, and
/// there is no way to spot one by reading — the landmark clearance check read like real work for
/// several revisions while testing the code against itself.
///
/// SelfCheck records the line of every assertion that fires. This reads the source, finds every
/// line that writes one, and reports the difference.
static class Coverage
{
    static readonly Regex Site = new Regex(@"(^|[^A-Za-z0-9_])check\s*\(", RegexOptions.Compiled);

    public static void Run(string sourcePath, HashSet<int> linesRun, Action<bool, string> check)
    {
        if (!File.Exists(sourcePath))
        {
            check(false, "SelfCheck.cs is where the coverage pass expects it: " + sourcePath);
            return;
        }

        var lines = File.ReadAllLines(sourcePath);
        var sites = new List<int>();
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            // Not the delegate's own definition, and not a line that only mentions it in prose.
            if (line.TrimStart().StartsWith("//")) continue;
            if (line.Contains("Check check") || line.Contains("delegate void Check")) continue;
            if (Site.IsMatch(line)) sites.Add(i + 1);
        }

        // A few assertions are meant never to fire: they sit in an error branch, or they scan
        // content for a mistake nobody has made. Those have to be declared, because "it never
        // ran" and "it can never fail" look identical from here and only one of them is fine.
        var dead = new List<int>();
        var tripwires = new List<int>();
        foreach (var at in sites)
        {
            if (linesRun.Contains(at)) continue;
            bool declared = false;
            for (int back = Math.Max(1, at - 4); back <= at; back++)
                if (lines[back - 1].Contains("tripwire:")) declared = true;
            if (declared) tripwires.Add(at); else dead.Add(at);
        }

        Console.WriteLine($"  {sites.Count} assertion sites, {sites.Count - dead.Count - tripwires.Count} ran, " +
                          $"{tripwires.Count} declared tripwires, {dead.Count} never ran and should have");
        foreach (var at in tripwires)
            Console.WriteLine($"    tripwire   SelfCheck.cs:{at}  {lines[at - 1].Trim()}");
        foreach (var at in dead)
            Console.WriteLine($"    NEVER RAN  SelfCheck.cs:{at}  {lines[at - 1].Trim()}");

        check(sites.Count > 0, "the coverage pass found the assertions");
        check(dead.Count == 0,
              dead.Count == 0
                  ? "every assertion runs, or says in writing why it never will"
                  : $"{dead.Count} assertion(s) never run and are not declared tripwires - " +
                    $"the first is SelfCheck.cs:{dead[0]}");

        // A tripwire that has quietly become most of the suite is a suite that checks nothing.
        check(tripwires.Count * 20 < sites.Count,
              $"tripwires are a handful, not the suite ({tripwires.Count} of {sites.Count})");
    }
}
