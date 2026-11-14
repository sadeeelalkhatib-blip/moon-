using System;
using System.Collections.Generic;
using UnityEngine;

namespace GatheringSamples
{
    public class WorldBuilder : MonoBehaviour
    {
        private struct Crater
        {
            public float x;
            public float z;
            public float r;
            public float d;
            public float rim;

            public Crater(float x, float z, float r, float d, float rim)
            {
                this.x = x;
                this.z = z;
                this.r = r;
                this.d = d;
                this.rim = rim;
            }
        }

        private struct Flat
        {
            public float x;
            public float z;
            public float r;

            public Flat(float x, float z, float r)
            {
                this.x = x;
                this.z = z;
                this.r = r;
            }
        }

        private static readonly Crater[] Craters =
        {
            new Crater(38f, 6f, 9f, 1.9f, 0.55f),
            new Crater(-41f, -27f, 12f, 2.6f, 0.7f),
            new Crater(6f, 37f, 6.5f, 1.2f, 0.35f),
            new Crater(-9f, 31f, 4.2f, 0.8f, 0.22f),
            new Crater(45f, 44f, 10f, 2.1f, 0.6f),
            new Crater(-39f, 43f, 8f, 1.6f, 0.45f),
            new Crater(2f, -64f, 15f, 3.2f, 0.8f),
            new Crater(65f, -28f, 11f, 2.3f, 0.55f),
            new Crater(-67f, 2f, 9f, 1.7f, 0.4f),
            new Crater(24f, 60f, 7f, 1.4f, 0.35f)
        };

        private static Flat[] flats;
        private static Texture2D regolithTexture;
        private static Texture2D rockTexture;

        public Transform Root { get; private set; }
        public Transform Lander { get; private set; }
        public Transform Earth { get; private set; }
        public ParticleSystem Dust { get; private set; }
        public readonly Dictionary<string, StationView> Stations = new Dictionary<string, StationView>();

        private Material terrainMaterial;
        private Material rockMaterial;
        private Material goldMaterial;
        private Material whiteMaterial;
        private Material darkMaterial;
        private Material panelMaterial;
        private Material samplePadMaterial;
        private Light landerBeaconLight;
        private float elapsed;
        private int dustEmissionRemainder;

        public static float TerrainHeight(float x, float z)
        {
            float height = BaseTerrainHeight(x, z);
            Flat[] flatAreas = Flats();
            for (int i = 0; i < flatAreas.Length; i++)
            {
                Flat flat = flatAreas[i];
                float dx = x - flat.x;
                float dz = z - flat.z;
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                float t = Mathf.Exp(-Mathf.Pow(distance / flat.r, 2f) * 1.7f);
                height = Mathf.Lerp(height, BaseTerrainHeight(flat.x, flat.z), t);
            }
            return height;
        }

        private static Flat[] Flats()
        {
            if (flats != null) return flats;

            List<Flat> list = new List<Flat> { new Flat(0f, 0f, 9f) };
            for (int i = 0; i < Content.Sites.Length; i++)
            {
                SiteDef site = Content.Sites[i];
                list.Add(new Flat(site.position.x, site.position.y, 5.2f));
            }
            flats = list.ToArray();
            return flats;
        }

        private static float BaseTerrainHeight(float x, float z)
        {
            float height =
                Mathf.Sin(x * 0.055f) * 0.85f +
                Mathf.Cos(z * 0.047f) * 0.75f +
                Mathf.Sin((x + z) * 0.021f) * 1.15f +
                Mathf.Sin(x * 0.13f - z * 0.1f) * 0.32f +
                (Fbm(x * 0.055f + 11f, z * 0.055f - 7f) - 0.5f) * 2.4f;

            for (int i = 0; i < Craters.Length; i++)
            {
                Crater crater = Craters[i];
                float dx = x - crater.x;
                float dz = z - crater.z;
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                if (distance >= crater.r * 1.9f) continue;

                float bowl = Mathf.Exp(-Mathf.Pow(distance / (crater.r * 0.72f), 2f) * 2.1f);
                float rim = Mathf.Exp(-Mathf.Pow((distance - crater.r * 0.92f) / (crater.r * 0.2f), 2f));
                height += -crater.d * bowl + crater.rim * rim;
            }

            return height;
        }

