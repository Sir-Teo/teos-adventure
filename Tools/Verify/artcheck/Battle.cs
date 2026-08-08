using System;
using System.Collections.Generic;

/// Composes the battle screen at the canvas reference resolution so it can actually be looked
/// at. The layout suite proves no two panels overlap; it cannot say whether the screen reads
/// well, whether anything is cramped, or whether the eye lands where it should.
static class Battle
{
    public const int W = 1920, H = 1080;

    // ---- a 5x7 bitmap font, so the mock shows real strings rather than greeked blocks ----
    static readonly Dictionary<char, string[]> Font = Build();

    static Dictionary<char, string[]> Build()
    {
        var f = new Dictionary<char, string[]>();
        void G(char c, string rows) => f[c] = rows.Split('|');
        // The filled dot every egg row starts with, and the half-dot for a species you have
        // seen but not caught.
        G('\u25cf', ".###.|#####|#####|#####|#####|#####|.###.");
        G('\u25d0', ".###.|####.|####.|####.|####.|####.|.###.");

        // The hollow ring the chart puts on a world you have walked and not finished, and the
        // field record puts on a species you have not caught.
        G('\u25cb', ".###.|#...#|#...#|#...#|#...#|#...#|.###.");

        // The semicolon, which the ending's first line uses. It was printing as
        // "IT IS NOT A CEREMONY? SHE SHIFTS HER WEIGHT".
        G(';', ".....|..##.|..##.|.....|..##.|..#..|.#...");

        // The em dash. "New species — not in your record" is the game's own punctuation and it
        // was printing as "NEW SPECIES ? NOT IN YOUR RECORD".
        G('\u2014', ".....|.....|.....|#####|.....|.....|.....");

        // The evolution line's arrows, which the field record uses to say what a species grows
        // from and becomes. Every glyph the game draws and the font lacked printed as "?", and
        // a row of question marks reads as broken data rather than a missing mock glyph.
        G('\u2190', ".....|..#..|.#...|#####|.#...|..#..|.....");
        G('\u2192', ".....|..#..|...#.|#####|...#.|..#..|.....");

        // The stat bars in the field record are drawn from '=', which the font also lacked -
        // four rows of base stats had been rendering as rows of "?".
        G('=', ".....|.....|#####|.....|#####|.....|.....");

        // The two effectiveness arrows the move cards carry. Substituting a caret for them
        // printed "?" - the font simply had no glyph - and a missing arrow is precisely the
        // thing this render exists to show.
        G('\u25b2', "..#..|..#..|.###.|.###.|#####|#####|.....");
        G('\u25bc', ".....|#####|#####|.###.|.###.|..#..|..#..");
        G('A', ".###.|#...#|#...#|#####|#...#|#...#|#...#");
        G('B', "####.|#...#|#...#|####.|#...#|#...#|####.");
        G('C', ".###.|#...#|#....|#....|#....|#...#|.###.");
        G('D', "####.|#...#|#...#|#...#|#...#|#...#|####.");
        G('E', "#####|#....|#....|####.|#....|#....|#####");
        G('F', "#####|#....|#....|####.|#....|#....|#....");
        G('G', ".###.|#...#|#....|#.###|#...#|#...#|.###.");
        G('H', "#...#|#...#|#...#|#####|#...#|#...#|#...#");
        G('I', "#####|..#..|..#..|..#..|..#..|..#..|#####");
        G('J', "..###|...#.|...#.|...#.|...#.|#..#.|.##..");
        G('K', "#...#|#..#.|#.#..|##...|#.#..|#..#.|#...#");
        G('L', "#....|#....|#....|#....|#....|#....|#####");
        G('M', "#...#|##.##|#.#.#|#...#|#...#|#...#|#...#");
        G('N', "#...#|##..#|#.#.#|#..##|#...#|#...#|#...#");
        G('O', ".###.|#...#|#...#|#...#|#...#|#...#|.###.");
        G('P', "####.|#...#|#...#|####.|#....|#....|#....");
        G('Q', ".###.|#...#|#...#|#...#|#.#.#|#..#.|.##.#");
        G('R', "####.|#...#|#...#|####.|#.#..|#..#.|#...#");
        G('S', ".####|#....|#....|.###.|....#|....#|####.");
        G('T', "#####|..#..|..#..|..#..|..#..|..#..|..#..");
        G('U', "#...#|#...#|#...#|#...#|#...#|#...#|.###.");
        G('V', "#...#|#...#|#...#|#...#|#...#|.#.#.|..#..");
        G('W', "#...#|#...#|#...#|#...#|#.#.#|##.##|#...#");
        G('X', "#...#|#...#|.#.#.|..#..|.#.#.|#...#|#...#");
        G('Y', "#...#|#...#|.#.#.|..#..|..#..|..#..|..#..");
        G('Z', "#####|....#|...#.|..#..|.#...|#....|#####");
        G('0', ".###.|#...#|#..##|#.#.#|##..#|#...#|.###.");
        G('1', "..#..|.##..|..#..|..#..|..#..|..#..|.###.");
        G('2', ".###.|#...#|....#|...#.|..#..|.#...|#####");
        G('3', "#####|...#.|..#..|...#.|....#|#...#|.###.");
        G('4', "...#.|..##.|.#.#.|#..#.|#####|...#.|...#.");
        G('5', "#####|#....|####.|....#|....#|#...#|.###.");
        G('6', "..##.|.#...|#....|####.|#...#|#...#|.###.");
        G('7', "#####|....#|...#.|..#..|.#...|.#...|.#...");
        G('8', ".###.|#...#|#...#|.###.|#...#|#...#|.###.");
        G('9', ".###.|#...#|#...#|.####|....#|...#.|.##..");
        G(' ', ".....|.....|.....|.....|.....|.....|.....");
        G('/', "....#|....#|...#.|..#..|.#...|#....|#....");
        G('(', "..##.|.#...|#....|#....|#....|.#...|..##.");
        G(')', ".##..|...#.|....#|....#|....#|...#.|.##..");
        G('.', ".....|.....|.....|.....|.....|.##..|.##..");
        G(',', ".....|.....|.....|.....|.##..|.##..|.#...");
        G('!', "..#..|..#..|..#..|..#..|..#..|.....|..#..");
        G('?', ".###.|#...#|....#|...#.|..#..|.....|..#..");
        G('-', ".....|.....|.....|#####|.....|.....|.....");
        G('+', ".....|..#..|..#..|#####|..#..|..#..|.....");
        G('%', "##..#|##..#|...#.|..#..|.#...|#..##|#..##");
        G(':', ".....|.##..|.##..|.....|.##..|.##..|.....");
        G('\'', "..#..|..#..|.....|.....|.....|.....|.....");
        G('·', ".....|.....|.....|..#..|.....|.....|.....");
        G('*', "#...#|.#.#.|#####|.#.#.|#####|.#.#.|#...#");   // stands in for the ● dot
        return f;
    }

