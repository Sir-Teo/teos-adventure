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

They are written down twice: on the title screen in prose, and in the **pause menu** as a compact
grid under the rule. The title screen is read once and never seen again, so the pause menu is
where a player who has forgotten which key opens the chart can actually look.

The self-check requires both lists to mention the same keys — not to read the same, which they
should not. It caught the `N` key on its first run: naming a caught egg was taught only in the
catch prompt itself and had never made it onto the title screen.

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

Resting at a Nest Station says what it actually did. Walk in with a fainted egg and it tells you
how many are back on their feet; walk in whole and fully stocked and it says so rather than
claiming to have mended anything — *"Nothing needed doing. The pad is warm anyway."*, and three
other lines it rotates through, because a player standing on the pad pressing E is usually just
fond of the place.

It used to say one line forever, and follow it with a second toast reading "Progress saved."
Resting now saves quietly in the corner like every other autosave, so the message you actually
wanted to read is the only one on screen.

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
- **39 moves** with a type chart, STAB, crits, PP, stat stages and lingering conditions — every element has a finisher
- **8 shell patterns** (speckled, mottled, banded, striped, swirled, starry, cracked, glossy)
- **8 passive traits**, one per element — learn it once and it holds for every egg of that type
- **2 supplies**: 12 egg cartons and 4 yolk salves, both restocked at any Nest Station
- **4 music loops** and 21 sound effects, all synthesised

The music follows where you are: a pentatonic drift for the Reach and the Drift, a driving
riff for battles, a swelling chord for Amaranth Prime, and — for the Shattered Belt — **one
sustained note** with its own overtones drifting in and out, because that is exactly what Pim
says the Belt is doing. Crossing into Sector III is audible before it is visible.

Transitions are paced by what they are, not by one number. A battle arrives in **0.45s** — an egg
has just jumped you and music catching up half a second later undercuts it. Coming out of one
settles over **1.8s**. Amaranth takes **3s**, because it is meant to be felt on the way down.
Everything else crossfades in 1.1s.

### Lingering conditions

Every other move effect in the game resolves the instant it lands. These are the ones you are
still paying for three turns later:

| Condition | Dealt by | Effect |
|---|---|---|
| **Scorched** | Molten — *Lava Yolk* | burns a sixteenth of its bulk at the end of each round |
| **Chilled** | Frost — *Frost Crack* | moves at half speed |
| **Dazed** | Volt — *Volt Crack* | loses the turn outright one time in four |

The move button says so on its own line — *LEAVES SCORCHED* in the condition's colour — rather
than leaving the player to infer it from flavour text. It sits under the stat line because
beside the name it made "Frost Crack  95%  CHILL" 287px wide in a 286px button, and on the end
of the stat line it made 34 characters where 32 fit. Abbreviating it to BURN would have fitted
and would have contradicted the SCORCHED shown on the card.

One at a time, three rounds each, and **an egg cannot catch the condition its own element deals
out** — Molten never burns, Frost never chills, Volt is never dazed. They clear when the fight
ends, so nothing goes home with one and nothing touches the save file.

They were permanent at first, which did not add tactics so much as decide fights: a boss match
runs about twenty rounds, and a burn at a sixteenth a round is 119% of the target's health over
that. A bare-minimum gate-legal nest went from beating Amy 61% of the time to 84%.

Three rounds keeps them worth landing and worth landing again. Amy's ace went up one level to
absorb the rest — the player has three ways to wear her down where she has one. Bumping all
three of her eggs was far too coarse a knob: it took the fight from 82% to 42% in a single
level. She now sits at **54%** against the worst legal nest, and salves matter much more than
they did (+16 points, up from +5) because a salve is how you outlast a burn.

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

## Buried caches

Four of the seventeen worlds hide a supply cache — **Mosswell**, **Tidewrack**, **Arcmoor** and
**Nullreach**, one per sector plus one off the usual route. Each sits between two-thirds and
nine-tenths of the way out from the Nest Station, in the same place every time you land, drawn
faintly enough that you find it by walking rather than by glancing.

Digging one up raises how many cartons you can carry, permanently: twelve to sixteen over a
whole run. Capacity rather than a refill, because a refill is worth nothing standing next to the
station that gives you one free. It eases the trip back without touching the per-throw odds the
catch rates are tuned around.

They are the only reason to walk a planet out to its horizon instead of going shell field to
shell field, and they are recorded in the save, so a world gives up its cache once.

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

**Left/Right pick a column, Up/Down move inside it, Enter swaps a nest egg into the party.**

