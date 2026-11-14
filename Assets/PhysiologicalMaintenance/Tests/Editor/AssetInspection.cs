#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PhysiologicalMaintenance.Tests
{
    public static class AssetInspection
    {
        [MenuItem("Tools/Physiological Maintenance/Configure Alternate Character Humanoid")]
        public static void ConfigureAlternateCharacterHumanoid()
        {
            const string path = "Assets/PhysiologicalMaintenance/Art/Characters/UniversalBaseCharacter/Superhero_Male_FullBody.fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError("Alternate character ModelImporter unavailable: " + path);
                return;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.SaveAndReimport();
            var report = new StringBuilder(2048);
            InspectModel(path, report);
            Debug.Log("Configured alternate character importer for Humanoid.\n" + report);
        }

        [MenuItem("Tools/Physiological Maintenance/Inspect Imported Art")]
        public static void InspectImportedArt()
        {
            var report = new StringBuilder(2048);
            InspectModel("Assets/PhysiologicalMaintenance/Art/Character/Grab3D_Man_Base_Mesh_Rigged.fbx", report);
            InspectModel("Assets/PhysiologicalMaintenance/Art/Characters/ManBaseCharacter/ManBaseCharacter.fbx", report);
            InspectModel("Assets/PhysiologicalMaintenance/Art/Characters/_Inspection/Superhero_Male_FullBody.fbx", report);
            InspectModel("Assets/PhysiologicalMaintenance/Art/Characters/_Inspection/Superhero_Female_FullBody.fbx", report);
            InspectModel("Assets/PhysiologicalMaintenance/Art/SpaceStation/CombinedModel.obj", report);
            Debug.Log(report.ToString());
        }

        private static void InspectModel(string path, StringBuilder report)
        {
            report.AppendLine("ASSET: " + path);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                report.AppendLine("  ModelImporter unavailable");
                return;
            }

            report.AppendLine("  Rig=" + importer.animationType + " importAnimation=" + importer.importAnimation +
                " scale=" + importer.globalScale + " materials=" + importer.materialImportMode);
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    report.AppendLine("  Clip=" + clip.name + " length=" + clip.length.ToString("0.00") + "s");
                else if (asset is Mesh mesh)
                    report.AppendLine("  Mesh=" + mesh.name + " vertices=" + mesh.vertexCount + " bounds=" + mesh.bounds);
                else if (asset is Material material)
                    report.AppendLine("  Material=" + material.name + " shader=" + (material.shader != null ? material.shader.name : "null"));
                else if (asset is Avatar modelAvatar)
                    report.AppendLine("  Avatar=" + modelAvatar.name + " valid=" + modelAvatar.isValid + " human=" + modelAvatar.isHuman);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return;

            GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null)
                instance = Object.Instantiate(prefab);
            var animator = instance.GetComponentInChildren<Animator>(true);
            report.AppendLine("  Root=" + prefab.name + " animator=" + (animator != null ? animator.avatar != null ? animator.avatar.name : "avatar missing" : "none"));
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                report.AppendLine("  Renderer=" + renderers[i].name + " type=" + renderers[i].GetType().Name + " localScale=" + renderers[i].transform.localScale +
                    " bounds=" + renderers[i].localBounds + " materials=" + renderers[i].sharedMaterials.Length);
                if (renderers[i] is SkinnedMeshRenderer skinned)
                {
                    report.AppendLine("    Bones=" + skinned.bones.Length + " rootBone=" + (skinned.rootBone != null ? skinned.rootBone.name : "none"));
                    for (int b = 0; b < skinned.bones.Length; b++)
                        report.AppendLine("    Bone=" + skinned.bones[b].name);
                }
            }
            DumpHierarchy(instance.transform, report, 0);
            Object.DestroyImmediate(instance);
        }

        private static void DumpHierarchy(Transform transform, StringBuilder report, int depth)
        {
            if (depth > 4)
                return;
            report.AppendLine("  Node=" + new string(' ', depth * 2) + transform.name);
            for (int i = 0; i < transform.childCount; i++)
                DumpHierarchy(transform.GetChild(i), report, depth + 1);
        }
    }
}
#endif
