#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using MoonCinematic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Timeline;

namespace MoonCinematic.EditorTools
{
    /// <summary>
    /// In-place repair + verification for the existing Moon_Cinematic scene:
    /// never rebuilds the scene, only re-checks and re-establishes bindings,
    /// camera default states, astronaut/wreckage placement and UI wiring.
    /// Also contains a headless Play-mode verifier that drives the director
    /// and screenshots the active Game-view camera along the timeline.
    /// </summary>
    public static class MoonCinematicFix
    {
        const string Root = "Assets/MoonCinematic";
        const string ScenePath = Root + "/Scenes/Moon_Cinematic.unity";
        const string PreviewDir = Root + "/Previews";

        [MenuItem("Moon Cinematic/Fix & Verify (in place)")]
        public static void FixFromMenu() => BatchFixCinematic();

        public static void BatchFixCinematic()
        {
            int code = 0;
            try { FixScene(); }
            catch (Exception ex) { Debug.LogException(ex); code = 2; }
            EditorApplication.Exit(code);
        }

        static void FixScene()
        {
            Debug.Log("[MoonFix] === FIX START ===");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Moon_Cinematic");
            if (root == null) { Debug.LogError("[MoonFix] root missing"); return; }

            var camRoot = root.transform.Find("Cameras");
            var cameras = camRoot != null
                ? camRoot.GetComponentsInChildren<Camera>(true).ToDictionary(c => c.gameObject.name, c => c)
                : new System.Collections.Generic.Dictionary<string, Camera>();

            // ---------------------------------------------------------------- cameras
            // An ActivationTrack toggles the bound GameObject's ACTIVE STATE, not the
            // Camera component. Therefore:
            //  - every Camera component stays enabled so a newly activated
            //    GameObject starts rendering immediately;
            //  - GameObject activeSelf must match the Timeline windows: only
            //    Shot 1's camera GameObject active at rest, others inactive (the
            //    activation track set-activates them during their windows).
            foreach (var kvp in cameras)
            {
                bool goShouldBeActive = kvp.Key.Contains("01");
                var go = kvp.Value.gameObject;
                if (go.activeSelf != goShouldBeActive)
                {
                    Debug.Log("[MoonFix] camera GameObject activeSelf changed: " + kvp.Key + " -> " + goShouldBeActive);
                    go.SetActive(goShouldBeActive);
                }
                if (!kvp.Value.enabled)
                {
                    Debug.Log("[MoonFix] camera component now always enabled: " + kvp.Key);
                    kvp.Value.enabled = true;
                }
                if (kvp.Value.targetTexture != null)
                {
                    Debug.LogWarning("[MoonFix] camera had stale targetTexture, clearing: " + kvp.Key);
                    kvp.Value.targetTexture = null;
                }
                var add = kvp.Value.GetComponent<UniversalAdditionalCameraData>();
                if (add != null && add.renderType != CameraRenderType.Base)
                {
                    Debug.LogWarning("[MoonFix] camera renderType != Base: " + kvp.Key);
                    add.renderType = CameraRenderType.Base;
                }
            }
            Debug.Log("[MoonFix] cameras: " + string.Join(", ", cameras.Keys.Select(k =>
                k + " GO=" + cameras[k].gameObject.activeSelf + " comp=" + cameras[k].enabled)));

            // ---------------------------------------------------------------- timeline bindings
            var director = root.GetComponentInChildren<PlayableDirector>(true);
            if (director == null || director.playableAsset == null) { Debug.LogError("[MoonFix] director/timeline missing"); return; }
            var timeline = director.playableAsset as TimelineAsset;
            if (timeline == null) { Debug.LogError("[MoonFix] playableAsset is not a TimelineAsset"); return; }
            Debug.Log("[MoonFix] director initial state=" + director.state + " playableAsset=" + director.playableAsset.name);

            int actBound = 0, animBound = 0;
            foreach (var track in timeline.GetOutputTracks())
            {
                var activation = track as ActivationTrack;
                if (activation != null)
                {
                    // Shot_1_Establishing -> Camera_01_Establishing (parse the index digit)
                    string camName = camNameFor(track.name);
                    Camera target = null;
                    if (camName != null && cameras.TryGetValue(camName, out target))
                    {
                        director.SetGenericBinding(track, target.gameObject);
                        actBound++;
                    }
                    else Debug.LogError("[MoonFix] cannot resolve camera for " + track.name + " -> " + (camName ?? "null"));

                    // the final shot must stay ACTIVE after playback ends, otherwise
                    // every camera deactivates and the Game view shows
                    // "No cameras rendering" during the post-playback hold
                    bool isLastShot = track.name.Contains("6");
                    var wantPost = isLastShot
                        ? ActivationTrack.PostPlaybackState.Active
                        : ActivationTrack.PostPlaybackState.Inactive;
                    if (activation.postPlaybackState != wantPost)
                    {
                        Debug.Log("[MoonFix] " + track.name + " postPlaybackState -> " + wantPost);
                        activation.postPlaybackState = wantPost;
                    }
                    foreach (var clip in activation.GetClips())
                    {
                        Debug.Log(string.Format("[MoonFix]   {0} clip '{1}' start={2:F2} dur={3:F2} asset={4}",
                            track.name, clip.displayName, clip.start, clip.duration,
                            clip.asset != null ? clip.asset.GetType().Name : "NULL"));
                    }
                }
                var anim = track as AnimationTrack;
                if (anim != null)
                {
                    var shotName = track.name.Replace("_Anim", "");
                    Camera target = null;
                    if (camNameFor(shotName) != null && cameras.TryGetValue(camNameFor(shotName), out target))
                    {
                        director.SetGenericBinding(track, target.gameObject);
                        animBound++;
                    }
                    var clip = anim.GetClips().Select(c => c.asset as AnimationPlayableAsset).FirstOrDefault();
                    Debug.Log("[MoonFix]   " + track.name + " clip=" + (clip != null && clip.clip != null ? clip.clip.name : "MISSING"));
                }
            }
            Debug.Log("[MoonFix] bindings: activation=" + actBound + " anim=" + animBound + " (of 6 each)");

            // make sure the playable graph mirrors what a player would build
            director.RebindPlayableGraphOutputs();
            director.Evaluate();
            var live = cameras.Values.FirstOrDefault(c => c.enabled && c.gameObject.activeInHierarchy);
            Debug.Log("[MoonFix] after Evaluate: renderable camera = " + (live == null ? "NONE (would be 'No cameras rendering')" : live.name));
            foreach (var kvp in cameras)
            {
                // restore rest state regardless: only camera 1's GameObject active,
                // every Camera component enabled (the Timeline switches via GO state)
                kvp.Value.enabled = true;
                kvp.Value.gameObject.SetActive(kvp.Key.Contains("01"));
            }

            // ---------------------------------------------------------------- controller / UI
            var controllers = root.GetComponentsInChildren<CinematicSequenceController>(true);
            Debug.Log("[MoonFix] CinematicSequenceController count = " + controllers.Length + (controllers.Length > 1 ? "  <-- DUPLICATES, extras removed" : ""));
            for (int i = 1; i < controllers.Length; i++) UnityEngine.Object.DestroyImmediate(controllers[i].gameObject);
            var controller = controllers.FirstOrDefault();
            if (controller != null)
            {
                Debug.Log("[MoonFix] controller storyText=" + (controller.storyText != null) + " canvas=" + (controller.uiCanvas != null)
                    + " fade=" + (controller.fadeOverlay != null) + " bars=" + (controller.topBar != null) + "/" + (controller.bottomBar != null)
                    + " lines=" + (controller.lines != null ? controller.lines.Count : 0));
                if (controller.storyText == null || controller.uiCanvas == null || controller.fadeOverlay == null ||
                    controller.topBar == null || controller.bottomBar == null)
                    Debug.LogError("[MoonFix] controller UI references incomplete");
                else
                {
                    // sanity: exactly one StoryText object under one canvas
                    var texts = root.GetComponentsInChildren<UnityEngine.UI.Text>(true);
                    Debug.Log("[MoonFix] uGUI Text objects in scene = " + texts.Length + ": " + string.Join(", ", texts.Select(t => FullPath(t.transform))));
                }
            }

            // ---------------------------------------------------------------- astronaut placement
            var astro = root.transform.Find("Astronaut/Survivor_Astronaut");
            if (astro == null) { Debug.LogError("[MoonFix] astronaut missing"); }
            else
            {
                var ap = astro.position;
                // Real terrain floor: terrainData heights already include every generated
                // crater (the builder wrote heights directly from the same HeightRaw).
                // Raycasting is not used as ground here — it can hit the sunken scorch
                // decal quad near the crater center and bury the astronaut.
                var terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
                float ground = -1000f;
                if (terrain != null)
                {
                    var tPos = terrain.transform.position;
                    var size = terrain.terrainData.size;
                    ground = terrain.terrainData.GetInterpolatedHeight(
                        (ap.x - tPos.x) / size.x, (ap.z - tPos.z) / size.z);
                }
                Debug.Log(string.Format("[MoonFix] astronaut ground={0:F2} staticGroundY(no craters)={1:F2}", ground, MoonCinematicBuilder.GroundY(ap.x, ap.z)));
                if (ground <= -900f)
                {
                    Debug.LogWarning("[MoonFix] no terrain found for astronaut placement");
                }
                else
                {
                    float targetY = ground + 0.16f;
                    if (Mathf.Abs(ap.y - targetY) > 0.05f)
                    {
                        Debug.LogWarning(string.Format("[MoonFix] astronaut y {0:F2} -> {1:F2} (terrain floor {2:F2})", ap.y, targetY, ground));
                        ap.y = targetY;
                        astro.position = ap;
                    }
                    Debug.Log(string.Format("[MoonFix] astronaut final pos={0} (lying on the surface)", ap));
                }
            }

            // ---------------------------------------------------------------- wreckage
            var wreck = root.transform.Find("CrashSite/Wreckage_Spacecraft");
            if (wreck == null) Debug.LogError("[MoonFix] wreckage missing");
            else Debug.Log("[MoonFix] wreckage root present, meshes = " + wreck.GetComponentsInChildren<Renderer>(true).Length + ", named: "
                + string.Join(", ", wreck.GetComponentsInChildren<Renderer>(true).Select(r => r.gameObject.name).Take(12)));

            // ---------------------------------------------------------------- audio listener
            // The scene had no AudioListener at all, which spams the console in play
            // mode ("There are no audio listeners in the scene"). Put one on the
            // ambience source object so it is never disabled by camera switching.
            var allListeners = root.GetComponentsInChildren<AudioListener>(true);
            Debug.Log("[MoonFix] existing AudioListeners in scene = " + allListeners.Length);
            if (allListeners.Length == 0)
            {
                var amb = root.GetComponentInChildren<AudioSource>(true);
                Transform listenerHost = amb != null ? amb.transform : camRoot;
                if (listenerHost != null)
                {
                    listenerHost.gameObject.AddComponent<AudioListener>();
                    Debug.Log("[MoonFix] AudioListener added to " + FullPath(listenerHost));
                }
                else Debug.LogError("[MoonFix] no host found for AudioListener");
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MoonFix] scene saved: " + ScenePath);
            Debug.Log("[MoonFix] === FIX DONE ===");
        }