The right-hand panel follows the cursor. On the field record it shows the species entry; on one
of your own eggs it shows *that egg* — what it has **ahead** of it, condition, experience to the
next level, **ATK/DEF/SPD**, and its four moves with power and remaining PP. Levelling up has
always announced "ATK +2" and there was nowhere in the game to go and see what ATK was.

The **AHEAD** line answers the question you are actually asking when you look at an egg: is this
one worth raising. It names the grown form only once you have recorded it yourself — otherwise
it says *"Something else at level 26"*, which tells you there is more coming without handing over
what.

Until that existed an egg that went to the nest stayed there. Your party was whichever six you
happened to catch first, for the entire run, while fifty more sat at home unusable — in a game
whose whole subject is collecting them. Enter trades the highlighted nest egg for whichever egg
is leading, or drops it straight into an empty slot if the party is not full; combined with
**1-6**, which promotes any party egg to the front, that reaches every arrangement without a
second cursor or a mode to get stuck in.

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

### How long is it

The story suite proves the beats can be walked, but it *forces* each gate — when a beat wants
level 22 it awards experience until the number appears. A separate pacing pass plays instead:
real catch odds, experience shared the way the battle screen shares it, and a party that only
fights worlds within about three levels of itself, because a level-7 team does not clear the
Belt.

Played that way, the mandatory path is **54 encounters** — roughly half an hour of actual
battling, on top of the travel, talking and exploring. The gate lands almost exactly where it
was aimed: a played run arrives at Amaranth with six eggs, six elements and a top level of 22
against a requirement of six, four and 22.

The record is the long tail, not the critical path. Twenty-four catchable species is the thing
that keeps you flying after the story runs out.

A first version of this asserted the run should take at least 60 encounters and failed at 54.
The threshold was what was wrong — it was invented before there was a measurement to base it
on. The band is now wide and exists to catch drift: a gate that goes trivial, or a curve that
turns into a grind.

## Saving

One slot at `Application.persistentDataPath/eggverse_save.json`. Autosaves on landing, story
beats, battle end and dialogue; explicit save when you rest at a Nest Station.

A save can name content the game no longer has — three worlds were added this session, and
renaming or dropping any id would leave exactly that file on a returning player's disk.
`SpeciesDatabase.Get` and `PlanetDatabase.Get` both fall back to their first entry, which is
right for a lookup and wrong for a save: it would quietly turn an Elder Glacegg into a
level-30 Sprouteg, and land you on Yolkhaven while the file still said otherwise.

So loading drops what it cannot resolve — eggs, record entries and visited worlds alike — and
falls back to home for an unknown current world rather than storing it and carrying it forward.
`SaveSystem.Restore` is split from `Load` and kept free of Unity logging so it can be run
headlessly against a deliberately hostile file: fake species, blank ids, a world called
Atlantis, 99 cartons and -4 salves. What survives is what should.

The game writes five times a run on its own — starting, landing, lifting off, story beats, the
finale — and used to do all of it in silence. Only the manual **Save now** in the pause menu said
anything, which leaves a player guessing whether the last twenty minutes are safe.

Autosaves now leave a quiet `· saved` in the bottom-right corner for a moment and fade. A toast
for each would be noise; a corner mark is enough to say the run is written down without
interrupting whatever earned it.

A *failed* autosave is the one case worth interrupting for. That now raises a full toast — a
player who keeps going believing their progress is being recorded is the worst outcome the save
system can produce.

## Accessibility

The palette work below assumes nothing is carried by hue alone. Alongside it, the pause menu's
**Settings** page has **Screen motion**, which answers for everything that moves the display: the battle scene shaking
on a hit, the per-egg shake on damage, and the full-screen flash on landing and mode changes.

Turned off, the shakes do not happen at all and the flash still does — it is what hides the scene
swapping underneath — but at a third the strength and half the duration, so it reads as a soft
wipe rather than the screen going white. It is saved with the run, and a file written before the
option existed loads with motion **on**, so nobody's feedback quietly disappears.

**Text speed** answers for both the dialogue reveal and how long a battle line sits on screen,
because they are the same preference:

| | reveal | a battle line holds |
|---|---|---|
| relaxed | 33 ch/s | 1.67s |
| normal | 55 ch/s | 1.00s |
| brisk | 99 ch/s | 0.56s |
| instant | appears | no wait |

There are 900 lines of dialogue and the battle talks constantly. Somebody who has read *"It's
super effective!"* two hundred times should not be made to read it again, and somebody who has
not should not be hurried.

