using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Events;

namespace RealityPlayground
{

    public sealed class WallBreach : PlaygroundTarget
    {
        [SerializeField] Transform[] bricks;
        [SerializeField] Vector3[] brickPositions;
        [SerializeField] Renderer[] brickRenderers;
        [SerializeField] GameObject missingBrick;
        [SerializeField] GameObject[] apertureBricks;
        [SerializeField] Transform[] crystalFragments;
        [SerializeField] Vector3[] fragmentPositions;
        [SerializeField] Renderer[] crystalRenderers;
        [SerializeField] GameObject realityVolume;
        [SerializeField] Renderer volumeRenderer;
        [SerializeField] Material controllerMaterial;
        [SerializeField] Vector3 opening = new Vector3(0, 1.46f, .47f);
        [SerializeField, Min(.1f)] float disturbanceDuration = 2.6f;
        [Tooltip("Wait for the story's hacking sequence before revealing the breach.")]
        public bool deferRuptureUntilAuthorized;
        public UnityEvent touched=new UnityEvent();
        public bool HasBeenTouched { get; private set; }
        public float HackAmount => hackAmount;
        public override bool UsesTrackedHandForGrab => true;
        BoxCollider touchSurface;
        float touchGateAge, hackAmount;
        Vector3 previousFeet;
        [SerializeField, Min(.1f)] float corruptionHoldSeconds = 4f;
        [SerializeField, Min(.1f)] float corruptionGrowthSpeed = .8f;
        readonly List<ControllerGrowth> controllers = new List<ControllerGrowth>();
        MaterialPropertyBlock wallProperties, volumeProperties;
        float disturbanceAge = -1, nextMeshRefresh;
        bool opened;
        const string EffectPrefix = "Breach Surface ";

        sealed class SurfaceCopy { public Renderer source, copy; public int layer; }
        sealed class ControllerGrowth
        {
            public Transform hand;
            public float amount, hold;
            public readonly List<SurfaceCopy> surfaces = new List<SurfaceCopy>();
            public readonly HashSet<Renderer> copiedSources = new HashSet<Renderer>();
            public MaterialPropertyBlock properties;
            public bool sampled, touchArmed;
            public float clearAge, contactAge;
            public Vector3 previousPosition;
        }

        public bool IsOpen => opened;
        public float RuptureAmount => disturbanceAge < 0 ? 0 : Mathf.Clamp01(disturbanceAge / disturbanceDuration);
        public Vector3 OpeningWorldPosition => transform.TransformPoint(opening);
        public int ActiveControllerSurfaceCount
        {
            get
            {
                int count = 0;
                foreach (var c in controllers)
                    foreach (var s in c.surfaces)
                        if (s.copy && s.copy.enabled && s.copy.gameObject.activeInHierarchy) count++;
                return count;
            }
        }
        public float GetCorruptionAmount(Transform hand)
        {
            foreach (var c in controllers) if (c.hand == hand) return c.amount;
            return 0;
        }
        public int GetControllerSurfaceCount(Transform hand)
        {
            foreach (var c in controllers) if (c.hand == hand) return c.surfaces.Count;
            return 0;
        }
        public bool IsControllerInside(Transform hand)
        {
            if (!opened || !hand) return false;
            Vector3 p = transform.InverseTransformPoint(hand.position) - opening;
            float ellipse = p.x * p.x / (.56f * .56f) + p.y * p.y / (.31f * .31f);
            return ellipse < 1 && p.z > -.115f && p.z < .48f;
        }

