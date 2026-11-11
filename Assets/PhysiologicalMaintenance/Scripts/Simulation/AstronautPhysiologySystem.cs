using UnityEngine;

namespace PhysiologicalMaintenance
{
    public sealed class AstronautPhysiologySystem
    {
        public float MuscleHealth { get; private set; } = 56f;
        public float BoneHealth { get; private set; } = 59f;
        public float CardiovascularFitness { get; private set; } = 57f;
        public float Fatigue { get; private set; } = 14f;
        public float ExerciseConsistency { get; private set; } = 48f;

        public void Reset()
        {
            MuscleHealth = 56f;
            BoneHealth = 59f;
            CardiovascularFitness = 57f;
            Fatigue = 14f;
            ExerciseConsistency = 48f;
        }

        public void Advance(float deltaMinutes, ExerciseDefinition exercise, float intensity,
            SimulationTuning tuning, bool equipmentPowered)
        {
            if (deltaMinutes <= 0f)
                return;

            bool training = equipmentPowered && exercise != null && intensity > 0f;
            if (training)
            {
                float fatiguePenalty = 1f - (Fatigue / 100f) * tuning.FatigueEffectOnTraining;
                float consistencyFactor = 0.55f + ExerciseConsistency / 220f;
                float effectiveIntensity = Mathf.Clamp01(intensity) * Mathf.Clamp01(fatiguePenalty);
                MuscleHealth = Mathf.Clamp(MuscleHealth + exercise.MuscleAdaptationPerMinute * effectiveIntensity * consistencyFactor * deltaMinutes, 0f, 100f);
                BoneHealth = Mathf.Clamp(BoneHealth + exercise.BoneAdaptationPerMinute * effectiveIntensity * consistencyFactor * deltaMinutes, 0f, 100f);
                CardiovascularFitness = Mathf.Clamp(CardiovascularFitness + exercise.CardiovascularAdaptationPerMinute * effectiveIntensity * consistencyFactor * deltaMinutes, 0f, 100f);
                Fatigue = Mathf.Clamp(Fatigue + exercise.FatiguePerMinute * Mathf.Lerp(0.35f, 1f, intensity) * deltaMinutes, 0f, 100f);
                ExerciseConsistency = Mathf.Clamp(ExerciseConsistency + tuning.ConsistencyGainPerTrainingMinute * deltaMinutes, 0f, 100f);
            }
            else
            {
                MuscleHealth = Mathf.Clamp(MuscleHealth - tuning.MuscleInactivityLossPerMinute * deltaMinutes, 0f, 100f);
                BoneHealth = Mathf.Clamp(BoneHealth - tuning.BoneInactivityLossPerMinute * deltaMinutes, 0f, 100f);
                CardiovascularFitness = Mathf.Clamp(CardiovascularFitness - tuning.CardioInactivityLossPerMinute * deltaMinutes, 0f, 100f);
                Fatigue = Mathf.Clamp(Fatigue - tuning.FatigueRecoveryPerMinute * deltaMinutes, 0f, 100f);
                ExerciseConsistency = Mathf.Clamp(ExerciseConsistency - tuning.ConsistencyLossPerIdleMinute * deltaMinutes, 0f, 100f);
            }
        }
    }
}
