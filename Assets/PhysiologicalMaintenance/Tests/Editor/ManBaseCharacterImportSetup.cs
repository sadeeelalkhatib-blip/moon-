#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PhysiologicalMaintenance.Tests
{
    public static class ManBaseCharacterImportSetup
    {
        private const string ModelPath = "Assets/PhysiologicalMaintenance/Art/Characters/ManBaseCharacter/ManBaseCharacter.fbx";
        private const string PrefabPath = "Assets/PhysiologicalMaintenance/Resources/PhysiologicalMaintenance/Characters/ManBaseCharacter.prefab";

        public static void ConfigureAndInspect()
        {
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
                throw new System.InvalidOperationException("Model importer unavailable for " + ModelPath);

            // Inspection confirmed this FBX is a static mesh: Humanoid import fails because it has no Hips bone.
            importer.animationType = ModelImporterAnimationType.None;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.importAnimation = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.globalScale = 1f;
            importer.SaveAndReimport();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
                throw new System.InvalidOperationException("FBX model did not import: " + ModelPath);

            var report = new StringBuilder();
            report.AppendLine("FBX: " + ModelPath);
            report.AppendLine("Model loaded: true");
            var renderers = model.GetComponentsInChildren<Renderer>(true);
            report.AppendLine("Renderer count: " + renderers.Length);
            report.AppendLine("Animator count: " + model.GetComponentsInChildren<Animator>(true).Length);
            foreach (var renderer in renderers)
            {
                report.AppendLine("Renderer: " + renderer.GetType().Name + " " +
                                  AnimationUtility.CalculateTransformPath(renderer.transform, model.transform) +
                                  " bounds=" + renderer.bounds +
                                  " mesh=" + (renderer is MeshRenderer ? renderer.GetComponent<MeshFilter>()?.sharedMesh?.name : "skinned"));
                foreach (var material in renderer.sharedMaterials)
                    report.AppendLine("Material: " + (material != null ? material.name + " shader=" + material.shader.name +
                        " color=" + material.color + " mainTexture=" + (material.mainTexture != null ? material.mainTexture.name : "null") : "null"));
            }
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(ModelPath))
            {
                if (asset is Texture2D texture)
                    report.AppendLine("Embedded texture: " + texture.name + " " + texture.width + "x" + texture.height);
                if (asset is Avatar avatar)
                    report.AppendLine("Imported avatar subasset: " + avatar.name + " human=" + avatar.isHuman + " valid=" + avatar.isValid);
            }

            var modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            modelInstance.name = "Man Base Character FBX";
            var instanceRenderers = modelInstance.GetComponentsInChildren<Renderer>(true);
            if (instanceRenderers.Length == 0)
                throw new System.InvalidOperationException("Imported character has no renderers.");
            var combinedBounds = instanceRenderers[0].bounds;
            for (int i = 1; i < instanceRenderers.Length; i++)
                combinedBounds.Encapsulate(instanceRenderers[i].bounds);
            float sourceHeight = combinedBounds.size.y;
            if (sourceHeight <= 0.001f)
                throw new System.InvalidOperationException("Imported character has no visible mesh bounds.");

            var wrapper = new GameObject("ManBaseCharacter");
            modelInstance.transform.SetParent(wrapper.transform, true);
            float fitScale = 1.75f / sourceHeight;
            modelInstance.transform.localScale = Vector3.one * fitScale;
            instanceRenderers = modelInstance.GetComponentsInChildren<Renderer>(true);
            combinedBounds = new Bounds(modelInstance.transform.position, Vector3.zero);
            foreach (var renderer in instanceRenderers)
                combinedBounds.Encapsulate(renderer.bounds);
            modelInstance.transform.position -= new Vector3(0f, combinedBounds.min.y, 0f);
            report.AppendLine("Prefab wrapper scale=" + fitScale + " mesh bounds after fit=" + combinedBounds.size +
                              " centered at floor via local Y=" + modelInstance.transform.localPosition.y);

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            var savedPrefab = PrefabUtility.SaveAsPrefabAsset(wrapper, PrefabPath);
            Object.DestroyImmediate(wrapper);
            Object.DestroyImmediate(modelInstance);
            if (savedPrefab == null)
                throw new System.InvalidOperationException("Could not save player visual prefab at " + PrefabPath);

            report.AppendLine("Playable visual prefab: " + PrefabPath);
            File.WriteAllText("/tmp/pm-man-character-inspection.txt", report.ToString());
            Debug.Log(report.ToString());
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
