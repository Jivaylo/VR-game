using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Filtering;

namespace RealityPlayground
{

    public sealed class RealityShift : PlaygroundTarget, IXRSelectFilter
    {
        [SerializeField] RealityShift controller;
        [SerializeField] Transform destination;
        [SerializeField] Transform veil;
        [SerializeField] MeshRenderer veilRenderer;
        [SerializeField] MeshRenderer buttonRenderer;
        [SerializeField] Transform fractureRoot;
        [SerializeField] Transform[] fragments;
        [SerializeField] Transform[] worldFragments;
        [SerializeField, Min(2f)] float duration = 4.8f;
        [SerializeField, Range(.2f, 1.5f)] float visualIntensity = 1.1f;
        [Tooltip("Contact tolerance outside the button collider, in metres.")]
        [SerializeField, Range(.005f, .08f)] float physicalContactPadding = .035f;
        Collider pressCollider;
        XRSimpleInteractable buttonInteractable;
        readonly List<XRPokeInteractor> pokePoints = new List<XRPokeInteractor>();
        Material veilMaterial, fragmentMaterial, buttonMaterial;
        Transform veilOriginalParent;
        Vector3 returnFloor, buttonRest;
        Vector3[] worldRest;
        Quaternion[] worldRotations;
        Vector3 effectOrigin;
        Quaternion effectRotation;
        UniversalAdditionalCameraData cameraData;
        CameraOverrideOption previousOpaqueOption;
        float elapsed, cooldownUntil, push;
        bool transitioning, moved, alternate, contactLatched;
        bool isRelay, cameraSettingsChanged, transitionUsedCamera;
        Transform transitionHead;
        Camera viewingCamera;
        Color buttonEmission;

        public bool IsTransitioning => controller ? controller.IsTransitioning : transitioning;
        public bool IsAlternate => controller ? controller.IsAlternate : alternate;
        public override bool SupportsTriggerActivation => true;
        public float TransitionProgress => controller ? controller.TransitionProgress : (transitioning ? Mathf.Clamp01(elapsed / duration) : 0f);
        public bool canProcess => isActiveAndEnabled;

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {

            if (!(interactor is XRPokeInteractor poke)) return true;
            if (!poke) return false;
            var tip = poke.GetAttachTransform(interactable);
            return tip && DistanceToButton(tip.position) <= physicalContactPadding + .025f;
        }

        void OnEnable()
        {
            buttonInteractable=GetComponent<XRSimpleInteractable>();
            if(buttonInteractable) buttonInteractable.selectFilters.Add(this);
        }

        public static void ConfigurePoke(RealityShift button)
        {
            if (!button) return;
            var interactable=button.GetComponent<XRSimpleInteractable>();
            if (!interactable) return;
            var filter=button.GetComponent<XRPokeFilter>();
            if (!filter) filter=button.gameObject.AddComponent<XRPokeFilter>();
            filter.pokeInteractable=interactable;
            filter.pokeCollider=button.GetComponent<Collider>();
            filter.pokeConfiguration=new PokeThresholdDatumProperty(new PokeThresholdData
            {
                pokeDirection=PokeAxis.Y,
                enablePokeAngleThreshold=false,
                interactionDepthOffset=.015f
            });
        }

