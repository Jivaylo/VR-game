using System.Collections.Generic;
using UnityEngine;

namespace RealityPlayground
{

    public sealed class ClayUI : PlaygroundTarget
    {
        enum Control { Surface, Restore, Palette }
        [SerializeField] Control control;
        [SerializeField] ClayUI surface;
        [SerializeField] MeshFilter meshFilter;
        [SerializeField] MeshCollider meshCollider;
        [SerializeField] MeshRenderer meshRenderer;
        [SerializeField] Transform[] content;
        [SerializeField] Vector2[] contentUV;
        [SerializeField] LineRenderer waveform;
        [SerializeField] int columns = 24;
        [SerializeField] int rows = 16;
        [SerializeField] float width = 2.55f;
        [SerializeField] float height = 1.65f;
        [Tooltip("How far each grip pulls the surrounding clay, in local metres.")]
        [SerializeField, Min(.05f)] float influenceRadius = .86f;
        [Tooltip("Maximum displacement from the original shape, in local metres.")]
        [SerializeField, Range(.05f, 1f)] float maximumDeformation = .7f;

        sealed class Grip
        {
            public Vector3 point;
            public Vector3 last;
        }

        readonly Dictionary<Transform, Grip> grips = new Dictionary<Transform, Grip>();
        readonly List<Transform> expiredGrips = new List<Transform>();
        Mesh runtimeMesh;
        Material runtimeMaterial;
        Vector3[] original, target, vertices;
        float colliderTimer;
        float gain = .5f;
        int paletteIndex;
        static readonly Color[] Palette = { new Color(.82f, .39f, .25f), new Color(.46f, .34f, .72f), new Color(.22f, .62f, .49f) };

        public static GameObject Create(Transform parent)
        {
            var root = new GameObject("ClayPanel");
            root.transform.SetParent(parent, false);
            var frame = GreyboxUtil.Material("Clay graphite", new Color(.105f, .125f, .14f));
            var clay = GreyboxUtil.Material("Clay apricot", Palette[0]);
            var ink = GreyboxUtil.Material("Clay dark ink", new Color(.06f, .055f, .07f));
            var mint = GreyboxUtil.Material("Clay mint signal", new Color(.44f, 1f, .81f), 1.5f);
            GreyboxUtil.Primitive("Low plinth", PrimitiveType.Cube, root.transform, new Vector3(0f, .12f, .35f), new Vector3(3.4f, .24f, 1.25f), frame);
            GreyboxUtil.Primitive("Left stand", PrimitiveType.Cube, root.transform, new Vector3(-1.24f, .65f, .46f), new Vector3(.07f, 1.1f, .09f), frame);
            GreyboxUtil.Primitive("Right stand", PrimitiveType.Cube, root.transform, new Vector3(1.24f, .65f, .46f), new Vector3(.07f, 1.1f, .09f), frame);
            GreyboxUtil.Label("Grip and pull the surface. Use both hands to stretch it.", root.transform, new Vector3(0f, 2.75f, .12f), .07f, new Color(.73f, .81f, .82f));

            var panel = new GameObject("Sculptableclaywindow");
            panel.transform.SetParent(root.transform, false);
            panel.transform.localPosition = new Vector3(0f, 1.57f, .1f);
            var sculpt = panel.AddComponent<ClayUI>();
            sculpt.meshFilter = panel.AddComponent<MeshFilter>();
            sculpt.meshRenderer = panel.AddComponent<MeshRenderer>();
            sculpt.meshRenderer.sharedMaterial = clay;
            sculpt.meshFilter.sharedMesh = sculpt.BuildMesh();
            sculpt.meshCollider = panel.AddComponent<MeshCollider>();
            sculpt.meshCollider.sharedMesh = sculpt.meshFilter.sharedMesh;

            var reset = GreyboxUtil.Primitive("Reset Button", PrimitiveType.Cube, panel.transform, new Vector3(-.61f, -.49f, -.15f), new Vector3(.84f, .29f, .12f), ink);
            var resetTarget = reset.AddComponent<ClayUI>(); resetTarget.control = Control.Restore; resetTarget.surface = sculpt;
            GreyboxUtil.Label("Restore shape", reset.transform, new Vector3(0f, 0f, -.54f), .054f, Color.white).transform.localScale = new Vector3(1f / .84f, 1f / .29f, 1f / .12f);
            var tint = GreyboxUtil.Primitive("Change clay pigment", PrimitiveType.Cube, panel.transform, new Vector3(.61f, -.49f, -.15f), new Vector3(.84f, .29f, .12f), ink);
            var tintTarget = tint.AddComponent<ClayUI>(); tintTarget.control = Control.Palette; tintTarget.surface = sculpt;
            GreyboxUtil.Label("Change colour", tint.transform, new Vector3(0f, 0f, -.54f), .054f, Color.white).transform.localScale = new Vector3(1f / .84f, 1f / .29f, 1f / .12f);

            sculpt.content = new[] { reset.transform, tint.transform };
            sculpt.contentUV = new[] { new Vector2(-.61f / sculpt.width + .5f, -.49f / sculpt.height + .5f), new Vector2(.61f / sculpt.width + .5f, -.49f / sculpt.height + .5f) };
            var points = new Vector3[64];
            for (int i = 0; i < points.Length; i++) points[i] = new Vector3(Mathf.Lerp(-1.03f, 1.03f, i / 63f), .21f + Mathf.Sin(i * .3f) * .10f, -.13f);
            sculpt.waveform = GreyboxUtil.Line("Signal", panel.transform, points, mint, .015f);
            sculpt.waveform.useWorldSpace = false;
            return root;
        }

