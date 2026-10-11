using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;

namespace Garden.Editor
{
    public static class GardenFlow
    {
        [Serializable] sealed class Voice { public string text, path; }
        [Serializable] sealed class Voices { public Voice[] lines; }

        public static void Apply(GameObject root)
        {
            var loop = root.GetComponent<Loop>();
            if (!loop) throw new InvalidOperationException("Garden controller is missing.");
            loop.breakfast = ObjectNames.Find(root.transform, "Breakfast");
            loop.factory = ObjectNames.Find(root.transform, "Factory");
            loop.concourse = ObjectNames.Find(root.transform, "Concourse");
            var delivery = root.GetComponentInChildren<Delivery>(true);
            var grips = root.GetComponentsInChildren<Grip>(true);
            loop.dinnerPoint = grips.FirstOrDefault(x => Action(x.action, x.used) == "Dinner")?.transform;
            loop.bed = grips.FirstOrDefault(x => Action(x.action, x.used) == "RestNight" || Action(x.action, x.used) == "Sleep")?.transform;
            foreach (var grip in grips)
            {
                string action = Action(grip.action, grip.used);
                if (!new[] { "Dinner", "Sleep", "RestNight", "SleepOffer", "OfferLocal", "Glove", "EnterAngel", "AskAngel", "LeaveAngel", "News", "Noor", "Rest", "Water", "Evidence" }.Contains(action)) continue;
                var control = grip.GetComponent<Control>();
                if (!control) control = grip.gameObject.AddComponent<Control>();
                control.loop = loop; control.action = action; control.grip = grip;
                control.model = grip.model ? grip.model.gameObject : null;
                control.hide = action == "OfferLocal" || action == "Glove";
            }
            foreach (var pad in root.GetComponentsInChildren<NavPad>(true)) pad.hideUnavailable = true;
            foreach (var hatch in root.GetComponentsInChildren<Hatch>(true))
            {
                var module = hatch.GetComponentInParent<Link>();
                if (!module) continue;
                hatch.marker = module.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(x => x.text.StartsWith("SERVICE HATCH\n") || x.text.StartsWith("CONCOURSE HATCH\n") || x.text.StartsWith("GARDEN VIEW\n"));
                if (hatch.marker) hatch.marker.text = loop.state.calibrated ? "SERVICE HATCH\nTrace with the glove" : "GARDEN VIEW\nA moment of calm";
                if (hatch.display) hatch.display.gameObject.SetActive(loop.state.calibrated || loop.state.traced || loop.state.hatch || loop.state.local);
            }
            if (loop.receiver)
            {
                var schedule = loop.receiver.GetComponent<Schedule>();
                if (!schedule) schedule = loop.receiver.gameObject.AddComponent<Schedule>();
                schedule.loop = loop; schedule.kind = Schedule.Kind.Receiver; schedule.detail = loop.receiver;
            }
            if (delivery && delivery.package)
            {
                var carry = delivery.package.GetComponent<Carry>();
                if (!carry) carry = delivery.package.AddComponent<Carry>();
                carry.dinner = true; carry.model = delivery.package.transform;
            }
            var home = ObjectNames.Find(root.transform, "Home");
            if (home && delivery) PlaceDelivery(home, delivery, loop.dinnerPoint);
            if (home && loop.overlays)
            {
                var panel = Board(loop.overlays.transform, "Schedule", home.TransformPoint(new Vector3(-2.86f, 1.75f, -1.05f)), home.rotation * Quaternion.Euler(0, 270, 0), new Vector2(1.38f, .96f), loop, Schedule.Kind.Day);
                panel.layer = 11;
                Board(loop.overlays.transform, "Rest", home.TransformPoint(new Vector3(-1.62f, 1.2f, 1.96f)), home.rotation, new Vector2(.94f, .45f), loop, Schedule.Kind.Bed);
            }
            string path = "Assets/Garden/Audio/Voices.json";
            if (loop.talk && File.Exists(path))
            {
                var data = JsonUtility.FromJson<Voices>(File.ReadAllText(path));
                if (data?.lines != null) loop.talk.lines = data.lines.Select(x => new Talk.Line { text = x.text, clip = AssetDatabase.LoadAssetAtPath<AudioClip>(x.path.Replace('\\', '/')) }).Where(x => x.clip).ToArray();
            }
            PadStyle.Apply(root);
            WristStyle.Apply(root);
            EditorUtility.SetDirty(root);
        }

