using System;

namespace PhysiologicalMaintenance
{
    public enum MissionOutcome
    {
        Success,
        ObjectivesIncomplete,
        LifeSupportCritical
    }

    public enum MissionState
    {
        NotStarted,
        InProgress,
        Paused,
        Completed,
        Failed
    }

    [Serializable]
    public sealed class ExerciseObjectiveSnapshot
    {
        public string ExerciseId;
        public string DisplayName;
        public ExerciseType Type;
        public int TargetRepetitions;
        public int CompletedRepetitions;
        public bool IsComplete;
    }

    [Serializable]
    public sealed class MissionResult
    {
        public MissionOutcome Outcome;
        public MissionState State;
        public ExerciseObjectiveSnapshot[] Objectives;
        public float MissionElapsedSeconds;
        public string Assessment;
        public float MissionElapsedMinutes;
        public int SessionsCompleted;
        public float ExerciseSeconds;
        public int Score;
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
