using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using MoonCinematic; // runtime scripts

namespace MoonCinematic.EditorTools
{
    /// <summary>
    /// Builds the complete "Moon_Cinematic" opening scene procedurally:
    /// Moon terrain, space with distant Earth, crash site, astronaut survivor,
    /// cinematic lighting, 6 cinematic cameras, Timeline sequence, opening story
    /// text UI and audio ambience. Uses only Unity-native generation and assets
    /// that already exist in the project (all free).
    /// </summary>
    public static class MoonCinematicBuilder
    {
        // ---------------------------------------------------------------- paths
        const string Root = "Assets/MoonCinematic";
        const string DirScripts = Root + "/Scripts";
        const string DirEditor = Root + "/Editor";
        const string DirTextures = Root + "/Textures";
        const string DirMaterials = Root + "/Materials";
        const string DirMeshes = Root + "/Meshes";
        const string DirTerrain = Root + "/Terrain";
        const string DirTimeline = Root + "/Timeline";
        const string DirSettings = Root + "/Settings";
        const string DirScenes = Root + "/Scenes";
        const string DirPreviews = Root + "/Previews";

        const string ScenePath = DirScenes + "/Moon_Cinematic.unity";

        // External free assets we reuse (already imported in the project):
        const string RockPrefabDir = "Assets/Mars Landscape 3D/Prefabs";
        const string RockAlbedoDir = "Assets/Mars Landscape 3D/Resources/Textures";
        const string StarSkyboxMat = "Assets/Stagit/SkyboxEarthPlanets/skyboxes/skyboxv1_starsonly.mat";
        const string EarthSkyFace = "Assets/Stagit/SkyboxEarthPlanets/textures/skyboxv1_earthfar/earth300004.png";
        const string WindSound = "Assets/Mars Landscape 3D/Resources/Sounds/Wind.wav";

        // ---------------------------------------------------------------- world layout constants
        public const float TerrainSize = 600f;
        public const float TerrainHeightRange = 60f;

        // crash site anchor (world XZ)
        public static Vector2 CrashCenter = new Vector2(12f, -8f);

        static readonly CraterDef[] BigCraters =
        {
            new CraterDef(12,   -8, 46, 0.150f, 0.030f),  // main crash crater
            new CraterDef(75,  120, 60, 0.210f, 0.028f),
            new CraterDef(-130, -60, 40, 0.140f, 0.024f),
            new CraterDef(-40, 160, 32, 0.120f, 0.020f),
            new CraterDef(150,  30, 27, 0.100f, 0.018f),
            new CraterDef(95, -120, 34, 0.130f, 0.020f),
            new CraterDef(-150, 60, 36, 0.150f, 0.020f),
            new CraterDef(3,    62, 17, 0.060f, 0.010f),
            new CraterDef(56,   54, 12, 0.050f, 0.008f),
            new CraterDef(-70,-140, 21, 0.080f, 0.014f),
            new CraterDef(160, 100, 18, 0.070f, 0.012f),
        };

        public struct CraterDef
        {
            public Vector2 c; public float r; public float d; public float rim;
            public CraterDef(float x, float z, float radius, float depth, float rimHeight)
            { c = new Vector2(x, z); r = radius; d = depth; rim = rimHeight; }
        }

        static List<CraterDef> s_Craters; // small craters generated with a fixed seed
        static TerrainData s_TerrainData;
        static float s_ScortchRadius = 34f;

        // ---------------------------------------------------------------- entry points

        [MenuItem("Moon Cinematic/Build Everything")]
        public static void BuildFromMenu()
        {
            BuildAll();
        }

        public static void BatchBuildAll()
        {
            int code = 0;
            try { BuildAll(); }
            catch (Exception ex) { Debug.LogException(ex); code = 2; }
            EditorApplication.Exit(code);
        }

        /// <summary>Fast entry point that only (re)renders the Earth disc texture for tuning previews.</summary>
        public static void BatchEarthTune()
        {
            int code = 0;
            try
            {
                Directory.CreateDirectory(DirTextures);
                var tex = MoonFactory.GenerateEarthDisc(1024, new Color(0.25f, 0.55f, 0.85f));
                File.WriteAllBytes(DirTextures + "/Earth_Tune.png", tex.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(tex);
                Debug.Log("[MoonBuild] earth tune preview written");
            }
            catch (Exception ex) { Debug.LogException(ex); code = 2; }
            EditorApplication.Exit(code);
        }

        /// <summary>Bearing from the moon surface up to Earth_Distant (direction, unit length).</summary>
        public static readonly Vector3 EarthDir = new Vector3(-0.62f, 0.40f, -0.68f).normalized;

        public static void BuildAll()
        {
            Debug.Log("[MoonBuild] === START ===");
            EnsureFolders();
            CreateCraters();
            CreateTextures();
            CreateTerrain();
            CreateMaterials();
            CreateMeshes();
            CreateScene();
            CreateTimeline();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MoonBuild] === DONE ===");
        }

        static void EnsureFolders()
        {
            foreach (var dir in new[] { DirTextures, DirMaterials, DirMeshes, DirTerrain, DirTimeline, DirSettings, DirScenes, DirPreviews })
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    int slash = dir.LastIndexOf('/');
                    string parent = dir.Substring(0, slash);
                    if (!AssetDatabase.IsValidFolder(parent)) EnsureFoldersRec(parent);
                    AssetDatabase.CreateFolder(parent, dir.Substring(slash + 1));
                }
        }

        static void EnsureFoldersRec(string dir)
        {
            int slash = dir.LastIndexOf('/');
            string parent = slash > 0 ? dir.Substring(0, slash) : "Assets";
            if (slash > 0 && !AssetDatabase.IsValidFolder(parent)) EnsureFoldersRec(parent);
            string leaf = dir.Substring(slash + 1);
            if (!AssetDatabase.IsValidFolder(dir)) AssetDatabase.CreateFolder(parent, leaf);
        }

        // ---------------------------------------------------------------- terrain math

        static void CreateCraters()
        {
            var list = new List<CraterDef>(BigCraters);
            var rnd = new System.Random(7332);
            for (int i = 0; i < 26; i++)
            {
                float x = (float)(rnd.NextDouble() * 2 - 1) * 275f;
                float z = (float)(rnd.NextDouble() * 2 - 1) * 275f;
                // keep the crash area clean
                if (Mathf.Abs(x - CrashCenter.x) < 70 && Mathf.Abs(z - CrashCenter.y) < 70) { i--; continue; }
                float radius = 3f + (float)rnd.NextDouble() * 9f;
                float depth = 0.015f + (float)rnd.NextDouble() * 0.035f;
                float rim = 0.004f + (float)rnd.NextDouble() * 0.008f;
                list.Add(new CraterDef(x, z, radius, depth, rim));
            }
            s_Craters = list;
        }

        /// <summary>Normalized height (0..1) of the heightmap at world XZ. Mirrors what is written to the terrain data.</summary>
        public static float HeightRaw(float wx, float wz)
        {
            float n1 = MoonFactory.Fbm((wx + 300f) * 0.0045f, (wz + 300f) * 0.0045f, 5, 5);
            float n2 = MoonFactory.Fbm((wx + 300f) * 0.016f, (wz + 300f) * 0.016f, 31, 4);
            float n3 = MoonFactory.Fbm((wx + 300f) * 0.06f, (wz + 300f) * 0.06f, 89, 3);
            float h = 0.40f + (n1 - 0.5f) * 0.30f + (n2 - 0.5f) * 0.10f + (n3 - 0.5f) * 0.03f;

            if (s_Craters != null)
            {
                foreach (var cr in s_Craters)
                {
                    float dist = Mathf.Sqrt((wx - cr.c.x) * (wx - cr.c.x) + (wz - cr.c.y) * (wz - cr.c.y));
                    if (dist < cr.r * 1.08f)
                    {
                        float s = dist / cr.r;
                        h -= cr.d * MoonFactory.CraterProfileShape(s);
                        h += cr.rim * MoonFactory.CraterRimShape(s);
                    }
                }
            }
            return Mathf.Clamp01(h);
        }

        public static float GroundY(float wx, float wz)
        {
            return HeightRaw(wx, wz) * TerrainHeightRange;
        }

        // ---------------------------------------------------------------- textures

