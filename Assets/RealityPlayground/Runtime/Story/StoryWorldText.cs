using UnityEngine;
using TMPro;

namespace RealityPlayground.Story
{

    [ExecuteAlways,RequireComponent(typeof(TextMesh))]
    public sealed class StoryWorldText : MonoBehaviour
    {
        TextMesh label;
        Material material;
        bool ownsMaterial;
        [SerializeField] TMP_FontAsset distanceFieldFont;
        [SerializeField] Material distanceFieldMaterial;
        [SerializeField] TextMeshPro distanceFieldText;
        string lastText;
        Color lastColor;
        float lastSize=-1;
        public bool UsesDistanceField => distanceFieldText && distanceFieldFont;
        public TextMeshPro DistanceFieldText => distanceFieldText;
        public void SetDistanceFieldFont(TMP_FontAsset font,Material sharedMaterial=null)
        {
            distanceFieldFont=font;distanceFieldMaterial=sharedMaterial;lastText=null;lastSize=-1;Refresh();
        }
        void OnEnable(){Font.textureRebuilt+=FontChanged;Refresh();}
        void OnDisable(){Font.textureRebuilt-=FontChanged;}
        void FontChanged(Font font){if(label && label.font==font && material)material.mainTexture=font.material.mainTexture;}
        public void Refresh()
        {
            label=GetComponent<TextMesh>();if(!label || !label.font)return;
            if(distanceFieldFont)
            {
                if(!distanceFieldText)
                {
                    var child=ObjectNames.Find(transform, "Distance field lettering");
                    if(!child){child=new GameObject("Distancefieldlettering").transform;child.SetParent(transform,false);}
                    distanceFieldText=child.GetComponent<TextMeshPro>();
                    if(!distanceFieldText)distanceFieldText=child.gameObject.AddComponent<TextMeshPro>();
                    distanceFieldText.rectTransform.sizeDelta=new Vector2(100,100);
                    distanceFieldText.textWrappingMode=TextWrappingModes.NoWrap;
                    distanceFieldText.overflowMode=TextOverflowModes.Overflow;
                    distanceFieldText.enableAutoSizing=false;
                    distanceFieldText.isOrthographic=false;
                    distanceFieldText.richText=label.richText;
                    distanceFieldText.alignment=TextAlignmentOptions.Center;
                    distanceFieldText.renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                distanceFieldText.font=distanceFieldFont;
                if(distanceFieldMaterial)distanceFieldText.fontSharedMaterial=distanceFieldMaterial;
                GetComponent<MeshRenderer>().enabled=false;
                SyncText();
                return;
            }
            var shader=Shader.Find("RealityPlayground/WorldText");if(!shader)return;
            var renderer=GetComponent<MeshRenderer>();
            if(!material)
            {
                if(!Application.isPlaying && renderer.sharedMaterial && renderer.sharedMaterial.shader==shader)material=renderer.sharedMaterial;
                else {material=new Material(shader){name="World Font"};ownsMaterial=true;}
            }
            material.mainTexture=label.font.material.mainTexture;
            renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        void LateUpdate(){if(distanceFieldFont && distanceFieldText)SyncText();}
        void SyncText()
        {
            if(!label || !distanceFieldText)return;
            float size=label.fontSize*label.characterSize;
            if(lastText==label.text && lastColor==label.color && Mathf.Approximately(lastSize,size))return;
            lastText=label.text;lastColor=label.color;lastSize=size;
            distanceFieldText.text=label.text;distanceFieldText.color=label.color;distanceFieldText.fontSize=size;
            distanceFieldText.ForceMeshUpdate(true);
        }
        void OnDestroy(){if(ownsMaterial && material){if(Application.isPlaying)Destroy(material);else if(!UnityEditorAsset(material))DestroyImmediate(material);}}
        static bool UnityEditorAsset(Object asset)
        {
            #if UNITY_EDITOR
            return UnityEditor.AssetDatabase.Contains(asset);
            #else
            return false;
            #endif
        }
    }
}
