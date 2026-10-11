Shader "RealityPlayground/Hologram"
{
    Properties
    {
        _BaseColor ("Hologram tint", Color) = (0.02,0.9,1,0.42)
        _Intensity ("Light intensity", Range(0.1,8)) = 2.5
        _ScanFrequency ("Scanline frequency", Range(5,160)) = 78
        _Glitch ("Signal interference", Range(0,1)) = 0.08
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Hologram"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Intensity;
                float _ScanFrequency;
                float _Glitch;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 p = input.positionOS.xyz;
                float strip = step(0.94, frac(sin(floor(p.y * 19 + _Time.y * 13) * 17.31) * 173.12));
                p.x += strip * sin(_Time.y * 47) * 0.045 * _Glitch;
                VertexPositionInputs pos = GetVertexPositionInputs(p);
                output.positionCS = pos.positionCS;
                output.positionWS = pos.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float rim = pow(1 - abs(dot(normalize(input.normalWS), view)), 2);
                float scan = pow(saturate(sin(input.positionWS.y * _ScanFrequency - _Time.y * 5)), 10);
                float sweep = pow(saturate(sin(input.positionWS.y * 3 - _Time.y * 2)), 24);
                float flicker = 1 - _Glitch * 0.15 * step(0.87, frac(sin(floor(_Time.y * 21) * 97.13) * 234.7));
                float alpha = saturate(_BaseColor.a * (0.4 + rim * 0.75 + scan * 0.45 + sweep * 0.3));
                return half4(_BaseColor.rgb * _Intensity * (0.55 + rim * 1.0 + scan * 0.7 + sweep) * flicker, alpha);
            }
            ENDHLSL
        }
    }
}
