using System.Collections.Generic;

namespace Eggverse
{
    /// <summary>
    /// The silhouette a landmark is built from. Seventeen were drawn as one identical grey
    /// standing stone, which at map scale is indistinguishable from a rock - the render made
    /// that obvious in a way the assertions never would. Six forms cover all seventeen, and
    /// each one reads differently from across a field.
    /// </summary>
    public enum LandmarkForm
    {
        Post,     // a tall upright: the notched post, the mast, the tuning slab, the pillar
        Frame,    // uprights with things hung between them: the bell, the fence, the lamp rack
        Stones,   // a line or scatter of set stones: the tideline, the cairns, the wall
        Hollow,   // a ring around an opening: the well, the listening bowl, the twin pads
        Hulk,     // one heavy mass: the ship's bow, the kiln, the gauge on its tripod
        Seam,     // a line across the ground: Amaranth's mortared crack
    }

    /// <summary>One thing worth walking to, on every world.</summary>
    public class LandmarkDef
    {
        public readonly string PlanetId;
        public readonly string Name;
        public readonly LandmarkForm Form;
        public readonly string[] Lines;

        /// <summary>Read only when every other inscription has been. Null on all but one.</summary>
        public string[] Coda;

        public LandmarkDef(string planetId, string name, LandmarkForm form, params string[] lines)
        {
            PlanetId = planetId; Name = name; Form = form; Lines = lines;
        }

        public LandmarkDef WithCoda(params string[] coda) { Coda = coda; return this; }
    }

    /// <summary>
    /// A marker on each world, out past the shell fields, that says something only that world
    /// could say.
    ///
    /// Every planet had the same four things on it - fields, roamers, a station, one resident -
    /// so there was no reason to walk anywhere except between the fields. Four worlds hide a
    /// supply cache; the other thirteen hid nothing at all. These give the rest of the map a
    /// reason to be crossed, and together they tell the part of the history nobody in the cast
    /// is old enough to have seen.
    ///
    /// They are not signposted. You find them by going to look.
    /// </summary>
    public static class LandmarkDatabase
    {
        static readonly Dictionary<string, LandmarkDef> byPlanet = new Dictionary<string, LandmarkDef>();

        public static LandmarkDef For(string planetId)
        {
            LandmarkDef d;
            return byPlanet.TryGetValue(planetId, out d) ? d : null;
        }

        public static IEnumerable<LandmarkDef> All => byPlanet.Values;

        /// <summary>True once every inscription except this one has been read.</summary>
        public static bool AllOthersRead(string planetId, ICollection<string> read)
        {
            foreach (var d in byPlanet.Values)
                if (d.PlanetId != planetId && !read.Contains(d.PlanetId)) return false;
            return true;
        }
        public static int Count => byPlanet.Count;

        static void Add(LandmarkDef d) { byPlanet[d.PlanetId] = d; }

