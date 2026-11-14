#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PhysiologicalMaintenance.Tests
{
    [InitializeOnLoad]
    public static class PlayModeCharacterVerification
    {
        private const string ScenePath = "Assets/PhysiologicalMaintenance/Scenes/PhysiologicalMaintenance.unity";
        private const string ScreenshotPath = "/tmp/pm-character-playmode.png";
        private const string RequestedKey = "PM_CharacterPlayModeCheckRequested";
        private const string CapturedKey = "PM_CharacterPlayModeCheckCaptured";
        private static int frames;
        private static bool captureRequested;

        static PlayModeCharacterVerification()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            if (EditorApplication.isPlaying && SessionState.GetBool(RequestedKey, false))
                EditorApplication.update += VerifyRunningGame;
        }

        public static void OpenSceneAndCapturePlayMode()
        {
            SessionState.SetBool(RequestedKey, true);
            SessionState.SetBool(CapturedKey, false);
            if (File.Exists(ScreenshotPath))
                File.Delete(ScreenshotPath);
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            EditorApplication.delayCall += StartPlayMode;
        }

        private static void StartPlayMode()
        {
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.isPlaying = true;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(RequestedKey, false))
            {
                frames = 0;
                captureRequested = false;
                EditorApplication.update += VerifyRunningGame;
            }
            else if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(RequestedKey, false))
            {
                SessionState.SetBool(RequestedKey, false);
                EditorApplication.update -= VerifyRunningGame;
                Debug.Log("PM CHARACTER PLAYMODE SCREENSHOT: " +
                          (SessionState.GetBool(CapturedKey, false) && File.Exists(ScreenshotPath)
                              ? ScreenshotPath
                              : "not captured; see preceding Play Mode errors"));
            }
        }

        private static void VerifyRunningGame()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorApplication.update -= VerifyRunningGame;
                return;
            }

            frames++;
            if (!captureRequested && frames == 20)
            {
                var mission = Object.FindFirstObjectByType<PhysiologicalMaintenanceController>();
                var player = GameObject.Find("PM Astronaut");
                var visual = player != null ? player.transform.Find("Man Base Character (Downloaded FBX)") : null;
                var renderers = visual != null ? visual.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
                Debug.Log("PM CHARACTER PLAYMODE CHECK: state=" + (mission != null ? mission.CurrentMissionState.ToString() : "missing controller") +
                          ", playerRoot=" + (player != null) +
                          ", downloadedModel=" + (visual != null) +
                          ", rendererCount=" + renderers.Length +
                          ", modelWorldPosition=" + (visual != null ? visual.position.ToString() : "n/a") +
                          ", humanAnimator=" + (visual != null && visual.GetComponentInChildren<Animator>() != null));
                if (mission == null || player == null || visual == null || renderers.Length == 0)
                {
                    EditorApplication.isPlaying = false;
                    return;
                }
                mission.DismissWelcomeTutorial();
            }

            if (!captureRequested && frames >= 90)
            {
                ScreenCapture.CaptureScreenshot(ScreenshotPath, 1);
                captureRequested = true;
                Debug.Log("PM CHARACTER PLAYMODE CAPTURE REQUESTED: " + ScreenshotPath);
            }

            if (captureRequested && File.Exists(ScreenshotPath) && new FileInfo(ScreenshotPath).Length > 20000)
            {
                SessionState.SetBool(CapturedKey, true);
                EditorApplication.isPlaying = false;
            }
            else if (frames > 360)
            {
                EditorApplication.isPlaying = false;
            }
        }
    }
}
#endif