        static void CreateTextures()
        {
            WritePng(GenerateOrLoad(DirTextures + "/Regolith_Albedo_00.png", () => MoonFactory.GenerateRegolithAlbedo(1024, 0.52f, 1.0f)), DirTextures + "/Regolith_Albedo_00.png", sRGB: true, wrap: false);
            WritePng(GenerateOrLoad(DirTextures + "/Regolith_Albedo_01.png", () => MoonFactory.GenerateRegolithAlbedo(1024, 0.20f, 1.4f)), DirTextures + "/Regolith_Albedo_01.png", sRGB: true, wrap: false);
            WritePng(GenerateOrLoad(DirTextures + "/Regolith_Normal_00.png", () => MoonFactory.GenerateRegolithNormal(1024)), DirTextures + "/Regolith_Normal_00.png", normal: true);
            WritePng(GenerateOrLoad(DirTextures + "/Scorch_Decal_00.png", () => MoonFactory.GenerateScorchDecal(768)), DirTextures + "/Scorch_Decal_00.png", sRGB: true, clamp: true);
            WritePng(GenerateOrLoad(DirTextures + "/Spark_Sprite.png", () => MoonFactory.GenerateSparkSprite(64)), DirTextures + "/Spark_Sprite.png", sRGB: false, clamp: true, small: true);
            WritePng(GenerateOrLoad(DirTextures + "/Earth_Disc.png", () => MoonFactory.GenerateEarthDisc(1024, new Color(0.25f, 0.55f, 0.85f))), DirTextures + "/Earth_Disc.png", sRGB: true, clamp: true);
            // recolor the three free Mars rock albedos to neutral gray lunar rock
            WritePng(GenerateOrLoad(DirTextures + "/RockGray_00.png", () => MoonFactory.RecolorToGrayMoon(RockAlbedoDir + "/ML3D_Rocks_00_AlbedoTransparency.png", 0.30f)), DirTextures + "/RockGray_00.png", sRGB: true);
            WritePng(GenerateOrLoad(DirTextures + "/RockGray_01.png", () => MoonFactory.RecolorToGrayMoon(RockAlbedoDir + "/ML3D_Rocks_01_AlbedoTransparency.png", 0.34f)), DirTextures + "/RockGray_01.png", sRGB: true);
            WritePng(GenerateOrLoad(DirTextures + "/RockGray_02.png", () => MoonFactory.RecolorToGrayMoon(RockAlbedoDir + "/ML3D_Rocks_02_AlbedoTransparency.png", 0.36f)), DirTextures + "/RockGray_02.png", sRGB: true);
            AssetDatabase.Refresh();
        }

        static Texture2D GenerateOrLoad(string path, Func<Texture2D> generator)
        {
            if (File.Exists(path)) return null; // already imported; will load via AssetDatabase later
            var tex = generator();
            return tex;
        }

