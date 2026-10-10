Shader "Garden/Sky"
{
    Properties
    {
        _Horizon("Horizon", Color) = (.66,.68,.63,1)
        _Zenith("Zenith", Color) = (.29,.43,.52,1)
        _Glow("Glow", Color) = (.75,.67,.51,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Front
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Horizon;
                half4 _Zenith;
                half4 _Glow;
            CBUFFER_END
            struct Input
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Input input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                #if UNITY_REVERSED_Z
                output.positionCS.z = 0;
                #else
                output.positionCS.z = output.positionCS.w;
                #endif
                output.direction = normalize(input.positionOS.xyz);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half3 direction = normalize(input.direction);
                half height = saturate(direction.y * 1.5);
                half3 color = lerp(_Horizon.rgb, _Zenith.rgb, smoothstep(0, 1, height));
                half light = pow(saturate(dot(direction, normalize(half3(.8,.08,-.6)))), 10) * .48;
                return half4(lerp(color, _Glow.rgb, light), 1);
            }
            ENDHLSL
        }
    }
}