    public struct Ctx { public Col[] Px; }

    static void Px(Ctx c, int x, int y, Col col, float a)
    {
        if (x < 0 || y < 0 || x >= W || y >= H || a <= 0f) return;
        Program.OverPublic(c.Px, y * W + x, col, a * col.a);
    }

    public static void Rect(Ctx c, float x0, float y0, float x1, float y1, Col col)
    {
        for (int y = Math.Max(0, (int)y0); y < Math.Min(H, (int)y1); y++)
            for (int x = Math.Max(0, (int)x0); x < Math.Min(W, (int)x1); x++)
                Px(c, x, y, col, 1f);
    }

    public static void Disc(Ctx c, float cx, float cy, float rad, Col col, float soft)
    {
        for (int y = Math.Max(0, (int)(cy - rad)); y < Math.Min(H, (int)(cy + rad + 1)); y++)
            for (int x = Math.Max(0, (int)(cx - rad)); x < Math.Min(W, (int)(cx + rad + 1)); x++)
            {
                float d = (float)Math.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / rad;
                if (d > 1f) continue;
                Px(c, x, y, col, M.Clamp01((1f - d) / soft));
            }
    }

    public static void Ellipse(Ctx c, float cx, float cy, float rx, float ry, Col col, float soft = 0.06f)
    {
        for (int y = Math.Max(0, (int)(cy - ry)); y < Math.Min(H, (int)(cy + ry + 1)); y++)
            for (int x = Math.Max(0, (int)(cx - rx)); x < Math.Min(W, (int)(cx + rx + 1)); x++)
            {
                float dx = (x - cx) / rx, dy = (y - cy) / ry;
                float d = (float)Math.Sqrt(dx * dx + dy * dy);
                if (d > 1f) continue;
                Px(c, x, y, col, M.Clamp01((1f - d) / soft));
            }
    }

