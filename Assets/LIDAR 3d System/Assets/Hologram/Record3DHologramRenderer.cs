using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

/// <summary>
/// Renders the frames decoded by <see cref="Record3DPlayback"/> as a point-cloud hologram: one
/// camera-facing quad per sampled texel, drawn in a single draw call by Record3DHologram.shader.
///
/// The original VFX Graph spawned particles at random texels, kept each one alive for ~0.5 s while
/// curl noise pushed it around, and drew them as additive HDR sparkles. Here every sample is drawn
/// exactly where and when it was recorded, sized relative to the gap between samples and clamped in
/// screen pixels, so the hologram stays solid at any viewing distance.
/// </summary>
[RequireComponent(typeof(Record3DPlayback))]
public class Record3DHologramRenderer : MonoBehaviour
{
    public enum SizeMode { RelativeToSampleSpacing, FixedLocalSize }
    public enum HologramBlendMode { Opaque, AlphaBlended, Additive }
    public enum ParticleShape { Square, Circle, Soft }

    [SerializeField] private Record3DPlayback playback;
    [SerializeField] private Shader shader;

    [Header("Depth Clipping (metres from the recording iPhone)")]
    [Tooltip("Hide samples closer to the recording camera than this. Measured along the recording camera's view axis, like the depth range in the Record3D app, so it does not depend on where the player is or how the prefab is placed, rotated or scaled.")]
    [Min(0f)] public float nearDepth = 0f;
    [Tooltip("Hide samples further from the recording camera than this. iPhone LiDAR is only reliable up to about 5 m.")]
    [Min(0f)] public float farDepth = 5f;

    [Header("Density")]
    [Tooltip("Draw every Nth texel in each direction. Recordings decode to 720x960 colour pixels but only 192x256 real LiDAR depth samples. 1 = 691,200 points (full colour detail), 2 = 172,800, 3 = 76,800, 4 = 43,200 (about the native depth resolution).")]
    [Range(1, 8)] public int sampleStride = 2;
    [Tooltip("Used instead of Sample Stride in Android (Meta Quest) builds.")]
    [Range(1, 8)] public int androidSampleStride = 4;

    [Header("Particle Size")]
    public SizeMode sizeMode = SizeMode.RelativeToSampleSpacing;
    [Tooltip("Relative To Sample Spacing: 1 makes neighbouring particles just touch at every depth, so surfaces have no gaps. Fixed Local Size: edge length in the prefab's local units (metres before the prefab's scale).")]
    [Min(0f)] public float particleSize = 1.25f;
    [Tooltip("Particles are never drawn smaller than this many pixels, so a distant hologram stays solid instead of breaking up into faint sub-pixel dots.")]
    [Min(0f)] public float minScreenSize = 1.5f;
    [Tooltip("Particles are never drawn larger than this many pixels, so samples close to the viewer do not become large squares.")]
    [Min(0f)] public float maxScreenSize = 32f;

    [Header("Appearance")]
    [Tooltip("Opaque: sharpest and cheapest, ignores Opacity. Alpha Blended: supports Opacity. Additive: glowing see-through look, brightens where particles overlap.")]
    public HologramBlendMode blendMode = HologramBlendMode.AlphaBlended;
    [Tooltip("Alpha Blended only: write depth so the nearest surface hides what is behind it. Turn off for a see-through look.")]
    public bool writeDepth = true;
    public ParticleShape shape = ParticleShape.Square;
    [Range(0f, 1f)] public float opacity = 1f;
    [Tooltip("Multiplies the colours. White keeps the recorded colours unchanged.")]
    public Color hologramColor = Color.white;
    [Tooltip("0 = recorded colours, 1 = brightness only, so the hologram takes on Hologram Color.")]
    [Range(0f, 1f)] public float monochrome = 0f;
    [Tooltip("Pushes colours above 1.0 so URP Bloom (threshold 1 in this project's volumes) makes the hologram glow. 0 = no extra bloom, sharpest. Bloom is expensive on Quest.")]
    [Min(0f)] public float glowIntensity = 0f;

