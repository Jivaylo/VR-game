using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

/// <summary>
/// Turns the Record3D point cloud into a configurable sci-fi hologram: luminance-mapped monochrome colour,
/// translucency, local-space scanlines, glitches and glow, plus an unstable "being reconstructed" quality
/// (vapour drift, silhouette breakup, motion lag/trails, mist) and a projector beaming it up from below.
/// Everything is set from the Inspector and live in Play Mode.
/// Draws the textures decoded by <see cref="Record3DPlayback"/> with VolumetricHologram.shader and
/// HologramProjector.shader. While enabled it stands in for <see cref="Record3DHologramRenderer"/> and the
/// original VFX Graph; disable it to get the previous look back.
/// </summary>
[RequireComponent(typeof(Record3DPlayback))]
public class VolumetricHologramController : MonoBehaviour
{
    public enum Preset { Custom, ClassicSciFi, GhostlyProjection, UnstableTransmission, CleanDigitalProjection }
    public enum Blending { Alpha, Additive, Hybrid, Opaque }
    public enum Sorting { FrontSurfaceOnly, WriteDepth, NoDepthWrite }
    public enum ParticleShape { Square, Round }

    [Serializable]
    public class AppearanceSettings
    {
        [Tooltip("Hue of the hologram. Each particle's brightness in the original recording picks a darker or lighter shade of it. HDR intensity above 1 makes the whole hologram brighter.")]
        [ColorUsage(false, true)] public Color hologramColor = new Color(0.3f, 0.9f, 1f);
        [Tooltip("How strongly the hue is applied: 0 = grey (brightness only), 1 = fully coloured.")]
        [Range(0f, 1f)] public float colorIntensity = 1f;
        [Tooltip("Shifts every particle towards the bright or dark end of the colour ramp.")]
        [Range(-0.5f, 0.5f)] public float brightness = 0.05f;
        [Tooltip("Spreads (above 1) or compresses (below 1) the difference between dark and bright areas.")]
        [Range(0.25f, 3f)] public float contrast = 1.1f;
        [Tooltip("Stretches the subject's own brightness range (measured each video frame, smoothed) over the whole colour ramp, so dark or backlit recordings still show their detail. 0 = use the recording's brightness as is.")]
        [Range(0f, 1f)] public float autoLevels = 1f;
        [Tooltip("Lightness of the darkest areas (hair, dark clothing), so shadows stay visible as deep shades of the hue.")]
        [Range(0f, 1f)] public float shadowBrightness = 0.3f;
        [Tooltip("Brightness multiplier for highlights, which also turn paler (towards white).")]
        [Range(0.5f, 4f)] public float highlightIntensity = 1.2f;
        [Tooltip("How much the recording's brightness drives the ramp: 1 = full facial and clothing detail, 0 = one flat shade.")]
        [Range(0f, 1f)] public float luminanceInfluence = 1f;
    }

    [Serializable]
    public class TransparencySettings
    {
        [Tooltip("Overall opacity of the hologram (also the particle opacity).")]
        [Range(0f, 1f)] public float opacity = 0.9f;
        [Tooltip("Opacity of the darkest areas, relative to Opacity. Lower = dark areas become see-through.")]
        [Range(0f, 1f)] public float shadowOpacity = 0.35f;
        [Tooltip("Opacity of the brightest areas, relative to Opacity.")]
        [Range(0f, 1f)] public float highlightOpacity = 1f;
        [Tooltip("How sharply opacity changes between shadows and highlights.")]
        [Range(0.25f, 4f)] public float opacityContrast = 1.2f;
    }

    [Serializable]
    public class GlowSettings
    {
        [Tooltip("Emission: glow that grows with each particle's brightness, so bright areas glow more than dark ones. Values above 1.0 on screen are picked up by the scene's existing URP Bloom; nothing is added to the scene's post-processing.")]
        [Range(0f, 3f)] public float glowIntensity = 0.6f;
        [Tooltip("Extra glow only for areas brighter than Glow Threshold.")]
        [Range(0f, 4f)] public float highlightGlow = 1f;
        [Tooltip("Brightness (0-1 on the colour ramp) above which Highlight Glow starts. Note: the scene's ACES tonemapping already turns very bright areas white, so glow there mostly shows through Bloom.")]
        [Range(0f, 0.95f)] public float glowThreshold = 0.5f;
        [Tooltip("0 = glow is white, 1 = glow takes the hologram colour.")]
        [Range(0f, 1f)] public float emissionColorInfluence = 0.85f;
    }

    [Serializable]
    public class ParticleSettings
    {
        [Tooltip("Particle size as a multiple of the gap between neighbouring samples; 1 = neighbours just touch at any depth.")]
        [Range(0.25f, 3f)] public float size = 1.15f;
        [Tooltip("Draw every Nth sample in each direction (1 = densest). See Real Depth Samples Only for what a sample is.")]
        [Range(1, 8)] public int density = 2;
        [Tooltip("On: one particle per real LiDAR depth sample (192x256 = 49,152 for this recording). Off: particles follow the 720x960 colour pixels, which share upsampled depth but carry more colour detail (Density 2 = 172,800).")]
        public bool realDepthSamplesOnly = false;
        [Tooltip("1 = hard-edged particles, lower = edges fade out (softer, slightly blurrier). Applies with Front Surface Only and No Depth Write sorting; Write Depth and Opaque always use hard edges, because soft edges would leave seams there.")]
        [Range(0f, 1f)] public float sharpness = 0.8f;
        public ParticleShape shape = ParticleShape.Square;
        [Tooltip("Keep particles between Min and Max Screen Size in pixels, so the hologram stays solid far away without huge particles up close. Off = pure world-space size.")]
        public bool distanceCompensation = true;
        [Min(0f)] public float minScreenSize = 1.5f;
        [Min(0f)] public float maxScreenSize = 32f;
        [Tooltip("Random per-particle shimmer. 0 = off.")]
        [Range(0f, 1f)] public float flicker = 0.05f;
    }

    [Serializable]
    public class InstabilitySettings
    {
        [Tooltip("Overall hologram instability: scales drift, edge drift and dissolve, lag, trails, overshoot, jitter, mist, glitches and flicker together. 0 = perfectly stable, 1 = as set below.")]
        [Range(0f, 2f)] public float overall = 1f;
        [Tooltip("How far particles float around their recorded position, like holographic vapour, in local units (metres before the prefab's scale). The core only wobbles within the gap between particles so it never opens holes; loose particles float further. The silhouette is set under Edges.")]
        [Range(0f, 0.03f)] public float drift = 0.004f;
        [Tooltip("Speed of the drifting motion.")]
        [Range(0f, 3f)] public float driftSpeed = 0.7f;
        [Tooltip("Share of particles in moving areas that leave a copy lagging behind the motion, as if the projection were catching up. The body itself stays in place, so it stays readable.")]
        [Range(0f, 1f)] public float lag = 0.25f;
        [Tooltip("How far behind lagging particles fall, in seconds of the subject's motion.")]
        [Range(0f, 0.5f)] public float lagTime = 0.08f;
        [Tooltip("Share of particles in moving areas that leave a light streak along the motion.")]
        [Range(0f, 1f)] public float trails = 0.15f;
        [Tooltip("Length of the trails, in seconds of the subject's motion.")]
        [Range(0f, 0.5f)] public float trailLength = 0.1f;
        [Tooltip("Share of particles in moving areas that leave a copy running ahead of the motion.")]
        [Range(0f, 1f)] public float overshoot = 0.08f;
        [Tooltip("Random jitter of moving parts. Parts that are not moving stay calm.")]
        [Range(0f, 1f)] public float jitter = 0.3f;
    }

    [Serializable]
    public class EdgeSettings
    {
        [Tooltip("How far in from the silhouette the soft, unstable edge reaches, in local units. Inside it the form stays coherent.")]
        [Range(0.005f, 0.15f)] public float width = 0.04f;
        [Tooltip("Transparency falloff towards the silhouette, so the form fades out like projected light instead of ending in a hard outline.")]
        [Range(0f, 1f)] public float fade = 0.55f;
        [Tooltip("How far edge particles float outward from the form and hover there, in local units.")]
        [Range(0f, 0.05f)] public float outwardDrift = 0.008f;
        [Tooltip("Speed of the slow breathing/hovering motion of the edge particles.")]
        [Range(0f, 2f)] public float hoverSpeed = 0.35f;
        [Tooltip("Share of the outermost particles that detach and float away as loose light, so the outline dissolves.")]
        [Range(0f, 1f)] public float dissolve = 0.35f;
        [Tooltip("How much edge particles turn from crisp squares into larger, fainter round glows that blend into haze.")]
        [Range(0f, 1f)] public float softness = 0.8f;
    }

    [Serializable]
    public class MistSettings
    {
        [Tooltip("Share of particles that also shed a loose mist/debris particle (mostly from the silhouette), which floats outward and upward and fades. Mist, lag copies and trails are drawn as an extra additive layer, so the body itself stays intact.")]
        [Range(0f, 1f)] public float amount = 0.12f;
        [Tooltip("How far mist floats before fading, in local units.")]
        [Range(0f, 0.3f)] public float distance = 0.06f;
        [Range(0f, 1f)] public float opacity = 0.3f;
        [Tooltip("Mist particle size relative to body particles. Larger, fainter puffs read as vapour; small ones as specks.")]
        [Range(0.1f, 4f)] public float size = 1.6f;
        [Tooltip("Upward pull on the mist, as if carried up the projection.")]
        [Range(0f, 2f)] public float rise = 0.8f;
        [Tooltip("How much of the mist comes from the silhouette (1) rather than evenly from the whole form (0).")]
        [Range(0f, 1f)] public float edgeBias = 0.85f;
    }