    // A 5x7 cell advancing a full cell width would run 0.86em per character; real Arial
    // averages about 0.52em, the ratio the text-fit suite measures against. Drawing at the
    // wider advance made every dense label look like it overflowed when it does not, so the
    // glyphs are drawn narrow and the advance matches the metric the rest of the harness uses.
    const float Advance = 0.52f;        // em per character
    static float XScale(int fontSize) => Advance * fontSize / 6f;
    static float YScale(int fontSize) => fontSize / 7f;

    public static float TextWidth(string s, int fontSize) => s.Length * Advance * fontSize;

    public static void Text(Ctx c, string s, float x, float yTop, int fontSize, Col col)
    {
        float sx = XScale(fontSize), sy = YScale(fontSize);
        float pen = x;
        foreach (char raw in s.ToUpperInvariant())
        {
            char ch = Font.ContainsKey(raw) ? raw : '?';
            var g = Font[ch];
            for (int row = 0; row < 7; row++)
                for (int col2 = 0; col2 < 5; col2++)
                {
                    if (g[row][col2] != '#') continue;
                    float px0 = pen + col2 * sx, py1 = yTop - row * sy;
                    Rect(c, px0, py1 - sy, px0 + sx + 0.5f, py1, col);
                }
            pen += 6f * sx;
        }
    }

    static void TextRight(Ctx c, string s, float xRight, float yTop, int fontSize, Col col) =>
        Text(c, s, xRight - TextWidth(s, fontSize), yTop, fontSize, col);

    public static void TextCentre(Ctx c, string s, float xCentre, float yTop, int fontSize, Col col) =>
        Text(c, s, xCentre - TextWidth(s, fontSize) * 0.5f, yTop, fontSize, col);

    public static readonly Col Ink = Col.Hex(0xF2F5FA);
    public static readonly Col InkDim = Col.Hex(0xA8B2C4);
    static readonly Col PanelDark = Col.Hex(0x121422);
    static readonly Col PanelMid = Col.Hex(0x1E2238);
    static readonly Col PanelLight = Col.Hex(0x2C3250);
    public static readonly Col Accent = Col.Hex(0xFFC24D);
    static readonly Col Good = Col.Hex(0x5FD068);

    static Col Health(float f) =>
        f > 0.5f ? Good : f > 0.22f ? Col.Hex(0xE5C055) : Col.Hex(0xE55555);

    /// One combatant card, mirroring BattleMode.BuildCard.
    static void Card(Ctx c, float x0, float y0, float x1, float y1,
                     string name, string type, Col typeCol, string meta,
                     float hpFrac, string hpText, bool withXp)
    {
        float w = x1 - x0;
        Rect(c, x0, y0, x1, y1, PanelMid);

        Text(c, name, x0 + 24, y1 - 14, 30, Ink);

        // type chip, right aligned
        Rect(c, x1 - 24 - 150, y1 - 14 - 32, x1 - 24, y1 - 14, typeCol);
        TextCentre(c, type, x1 - 24 - 75, y1 - 14 - 6, 20, Col.Hex(0x141420));

        Text(c, meta, x0 + 24, y1 - 52, 18, InkDim);

        // hp bar
        float bx0 = x0 + 24, bx1 = x1 - 24, by1 = y1 - 80, by0 = by1 - 18;
        Rect(c, bx0, by0, bx1, by1, Col.Hex(0x0C0E18));
        Rect(c, bx0, by0, bx0 + (bx1 - bx0) * hpFrac, by1, Health(hpFrac));

        if (hpText != null) TextRight(c, hpText, x1 - 24, y1 - 104, 22, InkDim);

        if (withXp)
        {
            Text(c, "XP", x0 + 24, y0 + 46, 18, InkDim);
            Rect(c, x0 + 66, y0 + 24, x0 + 666, y0 + 36, Col.Hex(0x0C0E18));
            Rect(c, x0 + 66, y0 + 24, x0 + 66 + 600 * 0.42f, y0 + 36, Col.Hex(0x62C8F5));
        }
    }