        public static GameObject Create(Transform parent)
        {
            var root = new GameObject("Breach");
            root.transform.SetParent(parent, false);
            var breach = root.AddComponent<WallBreach>();
            var mortar = GreyboxUtil.Material("Mortar", new Color(.28f, .255f, .23f));
            var clay = new Material[5];
            for (int i = 0; i < clay.Length; i++)
                clay[i] = ShaderMaterial("RealityPlayground/BreachBrick", "Breach Brick " + i,
                    new Color(.35f + i * .026f, .16f + i * .015f, .10f + i * .012f));
            var crystal = ShaderMaterial("RealityPlayground/BreachBrick", "Breach Shards", Color.white);
            crystal.SetFloat("_Crystal", 1);
            var volumeMaterial = ShaderMaterial("RealityPlayground/BreachVolume", "Breach Volume", Color.white);
            volumeMaterial.SetFloat("_Intensity", 2.25f);
            breach.controllerMaterial = ShaderMaterial("RealityPlayground/ControllerCorruption", "Controller Membrane", Color.white);
            breach.controllerMaterial.SetFloat("_Intensity", 1.8f);

            var resource = GreyboxUtil.Primitive("Controller Material", PrimitiveType.Cube, root.transform,
                Vector3.zero, Vector3.one * .001f, breach.controllerMaterial, false);
            resource.SetActive(false);
            var allBricks = new List<Transform>();
            var allPositions = new List<Vector3>();
            var allRenderers = new List<Renderer>();
            var openingBricks = new List<GameObject>();
            for (int row = 0; row < 10; row++)
            {
                int count = row % 2 == 0 ? 8 : 9;
                for (int col = 0; col < count; col++)
                {
                    bool half = count == 9 && (col == 0 || col == count - 1);
                    float width = half ? .205f : .43f;
                    float x = count == 8 ? (col - 3.5f) * .445f : (col - 4) * .445f;
                    if (half) x += col == 0 ? .115f : -.115f;
                    Vector3 p = new Vector3(x, .235f + row * .245f, .47f);
                    var brick = GreyboxUtil.Primitive("Brick " + row + ":" + col, PrimitiveType.Cube, root.transform,
                        p, new Vector3(width, .23f, .26f), clay[(row * 3 + col) % clay.Length]);
                    allBricks.Add(brick.transform); allPositions.Add(p); allRenderers.Add(brick.GetComponent<Renderer>());
                    if (row == 5 && col == 4) { breach.missingBrick = brick; breach.opening = p; }
                    if ((row == 5 && Mathf.Abs(x) < .5f) || ((row == 4 || row == 6) && Mathf.Abs(x) < .3f))
                        openingBricks.Add(brick);
                }
                float y = .113f + row * .245f;

                if (row >= 4 && row <= 7)
                {
                    for (int side = -1; side <= 1; side += 2)
                        GreyboxUtil.Primitive("Mortar side " + row + ":" + side, PrimitiveType.Cube, root.transform,
                            new Vector3(side * 1.17f, y, .49f), new Vector3(1.21f, .015f, .25f), mortar);
                }
                else GreyboxUtil.Primitive("Mortar bed " + row, PrimitiveType.Cube, root.transform,
                    new Vector3(0, y, .49f), new Vector3(3.56f, .015f, .25f), mortar);
            }
            breach.bricks = allBricks.ToArray(); breach.brickPositions = allPositions.ToArray();
            breach.brickRenderers = allRenderers.ToArray(); breach.apertureBricks = openingBricks.ToArray();
            GreyboxUtil.Primitive("Brick foundation", PrimitiveType.Cube, root.transform,
                new Vector3(0, .05f, .47f), new Vector3(3.74f, .1f, .52f), mortar);
            GreyboxUtil.Primitive("Wall coping", PrimitiveType.Cube, root.transform,
                new Vector3(0, 2.58f, .47f), new Vector3(3.7f, .13f, .4f), mortar);

            breach.realityVolume = GreyboxUtil.Primitive("Breach volume", PrimitiveType.Cube,
                root.transform, breach.opening + new Vector3(0, 0, .49f), new Vector3(1.32f, .74f, 1.23f), volumeMaterial, false);
            breach.volumeRenderer = breach.realityVolume.GetComponent<Renderer>();
            breach.volumeRenderer.shadowCastingMode = ShadowCastingMode.Off; breach.volumeRenderer.receiveShadows = false;
            breach.realityVolume.SetActive(false);
            Mesh prism = CreateCrystalMesh();
            const int fragmentCount = 24;
            breach.crystalFragments = new Transform[fragmentCount];
            breach.fragmentPositions = new Vector3[fragmentCount];
            breach.crystalRenderers = new Renderer[fragmentCount];
            for (int i = 0; i < fragmentCount; i++)
            {
                float a = i * Mathf.PI * 2 / fragmentCount;
                Vector3 p = breach.opening + new Vector3(Mathf.Cos(a) * .61f, Mathf.Sin(a) * .365f, -.145f);
                var shard = new GameObject(ObjectNames.Short("Displaced reality prism " + i));
                shard.transform.SetParent(root.transform, false); shard.transform.localPosition = p;
                shard.transform.localRotation = Quaternion.Euler(Mathf.Sin(i * 4.1f) * 32, i * 47, a * Mathf.Rad2Deg - 90);
                shard.transform.localScale = new Vector3(.055f + (i % 4) * .014f, .08f + (i % 5) * .021f, .11f);
                shard.AddComponent<MeshFilter>().sharedMesh = prism;
                var renderer = shard.AddComponent<MeshRenderer>(); renderer.sharedMaterial = crystal;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                breach.crystalFragments[i] = shard.transform; breach.fragmentPositions[i] = p; breach.crystalRenderers[i] = renderer;
                shard.SetActive(false);
            }
            GreyboxUtil.Label("Bring your controller close, then reach through the opening.", root.transform,
                new Vector3(0, 2.83f, .30f), .055f, new Color(.82f, .87f, .90f));
            var interaction = root.AddComponent<BoxCollider>(); interaction.isTrigger = true;
            interaction.center = new Vector3(0, 1.3f, .47f); interaction.size = new Vector3(3.57f, 2.5f, .29f);
            return root;
        }
        static Material ShaderMaterial(string shaderName, string materialName, Color colour)
        {
            Shader shader = Shader.Find(shaderName);
            if (!shader) return GreyboxUtil.Material(materialName, colour);
            var material = new Material(shader) { name = materialName, enableInstancing = true };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", colour);
            return material;
        }
        static Mesh CreateCrystalMesh()
        {
            var mesh = new Mesh { name = "Breach Crystal" };
            Vector3 top = new Vector3(0, 1, 0), bottom = new Vector3(0, -.65f, 0);
            Vector3[] equator = { new Vector3(-.55f, 0, 0), new Vector3(0, 0, .5f), new Vector3(.55f, 0, 0), new Vector3(0, 0, -.5f) };
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            for (int i = 0; i < 4; i++)
            {
                int start = vertices.Count;
                vertices.Add(top); vertices.Add(equator[i]); vertices.Add(equator[(i + 1) % 4]);
                vertices.Add(bottom); vertices.Add(equator[(i + 1) % 4]); vertices.Add(equator[i]);
                for (int j = 0; j < 6; j++) triangles.Add(start + j);
            }
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
        void Awake()
        {

            ApplyMaterialState(0, 0);
            HideRuptureObjects();
        }
        void OnEnable()
        {
            ArmForFreshTouch();
            ApplyMaterialState(Mathf.SmoothStep(0, 1, RuptureAmount),
                opened ? Mathf.Clamp01((disturbanceAge - disturbanceDuration) * 2.1f) : 0);
            if (opened && realityVolume) realityVolume.SetActive(true);
        }
        public override void Activate()
        {

            if(!isActiveAndEnabled || RealityPlayer.IsXR)return;
            AcceptTouch();
        }
        public void ArmForFreshTouch()
        {
            touchSurface=GetComponent<BoxCollider>();touchGateAge=0;previousFeet=RealityPlayer.FeetPosition;
            foreach(var c in controllers){c.touchArmed=false;c.sampled=false;c.clearAge=c.contactAge=0;}
        }
        void AcceptTouch()
        {
            if(HasBeenTouched || disturbanceAge>=0)return;
            HasBeenTouched=true;touched.Invoke();
            if(!deferRuptureUntilAuthorized)BeginRupture();
        }
        public void BeginRupture(){if(disturbanceAge<0)disturbanceAge=0;}
        public void SetHackProgress(float value){hackAmount=Mathf.Clamp01(value);}
        void ObserveFreshTouch(ControllerGrowth c,float dt)
        {
            if(HasBeenTouched || !touchSurface || !c.hand.gameObject.activeInHierarchy)return;
            Vector3 point=c.hand.position+c.hand.forward*.045f;
            bool jumped=c.sampled && Vector3.Distance(point,c.previousPosition)>.38f;
            c.previousPosition=point;c.sampled=true;
            if(jumped){c.touchArmed=false;c.clearAge=c.contactAge=0;return;}
            float distance=Vector3.Distance(point,touchSurface.ClosestPoint(point));
            if(distance>.14f)
            {
                c.clearAge+=Mathf.Min(dt,.1f);c.contactAge=0;
                if(c.clearAge>=.18f && touchGateAge>=.35f)c.touchArmed=true;
            }
            else
            {
                c.clearAge=0;
                if(c.touchArmed && distance<=.025f)
                {
                    c.contactAge+=Mathf.Min(dt,.05f);
                    if(c.contactAge>=.045f)AcceptTouch();
                }
                else c.contactAge=0;
            }
        }
        public void RegisterController(Transform hand)
        {
            if (!hand || !controllerMaterial) return;
            foreach (var existing in controllers) if (existing.hand == hand) return;
            var c = new ControllerGrowth { hand = hand, properties = new MaterialPropertyBlock() };
            controllers.Add(c); RefreshControllerSurfaces(c);
        }
        void Update() { StepEffects(Time.deltaTime); }

        public void StepEffects(float deltaTime)
        {
            deltaTime = Mathf.Max(0, deltaTime);
            if (wallProperties == null) wallProperties = new MaterialPropertyBlock();
            if (volumeProperties == null) volumeProperties = new MaterialPropertyBlock();
            RegisterController(RealityPlayer.LeftHand); RegisterController(RealityPlayer.RightHand);
            Vector3 feet=RealityPlayer.FeetPosition;
            if(Vector3.Distance(feet,previousFeet)>.2f)ArmForFreshTouch();
            previousFeet=feet;touchGateAge+=Mathf.Min(deltaTime,.1f);
            bool refresh = Time.time >= nextMeshRefresh;
            if (refresh) nextMeshRefresh = Time.time + .6f;
            for (int i = controllers.Count - 1; i >= 0; i--)
            {
                var c = controllers[i];
                if (!c.hand) { DestroyControllerSurfaces(c); controllers.RemoveAt(i); continue; }
                if (refresh) RefreshControllerSurfaces(c);
                ObserveFreshTouch(c,deltaTime);
            }
            if (disturbanceAge >= 0) disturbanceAge += deltaTime;
            if (!opened && disturbanceAge >= disturbanceDuration)
            {
                opened = true;
                if (missingBrick) missingBrick.SetActive(false);
                if (realityVolume) realityVolume.SetActive(true);
            }
            AnimateWall();
            foreach (var c in controllers)
            {
                bool inside = c.hand.gameObject.activeInHierarchy && IsControllerInside(c.hand);
                if (inside) c.hold = corruptionHoldSeconds; else c.hold = Mathf.Max(0, c.hold - deltaTime);
                float target = inside || c.hold > 0 ? 1 : 0;
                c.amount = Mathf.MoveTowards(c.amount, target, deltaTime * (target > 0 ? corruptionGrowthSpeed : .22f));
                UpdateControllerSurfaces(c);
            }
        }
        void AnimateWall()
        {
            float rupture = Mathf.SmoothStep(0, 1, RuptureAmount);
            ApplyMaterialState(rupture, opened ? Mathf.Clamp01((disturbanceAge - disturbanceDuration) * 2.1f) : 0);
            if (bricks != null)
                for (int i = 0; i < bricks.Length; i++)
                {
                    if (!bricks[i] || brickPositions == null || i >= brickPositions.Length) continue;
                    Vector3 radial = brickPositions[i] - opening; radial.z = 0;
                    float nearby = Mathf.Clamp01(1 - radial.magnitude / 1.45f) * rupture;
                    float oscillation = Mathf.Sin(Time.time * 1.7f + i * 5.17f) * .5f + .5f;
                    float phase = Mathf.Sin(Time.time * 4.1f + Mathf.Floor(i / 8f) * 11.7f);
                    bricks[i].localPosition = brickPositions[i] + new Vector3(phase * .027f, 0, -oscillation * .075f) * nearby;
                    bricks[i].localRotation = Quaternion.Euler(0, phase * nearby * 2.5f, phase * nearby * 1.3f);
                }
            if (opened && apertureBricks != null)
                for (int i = 0; i < apertureBricks.Length; i++)
                    if (apertureBricks[i] && disturbanceAge > disturbanceDuration + i * .07f) apertureBricks[i].SetActive(false);
            if (crystalFragments != null)
                for (int i = 0; i < crystalFragments.Length; i++)
                {
                    if (!crystalFragments[i] || fragmentPositions == null || i >= fragmentPositions.Length) continue;
                    crystalFragments[i].gameObject.SetActive(rupture > .55f);
                    float emerge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, .95f, rupture));
                    Vector3 p = fragmentPositions[i];
                    p.z += (1 - emerge) * .23f + Mathf.Sin(Time.time * .95f + i * 2.3f) * .045f * emerge;
                    crystalFragments[i].localPosition = p;
                }
        }
        void ApplyMaterialState(float rupture, float volumeRupture)
        {
            if (wallProperties == null) wallProperties = new MaterialPropertyBlock();
            if (volumeProperties == null) volumeProperties = new MaterialPropertyBlock();
            wallProperties.SetFloat("_Rupture", rupture);
            wallProperties.SetFloat("_Hack",hackAmount*(1-rupture));
            wallProperties.SetVector("_BreachOrigin", OpeningWorldPosition);
            wallProperties.SetVector("_BreachRight", transform.right);
            wallProperties.SetVector("_BreachUp", transform.up);
            wallProperties.SetVector("_BreachForward", transform.forward);
            if (brickRenderers != null) foreach (var r in brickRenderers) if (r) r.SetPropertyBlock(wallProperties);
            if (crystalRenderers != null) foreach (var r in crystalRenderers) if (r) r.SetPropertyBlock(wallProperties);
            if (volumeRenderer)
            {
                volumeProperties.SetFloat("_Rupture", volumeRupture);
                volumeRenderer.SetPropertyBlock(volumeProperties);
            }
        }
        void HideRuptureObjects()
        {
            if (realityVolume) realityVolume.SetActive(false);
            if (crystalFragments != null)
                foreach (var fragment in crystalFragments) if (fragment) fragment.gameObject.SetActive(false);
        }
        void RefreshControllerSurfaces(ControllerGrowth c)
        {

            Renderer[] sources = c.hand.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer source in sources)
            {
                if (!source || ObjectNames.StartsWith(source.name, EffectPrefix) || c.copiedSources.Contains(source)) continue;
                bool isEffect = false;
                foreach (Material sourceMaterial in source.sharedMaterials)
                    if (sourceMaterial && sourceMaterial.shader && sourceMaterial.shader.name.StartsWith("RealityPlayground/")) { isEffect = true; break; }
                if (isEffect) continue;
                var skin = source as SkinnedMeshRenderer;
                var filter = source is MeshRenderer ? source.GetComponent<MeshFilter>() : null;
                Mesh mesh = skin ? skin.sharedMesh : filter ? filter.sharedMesh : null;
                if (!mesh) continue;
                c.copiedSources.Add(source);
                for (int layer = 0; layer < 2; layer++)
                {
                    var shell = new GameObject(ObjectNames.Short(EffectPrefix + source.name + (layer == 0 ? " Infection" : " Membrane")));
                    shell.layer = source.gameObject.layer;
                    shell.transform.SetParent(source.transform, false);
                    Renderer renderer;
                    if (skin)
                    {
                        var copy = shell.AddComponent<SkinnedMeshRenderer>();
                        copy.sharedMesh = mesh; copy.bones = skin.bones; copy.rootBone = skin.rootBone;
                        copy.localBounds = skin.localBounds; copy.quality = skin.quality; copy.updateWhenOffscreen = skin.updateWhenOffscreen;
                        renderer = copy;
                    }
                    else
                    {
                        shell.AddComponent<MeshFilter>().sharedMesh = mesh;
                        renderer = shell.AddComponent<MeshRenderer>();
                    }
                    var materials = new Material[Mathf.Max(1, mesh.subMeshCount)];
                    for (int m = 0; m < materials.Length; m++) materials[m] = controllerMaterial;
                    renderer.sharedMaterials = materials; renderer.shadowCastingMode = ShadowCastingMode.Off;
                    renderer.receiveShadows = false; renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                    renderer.enabled = false;
                    c.surfaces.Add(new SurfaceCopy { source = source, copy = renderer, layer = layer });
                }
            }
        }
        void UpdateControllerSurfaces(ControllerGrowth c)
        {
            var hand = c.hand;
            c.properties.SetFloat("_Growth", c.amount);
            c.properties.SetVector("_HandOrigin", hand.position);
            c.properties.SetVector("_HandRight", hand.right);
            c.properties.SetVector("_HandUp", hand.up);
            c.properties.SetVector("_HandForward", hand.forward);
            foreach (var surface in c.surfaces)
            {
                if (!surface.copy) continue;
                surface.copy.enabled = c.amount > .003f && surface.source && surface.source.enabled;
                if (!surface.copy.enabled) continue;
                if (surface.source is SkinnedMeshRenderer sourceSkin && surface.copy is SkinnedMeshRenderer copySkin)
                    for (int i = 0; i < sourceSkin.sharedMesh.blendShapeCount; i++) copySkin.SetBlendShapeWeight(i, sourceSkin.GetBlendShapeWeight(i));
                c.properties.SetFloat("_ShellLayer", surface.layer);
                surface.copy.SetPropertyBlock(c.properties);
            }
        }
        static void DestroyControllerSurfaces(ControllerGrowth c)
        {
            foreach (var s in c.surfaces) if (s.copy) Destroy(s.copy.gameObject);
            c.surfaces.Clear(); c.copiedSources.Clear();

        }
        void OnDisable()
        {
            hackAmount=0;
            ApplyMaterialState(0, 0);
            HideRuptureObjects();
            foreach (var c in controllers) foreach (var s in c.surfaces) if (s.copy) s.copy.enabled = false;
        }
        void OnDestroy()
        {
            foreach (var c in controllers) DestroyControllerSurfaces(c);
            controllers.Clear();
        }
    }
}

