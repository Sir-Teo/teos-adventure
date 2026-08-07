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

It renders the battle screen, the navigation chart, the collection screen, a planet surface and
the star map at the canvas reference resolution, using the real `PlanetDatabase` and
`SpeciesDatabase`. Several real layout problems were only ever visible this way — a chart wasting
44% of its width, a card carrying a third of its height as dead space, a collection column half
empty.

## A warning about the mocks

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
