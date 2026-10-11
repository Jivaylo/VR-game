using UnityEngine;
using UnityEngine.Events;

namespace RealityPlayground.Story
{

    public sealed class StoryPoster : PlaygroundTarget
    {
        public bool interactionEnabled;
        public bool deferRiftOpening;
        public UnityEvent torn = new UnityEvent();
        public bool Torn { get; private set; }
        public float PeelProgress { get; private set; }
        public StoryFallenPoster DetachedPaper { get; private set; }
        public Vector3 MeasuredReleaseVelocity => releaseVelocity;
        public int RejectedVelocitySamples { get; private set; }
        public int PaperVertexCount => paperWork == null ? 0 : paperWork.Length;
        public int BoundGlyphMeshCount => ink!=null?ink.GlyphMeshCount:0;
        public int BoundGlyphVertexCount => ink!=null?ink.GlyphVertexCount:0;
        public override bool UsesTrackedHandForGrab => true;
        [SerializeField] MeshFilter paper, worldMembrane;
        [SerializeField] Transform graphics, rift;
        [SerializeField] Transform[] spiralRings, shards;
        [SerializeField] Collider selectionVolume;
        [SerializeField] Renderer paperRenderer, membraneRenderer;
        Mesh paperMesh, membraneMesh;
        StoryPosterInk ink;
        Vector3[] paperHome, membraneHome;
        Vector3[] paperWork, membraneWork;
        Vector3[] previousPaperWorld, paperVelocity;
        Vector3 paperMin, paperMax;
        float[] paperDistances, membraneDistances, paperWeights, membraneWeights;
        Vector3 weightedCorner;
        bool weightsReady;
        Vector3 handStart, pull, corner;
        Vector3 previousHandPosition, releaseVelocity;
        float previousHandTime, previousMeshTime, nextHaptic, previousTension;
        Vector3 previousFeet, graphicsHomePosition;
        Quaternion graphicsHomeRotation;
        bool graphicsCaptured, suppressVelocitySample;
        Transform grabbingHand;
        float riftAge, age;
        bool riftOpen;
        MaterialPropertyBlock properties;

        public void ConfigureAperture()
        {
            if(!rift)return;
            var volume=ObjectNames.Find(rift, "Impossible depth");
            if(volume)
            {

                volume.localPosition=new Vector3(0,0,-.415f);
                volume.localScale=new Vector3(1.7f,2.25f,.42f);
                var renderer=volume.GetComponent<Renderer>();
                if(renderer){var block=new MaterialPropertyBlock();renderer.GetPropertyBlock(block);block.SetFloat("_Intensity",2f);renderer.SetPropertyBlock(block);}
            }
            if(spiralRings!=null)for(int i=0;i<spiralRings.Length;i++)
                if(spiralRings[i])spiralRings[i].localPosition=new Vector3(0,0,-.2f-i*.024f);
        }
        void Awake(){ConfigureAperture();}
        void Start(){EnsureMeshes();}

