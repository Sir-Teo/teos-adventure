# Eggverse — Teo and the Long Way to Amy

A monster-collector where all the monsters are eggs. Press **Play** on any scene; the game builds
itself at runtime (`GameBootstrap` runs on `RuntimeInitializeOnLoadMethod`), so there is no scene
setup, no prefabs, and **no art or audio assets** — every sprite and every sound is generated in code.

## Controls

| Where | Keys |
|---|---|
| Space (star map) | `WASD` / arrows to fly · `E` to land |
| Planet surface | `WASD` to walk · `E` to talk or rest · `Q` to lift off |
| Battle | arrows to choose · `Enter`/`Space` to confirm · `Esc` back · `1-6` quick select |
| Move menu | the message box describes the highlighted move and whether it is strong here |
| Collection | arrows browse the field record · `1-6` promotes that egg to lead |
| Anywhere | `M` navigation chart · `Tab` collection · `Esc` pause · `0` mute |
| Title | `Space` continue or begin · `N` new run |

`Esc` opens a pause menu with sound settings, an explicit save, and a way back to the title.

## The story

Every planet is a shard of one broken egg. The Prime Egg's core is still out there, still whole —
and eleven years ago it started to split. Amy has been draining the warmth from three sectors to
hold it shut, which is why the Nest Stations are going cold. Ten beats across four chapters, with
a main cast of six: **Ori** (mentor), **Marn** and **Sable** (Keepers), **Pim** (hears the Belt),
**Vess** (rival, fought twice) and **Amy**.

Seven more residents live on the worlds the main story does not stop at — Hob, Nell, Bram, Sax,
Quill, Moth and Wren. They are local colour, and each quietly teaches something the game never
states outright: type advantage, weakening before you throw, evolution, swapping mid-fight, the
chart, how traits work, and one piece of Belt lore. **All 13 landable worlds have someone on them.**

Sectors unlock through story progress, not level checks. The final gate wants six eggs, four
distinct types, and one raised to level 22 — a threshold set by simulation, not by guessing
(see *Balance* below).

## The ending

