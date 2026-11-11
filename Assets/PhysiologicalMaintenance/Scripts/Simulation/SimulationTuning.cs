using UnityEngine;

namespace PhysiologicalMaintenance
{
    [CreateAssetMenu(menuName = "Physiological Maintenance/Simulation Tuning")]
    public sealed class SimulationTuning : ScriptableObject
    {
        [Header("Mission")]
        [SerializeField, Min(1f)] private float missionDurationMinutes = 10f;
        [SerializeField, Min(0.1f)] private float simulationSecondsPerRealSecond = 6f;
        [SerializeField, Min(0f)] private float exerciseObjectiveSeconds = 180f;
        [SerializeField, Min(1)] private int exerciseSessionObjective = 2;

        [Header("Initial resources")]
        [SerializeField, Range(0f, 100f)] private float initialOxygenPercent = 62f;
        [SerializeField, Range(0f, 100f)] private float initialPower = 52f;
        [SerializeField, Range(0f, 100f)] private float initialWaste = 70f;
        [SerializeField, Range(0f, 100f)] private float wasteCapacity = 100f;
        [SerializeField, Range(10f, 35f)] private float initialTemperatureC = 22f;

        [Header("Physiology: educational gameplay approximation")]
        [SerializeField, Range(0f, 5f)] private float muscleInactivityLossPerMinute = 0.035f;
        [SerializeField, Range(0f, 5f)] private float boneInactivityLossPerMinute = 0.025f;
        [SerializeField, Range(0f, 5f)] private float cardioInactivityLossPerMinute = 0.03f;
        [SerializeField, Range(0f, 10f)] private float fatigueRecoveryPerMinute = 2.4f;
        [SerializeField, Range(0f, 2f)] private float consistencyGainPerTrainingMinute = 0.32f;
        [SerializeField, Range(0f, 2f)] private float consistencyLossPerIdleMinute = 0.08f;
        [SerializeField, Range(0f, 1f)] private float fatigueEffectOnTraining = 0.65f;

        [Header("Oxygen: percentage points per simulated minute")]
        [SerializeField, Min(0f)] private float idleOxygenUsePerMinute = 0.7f;
        [SerializeField, Min(0f)] private float oxygenRegenerationPerMinute = 8f;
        [SerializeField, Range(0f, 100f)] private float oxygenWarningPercent = 35f;
        [SerializeField, Range(0f, 100f)] private float oxygenCriticalPercent = 10f;

        [Header("Temperature: degrees Celsius per simulated minute")]
        [SerializeField, Min(0f)] private float passiveTemperatureDriftPerMinute = 0.12f;
        [SerializeField, Min(0f)] private float coolingPerMinute = 2.4f;
        [SerializeField, Range(0f, 20f)] private float coolingPowerPerMinute = 1.8f;
        [SerializeField, Range(0f, 20f)] private float temperatureWarningLowC = 18f;
        [SerializeField, Range(0f, 40f)] private float temperatureWarningHighC = 29f;
        [SerializeField, Range(0f, 20f)] private float temperatureCriticalLowC = 14f;
        [SerializeField, Range(20f, 50f)] private float temperatureCriticalHighC = 36f;

        [Header("Waste and power")]
        [SerializeField, Min(0f)] private float idleWastePerMinute = 0.12f;
        [SerializeField, Min(0f)] private float wasteProcessingPerMinute = 2.2f;
        [SerializeField, Min(0f)] private float wasteProcessorPowerPerMinute = 0.7f;
        [SerializeField, Range(0f, 100f)] private float wasteWarningFraction = 0.8f;
        [SerializeField, Min(0f)] private float habitatPowerPerMinute = 0.45f;
        [SerializeField, Min(0f)] private float solarSupplyPerMinute = 0.65f;
        [SerializeField, Range(0f, 20f)] private float oxygenRegulatorPowerPerMinute = 0.9f;
        [SerializeField, Range(0f, 100f)] private float lowPowerWarning = 18f;
        [SerializeField, Range(0f, 100f)] private float powerCritical = 3f;

        public float MissionDurationMinutes => missionDurationMinutes;
        public float SimulationSecondsPerRealSecond => simulationSecondsPerRealSecond;
        public float ExerciseObjectiveSeconds => exerciseObjectiveSeconds;
        public int ExerciseSessionObjective => exerciseSessionObjective;
        public float InitialOxygenPercent => initialOxygenPercent;
        public float InitialPower => initialPower;
        public float InitialWaste => initialWaste;
        public float WasteCapacity => wasteCapacity;
        public float InitialTemperatureC => initialTemperatureC;
        public float MuscleInactivityLossPerMinute => muscleInactivityLossPerMinute;
        public float BoneInactivityLossPerMinute => boneInactivityLossPerMinute;
        public float CardioInactivityLossPerMinute => cardioInactivityLossPerMinute;
        public float FatigueRecoveryPerMinute => fatigueRecoveryPerMinute;
        public float ConsistencyGainPerTrainingMinute => consistencyGainPerTrainingMinute;
        public float ConsistencyLossPerIdleMinute => consistencyLossPerIdleMinute;
        public float FatigueEffectOnTraining => fatigueEffectOnTraining;
        public float IdleOxygenUsePerMinute => idleOxygenUsePerMinute;
        public float OxygenRegenerationPerMinute => oxygenRegenerationPerMinute;
        public float OxygenWarningPercent => oxygenWarningPercent;
        public float OxygenCriticalPercent => oxygenCriticalPercent;
        public float PassiveTemperatureDriftPerMinute => passiveTemperatureDriftPerMinute;
        public float CoolingPerMinute => coolingPerMinute;
        public float CoolingPowerPerMinute => coolingPowerPerMinute;
        public float TemperatureWarningLowC => temperatureWarningLowC;
        public float TemperatureWarningHighC => temperatureWarningHighC;
        public float TemperatureCriticalLowC => temperatureCriticalLowC;
        public float TemperatureCriticalHighC => temperatureCriticalHighC;
        public float IdleWastePerMinute => idleWastePerMinute;
        public float WasteProcessingPerMinute => wasteProcessingPerMinute;
        public float WasteProcessorPowerPerMinute => wasteProcessorPowerPerMinute;
        public float WasteWarningFraction => wasteWarningFraction;
        public float HabitatPowerPerMinute => habitatPowerPerMinute;
        public float SolarSupplyPerMinute => solarSupplyPerMinute;
        public float OxygenRegulatorPowerPerMinute => oxygenRegulatorPowerPerMinute;
        public float LowPowerWarning => lowPowerWarning;
        public float PowerCritical => powerCritical;
    }
}
