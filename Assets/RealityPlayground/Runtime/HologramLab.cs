using System.Collections.Generic;
using UnityEngine;

namespace RealityPlayground
{

    public sealed class HologramLab : PlaygroundTarget
    {
        public enum Exhibit { MemoryCore, CityForecast, SignalGate }

        [SerializeField] Exhibit exhibit;
        [SerializeField] Transform display;
        [SerializeField] Transform[] rings;
        [SerializeField] Transform[] elements;
        [SerializeField] Renderer[] hologramRenderers;
        [SerializeField] Color baseColor = Color.cyan;
        [SerializeField] float idleRotation = 18;

        int mode;
        public int Mode => mode;
        float pulse;
        float grabYaw;
        Vector3 lastHand;
        Transform heldHand;
        float[] heightTargets;
        MaterialPropertyBlock properties;

        void Awake() { properties = new MaterialPropertyBlock(); }

        static readonly Color[] Colors = {
            new Color(0.02f, 0.95f, 1, 0.65f), new Color(1, 0.025f, 0.48f, 0.6f), new Color(0.58f, 1, 0.02f, 0.58f)
        };

        public static GameObject Create(Transform parent)
        {
            var root = new GameObject("Holograms");
            root.transform.SetParent(parent, false);
            Material chassis = GreyboxUtil.Material("Projector Graphite", new Color(0.065f, 0.075f, 0.09f));
            Material trim = GreyboxUtil.Material("Projector Metal", new Color(0.27f, 0.3f, 0.34f));
            GreyboxUtil.Primitive("Projection laboratory deck", PrimitiveType.Cube, root.transform, new Vector3(0, 0.08f, 0.37f), new Vector3(3.8f, 0.16f, 1.6f), chassis);
            GreyboxUtil.Label("Grip a hologram to change it. Hold and move to reshape it.", root.transform, new Vector3(0, 2.65f, 0.45f), 0.047f, new Color(0.85f, 0.96f, 1));
            for (int index = 0; index < 3; index++)
            {
                var station = new GameObject(ObjectNames.Short(((Exhibit)index).ToString()));
                station.transform.SetParent(root.transform, false);
                station.transform.localPosition = new Vector3((index - 1) * 1.22f, 0, 0.25f);
                var target = station.AddComponent<HologramLab>();
                target.exhibit = (Exhibit)index;
                target.baseColor = Colors[index];
                target.idleRotation = index == 1 ? 8 : 18;
                Material light = GreyboxUtil.Material("Hologram " + index + " light", target.baseColor, 2.2f);
                Material holo = CreateHologramMaterial("Hologram " + index + " scanlines", target.baseColor);
                GreyboxUtil.Primitive("Projector column", PrimitiveType.Cylinder, station.transform, new Vector3(0, 0.51f, 0), new Vector3(0.62f, 0.34f, 0.62f), chassis);
                GreyboxUtil.Primitive("Projector rim", PrimitiveType.Cylinder, station.transform, new Vector3(0, 0.86f, 0), new Vector3(0.91f, 0.045f, 0.91f), trim);
                GreyboxUtil.Primitive("Luminous projection lens", PrimitiveType.Cylinder, station.transform, new Vector3(0, 0.915f, 0), new Vector3(0.71f, 0.01f, 0.71f), light, false);
                GreyboxUtil.Primitive("Volumetric projection", PrimitiveType.Cylinder, station.transform, new Vector3(0, 1.1f, 0), new Vector3(0.38f, 0.18f, 0.38f), holo, false);
                target.display = new GameObject("Floatingproj").transform;
                target.display.SetParent(station.transform, false);
                target.display.localPosition = new Vector3(0, 1.62f, 0);
                var parts = new List<Transform>();
                var orbitRings = new List<Transform>();
                if (index == 0)
                {
                    var core = GreyboxUtil.Primitive("Translucent memory crystal", PrimitiveType.Cube, target.display, Vector3.zero, Vector3.one * 0.39f, holo, false);
                    core.transform.localRotation = Quaternion.Euler(35, 0, 45);
                    parts.Add(core.transform);
                    for (int echo = 0; echo < 6; echo++)
                    {
                        float angle = echo * Mathf.PI / 3;
                        var shard = GreyboxUtil.Primitive("Memory fragment " + echo, PrimitiveType.Cube, target.display, new Vector3(Mathf.Cos(angle) * 0.39f, Mathf.Sin(angle) * 0.28f, 0), Vector3.one * 0.085f, holo, false);
                        shard.transform.localRotation = Quaternion.Euler(35, echo * 45, 45);
                        parts.Add(shard.transform);
                    }
                    for (int ring = 0; ring < 3; ring++)
                    {
                        LineRenderer line = GreyboxUtil.Line("Memory orbit " + ring, target.display, Circle(0.47f, 64), light, 0.006f, true);
                        line.transform.localRotation = Quaternion.Euler(ring * 60, ring * 40, 0);
                        orbitRings.Add(line.transform);
                    }
                }
                else if (index == 1)
                {
                    target.display.localPosition = new Vector3(0, 1.28f, 0);
                    GreyboxUtil.Primitive("City grid", PrimitiveType.Cube, target.display, new Vector3(0, -0.015f, 0), new Vector3(0.8f, 0.02f, 0.8f), holo, false);
                    for (int x = 0; x < 4; x++)
                        for (int z = 0; z < 4; z++)
                        {
                            float height = 0.15f + Mathf.Abs(Mathf.Sin(x * 5.3f + z * 3.7f)) * 0.52f;
                            var tower = GreyboxUtil.Primitive("Forecast building " + x + ":" + z, PrimitiveType.Cube, target.display,
                                new Vector3((x - 1.5f) * 0.19f, height * 0.5f, (z - 1.5f) * 0.19f), new Vector3(0.125f, height, 0.125f), holo, false);
                            parts.Add(tower.transform);
                        }
                    for (int axis = 0; axis < 5; axis++)
                    {
                        float offset = (axis - 2) * 0.19f;
                        GreyboxUtil.Line("North grid " + axis, target.display, new[] { new Vector3(offset, 0, -0.4f), new Vector3(offset, 0, 0.4f) }, light, 0.003f);
                        GreyboxUtil.Line("East grid " + axis, target.display, new[] { new Vector3(-0.4f, 0, offset), new Vector3(0.4f, 0, offset) }, light, 0.003f);
                    }
                    var scanner = GreyboxUtil.Line("Circular city scanner", target.display, Circle(0.51f, 64), light, 0.008f, true);
                    scanner.transform.localRotation = Quaternion.Euler(90, 0, 0);
                    orbitRings.Add(scanner.transform);
                }
                else
                {
                    for (int ring = 0; ring < 4; ring++)
                    {
                        var r = GreyboxUtil.Line("Signal wave " + ring, target.display, Circle(0.28f + ring * 0.065f, 64), light, ring == 0 ? 0.018f : 0.007f, true);
                        r.transform.localPosition = new Vector3(0, 0, ring * 0.06f);
                        orbitRings.Add(r.transform);
                    }
                    var globe = GreyboxUtil.Primitive("Signal envelope", PrimitiveType.Sphere, target.display, Vector3.zero, Vector3.one * 0.62f, holo, false);
                    parts.Add(globe.transform);
                    for (int i = 0; i < 8; i++)
                    {
                        float angle = i * Mathf.PI / 4;
                        var bit = GreyboxUtil.Primitive("Broadcast marker " + i, PrimitiveType.Cube, target.display,
                            new Vector3(Mathf.Cos(angle) * 0.44f, Mathf.Sin(angle) * 0.44f, -0.06f), new Vector3(0.025f, 0.07f, 0.025f), light, false);
                        bit.transform.localRotation = Quaternion.Euler(0, 0, i * 45);
                    }
                }
                target.elements = parts.ToArray();
                target.rings = orbitRings.ToArray();
                target.hologramRenderers = target.display.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in target.hologramRenderers)
                {
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                var select = station.AddComponent<BoxCollider>();
                select.isTrigger = true;
                select.center = new Vector3(0, 1.45f, 0);
                select.size = new Vector3(1.08f, 1.95f, 0.95f);
            }
            return root;
        }

