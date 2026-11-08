#if UNITY_EDITOR
using System;
using System.Linq;
using MoonCinematic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MoonCinematic.EditorTools
{
    /// <summary>
    /// Second polish pass (still in place, no rebuild):
    ///  1. astronaut v2 — bulky suit silhouette, big mirrored gold visor, spherical
    ///     joints, no limb crossings (replaces the v1 children on the same object,
    ///     same position/heading);
    ///  2. hull materials — the main hull pieces become silver-white spacecraft
    ///     plating with an orange marking band (scorched debris stays charred);
    ///  3. rest-state sanity for all cinematic cameras.
    /// </summary>
    public static class MoonCinematicUpgrade2
    {
        const string Root = "Assets/MoonCinematic";
        const string ScenePath = Root + "/Scenes/Moon_Cinematic.unity";
        const string DirMaterials = Root + "/Materials";

        [MenuItem("Moon Cinematic/Polish Astronaut & Hull (pass 2)")]
        public static void FromMenu() => Batch();

        public static void Batch()
        {
            int code = 0;
            try { Run(); }
            catch (Exception ex) { Debug.LogException(ex); code = 2; }
            EditorApplication.Exit(code);
        }

        static void Run()
        {
            Debug.Log("[MoonUpg2] === PASS 2 START ===");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Moon_Cinematic");
            if (root == null) { Debug.LogError("[MoonUpg2] root missing"); return; }
            var terrain = UnityEngine.Object.FindObjectOfType<Terrain>();
            Func<float, float, float> gY = (x, z) =>
            {
                var tp = terrain.transform.position;
                var sz = terrain.terrainData.size;
                return terrain.terrainData.GetInterpolatedHeight((x - tp.x) / sz.x, (z - tp.z) / sz.z);
            };

            RebuildAstronautV2(root, gY);
            RecolorHull(root);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MoonUpg2] === PASS 2 DONE ===");
        }

        static Material NewMat(string path, Color col, float smooth, float metal)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Legacy Shaders/Standard");
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.color = col;
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smooth);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metal);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void RebuildAstronautV2(GameObject root, Func<float, float, float> gY)
        {
            var astroRootTr = root.transform.Find("Astronaut/Survivor_Astronaut");
            if (astroRootTr == null) { Debug.LogError("[MoonUpg2] astronaut root missing"); return; }

            var matSuit = NewMat(DirMaterials + "/Mat_Suit_WhiteUpgrade.mat", new Color(0.95f, 0.94f, 0.91f), 0.32f, 0.0f);
            var matJoint = NewMat(DirMaterials + "/Mat_Suit_JointUpgrade.mat", new Color(0.38f, 0.38f, 0.40f), 0.45f, 0.1f);
            var matAccent = NewMat(DirMaterials + "/Mat_Suit_AccentUpgrade.mat", new Color(0.78f, 0.36f, 0.10f), 0.4f, 0.0f);
            var matVisorGold = NewMat(DirMaterials + "/Mat_Visor_GoldUpgrade.mat", new Color(0.85f, 0.60f, 0.18f), 1.0f, 0.85f);
            var matBoot = NewMat(DirMaterials + "/Mat_Boot_Upgrade.mat", new Color(0.12f, 0.12f, 0.13f), 0.35f, 0.05f);

            int oldChildren = astroRootTr.childCount;
            for (int i = oldChildren - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(astroRootTr.GetChild(i).gameObject);

            var ap = astroRootTr.position;
            float ground = gY(ap.x, ap.z);
            ap.y = ground + 0.02f;
            astroRootTr.position = ap;
            astroRootTr.rotation = Quaternion.Euler(0f, 118f, 0f); // head toward wreckage

            // local space: +X = feet, +Y = up, supine (chest faces up), helmet on -X
            Transform Part(string name, PrimitiveType t, Vector3 pos, Vector3 rot, Vector3 scale, Material mat)
            {
                var go = GameObject.CreatePrimitive(t);
                go.name = name;
                go.transform.SetParent(astroRootTr, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(rot);
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = mat;
                return go.transform;
            }
            // capsule lying with its long axis along local X
            Transform Caps(string name, Vector3 pos, Vector3 scale, Material mat)
                => Part(name, PrimitiveType.Capsule, pos, new Vector3(0f, 0f, 90f), scale, mat);

            // ---- torso volume (bulky unit capsule, chest up) ----
            Caps("Suit_Torso", new Vector3(0f, 0.17f, 0f), new Vector3(0.22f, 0.27f, 0.22f), matSuit);
            Part("Suit_Hips", PrimitiveType.Sphere, new Vector3(0.26f, 0.15f, 0.02f), Vector3.zero, new Vector3(0.21f, 0.15f, 0.21f), matSuit);
            Part("Suit_Soulder_R", PrimitiveType.Sphere, new Vector3(-0.17f, 0.21f, 0.155f), Vector3.zero, new Vector3(0.12f, 0.12f, 0.12f), matJoint);
            Part("Suit_Shoulder_L", PrimitiveType.Sphere, new Vector3(-0.17f, 0.21f, -0.155f), Vector3.zero, new Vector3(0.12f, 0.12f, 0.12f), matJoint);

            // ---- helmet: big bubble + wide gold visor dome facing up-front ----
            Part("Suit_Helmet", PrimitiveType.Sphere, new Vector3(-0.335f, 0.22f, 0f), Vector3.zero, new Vector3(0.17f, 0.17f, 0.17f), matSuit);
            Part("Suit_Visor", PrimitiveType.Sphere, new Vector3(-0.405f, 0.265f, 0.012f), new Vector3(-38f, 0f, 0f), new Vector3(0.155f, 0.085f, 0.16f), matVisorGold);
            Part("Suit_NeckRing", PrimitiveType.Cylinder, new Vector3(-0.24f, 0.21f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.085f, 0.05f, 0.085f), matJoint);
            Part("Suit_CommPod_R", PrimitiveType.Cylinder, new Vector3(-0.33f, 0.235f, 0.175f), new Vector3(90f, 0f, 0f), new Vector3(0.05f, 0.022f, 0.05f), matJoint);
            Part("Suit_CommPod_L", PrimitiveType.Cylinder, new Vector3(-0.33f, 0.235f, -0.175f), new Vector3(90f, 0f, 0f), new Vector3(0.05f, 0.022f, 0.05f), matJoint);

            // ---- chest console + O2 lines (all facing up) ----
            Part("Suit_ChestConsole", PrimitiveType.Cube, new Vector3(-0.02f, 0.315f, 0f), new Vector3(0f, -4f, 0f), new Vector3(0.26f, 0.05f, 0.20f), matAccent);
            Part("Suit_ConsoleKnob_R", PrimitiveType.Sphere, new Vector3(-0.10f, 0.35f, 0.045f), Vector3.zero, new Vector3(0.05f, 0.05f, 0.05f), matJoint);
            Part("Suit_ConsoleKnob_L", PrimitiveType.Sphere, new Vector3(-0.10f, 0.35f, -0.045f), Vector3.zero, new Vector3(0.05f, 0.05f, 0.05f), matJoint);
            Caps("Suit_Tube_R", new Vector3(0f, 0.235f, 0.145f), new Vector3(0.05f, 0.20f, 0.05f), matJoint);
            Caps("Suit_Tube_L", new Vector3(0f, 0.235f, -0.145f), new Vector3(0.05f, 0.20f, 0.05f), matJoint);

            // ---- PLSS flank (visible along the -Z side) + antenna ----
            Part("Suit_PLSS_Flank", PrimitiveType.Cube, new Vector3(0.02f, 0.10f, -0.17f), new Vector3(0f, 3f, 0f), new Vector3(0.36f, 0.18f, 0.15f), matJoint);
            Part("Suit_BackpackAntenna", PrimitiveType.Cylinder, new Vector3(0.06f, 0.34f, -0.18f), new Vector3(-14f, 0f, 12f), new Vector3(0.016f, 0.28f, 0.016f), matJoint);

            // ---- arms straight out to the sides, slight elbow bend, no crossings ----
            Part("Arm_R_Upper", PrimitiveType.Capsule, new Vector3(-0.17f, 0.17f, 0.245f), new Vector3(90f, 0f, 0f), new Vector3(0.095f, 0.15f, 0.095f), matSuit);
            Part("Arm_R_Elbow", PrimitiveType.Sphere, new Vector3(-0.17f, 0.16f, 0.34f), Vector3.zero, new Vector3(0.105f, 0.105f, 0.105f), matJoint);
            Part("Arm_R_Fore", PrimitiveType.Capsule, new Vector3(-0.155f, 0.14f, 0.44f), new Vector3(90f, 0f, 0f), new Vector3(0.085f, 0.13f, 0.085f), matSuit);
            Part("Glove_R", PrimitiveType.Sphere, new Vector3(-0.15f, 0.145f, 0.555f), Vector3.zero, new Vector3(0.11f, 0.11f, 0.11f), matJoint);

            Part("Arm_L_Upper", PrimitiveType.Capsule, new Vector3(-0.17f, 0.17f, -0.245f), new Vector3(90f, 0f, 0f), new Vector3(0.095f, 0.15f, 0.095f), matSuit);
            Part("Arm_L_Elbow", PrimitiveType.Sphere, new Vector3(-0.17f, 0.16f, -0.34f), Vector3.zero, new Vector3(0.105f, 0.105f, 0.105f), matJoint);
            Part("Arm_L_Fore", PrimitiveType.Capsule, new Vector3(-0.155f, 0.14f, -0.44f), new Vector3(90f, 0f, 0f), new Vector3(0.085f, 0.13f, 0.085f), matSuit);
            Part("Glove_L", PrimitiveType.Sphere, new Vector3(-0.15f, 0.145f, -0.555f), Vector3.zero, new Vector3(0.11f, 0.11f, 0.11f), matJoint);

            // ---- legs: left straight, right slightly raised ----
            Caps("Leg_R_Thigh", new Vector3(0.48f, 0.15f, 0.055f), new Vector3(0.125f, 0.23f, 0.125f), matSuit);
            Part("Suit_Knee_R", PrimitiveType.Sphere, new Vector3(0.71f, 0.185f, 0.07f), Vector3.zero, new Vector3(0.09f, 0.09f, 0.09f), matJoint);
            Caps("Leg_R_Shin", new Vector3(0.90f, 0.155f, 0.095f), new Vector3(0.105f, 0.20f, 0.105f), matSuit);
            Part("Leg_R_Boot", PrimitiveType.Cube, new Vector3(1.10f, 0.09f, 0.10f), new Vector3(0f, -8f, 0f), new Vector3(0.14f, 0.085f, 0.10f), matBoot);

            Caps("Leg_L_Thigh", new Vector3(0.47f, 0.15f, -0.055f), new Vector3(0.125f, 0.23f, 0.125f), matSuit);
            Part("Suit_Knee_L", PrimitiveType.Sphere, new Vector3(0.70f, 0.16f, -0.06f), Vector3.zero, new Vector3(0.085f, 0.085f, 0.085f), matJoint);
            Caps("Leg_L_Shin", new Vector3(0.90f, 0.15f, -0.075f), new Vector3(0.105f, 0.20f, 0.105f), matSuit);
            Part("Leg_L_Boot", PrimitiveType.Cube, new Vector3(1.10f, 0.09f, -0.095f), new Vector3(0f, 4f, 0f), new Vector3(0.14f, 0.085f, 0.10f), matBoot);

            Debug.Log("[MoonUpg2] astronaut v2 rebuilt: removed=" + oldChildren + " parts=" + astroRootTr.childCount
                + " pos=" + astroRootTr.position + " (bulky suit, gold visor, no limb crossings)");
        }

        static void RecolorHull(GameObject root)
        {
            var silver = NewMat(DirMaterials + "/Mat_Wreck_HullSilver.mat",
                new Color(0.60f, 0.62f, 0.64f), 0.42f, 0.78f);
            var band = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_Suit_Accent.mat");

            var wreck = root.transform.Find("CrashSite/Wreckage_Spacecraft");
            if (wreck == null) { Debug.LogError("[MoonUpg2] wreckage root missing"); return; }

            string[] silverParts = {
                "Wreck_Hull_Main", "Wreck_Hull_Tail", "Wreck_Hull_Nose",
                "Wreck_Engine_Housing", "Wreck_Hull_Rib_1", "Wreck_Hull_Rib_2",
                "Wreck_Hull_Rib_3", "Wreck_Cockpit_Shell", "Wreck_SolarWing_A", "Wreck_SolarWing_B",
                "Wreck_Leg_Bent", "Wreck_Leg_Detached"
            };
            int painted = 0;
            foreach (var name in silverParts)
            {
                var t = wreck.Find(name);
                if (t == null) { Debug.LogWarning("[MoonUpg2] hull part not found (skip): " + name); continue; }
                t.GetComponent<Renderer>().sharedMaterial = silver;
                painted++;
            }

            // orange marking band around the main hull -> instantly "vehicle hull";
            // the hull sits partly buried, so the band reads as a torn hull ring
            var hull = wreck.Find("Wreck_Hull_Main");
            if (hull != null)
            {
                var existing = wreck.Find("Wreck_Hull_MarkingBand");
                if (existing == null)
                {
                    Vector3 hp = hull.position;
                    Vector3 axis = hull.forward;
                    var bandGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    bandGo.name = "Wreck_Hull_MarkingBand";
                    bandGo.transform.SetParent(wreck, false);
                    Vector3 bp = hp + axis * (-0.35f);
                    bandGo.transform.position = bp;
                    bandGo.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis);
                    bandGo.transform.localScale = new Vector3(2.05f, 0.05f, 2.05f);
                    bandGo.GetComponent<Renderer>().sharedMaterial = band;
                    bandGo.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
                    bandGo.isStatic = true;
                    Debug.Log("[MoonUpg2] marking band added at " + bandGo.transform.position);
                }
            }

            Debug.Log("[MoonUpg2] hull silver recoat: " + painted + " parts");
        }
    }
}
#endif
