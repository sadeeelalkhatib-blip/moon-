using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PhysiologicalMaintenance
{
    public sealed class PhysiologicalMaintenanceUI : MonoBehaviour
    {
        private PhysiologicalMaintenanceController controller;
        private Text missionText;
        private Text objectiveText;
        private Text scoreText;
        private Text interactionText;
        private Text exerciseLabel;
        private Text warningText;
        private Text simulationPauseButtonLabel;
        private Text exerciseInstructionText;
        private Text exerciseProgressText;
        private Text lifeSupportStatusText;
        private Text feedbackText;
        private GameObject welcomePanel;
        private GameObject exerciseInstructionPanel;
        private GameObject lifeSupportPanel;
        private float feedbackRemaining;
        private readonly System.Collections.Generic.Dictionary<string, int> previousObjectiveCounts =
            new System.Collections.Generic.Dictionary<string, int>();
        private Image squatButtonImage;
        private Image rowButtonImage;
        private Image cycleButtonImage;
        private GameObject warningPanel;
        private GameObject resultPanel;
        private GameObject pausePanel;
        private Text resultText;
        private string latestWarnings = string.Empty;
        private float warningRemaining;
        private bool resultDismissed;

        public bool BlocksGameplay => (welcomePanel != null && welcomePanel.activeSelf) ||
            (exerciseInstructionPanel != null && exerciseInstructionPanel.activeSelf);

        public void Initialize(PhysiologicalMaintenanceController missionController)
        {
            controller = missionController;
            CreateEventSystem();
            BuildCanvas();
            Refresh(controller.Snapshot, controller);
            ShowWelcomeTutorial();
        }

        public void Refresh(SimulationSnapshot snapshot, PhysiologicalMaintenanceController missionController)
        {
            if (snapshot == null)
                return;

            controller = missionController;
            float durationSeconds = snapshot.MissionDurationMinutes * 60f;
            string missionClock = FormatTime(Mathf.Max(0f, durationSeconds - snapshot.MissionElapsedSeconds));
            missionText.text = missionClock + "   " + snapshot.MissionState.ToString().ToUpperInvariant();
            objectiveText.text = BuildObjectiveText(snapshot.Objectives);
            scoreText.text = "SCORE  " + snapshot.Score.ToString("0000");
            interactionText.text = missionController.PlayerController != null
                ? missionController.PlayerController.InteractionPrompt
                : "WASD: walk     Mouse: look around     E: use equipment     ESC: pause";
            lifeSupportStatusText.text = "OXYGEN " + (snapshot.OxygenRegulatorEnabled ? "ON" : "OFF") + "   " +
                snapshot.OxygenPercent.ToString("0") + "%     COOLING " + (snapshot.CoolingEnabled ? "ON" : "OFF") +
                "     WASTE " + (snapshot.WasteProcessorEnabled ? "ON" : "OFF");
            if (exerciseInstructionPanel.activeSelf)
                RefreshExerciseProgress(snapshot, missionController.SelectedExerciseType);
            RefreshObjectiveFeedback(snapshot.Objectives);
            pausePanel.SetActive(snapshot.MissionState == MissionState.Paused);
            exerciseLabel.text = missionController.SelectedExerciseName;
            squatButtonImage.color = missionController.SelectedExerciseType == ExerciseType.Squat ? SelectedColor : ButtonColor;
            rowButtonImage.color = missionController.SelectedExerciseType == ExerciseType.Row ? SelectedColor : ButtonColor;
            cycleButtonImage.color = missionController.SelectedExerciseType == ExerciseType.Cycling ? SelectedColor : ButtonColor;
            simulationPauseButtonLabel.text = missionController.SimulationPaused ? "RESUME" : "PAUSE";

            latestWarnings = BuildWarnings(snapshot, missionController.Tuning);
            if (latestWarnings.Length > 0 && warningText.text != latestWarnings)
            {
                warningText.text = latestWarnings;
                warningText.color = latestWarnings.Contains("CRITICAL") ? CriticalColor : WarningColor;
                warningRemaining = 6f;
                warningPanel.SetActive(true);
            }
            else if (latestWarnings.Length == 0)
            {
                warningRemaining = 0f;
                warningText.text = string.Empty;
                warningPanel.SetActive(false);
            }

            if (snapshot.MissionEnded && resultPanel != null && !resultDismissed)
                resultPanel.SetActive(true);
        }

        private void Update()
        {
            if (warningRemaining > 0f)
            {
                warningRemaining -= Time.unscaledDeltaTime;
                if (warningRemaining <= 0f && warningPanel != null)
                    warningPanel.SetActive(false);
            }

            if (feedbackRemaining > 0f)
            {
                feedbackRemaining -= Time.unscaledDeltaTime;
                if (feedbackRemaining <= 0f && feedbackText != null)
                    feedbackText.text = string.Empty;
            }
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
                "MISSION TIME: " + FormatTime(result.MissionElapsedSeconds) + "\n" + BuildObjectiveText(result.Objectives) + "\n" +
                "SCORE: " + result.Score + "     Sessions completed: " + result.SessionsCompleted + "     Exercise time: " + FormatTime(result.ExerciseSeconds) + "\n" +
                "Muscle " + result.MuscleHealth.ToString("0.0") + "   Bone " + result.BoneHealth.ToString("0.0") +
                "   Cardio " + result.CardiovascularFitness.ToString("0.0") + "   Fatigue " + result.Fatigue.ToString("0.0") + "\n" +
                "Oxygen " + result.OxygenPercent.ToString("0.0") + "%   Temperature " + result.HabitatTemperatureC.ToString("0.0") + "°C\n" +
                "Waste " + result.WasteStored.ToString("0.0") + "/" + result.WasteCapacity.ToString("0") +
                "   Power " + result.PowerStored.ToString("0.0") + " units   Power used " + result.TotalPowerUsed.ToString("0.0") + " units\n\n" +
                "Educational simulation only — not medical advice or an official exercise protocol.";
            resultPanel.SetActive(true);
        }

        public void ShowWelcomeTutorial()
        {
            if (welcomePanel == null)
                return;
            exerciseInstructionPanel?.SetActive(false);
            welcomePanel.SetActive(true);
            controller?.PlayerController?.SetUIInteraction(true);
        }

        public void DismissWelcomeTutorial()
        {
            welcomePanel?.SetActive(false);
        }

        public void OpenExerciseInstructions(ExerciseType type)
        {
            if (exerciseInstructionPanel == null)
                return;
            welcomePanel?.SetActive(false);
            exerciseInstructionPanel.SetActive(true);
            string exercise = type == ExerciseType.Squat ? "squat machine" :
                type == ExerciseType.Row ? "row machine" : "exercise bike";
            exerciseInstructionText.text = type == ExerciseType.Squat
                ? "Get ready! Your astronaut will perform 10 squats."
                : type == ExerciseType.Row
                    ? "Let's row! Complete 10 repetitions."
                    : "Pedal away! Complete 30 revolutions.";
            exerciseProgressText.text = "Ready at the " + exercise + "!";
            feedbackText.text = string.Empty;
        }

        public void CloseExerciseInstructions()
        {
            exerciseInstructionPanel?.SetActive(false);
        }

        public void CloseTransientPanels()
        {
            welcomePanel?.SetActive(false);
            exerciseInstructionPanel?.SetActive(false);
        }

        public void ShowExerciseStartUnavailable()
        {
            feedbackText.text = "Take a short rest, Astronaut, then try again!";
            feedbackRemaining = 4f;
            feedbackText.color = WarningColor;
        }

        private void RefreshExerciseProgress(SimulationSnapshot snapshot, ExerciseType type)
        {
            ExerciseObjectiveSnapshot objective = FindObjective(snapshot.Objectives, type);
            if (objective != null)
                exerciseProgressText.text = "Progress: " + objective.CompletedRepetitions + " / " + objective.TargetRepetitions;
        }

        private void RefreshObjectiveFeedback(ExerciseObjectiveSnapshot[] objectives)
        {
            if (objectives == null)
                return;

            for (int i = 0; i < objectives.Length; i++)
            {
                ExerciseObjectiveSnapshot objective = objectives[i];
                if (objective == null)
                    continue;
                int previous = previousObjectiveCounts.TryGetValue(objective.ExerciseId, out int count) ? count : 0;
                previousObjectiveCounts[objective.ExerciseId] = objective.CompletedRepetitions;
                if (objective.CompletedRepetitions <= previous)
                    continue;

                int incomplete = 0;
                for (int j = 0; j < objectives.Length; j++)
                    if (objectives[j] != null && !objectives[j].IsComplete)
                        incomplete++;

                string message = objective.IsComplete
                    ? incomplete == 0 ? "Mission complete! Fantastic work, Astronaut!" :
                        incomplete == 1 ? "Exercise completed! One more workout to go!" : "Exercise completed! Great job!"
                    : objective.TargetRepetitions - objective.CompletedRepetitions <= 1
                        ? "Almost there! You can do it!" : "Great job! Keep going!";
                feedbackText.text = message;
                feedbackRemaining = 3.5f;
                feedbackText.color = AccentColor;
            }
        }

        private static ExerciseObjectiveSnapshot FindObjective(ExerciseObjectiveSnapshot[] objectives, ExerciseType type)
        {
            if (objectives == null)
                return null;
            for (int i = 0; i < objectives.Length; i++)
                if (objectives[i] != null && objectives[i].Type == type)
                    return objectives[i];
            return null;
        }

        private static string BuildObjectiveText(ExerciseObjectiveSnapshot[] objectives)
        {
            var builder = new StringBuilder(160);
            builder.Append("EXERCISE OBJECTIVES\n");
            if (objectives == null || objectives.Length == 0)
            {
                builder.Append("Complete required exercise sessions.");
                return builder.ToString();
            }

            for (int i = 0; i < objectives.Length; i++)
            {
                ExerciseObjectiveSnapshot objective = objectives[i];
                if (objective == null)
                    continue;
                builder.Append(objective.DisplayName).Append(": ")
                    .Append(objective.CompletedRepetitions).Append(" / ")
                    .Append(objective.TargetRepetitions)
                    .Append(objective.IsComplete ? "  ✓" : string.Empty);
                if (i < objectives.Length - 1)
                    builder.Append('\n');
            }
            return builder.ToString();
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

            GameObject missionPanel = CreatePanel(canvasObject.transform, "Mission timer", new Vector2(0f, 1f),
                new Vector2(18f, -18f), new Vector2(310f, 142f));
            CreateText(missionPanel.transform, "PHYSIOLOGICAL MAINTENANCE", new Vector2(16f, 14f),
                new Vector2(278f, 28f), 18, Color.white, font, FontStyle.Bold);
            missionText = CreateText(missionPanel.transform, "--:--", new Vector2(16f, 48f),
                new Vector2(278f, 34f), 23, AccentColor, font, FontStyle.Bold);
            CreateButton(missionPanel.transform, "PAUSE", new Vector2(16f, 94f), new Vector2(112f, 34f), font,
                () => controller.ToggleSimulationPause(), out _, out simulationPauseButtonLabel);
            CreateButton(missionPanel.transform, "HOW TO PLAY", new Vector2(140f, 94f), new Vector2(154f, 34f), font,
                () => controller.ReplayWelcomeTutorial(), out _, out _);

            GameObject exercise = CreatePanel(canvasObject.transform, "Exercise stations", new Vector2(1f, 1f),
                new Vector2(-18f, -18f), new Vector2(330f, 154f));
            CreateText(exercise.transform, "EXERCISE STATIONS", new Vector2(16f, 14f),
                new Vector2(298f, 26f), 18, Color.white, font, FontStyle.Bold);
            CreateButton(exercise.transform, "SQUAT", new Vector2(16f, 52f), new Vector2(92f, 38f), font,
                () => controller.SelectExercise(ExerciseType.Squat), out var squatButton, out _);
            CreateButton(exercise.transform, "ROW", new Vector2(119f, 52f), new Vector2(92f, 38f), font,
                () => controller.SelectExercise(ExerciseType.Row), out var rowButton, out _);
            CreateButton(exercise.transform, "CYCLE", new Vector2(222f, 52f), new Vector2(92f, 38f), font,
                () => controller.SelectExercise(ExerciseType.Cycling), out var cycleButton, out _);
            squatButtonImage = squatButton.GetComponent<Image>();
            rowButtonImage = rowButton.GetComponent<Image>();
            cycleButtonImage = cycleButton.GetComponent<Image>();
            exerciseLabel = CreateText(exercise.transform, "Approach a station and press E to exercise", new Vector2(16f, 105f),
                new Vector2(298f, 34f), 14, AccentColor, font, FontStyle.Bold);

            GameObject objectives = CreatePanel(canvasObject.transform, "Exercise objectives", new Vector2(0f, 0f),
                new Vector2(18f, 18f), new Vector2(420f, 148f));
            CreateText(objectives.transform, "MISSION OBJECTIVES", new Vector2(14f, 10f),
                new Vector2(390f, 26f), 17, AccentColor, font, FontStyle.Bold);
            objectiveText = CreateText(objectives.transform, "", new Vector2(14f, 42f),
                new Vector2(390f, 78f), 16, Color.white, font, FontStyle.Bold);
            scoreText = CreateText(objectives.transform, "SCORE  0000", new Vector2(14f, 120f),
                new Vector2(390f, 22f), 16, AccentColor, font, FontStyle.Bold);

            GameObject promptPanel = CreatePanel(canvasObject.transform, "Interaction prompt", new Vector2(0.5f, 0f),
                new Vector2(0f, 18f), new Vector2(690f, 48f));
            interactionText = CreateText(promptPanel.transform, "", new Vector2(12f, 9f),
                new Vector2(666f, 30f), 15, Color.white, font, FontStyle.Bold);

            lifeSupportPanel = CreatePanel(canvasObject.transform, "Life support controls", new Vector2(1f, 0f),
                new Vector2(-18f, 18f), new Vector2(390f, 116f));
            CreateText(lifeSupportPanel.transform, "LIFE SUPPORT SYSTEMS", new Vector2(12f, 8f),
                new Vector2(366f, 24f), 15, AccentColor, font, FontStyle.Bold);
            lifeSupportStatusText = CreateText(lifeSupportPanel.transform, "", new Vector2(12f, 33f),
                new Vector2(366f, 24f), 11, Color.white, font, FontStyle.Bold);
            CreateButton(lifeSupportPanel.transform, "OXYGEN", new Vector2(12f, 65f), new Vector2(112f, 36f), font,
                () => controller.ToggleOxygenRegulator(), out _, out _);
            CreateButton(lifeSupportPanel.transform, "COOLING", new Vector2(136f, 65f), new Vector2(112f, 36f), font,
                () => controller.ToggleCooling(), out _, out _);
            CreateButton(lifeSupportPanel.transform, "WASTE", new Vector2(260f, 65f), new Vector2(112f, 36f), font,
                () => controller.ToggleWasteProcessor(), out _, out _);

            feedbackText = CreateText(canvasObject.transform, "", Vector2.zero,
                new Vector2(690f, 32f), 20, AccentColor, font, FontStyle.Bold);
            RectTransform feedbackRect = feedbackText.GetComponent<RectTransform>();
            feedbackRect.anchorMin = feedbackRect.anchorMax = feedbackRect.pivot = new Vector2(0.5f, 0f);
            feedbackRect.anchoredPosition = new Vector2(0f, 76f);
            feedbackText.alignment = TextAnchor.MiddleCenter;

            pausePanel = CreatePanel(canvasObject.transform, "Pause menu", new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(440f, 310f));
            CreateText(pausePanel.transform, "MISSION PAUSED", new Vector2(24f, 24f),
                new Vector2(392f, 40f), 26, AccentColor, font, FontStyle.Bold);
            CreateText(pausePanel.transform, "Click Resume or press Escape to continue.\nF1 releases/relocks the cursor.",
                new Vector2(24f, 82f), new Vector2(392f, 70f), 17, Color.white, font, FontStyle.Normal);
            CreateButton(pausePanel.transform, "RESUME", new Vector2(24f, 174f), new Vector2(174f, 48f), font,
                () => controller.ToggleSimulationPause(), out _, out _);
            CreateButton(pausePanel.transform, "RESTART", new Vector2(218f, 174f), new Vector2(198f, 48f), font,
                () => controller.RestartMission(), out _, out _);
            CreateButton(pausePanel.transform, "HOW TO PLAY", new Vector2(24f, 234f), new Vector2(392f, 46f), font,
                () => controller.ReplayWelcomeTutorial(), out _, out _);
            pausePanel.SetActive(false);

            welcomePanel = CreatePanel(canvasObject.transform, "Welcome Astronaut tutorial", new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(620f, 450f));
            CreateText(welcomePanel.transform, "Welcome, Astronaut!\nYour mission is to stay healthy in space!",
                new Vector2(24f, 20f), new Vector2(572f, 72f), 25, AccentColor, font, FontStyle.Bold);
            CreateText(welcomePanel.transform, "W A S D     Walk around\n\nMOUSE     Look around\n\nE     Use exercise equipment\n\nESC     Pause your mission",
                new Vector2(32f, 108f), new Vector2(556f, 220f), 22, Color.white, font, FontStyle.Bold);
            CreateText(welcomePanel.transform, "Complete squats, rows and bike revolutions to earn points!",
                new Vector2(32f, 334f), new Vector2(556f, 34f), 16, Color.white, font, FontStyle.Normal);
            CreateButton(welcomePanel.transform, "LET'S GO!", new Vector2(190f, 382f), new Vector2(240f, 46f), font,
                () => controller.DismissWelcomeTutorial(), out _, out _);
            welcomePanel.SetActive(false);

            exerciseInstructionPanel = CreatePanel(canvasObject.transform, "Exercise instructions", new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(560f, 300f));
            CreateText(exerciseInstructionPanel.transform, "READY, ASTRONAUT?", new Vector2(24f, 20f),
                new Vector2(512f, 38f), 24, AccentColor, font, FontStyle.Bold);
            exerciseInstructionText = CreateText(exerciseInstructionPanel.transform, "", new Vector2(28f, 82f),
                new Vector2(504f, 82f), 23, Color.white, font, FontStyle.Bold);
            exerciseProgressText = CreateText(exerciseInstructionPanel.transform, "", new Vector2(28f, 174f),
                new Vector2(504f, 34f), 19, AccentColor, font, FontStyle.Bold);
            CreateButton(exerciseInstructionPanel.transform, "START", new Vector2(28f, 230f), new Vector2(230f, 48f), font,
                () => controller.StartExerciseFromInstructions(), out _, out _);
            CreateButton(exerciseInstructionPanel.transform, "NOT NOW", new Vector2(278f, 230f), new Vector2(254f, 48f), font,
                () => controller.CancelExerciseInstructions(), out _, out _);
            exerciseInstructionPanel.SetActive(false);

            warningPanel = CreatePanel(canvasObject.transform, "Temporary warnings", new Vector2(0.5f, 1f),
                new Vector2(0f, -18f), new Vector2(620f, 76f));
            warningText = CreateText(warningPanel.transform, "", new Vector2(14f, 10f),
                new Vector2(592f, 56f), 15, WarningColor, font, FontStyle.Bold);
            warningPanel.SetActive(false);

            resultPanel = CreatePanel(canvasObject.transform, "Mission summary", new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(850f, 600f));
            resultPanel.SetActive(false);
            CreateText(resultPanel.transform, "MISSION DEBRIEF", new Vector2(24f, 18f),
                new Vector2(802f, 42f), 27, AccentColor, font, FontStyle.Bold);
            resultText = CreateText(resultPanel.transform, "", new Vector2(24f, 78f),
                new Vector2(802f, 420f), 17, Color.white, font, FontStyle.Normal);
            CreateButton(resultPanel.transform, "RESTART MISSION", new Vector2(24f, 525f),
                new Vector2(220f, 48f), font, () => controller.RestartMission(), out _, out _);
            CreateButton(resultPanel.transform, "CLOSE DEBRIEF", new Vector2(264f, 525f),
                new Vector2(220f, 48f), font, () => { resultDismissed = true; resultPanel.SetActive(false); }, out _, out _);
        }

        private void CreateEventSystem()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("PM Event System", typeof(EventSystem));
                eventSystemObject.transform.SetParent(transform, false);
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }

            StandaloneInputModule[] legacyModules = eventSystem.GetComponents<StandaloneInputModule>();
            for (int i = 0; i < legacyModules.Length; i++)
            {
                legacyModules[i].enabled = false;
                Destroy(legacyModules[i]);
            }

            InputSystemUIInputModule inputModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (inputModule == null)
                inputModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
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
            panel.GetComponent<Image>().color = new Color(0.035f, 0.065f, 0.09f, 0.88f);
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
            var buttonObject = new GameObject(string.IsNullOrEmpty(label) ? "Control" : label,
                typeof(RectTransform), typeof(Image), typeof(Button));
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
            buttonText = CreateText(buttonObject.transform, label, new Vector2(6f, 5f),
                new Vector2(size.x - 12f, size.y - 10f), 14, Color.white, font, FontStyle.Bold);
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
