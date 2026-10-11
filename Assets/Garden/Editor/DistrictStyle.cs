using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Garden.Editor
{
    public static class DistrictStyle
    {
        const string Folder = "Assets/Garden/Art/Districts/";
        const string Shared = "Assets/RealityPlayground/Materials and Meshes/";
        static Transform world, physical, digital;
        static TMP_FontAsset font;
        static NavPad[] pads;
        static Material stone, dark, metal, cream, wood, sage, sand, blue, rust, violet, window;
        static Material amber, mint, cyan, lilac, holoAmber, holoMint, holoCyan, holoLilac, plants;
        static Mesh ringMesh, leafMesh;
        static readonly List<DistrictMotion.Group> groups = new List<DistrictMotion.Group>();
        public static string Report { get; private set; }

        public static void Apply(GameObject root)
        {
            if (!root || EditorApplication.isPlaying) throw new InvalidOperationException("Open Garden in edit mode.");
            var loop = root.GetComponent<Loop>();
            if (!loop || !loop.overlays) throw new InvalidOperationException("Garden presentation is missing.");
            world = root.transform;
            pads = root.GetComponentsInChildren<NavPad>(true);
            Directory.CreateDirectory(Folder + "Meshes");
            Directory.CreateDirectory(Folder + "Materials");
            AssetDatabase.Refresh();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            stone = Material("Stone", new Color(.40f, .43f, .42f));
            dark = Material("Ink", new Color(.055f, .085f, .10f));
            metal = Material("Steel", new Color(.23f, .28f, .29f));
            cream = Material("Linen", new Color(.72f, .69f, .59f));
            wood = Material("Wood", new Color(.36f, .24f, .15f));
            sand = Material("Sand", new Color(.61f, .54f, .42f));
            sage = Material("Sage", new Color(.31f, .47f, .39f));
            blue = Material("Blue", new Color(.27f, .43f, .48f));
            rust = Material("Clay", new Color(.53f, .31f, .22f));
            violet = Material("Violet", new Color(.37f, .34f, .48f));
            window = Material("Glass", new Color(.12f, .22f, .25f));
            amber = Material("Amber", new Color(.85f, .63f, .28f), true);
            mint = Material("Mint", new Color(.35f, .77f, .57f), true);
            cyan = Material("Cyan", new Color(.19f, .72f, .81f), true);
            lilac = Material("Lilac", new Color(.66f, .51f, .83f), true);
            holoAmber = Hologram("Sun", new Color(.91f, .66f, .29f, .78f));
            holoMint = Hologram("Leaf", new Color(.28f, .71f, .47f, .82f));
            holoCyan = Hologram("Water", new Color(.20f, .69f, .81f, .78f));
            holoLilac = Hologram("Signal", new Color(.62f, .47f, .84f, .78f));
            plants = AssetDatabase.LoadAssetAtPath<Material>(Shared + "FacadeSurfaces.mat");
            ringMesh = RingMesh();
            leafMesh = LeafMesh();
            physical = Reset("Districts", world);
            digital = Reset("Districts", loop.overlays.transform);
            groups.Clear();
            RemoveOld(loop.overlays.transform);
            Lighting(root);
            Physics.SyncTransforms();
            Home();
            Commons();
            Metro(root.GetComponentInChildren<Train>(true));
            Factory();
            Concourse();
            Yard();
            Facades();
            var motion = digital.gameObject.AddComponent<DistrictMotion>();
            motion.groups = groups.ToArray();
            foreach (var t in digital.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 11;
            foreach (var renderer in physical.GetComponentsInChildren<Renderer>(true).Concat(digital.GetComponentsInChildren<Renderer>(true)))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            Report = physical.GetComponentsInChildren<Renderer>(true).Length + " physical renderers, "
                + digital.GetComponentsInChildren<Renderer>(true).Length + " AR renderers, "
                + groups.Count + " distance groups, " + groups.Sum(x => x.parts.Length) + " moving parts. No new lights or colliders.";
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();
        }

        static void RemoveOld(Transform presentation)
        {
            foreach (var name in new[] { "Residence", "Commons", "Assembly", "Concourse" })
            {
                var header = ObjectNames.Find(world, "Streets/" + name);
                if (header && !header.GetComponentsInChildren<Collider>(true).Any()) Object.DestroyImmediate(header.gameObject);
            }
            var art = ObjectNames.Find(presentation, "Art");
            if (!art) return;
            foreach (var t in art.Cast<Transform>().ToArray())
                if (ObjectNames.Matches(t.name, "Breakfast") || ObjectNames.Matches(t.name, "Factory") || ObjectNames.Matches(t.name, "Concourse") || ObjectNames.Matches(t.name, "Metro"))
                    Object.DestroyImmediate(t.gameObject);
        }

        static void Lighting(GameObject root)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.43f, .46f, .52f);
            RenderSettings.ambientIntensity = 1;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(.13f, .18f, .23f);
            RenderSettings.fogStartDistance = 25;
            RenderSettings.fogEndDistance = 105;
            var sun = ObjectNames.Find(root.transform, "Sun");
            if (sun && sun.TryGetComponent<Light>(out var light))
            {
                light.color = new Color(1f, .92f, .79f);
                light.intensity = 1.05f;
                EditorUtility.SetDirty(light);
            }
        }

        static Material Material(string name, Color color, bool unlit = false)
        {
            string path = Folder + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.name = name;
            material.SetColor("_BaseColor", color);
            material.enableInstancing = true;
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .26f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Material Hologram(string name, Color color)
        {
            string path = Folder + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("RealityPlayground/StoryLivingHologram"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Glow", .72f);
            material.SetFloat("_Reveal", 0);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static Transform New(string name, Transform parent, Vector3 position = default)
        {
            var value = new GameObject(ObjectNames.Short(name)).transform;
            value.SetParent(parent, false);
            value.localPosition = position;
            return value;
        }

        static Transform Reset(string name, Transform parent)
        {
            var old = ObjectNames.Find(parent, name);
            if (old) Object.DestroyImmediate(old.gameObject);
            return New(name, parent);
        }

        static Transform Site(string name, Transform parent)
        {
            var source = ObjectNames.Find(world, name);
            if (!source) return null;
            var value = New(name, parent);
            value.SetPositionAndRotation(source.position, source.rotation);
            return value;
        }

        static Transform Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 size, Material material, Vector3 angles = default)
        {
            var value = GameObject.CreatePrimitive(type);
            value.name = ObjectNames.Short(name);
            Object.DestroyImmediate(value.GetComponent<Collider>());
            value.transform.SetParent(parent, false);
            value.transform.localPosition = position;
            value.transform.localRotation = Quaternion.Euler(angles);
            value.transform.localScale = size;
            value.GetComponent<Renderer>().sharedMaterial = material;
            return value.transform;
        }

        static Transform Box(Transform parent, Vector3 position, Vector3 size, Material material, string name = "Trim", Vector3 angles = default)
            => Shape(name, PrimitiveType.Cube, parent, position, size, material, angles);

        static Transform Ball(Transform parent, Vector3 position, Vector3 size, Material material)
            => Shape("Orb", PrimitiveType.Sphere, parent, position, size, material);

        static Transform Rod(Transform parent, Vector3 from, Vector3 to, float radius, Material material)
        {
            var rod = Shape("Rail", PrimitiveType.Cylinder, parent, (from + to) * .5f, new Vector3(radius * 2, (to - from).magnitude * .5f, radius * 2), material);
            rod.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
            return rod;
        }

        static Transform Ring(Transform parent, Vector3 position, float radius, Material material, Vector3 angles = default, float thickness = 1)
        {
            var value = New("Ring", parent, position);
            value.localRotation = Quaternion.Euler(angles);
            value.localScale = Vector3.one * radius;
            value.gameObject.AddComponent<MeshFilter>().sharedMesh = ringMesh;
            value.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            return value;
        }

        static TMP_Text Label(Transform parent, string text, Vector3 position, float width, float size, Vector3 angles = default)
        {
            var label = New("Text", parent, position).gameObject.AddComponent<TextMeshPro>();
            label.transform.localRotation = Quaternion.Euler(angles);
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 3;
            label.rectTransform.sizeDelta = new Vector2(width, size * .17f);
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Truncate;
            label.color = new Color(.86f, .88f, .82f);
            return label;
        }

        static void Title(Transform parent, string title, Vector3 position, float width, Material accent)
        {
            Box(parent, position + new Vector3(0, 0, .05f), new Vector3(width, .53f, .11f), dark, "Sign");
            Box(parent, position + new Vector3(0, -.29f, .0f), new Vector3(width * .8f, .024f, .04f), accent, "Light");
            Label(parent, title, position + new Vector3(0, -.015f, -.012f), width - .20f, 2.5f);
        }

        static void Group(Transform anchor, float range, params DistrictMotion.Part[] parts)
        {
            groups.Add(new DistrictMotion.Group
            {
                anchor = anchor,
                surfaces = anchor.GetComponentsInChildren<Renderer>(true),
                parts = parts,
                range = range
            });
        }

        static DistrictMotion.Part Move(Transform target, Vector3 spin, float lift = 0, float rate = 1, float pulse = 0, float phase = 0)
            => new DistrictMotion.Part { target = target, spin = spin, lift = lift, rate = rate, pulse = pulse, phase = phase };

        static void Home()
        {
            var r = Site("Home", physical);
            var p = Site("Home", digital);
            if (!r || !p) return;
            var frame = New("Entry", r);
            foreach (float x in new[] { -1.08f, 1.08f })
            {
                Box(frame, new Vector3(x, 1.29f, -2.63f), new Vector3(.085f, 2.58f, .20f), metal);
                Box(frame, new Vector3(x, 2.57f, -2.91f), new Vector3(.085f, .11f, .74f), metal);
            }
            Box(frame, new Vector3(0, 2.65f, -2.96f), new Vector3(2.35f, .11f, .92f), stone, "Canopy");
            Batch(frame, "HomeEntry");
            var finish = New("Entry", p);
            Box(finish, new Vector3(0, 2.716f, -2.96f), new Vector3(2.38f, .018f, .96f), sand);
            Box(finish, new Vector3(0, 2.578f, -3.37f), new Vector3(2.05f, .021f, .025f), amber, "Light");
            for (int i = 0; i < 13; i++) Box(finish, new Vector3(-1.08f + i * .18f, 2.584f, -2.93f), new Vector3(.035f, .045f, .71f), wood, "Slat");
            Title(finish, "RESIDENCE", new Vector3(0, 2.97f, -3.44f), 2.48f, amber);
            Batch(finish, "HomeFinish");
            var projector = New("Sun", p, new Vector3(-1.95f, 2.43f, 2.10f));
            Shape("Lens", PrimitiveType.Cylinder, r, new Vector3(-1.95f, 2.43f, 2.37f), new Vector3(.20f, .03f, .20f), metal, new Vector3(90, 0, 0));
            var sun = New("Sun", projector);
            Ball(sun, Vector3.zero, Vector3.one * .31f, holoAmber);
            Ring(sun, Vector3.zero, .27f, amber, new Vector3(12, 0, 8));
            for (int i = 0; i < 10; i++)
            {
                float angle = i * Mathf.PI * .2f;
                var at = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * .40f;
                Box(sun, at, new Vector3(.09f, .025f, .025f), amber, "Ray", new Vector3(0, 0, angle * Mathf.Rad2Deg));
            }
            Batch(sun, "HomeSun");
            var orbit = New("Orbit", projector);
            Ring(orbit, Vector3.zero, .52f, holoAmber, new Vector3(28, 0, 0));
            Ball(orbit, new Vector3(.50f, 0, 0), Vector3.one * .06f, cream);
            Batch(orbit, "HomeOrbit");
            Group(p, 24, Move(sun, new Vector3(0, 0, 5), .015f, .7f), Move(orbit, new Vector3(0, 0, -11)));
        }

        static void Commons()
        {
            var r = Site("Breakfast", physical);
            var p = Site("Breakfast", digital);
            if (!r || !p) return;
            var frame = New("Market", r, new Vector3(0, 0, .20f));
            foreach (float x in new[] { -1.38f, 1.38f })
            {
                Box(frame, new Vector3(x, .05f, 1.66f), new Vector3(.28f, .10f, .28f), metal, "Foot");
                Rod(frame, new Vector3(x, .10f, 1.66f), new Vector3(x, 3.2f, 1.66f), .045f, metal);
                Rod(frame, new Vector3(x, 3.17f, 1.66f), new Vector3(x, 3.17f, .15f), .045f, metal);
                Rod(frame, new Vector3(x, 2.45f, 1.66f), new Vector3(x, 3.17f, 1.02f), .024f, metal);
            }
            Rod(frame, new Vector3(-1.50f, 3.18f, .16f), new Vector3(1.50f, 3.18f, .16f), .055f, metal);
            Batch(frame, "MarketFrame");
            var market = New("Canopy", p);
            for (int i = 0; i < 18; i++)
            {
                float x = -1.58f + i * .186f;
                float y = 3.23f + .28f * (1 - x * x / 2.8f);
                Box(market, new Vector3(x, y, .96f), new Vector3(.184f, .055f, 1.95f), i % 3 == 0 ? cream : sage, "Sail", new Vector3(0, 0, -x * 11));
            }
            var title = New("Sign", market, new Vector3(-1.72f, 2.91f, .96f));
            title.localRotation = Quaternion.Euler(0, 90, 0);
            Title(title, "COMMONS", Vector3.zero, 1.82f, amber);
            Box(market, new Vector3(0, .62f, .837f), new Vector3(2.27f, 1.08f, .025f), sage);
            for (int i = 0; i < 16; i++) Box(market, new Vector3(-1.06f + i * .141f, .63f, .813f), new Vector3(.014f, .93f, .012f), cream, "Flute");
            for (int i = 0; i < 3; i++) HangingPlant(market, new Vector3(-1.02f + i * 1.02f, 2.67f, 1.57f), .38f);
            Batch(market, "MarketCanopy");
            MarketSign(r);
            var orbit = New("Breakfast", p, new Vector3(-1.73f, 2.30f, .65f));
            Projector(r, new Vector3(-1.73f, 0, .65f), 1.36f, amber);
            var food = New("Meal", orbit);
            Shape("Plate", PrimitiveType.Cylinder, food, new Vector3(0, -.18f, 0), new Vector3(.66f, .018f, .66f), holoAmber);
            Shape("Bread", PrimitiveType.Capsule, food, new Vector3(0, -.035f, 0), new Vector3(.37f, .09f, .24f), holoAmber, new Vector3(0, 0, 90));
            Ball(food, new Vector3(.23f, -.035f, -.015f), Vector3.one * .14f, holoMint);
            Rod(food, new Vector3(.23f, .02f, -.015f), new Vector3(.26f, .105f, -.015f), .012f, mint);
            Batch(food, "Meal");
            var turn = New("Orbit", orbit);
            Ring(turn, Vector3.zero, .52f, amber, new Vector3(78, 0, 0));
            Ball(turn, new Vector3(.48f, 0, .17f), Vector3.one * .08f, holoMint);
            Batch(turn, "MealOrbit");
            var coffee = New("Cup", orbit, new Vector3(.58f, .12f, 0));
            Shape("Cup", PrimitiveType.Cylinder, coffee, Vector3.zero, new Vector3(.20f, .12f, .20f), holoMint);
            Ring(coffee, new Vector3(.145f, 0, 0), .085f, mint);
            Shape("Lid", PrimitiveType.Cylinder, coffee, new Vector3(0, .127f, 0), new Vector3(.19f, .01f, .19f), cream);
            Batch(coffee, "MarketCup");
            Plant(r, p, new Vector3(3.22f, 0, 1.67f), 1.75f, 13, "CommonsTree");
            Group(p, 32, Move(food, new Vector3(0, 13, 0), .045f, .9f), Move(turn, new Vector3(0, -17, 0)), Move(coffee, new Vector3(0, -11, 0), .10f, .85f, .025f));
        }

        static void Metro(Train train)
        {
            if (!train) return;
            int index = 0;
            foreach (var source in new[] { train.south, train.work })
            {
                if (!source) continue;
                string key = "Metro" + (++index);
                var r = New(key, physical);
                var p = New(key, digital);
                var position = source.position + (index == 1 ? source.forward * .22f : Vector3.zero);
                var rotation = source.rotation * (index == 1 ? Quaternion.Euler(0, 180, 0) : Quaternion.identity);
                r.SetPositionAndRotation(position, rotation);
                p.SetPositionAndRotation(position, rotation);
                var frame = New("Frame", r);
                foreach (float x in new[] { -2.4f, 2.4f })
                {
                    Box(frame, new Vector3(x, .055f, 1.62f), new Vector3(.32f, .11f, .34f), metal, "Foot");
                    Box(frame, new Vector3(x, 1.59f, 1.62f), new Vector3(.13f, 3.18f, .18f), metal, "Post");
                }
                Box(frame, new Vector3(0, 3.25f, 1.62f), new Vector3(4.93f, .22f, 1.15f), stone, "Roof");
                Batch(frame, key + "Frame");
                var portal = New("Portal", p);
                foreach (float x in new[] { -2.32f, 2.32f }) Box(portal, new Vector3(x, 1.65f, 1.514f), new Vector3(.028f, 3.0f, .016f), cyan, "Light");
                Box(portal, new Vector3(0, 3.13f, 1.51f), new Vector3(4.65f, .026f, .016f), cyan, "Light");
                Title(portal, "METRO", new Vector3(0, 2.82f, 1.53f), 3.13f, cyan);
                for (int i = 0; i < 13; i++) Box(portal, new Vector3(-2.1f + i * .35f, 3.38f, 1.63f), new Vector3(.16f, .035f, .86f), blue, "Fin");
                Batch(portal, key + "Portal");
                var ad = New("Route", p, new Vector3(0, 4.13f, 1.60f));
                Shape("Lens", PrimitiveType.Cylinder, r, new Vector3(0, 3.395f, 1.60f), new Vector3(.38f, .035f, .38f), metal);
                var track = New("Loop", ad);
                float routeRadius = index == 1 ? .75f : 1.15f;
                Ring(track, Vector3.zero, routeRadius, holoCyan, new Vector3(90, 0, 0));
                for (int i = 0; i < 3; i++)
                {
                    float angle = i * Mathf.PI * 2 / 3;
                    Ball(track, new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * routeRadius, Vector3.one * .11f, cyan);
                }
                Batch(track, key + "Route");
                var turn = New("Orbit", ad);
                var car = New("Train", turn, new Vector3(routeRadius, .03f, 0));
                car.localRotation = Quaternion.Euler(0, 180, 0);
                Shape("Body", PrimitiveType.Capsule, car, Vector3.zero, new Vector3(.21f, .42f, .22f), holoCyan, new Vector3(90, 0, 0));
                Box(car, new Vector3(0, -.09f, 0), new Vector3(.24f, .035f, .65f), blue);
                foreach (float side in new[] { -.108f, .108f })
                    for (int i = 0; i < 4; i++) Box(car, new Vector3(side, .035f, -.24f + i * .16f), new Vector3(.012f, .10f, .09f), cream, "Window");
                Ball(car, new Vector3(0, .02f, .40f), new Vector3(.12f, .10f, .028f), cream);
                Batch(car, key + "Train");
                Group(p, 36, Move(turn, new Vector3(0, 23, 0)), Move(ad, Vector3.zero, .025f, .65f));
            }
        }

        static void Factory()
        {
            var r = Site("Factory", physical);
            var p = Site("Factory", digital);
            if (!r || !p) return;
            var frame = New("Gantry", r);
            foreach (float x in new[] { -2.76f, 2.76f })
            {
                Box(frame, new Vector3(x, .04f, 2.85f), new Vector3(.44f, .08f, .48f), metal, "Foot");
                Box(frame, new Vector3(x, 1.69f, 2.85f), new Vector3(.20f, 3.38f, .26f), metal, "Post");
                Box(frame, new Vector3(x, 1.01f, 2.684f), new Vector3(.21f, .72f, .055f), rust);
                for (int i = 0; i < 4; i++) Box(frame, new Vector3(x, .78f + .16f * i, 2.65f), new Vector3(.22f, .055f, .03f), cream, "Band", new Vector3(0, 0, -25));
            }
            Box(frame, new Vector3(0, 3.38f, 2.85f), new Vector3(5.82f, .26f, .36f), metal, "Beam");
            for (int i = 0; i < 8; i++) Box(frame, new Vector3(-2.47f + i * .70f, 3.39f, 2.635f), new Vector3(.31f, .13f, .035f), rust, "Mark");
            Batch(frame, "AssemblyFrame");
            var finish = New("Assembly", p);
            Title(finish, "ASSEMBLY", new Vector3(0, 3.82f, 2.62f), 3.7f, amber);
            Box(finish, new Vector3(0, 3.205f, 2.64f), new Vector3(5.25f, .023f, .032f), amber, "Light");
            Batch(finish, "AssemblyFinish");
            var ad = New("Product", p, new Vector3(2.76f, 2.27f, 2.50f));
            Shape("Lens", PrimitiveType.Cylinder, r, new Vector3(2.76f, 1.69f, 2.5f), new Vector3(.36f, .05f, .36f), metal);
            Rod(r, new Vector3(2.76f, 1.64f, 2.83f), new Vector3(2.76f, 1.64f, 2.5f), .035f, metal);
            var product = New("Module", ad);
            Model("Assets/Garden/Art/Models/Meshes/Camera.asset", product, Vector3.zero, new Vector3(.35f, .48f, .70f), holoAmber, true);
            var front = New("Optic", product, new Vector3(0, -.02f, .43f));
            Ring(front, Vector3.zero, .15f, cyan);
            Ball(front, Vector3.zero, new Vector3(.20f, .20f, .065f), holoCyan);
            Batch(front, "AssemblyOptic");
            var scan = New("Scan", ad);
            Ring(scan, Vector3.zero, .49f, amber, new Vector3(90, 0, 0));
            Ring(scan, new Vector3(0, .28f, 0), .40f, holoAmber, new Vector3(90, 0, 0));
            Batch(scan, "AssemblyScan");
            Group(p, 32, Move(product, new Vector3(0, 19, 0), .025f, .8f), Move(scan, new Vector3(0, -15, 0), .21f, 1.0f));
        }

        static void Concourse()
        {
            var r = Site("Concourse", physical);
            var p = Site("Concourse", digital);
            if (!r || !p) return;
            var frame = New("Portal", r, new Vector3(.315f, 0, 0));
            frame.localRotation = Quaternion.Euler(0, 90, 0);
            foreach (float x in new[] { -1.79f, 1.79f })
            {
                Box(frame, new Vector3(x, 1.43f, 0), new Vector3(.14f, 2.86f, .16f), metal, "Post");
                Box(frame, new Vector3(x, .055f, -.015f), new Vector3(.25f, .11f, .25f), stone, "Foot");
            }
            Box(frame, new Vector3(0, 2.89f, 0), new Vector3(3.70f, .18f, .24f), metal, "Beam");
            foreach (float x in new[] { -1.35f, 1.35f }) Rod(frame, new Vector3(x, 2.97f, 0), new Vector3(x, 3.72f, -.55f), .027f, metal);
            Batch(frame, "ConcourseFrame");
            var portal = New("Portal", p, new Vector3(.26f, 0, 0));
            portal.localRotation = Quaternion.Euler(0, 90, 0);
            foreach (float x in new[] { -1.78f, 1.78f }) Box(portal, new Vector3(x, 1.53f, -.095f), new Vector3(.025f, 2.62f, .014f), lilac, "Light");
            Box(portal, new Vector3(0, 2.782f, -.09f), new Vector3(3.46f, .025f, .035f), lilac, "Light");
            Title(portal, "CONCOURSE", new Vector3(0, 3.72f, -.60f), 3.42f, lilac);
            Batch(portal, "ConcourseFinish");
            var flower = New("Bloom", p, new Vector3(.17f, 4.85f, 0));
            flower.localRotation = Quaternion.Euler(0, 90, 0);
            var petals = New("Petals", flower);
            var petalMesh = PetalMesh();
            for (int i = 0; i < 7; i++)
            {
                var petal = New("Petal", petals);
                petal.localRotation = Quaternion.Euler(0, 0, i * 360f / 7);
                petal.gameObject.AddComponent<MeshFilter>().sharedMesh = petalMesh;
                petal.gameObject.AddComponent<MeshRenderer>().sharedMaterial = holoLilac;
            }
            Batch(petals, "BloomPetals");
            var inner = New("Heart", flower);
            Ball(inner, Vector3.zero, Vector3.one * .28f, holoAmber);
            Ring(inner, Vector3.zero, .33f, lilac, new Vector3(20, 10, 0));
            Batch(inner, "BloomHeart");
            Ring(flower, Vector3.zero, .79f, holoLilac, new Vector3(15, 0, 0));
            Shape("Lens", PrimitiveType.Cylinder, r, new Vector3(.255f, 3.82f, 0), new Vector3(.26f, .035f, .26f), metal, new Vector3(0, 0, 90));
            var product = New("Style", p, new Vector3(-1.32f, 1.94f, 2.80f));
            if (Clear(p.TransformPoint(new Vector3(-1.32f, 0, 2.80f)), .62f, 1.70f))
            {
                Projector(r, new Vector3(-1.32f, 0, 2.80f), 1.55f, lilac);
                Model(Shared + "SpatialProductShoe.asset", product, Vector3.zero, new Vector3(.57f, .32f, .32f), holoLilac, true);
                Ring(product, new Vector3(0, -.23f, 0), .38f, lilac, new Vector3(90, 0, 0));
                Batch(product, "ConcourseShoe");
            }
            else Object.DestroyImmediate(product.gameObject);
            var parts = new List<DistrictMotion.Part> { Move(petals, new Vector3(0, 0, 10), 0, .6f, .035f), Move(inner, new Vector3(8, 14, -8), 0, .85f, .055f) };
            if (product) parts.Add(Move(product, new Vector3(0, 16, 0), .075f, 1.0f));
            Group(p, 32, parts.ToArray());
        }

        static void Yard()
        {
            var r = Site("Yard", physical);
            if (!r) return;
            var wall = ObjectNames.Find(world, "Yard").GetComponentsInChildren<BoxCollider>(true)
                .Where(x => ObjectNames.Matches(x.name, "Wall") && x.bounds.size.x > 8 && x.bounds.size.x < 20)
                .OrderByDescending(x => x.bounds.center.z).FirstOrDefault();
            if (!wall) return;
            var back = r.InverseTransformPoint(wall.bounds.center);
            back.z = r.InverseTransformPoint(new Vector3(wall.bounds.center.x, 0, wall.bounds.min.z)).z - .018f;
            back.y = 3.15f;
            Title(r, "SERVICE", back, 2.88f, sand);
            var fittings = New("Wires", r);
            for (int line = 0; line < 3; line++)
            {
                Vector3 last = new Vector3(-7.2f, 3.77f + line * .10f, back.z - .08f);
                for (int i = 1; i <= 20; i++)
                {
                    float t = i / 20f;
                    var next = new Vector3(-7.2f + t * 13.6f, 3.77f + line * .10f - Mathf.Sin(t * Mathf.PI) * .23f, back.z - .08f);
                    Rod(fittings, last, next, .011f, dark);
                    last = next;
                }
            }
            foreach (float x in new[] { -7.2f, 6.4f }) Box(fittings, new Vector3(x, 3.90f, back.z + .005f), new Vector3(.18f, .44f, .12f), metal, "Mount");
            Batch(fittings, "YardWires");
            for (int i = 0; i < 11; i++)
            {
                float x = -4.20f + i * .55f;
                Box(r, new Vector3(x, .047f, -2.46f), new Vector3(.32f, .012f, .16f), i % 2 == 0 ? sand : dark, "Mark", new Vector3(0, 20, 0));
            }
            Batch(r, "YardFinish");
        }

        static void Facades()
        {
            var source = ObjectNames.Find(world, "Models/Buildings");
            if (!source) return;
            var sites = new[] { ("Home", sand), ("Breakfast", sage), ("Factory", rust), ("Concourse", violet) };
            var assigned = new HashSet<MeshFilter>();
            foreach (var site in sites)
            {
                var center = ObjectNames.Find(world, site.Item1);
                if (!center) continue;
                var group = New(site.Item1 + "Front", digital);
                group.position = center.position;
                int count = 0;
                foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true).OrderBy(f => Vector3.ProjectOnPlane(f.transform.position - center.position, Vector3.up).sqrMagnitude))
                {
                    if (assigned.Contains(filter) || !filter.sharedMesh || !(ObjectNames.Matches(filter.name, "Wall2") || ObjectNames.Matches(filter.name, "Wall4") || ObjectNames.Matches(filter.name, "Entry") || ObjectNames.Matches(filter.name, "DoorWall"))) continue;
                    var delta = filter.transform.position - center.position;
                    if (Vector3.ProjectOnPlane(delta, Vector3.up).sqrMagnitude > 155) continue;
                    if (!pads.Any(p => Vector3.Dot(p.transform.position - filter.transform.position, filter.transform.forward) > .4f && Vector3.ProjectOnPlane(p.transform.position - filter.transform.position, Vector3.up).sqrMagnitude < 75)) continue;
                    if (count++ >= 16) break;
                    var panel = New("Skin", group);
                    panel.SetPositionAndRotation(filter.transform.position + filter.transform.forward * .013f, filter.transform.rotation);
                    panel.localScale = filter.transform.lossyScale;
                    panel.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                    panel.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[] { site.Item2, cream, window };
                    assigned.Add(filter);
                }
                Batch(group, site.Item1 + "Front");
                Group(group, 39);
            }
        }

        static void HangingPlant(Transform parent, Vector3 point, float size)
        {
            Rod(parent, point + Vector3.up * .05f, point + Vector3.up * .54f, .012f, metal);
            Shape("Pot", PrimitiveType.Cylinder, parent, point, new Vector3(size, .09f, size), cream);
            for (int i = 0; i < 7; i++)
            {
                float angle = i * Mathf.PI * 2 / 7;
                var direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
                var stem = point + direction * size * .43f + Vector3.down * (.04f + i % 3 * .045f);
                Rod(parent, point + Vector3.up * .08f, stem, .007f, sage);
                var leaf = New("Leaf", parent, stem);
                leaf.gameObject.AddComponent<MeshFilter>().sharedMesh = leafMesh;
                leaf.gameObject.AddComponent<MeshRenderer>().sharedMaterial = holoMint;
                leaf.localScale = new Vector3(.21f, .30f + i % 2 * .08f, .21f);
                leaf.localRotation = Quaternion.FromToRotation(Vector3.up, direction * .60f + Vector3.down);
            }
        }

        static void MarketSign(Transform site)
        {
            var props = ObjectNames.Find(world, "Models/Props");
            var sign = props ? ObjectNames.Find(props, "Sign") : null;
            if (!sign) return;
            var counter = ObjectNames.Find(world, "Breakfast/Counter");
            foreach (Transform child in props.Cast<Transform>().ToArray())
            {
                if (!(ObjectNames.Matches(child.name, "Post") || ObjectNames.Matches(child.name, "Beam") || ObjectNames.Matches(child.name, "Hook") || ObjectNames.Matches(child.name, "Text"))) continue;
                var delta = child.position - counter.position;
                if (Mathf.Abs(delta.x) > 1.2f || Mathf.Abs(delta.z) > .20f || delta.y < .7f || delta.y > 2.2f) continue;
                Object.DestroyImmediate(child.gameObject);
            }
            var point = site.TransformPoint(new Vector3(1.79f, 2.32f, 1.12f));
            var rotation = site.rotation * Quaternion.Euler(0, 90, 0);
            sign.SetPositionAndRotation(point, rotation);
            Rod(site, new Vector3(1.38f, 3.17f, 1.12f), new Vector3(1.82f, 3.17f, 1.12f), .028f, metal);
            foreach (float side in new[] { -1f, 1f })
            {
                var label = Label(site, "FRESH", site.InverseTransformPoint(point + rotation * new Vector3(0, .28f, .077f * side)), 1.04f, 1.15f);
                label.transform.rotation = rotation * Quaternion.Euler(0, side > 0 ? 180 : 0, 0);
            }
        }

        static bool Projector(Transform parent, Vector3 point, float height, Material accent)
        {
            if (!Clear(parent.TransformPoint(point), .45f, height)) return false;
            var pod = New("Projector", parent, point);
            Shape("Foot", PrimitiveType.Cylinder, pod, new Vector3(0, .055f, 0), new Vector3(.42f, .055f, .42f), metal);
            Shape("Post", PrimitiveType.Cylinder, pod, new Vector3(0, height * .47f, 0), new Vector3(.13f, height * .44f, .13f), metal);
            Shape("Lens", PrimitiveType.Cylinder, pod, new Vector3(0, height, 0), new Vector3(.33f, .045f, .33f), accent);
            Batch(pod, "Projector" + Mathf.Abs(parent.position.x * 37 + point.x * 13 + point.z).ToString("F0"));
            return true;
        }

        static void Plant(Transform r, Transform p, Vector3 point, float height, int variant, string key)
        {
            if (!Clear(r.TransformPoint(point), 1.2f, height + .4f)) return;
            var pot = New("Planter", r, point);
            Shape("Pot", PrimitiveType.Cylinder, pot, new Vector3(0, .22f, 0), new Vector3(.70f, .22f, .70f), metal);
            Ring(pot, new Vector3(0, .444f, 0), .36f, sage, new Vector3(90, 0, 0));
            Batch(pot, key + "Pot");
            Model(Shared + "TreeCanopy" + variant + ".asset", p, point + Vector3.up * .40f, new Vector3(1.1f, height, 1.1f), plants, false);
        }

        static bool Clear(Vector3 point, float width, float height)
        {
            foreach (var pad in pads)
            {
                var delta = pad.transform.position - point;
                if (delta.y > height + .1f || delta.y < -2.05f) continue;
                if (new Vector2(delta.x, delta.z).magnitude < width * .5f + .65f) return false;
            }
            float radius = Mathf.Min(.30f, width * .48f);
            return !Physics.CheckCapsule(point + Vector3.up * (radius + .05f), point + Vector3.up * Mathf.Max(radius + .05f, height - radius), radius, 1, QueryTriggerInteraction.Ignore);
        }

        static Transform Model(string path, Transform parent, Vector3 point, Vector3 size, Material material, bool centered)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh || !material) return null;
            var model = New("Model", parent, point);
            model.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = model.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = Enumerable.Repeat(material, mesh.subMeshCount).ToArray();
            var bounds = mesh.bounds;
            model.localScale = new Vector3(size.x / Mathf.Max(.001f, bounds.size.x), size.y / Mathf.Max(.001f, bounds.size.y), size.z / Mathf.Max(.001f, bounds.size.z));
            var pivot = centered ? bounds.center : new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            model.localPosition = point - Vector3.Scale(pivot, model.localScale);
            return model;
        }

        static Mesh RingMesh()
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            const int segments = 48;
            const int sides = 6;
            for (int i = 0; i <= segments; i++)
                for (int j = 0; j <= sides; j++)
                {
                    float a = i * Mathf.PI * 2 / segments;
                    float b = j * Mathf.PI * 2 / sides;
                    float radius = 1 + Mathf.Cos(b) * .015f;
                    vertices.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, Mathf.Sin(b) * .015f));
                    if (i == segments || j == sides) continue;
                    int n = i * (sides + 1) + j;
                    indices.AddRange(new[] { n, n + sides + 1, n + 1, n + 1, n + sides + 1, n + sides + 2 });
                }
            var mesh = new Mesh { name = "Ring" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return Save(mesh, "Ring");
        }

        static Mesh LeafMesh()
        {
            var mesh = new Mesh { name = "Leaf" };
            mesh.vertices = new[] { Vector3.zero, new Vector3(-.43f, .42f, .025f), new Vector3(0, .40f, .16f), new Vector3(.43f, .42f, .025f), new Vector3(0, 1, -.06f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 1, 2, 4, 2, 3, 4 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return Save(mesh, "Leaf");
        }

        static Mesh PetalMesh()
        {
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            for (int row = 0; row <= 14; row++)
            {
                float t = row / 14f;
                float width = .018f + Mathf.Sin(t * Mathf.PI) * .15f;
                for (int col = 0; col <= 4; col++)
                {
                    float side = col * .5f - 1;
                    vertices.Add(new Vector3(side * width, .14f + .58f * t, Mathf.Sin(t * Mathf.PI) * (.14f - side * side * .09f) - .05f * t));
                    if (row == 14 || col == 4) continue;
                    int n = row * 5 + col;
                    indices.AddRange(new[] { n, n + 5, n + 1, n + 1, n + 5, n + 6 });
                }
            }
            var mesh = new Mesh { name = "Petal" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return Save(mesh, "Petal");
        }

        static void Batch(Transform root, string name)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(true).Where(x => x.sharedMesh && !x.GetComponent<TMP_Text>()).ToArray();
            var materials = filters.SelectMany(x => x.GetComponent<MeshRenderer>().sharedMaterials).Where(x => x).Distinct().ToArray();
            foreach (var material in materials)
            {
                var sources = new List<CombineInstance>();
                foreach (var filter in filters)
                {
                    var selected = filter.GetComponent<MeshRenderer>().sharedMaterials;
                    for (int sub = 0; sub < selected.Length && sub < filter.sharedMesh.subMeshCount; sub++)
                        if (selected[sub] == material)
                            sources.Add(new CombineInstance { mesh = filter.sharedMesh, subMeshIndex = sub, transform = root.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                }
                if (sources.Count == 0) continue;
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(sources.ToArray(), true, true);
                mesh = Save(mesh, name + material.name);
                var value = New(material.name, root);
                value.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                value.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
            foreach (var filter in filters)
            {
                Object.DestroyImmediate(filter.GetComponent<MeshRenderer>());
                Object.DestroyImmediate(filter);
            }
            foreach (var child in root.GetComponentsInChildren<Transform>(true).Reverse())
                if (child != root && child.childCount == 0 && child.GetComponents<Component>().Length == 1) Object.DestroyImmediate(child.gameObject);
        }

        static Mesh Save(Mesh mesh, string name)
        {
            string path = Folder + "Meshes/" + name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved)
            {
                saved.Clear();
                saved.indexFormat = mesh.indexFormat;
                saved.vertices = mesh.vertices;
                saved.normals = mesh.normals;
                saved.tangents = mesh.tangents;
                saved.colors = mesh.colors;
                saved.uv = mesh.uv;
                saved.uv2 = mesh.uv2;
                saved.subMeshCount = mesh.subMeshCount;
                for (int i = 0; i < mesh.subMeshCount; i++) saved.SetIndices(mesh.GetIndices(i), mesh.GetTopology(i), i);
                saved.bounds = mesh.bounds;
                saved.UploadMeshData(false);
                saved.name = name;
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(saved);
                return saved;
            }
            mesh.name = name;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
