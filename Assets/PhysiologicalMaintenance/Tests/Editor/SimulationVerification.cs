#if UNITY_EDITOR
using System;
using System.Reflection;
using PhysiologicalMaintenance;
using UnityEditor;
using UnityEngine;

namespace PhysiologicalMaintenance.Tests
{
    public static class SimulationVerification
    {
        [MenuItem("Tools/Physiological Maintenance/Run Simulation Checks")]
        public static void RunAll()
        {
            CheckOxygenConsumptionAndRegulation();
            CheckTemperatureAndCooling();
            CheckWasteGenerationProcessingAndCapacity();
            CheckPowerConsumptionAndInsufficientSupply();
            CheckPhysiologyAdaptationFatigueAndRecovery();
            CheckExerciseSessionStateAndReset();
            CheckCoordinatedExerciseEffectsAndMissionSuccess();
            CheckCriticalLifeSupportFailure();
            CheckLevelSceneReferences();
            Debug.Log("Physiological Maintenance verification passed: 9 simulation groups.");
        }

        private static void CheckOxygenConsumptionAndRegulation()
        {
            var oxygen = new OxygenSystem(50f);
            oxygen.Advance(1f, 2f, 1f, false, 10f, 1f);
            Near(47f, oxygen.Percent, "oxygen consumption");
            oxygen.Advance(1f, 0f, 1f, true, 10f, 1f);
            Near(56f, oxygen.Percent, "oxygen regulation");
        }

        private static void CheckTemperatureAndCooling()
        {
            var temperature = new TemperatureSystem(22f);
            temperature.Advance(1f, 2f, 0.5f, false, 1f, 1f);
            Near(24.5f, temperature.Celsius, "exercise heat");
            temperature.Advance(1f, 0f, 0f, true, 1f, 1f);
            Near(23.5f, temperature.Celsius, "active cooling");
        }

        private static void CheckWasteGenerationProcessingAndCapacity()
        {
            var waste = new WasteManagementSystem(20f, 100f);
            waste.Advance(1f, 3f, 1f, false, 2f, 1f);
            Near(24f, waste.Stored, "waste accumulation");
            waste.Advance(1f, 0f, 1f, true, 2f, 1f);
            Near(23f, waste.Stored, "waste processing");
            waste.Advance(100f, 0f, 1f, false, 0f, 1f);
            Near(100f, waste.Stored, "waste capacity clamp");
        }

        private static void CheckPowerConsumptionAndInsufficientSupply()
        {
            var power = new PowerSystem(10f);
            Near(1f, power.GetAvailableFactor(1f, 4f, 1f), "available power factor");
            power.Advance(1f, 4f, 1f, 1f);
            Near(7f, power.Stored, "power drain");
            power.Reset(0f);
            Near(0.25f, power.GetAvailableFactor(1f, 4f, 1f), "power-limited delivery");
            power.Advance(1f, 4f, 1f, 0.25f);
            Near(0f, power.Stored, "power depletion floor");
        }

        private static void CheckPhysiologyAdaptationFatigueAndRecovery()
        {
            var tuning = ScriptableObject.CreateInstance<SimulationTuning>();
            var definition = ExerciseDefinition.CreateRuntimeDefault("test", "Test Resistance", ExerciseMode.Resistance,
                1f, 0.5f, 0.2f, 10f, 2f, 2f, 1f, 1f);
            var physiology = new AstronautPhysiologySystem();
            float initialMuscle = physiology.MuscleHealth;
            float initialBone = physiology.BoneHealth;
            float initialFatigue = physiology.Fatigue;
            physiology.Advance(1f, definition, 0.7f, tuning, true);
            Require(physiology.MuscleHealth > initialMuscle, "resistance should improve muscle metric");
            Require(physiology.BoneHealth > initialBone, "resistance should improve bone metric");
            Require(physiology.Fatigue > initialFatigue, "exercise should accumulate fatigue");
            float trainedMuscle = physiology.MuscleHealth;
            float trainedFatigue = physiology.Fatigue;
            physiology.Advance(1f, null, 0f, tuning, false);
            Require(physiology.MuscleHealth < trainedMuscle, "inactivity should gradually affect muscle metric");
            Require(physiology.Fatigue < trainedFatigue, "rest should recover fatigue");
            Require(physiology.ExerciseConsistency > 48f, "training should improve consistency metric");
            UnityEngine.Object.DestroyImmediate(definition);
            UnityEngine.Object.DestroyImmediate(tuning);
        }

        private static void CheckExerciseSessionStateAndReset()
        {
            var definition = ExerciseDefinition.CreateRuntimeDefault("session", "Session", ExerciseMode.CycleErgometer,
                0f, 0f, 1f, 1f, 1f, 1f, 1f, 1f);
            var session = new ExerciseSession();
            session.Start(definition, 0.7f, 60f);
            session.Advance(20f, true);
            Near(20f, session.ElapsedSeconds, "exercise progress");
            session.TogglePause();
            session.Advance(20f, true);
            Near(20f, session.ElapsedSeconds, "paused session progress");
            session.TogglePause();
            session.Advance(40f, true);
            Require(session.State == ExerciseSessionState.Completed, "session should complete at target duration");
            session.Reset();
            Require(session.State == ExerciseSessionState.Idle && session.ElapsedSeconds == 0f, "session reset");
            UnityEngine.Object.DestroyImmediate(definition);
        }

