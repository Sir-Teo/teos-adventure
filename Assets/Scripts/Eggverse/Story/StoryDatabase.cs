using System.Collections.Generic;
using UnityEngine;

namespace Eggverse
{
    public struct DialogueLine
    {
        public readonly string Speaker;
        public readonly string Text;
        public DialogueLine(string speaker, string text) { Speaker = speaker; Text = text; }
    }

    /// <summary>One conversation, plus whatever it changes about the world when it ends.</summary>
    public class DialogueScript
    {
        public readonly DialogueLine[] Lines;
        public readonly string SetsFlag;        // story flag raised when the script finishes
        public readonly string StartsTrainer;   // trainer id to fight immediately afterwards
        public readonly string GivesSpeciesId;  // an egg handed over, if any
        public readonly int GivesSpeciesLevel;
        public readonly bool HealsParty;        // mends eggs
        public readonly bool RestocksCartons;   // resupplies, which only Nest Stations normally do

        public DialogueScript(DialogueLine[] lines, string setsFlag = null, string startsTrainer = null,
                              string givesSpeciesId = null, int givesSpeciesLevel = 5, bool healsParty = false,
                              bool restocksCartons = false)
        {
            Lines = lines; SetsFlag = setsFlag; StartsTrainer = startsTrainer;
            GivesSpeciesId = givesSpeciesId; GivesSpeciesLevel = givesSpeciesLevel;
            HealsParty = healsParty; RestocksCartons = restocksCartons;
        }
    }

    public class TrainerDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string[] SpeciesIds;
        public readonly int[] Levels;
        public readonly string VictoryFlag;
        public readonly DialogueLine[] OnDefeat;

