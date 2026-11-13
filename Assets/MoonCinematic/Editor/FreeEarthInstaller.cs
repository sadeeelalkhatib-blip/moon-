using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MoonCinematic.EditorTools
{
    /// <summary>
    /// Batch-mode importer for the official Free Earth Planet asset store package.
    /// Usage:
    ///   Unity.exe -batchmode -quit -projectPath ... -executeMethod MoonCinematic.EditorTools.FreeEarthInstaller.Import
    ///             -importEarthPackage C:\path\package.unitypackage -logFile ...
    /// </summary>
    public static class FreeEarthInstaller
    {
        public static void Import()
        {
            try
            {
                var args = Environment.GetCommandLineArgs();
                string pkg = null;
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-importEarthPackage")
                        pkg = args[i + 1];

                if (string.IsNullOrEmpty(pkg) || !File.Exists(pkg))
                {
                    Debug.LogError("[FreeEarthInstaller] package path missing: " + pkg);
                    EditorApplication.Exit(30);
                    return;
                }

                Debug.Log("[FreeEarthInstaller] importing package: " + pkg);
                AssetDatabase.ImportPackage(pkg, false);   // false = no import dialog (batch safe)
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                var all = AssetDatabase.FindAssets("t:Object", new[] { "Assets/Earth" })
                    .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(x => x).ToList();
                Debug.Log("[FreeEarthImporter] imported " + all.Count + " assets:");
                foreach (var p in all) Debug.Log("[FreeEarthImporter] " + p);

                var prefab = all.FirstOrDefault(p => p.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase));
                Debug.Log("[FreeEarthImporter] prefab=" + prefab);

                if (Directory.Exists("Assets/Earth"))
                {
                    Debug.Log("[FreeEarthImporter] OK_IMPORT");
                    EditorApplication.Exit(0);
                }
                else
                {
                    Debug.LogError("[FreeEarthImporter] FAIL_IMPORT");
                    EditorApplication.Exit(31);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[FreeEarthInstaller] exception: " + e);
                EditorApplication.Exit(32);
            }
        }

        // ------------------------------------------------------------------
        // Finalize: bake icosphere mesh into the prefab, align sun direction,
        // verify sizes, then let MoonCinematicChecker render previews.
        // ------------------------------------------------------------------

        public static void FinalizeEarth()
        {
            try
            {
                const string scenePath = "Assets/MoonCinematic/Scenes/Moon_Cinematic.unity";
                const string prefabPath = "Assets/Earth/Prefab/Earth.prefab";
                const string meshAssetPath = "Assets/Earth/Meshes/Earth_IcoSphere_D4_R20.asset";

                // 1. bake the generated mesh into the prefab asset
                var prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
                bool meshOk = false;
                try
                {
                    var sphere = prefabRoot.GetComponentInChildren<DigitalRuby.Earth.SphereScript>(true);
                    if (sphere == null) { Debug.LogError("[EarthFinalize] SphereScript missing on prefab"); }
                    else
                    {
                        var t = typeof(DigitalRuby.Earth.SphereScript);
                        var gen = t.GetMethod("CreateIcoSphere",
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (gen == null) { Debug.LogError("[EarthFinalize] CreateIcoSphere not found"); }
                        else
                        {
                            gen.Invoke(sphere, null);
                            var mf = sphere.GetComponent<MeshFilter>();
                            if (mf != null && mf.sharedMesh != null)
                            {
                                mf.sharedMesh.name = "Earth_IcoSphere_D4_R20";
                                if (!Directory.Exists("Assets/Earth/Meshes")) Directory.CreateDirectory("Assets/Earth/Meshes");
                                if (File.Exists(meshAssetPath)) AssetDatabase.DeleteAsset(meshAssetPath);
                                AssetDatabase.CreateAsset(mf.sharedMesh, meshAssetPath);
                                Debug.Log("[EarthFinalize] mesh asset created: " + meshAssetPath +
                                          " verts=" + mf.sharedMesh.vertexCount + " diameter=" + mf.sharedMesh.bounds.size.y);
                                meshOk = true;

                                // stop the script from regenerating over the baked mesh
                                var so = new SerializedObject(sphere);
                                so.FindProperty("lastRadius").floatValue = 20f;
                                so.FindProperty("lastDetail").intValue = 4;
                                so.FindProperty("lastIcoSphere").boolValue = true;
                                so.FindProperty("dirty").boolValue = false;
                                so.ApplyModifiedPropertiesWithoutUndo();
                            }
                            else Debug.LogError("[EarthFinalize] mesh not generated");
                        }
                    }
                }
                finally
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                }
                if (!meshOk) { EditorApplication.Exit(50); return; }
                AssetDatabase.SaveAssets();

                // 2. open scene and inspect/repair the instance
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var earth = FindByName(roots, "Earth_Distant");
                if (earth == null) { Debug.LogError("[EarthFinalize] Earth_Distant missing in scene"); EditorApplication.Exit(51); return; }
                var rend = earth.GetComponentInChildren<MeshRenderer>(true);
                if (rend != null)
                {
                    Debug.Log(string.Format("[EarthFinalize] instance renderer mesh={0} worldBounds={1} center={2}",
                        rend.sharedMaterial != null ? rend.sharedMaterial.name : "NULL", rend.bounds.size, rend.bounds.center));
                }

                // material sun direction from the scene's Sun_Lunar
                var sunGO = FindByName(roots, "Sun_Lunar");
                if (sunGO != null && rend != null && rend.sharedMaterial != null)
                {
                    var sunLight = sunGO.GetComponent<Light>();
                    var dir = -sunLight.transform.forward; // light points away from sun
                    var mat = rend.sharedMaterial;
                    mat.SetVector("_SunDir", new Vector4(dir.x, dir.y, dir.z, 0f));
                    mat.SetColor("_SunColor", new Color(sunLight.color.r, sunLight.color.g, sunLight.color.b, Mathf.Min(4f, sunLight.intensity * 0.01f)));
                    EditorUtility.SetDirty(mat);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[EarthFinalize] material SunDir=" + dir + " sunIntensity=" + sunLight.intensity);
                }

                // 3. counts + save
                Debug.Log("[EarthFinalize] diagnostics:");
                var rootTf = earth.transform;
                Debug.Log(string.Format("[EarthFinalize] root localScale={0} lossyScale={1} pos={2}",
                    rootTf.localScale, rootTf.lossyScale, rootTf.position));
                var space = rootTf.parent;
                if (space != null)
                    Debug.Log(string.Format("[EarthFinalize] parent={0} localScale={1} lossyScale={2}",
                        space.gameObject.name, space.localScale, space.lossyScale));
                foreach (var mf2 in rootTf.GetComponentsInChildren<MeshFilter>(true))
                {
                    var r2 = mf2.GetComponent<MeshRenderer>();
                    var m2 = mf2.sharedMesh;
                    Debug.Log(string.Format("[EarthFinalize] filter on={0} mesh={1} localBounds={2} rendererBounds={3} mat={4} goLossyScale={5} goActive={6}",
                        mf2.gameObject.name,
                        m2 != null ? m2.name : "NULL",
                        m2 != null ? m2.bounds.size.ToString() : "-",
                        r2 != null ? r2.bounds.size.ToString() : "-",
                        r2 != null && r2.sharedMaterial != null ? r2.sharedMaterial.name : "NULL",
                        mf2.transform.lossyScale, mf2.gameObject.activeInHierarchy));
                }
                int count = 0;
                foreach (var t in earth.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Earth_Distant") count++;
                Debug.Log("[EarthFinalize] Earth_Distant count=" + count);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[EarthFinalize] SCENE_SAVED");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[EarthFinalize] exception: " + e);
                EditorApplication.Exit(52);
            }
        }

        // ------------------------------------------------------------------
        // Earth replacement
        // ------------------------------------------------------------------

        // ------------------------------------------------------------------
        // Calibration: match the ORIGINAL Earth's apparent size/persona -
        //   old previews showed a small (~33 px at 960x540) fully lit marble;
        //   first render of the asset showed a 325 px night-side planet.
        //   Pixel ratio 33/325 -> scale factor 0.1015 (31.25 -> ~3.17).
        //   Original Earth was an UNLIT quad (always fully lit), so the sun
        //   direction is set toward the Earth-view camera and the EarthScript
        //   per-frame sun override is disabled on the scene instance.
        // ------------------------------------------------------------------

        public static void CalibrateEarth()
        {
            try
            {
                const string scenePath = "Assets/MoonCinematic/Scenes/Moon_Cinematic.unity";
                // final calibration target: old preview disc 34x36 px, current 28x30 px
                // -> scale 3.17 * 35/29 ~= 3.80 (multi-run safe: absolute value)
                var targetScale = new Vector3(3.80f, 3.80f, 3.80f);

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var earth = FindByName(roots, "Earth_Distant");
                if (earth == null) { Debug.LogError("[EarthCalib] Earth_Distant missing"); EditorApplication.Exit(60); return; }

                // 1. apparent-size calibration
                var tf = earth.transform;
                var s = tf.localScale;
                tf.localScale = targetScale;
                Debug.Log(string.Format("[EarthCalib] localScale {0} -> {1} (world diameter ~{2:F0})",
                    s, tf.localScale, 40f * tf.lossyScale.y));

                // 2. stop the per-frame sun-direction override (shows night side;
                //    the original Earth was an unlit always-lit quad)
                var earthScript = tf.GetComponentInChildren<DigitalRuby.Earth.EarthScript>(true);
                if (earthScript != null)
                {
                    earthScript.enabled = false;
                    Debug.Log("[EarthCalib] EarthScript disabled on instance (constant lit look, like the original)");
                }

                // 3. sun direction toward the Earth-view camera + full sun strength
                var rend = tf.GetComponentInChildren<MeshRenderer>(true);
                var mat = rend != null ? rend.sharedMaterial : null;
                if (mat != null)
                {
                    Vector3 sunDir;
                    var cam = FindByName(roots, "Camera_04_Earth");
                    if (cam != null) sunDir = (cam.transform.position - tf.position).normalized;
                    else sunDir = new Vector3(0.624f, -0.387f, 0.679f).normalized; // fallback: earth -> camera_04
                    mat.SetVector("_SunDir", new Vector4(sunDir.x, sunDir.y, sunDir.z, 0f));
                    mat.SetColor("_SunColor", new Color(1f, 0.965f, 0.91f, 1f)); // shader default, full strength
                    EditorUtility.SetDirty(mat);
                    AssetDatabase.SaveAssets();
                    Debug.Log("[EarthCalib] material _SunDir=" + sunDir + " _SunColor.a=1 (full sun)");
                }
                else Debug.LogError("[EarthCalib] renderer/material missing");

                // 4. verify
                int count = 0;
                foreach (var t in tf.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Earth_Distant") count++;
                Debug.Log("[EarthCalib] Earth_Distant count=" + count +
                          " pos=" + tf.position + " parent=" + (tf.parent != null ? tf.parent.name : "null"));
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[EarthCalib] SCENE_SAVED");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[EarthCalib] exception: " + e);
                EditorApplication.Exit(62);
            }
        }

        static GameObject FindByName(GameObject[] roots, string name)
        {
            foreach (var r in roots)
            {
                if (r.name == name) return r;
                foreach (var t in r.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            }
            return null;
        }

        /// <summary>
        /// Batch-mode replacement of the procedural Earth_Distant object with the
        /// newly imported Earth prefab. Usage:
        ///   Unity.exe -batchmode -projectPath ... -executeMethod
        ///             MoonCinematic.EditorTools.FreeEarthInstaller.ReplaceEarth -logFile ...
        /// Preserves the original position, world scale (diameter), rotation and
        /// distance to Moon/cameras. Uses the new asset's own prefab/material/shader.
        /// </summary>
        public static void ReplaceEarth()
        {
            try
            {
                const string scenePath = "Assets/MoonCinematic/Scenes/Moon_Cinematic.unity";
                const string prefabPath = "Assets/Earth/Prefab/Earth.prefab";
                const string newEarthName = "Earth_Distant"; // keep scene/checker references stable

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogError("[EarthReplace] prefab missing: " + prefabPath);
                    EditorApplication.Exit(40);
                    return;
                }

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                var spaceGO = FindByName(roots, "Space");
                GameObject oldEarth = null;
                if (spaceGO != null)
                {
                    var oldT = spaceGO.transform.Find("Earth_Distant");
                    if (oldT != null) oldEarth = oldT.gameObject;
                }
                if (oldEarth == null) oldEarth = FindByName(roots, "Earth_Distant");

                // capture the original placement
                var oldWorldPos = new Vector3(-1606.547f, 1036.4819f, -1762.0193f);
                var oldWorldRot = new Quaternion(0.18983653f, 0.35371208f, -0.07355166f, 0.91292936f);
                float oldWorldDiameter = 1250f;
                GameObject parent = null;
                if (oldEarth != null)
                {
                    var oldTf = oldEarth.transform;
                    oldWorldPos = oldTf.position;
                    oldWorldRot = oldTf.rotation;
                    oldWorldDiameter = oldTf.lossyScale.y; // built-in sphere: diameter == scale (r=0.5)
                    parent = oldTf.parent != null ? oldTf.parent.gameObject : null;
                    Debug.Log(string.Format("[EarthReplace] captured old state pos={0} rot={1} lossyScale={2}",
                        oldWorldPos, oldWorldRot.eulerAngles, oldTf.lossyScale));
                }
                else
                {
                    // The old procedural Earth was removed from the scene outside this
                    // session; fall back to the exact transform captured beforehand so
                    // the composition (position, size, distance to moon/cameras) matches.
                    if (spaceGO == null)
                    {
                        Debug.LogError("[EarthReplace] neither old Earth nor Space parent found");
                        EditorApplication.Exit(41);
                        return;
                    }
                    parent = spaceGO;
                    Debug.Log("[EarthReplace] old Earth missing; using captured snapshot transform, parent=Space");
                }

                // instantiate the asset's own prefab
                var newGO = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                newGO.name = newEarthName;
                if (parent != null) newGO.transform.SetParent(parent.transform, true);
                newGO.transform.position = oldWorldPos;
                newGO.transform.rotation = oldWorldRot;

                // force icosphere generation (SphereScript is ExecuteInEditMode, but
                // batch mode has no editor Update loop -> call via reflection)
                var sphereScript = newGO.GetComponentInChildren<DigitalRuby.Earth.SphereScript>(true);
                float baseDiameter = 40f;
                if (sphereScript != null)
                {
                    var t = typeof(DigitalRuby.Earth.SphereScript);
                    var gen = t.GetMethod("EditorGenerateMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (gen == null) gen = t.GetMethod("CreateIcoSphere", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (gen != null)
                    {
                        var mf = sphereScript.GetComponent<MeshFilter>();
                        var nm = (System.Reflection.MethodInfo)gen;
                        // try the most likely member set
                        foreach (var m in t.GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance))
                            Debug.Log("[EarthReplace] sphereScript method: " + m.Name);
                        nm.Invoke(sphereScript, null);
                        if (mf.sharedMesh != null) baseDiameter = mf.sharedMesh.bounds.size.y;
                        Debug.Log("[EarthReplace] generated mesh, bounds diameter=" + baseDiameter);
                    }
                    else
                    {
                        Debug.LogWarning("[EarthReplace] CreateIcoSphere not found; falling back to default diameter 40");
                    }
                }

                // scale so the world-space diameter matches the old Earth (apparent size preserved)
                var rend = sphereScript != null ? sphereScript.GetComponent<MeshRenderer>() : null;
                float currentWorldDiameter;
                if (rend != null && sphereScript.GetComponent<MeshFilter>().sharedMesh != null)
                {
                    currentWorldDiameter = rend.bounds.size.y; // world-space bounds
                }
                else
                {
                    var lossy = sphereScript != null ? sphereScript.transform.lossyScale.y : 1f;
                    currentWorldDiameter = baseDiameter * lossy;
                }
                float factor = oldWorldDiameter / currentWorldDiameter;
                var rootScale = newGO.transform.localScale;
                newGO.transform.localScale = new Vector3(rootScale.x * factor, rootScale.y * factor, rootScale.z * factor);
                Debug.Log(string.Format("[EarthReplace] scale factor={0} -> localScale={1} worldDiameter={2}",
                    factor, newGO.transform.localScale, oldWorldDiameter));

                // wire the new asset's own scripts: sun reference + preserved rotation
                var earthScript = newGO.GetComponentInChildren<DigitalRuby.Earth.EarthScript>(true);
                if (earthScript != null)
                {
                    Light sun = null;
                    var sunGO = FindByName(roots, "Sun_Lunar");
                    if (sunGO != null) sun = sunGO.GetComponent<Light>();
                    if (sun == null) sun = UnityEngine.Object.FindObjectsOfType<Light>(true)
                        .FirstOrDefault(l => l.type == LightType.Directional);
                    earthScript.Sun = sun;
                    earthScript.RotationSpeed = 0f; // original Earth did not rotate -> preserve orientation exactly
                    earthScript.Axis = newGO.transform.up;
                    Debug.Log("[EarthReplace] EarthScript wired; sun=" + (sun != null ? sun.name : "null") + " rotationSpeed=0");
                }

                // delete the old Earth so exactly ONE Earth remains
                if (oldEarth != null)
                {
                    UnityEngine.Object.DestroyImmediate(oldEarth);
                    Debug.Log("[EarthReplace] old Earth_Distant removed");
                }

                // shader sanity check for URP compatibility
                if (rend != null)
                {
                    var sh = rend.sharedMaterial != null ? rend.sharedMaterial.shader : null;
                    if (sh != null)
                    {
                        bool hasErr = UnityEditor.ShaderUtil.ShaderHasError(sh);
                        Debug.Log("[EarthReplace] shader=" + sh.name + " hasErrors=" + hasErr);
                        if (hasErr)
                            Debug.LogError("[EarthReplace] shader errors:\n" + UnityEditor.ShaderUtil.GetShaderMessages(sh));
                    }
                }

                // verify: exactly one Earth remains, parent/pos/rot correct
                var remaining = new[] { newGO }.Concat(
                    newGO.transform.parent != null
                        ? newGO.transform.parent.GetComponentsInChildren<Transform>(true)
                            .Where(t2 => t2.name == "Earth_Distant" && t2 != newGO.transform).Select(t2 => t2.gameObject)
                        : Enumerable.Empty<GameObject>()).ToList();
                Debug.Log("[EarthReplace] final Earth_Distant count=" + remaining.Count);
                Debug.Log(string.Format("[EarthReplace] new pos={0} rot={1} parent={2} lossyScale={3}",
                    newGO.transform.position, newGO.transform.rotation.eulerAngles, newGO.transform.parent.name, newGO.transform.lossyScale));
                if (rend != null)
                    Debug.Log(string.Format("[EarthReplace] final world bounds={0} center={1}", rend.bounds.size, rend.bounds.center));

                EditorSceneManager.SaveScene(scene);
                Debug.Log("[EarthReplace] SCENE_SAVED");
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("[EarthReplace] exception: " + e);
                EditorApplication.Exit(42);
            }
        }
    }
}
