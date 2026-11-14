using UnityEngine;

namespace PhysiologicalMaintenance
{
    public enum ExerciseSessionState
    {
        Idle,
        Running,
        Paused,
        Completed,
        Interrupted
    }

    public sealed class ExerciseSession
    {
        private float repetitionClock;

        public ExerciseDefinition Definition { get; private set; }
        public float Intensity { get; private set; }
        public float TargetSeconds { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public int TargetRepetitions { get; private set; }
        public int RepetitionsCompleted { get; private set; }
        public float RepetitionProgress => TargetRepetitions <= 0 ? 0f : Mathf.Clamp01(repetitionClock / Definition.RepetitionCycleSeconds);
        public ExerciseSessionState State { get; private set; } = ExerciseSessionState.Idle;
        public float Progress => TargetRepetitions > 0
            ? Mathf.Clamp01((RepetitionsCompleted + RepetitionProgress) / TargetRepetitions)
            : TargetSeconds <= 0f ? 0f : Mathf.Clamp01(ElapsedSeconds / TargetSeconds);
        public bool IsRunning => State == ExerciseSessionState.Running;

        public void Start(ExerciseDefinition definition, float intensity, float targetSeconds, int targetRepetitions = 0)
        {
            if (definition == null)
                return;

            Definition = definition;
            Intensity = Mathf.Clamp(intensity, 0.25f, 1f);
            TargetSeconds = Mathf.Max(30f, targetSeconds);
            TargetRepetitions = Mathf.Max(0, targetRepetitions);
            RepetitionsCompleted = 0;
            ElapsedSeconds = 0f;
            repetitionClock = 0f;
            State = ExerciseSessionState.Running;
        }

        public void TogglePause()
        {
            if (State == ExerciseSessionState.Running)
                State = ExerciseSessionState.Paused;
            else if (State == ExerciseSessionState.Paused)
                State = ExerciseSessionState.Running;
        }

        public void Stop()
        {
            if (State == ExerciseSessionState.Running || State == ExerciseSessionState.Paused)
                State = ExerciseSessionState.Interrupted;
        }

        public void Advance(float simulationSeconds, bool equipmentPowered)
        {
            if (!IsRunning || !equipmentPowered || simulationSeconds <= 0f)
                return;

            float activeSeconds = Mathf.Min(simulationSeconds, Mathf.Max(0f, TargetSeconds - ElapsedSeconds));
            ElapsedSeconds += activeSeconds;
            if (TargetRepetitions > 0)
            {
                repetitionClock += activeSeconds;
                float cycleSeconds = Mathf.Max(0.1f, Definition.RepetitionCycleSeconds);
                while (repetitionClock >= cycleSeconds && RepetitionsCompleted < TargetRepetitions)
                {
                    repetitionClock -= cycleSeconds;
                    RepetitionsCompleted++;
                }
                if (RepetitionsCompleted >= TargetRepetitions || ElapsedSeconds >= TargetSeconds)
                    State = ExerciseSessionState.Completed;
            }
            else if (ElapsedSeconds >= TargetSeconds)
            {
                State = ExerciseSessionState.Completed;
            }
        }

        public void Reset()
        {
            Definition = null;
            Intensity = 0f;
            TargetSeconds = 0f;
            ElapsedSeconds = 0f;
            TargetRepetitions = 0;
            RepetitionsCompleted = 0;
            repetitionClock = 0f;
            State = ExerciseSessionState.Idle;
        }
    }
}
