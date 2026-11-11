using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PhysiologicalMaintenance
{
    public sealed class PhysiologicalMaintenanceUI : MonoBehaviour
    {
        private PhysiologicalMaintenanceController controller;
        private Text statusText;
        private Text missionText;
        private Text objectiveText;
        private Text exerciseLabel;
        private Text intensityLabel;
        private Text durationLabel;
        private Text sessionStateLabel;
        private Text warningText;
        private Text oxygenButtonLabel;
        private Text coolingButtonLabel;
        private Text wasteButtonLabel;
        private Text pauseButtonLabel;
        private Image resistanceButtonImage;
        private Image cycleButtonImage;
        private GameObject resultPanel;
        private Text resultText;
        private string latestWarnings = string.Empty;
        private bool resultDismissed;
        private readonly StringBuilder statusBuilder = new StringBuilder(512);

        public void Initialize(PhysiologicalMaintenanceController missionController)
        {
            controller = missionController;
            CreateEventSystem();
            BuildCanvas();
            Refresh(controller.Snapshot, controller);
        }

        public void Refresh(SimulationSnapshot snapshot, PhysiologicalMaintenanceController missionController)
        {
            if (snapshot == null)
                return;

            controller = missionController;
            statusBuilder.Length = 0;
            statusBuilder.Append("LIFE SUPPORT\n")
                .Append("Oxygen reserve       ").Append(snapshot.OxygenPercent.ToString("0.0")).Append(" %\n")
                .Append("Habitat temperature ").Append(snapshot.HabitatTemperatureC.ToString("0.0")).Append(" °C\n")
                .Append("Waste storage        ").Append(snapshot.WasteStored.ToString("0.0")).Append(" / ").Append(snapshot.WasteCapacity.ToString("0")).Append(" units\n")
                .Append("Electrical reserve   ").Append(snapshot.PowerStored.ToString("0.0")).Append(" / ").Append(snapshot.PowerCapacity.ToString("0")).Append(" units\n")
                .Append("Current power load   ").Append(snapshot.CurrentPowerDemand.ToString("0.0")).Append(" units/min\n\n")
                .Append("ASTRONAUT STATUS\n")
                .Append("Muscle health        ").Append(snapshot.MuscleHealth.ToString("0.0")).Append(" / 100\n")
                .Append("Bone health          ").Append(snapshot.BoneHealth.ToString("0.0")).Append(" / 100\n")
                .Append("Cardio fitness       ").Append(snapshot.CardiovascularFitness.ToString("0.0")).Append(" / 100\n")
                .Append("Fatigue               ").Append(snapshot.Fatigue.ToString("0.0")).Append(" / 100\n")
                .Append("Training consistency ").Append(snapshot.ExerciseConsistency.ToString("0.0")).Append(" / 100");
            statusText.text = statusBuilder.ToString();

            missionText.text = "MISSION TIME  " + FormatTime(snapshot.MissionElapsedMinutes * 60f) + " / " + FormatTime(snapshot.MissionDurationMinutes * 60f);
            objectiveText.text = "OBJECTIVES\nComplete " + missionController.Tuning.ExerciseSessionObjective + " exercise sessions (" + snapshot.SessionsCompleted + ")\nTrain " +
                (missionController.Tuning.ExerciseObjectiveSeconds / 60f).ToString("0.#") + " minutes total (" +
                (snapshot.ExerciseSeconds / 60f).ToString("0.0") + " min)\nKeep all life-support systems out of critical state.";
            exerciseLabel.text = missionController.SelectedExerciseName;
            intensityLabel.text = "Intensity: " + Mathf.RoundToInt(missionController.ExerciseIntensity * 100f) + "%";
            durationLabel.text = "Target session: " + FormatTime(missionController.TargetSessionSeconds);
            sessionStateLabel.text = "Session: " + snapshot.Session.State + "   " + Mathf.RoundToInt(snapshot.Session.Progress * 100f) + "%";
            resistanceButtonImage.color = missionController.SelectedMode == ExerciseMode.Resistance ? SelectedColor : ButtonColor;
            cycleButtonImage.color = missionController.SelectedMode == ExerciseMode.CycleErgometer ? SelectedColor : ButtonColor;
            oxygenButtonLabel.text = ToggleLabel("Oxygen regulator", snapshot.OxygenRegulatorEnabled);
            coolingButtonLabel.text = ToggleLabel("Thermal cooling", snapshot.CoolingEnabled);
            wasteButtonLabel.text = ToggleLabel("Waste processor", snapshot.WasteProcessorEnabled);
            pauseButtonLabel.text = missionController.SimulationPaused ? "RESUME SIM" : "PAUSE SIM";

            latestWarnings = BuildWarnings(snapshot, missionController.Tuning);
            warningText.text = latestWarnings.Length == 0 ? "SYSTEMS NOMINAL  •  Monitor resources during exercise" : latestWarnings;
            warningText.color = latestWarnings.Contains("CRITICAL") ? CriticalColor : WarningColor;

            if (snapshot.MissionEnded && resultPanel != null && !resultDismissed)
                resultPanel.SetActive(true);
        }

        public void ShowMissionResult(MissionResult result)
        {
            if (resultPanel == null)
                return;

            if (result == null)
            {
                resultDismissed = false;
                resultPanel.SetActive(false);
                return;
            }

            resultDismissed = false;
            string outcome = result.Outcome == MissionOutcome.Success ? "MISSION SUCCESS" :
                result.Outcome == MissionOutcome.LifeSupportCritical ? "MISSION ABORTED — LIFE SUPPORT" : "MISSION COMPLETE — OBJECTIVES INCOMPLETE";
            resultText.text = outcome + "\n\n" + result.Assessment + "\n\n" +
                "Sessions completed: " + result.SessionsCompleted + "     Exercise time: " + FormatTime(result.ExerciseSeconds) + "\n" +
                "Muscle " + result.MuscleHealth.ToString("0.0") + "   Bone " + result.BoneHealth.ToString("0.0") +
                "   Cardio " + result.CardiovascularFitness.ToString("0.0") + "   Fatigue " + result.Fatigue.ToString("0.0") + "\n" +
                "Oxygen " + result.OxygenPercent.ToString("0.0") + "%   Temperature " + result.HabitatTemperatureC.ToString("0.0") + "°C\n" +
                "Waste " + result.WasteStored.ToString("0.0") + "/" + result.WasteCapacity.ToString("0") +
                "   Power " + result.PowerStored.ToString("0.0") + " units   Power used " + result.TotalPowerUsed.ToString("0.0") + " units\n\n" +
                "Educational simulation only — not medical advice or an official exercise protocol.";
            resultPanel.SetActive(true);
        }

        private void BuildCanvas()
        {
            var canvasObject = new GameObject("PM Operations Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            GameObject left = CreatePanel(canvasObject.transform, "Status panel", new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(415f, 590f));
            CreateText(left.transform, "PHYSIOLOGICAL MAINTENANCE", new Vector2(18f, 16f), new Vector2(379f, 36f), 22, Color.white, font, FontStyle.Bold);
            missionText = CreateText(left.transform, "MISSION TIME", new Vector2(18f, 58f), new Vector2(379f, 28f), 17, AccentColor, font, FontStyle.Bold);
            objectiveText = CreateText(left.transform, "OBJECTIVES", new Vector2(18f, 94f), new Vector2(379f, 90f), 15, Color.white, font, FontStyle.Normal);
            statusText = CreateText(left.transform, "", new Vector2(18f, 196f), new Vector2(379f, 315f), 16, new Color(0.82f, 0.9f, 0.94f), font, FontStyle.Normal);
            CreateButton(left.transform, "Pause simulation", new Vector2(18f, 526f), new Vector2(115f, 42f), font, () => controller.ToggleSimulationPause(), out _, out pauseButtonLabel);
            CreateButton(left.transform, "End mission", new Vector2(143f, 526f), new Vector2(115f, 42f), font, () => controller.EndMission(), out _, out _);
            CreateButton(left.transform, "Restart", new Vector2(268f, 526f), new Vector2(115f, 42f), font, () => controller.RestartMission(), out _, out _);

            GameObject exercise = CreatePanel(canvasObject.transform, "Exercise controls", new Vector2(1f, 1f), new Vector2(-18f, -18f), new Vector2(390f, 420f));
            CreateText(exercise.transform, "EXERCISE EQUIPMENT", new Vector2(18f, 16f), new Vector2(354f, 32f), 20, Color.white, font, FontStyle.Bold);
            CreateButton(exercise.transform, "ARED / RESISTANCE", new Vector2(18f, 62f), new Vector2(170f, 44f), font, () => controller.SelectEquipment(ExerciseMode.Resistance), out var resistanceButton, out _);
            CreateButton(exercise.transform, "CYCLE ERGOMETER", new Vector2(202f, 62f), new Vector2(170f, 44f), font, () => controller.SelectEquipment(ExerciseMode.CycleErgometer), out var cycleButton, out _);
            resistanceButtonImage = resistanceButton.GetComponent<Image>();
            cycleButtonImage = cycleButton.GetComponent<Image>();
            exerciseLabel = CreateText(exercise.transform, "", new Vector2(18f, 121f), new Vector2(275f, 34f), 17, AccentColor, font, FontStyle.Bold);
            CreateButton(exercise.transform, "NEXT", new Vector2(293f, 114f), new Vector2(79f, 43f), font, () => { controller.SelectNextExercise(); Refresh(controller.Snapshot, controller); }, out _, out _);
            intensityLabel = CreateText(exercise.transform, "Intensity", new Vector2(18f, 176f), new Vector2(215f, 28f), 16, Color.white, font, FontStyle.Normal);
            CreateButton(exercise.transform, "−", new Vector2(250f, 166f), new Vector2(54f, 44f), font, () => controller.AdjustIntensity(-0.1f), out _, out _);
            CreateButton(exercise.transform, "+", new Vector2(318f, 166f), new Vector2(54f, 44f), font, () => controller.AdjustIntensity(0.1f), out _, out _);
            durationLabel = CreateText(exercise.transform, "Session duration", new Vector2(18f, 231f), new Vector2(215f, 28f), 16, Color.white, font, FontStyle.Normal);
            CreateButton(exercise.transform, "−30s", new Vector2(250f, 221f), new Vector2(54f, 44f), font, () => controller.AdjustSessionDuration(-30f), out _, out _);
            CreateButton(exercise.transform, "+30s", new Vector2(318f, 221f), new Vector2(54f, 44f), font, () => controller.AdjustSessionDuration(30f), out _, out _);
            sessionStateLabel = CreateText(exercise.transform, "Session: Idle", new Vector2(18f, 278f), new Vector2(354f, 28f), 15, new Color(0.82f, 0.9f, 0.94f), font, FontStyle.Normal);
            CreateButton(exercise.transform, "START", new Vector2(18f, 330f), new Vector2(106f, 52f), font, () => controller.StartExercise(), out _, out _);
            CreateButton(exercise.transform, "PAUSE / RESUME", new Vector2(142f, 330f), new Vector2(128f, 52f), font, () => controller.ToggleExercisePause(), out _, out _);
            CreateButton(exercise.transform, "STOP", new Vector2(288f, 330f), new Vector2(84f, 52f), font, () => controller.StopExercise(), out _, out _);

            GameObject lifeSupport = CreatePanel(canvasObject.transform, "Life support controls", new Vector2(1f, 1f), new Vector2(-18f, -458f), new Vector2(390f, 250f));
            CreateText(lifeSupport.transform, "LIFE-SUPPORT CONTROLS", new Vector2(18f, 14f), new Vector2(354f, 30f), 19, Color.white, font, FontStyle.Bold);
            CreateButton(lifeSupport.transform, "", new Vector2(18f, 57f), new Vector2(354f, 48f), font, () => controller.ToggleOxygenRegulator(), out _, out oxygenButtonLabel);
            CreateButton(lifeSupport.transform, "", new Vector2(18f, 115f), new Vector2(354f, 48f), font, () => controller.ToggleCooling(), out _, out coolingButtonLabel);
            CreateButton(lifeSupport.transform, "", new Vector2(18f, 173f), new Vector2(354f, 48f), font, () => controller.ToggleWasteProcessor(), out _, out wasteButtonLabel);

            GameObject warningPanel = CreatePanel(canvasObject.transform, "Warnings", new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(900f, 94f));
            warningText = CreateText(warningPanel.transform, "", new Vector2(16f, 12f), new Vector2(868f, 70f), 17, WarningColor, font, FontStyle.Bold);

            resultPanel = CreatePanel(canvasObject.transform, "Mission summary", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(850f, 600f));
            resultPanel.SetActive(false);
            CreateText(resultPanel.transform, "MISSION DEBRIEF", new Vector2(24f, 18f), new Vector2(802f, 42f), 27, AccentColor, font, FontStyle.Bold);
            resultText = CreateText(resultPanel.transform, "", new Vector2(24f, 78f), new Vector2(802f, 420f), 17, Color.white, font, FontStyle.Normal);
            CreateButton(resultPanel.transform, "RESTART MISSION", new Vector2(24f, 525f), new Vector2(220f, 48f), font, () => controller.RestartMission(), out _, out _);
            CreateButton(resultPanel.transform, "CLOSE DEBRIEF", new Vector2(264f, 525f), new Vector2(220f, 48f), font, () => { resultDismissed = true; resultPanel.SetActive(false); }, out _, out _);
        }

        private void CreateEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var eventSystem = new GameObject("PM Event System", typeof(EventSystem));
            eventSystem.transform.SetParent(transform, false);
            var moduleType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (moduleType != null)
            {
                var module = eventSystem.AddComponent(moduleType);
                MethodInfo assignDefaults = moduleType.GetMethod("AssignDefaultActions", BindingFlags.Instance | BindingFlags.Public);
                assignDefaults?.Invoke(module, null);
            }
            else
            {
                eventSystem.AddComponent<StandaloneInputModule>();
            }
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var panel = new GameObject(name, typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(parent, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor == new Vector2(0.5f, 0.5f) ? new Vector2(0.5f, 0.5f) : anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            panel.GetComponent<Image>().color = new Color(0.035f, 0.065f, 0.09f, 0.92f);
            return panel;
        }

        private static Text CreateText(Transform parent, string text, Vector2 position, Vector2 size, int fontSize,
            Color color, Font font, FontStyle style)
        {
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            var rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(position.x, -position.y);
            rect.sizeDelta = size;
            var component = textObject.GetComponent<Text>();
            component.font = font;
            component.text = text;
            component.fontSize = fontSize;
            component.fontStyle = style;
            component.color = color;
            component.alignment = TextAnchor.UpperLeft;
            component.horizontalOverflow = HorizontalWrapMode.Wrap;
            component.verticalOverflow = VerticalWrapMode.Overflow;
            component.raycastTarget = false;
            return component;
        }

        private static void CreateButton(Transform parent, string label, Vector2 position, Vector2 size, Font font,
            UnityEngine.Events.UnityAction onClick, out Button button, out Text buttonText)
        {
            var buttonObject = new GameObject(string.IsNullOrEmpty(label) ? "Control" : label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(position.x, -position.y);
            rect.sizeDelta = size;
            var image = buttonObject.GetComponent<Image>();
            image.color = ButtonColor;
            button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(onClick);
            buttonText = CreateText(buttonObject.transform, label, new Vector2(6f, 5f), new Vector2(size.x - 12f, size.y - 10f), 15, Color.white, font, FontStyle.Bold);
            buttonText.alignment = TextAnchor.MiddleCenter;
        }

        private static string BuildWarnings(SimulationSnapshot state, SimulationTuning tuning)
        {
            var warnings = new StringBuilder(192);
            AddWarning(warnings, state.OxygenPercent <= tuning.OxygenCriticalPercent, "CRITICAL: OXYGEN RESERVE — restore supply immediately");
            AddWarning(warnings, state.OxygenPercent > tuning.OxygenCriticalPercent && state.OxygenPercent <= tuning.OxygenWarningPercent,
                "WARNING: oxygen reserve low — enable oxygen regulation");
            AddWarning(warnings, state.HabitatTemperatureC <= tuning.TemperatureCriticalLowC || state.HabitatTemperatureC >= tuning.TemperatureCriticalHighC,
                "CRITICAL: habitat temperature outside operating limits");
            AddWarning(warnings, (state.HabitatTemperatureC <= tuning.TemperatureWarningLowC || state.HabitatTemperatureC >= tuning.TemperatureWarningHighC) &&
                state.HabitatTemperatureC > tuning.TemperatureCriticalLowC && state.HabitatTemperatureC < tuning.TemperatureCriticalHighC,
                "WARNING: habitat temperature trending outside comfort band — adjust cooling");
            float wasteFraction = state.WasteCapacity > 0f ? state.WasteStored / state.WasteCapacity : 1f;
            AddWarning(warnings, wasteFraction >= 1f, "CRITICAL: waste storage is full");
            AddWarning(warnings, wasteFraction >= tuning.WasteWarningFraction && wasteFraction < 1f,
                "WARNING: waste storage nearing capacity — run processor");
            AddWarning(warnings, state.PowerStored <= tuning.PowerCritical, "CRITICAL: electrical reserve nearly depleted — shed loads");
            AddWarning(warnings, state.PowerStored > tuning.PowerCritical && state.PowerStored <= tuning.LowPowerWarning,
                "WARNING: low electrical reserve — prioritize essential systems");
            AddWarning(warnings, state.Fatigue >= 95f, "WARNING: fatigue is limiting exercise continuation");
            AddWarning(warnings, state.Fatigue >= 80f && state.Fatigue < 95f, "WARNING: fatigue high — reduce intensity or recover");
            return warnings.ToString().TrimEnd('\n');
        }

        private static void AddWarning(StringBuilder builder, bool condition, string message)
        {
            if (!condition)
                return;
            if (builder.Length > 0)
                builder.Append('\n');
            builder.Append(message);
        }

        private static string ToggleLabel(string control, bool enabled) => control + ": " + (enabled ? "ON" : "OFF") + "  (CLICK TO TOGGLE)";

        private static string FormatTime(float seconds)
        {
            int wholeSeconds = Mathf.Max(0, Mathf.FloorToInt(seconds));
            return (wholeSeconds / 60).ToString("00") + ":" + (wholeSeconds % 60).ToString("00");
        }

        private static readonly Color ButtonColor = new Color(0.12f, 0.24f, 0.31f, 1f);
        private static readonly Color SelectedColor = new Color(0.05f, 0.42f, 0.48f, 1f);
        private static readonly Color AccentColor = new Color(0.25f, 0.88f, 0.86f, 1f);
        private static readonly Color WarningColor = new Color(1f, 0.68f, 0.24f, 1f);
        private static readonly Color CriticalColor = new Color(1f, 0.25f, 0.19f, 1f);
    }
}
