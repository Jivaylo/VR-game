using UnityEngine;
using UnityEngine.Rendering;

namespace RealityPlayground
{
    public static class GreyboxUtil
    {
        public static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, bool collider = true)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = ObjectNames.Short(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider)
            {
                var c = go.GetComponent<Collider>();
                if (Application.isPlaying) Object.Destroy(c); else Object.DestroyImmediate(c);
            }
            return go;
        }

        public static Material Material(string name, Color color, float emission = 0f, bool transparent = false)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", .28f);
            if (emission > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            if (transparent)
            {
                mat.SetFloat("_Surface", 1);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            return mat;
        }

        public static TextMesh Label(string text, Transform parent, Vector3 position, float size, Color color)
        {
            var go = new GameObject(ObjectNames.Short(parent ? parent.name + " Text" : "Text"));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            var label = go.AddComponent<TextMesh>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 64;
            label.characterSize = Mathf.Max(.06f, size) * 10f / label.fontSize;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            return label;
        }

        public static LineRenderer Line(string name, Transform parent, Vector3[] points, Material material, float width, bool loop = false)
        {
            var go = new GameObject(ObjectNames.Short(name));
            go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Length;
            line.SetPositions(points);
            line.sharedMaterial = material;
            line.widthMultiplier = width;
            line.loop = loop;
            line.numCornerVertices = 3;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }
    }
}
