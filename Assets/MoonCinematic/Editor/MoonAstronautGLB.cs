#if UNITY_EDITOR
using System;
using System.Linq;
using System.Text;
using MoonCinematic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoonCinematic.EditorTools
{
    /// <summary>Imports the downloaded rigged GLB astronaut via glTFast, verifies the
    /// import (materials, textures, clips, size), then replaces the placeholder
    /// astronaut in the existing Moon_Cinematic scene with a realistic unconscious
    /// lying pose — everything in place, no other scene objects touched.</summary>
    public static class MoonAstronautGLB
    {
        const string Root = "Assets/MoonCinematic";
        const string ScenePath = Root + "/Scenes/Moon_Cinematic.unity";
        const string GlbPath = "Assets/Astronaut_New/astronaut-in-spacesuit-rigged.glb";

        [MenuItem("Moon Cinematic/Inspect GLB Astronaut")]
        public static void InspectFromMenu() => BatchInspect();

        public static void BatchInspect()
        {
            int code = 0;
            try { Inspect(); }
            catch (Exception ex) { Debug.LogException(ex); code = 2; }
            EditorApplication.Exit(code);
        }

        [MenuItem("Moon Cinematic/Place GLB Astronaut")]
        public static void PlaceFromMenu() => BatchPlace();

        public static void BatchPlace()
        {
            int code = 0;
            try { Place(); }
            catch (Exception ex) { Debug.LogException(ex); code = 2; }
            EditorApplication.Exit(code);
        }

        [MenuItem("Moon Cinematic/Delete Old Placeholder Astronaut")]
        public static void DeleteOldFromMenu() => BatchDeleteOld();

        /// <summary>Removes the deactivated old placeholder astronaut once the GLB
        /// replacement has been visually verified. Only after that confirmation.</summary>
        public static void BatchDeleteOld()
        {
            int code = 0;
            try
            {
                Debug.Log("[MoonGLB] === DELETE OLD START ===");
                var scene = Open();
                var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Moon_Cinematic");
                if (root == null) { Debug.LogError("[MoonGLB] root missing"); code = 2; return; }
                var newAstro = root.transform.Find("Astronaut/Survivor_Astronaut_New");
                if (newAstro == null)
                {
                    Debug.LogError("[MoonGLB] new astronaut NOT found — refusing to delete the old one");
                    code = 2; return;
                }
                var oldAstro = root.transform.Find("Astronaut/Survivor_Astronaut");
                if (oldAstro != null)
                {
                    if (newAstro.gameObject.scene.IsValid() && oldAstro.GetComponent<Renderer>() != null)
                        Debug.Log("[MoonGLB] sanity ok: new astronaut has " + newAstro.GetComponentsInChildren<Renderer>(true).Length + " renderers");
                    UnityEngine.Object.DestroyImmediate(oldAstro.gameObject);
                    Debug.Log("[MoonGLB] old placeholder astronaut deleted (new one keeps its position)");
                }
                else
                {
                    Debug.Log("[MoonGLB] old astronaut already absent");
                }
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log("[MoonGLB] === DELETE OLD DONE ===");
            }
            catch (Exception ex) { Debug.LogException(ex); code = 2; }
            EditorApplication.Exit(code);
        }

        // helper used by both
        static UnityEngine.SceneManagement.Scene Open()
        {
            return EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        // ============================================================ inspect

        static void Inspect()
        {
            Debug.Log("[MoonGLB] === INSPECT START ===");
            AssetDatabase.Refresh();
            if (!System.IO.File.Exists(GlbPath)) { Debug.LogError("[MoonGLB] glb missing: " + GlbPath); return; }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath) == null)
            {
                Debug.Log("[MoonGLB] no imported GameObject yet — forcing import");
                AssetDatabase.ImportAsset(GlbPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh();
            }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath);
            if (model == null) { Debug.LogError("[MoonGLB] import did NOT yield a GameObject — glTFast importer still disabled"); return; }
            Debug.Log("[MoonGLB] main asset: " + model.name);

            // hierarchy + transforms
            var kids = model.GetComponentsInChildren<Transform>(true);
            var sb = new StringBuilder();
            foreach (var t in kids)
            {
                int depth = 0; var p = t; while (p.parent != null) { depth++; p = p.parent; }
                sb.AppendLine(new string(' ', depth * 2) + t.name
                    + string.Format(" [P={0}, R={1}, S={2}]", t.localPosition, t.localEulerAngles, t.localScale));
            }
            Debug.Log("[MoonGLB] hierarchy:\n" + sb);

            // skinned mesh info
            var smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var smr in smrs)
            {
                var b = smr.sharedMesh.bounds;
                Debug.Log(string.Format("[MoonGLB] skinned mesh '{0}': bones={1} rootBone={2} bounds.size={3} bounds.min={4} vertexCount={5}",
                    smr.name, smr.bones.Length, smr.rootBone != null ? smr.rootBone.name : "(null)", b.size, b.min, smr.sharedMesh.vertexCount));
            }
            var meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
            Debug.Log("[MoonGLB] static MeshFilters: " + meshFilters.Length);

            // materials & textures
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                Debug.Log("[MoonGLB] renderer '" + r.name + "' mats=" + r.sharedMaterials.Length);
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) { Debug.LogWarning("[MoonGLB] renderer '" + r.name + "': sharedMaterial NULL"); continue; }
                    var shader = m.shader;
                    var sb2 = new StringBuilder();
                    var props = new[] { "_BaseMap", "_MainTex", "_BumpMap", "_NormalMap", "_MetallicGlossMap", "_Metallic", "_Smoothness", "_Color", "_BaseColor" };
                    foreach (var p in props)
                    {
                        if (m.HasProperty(p))
                        {
                            var tex = m.GetTexture(p);
                            sb2.Append(p).Append("=").Append(tex != null ? tex.name + " (" + tex.width + "x" + tex.height + ")" : "none").Append("  ");
                        }
                    }
                    Debug.Log(string.Format("[MoonGLB] material '{0}' shader='{1}'\n    {2}", m.name, shader.name, sb2));
                }
            }

            // animation clips
            var anims = model.GetComponentsInChildren<Animator>(true);
            foreach (var a in anims)
                Debug.Log("[MoonGLB] Animator on '" + a.name + "' controller=" + (a.runtimeAnimatorController != null ? a.runtimeAnimatorController.name : "(none)"));
            foreach (var guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { "Assets/Astronaut_New" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip != null) Debug.Log("[MoonGLB] imported AnimationClip: " + path + " len=" + clip.length.ToString("F2") + "s legacy=" + clip.legacy);
            }

            // world size via temp instantiation
            var tmp = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try { }
            finally { }
            var renderers2 = tmp.GetComponentsInChildren<Renderer>(true);
            Bounds world = new Bounds();
            bool first = true;
            foreach (var r in renderers2)
            {
                if (first) { world = r.bounds; first = false; }
                else world.Encapsulate(r.bounds);
            }
            Debug.Log(string.Format("[MoonGLB] instantiated world bounds: min={0} max={1} size={2}",
                world.min, world.max, world.size));
            UnityEngine.Object.DestroyImmediate(tmp);
            Debug.Log("[MoonGLB] === INSPECT DONE ===");
        }

        // ============================================================ place

        static void Place()
        {
            Debug.Log("[MoonGLB] === PLACE START ===");
            var scene = Open();
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Moon_Cinematic");
            if (root == null) { Debug.LogError("[MoonGLB] root missing"); return; }
            var terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            var tp = terrain.transform.position;
            var sz = terrain.terrainData.size;
            Func<float, float, float> gY = (x, z) => terrain.terrainData.GetInterpolatedHeight((x - tp.x) / sz.x, (z - tp.z) / sz.z);

            var oldAstroTr = root.transform.Find("Astronaut/Survivor_Astronaut");
            if (oldAstroTr == null) { Debug.LogError("[MoonGLB] old astronaut missing"); return; }

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(GlbPath);
            if (model == null) { Debug.LogError("[MoonGLB] glb import missing"); return; }

            // old astronaut stays in the scene but off (new one is verified first)
            var oldAstro = oldAstroTr.gameObject;
            if (oldAstro.activeSelf)
            {
                oldAstro.SetActive(false);
                Debug.Log("[MoonGLB] old astronaut deactivated (not deleted): " + oldAstroTr.childCount + " children kept");
            }

            // remove a previous replacement attempt
            var existingNew = oldAstroTr.parent.Find("Survivor_Astronaut_New");
            if (existingNew != null) UnityEngine.Object.DestroyImmediate(existingNew.gameObject);

            // wrap the model under a controller node placed exactly where the old one was
            var wrapper = new GameObject("Survivor_Astronaut_New");
            wrapper.transform.SetParent(oldAstroTr.parent, false);
            var oldPos = oldAstroTr.position;
            float ground = gY(oldPos.x, oldPos.z);
            wrapper.transform.position = new Vector3(oldPos.x, ground, oldPos.z);
            // head toward the wreck: the model lies on its back, head pointing to local -Z,
            // so pick body yaw so that -Z ends up at world direction (0.469, 0, 0.883)
            wrapper.transform.rotation = Quaternion.Euler(0f, 208f, 0f);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            inst.name = "Astronaut_Model";
            inst.transform.SetParent(wrapper.transform, false);

            // lay the standing figure on its back: -90 deg about local X
            // (+Y up -> head toward -Z, facing +Z -> +Y face up)
            var armature = inst.transform;

            // normalize size: instantiate first at identity, measure height, scale to 1.8 m,
            // then apply the lying rotation on the armature node itself
            var renderers = inst.GetComponentsInChildren<Renderer>(true);
            Bounds b = new Bounds();
            bool first = true;
            foreach (var r in renderers)
            {
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            float height = b.size.y;
            if (first) { Debug.LogError("[MoonGLB] model has no renderers"); return; }
            float targetH = 1.78f;
            float norm = targetH / Mathf.Max(0.0001f, height);
            Debug.Log(string.Format("[MoonGLB] measured world size={0} height={1:F3} -> normalize factor {2:F3}", b.size, height, norm));

            // apply: armature local rotation to lie supine, then normalize scale on the wrapper level
            armature.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            wrapper.transform.localScale = Vector3.one * norm;
            Debug.Log("[MoonGLB] hierarchy after lay-flat: " + armature.name + " / child count " + armature.childCount);

            // prevent the embedded run/walk clips from overriding the pose
            foreach (var animator in inst.GetComponentsInChildren<Animator>(true))
            {
                animator.runtimeAnimatorController = null;
                animator.enabled = false;
                Debug.Log("[MoonGLB] animator disabled on " + animator.name);
            }
            foreach (var animation in inst.GetComponentsInChildren<Animation>(true))
            {
                animation.enabled = false;
                Debug.Log("[MoonGLB] legacy Animation disabled on " + animation.name);
            }

            // relaxed fallen pose: gentle limb rotations (local to each bone),
            // head tilt and slightly bent limbs — supine, arms out
            PoseRelaxed(inst.transform);

            // so the back rests on the surface: measure standing->lying height (thickness)
            Bounds b2 = new Bounds();
            first = true;
            foreach (var r in renderers)
            {
                if (first) { b2 = r.bounds; first = false; }
                else b2.Encapsulate(r.bounds);
            }
            Debug.Log(string.Format("[MoonGLB] lying bounds: min={0} max={1} size={2}", b2.min, b2.max, b2.size));
            // wrapper sits at ground level; lower/raise model so bottom of the suit touches ground
            float bottomOffset = b2.min.y - wrapper.transform.position.y;
            var ap2 = inst.transform.localPosition;
            ap2.y -= bottomOffset;
            inst.transform.localPosition = ap2;

            // fresh measure + final log
            Bounds b3 = new Bounds();
            first = true;
            foreach (var r in renderers)
            {
                if (first) { b3 = r.bounds; first = false; }
                else b3.Encapsulate(r.bounds);
            }
            Debug.Log(string.Format("[MoonGLB] final world bounds: min={0} max={1} size={2} (ground={3:F2})",
                b3.min, b3.max, b3.size, ground));

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MoonGLB] === PLACE DONE ===");
        }

        /// <summary>Gentle unconscious pose on top of the lay-flat: slightly open
        /// arms/legs, tilted head. Bone lookups by name on the glTF skeleton.</summary>
        static void PoseRelaxed(Transform modelRoot)
        {
            Transform Find(string n)
            {
                foreach (var t in modelRoot.GetComponentsInChildren<Transform>(true))
                    if (t.name == n) return t;
                return null;
            }
            void IfPose(string name, Quaternion q, string tag)
            {
                var t = Find(name);
                if (t == null) { Debug.LogWarning("[MoonGLB] bone not found: " + name); return; }
                t.localRotation = t.localRotation * q;
            }

            // glTF humanoid bone rotations (about each bone's local axes):
            // arms slightly spread away from the body, elbows a bit bent
            IfPose("LeftArm", Quaternion.Euler(0, 25, -10), "armL");
            IfPose("LeftForeArm", Quaternion.Euler(0, 0, -25), "foreL");
            IfPose("LeftHand", Quaternion.Euler(0, 0, -10), "handL");
            IfPose("RightArm", Quaternion.Euler(0, -25, 10), "armR");
            IfPose("RightForeArm", Quaternion.Euler(0, 0, 25), "foreR");
            IfPose("RightHand", Quaternion.Euler(0, 0, 10), "handR");
            // legs slightly bent apart
            IfPose("LeftUpLeg", Quaternion.Euler(0, 0, -8), "legL");
            IfPose("LeftLeg", Quaternion.Euler(0, 0, -12), "shinL");
            IfPose("RightUpLeg", Quaternion.Euler(0, 0, 8), "legR");
            IfPose("RightLeg", Quaternion.Euler(0, 0, 12), "shinR");
            // head tilted to the side
            IfPose("Head", Quaternion.Euler(6, 0, 18), "head");
            Debug.Log("[MoonGLB] relaxed pose applied (missing bones logged, if any)");
        }
    }
}
#endif
