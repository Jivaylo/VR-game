using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Garden
{
    public sealed class RigQuality : MonoBehaviour
    {
        [Range(1f, 1.5f)] public float renderScale = 1.25f;
        RenderPipelineAsset previousPipeline;
        UniversalRenderPipelineAsset pipeline;

        void Awake()
        {
            foreach (var camera in GetComponentsInChildren<Camera>(true)) Configure(camera);
            if (Application.platform != RuntimePlatform.Android) return;
            previousPipeline = QualitySettings.renderPipeline;
            var source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (!source) return;
            pipeline = Instantiate(source);
            pipeline.name = "Garden Pipeline";
            pipeline.renderScale = renderScale;
            pipeline.msaaSampleCount = 4;
            pipeline.supportsHDR = false;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            QualitySettings.renderPipeline = pipeline;
        }

        public static void Configure(Camera camera)
        {
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 190f;
            camera.allowHDR = false;
            camera.allowMSAA = true;
            camera.allowDynamicResolution = false;
            if (camera.TryGetComponent<UniversalAdditionalCameraData>(out var data))
            {
                data.renderPostProcessing = false;
                data.antialiasing = AntialiasingMode.None;
                data.requiresColorOption = CameraOverrideOption.Off;
                data.requiresDepthOption = CameraOverrideOption.Off;
            }
        }

        void OnDestroy()
        {
            if (!pipeline) return;
            if (QualitySettings.renderPipeline == pipeline) QualitySettings.renderPipeline = previousPipeline;
            Destroy(pipeline);
        }
    }
}
