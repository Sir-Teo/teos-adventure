using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Eggverse
{
    /// <summary>Full-width dialogue box with a portrait and a typewriter reveal.</summary>
    public class DialogueView : MonoBehaviour
    {
        GameDirector dir;
        Canvas canvas;
        Image portrait, portraitFrame;
        Text speakerText, bodyText, hintText;
        Coroutine routine;

        public bool IsOpen { get { return canvas != null && canvas.gameObject.activeSelf; } }

        const float CharsPerSecond = 55f;

        /// <summary>The reveal rate for this run, or 0 when text should simply appear.</summary>
        float RevealRate => dir != null && dir.State != null
            ? CharsPerSecond * dir.State.RevealScale
            : CharsPerSecond;

        /// <summary>
        /// Portrait tints, built from the cast rather than kept beside it.
        ///
        /// This was a second hand-written table listing the same people as StoryDatabase.Npcs,
        /// and it drifted the moment three residents were added: Lune, Tilda and Garrow spoke in
        /// generic grey for several revisions. Nothing caught it, because the fallback returns a
        /// real colour and the cast check only asked that the tint was not default.
        /// </summary>
        static Dictionary<string, Color> tints;

        static Dictionary<string, Color> Tints
        {
            get
            {
                if (tints != null) return tints;
                tints = new Dictionary<string, Color>
                {
                    // Not residents, so not in the cast list: the player, and the woman at the end.
                    { "Teo", new Color32(0xEC, 0xF1, 0xF7, 0xFF) },
                    { "Amy", new Color32(0xFF, 0xC2, 0x4D, 0xFF) },
                };
                var cast = StoryDatabase.Npcs;
                for (int i = 0; i < cast.Length; i++) tints[cast[i].Name] = cast[i].Tint;
                return tints;
            }
        }

        /// <summary>Whether this speaker has a tint of their own, rather than the fallback.</summary>
        public static bool HasTintFor(string speaker) => Tints.ContainsKey(speaker);

        public static Color TintFor(string speaker)
        {
            Color c;
            return Tints.TryGetValue(speaker, out c) ? c : new Color32(0xA8, 0xB2, 0xC4, 0xFF);
        }


        public void Build(GameDirector director)
        {
            dir = director;
            canvas = UIKit.CreateCanvas("DialogueUI", 25, transform);
            var root = (RectTransform)canvas.transform;

            var shade = UIKit.Panel(root, "Shade", new Color(0f, 0f, 0f, 0.35f));
            UIKit.Stretch(shade.rectTransform, 0, 0, 0, 0);

            var box = UIKit.Node(root, "Box");
            UIKit.Place(box, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(1660f, 300f));

            var bg = UIKit.Panel(box, "Bg", new Color32(0x10, 0x12, 0x20, 0xFA));
            UIKit.Stretch(bg.rectTransform, 0, 0, 0, 0);
            var edge = UIKit.Panel(box, "Edge", UIKit.Accent);
            UIKit.Place(edge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(1660f, 4f));

            portraitFrame = UIKit.Panel(box, "PortraitFrame", UIKit.PanelLight);
            UIKit.Place(portraitFrame.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -26f), new Vector2(196f, 196f));

            portrait = UIKit.Picture(portraitFrame.transform, "Portrait", ProcArt.White);
            UIKit.Stretch(portrait.rectTransform, 5, 5, 5, 5);

            speakerText = UIKit.Label(box, "Speaker", "", 30, UIKit.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UIKit.Place(speakerText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(246f, -26f), new Vector2(900f, 38f));

            bodyText = UIKit.Label(box, "Body", "", 28, UIKit.Ink, TextAnchor.UpperLeft);
            // 180, not 190: the body ran down to 36 from the box floor and the hint sits at
            // 16..42 in the same corner, so they overlapped by 6px. Five lines either way at
            // font 28, so nothing is lost.
            UIKit.Place(bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(246f, -74f), new Vector2(1380f, 180f));

            hintText = UIKit.Label(box, "Hint", "Space to continue", 20, UIKit.InkDim, TextAnchor.LowerRight);
            UIKit.Place(hintText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-26f, 16f), new Vector2(500f, 26f));

            canvas.gameObject.SetActive(false);
        }

        public void Play(DialogueScript script, Action onComplete)
        {
            if (script == null || script.Lines == null || script.Lines.Length == 0)
            {
                if (onComplete != null) onComplete();
                return;
            }
            if (routine != null) StopCoroutine(routine);
            canvas.gameObject.SetActive(true);
            routine = StartCoroutine(Run(script, onComplete));
        }

        IEnumerator Run(DialogueScript script, Action onComplete)
        {
            for (int i = 0; i < script.Lines.Length; i++)
            {
                var line = script.Lines[i];
                Color tint = TintFor(line.Speaker);
                speakerText.text = line.Speaker;
                speakerText.color = tint;
                portrait.sprite = ProcArt.Portrait(line.Speaker, tint);
                portraitFrame.color = Color.Lerp(UIKit.PanelLight, tint, 0.35f);
                hintText.text = "Space to continue";

                yield return Typewriter(line.Text);

                // Wait for a deliberate press before moving on.
                bool advanced = false;
                float grace = 0f;
                while (!advanced)
                {
                    grace += Time.deltaTime;
                    if (grace > 0.12f && (EggInput.ConfirmPressed || EggInput.InteractPressed)) advanced = true;
                    yield return null;
                }
            }

            canvas.gameObject.SetActive(false);
            routine = null;
            if (onComplete != null) onComplete();
        }

        IEnumerator Typewriter(string text)
        {
            bodyText.text = "";
            float shown = 0f;
            // At "instant" the line is simply there; the loop below would spin a frame anyway.
            if (RevealRate <= 0f) { bodyText.text = text; yield break; }

            while (shown < text.Length)
            {
                shown += Time.deltaTime * RevealRate;
                if (EggInput.ConfirmPressed || EggInput.InteractPressed)
                {
                    bodyText.text = text;
                    // Swallow the press that skipped the reveal.
                    yield return null;
                    yield break;
                }
                int visible = Mathf.Min(text.Length, Mathf.FloorToInt(shown));
                if (visible > bodyText.text.Length && AudioDirector.Instance != null)
                    AudioDirector.Instance.Play(Sfx.Talk);
                bodyText.text = text.Substring(0, visible);
                yield return null;
            }
            bodyText.text = text;
        }
    }
}
