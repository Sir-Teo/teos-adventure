using UnityEngine;
using UnityEngine.UI;

namespace Eggverse
{
    /// <summary>
    /// A full-screen colour wash that snaps opaque and fades out, used to cover the instant
    /// pop when the world is rebuilt underneath the player (landing, lifting off, battling).
    /// </summary>
    public class TransitionView : MonoBehaviour
    {
        Canvas canvas;
        Image sheet;
        float alpha;
        float fadeSpeed = 2.6f;
        Color tint = Color.black;

        public void Build()
        {
            // Above everything else, including the battle UI.
            canvas = UIKit.CreateCanvas("Transition", 60, transform);
            sheet = UIKit.Panel((RectTransform)canvas.transform, "Sheet", Color.black);
            UIKit.Stretch(sheet.rectTransform, 0, 0, 0, 0);
            sheet.raycastTarget = false;
            alpha = 0f;
            canvas.gameObject.SetActive(false);
        }

        /// <summary>Snaps to full cover and fades back out over `seconds`.</summary>
        public void Flash(Color color, float seconds = 0.42f)
        {
            tint = color;
            alpha = 1f;
            fadeSpeed = 1f / Mathf.Max(0.05f, seconds);
            canvas.gameObject.SetActive(true);
            sheet.color = new Color(tint.r, tint.g, tint.b, alpha);
        }

        public void FlashDark(float seconds = 0.42f) => Flash(new Color(0.02f, 0.02f, 0.05f), seconds);

        void Update()
        {
            if (alpha <= 0f) return;
            alpha -= Time.deltaTime * fadeSpeed;
            if (alpha <= 0f)
            {
                alpha = 0f;
                canvas.gameObject.SetActive(false);
                return;
            }
            // Ease out so the last of the fade is gentle.
            sheet.color = new Color(tint.r, tint.g, tint.b, alpha * alpha);
        }
    }
}