        public static GameObject Create(Transform parent)
        {
            var root = new GameObject("Poster"); root.transform.SetParent(parent, false);
            var poster = root.AddComponent<StoryPoster>();
            var shader = Shader.Find("RealityPlayground/StoryMembrane");
            var paperMat = shader ? new Material(shader) { name = "Poster Film" } : StoryVignetteGeometry.Hologram("Poster Film", new Color(.1f, .2f, .35f, .85f));
            var membraneMat = new Material(paperMat) { name = "Poster Membrane" };
            if (membraneMat.HasProperty("_Membrane")) membraneMat.SetFloat("_Membrane", 1);
            if(paperMat.HasProperty("_DepthWrite"))paperMat.SetFloat("_DepthWrite",1);
            paperMat.renderQueue=2990;
            poster.paper = StoryVignetteGeometry.Surface("Poster Sheet", root.transform, new Vector3(0, 1.45f, -.06f), StoryVignetteGeometry.Grid("Peelable advert surface", 24, 32, new Vector2(1.18f, 1.64f)), paperMat);
            poster.paperRenderer = poster.paper.GetComponent<Renderer>();
            poster.worldMembrane = StoryVignetteGeometry.Surface("Wall Membrane", root.transform, new Vector3(0, 1.45f, -.045f), StoryVignetteGeometry.Grid("Wall Membrane Mesh", 32, 32, new Vector2(3.5f, 3.15f)), membraneMat);
            poster.membraneRenderer = poster.worldMembrane.GetComponent<Renderer>(); poster.membraneRenderer.enabled = false;
            poster.graphics = StoryVignetteGeometry.Group("Forbidden message", root.transform, new Vector3(0, 1.45f, -.08f));
            var cream = new Color(.91f, .95f, .73f);
            GreyboxUtil.Label("DO YOU REMEMBER\nAN UNFILTERED SKY?", poster.graphics, new Vector3(0, .57f, -.012f), .135f, cream);
            GreyboxUtil.Label("THIS IS NOT\nTHE WORLD.", poster.graphics, new Vector3(0, -.23f, -.012f), .2f, Color.white);
            GreyboxUtil.Label("PULL ANY CORNER", poster.graphics, new Vector3(0, -.67f, -.012f), .075f, new Color(.05f, 1, .91f));
            var eyeMat = GreyboxUtil.Material("Poster Eye", new Color(.1f, 1, .78f), 3);
            var eye = StoryVignetteGeometry.Ring("The unblinking eye", poster.graphics, .23f, .012f, eyeMat);
            eye.localScale = new Vector3(1.8f, .58f, 1); eye.localPosition = new Vector3(0, .12f, -.012f);
            var iris = StoryVignetteGeometry.Ring("Iris", poster.graphics, .085f, .009f, eyeMat); iris.localPosition = eye.localPosition;
            GreyboxUtil.Primitive("Eye pupil", PrimitiveType.Sphere, poster.graphics, new Vector3(0, .12f, -.014f), Vector3.one * .075f, eyeMat, false);
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            {
                var tab = GreyboxUtil.Primitive("Lift corner", PrimitiveType.Cube, poster.graphics, new Vector3(x * .52f, y * .735f, -.02f), new Vector3(.13f, .018f, .018f), eyeMat, false);
                tab.transform.localRotation = Quaternion.Euler(0, 0, x * y * 32);
            }
            var box = root.AddComponent<BoxCollider>(); box.isTrigger = true; box.center = new Vector3(0, 1.45f, -.085f); box.size = new Vector3(1.3f, 1.8f, .13f); poster.selectionVolume = box;
            poster.rift = StoryVignetteGeometry.Group("Impossible aperture", root.transform, new Vector3(0, 1.45f, .075f));
            var volumeShader = Shader.Find("RealityPlayground/StoryPortalFast");
            var volume = volumeShader ? new Material(volumeShader) { name = "Rift Volume" } : eyeMat;
            if (volume.HasProperty("_Rupture")) { volume.SetFloat("_Rupture", 1); volume.SetFloat("_Intensity", 3.2f); }
            GreyboxUtil.Primitive("Impossible depth", PrimitiveType.Cube, poster.rift, new Vector3(0, 0, .1f), new Vector3(2.5f, 2.9f, 2.6f), volume, false);
            poster.spiralRings = new Transform[10];
            for (int i = 0; i < poster.spiralRings.Length; i++)
            {
                var color = Color.HSVToRGB(i / 11f, .86f, 1); color.a = .8f;
                var mat = StoryVignetteGeometry.Hologram("Rupture spectral band " + i, color, 3.5f);
                var points = new Vector3[96];
                for (int j = 0; j < points.Length; j++)
                {
                    float a = j * Mathf.PI * 2 / points.Length;
                    float rad = .78f - i * .05f + .06f * Mathf.Sin(a * 5 + i * .75f);
                    points[j] = new Vector3(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad * 1.2f, Mathf.Sin(a * 3 + i) * .06f);
                }
                poster.spiralRings[i] = GreyboxUtil.Line("Torn spectral fold " + i, poster.rift, points, mat, .026f - i * .001f, true).transform;
                poster.spiralRings[i].localPosition = new Vector3(0, 0, .12f + i * .095f);
            }
            poster.shards = new Transform[24];
            for (int i = 0; i < poster.shards.Length; i++)
            {
                float a = i * Mathf.PI * 2 / poster.shards.Length;
                var shard = GreyboxUtil.Primitive("Poster Shard " + i, PrimitiveType.Cube, poster.rift,
                    new Vector3(Mathf.Cos(a) * 1.15f, Mathf.Sin(a) * 1.35f, -.1f), new Vector3(.04f + i % 4 * .02f, .16f + i % 3 * .06f, .09f), i % 2 == 0 ? eyeMat : paperMat, false).transform;
                shard.localRotation = Quaternion.Euler(i * 32, i * 43, i * 17); poster.shards[i] = shard;
            }
            poster.rift.gameObject.SetActive(false);
            poster.ConfigureAperture();
            return root;
        }

