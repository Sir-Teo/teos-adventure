# Offline verification

Eggverse has no automated tests inside Unity, because it has no scene, no prefabs and no assets —
everything is built at runtime from code. That makes it unusually easy to check *outside* Unity:
the game's data and maths can be compiled against Unity's reference assemblies and exercised in a
plain .NET console app, with no Editor and no play mode.

That is what this directory is. It needs Unity installed (for its bundled Roslyn and reference
DLLs) but never launches it.

## Running it

```bash
Tools/Verify/verify.sh
```

Every step is fatal. It typechecks the runtime scripts, typechecks them again together with the
Editor scripts, and then runs every suite. Roughly a thousand lines of output ending in
`ALL CHECKS PASSED`.

If your Unity lives somewhere unusual:

```bash
UNITY_VERSION=6000.5.7f1 Tools/Verify/verify.sh
UNITY_SCRIPTING=/path/to/Unity.app/Contents/Resources/Scripting Tools/Verify/verify.sh
```

## What it covers

`verify.sh` runs two things.

**The shipped self-check** (`Assets/Scripts/Eggverse/Verify/SelfCheck.cs`) is part of the game —
it also runs from the `Eggverse ▸ Run Self-Check` menu item inside Unity. About 2,300 assertions
over the type chart, progression, species, palettes, and whether every authored string fits the
box it is drawn in.

**The offline suites** (`storysim/`) link the compiled game and exercise things the self-check
cannot: thousands of simulated battles for balance, a full playthrough for state invariants,
save round-trips including a deliberately corrupt file, uGUI anchor arithmetic reproduced to catch
overlapping panels, CIE Lab colour distance under dichromat simulation, audio buffers checked for
silence and loop seams, and a prose pass over every string the game can display.

`artcheck/` is separate and produces images rather than assertions:

```bash
Tools/Verify/artcheck/run.sh
```

It renders the battle screen, the navigation chart, the collection screen, a planet surface, the
six landmark silhouettes and the star map at the canvas reference resolution, using the real
`PlanetDatabase` and `SpeciesDatabase`. Several real layout problems were only ever visible this way — a chart wasting
44% of its width, a card carrying a third of its height as dead space, a collection column half
empty.

## Checking that a check works

```bash
Tools/Verify/plant.sh <file> <find> <replace> [expected text in the failure]
```

Plants a fault, runs the suite, restores the file, and reports whether the fault was caught.

It exists because twice a hand-run version of this produced no failure and I read that as the
check passing — when in fact the `find` had matched nothing and the file was never touched. **A
no-op edit and a working check look identical from the outside.** So this refuses to draw any
conclusion unless it can prove the plant landed: it counts the occurrences, aborts at zero, and
compares the file against its backup before running anything.

It distinguishes five outcomes, which is the whole point:

- `ABORT` — the pattern matched nothing. Nothing was tested; the result means nothing.
- `ABORT` — the planted code does not compile, so the suite never ran.
- `ABORT` — the suite crashed rather than finishing. Also produces no failures, and also is not
  "still passes". A check that built a `GameObject` took the entire self-check down with it and
  read as toothless. A plant that fails to
  build produces no failures either, and reporting that as "still passes" is the same mistake
  this script exists to prevent. It happened while testing a coda check, and read exactly like a
  missing assertion.
- `NOT CAUGHT` — the fault landed and the suite still passed. The check is missing or toothless.
- `caught:` — with the failures listed, and a note if the expected one is among them.

It also rebuilds after restoring the file. Restoring the source is not enough: the suite builds
before it checks, so a planted run leaves the fault compiled into `Eggverse.dll`, and `artcheck`
links that DLL. A render will then show a defect that exists in no source file — which happened,
and looked exactly like a real layout bug in the game.

### Checks that assert nothing

Three checks in this suite turned out to be passing without testing anything, and all three had
the same shape: an assertion that could be *skipped* rather than fail.

- The landmark dialogue check measured lines against a box that holds five of them, so no
  inscription could ever overflow it.
- The party-row check built its worst case from a long nickname — but nicknames are capped at
  twelve, so widening the truncation limit changed nothing.
- The gate-hint check ran only `if` the blocker text mentioned types, so switching the hint off
  entirely left it silent. Worse, the party it built to be "one type short" was not short at all,
  because the gate wants four types and the test assumed six.