        private static float Fbm(float x, float z)
        {
            float value = 0f;
            float amplitude = 1f;
            float frequency = 1f;
            float total = 0f;

            for (int i = 0; i < 4; i++)
            {
                value += Mathf.PerlinNoise(x * frequency, z * frequency) * amplitude;
                total += amplitude;
                amplitude *= 0.5f;
                frequency *= 2.05f;
            }

            return value / total;
        }

        public Vector3 StationPosition(SiteDef site)
        {
            return new Vector3(site.position.x, TerrainHeight(site.position.x, site.position.y), site.position.y);
        }

        public void Build()
        {
            Root = new GameObject("World").transform;
            Root.SetParent(transform, false);
            Physics.gravity = new Vector3(0f, -Sim.Gravity, 0f);
            QualitySettings.shadowDistance = 70f;

            BuildMaterials();
            BuildLighting();
            BuildTerrain();
            BuildRocks();
            BuildLander();
            BuildBoundary();
            BuildSky();
            BuildStations();
            BuildDust();
        }

        private void BuildMaterials()
        {
            regolithTexture = RuntimeAssets.CreateNoiseTexture(512, 0.78f, 0.24f, 1337);
            rockTexture = RuntimeAssets.CreateNoiseTexture(256, 0.56f, 0.3f, 99);

            terrainMaterial = RuntimeAssets.CreateLit(
                "terrain", Color.white, 0f, 0.07f, regolithTexture, new Vector2(14f, 14f));
            rockMaterial = RuntimeAssets.CreateLit(
                "rock", new Color(0.56f, 0.54f, 0.5f), 0f, 0.08f, rockTexture, new Vector2(2f, 2f));
            goldMaterial = RuntimeAssets.CreateLit("lander-gold", new Color(0.82f, 0.6f, 0.2f), 0.9f, 0.55f);
            whiteMaterial = RuntimeAssets.CreateLit("lander-white", new Color(0.82f, 0.82f, 0.79f), 0.1f, 0.32f);
            darkMaterial = RuntimeAssets.CreateLit("lander-dark", new Color(0.16f, 0.17f, 0.18f), 0.7f, 0.36f);
            panelMaterial = RuntimeAssets.CreateLit("lander-panel", new Color(0.06f, 0.12f, 0.3f), 0.65f, 0.62f);
            samplePadMaterial = RuntimeAssets.CreateLit("sample-pad", new Color(0.2f, 0.2f, 0.2f), 0f, 0.08f);
        }

        private void BuildLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.17f, 0.19f, 0.23f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0022f;
            RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.045f);
            RenderSettings.skybox = null;

            GameObject sunObject = new GameObject("Sun");
            sunObject.transform.SetParent(Root, false);
            sunObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            Light sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.9f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.68f;
            RenderSettings.sun = sun;

