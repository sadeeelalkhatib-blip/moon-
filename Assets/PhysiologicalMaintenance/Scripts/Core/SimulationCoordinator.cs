using System;
using System.Collections.Generic;
using UnityEngine;

namespace PhysiologicalMaintenance
{
    public sealed class SimulationSnapshot
    {
        public MissionState MissionState;
        public float MissionElapsedSeconds;
        public ExerciseObjectiveSnapshot[] Objectives;
        public float MissionElapsedMinutes;
        public float MissionDurationMinutes;
        public int SessionsCompleted;
        public int Score;
        public float ExerciseSeconds;
        public ExerciseSession Session;
        public float MuscleHealth;
        public float BoneHealth;
        public float CardiovascularFitness;
        public float Fatigue;
        public float ExerciseConsistency;
        public float OxygenPercent;
        public float HabitatTemperatureC;
        public float WasteStored;
        public float WasteCapacity;
        public float PowerStored;
        public float PowerCapacity;
        public float CurrentPowerDemand;
        public bool OxygenRegulatorEnabled;
        public bool CoolingEnabled;
        public bool WasteProcessorEnabled;
        public bool MissionEnded;
    }

    public sealed class SimulationCoordinator
    {
        private readonly SimulationTuning tuning;
        private readonly AstronautPhysiologySystem physiology = new AstronautPhysiologySystem();
        private readonly ExerciseSession session = new ExerciseSession();
        private readonly OxygenSystem oxygen;
        private readonly TemperatureSystem temperature;
        private readonly WasteManagementSystem waste;
        private readonly PowerSystem power;
        private readonly Dictionary<string, int> repetitionProgress = new Dictionary<string, int>();
        private ExerciseDefinition[] objectives = Array.Empty<ExerciseDefinition>();
        private float missionElapsedMinutes;
        private float missionElapsedSeconds;
        private float exerciseSeconds;
        private int sessionsCompleted;
        private int score;
        private MissionState missionState = MissionState.NotStarted;
        private float totalPowerUsed;
        private bool missionEnded;

        public bool OxygenRegulatorEnabled { get; set; } = true;
        public bool CoolingEnabled { get; set; }
        public bool WasteProcessorEnabled { get; set; }
        public bool MissionEnded => missionEnded;
        public MissionState State => missionState;
        public event Action<MissionResult> MissionFinished;

        public SimulationCoordinator(SimulationTuning simulationTuning)
        {
            tuning = simulationTuning;
            oxygen = new OxygenSystem(tuning.InitialOxygenPercent);
            temperature = new TemperatureSystem(tuning.InitialTemperatureC);
            waste = new WasteManagementSystem(tuning.InitialWaste, tuning.WasteCapacity);
            power = new PowerSystem(tuning.InitialPower);
        }

        public ExerciseSession Session => session;

        public void ConfigureObjectives(ExerciseDefinition[] definitions)
        {
            objectives = definitions ?? Array.Empty<ExerciseDefinition>();
            repetitionProgress.Clear();
            for (int i = 0; i < objectives.Length; i++)
                if (objectives[i] != null && objectives[i].TargetRepetitions > 0)
                    repetitionProgress[objectives[i].ExerciseId] = 0;
        }

        public void StartMission()
        {
            if (!missionEnded && missionState == MissionState.NotStarted)
                missionState = MissionState.InProgress;
        }

        public void SetPaused(bool paused)
        {
            if (missionEnded || missionState == MissionState.NotStarted)
                return;
            missionState = paused ? MissionState.Paused : MissionState.InProgress;
        }

        public void Reset()
        {
            missionElapsedMinutes = 0f;
            missionElapsedSeconds = 0f;
            exerciseSeconds = 0f;
            sessionsCompleted = 0;
            score = 0;
            missionState = MissionState.NotStarted;
            for (int i = 0; i < objectives.Length; i++)
                if (objectives[i] != null && objectives[i].TargetRepetitions > 0)
                    repetitionProgress[objectives[i].ExerciseId] = 0;
            totalPowerUsed = 0f;
            missionEnded = false;
            physiology.Reset();
            oxygen.Reset(tuning.InitialOxygenPercent);
            temperature.Reset(tuning.InitialTemperatureC);
            waste.Reset(tuning.InitialWaste);
            power.Reset(tuning.InitialPower);
            session.Reset();
            OxygenRegulatorEnabled = true;
            CoolingEnabled = false;
            WasteProcessorEnabled = false;
        }

        public void StartExercise(ExerciseDefinition definition, float intensity, float targetSeconds)
        {
            if (missionEnded || definition == null || physiology.Fatigue >= 96f)
                return;

            StartMission();
            int remainingRepetitions = definition.TargetRepetitions;
            if (remainingRepetitions > 0)
            {
                int completed = GetCompletedRepetitions(definition);
                remainingRepetitions = Mathf.Max(0, definition.TargetRepetitions - completed);
                if (remainingRepetitions == 0)
                    return;
            }
            session.Start(definition, intensity, targetSeconds, remainingRepetitions);
        }

