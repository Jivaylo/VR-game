using System.IO;
using System.Linq;
using Garden;
using RealityPlayground;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Object = UnityEngine.Object;

namespace Garden.Editor
{
    public static class TransitStyle
    {
        const string Folder = "Assets/Garden/Art/Transit/";
        static Material concrete, dark, steel, green, grass, sky, cream, ink;
        static TMP_FontAsset font;

        public static void Apply(GameObject world)
        {
            if (!world || EditorApplication.isPlaying) return;
            var loop = world.GetComponent<Loop>();
            if (!loop) return;
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            concrete = Mat("Concrete", new Color(.28f, .31f, .32f));
            dark = Mat("Dark", new Color(.035f, .05f, .054f));
            steel = Mat("Steel", new Color(.43f, .47f, .48f));
            green = Mat("Leaf", new Color(.19f, .38f, .14f));
            grass = Mat("Grass", new Color(.28f, .47f, .29f));
            sky = Mat("Sky", new Color(.47f, .70f, .72f));
            cream = Mat("Paper", new Color(.86f, .84f, .69f));
            ink = Mat("Ink", new Color(.12f, .23f, .21f));
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            Cabin(loop);
            Return(loop);
            AssetDatabase.SaveAssets();
        }

        static void Cabin(Loop loop)
        {
            var train = loop.GetComponentInChildren<Train>(true);
            if (!train || !train.cabin) return;
            var car = train.cabin.parent;
            var root = Reset(car, "Transit");
            var view = New(root, "Scenic");
            var raw = New(root, "Tunnel");
            foreach (float side in new[] { -1f, 1f })
            {
                var window = New(view, "View");
                window.localPosition = new Vector3(side * 2.34f, 1.52f, 0f);
                window.localRotation = Quaternion.Euler(0f, side * 90f, 0f);
                Box(window, "Sky", new Vector3(0f, .06f, .025f), new Vector3(4.5f, 1.8f, .06f), sky);
                Box(window, "Grass", new Vector3(0f, -.57f, -.012f), new Vector3(4.5f, .4f, .028f), grass);
                for (int i = 0; i < 5; i++)
                {
                    float x = -1.72f + i * .83f;
                    float height = .54f + i % 2 * .17f;
                    Box(window, "Trunk", new Vector3(x, -.32f, -.055f), new Vector3(.045f, .5f, .035f), cream);
                    var crown = Box(window, "Crown", new Vector3(x, -.09f + height * .28f, -.065f), new Vector3(.33f, height, .06f), grass);
                    crown.localRotation = Quaternion.Euler(0f, 0f, 34f);
                }
                var tunnel = New(raw, "Wall");
                tunnel.localPosition = window.localPosition;
                tunnel.localRotation = window.localRotation;
                Box(tunnel, "Concrete", Vector3.zero, new Vector3(4.5f, 1.8f, .08f), concrete);
                Box(tunnel, "Vent", new Vector3(.65f, .06f, -.055f), new Vector3(1.02f, .64f, .05f), dark);
                for (int i = 0; i < 7; i++) Box(tunnel, "Bar", new Vector3(.22f + i * .145f, .06f, -.088f), new Vector3(.025f, .60f, .035f), steel);
                for (int i = 0; i < 5; i++)
                {
                    var strand = Box(tunnel, "Strand", new Vector3(.57f + Mathf.Sin(i * .7f) * .11f, -.25f + i * .104f, -.118f), new Vector3(.018f, .12f, .012f), green);
                    strand.localRotation = Quaternion.Euler(0f, 0f, 14f + i * 4f);
                    if (i % 2 == 0) Box(tunnel, "Leaf", strand.localPosition + new Vector3(.052f, .02f, -.006f), new Vector3(.095f, .032f, .012f), green).localRotation = Quaternion.Euler(0f, 0f, 24f);
                }
                Box(tunnel, "Pipe", new Vector3(-1.23f, 0f, -.12f), new Vector3(.105f, 1.8f, .08f), steel);
                Text(tunnel, "Date", new Vector3(-.98f, -.54f, -.051f), new Vector2(.66f, .16f), .13f, "SERVICE 07\nEXTERIOR INTAKE", cream);
            }
            train.scenicView = view.gameObject;
            train.rawView = raw.gameObject;
            view.gameObject.SetActive(false);
            raw.gameObject.SetActive(false);
            var trace = Text(root, "Trace", new Vector3(.047f, 1.82f, 2.37f), new Vector2(1.22f, .14f), .16f, "The interruption was the view.", cream);
            train.interruption = trace;
            trace.gameObject.SetActive(false);
            ServicePanel(loop, train, root);
            foreach (var text in car.GetComponentsInChildren<TMP_Text>(true))
                if (text.text.StartsWith("SERVICE RECORD\nX > CHECK > X")) Object.DestroyImmediate(text.gameObject);
            AddPad(loop, root, "Window", car.TransformPoint(new Vector3(-1.02f, 0f, .78f)));
            EditorUtility.SetDirty(train);
        }

