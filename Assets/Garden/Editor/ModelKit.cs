using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Garden.Editor
{
    public static class ModelKit
    {
        public const string Root = "Assets/Garden/Art/Models/";
        public static readonly string[] Keys = { "AC", "Chair", "Pipe", "PipeBend", "PipeEnd", "Fence", "Sign", "Pillar", "Camera", "Barrier", "Landing", "Step", "Lamp", "Wall2", "Wall4", "Entry", "DoorWall" };
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, float> surfaces = new Dictionary<string, float>();

        public static void Build()
        {
            Directory.CreateDirectory(Root + "Meshes");
            Directory.CreateDirectory(Root + "Prefabs");
            Directory.CreateDirectory(Root + "Materials");
            AssetDatabase.Refresh();
            var stone = Material("Stone", new Color(.44f, .46f, .44f), .06f, .26f);
            var steel = Material("Steel", new Color(.22f, .26f, .27f), .42f, .36f);
            var trim = Material("Trim", new Color(.40f, .43f, .42f), .24f, .42f);
            var glass = Material("Glass", new Color(.075f, .16f, .19f), .18f, .72f);
            var seat = Material("Seat", new Color(.34f, .30f, .25f), .02f, .22f);
            var light = Material("Light", new Color(.75f, .78f, .66f), .05f, .40f);
            light.EnableKeyword("_EMISSION");
            light.SetColor("_EmissionColor", new Color(.42f, .48f, .30f));
            prefabs.Clear();
            surfaces.Clear();
            foreach (string key in Keys)
            {
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Root + key + ".fbx");
                if (!source) throw new InvalidOperationException("Missing model: " + key);
                var sample = Object.Instantiate(source);
                sample.transform.position = Vector3.zero;
                Mesh mesh;
                try { mesh = Bake(sample, key); }
                finally { Object.DestroyImmediate(sample); }
                string meshPath = Root + "Meshes/" + key + ".asset";
                var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if (existing)
                {
                    EditorUtility.CopySerialized(mesh, existing);
                    Object.DestroyImmediate(mesh);
                    mesh = existing;
                    EditorUtility.SetDirty(mesh);
                }
                else AssetDatabase.CreateAsset(mesh, meshPath);
                var prefab = new GameObject(ObjectNames.Short(key));
                prefab.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = prefab.AddComponent<MeshRenderer>();
                Material baseMaterial = Wall(key) ? stone : key == "Chair" ? seat : steel;
                renderer.sharedMaterials = new[] { baseMaterial, trim, key == "Lamp" ? light : glass };
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = true;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                GameObjectUtility.SetStaticEditorFlags(prefab, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                prefabs[key] = PrefabUtility.SaveAsPrefabAsset(prefab, Root + "Prefabs/" + key + ".prefab");
                Object.DestroyImmediate(prefab);
            }
            AssetDatabase.SaveAssets();
        }

        static Material Material(string name, Color color, float metallic, float smoothness)
        {
            string path = Root + "Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static bool Wall(string key) => key == "Wall2" || key == "Wall4" || key == "Entry" || key == "DoorWall";

        static Mesh Bake(GameObject source, string key)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            var turn = Matrix4x4.Rotate(Quaternion.Euler(0, 90, 0));
            foreach (var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var raw = filter.sharedMesh;
                if (!raw) continue;
                int first = vertices.Count;
                var matrix = turn * filter.transform.localToWorldMatrix;
                var normalMatrix = matrix.inverse.transpose;
                bool reverse = matrix.determinant < 0;
                var rawVertices = raw.vertices;
                var rawNormals = raw.normals;
                var rawUvs = raw.uv;
                for (int i = 0; i < rawVertices.Length; i++)
                {
                    vertices.Add(matrix.MultiplyPoint3x4(rawVertices[i]));
                    normals.Add(rawNormals.Length == rawVertices.Length ? normalMatrix.MultiplyVector(rawNormals[i]).normalized : Vector3.up);
                    uvs.Add(rawUvs.Length == rawVertices.Length ? rawUvs[i] : Vector2.zero);
                }
                for (int sub = 0; sub < raw.subMeshCount; sub++)
                {
                    var indices = raw.GetTriangles(sub);
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        triangles.Add(first + indices[i]);
                        triangles.Add(first + indices[i + (reverse ? 2 : 1)]);
                        triangles.Add(first + indices[i + (reverse ? 1 : 2)]);
                    }
                }
            }
            if (vertices.Count == 0) throw new InvalidOperationException("Empty model: " + key);
            var bounds = new Bounds(vertices[0], Vector3.zero);
            foreach (var vertex in vertices) bounds.Encapsulate(vertex);
            var origin = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            for (int i = 0; i < vertices.Count; i++) vertices[i] -= origin;
            var groups = new[] { new List<int>(), new List<int>(), new List<int>() };
            for (int i = 0; i < triangles.Count; i += 3)
            {
                var a = vertices[triangles[i]];
                var b = vertices[triangles[i + 1]];
                var c = vertices[triangles[i + 2]];
                var normal = Vector3.Cross(b - a, c - a).normalized;
                var center = (a + b + c) / 3f;
                int group = 0;
                if (Wall(key))
                {
                    float near = Mathf.Min(a.z, Mathf.Min(b.z, c.z));
                    if (near > .042f)
                    {
                        group = 1;
                        if (normal.z > .90f && center.z < .092f && center.y > (key == "Entry" || key == "DoorWall" ? 2.8f : .6f)) group = 2;
                    }
                }
                else if (key == "Camera" && normal.z > .7f && center.z > bounds.size.z * .35f) group = 2;
                else if (key == "Lamp" && normal.y < -.75f && center.y > bounds.size.y * .86f) group = 2;
                groups[group].Add(triangles[i]);
                groups[group].Add(triangles[i + 1]);
                groups[group].Add(triangles[i + 2]);
            }
            var mesh = new Mesh { name = key, indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) mesh.SetTriangles(groups[i], i);
            mesh.RecalculateBounds();
            return mesh;
        }

        static GameObject Prefab(string key)
        {
            if (prefabs.TryGetValue(key, out var prefab) && prefab) return prefab;
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Prefabs/" + key + ".prefab");
            if (!prefab) throw new InvalidOperationException("Build the model kit first: " + key);
            prefabs[key] = prefab;
            return prefab;
        }

        public static Vector3 Size(string key) => Prefab(key).GetComponent<MeshFilter>().sharedMesh.bounds.size;

        public static float Surface(string key)
        {
            if (surfaces.TryGetValue(key, out float result)) return result;
            var mesh = Prefab(key).GetComponent<MeshFilter>().sharedMesh;
            var vertices = mesh.vertices;
            var indices = mesh.triangles;
            var levels = new Dictionary<int, float>();
            for (int i = 0; i < indices.Length; i += 3)
            {
                var a = vertices[indices[i]];
                var b = vertices[indices[i + 1]];
                var c = vertices[indices[i + 2]];
                var normal = Vector3.Cross(b - a, c - a);
                if (normal.normalized.y < .95f) continue;
                int level = Mathf.RoundToInt((a.y + b.y + c.y) / 3f * 10000f);
                levels.TryGetValue(level, out float area);
                levels[level] = area + normal.magnitude;
            }
            result = levels.Count == 0 ? mesh.bounds.max.y : levels.OrderByDescending(p => p.Value).First().Key / 10000f;
            surfaces[key] = result;
            return result;
        }

        public static GameObject Place(string key, Transform parent, Vector3 worldBase, Quaternion worldRotation, Vector3 desiredSize)
        {
            var prefab = Prefab(key);
            var size = Size(key);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = ObjectNames.Short(key);
            instance.transform.SetPositionAndRotation(worldBase, worldRotation);
            instance.transform.localScale = new Vector3(desiredSize.x > 0 ? desiredSize.x / size.x : 1, desiredSize.y > 0 ? desiredSize.y / size.y : 1, desiredSize.z > 0 ? desiredSize.z / size.z : 1);
            instance.transform.SetParent(parent, true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            return instance;
        }
    }
}