    [Header("Cleanup")]
    [Tooltip("Hide samples whose depth differs from a neighbouring depth sample by more than this fraction of their own depth. Removes the smeared 'flying pixels' LiDAR produces along silhouettes, at the cost of a thin border. 0 = off; try 0.05-0.1.")]
    [Range(0f, 0.5f)] public float edgeFilter = 0f;
    [Tooltip("Record3D positions are right-handed and Unity is left-handed, so the hologram is a mirror image of the real scene. Enable to flip it the right way round (mirrors across the recording camera's vertical plane).")]
    public bool correctMirroring = false;
    [Tooltip("Disable the original VFX Graph effect on this object while this component is active. Disable this component to compare with the original look.")]
    public bool hideOriginalVfx = true;

    /// <summary>Number of particles drawn per frame at the current stride (before clipping).</summary>
    public int pointCount { get; private set; }

    private static readonly int PositionTexId = Shader.PropertyToID("_PositionTex");
    private static readonly int ColorTexId = Shader.PropertyToID("_ColorTex");
    private static readonly int DepthRangeId = Shader.PropertyToID("_DepthRange");
    private static readonly int SizeParamsId = Shader.PropertyToID("_SizeParams");
    private static readonly int ScreenSizeLimitsId = Shader.PropertyToID("_ScreenSizeLimits");
    private static readonly int SampleParamsId = Shader.PropertyToID("_SampleParams");
    private static readonly int HologramColorId = Shader.PropertyToID("_HologramColor");
    private static readonly int ColorParamsId = Shader.PropertyToID("_ColorParams");
    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");

    private Material material_;
    private Mesh mesh_;
    private int meshWidth_, meshHeight_, meshStride_;
    private VisualEffect originalVfx_;
    private bool originalVfxWasEnabled_;

    private void OnEnable()
    {
        if (playback == null)
        {
            playback = GetComponent<Record3DPlayback>();
        }
        if (shader == null)
        {
            shader = Shader.Find("Record3D/Hologram Points");
        }
        if (shader == null || !shader.isSupported)
        {
            Debug.LogError("Record3DHologramRenderer: the 'Record3D/Hologram Points' shader is missing or not supported on this platform.", this);
            enabled = false;
            return;
        }

        material_ = new Material(shader) { name = "Record3D Hologram (runtime)", hideFlags = HideFlags.DontSave };

        originalVfx_ = playback.streamEffect != null ? playback.streamEffect : GetComponent<VisualEffect>();
        if (originalVfx_ != null)
        {
            originalVfxWasEnabled_ = originalVfx_.enabled;
        }
    }

    private void OnDisable()
    {
        if (originalVfx_ != null)
        {
            originalVfx_.enabled = originalVfxWasEnabled_;
        }
        if (material_ != null) Destroy(material_);
        if (mesh_ != null) Destroy(mesh_);
        material_ = null;
        mesh_ = null;
    }

    private void LateUpdate()
    {
        if (originalVfx_ != null)
        {
            originalVfx_.enabled = !hideOriginalVfx && originalVfxWasEnabled_;
        }

        var video = playback.video;
        var positions = playback.positionTexture;
        var colors = playback.colorTexture;
        if (material_ == null || video == null || positions == null || colors == null)
        {
            return;
        }

        int stride = Mathf.Max(1, Application.platform == RuntimePlatform.Android ? androidSampleStride : sampleStride);
        EnsureMesh(positions.width, positions.height, stride);

        float near = Mathf.Max(0f, nearDepth);
        float far = Mathf.Max(near, farDepth);
        UpdateMaterial(video, positions, colors, stride, near, far);

        var localToWorld = transform.localToWorldMatrix;
        var renderParams = new RenderParams(material_)
        {
            layer = gameObject.layer,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            lightProbeUsage = LightProbeUsage.Off,
            reflectionProbeUsage = ReflectionProbeUsage.Off,
            motionVectorMode = MotionVectorGenerationMode.Camera,
            worldBounds = Record3DPointMesh.Transform(ComputeLocalBounds(video, near, far), localToWorld),
        };
        Graphics.RenderMesh(renderParams, mesh_, 0, localToWorld);
    }