    [Serializable]
    public class ProjectorSettings
    {
        [Tooltip("Draw the projector under the hologram and run the assembly sweep.")]
        public bool enabled = true;
        [Tooltip("Brightness of the glowing emitter disc under the hologram.")]
        [Range(0f, 5f)] public float baseGlow = 1.5f;
        [Tooltip("Brightness of the light cone rising from the emitter.")]
        [Range(0f, 3f)] public float coneIntensity = 0.45f;
        [Tooltip("Number of laser-like beams inside the cone.")]
        [Range(0, 48)] public int beamCount = 12;
        [Tooltip("Brightness of the beams.")]
        [Range(0f, 3f)] public float beamStrength = 1f;
        [Tooltip("Strength of the scan that sweeps up the cone and through the hologram, lighting up and settling the particles it passes.")]
        [Range(0f, 2f)] public float assemblySweep = 0.6f;
        [Tooltip("Seconds between sweeps.")]
        [Range(0.5f, 10f)] public float sweepPeriod = 3.5f;
        [Tooltip("How unsettled particles are just above the sweep, before it reaches them.")]
        [Range(0f, 1f)] public float preAssemblyScatter = 0.4f;
        [Tooltip("Radius of the emitter in local units. 0 = sized from the hologram.")]
        [Min(0f)] public float baseRadius = 0f;
        [Tooltip("Moves the projector from its automatic position under the hologram, in local units. Not changed by presets.")]
        public Vector3 offset = new Vector3(0f, -0.06f, 0f);
    }

    [Serializable]
    public class ScanlineSettings
    {
        public bool enabled = true;
        [Tooltip("Distance between lines in the recording's local units (metres before the prefab's scale), so lines scale with the hologram.")]
        [Range(0.002f, 0.2f)] public float spacing = 0.015f;
        [Tooltip("Fraction of each spacing period covered by the line.")]
        [Range(0.02f, 0.95f)] public float thickness = 0.35f;
        [Tooltip("Scroll speed in local units per second along Direction. Negative scrolls the other way.")]
        [Range(-0.5f, 0.5f)] public float speed = 0.03f;
        [Tooltip("How strongly lines brighten and gaps dim/fade.")]
        [Range(0f, 1f)] public float intensity = 0.3f;
        [Tooltip("Axis the lines move along, in the hologram's local space. (0, 1, 0) = horizontal lines moving up.")]
        public Vector3 direction = Vector3.up;
    }

    [Serializable]
    public class GlitchSettings
    {
        public bool enabled = true;
        [Tooltip("Average number of glitches per second.")]
        [Range(0f, 5f)] public float frequency = 0.25f;
        [Tooltip("Length of one glitch in seconds.")]
        [Range(0.02f, 1f)] public float duration = 0.12f;
        [Tooltip("Overall strength of brightness flicker, interference and dropout during a glitch.")]
        [Range(0f, 1f)] public float intensity = 0.4f;
        [Tooltip("Maximum sideways shift of a glitching band, in local units.")]
        [Range(0f, 0.2f)] public float displacement = 0.012f;
        [Tooltip("Chance that small groups of particles vanish during a glitch (mostly inside the band).")]
        [Range(0f, 1f)] public float dropout = 0.15f;
        [Tooltip("Changes when and where glitches happen.")]
        public int seed = 1;
    }

    [Serializable]
    public class DepthClippingSettings
    {
        [Tooltip("Hide samples closer to the recording iPhone than this (metres along its view axis, like the Record3D app). Independent of the prefab's placement and of the player.")]
        [Min(0f)] public float nearDepth = 0f;
        [Tooltip("Hide samples further from the recording iPhone than this. LiDAR is only reliable up to about 5 m.")]
        [Min(0f)] public float farDepth = 5f;
    }

    [Serializable]
    public class MaskingSettings
    {
        [Tooltip("Only keep points inside a box in the recording camera's space (shown as a gizmo when selected). Use it to cut background at the same depth as the person, beside or above them.")]
        public bool useCropBox = false;
        public Vector3 cropCenter = new Vector3(0f, -0.1f, -0.75f);
        public Vector3 cropSize = new Vector3(1.4f, 1.8f, 1f);
        [Tooltip("Keep only depth regions that are physically connected to the largest one (usually the person). Separates the subject from background objects that are not touching it, even at the same depth.")]
        public bool isolateSubject = false;
        [Tooltip("Neighbouring depth samples count as connected when their depths differ by less than this fraction of their depth.")]
        [Range(0.005f, 0.2f)] public float connectionTolerance = 0.04f;
        [Tooltip("Also keep separate regions at least this big relative to the largest one (e.g. a hand separated by a depth gap). 1 = largest region only.")]
        [Range(0f, 1f)] public float keepRegionsLargerThan = 0.2f;
        [Tooltip("Hide samples whose depth jumps by more than this fraction compared to the next depth sample: removes the smeared 'flying pixels' LiDAR leaves along silhouettes. 0 = off.")]
        [Range(0f, 0.5f)] public float edgeFilter = 0.05f;
    }

    [Serializable]
    public class PerformanceSettings
    {
        [Tooltip("Android (Meta Quest) builds always use real depth samples; this is their density (1 = every LiDAR sample).")]
        [Range(1, 4)] public int questDensity = 1;
        [Tooltip("Use Front Surface Only sorting on Quest too. It draws the hologram twice (depth, then colour); off falls back to Write Depth, which is cheaper but shows a faint grid where particles overlap.")]
        public bool questFrontSurfaceSorting = true;
        [Tooltip("Run Isolate Subject on Quest. Costs roughly 1-3 ms of CPU per video frame there.")]
        public bool questSubjectIsolation = true;
        [Tooltip("Draw the loose-particle layer (mist, lag copies, trails) on Quest. It is one extra pass over the particles.")]
        public bool questLooseParticles = true;
    }

    [Serializable]
    public class AdvancedSettings
    {
        [Tooltip("Alpha: translucent. Additive: light-only, never darkens the scene. Hybrid: translucent body with additive glow (recommended). Opaque: solid, cheapest.")]
        public Blending blending = Blending.Hybrid;
        [Tooltip("Front Surface Only: only the nearest layer is blended, once per pixel, so translucency is even and the figure does not wash out into a glowing cloud. Write Depth: one pass, nearer particles hide later ones. No Depth Write: every layer blends (most see-through, can build up).")]
        public Sorting sorting = Sorting.FrontSurfaceOnly;
        public DepthClippingSettings depthClipping = new DepthClippingSettings();
        public MaskingSettings masking = new MaskingSettings();
        public PerformanceSettings performance = new PerformanceSettings();
        [Tooltip("Record3D positions are right-handed and Unity is left-handed, so the hologram is a mirror image of the real scene. Enable to flip it the right way round.")]
        public bool correctMirroring = false;
        [Tooltip("Disable the original VFX Graph and the Record3DHologramRenderer on this object while this component is active.")]
        public bool hideOtherRenderers = true;
    }

    [SerializeField] private Record3DPlayback playback;
    [SerializeField] private Shader shader;
    [SerializeField] private Shader projectorShader;

    [Tooltip("Picking a preset copies its values into the settings below. Editing any value afterwards switches to Custom; picking Custom again restores the values you had before choosing a preset. Depth clipping, masking, density, performance and the projector's placement are never changed by presets.")]
    public Preset preset = Preset.ClassicSciFi;

    public AppearanceSettings appearance = new AppearanceSettings();
    public TransparencySettings transparency = new TransparencySettings();
    public GlowSettings glow = new GlowSettings();
    public ParticleSettings particles = new ParticleSettings();
    public InstabilitySettings instability = new InstabilitySettings();
    public EdgeSettings edges = new EdgeSettings();
    public MistSettings mist = new MistSettings();
    public ScanlineSettings scanlines = new ScanlineSettings();
    public GlitchSettings glitches = new GlitchSettings();
    public ProjectorSettings projector = new ProjectorSettings();
    public AdvancedSettings advanced = new AdvancedSettings();

    [SerializeField, HideInInspector] private Preset appliedPreset_ = Preset.ClassicSciFi;
    [SerializeField, HideInInspector] private Look customLook_;
    [SerializeField, HideInInspector] private bool hasCustomLook_;

    /// <summary>Number of particles drawn per frame at the current density (before clipping and masking).</summary>
    public int pointCount { get; private set; }

    /// <summary>Local-space bounds of the samples that survived clipping and masking in the current frame.</summary>
    public Bounds contentBounds { get { return contentBounds_; } }

