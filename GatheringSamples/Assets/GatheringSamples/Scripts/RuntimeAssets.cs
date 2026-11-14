using System;
using System.Collections.Generic;
using UnityEngine;

namespace GatheringSamples
{
    public static class RuntimeAssets
    {
        private static Shader litShader;
        private static Shader unlitShader;
        private static Shader particleShader;
        private static Texture2D whiteTexture;
        private static Sprite whiteSprite;
        private static Sprite circleSprite;
        private static Sprite ringSprite;
        private static Sprite triangleSprite;
        private static Texture2D vignetteTexture;
        private static readonly Dictionary<string, Material> materialCache = new Dictionary<string, Material>();
        private static readonly Dictionary<string, Mesh> rockMeshes = new Dictionary<string, Mesh>();

        public static Shader LitShader
        {
            get
            {
                if (litShader == null)
                {
                    litShader = Shader.Find("Universal Render Pipeline/Lit");
                    if (litShader == null) litShader = Shader.Find("Standard");
                }
                return litShader;
            }
        }

        public static Shader UnlitShader
        {
            get
            {
                if (unlitShader == null)
                {
                    unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
                    if (unlitShader == null) unlitShader = Shader.Find("Unlit/Color");
                }
                return unlitShader;
            }
        }

        public static Shader ParticleShader
        {
            get
            {
                if (particleShader == null)
                {
                    particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                    if (particleShader == null) particleShader = Shader.Find("Particles/Standard Unlit");
                    if (particleShader == null) particleShader = Shader.Find("Sprites/Default");
                }
                return particleShader;
            }
        }

        public static Texture2D WhiteTexture
        {
            get
            {
                if (whiteTexture == null)
                {
                    whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                    whiteTexture.SetPixel(0, 0, Color.white);
                    whiteTexture.Apply();
                }
                return whiteTexture;
            }
        }

        public static Sprite WhiteSprite
        {
            get
            {
                if (whiteSprite == null)
                {
                    whiteSprite = Sprite.Create(WhiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
                }
                return whiteSprite;
            }
        }

        public static Sprite CircleSprite
        {
            get
            {
                if (circleSprite == null)
                {
                    Texture2D texture = CreateCircleTexture(64, Color.white, new Color(1f, 1f, 1f, 0f));
                    circleSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 64f);
                }
                return circleSprite;
            }
        }

        public static Sprite RingSprite
        {
            get
            {
                if (ringSprite == null)
                {
                    Texture2D texture = CreateRingTexture(128, 0.9f, 0.97f, Color.white);
                    ringSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 128f);
                }
                return ringSprite;
            }
        }