        public static GameObject Create(Transform parent)
        {
            var root = new GameObject("ShiftStage"); root.transform.SetParent(parent, false);
            var black = GreyboxUtil.Material("Shift basalt", new Color(.022f, .033f, .044f));
            var stone = GreyboxUtil.Material("Shift fractured ceramic", new Color(.35f, .39f, .45f));
            var coral = GreyboxUtil.Material("Shift coral fracture", new Color(1f, .08f, .2f), 3f);
            var cyan = GreyboxUtil.Material("Shift cyan fracture", new Color(.04f, .8f, 1f), 3.2f);
            var purple = GreyboxUtil.Material("Shift violet fracture", new Color(.62f, .055f, 1f), 2.7f);
            GreyboxUtil.Label("Press to cross.\nPress the button inside to return.", root.transform, new Vector3(0f, .5f, -.43f), .075f, new Color(.76f, .85f, .87f));
            var stage=GreyboxUtil.Primitive("Shift stage", PrimitiveType.Cylinder, root.transform, new Vector3(0f, .07f, .15f), new Vector3(3f, .07f, 2.1f), black, false);

            stage.AddComponent<BoxCollider>().size=new Vector3(1,2,1);
            GreyboxUtil.Primitive("Button plinth", PrimitiveType.Cube, root.transform, new Vector3(0f, .7f, .05f), new Vector3(.78f, 1.18f, .56f), black);
            GreyboxUtil.Primitive("Split in console", PrimitiveType.Cube, root.transform, new Vector3(0f, .79f, -.241f), new Vector3(.018f, .99f, .026f), coral, false);
            var button = GreyboxUtil.Primitive("Shift Button", PrimitiveType.Sphere, root.transform, new Vector3(0f, 1.39f, -.14f), new Vector3(.43f, .23f, .43f), coral);
            var shift = button.AddComponent<RealityShift>(); shift.buttonRenderer = button.GetComponent<MeshRenderer>();
            for (int i = 0; i < 7; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float y = .3f + i * .33f;
                var rib = GreyboxUtil.Primitive("Dislocated doorway rib " + i, PrimitiveType.Cube, root.transform, new Vector3(side * (1.13f + Mathf.Sin(i) * .1f), y, .78f + i * .045f), new Vector3(.1f, .63f, .15f), black, false);
                rib.transform.localRotation = Quaternion.Euler(0f, 0f, side * (i * 3f));
                GreyboxUtil.Primitive("Rib fault " + i, PrimitiveType.Cube, rib.transform, new Vector3(0f, 0f, -.53f), new Vector3(.37f, .93f, .08f), i % 2 == 0 ? cyan : purple, false);
            }
            CreateOtherworld(root.transform, shift, black, stone, coral, cyan, purple);
            var veilAsset = new Material(Shader.Find("RealityPlayground/TransitionVeil")) { name = "Shift Refraction" };
            var overlay = GreyboxUtil.Primitive("Transition Veil", PrimitiveType.Quad, root.transform, new Vector3(0f, 1.6f, -.65f), Vector3.one, veilAsset, false);
            shift.veil = overlay.transform; shift.veilRenderer = overlay.GetComponent<MeshRenderer>();
            shift.veilRenderer.shadowCastingMode = ShadowCastingMode.Off; shift.veilRenderer.receiveShadows = false; shift.veilRenderer.enabled = false;
            var shardAsset = new Material(Shader.Find("RealityPlayground/RealityShard")) { name = "Shift Shards" };
            shift.fractureRoot = new GameObject("Worldspacefractureshell").transform;
            shift.fractureRoot.SetParent(root.transform, false);
            shift.fragments = new Transform[36];
            var triangle = new Mesh { name = "Glass Shard" };
            triangle.vertices = new[] { new Vector3(-.61f, -.37f, 0f), new Vector3(.54f, -.22f, 0f), new Vector3(.13f, .89f, 0f) };
            triangle.uv = new[] { Vector2.zero, Vector2.right, Vector2.up }; triangle.triangles = new[] { 0, 2, 1 }; triangle.RecalculateNormals(); triangle.RecalculateBounds();
            for (int i = 0; i < shift.fragments.Length; i++)
            {
                var shard = new GameObject(ObjectNames.Short("Detached reality shard " + i)); shard.transform.SetParent(shift.fractureRoot, false);
                shard.AddComponent<MeshFilter>().sharedMesh = triangle;
                var renderer = shard.AddComponent<MeshRenderer>(); renderer.sharedMaterial = shardAsset; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                shift.fragments[i] = shard.transform;
            }
            shift.fractureRoot.gameObject.SetActive(false);
            return root;
        }