    private static readonly int PositionTexId = Shader.PropertyToID("_PositionTex");
    private static readonly int ColorTexId = Shader.PropertyToID("_ColorTex");
    private static readonly int MaskTexId = Shader.PropertyToID("_MaskTex");
    private static readonly int MotionTexId = Shader.PropertyToID("_MotionTex");
    private static readonly int DepthRangeId = Shader.PropertyToID("_DepthRange");
    private static readonly int SizeParamsId = Shader.PropertyToID("_SizeParams");
    private static readonly int PointParamsId = Shader.PropertyToID("_PointParams");
    private static readonly int HoloColorId = Shader.PropertyToID("_HoloColor");
    private static readonly int ToneParamsId = Shader.PropertyToID("_ToneParams");
    private static readonly int LevelsParamsId = Shader.PropertyToID("_LevelsParams");
    private static readonly int RampParamsId = Shader.PropertyToID("_RampParams");
    private static readonly int OpacityParamsId = Shader.PropertyToID("_OpacityParams");
    private static readonly int GlowParamsId = Shader.PropertyToID("_GlowParams");
    private static readonly int ScanParamsId = Shader.PropertyToID("_ScanParams");
    private static readonly int ScanDirId = Shader.PropertyToID("_ScanDir");
    private static readonly int GlitchBandId = Shader.PropertyToID("_GlitchBand");
    private static readonly int GlitchParamsId = Shader.PropertyToID("_GlitchParams");
    private static readonly int OutputParamsId = Shader.PropertyToID("_OutputParams");
    private static readonly int CropMinId = Shader.PropertyToID("_CropMin");
    private static readonly int CropMaxId = Shader.PropertyToID("_CropMax");
    private static readonly int MaskScaleId = Shader.PropertyToID("_MaskScale");
    private static readonly int HoloTimeId = Shader.PropertyToID("_HoloTime");
    private static readonly int DriftParamsId = Shader.PropertyToID("_DriftParams");
    private static readonly int MotionParamsId = Shader.PropertyToID("_MotionParams");
    private static readonly int TrailParamsId = Shader.PropertyToID("_TrailParams");
    private static readonly int AssemblyParamsId = Shader.PropertyToID("_AssemblyParams");
    private static readonly int MistParamsId = Shader.PropertyToID("_MistParams");
    private static readonly int MistParams2Id = Shader.PropertyToID("_MistParams2");
    private static readonly int EdgeTexId = Shader.PropertyToID("_EdgeTex");
    private static readonly int EdgeParamsId = Shader.PropertyToID("_EdgeParams");
    private static readonly int EdgeParams2Id = Shader.PropertyToID("_EdgeParams2");
    private static readonly int PowerParamsId = Shader.PropertyToID("_PowerParams");
    private static readonly int EmitterParamsId = Shader.PropertyToID("_EmitterParams");
    private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
    private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
    private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
    private static readonly int ProjBaseId = Shader.PropertyToID("_ProjBase");
    private static readonly int ProjShapeId = Shader.PropertyToID("_ProjShape");
    private static readonly int ProjColorId = Shader.PropertyToID("_ProjColor");
    private static readonly int ProjParamsId = Shader.PropertyToID("_ProjParams");
    private static readonly int ProjParams2Id = Shader.PropertyToID("_ProjParams2");
    private static readonly int ProjTargetMinId = Shader.PropertyToID("_ProjTargetMin");
    private static readonly int ProjTargetMaxId = Shader.PropertyToID("_ProjTargetMax");
    private static readonly int ProjPowerId = Shader.PropertyToID("_ProjPower");
    private const string DepthPrepassLightMode = "SRPDefaultUnlit";

    private Material material_;
    private Material looseMaterial_;
    private Material projectorMaterial_;
    private Mesh mesh_;
    private Mesh projectorMesh_;
    private int meshKey_;
    private float boundsPadding_;
    private bool drawHologram_;
    private bool drawLoose_;
    private bool drawProjector_;

    // Power state, driven by HologramPlaybackController (or your own scripts) through SetPowerState.
    private float powerCollapse_;
    private float powerVisibility_ = 1f;
    private float projectorVisibility_ = 1f;
    private float powerFlash_;
    private float instabilityBoost_;

    // Per-frame analysis on the real depth samples: content bounds and the subject mask.
    private int analysisKey_;
    private Bounds contentBounds_;
    private bool hasContent_;
    private float[] gridDepth_;
    private int[] gridIndex_;
    private int[] labels_;
    private int[] queue_;
    private byte[] mask_;
    private readonly List<int> regionSizes_ = new List<int>();
    private Texture2D maskTex_;

    // Auto Levels: smoothed dark and bright points of the subject's perceptual brightness.
    private readonly int[] lightnessHistogram_ = new int[64];
    private float levelsLow_, levelsHigh_ = 1f;
    private bool hasLevels_;
    private static float[] srgbToLinear_;
    private static float[] linearToLightness_;

    // Coarse motion field: centroids of the surviving samples in cells of MotionCellSize x MotionCellSize
    // depth samples, compared between consecutive video frames. Gives lag and trails the subject's real
    // direction of movement without per-point tracking (the recording has none).
    private const int MotionCellSize = 16;
    private int motionColumns_, motionRows_;
    private int[] cellCount_;
    private Vector3[] cellSum_;
    private Vector3[] cellCentroid_;
    private bool[] cellValid_;
    private Vector3[] cellVelocity_;
    private ushort[] motionPixels_;
    private Texture2D motionTex_;
    private int motionFrame_ = -1;
    private float lastMotionTime_;

    // Soft silhouette: per depth sample, the distance to the outline (capped at the edge width) and the
    // direction out of the form, blurred so the stair-stepped outline of the depth data turns into a smooth falloff.
    private float[] edgeDistance_;
    private float[] edgeBlur_;
    private Color32[] edgePixels_;
    private Texture2D edgeTex_;

    // The projector follows the hologram slowly, so it does not jump around with every gesture.
    private Bounds projectorBounds_;
    private bool hasProjectorBounds_;

    private VisualEffect vfx_;
    private bool vfxWasEnabled_;
    private Record3DHologramRenderer legacyRenderer_;
    private bool legacyRendererWasEnabled_;

    // Lets automated render tests drive the animated effects deterministically; negative = use Time.time.
    private float timeOverride_ = -1f;
    private float lastUpdateTime_ = -1f;

    /// <summary>
    /// Powers the hologram up or down. <paramref name="collapse"/>: 0 = normal, 1 = everything pulled into the
    /// projector's emitter. <paramref name="visibility"/> and <paramref name="projectorVisibility"/> fade the
    /// hologram and the projector (0 = not drawn at all). <paramref name="flash"/> makes the emitter flare.
    /// <paramref name="instabilityBoost"/> is added to Overall Instability for the moment.
    /// </summary>
    public void SetPowerState(float collapse, float visibility, float projectorVisibility, float flash, float instabilityBoost)
    {
        powerCollapse_ = Mathf.Clamp01(collapse);
        powerVisibility_ = Mathf.Clamp01(visibility);
        projectorVisibility_ = Mathf.Clamp01(projectorVisibility);
        powerFlash_ = Mathf.Max(0f, flash);
        instabilityBoost_ = Mathf.Max(0f, instabilityBoost);
    }

    private void OnEnable()
    {
        if (playback == null)
        {
            playback = GetComponent<Record3DPlayback>();
        }
        if (shader == null)
        {
            shader = Shader.Find("Record3D/Volumetric Hologram");
        }
        if (projectorShader == null)
        {
            projectorShader = Shader.Find("Record3D/Hologram Projector");
        }
        if (shader == null || !shader.isSupported)
        {
            Debug.LogError("VolumetricHologramController: the 'Record3D/Volumetric Hologram' shader is missing or not supported on this platform.", this);
            enabled = false;
            return;
        }

        material_ = new Material(shader) { name = "Volumetric Hologram (runtime)", hideFlags = HideFlags.DontSave };
        looseMaterial_ = new Material(shader) { name = "Volumetric Hologram Loose Particles (runtime)", hideFlags = HideFlags.DontSave };
        if (projectorShader != null && projectorShader.isSupported)
        {
            projectorMaterial_ = new Material(projectorShader) { name = "Hologram Projector (runtime)", hideFlags = HideFlags.DontSave };
            projectorMesh_ = BuildProjectorMesh();
        }
        else
        {
            Debug.LogWarning("VolumetricHologramController: the 'Record3D/Hologram Projector' shader is missing or not supported, so the projector is not drawn.", this);
        }
        meshKey_ = 0;
        analysisKey_ = 0;
        motionFrame_ = -1;
        hasProjectorBounds_ = false;
        lastUpdateTime_ = -1f;

        // Disable the legacy renderer before looking at the VFX: its OnDisable gives the VFX its original state back.
        legacyRenderer_ = GetComponent<Record3DHologramRenderer>();
        legacyRendererWasEnabled_ = legacyRenderer_ != null && legacyRenderer_.enabled;
        if (advanced.hideOtherRenderers && legacyRendererWasEnabled_)
        {
            legacyRenderer_.enabled = false;
        }
        vfx_ = playback.streamEffect != null ? playback.streamEffect : GetComponent<VisualEffect>();
        vfxWasEnabled_ = vfx_ != null && vfx_.enabled;
        UpdateOtherRenderers();
    }

    private void OnDisable()
    {
        if (vfx_ != null)
        {
            vfx_.enabled = vfxWasEnabled_;
        }
        if (legacyRenderer_ != null && legacyRendererWasEnabled_ && !legacyRenderer_.enabled)
        {
            legacyRenderer_.enabled = true;
        }
        DestroyOwned(material_);
        DestroyOwned(looseMaterial_);
        DestroyOwned(projectorMaterial_);
        DestroyOwned(mesh_);
        DestroyOwned(projectorMesh_);
        DestroyOwned(maskTex_);
        DestroyOwned(motionTex_);
        DestroyOwned(edgeTex_);
        material_ = null;
        looseMaterial_ = null;
        projectorMaterial_ = null;
        mesh_ = null;
        projectorMesh_ = null;
        maskTex_ = null;
        motionTex_ = null;
        edgeTex_ = null;
    }

    private void UpdateOtherRenderers()
    {
        bool hide = advanced.hideOtherRenderers;
        if (legacyRenderer_ != null && legacyRendererWasEnabled_ && legacyRenderer_.enabled == hide)
        {
            legacyRenderer_.enabled = !hide; // it shows or hides the VFX itself
        }
        if (vfx_ != null)
        {
            if (hide)
            {
                vfx_.enabled = false;
            }
            else if (legacyRenderer_ == null || !legacyRenderer_.enabled)
            {
                vfx_.enabled = vfxWasEnabled_;
            }
        }
    }

    private void LateUpdate()
    {
        float time = timeOverride_ >= 0f ? timeOverride_ : Time.time;
        if (UpdateState(time))
        {
            Draw();
        }
    }

