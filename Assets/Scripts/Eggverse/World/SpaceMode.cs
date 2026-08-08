using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Eggverse
{
    /// <summary>The star map: Teo drifts between planets and picks one to land on.</summary>
    public class SpaceMode : MonoBehaviour
    {
        class PlanetView
        {
            public PlanetDef Def;
            public Transform Root;
            public SpriteRenderer Body;
            public SpriteRenderer Halo;
            public Text Label;
            public RectTransform LabelRect;
        }

        GameDirector dir;
        readonly List<PlanetView> views = new List<PlanetView>();
        Transform root;
        Canvas labelCanvas;
        RectTransform labelCanvasRect;
        PlanetView inRange;

        const float LandingMargin = 3.2f;

        /// <summary>How far under Amy's best a lead can be before the chart says so in red.</summary>
        public const int AmyComfortableGap = 3;

        // ---- the drift ----
        //
        // Amy has been pulling warmth off every nest in the sector for eleven years to hold the
        // first egg shut. That is the whole plot, and until now none of it was visible: space
        // was a black field you crossed to get somewhere. It is a thing you can watch happening
        // from the first minute, if you look - faint motes, all of them going one way, and the
        // one way is Amaranth.
        //
        // Nobody remarks on it. Amy explains it at the end, and after that it stops, which is
        // the only announcement it gets.
        public const int DriftCount = 120;
        public const float DriftSpeed = 1.35f;
        // A field carried around the player, not a disc around Amaranth. The disc version left
        // Voltacrest - 220 units out - with no drift at all, which the checks caught: a player
        // could spend the whole Long Drift never seeing the one thing the plot is made of.
        // The camera shows 30 units tall, so a 46-unit field is comfortably wider than the view
        // and 90 motes in it stay sparse.
        public const float DriftFieldRadius = 46f;
        readonly List<Transform> drift = new List<Transform>();
        readonly List<float> driftRate = new List<float>();
        Vector2 driftTarget;

        public void Build(GameDirector director)
        {
            dir = director;
            root = new GameObject("Space").transform;
            root.SetParent(transform, false);

            labelCanvas = UIKit.CreateCanvas("SpaceLabels", 5, transform);
            labelCanvasRect = (RectTransform)labelCanvas.transform;

            BuildStarfield();
            BuildDrift();

            var planets = PlanetDatabase.All;
            for (int i = 0; i < planets.Count; i++) BuildPlanet(planets[i]);
        }

        // Depths of sky. A layer parked at `depth` drifts at (1 - depth) of the camera's speed,
        // so the far stars barely move and the near ones sweep past.
        //
        // Each layer is a *tile* that wraps around the camera rather than a field spanning the
        // whole galaxy. Spreading a few hundred stars across the full ~130,000 square units of
        // map put roughly six of them on screen at a time, which made space read as empty and
        // made the parallax invisible. Tiling gives a dense sky from the same object count.
        readonly List<Transform> parallaxLayers = new List<Transform>();
        readonly List<float> parallaxDepths = new List<float>();
        readonly List<float> parallaxTiles = new List<float>();

        /// <summary>
        /// Motes of warmth crossing the sector, all of them toward Amaranth. Ninety of them, on
        /// top of a starfield that already carries 1410 sprites, so they are cheap: one sprite
        /// each, moved on the CPU, recycled rather than respawned.
        /// </summary>
        void BuildDrift()
        {
            var amaranth = PlanetDatabase.Get("amaranth");
            driftTarget = amaranth != null ? amaranth.SpacePosition : Vector2.zero;

            Sprite mote = ProcArt.Star();
            var rng = new System.Random(0x0D71F7);
            var layerRoot = new GameObject("Drift").transform;
            layerRoot.SetParent(root, false);

            for (int i = 0; i < DriftCount; i++)
            {
                var go = new GameObject("mote");
                go.transform.SetParent(layerRoot, false);
                go.transform.localPosition = RandomDriftStart(rng, Vector2.zero);
                go.transform.localScale = Vector3.one * Mathf.Lerp(0.14f, 0.34f, (float)rng.NextDouble());

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = mote;
                sr.material = ProcArt.SpriteMaterial;
                // Warm against a sky of cool white stars, which is what makes them a separate
                // thing rather than more sky. At 0.10-0.30 alpha they were invisible even in a
                // render with the exposure of a screenshot - "subtle" had turned into "absent",
                // and a detail nobody can see is one that is not there.
                sr.color = new Color(1f, 0.84f, 0.58f, Mathf.Lerp(0.34f, 0.68f, (float)rng.NextDouble()));
                sr.sortingOrder = -40;

                drift.Add(go.transform);
                driftRate.Add(Mathf.Lerp(0.55f, 1.45f, (float)rng.NextDouble()));
            }
        }

        /// <summary>A point in the field, measured from wherever the player currently is.</summary>
        static Vector3 RandomDriftStart(System.Random rng, Vector2 around)
        {
            float a = (float)rng.NextDouble() * Mathf.PI * 2f;
            float r = DriftFieldRadius * Mathf.Sqrt((float)rng.NextDouble());
            return new Vector3(around.x + Mathf.Cos(a) * r, around.y + Mathf.Sin(a) * r, 0f);
        }

        /// <summary>
        /// True while the sector is still bleeding warmth. Once you and Amy are both holding the
        /// shell it stops, and nothing says so - the background simply goes still.
        /// </summary>
        bool DriftRunning => dir != null && dir.Story != null && !dir.Story.HasFlag("beat_amy");

        void TickDrift()
        {
            if (drift.Count == 0) return;

            bool running = DriftRunning;
            Vector2 here = dir.Teo.transform.position;

            for (int i = 0; i < drift.Count; i++)
            {
                var t = drift[i];
                Vector2 pos = t.localPosition;
                Vector2 toward = driftTarget - pos;
                float dist = toward.magnitude;

                var sr = t.GetComponent<SpriteRenderer>();
                if (!running)
                {
                    // Fade out where they stand rather than snapping off. The last few motes
                    // hang about for a moment after the fight, which is the right amount of
                    // ceremony for something nobody ever mentioned.
                    if (sr != null && sr.color.a > 0.001f)
                    {
                        var c = sr.color;
                        c.a = Mathf.MoveTowards(c.a, 0f, Time.deltaTime * 0.12f);
                        sr.color = c;
                    }
                    continue;
                }

                // Recycled when it arrives, and when the player has flown far enough that it
                // is behind them - so the field is always around you rather than somewhere you
                // used to be.
                if (dist < 2.5f || Vector2.Distance(pos, here) > DriftFieldRadius * 1.25f)
                {
                    t.localPosition = RandomDriftStart(driftRng, here);
                    continue;
                }

                t.localPosition = pos + toward / dist * (DriftSpeed * driftRate[i] * Time.deltaTime);
            }
        }

        readonly System.Random driftRng = new System.Random(0x5EE1);

        void BuildStarfield()
        {
            Sprite star = ProcArt.Star();
            var rng = new System.Random(20260807);

            // depth, tile size, count, size range, alpha range
            float[] depths = { 0.86f, 0.55f, 0.18f };
            float[] tiles = { 120f, 140f, 160f };   // differing periods hide the repeat
            // Sized for roughly 75 stars on screen at once: enough to feel like a sky and
            // to make the parallax readable while flying.
            int[] counts = { 700, 450, 260 };
            float[] sizeLow = { 0.05f, 0.09f, 0.14f };
            float[] sizeHigh = { 0.13f, 0.20f, 0.32f };
            float[] alphaLow = { 0.26f, 0.42f, 0.60f };
            float[] alphaHigh = { 0.55f, 0.80f, 1.00f };

            for (int layer = 0; layer < depths.Length; layer++)
            {
                var layerRoot = new GameObject("Stars_" + layer).transform;
                layerRoot.SetParent(root, false);
                parallaxLayers.Add(layerRoot);
                parallaxDepths.Add(depths[layer]);
                parallaxTiles.Add(tiles[layer]);

                float half = tiles[layer] * 0.5f;
                for (int i = 0; i < counts[layer]; i++)
                {
                    var go = new GameObject("star");
                    go.transform.SetParent(layerRoot, false);
                    go.transform.localPosition = new Vector3(
                        Mathf.Lerp(-half, half, (float)rng.NextDouble()),
                        Mathf.Lerp(-half, half, (float)rng.NextDouble()), 0f);
                    go.transform.localScale = Vector3.one * Mathf.Lerp(sizeLow[layer], sizeHigh[layer], (float)rng.NextDouble());

                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = star;
                    sr.material = ProcArt.SpriteMaterial;
                    // Distant stars run cooler, near ones warmer — cheap depth cue.
                    float warmth = layer / (float)(depths.Length - 1);
                    sr.color = new Color(Mathf.Lerp(0.82f, 1f, warmth), Mathf.Lerp(0.88f, 0.98f, warmth), 1f,
                                         Mathf.Lerp(alphaLow[layer], alphaHigh[layer], (float)rng.NextDouble()));
                    sr.sortingOrder = -100 + layer;
                }
            }

            // Wide, faint nebulae sit furthest back, on their own slower tile.
            var nebulaRoot = new GameObject("Nebulae").transform;
            nebulaRoot.SetParent(root, false);
            parallaxLayers.Add(nebulaRoot);
            parallaxDepths.Add(0.93f);
            parallaxTiles.Add(200f);

            Color[] nebulaTints =
            {
                new Color(0.35f, 0.22f, 0.55f, 1f),
                new Color(0.16f, 0.30f, 0.52f, 1f),
                new Color(0.45f, 0.20f, 0.30f, 1f),
            };
            // 14 clouds over a 260-unit tile at 0.15 alpha put *nothing* in frame most of the
            // time — the camera only sees 53x30 units. Denser tile, bigger clouds, and enough
            // alpha to actually tint the black.
            for (int i = 0; i < 18; i++)
            {
                Color tint = nebulaTints[i % nebulaTints.Length];
                var go = new GameObject("nebula");
                go.transform.SetParent(nebulaRoot, false);
                go.transform.localPosition = new Vector3(
                    Mathf.Lerp(-100f, 100f, (float)rng.NextDouble()),
                    Mathf.Lerp(-100f, 100f, (float)rng.NextDouble()), 0f);
                go.transform.localScale = Vector3.one * Mathf.Lerp(26f, 58f, (float)rng.NextDouble());
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ProcArt.Disc("nebula" + i, tint, new Color(tint.r, tint.g, tint.b, 0f), 1.5f, 128, 64f);
                sr.material = ProcArt.SpriteMaterial;
                sr.color = new Color(1f, 1f, 1f, 0.24f);
                sr.sortingOrder = -105;
            }
        }

        void LateUpdate()
        {
            if (dir == null || dir.Cam == null || root == null || !root.gameObject.activeSelf) return;
            Vector3 cam = dir.Cam.transform.position;

            for (int i = 0; i < parallaxLayers.Count; i++)
            {
                float depth = parallaxDepths[i];
                float tile = parallaxTiles[i];

                // Apparent drift is cam*(1-depth); snapping that to whole tiles keeps the
                // field centred on the camera forever without any visible jump.
                float driftX = cam.x * (1f - depth), driftY = cam.y * (1f - depth);
                float wrapX = Mathf.Round(driftX / tile) * tile;
                float wrapY = Mathf.Round(driftY / tile) * tile;

                parallaxLayers[i].position = new Vector3(cam.x * depth + wrapX, cam.y * depth + wrapY, 0f);
            }
        }

        void BuildPlanet(PlanetDef def)
        {
            var view = new PlanetView { Def = def };

            var go = new GameObject("Planet_" + def.Id);
            go.transform.SetParent(root, false);
            go.transform.position = def.SpacePosition;
            view.Root = go.transform;

            var bodyGo = new GameObject("Body");
            bodyGo.transform.SetParent(go.transform, false);
            view.Body = bodyGo.AddComponent<SpriteRenderer>();
            view.Body.sprite = ProcArt.Planet(def);
            view.Body.material = ProcArt.SpriteMaterial;
            view.Body.sortingOrder = 0;
            // The planet sprite reserves room for its glow, so scale from the body radius, not the sprite.
            float planetSpriteWorld = view.Body.sprite.rect.width / view.Body.sprite.pixelsPerUnit;
            float bodyRadius = planetSpriteWorld / (2f * 1.32f);
            bodyGo.transform.localScale = Vector3.one * (def.SpaceRadius / bodyRadius);

            var haloGo = new GameObject("Halo");
            haloGo.transform.SetParent(go.transform, false);
            view.Halo = haloGo.AddComponent<SpriteRenderer>();
            view.Halo.sprite = ProcArt.Ring("landing", Color.white, 0.035f);
            view.Halo.material = ProcArt.SpriteMaterial;
            view.Halo.sortingOrder = 1;
            view.Halo.color = new Color(1f, 1f, 1f, 0f);
            float haloDiameter = (def.SpaceRadius + LandingMargin) * 2f;
            float haloSpriteWorld = view.Halo.sprite.rect.width / view.Halo.sprite.pixelsPerUnit;
            haloGo.transform.localScale = Vector3.one * (haloDiameter / haloSpriteWorld);

            // Anchored to the canvas centre so ScreenPointToLocalPointInRectangle results drop straight in.
            var labelRt = UIKit.Node(labelCanvas.transform, "Label_" + def.Id);
            UIKit.Place(labelRt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420f, 90f));
            var text = UIKit.Label(labelRt, "Text", def.Name, 26, UIKit.Ink, TextAnchor.UpperCenter, FontStyle.Bold);
            UIKit.Stretch(text.rectTransform, 0f, 0f, 0f, 0f);
            view.Label = text;
            view.LabelRect = labelRt;

            views.Add(view);
        }

        public void SetActive(bool active)
        {
            if (root != null) root.gameObject.SetActive(active);
            if (labelCanvas != null) labelCanvas.gameObject.SetActive(active);
            if (!active) inRange = null;
        }

        /// <summary>A good place to drop Teo when he lifts off from a planet.</summary>
        public Vector2 DeparturePoint(PlanetDef from)
        {
            return from.SpacePosition + new Vector2(0f, -(from.SpaceRadius + LandingMargin + 2.5f));
        }

        void Update()
        {
            if (dir == null || dir.Mode != GameMode.Space) return;
            if (dir.OverlayOpen) return;

            TickDrift();

            Vector2 teo = dir.Teo.transform.position;
            PlanetView best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < views.Count; i++)
            {
                var v = views[i];
                float d = Vector2.Distance(teo, v.Def.SpacePosition);
                float range = v.Def.SpaceRadius + LandingMargin;

                float haloAlpha = d < range ? 0.9f : 0f;
                var c = v.Halo.color;
                v.Halo.color = new Color(
                    TypeChart.ColorOf(v.Def.Theme).r,
                    TypeChart.ColorOf(v.Def.Theme).g,
                    TypeChart.ColorOf(v.Def.Theme).b,
                    Mathf.Lerp(c.a, haloAlpha, 1f - Mathf.Exp(-8f * Time.deltaTime)));

                if (d < range && d < bestDist) { best = v; bestDist = d; }

                UpdateLabel(v, teo);
            }

            inRange = best;

            if (best == null)
            {
                dir.Hud.SetPrompt(null);
                return;
            }

            bool sealedWorld = !dir.Story.CanEnter(best.Def.Sector);
            if (sealedWorld)
            {
                string missing = dir.Story.CurrentBlockerText(dir.State);
                dir.Hud.SetPrompt("<b>" + best.Def.Name + "</b> is beyond your charted route." +
                                  (missing != null ? "  Still needed: " + missing + "." : "  " + dir.Story.Current.Objective));
            }
            else if (best.Def.IsBossWorld)
            {
                var lead = dir.State.Leader;
                dir.Hud.SetPrompt(BossPrompt(best.Def, lead != null ? lead.Level : 0));
            }
            else
            {
                // What it still owes you, in the moment you decide whether to go down. The
                // chart says this and so does the toast on landing; this is where the choice is
                // actually made.
                string owed = dir.State.Visited.Contains(best.Def.Id) ? dir.StillOwed(best.Def) : null;
                dir.Hud.SetPrompt(LandPrompt(best.Def, dir.State, owed));
            }

            if (EggInput.InteractPressed)
            {
                if (sealedWorld) dir.Hud.Toast("Your charts do not reach that far yet.");
                else dir.Land(best.Def);
            }
        }

        /// <summary>
        /// What the chart says before you commit to the boss world.
        ///
        /// Every other world tells you what level you are flying into; this one said "Amy is
        /// down there" and nothing else, and a player who has followed the story efficiently
        /// arrives here with most of the run's forced levelling still ahead of them.
        ///
        /// It quotes the wild range as well as Amy's team, because Amaranth's own eggs run to
        /// 26 against her 23 - the oldest things in the game live on the first egg - and a
        /// prompt that mentioned only her would understate the walk to her door.
        /// </summary>
        /// <summary>
        /// The prompt for going down to an ordinary world.
        ///
        /// The comment above the call says this is where the choice is actually made, and it was
        /// the one place that gave the level band and left the player to do the arithmetic. The
        /// chart says where you stand; the descent prompt says it in front of Amy; approaching
        /// any other world said nothing.
        ///
        /// Only when it is worth saying. This line is already near the width of its panel, and a
        /// warning that appears every time is not a warning - so it speaks when the lead is
        /// under everything down there, which is the case that changes the decision, and stays
        /// quiet otherwise.
        /// </summary>
        public static string LandPrompt(PlanetDef world, GameState state, string owed)
        {
            var lead = state.Leader;
            bool outmatched = lead != null &&
                              lead.Level + GalaxyMapView.ComfortGap < world.MinLevel;

            // The warning takes the owed clause's place rather than sitting after it. Partly
            // because all three together overrun the 1200px panel - the check said so
            // immediately - and partly because they are not both the decision: what a world
            // still owes your record does not matter while your lead cannot survive it.
            return "Press <b>E</b> to land on <b>" + world.Name + "</b>  ·  Lv " +
                   world.MinLevel + "-" + world.MaxLevel +
                   (outmatched
                       ? "  ·  <color=#E55555>your lead is Lv " + lead.Level + "</color>"
                       : owed != null ? "  ·  " + owed : "");
        }

        public static string BossPrompt(PlanetDef world, int leadLevel)
        {
            int amy = StoryDatabase.TopLevelOf("amy");
            string readiness = leadLevel <= 0 ? ""
                : leadLevel + AmyComfortableGap < amy
                    ? "  ·  <color=#E55555>your lead is Lv " + leadLevel + "</color>"
                    : "  ·  <color=#A8B2C4>your lead is Lv " + leadLevel + "</color>";

            return "Press <b>E</b> to descend to <b>" + world.Name + "</b>  ·  Lv " +
                   world.MinLevel + "-" + world.MaxLevel + "  ·  Amy fields Lv " + amy + readiness;
        }

        void UpdateLabel(PlanetView v, Vector2 teo)
        {
            Vector3 world = v.Def.SpacePosition + new Vector2(0f, -(v.Def.SpaceRadius + 1.6f));
            Vector3 screen = dir.Cam.WorldToScreenPoint(world);

            bool onScreen = screen.z > 0f &&
                            screen.x > -200f && screen.x < Screen.width + 200f &&
                            screen.y > -200f && screen.y < Screen.height + 200f;
            if (v.LabelRect.gameObject.activeSelf != onScreen) v.LabelRect.gameObject.SetActive(onScreen);
            if (!onScreen) return;

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(labelCanvasRect, screen, null, out local);
            v.LabelRect.anchoredPosition = local;

            bool sealedWorld = !dir.Story.CanEnter(v.Def.Sector);
            bool here = dir.State.CurrentPlanetId == v.Def.Id;
            bool charted = dir.State.Visited.Contains(v.Def.Id);
            string sub;
            if (sealedWorld) sub = "<color=#E55555>SEALED ROUTE</color>";
            else if (v.Def.IsBossWorld) sub = "<color=#FFC24D>AMY</color>";
            else if (!charted) sub = "<color=#8A90A2>UNCHARTED · Lv " + v.Def.MinLevel + "-" + v.Def.MaxLevel + "</color>";
            else
            {
                // The same hollow ring the chart puts on a world you have walked and not
                // finished. It meant something on one screen and nothing on the other, so a
                // player flying past had to open the chart to learn what they were looking at.
                int owed = dir.State.UnrecordedOn(v.Def);
                sub = "<color=#A8B2C4>Lv " + v.Def.MinLevel + "-" + v.Def.MaxLevel + " · " +
                      TypeChart.Name(v.Def.Theme) + "</color>" +
                      (owed > 0 ? "  <color=#FFC24D>\u25cb</color>" : "");
            }

            Color nameColor = here ? UIKit.Accent : sealedWorld ? UIKit.InkDim : UIKit.Ink;
            v.Label.text = "<color=#" + ColorUtility.ToHtmlStringRGB(nameColor) + "><b>" + v.Def.Name + "</b></color>\n<size=20>" + sub + "</size>";
        }
    }
}
