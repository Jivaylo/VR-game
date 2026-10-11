using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Garden.Editor
{
    public static class WristStyle
    {
        const string Path = "Assets/Garden/Art/Watch";

        public static void Apply(GameObject root)
        {
            if (!root || EditorApplication.isPlaying) throw new InvalidOperationException("Open Garden in edit mode.");
            var talk = root.GetComponentInChildren<Talk>(true);
            if (!talk || !talk.board) throw new InvalidOperationException("Garden receiver is missing.");
            if (!AssetDatabase.IsValidFolder(Path)) AssetDatabase.CreateFolder("Assets/Garden/Art", "Watch");
            var wrist = talk.GetComponent<Wrist>();
            if (!wrist) wrist = talk.gameObject.AddComponent<Wrist>();
            var old = ObjectNames.Find(talk.transform, "Watch");
            if (old) Object.DestroyImmediate(old.gameObject);
            var housing = New("Watch", talk.transform);
            var rubber = Material("Band", new Color(.045f, .052f, .058f), false, 0, .24f);
            var metal = Material("Case", new Color(.32f, .35f, .37f), false, .75f, .48f);
            var lens = Material("Lens", new Color(.026f, .055f, .065f), false, .18f, .82f);
            var ink = Material("Panel", new Color(.023f, .029f, .035f), true, 0, 0);
            var band = new Shape();
            band.Ring(.034f, .029f, .005f, .034f);
            band.Round(new Vector3(.062f, .046f, .006f), .011f, .001f, new Vector3(0, .026f, 0), Quaternion.Euler(90, 0, 0));
            Draw("Band", housing, band, rubber);
            var frame = new Shape();
            frame.Round(new Vector3(.078f, .064f, .016f), .014f, .002f, new Vector3(0, .035f, 0), Quaternion.Euler(90, 0, 0));
            foreach (float side in new[] { -1f, 1f })
                frame.Round(new Vector3(.050f, .010f, .007f), .003f, .001f, new Vector3(0, .029f, side * .033f), Quaternion.Euler(90, 0, 0));
            Draw("Case", housing, frame, metal);
            var glass = new Shape();
            glass.Round(new Vector3(.064f, .047f, .002f), .009f, .0005f, new Vector3(0, .0435f, 0), Quaternion.Euler(90, 0, 0));
            Draw("Lens", housing, glass, lens);
            var board = talk.board;
            var font = talk.caption ? talk.caption.font : null;
            if (!font) font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            string line = talk.caption ? talk.caption.text : "";
            string speaker = talk.speakerLabel ? talk.speakerLabel.text : "";
            for (int i = board.childCount - 1; i >= 0; i--) Object.DestroyImmediate(board.GetChild(i).gameObject);
            board.localScale = Vector3.one;
            var back = new Shape();
            back.Round(new Vector3(1, .42f, .016f), .035f, .004f, new Vector3(0, 0, .018f), Quaternion.identity);
            Draw("Back", board, back, ink);
            talk.speakerLabel = Text("Speaker", board, font, speaker, .37f, new Vector3(0, .151f, 0), new Vector2(.91f, .055f));
            talk.speakerLabel.fontStyle = FontStyles.Bold;
            talk.speakerLabel.characterSpacing = 2;
            talk.caption = Text("Text", board, font, line, .60f, new Vector3(0, -.029f, 0), new Vector2(.91f, .285f));
            var panel = board.GetComponent<Panel>();
            if (!panel) panel = board.gameObject.AddComponent<Panel>();
            panel.size = new Vector2(1, .42f);
            panel.center = Vector3.zero;
            panel.depth = .08f;
            panel.height = .43f;
            panel.distance = 1.15f;
            panel.fitChildren = false;
            wrist.housing = housing;
            wrist.board = board;
            wrist.panelOffset = new Vector3(.255f, .10f, -.025f);
            talk.wrist = wrist;
            EditorUtility.SetDirty(talk);
            EditorUtility.SetDirty(wrist);
            EditorUtility.SetDirty(panel);
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();
        }

        static Transform New(string name, Transform parent)
        {
            var result = new GameObject(ObjectNames.Short(name)).transform;
            result.SetParent(parent, false);
            result.gameObject.layer = 2;
            return result;
        }

        static Material Material(string name, Color color, bool unlit, float metallic, float smoothness)
        {
            string path = Path + "/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit");
            if (!shader) throw new InvalidOperationException("URP material is missing.");
            if (!result)
            {
                result = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(result, path);
            }
            result.shader = shader;
            result.SetColor("_BaseColor", color);
            if (result.HasProperty("_Metallic")) result.SetFloat("_Metallic", metallic);
            if (result.HasProperty("_Smoothness")) result.SetFloat("_Smoothness", smoothness);
            result.enableInstancing = true;
            result.renderQueue = 2000;
            EditorUtility.SetDirty(result);
            return result;
        }

        static void Draw(string name, Transform parent, Shape shape, Material material)
        {
            string path = Path + "/" + name + ".asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (!mesh)
            {
                mesh = new Mesh { name = name };
                AssetDatabase.CreateAsset(mesh, path);
            }
            mesh.Clear(false);
            mesh.SetVertices(shape.vertices);
            mesh.SetTriangles(shape.triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(false);
            EditorUtility.SetDirty(mesh);
            var part = New(name, parent);
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, string value, float size, Vector3 position, Vector2 bounds)
        {
            var text = New(name, parent).gameObject.AddComponent<TextMeshPro>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = new Color(.96f, .97f, .98f);
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.richText = false;
            text.enableAutoSizing = false;
            text.transform.localPosition = position;
            text.rectTransform.sizeDelta = bounds;
            var renderer = text.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return text;
        }

        sealed class Shape
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<int> triangles = new List<int>();

            void Quad(int a, int b, int c, int d)
            {
                triangles.AddRange(new[] { a, b, c, a, c, d });
            }

            public void Round(Vector3 size, float radius, float bevel, Vector3 position, Quaternion rotation)
            {
                const int steps = 6;
                const int count = 4 * (steps + 1);
                int start = vertices.Count;
                for (int ring = 0; ring < 4; ring++)
                {
                    float inset = ring == 0 || ring == 3 ? bevel : 0;
                    float z = ring == 0 ? -size.z * .5f : ring == 1 ? -size.z * .5f + bevel : ring == 2 ? size.z * .5f - bevel : size.z * .5f;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        var center = new Vector2((corner == 0 || corner == 3 ? 1 : -1) * (size.x * .5f - radius), (corner < 2 ? 1 : -1) * (size.y * .5f - radius));
                        for (int i = 0; i <= steps; i++)
                        {
                            float angle = (corner * 90 + i * 90f / steps) * Mathf.Deg2Rad;
                            var point = new Vector3(center.x + Mathf.Cos(angle) * (radius - inset), center.y + Mathf.Sin(angle) * (radius - inset), z);
                            vertices.Add(position + rotation * point);
                        }
                    }
                }
                for (int ring = 0; ring < 3; ring++)
                    for (int i = 0; i < count; i++)
                    {
                        int next = (i + 1) % count;
                        Quad(start + ring * count + i, start + ring * count + next, start + (ring + 1) * count + next, start + (ring + 1) * count + i);
                    }
                int front = vertices.Count;
                vertices.Add(position + rotation * new Vector3(0, 0, -size.z * .5f));
                vertices.Add(position + rotation * new Vector3(0, 0, size.z * .5f));
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    triangles.AddRange(new[] { front, start + next, start + i, front + 1, start + count * 3 + i, start + count * 3 + next });
                }
            }

            public void Ring(float width, float height, float thickness, float length)
            {
                const int count = 48;
                int start = vertices.Count;
                for (int ring = 0; ring < 4; ring++)
                    for (int i = 0; i < count; i++)
                    {
                        float angle = i * Mathf.PI * 2 / count;
                        float inset = ring >= 2 ? thickness : 0;
                        vertices.Add(new Vector3(Mathf.Cos(angle) * (width - inset), Mathf.Sin(angle) * (height - inset), (ring % 2 == 0 ? -1 : 1) * length * .5f));
                    }
                for (int i = 0; i < count; i++)
                {
                    int next = (i + 1) % count;
                    Quad(start + i, start + next, start + count + next, start + count + i);
                    Quad(start + count * 2 + i, start + count * 3 + i, start + count * 3 + next, start + count * 2 + next);
                    Quad(start + i, start + count * 2 + i, start + count * 2 + next, start + next);
                    Quad(start + count + i, start + count + next, start + count * 3 + next, start + count * 3 + i);
                }
            }
        }
    }
}
