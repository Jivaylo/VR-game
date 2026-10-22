// Sci-fi hologram look for the Record3D point cloud, driven by VolumetricHologramController.
// Extends Record3DHologram.shader (same geometry, clipping and screen-space sizing) with luminance-mapped
// monochrome colour, translucency, local-space scanlines, glitches, HDR glow for URP Bloom, and an unstable
// "being reconstructed" quality: a soft silhouette that dissolves into hovering light, vapour drift, motion
// lag/trails/overshoot driven by a coarse motion field, an upward assembly sweep, a separate draw of loose
// particles (mist and motion copies), and a power-up/power-down collapse into the projector.
// Vertex/fragment only (no compute or geometry shaders), so it runs on desktop and Quest (Vulkan/GLES3, multiview).
Shader "Record3D/Volumetric Hologram"
{
    Properties
    {
        [NoScaleOffset] _PositionTex ("Position Texture (recording camera space)", 2D) = "black" {}
        [NoScaleOffset] _ColorTex ("Color Texture", 2D) = "white" {}
        [NoScaleOffset] _MaskTex ("Subject Mask", 2D) = "white" {}
        [NoScaleOffset] _MotionTex ("Motion Field (local units per second)", 2D) = "black" {}
        [NoScaleOffset] _EdgeTex ("Edge Field (r = distance inside, gb = outward direction)", 2D) = "white" {}
        _DepthRange ("Depth (near, far, edge offset, edge threshold)", Vector) = (0, 5, 4, 0)
        _SizeParams ("Size (size, spacing per m, object scale, mirror x)", Vector) = (1.15, 0.003, 1, 1)
        _PointParams ("Point (min px, max px, sharpness, round)", Vector) = (1.5, 32, 0.8, 0)
        _HoloColor ("Hologram hue (rgb, max 1) and intensity (a)", Vector) = (0.3, 0.9, 1, 1)
        _ToneParams ("Tone (brightness, contrast, luminance influence, colour intensity)", Vector) = (0, 1, 1, 1)
        _LevelsParams ("Auto levels (dark point, 1 / range, amount)", Vector) = (0, 1, 0, 0)
        _RampParams ("Ramp (shadow brightness, highlight intensity)", Vector) = (0.3, 1.2, 0, 0)
        _OpacityParams ("Opacity (overall, shadow, highlight, contrast)", Vector) = (0.9, 0.35, 1, 1.2)
        _GlowParams ("Glow (intensity, highlight glow, threshold, colour influence)", Vector) = (0.6, 1, 0.5, 0.85)
        _ScanParams ("Scanlines (spacing, thickness, phase, intensity)", Vector) = (0.015, 0.35, 0, 0)
        _ScanDir ("Scanline axis (local)", Vector) = (0, 1, 0, 0)
        _GlitchBand ("Glitch band (centre y, half height, shift, strength)", Vector) = (0, 0, 0, 0)
        _GlitchParams ("Glitch (dropout, slot, seed, flicker)", Vector) = (0, 0, 0, 0)
        _OutputParams ("Output (mode, prepass cutoff, flicker slot, hard edges)", Vector) = (2, 0.05, 0, 0)
        _CropMin ("Crop min (xyz, enabled)", Vector) = (-100, -100, -100, 0)
        _CropMax ("Crop max (xyz, mask enabled)", Vector) = (100, 100, 100, 0)
        _MaskScale ("Mask texels per colour texel", Vector) = (1, 1, 0, 0)
        _HoloTime ("Time (seconds, wrapped)", Vector) = (0, 0, 0, 0)
        _DriftParams ("Drift (amount, jitter, speed)", Vector) = (0.004, 0.3, 0.7, 0)
        _EdgeParams ("Edges (outward drift, fade, dissolve, hover speed)", Vector) = (0.008, 0.55, 0.45, 0.35)
        _EdgeParams2 ("Edges (softness, mist edge bias, field available)", Vector) = (0.8, 0.85, 0, 0)
        _MotionParams ("Motion (lag share, lag seconds, overshoot share, overshoot seconds)", Vector) = (0.25, 0.08, 0.08, 0.04)
        _TrailParams ("Trails (share, seconds, width, max px)", Vector) = (0.15, 0.1, 0.5, 160)
        _AssemblyParams ("Assembly sweep (y, half height, intensity, scatter above)", Vector) = (0, 0.02, 0, 0)
        _MistParams ("Mist (share, distance, size, opacity)", Vector) = (0.12, 0.06, 0.7, 0.4)
        _MistParams2 ("Loose layer (is loose-particle draw, mist rise, mist cycles per second)", Vector) = (0, 0.8, 0.35, 0)
        _PowerParams ("Power (collapse, visibility)", Vector) = (0, 1, 0, 0)
        _EmitterParams ("Projector emitter (centre xyz local, cone height)", Vector) = (0, -0.6, -0.75, 1)

        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 10
        [HideInInspector] _ZWrite ("__zw", Float) = 0
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

    TEXTURE2D_FLOAT(_PositionTex);
    TEXTURE2D(_ColorTex);
    TEXTURE2D(_MaskTex);
    TEXTURE2D(_MotionTex);
    SAMPLER(sampler_MotionTex);
    TEXTURE2D(_EdgeTex);
    SAMPLER(sampler_EdgeTex);

    CBUFFER_START(UnityPerMaterial)
        float4 _PositionTex_TexelSize;
        float4 _DepthRange;
        float4 _SizeParams;
        float4 _PointParams;
        float4 _HoloColor;
        float4 _ToneParams;
        float4 _LevelsParams;
        float4 _RampParams;
        float4 _OpacityParams;
        float4 _GlowParams;
        float4 _ScanParams;
        float4 _ScanDir;
        float4 _GlitchBand;
        float4 _GlitchParams;
        float4 _OutputParams;
        float4 _CropMin;
        float4 _CropMax;
        float4 _MaskScale;
        float4 _HoloTime;
        float4 _DriftParams;
        float4 _EdgeParams;
        float4 _EdgeParams2;
        float4 _MotionParams;
        float4 _TrailParams;
        float4 _AssemblyParams;
        float4 _MistParams;
        float4 _MistParams2;
        float4 _PowerParams;
        float4 _EmitterParams;
    CBUFFER_END

    struct Attributes
    {
        float4 positionOS : POSITION; // xy = texel coordinate, zw = quad corner (-1 or +1)
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct Varyings
    {
        float4 positionCS : SV_POSITION;
        half4 baseAlpha : COLOR0;   // hologram colour and opacity before the particle's edge falloff
        half3 emission : TEXCOORD1; // HDR glow added on top
        float2 corner : TEXCOORD0;
        half3 flags : TEXCOORD2;    // x = streak (fades towards its tail), y = never in the depth prepass, z = soft round glow (0-1)
        UNITY_VERTEX_OUTPUT_STEREO
    };

    static const half3 kLuminance = half3(0.2126, 0.7152, 0.0722);

    uint HoloHash(uint x)
    {
        x ^= x >> 16;
        x *= 0x7feb352du;
        x ^= x >> 15;
        x *= 0x846ca68bu;
        x ^= x >> 16;
        return x;
    }

    float HoloHash01(uint a, uint b, uint c)
    {
        return (HoloHash(a * 0x8da6b343u ^ b * 0xd8163841u ^ c * 0xcb1ab31fu) >> 8) * (1.0 / 16777216.0);
    }

    // Depth difference to a neighbouring sample; neighbours without depth (NaN) never count as an edge.
    float NeighbourDepthDelta(int2 texel, float depth)
    {
        float neighbourDepth = -LOAD_TEXTURE2D(_PositionTex, texel).z;
        return neighbourDepth > 0.0 ? abs(neighbourDepth - depth) : 0.0;
    }

    // Smooth silhouette field computed by the controller each frame: x = 1 at the silhouette falling to 0
    // in the coherent core, yz = direction pointing out of the form (local x/y).
    float3 EdgeField(int2 texel)
    {
        if (_EdgeParams2.z < 0.5)
        {
            return float3(0.0, 0.0, 1.0);
        }
        half4 field = SAMPLE_TEXTURE2D_LOD(_EdgeTex, sampler_EdgeTex, (float2(texel) + 0.5) * _PositionTex_TexelSize.xy, 0);
        float edge = 1.0 - field.r;
        float2 outward = field.gb * 2.0 - 1.0;
        outward.y = -outward.y; // image rows run down, local y runs up
        outward.x *= _SizeParams.w;
        return float3(edge * edge * (3.0 - 2.0 * edge), outward / max(length(outward), 1e-3));
    }

    // Smooth, slowly varying wobble for the vapour drift (roughly unit amplitude).
    float3 DriftNoise(float3 p, float phase, float time)
    {
        float3 q = p * 11.0 + phase * 6.2831853;
        return float3(
            sin(q.y * 1.7 + time * 1.3 + phase * 4.1) + 0.5 * sin(q.z * 3.1 - time * 2.3),
            sin(q.z * 1.3 + time * 1.1 + phase * 2.7) + 0.5 * sin(q.x * 2.9 + time * 1.9),
            sin(q.x * 1.5 + time * 0.9 + phase * 5.3) + 0.5 * sin(q.y * 2.3 - time * 1.7)) * (1.0 / 1.5);
    }

    Varyings CullVertex(Varyings output)
    {
        output.positionCS = float4(asfloat(~0u), 0.0, 0.0, 1.0); // NaN culls the quad, as VFX Graph does
        return output;
    }

    // Roles in the loose-particle layer. The body itself is never moved far, so it stays readable;
    // everything that leaves the surface is an extra copy drawn on top.
    #define ROLE_BODY 0
    #define ROLE_TRAIL 1
    #define ROLE_LAG 2
    #define ROLE_OVERSHOOT 3
    #define ROLE_MIST 4

    Varyings HologramVertex(Attributes input)
    {
        Varyings output = (Varyings)0;
        UNITY_SETUP_INSTANCE_ID(input);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

        int2 texel = (int2)input.positionOS.xy;
        float2 corner = input.positionOS.zw;

        // Record3D positions are in the recording camera's frame (x right, y up, looking down -Z), so the
        // recorded depth is -z regardless of how the prefab is placed. Everything below works in this
        // local space, so every effect follows the prefab's position, rotation and scale.
        float3 positionOS = LOAD_TEXTURE2D(_PositionTex, texel).xyz;
        float depth = -positionOS.z;

        // Written so that NaN (no depth sample) fails both comparisons.
        bool visible = depth >= _DepthRange.x && depth <= _DepthRange.y;

        if (_DepthRange.w > 0.0)
        {
            int2 maxTexel = int2(_PositionTex_TexelSize.zw) - 1;
            int2 offset = int2((int)_DepthRange.z, 0);
            float delta = max(
                max(NeighbourDepthDelta(clamp(texel - offset.xy, 0, maxTexel), depth),
                    NeighbourDepthDelta(clamp(texel + offset.xy, 0, maxTexel), depth)),
                max(NeighbourDepthDelta(clamp(texel - offset.yx, 0, maxTexel), depth),
                    NeighbourDepthDelta(clamp(texel + offset.yx, 0, maxTexel), depth)));
            visible = visible && delta <= _DepthRange.w * depth;
        }

        positionOS.x *= _SizeParams.w;

        if (_CropMin.w > 0.0)
        {
            visible = visible && all(positionOS >= _CropMin.xyz) && all(positionOS <= _CropMax.xyz);
        }
        if (_CropMax.w > 0.0)
        {
            visible = visible && LOAD_TEXTURE2D(_MaskTex, int2(float2(texel) * _MaskScale.xy)).r > 0.5;
        }

        if (!visible)
        {
            return CullVertex(output);
        }

        bool looseLayer = _MistParams2.x > 0.5;
        float time = _HoloTime.x;
        uint seed = (uint)_GlitchParams.z;
        uint particleId = (uint)texel.x * 0x27d4eb2du ^ (uint)texel.y * 0x165667b1u;
        float phase = HoloHash01(particleId, seed, 31u);
        float phase2 = HoloHash01(particleId, seed, 37u);
        float3 randomDir = float3(HoloHash01(particleId, seed, 41u), HoloHash01(particleId, seed, 43u), HoloHash01(particleId, seed, 47u)) * 2.0 - 1.0;
        float particleLocalSize = _SizeParams.x * depth * _SizeParams.y;

        // Silhouette: 1 at the outline, 0 in the coherent core, plus the direction out of the form.
        float3 edgeField = EdgeField(texel);
        float edge = edgeField.x;
        float3 outward = float3(edgeField.yz, 0.0);
        // Slow hover: edge matter breathes in and out instead of jittering.
        float hover = 0.65 + 0.35 * sin(time * _EdgeParams.w * 6.2831853 + phase * 6.2831853);

        // Motion field (local units per second): where the recorded subject is moving.
        float3 velocity = SAMPLE_TEXTURE2D_LOD(_MotionTex, sampler_MotionTex, (float2(texel) + 0.5) * _PositionTex_TexelSize.xy, 0).xyz;
        float speed = length(velocity);

        // Upward assembly sweep: particles in the band light up, the zone just above it is unsettled.
        float sweepDistance = positionOS.y - _AssemblyParams.x;
        half sweepBand = _AssemblyParams.z * (1.0 - smoothstep(0.0, _AssemblyParams.y, abs(sweepDistance)));
        float preAssembly = _AssemblyParams.w * saturate(1.0 - sweepDistance / (_AssemblyParams.y * 6.0)) * step(0.0, sweepDistance);

        int role = ROLE_BODY;
        float3 tailOS = positionOS;
        half looseAlpha = 1.0;
        half softness = 0.0;
        half edgeAlpha = 1.0;
        float glowScale = 1.0;
        bool skipDepth = looseLayer;
        float3 drift = DriftNoise(positionOS, phase, time * _DriftParams.z);
        if (looseLayer)
        {
            // Each particle may shed one extra copy. Copies that follow motion only appear where the
            // subject moves; roles reshuffle slowly over time.
            float pick = frac(HoloHash01(particleId, seed, 53u) + time * 0.15);
            float amount = HoloHash01(particleId, seed, 59u);
            float moving = saturate(speed / 0.25);
            float trailEnd = _TrailParams.x * moving;
            float lagEnd = trailEnd + _MotionParams.x * moving;
            float overshootEnd = lagEnd + _MotionParams.z * moving;
            if (pick < trailEnd)
            {
                role = ROLE_TRAIL; // a light streak from where the motion was to the surface
                tailOS = positionOS - velocity * (_TrailParams.y * (0.5 + 0.5 * amount));
                looseAlpha = 0.8 * moving;
            }
            else if (pick < lagEnd)
            {
                role = ROLE_LAG; // left behind, catching up
                positionOS -= velocity * (_MotionParams.y * (0.3 + 0.7 * amount));
                looseAlpha = 0.6 * moving;
            }
            else if (pick < overshootEnd)
            {
                role = ROLE_OVERSHOOT; // ran ahead of the motion
                positionOS += velocity * (_MotionParams.w * (0.3 + 0.7 * amount));
                looseAlpha = 0.5 * moving;
            }
            else
            {
                // Mist: mostly shed from the silhouette, floating outward and up the projection, hovering
                // and fading, so the outline dissolves into projected light.
                float share = _MistParams.x * lerp(1.0 - _EdgeParams2.y, 1.0, edge);
                if (amount >= share)
                {
                    return CullVertex(output);
                }
                role = ROLE_MIST;
                float life = frac(phase + time * _MistParams2.z);
                float3 away = normalize(lerp(randomDir, outward * 1.5, edge) + float3(0.0, _MistParams2.y, 0.0) + 1e-4);
                positionOS += away * (_MistParams.y * life) + drift * (_MistParams.y * 0.2);
                looseAlpha = sin(life * PI);
                softness = 1.0;
            }
            // Loose copies float more freely than the body.
            float3 looseDrift = drift * (1.5 * _DriftParams.x);
            positionOS += looseDrift;
            tailOS += looseDrift;
        }
        else
        {
            // The core only wobbles within the overlap between neighbouring particles, so it never opens holes.
            float wobble = min(_DriftParams.x, 0.12 * particleLocalSize) * (0.3 + 0.7 * phase2) * (1.0 + 3.0 * preAssembly);
            positionOS += drift * wobble;
            float jitter = HoloHash01(particleId, (uint)(time * 30.0), seed + 3u);
            positionOS += randomDir * (min(speed * _DriftParams.y * 0.02, 0.15 * particleLocalSize) * jitter);

            // Edges: the form is least stable here. Particles drift slowly outward and hover, turn into soft
            // glows and fade, and the outermost ones detach into floating light. No jagged displacement.
            float edgeWeight = edge * edge;
            positionOS += (outward + float3(0.0, 0.25, 0.0)) * (_EdgeParams.x * edgeWeight * hover);
            positionOS += drift * (_EdgeParams.x * 0.35 * edgeWeight);
            float detach = saturate((_EdgeParams.z * edgeWeight - phase2) * 8.0);
            positionOS += (outward + float3(0.0, 0.4, 0.0)) * (_EdgeParams.x * 3.0 * detach * hover);
            softness = saturate(_EdgeParams2.x * (edge + detach));
            // Edge matter grows into larger, fainter glows that overlap into haze; tiny hard dots would read as noise.
            glowScale = 1.0 + _EdgeParams2.x * (1.6 * detach + 0.6 * edgeWeight);
            edgeAlpha = (1.0 - _EdgeParams.y * pow(edge, 1.5)) * (1.0 - 0.45 * detach) / glowScale;
            skipDepth = detach > 0.5;
        }

        uint groupId = (uint)(texel.x >> 3) * 0x9e3779b1u ^ (uint)(texel.y >> 3) * 0x85ebca77u;
        half brightness = 1.0 + sweepBand;
        half alphaScale = (1.0 - 0.35 * preAssembly) * edgeAlpha;
        half interference = 0.0;

        // Glitch event (scheduled by the controller): one horizontal band shifts sideways in torn slices,
        // groups of particles drop out, and the band shows interference stripes.
        float glitchStrength = _GlitchBand.w;
        if (glitchStrength > 0.0)
        {
            uint slot = (uint)_GlitchParams.y;
            float band = glitchStrength * (1.0 - smoothstep(_GlitchBand.y * 0.5, _GlitchBand.y, abs(positionOS.y - _GlitchBand.x)));
            float sliceHeight = max(_GlitchBand.y * 0.35, 1e-4);
            float slice = floor(positionOS.y / sliceHeight);
            float jitterSlice = HoloHash01((uint)(int)slice, slot, seed) * 2.0 - 1.0;
            float shift = _GlitchBand.z * band * (0.65 + 0.35 * jitterSlice);
            positionOS.x += shift;
            tailOS.x += shift;

            float dropChance = _GlitchParams.x * (band + 0.05 * glitchStrength);
            if (HoloHash01(groupId, slot, seed + 1u) < dropChance)
            {
                return CullVertex(output);
            }

            // Interference stripes light up in the hologram colour, so the band reads on dark clothing too.
            float stripes = frac(positionOS.y / sliceHeight * 4.0 + HoloHash01(slot, seed, 7u));
            brightness *= 1.0 + band * (stripes > 0.5 ? 0.5 : -0.35);
            brightness *= 1.0 + glitchStrength * 0.2 * (HoloHash01(slot, seed, 3u) * 2.0 - 1.0);
            interference = band * (stripes > 0.5 ? 0.35 : 0.1);
        }

        if (_GlitchParams.w > 0.0)
        {
            float flicker = HoloHash01((uint)texel.x * 0x27d4eb2du ^ (uint)texel.y, (uint)_OutputParams.z, seed + 11u);
            half dim = _GlitchParams.w * flicker * flicker;
            brightness *= 1.0 - dim;
            alphaScale *= 1.0 - 0.5 * dim;
        }

        // Power down / up: everything is pulled down into the projector's emitter, staggered per particle,
        // streaking as it falls and flaring with energy.
        half energy = 0.0;
        bool streak = role == ROLE_TRAIL;
        float collapse = _PowerParams.x;
        if (collapse > 0.0)
        {
            float progress = saturate((collapse - phase * 0.35) / 0.65);
            float ease = progress * progress;
            float3 emitter = _EmitterParams.xyz;
            float fallen = (positionOS.y - emitter.y) * ease;
            positionOS.y -= fallen;
            positionOS.xz = lerp(positionOS.xz, emitter.xz, ease * ease);
            positionOS += randomDir * (particleLocalSize * 4.0 * progress * (1.0 - progress));
            tailOS = positionOS + float3(0.0, max(fallen, 0.0) * 0.3 + _EmitterParams.w * 0.05 * progress, 0.0);
            streak = progress > 0.02 && progress < 0.98;
            energy = 4.0 * progress * (1.0 - progress);
            softness = max(softness, progress);
            skipDepth = true;
        }

        // Size: a multiple of the gap between neighbouring samples, clamped in pixels for this eye/camera.
        float sizeScale = role == ROLE_MIST ? _MistParams.z : role == ROLE_BODY ? glowScale : 0.8;
        float sizeLocal = particleLocalSize * sizeScale;
        float4 positionCS = TransformWorldToHClip(TransformObjectToWorld(positionOS));
        float pixelsPerUnit = abs(UNITY_MATRIX_P[1][1]) * 0.5 * _ScaledScreenParams.y / max(positionCS.w, 1e-5);
        float sizePixels = clamp(sizeLocal * _SizeParams.z * pixelsPerUnit, _PointParams.x, _PointParams.y);

        // Expand the quad on screen; streaks become lines from the tail to the particle.
        float2 halfScreen = 0.5 * _ScaledScreenParams.xy;
        float2 offsetPixels = corner * (0.5 * sizePixels);
        half isStreak = 0.0;
        if (streak)
        {
            float4 tailCS = TransformWorldToHClip(TransformObjectToWorld(tailOS));
            if (tailCS.w > 1e-4 && positionCS.w > 1e-4)
            {
                float2 along = (positionCS.xy / positionCS.w - tailCS.xy / tailCS.w) * halfScreen;
                float alongLength = length(along);
                float streakPixels = min(alongLength, _TrailParams.w);
                if (streakPixels > sizePixels)
                {
                    float2 direction = along / alongLength;
                    float2 side = float2(-direction.y, direction.x);
                    offsetPixels = direction * ((corner.x * 0.5 - 0.5) * streakPixels + corner.x * 0.5 * sizePixels)
                                 + side * (corner.y * 0.5 * sizePixels * _TrailParams.z);
                    isStreak = 1.0;
                }
            }
        }
        positionCS.xy += offsetPixels * positionCS.w / halfScreen;

        // Monochrome colour: the recorded RGB only provides brightness, which picks a shade of the hologram hue.
        half3 recorded = LOAD_TEXTURE2D(_ColorTex, texel).rgb;
        half lightness = pow(max(dot(recorded, kLuminance), 0.0), 1.0 / 2.2); // perceptual 0-1
        // Auto Levels: stretch the subject's measured brightness range over the whole ramp.
        lightness = lerp(lightness, saturate((lightness - _LevelsParams.x) * _LevelsParams.y), _LevelsParams.z);
        half t = lerp(0.5, lightness, _ToneParams.z);
        t = saturate((t - 0.5) * _ToneParams.y + 0.5 + _ToneParams.x);

        // Scanlines along a local axis, scrolling with time.
        if (_ScanParams.w > 0.0)
        {
            float scanPhase = frac(dot(positionOS, _ScanDir.xyz) / _ScanParams.x - _ScanParams.z);
            float distanceToLine = min(scanPhase, 1.0 - scanPhase) * 2.0; // 0 on a line, 1 halfway between lines
            half onLine = 1.0 - smoothstep(_ScanParams.y, _ScanParams.y + 0.15, distanceToLine);
            brightness *= lerp(1.0 - 0.45 * _ScanParams.w, 1.0 + 0.6 * _ScanParams.w, onLine);
            alphaScale *= lerp(1.0 - 0.5 * _ScanParams.w, 1.0, onLine);
        }

        // Shadows keep at least Shadow Brightness, mid-tones take the hue, highlights turn pale and brighter.
        half3 tint = lerp(half3(1.0, 1.0, 1.0), (half3)_HoloColor.rgb, _ToneParams.w);
        half3 hue = lerp(tint, half3(1.0, 1.0, 1.0), smoothstep(0.6, 1.0, t) * 0.35);
        half light = lerp(_RampParams.x, 1.0, t);
        half highlight = lerp(1.0, _RampParams.y, smoothstep(0.5, 1.0, t));
        half3 baseColor = hue * pow(max(light, 0.0), 2.2) * highlight * _HoloColor.a * brightness * (1.0 + 1.5 * energy);

        // Darker areas are more transparent than brighter ones.
        half opacityT = saturate((t - 0.5) * _OpacityParams.w + 0.5);
        half alpha = _OpacityParams.x * lerp(_OpacityParams.y, _OpacityParams.z, opacityT) * alphaScale;

        // Glow grows with brightness, plus extra above the threshold; values above 1 feed URP Bloom.
        half glowMask = smoothstep(_GlowParams.z, 1.0, t);
        half3 glowTint = lerp(dot(baseColor, kLuminance).xxx, baseColor, _GlowParams.w);
        half3 emission = glowTint * (_GlowParams.x * t + _GlowParams.y * glowMask)
                       + tint * ((interference + 0.5 * sweepBand + 0.6 * energy) * _HoloColor.a);

        if (role == ROLE_MIST)
        {
            // Mist is light, not surface: an even, faint shade of the hue whatever the recorded brightness.
            baseColor = tint * (0.25 + 0.5 * t) * _HoloColor.a * (1.0 + energy);
            emission = baseColor * 0.5;
            alpha = _MistParams.w * looseAlpha;
        }
        else if (looseLayer)
        {
            // Motion copies keep the surface's shade, a little dimmer, so they read as smeared geometry.
            emission *= 0.5;
            alpha *= looseAlpha;
        }
        alpha *= _PowerParams.y;

        output.positionCS = positionCS;
        output.baseAlpha = half4(baseColor, saturate(alpha));
        output.emission = emission * _PowerParams.y;
        output.corner = corner;
        output.flags = half3(isStreak, skipDepth ? 1.0 : 0.0, softness);
        return output;
    }

    // 1 inside the particle, falling to 0 at its edge; Particle Sharpness sets how late the falloff starts.
    // When the colour pass writes depth, soft edges would hide neighbours and leave seams, so edges are hard.
    // Soft particles (silhouette, mist, collapsing) blend towards a round glow.
    half PointCoverage(float2 corner, half3 flags)
    {
        float radius = _PointParams.w > 0.5 ? length(corner) : max(abs(corner.x), abs(corner.y));
        half coverage;
        if (_OutputParams.w > 0.5)
        {
            coverage = radius < 1.0 ? 1.0 : 0.0;
        }
        else
        {
            coverage = saturate((1.0 - radius) / max(1.0 - _PointParams.z, 1e-3));
        }
        if (flags.z > 0.0)
        {
            half glow = saturate(1.0 - length(corner));
            coverage = lerp(coverage, glow * glow, flags.z);
        }
        if (flags.x > 0.5)
        {
            coverage *= pow(saturate(corner.x * 0.5 + 0.5), 1.5); // streaks fade out towards their tail
        }
        return coverage;
    }

    half4 HologramFragment(Varyings input) : SV_Target
    {
        half coverage = PointCoverage(input.corner, input.flags);
        half alpha = input.baseAlpha.a * coverage;
        half3 baseColor = input.baseAlpha.rgb;
        int mode = (int)_OutputParams.x;

        if (mode == 3) // Opaque
        {
            clip(coverage - 0.5);
            return half4(baseColor + input.emission, 1.0);
        }

        clip(alpha - 0.002);
        if (mode == 2) // Hybrid: the body blends over the scene, the glow adds on top of it
        {
            return half4(baseColor * alpha + input.emission * coverage * _OpacityParams.x, alpha);
        }
        return half4((baseColor + input.emission) * alpha, alpha); // Alpha and Additive (premultiplied)
    }

    // Only the solid core of each body particle claims the pixel: its faded edge must still let the neighbour
    // underneath draw, otherwise the edges show up as dark seams. Loose, detached and streaking particles
    // never claim pixels.
    half4 DepthPrepassFragment(Varyings input) : SV_Target
    {
        clip(0.5 - input.flags.y);
        clip(PointCoverage(input.corner, input.flags) - 0.999);
        clip(input.baseAlpha.a - _OutputParams.y);
        return 0;
    }
    ENDHLSL

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        // Front Surface Only sorting: writes the nearest particle's depth first so the colour pass blends
        // just the front layer once per pixel (no overdraw build-up, no order artefacts). Toggled from C#.
        Pass
        {
            Name "HologramDepthPrepass"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HologramVertex
            #pragma fragment DepthPrepassFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "Hologram"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull Off
            // Keeps the colour pass from losing the depth test against its own prepass depth.
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex HologramVertex
            #pragma fragment HologramFragment
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }

    Fallback Off
}
