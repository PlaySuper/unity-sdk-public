using UnityEngine;
using UnityEngine.UI;

namespace PlaySuperUnity
{
    /// <summary>
    /// Branded loading screen shown during OpenStore() between the user tap
    /// and the native WebView painting. Self-contained — uses a procedurally
    /// generated arc texture so no assets need to ship with the SDK.
    ///
    /// Visual matches the store-side EntryLoader so the transition from
    /// SDK loader to in-WebView HTML loader is invisible to the user.
    /// </summary>
    internal static class LoadingScreen
    {
        private static GameObject root;
        private static Texture2D spinnerTexture;

        // Matches the store's Tailwind purple-500 (#A855F7) used in the
        // store-side EntryLoader / SpinnerOverlay components.
        private static readonly Color SpinnerColor =
            new Color(0xA8 / 255f, 0x55 / 255f, 0xF7 / 255f, 1f);
        private static readonly Color TextColor =
            new Color(0.15f, 0.15f, 0.15f, 1f);
        private const string LoadingText = "Amazing rewards await...";

        public static void Show()
        {
            if (root != null) return;

            try
            {
                root = new GameObject("PlaySuperLoadingScreen");
                Object.DontDestroyOnLoad(root);

                var canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 32767; // above all other UI

                var scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920);
                scaler.matchWidthOrHeight = 0.5f;

                root.AddComponent<GraphicRaycaster>();

                BuildBackground(root.transform);
                var spinnerRT = BuildSpinner(root.transform);
                BuildText(root.transform);

                var rotator = root.AddComponent<SpinnerRotator>();
                rotator.target = spinnerRT;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PlaySuper] LoadingScreen.Show failed: {ex.Message}");
                Hide();
            }
        }

        public static void Hide()
        {
            if (root != null)
            {
                Object.Destroy(root);
                root = null;
            }
        }

        private static void BuildBackground(Transform parent)
        {
            var go = new GameObject("Background");
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<Image>();
            image.color = Color.white;

            var rt = image.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static RectTransform BuildSpinner(Transform parent)
        {
            if (spinnerTexture == null)
                spinnerTexture = GenerateArcTexture(128, 12);

            var go = new GameObject("Spinner");
            go.transform.SetParent(parent, false);

            var image = go.AddComponent<RawImage>();
            image.texture = spinnerTexture;
            image.color = SpinnerColor;
            image.raycastTarget = false;

            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(120, 120);
            rt.anchoredPosition = new Vector2(0, 40);
            return rt;
        }

        private static void BuildText(Transform parent)
        {
            var go = new GameObject("LoadingText");
            go.transform.SetParent(parent, false);

            var text = go.AddComponent<Text>();
            text.font = LoadDefaultFont();
            text.text = LoadingText;
            text.fontSize = 32;
            text.color = TextColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            var rt = text.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(800, 60);
            rt.anchoredPosition = new Vector2(0, -80);
        }

        private static Font LoadDefaultFont()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null) return font;
            return Resources.GetBuiltinResource(typeof(Font), "Arial.ttf") as Font;
        }

        /// <summary>
        /// Procedural 270° arc texture — visually similar to react-spinners'
        /// ClipLoader (a ring with a quarter cut out). Caller tints via
        /// RawImage.color so this can be reused across colors.
        /// </summary>
        private static Texture2D GenerateArcTexture(int size, int strokeWidth)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;

            var pixels = new Color32[size * size];
            float center = (size - 1) / 2f;
            float outerRadius = center - 1f;
            float innerRadius = outerRadius - strokeWidth;

            const float arcDegrees = 270f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist < innerRadius - 1f || dist > outerRadius + 1f)
                    {
                        pixels[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (angle < 0f) angle += 360f;

                    if (angle > arcDegrees)
                    {
                        pixels[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float edgeAlpha = 1f;
                    if (dist > outerRadius)
                        edgeAlpha = 1f - (dist - outerRadius);
                    else if (dist < innerRadius)
                        edgeAlpha = 1f - (innerRadius - dist);
                    edgeAlpha = Mathf.Clamp01(edgeAlpha);

                    pixels[y * size + x] = new Color32(
                        255, 255, 255,
                        (byte)Mathf.RoundToInt(255f * edgeAlpha));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }

    internal class SpinnerRotator : MonoBehaviour
    {
        public RectTransform target;
        private const float RotationSpeed = 360f; // degrees per second

        private void Update()
        {
            if (target != null)
                target.Rotate(0f, 0f, -RotationSpeed * Time.unscaledDeltaTime);
        }
    }
}
