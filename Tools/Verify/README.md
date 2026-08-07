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

## What only looking finds

Rendering has now found, in things that had passed every assertion:

| Screen | What eleven thousand assertions could not say |
|---|---|
| Landmarks | a bell, a ship's bow and nine hundred cairns all drew as the same grey rock |
| Pause menu | every row laid out from its own width; the caret slid sideways down the list |
| Portraits | `ProcArt.Portrait` took a name, used it to key the cache, and drew one face for everybody |
| Dialogue box | 300px tall over a two-row worst case — every line in the game floated above 110px of nothing |

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