Each was found by planting a fault and watching nothing happen. Where a check only fires under a
condition, count how many times it fired and assert that number is not zero — `verify.sh` now
reports coverage for the guarded blocks that could legitimately run zero times.

Build test data from the game's own constants, never from a number you remember. The gate-hint
check was wrong precisely because six was a plausible thing to remember.

A check that has never failed is a check nobody has verified.

Landmarks are the clearest case for rendering. All seventeen were drawn as one identical grey
disc — every assertion passed, and a bell, a ship's bow and nine hundred cairns were the same
rock. `landmarks.png` puts the six forms side by side at the camera's own scale, each standing on
the world it actually belongs to with Teo below it for size.

That sheet used to draw the forms itself on flat colour swatches, which meant two mock
implementations of the same shapes — and the other one, buried in the world render, was still a
generic stone that had never heard of the six forms. It composites real `Surface` renders now, so
the shapes are judged against real terrain and real decor. That immediately showed a form failing
in a way flat swatches could not: on Brineholt the five set stones were dark blobs among dark
decor blobs, so the one made thing on the world looked like more scenery.

## A warning about the mocks

Every panel whose text was transcribed into the renderer rather than read from the game turned
out to be wrong, without exception — five screens, five faults:

| Screen | What the render showed | What the game shows |
|---|---|---|
| Navigation chart | `RECORDED HERE`, `NEST STATION  YES`, `TRAVEL  1.5S` | tagline, spawn list, distance, cache, landmark |
| Battle move cards | no effectiveness arrows at all | `▲` / `▼` on every card |
| Battle message box | the root prompt, while the move menu was open | the highlighted move and its rider |
| Field record lore | the blurb only | matchups, base stats, evolution, found-on |
| Egg panel | no `STATS` heading; `AHEAD` above `CONDITION` | `CONDITION`, `STATS`, `AHEAD`, `MOVES` |

All five looked entirely convincing. That is the point: a transcription is written once, from a
correct reading, and then the game moves and it does not. The fix each time was to extract the
text builder from the view — `GalaxyMapView.DetailBody`, `BattleMode.MoveCardText`,
`HudView.DexLoreText`, `HudView.EggLoreText` — so the renderer and the checks both read the game.

If you add a panel to a render, do not type its contents.

**A transcription raises a false alarm as readily as it gives false comfort.** The battle render
drew its two eggs from two colour pairs typed against two positions, and those had drifted from
the cards — so the picture showed the player's green egg in the foe's slot and the foe's blue one
in the player's. Looking at that screenshot, the layout appeared broken. It was not; the picture
was. The eggs are built from the same species the cards describe now, and the four placements are
constants the self-check measures: nothing covers anything, each egg sits diagonally opposite its
own card, and yours is the larger of the two because yours is nearer.

The collection screen was the last one still laying out a column itself. Converting its two
columns to draw `CollectionBodyText` and `CollectionDexText` line for line immediately showed
four things the render had been hiding: the party's 1-6 numbers, the nest's real summary line,
the record's actual caught/seen marks, and a nest ordering line that had been added to the game
and could never have appeared.

It also found a fault of a different shape. The footer *did* call `HintFor` — from a state built
separately, with one egg in the nest against the fifty-six drawn above it. **Two states in one
screenshot**: a full nest and a footer describing a game that had almost nothing in it. One
roster now feeds the whole picture.

The navigation chart had **three**. Its markers came from `(visited++ % 3) != 2` — every third
world unvisited, arbitrarily — its header counted charted worlds off a fully-visited state, and
its detail panel was built from a third. Each was convincing on its own. One roster there too,
now a real mid-run save: thirteen charted, the outer sectors still dark, and the header says so
because it counts the same state.

And a bug in the renderer's own tag parser: a `<` that matched none of `<b>`, `<color=…>` or
their closers consumed zero characters and looped forever. The record column carries `<size=…>`.
It presented as "Out of memory" halfway through a render, which is not what an infinite loop
usually looks like. The same code had been copied into three files.

One more shape of it, found by looking at a panel that had been rendered a dozen times: the egg
detail column coloured **a whole line by the first colour tag anywhere in it**. So
`Frosty <grey>Glacegg</grey>` came out entirely grey, and a nicknamed egg's own name looked dimmer
than its trait. The panel was right; the picture was not, and I nearly went and brightened a
correct heading.

