using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RealityPlayground
{

    [ExecuteAlways]
    public sealed class BleakMirror : MonoBehaviour
    {
        [SerializeField] Renderer mirrorSurface;
        [SerializeField] LiquidMirror liquidMirror;
        [SerializeField, Range(256, 1536)] int reflectionResolution = 768;
        [SerializeField, Range(0, 1)] float desaturation = .985f;
        [SerializeField, Range(.2f, 1.2f)] float exposure = .59f;
        [SerializeField] float clipOffset = .025f;
        [Tooltip("Layers visible in the mirror even when hidden by the viewer camera. Layer 30 is the first-person head proxy.")]
        [SerializeField] LayerMask additionalReflectionLayers = 1 << 30;
        [SerializeField] LayerMask hiddenLayers;
        Camera reflectionCamera;
        RenderTexture leftTexture, rightTexture;
        MaterialPropertyBlock properties;
        static bool renderingReflection;
        public int HiddenMask => hiddenLayers.value;

        public void SetView(float saturation, float brightness, int hidden)
        {
            desaturation = 1 - Mathf.Clamp01(saturation);
            exposure = Mathf.Clamp(brightness, .2f, 1.2f);
            hiddenLayers = hidden;
        }

        void OnEnable() => RenderPipelineManager.beginCameraRendering += RenderReflection;

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= RenderReflection;
            Release(leftTexture); Release(rightTexture);
            leftTexture = rightTexture = null;
            if (reflectionCamera) Release(reflectionCamera.gameObject);
        }

        static void Release(Object value)
        {
            if (!value) return;
            if (value is RenderTexture texture) texture.Release();
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        void PrepareResources()
        {
            if (!reflectionCamera)
            {
                var cameraObject = new GameObject("ReflectionCamera") { hideFlags = HideFlags.HideAndDontSave };
                reflectionCamera = cameraObject.AddComponent<Camera>();
                reflectionCamera.enabled = false;
                var cameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
                cameraData.allowXRRendering = false;
                cameraData.renderPostProcessing = false;
                cameraData.renderShadows = true;
                cameraData.requiresColorOption = CameraOverrideOption.Off;
                cameraData.requiresDepthOption = CameraOverrideOption.Off;
            }
            if (leftTexture && leftTexture.width == reflectionResolution) return;
            Release(leftTexture); Release(rightTexture);
            leftTexture = MakeTexture("Mirror left eye");
            rightTexture = MakeTexture("Mirror right eye");
            properties ??= new MaterialPropertyBlock();
        }

        RenderTexture MakeTexture(string label)
        {
            var texture = new RenderTexture(reflectionResolution, reflectionResolution, 24, RenderTextureFormat.ARGBHalf)
            {
                name = label,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                antiAliasing = 1
            };
            texture.Create();
            return texture;
        }

        void RenderReflection(ScriptableRenderContext context, Camera source)
        {
            if (renderingReflection || !mirrorSurface || !mirrorSurface.enabled || !isActiveAndEnabled) return;
            if (source.cameraType != CameraType.Game && source.cameraType != CameraType.SceneView) return;
            var point = mirrorSurface.transform.position;
            var normal = -mirrorSurface.transform.forward;
            if (Vector3.Dot(normal, source.transform.position - point) < .035f) return;
            if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(source), mirrorSurface.bounds)) return;
            PrepareResources();
            renderingReflection = true;
            bool previousCulling = GL.invertCulling;
            bool previousOff = mirrorSurface.forceRenderingOff;
            try
            {
                mirrorSurface.forceRenderingOff = true;
                if (liquidMirror) liquidMirror.HideFromReflection(true);
                GL.invertCulling = !previousCulling;
                var leftVP = RenderEye(source, Camera.StereoscopicEye.Left, leftTexture, point, normal);
                var rightVP = source.stereoEnabled
                    ? RenderEye(source, Camera.StereoscopicEye.Right, rightTexture, point, normal)
                    : leftVP;
                properties.SetTexture("_ReflectionLeft", leftTexture);
                properties.SetTexture("_ReflectionRight", source.stereoEnabled ? rightTexture : leftTexture);
                properties.SetMatrix("_ReflectionVPLeft", leftVP);
                properties.SetMatrix("_ReflectionVPRight", rightVP);
                properties.SetFloat("_Desaturation", desaturation);
                properties.SetFloat("_Exposure", exposure);
                properties.SetFloat("_HasReflection", 1);
                properties.SetFloat("_MultipassEye", source.stereoActiveEye == Camera.MonoOrStereoscopicEye.Right ? 1 : 0);
                if (liquidMirror) liquidMirror.ApplyReflectionProperties(properties);
                mirrorSurface.SetPropertyBlock(properties);
            }
            finally
            {
                mirrorSurface.forceRenderingOff = previousOff;
                if (liquidMirror) liquidMirror.HideFromReflection(false);
                GL.invertCulling = previousCulling;
                renderingReflection = false;
            }
        }

        Matrix4x4 RenderEye(Camera source, Camera.StereoscopicEye eye, RenderTexture target, Vector3 point, Vector3 normal)
        {
            reflectionCamera.CopyFrom(source);
            reflectionCamera.enabled = false;
            reflectionCamera.cameraType = CameraType.Reflection;

            reflectionCamera.GetUniversalAdditionalCameraData().allowXRRendering = false;
            reflectionCamera.cullingMask = (source.cullingMask | additionalReflectionLayers.value) & ~hiddenLayers.value;
            reflectionCamera.targetTexture = target;
            reflectionCamera.useOcclusionCulling = false;
            reflectionCamera.allowMSAA = false;
            reflectionCamera.rect = new Rect(0, 0, 1, 1);
            var view = source.stereoEnabled ? source.GetStereoViewMatrix(eye) : source.worldToCameraMatrix;
            var projection = source.stereoEnabled ? source.GetStereoProjectionMatrix(eye) : source.projectionMatrix;
            var plane = new Vector4(normal.x, normal.y, normal.z, -Vector3.Dot(normal, point));
            Matrix4x4 reflect = ReflectionMatrix(plane);
            var eyePosition = view.inverse.MultiplyPoint(Vector3.zero);
            reflectionCamera.transform.position = reflect.MultiplyPoint(eyePosition);
            reflectionCamera.transform.rotation = Quaternion.LookRotation(
                reflect.MultiplyVector(source.transform.forward), reflect.MultiplyVector(source.transform.up));
            reflectionCamera.worldToCameraMatrix = view * reflect;
            reflectionCamera.projectionMatrix = projection;
            var clipPoint = reflectionCamera.worldToCameraMatrix.MultiplyPoint(point + normal * clipOffset);
            var clipNormal = reflectionCamera.worldToCameraMatrix.MultiplyVector(normal).normalized;
            var clipPlane = new Vector4(clipNormal.x, clipNormal.y, clipNormal.z, -Vector3.Dot(clipPoint, clipNormal));
            reflectionCamera.projectionMatrix = reflectionCamera.CalculateObliqueMatrix(clipPlane);
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            if (RenderPipeline.SupportsRenderRequest(reflectionCamera, request))
                RenderPipeline.SubmitRenderRequest(reflectionCamera, request);
            return GL.GetGPUProjectionMatrix(reflectionCamera.projectionMatrix, true) * reflectionCamera.worldToCameraMatrix;
        }

        static Matrix4x4 ReflectionMatrix(Vector4 p)
        {
            var result = Matrix4x4.identity;
            result.m00 = 1 - 2 * p.x * p.x; result.m01 = -2 * p.x * p.y; result.m02 = -2 * p.x * p.z; result.m03 = -2 * p.w * p.x;
            result.m10 = -2 * p.y * p.x; result.m11 = 1 - 2 * p.y * p.y; result.m12 = -2 * p.y * p.z; result.m13 = -2 * p.w * p.y;
            result.m20 = -2 * p.z * p.x; result.m21 = -2 * p.z * p.y; result.m22 = 1 - 2 * p.z * p.z; result.m23 = -2 * p.w * p.z;
            return result;
        }

        public static GameObject Create(Transform parent)
        {
            var root = new GameObject("Liquidbleakmirror");
            root.transform.SetParent(parent, false);
            var frame = GreyboxUtil.Material("Mirror graphite frame", new Color(.065f, .07f, .083f));
            var trim = GreyboxUtil.Material("Mirror oxblood edge", new Color(.35f, .035f, .065f), .6f);
            var shader = Shader.Find("RealityPlayground/BleakPlanarMirror");
            var material = shader ? new Material(shader) { name = "Bleak mirror surface" } : GreyboxUtil.Material("Mirror shader fallback", new Color(.2f, .23f, .25f));
            var liquid = LiquidMirror.Create(root.transform, material, out var surface);
            for (int side = -1; side <= 1; side += 2)
            {
                GreyboxUtil.Primitive("Vertical frame", PrimitiveType.Cube, root.transform, new Vector3(side * 1.31f, 1.55f, -.03f), new Vector3(.16f, 3.05f, .19f), frame);
                GreyboxUtil.Primitive("Red seam", PrimitiveType.Cube, root.transform, new Vector3(side * 1.215f, 1.55f, -.055f), new Vector3(.018f, 2.73f, .035f), trim, false);
                GreyboxUtil.Primitive("Horizontal frame", PrimitiveType.Cube, root.transform, new Vector3(0, 1.55f + side * 1.46f, -.03f), new Vector3(2.8f, .14f, .19f), frame);
            }
            GreyboxUtil.Label("Touch the mirror. Pull back slowly, then farther.", root.transform, new Vector3(0, 3.25f, -.08f), .055f, new Color(.78f, .8f, .85f));
            var behaviour = root.AddComponent<BleakMirror>();
            behaviour.mirrorSurface = surface;
            behaviour.liquidMirror = liquid;
            return root;
        }
    }
}