    /// <summary>
    /// Analyses the current frame and updates all materials for <paramref name="time"/>. Returns false when
    /// there is nothing to draw.
    /// </summary>
    private bool UpdateState(float time)
    {
        UpdateOtherRenderers();

        var video = playback.video;
        var positions = playback.positionTexture;
        var colors = playback.colorTexture;
        if (material_ == null || video == null || positions == null || colors == null)
        {
            return false;
        }
        if (powerVisibility_ <= 0.001f && projectorVisibility_ <= 0.001f)
        {
            return false; // fully powered down
        }

        float deltaTime = lastUpdateTime_ >= 0f ? Mathf.Clamp(time - lastUpdateTime_, 0f, 0.25f) : 0f;
        lastUpdateTime_ = time;

        bool android = Application.platform == RuntimePlatform.Android;
        var performance = advanced.performance;

        // Sampling grid: colour pixels, or one sample per real LiDAR depth sample.
        int stride = Mathf.Max(1, android ? performance.questDensity : particles.density);
        bool depthSamples = (android || particles.realDepthSamplesOnly) && video.depthWidth > 0 && video.depthHeight > 0;
        int columns, rows;
        float stepX, stepY;
        if (depthSamples)
        {
            columns = (video.depthWidth + stride - 1) / stride;
            rows = (video.depthHeight + stride - 1) / stride;
            stepX = stride * (float)video.width / video.depthWidth;
            stepY = stride * (float)video.height / video.depthHeight;
        }
        else
        {
            columns = (video.width + stride - 1) / stride;
            rows = (video.height + stride - 1) / stride;
            stepX = stepY = stride;
        }
        EnsureMesh(video.width, video.height, columns, rows, stepX, stepY);

        float near = Mathf.Max(0f, advanced.depthClipping.nearDepth);
        float far = Mathf.Max(near, advanced.depthClipping.farDepth);
        float mirror = advanced.correctMirroring ? -1f : 1f;
        bool isolate = advanced.masking.isolateSubject && (!android || performance.questSubjectIsolation);
        AnalyzeFrameIfNeeded(video, positions, near, far, mirror, isolate, time);
        if (!hasContent_)
        {
            return false;
        }
        UpdateMotionTexture(time, deltaTime);
        UpdateProjectorBounds(deltaTime);

        float spacingPerMetre = 0.5f * (stepX + stepY) / Mathf.Max(0.5f * (video.fx + video.fy), 1e-3f);
        UpdateMaterial(video, positions, colors, spacingPerMetre, near, far, mirror, isolate, time, android);
        drawHologram_ = powerVisibility_ > 0.001f;
        bool anyLoose = (mist.amount > 0f && mist.opacity > 0f) || instability.lag > 0f || instability.trails > 0f || instability.overshoot > 0f;
        drawLoose_ = drawHologram_ && anyLoose && instability.overall + instabilityBoost_ > 0f && (!android || performance.questLooseParticles);
        if (drawLoose_)
        {
            UpdateLooseMaterial();
        }
        drawProjector_ = projectorMaterial_ != null && projector.enabled && projectorVisibility_ > 0.001f;
        if (drawProjector_)
        {
            UpdateProjectorMaterial(time);
        }

        // Cull with the surviving samples, padded for particle size and everything that moves particles.
        boundsPadding_ = 2f * (glitches.displacement + particles.size * far * spacingPerMetre)
                       + 6f * instability.drift * instability.overall + 4f * edges.outwardDrift + mist.distance
                       + 1.5f * Mathf.Max(instability.lagTime, instability.trailLength);
        return true;
    }

    private void Draw()
    {
        var localToWorld = transform.localToWorldMatrix;
        var localBounds = contentBounds_;
        localBounds.Expand(boundsPadding_ + 0.05f * localBounds.size.magnitude);
        var worldBounds = Record3DPointMesh.Transform(localBounds, localToWorld);
        if (drawHologram_)
        {
            Graphics.RenderMesh(MakeRenderParams(material_, worldBounds), mesh_, 0, localToWorld);
        }
        if (drawLoose_)
        {
            Graphics.RenderMesh(MakeRenderParams(looseMaterial_, worldBounds), mesh_, 0, localToWorld);
        }
        if (drawProjector_)
        {
            var projectorWorldBounds = Record3DPointMesh.Transform(ProjectorLocalBounds(), localToWorld);
            Graphics.RenderMesh(MakeRenderParams(projectorMaterial_, projectorWorldBounds), projectorMesh_, 0, localToWorld);
        }
    }

    private RenderParams MakeRenderParams(Material material, Bounds worldBounds)
    {
        return new RenderParams(material)
        {
            layer = gameObject.layer,
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = false,
            lightProbeUsage = LightProbeUsage.Off,
            reflectionProbeUsage = ReflectionProbeUsage.Off,
            motionVectorMode = MotionVectorGenerationMode.Camera,
            worldBounds = worldBounds,
        };
    }

    private void EnsureMesh(int width, int height, int columns, int rows, float stepX, float stepY)
    {
        int key = HashCode.Combine(width, height, columns, rows, stepX, stepY);
        if (mesh_ != null && key == meshKey_)
        {
            return;
        }
        DestroyOwned(mesh_);
        mesh_ = Record3DPointMesh.Build(width, height, columns, rows, stepX, stepY);
        meshKey_ = key;
        pointCount = columns * rows;
    }

    private void UpdateMaterial(Record3DVideo video, Texture2D positions, Texture2D colors, float spacingPerMetre,
        float near, float far, float mirror, bool isolate, float time, bool android)
    {
        material_.SetTexture(PositionTexId, positions);
        material_.SetTexture(ColorTexId, colors);
        if (maskTex_ != null)
        {
            material_.SetTexture(MaskTexId, maskTex_);
        }
        if (motionTex_ != null)
        {
            material_.SetTexture(MotionTexId, motionTex_);
        }
        if (edgeTex_ != null)
        {
            material_.SetTexture(EdgeTexId, edgeTex_);
        }

        // Compare against the next real depth sample: depth is upsampled to the colour resolution.
        int edgeOffset = video.depthWidth > 0 ? Mathf.CeilToInt((float)video.width / video.depthWidth) : 1;
        material_.SetVector(DepthRangeId, new Vector4(near, far, edgeOffset, advanced.masking.edgeFilter));

        var scale = transform.lossyScale;
        float objectScale = (Mathf.Abs(scale.x) + Mathf.Abs(scale.y) + Mathf.Abs(scale.z)) / 3f;
        material_.SetVector(SizeParamsId, new Vector4(particles.size, spacingPerMetre, objectScale, mirror));

        float minPixels = particles.distanceCompensation ? Mathf.Max(0f, particles.minScreenSize) : 0f;
        float maxPixels = particles.distanceCompensation ? Mathf.Max(minPixels, particles.maxScreenSize) : 1e5f;
        material_.SetVector(PointParamsId, new Vector4(minPixels, maxPixels, Mathf.Clamp01(particles.sharpness), particles.shape == ParticleShape.Round ? 1f : 0f));

        // Hue normalised to a maximum channel of 1, with the HDR intensity separate.
        var hue = HueAndIntensity(out float intensity);
        material_.SetVector(HoloColorId, new Vector4(hue.x, hue.y, hue.z, intensity));
        material_.SetVector(ToneParamsId, new Vector4(appearance.brightness, appearance.contrast, appearance.luminanceInfluence, appearance.colorIntensity));
        material_.SetVector(LevelsParamsId, new Vector4(levelsLow_, 1f / Mathf.Max(levelsHigh_ - levelsLow_, 0.05f), hasLevels_ ? appearance.autoLevels : 0f, 0f));
        material_.SetVector(RampParamsId, new Vector4(appearance.shadowBrightness, appearance.highlightIntensity, 0f, 0f));
        material_.SetVector(OpacityParamsId, new Vector4(transparency.opacity, transparency.shadowOpacity, transparency.highlightOpacity, transparency.opacityContrast));
        material_.SetVector(GlowParamsId, new Vector4(glow.glowIntensity, glow.highlightGlow, Mathf.Min(glow.glowThreshold, 0.95f), glow.emissionColorInfluence));

        float scanIntensity = scanlines.enabled ? scanlines.intensity : 0f;
        float spacing = Mathf.Max(scanlines.spacing, 1e-4f);
        double scanPhase = time * (double)scanlines.speed / spacing;
        material_.SetVector(ScanParamsId, new Vector4(spacing, scanlines.thickness, (float)(scanPhase - Math.Floor(scanPhase)), scanIntensity));
        var direction = scanlines.direction.sqrMagnitude > 1e-8f ? scanlines.direction.normalized : Vector3.up;
        material_.SetVector(ScanDirId, direction);

        float instabilityScale = Mathf.Max(0f, instability.overall + instabilityBoost_);
        uint glitchSlot;
        material_.SetVector(GlitchBandId, ComputeGlitchBand(time, instabilityScale, out glitchSlot));
        material_.SetVector(GlitchParamsId, new Vector4(Mathf.Clamp01(glitches.dropout * instabilityScale), glitchSlot & 0xFFFF, (uint)glitches.seed & 0xFFFF, Mathf.Clamp01(particles.flicker * instabilityScale)));

        // Instability: wrapped time keeps the shader's sin() precise however long the game runs.
        material_.SetVector(HoloTimeId, new Vector4((float)(time % 600.0), 0f, 0f, 0f));
        material_.SetVector(DriftParamsId, new Vector4(instability.drift * instabilityScale, Mathf.Clamp01(instability.jitter * instabilityScale), instability.driftSpeed, 0f));
        material_.SetVector(EdgeParamsId, new Vector4(edges.outwardDrift * instabilityScale, edges.fade, Mathf.Clamp01(edges.dissolve * instabilityScale), edges.hoverSpeed));
        material_.SetVector(EdgeParams2Id, new Vector4(edges.softness, mist.edgeBias, edgeTex_ != null ? 1f : 0f, 0f));
        material_.SetVector(MotionParamsId, new Vector4(Mathf.Clamp01(instability.lag * instabilityScale), instability.lagTime, Mathf.Clamp01(instability.overshoot * instabilityScale), 0.5f * instability.lagTime));
        material_.SetVector(TrailParamsId, new Vector4(Mathf.Clamp01(instability.trails * instabilityScale), instability.trailLength, 0.5f, 160f));
        material_.SetVector(MistParamsId, new Vector4(Mathf.Clamp01(mist.amount * instabilityScale), mist.distance, mist.size, mist.opacity));
        material_.SetVector(MistParams2Id, new Vector4(0f, mist.rise, 0.35f, 0f));
        material_.SetVector(AssemblyParamsId, ComputeAssemblySweep(time, out _));
        ProjectorShape(out var emitter, out _, out float coneHeight, out _);
        material_.SetVector(PowerParamsId, new Vector4(powerCollapse_, powerVisibility_, 0f, 0f));
        material_.SetVector(EmitterParamsId, new Vector4(emitter.x, emitter.y, emitter.z, coneHeight));

        var masking = advanced.masking;
        var cropMin = masking.cropCenter - 0.5f * masking.cropSize;
        var cropMax = masking.cropCenter + 0.5f * masking.cropSize;
        material_.SetVector(CropMinId, new Vector4(cropMin.x, cropMin.y, cropMin.z, masking.useCropBox ? 1f : 0f));
        material_.SetVector(CropMaxId, new Vector4(cropMax.x, cropMax.y, cropMax.z, isolate && maskTex_ != null ? 1f : 0f));
        if (maskTex_ != null)
        {
            material_.SetVector(MaskScaleId, new Vector4((float)maskTex_.width / video.width, (float)maskTex_.height / video.height, 0f, 0f));
        }

        var blending = advanced.blending;
        var sorting = advanced.sorting;
        if (android && sorting == Sorting.FrontSurfaceOnly && !advanced.performance.questFrontSurfaceSorting)
        {
            sorting = Sorting.WriteDepth;
        }
        // While collapsing, particles stream into the emitter as light and no longer form a surface.
        bool prepass = blending != Blending.Opaque && sorting == Sorting.FrontSurfaceOnly && powerCollapse_ <= 0.001f;
        bool hardEdges = blending == Blending.Opaque || sorting == Sorting.WriteDepth;
        float flickerSlot = Mathf.Floor(time * 24f) % 65536f;
        material_.SetVector(OutputParamsId, new Vector4((float)blending, 0.05f, flickerSlot, hardEdges ? 1f : 0f));
        material_.SetShaderPassEnabled(DepthPrepassLightMode, prepass);

        // The shader outputs premultiplied colour.
        material_.SetFloat(SrcBlendId, (float)BlendMode.One);
        material_.SetFloat(DstBlendId, (float)(blending == Blending.Additive ? BlendMode.One : blending == Blending.Opaque ? BlendMode.Zero : BlendMode.OneMinusSrcAlpha));
        material_.SetFloat(ZWriteId, blending == Blending.Opaque || sorting == Sorting.WriteDepth ? 1f : 0f);
        material_.renderQueue = blending == Blending.Opaque ? (int)RenderQueue.AlphaTest : (int)RenderQueue.Transparent;
    }

