using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Reachoreo
{
    /// <summary>
    /// Small helpers that build Reachoreo's in-VR interface from code: calm dark panels, coral
    /// for what's selected, rounded cards, readable text. Units are pixels on a world-space canvas
    /// scaled so 1 px = 1 mm.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color Bg = new Color(0.09f, 0.09f, 0.10f, 1f);
        public static readonly Color Card = new Color(0.17f, 0.17f, 0.19f, 1f);
        public static readonly Color CardDim = new Color(0.13f, 0.13f, 0.145f, 1f);
        public static readonly Color Coral = new Color(0.90f, 0.47f, 0.36f, 1f);
        public static readonly Color Teal = new Color(0.33f, 0.62f, 0.70f, 1f);
        public static readonly Color Ink = new Color(0.95f, 0.94f, 0.92f, 1f);
        public static readonly Color Mute = new Color(0.66f, 0.65f, 0.63f, 1f);
        public static readonly Color Faint = new Color(0.42f, 0.42f, 0.44f, 1f);
        public static readonly Color Good = new Color(0.45f, 0.78f, 0.52f, 1f);
        public static readonly Color Warn = new Color(0.93f, 0.74f, 0.35f, 1f);

        static Sprite rounded;

        /// <summary>A white rounded rectangle, 9-sliced, made at run time (no image files needed).</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int s = 64, r = 18;
                var tex = new Texture2D(s, s, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[s * s];
                for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float dx = Mathf.Max(r - x - 0.5f, 0f, x + 0.5f - (s - r));
                        float dy = Mathf.Max(r - y - 0.5f, 0f, y + 0.5f - (s - r));
                        float a = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                        px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                tex.SetPixels32(px); tex.Apply();
                rounded = Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return rounded;
            }
        }

        public static Canvas WorldCanvas(Transform parent, string name, Vector2 sizePx, Vector3 localPos, Quaternion localRot)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = sizePx; rt.localPosition = localPos; rt.localRotation = localRot; rt.localScale = Vector3.one * 0.001f;
            var c = go.AddComponent<Canvas>(); c.renderMode = RenderMode.WorldSpace;
            go.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 3f;
            go.AddComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
            return c;
        }

        public static RectTransform Box(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        }

        public static Image Fill(RectTransform rt, Color c, bool roundedCorners = true)
        {
            var img = rt.gameObject.AddComponent<Image>();
            if (roundedCorners) { img.sprite = Rounded; img.type = Image.Type.Sliced; }
            img.color = c;
            return img;
        }

        public static LayoutElement Size(Component c, float w = -1, float h = -1, float flexW = -1, float flexH = -1)
        {
            var le = c.GetComponent<LayoutElement>() ?? c.gameObject.AddComponent<LayoutElement>();
            if (w >= 0) { le.minWidth = w; le.preferredWidth = w; }
            if (h >= 0) { le.minHeight = h; le.preferredHeight = h; }
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }

        public static RectTransform VStack(Transform parent, string name, float spacing = 12, int pad = 0, TextAnchor align = TextAnchor.UpperLeft)
        {
            var rt = Box(parent, name);
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing; v.padding = new RectOffset(pad, pad, pad, pad); v.childAlignment = align;
            v.childControlWidth = true; v.childControlHeight = true; v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return rt;
        }

        public static RectTransform HStack(Transform parent, string name, float spacing = 12, int pad = 0, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var rt = Box(parent, name);
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing; h.padding = new RectOffset(pad, pad, pad, pad); h.childAlignment = align;
            h.childControlWidth = true; h.childControlHeight = true; h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return rt;
        }

        /// <summary>Empty space that grows along a row (horizontal) or a column (vertical).</summary>
        public static RectTransform Spacer(Transform parent, bool vertical = false)
        {
            var rt = Box(parent, "Spacer");
            if (vertical) Size(rt, flexW: 0, flexH: 1); else Size(rt, flexW: 1, flexH: 0);
            return rt;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float size = 24, Color? color = null, bool bold = false,
                                           TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var rt = Box(parent, "Text");
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = TMP_Settings.defaultFontAsset;
            t.text = text; t.fontSize = size; t.color = color ?? Ink;
            t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            t.alignment = align; t.textWrappingMode = TextWrappingModes.Normal; t.raycastTarget = false;
            return t;
        }

        public enum Style { Primary, Secondary, Quiet }

        public static Button Button(Transform parent, string label, Action onClick, Style style = Style.Secondary, float w = -1, float h = 64, float fontSize = 24)
        {
            var rt = Box(parent, "Button " + label);
            var img = Fill(rt, style == Style.Primary ? Coral : style == Style.Secondary ? Card : new Color(1, 1, 1, 0.04f));
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors; cb.normalColor = Color.white; cb.highlightedColor = new Color(1.18f, 1.18f, 1.18f, 1f);
            cb.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); cb.selectedColor = Color.white; cb.disabledColor = new Color(1, 1, 1, 0.35f); cb.fadeDuration = 0.08f;
            b.colors = cb;
            var nav = b.navigation; nav.mode = Navigation.Mode.None; b.navigation = nav;
            if (onClick != null) b.onClick.AddListener(() => onClick());
            var t = Text(rt, label, fontSize, Ink, true, TextAlignmentOptions.Center);
            Stretch((RectTransform)t.transform, 6);
            t.name = "Label";
            Size(rt, w, h, w < 0 ? 1 : 0);
            return b;
        }

        public static void SetLabel(Button b, string label)
        {
            var t = b.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (t != null) t.text = label;
        }

        public static void SetSelected(Button b, bool on, Color? onColor = null)
        {
            var img = b.targetGraphic as Image;
            if (img != null) img.color = on ? (onColor ?? Coral) : Card;
        }

        public static Slider Slider(Transform parent, float value, Action<float> onChange)
        {
            var root = Box(parent, "Slider");
            Size(root, h: 48, flexW: 1);
            var bg = Box(root, "Track"); bg.anchorMin = new Vector2(0, 0.5f); bg.anchorMax = new Vector2(1, 0.5f); bg.sizeDelta = new Vector2(0, 12);
            Fill(bg, CardDim).raycastTarget = true;
            var fillArea = Box(root, "Fill Area"); fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f);
            fillArea.offsetMin = new Vector2(0, -6); fillArea.offsetMax = new Vector2(0, 6);
            var fill = Box(fillArea, "Fill"); fill.anchorMin = Vector2.zero; fill.anchorMax = new Vector2(0, 1); fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
            Fill(fill, Coral);
            var handleArea = Box(root, "Handle Area"); Stretch(handleArea); handleArea.offsetMin = new Vector2(18, 0); handleArea.offsetMax = new Vector2(-18, 0);
            var handle = Box(handleArea, "Handle"); handle.sizeDelta = new Vector2(34, -12);
            var himg = Fill(handle, Ink);
            var s = root.gameObject.AddComponent<Slider>();
            s.fillRect = fill; s.handleRect = handle; s.targetGraphic = himg; s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = 0; s.maxValue = 1; s.value = value;
            var nav = s.navigation; nav.mode = Navigation.Mode.None; s.navigation = nav;
            s.onValueChanged.AddListener(v => onChange(v));
            return s;
        }

        /// <summary>A labelled row: name on the left, control(s) on the right.</summary>
        public static RectTransform Row(Transform parent, string label, float labelWidth = 230)
        {
            var row = HStack(parent, "Row " + label, 14);
            Size(row, h: 56);
            var t = Text(row, label, 22, Mute, false, TextAlignmentOptions.MidlineLeft);
            Size(t, labelWidth, 56);
            return row;
        }

        public static RectTransform Panel(Transform parent, string name, Color color, int pad = 20, float spacing = 12)
        {
            var rt = VStack(parent, name, spacing, pad);
            Fill(rt, color);
            return rt;
        }

        public static Image Dot(Transform parent, Color c, float size = 18)
        {
            var rt = Box(parent, "Dot");
            Size(rt, size, size);
            return Fill(rt, c);
        }
    }
}
