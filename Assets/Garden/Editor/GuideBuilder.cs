using System;
using System.Collections.Generic;
using System.Linq;
using RealityPlayground;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Garden.Editor
{
    public static class GuideBuilder
    {
        const string MeshPath = "Assets/Garden/Materials/Trail.asset";
        const string MaterialPath = "Assets/Garden/Materials/Guide.mat";

        public static void Apply(GameObject root)
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode first.");
            var loop = root.GetComponent<Loop>();
            if (!loop || !loop.overlays) throw new InvalidOperationException("Garden loop and overlays required.");
            foreach (var old in root.GetComponentsInChildren<Guide>(true)) UnityEngine.Object.DestroyImmediate(old.gameObject);
            Physics.SyncTransforms();
            var obj = new GameObject("Guide");
            obj.transform.SetParent(root.transform, false);
            obj.layer = 11;
            var guide = obj.AddComponent<Guide>();
            guide.loop = loop;
            guide.train = root.GetComponentInChildren<Train>(true);
            CabinPad(guide.train);
            guide.delivery = root.GetComponentInChildren<Delivery>(true);
            guide.homeDoor = root.GetComponentsInChildren<Door>(true).FirstOrDefault(x => !x.localOnly);
            guide.innerDoor = root.GetComponentsInChildren<Door>(true).FirstOrDefault(x => x.localOnly);
            guide.gate = root.GetComponentInChildren<Gate>(true);
            guide.nodes = root.GetComponentsInChildren<NavPad>(true);
            foreach (var node in guide.nodes) { node.guide = guide; node.hideUnavailable = true; EditorUtility.SetDirty(node); }
            guide.edges = Links(root, guide.nodes).ToArray();
            var grips = root.GetComponentsInChildren<Grip>(true);
            guide.doorHandles = grips.Where(x => Calls(x, guide.homeDoor, "Toggle")).Select(x => x.transform).ToArray();
            guide.innerHandles = grips.Where(x => Calls(x, guide.innerDoor, "Toggle")).Select(x => x.transform).ToArray();
            guide.breakfast = root.GetComponentsInChildren<Carry>(true).FirstOrDefault(x => x.meal && !x.dinner)?.transform;
            guide.metro = guide.nodes.FirstOrDefault(x => Calls(x.arrived, guide.train, "BoardSouth"))?.transform;
            if (!guide.metro) guide.metro = guide.train ? guide.train.south : null;
            var assembly = root.GetComponentInChildren<Assembly>(true);
            var control = root.GetComponentsInChildren<Button>(true).FirstOrDefault(x => Calls(x, assembly, "Accept"));
            guide.station = control ? control.transform : loop.factory;
            guide.dinner = grips.FirstOrDefault(x => x.action == "Dinner")?.transform;
            guide.bed = grips.FirstOrDefault(x => x.action == "RestNight")?.transform;
            guide.trainDoor = grips.FirstOrDefault(x => Calls(x, guide.train, "ToggleDoor"))?.transform;
            guide.trainRide = grips.FirstOrDefault(x => Calls(x, guide.train, "Ride"))?.transform;
            guide.trainExit = guide.nodes.FirstOrDefault(x => Calls(x.arrived, guide.train, "Leave"))?.transform;
            guide.threshold = guide.nodes.FirstOrDefault(x => x.action == "ReachOutside" || Calls(x.arrived, loop, "ReachOutside"))?.transform;
            guide.signalExit = grips.FirstOrDefault(x => x.action == "LeaveAngel" || Calls(x, loop, "LeaveAngel"))?.transform;
            guide.stops = Stops(root, grips).ToArray();
            var material = Material();
            guide.arrows = Array.Empty<Renderer>();
            guide.beam = Line(obj.transform, "Route", material, .12f);
            guide.pulse = Line(obj.transform, "Pulse", material, .19f);
            guide.pin = Line(obj.transform, "Pin", material, .012f);
            guide.beam.alignment = guide.pulse.alignment = LineAlignment.TransformZ;
            guide.beam.transform.rotation = guide.pulse.transform.rotation = Quaternion.Euler(90, 0, 0);
            guide.signs = new TMP_Text[4];
            for (int i = 0; i < guide.signs.Length; i++)
            {
                var sign = Text(obj.transform, "Stop", new Vector2(.9f, .14f), .24f);
                sign.fontStyle = FontStyles.Bold;
                guide.signs[i] = sign;
            }
            var text = Text(obj.transform, "Action", new Vector2(.9f, .22f), .50f);
            text.fontStyle = FontStyles.Bold;
            text.color = new Color(.52f, .94f, .83f);
            text.enableAutoSizing = true;
            text.fontSizeMin = .36f;
            text.fontSizeMax = .50f;
            text.text = "CIVIC";
            guide.label = text;
            var plate = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plate.name = "Plate";
            plate.layer = 11;
            UnityEngine.Object.DestroyImmediate(plate.GetComponent<Collider>());
            plate.transform.SetParent(text.transform, false);
            plate.transform.localPosition = Vector3.forward * .018f;
            plate.transform.localScale = new Vector3(.94f, .24f, 1);
            var plateView = plate.GetComponent<Renderer>();
            plateView.sharedMaterial = Plate();
            plateView.shadowCastingMode = ShadowCastingMode.Off;
            plateView.receiveShadows = false;
            var frame = Line(text.transform, "Frame", material, .003f);
            frame.useWorldSpace = false;
            frame.positionCount = 5;
            frame.SetPositions(new[] { new Vector3(-.465f, -.115f, .008f), new Vector3(-.465f, .115f, .008f), new Vector3(.465f, .115f, .008f), new Vector3(.465f, -.115f, .008f), new Vector3(-.465f, -.115f, .008f) });
            frame.enabled = true;
            EditorUtility.SetDirty(guide);
            AssetDatabase.SaveAssets();
        }

        static TMP_Text Text(Transform parent, string name, Vector2 size, float font)
        {
            var obj = new GameObject(ObjectNames.Short(name));
            obj.layer = 11;
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<TextMeshPro>();
            text.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            text.fontSize = font;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.sizeDelta = size;
            text.richText = false;
            text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Overflow;
            obj.SetActive(false);
            return text;
        }

        static void CabinPad(Train train)
        {
            if (!train || !train.cabin || !train.cabin.parent || ObjectNames.Find(train.cabin.parent, "Door Pad")) return;
            var car = train.cabin.parent;
            var handle = car.GetComponentsInChildren<Grip>(true).FirstOrDefault(x => Calls(x, train, "ToggleDoor"));
            var source = car.GetComponentsInChildren<NavPad>(true).FirstOrDefault(x => ObjectNames.Matches(x.name, "Stand"));
            if (!handle || !source) return;
            Vector3 point = handle.transform.position;
            point.y = train.cabin.position.y;
            if (car.GetComponentsInChildren<NavPad>(true).Any(x => x.arrived.GetPersistentEventCount() == 0 && string.IsNullOrEmpty(x.action) && Vector3.Distance(Point(x), point) <= 1.05f)) return;
            var direction = Vector3.ProjectOnPlane(train.cabin.position - point, Vector3.up).normalized;
            point += direction * .75f;
            if (!Physics.Raycast(point + Vector3.up * .18f, Vector3.down, out var hit, .38f, 1, QueryTriggerInteraction.Ignore) || hit.normal.y < .7f)
                throw new InvalidOperationException("Cabin door approach has no floor.");
            if (Physics.CheckCapsule(point + Vector3.up * .27f, point + Vector3.up * 1.48f, .22f, 1, QueryTriggerInteraction.Ignore))
                throw new InvalidOperationException("Cabin door approach is blocked.");
            var pad = UnityEngine.Object.Instantiate(source, car);
            pad.name = "DoorPad";
            pad.transform.position = point;
            pad.destination = pad.transform;
            pad.arrived = new UnityEngine.Events.UnityEvent();
            pad.action = "";
            Physics.SyncTransforms();
        }

        static LineRenderer Line(Transform parent, string name, Material material, float width)
        {
            var obj = new GameObject(ObjectNames.Short(name));
            obj.layer = 11;
            obj.transform.SetParent(parent, false);
            var line = obj.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = width;
            line.numCapVertices = 4;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            line.enabled = false;
            return line;
        }

        static List<Guide.Stop> Stops(GameObject root, Grip[] grips)
        {
            var stops = new List<Guide.Stop>();
            foreach (var grip in grips)
            {
                string action = grip.action;
                if (string.IsNullOrEmpty(action) && grip.used.GetPersistentEventCount() > 0) action = grip.used.GetPersistentMethodName(0);
                string title = action == "Mara" ? "MARA" : action == "Cup" ? "CUP" : action == "Wash" ? "WATER" : action == "News" ? "NEWS" : action == "Glove" ? "SERVICE GLOVE" : action == "ViewSample" ? "SURVEY" : action == "Review" ? "INSPECTION" : action == "EnterAngel" ? "SIGNAL" : action == "Noor" ? "NOOR" : action == "Water" ? "PLANTS" : action == "Rest" ? "REST" : "";
                if (title.Length > 0) stops.Add(new Guide.Stop { point = grip.transform, title = title, need = action == "ViewSample" ? "Survey" : action });
            }
            foreach (var mirror in root.GetComponentsInChildren<Garden.Mirror>(true))
                stops.Add(new Guide.Stop { point = mirror.surface ? mirror.surface : mirror.transform, title = "MIRROR", need = "Mirror" });
            foreach (var hatch in root.GetComponentsInChildren<Hatch>(true))
                stops.Add(new Guide.Stop { point = hatch.transform, title = "SERVICE SEAM", need = "Hatch" });
            foreach (var bridge in root.GetComponentsInChildren<Bridge>(true))
                stops.Add(new Guide.Stop { point = bridge.transform, title = "BRIDGE", need = "Bridge" });
            foreach (var poster in root.GetComponentsInChildren<Poster>(true))
                stops.Add(new Guide.Stop { point = poster.paper ? poster.paper : poster.transform, title = "ALCOVE", need = "Poster" });
            foreach (var peel in root.GetComponentsInChildren<Peel>(true))
                stops.Add(new Guide.Stop { point = peel.transform, title = "WRAPPER", need = "Peel" });
            return stops;
        }

        static bool Calls(UnityEngine.Events.UnityEvent action, UnityEngine.Object target, string method)
        {
            for (int i = 0; i < action.GetPersistentEventCount(); i++)
                if (action.GetPersistentTarget(i) == target && action.GetPersistentMethodName(i) == method) return true;
            return false;
        }

        static bool Calls(Grip grip, UnityEngine.Object target, string method)
        {
            for (int i = 0; i < grip.used.GetPersistentEventCount(); i++)
                if (grip.used.GetPersistentTarget(i) == target && grip.used.GetPersistentMethodName(i) == method) return true;
            return false;
        }

        static bool Calls(Button button, UnityEngine.Object target, string method)
        {
            for (int i = 0; i < button.pressed.GetPersistentEventCount(); i++)
                if (button.pressed.GetPersistentTarget(i) == target && button.pressed.GetPersistentMethodName(i) == method) return true;
            return false;
        }

        static List<Guide.Edge> Links(GameObject root, NavPad[] nodes)
        {
            var barriers = Barriers(root);
            var actors = Actors(root);
            var links = new List<Guide.Edge>();
            for (int from = 0; from < nodes.Length; from++)
            {
                if (!nodes[from]) continue;
                var a = Point(nodes[from]);
                for (int to = 0; to < nodes.Length; to++)
                {
                    if (from == to || !nodes[to]) continue;
                    var b = Point(nodes[to]);
                    var offset = b - a;
                    float horizontal = new Vector2(offset.x, offset.z).magnitude;
                    if (horizontal > Mathf.Min(11f, nodes[to].maxDistance) || Mathf.Abs(offset.y) > Mathf.Min(2f, nodes[to].maxRise)) continue;
                    if (!Clear(a + Vector3.up * 1.65f, b + Vector3.up * .12f, barriers, actors, out var boundary)) continue;
                    links.Add(new Guide.Edge { from = from, to = to, cost = Mathf.Max(.05f, offset.magnitude), boundary = boundary });
                }
            }
            return links;
        }

        static HashSet<Collider> Barriers(GameObject root)
        {
            var barriers = new HashSet<Collider>();
            foreach (var door in root.GetComponentsInChildren<Door>(true)) if (door.barrier) barriers.Add(door.barrier);
            foreach (var hatch in root.GetComponentsInChildren<Hatch>(true)) if (hatch.barrier) barriers.Add(hatch.barrier);
            foreach (var gate in root.GetComponentsInChildren<Gate>(true)) if (gate.barrier) barriers.Add(gate.barrier);
            return barriers;
        }

        static HashSet<Collider> Actors(GameObject root)
        {
            var result = new HashSet<Collider>();
            foreach (var player in root.GetComponentsInChildren<RealityPlayer>(true))
            {
                var parts = new[] { player.xrOrigin ? player.xrOrigin.transform : null, player.desktopRoot ? player.desktopRoot.transform : null, player.avatarBody, player.avatarHead };
                foreach (var part in parts)
                    if (part) foreach (var collider in part.GetComponentsInChildren<Collider>(true)) result.Add(collider);
            }
            return result;
        }

        static bool Clear(Vector3 from, Vector3 to, HashSet<Collider> barriers, HashSet<Collider> actors, out Collider boundary)
        {
            boundary = null;
            var offset = to - from;
            foreach (var hit in Physics.RaycastAll(from, offset.normalized, Mathf.Max(0, offset.magnitude - .015f), 1, QueryTriggerInteraction.Ignore))
            {
                if (actors.Contains(hit.collider) || hit.collider.GetComponentInParent<NavPad>()) continue;
                if (!barriers.Contains(hit.collider)) return false;
                if (boundary && boundary != hit.collider) return false;
                boundary = hit.collider;
            }
            return true;
        }

        static Vector3 Point(NavPad pad) => pad.destination ? pad.destination.position : pad.transform.position;

        public static string Validate(GameObject root)
        {
            var guide = root.GetComponentInChildren<Guide>(true);
            if (!guide) return "Guide missing";
            Physics.SyncTransforms();
            var barriers = Barriers(root);
            var actors = Actors(root);
            int invalid = 0;
            var exits = new int[guide.nodes.Length];
            foreach (var edge in guide.edges)
            {
                if (edge.from < 0 || edge.from >= guide.nodes.Length || edge.to < 0 || edge.to >= guide.nodes.Length || !guide.nodes[edge.from] || !guide.nodes[edge.to]) { invalid++; continue; }
                var a = Point(guide.nodes[edge.from]);
                var b = Point(guide.nodes[edge.to]);
                var offset = b - a;
                if (new Vector2(offset.x, offset.z).magnitude > Mathf.Min(11f, guide.nodes[edge.to].maxDistance) + .01f || Mathf.Abs(offset.y) > Mathf.Min(2f, guide.nodes[edge.to].maxRise) + .01f || !Clear(a + Vector3.up * 1.65f, b + Vector3.up * .12f, barriers, actors, out _)) invalid++;
                exits[edge.from]++;
            }
            return guide.nodes.Length + " pads, " + guide.edges.Length + " links, " + invalid + " invalid links, " + exits.Count(x => x == 0) + " isolated pads";
        }

        public static string Routes(GameObject root)
        {
            var guide = root.GetComponentInChildren<Guide>(true);
            if (!guide || !guide.loop) return "Guide missing";
            var loop = guide.loop;
            var lines = new List<string>();
            var points = new[] { loop.home, loop.breakfast, guide.metro, guide.train ? guide.train.work : null, loop.factory, loop.concourse, loop.breakfast, loop.home };
            var names = new[] { "Home to Commons", "Commons to Metro", "", "Metro to Assembly", "Assembly to Arcade", "Arcade to Commons", "Commons to Home" };
            for (int route = 0; route < names.Length; route++)
            {
                if (names[route].Length == 0) continue;
                int from = Nearest(guide, points[route]);
                int to = Nearest(guide, points[route + 1]);
                var seen = new HashSet<int>();
                var queue = new Queue<int>();
                if (from >= 0) { queue.Enqueue(from); seen.Add(from); }
                while (queue.Count > 0 && !seen.Contains(to))
                {
                    int current = queue.Dequeue();
                    foreach (var edge in guide.edges)
                    {
                        if (edge.from != current || edge.to < 0 || edge.to >= guide.nodes.Length || seen.Contains(edge.to)) continue;
                        if (edge.boundary && edge.boundary.enabled && (!guide.homeDoor || edge.boundary != guide.homeDoor.barrier)) continue;
                        seen.Add(edge.to);
                        queue.Enqueue(edge.to);
                    }
                }
                lines.Add(names[route] + ": " + (to >= 0 && seen.Contains(to) ? "connected" : "unreachable") + " (" + from + " > " + to + ")");
            }
            return string.Join("\n", lines);
        }

        public static string Analyze(GameObject root)
        {
            var guide = root.GetComponentInChildren<Guide>(true);
            if (!guide || !guide.loop) return "Guide missing";
            Physics.SyncTransforms();
            int home = Nearest(guide, guide.loop.home);
            int commons = Nearest(guide, guide.loop.breakfast);
            var reachable = Reachable(guide, home, false);
            var returning = Reachable(guide, commons, true);
            var pairs = new List<Tuple<int, int, float>>();
            foreach (int from in reachable)
                foreach (int to in returning)
                {
                    if (reachable.Contains(to) || !guide.nodes[from] || !guide.nodes[to]) continue;
                    var delta = Point(guide.nodes[to]) - Point(guide.nodes[from]);
                    if (delta.magnitude < 18) pairs.Add(Tuple.Create(from, to, delta.magnitude));
                }
            pairs.Sort((a, b) => a.Item3.CompareTo(b.Item3));
            var lines = new List<string> { "Home reachable: " + reachable.Count + ", Commons reachable: " + reachable.Contains(commons), "Incoming Commons: " + returning.Count };
            foreach (var pair in pairs.Take(18))
            {
                var from = guide.nodes[pair.Item1];
                var to = guide.nodes[pair.Item2];
                Vector3 a = Point(from), b = Point(to), delta = b - a;
                string reason = "";
                float horizontal = new Vector2(delta.x, delta.z).magnitude;
                if (horizontal > Mathf.Min(11f, to.maxDistance)) reason += " range " + horizontal.ToString("F2");
                if (Mathf.Abs(delta.y) > Mathf.Min(2f, to.maxRise)) reason += " rise " + delta.y.ToString("F2");
                var ray = b + Vector3.up * .12f - (a + Vector3.up * 1.65f);
                var barriers = Barriers(root);
                foreach (var hit in Physics.RaycastAll(a + Vector3.up * 1.65f, ray.normalized, ray.magnitude - .015f, 1, QueryTriggerInteraction.Ignore).OrderBy(x => x.distance))
                {
                    string path = Path(hit.collider.transform, root.transform);
                    reason += " hit " + path + "@" + hit.point.ToString("F2") + (barriers.Contains(hit.collider) ? " [boundary]" : "");
                }
                if (reason.Length == 0) reason = " clear with pad limits";
                lines.Add(pair.Item1 + " " + Path(from.transform, root.transform) + " " + a.ToString("F2") + " > " + pair.Item2 + " " + Path(to.transform, root.transform) + " " + b.ToString("F2") + ":" + reason);
            }
            return string.Join("\n", lines);
        }

        static HashSet<int> Reachable(Guide guide, int start, bool reverse)
        {
            var result = new HashSet<int>();
            var queue = new Queue<int>();
            if (start >= 0) { result.Add(start); queue.Enqueue(start); }
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                foreach (var edge in guide.edges)
                {
                    int from = reverse ? edge.to : edge.from;
                    int to = reverse ? edge.from : edge.to;
                    if (from != current || to < 0 || to >= guide.nodes.Length || result.Contains(to)) continue;
                    if (edge.boundary && edge.boundary.enabled && (!guide.homeDoor || edge.boundary != guide.homeDoor.barrier)) continue;
                    result.Add(to);
                    queue.Enqueue(to);
                }
            }
            return result;
        }

        static string Path(Transform item, Transform root)
        {
            string path = item.name;
            while (item.parent && item.parent != root) { item = item.parent; path = item.name + "/" + path; }
            return path;
        }

        static int Nearest(Guide guide, Transform point)
        {
            if (!point) return -1;
            int best = -1;
            float distance = float.PositiveInfinity;
            for (int i = 0; i < guide.nodes.Length; i++)
            {
                if (!guide.nodes[i]) continue;
                var offset = Point(guide.nodes[i]) - point.position;
                if (Mathf.Abs(offset.y) > 1.8f || offset.sqrMagnitude >= distance) continue;
                distance = offset.sqrMagnitude;
                best = i;
            }
            return best;
        }

        static Mesh Mesh()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh) return mesh;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int group = 0; group < 3; group++)
            {
                float z = (group - 1) * .24f;
                int start = vertices.Count;
                vertices.AddRange(new[] { new Vector3(-.13f, 0, z - .07f), new Vector3(0, 0, z + .035f), new Vector3(.13f, 0, z - .07f), new Vector3(.13f, 0, z - .015f), new Vector3(0, 0, z + .09f), new Vector3(-.13f, 0, z - .015f) });
                foreach (int value in new[] { 0, 4, 1, 0, 5, 4, 1, 4, 2, 2, 4, 3 }) triangles.Add(start + value);
            }
            mesh = new Mesh { name = "Trail" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, MeshPath);
            return mesh;
        }

        static Material Material()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "Guide", enableInstancing = true };
            material.SetColor("_BaseColor", new Color(.25f, .9f, .74f));
            material.SetFloat("_Cull", 0);
            AssetDatabase.CreateAsset(material, MaterialPath);
            return material;
        }

        static Material Plate()
        {
            const string path = "Assets/Garden/Materials/GuidePlate.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = !material;
            if (create) material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "GuidePlate", renderQueue = 3000 };
            material.SetColor("_BaseColor", new Color(.025f, .05f, .055f, .65f));
            material.SetFloat("_Surface", 1);
            material.SetFloat("_Blend", 0);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0);
            material.SetFloat("_Cull", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            if (create) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }
    }
}