    static void Egg(Ctx c, float cx, float cy, float rad, Col body, Col accent, int seed)
    {
        // Egg silhouette: taller than wide, fatter at the bottom.
        Ellipse(c, cx, cy - rad * 0.06f, rad * 0.74f, rad, body);
        // shading
        Ellipse(c, cx + rad * 0.26f, cy - rad * 0.30f, rad * 0.52f, rad * 0.66f, new Col(0, 0, 0, 0.22f), 0.9f);
        Ellipse(c, cx - rad * 0.24f, cy + rad * 0.34f, rad * 0.30f, rad * 0.34f, new Col(1, 1, 1, 0.20f), 0.9f);
        var rng = new Random(seed);
        for (int i = 0; i < 7; i++)
        {
            float a = (float)rng.NextDouble() * 6.2832f;
            float d = (float)Math.Sqrt(rng.NextDouble()) * rad * 0.55f;
            Ellipse(c, cx + (float)Math.Cos(a) * d, cy + (float)Math.Sin(a) * d,
                    rad * 0.13f, rad * 0.10f, new Col(accent.r, accent.g, accent.b, 0.85f), 0.5f);
        }
    }

    /// `menu` picks which of the two right-hand panels is showing.
    public static Col[] Render(string menu)
    {
        var c = new Ctx { Px = new Col[W * H] };
        for (int i = 0; i < c.Px.Length; i++) c.Px[i] = Col.Hex(0x080A14);

        // The two combatants, built before they are drawn, so the eggs are coloured from the
        // same species the cards describe. These were two hardcoded colour pairs typed against
        // two hardcoded positions - and they had drifted apart from the cards, so the render
        // showed the player's green egg in the foe's slot and the foe's blue one in the
        // player's. Reading that screenshot, the layout looked broken. It was not; the picture
        // was. A transcription can raise a false alarm as easily as give false comfort.
        var foeDef = Eggverse.SpeciesDatabase.Get("wavelet");
        var mineDef = Eggverse.SpeciesDatabase.Get("sprouteg");
        Col foeBody = new Col(foeDef.Body.r, foeDef.Body.g, foeDef.Body.b, 1f);
        Col foeAccent = new Col(foeDef.Accent.r, foeDef.Accent.g, foeDef.Accent.b, 1f);
        Col mineBody = new Col(mineDef.Body.r, mineDef.Body.g, mineDef.Body.b, 1f);
        Col mineAccent = new Col(mineDef.Accent.r, mineDef.Accent.g, mineDef.Accent.b, 1f);

        // Foe upper right, yours lower left, each diagonally opposite its own card. That is the
        // game's placement: FoeEgg anchors top-right at (-430,-330), MyEgg bottom-left at
        // (440,545), and the cards sit on the far side so neither covers the other.
        Disc(c, 1490, 750, 450, new Col(foeBody.r, foeBody.g, foeBody.b, 0.32f), 1.4f);
        Disc(c, 440, 540, 500, new Col(mineBody.r, mineBody.g, mineBody.b, 0.30f), 1.4f);

        Egg(c, 1490, 750, 150, foeBody, foeAccent, foeDef.ArtSeed);
        Egg(c, 440, 545, 180, mineBody, mineAccent, mineDef.ArtSeed);

        // Both plates from the game's own builders. Typed out, the meta line read
        // "WARM YOLK  ATK +1" - the game draws "ATK +1▲", with one arrow per stage, and the
        // arrows are how you read the magnitude at a glance.
        var foe = Eggverse.EggInstance.Wild(foeDef.Id, 21);
        var mine = Eggverse.EggInstance.Wild(mineDef.Id, 22);
        mine.Nickname = "Pebbles";
        mine.AtkStage = 1;                       // so the plate's stage arrow is exercised
        mine.CurrentHP = mine.MaxHP / 3;         // and the health bar's low-HP colour
        var bstate = new Eggverse.GameState(false);

        // Foe card: x 70..730, y 860..1010
        Card(c, 70, 860, 730, 1010, Strip(Eggverse.BattleMode.PlateName(foe)),
             Eggverse.TypeChart.Name(foe.Type).ToUpperInvariant(), Col.Hex(TypeHex(foe.Type)),
             Strip(Eggverse.BattleMode.PlateMeta(foe)).ToUpperInvariant(), 0.62f, null, false);
        Text(c, Eggverse.BattleMode.PlateRecord(foe, bstate, false).ToUpperInvariant(),
             94, 1010 - 104, 20, Accent);

        // My card: x 1150..1850, y 325..535
        Card(c, 1150, 325, 1850, 535, Strip(Eggverse.BattleMode.PlateName(mine)),
             Eggverse.TypeChart.Name(mine.Type).ToUpperInvariant(), Col.Hex(TypeHex(mine.Type)),
             Strip(Eggverse.BattleMode.PlateMeta(mine, foe)).ToUpperInvariant(), 0.34f,
             mine.CurrentHP + "/" + mine.MaxHP + " HP", true);

        // Message box: x 40..1120, y 40..250
        Rect(c, 40, 40, 1120, 250, PanelDark);
        Rect(c, 40, 246, 1120, 250, PanelLight);
        // While the move menu is open the game explains the highlighted move here. The render
        // showed the root prompt instead, so the panel looked emptier than it ever is.
        if (menu == "move")
        {
            var mv0 = Eggverse.MoveDatabase.Get("voltcrack");
            var msg = Eggverse.BattleMode.MoveMessageText(new Eggverse.MoveSlot(mv0), Eggverse.EggType.Tidal)
                                         .Split('\n');
            Text(c, (Strip(msg[0])), 72, 196, 28, Ink);
            if (msg.Length > 1) Text(c, Strip(msg[1]), 72, 196 - 28f * 1.16f, 24, InkDim);
        }
        else
        {
            // The action menu explains whichever action is highlighted, the same way the move
            // menu below it does. FIGHT is the default cursor position.
            Text(c, ("What will " + mine.Name + " do?").ToUpperInvariant(), 72, 196, 30, Ink);
            Text(c, Strip(Eggverse.BattleLog.ActionMessageText(0, mine, false)).ToUpperInvariant(),
                 72, 196 - 30f * 1.16f - 8f, 24, InkDim);
        }
        Text(c, Eggverse.UiCopy.BattleFooter.ToUpperInvariant(), 72, 84, 19, InkDim);

        if (menu == "action")
        {
            // Action menu: x 1140..1880, y 40..300, five buttons in a 2x3 grid
            Rect(c, 1140, 40, 1880, 300, PanelDark);
            Rect(c, 1140, 296, 1880, 300, PanelLight);
            string[] labels = { "FIGHT", "CARTON (12)", "SALVE (4)", "SWAP", "RUN" };
            for (int i = 0; i < labels.Length; i++)
            {
                float bx = 1140 + 20 + (i % 2) * 350;
                float byTop = 300 - 20 - (i / 2) * 78;
                bool selected = i == 0;
                Rect(c, bx, byTop - 64, bx + 330, byTop, selected ? Col.Hex(0x46507E) : PanelLight);
                if (selected) Rect(c, bx, byTop - 64, bx + 5, byTop, Accent);
                Text(c, labels[i], bx + 22, byTop - 20, 26, i == 2 ? Ink : Ink);
            }
        }
        else
        {
            // Move menu: same anchor, 210 tall, four buttons
            Rect(c, 1140, 40, 1880, 250, PanelDark);
            Rect(c, 1140, 246, 1880, 250, PanelLight);
            // The game's own card text, not a transcription of it. The transcription had no
            // effectiveness arrows on it at all - the one mark on a card that changes which
            // move you pick - and I had been looking at this render for weeks.
            var slots = new System.Collections.Generic.List<Eggverse.MoveSlot>();
            // Real ids. Guessed ones silently became Shell Bash - MoveDatabase.Get falls back to
            // tackle rather than throwing, which is right for a save file and invisible here.
            // Chosen to show all three states at once: resisted, super-effective, and neither.
            foreach (var id in new[] { "frostcrack", "voltcrack", "lavayolk", "harden" })
            {
                var mv = Eggverse.MoveDatabase.Get(id);
                if (mv != null) slots.Add(new Eggverse.MoveSlot(mv));
            }

            for (int i = 0; i < slots.Count && i < 4; i++)
            {
                float bx = 1140 + 20 + (i % 2) * 350;
                float byTop = 250 - 20 - (i / 2) * 90;
                bool selected = i == 0;
                Rect(c, bx, byTop - 74, bx + 330, byTop, selected ? Col.Hex(0x46507E) : PanelLight);
                if (selected) Rect(c, bx, byTop - 74, bx + 5, byTop, Accent);

                var rows = Eggverse.BattleMode.MoveCardText(slots[i], Eggverse.EggType.Tidal).Split('\n');
                float blockH = 24f * 1.16f + 17f * 1.16f * (rows.Length - 1);
                float top = byTop - (74f - blockH) * 0.5f;
                for (int r = 0; r < rows.Length; r++)
                {
                    int size = r == 0 ? 24 : 17;
                    float y = top - (r == 0 ? 0f : 24f * 1.16f + (r - 1) * 17f * 1.16f);
                    // Run by run. FirstColour painted the whole row in the first colour it found,
                    // and row zero is "<element>Frost Crack</element> <orange>▲</orange>" — so
                    // the effectiveness arrow, the one mark on this card a player is actually
                    // deciding on, came out in the move's element colour instead of its own.
                    float pen = bx + 22;
                    foreach (var run in Split(rows[r], InkDim))
                    {
                        if (run.text.Trim().Length > 0)
                            Text(c, run.text, pen, y, size, run.col);
                        pen += TextWidth(run.text, size);
                    }
                }
            }
        }
        return c.Px;
    }