Fixing it took three tries, each of which broke the line the previous one fixed. The rule that
holds: a line is mixed if it **starts in the panel's own ink and changes partway through**, or if
it **carries more than one colour**. Either condition alone gets one of the two lines wrong.

Then the same question, asked of the rest of the screen. The egg's stat row is
`<dim>ATK</dim> 70  <dim>DEF</dim> 74` — labels quiet, numbers bright — and it was drawn entirely
quiet, numbers included. And the record's lore panel stripped *every* tag and drew the lot in one
ink, so `MATCHUPS`, `BASE STATS` and `FOUND ON` came out as body text and **every element
abbreviation lost its colour** — on the one panel in the game that carries eight of them at once.

The most colour-rich screen in the game had been rendered flat since it was first drawn, and it
looked fine, because a flat panel looks like a panel.

The battle move cards were the same. Row zero is
`<element>Frost Crack</element> <orange>▲</orange>` — so the **effectiveness arrow**, the one mark
on that card a player is actually deciding on, was drawn in the move's element colour instead of
its own, and was indistinguishable from the name beside it. The rider lines under it — LEAVES
CHILLED, LEAVES SCORCHED — lost their condition's colour the same way.

Three renders, one fault, three different ways of not noticing: a heading that looked dim on
purpose, a stat row that looked uniformly quiet on purpose, and an arrow that looked like part of
its move's name.

`artcheck` reimplements the game's *drawing*, not its data. That reimplementation has produced
false alarms seven times: a font metric that overstated every label by two thirds, sector captions
placed by an older algorithm than the game's, a truncating text wrap that hid the overflow it
existed to reveal, captions drawn at fixed offsets where uGUI centres them, a row highlight on the
pause menu that the view does not have, drawn volume meters landing a full row high, and — the
worst of them — an ellipse fill that flipped y "to correct for" a writer that already flips, so
the entire cast rendered upside down. Shoulders above the head, crests hanging under the chin,
brows sitting on cheekbones. It was plausible enough that the first round of fixes went into the
marks rather than into the transform.

The lesson is the same one as transcription, one level down: if the render reimplements a
function the game already has, copy it to the letter or call it. `Cast.Ell` is now
`ProcArt.FillEllipse` line for line, and it says so.

## Every species has a voice

Twenty-eight creatures that look distinct — their own shell colour, their own pattern, their own
silhouette — and every one of them arrived on the same three-note encounter sting. The sting says
something is here and nothing about what. Exactly the fault the portraits had, one sense over.

`CryForm` is built the way `PortraitForm` is: the **element** decides the shape it makes — Molten
falls away, Aether climbs and hangs, Volt chatters, Stone says one low thing — and the creature's
own numbers decide where it sits. Bulk pulls the pitch down, speed pushes it up and quickens the
beat, so the sound agrees with the stats on the record page instead of being sprinkled over them.

| measure | value |
|---|---|
| closest pair of cries | Shadowhisk / Vesperling at 0.131 (floor 0.05) |
| average distance within an element | 0.854 |
| average distance across elements | 1.619 |
| heaviest / lightest voice | Obsidyolk 336Hz, Yolty 532Hz |

An element being **twice as tight internally as across** is the design working: a family that is
recognisable without its members being interchangeable.

### A hash reshuffles; a stat separates

The closest pair started at 0.061 — Craggle and Obsidyolk, both Stone, whose voice is a single
note. Their bulk and speed put them 0.9 semitones apart and nothing else was in play.

Two rounds of hash-derived variation were tried and **measured**:

| what | closest pair | within an element |
|---|---|---|
| stats only | 0.071 | 0.730 |
| + hash pitch jitter | 0.053 | — |
| + hash overtone | 0.066 | 0.778 |
| + both | 0.065 | 0.854 |
| **+ stat-derived sag** | **0.131** | **0.854** |

The jitter made it *worse*. The overtone made it worse than doing neither. Both bought real
variety across the roster — within-element distance rose 17% — but neither moved the pair that
was actually too close, because **a random offset reshuffles which pair is closest without making
any pair further apart**.

What worked was giving the one-note voices a second stat-derived dimension: how far the last note
sags as it dies, from bulk. A heavier egg sags further. Craggle and Obsidyolk stopped being the
closest pair at all, the new worst is 0.131 — 2.6× the floor — and the variety the hashes bought
was kept.