    private void UpdateMaterial(Record3DVideo video, Texture2D positions, Texture2D colors, int stride, float near, float far)
    {
        material_.SetTexture(PositionTexId, positions);
        material_.SetTexture(ColorTexId, colors);
        material_.SetVector(DepthRangeId, new Vector4(near, far, 0f, 0f));

        // Neighbouring samples at depth d are d * stride / f apart (pinhole camera).
        float focalLength = Mathf.Max(0.5f * (video.fx + video.fy), 1e-3f);
        var scale = transform.lossyScale;
        float objectScale = (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3f;
        material_.SetVector(SizeParamsId, new Vector4(particleSize, sizeMode == SizeMode.FixedLocalSize ? 1f : 0f, stride / focalLength, objectScale));

        float minPixels = Mathf.Max(0f, minScreenSize);
        material_.SetVector(ScreenSizeLimitsId, new Vector4(minPixels, Mathf.Max(minPixels, maxScreenSize), 0f, 0f));

        // Depth is upsampled to the colour resolution with nearest-neighbour, so compare against the
        // next real depth sample rather than the next texel.
        int edgeOffset = video.depthWidth > 0 ? Mathf.CeilToInt((float)video.width / video.depthWidth) : 1;
        material_.SetVector(SampleParamsId, new Vector4(edgeOffset, edgeFilter, correctMirroring ? -1f : 1f, 0f));

        material_.SetColor(HologramColorId, hologramColor);
        material_.SetVector(ColorParamsId, new Vector4(monochrome, glowIntensity, opacity, 0f));

        BlendMode src, dst;
        bool zWrite;
        int queue;
        switch (blendMode)
        {
            case HologramBlendMode.Opaque:
                src = BlendMode.One;
                dst = BlendMode.Zero;
                zWrite = true;
                queue = shape == ParticleShape.Square ? (int)RenderQueue.Geometry : (int)RenderQueue.AlphaTest;
                break;
            case HologramBlendMode.Additive:
                src = BlendMode.SrcAlpha;
                dst = BlendMode.One;
                zWrite = false;
                queue = (int)RenderQueue.Transparent;
                break;
            default:
                src = BlendMode.SrcAlpha;
                dst = BlendMode.OneMinusSrcAlpha;
                zWrite = writeDepth;
                queue = (int)RenderQueue.Transparent;
                break;
        }
        material_.SetFloat(SrcBlendId, (float)src);
        material_.SetFloat(DstBlendId, (float)dst);
        material_.SetFloat(ZWriteId, zWrite ? 1f : 0f);
        material_.renderQueue = queue;

        SetKeyword("_SHAPE_CIRCLE", shape == ParticleShape.Circle);
        SetKeyword("_SHAPE_SOFT", shape == ParticleShape.Soft);
    }

    private void SetKeyword(string keyword, bool on)
    {
        if (on) material_.EnableKeyword(keyword);
        else material_.DisableKeyword(keyword);
    }

    private void EnsureMesh(int width, int height, int stride)
    {
        if (mesh_ != null && meshWidth_ == width && meshHeight_ == height && meshStride_ == stride)
        {
            return;
        }
        if (mesh_ != null) Destroy(mesh_);

        int columns = (width + stride - 1) / stride;
        int rows = (height + stride - 1) / stride;
        mesh_ = Record3DPointMesh.Build(width, height, columns, rows, stride, stride);
        meshWidth_ = width;
        meshHeight_ = height;
        meshStride_ = stride;
        pointCount = columns * rows;
    }

    private Bounds ComputeLocalBounds(Record3DVideo video, float near, float far)
    {
        var bounds = Record3DPointMesh.FrustumBounds(video, near, far);
        bounds.Expand(0.1f * bounds.size.magnitude + (sizeMode == SizeMode.FixedLocalSize ? particleSize : 0f));
        return bounds;
    }
}
