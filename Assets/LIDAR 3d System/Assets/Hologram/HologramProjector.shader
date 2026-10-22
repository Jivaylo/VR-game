// Projection source for the Record3D hologram, driven by VolumetricHologramController: a glowing emitter disc,
// a light cone rising from it and laser-like beams that scan the hologram, plus the upward assembly sweep.
// Powering down retracts the cone and beams into the emitter, which flares and then goes dark.
// One procedural mesh holds all three parts (the first vertex component says which); everything is computed
// in the hologram's local space so the projector moves, rotates and scales with the prefab.
// Additive, no depth writes, vertex/fragment only: works on desktop and Quest (Vulkan/GLES3, multiview).
Shader "Record3D/Hologram Projector"
{
    Properties
    {
        _ProjBase ("Emitter centre (xyz, local) and radius", Vector) = (0, -0.5, -0.75, 0.15)
        _ProjShape ("Cone (height, top radius, time, sweep height 0-1)", Vector) = (1, 0.35, 0, 0)
        _ProjColor ("Hue (rgb) and intensity", Vector) = (0.3, 0.9, 1, 1)
        _ProjParams ("Base glow, cone, beam count, beam strength", Vector) = (1.5, 0.45, 12, 1)
        _ProjParams2 ("Sweep intensity, seed, beam width px", Vector) = (0.6, 1, 2, 0)
        _ProjTargetMin ("Beam target volume min (local)", Vector) = (-0.3, -0.5, -0.9, 0)
        _ProjTargetMax ("Beam target volume max (local)", Vector) = (0.3, 0.4, -0.6, 0)
        _ProjPower ("Power (visibility, collapse, flash)", Vector) = (1, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "HologramProjector"
            Tags { "LightMode" = "UniversalForward" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ProjectorVertex
            #pragma fragment ProjectorFragment
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _ProjBase;
                float4 _ProjShape;
                float4 _ProjColor;
                float4 _ProjParams;
                float4 _ProjParams2;
                float4 _ProjTargetMin;
                float4 _ProjTargetMax;
                float4 _ProjPower;
            CBUFFER_END

            struct Attributes
            {
                float4 data : POSITION; // x = part (0 cone, 1 emitter, 2 beam), yzw = part parameters
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                float part : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            uint ProjectorHash(uint x)
            {
                x ^= x >> 16;
                x *= 0x7feb352du;
                x ^= x >> 15;
                x *= 0x846ca68bu;
                x ^= x >> 16;
                return x;
            }

            float ProjectorHash01(uint a, uint b, uint c)
            {
                return (ProjectorHash(a * 0x8da6b343u ^ b * 0xd8163841u ^ c * 0xcb1ab31fu) >> 8) * (1.0 / 16777216.0);
            }

            // exp(-x^2), written without pow() because pow of a negative base is NaN on some mobile GPUs.
            float Gaussian(float x)
            {
                return exp(-x * x);
            }

            // A point inside the hologram that beam `id` aims at during time slot `slot` (biased upwards).
            float3 BeamTarget(uint id, uint slot, uint seed)
            {
                float3 r = float3(ProjectorHash01(id, slot, seed + 2u), ProjectorHash01(id, slot, seed + 3u), ProjectorHash01(id, slot, seed + 4u));
                r.y = 0.25 + 0.75 * r.y;
                return lerp(_ProjTargetMin.xyz, _ProjTargetMax.xyz, r);
            }

            Varyings ProjectorVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                int part = (int)(input.data.x + 0.5);
                float3 centre = _ProjBase.xyz;
                float baseRadius = _ProjBase.w;
                // Powering down retracts the cone into the emitter.
                float collapse = _ProjPower.y;
                float height = _ProjShape.x * (1.0 - collapse);
                float topRadius = lerp(_ProjShape.y, baseRadius, collapse);
                float time = _ProjShape.z;
                output.part = part;

                if (part == 0)
                {
                    // Cone wall: u around, v up.
                    float u = input.data.y;
                    float v = input.data.z;
                    float angle = u * TWO_PI;
                    float3 outward = float3(cos(angle), 0.0, sin(angle));
                    float3 positionWS = TransformObjectToWorld(centre + outward * lerp(baseRadius, topRadius, v) + float3(0.0, v * height, 0.0));
                    float3 normalWS = normalize(TransformObjectToWorldDir(outward));
                    float rim = 1.0 - abs(dot(normalWS, GetWorldSpaceNormalizeViewDir(positionWS)));
                    output.positionCS = TransformWorldToHClip(positionWS);
                    output.uv = float4(u, v, rim, 0.0);
                }
                else if (part == 1)
                {
                    // Emitter disc in the local XZ plane; uv is in units of the emitter radius.
                    float2 xz = input.data.yz * 1.8;
                    output.positionCS = TransformObjectToHClip(centre + float3(xz.x, 0.0, xz.y) * baseRadius);
                    output.uv = float4(xz, 0.0, 0.0);
                }
                else
                {
                    // Beam: a screen-facing line from the emitter rim to a point in the hologram that is
                    // retargeted every few seconds, so the beams scan across the figure.
                    float index = input.data.y;
                    float along = input.data.z;
                    float side = input.data.w;
                    if (index >= _ProjParams.z)
                    {
                        output.positionCS = float4(asfloat(~0u), 0.0, 0.0, 1.0);
                        return output;
                    }
                    uint id = (uint)index;
                    uint seed = (uint)_ProjParams2.y;
                    float slotTime = time * 0.35 + ProjectorHash01(id, seed, 1u) * 10.0;
                    uint slot = (uint)slotTime;
                    float3 target = lerp(BeamTarget(id, slot, seed), BeamTarget(id, slot + 1u, seed), smoothstep(0.6, 1.0, frac(slotTime)));
                    float baseAngle = ProjectorHash01(id, seed, 5u) * TWO_PI + time * 0.2;
                    float3 bottom = centre + float3(cos(baseAngle), 0.0, sin(baseAngle)) * (baseRadius * 0.7);
                    target = lerp(target, bottom, collapse); // beams retract when powering down

                    float4 bottomCS = TransformObjectToHClip(bottom);
                    float4 targetCS = TransformObjectToHClip(target);
                    float4 positionCS = TransformObjectToHClip(lerp(bottom, target, along));
                    float2 screen = _ScaledScreenParams.xy;
                    float2 direction = (targetCS.xy / max(targetCS.w, 1e-4) - bottomCS.xy / max(bottomCS.w, 1e-4)) * screen;
                    direction /= max(length(direction), 1e-4);
                    float widthPixels = _ProjParams2.z * lerp(1.6, 0.7, along);
                    positionCS.xy += float2(-direction.y, direction.x) * side * widthPixels * positionCS.w / screen;
                    output.positionCS = positionCS;
                    output.uv = float4(along, side, ProjectorHash01(id, seed, 6u), 0.0);
                }
                return output;
            }

            half4 ProjectorFragment(Varyings input) : SV_Target
            {
                int part = (int)(input.part + 0.5);
                float time = _ProjShape.z;
                float value;

                if (part == 0)
                {
                    float u = input.uv.x;
                    float v = input.uv.y;
                    float rim = input.uv.z;
                    // Bright at the emitter, fading upwards; strongest where the wall is seen edge-on.
                    float fade = pow(saturate(1.0 - v), 1.5) * smoothstep(0.0, 0.06, v);
                    float streaks = 0.55 + 0.45 * sin(u * TWO_PI * 23.0 + time * 0.5) * sin(u * TWO_PI * 7.0 - time * 0.8);
                    float rising = 0.75 + 0.25 * sin((v * 5.0 - time * 1.2) * TWO_PI);
                    float sweep = _ProjParams2.x * Gaussian((v - _ProjShape.w) / 0.025);
                    value = _ProjParams.y * fade * (0.25 + 0.75 * rim * rim) * streaks * rising + sweep * (0.3 + 0.7 * rim);
                }
                else if (part == 1)
                {
                    float r = length(input.uv.xy); // 1 = emitter radius
                    float angle = atan2(input.uv.y, input.uv.x);
                    float ring = Gaussian((r - 1.0) / 0.06);
                    float ticks = step(0.5, frac(angle / TWO_PI * 16.0 + time * 0.3));
                    float innerRing = Gaussian((r - 0.55) / 0.04) * (0.5 + 0.5 * ticks);
                    float core = exp(-r * r * 6.0);
                    float halo = exp(-max(r - 1.0, 0.0) * 5.0) * 0.3;
                    float pulse = 0.85 + 0.15 * sin(time * 3.0);
                    value = _ProjParams.x * (ring + 0.7 * innerRing + 0.8 * core + halo) * pulse * saturate((1.8 - r) * 4.0);
                    // Power flash: the emitter flares as the hologram collapses into it (or ignites).
                    value += _ProjPower.z * (2.5 * core + ring) * saturate((1.8 - r) * 4.0);
                }
                else
                {
                    float along = input.uv.x;
                    float across = 1.0 - abs(input.uv.y);
                    float phase = input.uv.z;
                    // Light packets travel up each beam: the geometry is being sent upwards.
                    float packet = Gaussian((along - frac(time * 0.6 + phase)) / 0.05);
                    float flicker = 0.75 + 0.25 * sin(time * 9.0 + phase * 40.0);
                    float ends = smoothstep(0.0, 0.05, along) * (1.0 - smoothstep(0.85, 1.0, along));
                    value = _ProjParams.w * across * across * (0.35 * (1.0 - 0.7 * along) + packet) * flicker * ends;
                }

                return half4(_ProjColor.rgb * (_ProjColor.w * value * _ProjPower.x), 0.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
