using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Garden.Editor
{
    public static class ModelWalls
    {
        static Transform group;
        static Material stone, trim;
        static Bounds[] blocks;
        public static int Count { get; private set; }

        public static void Apply(GameObject root)
        {
            var source = ObjectNames.Find(root.transform, "City/Blocks");
            if (!source) return;
            var renderers = source.GetComponentsInChildren<MeshRenderer>(true);
            blocks = renderers.Select(x => x.bounds).ToArray();
            group = new GameObject("Buildings").transform;
            group.SetParent(ObjectNames.Find(root.transform, "Models"), false);
            stone = Material("Stone", new Color(.43f, .44f, .42f));
            trim = Material("Trim", new Color(.23f, .27f, .28f));
            Count = 0;
            for (int i = 0; i < blocks.Length; i++)
            {
                var b = blocks[i];
                if (b.size.x < .8f || b.size.z < .8f) continue;
                var house = new GameObject(ObjectNames.Short(renderers[i].name)).transform;
                house.SetParent(group, false);
                renderers[i].enabled = false;
                Box("Core", house, b.center, new Vector3(b.size.x - .36f, b.size.y, b.size.z - .36f), stone);
                Box("Roof", house, new Vector3(b.center.x, b.max.y - .075f, b.center.z), new Vector3(b.size.x, .15f, b.size.z), trim);
                int floors = Mathf.Max(1, Mathf.RoundToInt(b.size.y / 3.3f));
                float height = b.size.y / floors;
                foreach (var normal in new[] { Vector3.forward, Vector3.back, Vector3.left, Vector3.right })
                    for (int floor = 0; floor < floors; floor++)
                        Face(house, i, normal, floor, height);
            }
            var art = ObjectNames.Find(root.transform, "Presentation/Art");
            if (art)
                foreach (var t in art.Cast<Transform>().Where(t => ObjectNames.Matches(t.name, "Facade")).ToArray())
                    Object.DestroyImmediate(t.gameObject);
        }

        static void Face(Transform parent, int index, Vector3 normal, int floor, float height)
        {
            var b = blocks[index];
            bool x = normal.z != 0;
            float low = x ? b.min.x : b.min.z;
            float high = x ? b.max.x : b.max.z;
            float plane = x ? (normal.z > 0 ? b.max.z : b.min.z) : (normal.x > 0 ? b.max.x : b.min.x);
            float bottom = b.min.y + floor * height;
            var spans = new List<Vector2> { new Vector2(low, high) };
            for (int j = 0; j < blocks.Length; j++)
            {
                if (j == index) continue;
                var other = blocks[j];
                if (other.min.y > bottom + .05f || other.max.y < bottom + height - .05f) continue;
                float a = x ? other.min.z : other.min.x;
                float z = x ? other.max.z : other.max.x;
                float probe = plane + (x ? normal.z : normal.x) * .025f;
                if (a > probe || z < probe) continue;
                float cutLow = x ? other.min.x : other.min.z;
                float cutHigh = x ? other.max.x : other.max.z;
                var next = new List<Vector2>();
                foreach (var span in spans)
                {
                    if (cutHigh <= span.x || cutLow >= span.y) { next.Add(span); continue; }
                    if (cutLow - span.x > .03f) next.Add(new Vector2(span.x, Mathf.Min(cutLow, span.y)));
                    if (span.y - cutHigh > .03f) next.Add(new Vector2(Mathf.Max(cutHigh, span.x), span.y));
                }
                spans = next;
            }
            foreach (var span in spans)
            {
                float width = span.y - span.x;
                int columns = Mathf.Max(1, Mathf.RoundToInt(width / 4f));
                float piece = width / columns;
                for (int column = 0; column < columns; column++)
                {
                    float along = span.x + (column + .5f) * piece;
                    var at = x ? new Vector3(along, bottom, plane) : new Vector3(plane, bottom, along);
                    var rotation = Quaternion.LookRotation(normal);
                    at -= normal * .115f;
                    if (piece < 1.65f)
                    {
                        var size = x ? new Vector3(piece, height, .25f) : new Vector3(.25f, height, piece);
                        Box("Pier", parent, at + Vector3.up * height * .5f, size, stone);
                        continue;
                    }
                    string model = (index + column) % 3 == 0 ? "Wall4" : "Wall2";
                    if (floor == 0 && column == columns / 2 && width > 3.2f)
                        model = index % 2 == 0 ? "Entry" : "DoorWall";
                    ModelKit.Place(model, parent, at, rotation, new Vector3(piece, height, .25f));
                    Count++;
                }
            }
        }

        static Material Material(string name, Color color)
        {
            const string folder = "Assets/Garden/Materials/Models/";
            Directory.CreateDirectory(folder);
            string path = folder + name + ".mat";
            var value = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!value)
            {
                value = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(value, path);
            }
            value.SetColor("_BaseColor", color);
            value.SetFloat("_Smoothness", .14f);
            value.enableInstancing = true;
            EditorUtility.SetDirty(value);
            return value;
        }

        static void Box(string name, Transform parent, Vector3 at, Vector3 size, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = ObjectNames.Short(name);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            go.transform.localScale = size;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
        }
    }
}
