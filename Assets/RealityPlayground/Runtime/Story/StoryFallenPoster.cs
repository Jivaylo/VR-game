using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace RealityPlayground.Story
{

    public sealed class StoryFallenPoster : MonoBehaviour
    {
        public bool HasSettled => cloth != null && cloth.HasSettled;
        public bool FoundLandingSurface => cloth != null && cloth.FoundLandingSurface;
        public Renderer PaperRenderer { get; private set; }
        public Transform PrintedGraphics { get; private set; }
        public float ReleaseContinuityError { get; private set; }
        public float PrintContinuityError { get; private set; }
        public float MaximumReleaseSpeed => cloth != null ? cloth.MaximumReleaseSpeed : 0;
        public float MaximumFloorPenetration => cloth != null ? cloth.MaximumFloorPenetration : 0;
        public float MaximumStretchError => cloth != null ? cloth.MaximumStretchError : 0;
        public float MaximumParticleSpeed => cloth != null ? cloth.MaximumSpeed : 0;
        public float RootMeanSquareParticleSpeed => cloth != null ? cloth.RootMeanSquareSpeed : 0;
        public int SimulationParticleCount => cloth != null ? cloth.ParticleCount : 0;
        public int SimulationSteps => cloth != null ? cloth.StepCount : 0;
        public int CollisionCastCount => cloth != null ? cloth.CollisionCastCount : 0;
        public int MaximumCastsInOneStep => cloth != null ? cloth.MaximumCastsInOneStep : 0;
        public bool HasCachedWall => cloth != null && cloth.HasCachedWall;
        public Vector3 CenterOfMass => cloth != null ? cloth.Center : transform.position;
        public Vector3 SurfaceFrontNormal => cloth != null ? cloth.FrontNormal : -transform.forward;
        public int BoundPrintVertexCount { get; private set; }
        public int BoundGlyphMeshCount { get; private set; }
        public int BoundGlyphVertexCount { get; private set; }

        sealed class PrintedMesh
        {
            public Mesh mesh;
            public Transform space;
            public Vector3[] vertices, normals;
            public StoryPaperCloth.Binding[] bindings;
        }
        sealed class PrintedLine
        {
            public LineRenderer line;
            public Vector3[] vertices;
            public StoryPaperCloth.Binding[] bindings;
        }
        sealed class PrintedTransform
        {
            public Transform target;
            public StoryPaperCloth.Binding center, right, up;
        }

        StoryPaperCloth cloth;
        Mesh mesh;
        Vector3[] working;
        StoryPaperCloth.Binding[] bindings;
        readonly List<PrintedMesh> printedMeshes = new List<PrintedMesh>();
        readonly List<PrintedLine> printedLines = new List<PrintedLine>();
        readonly List<PrintedTransform> printedTransforms = new List<PrintedTransform>();
        MaterialPropertyBlock properties;
        float age, accumulator;
        int createdFrame;

        public static StoryFallenPoster Create(MeshFilter source, Vector3[] restVertices, Transform graphics,
            Material material, Vector3 releaseVelocity, StoryPoster poster, Vector3[] worldVertexVelocities = null,StoryPosterInk ink=null)
        {
            var go = new GameObject("FallenPoster");
            SceneManager.MoveGameObjectToScene(go, poster.gameObject.scene);
            go.layer = 2;

            go.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            var fallen = go.AddComponent<StoryFallenPoster>();
            fallen.createdFrame = Time.frameCount;
            Vector3[] released = source.sharedMesh.vertices;
            fallen.cloth = new StoryPaperCloth(source.transform, released, restVertices, worldVertexVelocities, releaseVelocity, poster.transform);
            fallen.mesh = Instantiate(source.sharedMesh);
            fallen.mesh.name = "Paper Mesh";
            fallen.mesh.MarkDynamic();
            fallen.working = new Vector3[released.Length];
            fallen.bindings = new StoryPaperCloth.Binding[released.Length];
            Vector2[] uv = fallen.mesh.uv;
            Matrix4x4 sourceToWorld = source.transform.localToWorldMatrix;
            Matrix4x4 worldToFallen = go.transform.worldToLocalMatrix;
            for (int i = 0; i < released.Length; i++)
            {
                Vector3 exact = sourceToWorld.MultiplyPoint3x4(released[i]);
                fallen.bindings[i] = fallen.cloth.Bind(uv[i].x, uv[i].y, exact);
                Vector3 preserved = fallen.cloth.Evaluate(fallen.bindings[i]);
                fallen.working[i] = worldToFallen.MultiplyPoint3x4(preserved);
                fallen.ReleaseContinuityError = Mathf.Max(fallen.ReleaseContinuityError, Vector3.Distance(exact, preserved));
            }
            fallen.mesh.vertices = fallen.working; fallen.mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = fallen.mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            fallen.PaperRenderer = renderer;
            fallen.properties = new MaterialPropertyBlock();
            var originalRenderer = source.GetComponent<Renderer>();
            if (originalRenderer) originalRenderer.GetPropertyBlock(fallen.properties);
            renderer.SetPropertyBlock(fallen.properties);
            if (graphics) fallen.CopyPrint(graphics,ink);
            return fallen;
        }

        void CopyPrint(Transform graphics,StoryPosterInk ink)
        {

            var copy=new GameObject("PosterInk").transform;
            copy.SetParent(transform,false);copy.SetPositionAndRotation(graphics.position,graphics.rotation);
            copy.gameObject.layer=2;PrintedGraphics=copy;
            foreach(var source in graphics.GetComponentsInChildren<MeshFilter>(true))
            {
                var original=source.GetComponent<MeshRenderer>();
                if(!source.sharedMesh || !original || !original.enabled || !source.gameObject.activeInHierarchy)continue;
                var child=new GameObject(ObjectNames.Short(source.name+" Ink")).transform;
                child.SetParent(copy,false);child.SetPositionAndRotation(source.transform.position,source.transform.rotation);
                child.localScale=source.transform.lossyScale;child.gameObject.layer=2;
                var data=new PrintedMesh {mesh=Instantiate(source.sharedMesh),space=child};
                data.mesh.name=source.sharedMesh.name+" Fibres";data.mesh.MarkDynamic();
                data.vertices=data.mesh.vertices;data.bindings=new StoryPaperCloth.Binding[data.vertices.Length];
                if(source.GetComponent<TextMeshPro>())
                {
                    data.normals=new Vector3[data.vertices.Length];
                    if(data.vertices.Length>0){BoundGlyphMeshCount++;BoundGlyphVertexCount+=data.vertices.Length;}
                }
                Vector3[] coordinates=null;bool known=ink!=null && ink.TryCoordinates(source,out coordinates);
                Matrix4x4 sourceWorld=source.transform.localToWorldMatrix,inverse=child.worldToLocalMatrix;
                for(int i=0;i<data.vertices.Length;i++)
                {
                    Vector3 exact=sourceWorld.MultiplyPoint3x4(data.vertices[i]);
                    data.bindings[i]=known?cloth.Bind(coordinates[i].x,coordinates[i].y,exact):cloth.BindNearest(exact);
                    data.vertices[i]=inverse.MultiplyPoint3x4(exact);
                    PrintContinuityError=Mathf.Max(PrintContinuityError,Vector3.Distance(exact,cloth.Evaluate(data.bindings[i],1,true)));
                }
                data.mesh.vertices=data.vertices;data.mesh.RecalculateBounds();
                child.gameObject.AddComponent<MeshFilter>().sharedMesh=data.mesh;
                var renderer=child.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterials=original.sharedMaterials;
                var block=new MaterialPropertyBlock();original.GetPropertyBlock(block);renderer.SetPropertyBlock(block);
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                BoundPrintVertexCount+=data.vertices.Length;printedMeshes.Add(data);
            }
            foreach(var source in graphics.GetComponentsInChildren<LineRenderer>(true))
            {
                if(!source.enabled || !source.gameObject.activeInHierarchy)continue;
                var child=new GameObject(ObjectNames.Short(source.name+" Ink")).transform;
                child.SetParent(copy,false);child.SetPositionAndRotation(source.transform.position,source.transform.rotation);
                child.localScale=source.transform.lossyScale;child.gameObject.layer=2;
                var line=child.gameObject.AddComponent<LineRenderer>();
                line.sharedMaterials=source.sharedMaterials;line.useWorldSpace=source.useWorldSpace;
                line.widthCurve=source.widthCurve;line.widthMultiplier=source.widthMultiplier;
                line.colorGradient=source.colorGradient;line.loop=source.loop;line.alignment=source.alignment;
                line.textureMode=source.textureMode;line.numCornerVertices=source.numCornerVertices;line.numCapVertices=source.numCapVertices;
                line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;line.positionCount=source.positionCount;
                var data=new PrintedLine {line=line,vertices=new Vector3[source.positionCount],bindings=new StoryPaperCloth.Binding[source.positionCount]};
                source.GetPositions(data.vertices);
                Vector3[] coordinates=null;bool known=ink!=null && ink.TryCoordinates(source,out coordinates);
                for(int i=0;i<data.vertices.Length;i++)
                {
                    Vector3 exact=source.useWorldSpace?data.vertices[i]:source.transform.TransformPoint(data.vertices[i]);
                    data.bindings[i]=known?cloth.Bind(coordinates[i].x,coordinates[i].y,exact):cloth.BindNearest(exact);
                    data.vertices[i]=line.useWorldSpace?exact:line.transform.InverseTransformPoint(exact);
                }
                line.SetPositions(data.vertices);BoundPrintVertexCount+=data.vertices.Length;printedLines.Add(data);
            }
        }
        void Update()
        {

            if (Time.frameCount == createdFrame) return;
            StepSimulation(Time.deltaTime);
        }

        public void StepSimulation(float deltaTime)
        {
            if(cloth==null || HasSettled || !float.IsFinite(deltaTime) || deltaTime<=0)return;
            age += deltaTime;
            accumulator = Mathf.Min(accumulator + deltaTime, StoryPaperCloth.StepDuration * 4);
            while (accumulator >= StoryPaperCloth.StepDuration && !cloth.HasSettled)
            {
                cloth.Step(); accumulator -= StoryPaperCloth.StepDuration;
            }
            Draw();
            if (HasSettled) enabled = false;
            else if (age > 12 && !FoundLandingSurface) Destroy(gameObject);
        }

        void Draw()
        {
            float residual = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / 1.5f));
            Matrix4x4 local = transform.worldToLocalMatrix;
            for (int i = 0; i < working.Length; i++) working[i] = local.MultiplyPoint3x4(cloth.Evaluate(bindings[i], residual));
            mesh.vertices = working; mesh.RecalculateBounds();
            properties.SetFloat("_Peel", 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(age / 1.1f)));
            PaperRenderer.SetPropertyBlock(properties);
            for (int m = 0; m < printedMeshes.Count; m++)
            {
                var data = printedMeshes[m]; Matrix4x4 inverse = data.space.worldToLocalMatrix;
                Matrix4x4 normalToLocal=data.space.localToWorldMatrix.transpose;
                for (int i = 0; i < data.vertices.Length; i++)
                {
                    data.vertices[i] = inverse.MultiplyPoint3x4(cloth.Evaluate(data.bindings[i], 1, true));
                    if(data.normals!=null)data.normals[i]=normalToLocal.MultiplyVector(cloth.EvaluateFrontNormal(data.bindings[i])).normalized;
                }
                data.mesh.vertices = data.vertices;if(data.normals!=null)data.mesh.normals=data.normals;data.mesh.RecalculateBounds();
            }
            for (int m = 0; m < printedLines.Count; m++)
            {
                var data = printedLines[m]; Matrix4x4 inverse = data.line.transform.worldToLocalMatrix;
                for (int i = 0; i < data.vertices.Length; i++)
                {
                    Vector3 world = cloth.Evaluate(data.bindings[i], 1, true);
                    data.vertices[i] = data.line.useWorldSpace ? world : inverse.MultiplyPoint3x4(world);
                }
                data.line.SetPositions(data.vertices);
            }
            for (int i = 0; i < printedTransforms.Count; i++)
            {
                var data = printedTransforms[i]; Vector3 center = cloth.Evaluate(data.center, 1, true);
                Vector3 right = cloth.Evaluate(data.right, 1, true) - center, up = cloth.Evaluate(data.up, 1, true) - center;
                Vector3 normal = Vector3.Cross(right, up);
                if (normal.sqrMagnitude > .00001f) data.target.SetPositionAndRotation(center, Quaternion.LookRotation(normal, up));
            }
        }

        public void CopyWorldVertices(Vector3[] destination)
        {
            if (destination == null || destination.Length < working.Length) return;
            Matrix4x4 world = transform.localToWorldMatrix;
            for (int i = 0; i < working.Length; i++) destination[i] = world.MultiplyPoint3x4(working[i]);
        }

        void OnDestroy()
        {
            if (mesh) Destroy(mesh);
            for (int i = 0; i < printedMeshes.Count; i++) if (printedMeshes[i].mesh) Destroy(printedMeshes[i].mesh);
        }
    }
}

