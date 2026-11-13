#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using MoonCinematic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Rendering; // shadow enums
using UnityEngine.Timeline;
using UnityEngine.UI;

namespace MoonCinematic.EditorTools
{
    /// <summary>
    /// In-place upgrade of the existing Moon_Cinematic scene (no rebuild):
    ///  1. replaces the primitive placeholder astronaut with a detailed, clearly
    ///     readable spacesuit figure (helmet + gold visor, PLSS, gloves, boots),
    ///     still unconscious by the crash;
    ///  2. adds clearly recognizable spacecraft wreckage around the existing
    ///     hull pieces (ribbed fuselage struts, snapped landing legs, torn
    ///     solar wing, cockpit shell, fuel lines, skid gouges);
    ///  3. re-frames Camera_06 FinalWide so astronaut + wreck share the final
    ///     hold shot and re-bakes its drift clip;
    ///  4. builds the cinematic END CARD ("THE LUNAR") and wires it into
    ///     the CinematicSequenceController.
    /// </summary>
    public static class MoonCinematicUpgrade
    {
        const string Root = "Assets/MoonCinematic";
        const string ScenePath = Root + "/Scenes/Moon_Cinematic.unity";
        const string DirMaterials = Root + "/Materials";
        const string DirMeshes = Root + "/Meshes";
        const string DirTimeline = Root + "/Timeline";

        [MenuItem("Moon Cinematic/Upgrade Astronaut, Wreckage & Ending")]
        public static void UpgradeFromMenu() => BatchUpgrade();

        public static void BatchUpgrade()
        {
            int code = 0;
            try { UpgradeScene(); }
            catch (Exception ex) { Debug.LogException(ex); code = 2; }
            EditorApplication.Exit(code);
        }

        static void UpgradeScene()
        {
            Debug.Log("[MoonUpg] === UPGRADE START ===");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Moon_Cinematic");
            if (root == null) { Debug.LogError("[MoonUpg] root missing"); return; }
            var terrain = UnityEngine.Object.FindObjectOfType<Terrain>();

            Func<float, float, float> gY = (x, z) =>
            {
                var tp = terrain.transform.position;
                var sz = terrain.terrainData.size;
                return terrain.terrainData.GetInterpolatedHeight((x - tp.x) / sz.x, (z - tp.z) / sz.z);
            };

            UpgradeAstronaut(root, gY);
            UpgradeWreckage(root, gY);
            ReframeFinalShot(root, terrain, gY);
            BuildEndCard(root);

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MoonUpg] === UPGRADE DONE ===");
        }

        // ============================================================ astronaut

        /// <summary>Load or create a URP Lit material with the given look.</summary>
        static Material NewMat(string path, Color col, float smooth, float metal)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Legacy Shaders/Standard");
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

