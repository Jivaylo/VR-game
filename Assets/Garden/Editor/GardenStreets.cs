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
    public static class GardenStreets
    {
        const string MaterialPath = "Assets/Garden/Materials/Streets/";
        const string MeshPath = "Assets/Garden/Meshes/Streets/";
        static readonly List<Place> places = new List<Place>();
        static readonly Dictionary<string, Batch> batches = new Dictionary<string, Batch>();
        static readonly List<Vector3> signPoints = new List<Vector3>();
        static Transform group;
        static TMP_FontAsset font;
        static Material stone, trim, ink, paper, window;
        static NavPad[] pads;
        static Vector3[] points;
        static List<int>[] graph;
        static bool[] valid;
        public static string Report { get; private set; }

        sealed class Place
        {
            public string name;
            public string code;
            public Transform root;
            public Vector3 point;
            public Material color;
            public int pad;
            public float[] distances;
            public int[] next;
        }

        sealed class Batch
        {
            public Material material;
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<int> triangles = new List<int>();
        }

        public static void Apply(GameObject root)
        {
            if (!root) throw new ArgumentNullException(nameof(root));
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop play mode before editing streets.");
            var old = ObjectNames.Find(root.transform, "Streets");
            if (old) Object.DestroyImmediate(old.gameObject);
            Directory.CreateDirectory(MaterialPath);
            Directory.CreateDirectory(MeshPath);
            group = New("Streets", root.transform, Vector3.zero, Quaternion.identity);
            places.Clear();
            batches.Clear();
            signPoints.Clear();
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            stone = Material("Stone", new Color(.46f, .47f, .45f));
            trim = Material("Trim", new Color(.26f, .29f, .29f));
            ink = Material("Ink", new Color(.075f, .11f, .12f), true);
            paper = Material("Paper", new Color(.84f, .86f, .81f), true);
            window = Material("Window", new Color(.25f, .32f, .34f));
            var sand = Material("Sand", new Color(.60f, .53f, .40f));
            var sage = Material("Sage", new Color(.35f, .48f, .40f));
            var blue = Material("Blue", new Color(.26f, .43f, .53f));
            var rust = Material("Rust", new Color(.54f, .34f, .26f));
            var chalk = Material("Chalk", new Color(.60f, .65f, .61f));
            var amber = Material("Amber", new Color(.62f, .48f, .26f));
            AddPlace(root, "Home", "Residence", "01", sand, "Landing");
            AddPlace(root, "Breakfast", "Commons", "02", sage, "Counter Pad");
            AddPlace(root, "Factory", "Assembly", "03", rust, "Station");
            AddPlace(root, "Concourse", "Concourse", "04", chalk, "Hatch Pad");
            AddPlace(root, "Yard", "Service", "05", amber, "P0");
            var train = root.GetComponentInChildren<Train>(true);
            if (train)
            {
                AddStation(train.south, "Metro", "M1", blue);
                AddStation(train.work, "Metro", "M2", blue);
            }
            Physics.SyncTransforms();
            BuildGraph(root);
            if (!ObjectNames.Find(root.transform, "Models")) Architecture(root);
            Landmarks();
            Routes();
            Flush();
            Report = places.Count + " places, " + signPoints.Count + " direction signs, " + batches.Count + " static mesh batches. Navigation positions and colliders are unchanged.";
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();
        }

        static void AddPlace(GameObject root, string name, string title, string code, Material color, string padName)
        {
            var module = ObjectNames.Find(root.transform, name);
            if (!module) return;
            var marker = module.GetComponentsInChildren<NavPad>(true).FirstOrDefault(p => ObjectNames.Matches(p.name, padName));
            places.Add(new Place { name = title, code = code, root = module, point = marker ? marker.transform.position : module.position, color = color });
        }

        static void AddStation(Transform station, string name, string code, Material color)
        {
            if (station) places.Add(new Place { name = name, code = code, root = station, point = station.position, color = color });
        }

        static Material Material(string name, Color color, bool unlit = false)
        {
            string path = MaterialPath + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.name = name;
            material.enableInstancing = true;
            material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .12f);
            EditorUtility.SetDirty(material);
            return material;
        }

        static Transform New(string name, Transform parent, Vector3 position, Quaternion rotation)
        {
            var result = new GameObject(ObjectNames.Short(name)).transform;
            result.SetParent(parent, false);
            result.SetPositionAndRotation(position, rotation);
            return result;
        }

        static void BuildGraph(GameObject root)
        {
            pads = root.GetComponentsInChildren<NavPad>(true).Where(p => p.isActiveAndEnabled && p.unlocked && Mathf.Abs(p.transform.position.x) < 90).ToArray();
            points = pads.Select(p => p.destination ? p.destination.position : p.transform.position).ToArray();
            graph = new List<int>[pads.Length];
            valid = new bool[pads.Length];
            var opened = new List<Collider>();
            try
            {
                var home = ObjectNames.Find(root.transform, "Home");
                if (home)
                    foreach (var door in home.GetComponentsInChildren<Door>(true))
                        if (!door.localOnly && door.barrier && door.barrier.enabled) { opened.Add(door.barrier); door.barrier.enabled = false; }
                Physics.SyncTransforms();
                for (int i = 0; i < pads.Length; i++)
                {
                    graph[i] = new List<int>();
                    var point = points[i];
                    valid[i] = !Physics.CheckCapsule(point + Vector3.up * .27f, point + Vector3.up * 1.48f, .22f, 1, QueryTriggerInteraction.Ignore)
                        && Physics.Raycast(point + Vector3.up * .18f, Vector3.down, .38f, 1, QueryTriggerInteraction.Ignore);
                }
                for (int i = 0; i < pads.Length; i++)
                    for (int j = i + 1; j < pads.Length; j++)
                    {
                        if (!valid[i] || !valid[j]) continue;
                        var delta = points[j] - points[i];
                        if (Mathf.Abs(delta.y) > Mathf.Min(pads[i].maxRise, pads[j].maxRise)) continue;
                        float reach = Mathf.Min(pads[i].maxDistance, pads[j].maxDistance);
                        if (new Vector2(delta.x, delta.z).sqrMagnitude > reach * reach) continue;
                        if (!Sight(points[i] + Vector3.up * 1.25f, points[j] + Vector3.up * .12f) || !Sight(points[j] + Vector3.up * 1.25f, points[i] + Vector3.up * .12f)) continue;
                        graph[i].Add(j);
                        graph[j].Add(i);
                    }
            }
            finally
            {
                foreach (var collider in opened) if (collider) collider.enabled = true;
                Physics.SyncTransforms();
            }
            foreach (var place in places)
            {
                place.pad = Enumerable.Range(0, points.Length).Where(i => valid[i]).OrderBy(i => (points[i] - place.point).sqrMagnitude).DefaultIfEmpty(-1).First();
                place.distances = Enumerable.Repeat(float.PositiveInfinity, points.Length).ToArray();
                place.next = Enumerable.Repeat(-1, points.Length).ToArray();
                if (place.pad < 0) continue;
                var pending = new HashSet<int>(Enumerable.Range(0, points.Length).Where(i => valid[i]));
                place.distances[place.pad] = 0;
                while (pending.Count > 0)
                {
                    int current = pending.OrderBy(i => place.distances[i]).First();
                    if (float.IsInfinity(place.distances[current])) break;
                    pending.Remove(current);
                    foreach (int neighbour in graph[current])
                    {
                        float distance = place.distances[current] + Vector3.Distance(points[current], points[neighbour]);
                        if (distance >= place.distances[neighbour]) continue;
                        place.distances[neighbour] = distance;
                        place.next[neighbour] = current;
                    }
                }
            }
        }

        static bool Sight(Vector3 from, Vector3 to)
        {
            return !Physics.Linecast(from, to, 1, QueryTriggerInteraction.Ignore);
        }

        static void Architecture(GameObject root)
        {
            var blocks = ObjectNames.Find(root.transform, "City/Blocks");
            if (!blocks) return;
            foreach (var renderer in blocks.GetComponentsInChildren<MeshRenderer>(true))
            {
                var bounds = renderer.bounds;
                if (bounds.size.x < 1.2f || bounds.size.z < 1.2f) continue;
                var nearby = points.Where((p, i) => valid[i] && bounds.SqrDistance(p + Vector3.up * 1.5f) < 30).ToArray();
                if (nearby.Length == 0) continue;
                var near = places.Where(p => p.code != "05").OrderBy(p => Horizontal(p.point, bounds.center)).FirstOrDefault();
                var accent = near != null && Horizontal(near.point, bounds.center) < 18 ? near.color : stone;
                Box(bounds.center + Vector3.up * (bounds.extents.y - .09f), new Vector3(bounds.size.x + .035f, .18f, bounds.size.z + .035f), Quaternion.identity, trim);
                Box(new Vector3(bounds.center.x, bounds.min.y + .24f, bounds.center.z), new Vector3(bounds.size.x + .025f, .48f, bounds.size.z + .025f), Quaternion.identity, stone);
                foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                {
                    var face = bounds.center + Vector3.Scale(bounds.extents, direction);
                    if (!nearby.Any(p => Vector3.Dot(p - face, direction) > .5f && Horizontal(p, face) < 14)) continue;
                    bool acrossX = direction.x == 0;
                    float width = acrossX ? bounds.size.x : bounds.size.z;
                    if (width < 2.2f) continue;
                    var facing = Quaternion.LookRotation(-direction);
                    var bandPoint = face + direction * .024f;
                    bandPoint.y = bounds.min.y + 3.0f;
                    Box(bandPoint, new Vector3(width - .18f, .14f, .028f), facing, accent);
                    int columns = Mathf.Min(5, Mathf.FloorToInt((width - .7f) / 2.4f));
                    int rows = bounds.size.y > 8 ? 2 : 1;
                    for (int row = 0; row < rows; row++)
                        for (int column = 0; column < columns; column++)
                        {
                            float x = (column - (columns - 1) * .5f) * Mathf.Min(3.0f, (width - 1.4f) / Mathf.Max(1, columns));
                            var center = face + facing * new Vector3(x, 0, -.026f);
                            center.y = bounds.min.y + 4.5f + row * 2.3f;
                            if (center.y + .65f > bounds.max.y - .35f) continue;
                            Box(center, new Vector3(.88f, 1.1f, .027f), facing, window);
                            Box(center + Vector3.down * .59f, new Vector3(1.02f, .07f, .04f), facing, stone);
                            Box(center, new Vector3(.04f, 1.12f, .038f), facing, trim);
                        }
                }
            }
        }

        static void Landmarks()
        {
            foreach (var place in places)
            {
                var module = place.root;
                if (place.code == "01")
                {
                    Header(place, module.TransformPoint(new Vector3(0, 2.73f, -2.64f)), module.rotation, 2.65f, "RESIDENCE 01");
                    Box(module.TransformPoint(new Vector3(0, 3.28f, -2.65f)), new Vector3(3.05f, .09f, .65f), module.rotation, place.color);
                }
                else if (place.code == "02")
                {
                    Header(place, module.TransformPoint(new Vector3(0, 2.75f, 1.48f)), module.rotation, 2.8f, "COMMONS 02");
                    Box(module.TransformPoint(new Vector3(0, 3.06f, 1.30f)), new Vector3(3.10f, .13f, 1.2f), module.rotation, place.color);
                    foreach (float side in new[] { -1.43f, 1.43f }) Box(module.TransformPoint(new Vector3(side, 2.15f, 1.53f)), new Vector3(.055f, 1.70f, .055f), module.rotation, trim);
                }
                else if (place.code == "03")
                {
                    Header(place, module.TransformPoint(new Vector3(0, 3.55f, 2.81f)), module.rotation, 4.2f, "ASSEMBLY 03");
                    Box(module.TransformPoint(new Vector3(0, 3.9f, 2.86f)), new Vector3(4.6f, .18f, .35f), module.rotation, place.color);
                    foreach (float side in new[] { -2.18f, 2.18f }) Box(module.TransformPoint(new Vector3(side, 1.9f, 2.98f)), new Vector3(.11f, 3.8f, .12f), module.rotation, trim);
                }
                else if (place.code == "04")
                {
                    var facing = module.rotation * Quaternion.Euler(0, 90, 0);
                    Header(place, module.TransformPoint(new Vector3(.34f, 3.45f, 0)), facing, 3.0f, "CONCOURSE 04");
                    Box(module.TransformPoint(new Vector3(.30f, 3.87f, 0)), new Vector3(3.45f, .1f, .35f), facing, place.color);
                }
                else if (place.code == "05")
                {
                    var wall = module.GetComponentsInChildren<Renderer>(true).Where(r => ObjectNames.Matches(r.name, "Wall")).OrderBy(r => Vector3.Distance(r.bounds.center, place.point)).FirstOrDefault();
                    if (wall)
                    {
                        Vector3 point = wall.bounds.ClosestPoint(place.point + Vector3.up * 2.7f);
                        var normal = place.point - point;
                        normal.y = 0;
                        if (normal.sqrMagnitude > .1f) Header(place, point + normal.normalized * .065f, Quaternion.LookRotation(-normal.normalized), 2.6f, "SERVICE 05");
                    }
                }
                else
                {
                    var at = place.point + new Vector3(1.6f, 2.6f, place.code == "M1" ? 1.3f : .6f);
                    Header(place, at, Quaternion.identity, 1.55f, "M  " + place.code.Substring(1));
                }
                GroundMark(place);
            }
        }

        static void Header(Place place, Vector3 position, Quaternion rotation, float width, string title)
        {
            var sign = New(place.name, group, position, rotation);
            Visual("Face", sign, new Vector3(0, 0, .035f), new Vector3(width, .48f, .06f), ink);
            Visual("Band", sign, new Vector3(0, .24f, -.008f), new Vector3(width, .06f, .024f), place.color);
            Label(title, sign, new Vector3(0, -.015f, -.012f), new Vector2(width - .18f, .36f), 1.8f, TextAlignmentOptions.Center);
        }

        static void GroundMark(Place place)
        {
            Vector3 at = place.point;
            for (int i = -1; i <= 1; i++)
            {
                Vector3 start = at + new Vector3(i * .20f, .042f, -.6f);
                if (Physics.Raycast(start + Vector3.up * .14f, Vector3.down, out var hit, .3f, 1, QueryTriggerInteraction.Ignore) && hit.normal.y > .8f)
                    Box(hit.point + Vector3.up * .008f, new Vector3(.12f, .01f, .22f), Quaternion.identity, place.color);
            }
        }

        static void Routes()
        {
            var publicPlaces = places.Where(p => p.code != "05" && p.pad >= 0).ToArray();
            if (publicPlaces.Length < 2) return;
            var candidates = new HashSet<int>();
            foreach (var source in publicPlaces)
                foreach (var target in publicPlaces)
                {
                    if (source == target) continue;
                    int current = source.pad;
                    int last = -1;
                    for (int step = 0; step < points.Length && current >= 0 && current != target.pad; step++)
                    {
                        int next = target.next[current];
                        if (next < 0) break;
                        if (last >= 0 && Vector3.Angle(Flat(points[current] - points[last]), Flat(points[next] - points[current])) > 28) candidates.Add(current);
                        if (last < 0) candidates.Add(next);
                        last = current;
                        current = next;
                    }
                }
            var placed = new List<Vector3>();
            int total = 0;
            foreach (int id in candidates.OrderBy(i => publicPlaces.Min(p => p.distances[i])))
            {
                if (total >= 16) break;
                if (placed.Any(p => Horizontal(p, points[id]) < 11 && Mathf.Abs(p.y - points[id].y) < 2)) continue;
                if (publicPlaces.Any(p => Horizontal(p.point, points[id]) < 3 && Mathf.Abs(p.point.y - points[id].y) < 2)) continue;
                var choices = publicPlaces.Where(p => p.next[id] >= 0 && p.distances[id] > 4 && p.distances[id] < 120)
                    .OrderBy(p => p.distances[id]).GroupBy(p => p.name).Select(p => p.First()).ToArray();
                if (choices.Length < 2) continue;
                var first = choices[0];
                var second = choices.Skip(1).FirstOrDefault(p => Vector3.Angle(Flat(points[p.next[id]] - points[id]), Flat(points[first.next[id]] - points[id])) > 50) ?? choices[1];
                if (!PlaceDirection(id, first, second, out var at)) continue;
                placed.Add(points[id]);
                signPoints.Add(at);
                total++;
            }
        }

        static bool PlaceDirection(int id, Place first, Place second, out Vector3 placed)
        {
            placed = Vector3.zero;
            Vector3 pad = points[id];
            float best = float.PositiveInfinity;
            Quaternion rotation = Quaternion.identity;
            foreach (var direction in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
            {
                foreach (float distance in new[] { 1.05f, 1.4f, 1.8f })
                {
                    Vector3 candidate = pad + direction * distance + Vector3.up * 1.7f;
                    var facing = Quaternion.LookRotation(direction);
                    if (Physics.CheckBox(candidate, new Vector3(.62f, .58f, .07f), facing, 1, QueryTriggerInteraction.Ignore)) continue;
                    if (!Sight(pad + Vector3.up * 1.5f, candidate)) continue;
                    if (!Physics.Raycast(candidate, Vector3.down, out var floor, 2.1f, 1, QueryTriggerInteraction.Ignore) || floor.normal.y < .8f || Mathf.Abs(floor.point.y - pad.y) > .2f) continue;
                    if (points.Any(p => Horizontal(p, candidate) < .72f && Mathf.Abs(p.y - pad.y) < .2f)) continue;
                    float score = distance;
                    foreach (int next in graph[id]) score += Mathf.Max(0, Vector3.Dot(Flat(points[next] - pad).normalized, direction)) * 1.2f;
                    if (score >= best) continue;
                    best = score;
                    placed = candidate;
                    rotation = facing;
                }
            }
            if (float.IsInfinity(best)) return false;
            var sign = New("Directions", group, placed, rotation);
            Visual("Post", sign, new Vector3(0, -.75f, .05f), new Vector3(.065f, 1.5f, .065f), trim);
            Visual("Face", sign, new Vector3(0, 0, .035f), new Vector3(1.24f, .96f, .06f), ink);
            DirectionFace(sign, first, second, id);
            var reverse = New("Back", sign, sign.TransformPoint(new Vector3(0, 0, .075f)), sign.rotation * Quaternion.Euler(0, 180, 0));
            DirectionFace(reverse, first, second, id);
            return true;
        }

        static void DirectionFace(Transform sign, Place first, Place second, int id)
        {
            Label("DISTRICT 07", sign, new Vector3(0, .31f, -.012f), new Vector2(1.08f, .18f), .85f, TextAlignmentOptions.Center);
            DirectionRow(sign, first, id, .045f);
            DirectionRow(sign, second, id, -.25f);
        }

        static void DirectionRow(Transform sign, Place place, int id, float height)
        {
            Visual("Band", sign, new Vector3(-.565f, height, -.012f), new Vector3(.035f, .22f, .018f), place.color);
            Label(place.name, sign, new Vector3(-.065f, height, -.018f), new Vector2(.83f, .23f), 1.1f, TextAlignmentOptions.MidlineLeft);
            Vector3 direction = points[place.next[id]] - points[id];
            var local = Quaternion.Inverse(sign.rotation) * Flat(direction).normalized;
            Vector2 arrow = Mathf.Abs(direction.y) > 1 ? new Vector2(0, Mathf.Sign(direction.y)) : new Vector2(local.x, local.z);
            if (arrow.sqrMagnitude < .01f) arrow = Vector2.up;
            arrow.Normalize();
            float angle = -Mathf.Atan2(arrow.x, arrow.y) * Mathf.Rad2Deg;
            var pivot = New("Arrow", sign, sign.TransformPoint(new Vector3(.45f, height, -.028f)), sign.rotation * Quaternion.Euler(0, 0, angle));
            Visual("Stem", pivot, new Vector3(0, -.018f, 0), new Vector3(.024f, .12f, .012f), paper);
            var left = Visual("Tip", pivot, new Vector3(-.035f, .038f, 0), new Vector3(.026f, .085f, .012f), paper);
            left.localRotation = Quaternion.Euler(0, 0, -45);
            var right = Visual("Tip", pivot, new Vector3(.035f, .038f, 0), new Vector3(.026f, .085f, .012f), paper);
            right.localRotation = Quaternion.Euler(0, 0, 45);
        }

        static Vector3 Flat(Vector3 value) { value.y = 0; return value; }
        static float Horizontal(Vector3 a, Vector3 b) { return Flat(a - b).magnitude; }

        static Transform Visual(string name, Transform parent, Vector3 local, Vector3 scale, Material material)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = ObjectNames.Short(name);
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = local;
            cube.transform.localScale = scale;
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            var renderer = cube.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(cube, StaticEditorFlags.BatchingStatic);
            return cube.transform;
        }

        static TMP_Text Label(string text, Transform parent, Vector3 local, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = local;
            var label = go.AddComponent<TextMeshPro>();
            label.font = font;
            label.text = text;
            label.fontSize = fontSize;
            label.fontStyle = FontStyles.Normal;
            label.color = new Color(.86f, .89f, .85f);
            label.alignment = alignment;
            label.rectTransform.sizeDelta = size;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.richText = false;
            label.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            return label;
        }

        static void Box(Vector3 position, Vector3 scale, Quaternion rotation, Material material)
        {
            string key = Mathf.FloorToInt(position.x / 20) + " " + Mathf.FloorToInt(position.z / 20) + " " + material.name;
            if (!batches.TryGetValue(key, out var batch)) batches.Add(key, batch = new Batch { material = material });
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++) corners[i] = position + rotation * Vector3.Scale(new Vector3((i & 1) == 0 ? -.5f : .5f, (i & 2) == 0 ? -.5f : .5f, (i & 4) == 0 ? -.5f : .5f), scale);
            int[] faces = { 0, 2, 3, 1, 4, 5, 7, 6, 0, 4, 6, 2, 1, 3, 7, 5, 0, 1, 5, 4, 2, 6, 7, 3 };
            for (int face = 0; face < 6; face++)
            {
                int start = batch.vertices.Count;
                for (int i = 0; i < 4; i++) batch.vertices.Add(corners[faces[face * 4 + i]]);
                batch.triangles.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
            }
        }

        static void Flush()
        {
            int count = 0;
            foreach (var pair in batches.OrderBy(p => p.Key))
            {
                string name = "Street " + (++count).ToString("00");
                var mesh = new Mesh { name = name };
                mesh.SetVertices(pair.Value.vertices);
                mesh.SetTriangles(pair.Value.triangles, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                string path = MeshPath + name + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (existing) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
                else AssetDatabase.CreateAsset(mesh, path);
                var obj = New(name, group, Vector3.zero, Quaternion.identity).gameObject;
                obj.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = obj.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = pair.Value.material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                GameObjectUtility.SetStaticEditorFlags(obj, StaticEditorFlags.BatchingStatic);
            }
        }
    }
}