        static string camNameFor(string shotName)
        {
            // "Shot_1_Establishing" -> "Camera_01_Establishing"
            const string Prefix = "Shot_";
            if (!shotName.StartsWith(Prefix, StringComparison.Ordinal)) return null;
            var tail = shotName.Substring(Prefix.Length);           // "1_Establishing"
            int sep = tail.IndexOf('_');
            if (sep < 1) return null;
            int idx;
            if (!int.TryParse(tail.Substring(0, sep), out idx)) return null;
            return "Camera_" + idx.ToString("00") + "_" + tail.Substring(sep + 1);
        }

        static string FullPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            while (t.parent != null) { t = t.parent; sb.Insert(0, t.name + "/"); }
            return sb.ToString();
        }

        // ============================================================== play-mode verifier

        // The domain reload that happens when entering play mode wipes normal update
        // subscriptions; InitializeOnLoadMethod re-subscribes right after the reload.
        [InitializeOnLoadMethod]
        static void AutoSubscribePlayTick()
        {
            EditorApplication.update -= PlayTick;
            EditorApplication.update += PlayTick;
        }

        // SessionState survives the play-mode domain reload (plain statics do not)
        const string SessionKeyActive = "MoonCinematicPlayVerify.Active";

        /// <summary>Headless Play-mode verification: drives the real director in play mode,
        /// logs camera switching on every shot boundary and screenshots the active
        /// Game-view camera (same camera, same post-processing) at several moments.</summary>
        public static void BatchPlayVerify()
        {
            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Directory.CreateDirectory(PreviewDir);
                SessionState.SetBool(SessionKeyActive, true);
                _lastCam = "?none";
                _samplesDone = 0;
                _director = null;
                _cinCams = null;
                EditorApplication.isPlaying = true; // play mode starts next frame
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                EditorApplication.Exit(2);
            }
        }

        static float[] _sampleTimes = { 1.5f, 4.0f, 11.0f, 14.0f, 17.0f, 21.0f, 26.0f, 33.0f, 36.0f, 41.0f, 44.0f, 46.4f, 48.4f, 50.4f };
        static int _samplesDone = 0;
        static PlayableDirector _director;
        static System.Collections.Generic.List<Camera> _cinCams;
        static string _lastCam = "?none";
        static double _lastProgressTime = -1.0;
        static float _lastRealTime = 0f;
        static int _frameCounter;

        static void PlayTick()
        {
            if (!SessionState.GetBool(SessionKeyActive, false)) return;
            if (!EditorApplication.isPlaying) return;

            try
            {
                // cache director and camera list once per play session
                if (_director == null || _cinCams == null || _cinCams.Count == 0)
                {
                    _director = UnityEngine.Object.FindObjectOfType<PlayableDirector>();
                    _cinCams = UnityEngine.Object.FindObjectsOfType<Camera>(true)
                        .Where(c => c.name.StartsWith("Camera_", StringComparison.Ordinal)).ToList();
                    if (_director == null || _cinCams == null || _cinCams.Count == 0)
                        return; // objects not spawn-settled yet
                    Debug.Log("[MoonPlay] play mode reached: cameras=" + _cinCams.Count + " director playing=" + _director.playOnAwake);
                    _lastCam = "?none";
                    _lastProgressTime = -1.0;
                }

                _frameCounter++;
                if (_frameCounter % 3 != 0) return; // sample a few times per second

                double t = Time.time; // controller clock: keeps advancing after the director finishes (end-card phase)
                var enabledNow = string.Join("|", _cinCams
                    .Where(c => c.enabled && c.gameObject.activeInHierarchy)
                    .Select(c => c.name));

                // camera switching log on every change
                if (enabledNow != _lastCam)
                {
                    _lastCam = enabledNow;
                    Debug.Log("[MoonPlay] t=" + t.ToString("F2") + " enabled=" + (enabledNow.Length == 0 ? "NONE <-- 'No cameras rendering'" : enabledNow));
                }

                // watchdog: playback must keep advancing
                if (t > _lastProgressTime + 0.0001)
                {
                    _lastProgressTime = t;
                    _lastRealTime = Time.realtimeSinceStartup;
                }
                else if (_samplesDone < _sampleTimes.Length && Time.realtimeSinceStartup - _lastRealTime > 240f)
                {
                    Debug.LogError("[MoonPlay] playback stalled for 240 s at t=" + t.ToString("F2") + " -> aborting");
                    Finish(3);
                    return;
                }

                if (_samplesDone < _sampleTimes.Length && t >= _sampleTimes[_samplesDone])
                {
                    float st = _sampleTimes[_samplesDone];
                    Debug.Log("[MoonPlay] SAMPLE t=" + st.ToString("F1") + " state=" + _director.state + " enabled=[" + enabledNow + "]");
                    Capture(_cinCams, st);
                    _samplesDone++;
                    if (_samplesDone >= _sampleTimes.Length) { Finish(0); return; }
                }
                else if (t >= 58.0 && _samplesDone < _sampleTimes.Length)
                {
                    // failsafe: never sample-stuck forever
                    Debug.LogError("[MoonPlay] sampling never reached the end (t=" + t.ToString("F1") + ") -> aborting");
                    Finish(3);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Finish(2);
            }
        }

        static void Capture(System.Collections.Generic.List<Camera> cinCams, float t)
        {
            var cam = cinCams.Where(c => c.enabled && c.gameObject.activeInHierarchy)
                .OrderByDescending(c => c.depth).FirstOrDefault();
            const int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            try
            {
                if (cam == null)
                {
                    Debug.LogError("[MoonPlay] SAMPLE t=" + t + ": NO ENABLED CAMERA -> this is the 'No cameras rendering' bug");
                    RenderTexture.active = rt;
                    GL.Clear(false, true, Color.magenta); // magenta marker frame
                    SaveRT(rt, t);
                    return;
                }
                var rtold = cam.targetTexture;
                cam.targetTexture = rt;
                // end-card samples: the story/end-card UI is Screen Space Overlay which
                // never draws into a camera RT — temporarily switch the canvas to
                // Screen Space - Camera so these captures show the full picture.
                Canvas uiCanvas = null;
                if (t >= 46f)
                {
                    uiCanvas = UnityEngine.Object.FindObjectOfType<Canvas>(false);
                    if (uiCanvas != null)
                    {
                        uiCanvas.renderMode = RenderMode.ScreenSpaceCamera;
                        uiCanvas.worldCamera = cam;
                        uiCanvas.planeDistance = 5f;
                    }
                }
                try
                {
                    cam.Render();
                }
                finally
                {
                    if (uiCanvas != null)
                    {
                        uiCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        uiCanvas.worldCamera = null;
                    }
                    cam.targetTexture = rtold;
                }
                RenderTexture.active = rt;
                SaveRT(rt, t);
            }
            finally
            {
                DestroyRT(rt);
                RenderTexture.active = null;
            }
        }

        static void SaveRT(RenderTexture rt, float t)
        {
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply(false, false);
            File.WriteAllBytes(PreviewDir + "/Play_t" + t.ToString("000").Replace(".", "_") + ".png", tex.EncodeToPNG());
            Debug.Log("[MoonPlay] screenshot saved at t=" + t);
            UnityEngine.Object.DestroyImmediate(tex);
        }

        static void DestroyRT(RenderTexture rt)
        {
            rt.Release();
            UnityEngine.Object.DestroyImmediate(rt, true);
        }

        static void Finish(int code)
        {
            SessionState.SetBool(SessionKeyActive, false);
            Debug.Log("[MoonPlay] === PLAY VERIFY DONE ===");
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(code);
        }
    }
}
#endif
