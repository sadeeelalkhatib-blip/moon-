using System;

namespace PhysiologicalMaintenance
{
    public enum MissionOutcome
    {
        Success,
        ObjectivesIncomplete,
        LifeSupportCritical
    }

    [Serializable]
    public sealed class MissionResult
    {
        public MissionOutcome Outcome;
        public string Assessment;
        public float MissionElapsedMinutes;
        public int SessionsCompleted;
        public float ExerciseSeconds;
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
        public float TotalPowerUsed;
    }
}