        void EnsureMeshes()
        {
            if (properties == null) properties = new MaterialPropertyBlock();
            if (paper && !paperMesh)
            {
                paperMesh = Instantiate(paper.sharedMesh); paperMesh.MarkDynamic(); paper.sharedMesh = paperMesh; paperHome = paperMesh.vertices;
                paperMin=paperMesh.bounds.min;paperMax=paperMesh.bounds.max;
                paperWork=new Vector3[paperHome.Length];paperDistances=new float[paperHome.Length];paperWeights=new float[paperHome.Length];weightsReady=false;
                previousPaperWorld=new Vector3[paperHome.Length];paperVelocity=new Vector3[paperHome.Length];
                for(int i=0;i<paperHome.Length;i++){paperWork[i]=paperHome[i];previousPaperWorld[i]=paper.transform.TransformPoint(paperHome[i]);}
                previousMeshTime=Time.unscaledTime;
            }
            if (worldMembrane && !membraneMesh)
            {
                membraneMesh = Instantiate(worldMembrane.sharedMesh); membraneMesh.MarkDynamic(); worldMembrane.sharedMesh = membraneMesh; membraneHome = membraneMesh.vertices;
                membraneWork=new Vector3[membraneHome.Length];membraneDistances=new float[membraneHome.Length];membraneWeights=new float[membraneHome.Length];weightsReady=false;
            }
            if(graphics && !graphicsCaptured){graphicsHomePosition=graphics.localPosition;graphicsHomeRotation=graphics.localRotation;graphicsCaptured=true;}
            if(ink==null && paper && paperHome!=null && graphics)ink=new StoryPosterInk(paper,graphics,paperHome);
        }
        public override void BeginInteraction(Transform hand)
        {
            if (!interactionEnabled || Torn || !hand || grabbingHand) return;
            EnsureMeshes(); grabbingHand = hand;
            handStart = hand.position - (paper ? paper.transform.TransformVector(pull) : transform.TransformVector(pull));
            previousHandPosition=hand.position;previousHandTime=Time.unscaledTime;releaseVelocity=Vector3.zero;
            previousFeet=RealityPlayer.FeetPosition;previousTension=PeelProgress;nextHaptic=0;
            Vector3 local = paper ? paper.transform.InverseTransformPoint(hand.position) : transform.InverseTransformPoint(hand.position) - Vector3.up * 1.45f;
            corner = CornerFor(local);
        }
        public override void BeginInteraction(Transform hand, Vector3 selectionPoint)
        {
            if (grabbingHand) return;
            BeginInteraction(hand);
            if (grabbingHand != hand || !hand || !paper) return;

            Vector3 local = paper.transform.InverseTransformPoint(selectionPoint);
            corner = CornerFor(local);
        }
        Vector3 CornerFor(Vector3 point)
        {
            return new Vector3(point.x<(paperMin.x+paperMax.x)*.5f?paperMin.x:paperMax.x,
                point.y<(paperMin.y+paperMax.y)*.5f?paperMin.y:paperMax.y,(paperMin.z+paperMax.z)*.5f);
        }
        public override void UpdateInteraction(Transform hand)
        {
            if (hand != grabbingHand || !hand || Torn || !interactionEnabled) return;
            float handDelta=Time.unscaledTime-previousHandTime;
            Vector3 handMotion=hand.position-previousHandPosition;
            Vector3 feet=RealityPlayer.FeetPosition,rigMotion=feet-previousFeet;
            previousFeet=feet;
            if(rigMotion.sqrMagnitude>.2f*.2f){handStart+=rigMotion;handMotion-=rigMotion;}
            suppressVelocitySample=handDelta<=.0001f || handDelta>.12f || handMotion.sqrMagnitude>.45f*.45f || rigMotion.sqrMagnitude>.2f*.2f;
            if(suppressVelocitySample){releaseVelocity=Vector3.zero;RejectedVelocitySamples++;}
            else releaseVelocity=Vector3.Lerp(releaseVelocity,Vector3.ClampMagnitude(handMotion/handDelta,3.5f),1-Mathf.Exp(-handDelta*22));
            previousHandPosition=hand.position;previousHandTime=Time.unscaledTime;
            pull = paper ? paper.transform.InverseTransformVector(hand.position-handStart) : transform.InverseTransformVector(hand.position-handStart);
            PeelProgress = Mathf.Clamp01((hand.position-handStart).magnitude / .58f);
            Deform();
            TensionFeedback();
            if (PeelProgress >= .999f)
            {

                if(paper && paperMesh && paperRenderer)
                    DetachedPaper=StoryFallenPoster.Create(paper,paperHome,graphics,paperRenderer.sharedMaterial,releaseVelocity,this,paperVelocity,ink);
                Torn = true; grabbingHand = null;
                if (paperRenderer) paperRenderer.enabled = false;
                if (membraneRenderer) membraneRenderer.enabled = false;
                if (graphics) graphics.gameObject.SetActive(false);
                if (selectionVolume) selectionVolume.enabled = false;

                var interactable=GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRSimpleInteractable>();
                if(interactable)interactable.enabled=false;
                if(!deferRiftOpening)SetRiftOpen(true); torn.Invoke();
            }
        }
        public override void EndInteraction(Transform hand) { if (hand == grabbingHand) { grabbingHand = null; releaseVelocity=Vector3.zero; } }