## Colour and readability

Every character's portrait tint comes from the cast list itself, not from a second table beside
it. It used to be a hand-written map of the same fourteen names, and it drifted the moment three
residents were added — **Lune, Tilda and Garrow spoke in generic grey for several revisions**, and
the check meant to catch that asked whether the tint was `!= default`. The fallback returns a real
grey, so it passed. It now asks whether the speaker has a tint *of their own*.


The nine type colours carry real information, so they are measured rather than eyeballed. The
harness converts each to CIE Lab and computes pairwise ΔE under normal vision plus approximated
protanopia and deuteranopia. That found **Verdant and Stone colliding under deuteranopia** (ΔE
10.3, the commonest form of colour blindness); Stone was darkened to `#887058`, which keeps it
reading as rock and takes the pair to **ΔE 20.4**. Every type is also checked for contrast
against the panel background.

Colour is never the only signal: every type carries a unique three-letter tag (`VRD`, `STN`,
`FRS`…) shown alongside the colour in the party strip, and type names appear in full on battle
cards and in the field record.

### One rule, applied everywhere

Anything drawn on a planet's surface is held to a minimum luminance gap against that planet's
ground, because a fixed colour reads on some worlds and vanishes on others. This has now caught
five separate things:

| What | Where it vanished | Gap before | After |
|---|---|---|---|
| Shell fields | Glacierim's ice | 0.035 | 0.284 |
| Buried caches | Nullreach | 0.069 | 0.353 |
| Tidal pools | Brineholt | 0.077 | 0.188 |
| **Teo** | Glacierim's ice | **0.064** | 0.784 |
| Wild eggs | Shadowhisk on Nullreach | 0.130 | 0.477 |
| Wild eggs *on shell fields* | Yolkano on Cinderoost | 0.060 | 0.213 |
| **World labels** | Glacierim's ice | **0.082** | 0.819 |
| Dark eggs | Gloomolk on the battle backdrop | 0.128 | 0.525 |

Teo was the worst of them — a white suit on a white world — and the fix is a dark outline rather
than a per-planet tint, so the protagonist stays one colour and the outline only does work where
it is needed. Wild eggs sit on a backing disc coloured against the ground, since species colour
comes from the element and ground colour from the planet: an egg living on a world of its own
element collides by construction.

The self-check measures every one of these against every world that has it — and against the
**shell fields** as well as the bare ground, which is where most of them are actually standing.
Checking only against the ground was its own version of the mistake: correct about a background
that is not the one the player is looking at.

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

### Prose

A separate pass reads every string the game can put on screen — 291 of them — out of the
databases rather than out of the source. The first attempt grepped the `.cs` files and matched
across string boundaries, reporting code as dialogue; the data cannot misreport what it holds.

It walks the story one flag at a time rather than jumping from "nothing set" to "everything
set", because several conversations only exist in between: Ori's briefing, the one place the
Keepers are named, needs `met_ori` set and `ori_briefed` not.

It checks doubled words, double spaces, stray whitespace, space before punctuation, and that
apostrophes and ellipses each pick one convention and keep it (currently 90 straight, 0 curly;
10 ASCII, 0 unicode). It also checks the invented nouns — Nest Station, Prime Egg, Elder,
Keeper, the sector names — are capitalised the same way everywhere, skipping sentence-initial
uses so ordinary grammar is not mistaken for drift.

#### Numbers the writing says out loud

A line like *"it burns for three rounds"* is true right up until somebody changes the constant it
was written beside, and nothing complains. So:

- Where the number can be spelled from the constant, it is. The move descriptions build their own
  duration, and the story objectives spell their own gates — *"Bring six eggs, four types, and one
  raised to 22"* is generated from the requirements that beat actually checks, because an objective
  that misstates its own gate is the worst line in the game to get wrong.
- Where the writing is better with the word in it — Nell's *"Twelve is all you get between rests"*
  reads better than *"12 is all you get"* — the constant is held to the line instead, and the
  failure message names the speaker and the world so the fix is obvious.

Four characters state a number the code owns: Nell (carton stack), Lune (salve stack), Garrow
(Elder level bonus) and Amy (party size). Change any of those constants and the build tells you
whose line to rewrite.

## What is still not verified

**Actual rendering.** Text overflow inside correctly-sized panels, font legibility, sprite draw
order on screen, input feel, frame timing. Layout arithmetic is not the same as looking at it,
and none of this has drawn a frame yet.

## Not built yet

A proper options screen, controller support, egg trading or breeding, side quests beyond the
main story.