            GameObject fillObject = new GameObject("Earthshine Fill");
            fillObject.transform.SetParent(Root, false);
            fillObject.transform.rotation = Quaternion.Euler(18f, 152f, 0f);
            Light fill = fillObject.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.36f, 0.48f, 0.68f);
            fill.intensity = 0.18f;
            fill.shadows = LightShadows.None;
        }

        private void BuildTerrain()
        {
            int segments = Sim.TerrainSegments;
            int vertexCount = (segments + 1) * (segments + 1);
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[segments * segments * 6];
            float half = Sim.TerrainSize * 0.5f;
            float step = Sim.TerrainSize / segments;

            for (int z = 0; z <= segments; z++)
            {
                for (int x = 0; x <= segments; x++)
                {
                    int index = z * (segments + 1) + x;
                    float worldX = -half + x * step;
                    float worldZ = -half + z * step;
                    vertices[index] = new Vector3(worldX, TerrainHeight(worldX, worldZ), worldZ);
                    uvs[index] = new Vector2((float)x / segments * 14f, (float)z / segments * 14f);
                }
            }

            int triangle = 0;
            for (int z = 0; z < segments; z++)
            {
                for (int x = 0; x < segments; x++)
                {
                    int index = z * (segments + 1) + x;
                    int next = index + segments + 1;
                    triangles[triangle++] = index;
                    triangles[triangle++] = next;
                    triangles[triangle++] = index + 1;
                    triangles[triangle++] = index + 1;
                    triangles[triangle++] = next;
                    triangles[triangle++] = next + 1;
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "Lunar Terrain";
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject terrain = new GameObject("Lunar Terrain");
            terrain.transform.SetParent(Root, false);
            MeshFilter filter = terrain.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = terrain.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = terrainMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            MeshCollider collider = terrain.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        private void BuildRocks()
        {
            Mesh[] meshes =
            {
                RuntimeAssets.CreateRockMesh("rock-a", 17),
                RuntimeAssets.CreateRockMesh("rock-b", 41),
                RuntimeAssets.CreateRockMesh("rock-c", 73),
                RuntimeAssets.CreateRockMesh("rock-d", 109)
            };

            System.Random random = new System.Random(2026);

            for (int i = 0; i < 150; i++)
            {
                float x = ((float)random.NextDouble() * 2f - 1f) * 115f;
                float z = ((float)random.NextDouble() * 2f - 1f) * 115f;
                if (Mathf.Sqrt(x * x + z * z) > 120f) continue;
                if (Mathf.Sqrt(x * x + z * z) < 7f) continue;
                if (NearStation(x, z, 3f)) continue;

                float scale = 0.25f + (float)random.NextDouble() * 1.25f;
                Mesh mesh = meshes[random.Next(meshes.Length)];
                float y = TerrainHeight(x, z) + scale * 0.34f;
                GameObject rock = RuntimeAssets.CreateMeshObject(
                    mesh, Root, "Rock", new Vector3(x, y, z),
                    new Vector3(scale, scale * 0.72f, scale), rockMaterial, scale > 0.75f);
                rock.transform.rotation = Quaternion.Euler(
                    (float)random.NextDouble() * 360f,
                    (float)random.NextDouble() * 360f,
                    (float)random.NextDouble() * 360f);
            }

            for (int i = 0; i < 120; i++)
            {
                float x = ((float)random.NextDouble() * 2f - 1f) * 100f;
                float z = ((float)random.NextDouble() * 2f - 1f) * 100f;
                if (Mathf.Sqrt(x * x + z * z) < 6f) continue;
                if (NearStation(x, z, 2.2f)) continue;

                float scale = 0.05f + (float)random.NextDouble() * 0.18f;
                Mesh mesh = meshes[random.Next(meshes.Length)];
                float y = TerrainHeight(x, z) + scale * 0.28f;
                RuntimeAssets.CreateMeshObject(
                    mesh, Root, "Pebble", new Vector3(x, y, z),
                    new Vector3(scale, scale * 0.6f, scale), rockMaterial, false);
            }
        }

        private static bool NearStation(float x, float z, float radius)
        {
            for (int i = 0; i < Content.Sites.Length; i++)
            {
                Vector2 position = Content.Sites[i].position;
                float dx = x - position.x;
                float dz = z - position.y;
                if (dx * dx + dz * dz < radius * radius) return true;
            }
            return false;
        }

        private void BuildLander()
        {
            Lander = new GameObject("LUNA-1 Lander").transform;
            Lander.SetParent(Root, false);
            Lander.position = new Vector3(0f, TerrainHeight(0f, 0f), 0f);

            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Cylinder, Lander, "Landing Base",
                new Vector3(0f, 0.16f, 0f), new Vector3(5.2f, 0.16f, 5.2f), darkMaterial);
            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Cylinder, Lander, "Descent Stage",
                new Vector3(0f, 0.72f, 0f), new Vector3(3.4f, 0.46f, 3.4f), darkMaterial);
            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Cube, Lander, "Crew Module",
                new Vector3(0f, 1.7f, 0f), new Vector3(2.4f, 1.4f, 2.4f), goldMaterial);
            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Cylinder, Lander, "Ascent Stage",
                new Vector3(0f, 2.9f, 0f), new Vector3(2.5f, 0.5f, 2.5f), whiteMaterial);
            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Cylinder, Lander, "Top Cap",
                new Vector3(0f, 3.52f, 0f), new Vector3(1.6f, 0.12f, 1.6f), darkMaterial);

            for (int i = 0; i < 4; i++)
            {
                float sx = i < 2 ? -1f : 1f;
                float sz = i % 2 == 0 ? -1f : 1f;
                Vector3 top = new Vector3(sx * 1.2f, 1.15f, sz * 1.2f);
                Vector3 foot = new Vector3(sx * 2.45f, 0.06f, sz * 2.45f);
                Vector3 direction = foot - top;
                Vector3 middle = (top + foot) * 0.5f;
                GameObject leg = RuntimeAssets.CreatePrimitive(
                    PrimitiveType.Cylinder, Lander, "Landing Leg", middle,
                    new Vector3(0.11f, direction.magnitude * 0.5f, 0.11f), darkMaterial);
                leg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
                RuntimeAssets.CreatePrimitive(
                    PrimitiveType.Cylinder, Lander, "Foot Pad",
                    new Vector3(foot.x, 0.06f, foot.z), new Vector3(0.62f, 0.06f, 0.62f), whiteMaterial);
            }

            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Cylinder, Lander, "Antenna Mast",
                new Vector3(1.15f, 4.2f, 1.15f), new Vector3(0.06f, 0.7f, 0.06f), darkMaterial);
            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Sphere, Lander, "Antenna Dish",
                new Vector3(1.15f, 4.92f, 1.15f), new Vector3(0.72f, 0.2f, 0.72f), whiteMaterial);

            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Cube, Lander, "Solar Panel Left",
                new Vector3(-2.2f, 1.25f, 0f), new Vector3(2.1f, 0.06f, 1.15f), panelMaterial);
            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Cube, Lander, "Solar Panel Right",
                new Vector3(2.2f, 1.25f, 0f), new Vector3(2.1f, 0.06f, 1.15f), panelMaterial);

            for (int i = 0; i < 5; i++)
            {
                RuntimeAssets.CreatePrimitive(
                    PrimitiveType.Cube, Lander, "Ladder Rung",
                    new Vector3(0f, 0.55f + i * 0.36f, -1.24f),
                    new Vector3(0.8f, 0.05f, 0.06f), whiteMaterial);
            }

            Material beaconMaterial = RuntimeAssets.CreateUnlit("lander-beacon", new Color(1f, 0.72f, 0.28f));
            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Sphere, Lander, "Beacon",
                new Vector3(0f, 3.85f, 0f), new Vector3(0.2f, 0.2f, 0.2f), beaconMaterial);

            GameObject lightObject = new GameObject("Beacon Light");
            lightObject.transform.SetParent(Lander, false);
            lightObject.transform.localPosition = new Vector3(0f, 3.85f, 0f);
            landerBeaconLight = lightObject.AddComponent<Light>();
            landerBeaconLight.type = LightType.Point;
            landerBeaconLight.color = new Color(1f, 0.72f, 0.28f);
            landerBeaconLight.range = 8f;
            landerBeaconLight.intensity = 1.4f;
            landerBeaconLight.shadows = LightShadows.None;

            WorldLabel.Create(
                Lander, "Lander Label", new Vector3(0f, 5.6f, 0f),
                "LUNA-1 · LANDER", new Color(0.5f, 0.85f, 1f));

            CreateLocalRing(
                Lander, "Lander Pad Ring", 5f, new Color(0.5f, 0.85f, 1f),
                0.05f, 0.06f, 96, RuntimeAssets.CreateUnlit("lander-pad-ring", new Color(0.5f, 0.85f, 1f)));
        }

        private void BuildBoundary()
        {
            CreateWorldCircle(
                "Mission Boundary", Sim.BoundaryRadius, new Color(1f, 0.55f, 0.2f),
                0.1f, 0.16f, 160);

            CreateWorldCircle(
                "Soft Boundary", Sim.SoftBoundaryRadius, new Color(0.4f, 0.8f, 1f),
                0.055f, 0.13f, 140);

            Material postMaterial = RuntimeAssets.CreateLit("boundary-post", new Color(0.2f, 0.21f, 0.22f), 0.6f, 0.3f);
            Material capMaterial = RuntimeAssets.CreateUnlit("boundary-cap", new Color(1f, 0.55f, 0.2f));

            for (int i = 0; i < 12; i++)
            {
                float angle = (float)i / 12f * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * Sim.BoundaryRadius;
                float z = Mathf.Sin(angle) * Sim.BoundaryRadius;
                float y = TerrainHeight(x, z);
                RuntimeAssets.CreatePrimitive(
                    PrimitiveType.Cylinder, Root, "Boundary Post",
                    new Vector3(x, y + 0.48f, z), new Vector3(0.08f, 0.48f, 0.08f), postMaterial);
                RuntimeAssets.CreatePrimitive(
                    PrimitiveType.Sphere, Root, "Boundary Cap",
                    new Vector3(x, y + 1.02f, z), new Vector3(0.18f, 0.18f, 0.18f), capMaterial);
            }
        }

        private LineRenderer CreateWorldCircle(string name, float radius, Color color, float width, float yOffset, int segments)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(Root, false);
            LineRenderer line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = segments;
            line.startWidth = width;
            line.endWidth = width;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.material = RuntimeAssets.CreateUnlit("line-" + name, color);
            line.startColor = color;
            line.endColor = color;

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                line.SetPosition(i, new Vector3(x, TerrainHeight(x, z) + yOffset, z));
            }
            return line;
        }

        private LineRenderer CreateLocalRing(Transform parent, string name, float radius, Color color, float width, float yOffset, int segments, Material material)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            LineRenderer line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = segments;
            line.startWidth = width;
            line.endWidth = width;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.material = material;
            line.startColor = color;
            line.endColor = color;

            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, yOffset, Mathf.Sin(angle) * radius));
            }
            return line;
        }

        private void BuildSky()
        {
            Texture2D circle = RuntimeAssets.CreateCircleTexture(64, Color.white, new Color(1f, 1f, 1f, 0f));
            GameObject starObject = new GameObject("Starfield");
            starObject.transform.SetParent(Root, false);
            ParticleSystem stars = starObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = stars.main;
            main.loop = false;
            main.startLifetime = 99999f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3.4f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.75f, 0.82f, 1f, 1f),
                new Color(1f, 1f, 1f, 1f));
            main.maxParticles = 2200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            ParticleSystem.EmissionModule emission = stars.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = stars.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 480f;
            shape.radiusThickness = 0f;

            ParticleSystemRenderer starRenderer = starObject.GetComponent<ParticleSystemRenderer>();
            starRenderer.material = RuntimeAssets.CreateParticleMaterial("stars", circle, Color.white);
            starRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            starRenderer.alignment = ParticleSystemRenderSpace.View;
            starRenderer.sortMode = ParticleSystemSortMode.Distance;
            starRenderer.maxParticleSize = 0.6f;
            stars.Emit(1800);

            Texture2D earthTexture = Resources.Load<Texture2D>("Textures/earth_atmos_2048");
            Material earthMaterial = earthTexture != null
                ? RuntimeAssets.CreateUnlit("earth", Color.white, earthTexture)
                : RuntimeAssets.CreateLit("earth-fallback", new Color(0.18f, 0.38f, 0.68f), 0f, 0.3f);
            Material atmosphereMaterial = RuntimeAssets.CreateUnlit(
                "earth-atmosphere", new Color(0.25f, 0.52f, 1f, 0.12f), null, true);

            Vector3 earthDirection = new Vector3(0.55f, 0.3f, 0.78f).normalized;
            GameObject earth = RuntimeAssets.CreatePrimitive(
                PrimitiveType.Sphere, Root, "Earth", earthDirection * 330f,
                Vector3.one * 42f, earthMaterial);
            earth.transform.position = earthDirection * 330f;
            earth.transform.localScale = Vector3.one * 42f;
            Renderer earthRenderer = earth.GetComponent<Renderer>();
            if (earthRenderer != null) earthRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            GameObject atmosphere = RuntimeAssets.CreatePrimitive(
                PrimitiveType.Sphere, Root, "Earth Atmosphere", earthDirection * 330f,
                Vector3.one * 44f, atmosphereMaterial);
            atmosphere.transform.position = earthDirection * 330f;
            atmosphere.transform.localScale = Vector3.one * 44f;
            Renderer atmosphereRenderer = atmosphere.GetComponent<Renderer>();
            if (atmosphereRenderer != null) atmosphereRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Earth = earth.transform;
        }

        private void BuildStations()
        {
            Material poleMaterial = RuntimeAssets.CreateLit("station-pole", new Color(0.3f, 0.31f, 0.32f), 0.65f, 0.32f);

            for (int i = 0; i < Content.Sites.Length; i++)
            {
                SiteDef site = Content.Sites[i];
                GameObject group = new GameObject("Station " + site.id);
                group.transform.SetParent(Root, false);
                group.transform.position = StationPosition(site);

                Color pending = new Color(1f, 0.62f, 0.28f);
                Material beaconMaterial = RuntimeAssets.CreateUnlit("beacon-" + site.id, pending);
                Material ringMaterial = RuntimeAssets.CreateUnlit("station-ring-" + site.id, pending);
                Material beamMaterial = RuntimeAssets.CreateUnlit(
                    "station-beam-" + site.id, new Color(pending.r, pending.g, pending.b, 0.16f), null, true);

                Material sampleMaterial = RuntimeAssets.CreateLit(
                    "sample-" + site.id, site.color, 0f, 0.12f, rockTexture, new Vector2(1.4f, 1.4f));

                RuntimeAssets.CreatePrimitive(
                    PrimitiveType.Cylinder, group.transform, "Sample Pad",
                    new Vector3(0f, 0.02f, 0f), new Vector3(3.2f, 0.02f, 3.2f), samplePadMaterial);

                BuildSample(site, group.transform, sampleMaterial);
                BuildBeacon(site, group.transform, poleMaterial, beaconMaterial, ringMaterial, beamMaterial);

                WorldLabel label = WorldLabel.Create(
                    group.transform, "Station Label", new Vector3(0f, 2.55f, 0f),
                    site.code + " · " + site.name, pending);

                StationView view = group.AddComponent<StationView>();
                view.Site = site;
                view.Ring = group.GetComponentInChildren<LineRenderer>();
                view.BeaconRenderer = group.transform.Find("Beacon Orb").GetComponent<Renderer>();
                view.BeaconLight = group.transform.Find("Beacon Light").GetComponent<Light>();
                view.Label = label;
                view.BeaconMaterial = beaconMaterial;
                view.RingMaterial = ringMaterial;
                view.BeamMaterial = beamMaterial;
                view.BaseLabel = site.code + " · " + site.name;
                view.PulsePhase = site.position.x * 0.11f + site.position.y * 0.13f;
                view.SetState("pending");
                Stations[site.id] = view;
            }
        }

        private void BuildSample(SiteDef site, Transform parent, Material sampleMaterial)
        {
            Mesh[] meshes =
            {
                RuntimeAssets.CreateRockMesh("rock-a", 17),
                RuntimeAssets.CreateRockMesh("rock-b", 41),
                RuntimeAssets.CreateRockMesh("rock-c", 73),
                RuntimeAssets.CreateRockMesh("rock-d", 109)
            };
            System.Random random = new System.Random(site.id.GetHashCode());

            if (site.type == "loose_regolith")
            {
                RuntimeAssets.CreatePrimitive(
                    PrimitiveType.Sphere, parent, "Regolith Mound",
                    new Vector3(0f, 0.14f, 0f), new Vector3(2.2f, 0.28f, 2.2f), sampleMaterial);
                for (int i = 0; i < 14; i++)
                {
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    float distance = 0.35f + (float)random.NextDouble() * 0.9f;
                    float scale = 0.08f + (float)random.NextDouble() * 0.13f;
                    Vector3 position = new Vector3(Mathf.Cos(angle) * distance, scale * 0.3f, Mathf.Sin(angle) * distance);
                    RuntimeAssets.CreateMeshObject(
                        meshes[random.Next(meshes.Length)], parent, "Regolith Clast",
                        position, new Vector3(scale, scale * 0.7f, scale), rockMaterial, false);
                }
            }
            else if (site.type == "individual_rock")
            {
                RuntimeAssets.CreateMeshObject(
                    meshes[random.Next(meshes.Length)], parent, "Float Rock",
                    new Vector3(0f, 0.58f, 0f), new Vector3(1.1f, 1.1f, 1.1f), sampleMaterial, true);
            }
            else if (site.type == "outcrop")
            {
                RuntimeAssets.CreateMeshObject(
                    meshes[random.Next(meshes.Length)], parent, "Outcrop Core",
                    new Vector3(0f, 1.35f, 0f), new Vector3(2.2f, 2.7f, 1.8f), sampleMaterial, true);
                RuntimeAssets.CreateMeshObject(
                    meshes[random.Next(meshes.Length)], parent, "Outcrop Shoulder",
                    new Vector3(0.9f, 0.72f, 0.45f), new Vector3(1.15f, 1.3f, 1.0f), sampleMaterial, true);
                RuntimeAssets.CreateMeshObject(
                    meshes[random.Next(meshes.Length)], parent, "Outcrop Toe",
                    new Vector3(-0.8f, 0.55f, -0.35f), new Vector3(0.95f, 1.0f, 0.85f), sampleMaterial, true);
            }
            else if (site.type == "stratified_soil")
            {
                Material layerOne = RuntimeAssets.CreateLit("stratum-1", new Color(0.37f, 0.32f, 0.27f), 0f, 0.08f);
                Material layerTwo = RuntimeAssets.CreateLit("stratum-2", new Color(0.51f, 0.45f, 0.37f), 0f, 0.1f);
                Material layerThree = RuntimeAssets.CreateLit("stratum-3", new Color(0.63f, 0.57f, 0.48f), 0f, 0.12f);
                RuntimeAssets.CreatePrimitive(
                    PrimitiveType.Cube, parent, "Stratum Lower",
                    new Vector3(0f, 0.18f, 0f), new Vector3(2f, 0.36f, 1f), layerOne);
                RuntimeAssets.CreatePrimitive(
                    PrimitiveType.Cube, parent, "Stratum Middle",
                    new Vector3(0f, 0.54f, 0f), new Vector3(2f, 0.36f, 1f), layerTwo);
                RuntimeAssets.CreatePrimitive(
                    PrimitiveType.Cube, parent, "Stratum Upper",
                    new Vector3(0f, 0.9f, 0f), new Vector3(2f, 0.36f, 1f), layerThree);
                BoxCollider collider = parent.gameObject.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, 0.54f, 0f);
                collider.size = new Vector3(2f, 1.08f, 1f);
            }
            else if (site.type == "coarse_material")
            {
                RuntimeAssets.CreatePrimitive(
                    PrimitiveType.Sphere, parent, "Coarse Lag Base",
                    new Vector3(0f, 0.09f, 0f), new Vector3(2.4f, 0.18f, 2.4f), sampleMaterial);
                for (int i = 0; i < 32; i++)
                {
                    float angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    float distance = (float)random.NextDouble() * 1.35f;
                    float scale = 0.1f + (float)random.NextDouble() * 0.22f;
                    Vector3 position = new Vector3(Mathf.Cos(angle) * distance, scale * 0.28f, Mathf.Sin(angle) * distance);
                    RuntimeAssets.CreateMeshObject(
                        meshes[random.Next(meshes.Length)], parent, "Coarse Clast",
                        position, new Vector3(scale, scale * 0.7f, scale), rockMaterial, false);
                }
            }
        }

        private void BuildBeacon(
            SiteDef site, Transform parent, Material poleMaterial, Material beaconMaterial,
            Material ringMaterial, Material beamMaterial)
        {
            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Cylinder, parent, "Beacon Pole",
                new Vector3(1.55f, 0.55f, 0f), new Vector3(0.07f, 0.55f, 0.07f), poleMaterial);
            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Sphere, parent, "Beacon Orb",
                new Vector3(1.55f, 1.16f, 0f), new Vector3(0.2f, 0.2f, 0.2f), beaconMaterial);
            RuntimeAssets.CreatePrimitive(
                PrimitiveType.Cylinder, parent, "Beacon Beam",
                new Vector3(0f, 1.65f, 0f), new Vector3(0.3f, 3.3f, 0.3f), beamMaterial);

            GameObject lightObject = new GameObject("Beacon Light");
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = new Vector3(1.55f, 1.16f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.28f);
            light.range = 5f;
            light.intensity = 1.2f;
            light.shadows = LightShadows.None;

            CreateLocalRing(
                parent, "Station Ring", 1.85f, new Color(1f, 0.62f, 0.28f),
                0.05f, 0.045f, 64, ringMaterial);
        }

        private void BuildDust()
        {
            Texture2D circle = RuntimeAssets.CreateCircleTexture(64, Color.white, new Color(1f, 1f, 1f, 0f));
            GameObject dustObject = new GameObject("Regolith Dust");
            dustObject.transform.SetParent(Root, false);
            ParticleSystem dust = dustObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = dust.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.12f, 0.42f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.15f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.62f, 0.6f, 0.56f, 0.42f),
                new Color(0.78f, 0.75f, 0.7f, 0.22f));
            main.maxParticles = 240;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.18f;

            ParticleSystem.EmissionModule emission = dust.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = dust.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.55f;

            ParticleSystemRenderer renderer = dustObject.GetComponent<ParticleSystemRenderer>();
            renderer.material = RuntimeAssets.CreateParticleMaterial("dust", circle, Color.white);
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.View;
            Dust = dust;
        }

        public void UpdateWorld(float deltaTime, Vector3 playerPosition, bool moving)
        {
            elapsed += deltaTime;

            if (Earth != null)
            {
                Earth.Rotate(Vector3.up, deltaTime * 0.55f, Space.World);
            }

            if (landerBeaconLight != null)
            {
                landerBeaconLight.intensity = 1.35f + Mathf.Sin(elapsed * 2.4f) * 0.45f;
            }

            if (Dust != null)
            {
                Dust.transform.position = playerPosition + new Vector3(0f, 0.08f, 0f);
                if (moving)
                {
                    dustEmissionRemainder += Mathf.RoundToInt(deltaTime * 22f);
                    if (dustEmissionRemainder > 0)
                    {
                        Dust.Emit(dustEmissionRemainder);
                        dustEmissionRemainder = 0;
                    }
                }
            }
        }

        public void DustBurst(int count)
        {
            if (Dust != null) Dust.Emit(count);
        }
    }
}