        void TensionFeedback()
        {
            if(!RealityPlayer.IsXR || !grabbingHand || Time.unscaledTime<nextHaptic || PeelProgress-previousTension<.075f)return;
            UnityEngine.XR.XRNode node=grabbingHand==RealityPlayer.LeftHand || (RealityPlayer.LeftHand && grabbingHand.IsChildOf(RealityPlayer.LeftHand))
                ? UnityEngine.XR.XRNode.LeftHand : UnityEngine.XR.XRNode.RightHand;
            var device=UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
            if(device.isValid)device.SendHapticImpulse(0,PeelProgress>.97f?.28f:Mathf.Lerp(.035f,.13f,PeelProgress),PeelProgress>.97f?.055f:.025f);
            nextHaptic=Time.unscaledTime+.07f;previousTension=PeelProgress;
        }
        void Update()
        {
            age += Time.deltaTime;
            if (!Torn && !grabbingHand && PeelProgress > .0001f)
            {
                pull = Vector3.MoveTowards(pull, Vector3.zero, Time.deltaTime * .85f);
                PeelProgress = Mathf.Clamp01((paper?paper.transform.TransformVector(pull):transform.TransformVector(pull)).magnitude / .58f); Deform();
            }
            if (!riftOpen || !rift) return;
            riftAge += Time.deltaTime;
            rift.localScale = Vector3.one * Mathf.SmoothStep(.04f, 1, Mathf.Clamp01(riftAge / 1.7f));
            if (spiralRings != null) for (int i = 0; i < spiralRings.Length; i++)
                if (spiralRings[i]) spiralRings[i].localRotation = Quaternion.Euler(Mathf.Sin(age * .37f + i) * 16, Mathf.Cos(age * .43f + i) * 12, age * (i % 2 == 0 ? 16 : -11) + i * 13);
            if (shards != null) for (int i = 0; i < shards.Length; i++)
                if (shards[i]) shards[i].Rotate(new Vector3(11, 18, 6) * Time.deltaTime * (1 + i % 3), Space.Self);
        }
        void Deform()
        {
            EnsureMeshes();
            if(!weightsReady || weightedCorner!=corner)
            {
                PrepareWeights(paperHome,paperDistances,paperWeights,false);
                PrepareWeights(membraneHome,membraneDistances,membraneWeights,true);
                weightedCorner=corner;weightsReady=true;
            }
            if (paperMesh) ApplyMesh(paperMesh, paperHome, paperWork, paperDistances, paperWeights, false);
            if (membraneMesh) ApplyMesh(membraneMesh, membraneHome, membraneWork, membraneDistances, membraneWeights, true);
            if(ink!=null)ink.Update(paperWork);
            properties.SetFloat("_Peel", PeelProgress);
            if (paperRenderer) paperRenderer.SetPropertyBlock(properties);
            if (membraneRenderer) { membraneRenderer.enabled = PeelProgress > .01f; membraneRenderer.SetPropertyBlock(properties); }
        }
        void PrepareWeights(Vector3[] rest,float[] distances,float[] weights,bool broad)
        {
            if(rest==null)return;
            for(int i=0;i<rest.Length;i++)
            {
                float distance=Vector3.Distance(rest[i],corner);distances[i]=distance;
                weights[i]=broad?Mathf.Exp(-distance*distance*.82f):Mathf.Pow(Mathf.Clamp01(1-distance/2.08f),1.25f);
            }
        }
        void ApplyMesh(Mesh mesh, Vector3[] rest, Vector3[] vertices, float[] distances, float[] weights, bool broad)
        {
            Vector3 localPull=broad && worldMembrane && paper?worldMembrane.transform.InverseTransformVector(paper.transform.TransformVector(pull)):pull;
            float loosen=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.08f,1,PeelProgress));
            for (int i = 0; i < vertices.Length; i++)
            {
                float distance=distances[i],weight=broad?weights[i]:Mathf.Lerp(weights[i],1,loosen*.78f);
                vertices[i] = rest[i] + localPull * weight * (broad ? 1.8f : 1f);

                float curlAtGrip=broad?1:Mathf.SmoothStep(0,1,Mathf.Clamp01(distance/.22f));
                vertices[i].z -= Mathf.Sin(distance*7-PeelProgress*4)*weights[i]*PeelProgress*(broad?.21f:.065f)*curlAtGrip;
            }
            if(!broad && previousPaperWorld!=null)
            {
                float dt=Time.unscaledTime-previousMeshTime;
                bool valid=!suppressVelocitySample && dt>.0001f && dt<.12f;
                Matrix4x4 world=paper.transform.localToWorldMatrix;
                for(int i=0;i<vertices.Length;i++)
                {
                    Vector3 point=world.MultiplyPoint3x4(vertices[i]),delta=point-previousPaperWorld[i];
                    paperVelocity[i]=valid && delta.sqrMagnitude<.45f*.45f?Vector3.Lerp(paperVelocity[i],Vector3.ClampMagnitude(delta/dt,3.5f),1-Mathf.Exp(-dt*22)):Vector3.zero;
                    previousPaperWorld[i]=point;
                }
                previousMeshTime=Time.unscaledTime;suppressVelocitySample=false;
            }

            mesh.vertices = vertices; mesh.RecalculateBounds();
        }
        public void CopyHeldWorldVertices(Vector3[] destination)
        {
            if(!paper || paperWork==null || destination==null || destination.Length<paperWork.Length)return;
            Matrix4x4 world=paper.transform.localToWorldMatrix;
            for(int i=0;i<paperWork.Length;i++)destination[i]=world.MultiplyPoint3x4(paperWork[i]);
        }
        public void SetRiftOpen(bool value)
        {
            riftOpen = value; if (value) riftAge = 0;
            if (rift) { rift.gameObject.SetActive(value); if (value) rift.localScale = Vector3.one * .04f; }
        }
        void OnDisable() { grabbingHand = null; }
        void OnDestroy()
        {
            if(DetachedPaper)Destroy(DetachedPaper.gameObject);
            if(ink!=null)ink.Dispose();
            if (paperMesh) Destroy(paperMesh); if (membraneMesh) Destroy(membraneMesh);
        }
    }
}
