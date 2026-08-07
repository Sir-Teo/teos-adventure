using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Eggverse
{
    /// <summary>Pause menu: sound, an explicit save, and a way back to the title card.</summary>
    public class PauseView : MonoBehaviour
    {
        enum Row { Resume, Sound, Music, Effects, Motion, Save, Quit, Count }

        GameDirector dir;
        Canvas canvas;
        readonly List<Text> rows = new List<Text>();
        Text footer;
        int cursor;
        bool confirmingQuit;

        public bool IsOpen { get { return canvas != null && canvas.gameObject.activeSelf; } }

        public void Build(GameDirector director)
        {
            dir = director;
            canvas = UIKit.CreateCanvas("Pause", 35, transform);
            var root = (RectTransform)canvas.transform;

            var shade = UIKit.Panel(root, "Shade", new Color(0f, 0f, 0f, 0.66f));
            UIKit.Stretch(shade.rectTransform, 0, 0, 0, 0);

            var box = UIKit.Node(root, "Box");
            // 800 rather than 620: the extra 180 carries the controls reference below the menu.
            UIKit.Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 862f));
            var bg = UIKit.Panel(box, "Bg", new Color32(0x11, 0x13, 0x22, 0xFA));
            UIKit.Stretch(bg.rectTransform, 0, 0, 0, 0);
            var edge = UIKit.Panel(box, "Edge", UIKit.Accent);
            UIKit.Place(edge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(760f, 4f));

            var title = UIKit.Label(box, "Title", "PAUSED", 40, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -38f), new Vector2(700f, 48f));

            for (int i = 0; i < (int)Row.Count; i++)
            {
                var label = UIKit.Label(box, "Row" + i, "", 28, UIKit.Ink, TextAnchor.MiddleCenter);
                UIKit.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -118f - i * 62f), new Vector2(700f, 52f));
                rows.Add(label);
            }

            // A controls reference, always on screen rather than behind another keypress. The
            // title screen lists them once and is never seen again, so this is the only place a
            // player who has forgotten which key opens the chart can actually look.
            var rule = UIKit.Panel(box, "Rule", new Color32(0x2C, 0x32, 0x50, 0xFF));
            UIKit.Place(rule.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                        new Vector2(0f, -568f), new Vector2(660f, 2f));

            for (int i = 0; i < UiCopy.PauseControls.Length; i++)
            {
                var line = UIKit.Label(box, "Controls" + i, UiCopy.PauseControls[i],
                                       20, UIKit.InkDim, TextAnchor.MiddleCenter);
                UIKit.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -602f - i * 32f), new Vector2(700f, 28f));
            }

            footer = UIKit.Label(box, "Footer", "", 20, UIKit.InkDim, TextAnchor.MiddleCenter);
            UIKit.Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(700f, 60f));

            canvas.gameObject.SetActive(false);
        }

        public void Open()
        {
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

        void Refresh()
        {
            var audio = dir.Audio;
            string[] text =
            {
                "Resume",
                "Sound  <color=#FFC24D>" + (audio.Muted ? "off" : "on") + "</color>",
                "Music  " + Meter(audio.MusicVolume, 0.6f),
                "Effects  " + Meter(audio.SfxVolume, 1f),
                "Screen motion  <color=#FFC24D>" + (dir.State.ScreenMotion ? "on" : "off") + "</color>",
                "Save now",
                confirmingQuit
                    ? "<color=#E55555>Quit to title — press Enter again</color>"
                    : "Quit to title",
            };

            for (int i = 0; i < rows.Count; i++)
            {
                bool selected = i == cursor;
                rows[i].text = (selected ? "<color=#FFC24D>▸</color>  " : "    ") + text[i];
                rows[i].color = selected ? UIKit.Accent : UIKit.Ink;
                rows[i].fontStyle = selected ? FontStyle.Bold : FontStyle.Normal;
            }

            bool slider = cursor == (int)Row.Music || cursor == (int)Row.Effects;
            footer.text = slider
                ? "Left / Right to adjust  ·  Esc to resume"
                : "Up / Down to move  ·  Enter to choose  ·  Esc to resume";
        }

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

        void Update()
        {
            if (!IsOpen) return;

            int before = cursor;
            if (EggInput.DownPressed) cursor = (cursor + 1) % (int)Row.Count;
            if (EggInput.UpPressed) cursor = (cursor + (int)Row.Count - 1) % (int)Row.Count;
            if (cursor != before)
            {
                confirmingQuit = false;
                dir.Audio.Play(Sfx.UiMove);
                Refresh();
            }

            // Sliders respond to left/right without needing to be "entered".
            if (cursor == (int)Row.Music || cursor == (int)Row.Effects)
            {
                float step = EggInput.RightPressed ? 0.05f : EggInput.LeftPressed ? -0.05f : 0f;
                if (step != 0f)
                {
                    if (cursor == (int)Row.Music)
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

            if (EggInput.CancelPressed) { Close(); return; }

            if (EggInput.ConfirmPressed || EggInput.InteractPressed)
            {
                switch ((Row)cursor)
                {
                    case Row.Resume:
                        dir.Audio.Play(Sfx.UiConfirm);
                        Close();
                        break;

                    case Row.Sound:
                        dir.Audio.ToggleMute();
                        Refresh();
                        break;

                    case Row.Motion:
                        dir.State.ScreenMotion = !dir.State.ScreenMotion;
                        dir.ApplyMotionSetting();
                        dir.Audio.Play(Sfx.UiConfirm);
                        Refresh();
                        break;

                    case Row.Save:
                        dir.SaveNow(true);
                        Close();
                        break;

                    case Row.Quit:
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
            }
        }
    }
}