An earlier commit here claimed the overtone fixed that pair. It did not; it improved on a state
the jitter had made worse. The table is what actually happened.

And the check that every voice is spoken by somebody failed on `Plain` — which is a *move* type,
the neutral element `Flail` is written in, already excluded from species logic in five other
places. The content was right. It now asserts that Plain is deliberately unspoken, which says more
than skipping it would.

## What only listening finds

Every sound in the game is synthesised from pure `Mathf`, so the harness builds all of them and
writes them to `Tools/Verify/audio/` — twenty-five effects, four music loops, and `sfx-palette.wav`,
which is the whole palette end to end with a beat between each. The loops had been written out
since they were written; the effects were measured and never once played.

Measurement had been per-sound: audible, not clipping, decays, sits in the spread. Nothing
compared one sound to another. A **fingerprint** does now — twelve log-spaced band energies over
the first half second, plus the energy shape across eight slices, both normalised so the
comparison is of character rather than loudness.

It found `CatchSuccess` and `LevelUp` 0.319 apart, the closest pair in the set. Both were a C
major triangle arpeggio from C5 resolving onto C6, and **they play back to back** — you catch an
egg in a fight and then it levels. A player could not hear which had just happened. They are 1.9
apart now and opposite in motion: catching latches low and settles onto a held triad, levelling
lifts to G and leaves a fifth hanging.

The check that matters is not the global floor. It is the **adjacency table**: fifteen pairs a
player actually hears within a second or two of each other, held to a floor four times higher
than the rest. Planting the old `LevelUp` back in scores 0.113 — which passes the global floor and
fails the adjacency one. A flat threshold cannot tell `Liftoff` against `Inscription`, heard on
opposite sides of the game, from two sounds that land in the same second.

## The mechanism that exists on one surface

The most reliable thing to look for in this codebase has turned out to be a mechanism that was
built once, works well, and was never applied to its neighbour. Five findings so far:

| mechanism | had it | did not |
|---|---|---|
| describing the highlighted option | the move menu | the action menu above it |
| a cursor marked in amber with a caret | every list in the game | the navigation chart |
| a shape derived from a name | the eggs, the landmarks | the seventeen portraits |
| a bar that eases | the HP bar | the XP bar directly under it |
| an idle bob | the surface's wildlife | both eggs in every fight |
| a floating number | damage | salves, drains, recoil, burn ticks, Static jolts |

The last one has a check that generalises: **`Feedback`** scans `BattleMode.cs` for every
`.TakeDamage(` and `.Heal(` and asks whether a number is spawned within four lines of it. Six
health changes, six numbers. Deleting any one of them fails by file and line, so the next effect
somebody adds gets asked the same question without anybody remembering to ask it.

## The strings nobody re-reads

The prose pass reads every authored line in `UiCopy` — five hundred and eighty of them. It did not
read a single one of the game's **refusals**, because those were string literals at the call sites
that raised them. They are the strings most likely to be clumsy: written in a hurry, seen rarely,
never re-read.

Moving fourteen of them into `UiCopy` found two pairs saying one thing twice:

| situation | one place said | the other said |
|---|---|---|
| flying at a sealed sector | "That route is sealed." | "Your charts do not reach that far yet." |
| a save that would not write | "Could not write the save file." | "…Your progress is not being saved." |

The second wording wins in both cases, and for the same reason. A *sealed route* sounds like a
door somebody shut; *charts that do not reach* is a fact about you, and the fact about you is the
one that changes. And a failed write that does not say the run has stopped being written down has
left out the only part that matters.

`Feedback.Toasts` now scans for a quoted sentence passed straight to `Toast(` and fails by file and
line. Thirty-three toasts, none written where it is raised.

The battle log was the same gap, one level bigger: **thirty-three sentences built inline** from a
name and a number, and it is the second most-read text in the game after the dialogue box. A
player who fights two hundred times reads "Pebbles used Shell Bash!" far more often than any
single line of story. They live in `BattleLog` now and the prose pass reads all of them — the
authored-string count went from 581 to 612.

Running them through it immediately found two things about the rules themselves. Five lines use
`"!  ("` — a deliberate double space setting a count off from its sentence, the same idiom as
`"  ·  "` everywhere else — and the no-double-space rule had simply never met it. The rule learned
the idiom once rather than five lines being exempted one at a time, because five exemptions would
also excuse a real accident on those lines.

