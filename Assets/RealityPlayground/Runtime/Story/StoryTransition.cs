using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RealityPlayground.Story
{

    public sealed class StoryTransition : MonoBehaviour
    {
        public MeshRenderer veil;
        public MeshRenderer whiteVeil;
        public MeshRenderer blinkVeil;
        public Transform shards;
        public Transform[] pieces;
        Material material, whiteMaterial, shardMaterial, blinkMaterial;
        UniversalAdditionalCameraData cameraData;
        CameraOverrideOption previousOpaque;
        bool cameraChanged;
        uint operation;
        public bool Busy { get; private set; }
        public float Progress { get; private set; }
        public bool IsBlinking { get; private set; }
        public float BlinkClosure { get; private set; }
        public static StoryTransition Create(Transform parent)
        {
            var root=new GameObject("Transition"); root.transform.SetParent(parent,false);
            var fx=root.AddComponent<StoryTransition>();
            var mat=new Material(Shader.Find("RealityPlayground/TransitionVeil")){name="Glitch Refraction"};
            fx.veil=GreyboxUtil.Primitive("Stereo refraction",PrimitiveType.Quad,root.transform,Vector3.zero,Vector3.one,mat,false).GetComponent<MeshRenderer>();
            fx.veil.enabled=false; fx.veil.shadowCastingMode=ShadowCastingMode.Off;
            var white=new Material(Shader.Find("RealityPlayground/StoryWhiteout")){name="Whiteout"};
            fx.whiteVeil=GreyboxUtil.Primitive("Whiteout",PrimitiveType.Quad,root.transform,Vector3.zero,Vector3.one,white,false).GetComponent<MeshRenderer>(); fx.whiteVeil.enabled=false;
            fx.shards=new GameObject("Worldfractureshell").transform; fx.shards.SetParent(root.transform,false);
            var glass=new Material(Shader.Find("RealityPlayground/RealityShard")){name="Glitch Shards"};
            fx.pieces=new Transform[28];
            var mesh=new Mesh{name="Story fracture triangle"}; mesh.vertices=new[]{new Vector3(-.5f,-.3f,0),new Vector3(.6f,-.2f,0),new Vector3(.1f,.8f,0)}; mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.up}; mesh.triangles=new[]{0,2,1};mesh.RecalculateNormals();mesh.RecalculateBounds();
            for(int i=0;i<fx.pieces.Length;i++) { var part=new GameObject(ObjectNames.Short("Reality plate "+i));part.transform.SetParent(fx.shards,false);part.AddComponent<MeshFilter>().sharedMesh=mesh;var r=part.AddComponent<MeshRenderer>();r.sharedMaterial=glass;r.shadowCastingMode=ShadowCastingMode.Off;fx.pieces[i]=part.transform; }
            fx.shards.gameObject.SetActive(false);
            fx.EnsureBlinkResources();
            return fx;
        }

        public void EnsureBlinkResources()
        {
            if (!blinkVeil)
            {
                var shader = Shader.Find("RealityPlayground/StoryEyelids");
                if (!shader) return;
                var lids = new Material(shader) { name = "Blink Material" };
                blinkVeil = GreyboxUtil.Primitive("Eyelids", PrimitiveType.Quad,
                    transform, Vector3.zero, Vector3.one, lids, false).GetComponent<MeshRenderer>();
                if (Application.isPlaying) blinkMaterial = lids;
            }
            blinkVeil.shadowCastingMode = ShadowCastingMode.Off;
            blinkVeil.receiveShadows = false;
            blinkVeil.lightProbeUsage = LightProbeUsage.Off;
            blinkVeil.reflectionProbeUsage = ReflectionProbeUsage.Off;
            blinkVeil.allowOcclusionWhenDynamic = false;
            blinkVeil.enabled = false;
            if (Application.isPlaying && !blinkMaterial) blinkMaterial = blinkVeil.material;
        }
        void Awake()
        {
            if(veil) material=veil.material;
            if(whiteVeil) whiteMaterial=whiteVeil.material;
            if(pieces!=null) foreach(var p in pieces) if(p && p.TryGetComponent<Renderer>(out var r)) { if(!shardMaterial) shardMaterial=new Material(r.sharedMaterial);r.sharedMaterial=shardMaterial; }
            EnsureBlinkResources();
        }

        public IEnumerator PlayBlink(float duration = .84f, Action midpoint = null)
        {
            if (Busy || !isActiveAndEnabled || !RealityPlayer.Head) yield break;
            EnsureBlinkResources();
            if (!blinkVeil || !blinkMaterial) yield break;
            uint token = ++operation;
            Busy = true; IsBlinking = true;
            if (veil) veil.enabled = false;
            if (whiteVeil) whiteVeil.enabled = false;
            if (shards) shards.gameObject.SetActive(false);
            duration = Mathf.Max(.35f, duration);
            float closeTime = duration * .34f, holdTime = duration * .18f, openTime = duration * .48f;
            try
            {
                float elapsed = 0;
                while (elapsed < closeTime && Current(token))
                {
                    Progress = elapsed / duration;
                    DrawBlink(Mathf.SmoothStep(0, 1, elapsed / closeTime));
                    elapsed += Time.unscaledDeltaTime; yield return null;
                }
                if (!Current(token)) yield break;
                Progress = .34f; DrawBlink(1);

                yield return null;
                if (!Current(token)) yield break;
                midpoint?.Invoke();
                if (!Current(token)) yield break;
                DrawBlink(1);
                elapsed = 0;
                while (elapsed < holdTime && Current(token))
                {
                    Progress = .34f + elapsed / duration;
                    elapsed += Time.unscaledDeltaTime; yield return null;
                }
                elapsed = 0;
                while (elapsed < openTime && Current(token))
                {
                    Progress = .52f + elapsed / duration;
                    DrawBlink(1 - Mathf.SmoothStep(0, 1, elapsed / openTime));
                    elapsed += Time.unscaledDeltaTime; yield return null;
                }
            }
            finally { if (operation == token) Cleanup(); }
        }
        bool Current(uint token) => token == operation && Busy && isActiveAndEnabled && RealityPlayer.Head;
        void DrawBlink(float closure)
        {
            BlinkClosure = Mathf.Clamp01(closure);
            if (!blinkVeil || !blinkMaterial || !RealityPlayer.Head) return;
            PositionBlink();
            blinkMaterial.SetFloat("_Closure", BlinkClosure);
            blinkVeil.enabled = BlinkClosure > 0;
        }
        void PositionBlink()
        {
            var head = RealityPlayer.Head;
            if (!head || !blinkVeil) return;

            blinkVeil.transform.SetPositionAndRotation(head.position + head.forward * .35f, head.rotation);
            blinkVeil.transform.localScale = new Vector3(4, 4, 1);
        }
        void LateUpdate()
        {
            if (IsBlinking)
            {
                if (!RealityPlayer.Head) { ++operation; Cleanup(); }
                else PositionBlink();
            }
        }
        public IEnumerator Play(float duration, Action midpoint=null, bool white=false, Action<float> during=null)
        {
            if(Busy || !isActiveAndEnabled || !RealityPlayer.Head) yield break;
            uint token = ++operation;
            Busy=true;bool crossed=false;float elapsed=0;duration=Mathf.Max(.05f,duration);
            var head=RealityPlayer.Head;
            if(head && head.TryGetComponent<Camera>(out var camera)) {cameraData=camera.GetUniversalAdditionalCameraData();previousOpaque=cameraData.requiresColorOption;cameraData.requiresColorOption=CameraOverrideOption.On;cameraChanged=true;}
            if(shards) { shards.position=head?head.position:Vector3.zero;shards.rotation=Quaternion.identity;shards.gameObject.SetActive(true); }
            try
            {
                while(elapsed<duration && Current(token))
                {
                    Progress=Mathf.Clamp01(elapsed/duration);
                    if(!crossed && Progress>=.5f) {crossed=true;midpoint?.Invoke();if(shards && RealityPlayer.Head)shards.position=RealityPlayer.Head.position;}
                    if (!Current(token)) yield break;
                    during?.Invoke(Progress); Draw(Progress,white);
                    elapsed+=Time.deltaTime;yield return null;
                }
                if(Current(token))
                {
                    if(!crossed) midpoint?.Invoke();
                    during?.Invoke(1);
                }
            }
            finally { if(operation == token) Cleanup(); }
        }
        void Draw(float t,bool white)
        {
            var head=RealityPlayer.Head;if(!head)return;
            float amount=Mathf.Sin(t*Mathf.PI);
            if(veil) { veil.enabled=true; veil.transform.SetPositionAndRotation(head.position+head.forward*.25f,head.rotation);veil.transform.localScale=new Vector3(2,2,1); }
            if(material) { material.SetFloat("_Progress",t);material.SetFloat("_Amount",amount);material.SetFloat("_Phase",Time.time);material.SetFloat("_Intensity",1.1f); }
            if(whiteVeil) { whiteVeil.enabled=white;whiteVeil.transform.SetPositionAndRotation(head.position+head.forward*.24f,head.rotation);whiteVeil.transform.localScale=new Vector3(2,2,1); }
            if(whiteMaterial)whiteMaterial.SetFloat("_Opacity",white?Mathf.SmoothStep(0,1,Mathf.InverseLerp(.22f,.45f,t))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.58f,.85f,t))):0);
            if(shardMaterial){shardMaterial.SetFloat("_Amount",amount);shardMaterial.SetFloat("_Phase",Time.time);}
            if(pieces!=null) for(int i=0;i<pieces.Length;i++) if(pieces[i])
            {
                float a=i*2.399963f;float y=1-(i+.5f)/pieces.Length*2;float r=Mathf.Sqrt(1-y*y);
                var direction=new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);
                pieces[i].position=(shards?shards.position:head.position)+direction*(1.8f+amount*1.8f);
                pieces[i].rotation=Quaternion.LookRotation(-direction)*Quaternion.Euler(amount*35*Mathf.Sin(i),amount*60, i*17);
                pieces[i].localScale=Vector3.one*(.4f+amount*.7f);
            }
        }
        void Cleanup(){if(veil)veil.enabled=false;if(whiteVeil)whiteVeil.enabled=false;if(blinkVeil)blinkVeil.enabled=false;if(shards)shards.gameObject.SetActive(false);if(cameraChanged&&cameraData)cameraData.requiresColorOption=previousOpaque;cameraChanged=false;Busy=false;Progress=0;IsBlinking=false;BlinkClosure=0;if(blinkMaterial)blinkMaterial.SetFloat("_Closure",0);}
        void OnDisable(){++operation;StopAllCoroutines();Cleanup();}
        void OnDestroy(){if(material)Destroy(material);if(whiteMaterial)Destroy(whiteMaterial);if(shardMaterial)Destroy(shardMaterial);if(blinkMaterial)Destroy(blinkMaterial);}
    }
}
