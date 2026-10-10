using UnityEngine;
using UnityEngine.Rendering;

namespace RealityPlayground.Story
{

    public sealed class StoryHologramShatter : MonoBehaviour
    {
        const int Count = 84;
        public int ShardCount => Count;
        Mesh mesh;
        MeshRenderer meshRenderer;
        Material material;
        readonly Vector3[] vertices = new Vector3[Count * 3];
        readonly Vector3[] normals = new Vector3[Count * 3];
        readonly Color[] colors = new Color[Count * 3];
        readonly Vector3[] positions = new Vector3[Count], velocities = new Vector3[Count], axes = new Vector3[Count];
        readonly Quaternion[] rotations = new Quaternion[Count];
        readonly float[] sizes = new float[Count], spins = new float[Count];
        readonly Color[] tints = new Color[Count];
        float age = -1, floor;

        public void Prepare()
        {
            if (mesh) return;
            var filter = gameObject.AddComponent<MeshFilter>();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            material = new Material(Resources.Load<Shader>("StoryAlarmFX/HologramGlassShards")) { name = "Rooster Glass" };
            meshRenderer.sharedMaterial = material;
            meshRenderer.enabled = false;
            mesh = new Mesh { name = "Hologram Shards", indexFormat = IndexFormat.UInt16 };
            mesh.MarkDynamic();
            var indices = new int[Count * 3];
            var uv = new Vector2[Count * 3];
            for (int i = 0; i < Count; i++)
            {
                int j = i * 3; indices[j] = j; indices[j + 1] = j + 1; indices[j + 2] = j + 2;
                uv[j] = Vector2.zero; uv[j + 1] = Vector2.right; uv[j + 2] = Vector2.up;
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.colors = colors; mesh.uv = uv;
            mesh.triangles = indices; mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 20);
            filter.sharedMesh = mesh;
        }

        public void Burst(Vector3 center, Vector3 flightVelocity, float floorHeight)
        {
            Prepare(); age = 0; floor = floorHeight;
            transform.position = center; transform.rotation = Quaternion.identity;

            var random = new System.Random(604);
            for (int i = 0; i < Count; i++)
            {
                Vector3 direction = new Vector3(Value(random), Value(random), Value(random)).normalized;
                positions[i] = center + Vector3.Scale(direction, new Vector3(.2f, .27f, .16f));
                velocities[i] = flightVelocity * .22f + direction * Mathf.Lerp(.7f, 2.9f, (float)random.NextDouble()) + Vector3.up * .75f;
                axes[i] = new Vector3(Value(random), Value(random), Value(random)).normalized;
                rotations[i] = Quaternion.Euler(Value(random) * 180, Value(random) * 180, Value(random) * 180);
                sizes[i] = Mathf.Lerp(.025f, .1f, (float)random.NextDouble());
                spins[i] = Mathf.Lerp(100, 570, (float)random.NextDouble());
                tints[i] = i % 3 == 0 ? new Color(1, .52f, .12f, .85f) : i % 3 == 1 ? new Color(.1f, .88f, 1, .9f) : new Color(1, .11f, .44f, .85f);
            }
            meshRenderer.enabled = true;
            Draw(0);
        }

        static float Value(System.Random random) => (float)random.NextDouble() * 2 - 1;

        void Update()
        {
            if (age < 0) return;
            float dt = Mathf.Min(Time.deltaTime, .06f); age += dt;
            if (age >= 1.55f) { age = -1; meshRenderer.enabled = false; return; }
            Draw(dt);
        }

        void Draw(float dt)
        {
            float fade = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.45f, 1.55f, age));
            Matrix4x4 local = transform.worldToLocalMatrix;
            for (int i = 0; i < Count; i++)
            {
                velocities[i] += Vector3.down * (4.8f * dt);
                positions[i] += velocities[i] * dt;
                if (positions[i].y < floor + .035f)
                {
                    positions[i].y = floor + .035f;
                    if (velocities[i].y < 0) velocities[i] = new Vector3(velocities[i].x * .65f, -velocities[i].y * .25f, velocities[i].z * .65f);
                }
                rotations[i] = Quaternion.AngleAxis(spins[i] * dt, axes[i]) * rotations[i];
                float size = sizes[i] * (.65f + fade * .35f);
                int j = i * 3;
                vertices[j] = local.MultiplyPoint3x4(positions[i] + rotations[i] * new Vector3(-size, -size * .3f, 0));
                vertices[j + 1] = local.MultiplyPoint3x4(positions[i] + rotations[i] * new Vector3(size, -size * .5f, 0));
                vertices[j + 2] = local.MultiplyPoint3x4(positions[i] + rotations[i] * new Vector3(size * .2f, size * 1.5f, 0));
                Vector3 n = local.MultiplyVector(rotations[i] * Vector3.forward).normalized;
                normals[j] = normals[j + 1] = normals[j + 2] = n;
                Color tint = tints[i]; tint.a *= fade;
                colors[j] = colors[j + 1] = colors[j + 2] = tint;
            }
            mesh.vertices = vertices; mesh.normals = normals; mesh.colors = colors;
        }

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            if (material) Destroy(material);
        }
    }
}