        public void ToggleExercisePause() => session.TogglePause();
        public void StopExercise() => session.Stop();

        public SimulationSnapshot Tick(float simulationSeconds)
        {
            if (missionEnded || missionState != MissionState.InProgress || simulationSeconds <= 0f)
                return GetSnapshot();

            float deltaMinutes = simulationSeconds / 60f;
            ExerciseDefinition exercise = session.Definition;
            bool requestedExercise = session.IsRunning && exercise != null;
            float requestedIntensity = requestedExercise ? session.Intensity : 0f;

            float demand = tuning.HabitatPowerPerMinute;
            if (requestedExercise)
                demand += exercise.PowerUsePerMinute * requestedIntensity;
            if (OxygenRegulatorEnabled)
                demand += tuning.OxygenRegulatorPowerPerMinute;
            if (CoolingEnabled)
                demand += tuning.CoolingPowerPerMinute;
            if (WasteProcessorEnabled)
                demand += tuning.WasteProcessorPowerPerMinute;

            float powerFactor = power.GetAvailableFactor(deltaMinutes, demand, tuning.SolarSupplyPerMinute);
            bool equipmentPowered = powerFactor >= 0.6f;
            float exerciseIntensity = requestedExercise && equipmentPowered ? requestedIntensity : 0f;

            ExerciseSessionState previousSessionState = session.State;
            float previousSessionElapsed = session.ElapsedSeconds;
            int previousRepetitions = session.RepetitionsCompleted;
            session.Advance(simulationSeconds, equipmentPowered);
            exerciseSeconds += Mathf.Max(0f, session.ElapsedSeconds - previousSessionElapsed);
            if (exercise != null && exercise.TargetRepetitions > 0)
            {
                int completedThisTick = session.RepetitionsCompleted - previousRepetitions;
                AddCompletedRepetitions(exercise, completedThisTick);
                score += completedThisTick * 10;
            }
            if (previousSessionState == ExerciseSessionState.Running && session.State == ExerciseSessionState.Completed)
                sessionsCompleted++;

            physiology.Advance(deltaMinutes, exerciseIntensity > 0f ? exercise : null, exerciseIntensity,
                tuning, equipmentPowered && exerciseIntensity > 0f);
            oxygen.Advance(deltaMinutes,
                exerciseIntensity > 0f ? exercise.OxygenUsePerMinute * exerciseIntensity : 0f,
                tuning.IdleOxygenUsePerMinute, OxygenRegulatorEnabled,
                tuning.OxygenRegenerationPerMinute, powerFactor);
            temperature.Advance(deltaMinutes,
                exerciseIntensity > 0f ? exercise.HeatPerMinute * exerciseIntensity : 0f,
                tuning.PassiveTemperatureDriftPerMinute, CoolingEnabled,
                tuning.CoolingPerMinute, powerFactor);
            waste.Advance(deltaMinutes,
                exerciseIntensity > 0f ? exercise.WastePerMinute * exerciseIntensity : 0f,
                tuning.IdleWastePerMinute, WasteProcessorEnabled,
                tuning.WasteProcessingPerMinute, powerFactor);
            power.Advance(deltaMinutes, demand, tuning.SolarSupplyPerMinute, powerFactor);
            totalPowerUsed += demand * powerFactor * deltaMinutes;
            missionElapsedMinutes = Mathf.Min(tuning.MissionDurationMinutes, missionElapsedMinutes + deltaMinutes);
            float simulationRate = Mathf.Max(0.01f, tuning.SimulationSecondsPerRealSecond);
            missionElapsedSeconds += simulationSeconds / simulationRate;

            if (HasCriticalLifeSupportCondition())
            {
                Finish(MissionOutcome.LifeSupportCritical, "A life-support resource crossed its documented critical limit.");
            }
            else if (HasConfiguredRepetitionObjectives() && AreExerciseObjectivesMet())
            {
                Finish(MissionOutcome.Success, "All required exercise repetitions were completed while life support remained operational.");
            }
            else if (missionElapsedMinutes >= tuning.MissionDurationMinutes)
            {
                bool objectivesMet = AreExerciseObjectivesMet();
                Finish(objectivesMet ? MissionOutcome.Success : MissionOutcome.ObjectivesIncomplete,
                    objectivesMet
                        ? "Exercise objectives were met while life support remained within safe operating limits."
                        : "The habitat remained operational, but one or more exercise objectives were not completed.");
            }

            return GetSnapshot();
        }

        public SimulationSnapshot GetSnapshot()
        {
            return new SimulationSnapshot
            {
                MissionState = missionState,
                MissionElapsedSeconds = missionElapsedSeconds,
                Objectives = BuildObjectiveSnapshots(),
                MissionElapsedMinutes = missionElapsedMinutes,
                MissionDurationMinutes = tuning.MissionDurationMinutes,
                SessionsCompleted = sessionsCompleted,
                Score = score,
                ExerciseSeconds = exerciseSeconds,
                Session = session,
                MuscleHealth = physiology.MuscleHealth,
                BoneHealth = physiology.BoneHealth,
                CardiovascularFitness = physiology.CardiovascularFitness,
                Fatigue = physiology.Fatigue,
                ExerciseConsistency = physiology.ExerciseConsistency,
                OxygenPercent = oxygen.Percent,
                HabitatTemperatureC = temperature.Celsius,
                WasteStored = waste.Stored,
                WasteCapacity = waste.Capacity,
                PowerStored = power.Stored,
                PowerCapacity = power.Capacity,
                CurrentPowerDemand = power.LastDemandPerMinute,
                OxygenRegulatorEnabled = OxygenRegulatorEnabled,
                CoolingEnabled = CoolingEnabled,
                WasteProcessorEnabled = WasteProcessorEnabled,
                MissionEnded = missionEnded
            };
        }

