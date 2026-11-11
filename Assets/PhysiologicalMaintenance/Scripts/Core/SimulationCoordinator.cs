using System;
using UnityEngine;

namespace PhysiologicalMaintenance
{
    public sealed class SimulationSnapshot
    {
        public float MissionElapsedMinutes;
        public float MissionDurationMinutes;
        public int SessionsCompleted;
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
        private float missionElapsedMinutes;
        private float exerciseSeconds;
        private int sessionsCompleted;
        private float totalPowerUsed;
        private bool missionEnded;

        public bool OxygenRegulatorEnabled { get; set; } = true;
        public bool CoolingEnabled { get; set; }
        public bool WasteProcessorEnabled { get; set; }
        public bool MissionEnded => missionEnded;
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

        public void Reset()
        {
            missionElapsedMinutes = 0f;
            exerciseSeconds = 0f;
            sessionsCompleted = 0;
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
            if (!missionEnded && definition != null && physiology.Fatigue < 96f)
                session.Start(definition, intensity, targetSeconds);
        }

        public void ToggleExercisePause() => session.TogglePause();
        public void StopExercise() => session.Stop();

        public SimulationSnapshot Tick(float simulationSeconds)
        {
            if (missionEnded || simulationSeconds <= 0f)
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
            session.Advance(simulationSeconds, equipmentPowered);
            exerciseSeconds += Mathf.Max(0f, session.ElapsedSeconds - previousSessionElapsed);
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

            if (HasCriticalLifeSupportCondition())
            {
                Finish(MissionOutcome.LifeSupportCritical, "A life-support resource crossed its documented critical limit.");
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
                MissionElapsedMinutes = missionElapsedMinutes,
                MissionDurationMinutes = tuning.MissionDurationMinutes,
                SessionsCompleted = sessionsCompleted,
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

        private bool AreExerciseObjectivesMet() =>
            sessionsCompleted >= tuning.ExerciseSessionObjective && exerciseSeconds >= tuning.ExerciseObjectiveSeconds;

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
            session.Stop();
            MissionFinished?.Invoke(new MissionResult
            {
                Outcome = outcome,
                Assessment = assessment,
                MissionElapsedMinutes = missionElapsedMinutes,
                SessionsCompleted = sessionsCompleted,
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
