using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;
using MoonCinematic;

namespace MoonCinematic.EditorTools
{
    /// <summary>
    /// Quality checks: opens Moon_Cinematic, verifies key references, and renders
    /// a preview PNG for each cinematic camera into Assets/MoonCinematic/Previews.
    /// </summary>
    public static class MoonCinematicChecker
    {
        const string ScenePath = "Assets/MoonCinematic/Scenes/Moon_Cinematic.unity";
        const string PreviewDir = "Assets/MoonCinematic/Previews";

        [MenuItem("Moon Cinematic/Check & Render Previews")]
        public static void CheckFromMenu() => RunAllChecks(false);

        public static void BatchCheck()
        {
            int code = 0;
            try { RunAllChecks(true); }
            catch (Exception ex) { Debug.LogException(ex); code = 2; }
            EditorApplication.Exit(code);
        }

        public static void RunAllChecks(bool batch)
        {
            if (!Directory.Exists(PreviewDir)) Directory.CreateDirectory(PreviewDir);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("[MoonCheck] opened " + ScenePath);

            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Moon_Cinematic");
            if (root == null) { Debug.LogError("[MoonCheck] root missing"); return; }

            // cameras
            var camRoot = root.transform.Find("Cameras");
            var cams = camRoot.GetComponentsInChildren<Camera>(true).ToDictionary(c => c.gameObject.name, c => c);
            foreach (var need in new[] { "Camera_01_Establishing", "Camera_02_CrashSite", "Camera_03_Astronaut", "Camera_04_Earth", "Camera_05_Wreckage", "Camera_06_FinalWide" })
                Debug.Log("[MoonCheck] camera " + need + " -> " + (cams.ContainsKey(need) ? "OK" : "MISSING"));

            // director & timeline
            var director = root.GetComponentInChildren<PlayableDirector>(true);
            Debug.Log("[MoonCheck] PlayableDirector -> " + (director != null && director.playableAsset != null ? "OK (" + director.playableAsset.name + ")" : "MISSING"));
            if (director != null)
            {
                var timeline = director.playableAsset as UnityEngine.Timeline.TimelineAsset;
                if (timeline != null)
                {
                    Debug.Log("[MoonCheck] timeline tracks = " + timeline.outputTrackCount);
                    foreach (var t in timeline.GetOutputTracks())
                        Debug.Log("[MoonCheck]   track: " + t.name + (director.GetGenericBinding(t) != null || t is UnityEngine.Timeline.ActivationTrack ? " bound->" + (director.GetGenericBinding(t) != null ? director.GetGenericBinding(t).name : "(dynamic)") : " UNBOUND"));
                }
            }

            // astronaut + casualty of importance
            Debug.Log("[MoonCheck] astronaut -> " + (root.transform.Find("Astronaut/Survivor_Astronaut") != null ? "OK" : "MISSING"));
            Debug.Log("[MoonCheck] crash site -> " + (root.transform.Find("CrashSite/Wreckage_Spacecraft") != null ? "OK" : "MISSING"));
            Debug.Log("[MoonCheck] earth -> " + (root.transform.Find("Space/Earth_Distant") != null ? "OK" : "MISSING"));
            var controller = root.GetComponentInChildren<CinematicSequenceController>(true);
            Debug.Log("[MoonCheck] story controller -> " + (controller != null && controller.storyText != null && controller.fadeOverlay != null ? "OK" : "MISSING"));
            var audio = root.GetComponentsInChildren<AudioSource>(true);
            Debug.Log("[MoonCheck] audio sources = " + audio.Length + (audio.Any(a => a.clip != null) ? " with clip OK" : " NO CLIP"));

            // missing scripts check
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            foreach (var c in t.gameObject.GetComponents<Component>())
                if (c == null) Debug.LogError("[MoonCheck] missing script on " + FullPath(t));

            // render previews
            Directory.CreateDirectory(PreviewDir);
            int w = 960, h = 540;
            foreach (var kvp in cams)
            {
                var cam = kvp.Value;
                var add = cam.GetComponent<UniversalAdditionalCameraData>();
                var oldAA = add.antialiasing;
                add.antialiasing = AntialiasingMode.None;

                var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.enabled = true;
                try
                {
                    cam.Render();
                    cam.Render(); // second pass stabilizes post/TAA-ish setups
                    RenderTexture.active = rt;
                    var tex = new Texture2D(w, h, TextureFormat.RGB24, false, false);
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    tex.Apply(false, false);
                    File.WriteAllBytes(PreviewDir + "/" + kvp.Key + ".png", tex.EncodeToPNG());
                    Debug.Log("[MoonCheck] preview written: " + kvp.Key);
                    UnityEngine.Object.DestroyImmediate(tex);
                    RenderTexture.active = null;
                }
                catch (Exception ex)
                {
                    Debug.LogError("[MoonCheck] render failed for " + kvp.Key + ": " + ex.Message);
                }
                finally
                {
                    cam.targetTexture = null;
                    cam.enabled = false;
                    add.antialiasing = oldAA;
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(rt, true);
                }
            }

            // restore default enabled state (Camera 01 active)
            foreach (var kvp in cams)
                kvp.Value.enabled = kvp.Key.Contains("01");

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[MoonCheck] === CHECKS DONE ===");
        }

        static string FullPath(Transform t)
        {
            var sb = new System.Text.StringBuilder(t.name);
            while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }
    }
}
