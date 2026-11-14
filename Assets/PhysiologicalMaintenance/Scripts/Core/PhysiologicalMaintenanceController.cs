using System;
using UnityEngine;

namespace PhysiologicalMaintenance
{
    [DisallowMultipleComponent]
    public sealed class PhysiologicalMaintenanceController : MonoBehaviour
    {
        [SerializeField] private SimulationTuning tuning;
        [SerializeField] private ExerciseDefinition[] exerciseCatalog = Array.Empty<ExerciseDefinition>();

        private SimulationCoordinator simulation;
        private PhysiologicalMaintenanceUI ui;
        private AstronautPlayerController playerController;
        private ExerciseDefinition selectedExercise;
        private ExerciseMode selectedMode = ExerciseMode.Resistance;
        private float exerciseIntensity = 0.65f;
        private float targetSessionSeconds = 120f;
        private float uiRefreshTimer;
        private bool simulationPaused;

        public event Action<MissionResult> MissionCompleted;
        public MissionResult LastMissionResult { get; private set; }
        public SimulationSnapshot Snapshot => simulation != null ? simulation.GetSnapshot() : null;
        public string SelectedExerciseName => selectedExercise != null ? selectedExercise.DisplayName : "No equipment selected";
        public ExerciseType SelectedExerciseType => selectedExercise != null ? selectedExercise.Type : ExerciseType.Squat;
        public float ExerciseIntensity => exerciseIntensity;
        public float TargetSessionSeconds => targetSessionSeconds;
        public ExerciseMode SelectedMode => selectedMode;
        public SimulationTuning Tuning => tuning;
        public bool SimulationPaused => simulationPaused;
        public MissionState CurrentMissionState => simulation != null ? simulation.State : MissionState.NotStarted;
        public AstronautPlayerController PlayerController => playerController;

