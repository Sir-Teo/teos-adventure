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

It distinguishes four outcomes, which is the whole point:

- `ABORT` — the pattern matched nothing. Nothing was tested; the result means nothing.
- `ABORT` — the planted code does not compile, so the suite never ran. A plant that fails to
  build produces no failures either, and reporting that as "still passes" is the same mistake
  this script exists to prevent. It happened while testing a coda check, and read exactly like a
  missing assertion.
- `NOT CAUGHT` — the fault landed and the suite still passed. The check is missing or toothless.
- `caught:` — with the failures listed, and a note if the expected one is among them.

It also rebuilds after restoring the file. Restoring the source is not enough: the suite builds
before it checks, so a planted run leaves the fault compiled into `Eggverse.dll`, and `artcheck`
links that DLL. A render will then show a defect that exists in no source file — which happened,
and looked exactly like a real layout bug in the game.

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
false alarms four times: a font metric that overstated every label by two thirds, sector captions
placed by an older algorithm than the game's, a truncating text wrap that hid the overflow it
existed to reveal, and captions drawn at fixed offsets where uGUI centres them.

Anything the renders show should be confirmed against the game's own numbers before being treated
as a bug. Where a mock can drive off the real database instead of a copy, it now does.

## Nothing here has run in Unity

Every check in this directory is against Unity's compiler and this harness. Nothing has been
rendered or executed by the actual engine. Real font metrics, `RectMask2D` clipping, sprite
generation at scale and the audio as heard are all unverified — the harness models them, which is
not the same thing.
