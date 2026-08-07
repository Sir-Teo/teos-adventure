using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Eggverse
{
    /// <summary>
    /// A small text prompt for nicknaming a caught egg. Uses the Input System's
    /// onTextInput event rather than uGUI's InputField, which relies on the legacy
    /// input backend this project does not have enabled.
    /// </summary>
    public class NameEntryView : MonoBehaviour
    {
        public const int MaxLength = 12;

        /// <summary>
        /// What a nickname may be made of.
        ///
        /// Everything a player types here is interpolated straight into rich text — the party
        /// strip, the battle log, every toast, the ending card. A nickname of "&lt;b&gt;" bolds the
        /// rest of the line it lands in; one containing "&lt;/color&gt;" ends the colour it was
        /// wrapped in and repaints whatever follows. It goes into the save file that way too.
        /// "&lt;3" is a name somebody will type on their first run.
        ///
        /// Letters, digits, space, apostrophe and hyphen is what a name is. This is a filter
        /// rather than an escape because the stored string should be clean: escaping at every
        /// one of the dozen places a name is drawn is a rule that gets forgotten at the
        /// thirteenth.
        /// </summary>
        public static bool Allowed(char c) =>
            char.IsLetterOrDigit(c) || c == ' ' || c == '\'' || c == '-';

        /// <summary>The name a raw string is allowed to become, or null if nothing is left.</summary>
        public static string Clean(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            var sb = new System.Text.StringBuilder();
            foreach (var c in raw)
            {
                if (!Allowed(c)) continue;
                // No runs of spaces, and none at the front - a name is not a layout tool.
                if (c == ' ' && (sb.Length == 0 || sb[sb.Length - 1] == ' ')) continue;
                if (sb.Length >= MaxLength) break;
                sb.Append(c);
            }
            string outp = sb.ToString().TrimEnd();
            return outp.Length == 0 ? null : outp;
        }

        Canvas canvas;
        Text titleText, entryText, hintText;
        Image portrait;
        string buffer = "";
        Action<string> onDone;
        bool subscribed;
        float caretTimer;

        // How long the counter stays lit after a keystroke was refused. The cap used to swallow
        // characters in silence: a player typing a thirteenth letter saw the field simply stop
        // taking them, with nothing on screen having ever mentioned twelve.
        float full;

        public bool IsOpen { get { return canvas != null && canvas.gameObject.activeSelf; } }

        public void Build()
        {
            canvas = UIKit.CreateCanvas("NameEntry", 40, transform);
            var root = (RectTransform)canvas.transform;

            var shade = UIKit.Panel(root, "Shade", new Color(0f, 0f, 0f, 0.62f));
            UIKit.Stretch(shade.rectTransform, 0, 0, 0, 0);

            var box = UIKit.Node(root, "Box");
            UIKit.Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 420f));
            var bg = UIKit.Panel(box, "Bg", new Color32(0x12, 0x14, 0x24, 0xFA));
            UIKit.Stretch(bg.rectTransform, 0, 0, 0, 0);
            var edge = UIKit.Panel(box, "Edge", UIKit.Accent);
            UIKit.Place(edge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(900f, 4f));

            portrait = UIKit.Picture(box, "Egg", ProcArt.White);
            UIKit.Place(portrait.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(130f, 130f));

            titleText = UIKit.Label(box, "Title", "", 30, UIKit.Ink, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -172f), new Vector2(820f, 40f));

            var field = UIKit.Panel(box, "Field", new Color32(0x0A, 0x0C, 0x16, 0xFF));
            UIKit.Place(field.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -60f), new Vector2(680f, 76f));
            entryText = UIKit.Label(field.transform, "Text", "", 38, UIKit.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            UIKit.Stretch(entryText.rectTransform, 12, 6, 12, 6);

            hintText = UIKit.Label(box, "Hint", Hint(0, false),
                                   20, UIKit.InkDim, TextAnchor.MiddleCenter);
            UIKit.Place(hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(840f, 28f));

            canvas.gameObject.SetActive(false);
        }

        /// <summary>
        /// The line under the field. It carries the count, because a limit a player only
        /// discovers by hitting it is a limit that reads as the game having stopped working.
        /// </summary>
        public static string Hint(int used, bool refused)
        {
            // Short, because the rest of the line is the two keys and it all has to fit 840px at
            // font 20. The first wording ran to "that is as long as it goes" and overran by a
            // comfortable margin, which the fit check said immediately.
            string count = refused
                ? "<color=#FFC24D>" + used + "/" + MaxLength + " — that is all</color>"
                : used >= MaxLength
                    ? "<color=#FFC24D>" + used + "/" + MaxLength + "</color>"
                    : used + "/" + MaxLength;
            return count + "  ·  Enter to confirm  ·  Esc to keep the species name";
        }

        public void Open(EggInstance egg, Action<string> done)
        {
            onDone = done;
            buffer = "";
            titleText.text = "Give this " + egg.Species.Name + " a nickname?";
            portrait.sprite = ProcArt.Egg(egg.Species, 128);
            canvas.gameObject.SetActive(true);
            Subscribe(true);
        }

        void Subscribe(bool on)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (on && !subscribed) { keyboard.onTextInput += OnChar; subscribed = true; }
            else if (!on && subscribed) { keyboard.onTextInput -= OnChar; subscribed = false; }
        }

        void OnDisable() { Subscribe(false); }

        void OnChar(char c)
        {
            if (!IsOpen) return;
            // Printable characters only; Enter and Backspace are handled as keys.
            if (!Allowed(c)) return;
            if (buffer.Length >= MaxLength) { full = 0.5f; return; }
            buffer += c;
        }

        void Update()
        {
            if (!IsOpen) return;

            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.backspaceKey.wasPressedThisFrame && buffer.Length > 0)
                    buffer = buffer.Substring(0, buffer.Length - 1);

                if (keyboard.escapeKey.wasPressedThisFrame) { Close(null); return; }

                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    Close(Clean(buffer));
                    return;
                }
            }

            caretTimer += Time.deltaTime;
            full = Mathf.Max(0f, full - Time.deltaTime);

            // The caret keeps its slot whether it is lit or not. It used to swap the bar for a
            // space, and a bar and a space are not the same width - so the whole name shifted
            // sideways twice a second, for the entire time a player was reading what they had
            // typed.
            bool caretOn = (caretTimer % 1f) < 0.55f;
            entryText.text = buffer + "<color=" + (caretOn ? "#FFC24D" : "#00000000") + ">|</color>";

            hintText.text = Hint(buffer.Length, full > 0f);
        }

        void Close(string result)
        {
            Subscribe(false);
            canvas.gameObject.SetActive(false);
            var callback = onDone;
            onDone = null;
            if (callback != null) callback(result);
        }
    }
}
