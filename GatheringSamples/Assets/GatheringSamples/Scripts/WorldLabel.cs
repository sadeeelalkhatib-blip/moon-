using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GatheringSamples
{
    public class WorldLabel : MonoBehaviour
    {
        public Canvas Canvas;
        public TextMeshProUGUI Text;
        public Image Background;
        public Image Accent;

        public static WorldLabel Create(Transform parent, string name, Vector3 localPosition, string text, Color accent)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            root.transform.localScale = Vector3.one * 0.0065f;

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 20;

            RectTransform rect = root.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(380f, 78f);

            Image background = root.AddComponent<Image>();
            background.color = new Color(0.025f, 0.045f, 0.065f, 0.84f);
            background.raycastTarget = false;

            GameObject accentObject = new GameObject("Accent");
            accentObject.transform.SetParent(root.transform, false);
            Image accentImage = accentObject.AddComponent<Image>();
            accentImage.color = accent;
            accentImage.raycastTarget = false;
            RectTransform accentRect = accentImage.rectTransform;
            accentRect.anchorMin = new Vector2(0f, 0f);
            accentRect.anchorMax = new Vector2(0f, 1f);
            accentRect.offsetMin = new Vector2(0f, 0f);
            accentRect.offsetMax = new Vector2(8f, 0f);

            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(root.transform, false);
            TextMeshProUGUI textMesh = textObject.AddComponent<TextMeshProUGUI>();
            textMesh.font = UiFont.Get();
            textMesh.fontSize = 25f;
            textMesh.alignment = TextAlignmentOptions.Left;
            textMesh.color = new Color(0.94f, 0.97f, 1f);
            textMesh.text = text;
            textMesh.raycastTarget = false;
            textMesh.textWrappingMode = TextWrappingModes.NoWrap;
            RectTransform textRect = textMesh.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(22f, 7f);
            textRect.offsetMax = new Vector2(-12f, -7f);

            WorldLabel label = root.AddComponent<WorldLabel>();
            label.Canvas = canvas;
            label.Text = textMesh;
            label.Background = background;
            label.Accent = accentImage;
            return label;
        }

        public void SetText(string value)
        {
            if (Text != null) Text.text = value;
        }

        public void SetAccent(Color color)
        {
            if (Accent != null) Accent.color = color;
        }

        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                transform.rotation = camera.transform.rotation;
            }
        }
    }
}
