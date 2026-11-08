using System.Text;
using UnityEngine;

namespace MoonCinematic.EditorTools
{
    /// <summary>
    /// Procedural generation helpers for the Moon cinematic:
    /// deterministic noise, textures (regolith, scorch, gray rock recoloring,
    /// distant Earth disc) and crumpled wreckage meshes.
    /// Everything is generated for free, no external assets are downloaded.
    /// </summary>
    public static class MoonFactory
    {
        // ------------------------------------------------------------------ noise

        static int Hash(int x, int y, int seed)
        {
            int h = seed + x * 374761393 + y * 668265263;
            h = (h ^ (h >> 13)) * 1274126177;
            h = h ^ (h >> 16);
            return h;
        }

        static float Rand01(int hash)
        {
            return (hash & 0x7fffffff) / 2147483647f;
        }

        public static float ValueNoise(float x, float y, int seed)
        {
            int ix = Mathf.FloorToInt(x);
            int iy = Mathf.FloorToInt(y);
            float fx = x - ix, fy = y - iy;
            float sx = fx * fx * (3f - 2f * fx);
            float sy = fy * fy * (3f - 2f * fy);
            float v00 = Rand01(Hash(ix, iy, seed));
            float v10 = Rand01(Hash(ix + 1, iy, seed));
            float v01 = Rand01(Hash(ix, iy + 1, seed));
            float v11 = Rand01(Hash(ix + 1, iy + 1, seed));
            return Mathf.Lerp(Mathf.Lerp(v00, v10, sx), Mathf.Lerp(v01, v11, sx), sy);
        }

        /// <summary>HLSL-style smoothstep: smooth 0..1 ramp between edges (Mathf.SmoothStep itself expects t already clamped to 0..1).</summary>
        public static float Sstep01(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        public static float Fbm(float x, float y, int seed, int octaves = 5)
        {
            float f = 0f, amp = 0.5f, tot = 0f;
            for (int i = 0; i < octaves; i++)
            {
                f += amp * ValueNoise(x, y, seed + i * 131);
                tot += amp;
                x *= 2f; y *= 2f; amp *= 0.5f;
            }
            return f / tot;
        }

        // ------------------------------------------------------------------ terrain height field

        public struct Crater
        {
            public Vector2 center;   // world XZ
            public float radius;     // meters
            public float depth;      // normalized terrain units (0..1 of terrain height range)
            public float rim;        // rim height (normalized units)
        }

        /// <summary>Standard lunar crater depth profile: bowl inside, raised rim near edge.</summary>
        public static float CraterProfileShape(float s) // s = dist / radius in 0..1
        {
            // bowl: 1 at center -> 0 at edge
            float bowl = 1f - Mathf.SmoothStep(0f, 1f, s);
            return bowl * bowl;
        }

        public static float CraterRimShape(float s)
        {
            // rim: peaks at s ~ 0.88, zero before 0.68 and after 1.0
            if (s < 0.68f || s > 1.05f) return 0f;
            float t = Mathf.Clamp01((s - 0.68f) / 0.37f);
            return Mathf.Sin(t * Mathf.PI);
        }

        // ------------------------------------------------------------------ textures

        public static Texture2D GenerateRegolithAlbedo(int size, float baseLevel = 0.5f, float contrast = 1f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x / (float)size;
                    float fy = y / (float)size;
                    float n1 = Fbm(fx * 3.1f, fy * 3.1f, 17, 5);
                    float n2 = Fbm(fx * 11.0f, fy * 11.0f, 41, 4);
                    float speckle = Fbm(fx * 30.0f, fy * 30.0f, 77, 3);
                    float v = baseLevel + (n1 * 0.22f + n2 * 0.16f - speckle * 0.10f) * contrast;
                    v = Mathf.Clamp(v, 0.04f, 0.98f);
                    // cool neutral gray with a whisper of warm dust tint
                    byte b = (byte)Mathf.RoundToInt(v * 255f);
                    byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(b * 1.015f), 0, 255);
                    byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(b * 1.005f), 0, 255);
                    pixels[y * size + x] = new Color32(r, g, b, 255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(updateMipmaps: true, makeNoLongerReadable: false);
            return tex;
        }

