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

        Canvas canvas;
        Text titleText, entryText, hintText;
        Image portrait;
        string buffer = "";
        Action<string> onDone;
        bool subscribed;
        float caretTimer;

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

            hintText = UIKit.Label(box, "Hint", "type a name  ·  Enter to confirm  ·  Esc to keep the species name",
                                   20, UIKit.InkDim, TextAnchor.MiddleCenter);
            UIKit.Place(hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(840f, 28f));

            canvas.gameObject.SetActive(false);
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
            if (c < ' ' || c == 127) return;
            if (buffer.Length >= MaxLength) return;
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
                    string trimmed = buffer.Trim();
                    Close(string.IsNullOrEmpty(trimmed) ? null : trimmed);
                    return;
                }
            }

            caretTimer += Time.deltaTime;
            bool caretOn = (caretTimer % 1f) < 0.55f;
            entryText.text = buffer + (caretOn ? "<color=#FFC24D>|</color>" : " ");
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