        static void WritePng(Texture2D tex, string path, bool sRGB = false, bool wrap = false, bool clamp = false, bool normal = false, bool small = false)
        {
            if (tex == null) return; // existing file: importer configured previously
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex, true);
        }

        static string GetOrCreatedTexturePath(string path) => path;

        static void ConfigureImporter(string path, bool sRGB = false, bool clamp = false, bool normal = false, bool small = false)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            if (small) imp.maxTextureSize = 64;
            if (clamp) imp.wrapMode = TextureWrapMode.Clamp;
            if (normal)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.sRGBTexture = false;
            }
            imp.sRGBTexture = sRGB;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 2;
            imp.SaveAndReimport();
        }

        // ---------------------------------------------------------------- terrain

        static void CreateTerrain()
        {
            string dataPath = DirTerrain + "/Moon_TerrainData.asset";
            bool existed = AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath) != null;

            s_TerrainData = existed
                ? AssetDatabase.LoadAssetAtPath<TerrainData>(dataPath)
                : new TerrainData();

            const int res = 513;
            s_TerrainData.heightmapResolution = res;
            s_TerrainData.size = new Vector3(TerrainSize, TerrainHeightRange, TerrainSize);
            s_TerrainData.alphamapResolution = 257;

            var heights = new float[res, res];
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float wx = (x / (float)(res - 1) - 0.5f) * TerrainSize;
                    float wz = (z / (float)(res - 1) - 0.5f) * TerrainSize;
                    heights[z, x] = HeightRaw(wx, wz);
                }
            }
            s_TerrainData.SetHeights(0, 0, heights);

            // ---- layers
            var albedo0 = AssetDatabase.LoadAssetAtPath<Texture2D>(DirTextures + "/Regolith_Albedo_00.png");
            var albedo1 = AssetDatabase.LoadAssetAtPath<Texture2D>(DirTextures + "/Regolith_Albedo_01.png");
            var normal0 = AssetDatabase.LoadAssetAtPath<Texture2D>(DirTextures + "/Regolith_Normal_00.png");
            ConfigureImporter(DirTextures + "/Regolith_Albedo_00.png", sRGB: true);
            ConfigureImporter(DirTextures + "/Regolith_Albedo_01.png", sRGB: true);
            ConfigureImporter(DirTextures + "/Regolith_Normal_00.png", normal: true);

            string layer0Path = DirTerrain + "/Layer_Regolith.asset";
            string layer1Path = DirTerrain + "/Layer_Scorch.asset";
            TerrainLayer l0 = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layer0Path);
            if (l0 == null)
            {
                l0 = new TerrainLayer();
                AssetDatabase.CreateAsset(l0, layer0Path);
            }
            TerrainLayer l1 = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layer1Path);
            if (l1 == null)
            {
                l1 = new TerrainLayer();
                AssetDatabase.CreateAsset(l1, layer1Path);
            }

            l0.diffuseTexture = albedo0;
            l0.normalMapTexture = normal0;
            l0.normalScale = 1.0f;
            l0.tileSize = new Vector2(24f, 24f);
            l0.tileOffset = Vector2.zero;

            l1.diffuseTexture = albedo1;
            l1.tileSize = new Vector2(18f, 18f);

            EditorUtility.SetDirty(l0);
            EditorUtility.SetDirty(l1);

            s_TerrainData.terrainLayers = new[] { l0, l1 };

            // ---- splat: dark scorch around the crash, gray everywhere else
            const int amapRes = 257;
            var amap = new float[amapRes, amapRes, 2];
            float half = TerrainSize * 0.5f;
            for (int z = 0; z < amapRes; z++)
            {
                for (int x = 0; x < amapRes; x++)
                {
                    float wx = (x / (float)(amapRes - 1) - 0.5f) * TerrainSize;
                    float wz = (z / (float)(amapRes - 1) - 0.5f) * TerrainSize;
                    float dc = Mathf.Sqrt((wx - CrashCenter.x) * (wx - CrashCenter.x) + (wz - CrashCenter.y) * (wz - CrashCenter.y));
                    float scorch = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Clamp01((dc - 6f) / (s_ScortchRadius - 6f)));
                    scorch = Mathf.Pow(scorch, 1.6f) * 0.92f;
                    amap[z, x, 0] = 1f - scorch;
                    amap[z, x, 1] = scorch;
                }
            }
            s_TerrainData.SetAlphamaps(0, 0, amap);

            if (!existed)
            {
                AssetDatabase.CreateAsset(s_TerrainData, dataPath);
                AssetDatabase.SaveAssets();
            }
            else
            {
                EditorUtility.SetDirty(s_TerrainData);
                AssetDatabase.SaveAssets();
            }
        }

        // ---------------------------------------------------------------- materials & meshes

        static void CreateMaterials()
        {
            ConfigureImporter(DirTextures + "/RockGray_00.png", sRGB: true);
            ConfigureImporter(DirTextures + "/RockGray_01.png", sRGB: true);
            ConfigureImporter(DirTextures + "/RockGray_02.png", sRGB: true);
            ConfigureImporter(DirTextures + "/Scorch_Decal_00.png", sRGB: true, clamp: true);
            ConfigureImporter(DirTextures + "/Spark_Sprite.png", sRGB: false, clamp: true, small: true);
            ConfigureImporter(DirTextures + "/Earth_Disc.png", sRGB: true, clamp: true);
            AssetDatabase.Refresh();

            CreateUrpLitMat(DirMaterials + "/Mat_Suit_White.mat", new Color(0.93f, 0.92f, 0.90f), smooth: 0.28f, metal: 0.0f);
            CreateUrpLitMat(DirMaterials + "/Mat_Suit_GreyJoint.mat", new Color(0.42f, 0.42f, 0.44f), smooth: 0.4f, metal: 0.15f);
            CreateUrpLitMat(DirMaterials + "/Mat_Suit_Accent.mat", new Color(0.72f, 0.36f, 0.13f), smooth: 0.35f, metal: 0.0f);
            CreateUrpLitMat(DirMaterials + "/Mat_Visor.mat", new Color(0.05f, 0.06f, 0.07f), smooth: 1.0f, metal: 0.4f);
            CreateUrpLitMat(DirMaterials + "/Mat_Boot.mat", new Color(0.15f, 0.15f, 0.16f), smooth: 0.35f, metal: 0.1f);

            CreateUrpLitMat(DirMaterials + "/Mat_Wreck_Charred.mat", new Color(0.115f, 0.110f, 0.105f), smooth: 0.16f, metal: 0.35f);
            CreateUrpLitMat(DirMaterials + "/Mat_Wreck_Metal.mat", new Color(0.40f, 0.40f, 0.41f), smooth: 0.35f, metal: 0.55f);
            CreateUrpLitMat(DirMaterials + "/Mat_Wreck_Debris.mat", new Color(0.23f, 0.22f, 0.21f), smooth: 0.2f, metal: 0.3f);
            CreateUrpLitMat(DirMaterials + "/Mat_Rock_Gray_00.mat", new Color(0.85f, 0.85f, 0.85f), smooth: 0.35f, metal: 0.05f, texPath: DirTextures + "/RockGray_00.png");
            CreateUrpLitMat(DirMaterials + "/Mat_Rock_Gray_01.mat", new Color(0.85f, 0.85f, 0.85f), smooth: 0.35f, metal: 0.05f, texPath: DirTextures + "/RockGray_01.png");
            CreateUrpLitMat(DirMaterials + "/Mat_Rock_Gray_02.mat", new Color(0.85f, 0.85f, 0.85f), smooth: 0.35f, metal: 0.05f, texPath: DirTextures + "/RockGray_02.png");
        }

        static Material CreateUrpLitMat(string path, Color baseColor, float smooth, float metal, string texPath = null)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", smooth);
            mat.SetFloat("_Metallic", metal);
            if (!string.IsNullOrEmpty(texPath))
            {
                var t = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                if (t != null) mat.SetTexture("_BaseMap", t);
            }
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static void CreateMeshes()
        {
            SaveOrLoadMesh(DirMeshes + "/Wreck_Hull_Main.asset", () => MoonFactory.GenerateCrumpledBox(7, 4, 3, new Vector3(6.0f, 3.2f, 2.6f), 1.0f, 11));
            SaveOrLoadMesh(DirMeshes + "/Wreck_Hull_Tail.asset", () => MoonFactory.GenerateCrumpledBox(5, 3, 2, new Vector3(3.4f, 2.2f, 1.7f), 1.3f, 23));
            SaveOrLoadMesh(DirMeshes + "/Wreck_Hull_Nose.asset", () => MoonFactory.GenerateCrumpledBox(4, 2, 2, new Vector3(2.0f, 1.5f, 1.5f), 1.6f, 37));
            SaveOrLoadMesh(DirMeshes + "/Wreck_Panel_01.asset", () => MoonFactory.GenerateCrumpledBox(6, 1, 4, new Vector3(2.4f, 0.16f, 1.9f), 1.1f, 41));
            SaveOrLoadMesh(DirMeshes + "/Wreck_Panel_02.asset", () => MoonFactory.GenerateCrumpledBox(4, 1, 3, new Vector3(1.7f, 0.14f, 1.4f), 1.2f, 53));
            SaveOrLoadMesh(DirMeshes + "/Wreck_Panel_03.asset", () => MoonFactory.GenerateCrumpledBox(5, 1, 5, new Vector3(2.9f, 0.15f, 2.4f), 1.05f, 67));
            SaveOrLoadMesh(DirMeshes + "/Wreck_Panel_04.asset", () => MoonFactory.GenerateCrumpledBox(3, 1, 4, new Vector3(1.3f, 0.13f, 1.7f), 1.25f, 79));
            for (int i = 0; i < 8; i++)
                SaveOrLoadMesh(DirMeshes + "/Wreck_Shard_" + i + ".asset", (Func<Mesh>)(() => MoonFactory.GenerateAngularShard(1f, 500 + i * 37)));
        }

        static void SaveOrLoadMesh(string path, Func<Mesh> generator)
        {
            if (File.Exists(path)) return;
            var mesh = generator();
            AssetDatabase.CreateAsset(mesh, path);
        }

        // ---------------------------------------------------------------- scene

        static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var root = new GameObject("Moon_Cinematic");

            var environment = new GameObject("Environment"); environment.transform.SetParent(root.transform, false);
            var space = new GameObject("Space"); space.transform.SetParent(root.transform, false);
            var crashSite = new GameObject("CrashSite"); crashSite.transform.SetParent(root.transform, false);
            var astronaut = new GameObject("Astronaut"); astronaut.transform.SetParent(root.transform, false);
            var lighting = new GameObject("Lighting"); lighting.transform.SetParent(root.transform, false);
            var cameras = new GameObject("Cameras"); cameras.transform.SetParent(root.transform, false);
            var cinematic = new GameObject("Cinematic"); cinematic.transform.SetParent(root.transform, false);
            var ui = new GameObject("UI"); ui.transform.SetParent(root.transform, false);

            // ---------- Environment
            var terrainGO = new GameObject("Moon_Terrain");
            terrainGO.transform.SetParent(environment.transform, false);
            terrainGO.transform.position = new Vector3(-TerrainSize * 0.5f, 0f, -TerrainSize * 0.5f);
            var terrain = terrainGO.AddComponent<Terrain>();
            terrain.terrainData = s_TerrainData;
            var terrainMat = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_Terrain_URP.mat");
            if (terrainMat == null)
            {
                terrainMat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit"));
                AssetDatabase.CreateAsset(terrainMat, DirMaterials + "/Mat_Terrain_URP.mat");
            }
            terrain.materialTemplate = terrainMat;
            terrain.bakeLightProbesForTrees = false;
            terrain.drawTreesAndFoliage = false;

            var rocksRoot = new GameObject("Rocks"); rocksRoot.transform.SetParent(environment.transform, false);
            SpawnRocks(rocksRoot.transform, farRocks: 60, nearRocks: 26, pebbles: 90);

            // ---------- Space
            Vector3 earthDir = EarthDir;
            Vector3 earthPos = earthDir * 2600f;
            EarthQuad(space.transform, earthPos, 170f); // ~3.7 deg angular size as seen from the surface

            // ---------- CrashSite
            BuildCrashSite(crashSite.transform);

            // ---------- Astronaut
            BuildAstronaut(astronaut.transform);

            // ---------- Lighting
            BuildLighting(lighting.transform);
            GlobalVolume(lighting.transform);

            // ---------- Cameras
            BuildCameras(cameras.transform, earthDir);

            // ---------- Cinematic
            BuildCinematic(cinematic.transform);

            // ---------- UI
            BuildUI(ui.transform);

            // ---------- scene settings
            // ---------- scene render settings (applies to the newly active scene)
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.075f, 0.078f, 0.092f);
            RenderSettings.ambientEquatorColor = new Color(0.042f, 0.042f, 0.048f);
            RenderSettings.ambientGroundColor = new Color(0.016f, 0.016f, 0.018f);
            RenderSettings.ambientIntensity = 1.05f;
            RenderSettings.fog = false;
            RenderSettings.reflectionIntensity = 0.25f;
            var skyboxMat = AssetDatabase.LoadAssetAtPath<Material>(StarSkyboxMat);
            if (skyboxMat != null) { RenderSettings.skybox = skyboxMat; }
            else Debug.LogError("[MoonBuild] star skybox material missing: " + StarSkyboxMat);
            var sun = lighting.GetComponentInChildren<Light>();
            if (sun != null) RenderSettings.sun = sun;

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[MoonBuild] Scene saved: " + ScenePath);
        }

        static void SpawnRocks(Transform parent, int farRocks, int nearRocks, int pebbles)
        {
            if (!Directory.Exists(RockPrefabDir)) { Debug.LogWarning("[MoonBuild] rock prefab dir missing"); return; }
            var prefabPaths = Directory.GetFiles(RockPrefabDir, "*.prefab").OrderBy(p => p).ToArray();
            if (prefabPaths.Length == 0) return;

            var mat00 = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_Rock_Gray_00.mat");
            var mat01 = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_Rock_Gray_01.mat");
            var mat02 = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_Rock_Gray_02.mat");
            Material MatFor(string path)
            {
                if (path.Contains("Rocks_00")) return mat00;
                if (path.Contains("Rocks_01")) return mat01;
                return mat02;
            }

            var rnd = new System.Random(9013);
            var prefabCache = new Dictionary<string, GameObject>();

            GameObject GetPrefabInstance(string path, Transform par)
            {
                if (!prefabCache.TryGetValue(path, out var prefab))
                {
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    prefabCache[path] = prefab;
                }
                if (prefab == null) return null;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, par);
                // override materials to neutral gray lunar versions
                foreach (var r in inst.GetComponentsInChildren<Renderer>())
                {
                    var mat = MatFor(path);
                    if (mat != null) r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
                }
                return inst;
            }

            void Place(string path, float x, float z, float scale, Transform par)
            {
                var inst = GetPrefabInstance(path, par);
                if (inst == null) return;
                float y = GroundY(x, z);
                inst.transform.position = new Vector3(x, y - 0.18f * scale, z);
                inst.transform.rotation = Quaternion.Euler((float)(rnd.NextDouble() - 0.5) * 14f, (float)(rnd.NextDouble() * 360f), (float)(rnd.NextDouble() - 0.5) * 14f);
                inst.transform.localScale = Vector3.one * scale;
                inst.isStatic = true;
            }

            // far rocks
            for (int i = 0; i < farRocks; i++)
            {
                for (int tries = 0; tries < 6; tries++)
                {
                    float x = (float)(rnd.NextDouble() * 2 - 1) * 285f;
                    float z = (float)(rnd.NextDouble() * 2 - 1) * 285f;
                    if (Mathf.Sqrt((x - CrashCenter.x) * (x - CrashCenter.x) + (z - CrashCenter.y) * (z - CrashCenter.y)) < 70f) continue;
                    Place(prefabPaths[rnd.Next(prefabPaths.Length)], x, z, 0.7f + (float)rnd.NextDouble() * 1.3f, parent);
                    break;
                }
            }
            // near crash but fairly large boulders (frame the shot)
            for (int i = 0; i < nearRocks; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rr = 75f + (float)rnd.NextDouble() * 90f;
                float x = CrashCenter.x + Mathf.Sin(ang) * rr;
                float z = CrashCenter.y + Mathf.Cos(ang) * rr;
                if (Mathf.Abs(x) > 290 || Mathf.Abs(z) > 290) continue;
                Place(prefabPaths[rnd.Next(prefabPaths.Length)], x, z, 0.8f + (float)rnd.NextDouble() * 1.1f, parent);
            }
            // pebbles/small debris rocks near the crash
            var smallPath = prefabPaths.FirstOrDefault(p => p.Contains("Rocks_02")) ?? prefabPaths[0];
            for (int i = 0; i < pebbles; i++)
            {
                float ang = (float)rnd.NextDouble() * Mathf.PI * 2f;
                float rr = 6f + (float)rnd.NextDouble() * 55f;
                float x = CrashCenter.x + Mathf.Sin(ang) * rr;
                float z = CrashCenter.y + Mathf.Cos(ang) * rr;
                Place(smallPath, x, z, 0.05f + (float)rnd.NextDouble() * 0.28f, parent);
            }
        }

        static void EarthQuad(Transform parent, Vector3 pos, float size)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Earth_Distant";
            quad.transform.SetParent(parent, false);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DirTextures + "/Earth_Disc.png");
            // Sprites/Default is unlit + alpha-blended, so the atmosphere halo composites
            // cleanly over the star skybox ("Unlit/Texture" would render the alpha as a black square)
            var shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (tex != null) mat.mainTexture = tex;
            quad.GetComponent<Renderer>().sharedMaterial = mat;
            quad.transform.position = pos;
            quad.transform.localScale = new Vector3(size, size, 1f);
            // face back toward the moon center (we view it from there)
            quad.transform.rotation = Quaternion.LookRotation((Vector3.zero - pos).normalized, Vector3.up);
        }

        static void BuildCrashSite(Transform parent)
        {
            var wreckRoot = new GameObject("Wreckage_Spacecraft");
            wreckRoot.transform.SetParent(parent, false);

            var matCharred = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_Wreck_Charred.mat");
            var matMetal = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_Wreck_Metal.mat");
            var matDebris = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_Wreck_Debris.mat");

            var rnd = new System.Random(4102);
            Vector2 C = CrashCenter;
            float gCr = GroundY(C.x, C.y);

            Mesh Asset(string p) => AssetDatabase.LoadAssetAtPath<Mesh>(p);

            var pieces = new List<(string meshPath, Vector3 pos, Vector3 eul, Vector3 scale, bool metal)>
            {
                ("Wreck_Hull_Main", new Vector3(0f, -0.35f, 0f),   new Vector3(-7f, 38f, -5f),   new Vector3(1f, 1f, 1f),  false),
                ("Wreck_Hull_Tail", new Vector3(-7.5f, -0.1f, -8.2f), new Vector3(16f, 128f, -8f), new Vector3(1.15f, 1.05f, 1.1f), false),
                ("Wreck_Hull_Nose", new Vector3(7.4f, -0.55f, -5.4f), new Vector3(-24f, -55f, 28f), new Vector3(1.3f, 1.25f, 1.25f), true),
            };

            foreach (var p in pieces)
            {
                var part = new GameObject(p.meshPath);
                part.transform.SetParent(wreckRoot.transform, false);
                var mf = part.AddComponent<MeshFilter>();
                mf.sharedMesh = Asset(DirMeshes + "/" + p.meshPath + ".asset");
                part.AddComponent<MeshRenderer>().sharedMaterial = p.metal ? matMetal : matCharred;
                part.transform.localPosition = new Vector3(C.x + p.pos.x, gCr + p.pos.y, C.y + p.pos.z);
                part.transform.localRotation = Quaternion.Euler(p.eul);
                part.transform.localScale = p.scale;
                part.isStatic = true;
            }

            // engine housing (sunk, most deformed)
            var engine = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            engine.name = "Wreck_Engine_Housing";
            engine.transform.SetParent(wreckRoot.transform, false);
            engine.GetComponent<Renderer>().sharedMaterial = matCharred;
            engine.transform.position = new Vector3(C.x + 2.6f, gCr - 0.5f, C.y - 1.8f);
            engine.transform.localRotation = Quaternion.Euler(-20f, 40f, -35f);
            engine.transform.localScale = new Vector3(0.9f, 1.1f, 0.9f);
            // squash it a bit
            var engineMesh = engine.GetComponent<MeshFilter>().mesh;
            engine.GetComponent<MeshFilter>().mesh = engineMesh;
            engine.isStatic = true;

            var bell = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bell.name = "Wreck_Engine_Bell";
            bell.transform.SetParent(wreckRoot.transform, false);
            bell.GetComponent<Renderer>().sharedMaterial = matDebris;
            bell.transform.position = new Vector3(C.x + 4.4f, gCr - 0.75f, C.y - 3.4f);
            bell.transform.localRotation = Quaternion.Euler(65f, 10f, 15f);
            bell.transform.localScale = new Vector3(0.55f, 0.62f, 0.55f);
            bell.isStatic = true;

            // two bent antenna masts
            for (int i = 0; i < 2; i++)
            {
                var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                mast.name = "Wreck_Antenna_Mast_" + i;
                mast.transform.SetParent(wreckRoot.transform, false);
                mast.GetComponent<Renderer>().sharedMaterial = matMetal;
                mast.transform.position = new Vector3(C.x - 2.2f + i * 3.1f, gCr + 0.9f, C.y + 2.4f + i);
                mast.transform.localRotation = Quaternion.Euler(64f + i * 18f, 22f * i, 34f - i * 44f);
                mast.transform.localScale = new Vector3(0.05f, i == 0 ? 1.15f : 0.85f, 0.05f);
                mast.isStatic = true;
            }

            // torn solar / hull panels scattered
            var panelDefs = new (string mesh, Vector3 pos, Vector3 eul, Vector3 scale)[]
            {
                ("Wreck_Panel_01", new Vector3(-8.0f, -0.30f, 1.8f), new Vector3(183f, 42f, 12f), new Vector3(1.35f, 1f, 1.15f)),
                ("Wreck_Panel_02", new Vector3(-3.4f, -0.45f, 6.2f), new Vector3(6f, 110f, 4f), new Vector3(1.1f, 1f, 1f)),
                ("Wreck_Panel_03", new Vector3(9.5f, -0.25f, 3.8f), new Vector3(-12f, 28f, 8f), new Vector3(1.0f, 1f, 0.95f)),
                ("Wreck_Panel_04", new Vector3(4.6f, -0.5f, 8.1f), new Vector3(24f, 64f, -3f), new Vector3(0.8f, 1f, 0.9f)),
                ("Wreck_Panel_01", new Vector3(11.8f, -0.5f, 2.0f), new Vector3(-8f, 152f, -14f), new Vector3(0.9f, 1f, 1.1f)),
                ("Wreck_Panel_03", new Vector3(-1.8f, -0.65f, 10.5f), new Vector3(10f, 74f, 50f), new Vector3(0.75f, 1f, 0.8f)),
            };
            foreach (var pd in panelDefs)
            {
                var g = new GameObject("Wreck_Panel_Scrap");
                g.transform.SetParent(wreckRoot.transform, false);
                var mf = g.AddComponent<MeshFilter>();
                mf.sharedMesh = Asset(DirMeshes + "/" + pd.mesh + ".asset");
                g.AddComponent<MeshRenderer>().sharedMaterial = matMetal;
                g.transform.localPosition = new Vector3(C.x + pd.pos.x, gCr + pd.pos.y + 0.42f, C.y + pd.pos.z);
                g.transform.localRotation = Quaternion.Euler(pd.eul);
                g.transform.localScale = pd.scale;
                g.isStatic = true;
            }

            // angular debris shards streaking away from the impact
            var debris = new GameObject("Debris_Field");
            debris.transform.SetParent(parent, false);
            for (int i = 0; i < 46; i++)
            {
                float f = (float)rnd.NextDouble();
                float dir = Mathf.PI * 0.78f + ((float)rnd.NextDouble() - 0.5f) * 0.9f; // NW->SE streak
                float rr = 2f + f * 55f;
                float x = C.x + Mathf.Sin(dir) * rr * 1.25f;
                float z = C.y + Mathf.Cos(dir) * rr * 0.8f;
                var sh = new GameObject("Debris_Shard");
                sh.transform.SetParent(debris.transform, false);
                var mf = sh.AddComponent<MeshFilter>();
                mf.sharedMesh = Asset(DirMeshes + "/Wreck_Shard_" + rnd.Next(8) + ".asset");
                var mr = sh.AddComponent<MeshRenderer>();
                mr.sharedMaterial = rnd.NextDouble() < 0.7 ? matDebris : matMetal;
                float scale = 0.10f + (float)rnd.NextDouble() * 0.55f;
                sh.transform.localPosition = new Vector3(x, GroundY(x, z) - scale * 0.25f, z);
                sh.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f);
                sh.transform.localScale = Vector3.one * scale;
                sh.isStatic = true;
            }

            // extra scattered haul panels in the field
            for (int i = 0; i < 14; i++)
            {
                float f = (float)rnd.NextDouble();
                float dir = Mathf.PI * 0.78f + ((float)rnd.NextDouble() - 0.5f) * 1.5f;
                float rr = 6f + f * 42f;
                float x = C.x + Mathf.Sin(dir) * rr * 1.25f;
                float z = C.y + Mathf.Cos(dir) * rr * 0.8f;
                var sc = new GameObject("Debris_Panel");
                sc.transform.SetParent(debris.transform, false);
                var mf = sc.AddComponent<MeshFilter>();
                string baseName = rnd.NextDouble() < 0.5f ? "Wreck_Panel_02" : "Wreck_Panel_04";
                mf.sharedMesh = Asset(DirMeshes + "/" + baseName + ".asset");
                sc.AddComponent<MeshRenderer>().sharedMaterial = matMetal;
                float scale = 0.25f + (float)rnd.NextDouble() * 0.75f;
                sc.transform.localPosition = new Vector3(x, GroundY(x, z) + 0.03f * scale, z);
                sc.transform.localRotation = Quaternion.Euler((float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f, (float)rnd.NextDouble() * 360f);
                sc.transform.localScale = Vector3.one * scale;
                sc.isStatic = true;
            }

            // impact scorch decal over the crater floor
            var decal = GameObject.CreatePrimitive(PrimitiveType.Quad);
            decal.name = "Impact_ScorchDecal";
            decal.transform.SetParent(parent, false);
            decal.GetComponent<Renderer>().sharedMaterial = DecalMaterial();
            decal.transform.position = new Vector3(C.x, gCr + 0.06f, C.y);
            decal.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lying flat (sprite faces +Y plane rotated)
            decal.transform.localScale = new Vector3(64f, 64f, 1f);
            decal.isStatic = true;

            // sparks over the cracked battery section
            var spark = new GameObject("Sparks_Electrical");
            spark.transform.SetParent(parent, false);
            spark.transform.position = new Vector3(C.x - 6.4f, gCr + 0.9f, C.y - 9.4f);
            spark.transform.localRotation = Quaternion.Euler(-70f, 0f, 0f);
            BuildSparks(spark);

            // a couple of scorch-marked subtle "rolling ejecta" small rocks off to sides
        }

        static Material DecalMaterial()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_ScorchDecal.mat");
            if (existing != null) return existing;
            var shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DirTextures + "/Scorch_Decal_00.png");
            if (tex != null) mat.mainTexture = tex;
            mat.color = new Color(0.18f, 0.175f, 0.17f, 0.92f);
            AssetDatabase.CreateAsset(mat, DirMaterials + "/Mat_ScorchDecal.mat");
            return mat;
        }

        static void BuildSparks(GameObject spark)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(DirTextures + "/Spark_Sprite.png");
            var shader = Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            if (tex != null) mat.mainTexture = tex;
            mat.color = new Color(1f, 0.75f, 0.35f, 1f);
            const string sparkMatPath = DirMaterials + "/Mat_Spark.mat";
            var saved = AssetDatabase.LoadAssetAtPath<Material>(sparkMatPath);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(mat, sparkMatPath);
                saved = mat;
            }

            var ps = spark.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var rend = ps.GetComponent<ParticleSystemRenderer>();
            rend.sharedMaterial = saved;

            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.055f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.82f, 0.42f), new Color(1f, 0.55f, 0.18f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 220;
            main.gravityModifier = 0.28f; // reduced lunar gravity vibe

            var em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 46) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 22f;
            shape.radius = 0.05f;

            var color = ps.colorOverLifetime;
            color.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.6f), 0f), new GradientColorKey(new Color(1f, 0.45f, 0.15f), 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.85f, 0.5f), new GradientAlphaKey(0f, 1f) });
            color.color = grad;

            var lights = ps.lights;
            lights.enabled = false;

            var sparkLight = spark.AddComponent<Light>();
            sparkLight.type = LightType.Point;
            sparkLight.color = new Color(1f, 0.72f, 0.35f);
            sparkLight.intensity = 5.0f;
            sparkLight.range = 7f;
            sparkLight.shadows = LightShadows.None;
            sparkLight.enabled = false;

            var sparkFlicker = spark.AddComponent<SparkFlicker>(); // from Assembly-CSharp (runtime)
            sparkFlicker.sparkParticles = ps;
            sparkFlicker.sparkLight = sparkLight;
        }

        static void BuildAstronaut(Transform parent)
        {
            var astro = new GameObject("Survivor_Astronaut");
            astro.transform.SetParent(parent, false);

            var matSuit = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_Suit_White.mat");
            var matJoint = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_Suit_GreyJoint.mat");
            var matAccent = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_Suit_Accent.mat");
            var matVisor = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_Visor.mat");
            var matBoot = AssetDatabase.LoadAssetAtPath<Material>(DirMaterials + "/Mat_Boot.mat");

            var ax = CrashCenter.x + 10.6f;
            var az = CrashCenter.y + 15.2f;
            float gy = GroundY(ax, az);
            astro.transform.position = new Vector3(ax, gy + 0.16f, az);
            astro.transform.rotation = Quaternion.Euler(0f, 118f, 0f); // lying, head toward wreckage

            Transform Part(string name, PrimitiveType t, Vector3 pos, Vector3 rot, Vector3 scale, Material mat, Transform par)
            {
                var go = GameObject.CreatePrimitive(t);
                go.name = name;
                go.transform.SetParent(par, false);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(rot);
                go.transform.localScale = scale;
                go.GetComponent<Renderer>().sharedMaterial = mat;
                go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                return go.transform;
            }

            // lying on the back, local +X = feet direction, local +Z = up
            Part("Suit_Torso", PrimitiveType.Capsule, new Vector3(-0.05f, 0.10f, 0.10f), new Vector3(0f, 0f, 90f), new Vector3(0.155f, 0.24f, 0.155f), matSuit, astro.transform);
            Part("Suit_Helmet", PrimitiveType.Sphere, new Vector3(-0.52f, 0.115f, 0.115f), Vector3.zero, new Vector3(0.135f, 0.135f, 0.135f), matSuit, astro.transform);
            Part("Suit_Visor", PrimitiveType.Sphere, new Vector3(-0.585f, 0.112f, 0.12f), new Vector3(0f, 0f, 90f), new Vector3(0.108f, 0.075f, 0.108f), matVisor, astro.transform);
            Part("Suit_NeckRing", PrimitiveType.Cylinder, new Vector3(-0.415f, 0.115f, 0.115f), new Vector3(0f, 0f, 90f), new Vector3(0.075f, 0.05f, 0.075f), matJoint, astro.transform);
            Part("Suit_FrontConsole", PrimitiveType.Cube, new Vector3(0.02f, 0.185f, 0.12f), new Vector3(0f, 0f, 0f), new Vector3(0.20f, 0.10f, 0.028f), matAccent, astro.transform);
            Part("Suit_ConsoleKnob1", PrimitiveType.Cube, new Vector3(-0.03f, 0.20f, 10f*0f + 0.135f), new Vector3(0f, 0f, 0f), new Vector3(0.035f, 0.035f, 0.012f), matJoint, astro.transform);
            Part("Suit_ConsoleKnob2", PrimitiveType.Cube, new Vector3(0.045f, 0.20f, 0.135f), new Vector3(0f, 0f, 0f), new Vector3(0.035f, 0.035f, 0.012f), matJoint, astro.transform);
            Part("Suit_Backpack", PrimitiveType.Cube, new Vector3(0f, 0.16f, -0.10f), new Vector3(0f, 0f, 0f), new Vector3(0.30f, 0.38f, 0.16f), matSuit, astro.transform);
            Part("Suit_BackpackPanel", PrimitiveType.Cube, new Vector3(0f, 0.29f, -0.185f), new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0.16f, 0.02f), matJoint, astro.transform);

            // right arm: limp, bent elbow, hand up near helmet/shoulder area
            Part("Arm_R_Upper", PrimitiveType.Capsule, new Vector3(0.22f, 0.15f, 0.10f), new Vector3(0f, 0f, 28f), new Vector3(0.045f, 0.19f, 0.045f), matSuit, astro.transform);
            Part("Arm_R_Fore", PrimitiveType.Capsule, new Vector3(0.40f, 0.155f, 0.05f), new Vector3(0f, 0f, 68f), new Vector3(0.038f, 0.16f, 0.038f), matSuit, astro.transform);
            Part("Glove_R", PrimitiveType.Sphere, new Vector3(0.575f, 0.14f, -0.015f), Vector3.zero, new Vector3(0.055f, 0.055f, 0.055f), matJoint, astro.transform);
            // left arm: hanging over torso, motionless
            Part("Arm_L_Upper", PrimitiveType.Capsule, new Vector3(-0.32f, 0.12f, 0.045f), new Vector3(0f, 0f, 74f), new Vector3(0.045f, 0.19f, 0.045f), matSuit, astro.transform);
            Part("Arm_L_Fore", PrimitiveType.Capsule, new Vector3(-0.12f, 0.13f, 0.02f), new Vector3(0f, 0f, 16f), new Vector3(0.038f, 0.17f, 0.038f), matSuit, astro.transform);
            Part("Glove_L", PrimitiveType.Sphere, new Vector3(0.075f, 0.135f, 0.02f), Vector3.zero, new Vector3(0.055f, 0.055f, 0.055f), matJoint, astro.transform);

            // legs: knees slightly bent, one leg fallen over the other
            Part("Leg_R_Upper", PrimitiveType.Capsule, new Vector3(0.26f, 0.115f, 0.10f), new Vector3(0f, 0f, 96f), new Vector3(0.065f, 0.21f, 0.065f), matSuit, astro.transform);
            Part("Leg_R_Lower", PrimitiveType.Capsule, new Vector3(0.585f, 0.135f, 0.115f), new Vector3(0f, 0f, 80f), new Vector3(0.052f, 0.20f, 0.052f), matSuit, astro.transform);
            Part("Leg_R_Boot", PrimitiveType.Cube, new Vector3(0.79f, 0.155f, 0.115f), new Vector3(0f, 0f, 0f), new Vector3(0.13f, 0.09f, 0.075f), matBoot, astro.transform);
            Part("Leg_L_Upper", PrimitiveType.Capsule, new Vector3(0.245f, 0.145f, -0.025f), new Vector3(0f, 0f, 100f), new Vector3(0.065f, 0.21f, 0.065f), matSuit, astro.transform);
            Part("Leg_L_Lower", PrimitiveType.Capsule, new Vector3(0.60f, 0.185f, 0.02f), new Vector3(0f, 0f, 93f), new Vector3(0.052f, 0.20f, 0.052f), matSuit, astro.transform);
            Part("Leg_L_Boot", PrimitiveType.Cube, new Vector3(0.81f, 0.20f, 0.02f), new Vector3(0f, 0f, 0f), new Vector3(0.13f, 0.09f, 0.075f), matBoot, astro.transform);
            // knee joint caps
            Part("Suit_Joint_R_Knee", PrimitiveType.Sphere, new Vector3(0.415f, 0.12f, 0.11f), Vector3.zero, new Vector3(0.055f, 0.055f, 0.055f), matJoint, astro.transform);
            Part("Suit_Joint_L_Knee", PrimitiveType.Sphere, new Vector3(0.40f, 0.15f, -0.01f), Vector3.zero, new Vector3(0.055f, 0.055f, 0.055f), matJoint, astro.transform);

            // tumbled sample kit boxes near the survivor (scientific mission props)
            var kit = new GameObject("SampleKit_Tumbled");
            kit.transform.SetParent(parent, false);
            float kx = ax - 1.2f, kz = az - 1.1f;
            float kgy = GroundY(kx, kz);
            kit.transform.position = new Vector3(kx, kgy + 0.1f, kz);
            kit.transform.rotation = Quaternion.Euler(0f, 30f, 0f);
            Part("Sample_Crate_1", PrimitiveType.Cube, new Vector3(0f, 0.06f, 0f), new Vector3(0f, 0f, 0f), new Vector3(0.30f, 0.12f, 0.22f), matSuit, kit.transform);
            Part("Sample_Crate_2", PrimitiveType.Cube, new Vector3(0.35f, 0.05f, 0.2f), new Vector3(0f, 22f, -2f), new Vector3(0.26f, 0.10f, 0.19f), matJoint, kit.transform);
            Part("Sample_Lid_1", PrimitiveType.Cube, new Vector3(-0.3f, 0.03f, -0.25f), new Vector3(0f, 80f, 3f), new Vector3(0.26f, 0.02f, 0.20f), matAccent, kit.transform);
        }

        static void BuildLighting(Transform parent)
        {
            var sun = new GameObject("Sun_Lunar");
            sun.transform.SetParent(parent, false);
            var l = sun.AddComponent<Light>();
            l.type = LightType.Directional;
            // sun from east-northeast, moderately low -> long dramatic shadows
            Vector3 forward = new Vector3(0.44f, -0.36f, -0.83f).normalized;
            sun.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            l.color = new Color(1.0f, 0.965f, 0.91f);
            l.intensity = 3.1f;
            l.shadows = LightShadows.Soft;
            l.shadowStrength = 0.96f;
            l.shadowBias = 0.04f;
            l.shadowNormalBias = 0.5f;
            l.shadowCustomResolution = 4096;
            sun.isStatic = true;

            var fill = new GameObject("Fill_Earthlight");
            fill.transform.SetParent(parent, false);
            var lf = fill.AddComponent<Light>();
            lf.type = LightType.Directional;
            // soft cool fill shining FROM the Earth quadrant down onto the scene:
            // light travels along (moon - earth) = -earthDir, so it relights exactly
            // the ground seen when the cameras look toward Earth (visible "earthshine")
            Vector3 fdir = (-EarthDir).normalized;
            fill.transform.rotation = Quaternion.LookRotation(fdir, Vector3.up);
            lf.color = new Color(0.62f, 0.70f, 0.85f);
            lf.intensity = 0.5f;
            lf.shadows = LightShadows.None;
            fill.isStatic = true;
        }

        static void GlobalVolume(Transform parent)
        {
            var vol = parent.Find("__GlobalVolume");
            if (vol == null)
            {
                vol = new GameObject("__GlobalVolume").transform;
                vol.SetParent(parent, false);
            }
            var volume = vol.GetComponent<Volume>() ?? vol.gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 5f;

            const string profilePath = DirSettings + "/MoonCinematicVolume.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
            }

            // add/patch components
            void Patch<T>(Action<T> apply) where T : VolumeComponent, new()
            {
                T c;
                if (!profile.TryGet<T>(out c)) c = profile.Add<T>(true);
                apply(c);
            }
            Patch<Bloom>(b =>
            {
                b.active = true;
                b.intensity.overrideState = true; b.intensity.value = 0.32f;
                b.threshold.overrideState = true; b.threshold.value = 0.85f;
                b.tint.overrideState = true; b.tint.value = new Color(1f, 0.97f, 0.92f);
            });
            Patch<Vignette>(v =>
            {
                v.active = true;
                v.intensity.overrideState = true; v.intensity.value = 0.38f;
                v.smoothness.overrideState = true; v.smoothness.value = 0.45f;
            });
            Patch<ColorAdjustments>(c =>
            {
                c.active = true;
                c.postExposure.overrideState = true; c.postExposure.value = -0.25f;
                c.contrast.overrideState = true; c.contrast.value = 9f;
                c.saturation.overrideState = true; c.saturation.value = -6f;
                c.hueShift.overrideState = true; c.hueShift.value = 0f;
            });
            Patch<FilmGrain>(g =>
            {
                g.active = true;
                g.intensity.overrideState = true; g.intensity.value = 0.30f;
                g.type.overrideState = false;
            });

            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);
            EditorUtility.SetDirty(profile);
        }

        static void BuildCameras(Transform parent, Vector3 earthDir)
        {
            // All heights are snapped to the terrain: the regolith baseline is around
            // GroundY(x,z) (~24 m), so hard-coded Y would bury the cameras underground.
            float Ay(float x, float z, float off) => GroundY(x, z) + off;
            var astroX = CrashCenter.x + 10.6f;   // keep in sync with BuildAstronaut
            var astroZ = CrashCenter.y + 15.2f;

            var camDefs = new[]
            {
                new { name="Camera_01_Establishing", pos=new Vector3(56f, Ay(56f,42f,9.5f), 42f),    lookAt=new Vector3(16f, Ay(16f,-6f,21f), -6f), fov=62f, near=0.1f },
                new { name="Camera_02_CrashSite",    pos=new Vector3(-4f, Ay(-4f,20f,2.2f), 20f),   lookAt=new Vector3(12f, Ay(12f,-9f,4f), -9f),  fov=48f, near=0.08f },
                new { name="Camera_03_Astronaut",    pos=new Vector3(26.8f, Ay(26.8f,8.9f,1.65f), 8.9f), lookAt=new Vector3(astroX, Ay(astroX,astroZ,0.45f), astroZ), fov=30f, near=0.06f },
                new { name="Camera_04_Earth",        pos=new Vector3(26.5f, Ay(26.5f,13.5f,2.2f), 13.5f), lookAt=new Vector3(astroX, Ay(astroX,astroZ,0.45f), astroZ), fov=52f, near=0.06f },
                new { name="Camera_05_Wreckage",     pos=new Vector3(17.6f, Ay(17.6f,4.2f,1.5f), 4.2f),  lookAt=new Vector3(9.6f, Ay(9.6f,-10.2f,2.0f), -10.2f), fov=29f, near=0.05f },
                new { name="Camera_06_FinalWide",    pos=new Vector3(72f, Ay(72f,64f,10f), 64f),    lookAt=new Vector3(18f, Ay(18f,2f,22f), 2f),   fov=42f, near=0.5f },
            };

            foreach (var d in camDefs)
            {
                var go = new GameObject(d.name);
                go.transform.SetParent(parent, false);
                var cam = go.AddComponent<Camera>();
                if (d.name.Contains("04"))
                {
                    // Aim deterministically: take the Earth bearing, offset its yaw,
                    // then look at a fixed elevation so the ground foreground fills
                    // the lower frame and Earth sits in the sky (no Euler-composition quirks)
                    Vector3 dir = (earthDir * 2600f - d.pos).normalized;
                    Vector3 dirH = new Vector3(dir.x, 0f, dir.z).normalized;
                    float elevGoal = 2f * Mathf.Deg2Rad;
                    Vector3 aim = dirH * Mathf.Cos(elevGoal) + Vector3.up * Mathf.Sin(elevGoal);
                    go.transform.rotation = Quaternion.LookRotation(aim, Vector3.up);
                }
                else
                {
                    go.transform.rotation = Quaternion.LookRotation((d.lookAt - d.pos).normalized, Vector3.up);
                }
                go.transform.position = d.pos;
                cam.fieldOfView = d.fov;
                cam.nearClipPlane = d.near;
                cam.farClipPlane = 5000f;
                var add = go.AddComponent<UniversalAdditionalCameraData>();
                add.renderPostProcessing = true;
                add.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                add.allowXRRendering = false;
                cam.enabled = d.name.Contains("01"); // only establishing shot active by default; Timeline switches cameras
            }
        }

        static void BuildCinematic(Transform parent)
        {
            var dirGo = new GameObject("PlayableDirector");
            dirGo.transform.SetParent(parent, false);
            var director = dirGo.AddComponent<PlayableDirector>();
            director.playOnAwake = true;
            director.extrapolationMode = DirectorWrapMode.Hold;

            // ambience audio (free wind ambience from an existing project asset)
            var audioGo = new GameObject("Ambience_Audio");
            audioGo.transform.SetParent(dirGo.transform, false);
            var src = audioGo.AddComponent<AudioSource>();
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(WindSound);
            if (clip != null) src.clip = clip;
            src.loop = true;
            src.playOnAwake = true;
            src.volume = 0.22f;
            src.pitch = 0.82f;      // deepened rumble
            src.spatialBlend = 0f;  // ambient 2D
            src.dopplerLevel = 0f;

            var controller = dirGo.AddComponent<CinematicSequenceController>(); // runtime script
            controller.director = director;
            controller.ambienceSource = src;
            controller.fadeInDuration = 2.2f;
            controller.fadeOutStart = 43.4f;
            controller.fadeOutDuration = 1.6f;
            // UI references get wired in BuildUI via state object
            MoonCinematicUIState.CinematicDirector = director;
            MoonCinematicUIState.Controller = controller;
        }

        static void BuildUI(Transform parent)
        {
            var canvasGo = new GameObject("OpeningText_UI", typeof(Canvas), typeof(CanvasScaler));
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var controller = MoonCinematicUIState.Controller;
            if (controller == null) { Debug.LogError("[MoonBuild] missing cinematic controller"); return; }
            controller.uiCanvas = canvas;

            Font GetFont()
            {
                Font font = null;
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch { font = null; }
                if (font == null)
                {
                    font = Font.CreateDynamicFontFromOSFont("Arial", 34);
                }
                return font;
            }

            // fade overlay
            var fadeGo = new GameObject("FadeOverlay", typeof(Image));
            fadeGo.transform.SetParent(canvasGo.transform, false);
            var fadeImg = fadeGo.GetComponent<Image>();
            SetAnchor(fadeImg.rectTransform, 0f, 0f, 1f, 1f);
            fadeImg.color = new Color(0f, 0f, 0f, 1f);
            fadeImg.raycastTarget = false;
            controller.fadeOverlay = fadeImg;

            // letterbox bars
            var topGo = new GameObject("TopBar", typeof(Image));
            topGo.transform.SetParent(canvasGo.transform, false);
            var topImg = topGo.GetComponent<Image>();
            SetAnchor(topImg.rectTransform, 0f, 0.90f, 1f, 1f);
            topImg.color = new Color(0f, 0f, 0f, 1f);
            topImg.raycastTarget = false;
            controller.topBar = topImg;

            var botGo = new GameObject("BottomBar", typeof(Image));
            botGo.transform.SetParent(canvasGo.transform, false);
            var botImg = botGo.GetComponent<Image>();
            SetAnchor(botImg.rectTransform, 0f, 0f, 1f, 0.10f);
            botImg.color = new Color(0f, 0f, 0f, 1f);
            botImg.raycastTarget = false;
            controller.bottomBar = botImg;

            // story text, bottom third, simple timeless typography
            var textGo = new GameObject("StoryText", typeof(Text));
            textGo.transform.SetParent(canvasGo.transform, false);
            var text = textGo.GetComponent<Text>();
            SetAnchor(text.rectTransform, 0.06f, 0.135f, 0.94f, 0.33f);
            text.font = GetFont();
            text.fontSize = 30;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 18;
            text.resizeTextMaxSize = 30;
            text.color = new Color(0.95f, 0.95f, 0.96f, 0f);
            text.raycastTarget = false;
            var shadow = textGo.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
            shadow.effectDistance = new Vector2(2f, -2f);
            text.text = "";
            controller.storyText = text;
        }

        static void SetAnchor(RectTransform rt, float minX, float minY, float maxX, float maxY)
        {
            rt.anchorMin = new Vector2(minX, minY);
            rt.anchorMax = new Vector2(maxX, maxY);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        // ---------------------------------------------------------------- timeline

        static void CreateTimeline()
        {
            Debug.Log("[MoonBuild] timeline step start");
            try
            {
            string timelinePath = DirTimeline + "/MoonCinematic.playable";

            Debug.Log("[MoonBuild] opening scene for timeline binding");
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("[MoonBuild] scene opened for timeline binding");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var director = scene.GetRootGameObjects().Select(g => g.transform.GetComponentsInChildren<PlayableDirector>()).SelectMany(x => x).FirstOrDefault();
            var camRoot = scene.GetRootGameObjects().First(g => g.name == "Moon_Cinematic").transform.Find("Cameras");
            if (director == null || camRoot == null) { Debug.LogError("[MoonBuild] scene objects missing for timeline step"); return; }
            Debug.Log("[MoonBuild] director resolved: " + (director != null));

            var asset = LoadOrCreateTimelineAsset(timelinePath);
            Debug.Log("[MoonBuild] timeline asset ready: " + (asset != null));

            // Pre-pass: write every drift .anim asset FIRST. Each AssetDatabase.CreateAsset
            // triggers an import/refresh that invalidates freshly-created native assets,
            // so no timeline tracks may exist yet when those refreshes fire.
            var cams = new[]
            {
                (trackName: "Shot_1_Establishing", cam: camRoot.Find("Camera_01_Establishing").gameObject, start: 0.0, len: 8.0),
                (trackName: "Shot_2_CrashSite",    cam: camRoot.Find("Camera_02_CrashSite").gameObject, start: 8.0, len: 7.0),
                (trackName: "Shot_3_Astronaut",    cam: camRoot.Find("Camera_03_Astronaut").gameObject, start: 15.0, len: 7.0),
                (trackName: "Shot_4_Earth",        cam: camRoot.Find("Camera_04_Earth").gameObject, start: 22.0, len: 8.0),
                (trackName: "Shot_5_Wreckage",     cam: camRoot.Find("Camera_05_Wreckage").gameObject, start: 30.0, len: 7.0),
                (trackName: "Shot_6_FinalWide",    cam: camRoot.Find("Camera_06_FinalWide").gameObject, start: 37.0, len: 8.0),
            };

            // ---- pre-pass: drift .anim assets (visible, loadable, saved to disk) ----
            var driftClips = new AnimationClip[cams.Length];
            for (int i = 0; i < cams.Length; i++)
            {
                var def = cams[i];
                string animPath = DirTimeline + "/" + def.trackName + "_drift.anim";
                AssetDatabase.DeleteAsset(animPath);

                var animClip = new AnimationClip { frameRate = 30f };
                BuildDrift(animClip, def.cam.transform, def.start, def.len);
                AssetDatabase.CreateAsset(animClip, animPath);
                AssetDatabase.SaveAssets();
                var saved = AssetDatabase.LoadAssetAtPath<AnimationClip>(animPath);
                Debug.Log("[MoonBuild] anim asset saved for " + def.trackName + (saved != null ? " OK" : " FAILED"));
                driftClips[i] = saved != null ? saved : animClip;
            }

            // The anim asset imports may have invalidated the timeline's native object;
            // reload a fresh instance from disk before building any tracks.
            asset = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            if (asset == null)
            {
                asset = LoadOrCreateTimelineAsset(timelinePath);
            }
            asset.editorSettings.fps = 30f;

            foreach (var def0 in cams)
            {
                // activation track: switches cameras on/off
                Debug.Log("[MoonBuild] creating activation track: " + def0.trackName);
                var act = asset.CreateTrack<ActivationTrack>(null, def0.trackName);
                act.name = def0.trackName;
                act.postPlaybackState = ActivationTrack.PostPlaybackState.Inactive;
                var clipA = act.CreateDefaultClip();
                clipA.start = def0.start;
                clipA.duration = def0.len;
                clipA.displayName = def0.cam.name;
                Debug.Log("[MoonBuild] activation track created " + def0.trackName);
            }

            // animation tracks: subtle cinematic drift, bound to the saved .anim assets
            for (int i = 0; i < cams.Length; i++)
            {
                var def = cams[i];
                var animTrack = asset.CreateTrack<AnimationTrack>(null, def.trackName + "_Anim");
                animTrack.name = def.trackName + "_Anim";
                var clipB = animTrack.CreateClip<AnimationPlayableAsset>();
                clipB.start = def.start;
                clipB.duration = def.len;
                clipB.displayName = def.cam.name + "_drift";
                ((AnimationPlayableAsset)clipB.asset).clip = driftClips[i];
                Debug.Log("[MoonBuild] binding anim for " + def.cam.name);
                director.SetGenericBinding(animTrack, def.cam);
            }

            // camera activation bindings go on the activation tracks themselves
            for (int i = 0; i < cams.Length; i++)
            {
                var actTrack = asset.GetOutputTracks().OfType<ActivationTrack>().FirstOrDefault(t => t.name == cams[i].trackName);
                if (actTrack == null) { Debug.LogError("[MoonBuild] activation track missing: " + cams[i].trackName); continue; }
                director.SetGenericBinding(actTrack, cams[i].cam);
                Debug.Log("[MoonBuild] binding act for " + cams[i].cam.name);
            }

            director.timeUpdateMode = DirectorUpdateMode.GameTime;
            director.playableAsset = asset;
            EditorUtility.SetDirty(director);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log("[MoonBuild] Timeline written and bound: " + timelinePath);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                Debug.LogError("[MoonBuild] timeline step failed");
            }
        }

        /// <summary>Deletes any stale .playable and creates a clean, saved TimelineAsset, returning a live instance.</summary>
        static TimelineAsset LoadOrCreateTimelineAsset(string timelinePath)
        {
            AssetDatabase.DeleteAsset(timelinePath);
            var asset = ScriptableObject.CreateInstance<TimelineAsset>();
            asset.editorSettings.fps = 30f;
            AssetDatabase.CreateAsset(asset, timelinePath);
            AssetDatabase.SaveAssets();
            var loaded = AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            return loaded != null ? loaded : asset;
        }

        /// <summary>Small, human-feeling dolly + micro-shake baked into the shot window.</summary>
        static void BuildDrift(AnimationClip clip, Transform cam, double start, double len)
        {
            clip.frameRate = 30f;
            var forward = cam.forward;
            var right = cam.right;
            float push = Mathf.Min(1.1f, (float)len * 0.12f); // slow push-in
            int frames = Mathf.CeilToInt((float)len * 30f);

            var eul = cam.localRotation.eulerAngles;
            Vector3 basePos = cam.localPosition;

            float baseYaw = eul.y, basePitch = eul.x, baseRoll = eul.z;

            // Simple construction: independent curves for each local channel
            var posCurve = new List<Keyframe>();
            var upCurve = new List<Keyframe>();
            for (int f = 0; f < frames; f++)
            {
                float t = f / 30f;
                float p = t / (float)len;
                p = p * p * (3f - 2f * p);
                float dx = forward.x * push * p + right.x * (Mathf.Sin(t * 2.3f + (float)start) * 0.02f + Mathf.Sin(t * 5.3f) * 0.007f);
                float dy = forward.y * push * p - (Mathf.Sin(t * 1.9f + 0.6f) * 0.014f + Mathf.Sin(t * 4.7f) * 0.005f);
                posCurve.Add(new Keyframe((float)start + t, basePos.x + dx));
                upCurve.Add(new Keyframe((float)start + t, basePos.y + dy));
            }

            // Build per-channel curves cleanly (x in posCurve, y in upCurve; recompute z now)
            var zCurve = new List<Keyframe>();
            for (int f = 0; f < frames; f++)
            {
                float t = f / 30f;
                float p = t / (float)len;
                p = p * p * (3f - 2f * p);
                float dz = forward.z * push * p + right.z * (Mathf.Sin(t * 2.3f + (float)start) * 0.02f + Mathf.Sin(t * 5.3f) * 0.007f);
                zCurve.Add(new Keyframe((float)start + t, basePos.z + dz));
            }

            var posX = new AnimationCurve(posCurve.ToArray());
            var posY = new AnimationCurve(upCurve.ToArray());
            var posZ = new AnimationCurve(zCurve.ToArray());

            // rotation micro drift: a couple of gentle degrees at most
            var rotX = new List<Keyframe>();
            var rotY = new List<Keyframe>();
            var rotZ = new List<Keyframe>();
            for (int f = 0; f < frames; f++)
            {
                float t = f / 30f;
                float p = t / (float)len;
                p = p * p * (3f - 2f * p);
                float rx = basePitch + Mathf.Sin(t * 0.9f + (float)start) * 1.1f * p - p * 0.9f;
                float ry = baseYaw + Mathf.Sin(t * 0.65f + 2.1f) * 1.3f * p;
                float rz = baseRoll + Mathf.Sin(t * 0.5f + 4.4f) * 0.55f * p;
                // normalize yaw near camera's base to avoid 360 jumps
                while (ry - baseYaw > 180f) ry -= 360f;
                while (ry - baseYaw < -180f) ry += 360f;
                rotX.Add(new Keyframe((float)start + t, rx));
                rotY.Add(new Keyframe((float)start + t, ry));
                rotZ.Add(new Keyframe((float)start + t, rz));
            }

            clip.SetCurve("", typeof(Transform), "localPosition.x", posX);
            clip.SetCurve("", typeof(Transform), "localPosition.y", posY);
            clip.SetCurve("", typeof(Transform), "localPosition.z", posZ);
            // NOTE: "localRotation.x/y/z" are raw quaternion components (range -1..1);
            // feeding Euler degrees there produces garbage rotations. The Euler-degree
            // channel Unity uses for authored rotation curves is "localEulerAnglesRaw".
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.x", new AnimationCurve(rotX.ToArray()));
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.y", new AnimationCurve(rotY.ToArray()));
            clip.SetCurve("", typeof(Transform), "localEulerAnglesRaw.z", new AnimationCurve(rotZ.ToArray()));
        }
    }

    /// <summary>Scratch state to hand objects from scene creation into the UI builder step.</summary>
    internal static class MoonCinematicUIState
    {
        public static PlayableDirector CinematicDirector;
        public static CinematicSequenceController Controller;
    }
}
