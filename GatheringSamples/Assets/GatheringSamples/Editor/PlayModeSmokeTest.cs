using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GatheringSamples.EditorTools
{
    /// <summary>
    /// Automated play-mode smoke test. It boots the playable scene, verifies the
    /// runtime-built world and HUD, runs all five sampling workflows, stows the
    /// manifest at the lander, then restarts and triggers a boundary failure.
    /// Run from the menu or in batch mode with -executeMethod ...RunBatch.
    /// </summary>
    public static class PlayModeSmokeTest
    {
        private const string ScenePath = "Assets/GatheringSamples/Scenes/LunarGathering.unity";
        private const string ActiveKey = "GatheringSamples.Smoke.Active";
        private const double TimeoutSeconds = 300.0;

        private static readonly List<string> Errors = new List<string>();
        private static readonly List<string> Notes = new List<string>();

        private static double startedAt;
        private static double phaseAt;
        private static int phase;
        private static int siteIndex;
        private static int toolIndex;
        private static int waitFrames;
        private static int lastCollected;
        private static bool began;
        private static bool validatedBoot;
        private static bool wrongToolChecked;
        private static bool pauseGuideChecked;
        private static bool restartRequested;
        private static bool failureRunBegan;
        private static bool playRequested;
        private static int readyFrames;
        private static double lastDiagnostic;

        [MenuItem("Gathering Samples/Run Play Mode Smoke Test")]
        public static void Run()
        {
            Errors.Clear();
            Notes.Clear();
            phase = 0;
            siteIndex = 0;
            toolIndex = 0;
            waitFrames = 0;
            lastCollected = 0;
            began = false;
            validatedBoot = false;
            wrongToolChecked = false;
            pauseGuideChecked = false;
            restartRequested = false;
            failureRunBegan = false;
            playRequested = false;
            readyFrames = 0;

            SessionState.SetBool(ActiveKey, true);
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            startedAt = EditorApplication.timeSinceStartup;
            phaseAt = startedAt;
            Debug.Log("[SmokeTest] Waiting for the editor to finish loading before entering play mode.");
        }

        /// <summary>
        /// Editor startup can force one domain reload (for example when this script was
        /// just imported). Re-subscribe so the test survives that reload.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void ResumeIfActive()
        {
            if (!SessionState.GetBool(ActiveKey, false)) return;

            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            startedAt = EditorApplication.timeSinceStartup;
            phaseAt = startedAt;
            readyFrames = 0;
            Debug.Log("[SmokeTest] Resumed after domain reload.");
        }

        /// <summary>Batch-mode entry point.</summary>
        public static void RunBatch()
        {
            Run();
        }

        private static void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                Errors.Add("Exception: " + condition + "\n" + stackTrace);
            }
            else if (type == LogType.Error)
            {
                // Batch-mode graphics warnings are not gameplay errors, but record them for review.
                Notes.Add("Error: " + condition);
            }
        }

        private static void Tick()
        {
            double now = EditorApplication.timeSinceStartup;

            if (!playRequested)
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                    EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    readyFrames = 0;
                    return;
                }

                // Let editor startup, script compilation and asset post-processing settle
                // completely; entering play mode mid-import forces a domain reload that
                // detaches this update callback.
                readyFrames++;
                if (readyFrames < 20 || now - startedAt < 3.0)
                {
                    return;
                }

                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                playRequested = true;
                phaseAt = EditorApplication.timeSinceStartup;
                EditorApplication.isPlaying = true;
                Debug.Log("[SmokeTest] Play mode requested.");
                return;
            }

            if (!EditorApplication.isPlaying)
            {
                return;
            }

            if (now - startedAt > TimeoutSeconds)
            {
                Finish(false, "Timed out in phase " + phase + " (site " + siteIndex + ").");
                return;
            }

            GameManager game = GameManager.Instance;
            if (game == null)
            {
                return;
            }

            if (!began)
            {
                if (now - phaseAt < 1.5)
                {
                    return;
                }

                if (!validatedBoot)
                {
                    ValidateBoot(game);
                    validatedBoot = true;
                }

                game.BeginMission();
                if (game.State != EVAState.Playing)
                {
                    Finish(false, "BeginMission did not enter Playing state.");
                    return;
                }

                Time.timeScale = 6f;
                began = true;
                phaseAt = now;
                phase = 1;
                lastCollected = 0;
                Notes.Add("Mission started.");
                return;
            }

            if (restartRequested && !failureRunBegan)
            {
                if (game.State == EVAState.Briefing && now - phaseAt > 1.5)
                {
                    game.BeginMission();
                    if (game.State != EVAState.Playing)
                    {
                        Finish(false, "Restart did not allow a second mission.");
                        return;
                    }

                    Time.timeScale = 5f;
                    Teleport(game, 100f, 100f);
                    failureRunBegan = true;
                    phaseAt = now;
                    Notes.Add("Second mission started for boundary failure test.");
                }

                return;
            }

            if (failureRunBegan)
            {
                if (game.State == EVAState.Failure)
                {
                    bool endVisible = IsEndScreenVisible(game.UI);
                    if (!endVisible)
                    {
                        Finish(false, "Failure state reached but the failure screen is hidden.");
                        return;
                    }

                    Notes.Add("Boundary failure screen shown: " + game.FailureReason);
                    Finish(true, "Passed.");
                }
                else if (game.State != EVAState.Playing)
                {
                    Finish(false, "Unexpected state during boundary test: " + game.State);
                }

                return;
            }

            if (phase == 1)
            {
                if (!pauseGuideChecked)
                {
                    pauseGuideChecked = true;
                    game.Pause();
                    if (game.State != EVAState.Paused)
                    {
                        Finish(false, "Pause() did not enter Paused state.");
                        return;
                    }

                    game.Resume();
                    if (game.State != EVAState.Playing)
                    {
                        Finish(false, "Resume() did not return to Playing.");
                        return;
                    }

                    game.ToggleFieldGuide();
                    if (game.State != EVAState.Guide)
                    {
                        Finish(false, "Field Guide did not open.");
                        return;
                    }

                    game.CloseGuide();
                    if (game.State != EVAState.Playing)
                    {
                        Finish(false, "Field Guide did not close back to Playing.");
                        return;
                    }

                    Notes.Add("Pause/resume and field guide verified.");
                }

                if (siteIndex >= Content.Sites.Length)
                {
                    phase = 4;
                    waitFrames = 3;
                    Teleport(game, 0f, 0f);
                    phaseAt = now;
                    return;
                }

                Teleport(game, Content.Sites[siteIndex].position.x + 2.5f, Content.Sites[siteIndex].position.y);
                toolIndex = 0;
                waitFrames = 2;
                phase = 2;
                phaseAt = now;
                lastDiagnostic = 0;
                return;
            }

            if (phase == 2)
            {
                if (waitFrames > 0)
                {
                    waitFrames--;
                    return;
                }

                SiteDef site = Content.Sites[siteIndex];

                // EditorApplication.update can tick more than once per rendered frame.
                // Wait in real time until the game has actually registered the player
                // at the new station before sending tool input.
                string activeId = ActiveSiteId(game);
                if (activeId != site.id)
                {
                    if (now - phaseAt > 5.0)
                    {
                        Finish(false, "Game did not register the player at " + site.id +
                                      " (active=" + activeId + ", distance=" +
                                      Vector2.Distance(new Vector2(game.Astronaut.transform.position.x,
                                          game.Astronaut.transform.position.z), site.position).ToString("F2") + ").");
                    }

                    return;
                }
                if (!wrongToolChecked && siteIndex == 0)
                {
                    wrongToolChecked = true;
                    game.SelectTool("hammer"); // REG-01 requires the scoop.
                    game.TryInteract();
                    if (game.WrongToolAttempts <= 0)
                    {
                        Finish(false, "Wrong-tool feedback was not recorded.");
                        return;
                    }

                    if (game.State != EVAState.Playing || game.CollectedCount != 0)
                    {
                        Finish(false, "Wrong tool unexpectedly advanced or completed the sample.");
                        return;
                    }

                    Notes.Add("Wrong-tool rejection verified.");
                }

                if (toolIndex < site.requiredTools.Length)
                {
                    game.SelectTool(site.requiredTools[toolIndex]);
                    game.TryInteract();
                    Debug.Log("[SmokeTest] Tool " + site.requiredTools[toolIndex] + " at " + site.id +
                              " | selected=" + game.SelectedToolId + " | collected=" + game.CollectedCount +
                              " | pos=" + game.Astronaut.transform.position.ToString("F2"));
                    toolIndex++;
                    return;
                }

                phase = 3;
                phaseAt = now;
                return;
            }

            if (phase == 3)
            {
                if (game.CollectedCount > lastCollected)
                {
                    lastCollected = game.CollectedCount;
                    Notes.Add("Collected " + Content.Sites[siteIndex].id + " (" + game.CollectedCount + "/5).");
                    siteIndex++;
                    phase = 1;
                    phaseAt = now;
                    return;
                }

                if (now - phaseAt > 30.0)
                {
                    Vector2 target = Content.Sites[siteIndex].position;
                    Vector3 player = game.Astronaut.transform.position;
                    float distance = Vector2.Distance(new Vector2(player.x, player.z), target);
                    Finish(false, "Collection stalled at site " + Content.Sites[siteIndex].id +
                                  " (collected " + game.CollectedCount + ", distance " + distance.ToString("F2") +
                                  ", oxygen " + game.Oxygen.ToString("F1") + ", state " + game.State +
                                  ", " + DescribeCollection(game) + ").");
                    return;
                }

                if (now - lastDiagnostic > 1.0)
                {
                    lastDiagnostic = now;
                    Debug.Log("[SmokeTest] Waiting on " + Content.Sites[siteIndex].id + ": " + DescribeCollection(game));
                }

                return;
            }

            if (phase == 4)
            {
                if (waitFrames > 0)
                {
                    waitFrames--;
                    return;
                }

                if (game.CollectedCount != Sim.SampleCount)
                {
                    Finish(false, "Not all samples collected: " + game.CollectedCount + "/" + Sim.SampleCount);
                    return;
                }

                game.TryInteract(); // Stow the manifest at LUNA-1.
                if (game.State != EVAState.Success)
                {
                    Finish(false, "Returning to the lander did not complete the mission. State: " + game.State);
                    return;
                }

                if (!IsEndScreenVisible(game.UI))
                {
                    Finish(false, "Success state reached but the success screen is hidden.");
                    return;
                }

                Notes.Add("Success screen shown. Best time: " +
                          PlayerPrefs.GetFloat("GatheringSamples.BestTime", -1f).ToString("0.0") + "s");

                restartRequested = true;
                phaseAt = now;
                Time.timeScale = 1f;
                game.RestartMission();
                return;
            }
        }

        private static void ValidateBoot(GameManager game)        {
            if (game.World == null) Errors.Add("WorldBuilder missing.");
            if (game.Astronaut == null) Errors.Add("AstronautRig missing.");
            if (game.CameraRig == null || game.CameraRig.Cam == null) Errors.Add("CameraRig/camera missing.");
            if (game.Audio == null) Errors.Add("AudioManager missing.");
            if (game.UI == null || game.UI.Canvas == null) Errors.Add("GameUI/canvas missing.");

            if (game.World != null && game.World.Stations.Count != Sim.SampleCount)
            {
                Errors.Add("Expected " + Sim.SampleCount + " sample stations, found " + game.World.Stations.Count + ".");
            }

            if (game.Astronaut != null)
            {
                Renderer[] renderers = game.Astronaut.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) Errors.Add("Astronaut has no renderers.");
                if (game.Astronaut.Animator == null) Errors.Add("Astronaut has no Animator.");
                if (game.Astronaut.Controller == null) Errors.Add("Astronaut has no CharacterController.");
            }

            if (game.UI != null && game.UI.Canvas != null && game.UI.Canvas.transform.childCount == 0)
            {
                Errors.Add("HUD canvas has no children.");
            }

            if (Errors.Count > 0)
            {
                Finish(false, "Boot validation failed: " + string.Join(" | ", Errors.ToArray()));
                return;
            }

            Notes.Add("Boot validation passed: world, astronaut, camera, audio and HUD present.");
        }

        private static void Teleport(GameManager game, float x, float z)
        {
            if (game.Astronaut == null) return;
            CharacterController controller = game.Astronaut.Controller;
            if (controller != null) controller.enabled = false;
            float y = WorldBuilder.TerrainHeight(x, z) + 0.25f;
            game.Astronaut.transform.position = new Vector3(x, y, z);
            if (controller != null) controller.enabled = true;
            Physics.SyncTransforms();
        }

        private static string DescribeCollection(GameManager game)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Type type = typeof(GameManager);
            string collectingText = "collecting=none";
            string activeText = "active=none";

            object collecting = type.GetField("collectingSite", Flags)?.GetValue(game);
            if (collecting != null)
            {
                collectingText = "collecting=" + SiteIdOf(collecting) + " progress=" +
                                  collecting.GetType().GetField("CollectProgress")?.GetValue(collecting);
            }

            object active = type.GetField("activeSite", Flags)?.GetValue(game);
            if (active != null)
            {
                activeText = "active=" + SiteIdOf(active) + " stage=" +
                             active.GetType().GetField("Stage")?.GetValue(active);
            }

            return collectingText + " " + activeText + " dt=" + Time.deltaTime.ToString("F3") +
                   " scale=" + Time.timeScale.ToString("F1");
        }

        private static string SiteIdOf(object runtime)
        {
            object def = runtime.GetType().GetField("Def")?.GetValue(runtime);
            return def?.GetType().GetField("id")?.GetValue(def) as string ?? "?";
        }

        private static string ActiveSiteId(GameManager game)
        {
            const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
            object active = typeof(GameManager).GetField("activeSite", Flags)?.GetValue(game);
            return active != null ? SiteIdOf(active) : "none";
        }

        private static bool IsEndScreenVisible(GameUI ui)        {
            if (ui == null) return false;
            FieldInfo field = typeof(GameUI).GetField("endRoot", BindingFlags.Instance | BindingFlags.NonPublic);
            RectTransform endRoot = field != null ? field.GetValue(ui) as RectTransform : null;
            return endRoot != null && endRoot.gameObject.activeSelf;
        }

        private static void Finish(bool passed, string message)
        {
            SessionState.SetBool(ActiveKey, false);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            Time.timeScale = 1f;

            string report = "[SmokeTest] " + (passed ? "PASS" : "FAIL") + " - " + message + "\n" +
                            "Notes:\n- " + string.Join("\n- ", Notes.ToArray());
            if (Errors.Count > 0)
            {
                report += "\nExceptions:\n- " + string.Join("\n- ", Errors.ToArray());
            }

            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
