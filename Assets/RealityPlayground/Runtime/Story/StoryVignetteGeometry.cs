using UnityEngine;
using UnityEngine.Rendering;

namespace RealityPlayground.Story
{

    public static class StoryVignetteGeometry
    {
        public static Material Hologram(string name, Color color, float intensity = 2.2f)
        {
            var shader = Shader.Find("RealityPlayground/Hologram");
            if (!shader) return GreyboxUtil.Material(name, color, intensity, true);
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Intensity", intensity);
            material.SetFloat("_Glitch", .035f);
            return material;
        }

        public static Transform Group(string name, Transform parent, Vector3 position)
        {
            var group = new GameObject(ObjectNames.Short(name)).transform;
            group.SetParent(parent, false);
            group.localPosition = position;
            return group;
        }

        public static Transform Ring(string name, Transform parent, float radius, float thickness, Material material, int segments = 80)
        {
            var points = new Vector3[segments];
            for (int i = 0; i < segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                points[i] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius;
            }
            return GreyboxUtil.Line(name, parent, points, material, thickness, true).transform;
        }

        public static Transform Rod(string name, Transform parent, Vector3 from, Vector3 to, float width, Material material)
        {
            var rod = GreyboxUtil.Primitive(name, PrimitiveType.Cube, parent, (from + to) * .5f,
                new Vector3(width, width, Vector3.Distance(from, to)), material, false).transform;
            rod.localRotation = Quaternion.LookRotation(to - from, Vector3.up);
            return rod;
        }

        public static Mesh Grid(string name, int columns, int rows, Vector2 size)
        {
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[columns * rows * 6];
            for (int y = 0; y <= rows; y++)
            for (int x = 0; x <= columns; x++)
            {
                int i = y * (columns + 1) + x;
                uv[i] = new Vector2((float)x / columns, (float)y / rows);
                vertices[i] = new Vector3((uv[i].x - .5f) * size.x, (uv[i].y - .5f) * size.y, 0);
            }
            int cursor = 0;
            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                int a = y * (columns + 1) + x, b = a + columns + 1;
                triangles[cursor++] = a; triangles[cursor++] = b; triangles[cursor++] = a + 1;
                triangles[cursor++] = a + 1; triangles[cursor++] = b; triangles[cursor++] = b + 1;
            }
            var mesh = new Mesh { name = name, vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        public static MeshFilter Surface(string name, Transform parent, Vector3 position, Mesh mesh, Material material)
        {
            var surface = Group(name, parent, position);
            var filter = surface.gameObject.AddComponent<MeshFilter>(); filter.sharedMesh = mesh;
            var renderer = surface.gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            return filter;
        }

        public static AudioClip Tone(string name, float length, bool rooster)
        {
            const int sampleRate = 22050;
            var samples = new float[Mathf.CeilToInt(length * sampleRate)];
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / sampleRate;
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / length));
                float frequency = rooster ? 590 + 210 * Mathf.Sin(t * 21) + 260 * Mathf.Exp(-t * 7) : 108 + 40 * Mathf.Sin(t * 1.6f);
                float wave = Mathf.Sin(t * frequency * Mathf.PI * 2) + .3f * Mathf.Sin(t * frequency * Mathf.PI * 4.03f);
                samples[i] = wave * envelope * (rooster ? .17f : .09f);
            }
            var clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