        public static Texture2D GenerateRegolithNormal(int size)
        {
            // height field from fbm, then sobel -> normals
            var tex = new Texture2D(size, size, TextureFormat.RGB24, true, true);
            var heights = new float[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float fx = x / (float)size, fy = y / (float)size;
                    heights[y * size + x] = Fbm(fx * 5.0f, fy * 5.0f, 111, 5) * 0.65f
                                          + Fbm(fx * 19.0f, fy * 19.0f, 55, 3) * 0.35f;
                }
            }
            const float strength = 2.2f;
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int xl = (x - 1 + size) % size, xr = (x + 1) % size;
                    int yd = (y - 1 + size) % size, yu = (y + 1) % size;
                    float dx = heights[y * size + xr] - heights[y * size + xl];
                    float dy = heights[yu * size + x] - heights[yd * size + x];
                    Vector3 n = new Vector3(-dx * strength, -dy * strength, 1f).normalized;
                    byte r = (byte)Mathf.RoundToInt((n.x * 0.5f + 0.5f) * 255f);
                    byte g = (byte)Mathf.RoundToInt((n.y * 0.5f + 0.5f) * 255f);
                    byte b = (byte)Mathf.RoundToInt((n.z * 0.5f + 0.5f) * 255f);
                    pixels[y * size + x] = new Color32(r, g, b, 255);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Radial charred/scorched decal with noisy edge, alpha used for blending.</summary>
        public static Texture2D GenerateScorchDecal(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, true);
            var pixels = new Color32[size * size];
            Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float r = Vector2.Distance(p, c) / half; // 0..1 to corners
                    float ang = Mathf.Atan2(p.y - c.y, p.x - c.x);
                    float edgeNoise = Fbm(Mathf.Cos(ang) * 2.2f + 8f, Mathf.Sin(ang) * 2.2f + 8f, 303, 4);
                    r += (edgeNoise - 0.5f) * 0.28f;
                    float a = 1f - Mathf.SmoothStep(0.10f, 0.985f, r);
                    a = Mathf.Pow(a, 1.35f);
                    // charred brightness: darker center, mottled soot
                    float shade = 0.10f + 0.10f * Fbm(x * 0.07f, y * 0.07f, 12, 4);
                    float v = Mathf.Clamp01(a > 0.02f ? shade : 0f);
                    byte b = (byte)Mathf.RoundToInt(v * 255f);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f);
                    pixels[y * size + x] = new Color32(b, b, b, alpha);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>Compact radial spark sprite for particles.</summary>
        public static Texture2D GenerateSparkSprite(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var pixels = new Color32[size * size];
            float c = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = Mathf.Sqrt((x + 0.5f - c) * (x + 0.5f - c) + (y + 0.5f - c) * (y + 0.5f - c)) / c;
                    float a = Mathf.Pow(Mathf.Clamp01(1f - r), 2.4f);
                    byte v = (byte)Mathf.RoundToInt(a * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, v);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        // ------------------------------------------------------------------ earth disc (crop from free skybox texture)

        /// <summary>
        /// Crops a full Earth disc (against black space) out of one free Earth-skybox
        /// face and adds a feathered atmosphere alpha so it can be used as a small
        /// distant Earth object. Returns null when the source file is missing.
        /// </summary>
        /// <summary>
        /// Fully procedural Earth view from space ("Earthrise" look): blue oceans,
        /// noisy continents, cloud swirls, soft terminator and an atmosphere halo.
        /// Deterministic, so it always reads as Earth regardless of source art.
        /// </summary>
        public static Texture2D GenerateEarthDisc(int outSize, Color atmosphereTint)
        {
            int side = outSize;
            var pixels = new Color32[side * side];
            float R = side * 0.405f;
            float cx = side * 0.5f, cy = side * 0.5f;
            // sun direction in disc space: mostly toward the camera, slightly upper-left
            var L = new Vector3(-0.32f, 0.28f, 0.90f).normalized;

            var ocean = new Color(0.10f, 0.24f, 0.48f);
            var oceanDark = new Color(0.03f, 0.08f, 0.20f);
            var land = new Color(0.28f, 0.38f, 0.16f);
            var land2 = new Color(0.48f, 0.40f, 0.22f);
            var cloudCol = new Color(1.00f, 1.00f, 1.00f);

            for (int y = 0; y < side; y++)
            {
                for (int x = 0; x < side; x++)
                {
                    float nx = (x - cx) / R;
                    float ny = (cy - y) / R; // flip so +y is up
                    float rr = Mathf.Sqrt(nx * nx + ny * ny);

                    // atmosphere halo ring just outside the limb (zero across the disc interior)
                    float halo = Mathf.Clamp01(1f - (rr - 0.965f) / 0.115f);
                    halo *= Mathf.Clamp01((rr - 0.88f) / 0.10f);
                    halo *= 0.55f;

                    if (rr > 1.0f)
                    {
                        byte ha = (byte)Mathf.RoundToInt(Mathf.Clamp01(halo) * 255f);
                        pixels[y * side + x] = new Color32((byte)Mathf.RoundToInt(atmosphereTint.r * 255f * 0.5f),
                                                           (byte)Mathf.RoundToInt(atmosphereTint.g * 255f * 0.65f),
                                                           (byte)Mathf.RoundToInt(atmosphereTint.b * 255f * 0.9f),
                                                           ha);
                        continue;
                    }

                    // fake sphere normal for shading + curvature of surface details
                    float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - rr * rr));
                    var N = new Vector3(nx, ny, nz);
                    float lit = Vector3.Dot(N, L);
                    float shade = Sstep01(-0.10f, 0.30f, lit);

                    // surface detail, curved by the sphere so continents wrap believably
                    float sxI = nx * (1.7f + nz * 0.6f) + 11.3f;
                    float syI = ny * (1.7f + nz * 0.6f) + 3.7f;
                    // Fbm centers on ~0.5; stretch it for real contrast before masking
                    float landV = Mathf.Clamp01((Fbm(sxI * 1.15f, syI * 1.15f, 41, 5) - 0.5f) * 3.4f + 0.5f);
                    float landMask = Sstep01(0.53f, 0.63f, landV);
                    float landDetail = Fbm(sxI * 3.3f, syI * 3.3f, 97, 4);
                    var surfaceCol = Color.Lerp(
                        Color.Lerp(ocean, oceanDark, landV * 0.35f),
                        Color.Lerp(land, land2, Mathf.Clamp01((landDetail - 0.5f) * 2.6f + 0.5f)),
                        landMask);

                    // cloud layer, strong coverage ocean-wide + varied holes
                    float cloudV = Mathf.Clamp01((Fbm(sxI * 3.2f + 55f, syI * 3.2f - 20f, 73, 5) - 0.5f) * 3.2f + 0.5f);
                    float bandN = Mathf.Sin((ny + rr * 0.3f) * 3.8f + 1.2f) * 0.07f;
                    float cloudMask = Sstep01(0.52f - bandN, 0.68f - bandN, cloudV);
                    cloudMask = Mathf.Max(cloudMask, Sstep01(0.60f, 0.76f, cloudV) * 0.40f);
                    var col = Color.Lerp(surfaceCol, cloudCol, cloudMask);

                    // dayside/terminator shading, slight blue scatter in twilight
                    col *= Mathf.Lerp(0.10f, 1.55f, shade);
                    if (shade > 0f && shade < 0.35f)
                        col = Color.Lerp(col, atmosphereTint * 0.6f, (0.35f - shade) / 0.35f * 0.25f);

                    // limb darkening + faint atmosphere edge inside the disc
                    float limb = 1f - Mathf.Pow(rr, 7f) * 0.35f;
                    col *= limb;

                    // translucent atmosphere: fades out across the limb, tints the rim
                    float edge = 1f - Sstep01(0.955f, 1.0f, rr);
                    col = Color.Lerp(atmosphereTint * 0.9f, col, edge);
                    float haloAlpha = Mathf.Clamp01(halo) * Mathf.Clamp01(1f - shade * 0.55f);
                    var outC = Color.Lerp(col, new Color(0.58f, 0.76f, 1f), halo * 0.4f);
                    byte aa = (byte)Mathf.RoundToInt(Mathf.Clamp01(Mathf.Max(haloAlpha, edge * 0.92f)) * 255f);
                    pixels[y * side + x] = new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(outC.r) * 255f),
                                                       (byte)Mathf.RoundToInt(Mathf.Clamp01(outC.g) * 255f),
                                                       (byte)Mathf.RoundToInt(Mathf.Clamp01(outC.b) * 255f),
                                                       aa);
                }
            }
            var tex = new Texture2D(side, side, TextureFormat.RGBA32, true, false);
            tex.SetPixels32(pixels);
            tex.Apply(true);
            return tex;
        }

        // ------------------------------------------------------------------ gray moon-rock recoloring

        /// <summary>
        /// Recolors an existing (reddish/Mars) rock albedo texture into neutral gray
        /// by remapping its luminance, keeping every surface detail. Source PNG is
        /// read directly from disk, no importer settings are modified.
        /// </summary>
        public static Texture2D RecolorToGrayMoon(string sourcePngPath, float grayBase)
        {
            if (!System.IO.File.Exists(sourcePngPath)) return null;
            var src = new Texture2D(2, 2);
            src.LoadImage(System.IO.File.ReadAllBytes(sourcePngPath));
            var srcPix = src.GetPixels32();
            int w = src.width, h = src.height;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, false);
            var pixels = new Color32[srcPix.Length];
            // per-channel remap: use per-pixel luminance, slightly compress contrast
            for (int i = 0; i < srcPix.Length; i++)
            {
                var p = srcPix[i];
                float lum = (p.r * 0.3f + p.g * 0.5f + p.b * 0.2f) / 255f;
                float v = Mathf.Clamp01(grayBase + (lum - 0.55f) * 0.75f);
                byte b = (byte)Mathf.RoundToInt(v * 255f);
                byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(b * 0.995f), 0, 255);
                byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(b * 1.0f), 0, 255);
                pixels[i] = new Color32(r, g, b, 255);
            }
            tex.SetPixels32(pixels);
            tex.Apply(true, false);
            Object.DestroyImmediate(src);
            return tex;
        }

        // ------------------------------------------------------------------ crumpled wreckage meshes

        public static Mesh GenerateCrumpledBox(int segX, int segY, int segZ, Vector3 size, float noise, float seed)
        {
            var verts = new Vector3[(segX + 1) * (segY + 1) * (segZ + 1)];
            Vector3 half = size * 0.5f;
            int i = 0;
            for (int z = 0; z <= segZ; z++)
            {
                for (int y = 0; y <= segY; y++)
                {
                    for (int x = 0; x <= segX; x++)
                    {
                        float u = x / (float)segX, v = y / (float)segY, q = z / (float)segZ;
                        float px = (u - 0.5f) * size.x;
                        float py = (v - 0.5f) * size.y;
                        float pz = (q - 0.5f) * size.z;

                        // large smooth warp (bent hull)
                        float warp = Fbm(u * 1.7f + 2f, v * 1.7f + q * 1.3f, (int)seed * 7 + 3, 4) - 0.5f;
                        float dent = Fbm(u * 4.5f - 8f, q * 4.5f + v * 3.1f, (int)seed * 11 + 9, 5) - 0.5f;
                        float crumple = Fbm(u * 9.0f, v * 9.0f + q * 9.0f, (int)seed * 17 + 21, 4) - 0.5f;

                        float nx = px + dent * noise * size.x * 0.11f;
                        float ny = py + warp * noise * size.y * 0.30f + crumple * noise * size.y * 0.10f;
                        float nz = pz + dent * noise * size.z * 0.11f;
                        verts[i] = new Vector3(nx, ny, nz);
                        i++;
                    }
                }
            }

            // build the six open shell faces using the grid (leave hollow: only outer faces)
            var tris = new System.Collections.Generic.List<int>();
            void Quad(int a, int b, int c, int d)
            {
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(a); tris.Add(d); tris.Add(c);
            }
            int V(int x, int y, int z) => (z * (segY + 1) + y) * (segX + 1) + x;

            // back (z=0) and front (z=segZ)
            for (int y = 0; y < segY; y++)
                for (int x = 0; x < segX; x++)
                {
                    Quad(V(x, y, 0), V(x + 1, y, 0), V(x + 1, y + 1, 0), V(x, y + 1, 0));
                    Quad(V(x, y, segZ), V(x, y + 1, segZ), V(x + 1, y + 1, segZ), V(x + 1, y, segZ));
                }
            // left (x=0) and right (x=segX)
            for (int z = 0; z < segZ; z++)
                for (int y = 0; y < segY; y++)
                {
                    Quad(V(0, y, z), V(0, y + 1, z), V(0, y + 1, z + 1), V(0, y, z + 1));
                    Quad(V(segX, y, z), V(segX, y, z + 1), V(segX, y + 1, z + 1), V(segX, y + 1, z));
                }
            // bottom (y=0) and top (y=segY)
            for (int z = 0; z < segZ; z++)
                for (int x = 0; x < segX; x++)
                {
                    Quad(V(x, 0, z), V(x, 0, z + 1), V(x + 1, 0, z + 1), V(x + 1, 0, z));
                    Quad(V(x, segY, z), V(x + 1, segY, z), V(x + 1, segY, z + 1), V(x, segY, z + 1));
                }

            var mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.triangles = tris.ToArray();
            // simple box-ish UVs
            var uvs = new Vector2[verts.Length];
            i = 0;
            for (int z = 0; z <= segZ; z++)
                for (int y = 0; y <= segY; y++)
                    for (int x = 0; x <= segX; x++)
                    {
                        uvs[i++] = new Vector2((x + y * 0.35f) / (segX + 2f), (y + z * 0.35f) / (segY + 2f));
                    }
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.Optimize();
            mesh.name = "CrumpledBox_" + seed;
            return mesh;
        }

        public static Mesh GenerateAngularShard(float scale /* ~1 */, int seed)
        {
            // irregular polyhedron from a jittered icosphere-like cluster
            var rnd = new System.Random(seed);
            var vertsList = new System.Collections.Generic.List<Vector3>();
            var tris = new System.Collections.Generic.List<int>();

            // base octahedron jittered, then subdivided once by midpoint splitting
            Vector3[] ctrl = new Vector3[6];
            for (int i = 0; i < 6; i++)
            {
                float a = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float b = (float)(rnd.NextDouble() * 2.0 - 1.0);
                float c = (float)(rnd.NextDouble() * 2.0 - 1.0);
                ctrl[i] = (new Vector3(a, b, c)).normalized * scale * (0.6f + (float)rnd.NextDouble() * 0.7f);
            }

            void AddTri(Vector3 v0, Vector3 v1, Vector3 v2)
            {
                int i0 = vertsList.Count; vertsList.Add(v0);
                int i1 = vertsList.Count; vertsList.Add(v1);
                int i2 = vertsList.Count; vertsList.Add(v2);
                tris.Add(i0); tris.Add(i1); tris.Add(i2);
            }

            Vector3[] pair =
            {
                ctrl[0], ctrl[2], ctrl[4],
                ctrl[0], ctrl[4], ctrl[1],
                ctrl[0], ctrl[1], ctrl[3],
                ctrl[0], ctrl[3], ctrl[2],
                ctrl[5], ctrl[4], ctrl[2],
                ctrl[5], ctrl[1], ctrl[4],
                ctrl[5], ctrl[3], ctrl[1],
                ctrl[5], ctrl[2], ctrl[3],
            };
            for (int t = 0; t < pair.Length; t += 3)
            {
                // one random midpoint split for extra jaggedness
                Vector3 m0 = Vector3.Lerp(pair[t], pair[t + 1], 0.5f);
                m0 += new Vector3((float)(rnd.NextDouble() - 0.5), (float)(rnd.NextDouble() - 0.5), (float)(rnd.NextDouble() - 0.5)) * scale * 0.4f;
                m0 = m0.normalized * Mathf.Lerp(pair[t].magnitude, pair[t + 1].magnitude, 0.5f);
                AddTri(pair[t], m0, pair[t + 2]);
                AddTri(m0, pair[t + 1], pair[t + 2]);
            }

            var mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertsList.ToArray();
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.name = "Shard_" + seed;
            return mesh;
        }
    }
}