        static void CreateOtherworld(Transform parent, RealityShift shift, Material black, Material stone, Material coral, Material cyan, Material purple)
        {
            var world = new GameObject("AlternateWorld").transform; world.SetParent(parent, false); world.localPosition = new Vector3(0f, 0f, 40f);
            GreyboxUtil.Primitive("Otherworld floor", PrimitiveType.Cube, world, new Vector3(0f, -.15f, 1f), new Vector3(10f, .3f, 12f), black);
            GreyboxUtil.Primitive("Otherworld back", PrimitiveType.Cube, world, new Vector3(0f, 3.5f, 7f), new Vector3(10f, 7f, .3f), black);
            GreyboxUtil.Primitive("Otherworld front", PrimitiveType.Cube, world, new Vector3(0f, 3.5f, -5f), new Vector3(10f, 7f, .3f), black);
            for (int side = -1; side <= 1; side += 2) GreyboxUtil.Primitive("Otherworld side", PrimitiveType.Cube, world, new Vector3(side * 5f, 3.5f, 1f), new Vector3(.3f, 7f, 12f), black);
            GreyboxUtil.Primitive("Otherworld roof", PrimitiveType.Cube, world, new Vector3(0f, 7f, 1f), new Vector3(10f, .3f, 12f), black);
            var moving = new List<Transform>();
            for (int i = 0; i < 24; i++)
            {
                float side = i % 2 == 0 ? -1f : 1f;
                float z = (i / 2) * .85f - 3.3f;
                float y = 1.8f + Mathf.Sin(i * 1.63f) * .7f;
                var slab = GreyboxUtil.Primitive("Floating Block " + i, PrimitiveType.Cube, world, new Vector3(side * (3.6f + Mathf.Sin(i) * .35f), y, z), new Vector3(.62f, 3.2f + Mathf.Cos(i) * .8f, .54f), stone, false);
                slab.transform.localRotation = Quaternion.Euler(i * 7f, i * 23f, side * (13f + i % 4 * 9f));
                GreyboxUtil.Primitive("Fragment Seam", PrimitiveType.Cube, slab.transform, new Vector3(0f, .04f, -.52f), new Vector3(1.02f, .016f, .027f), i % 3 == 0 ? coral : cyan, false);
                moving.Add(slab.transform);
            }
            for (int ring = 0; ring < 6; ring++)
            {
                var holder = new GameObject(ObjectNames.Short("Angular Frame " + ring)).transform; holder.SetParent(world, false); holder.localPosition = new Vector3(0f, 3.05f, 4.5f + ring * .13f);
                float radius = 2.15f - ring * .26f;
                var loop = new Vector3[6];
                for (int j = 0; j < loop.Length; j++) { float a = j * Mathf.PI / 3f + ring * .16f; loop[j] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f); }
                GreyboxUtil.Line("Luminous torn edge", holder, loop, ring % 2 == 0 ? purple : cyan, .025f + ring * .003f, true);
                moving.Add(holder);
            }
            for (int i = 0; i < 13; i++)
            {
                float x = Mathf.Sin(i * 2.4f) * 2.2f, z = Mathf.Cos(i * 1.7f) * 3.4f + 1f;
                var floating = GreyboxUtil.Primitive("Suspended world voxel " + i, PrimitiveType.Cube, world, new Vector3(x, 4.25f + (i % 3) * .38f, z), Vector3.one * (.25f + (i % 4) * .1f), i % 3 == 0 ? purple : stone, false);
                floating.transform.localRotation = Quaternion.Euler(i * 17f, 45f, i * 21f); moving.Add(floating.transform);
            }
            for (int i = -3; i <= 3; i++)
            {
                Vector3[] crack = { new Vector3(i * 1.15f, .012f, -4f), new Vector3(i * .73f + .18f, .012f, -.2f), new Vector3(i * 1.11f - .25f, .012f, 2.4f), new Vector3(i * .72f, .012f, 6.4f) };
                GreyboxUtil.Line("Floor fault", world, crack, i % 2 == 0 ? cyan : purple, .018f);
            }
            shift.worldFragments = moving.ToArray();
            for (int side = 0; side < 2; side++)
            {
                float z = side == 0 ? 1.4f : -2.45f;
                GreyboxUtil.Primitive("Return pedestal " + side, PrimitiveType.Cube, world, new Vector3(0f, .48f, z), new Vector3(.8f, .96f, .6f), black);
                var button = GreyboxUtil.Primitive("Return to playground " + side, PrimitiveType.Sphere, world, new Vector3(0f, 1.14f, z), new Vector3(.4f, .23f, .4f), cyan);
                var relay = button.AddComponent<RealityShift>(); relay.controller = shift; relay.buttonRenderer = button.GetComponent<MeshRenderer>();
                var text = GreyboxUtil.Label("Press to return.", world, new Vector3(0f, 1.55f, z), .075f, Color.white);
                if (side == 1) text.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            }
            var marker = new GameObject("Destinationfloor").transform; marker.SetParent(world, false); marker.localPosition = new Vector3(0f, 0f, -.35f); shift.destination = marker;
            for (int i = 0; i < 2; i++)
            {
                var lamp = new GameObject("Otherworldcolourfill").AddComponent<Light>(); lamp.transform.SetParent(world, false); lamp.transform.localPosition = new Vector3(i == 0 ? -2.5f : 2.5f, 3.6f, 1.5f);
                lamp.type = LightType.Point; lamp.color = i == 0 ? new Color(.15f, .8f, 1f) : new Color(.85f, .12f, .64f); lamp.intensity = 5f; lamp.range = 12f; lamp.shadows = LightShadows.None;
            }
        }