And planting a space before an exclamation mark went straight through: there was no rule against
it. `"Go, Pebbles !"` is the kind of slip that survives every reading, because the eye supplies
what it expects.

### The general form

Toasts and prompts each got their own pass; `Feedback.StraySentences` is the general one. A
capitalised sentence ending in punctuation, in any file that is not a place authored text lives,
is a line the prose pass will never read. **8,370 lines scanned, none found.**

Every stray it turned up was worth moving for a second reason as well as the first:

- the swap panel's heading asked "SEND OUT WHICH EGG?" while the message box under it asked "Send
  out which egg?" — **the same question twice, on screen at once**. The box now says what swapping
  costs, which is the thing a player needs at that moment.
- "Every egg here is already in your record" was a fourth copy of a sentence the chart, the
  approach prompt and the landing toast already share.

620 authored strings before the sweep, 635 after.

## Which assertions never ran

"Did anything fail" and "did everything get asked" are different questions, and a suite this size
only ever answered the first. A check sitting inside a guard that is never true passes forever
and proves nothing, and there is no way to spot one by reading.

`SelfCheck.Check` is a delegate with `[CallerLineNumber]`, so every assertion records the line it
was written on. `Coverage` reads `SelfCheck.cs`, finds every line that writes one, and reports the
difference. 547 sites; 545 run.

The two that do not are **declared tripwires** — an assertion meant never to fire, marked
`// tripwire:` in the source with the reason. One is the story walk failing to advance. The other
scans dialogue for a claim that every egg on the speaker's own world is one element; nobody
currently writes such a line, so the corpus cannot exercise it. The corpus cannot be made to
without writing a bad line on purpose, so **the detector is what gets tested** — including
against Moth's "every egg carries a knack from its element", which is true, is about eggs
everywhere rather than about Umbralux, and which an earlier version of the filter failed.

Undeclared dead checks fail the suite. Guarding one assertion away is caught; guarding a block
away is caught as seven. There is also a ceiling on how much of the suite may be tripwires, since
a suite that is mostly tripwires checks nothing.

## One fact said in more than one place

The most reliable finding after "a mechanism built once and never applied to its neighbour" has
been a fact the game states twice and holds together nowhere.

| the fact | said in | what went wrong |
|---|---|---|
| Vesper's pad after Amy | the ending card, the surface, the chart | the card said it stays dark; the world warmed it |
| what "comfortable" means | the chart, the descent prompt | two constants that happened to agree |
| what "strong" means | thirteen places | thirteen bare `1.2f` and `0.8f` |
| what an action does | the menu's prose, the formula | nothing tied the sentence to the maths |

That last row is a shape worth naming on its own: **prose that describes a formula**. The action
menu says "Wear the egg down first — a healthy one kicks straight back out" and "A faster egg gets
away more often". Both are true. Neither was checked, and both would have gone quietly false the
moment `CatchChance` or `FleeChance` was retuned — on the screen where the game teaches its own
rules, so a line that goes stale there teaches them wrong.

The checks assert the *claim*, not the formula: a worn egg is easier to catch **and by more than a
rounding difference**, because "kicks straight back out" is a strong thing to promise; a faster egg
gets away more often; an Elder is harder to keep. Flattening any one of the three factors fails.

The eight trait blurbs were the same fault eight times over. Six of them quote a magnitude — "30%
harder", "15% faster", "an eighth of the damage back", "a quarter less" — and every one was typed
*beside* its constant rather than *from* it, on the page a player reads to decide what to raise.
They are generated now, so retuning `OverheatBoost` to 1.5 makes the record page say "50% harder"
by itself. The check still pins the current wording, which is the point: a retune should be a
decision, not a silent consequence.

Three move effects stated their share **three times** — in the effect's name (`Lifesteal50`,
`Recoil25`, `Heal50`), in the sentence a player reads while choosing, and in the division the
fight does. All three now read one constant, through `Words.Fraction`, which turns 0.125 into "an
eighth" so a sentence can be built from a number rather than typed beside it.

Checking that turned up something else: **every shipped move with one of those effects has an
authored blurb**, which takes precedence — "Reckless full-force hit" beats "Costs the user a
quarter of the damage dealt". So the generated sentences are a fallback the roster cannot
exercise, the same shape as the overclaim detector nobody's dialogue trips. The generator is
tested directly, on a `MoveDef` built for the purpose.