        static Material CreateHologramMaterial(string name, Color color)
        {
            Shader shader = Shader.Find("RealityPlayground/Hologram");
            if (!shader) return GreyboxUtil.Material(name, color, 2, true);
            var material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Intensity", 2.3f);
            material.SetFloat("_ScanFrequency", 90);
            material.SetFloat("_Glitch", 0.16f);
            return material;
        }

        static Vector3[] Circle(float radius, int count)
        {
            Vector3[] points = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count;
                points[i] = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0);
            }
            return points;
        }

        void Start()
        {
            if (exhibit == Exhibit.CityForecast)
            {
                heightTargets = new float[elements.Length];
                for (int i = 0; i < elements.Length; i++) heightTargets[i] = elements[i].localScale.y;
            }
        }

        public override void Activate()
        {
            mode = (mode + 1) % 3;
            pulse = 1;
            if (exhibit == Exhibit.CityForecast)
            {
                if (heightTargets == null) heightTargets = new float[elements.Length];
                for (int i = 0; i < heightTargets.Length; i++)
                    heightTargets[i] = 0.12f + Mathf.Abs(Mathf.Sin(i * 9.51f + mode * 3.61f + Time.time * 0.23f)) * (mode == 1 ? 0.72f : mode == 2 ? 0.25f : 0.52f);
            }
        }

        public override void BeginInteraction(Transform hand)
        {
            if (heldHand && heldHand != hand) return;
            heldHand = hand;
            if (hand) lastHand = transform.InverseTransformPoint(hand.position);
            Activate();
        }

        public override void UpdateInteraction(Transform hand)
        {
            if (!hand || hand != heldHand) return;
            Vector3 current = transform.InverseTransformPoint(hand.position);
            grabYaw += (current.x - lastHand.x) * 220;
            if (exhibit == Exhibit.CityForecast && heightTargets != null)
            {
                float movement = (current.y - lastHand.y) * 1.5f;
                for (int i = 0; i < heightTargets.Length; i++) heightTargets[i] = Mathf.Clamp(heightTargets[i] + movement, 0.06f, 0.9f);
            }
            lastHand = current;
        }

        public override void EndInteraction(Transform hand)
        {
            if (hand == heldHand) heldHand = null;
        }

        void Update()
        {
            if (!display) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            pulse = Mathf.MoveTowards(pulse, 0, Time.deltaTime * 0.7f);
            float time = Time.time;
            float speed = idleRotation * (mode == 2 ? 3 : 1);
            display.localRotation = Quaternion.Euler(0, grabYaw + time * speed, 0);
            if (exhibit == Exhibit.SignalGate)
                display.localRotation = Quaternion.Euler(0, Mathf.Sin(time * 0.5f) * 18 + grabYaw, Mathf.Sin(time * 0.7f) * 6);
            float pop = 1 + pulse * 0.12f * Mathf.Sin((1 - pulse) * 14);
            display.localScale = Vector3.one * pop;
            for (int i = 0; i < rings.Length; i++)
            {
                if (!rings[i]) continue;
                if (exhibit == Exhibit.MemoryCore)
                    rings[i].localRotation = Quaternion.Euler(i * 60 + time * (i + 1) * 9, i * 40 + time * 15, time * 13);
                else if (exhibit == Exhibit.CityForecast)
                    rings[i].localPosition = new Vector3(0, 0.35f + Mathf.Sin(time * 1.7f) * 0.34f, 0);
                else
                {
                    float wave = 1 + Mathf.Sin(time * 3 - i * 0.7f) * 0.045f + pulse * (0.2f + i * 0.12f);
                    rings[i].localScale = Vector3.one * wave;
                    rings[i].localPosition = new Vector3(0, 0, i * 0.06f + pulse * i * 0.08f);
                }
            }
            if (exhibit == Exhibit.MemoryCore)
            {
                for (int i = 1; i < elements.Length; i++)
                {
                    float angle = (i - 1) * Mathf.PI / 3 + time * 0.35f;
                    float radius = mode == 1 ? 0.49f : mode == 2 ? 0.43f : 0.32f;
                    elements[i].localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.75f, Mathf.Sin(angle * 2 + time) * 0.13f);
                    elements[i].Rotate(23 * Time.deltaTime, 42 * Time.deltaTime, 17 * Time.deltaTime, Space.Self);
                }
            }
            else if (exhibit == Exhibit.CityForecast && heightTargets != null)
            {
                for (int i = 0; i < elements.Length; i++)
                {
                    Vector3 scale = elements[i].localScale;
                    scale.y = Mathf.Lerp(scale.y, heightTargets[i], 1 - Mathf.Exp(-Time.deltaTime * 4));
                    elements[i].localScale = scale;
                    Vector3 position = elements[i].localPosition;
                    position.y = scale.y * 0.5f;
                    elements[i].localPosition = position;
                }
            }
            Color tint = Color.Lerp(baseColor, Colors[((int)exhibit + mode) % Colors.Length], 0.65f);
            foreach (Renderer renderer in hologramRenderers)
            {
                if (!renderer || renderer is LineRenderer || renderer.GetComponent<TextMesh>()) continue;
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", tint);
                properties.SetFloat("_Intensity", 2.3f + pulse * 1.6f);
                properties.SetFloat("_Glitch", heldHand ? 0.4f : mode == 2 ? 0.28f : 0.08f);
                renderer.SetPropertyBlock(properties);
            }
        }

        void OnDisable() { heldHand = null; }
    }
}