        static void PlaceDelivery(Transform home, Delivery delivery, Transform order)
        {
            if (!delivery.package) return;
            Vector3 serving = new Vector3(1.45f, 1.08f, .74f);
            delivery.package.transform.position = home.TransformPoint(serving);
            if (delivery.landing) delivery.landing.position = home.TransformPoint(serving + new Vector3(0, .30f, -.06f));
            var shelf = ObjectNames.Find(delivery.transform, "Shelf");
            if (shelf)
            {
                shelf.SetPositionAndRotation(home.TransformPoint(serving + Vector3.down * .105f), home.rotation);
                shelf.localScale = new Vector3(.68f, .11f, .5f);
                var stand = ObjectNames.Find(delivery.transform, "Stand");
                if (!stand)
                {
                    stand = new GameObject("Stand").transform;
                    stand.SetParent(delivery.transform, false);
                }
                stand.SetPositionAndRotation(home.position, home.rotation);
                for (int i = 0; i < 2; i++)
                {
                    string name = i == 0 ? "Left" : "Right";
                    var support = ObjectNames.Find(stand, name);
                    if (!support)
                    {
                        support = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                        support.name = ObjectNames.Short(name);
                        support.SetParent(stand, false);
                    }
                    support.localPosition = new Vector3(i == 0 ? 1.25f : 1.65f, .815f, .62f);
                    support.localRotation = Quaternion.identity;
                    support.localScale = new Vector3(.035f, .21f, .035f);
                    var renderer = support.GetComponent<Renderer>();
                    var top = shelf.GetComponent<Renderer>();
                    if (renderer && top) renderer.sharedMaterial = top.sharedMaterial;
                    if (renderer) renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            if (order) order.SetPositionAndRotation(home.TransformPoint(new Vector3(1.45f, .91f, .445f)), home.rotation);
            if (delivery.drone && delivery.landing)
            {
                var direction = Vector3.ProjectOnPlane(delivery.landing.position - delivery.drone.position, home.up);
                if (direction.sqrMagnitude > .01f) delivery.drone.rotation = Quaternion.LookRotation(-direction, home.up);
            }
        }

        static string Action(string action, UnityEvent used)
        {
            if (!string.IsNullOrEmpty(action)) return action;
            return used.GetPersistentEventCount() > 0 ? used.GetPersistentMethodName(0) : "";
        }

        static GameObject Board(Transform parent, string name, Vector3 position, Quaternion rotation, Vector2 size, Loop loop, Schedule.Kind kind)
        {
            var old = ObjectNames.Find(parent, name);
            if (old) UnityEngine.Object.DestroyImmediate(old.gameObject);
            var root = new GameObject(ObjectNames.Short(name));
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, rotation);
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Garden/Materials/Ink.mat");
            var backing = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backing.name = "Back";
            UnityEngine.Object.DestroyImmediate(backing.GetComponent<Collider>());
            backing.transform.SetParent(root.transform, false);
            backing.transform.localPosition = Vector3.forward * .018f;
            backing.transform.localScale = new Vector3(size.x + .08f, size.y + .07f, .025f);
            backing.GetComponent<Renderer>().sharedMaterial = material;
            backing.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            var schedule = root.AddComponent<Schedule>();
            schedule.loop = loop; schedule.kind = kind;
            schedule.title = Label(root.transform, "Title", new Vector3(0, size.y * .38f, -.005f), new Vector2(size.x, .10f), .054f, new Color(.47f, .82f, .78f));
            schedule.detail = Label(root.transform, "Text", new Vector3(0, -.05f, -.005f), new Vector2(size.x, size.y * .7f), kind == Schedule.Kind.Day ? .058f : .05f, new Color(.86f, .91f, .87f));
            schedule.Refresh();
            foreach (var t in root.GetComponentsInChildren<Transform>()) t.gameObject.layer = 11;
            return root;
        }

        static TMP_Text Label(Transform parent, string name, Vector3 at, Vector2 size, float height, Color color)
        {
            var root = new GameObject(ObjectNames.Short(name));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = at;
            var text = root.AddComponent<TextMeshPro>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            text.fontSize = height * 10;
            text.rectTransform.sizeDelta = size;
            text.alignment = TextAlignmentOptions.Left;
            text.color = color;
            text.richText = false;
            text.enableAutoSizing = false;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }
    }
}