        void Awake()
        {

            isRelay = controller || (!destination && !veil && !fractureRoot &&
                (fragments == null || fragments.Length == 0) && (worldFragments == null || worldFragments.Length == 0));
            pressCollider=GetComponent<Collider>();
            buttonRest = transform.localPosition;
            if (buttonRenderer) { buttonMaterial = buttonRenderer.material; buttonEmission = buttonMaterial.GetColor("_EmissionColor"); }
            if (isRelay) return;
            if (veilRenderer) veilMaterial = veilRenderer.material;
            if (veil) veilOriginalParent = veil.parent;
            if (fragments != null && fragments.Length > 0)
            {
                for (int i = 0; i < fragments.Length; i++)
                {
                    if (!fragments[i]) continue;
                    var renderer = fragments[i].GetComponent<MeshRenderer>();
                    if (!renderer || !renderer.sharedMaterial) continue;
                    if (!fragmentMaterial) fragmentMaterial = new Material(renderer.sharedMaterial);
                    renderer.sharedMaterial = fragmentMaterial;
                    var properties = new MaterialPropertyBlock(); properties.SetFloat("_Seed", i * .173f); renderer.SetPropertyBlock(properties);
                }
            }
            if (worldFragments != null)
            {
                worldRest = new Vector3[worldFragments.Length]; worldRotations = new Quaternion[worldFragments.Length];
                for (int i = 0; i < worldFragments.Length; i++)
                {
                    if (!worldFragments[i]) continue;
                    worldRest[i] = worldFragments[i].localPosition; worldRotations[i] = worldFragments[i].localRotation;
                }
            }
        }

