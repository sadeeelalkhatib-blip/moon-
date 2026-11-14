using UnityEngine;
using UnityEngine.EventSystems;

namespace PhysiologicalMaintenance
{
    [DisallowMultipleComponent]
    public sealed class EquipmentStation : MonoBehaviour
    {
        [SerializeField] private ExerciseMode mode;
        [SerializeField] private ExerciseType exerciseType = ExerciseType.Squat;
        [SerializeField] private float interactionRadius = 2.2f;
        private Renderer highlightRenderer;
        private Material highlightMaterial;
        private float highlightPhase;

        public ExerciseMode Mode => mode;
        public string FriendlyName => exerciseType == ExerciseType.Squat ? "Squat Machine" :
            exerciseType == ExerciseType.Row ? "Row Machine" : "Exercise Bike";
        public ExerciseType ExerciseType => exerciseType;
        public float InteractionRadius => interactionRadius;

        public void Configure(ExerciseMode stationMode, ExerciseType stationType = ExerciseType.Squat, float radius = 2.2f)
        {
            mode = stationMode;
            exerciseType = stationType;
            interactionRadius = radius;
        }

        public void SetHighlightRenderer(Renderer renderer, Material material)
        {
            highlightRenderer = renderer;
            highlightMaterial = material;
            SetHighlighted(false);
        }

        public void SetHighlighted(bool highlighted)
        {
            if (highlightRenderer != null)
                highlightRenderer.enabled = highlighted;
        }

        private void Update()
        {
            if (highlightRenderer == null || !highlightRenderer.enabled || highlightMaterial == null)
                return;

            highlightPhase += Time.deltaTime * 2.4f;
            Color color = Color.Lerp(new Color(0.1f, 0.9f, 1f), new Color(1f, 0.82f, 0.22f),
                (Mathf.Sin(highlightPhase) + 1f) * 0.5f);
            highlightMaterial.SetColor("_BaseColor", color);
            if (highlightMaterial.HasProperty("_EmissionColor"))
                highlightMaterial.SetColor("_EmissionColor", color * 1.8f);
        }

        public bool IsWithinInteractionRange(Vector3 playerPosition)
        {
            float distance = Vector3.Distance(playerPosition, transform.position);
            return distance <= interactionRadius;
        }

        private void OnMouseDown()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            var controller = FindFirstObjectByType<PhysiologicalMaintenanceController>();
            if (controller != null)
                controller.SelectEquipment(mode, exerciseType);
        }
    }
}