The last one is the sharpest. `1.2f` and `0.8f` appeared as literals across five concerns: Tough
Shell's trigger, the screen shake, a damage number's colour and size, the move card's arrow, the
message box's wording, the swap menu's warning, and the record page's three matchup lists. Every
one meant the same thing. An arrow could have promised a strong hit that Tough Shell then declined
to treat as one — on the screen whose whole job is telling a player what their move will do.

Two checks hold it now. The self-check asks, for all 81 element pairs, whether the battle line,
the card's arrow, the record's three lists and Tough Shell's decision all agree. A source pass
asks whether anybody has written `1.2f` or `0.8f` at a call site again.

**Both plants had to be chosen carefully.** Changing Tough Shell's threshold from `1.2` to `1.4`
is a no-op — the chart's multipliers are 2.0, 1.0 and 0.5, so nothing falls between them. A plant
that does not change an outcome proves nothing about the check, only about the plant.

## Checks that pass and prove nothing

### The code agreeing with itself

`LandmarkPosition` pushes a landmark to the far side of the world when it lands within
`LandmarkClearance` of a cache. The check asserted that the resulting gap exceeded
`LandmarkClearance` — against the function that produces that gap by enforcing that constant.
Setting the clearance to `0` made `gap > 0` trivially true and the whole thing passed. It read
like real work for several revisions.

Replacing it with the physical fact — `gap > CacheRange + LandmarkRange`, the distance at which
both "press E" prompts can offer themselves from the same patch of ground — was *also* wrong on
its own. Deleting the push leaves Mosswell 5.0u apart, which clears the 4.2u the prompts reach
and is nowhere near the margin the rule exists to give. That plant escaped too.

Three checks, none circular together:

| Check | Catches |
|---|---|
| `LandmarkClearance > CacheRange + LandmarkRange` | the rule being too small to matter |
| `gap >= LandmarkClearance`, per world | the placement not honouring its own rule |
| some world's raw placement violates the clearance | the push becoming decoration nobody tests |

The third exists because the branch fires exactly once across seventeen worlds — Mosswell, 5.0u
to 44.0u. A rule no roster reaches is a rule nobody has tested, and a reseed could make that true
silently.


Three faults planted at the HP counter all escaped, and only one of them was a no-op:

- **Twenty samples.** The slide runs about twenty frames, so twenty samples felt like the honest
  number — and it is, for what a player sees. It is not enough to prove a claim about the whole
  slide, and the claim is what gets written down. A wobble injected into the lerp passed. Two
  hundred samples catches it.
- **Test data that made a branch unreachable.** Every pair landed on a whole fraction, where
  `to * maxHp` is exact in float, so the `k >= 1` guard could be deleted with nothing noticing.
  The rise cases end on thirds and sevenths now.
- **A property named instead of stated.** Swapping `Ceil` for `Floor` left the number one low all
  the way down a fall — not zero, not backwards, so nothing above it fired. What was meant is
  *a falling count never reads under the bar*, and that is now what is asserted.

Related: test data invented out of the air fails the same way a threshold invented out of the air
does. The first version of this block fed in `maxHp 1` with a target of half a bar and duly
failed — on a state no egg in the game can be in. Fractions are built from whole hit points now.

## The naming prompt

The last screen nobody had drawn, and a text field — which is where the small failures live.
Three, none visible from reading the source:

- **The cap swallowed keystrokes in silence.** A player typing a thirteenth letter saw the field
  simply stop taking them, with nothing on screen having ever mentioned twelve. The hint carries
  the count now, and says so when a keystroke is refused.
- **The caret swapped a bar for a space when it blinked**, and a bar and a space are not the same
  width. The whole name shifted sideways twice a second, for as long as the player was reading
  what they had typed. It keeps its slot and changes colour instead.
- **Everything typed went into rich text unfiltered.** A nickname is interpolated into the party
  strip, the battle log, every toast and the ending card. `<b>` bolds the rest of the line it
  lands in; `</color>` ends the colour it was wrapped in and repaints whatever follows. `<3` is a
  name somebody types on their first run.