        public void EndMission()
        {
            if (missionEnded)
                return;

            if (HasCriticalLifeSupportCondition())
            {
                Finish(MissionOutcome.LifeSupportCritical, "A life-support resource crossed its documented critical limit.");
                return;
            }

            bool objectivesMet = AreExerciseObjectivesMet();
            Finish(objectivesMet ? MissionOutcome.Success : MissionOutcome.ObjectivesIncomplete,
                objectivesMet ? "Mission concluded with exercise objectives met." : "Mission ended before all exercise objectives were met.");
        }

        private bool HasConfiguredRepetitionObjectives()
        {
            for (int i = 0; i < objectives.Length; i++)
                if (objectives[i] != null && objectives[i].TargetRepetitions > 0)
                    return true;
            return false;
        }

        private bool AreExerciseObjectivesMet()
        {
            if (HasConfiguredRepetitionObjectives())
            {
                for (int i = 0; i < objectives.Length; i++)
                {
                    ExerciseDefinition objective = objectives[i];
                    if (objective != null && objective.TargetRepetitions > 0 &&
                        GetCompletedRepetitions(objective) < objective.TargetRepetitions)
                        return false;
                }
                return true;
            }
            return sessionsCompleted >= tuning.ExerciseSessionObjective && exerciseSeconds >= tuning.ExerciseObjectiveSeconds;
        }

        private int GetCompletedRepetitions(ExerciseDefinition definition)
        {
            return definition != null && repetitionProgress.TryGetValue(definition.ExerciseId, out int completed)
                ? completed : 0;
        }

        private void AddCompletedRepetitions(ExerciseDefinition definition, int repetitions)
        {
            if (definition == null || repetitions <= 0 || !repetitionProgress.ContainsKey(definition.ExerciseId))
                return;
            repetitionProgress[definition.ExerciseId] = Mathf.Min(definition.TargetRepetitions,
                GetCompletedRepetitions(definition) + repetitions);
        }

        private ExerciseObjectiveSnapshot[] BuildObjectiveSnapshots()
        {
            var snapshots = new List<ExerciseObjectiveSnapshot>();
            for (int i = 0; i < objectives.Length; i++)
            {
                ExerciseDefinition definition = objectives[i];
                if (definition == null || definition.TargetRepetitions <= 0)
                    continue;
                int completed = GetCompletedRepetitions(definition);
                snapshots.Add(new ExerciseObjectiveSnapshot
                {
                    ExerciseId = definition.ExerciseId,
                    DisplayName = definition.DisplayName,
                    Type = definition.Type,
                    TargetRepetitions = definition.TargetRepetitions,
                    CompletedRepetitions = completed,
                    IsComplete = completed >= definition.TargetRepetitions
                });
            }
            return snapshots.ToArray();
        }

        private bool HasCriticalLifeSupportCondition() =>
            oxygen.Percent <= tuning.OxygenCriticalPercent ||
            temperature.Celsius <= tuning.TemperatureCriticalLowC ||
            temperature.Celsius >= tuning.TemperatureCriticalHighC ||
            waste.Stored >= waste.Capacity ||
            power.Stored <= tuning.PowerCritical;

        private void Finish(MissionOutcome outcome, string assessment)
        {
            if (missionEnded)
                return;

            missionEnded = true;
            missionState = outcome == MissionOutcome.Success ? MissionState.Completed : MissionState.Failed;
            session.Stop();
            MissionFinished?.Invoke(new MissionResult
            {
                Outcome = outcome,
                State = missionState,
                Objectives = BuildObjectiveSnapshots(),
                Assessment = assessment,
                MissionElapsedSeconds = missionElapsedSeconds,
                MissionElapsedMinutes = missionElapsedMinutes,
                SessionsCompleted = sessionsCompleted,
                Score = score,
                ExerciseSeconds = exerciseSeconds,
                MuscleHealth = physiology.MuscleHealth,
                BoneHealth = physiology.BoneHealth,
                CardiovascularFitness = physiology.CardiovascularFitness,
                Fatigue = physiology.Fatigue,
                ExerciseConsistency = physiology.ExerciseConsistency,
                OxygenPercent = oxygen.Percent,
                HabitatTemperatureC = temperature.Celsius,
                WasteStored = waste.Stored,
                WasteCapacity = waste.Capacity,
                PowerStored = power.Stored,
                TotalPowerUsed = totalPowerUsed
            });
        }
    }
}
