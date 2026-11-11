using UnityEngine;
using UnityEngine.EventSystems;

namespace PhysiologicalMaintenance
{
    [DisallowMultipleComponent]
    public sealed class EquipmentStation : MonoBehaviour
    {
        [SerializeField] private ExerciseMode mode;

        public void Configure(ExerciseMode stationMode) => mode = stationMode;

        private void OnMouseDown()
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            var controller = FindFirstObjectByType<PhysiologicalMaintenanceController>();
            if (controller != null)
                controller.SelectEquipment(mode);
        }
    }
}