        static void ServicePanel(Loop loop, Train train, Transform root)
        {
            var panel = New(root, "Service");
            panel.localPosition = new Vector3(-1.935f, 1.37f, .76f);
            panel.localRotation = Quaternion.Euler(0f, -90f, 0f);
            Box(panel, "Case", Vector3.zero, new Vector3(.80f, .61f, .04f), steel);
            Box(panel, "Plate", new Vector3(0f, 0f, -.025f), new Vector3(.72f, .54f, .012f), cream);
            Text(panel, "Pattern", new Vector3(0f, .085f, -.036f), new Vector2(.68f, .14f), .13f, "X    CHECK    X", ink);
            Text(panel, "Trace", new Vector3(0f, -.18f, -.036f), new Vector2(.68f, .1f), .08f, "WATCH THE ARMS", ink);
            for (int i = 0; i < 3; i++)
            {
                var arm = New(panel, "Arm");
                arm.localPosition = new Vector3(-.22f + i * .22f, -.04f, -.037f);
                Box(arm, "Base", Vector3.zero, new Vector3(.023f, .065f, .006f), ink);
                Box(arm, "Link", new Vector3(.027f, .032f, 0f), new Vector3(.08f, .016f, .006f), ink).localRotation = Quaternion.Euler(0f, 0f, i == 1 ? -25f : 30f);
                Box(arm, "Grip", new Vector3(i == 1 ? -.021f : .052f, .07f, 0f), new Vector3(.03f, .036f, .006f), ink);
            }
            var sheet = New(panel, "Ad");
            sheet.localPosition = new Vector3(0f, 0f, -.057f);
            Box(sheet, "Front", Vector3.zero, new Vector3(.75f, .56f, .016f), sky);
            Text(sheet, "Copy", new Vector3(0f, .02f, -.012f), new Vector2(.69f, .34f), .15f, "YOUR SPACE\nYOUR PACE", ink);
            var tab = Box(sheet, "Corner", new Vector3(.30f, -.24f, -.012f), new Vector3(.10f, .07f, .012f), cream);
            tab.localRotation = Quaternion.Euler(-12f, 0f, -8f);
            foreach (var child in sheet.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 11;
            var peel = panel.gameObject.AddComponent<Peel>();
            peel.loop = loop;
            peel.kind = Peel.Kind.Ad;
            peel.sheet = sheet;
            peel.pull = new Vector3(0f, 1f, -.5f);
            peel.distance = .35f;
            var collider = sheet.gameObject.AddComponent<BoxCollider>();
            sheet.gameObject.layer = 8;
            collider.size = new Vector3(.75f, .56f, .02f);
            collider.isTrigger = true;
            Wire(panel.gameObject, collider);
            var mirror = New(root, "Reflection");
            mirror.localPosition = new Vector3(1.973f, 1.45f, .76f);
            mirror.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Box(mirror, "Frame", new Vector3(0f, 0f, .023f), new Vector3(1.02f, .73f, .04f), steel);
            var glass = GameObject.CreatePrimitive(PrimitiveType.Quad);
            glass.name = "Glass";
            glass.transform.SetParent(mirror, false);
            glass.transform.localScale = new Vector3(.97f, .68f, 1f);
            Object.DestroyImmediate(glass.GetComponent<Collider>());
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "Glass.mat");
            if (!material)
            {
                material = new Material(Shader.Find("RealityPlayground/BleakPlanarMirror")) { name = "Glass" };
                AssetDatabase.CreateAsset(material, Folder + "Glass.mat");
            }
            var renderer = glass.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var reflection = mirror.gameObject.AddComponent<BleakMirror>();
            var serialized = new SerializedObject(reflection);
            serialized.FindProperty("mirrorSurface").objectReferenceValue = renderer;
            serialized.FindProperty("reflectionResolution").intValue = 256;
            serialized.FindProperty("desaturation").floatValue = .1f;
            serialized.FindProperty("exposure").floatValue = .9f;
            serialized.FindProperty("hiddenLayers").intValue = 1 << 11;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            train.reflection = mirror.gameObject;
            mirror.gameObject.SetActive(false);
        }

