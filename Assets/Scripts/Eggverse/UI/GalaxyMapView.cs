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
        Text detailTitle, detailBody, footer;
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

            var title = UIKit.Label(root, "Title", "NAVIGATION CHART", 34, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(70f, -46f), new Vector2(900f, 44f));

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
            UIKit.Place(detailBody.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -112f), new Vector2(DetailWidth - 52f, 660f));

            footer = UIKit.Label(root, "Footer",
                "Arrows select  ·  Enter to set course  ·  M or Esc to close",
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
        enum Presence { Elsewhere, Landed, InOrbit }

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

                string suffix;
                if (!sectorOpen) suffix = "<color=#5A6072>· sealed</color>";
                else if (!visited) suffix = "<color=#8A90A2>· uncharted</color>";
                else if (PresenceAt(m.Def) == Presence.Landed) suffix = "<color=#FFC24D>· you are here</color>";
                else if (PresenceAt(m.Def) == Presence.InOrbit) suffix = "<color=#FFC24D>· in orbit</color>";
                else suffix = "<color=#A8B2C4>Lv " + m.Def.MinLevel + "-" + m.Def.MaxLevel + "</color>";

                string nameColor = sectorOpen ? "#F2F5FA" : "#5A6072";
                m.Label.text = "<color=" + nameColor + ">" + m.Def.Name + "</color>\n<size=15>" + suffix + "</size>";

                bool isSelected = i == selected;
                m.Halo.color = isSelected
                    ? new Color(TypeChart.ColorOf(m.Def.Theme).r, TypeChart.ColorOf(m.Def.Theme).g, TypeChart.ColorOf(m.Def.Theme).b, 0.95f)
                    : new Color(1f, 1f, 1f, 0f);
            }

            // Where Teo actually is.
            Vector2 teoWorld = dir.Mode == GameMode.Space
                ? (Vector2)dir.Teo.transform.position
                : (dir.CurrentPlanet != null ? dir.CurrentPlanet.SpacePosition : Vector2.zero);
            teoMarker.rectTransform.anchoredPosition = WorldToChart(teoWorld) + new Vector2(0f, 34f);

            RefreshDetail();
        }

        void RefreshDetail()
        {
            var m = markers[selected];
            var state = dir.State;
            var story = dir.Story;
            bool sectorOpen = story.CanEnter(m.Def.Sector);
            bool visited = state.Visited.Contains(m.Def.Id);

            detailTitle.text = m.Def.Name + "\n<size=19><color=#A8B2C4>" +
                               PlanetDatabase.SectorName(m.Def.Sector) + " · " + TypeChart.Name(m.Def.Theme) + "</color></size>";
            detailTitle.color = sectorOpen ? UIKit.Ink : UIKit.InkDim;

            var sb = new System.Text.StringBuilder();

            if (!sectorOpen)
            {
                sb.Append("<color=#E55555>ROUTE SEALED</color>\n\n");
                sb.Append(story.SectorBlockerText(m.Def.Sector)).Append("\n\n");
            }
            else
            {
                sb.Append("<i><color=#A8B2C4>").Append(m.Def.Tagline).Append("</color></i>\n\n");
                sb.Append("<b>Wild eggs</b>  <color=#A8B2C4>Lv ").Append(m.Def.MinLevel).Append("-").Append(m.Def.MaxLevel).Append("</color>\n");

                for (int i = 0; i < m.Def.Spawns.Length; i++)
                {
                    var species = SpeciesDatabase.Get(m.Def.Spawns[i].SpeciesId);
                    bool known = state.Seen.Contains(species.Id);
                    string hex = ColorUtility.ToHtmlStringRGB(TypeChart.ColorOf(species.Type));
                    string mark = state.Caught.Contains(species.Id) ? "<color=#5FD068>●</color>"
                                : known ? "<color=#FFC24D>◐</color>" : "<color=#3A4052>○</color>";
                    sb.Append("  ").Append(mark).Append(" <color=#").Append(hex).Append(">")
                      .Append(known ? species.Name : "? ? ?").Append("</color>\n");
                }

                sb.Append("\n");
                if (m.Def.IsBossWorld) sb.Append("<color=#FFC24D>Amy is here.</color>\n\n");
            }

            var presence = PresenceAt(m.Def);
            if (presence == Presence.Landed) sb.Append("<color=#FFC24D>You are here.</color>");
            else if (presence == Presence.InOrbit) sb.Append("<color=#FFC24D>You are in orbit. Land with E.</color>");
            else if (!sectorOpen) sb.Append("<color=#5A6072>No course can be plotted.</color>");
            else if (!visited) sb.Append("<color=#8A90A2>Uncharted. Fly there once to add it to the chart.</color>");
            else sb.Append("<color=#5FD068>Press Enter to set course.</color>");

            detailBody.text = sb.ToString();
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
                dir.Hud.Toast("You are already there.");
                return;
            }
            if (!dir.Story.CanEnter(m.Def.Sector))
            {
                dir.Hud.Toast("That route is sealed.");
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
