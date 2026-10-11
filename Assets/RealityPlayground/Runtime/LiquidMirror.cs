using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealityPlayground
{

    [DefaultExecutionOrder(200)]
    public sealed class LiquidMirror : PlaygroundTarget
    {
        [SerializeField] MeshFilter surface;
        [SerializeField] Renderer surfaceRenderer;
        [SerializeField] Vector2 dimensions = new Vector2(2.45f, 2.75f);
        [SerializeField, Range(.35f, 1f)] float detachDistance = .64f;
        [SerializeField, Range(.12f, .45f)] float adhesionRadius = .24f;
        [SerializeField, Range(8f, 28f)] float springFrequency = 19f;
        [SerializeField, Range(.25f, 1f)] float springDamping = .48f;

        sealed class HandContact
        {
            public Transform hand;
            public Vector3 previous, anchor, delta;
            public bool hasPrevious, attached;
            public int pinnedVertex = -1;
            public float cooldown;
            public GameObject film;
            public readonly List<FilmPart> filmParts = new List<FilmPart>();
            public readonly List<FilmNode> filmNodes = new List<FilmNode>();
        }

        sealed class FilmPart
        {
            public Renderer source, renderer;
            public SkinnedMeshRenderer skinnedSource;
            public Mesh baked;
        }
        struct FilmNode { public Transform source, copy; }

        struct Ripple { public Vector2 center; public float age, amplitude; }
        readonly HandContact[] contacts = { new HandContact(), new HandContact() };
        readonly Ripple[] ripples = new Ripple[6];
        int nextRipple;
        Mesh mesh;
        Vector3[] rest, positions, velocities, targets;
        float maximumDisplacement;
        float maximumSpeed;

        public Transform SurfaceTransform => surface ? surface.transform : transform;
        public Vector2 SurfaceDimensions => dimensions;
        public float DetachDistance => detachDistance;
        public float MaximumDisplacement => maximumDisplacement;
        public int AttachedHandCount => (contacts[0].attached ? 1 : 0) + (contacts[1].attached ? 1 : 0);
        public int CoatedRendererCount => contacts[0].filmParts.Count + contacts[1].filmParts.Count;
        public bool IsAttached(Transform hand) => hand && ((contacts[0].hand == hand && contacts[0].attached) || (contacts[1].hand == hand && contacts[1].attached));
        public Vector3 AttachedTipWorld(Transform hand)
        {
            foreach (var contact in contacts)
                if (contact.hand == hand && contact.attached)
                    return SurfaceTransform.TransformPoint(contact.anchor + contact.delta);
            return SurfaceTransform.position;
        }

        void Awake() => InitializeMesh();
        void LateUpdate() => StepSimulation(Time.deltaTime);

        public override void BeginInteraction(Transform hand) => ProcessHand(hand);
        public override void UpdateInteraction(Transform hand) => ProcessHand(hand);
        public override void EndInteraction(Transform hand) => ProcessHand(hand);

        void InitializeMesh()
        {
            if (mesh || !surface || !surface.sharedMesh) return;
            mesh = Instantiate(surface.sharedMesh);
            mesh.name = "Live liquid mirror membrane";
            mesh.MarkDynamic();
            surface.sharedMesh = mesh;
            rest = mesh.vertices;
            positions = (Vector3[])rest.Clone();
            velocities = new Vector3[rest.Length];
            targets = new Vector3[rest.Length];
        }

        public void StepSimulation(float deltaTime)
        {
            InitializeMesh();
            if (!mesh) return;
            float dt = Mathf.Clamp(deltaTime, 0, .05f);
            for (int i = 0; i < contacts.Length; i++) contacts[i].cooldown = Mathf.Max(0, contacts[i].cooldown - dt);
            var left = RealityPlayer.LeftHand;
            var right = RealityPlayer.RightHand;
            ProcessHand(left);
            ProcessHand(right);
            for (int i = 0; i < contacts.Length; i++)
            {
                var contact = contacts[i];
                if (contact.hand && contact.hand == (i == 0 ? left : right) && contact.hand.gameObject.activeInHierarchy) continue;
                if (contact.attached) Detach(contact);
                contact.hasPrevious = false;
            }
            for (int i = 0; i < ripples.Length; i++) ripples[i].age += dt;
            UpdateFilms();
            UpdateMembrane(dt);
        }

        public void ProcessHand(Transform hand)
        {
            if (!hand || !surface || !hand.gameObject.activeInHierarchy) return;
            int side = hand == RealityPlayer.LeftHand ? 0 : hand == RealityPlayer.RightHand ? 1 : -1;
            if (side < 0) return;
            var contact = contacts[side];
            if (contact.hand != hand)
            {
                if (contact.attached) Detach(contact);
                contact.hand = hand;
                contact.hasPrevious = false;
            }
            Vector3 current = surface.transform.InverseTransformPoint(hand.position);
            if (contact.attached)
            {
                contact.delta = current - contact.anchor;
                if (surface.transform.TransformVector(contact.delta).magnitude >= detachDistance)
                    Detach(contact);
            }
            else if (contact.hasPrevious && contact.cooldown <= 0 && contact.previous.z < 0 && current.z >= 0)
            {
                float fraction = -contact.previous.z / Mathf.Max(.00001f, current.z - contact.previous.z);
                Vector3 crossing = Vector3.Lerp(contact.previous, current, fraction);

                bool continuous = (hand.position - surface.transform.TransformPoint(contact.previous)).sqrMagnitude < .55f * .55f;
                if (continuous && Mathf.Abs(crossing.x) < dimensions.x * .5f - .09f && Mathf.Abs(crossing.y) < dimensions.y * .5f - .09f)
                {
                    contact.anchor = new Vector3(crossing.x, crossing.y, 0);
                    contact.delta = current - contact.anchor;
                    contact.attached = true;
                    contact.pinnedVertex = FindNearestVertex(contact.anchor);
                    CreateFilm(contact);
                    AddRipple(contact.anchor, .027f);
                }
            }
            contact.previous = current;
            contact.hasPrevious = true;
        }

        void CreateFilm(HandContact contact)
        {
            ClearFilm(contact);

            var sources = contact.hand.GetComponentsInChildren<Renderer>(true);
            contact.film = new GameObject("HandCoating");
            contact.film.transform.SetParent(contact.hand, false);
            var nodes = new Dictionary<Transform, Transform> { { contact.hand, contact.film.transform } };
            foreach (var source in sources)
            {
                if (!source.enabled || !source.gameObject.activeInHierarchy || IsExperimentOverlay(source)) continue;
                Mesh sourceMesh = null;
                var skinned = source as SkinnedMeshRenderer;
                Mesh baked = null;
                if (skinned && skinned.sharedMesh)
                {
                    baked = new Mesh { name = "Mirror Coating Mesh" };
                    baked.MarkDynamic();
                    skinned.BakeMesh(baked);
                    sourceMesh = baked;
                }
                else if (source is MeshRenderer && source.TryGetComponent<MeshFilter>(out var sourceFilter))
                    sourceMesh = sourceFilter.sharedMesh;
                if (!sourceMesh) continue;
                var node = CopyTransformChain(source.transform, contact, nodes);
                var part = new GameObject(ObjectNames.Short("Mirror coating " + source.name));
                part.transform.SetParent(node, false);
                part.AddComponent<MeshFilter>().sharedMesh = sourceMesh;
                var renderer = part.AddComponent<MeshRenderer>();
                var materials = new Material[sourceMesh.subMeshCount];
                for (int i = 0; i < materials.Length; i++) materials[i] = surfaceRenderer.sharedMaterial;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                contact.filmParts.Add(new FilmPart { source = source, renderer = renderer, skinnedSource = skinned, baked = baked });
            }
        }

        static Transform CopyTransformChain(Transform source, HandContact contact, Dictionary<Transform, Transform> nodes)
        {
            if (nodes.TryGetValue(source, out var existing)) return existing;
            var parent = CopyTransformChain(source.parent, contact, nodes);
            var copy = new GameObject(ObjectNames.Short("Mirror coating transform " + source.name)).transform;
            copy.SetParent(parent, false);
            copy.localPosition = source.localPosition;
            copy.localRotation = source.localRotation;
            copy.localScale = source.localScale;
            nodes.Add(source, copy);
            contact.filmNodes.Add(new FilmNode { source = source, copy = copy });
            return copy;
        }

        static bool IsExperimentOverlay(Renderer source)
        {
            if (ObjectNames.StartsWith(source.name, "Mirror coating") || ObjectNames.StartsWith(source.name, "Breach Surface ")) return true;
            foreach (var material in source.sharedMaterials)
                if (material && material.shader && material.shader.name.StartsWith("RealityPlayground/")) return true;
            return false;
        }

        void UpdateFilms()
        {
            foreach (var contact in contacts)
            {
                foreach (var node in contact.filmNodes)
                {
                    if (!node.source || !node.copy) continue;
                    node.copy.localPosition = node.source.localPosition;
                    node.copy.localRotation = node.source.localRotation;
                    node.copy.localScale = node.source.localScale;
                }
                foreach (var part in contact.filmParts)
                {
                    if (!part.renderer) continue;
                    part.renderer.enabled = part.source && part.source.enabled && part.source.gameObject.activeInHierarchy;
                    if (part.skinnedSource && part.baked) part.skinnedSource.BakeMesh(part.baked);
                }
            }
        }

        void ClearFilm(HandContact contact)
        {
            if (contact.film) Destroy(contact.film);
            foreach (var part in contact.filmParts) if (part.baked) Destroy(part.baked);
            contact.film = null;
            contact.filmParts.Clear();
            contact.filmNodes.Clear();
        }

        void Detach(HandContact contact)
        {
            if (!contact.attached) return;
            contact.attached = false;
            contact.cooldown = .22f;
            AddRipple(contact.anchor, .055f);

            ClearFilm(contact);
        }

        void AddRipple(Vector3 point, float amplitude)
        {
            ripples[nextRipple] = new Ripple { center = new Vector2(point.x, point.y), age = 0, amplitude = amplitude };
            nextRipple = (nextRipple + 1) % ripples.Length;
        }

        int FindNearestVertex(Vector3 point)
        {
            InitializeMesh();
            if (rest == null) return -1;
            int result = 0;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < rest.Length; i++)
            {
                float candidate = (rest[i] - point).sqrMagnitude;
                if (candidate >= distance) continue;
                distance = candidate; result = i;
            }
            return result;
        }

        void UpdateMembrane(float dt)
        {
            bool activeRipple = false;
            foreach (var ripple in ripples) activeRipple |= ripple.amplitude > 0 && ripple.age < 3;
            if (AttachedHandCount == 0 && !activeRipple && maximumDisplacement < .00005f && maximumSpeed < .0001f) return;
            float inverseRadiusSquared = 1 / (adhesionRadius * adhesionRadius);
            for (int i = 0; i < rest.Length; i++)
            {
                Vector3 p = rest[i], offset = Vector3.zero;
                float sum = 0;
                foreach (var contact in contacts)
                {
                    if (!contact.attached) continue;
                    float x = p.x - contact.anchor.x, y = p.y - contact.anchor.y;
                    float weight = Mathf.Exp(-(x * x + y * y) * inverseRadiusSquared);
                    offset += contact.delta * weight;
                    sum += weight;
                }
                offset /= Mathf.Max(1, sum);
                float wave = 0;
                foreach (var ripple in ripples)
                {
                    if (ripple.amplitude <= 0 || ripple.age > 3) continue;
                    float radius = Vector2.Distance(new Vector2(p.x, p.y), ripple.center);
                    float front = radius - ripple.age * 1.15f;
                    wave += Mathf.Sin(front * 31) * Mathf.Exp(-front * front * 14) * Mathf.Exp(-ripple.age * 1.9f) * ripple.amplitude;
                }
                float edge = Mathf.Clamp01((dimensions.x * .5f - Mathf.Abs(p.x)) * 14) * Mathf.Clamp01((dimensions.y * .5f - Mathf.Abs(p.y)) * 14);
                targets[i] = rest[i] + (offset + Vector3.forward * wave) * edge;
            }

            int substeps = Mathf.Max(1, Mathf.CeilToInt(dt * 120));
            float step = dt / substeps;
            float stiffness = springFrequency * springFrequency;
            float damping = 2 * springFrequency * springDamping;
            for (int sub = 0; sub < substeps; sub++)
                for (int i = 0; i < positions.Length; i++)
                {
                    velocities[i] += ((targets[i] - positions[i]) * stiffness - velocities[i] * damping) * step;
                    positions[i] += velocities[i] * step;
                }

            foreach (var contact in contacts)
            {
                if (!contact.attached || contact.pinnedVertex < 0) continue;
                int index = contact.pinnedVertex;
                Vector3 tip = contact.anchor + contact.delta;
                velocities[index] = Vector3.ClampMagnitude((tip - positions[index]) / Mathf.Max(.001f, dt), 3);
                positions[index] = tip;
            }
            maximumDisplacement = maximumSpeed = 0;
            for (int i = 0; i < positions.Length; i++)
            {
                maximumDisplacement = Mathf.Max(maximumDisplacement, (positions[i] - rest[i]).magnitude);
                maximumSpeed = Mathf.Max(maximumSpeed, velocities[i].magnitude);
            }
            mesh.vertices = positions;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        public void ApplyReflectionProperties(MaterialPropertyBlock properties)
        {
            properties.SetFloat("_LiquidCoat", 1);
            foreach (var contact in contacts)
                foreach (var part in contact.filmParts)
                    if (part.renderer) part.renderer.SetPropertyBlock(properties);
            properties.SetFloat("_LiquidCoat", 0);
        }

        public void HideFromReflection(bool hidden)
        {
            foreach (var contact in contacts)
                foreach (var part in contact.filmParts)
                    if (part.renderer) part.renderer.forceRenderingOff = hidden;
        }

        void OnDisable()
        {
            foreach (var contact in contacts)
            {
                ClearFilm(contact);
                contact.attached = contact.hasPrevious = false;
            }
        }

        void OnDestroy() { if (mesh) Destroy(mesh); }

        public static LiquidMirror Create(Transform parent, Material material, out Renderer renderer)
        {
            var surfaceObject = new GameObject("MirrorMembrane");
            surfaceObject.transform.SetParent(parent, false);
            surfaceObject.transform.localPosition = new Vector3(0, 1.55f, -.002f);
            var filter = surfaceObject.AddComponent<MeshFilter>();
            filter.sharedMesh = BuildMesh(2.45f, 2.75f, 40, 46);
            renderer = surfaceObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var trigger = surfaceObject.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(2.45f, 2.75f, .04f);
            var liquid = parent.gameObject.AddComponent<LiquidMirror>();
            liquid.surface = filter;
            liquid.surfaceRenderer = renderer;
            return liquid;
        }

        static Mesh BuildMesh(float width, float height, int columns, int rows)
        {
            int count = (columns + 1) * (rows + 1);
            var vertices = new Vector3[count];
            var uvs = new Vector2[count];
            var triangles = new int[columns * rows * 6];
            for (int y = 0; y <= rows; y++)
                for (int x = 0; x <= columns; x++)
                {
                    int index = y * (columns + 1) + x;
                    uvs[index] = new Vector2((float)x / columns, (float)y / rows);
                    vertices[index] = new Vector3((uvs[index].x - .5f) * width, (uvs[index].y - .5f) * height, 0);
                }
            int t = 0;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < columns; x++)
                {
                    int a = y * (columns + 1) + x, b = a + 1, c = a + columns + 1, d = c + 1;
                    triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                    triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                }
            var result = new Mesh { name = "Mirror Mesh" };
            result.vertices = vertices; result.uv = uvs; result.triangles = triangles;
            result.RecalculateNormals(); result.RecalculateBounds();
            return result;
        }
    }
}
