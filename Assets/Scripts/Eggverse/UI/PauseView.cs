using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Eggverse
{
    /// <summary>
    /// Pause menu, in two pages: the things you came here to do, and the settings.
    ///
    /// It was one flat list, and every option added a row and grew the box — eight rows and 924px
    /// by the end, 78px from the edge of a 1080 screen, with the next setting having nowhere to
    /// go. Splitting it means the page a player actually opens under pressure is four lines long,
    /// and settings can grow without the box chasing them down the screen.
    /// </summary>
    public class PauseView : MonoBehaviour
    {
        enum Page { Main, Settings }

        enum MainRow { Resume, Settings, Save, Quit, Count }
        enum SetRow { Sound, Music, Effects, Motion, TextSpeed, Back, Count }

        // One box for both pages. Resizing it as you step in and out reads as the menu flinching.
        // Public because the render and the layout check measure the real numbers rather than
        // their own transcription of them - a screen nobody had ever drawn is exactly where a
        // second copy of the geometry would have gone unnoticed.
        public const float BoxWidth = 760f, BoxHeight = 620f;
        public const float FirstRowY = -118f, RowStep = 62f, RowHeight = 52f;
        public const float RuleY = -390f, ControlsY = -424f, ControlsStep = 32f;
        public const float RowWidth = 700f, RowFont = 28f, ControlsFont = 20f, FooterFont = 20f;

        // Rows were centre-anchored, each one laid out from its own width. Nothing lined up, and
        // the cursor slid sideways as it moved down a list. Worse on the settings page, where the
        // values change width: toggling sound on to off, or stepping text speed from "relaxed" to
        // "instant", moved the row horizontally underneath the cursor while the player was looking
        // straight at it. Three columns instead: a gutter the cursor lives in, labels left-aligned
        // from a fixed x, and values from a second fixed x. This is what every other list in the
        // game does - the party strip, the collection, the move buttons.
        public const float GutterX = -330f, GutterWidth = 34f;
        public const float LabelX = -288f, LabelWidth = 300f;
        // The widest label, "Screen motion", runs to -99. The value column at -30 leaves a clear
        // 69px channel between the two and keeps the pair centred in the box, rather than the
        // labels hugging the left edge with a third of the box empty on the right.
        public const float ValueX = -30f, ValueWidth = 320f;

        GameDirector dir;
        Canvas canvas;
        readonly List<Text> cursors = new List<Text>();
        readonly List<Text> rows = new List<Text>();
        readonly List<Text> values = new List<Text>();
        readonly List<Text> controlLines = new List<Text>();
        Text title, footer;
        RectTransform rule;
        Page page;
        int cursor;
        bool confirmingQuit;

        int RowCount => page == Page.Main ? (int)MainRow.Count : (int)SetRow.Count;

        public bool IsOpen { get { return canvas != null && canvas.gameObject.activeSelf; } }

        public void Build(GameDirector director)
        {
            dir = director;
            canvas = UIKit.CreateCanvas("Pause", 35, transform);
            var root = (RectTransform)canvas.transform;

            var shade = UIKit.Panel(root, "Shade", new Color(0f, 0f, 0f, 0.66f));
            UIKit.Stretch(shade.rectTransform, 0, 0, 0, 0);

            var box = UIKit.Node(root, "Box");
            UIKit.Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                        new Vector2(BoxWidth, BoxHeight));
            var bg = UIKit.Panel(box, "Bg", new Color32(0x11, 0x13, 0x22, 0xFA));
            UIKit.Stretch(bg.rectTransform, 0, 0, 0, 0);
            var edge = UIKit.Panel(box, "Edge", UIKit.Accent);
            UIKit.Place(edge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                        new Vector2(BoxWidth, 4f));

            title = UIKit.Label(box, "Title", "PAUSED", 40, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -38f), new Vector2(700f, 48f));

            // Enough rows for the longer of the two pages; the extras are simply hidden.
            int most = Mathf.Max((int)MainRow.Count, (int)SetRow.Count);
            for (int i = 0; i < most; i++)
            {
                float y = FirstRowY - i * RowStep;

                var caret = UIKit.Label(box, "Caret" + i, "▸", (int)RowFont, UIKit.Accent, TextAnchor.MiddleCenter);
                UIKit.Place(caret.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(GutterX + GutterWidth * 0.5f, y), new Vector2(GutterWidth, RowHeight));
                cursors.Add(caret);

                var label = UIKit.Label(box, "Row" + i, "", (int)RowFont, UIKit.Ink, TextAnchor.MiddleLeft);
                UIKit.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(LabelX + LabelWidth * 0.5f, y), new Vector2(LabelWidth, RowHeight));
                rows.Add(label);

                var value = UIKit.Label(box, "Value" + i, "", (int)RowFont, UIKit.Ink, TextAnchor.MiddleLeft);
                UIKit.Place(value.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(ValueX + ValueWidth * 0.5f, y), new Vector2(ValueWidth, RowHeight));
                values.Add(value);
            }

            // The controls reference belongs on the page a stuck player lands on, not behind a
            // second keypress. It is hidden on the settings page, where it would only be noise.
            var ruleImg = UIKit.Panel(box, "Rule", new Color32(0x2C, 0x32, 0x50, 0xFF));
            UIKit.Place(ruleImg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, RuleY), new Vector2(660f, 2f));
            rule = ruleImg.rectTransform;

            for (int i = 0; i < UiCopy.PauseControls.Length; i++)
            {
                var line = UIKit.Label(box, "Controls" + i, UiCopy.PauseControls[i],
                                       20, UIKit.InkDim, TextAnchor.MiddleCenter);
                UIKit.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, ControlsY - i * ControlsStep), new Vector2(700f, 28f));
                controlLines.Add(line);
            }

            footer = UIKit.Label(box, "Footer", "", 20, UIKit.InkDim, TextAnchor.MiddleCenter);
            UIKit.Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                        new Vector2(0f, 42f), new Vector2(700f, 60f));

            canvas.gameObject.SetActive(false);
        }

        public void Open()
        {
            page = Page.Main;
            cursor = 0;
            confirmingQuit = false;
            canvas.gameObject.SetActive(true);
            Refresh();
        }

        public void Close()
        {
            confirmingQuit = false;
            canvas.gameObject.SetActive(false);
        }

        /// <summary>
        /// The four things you came here to do. The quit confirmation is a value rather than a
        /// replacement label, so the row a player is about to press does not change under them -
        /// the words they aimed at stay put and a warning appears beside them.
        /// </summary>
        public static (string label, string value)[] MainRows(bool confirmingQuit)
        {
            return new[]
            {
                ("Resume", ""),
                ("Settings", ""),
                ("Save now", ""),
                ("Quit to title", confirmingQuit ? "<color=#E55555>press Enter again</color>" : ""),
            };
        }

        /// <summary>
        /// The settings page, from values rather than from a director, so the render and the
        /// layout check can ask for the widest state the page can actually reach.
        /// </summary>
        public static (string label, string value)[] SettingsRows(
            bool muted, float music, float sfx, bool motion, string textSpeed)
        {
            return new[]
            {
                ("Sound", "<color=#FFC24D>" + (muted ? "off" : "on") + "</color>"),
                ("Music", Meter(music, 0.6f)),
                ("Effects", Meter(sfx, 1f)),
                ("Screen motion", "<color=#FFC24D>" + (motion ? "on" : "off") + "</color>"),
                ("Text speed", "<color=#FFC24D>" + textSpeed + "</color>"),
                ("Back", ""),
            };
        }

        public static string FooterText(bool main, bool onSlider)
        {
            return onSlider
                ? "Left / Right to adjust  ·  Esc to go back"
                : main
                    ? "Up / Down to move  ·  Enter to choose  ·  Esc to resume"
                    : "Up / Down to move  ·  Enter to change  ·  Esc to go back";
        }

        (string label, string value)[] PageText()
        {
            if (page == Page.Main) return MainRows(confirmingQuit);
            var audio = dir.Audio;
            return SettingsRows(audio.Muted, audio.MusicVolume, audio.SfxVolume,
                                dir.State.ScreenMotion, dir.State.TextSpeedName);
        }

        void Refresh()
        {
            title.text = page == Page.Main ? "PAUSED" : "SETTINGS";

            var text = PageText();
            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < text.Length;
                rows[i].gameObject.SetActive(used);
                values[i].gameObject.SetActive(used);
                cursors[i].gameObject.SetActive(used && i == cursor);
                if (!used) continue;

                bool selected = i == cursor;
                rows[i].text = text[i].label;
                rows[i].color = selected ? UIKit.Accent : UIKit.Ink;
                rows[i].fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
                values[i].text = text[i].value;
            }

            bool showControls = page == Page.Main;
            rule.gameObject.SetActive(showControls);
            for (int i = 0; i < controlLines.Count; i++) controlLines[i].gameObject.SetActive(showControls);

            footer.text = FooterText(page == Page.Main, OnSlider);
        }

        bool OnSlider => page == Page.Settings &&
                         (cursor == (int)SetRow.Music || cursor == (int)SetRow.Effects);

        static string Meter(float value, float max)
        {
            int filled = Mathf.RoundToInt(Mathf.Clamp01(value / max) * 10f);
            var sb = new System.Text.StringBuilder("<color=#FFC24D>");
            for (int i = 0; i < 10; i++)
            {
                if (i == filled) sb.Append("</color><color=#3A4052>");
                sb.Append('|');
            }
            sb.Append("</color>");
            return sb.ToString();
        }

        void GoTo(Page next)
        {
            page = next;
            cursor = 0;
            confirmingQuit = false;
            dir.Audio.Play(next == Page.Settings ? Sfx.UiConfirm : Sfx.UiBack);
            Refresh();
        }

        void Update()
        {
            if (!IsOpen) return;

            int before = cursor;
            if (EggInput.DownPressed) cursor = (cursor + 1) % RowCount;
            if (EggInput.UpPressed) cursor = (cursor + RowCount - 1) % RowCount;
            if (cursor != before)
            {
                confirmingQuit = false;
                dir.Audio.Play(Sfx.UiMove);
                Refresh();
            }

            // Sliders respond to left/right without needing to be "entered".
            if (OnSlider)
            {
                float step = EggInput.RightPressed ? 0.05f : EggInput.LeftPressed ? -0.05f : 0f;
                if (step != 0f)
                {
                    if (cursor == (int)SetRow.Music)
                    {
                        dir.Audio.MusicVolume = Mathf.Clamp(dir.Audio.MusicVolume + step, 0f, 0.6f);
                        dir.Audio.ApplyMusicVolume();
                    }
                    else
                    {
                        dir.Audio.SfxVolume = Mathf.Clamp(dir.Audio.SfxVolume + step, 0f, 1f);
                        dir.Audio.Play(Sfx.UiMove);
                    }
                    Refresh();
                }
            }

            // Esc steps back a page before it closes the menu, so it never throws away a
            // settings visit in one press.
            if (EggInput.CancelPressed)
            {
                if (page == Page.Settings) GoTo(Page.Main);
                else Close();
                return;
            }

            if (!EggInput.ConfirmPressed && !EggInput.InteractPressed) return;

            if (page == Page.Main)
            {
                switch ((MainRow)cursor)
                {
                    case MainRow.Resume:
                        dir.Audio.Play(Sfx.UiConfirm);
                        Close();
                        break;

                    case MainRow.Settings:
                        GoTo(Page.Settings);
                        break;

                    case MainRow.Save:
                        dir.SaveNow(true);
                        Close();
                        break;

                    case MainRow.Quit:
                        if (!confirmingQuit)
                        {
                            confirmingQuit = true;
                            dir.Audio.Play(Sfx.UiBack);
                            Refresh();
                        }
                        else
                        {
                            Close();
                            dir.ReturnToTitle();
                        }
                        break;
                }
                return;
            }

            switch ((SetRow)cursor)
            {
                case SetRow.Sound:
                    dir.Audio.ToggleMute();
                    Refresh();
                    break;

                case SetRow.Motion:
                    dir.State.ScreenMotion = !dir.State.ScreenMotion;
                    dir.ApplyMotionSetting();
                    dir.Audio.Play(Sfx.UiConfirm);
                    Refresh();
                    break;

                case SetRow.TextSpeed:
                    dir.State.TextSpeed = (dir.State.TextSpeed + 1) % GameState.TextSpeedCount;
                    dir.Audio.Play(Sfx.UiConfirm);
                    Refresh();
                    break;

                case SetRow.Back:
                    GoTo(Page.Main);
                    break;
            }
        }
    }
}