The filter is at entry rather than at display, so the stored string is clean — escaping at each of
the dozen places a name is drawn is a rule that gets forgotten at the thirteenth. **Loading is the
other door**, and it was standing open: a save file is a text file, and one written before this
change carries markup straight back in. `EggInstance.Restore` goes through the same filter.

## What only looking finds

Rendering has now found, in things that had passed every assertion:

| Screen | What eleven thousand assertions could not say |
|---|---|
| Landmarks | a bell, a ship's bow and nine hundred cairns all drew as the same grey rock |
| Pause menu | every row laid out from its own width; the caret slid sideways down the list |
| Portraits | `ProcArt.Portrait` took a name, used it to key the cache, and drew one face for everybody |
| Dialogue box | 300px tall over a two-row worst case — every line in the game floated above 110px of nothing |
| Surface HUD | "Take your three eggs back to Ori." with "Still needed: your three eggs, back to Ori" directly under it |

The surface HUD was the last screen nobody had drawn — the party strip, the objective panel, the
supplies, the prompt. It is what is on screen for the whole of every walk between fights, and it
took until every menu in the game had been rendered twice to get to it.

Drawing the dialogue box also turned up something no render could see: the fit check walked
`StoryDatabase.Npcs`, and **Amy is not an NPC**. The boss's dialogue — the climax of the game —
had never been measured against the box it is delivered in. It fits, but that was luck. There is
now one `StoryDatabase.EveryLine()` that everything walks, and a check that it reaches her.

### A blind spot, stated

`ProcArt.Portrait` cannot be called outside the engine — it allocates a `Texture2D`. So planting
`PortraitForm.For(key)` → `PortraitForm.For("Ori")` back into it is **not caught**, and cannot be
by anything that runs here. That is the original bug exactly, one level down.

What was done instead of faking a check: `PortraitForm.Shapes` is now the only source of geometry
either side has. `ProcArt.Portrait` is a five-line loop over it with no shapes of its own, and
`Cast.Portrait` is the same loop. The fault is still possible, but it is confined to one
identifiable line rather than spread across sixteen fill calls, and the self-check proves the
shapes it is handed do differ per name.

Verify that line by eye when the game next runs in the Editor. It is on the list.

### The sharpest example

The portrait one. The function had a `key` parameter, used it, and used
it only for the cache — so seventeen residents shared a silhouette and were told apart by tint
alone, on a roster that runs five shades of purple deep. Nothing measurable was wrong. Two of
them side by side was all it took.

Anything the renders show should be confirmed against the game's own numbers before being treated
as a bug. Where a mock can drive off the real database instead of a copy, it now does.

## How much slack the layouts have

Every text-fit check in this suite rests on one number: an average glyph 0.52em wide. That number
has never been checked against the font Unity actually renders, and if it is optimistic then
panels that pass here overflow on screen.

So the model is stressed rather than trusted. Widening it and re-running says how much room the
layouts actually have:

| glyph width | fit failures |
|---|---|
| 0.52em (the model) | 0 |
| 0.54em (+4%) | 0 |
| 0.56em (+8%) | 3 |
| 0.58em (+12%) | 26 |
| 0.60em (+15%) | 57 |

The first run of this was far worse: 38 failures at +4%, every one of them the surface HUD's party
strip, which sat inside four percent of wrapping on every row it had. That panel is over empty
screen; the width was free and it now has it. Two more — the collection footer and the battle
card's name — were widened for the same reason.

The other half of the model is line height, assumed at 1.16x the font size. Stressed the same
way:

| line height | failures |
|---|---|
| 1.16x (the model) | 0 |
| 1.20x (+3%) | 0 |
| 1.25x (+8%) | 22 |

Left alone deliberately. Unity's built-in font sits around 1.15-1.2x, so 1.25 is outside the range
the model could plausibly be wrong by — and the 22 are almost all one panel, the record's detail
box, which has a line of slack rather than none. The font-width case was worth fixing because it
failed at +4%, which is an error the model could actually make.

Re-run both after adding anything to a fixed panel:

```bash
# temporarily change 0.52f in SelfCheck's `lines` helper, run, change it back
```

## Nothing here has run in Unity

Every check in this directory is against Unity's compiler and this harness. Nothing has been
rendered or executed by the actual engine. Real font metrics, `RectMask2D` clipping, sprite
generation at scale and the audio as heard are all unverified — the harness models them, which is
not the same thing.