        static void UpgradeAstronaut(GameObject root, Func<float, float, float> gY)
        {
            var astroRootTr = root.transform.Find("Astronaut/Survivor_Astronaut");
            if (astroRootTr == null) { Debug.LogError("[MoonUpg] astronaut root missing"); return; }
            var astroRoot = astroRootTr.gameObject;

            var matSuit = NewMat(DirMaterials + "/Mat_Suit_WhiteUpgrade.mat",
                new Color(0.95f, 0.94f, 0.91f), 0.32f, 0.0f);
            var matJoint = NewMat(DirMaterials + "/Mat_Suit_JointUpgrade.mat",
                new Color(0.38f, 0.38f, 0.40f), 0.45f, 0.1f);
            var matAccent = NewMat(DirMaterials + "/Mat_Suit_AccentUpgrade.mat",
                new Color(0.78f, 0.36f, 0.10f), 0.4f, 0.0f);
            var matVisorGold = NewMat(DirMaterials + "/Mat_Visor_GoldUpgrade.mat",
                new Color(0.85f, 0.60f, 0.18f), 1.0f, 0.85f);
            var matBoot = NewMat(DirMaterials + "/Mat_Boot_Upgrade.mat",
                new Color(0.12f, 0.12f, 0.13f), 0.35f, 0.05f);

            // replace contents in place (same GameObject, same name/position, cameras keep aiming)
            int oldChildren = astroRootTr.childCount;
            for (int i = oldChildren - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(astroRootTr.GetChild(i).gameObject);

            // keep the same ground anchor the scene had (22.60, 20.45, 7.20)
            var ap = astroRootTr.position;
            float ground = gY(ap.x, ap.z);
            ap.y = ground + 0.02f;
            astroRootTr.position = ap;
            astroRootTr.rotation = Quaternion.Euler(0f, 118f, 0f); // head toward wreckage

            // local space: +X = feet direction, +Y = up, supine (chest faces up)
            Transform Part(string name, PrimitiveType t, Vector3 pos, Vector3 rot, Vector3 scale, Material mat)
            {
                var go = GameObject.CreatePrimitive(t);
                go.name = name;
                go.transform.SetParent(astroRoot.transform, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(rot);
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = mat;
                return go.transform;
            }

            // ---- torso & helmet (supine; chest & helmet top face +Y) ----
            Part("Suit_Torso", PrimitiveType.Capsule, new Vector3(-0.02f, 0.15f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.185f, 0.24f, 0.185f), matSuit);
            Part("Suit_Hips", PrimitiveType.Sphere, new Vector3(0.28f, 0.13f, 0.02f), Vector3.zero, new Vector3(0.20f, 0.15f, 0.20f), matSuit);
            Part("Suit_NeckRing", PrimitiveType.Cylinder, new Vector3(-0.225f, 0.17f, 0f), new Vector3(0f, 0f, 90f), new Vector3(0.075f, 0.05f, 0.075f), matJoint);
            Part("Suit_Helmet", PrimitiveType.Sphere, new Vector3(-0.335f, 0.18f, 0f), Vector3.zero, new Vector3(0.135f, 0.135f, 0.135f), matSuit);
            Part("Suit_Visor", PrimitiveType.Sphere, new Vector3(-0.385f, 0.235f, 0.02f), new Vector3(-35f, 0f, 90f), new Vector3(0.105f, 0.075f, 0.10f), matVisorGold);
            Part("Suit_CommPod_R", PrimitiveType.Cylinder, new Vector3(-0.33f, 0.18f, 0.095f), new Vector3(0f, 0f, 90f), new Vector3(0.055f, 0.02f, 0.055f), matJoint);
            Part("Suit_CommPod_L", PrimitiveType.Cylinder, new Vector3(-0.33f, 0.18f, -0.095f), new Vector3(0f, 0f, 90f), new Vector3(0.055f, 0.02f, 0.055f), matJoint);

            // ---- chest console & gear ----
            Part("Suit_ChestConsole", PrimitiveType.Cube, new Vector3(-0.03f, 0.275f, 0.0f), new Vector3(0f, -4f, 0f), new Vector3(0.24f, 0.04f, 0.185f), matAccent);
            Part("Suit_ConsoleKnob_R", PrimitiveType.Sphere, new Vector3(-0.09f, 0.30f, 0.045f), Vector3.zero, new Vector3(0.05f, 0.05f, 0.05f), matJoint);
            Part("Suit_ConsoleKnob_L", PrimitiveType.Sphere, new Vector3(-0.09f, 0.30f, -0.045f), Vector3.zero, new Vector3(0.05f, 0.05f, 0.05f), matJoint);
            Part("Suit_Backpack", PrimitiveType.Cube, new Vector3(-0.05f, 0.07f, -0.03f), new Vector3(0f, 3f, 0f), new Vector3(0.34f, 0.15f, 0.31f), matJoint);
            Part("Suit_BackpackAntenna", PrimitiveType.Cylinder, new Vector3(0.05f, 0.30f, -0.12f), new Vector3(-12f, 0f, 14f), new Vector3(0.015f, 0.26f, 0.015f), matJoint);

            // ---- right arm: flung out above the head, limp (clearly readable) ----
            Part("Arm_R_Upper", PrimitiveType.Capsule, new Vector3(-0.14f, 0.13f, 0.20f), new Vector3(90f, 0f, 0f), new Vector3(0.075f, 0.16f, 0.075f), matSuit);
            Part("Arm_R_Elbow", PrimitiveType.Sphere, new Vector3(-0.14f, 0.12f, 0.31f), Vector3.zero, new Vector3(0.06f, 0.06f, 0.06f), matJoint);
            Part("Arm_R_Fore", PrimitiveType.Capsule, new Vector3(-0.28f, 0.125f, 0.33f), new Vector3(0f, 0f, 96f), new Vector3(0.065f, 0.15f, 0.065f), matSuit);
            Part("Glove_R", PrimitiveType.Sphere, new Vector3(-0.425f, 0.125f, 0.345f), Vector3.zero, new Vector3(0.095f, 0.095f, 0.095f), matJoint);

            // ---- left arm: bent across the chest ----
            Part("Arm_L_Upper", PrimitiveType.Capsule, new Vector3(-0.145f, 0.135f, -0.195f), new Vector3(90f, 0f, 0f), new Vector3(0.075f, 0.15f, 0.075f), matSuit);
            Part("Arm_L_Elbow", PrimitiveType.Sphere, new Vector3(-0.155f, 0.10f, -0.285f), Vector3.zero, new Vector3(0.06f, 0.06f, 0.06f), matJoint);
            Part("Arm_L_Fore", PrimitiveType.Capsule, new Vector3(-0.105f, 0.255f, -0.165f), new Vector3(90f, 0f, 0f), new Vector3(0.065f, 0.14f, 0.065f), matSuit);
            Part("Glove_L", PrimitiveType.Sphere, new Vector3(-0.09f, 0.26f, -0.02f), Vector3.zero, new Vector3(0.09f, 0.09f, 0.09f), matJoint);

            // ---- right leg: knee raised (fallen mid-step) ----
            Part("Leg_R_Thigh", PrimitiveType.Capsule, new Vector3(0.52f, 0.135f, 0.06f), new Vector3(0f, 0f, 90f), new Vector3(0.125f, 0.23f, 0.125f), matSuit);
            Part("Suit_Knee_R", PrimitiveType.Sphere, new Vector3(0.72f, 0.235f, 0.10f), Vector3.zero, new Vector3(0.075f, 0.075f, 0.075f), matJoint);
            Part("Leg_R_Shin", PrimitiveType.Capsule, new Vector3(0.87f, 0.15f, 0.125f), new Vector3(0f, -6f, -31f), new Vector3(0.105f, 0.17f, 0.105f), matSuit);
            Part("Leg_R_Boot", PrimitiveType.Cube, new Vector3(1.02f, 0.085f, 0.15f), new Vector3(0f, -10f, 0f), new Vector3(0.13f, 0.075f, 0.09f), matBoot);

            // ---- left leg: extended straight ----
            Part("Leg_L_Thigh", PrimitiveType.Capsule, new Vector3(0.50f, 0.125f, -0.06f), new Vector3(0f, 0f, 90f), new Vector3(0.125f, 0.23f, 0.125f), matSuit);
            Part("Suit_Knee_L", PrimitiveType.Sphere, new Vector3(0.73f, 0.135f, -0.06f), Vector3.zero, new Vector3(0.07f, 0.07f, 0.07f), matJoint);
            Part("Leg_L_Shin", PrimitiveType.Capsule, new Vector3(0.95f, 0.125f, -0.07f), new Vector3(0f, 0f, 90f), new Vector3(0.105f, 0.21f, 0.105f), matSuit);
            Part("Leg_L_Boot", PrimitiveType.Cube, new Vector3(1.17f, 0.075f, -0.08f), new Vector3(0f, 6f, 0f), new Vector3(0.135f, 0.075f, 0.09f), matBoot);

            // suit accent: O2 tubes running along the torso side
            Part("Suit_Tube_R", PrimitiveType.Cylinder, new Vector3(0.0f, 0.20f, 0.125f), new Vector3(0f, 0f, 90f), new Vector3(0.05f, 0.30f, 0.05f), matJoint);
            Part("Suit_Tube_L", PrimitiveType.Cylinder, new Vector3(0.0f, 0.20f, -0.125f), new Vector3(0f, 0f, 90f), new Vector3(0.05f, 0.28f, 0.05f), matJoint);

            int rebuilt = astroRootTr.childCount;
            Debug.Log("[MoonUpg] astronaut rebuilt: old children removed=" + oldChildren + " new parts=" + rebuilt
                + " pos=" + astroRootTr.position + " ground=" + ground.ToString("F2"));
        }

        // ============================================================ wreckage

        static void UpgradeWreckage(GameObject root, Func<float, float, float> gY)
        {
            var wreckRootTr = root.transform.Find("CrashSite/Wreckage_Spacecraft");
            if (wreckRootTr == null) { Debug.LogError("[MoonUpg] wreckage root missing"); return; }

            var matCharred = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_Wreck_Charred.mat");
            var matMetal = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_Wreck_Metal.mat");
            var matDebris = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_Wreck_Debris.mat");
            var matScorch = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_ScorchDecal.mat");

            var hullMain = wreckRootTr.Find("Wreck_Hull_Main");
            var hullTail = wreckRootTr.Find("Wreck_Hull_Tail");
            var hullNose = wreckRootTr.Find("Wreck_Hull_Nose");
            Vector3 hullP = hullMain != null ? hullMain.transform.position : new Vector3(12f, gY(12f, -8f) - 0.35f, -8f);

            // idempotency: the add-on parts are only created once
            if (wreckRootTr.Find("Wreck_Hull_Rib_1") != null)
            {
                Debug.Log("[MoonUpg] wreckage add-on parts already present, skipping");
                return;
            }

            Transform Add(string name, PrimitiveType t, Vector3 worldPos, Vector3 rot, Vector3 scale, Material mat, Transform parent)
            {
                var go = GameObject.CreatePrimitive(t);
                go.name = name;
                go.transform.SetParent(parent, false);
                go.transform.position = worldPos;
                go.transform.rotation = Quaternion.Euler(rot);
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = mat;
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
                go.isStatic = true;
                return go.transform;
            }

            // fuselage rib rings around the main hull -> reads as torn user tube section
            Vector3 hullAxis = hullMain != null ? hullMain.transform.forward : Vector3.right;
            foreach (var off in new[] { -1.15f, 0.05f, 1.25f })
            {
                Vector3 p = hullP + hullAxis * off;
                p.y = gY(p.x, p.z) + 0.42f;
                var rib = Add("Wreck_Hull_Rib_" + (off + 2).ToString("0"), PrimitiveType.Cylinder, p,
                    Quaternion.FromToRotation(Vector3.up, hullAxis).eulerAngles,
                    new Vector3(1.55f, 0.05f, 1.55f), matCharred, wreckRootTr);
                rib.GetComponent<Renderer>().sharedMaterial = matCharred;
            }

            // snapped landing legs: bent-under strut, detached strut with foot pad
            Add("Wreck_Leg_Bent", PrimitiveType.Cylinder, hullP + new Vector3(-1.9f, -0.15f, 1.1f),
                new Vector3(18f, 46f, -128f), new Vector3(0.10f, 0.9f, 0.10f), matMetal, wreckRootTr);
            Vector3 legDet = hullP + new Vector3(3.3f, 0f, 4.2f);
            legDet.y = gY(legDet.x, legDet.z) + 0.14f;
            Add("Wreck_Leg_Detached", PrimitiveType.Cylinder, legDet,
                new Vector3(97f, 205f, 6f), new Vector3(0.09f, 0.85f, 0.09f), matMetal, wreckRootTr);
            Vector3 footPad = legDet + new Vector3(0.6f, 0f, -0.4f);
            footPad.y = gY(footPad.x, footPad.z) + 0.04f;
            Add("Wreck_Leg_FootPad", PrimitiveType.Cube, footPad,
                new Vector3(0f, 24f, 6f), new Vector3(0.38f, 0.06f, 0.38f), matCharred, wreckRootTr);
            Add("Wreck_Leg_Joint", PrimitiveType.Sphere, hullP + new Vector3(-1.2f, 0.15f, 1.55f),
                Vector3.zero, new Vector3(0.22f, 0.22f, 0.22f), matDebris, wreckRootTr);

            // torn solar wing: two hinged panel boxes leaning against the hull
            Add("Wreck_SolarWing_A", PrimitiveType.Cube, hullP + new Vector3(-2.6f, 0.62f, -1.2f),
                new Vector3(64f, 128f, -18f), new Vector3(1.1f, 0.035f, 0.82f), matMetal, wreckRootTr);
            Add("Wreck_SolarWing_B", PrimitiveType.Cube, hullP + new Vector3(-3.3f, 0.34f, -0.4f),
                new Vector3(52f, 118f, -10f), new Vector3(1.0f, 0.03f, 0.75f), matDebris, wreckRootTr);

            // ripped-off cockpit shell half sunk near the nose
            Add("Wreck_Cockpit_Shell", PrimitiveType.Sphere, hullP + new Vector3(1.8f, -0.05f, -2.6f),
                new Vector3(14f, 38f, -8f), new Vector3(0.62f, 0.40f, 0.62f), matCharred, wreckRootTr);

            // bent fuel / data lines poking from the hull
            Add("Wreck_FuelLine_A", PrimitiveType.Cylinder, hullP + new Vector3(0.9f, 0.55f, 0.7f),
                new Vector3(32f, 118f, 18f), new Vector3(0.035f, 0.55f, 0.035f), matDebris, wreckRootTr);
            Add("Wreck_FuelLine_B", PrimitiveType.Cylinder, hullP + new Vector3(0.35f, 0.72f, -0.9f),
                new Vector3(60f, 145f, -20f), new Vector3(0.03f, 0.45f, 0.03f), matDebris, wreckRootTr);

            // skid gouges: dark scorched strips where the craft slid before breaking up
            Vector2 entryA = new Vector2(hullP.x + 6.5f, hullP.z + 7.0f);
            Vector2 entryB = new Vector2(hullP.x, hullP.z);
            int gi = 0;
            foreach (var s in new[] { 0.95f, 0.62f, 0.30f })
            {
                Vector2 p2 = Vector2.Lerp(entryA, entryB, s);
                float g1 = gY(p2.x, p2.y), g2 = gY(p2.x + 1.0f, p2.y);
                float slopeDeg = Mathf.Atan2(Mathf.Abs(g2 - g1), 1.0f) * Mathf.Rad2Deg * (g2 < g1 ? 1f : -1f);
                var gouge = GameObject.CreatePrimitive(PrimitiveType.Plane);
                gouge.name = "Crash_SkidGouge_" + (gi++);
                gouge.transform.SetParent(wreckRootTr, false);
                gouge.transform.position = new Vector3(p2.x, g1 + 0.055f, p2.y);
                gouge.transform.rotation = Quaternion.Euler(0f, RandomYaw(entryA.x - entryB.x, entryA.y - entryB.y) + 0f, 0f)
                                         * Quaternion.Euler(slopeDeg, 0f, 0f);
                gouge.transform.localScale = new Vector3(0.14f + 0.05f * s, 1f, 0.30f + 0.10f * s);
                gouge.GetComponent<Renderer>().sharedMaterial = matScorch;
                gouge.isStatic = true;
            }

            // extra fresh shards for debris scatter (uses existing procedural shard meshes)
            Mesh ShardN(int i) => AssetDatabase.LoadAssetAtPath<Mesh>(DirMeshes + "/Wreck_Shard_" + i + ".asset");
            var rnd = new System.Random(9001);
            for (int i = 0; i < 5; i++)
            {
                Vector2 d = new Vector2((float)rnd.NextDouble() * 8f - 4f, (float)rnd.NextDouble() * 8f - 4f);
                Vector3 p = hullP + new Vector3(d.x, 0f, d.y);
                p.y = gY(p.x, p.z) + 0.03f + 0.05f * (float)rnd.NextDouble();
                var go = new GameObject("Wreck_Alt_Shard_" + i);
                go.transform.SetParent(wreckRootTr, false);
                go.transform.position = p;
                go.transform.rotation = Quaternion.Euler((float)rnd.NextDouble() * 70f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 60f);
                go.transform.localScale = Vector3.one * (0.25f + 0.5f * (float)rnd.NextDouble());
                var mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = ShardN(rnd.Next(0, 8));
                go.AddComponent<MeshRenderer>().sharedMaterial = matDebris;
                go.isStatic = true;
            }

            Debug.Log("[MoonUpg] wreckage upgraded: new children=" + wreckRootTr.childCount
                + " (ribs/legs/solar wing/cockpit shell/lines/skid gouges/shards) hullP=" + hullP);
        }

        static float RandomYaw(float dx, float dz) => Mathf.Atan2(dz, dx) * Mathf.Rad2Deg + 90f;

        // ============================================================ final shot

        static void ReframeFinalShot(GameObject root, Terrain terrain, Func<float, float, float> gY)
        {
            var astroTr = root.transform.Find("Astronaut/Survivor_Astronaut");
            var hullTr = root.transform.Find("CrashSite/Wreckage_Spacecraft/Wreck_Hull_Main");
            var camTr = root.transform.Find("Cameras/Camera_06_FinalWide");
            if (astroTr == null || hullTr == null || camTr == null) { Debug.LogError("[MoonUpg] reframe refs missing"); return; }

            Vector3 astro = astroTr.position;
            Vector3 wreck = hullTr.position;
            if (terrain == null) { Debug.LogError("[MoonUpg] terrain missing"); return; }

            Vector3 dir = wreck - astro; dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
            dir.Normalize();

            Vector3 lookAt = Vector3.Lerp(astro, wreck, 0.55f);
            lookAt.y = gY(lookAt.x, lookAt.z) + 0.8f;

            // camera BEHIND the astronaut, looking down the line to the wreck, with a
            // small lateral offset so both subjects separate on screen (~12 apart)
            Vector3 perp = new Vector3(-dir.z, 0f, dir.x); // perpendicular in the ground plane
            Vector3 camPos = astro - dir * 6.5f + perp * 2.0f;
            camPos.y = gY(camPos.x, camPos.z) + 3.6f;

            camTr.position = camPos;
            camTr.rotation = Quaternion.LookRotation((lookAt - camPos).normalized, Vector3.up);
            var cam = camTr.GetComponent<Camera>();
            if (cam != null) cam.fieldOfView = 40f;

            Debug.Log(string.Format("[MoonUpg] Camera_06 reframed: pos={0} lookAt={1} fov=40 (astro dist {2:F1} m, wreck dist {3:F1} m)",
                camPos, lookAt, Vector3.Distance(camPos, astro), Vector3.Distance(camPos, wreck)));

            // re-bake the drift clip for the new pose (same asset path -> GUID survives,
            // so the AnimationPlayableAsset on the timeline keeps referencing it)
            string animPath = DirTimeline + "/Shot_6_FinalWide_drift.anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
            if (clip == null)
            {
                clip = new AnimationClip { frameRate = 30f };
                AssetDatabase.CreateAsset(clip, animPath);
            }
            BuildDriftBaked(clip, camTr, 37.0, 8.0);
            EditorUtility.SetDirty(clip);
        }

        /// <summary>Same drift model as MoonCinematicBuilder.BuildDrift, but evaluated on
        /// an already-positioned camera; slow push-in + micro-shake, baked to keys.</summary>
        static void BuildDriftBaked(AnimationClip clip, Transform cam, double start, double len)
        {
            clip.frameRate = 30f;
            var forward = cam.forward;
            var right = cam.right;
            float push = Mathf.Min(1.1f, (float)len * 0.12f);
            int frames = Mathf.CeilToInt((float)len * 30f);

            var eul = cam.localRotation.eulerAngles;
            Vector3 basePos = cam.localPosition;
            float baseYaw = eul.y, basePitch = eul.x, baseRoll = eul.z;

            var posX = new List<Keyframe>();
            var posY = new List<Keyframe>();
            var posZ = new List<Keyframe>();
            var rotX = new List<Keyframe>();
            var rotY = new List<Keyframe>();
            var rotZ = new List<Keyframe>();

            for (int f = 0; f < frames; f++)
            {
                float t = f / 30f;
                float p = t / (float)len;
                p = p * p * (3f - 2f * p);
                float dx = forward.x * push * p + right.x * (Mathf.Sin(t * 2.3f + (float)start) * 0.02f + Mathf.Sin(t * 5.3f) * 0.007f);
                float dy = forward.y * push * p - (Mathf.Sin(t * 1.9f + 0.6f) * 0.014f + Mathf.Sin(t * 4.7f) * 0.005f);
                float dz = forward.z * push * p + right.z * (Mathf.Sin(t * 2.3f + (float)start) * 0.02f + Mathf.Sin(t * 5.3f) * 0.007f);
                posX.Add(new Keyframe((float)start + t, basePos.x + dx));
                posY.Add(new Keyframe((float)start + t, basePos.y + dy));
                posZ.Add(new Keyframe((float)start + t, basePos.z + dz));

                float rx = basePitch + Mathf.Sin(t * 0.9f + (float)start) * 1.1f * p - p * 0.9f;
                float ry = baseYaw + Mathf.Sin(t * 0.65f + 2.1f) * 1.3f * p;
                float rz = baseRoll + Mathf.Sin(t * 0.5f + 4.4f) * 0.55f * p;
                while (ry - baseYaw > 180f) ry -= 360f;
                while (ry - baseYaw < -180f) ry += 360f;
                rotX.Add(new Keyframe((float)start + t, rx));
                rotY.Add(new Keyframe((float)start + t, ry));
                rotZ.Add(new Keyframe((float)start + t, rz));
            }

            clip.ClearCurves();
            clip.SetCurve("", typeof(Transform), "localPosition.x", new AnimationCurve(posX.ToArray()));
            clip.SetCurve("", typeof(Transform), "localPosition.y", new AnimationCurve(posY.ToArray()));
            clip.SetCurve("", typeof(Transform), "localPosition.z", new AnimationCurve(posZ.ToArray()));
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.x", new AnimationCurve(rotX.ToArray()));
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.y", new AnimationCurve(rotY.ToArray()));
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.z", new AnimationCurve(rotZ.ToArray()));
            Debug.Log("[MoonUpg] Shot_6_FinalWide drift rebaked: " + frames + " frames, base=" + basePos);
        }

        // ============================================================ end card

        static void BuildEndCard(GameObject root)
        {
            var controller = root.GetComponentInChildren<CinematicSequenceController>(true);
            if (controller == null) { Debug.LogError("[MoonUpg] controller missing"); return; }

            if (controller.endCardTitle != null && controller.endCardSub != null)
            {
                Debug.Log("[MoonUpg] end card already wired: " + controller.endCardTitle.name + ", " + controller.endCardSub.name);
                return;
            }

            var storyTr = root.transform.Find("UI/OpeningText_UI/StoryText");
            if (storyTr == null) { Debug.LogError("[MoonUpg] StoryText not found for font/parenting"); return; }
            var storyText = storyTr.GetComponent<Text>();
            var canvasRoot = storyText.canvas != null ? storyText.canvas.transform : storyTr.root;
            // draw order: created last under the canvas -> end card renders above the
            // fade overlay and letterbox bars
            var parent = canvasRoot;

            var card = new GameObject("EndCard_UI", typeof(RectTransform));
            var rt = card.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0.06f, 0.28f);
            rt.anchorMax = new Vector2(0.94f, 0.70f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var titleTr = new GameObject("EndCardTitle", typeof(RectTransform));
            var title = titleTr.AddComponent<Text>();
            title.font = storyText.font; title.fontSize = 56; title.fontStyle = FontStyle.Bold;
            title.alignment = TextAnchor.MiddleCenter;
            title.horizontalOverflow = HorizontalWrapMode.Overflow; title.verticalOverflow = VerticalWrapMode.Overflow;
            var rtT = titleTr.GetComponent<RectTransform>();
            rtT.SetParent(rt, false);
            rtT.anchorMin = new Vector2(0f, 0.42f); rtT.anchorMax = new Vector2(1f, 1f);
            rtT.offsetMin = rtT.offsetMax = Vector2.zero;
            title.color = new Color(0.92f, 0.35f, 0.28f, 0f);

            var subTr = new GameObject("EndCardSub", typeof(RectTransform));
            var sub = subTr.AddComponent<Text>();
            sub.font = storyText.font; sub.fontSize = 24; sub.fontStyle = FontStyle.Bold;
            sub.alignment = TextAnchor.MiddleCenter;
            sub.horizontalOverflow = HorizontalWrapMode.Overflow; sub.verticalOverflow = VerticalWrapMode.Overflow;
            var rtS = subTr.GetComponent<RectTransform>();
            rtS.SetParent(rt, false);
            rtS.anchorMin = new Vector2(0f, 0f); rtS.anchorMax = new Vector2(1f, 0.34f);
            rtS.offsetMin = rtS.offsetMax = Vector2.zero;
            sub.color = new Color(0.82f, 0.80f, 0.78f, 0f);

            controller.endCardTitle = title;
            controller.endCardSub = sub;
            EditorUtility.SetDirty(controller);

            Debug.Log("[MoonUpg] end card built: EndCard_UI with title font=" + (title.font != null ? title.font.name : "NULL")
                + " and sub; wired into controller");
        }
    }
}
#endif
