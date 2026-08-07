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
        const float BoxWidth = 760f, BoxHeight = 620f;
        const float FirstRowY = -118f, RowStep = 62f, RowHeight = 52f;
        const float RuleY = -390f, ControlsY = -424f, ControlsStep = 32f;

        GameDirector dir;
        Canvas canvas;
        readonly List<Text> rows = new List<Text>();
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
                var label = UIKit.Label(box, "Row" + i, "", 28, UIKit.Ink, TextAnchor.MiddleCenter);
                UIKit.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, FirstRowY - i * RowStep), new Vector2(700f, RowHeight));
                rows.Add(label);
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

        string[] PageText()
        {
            if (page == Page.Main)
                return new[]
                {
                    "Resume",
                    "Settings",
                    "Save now",
                    confirmingQuit
                        ? "<color=#E55555>Quit to title — press Enter again</color>"
                        : "Quit to title",
                };

            var audio = dir.Audio;
            return new[]
            {
                "Sound  <color=#FFC24D>" + (audio.Muted ? "off" : "on") + "</color>",
                "Music  " + Meter(audio.MusicVolume, 0.6f),
                "Effects  " + Meter(audio.SfxVolume, 1f),
                "Screen motion  <color=#FFC24D>" + (dir.State.ScreenMotion ? "on" : "off") + "</color>",
                "Text speed  <color=#FFC24D>" + dir.State.TextSpeedName + "</color>",
                "Back",
            };
        }

        void Refresh()
        {
            title.text = page == Page.Main ? "PAUSED" : "SETTINGS";

            var text = PageText();
            for (int i = 0; i < rows.Count; i++)
            {
                bool used = i < text.Length;
                rows[i].gameObject.SetActive(used);
                if (!used) continue;

                bool selected = i == cursor;
                rows[i].text = (selected ? "<color=#FFC24D>▸</color>  " : "    ") + text[i];
                rows[i].color = selected ? UIKit.Accent : UIKit.Ink;
                rows[i].fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
            }

            bool showControls = page == Page.Main;
            rule.gameObject.SetActive(showControls);
            for (int i = 0; i < controlLines.Count; i++) controlLines[i].gameObject.SetActive(showControls);

            footer.text = OnSlider
                ? "Left / Right to adjust  ·  Esc to go back"
                : page == Page.Main
                    ? "Up / Down to move  ·  Enter to choose  ·  Esc to resume"
                    : "Up / Down to move  ·  Enter to change  ·  Esc to go back";
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
