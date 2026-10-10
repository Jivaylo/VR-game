using System;
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
    public static class SessionStyle
    {
        static TMP_FontAsset font;
        static Material screen, casing, paper, mint;

        public static void Apply(GameObject root)
        {
            var loop = root.GetComponent<Loop>();
            if (!loop || !loop.talk || !loop.talk.wrist || !loop.talk.wrist.housing) throw new InvalidOperationException("Missing wrist housing.");
            var housing = loop.talk.wrist.housing;
            var old = ObjectNames.Find(housing, "Session");
            if (old) Object.DestroyImmediate(old.gameObject);
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            screen = AssetDatabase.LoadAssetAtPath<Material>("Assets/Garden/Art/Story/Screen.mat");
            casing = AssetDatabase.LoadAssetAtPath<Material>("Assets/Garden/Art/Story/Case.mat");
            paper = AssetDatabase.LoadAssetAtPath<Material>("Assets/Garden/Art/Story/Label.mat");
            mint = AssetDatabase.LoadAssetAtPath<Material>("Assets/Garden/Art/Story/Mint.mat");
            if (!font || !screen || !casing || !paper || !mint) throw new InvalidOperationException("Apply Story materials first.");
            var mount = New(housing, "Session");
            var session = mount.gameObject.AddComponent<Session>();
            session.loop = loop;
            Box(mount, "Mount", new Vector3(-.084f, .044f, .012f), new Vector3(.04f, .008f, .026f), casing);
            var tab = New(mount, "Tab");
            tab.localPosition = new Vector3(-.104f, .051f, .012f);
            tab.localRotation = Quaternion.Euler(90, 0, 0);
            Control(tab, "Open", Vector3.zero, new Vector2(.035f, .024f), "SESSION", .047f, session.Open);
            var panel = New(mount, "Panel");
            panel.localPosition = new Vector3(-.325f, .12f, -.012f);
            panel.localRotation = Quaternion.Euler(65, 0, 0);
            session.panel = panel.gameObject;
            Box(panel, "Case", Vector3.zero, new Vector3(.43f, .35f, .012f), casing);
            Box(panel, "Screen", new Vector3(0, 0, -.009f), new Vector3(.415f, .335f, .006f), screen);
            Text(panel, "Title", new Vector3(0, .146f, -.016f), new Vector2(.39f, .032f), .19f, "SESSION");
            session.checkpoint = Text(panel, "Checkpoint", new Vector3(0, .104f, -.016f), new Vector2(.38f, .045f), .105f, "CURRENT CHECKPOINT");
            var menu = New(panel, "Menu");
            session.menu = menu.gameObject;
            Control(menu, "Resume", new Vector3(0, .048f, -.024f), new Vector2(.37f, .038f), "KEEP PLAYING", .145f, session.Resume);
            Control(menu, "Reload", new Vector3(-.097f, -.002f, -.024f), new Vector2(.176f, .039f), "RELOAD CHECKPOINT", .115f, session.Checkpoint);
            Control(menu, "Restart", new Vector3(.097f, -.002f, -.024f), new Vector2(.176f, .039f), "START AGAIN", .125f, session.StartAgain);
            var patrol = Control(menu, "Patrol", new Vector3(0, -.052f, -.024f), new Vector2(.37f, .039f), "SLOW PATROLS   OFF", .125f, session.TogglePatrol);
            session.patrol = patrol.GetComponentInChildren<TMP_Text>();
            Text(menu, "Hint", new Vector3(0, -.09f, -.016f), new Vector2(.38f, .032f), .095f, "Slower movement and more time to leave a sensor's view.");
            var confirmation = New(panel, "Confirm");
            session.confirmation = confirmation.gameObject;
            Text(confirmation, "Prompt", new Vector3(0, .032f, -.016f), new Vector2(.38f, .10f), .125f, "Start from the first morning?\nYour current progress will be backed up.\nThis starts a new game.");
            Control(confirmation, "Confirm", new Vector3(-.097f, -.045f, -.024f), new Vector2(.176f, .043f), "START AGAIN", .125f, session.Confirm);
            Control(confirmation, "Cancel", new Vector3(.097f, -.045f, -.024f), new Vector2(.176f, .043f), "KEEP PLAYING", .125f, session.Cancel);
            Text(confirmation, "Timeout", new Vector3(0, -.094f, -.016f), new Vector2(.38f, .032f), .10f, "This confirmation closes after 15 seconds.");
            session.status = Text(panel, "Status", new Vector3(0, -.139f, -.017f), new Vector2(.39f, .048f), .10f, "Game controls. Detection is held while this panel is open.");
            confirmation.gameObject.SetActive(false);
            panel.gameObject.SetActive(false);
            EditorUtility.SetDirty(session);
        }

        static Transform Control(Transform parent, string name, Vector3 point, Vector2 size, string value, float textSize, UnityAction action)
        {
            var root = New(parent, name);
            root.localPosition = point;
            root.gameObject.layer = 8;
            var face = Box(root, "Face", Vector3.zero, new Vector3(size.x, size.y, .012f), mint);
            var label = Text(root, "Label", new Vector3(0, 0, -.008f), size * .96f, textSize, value);
            label.color = screen.GetColor("_BaseColor");
            var button = root.gameObject.AddComponent<Garden.Button>();
            button.visual = face;
            button.action = "";
            UnityEventTools.AddPersistentListener(button.pressed, action);
            var collider = root.gameObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(size.x, size.y, .018f);
            collider.isTrigger = true;
            var interactable = root.gameObject.AddComponent<XRSimpleInteractable>();
            interactable.colliders.Clear();
            interactable.colliders.Add(collider);
            root.gameObject.AddComponent<XRExperimentBridge>();
            return root;
        }

        static Transform New(Transform parent, string name)
        {
            var root = new GameObject(ObjectNames.Short(name)).transform;
            root.SetParent(parent, false);
            return root;
        }

        static Transform Box(Transform parent, string name, Vector3 point, Vector3 size, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = ObjectNames.Short(name);
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = point;
            obj.transform.localScale = size;
            Object.DestroyImmediate(obj.GetComponent<Collider>());
            var renderer = obj.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return obj.transform;
        }

        static TMP_Text Text(Transform parent, string name, Vector3 point, Vector2 size, float fontSize, string value)
        {
            var text = New(parent, name).gameObject.AddComponent<TextMeshPro>();
            text.transform.localPosition = point;
            text.font = font;
            text.fontSize = fontSize;
            text.color = paper.GetColor("_BaseColor");
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = size;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.richText = false;
            text.text = value;
            return text;
        }
    }
}
