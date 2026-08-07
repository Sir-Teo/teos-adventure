using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Eggverse
{
    /// <summary>Overworld HUD: party strip, objective tracker, prompts, toasts, collection screen.</summary>
    public class HudView : MonoBehaviour
    {
        class PartySlot
        {
            public RectTransform Root;
            public Image Dot;
            public Text Label;
            public BarWidget Hp;
        }

        GameDirector dir;
        Canvas canvas;

        readonly List<PartySlot> slots = new List<PartySlot>();
        Text objectiveText, cartonText, promptText, toastText, planetText;
        RectTransform promptPanel, toastPanel, partyStrip, objectivePanel;
        RectTransform titlePanel, collectionPanel, victoryPanel;
        Text collectionBody, collectionDex, dexDetail, dexLore, titleHint, titleSaveLine;
        Image dexPortrait;
        int dexCursor;

        readonly Queue<string> toastQueue = new Queue<string>();
        float toastTimer;

        public bool CollectionOpen { get { return collectionPanel != null && collectionPanel.gameObject.activeSelf; } }

        public void Build(GameDirector director)
        {
            dir = director;
            canvas = UIKit.CreateCanvas("HUD", 10, transform);
            var root = (RectTransform)canvas.transform;

            BuildPartyStrip(root);
            BuildObjective(root);
            BuildPrompt(root);
            BuildToast(root);
            BuildCollection(root);
            BuildTitle(root);
            BuildVictory(root);

            Rebind(dir.State);
        }

        GameState bound;

        /// <summary>Points the HUD at a (possibly brand new) game state, e.g. after loading a save.</summary>
        public void Rebind(GameState state)
        {
            if (bound != null) bound.Changed -= Refresh;
            bound = state;
            if (bound != null) bound.Changed += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (bound != null) bound.Changed -= Refresh;
        }

        // ------------------------------------------------------------------
        // construction
        // ------------------------------------------------------------------

        void BuildPartyStrip(RectTransform root)
        {
            var panel = UIKit.Node(root, "PartyStrip");
            partyStrip = panel;
            UIKit.Place(panel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -28f), new Vector2(360f, 380f));

            var bg = UIKit.Panel(panel, "Bg", UIKit.PanelDark);
            UIKit.Stretch(bg.rectTransform, 0, 0, 0, 0);

            var title = UIKit.Label(panel, "Title", "PARTY", 20, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -12f), new Vector2(200f, 24f));

            for (int i = 0; i < GameState.PartySize; i++)
            {
                var slotRoot = UIKit.Node(panel, "Slot" + i);
                UIKit.Place(slotRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -44f - i * 54f), new Vector2(332f, 50f));

                var dot = UIKit.Panel(slotRoot, "Dot", Color.gray);
                UIKit.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(6f, 0f), new Vector2(10f, 34f));

                var label = UIKit.Label(slotRoot, "Label", "", 18, UIKit.Ink, TextAnchor.UpperLeft);
                UIKit.Place(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -2f), new Vector2(300f, 24f));

                var hp = UIKit.Bar(slotRoot, "Hp", new Color32(0x0C, 0x0E, 0x18, 0xFF), UIKit.Good);
                UIKit.Place(hp.Background.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 6f), new Vector2(296f, 8f));

                slots.Add(new PartySlot { Root = slotRoot, Dot = dot, Label = label, Hp = hp });
            }
        }

        void BuildObjective(RectTransform root)
        {
            var panel = UIKit.Node(root, "Objective");
            objectivePanel = panel;
            UIKit.Place(panel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(560f, 268f));

            var bg = UIKit.Panel(panel, "Bg", UIKit.PanelDark);
            UIKit.Stretch(bg.rectTransform, 0, 0, 0, 0);

            planetText = UIKit.Label(panel, "Planet", "", 24, UIKit.Accent, TextAnchor.UpperLeft, FontStyle.Bold);
            UIKit.Place(planetText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -14f), new Vector2(480f, 30f));

            objectiveText = UIKit.Label(panel, "Objective", "", 20, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Place(objectiveText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -50f), new Vector2(524f, 150f));

            // Two lines: supplies on top, collection below. One line at 524px could not hold
            // both without abbreviating the labels into guesswork.
            cartonText = UIKit.Label(panel, "Supplies", "", 19, UIKit.InkDim, TextAnchor.LowerLeft);
            UIKit.Place(cartonText.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 12f), new Vector2(524f, 50f));
        }

        void BuildPrompt(RectTransform root)
        {
            promptPanel = UIKit.Node(root, "Prompt");
            UIKit.Place(promptPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1200f, 60f));
            var bg = UIKit.Panel(promptPanel, "Bg", UIKit.PanelDark);
            UIKit.Stretch(bg.rectTransform, 0, 0, 0, 0);
            promptText = UIKit.Label(promptPanel, "Text", "", 24, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Stretch(promptText.rectTransform, 16, 4, 16, 4);
            promptPanel.gameObject.SetActive(false);
        }

        void BuildToast(RectTransform root)
        {
            toastPanel = UIKit.Node(root, "Toast");
            // Sits below the objective panel, not level with it. At the top of the screen this
            // 1000px bar ran 128px into that panel and one opaque box drew over the other.
            // Narrowing instead would clip the longest toast, which is 58 characters.
            UIKit.Place(toastPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -308f), new Vector2(1000f, 56f));
            var bg = UIKit.Panel(toastPanel, "Bg", new Color32(0x1E, 0x22, 0x38, 0xF0));
            UIKit.Stretch(bg.rectTransform, 0, 0, 0, 0);
            toastText = UIKit.Label(toastPanel, "Text", "", 24, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(toastText.rectTransform, 16, 4, 16, 4);
            toastPanel.gameObject.SetActive(false);
        }

        void BuildCollection(RectTransform root)
        {
            collectionPanel = UIKit.Node(root, "Collection");
            UIKit.Place(collectionPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1760f, 940f));
            var bg = UIKit.Panel(collectionPanel, "Bg", new Color32(0x0B, 0x0D, 0x18, 0xFA));
            UIKit.Stretch(bg.rectTransform, 0, 0, 0, 0);

            var title = UIKit.Label(collectionPanel, "Title", "YOUR COLLECTION", 34, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(1600f, 44f));

            // Three columns: your nest, the field record, and detail on whichever species
            // the dex cursor is sitting on.
            collectionBody = UIKit.Label(collectionPanel, "Body", "", 20, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Place(collectionBody.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -84f), new Vector2(720f, 780f));

            var div1 = UIKit.Panel(collectionPanel, "Div1", new Color32(0x2C, 0x32, 0x50, 0xFF));
            UIKit.Place(div1.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(772f, -82f), new Vector2(2f, 786f));

            collectionDex = UIKit.Label(collectionPanel, "Dex", "", 19, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Place(collectionDex.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(800f, -84f), new Vector2(420f, 780f));

            var div2 = UIKit.Panel(collectionPanel, "Div2", new Color32(0x2C, 0x32, 0x50, 0xFF));
            UIKit.Place(div2.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1236f, -82f), new Vector2(2f, 786f));

            dexPortrait = UIKit.Picture(collectionPanel, "DexPortrait", ProcArt.White);
            UIKit.Place(dexPortrait.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1280f, -92f), new Vector2(150f, 150f));

            dexDetail = UIKit.Label(collectionPanel, "DexDetail", "", 19, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Place(dexDetail.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1450f, -92f), new Vector2(280f, 160f));

            dexLore = UIKit.Label(collectionPanel, "DexLore", "", 19, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Place(dexLore.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(1280f, -262f), new Vector2(450f, 600f));

            var hint = UIKit.Label(collectionPanel, "Hint",
                "arrows browse the record  ·  1-6 changes who leads  ·  Tab or Esc to close",
                20, UIKit.InkDim, TextAnchor.MiddleCenter);
            UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(1200f, 26f));

            collectionPanel.gameObject.SetActive(false);
        }

        void BuildTitle(RectTransform root)
        {
            titlePanel = UIKit.Node(root, "Title");
            UIKit.Stretch(titlePanel, 0, 0, 0, 0);
            var bg = UIKit.Panel(titlePanel, "Bg", new Color32(0x06, 0x07, 0x10, 0xFC));
            UIKit.Stretch(bg.rectTransform, 0, 0, 0, 0);

            var heading = UIKit.Label(titlePanel, "Heading", UiCopy.Title, 96, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(heading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 250f), new Vector2(1200f, 120f));

            var sub = UIKit.Label(titlePanel, "Sub", UiCopy.Subtitle, 34, UIKit.Ink, TextAnchor.MiddleCenter);
            UIKit.Place(sub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1200f, 50f));

            var body = UIKit.Label(titlePanel, "Body", UiCopy.TitleBody,
                24, UIKit.Ink, TextAnchor.UpperCenter);
            UIKit.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -110f), new Vector2(1500f, 460f));

            titleHint = UIKit.Label(titlePanel, "Hint", UiCopy.BeginHint, 30, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(titleHint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(1300f, 40f));

            titleSaveLine = UIKit.Label(titlePanel, "SaveLine", "", 21, UIKit.InkDim, TextAnchor.MiddleCenter);
            UIKit.Place(titleSaveLine.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 62f), new Vector2(1300f, 28f));
        }

        void BuildVictory(RectTransform root)
        {
            victoryPanel = UIKit.Node(root, "Victory");
            UIKit.Stretch(victoryPanel, 0, 0, 0, 0);
            var bg = UIKit.Panel(victoryPanel, "Bg", new Color32(0x0A, 0x06, 0x14, 0xF7));
            UIKit.Stretch(bg.rectTransform, 0, 0, 0, 0);

            var heading = UIKit.Label(victoryPanel, "Heading", UiCopy.VictoryHeading, 82, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(heading.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 140f), new Vector2(1400f, 110f));

            var body = UIKit.Label(victoryPanel, "Body", "", 28, UIKit.Ink, TextAnchor.UpperCenter);
            UIKit.Place(body.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(1300f, 300f));
            body.name = "VictoryBody";

            var hint = UIKit.Label(victoryPanel, "Hint", "press SPACE to keep exploring", 26, UIKit.InkDim, TextAnchor.MiddleCenter);
            UIKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 80f), new Vector2(800f, 34f));

            victoryPanel.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // api
        // ------------------------------------------------------------------

        public void SetHudVisible(bool visible)
        {
            if (partyStrip != null) partyStrip.gameObject.SetActive(visible);
            if (objectivePanel != null) objectivePanel.gameObject.SetActive(visible);
            if (!visible) SetPrompt(null);
        }

        public void SetPrompt(string text)
        {
            if (promptPanel == null) return;
            bool show = !string.IsNullOrEmpty(text);
            if (promptPanel.gameObject.activeSelf != show) promptPanel.gameObject.SetActive(show);
            if (show) promptText.text = text;
        }

        public void Toast(string text)
        {
            toastQueue.Enqueue(text);
        }

        public void HideTitle()
        {
            if (titlePanel != null) titlePanel.gameObject.SetActive(false);
        }

        public void ShowTitle()
        {
            if (titlePanel != null) titlePanel.gameObject.SetActive(true);
        }

        /// <summary>Shows a "continue" option on the title card when a save exists.</summary>
        public void SetTitleSave(string description)
        {
            if (titleHint == null) return;
            if (string.IsNullOrEmpty(description))
            {
                titleHint.text = "press SPACE to begin";
                titleSaveLine.text = "";
            }
            else
            {
                titleHint.text = "press SPACE to continue   ·   N for a new run";
                titleSaveLine.text = "<color=#A8B2C4>saved run —  " + description +
                                     "</color>\n<color=#7A8090>a new run overwrites it</color>";
            }
        }

        public void ShowVictory()
        {
            if (victoryPanel == null) return;
            var body = victoryPanel.Find("VictoryBody");
            if (body != null)
            {
                var t = body.GetComponent<Text>();
                if (t != null)
                {
                    t.text = UiCopy.VictoryBody +
                             UiCopy.VictoryTally(dir.State.TotalCollected,
                                                 dir.State.Caught.Count,
                                                 SpeciesDatabase.CatchableCount,
                                                 dir.State.DistinctTypesHeld);
                }
            }
            victoryPanel.gameObject.SetActive(true);
        }

        public void HideVictory()
        {
            if (victoryPanel != null) victoryPanel.gameObject.SetActive(false);
        }

        public void ToggleCollection()
        {
            if (collectionPanel == null) return;
            bool next = !collectionPanel.gameObject.activeSelf;
            if (next) RefreshCollection();
            collectionPanel.gameObject.SetActive(next);
        }

        public void CloseCollection()
        {
            if (collectionPanel != null) collectionPanel.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // refresh
        // ------------------------------------------------------------------

        public void Refresh()
        {
            var state = dir.State;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                bool has = i < state.Party.Count;
                slot.Root.gameObject.SetActive(has);
                if (!has) continue;

                var egg = state.Party[i];
                slot.Dot.color = TypeChart.ColorOf(egg.Type);
                slot.Label.text = Shorten(egg.Name, 14) + "  <color=#A8B2C4>Lv " + egg.Level +
                                  "  " + TypeChart.Abbrev(egg.Type) + "</color>" +
                                  (egg.IsFainted ? "  <color=#E55555>OUT</color>" : "");
                slot.Hp.SetFraction(egg.HPFraction);
                slot.Hp.SetFillColor(UIKit.HealthColor(egg.HPFraction));
            }

            var beat = dir.Story.Current;
            string blocker = dir.Story.CurrentBlockerText(state);
            objectiveText.text =
                "<color=#FFC24D><b>" + beat.Chapter + "</b></color>\n" +
                beat.Objective +
                (blocker != null ? "\n<color=#A8B2C4>Still needed: " + blocker + "</color>" : "");

            // Terse on purpose: this is a fixed 524px strip that cannot wrap.
            cartonText.text = "Cartons " + state.Cartons + "/" + state.MaxCartons +
                              " · Salves " + state.Salves + "/" + GameState.MaxSalves + "\n" +
                              "Nest " + state.TotalCollected +
                              " · Types " + state.DistinctTypesHeld + "/8" +
                              " · Record " + state.Caught.Count + "/" + SpeciesDatabase.CatchableCount;

            if (dir.CurrentPlanet != null)
            {
                planetText.text = dir.Mode == GameMode.Space
                    ? "DEEP SPACE"
                    : dir.CurrentPlanet.Name.ToUpperInvariant();
            }

            if (CollectionOpen) RefreshCollection();
        }

        void RefreshCollection()
        {
            var state = dir.State;
            var sb = new System.Text.StringBuilder();

            sb.Append("<b><color=#FFC24D>PARTY</color></b>   <color=#7A8090>press 1-6 to lead with that egg</color>\n");
            for (int i = 0; i < state.Party.Count; i++)
            {
                sb.Append(i == 0 ? "<color=#FFC24D>1</color>" : "<color=#7A8090>" + (i + 1) + "</color>").Append(' ');
                sb.Append(DescribeEgg(state.Party[i])).Append('\n');
            }
            if (state.Party.Count == 0) sb.Append("<color=#A8B2C4>empty</color>\n");

            if (state.Nest.Count > 0)
            {
                sb.Append("\n<b><color=#FFC24D>NEST</color></b>  <color=#A8B2C4>")
                  .Append(state.Nest.Count).Append(" back home</color>\n");

                // The nest is unbounded, so show a window of it. The column is 780px at font 20,
                // which is 33 lines; the party costs at most 8 of those and the headers 2, so 20
                // is what is left. It used to show 8 and leave over half the column empty.
                const int shown = 20;
                for (int i = 0; i < Mathf.Min(shown, state.Nest.Count); i++)
                    sb.Append(DescribeEgg(state.Nest[i])).Append('\n');
                if (state.Nest.Count > shown)
                    sb.Append("<color=#7A8090>      ...and ").Append(state.Nest.Count - shown).Append(" more</color>\n");
            }

            collectionBody.text = sb.ToString();

            // Middle column: the field record, with a cursor.
            var all = SpeciesDatabase.All;
            dexCursor = Mathf.Clamp(dexCursor, 0, all.Count - 1);

            var dex = new System.Text.StringBuilder();
            dex.Append("<b><color=#FFC24D>FIELD RECORD</color></b>   <color=#A8B2C4>")
               .Append(state.Caught.Count).Append("/").Append(SpeciesDatabase.CatchableCount)
               .Append(" caught  ·  ").Append(state.Seen.Count).Append("/").Append(SpeciesDatabase.Count)
               .Append(" seen</color>\n\n");

            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                bool caught = state.Caught.Contains(s.Id);
                bool seen = state.Seen.Contains(s.Id);
                string hex = ColorUtility.ToHtmlStringRGB(TypeChart.ColorOf(s.Type));
                string mark = caught ? "<color=#5FD068>●</color>" : seen ? "<color=#FFC24D>◐</color>" : "<color=#3A4052>○</color>";
                string name = caught || seen ? s.Name : "? ? ?";
                bool here = i == dexCursor;

                dex.Append(here ? "<color=#FFC24D>▸</color>" : " ").Append(' ')
                   .Append("<color=#5A6072>").Append((i + 1).ToString("00")).Append("</color> ")
                   .Append(mark).Append(' ')
                   .Append("<color=#").Append(caught || seen ? hex : "3A4052").Append(">")
                   .Append(here ? "<b>" + name + "</b>" : name).Append("</color>\n");
            }
            collectionDex.text = dex.ToString();

            RefreshDexDetail(all[dexCursor], state);
        }

        /// <summary>Right column: everything known about the species under the dex cursor.</summary>
        void RefreshDexDetail(SpeciesDef species, GameState state)
        {
            bool caught = state.Caught.Contains(species.Id);
            bool seen = state.Seen.Contains(species.Id);

            if (!seen && !caught)
            {
                dexPortrait.sprite = ProcArt.Egg(species, 128);
                dexPortrait.color = new Color(0.06f, 0.07f, 0.11f, 1f);   // silhouette
                dexDetail.text = "<size=26><b>#" + (SpeciesDatabase.DexNumber(species.Id)).ToString("00") +
                                 "  ? ? ?</b></size>\n\n<color=#5A6072>Not yet encountered.</color>";
                dexLore.text = "<color=#5A6072>Find one in the wild to fill in this entry.</color>";
                return;
            }

            string hex = ColorUtility.ToHtmlStringRGB(TypeChart.ColorOf(species.Type));
            dexPortrait.sprite = ProcArt.Egg(species, 128);
            dexPortrait.color = Color.white;

            dexDetail.text =
                "<size=26><b>#" + SpeciesDatabase.DexNumber(species.Id).ToString("00") + "  " + species.Name + "</b></size>\n" +
                "<color=#" + hex + ">" + TypeChart.Name(species.Type) + "</color>" +
                "   <color=#7A8090>" + species.Pattern + " shell</color>\n\n" +
                "<color=#FFC24D>" + TypeChart.TraitName(TypeChart.TraitOf(species.Type)) + "</color>\n" +
                "<color=#A8B2C4>" + TypeChart.TraitBlurb(TypeChart.TraitOf(species.Type)) + "</color>";

            var lore = new System.Text.StringBuilder();

            // The matchups a player actually needs, in the place they are looking them up.
            lore.Append("<b><color=#FFC24D>MATCHUPS</color></b>\n");
            lore.Append("<color=#A8B2C4>hits hard  </color>").Append(TypeChart.Join(TypeChart.StrongAgainst(species.Type))).Append('\n');
            lore.Append("<color=#A8B2C4>weak to    </color>").Append(TypeChart.Join(TypeChart.VulnerableTo(species.Type))).Append('\n');
            lore.Append("<color=#A8B2C4>shrugs off </color>").Append(TypeChart.Join(TypeChart.Resists(species.Type))).Append("\n\n");

            lore.Append("<b><color=#FFC24D>BASE STATS</color></b>\n");
            lore.Append(StatRow("HP ", species.BaseHP, 100));
            lore.Append(StatRow("ATK", species.BaseAtk, 100));
            lore.Append(StatRow("DEF", species.BaseDef, 100));
            lore.Append(StatRow("SPD", species.BaseSpd, 100));
            lore.Append("<color=#7A8090>total ").Append(species.BaseTotal).Append("</color>\n\n");

            if (species.CanEvolve)
            {
                var next = SpeciesDatabase.Get(species.EvolvesIntoId);
                bool nextKnown = state.Seen.Contains(next.Id) || state.Caught.Contains(next.Id);
                lore.Append("<color=#62C8F5>→ becomes ")
                    .Append(nextKnown ? next.Name : "something")
                    .Append(" at level ").Append(species.EvolveLevel).Append("</color>\n\n");
            }
            else
            {
                lore.Append("<color=#7A8090>Final form.</color>\n\n");
            }

            if (caught) lore.Append("<i><color=#D2D8E4>").Append(species.Blurb).Append("</color></i>");
            else lore.Append("<color=#5A6072>Seen, but not yet collected. Catch one to record its notes.</color>");

            dexLore.text = lore.ToString();

            // Where to find it. Only worlds you have actually charted are listed — the record is
            // a reward for exploring, not a shopping list handed over at the start.
            var homes = PlanetDatabase.WorldsWith(species.Id);
            var known = new System.Collections.Generic.List<string>();
            int uncharted = 0;
            for (int i = 0; i < homes.Count; i++)
            {
                if (state.Visited.Contains(homes[i].Id)) known.Add(homes[i].Name);
                else uncharted++;
            }

            if (known.Count > 0)
            {
                dexLore.text += "\n\n<color=#FFC24D>FOUND ON</color>\n<color=#A8B2C4>" +
                                string.Join(" · ", known.ToArray()) + "</color>";
                if (uncharted > 0)
                    dexLore.text += "\n<color=#5A6072>and " + uncharted +
                                    (uncharted == 1 ? " world" : " worlds") + " you have not charted</color>";
            }
            else if (homes.Count > 0)
            {
                dexLore.text += "\n\n<color=#5A6072>Not seen on any world you have charted.</color>";
            }
        }

        static string StatRow(string label, int value, int max)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(value / (float)max * 12f), 0, 12);
            var bar = new System.Text.StringBuilder("<color=#5FD068>");
            for (int i = 0; i < 12; i++)
            {
                if (i == filled) bar.Append("</color><color=#2C3250>");
                bar.Append('=');
            }
            bar.Append("</color>");
            return "<color=#A8B2C4>" + label + "</color> " + value.ToString().PadLeft(3) + "  " + bar + "\n";
        }

        /// <summary>Clips a name to fit a fixed-width slot. The party strip cannot wrap.</summary>
        public static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max) return text;
            return text.Substring(0, max - 1) + "\u2026";
        }

        string DescribeEgg(EggInstance egg)
        {
            string hex = ColorUtility.ToHtmlStringRGB(TypeChart.ColorOf(egg.Type));
            var moves = new System.Text.StringBuilder();
            for (int i = 0; i < egg.Moves.Count; i++)
            {
                if (i > 0) moves.Append(", ");
                moves.Append(egg.Moves[i].Move.Name);
            }
            string evo = egg.EvolutionHint();
            return "<color=#" + hex + ">●</color> <b>" + egg.Name + "</b>  Lv " + egg.Level +
                   "  <color=#A8B2C4>" + TypeChart.Name(egg.Type) + "  " +
                   egg.CurrentHP + "/" + egg.MaxHP + " HP</color>" +
                   "  <color=#FFC24D>" + TypeChart.TraitName(egg.Trait) + "</color>" +
                   (evo != null ? "  <color=#62C8F5>→ " + evo + "</color>" : "") +
                   "\n      <color=#7A8090>" + moves + "  ·  " + TypeChart.TraitBlurb(egg.Trait) + "</color>";
        }

        void Update()
        {
            // In the collection screen, 1-6 promotes that egg to the front of the party,
            // which is who leads the next battle.
            if (CollectionOpen)
            {
                int slot = EggInput.DigitPressed;
                if (slot > 0 && slot < dir.State.Party.Count)
                {
                    dir.State.SwapPartySlots(0, slot);
                    if (dir.Audio != null) dir.Audio.Play(Sfx.UiConfirm);
                    Toast(dir.State.Party[0].Name + " will lead.");
                }

                // Arrows browse the field record; left/right jump a page.
                int move = 0;
                if (EggInput.DownPressed) move = 1;
                else if (EggInput.UpPressed) move = -1;
                else if (EggInput.RightPressed) move = 8;
                else if (EggInput.LeftPressed) move = -8;

                if (move != 0)
                {
                    int next = Mathf.Clamp(dexCursor + move, 0, SpeciesDatabase.Count - 1);
                    if (next != dexCursor)
                    {
                        dexCursor = next;
                        if (dir.Audio != null) dir.Audio.Play(Sfx.UiMove);
                        RefreshCollection();
                    }
                }
            }

            if (toastTimer > 0f)
            {
                toastTimer -= Time.deltaTime;
                if (toastTimer <= 0f) toastPanel.gameObject.SetActive(false);
            }
            else if (toastQueue.Count > 0)
            {
                toastText.text = toastQueue.Dequeue();
                toastPanel.gameObject.SetActive(true);
                toastTimer = 2.6f;
            }
        }
    }
}