    public static void Report()
    {
        // What the eye should confirm, stated as numbers too.
        Console.WriteLine("  action grid: 5 buttons, rows at y 216-280 / 138-202 / 60-124, panel floor y 40");
        Console.WriteLine($"  longest action label \"CARTON (12)\" = {TextWidth("CARTON (12)", 26):0}px in a 330px button");
        Console.WriteLine($"  message box text at 30px = {TextWidth("WHAT WILL PEBBLES DO?", 30):0}px in 1016px");
    }

    static string Strip(string t) =>
        System.Text.RegularExpressions.Regex.Replace(t, "<[^>]+>", "");



    /// A rich-text line as coloured runs, so a line that changes colour partway through is drawn
    /// the way the game draws it rather than in whichever colour came first.
    static System.Collections.Generic.List<(string text, Col col)> Split(string line, Col baseCol)
    {
        var outp = new System.Collections.Generic.List<(string, Col)>();
        int i = 0;
        Col current = baseCol;
        while (i < line.Length)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                line.Substring(i), @"^<(/?)(b|i|color|size)(=[^>]*)?>");
            if (m.Success)
            {
                if (m.Groups[1].Value == "/") current = baseCol;
                else if (m.Groups[2].Value == "color" && m.Groups[3].Value.Length == 8)
                    current = Col.Hex(Convert.ToInt32(m.Groups[3].Value.Substring(2), 16));
                i += m.Length;
                continue;
            }
            int next = line.IndexOf('<', i + 1);
            if (next < 0) next = line.Length;
            outp.Add((line.Substring(i, next - i), current));
            i = next;
        }
        return outp;
    }

    static Col FirstColour(string richText, Col fallback)
    {
        var m = System.Text.RegularExpressions.Regex.Match(richText, "<color=#([0-9A-Fa-f]{6})>");
        return m.Success ? Col.Hex(Convert.ToInt32(m.Groups[1].Value, 16)) : fallback;
    }

    static int TypeHex(Eggverse.EggType t)
    {
        var col = Eggverse.TypeChart.ColorOf(t);
        return ((int)(col.r * 255) << 16) | ((int)(col.g * 255) << 8) | (int)(col.b * 255);
    }
}