    /// <summary>
    /// The loose-particle layer: the same particles and settings drawn again, additively, where a share of
    /// them become mist, lag copies, overshoot copies or motion trails. The shader culls the rest.
    /// </summary>
    private void UpdateLooseMaterial()
    {
        looseMaterial_.CopyPropertiesFromMaterial(material_);
        var looseParams = material_.GetVector(MistParams2Id);
        looseParams.x = 1f;
        looseMaterial_.SetVector(MistParams2Id, looseParams);
        var output = material_.GetVector(OutputParamsId);
        output.x = (float)Blending.Additive;
        output.w = 0f;
        looseMaterial_.SetVector(OutputParamsId, output);
        looseMaterial_.SetFloat(SrcBlendId, (float)BlendMode.One);
        looseMaterial_.SetFloat(DstBlendId, (float)BlendMode.One);
        looseMaterial_.SetFloat(ZWriteId, 0f);
        looseMaterial_.SetShaderPassEnabled(DepthPrepassLightMode, false);
        looseMaterial_.renderQueue = (int)RenderQueue.Transparent + 1;
    }

    private Vector3 HueAndIntensity(out float intensity)
    {
        var color = appearance.hologramColor.linear;
        intensity = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        return intensity > 1e-5f ? new Vector3(color.r, color.g, color.b) / intensity : Vector3.one;
    }

    // ---------------------------------------------------------------- Projector

    private void UpdateProjectorBounds(float deltaTime)
    {
        if (!hasProjectorBounds_)
        {
            projectorBounds_ = contentBounds_;
            hasProjectorBounds_ = true;
            return;
        }
        float follow = 1f - Mathf.Exp(-deltaTime / 1.5f);
        var min = Vector3.Lerp(projectorBounds_.min, contentBounds_.min, follow);
        var max = Vector3.Lerp(projectorBounds_.max, contentBounds_.max, follow);
        projectorBounds_.SetMinMax(min, max);
    }

    /// <summary>Emitter centre (local), emitter radius, cone height and cone top radius.</summary>
    private void ProjectorShape(out Vector3 centre, out float baseRadius, out float height, out float topRadius)
    {
        var b = projectorBounds_;
        centre = new Vector3(b.center.x, b.min.y, b.center.z) + projector.offset;
        float halfWidth = Mathf.Max(b.extents.x, b.extents.z);
        baseRadius = projector.baseRadius > 0f ? projector.baseRadius : Mathf.Max(0.03f, halfWidth * 0.45f);
        height = Mathf.Max(0.05f, b.max.y - centre.y + 0.05f * b.size.y);
        topRadius = Mathf.Max(baseRadius, halfWidth * 1.1f);
    }

    private Bounds ProjectorLocalBounds()
    {
        ProjectorShape(out var centre, out float baseRadius, out float height, out float topRadius);
        float radius = Mathf.Max(topRadius, baseRadius * 1.8f);
        return new Bounds(centre + new Vector3(0f, 0.5f * height, 0f), new Vector3(2f * radius, height + 0.1f, 2f * radius));
    }

    /// <summary>
    /// The assembly sweep rises from the emitter to just above the hologram once per Sweep Period.
    /// Returns (sweep y, half height of the bright band, intensity, pre-assembly scatter) for the hologram.
    /// </summary>
    private Vector4 ComputeAssemblySweep(float time, out float sweepHeight01)
    {
        sweepHeight01 = -1f;
        if (!projector.enabled || !hasProjectorBounds_)
        {
            return new Vector4(0f, 0.01f, 0f, 0f);
        }
        ProjectorShape(out var centre, out _, out float height, out _);
        double cycle = time / (double)Mathf.Max(projector.sweepPeriod, 0.1f);
        float phase = (float)(cycle - Math.Floor(cycle));
        sweepHeight01 = phase * 1.1f;
        float sweepY = centre.y + sweepHeight01 * height;
        return new Vector4(sweepY, Mathf.Max(0.02f * height, 1e-3f), projector.assemblySweep, projector.preAssemblyScatter * Mathf.Max(0f, instability.overall));
    }

    private void UpdateProjectorMaterial(float time)
    {
        ProjectorShape(out var centre, out float baseRadius, out float height, out float topRadius);
        ComputeAssemblySweep(time, out float sweepHeight01);
        var hue = HueAndIntensity(out float intensity);
        projectorMaterial_.SetVector(ProjBaseId, new Vector4(centre.x, centre.y, centre.z, baseRadius));
        projectorMaterial_.SetVector(ProjShapeId, new Vector4(height, topRadius, (float)(time % 600.0), sweepHeight01));
        projectorMaterial_.SetVector(ProjColorId, new Vector4(hue.x, hue.y, hue.z, intensity));
        projectorMaterial_.SetVector(ProjParamsId, new Vector4(projector.baseGlow, projector.coneIntensity, projector.beamCount, projector.beamStrength));
        projectorMaterial_.SetVector(ProjParams2Id, new Vector4(projector.assemblySweep, (uint)glitches.seed & 0xFFFF, 2f, 0f));
        projectorMaterial_.SetVector(ProjTargetMinId, projectorBounds_.min);
        projectorMaterial_.SetVector(ProjTargetMaxId, projectorBounds_.max);
        projectorMaterial_.SetVector(ProjPowerId, new Vector4(projectorVisibility_, powerCollapse_, powerFlash_, 0f));
    }