        private void Awake()
        {
            if (tuning == null)
                tuning = ScriptableObject.CreateInstance<SimulationTuning>();
            if (exerciseCatalog == null || exerciseCatalog.Length == 0)
                exerciseCatalog = CreateDefaultCatalog();

            selectedExercise = FindExercise(selectedMode);
            simulation = new SimulationCoordinator(tuning);
            simulation.ConfigureObjectives(exerciseCatalog);
            simulation.MissionFinished += HandleMissionFinished;

            Camera camera = Camera.main;
            if (camera == null)
            {
                var cameraObject = new GameObject("PM Mission Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }
            HabitatBuilder.Build(this, camera);
            EnsureLighting();
            playerController = CreatePlayerController(camera);

            var uiObject = new GameObject("Physiological Maintenance UI");
            ui = uiObject.AddComponent<PhysiologicalMaintenanceUI>();
            ui.Initialize(this);
            simulation.StartMission();
            playerController.UpdateCursorForMissionState();
        }

        private void Update()
        {
            if (!simulationPaused && !simulation.MissionEnded)
            {
                float remaining = Time.unscaledDeltaTime * tuning.SimulationSecondsPerRealSecond;
                while (remaining > 0f)
                {
                    float step = Mathf.Min(remaining, 0.25f);
                    simulation.Tick(step);
                    remaining -= step;
                }
            }

            uiRefreshTimer += Time.unscaledDeltaTime;
            if (uiRefreshTimer >= 0.15f)
            {
                uiRefreshTimer = 0f;
                if (ui != null)
                    ui.Refresh(simulation.GetSnapshot(), this);
            }
        }

        public void SelectEquipment(ExerciseMode mode, ExerciseType type = ExerciseType.Squat)
        {
            selectedMode = mode;
            selectedExercise = FindExerciseByType(type) ?? FindExercise(mode);
            if (ui != null)
                ui.Refresh(simulation.GetSnapshot(), this);
        }

        public void SelectExercise(ExerciseType type)
        {
            if (exerciseCatalog == null)
                return;

            for (int i = 0; i < exerciseCatalog.Length; i++)
            {
                ExerciseDefinition definition = exerciseCatalog[i];
                if (definition == null || definition.Type != type)
                    continue;
                selectedExercise = definition;
                selectedMode = definition.Mode;
                if (ui != null)
                    ui.Refresh(simulation.GetSnapshot(), this);
                return;
            }
        }

        public void SelectNextExercise()
        {
            int first = -1;
            int selected = -1;
            for (int i = 0; i < exerciseCatalog.Length; i++)
            {
                if (exerciseCatalog[i] == null || exerciseCatalog[i].Mode != selectedMode)
                    continue;
                if (first < 0)
                    first = i;
                if (exerciseCatalog[i] == selectedExercise)
                    selected = i;
            }

            if (first < 0)
                return;

            int next = selected < 0 ? first : -1;
            if (selected >= 0)
            {
                for (int offset = 1; offset <= exerciseCatalog.Length; offset++)
                {
                    int candidate = (selected + offset) % exerciseCatalog.Length;
                    if (exerciseCatalog[candidate] != null && exerciseCatalog[candidate].Mode == selectedMode)
                    {
                        next = candidate;
                        break;
                    }
                }
            }
            if (next >= 0)
                selectedExercise = exerciseCatalog[next];
        }

        public void AdjustIntensity(float amount) => exerciseIntensity = Mathf.Clamp(exerciseIntensity + amount, 0.25f, 1f);
        public void AdjustSessionDuration(float amount) => targetSessionSeconds = Mathf.Clamp(targetSessionSeconds + amount, 30f, 240f);

        public void StartExercise()
        {
            if (!simulationPaused && !simulation.MissionEnded && selectedExercise != null && simulation.GetSnapshot().Fatigue < 96f)
                simulation.StartExercise(selectedExercise, exerciseIntensity, targetSessionSeconds);
        }

        public void OpenExerciseInstructions(EquipmentStation station)
        {
            if (station == null || simulationPaused || simulation.MissionEnded)
                return;

            SelectExercise(station.ExerciseType);
            ui?.OpenExerciseInstructions(station.ExerciseType);
            playerController?.SetUIInteraction(true);
        }

        public void StartExerciseFromInstructions()
        {
            if (playerController == null || !playerController.StartExerciseAtStation())
            {
                ui?.ShowExerciseStartUnavailable();
                return;
            }
            ui?.CloseExerciseInstructions();
            playerController.SetUIInteraction(false);
        }

        public void CancelExerciseInstructions()
        {
            ui?.CloseExerciseInstructions();
            playerController?.CancelStationInteraction();
            playerController?.SetUIInteraction(false);
        }

        public void DismissWelcomeTutorial()
        {
            ui?.DismissWelcomeTutorial();
            playerController?.SetUIInteraction(false);
        }

        public void ReplayWelcomeTutorial()
        {
            if (simulation.MissionEnded)
                return;
            ui?.ShowWelcomeTutorial();
            playerController?.SetUIInteraction(true);
        }

        public void ToggleExercisePause() => simulation.ToggleExercisePause();
        public void StopExercise() => simulation.StopExercise();

        public void ToggleOxygenRegulator() => simulation.OxygenRegulatorEnabled = !simulation.OxygenRegulatorEnabled;
        public void ToggleCooling() => simulation.CoolingEnabled = !simulation.CoolingEnabled;
        public void ToggleWasteProcessor() => simulation.WasteProcessorEnabled = !simulation.WasteProcessorEnabled;

        public void ToggleSimulationPause()
        {
            if (simulation.State == MissionState.NotStarted || simulation.MissionEnded)
                return;
            simulationPaused = !simulationPaused;
            simulation.SetPaused(simulationPaused);
            if (simulationPaused)
            {
                ui?.CloseTransientPanels();
                playerController?.SetUIInteraction(false);
            }
            playerController?.UpdateCursorForMissionState();
        }

        public void EndMission() => simulation.EndMission();

        public void RestartMission()
        {
            simulation.Reset();
            simulation.StartMission();
            LastMissionResult = null;
            simulationPaused = false;
            selectedExercise = FindExercise(selectedMode);
            playerController?.ResetForMission();
            if (ui != null)
                ui.ShowMissionResult(null);
        }

        private void HandleMissionFinished(MissionResult result)
        {
            LastMissionResult = result;
            playerController?.UpdateCursorForMissionState();
            if (ui != null)
                ui.ShowMissionResult(result);
            MissionCompleted?.Invoke(result);
        }

        private ExerciseDefinition FindExercise(ExerciseMode mode)
        {
            if (exerciseCatalog == null)
                return null;
            for (int i = 0; i < exerciseCatalog.Length; i++)
                if (exerciseCatalog[i] != null && exerciseCatalog[i].Mode == mode)
                    return exerciseCatalog[i];
            return null;
        }

        private ExerciseDefinition FindExerciseByType(ExerciseType type)
        {
            if (exerciseCatalog == null)
                return null;
            for (int i = 0; i < exerciseCatalog.Length; i++)
            {
                ExerciseDefinition definition = exerciseCatalog[i];
                if (definition != null && definition.Type == type)
                    return definition;
            }
            return null;
        }

        private static ExerciseDefinition[] CreateDefaultCatalog()
        {
            return new[]
            {
                ExerciseDefinition.CreateRuntimeDefault("ared-squat", "ARED Squat", ExerciseMode.Resistance,
                    0.24f, 0.17f, 0f, 6f, 4.8f, 3.2f, 1.1f, 3.5f, ExerciseType.Squat, 10, 9f),
                ExerciseDefinition.CreateRuntimeDefault("ared-row", "ARED Row", ExerciseMode.Resistance,
                    0.18f, 0.1f, 0.02f, 5f, 4.4f, 2.8f, 0.9f, 3.2f, ExerciseType.Row, 10, 9f),
                ExerciseDefinition.CreateRuntimeDefault("cycle-steady", "Cycle Ergometer", ExerciseMode.CycleErgometer,
                    0.02f, 0.01f, 0.31f, 5.2f, 6.4f, 3.4f, 1.6f, 3.7f, ExerciseType.Cycling, 30, 3f)
            };
        }

        private static void EnsureLighting()
        {
            if (FindFirstObjectByType<Light>() != null)
                return;

            var lightObject = new GameObject("Habitat Key Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(0.78f, 0.88f, 1f);
            lightObject.transform.rotation = Quaternion.Euler(38f, -28f, 0f);

            var fillObject = new GameObject("Habitat Utility Lighting");
            var fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.range = 18f;
            fill.intensity = 18f;
            fill.color = new Color(0.18f, 0.66f, 0.75f);
            fillObject.transform.position = new Vector3(0f, 4.2f, 0f);
        }

        private AstronautPlayerController CreatePlayerController(Camera camera)
        {
            var playerObject = new GameObject("PM Astronaut");
            playerObject.transform.position = new Vector3(0f, 0.16f, -4.5f);
            var body = playerObject.AddComponent<CharacterController>();
            body.height = 1.8f;
            body.radius = 0.32f;
            body.center = new Vector3(0f, 0.9f, 0f);
            body.stepOffset = 0.35f;
            body.minMoveDistance = 0f;

            var player = playerObject.AddComponent<AstronautPlayerController>();
            player.Setup(camera, this);
            return player;
        }
    }
}