        Mesh BuildMesh()
        {
            int layerSize = (columns + 1) * (rows + 1);
            var points = new Vector3[layerSize * 2];
            var uv = new Vector2[points.Length];
            var triangles = new List<int>(columns * rows * 12 + (columns + rows) * 12);
            for (int side = 0; side < 2; side++)
            for (int y = 0; y <= rows; y++)
            for (int x = 0; x <= columns; x++)
            {
                int i = side * layerSize + y * (columns + 1) + x;
                float u = x / (float)columns, v = y / (float)rows;
                var p = new Vector2((u - .5f) * width, (v - .5f) * height);
                const float radius = .17f;
                var corner = new Vector2(Mathf.Max(Mathf.Abs(p.x) - (width * .5f - radius), 0f), Mathf.Max(Mathf.Abs(p.y) - (height * .5f - radius), 0f));
                if (corner.sqrMagnitude > radius * radius)
                {
                    var rounded = corner.normalized * radius;
                    p.x -= Mathf.Sign(p.x) * (corner.x - rounded.x);
                    p.y -= Mathf.Sign(p.y) * (corner.y - rounded.y);
                }
                float puff = Mathf.Sin(u * Mathf.PI) * Mathf.Sin(v * Mathf.PI) * .026f;
                points[i] = new Vector3(p.x, p.y, side == 0 ? -.07f - puff : .07f + puff);
                uv[i] = new Vector2(u, v);
                if (x == columns || y == rows) continue;
                int a = i, b = i + 1, c = i + columns + 1, d = c + 1;
                if (side == 0) { triangles.Add(a); triangles.Add(c); triangles.Add(b); triangles.Add(b); triangles.Add(c); triangles.Add(d); }
                else { triangles.Add(a); triangles.Add(b); triangles.Add(c); triangles.Add(b); triangles.Add(d); triangles.Add(c); }
            }
            var edge = new List<int>();
            for (int x = 0; x <= columns; x++) edge.Add(x);
            for (int y = 1; y <= rows; y++) edge.Add(y * (columns + 1) + columns);
            for (int x = columns - 1; x >= 0; x--) edge.Add(rows * (columns + 1) + x);
            for (int y = rows - 1; y > 0; y--) edge.Add(y * (columns + 1));
            for (int i = 0; i < edge.Count; i++)
            {
                int a = edge[i], b = edge[(i + 1) % edge.Count];
                triangles.Add(a); triangles.Add(b); triangles.Add(a + layerSize);
                triangles.Add(b); triangles.Add(b + layerSize); triangles.Add(a + layerSize);
            }
            var mesh = new Mesh { name = "Clay Mesh" };
            mesh.vertices = points; mesh.uv = uv; mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        void Awake()
        {
            if (control != Control.Surface || !meshFilter || !meshFilter.sharedMesh) return;
            runtimeMesh = Instantiate(meshFilter.sharedMesh);
            runtimeMesh.name = "Clay Mesh";
            runtimeMesh.MarkDynamic();
            meshFilter.sharedMesh = runtimeMesh;
            original = runtimeMesh.vertices;
            target = (Vector3[])original.Clone();
            vertices = (Vector3[])original.Clone();
            meshCollider.sharedMesh = runtimeMesh;
            runtimeMaterial = meshRenderer.material;
        }

        public override void BeginInteraction(Transform hand)
        {
            if (control != Control.Surface) { Activate(); return; }
            if (!hand || vertices == null) return;
            Vector3 local = transform.InverseTransformPoint(hand.position);
            int nearest = 0;
            float distance = float.PositiveInfinity;
            int count = (columns + 1) * (rows + 1);
            for (int i = 0; i < count; i++)
            {
                float d = (vertices[i] - local).sqrMagnitude;
                if (d < distance) { nearest = i; distance = d; }
            }
            grips[hand] = new Grip { point = vertices[nearest], last = local };
        }

        public override void UpdateInteraction(Transform hand)
        {
            if (control != Control.Surface || !hand || target == null || !grips.TryGetValue(hand, out Grip grip)) return;
            Vector3 local = transform.InverseTransformPoint(hand.position);
            Vector3 delta = Vector3.ClampMagnitude(local - grip.last, .085f);
            grip.last = local;
            if (delta.sqrMagnitude < .0000001f) return;
            for (int i = 0; i < target.Length; i++)
            {
                Vector3 offset = target[i] - grip.point;
                offset.z *= .2f;
                float radius = Mathf.Max(.05f, influenceRadius);
                float weight = Mathf.Exp(-offset.sqrMagnitude / (radius * radius)) * .88f + .12f;
                Vector3 candidate = target[i] + delta * weight;
                Vector3 deformation = Vector3.ClampMagnitude(candidate - original[i], maximumDeformation);
                deformation.z = Mathf.Clamp(deformation.z, -.42f, .42f);
                target[i] = original[i] + deformation;
            }
            grip.point += delta;
        }

        public override void EndInteraction(Transform hand)
        {
            if (hand) grips.Remove(hand);
        }

        public override void Activate()
        {
            if (!surface) return;
            if (control == Control.Restore) surface.RestoreShape();
            if (control == Control.Palette) surface.ChangePigment();
        }

        void RestoreShape()
        {
            if (target == null) return;
            System.Array.Copy(original, target, target.Length);
            grips.Clear();
        }

        void ChangePigment()
        {
            paletteIndex = (paletteIndex + 1) % Palette.Length;
            if (!runtimeMaterial) return;
            runtimeMaterial.SetColor("_BaseColor", Palette[paletteIndex]);
            if (runtimeMaterial.HasProperty("_Color")) runtimeMaterial.SetColor("_Color", Palette[paletteIndex]);
        }

        Vector3 SurfacePoint(Vector2 uv)
        {
            float fx = Mathf.Clamp01(uv.x) * columns, fy = Mathf.Clamp01(uv.y) * rows;
            int x = Mathf.Min(Mathf.FloorToInt(fx), columns - 1), y = Mathf.Min(Mathf.FloorToInt(fy), rows - 1);
            int i = y * (columns + 1) + x;
            return Vector3.Lerp(Vector3.Lerp(vertices[i], vertices[i + 1], fx - x), Vector3.Lerp(vertices[i + columns + 1], vertices[i + columns + 2], fx - x), fy - y);
        }

        void Update()
        {
            if (control != Control.Surface || vertices == null) return;
            expiredGrips.Clear();
            foreach (var entry in grips)
            {
                if (!entry.Key) expiredGrips.Add(entry.Key);
                else UpdateInteraction(entry.Key);
            }
            foreach (var key in expiredGrips) grips.Remove(key);
            float blend = 1f - Mathf.Exp(-19f * Time.deltaTime);
            bool changed = false;
            for (int i = 0; i < vertices.Length; i++)
            {
                changed |= (target[i] - vertices[i]).sqrMagnitude > .00000001f;
                vertices[i] = Vector3.Lerp(vertices[i], target[i], blend);
            }
            if (changed)
            {
                runtimeMesh.vertices = vertices;
                runtimeMesh.RecalculateNormals();
                runtimeMesh.RecalculateBounds();
                colliderTimer -= Time.deltaTime;
                if (colliderTimer <= 0f)
                {
                    meshCollider.sharedMesh = null; meshCollider.sharedMesh = runtimeMesh;
                    colliderTimer = .08f;
                }
                for (int i = 0; i < content.Length; i++)
                    if (content[i]) content[i].localPosition = SurfacePoint(contentUV[i]) + Vector3.back * .083f;
                float span = Vector3.Distance(SurfacePoint(new Vector2(.02f, .5f)), SurfacePoint(new Vector2(.98f, .5f)));
                gain = Mathf.Clamp01(.5f + (span / (width * .96f) - 1f) * 1.8f);
            }
            if (waveform)
            {
                int count = waveform.positionCount;
                for (int i = 0; i < count; i++)
                {
                    float t = i / (float)(count - 1);
                    float wave = Mathf.Sin(t * 19f - Time.time * 2.2f) * Mathf.Sin(t * Mathf.PI) * (.02f + gain * .11f);
                    waveform.SetPosition(i, SurfacePoint(new Vector2(Mathf.Lerp(.1f, .9f, t), .63f + wave)) + Vector3.back * .045f);
                }
            }
        }

        void OnDisable() { grips.Clear(); }
        void OnDestroy()
        {
            if (runtimeMesh) Destroy(runtimeMesh);
            if (runtimeMaterial) Destroy(runtimeMaterial);
        }
    }
}
