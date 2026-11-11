using UnityEngine;

namespace PhysiologicalMaintenance
{
    public enum ExerciseMode
    {
        Resistance,
        CycleErgometer
    }

    [CreateAssetMenu(menuName = "Physiological Maintenance/Exercise Definition")]
    public sealed class ExerciseDefinition : ScriptableObject
    {
        [SerializeField] private string exerciseId = "resistance-basic";
        [SerializeField] private string displayName = "Resistance Training";
        [SerializeField] private ExerciseMode mode = ExerciseMode.Resistance;
        [SerializeField, Range(0f, 4f)] private float muscleAdaptationPerMinute = 0.18f;
        [SerializeField, Range(0f, 4f)] private float boneAdaptationPerMinute = 0.12f;
        [SerializeField, Range(0f, 4f)] private float cardiovascularAdaptationPerMinute;
        [SerializeField, Range(0f, 20f)] private float fatiguePerMinute = 6f;
        [SerializeField, Range(0f, 20f)] private float oxygenUsePerMinute = 4f;
        [SerializeField, Range(0f, 20f)] private float powerUsePerMinute = 2.8f;
        [SerializeField, Range(0f, 5f)] private float heatPerMinute = 0.55f;
        [SerializeField, Range(0f, 5f)] private float wastePerMinute = 0.7f;

        public string ExerciseId => exerciseId;
        public string DisplayName => displayName;
        public ExerciseMode Mode => mode;
        public float MuscleAdaptationPerMinute => muscleAdaptationPerMinute;
        public float BoneAdaptationPerMinute => boneAdaptationPerMinute;
        public float CardiovascularAdaptationPerMinute => cardiovascularAdaptationPerMinute;
        public float FatiguePerMinute => fatiguePerMinute;
        public float OxygenUsePerMinute => oxygenUsePerMinute;
        public float PowerUsePerMinute => powerUsePerMinute;
        public float HeatPerMinute => heatPerMinute;
        public float WastePerMinute => wastePerMinute;

        public static ExerciseDefinition CreateRuntimeDefault(string id, string label, ExerciseMode exerciseMode,
            float muscleGain, float boneGain, float cardioGain, float fatigue, float oxygen, float power,
            float heat, float wasteGeneration)
        {
            var definition = CreateInstance<ExerciseDefinition>();
            definition.exerciseId = id;
            definition.displayName = label;
            definition.mode = exerciseMode;
            definition.muscleAdaptationPerMinute = muscleGain;
            definition.boneAdaptationPerMinute = boneGain;
            definition.cardiovascularAdaptationPerMinute = cardioGain;
            definition.fatiguePerMinute = fatigue;
            definition.oxygenUsePerMinute = oxygen;
            definition.powerUsePerMinute = power;
            definition.heatPerMinute = heat;
            definition.wastePerMinute = wasteGeneration;
            return definition;
        }
    }
}
