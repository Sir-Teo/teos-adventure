using System;
using Eggverse;

/// The six landmark silhouettes, each on the world it actually stands on, at the scale the player
/// sees them, with Teo below for size.
///
/// They were one grey disc each. Nothing in the assertions could have told me that a bell, a
/// ship's bow and nine hundred cairns all rendered as the same rock — only looking could.
///
/// This used to draw the forms itself on flat colour swatches, which meant two mock
/// implementations of the same shapes: this one, and the generic stone buried in the world
/// render. They disagreed within a day. Now it composites real Surface renders, so the forms are
/// judged against real terrain, real decor and the real contrast rule — and there is one drawing.
static class Landmarks
{
    const int Tile = 280;

    static readonly (string world, LandmarkForm form)[] Cells =
    {
        ("yolkhaven",  LandmarkForm.Post),
        ("cinderoost", LandmarkForm.Frame),
        ("brineholt",  LandmarkForm.Stones),
        ("mosswell",   LandmarkForm.Hollow),
        ("tidewrack",  LandmarkForm.Hulk),
        ("amaranth",   LandmarkForm.Seam),
    };

    public static (Col[] px, int w, int h) Render()
    {
        int w = Tile * 3, h = Tile * 2;
        var px = new Col[w * h];

        for (int i = 0; i < Cells.Length; i++)
        {
            var def = PlanetDatabase.Get(Cells[i].world);

            // 22 world units across a 280px tile: close enough to read the shape, wide enough to
            // see what it is standing on.
            var tile = Surface.Render(Tile, Program.ToHexPublic(def.Ocean), Program.ToHexPublic(def.Land),
                                      Program.ToHexPublic(def.Atmosphere), def.Theme.ToString(),
                                      def.Seed, "Scattered", 22f, def.Id, true);

            int x0 = (i % 3) * Tile, y0 = (i / 3) * Tile;
            for (int y = 0; y < Tile; y++)
                for (int x = 0; x < Tile; x++)
                    px[(y0 + y) * w + x0 + x] = tile[y * Tile + x];
        }
        return (px, w, h);
    }

    /// Every form has to appear in the sheet, or a shape ships without anyone having looked at it.
    public static string Coverage()
    {
        int missing = 0;
        foreach (LandmarkForm f in Enum.GetValues(typeof(LandmarkForm)))
        {
            bool found = false;
            foreach (var cell in Cells) if (cell.form == f) found = true;
            if (!found) missing++;
        }
        return missing == 0
            ? $"  landmark forms rendered: {Cells.Length}, every form covered"
            : $"  landmark forms rendered: {Cells.Length}, {missing} FORM(S) NEVER LOOKED AT";
    }
}
