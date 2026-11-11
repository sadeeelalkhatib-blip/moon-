using UnityEngine;

namespace PhysiologicalMaintenance
{
    public static class HabitatBuilder
    {
        private static Material wallMaterial;
        private static Material floorMaterial;
        private static Material trimMaterial;
        private static Material accentMaterial;
        private static Material equipmentMaterial;

        public static void Build(PhysiologicalMaintenanceController controller, Camera camera)
        {
            if (GameObject.Find("PM Habitat") != null)
                return;

            wallMaterial = MakeMaterial("Habitat ceramic", new Color(0.16f, 0.22f, 0.28f));
            floorMaterial = MakeMaterial("Deck graphite", new Color(0.075f, 0.1f, 0.13f));
            trimMaterial = MakeMaterial("Safety trim", new Color(0.08f, 0.68f, 0.72f));
            accentMaterial = MakeMaterial("Panel amber", new Color(1f, 0.58f, 0.13f));
            equipmentMaterial = MakeMaterial("Equipment alloy", new Color(0.48f, 0.57f, 0.63f));

            Transform habitat = new GameObject("PM Habitat").transform;
            CreateBox("Deck", habitat, new Vector3(0f, -0.18f, 0f), new Vector3(20f, 0.35f, 13f), floorMaterial);
            CreateBox("Rear bulkhead", habitat, new Vector3(0f, 2.4f, 6.35f), new Vector3(20f, 5f, 0.35f), wallMaterial);
            CreateBox("Port bulkhead", habitat, new Vector3(-10f, 2.4f, 0f), new Vector3(0.35f, 5f, 13f), wallMaterial);
            CreateBox("Starboard bulkhead", habitat, new Vector3(10f, 2.4f, 0f), new Vector3(0.35f, 5f, 13f), wallMaterial);
            CreateBox("Overhead beam", habitat, new Vector3(0f, 4.85f, 0f), new Vector3(20f, 0.2f, 13f), wallMaterial);
            CreateBox("Rear safety stripe", habitat, new Vector3(0f, 0.04f, 6.12f), new Vector3(19.6f, 0.12f, 0.08f), trimMaterial);

            for (int i = -4; i <= 4; i++)
                CreateBox("Deck guide " + i, habitat, new Vector3(i * 1.9f, 0.008f, -0.2f), new Vector3(0.035f, 0.015f, 11.6f), trimMaterial);

            CreateStation(habitat, controller, "ARED RESISTANCE", ExerciseMode.Resistance,
                new Vector3(-5.4f, 0f, 1.5f), equipmentMaterial, accentMaterial);
            CreateStation(habitat, controller, "CYCLE ERGOMETER", ExerciseMode.CycleErgometer,
                new Vector3(-0.2f, 0f, 1.5f), equipmentMaterial, trimMaterial);
            CreateLifeSupportStation(habitat, "OXYGEN REGULATION", new Vector3(4.5f, 0f, 2.1f), accentMaterial);
            CreateLifeSupportStation(habitat, "THERMAL CONTROL", new Vector3(6.6f, 0f, -1.6f), trimMaterial);
            CreateLifeSupportStation(habitat, "WASTE PROCESSOR", new Vector3(4.4f, 0f, -4.7f), accentMaterial);
            CreateLabel("PHYSIOLOGICAL MAINTENANCE // HABITAT MODULE", new Vector3(0f, 4.2f, 6.12f), 0.38f, trimMaterial.color);
            CreateBox("Oxygen manifold", habitat, new Vector3(8.7f, 1.4f, 2.05f), new Vector3(1.7f, 2.3f, 0.9f), equipmentMaterial);
            CreateBox("Cooling duct", habitat, new Vector3(8.7f, 1.4f, -1.6f), new Vector3(1.7f, 2.3f, 0.9f), equipmentMaterial);
            CreateBox("Waste lock", habitat, new Vector3(8.7f, 1.4f, -4.7f), new Vector3(1.7f, 2.3f, 0.9f), equipmentMaterial);
            CreateBox("Monitor strip", habitat, new Vector3(0f, 3.45f, 6.05f), new Vector3(7.5f, 0.08f, 0.06f), accentMaterial);

            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 8.3f, -14.5f);
                camera.transform.LookAt(new Vector3(0f, 1.5f, 1.4f));
                camera.fieldOfView = 58f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
            }
        }

        private static void CreateStation(Transform parent, PhysiologicalMaintenanceController controller,
            string label, ExerciseMode mode, Vector3 position, Material body, Material accent)
        {
            var station = new GameObject(label);
            station.transform.SetParent(parent, false);
            station.transform.position = position;
            var collider = station.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.05f, 0f);
            collider.size = new Vector3(2.6f, 2.6f, 2.4f);
            station.AddComponent<EquipmentStation>().Configure(mode);

            if (mode == ExerciseMode.Resistance)
            {
                CreateBox("ARED left rail", station.transform, new Vector3(-0.85f, 1.35f, 0f), new Vector3(0.15f, 2.7f, 0.16f), body);
                CreateBox("ARED right rail", station.transform, new Vector3(0.85f, 1.35f, 0f), new Vector3(0.15f, 2.7f, 0.16f), body);
                CreateBox("ARED top bar", station.transform, new Vector3(0f, 2.65f, 0f), new Vector3(1.85f, 0.18f, 0.2f), accent);
                CreateBox("ARED load carriage", station.transform, new Vector3(0f, 1.75f, -0.12f), new Vector3(1.15f, 0.5f, 0.5f), body);
                CreateBox("ARED foot plate", station.transform, new Vector3(0f, 0.14f, 0.45f), new Vector3(1.9f, 0.18f, 1.15f), body);
            }
            else
            {
                CreateCylinder("Cycle flywheel", station.transform, new Vector3(0f, 0.92f, 0.05f), new Vector3(0.16f, 0.62f, 0.62f), accent);
                CreateBox("Cycle frame", station.transform, new Vector3(0f, 0.72f, 0f), new Vector3(0.13f, 1.45f, 0.13f), body);
                CreateBox("Cycle base", station.transform, new Vector3(0f, 0.12f, 0f), new Vector3(1.8f, 0.16f, 0.8f), body);
                CreateBox("Cycle seat", station.transform, new Vector3(0f, 1.62f, -0.42f), new Vector3(0.7f, 0.15f, 0.48f), body);
                CreateBox("Cycle handle", station.transform, new Vector3(0f, 1.65f, 0.48f), new Vector3(0.9f, 0.12f, 0.2f), body);
                CreateBox("Cycle console", station.transform, new Vector3(0f, 2.05f, 0.48f), new Vector3(0.65f, 0.42f, 0.12f), accent);
            }
            CreateLabel(label + "\nCLICK OR SELECT IN CONSOLE", position + new Vector3(0f, 2.95f, 0f), 0.2f, accent.color);
        }

        private static void CreateLifeSupportStation(Transform parent, string label, Vector3 position, Material accent)
        {
            CreateBox(label + " console", parent, position + new Vector3(0f, 1.2f, 0f), new Vector3(1.8f, 2.3f, 0.32f), equipmentMaterial);
            CreateBox(label + " display", parent, position + new Vector3(0f, 1.55f, -0.19f), new Vector3(1.35f, 0.72f, 0.06f), accent);
            CreateLabel(label, position + new Vector3(0f, 2.55f, 0f), 0.18f, accent.color);
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            var collider = item.GetComponent<Collider>();
            if (collider != null && parent != null && parent.GetComponent<EquipmentStation>() != null)
                Object.Destroy(collider);
            return item;
        }

        private static void CreateCylinder(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (parent.GetComponent<EquipmentStation>() != null)
                Object.Destroy(item.GetComponent<Collider>());
        }

        private static void CreateLabel(string text, Vector3 position, float size, Color color)
        {
            var label = new GameObject(text.Replace('\n', ' '));
            label.transform.position = position;
            label.transform.rotation = Quaternion.identity;
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.characterSize = size;
            mesh.fontSize = 48;
            mesh.color = color;
        }

        private static Material MakeMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Standard");
            var material = new Material(shader) { name = name, color = color };
            return material;
        }
    }
}