        static LandmarkDatabase()
        {
            // ---------------- The Hatchery Reach ----------------
            Add(new LandmarkDef("yolkhaven", "The First Post", LandmarkForm.Post,
                "A wooden post, worn smooth, with notches cut up one side.",
                "The lowest is at a child's height and reads ORI. The highest was cut this year.",
                "Somebody has started a fresh column on the other face, and left it empty."));

            Add(new LandmarkDef("cinderoost", "The Slagfield Bell", LandmarkForm.Frame,
                "A bell cast from cooled slag, hung between two posts. No rope.",
                "The plaque says it was rung when the ash storms came, and that it has not needed",
                "ringing in eleven years. The ash has not stopped. Nobody has taken the bell down."));

            Add(new LandmarkDef("brineholt", "The Tideline Stones", LandmarkForm.Stones,
                "Forty stones set in a curve, each marking where the water reached in some year.",
                "The outermost are green with weed and generations old.",
                "The last four are set well inland, close together, and cut in a hurried hand."));

            Add(new LandmarkDef("mosswell", "The Deep Well", LandmarkForm.Hollow,
                "A well with no bucket and no rope, ringed by a low wall people sit on.",
                "Drop a stone and you will not hear it land.",
                "Somebody has scratched into the rim: IT IS NOT WATER DOWN THERE."));

            // ---------------- The Long Drift ----------------
            Add(new LandmarkDef("shimmerfen", "The Shadowless Marker", LandmarkForm.Post,
                "A stone pillar in open ground, casting nothing in any direction.",
                "The fen has no sun and no dark, only an even standing light with no source.",
                "Whoever raised the pillar carved a sundial into the top anyway, out of habit."));

            Add(new LandmarkDef("tidewrack", "The Hull of the Gannet", LandmarkForm.Hulk,
                "A ship's bow driven into the rock at an angle no tide could manage.",
                "Eggs nest in the hold now, in the coils of rope, warm against the iron.",
                "The name board is legible. The date under it is not."));

            Add(new LandmarkDef("voltacrest", "The Standing Mast", LandmarkForm.Post,
                "An iron mast, blackened, with eleven years of strike-marks scored into it.",
                "The oldest are a hand apart. The newest are so close they have run together.",
                "It is still standing, which the marks suggest is remarkable."));

            Add(new LandmarkDef("arcmoor", "The Humming Fence", LandmarkForm.Frame,
                "A fence of iron stakes across empty moor, fencing nothing in or out.",
                "Each stake hums a different note. Walk the line and it plays.",
                "The tune stops one stake short of finishing. That stake is missing."));

            Add(new LandmarkDef("emberfall", "The Last Kiln", LandmarkForm.Hulk,
                "A kiln the size of a house, cold, with its door propped open by a stone.",
                "The shelves inside are stacked with unfired clay eggs, hundreds of them.",
                "Whoever was making them meant to come back before the fire went out."));

            Add(new LandmarkDef("cobblestead", "The Unfinished Wall", LandmarkForm.Stones,
                "A drystone wall running out of the hills and stopping in flat ground.",
                "It is beautifully made for two hundred paces and then simply ends.",
                "The next stones are stacked and squared, ready, exactly where they were set down."));

            Add(new LandmarkDef("glacierim", "The Listening Hollow", LandmarkForm.Hollow,
                "A bowl worn into the ice, smooth, with a bench cut at one edge.",
                "Sit in it and the shelf's noise resolves into something with a rhythm.",
                "Sable has left a notebook here. Most pages are the same three words, dated."));

            // ---------------- The Shattered Belt ----------------
            Add(new LandmarkDef("umbralux", "The Lamp Rack", LandmarkForm.Frame,
                "A frame hung with sixty lamps, every one of them burned out.",
                "A tin beside it holds wicks and oil, untouched, and a note: LIGHT THEM IF YOU LIKE.",
                "Underneath, in a different hand: I DID. IT DID NOT HELP. LEAVE THEM."));

            Add(new LandmarkDef("aetherwake", "The Tuning Stone", LandmarkForm.Post,
                "A slab of shell taller than you are, humming the Belt's one flat note.",
                "Strike it and the note does not change. Pim says it has not changed in eleven years.",
                "There is a chalk mark on it from where the note used to sit, well above the line."));

            Add(new LandmarkDef("nullreach", "The Nothing Gauge", LandmarkForm.Hulk,
                "An instrument on a tripod, needles at zero, dials clean and carefully maintained.",
                "Wren winds it every morning. It has never read anything at all.",
                "The logbook is full: eleven years of the same entry, in a steady hand."));

            Add(new LandmarkDef("vesper", "The Two Cold Stations", LandmarkForm.Hollow,
                "Two Nest Station pads, side by side, both dark. The straw is still in them.",
                "A child's chalk drawing on the wall of the near one: a person, an egg, a sun.",
                "The name under the drawing has been scrubbed at, and not quite removed. VESS."));

            Add(new LandmarkDef("cairnhold", "The Cairn Field", LandmarkForm.Stones,
                "Nine hundred cairns, each one stone from somewhere else in the Belt.",
                "Garrow stacks them. He does not ask whose rock is whose and does not keep a count.",
                "The oldest cairn is not his. It has been here longer than the Belt has been broken."));

            // ---------------- Amaranth ----------------
            // The last word, and the only coda in the game. Sixteen inscriptions record the year
            // something started going wrong; not one of them records anybody fixing it. That is
            // the argument the whole collection has been making without saying so, and it only
            // lands once you have actually read them all.
            Add(new LandmarkDef("amaranth", "The Shell Line", LandmarkForm.Seam,
                "A seam runs across the whole visible surface, hairline, filled with old mortar.",
                "The mortar is patched over and over, in a hundred different hands, none recent.",
                "This is not the crack. This is one that somebody closed, a very long time ago.")
                .WithCoda(
                "You have read the others now. A post, a bell, a well, nine hundred stacked cairns.",
                "Every one of them is somebody writing down the year a thing started going wrong.",
                "Not one records the mending. Nobody ever thought that part was worth the stone.",
                "Somebody closed this seam and left no name. It held. That is the whole of it."));
        }
    }
}
