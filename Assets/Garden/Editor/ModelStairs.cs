using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Garden.Editor
{
    public static class ModelStairs
    {
        public static string Report { get; private set; }

        public static void Apply(GameObject root)
        {
            var models = ObjectNames.Find(root.transform, "Models");
            if (!models)
            {
                models = new GameObject("Models").transform;
                models.SetParent(root.transform, false);
            }
            var old = ObjectNames.Find(models, "Stairs");
            if (old) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject("Stairs").transform;
            group.SetParent(models, false);
            var stairs = ObjectNames.Find(root.transform, "City/Stairs");
            var city = ObjectNames.Find(root.transform, "City");
            var decks = city ? city.GetComponentsInChildren<BoxCollider>(true).Where(c => !c.isTrigger && !c.GetComponentInParent<NavPad>() && (ObjectNames.Matches(c.name, "Landing") || ObjectNames.Matches(c.name, "Home") || ObjectNames.StartsWith(c.name, "Walk")) && Vector3.Dot(c.transform.up, Vector3.up) > .99f).ToArray() : new BoxCollider[0];
            int flights = 0, treads = 0, landings = 0, fences = 0, supports = 0;
            if (stairs)
            {
                foreach (var filter in stairs.GetComponentsInChildren<MeshFilter>(true).Where(m => ObjectNames.Matches(m.name, "Steps")))
                {
                    var owner = new GameObject(ObjectNames.Short(filter.transform.parent.name)).transform;
                    owner.SetParent(group, false);
                    var transform = filter.transform;
                    var scale = transform.lossyScale;
                    var mesh = filter.sharedMesh;
                    if (!mesh) continue;
                    float height = mesh.bounds.max.y;
                    float width = mesh.bounds.size.x;
                    float run = mesh.bounds.size.z;
                    if (Mathf.Abs(height - 3.2f) > .1f || Mathf.Abs(run - 8) > .1f) continue;
                    var shape = ModelKit.Size("Step");
                    float surface = ModelKit.Surface("Step");
                    for (int i = 0; i < 18; i++)
                    {
                        float h = height * (18 - i) / 18f;
                        float z = (i + .5f) / 3f + (i >= 9 ? 2 : 0);
                        if (Covered(transform.TransformPoint(new Vector3(0, h, z)), decks)) continue;
                        var point = transform.TransformPoint(new Vector3(0, h - surface + .001f, z));
                        ModelKit.Place("Step", owner, point, transform.rotation, new Vector3(width * scale.x, shape.y * scale.y, scale.z / 3f));
                        treads++;
                    }
                    var flat = ModelKit.Size("Landing");
                    for (int i = 0; i < 4; i++)
                    {
                        var point = transform.TransformPoint(new Vector3(0, height * .5f - flat.y + .001f, 3.25f + i * .5f));
                        ModelKit.Place("Landing", owner, point, transform.rotation, new Vector3(width * scale.x, flat.y * scale.y, .5f * scale.z));
                        landings++;
                    }
                    float slope = height / 6f;
                    float beamTop = .05f / Mathf.Sqrt(1f + slope * slope);
                    float inset = surface - height / 36f + beamTop;
                    float footY = beamTop + .001f;
                    float footZ = 8f - (inset + footY) / slope;
                    foreach (float x in new[] { -width * .47f, width * .47f })
                    {
                        Brace(owner, transform.TransformPoint(new Vector3(x, height * .5f - inset, 3)), transform.TransformPoint(new Vector3(x, height - inset, 0)), .10f);
                        Brace(owner, transform.TransformPoint(new Vector3(x, footY, footZ)), transform.TransformPoint(new Vector3(x, height * .5f - inset, 5)), .10f);
                        float deckBeam = height * .5f - flat.y - .049f;
                        Brace(owner, transform.TransformPoint(new Vector3(x, deckBeam, 3)), transform.TransformPoint(new Vector3(x, deckBeam, 5)), .10f);
                    }
                    var renderer = filter.GetComponent<Renderer>();
                    if (renderer) renderer.enabled = false;
                    flights++;
                }
            }
            var walkways = ObjectNames.Find(root.transform, "City/Walkways");
            var supportPoints = new List<Vector3>();
            var pads = root.GetComponentsInChildren<NavPad>(true).Select(p => p.transform.position).ToArray();
            if (walkways)
            {
                foreach (var rail in walkways.GetComponentsInChildren<Transform>(true).Where(t => ObjectNames.Matches(t.name, "Rail") && t.GetComponent<BoxCollider>()))
                {
                    var a = rail.TransformPoint(new Vector3(0, 0, -.5f));
                    var b = rail.TransformPoint(new Vector3(0, 0, .5f));
                    if (Mathf.Abs(a.y - b.y) > .05f) continue;
                    var delta = b - a;
                    float length = delta.magnitude;
                    if (length < .15f) continue;
                    var facing = Quaternion.LookRotation(Vector3.Cross(delta.normalized, Vector3.up));
                    int count = Mathf.Max(1, Mathf.RoundToInt(length / 1.08f));
                    var size = ModelKit.Size("Fence");
                    for (int i = 0; i < count; i++)
                    {
                        var at = Vector3.Lerp(a, b, (i + .5f) / count) - Vector3.up * 1.05f;
                        ModelKit.Place("Fence", group, at, facing, new Vector3(length / count, 1.05f, size.z));
                        fences++;
                    }
                    var renderer = rail.GetComponent<Renderer>();
                    if (renderer) renderer.enabled = false;
                }
                foreach (var post in walkways.GetComponentsInChildren<Transform>(true).Where(t => ObjectNames.Matches(t.name, "Post") && t.GetComponent<BoxCollider>()))
                {
                    var renderer = post.GetComponent<Renderer>();
                    if (renderer) renderer.enabled = false;
                    var top = post.TransformPoint(new Vector3(0, -.5f, 0));
                    if (supportPoints.Any(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(top.x, top.z)) < 5)) continue;
                    if (pads.Any(p => Vector2.Distance(new Vector2(p.x, p.z), new Vector2(top.x, top.z)) < .60f)) continue;
                    top.y -= .24f;
                    if (!Physics.Raycast(top - Vector3.up * .03f, Vector3.down, out var floor, 4f, 1, QueryTriggerInteraction.Ignore) || floor.normal.y < .9f) continue;
                    float height = top.y - floor.point.y;
                    if (height < .4f || height > 3.3f) continue;
                    if (Physics.CheckCapsule(floor.point + Vector3.up * .22f, top - Vector3.up * .20f, .12f, 1, QueryTriggerInteraction.Ignore)) continue;
                    ModelKit.Place("Pillar", group, floor.point, Quaternion.identity, new Vector3(.18f, height, .18f));
                    supportPoints.Add(top);
                    supports++;
                }
            }
            Report = flights + " stairs, " + treads + " treads, " + landings + " landing panels, " + fences + " fence panels, " + supports + " deck supports.";
            EditorUtility.SetDirty(root);
        }

        static void Brace(Transform parent, Vector3 a, Vector3 b, float width)
        {
            var delta = b - a;
            var instance = ModelKit.Place("Pillar", parent, a, Quaternion.FromToRotation(Vector3.up, delta.normalized), new Vector3(width, delta.magnitude, width));
            instance.name = "Stringer";
        }

        static bool Covered(Vector3 surface, BoxCollider[] decks)
        {
            foreach (var deck in decks)
            {
                var local = deck.transform.InverseTransformPoint(surface) - deck.center;
                var half = deck.size * .5f;
                if (Mathf.Abs(local.x) > half.x || Mathf.Abs(local.z) > half.z) continue;
                float top = deck.transform.TransformPoint(deck.center + Vector3.up * half.y).y;
                if (top >= surface.y - .006f && top <= surface.y + .25f) return true;
            }
            return false;
        }
    }
}
