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
        public ExerciseDefinition Definition { get; private set; }
        public float Intensity { get; private set; }
        public float TargetSeconds { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public ExerciseSessionState State { get; private set; } = ExerciseSessionState.Idle;
        public float Progress => TargetSeconds <= 0f ? 0f : Mathf.Clamp01(ElapsedSeconds / TargetSeconds);
        public bool IsRunning => State == ExerciseSessionState.Running;

        public void Start(ExerciseDefinition definition, float intensity, float targetSeconds)
        {
            if (definition == null)
                return;

            Definition = definition;
            Intensity = Mathf.Clamp(intensity, 0.25f, 1f);
            TargetSeconds = Mathf.Max(30f, targetSeconds);
            ElapsedSeconds = 0f;
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

            ElapsedSeconds = Mathf.Min(TargetSeconds, ElapsedSeconds + simulationSeconds);
            if (ElapsedSeconds >= TargetSeconds)
                State = ExerciseSessionState.Completed;
        }

        public void Reset()
        {
            Definition = null;
            Intensity = 0f;
            TargetSeconds = 0f;
            ElapsedSeconds = 0f;
            State = ExerciseSessionState.Idle;
        }
    }
}