        public override void Activate()
        {
            if (Time.unscaledTime < cooldownUntil || IsTransitioning) return;
            push = 1f; cooldownUntil = Time.unscaledTime + .65f;
            if (isRelay) { if (controller) controller.Activate(); return; }
            if (!RealityPlayer.Head) return;
            if (!alternate) returnFloor = RealityPlayer.FeetPosition;
            var head = RealityPlayer.Head; transitionHead = head;
            viewingCamera = head.GetComponent<Camera>(); if (!viewingCamera) viewingCamera = Camera.main;
            transitionUsedCamera = viewingCamera;
            if (viewingCamera)
            {
                cameraData = viewingCamera.GetUniversalAdditionalCameraData(); previousOpaqueOption = cameraData.requiresColorOption; cameraData.requiresColorOption = CameraOverrideOption.On;
                cameraSettingsChanged = true;
            }
            if (veil)
            {
                veil.SetParent(head, false);
                float distance = viewingCamera ? Mathf.Max(.26f, viewingCamera.nearClipPlane + .08f) : .26f;
                veil.localPosition = Vector3.forward * distance; veil.localRotation = Quaternion.identity; veil.localScale = Vector3.one * distance * 10f;
            }
            if (veilRenderer) veilRenderer.enabled = true;
            effectOrigin = head.position; effectRotation = Quaternion.Euler(0f, head.eulerAngles.y, 0f);
            if (fractureRoot) { fractureRoot.gameObject.SetActive(true); fractureRoot.SetPositionAndRotation(effectOrigin, effectRotation); }
            elapsed = 0f; moved = false; transitioning = true; ApplyEffect(0f);
        }

