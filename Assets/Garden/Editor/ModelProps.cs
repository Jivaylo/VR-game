using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Garden.Editor
{
    public static class ModelProps
    {
        static Transform props;
        static NavPad[] pads;
        static Material steel;
        static Bounds[] signs;
        static readonly List<Vector3> lamps = new List<Vector3>();

        public static void Apply(GameObject root)
        {
            var models = ObjectNames.Find(root.transform, "Models");
            if (!models) models = Group("Models", root.transform);
            var old = ObjectNames.Find(models, "Props");
            if (old) Object.DestroyImmediate(old.gameObject);
            props = Group("Props", models);
            pads = root.GetComponentsInChildren<NavPad>(true);
            steel = AssetDatabase.LoadAssetAtPath<Material>("Assets/Garden/Materials/Steel.mat");
            var streets = ObjectNames.Find(root.transform, "Streets");
            signs = streets ? streets.Cast<Transform>().Where(t => ObjectNames.Matches(t.name, "Directions")).SelectMany(t => t.GetComponentsInChildren<Renderer>(true)).Select(r => r.bounds).ToArray() : new Bounds[0];
            lamps.Clear();
            Physics.SyncTransforms();
            Chair(root.transform);
            Streets(root.transform);
            Walls(root.transform);
            Sign(root.transform);
            Cameras(root);
            Barriers(root.transform);
        }

        static Transform Group(string name, Transform parent)
        {
            var result = new GameObject(ObjectNames.Short(name)).transform;
            result.SetParent(parent, false);
            return result;
        }

        static void Chair(Transform root)
        {
            var home = ObjectNames.Find(root, "Home");
            if (!home) return;
            var chair = ObjectNames.Find(home, "Chair");
            if (!chair) return;
            foreach (var renderer in chair.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            foreach (string path in new[] { "Home/Fittings/Seat", "Home/Fittings/Back", "Fittings/Home/Seat", "Fittings/Home/Back" })
            {
                var duplicate = ObjectNames.Find(root, path);
                if (duplicate)
                    foreach (var renderer in duplicate.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            }
            var position = chair.position;
            position.y = home.position.y;
            ModelKit.Place("Chair", props, position, home.rotation, new Vector3(.50f, 1.03f, .62f));
        }

        static void Streets(Transform root)
        {
            var streets = ObjectNames.Find(root, "City/Streets");
            if (!streets) return;
            var stops = new[] { ObjectNames.Find(root, "Home"), ObjectNames.Find(root, "Breakfast"), ObjectNames.Find(root, "Factory"), ObjectNames.Find(root, "Yard") }.Where(x => x).ToArray();
            var lanes = streets.GetComponentsInChildren<Renderer>(true).Where(x => ObjectNames.Matches(x.name, "Lane"))
                .OrderBy(x => stops.Length == 0 ? 0 : stops.Min(t => Flat(x.bounds.center - t.position))).ToArray();
            foreach (var lane in lanes)
            {
                if (lamps.Count >= 14) break;
                var bounds = lane.bounds;
                bool horizontal = bounds.size.x > bounds.size.z;
                var along = horizontal ? Vector3.right : Vector3.forward;
                var side = horizontal ? Vector3.forward : Vector3.right;
                float length = horizontal ? bounds.size.x : bounds.size.z;
                float width = horizontal ? bounds.size.z : bounds.size.x;
                if (length < 4) continue;
                foreach (float fraction in new[] { -.27f, .27f, 0f })
                    foreach (float sign in new[] { -1f, 1f })
                    {
                        if (lamps.Count >= 14) break;
                        var point = bounds.center + along * length * fraction + side * sign * (width * .5f + .40f);
                        point.y = 0;
                        if (lamps.Any(x => Flat(x - point) < 49)) continue;
                        var rotation = Quaternion.LookRotation(-side * sign);
                        var size = new Vector3(.70f, 3.05f, 1.14f);
                        if (!Clear(point, rotation, size + new Vector3(.1f, 0, .1f), null)) continue;
                        ModelKit.Place("Lamp", props, point, rotation, size);
                        lamps.Add(point);
                    }
            }
        }

        static void Walls(Transform root)
        {
            var blocks = ObjectNames.Find(root, "City/Blocks");
            if (!blocks) return;
            var walls = blocks.GetComponentsInChildren<BoxCollider>(true).Where(x => x.bounds.size.x > 3 && x.bounds.size.z > 3)
                .OrderBy(x => pads.Min(p => Flat(x.bounds.center - p.transform.position))).ToArray();
            var used = new List<Vector3>();
            int ac = 0;
            int pipes = 0;
            foreach (var wall in walls)
            {
                if (ac >= 6 && pipes >= 3) break;
                var bounds = wall.bounds;
                foreach (var normal in new[] { Vector3.back, Vector3.right, Vector3.forward, Vector3.left })
                {
                    float extent = Mathf.Abs(normal.x) > .5f ? bounds.extents.x : bounds.extents.z;
                    var edge = bounds.center + normal * extent;
                    edge.y = bounds.min.y;
                    if (pads.Min(p => Flat(p.transform.position - edge)) > 70) continue;
                    if (used.Any(x => Flat(x - edge) < 36)) continue;
                    var rotation = Quaternion.LookRotation(normal);
                    var size = new Vector3(1.0f, .79f, .44f);
                    float level = bounds.size.y / Mathf.Max(1, Mathf.RoundToInt(bounds.size.y / 3.3f));
                    var position = edge + normal * (size.z * .5f + .016f) + Vector3.up * (level + .20f);
                    if (ac < 6 && Clear(position, rotation, size, wall))
                    {
                        ModelKit.Place("AC", props, position, rotation, size);
                        foreach (float side in new[] { -.36f, .36f })
                            Box("Bracket", position + rotation * new Vector3(side, -.035f, 0), new Vector3(.065f, .07f, .49f), rotation);
                        used.Add(edge);
                        ac++;
                    }
                    if (pipes >= 3) continue;
                    string key = new[] { "Pipe", "PipeBend", "PipeEnd" }[pipes];
                    var raw = ModelKit.Size(key);
                    size = raw * (.22f / raw.z);
                    size.y = 2.9f;
                    var tangent = rotation * Vector3.right;
                    var pipeEdge = edge + tangent * (Mathf.Abs(normal.x) > .5f ? bounds.extents.z - .45f : bounds.extents.x - .45f);
                    position = pipeEdge + normal * (size.z * .5f - .008f) + Vector3.up * .20f;
                    if (!Clear(position, rotation, size, wall)) continue;
                    ModelKit.Place(key, props, position, rotation, size);
                    pipes++;
                }
            }
        }

        static void Sign(Transform root)
        {
            var counter = ObjectNames.Find(root, "Breakfast/Counter");
            if (!counter || !counter.TryGetComponent<Renderer>(out var surface)) return;
            var header = ObjectNames.Find(root, "Streets/Commons");
            if (header) Object.DestroyImmediate(header.gameObject);
            var bounds = surface.bounds;
            var center = bounds.center;
            center.y = bounds.max.y;
            var rotation = counter.rotation;
            float top = center.y + 1.44f;
            var point = new Vector3(center.x, top - .84f, center.z);
            ModelKit.Place("Sign", props, point, rotation, new Vector3(1.22f, .84f, .14f));
            foreach (float side in new[] { -1.0f, 1.0f })
                Box("Post", center + rotation * new Vector3(side, .72f, 0), new Vector3(.055f, 1.44f, .055f), rotation);
            Box("Beam", new Vector3(center.x, top + .025f, center.z), new Vector3(2.05f, .05f, .065f), rotation);
            foreach (float side in new[] { -.40f, .40f })
                Box("Hook", new Vector3(center.x, top, center.z) + rotation * new Vector3(side, 0, 0), new Vector3(.035f, .05f, .16f), rotation);
            foreach (float side in new[] { -1f, 1f })
            {
                var label = new GameObject("Text", typeof(TextMeshPro)).GetComponent<TextMeshPro>();
                label.transform.SetParent(props, false);
                label.transform.SetPositionAndRotation(point + rotation * new Vector3(0, .28f, .076f * side), rotation * Quaternion.Euler(0, side > 0 ? 180 : 0, 0));
                label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
                label.text = "COMMONS 02";
                label.fontSize = .9f;
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(.84f, .88f, .82f);
                label.rectTransform.sizeDelta = new Vector2(1.06f, .32f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
            }
        }

        static void Cameras(GameObject root)
        {
            foreach (var drone in root.GetComponentsInChildren<Drone>(true))
            {
                if (!drone.cameraOnly || !drone.visual) continue;
                var old = ObjectNames.Find(drone.visual, "Camera");
                if (old) Object.DestroyImmediate(old.gameObject);
                foreach (var renderer in drone.visual.GetComponentsInChildren<Renderer>(true))
                    if (renderer != drone.lamp && (!drone.status || renderer.gameObject != drone.status.gameObject)) renderer.enabled = false;
                var camera = ModelKit.Place("Camera", props, drone.visual.position, drone.visual.rotation, new Vector3(.34f, .50f, .72f));
                GameObjectUtility.SetStaticEditorFlags(camera, 0);
                camera.transform.position += drone.visual.position - End(camera, true);
                camera.transform.SetParent(drone.visual, true);
                GameObjectUtility.SetStaticEditorFlags(camera, 0);
                if (drone.lamp)
                {
                    drone.lamp.transform.SetParent(drone.visual, true);
                    drone.lamp.transform.position = drone.visual.position + drone.visual.rotation * new Vector3(.18f, -.28f, .31f);
                    drone.lamp.transform.rotation = drone.visual.rotation;
                    drone.lamp.transform.localScale = new Vector3(.055f / drone.visual.lossyScale.x, .028f / drone.visual.lossyScale.y, .012f / drone.visual.lossyScale.z);
                }
                var start = drone.visual.position + Vector3.up * .025f;
                if (Physics.Raycast(start, -drone.transform.forward, out var wall, 2f, 1, QueryTriggerInteraction.Ignore))
                {
                    var end = wall.point;
                    var delta = end - start;
                    Box("Bracket", (start + end) * .5f, new Vector3(.075f, .075f, delta.magnitude + .02f), Quaternion.LookRotation(delta));
                    Box("Mount", end, new Vector3(.20f, .24f, .045f), Quaternion.LookRotation(wall.normal));
                }
            }
        }

        static void Barriers(Transform root)
        {
            var yard = ObjectNames.Find(root, "Yard");
            if (!yard) return;
            foreach (var wall in yard.GetComponentsInChildren<BoxCollider>(true))
            {
                if (!ObjectNames.Matches(wall.name, "Wall")) continue;
                var bounds = wall.bounds;
                if (bounds.size.x < 8 || bounds.size.x > 20 || bounds.size.y < 4 || bounds.size.z > .5f) continue;
                int count = Mathf.FloorToInt(bounds.size.x / 2.2f);
                if (count < 1) continue;
                float width = bounds.size.x / count;
                for (int i = 0; i < count; i++)
                    ModelKit.Place("Barrier", props, new Vector3(bounds.min.x + width * (i + .5f), bounds.max.y, bounds.center.z), Quaternion.identity, new Vector3(width, .90f, .18f));
            }
        }

        static bool Clear(Vector3 point, Quaternion rotation, Vector3 size, Collider mount)
        {
            var center = point + Vector3.up * (size.y * .5f);
            var right = rotation * Vector3.right * size.x;
            var up = rotation * Vector3.up * size.y;
            var forward = rotation * Vector3.forward * size.z;
            var box = new Bounds(center, new Vector3(Mathf.Abs(right.x) + Mathf.Abs(up.x) + Mathf.Abs(forward.x), Mathf.Abs(right.y) + Mathf.Abs(up.y) + Mathf.Abs(forward.y), Mathf.Abs(right.z) + Mathf.Abs(up.z) + Mathf.Abs(forward.z)));
            if (signs.Any(b => b.Intersects(box))) return false;
            foreach (var pad in pads)
            {
                var foot = pad.transform.position;
                if (foot.y + 2.15f < point.y || foot.y > point.y + size.y + .1f) continue;
                var local = Quaternion.Inverse(rotation) * (foot - center);
                float dx = Mathf.Max(0, Mathf.Abs(local.x) - size.x * .5f);
                float dz = Mathf.Max(0, Mathf.Abs(local.z) - size.z * .5f);
                if (dx * dx + dz * dz < .49f) return false;
            }
            foreach (var hit in Physics.OverlapBox(center + Vector3.up * .015f, size * .5f - Vector3.up * .015f, rotation, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit == mount || !hit.enabled || !hit.gameObject.activeInHierarchy) continue;
                if (hit.GetComponentInParent<NavPad>()) continue;
                if (hit.bounds.max.y <= point.y + .025f) continue;
                return false;
            }
            return true;
        }

        static float Flat(Vector3 value) => value.x * value.x + value.z * value.z;

        static Vector3 End(GameObject model, bool top)
        {
            var vertices = model.GetComponentsInChildren<MeshFilter>(true).Where(x => x.sharedMesh && x.sharedMesh.isReadable)
                .SelectMany(x => x.sharedMesh.vertices.Select(v => x.transform.TransformPoint(v))).ToArray();
            if (vertices.Length == 0) return model.transform.position;
            float end = top ? vertices.Max(x => x.y) : vertices.Min(x => x.y);
            var rim = vertices.Where(x => Mathf.Abs(x.y - end) < .006f).ToArray();
            var point = Vector3.zero;
            foreach (var vertex in rim) point += vertex;
            point /= Mathf.Max(1, rim.Length);
            point.y = end;
            return point;
        }

        static void Box(string name, Vector3 center, Vector3 size, Quaternion rotation)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = ObjectNames.Short(name);
            cube.transform.SetParent(props, false);
            cube.transform.SetPositionAndRotation(center, rotation);
            cube.transform.localScale = size;
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            var renderer = cube.GetComponent<Renderer>();
            renderer.sharedMaterial = steel;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }
}