        static void Return(Loop loop)
        {
            var gate = loop.GetComponentInChildren<Gate>(true);
            if (!gate) return;
            var root = Reset(gate.transform, "Return");
            root.localPosition = new Vector3(.54f, 1.03f, 1.04f);
            root.localRotation = Quaternion.Euler(0f, -90f, 0f);
            Box(root, "Case", Vector3.zero, new Vector3(.40f, .54f, .13f), steel);
            Box(root, "Mount", new Vector3(0f, -.51f, .03f), new Vector3(.08f, .67f, .1f), concrete);
            Box(root, "Plate", new Vector3(0f, -.23f, -.07f), new Vector3(.36f, .09f, .016f), cream);
            var latch = New(root, "Handle");
            latch.localPosition = new Vector3(0f, .08f, -.115f);
            Box(latch, "Grip", Vector3.zero, new Vector3(.22f, .036f, .054f), dark);
            Box(latch, "Link", new Vector3(0f, -.085f, .026f), new Vector3(.035f, .18f, .045f), steel);
            var grip = root.gameObject.AddComponent<Grip>();
            grip.mode = Grip.Mode.Pull;
            grip.travel = .12f;
            grip.axis = Vector3.back;
            UnityEventTools.AddPersistentListener(grip.used, gate.ToggleReturn);
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, .08f, -.10f);
            collider.size = new Vector3(.27f, .10f, .10f);
            collider.isTrigger = true;
            Wire(root.gameObject, collider);
            gate.returnLatch = latch;
            gate.returnDisplay = Text(root, "Label", new Vector3(0f, -.17f, -.083f), new Vector2(.36f, .13f), .075f, "RETURN LATCH\nPULL TO CLOSE", ink);
            AddPad(loop, root, "Return Pad", gate.transform.TransformPoint(new Vector3(1.2f, 0f, .34f)));
            EditorUtility.SetDirty(gate);
        }

        static void AddPad(Loop loop, Transform parent, string name, Vector3 point)
        {
            var source = loop.GetComponentsInChildren<NavPad>(true).Where(x => x && !x.transform.IsChildOf(parent)).OrderBy(x => Vector3.Distance(x.transform.position, point)).FirstOrDefault();
            if (!source) return;
            var obj = Object.Instantiate(source.gameObject, parent);
            obj.name = ObjectNames.Short(name);
            obj.transform.SetPositionAndRotation(point, Quaternion.identity);
            obj.transform.localScale = Vector3.one;
            var pad = obj.GetComponent<NavPad>();
            pad.destination = obj.transform;
            pad.action = "";
            pad.arrived = new UnityEvent();
            pad.guide = null;
            pad.unlocked = true;
            var anchor = obj.GetComponent<PadAnchor>();
            if (anchor) anchor.teleportAnchorTransform = obj.transform;
        }

        static Transform New(Transform parent, string name)
        {
            var result = new GameObject(ObjectNames.Short(name)).transform;
            result.SetParent(parent, false);
            return result;
        }

        static Transform Reset(Transform parent, string name)
        {
            var old = ObjectNames.Find(parent, name);
            if (old) Object.DestroyImmediate(old.gameObject);
            return New(parent, name);
        }

        static Transform Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = ObjectNames.Short(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            obj.transform.localScale = size;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return obj.transform;
        }

        static TMP_Text Text(Transform parent, string name, Vector3 position, Vector2 size, float fontSize, string content, Material color)
        {
            var obj = new GameObject(ObjectNames.Short(name));
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position;
            var text = obj.AddComponent<TextMeshPro>();
            text.font = font;
            text.rectTransform.sizeDelta = size;
            text.fontSize = fontSize * 4f;
            text.fontSizeMin = fontSize * 2.4f;
            text.fontSizeMax = fontSize * 4f;
            text.enableAutoSizing = true;
            text.alignment = TextAlignmentOptions.Center;
            text.text = content;
            text.color = color.color;
            text.raycastTarget = false;
            return text;
        }

        static Material Mat(string name, Color color)
        {
            string path = Folder + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void Wire(GameObject obj, Collider collider)
        {
            obj.layer = 8;
            var target = obj.AddComponent<XRSimpleInteractable>();
            target.colliders.Clear();
            target.colliders.Add(collider);
            obj.AddComponent<XRExperimentBridge>();
        }
    }
}