Beating Amy is not the last thing that happens. Ori feels the pad go warm; **Marn**'s eleven-year
line finally turns and climbs; **Sable** refuses to let you mistake relief for a fix ("a thing that
size is never fixed, it is *carried*"); **Pim** hears the Belt's one flat note come up by a hair;
and **Vess** — who wanted to break the Prime rather than hold it — concedes she only ever counted
to two, and asks whether Amy is all right.

All sixteen react, not just the keepers. Hob's ash is warm again, not hot — "forty years, I know
the difference". Nell's eggs kick when she throws at them. Wren's instruments read a temperature
for the first time in eleven years. Garrow has stacked you a cairn, a small one, "you are not dead,
it would be rude to make it big".

The self-check enforces this: **every** character in the cast must say something different once
`beat_amy` is set. All of them spoke their mid-game lines over the credits until something checked.

A second check walks the dialogue tree to a fixed point and requires every beat's prerequisite
flag to be inside the set of flags something can actually grant. A gate nothing opens is a
soft-lock, and a one-character typo in a flag name is all it takes; the check names the beat.

## The loop

Land → walk into a pale **shell field** or bump a roaming egg → battle → weaken it → throw an
**egg carton**. Press `N` when you catch one to give it a nickname. Rest at a Nest Station to
heal, restock and save. Eggs evolve at level thresholds.

Battle actions are **FIGHT · CARTON · SALVE · SWAP · RUN** in a 2×3 grid. A **yolk salve**
mends 55% of the active egg's bulk and costs the turn; you carry four and Nest Stations
restock them alongside cartons. They exist because a wild fight is not free — at the level
each planet is tuned for, one costs the lead egg 44–68% of its health and roughly a quarter
to a third of encounters leave something fainted. A salve buys you the next fight without
the walk home. Against Amy they lift a bare-minimum gate-legal nest from 61% to 72%, which
is help, not a pass.

About one wild egg in twelve is an **Elder** — three levels older, visibly larger with an aura
of its own element, worth 1.7× the experience, and far harder to keep (14% at full health
against an ordinary egg's 33%, and 40% even at 1 HP). They are trophies, not obstacles.

## The worlds

Three sectors ring Yolkhaven, and Amaranth Prime sits beyond all of them.

| Sector | Worlds | Levels |
|---|---|---|
| The Hatchery Reach | Yolkhaven, Cinderoost, Brineholt, Mosswell | 5-11 |
| The Long Drift | Shimmerfen, Tidewrack, Voltacrest, Arcmoor, Emberfall, Cobblestead, Glacierim | 10-18 |
| The Shattered Belt | Umbralux, Aetherwake, Nullreach, Vesper, Cairnhold | 16-23 |
| Amaranth | Amaranth Prime | boss |

Every element has at least two worlds of its own. **Shimmerfen** (Aether), **Arcmoor** (Volt)
and **Cairnhold** (Stone) exist because those three were served by a single planet each, which
made them the elements you were least likely to have raised by the time Amy is reachable.

Each of the sixteen landable worlds has a resident, and each resident quietly teaches one
thing the game never states outright — Nell that you weaken an egg before throwing, Moth that
elements carry passive traits, Lune that a salve is only worth it mid-fight, Tilda that stat
stages last the whole battle, Garrow that Elders resist the carton twice as hard.

## Content

- **28 species** across 9 types, in **8 three-stage evolution families** (one per element)
- **17 planets** in three sectors plus Amaranth Prime
- **35 moves** with a type chart, STAB, crits, PP, stat stages and status effects
- **8 shell patterns** (speckled, mottled, banded, striped, swirled, starry, cracked, glossy)
- **8 passive traits**, one per element — learn it once and it holds for every egg of that type
- **2 supplies**: 12 egg cartons and 4 yolk salves, both restocked at any Nest Station
- **4 music loops** and 21 sound effects, all synthesised

The music follows where you are: a pentatonic drift for the Reach and the Drift, a driving
riff for battles, a swelling chord for Amaranth Prime, and — for the Shattered Belt — **one
sustained note** with its own overtones drifting in and out, because that is exactly what Pim
says the Belt is doing. Crossing into Sector III is audible before it is visible.

### The type ring

Each element beats the **next two** in this order, and is beaten by the previous two:

```
Molten → Frost → Verdant → Stone → Void → Volt → Aether → Tidal → (Molten)
```

| Element | Crushes | Beaten by |
|---|---|---|
| Molten | Frost, Verdant | Tidal, Aether |
| Frost | Verdant, Stone | Molten, Tidal |
| Verdant | Stone, Void | Frost, Molten |
| Stone | Void, Volt | Verdant, Frost |
| Void | Volt, Aether | Stone, Verdant |
| Volt | Aether, Tidal | Void, Stone |
| Aether | Tidal, Molten | Volt, Void |
| Tidal | Molten, Frost | Aether, Volt |

Super-effective is ×1.85, resisted is ×0.55, and same-type attacks get ×1.4. The collection
screen shows each species' matchups in place, so the ring never has to be memorised.

### Traits

| Element | Passive | Effect |
|---|---|---|
| Molten | Overheat | Hits 30% harder below a third health |
| Tidal | Featherlight | 15% faster than its bulk suggests |
| Verdant | Warm Yolk | Mends a little every round |
| Volt | Static | Attackers take an eighth of the damage back |
| Frost | Tough Shell | Takes a quarter less from super-effective hits |
| Stone | Sturdy | Survives one knockout from full health with 1 HP |
| Aether | Lucky | Crits far more often |
| Void | Hardhead | Its stats cannot be lowered |

## Layout

```
Data/     types + chart, moves, 28 species, 17 planets, runtime EggInstance
Core/     GameBootstrap (entry), GameDirector (modes/camera/story), GameState, SaveSystem, EggInput
Story/    StoryDatabase (beats, cast, dialogue, trainers), StoryState (flags + progression)
Art/      ProcArt — every sprite, from noise and ellipse maths
Audio/    ProcAudio (synthesis), AudioDirector (playback + cross-fade)
World/    TeoController, SpaceMode (star map + parallax), SurfaceMode (biomes, NPCs, encounters)
Battle/   BattleCalc (damage/catch/AI maths), BattleMode (coroutine turn loop)
UI/       UIKit, HudView, DialogueView, GalaxyMapView, TransitionView
```

## Tuning knobs

- Species, learnsets, catch rates, evolutions, patterns → `Data/SpeciesDatabase.cs`
- Planets, sectors, spawn tables, level ranges → `Data/PlanetDatabase.cs`
- Type effectiveness → `Data/EggType.cs`
- Damage / catch / flee / AI maths → `Battle/BattleCalc.cs`
- Story beats, dialogue, trainers → `Story/StoryDatabase.cs`
- Level curve and stat growth → `Data/EggInstance.cs`
- Sound design and music → `Audio/ProcAudio.cs`
- Carton and salve counts, salve strength → `Core/GameState.cs`, `Battle/BattleMode.cs`

## Filling the record

**24 of the 28 species are catchable** — Amy's trio and Vess's ace have catch rates low enough
to be decorative, so they can be seen but not kept, and the "caught" counter is out of 24 rather
than 28 so you are never left hunting something that does not exist.

Ori notices as your record grows: at **10** species, at **18**, and when you close it completely
he hands over the egg he has kept in the straw since the day he took the station — a **Bloomolk
at level 20**. He is the one who taught you to read a nest, so he is the one who reacts to it.

The star map tells you what lives on a world. The dex now tells you the other direction — pick
any species in the field record and its entry lists **which worlds it is found on**. Three
catchable eggs (Mossmallow, Wavelet, Bloomolk) live on exactly one world each, so without the
reverse lookup finishing the record meant flying to all seventeen and reading each in turn.

Only worlds you have already charted are named, so the record stays a reward for exploring
rather than a shopping list handed over at the start; anything left is counted as "and N worlds
you have not charted". The self-check requires every catchable species to spawn somewhere at
all — one that does not would make the record impossible to finish, and nothing else would say so.

## The collection screen

`Tab` opens three columns: your party and nest on the left, the 28-entry field record in the
middle, and a detail panel on the right for whichever entry the cursor is on — portrait, dex
number, type, passive, base-stat bars, evolution target, and the species' field note. Unseen
species show as a blacked-out silhouette; seen-but-uncaught show stats but withhold the note.

The nest column shows a window of twenty, not the whole nest — it is unbounded, and by the
end of a run holds fifty or more. Twenty is what the 780px column holds once the party and
headers are paid for; it used to show eight and leave over half the column empty. The field
record lists every species with no window at all, so the self-check bounds it: 30 lines of
the 35 that column can hold, which is the number that breaks first if the roster grows.

## Balance

All battle randomness routes through `EggRandom`, which can be given a seeded generator. That
makes the combat maths runnable outside the editor, so the curve is measured rather than guessed.
Current figures, from a few thousand simulated fights against the shipping formulas:

| Situation | Result |
|---|---|
| Wild encounters, paced mixed team | 96–100% wins, 3.3–5.5 turns |
| Solo lead into a bad type matchup | 32% wins (vs 100% with the right type) |
| Vess I / Vess II (mandatory story fights) | 82% / 98% |
| Amy, minimum gate-legal nest | 59% |
| Amy, well-raised nest | 100% |
| Reaching the gate (six-egg nest) | ~64 battles, nest within one level of itself |

Two findings worth knowing before you retune anything.

**Level is a very coarse difficulty knob.** A single level on a trainer's team swings the
outcome by 40–60 points, because long near-parity attrition fights are winner-take-all. Vess I
jumps 99.8% → 15.8% for one level across four eggs. The finer levers are **team size** (five
eggs at the lower level landed at 86%) and **base stats**, which is how Amy is tuned.

**The type chart is a ring, and that is load-bearing.** Each element beats the next two in the
order Molten → Frost → Verdant → Stone → Void → Volt → Aether → Tidal → (Molten). That structure
guarantees every element has exactly two targets and two counters, so strict dominance is
impossible — an earlier hand-written chart had Stone beating five other elements outright. If you
edit the chart, keep the ring or the dominance check in the harness will tell you what broke.

## Saving

One slot at `Application.persistentDataPath/eggverse_save.json`. Autosaves on landing, story
beats, battle end and dialogue; explicit save when you rest at a Nest Station.

## Colour and readability

The nine type colours carry real information, so they are measured rather than eyeballed. The
harness converts each to CIE Lab and computes pairwise ΔE under normal vision plus approximated
protanopia and deuteranopia. That found **Verdant and Stone colliding under deuteranopia** (ΔE
10.3, the commonest form of colour blindness); Stone was darkened to `#887058`, which keeps it
reading as rock and takes the pair to **ΔE 20.4**. Every type is also checked for contrast
against the panel background.

Colour is never the only signal: every type carries a unique three-letter tag (`VRD`, `STN`,
`FRS`…) shown alongside the colour in the party strip, and type names appear in full on battle
cards and in the field record.

## Self-check

**Eggverse ▸ Run Self-Check** (`Cmd/Ctrl+Shift+E`) runs **~1,670 assertions** over the game's
design data and reports in a dialog. It lives in `Verify/SelfCheck.cs`, needs no scene, and
covers:

- the story can be played to its end, and every required flag comes from someone reachable
  during that beat
- every NPC has something to say at every point in the story; every landable world is peopled
- the type chart holds its ring shape, and no element strictly dominates another
- every world's theme has a counter that is obtainable by the time you get there
- species data: unique dex numbers, real moves, a damaging and a same-type move at max level,
  evolutions that upgrade and look different, valid spawn tables, stable world seeds
- type colours stay distinguishable under deuteranopia, and every tag is unique
- no text overflows the box it is displayed in — **every** fixed-width text site is covered:
  dialogue, objectives, planet taglines, battle cards, move buttons, the party strip, the HUD
  counter line, party-swap rows and star-map labels, each tested at the worst case its data can
  produce (an elder with the longest species name, every stat stage showing, at level 30)

The same menu also offers **Delete Save File** and **Reveal Save File** for testing.

These are not hypothetical checks — between them they caught a story gate that gated nothing, a
type chart where Stone dominated five other elements, a Tidal world with no obtainable counter,
and two colours indistinguishable to the commonest form of colour blindness.

## What is still not verified

**Actual rendering.** Text overflow inside correctly-sized panels, font legibility, sprite draw
order on screen, input feel, frame timing. Layout arithmetic is not the same as looking at it,
and none of this has drawn a frame yet.

## Not built yet

A proper options screen, controller support, egg trading or breeding, side quests beyond the
main story.
