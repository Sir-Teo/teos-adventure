using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Eggverse
{
    /// <summary>A progress bar built from two stretched images.</summary>
    public class BarWidget
    {
        public Image Background;
        public Image Fill;
        public RectTransform FillRect;

        public void SetFraction(float f)
        {
            f = Mathf.Clamp01(f);
            FillRect.anchorMin = new Vector2(0f, 0f);
            FillRect.anchorMax = new Vector2(f, 1f);
            FillRect.offsetMin = Vector2.zero;
            FillRect.offsetMax = Vector2.zero;
        }

        public void SetFillColor(Color c) { Fill.color = c; }
    }

    /// <summary>Small helpers for assembling uGUI hierarchies entirely from code.</summary>
    public static class UIKit
    {
        public static readonly Color Ink = new Color32(0xF2, 0xF5, 0xFA, 0xFF);
        public static readonly Color InkDim = new Color32(0xA8, 0xB2, 0xC4, 0xFF);
        public static readonly Color PanelDark = new Color32(0x12, 0x14, 0x22, 0xEE);
        public static readonly Color PanelMid = new Color32(0x1E, 0x22, 0x38, 0xF2);
        public static readonly Color PanelLight = new Color32(0x2C, 0x32, 0x50, 0xFF);
        public static readonly Color Accent = new Color32(0xFF, 0xC2, 0x4D, 0xFF);
        public static readonly Color Danger = new Color32(0xE5, 0x55, 0x55, 0xFF);
        public static readonly Color Good = new Color32(0x5F, 0xD0, 0x68, 0xFF);

        static Font font;
        static bool fontWarned;

        /// <summary>
        /// The UI font. Every label in the game depends on this, and a null font renders
        /// nothing at all, so try each known source before giving up loudly.
        /// </summary>
        public static Font Font
        {
            get
            {
                if (font != null) return font;

                // Unity 2022+ renamed the built-in Arial to LegacyRuntime.
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
                if (font == null) { try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); } catch { } }

                // Last resort: whatever the OS can give us.
                if (font == null)
                {
                    try { font = Font.CreateDynamicFontFromOSFont("Arial", 16); } catch { }
                    if (font == null) { try { font = Font.CreateDynamicFontFromOSFont("Helvetica", 16); } catch { } }
                }

                if (font == null && !fontWarned)
                {
                    fontWarned = true;
                    Debug.LogError("Eggverse: no usable UI font was found, so all text will be invisible. " +
                                   "Check that the built-in LegacyRuntime.ttf resource is available.");
                }
                return font;
            }
        }

        public static Canvas CreateCanvas(string name, int sortOrder, Transform parent = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            if (parent != null) go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        // ---------- rect layout ----------

        public static RectTransform Stretch(RectTransform rt, float left, float bottom, float right, float top)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Anchor to a point (0..1 in both axes) with an explicit pixel size.</summary>
        public static RectTransform Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        // ---------- widgets ----------

        public static Image Panel(Transform parent, string name, Color color)
        {
            var rt = Node(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = ProcArt.White;
            img.color = color;
            img.type = Image.Type.Simple;
            return img;
        }

        public static Image Picture(Transform parent, string name, Sprite sprite)
        {
            var rt = Node(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color,
                                 TextAnchor anchor = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var rt = Node(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static Button TextButton(Transform parent, string name, string caption, int fontSize,
                                        Color background, Color foreground, UnityAction onClick)
        {
            var img = Panel(parent, name, background);
            var button = img.gameObject.AddComponent<Button>();
            button.targetGraphic = img;

            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;

            var label = Label(img.transform, "Text", caption, fontSize, foreground, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(label.rectTransform, 10f, 4f, 10f, 4f);

            if (onClick != null) button.onClick.AddListener(onClick);
            return button;
        }

        public static Text CaptionOf(Button b) => b.GetComponentInChildren<Text>();

        public static BarWidget Bar(Transform parent, string name, Color background, Color fill)
        {
            var bg = Panel(parent, name, background);
            bg.raycastTarget = false;
            var fillRt = Node(bg.transform, "Fill");
            var fillImg = fillRt.gameObject.AddComponent<Image>();
            fillImg.sprite = ProcArt.White;
            fillImg.color = fill;
            fillImg.raycastTarget = false;
            var widget = new BarWidget { Background = bg, Fill = fillImg, FillRect = fillRt };
            widget.SetFraction(1f);
            return widget;
        }

        /// <summary>Green above half, amber above a quarter, red below.</summary>
        public static Color HealthColor(float fraction)
        {
            if (fraction > 0.5f) return Good;
            if (fraction > 0.25f) return Accent;
            return Danger;
        }

        public static void SetActive(Component c, bool active)
        {
            if (c != null && c.gameObject.activeSelf != active) c.gameObject.SetActive(active);
        }
    }
}