    /// <summary>
    /// One mesh for the whole projector; the shader builds the shapes from these parameters.
    /// Cone: (0, u around, v up). Emitter disc: (1, x, z). Beams: (2, index, along, side).
    /// </summary>
    private static Mesh BuildProjectorMesh()
    {
        const int coneSegments = 48;
        const int maxBeams = 48;
        var vertices = new List<Vector4>();
        var indices = new List<int>();

        for (int s = 0; s <= coneSegments; s++)
        {
            float u = (float)s / coneSegments;
            vertices.Add(new Vector4(0f, u, 0f, 0f));
            vertices.Add(new Vector4(0f, u, 1f, 0f));
            if (s < coneSegments)
            {
                int i = 2 * s;
                indices.AddRange(new[] { i, i + 1, i + 3, i, i + 3, i + 2 });
            }
        }

        int disc = vertices.Count;
        vertices.Add(new Vector4(1f, -1f, -1f, 0f));
        vertices.Add(new Vector4(1f, 1f, -1f, 0f));
        vertices.Add(new Vector4(1f, 1f, 1f, 0f));
        vertices.Add(new Vector4(1f, -1f, 1f, 0f));
        indices.AddRange(new[] { disc, disc + 1, disc + 2, disc, disc + 2, disc + 3 });

        for (int b = 0; b < maxBeams; b++)
        {
            int i = vertices.Count;
            vertices.Add(new Vector4(2f, b, 0f, -1f));
            vertices.Add(new Vector4(2f, b, 0f, 1f));
            vertices.Add(new Vector4(2f, b, 1f, 1f));
            vertices.Add(new Vector4(2f, b, 1f, -1f));
            indices.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }

        var mesh = new Mesh { name = "Hologram Projector", hideFlags = HideFlags.DontSave };
        mesh.SetVertexBufferParams(vertices.Count, new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 4));
        mesh.SetVertexBufferData(vertices, 0, 0, vertices.Count);
        mesh.SetIndexBufferParams(indices.Count, IndexFormat.UInt16);
        var shortIndices = new ushort[indices.Count];
        for (int i = 0; i < indices.Count; i++)
        {
            shortIndices[i] = (ushort)indices[i];
        }
        mesh.SetIndexBufferData(shortIndices, 0, 0, shortIndices.Length);
        mesh.subMeshCount = 1;
        mesh.SetSubMesh(0, new SubMeshDescriptor(0, indices.Count, MeshTopology.Triangles), MeshUpdateFlags.DontRecalculateBounds);
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10f);
        mesh.UploadMeshData(true);
        return mesh;
    }

    // ---------------------------------------------------------------- Glitches

    /// <summary>
    /// Glitches are scheduled in fixed time slots of Glitch Duration; each slot glitches with probability
    /// Frequency x Duration, in a random horizontal band of the visible content.
    /// </summary>
    private Vector4 ComputeGlitchBand(float time, float instabilityScale, out uint slot)
    {
        slot = 0;
        float strength = glitches.intensity * Mathf.Min(instabilityScale, 1f);
        if (!glitches.enabled || strength <= 0f)
        {
            return Vector4.zero;
        }
        float duration = Mathf.Max(0.02f, glitches.duration);
        double slotTime = time / (double)duration;
        long index = (long)Math.Floor(slotTime);
        slot = (uint)index;
        uint seed = (uint)glitches.seed;
        if (Hash01(slot, seed, 0u) >= Mathf.Clamp01(glitches.frequency * instabilityScale * duration))
        {
            return Vector4.zero;
        }

        float envelope = Mathf.Sin((float)(slotTime - index) * Mathf.PI);
        float bottom = contentBounds_.min.y;
        float height = Mathf.Max(contentBounds_.size.y, 1e-3f);
        float centre = bottom + height * Hash01(slot, seed, 1u);
        float halfHeight = height * Mathf.Lerp(0.03f, 0.12f, Hash01(slot, seed, 2u));
        float shift = glitches.displacement * (Hash01(slot, seed, 3u) < 0.5f ? -1f : 1f) * Mathf.Lerp(0.5f, 1f, Hash01(slot, seed, 4u));
        return new Vector4(centre, halfHeight, shift, strength * envelope);
    }

    private static uint Hash(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352dU;
        x ^= x >> 15;
        x *= 0x846ca68bU;
        x ^= x >> 16;
        return x;
    }

    private static float Hash01(uint a, uint b, uint c)
    {
        return (Hash(a * 0x8da6b343U ^ b * 0xd8163841U ^ c * 0xcb1ab31fU) >> 8) * (1f / 16777216f);
    }

    // ---------------------------------------------------------------- Frame analysis

    /// <summary>
    /// Works on one sample per real depth sample of the decoded frame: finds the bounds of what survives
    /// clipping and masking (used for culling, glitch placement and the projector), builds the subject mask
    /// with Isolate Subject, measures Auto Levels and feeds the motion field. Runs only when the frame or a
    /// relevant setting changes.
    /// </summary>
    private void AnalyzeFrameIfNeeded(Record3DVideo video, Texture2D positions, float near, float far, float mirror, bool isolate, float time)
    {
        var masking = advanced.masking;
        int key = HashCode.Combine(playback.loadedFrame, positions.GetInstanceID(), near, far, mirror, isolate,
            HashCode.Combine(masking.useCropBox, masking.cropCenter, masking.cropSize, masking.connectionTolerance, masking.keepRegionsLargerThan, edges.width));
        if (key == analysisKey_ && (maskTex_ != null || !isolate))
        {
            return;
        }
        analysisKey_ = key;

        int width = video.width;
        int height = video.height;
        int gridWidth = video.depthWidth > 0 ? video.depthWidth : Mathf.Max(1, width / 4);
        int gridHeight = video.depthHeight > 0 ? video.depthHeight : Mathf.Max(1, height / 4);
        int count = gridWidth * gridHeight;
        if (gridDepth_ == null || gridDepth_.Length != count)
        {
            gridDepth_ = new float[count];
            gridIndex_ = new int[count];
            labels_ = new int[count];
            queue_ = new int[count];
            mask_ = new byte[count];
        }

        var source = video.positionsBuffer;
        var cropMin = masking.cropCenter - 0.5f * masking.cropSize;
        var cropMax = masking.cropCenter + 0.5f * masking.cropSize;
        for (int gy = 0; gy < gridHeight; gy++)
        {
            int ty = Mathf.Min((int)((gy + 0.5f) * height / gridHeight), height - 1);
            for (int gx = 0; gx < gridWidth; gx++)
            {
                int tx = Mathf.Min((int)((gx + 0.5f) * width / gridWidth), width - 1);
                int k = gy * gridWidth + gx;
                int i = (ty * width + tx) * 4;
                gridIndex_[k] = i;

                float x = source[i] * mirror;
                float y = source[i + 1];
                float depth = -source[i + 2];
                bool keep = depth >= near && depth <= far; // NaN (no depth) fails
                if (keep && masking.useCropBox)
                {
                    keep = x >= cropMin.x && x <= cropMax.x && y >= cropMin.y && y <= cropMax.y && -depth >= cropMin.z && -depth <= cropMax.z;
                }
                gridDepth_[k] = keep ? depth : -1f;
            }
        }

        if (isolate)
        {
            IsolateSubject(gridWidth, gridHeight, masking.connectionTolerance, masking.keepRegionsLargerThan);
            if (maskTex_ == null || maskTex_.width != gridWidth || maskTex_.height != gridHeight)
            {
                DestroyOwned(maskTex_);
                maskTex_ = new Texture2D(gridWidth, gridHeight, TextureFormat.R8, false, true)
                {
                    name = "Volumetric Hologram Subject Mask",
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.DontSave,
                };
            }
            maskTex_.SetPixelData(mask_, 0);
            maskTex_.Apply(false, false);
        }

        EnsureMotionGrid(gridWidth, gridHeight);
        Array.Clear(cellCount_, 0, cellCount_.Length);
        Array.Clear(cellSum_, 0, cellSum_.Length);

        EnsureLightnessTables();
        var colors = video.rgbBuffer;
        Array.Clear(lightnessHistogram_, 0, lightnessHistogram_.Length);
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
        int kept = 0;
        for (int k = 0; k < count; k++)
        {
            if (gridDepth_[k] < 0f)
            {
                continue;
            }
            int i = gridIndex_[k];
            var p = new Vector3(source[i] * mirror, source[i + 1], source[i + 2]);
            min = Vector3.Min(min, p);
            max = Vector3.Max(max, p);

            int cell = (k / gridWidth / MotionCellSize) * motionColumns_ + (k % gridWidth) / MotionCellSize;
            cellCount_[cell]++;
            cellSum_[cell] += p;

            int c = i / 4 * 3;
            float luminance = 0.2126f * srgbToLinear_[colors[c]] + 0.7152f * srgbToLinear_[colors[c + 1]] + 0.0722f * srgbToLinear_[colors[c + 2]];
            float lightness = linearToLightness_[Mathf.Min((int)(luminance * 1023f), 1023)];
            lightnessHistogram_[Mathf.Min((int)(lightness * 64f), 63)]++;
            kept++;
        }
        hasContent_ = kept > 0;
        if (hasContent_)
        {
            contentBounds_.SetMinMax(min, max);
            UpdateLevels(kept);
            // Depth samples per local unit at the subject's distance, to express Edge Width in local units.
            float focalInSamples = 0.5f * (video.fx + video.fy) * gridWidth / width;
            UpdateEdgeField(gridWidth, gridHeight, focalInSamples / Mathf.Max(-contentBounds_.center.z, 0.1f));
        }
        UpdateMotionField(time);
    }

    /// <summary>
    /// Distance from each kept depth sample to the nearest removed sample or the image border (chamfer
    /// distance, capped at Edge Width), blurred, with the direction pointing out of the form.
    /// </summary>
    private void UpdateEdgeField(int gridWidth, int gridHeight, float samplesPerUnit)
    {
        int count = gridWidth * gridHeight;
        if (edgeDistance_ == null || edgeDistance_.Length != count)
        {
            edgeDistance_ = new float[count];
            edgeBlur_ = new float[count];
            edgePixels_ = new Color32[count];
        }
        float bandWidth = Mathf.Clamp(edges.width * samplesPerUnit, 1f, 48f);
        const float diagonal = 1.4142135f;

        for (int k = 0; k < count; k++)
        {
            edgeDistance_[k] = gridDepth_[k] >= 0f ? bandWidth : 0f;
        }
        // Outside the image counts as outside the form, so frame-cut edges dissolve too.
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int k = y * gridWidth + x;
                float d = edgeDistance_[k];
                if (d == 0f) continue;
                d = Mathf.Min(d, x > 0 ? edgeDistance_[k - 1] + 1f : 1f);
                d = Mathf.Min(d, y > 0 ? edgeDistance_[k - gridWidth] + 1f : 1f);
                d = Mathf.Min(d, x > 0 && y > 0 ? edgeDistance_[k - gridWidth - 1] + diagonal : diagonal);
                d = Mathf.Min(d, x < gridWidth - 1 && y > 0 ? edgeDistance_[k - gridWidth + 1] + diagonal : diagonal);
                edgeDistance_[k] = d;
            }
        }
        for (int y = gridHeight - 1; y >= 0; y--)
        {
            for (int x = gridWidth - 1; x >= 0; x--)
            {
                int k = y * gridWidth + x;
                float d = edgeDistance_[k];
                if (d == 0f) continue;
                d = Mathf.Min(d, x < gridWidth - 1 ? edgeDistance_[k + 1] + 1f : 1f);
                d = Mathf.Min(d, y < gridHeight - 1 ? edgeDistance_[k + gridWidth] + 1f : 1f);
                d = Mathf.Min(d, x < gridWidth - 1 && y < gridHeight - 1 ? edgeDistance_[k + gridWidth + 1] + diagonal : diagonal);
                d = Mathf.Min(d, x > 0 && y < gridHeight - 1 ? edgeDistance_[k + gridWidth - 1] + diagonal : diagonal);
                edgeDistance_[k] = d;
            }
        }

        // 3x3 blur removes the stair steps of the block-shaped depth outline.
        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                float sum = 0f;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int row = Mathf.Clamp(y + dy, 0, gridHeight - 1) * gridWidth;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        sum += edgeDistance_[row + Mathf.Clamp(x + dx, 0, gridWidth - 1)];
                    }
                }
                edgeBlur_[y * gridWidth + x] = sum / 9f;
            }
        }

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                int k = y * gridWidth + x;
                float gx = edgeBlur_[y * gridWidth + Mathf.Min(x + 1, gridWidth - 1)] - edgeBlur_[y * gridWidth + Mathf.Max(x - 1, 0)];
                float gy = edgeBlur_[Mathf.Min(y + 1, gridHeight - 1) * gridWidth + x] - edgeBlur_[Mathf.Max(y - 1, 0) * gridWidth + x];
                var outward = new Vector2(-gx, -gy);
                outward = outward.sqrMagnitude > 1e-8f ? outward.normalized : Vector2.zero;
                edgePixels_[k] = new Color32(
                    (byte)Mathf.RoundToInt(Mathf.Clamp01(edgeBlur_[k] / bandWidth) * 255f),
                    (byte)Mathf.RoundToInt((outward.x * 0.5f + 0.5f) * 255f),
                    (byte)Mathf.RoundToInt((outward.y * 0.5f + 0.5f) * 255f),
                    255);
            }
        }

        if (edgeTex_ == null || edgeTex_.width != gridWidth || edgeTex_.height != gridHeight)
        {
            DestroyOwned(edgeTex_);
            edgeTex_ = new Texture2D(gridWidth, gridHeight, TextureFormat.RGBA32, false, true)
            {
                name = "Volumetric Hologram Edge Field",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
        }
        edgeTex_.SetPixels32(edgePixels_);
        edgeTex_.Apply(false, false);
    }

    private void EnsureMotionGrid(int gridWidth, int gridHeight)
    {
        int columns = (gridWidth + MotionCellSize - 1) / MotionCellSize;
        int rows = (gridHeight + MotionCellSize - 1) / MotionCellSize;
        if (cellCount_ != null && columns == motionColumns_ && rows == motionRows_)
        {
            return;
        }
        motionColumns_ = columns;
        motionRows_ = rows;
        int cells = columns * rows;
        cellCount_ = new int[cells];
        cellSum_ = new Vector3[cells];
        cellCentroid_ = new Vector3[cells];
        cellValid_ = new bool[cells];
        cellVelocity_ = new Vector3[cells];
        motionPixels_ = new ushort[cells * 4];
        motionFrame_ = -1;
    }

    /// <summary>
    /// When the video advanced by a frame or two, each cell's velocity moves towards the change of its centroid.
    /// Looping, seeking or a change of settings on the same frame never creates a velocity.
    /// </summary>
    private void UpdateMotionField(float time)
    {
        int frame = playback.loadedFrame;
        if (frame == motionFrame_)
        {
            return;
        }
        int step = frame - motionFrame_;
        bool consecutive = motionFrame_ >= 0 && step >= 1 && step <= 3;
        float framesPerSecond = Mathf.Max(1, playback.fps);
        for (int c = 0; c < cellCount_.Length; c++)
        {
            bool valid = cellCount_[c] >= 6;
            var centroid = valid ? cellSum_[c] / cellCount_[c] : Vector3.zero;
            if (consecutive && valid && cellValid_[c])
            {
                var velocity = Vector3.ClampMagnitude((centroid - cellCentroid_[c]) * (framesPerSecond / step), 3f);
                cellVelocity_[c] = Vector3.Lerp(cellVelocity_[c], velocity, 0.5f);
            }
            else if (!consecutive || !valid)
            {
                cellVelocity_[c] = Vector3.zero;
            }
            cellCentroid_[c] = centroid;
            cellValid_[c] = valid;
        }
        motionFrame_ = frame;
        lastMotionTime_ = time;
    }

    private void UpdateMotionTexture(float time, float deltaTime)
    {
        if (cellVelocity_ == null)
        {
            return;
        }
        // If the video stalls (paused), let the motion settle instead of freezing mid-trail.
        if (time - lastMotionTime_ > 0.1f)
        {
            float decay = Mathf.Exp(-deltaTime / 0.2f);
            for (int c = 0; c < cellVelocity_.Length; c++)
            {
                cellVelocity_[c] *= decay;
            }
        }
        if (motionTex_ == null || motionTex_.width != motionColumns_ || motionTex_.height != motionRows_)
        {
            DestroyOwned(motionTex_);
            motionTex_ = new Texture2D(motionColumns_, motionRows_, TextureFormat.RGBAHalf, false, true)
            {
                name = "Volumetric Hologram Motion Field",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
        }
        for (int c = 0; c < cellVelocity_.Length; c++)
        {
            var v = cellVelocity_[c];
            motionPixels_[4 * c] = Mathf.FloatToHalf(v.x);
            motionPixels_[4 * c + 1] = Mathf.FloatToHalf(v.y);
            motionPixels_[4 * c + 2] = Mathf.FloatToHalf(v.z);
            motionPixels_[4 * c + 3] = 0;
        }
        motionTex_.SetPixelData(motionPixels_, 0);
        motionTex_.Apply(false, false);
    }

    /// <summary>Moves the Auto Levels dark/bright points towards this frame's 2nd/98th brightness percentiles.</summary>
    private void UpdateLevels(int samples)
    {
        float low = HistogramPercentile(0.02f * samples);
        float high = HistogramPercentile(0.98f * samples);
        if (high - low < 0.15f)
        {
            float centre = 0.5f * (low + high);
            low = Mathf.Max(0f, centre - 0.075f);
            high = Mathf.Min(1f, centre + 0.075f);
        }
        // Smooth over roughly ten video frames so the exposure does not pump.
        levelsLow_ = hasLevels_ ? Mathf.Lerp(levelsLow_, low, 0.1f) : low;
        levelsHigh_ = hasLevels_ ? Mathf.Lerp(levelsHigh_, high, 0.1f) : high;
        hasLevels_ = true;
    }

    private float HistogramPercentile(float rank)
    {
        int bins = lightnessHistogram_.Length;
        int total = 0;
        for (int bin = 0; bin < bins; bin++)
        {
            total += lightnessHistogram_[bin];
            if (total >= rank)
            {
                return (bin + 0.5f) / bins;
            }
        }
        return 1f;
    }

    private static void EnsureLightnessTables()
    {
        if (srgbToLinear_ != null)
        {
            return;
        }
        srgbToLinear_ = new float[256];
        for (int v = 0; v < 256; v++)
        {
            srgbToLinear_[v] = Mathf.GammaToLinearSpace(v / 255f);
        }
        // Same perceptual curve as the shader: lightness = luminance^(1/2.2).
        linearToLightness_ = new float[1024];
        for (int v = 0; v < 1024; v++)
        {
            linearToLightness_[v] = Mathf.Pow(v / 1023f, 1f / 2.2f);
        }
    }

    /// <summary>
    /// Labels 4-connected regions of depth samples whose depths are continuous, then keeps the largest
    /// region and any region at least <paramref name="keepFraction"/> of its size. Writes mask_ and
    /// marks removed samples in gridDepth_.
    /// </summary>
    private void IsolateSubject(int gridWidth, int gridHeight, float tolerance, float keepFraction)
    {
        int count = gridWidth * gridHeight;
        for (int k = 0; k < count; k++)
        {
            labels_[k] = -1;
        }
        regionSizes_.Clear();

        for (int start = 0; start < count; start++)
        {
            if (gridDepth_[start] < 0f || labels_[start] >= 0)
            {
                continue;
            }
            int label = regionSizes_.Count;
            int head = 0, tail = 0;
            labels_[start] = label;
            queue_[tail++] = start;
            while (head < tail)
            {
                int k = queue_[head++];
                int x = k % gridWidth;
                float depth = gridDepth_[k];
                if (x > 0) Join(k - 1, depth, label, tolerance, ref tail);
                if (x < gridWidth - 1) Join(k + 1, depth, label, tolerance, ref tail);
                if (k >= gridWidth) Join(k - gridWidth, depth, label, tolerance, ref tail);
                if (k + gridWidth < count) Join(k + gridWidth, depth, label, tolerance, ref tail);
            }
            regionSizes_.Add(tail);
        }

        int largest = 0;
        foreach (int size in regionSizes_)
        {
            largest = Mathf.Max(largest, size);
        }
        float minimumSize = Mathf.Max(1f, keepFraction * largest);
        for (int k = 0; k < count; k++)
        {
            bool keep = labels_[k] >= 0 && regionSizes_[labels_[k]] >= minimumSize;
            mask_[k] = keep ? (byte)255 : (byte)0;
            if (!keep)
            {
                gridDepth_[k] = -1f;
            }
        }
    }

    private void Join(int neighbour, float depth, int label, float tolerance, ref int tail)
    {
        float neighbourDepth = gridDepth_[neighbour];
        if (neighbourDepth < 0f || labels_[neighbour] >= 0 || Mathf.Abs(neighbourDepth - depth) > tolerance * Mathf.Min(depth, neighbourDepth))
        {
            return;
        }
        labels_[neighbour] = label;
        queue_[tail++] = neighbour;
    }

    private static void DestroyOwned(UnityEngine.Object obj)
    {
        if (obj == null) return;
        if (Application.isPlaying) Destroy(obj);
        else DestroyImmediate(obj);
    }

    private void OnDrawGizmosSelected()
    {
        if (!advanced.masking.useCropBox)
        {
            return;
        }
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube(advanced.masking.cropCenter, advanced.masking.cropSize);
    }

    // ---------------------------------------------------------------- Presets

    /// <summary>The settings a preset controls.</summary>
    [Serializable]
    private class Look
    {
        public AppearanceSettings appearance;
        public TransparencySettings transparency;
        public GlowSettings glow;
        public InstabilitySettings instability;
        public EdgeSettings edges;
        public MistSettings mist;
        public ScanlineSettings scanlines;
        public GlitchSettings glitches;
        public ProjectorSettings projector;
        public float particleSize;
        public float particleSharpness;
        public ParticleShape particleShape;
        public float particleFlicker;
        public Blending blending;
        public Sorting sorting;
    }

    private Look CaptureLook()
    {
        var look = new Look
        {
            appearance = appearance,
            transparency = transparency,
            glow = glow,
            instability = instability,
            edges = edges,
            mist = mist,
            scanlines = scanlines,
            glitches = glitches,
            projector = projector,
            particleSize = particles.size,
            particleSharpness = particles.sharpness,
            particleShape = particles.shape,
            particleFlicker = particles.flicker,
            blending = advanced.blending,
            sorting = advanced.sorting,
        };
        return JsonUtility.FromJson<Look>(JsonUtility.ToJson(look)); // deep copy
    }

    private void ApplyLook(Look source)
    {
        var look = JsonUtility.FromJson<Look>(JsonUtility.ToJson(source)); // deep copy
        appearance = look.appearance;
        transparency = look.transparency;
        glow = look.glow;
        instability = look.instability ?? new InstabilitySettings();
        edges = look.edges ?? new EdgeSettings();
        mist = look.mist ?? new MistSettings();
        scanlines = look.scanlines;
        glitches = look.glitches;
        if (look.projector != null)
        {
            look.projector.offset = projector.offset; // placement belongs to the scene, not the look
            look.projector.baseRadius = projector.baseRadius;
            projector = look.projector;
        }
        particles.size = look.particleSize;
        particles.sharpness = look.particleSharpness;
        particles.shape = look.particleShape;
        particles.flicker = look.particleFlicker;
        advanced.blending = look.blending;
        advanced.sorting = look.sorting;
    }

    private Look PresetLook(Preset preset)
    {
        // The field defaults are the Classic Sci-Fi Hologram.
        var defaults = new ParticleSettings();
        var look = new Look
        {
            appearance = new AppearanceSettings(),
            transparency = new TransparencySettings(),
            glow = new GlowSettings(),
            instability = new InstabilitySettings(),
            edges = new EdgeSettings(),
            mist = new MistSettings(),
            scanlines = new ScanlineSettings(),
            glitches = new GlitchSettings { seed = glitches.seed },
            projector = new ProjectorSettings { offset = projector.offset, baseRadius = projector.baseRadius },
            particleSize = defaults.size,
            particleSharpness = defaults.sharpness,
            particleShape = defaults.shape,
            particleFlicker = defaults.flicker,
            blending = Blending.Hybrid,
            sorting = Sorting.FrontSurfaceOnly,
        };

        switch (preset)
        {
            case Preset.GhostlyProjection:
                look.appearance.hologramColor = new Color(0.62f, 0.8f, 1f);
                look.appearance.colorIntensity = 0.6f;
                look.appearance.brightness = 0.1f;
                look.appearance.contrast = 0.9f;
                look.appearance.shadowBrightness = 0.35f;
                look.transparency.opacity = 0.45f;
                look.transparency.shadowOpacity = 0.1f;
                look.transparency.highlightOpacity = 0.85f;
                look.transparency.opacityContrast = 1.6f;
                look.glow.glowIntensity = 0.45f;
                look.glow.highlightGlow = 0.6f;
                look.glow.glowThreshold = 0.55f;
                look.glow.emissionColorInfluence = 0.6f;
                look.instability.drift = 0.007f;
                look.instability.driftSpeed = 0.5f;
                look.edges.width = 0.06f;
                look.edges.fade = 0.7f;
                look.edges.outwardDrift = 0.012f;
                look.edges.dissolve = 0.6f;
                look.edges.softness = 1f;
                look.instability.lag = 0.2f;
                look.instability.trails = 0.1f;
                look.instability.overshoot = 0.05f;
                look.instability.jitter = 0.2f;
                look.mist.amount = 0.25f;
                look.mist.distance = 0.09f;
                look.mist.opacity = 0.25f;
                look.mist.size = 2f;
                look.mist.rise = 1.2f;
                look.projector.baseGlow = 1f;
                look.projector.coneIntensity = 0.6f;
                look.projector.beamCount = 8;
                look.projector.beamStrength = 0.6f;
                look.projector.assemblySweep = 0.4f;
                look.particleSize = 1.25f;
                look.particleSharpness = 0.45f;
                look.particleFlicker = 0.03f;
                look.scanlines.spacing = 0.02f;
                look.scanlines.thickness = 0.3f;
                look.scanlines.speed = 0.015f;
                look.scanlines.intensity = 0.15f;
                look.glitches.frequency = 0.05f;
                look.glitches.intensity = 0.2f;
                look.glitches.displacement = 0.006f;
                look.glitches.dropout = 0.05f;
                look.blending = Blending.Additive;
                break;
            case Preset.UnstableTransmission:
                look.appearance.hologramColor = new Color(0.2f, 0.9f, 1f);
                look.appearance.contrast = 1.25f;
                look.appearance.shadowBrightness = 0.25f;
                look.appearance.highlightIntensity = 1.4f;
                look.transparency.shadowOpacity = 0.3f;
                look.glow.glowIntensity = 0.7f;
                look.glow.highlightGlow = 1.4f;
                look.instability.drift = 0.006f;
                look.edges.outwardDrift = 0.012f;
                look.edges.hoverSpeed = 0.6f;
                look.edges.dissolve = 0.6f;
                look.instability.lag = 0.45f;
                look.instability.lagTime = 0.14f;
                look.instability.trails = 0.3f;
                look.instability.trailLength = 0.16f;
                look.instability.overshoot = 0.15f;
                look.instability.jitter = 0.7f;
                look.mist.amount = 0.2f;
                look.projector.beamCount = 18;
                look.projector.beamStrength = 1.4f;
                look.projector.assemblySweep = 0.9f;
                look.projector.sweepPeriod = 2f;
                look.projector.preAssemblyScatter = 0.7f;
                look.particleFlicker = 0.15f;
                look.scanlines.spacing = 0.012f;
                look.scanlines.speed = 0.06f;
                look.scanlines.intensity = 0.45f;
                look.glitches.frequency = 1.2f;
                look.glitches.duration = 0.15f;
                look.glitches.intensity = 0.85f;
                look.glitches.displacement = 0.03f;
                look.glitches.dropout = 0.35f;
                break;
            case Preset.CleanDigitalProjection:
                look.appearance.hologramColor = new Color(0.35f, 0.85f, 1f);
                look.appearance.brightness = 0f;
                look.appearance.contrast = 1.05f;
                look.appearance.shadowBrightness = 0.22f;
                look.appearance.highlightIntensity = 1.1f;
                look.transparency.opacity = 0.95f;
                look.transparency.shadowOpacity = 0.6f;
                look.transparency.opacityContrast = 1f;
                look.glow.glowIntensity = 0.15f;
                look.glow.highlightGlow = 0.2f;
                look.glow.glowThreshold = 0.8f;
                look.glow.emissionColorInfluence = 1f;
                look.instability.drift = 0.0015f;
                look.edges.width = 0.02f;
                look.edges.fade = 0.35f;
                look.edges.outwardDrift = 0.003f;
                look.edges.dissolve = 0.15f;
                look.edges.softness = 0.5f;
                look.instability.lag = 0.08f;
                look.instability.lagTime = 0.05f;
                look.instability.trails = 0.04f;
                look.instability.overshoot = 0.02f;
                look.instability.jitter = 0.1f;
                look.mist.amount = 0.04f;
                look.mist.opacity = 0.2f;
                look.projector.baseGlow = 1.2f;
                look.projector.coneIntensity = 0.3f;
                look.projector.beamCount = 6;
                look.projector.beamStrength = 0.7f;
                look.projector.assemblySweep = 0.3f;
                look.projector.preAssemblyScatter = 0.1f;
                look.particleSize = 1.1f;
                look.particleSharpness = 1f;
                look.particleFlicker = 0f;
                look.scanlines.intensity = 0.1f;
                look.glitches.frequency = 0.03f;
                look.glitches.intensity = 0.2f;
                look.glitches.displacement = 0.005f;
                look.glitches.dropout = 0.03f;
                look.blending = Blending.Alpha;
                break;
        }
        return look;
    }

    private static bool SameLook(Look a, Look b)
    {
        return JsonUtility.ToJson(a) == JsonUtility.ToJson(b);
    }

    private void OnValidate()
    {
        if (preset != appliedPreset_)
        {
            if (appliedPreset_ == Preset.Custom)
            {
                customLook_ = CaptureLook();
                hasCustomLook_ = true;
            }
            if (preset != Preset.Custom)
            {
                ApplyLook(PresetLook(preset));
            }
            else if (hasCustomLook_)
            {
                ApplyLook(customLook_);
            }
            appliedPreset_ = preset;
        }
        else if (preset != Preset.Custom && !SameLook(CaptureLook(), PresetLook(preset)))
        {
            // A value was edited after picking a preset: keep the edit and treat the result as custom.
            preset = Preset.Custom;
            appliedPreset_ = Preset.Custom;
        }
    }

    [ContextMenu("Reapply Selected Preset")]
    private void ReapplyPreset()
    {
        if (preset != Preset.Custom)
        {
            ApplyLook(PresetLook(preset));
        }
        else if (hasCustomLook_)
        {
            ApplyLook(customLook_);
        }
    }
}