        public TrainerDef(string id, string name, string[] speciesIds, int[] levels, string victoryFlag, DialogueLine[] onDefeat)
        {
            Id = id; Name = name; SpeciesIds = speciesIds; Levels = levels;
            VictoryFlag = victoryFlag; OnDefeat = onDefeat;
        }
    }

    public class NpcDef
    {
        public readonly string Id;
        public readonly string Name;
        public readonly string PlanetId;
        public readonly Vector2 Position;
        public readonly Color Tint;
        public readonly bool IsKeeper;

        public NpcDef(string id, string name, string planetId, Vector2 position, Color tint, bool isKeeper = true)
        {
            Id = id; Name = name; PlanetId = planetId; Position = position; Tint = tint; IsKeeper = isKeeper;
        }
    }

    /// <summary>A step in the main story. Completes once every stated requirement is satisfied.</summary>
    public class StoryBeat
    {
        public readonly string Id;
        public readonly string Chapter;
        public readonly string Objective;
        public readonly Sector MaxSector;      // how far out the player may travel during this beat
        public readonly string[] RequiredFlags;
        public readonly int RequiredEggs;
        public readonly int RequiredTypes;
        public readonly int RequiredLevel;

        public StoryBeat(string id, string chapter, string objective, Sector maxSector,
                         string[] requiredFlags = null, int requiredEggs = 0, int requiredTypes = 0, int requiredLevel = 0)
        {
            Id = id; Chapter = chapter; Objective = objective; MaxSector = maxSector;
            RequiredFlags = requiredFlags ?? new string[0];
            RequiredEggs = requiredEggs; RequiredTypes = requiredTypes; RequiredLevel = requiredLevel;
        }
    }

    public static class StoryDatabase
    {
        // ------------------------------------------------------------------
        // beats
        // ------------------------------------------------------------------

        public static readonly StoryBeat[] Beats =
        {
            new StoryBeat("wake", "Chapter 1 · The Cold Nests",
                "Find Ori at the Yolkhaven Nest Station.",
                Sector.HatcheryReach, new[] { "met_ori" }),

            new StoryBeat("first_catch", "Chapter 1 · The Cold Nests",
                "Collect eggs until you are carrying three, so Ori can show you the cold-reading.",
                Sector.HatcheryReach, null, 3),

            new StoryBeat("report_ori", "Chapter 1 · The Cold Nests",
                "Take your three eggs back to Ori.",
                Sector.HatcheryReach, new[] { "ori_briefed" }),

            new StoryBeat("drift_keepers", "Chapter 2 · The Long Drift",
                "The Drift's nests are going cold too. Find Marn on Voltacrest and Sable on Glacierim.",
                Sector.LongDrift, new[] { "keeper_marn", "keeper_sable" }),

            new StoryBeat("vess_one", "Chapter 2 · The Long Drift",
                "A hatcher called Vess is stripping the Drift ahead of you. She is on Cobblestead.",
                Sector.LongDrift, new[] { "beat_vess_1" }),

            new StoryBeat("belt_open", "Chapter 3 · The Shattered Belt",
                "The cold runs north. Follow it into the Shattered Belt and find Pim on Aetherwake.",
                Sector.ShatteredBelt, new[] { "learned_truth" }),

            new StoryBeat("vess_two", "Chapter 3 · The Shattered Belt",
                "Vess has gone to Vesper to make her own run at Amaranth. Catch her first.",
                Sector.ShatteredBelt, new[] { "beat_vess_2" }),

            // Note the sector here is the Belt, not Amaranth: the route only opens once the
            // nest requirement below is met and the story moves on to "finale".
            new StoryBeat("amaranth", "Chapter 4 · Amaranth Prime",
                "Amaranth Prime will only open to a full nest. Bring six eggs, four types, and one raised to 22.",
                Sector.ShatteredBelt, null, 6, 4, 22),

            new StoryBeat("finale", "Chapter 4 · Amaranth Prime",
                "Amy is waiting on Amaranth Prime.",
                Sector.Amaranth, new[] { "beat_amy" }),

            new StoryBeat("epilogue", "Epilogue",
                "The Prime Egg is warm again. Go wherever you like.",
                Sector.Amaranth),
        };

        // ------------------------------------------------------------------
        // cast
        // ------------------------------------------------------------------

        static Color C(int rgb) => new Color32((byte)(rgb >> 16 & 0xFF), (byte)(rgb >> 8 & 0xFF), (byte)(rgb & 0xFF), 0xFF);

        public static readonly NpcDef[] Npcs =
        {
            new NpcDef("ori",   "Ori",   "yolkhaven",   new Vector2(6f, 5f),    C(0x9FD6A0)),
            new NpcDef("marn",  "Marn",  "voltacrest",  new Vector2(-7f, 6f),   C(0xFFD84A)),
            new NpcDef("sable", "Sable", "glacierim",   new Vector2(8f, -7f),   C(0xBEE8F4)),
            new NpcDef("vess1", "Vess",  "cobblestead", new Vector2(0f, 10f),   C(0x9A8CD6), false),
            new NpcDef("pim",   "Pim",   "aetherwake",  new Vector2(-6f, 8f),   C(0xD6B8FF)),
            new NpcDef("vess2", "Vess",  "vesper",      new Vector2(0f, 9f),    C(0x9A8CD6), false),

            // Residents of the worlds the main story does not stop on. Local colour, and
            // each one quietly teaches something the game never explains outright.
            new NpcDef("hob",   "Hob",   "cinderoost",  new Vector2(-8f, 4f),   C(0xE0885A)),
            new NpcDef("nell",  "Nell",  "brineholt",   new Vector2(7f, 6f),    C(0x7FC6E8)),
            new NpcDef("bram",  "Bram",  "mosswell",    new Vector2(-6f, -6f),  C(0x9CCB7E)),
            new NpcDef("sax",   "Sax",   "tidewrack",   new Vector2(8f, -5f),   C(0x6FA8C4)),
            new NpcDef("quill", "Quill", "emberfall",   new Vector2(-7f, 7f),   C(0xF2A86B)),
            new NpcDef("moth",  "Moth",  "umbralux",    new Vector2(6f, 8f),    C(0x9B8BD6)),
            new NpcDef("wren",  "Wren",  "nullreach",   new Vector2(-5f, -8f),  C(0x8C82C0)),
        };

        // ------------------------------------------------------------------
        // trainers
        // ------------------------------------------------------------------

        static readonly Dictionary<string, TrainerDef> trainers = new Dictionary<string, TrainerDef>();

        public static TrainerDef GetTrainer(string id)
        {
            TrainerDef t;
            return trainers.TryGetValue(id, out t) ? t : null;
        }

        static void AddTrainer(TrainerDef t) { trainers[t.Id] = t; }

        static StoryDatabase()
        {
            AddTrainer(new TrainerDef("vess_1", "Vess",
                new[] { "yolty", "tidepoach", "chillet", "cobblet", "duskle" },
                new[] { 14, 14, 14, 15, 16 },
                "beat_vess_1",
                new[]
                {
                    new DialogueLine("Vess", "Fine. Fine! You're not useless."),
                    new DialogueLine("Vess", "I'm not stripping the Drift for fun, you know. The nests out here have about a season left and nobody in the Reach has noticed."),
                    new DialogueLine("Vess", "Something north is drinking. I'm going to go and put a stop to it."),
                    new DialogueLine("Teo", "Then let me come."),
                    new DialogueLine("Vess", "...I'll think about it. Go north if you're so keen. It's cold and it hums."),
                }));

            AddTrainer(new TrainerDef("vess_2", "Vess",
                new[] { "frizzlebolt", "snowpoach", "gloomolk", "vesperling" },
                new[] { 17, 17, 18, 19 },
                "beat_vess_2",
                new[]
                {
                    new DialogueLine("Vess", "Stop. Stop, I yield."),
                    new DialogueLine("Vess", "I grew up on this rock, Teo. Two Nest Stations. Both cold by the time I was nine."),
                    new DialogueLine("Vess", "I thought if I got to Amaranth first and broke whatever's up there, it'd all come back."),
                    new DialogueLine("Teo", "Pim says it isn't a thief. It's someone holding something together."),
                    new DialogueLine("Vess", "Then she's doing a rotten job of it."),
                    new DialogueLine("Vess", "...Go. You've got the better nest. I'll keep the Belt warm till you're back."),
                }));

            AddTrainer(new TrainerDef("amy", "Amy",
                new[] { "solyolk", "obsidyolk", "reginova" },
                new[] { 19, 20, 22 },
                "beat_amy",
                new[]
                {
                    new DialogueLine("Amy", "Well. You actually did it."),
                    new DialogueLine("Amy", "Do you know what I'm standing on, Teo? Not a planet. An egg. The first one. All the others came off it when it cracked."),
                    new DialogueLine("Amy", "It has been cracking again for eleven years. I have been pulling warmth off every nest I could reach to keep it shut."),
                    new DialogueLine("Teo", "The Reach. The Drift. Vess's whole childhood."),
                    new DialogueLine("Amy", "Yes. I know exactly what it cost, and I would do it again, because the alternative is no eggs anywhere, ever."),
                    new DialogueLine("Amy", "But I'm one person, and I've been one person for a long time."),
                    new DialogueLine("Amy", "Your six can hold it with me. That's all I needed — someone to show up with a full nest."),
                    new DialogueLine("Teo", "Then move over."),
                    new DialogueLine("Amy", "...Try not to scramble it."),
                }));
        }

        // ------------------------------------------------------------------
        // dialogue
        // ------------------------------------------------------------------

        /// <summary>The right conversation for this NPC given where the story currently stands.</summary>
        public static DialogueScript GetDialogue(string npcId, StoryState story, GameState state)
        {
            switch (npcId)
            {
                case "ori": return OriDialogue(story, state);
                case "marn": return MarnDialogue(story);
                case "sable": return SableDialogue(story);
                case "vess1": return VessOneDialogue(story);
                case "pim": return PimDialogue(story);
                case "vess2": return VessTwoDialogue(story);

                case "hob": return Resident("Hob", new[]
                {
                    "Forty years I've farmed ash. You learn the trick of it: never fight fire with fire.",
                    "Every egg on this rock is Molten. Bring something wet, or something clever, and you'll walk out with a full carton.",
                    "Bring another Molten and you'll walk out carrying it.",
                }, true);

                case "nell": return Resident("Nell", new[]
                {
                    "Watch how I do it. You don't grab a healthy egg — it just kicks out and you've lost a carton.",
                    "You wear it down first. Get it low, then throw. The difference is night and day, I promise you.",
                    "And keep an eye on the carton count. Twelve is all you get between rests.",
                }, true);

                case "bram": return Resident("Bram", new[]
                {
                    "Careful round the wells. They go down further than the planet ought to allow.",
                    "You've got a young one there. Keep it fighting and it'll crack — properly crack, I mean, and come out bigger.",
                    "Mine did it twice. Went in a Sprouteg, came out something I needed both arms for.",
                }, false);

                case "sax": return Resident("Sax", new[]
                {
                    "Mind the hulls. Half of them are still full of eggs and the other half are still full of sea.",
                    "Rule of the wreck: when a fight turns against you, pull the egg out. Swapping costs you a turn, losing costs you the egg.",
                    "Nobody who bled out down here did it because they couldn't swim. They did it because they wouldn't let go.",
                }, true);

                case "quill": return Resident("Quill", new[]
                {
                    "Don't tell me where you've been. Tell me where you haven't — that's the interesting map.",
                    "You've a chart of your own, haven't you? Press M and look at it properly.",
                    "Anywhere you've already set foot, you can jump straight back to. No sense flying the same dark twice.",
                }, false);

                case "moth": return Resident("Moth", new[]
                {
                    "Light's a habit, not a need. You'll adjust.",
                    "Here's a thing worth knowing: every egg carries a knack from its element. Void ones can't be rattled — you can't lower what they've got.",
                    "The green ones mend themselves as they fight. The stone ones will not go down from full health, not for anything.",
                    "Learn the eight and you'll never be surprised twice.",
                }, true);

                case "wren": return Resident("Wren", new[]
                {
                    "...",
                    "Sorry. You get out of the habit of talking.",
                    "The instruments read nothing here. No temperature, no mass, no sound. And yet the eggs sit perfectly happy in it.",
                    "Which tells you the cold isn't a *place*. It's a direction. Something upstream is drinking, and this is just where the river runs dry.",
                    "Go north and see. I've had eleven years to and I never did.",
                }, true);
            }
            return null;
        }

        /// <summary>A short, repeatable conversation from a non-story resident.</summary>
        static DialogueScript Resident(string speaker, string[] lines, bool rests)
        {
            var built = new DialogueLine[lines.Length];
            for (int i = 0; i < lines.Length; i++) built[i] = new DialogueLine(speaker, lines[i]);
            return new DialogueScript(built, null, null, null, 5, rests);
        }

        /// <summary>What Amy says when you walk up to her, ending in the fight.</summary>
        public static DialogueScript AmyIntro(StoryState story)
        {
            if (story.HasFlag("beat_amy"))
            {
                return new DialogueScript(new[]
                {
                    new DialogueLine("Amy", "Back again? Good. It gets heavy when nobody's pushing."),
                    new DialogueLine("Amy", "Cartons down. Same as before."),
                }, null, "amy");
            }

            return new DialogueScript(new[]
            {
                new DialogueLine("Amy", "Stop there."),
                new DialogueLine("Amy", "I felt you coming from Vesper. Six of them, all different. That's the first time in eleven years."),
                new DialogueLine("Teo", "I know what you've been doing."),
                new DialogueLine("Amy", "Do you. And you came anyway, with a full nest, which means you either want to stop me or relieve me."),
                new DialogueLine("Amy", "Either way the answer is the same, so let's find out which of us can actually carry it."),
                new DialogueLine("Amy", "Cartons down, hatcher. Nothing here is yours to take."),
            }, null, "amy");
        }

        static DialogueScript OriDialogue(StoryState story, GameState state)
        {
            if (!story.HasFlag("met_ori"))
            {
                return new DialogueScript(new[]
                {
                    new DialogueLine("Ori", "There you are. I was starting to think you'd walked into the sea again."),
                    new DialogueLine("Ori", "Sit down a moment. Feel the pad."),
                    new DialogueLine("Teo", "It's cold."),
                    new DialogueLine("Ori", "It has never been cold. Not in forty years. A Nest Station runs warm off the eggs around it — that's the whole trick of it."),
                    new DialogueLine("Ori", "Brineholt's gone cold. Mosswell's gone cold. And now ours."),
                    new DialogueLine("Ori", "So. You're going to go out and collect. Three in your nest, minimum — I can't read the cold off fewer than three."),
                    new DialogueLine("Ori", "Take Sprouteg. It's been yours since it was the size of a thumbnail anyway."),
                    new DialogueLine("Ori", "Shell fields are the pale patches. Eggs hide in them. Some just wander about in the open, the bold ones."),
                    new DialogueLine("Ori", "Wear one down before you throw a carton at it. A healthy egg will kick straight back out, every time."),
                }, "met_ori");
            }

            if (!story.HasFlag("ori_briefed"))
            {
                if (state.TotalCollected < 3)
                {
                    return new DialogueScript(new[]
                    {
                        new DialogueLine("Ori", "Three, I said. You've got " + state.TotalCollected + "."),
                        new DialogueLine("Ori", "Go on. The fields won't walk over here."),
                    }, null, null, null, 5, true);
                }

                return new DialogueScript(new[]
                {
                    new DialogueLine("Ori", "Three. Good. Hold them near the pad — no, closer."),
                    new DialogueLine("Ori", "...Hm."),
                    new DialogueLine("Teo", "What is it?"),
                    new DialogueLine("Ori", "They're not dying, that's the thing. They're being *drawn* on. Something's got a straw in the sector and it's pulling north."),
                    new DialogueLine("Ori", "Which is a stupid thing to say out loud, so I'm going to say it quietly and only to you."),
                    new DialogueLine("Ori", "The Long Drift's the next sector out. There are Keepers there — Marn on Voltacrest, Sable up on Glacierim. They'll have felt it first."),
                    new DialogueLine("Ori", "Go and ask them. And Teo — take the whole nest with you. Not one favourite and five spares."),
                    new DialogueLine("Ori", "Here. Cartons, and everything of yours is warm again."),
                }, "ori_briefed", null, null, 5, true, true);
            }

            // Field-record milestones. Ori is the one who taught you to read a nest,
            // so he is the one who notices when your record starts getting serious.
            if (story.HasFlag("ori_briefed"))
            {
                int recorded = state.Caught.Count;
                int catchable = SpeciesDatabase.CatchableCount;

                if (recorded >= catchable && !story.HasFlag("ori_record_full"))
                {
                    return new DialogueScript(new[]
                    {
                        new DialogueLine("Ori", "Let me see the record. All of it."),
                        new DialogueLine("Ori", "..."),
                        new DialogueLine("Ori", "Every one. Every single species anybody has ever managed to carry off a rock."),
                        new DialogueLine("Ori", "I have been doing this for forty years and I have never closed a record. Not once."),
                        new DialogueLine("Ori", "So. There's a thing I've been keeping."),
                        new DialogueLine("Ori", "It was in the straw the day I took over this station. Never hatched, never cracked, never got a scratch on it."),
                        new DialogueLine("Ori", "I always told myself I'd hand it on when somebody turned up who'd earned it."),
                        new DialogueLine("Teo", "Ori —"),
                        new DialogueLine("Ori", "Don't. Just take it out there and let it see the place."),
                    }, "ori_record_full", null, "bloomolk", 20, true, true);
                }

                if (recorded >= 18 && !story.HasFlag("ori_record_18"))
                {
                    return new DialogueScript(new[]
                    {
                        new DialogueLine("Ori", "Eighteen. You've eighteen species in that record."),
                        new DialogueLine("Ori", "The station log says the last hatcher to break fifteen was me, and I cheated — I counted one twice."),
                        new DialogueLine("Ori", "Keep at it. There are a few out there I've only ever read about."),
                    }, "ori_record_18", null, null, 5, true, true);
                }

                if (recorded >= 10 && !story.HasFlag("ori_record_10"))
                {
                    return new DialogueScript(new[]
                    {
                        new DialogueLine("Ori", "Ten different species. That's a proper record, that is."),
                        new DialogueLine("Ori", "Most hatchers find their four favourites and stop. Don't stop."),
                        new DialogueLine("Ori", "A wide nest reads the cold better than a strong one. Remember that when you get north."),
                    }, "ori_record_10", null, null, 5, true, true);
                }
            }

            if (story.HasFlag("beat_amy"))
            {
                return new DialogueScript(new[]
                {
                    new DialogueLine("Ori", "Pad's warm. Warmer than I've felt it in a decade."),
                    new DialogueLine("Ori", "I'm told there's someone up on Amaranth sharing the load now. Two of them."),
                    new DialogueLine("Ori", "Don't tell me the details. I'd only worry retroactively."),
                    new DialogueLine("Ori", "Rest as long as you like. You've earned the straw."),
                }, null, null, null, 5, true);
            }

            return new DialogueScript(new[]
            {
                new DialogueLine("Ori", "Still cold. Still pulling north."),
                new DialogueLine("Ori", "Sit, rest your nest, then get back out there."),
            }, null, null, null, 5, true);
        }

        static DialogueScript MarnDialogue(StoryState story)
        {
            if (story.HasFlag("keeper_marn"))
            {
                return new DialogueScript(new[]
                {
                    new DialogueLine("Marn", "Still dropping. Point four a day. I've stopped writing it down, it was making me ill."),
                    new DialogueLine("Marn", "Go north. Please go north."),
                }, null, null, null, 5, true);
            }

            return new DialogueScript(new[]
            {
                new DialogueLine("Marn", "Don't touch the mast. Don't — thank you."),
                new DialogueLine("Marn", "You're the one Ori sent. Right. Good. Look at this."),
                new DialogueLine("Marn", "Station temperature, eleven years of it. Flat, flat, flat, and then it walks downhill and never stops."),
                new DialogueLine("Teo", "Eleven years."),
                new DialogueLine("Marn", "Eleven years and two months. It didn't start slow, either. It started all at once, like someone opened a tap."),
                new DialogueLine("Marn", "And it's worse the further north you go. Sable's readings on Glacierim make mine look cosy."),
                new DialogueLine("Marn", "Ask her. She's been at this longer than the rest of us put together."),
            }, "keeper_marn", null, null, 5, true);
        }

        static DialogueScript SableDialogue(StoryState story)
        {
            if (story.HasFlag("keeper_sable"))
            {
                return new DialogueScript(new[]
                {
                    new DialogueLine("Sable", "North, hatcher. It was always going to be north."),
                }, null, null, null, 5, true);
            }

            return new DialogueScript(new[]
            {
                new DialogueLine("Sable", "Shut the door. The cold in here is mine, I'd rather not share it with the cold out there."),
                new DialogueLine("Sable", "Marn sent you. Marn sends everyone. He's frightened, and he's right to be."),
                new DialogueLine("Teo", "He said it started eleven years ago."),
                new DialogueLine("Sable", "Eleven years ago the Prime cracked."),
                new DialogueLine("Teo", "The what?"),
                new DialogueLine("Sable", "They don't teach it in the Reach any more. Fine. Listen once."),
                new DialogueLine("Sable", "Every planet you have ever stood on came off one egg. It broke, a long time ago, and the pieces cooled into worlds. That's all a planet is. Shell."),
                new DialogueLine("Sable", "The core of it is still up there. Still whole. Still, technically, unhatched."),
                new DialogueLine("Sable", "Eleven years ago it started to split, and someone up on Amaranth started pulling every scrap of warmth in three sectors to hold it shut."),
                new DialogueLine("Sable", "So it isn't a thief, hatcher. It's a woman with her arms around something enormous, and no relief coming."),
                new DialogueLine("Sable", "Go and see. Take the Belt road. Find Pim on Aetherwake — she hears it better than she talks."),
            }, "keeper_sable", null, null, 5, true);
        }

        static DialogueScript VessOneDialogue(StoryState story)
        {
            if (story.HasFlag("beat_vess_1"))
            {
                return new DialogueScript(new[]
                {
                    new DialogueLine("Vess", "Go north, I said. Why are you still standing on my rock."),
                }, null, null, null, 5, true);
            }

            return new DialogueScript(new[]
            {
                new DialogueLine("Vess", "Nope. Field's mine. I've been here since the tide turned and I've cleared four patches."),
                new DialogueLine("Teo", "You've taken everything. There's nothing left in the stone for anyone else."),
                new DialogueLine("Vess", "Correct."),
                new DialogueLine("Vess", "You've got the Reach accent, so let me guess — warm pads, full nests, an old man who tells you stories."),
                new DialogueLine("Vess", "Out here the nests go cold and stay cold, and the only eggs that live are the ones somebody carried off before the pad quit."),
                new DialogueLine("Vess", "So yes. I take them. All of them."),
                new DialogueLine("Teo", "Then take mine off me first."),
                new DialogueLine("Vess", "...Oh, I *like* that. Cartons down, hatcher."),
            }, null, "vess_1");
        }

        static DialogueScript PimDialogue(StoryState story)
        {
            if (story.HasFlag("learned_truth"))
            {
                return new DialogueScript(new[]
                {
                    new DialogueLine("Pim", "Still one note. Still a bit flat."),
                    new DialogueLine("Pim", "She's tired, that's all it is. Go on."),
                }, null, null, null, 5, true);
            }

            return new DialogueScript(new[]
            {
                new DialogueLine("Pim", "Shh. Listen first, ask after."),
                new DialogueLine("Pim", "...Hear it?"),
                new DialogueLine("Teo", "It's just the debris."),
                new DialogueLine("Pim", "It's one note. All of it. Every rock in this belt is humming the same note, because it all came off the same shell, and shell remembers."),
                new DialogueLine("Pim", "Been flat eleven years. Flat, and getting flatter, which is what a thing does when it's being asked to hold."),
                new DialogueLine("Pim", "There's a woman up on Amaranth with her arms around the Prime Egg. Amy. Nobody's relieved her since before you could walk."),
                new DialogueLine("Teo", "Then I'll relieve her."),
                new DialogueLine("Pim", "You'll need a full nest. Six, at least, and not six of the same — the Prime doesn't take kindly to one flavour."),
                new DialogueLine("Pim", "And she won't just hand it over. She'll make you prove your six can carry it."),
                new DialogueLine("Pim", "One more thing. Your loud friend went to Vesper. She means to break the Prime, not hold it. She thinks that's mercy."),
                new DialogueLine("Pim", "Go and be louder."),
            }, "learned_truth");
        }

        static DialogueScript VessTwoDialogue(StoryState story)
        {
            if (story.HasFlag("beat_vess_2"))
            {
                return new DialogueScript(new[]
                {
                    new DialogueLine("Vess", "Belt's holding. Go and do your bit."),
                }, null, null, null, 5, true);
            }

            return new DialogueScript(new[]
            {
                new DialogueLine("Vess", "You made it further than I thought."),
                new DialogueLine("Teo", "Pim said you're going to break it."),
                new DialogueLine("Vess", "I'm going to *end* it. There's a difference, and the difference is that mine stops."),
                new DialogueLine("Vess", "Every nest in three sectors is being bled so one rock stays shut. Open the rock, the bleeding stops."),
                new DialogueLine("Teo", "And every egg everywhere goes with it."),
                new DialogueLine("Vess", "You don't know that."),
                new DialogueLine("Teo", "Neither do you. That's the point."),
                new DialogueLine("Vess", "...Then you'd better be able to stop me, hadn't you."),
            }, null, "vess_2");
        }
    }
}
