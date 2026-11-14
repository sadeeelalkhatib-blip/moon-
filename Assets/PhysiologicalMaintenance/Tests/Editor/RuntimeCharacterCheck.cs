#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysiologicalMaintenance.Tests
{
    public static class RuntimeCharacterCheck
    {
        public static void CheckPlayerModelBinding()
        {
            var scene = EditorSceneManager.OpenScene("Assets/PhysiologicalMaintenance/Scenes/PhysiologicalMaintenance.unity", OpenSceneMode.Single);
            var controller = Object.FindFirstObjectByType<PhysiologicalMaintenanceController>();
            var player = GameObject.Find("PM Astronaut");
            if (controller == null)
                throw new System.InvalidOperationException("PhysiologicalMaintenanceController missing from scene.");
            if (player == null)
                throw new System.InvalidOperationException("PM Astronaut missing from scene.");

            var visual = player.transform.Find("Man Base Character (Downloaded FBX)");
            var renderers = visual != null ? visual.GetComponentsInChildren<Renderer>(true).Length : 0;
            var playerController = player.GetComponent<AstronautPlayerController>();
            var modelRoot = playerController != null ? playerController.GetType().GetField("modelRoot", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance) : null;
            var modelRootValue = modelRoot != null ? modelRoot.GetValue(playerController) : null;
            Debug.Log("Scene open and player bound. controller=" + controller.name +
                      " player=" + player.name +
                      " childVisual=" + (visual != null ? visual.name : "none") +
                      " renderers=" + renderers +
                      " modelRootField=" + (modelRootValue != null ? ((Transform)modelRootValue).name : "null") +
                      " resourcesPrefab=" + (Resources.Load<GameObject>("PhysiologicalMaintenance/Characters/ManBaseCharacter") != null));
            EditorApplication.Exit(0);
        }
    }
}
#endif
