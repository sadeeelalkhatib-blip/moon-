using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GatheringSamples
{
    /// <summary>
    /// Runtime uGUI HUD, briefing, pause, field guide and end-of-mission screens.
    /// Everything is built in code so the scene stays self-contained.
    /// </summary>
    public class GameUI : MonoBehaviour
    {
        private struct ToolSlot
        {
            public string Id;
            public Image Background;
            public TextMeshProUGUI Label;
            public Image Accent;
        }

        private static readonly Color PanelColor = new Color(0.018f, 0.03f, 0.045f, 0.9f);
        private static readonly Color PanelSoft = new Color(0.028f, 0.045f, 0.063f, 0.94f);
        private static readonly Color Accent = new Color(0.38f, 0.78f, 1f);
        private static readonly Color AccentWarm = new Color(1f, 0.68f, 0.3f);
        private static readonly Color TextMain = new Color(0.9f, 0.94f, 0.98f);
        private static readonly Color TextDim = new Color(0.6f, 0.68f, 0.76f);
        private static readonly Color Success = new Color(0.56f, 0.89f, 0.65f);
        private static readonly Color Danger = new Color(1f, 0.35f, 0.27f);

        private GameManager manager;
        private Canvas canvas;
        private RectTransform hudRoot;

        private TextMeshProUGUI clockText;
        private TextMeshProUGUI oxygenText;
        private Image oxygenFill;
        private TextMeshProUGUI manifestHeaderText;
        private TextMeshProUGUI manifestText;
        private TextMeshProUGUI objectiveText;

        private readonly List<ToolSlot> toolSlots = new List<ToolSlot>();
        private string selectedToolId = "scoop";

        private RectTransform toastRoot;
        private CanvasGroup toastGroup;
        private TextMeshProUGUI toastText;
        private Image toastAccent;
        private float toastTimer;

        private RectTransform warningRoot;
        private Image warningBackground;
        private Image warningAccent;
        private TextMeshProUGUI warningText;
        private Color warningBaseColor = AccentWarm;

        private RawImage vignetteImage;
        private float vignetteAmount;

        private RectTransform promptRoot;
        private Image promptBackground;
        private Image promptAccent;
        private TextMeshProUGUI promptKeyText;
        private TextMeshProUGUI promptTitleText;
        private TextMeshProUGUI promptSubText;
        private RectTransform promptProgressRoot;
        private Image promptProgressFill;

        private RectTransform briefingRoot;
        private RectTransform pauseRoot;
        private RectTransform endRoot;
        private Image endAccentImage;
        private TextMeshProUGUI endTitleText;
        private TextMeshProUGUI endReasonText;
        private TextMeshProUGUI endStatsText;
        private TextMeshProUGUI endPrimaryLabel;
        private TextMeshProUGUI endSecondaryLabel;

        private RectTransform guideRoot;
        private ScrollRect guideScroll;
        private TextMeshProUGUI guideBody;
        private readonly List<Image> guideTabBackgrounds = new List<Image>();
        private readonly List<TextMeshProUGUI> guideTabLabels = new List<TextMeshProUGUI>();
        private int guideTab;

        public Canvas Canvas => canvas;

        public void Build(GameManager gameManager)
        {
            manager = gameManager;

            GameObject canvasObject = new GameObject("UI Canvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            BuildHud();
            BuildBriefing();
            BuildPause();
            BuildEndScreen();
            BuildGuide();

            guideTab = 0;
            SetGuideTab(0);
            SetGuideVisible(false);
            SetPauseVisible(false);
            SetEndVisible(false);
            SetBriefingVisible(true);
            HideInteraction();
            SetVignetteAlpha(0f);
            SetWarning(null, AccentWarm);
        }

        private void Update()
        {
            if (vignetteImage != null)
            {
                float pulse = 0.78f + 0.22f * Mathf.Sin(Time.unscaledTime * 5.2f);
                vignetteImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(vignetteAmount * pulse));
            }

            if (warningRoot != null && warningRoot.gameObject.activeSelf)
            {
                float pulse = 0.86f + 0.14f * Mathf.Sin(Time.unscaledTime * 6.4f);
                Color color = warningBaseColor;
                color.a *= pulse;
                warningBackground.color = color;
            }

            if (toastTimer > 0f)
            {
                toastTimer -= Time.unscaledDeltaTime;
                toastGroup.alpha = toastTimer < 0.5f ? Mathf.Clamp01(toastTimer / 0.5f) : 1f;
                if (toastTimer <= 0f)
                {
                    toastRoot.gameObject.SetActive(false);
                }
            }
        }

        // ------------------------------------------------------------------ HUD

        private void BuildHud()
        {
            hudRoot = CreatePanel("HUD", canvas.transform, new Color(0f, 0f, 0f, 0f), false);
            Stretch(hudRoot);

            RectTransform vignetteRect = CreateRect("Oxygen Vignette", hudRoot);
            Stretch(vignetteRect);
            vignetteImage = vignetteRect.gameObject.AddComponent<RawImage>();
            vignetteImage.texture = RuntimeAssets.VignetteTexture;
            vignetteImage.raycastTarget = false;
            vignetteImage.color = new Color(1f, 1f, 1f, 0f);

            RectTransform crosshair = CreateRect("Crosshair", hudRoot);
            Center(crosshair, new Vector2(6f, 6f), Vector2.zero);
            Image crosshairImage = AddImage(crosshair, new Color(0.9f, 0.95f, 1f, 0.42f), false);
            crosshairImage.sprite = RuntimeAssets.CircleSprite;

            BuildVitalsPanel();
            BuildManifestPanel();
            BuildToolBelt();
            BuildControlsHint();
            BuildInteractionPrompt();
            BuildWarningBanner();
            BuildToast();
        }

        private void BuildVitalsPanel()
        {
            RectTransform panel = CreatePanel("Vitals", hudRoot, PanelColor, false);
            TopLeft(panel, new Vector2(24f, -24f), new Vector2(392f, 154f));

            Image accent = AddImage(CreateRect("Accent", panel), Accent, false);
            accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            accent.rectTransform.offsetMin = Vector2.zero;
            accent.rectTransform.offsetMax = new Vector2(4f, 0f);

            TextMeshProUGUI title = CreateText(panel, "Title", "LUNA-1 · SURFACE EVA", 16f,
                new Color(0.72f, 0.85f, 0.96f), TextAlignmentOptions.Left);
            TopLeft(title.rectTransform, new Vector2(18f, -10f), new Vector2(350f, 22f));
            title.characterSpacing = 2.5f;

            TextMeshProUGUI clockLabel = CreateText(panel, "Clock Label", "MISSION CLOCK", 11f, TextDim,
                TextAlignmentOptions.Left);
            TopLeft(clockLabel.rectTransform, new Vector2(18f, -40f), new Vector2(170f, 18f));

            clockText = CreateText(panel, "Clock", "08:00", 32f, TextMain, TextAlignmentOptions.Left);
            TopLeft(clockText.rectTransform, new Vector2(18f, -56f), new Vector2(170f, 40f));
            clockText.fontStyle = FontStyles.Bold;

            TextMeshProUGUI o2Label = CreateText(panel, "Oxygen Label", "OXYGEN", 11f, TextDim,
                TextAlignmentOptions.Right);
            TopLeft(o2Label.rectTransform, new Vector2(204f, -40f), new Vector2(168f, 18f));

            oxygenText = CreateText(panel, "Oxygen", "100%", 32f, TextMain, TextAlignmentOptions.Right);
            TopLeft(oxygenText.rectTransform, new Vector2(204f, -56f), new Vector2(168f, 40f));
            oxygenText.fontStyle = FontStyles.Bold;

            RectTransform o2BarBackground = CreateRect("Oxygen Bar", panel);
            TopLeft(o2BarBackground, new Vector2(18f, -110f), new Vector2(354f, 12f));
            AddImage(o2BarBackground, new Color(0.08f, 0.12f, 0.16f, 0.95f), false);

            RectTransform o2BarFill = CreateRect("Oxygen Fill", o2BarBackground);
            Stretch(o2BarFill);
            oxygenFill = AddImage(o2BarFill, Success, false);
            oxygenFill.type = Image.Type.Filled;
            oxygenFill.fillMethod = Image.FillMethod.Horizontal;
            oxygenFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            oxygenFill.fillAmount = 1f;
        }

        private void BuildManifestPanel()
        {
            RectTransform panel = CreatePanel("Manifest", hudRoot, PanelColor, false);
            TopRight(panel, new Vector2(-24f, -24f), new Vector2(414f, 292f));

            Image accent = AddImage(CreateRect("Accent", panel), AccentWarm, false);
            accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            accent.rectTransform.offsetMin = Vector2.zero;
            accent.rectTransform.offsetMax = new Vector2(4f, 0f);

            manifestHeaderText = CreateText(panel, "Header", "SAMPLE MANIFEST  0 / 5", 15f,
                new Color(0.85f, 0.9f, 0.96f), TextAlignmentOptions.Left);
            TopLeft(manifestHeaderText.rectTransform, new Vector2(18f, -12f), new Vector2(376f, 22f));
            manifestHeaderText.characterSpacing = 1.5f;

            manifestText = CreateText(panel, "Manifest", string.Empty, 15f, TextMain, TextAlignmentOptions.TopLeft);
            TopLeft(manifestText.rectTransform, new Vector2(18f, -42f), new Vector2(376f, 142f));
            manifestText.lineSpacing = 8f;

            RectTransform divider = CreateRect("Divider", panel);
            TopLeft(divider, new Vector2(18f, -192f), new Vector2(376f, 1f));
            AddImage(divider, new Color(0.22f, 0.3f, 0.38f, 0.65f), false);

            TextMeshProUGUI objectiveHeader = CreateText(panel, "Objective Header", "OBJECTIVE", 11f, TextDim,
                TextAlignmentOptions.Left);
            TopLeft(objectiveHeader.rectTransform, new Vector2(18f, -204f), new Vector2(376f, 18f));
            objectiveHeader.characterSpacing = 2f;

            objectiveText = CreateText(panel, "Objective", string.Empty, 14f,
                new Color(0.78f, 0.85f, 0.92f), TextAlignmentOptions.TopLeft);
            TopLeft(objectiveText.rectTransform, new Vector2(18f, -226f), new Vector2(376f, 52f));
        }

        private void BuildToolBelt()
        {
            RectTransform panel = CreatePanel("Tool Belt", hudRoot, new Color(0.014f, 0.024f, 0.036f, 0.93f), false);
            panel.anchorMin = new Vector2(0.5f, 0f);
            panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, 24f);
            panel.sizeDelta = new Vector2(858f, 96f);

            TextMeshProUGUI title = CreateText(panel, "Title", "TOOL BELT", 11f, TextDim, TextAlignmentOptions.Left);
            TopLeft(title.rectTransform, new Vector2(16f, -8f), new Vector2(300f, 18f));
            title.characterSpacing = 2f;

            for (int i = 0; i < Content.Tools.Length; i++)
            {
                ToolDef tool = Content.Tools[i];
                RectTransform slot = CreateRect("Tool " + tool.key, panel);
                TopLeft(slot, new Vector2(16f + i * 139f, -33f), new Vector2(131f, 51f));

                Image background = AddImage(slot, new Color(0.05f, 0.075f, 0.1f, 0.95f), true);
                Button button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                string capturedId = tool.id;
                button.onClick.AddListener(() =>
                {
                    if (manager != null && manager.Audio != null) manager.Audio.Click();
                    if (manager != null) manager.SelectTool(capturedId);
                });

                Image accent = AddImage(CreateRect("Selected", slot), new Color(0f, 0f, 0f, 0f), false);
                accent.rectTransform.anchorMin = new Vector2(0f, 0f);
                accent.rectTransform.anchorMax = new Vector2(1f, 0f);
                accent.rectTransform.pivot = new Vector2(0.5f, 0f);
                accent.rectTransform.offsetMin = Vector2.zero;
                accent.rectTransform.offsetMax = new Vector2(0f, 3f);

                TextMeshProUGUI label = CreateText(slot, "Label", tool.key + "  " + tool.name, 15f,
                    new Color(0.74f, 0.81f, 0.88f), TextAlignmentOptions.Center);
                Stretch(label.rectTransform, 4f, 4f, 4f, 4f);

                toolSlots.Add(new ToolSlot
                {
                    Id = tool.id,
                    Background = background,
                    Label = label,
                    Accent = accent
                });
            }
        }

        private void BuildControlsHint()
        {
            RectTransform panel = CreatePanel("Controls", hudRoot, new Color(0.018f, 0.03f, 0.043f, 0.74f), false);
            TopLeft(panel, new Vector2(24f, -194f), new Vector2(392f, 88f));

            TextMeshProUGUI title = CreateText(panel, "Title", "FIELD CONTROLS", 10.5f, TextDim,
                TextAlignmentOptions.Left);
            TopLeft(title.rectTransform, new Vector2(16f, -8f), new Vector2(360f, 16f));
            title.characterSpacing = 2f;

            TextMeshProUGUI body = CreateText(panel, "Body",
                "WASD move  ·  Shift lope  ·  Space jump\n" +
                "RMB orbit  ·  Wheel zoom  ·  E use tool\n" +
                "1–6 select tool  ·  Tab field guide  ·  Esc pause",
                13f, new Color(0.7f, 0.78f, 0.86f), TextAlignmentOptions.Left);
            TopLeft(body.rectTransform, new Vector2(16f, -26f), new Vector2(360f, 56f));
            body.lineSpacing = 3f;
        }

        private void BuildInteractionPrompt()
        {
            promptRoot = CreatePanel("Interaction Prompt", hudRoot, new Color(0.015f, 0.027f, 0.04f, 0.94f), false);
            promptRoot.anchorMin = new Vector2(0.5f, 0f);
            promptRoot.anchorMax = new Vector2(0.5f, 0f);
            promptRoot.pivot = new Vector2(0.5f, 0f);
            promptRoot.anchoredPosition = new Vector2(0f, 138f);
            promptRoot.sizeDelta = new Vector2(730f, 84f);

            promptBackground = promptRoot.GetComponent<Image>();
            promptAccent = AddImage(CreateRect("Accent", promptRoot), Accent, false);
            promptAccent.rectTransform.anchorMin = new Vector2(0f, 0f);
            promptAccent.rectTransform.anchorMax = new Vector2(0f, 1f);
            promptAccent.rectTransform.pivot = new Vector2(0f, 0.5f);
            promptAccent.rectTransform.offsetMin = Vector2.zero;
            promptAccent.rectTransform.offsetMax = new Vector2(4f, 0f);

            RectTransform keyBadge = CreateRect("Key", promptRoot);
            TopLeft(keyBadge, new Vector2(18f, -18f), new Vector2(46f, 46f));
            AddImage(keyBadge, new Color(0.08f, 0.15f, 0.2f, 0.96f), false);

            promptKeyText = CreateText(keyBadge, "Key Label", "E", 23f, Accent, TextAlignmentOptions.Center);
            Stretch(promptKeyText.rectTransform);
            promptKeyText.fontStyle = FontStyles.Bold;

            promptTitleText = CreateText(promptRoot, "Title", string.Empty, 18f, TextMain,
                TextAlignmentOptions.Left);
            TopLeft(promptTitleText.rectTransform, new Vector2(80f, -12f), new Vector2(630f, 28f));

            promptSubText = CreateText(promptRoot, "Subtitle", string.Empty, 13f, TextDim,
                TextAlignmentOptions.Left);
            TopLeft(promptSubText.rectTransform, new Vector2(80f, -40f), new Vector2(630f, 22f));

            promptProgressRoot = CreateRect("Progress", promptRoot);
            TopLeft(promptProgressRoot, new Vector2(80f, -66f), new Vector2(630f, 5f));
            AddImage(promptProgressRoot, new Color(0.08f, 0.12f, 0.16f, 0.9f), false);

            RectTransform fill = CreateRect("Fill", promptProgressRoot);
            Stretch(fill);
            promptProgressFill = AddImage(fill, Accent, false);
            promptProgressFill.type = Image.Type.Filled;
            promptProgressFill.fillMethod = Image.FillMethod.Horizontal;
            promptProgressFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            promptProgressFill.fillAmount = 0f;

            promptRoot.gameObject.SetActive(false);
        }

        private void BuildWarningBanner()
        {
            warningRoot = CreatePanel("Warning Banner", hudRoot, AccentWarm, false);
            warningRoot.anchorMin = new Vector2(0.5f, 1f);
            warningRoot.anchorMax = new Vector2(0.5f, 1f);
            warningRoot.pivot = new Vector2(0.5f, 1f);
            warningRoot.anchoredPosition = new Vector2(0f, -18f);
            warningRoot.sizeDelta = new Vector2(760f, 42f);
            warningBackground = warningRoot.GetComponent<Image>();
            warningBackground.raycastTarget = false;

            warningAccent = AddImage(CreateRect("Accent", warningRoot), Color.white, false);
            warningAccent.rectTransform.anchorMin = new Vector2(0f, 0f);
            warningAccent.rectTransform.anchorMax = new Vector2(0f, 1f);
            warningAccent.rectTransform.pivot = new Vector2(0f, 0.5f);
            warningAccent.rectTransform.offsetMin = Vector2.zero;
            warningAccent.rectTransform.offsetMax = new Vector2(4f, 0f);

            warningText = CreateText(warningRoot, "Text", string.Empty, 16f, new Color(0.04f, 0.05f, 0.065f),
                TextAlignmentOptions.Center);
            Stretch(warningText.rectTransform, 16f, 0f, 16f, 0f);
            warningText.fontStyle = FontStyles.Bold;
            warningRoot.gameObject.SetActive(false);
        }

        private void BuildToast()
        {
            toastRoot = CreatePanel("Toast", hudRoot, new Color(0.016f, 0.03f, 0.044f, 0.95f), false);
            toastRoot.anchorMin = new Vector2(0.5f, 1f);
            toastRoot.anchorMax = new Vector2(0.5f, 1f);
            toastRoot.pivot = new Vector2(0.5f, 1f);
            toastRoot.anchoredPosition = new Vector2(0f, -70f);
            toastRoot.sizeDelta = new Vector2(720f, 46f);
            toastGroup = toastRoot.gameObject.AddComponent<CanvasGroup>();
            toastGroup.blocksRaycasts = false;
            toastGroup.interactable = false;

            toastAccent = AddImage(CreateRect("Accent", toastRoot), Accent, false);
            toastAccent.rectTransform.anchorMin = new Vector2(0f, 0f);
            toastAccent.rectTransform.anchorMax = new Vector2(0f, 1f);
            toastAccent.rectTransform.pivot = new Vector2(0f, 0.5f);
            toastAccent.rectTransform.offsetMin = Vector2.zero;
            toastAccent.rectTransform.offsetMax = new Vector2(4f, 0f);

            toastText = CreateText(toastRoot, "Text", string.Empty, 15f, TextMain, TextAlignmentOptions.Center);
            Stretch(toastText.rectTransform, 18f, 0f, 18f, 0f);
            toastRoot.gameObject.SetActive(false);
        }

        // -------------------------------------------------------------- overlays

        private void BuildBriefing()
        {
            briefingRoot = CreatePanel("Briefing", canvas.transform, new Color(0.006f, 0.01f, 0.018f, 0.76f), true);
            Stretch(briefingRoot);

            RectTransform card = CreatePanel("Briefing Card", briefingRoot, new Color(0.018f, 0.03f, 0.045f, 0.97f), true);
            Center(card, new Vector2(1030f, 650f), new Vector2(0f, -8f));

            Image accent = AddImage(CreateRect("Accent", card), Accent, false);
            accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            accent.rectTransform.offsetMin = Vector2.zero;
            accent.rectTransform.offsetMax = new Vector2(6f, 0f);

            TextMeshProUGUI title = CreateText(card, "Title", "GATHERING SAMPLES", 52f,
                new Color(0.93f, 0.96f, 1f), TextAlignmentOptions.Left);
            TopLeft(title.rectTransform, new Vector2(52f, -38f), new Vector2(860f, 66f));
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 2f;

            TextMeshProUGUI subtitle = CreateText(card, "Subtitle", "LUNA-1 · SURFACE EVA · SAMPLE COLLECTION",
                18f, Accent, TextAlignmentOptions.Left);
            TopLeft(subtitle.rectTransform, new Vector2(54f, -108f), new Vector2(900f, 28f));
            subtitle.characterSpacing = 3f;

            RectTransform divider = CreateRect("Divider", card);
            TopLeft(divider, new Vector2(52f, -146f), new Vector2(920f, 1f));
            AddImage(divider, new Color(0.24f, 0.33f, 0.42f, 0.7f), false);

            TextMeshProUGUI brief = CreateText(card, "Brief",
                "You are outside LUNA-1 on the lunar surface. Five sample stations have been " +
                "marked across the landing site. Each material needs a different field tool, and " +
                "the samples must be documented and sealed before you return to the lander.",
                19f, new Color(0.82f, 0.88f, 0.94f), TextAlignmentOptions.TopLeft);
            TopLeft(brief.rectTransform, new Vector2(52f, -168f), new Vector2(920f, 108f));
            brief.lineSpacing = 6f;

            TextMeshProUGUI taskHeader = CreateText(card, "Task Header", "YOUR TASK", 12f, TextDim,
                TextAlignmentOptions.Left);
            TopLeft(taskHeader.rectTransform, new Vector2(52f, -294f), new Vector2(300f, 18f));
            taskHeader.characterSpacing = 2f;

            TextMeshProUGUI task = CreateText(card, "Task",
                "01  Collect and verify all five samples with the correct tools.\n" +
                "02  Watch oxygen, the mission clock, and the 55 m boundary.\n" +
                "03  Return to the LUNA-1 lander and press E to stow the samples.",
                17f, new Color(0.74f, 0.82f, 0.9f), TextAlignmentOptions.TopLeft);
            TopLeft(task.rectTransform, new Vector2(52f, -322f), new Vector2(920f, 92f));
            task.lineSpacing = 5f;

            RectTransform beginRect = CreateRect("Begin Button", card);
            TopLeft(beginRect, new Vector2(52f, -446f), new Vector2(280f, 58f));
            MakeButton(beginRect, "BEGIN EVA", new Color(0.14f, 0.46f, 0.68f), Color.white,
                () => { if (manager != null) manager.BeginMission(); }, 19f);

            RectTransform guideRect = CreateRect("Guide Button", card);
            TopLeft(guideRect, new Vector2(348f, -446f), new Vector2(240f, 58f));
            MakeButton(guideRect, "FIELD GUIDE", new Color(0.07f, 0.11f, 0.15f), new Color(0.75f, 0.86f, 0.95f),
                () => { if (manager != null) manager.ToggleFieldGuide(); }, 17f);

            TextMeshProUGUI footer = CreateText(card, "Footer",
                "5 samples  ·  480 second EVA  ·  100% oxygen  ·  Lunar gravity 1.62 m/s²",
                13f, TextDim, TextAlignmentOptions.Left);
            TopLeft(footer.rectTransform, new Vector2(52f, -548f), new Vector2(920f, 24f));

            TextMeshProUGUI hint = CreateText(card, "Hint",
                "Press Enter, Space, or N to begin.  WASD to move.  Tab for the field guide.",
                13f, new Color(0.5f, 0.6f, 0.7f), TextAlignmentOptions.Left);
            TopLeft(hint.rectTransform, new Vector2(52f, -578f), new Vector2(920f, 24f));
        }

        private void BuildPause()
        {
            pauseRoot = CreatePanel("Pause", canvas.transform, new Color(0.006f, 0.01f, 0.018f, 0.78f), true);
            Stretch(pauseRoot);

            RectTransform card = CreatePanel("Pause Card", pauseRoot, new Color(0.018f, 0.03f, 0.045f, 0.98f), true);
            Center(card, new Vector2(560f, 470f), Vector2.zero);

            Image accent = AddImage(CreateRect("Accent", card), Accent, false);
            accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            accent.rectTransform.offsetMin = Vector2.zero;
            accent.rectTransform.offsetMax = new Vector2(6f, 0f);

            TextMeshProUGUI title = CreateText(card, "Title", "MISSION PAUSED", 38f, TextMain,
                TextAlignmentOptions.Left);
            TopLeft(title.rectTransform, new Vector2(44f, -42f), new Vector2(470f, 50f));
            title.fontStyle = FontStyles.Bold;

            TextMeshProUGUI subtitle = CreateText(card, "Subtitle", "The EVA clock and oxygen drain are stopped.",
                16f, TextDim, TextAlignmentOptions.Left);
            TopLeft(subtitle.rectTransform, new Vector2(46f, -94f), new Vector2(470f, 26f));

            RectTransform resumeRect = CreateRect("Resume", card);
            TopLeft(resumeRect, new Vector2(44f, -158f), new Vector2(472f, 54f));
            MakeButton(resumeRect, "RESUME EVA", new Color(0.14f, 0.46f, 0.68f), Color.white,
                () => { if (manager != null) manager.Resume(); }, 18f);

            RectTransform guideRect = CreateRect("Guide", card);
            TopLeft(guideRect, new Vector2(44f, -226f), new Vector2(472f, 54f));
            MakeButton(guideRect, "FIELD GUIDE", new Color(0.07f, 0.11f, 0.15f), new Color(0.75f, 0.86f, 0.95f),
                () => { if (manager != null) manager.ToggleFieldGuide(); }, 17f);

            RectTransform restartRect = CreateRect("Restart", card);
            TopLeft(restartRect, new Vector2(44f, -294f), new Vector2(472f, 54f));
            MakeButton(restartRect, "RESTART EVA", new Color(0.23f, 0.1f, 0.1f), new Color(1f, 0.78f, 0.72f),
                () => { if (manager != null) manager.RestartMission(); }, 17f);

            TextMeshProUGUI footer = CreateText(card, "Footer",
                "Press Esc to resume  ·  Tab to open the field guide",
                13f, TextDim, TextAlignmentOptions.Center);
            TopLeft(footer.rectTransform, new Vector2(44f, -392f), new Vector2(472f, 24f));
        }

        private void BuildEndScreen()
        {
            endRoot = CreatePanel("End Screen", canvas.transform, new Color(0.006f, 0.01f, 0.018f, 0.84f), true);
            Stretch(endRoot);

            RectTransform card = CreatePanel("End Card", endRoot, new Color(0.018f, 0.03f, 0.045f, 0.98f), true);
            Center(card, new Vector2(780f, 590f), Vector2.zero);

            endAccentImage = AddImage(CreateRect("Accent", card), Success, false);
            endAccentImage.rectTransform.anchorMin = new Vector2(0f, 1f);
            endAccentImage.rectTransform.anchorMax = new Vector2(1f, 1f);
            endAccentImage.rectTransform.pivot = new Vector2(0.5f, 1f);
            endAccentImage.rectTransform.offsetMin = new Vector2(0f, -6f);
            endAccentImage.rectTransform.offsetMax = Vector2.zero;

            endTitleText = CreateText(card, "Title", "MISSION COMPLETE", 42f, TextMain,
                TextAlignmentOptions.Left);
            TopLeft(endTitleText.rectTransform, new Vector2(46f, -44f), new Vector2(688f, 58f));
            endTitleText.fontStyle = FontStyles.Bold;
            endTitleText.characterSpacing = 1.5f;

            endReasonText = CreateText(card, "Reason", string.Empty, 18f,
                new Color(0.8f, 0.86f, 0.92f), TextAlignmentOptions.TopLeft);
            TopLeft(endReasonText.rectTransform, new Vector2(46f, -112f), new Vector2(688f, 96f));
            endReasonText.lineSpacing = 5f;

            endStatsText = CreateText(card, "Stats", string.Empty, 19f,
                new Color(0.8f, 0.88f, 0.95f), TextAlignmentOptions.TopLeft);
            TopLeft(endStatsText.rectTransform, new Vector2(46f, -236f), new Vector2(688f, 150f));
            endStatsText.lineSpacing = 8f;

            RectTransform primaryRect = CreateRect("Primary", card);
            TopLeft(primaryRect, new Vector2(46f, -432f), new Vector2(330f, 58f));
            Button primary = MakeButton(primaryRect, "RETRY EVA", new Color(0.14f, 0.46f, 0.68f), Color.white,
                () => { if (manager != null) manager.RestartMission(); }, 18f);
            endPrimaryLabel = primary.GetComponentInChildren<TextMeshProUGUI>();

            RectTransform secondaryRect = CreateRect("Secondary", card);
            TopLeft(secondaryRect, new Vector2(396f, -432f), new Vector2(300f, 58f));
            Button secondary = MakeButton(secondaryRect, "FIELD GUIDE", new Color(0.07f, 0.11f, 0.15f),
                new Color(0.75f, 0.86f, 0.95f),
                () => { if (manager != null) manager.ToggleFieldGuide(); }, 16f);
            endSecondaryLabel = secondary.GetComponentInChildren<TextMeshProUGUI>();

            TextMeshProUGUI footer = CreateText(card, "Footer",
                "GATHERING SAMPLES  ·  LUNA-1 SURFACE EVA", 12f, TextDim, TextAlignmentOptions.Center);
            TopLeft(footer.rectTransform, new Vector2(46f, -528f), new Vector2(688f, 22f));
            footer.characterSpacing = 1.5f;
        }

        private void BuildGuide()
        {
            guideRoot = CreatePanel("Field Guide", canvas.transform, new Color(0.006f, 0.01f, 0.016f, 0.95f), true);
            Stretch(guideRoot);

            RectTransform card = CreatePanel("Guide Card", guideRoot, new Color(0.015f, 0.026f, 0.04f, 0.99f), true);
            card.anchorMin = new Vector2(0f, 0f);
            card.anchorMax = new Vector2(1f, 1f);
            card.offsetMin = new Vector2(70f, 58f);
            card.offsetMax = new Vector2(-70f, -58f);

            Image accent = AddImage(CreateRect("Accent", card), Accent, false);
            accent.rectTransform.anchorMin = new Vector2(0f, 0f);
            accent.rectTransform.anchorMax = new Vector2(0f, 1f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            accent.rectTransform.offsetMin = Vector2.zero;
            accent.rectTransform.offsetMax = new Vector2(6f, 0f);

            TextMeshProUGUI title = CreateText(card, "Title", "FIELD GUIDE", 38f, TextMain,
                TextAlignmentOptions.Left);
            TopLeft(title.rectTransform, new Vector2(42f, -26f), new Vector2(700f, 48f));
            title.fontStyle = FontStyles.Bold;
            title.characterSpacing = 2f;

            TextMeshProUGUI subtitle = CreateText(card, "Subtitle", "TOOLS · SAMPLE TYPES · LUNAR CONTEXT · REFERENCES",
                13f, TextDim, TextAlignmentOptions.Left);
            TopLeft(subtitle.rectTransform, new Vector2(44f, -72f), new Vector2(900f, 20f));
            subtitle.characterSpacing = 1.5f;

            RectTransform closeRect = CreateRect("Close", card);
            closeRect.anchorMin = new Vector2(1f, 1f);
            closeRect.anchorMax = new Vector2(1f, 1f);
            closeRect.pivot = new Vector2(1f, 1f);
            closeRect.anchoredPosition = new Vector2(-36f, -28f);
            closeRect.sizeDelta = new Vector2(118f, 44f);
            MakeButton(closeRect, "CLOSE", new Color(0.08f, 0.12f, 0.16f), new Color(0.8f, 0.88f, 0.95f),
                () => { if (manager != null) manager.CloseGuide(); }, 15f);

            string[] tabs = { "TOOLS", "SAMPLE TYPES", "MOON CONTEXT", "REFERENCES" };
            for (int i = 0; i < tabs.Length; i++)
            {
                RectTransform tabRect = CreateRect("Tab " + tabs[i], card);
                TopLeft(tabRect, new Vector2(42f + i * 210f, -102f), new Vector2(198f, 42f));
                Image background = AddImage(tabRect, new Color(0.045f, 0.07f, 0.095f, 0.95f), true);
                Button button = tabRect.gameObject.AddComponent<Button>();
                button.targetGraphic = background;
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                int capturedIndex = i;
                button.onClick.AddListener(() =>
                {
                    if (manager != null && manager.Audio != null) manager.Audio.Click();
                    SetGuideTab(capturedIndex);
                });

                TextMeshProUGUI label = CreateText(tabRect, "Label", tabs[i], 14f,
                    new Color(0.7f, 0.79f, 0.87f), TextAlignmentOptions.Center);
                Stretch(label.rectTransform, 6f, 2f, 6f, 2f);
                label.characterSpacing = 1f;

                guideTabBackgrounds.Add(background);
                guideTabLabels.Add(label);
            }

            RectTransform scrollFrame = CreateRect("Guide Scroll Frame", card);
            scrollFrame.anchorMin = new Vector2(0f, 0f);
            scrollFrame.anchorMax = new Vector2(1f, 1f);
            scrollFrame.offsetMin = new Vector2(42f, 46f);
            scrollFrame.offsetMax = new Vector2(-42f, -156f);
            AddImage(scrollFrame, new Color(0.01f, 0.018f, 0.028f, 0.86f), true);

            RectTransform scrollRectTransform = CreateRect("Guide Scroll", scrollFrame);
            Stretch(scrollRectTransform, 1f, 1f, 1f, 1f);
            // Invisible raycast target: without a Graphic here the pointer/wheel never
            // reaches the ScrollRect, because the viewport is mask-only and the TMP body
            // has raycastTarget disabled. Keeps the existing look unchanged.
            AddImage(scrollRectTransform, new Color(0f, 0f, 0f, 0f), true);
            guideScroll = scrollRectTransform.gameObject.AddComponent<ScrollRect>();
            guideScroll.horizontal = false;
            guideScroll.vertical = true;
            guideScroll.movementType = ScrollRect.MovementType.Clamped;
            guideScroll.scrollSensitivity = 38f;

            RectTransform viewport = CreateRect("Viewport", scrollRectTransform);
            Stretch(viewport, 26f, 18f, 26f, 18f);
            viewport.gameObject.AddComponent<RectMask2D>();
            guideScroll.viewport = viewport;

            RectTransform content = CreateRect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            guideBody = content.gameObject.AddComponent<TextMeshProUGUI>();
            guideBody.font = UiFont.Get();
            guideBody.fontSize = 18f;
            guideBody.color = new Color(0.78f, 0.85f, 0.92f);
            guideBody.alignment = TextAlignmentOptions.TopLeft;
            guideBody.textWrappingMode = TextWrappingModes.Normal;
            guideBody.overflowMode = TextOverflowModes.Overflow;
            guideBody.richText = true;
            guideBody.raycastTarget = false;
            guideBody.lineSpacing = 6f;

            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            guideScroll.content = content;

            TextMeshProUGUI footer = CreateText(card, "Footer",
                "Scroll to read  ·  Tab / G or Esc to close", 12f, TextDim, TextAlignmentOptions.Right);
            footer.rectTransform.anchorMin = new Vector2(1f, 0f);
            footer.rectTransform.anchorMax = new Vector2(1f, 0f);
            footer.rectTransform.pivot = new Vector2(1f, 0f);
            footer.rectTransform.anchoredPosition = new Vector2(-42f, 18f);
            footer.rectTransform.sizeDelta = new Vector2(520f, 20f);
        }

        // ------------------------------------------------------------ public API

        public void UpdateHud(
            float timeRemaining, float oxygen, float elapsed, int sampleCount, int sampleTotal,
            string manifest, string objective, string selectedTool)
        {
            if (clockText != null) clockText.text = GameManager.FormatClock(timeRemaining);
            if (oxygenText != null)
            {
                oxygenText.text = Mathf.CeilToInt(oxygen) + "%";
                oxygenText.color = oxygen <= Sim.CriticalOxygen
                    ? Danger
                    : oxygen <= Sim.LowOxygen
                        ? AccentWarm
                        : TextMain;
            }

            if (oxygenFill != null)
            {
                oxygenFill.fillAmount = Mathf.Clamp01(oxygen / Sim.OxygenMax);
                oxygenFill.color = oxygen <= Sim.CriticalOxygen
                    ? Danger
                    : oxygen <= Sim.LowOxygen
                        ? AccentWarm
                        : Success;
            }

            if (manifestHeaderText != null)
            {
                manifestHeaderText.text = "SAMPLE MANIFEST  " + sampleCount + " / " + sampleTotal;
            }
            if (manifestText != null) manifestText.text = manifest;
            if (objectiveText != null) objectiveText.text = objective;
            if (!string.IsNullOrEmpty(selectedTool) && selectedTool != selectedToolId) SetSelectedTool(selectedTool);
        }

        public void SetSelectedTool(string id)
        {
            selectedToolId = id;
            for (int i = 0; i < toolSlots.Count; i++)
            {
                ToolSlot slot = toolSlots[i];
                bool selected = string.Equals(slot.Id, id, StringComparison.OrdinalIgnoreCase);
                slot.Background.color = selected
                    ? new Color(0.12f, 0.32f, 0.45f, 0.98f)
                    : new Color(0.05f, 0.075f, 0.1f, 0.95f);
                slot.Label.color = selected
                    ? Color.white
                    : new Color(0.74f, 0.81f, 0.88f);
                slot.Accent.color = selected ? Accent : new Color(0f, 0f, 0f, 0f);
            }
        }

        public void SetInteraction(string title, string subtitle, float progress, bool warning, bool show)
        {
            if (promptRoot == null) return;

            if (!show || string.IsNullOrEmpty(title))
            {
                promptRoot.gameObject.SetActive(false);
                return;
            }

            promptRoot.gameObject.SetActive(true);
            promptTitleText.text = title;
            promptSubText.text = subtitle;
            promptKeyText.text = warning ? "!" : "E";
            Color color = warning ? AccentWarm : Accent;
            promptAccent.color = color;
            promptKeyText.color = color;
            if (promptBackground != null) promptBackground.color = new Color(0.015f, 0.027f, 0.04f, 0.94f);

            bool hasProgress = progress >= 0f;
            if (promptProgressRoot != null) promptProgressRoot.gameObject.SetActive(hasProgress);
            if (hasProgress && promptProgressFill != null)
            {
                promptProgressFill.fillAmount = Mathf.Clamp01(progress);
                promptProgressFill.color = color;
            }
        }

        public void HideInteraction()
        {
            if (promptRoot != null) promptRoot.gameObject.SetActive(false);
        }

        public void Toast(string message, Color color)
        {
            if (toastRoot == null) return;
            toastRoot.gameObject.SetActive(true);
            toastText.text = message;
            toastAccent.color = color;
            toastGroup.alpha = 1f;
            toastTimer = 4.4f;
        }

        public void SetWarning(string text, Color color)
        {
            if (warningRoot == null) return;
            if (string.IsNullOrEmpty(text))
            {
                warningRoot.gameObject.SetActive(false);
                return;
            }

            warningRoot.gameObject.SetActive(true);
            warningText.text = text;
            warningBaseColor = color;
            warningBackground.color = color;
            warningAccent.color = color;
        }

        public void SetVignetteAlpha(float amount)
        {
            vignetteAmount = Mathf.Clamp01(amount);
        }

        public void ShowBriefing(bool visible)
        {
            SetBriefingVisible(visible);
        }

        public void ShowPause(bool visible)
        {
            SetPauseVisible(visible);
        }

        public void ShowGuide(bool visible)
        {
            SetGuideVisible(visible);
            if (visible) SetGuideTab(guideTab);
        }

        public void ShowSuccess(string reason, string stats)
        {
            ShowEnd(true, true, reason, stats);
        }

        public void ShowFailure(string reason, string stats)
        {
            ShowEnd(true, false, reason, stats);
        }

        public void ShowEnd(bool visible, bool success, string reason, string stats)
        {
            if (endRoot == null) return;

            if (visible)
            {
                endTitleText.text = success ? "MISSION COMPLETE" : "EVA TERMINATED";
                endReasonText.text = reason;
                endStatsText.text = stats;
                endAccentImage.color = success ? Success : Danger;
                endTitleText.color = success ? Success : Danger;
                endPrimaryLabel.text = success ? "FLY AGAIN" : "RETRY EVA";
                endSecondaryLabel.text = "FIELD GUIDE";
            }

            SetEndVisible(visible);
            if (!visible) return;
        }

        private void SetBriefingVisible(bool visible)
        {
            if (briefingRoot != null) briefingRoot.gameObject.SetActive(visible);
        }

        private void SetPauseVisible(bool visible)
        {
            if (pauseRoot != null) pauseRoot.gameObject.SetActive(visible);
        }

        private void SetEndVisible(bool visible)
        {
            if (endRoot != null) endRoot.gameObject.SetActive(visible);
        }

        private void SetGuideVisible(bool visible)
        {
            if (guideRoot != null) guideRoot.gameObject.SetActive(visible);
        }

        private void SetGuideTab(int index)
        {
            if (guideBody == null) return;
            guideTab = Mathf.Clamp(index, 0, 3);

            for (int i = 0; i < guideTabBackgrounds.Count; i++)
            {
                bool selected = i == guideTab;
                guideTabBackgrounds[i].color = selected
                    ? new Color(0.13f, 0.34f, 0.47f, 0.98f)
                    : new Color(0.045f, 0.07f, 0.095f, 0.95f);
                guideTabLabels[i].color = selected ? Color.white : new Color(0.7f, 0.79f, 0.87f);
            }

            guideBody.text = BuildGuideText(guideTab);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(guideBody.rectTransform);
            if (guideScroll != null) guideScroll.verticalNormalizedPosition = 1f;
        }

        // ---------------------------------------------------------- guide content

        private static string BuildGuideText(int tab)
        {
            switch (tab)
            {
                case 0: return BuildToolsGuide();
                case 1: return BuildSamplingGuide();
                case 2: return BuildMoonGuide();
                default: return BuildReferenceGuide();
            }
        }

        private static string BuildToolsGuide()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<size=30><b>SAMPLE TOOLS</b></size>\n");
            builder.Append("<color=#7E93A8>Press 1–6 during the EVA to switch tools. A tool must match the material " +
                           "at the station; the wrong tool wastes time and can damage a sample.</color>\n\n");

            for (int i = 0; i < Content.Tools.Length; i++)
            {
                ToolDef tool = Content.Tools[i];
                builder.Append("<size=22><color=#84D8FF>").Append(tool.key).Append(" · ").Append(tool.name)
                    .Append("</color></size>  <color=#AAB8C6>").Append(tool.shortDesc).Append("</color>\n");
                builder.Append("<b>Function</b>  ").Append(tool.function).Append('\n');
                builder.Append("<b>When</b>  ").Append(tool.when).Append('\n');
                builder.Append("<b>Why</b>  ").Append(tool.why).Append('\n');
                builder.Append("<color=#8FA3B6>").Append(tool.science).Append("</color>\n\n");
            }

            return builder.ToString();
        }

        private static string BuildSamplingGuide()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<size=30><b>THE FIVE SAMPLE TYPES</b></size>\n");
            builder.Append("<color=#7E93A8>Each station represents a different material and a different sampling " +
                           "problem. The required workflow is the same one a field geologist would use.</color>\n\n");

            for (int i = 0; i < Content.Sites.Length; i++)
            {
                SiteDef site = Content.Sites[i];
                ContainerDef container = Content.Container(site.container);
                builder.Append("<size=23><color=#FFC56B>").Append(site.code).Append(" · ").Append(site.name)
                    .Append("</color></size>\n");
                builder.Append("<color=#8FA3B6>").Append(site.typeLabel).Append("</color>\n");
                builder.Append("<b>Required tool").Append(site.requiredTools.Length > 1 ? "s" : string.Empty)
                    .Append("</b>  ");
                for (int t = 0; t < site.requiredTools.Length; t++)
                {
                    if (t > 0) builder.Append(" → ");
                    ToolDef tool = Content.Tool(site.requiredTools[t]);
                    builder.Append(tool != null ? tool.name : site.requiredTools[t]);
                }
                builder.Append("    <b>Container</b>  ")
                    .Append(container != null ? container.name : site.container).Append('\n');
                builder.Append("<b>Brief</b>  ").Append(site.brief).Append('\n');
                builder.Append("<b>Field hint</b>  ").Append(site.hint).Append('\n');
                builder.Append("<color=#8FA3B6>").Append(site.science).Append("</color>\n\n");
            }

            return builder.ToString();
        }

        private static string BuildMoonGuide()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<size=30><b>LUNAR CONTEXT</b></size>\n");
            builder.Append("<color=#7E93A8>Why the Moon is a difficult and scientifically valuable place to sample.</color>\n\n");

            AppendFact(builder, "Gravity", Content.Gravity);
            AppendFact(builder, "Atmosphere", Content.Atmosphere);
            AppendFact(builder, "Regolith", Content.Regolith);
            AppendFact(builder, "Temperature", Content.Temperature);
            AppendFact(builder, "Mobility", Content.Mobility);
            AppendFact(builder, "Samples", Content.Samples);
            AppendFact(builder, "Drive tubes", Content.DriveTubes);
            AppendFact(builder, "Tool selection", Content.ToolsScience);
            return builder.ToString();
        }

        private static void AppendFact(StringBuilder builder, string title, string body)
        {
            builder.Append("<size=21><color=#84D8FF>").Append(title).Append("</color></size>\n");
            builder.Append(body).Append("\n\n");
        }

        private static string BuildReferenceGuide()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<size=30><b>REFERENCES</b></size>\n");
            builder.Append("<color=#7E93A8>Further reading from NASA on the Moon, lunar samples, spacesuits, and curation.</color>\n\n");

            for (int i = 0; i < Content.ReferenceLabels.Length; i++)
            {
                builder.Append("<size=20><color=#FFC56B>").Append(Content.ReferenceLabels[i]).Append("</color></size>\n");
                builder.Append("<color=#84D8FF>").Append(Content.ReferenceUrls[i]).Append("</color>\n\n");
            }

            builder.Append("<color=#7E93A8>This game is a simplified educational simulation. Oxygen, tool costs, " +
                           "and mission timing are gameplay choices rather than measured EVA specifications.</color>\n");
            return builder.ToString();
        }

        // ---------------------------------------------------------------- helpers

        private Button MakeButton(RectTransform rect, string label, Color background, Color textColor,
            Action onClick, float fontSize)
        {
            Image image = AddImage(rect, background, true);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.16f, 1.2f, 1f);
            colors.pressedColor = new Color(0.86f, 0.9f, 0.94f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;

            TextMeshProUGUI text = CreateText(rect, "Label", label, fontSize, textColor, TextAlignmentOptions.Center);
            Stretch(text.rectTransform, 8f, 2f, 8f, 2f);
            text.characterSpacing = 1f;

            button.onClick.AddListener(() =>
            {
                if (manager != null && manager.Audio != null) manager.Audio.Click();
                onClick?.Invoke();
            });
            return button;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return (RectTransform)gameObject.transform;
        }

        private static RectTransform CreatePanel(string name, Transform parent, Color color, bool raycast)
        {
            RectTransform rect = CreateRect(name, parent);
            AddImage(rect, color, raycast);
            return rect;
        }

        private static Image AddImage(RectTransform rect, Color color, bool raycast)
        {
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = RuntimeAssets.WhiteSprite;
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size,
            Color color, TextAlignmentOptions alignment)
        {
            RectTransform rect = CreateRect(name, parent);
            TextMeshProUGUI label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = UiFont.Get();
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = alignment;
            label.richText = true;
            label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

        private static void Stretch(RectTransform rect, float left = 0f, float bottom = 0f, float right = 0f,
            float top = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Center(RectTransform rect, Vector2 size, Vector2 offset)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = offset;
        }

        private static void TopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void TopRight(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