        void Update()
        {
            PollPhysicalPress();
            push = Mathf.MoveTowards(push, 0f, Time.unscaledDeltaTime * 3.5f);
            transform.localPosition = buttonRest + Vector3.down * (push * .045f);
            if (buttonMaterial) buttonMaterial.SetColor("_EmissionColor", buttonEmission * (1f + push * 1.2f + Mathf.Sin(Time.time * 1.7f) * .12f));
            if (isRelay) return;
            if (worldRest != null && worldFragments != null)
                for (int i = 0; i < Mathf.Min(worldRest.Length, worldFragments.Length); i++)
                {
                    if (!worldFragments[i]) continue;
                    worldFragments[i].localPosition = worldRest[i] + Vector3.up * (Mathf.Sin(Time.time * .47f + i * 1.3f) * .11f);
                    worldFragments[i].localRotation = worldRotations[i] * Quaternion.Euler(Mathf.Sin(Time.time * .22f + i) * 4f, 0f, Mathf.Sin(Time.time * .3f + i * .8f) * 8f);
                }
            if (!transitioning) return;

            if (!transitionHead || !transitionHead.gameObject.activeInHierarchy ||
                (transitionUsedCamera && (!viewingCamera || !viewingCamera.isActiveAndEnabled)) || RealityPlayer.Head != transitionHead)
            {
                FinishTransition();
                return;
            }
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(2f, duration)); ApplyEffect(t);
            if (!moved && t >= .5f)
            {

                moved = true;
                if (TryGetLanding(out var landing))
                {
                    RealityPlayer.TeleportTo(landing);
                    alternate = !alternate;
                }
                else Debug.LogWarning("Reality Shift: the landing marker or its floor is missing. Finished the visual effect in place.", this);
                effectOrigin = transitionHead.position;
                if (fractureRoot) fractureRoot.SetPositionAndRotation(effectOrigin, effectRotation);
            }
            if (t >= 1f) FinishTransition();
        }

        bool TryGetLanding(out Vector3 landing)
        {
            landing = default;
            if (!alternate && (!destination || !destination.gameObject.activeInHierarchy)) return false;
            var requested = alternate ? returnFloor : destination.position;

            var hits = Physics.RaycastAll(requested + Vector3.up * .35f, Vector3.down, .9f, ~0, QueryTriggerInteraction.Ignore);
            foreach (var hit in hits)
            {
                if (hit.normal.y < .65f || Mathf.Abs(hit.point.y - requested.y) > .5f) continue;
                landing = new Vector3(requested.x, hit.point.y, requested.z);
                return true;
            }
            return false;
        }

        void PollPhysicalPress()
        {
            if (!RealityPlayer.IsXR) { contactLatched=false; return; }
            float distance=Mathf.Min(HandDistance(RealityPlayer.LeftHand),HandDistance(RealityPlayer.RightHand));
            if (distance > physicalContactPadding + .07f) contactLatched = false;
            if (!contactLatched && distance <= physicalContactPadding) { contactLatched = true; Activate(); }
        }

        public float DistanceToButton(Vector3 point)
        {
            if (!pressCollider) pressCollider=GetComponent<Collider>();
            return pressCollider && pressCollider.enabled
                ? Vector3.Distance(point,pressCollider.ClosestPoint(point))
                : float.PositiveInfinity;
        }

        float HandDistance(Transform hand)
        {
            if (!hand || !hand.gameObject.activeInHierarchy) return float.PositiveInfinity;
            float distance=DistanceToButton(hand.position);

            pokePoints.Clear(); hand.GetComponentsInChildren(false,pokePoints);
            foreach (var poke in pokePoints)
                if (poke.isActiveAndEnabled)
                {
                    var tip = poke.GetAttachTransform(null);
                    if (tip) distance=Mathf.Min(distance,DistanceToButton(tip.position));
                }
            return distance;
        }

        void ApplyEffect(float t)
        {
            float gather = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, .32f, t));
            float release = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.66f, 1f, t));
            float amount = gather * release;
            float conceal = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.4f, .445f, t)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.58f, .65f, t)));
            if (veilMaterial) { veilMaterial.SetFloat("_Amount", amount); veilMaterial.SetFloat("_Intensity", visualIntensity); veilMaterial.SetFloat("_Phase", elapsed); veilMaterial.SetFloat("_Progress", t); }
            if (fragmentMaterial) { fragmentMaterial.SetFloat("_Amount", amount * (1f - conceal)); fragmentMaterial.SetFloat("_Phase", elapsed); }
            if (fragments == null) return;
            float burst = t < .5f ? Mathf.SmoothStep(0f, 1f, t / .45f) : 1f - Mathf.SmoothStep(0f, 1f, (t - .6f) / .4f);
            for (int i = 0; i < fragments.Length; i++)
            {
                if (!fragments[i]) continue;
                float h = 1f - 2f * ((i + .5f) / fragments.Length), angle = i * 2.399963f + Mathf.Sin(elapsed * .8f + i) * .13f;
                float r = Mathf.Sqrt(1f - h * h);
                var direction = new Vector3(Mathf.Cos(angle) * r, h * .78f, Mathf.Sin(angle) * r).normalized;
                float distance = Mathf.Lerp(3.5f + (i % 3) * .3f, 1.05f + (i % 4) * .14f, burst);
                fragments[i].localPosition = direction * distance + Vector3.up * Mathf.Sin(elapsed * 1.6f + i) * .11f;
                fragments[i].localRotation = Quaternion.LookRotation(-direction, Vector3.up) * Quaternion.Euler(Mathf.Sin(elapsed + i) * 17f, 0f, i * 47f + elapsed * (i % 2 == 0 ? 22f : -17f));
                fragments[i].localScale = Vector3.one * (.42f + (i % 5) * .16f) * Mathf.Lerp(.2f, 1.2f, amount);
            }
        }

        void FinishTransition()
        {
            transitioning = false;
            if (cameraSettingsChanged && cameraData) cameraData.requiresColorOption = previousOpaqueOption;
            cameraSettingsChanged = false;
            cameraData = null;
            transitionHead = null;
            transitionUsedCamera = false;
            if (veilRenderer) veilRenderer.enabled = false;
            if (veil) veil.SetParent(veilOriginalParent, false);
            if (fractureRoot) fractureRoot.gameObject.SetActive(false);
        }

        void OnDisable()
        {
            if(buttonInteractable) buttonInteractable.selectFilters.Remove(this);
            if (!isRelay && transitioning) FinishTransition();
        }
        void OnDestroy()
        {
            if (Application.isPlaying && veilMaterial && veil && veil.parent != veilOriginalParent) Destroy(veil.gameObject);
            if (veilMaterial) Destroy(veilMaterial); if (fragmentMaterial) Destroy(fragmentMaterial); if (buttonMaterial) Destroy(buttonMaterial);
        }
    }
}
