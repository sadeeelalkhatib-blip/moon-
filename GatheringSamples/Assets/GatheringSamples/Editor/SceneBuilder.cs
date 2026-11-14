using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GatheringSamples.EditorTools
{
    /// <summary>
    /// Creates the playable bootstrap scene and registers it as the startup scene.
    /// The scene intentionally contains only one GameObject: every world, astronaut,
    /// camera, audio and UI object is built at runtime by GameManager.
    /// </summary>
    public static class SceneBuilder
    {
        public const string ScenePath = "Assets/GatheringSamples/Scenes/LunarGathering.unity";

        [MenuItem("Gathering Samples/Rebuild Playable Scene")]
        public static void BuildScene()
        {
            string directory = Path.GetDirectoryName(ScenePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject bootstrap = new GameObject("Game Bootstrap");
            bootstrap.AddComponent<GameManager>();
            bootstrap.transform.position = Vector3.zero;

            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            if (!saved)
            {
                Debug.LogError("[GatheringSamples] Failed to save scene at " + ScenePath);
                return;
            }

            AssetDatabase.Refresh();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            AssetDatabase.SaveAssets();
            Debug.Log("[GatheringSamples] Playable scene created: " + ScenePath);
        }

        /// <summary>Batch-mode entry point.</summary>
        public static void BuildSceneBatch()
        {
            BuildScene();
        }

        [MenuItem("Gathering Samples/Open Playable Scene")]
        public static void OpenScene()
        {
            if (File.Exists(ScenePath))
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }
            else
            {
                BuildScene();
            }
        }
    }
}
