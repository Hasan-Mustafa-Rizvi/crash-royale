using CrashRoyale.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace CrashRoyale.EditorTools
{
    /// <summary>Builds uGUI hierarchies in the game's visual style.</summary>
    internal static class UIKit
    {
        public static readonly Color Gold = new Color(1f, 0.82f, 0.3f);
        public static readonly Color Cream = new Color(1f, 0.96f, 0.86f);
        public static readonly Color Muted = new Color(0.82f, 0.76f, 0.9f);
        public static readonly Color Dim = new Color(0.04f, 0.01f, 0.07f, 0.72f);
        public static readonly Color Pink = new Color(0.93f, 0.36f, 0.93f);

        public static Sprite S(string name) => EditorKit.Sprite($"{Paths.UI}/{name}.png");

        public static Canvas Canvas(string name, int order = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        public static void EventSystem()
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform At(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Fill(this RectTransform rt, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Stretches horizontally along the bottom or top edge with a fixed height.</summary>
        public static RectTransform Band(this RectTransform rt, bool bottom, float height, float offset = 0f, float left = 0f, float right = 0f)
        {
            rt.anchorMin = new Vector2(0f, bottom ? 0f : 1f);
            rt.anchorMax = new Vector2(1f, bottom ? 0f : 1f);
            rt.pivot = new Vector2(0.5f, bottom ? 0f : 1f);
            rt.offsetMin = new Vector2(left, 0f);
            rt.offsetMax = new Vector2(-right, 0f);
            rt.anchoredPosition = new Vector2((left - right) * 0.5f, bottom ? offset : -offset);
            rt.sizeDelta = new Vector2(-(left + right), height);
            return rt;
        }

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, bool sliced = false, bool raycast = false)
        {
            var rt = Rect(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.type = sliced ? UnityEngine.UI.Image.Type.Sliced : UnityEngine.UI.Image.Type.Simple;
            image.raycastTarget = raycast;
            if (!sliced && sprite != null) image.preserveAspect = true;
            return image;
        }

        public static Image Panel(Transform parent, string name, string sprite = "panel", Color? color = null, bool raycast = true)
        {
            return Image(parent, name, S(sprite), color ?? Color.white, true, raycast);
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center, bool outlined = true)
        {
            var rt = Rect(parent, name);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = ContentBuilder.Font;
            if (outlined && ContentBuilder.FontOutlined != null) tmp.fontSharedMaterial = ContentBuilder.FontOutlined;
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }

        public static Button Button(Transform parent, string name, string label, string sprite, Vector2 size, float fontSize = 52f)
        {
            var image = Image(parent, name, S(sprite), Color.white, true, true);
            image.rectTransform.sizeDelta = size;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1f, 1f, 1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.85f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            image.gameObject.AddComponent<ButtonSound>();

            if (!string.IsNullOrEmpty(label))
            {
                var text = Text(image.transform, "Label", label, fontSize, Cream);
                text.rectTransform.Fill(16, 16, 6, 12);
                text.enableAutoSizing = true;
                text.fontSizeMin = 20;
                text.fontSizeMax = fontSize;
            }

            return button;
        }

        public static Button IconButton(Transform parent, string name, string background, string icon, Vector2 size, float iconScale = 0.62f)
        {
            var button = Button(parent, name, null, background, size);
            var glyph = Image(button.transform, "Icon", S(icon), Color.white);
            glyph.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), size * iconScale);
            return button;
        }

        /// <summary>Full-screen dimmer that blocks clicks, with a centered panel inside.</summary>
        public static (GameObject root, RectTransform panel) Modal(Transform parent, string name, Vector2 size, string panelSprite = "panel")
        {
            var dim = Image(parent, name, S("white"), Dim, false, true);
            dim.preserveAspect = false;
            dim.rectTransform.Fill();
            var panel = Panel(dim.transform, "Panel", panelSprite);
            panel.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return (dim.gameObject, panel.rectTransform);
        }

        public static Slider Slider(Transform parent, string name, Vector2 size)
        {
            var root = Rect(parent, name);
            root.sizeDelta = size;
            var slider = root.gameObject.AddComponent<Slider>();

            var background = Image(root, "Background", S("rounded_white"), new Color(0.1f, 0.05f, 0.14f, 0.9f), true, true);
            background.rectTransform.Fill(0, 0, size.y * 0.3f, size.y * 0.3f);

            var fillArea = Rect(root, "Fill Area").Fill(size.y * 0.25f, size.y * 0.25f, size.y * 0.3f, size.y * 0.3f);
            var fill = Image(fillArea, "Fill", S("rounded_white"), Pink, true);
            fill.rectTransform.Fill();

            var handleArea = Rect(root, "Handle Slide Area").Fill(size.y * 0.4f, size.y * 0.4f, 0, 0);
            var handle = Image(handleArea, "Handle", S("elixir_drop"), Color.white, false, true);
            handle.rectTransform.sizeDelta = new Vector2(size.y * 1.1f, 0f);
            handle.rectTransform.anchorMin = new Vector2(0f, 0f);
            handle.rectTransform.anchorMax = new Vector2(0f, 1f);

            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.7f;
            return slider;
        }

        public static TMP_InputField Input(Transform parent, string name, Vector2 size, string placeholder)
        {
            var background = Image(parent, name, S("rounded_white"), new Color(0.97f, 0.94f, 1f), true, true);
            background.rectTransform.sizeDelta = size;
            var input = background.gameObject.AddComponent<TMP_InputField>();

            var area = Rect(background.transform, "Text Area").Fill(28, 28, 10, 10);
            area.gameObject.AddComponent<RectMask2D>();

            var hint = Text(area, "Placeholder", placeholder, size.y * 0.42f, new Color(0.45f, 0.4f, 0.55f), TextAlignmentOptions.Left, false);
            hint.rectTransform.Fill();
            hint.fontStyle = FontStyles.Normal;
            var text = Text(area, "Text", string.Empty, size.y * 0.46f, new Color(0.18f, 0.08f, 0.25f), TextAlignmentOptions.Left, false);
            text.rectTransform.Fill();
            text.textWrappingMode = TextWrappingModes.NoWrap;

            input.textViewport = area;
            input.textComponent = text;
            input.placeholder = hint;
            input.fontAsset = ContentBuilder.Font;
            input.pointSize = size.y * 0.46f;
            input.caretColor = new Color(0.4f, 0.2f, 0.5f);
            input.customCaretColor = true;
            input.caretWidth = 4;
            input.lineType = TMP_InputField.LineType.SingleLine;
            return input;
        }

        public static RectTransform IconLabel(Transform parent, string name, string icon, string text, float height, out TextMeshProUGUI label)
        {
            var group = Rect(parent, name);
            var layout = group.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            var fitter = group.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var image = Image(group, "Icon", icon.StartsWith("Assets/") ? EditorKit.Sprite(icon) : S(icon), Color.white);
            var le = image.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = height;
            le.preferredHeight = height;

            label = Text(group, "Value", text, height * 0.72f, Cream, TextAlignmentOptions.Left);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            return group;
        }

        /// <summary>A card face. Returns the view; optional parts are wired when requested.</summary>
        public static CardView Card(Transform parent, string name, Vector2 size, bool showName, bool withLock, bool withCharge, bool withTag, bool clickable)
        {
            var frame = Image(parent, name, S("card_frame_common"), Color.white, true, clickable);
            frame.rectTransform.sizeDelta = size;
            var view = frame.gameObject.AddComponent<CardView>();
            float inset = size.x * 0.06f;

            var portrait = Image(frame.transform, "Portrait", null, Color.white);
            portrait.preserveAspect = false;
            portrait.rectTransform.Fill(inset, inset, inset, inset);

            Image charge = null;
            if (withCharge)
            {
                charge = Image(frame.transform, "Charge", S("card_charge"), Color.white);
                charge.preserveAspect = false;
                charge.type = UnityEngine.UI.Image.Type.Filled;
                charge.fillMethod = UnityEngine.UI.Image.FillMethod.Vertical;
                charge.fillOrigin = (int)UnityEngine.UI.Image.OriginVertical.Top;
                charge.rectTransform.Fill(inset, inset, inset, inset);
            }

            TextMeshProUGUI nameText = null;
            if (showName)
            {
                var band = Image(frame.transform, "NameBand", S("rounded_white"), new Color(0.08f, 0.03f, 0.12f, 0.78f), true);
                band.rectTransform.Band(true, size.y * 0.17f, inset, inset, inset);
                nameText = Text(band.transform, "Name", "Card", size.y * 0.1f, Cream);
                nameText.rectTransform.Fill(6, 6, 2, 4);
                nameText.enableAutoSizing = true;
                nameText.fontSizeMin = 14;
                nameText.fontSizeMax = size.y * 0.1f;
            }

            var badge = Image(frame.transform, "Cost", S("elixir_drop"), Color.white);
            float badgeSize = size.x * 0.36f;
            badge.rectTransform.At(new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(badgeSize * 0.32f, -badgeSize * 0.3f), new Vector2(badgeSize, badgeSize));
            var cost = Text(badge.transform, "Value", "3", badgeSize * 0.52f, Color.white);
            cost.rectTransform.Fill(0, 0, badgeSize * 0.18f, 0);

            var highlight = Image(frame.transform, "Highlight", S("card_glow"), Gold, true);
            highlight.rectTransform.Fill(-12, -12, -12, -12);
            highlight.gameObject.SetActive(false);

            GameObject lockOverlay = null;
            TextMeshProUGUI lockText = null;
            if (withLock)
            {
                var dark = Image(frame.transform, "Lock", S("rounded_white"), new Color(0.05f, 0.02f, 0.08f, 0.62f), true);
                dark.rectTransform.Fill(inset, inset, inset, inset);
                var icon = Image(dark.transform, "Icon", S("lock"), Color.white);
                icon.rectTransform.At(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, size.y * 0.13f), Vector2.one * size.x * 0.4f);
                var priceRow = IconLabel(dark.transform, "Price", "coin", "250", size.y * 0.13f, out lockText);
                priceRow.At(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, size.y * 0.24f), new Vector2(size.x * 0.7f, size.y * 0.13f));
                priceRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
                lockOverlay = dark.gameObject;
                lockOverlay.SetActive(false);
            }

            GameObject tag = null;
            TextMeshProUGUI tagText = null;
            if (withTag)
            {
                var pill = Image(frame.transform, "Tag", S("pill"), new Color(0.3f, 0.75f, 0.35f), true);
                pill.rectTransform.At(new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -4f), new Vector2(size.x * 0.78f, size.y * 0.13f));
                tagText = Text(pill.transform, "Text", "IN DECK", size.y * 0.075f, Color.white);
                tagText.rectTransform.Fill(4, 4, 2, 4);
                tag = pill.gameObject;
                tag.SetActive(false);
            }

            if (clickable)
            {
                var button = frame.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                frame.gameObject.AddComponent<ButtonSound>();
            }

            EditorKit.Set(view, "frame", frame);
            EditorKit.Set(view, "portrait", portrait);
            EditorKit.Set(view, "costBadge", badge.gameObject);
            EditorKit.Set(view, "costText", cost);
            EditorKit.Set(view, "nameText", nameText);
            EditorKit.Set(view, "lockOverlay", lockOverlay);
            EditorKit.Set(view, "lockText", lockText);
            EditorKit.Set(view, "highlight", highlight.gameObject);
            EditorKit.Set(view, "cornerTag", tag);
            EditorKit.Set(view, "cornerTagText", tagText);
            EditorKit.Set(view, "chargeOverlay", charge);
            EditorKit.Set(view, "rarityFrames", new Object[] { S("card_frame_common"), S("card_frame_rare"), S("card_frame_epic") });
            return view;
        }

        public static GameObject SafeArea(Transform canvas)
        {
            var safe = Rect(canvas, "SafeArea").Fill();
            safe.gameObject.AddComponent<SafeArea>();
            return safe.gameObject;
        }

        public static void Background(Transform canvas, string spritePath, Color tint, float aspect)
        {
            var image = Image(canvas, "Background", EditorKit.Sprite(spritePath), tint);
            image.preserveAspect = false;
            image.rectTransform.Fill();
            var fitter = image.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = aspect;
        }

        public static void MarkDirty(Object o) => EditorUtility.SetDirty(o);
    }
}