        private static void CheckCoordinatedExerciseEffectsAndMissionSuccess()
        {
            var tuning = ScriptableObject.CreateInstance<SimulationTuning>();
            var resistance = ExerciseDefinition.CreateRuntimeDefault("success", "Test ARED", ExerciseMode.Resistance,
                0.24f, 0.17f, 0f, 6f, 4.8f, 3.2f, 1.1f, 3.5f);
            var cycle = ExerciseDefinition.CreateRuntimeDefault("success-cycle", "Test Cycle", ExerciseMode.CycleErgometer,
                0.02f, 0.01f, 0.31f, 5.2f, 6.4f, 3.4f, 1.6f, 3.7f);
            var simulation = new SimulationCoordinator(tuning);
            simulation.StartExercise(resistance, 0.65f, 120f);
            simulation.Tick(120f);
            Require(simulation.GetSnapshot().SessionsCompleted == 1, "first exercise completion");
            Near(120f, simulation.GetSnapshot().ExerciseSeconds, "exercise time should stop at the session target");
            simulation.StartExercise(cycle, 0.65f, 120f);
            simulation.Tick(120f);
            var during = simulation.GetSnapshot();
            Require(during.SessionsCompleted == 2, "second exercise completion");
            Require(during.ExerciseSeconds >= 180f, "exercise objective duration");
            Require(during.CardiovascularFitness > 57f, "cycle should improve cardiovascular fitness");
            Require(during.WasteStored > tuning.InitialWaste, "exercise should generate waste");
            Require(during.HabitatTemperatureC > tuning.InitialTemperatureC, "exercise should generate heat");
            Require(during.PowerStored < tuning.InitialPower, "exercise and life support should consume power");
            simulation.Tick(360f);
            Require(simulation.MissionEnded, "mission should end at configured duration");
            Require(simulation.GetSnapshot().MissionEnded, "mission state should be reflected in snapshot");

            simulation.Reset();
            Require(!simulation.MissionEnded && simulation.GetSnapshot().SessionsCompleted == 0, "simulation restart resets mission state");
            Near(tuning.InitialOxygenPercent, simulation.GetSnapshot().OxygenPercent, "oxygen restart");
            UnityEngine.Object.DestroyImmediate(resistance);
            UnityEngine.Object.DestroyImmediate(cycle);
            UnityEngine.Object.DestroyImmediate(tuning);
        }

        private static void CheckCriticalLifeSupportFailure()
        {
            var tuning = ScriptableObject.CreateInstance<SimulationTuning>();
            SetPrivateField(tuning, "initialOxygenPercent", 9f);
            var simulation = new SimulationCoordinator(tuning);
            MissionResult result = null;
            simulation.MissionFinished += missionResult => result = missionResult;
            simulation.Tick(1f);
            Require(simulation.MissionEnded, "critical oxygen should end the mission");
            Require(result != null && result.Outcome == MissionOutcome.LifeSupportCritical, "critical oxygen should produce failure result");

            SetPrivateField(tuning, "initialOxygenPercent", 62f);
            SetPrivateField(tuning, "initialPower", 2f);
            simulation = new SimulationCoordinator(tuning);
            result = null;
            simulation.MissionFinished += missionResult => result = missionResult;
            simulation.Tick(1f);
            Require(simulation.MissionEnded, "critical electrical reserve should end the mission");
            Require(result != null && result.Outcome == MissionOutcome.LifeSupportCritical, "critical power should produce failure result");
            UnityEngine.Object.DestroyImmediate(tuning);
        }

        private static void CheckLevelSceneReferences()
        {
            const string scenePath = "Assets/PhysiologicalMaintenance/Scenes/PhysiologicalMaintenance.unity";
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
            Require(scene.IsValid() && scene.isLoaded, "level scene should open successfully");

            var controller = UnityEngine.Object.FindFirstObjectByType<PhysiologicalMaintenanceController>();
            Require(controller != null, "level scene should contain its controller");
            Require(controller.Tuning != null, "level scene should reference tuning data");
            var serializedController = new SerializedObject(controller);
            var catalog = serializedController.FindProperty("exerciseCatalog");
            Require(catalog != null && catalog.isArray && catalog.arraySize == 3, "level scene should reference its exercise catalog");
            for (int i = 0; i < catalog.arraySize; i++)
                Require(catalog.GetArrayElementAtIndex(i).objectReferenceValue != null, "exercise catalog references should resolve");
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new InvalidOperationException("Missing test field: " + fieldName);
            field.SetValue(target, value);
        }

        private static void Near(float expected, float actual, string label)
        {
            if (Mathf.Abs(expected - actual) > 0.001f)
                throw new InvalidOperationException(label + " expected " + expected + " but got " + actual);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
#endif
