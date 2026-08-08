using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Eggverse
{
    /// <summary>The navigation chart: every known world, what lives there, and fast travel.</summary>
    public class GalaxyMapView : MonoBehaviour
    {
        class Marker
        {
            public PlanetDef Def;
            public RectTransform Root;
            public Image Dot;
            public Image Halo;
            public Text Label;
            public Vector2 ChartPos;
            public bool Reachable;
        }

        GameDirector dir;
        Canvas canvas;
        RectTransform chart;
        readonly List<Marker> markers = new List<Marker>();
        readonly List<Text> sectorLabels = new List<Text>();
        Image teoMarker;
        Text detailTitle, detailBody, detailCourse, footer, tally;
        int selected;

        public bool IsOpen { get { return canvas != null && canvas.gameObject.activeSelf; } }

        // The galaxy is taller than it is wide (194 x 214 world units, aspect 0.91), so the
        // chart always fits vertically. A 1180-wide panel therefore left 518px — 44% of its
        // width — as empty margin either side of the map. Sized to the content instead, and
        // the width that frees goes to the detail panel, which has lore to show.
        const float ChartWidth = 760f;
        const float ChartHeight = 820f;
        const float DetailWidth = 900f;

        public void Build(GameDirector director)
        {
            dir = director;
            canvas = UIKit.CreateCanvas("GalaxyMap", 22, transform);
            var root = (RectTransform)canvas.transform;

            var bg = UIKit.Panel(root, "Bg", new Color32(0x06, 0x07, 0x11, 0xFB));
            UIKit.Stretch(bg.rectTransform, 0, 0, 0, 0);

            var title = UIKit.Label(root, "Title", UiCopy.ChartTitle, 34, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -46f), new Vector2(900f, 44f));

            // The header row was a title and 900px of nothing. The chart is the screen a player
            // opens to decide where to go next, and it had no way of telling them how much of
            // the map they had actually seen.
            tally = UIKit.Label(root, "Tally", "", 22, UIKit.InkDim, TextAnchor.MiddleRight);
            UIKit.Place(tally.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                        new Vector2(-70f, -46f), new Vector2(900f, 44f));

            chart = UIKit.Node(root, "Chart");
            UIKit.Place(chart, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(70f, -20f), new Vector2(ChartWidth, ChartHeight));
            var chartBg = UIKit.Panel(chart, "ChartBg", new Color32(0x0B, 0x0D, 0x1C, 0xC0));
            UIKit.Stretch(chartBg.rectTransform, 0, 0, 0, 0);

            // uGUI does not clip children on its own. Without this, anything positioned near
            // the edge of the chart draws straight over the detail panel and the title.
            chart.gameObject.AddComponent<RectMask2D>();
            chartBg.transform.SetAsFirstSibling();

            BuildSectorLabels();
            BuildMarkers();

            teoMarker = UIKit.Picture(chart, "You", ProcArt.Teo());
            UIKit.Place(teoMarker.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));

            // Details column.
            var detail = UIKit.Node(root, "Detail");
            UIKit.Place(detail, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-70f, -20f), new Vector2(DetailWidth, ChartHeight));
            var detailBg = UIKit.Panel(detail, "Bg", new Color32(0x12, 0x14, 0x26, 0xE8));
            UIKit.Stretch(detailBg.rectTransform, 0, 0, 0, 0);

            detailTitle = UIKit.Label(detail, "Title", "", 30, UIKit.Ink, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Place(detailTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -26f), new Vector2(DetailWidth - 52f, 76f));

            detailBody = UIKit.Label(detail, "Body", "", 22, UIKit.Ink, TextAnchor.UpperLeft);
            // 560, not 660: the body ran to 48px off the panel floor, which is straight through
            // the course footer's rule at 86.
            UIKit.Place(detailBody.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -112f), new Vector2(DetailWidth - 52f, 560f));

            // Pinned to the bottom of the panel, above a rule, so it reads as the panel's own
            // footer and sits at the same height whatever the world above it says.
            var courseRule = UIKit.Panel(detail, "CourseRule", new Color32(0x2C, 0x32, 0x50, 0xFF));
            UIKit.Place(courseRule.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 86f), new Vector2(DetailWidth - 52f, 2f));

            detailCourse = UIKit.Label(detail, "Course", "", 22, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Place(detailCourse.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 48f), new Vector2(DetailWidth - 52f, 52f));

            footer = UIKit.Label(root, "Footer",
                UiCopy.ChartFooter,
                22, UIKit.InkDim, TextAnchor.MiddleCenter);
            UIKit.Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(1400f, 30f));

            canvas.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // layout
        // ------------------------------------------------------------------

        static void WorldBounds(out Vector2 min, out Vector2 max)
        {
            var all = PlanetDatabase.All;
            min = all[0].SpacePosition;
            max = all[0].SpacePosition;
            for (int i = 1; i < all.Count; i++)
            {
                min = Vector2.Min(min, all[i].SpacePosition);
                max = Vector2.Max(max, all[i].SpacePosition);
            }
            min -= Vector2.one * 22f;
            max += Vector2.one * 22f;
        }

        public static float ChartScale()
        {
            Vector2 min, max;
            WorldBounds(out min, out max);
            Vector2 span = max - min;
            // 120px of padding, not 90: the widest planet label overhangs its dot by ~51px,
            // and at the tighter margin "Cobblestead" ran past the edge of the chart.
            return Mathf.Min((ChartWidth - 120f) / Mathf.Max(1f, span.x),
                             (ChartHeight - 120f) / Mathf.Max(1f, span.y));
        }

        public static Vector2 WorldToChart(Vector2 world)
        {
            Vector2 min, max;
            WorldBounds(out min, out max);
            Vector2 centre = (min + max) * 0.5f;
            return (world - centre) * ChartScale();
        }

        /// <summary>
        /// Sector names, sat above the worlds they belong to.
        ///
        /// These used to be circles enclosing every world in a sector. Two things were wrong
        /// with that. The circles did not fit: all three overflowed the chart panel, the Long
        /// Drift by 186px, and uGUI does not clip children, so they drew over the rest of the
        /// screen. And a circle round the Belt's four worlds has to reach 93 units to hold
        /// them, while Amaranth sits only 66 units past their centre — so the final world was
        /// drawn inside Sector III, which is the one place the story says it is not. No
        /// centroid circle can express this layout; a label can.
        /// </summary>
        void BuildSectorLabels()
        {
            Sector[] sectors = { Sector.HatcheryReach, Sector.LongDrift, Sector.ShatteredBelt };
            var all = PlanetDatabase.All;
            float scale = ChartScale();

            for (int s = 0; s < sectors.Length; s++)
            {
                Sector sector = sectors[s];

                // Sit the label clear of the topmost world that belongs to this sector.
                float top = float.NegativeInfinity;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].Sector != sector) continue;
                    top = Mathf.Max(top, WorldToChart(all[i].SpacePosition).y + all[i].SpaceRadius * scale);
                }
                if (float.IsNegativeInfinity(top)) continue;

                float y = Mathf.Min(top + 34f, ChartHeight * 0.5f - 20f);
                string caption = PlanetDatabase.SectorName(sector).ToUpperInvariant();
                float x = PlaceSectorLabel(WorldToChart(PlanetDatabase.SectorCentre(sector)).x, y, caption, scale);

                var label = UIKit.Label(chart, "SectorLabel" + s, caption,
                                        19, new Color(1f, 1f, 1f, 0.28f), TextAnchor.MiddleCenter, FontStyle.Bold);
                UIKit.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(x, y), new Vector2(420f, 24f));
                sectorLabels.Add(label);
            }
        }

        /// <summary>
        /// Nudges a sector caption sideways until it clears every world's dot and name.
        /// Sat at the bare centroid, "THE LONG DRIFT" landed directly on Umbralux — the
        /// sectors interleave vertically, so a sector's own centre is not reliably clear.
        /// </summary>
        public static float PlaceSectorLabel(float preferredX, float y, string caption, float scale)
        {
            float half = CaptionHalfWidth(caption, 19);
            float limit = ChartWidth * 0.5f - half - 8f;
            var all = PlanetDatabase.All;

            for (int step = 0; step <= 14; step++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    float x = Mathf.Clamp(preferredX + sign * step * 30f, -limit, limit);
                    if (ClearOfWorlds(x, y, half, all, scale)) return x;
                    if (step == 0) break;      // no point testing +0 and -0
                }
            }
            return Mathf.Clamp(preferredX, -limit, limit);
        }

        public static float CaptionHalfWidth(string caption, int fontSize) =>
            caption.Length * fontSize * 0.52f * 0.5f;

        public static bool ClearOfWorlds(float x, float y, float half, IReadOnlyList<PlanetDef> all, float scale)
        {
            for (int i = 0; i < all.Count; i++)
            {
                Vector2 c = WorldToChart(all[i].SpacePosition);
                float dot = Mathf.Clamp(all[i].SpaceRadius * 2f * scale, 18f, 62f);

                // The dot itself, and the name sat underneath it.
                if (Boxes(x, y, half, 14f, c.x, c.y, dot * 0.5f, dot * 0.5f)) return false;
                float nameHalf = CaptionHalfWidth(all[i].Name, 18);
                float nameY = c.y - dot * 0.66f - 14f;
                if (Boxes(x, y, half, 14f, c.x, nameY, nameHalf, 12f)) return false;
            }
            return true;
        }

        static bool Boxes(float ax, float ay, float ahw, float ahh,
                          float bx, float by, float bhw, float bhh) =>
            Mathf.Abs(ax - bx) < ahw + bhw + 10f && Mathf.Abs(ay - by) < ahh + bhh + 6f;

        void BuildMarkers()
        {
            var all = PlanetDatabase.All;
            float scale = ChartScale();

            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                var m = new Marker { Def = def, ChartPos = WorldToChart(def.SpacePosition) };

                m.Root = UIKit.Node(chart, "Marker_" + def.Id);
                UIKit.Place(m.Root, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), m.ChartPos, new Vector2(120f, 120f));

                float dotSize = Mathf.Clamp(def.SpaceRadius * 2f * scale, 18f, 62f);

                m.Halo = UIKit.Picture(m.Root, "Halo", ProcArt.Ring("mapsel", Color.white, 0.05f));
                UIKit.Place(m.Halo.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                            new Vector2(dotSize + 18f, dotSize + 18f));
                m.Halo.color = new Color(1f, 1f, 1f, 0f);

                // Chart dots are tiny; a small bake keeps map construction cheap.
                m.Dot = UIKit.Picture(m.Root, "Dot", ProcArt.Planet(def, 96));
                UIKit.Place(m.Dot.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                            new Vector2(dotSize * 1.32f, dotSize * 1.32f));

                m.Label = UIKit.Label(m.Root, "Label", def.Name, 18, UIKit.Ink, TextAnchor.UpperCenter);
                UIKit.Place(m.Label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -dotSize * 0.66f - 4f),
                            new Vector2(240f, 44f));

                markers.Add(m);
            }
        }

        // ------------------------------------------------------------------
        // open / close
        // ------------------------------------------------------------------

        /// <summary>
        /// Where the player physically is right now. Landing sets CurrentPlanetId, but fast
        /// travel only puts you in orbit — so the chart has to distinguish the two, or it
        /// claims you are still standing on the world you just left.
        /// </summary>
        public enum Presence { Elsewhere, Landed, InOrbit }

        Presence PresenceAt(PlanetDef def)
        {
            if (dir.Mode == GameMode.Surface)
                return dir.CurrentPlanet != null && dir.CurrentPlanet.Id == def.Id ? Presence.Landed : Presence.Elsewhere;

            if (dir.Mode == GameMode.Space && dir.Teo != null)
            {
                float d = Vector2.Distance(dir.Teo.transform.position, def.SpacePosition);
                if (d <= def.SpaceRadius + 3.2f) return Presence.InOrbit;
            }
            return Presence.Elsewhere;
        }

        public void Open()
        {
            canvas.gameObject.SetActive(true);
            // Start on wherever the player currently is.
            selected = 0;
            for (int i = 0; i < markers.Count; i++)
                if (dir.CurrentPlanet != null && markers[i].Def.Id == dir.CurrentPlanet.Id) { selected = i; break; }
            Refresh();
        }

        public void Close()
        {
            canvas.gameObject.SetActive(false);
        }

        void Refresh()
        {
            var state = dir.State;
            var story = dir.Story;

            for (int i = 0; i < markers.Count; i++)
            {
                var m = markers[i];
                bool sectorOpen = story.CanEnter(m.Def.Sector);
                bool visited = state.Visited.Contains(m.Def.Id);
                m.Reachable = sectorOpen && visited;

                Color dotTint;
                if (!sectorOpen) dotTint = new Color(0.30f, 0.30f, 0.38f, 0.55f);
                else if (!visited) dotTint = new Color(0.70f, 0.70f, 0.78f, 0.75f);
                else dotTint = Color.white;
                m.Dot.color = dotTint;

                string suffix = MarkerSuffix(m.Def, state, story, PresenceAt(m.Def));

                bool isSelected = i == selected;
                m.Label.text = MarkerLabel(m.Def, suffix, sectorOpen, isSelected);

                // Amber, and a caret. The halo used to be the world's own theme colour at full
                // alpha - so "selected" was a slightly brighter blur of the colour already
                // there, and on a chart of pale worlds it read as almost nothing. Worse, it was
                // hue and only hue, on a screen whose entire job is picking one of seventeen
                // things. This game's rule everywhere else is that nothing is carried by colour
                // alone, and amber is what a cursor is: the pause caret, the collection caret,
                // the party leader's arrow.
                m.Halo.color = isSelected ? UIKit.Accent : new Color(1f, 1f, 1f, 0f);
            }

            // Where Teo actually is.
            Vector2 teoWorld = dir.Mode == GameMode.Space
                ? (Vector2)dir.Teo.transform.position
                : (dir.CurrentPlanet != null ? dir.CurrentPlanet.SpacePosition : Vector2.zero);
            teoMarker.rectTransform.anchoredPosition = WorldToChart(teoWorld) + new Vector2(0f, 34f);

            RefreshDetail();
        }

        /// <summary>
        /// Whether the pad on this world will do anything for you.
        ///
        /// Four stations in the game have gone cold - Yolkhaven, Brineholt, Mosswell, Vesper -
        /// and the chart never said so. That is where you heal and restock, so flying somewhere
        /// expecting to rest and finding dead stone is exactly what a navigation chart exists to
        /// prevent. It is also the plot: Amy has been pulling the warmth off them for eleven
        /// years, and after she is beaten they come back. Watching four worlds on your own chart
        /// warm up again is the only report the ending gives you of what you actually changed.
        ///
        /// Only for worlds you have stood on. That is the rule the cache and the landmark
        /// already follow here - nothing is announced before it is found.
        /// </summary>
        public static string StationLine(PlanetDef def, GameState state, StoryState story)
        {
            if (!state.Visited.Contains(def.Id)) return "";

            bool everCold = PlanetDatabase.StationCold(def.Id);
            bool amyBeaten = story != null && story.HasFlag("beat_amy");

            if (!everCold) return "<color=#A8B2C4>The nest station here runs warm.</color>";
            if (!PlanetDatabase.StationColdNow(def.Id, amyBeaten))
                return "<color=#5FD068>The nest station here is warm again.</color>";
            // Vesper. The ending card promises it stays dark a while longer, and a chart that
            // said otherwise would be the last thing the game says contradicted by the screen a
            // player opens straight afterwards.
            if (amyBeaten)
                return "<color=#E5A055>Still dark. Vess says she can wait.</color>";
            return "<color=#E55555>The nest station here has gone cold.</color>";
        }

        /// <summary>
        /// How far outside a world's level band you have to be before it is worth saying so.
        /// The same number the descent prompt uses for Amy - two readings of "comfortable" that
        /// disagree would be the chart and the prompt telling a player different things about
        /// the same decision.
        /// </summary>
        public const int ComfortGap = 3;

        /// <summary>
        /// Your lead against this world's wild eggs, in one line.
        ///
        /// The lead rather than the party average, because the lead is who walks into the first
        /// fight. Five bands, not two: "under" and "over" are the ones that change a decision,
        /// but a player who is exactly right wants to be told that too.
        /// </summary>
        public static string Readiness(PlanetDef def, GameState state)
        {
            var lead = state.Leader;
            if (lead == null) return "";
            int lv = lead.Level;

            if (lv + ComfortGap < def.MinLevel)
                return "<color=#E55555>Your lead is Lv " + lv + " — under everything here.</color>";
            if (lv < def.MinLevel)
                return "<color=#FFC24D>Your lead is Lv " + lv + ", a shade under the low end.</color>";
            if (lv <= def.MaxLevel)
                return "<color=#A8B2C4>Your lead is Lv " + lv + ", in among them.</color>";
            if (lv <= def.MaxLevel + ComfortGap)
                return "<color=#A8B2C4>Your lead is Lv " + lv + ", a little over.</color>";
            return "<color=#7A8090>Your lead is Lv " + lv + ". Nothing here will trouble you.</color>";
        }

        /// <summary>
        /// A marker's two lines. The caret is the non-chromatic half of "this is the one you
        /// have selected" - a halo in the world's own colour said it in hue and nothing else,
        /// which on this chart is not saying it.
        /// </summary>
        public static string MarkerLabel(PlanetDef def, string suffix, bool sectorOpen, bool selected)
        {
            string nameColor = sectorOpen ? "#F2F5FA" : "#5A6072";
            return (selected ? "<color=#FFC24D>\u25b8</color> " : "")
                 + "<color=" + nameColor + ">" + def.Name + "</color>"
                 + "\n<size=15>" + suffix + "</size>";
        }

        /// <summary>
        /// The line under a world's name on the chart. Static so the checks measure what the
        /// chart draws - the first version of this check built the string itself, which meant
        /// deleting the element from the real one changed nothing and the suite passed.
        /// </summary>
        public static string MarkerSuffix(PlanetDef def, GameState state, StoryState story, Presence presence)
        {
            if (!story.CanEnter(def.Sector)) return "<color=#5A6072>· sealed</color>";
            // The level range, as the space view has always shown it. A world's size and
            // distance are survey data; a player can read them from orbit and from a chart
            // alike. What you only learn by going is what lives there - so the element stays
            // behind, and the two screens stop disagreeing about what is known.
            if (!state.Visited.Contains(def.Id))
                return "<color=#8A90A2>· uncharted  ·  Lv " + def.MinLevel + "-" + def.MaxLevel + "</color>";
            if (presence == Presence.Landed) return "<color=#FFC24D>· you are here</color>";
            if (presence == Presence.InOrbit) return "<color=#FFC24D>· in orbit</color>";

            // A world you have walked and not finished. The hollow ring is the same mark the
            // field record uses for a species you have not recorded, so it means the same thing
            // in both places - and it appears only on worlds you have been to, because it is
            // your own record talking rather than the game pointing.
            //
            // The element too. Every dot on this chart is coloured by its world's element and
            // the label never said which - the last place in the game carrying type by hue
            // alone, after the field record's own list.
            int owed = state.UnrecordedOn(def);
            return "<color=#A8B2C4>Lv " + def.MinLevel + "-" + def.MaxLevel + "</color>" +
                   "  <color=#7A8090>" + TypeChart.Abbrev(def.Theme) + "</color>" +
                   (owed > 0 ? "  <color=#FFC24D>\u25cb</color>" : "");
        }

        /// <summary>
        /// How much of the map you have actually seen. Inscriptions stay off it until you have
        /// found one, the same rule the chart's per-world lines follow - a "0 of 17" on a screen
        /// you open in the first ten minutes is a checklist handed to somebody who has not been
        /// told there is anything to check.
        /// </summary>
        public static string Tally(GameState state)
        {
            int charted = 0;
            foreach (var w in PlanetDatabase.All) if (state.Visited.Contains(w.Id)) charted++;

            string text = charted + " of " + PlanetDatabase.All.Count + " worlds charted";
            if (state.Landmarks.Count > 0)
                text += "  ·  " + state.Landmarks.Count + " of " + LandmarkDatabase.Count +
                        " inscriptions read";
            return text;
        }

        void RefreshDetail()
        {
            tally.text = Tally(dir.State);

            var m = markers[selected];
            var state = dir.State;
            var story = dir.Story;
            bool sectorOpen = story.CanEnter(m.Def.Sector);

            detailTitle.text = m.Def.Name + "\n<size=19><color=#A8B2C4>" +
                               PlanetDatabase.SectorName(m.Def.Sector) + " · " + TypeChart.Name(m.Def.Theme) + "</color></size>";
            detailTitle.color = sectorOpen ? UIKit.Ink : UIKit.InkDim;

            detailBody.text = DetailBody(m.Def, state, story, dir.CurrentPlanet, PresenceAt(m.Def));
            detailCourse.text = CourseLine(m.Def, state, story, PresenceAt(m.Def));
        }

        /// <summary>
        /// The chart's detail column, as text. Split out of RefreshDetail so the checks can
        /// measure what the panel will actually hold rather than a reconstruction of it - the
        /// panel is 660px at font 22 and everything below competes for the same 25 lines.
        /// </summary>
        public static string DetailBody(PlanetDef def, GameState state, StoryState story,
                                        PlanetDef from, Presence presence)
        {
            bool sectorOpen = story.CanEnter(def.Sector);
            bool visited = state.Visited.Contains(def.Id);

            var sb = new System.Text.StringBuilder();

            if (!sectorOpen)
            {
                sb.Append("<color=#E55555>ROUTE SEALED</color>\n\n");
                sb.Append(story.SectorBlockerText(def.Sector, state)).Append("\n\n");
            }
            else
            {
                sb.Append("<i><color=#A8B2C4>").Append(def.Tagline).Append("</color></i>\n\n");
                sb.Append("<b>Wild eggs</b>  <color=#A8B2C4>Lv ").Append(def.MinLevel).Append("-").Append(def.MaxLevel).Append("</color>\n");

                for (int i = 0; i < def.Spawns.Length; i++)
                {
                    var species = SpeciesDatabase.Get(def.Spawns[i].SpeciesId);
                    bool known = state.Seen.Contains(species.Id);
                    string hex = ColorUtility.ToHtmlStringRGB(TypeChart.ColorOf(species.Type));
                    string mark = state.Caught.Contains(species.Id) ? "<color=#5FD068>●</color>"
                                : known ? "<color=#FFC24D>◐</color>" : "<color=#3A4052>○</color>";
                    sb.Append("  ").Append(mark).Append(" <color=#").Append(hex).Append(">")
                      .Append(known ? species.Name : "? ? ?").Append("</color>\n");
                }

                // Where you stand against it. The panel said "Wild eggs Lv 14-18" and never
                // said what yours are - so the chart described seventeen worlds in detail and
                // answered none of the question a player actually opens it with, which is
                // whether this is a good place for them right now. The space prompt has told
                // you your lead's level in front of Amy since it was written; every other world
                // was left to arithmetic in the player's head.
                string ready = Readiness(def, state);
                if (ready.Length > 0) sb.Append('\n').Append(ready).Append('\n');

                string pad = StationLine(def, state, story);
                if (pad.Length > 0) sb.Append(pad).Append('\n');

                // What it costs and what you have already done here - separated from the two
                // lines above, which are about you rather than about the world.
                if (ready.Length > 0 || pad.Length > 0) sb.Append('\n');

                // How far it is, from wherever you are standing. The chart is where a course is
                // chosen and it has never said what choosing one costs.
                if (from != null && from.Id != def.Id)
                {
                    float gap = Vector2.Distance(from.SpacePosition, def.SpacePosition)
                                - from.SpaceRadius - def.SpaceRadius;
                    sb.Append("<color=#A8B2C4>").Append(Mathf.RoundToInt(Mathf.Max(0f, gap)))
                      .Append(" units from ").Append(from.Name)
                      .Append("  ·  about ").Append(TeoController.FlightSeconds(Mathf.Max(0f, gap)).ToString("0.0"))
                      .Append("s of flying</color>\n");
                }

                // A recovered cache, as a record of what you have done here. Not announced
                // before you find it - the whole point is that it is buried.
                if (PlanetDatabase.HasCache(def.Id) && state.Caches.Contains(def.Id))
                    sb.Append("<color=#5FD068>Supply cache recovered.</color>\n");

                // Landmarks follow the cache's rule: nothing is announced before it is found.
                // The one exception is a world you have already walked - once you have read a
                // single inscription anywhere you know these exist, and the chart's job is to
                // record where you have been, not to point at where you have not.
                var lm = LandmarkDatabase.For(def.Id);
                if (lm != null && state.Landmarks.Contains(def.Id))
                    sb.Append("<color=#5FD068>Read: </color><color=#A8B2C4>").Append(lm.Name).Append("</color>\n");
                else if (lm != null && visited && state.Landmarks.Count > 0)
                    sb.Append("<color=#7A8090>Something stands out past the fields.</color>\n");

                if (def.IsBossWorld) sb.Append("\n<color=#FFC24D>Amy is here.</color>\n");
            }

            return sb.ToString().TrimEnd('\n');
        }

        /// <summary>
        /// What you can do about the world you have selected. It used to trail the detail text,
        /// so it landed at a different height on every planet - halfway up a 660px panel with
        /// 380px of nothing under it. It is the one line here that is an instruction rather than
        /// a description, and it belongs at the bottom where instructions live.
        /// </summary>
        public static string CourseLine(PlanetDef def, GameState state, StoryState story, Presence presence)
        {
            if (presence == Presence.Landed) return "<color=#FFC24D>You are here.</color>";
            if (presence == Presence.InOrbit) return "<color=#FFC24D>You are in orbit. Land with E.</color>";
            if (!story.CanEnter(def.Sector)) return "<color=#5A6072>No course can be plotted.</color>";
            if (!state.Visited.Contains(def.Id))
                return "<color=#8A90A2>Uncharted. Fly there once to add it to the chart.</color>";
            return "<color=#5FD068>Press Enter to set course.</color>";
        }

        // ------------------------------------------------------------------
        // input
        // ------------------------------------------------------------------

        void Update()
        {
            if (!IsOpen) return;

            Vector2 dirVec = Vector2.zero;
            if (EggInput.UpPressed) dirVec = Vector2.up;
            else if (EggInput.DownPressed) dirVec = Vector2.down;
            else if (EggInput.LeftPressed) dirVec = Vector2.left;
            else if (EggInput.RightPressed) dirVec = Vector2.right;

            if (dirVec != Vector2.zero)
            {
                int next = NearestInDirection(selected, dirVec);
                if (next != selected) { selected = next; Refresh(); }
            }

            if (EggInput.ConfirmPressed) TryTravel();
        }

        /// <summary>Picks the marker that best matches the pressed direction, favouring close and well-aligned ones.</summary>
        int NearestInDirection(int from, Vector2 direction)
        {
            Vector2 origin = markers[from].ChartPos;
            int best = from;
            float bestScore = float.MaxValue;

            for (int i = 0; i < markers.Count; i++)
            {
                if (i == from) continue;
                Vector2 delta = markers[i].ChartPos - origin;
                float along = Vector2.Dot(delta, direction);
                if (along <= 1f) continue;                       // behind us
                float lateral = Mathf.Abs(Vector2.Dot(delta, new Vector2(-direction.y, direction.x)));
                float score = along + lateral * 2.2f;            // punish sideways drift
                if (score < bestScore) { bestScore = score; best = i; }
            }
            return best;
        }

        void TryTravel()
        {
            var m = markers[selected];
            if (PresenceAt(m.Def) != Presence.Elsewhere)
            {
                dir.Hud.Toast(UiCopy.AlreadyThere);
                return;
            }
            if (!dir.Story.CanEnter(m.Def.Sector))
            {
                dir.Hud.Toast(UiCopy.RouteSealed);
                return;
            }
            if (!dir.State.Visited.Contains(m.Def.Id))
            {
                dir.Hud.Toast(m.Def.Name + " is uncharted. Fly there once to add it to the chart.");
                return;
            }

            Close();
            dir.FastTravel(m.Def);
        }
    }
}
