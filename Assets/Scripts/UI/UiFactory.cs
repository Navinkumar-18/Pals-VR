using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace AmbedkarHeritage.UI
{
    /// <summary>
    /// Tiny helper for building clean, Quest-friendly world-space UI out of
    /// legacy Unity UI primitives (no TMP import, no EventSystem required).
    /// All sizes are in canvas pixels; callers control world scale via the
    /// canvas localScale.
    /// </summary>
    public static class UiFactory
    {
        public static Font LoadFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return font != null ? font : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        /// <summary>Creates a world-space canvas parented to <paramref name="parent"/> at local position/rotation.</summary>
        public static Canvas MakeWorldCanvas(string name, Transform parent, Vector3 localPos, Quaternion localRot,
            float width, float height, float scale, Color? background = null)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;

            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform rect = (RectTransform)canvas.transform;
            rect.sizeDelta = new Vector2(width, height);
            rect.localScale = Vector3.one * scale;

            if (background.HasValue)
            {
                Image bg = go.AddComponent<Image>();
                bg.color = background.Value;
                bg.raycastTarget = false;
            }

            return canvas;
        }

        /// <summary>
        /// Invisible physics blocker so rays aimed at a panel cannot reach
        /// exhibits behind it. Non-trigger: both the exhibit highlighter and the
        /// UI pointer raycast through it and find nothing behind. It is offset
        /// slightly behind the button plane so buttons always win the
        /// nearest-hit test.
        /// </summary>
        public static void AddBlockerCollider(Canvas canvas)
        {
            if (canvas == null)
            {
                return;
            }

            RectTransform rect = (RectTransform)canvas.transform;
            BoxCollider collider = canvas.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(
                rect.sizeDelta.x * canvas.transform.lossyScale.x,
                rect.sizeDelta.y * canvas.transform.lossyScale.y,
                0.04f);
            collider.center = new Vector3(0f, 0f, -0.5f); // behind the button plane (local -Z)
        }

        /// <summary>Text centered on a normalized canvas point, in pixels.</summary>
        public static Text MakeLabel(Transform parent, string name, Vector2 centerNorm, Vector2 sizePx,
            int fontSize, TextAnchor alignment, string value, Color color)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = centerNorm;
            rect.anchorMax = centerNorm;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = sizePx;

            Text text = go.AddComponent<Text>();
            text.text = value;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.font = LoadFont();
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        /// <summary>World-space push button with a matching physics collider, centered on a normalized canvas point.</summary>
        public static VRButton MakeButton(Canvas canvas, Transform parent, string name, string label, Vector2 centerNorm,
            Vector2 sizePx, int fontSize, Color bg, UnityAction onClick)
        {
            parent = parent != null ? parent : canvas.transform;

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = centerNorm;
            rect.anchorMax = centerNorm;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = sizePx;

            Image image = go.AddComponent<Image>();
            image.color = bg;
            image.raycastTarget = false;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;

            Text text = MakeLabel(go.transform, "Label", new Vector2(0.5f, 0.5f), sizePx,
                fontSize, TextAnchor.MiddleCenter, label, Color.white);

            VRButton vrButton = go.AddComponent<VRButton>();
            vrButton.Configure(button, text, image);
            vrButton.SetAction(onClick);

            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = new Vector3(
                sizePx.x * canvas.transform.lossyScale.x,
                sizePx.y * canvas.transform.lossyScale.y,
                0.03f);

            return vrButton;
        }
    }
}