        public static Sprite TriangleSprite
        {
            get
            {
                if (triangleSprite == null)
                {
                    Texture2D texture = CreateTriangleTexture(64);
                    triangleSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 64f);
                }
                return triangleSprite;
            }
        }

        public static Texture2D VignetteTexture
        {
            get
            {
                if (vignetteTexture == null)
                {
                    vignetteTexture = CreateVignetteTexture(256);
                }
                return vignetteTexture;
            }
        }

        public static Material CreateLit(string key, Color color, float metallic = 0f, float smoothness = 0.25f, Texture2D mainTexture = null, Vector2 tiling = default)
        {
            string cacheKey = "lit:" + key;
            if (materialCache.TryGetValue(cacheKey, out Material cached) && cached != null)
            {
                return cached;
            }

            Material material = new Material(LitShader);
            material.name = key;
            material.color = color;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Glossiness", smoothness);
            if (mainTexture != null)
            {
                material.mainTexture = mainTexture;
                material.SetTexture("_BaseMap", mainTexture);
                material.mainTextureScale = tiling;
                material.SetTextureScale("_BaseMap", tiling);
            }
            materialCache[cacheKey] = material;
            return material;
        }

        public static Material CreateUnlit(string key, Color color, Texture2D texture = null, bool transparent = false)
        {
            string cacheKey = "unlit:" + key + ":" + transparent;
            if (materialCache.TryGetValue(cacheKey, out Material cached) && cached != null)
            {
                return cached;
            }

            Material material = new Material(UnlitShader);
            material.name = key;
            material.color = color;
            material.SetColor("_BaseColor", color);
            if (texture != null)
            {
                material.mainTexture = texture;
                material.SetTexture("_BaseMap", texture);
            }

            if (transparent)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHATEST_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            materialCache[cacheKey] = material;
            return material;
        }

        public static Material CreateParticleMaterial(string key, Texture2D texture, Color color)
        {
            string cacheKey = "particle:" + key;
            if (materialCache.TryGetValue(cacheKey, out Material cached) && cached != null)
            {
                return cached;
            }

            Material material = new Material(ParticleShader);
            material.name = key;
            material.mainTexture = texture;
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_Color", color);
            material.SetColor("_BaseColor", color);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            materialCache[cacheKey] = material;
            return material;
        }

        public static Texture2D CreateNoiseTexture(int size, float baseGray, float variation, int seed)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            Color32[] pixels = new Color32[size * size];
            System.Random random = new System.Random(seed);

            for (int i = 0; i < pixels.Length; i++)
            {
                float value = baseGray + ((float)random.NextDouble() - 0.5f) * variation;
                value = Mathf.Clamp01(value);
                byte gray = (byte)(value * 255f);
                pixels[i] = new Color32(gray, (byte)Mathf.Clamp(gray - 2, 0, 255), (byte)Mathf.Clamp(gray - 7, 0, 255), 255);
            }

            for (int i = 0; i < size * 8; i++)
            {
                int cx = random.Next(0, size);
                int cy = random.Next(0, size);
                int radius = random.Next(1, 7);
                float shade = 0.32f + (float)random.NextDouble() * 0.24f;
                byte gray = (byte)(shade * 255f);
                for (int y = -radius; y <= radius; y++)
                {
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (x * x + y * y > radius * radius) continue;
                        int px = (cx + x + size) % size;
                        int py = (cy + y + size) % size;
                        pixels[py * size + px] = new Color32(gray, (byte)Mathf.Clamp(gray - 2, 0, 255), (byte)Mathf.Clamp(gray - 6, 0, 255), 255);
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true);
            return texture;
        }

        public static Texture2D CreateCircleTexture(int size, Color inner, Color outer)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / radius;
                    float dy = (y - center) / radius;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01(1f - distance);
                    Color color = Color.Lerp(inner, outer, distance);
                    color.a *= alpha;
                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        public static Texture2D CreateRingTexture(int size, float innerRadius, float outerRadius, Color color)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / radius;
                    float dy = (y - center) / radius;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = distance >= innerRadius && distance <= outerRadius ? 1f : 0f;
                    pixels[y * size + x] = new Color(color.r, color.g, color.b, color.a * alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        public static Texture2D CreateTriangleTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            Vector2 a = new Vector2(size * 0.5f, size * 0.92f);
            Vector2 b = new Vector2(size * 0.12f, size * 0.12f);
            Vector2 c = new Vector2(size * 0.88f, size * 0.12f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    bool inside = PointInTriangle(p, a, b, c);
                    pixels[y * size + x] = inside ? Color.white : new Color(1f, 1f, 1f, 0f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        public static Texture2D CreateVignetteTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Color[] pixels = new Color[size * size];
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / radius;
                    float dy = (y - center) / radius;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, distance));
                    pixels[y * size + x] = new Color(0.85f, 0.08f, 0.04f, alpha * 0.62f);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        public static Mesh CreateRockMesh(string key, int seed, int rings = 8, int segments = 12)
        {
            if (rockMeshes.TryGetValue(key, out Mesh cached) && cached != null)
            {
                return cached;
            }

            System.Random random = new System.Random(seed);
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();

            for (int ring = 0; ring <= rings; ring++)
            {
                float v = (float)ring / rings;
                float phi = v * Mathf.PI;
                float y = Mathf.Cos(phi) * 0.5f;
                float ringRadius = Mathf.Sin(phi) * 0.5f;

                for (int segment = 0; segment <= segments; segment++)
                {
                    float u = (float)segment / segments;
                    float theta = u * Mathf.PI * 2f;
                    float noise = 0.82f + (float)random.NextDouble() * 0.36f;
                    float x = Mathf.Cos(theta) * ringRadius * noise;
                    float z = Mathf.Sin(theta) * ringRadius * noise;
                    vertices.Add(new Vector3(x, y * noise, z));
                    uvs.Add(new Vector2(u, v));
                }
            }

            for (int ring = 0; ring < rings; ring++)
            {
                for (int segment = 0; segment < segments; segment++)
                {
                    int current = ring * (segments + 1) + segment;
                    int next = current + segments + 1;
                    triangles.Add(current);
                    triangles.Add(next);
                    triangles.Add(current + 1);
                    triangles.Add(current + 1);
                    triangles.Add(next);
                    triangles.Add(next + 1);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = key;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            rockMeshes[key] = mesh;
            return mesh;
        }

        public static GameObject CreatePrimitive(PrimitiveType type, Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject gameObject = GameObject.CreatePrimitive(type);
            gameObject.name = name;
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            gameObject.transform.localScale = localScale;
            Collider collider = gameObject.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.Destroy(collider);
            Renderer renderer = gameObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            return gameObject;
        }

        public static GameObject CreateMeshObject(Mesh mesh, Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, bool addCollider = false)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = localPosition;
            gameObject.transform.localScale = localScale;
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;
            if (addCollider)
            {
                MeshCollider collider = gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = mesh;
            }
            return gameObject;
        }

        public static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private static bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Sign(p, a, b);
            float d2 = Sign(p, b, c);
            float d3 = Sign(p, c, a);
            bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
            bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(hasNegative && hasPositive);
        }

        private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }
    }
}
