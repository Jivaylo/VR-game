// Draws a Record3D frame as a point-cloud hologram: one camera-facing quad per sampled texel of the
// position texture written by Record3DPlayback. Driven by Record3DHologramRenderer; no compute shaders,
// geometry shaders or VFX Graph, so it runs on desktop and on Quest (Vulkan / GLES3, multiview).
Shader "Record3D/Hologram Points"
{
    Properties
    {
        [NoScaleOffset] _PositionTex ("Position Texture (recording camera space)", 2D) = "black" {}
        [NoScaleOffset] _ColorTex ("Color Texture", 2D) = "white" {}
        _DepthRange ("Depth Range (near, far) m", Vector) = (0, 5, 0, 0)
        _SizeParams ("Size (size, fixed mode, spacing per m, object scale)", Vector) = (1.25, 0, 0.003, 1)
        _ScreenSizeLimits ("Screen Size Limits px (min, max)", Vector) = (1.5, 32, 0, 0)
        _SampleParams ("Sampling (edge offset, edge threshold, mirror x)", Vector) = (4, 0, 1, 0)
        _HologramColor ("Hologram Color", Color) = (1, 1, 1, 1)
        _ColorParams ("Color (mono blend, glow, opacity)", Vector) = (0, 0, 1, 0)

        [HideInInspector] _SrcBlend ("__src", Float) = 5
        [HideInInspector] _DstBlend ("__dst", Float) = 10
        [HideInInspector] _ZWrite ("__zw", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "HologramPoints"

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #pragma multi_compile_instancing
            // multi_compile rather than shader_feature: the material is created at runtime, so
            // shader_feature variants would be stripped from player builds.
            #pragma multi_compile_local_fragment _ _SHAPE_CIRCLE _SHAPE_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_FLOAT(_PositionTex);
            TEXTURE2D(_ColorTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _PositionTex_TexelSize;
                float4 _DepthRange;
                float4 _SizeParams;
                float4 _ScreenSizeLimits;
                float4 _SampleParams;
                half4 _HologramColor;
                half4 _ColorParams;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION; // xy = texel coordinate, zw = quad corner (-1 or +1)
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 corner : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // Depth difference to a neighbouring sample; neighbours without depth (NaN) never count as an edge.
            float NeighbourDepthDelta(int2 texel, float depth)
            {
                float neighbourDepth = -LOAD_TEXTURE2D(_PositionTex, texel).z;
                return neighbourDepth > 0.0 ? abs(neighbourDepth - depth) : 0.0;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                int2 texel = (int2)input.positionOS.xy;
                float2 corner = input.positionOS.zw;

                // Record3D positions are in the recording camera's frame (ARKit convention: x right, y up,
                // looking down -Z), so the recorded depth is -z regardless of how the prefab is placed.
                float3 positionOS = LOAD_TEXTURE2D(_PositionTex, texel).xyz;
                float depth = -positionOS.z;

                // Written so that NaN (no depth sample) fails both comparisons.
                bool visible = depth >= _DepthRange.x && depth <= _DepthRange.y;

                if (_SampleParams.y > 0.0)
                {
                    int2 maxTexel = int2(_PositionTex_TexelSize.zw) - 1;
                    int2 offset = int2((int)_SampleParams.x, 0);
                    float delta = max(
                        max(NeighbourDepthDelta(clamp(texel - offset.xy, 0, maxTexel), depth),
                            NeighbourDepthDelta(clamp(texel + offset.xy, 0, maxTexel), depth)),
                        max(NeighbourDepthDelta(clamp(texel - offset.yx, 0, maxTexel), depth),
                            NeighbourDepthDelta(clamp(texel + offset.yx, 0, maxTexel), depth)));
                    visible = visible && delta <= _SampleParams.y * depth;
                }

                if (!visible)
                {
                    output.positionCS = float4(asfloat(~0u), 0.0, 0.0, 1.0); // NaN culls the quad, as VFX Graph does
                    return output;
                }

                positionOS.x *= _SampleParams.z;

                // Size in the hologram's local units: either a multiple of the gap between neighbouring
                // samples (which grows linearly with depth) or a fixed size.
                float sizeLocal = _SizeParams.y > 0.5 ? _SizeParams.x : _SizeParams.x * depth * _SizeParams.z;

                float4 positionCS = TransformWorldToHClip(TransformObjectToWorld(positionOS));

                // Convert to pixels for this eye/camera, clamp, then expand the quad in clip space so the
                // clamp is exact on screen.
                float pixelsPerUnit = abs(UNITY_MATRIX_P[1][1]) * 0.5 * _ScaledScreenParams.y / max(positionCS.w, 1e-5);
                float sizePixels = clamp(sizeLocal * _SizeParams.w * pixelsPerUnit, _ScreenSizeLimits.x, _ScreenSizeLimits.y);
                positionCS.xy += corner * sizePixels * positionCS.w / _ScaledScreenParams.xy;

                half3 recorded = LOAD_TEXTURE2D(_ColorTex, texel).rgb;
                half luminance = dot(recorded, half3(0.2126, 0.7152, 0.0722));
                half3 rgb = lerp(recorded, luminance.xxx, _ColorParams.x) * _HologramColor.rgb * (1.0 + _ColorParams.y);

                output.positionCS = positionCS;
                output.color = half4(rgb, _ColorParams.z);
                output.corner = corner;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = input.color;

                #if defined(_SHAPE_CIRCLE) || defined(_SHAPE_SOFT)
                    float radiusSq = dot(input.corner, input.corner);
                    clip(1.0 - radiusSq);
                    #if defined(_SHAPE_SOFT)
                        half falloff = 1.0 - radiusSq;
                        color.a *= falloff * falloff;
                    #endif
                #endif

                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
