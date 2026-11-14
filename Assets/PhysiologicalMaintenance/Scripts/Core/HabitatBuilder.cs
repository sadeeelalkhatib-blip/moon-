using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

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
            Material windowMaterial = MakeMaterial("Viewport polar glass", new Color(0.025f, 0.12f, 0.24f));
            Material earthMaterial = MakeMaterial("Earth oceans", new Color(0.07f, 0.32f, 0.72f));
            Material landMaterial = MakeMaterial("Earth continents", new Color(0.18f, 0.48f, 0.31f));

            Transform habitat = new GameObject("PM Habitat").transform;
            CreateBox("Deck", habitat, new Vector3(0f, -0.18f, 0f), new Vector3(20f, 0.35f, 13f), floorMaterial);
            BuildSafetyColliders(habitat);
            CreateBox("Rear bulkhead port", habitat, new Vector3(-6.3f, 2.4f, 6.35f), new Vector3(7.4f, 5f, 0.35f), wallMaterial);
            CreateBox("Rear bulkhead starboard", habitat, new Vector3(6.3f, 2.4f, 6.35f), new Vector3(7.4f, 5f, 0.35f), wallMaterial);
            CreateBox("Rear bulkhead below viewport", habitat, new Vector3(0f, 0.75f, 6.35f), new Vector3(5.2f, 1.7f, 0.35f), wallMaterial);
            CreateBox("Rear bulkhead above viewport", habitat, new Vector3(0f, 4.05f, 6.35f), new Vector3(5.2f, 1.7f, 0.35f), wallMaterial);
            BuildViewport(habitat, windowMaterial, earthMaterial, landMaterial);
            CreateBox("Port bulkhead", habitat, new Vector3(-10f, 2.4f, 0f), new Vector3(0.35f, 5f, 13f), wallMaterial);
            CreateBox("Starboard bulkhead", habitat, new Vector3(10f, 2.4f, 0f), new Vector3(0.35f, 5f, 13f), wallMaterial);
            CreateBox("Overhead beam", habitat, new Vector3(0f, 4.85f, 0f), new Vector3(20f, 0.2f, 13f), wallMaterial);
            CreateBox("Rear safety stripe", habitat, new Vector3(0f, 0.04f, 6.12f), new Vector3(19.6f, 0.12f, 0.08f), trimMaterial);

            for (int i = -4; i <= 4; i++)
                CreateBox("Deck guide " + i, habitat, new Vector3(i * 1.9f, 0.008f, -0.2f), new Vector3(0.035f, 0.015f, 11.6f), trimMaterial);

            BuildBulkheadDetails(habitat);
            CreateStation(habitat, controller, "ARED RESISTANCE", ExerciseMode.Resistance,
                new Vector3(-5.4f, 0f, 1.5f), equipmentMaterial, accentMaterial, ExerciseType.Squat);
            CreateStation(habitat, controller, "ARED ROW", ExerciseMode.Resistance,
                new Vector3(-5.4f, 0f, -2.8f), equipmentMaterial, accentMaterial, ExerciseType.Row);
            CreateStation(habitat, controller, "CYCLE ERGOMETER", ExerciseMode.CycleErgometer,
                new Vector3(-0.2f, 0f, 1.5f), equipmentMaterial, trimMaterial, ExerciseType.Cycling);
            CreateLifeSupportStation(habitat, "OXYGEN REGULATION", new Vector3(4.5f, 0f, 2.1f), accentMaterial);
            CreateLifeSupportStation(habitat, "THERMAL CONTROL", new Vector3(6.6f, 0f, -1.6f), trimMaterial);
            CreateLifeSupportStation(habitat, "WASTE PROCESSOR", new Vector3(4.4f, 0f, -4.7f), accentMaterial);
            CreateBox("Oxygen manifold", habitat, new Vector3(8.7f, 1.4f, 2.05f), new Vector3(1.7f, 2.3f, 0.9f), equipmentMaterial);
            CreateBox("Cooling duct", habitat, new Vector3(8.7f, 1.4f, -1.6f), new Vector3(1.7f, 2.3f, 0.9f), equipmentMaterial);
            CreateBox("Waste lock", habitat, new Vector3(8.7f, 1.4f, -4.7f), new Vector3(1.7f, 2.3f, 0.9f), equipmentMaterial);
            CreateBox("Monitor strip", habitat, new Vector3(0f, 3.45f, 6.05f), new Vector3(7.5f, 0.08f, 0.06f), accentMaterial);

            if (camera != null)
            {
                camera.transform.position = new Vector3(0f, 3.6f, -14.5f);
                camera.transform.LookAt(new Vector3(0f, 2.2f, 1.4f));
                camera.fieldOfView = 58f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
            }
        }

        private static void BuildSafetyColliders(Transform habitat)
        {
            AddBoundaryCollider("Port invisible safety boundary", habitat,
                new Vector3(-9.65f, 2.45f, 0f), new Vector3(0.5f, 5.3f, 13f));
            AddBoundaryCollider("Starboard invisible safety boundary", habitat,
                new Vector3(9.65f, 2.45f, 0f), new Vector3(0.5f, 5.3f, 13f));
            AddBoundaryCollider("Forward invisible safety boundary", habitat,
                new Vector3(0f, 2.45f, -6.2f), new Vector3(20f, 5.3f, 0.5f));
            AddBoundaryCollider("Aft invisible safety boundary", habitat,
                new Vector3(0f, 2.45f, 6.2f), new Vector3(20f, 5.3f, 0.5f));
            AddBoundaryCollider("Ceiling safety collider", habitat,
                new Vector3(0f, 5.15f, 0f), new Vector3(20f, 0.4f, 13f));
        }

        private static void AddBoundaryCollider(string name, Transform parent, Vector3 position, Vector3 size)
        {
            var boundary = new GameObject(name);
            boundary.transform.SetParent(parent, false);
            boundary.transform.localPosition = position;
            var collider = boundary.AddComponent<BoxCollider>();
            collider.size = size;
        }

        private static void BuildViewport(Transform habitat, Material glass, Material earth, Material land)
        {
            CreateBox("Viewport glass", habitat, new Vector3(0f, 2.75f, 6.12f), new Vector3(5.05f, 2.05f, 0.06f), glass);
            CreateBox("Viewport frame top", habitat, new Vector3(0f, 3.82f, 6.05f), new Vector3(5.45f, 0.12f, 0.16f), equipmentMaterial);
            CreateBox("Viewport frame bottom", habitat, new Vector3(0f, 1.68f, 6.05f), new Vector3(5.45f, 0.12f, 0.16f), equipmentMaterial);
            CreateBox("Viewport frame port", habitat, new Vector3(-2.66f, 2.75f, 6.05f), new Vector3(0.12f, 2.25f, 0.16f), equipmentMaterial);
            CreateBox("Viewport frame starboard", habitat, new Vector3(2.66f, 2.75f, 6.05f), new Vector3(0.12f, 2.25f, 0.16f), equipmentMaterial);
            CreateBox("Viewport center brace", habitat, new Vector3(0f, 2.75f, 6.04f), new Vector3(0.055f, 2.05f, 0.12f), trimMaterial);

            GameObject earthBody = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            earthBody.name = "Earth beyond viewport";
            earthBody.transform.SetParent(habitat, false);
            earthBody.transform.position = new Vector3(1.05f, 3.05f, 15f);
            earthBody.transform.localScale = new Vector3(4.2f, 4.2f, 0.8f);
            earthBody.GetComponent<Renderer>().sharedMaterial = earth;
            Object.Destroy(earthBody.GetComponent<Collider>());

            CreateSphere("Earth landmass one", habitat, new Vector3(0.2f, 3.65f, 14.56f), new Vector3(1.15f, 0.6f, 0.12f), land);
            CreateSphere("Earth landmass two", habitat, new Vector3(1.75f, 2.65f, 14.58f), new Vector3(0.8f, 0.45f, 0.1f), land);
            var random = new System.Random(2026);
            for (int i = 0; i < 36; i++)
            {
                float x = (float)(random.NextDouble() * 12.5 - 6.25);
                float y = (float)(random.NextDouble() * 5.2 + 0.15);
                CreateSphere("Distant star " + i, habitat, new Vector3(x, y, 18f),
                    Vector3.one * (float)(random.NextDouble() * 0.035 + 0.018), trimMaterial);
            }
        }

        private static void BuildBulkheadDetails(Transform habitat)
        {
            for (int i = -4; i <= 4; i++)
            {
                float x = i * 2f;
                if (Mathf.Abs(x) < 3f)
                    continue;
                CreateBox("Rear bulkhead structural rib", habitat, new Vector3(x, 2.35f, 6.12f),
                    new Vector3(0.09f, 4.65f, 0.12f), equipmentMaterial);
            }

            for (int i = -4; i <= 4; i++)
            {
                float z = i * 1.4f;
                CreateBox("Overhead service panel", habitat, new Vector3(i * 2.05f, 4.72f, z),
                    new Vector3(1.88f, 0.07f, 1.24f), i % 2 == 0 ? wallMaterial : equipmentMaterial);
                CreateBox("Overhead light diffuser", habitat, new Vector3(i * 2.05f, 4.66f, z),
                    new Vector3(0.72f, 0.035f, 0.16f), trimMaterial);
            }

            CreateBox("Pressure door frame", habitat, new Vector3(-9.72f, 1.45f, -3.1f),
                new Vector3(0.12f, 2.95f, 2.35f), equipmentMaterial);
            CreateBox("Sealed sliding pressure door", habitat, new Vector3(-9.62f, 1.45f, -3.1f),
                new Vector3(0.16f, 2.65f, 2.05f), wallMaterial);
            CreateBox("Pressure door status light", habitat, new Vector3(-9.5f, 2.75f, -3.1f),
                new Vector3(0.04f, 0.08f, 0.9f), trimMaterial);

            for (int i = 0; i < 5; i++)
            {
                float z = 4f - i * 1.55f;
                CreateBox("Port ventilation slot", habitat, new Vector3(-9.78f, 3.65f, z),
                    new Vector3(0.035f, 0.09f, 0.92f), equipmentMaterial);
            }
            CreateBox("Stowage locker upper", habitat, new Vector3(8.35f, 3.65f, 4.45f),
                new Vector3(2.25f, 1.2f, 1.25f), equipmentMaterial);
            CreateBox("Stowage locker lower", habitat, new Vector3(8.35f, 2.25f, 4.45f),
                new Vector3(2.25f, 1.45f, 1.25f), wallMaterial);
            CreateBox("Locker latch line", habitat, new Vector3(7.2f, 2.35f, 3.8f),
                new Vector3(0.035f, 0.8f, 0.05f), accentMaterial);

            CreateBox("Resistance zone deck mat", habitat, new Vector3(-5.4f, 0.025f, 1.3f),
                new Vector3(4.2f, 0.025f, 4.2f), trimMaterial);
            CreateBox("Cycle zone deck mat", habitat, new Vector3(-0.2f, 0.027f, 1.3f),
                new Vector3(3.7f, 0.03f, 3.7f), accentMaterial);
        }

        private static GameObject CreateSphere(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.position = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            Object.Destroy(item.GetComponent<Collider>());
            return item;
        }

        private static void CreateStation(Transform parent, PhysiologicalMaintenanceController controller,
            string label, ExerciseMode mode, Vector3 position, Material body, Material accent, ExerciseType exerciseType)
        {
            var station = new GameObject(label);
            station.transform.SetParent(parent, false);
            station.transform.position = position;
            var collider = station.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.05f, 0f);
            collider.size = new Vector3(2.6f, 2.6f, 2.4f);
            EquipmentStation equipmentStation = station.AddComponent<EquipmentStation>();
            equipmentStation.Configure(mode, exerciseType, 2.2f);
            Material highlightMaterial = MakeMaterial(label + " highlight", new Color(0.1f, 0.9f, 1f));
            highlightMaterial.EnableKeyword("_EMISSION");
            highlightMaterial.SetColor("_EmissionColor", new Color(0.1f, 0.9f, 1f) * 1.8f);
            GameObject highlight = CreateCylinder("Approach highlight", station.transform,
                new Vector3(0f, 0.045f, 0f), new Vector3(1.7f, 0.025f, 1.7f), highlightMaterial);
            equipmentStation.SetHighlightRenderer(highlight.GetComponent<Renderer>(), highlightMaterial);
            highlight.GetComponent<Renderer>().enabled = false;

            if (mode == ExerciseMode.Resistance)
            {
                CreateBox("ARED left rail", station.transform, new Vector3(-0.85f, 1.35f, 0f), new Vector3(0.15f, 2.7f, 0.16f), body);
                CreateBox("ARED right rail", station.transform, new Vector3(0.85f, 1.35f, 0f), new Vector3(0.15f, 2.7f, 0.16f), body);
                CreateBox("ARED top bar", station.transform, new Vector3(0f, 2.65f, 0f), new Vector3(1.85f, 0.18f, 0.2f), accent);
                CreateBox("ARED load carriage", station.transform, new Vector3(0f, 1.75f, -0.12f), new Vector3(1.15f, 0.5f, 0.5f), body);
                CreateBox("ARED foot plate", station.transform, new Vector3(0f, 0.14f, 0.45f), new Vector3(1.9f, 0.18f, 1.15f), body);
                if (exerciseType == ExerciseType.Row)
                {
                    CreateBox("ARED row handle", station.transform, new Vector3(0f, 1.8f, 0.7f), new Vector3(1.2f, 0.12f, 0.12f), accent);
                }
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
        }

        private static void CreateLifeSupportStation(Transform parent, string label, Vector3 position, Material accent)
        {
            CreateBox(label + " console", parent, position + new Vector3(0f, 1.2f, 0f), new Vector3(1.8f, 2.3f, 0.32f), equipmentMaterial);
            CreateBox(label + " display", parent, position + new Vector3(0f, 1.55f, -0.19f), new Vector3(1.35f, 0.72f, 0.06f), accent);
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

        private static GameObject CreateCylinder(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = material;
            if (parent.GetComponent<EquipmentStation>() != null)
                Object.Destroy(item.GetComponent<Collider>());
            return item;
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
