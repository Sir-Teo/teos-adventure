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

        /// <summary>What Ori asks for before the cold-reading, and what Amaranth asks for at the end.
        /// Named because the objectives and several characters say these numbers out loud.</summary>
        /// <summary>Where Ori notices the record. He says both numbers out loud, so they live
        /// here rather than as literals inside his dialogue.</summary>
        public const int RecordNoticeFirst = 10;
        public const int RecordNoticeSecond = 18;

        /// <summary>
        /// What each gate flag means, in words, for the "Still needed" line. A beat that wants
        /// two people found reads the same whether you have found neither or one of them, which
        /// is the moment a player most wants to be told which one is left.
        /// </summary>
        static readonly Dictionary<string, string> FlagLabels = new Dictionary<string, string>
        {
            { "met_ori",      "Ori at the Yolkhaven Nest Station" },
            { "ori_briefed",  "your three eggs, back to Ori" },
            { "keeper_marn",  "Marn on Voltacrest" },
            { "keeper_sable", "Sable on Glacierim" },
            { "learned_truth","Pim on Aetherwake" },
            { "beat_vess_1",  "Vess on Cobblestead" },
            { "beat_vess_2",  "Vess on Vesper" },
            { "beat_amy",     "Amy on Amaranth Prime" },
            // Nothing is gated on Ori noticing his egg, so it has no label to show as
            // outstanding - but it is still a flag, and leaving it out of this map would
            // make it the only one nobody can look up.
            { "ori_saw_starter", null },
        };

        /// <summary>A short phrase for a gate flag, or null if it has none.</summary>
        public static string LabelForFlag(string flag)
        {
            string label;
            return FlagLabels.TryGetValue(flag, out label) ? label : null;
        }

        public const int FirstCatchEggs = 3;
        /// <summary>The level at which Ori notices what his Sprouteg has grown into.</summary>
        public const int OriNoticesLevel = 16;

        public const int GateEggs = 6;
        public const int GateTypes = 4;
        public const int GateLevel = 22;


        public static readonly StoryBeat[] Beats =
        {
            new StoryBeat("wake", "Chapter 1 · The Cold Nests",
                "Find Ori at the Yolkhaven Nest Station.",
                Sector.HatcheryReach, new[] { "met_ori" }),

            // The objectives spell their own requirements. Written out by hand they were true
            // beside the numbers they were written beside, and silently false the moment either
            // moved - and an objective that misstates its own gate is the worst line in the game
            // to get wrong.
            new StoryBeat("first_catch", "Chapter 1 · The Cold Nests",
                "Collect eggs until you are carrying " + Words.Spell(FirstCatchEggs) +
                ", so Ori can show you the cold-reading.",
                Sector.HatcheryReach, null, FirstCatchEggs),

            new StoryBeat("report_ori", "Chapter 1 · The Cold Nests",
                "Take your " + Words.Spell(FirstCatchEggs) + " eggs back to Ori.",
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
                "Amaranth Prime will only open to a full nest. Bring " + Words.Spell(GateEggs) +
                " eggs, " + Words.Spell(GateTypes) + " types, and one raised to " + GateLevel + ".",
                Sector.ShatteredBelt, null, GateEggs, GateTypes, GateLevel),

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
            new NpcDef("lune",  "Lune",  "shimmerfen",  new Vector2(7f, -6f),   C(0xC9B4F0)),
            new NpcDef("tilda", "Tilda", "arcmoor",     new Vector2(-8f, -5f),  C(0xCFDD5E)),
            new NpcDef("garrow","Garrow","cairnhold",   new Vector2(6f, 7f),    C(0xA9A6B4)),
        };

        // ------------------------------------------------------------------
        // trainers
        // ------------------------------------------------------------------

        static readonly Dictionary<string, TrainerDef> trainers = new Dictionary<string, TrainerDef>();

        /// <summary>The highest level a trainer fields, for telling a player what they face.</summary>
        public static int TopLevelOf(string trainerId)
        {
            var t = GetTrainer(trainerId);
            if (t == null || t.Levels == null || t.Levels.Length == 0) return 0;
            int top = 0;
            foreach (int lv in t.Levels) if (lv > top) top = lv;
            return top;
        }

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
                // Reginova is a level up on what it used to be. Lingering conditions gave the
                // player three ways to wear this fight down where Amy has one, and a bare-legal
                // nest went from winning 61% to 84%. Her ace absorbs the difference; bumping all
                // three was far too coarse, taking it from 82% to 42% in a single level.
                new[] { 19, 20, 23 },
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
        public static NpcDef NpcById(string id)
        {
            for (int i = 0; i < Npcs.Length; i++) if (Npcs[i].Id == id) return Npcs[i];
            return null;
        }

        /// <summary>
        /// One line from each resident about the landmark on their own world, the first time you
        /// talk to them after reading it.
        ///
        /// Every NPC lives on a world with a landmark, and six of the inscriptions name them
        /// outright - Ori's notches, Sable's notebook, the chalk drawing signed VESS. Walking out
        /// to read a stone and having nobody ever mention it made the landmarks feel like set
        /// dressing rather than the place these people actually live.
        /// </summary>
        static string[] LandmarkAside(string npcId)
        {
            switch (npcId)
            {
                case "ori": return new[] {
                    "You found the post. My father cut the first notch. I cut the one at my shoulder.",
                    "The empty column is not for me. I started it the year you were born, if you must know." };
                case "hob": return new[] {
                    "The bell? I took the rope off it myself. Eleven years is long enough to keep flinching.",
                    "I kept the rope, mind. That is not the same as throwing it out." };
                case "nell": return new[] {
                    "Forty stones, and I set the last four of them. My hand was not steady. It shows.",
                    "The water is not coming in any faster. It is just not going back out." };
                case "bram": return new[] {
                    "Somebody cut that warning before I was born and nobody has argued with it since.",
                    "I lower a line every spring. It has never come up wet. It has never come up dry either." };
                case "sax": return new[] {
                    "The Gannet. Eggs nest in her hold now, which is more use than she ever was to me.",
                    "I scrubbed at that date a whole winter. Some things would rather not be read." };
                case "marn": return new[] {
                    "Count the strike-marks if you like. I stopped at four hundred and took up drinking.",
                    "It is still standing. So am I. Neither of us can tell you why." };
                case "tilda": return new[] {
                    "You walked the fence, then. Everyone does once, and then everyone asks the same thing.",
                    "I have the last stake. It goes back when somebody tells me what the tune was for." };
                case "quill": return new[] {
                    "Hundreds of clay eggs and not one of them fired. My mother's work. She meant to return.",
                    "I keep the door propped. If the fire ever comes back it will find the shelves ready." };
                case "vess1": return new[] {
                    "Two hundred paces of good wall and then nothing. People assume the builder died.",
                    "They did not. They worked out that the wall was not going to help, and set the stone down." };
                case "sable": return new[] {
                    "You read my notebook. It was not locked, so I can hardly complain about it.",
                    "The same three words for two years. I write them because writing them is not nothing." };
                case "moth": return new[] {
                    "Sixty lamps. I lit every one, and it was exactly as dark as before. Warmer, though.",
                    "The second note is mine. If you want to waste an evening, the oil is still in the tin." };
                case "pim": return new[] {
                    "The chalk mark is mine. That is where the note used to sit, before it came down.",
                    "It has not shifted in eleven years. I check it each morning like a fool with a job." };
                case "wren": return new[] {
                    "Eleven years of nothing, recorded properly. Do not laugh. Nothing is a reading.",
                    "The day it moves, somebody will want to know exactly how long it did not." };
                case "vess2": return new[] {
                    "You saw the drawing, then. I scrubbed at the name and could not finish the job.",
                    "I was six. I drew a sun on a world that has not had one since." };
                case "garrow": return new[] {
                    "Nine hundred, near enough. I do not count them and I do not ask whose rock is whose.",
                    "The oldest one is not mine. Somebody was doing this before the Belt came apart." };
                case "lune": return new[] {
                    "A sundial. On a world with no sun. My grandmother cut it there out of pure habit.",
                    "I have never once thought that was stupid. I have thought a great many other things." };
                default: return null;
            }
        }

        /// <summary>
        /// The aside rides on the front of whatever the NPC was going to say, rather than
        /// replacing it. Replacing it would have swallowed Ori's "three, I said" nag, which is
        /// the only thing telling a new player what to do next.
        /// </summary>
        public static DialogueScript GetDialogue(string npcId, StoryState story, GameState state)
        {
            var script = NpcDialogue(npcId, story, state);
            if (script == null || state == null) return script;

            var npc = NpcById(npcId);
            if (npc == null || !state.Landmarks.Contains(npc.PlanetId)) return script;
            if (state.LandmarkAsides.Contains(npcId)) return script;

            var aside = LandmarkAside(npcId);
            if (aside == null) return script;
            state.LandmarkAsides.Add(npcId);

            var merged = new DialogueLine[aside.Length + script.Lines.Length];
            for (int i = 0; i < aside.Length; i++) merged[i] = new DialogueLine(npc.Name, aside[i]);
            for (int i = 0; i < script.Lines.Length; i++) merged[aside.Length + i] = script.Lines[i];

            return new DialogueScript(merged, script.SetsFlag, script.StartsTrainer,
                                      script.GivesSpeciesId, script.GivesSpeciesLevel,
                                      script.HealsParty, script.RestocksCartons);
        }

        static DialogueScript NpcDialogue(string npcId, StoryState story, GameState state)
        {
            switch (npcId)
            {
                case "ori": return OriDialogue(story, state);
                case "marn": return MarnDialogue(story);
                case "sable": return SableDialogue(story);
                case "vess1": return VessOneDialogue(story);
                case "pim": return PimDialogue(story);
                case "vess2": return VessTwoDialogue(story);

                case "hob": return Resident(story, "Hob", new[]
                {
                    "Forty years I've farmed ash. You learn the trick of it: never fight fire with fire.",
                    "Everything on this rock is Molten, near enough. Bring something wet, or something clever, and you'll walk out with a full carton.",
                    "The odd Cobblet rolls down off the scree. Forty years, and I have never once counted it as ash.",
                    "Bring another Molten and you'll walk out carrying it.",
                }, new[]
                {
                    "Ash is warm again. Not hot. *Warm.* Forty years, I know the difference.",
                    "And you did it the way I would have told you to, near enough. Brought something that was not fire.",
                }, true);

                case "nell": return Resident(story, "Nell", new[]
                {
                    "Watch how I do it. You don't grab a healthy egg — it just kicks out and you've lost a carton.",
                    "You wear it down first. Get it low, then throw. The difference is night and day, I promise you.",
                    "And keep an eye on the carton count. Twelve is all you get between rests.",
                }, new[]
                {
                    "Caught six before breakfast and never broke a sweat. They are *lively* again.",
                    "You can tell, you know. A cold egg does not kick. These kick.",
                }, true);

                case "bram": return Resident(story, "Bram", new[]
                {
                    "Careful round the wells. They go down further than the planet ought to allow.",
                    "You've got a young one there. Keep it fighting and it'll crack — properly crack, I mean, and come out bigger.",
                    "Mine did it twice. Went in a Sprouteg, came out something I needed both arms for.",
                }, new[]
                {
                    "Wells have gone quiet. The good quiet — the kind where nothing is being pulled down them.",
                    "My old Bloomolk cracked again last night. Third time. I did not know they could.",
                }, false);

                case "sax": return Resident(story, "Sax", new[]
                {
                    "Mind the hulls. Half of them are still full of eggs and the other half are still full of sea.",
                    "Rule of the wreck: when a fight turns against you, pull the egg out. Swapping costs you a turn, losing costs you the egg.",
                    "Nobody who bled out down here did it because they couldn't swim. They did it because they wouldn't let go.",
                }, new[]
                {
                    "Tide came in warm. First time since I got here.",
                    "You knew when to pull an egg out and when to hold on. That is the whole job. Most only ever learn the one half.",
                }, true);

                case "quill": return Resident(story, "Quill", new[]
                {
                    "Don't tell me where you've been. Tell me where you haven't — that's the interesting map.",
                    "You've a chart of your own, haven't you? Press M and look at it properly.",
                    "Anywhere you've already set foot, you can jump straight back to. No sense flying the same dark twice.",
                }, new[]
                {
                    "So. Where have you not been?",
                    "Do not answer. I have seen your chart. There is nothing left on it you have not stood on.",
                    "Come back when somebody draws a new one.",
                }, false);

                case "moth": return Resident(story, "Moth", new[]
                {
                    "Light's a habit, not a need. You'll adjust.",
                    "Here's a thing worth knowing: every egg carries a knack from its element. Void ones can't be rattled — you can't lower what they've got.",
                    "The green ones mend themselves as they fight. The stone ones will not go down from full health, not for anything.",
                    "Learn the eight and you'll never be surprised twice.",
                }, new[]
                {
                    "Light has come back a little. I had got so used to the habit I nearly resented it.",
                    "Every egg on this rock stood up straighter the same hour. All eight knacks at once. You do not see that.",
                }, true);

                case "lune": return Resident(story, "Lune", new[]
                {
                    "You came over the fen without a lamp. Brave, or you didn't know it was a fen.",
                    "Here — take a salve while you're standing still. Nobody thinks to use one mid-fight, and that's the only time it counts.",
                    "It costs you the turn, mind. You'll take a hit for it. But an egg that's still standing is worth more than a turn.",
                    "Four in a stack, same as your cartons. Any Nest Station will fill both.",
                }, new[]
                {
                    "The fen is throwing shadows. Ours, I mean. We have them again.",
                    "Keep the salves on you. The habit is worth more than the stack.",
                }, true);

                case "tilda": return Resident(story, "Tilda", new[]
                {
                    "Mind the heather. It's not the ground that's charged, it's the air above it.",
                    "You'll have seen the little arrows on an egg's card — the up ones and the down ones. Those aren't decoration.",
                    "A move that drops the other one's speed sticks for the whole fight. Two of those and you're going first every round, whatever it was born with.",
                    "Half of winning up here is deciding who moves first. The other half is remembering you decided it.",
                }, new[]
                {
                    "Heather is humming a whole tone up. I have had to retune every fence on the moor.",
                    "So you worked out who moves first. Good. Most never do.",
                }, true);

                case "garrow": return Resident(story, "Garrow", new[]
                {
                    "Every cairn out there is somebody's rock. I stack them; I don't ask whose.",
                    "You'll meet a big one eventually. Older, three levels past its neighbours, and lit up round the shell.",
                    "Don't throw early at those. They sit in the carton twice as hard as an ordinary egg — get it right down first or you'll spend the whole stack.",
                    "Worth it, though. They come up faster than anything you'll raise from a hatchling.",
                }, new[]
                {
                    "Stacked one for you. Small one, mind — you are not dead, it would be rude to make it big.",
                    "The old ones are coming out of the rock again. Big, lit up round the shell. Years since the Belt had Elders in it.",
                }, true);

                case "wren": return Resident(story, "Wren", new[]
                {
                    "...",
                    "Sorry. You get out of the habit of talking.",
                    "The instruments read nothing here. No temperature, no mass, no sound. And yet the eggs sit perfectly happy in it.",
                    "Which tells you the cold isn't a *place*. It's a direction. Something upstream is drinking, and this is just where the river runs dry.",
                    "Go north and see. I've had eleven years to and I never did.",
                }, new[]
                {
                    "The instruments read something.",
                    "Not much. A temperature. It has been eleven years since this place had a temperature.",
                    "You went north. I never did. I am glad one of us was going to.",
                }, true);
            }
            return null;
        }

        /// <summary>
        /// A resident who has something different to say once the nests are warm again.
        /// Everyone on every world felt this happen; it would be strange if only the keepers did.
        /// </summary>
        static DialogueScript Resident(StoryState story, string speaker, string[] lines,
                                       string[] afterAmy, bool rests) =>
            Resident(speaker, story.HasFlag("beat_amy") && afterAmy != null ? afterAmy : lines, rests);

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
                    new DialogueLine("Ori", "And walk out past the fields sometime. There's a post out there with my name cut in it."),
                    new DialogueLine("Ori", "I was your height when I cut it. Every world has one of something, if you go far enough."),
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
                int recorded = state.RecordedCatchable;
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

                if (recorded >= RecordNoticeSecond && !story.HasFlag("ori_record_18"))
                {
                    return new DialogueScript(new[]
                    {
                        new DialogueLine("Ori", Words.SpellCapitalised(RecordNoticeSecond) + ". You've " +
                                                Words.Spell(RecordNoticeSecond) + " species in that record."),
                        new DialogueLine("Ori", "The station log says the last hatcher to break fifteen was me, and I cheated — I counted one twice."),
                        new DialogueLine("Ori", "Keep at it. There are a few out there I've only ever read about."),
                    }, "ori_record_18", null, null, 5, true, true);
                }

                if (recorded >= RecordNoticeFirst && !story.HasFlag("ori_record_10"))
                {
                    return new DialogueScript(new[]
                    {
                        new DialogueLine("Ori", Words.SpellCapitalised(RecordNoticeFirst) +
                                                " different species. That's a proper record, that is."),
                        new DialogueLine("Ori", "Most hatchers find their four favourites and stop. Don't stop."),
                        new DialogueLine("Ori", "A wide nest reads the cold better than a strong one. Remember that when you get north."),
                    }, "ori_record_10", null, null, 5, true, true);
                }

                // The egg he handed over, brought back grown.
                //
                // It was an ordinary Sprouteg, so the moment a player caught a second one the
                // game had no idea which was which - and the one thing Ori would certainly
                // notice was the one thing he could not.
                //
                // Below the record milestones on purpose. The full-record gift is the biggest
                // thing Ori has to say, and an aside about a Sprouteg must never stand in front
                // of it.
                var his = state.EggFromOri;
                if (his != null && !story.HasFlag("ori_saw_starter") &&
                    (his.Level >= OriNoticesLevel || his.Species.Id != "sprouteg"))
                {
                    return new DialogueScript(new[]
                    {
                        new DialogueLine("Ori", "Hold on. Bring that one here."),
                        new DialogueLine("Ori", "..."),
                        new DialogueLine("Ori", his.Species.Id != "sprouteg"
                            ? "It cracked, then. I had that in a drawer the size of my thumb, and now look at it."
                            : "Look at the size of it. I had that in a drawer the size of my thumb."),
                        new DialogueLine("Ori", "You have been feeding it properly. That is not nothing, whatever else happens out there."),
                        new DialogueLine("Teo", "It mostly feeds itself."),
                        new DialogueLine("Ori", "They all say that. Go on."),
                    }, "ori_saw_starter", null, null, 5, true);
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
            if (story.HasFlag("beat_amy"))
            {
                return new DialogueScript(new[]
                {
                    new DialogueLine("Marn", "Point four a day. *Up.* I've started writing it down again."),
                    new DialogueLine("Marn", "Eleven years I watched that line walk downhill. It's been climbing eight days and I still check it twice an hour in case it stops."),
                    new DialogueLine("Marn", "I don't know what you did up there. I know what it looks like from a mast on Voltacrest."),
                    new DialogueLine("Marn", "It looks like somebody finally answered."),
                }, null, null, null, 5, true);
            }


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
            if (story.HasFlag("beat_amy"))
            {
                return new DialogueScript(new[]
                {
                    new DialogueLine("Sable", "Shut the — no. Leave it open. Let some of it out."),
                    new DialogueLine("Sable", "You did not fix it. I want you clear on that. A thing that size is never fixed, it is *carried*."),
                    new DialogueLine("Teo", "I know."),
                    new DialogueLine("Sable", "Good. Then here is the part worth having: it was one pair of arms for eleven years, and now it is two."),
                    new DialogueLine("Sable", "Two is not twice one. Two is the difference between holding, and holding on."),
                    new DialogueLine("Sable", "Come back when you are tired of it. I will teach you the rest of what they stopped teaching."),
                }, null, null, null, 5, true);
            }


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
            if (story.HasFlag("beat_amy"))
            {
                return new DialogueScript(new[]
                {
                    new DialogueLine("Vess", "Heard. The whole Belt heard."),
                    new DialogueLine("Vess", "I am going back down to the Reach. Somebody has to tell them the nests are coming back."),
                    new DialogueLine("Vess", "I am told I am loud. Might as well be loud about something true."),
                }, null, null, null, 5, true);
            }


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
            if (story.HasFlag("beat_amy"))
            {
                return new DialogueScript(new[]
                {
                    new DialogueLine("Pim", "Shh."),
                    new DialogueLine("Pim", "...It moved."),
                    new DialogueLine("Pim", "Eleven years flat. Came up this morning. Not much — a hair, you would not hear it."),
                    new DialogueLine("Pim", "That is you, that is. That is the sound of somebody putting their shoulder in."),
                    new DialogueLine("Pim", "Go on. I want to hear it on my own a while."),
                }, null, null, null, 5, true);
            }


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
            if (story.HasFlag("beat_amy"))
            {
                return new DialogueScript(new[]
                {
                    new DialogueLine("Vess", "So you held it."),
                    new DialogueLine("Vess", "I would have broken it. On the numbers I had I still think I would have been right to."),
                    new DialogueLine("Vess", "But you had a third answer and I only ever counted to two."),
                    new DialogueLine("Vess", "...Is she all right? Amy."),
                    new DialogueLine("Teo", "She is now."),
                    new DialogueLine("Vess", "Then that is the bit I got wrong. I had stopped thinking of her as a person somewhere around year nine."),
                    new DialogueLine("Vess", "Go on. Someone ought to be up there, and it was never going to be me."),
                }, null, null, null, 5, true);
            }


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
